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
    private static readonly Vector3 BasePos = new(-8.0f, 0, 2.0f);

    /// <summary>区画の位置（道 × 番号）。北の道が奥（-Z）、南の道が手前（+Z）。</summary>
    private static Vector3 NodePos(int road, int index)
        => new(3.0f + index * 11.0f, 0, road == 0 ? -8.5f : 8.5f);

    /// <summary>隊が拠点で待つ位置（3 隊を縦に並べる）。</summary>
    private static Vector3 WaitPos(int squad) => BasePos + new Vector3(-5.4f + squad * 5.4f, 0, 6.4f);

    private Camera3D _camera = null!;
    private readonly List<Marker> _squadMarkers = new();
    private readonly List<Marker> _nodeMarkers = new();

    private VBoxContainer _squadList = null!;
    private VBoxContainer _roadList = null!;
    private Label _headline = null!;
    private Label _toast = null!;
    private Button _sendNorth = null!;
    private Button _sendSouth = null!;
    private Button _inspect = null!;

    private Control _detailOverlay = null!;
    private VBoxContainer _detailBody = null!;
    private Label _detailTitle = null!;

    private Control _encounterOverlay = null!;
    private Label _encounterTitle = null!;
    private Label _encounterBody = null!;

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
        if (args.Contains("--map11-flow-smoke", StringComparer.Ordinal))
            Callable.From(AutoStep).CallDeferred();
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
        if (parts.Length > 1 && parts[1] == "detail") ShowDetail();
        else if (parts.Length > 1 && parts[1] == "encounter") Send(1);
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
        home.AddChild(WorldLabel("拠点", new Vector3(0, 0.9f, 3.1f), 32, PlayerColor));

        for (int road = 0; road < Map11.RoadCount; road++)
            for (int i = 0; i < Map11.Roads[road].Count; i++)
            {
                var m = new Marker(this, NodePos(road, i), EnemyColor, 2.0f);
                _nodeMarkers.Add(m);
            }

        for (int s = 0; s < St.Squads.Length; s++)
            _squadMarkers.Add(new Marker(this, WaitPos(s), PlayerColor, 1.3f));
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
        PanelContainer left = Panel();
        left.Position = new Vector2(20, 90);
        left.CustomMinimumSize = new Vector2(330, 0);
        root.AddChild(left);
        var leftCol = new VBoxContainer();
        leftCol.AddThemeConstantOverride("separation", 8);
        left.AddChild(leftCol);
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

        _inspect = UiKit.ActionButton("この隊の中身を見る");
        _inspect.Pressed += ShowDetail;
        leftCol.AddChild(_inspect);

        // ---- 右: 道 ----
        PanelContainer right = Panel();
        right.AnchorLeft = 1;
        right.AnchorRight = 1;
        right.OffsetLeft = -400;
        right.OffsetRight = -20;
        right.OffsetTop = 90;
        root.AddChild(right);
        var rightCol = new VBoxContainer();
        rightCol.AddThemeConstantOverride("separation", 8);
        right.AddChild(rightCol);
        rightCol.AddChild(UiKit.Text("道と敵部隊", 12, UiKit.Faint));
        _roadList = new VBoxContainer();
        _roadList.AddThemeConstantOverride("separation", 6);
        rightCol.AddChild(_roadList);

        BuildDetailOverlay(root);
        BuildEncounterOverlay(root);
        BuildResultOverlay(root);
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

    private void BuildDetailOverlay(Control root)
    {
        _detailOverlay = Modal(root, 330, 250, out VBoxContainer col, "SQUAD", PlayerColor);
        _detailTitle = UiKit.Text("", 24, Colors.White);
        col.AddChild(_detailTitle);
        _detailBody = new VBoxContainer();
        _detailBody.AddThemeConstantOverride("separation", 6);
        _detailBody.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(_detailBody);
        Button close = UiKit.ActionButton("閉じる");
        close.Pressed += () => _detailOverlay.Visible = false;
        col.AddChild(close);
    }

    private void BuildEncounterOverlay(Control root)
    {
        _encounterOverlay = Modal(root, 300, 190, out VBoxContainer col, "ENCOUNTER", EnemyColor);
        _encounterTitle = UiKit.Text("", 24, Colors.White);
        col.AddChild(_encounterTitle);
        _encounterBody = UiKit.Text("", 14, Color.FromHtml("#ccd5cb"));
        _encounterBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _encounterBody.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
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
        _selected = index;
        Refresh();
    }

    private void Send(int road)
    {
        if (St.Finished) return;
        Map11State.Squad s = St.Squads[_selected];
        if (s.Lost) { ShowToast($"{s.Def.Name} は失われています", EnemyColor, 3.0f); return; }
        if (St.NextNode(road) is null) { ShowToast($"{Map11.RoadNames[road]} は抜け切っています", UiKit.Muted, 3.0f); return; }

        Map11Session.Send(_selected, road);
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
        _encounterBody.Text =
            $"{Map11.RoadNames[node.Def.Road]} {node.Def.Index + 1} 戦目。\n"
            + $"{Map11.RuleLineOf(node.Def.Enemy)}\n"
            + $"敵 {alive} 体 ・ 残り HP {NodeHpPercent(node):F0}%\n\n"
            + $"味方 {s.Units.Count(u => u.IsAlive)} 体 ・ 残り HP {SquadHpPercent(s):F0}%\n"
            + "戦えば、傷も死者もそのまま持ち込みます。";
        _encounterOverlay.Visible = true;
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

    private void ShowDetail()
    {
        Map11State.Squad s = St.Squads[_selected];
        _detailTitle.Text = $"{s.Def.Name}（{s.Def.Role}）";
        foreach (Node child in _detailBody.GetChildren()) child.QueueFree();

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
            var head = UiKit.Text(
                r.Alive ? $"{seat}  {r.Def.Name}   HP {r.Hp} / {r.MaxHp}" : $"{seat}  {r.Def.Name}   （戦死）",
                15, tint);
            _detailBody.AddChild(head);
            if (Map11.LineOf(r.Def.Id) is { } line)
                _detailBody.AddChild(UiKit.Text($"      {line}", 12, r.Alive ? GoldColor : DeadColor));
        }
        if (s.Units is null && !s.Deployed)
            _detailBody.AddChild(UiKit.Text("（まだ拠点にいます。数字は定義上の値）", 12, UiKit.Faint));
        _detailOverlay.Visible = true;
    }

    private void ShowResult()
    {
        _resultTitle.Text = St.Won ? "踏破" : "撤退";
        _resultBody.Text =
            $"抜いた敵部隊 {St.ClearedCount} / {Map11.TotalNodes}（部分点 {St.Partial():F2}）\n"
            + $"生存枚数 {St.Squads.Sum(s => s.Units?.Count(u => u.IsAlive) ?? 0)} 枚"
            + $" ・ 戦闘回数 {St.Battles} 回\n\n"
            + $"出した順: {Map11Session.OrderSummary()}\n\n"
            + $"seed {Map11Session.Seed}（この数字を控えれば同じ通しを再現できます）";
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

            Marker m = _squadMarkers[i];
            bool onMap = s.Units is not null;
            Vector3 pos = !onMap || s.Road < 0
                ? WaitPos(i)
                : NodePos(s.Road, St.NextNode(s.Road)?.Def.Index ?? 0) + new Vector3(-3.4f, 0, 0);
            m.MoveTo(pos);
            // 盤上の札は短く（同じ数字は左のパネルに出ている）。
            string badge = s.Units is null
                ? $"{s.Def.Name}\n待機"
                : $"{s.Def.Name}\n{s.Units.Count(u => u.IsAlive)}枚 {SquadHpPercent(s):F0}%";
            m.Set(s.Lost ? "" : badge, PlayerColor, !s.Lost);
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
                    ? $"  {i + 1}. {n.Def.Name} — 撃破"
                    : $"  {i + 1}. {n.Def.Name}\n     {Map11.RuleLineOf(n.Def.Enemy)}\n"
                      + $"     {alive} 体 ・ 残り HP {NodeHpPercent(n):F0}%";
                _roadList.AddChild(UiKit.Text(text, 13, n.Cleared ? DeadColor : UiKit.Ink));

                Marker m = _nodeMarkers[marker++];
                m.Set(n.Cleared ? "" : $"{n.Def.Name}\n{alive} 体 ・ {NodeHpPercent(n):F0}%",
                      EnemyColor, !n.Cleared);
            }
        }

        bool busy = St.Finished;
        _sendNorth.Disabled = busy || St.NextNode(0) is null;
        _sendSouth.Disabled = busy || St.NextNode(1) is null;
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
