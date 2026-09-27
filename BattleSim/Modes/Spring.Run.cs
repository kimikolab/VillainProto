using BattleCore;
using static Common;

// spring run —— 表A〜F（第228期）。台 M-ハネ（H3 で選んだ総当たりの1位 ／ 第227期 L2 の1位の席）・ハネのいる compare の行・参考（雷・ポンの席）
// × 版 H0〜H3w × 波（九/新兵・九/農兵・本編の第2〜5波）× 倍率（200/200・200/115・115/115）・seed 0..199。
static partial class SpringDiag
{
    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第228期 —— 吹っ飛ばしと弾き返し（`0 spring run`）");
        Console.WriteLine();
        var vers = Versions;
        Console.WriteLine("版: " + string.Join(" ／ ", vers.Select(v => v.Tag)) + "。**線は置かない。**");
        Console.WriteLine();

        var pick = PickSeats(MHane227, VerOf("H3"));
        var top = pick[0];
        Console.WriteLine($"### 席（H3 × 九/新兵 × 200/200 × seed {PickSeed0}..{PickSeed0 + PickSeeds - 1}・120 通り）");
        Console.WriteLine();
        Console.WriteLine($"全員生存が最大と同値の並び: {pick.Count(x => x.Sv == top.Sv)} 通り。第227期 L2 の席は {pick.FindIndex(x => SeatsNamed(x.F) == SeatsNamed(Apply(MHane227, VerOf("H3")))) + 1} 位。");
        Console.WriteLine();
        Console.WriteLine($"| 順位 | 席 | 全員生存/{PickSeeds} | 落ちた駒（計） | 勝ち数 | 決着T |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|");
        for (int i = 0; i < 5; i++)
            Console.WriteLine($"| {i + 1} | {SeatsNamed(pick[i].F)} | {pick[i].Sv} | {pick[i].Fell} | {pick[i].W} | {(pick[i].W == 0 ? "—" : ((double)pick[i].T / pick[i].W).ToString("F2"))} |");
        Console.WriteLine();

        Formation topRaw = Apply(top.F, VerOf("H0"));
        var benches = new List<(string Name, Formation F, bool Versions)>
        {
            ("総当たり（H3）", topRaw, true),
            ("227 L2 の席", MHane227, true),
        };
        foreach (var (n, f) in HaneRows()) benches.Add(("compare " + n, f, true));
        benches.Add(("参考 雷（ポンの席）", Thunder, false));
        foreach (var b in benches) Console.WriteLine($"- {b.Name}: {SeatsNamed(b.F)}");
        Console.WriteLine();

        var res = new Dictionary<(int, string, int, int), Agg>();
        for (int b = 0; b < benches.Count; b++)
            foreach (var v in benches[b].Versions ? vers : vers.Take(1).ToArray())
                for (int w = 0; w < WaveNames.Length; w++)
                    for (int s = 0; s < Scales.Length; s++)
                        res[(b, v.Tag, w, s)] = Measure(Apply(benches[b].F, v), w, Scales[s].Sc);
        int[] waveOrder = { 4, 5, 0, 1, 2, 3 };
        IEnumerable<Ver> VersOf(int b) => benches[b].Versions ? vers : vers.Take(1);
        int nM = 2;   // M-ハネの台の数

