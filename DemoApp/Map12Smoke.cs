using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// =====================================================================================
// 1-2 の門（第440期 §5-2・`--map12-smoke`）
//
// **画面を1つも作らない。** `MapStage.Stage12` の盤と `Map11State` / `Map11Orders` だけを回す（seed 0 から）。
// **ここには Godot の型を1つも使わない**（`MapGraphSmoke` と同じ扱い）。
//
//   (a) 盤 10 マス（拠点 1 ＋ 9）＋ 敵の拠点・経路 3 本・横の辺 2 本が両方向
//   (b) N1 → S1 の最短路が N1 → C1 → S1（拠点を通らない）
//   (c) 中の経路の先頭（物見・C1）が 2 刻で拠点に着き、迎撃が立つ
//   (d) 貪欲方針 100 seed で落ちない。踏破率・湧いた数・横の辺を通った手・作戦ターンの中央値は**報告だけ**（線は置かない）
//   (e) 待ち伏せ: 止まっている隊のマスに北の先頭が入って戦闘になる（敵は横の辺を使わない）
//
// 加えて、画面の敵の矢印（`Map11State.PlannedFoeSteps`）が実際の前進（`AdvanceFoes`）と一致することを、
// (d) の参考の方針の通し全部で数える。
// =====================================================================================

public static class Map12Smoke
{
    public const int Seeds = 100;

    /// <summary>器具の貪欲方針の割り当て（カド隊 → 南 ／ かき回し隊 → 北・控えは全滅した道へ）——1-1 の「正」と同じ意味。</summary>
    public static readonly int[] Straight12 = { 2, 0 };

    public static bool Run(Action<string> write)
    {
        bool ok = true;
        void Req(bool c, string what) { write($"{(c ? "○" : "×")} {what}"); ok &= c; }
        MapStage stage = MapStage.Stage12;
        string Names(MapGraph g, IEnumerable<int> path) => string.Join(" → ", path.Select(id => g[id].Name));

        // ---- (a) 盤 ----
        {
            var g = stage.Board(true);
            var off = stage.Board(false);
            var cross = g.Edges.Where(e => g.IsCross(e.A, e.B)).ToList();
            write($"  盤: マス {g.Cells.Count}（拠点なしの盤 {off.Cells.Count}）・経路 {g.Lanes.Count} 本 × {string.Join("/", g.Lanes.Select(l => l.Length))}"
                + $"・横の辺 {string.Join("・", cross.Select(e => $"{g[e.A].Name}–{g[e.B].Name}"))}");
            Req(off.Cells.Count == 10 && g.Cells.Count == 11 && g.HasPortal && g.Lanes.Count == 3
                && g.Lanes.All(l => l.Length == Map11.RoadCells) && stage.LaneCount == 3 && stage.TotalNodes == 6,
                "(a) 盤 10 マス（拠点 1 ＋ 9）＋ 敵の拠点・経路 3 本 × 3 マス・区画 6");
            Req(cross.Count == 2
                && cross.Any(e => Pair(e, g.Lanes[0][1], g.Lanes[1][1])) && cross.Any(e => Pair(e, g.Lanes[1][1], g.Lanes[2][1]))
                && g.Cells.All(c => c.Neighbors.All(n => g[n].Neighbors.Contains(c.Id))),
                "(a) 横の辺は N1–C1・C1–S1 の 2 本で、すべての辺が両方向");
            Req(Enumerable.Range(0, 3).All(r => g.Path(g.Home, -1, g.Lanes[r][0], -1).Count == 2)
                && Enumerable.Range(0, 3).All(r => g[g.Lanes[r][^1]].Neighbors.Contains(g.Portal)),
                "(a) 拠点はどの経路の手前とも繋がり、3 経路の奥は敵の拠点で合流する");
        }

        // ---- (b) 最短路 ----
        {
            var g = stage.Board(true);
            int n1 = g.Lanes[0][1], c1 = g.Lanes[1][1], s1 = g.Lanes[2][1];
            var p = g.Path(n1, -1, s1, -1);
            write($"  N1 → S1: {Names(g, p)}");
            Req(p.SequenceEqual(new[] { n1, c1, s1 }), "(b) N1 → S1 の最短路は N1 → C1 → S1（拠点を通らない）");

            // 横の辺で移った隊の経路（`Road`）が、移った先の経路に書き換わる（§1-3）。
            var st = new Map11State(0, stage.Time, stage.Portal, stage: stage);
            Map11State.Squad s = st.Squads[0];
            st.Nodes[0][0].Cleared = true;   // 北の 1 戦目を抜いた体にする（N1 に立てるように）
            st.Nodes[1][0].Cleared = true;   // 中の 1 戦目も（C1 を素通りできるように）
            st.Nodes[2][0].Cleared = true;
            var trail = new List<string>();
            Dest dest = Dest.Of(g, s1);
            for (int i = 0; i < 6 && !st.AtDest(0, dest); i++)
            {
                Map11Orders.Order o = Map11Orders.Plan(st, 0, dest);
                Map11Orders.Apply(st, 0, dest, o);
                trail.Add($"{o}:{g[s.Pos].Name}({st.Stage.LaneNames[s.Road][..1]})");
            }
            write($"  拠点 → S1（1 戦目を抜いた盤）: {string.Join(" → ", trail)}");
            Req(st.AtDest(0, dest) && s.Road == 2, "(b') 行き先へ着いた隊の経路は、着いたマスの経路（南）になる");
            var st2 = new Map11State(0, stage.Time, stage.Portal, stage: stage);
            st2.Nodes[0][0].Cleared = st2.Nodes[1][0].Cleared = st2.Nodes[2][0].Cleared = true;
            Map11State.Squad s2 = st2.Squads[0];
            Dest toN1 = Dest.Of(g, n1);
            for (int i = 0; i < 4 && !st2.AtDest(0, toN1); i++) Map11Orders.Apply(st2, 0, toN1, Map11Orders.Plan(st2, 0, toN1));
            Map11Orders.Order side = Map11Orders.Plan(st2, 0, Dest.Of(g, s1));
            Map11Orders.Apply(st2, 0, Dest.Of(g, s1), side);
            write($"  N1 から S1 へ 1 歩: {side} → {g[s2.Pos].Name}（経路 {st2.Stage.LaneNames[s2.Road]}）");
            Req(side == Map11Orders.Order.Move && s2.Pos == c1 && s2.Road == 1,
                "(b'') N1 の隊を S1 へ向けると、横の辺で C1 へ移り（命令 Move）、経路が中に書き換わる");
        }

        // ---- (c) 中の先頭が 2 刻で拠点に ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, stage: stage);
            var g = st.Graph;
            Map11State.Node front = st.Nodes[1][0];
            var trail = new List<string> { g[front.Pos].Name };
            Map11State.Encounter? hit = null;
            int steps = 0;
            while (steps < 6 && hit is null)
            {
                steps++;
                hit = st.AdvanceFoes().FirstOrDefault(e => e.Intercept && ReferenceEquals(e.Node, front));
                trail.Add(g[front.Pos].Name);
            }
            write($"  中の先頭（{front.Def.Name}）: {string.Join(" → ", trail)}（{steps} 刻）");
            Req(front.Def.Name == EnemyCatalog.Stages[0].Name && hit is not null && steps == 2 && front.Pos == g.Home
                && st.NextInterceptor(front) >= 0,
                "(c) 中の経路の先頭（物見・C1）は 2 刻で拠点に着き、迎撃が立つ");
        }

