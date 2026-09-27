using BattleCore;
using static Common;

// gale run —— 表A〜E（§8.2）。
static partial class GaleDiag
{
    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第229期 gale run");
        Console.WriteLine();

        // ---- 席 ----
        var pick = PickSeats(MHane228, VerOf("G4"));
        var top = pick[0];
        int rank228 = pick.FindIndex(x => SeatsNamed(x.F) == SeatsNamed(Apply(MHane228, VerOf("G4"))));
        Console.WriteLine("## 席（G4 × 九/新兵 × 200/200 × seed 1000..1199・120 通り）");
        Console.WriteLine();
        Console.WriteLine($"- 総当たりの1位: {SeatsNamed(top.F)}（全員生存 {top.Sv}/200・落ちた駒 {top.Fell}・勝ち {top.W}）");
        Console.WriteLine($"- 全員生存が最大と同値の並び: {pick.Count(x => x.Sv == top.Sv)} 通り。第228期 H3 の席は {rank228 + 1} 位（{pick[rank228].Sv}/200）");
        Console.WriteLine("- 上位5: " + string.Join(" ／ ", pick.Take(5).Select(x => $"[{SeatsNamed(x.F)}] {x.Sv}")));
        Console.WriteLine();

        var benches = new List<(string Name, Formation F, bool All)>
        {
            ("総当たり（G4）", top.F, true),
            ("228 H3 の席", MHane228, true),
            ("参考 雷", Thunder, false),
        };
        int[] waveOrder = { 4, 5, 0, 1, 2, 3 };
        var res = new Dictionary<(int B, string V, int W, int S), Agg>();
        for (int b = 0; b < benches.Count; b++)
            foreach (var v in Versions)
            {
                if (!benches[b].All && v.Tag is not ("G0" or "G4")) continue;
                for (int s = 0; s < Scales.Length; s++)
                    foreach (int w in waveOrder)
                        res[(b, v.Tag, w, s)] = Measure(benches[b].F, v, w, Scales[s].Sc);
            }
        IEnumerable<Ver> VersOf(int b) => benches[b].All ? Versions : Versions.Where(v => v.Tag is "G0" or "G4");

