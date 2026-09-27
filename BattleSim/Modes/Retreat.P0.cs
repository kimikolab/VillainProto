using BattleCore;
using static Common;

// retreat phase0 —— Q0-1〜Q0-6（第225期）。前段の盤面（セロ規定化の後）・J0（規定のシオ）で回す。
static partial class RetreatDiag
{
    internal static readonly int[] StageAtProvisional = { 4, 8, 14 };

    internal sealed class P0Agg
    {
        public long N, Wins, AllSurv, WinTurns, Turns;
        public readonly long[] MovesByTurn = new long[31];
        public readonly long[] AliveBattlesAtTurn = new long[31];
        public readonly long[] ReachCount = new long[21], ReachTurn = new long[21];
        public readonly Dictionary<string, long> MoveSrc = new();
        public long TotalMoves;
        // Q0-2
        public long LowHits, LowHitsBack, LowHitsReaction, LowFirst, LowFirstBack;
        public readonly long[] LowByTurn = new long[31];
        public readonly Dictionary<string, long> LowByUnit = new(), LowBackByUnit = new();
        // Q0-4
        public long ThornGuard, MultiMove, KadoFF, KadoFFLow, KadoLowSelf;

        public void Merge(P0Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; WinTurns += o.WinTurns; Turns += o.Turns; TotalMoves += o.TotalMoves;
            for (int i = 0; i < 31; i++) { MovesByTurn[i] += o.MovesByTurn[i]; AliveBattlesAtTurn[i] += o.AliveBattlesAtTurn[i]; LowByTurn[i] += o.LowByTurn[i]; }
            for (int i = 0; i < 21; i++) { ReachCount[i] += o.ReachCount[i]; ReachTurn[i] += o.ReachTurn[i]; }
            Add(MoveSrc, o.MoveSrc); Add(LowByUnit, o.LowByUnit); Add(LowBackByUnit, o.LowBackByUnit);
            LowHits += o.LowHits; LowHitsBack += o.LowHitsBack; LowHitsReaction += o.LowHitsReaction; LowFirst += o.LowFirst; LowFirstBack += o.LowFirstBack;
            ThornGuard += o.ThornGuard; MultiMove += o.MultiMove; KadoFF += o.KadoFF; KadoFFLow += o.KadoFFLow; KadoLowSelf += o.KadoLowSelf;
        }
        static void Add(Dictionary<string, long> a, Dictionary<string, long> b) { foreach (var (k, v) in b) a[k] = a.GetValueOrDefault(k) + v; }
        public double Per(long x) => (double)x / Math.Max(1, N);

        public void Take(BattleResult r, List<UnitState> player)
        {
            N++; Turns += r.Turns;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            for (int t = 1; t <= Math.Min(30, r.Turns); t++) AliveBattlesAtTurn[t]++;
            var byId = player.ToDictionary(u => u.InstanceId);
            var slots = player.ToDictionary(u => u.InstanceId, u => u.Slot);
            var everLow = new HashSet<int>();
            var movedThisTurn = new Dictionary<(int, int), int>();
            int cum = 0;
            foreach (BattleEvent e in r.Events)
            {
                switch (e.Kind)
                {
                    case BattleEventKind.Move when e.TargetId is int mt && byId.ContainsKey(mt):
                    {
                        slots[mt] = e.Slot;
                        cum++; TotalMoves++;
                        if (e.Turn < 31) MovesByTurn[e.Turn]++;
                        if (cum <= 20) { ReachCount[cum]++; ReachTurn[cum] += e.Turn; }
                        string src = e.ActorId is int a ? (byId.TryGetValue(a, out var au) ? au.Def.Id : "敵") : "不明";
                        MoveSrc[src] = MoveSrc.GetValueOrDefault(src) + 1;
                        var k = (mt, e.Turn);
                        movedThisTurn[k] = movedThisTurn.GetValueOrDefault(k) + 1;
                        if (movedThisTurn[k] == 2) MultiMove++;
                        break;
                    }
                    case BattleEventKind.Death when e.TargetId is int d && slots.ContainsKey(d):
                        slots.Remove(d);
                        break;
                    case BattleEventKind.Intercept when e.Text == InterceptLabels.ThornGuard && e.ActorId is int ka && byId.ContainsKey(ka):
                        ThornGuard++;
                        break;
                    case BattleEventKind.Damage when e.TargetId is int dt && byId.TryGetValue(dt, out var u) && e.HpAfter > 0:
                    {
                        bool kadoFF = e.ActorId is int fa && byId.TryGetValue(fa, out var fu) && fu.Def.Id == "kado" && fu != u;
                        if (kadoFF) KadoFF++;
                        int hp = e.HpAfter; if (hp * 100 >= u.MaxHp * 40) break;
                        LowHits++;
                        if (e.Turn < 31) LowByTurn[e.Turn]++;
                        LowByUnit[u.Def.Id] = LowByUnit.GetValueOrDefault(u.Def.Id) + 1;
                        if (e.Reaction) LowHitsReaction++;
                        if (kadoFF) KadoFFLow++;
                        if (u.Def.Id == "kado") KadoLowSelf++;
                        bool back = slots.ContainsKey(dt) && BackPartner(slots[dt], slots.Where(kv => kv.Key != dt).ToDictionary(kv => kv.Key, kv => kv.Value)) is not null;
                        if (back) { LowHitsBack++; LowBackByUnit[u.Def.Id] = LowBackByUnit.GetValueOrDefault(u.Def.Id) + 1; }
                        if (everLow.Add(dt)) { LowFirst++; if (back) LowFirstBack++; }
                        break;
                    }
                }
            }
        }
    }

