using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 作戦マップの盤 ＝ マスのグラフ（第438期 段A1）
//
// **規則は1つも持たない。** ここにあるのは「どのマスがどこに繋がっているか」と
// 「敵の経路（Lane）がどのマスを通るか」と「画面のどこに置くか」だけ。
// 進行の規則は `Map11State` に残り、ここは位置の持ち方だけを引き受ける。
// **ここには Godot の型を1つも使わない**（`Map11.cs` と同じ扱い・頭なしの門が同じクラスを回す）。
//
// 1-1（`Build11`）は第437期までの「北の道・南の道 × 番号」を**そのまま写した**盤で、新しいマスは無い:
//
//   Home ── N0 ── N1 ── N2 ──┐
//                             ├── Portal   （portalOn のときだけ存在）
//   Home ── S0 ── S1 ── S2 ──┘
//
// 拠点の扱い（第438期の報告 §3）:
//   - **味方の拠点（Home）は経由できる**——1 歩で入り、次の 1 歩で出る（第437期の「別の道ならいったん拠点まで戻る」がそれ）。
//   - **敵の拠点（Portal）は経由できない**——入って止まるか、入ってきた経路へ出るだけ。
//     1-1 では北と南がここで合流するので、**どの経路で入ったか（`via`）を位置と一緒に持つ**
//     （第437期の `Road` がそこで担っていた役目。行き先の「敵の拠点（北）／（南）」も同じ）。
// =====================================================================================

/// <summary>作戦マップの盤（マスのグラフ）。<b>静的な内容だけ</b>で、進行の状態は <see cref="Map11State"/> が持つ。</summary>
public sealed class MapGraph
{
    /// <summary>マスの種類。<b>Outpost（中間拠点・第444期）は経路の途中のマス</b>で、通り抜けられる（拠点・敵の拠点とは違う）。</summary>
    public enum Kind { Home, Open, Portal, Outpost }

    /// <summary>
    /// マス1つ。<c>Lane</c> はそのマスが属する敵の経路（属さなければ -1）、
    /// <c>Pos</c> は画面に置く座標（x, z）——<b>データとして持つだけ</b>（Godot の型にしない）。
    /// </summary>
    public sealed record Cell(int Id, string Name, Kind Kind, IReadOnlyList<int> Neighbors, int Lane, (float X, float Z) Pos);

    /// <summary>味方の拠点の Id。<b>どの盤でも 0</b>（<see cref="Dest.Home"/> が盤を引かずに作れるように）。</summary>
    public const int HomeId = 0;

    public IReadOnlyList<Cell> Cells { get; }
    public int Home => HomeId;
    /// <summary>敵の拠点の Id（無い盤では -1）。</summary>
    public int Portal { get; }
    /// <summary>敵の経路。<b>拠点の隣 → … → 敵の拠点の手前</b>のマス Id の列。</summary>
    public IReadOnlyList<int[]> Lanes { get; }
    /// <summary>経路の名前（画面の文字と同じ。1-1 は「北の道」「南の道」）。</summary>
    public IReadOnlyList<string> LaneNames { get; }
    /// <summary>最初からいる敵部隊の初期位置（経路ごと・区画の番号の順）。</summary>
    public IReadOnlyList<int[]> FoeStarts { get; }
    /// <summary>辺（足した順）。画面の盤の線はこの順に引く。</summary>
    public IReadOnlyList<(int A, int B)> Edges { get; }

    private MapGraph(IReadOnlyList<Cell> cells, int portal, IReadOnlyList<int[]> lanes, IReadOnlyList<string> laneNames,
                     IReadOnlyList<int[]> foeStarts, IReadOnlyList<(int, int)> edges)
    {
        Cells = cells; Portal = portal; Lanes = lanes; LaneNames = laneNames; FoeStarts = foeStarts; Edges = edges;
    }

    public Cell this[int id] => Cells[id];
    public bool HasPortal => Portal >= 0;
    public bool IsBase(int id) => Cells[id].Kind is Kind.Home or Kind.Portal;

    /// <summary>中間拠点のマスか（第444期）。</summary>
    public bool IsOutpost(int id) => Cells[id].Kind == Kind.Outpost;

    /// <summary>
    /// 経路 <paramref name="lane"/> の上の位置（第437期の「マスの番号」）。
    /// <b>拠点は -1、敵の拠点は経路の長さ</b>（1-1 では 3 ＝ 第437期の <c>Map11.PortalCell</c>）。経路の外は <see cref="int.MinValue"/>。
    /// </summary>
    public int IndexOn(int lane, int id)
    {
        if (id == HomeId) return -1;
        if (lane < 0) return int.MinValue;
        if (id == Portal) return Lanes[lane].Length;
        int i = Array.IndexOf(Lanes[lane], id);
        return i >= 0 ? i : int.MinValue;
    }

