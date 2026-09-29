using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FS = FireScaleDiag;

// firelevel phase0 —— Q0-7（R0 ＝ 規定の駒で、固定席の台を数える）。**盤面は動かさない。**
static partial class FireLevelDiag
{
    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第242期 Phase 0 —— Q0-7（R0 ＝ 規定の駒・seed 0..199・verbose）");
        Console.WriteLine();
        var boards = new (string Name, Formation F)[]
        {
            ("T3-238", FS.T3), ("T3-1", FS.T31), ("雷＋ボルグ", FS.ThunderBorg), ("参考 移動", BA.RefMove), ("参考 雷", BA.RefThunder),
        };
        foreach (var (bn, f) in boards) Console.WriteLine($"- {bn}: {BA.SeatsNamed(f)}");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 倍率 | 勝率 | 全員生存 | 決着T | ボルグ攻撃/戦 | 1振りで当てた敵 | 燃えている敵 T1 ／ T2 ／ T3 ／ T4（頭） | ホタ 攻撃/戦 | ホタ 1発最大 | ホタ 1Tの総量（平均・最大） | ボルグ 1発最大 | ボルグ 1Tの総量 | ヒヨ手番/戦 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");
        var waves = new[] { BA.MainWave, 5, WaveHush, WaveYoke };
        foreach (var (bn, f) in boards)
            foreach (int w in waves)
                for (int s = 0; s < BA.Scales.Length; s++)
                {
                    if (w != BA.MainWave && s != 0) continue;
                    var st = new P0Agg();
                    var parts = new P0Agg[BA.Seeds];
                    Parallel.For(0, BA.Seeds, seed =>
                    {
                        var (r, p, e) = Fight(f, w, BA.Scales[s].Sc, seed);
                        var a = new P0Agg(); a.Take(r, p, e); parts[seed] = a;
                    });
                    foreach (var a in parts) st.Merge(a);
                    string burn = string.Join(" ／ ", Enumerable.Range(1, 4).Select(t => Per(st.FoeBurn[t], st.N)));
                    Console.WriteLine($"| {bn} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Pct(st.Wins, st.N)} | {Pct(st.AllSurv, st.N)} | {Per(st.Turns, st.Wins == 0 ? st.N : st.N)} | "
                        + $"{Per(st.BorgAtk, st.N)} | {Per(st.BorgFoesHit, st.BorgAtk)} | {burn} | {Per(st.HotaAtk, st.N)} | {st.HotaMax} | {Per(st.HotaTurnSum, st.HotaTurnN)}・{st.HotaTurnMax} | "
                        + $"{st.BorgMax} | {Per(st.BorgTurnSum, st.BorgTurnN)}・{st.BorgTurnMax} | {Per(st.HiyoHands, st.N)} |");
                }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    sealed class P0Agg
    {
        public long N, Wins, AllSurv, Turns, BorgAtk, BorgFoesHit, HotaAtk, HotaTurnSum, HotaTurnN, BorgTurnSum, BorgTurnN, HiyoHands;
        public long HotaMax, HotaTurnMax, BorgMax, BorgTurnMax;
        public readonly long[] FoeBurn = new long[31];
        public void Merge(P0Agg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; Turns += o.Turns; BorgAtk += o.BorgAtk; BorgFoesHit += o.BorgFoesHit;
            HotaAtk += o.HotaAtk; HotaTurnSum += o.HotaTurnSum; HotaTurnN += o.HotaTurnN; BorgTurnSum += o.BorgTurnSum; BorgTurnN += o.BorgTurnN; HiyoHands += o.HiyoHands;
            HotaMax = Math.Max(HotaMax, o.HotaMax); HotaTurnMax = Math.Max(HotaTurnMax, o.HotaTurnMax);
            BorgMax = Math.Max(BorgMax, o.BorgMax); BorgTurnMax = Math.Max(BorgTurnMax, o.BorgTurnMax);
            for (int i = 0; i < FoeBurn.Length; i++) FoeBurn[i] += o.FoeBurn[i];
        }
        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e)
        {
            N++; Turns += r.Turns;
            if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            for (int t = 0; t < FoeBurn.Length; t++) FoeBurn[t] += r.Brittle.BurnByTurn[0][t];
            int? borg = p.FirstOrDefault(u => u.Def.Id == "borg")?.InstanceId, hota = p.FirstOrDefault(u => u.Def.Id == "hota")?.InstanceId;
            var foes = e.Select(u => u.InstanceId).ToHashSet();
            var perTurn = new Dictionary<(int Id, int T), long>();
            HashSet<int>? cur = null; int curActor = -1;
            foreach (var ev in r.Events)
            {
                if (ev.Kind == BattleEventKind.Attack)
                {
                    if (cur is not null && curActor == borg) BorgFoesHit += cur.Count;
                    cur = new HashSet<int>(); curActor = ev.ActorId ?? -1;
                    if (curActor == borg) BorgAtk++;
                    if (curActor == hota) HotaAtk++;
                    continue;
                }
                if (ev.Kind == BattleEventKind.Damage && ev.ActorId is int a && ev.TargetId is int tg && foes.Contains(tg))
                {
                    int amt = ev.Amount;
                    if (cur is not null && a == curActor) cur.Add(tg);
                    perTurn[(a, ev.Turn)] = perTurn.GetValueOrDefault((a, ev.Turn)) + amt;
                    if (a == hota) HotaMax = Math.Max(HotaMax, amt);
                    if (a == borg) BorgMax = Math.Max(BorgMax, amt);
                }
            }
            if (cur is not null && curActor == borg) BorgFoesHit += cur.Count;
            foreach (var ((id, _), v) in perTurn)
            {
                if (id == hota) { HotaTurnSum += v; HotaTurnN++; HotaTurnMax = Math.Max(HotaTurnMax, v); }
                if (id == borg) { BorgTurnSum += v; BorgTurnN++; BorgTurnMax = Math.Max(BorgTurnMax, v); }
            }
            int? hiyo = p.FirstOrDefault(u => u.Def.Id == "hiyo")?.InstanceId;
            HiyoHands += r.Hands.Count(h => h.ActorId == hiyo);
        }
    }
}
