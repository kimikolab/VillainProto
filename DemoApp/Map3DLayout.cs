using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 3D のジオラマの<b>ステージごとの表</b>（発注書 §2-2）。<b>規則は 1 行も持たない</b>——盤（<see cref="MapGraph"/>）のマスを
/// 3D のどこに置くか・道がどう曲がるか・川・建物・森の場所だけを持つ。
/// 表の無いステージ（いまは 1-2 ／ 1-3）は盤の座標をそのまま使い、道はまっすぐ・地形は草地と木だけになる（<see cref="Generic"/>）。
/// 1-2 ／ 1-3 を足すときは <see cref="Tables"/> に 1 つ足すだけでよい。
/// <b>表は「ステージの Id ＋ 盤の形（経路ごとのマスの数）」で引く</b>——1-1 は単発（3 マス × 2 本）と 1 章の第474期（北 2 マス・南 4 マス）で
/// Id が同じ "1-1" なので、盤の形で分ける。
/// </summary>
internal sealed class Map3DLayout
{
    /// <summary>ステージ 1 つぶんの表。マスは「経路の番号 × 経路の上の番号」で書く（盤の Id に依らない）。</summary>
    internal sealed class Table
    {
        /// <summary>どのステージの表か（<c>MapStage.Id</c>）。盤の形は <see cref="Lanes"/> のマスの数で合わせる。</summary>
        public required string Stage { get; init; }
        /// <summary>場所の名前（左上の題・建物の名札に出す・仮）。</summary>
        public string Name { get; init; } = "";
        public string HomeName { get; init; } = "";
        public string PortalName { get; init; } = "";
        public required Vector3 Home { get; init; }
        public required Vector3 Portal { get; init; }
        /// <summary>経路ごとのマスの位置（拠点の隣 → 敵の拠点の手前）。</summary>
        public required Vector3[][] Lanes { get; init; }
        /// <summary>川（上流 → 下流の点の列）。道とは橋で交わる。</summary>
        public Vector3[][] Rivers { get; init; } = Array.Empty<Vector3[]>();
        /// <summary>拠点の建物（辺境の砦）の中心と、その隣の墓地の中心。</summary>
        public Vector3 Fort { get; init; }
        public Vector3 Graveyard { get; init; }
        /// <summary>敵の本陣の中心。</summary>
        public Vector3 Camp { get; init; }
        /// <summary>森（中心 x, z ・半径 x, z ・本数 ・種類）。</summary>
        public (Vector3 At, Vector2 Radius, int Count, string Kind)[] Woods { get; init; } = Array.Empty<(Vector3, Vector2, int, string)>();
        /// <summary>湿地（中心・半径）。水たまり・葦・柳を置く。</summary>
        public (Vector3 At, Vector2 Radius)[] Wetlands { get; init; } = Array.Empty<(Vector3, Vector2)>();
        /// <summary>山すそが始まる z（これより奥は地面がせり上がり、岩と山が並ぶ）。</summary>
        public float FoothillZ { get; init; } = -11f;
        /// <summary>拠点で待つ隊の立ち位置（隊の番号の順）。</summary>
        public Vector3[] HomeSlots { get; init; } = Array.Empty<Vector3>();
        /// <summary>カメラが見る点（盤の真ん中より少し手前——下の UI のぶん）。</summary>
        public Vector3 Look { get; init; }
        /// <summary>地形の台の端（x, z の四角）。地面・木・山はこの中にだけ置き、端は厚い台の側面になる。</summary>
        public Rect2 Bounds { get; init; } = new(-30, -21, 60, 38);
        /// <summary>カメラを引く倍率（1.0 ＝ `Map3DCamera.Height` のまま）。台の全体が 1 画面に入る値にする。</summary>
        public float Reach { get; init; } = 1f;
        /// <summary>峠の関（柵と盾の並ぶ狭い所）を置くマス（経路, 経路の上の番号）。無ければ null。</summary>
        public (int Lane, int Index)? Pass { get; init; }
        /// <summary>山を北へ寄せる（奥の山並みを厚く・高くする）。</summary>
        public bool NorthRange { get; init; }
    }

