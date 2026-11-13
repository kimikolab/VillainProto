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
        _cam3d.Setup(St.Stage.Id, _layout3d.Look, _layout3d.Reach);
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
            float radius = c.Kind switch { MapGraph.Kind.Home => 2.4f, MapGraph.Kind.Portal => 2.2f, MapGraph.Kind.Outpost => 1.8f, _ => 1.4f };
            var mark = new Map3DCellMark(radius) { Position = PosOf(c.Id) };
            _map3d.AddChild(mark);
            _cellMarks3d[c.Id] = mark;
        }
        if (!St.Portal.On) return;
        _portalMarker = Hidden(PortalPos, EnemyColor, 2.2f);
        // 敵の拠点へは経路ごとに入る（北から ／ 南から）ので、本陣の手前に経路ごとの小さな台を置く。
        for (int lane = 0; lane < St.Graph.Lanes.Count; lane++)
        {
            var mark = new Map3DCellMark(0.95f) { Position = _layout3d.Approach[lane] };
            _map3d.AddChild(mark);
            _approachMarks3d.Add(mark);
        }
        // 表の無いステージ（1-2 ／ 1-3）の中間拠点は今の絵と名札のまま。
        foreach (MapGraph.Cell c in St.Graph.Cells.Where(c => c.Kind == MapGraph.Kind.Outpost))
        {
            Vector3 at = PosOf(c.Id);
            AddOutpostArt(c.Id);
            Label3D name = WorldLabel(c.Name, new Vector3(at.X, 0.6f, at.Z + 4.2f), 30, EnemyColor);
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
        Bounds(_foeDrawer, -354, 150, -20, -224, anchorLeft: 1, anchorRight: 1, anchorBottom: 1);
        Bounds(_historyDrawer, -640, 150, -20, -224, anchorLeft: 1, anchorRight: 1, anchorBottom: 1);
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
                piece.Show(new Map3DPieceInfo(FoeBadgeName(foe), Representative(foe)?.Id, true, foe.IsHero,
                    ((MapUnitBadge)pick.Button).Count, NodeHpPercent(foe), pick.Marker.Note, pick.Marker.NoteColor,
                    pick.Marker.Note.Length > 0 && pick.Marker.NoteColor == UiKit.Hurt, WaveMark(foe.Def.Name)));
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
        for (int lane = 0; lane < _approachMarks3d.Count; lane++)
            _approachMarks3d[lane].SetOwner(squadsOn.ContainsKey((St.Graph.Portal, lane)) ? Map3DCellMark.Owner.Ally : Map3DCellMark.Owner.Empty);

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
        string title = (Chapter1.Active ? "1 章　" : "") + place + (St.Stage.FoeScale.Active ? $"　{St.Stage.FoeScale.BarText}" : "");
        string turn = !St.Time.On ? ""
            : $"作戦ターン {Map11Session.ShownTurn}" + (St.TimeLimitLeft is { } limit ? $"　／　期限まで あと {limit}" : "");
        string goal = St.HeroClock && St.Hero is { Cleared: false } hero ? $"目標　勇者を倒す（HP {hero.HpNow}）"
            : !St.Portal.On ? $"目標　敵部隊をすべて抜く（{St.ClearedCount} / {St.Stage.TotalNodes}）"
            : St.Captured ? $"目標　残る敵部隊を倒す（あと {left}）"
            : $"目標　{Map11.PortalName}を制圧する　・　残る敵部隊 {left}";
        _hud3d.SetHeader(title, turn, goal);
        _hud3d.SetGold(St.Stage.Gold.On ? St.Gold : null);
        _hud3d.SetWindows(_showFoes, _showHistory);

        var cards = new List<Map3DSquadCardInfo>();
        foreach (Map11State.Squad s in St.Squads)
        {
            bool locked = St.IsLocked(s.Index);
            var faces = s.Lost || locked ? new List<UnitDef>()
                : s.Units is { } units ? units.Where(u => u.IsAlive).OrderBy(u => u.Slot).Select(u => u.Def).ToList()
                : s.Def.F.Occupied().OrderBy(u => u.Slot).Select(u => u.Def).ToList();
            string where = s.Pos == St.Graph.Home ? "拠点" : s.Pos == St.Graph.Portal ? Map11.PortalName : PlaceName(s.Pos);
            cards.Add(new Map3DSquadCardInfo(s.Index, s.Label, s.Role, faces, faces.Count, faces.Count == 0 ? 0 : SquadHpPercent(s),
                where, St.Time.On ? "→ " + OrderText(s.Index) : "", s.Index == _selected && _guideSquadChosen, s.Lost, locked,
                locked ? $"枠を開ける: 金 {St.SlotPriceFor(s.Index)}" : ""));
        }
        _hud3d.SetSquads(cards);
        bool can = _selected >= 0 && _selected < St.Squads.Length && St.CanReform(_selected) && !St.Finished && !Map11Session.TurnRunning;
        _hud3d.SetReform("編成・呼ぶ・強化", can, _showSquadDetail ? "配置を閉じる" : "配置を見る");
    }

    // =================================================================================
    // 毎フレーム（位置を追うだけ）
    // =================================================================================

    private void ProcessMap3D(double delta)
    {
        if (_hud3d is null || _cam3d is null || _layout3d is null) return;
        _clock3d += (float)delta;
        FitOldUi();
        // 今の画面の上の帯・見出し・出来事の 1 行は、今の窓（編成）を開いているあいだだけ出す。作戦の画面では四隅の UI が持つ。
        if (_headerBand is not null) _headerBand.Visible = _reform;
        _headline.Visible = _toast.Visible = _reform;
        if (_portalInfo is not null) _portalInfo.Visible = false;
        _hud3d.SetShown(!_reform);
        if (_reform) return;

        _hud3d.SetToast(_toast.Text, _toast.GetThemeColor("font_color"));
        bool on = Map11Clock.On;
        string state = Map11Session.TurnRunning ? "戦闘中" : !on ? "出来事が起きるまで進む" : Map11Clock.Running ? "▶ 動いています" : "⏸ 止まっています";
        _hud3d.SetClock(on && Map11Clock.Running ? "⏸ 止める" : "▶ 刻を進める", _run.Disabled, on && St.Time.On, Map11Clock.Fast,
            Map11Clock.Fraction, on ? $"速さ {(Map11Clock.Fast ? "2×" : "1×")}　・　{state}" : state);
        RefreshSelection3D();

        // ---- 行ける先のマスを光らせ、「あと n」を出す（行けるかどうかは今の画面の行き先の札が押せるかだけを読む） ----
        Map11State.Squad sel = St.Squads[_selected];
        bool chosen = _guideSquadChosen && !sel.Lost && !St.Finished;
        Dest now = Map11Session.Destinations[_selected];
        foreach (DestButton d in _destButtons)
        {
            Map3DCellMark mark = d.Dest.IsPortal ? _approachMarks3d[Math.Clamp(d.Dest.Road, 0, _approachMarks3d.Count - 1)] : _cellMarks3d[d.Dest.Id];
            bool there = St.AtDest(_selected, d.Dest);
            bool reach = chosen && d.Node.Visible && !d.Node.Disabled && !there;
            bool goal = chosen && now == d.Dest && !there && !sel.Lost;
            mark.SetReach(reach, goal);
            // 押す領域（今の窓の層の座標）。
            Vector2 center = _camera.UnprojectPosition(mark.GlobalPosition);
            float half = Math.Max(36, _camera.UnprojectPosition(mark.GlobalPosition + new Vector3(mark.Radius * 1.08f, 0, 0)).X - center.X);
            var region = new Rect2(center.X - half, center.Y - half * 0.66f, half * 2, half * 1.32f);
            d.Node.Position = region.Position / _uiScale;
            d.Node.Size = region.Size / _uiScale;
            // 「あと n」の札（選んだ隊がそこまで歩く作戦ターン・今の 2D と同じ数）。
            if (!_chips3d.TryGetValue(d, out Label? chip))
            {
                _chips3d[d] = chip = Map3DHud.Text("", 22, Colors.White, bold: true);
                chip.HorizontalAlignment = HorizontalAlignment.Center;
                _hud3d.Plates.AddChild(chip);
            }
            chip.Visible = reach || goal;
            if (!chip.Visible) continue;
            string eta = TurnsToDest(_selected, d.Dest) is { } steps ? $"あと {steps}" : "";
            bool hover = ReferenceEquals(_hoverDestination, d);
            string where = d.Dest.AtHome ? "拠点" : d.Dest.IsPortal ? $"{Map11.PortalName}（{St.Stage.LaneMark(d.Dest.Road)}から）" : PlaceName(d.Dest.Id);
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
            chip.Position = (center - chip.Size / 2 + new Vector2(0, -2)).Round();
        }
        ProcessRoute3D(chosen, sel);

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

        // ---- いま何をすればよいかの 1 行（左下・隊の札の上） ----
        string hint = St.Finished || Map11Session.TurnRunning ? ""
            : !_guideSquadChosen ? "隊の札か、盤の上の駒を押して隊を選ぶ"
            : !St.CanSend(_selected) ? (St.CanReform(_selected) ? "この隊は空席——「編成・呼ぶ・強化」で駒を配る" : "")
            : _hoverDestination is null && !_guideDestinationChosen ? "光っているマスを押すと行き先が決まる（右ドラッグ: 横移動 ／ ホイール: 拡大）"
            : _orderHint.Text;
        _hud3d.SetHint(hint);
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
        if (_introOverlay is not null) _introOverlay.Visible = false;
        if (_investOverlay is not null) _investOverlay.Visible = false;
        if (Map11Session.Drafting && St.Bench.Count > 0)
        {
            // 1 章は手札から自分で組む（既定の組み方が無い）ので、使える隊へ前の席から順番に配る。単発の盤は既定の組み方。
            if (Chapter1.Active)
            {
                int[] open = Enumerable.Range(0, St.Squads.Length).Where(i => !St.IsLocked(i)).Take(2).ToArray();
                for (int k = 0; St.Bench.Count > 0 && k < open.Length * FormationRules.PlayableSlots.Length; k++)
                    St.SwapWithBench(open[k % open.Length], FormationRules.PlayableSlots[k / open.Length], 0);
            }
            else St.UseDefaultSquads();
        }
        Map11Session.TakeOpening();
        _reform = false;
        ClearHold();
        _guideSquadChosen = _guideDestinationChosen = false;
        Refresh();
        if (mode == "m3d-click") { await Map3DClickCheck(); return; }
        // 今の窓を開いたところ（重なりの確かめ用）: `m3d-detail` は隊の配置、`m3d-foes` は敵部隊の一覧。
        if (mode is "m3d-detail" or "m3d-foes")
        {
            Select(Enumerable.Range(0, St.Squads.Length).FirstOrDefault(St.CanSend, 0));
            _showSquadDetail = mode == "m3d-detail";
            _showFoes = mode == "m3d-foes";
            Refresh();
        }
        else if (mode != "m3d-start")
        {
            int[] movers = Enumerable.Range(0, St.Squads.Length).Where(St.CanSend).Take(2).ToArray();
            // 第1隊は北の道を奥へ。第2隊は `m3d-moved` では南の道の手前まで出て、`m3d-camp` では拠点で番をする（南から来る敵を迎え撃つ）。
            for (int k = 0; k < movers.Length; k++)
                Map11Session.SetDestination(movers[k], k == 0 ? Dest.Deep(St.Graph, 0)
                    : mode == "m3d-camp" ? Dest.Home : Dest.Of(St.Graph, St.Graph.Lanes[^1][0]));
            bool camp = mode == "m3d-camp";
            bool Near() => movers.Any(m => !St.Squads[m].Lost && St.Graph.Lanes.Any(l => l[^1] == St.Squads[m].Pos));
            for (int turn = 0; turn < (camp ? 14 : 2) && !St.Finished && !(camp && Near()); turn++)
            {
                HeadlessTurn3D();
                Refresh();
                await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);   // 駒が歩き終えるのを待つ（絵を撮る通しでも歩かせる）
            }
            int pick = movers.Where(m => !St.Squads[m].Lost).OrderByDescending(m => camp && St.Graph.Lanes.Any(l => l[^1] == St.Squads[m].Pos) ? 1 : 0)
                .DefaultIfEmpty(0).First();
            Select(pick);
            if (camp && St.Portal.On && !St.Squads[pick].Lost)
                Map11Session.SetDestination(pick, Dest.Portal(St.Graph, Math.Max(0, St.Squads[pick].Road)));
            _showSquadDetail = false;
            _guideDestinationChosen = camp;
            ShowToast("", GoldColor, 0);
            Refresh();
        }
        GD.Print($"MAP3D_CAPTURE mode={mode} stage={St.Stage.Id} seed={Map11Session.Seed} turn={St.Turn} battles={St.Battles} "
            + "squads=" + string.Join("/", St.Squads.Select(s => s.Lost ? "lost" : PlaceName(s.Pos))) + $" finished={St.Finished}");
        await ToSignal(GetTree().CreateTimer(1.6), SceneTreeTimer.SignalName.Timeout);   // 影と素材の読み込みが落ち着くのを待つ
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
