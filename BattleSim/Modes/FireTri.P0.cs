using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// firetri phase0 —— Q0-4（段2・段3 の台本の区別）・Q0-5（§6 の台を T0 ＝ 前段の規定の駒で数える）。盤面は1ビットも動かさない。
static partial class FireTriDiag
{
    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var t0 = VerOf("T0");
        Console.WriteLine("# 第247期 Phase 0 —— T0（前段の規定）で数える");
        Console.WriteLine();
        var boards = new (string Name, Formation F)[] { ("T3-244", Apply(T3244, t0)), ("T3-238", Apply(T3238, t0)), ("雷＋ボルグ", Apply(ThunderBorg, t0)) };

        // ---- Q0-4 ----
        Console.WriteLine("## Q0-4 段2（火槍）と段3（大火槍）の台本（九/新兵 × 400/300 × seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 台 | ホタの手番/戦 段1 ／ 段2 火槍 ／ 段3 大火槍 ／ 臨界 ／ 焼き尽くす ／ 残り火 | 見出し/戦 「段」Amount=2 ／ 「大火槍」／ 「臨界」 | 大火槍の手番のうち見出しあり % |");
        Console.WriteLine("|---|---|---|--:|");
        string? sample = null;
        foreach (var (bn, f) in boards.Take(2))
        {
            var c = Measure(f, BA.MainWave, BA.Scales[1].Sc).C;
            long st2 = 0, lance = 0, crit = 0, lanceHands = 0, lanceHead = 0; long nb = 0;
            for (int seed = 0; seed < BA.Seeds; seed++)
            {
                var (r, p, _) = FC.Fight(f, BA.MainWave, BA.Scales[1].Sc, seed);
                nb++;
                int hota = p.First(u => u.Def.Id == "hota").InstanceId;
                var ev = r.Events;
                foreach (var h in r.Hands.Where(h => h.ActorId == hota))
                {
                    var seg = ev.Skip(h.EventStart).Take(h.EventEnd - h.EventStart).ToList();
                    var stg = seg.FirstOrDefault(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Stage && x.ActorId == hota);
                    if (stg is null) continue;
                    if (stg.Amount == 2) st2++;
                    if (stg.Amount == 3) { lanceHands++; if (seg.Any(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Lance)) lanceHead++; }
                    if (sample is null && stg.Amount == 3)
                        sample = string.Join("\n", seg.Take(8).Select(x => $"    {x.Kind} T{x.Turn} actor={x.ActorId} target={x.TargetId} amount={x.Amount} slot={x.Slot} {x.Text} {(x.Kind == BattleEventKind.Attack ? x.Pattern.ToString() : "")}"));
                }
                lance += ev.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Lance);
                crit += ev.Count(x => x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Critical);
            }
            Console.WriteLine($"| {bn} | {string.Join(" ／ ", new[] { 1, 2, 4, 5, 6, 7 }.Select(k => Per(c.HandN[k], c.N)))} | {Per(st2, nb)} ／ {Per(lance, nb)} ／ {Per(crit, nb)} | {Pct(lanceHead, lanceHands)} |");
        }
        Console.WriteLine();
        Console.WriteLine("大火槍の手番の台本の頭（例）:");
        Console.WriteLine();
        Console.WriteLine("```");
        Console.WriteLine(sample ?? "（無し）");
        Console.WriteLine("```");
        Console.WriteLine();

        // ---- Q0-5 ----
        Console.WriteLine("## Q0-5 台の数え物（T0・九/新兵 ／ 本編 第四波 ／ 重い波 × 200/200 ／ 400/300 ＋ 的・一）");
        Console.WriteLine();
        Console.WriteLine("「緩い三角」＝ 焼き尽くす → ボルグへのギフト → 放つ → 呼び火（指名を問わない・次の焼き尽くすまで）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 倍率 | 決着T | 焼き尽くす ／ 放つ ／ 残り火 /戦 | 緩い三角/戦 | ギフトの手番/戦（火勢3 ／ 4） | 相手 1体目 ボルグ ／ ホタ ／ 相方 % | 2体目 ボルグ ／ ホタ % | ボルグがギフトを受けたときの火勢 1 ／ 2 ／ 3 ／ 4 %（受けた/戦） | そのうち放った % | 火勢3 で準備の味方が2体いた/戦 | ヒヨの火勢（周回の頭）T1 ／ T2 ／ T3 ／ T4 |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|---|---|---|--:|--:|---|");
        foreach (var (bn, f) in boards)
            foreach (int w in new[] { BA.MainWave, WaveYoke, WaveHeavy, WaveTarget1 })
                for (int s = 0; s < (IsTarget(w) ? 1 : 2); s++)
                {
                    var cell = Measure(f, w, IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc);
                    var c = cell.C; var t = cell.T;
                    long s1 = Enumerable.Range(0, 4).Sum(r => t.GiftSlotRole[1, r]), s2 = Enumerable.Range(0, 4).Sum(r => t.GiftSlotRole[2, r]);
                    long bg = t.BorgGiftLv.Sum();
                    string turns = IsTarget(w) ? "—" : Per(c.Turns, c.N);
                    string lv(int tt) => t.HiyoLvCnt[tt] == 0 ? "—" : ((double)t.HiyoLvSum[tt] / t.HiyoLvCnt[tt]).ToString("F2");
                    Console.WriteLine($"| {bn} | {WaveNames[w]} | {(IsTarget(w) ? "—" : BA.Scales[s].Name)} | {turns} | {Per(t.Burnouts, t.N)} ／ {Per(t.Unleashes, t.N)} ／ {Per(t.Embers, t.N)} | {Per(t.Loose, t.N)} | {Per(t.GiftHands, t.N)}（{Per(t.GiftAtLevel[3], t.N)} ／ {Per(t.GiftAtLevel[4], t.N)}） | {Pct(t.GiftSlotRole[1, 0], s1)} ／ {Pct(t.GiftSlotRole[1, 1], s1)} ／ {Pct(t.GiftSlotRole[1, 3], s1)} | {Pct(t.GiftSlotRole[2, 0], s2)} ／ {Pct(t.GiftSlotRole[2, 1], s2)} | {string.Join(" ／ ", Enumerable.Range(1, 4).Select(l => Pct(t.BorgGiftLv[l], bg)))}（{Per(bg, t.N)}） | {Pct(t.BorgGiftUnleash, bg)} | {(IsTarget(w) ? "—" : Per(t.GiftPairChance, t.N))} | {lv(1)} ／ {lv(2)} ／ {lv(3)} ／ {lv(4)} |");
                }
        Console.WriteLine();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
    }
}