    /// <summary>表のあるステージ。<b>Id は <c>MapStage.Id</c></b>（1 章の 1-1 も検証用の 1-1 も "1-1"）。</summary>
    private static readonly List<Table> Tables = new()
    {
        // 1-1（単発・3 マス × 2 本）: 左に辺境の砦と墓地、右に討伐隊の本陣。北の道は山すそを回り、南の道は森と湿地を抜ける。真ん中に森と川。
        new Table
        {
            Stage = "1-1",
            Bounds = new Rect2(-27.4f, -25, 54.4f, 42),
            Reach = 1.34f,
            Name = "辺境の砦",
            HomeName = "辺境の砦",
            PortalName = "討伐隊の本陣",
            Home = new Vector3(-16.0f, 0, 0.6f),
            Portal = new Vector3(16.2f, 0, 0.2f),
            Lanes = new[]
            {
                new[] { new Vector3(-11.4f, 0, -4.6f), new Vector3(-3.4f, 0, -7.4f), new Vector3(7.4f, 0, -6.8f) },
                new[] { new Vector3(-11.4f, 0, 5.6f), new Vector3(-3.4f, 0, 8.0f), new Vector3(7.4f, 0, 7.4f) },
            },
            Rivers = new[]
            {
                new[]
                {
                    new Vector3(3.4f, 0, -16.5f), new Vector3(1.9f, 0, -7.2f),
                    new Vector3(0.4f, 0, 0.3f), new Vector3(2.0f, 0, 7.8f), new Vector3(3.8f, 0, 16.8f),
                },
            },
            Fort = new Vector3(-21.6f, 0, 0.6f),
            Graveyard = new Vector3(-21.8f, 0, -7.0f),
            Camp = new Vector3(20.6f, 0, 0.2f),
            Woods = new[]
            {
                (new Vector3(-6.4f, 0, 0.6f), new Vector2(5.6f, 3.3f), 22, "forest"),    // 真ん中の森（川の西）
                (new Vector3(8.4f, 0, 0.3f), new Vector2(4.8f, 3.2f), 18, "forest"),     // 真ん中の森（川の東）
                (new Vector3(-9.0f, 0, 12.6f), new Vector2(7.0f, 2.6f), 16, "forest"),   // 南の道の手前の森
                (new Vector3(-12f, 0, -13.0f), new Vector2(9.0f, 2.0f), 12, "pine"),     // 山すその松
                (new Vector3(12f, 0, -13.0f), new Vector2(9.0f, 2.0f), 12, "pine"),
                (new Vector3(14f, 0, 13.0f), new Vector2(6.0f, 2.4f), 10, "forest"),
            },
            Wetlands = new[]
            {
                (new Vector3(3.6f, 0, 11.4f), new Vector2(5.2f, 2.6f)),
                (new Vector3(-0.6f, 0, 4.4f), new Vector2(2.6f, 2.0f)),
            },
            FoothillZ = -10.6f,
            HomeSlots = new[]
            {
                // 第1隊・第2隊は広場の手前（南）に並び、第3隊・第4隊は奥（北）。道（広場から北東・南東へ出る）は空ける。
                new Vector3(-21.4f, 0, 5.9f), new Vector3(-15.2f, 0, 6.4f), new Vector3(-21.0f, 0, -3.4f), new Vector3(-14.6f, 0, -4.2f),
            },
            Look = new Vector3(0, 0, 1.2f),
        },
        // 1-1（1 章・第474期・北 2 マス ／ 南 4 マス）: 上下で対称にしない。
        //   北 ＝ 砦から山すそを上がり、峠の関を越えて本陣へ（短くまっすぐ・道は高い所を通る）
        //   南 ＝ 森を抜け、湿地を回り、川を渡って本陣の裏へ（長く曲がる）
        //   川は峠の下の斜面から湧いて、盤を斜めに（北東 → 南西）流れる。山は北に、森は南に寄せる。
        new Table
        {
            Stage = "1-1",
            Bounds = new Rect2(-28.4f, -26, 55.4f, 43.5f),
            Reach = 1.34f,
            NorthRange = true,
            Name = "辺境の砦",
            HomeName = "辺境の砦",
            PortalName = "討伐隊の本陣",
            Home = new Vector3(-17.4f, 0, -0.8f),
            Portal = new Vector3(17.0f, 0, -2.0f),
            Lanes = new[]
            {
                new[] { new Vector3(-6.0f, 1.5f, -8.0f), new Vector3(6.0f, 3.0f, -9.4f) },
                new[] { new Vector3(-11.0f, 0, 6.4f), new Vector3(-3.8f, 0, 11.2f), new Vector3(6.8f, 0, 11.8f), new Vector3(15.6f, 0, 6.6f) },
            },
            Pass = (0, 1),
            Rivers = new[]
            {
                new[]
                {
                    new Vector3(9.8f, 0, -3.6f), new Vector3(7.4f, 0, -0.4f), new Vector3(4.6f, 0, 4.6f),
                    new Vector3(1.8f, 0, 11.4f), new Vector3(-0.4f, 0, 17.3f),
                },
            },
            Fort = new Vector3(-23.0f, 0, -0.8f),
            Graveyard = new Vector3(-22.6f, 0, -8.6f),
            Camp = new Vector3(21.4f, 0, -3.4f),
            Woods = new[]
            {
                (new Vector3(-13.5f, 0, 12.0f), new Vector2(7.5f, 3.6f), 26, "forest"),   // 南の道の入口の森
                (new Vector3(-7.0f, 0, 3.4f), new Vector2(4.6f, 2.4f), 12, "forest"),     // 砦と川のあいだの林
                (new Vector3(-6.0f, 0, 15.2f), new Vector2(7.0f, 1.5f), 10, "forest"),    // 手前の縁
                (new Vector3(12.0f, 0, 15.0f), new Vector2(8.0f, 1.6f), 10, "forest"),
                (new Vector3(12.6f, 0, 1.6f), new Vector2(3.0f, 2.2f), 6, "forest"),      // 本陣の手前の木立
                (new Vector3(-15f, 0, -15.0f), new Vector2(9.0f, 2.0f), 10, "pine"),      // 山すその松
                (new Vector3(17f, 0, -13.5f), new Vector2(8.0f, 2.2f), 10, "pine"),
            },
            Wetlands = new[]
            {
                (new Vector3(3.0f, 0, 7.4f), new Vector2(3.2f, 2.0f)),
                (new Vector3(-2.4f, 0, 15.2f), new Vector2(3.4f, 1.4f)),
            },
            FoothillZ = -13.0f,
            HomeSlots = new[]
            {
                // 第1隊・第2隊は広場の手前（南）、第3隊は道のあいだ（東）、第4隊は砦の南。
                new Vector3(-22.0f, 0, 4.6f), new Vector3(-16.0f, 0, 5.4f), new Vector3(-12.4f, 0, 0.0f), new Vector3(-25.6f, 0, 7.4f),
            },
            Look = new Vector3(0, 0, 0.6f),
        },
    };