        // ---- 表A ----
        Console.WriteLine("## 表A. 全員生存 ／ 勝率（%・seed 0..199）");
        Console.WriteLine();
        for (int s = 0; s < Scales.Length; s++)
        {
            Console.WriteLine($"### {Scales[s].Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | " + string.Join(" | ", waveOrder.Select(w => WaveNames[w])) + " | 本編の平均 |");
            Console.WriteLine("|---|---|" + string.Concat(waveOrder.Select(_ => "---|")) + "---|");
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
        string[] ids = { "basa", "sero", "yomi", "shio", "hane" };
        Console.WriteLine("| 台 | 倍率・波 | 版 | 全員生存 | 勝率 | 決着T | 落ちた駒/戦 | " + string.Join(" | ", ids) + " |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|" + string.Concat(Enumerable.Repeat("---|", ids.Length)));
        foreach (var (s, w) in new[] { (0, 4), (0, 5), (0, 0), (0, 1), (0, 2), (0, 3), (1, 4) })
            for (int b = 0; b < 2; b++)
                foreach (var v in Versions)
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F1(a.Surv)} | {F1(a.Win)} | {F2(a.WinT)} | {F2(a.Per(a.FellTotal))} | "
                                      + string.Join(" | ", ids.Select(id => a.Fell.GetValueOrDefault(id) == 0 ? "0%" : $"{100.0 * a.Fell[id] / a.N:F1}% ／ T{(double)a.FellT[id] / a.Fell[id]:F1}")) + " |");
                }
        Console.WriteLine();

        var cells = new[] { (0, 4), (0, 5), (0, 0), (0, 1), (0, 2), (0, 3), (1, 4), (2, 4) };
        // ---- 表B ----
        Console.WriteLine("## 表B. 移動（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("味方が動かされた回数（`Move` の件数＝動いた駒の延べ）を出どころで割る。追い風 ＝ `Tailwind` の直後の2件。前へ ＝ そのうち行が前に変わった。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 味方の移動 | " + string.Join(" | ", MoveSources) + " | 前へ | 嵐の手番 | 嵐で動いた |");
        Console.WriteLine("|---|---|---|--:|" + string.Concat(MoveSources.Select(_ => "--:|")) + "--:|--:|--:|");
        foreach (var (s, w) in cells)
            for (int b = 0; b < 2; b++)
                foreach (var v in Versions)
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F2(a.Per(a.AllyMoves.Sum()))} | " + string.Join(" | ", a.AllyMoves.Select(x => F2(a.Per(x))))
                                      + $" | {F2(a.Per(a.AllyForward))} | {F2(a.PerK("GaleStirs"))} | {F2(a.PerK("GaleAllyMoved"))} |");
                }
        Console.WriteLine();
        Console.WriteLine("### 表B'. 追い風（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("機会 ＝ 保持者が敵を後ろの行へ動かした（出どころ: バサの入れ替え ／ 吹っ飛ばし ／ 弾き返し ／ ほか＝突き返し・突き崩し）。踏み込み ＝ 入れ替えた。前へ出たのがヨミ ／ セロ ／ シオ。退避した駒を戻した ＝ 同じターンに緊急退避で下げた駒が追い風で前へ出た（移り木で4割を越えていた）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 機会 | 入れ替え ／ 吹っ飛ばし ／ 弾き返し ／ ほか | 踏み込み | ヨミ ／ セロ ／ シオ | 2体いない | 4割で飛ばした | 全員4割未満 | 連鎖で止めた | 退避した駒を戻した |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|--:|--:|--:|--:|--:|");
        foreach (var (s, w) in cells)
            for (int b = 0; b < 2; b++)
                foreach (var v in Versions.Where(v => v.Tag is "G2" or "G4"))
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F2(a.PerK("TailwindTriggers"))} | {F2(a.PerK("TailwindFromShuffle"))} ／ {F2(a.PerK("TailwindFromBlast"))} ／ {F2(a.PerK("TailwindFromSpring"))} ／ {F2(a.PerK("TailwindFromOther"))} | "
                                      + $"{F2(a.PerK("TailwindSteps"))} | {F2(a.Per(a.StepYomi))} ／ {F2(a.Per(a.StepSero))} ／ {F2(a.Per(a.StepShio))} | {F2(a.PerK("TailwindNoPair"))} | {F2(a.PerK("TailwindLowHp"))} | {F2(a.PerK("TailwindAllLow"))} | {F2(a.PerK("TailwindNested"))} | {F2(a.Per(a.StepRetreated))} |");
                }
        Console.WriteLine();
        Console.WriteLine("### 表B''. 段に届いたターン（届いた戦の割合・平均ターン）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | シオ 段1 ／ 段2 ／ 段3 | セロ 段1 ／ 段2 ／ 段3 | バサ（敵の乱れ）段1 ／ 段2 ／ 段3 | ヨミの初めての薙ぎ |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (var (s, w) in cells)
            for (int b = 0; b < 2; b++)
                foreach (var v in Versions)
                {
                    var a = res[(b, v.Tag, w, s)];
                    string st(long[] n, long[] t) => string.Join(" ／ ", Enumerable.Range(1, 3).Select(i => Agg.StageT(n, t, i, a.N)));
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {st(a.ShioN, a.ShioT)} | {st(a.SeroN, a.SeroT)} | {st(a.BasaN, a.BasaT)} | "
                                      + $"{(a.YomiSweepN == 0 ? "—" : $"{100.0 * a.YomiSweepN / a.N:F0}% T{(double)a.YomiSweepT / a.YomiSweepN:F1}")} |");
                }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C. 転倒の穴（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("越えた ＝ 転倒した列が立っていれば狙えなかった駒を主目標に選んだ（味方が敵陣を ／ 敵が味方の陣を）。外れた ＝ 転倒していて介入の候補から外れた（延べ・敵の駒 ／ 味方の駒）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 敵に付いた転倒 | 越えた（味方 ／ 敵） | 外れた介入（敵 ／ 味方） | 同士討ち | 突風の転倒 |");
        Console.WriteLine("|---|---|---|--:|---|---|--:|--:|");
        foreach (var (s, w) in cells)
            for (int b = 0; b < 2; b++)
                foreach (var v in Versions)
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F2(a.Per(a.StaggerFoe))} | {F2(a.Per(a.HoleBreachP))} ／ {F2(a.Per(a.HoleBreachE))} | {F2(a.Per(a.HoleSkipE))} ／ {F2(a.Per(a.HoleSkipP))} | {F2(a.Per(a.ConfusedStrikes))} | {F2(a.PerK("GustFell"))} |");
                }
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D. `compare` 61 行（115/115・第1〜5波・seed 0..199）");
        Console.WriteLine();
        var rows = CompareBuilds().ToList();
        var cmp = new Dictionary<string, double[,]>();
        foreach (var v in Versions)
        {
            var m = new double[rows.Count, 5];
            Parallel.For(0, rows.Count * 5, k =>
            {
                int ri = k / 5, st = k % 5;
                var f = OldYomiShio(Apply(rows[ri].F, v));
                int wins = 0;
                for (int seed = 0; seed < Seeds; seed++)
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, shuffler: v.Rule).PlayerWon) wins++;
                m[ri, st] = 100.0 * wins / Seeds;
            });
            cmp[v.Tag] = m;
        }
        // G0 が docs/balance.md と一致するか（検算）
        var bal = File.Exists("docs/balance.md") ? File.ReadAllLines("docs/balance.md") : Array.Empty<string>();
        int balMis = 0;
        for (int ri = 0; ri < rows.Count; ri++)
        {
            string? line = bal.FirstOrDefault(l => l.StartsWith("| " + rows[ri].Name + " |"));
            if (line is null) { balMis++; continue; }
            var cellsTxt = line.Split('|').Skip(2).Take(5).Select(c => double.Parse(c.Trim().TrimEnd('%'))).ToArray();
            for (int st = 0; st < 5; st++) if (Math.Abs(cellsTxt[st] - cmp["G0"][ri, st]) > 0.01) balMis++;
        }
        Console.WriteLine($"G0 と `docs/balance.md` の差: **{balMis} セル**（305 セル中）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 動いたセル | 動いた行 | max|Δ| | r（G0 対 版・305 セル） | 全61行の第2〜5波 | 主判定19行の第五波 | 情報セル（第2〜5波 0<x<100） |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|--:|--:|");
        var primary = Baseline.PrimaryRows.ToHashSet();
        foreach (var v in Versions)
        {
            var m = cmp[v.Tag]; var g = cmp["G0"];
            int cellsMoved = 0, rowsMoved = 0; double maxD = 0;
            var xs = new List<double>(); var ys = new List<double>();
            for (int ri = 0; ri < rows.Count; ri++)
            {
                bool any = false;
                for (int st = 0; st < 5; st++)
                {
                    double d = m[ri, st] - g[ri, st];
                    if (Math.Abs(d) > 0.001) { cellsMoved++; any = true; maxD = Math.Max(maxD, Math.Abs(d)); }
                    xs.Add(g[ri, st]); ys.Add(m[ri, st]);
                }
                if (any) rowsMoved++;
            }
            double r = Corr(xs, ys);
            string waves = string.Join(" / ", Enumerable.Range(1, 4).Select(st => F1(Enumerable.Range(0, rows.Count).Average(ri => m[ri, st]))));
            var prim = Enumerable.Range(0, rows.Count).Where(ri => primary.Contains(rows[ri].Name)).ToList();
            double pf = prim.Count == 0 ? double.NaN : prim.Average(ri => m[ri, 4]);
            int info = 0;
            for (int ri = 0; ri < rows.Count; ri++) for (int st = 1; st < 5; st++) if (m[ri, st] > 0 && m[ri, st] < 100) info++;
            Console.WriteLine($"| {v.Tag} | {cellsMoved} | {rowsMoved} | {F1(maxD)} | {r:F4} | {waves} | {F1(pf)} | {info} |");
        }
        Console.WriteLine();
        Console.WriteLine("動いた行（G0 → 版・第1〜5波）:");
        Console.WriteLine();
        for (int ri = 0; ri < rows.Count; ri++)
            foreach (var v in Versions.Skip(1))
            {
                bool any = false;
                for (int st = 0; st < 5; st++) if (Math.Abs(cmp[v.Tag][ri, st] - cmp["G0"][ri, st]) > 0.001) any = true;
                if (!any) continue;
                Console.WriteLine($"- {v.Tag} {rows[ri].Name}: " + string.Join(" / ", Enumerable.Range(0, 5).Select(st => F1(cmp["G0"][ri, st]))) + " → " + string.Join(" / ", Enumerable.Range(0, 5).Select(st => F1(cmp[v.Tag][ri, st]))));
            }
        Console.WriteLine();

        // ---- 表E ----
        Console.WriteLine("## 表E. 目標（全員生存 %・200/200）");
        Console.WriteLine();
        Console.WriteLine("第228期 H3: 総当たりの台（前1 バサ ／ 前3 セロ ／ 中央 ヨミ ／ 後1 シオ ／ 後3 ハネ）で主判定 99.0%・本編の平均 90.4%、227 L2 の席で 96.5%・91.2%。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 主判定（九/新兵） | 本編の平均（第2〜5波） | 第三波 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        for (int b = 0; b < 2; b++)
            foreach (var v in Versions)
                Console.WriteLine($"| {benches[b].Name} | {v.Tag} | {F1(res[(b, v.Tag, 4, 0)].Surv)} | {F1(Enumerable.Range(0, 4).Average(w => res[(b, v.Tag, w, 0)].Surv))} | {F1(res[(b, v.Tag, 1, 0)].Surv)} |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }

    static double Corr(List<double> x, List<double> y)
    {
        double mx = x.Average(), my = y.Average(), sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < x.Count; i++) { sxy += (x[i] - mx) * (y[i] - my); sxx += (x[i] - mx) * (x[i] - mx); syy += (y[i] - my) * (y[i] - my); }
        return sxx == 0 || syy == 0 ? 1.0 : sxy / Math.Sqrt(sxx * syy);
    }
}
