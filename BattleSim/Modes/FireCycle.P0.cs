using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;

// firecycle phase0 —— Q0-4（重い波の決着T）・Q0-5（§6 の台を Q0 ＝ 規定の駒で数える）。盤面は1ビットも動かさない。
static partial class FireCycleDiag
{
    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var q0 = VerOf("Q0");
        Console.WriteLine("# 第246期 Phase 0 —— Q0（前段の規定）で数える");
        Console.WriteLine();
        var boards = new (string Name, Formation F)[] { ("T3-244", Apply(T3244, q0)), ("T3-238", Apply(T3238, q0)), ("雷＋ボルグ", Apply(ThunderBorg, q0)), ("参考 移動", BA.RefMove), ("参考 雷", BA.RefThunder) };

        Console.WriteLine("## Q0-4 重い波（城塞の重装兵 ×3・X 字の前1・前3・中央・特性なし）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 倍率 | 勝率 ／ 全員生存 | 決着T（勝ち） | 3体の与ダメ/戦 ボルグ ／ ホタ ／ ヒヨ | ヨミ ／ セロ |");
        Console.WriteLine("|---|---|---|---|--:|---|---|");
        foreach (var (bn, f) in boards)
            foreach (int w in new[] { WaveHeavy, WaveYoke })
                for (int s = 0; s < BA.Scales.Length; s++)
                {
                    var (_, c, _) = Measure(f, w, BA.Scales[s].Sc);
                    Console.WriteLine($"| {bn} | {WaveNames[w]} | {BA.Scales[s].Name} | {Pct(c.Wins, c.N)} ／ {Pct(c.AllSurv, c.N)} | {Per(c.Turns, c.N)} | {D(c, "borg")} ／ {D(c, "hota")} ／ {D(c, "hiyo")} | {D(c, "yomi")} ／ {D(c, "sero")} |");
                }
        Console.WriteLine();

        Console.WriteLine("## Q0-5 台の数え物（Q0・九/新兵 ／ 本編 第四波 ／ 重い波 × 200/200 ／ 400/300）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 倍率 | 決着T | ギフト/戦（初めてのT） | 間隔 1 ／ 2 ／ 3 ／ 4+ % | 焼き尽くす ／ 放つ /戦 | 焼き尽くす → 放つ /戦 | 煽りの相手 ボルグ ／ ホタ ／ 相方 % | 煽りで型が変わった % | ギフトの相手が大技の準備 % | ホタの手番 段1 ／ 段2 ／ 5連撃 ／ 焼き尽くす ／ 残り火 /戦 | ホタ 段2・3・4 に届いたT | ギフトの手番で倒れた敵 /手番 |");
        Console.WriteLine("|---|---|---|--:|---|---|---|--:|---|--:|--:|---|---|--:|");
        foreach (var (bn, f) in boards.Take(3))
            foreach (int w in new[] { BA.MainWave, WaveYoke, WaveHeavy })
                for (int s = 0; s < 2; s++)
                {
                    var (_, c, _) = Measure(f, w, BA.Scales[s].Sc);
                    long iv = c.Interval.Sum();
                    string first = c.GiftBattles == 0 ? "—" : Per(Enumerable.Range(0, TM + 1).Sum(t => (long)t * c.FirstGift[t]), c.GiftBattles);
                    string hands = string.Join(" ／ ", new[] { 1, 2, 3, 6, 7 }.Select(k => Per(c.HandN[k], c.N)));
                    string reach = string.Join("・", Enumerable.Range(2, 3).Select(l => c.StageReachN[l] == 0 ? "—" : Per(c.StageReachT[l], c.StageReachN[l])));
                    Console.WriteLine($"| {bn} | {WaveNames[w]} | {BA.Scales[s].Name} | {Per(c.Turns, c.N)} | {Per(c.Gifts, c.N)}（{first}） | {string.Join(" ／ ", Enumerable.Range(1, 4).Select(i => Pct(c.Interval[i], iv)))} | {Per(c.Burnouts, c.N)} ／ {Per(c.Unleashes, c.N)} | {Per(c.BurnThenUnleash, c.N)} | {string.Join(" ／ ", Enumerable.Range(0, 4).Where(i => i != 2).Select(i => Pct(c.StokeRole[i], c.Stokes)))} | {Pct(c.StokeForm, c.Stokes)} | {Pct(c.GiftReady, c.GiftRecips)} | {hands} | {reach} | {Per(c.GiftKills, c.GiftHands)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 追記 的の波（Q0・1〜8 ターン目・倍率なし）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 1〜8T の与ダメ/戦 3体＋刻み ／ ヨミ＋セロ ／ 台の全員 | ターンごとの与ダメ（台の全員）T1 … T8 | 倒れた的/戦 | ギフト ／ 焼き尽くす ／ 放つ /戦 |");
        Console.WriteLine("|---|---|---|---|--:|---|");
        foreach (var (bn, f) in boards)
            foreach (int w in new[] { WaveTarget1, WaveTarget9 })
            {
                var (_, c, _) = Measure(f, w, EnemyScaleRule.None);
                long trio = new[] { "borg", "hota", "hiyo", "tick" }.Sum(k => c.TDealt.TryGetValue(k, out var a) ? a.Sum() : 0);
                long ys = new[] { "yomi", "sero" }.Sum(k => c.TDealt.TryGetValue(k, out var a) ? a.Sum() : 0);
                long all = c.TDealt.Values.Sum(a => a.Sum());
                string turns = string.Join(" ", Enumerable.Range(1, TM).Select(t => ((double)c.TDealt.Values.Sum(a => a[t]) / c.N).ToString("F0")));
                Console.WriteLine($"| {bn} | {WaveNames[w]} | {Per(trio, c.N)} ／ {Per(ys, c.N)} ／ {Per(all, c.N)} | {turns} | {Per(c.TFoeDeaths, c.N)} | {Per(c.TGifts, c.N)} ／ {Per(c.TBurnouts, c.N)} ／ {Per(c.TUnleashes, c.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    static string D(CAgg c, string id) => c.Dealt.TryGetValue(id, out long v) ? Per(v, c.N) : "—";
}
