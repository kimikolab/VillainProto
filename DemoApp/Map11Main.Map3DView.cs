using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 3D のジオラマの見た目の段（発注書 §2-2 ジオラマ ／ §2-3 動き ／ §2-4 UI）——今の作戦マップの画面へのつなぎ。
// <b>判定は 1 つも持たない。</b>ここがするのは「盤の状態を読んで駒・台・UI に写す」ことと「押されたら今の画面と同じ口を呼ぶ」ことだけ。
// `Map3DMode.Active` が偽（2D）のときは、このファイルのものは 1 つも動かない。
//   ジオラマ       Map3DDiorama.cs（`Map3DStage`）  位置の表  Map3DLayout.cs
//   駒・マスの台   Map3DPiece.cs                     カメラ    Map3DCamera.cs
//   遊ぶ UI        Map3DHud.cs                       検証      Map11Main.Map3D.cs（`Map3DDebugLayer`）
public partial class Map11Main
{
    /// <summary>3D の位置の表（2D のときは null——<see cref="PosOf"/> は盤の座標を返す）。</summary>
    private static Map3DLayout? _layout3d;
    private Map3DCamera? _cam3d;
    private Map3DHud? _hud3d;
    /// <summary>今の窓（2D の UI の層）の拡大率。3D の層は基準が 1920×1080 なので、1440×900 用の窓を同じ見かけの大きさに拡げる。</summary>
    private float _uiScale = 1f;
    /// <summary>拠点が空になる行き先を選ぼうとしているか（第479期・名札の点滅と敵の点線の太さだけが読む）。</summary>
    private bool _homeWarn3d;
    private ColorRect? _headerBand;
    private readonly Dictionary<Marker, Map3DPiece> _pieces3d = new();
    private readonly Dictionary<Map3DPiece, int> _pieceCell = new();
    private readonly Dictionary<int, Map3DCellMark> _cellMarks3d = new();
    private readonly List<Map3DCellMark> _approachMarks3d = new();
    private readonly Dictionary<DestButton, Label> _chips3d = new();
    private readonly List<MeshInstance3D> _routeDots3d = new();
    private Label? _homePlate3d, _portalPlate3d;
    /// <summary>中間拠点の名札（第478期・常に出す・文は盤の上の札と同じ）。</summary>
    private readonly Dictionary<int, Label> _outpostPlates3d = new();
    private float _clock3d;

    /// <summary>今の窓の層から見た画面の大きさ（2D では画面そのもの）。</summary>
    private Vector2 UiViewSize => GetViewport().GetVisibleRect().Size / _uiScale;

    // =================================================================================
    // 組み立て
    // =================================================================================

    /// <summary>`BuildWorld` の 3D 版。ジオラマ・カメラ・マスの台を組み、今の画面が使う札（位置の覚え）は見えない形で用意する。</summary>
    private void BuildWorld3D()
    {
        _labelScale = 1f;
        _layout3d = new Map3DLayout(St.Stage, St.Graph);
        _map3d = new Map3DStage { Name = "Map3DStage" };
        AddChild(_map3d);
        _map3d.Build(_layout3d, St.Graph);
        _cam3d = new Map3DCamera { Name = "Map3DCamera" };
        AddChild(_cam3d);
        _cam3d.Setup(St.Stage.Id, _layout3d.Look, _layout3d.Reach, _layout3d.Pan);
        // 第478期 §4-1: ステージを始めたとき・やり直したときは、始めの視点（味方の拠点・分かれ道・3 本の道の入口）へ戻す
        // ——視点は場面を読み直しても残る値なので、やり直しでは前の通しの終わりの位置のままだった（試遊 2026-11-16）。
        if (St.Turn == 0 && St.Battles == 0 && !Map11Session.TurnRunning) _cam3d.ResetView();
        _camera = _cam3d.Camera;

        // ---- 今の画面が読み書きする札（2D の盤の板）。3D では見せない——中身（出ているか・添え書き）だけを駒が読む。 ----
        Marker Hidden(Vector3 at, Color color, float radius)
        {
            var m = new Marker(this, at, color, radius);
            m.Root.Visible = false;
            return m;
        }
        _homeLabel = WorldLabel("拠点", Vector3.Zero, 30, PlayerColor);
        _homeLabel.Visible = false;
        AddChild(_homeLabel);
        for (int road = 0; road < St.Stage.LaneCount; road++)
        {
            Marker m = Hidden(PosOf(St.Graph.Lanes[road][0]), Color.FromHtml("#6f6a52"), 1.1f);
            m.Set("", Color.FromHtml("#6f6a52"));
            _cellMarkers.Add(m);
        }
        for (int road = 0; road < St.Stage.LaneCount; road++)
            for (int i = 0; i < St.Stage.Lanes[road].Count; i++)
                _nodeMarkers.Add(Hidden(PosOf(St.Nodes[road][i].Pos), EnemyColor, 2.0f));
        for (int s = 0; s < St.Squads.Length; s++)
        {
            _squadMarkers.Add(Hidden(BasePos, PlayerColor, 1.3f));
            _arrows.Add(new Arrow(this, PlayerColor));
        }

        // ---- マスの円い台（味方 ／ 敵 ／ 空の色は `RefreshMap3D` が塗る） ----
        foreach (MapGraph.Cell c in St.Graph.Cells)
        {
            // 第478期 §4-2: 中間拠点ごとの見返りを持つ盤では、中間拠点の台を大きく・八角に・旗つきにする（ほかの盤は今のまま）。
            bool flagged = c.Kind == MapGraph.Kind.Outpost && St.Stage.OutpostRewards is not null;
            // 第479期 §5: 拠点 3 種（味方の拠点・中間拠点・敵の拠点）は高い旗竿と大きな幟・地面の色の輪。中間拠点の台は 1.5 倍・ふつうのマスは小さく。
            bool lean = St.Stage.OutpostRewards is not null || (Chapter1.Active && Chapter1.Rule.Lean);
            float radius = c.Kind switch { MapGraph.Kind.Home => 2.5f, MapGraph.Kind.Portal => 2.3f, MapGraph.Kind.Outpost => flagged ? 3.3f : 1.9f, _ => lean ? 1.25f : 1.55f };
            var kind = !lean ? Map3DCellMark.Base.None : c.Kind switch
            {
                MapGraph.Kind.Home => Map3DCellMark.Base.Home, MapGraph.Kind.Portal => Map3DCellMark.Base.Portal,
                MapGraph.Kind.Outpost => Map3DCellMark.Base.Outpost, _ => Map3DCellMark.Base.None,
            };
            var reward = flagged && St.RewardAt(c.Id) is { } rw ? (rw.Relic ? Map3DCellMark.Reward.Relic : rw.Gold > 0 ? Map3DCellMark.Reward.Gold : Map3DCellMark.Reward.None) : Map3DCellMark.Reward.None;
            var mark = new Map3DCellMark(radius, flagged, kind, reward) { Position = PosOf(c.Id) };
            _map3d.AddChild(mark);
            _cellMarks3d[c.Id] = mark;
        }
        if (!St.Portal.On) return;
        _portalMarker = Hidden(PortalPos, EnemyColor, 2.2f);
        // 敵の拠点へは経路ごとに入る（北から ／ 南から）ので、本陣の手前に経路ごとの小さな台を置く。
        for (int lane = 0; lane < St.Graph.Lanes.Count; lane++)
        {
            var mark = new Map3DCellMark(1.05f) { Position = _layout3d.Approach[lane] };
            _map3d.AddChild(mark);
            _approachMarks3d.Add(mark);
        }
        // 表の無いステージ（1-2 ／ 1-3）の中間拠点は今の絵と名札のまま。
        foreach (MapGraph.Cell c in St.Graph.Cells.Where(c => c.Kind == MapGraph.Kind.Outpost))
        {
            Vector3 at = PosOf(c.Id);
            if (_layout3d.Sheet is not { Outposts.Length: > 0 }) AddOutpostArt(c.Id);   // 第477期: 表に建物のある中間拠点は建物が絵
            Label3D name = WorldLabel(c.Name, new Vector3(at.X, at.Y + 0.6f, at.Z + 4.2f), 30, EnemyColor);
            AddChild(name);
            _outpostMarks.Add((c.Id, name));
        }
    }

    /// <summary>遊ぶ UI を組み、今の窓（2D の UI の層）を 3D の基準に合わせる。`BuildMap3D` が盤と UI のあとに 1 度呼ぶ。</summary>
    private void BuildHud3D()
    {
        _hud3d = new Map3DHud { Name = "Map3DHud" };
        AddChild(_hud3d);
        _hud3d.SquadPressed += index => Select(index);
        _hud3d.SquadAction += OnSquadAction3D;   // 第479期: 隊の札の上の小さなボタン（呼び戻す・強化・休む）
        _hud3d.LeanText = true;
        _hud3d.DetailPressed += () => { _showSquadDetail = !_showSquadDetail; _showFoes = _showHistory = false; RefreshScreen(); };
        _hud3d.ReformPressed += ToggleReform;
        // まだ開けていない隊の枠: 今の編成の窓で開ける（拠点にいる隊の組み直しを開く）。
        _hud3d.SlotsPressed += () =>
        {
            int home = Enumerable.Range(0, St.Squads.Length).FirstOrDefault(St.CanReform, -1);
            if (home < 0) { ShowToast("隊の枠は、拠点に隊がいるときに編成の窓で開けられます", EnemyColor, 3.0f); return; }
            if (_selected != home) Select(home);
            if (!_reform) ToggleReform();
        };
        _hud3d.RunPressed += ToggleClock;
        _hud3d.StepPressed += AdvanceTurn;
        _hud3d.SpeedPressed += fast => { Map11Clock.Fast = fast; RefreshClock(); };
        _hud3d.FoesPressed += () => { _showFoes = !_showFoes; _showSquadDetail = _showHistory = false; RefreshScreen(); };
        _hud3d.HistoryPressed += () => { _showHistory = !_showHistory; _showFoes = _showSquadDetail = false; RefreshScreen(); };

        // 今の窓はそのまま使う。基準が 1440×900 の作りなので、層ごと拡げて同じ見かけの大きさにする（文字も一緒に大きくなる）。
        _uiRoot.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        _uiRoot.Position = Vector2.Zero;
        FitOldUi();
        // 引き出し（隊の配置・敵部隊・戦闘の記録）は四隅の UI に重ならない高さに。
        Bounds(_squadDrawer, 20, 150, 354, -224, anchorBottom: 1);
        // 第476期（不具合 5）: 敵部隊の窓は中身の幅（約 410）が枠（334）より広く、右へはみ出して画面の外に切れていた——枠を広げ、足りなければ左へ伸ばす。
        Bounds(_foeDrawer, -440, 150, -20, -224, anchorLeft: 1, anchorRight: 1, anchorBottom: 1);
        Bounds(_historyDrawer, -640, 150, -20, -224, anchorLeft: 1, anchorRight: 1, anchorBottom: 1);
        _foeDrawer.GrowHorizontal = _historyDrawer.GrowHorizontal = Control.GrowDirection.Begin;
        // 行き先の札は「押す領域」としてだけ残す（見た目はマスの台と「あと n」の札）。経路の線も 3D の点線に替える。
        foreach (DestButton d in _destButtons)
        {
            d.Node.SelfModulate = Colors.Transparent;
            d.Node.ClipText = true;
            d.Node.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        }
        _routePreview.Modulate = Colors.Transparent;

        _homePlate3d = PlacePlate();
        if (St.Portal.On) _portalPlate3d = PlacePlate();
        // 第478期 §4-2: 中間拠点の名前と見返りの名札は常に出す（盤の上の小さな字は読みにくかった・中間拠点ごとの見返りを持つ盤だけ）。
        if (St.Stage.OutpostRewards is not null)
            foreach (var (cell, name) in _outpostMarks)
            {
                name.Visible = false;
                _outpostPlates3d[cell] = PlacePlate();
            }
    }

    private Label PlacePlate()
    {
        Label plate = Map3DHud.Text("", 21, UiKit.Ink, bold: true);
        plate.HorizontalAlignment = HorizontalAlignment.Center;
        plate.AddThemeStyleboxOverride("normal", Map3DHud.Box(new Color(0.05f, 0.06f, 0.08f, 0.82f), Map3DHud.Edge, 2, 10, 8));
        _hud3d!.Plates.AddChild(plate);
        return plate;
    }

    /// <summary>拠点の名札（味方の拠点・中間拠点・敵の拠点）の文字の大きさ（第479期 §5・引いた視点ほど大きく・21〜28）。</summary>
    internal int BasePlateFont3D() => Mathf.Clamp(Mathf.RoundToInt(21f * Mathf.Max(1f, _cam3d?.Zoom ?? 1f) * 1.02f), 21, 28);

    /// <summary>今の窓の層を画面の高さに合わせて拡げる（高さ 900 の作りを保つ）。</summary>
    private void FitOldUi()
    {
        Vector2 view = GetViewport().GetVisibleRect().Size;
        float scale = Math.Max(0.5f, view.Y / 900f);
        if (Mathf.IsEqualApprox(scale, _uiScale) && _uiRoot.Size.IsEqualApprox(view / scale)) return;
        _uiScale = scale;
        ((CanvasLayer)_uiRoot.GetParent()).Scale = Vector2.One * scale;
        _uiRoot.Size = view / scale;
    }

    // =================================================================================
    // 盤の状態を写す（`Refresh` の最後に 1 度）
    // =================================================================================