    private readonly MapGraph _graph;
    private readonly Vector3[] _cells;
    /// <summary>辺ごとの道の点の列（a → b の向き）。隣り合わないマスは持たない。</summary>
    private readonly Dictionary<(int, int), Vector3[]> _edges = new();

    /// <summary>表（無いステージは null——草地と木だけの仮の盤）。</summary>
    internal Table? Sheet { get; }
    /// <summary>道の道具に渡す曲線（表のあるステージは経路ごとに 1 本・無いステージは辺ごとに 1 本）。</summary>
    internal List<Vector3[]> Roads { get; } = new();
    /// <summary>経路ごとの「本陣の手前」の点（敵の拠点へ入る隊が立つ所・行き先の札もここに置く）。</summary>
    internal Vector3[] Approach { get; }
    internal Vector3 Look { get; }
    /// <summary>カメラを引く倍率（表のあるステージは 1・無いステージは盤が収まるだけ引く）。</summary>
    internal float Reach { get; } = 1f;
    /// <summary>地形の台の端（x, z）。</summary>
    internal Rect2 Bounds { get; }
    /// <summary>峠の関のマスの位置と、そこでの道の向き（無ければ null）。</summary>
    internal (Vector3 At, Vector3 Forward)? Pass
        => Sheet?.Pass is { } p ? (_cells[_graph.Lanes[p.Lane][p.Index]], Forward(_graph.Lanes[p.Lane][p.Index])) : null;

