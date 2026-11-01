using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 盤（マスのグラフ）の門（第438期 §5-2・`--map11-graph-smoke`）
//
// **画面を1つも作らない。** `MapGraph` と `Map11State` / `Map11Orders` だけを回す。
// **ここには Godot の型を1つも使わない**（`Map11.cs` / `Map11Orders.cs` と同じ扱い）。
//
//   (a) 1-1 の盤で N2 から S1 への最短路が拠点を通り、敵の拠点を通り抜けない
//   (b) N1 に敵部隊がいるとき、N0 の隊を N2 へ命令しても N1 で止まって戦闘になる
//   (c) 南の経路の先頭の敵が、経路の長さ（S1 → S0 → 拠点）の歩数で拠点に着き、迎撃が立つ
//
// 加えて §5-3（**報告のみ・採否に使わない**）——1-1 の外の玩具の盤（北と南を N1–S1 で横に繋いだ盤）を
// 頭なしの貪欲方針で回し、落ちないことと踏破率を出す。
// =====================================================================================

public static class MapGraphSmoke
{
    /// <summary>玩具の盤を回す seed の本数（§5-3）。</summary>
    public const int ToySeeds = 100;

    /// <summary>玩具の盤: 1-1 に<b>北の 1 戦目の地点 ⇔ 南の 1 戦目の地点</b>の辺を1本足しただけ（経路・敵の初期位置は 1-1 のまま）。</summary>
    public static MapGraph ToyCross(bool portalOn)
        => MapGraph.Build11(portalOn, (b, lanes) => b.Link(lanes[0][1], lanes[1][1]));

    public static bool Run(Action<string> write)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        string Names(MapGraph g, IEnumerable<int> path) => string.Join(" → ", path.Select(id => g[id].Name));

        // ---- 盤そのもの ----
        foreach (bool portalOn in new[] { false, true })
        {
            var g = MapGraph.Build11(portalOn);
            Req(g.Cells.Count == (portalOn ? 8 : 7) && g.HasPortal == portalOn && g.Lanes.Count == Map11.RoadCount
                && g.Lanes.All(l => l.Length == Map11.RoadCells),
                $"1-1（拠点{(portalOn ? "あり" : "なし")}）: マス {g.Cells.Count}・経路 {g.Lanes.Count} 本 × {Map11.RoadCells}・新しいマスは無い");
            Req(g.Cells.All(c => c.Neighbors.All(n => g[n].Neighbors.Contains(c.Id))), "隣り合うマスは両方向に繋がる");
            Req(Enumerable.Range(0, Map11.RoadCount).All(r => Enumerable.Range(0, Map11.Roads[r].Count)
                    .All(i => g.IndexOn(r, g.FoeStarts[r][i]) == Map11.StartCellOf(i))),
                "敵部隊の初期位置 ＝ 第437期の `StartCellOf`（N1・N2 ／ S1・S2）");
        }

        // ---- (a) ----
        {
            var g = MapGraph.Build11(true);
            int n2 = g.Lanes[0][2], s1 = g.Lanes[1][1];
            var path = g.Path(n2, -1, s1, -1);
            write($"  N2 → S1: {Names(g, path)}");
            Req(path.Count > 0 && path.Contains(g.Home) && !path.Contains(g.Portal), "(a) N2 から S1 への最短路は拠点を通り、敵の拠点を通り抜けない");
            var viaPortal = g.Path(g.Portal, 0, s1, -1);
            write($"  敵の拠点（北から入った）→ S1: {Names(g, viaPortal)}");
            Req(viaPortal.Count > 1 && viaPortal[1] == n2 && viaPortal.Contains(g.Home), "(a') 敵の拠点からは入ってきた経路へ出る（北から入ったら北の道 → 拠点 → 南の道）");
        }

        // ---- (b) ----
        {
            var st = new Map11State(0, TimeRule.Every(1));
            var g = st.Graph;
            int n0 = g.Lanes[0][0], n1 = g.Lanes[0][1];
            Dest deep = Dest.Deep(g, 0);
            Map11State.Node foe = st.Nodes[0][0];
            Map11Orders.Order o1 = Map11Orders.Plan(st, 0, deep);
            Map11State.Node? met1 = Map11Orders.Apply(st, 0, deep, o1);
            int at1 = st.Squads[0].Pos;
            Map11Orders.Order o2 = Map11Orders.Plan(st, 0, deep);
            Map11State.Node? met2 = Map11Orders.Apply(st, 0, deep, o2);
            write($"  1 歩目: {o1} → {g[at1].Name}（接敵 {met1?.Def.Name ?? "なし"}）／ 2 歩目: {o2} → {g[st.Squads[0].Pos].Name}（接敵 {met2?.Def.Name ?? "なし"}）");
            Req(foe.Pos == n1 && at1 == n0 && met1 is null && ReferenceEquals(met2, foe) && st.Squads[0].Pos == n1
                && st.Prepare(0, met2) is not null,
                "(b) N1 に敵部隊がいると、N2 へ命令した隊は N0 → N1 で止まり、そこで戦闘になる（敵を越えない）");
        }

