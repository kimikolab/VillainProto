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

    /// <summary>
    /// マスの位置（第174期 §2-1）。<b>マス 0 は拠点と1つ目の敵のあいだの空きマス</b>で、
    /// マス 1・2 は区画 0・1 と同じ場所（<see cref="NodePos"/>）。
    /// </summary>
    private static Vector3 CellPos(int road, int cell)
        => cell <= 0 ? new(-0.6f, 0, road == 0 ? -8.5f : 8.5f) : NodePos(road, cell - 1);

    private Camera3D _camera = null!;
    private Label3D _homeLabel = null!;
    private readonly List<Marker> _squadMarkers = new();
    private readonly List<Marker> _nodeMarkers = new();

    private VBoxContainer _squadList = null!;
    private VBoxContainer _roadList = null!;
    private Label _headline = null!;
    private Label _toast = null!;
    private Label _hint = null!;

    // ---- 第174期 部B: 作戦ターン ／ 第175期 §1-2: 行き先指定 ----

    /// <summary><b>何かが起きるまで</b>作戦ターンを自動で回す（第175期 §1-2）。</summary>
    private Button _run = null!;
    /// <summary>1 作戦ターンだけ進める（慣れた人・検証用。第174期の口をそのまま残す）。</summary>
    private Button _advanceTurn = null!;
    private Label _orderHint = null!;

    /// <summary>
    /// 盤の上の行き先の札（拠点 ＋ 道 × マス）。<b>隊を押してからここを押すと行き先が決まる</b>
    /// ——第174期の命令ボタン列（北へ進む／南へ進む／戻る／休む／待つ）はこれに置き換えた。
    /// </summary>
    private readonly List<DestButton> _destButtons = new();
    /// <summary>隊 → 行き先の矢印（隊ごとに1本）。</summary>
    private readonly List<Arrow> _arrows = new();
    /// <summary>自動で回している最中か。</summary>
    private bool _running;
    /// <summary>最初の引き直しだけは滑らせない（戦闘から戻ってきた直後に盤が動いて見えるため）。</summary>
    private bool _firstRefresh = true;
    private Control _interceptOverlay = null!;
    private Label _interceptTitle = null!;
    private VBoxContainer _interceptList = null!;
    /// <summary>道の上の空きマスの板（位置を教えるためだけの物）。</summary>
    private readonly List<Marker> _cellMarkers = new();

    private VBoxContainer _squadDetail = null!;
    private VBoxContainer _foeDetail = null!;
    private VBoxContainer _history = null!;

    // ---- 第171期 部A: 配置の図 ----
    private SeatDiagram _squadDiagram = null!;
    private SeatDiagram _foeDiagram = null!;
    /// <summary>図の下に<b>常時</b>出す「この隊の関係」（第174期 部A #1・#4）。</summary>
    private VBoxContainer _squadSummary = null!;
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
        else ShowToast("隊を押してから、盤の行き先を押してください（拠点を押せば引き返す）",
                       GoldColor, 6.0f);

        if (St.Finished) ShowResult();

        // 第169期 自己検査 (d')。**画面を通した通し確認**（頭なしで走る）。
        // 判断だけを器具と同じ固定の割り当てで代行し、戦闘は本物の再生を通す。
        // `_Ready` の中でシーンを替えると木の再入で落ちるので、必ず遅らせる。
        string[] args = OS.GetCmdlineUserArgs();
        // 第172期 自己検査 (c)。**1 回組み直してから出す**通し（組み直した隊で戦って、
        // 持ち越しと結果画面まで通ることを確かめる）。
        bool reformFirst = args.Contains("--map11-reform-smoke", StringComparer.Ordinal);
        // 第173期 自己検査 (d)。**道へ出した隊を拠点へ戻し、組み直してから出す**通し。
        bool recallFirst = args.Contains("--map11-recall-smoke", StringComparer.Ordinal);
        bool timeSmoke = args.Contains("--map11-time-smoke", StringComparer.Ordinal);
        // 第174期 自己検査 (d): **「休む」と「迎撃」を1回ずつ含む走行**。
        // seed を固定して全快方針で回す（傷が残っていれば拠点へ戻って休む）。
        if (timeSmoke && St.Turn == 0 && St.Battles == 0 && !Map11Session.TurnRunning)
            Map11Session.Reset(0);
        if (args.Contains("--map11-flow-smoke", StringComparer.Ordinal)
            || reformFirst || recallFirst || timeSmoke)
        {
            // **戻すのは1度だけ**——戦闘のたびにこのシーンへ戻ってくる（`_Ready` が毎回走る）ので、
            // 素通りさせると通しのあいだ毎回組み直してしまう。
            if (recallFirst && St.Battles == 0) AutoRecall();
            if (reformFirst) AutoReform();
            Callable.From(AutoStep).CallDeferred();
        }
        // 戦闘から戻ってきた——作戦ターンの続きを流す（第174期）。
        else if (Map11Session.TurnRunning) Callable.From(Pump).CallDeferred();
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
        if (parts.Length > 1 && parts[1] == "encounter")
        { PickDestination(Dest.Deep(1)); AdvanceTurn(); }
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
    /// 通し確認の1歩（<b>第174期に作戦ターン制へ作り替えた</b>）。
    /// 正の割り当て（カド隊 → 南 ／ かき回し隊 → 北、控えは空いた道）で命令を置き、
    /// 作戦ターンを進めて、戦闘に当たったら戦闘シーンへ送り出す。
    /// <b>迎撃は拠点にいる隊のうち番号の若いものを選ぶ</b>（器具と同じ）。
    /// </summary>
    private void AutoStep()
    {
        while (true)
        {
            if (St.Finished) { AutoFinish(); return; }

            if (!Map11Session.TurnRunning)
            {
                AutoOrders();
                Map11Session.BeginTurn();
            }

            Map11Session.StepKind kind = Map11Session.StepTurn();
            Refresh();
            switch (kind)
            {
                case Map11Session.StepKind.Battle:
                {
                    Map11State.Node? node = Map11Session.PendingNode;
                    GD.Print($"MAP11_FLOW_SMOKE_STEP turn={St.Turn + 1}"
                        + $" squad={St.Squads[Map11Session.PendingSquad].Def.Name}"
                        + $" foe={node?.Def.Name} battles={St.Battles}");
                    GetTree().ChangeSceneToFile(CampaignSession.BattleScene);
                    return;
                }
                case Map11Session.StepKind.Intercept:
                {
                    if (Map11Session.InterceptNode is not { } node) break;
                    int pick = St.NextInterceptor(node);
                    GD.Print($"MAP11_TIME_SMOKE intercept turn={St.Turn} foe={node.Def.Name}"
                        + $" squad={(pick >= 0 ? St.Squads[pick].Def.Name : "—")}");
                    if (pick >= 0 && Map11Session.ChooseInterceptor(pick))
                    {
                        GetTree().ChangeSceneToFile(CampaignSession.BattleScene);
                        return;
                    }
                    break;
                }
                case Map11Session.StepKind.Fallen:
                    AutoFinish();
                    return;
            }
        }
    }

    private void AutoFinish()
    {
        GD.Print($"MAP11_TIME_SMOKE rests={St.Rests} intercepts={St.Intercepts} fallen={St.Fallen}"
            + $" turns={St.Turn}");
        GD.Print($"MAP11_FLOW_SMOKE_COMPLETE won={St.Won} cleared={St.ClearedCount}"
            + $"/{Map11.TotalNodes} battles={St.Battles} seed={Map11Session.Seed}");
        foreach (Map11Session.Line l in Map11Session.BattleLog)
        {
            GD.Print("MAP11_LOG " + HistoryLine(l));
            foreach (string note in l.Notes) GD.Print("MAP11_LOG     " + note);
        }
        GD.Print($"MAP11_LOG 盤上 {Map11Session.AliveOnMap} 枚 / 未出撃を含む {Map11Session.AliveIncludingReserve} 枚");
        if (OS.GetCmdlineUserArgs()
                .FirstOrDefault(a => a.StartsWith("--map11-capture=", StringComparison.Ordinal))
            is { } shot) { _ = Capture(shot["--map11-capture=".Length..]); return; }
        GetTree().Quit();
    }

    /// <summary>
    /// 通し確認の<b>行き先</b>（第175期 §1-1 に合わせて命令から行き先へ置き換えた）。
    /// <b>器具（`Map11Time`）の方針をそのまま写す</b>——既定は貪欲（休まない・道の奥まで進む）で、
    /// <c>--map11-time-smoke</c> のときだけ全快（傷が残っていれば拠点へ戻って休む）。
    /// <b>命令は <see cref="Map11Orders.Plan"/> が行き先から作る</b>ので、ここは行き先を置くだけ。
    /// </summary>
    private void AutoOrders()
    {
        bool full = OS.GetCmdlineUserArgs().Contains("--map11-time-smoke", StringComparer.Ordinal);
        int[] home = { 1, 0, -1 };
        bool anyLost = St.Squads.Any(x => x.Lost);
        for (int i = 0; i < St.Squads.Length; i++)
        {
            Map11State.Squad sq = St.Squads[i];
            if (sq.Lost) { Map11Session.SetDestination(i, Dest.Home); continue; }

            // 控えは第169期と同じ——**隊が全滅してから**出す。
            if (i == St.Squads.Length - 1 && !sq.Deployed && !anyLost)
            { Map11Session.SetDestination(i, Dest.Home); continue; }

            if (full && St.Hurt(i)) { Map11Session.SetDestination(i, Dest.Home); continue; }

            int road = home[i];
            if (road < 0 || St.NextNode(road) is null)
            {
                road = -1;
                for (int r = 0; r < Map11.RoadCount; r++)
                    if (St.NextNode(r) is not null) { road = r; break; }
            }
            Map11Session.SetDestination(i, road < 0 ? Dest.Home : Dest.Deep(road));
        }
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

    /// <summary>
    /// 自己検査 (d) の「拠点へ戻す」（第173期 §1-2）。<b>画面と同じ口を通す</b>
    /// ——`Send` → `Recall` → 組み直し → `CanSend` の順で、道の上の隊が拠点へ戻って
    /// 組み直せることを頭なしで確かめる。<b>盤面には何も残さない</b>（戻した隊はそのまま
    /// 通し確認の <see cref="AutoStep"/> が出し直す）。
    /// </summary>
    private void AutoRecall()
    {
        _selected = 0;
        Map11Session.Send(0, 0);
        bool onRoad = St.CanWithdraw(0) && !St.CanReform(0);
        // 第174期: 戻るのは**1 作戦ターンに1マス**になったので、拠点まで1歩ずつ下げる
        // （第173期の「一瞬で戻す」口＝`Map11State.Withdraw` は時間なしの版のために残してある）。
        while (St.Squads[0].Cell >= 0) St.StepBack(0);
        bool home = St.Squads[0].Road < 0 && St.CanReform(0);
        bool reformed = St.SwapSeats(0, 0, 0, 3);
        bool sendable = St.CanSend(0);
        GD.Print($"MAP11_RECALL_SMOKE onRoad={onRoad} home={home} reformed={reformed} sendable={sendable}");
        GD.Print("MAP11_RECALL_SMOKE " + St.Squads[0].Def.Name + ": "
            + string.Join(" / ", St.Squads[0].Units!.OrderBy(x => x.Slot)
                .Select(x => $"{FormationRules.SeatNames[x.Slot]} {x.Def.Name}")));
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
            for (int cell = 0; cell < Map11.RoadCells; cell++)
            {
                Vector3 to = CellPos(road, cell);
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

        // 空きマス（第174期）。**位置を教えるためだけの薄い板**で、判定は1つも持たない。
        for (int road = 0; road < Map11.RoadCount; road++)
        {
            var m = new Marker(this, CellPos(road, 0), Color.FromHtml("#6f6a52"), 1.1f);
            m.Set("", Color.FromHtml("#6f6a52"));
            _cellMarkers.Add(m);
        }

        for (int road = 0; road < Map11.RoadCount; road++)
            for (int i = 0; i < Map11.Roads[road].Count; i++)
            {
                var m = new Marker(this, CellPos(road, Map11.StartCellOf(i)), EnemyColor, 2.0f);
                _nodeMarkers.Add(m);
            }

        for (int s = 0; s < St.Squads.Length; s++)
        {
            _squadMarkers.Add(new Marker(this, BasePos, PlayerColor, 1.3f));
            // 第175期 §1-2 —— 隊から行き先への矢印。**判定は1つも持たない**（見せるだけ）。
            _arrows.Add(new Arrow(this, PlayerColor));
        }
    }

    // =================================================================================
    // 第175期 §1-2 —— 盤のマスを押して行き先を決める
    //
    // **盤は 3D だが、札は 2D で重ねる**（`Camera3D.UnprojectPosition` で座標を引くだけ）
    // ——当たり判定の体（`Area3D`）と光線を足さずに済み、`_Process` で位置を追うだけで動く。
    // **判定は1つも持たない**: 押されたら `Map11Session.SetDestination` を呼ぶだけ。
    // =================================================================================

    /// <summary>盤の上の行き先の札1枚。</summary>
    private sealed class DestButton
    {
        public required Dest Dest { get; init; }
        public required Button Node { get; init; }
        public required Vector3 World { get; init; }
        /// <summary>札を置く向き（拠点は下、道の上は下）。</summary>
        public required Vector2 Offset { get; init; }
    }

    private void BuildDestButtons(Control root)
    {
        var layer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(layer);

        void Add(Dest dest, Vector3 world, Vector2 offset)
        {
            var b = new Button
            {
                Text = "",
                FocusMode = Control.FocusModeEnum.None,
                Size = new Vector2(96, 26),
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            b.AddThemeFontSizeOverride("font_size", 12);
            b.Pressed += () => PickDestination(dest);
            layer.AddChild(b);
            _destButtons.Add(new DestButton { Dest = dest, Node = b, World = world, Offset = offset });
        }

        // 札は<b>いつも板の真下</b>（道で揃える）。名前は板の上、前進の予告はさらにその上。
        Add(Dest.Home, BasePos, new Vector2(-48, 78));
        for (int road = 0; road < Map11.RoadCount; road++)
            for (int cell = 0; cell < Map11.RoadCells; cell++)
                Add(new Dest(road, cell), CellPos(road, cell), new Vector2(-48, 32));
    }

    /// <summary>
    /// 行き先を決めた（<b>盤面は1ビットも動かない</b>——動くのは「進める」を押したとき）。
    /// </summary>
    private void PickDestination(Dest dest)
    {
        if (St.Finished || Map11Session.TurnRunning) return;
        Map11State.Squad s = St.Squads[_selected];
        if (s.Lost) { ShowToast($"{s.Def.Name} は失われています", EnemyColor, 3.0f); return; }
        if (!dest.AtHome && !St.CanSend(_selected))
        { ShowToast($"{s.Def.Name} は 0 枚です（1 枚以上にしてください）", EnemyColor, 3.0f); return; }

        if (_reform) { _reform = false; ClearHold(); }
        Map11Session.SetDestination(_selected, dest);
        // 行き先の道の中身を右に出しておく（決める瞬間の隣に相手を置く・第171期 §1-3）。
        if (!dest.AtHome && St.NextNode(dest.Road) is { } next)
        { if (_foeView != (dest.Road, next.Def.Index)) _foePick = -1; _foeView = (dest.Road, next.Def.Index); }
        ShowToast($"{s.Def.Name}: {Map11Orders.Describe(St, _selected, dest)}", PlayerColor, 3.0f);
        Refresh();
    }

    /// <summary>札の位置と文字を引き直す（カメラは動かないが、画面の大きさは変わる）。</summary>
    private void RefreshDestButtons()
    {
        bool busy = St.Finished || Map11Session.TurnRunning;
        Map11State.Squad sel = St.Squads[_selected];
        foreach (DestButton d in _destButtons)
        {
            Vector2 p = _camera.UnprojectPosition(d.World);
            d.Node.Position = p + d.Offset;
            bool cleared = !d.Dest.AtHome && St.NextNode(d.Dest.Road) is null;
            d.Node.Disabled = busy || sel.Lost || cleared;
            bool here = Map11Session.Destinations[_selected] == d.Dest;
            // 札は短く（盤の上は狭い）。長い名前は左のパネルと案内が出す。
            d.Node.Text = (here ? "◆ " : "") + (d.Dest.AtHome ? "拠点"
                : d.Dest.Cell <= 0 ? "手前" : $"{d.Dest.Cell} 戦目");
            d.Node.Modulate = here ? new Color(1.0f, 0.90f, 0.62f)
                            : cleared ? new Color(1, 1, 1, 0.35f) : Colors.White;
        }
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
        /// <summary>第175期 §1-3 —— 盤の上の「あと n ／ →」。<b>札の下に大きめで出す。</b></summary>
        private readonly Label3D _note;
        private readonly StandardMaterial3D _material;
        private readonly float _radius;
        private Vector3 _from, _to;
        private float _slide, _slideFor;
        private float _clock;
        private bool _pulse;
        public Node3D Root { get; }

        public Marker(Node parent, Vector3 position, Color color, float radius)
        {
            Root = new Node3D { Position = position };
            _from = _to = position;
            _radius = radius;
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
            _note = WorldLabel("", new Vector3(0, radius + 4.2f, 0), 40, GoldColor);
            _note.Visible = false;
            Root.AddChild(_note);
        }

        public void Set(string text, Color color, bool visible = true)
        {
            _label.Text = text;
            _material.AlbedoColor = color;
            _material.Emission = color * 0.25f;
            _disc.Visible = visible;
            _label.Visible = visible;
            if (!visible) { _note.Visible = false; _pulse = false; }
        }

        /// <summary>
        /// 第175期 §1-3 —— <b>「あと n ターンで前進」を盤の上に出す</b>（右のパネルからは消した）。
        /// <paramref name="pulse"/> が真なら札が脈打つ（<c>n = 1</c>）。
        /// </summary>
        public void SetNote(string text, Color color, bool pulse)
        {
            _note.Text = text;
            _note.Modulate = color;
            _note.Visible = text.Length > 0 && _disc.Visible;
            _pulse = pulse && _note.Visible;
        }

        public void MoveTo(Vector3 position)
        {
            Root.Position = _from = _to = position;
            _slide = _slideFor = 0;
        }

        /// <summary>
        /// 第175期 §1-3 —— <b>1 マス滑らせる</b>（瞬間移動させない）。
        /// 行き先が同じなら何もしない。
        /// </summary>
        public void SlideTo(Vector3 position, float seconds = 0.45f)
        {
            if (_to.IsEqualApprox(position)) return;
            if (Root.Position.DistanceTo(position) < 0.01f) { MoveTo(position); return; }
            _from = Root.Position;
            _to = position;
            _slide = _slideFor = seconds;
        }

        public void Tick(double delta)
        {
            _clock += (float)delta;
            if (_slide > 0)
            {
                _slide -= (float)delta;
                float t = _slideFor <= 0 ? 1 : Mathf.Clamp(1 - _slide / _slideFor, 0, 1);
                Root.Position = _from.Lerp(_to, t * t * (3 - 2 * t));   // なめらかに
                if (_slide <= 0) Root.Position = _to;
            }
            float k = _pulse ? 1 + 0.10f * Mathf.Sin(_clock * 6.0f) : 1;
            _disc.Scale = new Vector3(k, 1, k);
        }
    }

    /// <summary>
    /// 第175期 §1-2 —— 隊から行き先への矢印。<b>判定も規則も1つも持たない。</b>
    /// </summary>
    private sealed class Arrow
    {
        private readonly Node3D _root;
        private readonly MeshInstance3D _shaft;
        private readonly MeshInstance3D _head;

        public Arrow(Node parent, Color color)
        {
            _root = new Node3D();
            parent.AddChild(_root);
            StandardMaterial3D mat = CampaignMain.MakeMaterial(color, roughness: 0.8f,
                                                               emission: color * 0.35f);
            _shaft = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.34f, 0.05f, 1) },
                MaterialOverride = mat,
            };
            _root.AddChild(_shaft);
            _head = new MeshInstance3D
            {
                Mesh = new CylinderMesh
                {
                    TopRadius = 0, BottomRadius = 0.52f, Height = 1.1f, RadialSegments = 10,
                },
                RotationDegrees = new Vector3(-90, 0, 0),   // +Y を -Z（LookAt の向き）へ倒す
                MaterialOverride = mat,
            };
            _root.AddChild(_head);
            _root.Visible = false;
        }

        public void Set(Vector3 from, Vector3 to)
        {
            float len = from.DistanceTo(to);
            if (len < 0.5f) { _root.Visible = false; return; }
            _root.Visible = true;
            _root.Position = new Vector3(from.X, 0.35f, from.Z);
            _root.LookAt(new Vector3(to.X, 0.35f, to.Z), Vector3.Up);
            float body = Math.Max(0.2f, len - 1.1f);
            _shaft.Position = new Vector3(0, 0, -body * 0.5f);
            _shaft.Scale = new Vector3(1, 1, body);
            _head.Position = new Vector3(0, 0, -body - 0.55f);
        }

        public void Hide() => _root.Visible = false;
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

        BuildDestButtons(root);

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
        _hint = UiKit.Text("① 隊を押す → ② 盤の行き先を押す（拠点を押せば引き返す）"
            + " → ③「▶ 進める」。何かが起きるまで作戦ターンが流れ、敵も1マスずつ近づきます", 13, GoldColor);
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

        // 第174期 部A #1・#4 —— **押さなくても読める**関係と、隊全体にかかる札。
        // <b>指示書は「図の下に」だが、隊の札のすぐ下に置いた</b>——左のパネルは
        // 第172期の時点で図が画面の下端近くにあり、その下に置くと
        // <b>スクロールしないと読めない</b>（第171期 問い5「下側で見切れて読めない」の再発になる）。
        // 「押さなくても読める」が指示書の狙いなので、**場所のほうを動かした**。
        _squadSummary = new VBoxContainer();
        _squadSummary.AddThemeConstantOverride("separation", 2);
        leftCol.AddChild(Scroll(_squadSummary, 128));

        // 第175期 §1-2 —— **命令ボタンの列は消した。** 行き先は盤のマスを押して決める
        // （隊を押す → マスを押す の2クリック）。第174期は1作戦ターンに最大7クリックかかっていた。
        _orderHint = UiKit.Text("", 12, GoldColor);
        _orderHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        leftCol.AddChild(_orderHint);

        var turnRow = new HBoxContainer();
        turnRow.AddThemeConstantOverride("separation", 8);
        leftCol.AddChild(turnRow);
        _run = UiKit.ActionButton("▶ 進める", GoldColor);
        _run.CustomMinimumSize = new Vector2(0, 38);
        _run.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _run.Pressed += RunUntilEvent;
        turnRow.AddChild(_run);
        _advanceTurn = UiKit.ActionButton("1ターン", UiKit.Muted);
        _advanceTurn.CustomMinimumSize = new Vector2(96, 38);
        _advanceTurn.Pressed += AdvanceTurn;
        turnRow.AddChild(_advanceTurn);

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
        leftCol.AddChild(LinkLegend());
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
        rightCol.AddChild(LinkLegend());
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
        BuildInterceptOverlay(root);
        BuildResultOverlay(root);
    }

    /// <summary>
    /// 線の凡例（第173期 §1-1）。<b>色の出どころは <see cref="SeatLinks.ColorOf"/> の1本</b>
    /// ——ここで色を書き直さない（同じ言葉の表を2つ作らない・第124期 §4）。
    /// </summary>
    private static Control LinkLegend()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        foreach (Map11Relations.LinkSign sign in new[]
                 {
                     Map11Relations.LinkSign.Gain, Map11Relations.LinkSign.Loss,
                     Map11Relations.LinkSign.Both,
                 })
            row.AddChild(UiKit.Text("■ " + Map11Relations.LabelOf(sign), 11, SeatLinks.ColorOf(sign)));
        row.AddChild(UiKit.Text("┄ 条件つき（殴ったとき等）", 11, UiKit.Faint));
        return row;
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
        // 第174期: **引き返す口はここには無い。** 進んだ先に敵がいればその場で戦闘で、
        // 引き返すのは「戻る」という**作戦ターンを1つ使う行動**になった（§2-1）。
        Button fight = UiKit.ActionButton("戦う", GoldColor);
        fight.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        fight.Pressed += Fight;
        row.AddChild(fight);
    }

    /// <summary>
    /// 拠点の迎撃（第174期 §2-1）。<b>敵が拠点まで来たら、拠点にいる隊を1つ選んで戦わせる。</b>
    /// 負ければ次の隊へ。<b>拠点に隊が1つもいなければ陥落</b>——その判定は
    /// <see cref="Map11State.AdvanceFoes"/> / <see cref="Map11State.CloseIntercept"/> が持つ。
    /// </summary>
    private void BuildInterceptOverlay(Control root)
    {
        _interceptOverlay = Modal(root, 330, 190, out VBoxContainer col, "INTERCEPT", EnemyColor);
        _interceptTitle = UiKit.Text("", 22, Colors.White);
        _interceptTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(_interceptTitle);
        col.AddChild(UiKit.Text("拠点にいる隊を1つ選んでください（抜けなければ次の隊）", 13, UiKit.Faint));
        _interceptList = new VBoxContainer();
        _interceptList.AddThemeConstantOverride("separation", 6);
        _interceptList.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(_interceptList);
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

    /// <summary>その隊がこの作戦ターンに何をするか（隊の札と案内に同じものを出す）。</summary>
    private static string OrderText(int squad) => Map11Session.OrderText(squad);

    // =================================================================================
    // 第174期 部B —— 作戦ターン
    //
    // **判定は1つも持たない。** 順番は `Map11Session.StepTurn` が持ち、規則は `Map11State`。
    // ここがするのは「止まった理由に応じて窓を出す」ことだけ。
    // =================================================================================

    private void AdvanceTurn()
    {
        if (St.Finished) { ShowResult(); return; }
        if (Map11Session.TurnRunning) return;
        if (_reform) { _reform = false; ClearHold(); }
        Map11Session.BeginTurn();
        Pump();
    }

    // ---- 第175期 §1-2: 「▶ 進める」——何かが起きるまで作戦ターンを自動で回す ----

    /// <summary>止まる理由を調べるために、作戦ターンの前に写しておく盤面。</summary>
    private readonly record struct Watch(bool[] Arrived, bool[] Imminent);

    private static Watch Take() => new(
        Enumerable.Range(0, St.Squads.Length).Select(AtDest).ToArray(),
        Enumerable.Range(0, Map11.RoadCount)
                  .Select(r => St.NextNode(r) is { } n && n.Cell <= 0).ToArray());

    /// <summary>その隊は行き先に着いているか。</summary>
    private static bool AtDest(int squad)
    {
        Dest d = Map11Session.Destinations[squad];
        Map11State.Squad s = St.Squads[squad];
        if (s.Lost) return true;
        return d.AtHome ? s.Cell < 0 : s.Road == d.Road && s.Cell == d.Cell;
    }

    /// <summary>
    /// 1 作戦ターンが終わったところで、自動で回すのを止める理由（無ければ null）。
    ///
    /// <para><b>止まるのは4つ</b>（指示書 §1-2）——接敵・迎撃は <see cref="Map11Session.StepKind"/> が
    /// 返すので、ここが見るのは残り2つ:<b>隊が行き先に着いた</b>と
    /// <b>敵が次の前進で拠点に着く</b>。後者を「あと n が 1 になった」と書くと、
    /// <b>K = 1 では毎ターン真になって意味を持たない</b>（<c>TurnsToAdvance</c> は常に 1）ので、
    /// <b>敵のマスで見る</b>。</para>
    /// </summary>
    private static string? StopReason(Watch before)
    {
        for (int i = 0; i < St.Squads.Length; i++)
            if (!before.Arrived[i] && AtDest(i) && !St.Squads[i].Lost)
                return $"{St.Squads[i].Def.Name} が {Map11Orders.Where(Map11Session.Destinations[i])} に着いた";
        for (int r = 0; r < Map11.RoadCount; r++)
            if (!before.Imminent[r] && St.NextNode(r) is { } n && n.Cell <= 0)
                return $"{Map11.RoadNames[r]} の {n.Def.Name} が拠点の手前まで来た（次の前進で迎撃）";
        return null;
    }

    /// <summary>
    /// <b>何かが起きるまで</b>作戦ターンを回す。
    /// <b>止まらずに回り続けることはない</b>——作戦ターンの上限（<see cref="Map11.TurnCap"/>）に
    /// 達すると <see cref="Map11State.Finished"/> が真になるので、最悪でもそこで終わる。
    /// </summary>
    private void RunUntilEvent()
    {
        if (St.Finished) { ShowResult(); return; }
        if (Map11Session.TurnRunning) { Pump(); return; }
        if (_reform) { _reform = false; ClearHold(); }
        _running = true;

        for (int guard = Map11.TurnCap + 2; guard > 0; guard--)
        {
            if (St.Finished) break;
            Watch before = Take();
            Map11Session.BeginTurn();
            Map11Session.StepKind kind = Map11Session.StepTurn();
            Refresh();
            switch (kind)
            {
                case Map11Session.StepKind.Battle: _running = false; ShowEncounter(); return;
                case Map11Session.StepKind.Intercept: _running = false; ShowIntercept(); return;
                case Map11Session.StepKind.Fallen: _running = false; ShowResult(); return;
            }
            if (StopReason(before) is { } why)
            { _running = false; ShowToast(why, GoldColor, 5.0f); Refresh(); return; }
        }

        _running = false;
        Refresh();
        if (St.Finished) ShowResult();
    }

    /// <summary>作戦ターンの続きを流す。<b>戦闘のところで止まって窓を出す。</b></summary>
    private void Pump()
    {
        Map11Session.StepKind kind = Map11Session.StepTurn();
        Refresh();
        switch (kind)
        {
            case Map11Session.StepKind.Battle: ShowEncounter(); return;
            case Map11Session.StepKind.Intercept: ShowIntercept(); return;
            case Map11Session.StepKind.Fallen: ShowResult(); return;
            default: if (St.Finished) ShowResult(); return;
        }
    }

    /// <summary>迎撃の窓。<b>拠点にいる隊を1つ選ぶ</b>（判定は `Map11State` が持つ）。</summary>
    private void ShowIntercept()
    {
        if (Map11Session.InterceptNode is not { } node) { Pump(); return; }
        _interceptTitle.Text = $"迎撃: {node.Def.Name} が拠点に来た";
        foreach (Node child in _interceptList.GetChildren()) child.QueueFree();
        for (int i = 0; i < St.Squads.Length; i++)
        {
            Map11State.Squad s = St.Squads[i];
            if (s.Lost || s.Cell >= 0 || !St.CanSend(i)) continue;
            if (node.Tried.Contains(i)) continue;
            int index = i;
            string stat = s.Units is { } u
                ? $"{u.Count(x => x.IsAlive)} 枚 ・ HP {SquadHpPercent(s):F0}%"
                : $"{s.Def.F.Occupied().Count()} 枚 ・ HP 100%（未出撃）";
            Button b = UiKit.ActionButton($"{s.Def.Name}  {stat}", GoldColor);
            b.Pressed += () => ChooseInterceptor(index);
            _interceptList.AddChild(b);
        }
        _interceptOverlay.Visible = true;
    }

    private void ChooseInterceptor(int squad)
    {
        _interceptOverlay.Visible = false;
        if (!Map11Session.ChooseInterceptor(squad))
        {
            ShowToast("その隊は迎撃に出せません", EnemyColor, 3.0f);
            Pump();
            return;
        }
        _selected = squad;
        Refresh();
        ShowEncounter();
    }

    /// <summary>
    /// 接敵の窓。<b>相手はもう決まっている</b>（`Map11Session` が戦闘を用意して止まった）ので、
    /// ここは<b>決める瞬間の隣に勝ち筋と敵のルールを並べる</b>ためだけにある（第171期 §1-3）。
    /// </summary>
    private void ShowEncounter()
    {
        if (Map11Session.PendingNode is not { } node) { Pump(); return; }
        int squad = Map11Session.PendingSquad;
        Map11State.Squad s = St.Squads[squad];
        if (s.Units is null) { Pump(); return; }
        _selected = squad;
        if (_foeView != (node.Def.Road, node.Def.Index)) _foePick = -1;
        _foeView = (node.Def.Road, node.Def.Index);

        int alive = node.Units?.Count(u => u.IsAlive) ?? node.Def.Enemy.Occupied().Count();
        bool athome = node.Cell < 0;
        _encounterTitle.Text = (athome ? "迎撃  " : "") + $"{s.Def.Name} × {node.Def.Name}";
        string plan = Map11Info.PlanOf(s.Def.Id).Replace("**", "");
        _encounterBody.Text =
            "[color=#8f9b90]" + (athome
                ? "拠点まで来られた。ここで抜けなければ次の隊が出る。"
                : $"{Map11.RoadNames[node.Def.Road]} {node.Def.Index + 1} 戦目。")
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

    // 第175期: 「戻る」のボタンは無い。**盤の拠点を押せば引き返す**（行き先が拠点になる）
    // ——第173期の「一瞬で戻す」口（`Map11State.Withdraw`）は時間なしの版のために残してある。

    /// <summary>戦う。<b>戦闘はもう用意されている</b>（`StepTurn` が止まる前に用意した）。</summary>
    private void Fight()
    {
        _encounterOverlay.Visible = false;
        if (!Map11Session.HasPendingBattle) { ShowToast("出撃できません", EnemyColor, 3.0f); Pump(); return; }
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

    // ---- 第174期 部A #2: 組み直しで関係が変わったら1行出す ----

    /// <summary>その隊のいまの顔ぶれ（盤上の駒があればそれ、無ければ定義）。</summary>
    private static Dictionary<int, UnitDef> SeatsOf(int squad)
    {
        Map11State.Squad s = St.Squads[squad];
        if (s.Units is { } u)
            return u.Where(x => x.IsAlive && !x.Def.Traits.Contains(TraitId.Ephemeral))
                    .ToDictionary(x => x.Slot, x => x.Def);
        return s.Lost ? new Dictionary<int, UnitDef>()
                      : s.Def.F.Occupied().ToDictionary(o => o.Slot, o => o.Def);
    }

    /// <summary>
    /// いま盤面に立っている関係を「語｜誰から｜誰へ」で写す。
    /// <b>3 隊ぶん全部を写す</b>——隊どうしの入れ替えは2つの隊を同時に動かすため。
    /// </summary>
    private static List<string> RelationKeys()
    {
        var list = new List<string>();
        for (int i = 0; i < St.Squads.Length; i++)
        {
            var seats = SeatsOf(i);
            foreach (Map11Relations.Link l in Map11Relations.Of(seats))
                list.Add($"{l.Word}|{seats[l.From].Name}|{seats[l.To].Name}");
        }
        return list;
    }

    /// <summary>
    /// 組み直しの前後で関係の差分を出す（第174期 部A #2）。
    /// <b>判定も計算も持たない</b>——`Map11Relations.Of` を前後で取って引き算するだけ。
    /// </summary>
    private void NoteRelationChange(List<string> before)
    {
        var after = RelationKeys();
        var added = after.Where(x => !before.Contains(x)).ToList();
        var gone = before.Where(x => !after.Contains(x)).ToList();
        if (added.Count == 0 && gone.Count == 0)
        {
            ShowToast("入れ替えた（関係は変わらない）", UiKit.Muted, 3.0f);
            return;
        }
        static string Fmt(string key, bool on)
        {
            string[] p = key.Split('|');
            return on ? $"{p[0]}: {p[1]} → {p[2]}" : $"{p[0]}: 切れた（{p[1]} → {p[2]}）";
        }
        var parts = added.Take(2).Select(x => Fmt(x, true))
            .Concat(gone.Take(2).Select(x => Fmt(x, false)));
        ShowToast(string.Join(" ／ ", parts), GoldColor, 6.0f);
    }

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
        // **ここでは関係の差分は出さない**（部A #2 は入れ替えの窓口だけ）
        // ——「元に戻す」は5席まとめて動くので、差分の1行にすると読めなくなる。
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
            var before = RelationKeys();
            if (!St.SwapWithBench(_selected, slot, _holdBench))
                ShowToast("その席へは入れられません", EnemyColor, 3.0f);
            else NoteRelationChange(before);
            ClearHold();
        }
        else if (_holdSlot >= 0)
        {
            if (_holdSquad == _selected && _holdSlot == slot) ClearHold();
            else
            {
                var before = RelationKeys();
                if (!St.SwapSeats(_holdSquad, _holdSlot, _selected, slot))
                    ShowToast("その2席は入れ替えられません（拠点にいる隊だけです）", EnemyColor, 3.0f);
                else NoteRelationChange(before);
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
            var before = RelationKeys();
            if (!St.SwapWithBench(_holdSquad, _holdSlot, index))
                ShowToast("その駒は動かせません", EnemyColor, 3.0f);
            else NoteRelationChange(before);
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
            // **失われた隊を満タンの既定編成で描かない**（第173期 §1-3 #3）——全滅すると
            // `Units` が null になるので、第172期はここで定義の値を満タンで描いていた。
            foreach ((int slot, UnitDef def) in s.Def.F.Occupied())
                cells.Add(new SeatDiagram.Cell(slot, def, s.Lost ? 0 : def.MaxHp, def.MaxHp, !s.Lost));

        // **空席も描く**（第172期）——組み直しで控えの駒を入れられる場所だから。
        foreach (int slot in Enumerable.Range(0, FormationRules.PlayableSlotCount))
            if (cells.All(c => c.Slot != slot))
                cells.Add(new SeatDiagram.Cell(slot, null, 0, 0, false));
        cells.Sort((x, y) => x.Slot.CompareTo(y.Slot));

        RenderSquadSummary(cells, s);
        _squadDiagram.AllowEmptyPress = _reform;
        _squadDiagram.DeadLabel = s.Lost ? "（失われた）" : "（戦死）";
        _squadDiagram.Render(cells, _squadPick);

        foreach (Node child in _squadFocus.GetChildren()) child.QueueFree();
        if (_squadPick < 0 || cells.First(c => c.Slot == _squadPick).Def is null)
        {
            FocusHint(_squadFocus, _reform
                ? "札を押すとつまみます。もう1枚押すと入れ替わります"
                : s.Lost
                    ? "（この隊は失われました。もう出せません）"
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
        // **色は線と同じ 得／損／両方**（第173期 §1-1・出どころは `SeatLinks.ColorOf` の1本）。
        foreach ((string rel, Color tint) in RelationWordsFor(cells, _squadPick))
            FocusLine(_squadFocus, rel, tint);
    }

    /// <summary>
    /// 図の下に<b>常時</b>出す2つ（第174期 部A #1・#4）——
    /// <b>(1) この隊の関係を 3〜5 行</b>（押さなくても読める。第173期は札を押さないと1本も出なかった）と、
    /// <b>(2) 隊全体にかかる札</b>（席に紐づかないので線が引けないもの）。
    ///
    /// <para><b>並びは 損 → 両方 → 得。</b> 5 行で切るので、<b>切り落とされるのは必ず得の側</b>
    /// ——置く前に気づきたいのは損のほうである（第173期 §1-1 の色と同じ考え）。</para>
    ///
    /// <para><b>元データは手書きしない</b>——線は <see cref="Map11Relations.Of"/>、
    /// 全体札は <see cref="Map11Relations.GlobalNotes"/>（`Unresolved` の「位置を問わない」から引く）。</para>
    /// </summary>
    private void RenderSquadSummary(IReadOnlyList<SeatDiagram.Cell> cells, Map11State.Squad s)
    {
        foreach (Node child in _squadSummary.GetChildren()) child.QueueFree();
        var seats = cells.Where(c => c.Def is not null && c.Alive)
                         .ToDictionary(c => c.Slot, c => c.Def!);
        var links = Map11Relations.Of(seats)
            .OrderBy(l => l.Sign == Map11Relations.LinkSign.Loss ? 0
                        : l.Sign == Map11Relations.LinkSign.Both ? 1 : 2)
            .ThenBy(l => l.From)
            .ToList();

        _squadSummary.AddChild(UiKit.Text(
            links.Count == 0 ? "この隊の関係: （線は1本も無い）"
                             : $"この隊の関係（{Math.Min(links.Count, SummaryLines)} / {links.Count} 本）",
            12, UiKit.Faint));
        foreach (Map11Relations.Link l in links.Take(SummaryLines))
            FocusLine(_squadSummary,
                $"[{Map11Relations.LabelOf(l.Sign)}] {l.Word}{(l.Conditional ? "（条件つき）" : "")}: "
                + $"{seats[l.From].Name} → {seats[l.To].Name}",
                SeatLinks.ColorOf(l.Sign));

        var globals = Map11Relations.GlobalNotes(seats.Values).Take(2).ToList();
        if (globals.Count == 0) return;
        _squadSummary.AddChild(UiKit.Text("この隊全体にかかるもの", 12, UiKit.Faint));
        foreach ((string unit, string why) in globals)
            FocusLine(_squadSummary, $"{unit}: {why}", GoldColor);
    }

    /// <summary>常時出す関係の行数（指示書 部A #1 は 3〜5 行）。</summary>
    private const int SummaryLines = 4;

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
        _foeDiagram.DeadLabel = "（倒れた）";
        _foeDiagram.Render(cells, _foePick);

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
        foreach (string l in Map11Info.LinesOf(d)) FocusLine(_foeFocus, l, UiKit.Muted);
        if (Map11Info.IsBoardRuleHolder(d))
            FocusLine(_foeFocus, "★ この駒が倒れるとルールが消える", GoldColor);
        foreach ((string rel, Color tint) in RelationWordsFor(cells, _foePick))
            FocusLine(_foeFocus, rel, tint);
        // 第174期 部A #4 —— 敵側も同じ「部隊全体にかかるもの」を出す（盤面ルールは別に出ている）。
        foreach ((string unit, string why) in Map11Relations.GlobalNotes(new[] { d }))
            FocusLine(_foeFocus, $"{unit}: {why}", GoldColor);
    }

    /// <summary>
    /// その席から出ている／その席に入っている関係を文にする。
    /// <b>元データは `Map11Relations`（席から機械で引いたもの）そのまま。</b>
    /// </summary>
    private static IEnumerable<(string Text, Color Tint)> RelationWordsFor(
        IReadOnlyList<SeatDiagram.Cell> cells, int slot)
    {
        var seats = cells.Where(c => c.Def is not null && c.Alive)
                         .ToDictionary(c => c.Slot, c => c.Def!);
        foreach (Map11Relations.Link l in Map11Relations.Of(seats))
        {
            // **語に意味の1行を添える**（第172期 §1-3）——第171期の観察ログ
            // 「ヒサの標が**どんな効果なのか分からない**」（問い1）への答え。
            // 文は `Map11Relations.Rules` が持ち、**網羅性は `--map11-phase172` が門にする**。
            string mean = Map11Relations.MeanOf(l.Trait) is { Length: > 0 } m ? $"（{m}）" : "";
            // **得／損／両方 と、条件つき（点線）かを文にも出す**（第173期 §1-1）
            // ——線の色に気づかなくても、押せば同じことが文で読める。
            string sign = $"[{Map11Relations.LabelOf(l.Sign)}]";
            string dash = l.Conditional ? "（条件つき）" : "";
            Color tint = SeatLinks.ColorOf(l.Sign);
            if (l.From == slot) yield return ($"{sign} → {seats[l.To].Name}：{l.Word}{dash}{mean}", tint);
            else if (l.To == slot) yield return ($"{sign} ← {seats[l.From].Name}：{l.Word}{dash}{mean}", tint);
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
    /// 右で選んでいる敵部隊の中身。<b>第173期 §1-4 に敵の <c>UnitDef</c> へ説明文を入れた</b>ので、
    /// 味方と同じ出どころ（<see cref="Map11Info.LinesOf"/>）から引く。
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
            foreach (string line in Map11Info.LinesOf(r.Def))
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

    /// <summary>戦闘の記録の1行。<b>第174期に「作戦ターン」と「迎撃」の印を足した</b>（§2-2）。</summary>
    private static string HistoryLine(Map11Session.Line l) =>
        (l.Turn > 0 ? $"T{l.Turn} " : "") + $"{l.No}戦目  "
        + (l.Intercept ? "【迎撃】" : "") + $"{l.Squad} → {l.Road} {l.Foe}"
        + $"  {(l.Won ? "抜いた" : "抜けず")}  （残り {l.Alive}枚 ・ HP {l.HpPercent:F0}%）";

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

    /// <summary>札の中の改行（この行以外に生の改行文字を書かないための定数）。</summary>
    private static readonly string Nl = "\n";

    /// <summary>マスの名前。<b>出どころは <see cref="Map11Orders.CellName"/> の1本</b>（写しを持たない）。</summary>
    private static string CellName(int road, int cell) => Map11Orders.CellName(cell);

    /// <summary>
    /// 隊の HP 割合。<b>分母は「いまのロスター」</b>（第174期 部A #8）——
    /// 第173期までは<b>定義上の5枚</b>の <c>MaxHp</c> の和だったので、
    /// 控えの駒と入れ替えたり駒が倒れたりした隊で割合が合わなくなっていた。
    /// </summary>
    private static double SquadHpPercent(Map11State.Squad s)
    {
        if (s.Units is not { } u) return 100.0;
        int max = u.Sum(x => x.MaxHp);
        return max == 0 ? 0 : 100.0 * u.Where(x => x.IsAlive).Sum(x => x.Hp) / max;
    }

    private static double NodeHpPercent(Map11State.Node n)
        => n.DefMaxHp == 0 ? 0 : 100.0 * n.HpNow / n.DefMaxHp;

    private void Refresh()
    {
        // 第174期 §2-2: **画面上部に「作戦ターン N」**。時間を切った設定では出さない。
        _headline.Text = $"検証用マップ 1-1 ・ "
            + (St.Time.On ? $"作戦ターン {St.Turn + 1} ・ " : "")
            + $"抜いた敵部隊 {St.ClearedCount} / {Map11.TotalNodes}"
            + $" ・ 戦闘 {St.Battles} 回 ・ seed {Map11Session.Seed}";

        for (int i = 0; i < St.Squads.Length; i++)
        {
            Map11State.Squad s = St.Squads[i];
            string where = s.Lost ? "失われた"
                : s.Cell < 0 ? (s.Units is null ? "未出撃（拠点）" : "拠点")
                : $"{Map11.RoadNames[s.Road]} {CellName(s.Road, s.Cell)}";
            string stat = s.Units is null
                ? (s.Lost ? "—" : $"{s.Def.F.Occupied().Count()} 枚 ・ HP 100%")
                : $"{s.Units.Count(u => u.IsAlive)} 枚 ・ HP {SquadHpPercent(s):F0}%";
            _squadButtons[i].Text = $"{s.Def.Name}  ({s.Def.Role})" + Nl + stat + Nl + where
                + (s.Lost || !St.Time.On ? "" : $"　命令: {OrderText(i)}");
            _squadButtons[i].Disabled = s.Lost;
            _squadButtons[i].Modulate = i == _selected ? new Color(0.72f, 1.0f, 0.90f) : Colors.White;

            // **盤上に札を出すのは道へ出ている隊だけ**（第170期）。待機と全滅は左のパネルが持つ
            // ——中身のパネルを常時開いたぶんパネルが伸びて、拠点の札が下に隠れたため。
            // 同じ言葉の表を2つ作らない（第124期 §4）。
            // 第174期: **位置はマス単位**（道の上も拠点も同じ規則で置く）。
            Marker m = _squadMarkers[i];
            if (s.Lost) { m.Set("", PlayerColor, visible: false); _arrows[i].Hide(); }
            else
            {
                int alive = s.Units?.Count(u => u.IsAlive) ?? s.Def.F.Occupied().Count();
                // 拠点にいる隊は**縦に並べる**（3 隊が重なると札が読めない）。
                Vector3 at = s.Cell >= 0
                    ? CellPos(s.Road, s.Cell) + new Vector3(-3.4f, 0, 0)
                    : BasePos + new Vector3(0.6f, 0, (i - 1) * 3.6f - 0.4f);
                if (_firstRefresh) m.MoveTo(at); else m.SlideTo(at);
                m.Set($"{s.Def.Name}" + Nl + $"{alive}枚 {SquadHpPercent(s):F0}%", PlayerColor);
                // 第175期 §1-2 —— **隊から行き先へ矢印**（選んでいる隊だけ太く見せる必要は無い）。
                Dest d = Map11Session.Destinations[i];
                bool arrived = AtDest(i);
                if (arrived || St.Finished) _arrows[i].Hide();
                else _arrows[i].Set(at, d.AtHome ? BasePos : CellPos(d.Road, d.Cell));
            }
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
                // 第175期 §1-3: **「あと n ターンで前進」はパネルから消して盤の上へ移した。**
                // 第170期から3期続けて左右のパネルへ行を足しており、
                // ポンは第174期にこの行に「気づかなかった」（観察ログ 問い2）。
                string text = n.Cleared
                    ? $"{i + 1}. {n.Def.Name} — 撃破"
                    : $"{i + 1}. {n.Def.Name}" + Nl + Map11.RuleLineOf(n.Def.Enemy) + Nl
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
                if (!n.Cleared)
                {
                    Vector3 at = n.Cell < 0 ? BasePos + new Vector3(0, 0, road == 0 ? -3.4f : 3.4f)
                                            : CellPos(road, n.Cell);
                    // 第175期 §1-3: **前進した瞬間は1マス滑らせる**（瞬間移動させない）。
                    if (_firstRefresh) m.MoveTo(at); else m.SlideTo(at);
                }
                m.Set(n.Cleared ? "" : $"{n.Def.Name}" + Nl + $"{alive} 体 ・ {NodeHpPercent(n):F0}%",
                      EnemyColor, !n.Cleared);
                // 第175期 §1-3: **盤の上に「← あと n」**（拠点の向きの矢印つき）。先頭の敵だけ。
                if (!n.Cleared && St.Time.On && St.NextNode(road) == n)
                {
                    int? left = Map11Session.TurnsToAdvance(road);
                    bool soon = n.Cell <= 0 || left is 1;
                    m.SetNote(n.Cell < 0 ? "◀ 拠点に到達" : $"◀ あと {left}",
                              soon ? UiKit.Hurt : GoldColor, soon);
                }
                else m.SetNote("", GoldColor, false);
            }
        }

        int waiting = St.Squads.Count(x => !x.Lost && x.Road < 0);
        _homeLabel.Text = waiting == 0 ? "拠点" : $"拠点（待機 {waiting} 隊）";

        RefreshSquadDetail();
        RefreshFoeDetail();
        RefreshHistory();
        RefreshBench();
        RefreshReform();

        bool busy = St.Finished || Map11Session.TurnRunning;
        Map11State.Squad sel = St.Squads[_selected];
        RefreshDestButtons();
        _run.Disabled = busy;
        _advanceTurn.Disabled = busy;
        // 第175期: **1 行にまとめた**（第174期は「いまの命令」と「行き先」で2行になっていた）。
        _orderHint.Text = St.Time.On
            ? $"{sel.Def.Name} の行き先: {Map11Orders.Where(Map11Session.Destinations[_selected])}"
              + $"　→ この作戦ターン: {OrderText(_selected)}"
            : "";
        // 操作の案内は**まだ1戦もしていない間だけ**（観察ログ「操作方法が最初わからなかった」）。
        _hint.Visible = Map11Session.BattleLog.Count == 0;
        _firstRefresh = false;
    }

    private void ShowToast(string text, Color color, float seconds)
    {
        _toast.Text = text;
        _toast.AddThemeColorOverride("font_color", color);
        _toastClock = seconds;
    }

    public override void _Process(double delta)
    {
        // 第175期 §1-3: 盤の札を滑らせる／脈打たせる。**判定は1つも通らない**（見た目だけ）。
        foreach (Marker m in _squadMarkers) m.Tick(delta);
        foreach (Marker m in _nodeMarkers) m.Tick(delta);
        foreach (Marker m in _cellMarkers) m.Tick(delta);
        // 画面の大きさが変わると札の位置がずれるので、毎フレーム引き直す（7 枚だけ）。
        foreach (DestButton d in _destButtons)
            d.Node.Position = _camera.UnprojectPosition(d.World) + d.Offset;

        if (_toastClock <= 0) return;
        _toastClock -= (float)delta;
        if (_toastClock <= 0) _toast.Text = "";
    }
}
