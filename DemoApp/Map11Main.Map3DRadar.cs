using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 第477期 §4-1 ／ §4-3: 盤が 1 画面に入らないステージ（1 章の 1-2）の「画面の外で起きること」の知らせ。
//   - 画面の端の矢印の印（歩いてくる敵・勇者・開いた門・拠点）——押すとそこへ視点が動く
//   - 小さな全体図（道・拠点・隊と敵の点・いま見えている枠）——押すとそこへ動く
//   - 歩いてくる敵の進路の赤い点線（選んだ隊の金の点線と同じ作り）
//   - 出来事で時計が止まったとき、その場所が画面の外なら視点を寄せる
// **判定は 1 つも持たない**——読むのは `Map11State` の今の盤だけ。3D の層（`Map3DMode.Active`）でだけ働く。
public partial class Map11Main
{
    private Map3DRadar? _radar3d;
    private readonly List<MeshInstance3D> _foeDots3d = new();
    private bool _seenGate3d, _seenHero3d;
    private int _seenSpawns3d;

    private void BuildRadar3D()
    {
        if (_cam3d is null || _layout3d is null) return;
        _seenGate3d = St.Stage.PortalGate > 0 && St.GateOpen;
        _seenHero3d = St.Hero is not null;
        _seenSpawns3d = St.SpawnCount;
        var layer = new CanvasLayer { Layer = 6, Name = "Map3DRadarLayer" };
        AddChild(layer);
        _radar3d = new Map3DRadar(this, _cam3d, _layout3d.Bounds, showMap: _layout3d.Pan is not null);
        layer.AddChild(_radar3d);
    }

    /// <summary>歩いてくる敵（拠点までの刻が読める敵・抜いていない）。全体図・矢印・赤い点線が同じものを読む。</summary>
    internal IEnumerable<(Map11State.Node Node, int Turns)> WalkingFoes3D()
    {
        for (int road = 0; road < St.Stage.LaneCount; road++)
            foreach (Map11State.Node n in St.FoesOn(road))
                if (!n.Cleared && St.TurnsToHome(n) is { } t && StuckBehind(n) is null) yield return (n, t);
    }

    /// <summary>次に拠点へ着く敵までの刻（「（次の敵は あと n 刻で着く）」・歩いてくる敵がいなければ空・第477期 §4）。</summary>
    private string NextArrivalText()
        => WalkingFoes3D().Select(x => (int?)x.Turns).Min() is { } t ? $"（次の敵は あと {t} 刻で着く）" : "";

    internal Vector3 RadarPos(int cell) => PosOf(cell);
    /// <summary>印を重ねない四角（いま画面にあるマスの台と名札）。</summary>
    internal IEnumerable<Rect2> RadarAvoid() => _discRects3d.Concat(_plateRects3d).Concat(_signRects3d).Select(x => x.Rect);
    internal Map11State RadarState => St;
    internal Camera3D RadarCamera => _camera;

