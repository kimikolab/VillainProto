using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 検証用マップ 1-1 の画面（第169期）
//
// **判定は1つも持たない。** 進行の規則は `Map11State` にあり、この画面は
// 「どの隊をどの道へ送るか」を受け取って `Map11Session` を叩くだけ。
//
// 見た目は最小（板とラベルと数字）。演出・アセットは別担当なので `DemoApp/assets/` は触らない。
//
// 既存の自由移動の作戦マップ（`CampaignMain.tscn`）は**壊していない**——別のシーンで、
// `--campaign-*` の各スモークからは今までどおり入れる。
// =====================================================================================

public partial class Map11Main : Node3D
{
    private static readonly Color PlayerColor = Color.FromHtml("#55d5b8");
    private static readonly Color EnemyColor = Color.FromHtml("#e96951");
    private static readonly Color GoldColor = Color.FromHtml("#f1c96b");
    private static readonly Color DeadColor = Color.FromHtml("#6d7370");

    /// <summary>拠点。左端。</summary>
    // 第170期に左のパネルが横に広がったので、拠点をその右へ寄せた（札が隠れていた）。
    private static readonly Vector3 BasePos = new(-4.0f, 0, 2.0f);

    /// <summary>区画の位置（道 × 番号）。北の道が奥（-Z）、南の道が手前（+Z）。</summary>
    private static Vector3 NodePos(int road, int index)
        => new(3.0f + index * 11.0f, 0, road == 0 ? -8.5f : 8.5f);

    private Camera3D _camera = null!;
    private Label3D _homeLabel = null!;
    private readonly List<Marker> _squadMarkers = new();
    private readonly List<Marker> _nodeMarkers = new();

    private VBoxContainer _squadList = null!;
    private VBoxContainer _roadList = null!;
    private Label _headline = null!;
    private Label _toast = null!;
    private Button _sendNorth = null!;
    private Button _sendSouth = null!;
    private Label _hint = null!;

    private VBoxContainer _squadDetail = null!;
    private VBoxContainer _foeDetail = null!;
    private VBoxContainer _history = null!;

    // ---- 第171期 部A: 配置の図 ----
    private SeatDiagram _squadDiagram = null!;
    private SeatDiagram _foeDiagram = null!;
    private VBoxContainer _squadFocus = null!;
    private VBoxContainer _foeFocus = null!;
    private Control _squadDetailBox = null!;
    private Control _foeDetailBox = null!;
    private Button _squadMore = null!;
    private Button _foeMore = null!;

    /// <summary>図で選んでいる席（-1 ＝ なし）。<b>隊／部隊を替えたら外す。</b></summary>
    private int _squadPick = -1;
    private int _foePick = -1;

    // ---- 第172期 部A: 拠点での組み直し ----

    /// <summary>組み直しの最中か。<b>拠点にいる隊だけ</b>（判定は `Map11State.CanReform`）。</summary>
    private bool _reform;

    /// <summary>つまんでいる駒。席のときは（隊, 席）、控えのときは番号。どちらも -1 ＝ 何も持っていない。</summary>
    private int _holdSquad = -1;
    private int _holdSlot = -1;
    private int _holdBench = -1;

    private bool Holding => _holdSlot >= 0 || _holdBench >= 0;

    private ScrollContainer _leftScroll = null!;
    private Button _reformButton = null!;
    private Button _resetButton = null!;
    private VBoxContainer _benchList = null!;
    private Label _reformHint = null!;

    /// <summary>右のパネルで中身を開いている敵の区画（道, 番号）。既定は北の 1 戦目。</summary>
    private (int Road, int Index) _foeView = (0, 0);

    private Control _encounterOverlay = null!;
    private Label _encounterTitle = null!;
    private RichTextLabel _encounterBody = null!;

    private Control _resultOverlay = null!;
    private Label _resultTitle = null!;
    private Label _resultBody = null!;

    private int _selected;
    private float _toastClock;
    private readonly List<Button> _squadButtons = new();

    private static Map11State St => Map11Session.State!;

    // =================================================================================
    // 組み立て
    // =================================================================================

    public override void _Ready()
    {
        Map11Session.EnsureStarted();

        BuildWorld();
        BuildUi();
        Refresh();

        if (Map11Session.LastOutcome is { } outcome)
        {
            ShowToast(outcome, GoldColor, 6.0f);
            Map11Session.LastOutcome = null;
        }
        else ShowToast("隊を選び、送り出す道を決めてください", GoldColor, 6.0f);

        if (St.Finished) ShowResult();

        // 第169期 自己検査 (d')。**画面を通した通し確認**（頭なしで走る）。
        // 判断だけを器具と同じ固定の割り当てで代行し、戦闘は本物の再生を通す。
        // `_Ready` の中でシーンを替えると木の再入で落ちるので、必ず遅らせる。
        string[] args = OS.GetCmdlineUserArgs();
        // 第172期 自己検査 (c)。**1 回組み直してから出す**通し（組み直した隊で戦って、
        // 持ち越しと結果画面まで通ることを確かめる）。
        bool reformFirst = args.Contains("--map11-reform-smoke", StringComparer.Ordinal);
        if (args.Contains("--map11-flow-smoke", StringComparer.Ordinal) || reformFirst)
        {
            if (reformFirst) AutoReform();
            Callable.From(AutoStep).CallDeferred();
        }
        else if (args.FirstOrDefault(a => a.StartsWith("--map11-capture=", StringComparison.Ordinal))
                 is { } shot)
            _ = Capture(shot["--map11-capture=".Length..]);
    }