        // ---- (c) ----
        {
            var st = new Map11State(0, TimeRule.Every(1));
            var g = st.Graph;
            Map11State.Node front = st.Nodes[1][0];
            int want = g.Path(front.Pos, -1, g.Home, -1).Count - 1;
            int steps = 0;
            Map11State.Encounter? hit = null;
            var trail = new List<string> { g[front.Pos].Name };
            while (steps < 10 && hit is null)
            {
                steps++;
                hit = st.AdvanceFoes().FirstOrDefault(e => e.Intercept && ReferenceEquals(e.Node, front));
                trail.Add(g[front.Pos].Name);
            }
            write($"  南の先頭: {string.Join(" → ", trail)}（{steps} 歩・盤の距離 {want}）");
            Req(hit is not null && steps == want && front.Pos == g.Home && st.NextInterceptor(front) >= 0,
                $"(c) 南の経路の先頭の敵は {want} 歩（S1 → S0 → 拠点）で拠点に着き、迎撃が立つ（指示書 §5-2 (c) は「3 歩」——先頭の初期位置は S1 なので盤の距離は 2）");
        }

        // ---- 玩具の盤（§5-3・報告のみ） ----
        {
            var toy = ToyCross(true);
            int n1 = toy.Lanes[0][1], s1 = toy.Lanes[1][1], n2 = toy.Lanes[0][2];
            var p = toy.Path(n2, -1, s1, -1);
            write($"  玩具の盤: N2 → S1: {Names(toy, p)}（拠点を通らない）");
            Req(p.SequenceEqual(new[] { n2, n1, s1 }), "玩具の盤では横の辺が最短路に乗る（N2 → N1 → S1）");

            int won = 0, fallen = 0, crashed = 0, moves = 0, turnsSum = 0;
            for (int seed = 0; seed < ToySeeds; seed++)
            {
                try
                {
                    var r = ToyRun(seed, toy);
                    if (r.Won) won++;
                    if (r.Fallen) fallen++;
                    moves += r.Moves;
                    turnsSum += r.Turns;
                }
                catch (Exception ex)
                {
                    crashed++;
                    if (crashed <= 3) write($"  × seed {seed}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            write($"  玩具の盤 × 貪欲 {ToySeeds} seed: 落ちた {crashed} ／ 踏破 {won}（{100.0 * won / ToySeeds:F1}%）／ 陥落 {fallen} ／ "
                + $"横の辺を通った手 {moves}（1 本あたり {(double)moves / ToySeeds:F1}）／ 平均 {(double)turnsSum / ToySeeds:F1} 作戦ターン");
            Req(crashed == 0, $"玩具の盤を {ToySeeds} seed 回して落ちない（数字は報告のみ・線は置かない）");
        }
        return ok;
    }

    private readonly record struct ToyOnce(bool Won, bool Fallen, int Turns, int Moves);

    /// <summary>
    /// 玩具の盤で 1 マップ。<b>貪欲</b>: 各隊は盤の上でいちばん近い敵部隊のマスを行き先にする（拠点に着いた敵は迎撃が受ける）。
    /// 敵が尽きたら敵の拠点へ（制圧後は拠点へ戻る）。控えは隊が全滅してから出す（第169期と同じ）。規則は `Map11State` / `Map11Orders` のまま。
    /// </summary>
    private static ToyOnce ToyRun(int seed, MapGraph toy)
    {
        var st = new Map11State(seed, Map11.AdoptedTime, Map11.AdoptedPortal, graph: toy);
        var g = st.Graph;
        int moves = 0;

        void Fight(int squad, Map11State.Node node, bool intercept)
        {
            var prep = intercept ? st.PrepareIntercept(squad, node) : st.Prepare(squad, node);
            if (prep is not { } q) return;
            BattleResult r = BattleEngine.Run(q.Players, q.Enemies, q.Seed, verbose: false);
            st.Resolve(squad, q.Node, r.PlayerWon, intercept);
        }

        Dest DestFor(int i)
        {
            Map11State.Squad s = st.Squads[i];
            if (s.Lost) return Dest.Home;
            if (i == st.Squads.Length - 1 && !s.Deployed && !st.Squads.Any(x => x.Lost)) return Dest.Home;
            Map11State.Node? best = null;
            int bestLen = int.MaxValue;
            for (int road = 0; road < g.Lanes.Count; road++)
                foreach (Map11State.Node n in st.FoesOn(road))
                {
                    if (n.Pos == g.Home) continue;
                    int len = g.Path(s.Pos, s.Road, n.Pos, n.Pos == g.Portal ? n.Def.Road : -1).Count;
                    if (len > 0 && len < bestLen) { bestLen = len; best = n; }
                }
            if (best is not null)
                return best.Pos == g.Portal ? Dest.Portal(g, best.Def.Road) : Dest.Of(g, best.Pos);
            if (!st.Captured) return Dest.Portal(g, s.Road >= 0 ? s.Road : 0);
            return Dest.Home;
        }

        while (!st.Finished)
        {
            for (int i = 0; i < st.Squads.Length && !st.Finished; i++)
            {
                Dest dest = DestFor(i);
                Map11Orders.Order order = Map11Orders.Plan(st, i, dest);
                if (order == Map11Orders.Order.Move) moves++;
                if (Map11Orders.Apply(st, i, dest, order) is { } node) Fight(i, node, false);
            }
            if (st.Finished) break;
            foreach (Map11State.Encounter e in st.AdvanceFoes())
            {
                if (st.Finished) break;
                if (!e.Intercept) { Fight(e.Squad, e.Node, false); continue; }
                while (!st.Finished && st.NextInterceptor(e.Node) is var j && j >= 0)
                {
                    Fight(j, e.Node, true);
                    if (e.Node.Cleared) break;
                }
                st.CloseIntercept(e.Node);
            }
        }
        return new ToyOnce(st.Won, st.Fallen, st.Turn, moves);
    }
}