    /// <summary>毎フレーム（`ProcessMap3D` から）: 知らせの層の出し入れと、歩いてくる敵の進路の赤い点線。</summary>
    private void ProcessRadar3D(bool shown)
    {
        if (_radar3d is null || _layout3d is null || _map3d is null) return;
        // 窓（遭遇・迎撃・連戦・結果・確かめ・頭の札・投資の札）が出ているあいだは隠す——窓の暗幕の上に全体図と印だけが明るく残るため。
        // 第479期: 遭遇・迎撃・連戦は横の札なので、盤も全体図も見せたまま。
        shown &= !CoverShown;
        _radar3d.Visible = shown;
        var points = new List<List<Vector3>>();
        if (shown && !St.Finished)
            foreach (var g in WalkingFoes3D().GroupBy(x => x.Node.Def.Road))
            {
                // 経路ごとに、いちばん奥から歩いてくる敵の道を 1 本（手前の敵の道はその一部）。
                Map11State.Node far = g.OrderByDescending(x => x.Turns).First().Node;
                List<int> cells = St.Graph.Path(far.Pos, far.Def.Road, St.Graph.Home, -1);
                if (cells.Count > 1) points.Add(_layout3d.Trace(cells));
            }
        const float gap = 1.5f;
        int used = 0;
        float phase = _clock3d * 1.2f % gap;
        foreach (List<Vector3> line in points)
        {
            float length = Map3DLayout.LengthOf(line);
            for (float at = phase; at < length - 2.6f && used < 160; at += gap, used++)
            {
                if (used >= _foeDots3d.Count)
                {
                    var dot = new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.15f, Height = 0.05f, RadialSegments = 10 },
                        MaterialOverride = new StandardMaterial3D
                        {
                            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = Color.FromHtml("#ff5a4a"),
                            EmissionEnabled = true, Emission = Color.FromHtml("#ff5a4a"), EmissionEnergyMultiplier = 1.4f,
                        },
                        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    };
                    _map3d.AddChild(dot);
                    _foeDots3d.Add(dot);
                }
                _foeDots3d[used].Visible = true;
                _foeDots3d[used].Scale = _homeWarn3d ? new Vector3(2.1f, 1, 2.1f) : Vector3.One;   // 第479期: 拠点が空になるなら、歩いてくる敵の点線を太く
                // 道の脇（味方の金の点線と重ならない側）を流れる。
                _foeDots3d[used].Position = Map3DLayout.Along(line, at) + new Vector3(0, 0.22f, 0.42f);
            }
        }
        for (int i = used; i < _foeDots3d.Count; i++) _foeDots3d[i].Visible = false;
    }

    /// <summary>
    /// 出来事で時計が止まったとき、その場所が画面の外なら視点を寄せる（`TickOnce` が 1 刻ごとに呼ぶ・3D の層の外では何もしない）。
    /// 迎撃・陥落 → 拠点 ／ 門が開いた・勇者が現れた・湧いた → 敵の砦 ／ 接敵・本陣の連戦 → その隊のいる所。
    /// </summary>
    private void FocusEvent3D(Map11Session.StepKind kind)
    {
        if (_cam3d is null || _layout3d is null || _radar3d is null) return;
        bool gate = St.Stage.PortalGate > 0 && St.GateOpen, hero = St.Hero is not null;
        Vector3? at = null;
        if (kind is Map11Session.StepKind.Intercept or Map11Session.StepKind.Fallen) at = PosOf(St.Graph.Home);
        else if (kind is Map11Session.StepKind.Battle or Map11Session.StepKind.Siege)
        {
            int sq = Enumerable.Range(0, St.SquadCap).FirstOrDefault(i => St.Squads[i].Units is { Count: > 0 } && !St.Squads[i].Lost && St.FoeFacing(i) is { Cleared: false }, -1);
            if (sq >= 0) at = PosOf(St.Squads[sq].Pos);
        }
        else if (St.Graph.HasPortal && ((gate && !_seenGate3d) || (hero && !_seenHero3d) || St.SpawnCount > _seenSpawns3d)) at = PosOf(St.Graph.Portal);
        _seenGate3d = gate; _seenHero3d = hero; _seenSpawns3d = St.SpawnCount;
        if (at is { } p && !_cam3d.Seen().Grow(-4f).HasPoint(new Vector2(p.X, p.Z))) _cam3d.FocusOn(p);
    }
}

/// <summary>画面の端の矢印の印と小さな全体図（第477期）。<b>描くだけ</b>——押されたら視点を動かす。</summary>
public partial class Map3DRadar : Control
{
    private readonly Map11Main _main;
    private readonly Map3DCamera _cam;
    private readonly Rect2 _bounds;
    private readonly bool _showMap;
    private readonly List<(Rect2 Rect, Vector3 World)> _hits = new();
    private static readonly Color Ink = Color.FromHtml("#f2ead8"), Back = new(0.04f, 0.05f, 0.06f, 0.86f), Line = Color.FromHtml("#8a7a56"),
        Mine = Color.FromHtml("#5ee0d0"), Foe = Color.FromHtml("#ff5a4a"), Gold = Color.FromHtml("#ffd874"), HeroInk = Color.FromHtml("#d08cff");