    internal static P0Agg MeasureP0(Formation f, Func<List<UnitState>> enemy, int seed0 = 0, int seeds = Seeds)
    {
        var total = new P0Agg();
        var gate = new object();
        Parallel.For(0, seeds, () => new P0Agg(), (j, _, local) =>
        {
            var player = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            local.Take(BattleEngine.Run(player, enemy(), seed0 + j, verbose: true), player);
            return local;
        }, local => { lock (gate) total.Merge(local); });
        return total;
    }

    internal static readonly (string Name, int[] Ws)[] Groups =
        { ("九/新兵", new[] { 4 }), ("九/農兵", new[] { 5 }), ("本編 第2〜5波", new[] { 0, 1, 2, 3 }) };

    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第225期 Phase 0（前段の盤面・J0 ＝ 規定のシオ・規定のセロ）");
        Console.WriteLine();
        var benches = new (string Name, Formation F)[] { ("M-ハネ（仮）", RawHane), ("M-カド（仮）", RawKado), ("参考 ガルド（D1）", RefGald) };
        Console.WriteLine("## Q0-6 台（仮の並び＝第222期 D1 のガルドの席に5枠目）");
        Console.WriteLine();
        foreach (var (n, f) in benches)
            Console.WriteLine($"- {n}: {SeatsNamed(f)} ／ HP 合計 {f.Occupied().Sum(o => o.Def.MaxHp)} ／ 攻 合計 {f.Occupied().Sum(o => o.Def.Attack)}");
        Console.WriteLine();
        foreach (var d in new[] { UnitCatalog.BasaK0, UnitCatalog.Yomi, UnitCatalog.Shio, UnitCatalog.Sero, UnitCatalog.HaneK0, UnitCatalog.Kado, UnitCatalog.Gald })
            Console.WriteLine($"- {d.Name}: HP {d.MaxHp} ／ 攻 {d.Attack} ／ 速 {d.Speed} ／ 札 {string.Join(", ", d.Traits)}");
        Console.WriteLine();

        var res = new Dictionary<(int B, int S, int W), P0Agg>();
        foreach (int b in Enumerable.Range(0, benches.Length))
            foreach (int s in Enumerable.Range(0, Scales.Length))
                foreach (int w in Enumerable.Range(0, WaveNames.Length))
                    res[(b, s, w)] = MeasureP0(benches[b].F, WaveOf(w, Scales[s].Sc));
        P0Agg G(int b, int s, int[] ws) { var a = new P0Agg(); foreach (int w in ws) a.Merge(res[(b, s, w)]); return a; }