        // ---- 表A ----
        Console.WriteLine("## 表A. 全員生存 ／ 勝率（%）");
        Console.WriteLine();
        foreach (int s in Enumerable.Range(0, Scales.Length))
        {
            Console.WriteLine($"### 倍率 {Scales[s].Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | " + string.Join(" | ", waveOrder.Select(w => WaveNames[w])) + " | 本編の平均 |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", waveOrder.Length)) + "---|");
            for (int b = 0; b < benches.Count; b++)
                foreach (var v in VersOf(b))
                {
                    double ms = Enumerable.Range(0, 4).Average(w => res[(b, v.Tag, w, s)].Surv), mw = Enumerable.Range(0, 4).Average(w => res[(b, v.Tag, w, s)].Win);
                    Console.WriteLine($"| {benches[b].Name} | {v.Tag} | " + string.Join(" | ", waveOrder.Select(w => res[(b, v.Tag, w, s)]).Select(a => $"{F1(a.Surv)} ／ {F1(a.Win)}")) + $" | {F1(ms)} ／ {F1(mw)} |");
                }
            Console.WriteLine();
        }
        Console.WriteLine("### 表A'. 決着T と落ちた駒（倒れた戦の割合 ／ 倒れた平均ターン）");
        Console.WriteLine();
        string[] ids = { "hane", "basa", "yomi", "shio", "sero" };
        Console.WriteLine("| 台 | 倍率・波 | 版 | 全員生存 | 勝率 | 決着T | 落ちた駒 | " + string.Join(" | ", ids) + " |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|" + string.Concat(Enumerable.Repeat("---|", ids.Length)));
        foreach (var (s, w) in new[] { (0, 4), (0, 5), (0, 0), (0, 1), (1, 4) })
            for (int b = 0; b < nM; b++)
                foreach (var v in vers)
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F1(a.Surv)} | {F1(a.Win)} | {F2(a.WinT)} | {F2(a.Per(a.FellTotal))} | "
                                      + string.Join(" | ", ids.Select(id => a.Fell.GetValueOrDefault(id) == 0 ? "0%" : $"{100.0 * a.Fell[id] / a.N:F1}% ／ T{(double)a.FellT[id] / a.Fell[id]:F1}")) + " |");
                }
        Console.WriteLine();

        var cellsBC = new[] { (0, 4), (0, 5), (0, 0), (0, 1), (0, 2), (0, 3), (1, 4) };
        // ---- 表B ----
        Console.WriteLine("## 表B. 吹っ飛ばし（1戦あたり ／ 1回あたり）");
        Console.WriteLine();
        Console.WriteLine("回数 ＝ 吹っ飛ばした経路の数（H3w の2本目を含む）。動いた ／ 前へ ／ 当たった ／ 与ダメ ／ 混乱 は1回あたり。A 撃破 ＝ 貫きで A が倒れて並べ替えなかった回。狙えず ＝ 前列の経路の席に敵がいなかった手番。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 回数/戦 | 動いた/回 | 前へ/回 | 当たった/回 | 与ダメ/回 | 混乱/回 | A 撃破 | 2本目/戦 | 狙えず/戦 | 突き返し/戦 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (s, w) in cellsBC)
            for (int b = 0; b < benches.Count - 1; b++)
                foreach (var v in vers)
                {
                    var a = res[(b, v.Tag, w, s)];
                    double n = Math.Max(1, a.Hane.GetValueOrDefault("BlastCount"));
                    string per(string k) => a.Hane.GetValueOrDefault("BlastCount") == 0 ? "—" : F2(a.Hane.GetValueOrDefault(k) / n);
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F2(a.PerK("BlastCount"))} | {per("BlastMoved")} | {per("BlastForward")} | {per("BlastHits")} | {(a.Hane.GetValueOrDefault("BlastCount") == 0 ? "—" : F1(a.Hane.GetValueOrDefault("BlastDealt") / n))} | {per("BlastConfused")} | "
                                      + $"{F2(a.PerK("BlastKilledA"))} | {F2(a.PerK("BlastSecond"))} | {F2(a.PerK("ReboundNoFront"))} | {F2(a.PerK("ReboundThrusts"))} |");
                }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C. 弾き返し（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("被弾 ＝ ハネが敵の攻撃を受けた回数（HP に届いた一撃）。前へ ＝ 入れ替えで前へ出た敵 ／ 混乱 ＝ そのうち混乱。止まった ＝ 回数の上限 ／ 粛 ／ ほか（痺れ・割り込みの中）／ 弾けない席（○前2・○後2・後列）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 被弾 | 弾いた | 前へ | 混乱 | 上限 | 粛 | ほか | 弾けない席 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (s, w) in cellsBC)
            for (int b = 0; b < benches.Count - 1; b++)
                foreach (var v in vers)
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F2(a.Per(a.HaneHits))} | {F2(a.PerK("SpringCount"))} | {F2(a.PerK("SpringForward"))} | {F2(a.PerK("SpringConfused"))} | "
                                      + $"{F2(a.PerK("SpringCapped"))} | {F2(a.PerK("SpringHushed"))} | {F2(a.PerK("SpringHeld"))} | {F2(a.PerK("SpringNoSeat"))} |");
                }
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D. ハネ（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 被ダメ | 倒れた | 入れ替わった 計 | うち弾き返しの後 | ハネの与ダメ |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|");
        foreach (var (s, w) in cellsBC)
            for (int b = 0; b < benches.Count - 1; b++)
                foreach (var v in vers)
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F1(a.Per(a.HaneTaken))} | {100.0 * a.HaneFell / a.N:F1}% | {F2(a.Per(a.OverrunSwaps))} | {F2(a.PerK("SpringSwaps"))} | {F1(a.Per(a.D.Dealt.GetValueOrDefault("hane")))} |");
                }
        Console.WriteLine();

        // ---- 表E ----
        Console.WriteLine("## 表E. 敵の乱れ（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("動かされた ＝ 敵の `Move`（出どころ別）。混乱 ＝ バサの対で立った混乱（全部）。同士討ち ＝ 混乱のまま自軍へ振った手番 ／ 敵に ＝ その与ダメ。失った ＝ 転倒 ＋ 痺れ ＋ 同士討ち。続けて ＝ 同じ敵が続けて手番を失った最大（2以上の戦 ／ 最大）。段 ＝ バサの敵の乱れの段に届いた戦の割合 ／ 平均ターン。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 動かされた | バサ | ハネ | 混乱 | 同士討ち | 敵に | 転倒した/戦 | 失った | 続けて2以上 | 最大 | 段1 | 段2 | 段3 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|---|---|");
        foreach (var (s, w) in cellsBC)
            for (int b = 0; b < benches.Count - 1; b++)
                foreach (var v in vers)
                {
                    var d = res[(b, v.Tag, w, s)].D;
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F2(d.Per(d.FoeMoves))} | {F2(d.Per(d.MoveSrc[0]))} | {F2(d.Per(d.MoveSrc[1]))} | {F2(d.Per(d.Confused))} | {F2(d.Per(d.Struck))} | {F1(d.Per(d.StruckDmgFoe))} | "
                                      + $"{F2(d.Per(d.Staggers))} | {F2(d.Per(d.LostStagger + d.LostStun + d.Struck))} | {100.0 * d.Run2 / d.N:F1}% | {d.RunMax} | {d.Reach(d.BasaReach, d.BasaTurn, 1)} | {d.Reach(d.BasaReach, d.BasaTurn, 2)} | {d.Reach(d.BasaReach, d.BasaTurn, 3)} |");
                }
        Console.WriteLine();

        Console.WriteLine("### 表E'. 第二波（粛）: 粛の伝令が倒れたターン ／ 粛で止められた行動（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 伝令が倒れた戦 | 倒れた平均ターン | 粛で止められた |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        for (int b = 0; b < benches.Count - 1; b++)
            foreach (var v in vers)
            {
                var a = res[(b, v.Tag, 0, 0)];
                Console.WriteLine($"| {benches[b].Name} | {v.Tag} | {100.0 * a.HushDead / a.N:F1}% | {(a.HushDead == 0 ? "—" : ((double)a.HushDeadTurn / a.HushDead).ToString("F2"))} | {F2(a.Per(a.HushBlocked))} |");
            }
        Console.WriteLine();

        // ---- 表F ----
        Console.WriteLine("## 表F. 目標（全員生存）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 九/新兵 200/200 | 本編の第2〜5波の平均 200/200 | 第二波 | 第三波 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        for (int b = 0; b < benches.Count; b++)
            foreach (var v in VersOf(b))
                Console.WriteLine($"| {benches[b].Name} | {v.Tag} | {F1(res[(b, v.Tag, MainWave, 0)].Surv)} | {F1(Enumerable.Range(0, 4).Average(w => res[(b, v.Tag, w, 0)].Surv))} | {F1(res[(b, v.Tag, 0, 0)].Surv)} | {F1(res[(b, v.Tag, 1, 0)].Surv)} |");
        Console.WriteLine();
        Console.WriteLine("第227期 L2（総当たりの1位・前段の前のハネ）: 九/新兵 96.0% ／ 本編の平均 86.1%。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}