        // ---- (e) 待ち伏せ ----
        {
            var st = new Map11State(0, stage.Time, stage.Portal, stage: stage);
            var g = st.Graph;
            int n0 = g.Lanes[0][0];
            Dest wait = Dest.Of(g, n0);
            Map11Orders.Order o = Map11Orders.Plan(st, 0, wait);
            Map11State.Node? met = Map11Orders.Apply(st, 0, wait, o);
            bool parked = st.Squads[0].Pos == n0 && met is null && st.AtDest(0, wait);
            Map11State.Node north = st.Nodes[0][0];
            var enc = st.AdvanceFoes();
            Map11State.Encounter? amb = enc.FirstOrDefault(e => !e.Intercept && e.Squad == 0);
            write($"  待ち伏せ: 隊を {g[n0].Name} に止める（{o}・接敵 {(met is null ? "なし" : met.Def.Name)}）→ 敵の前進で {north.Def.Name} が "
                + $"{g[north.Pos].Name} へ（戦闘 {(amb is null ? "なし" : "あり")}）");
            Req(parked && amb is not null && ReferenceEquals(amb.Node, north) && north.Pos == n0
                && st.Prepare(0, amb.Node) is not null,
                "(e) 止まっている隊のマスに北の先頭が入って戦闘になる（指示書は N1 だが、北の先頭の初期位置が N1 なので N0 で待つ形で確かめた）");
        }