    /// <summary>
    /// 画面の当たりを1枚だけ撮る（見た目の確認用。判定には一切使わない）。
    /// 書式は <c>--map11-capture=&lt;path&gt;[,detail|encounter]</c>。
    /// </summary>
    private async System.Threading.Tasks.Task Capture(string spec)
    {
        string[] parts = spec.Split(',');
        string path = parts[0];
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (parts.Length > 1 && parts[1] == "encounter") Send(1);
        // 第171期: 図の札を1枚選んだ絵（関係の線と「何をする」が出ている状態）。
        else if (parts.Length > 1 && parts[1] == "pick") { _squadPick = 3; _foePick = 2; Refresh(); }
        // 第172期: 組み直しの絵（つまんでいる札と控えの駒が出ている状態）。
        else if (parts.Length > 1 && parts[1] == "reform")
        {
            ToggleReform();
            OnSquadSlot(3);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _leftScroll.ScrollVertical = 10000;
        }
        await ToSignal(GetTree().CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
        Error error = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"MAP11_CAPTURE error={error} path={path}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    /// <summary>
    /// 通し確認の1歩。正の割り当て（カド隊 → 南 ／ ハネ隊 → 北、控えは空いた道）で
    /// 動ける隊を1つ選び、接敵させて戦闘シーンへ送り出す。
    /// </summary>
    private void AutoStep()
    {
        if (St.Finished)
        {
            GD.Print($"MAP11_FLOW_SMOKE_COMPLETE won={St.Won} cleared={St.ClearedCount}"
                + $"/{Map11.TotalNodes} battles={St.Battles} seed={Map11Session.Seed}");
            // 戦闘の記録と、盤面ルールが実際にしたこと（第170期 §1-4）をそのまま吐く。
            foreach (Map11Session.Line l in Map11Session.BattleLog)
            {
                GD.Print("MAP11_LOG " + HistoryLine(l));
                foreach (string note in l.Notes) GD.Print("MAP11_LOG     " + note);
            }
            GD.Print($"MAP11_LOG 盤上 {Map11Session.AliveOnMap} 枚 / 未出撃を含む {Map11Session.AliveIncludingReserve} 枚");
            // 通しの最後の絵も撮れるようにしておく（`--map11-capture=` を併せて渡したときだけ）。
            if (OS.GetCmdlineUserArgs()
                    .FirstOrDefault(a => a.StartsWith("--map11-capture=", StringComparison.Ordinal))
                is { } shot) { _ = Capture(shot["--map11-capture=".Length..]); return; }
            GetTree().Quit();
            return;
        }

        int[] home = { 1, 0, -1 };
        for (int i = 0; i < St.Squads.Length; i++)
        {
            Map11State.Squad s = St.Squads[i];
            if (s.Lost) continue;
            int road = s.Road >= 0 ? s.Road : home[i];
            if (road < 0 || St.NextNode(road) is null)
            {
                road = -1;
                for (int r = 0; r < Map11.RoadCount; r++)
                    if (St.NextNode(r) is not null) { road = r; break; }
                if (road < 0) break;
            }
            _selected = i;
            Map11Session.Send(i, road);
            if (!Map11Session.BeginBattle(i)) continue;
            GD.Print($"MAP11_FLOW_SMOKE_STEP squad={s.Def.Name} road={Map11.RoadNames[road]}"
                + $" battles={St.Battles}");
            GetTree().ChangeSceneToFile(CampaignSession.BattleScene);
            return;
        }

        GD.Print($"MAP11_FLOW_SMOKE_COMPLETE won={St.Won} cleared={St.ClearedCount}"
            + $"/{Map11.TotalNodes} battles={St.Battles} seed={Map11Session.Seed} (no move)");
        GetTree().Quit();
    }

    /// <summary>
    /// 自己検査 (c) の組み直し。<b>頭なしで 3 つの操作を1回ずつ通す</b>——
    /// 同じ隊の席の入れ替え ／ 隊どうしの入れ替え ／ 控えの駒との交代。
    /// </summary>
    private void AutoReform()
    {
        bool a = St.SwapSeats(0, 0, 0, 3);                 // カド隊の 前1 ↔ 後1
        bool b = St.SwapSeats(0, 1, 1, 4);                 // カド隊 前3 ↔ かき回し隊 後3
        bool c = St.SwapWithBench(0, 2, 0);                // カド隊 中央 ↔ 控えの 1 枚目
        GD.Print($"MAP11_REFORM_SMOKE seat={a} across={b} bench={c}");
        for (int i = 0; i < St.Squads.Length; i++)
        {
            Map11State.Squad sq = St.Squads[i];
            string body = sq.Units is { } u
                ? string.Join(" / ", u.OrderBy(x => x.Slot)
                    .Select(x => $"{FormationRules.SeatNames[x.Slot]} {x.Def.Name}"))
                : "（定義のまま）";
            GD.Print($"MAP11_REFORM_SMOKE {sq.Def.Name}: {body}");
        }
        GD.Print("MAP11_REFORM_SMOKE 控え: "
            + string.Join(" / ", St.Bench.Select(x => x.Def.Name)));
    }

    private void BuildWorld()
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = Color.FromHtml("#6d8790"),
            SkyHorizonColor = Color.FromHtml("#cfc5a6"),
            GroundBottomColor = Color.FromHtml("#243a31"),
            GroundHorizonColor = Color.FromHtml("#a7a37f"),
        };
        AddChild(new WorldEnvironment
        {
            Environment = new Godot.Environment
            {
                BackgroundMode = Godot.Environment.BGMode.Sky,
                Sky = new Sky { SkyMaterial = sky },
                AmbientLightSource = Godot.Environment.AmbientSource.Color,
                AmbientLightColor = Color.FromHtml("#a7beb3"),
                AmbientLightEnergy = 0.8f,
                TonemapMode = Godot.Environment.ToneMapper.Filmic,
            },
        });
        AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-52, -36, 0),
            LightEnergy = 1.05f,
        });

        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(90, 60) },
            MaterialOverride = CampaignMain.MakeMaterial(Color.FromHtml("#557343"), roughness: 0.96f),
        });

        // 道（拠点 → 区画0 → 区画1 の帯）。位置を教えるだけの板。
        for (int road = 0; road < Map11.RoadCount; road++)
        {
            Vector3 from = BasePos;
            for (int i = 0; i < Map11.Roads[road].Count; i++)
            {
                Vector3 to = NodePos(road, i);
                AddStrip(from, to, Color.FromHtml("#8b7756"));
                from = to;
            }
        }

        _camera = new Camera3D { Position = new Vector3(8.0f, 31.0f, 26.0f), Fov = 50 };
        AddChild(_camera);
        _camera.LookAt(new Vector3(8.0f, 0, 0), Vector3.Up);

        // 拠点
        var home = new Node3D { Position = BasePos };
        AddChild(home);
        home.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 2.4f, BottomRadius = 2.7f, Height = 0.4f, RadialSegments = 12 },
            Position = new Vector3(0, 0.2f, 0),
            MaterialOverride = CampaignMain.MakeMaterial(Color.FromHtml("#6a6b58"), roughness: 0.95f),
        });
        _homeLabel = WorldLabel("拠点", new Vector3(2.4f, 0.9f, 3.1f), 30, PlayerColor);
        home.AddChild(_homeLabel);

        for (int road = 0; road < Map11.RoadCount; road++)
            for (int i = 0; i < Map11.Roads[road].Count; i++)
            {
                var m = new Marker(this, NodePos(road, i), EnemyColor, 2.0f);
                _nodeMarkers.Add(m);
            }

        for (int s = 0; s < St.Squads.Length; s++)
            _squadMarkers.Add(new Marker(this, BasePos, PlayerColor, 1.3f));
    }

    private void AddStrip(Vector3 from, Vector3 to, Color color)
    {
        Vector3 mid = (from + to) * 0.5f;
        float len = from.DistanceTo(to);
        var node = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.6f, 0.04f, len) },
            Position = new Vector3(mid.X, 0.03f, mid.Z),
            MaterialOverride = CampaignMain.MakeMaterial(color, roughness: 1.0f),
        };
        node.LookAtFromPosition(node.Position, new Vector3(to.X, 0.03f, to.Z), Vector3.Up);
        AddChild(node);
    }

    private static Label3D WorldLabel(string text, Vector3 offset, int size, Color color) => new()
    {
        Text = text,
        Position = offset,
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        Font = new SystemFont
        {
            FontNames = new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "Segoe UI" },
            AllowSystemFallback = true,
        },
        FontSize = size,
        PixelSize = 0.021f,
        Modulate = color,
        OutlineModulate = new Color(0.01f, 0.015f, 0.012f, 0.95f),
        OutlineSize = 8,
        NoDepthTest = true,
    };

    /// <summary>盤上の札（板1枚 ＋ 文字）。<b>数字を出すためだけの物</b>。</summary>
    private sealed class Marker
    {
        private readonly MeshInstance3D _disc;
        private readonly Label3D _label;
        private readonly StandardMaterial3D _material;
        public Node3D Root { get; }

        public Marker(Node parent, Vector3 position, Color color, float radius)
        {
            Root = new Node3D { Position = position };
            parent.AddChild(Root);
            _material = CampaignMain.MakeMaterial(color, roughness: 0.7f, emission: color * 0.25f);
            _disc = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 0.3f, RadialSegments = 20 },
                Position = new Vector3(0, 0.16f, 0),
                MaterialOverride = _material,
            };
            Root.AddChild(_disc);
            _label = WorldLabel("", new Vector3(0, radius + 1.9f, 0), 28, Colors.White);
            Root.AddChild(_label);
        }

        public void Set(string text, Color color, bool visible = true)
        {
            _label.Text = text;
            _material.AlbedoColor = color;
            _material.Emission = color * 0.25f;
            _disc.Visible = visible;
            _label.Visible = visible;
        }

        public void MoveTo(Vector3 position) => Root.Position = position;
    }

    // =================================================================================
    // UI
    // =================================================================================

    private void BuildUi()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        var root = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Theme = new Theme
            {
                DefaultFont = new SystemFont
                {
                    FontNames = new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "Segoe UI" },
                    AllowSystemFallback = true,
                },
                DefaultFontSize = 14,
            },
        };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);

        _headline = UiKit.Text("検証用マップ 1-1", 22, UiKit.Ink);
        _headline.Position = new Vector2(24, 18);
        _headline.Size = new Vector2(900, 30);
        root.AddChild(_headline);

        _toast = UiKit.Text("", 15, GoldColor);
        _toast.Position = new Vector2(24, 52);
        _toast.Size = new Vector2(900, 26);
        root.AddChild(_toast);

        // ---- 左: 隊 ----
        // **高さを画面の中に止め、中身は縦に流す**（第172期 §1-3）——第171期の観察ログ
        // 「くわしくを押したが**下側で見切れて読めない**」（問い5）は、パネル自身が
        // 画面の下へ伸びていたため。中の欄だけをスクロールにしても外側が溢れていた。
        PanelContainer left = Panel();
        left.AnchorTop = 0;
        left.AnchorBottom = 1;
        left.OffsetLeft = 20;
        left.OffsetRight = 20 + 352;
        left.OffsetTop = 90;
        left.OffsetBottom = -20;
        root.AddChild(left);
        var leftCol = new VBoxContainer();
        leftCol.AddThemeConstantOverride("separation", 8);
        leftCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _leftScroll = Scroll(leftCol, 0);
        left.AddChild(_leftScroll);
        _hint = UiKit.Text("① 隊を選ぶ → ② 道を選ぶ → ③ 接敵したら「戦う」か「引き返す」", 13, GoldColor);
        _hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        leftCol.AddChild(_hint);
        leftCol.AddChild(UiKit.Text("部隊", 12, UiKit.Faint));
        _squadList = new VBoxContainer();
        _squadList.AddThemeConstantOverride("separation", 6);
        leftCol.AddChild(_squadList);

        for (int i = 0; i < St.Squads.Length; i++)
        {
            int index = i;
            var button = new Button
            {
                CustomMinimumSize = new Vector2(0, 74),
                Alignment = HorizontalAlignment.Left,
                FocusMode = Control.FocusModeEnum.None,
            };
            button.Pressed += () => Select(index);
            _squadList.AddChild(button);
            _squadButtons.Add(button);
        }

        var orders = new HBoxContainer();
        orders.AddThemeConstantOverride("separation", 8);
        leftCol.AddChild(orders);
        _sendNorth = UiKit.ActionButton("北の道へ", PlayerColor);
        _sendNorth.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _sendNorth.Pressed += () => Send(0);
        orders.AddChild(_sendNorth);
        _sendSouth = UiKit.ActionButton("南の道へ", PlayerColor);
        _sendSouth.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _sendSouth.Pressed += () => Send(1);
        orders.AddChild(_sendSouth);

        // 第171期 部A —— **文章ではなく図。**
        // 第170期の観察ログ「隊の中身、文章が長すぎて毎回読んでいられない」（問い5）と
        // 「どう配置されているかが良く見ないと分からない」（問い2）は同じことの表裏で、
        // **足りないのは量ではなく形**。説明文は札を押したときだけ下に出す。
        // 第172期 部A —— **図をそのまま編成の道具にする。**
        // 観察ログ「編成UIにこれは欲しい」（第171期 問い1）と、2 期続けて出た
        // 「カドを動かしたい・ヒヨを替えたい」（問い6）への答え。
        var reformRow = new HBoxContainer();
        reformRow.AddThemeConstantOverride("separation", 8);
        leftCol.AddChild(reformRow);
        _reformButton = UiKit.ActionButton("組み直す", GoldColor);
        _reformButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _reformButton.Pressed += ToggleReform;
        reformRow.AddChild(_reformButton);
        _resetButton = UiKit.ActionButton("元に戻す", UiKit.Muted);
        _resetButton.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _resetButton.Pressed += ResetSquad;
        reformRow.AddChild(_resetButton);
        _reformHint = UiKit.Text("", 12, GoldColor);
        _reformHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        leftCol.AddChild(_reformHint);

        leftCol.AddChild(UiKit.Text("この隊の配置（押すと、その駒が何をするか出ます）", 12, UiKit.Faint));
        _squadDiagram = new SeatDiagram();
        _squadDiagram.SlotSelected += OnSquadSlot;
        leftCol.AddChild(_squadDiagram);
        _squadFocus = new VBoxContainer();
        _squadFocus.AddThemeConstantOverride("separation", 3);
        // **高さを止める。** `PlusText` は長い駒がいる（カドは2文）ので、
        // そのまま伸ばすと左のパネルが画面の下へ抜ける（第170期の「長すぎる」の再発になる）。
        leftCol.AddChild(Scroll(_squadFocus, 128));

        // 控えの駒（第172期 §1-2）。**組み直しの最中だけ押せる。**
        leftCol.AddChild(UiKit.Text("控えの駒（拠点で交代できます）", 12, UiKit.Faint));
        _benchList = new VBoxContainer();
        _benchList.AddThemeConstantOverride("separation", 4);
        leftCol.AddChild(Scroll(_benchList, 116));

        // 第170期の文章のパネルは**畳む**（消さない）。
        _squadMore = UiKit.ActionButton("くわしく", UiKit.Muted);
        _squadMore.Pressed += () => { _squadDetailBox.Visible = !_squadDetailBox.Visible; Refresh(); };
        leftCol.AddChild(_squadMore);
        _squadDetail = new VBoxContainer();
        _squadDetail.AddThemeConstantOverride("separation", 4);
        _squadDetailBox = Scroll(_squadDetail, 230);
        _squadDetailBox.Visible = false;
        leftCol.AddChild(_squadDetailBox);

        // ---- 右: 道 ----
        PanelContainer right = Panel();
        right.AnchorLeft = 1;
        right.AnchorRight = 1;
        right.OffsetLeft = -430;
        right.OffsetRight = -20;
        right.OffsetTop = 90;
        right.AnchorBottom = 1;
        right.OffsetBottom = -20;
        root.AddChild(right);
        var rightCol = new VBoxContainer();
        rightCol.AddThemeConstantOverride("separation", 8);
        rightCol.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        right.AddChild(Scroll(rightCol, 0));
        rightCol.AddChild(UiKit.Text("道と敵部隊（押すと中身が出ます）", 12, UiKit.Faint));
        _roadList = new VBoxContainer();
        _roadList.AddThemeConstantOverride("separation", 6);
        rightCol.AddChild(_roadList);
        // 第171期 部A —— 敵も同じ図。**空席は空席として描く**（先遣は後1 が空いているのが見える）。
        rightCol.AddChild(UiKit.Text("この部隊の配置（押すと、その駒が何をするか出ます）", 12, UiKit.Faint));
        _foeDiagram = new SeatDiagram();
        _foeDiagram.SlotSelected += slot => { _foePick = _foePick == slot ? -1 : slot; Refresh(); };
        rightCol.AddChild(_foeDiagram);
        _foeFocus = new VBoxContainer();
        _foeFocus.AddThemeConstantOverride("separation", 3);
        rightCol.AddChild(Scroll(_foeFocus, 128));

        _foeMore = UiKit.ActionButton("くわしく", UiKit.Muted);
        _foeMore.Pressed += () => { _foeDetailBox.Visible = !_foeDetailBox.Visible; Refresh(); };
        rightCol.AddChild(_foeMore);
        _foeDetail = new VBoxContainer();
        _foeDetail.AddThemeConstantOverride("separation", 4);
        _foeDetailBox = Scroll(_foeDetail, 230);
        _foeDetailBox.Visible = false;
        rightCol.AddChild(_foeDetailBox);

        // ---- 下中央: 戦闘の記録（**戦闘1回につき1行**） ----
        PanelContainer bottom = Panel();
        bottom.AnchorTop = 1;
        bottom.AnchorBottom = 1;
        bottom.OffsetTop = -210;
        bottom.OffsetBottom = -20;
        bottom.OffsetLeft = 470;
        bottom.OffsetRight = -450;
        root.AddChild(bottom);
        var bottomCol = new VBoxContainer();
        bottomCol.AddThemeConstantOverride("separation", 6);
        bottom.AddChild(bottomCol);
        bottomCol.AddChild(UiKit.Text("戦闘の記録", 12, UiKit.Faint));
        _history = new VBoxContainer();
        _history.AddThemeConstantOverride("separation", 3);
        bottomCol.AddChild(Scroll(_history, 150));

        BuildEncounterOverlay(root);
        BuildResultOverlay(root);
    }

    /// <summary>中身が溢れても読めるように包む。</summary>
    private static ScrollContainer Scroll(Control content, int height)
    {
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, height),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        scroll.AddChild(content);
        return scroll;
    }

    private static PanelContainer Panel()
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.03f, 0.05f, 0.043f, 0.94f), UiKit.Line));
        return panel;
    }

    private Control Modal(Control root, int halfWidth, int halfHeight,
                          out VBoxContainer column, string tag, Color tagColor)
    {
        var overlay = new ColorRect
        {
            Color = new Color(0.005f, 0.01f, 0.008f, 0.78f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        overlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(overlay);
        PanelContainer modal = Panel();
        modal.AnchorLeft = modal.AnchorRight = 0.5f;
        modal.AnchorTop = modal.AnchorBottom = 0.5f;
        modal.OffsetLeft = -halfWidth;
        modal.OffsetRight = halfWidth;
        modal.OffsetTop = -halfHeight;
        modal.OffsetBottom = halfHeight;
        overlay.AddChild(modal);
        column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        modal.AddChild(column);
        column.AddChild(UiKit.Text(tag, 11, tagColor));
        return overlay;
    }

    private void BuildEncounterOverlay(Control root)
    {
        _encounterOverlay = Modal(root, 360, 200, out VBoxContainer col, "ENCOUNTER", EnemyColor);
        _encounterTitle = UiKit.Text("", 24, Colors.White);
        col.AddChild(_encounterTitle);
        // 第171期 §1-3 ——「決める瞬間の隣」に、**隊の勝ち筋**と**敵のルール**を上下に並べる。
        // **同じ語は同じ色**（ターン外の行動／回復／1発）。**良し悪しの語は足さない**（案B のまま）。
        _encounterBody = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = true,
            ScrollActive = false,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _encounterBody.AddThemeFontSizeOverride("normal_font_size", 14);
        _encounterBody.AddThemeColorOverride("default_color", Color.FromHtml("#ccd5cb"));
        col.AddChild(_encounterBody);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        col.AddChild(row);
        Button back = UiKit.ActionButton("拠点へ引き返す");
        back.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        back.Pressed += Withdraw;
        row.AddChild(back);
        Button fight = UiKit.ActionButton("戦う", GoldColor);
        fight.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        fight.Pressed += Fight;
        row.AddChild(fight);
    }

    private void BuildResultOverlay(Control root)
    {
        _resultOverlay = Modal(root, 340, 220, out VBoxContainer col, "RESULT", GoldColor);
        _resultTitle = UiKit.Text("", 26, Colors.White);
        col.AddChild(_resultTitle);
        _resultBody = UiKit.Text("", 14, Color.FromHtml("#ccd5cb"));
        _resultBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _resultBody.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(_resultBody);
        Button again = UiKit.ActionButton("もう一度", GoldColor);
        again.Pressed += () =>
        {
            Map11Session.Reset();
            GetTree().ReloadCurrentScene();
        };
        col.AddChild(again);
    }

    // =================================================================================
    // 操作
    // =================================================================================

    private void Select(int index)
    {
        // 隊を替えたら図の選択は外す（前の隊の席が選ばれたまま残ると読み違える）。
        if (_selected != index) _squadPick = -1;
        _selected = index;
        Refresh();
    }

    private void Send(int road)
    {
        if (St.Finished) return;
        Map11State.Squad s = St.Squads[_selected];
        if (s.Lost) { ShowToast($"{s.Def.Name} は失われています", EnemyColor, 3.0f); return; }
        // **0 枚の隊は出せない**（指示書 §1-1）。組み直しで空にしたときだけ起きる。
        if (!St.CanSend(_selected)) { ShowToast($"{s.Def.Name} は 0 枚です（1 枚以上にしてください）", EnemyColor, 3.0f); return; }
        if (_reform) { _reform = false; ClearHold(); }
        if (St.NextNode(road) is null) { ShowToast($"{Map11.RoadNames[road]} は抜け切っています", UiKit.Muted, 3.0f); return; }

        Map11Session.Send(_selected, road);
        // 送った先の中身を右に出しておく（**隊の勝ち筋と敵のルールを並べて見せる**・案B）。
        if (St.NextNode(road) is { } next) { if (_foeView != (road, next.Def.Index)) _foePick = -1; _foeView = (road, next.Def.Index); }
        ShowToast($"{s.Def.Name} を {Map11.RoadNames[road]} へ", PlayerColor, 3.0f);
        Refresh();
        TryEncounter();
    }

    /// <summary>
    /// 接敵。<b>同じ道に2隊が向かっていても、先に着いた隊しか戦わない</b>
    /// ——`NextNode` は道ごとに1つしか返さないので、1区画に2隊が同時に当たることは起きない。
    /// </summary>
    private void TryEncounter()
    {
        if (St.Finished) { ShowResult(); return; }
        Map11State.Squad s = St.Squads[_selected];
        if (s.Units is null || s.Road < 0) return;
        if (St.NextNode(s.Road) is not { } node) return;

        int alive = node.Units?.Count(u => u.IsAlive) ?? node.Def.Enemy.Occupied().Count();
        _encounterTitle.Text = $"{s.Def.Name} × {node.Def.Name}";
        string plan = Map11Info.PlanOf(s.Def.Id).Replace("**", "");
        _encounterBody.Text =
            $"[color=#8f9b90]{Map11.RoadNames[node.Def.Road]} {node.Def.Index + 1} 戦目。"
            + "戦えば、傷も死者もそのまま持ち込みます。[/color]\n\n"
            + $"[color=#{PlayerColor.ToHtml(false)}]この隊の勝ち筋[/color]"
            + $"　[color=#8f9b90]{s.Units.Count(u => u.IsAlive)} 体 ・ HP {SquadHpPercent(s):F0}%[/color]\n"
            + Shared(plan) + "\n\n"
            + $"[color=#{EnemyColor.ToHtml(false)}]この部隊のルール[/color]"
            + $"　[color=#8f9b90]{alive} 体 ・ HP {NodeHpPercent(node):F0}%[/color]\n"
            + Shared(Map11.RuleLineOf(node.Def.Enemy));
        _encounterOverlay.Visible = true;
    }

    /// <summary>
    /// 勝ち筋と敵のルールで<b>同じ語を同じ色にする</b>（第171期 §1-3）。
    ///
    /// <para>対になっているのは <b>ターン外の行動 ＝ 粛 ／ 回復 ＝ 渇き ／ 1発 ＝ 軛</b> の3組。
    /// <b>語そのものは第170期が既に揃えてある</b>（`Map11Info.Plans` と `Map11.RuleLineOf`）ので、
    /// この期に足したのは<b>色と、並べる場所</b>だけ——揃っているのに気づけない、が問い1 の答えだった。</para>
    ///
    /// <para><b>良し悪しの語は足さない</b>（案B）。「困りそうだ」も「相性が悪い」も書かない。</para>
    /// </summary>
    private static readonly (string Word, string Color)[] SharedWords =
    {
        ("ターン外の行動", "#bf91f3"),
        ("回復", "#71d7a1"),
        ("1発", "#efc66a"),
    };

    private static string Shared(string text)
    {
        foreach ((string word, string color) in SharedWords)
            text = text.Replace(word, $"[b][color={color}]{word}[/color][/b]", StringComparison.Ordinal);
        return text;
    }

    private void Withdraw()
    {
        _encounterOverlay.Visible = false;
        Map11State.Squad s = St.Squads[_selected];
        s.Road = -1;
        ShowToast($"{s.Def.Name} は拠点へ引き返した", UiKit.Muted, 3.0f);
        Refresh();
    }

    private void Fight()
    {
        _encounterOverlay.Visible = false;
        if (!Map11Session.BeginBattle(_selected)) { ShowToast("出撃できません", EnemyColor, 3.0f); return; }
        GetTree().ChangeSceneToFile(CampaignSession.BattleScene);
    }

    // =================================================================================
    // 第172期 部A —— 拠点での組み直し
    //
    // **画面は判定を1つも持たない。** 「動かせるか」は `Map11State.CanReform` / `SwapSeats` /
    // `SwapWithBench` / `ResetSquad` が全部返す——ここでするのは
    // 「どの札を押したか」を渡して、結果を引き直すことだけ。
    // =================================================================================

    private void ClearHold() { _holdSquad = -1; _holdSlot = -1; _holdBench = -1; }

    private void ToggleReform()
    {
        if (!_reform && !St.CanReform(_selected))
        {
            ShowToast($"{St.Squads[_selected].Def.Name} は拠点にいません（道の上の隊は組み直せません）",
                      EnemyColor, 3.0f);
            return;
        }
        _reform = !_reform;
        ClearHold();
        ShowToast(_reform
            ? "組み直し中——札を2枚押すと入れ替わります（控えの駒も同じ）"
            : "組み直しを終えました", GoldColor, 3.0f);
        Refresh();
    }

    private void ResetSquad()
    {
        Map11State.Squad s = St.Squads[_selected];
        if (!St.CanReform(_selected))
        {
            ShowToast($"{s.Def.Name} は拠点にいません", EnemyColor, 3.0f);
            return;
        }
        ClearHold();
        // **拠点にある駒からしか集められない**（道の上・全滅した隊の駒は戻せない）。
        bool done = St.ResetSquad(_selected);
        ShowToast(done
            ? $"{s.Def.Name} を既定の編成へ戻しました"
            : $"{s.Def.Name} の駒が拠点に揃っていません（戻せません）",
            done ? PlayerColor : EnemyColor, 3.0f);
        Refresh();
    }

    /// <summary>
    /// 図の札を押した。<b>組み直し中は「1 枚目でつまみ、2 枚目で入れ替える」</b>
    /// ——ドラッグではなく2回押しにしたのは、同じ札が「中身を見る」と
    /// 「つまむ」の両方を担うため（指示書 §1-3 が許している形）。
    /// </summary>
    private void OnSquadSlot(int slot)
    {
        if (!_reform)
        {
            _squadPick = _squadPick == slot ? -1 : slot;
            Refresh();
            return;
        }
        if (!St.CanReform(_selected))
        {
            ShowToast($"{St.Squads[_selected].Def.Name} は拠点にいません", EnemyColor, 3.0f);
            return;
        }

        if (_holdBench >= 0)
        {
            if (!St.SwapWithBench(_selected, slot, _holdBench))
                ShowToast("その席へは入れられません", EnemyColor, 3.0f);
            ClearHold();
        }
        else if (_holdSlot >= 0)
        {
            if (_holdSquad == _selected && _holdSlot == slot) ClearHold();
            else
            {
                if (!St.SwapSeats(_holdSquad, _holdSlot, _selected, slot))
                    ShowToast("その2席は入れ替えられません（拠点にいる隊だけです）", EnemyColor, 3.0f);
                ClearHold();
            }
        }
        else
        {
            if (St.PickUp(_selected, slot) is null)
            {
                ShowToast("空席です。先に動かしたい駒か控えの駒を押してください", UiKit.Muted, 3.0f);
                return;
            }
            _holdSquad = _selected;
            _holdSlot = slot;
        }
        _squadPick = slot;
        Refresh();
    }

    private void OnBenchPressed(int index)
    {
        if (!_reform) { ShowToast("「組み直す」を押してから選んでください", UiKit.Muted, 3.0f); return; }
        if (_holdSlot >= 0)
        {
            if (!St.SwapWithBench(_holdSquad, _holdSlot, index))
                ShowToast("その駒は動かせません", EnemyColor, 3.0f);
            ClearHold();
        }
        else if (_holdBench == index) ClearHold();
        else if (index < St.Bench.Count) _holdBench = index;
        else ShowToast("先に席の駒を押してください（ここへ下げられます）", UiKit.Muted, 3.0f);
        Refresh();
    }

    /// <summary>控えの駒の一覧。<b>末尾に「ここへ下げる」の枠</b>（席を空にする操作）。</summary>
    private void RefreshBench()
    {
        foreach (Node child in _benchList.GetChildren()) child.QueueFree();
        for (int i = 0; i < St.Bench.Count; i++)
        {
            UnitState u = St.Bench[i];
            int index = i;
            var b = new Button
            {
                Text = $"{u.Def.Name}   HP {u.Hp}/{u.MaxHp} ・ 攻{u.Def.Attack} ・ 速{u.Def.Speed}",
                Alignment = HorizontalAlignment.Left,
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(0, 26),
                Disabled = !_reform,
                Modulate = _holdBench == index ? new Color(1.0f, 0.90f, 0.62f) : Colors.White,
            };
            b.AddThemeFontSizeOverride("font_size", 12);
            b.Pressed += () => OnBenchPressed(index);
            _benchList.AddChild(b);
        }
        int tail = St.Bench.Count;
        var down = new Button
        {
            Text = "＋ ここへ下げる（席を空ける）",
            Alignment = HorizontalAlignment.Left,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 26),
            Disabled = !_reform || _holdSlot < 0,
        };
        down.AddThemeFontSizeOverride("font_size", 12);
        down.Pressed += () => OnBenchPressed(tail);
        _benchList.AddChild(down);
    }

    /// <summary>組み直しの案内（いま何ができるか・何をつまんでいるか）。</summary>
    private void RefreshReform()
    {
        bool can = St.CanReform(_selected) && !St.Finished;
        _reformButton.Disabled = !can && !_reform;
        _reformButton.Text = _reform ? "組み直しを終える" : "組み直す";
        _resetButton.Disabled = !can;

        string held = _holdBench >= 0 && _holdBench < St.Bench.Count
            ? $"控えの {St.Bench[_holdBench].Def.Name}"
            : _holdSlot >= 0 && St.UnitAt(_holdSquad, _holdSlot) is { } u
                ? $"{St.Squads[_holdSquad].Def.Name} {FormationRules.SeatNames[_holdSlot]} {u.Def.Name}"
                : "";
        _reformHint.Text = !_reform
            ? (can ? "" : $"{St.Squads[_selected].Def.Name} は道の上です（拠点の隊だけ組み直せます）")
            : held.Length > 0
                ? $"つまんでいる: {held} —— もう1枚押すと入れ替わります"
                : "動かしたい札（または控えの駒）を押してください";
        _reformHint.AddThemeColorOverride("font_color", can || _reform ? GoldColor : UiKit.Faint);
    }

    // =================================================================================
    // 第171期 部A —— 配置の図
    //
    // **席の意味は `FormationRules` から引くだけ**で、写しを持たない。
    // 図そのものは `SeatDiagram`（判定を1つも持たない Control）。
    // =================================================================================

    /// <summary>隊の 5 席を図にする。盤上に出ていればその現在値、まだなら定義の値。</summary>
    private void RenderSquadDiagram(Map11State.Squad s)
    {
        var cells = new List<SeatDiagram.Cell>();
        if (s.Units is { } units)
            foreach (UnitState u in units.Where(u => !u.Def.Traits.Contains(TraitId.Ephemeral)))
                cells.Add(new SeatDiagram.Cell(u.Slot, u.Def, Math.Max(0, u.Hp), u.MaxHp, u.IsAlive));
        else
            foreach ((int slot, UnitDef def) in s.Def.F.Occupied())
                cells.Add(new SeatDiagram.Cell(slot, def, def.MaxHp, def.MaxHp, true));

        // **空席も描く**（第172期）——組み直しで控えの駒を入れられる場所だから。
        foreach (int slot in Enumerable.Range(0, FormationRules.PlayableSlotCount))
            if (cells.All(c => c.Slot != slot))
                cells.Add(new SeatDiagram.Cell(slot, null, 0, 0, false));
        cells.Sort((x, y) => x.Slot.CompareTo(y.Slot));

        _squadDiagram.AllowEmptyPress = _reform;
        _squadDiagram.Render(cells, PlayerColor, _squadPick);

        foreach (Node child in _squadFocus.GetChildren()) child.QueueFree();
        if (_squadPick < 0 || cells.First(c => c.Slot == _squadPick).Def is null)
        {
            FocusHint(_squadFocus, _reform
                ? "札を押すとつまみます。もう1枚押すと入れ替わります"
                : s.Units is null && !s.Deployed
                    ? "（まだ拠点にいます。数字は定義上の値）"
                    : "札を押すと、その駒が何をするか出ます");
            return;
        }
        SeatDiagram.Cell pick = cells.First(c => c.Slot == _squadPick);
        UnitDef d = pick.Def!;
        _squadFocus.AddChild(UiKit.Text(
            $"{FormationRules.SeatNames[_squadPick]}  {d.Name}"
            + (pick.Alive ? "" : "（戦死）"), 15, pick.Alive ? Colors.White : DeadColor));
        FocusLine(_squadFocus, $"何をする: {d.PlusText}", UiKit.Muted);
        if (d.MinusText.Length > 0) FocusLine(_squadFocus, $"代わりに: {d.MinusText}", UiKit.Faint);
        if (Map11.LineOf(d.Id) is { } line) FocusLine(_squadFocus, $"どの道で: {line}", GoldColor);
        // その駒から出ている線の説明（図に描いた語をそのまま文字でも出す）。
        foreach (string rel in RelationWordsFor(cells, _squadPick))
            FocusLine(_squadFocus, rel, PlayerColor);
    }

    /// <summary>敵部隊の 5 席を図にする。<b>空席は空席として描く</b>（先遣は後1 が空いている）。</summary>
    private void RenderFoeDiagram(Map11State.Node node)
    {
        var cells = new List<SeatDiagram.Cell>();
        if (node.Units is { } units)
            foreach (UnitState u in units)
                cells.Add(new SeatDiagram.Cell(u.Slot, u.Def, Math.Max(0, u.Hp), u.MaxHp,
                                               u.IsAlive && !node.Cleared));
        else
            foreach ((int slot, UnitDef def) in node.Def.Enemy.Occupied())
                cells.Add(new SeatDiagram.Cell(slot, def, def.MaxHp, def.MaxHp, !node.Cleared));

        if (_foePick >= 0 && cells.All(c => c.Slot != _foePick)) _foePick = -1;
        _foeDiagram.Render(cells, EnemyColor, _foePick);

        foreach (Node child in _foeFocus.GetChildren()) child.QueueFree();
        if (_foePick < 0)
        {
            FocusHint(_foeFocus, node.Cleared ? "（撃破済み）" : "札を押すと、その駒が何をするか出ます");
            return;
        }
        SeatDiagram.Cell pick = cells.First(c => c.Slot == _foePick);
        UnitDef d = pick.Def!;
        _foeFocus.AddChild(UiKit.Text($"{FormationRules.SeatNames[_foePick]}  {d.Name}"
            + (pick.Alive ? "" : "（倒れた）"), 15, pick.Alive ? Colors.White : DeadColor));
        // 敵には説明文の元データが1体も無いので、札ごとの1行で埋める（第170期）。
        foreach (string l in Map11Info.TraitLinesOf(d)) FocusLine(_foeFocus, l, UiKit.Muted);
        if (Map11Info.IsBoardRuleHolder(d))
            FocusLine(_foeFocus, "★ この駒が倒れるとルールが消える", GoldColor);
        foreach (string rel in RelationWordsFor(cells, _foePick))
            FocusLine(_foeFocus, rel, EnemyColor);
    }

    /// <summary>
    /// その席から出ている／その席に入っている関係を文にする。
    /// <b>元データは `Map11Relations`（席から機械で引いたもの）そのまま。</b>
    /// </summary>
    private static IEnumerable<string> RelationWordsFor(IReadOnlyList<SeatDiagram.Cell> cells, int slot)
    {
        var seats = cells.Where(c => c.Def is not null && c.Alive)
                         .ToDictionary(c => c.Slot, c => c.Def!);
        foreach (Map11Relations.Link l in Map11Relations.Of(seats))
        {
            // **語に意味の1行を添える**（第172期 §1-3）——第171期の観察ログ
            // 「ヒサの標が**どんな効果なのか分からない**」（問い1）への答え。
            // 文は `Map11Relations.Rules` が持ち、**網羅性は `--map11-phase172` が門にする**。
            string mean = Map11Relations.MeanOf(l.Trait) is { Length: > 0 } m ? $"（{m}）" : "";
            if (l.From == slot) yield return $"→ {seats[l.To].Name}：{l.Word}{mean}";
            else if (l.To == slot) yield return $"← {seats[l.From].Name}：{l.Word}{mean}";
        }
    }

    private static void FocusHint(VBoxContainer box, string text)
        => box.AddChild(UiKit.Text(text, 12, UiKit.Faint));

    private static void FocusLine(VBoxContainer box, string text, Color color)
    {
        Label l = UiKit.Text("　" + text, 12, color);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(l);
    }

    /// <summary>
    /// 選んでいる隊の中身。<b>説明文は `UnitDef.PlusText` / `MinusText` から引くだけ</b>
    /// ——`docs/units.md` を生成しているのと同じ元データなので、写しを持たない（自己検査 (e)）。
    ///
    /// <para>見出しを2つに分ける（指示書 §1-2）——<b>「何をする」</b>（全15枚・元データ）と
    /// <b>「どの道で」</b>（第164〜168期で書けた4枚だけ・`Map11.LineOf`）。</para>
    /// </summary>
    private void RefreshSquadDetail()
    {
        foreach (Node child in _squadDetail.GetChildren()) child.QueueFree();
        Map11State.Squad s = St.Squads[_selected];

        RenderSquadDiagram(s);
        _squadMore.Text = _squadDetailBox.Visible ? "文章を畳む" : "くわしく（文章）";

        _squadDetail.AddChild(UiKit.Text($"{s.Def.Name}（{s.Def.Role}）", 17, Colors.White));
        Label plan = UiKit.Text(Map11Info.PlanOf(s.Def.Id).Replace("**", ""), 13, GoldColor);
        plan.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _squadDetail.AddChild(plan);
        _squadDetail.AddChild(UiKit.Text("", 6, UiKit.Faint));

        // 盤上に出ている駒があればその現在値、まだ出ていなければ定義の値を出す。
        var rows = s.Units is { } units
            ? units.Where(u => !u.Def.Traits.Contains(TraitId.Ephemeral))
                   .OrderBy(u => u.Slot)
                   .Select(u => (u.Slot, u.Def, u.Hp, u.MaxHp, Alive: u.IsAlive)).ToList()
            : s.Def.F.Occupied()
                   .Select(o => (o.Slot, o.Def, Hp: o.Def.MaxHp, MaxHp: o.Def.MaxHp, Alive: true)).ToList();

        foreach (var r in rows)
        {
            Color tint = r.Alive ? UiKit.Ink : DeadColor;
            string seat = FormationRules.SeatNames[Math.Clamp(r.Slot, 0, FormationRules.TotalSlots - 1)];
            _squadDetail.AddChild(UiKit.Text(
                r.Alive
                    ? $"{seat}  {r.Def.Name}   HP {r.Hp}/{r.MaxHp} ・ 攻{r.Def.Attack} ・ 速{r.Def.Speed}"
                    : $"{seat}  {r.Def.Name}   （戦死）",
                14, tint));
            Wrapped($"　何をする: {r.Def.PlusText}", 12, r.Alive ? UiKit.Muted : DeadColor);
            if (r.Def.MinusText.Length > 0)
                Wrapped($"　代わりに: {r.Def.MinusText}", 12, r.Alive ? UiKit.Faint : DeadColor);
            if (Map11.LineOf(r.Def.Id) is { } line)
                Wrapped($"　どの道で: {line}", 12, r.Alive ? GoldColor : DeadColor);
            _squadDetail.AddChild(UiKit.Text("", 4, UiKit.Faint));
        }
        if (s.Units is null && !s.Deployed)
            _squadDetail.AddChild(UiKit.Text("（まだ拠点にいます。数字は定義上の値）", 12, UiKit.Faint));

        void Wrapped(string text, int size, Color color)
        {
            Label l = UiKit.Text(text, size, color);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _squadDetail.AddChild(l);
        }
    }

    /// <summary>
    /// 右で選んでいる敵部隊の中身。<b>敵の <c>UnitDef</c> には説明文が1体も無い</b>ので
    /// （Phase 0 Q0-1b）、札ごとの1行（<see cref="Map11Info.TraitLineOf"/>）で埋める。
    ///
    /// <para>用語は<b>括弧の中を主、名前を従</b>にしてある（§1-3）——
    /// 観察ログの「()で効果を書いてくれていたからなんとか分かった」に合わせた。</para>
    /// </summary>
    private void RefreshFoeDetail()
    {
        foreach (Node child in _foeDetail.GetChildren()) child.QueueFree();
        Map11State.Node node = St.Nodes[_foeView.Road][_foeView.Index];

        RenderFoeDiagram(node);
        _foeMore.Text = _foeDetailBox.Visible ? "文章を畳む" : "くわしく（文章）";

        _foeDetail.AddChild(UiKit.Text(
            $"{Map11.RoadNames[node.Def.Road]} {node.Def.Index + 1} 戦目 ・ {node.Def.Name}"
            + (node.Cleared ? "（撃破済み）" : ""), 16, node.Cleared ? DeadColor : Colors.White));

        // 盤上に立っている駒があればその現在値、まだ当たっていなければ定義の値。
        var rows = node.Units is { } units
            ? units.OrderBy(u => u.Slot)
                   .Select(u => (u.Slot, u.Def, u.Hp, u.MaxHp, Alive: u.IsAlive)).ToList()
            : node.Def.Enemy.Occupied()
                   .Select(o => (o.Slot, o.Def, Hp: o.Def.MaxHp, MaxHp: o.Def.MaxHp, Alive: true)).ToList();

        foreach (var r in rows)
        {
            bool live = r.Alive && !node.Cleared;
            Color tint = live ? UiKit.Ink : DeadColor;
            string seat = FormationRules.SeatNames[Math.Clamp(r.Slot, 0, FormationRules.TotalSlots - 1)];
            _foeDetail.AddChild(UiKit.Text(
                live
                    ? $"{seat}  {r.Def.Name}   HP {r.Hp}/{r.MaxHp} ・ 攻{r.Def.Attack} ・ 速{r.Def.Speed}"
                    : $"{seat}  {r.Def.Name}   （倒れた）",
                14, tint));
            foreach (string line in Map11Info.TraitLinesOf(r.Def))
            {
                Label l = UiKit.Text($"　{line}", 12, live ? UiKit.Muted : DeadColor);
                l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                _foeDetail.AddChild(l);
            }
            if (Map11Info.IsBoardRuleHolder(r.Def))
            {
                Label l = UiKit.Text("　★ この駒が倒れるとルールが消える", 12, live ? GoldColor : DeadColor);
                l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                _foeDetail.AddChild(l);
            }
            _foeDetail.AddChild(UiKit.Text("", 4, UiKit.Faint));
        }
    }

    /// <summary>戦闘の記録（<b>戦闘1回につき1行</b>）。マップ上と結果画面の両方に同じものを出す。</summary>
    private void RefreshHistory()
    {
        foreach (Node child in _history.GetChildren()) child.QueueFree();
        if (Map11Session.BattleLog.Count == 0)
        {
            _history.AddChild(UiKit.Text("（まだ戦っていません）", 12, UiKit.Faint));
            return;
        }
        foreach (Map11Session.Line l in Map11Session.BattleLog)
        {
            _history.AddChild(UiKit.Text(HistoryLine(l), 13, l.Won ? UiKit.Ink : UiKit.Hurt));
            foreach (string note in l.Notes)
            {
                Label n = UiKit.Text($"　　{note}", 12, GoldColor);
                n.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                _history.AddChild(n);
            }
        }
    }

    private static string HistoryLine(Map11Session.Line l) =>
        $"{l.No}戦目  {l.Squad} → {l.Road} {l.Foe}  {(l.Won ? "抜いた" : "抜けず")}"
        + $"  （残り {l.Alive}枚 ・ HP {l.HpPercent:F0}%）";

    private void ShowResult()
    {
        _resultTitle.Text = St.Won ? "踏破" : "撤退";
        // **生存枚数は2つ出す**（第169期に画面内で数え方が2通りに割れていた）。
        var lines = new List<string>
        {
            $"抜いた敵部隊 {St.ClearedCount} / {Map11.TotalNodes}（部分点 {St.Partial():F2}）",
            $"盤上に残った枚数 {Map11Session.AliveOnMap} 枚"
            + $" ／ 未出撃を含む枚数 {Map11Session.AliveIncludingReserve} 枚",
            $"戦闘回数 {St.Battles} 回",
            "",
            "戦闘の記録:",
        };
        if (Map11Session.BattleLog.Count == 0) lines.Add("　（戦っていません）");
        foreach (Map11Session.Line l in Map11Session.BattleLog)
        {
            lines.Add("　" + HistoryLine(l));
            foreach (string note in l.Notes) lines.Add("　　　" + note);
        }
        lines.Add("");
        lines.Add($"seed {Map11Session.Seed}（この数字を控えれば同じ通しを再現できます）");
        _resultBody.Text = string.Join("\n", lines);
        _resultOverlay.Visible = true;
    }

    // =================================================================================
    // 表示の引き直し
    // =================================================================================

    private static double SquadHpPercent(Map11State.Squad s)
    {
        if (s.Units is not { } u) return 100.0;
        int max = s.Def.F.Occupied().Sum(o => o.Def.MaxHp);
        return max == 0 ? 0 : 100.0 * u.Where(x => x.IsAlive).Sum(x => x.Hp) / max;
    }

    private static double NodeHpPercent(Map11State.Node n)
        => n.DefMaxHp == 0 ? 0 : 100.0 * n.HpNow / n.DefMaxHp;

    private void Refresh()
    {
        _headline.Text = $"検証用マップ 1-1 ・ 抜いた敵部隊 {St.ClearedCount} / {Map11.TotalNodes}"
            + $" ・ 戦闘 {St.Battles} 回 ・ seed {Map11Session.Seed}";

        for (int i = 0; i < St.Squads.Length; i++)
        {
            Map11State.Squad s = St.Squads[i];
            string where = s.Lost ? "失われた"
                : s.Units is null ? "未出撃（拠点）"
                : s.Road < 0 ? "拠点で待機"
                : $"{Map11.RoadNames[s.Road]} へ";
            string stat = s.Units is null
                ? (s.Lost ? "—" : $"{s.Def.F.Occupied().Count()} 枚 ・ HP 100%")
                : $"{s.Units.Count(u => u.IsAlive)} 枚 ・ HP {SquadHpPercent(s):F0}%";
            _squadButtons[i].Text = $"{s.Def.Name}  ({s.Def.Role})\n{stat}\n{where}";
            _squadButtons[i].Disabled = s.Lost;
            _squadButtons[i].Modulate = i == _selected ? new Color(0.72f, 1.0f, 0.90f) : Colors.White;

            // **盤上に札を出すのは道へ出ている隊だけ**（第170期）。待機と全滅は左のパネルが持つ
            // ——中身のパネルを常時開いたぶんパネルが伸びて、拠点の札が下に隠れたため。
            // 同じ言葉の表を2つ作らない（第124期 §4）。
            Marker m = _squadMarkers[i];
            if (s.Units is { } live && s.Road >= 0)
            {
                m.MoveTo(NodePos(s.Road, St.NextNode(s.Road)?.Def.Index ?? 0) + new Vector3(-3.6f, 0, 0));
                m.Set($"{s.Def.Name}\n{live.Count(u => u.IsAlive)}枚 {SquadHpPercent(s):F0}%", PlayerColor);
            }
            else m.Set("", PlayerColor, visible: false);
        }

        foreach (Node child in _roadList.GetChildren()) child.QueueFree();
        int marker = 0;
        for (int road = 0; road < Map11.RoadCount; road++)
        {
            _roadList.AddChild(UiKit.Text(Map11.RoadNames[road], 16, GoldColor));
            for (int i = 0; i < St.Nodes[road].Length; i++)
            {
                Map11State.Node n = St.Nodes[road][i];
                int alive = n.Cleared ? 0 : n.Units?.Count(u => u.IsAlive) ?? n.Def.Enemy.Occupied().Count();
                string text = n.Cleared
                    ? $"{i + 1}. {n.Def.Name} — 撃破"
                    : $"{i + 1}. {n.Def.Name}\n{Map11.RuleLineOf(n.Def.Enemy)}\n"
                      + $"{alive} 体 ・ 残り HP {NodeHpPercent(n):F0}%";
                // 押すと右下に中身が出る（観察ログ: 敵のイメージが沸かない）。
                (int r0, int i0) = (road, i);
                var button = new Button
                {
                    Text = text,
                    Alignment = HorizontalAlignment.Left,
                    FocusMode = Control.FocusModeEnum.None,
                    CustomMinimumSize = new Vector2(0, n.Cleared ? 30 : 62),
                    Modulate = _foeView == (r0, i0) ? new Color(1.0f, 0.86f, 0.80f) : Colors.White,
                };
                button.AddThemeColorOverride("font_color", n.Cleared ? DeadColor : UiKit.Ink);
                button.Pressed += () => { if (_foeView != (r0, i0)) _foePick = -1; _foeView = (r0, i0); Refresh(); };
                _roadList.AddChild(button);

                Marker m = _nodeMarkers[marker++];
                m.Set(n.Cleared ? "" : $"{n.Def.Name}\n{alive} 体 ・ {NodeHpPercent(n):F0}%",
                      EnemyColor, !n.Cleared);
            }
        }

        int waiting = St.Squads.Count(x => !x.Lost && x.Road < 0);
        _homeLabel.Text = waiting == 0 ? "拠点" : $"拠点（待機 {waiting} 隊）";

        RefreshSquadDetail();
        RefreshFoeDetail();
        RefreshHistory();
        RefreshBench();
        RefreshReform();

        bool busy = St.Finished;
        _sendNorth.Disabled = busy || St.NextNode(0) is null;
        _sendSouth.Disabled = busy || St.NextNode(1) is null;
        // 操作の案内は**まだ1戦もしていない間だけ**（観察ログ「操作方法が最初わからなかった」）。
        _hint.Visible = Map11Session.BattleLog.Count == 0;
    }

    private void ShowToast(string text, Color color, float seconds)
    {
        _toast.Text = text;
        _toast.AddThemeColorOverride("font_color", color);
        _toastClock = seconds;
    }

    public override void _Process(double delta)
    {
        if (_toastClock <= 0) return;
        _toastClock -= (float)delta;
        if (_toastClock <= 0) _toast.Text = "";
    }
}
