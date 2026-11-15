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
            float radius = c.Kind switch { MapGraph.Kind.Home => 2.5f, MapGraph.Kind.Portal => 2.3f, MapGraph.Kind.Outpost => flagged ? 2.25f : 1.9f, _ => 1.55f };
            var mark = new Map3DCellMark(radius, flagged) { Position = PosOf(c.Id) };
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
        var shown = _mapPicks.Where(p => p.Marker.Shown && (p.Foe is { } foe ? !foe.Cleared
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
                else if (cell == St.Graph.Portal) at = PortalPos + new Vector3(0.2f, 0, rank * 2.7f);
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
                    bounty ? $"賞金 ＋{St.KillGold(foe)}" : "", bounty && foe.Def.Tag.Length > 0));
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
            if (_firstRefresh || !known) piece.Snap(at);
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
        string goal = St.HeroClock && St.Hero is { Cleared: false } hero ? $"目標　勇者を倒す（HP {hero.HpNow}）"
            : !St.Portal.On ? $"目標　敵部隊をすべて抜く（{St.ClearedCount} / {St.Stage.TotalNodes}）"
            : St.Won ? $"目標　{Map11.PortalName}を制圧した"   // 第476期: 制圧で勝つ盤で、勝った後に「残る敵部隊を倒す」と出ていた
            : St.Captured ? $"目標　残る敵部隊を倒す（あと {left}）"
            : $"目標　{Map11.PortalName}を制圧する　・　残る敵部隊 {left}";
        _hud3d.SetHeader(title, turn, goal);
        // 第477期 §3-a: 金を使える場所（拠点・取った中間拠点）に隊がいて、倒れた駒を呼び戻せるか金が 8 以上あるとき、隊の札と金の札に印（新しい 1-2 の盤だけ）。
        bool teach = St.Stage.OutpostRewards is not null && St.Stage.Gold.On && !St.Finished;
        bool Spendable(Map11State.Squad q) => teach && !q.Lost && q.Units is { Count: > 0 } && St.CanReform(q.Index)
            && ((St.Stage.Gold.Revive > 0 && St.FallenUnits.Count > 0 && St.Gold >= St.Stage.Gold.Revive) || St.Gold >= 8);
        bool idleGold = GoldIdle3D();   // 第478期 §4-3: 金が貯まっているのに 2 刻使っていない
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
            cards.Add(new Map3DSquadCardInfo(s.Index, s.Label + (Spendable(s) ? " ◆" : ""), faces, faces.Count, faces.Count == 0 ? 0 : SquadHpPercent(s),
                (St.Time.On ? "→ " + OrderText(s.Index) : where) + (Spendable(s) ? "　◆ 金を使える" : ""), s.Index == _selected && _guideSquadChosen, s.Lost));
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
        _hud3d.SetShown(!_reform);
        ProcessRadar3D(!_reform);   // 第477期: 画面の外の知らせ・全体図・歩く敵の赤い点線
        if (_reform) return;

        // 第476期（自分で遊んで見つけた）: 行き先に触れているあいだは、出来事の 1 行より案内の 1 行（経路上の敵・「峠の関: 硬い・賞金 14」・拠点が空になる）を先に出す
        // ——編成を終えた直後の 3 秒は「編成を終えました」が案内を隠していた。
        bool reading = _hoverDestination is { Node.Disabled: false } && _guideSquadChosen && !St.Finished && !Map11Session.TurnRunning;
        _hud3d.SetToast(reading ? "" : _toast.Text, _toast.GetThemeColor("font_color"));
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
        bool modal = _encounterOverlay.Visible || _interceptOverlay.Visible || _resultOverlay.Visible || _confirmOverlay.Visible || _siegeOverlay.Visible
                     || _introOverlay is { Visible: true } || _investOverlay is { Visible: true } || NoticeShown;
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

        // ---- 建物の名札（拠点 ／ 敵の拠点）。文は今の画面の札と同じ。 ----
        Map3DLayout.Table? sheet = _layout3d.Sheet;
        // 引き出し（今の窓）が開いている側の名札は隠す（窓の下に半分だけ見えるのを避ける）。
        if (_homePlate3d is not null) _homePlate3d.Visible = !_showSquadDetail;
        if (_portalPlate3d is not null) _portalPlate3d.Visible = !_showFoes && !_showHistory;
        if (_homePlate3d is not null)
        {
            string head = sheet?.HomeName is { Length: > 0 } hn ? hn + "\n" : "";
            SetPlate(_homePlate3d, head + _homeLabel.Text, St.HomeLost ? UiKit.Hurt : PlayerColor,
                (sheet?.Fort ?? BasePos) + new Vector3(0, 9.8f, 0));
        }
        if (_portalPlate3d is not null && _portalMarker is not null)
        {
            string head = sheet?.PortalName is { Length: > 0 } pn ? pn + "\n" : "";
            string note = _portalMarker.Note.Length > 0 ? "\n" + _portalMarker.Note : "";
            SetPlate(_portalPlate3d, head + _portalMarker.Caption + note, St.Captured ? PlayerColor : EnemyColor,
                (sheet?.Camp ?? PortalPos) + new Vector3(0, 8.4f, 0));
        }

        foreach (var (cell, plate) in _outpostPlates3d)
        {
            bool mine = St.HeldOutposts.Contains(cell);
            if (_cellMarks3d.TryGetValue(cell, out Map3DCellMark? flag)) flag.SetFlag(mine);
            plate.Visible = !modal;
            string text = _outpostMarks.First(m => m.Cell == cell).Name.Text;
            SetPlate(plate, "◆ " + text, mine ? PlayerColor : EnemyColor, PosOf(cell) + new Vector3(0, 6.6f, 0));
            // 遊ぶ UI（隊の札・刻を進める）の下に入った名札は出さない（失われた隊の薄い札の下に透けて見えた）。
            if (plate.Visible && _hud3d.Blocks().Any(block => block.Intersects(plate.GetGlobalRect()))) plate.Visible = false;
        }
        ProcessNotices3D(modal);

        // ---- いま何をすればよいかの 1 行（左下・隊の札の上） ----
        string hint = St.Finished || Map11Session.TurnRunning ? ""
            : GoldIdle3D() && !_reform && _hoverDestination is null ? "金が貯まっています（強化・呼び戻し）——拠点か取った中間拠点にいる隊の「編成・呼ぶ・強化」で使える"
            : !_guideSquadChosen ? "隊の札か、盤の上の駒を押して隊を選ぶ"
            : !St.CanSend(_selected) ? (St.CanReform(_selected) ? "この隊は空席——「編成・呼ぶ・強化」で駒を配る" : "")
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
                foreach (var (_, o) in _discRects3d) cost += Overlap(r, o);
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
                float cost = Cost(new Rect2(at, size), plate);
                if (cost < least) { least = cost; best = at; }
                if (cost <= 0) break;
            }
            return best;
        }

        // ---- 駒の名札: 台のすぐ下 → 右 → 左 → 頭の上 → さらに下 ----
        foreach (var p in pieces)
        {
            Vector2 size = p.Piece.Plate.Size;
            Rect2 body = p.Body;
            float cx = body.GetCenter().X;
            // 駒が載っている台（体の足元と重なる台）の下の縁まで下げる。
            float under = body.End.Y;
            foreach (var (_, disc) in _discRects3d)
                if (disc.Grow(2).HasPoint(new Vector2(cx, body.End.Y - 6))) under = Math.Max(under, disc.End.Y);
            Vector2 at = Choose(new[]
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
            }, size, plate: true);
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
        DestButton? pick = _hoverDestination is { Node.Disabled: false } hover && hover.Node.IsVisibleInTree() ? hover
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
        for (int i = 0; i < _routeDots3d.Count; i++)
        {
            _routeDots3d[i].Visible = i < need;
            if (i >= need) continue;
            float at = i * gap + phase;
            _routeDots3d[i].Position = Map3DLayout.Along(points, at) + new Vector3(0, 0.24f, 0);
            float edge = Mathf.Clamp(Math.Min(at, length - at) / 0.8f, 0.2f, 1f);
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
            _noticesOff = true;   // 第478期: 検査の場面は器具が進めるので、出来事の窓は出さない
            SceneStart3D();
            await Settle();
            Check("開始時");
            if (_layout3d!.Pan is not null) { await Wide(); goto done; }   // 第477期: 1 画面に入らない盤（1 章の 1-2）は別の 4 場面
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
        }
        catch (Exception e) { fails.Add("例外:" + e.Message); GD.PrintErr(e.ToString()); }
        done:
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
                            fails.Add($"{scene}: {mine[i].Name} が {others[j].Name} に重なる（{kind}）");
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
