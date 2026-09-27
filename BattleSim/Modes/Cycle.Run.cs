using BattleCore;
using static Common;

// cycle run —— 表A〜F（§8.2）＋ compare 61 行。
static partial class CycleDiag
{
    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第230期 cycle run");
        Console.WriteLine();

        var pick = PickSeats(MHane228, VerOf("W4"));
        var top = pick[0];
        int rank228 = pick.FindIndex(x => SeatsNamed(x.F) == SeatsNamed(Apply(MHane228, VerOf("W4"))));
        int rank229 = pick.FindIndex(x => SeatsNamed(x.F) == SeatsNamed(Apply(MHane229, VerOf("W4"))));
        Console.WriteLine("## 席（W4 × 九/新兵 × 200/200 × seed 1000..1199・120 通り）");
        Console.WriteLine();
        Console.WriteLine($"- 総当たりの1位: {SeatsNamed(top.F)}（全員生存 {top.Sv}/200・落ちた駒 {top.Fell}・勝ち {top.W}）");
        Console.WriteLine($"- 全員生存が最大と同値の並び: {pick.Count(x => x.Sv == top.Sv)} 通り。228 H3 の席は {rank228 + 1} 位（{pick[rank228].Sv}/200）・229 G4 の1位の席は {rank229 + 1} 位（{pick[rank229].Sv}/200）");
        Console.WriteLine("- 上位5: " + string.Join(" ／ ", pick.Take(5).Select(x => $"[{SeatsNamed(x.F)}] {x.Sv}")));
        Console.WriteLine();