    private void RefreshMap3D()
    {
        _map3d?.SetGateOpen(St.Stage.PortalGate <= 0 || St.GateOpen || St.Captured);   // 第477期: 砦の門の扉（扉の無いステージでは何もしない）
        if (_layout3d is null || _hud3d is null) return;
        // 2D の矢印（隊 → 行き先・敵の次の 1 歩・予備隊の道）は出さない——行き先は 3D の点線、敵は名札の「拠点まで あと n」で見せる。
        foreach (Arrow a in _arrows) a.Hide();
        foreach (Arrow a in _foeArrows) a.Hide();
        foreach (Arrow a in _reserveArrows) a.Hide();

        // ---- 駒: どこに立つか ----
        var shown = _mapPicks.Where(p => p.Marker.Shown && (p.Foe is { } foe ? (!foe.Cleared || (_hunterLeaving && foe.IsHero)) && !HunterHidden(foe)   // 第480期: 勇者は登場の演出で歩み出るまで出さない・退くあいだは残す
            : !St.Squads[p.Squad].Lost && !St.IsLocked(p.Squad) && ((MapUnitBadge)p.Button).Count > 0)).ToList();
        var squadsOn = shown.Where(p => p.Foe is null).GroupBy(p => (St.Squads[p.Squad].Pos, Via: St.Squads[p.Squad].Pos == St.Graph.Portal ? St.Squads[p.Squad].Via : -1))
            .ToDictionary(g => g.Key, g => g.Select(p => p.Squad).ToList());
        var foesOn = shown.Where(p => p.Foe is not null).GroupBy(p => p.Foe!.Pos).ToDictionary(g => g.Key, g => g.Select(p => p.Foe!).ToList());
        var live = new HashSet<Map3DPiece>();
        foreach (var pick in shown)
        {
            if (!_pieces3d.TryGetValue(pick.Marker, out Map3DPiece? piece))
                _pieces3d[pick.Marker] = piece = Map3DPiece.Create(_map3d!, _hud3d.Plates, () => _camera);
            live.Add(piece);
            int cell;
            Vector3 at;
            if (pick.Foe is { } foe)
            {
                cell = foe.Pos;
                List<Map11State.Node> group = foesOn[cell];
                float rank = group.IndexOf(foe) - (group.Count - 1) / 2f;
                if (cell == St.Graph.Home) at = BasePos + new Vector3(3.4f, 0, LaneSide(Math.Max(0, foe.Def.Road)) * 2.6f + rank * 2.4f);
                else if (cell == St.Graph.Portal) at = PortalPos + new Vector3(0.2f, 0, rank * 2.7f) + (HunterBoard && foe.IsHero ? new Vector3(-3.4f, 0, 0) : Vector3.Zero);   // 第480期: 勇者は門の前（守りの手前）に立つ
                else
                {
                    Vector3 forward = _layout3d.Forward(cell), side = forward.Cross(Vector3.Up);
                    at = PosOf(cell) + forward * (squadsOn.ContainsKey((cell, -1)) ? 1.35f : 0) + side * rank * 2.4f;
                }
                // 第476期 §2-1: 区画が自分の賞金を持つ盤では「賞金 ＋n」と出す（関だけ明るい金）。ほかの盤は今までどおり名前に「＋n」。
                bool bounty = St.Stage.Gold.On && foe.Def.Gold >= 0 && !foe.IsHero && St.KillGold(foe) > 0;
                piece.Show(new Map3DPieceInfo(bounty ? FoeBadgeHead(foe) : FoeBadgeName(foe), Representative(foe)?.Id, true, foe.IsHero,
                    ((MapUnitBadge)pick.Button).Count, NodeHpPercent(foe), pick.Marker.Note, pick.Marker.NoteColor,
                    pick.Marker.Note.Length > 0 && pick.Marker.NoteColor == UiKit.Hurt, WaveMark(foe.Def.Name),
                    bounty ? $"賞金 ＋{St.KillGold(foe)}" : "", bounty && foe.Def.Tag.Length > 0, Crown: foe.Def.Strong,
                    Trait: foe.IsHero ? "" : Map11Lines.FoeTraitOf(St.PeekFoe(foe).Where(u => u.IsAlive).Select(u => u.Def))));
            }
            else
            {
                Map11State.Squad s = St.Squads[pick.Squad];
                cell = s.Pos;
                List<int> group = squadsOn[(cell, cell == St.Graph.Portal ? s.Via : -1)];
                float rank = group.IndexOf(pick.Squad) - (group.Count - 1) / 2f;
                if (cell == St.Graph.Home)
                    at = _layout3d.Sheet is { HomeSlots: var slots } && pick.Squad < slots.Length
                        ? slots[pick.Squad] : BasePos + new Vector3(-0.4f + pick.Squad % 2 * 2.6f, 0, (pick.Squad - (St.Squads.Length - 1) / 2f) * 2.8f);
                else if (cell == St.Graph.Portal)
                {
                    Vector3 gate = _layout3d.Approach[Math.Clamp(s.Via, 0, _layout3d.Approach.Length - 1)];
                    Vector3 side = (PortalPos - gate).Normalized().Cross(Vector3.Up);
                    at = gate + side * rank * 2.4f;
                }
                else
                {
                    Vector3 forward = _layout3d.Forward(cell), side = forward.Cross(Vector3.Up);
                    at = PosOf(cell) - forward * (foesOn.ContainsKey(cell) ? 1.35f : 0) + side * rank * 2.4f;
                }
                UnitDef? lead = Representative(s);
                piece.Show(new Map3DPieceInfo(s.Label, lead?.Id, false, false, ((MapUnitBadge)pick.Button).Count, SquadHpPercent(s),
                    "", GoldColor, false, s.Label.Length > 0 ? s.Label[..1] : "隊"));
            }
            // ---- 動き: 隣のマスへは道に沿って歩く。最初の引き直し・現れた駒・ワープは滑らせない。 ----
            bool known = _pieceCell.TryGetValue(piece, out int from);
            // 第480期 §3-3: 湧いた部隊・出発した予備隊は、敵の拠点の中から歩いて出てくる（短い音・視点か端の印・時計は止めない）。
            if (!_firstRefresh && !known && !_linesOff && pick.Foe is { FromPortal: true, IsHero: false } born && born.Pos == St.Graph.Portal && _layout3d.Sheet is { } bornSheet)
            {
                piece.Snap(bornSheet.Camp + new Vector3(-1.5f, 0, 0));
                piece.Walk(new[] { at }, 0.9f);
                FoeEmerged3D(at, born.IsReserve ? "予備隊" : born.Def.Name);
            }
            else if (_firstRefresh || !known) piece.Snap(at);
            else if (from != cell) piece.Walk(RoadBetween(from, cell, at));
            else piece.Walk(new[] { at });
            _pieceCell[piece] = cell;
        }
        foreach (var (marker, piece) in _pieces3d)
            if (!live.Contains(piece)) { piece.Hide(); _pieceCell.Remove(piece); }
        RefreshSelection3D();

        // ---- マスの台の色（味方 ／ 敵 ／ 空） ----
        foreach (var (cell, mark) in _cellMarks3d)
        {
            bool ally = squadsOn.Keys.Any(k => k.Pos == cell), enemy = foesOn.ContainsKey(cell);
            if (cell == St.Graph.Home) { ally = !St.HomeLost; enemy = St.HomeLost; }
            if (cell == St.Graph.Portal) { ally = St.Captured; enemy = !St.Captured; }
            mark.SetOwner(ally ? Map3DCellMark.Owner.Ally : enemy ? Map3DCellMark.Owner.Enemy : Map3DCellMark.Owner.Empty);
        }
        // 第476期: 本陣の手前の小さな台は見せない（マスに見えるので）。位置だけ使う。
        foreach (Map3DCellMark approach in _approachMarks3d) approach.Visible = false;

        RefreshHud3D();
    }

    /// <summary>
    /// マス <paramref name="from"/> から隣のマス <paramref name="to"/> へ歩く道（最後は立ち位置 <paramref name="stand"/>）。
    /// 道の端はマスの真ん中なので、立ち位置より先へ行き過ぎる分は落とす。
    /// </summary>
    private List<Vector3> RoadBetween(int from, int to, Vector3 stand)
    {
        var path = _layout3d!.Walk(from, to).ToList();
        Vector3 goal = PosOf(to);
        float keep = stand.DistanceTo(goal) + 0.4f;
        while (path.Count > 1 && path[^1].DistanceTo(goal) < keep) path.RemoveAt(path.Count - 1);
        path.Add(stand);
        return path;
    }

    private void RefreshSelection3D()
    {
        bool choosing = _guideSquadChosen && !_reform && !St.Finished;
        foreach (var pick in _mapPicks)
            if (pick.Foe is null && _pieces3d.TryGetValue(pick.Marker, out Map3DPiece? piece))
                piece.Selected = choosing && pick.Squad == _selected;
    }

    /// <summary>四隅の UI に中身を渡す（文は今の画面と同じものを読む）。</summary>
    private void RefreshHud3D()
    {
        if (_hud3d is null) return;
        int left = St.Nodes.Sum(r => r.Count(x => !x.Cleared)) + St.Spawns.Count(x => !x.Cleared) + (St.ReserveWaiting ? 1 : 0);
        string place = _layout3d?.Sheet?.Name is { Length: > 0 } name ? $"{St.Stage.Id}　{name}" : St.Stage.Title;
        // 第474期: 敵の倍率（「敵 ×0.6」）は題字に出さない（検証の層 F3 が出す）。
        string title = (Chapter1.Active && !place.StartsWith("1 章", StringComparison.Ordinal) ? "1 章　" : "") + place;
        string turn = !St.Time.On ? ""
            : $"作戦ターン {Map11Session.ShownTurn}" + (St.TimeLimitLeft is { } limit ? $"　／　期限まで あと {limit}" : "")
              // 試遊（2026-11-15）: 「敵部隊」「戦闘の記録」の窓を開くと敵の拠点の名札（湧くまで あと n）が隠れる——左上にも出す。
              + (St.Portal.On && St.Portal.SpawnEvery > 0 && !St.Captured && !St.Fallen ? $"　／　湧くまで あと {St.PortalCooldown}" : "");
        // 第480期: 狩る者の盤。撤退の局面（登場の演出が目標を替える所まで進んだら）は赤い「生きて拠点へ戻れ」。
        bool rout = HunterRoutShown;
        _hud3d.SetGoalAlert(rout && !St.Finished);
        string goal = rout ? (St.Finished ? (St.HunterDriven ? $"目標　{HunterGoneText}" : St.Won ? "目標　生きて戻った" : Map11Lines.GoalRout) : Map11Lines.GoalRout)
            : HunterBoard ? (St.GateOpen ? "目標　野営地を制圧する" : $"目標　中間拠点を取る（{St.HeldOutposts.Count}／{St.Stage.PortalGate}）")
            : St.HeroClock && St.Hero is { Cleared: false } hero ? $"目標　勇者を倒す（HP {hero.HpNow}）"
            : !St.Portal.On ? $"目標　敵部隊をすべて抜く（{St.ClearedCount} / {St.Stage.TotalNodes}）"
            : St.Won ? $"目標　{Map11.PortalName}を制圧した"   // 第476期: 制圧で勝つ盤で、勝った後に「残る敵部隊を倒す」と出ていた
            : St.Captured ? $"目標　残る敵部隊を倒す（あと {left}）"
            // 第479期: 門のある盤は、門が開くまで「中間拠点を取る」・開いたら「砦を制圧」（窓は出さない——目標の札が変わる）。
            : St.Stage.PortalGate > 0 && Chapter1.Active && Chapter1.Rule.Lean ? (St.GateOpen ? "目標　砦を制圧する" : $"目標　中間拠点を取る（{St.HeldOutposts.Count}／{St.Stage.PortalGate}）")
            : $"目標　{Map11.PortalName}を制圧する　・　残る敵部隊 {left}";
        _hud3d.SetHeader(title, turn, goal);
        // 第477期 §3-a: 金を使える場所（拠点・取った中間拠点）に隊がいて、倒れた駒を呼び戻せるか金が 8 以上あるとき、隊の札と金の札に印（新しい 1-2 の盤だけ）。
        bool teach = St.Stage.OutpostRewards is not null && St.Stage.Gold.On && !St.Finished;
        bool Spendable(Map11State.Squad q) => teach && !q.Lost && q.Units is { Count: > 0 } && St.CanReform(q.Index)
            && ((St.Stage.Gold.Revive > 0 && St.FallenUnits.Count > 0 && St.Gold >= St.Stage.Gold.Revive) || St.Gold >= 8);
        // 第479期: 金の札は光るだけ（金が 20 以上・使える場所に隊がいる）。文は足さない。
        bool idleGold = teach && St.Gold >= Map11Lines.GoldLine && St.Squads.Any(q => CardActions(q) is { Revive: true } or { Enhance: true });
        _hud3d.SetGold(St.Stage.Gold.On ? St.Gold : null, St.Squads.Any(Spendable), idleGold);
        _hud3d.SetWindows(_showFoes, _showHistory);

        // 隊の札は使える隊だけ。まだ開けていない隊は「＋ 隊の枠」のボタン 1 つに畳む（第474期）。
        var cards = new List<Map3DSquadCardInfo>();
        foreach (Map11State.Squad s in St.Squads)
        {
            if (St.IsLocked(s.Index)) continue;
            var faces = s.Lost ? new List<UnitDef>()
                : s.Units is { } units ? units.Where(u => u.IsAlive).OrderBy(u => u.Slot).Select(u => u.Def).ToList()
                : s.Def.F.Occupied().OrderBy(u => u.Slot).Select(u => u.Def).ToList();
            string where = s.Pos == St.Graph.Home ? "拠点" : s.Pos == St.Graph.Portal ? Map11.PortalName : PlaceName(s.Pos);
            // 第479期: 金の使いどころは札の上の小さなボタン（文の印「◆ 金を使える」はやめた）。失われた隊は 1 行「全滅　砦の守将 残り 1」。
            var act = CardActions(s);
            // 第480期: 撤退の局面では、拠点の外にいる隊の札に足止めの印（勇者に当てる前に読める）。拠点に入った隊は「生還」。
            string tag = "", tagColor = "";
            if (HunterRoutShown && !s.Lost && faces.Count > 0 && !St.Finished)
            {
                if (s.Pos == St.Graph.Home) (tag, tagColor) = ("生還", "#71d7a1");
                else if (HoldMark(s.Index) is { } held and >= 0) (tag, tagColor) = HoldOf(held);
            }
            // 第481期 §4-4: 殿に指定した隊（「殿に出す」を押した ／ 殿を務めた）の札に「殿」の印を残す（戦闘の後も・倒れても）。
            bool rearSquad = HunterBoard && St.Rout && (Map11Session.IsRear(s.Index) || St.RearGuards.Contains(s.Index));
            if (rearSquad && !(s.Pos == St.Graph.Home && faces.Count > 0)) (tag, tagColor) = (tag.Length > 0 ? $"{Map11Lines.RearTag}・{tag}" : Map11Lines.RearTag, tagColor.Length > 0 ? tagColor : "#f1c96b");
            // 追記 2: 殿が全滅した隊の札は、案内を「殿・足を止めた」／「殿・止められなかった」に（「駒を配る」の代わり・このステージのあいだ）。
            string rearDone = rearSquad && faces.Count == 0 && Hs.RearHeld.TryGetValue(s.Index, out bool heldIt) ? (heldIt ? Map11Lines.RearCardHeld : Map11Lines.RearCardFail) : "";
            cards.Add(new Map3DSquadCardInfo(s.Index, s.Label, faces, faces.Count, faces.Count == 0 ? 0 : SquadHpPercent(s),
                rearDone.Length > 0 ? rearDone : St.Time.On ? "→ " + OrderText(s.Index) : where, s.Index == _selected && _guideSquadChosen, s.Lost,
                act.Revive, act.Enhance, act.Resting, s.Lost ? (rearDone.Length > 0 ? rearDone : (rearSquad ? $"{Map11Lines.RearTag}　" : "") + LostLine(s)) : "", tag, tagColor));
        }
        _hud3d.SetSquads(cards);
        bool can = _selected >= 0 && _selected < St.Squads.Length && St.CanReform(_selected) && !St.Finished && !Map11Session.TurnRunning;
        int locked = Enumerable.Range(0, St.Squads.Length).FirstOrDefault(St.IsLocked, -1);
        _hud3d.SetReform("編成・呼ぶ・強化", can, _showSquadDetail ? "配置を閉じる" : "配置を見る",
            locked < 0 ? "" : St.Stage.Gold.On ? $"＋ 隊の枠（金 {St.SlotPriceFor(locked)}）" : "＋ 隊の枠");
    }

