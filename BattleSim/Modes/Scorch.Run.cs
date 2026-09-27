using BattleCore;
using static Common;

// =====================================================================================
// scorch run（第219期） —— 表A〜E ＋ P5（第二波の1ターン目）
//
// 表A: `compare` 61 行 × 第2〜5波 × seed 0..199 × F0〜F4（倍率 115）
// 表B: 台（H1・H2 × X字 / P2 ＋ ポンの席 ／ H3 13 行 ／ H4 3 行 ／ H4' 2 行）× 版 × 倍率
// 表C: 脆さで増えた量を出どころ別に（敵／味方）
// 表D: 燃焼の在り方（F0 と F2）
// 表E: F3・F4 の味方側（反転・結界の外・ホタ・板）
// =====================================================================================

static partial class ScorchDiag
{
    internal static readonly string[] Versions = { "F0", "F1", "F2", "F3", "F4" };
    internal static readonly int[] ScalesMain = { 115, 150 }, ScalesRef = { 200, 250 };
    static EnemyScaleRule Sc(int x) => new(x, x);

    static List<(string Name, Formation F)>? _rigSeats;
    /// <summary>H1・H2 × X字 / P2 の席（F2 × 倍率 150 で選ぶ・<see cref="PickSeat"/>）。</summary>
    internal static List<(string Name, Formation F)> RigSeats(bool print = false)
    {
        if (_rigSeats is not null && !print) return _rigSeats;
        var list = new List<(string, Formation)>();
        if (print)
        {
            Console.WriteLine("### 席（F2 × 倍率 150 × 第2〜5波 × seed 1000..1049・同値は 全員生存 → 決着T → 列挙順）");
            Console.WriteLine();
            Console.WriteLine("| 台 | 陣形 | 席 | 勝ち数/200 | 勝ち数が同値 | 3段で同値 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|");
        }
        foreach (var (tag, mem) in new[] { ("H1", H1Members()), ("H2", H2Members()) })
            foreach (var sh in new[] { FormationShape.X, FormationShape.Diamond })
            {
                var (best, w, ties, ta) = PickSeat(mem, sh, EmberRule.Scorched("F2"), ShockDiag.Scale150);
                list.Add((tag + " " + ShapeName(sh), best));
                if (print) Console.WriteLine("| " + tag + " | " + ShapeName(sh) + " | " + SeatsNamed(best) + " | " + w + " | " + ties + " | " + ta + " |");
            }
        if (print) Console.WriteLine();
        return _rigSeats = list;
    }