        var benches = new List<(string Name, Formation F, bool All)>
        {
            ("総当たり（W4）", top.F, true),
            ("228 H3 の席", MHane228, true),
            ("参考 雷", Thunder, false),
        };
        int[] waveOrder = { 4, 5, 0, 1, 2, 3 };
        var res = new Dictionary<(int B, string V, int W, int S), Agg>();
        for (int b = 0; b < benches.Count; b++)
            foreach (var v in Versions)
            {
                if (!benches[b].All && v.Tag is not ("W0" or "W4")) continue;
                for (int s = 0; s < Scales.Length; s++)
                    foreach (int w in waveOrder)
                        res[(b, v.Tag, w, s)] = Measure(benches[b].F, v, w, Scales[s].Sc);
            }
        IEnumerable<Ver> VersOf(int b) => benches[b].All ? Versions : Versions.Where(v => v.Tag is "W0" or "W4");
        double MainAvg(int b, string v, int s, Func<Agg, double> f) => Enumerable.Range(0, 4).Average(w => f(res[(b, v, w, s)]));

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
                    Console.WriteLine($"| {benches[b].Name} | {v.Tag} | " + string.Join(" | ", waveOrder.Select(w => res[(b, v.Tag, w, s)]).Select(a => $"{F1(a.Surv)} ／ {F1(a.Win)}"))
                                      + $" | {F1(MainAvg(b, v.Tag, s, a => a.Surv))} ／ {F1(MainAvg(b, v.Tag, s, a => a.Win))} |");
            Console.WriteLine();
        }
        Console.WriteLine("### 表A'. 決着T と落ちた駒（倒れた戦の割合 ／ 倒れた平均ターン・200/200）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 全員生存 | 勝率 | 決着T | 落ちた駒/戦 | " + string.Join(" | ", UnitNames) + " |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|" + string.Concat(Enumerable.Repeat("---|", Units.Length)));
        foreach (int w in waveOrder)
            for (int b = 0; b < 2; b++)
                foreach (var v in Versions)
                {
                    var a = res[(b, v.Tag, w, 0)];
                    Console.WriteLine($"| {benches[b].Name} | {WaveNames[w]} | {v.Tag} | {F1(a.Surv)} | {F1(a.Win)} | {F2(a.WinT)} | {F2(a.Per(a.FellTotal))} | "
                                      + string.Join(" | ", Units.Select(id => a.Fell.GetValueOrDefault(id) == 0 ? "0%" : $"{100.0 * a.Fell[id] / a.N:F1}% ／ T{(double)a.FellT[id] / a.Fell[id]:F1}")) + " |");
                }
        Console.WriteLine();

        var cells = new[] { 4, 5, 0, 1, 2, 3 };
        // ---- 表B ----
        Console.WriteLine("## 表B. 追い風（1戦あたり・200/200）");
        Console.WriteLine();
        Console.WriteLine("踏み込んだ駒 ＝ `Tailwind` の `TargetId`。前へ ＝ 行が前に変わった `Move`（そのうち追い風の分）。味方の移動 ＝ 味方の `Move` の延べ（1ターンあたり ＝ ÷ 決着T）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 追い風 | 踏み込み: " + string.Join(" ／ ", UnitNames) + " | ヨミが前へ（追い風） | シオが前へ（追い風） | 味方の移動 ／ 1ターン |");
        Console.WriteLine("|---|---|---|--:|---|---|---|---|");
        foreach (int w in cells)
            for (int b = 0; b < 2; b++)
                foreach (var v in Versions)
                {
                    var a = res[(b, v.Tag, w, 0)];
                    Console.WriteLine($"| {benches[b].Name} | {WaveNames[w]} | {v.Tag} | {F2(a.Per(a.TailEvents))} | " + string.Join(" ／ ", Units.Select(id => F2(a.Per(a.StepBy.GetValueOrDefault(id)))))
                                      + $" | {F2(a.Per(a.YomiFwd))}（{F2(a.Per(a.YomiFwdTw))}） | {F2(a.Per(a.ShioFwd))}（{F2(a.Per(a.ShioFwdTw))}） | {F2(a.Per(a.AllyMoves))} ／ {F2(a.AllyMoves / Math.Max(1.0, a.TurnsAll))} |");
                }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C. 撃破の衝撃（ヨミの帳簿・1戦あたり・200/200）");
        Console.WriteLine();
        Console.WriteLine("撃破 ＝ ヨミの攻撃の中で倒した敵（枠の外の撃破は「外」）。吹き飛ばし ／ 転倒だけ（後ろに席が無い）／ 勢い余って ／ 上限で止まった ／ 隣に味方なし。T1〜T4 は 吹き飛ばし（席が変わったもの）／ 勢い余って。");
        Console.WriteLine("移動 ＝ ヨミが動かした `Move`（敵の吹き飛ばしと勢い余っての入れ替えの両方）。軋み ＝ ヨミの軋みの割り込みの振り（`CreakSwings`・全版）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | ヨミの撃破（全部） | 撃破 ／ 外 | 吹き飛ばし ／ 転倒だけ ／ 勢い余って ／ 上限 ／ 隣なし | T1 | T2 | T3 | T4+ | 移動 | 軋み |");
        Console.WriteLine("|---|---|---|--:|---|---|---|---|---|---|--:|--:|");
        foreach (int w in cells)
            for (int b = 0; b < 2; b++)
                foreach (var v in Versions)
                {
                    var a = res[(b, v.Tag, w, 0)];
                    string T(int t) => $"{F2(a.Per(a.BlowT[t]))} ／ {F2(a.Per(a.TumbleT[t]))}";
                    string T4() => $"{F2(a.Per(a.BlowT[4] + a.BlowT[5] + a.BlowT[6]))} ／ {F2(a.Per(a.TumbleT[4] + a.TumbleT[5] + a.TumbleT[6]))}";
                    Console.WriteLine($"| {benches[b].Name} | {WaveNames[w]} | {v.Tag} | {F2(a.Per(a.AllKills))} | {F2(a.Per(a.Kills))} ／ {F2(a.Per(a.Outside))} | "
                                      + $"{F2(a.Per(a.Blow))} ／ {F2(a.Per(a.Stumble))} ／ {F2(a.Per(a.Tumble))} ／ {F2(a.Per(a.Capped))} ／ {F2(a.Per(a.NoAlly))} | {T(1)} | {T(2)} | {T(3)} | {T4()} | {F2(a.Per(a.ImpactMoves))} | {F2(a.Per(a.CreakSwings))} |");
                }
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D. 溢れ（受け手別・1戦あたり・200/200）");
        Console.WriteLine();
        Console.WriteLine("溢れ ／ 攻撃力にした量 ／ 上限（+15）に当たった戦の割合。攻撃力にするのは W3・W4 だけ（W0〜W2 は溢れの計数だけ）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | " + string.Join(" | ", UnitNames) + " |");
        Console.WriteLine("|---|---|---|" + string.Concat(Units.Select(_ => "---|")));
        foreach (int w in cells)
            for (int b = 0; b < 2; b++)
                foreach (var v in Versions)
                {
                    var a = res[(b, v.Tag, w, 0)];
                    Console.WriteLine($"| {benches[b].Name} | {WaveNames[w]} | {v.Tag} | " + string.Join(" | ", Units.Select(id =>
                        $"{F1(a.Per(a.Over.GetValueOrDefault(id)))} ／ {F1(a.Per(a.Gain.GetValueOrDefault(id)))} ／ {100.0 * a.CapHit.GetValueOrDefault(id) / a.N:F0}%")) + " |");
                }
        Console.WriteLine();

        // ---- 表E ----
        Console.WriteLine("## 表E. 火力（与ダメ/戦 と ターン頭の攻撃力 T1 ／ T2 ／ T3 ／ T4・200/200）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 与ダメ: " + string.Join(" ／ ", UnitNames) + " | ヨミの攻撃力 | セロの攻撃力 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (int w in cells)
            for (int b = 0; b < 2; b++)
                foreach (var v in Versions)
                {
                    var a = res[(b, v.Tag, w, 0)];
                    string At(string id) => string.Join(" ／ ", Enumerable.Range(1, 4).Select(t => F1(a.AtkAt(id, t))));
                    Console.WriteLine($"| {benches[b].Name} | {WaveNames[w]} | {v.Tag} | " + string.Join(" ／ ", Units.Select(id => F1(a.Per(a.Dealt.GetValueOrDefault(id)))))
                                      + $" | {At("yomi")} | {At("sero")} |");
                }
        Console.WriteLine();

        // ---- compare 61 行 ----
        Console.WriteLine("## compare 61 行（115/115・第1〜5波・seed 0..199）");
        Console.WriteLine();
        var rows = CompareBuilds().ToList();
        var cmp = new Dictionary<string, double[,]>();
        foreach (var v in Versions)
        {
            var m = new double[rows.Count, 5];
            Parallel.For(0, rows.Count * 5, k =>
            {
                int ri = k / 5, st = k % 5;
                var f = Apply(rows[ri].F, v);
                int wins = 0;
                for (int seed = 0; seed < Seeds; seed++)
                    if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
                m[ri, st] = 100.0 * wins / Seeds;
            });
            cmp[v.Tag] = m;
        }
        var bal = File.Exists("docs/balance.md") ? File.ReadAllLines("docs/balance.md") : Array.Empty<string>();
        int balMis = 0;
        for (int ri = 0; ri < rows.Count; ri++)
        {
            string? line = bal.FirstOrDefault(l => l.StartsWith("| " + rows[ri].Name + " |"));
            if (line is null) { balMis++; continue; }
            var cols = line.Split('|').Select(c => c.Trim()).Where(c => c.EndsWith('%')).Take(5).Select(c => double.Parse(c.TrimEnd('%'))).ToArray();
            for (int st = 0; st < 5 && st < cols.Length; st++) if (Math.Abs(cols[st] - cmp["W0"][ri, st]) > 0.001) balMis++;
        }
        Console.WriteLine($"W0 と docs/balance.md の不一致: **{balMis} セル**");
        Console.WriteLine();
        var holders = new HashSet<string> { "basa", "hane", "yomi", "shio" };
        var primary = Baseline.PrimaryRows.ToHashSet();
        Console.WriteLine("| 版 | 動いたセル | 動いた行 | 札の保持者のいない行で動いたセル | max|Δ| | 全61行の第2〜5波 | 主判定19行の第五波 | 情報セル |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|--:|--:|");
        foreach (var v in Versions)
        {
            var m = cmp[v.Tag]; var g = cmp["W0"];
            int cellsMoved = 0, rowsMoved = 0, outside = 0; double maxD = 0;
            for (int ri = 0; ri < rows.Count; ri++)
            {
                bool any = false, has = rows[ri].F.Occupied().Any(o => holders.Contains(o.Def.Id));
                for (int st = 0; st < 5; st++)
                {
                    double d = m[ri, st] - g[ri, st];
                    if (Math.Abs(d) > 0.001) { cellsMoved++; any = true; maxD = Math.Max(maxD, Math.Abs(d)); if (!has) outside++; }
                }
                if (any) rowsMoved++;
            }
            string waves = string.Join(" / ", Enumerable.Range(1, 4).Select(st => F1(Enumerable.Range(0, rows.Count).Average(ri => m[ri, st]))));
            var prim = Enumerable.Range(0, rows.Count).Where(ri => primary.Contains(rows[ri].Name)).ToList();
            double pf = prim.Count == 0 ? double.NaN : prim.Average(ri => m[ri, 4]);
            int info = 0;
            for (int ri = 0; ri < rows.Count; ri++) for (int st = 1; st < 5; st++) if (m[ri, st] > 0 && m[ri, st] < 100) info++;
            Console.WriteLine($"| {v.Tag} | {cellsMoved} | {rowsMoved} | {outside} | {F1(maxD)} | {waves} | {F1(pf)} | {info} |");
        }
        Console.WriteLine();
        Console.WriteLine("動いた行（W0 → 版・第1〜5波）:");
        Console.WriteLine();
        for (int ri = 0; ri < rows.Count; ri++)
            foreach (var v in Versions.Skip(1))
            {
                bool any = false;
                for (int st = 0; st < 5; st++) if (Math.Abs(cmp[v.Tag][ri, st] - cmp["W0"][ri, st]) > 0.001) any = true;
                if (!any) continue;
                Console.WriteLine($"- {v.Tag} {rows[ri].Name}: " + string.Join(" / ", Enumerable.Range(0, 5).Select(st => F1(cmp["W0"][ri, st]))) + " → " + string.Join(" / ", Enumerable.Range(0, 5).Select(st => F1(cmp[v.Tag][ri, st]))));
            }
        Console.WriteLine();

        // ---- 表F ----
        Console.WriteLine("## 表F. 目標（全員生存 %・200/200）");
        Console.WriteLine();
        Console.WriteLine("第229期（228 H3 の席）: G0 主判定 99.0%・本編の平均 90.4% ／ G4 97.0%・95.4%（＝ この期の W0）。G4 の総当たりの1位で G4 98.0%・91.9%。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 主判定（九/新兵） | 九/農兵 | 本編の平均（第2〜5波） | 第二波 | 第三波 | 第四波 | 第五波 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int b = 0; b < benches.Count; b++)
            foreach (var v in VersOf(b))
                Console.WriteLine($"| {benches[b].Name} | {v.Tag} | {F1(res[(b, v.Tag, 4, 0)].Surv)} | {F1(res[(b, v.Tag, 5, 0)].Surv)} | {F1(MainAvg(b, v.Tag, 0, a => a.Surv))} | "
                                  + string.Join(" | ", Enumerable.Range(0, 4).Select(w => F1(res[(b, v.Tag, w, 0)].Surv))) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}