    // =================================================================================
    // 毎フレーム（位置を追うだけ）
    // =================================================================================

    private void ProcessMap3D(double delta)
    {
        if (_hud3d is null || _cam3d is null || _layout3d is null) return;
        _clock3d += (float)delta;
        FitOldUi();
        _hud3d.PulseGold((float)delta);
        // 今の画面の上の帯・見出し・出来事の 1 行は、今の窓（編成）を開いているあいだだけ出す。作戦の画面では四隅の UI が持つ。
        if (_headerBand is not null) _headerBand.Visible = _reform;
        _headline.Visible = _toast.Visible = _reform;
        if (_portalInfo is not null) _portalInfo.Visible = false;
        _hud3d.SetShown(!_reform && !StoryShown);   // 第481期: 会話の画面のあいだは四隅の UI も隠す
        ProcessClash3D((float)delta);   // 第479期の追記: 遭遇の台（下 1／3）と盤の上の輪
        ProcessRadar3D(!_reform);   // 第477期: 画面の外の知らせ・全体図・歩く敵の赤い点線
        ProcessHunter3D((float)delta);   // 第480期: 狩る者の登場・赤い点線・目の印・「生還」・勇者の HP の棒
        _hud3d.Plates.Visible = !RelicPickShown;   // 第481期 §4-2: レリックの候補の窓が出ているあいだは、駒と拠点の名札を隠す（窓に重なっていた）
        if (_reform) return;

        // 第476期（自分で遊んで見つけた）: 行き先に触れているあいだは、出来事の 1 行より案内の 1 行（経路上の敵・「峠の関: 硬い・賞金 14」・拠点が空になる）を先に出す
        // ——編成を終えた直後の 3 秒は「編成を終えました」が案内を隠していた。
        bool reading = _hoverDestination is { Node.Disabled: false } && _guideSquadChosen && !St.Finished && !Map11Session.TurnRunning;
        _hud3d.SetToast(reading ? "" : ShortToast(_toast.Text), _toast.GetThemeColor("font_color"));   // 第479期: 出来事の 1 行は 20 字まで
        bool on = Map11Clock.On;
        string state = Map11Session.TurnRunning ? "戦闘中" : !on ? "出来事が起きるまで進む" : Map11Clock.Running ? "▶ 動いています" : "⏸ 止まっています";
        _hud3d.SetClock(on && Map11Clock.Running ? "⏸ 止める" : "▶ 刻を進める", _run.Disabled, on && St.Time.On, Map11Clock.Fast,
            Map11Clock.Fraction, on ? $"速さ {(Map11Clock.Fast ? "2×" : "1×")}　・　{state}" : state);
        RefreshSelection3D();

        // ---- 行ける先のマスを光らせ、「あと n」を出す（行けるかどうかは今の画面の行き先の札が押せるかだけを読む） ----
        Map11State.Squad sel = St.Squads[_selected];
        bool chosen = _guideSquadChosen && !sel.Lost && !St.Finished;
        Dest now = Map11Session.Destinations[_selected];
        // 第476期（不具合 4）: 窓（遭遇・迎撃・結果・確かめ・連戦・頭の札）が出ているあいだは「あと n」を出さない（窓の上に透けて出ていた）。
        // 第479期: 遭遇・迎撃・連戦は横の札になった（盤は見えたまま）——行き先の札・道の点線は出さないが、名札は出しておく。
        bool modal = CoverShown || SideShown;
        var sideRects = SideRects().ToList();
        // 第479期: 拠点が空になる行き先（触れているマスか、今の行き先）なら、拠点の名札を赤く点滅させ、歩いてくる敵の点線を太くする（文は出さない）。
        _homeWarn3d = !St.Finished && _guideSquadChosen && !sel.Lost && St.CanSend(_selected)
                      && EmptiesHome(_selected, _hoverDestination is { Node.Disabled: false } hd ? hd.Dest : now);
        // 第476期（不具合 1・2）: 敵の拠点へは<b>最短の道の 1 つだけ</b>を、本陣の大きな台に出す（今の行き先が敵の拠点ならそれ）。
        // 道の途中の小さな丸（経路ごとの入口）はマスに見えないよう隠す——回り道で入りたいときは、その道のマスを先に選ぶ。
        DestButton? portalPick = null;
        int portalSteps = int.MaxValue;
        foreach (DestButton d in _destButtons)
        {
            if (!d.Dest.IsPortal) continue;
            if (now == d.Dest && !St.AtDest(_selected, d.Dest)) { portalPick = d; break; }
            int steps = TurnsToDest(_selected, d.Dest) ?? (St.AtDest(_selected, d.Dest) ? 0 : int.MaxValue);
            if (d.Node.Visible && !d.Node.Disabled && steps < portalSteps) { portalPick = d; portalSteps = steps; }
        }
        foreach (Map3DCellMark approach in _approachMarks3d) { approach.Visible = false; approach.SetReach(false, false); }
        bool portalLit = false;
        foreach (DestButton d in _destButtons)
        {
            if (d.Dest.IsPortal && !ReferenceEquals(d, portalPick))
            {
                // 最短でない入口は押す領域ごと畳む。
                d.Node.Position = new Vector2(-400, -400);
                d.Node.Size = Vector2.Zero;
                if (_chips3d.TryGetValue(d, out Label? idle)) idle.Visible = false;
                continue;
            }
            Map3DCellMark mark = d.Dest.IsPortal ? _cellMarks3d[St.Graph.Portal] : _cellMarks3d[d.Dest.Id];
            bool there = St.AtDest(_selected, d.Dest);
            bool reach = chosen && d.Node.Visible && !d.Node.Disabled && !there;
            bool goal = chosen && now == d.Dest && !there && !sel.Lost;
            mark.SetReach(reach, goal);
            if (d.Dest.IsPortal) portalLit = true;
            // 押す領域（今の窓の層の座標）。
            Vector2 center = _camera.UnprojectPosition(mark.GlobalPosition);
            float half = Math.Max(36, _camera.UnprojectPosition(mark.GlobalPosition + new Vector3(mark.Radius * 1.08f, 0, 0)).X - center.X);
            var region = new Rect2(center.X - half, center.Y - half * 0.66f, half * 2, half * 1.32f);
            // 第478期: 遊ぶ UI（隊の札・「刻を進める」・金の札…）の下に入ったマスの押す領域は、UI に掛からない所まで削る。
            // 見えないこの領域は今の窓の層にあって遊ぶ UI より手前なので、視点を動かして「1 刻」の上にマスが来ると、押しても刻が進まず行き先が替わっていた
            // （遊ぶ口の方針 3 が 5 刻目で止まった原因）。真ん中が UI の下なら押せない。
            bool covered = false;
            foreach (Rect2 block in _hud3d.Blocks())
            {
                if (block.HasPoint(center)) { covered = true; break; }
                if (!block.Intersects(region)) continue;
                if (block.Position.Y >= center.Y) region = new Rect2(region.Position, new Vector2(region.Size.X, Math.Max(0, block.Position.Y - region.Position.Y)));
                else if (block.End.Y <= center.Y) region = new Rect2(region.Position.X, block.End.Y, region.Size.X, Math.Max(0, region.End.Y - block.End.Y));
                else if (block.Position.X >= center.X) region = new Rect2(region.Position, new Vector2(Math.Max(0, block.Position.X - region.Position.X), region.Size.Y));
                else region = new Rect2(block.End.X, region.Position.Y, Math.Max(0, region.End.X - block.End.X), region.Size.Y);
            }
            d.Node.MouseFilter = covered ? Control.MouseFilterEnum.Ignore : Control.MouseFilterEnum.Stop;
            d.Node.Position = region.Position / _uiScale;
            d.Node.Size = region.Size / _uiScale;
            // 「あと n」の札（選んだ隊がそこまで歩く作戦ターン・今の 2D と同じ数）。
            if (!_chips3d.TryGetValue(d, out Label? chip))
            {
                _chips3d[d] = chip = Map3DHud.Text("", 22, Colors.White, bold: true);
                chip.HorizontalAlignment = HorizontalAlignment.Center;
                _hud3d.Plates.AddChild(chip);
            }
            // 第476期（見せ方 7）: 「あと n」は<b>次の 1 歩と今の行き先だけ</b>。ほかの行ける先は、マスに触れているあいだだけ（名前を添えて）。
            int? turns = TurnsToDest(_selected, d.Dest);
            bool hover = ReferenceEquals(_hoverDestination, d);
            chip.Visible = !modal && (goal || (reach && (hover || turns == 1)));
            if (!chip.Visible) continue;
            string eta = turns is { } steps ? $"あと {steps}" : "";
            string where = d.Dest.AtHome ? "拠点" : d.Dest.IsPortal ? $"{Map11.PortalName}（{St.Stage.LaneNames[d.Dest.Road]}から）" : PlaceName(d.Dest.Id);
            string text = goal ? (eta.Length > 0 ? $"◆ 行き先　{eta}" : "◆ 行き先") : hover ? $"{where}　{eta}" : eta.Length > 0 ? eta : where;
            if (chip.Text != text || chip.HasMeta("goal") != goal)
            {
                chip.Text = text;
                if (goal) chip.SetMeta("goal", true); else chip.RemoveMeta("goal");
                chip.AddThemeColorOverride("font_color", goal ? Color.FromHtml("#1b1608") : Color.FromHtml("#ffe9a8"));
                chip.AddThemeStyleboxOverride("normal", goal
                    ? Map3DHud.Box(Color.FromHtml("#f1c96b"), Color.FromHtml("#fff1c4"), 2, 16, 3)
                    : Map3DHud.Box(new Color(0.07f, 0.06f, 0.03f, 0.86f), Color.FromHtml("#f1c96b"), 2, 16, 3));
                chip.ResetSize();
            }
            _chipAt3d[chip] = new Rect2(center.X - half, center.Y - half * 0.66f, half * 2, half * 1.32f);
        }
        if (!portalLit && St.Portal.On && _cellMarks3d.TryGetValue(St.Graph.Portal, out Map3DCellMark? camp)) camp.SetReach(false, false);
        ProcessRoute3D(chosen && !modal, sel);
        // 第480期: 撤退の局面では味方の拠点が光る（「生きて拠点へ戻れ」の行き先）。
        if (HunterRoutShown && !St.Finished && _cellMarks3d.TryGetValue(St.Graph.Home, out Map3DCellMark? safe)) safe.SetReach(true, Mathf.PosMod(_clock3d, 1.0f) < 0.6f);

        // ---- 建物の名札（拠点 ／ 敵の拠点）。文は今の画面の札と同じ。 ----
        Map3DLayout.Table? sheet = _layout3d.Sheet;
        // 引き出し（今の窓）が開いている側の名札は隠す（窓の下に半分だけ見えるのを避ける）。
        if (_homePlate3d is not null) _homePlate3d.Visible = !_showSquadDetail;
        if (_portalPlate3d is not null) _portalPlate3d.Visible = !_showFoes && !_showHistory && !(HunterBoard && St.Rout && Hs.Stepped);   // 第480期: 撤退の局面では敵の拠点の札は消える
        if (_homePlate3d is not null)
        {
            string head = sheet?.HomeName is { Length: > 0 } hn ? hn + "\n" : "";
            bool blink = _homeWarn3d && Mathf.PosMod(_clock3d, 0.7f) < 0.35f;
            _homePlate3d.SelfModulate = blink ? new Color(1.9f, 0.55f, 0.5f) : Colors.White;
            SetPlate(_homePlate3d, head + _homeLabel.Text, St.HomeLost || _homeWarn3d ? UiKit.Hurt : PlayerColor,
                (sheet?.Fort ?? BasePos) + new Vector3(0, 9.8f, 0));
        }
        if (_portalPlate3d is not null && _portalMarker is not null)
        {
            string head = sheet?.PortalName is { Length: > 0 } pn ? pn + "\n" : "";
            string note = _portalMarker.Note.Length > 0 ? "\n" + _portalMarker.Note : "";
            SetPlate(_portalPlate3d, head + _portalMarker.Caption + note, St.Captured ? PlayerColor : EnemyColor,
                (sheet?.Camp ?? PortalPos) + new Vector3(0, 8.4f, 0));
        }

        // 第479期の続き: 拠点の名札は、上の固定の UI（題字・金・ボタン）の下に入れない——下へ逃がし、逃がせなければ出さない。
        if (_homePlate3d is { Visible: true } homeDodge) DodgePlate(homeDodge);
        if (_portalPlate3d is { Visible: true } portalDodge) DodgePlate(portalDodge);
        // 第479期: 横の札の下に入る名札は出さない。
        if (_portalPlate3d is { Visible: true } pp && sideRects.Any(r => r.Intersects(pp.GetGlobalRect()))) pp.Visible = false;
        if (_homePlate3d is { Visible: true } hp && sideRects.Any(r => r.Intersects(hp.GetGlobalRect()))) hp.Visible = false;
        // 第479期 §5: 味方の拠点 ＝ 青緑 ／ 敵の拠点 ＝ 赤（制圧したら青緑）。引いた視点では拠点の名札を大きくする。
        if (_cellMarks3d.TryGetValue(St.Graph.Home, out Map3DCellMark? homeMark)) homeMark.SetBanner(St.HomeLost ? Map3DCellMark.Owner.Enemy : Map3DCellMark.Owner.Ally);
        if (St.Portal.On && _cellMarks3d.TryGetValue(St.Graph.Portal, out Map3DCellMark? portalMark)) portalMark.SetBanner(St.Captured ? Map3DCellMark.Owner.Ally : Map3DCellMark.Owner.Enemy);
        int plateFont = BasePlateFont3D();
        foreach (Label? big in new[] { _homePlate3d, _portalPlate3d }.Concat(_outpostPlates3d.Values))
            if (big is not null && big.GetThemeFontSize("font_size") != plateFont) { big.AddThemeFontSizeOverride("font_size", plateFont); big.ResetSize(); }
        foreach (var (cell, plate) in _outpostPlates3d)
        {
            bool mine = St.HeldOutposts.Contains(cell);
            if (_cellMarks3d.TryGetValue(cell, out Map3DCellMark? flag)) flag.SetFlag(mine);
            plate.Visible = !CoverShown;
            string text = _outpostMarks.First(m => m.Cell == cell).Name.Text;
            SetPlate(plate, "◆ " + text, mine ? PlayerColor : Color.FromHtml("#9aa0a6"), PosOf(cell) + new Vector3(0, 6.6f, 0));   // 第479期: 取っていなければ灰
            // 遊ぶ UI（隊の札・刻を進める）の下に入った名札は出さない（失われた隊の薄い札の下に透けて見えた）。
            if (plate.Visible) DodgePlate(plate);
            if (plate.Visible && sideRects.Any(r => r.Intersects(plate.GetGlobalRect()))) plate.Visible = false;
        }
        ProcessLines3D();   // 第479期: 出来事の窓（第478期）はやめた——説明は顔つきの 1 行で

        // ---- いま何をすればよいかの 1 行（左下・隊の札の上） ----
        string hint = St.Finished || Map11Session.TurnRunning ? ""
            : !_guideSquadChosen ? "隊の札か、盤の上の駒を押して隊を選ぶ"
            : !St.CanSend(_selected) ? (St.CanReform(_selected) ? "空席——編成で駒を配る" : "")
            : reading ? _orderHint.Text
            : _hoverDestination is null && !_guideDestinationChosen ? "光っているマスを押すと行き先が決まる"
            : _orderHint.Text;
        _hud3d.SetHint(hint);
        PlaceLabels3D();
    }