        Console.WriteLine("## Q0-1 味方の移動の累計（J0・1戦あたり。累計 k 回に届いた戦の割合 ／ 届いたターンの平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 勝率 | 全員生存 | 決着T | 移動 計 | T1 | T2 | T3 | T4 | T5 | T6〜 | 4 回 | 8 回 | 14 回 | 出どころ |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|---|---|");
        foreach (int b in Enumerable.Range(0, benches.Length))
            foreach (int s in Enumerable.Range(0, Scales.Length))
                foreach (var (gn, ws) in Groups)
                {
                    var a = G(b, s, ws);
                    string R(int k) => a.ReachCount[k] == 0 ? "0%" : $"{100.0 * a.ReachCount[k] / a.N:F0}% ／ T{(double)a.ReachTurn[k] / a.ReachCount[k]:F1}";
                    long late = Enumerable.Range(6, 25).Sum(t => a.MovesByTurn[t]);
                    string src = string.Join(" ", a.MoveSrc.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {a.Per(kv.Value):F2}"));
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} | {gn} | {100.0 * a.Wins / a.N:F1} | {100.0 * a.AllSurv / a.N:F1} | {F2(a.Wins == 0 ? double.NaN : (double)a.WinTurns / a.Wins)} "
                        + $"| {a.Per(a.TotalMoves):F2} | " + string.Join(" | ", Enumerable.Range(1, 5).Select(t => $"{a.Per(a.MovesByTurn[t]):F2}")) + $" | {a.Per(late):F2} "
                        + $"| {R(4)} | {R(8)} | {R(14)} | {src} |");
                }
        Console.WriteLine();
        Console.WriteLine("移動は**動かされた駒1体につき1回**（入れ替えなら2回）。出どころは動かした駒（`Move` の `ActorId`）。");
        Console.WriteLine();

        Console.WriteLine("## Q0-2 味方の HP が4割を切った被弾（J0・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("「切った被弾」＝その一撃の後に生きていて HP が最大HPの 4 割未満（応急処置の `Needs` と同じ・跨いだ一撃に限らない）。");
        Console.WriteLine("「後ろ側」＝その瞬間に隣接かつより後ろの行に生きている味方がいる（シオの組み替えと同じ規則）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 切った被弾 | うち後ろ側あり | うち反撃・割り込みの中 | 初めて切った駒 | うち後ろ側あり | T1 | T2 | T3 | T4 | T5 | 駒別（後ろ側あり ／ 計） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (int b in Enumerable.Range(0, benches.Length))
            foreach (int s in Enumerable.Range(0, Scales.Length))
                foreach (var (gn, ws) in Groups)
                {
                    var a = G(b, s, ws);
                    string unit = string.Join(" ", a.LowByUnit.OrderByDescending(kv => kv.Value)
                        .Select(kv => $"{kv.Key} {a.Per(a.LowBackByUnit.GetValueOrDefault(kv.Key)):F2}／{a.Per(kv.Value):F2}"));
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} | {gn} | {a.Per(a.LowHits):F2} | {a.Per(a.LowHitsBack):F2} | {a.Per(a.LowHitsReaction):F2} "
                        + $"| {a.Per(a.LowFirst):F2} | {a.Per(a.LowFirstBack):F2} | " + string.Join(" | ", Enumerable.Range(1, 5).Select(t => $"{a.Per(a.LowByTurn[t]):F2}")) + $" | {unit} |");
                }
        Console.WriteLine();

        Console.WriteLine("## Q0-3 ツギの応急処置が手番を失うターンに起きるか（実装から）");
        Console.WriteLine();
        Console.WriteLine("- 応急処置（`FirstAidTrait.Try`）は `ctx.CanActOutOfTurn(ツギ, OutOfTurnRoute.FirstAid)` を通る。止まるのは **痺れ（`Stun`）・組み付き（`Grappled`）・粛（第二波）・`CanReact` が偽の札**。");
        Console.WriteLine("- **転倒（`Stagger`）・竦み（`Cowed`）では止まらない**——この2つは `TakeTurnCore` の頭で自分の手番を落とすだけで、ターン外の問い（`CanActOutOfTurn`）には入っていない。");
        Console.WriteLine("- 反撃（`InReaction`）・割り込み（`InInterrupt`）の中では貼らない（回数も使わない）。1ターンの回数は `ctx.Turn` で数える。倒れていれば出ない（`!self.IsAlive`）。敵が 0 体なら出ない。");
        Console.WriteLine("- → 緊急退避もこの形に揃える: `CanActOutOfTurn(シオ, OutOfTurnRoute.Retreat)`・反撃／割り込みの中では出ない・`ctx.Interrupt` で包む。");
        Console.WriteLine();

        Console.WriteLine("## Q0-4 カドの身代わりと緊急退避の重なり（J0・M-カド・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | 身代わり（入れ替え） | 同じ駒が1ターンに2回以上動いた | カドの棘の巻き込み（味方へ） | うち4割を切らせた | カド自身が4割を切った被弾 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (int s in Enumerable.Range(0, Scales.Length))
            foreach (var (gn, ws) in Groups)
            {
                var a = G(1, s, ws);
                Console.WriteLine($"| {Scales[s].Name} | {gn} | {a.Per(a.ThornGuard):F2} | {a.Per(a.MultiMove):F2} | {a.Per(a.KadoFF):F2} | {a.Per(a.KadoFFLow):F2} | {a.Per(a.KadoLowSelf):F2} |");
            }
        Console.WriteLine();
        Console.WriteLine("順番（実装から）: 敵の単体の一撃が身代わりの範囲の味方へ → 標的選択でカドへ差し替え（入れ替えはまだ）→ `ApplyDamage(カド)` → **カドの `OnDamaged`**: 棘守りの入れ替え（`SwapSlots`・2体の `Move`・移り木・ヨミ・セロの段）→ 棘の反撃（`Reaction` の中・巻き込みが隣の味方に `ApplyDamage`）→ **味方の `OnAllyDamaged`**（シオの緊急退避の判定はここ）。");
        Console.WriteLine("- **巻き込みで4割を切らせた被弾は反撃（`Reaction`）の中**なので、応急処置に揃えると緊急退避は出ない（`切った被弾` の「反撃・割り込みの中」の列）。出るのは元の一撃を受けたカド自身の判定だけ。");
        Console.WriteLine();

        Console.WriteLine("## Q0-5 過去の測定（`design/` を grep）");
        Console.WriteLine();
        Console.WriteLine("- 手番の外で味方を下げる機構は**ロスターに無い**。近いのは: 棘守り（カド・第19期〜・身代わりで前へ出る＝下げるのは守られた側）／ 旧セロの臆病（逃げて後列の味方を突き飛ばす・第223期で外した）／ ヒサの逃げ回る（第184期・指差したら隣と入れ替わる）／ シオの組み替え（第222期・**手番の中**）。");
        Console.WriteLine("- 応急の割り込みはツギの応急処置（第210〜213期）だけ——板を貼る・動かさない。第211期の R315「回数に上限のある割り込みは、条件を広げても回数が増えない」が緊急退避の回数（1ターン1〜4回）に当たる。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒");
    }
}