    /// <summary>経路 <paramref name="lane"/> の位置 <paramref name="index"/> のマス（-1 以下は拠点・経路の長さ以上は敵の拠点）。</summary>
    public int At(int lane, int index)
        => index < 0 ? HomeId
         : index >= Lanes[lane].Length ? (HasPortal ? Portal : throw new ArgumentOutOfRangeException(nameof(index)))
         : Lanes[lane][index];

    /// <summary>経路の上で 1 つ拠点寄りのマス（敵の前進の 1 歩・経路の先頭なら拠点）。</summary>
    public int TowardHome(int lane, int id) => At(lane, IndexOn(lane, id) - 1);

    /// <summary>
    /// <paramref name="from"/> から <paramref name="to"/> への最短路（幅優先・辺の重さは全部 1）。両端を含む。
    /// <b>敵の拠点は途中に使わない</b>。敵の拠点から出るときは <paramref name="fromVia"/> の経路へ、
    /// 敵の拠点へ入るときは <paramref name="toVia"/> の経路から（-1 ならどちらでも）。道が無ければ空。
    /// 同じ長さの道が複数あるときは、隣の並び（盤を作った順）で先に見つかったほうを返す。
    /// </summary>
    public List<int> Path(int from, int fromVia, int to, int toVia)
    {
        if (from == to && (to != Portal || toVia < 0 || fromVia == toVia)) return new List<int> { from };
        var prev = new Dictionary<int, int> { [from] = -1 };
        var queue = new Queue<int>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            int c = queue.Dequeue();
            foreach (int n in Exits(c, c == from ? fromVia : -1))
            {
                if (n == to)
                {
                    if (to == Portal && toVia >= 0 && c != Lanes[toVia][^1]) continue;
                    var path = new List<int>();
                    for (int x = c; x >= 0; x = prev[x]) path.Add(x);
                    path.Reverse();
                    path.Add(n);
                    return path;
                }
                if (prev.ContainsKey(n)) continue;
                if (n == Portal) continue;   // 敵の拠点は経由しない
                prev[n] = c;
                queue.Enqueue(n);
            }
        }
        return new List<int>();
    }

    /// <summary>最短路の次のマス（着いている・道が無いなら <paramref name="from"/>）。</summary>
    public int NextStep(int from, int fromVia, int to, int toVia)
    {
        var p = Path(from, fromVia, to, toVia);
        return p.Count >= 2 ? p[1] : from;
    }

    /// <summary>そのマスから出られる隣。<b>敵の拠点からは入ってきた経路へしか出られない。</b></summary>
    private IEnumerable<int> Exits(int id, int via)
        => id == Portal && via >= 0 ? new[] { Lanes[via][^1] } : Cells[id].Neighbors;

    // ---------------- 盤を組む ----------------

    /// <summary>盤を手で組むための小さな道具（1-1 と玩具の盤が使う）。</summary>
    public sealed class Builder
    {
        private readonly List<(string Name, Kind Kind, int Lane, (float, float) Pos)> _cells = new();
        private readonly List<List<int>> _nbr = new();
        private readonly List<(int, int)> _edges = new();
        private readonly List<int[]> _lanes = new();
        private readonly List<string> _laneNames = new();
        private readonly List<int[]> _starts = new();
        private int _portal = -1;

        /// <summary>最初に足すマスが拠点（Id 0）になる。</summary>
        public int Add(string name, Kind kind, (float X, float Z) pos, int lane = -1)
        {
            if (_cells.Count == 0 && kind != Kind.Home) throw new InvalidOperationException("最初のマスは味方の拠点");
            if (_cells.Count > 0 && kind == Kind.Home) throw new InvalidOperationException("味方の拠点は1つ");
            if (kind == Kind.Portal) { if (_portal >= 0) throw new InvalidOperationException("敵の拠点は1つ"); _portal = _cells.Count; }
            _cells.Add((name, kind, lane, pos));
            _nbr.Add(new List<int>());
            return _cells.Count - 1;
        }

        /// <summary>隣り合わせる（<b>両方向</b>）。</summary>
        public void Link(int a, int b)
        {
            if (a == b || _nbr[a].Contains(b)) return;
            _nbr[a].Add(b);
            _nbr[b].Add(a);
            _edges.Add((a, b));
        }

        public void Lane(string name, int[] cells, int[] foeStarts)
        {
            _laneNames.Add(name);
            _lanes.Add(cells);
            _starts.Add(foeStarts);
        }

        public MapGraph Build()
        {
            var cells = _cells.Select((c, i) => new Cell(i, c.Name, c.Kind, _nbr[i].ToArray(), c.Lane, c.Pos)).ToArray();
            return new MapGraph(cells, _portal, _lanes.ToArray(), _laneNames.ToArray(), _starts.ToArray(), _edges.ToArray());
        }
    }

    /// <summary>
    /// 1-1 の盤（第437期の道と番号を<b>そのまま</b>写す）。<c>Pos</c> は第437期の
    /// <c>Map11Main.BasePos</c> ／ <c>CellPos</c> ／ <c>PortalPos</c> と同じ値——画面は1ピクセルも動かない。
    /// <paramref name="portalOn"/> が偽なら敵の拠点のマスは作らない（第437期の <c>MaxCell</c>）。
    /// マスの名前は第437期の <c>Map11Orders.Where</c> と同じ文字列（「北の道 2 戦目の地点」など）。
    /// </summary>
    public static MapGraph Build11(bool portalOn) => Build11(portalOn, null);

    /// <summary>1-1 の盤に辺を書き足す口（玩具の盤 `MapGraphSmoke.ToyCross` だけが使う・経路と敵の初期位置は 1-1 のまま）。</summary>
    internal static MapGraph Build11(bool portalOn, Action<Builder, int[][]>? extra)
    {
        var b = new Builder();
        int home = b.Add("拠点", Kind.Home, (-4.0f, 2.0f));
        var lanes = new int[Map11.RoadCount][];
        for (int road = 0; road < Map11.RoadCount; road++)
        {
            float z = road == 0 ? -8.5f : 8.5f;
            lanes[road] = new int[Map11.RoadCells];
            for (int cell = 0; cell < Map11.RoadCells; cell++)
            {
                // 第437期の `CellPos`: マス 0 は拠点と1つ目の敵のあいだ、マス 1・2 は区画 0・1（`NodePos`）。
                float x = cell <= 0 ? -0.6f : 3.0f + (cell - 1) * 11.0f;
                lanes[road][cell] = b.Add($"{Map11.RoadNames[road]} {Map11Orders.CellName(cell)}", Kind.Open, (x, z), road);
            }
        }
        int portal = portalOn ? b.Add(Map11.PortalName, Kind.Portal, (22.0f, 0f)) : -1;
        // 辺は第437期の盤の線と同じ順（道ごとに 拠点 → マス 0 → 1 → 2 → 敵の拠点）。
        for (int road = 0; road < Map11.RoadCount; road++)
        {
            int from = home;
            foreach (int c in lanes[road]) { b.Link(from, c); from = c; }
            if (portal >= 0) b.Link(from, portal);
            b.Lane(Map11.RoadNames[road], lanes[road],
                   Enumerable.Range(0, Map11.Roads[road].Count).Select(i => lanes[road][Map11.StartCellOf(i)]).ToArray());
        }
        extra?.Invoke(b, lanes);
        return b.Build();
    }

    /// <summary>
    /// 1 章の 1-1 の盤・第474期（指示書 design/PHASE474_STAGE11_ASYM_SPEC.md §1・`ChapterRule.V474` だけが使う）。<b>道の長さが違う</b>:
    /// <code>
    ///              北1 ─── 北2
    ///             ／          ＼
    ///   拠点                    敵の拠点
    ///             ＼            ／
    ///      南1 ── 南2 ── 南3 ── 南4
    /// </code>
    /// 北の道は 2 マス（山越えの近道・拠点から敵の拠点まで 3 歩）、南の道は 4 マス（森と湿地の回り道・5 歩）。横の辺・中間拠点・門は無い。
    /// 敵の初期位置は <see cref="Starts11F474"/>（北 1・2 マス目 ／ 南 1・3 マス目・南の 2・4 マス目は空）。
    /// <c>Pos</c> は上下で対称にしない（北は短くまっすぐ・南は下へ膨らんで長い）。単発の 1-1（<see cref="Build11(bool)"/>）は変えない。
    /// </summary>
    public static MapGraph Build11F474(bool portalOn)
    {
        var b = new Builder();
        int home = b.Add("拠点", Kind.Home, (-6.0f, -1.0f));
        (float X, float Z)[][] pos =
        {
            new[] { (3.0f, -8.0f), (13.0f, -8.5f) },
            new[] { (-2.0f, 7.0f), (5.0f, 11.0f), (13.0f, 11.5f), (20.0f, 8.0f) },
        };
        var lanes = new int[pos.Length][];
        for (int lane = 0; lane < pos.Length; lane++)
            lanes[lane] = pos[lane].Select((p, k) => b.Add($"{Map11.RoadNames[lane]} {k + 1} マス目", Kind.Open, p, lane)).ToArray();
        int portal = portalOn ? b.Add(Map11.PortalName, Kind.Portal, (23.0f, -1.0f)) : -1;
        for (int lane = 0; lane < pos.Length; lane++)
        {
            int from = home;
            foreach (int c in lanes[lane]) { b.Link(from, c); from = c; }
            if (portal >= 0) b.Link(from, portal);
            b.Lane(Map11.RoadNames[lane], lanes[lane], Starts11F474[lane].Select(i => lanes[lane][i]).ToArray());
        }
        return b.Build();
    }

    /// <summary>第474期の 1 章の 1-1 の敵の初期位置（経路の上の位置・区画の番号の順）。北: 1 マス目・2 マス目 ／ 南: 1 マス目・3 マス目。</summary>
    internal static readonly int[][] Starts11F474 = { new[] { 0, 1 }, new[] { 0, 2 } };

    /// <summary>
    /// 辺 (a, b) が<b>経路をまたぐ横の辺</b>か（両端が別の経路の上のマス）。敵はこの辺を使わない
    /// ——敵の前進は <see cref="TowardHome"/>（経路に沿う）だけなので、規則として足すものは無い。画面が色を変えるためだけに読む。
    /// </summary>
    public bool IsCross(int a, int b) => Cells[a].Lane >= 0 && Cells[b].Lane >= 0 && Cells[a].Lane != Cells[b].Lane;

    /// <summary>1-2 の経路の名前（<see cref="MapStage.Stage12"/> と同じ並び）。</summary>
    internal static readonly string[] Lanes12 = { "北の道", "中の道", "南の道" };

    /// <summary>
    /// 1-2 の盤（第440期 §2）。経路 3 本（北・中・南）は 1-1 と同じ形（拠点 → 手前 → 1 戦目 → 2 戦目 → 敵の拠点）で、
    /// <b>横の辺 2 本（北1–中1・中1–南1）</b>を足した。<c>Pos</c> は 1-1 の並びを 3 段にしたもの（北を上・南を下）。
    /// マスの名前は 1-1 と同じ作り方（「中の道 1 戦目の地点」など）。
    /// </summary>
    public static MapGraph Build12(bool portalOn) => Build12(portalOn, midLead: Map11.StartCellOf(0));

    /// <summary>
    /// 1-2 の盤・第442期（§0-4）。<b>中の道の先頭（物見）だけが 1 マス奥（2 戦目の地点）</b>から始まる
    /// ——拠点まで 3 刻（北・南は 2 刻のまま）。2 戦目の地点には第二波・先遣もいるので、<b>同じマスに 2 部隊</b>で始まる
    /// （先頭だけが前進する規則なので、物見が出ていけば先遣は元のマスに残る・`Map11State.AdvanceFoes`）。
    /// </summary>
    public static MapGraph Build12F442(bool portalOn) => Build12(portalOn, midLead: Map11.StartCellOf(1));

    /// <summary>1-2 の中間拠点の名前（第444期・仮・Codex 発注書 §4 の仮名と揃えた）。経路の 3 マス目（2 戦目の地点）。</summary>
    internal static readonly string[] Outposts12 = { "北の砦", "浅瀬の野営地", "南の関所" };

    /// <summary>
    /// 1-2 の盤・第444期（§0-1）。<see cref="Build12F442"/> の各経路の <b>3 マス目（2 戦目の地点）を中間拠点</b>にした
    /// （<see cref="Kind.Outpost"/>・名前は <see cref="Outposts12"/>）。辺・座標・敵の初期位置は第442期のまま。
    /// </summary>
    public static MapGraph Build12F444(bool portalOn) => Build12(portalOn, midLead: Map11.StartCellOf(1), outposts: true);

    private static MapGraph Build12(bool portalOn, int midLead, bool outposts = false)
    {
        var b = new Builder();
        int home = b.Add("拠点", Kind.Home, (-10.0f, 0f));
        float[] zs = { -12.0f, 0f, 12.0f };
        var lanes = new int[Lanes12.Length][];
        for (int lane = 0; lane < Lanes12.Length; lane++)
        {
            lanes[lane] = new int[Map11.RoadCells];
            for (int cell = 0; cell < Map11.RoadCells; cell++)
            {
                float x = cell <= 0 ? -0.6f : 3.0f + (cell - 1) * 11.0f;
                bool outpost = outposts && cell == Map11.RoadCells - 1;
                lanes[lane][cell] = b.Add(outpost ? Outposts12[lane] : $"{Lanes12[lane]} {Map11Orders.CellName(cell)}",
                                          outpost ? Kind.Outpost : Kind.Open, (x, zs[lane]), lane);
            }
        }
        int portal = portalOn ? b.Add(Map11.PortalName, Kind.Portal, (24.0f, 0f)) : -1;
        for (int lane = 0; lane < Lanes12.Length; lane++)
        {
            int from = home;
            foreach (int c in lanes[lane]) { b.Link(from, c); from = c; }
            if (portal >= 0) b.Link(from, portal);
            // 敵の初期位置は 1-1 と同じ（1 戦目・2 戦目の地点）。第442期は中の道の先頭だけ <paramref name="midLead"/>。
            int lead = lane == 1 ? midLead : Map11.StartCellOf(0);
            b.Lane(Lanes12[lane], lanes[lane], new[] { lanes[lane][lead], lanes[lane][Map11.StartCellOf(1)] });
        }
        // 横の辺（隊だけが使う）。
        b.Link(lanes[0][1], lanes[1][1]);
        b.Link(lanes[1][1], lanes[2][1]);
        return b.Build();
    }

    /// <summary>第477期の 1 章の 1-2 の経路の名前（<see cref="MapStage.Stage12Ch477"/> と同じ並び・頭の 1 文字は 北 ／ 中 ／ 南 のまま）。</summary>
    internal static readonly string[] Lanes12F477 = { "北の山道", "中の街道", "南の湿地の道" };

    /// <summary>第477期の 1 章の 1-2 の中間拠点の名前（経路の順・仮）。北 3 マス目 ／ 中 2 マス目 ／ 南 4 マス目。</summary>
    internal static readonly string[] Outposts12F477 = { "見張り台", "関所", "渡し場" };

    /// <summary>第477期の 1 章の 1-2 の敵の初期位置（経路の上の位置・分かれ道が 0）。北: 見張り台(3) ／ 中: 先頭と 2 番手はどちらも 中3(3) ／ 南: 南2(2)・渡し場(4)。</summary>
    internal static readonly int[][] Starts12F477 = { new[] { 3 }, new[] { 3, 3 }, new[] { 2, 4 } };

    /// <summary>
    /// 1 章の 1-2 の盤・第477期（指示書 design/PHASE477_STAGE12_BOARD_SPEC.md §2・`ChapterRule.V477` だけが使う）。<b>道の長さが 3 本とも違う</b>:
    /// <code>
    ///                       ┌─ 北1 ─ 北2 ─[見張り台]─ 北4 ─┐
    ///                       │              ║               │
    ///   拠点 ── 分かれ道 ───┼─ 中1 ─[関所]─ 中3 ───────────┼── 敵の拠点
    ///                       │                              │
    ///                       └─ 南1 ─ 南2 ─ 南3 ─[渡し場]─ 南5 ─ 南6 ┘
    /// </code>
    /// 分かれ道から門まで 北 4 ／ 中 3 ／ 南 6 マス。拠点から中間拠点まで 関所 3 歩・見張り台 4 歩・渡し場 5 歩。<b>横の辺は 関所 ⇔ 見張り台 の 1 本だけ</b>（隊だけが使う）。
    /// 経路は 1-3 と同じく<b>分かれ道を先頭に含む</b>（分かれ道の <c>Lane</c> は -1）。<c>Pos</c> は上下で対称にしない（北は尾根へ上がり、南は大きく下へ膨らむ）。
    /// 単発の 1-2（<see cref="Build12F444"/>）は変えない。
    /// </summary>
    public static MapGraph Build12F477(bool portalOn)
    {
        var b = new Builder();
        int home = b.Add("拠点", Kind.Home, (-20.0f, 0f));
        int fork = b.Add(Fork13, Kind.Open, (-13.0f, 0.5f));
        (float X, float Z)[][] pos =
        {
            new[] { (-7.0f, -8.0f), (1.0f, -13.0f), (10.0f, -14.5f), (20.0f, -10.0f) },
            new[] { (-4.0f, 0.5f), (6.0f, -1.0f), (17.0f, 0f) },
            new[] { (-9.0f, 8.5f), (-3.0f, 14.0f), (5.0f, 17.0f), (14.0f, 18.0f), (22.0f, 15.5f), (27.0f, 9.0f) },
        };
        int[] outpost = { 2, 1, 3 };
        var lanes = new int[pos.Length][];
        for (int lane = 0; lane < pos.Length; lane++)
        {
            lanes[lane] = new int[pos[lane].Length + 1];
            lanes[lane][0] = fork;
            for (int k = 0; k < pos[lane].Length; k++)
            {
                bool op = k == outpost[lane];
                lanes[lane][k + 1] = b.Add(op ? Outposts12F477[lane] : $"{Lanes12F477[lane]} {k + 1} マス目", op ? Kind.Outpost : Kind.Open, pos[lane][k], lane);
            }
        }
        int portal = portalOn ? b.Add(Map11.PortalName, Kind.Portal, (30.0f, 0f)) : -1;
        b.Link(home, fork);
        for (int lane = 0; lane < pos.Length; lane++)
        {
            int from = fork;
            foreach (int c in lanes[lane].Skip(1)) { b.Link(from, c); from = c; }
            if (portal >= 0) b.Link(from, portal);
            b.Lane(Lanes12F477[lane], lanes[lane], Starts12F477[lane].Select(i => lanes[lane][i]).ToArray());
        }
        // 横の辺（関所 ⇔ 見張り台・隊だけが使う）。南はつながらない。
        b.Link(lanes[1][2], lanes[0][3]);
        return b.Build();
    }

    /// <summary>第480期の 1 章の 1-3 の経路の名前（頭の 1 文字は 北 ／ 中 ／ 南 のまま）。</summary>
    internal static readonly string[] Lanes13F480 = { "北の尾根道", "中の谷道", "南の森の道" };

    /// <summary>第480期の 1 章の 1-3 の中間拠点の名前（経路の順・仮）。北 3 マス目 ／ 中 3 マス目 ／ 南 2 マス目。</summary>
    internal static readonly string[] Outposts13F480 = { "尾根の砦跡", "谷の橋", "森の集落" };

    /// <summary>第480期の 1 章の 1-3 の味方の拠点（1-2 で取った砦）と敵の拠点の名前。</summary>
    internal const string Home13F480 = "討伐隊の砦", Camp13F480 = "残党の野営地";

    /// <summary>第480期の 1 章の 1-3 の敵の初期位置（経路の上の位置・分かれ道が 0）。北: 北2(2)・砦跡(3)・北5(5) ／ 中: 中1(1)・橋(3) ／ 南: 集落(2)・南4(4)。</summary>
    internal static readonly int[][] Starts13F480 = { new[] { 3, 4, 7 }, new[] { 1, 4 }, new[] { 3, 5 } };   // ポンの決定 2026-11-17: 道 7／6／7・中間拠点 4／4／3

    /// <summary>
    /// 1 章の 1-3 の盤・第480期（指示書 design/PHASE480_STAGE13_HUNTER_SPEC.md §2-1・`ChapterRule.V480` だけが使う）:
    /// <code>
    ///                        ┌ 北1 ─ 北2 ─ 北3 ─[尾根の砦跡]─ 北5 ─ 北6 ─ 北7 ┐
    ///   討伐隊の砦 ─ 分かれ道 ┼ 中1 ─ 中2 ─ 中3 ─[谷の橋]─ 中5 ─ 中6 ────────┼ 残党の野営地（門）
    ///                        └ 南1 ─ 南2 ─[森の集落]─ 南4 ─ 南5 ─ 南6 ─ 南7 ┘
    ///   横の辺: 尾根の砦跡 ⇔ 谷の橋 ／ 谷の橋 ⇔ 南4
    /// </code>
    /// 分かれ道から野営地まで 北 7 ／ 中 6 ／ 南 7 マス（ポンの決定 2026-11-17・始めの案は 5 ／ 4 ／ 5）。拠点から中間拠点まで 森の集落 4 歩・尾根の砦跡 5 歩・谷の橋 5 歩。
    /// <b>「門」はマスにしない</b>（今までどおり「中間拠点を 2 つ取ると敵の拠点へ入れる」の規則）——狩る者は野営地のマス（開いた門）に現れる（報告 §1-2）。
    /// 経路は分かれ道を先頭に含む（1-3 ／ 第477期の 1-2 と同じ）。単発の 1-3（<see cref="Build13(bool)"/>）は変えない。
    /// </summary>
    public static MapGraph Build13F480(bool portalOn) => Build13F480(portalOn, Starts13F480);

    /// <summary>中間拠点に <paramref name="stack"/> 部隊ずつ重ねた敵の初期位置（測りの口・1 なら <see cref="Starts13F480"/> と同じ）。重ねた分は各経路の末尾に足す。</summary>
    internal static int[][] Starts13F480Stacked(int stack)
        => new[] { 4, 4, 3 }.Select((op, lane) => Starts13F480[lane].Concat(Enumerable.Repeat(op, Math.Max(0, stack - 1))).ToArray()).ToArray();

    /// <summary>
    /// 測りの口: 道の長さ（<paramref name="len"/>・分かれ道を除くマスの数）と中間拠点の位置（<paramref name="op"/>・1 から数えたマス目）を替えた盤。
    /// 座標は等間隔に並べるだけ（画面には出さない）。敵の初期位置は 北 {中間拠点の 1 つ手前・中間拠点・奥} ／ 中 {1 マス目・中間拠点} ／ 南 {中間拠点・その 2 つ先}（案の形なら <see cref="Starts13F480"/> と同じ）。
    /// </summary>
    internal static MapGraph Build13F480Shaped(bool portalOn, int[] len, int[] op, int stack)
    {
        int[][] starts =
        {
            new[] { Math.Max(1, op[0] - 1), op[0], len[0] }, new[] { 1, op[1] }, new[] { op[2], Math.Min(len[2], op[2] + 2) },
        };
        starts = starts.Select((st, lane) => st.Concat(Enumerable.Repeat(op[lane], Math.Max(0, stack - 1))).ToArray()).ToArray();
        float[] zs = { -14f, 0f, 14f };
        var pos = len.Select((n, lane) => Enumerable.Range(0, n).Select(k => (-7f + 34f * k / Math.Max(1, n - 1), zs[lane])).ToArray()).ToArray();
        return Build13F480(portalOn, starts, pos, op.Select(x => x - 1).ToArray(), cross: new[] { (0, op[0], 1, op[1]), (1, op[1], 2, Math.Min(len[2], op[2] + 1)) });
    }

    internal static MapGraph Build13F480(bool portalOn, int[][] starts) => Build13F480(portalOn, starts, null, null, null);

    private static MapGraph Build13F480(bool portalOn, int[][] starts, (float X, float Z)[][]? shapedPos, int[]? shapedOutpost, (int LaneA, int IdxA, int LaneB, int IdxB)[]? cross)
    {
        var b = new Builder();
        int home = b.Add(Home13F480, Kind.Home, (-24.0f, 0f));
        int fork = b.Add(Fork13, Kind.Open, (-17.0f, 0.5f));
        (float X, float Z)[][] pos = shapedPos ?? new[]
        {
            new[] { (-11.0f, -8.0f), (-4.0f, -13.0f), (4.0f, -15.5f), (12.0f, -16.0f), (20.0f, -15.0f), (28.0f, -12.0f), (34.0f, -7.0f) },
            new[] { (-9.0f, 0.5f), (-1.0f, -1.0f), (7.0f, 0f), (15.0f, -1.0f), (23.0f, 0f), (31.0f, 0.5f) },
            new[] { (-12.0f, 8.5f), (-6.0f, 14.0f), (2.0f, 17.0f), (10.0f, 18.0f), (18.0f, 17.5f), (26.0f, 15.0f), (33.0f, 9.0f) },
        };
        int[] outpost = shapedOutpost ?? new[] { 3, 3, 2 };
        var lanes = new int[pos.Length][];
        for (int lane = 0; lane < pos.Length; lane++)
        {
            lanes[lane] = new int[pos[lane].Length + 1];
            lanes[lane][0] = fork;
            for (int k = 0; k < pos[lane].Length; k++)
            {
                bool op = k == outpost[lane];
                lanes[lane][k + 1] = b.Add(op ? Outposts13F480[lane] : $"{Lanes13F480[lane]} {k + 1} マス目", op ? Kind.Outpost : Kind.Open, pos[lane][k], lane);
            }
        }
        int portal = portalOn ? b.Add(Camp13F480, Kind.Portal, (39.0f, 0f)) : -1;
        b.Link(home, fork);
        for (int lane = 0; lane < pos.Length; lane++)
        {
            int from = fork;
            foreach (int c in lanes[lane].Skip(1)) { b.Link(from, c); from = c; }
            if (portal >= 0) b.Link(from, portal);
            b.Lane(Lanes13F480[lane], lanes[lane], starts[lane].Select(i => lanes[lane][i]).ToArray());
        }
        // 横の辺（隊と狩る者が使う）: 尾根の砦跡 ⇔ 谷の橋 ／ 谷の橋 ⇔ 南3。
        foreach (var (la, ia, lb, ib) in cross ?? new[] { (0, 4, 1, 4), (1, 4, 2, 4) }) b.Link(lanes[la][ia], lanes[lb][ib]);
        return b.Build();
    }

    /// <summary>1-3 の経路の名前（<see cref="MapStage.Stage13"/> と同じ並び・1-2 と同じ）。</summary>
    internal static readonly string[] Lanes13 = Lanes12;

    /// <summary>1-3 の中間拠点の名前（第458期・仮・1-2 と同じ）。各経路の 3 マス目（分岐の先から数えて N0 → N1 → 砦）。</summary>
    internal static readonly string[] Outposts13 = Outposts12;

    /// <summary>分岐のマスの名前（第458期）。</summary>
    internal const string Fork13 = "分かれ道";

    /// <summary>
    /// 1-3 の盤（第458期 §0-1・指示書 design/PHASE458_STAGE13_SPEC.md）。
    /// <code>
    ///                N0 ── N1 ──[北の砦]── N3 ──────┐
    ///               /               |               │
    ///   Home ── F ── C0 ── C1 ──[野営地]── C3 ── C4 ─┼── Portal
    ///               \               |               │
    ///                S0 ── S1 ──[南の関所]── S3 ─────┘
    /// </code>
    /// 拠点 → <b>分岐 F（Open・通り抜け・どの経路にも属さない）</b> → 3 経路。中間拠点は各経路の 3 マス目（拠点から 4 歩）で、
    /// そこから本陣まで北・南は 2 歩、中は 3 歩（C3・C4）。<b>横の辺は中間拠点どうし</b>（北の砦 ⇔ 野営地 ⇔ 南の関所・隊だけが使う）。
    /// 経路（<see cref="Lanes"/>）は<b>分岐 F を先頭に含む</b>——敵は F を通って拠点へ着く（F の <c>Lane</c> は -1・3 本の経路が共有する）。
    /// 盤のマスは拠点を除いて 14（F ＋ 北 4 ＋ 中 5 ＋ 南 4）＋ 本陣。指示書の図の「N2」は砦の位置の書き違いと読んだ（「拠点から 4 歩」「マス 14」と合う形）。
    /// 敵の初期位置は <paramref name="starts"/>（経路ごと・区画の番号の順・経路の上の位置＝ F が 0）。<c>Pos</c> は 1-2 を横に伸ばした座標。
    /// </summary>
    public static MapGraph Build13(bool portalOn) => Build13(portalOn, Starts13);

    /// <summary>1-3 の敵の初期位置（経路の上の位置・F が 0）。北: 弓隊 N1(2)・雷杖 砦(3) ／ 中: 物見 C1(2)・火 野営地(3)・大群 ×3 C3(4) ・本陣の守り ／ 南: 盾 S1(2)・槍兵 関所(3)。</summary>
    internal static readonly int[][] Starts13 = { new[] { 2, 3 }, new[] { 2, 3, 4, 4, 4 }, new[] { 2, 3 } };

    private static MapGraph Build13(bool portalOn, int[][] starts)
    {
        var b = new Builder();
        int home = b.Add("拠点", Kind.Home, (-15.0f, 0f));
        int fork = b.Add(Fork13, Kind.Open, (-9.0f, 0f));
        float[] zs = { -15.0f, 0f, 15.0f };
        // 経路の上のマス（F を除く）: 北・南は 4、中は 5。中間拠点は 3 マス目（添字 2）。
        int[] counts = { 4, 5, 4 };
        float[][] xs = { new[] { -3.0f, 5.0f, 13.0f, 22.0f }, new[] { -3.0f, 5.0f, 13.0f, 21.0f, 29.0f }, new[] { -3.0f, 5.0f, 13.0f, 22.0f } };
        var lanes = new int[Lanes13.Length][];
        for (int lane = 0; lane < Lanes13.Length; lane++)
        {
            lanes[lane] = new int[counts[lane] + 1];
            lanes[lane][0] = fork;
            for (int k = 0; k < counts[lane]; k++)
            {
                bool outpost = k == 2;
                string name = outpost ? Outposts13[lane] : $"{Lanes13[lane]} {k + 1} マス目";
                lanes[lane][k + 1] = b.Add(name, outpost ? Kind.Outpost : Kind.Open, (xs[lane][k], zs[lane]), lane);
            }
        }
        int portal = portalOn ? b.Add(Map11.PortalName, Kind.Portal, (37.0f, 0f)) : -1;
        b.Link(home, fork);
        for (int lane = 0; lane < Lanes13.Length; lane++)
        {
            int from = fork;
            foreach (int c in lanes[lane].Skip(1)) { b.Link(from, c); from = c; }
            if (portal >= 0) b.Link(from, portal);
            b.Lane(Lanes13[lane], lanes[lane], starts[lane].Select(i => lanes[lane][i]).ToArray());
        }
        // 横の辺（中間拠点どうし・隊だけが使う）。
        b.Link(lanes[0][3], lanes[1][3]);
        b.Link(lanes[1][3], lanes[2][3]);
        return b.Build();
    }
}