        // ---- (d) 100 seed（報告のみ） ----
        {
            Map11Time.Stat? greedy = null;
            string? crash = null;
            try
            {
                greedy = Map11Time.Band(Map11Time.Policy.Greedy, stage.Time, Straight12, Seeds, 0, stage.Portal, stage);
            }
            catch (Exception ex) { crash = $"{ex.GetType().Name}: {ex.InnerException?.Message ?? ex.Message}"; }
            if (greedy is { } gs)
                write($"  貪欲（器具 `Map11Time`・カド隊 → 南 ／ かき回し隊 → 北）× {Seeds} seed: 踏破 {gs.Full:F1}% ／ 陥落 {gs.Fall:F1}% ／ "
                    + $"制圧 {gs.Capture:F1}% ／ 湧いた数（中央値）{gs.SpawnMedian:F1} ／ 横の辺を通った手 {gs.MovesPerRun:F2} 回/通し ／ "
                    + $"作戦ターン（中央値）{gs.TurnMedian:F1} ／ 戦闘 {gs.Battles:F2} 回 ／ 膠着 {gs.Stall:F1}%");
            else write($"  × 貪欲: {crash}");
            Req(crash is null, $"(d) 貪欲方針を {Seeds} seed 回して落ちない（数字は報告のみ・線は置かない）");

            // 参考: 盤の上でいちばん近い敵へ向かう方針（第438期の玩具の盤と同じ選び方）。**制圧まで行く**ので湧きと横の辺が効く。
            int won = 0, fallen = 0, captured = 0, crashed = 0, moves = 0, spawns = 0, mismatch = 0, offLane = 0, checkedSteps = 0;
            var turns = new List<int>();
            for (int seed = 0; seed < Seeds; seed++)
            {
                try
                {
                    var r = NearRun(seed, stage);
                    if (r.Won) won++;
                    if (r.Fallen) fallen++;
                    if (r.Captured) captured++;
                    moves += r.Moves;
                    spawns += r.Spawns;
                    mismatch += r.Mismatch;
                    offLane += r.OffLane;
                    checkedSteps += r.Checked;
                    turns.Add(r.Turns);
                }
                catch (Exception ex)
                {
                    crashed++;
                    if (crashed <= 3) write($"  × seed {seed}: {ex.GetType().Name}: {ex.Message}");
                }
            }
            turns.Sort();
            double med = turns.Count == 0 ? 0 : turns.Count % 2 == 1 ? turns[turns.Count / 2]
                : (turns[turns.Count / 2 - 1] + turns[turns.Count / 2]) / 2.0;
            write($"  参考・近い敵へ × {Seeds} seed: 落ちた {crashed} ／ 踏破 {100.0 * won / Seeds:F1}% ／ 陥落 {100.0 * fallen / Seeds:F1}% ／ "
                + $"制圧 {100.0 * captured / Seeds:F1}% ／ 湧いた数 {(double)spawns / Seeds:F2} ／ 横の辺を通った手 {(double)moves / Seeds:F2} 回/通し ／ "
                + $"作戦ターン（中央値）{med:F1}");
            Req(crashed == 0, $"参考の方針を {Seeds} seed 回して落ちない（数字は報告のみ）");
            write($"  敵の矢印の予定と実際の前進: 照合 {checkedSteps} 回の前進 ／ 食い違い {mismatch} ／ 経路の外へ出た敵 {offLane}");
            Req(mismatch == 0 && checkedSteps > 0, "敵の矢印（`PlannedFoeSteps`）は実際の前進（`AdvanceFoes`）と 1 件も食い違わない");
            Req(offLane == 0, "敵は経路の外（横の辺の先）へ 1 度も出ない");
        }
        return ok;
    }

    private static bool Pair((int A, int B) e, int x, int y) => (e.A == x && e.B == y) || (e.A == y && e.B == x);

    private readonly record struct NearOnce(bool Won, bool Fallen, bool Captured, int Turns, int Moves, int Spawns,
                                            int Mismatch, int OffLane, int Checked);

    /// <summary>
    /// 参考の方針で 1 マップ: 各隊は盤の上でいちばん近い敵部隊のマスを行き先にする（拠点に着いた敵は迎撃が受ける）。
    /// 敵が尽きたら敵の拠点へ（制圧後は拠点へ戻る）。控えは隊が全滅してから出す。規則は `Map11State` / `Map11Orders` のまま。
    /// <b>敵が動くたびに、前もって引いた予定（<see cref="Map11State.PlannedFoeSteps"/>）と実際の位置を照合する。</b>
    /// </summary>
    private static NearOnce NearRun(int seed, MapStage stage)
    {
        var st = new Map11State(seed, stage.Time, stage.Portal, stage: stage);
        var g = st.Graph;
        int moves = 0, mismatch = 0, offLane = 0, checkedSteps = 0;

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

        IEnumerable<Map11State.Node> AllFoes()
            => Enumerable.Range(0, st.Stage.LaneCount).SelectMany(r => st.FoesOn(r));

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

            // 予定を引いてから動かし、動いた敵と行き先を照合する。
            var plan = st.PlannedFoeSteps();
            var before = new Dictionary<Map11State.Node, int>(ReferenceEqualityComparer.Instance);
            foreach (Map11State.Node n in AllFoes()) before[n] = n.Pos;
            var encounters = st.AdvanceFoes();
            if (!st.Fallen)
            {
                checkedSteps++;
                var planned = new Dictionary<Map11State.Node, int>(ReferenceEqualityComparer.Instance);
                foreach (var (pn, to) in plan) planned[pn] = to;
                foreach (var (n, was) in before)
                {
                    bool movedNow = n.Pos != was;
                    if (planned.TryGetValue(n, out int dst) ? n.Pos != dst : movedNow) mismatch++;
                }
            }
            foreach (Map11State.Node n in AllFoes())
                if (n.Pos != g.Home && n.Pos != g.Portal && g[n.Pos].Lane != n.Def.Road) offLane++;

            foreach (Map11State.Encounter e in encounters)
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
        return new NearOnce(st.Won, st.Fallen, st.Captured, st.Turn, moves, st.SpawnCount, mismatch, offLane, checkedSteps);
    }
}