    internal Map3DLayout(MapStage stage, MapGraph graph)
    {
        _graph = graph;
        // 表は Id と盤の形（経路の数・経路ごとのマスの数）の両方が合うもの。無ければ仮の盤に落とす。
        Sheet = Tables.FirstOrDefault(t => t.Stage == stage.Id && t.Lanes.Length == graph.Lanes.Count
            && Enumerable.Range(0, graph.Lanes.Count).All(l => t.Lanes[l].Length == graph.Lanes[l].Length));

        _cells = graph.Cells.Select(c => new Vector3(c.Pos.X, 0, c.Pos.Z)).ToArray();
        Approach = new Vector3[graph.Lanes.Count];
        if (Sheet is null)
        {
            Look = Generic();
            float wide = _cells.Max(c => c.X) - _cells.Min(c => c.X), deep = _cells.Max(c => c.Z) - _cells.Min(c => c.Z);
            Reach = Math.Max(1f, Math.Max((wide + 14) / 50f, (deep + 9) / 23f));
            Bounds = new Rect2(_cells.Min(c => c.X) - 9, _cells.Min(c => c.Z) - 12, wide + 18, deep + 21);
            return;
        }
        Reach = Sheet.Reach;
        Bounds = Sheet.Bounds;

        _cells[graph.Home] = Sheet.Home;
        if (graph.HasPortal) _cells[graph.Portal] = Sheet.Portal;
        for (int l = 0; l < graph.Lanes.Count; l++)
            for (int i = 0; i < graph.Lanes[l].Length; i++) _cells[graph.Lanes[l][i]] = Sheet.Lanes[l][i];

        // 経路ごとに 1 本のなめらかな曲線（拠点 → マス → 敵の拠点）を引き、マスとマスのあいだを切り出して辺の道にする。
        for (int l = 0; l < graph.Lanes.Count; l++)
        {
            var ids = new List<int> { graph.Home };
            ids.AddRange(graph.Lanes[l]);
            if (graph.HasPortal) ids.Add(graph.Portal);
            Vector3[] line = Smooth(ids.Select(id => _cells[id]).ToArray(), out int[] at);
            // マスのまわりは平らにする（坂の途中に台を置くと、台の山側が地面に埋もれる）。
            for (int i = 0; i < line.Length; i++)
                foreach (int id in ids)
                {
                    float d = new Vector2(line[i].X - _cells[id].X, line[i].Z - _cells[id].Z).Length();
                    float reach = id == graph.Home || (graph.HasPortal && id == graph.Portal) ? 3.4f : 2.2f;
                    line[i].Y = Mathf.Lerp(_cells[id].Y, line[i].Y, Mathf.SmoothStep(reach, reach + 3.0f, d));
                }
            Roads.Add(line);
            for (int k = 0; k + 1 < ids.Count; k++)
            {
                Vector3[] seg = line[at[k]..(at[k + 1] + 1)];
                _edges[(ids[k], ids[k + 1])] = seg;
                _edges[(ids[k + 1], ids[k])] = seg.Reverse().ToArray();
            }
            // 本陣の手前: 最後の辺を本陣から 3.4 だけ戻った点。
            Approach[l] = graph.HasPortal ? Along(_edges[(graph.Portal, ids[^2])], 4.2f) : _cells[ids[^1]];
        }
        // 経路をまたぐ横の辺（表のあるステージには今は無い）はまっすぐ。
        foreach ((int a, int b) in graph.Edges)
            if (!_edges.ContainsKey((a, b))) AddStraight(a, b, road: true);
        Look = Sheet.Look;
    }

    /// <summary>表の無いステージ: 盤の座標のまま・辺はまっすぐ。</summary>
    private Vector3 Generic()
    {
        foreach ((int a, int b) in _graph.Edges) AddStraight(a, b, road: true);
        for (int l = 0; l < _graph.Lanes.Count; l++)
            Approach[l] = _graph.HasPortal
                ? _cells[_graph.Portal].MoveToward(_cells[_graph.Lanes[l][^1]], 3.4f) : _cells[_graph.Lanes[l][^1]];
        float minX = _cells.Min(c => c.X), maxX = _cells.Max(c => c.X), minZ = _cells.Min(c => c.Z), maxZ = _cells.Max(c => c.Z);
        return new Vector3((minX + maxX) / 2, 0, (minZ + maxZ) / 2 + 1.5f);
    }

    private void AddStraight(int a, int b, bool road)
    {
        Vector3[] seg = { _cells[a], _cells[b] };
        _edges[(a, b)] = seg;
        _edges[(b, a)] = new[] { _cells[b], _cells[a] };
        if (road) Roads.Add(seg);
    }

    internal Vector3 PosOf(int cell) => _cells[cell];

    /// <summary>
    /// マス <paramref name="from"/> から隣のマス <paramref name="to"/> へ歩く道の点の列。隣り合っていなければ（ワープ・拠点越し）2 点のまっすぐ。
    /// </summary>
    internal Vector3[] Walk(int from, int to)
        => _edges.TryGetValue((from, to), out Vector3[]? seg) ? seg : new[] { _cells[from], _cells[to] };