    // =================================================================================
    // 名札と「あと n」の札を置く（第474期 直し 7: 台・橋・旗・ほかの札・遊ぶ UI と重ねない）
    // =================================================================================

    /// <summary>「あと n」の札 → そのマスの台の画面の四角（このフレームに出ている札だけ）。</summary>
    private readonly Dictionary<Label, Rect2> _chipAt3d = new();
    /// <summary>このフレームに置いた物の四角（3D のレイアウトの検査が読む）。</summary>
    private readonly List<(string Name, Rect2 Rect)> _plateRects3d = new(), _chipRects3d = new(), _bodyRects3d = new(), _discRects3d = new(),
        _bridgeRects3d = new(), _hudRects3d = new(), _signRects3d = new();

    private Rect2 ScreenBox(Vector3 centre, Vector3 halfX, Vector3 halfZ, float up = 0)
    {
        Rect2? box = null;
        foreach (float a in new[] { -1f, 1f })
            foreach (float b in new[] { -1f, 1f })
                foreach (float y in up > 0 ? new[] { 0f, up } : new[] { 0f })
                {
                    Vector2 q = _camera.UnprojectPosition(centre + halfX * a + halfZ * b + Vector3.Up * y);
                    box = box is { } r ? r.Expand(q) : new Rect2(q, Vector2.Zero);
                }
        return box ?? default;
    }

    private static float Overlap(Rect2 a, Rect2 b)
    {
        Rect2 c = a.Grow(-1).Intersection(b);
        return c.Size.X > 0 && c.Size.Y > 0 ? c.Size.X * c.Size.Y : 0;
    }

    /// <summary>
    /// 名札と「あと n」の札の置き場所を決める。候補（真下 → 右 → 左 → 頭の上 …）を順に見て、重なりが 0 の最初の所へ置く
    /// （どこも重なるなら、重なりのいちばん小さい所）。<b>見た目だけ</b>——盤は読まない。
    /// </summary>
    /// <summary>名札の置き場が見つからなかった駒を 1 行ずつ出す（3D のレイアウトの検査のあいだだけ）。</summary>
    private static readonly bool _placeTrace = OS.GetCmdlineUserArgs().Contains("--map3d-place-trace");

    private void PlaceLabels3D()
    {
        if (_hud3d is null || _map3d is null) return;
        Vector2 view = GetViewport().GetVisibleRect().Size;
        foreach (var list in new[] { _plateRects3d, _chipRects3d, _bodyRects3d, _discRects3d, _bridgeRects3d, _hudRects3d, _signRects3d }) list.Clear();
        foreach (var (cell, mark) in _cellMarks3d)
            _discRects3d.Add(($"台:{PlaceName(cell)}", ScreenBox(mark.GlobalPosition, Vector3.Right * mark.Radius * 1.06f, Vector3.Back * mark.Radius * 1.06f)));
        foreach (var (at, along) in _map3d.Bridges)
            _bridgeRects3d.Add(("橋", ScreenBox(at + Vector3.Up * 0.2f, along * 2.7f, along.Cross(Vector3.Up) * 1.25f, 0.6f)));
        foreach (Rect2 block in _hud3d.Blocks()) _hudRects3d.Add(("遊ぶ UI", block));
        foreach (Rect2 side in SideRects()) _hudRects3d.Add(("横の札", side));   // 第479期: 名札は横の札（遭遇・連戦・迎撃）を避ける
        if (_homePlate3d is { Visible: true }) _signRects3d.Add(("名札:拠点", _homePlate3d.GetGlobalRect()));
        if (_portalPlate3d is { Visible: true }) _signRects3d.Add(("名札:敵の拠点", _portalPlate3d.GetGlobalRect()));
        foreach (var (cell, plate) in _outpostPlates3d)
            if (plate.Visible) _signRects3d.Add(($"名札:{PlaceName(cell)}", plate.GetGlobalRect()));
        var pieces = _mapPicks.Where(p => _pieces3d.TryGetValue(p.Marker, out Map3DPiece? piece) && piece.Shown)
            .Select(p => (Piece: _pieces3d[p.Marker], Name: p.Foe is { } foe ? foe.Def.Name : St.Squads[p.Squad].Label))
            .Select(p => (p.Piece, p.Name, Body: p.Piece.ScreenRect(_camera))).OrderBy(p => p.Body.End.Y).ThenBy(p => p.Body.Position.X).ToList();
        foreach (var p in pieces) _bodyRects3d.Add(($"駒:{p.Name}", p.Body));

        float Cost(Rect2 r, bool plate)
        {
            float cost = 0;
            foreach (var (_, o) in _hudRects3d) cost += Overlap(r, o) * 4;
            foreach (var (_, o) in _signRects3d) cost += Overlap(r, o) * 2;
            foreach (var (_, o) in _bodyRects3d) cost += Overlap(r, o) * 2;
            foreach (var (_, o) in _plateRects3d) cost += Overlap(r, o) * 2;
            foreach (var (_, o) in _chipRects3d) cost += Overlap(r, o) * 2;
            if (plate)
            {
                foreach (var (_, o) in _discRects3d) cost += Overlap(r, o.Grow(3)) * 2;   // 台は光るときに 1〜2px 脈を打つ——3px 離して置く（縁を接した札が、次のフレームで「重なる」になっていた）   // 第481期 追記 2: 台との重なりも、ほかの札との重なりと同じ重さで避ける
                foreach (var (_, o) in _bridgeRects3d) cost += Overlap(r, o);
            }
            var screen = new Rect2(8, 8, view.X - 16, view.Y - 16);
            if (!screen.Encloses(r)) cost += r.Size.X * r.Size.Y * 6;
            return cost;
        }
        Vector2 Choose(IEnumerable<Vector2> candidates, Vector2 size, bool plate)
        {
            Vector2 best = default;
            float least = float.MaxValue;
            foreach (Vector2 at in candidates)
            {
                float cost = Cost(new Rect2(at.Round(), size), plate);   // 置くのは丸めた場所（検査も丸めた四角で見る——丸めで 0.5px 食い込むと、縁を接した札が「重なる」に数えられていた）
                if (cost < least) { least = cost; best = at; }
                if (cost <= 0) break;
            }
            return best;
        }

        var hudBlocks = _hud3d!.Blocks().ToList();
        // ---- 駒の名札: 台のすぐ下 → 右 → 左 → 頭の上 → さらに下 ----
        foreach (var p in pieces)
        {
            Vector2 size = p.Piece.Plate.Size;
            Rect2 body = p.Body;
            // 第479期の続き 2（ポンの写し 2026-11-17・峠の関）: 駒そのものが固定の UI（題字・金・ボタン）の下にいるなら、名札は出さない
            // ——横へ逃がした札だけが UI の隣に残って、何の札か分からなくなっていた。遭遇の台は数えない（台の縁に掛かる駒の札は残す）。
            if (hudBlocks.Any(b => b.Intersects(body))) { p.Piece.Plate.Visible = false; continue; }
            float cx = body.GetCenter().X;
            // 駒が載っている台（体の足元と重なる台）の下の縁まで下げる。
            float under = body.End.Y;
            foreach (var (_, disc) in _discRects3d)
                if (disc.Grow(2).HasPoint(new Vector2(cx, body.End.Y - 6))) under = Math.Max(under, disc.End.Y);
            var spots = new[]
            {
                new Vector2(cx - size.X / 2, under + 5),
                new Vector2(body.End.X + 8, body.End.Y - size.Y + 6),
                new Vector2(body.Position.X - 8 - size.X, body.End.Y - size.Y + 6),
                new Vector2(cx - size.X / 2, body.Position.Y - size.Y - 6),
                new Vector2(body.End.X + 8, body.Position.Y + 8),
                new Vector2(body.Position.X - 8 - size.X, body.Position.Y + 8),
                new Vector2(cx - size.X / 2, under + 40),
                new Vector2(cx - size.X / 2 + size.X * 0.6f, under + 5),
                new Vector2(cx - size.X / 2 - size.X * 0.6f, under + 5),
                // 第481期 追記 2: 横長の画面（2560×1080）の 1-2 で、砦の前に駒が集まると上の 9 か所が全部ふさがっていた——もう一回り外の場所を足す。
                new Vector2(cx - size.X / 2 + size.X * 1.1f, under + 5),
                new Vector2(cx - size.X / 2 - size.X * 1.1f, under + 5),
                new Vector2(body.End.X + 8 + size.X * 0.55f, body.End.Y - size.Y + 6),
                new Vector2(body.Position.X - 8 - size.X * 1.55f, body.End.Y - size.Y + 6),
                new Vector2(cx - size.X / 2, body.Position.Y - size.Y * 2 - 12),
                new Vector2(cx - size.X / 2 + size.X * 0.6f, body.Position.Y - size.Y - 6),
                new Vector2(cx - size.X / 2 - size.X * 0.6f, body.Position.Y - size.Y - 6),
                new Vector2(cx - size.X / 2, under + 76),
                new Vector2(cx - size.X / 2 + size.X * 0.6f, under + 40),
                new Vector2(cx - size.X / 2 - size.X * 0.6f, under + 40),
                new Vector2(body.End.X + 8 + size.X * 0.55f, body.Position.Y + 8),
                new Vector2(body.Position.X - 8 - size.X * 1.55f, body.Position.Y + 8),
                new Vector2(cx - size.X / 2 + size.X * 1.1f, under + 40),
                new Vector2(cx - size.X / 2 - size.X * 1.1f, under + 40),
                new Vector2(cx - size.X / 2 + size.X * 1.1f, body.Position.Y - size.Y - 6),
                new Vector2(cx - size.X / 2 - size.X * 1.1f, body.Position.Y - size.Y - 6),
            };
            // 第479期の続き: 固定の UI に重なる候補は、その UI のすぐ下へ逃がした場所も候補に足す（ほかの札との重なりも一緒に見て選ぶ）。
            // それでも固定の UI に重なるなら出さない（駒そのものが UI の下にいる）。
            Vector2 at = Choose(spots.Concat(spots.Select(q => Dodge(new Rect2(q, size))).Where(r => r is not null).Select(r => r!.Value.Position)), size, plate: true);
            // 第481期 追記 2: 決まった候補がどれも重なるとき（横長の画面の 1-2・砦の前に駒と台が集まる所）は、駒のまわりを近い順に探す。
            if (Cost(new Rect2(at.Round(), size), true) > 0)
            {
                Vector2 mid = body.GetCenter() - size / 2;
                var ring = new List<Vector2>();
                for (int gx = -5; gx <= 5; gx++)
                    for (int gy = -4; gy <= 5; gy++)
                        ring.Add(mid + new Vector2(gx * size.X * 0.5f, gy * size.Y * 0.6f));
                Vector2 wider = Choose(ring.OrderBy(q => q.DistanceSquaredTo(mid)), size, plate: true);
                if (Cost(new Rect2(wider.Round(), size), true) < Cost(new Rect2(at.Round(), size), true)) at = wider;
            }
            if (_placeTrace)
                GD.Print($"MAP3D_LAYOUT_PLACE {p.Name} 体 {body.Position.X:F0},{body.Position.Y:F0} {body.Size.X:F0}x{body.Size.Y:F0} 札 {size.X:F0}x{size.Y:F0} 選んだ {at.X:F0},{at.Y:F0} 重なり {Cost(new Rect2(at, size), true):F0} ／ 候補 "
                    + string.Join(" ", spots.Select(q => $"{q.X:F0},{q.Y:F0}={Cost(new Rect2(q, size), true):F0}")));
            if (Dodge(new Rect2(at, size)) is not { } free || free.Position != at) { p.Piece.Plate.Visible = false; continue; }
            p.Piece.Plate.Visible = true;
            p.Piece.Plate.Place(at, p.Piece.Clock);
            _plateRects3d.Add(($"名札:{p.Name}", new Rect2(at.Round(), size)));
        }

        // ---- 「あと n」の札: 空の台なら真ん中、駒のいる台なら台の左 → 右 → 上 ----
        foreach (var (chip, disc) in _chipAt3d)
        {
            if (!chip.Visible) continue;
            Vector2 size = chip.Size, c = disc.GetCenter();
            Vector2 at = Choose(new[]
            {
                c - size / 2 + new Vector2(0, -2),
                new Vector2(disc.Position.X - size.X - 4, c.Y - size.Y / 2),
                new Vector2(disc.End.X + 4, c.Y - size.Y / 2),
                new Vector2(disc.Position.X - size.X * 0.7f, disc.Position.Y - size.Y - 2),
                new Vector2(disc.End.X - size.X * 0.3f, disc.Position.Y - size.Y - 2),
                new Vector2(c.X - size.X / 2, disc.End.Y + 4),
            }, size, plate: false);
            chip.Position = at.Round();
            _chipRects3d.Add(($"あと n:{chip.Text}", new Rect2(at.Round(), size)));
        }
        _chipAt3d.Clear();
    }

