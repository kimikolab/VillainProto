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
}