    static partial void RunImpl(string arg)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第219期 scorch run —— 燃焼で脆くなる（F0〜F4）");
        Console.WriteLine();
        Console.WriteLine("版: " + string.Join(" ／ ", Versions.Select(v => v + " = " + EmberRule.Scorched(v))));
        Console.WriteLine();
        if (arg == "yoke") { YokeProbe(); return; }
        if (arg != "b" && arg != "p5") TableA();
        if (arg != "a" && arg != "p5") TablesBtoE();
        if (arg != "a" && arg != "b") P5();
        Console.WriteLine("所要 " + (sw.ElapsedMilliseconds / 1000.0).ToString("F0") + " 秒");
    }

    // =================================================================================
    // 表A —— compare
    // =================================================================================

    static void TableA()
    {
        var rows = CompareBuilds();
        int n = rows.Length;
        var aggs = new SAgg[Versions.Length, n];
        for (int v = 0; v < Versions.Length; v++)
            for (int i = 0; i < n; i++) aggs[v, i] = Measure(rows[i].F, EnemyScaleRule.Adopted, EmberRule.Scorched(Versions[v]));
        double W(int v, int i, int st) => aggs[v, i].WinPct(st);
        var burnRows = Enumerable.Range(0, n).Where(i => aggs[0, i].FoeLitBattles.Sum() + aggs[0, i].AllyLitBattles.Sum() > 0).ToHashSet();
        var primary = new HashSet<string>(PrimaryRowNames());

        Console.WriteLine("## 表A. `compare` 61 行 × 第2〜5波 × seed 0..199（倍率 115）");
        Console.WriteLine();
        Console.WriteLine("火の付く行（F0 で敵か味方に1度でも燃焼が付いた行）: **" + burnRows.Count + " / " + n + "**。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 動いたセル（±0.5 超）| うち火の付かない行 | 動いた行 | r（244 セル） | max\\|Δ\\| | 全61行の平均 第2/3/4/5波 | 火の付く行の平均Δ | 情報セル | 第五波 95% 超 | 主判定19行 第五波 | 新しく 100% ／ 0% |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|---|--:|--:|--:|--:|---|");
        for (int v = 0; v < Versions.Length; v++)
        {
            int moved = 0, movedNoBurn = 0, rowsMoved = 0, info = 0, over95 = 0, new100 = 0, new0 = 0;
            double maxd = 0, sumBurnD = 0; int cntBurn = 0;
            var xs = new List<double>(); var ys = new List<double>();
            for (int i = 0; i < n; i++)
            {
                bool rm = false;
                foreach (int st in Waves25)
                {
                    double a = W(0, i, st), b = W(v, i, st), d = b - a;
                    xs.Add(a); ys.Add(b);
                    if (Math.Abs(d) > 0.5) { moved++; rm = true; if (!burnRows.Contains(i)) movedNoBurn++; }
                    maxd = Math.Max(maxd, Math.Abs(d));
                    if (burnRows.Contains(i)) { sumBurnD += d; cntBurn++; }
                    if (b > 0 && b < 100) info++;
                    if (a < 100 && b >= 100) new100++;
                    if (a > 0 && b <= 0) new0++;
                }
                if (rm) rowsMoved++;
                if (W(v, i, 4) > 95) over95++;
            }
            string means = string.Join(" / ", Waves25.Select(st => F1(Enumerable.Range(0, n).Average(i => W(v, i, st)))));
            double prim5 = Enumerable.Range(0, n).Where(i => primary.Contains(rows[i].Name)).Select(i => W(v, i, 4)).DefaultIfEmpty(double.NaN).Average();
            Console.WriteLine("| " + Versions[v] + " | " + moved + " | " + movedNoBurn + " | " + rowsMoved + " | " + Pearson(xs, ys).ToString("F3") + " | " + F1(maxd) + " | "
                              + means + " | " + D1(cntBurn == 0 ? double.NaN : sumBurnD / cntBurn) + " | " + info + " | " + over95 + " | " + F1(prim5) + " | " + new100 + " ／ " + new0 + " |");
        }
        Console.WriteLine();

        Console.WriteLine("### 火の付く行の勝率（第2〜5波の平均・F0 → F1 / F2 / F3 / F4）と第2〜5波");
        Console.WriteLine();
        Console.WriteLine("| 行 | 敵が燃えていた % | 味方が燃えていた % | F0 | F1 | F2 | F3 | F4 | F2 − F0 | F3 − F1 | F4 − F2 | F2 の 第2/3/4/5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (int i in burnRows.OrderByDescending(i => aggs[2, i].Mean25 - aggs[0, i].Mean25))
        {
            var m = Enumerable.Range(0, Versions.Length).Select(v => aggs[v, i].Mean25).ToArray();
            Console.WriteLine("| " + rows[i].Name + " | " + P1(aggs[0, i].BurnUnitTurns[0], aggs[0, i].UnitTurns[0]) + " | " + P1(aggs[0, i].BurnUnitTurns[1], aggs[0, i].UnitTurns[1]) + " | "
                              + string.Join(" | ", m.Select(F1)) + " | " + D1(m[2] - m[0]) + " | " + D1(m[3] - m[1]) + " | " + D1(m[4] - m[2]) + " | "
                              + string.Join(" / ", Waves25.Select(st => F1(W(0, i, st)) + "→" + F1(W(2, i, st)))) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("### 波ごとの平均Δ（火の付く行・F0 比）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int v = 1; v < Versions.Length; v++)
            Console.WriteLine("| " + Versions[v] + " | " + string.Join(" | ", Waves25.Select(st => D1(burnRows.Average(i => W(v, i, st) - W(0, i, st))))) + " |");
        Console.WriteLine();

        // 表C（compare 全体）
        for (int v = 1; v < Versions.Length; v++)
        {
            var all = new SAgg();
            for (int i = 0; i < n; i++) all.Merge(aggs[v, i]);
            RouteTable("`compare` 61 行 × " + Versions[v], all);
        }
    }

    static IEnumerable<string> PrimaryRowNames() => Baseline.PrimaryRows;

    static double Pearson(List<double> x, List<double> y)
    {
        double mx = x.Average(), my = y.Average(), sxy = 0, sxx = 0, syy = 0;
        for (int i = 0; i < x.Count; i++) { sxy += (x[i] - mx) * (y[i] - my); sxx += (x[i] - mx) * (x[i] - mx); syy += (y[i] - my) * (y[i] - my); }
        return sxx == 0 || syy == 0 ? double.NaN : sxy / Math.Sqrt(sxx * syy);
    }

    /// <summary>表C の1ブロック: 脆さで足した量を [陣営, 経路] で（1戦あたり）と、切り上げ込みの実効の割合。</summary>
    static void RouteTable(string title, SAgg a)
    {
        Console.WriteLine("#### 表C. " + title + "（1戦あたり・足した量 ／ 掛かる前の量に対する %）");
        Console.WriteLine();
        Console.WriteLine("| 受けた側 | " + string.Join(" | ", BrittleLedger.Routes) + " | 計 | 反転（回復） |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", BrittleLedger.Routes.Length + 2)));
        for (int side = 0; side < 2; side++)
        {
            long tot = 0, totB = 0;
            var cells = new List<string>();
            for (int k = 0; k < BrittleLedger.Routes.Length; k++)
            {
                tot += a.Extra[side, k]; totB += a.Base[side, k];
                cells.Add(a.Extra[side, k] == 0 ? "—" : F2(a.Per(a.Extra[side, k])) + "（" + P1(a.Extra[side, k], a.Base[side, k]) + "）");
            }
            string inv = side == 1 && a.InverseExtra > 0 ? F2(a.Per(a.InverseExtra)) + "（" + P1(a.InverseExtra, a.InverseBase) + "）" : "—";
            Console.WriteLine("| " + (side == 0 ? "敵" : "味方") + " | " + string.Join(" | ", cells) + " | " + (tot == 0 ? "—" : F2(a.Per(tot)) + "（" + P1(tot, totB) + "）") + " | " + inv + " |");
        }
        long e0 = Enumerable.Range(0, 8).Sum(k => a.Extra[0, k]);
        if (e0 > 0)
            Console.WriteLine("\n敵が受けた分の内訳（%）: " + string.Join(" ／ ", Enumerable.Range(0, 8).Where(k => a.Extra[0, k] > 0)
                .Select(k => BrittleLedger.Routes[k] + " " + P1(a.Extra[0, k], e0))));
        Console.WriteLine();
    }

    // =================================================================================
    // 表B〜E —— 台
    // =================================================================================

    static void TablesBtoE()
    {
        Console.WriteLine("## 表B. 台 × 陣形 × 版 × 倍率（第2〜5波 × seed 0..199）");
        Console.WriteLine();
        var seats = RigSeats(print: true);
        var benches = new List<(string Name, Formation F, int[] Scales, string Group)>();
        foreach (var (n, f) in seats) benches.Add((n, f, ScalesMain.Concat(ScalesRef).ToArray(), "H12"));
        benches.Add(("H1 ポンの X字", PonX(), ScalesMain.Concat(ScalesRef).ToArray(), "H12"));
        benches.Add(("参考 ポンのP2（スィド）", PonP2(), ScalesMain.Concat(ScalesRef).ToArray(), "H12"));
        foreach (var (n, f) in H3Rows()) benches.Add(("H3 " + n, f, ScalesMain, "H3"));
        foreach (var (n, f) in H4Rows()) benches.Add(("H4 " + n, f, ScalesMain, "H4"));
        foreach (var (n, f) in H4PrimeRows()) benches.Add(("H4' " + n, f, ScalesMain, "H4p"));

        var res = new Dictionary<(int, int, int), SAgg>();   // (bench, version, scale)
        for (int b = 0; b < benches.Count; b++)
            for (int v = 0; v < Versions.Length; v++)
                foreach (int sc in benches[b].Scales)
                    res[(b, v, sc)] = Measure(benches[b].F, Sc(sc), EmberRule.Scorched(Versions[v]));

        Console.WriteLine("| 台 | 倍率 | 勝率 F0 → F1 / F2 / F3 / F4 | 全員生存 F0 → F1 / F2 / F3 / F4 | 決着T F0 → F1 / F2 / F3 / F4 |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int b = 0; b < benches.Count; b++)
            foreach (int sc in benches[b].Scales)
            {
                var a = Enumerable.Range(0, Versions.Length).Select(v => res[(b, v, sc)]).ToArray();
                string Row(Func<SAgg, double> f) => F1(f(a[0])) + " → " + string.Join(" / ", a.Skip(1).Select(x => F1(f(x))));
                Console.WriteLine("| " + benches[b].Name + " | " + sc + (sc >= 200 ? "（参考）" : "") + " | " + Row(x => x.Mean25) + " | " + Row(x => x.AllSurvPct) + " | "
                                  + string.Join(" / ", a.Select(x => F2(x.MeanWinT))) + " |");
            }
        Console.WriteLine();

        // 群の平均（H3・H4・H4'）
        Console.WriteLine("### 群の平均（勝率・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 群 | 倍率 | F0 | F1 | F2 | F3 | F4 | F3 − F1 | F4 − F2 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var g in new[] { "H3", "H4", "H4p" })
            foreach (int sc in ScalesMain)
            {
                var idx = Enumerable.Range(0, benches.Count).Where(b => benches[b].Group == g).ToList();
                var m = Enumerable.Range(0, Versions.Length).Select(v => idx.Average(b => res[(b, v, sc)].Mean25)).ToArray();
                Console.WriteLine("| " + (g == "H4p" ? "H4'" : g) + "（" + idx.Count + " 行） | " + sc + " | " + string.Join(" | ", m.Select(F1)) + " | " + D1(m[3] - m[1]) + " | " + D1(m[4] - m[2]) + " |");
            }
        Console.WriteLine();

        // 表C（台ごと）
        Console.WriteLine("## 表C. 脆さで増えた量（台ごと）");
        Console.WriteLine();
        for (int b = 0; b < benches.Count; b++)
        {
            if (benches[b].Group == "H3" || benches[b].Group == "H4") continue;
            foreach (string v in new[] { "F2", "F4" })
                RouteTable(benches[b].Name + " × " + v + " × 倍率 150", res[(b, Array.IndexOf(Versions, v), 150)]);
        }
        foreach (var g in new[] { "H3", "H4" })
            foreach (string v in new[] { "F2", "F4" })
            {
                var all = new SAgg();
                foreach (int b in Enumerable.Range(0, benches.Count).Where(b => benches[b].Group == g)) all.Merge(res[(b, Array.IndexOf(Versions, v), 150)]);
                RouteTable(g + " の全行 × " + v + " × 倍率 150", all);
            }

        // 表D
        Console.WriteLine("## 表D. 燃焼の在り方（倍率 150）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 敵の駒×ターン 燃えていた % | 味方 % | 1体でも燃えていたターン % | 書き手ごとの付与（相手陣営へ ／ 同じ陣営へ・1戦あたり） |");
        Console.WriteLine("|---|---|--:|--:|--:|---|");
        for (int b = 0; b < benches.Count; b++)
            foreach (string v in new[] { "F0", "F2" })
            {
                if (benches[b].Group == "H4" && !benches[b].Name.Contains("死の連鎖")) continue;
                var a = res[(b, Array.IndexOf(Versions, v), 150)];
                var ids = benches[b].F.Occupied().Select(o => o.Def.Id).ToHashSet();
                string writers = string.Join("・", ids.Where(id => a.IgniteFoe.ContainsKey(id) || a.IgniteAlly.ContainsKey(id))
                    .Select(id => Short(UnitCatalog.Everyone.First(d => d.Id == id)) + " " + F2(a.Per(a.IgniteFoe.GetValueOrDefault(id))) + "／" + F2(a.Per(a.IgniteAlly.GetValueOrDefault(id))))
                    .DefaultIfEmpty("—"));
                Console.WriteLine("| " + benches[b].Name + " | " + v + " | " + P1(a.BurnUnitTurns[0], a.UnitTurns[0]) + " | " + P1(a.BurnUnitTurns[1], a.UnitTurns[1]) + " | "
                                  + P1(a.TurnsAnyBurn[0], a.Turns) + " | " + writers + " |");
            }
        Console.WriteLine();

        // 表E
        Console.WriteLine("## 表E. F3・F4 の味方側（倍率 150・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 反転で増えた回復 | 味方が受けた脆さの分（計） | うち燃焼の刻み | うちホタ | 板の倍 × 脆さ（回） | 勝率 F1 → F3 ／ F2 → F4 | 全員生存 F1 → F3 ／ F2 → F4 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|---|---|");
        for (int b = 0; b < benches.Count; b++)
            foreach (string v in new[] { "F3", "F4" })
            {
                var a = res[(b, Array.IndexOf(Versions, v), 150)];
                long ally = Enumerable.Range(0, 8).Sum(k => a.Extra[1, k]);
                if (ally == 0 && a.InverseExtra == 0) continue;
                int lo = v == "F3" ? 1 : 2, hi = v == "F3" ? 3 : 4;
                var x = res[(b, lo, 150)]; var y = res[(b, hi, 150)];
                Console.WriteLine("| " + benches[b].Name + " | " + v + " | " + F2(a.Per(a.InverseExtra)) + " | " + F2(a.Per(ally)) + " | " + F2(a.Per(a.Extra[1, 6])) + " | "
                                  + F2(a.Per(a.PyreExtra)) + " | " + F2(a.Per(a.PlankTimesBrittle)) + " | " + F1(x.Mean25) + " → " + F1(y.Mean25) + " | "
                                  + F1(x.AllSurvPct) + " → " + F1(y.AllSurvPct) + " |");
            }
        Console.WriteLine();

        // ホタの燃えている割合（P4）
        Console.WriteLine("### ホタの燃えている割合（P4・F0・倍率 150）");
        Console.WriteLine();
        foreach (int b in Enumerable.Range(0, benches.Count).Where(b => benches[b].F.Occupied().Any(o => o.Def.Id == "hota")))
        {
            var a = res[(b, 0, 150)];
            var t = a.ByUnit.GetValueOrDefault("hota");
            if (t is null) continue;
            Console.WriteLine("- " + benches[b].Name + ": ホタが燃えて振った攻撃の割合 " + P1(t.BurnAttacks, t.Attacks) + "%（振った " + F2(a.Per(t.Attacks)) + " /戦）");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // 第四波（軛）の読み —— P3' が逆に出た理由
    // =================================================================================

    static void YokeProbe()
    {
        Console.WriteLine("## 第四波（軛）の読み（火の付く行・F0 と F2・倍率 115・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 勝率 | 敵に足した脆さ /戦 | 軛が敵への一撃を切った回数 /戦 | 切り落とした量 /戦 | 攻撃で足した脆さの前の1発（平均） | 重装兵が倒れた戦 % | 重装兵が倒れたターン | 脆さの分のうち軛に切られた %（上限の見積もり） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        var rows = CompareBuilds();
        foreach (var (name, f) in rows)
        {
            var a0 = Measure(f, EnemyScaleRule.Adopted, EmberRule.Default, waves: new[] { 3 });
            if (a0.FoeLitBattles.Sum() == 0) continue;
            foreach (string v in new[] { "F0", "F2" })
            {
                var ember = EmberRule.Scorched(v);
                long cutH = 0, cutL = 0, yokerDead = 0, yokerTurn = 0, n = 0, yx = 0;
                var agg = new SAgg();
                var gate = new object();
                var ids = f.Occupied().Select(o => o.Def.Id).ToHashSet();
                Parallel.For(0, MeasSeeds, s2 =>
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[3].Enemy, s2, verbose: false, ember: ember);
                    int dt = -1;
                    if (r.TallyByUnit.TryGetValue("yoker", out var yt) && yt.DeathTurnHist is not null) dt = Array.FindIndex(yt.DeathTurnHist, x => x > 0);
                    lock (gate)
                    {
                        agg.Take(r, 3, ids);
                        cutH += r.Yoke.CutOnEnemyHits; cutL += r.Yoke.CutOnEnemyLost; n++; yx += r.Brittle!.YokeCutExtra;
                        if (dt >= 0) { yokerDead++; yokerTurn += dt; }
                    }
                });
                long ex = Enumerable.Range(0, 8).Sum(k => agg.Extra[0, k]);
                Console.WriteLine("| " + name + " | " + v + " | " + F1(agg.WinPct(3)) + " | " + F2((double)ex / n) + " | " + F2((double)cutH / n) + " | " + F2((double)cutL / n) + " | "
                                  + (agg.Hits[0, 0] == 0 ? "—" : F1((double)agg.Base[0, 0] / agg.Hits[0, 0])) + " | " + P1(yokerDead, n) + " | "
                                  + (yokerDead == 0 ? "—" : F2((double)yokerTurn / yokerDead)) + " | " + (ex == 0 ? "—" : P1(yx, ex)) + " |");
            }
        }
        Console.WriteLine();
    }

    // =================================================================================
    // P5 —— 第二波の1ターン目
    // =================================================================================

    static void P5()
    {
        Console.WriteLine("## P5. 第二波の1ターン目（台本・倍率 115 と 150・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 敵の1発目の量 | 1ターン目に味方が受けた被ダメ（敵から・1戦あたり） | 1ターン目に敵が振った回数 | 1ターン目に倒れた敵 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|");
        var benches = RigSeats().Select(s => (s.Name, s.F)).Concat(new[] { ("H1 ポンの X字", PonX()) }).ToList();
        foreach (var (name, f) in benches)
            foreach (int sc in ScalesMain)
                foreach (string v in new[] { "F0", "F2" })
                {
                    var (first, t1, swings, deaths) = Turn1(new[] { f }, v, sc);
                    Console.WriteLine("| " + name + " | " + sc + " | " + v + " | " + F2(first) + " | " + F2(t1) + " | " + F2(swings) + " | " + F2(deaths) + " |");
                }
        foreach (string v in new[] { "F0", "F2" })
        {
            var (first, t1, swings, deaths) = Turn1(CompareBuilds().Select(r => r.F).ToArray(), v, 115);
            Console.WriteLine("| `compare` 61 行 | 115 | " + v + " | " + F2(first) + " | " + F2(t1) + " | " + F2(swings) + " | " + F2(deaths) + " |");
        }
        Console.WriteLine();
    }

    static (double First, double T1, double Swings, double Deaths) Turn1(Formation[] fs, string ver, int scale)
    {
        long firstSum = 0, firstN = 0, t1 = 0, swings = 0, deaths = 0, n = 0;
        var gate = new object();
        var ember = EmberRule.Scorched(ver);
        Parallel.For(0, fs.Length * MeasSeeds, j =>
        {
            Formation f = fs[j / MeasSeeds];
            int s = j % MeasSeeds;
            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var en = BattleEngine.Materialize(EnemyCatalog.Stages[1].Enemy, BattleContext.EnemyTeam, Sc(scale));
            BattleResult r = BattleEngine.Run(pl, en, s, verbose: true, ember: ember);
            var pid = pl.Select(u => u.InstanceId).ToHashSet();
            var eid = en.Select(u => u.InstanceId).ToHashSet();
            long lf = 0, lfn = 0, lt = 0, ls = 0, ld = 0;
            foreach (BattleEvent e in r.Events)
            {
                if (e.Turn != 1) continue;
                if (e.Kind == BattleEventKind.Damage && e.ActorId is int a && eid.Contains(a) && e.TargetId is int t && pid.Contains(t))
                {
                    if (lfn == 0) { lf = e.Amount; lfn = 1; }
                    lt += e.Amount;
                }
                if (e.Kind == BattleEventKind.Attack && e.ActorId is int a2 && eid.Contains(a2)) ls++;
                if (e.Kind == BattleEventKind.Death && e.TargetId is int d && eid.Contains(d)) ld++;
            }
            lock (gate) { firstSum += lf; firstN += lfn; t1 += lt; swings += ls; deaths += ld; n++; }
        });
        return (firstN == 0 ? double.NaN : (double)firstSum / firstN, (double)t1 / n, (double)swings / n, (double)deaths / n);
    }
}