    /// <summary>
    /// 札の四角を、固定の UI（題字・金・ボタン・隊の札・台）の外へ逃がす（重なっていれば、その UI のすぐ下へ）。逃がせなければ null。<b>見た目だけ。</b>
    /// </summary>
    private Rect2? Dodge(Rect2 r)
    {
        if (_hud3d is null) return r;
        Vector2 view = GetViewport().GetVisibleRect().Size;
        var blocks = _hud3d.Blocks().Concat(SideRects()).ToList();
        for (int pass = 0; pass < 3; pass++)
        {
            Rect2? hit = null;
            foreach (Rect2 b in blocks) if (b.Grow(3).Intersects(r)) { hit = b; break; }
            if (hit is not { } block) return r;
            r = new Rect2(new Vector2(r.Position.X, block.End.Y + 6), r.Size);
            if (r.End.Y > view.Y - 8) return null;
        }
        return blocks.Any(b => b.Grow(3).Intersects(r)) ? null : r;
    }

    private void DodgePlate(Label plate)
    {
        // 遠くへは逃がさない（札が元の建物から離れると、どの建物の札か分からなくなる・ほかの札の上に落ちる）——そのときは出さない。
        if (Dodge(plate.GetGlobalRect()) is { } free && free.Position.Y - plate.GetGlobalRect().Position.Y <= 130) plate.Position = free.Position.Round();
        else plate.Visible = false;
    }

    /// <summary>名札と固定の UI の重なりの数（レイアウトの検査と遊ぶ口が読む）。</summary>
    internal int PlatesUnderHud3D()
    {
        if (_hud3d is null) return 0;
        var blocks = _hud3d.Blocks().Concat(SideRects()).ToList();
        var plates = _plateRects3d.Select(p => p.Rect).Concat(_signRects3d.Select(p => p.Rect)).ToList();
        return plates.Count(p => blocks.Any(b => b.Intersects(p)));
    }

    private void SetPlate(Label plate, string text, Color edge, Vector3 world)
    {
        if (plate.Text != text)
        {
            plate.Text = text;
            plate.ResetSize();
        }
        plate.AddThemeStyleboxOverride("normal", Map3DHud.Box(new Color(0.05f, 0.06f, 0.08f, 0.82f), edge.Darkened(0.15f), 2, 10, 8));
        Vector2 view = GetViewport().GetVisibleRect().Size;
        Vector2 at = _camera.UnprojectPosition(world) - new Vector2(plate.Size.X / 2, plate.Size.Y);
        plate.Position = new Vector2(Mathf.Clamp(at.X, 12, view.X - plate.Size.X - 12), Mathf.Clamp(at.Y, 12, view.Y - plate.Size.Y - 12)).Round();
        // 第477期: 1 画面に入らない盤では、画面の外にある物の札を端に張り付けない（端で札どうしが重なる）——外の出来事は端の印と全体図が知らせる。
        if (_layout3d?.Pan is not null)
        {
            Vector2 raw = _camera.UnprojectPosition(world);
            if (_camera.IsPositionBehind(world) || !new Rect2(0, 0, view.X, view.Y).Grow(24).HasPoint(raw)) plate.Visible = false;
        }
    }