    /// <summary>マスの列（盤の最短路）を道に沿った点の列にする（経路の案内の点線が使う）。</summary>
    internal List<Vector3> Trace(IReadOnlyList<int> cells)
    {
        var points = new List<Vector3>();
        for (int i = 0; i + 1 < cells.Count; i++)
        {
            Vector3[] seg = Walk(cells[i], cells[i + 1]);
            points.AddRange(i == 0 ? seg : seg.Skip(1));
        }
        return points;
    }

    /// <summary>マスでの道の向き（拠点 → 敵の拠点の向きの単位ベクトル）。駒を道の手前側・奥側へずらすのに使う。</summary>
    internal Vector3 Forward(int cell)
    {
        int lane = _graph[cell].Lane;
        if (lane < 0) return Vector3.Right;
        int i = _graph.IndexOn(lane, cell);
        Vector3[] seg = Walk(cell, _graph.At(lane, _graph.HasPortal || i + 1 < _graph.Lanes[lane].Length ? i + 1 : i - 1));
        Vector3 dir = (seg[Math.Min(2, seg.Length - 1)] - seg[0]).Normalized();
        return !_graph.HasPortal && i + 1 >= _graph.Lanes[lane].Length ? -dir : dir;
    }

    /// <summary>点の列を先頭から <paramref name="distance"/> だけ進んだ点。</summary>
    internal static Vector3 Along(IReadOnlyList<Vector3> line, float distance)
    {
        for (int i = 0; i + 1 < line.Count; i++)
        {
            float step = line[i].DistanceTo(line[i + 1]);
            if (distance <= step) return line[i].Lerp(line[i + 1], step <= 0 ? 0 : distance / step);
            distance -= step;
        }
        return line[^1];
    }

    internal static float LengthOf(IReadOnlyList<Vector3> line)
    {
        float sum = 0;
        for (int i = 0; i + 1 < line.Count; i++) sum += line[i].DistanceTo(line[i + 1]);
        return sum;
    }

    /// <summary>点から点の列までの水平の距離と、いちばん近い所の高さ。地面を道の高さに合わせるのに使う。</summary>
    internal static float Nearest(IReadOnlyList<Vector3> line, Vector3 p, out float height)
    {
        float best = float.MaxValue;
        height = 0;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector3 a = line[i], ab = line[i + 1] - a;
            float flat = ab.X * ab.X + ab.Z * ab.Z;
            float t = flat <= 0 ? 0 : Mathf.Clamp(((p.X - a.X) * ab.X + (p.Z - a.Z) * ab.Z) / flat, 0, 1);
            Vector3 q = a + ab * t;
            float d = new Vector2(p.X - q.X, p.Z - q.Z).Length();
            if (d < best) { best = d; height = q.Y; }
        }
        return best;
    }

    /// <summary>点から点の列までの距離（水平）。木や岩を道・川から離すのに使う。</summary>
    internal static float DistanceTo(IReadOnlyList<Vector3> line, Vector3 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector3 a = line[i], ab = line[i + 1] - a;
            float t = ab.LengthSquared() <= 0 ? 0 : Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0, 1);
            Vector3 d = p - (a + ab * t);
            best = Math.Min(best, new Vector2(d.X, d.Z).Length());
        }
        return best;
    }

    /// <summary>
    /// 通る点をなめらかにつなぐ（Catmull-Rom）。<paramref name="at"/> は元の点が出力の何番目かを返す。刻みはおよそ 0.5。
    /// </summary>
    internal static Vector3[] Smooth(Vector3[] through, out int[] at)
    {
        var line = new List<Vector3>();
        at = new int[through.Length];
        for (int i = 0; i + 1 < through.Length; i++)
        {
            Vector3 p0 = through[Math.Max(i - 1, 0)], p1 = through[i], p2 = through[i + 1], p3 = through[Math.Min(i + 2, through.Length - 1)];
            int steps = Math.Max(2, (int)(p1.DistanceTo(p2) / 0.5f));
            at[i] = line.Count;
            for (int s = 0; s < steps; s++)
            {
                float t = s / (float)steps, t2 = t * t, t3 = t2 * t;
                line.Add(0.5f * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (3 * p1 - p0 - 3 * p2 + p3) * t3));
            }
        }
        at[^1] = line.Count;
        line.Add(through[^1]);
        return line.ToArray();
    }
}