    internal Map3DRadar(Map11Main main, Map3DCamera cam, Rect2 bounds, bool showMap)
    {
        _main = main; _cam = cam; _bounds = bounds; _showMap = showMap;
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    /// <summary>全体図の四角（左下・隊の札の上）。</summary>
    private Rect2 MapRect()
    {
        float w = 330f, h = w * _bounds.Size.Y / Math.Max(1f, _bounds.Size.X);
        return new Rect2(22, GetViewportRect().Size.Y - 216 - h, w, h);   // 第479期: 隊の札の上の小さなボタンの列（高さ 40）のぶん上げた   // 左下（隊の札の上）——右上は金と敵の砦の札、左上は題字が使っている
    }

    /// <summary>その場所の全体図の上の点（画面の座標・遊ぶ口が全体図を押すのに使う）。</summary>
    internal Vector2 MapPoint(Vector3 world) => OnMap(MapRect(), world.X, world.Z);

    private Vector2 OnMap(Rect2 map, float x, float z)
        => map.Position + new Vector2((x - _bounds.Position.X) / _bounds.Size.X * map.Size.X, (z - _bounds.Position.Y) / _bounds.Size.Y * map.Size.Y);

    public override void _Process(double delta) => QueueRedraw();

    public override void _Input(InputEvent e)
    {
        if (!IsVisibleInTree() || e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        foreach (var (rect, world) in _hits)
            if (rect.HasPoint(click.Position)) { _cam.FocusOn(world); GetViewport().SetInputAsHandled(); return; }
        if (!_showMap) return;
        Rect2 map = MapRect();
        if (!map.HasPoint(click.Position)) return;
        Vector2 t = (click.Position - map.Position) / map.Size;
        _cam.FocusOn(new Vector3(_bounds.Position.X + t.X * _bounds.Size.X, 0, _bounds.Position.Y + t.Y * _bounds.Size.Y));
        GetViewport().SetInputAsHandled();
    }

    public override void _Draw()
    {
        _hits.Clear();
        Map11State st = _main.RadarState;
        MapGraph g = st.Graph;
        Font font = ThemeDB.FallbackFont;
        var walking = _main.WalkingFoes3D().ToList();

        if (_showMap)
        {
            Rect2 map = MapRect();
            DrawRect(map.Grow(6), Back);
            DrawRect(map.Grow(6), Line, false, 2);
            Vector2 P(int cell) { Vector3 w = _main.RadarPos(cell); return OnMap(map, w.X, w.Z); }
            foreach ((int a, int b) in g.Edges) DrawLine(P(a), P(b), g.IsCross(a, b) ? Gold.Darkened(0.2f) : Line, 2.5f);
            foreach (MapGraph.Cell c in g.Cells)
            {
                Vector2 p = P(c.Id);
                switch (c.Kind)
                {
                    // 第479期 §5: 味方の拠点 ＝ 青緑の四角に旗 ／ 敵の拠点 ＝ 赤い砦（凹みのある四角）／ 中間拠点 ＝ 菱形（取っていなければ灰）。
                    case MapGraph.Kind.Home:
                        DrawRect(new Rect2(p - new Vector2(8, 8), new Vector2(16, 16)), Mine);
                        DrawRect(new Rect2(p - new Vector2(8, 8), new Vector2(16, 16)), Ink, false, 1.5f);
                        DrawLine(p + new Vector2(-8, -8), p + new Vector2(-8, -19), Ink, 1.5f);
                        DrawColoredPolygon(new[] { p + new Vector2(-8, -19), p + new Vector2(2, -15.5f), p + new Vector2(-8, -12) }, Mine);
                        break;
                    case MapGraph.Kind.Portal:
                        Color fort = st.Captured ? Mine : Foe;
                        DrawColoredPolygon(new[] { p + new Vector2(-9, 8), p + new Vector2(-9, -9), p + new Vector2(-4, -9), p + new Vector2(-4, -4), p + new Vector2(4, -4), p + new Vector2(4, -9), p + new Vector2(9, -9), p + new Vector2(9, 8) }, fort);
                        DrawPolyline(new[] { p + new Vector2(-9, 8), p + new Vector2(-9, -9), p + new Vector2(-4, -9), p + new Vector2(-4, -4), p + new Vector2(4, -4), p + new Vector2(4, -9), p + new Vector2(9, -9), p + new Vector2(9, 8), p + new Vector2(-9, 8) }, Ink, 1.5f);
                        break;
                    case MapGraph.Kind.Outpost:
                        // 第478期: 中間拠点は菱形（拠点・敵の拠点の四角、ふつうのマスの点と形で見分ける）。
                        var diamond = new[] { p + new Vector2(0, -8), p + new Vector2(8, 0), p + new Vector2(0, 8), p + new Vector2(-8, 0) };
                        DrawColoredPolygon(diamond, st.HeldOutposts.Contains(c.Id) ? Mine : new Color(0.60f, 0.63f, 0.65f));
                        DrawPolyline(diamond.Append(diamond[0]).ToArray(), Ink, 1.5f);
                        break;
                    default: DrawCircle(p, 2f, Line); break;
                }
            }
            for (int road = 0; road < st.Stage.LaneCount; road++)
                foreach (Map11State.Node n in st.FoesOn(road))
                    if (!n.Cleared) DrawCircle(P(n.Pos) + new Vector2(0, -5), n.IsHero ? 6f : 4.5f, n.IsHero ? HeroInk : Foe);
            foreach (var (n, _) in walking) DrawArc(P(n.Pos) + new Vector2(0, -5), 7.5f, 0, Mathf.Tau, 16, Ink, 1.5f);
            for (int i = 0; i < st.SquadCap; i++)
                if (st.Squads[i] is { Lost: false, Units: { } us } && us.Any(u => u.IsAlive))
                {
                    Vector2 p = P(st.Squads[i].Pos) + new Vector2(-6 + i * 4, 6);
                    DrawCircle(p, 5f, Mine);
                    DrawArc(p, 5f, 0, Mathf.Tau, 12, Back, 1.5f);
                }
            // いま見えている枠
            Rect2 seen = _cam.Seen();
            Vector2 s0 = OnMap(map, seen.Position.X, seen.Position.Y), s1 = OnMap(map, seen.End.X, seen.End.Y);
            Rect2 frame = new Rect2(s0, s1 - s0).Intersection(map.Grow(4));
            if (frame.Size.X > 4 && frame.Size.Y > 4) DrawRect(frame, Ink, false, 2);
        }

        // ---- 画面の端の矢印の印 ----
        Camera3D cam = _main.RadarCamera;
        Vector2 view = GetViewportRect().Size;
        Rect2 safe = new Rect2(70, 130, view.X - 140, view.Y - 130 - 190);
        var notes = new List<(Vector3 World, string Text, Color Color)>();
        foreach (var (n, t) in walking.OrderBy(x => x.Turns).Take(3))
            notes.Add((_main.RadarPos(n.Pos), $"{n.Def.Name}　拠点まで あと {t} 刻", Foe));
        if (st.Hero is { Cleared: false } h) notes.Add((_main.RadarPos(h.Pos), "勇者", HeroInk));
        if (g.HasPortal && st.Stage.PortalGate > 0 && !st.Captured)
            notes.Add((_main.RadarPos(g.Portal), st.GateOpen ? "砦の門が開いた" : $"砦の門　中間拠点 {st.HeldOutposts.Count} ／ {st.Stage.PortalGate}", Gold));
        notes.Add((_main.RadarPos(g.Home), "拠点", Mine));
        // 右上は敵の砦の札（今の画面の札）が端に張り付く場所なので、印はその下から並べる。
        var placed = new List<Rect2> { new(view.X - 420, 0, 420, 410) };
        foreach (var (world, text, color) in notes)
        {
            bool behind = cam.IsPositionBehind(world);
            Vector2 at = cam.UnprojectPosition(world);
            if (!behind && new Rect2(0, 0, view.X, view.Y).Grow(-40).HasPoint(at)) continue;   // 画面に見えていれば出さない
            Vector2 centre = view / 2, dir = (behind ? centre - at : at - centre).Normalized();
            if (dir == Vector2.Zero) dir = Vector2.Right;
            Vector2 edge = new(Mathf.Clamp(at.X, safe.Position.X, safe.End.X), Mathf.Clamp(at.Y, safe.Position.Y, safe.End.Y));
            Vector2 size = font.GetStringSize(text, HorizontalAlignment.Left, -1, 22) + new Vector2(28, 14);
            Vector2 corner = new(Mathf.Clamp(edge.X - (dir.X > 0 ? size.X : 0), 12, view.X - size.X - 12), Mathf.Clamp(edge.Y - size.Y / 2, safe.Position.Y, safe.End.Y));
            var box = new Rect2(corner, size);
            // 画面にあるマスの台・名札の上には載せない（端のマスが印に隠れて押せなくなる）。下へずらし、下が詰まれば上へ。
            var avoid = _main.RadarAvoid().Where(r => new Rect2(0, 0, view.X, view.Y).Intersects(r)).ToList();
            bool Busy(Rect2 r) => placed.Any(q => q.Intersects(r)) || avoid.Any(q => q.Intersects(r.Grow(6)));
            Rect2 home0 = box;
            for (int k = 0; k < 10 && Busy(box) && box.End.Y + size.Y + 6 < safe.End.Y + size.Y; k++) box.Position += new Vector2(0, size.Y + 6);
            if (Busy(box)) { box = home0; for (int k = 0; k < 10 && Busy(box) && box.Position.Y - size.Y - 6 > safe.Position.Y - size.Y; k++) box.Position -= new Vector2(0, size.Y + 6); }
            if (_main.SideRects().Any(r => r.Intersects(box.Grow(20)))) continue;   // 第479期: 横の札（遭遇・連戦・迎撃）の上には出さない
            placed.Add(box);
            DrawRect(box, Back);
            DrawRect(box, color, false, 2);
            DrawString(font, box.Position + new Vector2(14, size.Y - 12), text, HorizontalAlignment.Left, -1, 22, Ink);
            // 矢じり（指す向き）
            Vector2 tip = new(dir.X > 0 ? box.End.X + 16 : box.Position.X - 16, box.Position.Y + size.Y / 2);
            Vector2 baseAt = new(dir.X > 0 ? box.End.X + 2 : box.Position.X - 2, tip.Y);
            if (Mathf.Abs(dir.X) < 0.35f) { tip = new Vector2(box.Position.X + size.X / 2, dir.Y > 0 ? box.End.Y + 14 : box.Position.Y - 14); baseAt = new Vector2(tip.X, dir.Y > 0 ? box.End.Y + 2 : box.Position.Y - 2); }
            Vector2 wing = (tip - baseAt).Orthogonal().Normalized() * 9;
            DrawColoredPolygon(new[] { tip, baseAt + wing, baseAt - wing }, color);
            _hits.Add((box.Grow(8), world));
        }
    }
}