    /// <summary>
    /// 選んだ隊の行き先までの道を、道に沿った金色の点線で見せる（札に触れているあいだはその行き先・今の画面の経路の線と同じ道）。
    /// </summary>
    private void ProcessRoute3D(bool chosen, Map11State.Squad squad)
    {
        var points = new List<Vector3>();
        DestButton? pick = RearHover is { } rearHover ? rearHover   // 第480期 追記 4: 勇者の駒に触れているあいだは「殿に出す」の道
            : _hoverDestination is { Node.Disabled: false } hover && hover.Node.IsVisibleInTree() ? hover
            : _destButtons.FirstOrDefault(d => d.Dest == Map11Session.Destinations[_selected]);
        if (chosen && pick is not null && St.CanSend(_selected) && !St.AtDest(_selected, pick.Dest))
        {
            bool warp = St.CanWarp(_selected, pick.Dest);
            List<int> cells = warp ? new List<int> { squad.Pos, pick.Dest.Id } : St.Graph.Path(squad.Pos, squad.Via, pick.Dest.Id, pick.Dest.Lane);
            points = _layout3d!.Trace(cells);
            if (points.Count > 1 && pick.Dest.IsPortal)
            {
                Vector3 gate = _layout3d.Approach[Math.Clamp(pick.Dest.Road, 0, _layout3d.Approach.Length - 1)];
                float keep = gate.DistanceTo(PortalPos) + 0.3f;
                while (points.Count > 1 && points[^1].DistanceTo(PortalPos) < keep) points.RemoveAt(points.Count - 1);
                points.Add(gate);
            }
        }
        const float gap = 1.15f;
        float length = points.Count > 1 ? Map3DLayout.LengthOf(points) : 0;
        int need = points.Count > 1 ? Math.Min(90, (int)(length / gap)) : 0;
        while (_routeDots3d.Count < need)
        {
            var dot = new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.20f, BottomRadius = 0.20f, Height = 0.05f, RadialSegments = 12 },
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Color.FromHtml("#ffd874"),
                    EmissionEnabled = true, Emission = Color.FromHtml("#ffd874"), EmissionEnergyMultiplier = 1.6f,
                },
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            _map3d!.AddChild(dot);
            _routeDots3d.Add(dot);
        }
        // 点は行き先の向きへ流れる。
        float phase = _clock3d * 1.5f % gap;
        // 第480期 追記 5: 殿の道は勇者の赤い点線と同じ道を逆に走るので、道の外側へずらして少し大きく見せる（重なって読めなかった）。
        bool rearRoute = need > 0 && St.Rout && St.Hero is { } routeHero && pick is not null && pick.Dest.Id == routeHero.Pos
            && (RearHover is not null || Map11Session.IsRear(_selected));
        for (int i = 0; i < _routeDots3d.Count; i++)
        {
            _routeDots3d[i].Visible = i < need;
            if (i >= need) continue;
            float at = i * gap + phase;
            Vector3 side = Vector3.Zero;
            if (rearRoute)
            {
                Vector3 along = Map3DLayout.Along(points, Math.Min(length, at + 0.3f)) - Map3DLayout.Along(points, Math.Max(0, at - 0.3f));
                along.Y = 0;
                if (along.LengthSquared() > 0.0001f) side = Vector3.Up.Cross(along.Normalized()) * 0.85f;
            }
            _routeDots3d[i].Position = Map3DLayout.Along(points, at) + side + new Vector3(0, rearRoute ? 0.30f : 0.24f, 0);
            float edge = Mathf.Clamp(Math.Min(at, length - at) / 0.8f, 0.2f, 1f) * (rearRoute ? 1.35f : 1f);
            _routeDots3d[i].Scale = new Vector3(edge, 1, edge);
        }
    }

    /// <summary>`PositionMapPicks` の 3D 版——駒の上に、今の画面の札を「押す領域」として重ねる（見えない・触れると説明が出る）。</summary>
    private void PositionMapPicks3D()
    {
        foreach (var pick in _mapPicks)
        {
            bool shown = !_reform && pick.Marker.Shown && (pick.Foe is { } foe ? !foe.Cleared
                : !St.Squads[pick.Squad].Lost && !St.IsLocked(pick.Squad) && ((MapUnitBadge)pick.Button).Count > 0);
            pick.Button.Visible = shown && _pieces3d.TryGetValue(pick.Marker, out Map3DPiece? piece) && piece.Shown;
            if (!pick.Button.Visible) continue;
            pick.Button.Modulate = Colors.Transparent;
            pick.Button.CustomMinimumSize = Vector2.Zero;
            pick.Button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
            Rect2 rect = _pieces3d[pick.Marker].ScreenRect(_camera);
            // 第478期: 遊ぶ UI の下に入った駒の押す領域は出さない——見えないこの領域は遊ぶ UI より手前にあるので、視点を動かして
            // 「1 刻」のボタンの上に駒が来ると、押しても刻が進まず駒が選ばれていた（遊ぶ口の方針 3 が 5 刻目で止まった原因）。
            if (_hud3d is not null && _hud3d.Blocks().Any(block => block.Intersects(rect))) { pick.Button.Visible = false; continue; }
            pick.Button.Position = rect.Position / _uiScale;
            pick.Button.Size = rect.Size / _uiScale;
        }
        if (_waitingReserveBadge is not null)
        {
            _waitingReserveBadge.Visible = !_reform && St.ReserveWaiting;
            _waitingReserveBadge.Position = (_camera.UnprojectPosition(PortalPos) + new Vector2(70, -32)) / _uiScale;
        }
    }

    /// <summary>稼いだ金の数字と金貨を、その場所から右上の金へ飛ばす（`FloatNewGold` の 3D 版）。</summary>
    private void FloatGold3D(Map11State.GoldEvent e)
    {
        if (_hud3d is null) return;
        Vector2 from = _camera.UnprojectPosition(PosOf(e.Cell) + new Vector3(0, 2.4f, 0));
        Vector2 to = _hud3d.GoldAnchor;
        var coins = new MapGoldBurst();
        coins.Start(from, to, e.Amount);
        _hud3d.Plates.AddChild(coins);
        Label label = Map3DHud.Text($"＋{e.Amount}", 34, GoldColor, bold: true);
        label.Position = from;
        label.ZIndex = 50;
        _hud3d.Plates.AddChild(label);
        Tween tw = label.CreateTween();
        tw.TweenProperty(label, "position", from + new Vector2(0, -44), 0.45);
        tw.TweenProperty(label, "position", to, 0.65).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tw.Parallel().TweenProperty(label, "modulate:a", 0.0f, 0.65).SetDelay(0.2);
        tw.TweenCallback(Callable.From(label.QueueFree));
    }

    // =================================================================================
    // 写しと fps（発注書 §7-2）
    // =================================================================================

    /// <summary>頭なしで 1 作戦ターン流す（写しのため・戦闘の画面へは行かない）。規則は画面の通しと同じ口（`Map11Session`）。</summary>
    private void HeadlessTurn3D()
    {
        void Fight()
        {
            BattleResult r = BattleEngine.Run(CampaignSession.CarriedPlayers!, CampaignSession.CarriedEnemies!, CampaignSession.CarriedSeed, verbose: false);
            CampaignSession.ClearCarriedBattle();
            Map11Session.CompleteBattle(r.PlayerWon, r);
        }
        Map11Session.BeginTurn();
        for (int guard = 0; guard < 40 && Map11Session.TurnRunning && !St.Finished; guard++)
            switch (Map11Session.StepTurn())
            {
                case Map11Session.StepKind.Battle: Fight(); break;
                case Map11Session.StepKind.Intercept:
                    if (Map11Session.InterceptNode is not { } node || !Map11Session.ChooseInterceptor(St.NextInterceptor(node))) return;
                    Fight();
                    break;
                case Map11Session.StepKind.Siege: break;
                default: return;
            }
    }

    /// <summary>
    /// 写しの 3 場面（<c>--map11-capture=&lt;path&gt;,m3d-start|m3d-moved|m3d-camp</c>）。既定の組み方で組んで作戦の画面へ。
    /// <c>m3d-moved</c> は 2 隊を北と南へ 2 作戦ターン進めて第1隊を選んだところ、<c>m3d-camp</c> は敵の本陣の 1 つ手前まで進めたところ。
    /// </summary>
    private async System.Threading.Tasks.Task Map3DCaptureScene(string mode)
    {
        try { await Map3DCaptureSteps(mode); }
        catch (Exception e)
        {
            // 写しの通しは投げっぱなしの非同期なので、落ちたら黙って止まる——理由を出して終わる。
            GD.PrintErr($"MAP3D_CAPTURE_FAILED mode={mode} {e}");
            GetTree().Quit(1);
        }
    }

    private async System.Threading.Tasks.Task Map3DCaptureSteps(string mode)
    {
        SceneStart3D();
        if (mode == "m3d-click") { await Map3DClickCheck(); return; }
        if (mode == "story-all")
        {
            // 第481期: 会話の場面を 1 回の起動で順に撮って確かめる（`Map11Main.Story.cs`・自分で終わる）。
            string arg = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--map11-capture=", StringComparison.Ordinal))["--map11-capture=".Length..];
            if (_introOverlay is not null) _introOverlay.Visible = false;
            await StoryScenes(arg[..arg.LastIndexOf(',')]);
            await ToSignal(GetTree().CreateTimer(30), SceneTreeTimer.SignalName.Timeout);
            return;
        }
        if (mode == "m3d-art-hooks")
        {
            // 第482期: 素材の受け口（敵の顔・人物の絵・立ち絵の差分）を、仮の 1 枚を置いて確かめる（`Map11Main.ArtHooks.cs`・自分で終わる）。
            string arg = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--map11-capture=", StringComparison.Ordinal))["--map11-capture=".Length..];
            if (_introOverlay is not null) _introOverlay.Visible = false;
            await ArtHookScenes(arg[..arg.LastIndexOf(',')]);
            await ToSignal(GetTree().CreateTimer(30), SceneTreeTimer.SignalName.Timeout);
            return;
        }
        if (mode == "m3d-h-all")
        {
            // 第480期: 1-3「狩る者」の場面を 1 回の起動で順に撮って確かめる（`Map11Main.HunterShots.cs`・自分で終わる）。
            _hunterForce = true;
            string arg = OS.GetCmdlineUserArgs().First(a => a.StartsWith("--map11-capture=", StringComparison.Ordinal))["--map11-capture=".Length..];
            await HunterScenes(arg[..arg.LastIndexOf(',')]);
            await ToSignal(GetTree().CreateTimer(30), SceneTreeTimer.SignalName.Timeout);
            return;
        }
        // 今の窓を開いたところ（重なりの確かめ用）: `m3d-detail` は隊の配置、`m3d-foes` は敵部隊の一覧。
        if (mode is "m3d-detail" or "m3d-foes")
        {
            Select(Enumerable.Range(0, St.Squads.Length).FirstOrDefault(St.CanSend, 0));
            _showSquadDetail = mode == "m3d-detail";
            _showFoes = mode == "m3d-foes";
            Refresh();
        }
        else if (mode == "m3d-win")
        {
            // 1 章の 1-1 を 3D の画面で制圧まで（戦闘は器具が決着）: 本陣の手前まで進め、そのまま敵の拠点へ入る。合否は `MAP3D_WIN_COMPLETE`。
            await SceneAdvance3D(camp: true);
            for (int turn = 0; turn < 12 && !St.Finished; turn++)
            {
                HeadlessTurn3D();
                Refresh();
                await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            }
            if (St.Finished) ShowResult();
            GD.Print($"MAP3D_WIN_COMPLETE ok={St.Finished && St.Won} won={St.Won} captured={St.Captured} turn={St.Turn} battles={St.Battles} "
                + $"lanes={string.Join("/", St.Graph.Lanes.Select(l => l.Length))} result={_resultOverlay.Visible}");
        }
        else if (mode.StartsWith("m3d-at:", StringComparison.Ordinal))
        {
            // 第480期: 視点を盤の座標（x:z）へ寄せただけの絵（地形の確かめ用）。
            string[] xz = mode["m3d-at:".Length..].Split(':');
            _cam3d!.FocusOn(new Vector3(float.Parse(xz[0], System.Globalization.CultureInfo.InvariantCulture), 0, float.Parse(xz[1], System.Globalization.CultureInfo.InvariantCulture)), snap: true);
            Refresh();
        }
        else if (mode == "m3d-south") { await SceneAdvance3D(camp: true); await SceneSouth3D(); }   // 第476期: 南の道を進む場面
        else if (mode != "m3d-start") await SceneAdvance3D(camp: mode == "m3d-camp");
        GD.Print($"MAP3D_CAPTURE mode={mode} stage={St.Stage.Id} seed={Map11Session.Seed} turn={St.Turn} battles={St.Battles} "
            + "squads=" + string.Join("/", St.Squads.Select(s => s.Lost ? "lost" : $"{PlaceName(s.Pos)}:{s.Units?.Count(u => u.IsAlive) ?? 0}枚")) + $" finished={St.Finished}");
        await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);   // 影と素材の読み込みが落ち着くのを待つ
    }

    /// <summary>写しの「開始時」: 頭の札を閉じ、手札を配って作戦の画面へ。</summary>
    private void SceneStart3D()
    {
        if (_introOverlay is not null) _introOverlay.Visible = false;
        if (_investOverlay is not null) _investOverlay.Visible = false;
        if (Map11Session.Drafting && St.Bench.Count > 0)
        {
            // 1 章は手札から自分で組む（既定の組み方が無い）: 第1隊に 5 枚（主力）、残りを第2隊へ。単発の盤は既定の組み方。
            if (Chapter1.Active)
            {
                int[] open = Enumerable.Range(0, St.Squads.Length).Where(i => !St.IsLocked(i)).Take(2).ToArray();
                foreach (int squad in open)
                    foreach (int seat in FormationRules.PlayableSlots)
                        if (St.Bench.Count > 0) St.SwapWithBench(squad, seat, 0);
            }
            else St.UseDefaultSquads();
        }
        Map11Session.TakeOpening();
        _reform = false;
        ClearHold();
        _guideSquadChosen = _guideDestinationChosen = false;
        Refresh();
    }

    /// <summary>
    /// 写しの「隊が動いた後」／「敵の本陣の手前」。主力（第1隊）は 1 作戦ターン目は拠点で迎え撃ち（先頭の敵は 1 歩で拠点に着く）、
    /// そのあと北の道を進む。<paramref name="camp"/> なら敵の本陣の 1 つ手前のマスに着くまで。第2隊は拠点で番。戦闘は器具が決着（画面へは行かない）。
    /// </summary>
    private async System.Threading.Tasks.Task SceneAdvance3D(bool camp)
    {
        int main = Enumerable.Range(0, St.Squads.Length).Where(St.CanSend).DefaultIfEmpty(0).First();
        bool Near() => !St.Squads[main].Lost && St.Squads[main].Pos == St.Graph.Lanes[0][^1];
        for (int turn = 0; turn < 14 && !St.Finished && !St.Squads[main].Lost; turn++)
        {
            bool foesComing = St.Nodes.SelectMany(r => r).Any(n => !n.Cleared && St.TurnsToHome(n) is <= 1);
            Dest to = foesComing && St.Squads[main].Pos == St.Graph.Home ? Dest.Home
                : camp ? Dest.Deep(St.Graph, 0) : Dest.Of(St.Graph, St.Graph.Lanes[0][0]);
            Map11Session.SetDestination(main, to);
            if (!to.AtHome && St.AtDest(main, to)) break;
            HeadlessTurn3D();
            Refresh();
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);   // 駒が歩き終えるのを待つ（絵を撮る通しでも歩かせる）
            if (camp ? Near() : St.Squads[main].Pos != St.Graph.Home) break;
        }
        Select(main);
        if (camp && St.Portal.On && Near()) Map11Session.SetDestination(main, Dest.Portal(St.Graph, 0));
        _showSquadDetail = false;
        _guideDestinationChosen = camp;
        ShowToast("", GoldColor, 0);
        Refresh();
    }

    /// <summary>
    /// 「南の道を進む場面」（第476期 §5）。主力を南の道の 2 マス目まで歩かせ（途中の敵とは器具が決着）、次の行き先を 3 マス目にして選んだところ。
    /// 着けたら true。
    /// </summary>
    private async System.Threading.Tasks.Task<bool> SceneSouth3D()
    {
        int main = Enumerable.Range(0, St.Squads.Length).Where(St.CanSend).DefaultIfEmpty(0).First();
        int target = St.Graph.Lanes[1][1];
        for (int turn = 0; turn < 8 && !St.Finished && !St.Squads[main].Lost && St.Squads[main].Pos != target; turn++)
        {
            Map11Session.SetDestination(main, Dest.Of(St.Graph, target));
            HeadlessTurn3D();
            Refresh();
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        }
        bool there = !St.Squads[main].Lost && St.Squads[main].Pos == target;
        Select(main);
        if (there && !St.Finished) Map11Session.SetDestination(main, Dest.Of(St.Graph, St.Graph.Lanes[1][2]));
        _showSquadDetail = false;
        _guideDestinationChosen = true;
        ShowToast("", GoldColor, 0);
        Refresh();
        return there;
    }

    /// <summary>
    /// 3D 用のレイアウトの検査（<c>--map3d-layout-smoke</c>・第474期 直し 7）。写しの 3 場面（開始時 ／ 隊が動いた後 ／ 敵の本陣の手前）を続けて作り、
    /// 名札が台・橋・旗（駒）・遊ぶ UI・ほかの札と重ならないこと、札とマスの台が画面に収まること、下の UI が画面の 15% 以内であることを見る。
    /// 合否は <c>MAP3D_LAYOUT_SMOKE_COMPLETE</c> の行。<b>盤は読むだけ</b>（通しは写しの口と同じ）。
    /// </summary>
    private async System.Threading.Tasks.Task Map3DLayoutSmoke()
    {
        var fails = new List<string>();
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            _linesOff = true;   // 第479期: 検査の場面は器具が進めるので、セリフは検査が自分で出す
            SceneStart3D();
            await Settle();
            Check("開始時");
            if (_layout3d!.Pan is not null) { await Wide(); await LeanScenes(); goto done; }   // 第477期: 1 画面に入らない盤（1 章の 1-2）は別の 4 場面
            await SceneAdvance3D(camp: false);
            await Settle();
            Check("動いた後");
            await SceneAdvance3D(camp: true);
            await Settle();
            Check("本陣の手前");
            // 第476期 §5: 南の道を進む場面（長い道・橋・湧いた部隊が歩いている刻）。南の道が 2 マス以上ある盤だけ。
            if (St.Graph.Lanes.Count > 1 && St.Graph.Lanes[1].Length >= 3)
            {
                bool south = await SceneSouth3D();
                await Settle();
                Check("南の道");
                if (!south) fails.Add("南の道: 主力が南の道の 2 マス目に着けなかった");
            }
            await LeanScenes();
        }
        catch (Exception e) { fails.Add("例外:" + e.Message); GD.PrintErr(e.ToString()); }
        done:
        // 第481期 §5: 結果の札が出ているとき、盤の左 60% が隠れていない（暗幕なし・札は右 1／3）。検査は盤を終わらせずに、札だけを出して測る。
        try
        {
            ShowResult();
            await Settle();
            var (card, dimA) = ResultCardRect();
            Vector2 seen = GetViewport().GetVisibleRect().Size;
            GD.Print($"MAP3D_LAYOUT 結果の札 {card.Position.X:F0},{card.Position.Y:F0} {card.Size.X:F0}x{card.Size.Y:F0}（左の端は画面の幅の {card.Position.X / seen.X * 100:F1}%）暗幕 {dimA:F2}");
            if (card.Size == Vector2.Zero) fails.Add("結果の札: 出ていない");
            else if (card.Position.X < seen.X * 0.6f || dimA > 0.01f) fails.Add($"結果の札: 盤の左 60% が隠れている（左の端 {card.Position.X:F0}・暗幕 {dimA:F2}）");
            if (card.End.X > seen.X + 1 || card.End.Y > seen.Y + 1 || card.Position.Y < 0) fails.Add("結果の札: 画面からはみ出す");
            _resultOverlay.Visible = false;
        }
        catch (Exception e) { fails.Add("結果の札: 例外 " + e.Message); GD.PrintErr(e.ToString()); }
        foreach (string line in fails) GD.Print("MAP3D_LAYOUT_FAIL " + line);
        Vector2 size = GetViewport().GetVisibleRect().Size;
        GD.Print($"MAP3D_LAYOUT_SMOKE_COMPLETE stage={St.Stage.Id} lanes={string.Join("/", St.Graph.Lanes.Select(l => l.Length))} view={size.X:F0}x{size.Y:F0} "
            + $"bottom={_hud3d!.BottomShare * 100:F1}% ok={fails.Count == 0} failed={fails.Count}");
        GetTree().Quit(fails.Count == 0 ? 0 : 1);

        // 第477期 §5: 1-2 の場面（開始時 ／ 関所を取った後 ／ 門の前 ／ 画面を右へ寄せた所）。主力（いちばん枚数の多い隊）を 関所 → 見張り台 → 砦の手前 へ進める。
        // 戦闘は器具が決着（`HeadlessTurn3D`）。着けなければ、その場面は「着けなかった」として落とす。
        async System.Threading.Tasks.Task Wide()
        {
            MapGraph g = St.Graph;
            int main = Enumerable.Range(0, St.SquadCap).Where(i => St.Squads[i].Units is { Count: > 0 }).OrderByDescending(i => St.Squads[i].Units!.Count).First();
            int gate = g.Lanes[1].First(c => g.IsOutpost(c));
            bool Up() => !St.Squads[main].Lost && St.Squads[main].Units!.Any(u => u.IsAlive) && !St.Finished;
            void Go(Dest d, Func<bool> until)
            {
                for (int k = 0; k < 8 && Up() && !until(); k++) { Map11Session.SetDestination(main, d); HeadlessTurn3D(); }
                Refresh();
            }
            Go(Dest.Of(g, gate), () => St.HeldOutposts.Contains(gate));
            _cam3d!.FocusOn(PosOf(gate), snap: true);
            await Settle();
            Check("関所を取った後");
            if (!St.HeldOutposts.Contains(gate)) fails.Add("関所を取った後: 主力が関所を取れなかった");
            // 第478期 §5: 中間拠点の名札が常に出ている（3 つとも札を持ち、画面に入っている関所の札は見えていて、名前と「味方」が読める）。
            Vector2 seenSize = GetViewport().GetVisibleRect().Size;
            bool platesOk = _outpostPlates3d.Count == g.Cells.Count(c => c.Kind == MapGraph.Kind.Outpost)
                && _outpostPlates3d.TryGetValue(gate, out Label? gatePlate) && gatePlate.Visible && gatePlate.Text.Contains(PlaceName(gate))
                && new Rect2(0, 0, seenSize.X, seenSize.Y).Encloses(gatePlate.GetGlobalRect());
            GD.Print($"MAP3D_LAYOUT 中間拠点の名札 {string.Join(" ／ ", _outpostPlates3d.Select(kv => $"{PlaceName(kv.Key)}「{kv.Value.Text}」{(kv.Value.Visible ? "出ている" : "画面の外")}"))}");
            if (!platesOk) fails.Add("関所を取った後: 中間拠点の名札が出ていない");
            // 門を開ける 2 つ目は渡し場（別働が南の軽い 2 部隊を抜く）——見張り台は主力でも落ちることがあるので、場面を作る道には使わない。
            int second = Enumerable.Range(0, St.SquadCap).Where(i => i != main && St.Squads[i].Units is { Count: > 0 }).OrderByDescending(i => St.Squads[i].Units!.Count).DefaultIfEmpty(-1).First();
            int ferry = g.Lanes[2].First(c => g.IsOutpost(c));
            for (int k = 0; k < 9 && second >= 0 && !St.GateOpen && !St.Finished; k++)
            {
                Map11Session.SetDestination(second, Dest.Of(g, ferry));
                Map11Session.SetDestination(main, Dest.Of(g, gate));
                HeadlessTurn3D();
            }
            Go(Dest.Of(g, g.Lanes[1][^1]), () => St.Squads[main].Pos == g.Lanes[1][^1]);
            _cam3d.FocusOn(PosOf(g.Portal), snap: true);
            await Settle();
            Check("門の前");
            if (!St.GateOpen) fails.Add("門の前: 門が開かなかった（別働が渡し場を取れなかった）");
            _cam3d.FocusOn(PosOf(g.Portal) + new Vector3(12, 0, 0), snap: true);
            await Settle();
            Check("画面を右へ寄せた所");
        }

        // 第479期 §7: 引いた視点で拠点の名札が読める大きさ ／ セリフの吹き出しが名札・札と重ならない ／ 遭遇の台が出ているとき盤の上 2／3 が隠れていない・当たる 2 者が画面に入っている。
        async System.Threading.Tasks.Task LeanScenes()
        {
            Vector2 view = GetViewport().GetVisibleRect().Size;
            // ---- 引いた視点 ----
            float zoom0 = _cam3d!.Zoom;
            _cam3d.SetZoom(_cam3d.ZoomFar);
            await Settle();
            int font = BasePlateFont3D();
            var bases = new[] { ("拠点", _homePlate3d), ("敵の拠点", _portalPlate3d) }.Concat(_outpostPlates3d.Select(kv => (PlaceName(kv.Key), (Label?)kv.Value)))
                .Where(x => x.Item2 is { Visible: true }).ToList();
            int smallest = bases.Count == 0 ? 0 : bases.Min(x => x.Item2!.GetThemeFontSize("font_size"));
            float lowest = bases.Count == 0 ? 0 : bases.Min(x => x.Item2!.Size.Y);
            GD.Print($"MAP3D_LAYOUT 引いた視点 引き具合 {_cam3d.Zoom:0.##}（ふだん {zoom0:0.##}）拠点の名札の文字 {smallest}px（決まり {font}px・ふだん 21px）札の高さ {lowest:F0}px 出ている札 {string.Join("・", bases.Select(x => x.Item1))}");
            if (St.Stage.OutpostRewards is not null && (bases.Count == 0 || smallest < 26 || lowest < 30)) fails.Add($"引いた視点: 拠点の名札が小さい（文字 {smallest}px・高さ {lowest:F0}px）");
            _cam3d.SetZoom(zoom0);
            await Settle();

            // ---- セリフの吹き出し ----
            if (Map11Lines.SpeakerOf(St, "ガン") is { } who)
            {
                _hud3d!.Say(who, $"「{Map11Lines.All.OrderByDescending(l => l.Text.Length).First().Text}」", 30f);
                await Settle();
                Check("セリフ");
                Rect2 say = _hud3d.SayRect;
                if (!new Rect2(0, 0, view.X, view.Y).Encloses(say)) fails.Add("セリフ: 吹き出しが画面からはみ出す");
                foreach (Rect2 block in _hud3d.Blocks())
                    if (!block.IsEqualApprox(say) && Overlap(block, say) > 4) fails.Add("セリフ: 吹き出しが遊ぶ UI のほかの札に重なる");
                foreach (var (name, rect) in _signRects3d.Concat(_plateRects3d).Concat(_chipRects3d))
                    if (Overlap(rect, say) > 4) fails.Add($"セリフ: 吹き出しが {name} に重なる");
                GD.Print($"MAP3D_LAYOUT セリフ 吹き出し {say.Position.X:F0},{say.Position.Y:F0} {say.Size.X:F0}x{say.Size.Y:F0}「{_hud3d.SayText}」");
                _hud3d.HideSay();
            }

            // ---- 遭遇の台 ----
            int main = Enumerable.Range(0, St.SquadCap).Where(i => !St.Squads[i].Lost && St.Squads[i].Units is { } us && us.Any(u => u.IsAlive))
                .OrderByDescending(i => St.Squads[i].Units!.Count(u => u.IsAlive)).DefaultIfEmpty(-1).First();
            bool met = false;
            for (int turn = 0; turn < 6 && main >= 0 && !met && !St.Finished && St.Portal.On; turn++)
            {
                int lane = St.Squads[main].Road >= 0 && St.Squads[main].Road < St.Stage.LaneCount ? St.Squads[main].Road : 0;
                Map11Session.SetDestination(main, St.GateOpen ? Dest.Portal(St.Graph, lane) : Dest.Deep(St.Graph, lane));
                if (!Map11Session.TurnRunning) Map11Session.BeginTurn();
                for (int step = 0; step < 30 && Map11Session.TurnRunning && !met; step++)
                {
                    Map11Session.StepKind kind = Map11Session.StepTurn();
                    if (kind == Map11Session.StepKind.Battle && Map11Session.PendingSquad == main) { met = true; break; }
                    if (kind == Map11Session.StepKind.Battle)
                    {
                        foreach (UnitState u in CampaignSession.CarriedEnemies!) u.Hp = 0;
                        CampaignSession.ClearCarriedBattle();
                        Map11Session.CompleteBattle(true);
                    }
                    else if (kind == Map11Session.StepKind.Intercept)
                    {
                        if (!(St.NextInterceptor(Map11Session.InterceptNode!) is var j && j >= 0 && Map11Session.ChooseInterceptor(j))) break;
                        foreach (UnitState u in CampaignSession.CarriedEnemies!) u.Hp = 0;
                        CampaignSession.ClearCarriedBattle();
                        Map11Session.CompleteBattle(true);
                    }
                    else if (kind == Map11Session.StepKind.Siege) { if (!Map11Session.ContinueSiege()) break; met = Map11Session.PendingSquad == main; }
                    else break;
                }
            }
            if (!met) { fails.Add("遭遇の台: 遭遇を作れなかった（主力が敵に当たらなかった）"); return; }
            Refresh();
            ShowEncounter();
            await Settle();
            for (int i = 0; i < 6; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Settle();
            // 第479期の追記 8: 遭遇の台が出ているとき、盤の上 2／3 が隠れていない・当たる 2 者が画面に入っている。
            Rect2 stand = ClashShown ? ClashRect : default;
            // 第479期の続き: 名札が固定の UI の下に入っていない・「引き返す」に触れると引く先が光る。
            int under = PlatesUnderHud3D();
            ClashBackHover = true;
            for (int i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            int backCell = ClashBackCell;
            int wantCell = _clashSquad >= 0 ? St.RetreatCellOf(_clashSquad, siege: false) : -1;
            GD.Print($"MAP3D_LAYOUT 続き 名札と固定の UI の重なり {under}・引く先の輪 {(backCell >= 0 ? PlaceName(backCell) : "—")}（規則の引く先 {(wantCell >= 0 ? PlaceName(wantCell) : "—")}）・割れの印 {ClashRiskCount}");
            ClashBackHover = false;
            if (under > 0) fails.Add($"名札: 固定の UI の下に {under} 枚");
            if (_clashB.Visible && (backCell < 0 || backCell != wantCell)) fails.Add("引く先の輪: 出ていない ／ 規則の引く先と違う");
            // 第479期の続き 2: 味方の台が左・敵の台が右・勝ち目が出たら先頭の駒が 1 行話す・強い敵の顔にだけ王冠。
            for (int i = 0; i < 240 && ClashOddsText == "…"; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var (ownSide, foeSide) = ClashSides;
            bool strong = _clashNode?.Def.Strong ?? false;
            GD.Print($"MAP3D_LAYOUT 続き 2 味方の台 x={ownSide.Position.X:F0} 敵の台 x={foeSide.Position.X:F0}・勝ち目「{ClashOddsText}」セリフ {ClashSayText}・王冠 {ClashFoeCrown}（強い敵 {strong}）");
            if (ownSide.End.X > foeSide.Position.X) fails.Add("遭遇の台: 味方の台が敵の台の左に無い");
            if (ClashOddsText.Length > 0 && ClashOddsText != "…" && ClashSayText.Length == 0) fails.Add("遭遇の台: 勝ち目が出たのに先頭の駒が話していない");
            if (ClashFoeCrown != strong) fails.Add("遭遇の台: 王冠の印が強い敵かどうかと合っていない");
            var (ringOwn, ringFoe) = ClashRings();
            var upper = new Rect2(0, 0, view.X, view.Y * 2f / 3f);
            bool dim = _encounterOverlay is ColorRect { Color.A: > 0.01f };
            float hidden = Overlap(stand, upper.Grow(-2)) / (upper.Size.X * upper.Size.Y);
            for (int wait = 0; wait < 40 && ClashOddsText == "…"; wait++) await ToSignal(GetTree().CreateTimer(0.1), SceneTreeTimer.SignalName.Timeout);
            GD.Print($"MAP3D_LAYOUT 遭遇の台 {(ClashShown ? $"{stand.Position.X:F0},{stand.Position.Y:F0} {stand.Size.X:F0}x{stand.Size.Y:F0}（画面の高さの {stand.Size.Y / view.Y * 100:F0}%）" : "出ていない")} "
                + $"盤の上 2／3 のうち隠れている {hidden * 100:F1}% 暗幕 {(dim ? "あり" : "なし")} 盤を隠す窓 {(CoverShown ? "あり" : "なし")} 下の UI {(_hud3d!.BottomShown ? "出ている" : "隠した")} "
                + $"青い輪 {ringOwn.Position.X:F0},{ringOwn.Position.Y:F0} 赤い輪 {ringFoe.Position.X:F0},{ringFoe.Position.Y:F0} 勝ち目「{ClashOddsText}」"
                + $"敵 {_clashFoeLead.NameText} 席 {_clashFoe.SeatCount} ／ 味方 {_clashOwnLead.NameText} 席 {_clashOwn.SeatCount} 線 {_clashOwn.LinkCount} ボタン「{_clashA.Text}」{(_clashB.Visible ? $"「{_clashB.Text}」" : "")}");
            if (!ClashShown) fails.Add("遭遇の台: 出ていない");
            else
            {
                if (stand.Position.Y < view.Y * 2f / 3f - 3) fails.Add($"遭遇の台: 画面の下 1／3 を超えている（上の端 {stand.Position.Y:F0}・画面の高さ {view.Y:F0}）");
                if (hidden > 0.005f || dim || CoverShown) fails.Add("遭遇の台: 盤の上 2／3 が隠れている");
                foreach (var (whom, ring) in new[] { ("当たる隊（青い輪）", ringOwn), ("敵（赤い輪）", ringFoe) })
                    if (!upper.Encloses(ring)) fails.Add($"遭遇の台: {whom}が画面の上 2／3 に入っていない（{ring.Position.X:F0},{ring.Position.Y:F0} {ring.Size.X:F0}x{ring.Size.Y:F0}）");
                if (_hud3d.BottomShown) fails.Add("遭遇の台: いつもの隊の札と刻のボタンが出たまま");
                if (_clashOwn.SeatCount == 0 || _clashFoe.SeatCount == 0) fails.Add("遭遇の台: 陣形の台に駒がいない");
                if (ClashOddsText is not ("優勢" or "互角" or "劣勢")) fails.Add($"遭遇の台: 勝ち目の印が出ていない（「{ClashOddsText}」）");
                if (!_clashA.Visible || _clashA.Text != "戦う") fails.Add("遭遇の台: 「戦う」が出ていない");
                if (St.Stage.EncounterRetreat != _clashB.Visible) fails.Add("遭遇の台: 「引き返す」の出し方が盤の規則と合わない");
            }
            _encounterOverlay.Visible = false;
            foreach (UnitState u in CampaignSession.CarriedEnemies!) u.Hp = 0;
            CampaignSession.ClearCarriedBattle();
            Map11Session.CompleteBattle(true);
        }

        async System.Threading.Tasks.Task Settle()
        {
            await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);
            for (int i = 0; i < 3; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        void Check(string scene)
        {
            Vector2 view = GetViewport().GetVisibleRect().Size;
            var screen = new Rect2(0, 0, view.X, view.Y);
            // 第477期: 1 画面に入らない盤では、画面の外・端で切れているもの・遊ぶ UI の下にあるものは見ない（視点を動かして見る物）。全部が見えているものどうしの重なりだけを見る。
            bool wideBoard = _layout3d!.Pan is not null;
            bool Seen(Rect2 r) => screen.Encloses(r) && !_hudRects3d.Any(h => Overlap(r, h.Rect) > 0);
            List<(string Name, Rect2 Rect)> Pick(List<(string Name, Rect2 Rect)> all) => wideBoard ? all.Where(x => Seen(x.Rect)).ToList() : all;
            var plates = Pick(_plateRects3d); var chips = Pick(_chipRects3d); var signs = Pick(_signRects3d); var discs = Pick(_discRects3d); var bodies = Pick(_bodyRects3d);
            void Against(string kind, List<(string Name, Rect2 Rect)> mine, List<(string Name, Rect2 Rect)> others, bool same = false)
            {
                for (int i = 0; i < mine.Count; i++)
                    for (int j = same ? i + 1 : 0; j < others.Count; j++)
                        if (Overlap(mine[i].Rect, others[j].Rect) > 4)
                            { fails.Add($"{scene}: {mine[i].Name} が {others[j].Name} に重なる（{kind}）"); GD.Print($"MAP3D_LAYOUT_OVERLAP {scene} {mine[i].Name} {mine[i].Rect} × {others[j].Name} {others[j].Rect} ＝ {Overlap(mine[i].Rect, others[j].Rect):F0}"); }
            }
            // 名札: 台・橋・旗（駒）・遊ぶ UI・建物の名札・ほかの名札と重ならない。
            Against("名札×台", plates, discs);
            Against("名札×橋", plates, _bridgeRects3d);
            Against("名札×駒", plates, bodies);
            Against("名札×UI", plates, _hudRects3d);
            Against("名札×建物の名札", plates, signs);
            Against("名札×名札", plates, plates, same: true);
            // 「あと n」: 駒・名札・遊ぶ UI・ほかの「あと n」と重ならない（空の台の上には載る）。
            Against("あと n×駒", chips, bodies);
            Against("あと n×名札", chips, plates);
            Against("あと n×UI", chips, _hudRects3d);
            Against("あと n×あと n", chips, chips, same: true);
            Against("建物の名札×UI", signs, _hudRects3d);
            foreach (var (name, rect) in plates.Concat(chips).Concat(signs))
                if (!screen.Encloses(rect)) fails.Add($"{scene}: {name} が画面からはみ出す");
            // マスの台と駒: 画面に収まり、遊ぶ UI の下に隠れない。
            foreach (var (name, rect) in discs.Concat(bodies))
            {
                if (!screen.Encloses(rect)) fails.Add($"{scene}: {name} が画面からはみ出す");
                foreach (var (_, block) in _hudRects3d)
                    if (Overlap(rect, block) > 4) fails.Add($"{scene}: {name} が遊ぶ UI の下に隠れる");
            }
            if (_hud3d!.BottomShare > 0.15f) fails.Add($"{scene}: 下の UI が画面の {_hud3d.BottomShare * 100:F1}%（15% 以内）");
            if (!_hud3d.OrdersFit) fails.Add($"{scene}: 隊の札の命令の文が 2 行に収まらない");
            if (_chips3d.Count(kv => kv.Key.Dest.IsPortal && kv.Value.Visible) > 1) fails.Add($"{scene}: 敵の拠点の「あと n」が 2 つ出ている");
            GD.Print($"MAP3D_LAYOUT {scene} 名札 {_plateRects3d.Count} あと n {_chipRects3d.Count} 駒 {_bodyRects3d.Count} 台 {_discRects3d.Count} 橋 {_bridgeRects3d.Count} "
                + $"作戦ターン {St.Turn} 隊 " + string.Join("/", St.Squads.Where(q => !St.IsLocked(q.Index)).Select(q => q.Lost ? "失" : PlaceName(q.Pos))));
        }
    }

    /// <summary>
    /// 押す操作の確かめ（<c>,m3d-click</c>）。本物のマウスの出来事を画面の座標で流し、
    /// 駒を押す → 隊が選ばれる ／ マスに触れる → 行き先の案内が出る ／ マスを押す → 行き先が決まる ／ 隊の札を押す → 隊が替わる ／
    /// 「刻を進める」を押す → 時計が動く、の 5 つを見る。合否は <c>MAP3D_CLICK_CHECK</c> の行。
    /// </summary>
    private async System.Threading.Tasks.Task Map3DClickCheck()
    {
        async System.Threading.Tasks.Task Frames(int n = 3)
        {
            for (int i = 0; i < n; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        void Move(Vector2 at)
        {
            Vector2 p = GetViewport().GetFinalTransform() * at;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
        }
        void Click(Vector2 at)
        {
            Vector2 p = GetViewport().GetFinalTransform() * at;
            Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = p, GlobalPosition = p });
            Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = p, GlobalPosition = p });
        }
        await Frames(6);
        var fails = new List<string>();
        int squad = Enumerable.Range(0, St.Squads.Length).Where(St.CanSend).DefaultIfEmpty(0).Last();
        var pick = _mapPicks.First(p => p.Foe is null && p.Squad == squad);
        Click(_pieces3d[pick.Marker].ScreenRect(_camera).GetCenter());
        await Frames();
        if (!(_selected == squad && _guideSquadChosen)) fails.Add("piece-select");
        int cell = St.Graph.Lanes[0][0];
        Vector2 disc = _camera.UnprojectPosition(_cellMarks3d[cell].GlobalPosition);
        Move(disc);
        await Frames();
        if (_hoverDestination?.Dest.Id != cell) fails.Add("cell-hover");
        Click(disc);
        await Frames();
        if (Map11Session.Destinations[squad] != Dest.Of(St.Graph, cell)) fails.Add("cell-destination");
        Click(_hud3d!.CardRect(0).GetCenter());
        await Frames();
        if (_selected != 0) fails.Add("card-select");
        // 第476期（不具合 1）: 本陣の大きな台を押す → 行き先が敵の拠点（最短の道）になる。「あと n」は 1 つだけ。
        Click(_pieces3d[pick.Marker].ScreenRect(_camera).GetCenter());
        await Frames();
        Vector2 camp = _camera.UnprojectPosition(_cellMarks3d[St.Graph.Portal].GlobalPosition);
        Move(camp);
        await Frames();
        int portalChips = _chips3d.Count(kv => kv.Key.Dest.IsPortal && kv.Value.Visible);
        if (_hoverDestination?.Dest.IsPortal != true || portalChips != 1) fails.Add($"camp-hover(chips={portalChips})");
        Click(camp);
        await Frames();
        Dest shortest = Enumerable.Range(0, St.Graph.Lanes.Count).Select(l => Dest.Portal(St.Graph, l))
            .OrderBy(d => St.Graph.Path(St.Squads[squad].Pos, St.Squads[squad].Via, d.Id, d.Lane).Count).First();
        if (Map11Session.Destinations[squad] != shortest) fails.Add("camp-destination");
        // 第476期（不具合 5）: 「敵部隊」を押す → 敵部隊の一覧が開く。もう一度押すと閉じる。
        Click(_hud3d.FoesRect.GetCenter());
        await Frames();
        bool opened = _foeDrawer.Visible && _foeDrawer.GetGlobalRect().Size.X > 100
            && new Rect2(Vector2.Zero, UiViewSize).Grow(2).Encloses(_foeDrawer.GetGlobalRect());
        if (!opened) fails.Add($"foes-open(visible={_foeDrawer.Visible} rect={_foeDrawer.GetGlobalRect()} view={UiViewSize})");
        Click(_hud3d.FoesRect.GetCenter());
        await Frames();
        if (_foeDrawer.Visible) fails.Add("foes-close");
        bool clock = Map11Clock.On;
        Click(_hud3d.RunRect.GetCenter());
        await Frames();
        if (clock && !Map11Clock.Running && !Map11Session.TurnRunning && St.Turn == 0) fails.Add("run");
        Map11Clock.Stop();
        GD.Print($"MAP3D_CLICK_CHECK ok={fails.Count == 0} failed=[{string.Join(",", fails)}] squad={squad} cell={cell} clock={clock}");
    }

    /// <summary>
    /// fps を測る（<c>--map3d-fps</c>・発注書 §7-2）。開始時の場面（<c>m3d-start</c> と同じ）を 10 秒流した平均。垂直同期は切る。
    /// </summary>
    private async System.Threading.Tasks.Task Map3DFps()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await Map3DCaptureScene("m3d-start");
        DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = 0;
        await ToSignal(GetTree().CreateTimer(2.0), SceneTreeTimer.SignalName.Timeout);
        int frames = Engine.GetFramesDrawn();
        ulong start = Time.GetTicksUsec();
        await ToSignal(GetTree().CreateTimer(10.0), SceneTreeTimer.SignalName.Timeout);
        double seconds = (Time.GetTicksUsec() - start) / 1_000_000.0;
        int drawn = Engine.GetFramesDrawn() - frames;
        Vector2I size = DisplayServer.WindowGetSize();
        GD.Print($"MAP3D_FPS avg={drawn / seconds:F1} frames={drawn} seconds={seconds:F2} window={size.X}x{size.Y} vsync=off "
            + $"parts={_map3d?.PartCount} stage={St.Stage.Id} seed={Map11Session.Seed}");
        GetTree().Quit();
    }
}
