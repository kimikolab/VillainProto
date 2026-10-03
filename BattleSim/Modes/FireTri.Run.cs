using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// firetri run —— 段2 ＋ 表A〜I（指示書 §6.3）、`compare` 61 行の版ごとの動き。
static partial class FireTriDiag
{
    static readonly string[] BoardNames = { "T3", "T3′", "T3-238", "T3-244", "雷＋ボルグ" };
    static Formation BoardOf(string name, FB.Ver v, Dictionary<string, List<FB.Pick>> ranked) => name switch
    {
        "T3" => Apply(FB.Dec(ranked[v.Name][0].Best), v),
        "T3′" => Apply(FB.Dec(ranked[v.Name].First(FB.IsT3P).Best), v),
        "T3-238" => Apply(T3238, v),
        "T3-244" => Apply(T3244, v),
        "雷＋ボルグ" => Apply(ThunderBorg, v),
        _ => throw new ArgumentException(name),
    };
    static readonly (string Name, Func<Formation> F)[] Refs = { ("参考 移動", () => BA.RefMove), ("参考 雷", () => BA.RefThunder) };
    static readonly int[] MainWaves = { 0, 1, 2, 3 };
    static readonly string[] Trio = { "borg", "hota", "hiyo" };

    static Dictionary<(string B, string V, int W, int S), Cell> _cells = new();
    static Cell At(string b, string v, int w, int s) => _cells[(b, v, w, IsTarget(w) ? 0 : s)];

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ranked = new Dictionary<string, List<FB.Pick>>();
        foreach (var v in Versions) ranked[v.Name] = FB.Ranked(PicksOf(v, out _));
        Console.WriteLine("# 第247期 段2 —— 三角の循環を規則にする（版 T0〜T1-放粉）");
        Console.WriteLine();
        Stage1Summary(ranked);
        _cells = new();
        void Put(string b, string vn, Formation f)
        {
            for (int w = 0; w < WaveNames.Length; w++)
                for (int s = 0; s < (IsTarget(w) ? 1 : BA.Scales.Length); s++)
                    _cells[(b, vn, w, s)] = Measure(f, w, IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc);
        }
        foreach (string bn in BoardNames) foreach (var v in Versions) Put(bn, v.Name, BoardOf(bn, v, ranked));
        foreach (var (rn, rf) in Refs) Put(rn, "T0", rf());
        Console.Error.WriteLine($"  段2 の測定 {sw.Elapsed.TotalSeconds:F0} 秒");
        Console.WriteLine("## 台（版ごとの席）");
        Console.WriteLine();
        foreach (var v in Versions) foreach (string bn in BoardNames) Console.WriteLine($"- {v.Name} × {bn}: {BA.SeatsNamed(BoardOf(bn, v, ranked))}");
        foreach (var (rn, rf) in Refs) Console.WriteLine($"- {rn}（版に依らない）: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
        TableA(); TableB(); TableC(); TableD(); TableE(); TableF(); TableG(); TableH(); TableI();
        CompareMoves();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    static string Sc(FB.Score4 s) => $"{s.S.Sv}/40・400/300 {s.Sv4}/40・落 {s.S.Fell}・勝 {s.S.W}";
    static void Stage1Summary(Dictionary<string, List<FB.Pick>> ranked)
    {
        Console.WriteLine("## 段1（T3 1,081 組 × 席 120 × seed 1000..1039・九/新兵 × 200/200 → 400/300 → 落 → 決着T）");
        Console.WriteLine();
        Console.WriteLine("| 版 | T3 の1位（相方） | 席 | 得点 | T3′ の1位 | 席 | 得点 | 上位10 にシオ・ササ | 200/200 で 40/40 ／ 400/300 も 40/40 の組 |");
        Console.WriteLine("|---|---|---|---|---|---|---|--:|---|");
        foreach (var v in Versions)
        {
            var rk = ranked[v.Name];
            var b = rk[0]; var bp = rk.First(FB.IsT3P);
            int prime10 = rk.Take(10).Count(p => !FB.IsT3P(p));
            int full = rk.Count(p => p.BestS.S.Sv == BA.PickSeeds);
            int full4 = rk.Count(p => p.BestS.S.Sv == BA.PickSeeds && p.BestS.Sv4 == BA.PickSeeds);
            Console.WriteLine($"| {v.Name} | {string.Join("・", b.Partners)} | {BA.SeatsNamed(FB.Dec(b.Best))} | {Sc(b.BestS)} | {string.Join("・", bp.Partners)} | {BA.SeatsNamed(FB.Dec(bp.Best))} | {Sc(bp.BestS)} | {prime10} | {full} ／ {full4} |");
        }
        Console.WriteLine();
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static double MainAvg(string b, string v, int s, Func<FC.CAgg, double> f) => MainWaves.Average(w => f(At(b, v, w, s).C));
    static double MainAvgT(string b, string v, int s, Func<TAgg, double> f) => MainWaves.Average(w => f(At(b, v, w, s).T));
    static double WinP(FC.CAgg c) => c.N == 0 ? double.NaN : 100.0 * c.Wins / c.N;
    static double SurvP(FC.CAgg c) => c.N == 0 ? double.NaN : 100.0 * c.AllSurv / c.N;
    static double PerD(long a, long n) => n == 0 ? double.NaN : (double)a / n;

    static void TableA()
    {
        Console.WriteLine("## 表A —— 台 × 版（勝率・全員生存・決着T・落ちた駒）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 本編 400/300 勝率 ／ 全員生存（第2〜5波の平均） | 本編 200/200 勝率 ／ 全員生存 | 九/新兵 400/300 全員生存 ／ 決着T | 九/新兵 200/200 全員生存 | 九/農兵 400/300 全員生存 | 第五波 400/300 勝率 | 重い波 400/300 全員生存 ／ 決着T | 落ちた/戦（本編 400/300）ボルグ ／ ホタ ／ ヒヨ ／ 相方 |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|--:|---|---|");
        foreach (string b in BoardNames)
            foreach (var v in Versions)
            {
                var n9 = At(b, v.Name, BA.MainWave, 1).C; var h = At(b, v.Name, WaveHeavy, 1).C;
                long nn = MainWaves.Sum(w => At(b, v.Name, w, 1).C.N);
                string fell = string.Join(" ／ ", Enumerable.Range(0, 4).Select(r => Per(MainWaves.Sum(w => At(b, v.Name, w, 1).C.FellRole[r]), nn)));
                Console.WriteLine($"| {b} | {v.Name} | {F1(MainAvg(b, v.Name, 1, WinP))} ／ {F1(MainAvg(b, v.Name, 1, SurvP))} | {F1(MainAvg(b, v.Name, 0, WinP))} ／ {F1(MainAvg(b, v.Name, 0, SurvP))} | {F1(SurvP(n9))} ／ {Per(n9.Turns, n9.N)} | {F1(SurvP(At(b, v.Name, BA.MainWave, 0).C))} | {F1(SurvP(At(b, v.Name, BA.MainWave + 1, 1).C))} | {F1(WinP(At(b, v.Name, 3, 1).C))} | {F1(SurvP(h))} ／ {Per(h.Turns, h.N)} | {fell} |");
            }
        foreach (var (rn, _) in Refs)
        {
            var n9 = At(rn, "T0", BA.MainWave, 1).C; var h = At(rn, "T0", WaveHeavy, 1).C;
            Console.WriteLine($"| {rn} | — | {F1(MainAvg(rn, "T0", 1, WinP))} ／ {F1(MainAvg(rn, "T0", 1, SurvP))} | {F1(MainAvg(rn, "T0", 0, WinP))} ／ {F1(MainAvg(rn, "T0", 0, SurvP))} | {F1(SurvP(n9))} ／ {Per(n9.Turns, n9.N)} | {F1(SurvP(At(rn, "T0", BA.MainWave, 0).C))} | {F1(SurvP(At(rn, "T0", BA.MainWave + 1, 1).C))} | {F1(WinP(At(rn, "T0", 3, 1).C))} | {F1(SurvP(h))} ／ {Per(h.Turns, h.N)} | — |");
        }
        Console.WriteLine();
        Console.WriteLine("### 表A′ —— 全セル（台 × 版 × 波 × 倍率・勝率 ／ 全員生存 ／ 決着T ／ 落ちた/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | " + string.Join(" | ", WaveNames.Take(WaveHeavy + 1)) + " |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", WaveHeavy + 1)));
        foreach (string b in BoardNames)
            foreach (var v in Versions)
                for (int s = 0; s < BA.Scales.Length; s++)
                    Console.WriteLine($"| {b} | {v.Name} | {BA.Scales[s].Name} | " + string.Join(" | ", Enumerable.Range(0, WaveHeavy + 1).Select(w => { var c = At(b, v.Name, w, s).C; return $"{F1(WinP(c))} ／ {F1(SurvP(c))} ／ {Per(c.Turns, c.N)} ／ {Per(c.Fell, c.N)}"; })) + " |");
        Console.WriteLine();
    }

    static readonly (string B, int W, int S)[] CoreCells =
    {
        ("T3-244", BA.MainWave, 1), ("T3-244", BA.MainWave, 0), ("T3-244", WaveYoke, 1), ("T3-244", WaveHeavy, 1),
        ("T3-238", BA.MainWave, 1), ("T3-238", WaveYoke, 1), ("T3-238", WaveHeavy, 1),
        ("T3", BA.MainWave, 1), ("T3", WaveYoke, 1), ("T3", WaveHeavy, 1),
        ("T3′", BA.MainWave, 1), ("T3′", WaveYoke, 1),
    };
    static readonly (string B, int W, int S)[] TargetCells = { ("T3-244", WaveTarget1, 0), ("T3-244", WaveTarget9, 0), ("T3-238", WaveTarget1, 0), ("T3", WaveTarget1, 0) };
    static string WN(int w, int s) => IsTarget(w) ? $"{WaveNames[w]}（8T）" : $"{WaveNames[w]} {BA.Scales[s].Name}";

    static void TableB()
    {
        Console.WriteLine("## 表B —— 三角の循環（焼き尽くす → 指名 → ボルグの放つ → 呼び火・次の焼き尽くすまでに）");
        Console.WriteLine();
        Console.WriteLine("「緩い三角」＝ 指名を問わない（焼き尽くす → ボルグへのギフト → 放つ → 呼び火）。T0 には指名が無いので緩い三角だけが立つ。");
        Console.WriteLine("「途切れた」は放熱（指名）の札のある版だけ: " + string.Join(" ／ ", BreakNames) + "。的の波は 1〜8 ターン目（＝8ターンで何周）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | 焼き尽くす/戦 | **三角/戦**（1回以上の戦 %） | 緩い三角/戦 | 放つ/戦 | 途切れた/戦 " + string.Join(" ／ ", BreakNames.Select((_, i) => (i + 1).ToString())) + " |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|---|");
        foreach (var (b, w, s) in CoreCells.Concat(TargetCells))
            foreach (var v in Versions)
            {
                var t = At(b, v.Name, w, s).T;
                Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | {Per(t.Burnouts, t.N)} | **{Per(t.Tri, t.N)}**（{Pct(t.TriBattles, t.N)}） | {Per(t.Loose, t.N)} | {Per(t.Unleashes, t.N)} | {string.Join(" ／ ", t.Break.Select(x => Per(x, t.N)))} |");
            }
        Console.WriteLine();
        Console.WriteLine("### 表B′ —— 本編の長い波（第2〜5波の平均・400/300）と重い波");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 本編 400/300 三角/戦 ／ 緩い三角 ／ 焼き尽くす ／ 放つ | 重い波 400/300 三角 ／ 緩い ／ 焼き尽くす ／ 放つ | 本編 200/200 三角/戦 |");
        Console.WriteLine("|---|---|---|---|--:|");
        foreach (string b in BoardNames.Take(4))
            foreach (var v in Versions)
            {
                var h = At(b, v.Name, WaveHeavy, 1).T;
                string M(Func<TAgg, long> f, int s) => MainAvgT(b, v.Name, s, t => PerD(f(t), t.N)).ToString("F2");
                Console.WriteLine($"| {b} | {v.Name} | {M(t => t.Tri, 1)} ／ {M(t => t.Loose, 1)} ／ {M(t => t.Burnouts, 1)} ／ {M(t => t.Unleashes, 1)} | {Per(h.Tri, h.N)} ／ {Per(h.Loose, h.N)} ／ {Per(h.Burnouts, h.N)} ／ {Per(h.Unleashes, h.N)} | {M(t => t.Tri, 0)} |");
            }
        Console.WriteLine();
    }

    static void TableC()
    {
        Console.WriteLine("## 表C —— 大技の回数・ギフトの回数と間隔・相手の内訳");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | 焼き尽くす ／ 放つ ／ 残り火 /戦 | ギフトの手番/戦（火勢3 ／ 4 で撃った） | 間隔 1 ／ 2 ／ 3 ／ 4+ % | 相手/戦 | 1体目 ボルグ ／ ホタ ／ 相方 % | 2体目 ボルグ ／ ホタ ／ 相方 %（2体目/戦） | (b) 2体に渡した/戦 ／ 火勢3 で準備2体がいた/戦 |");
        Console.WriteLine("|---|---|---|---|---|---|--:|---|---|---|");
        foreach (var (b, w, s) in CoreCells.Concat(TargetCells))
            foreach (var v in Versions)
            {
                var cell = At(b, v.Name, w, s); var t = cell.T; var c = cell.C;
                long iv = c.Interval.Sum();
                long s1 = Enumerable.Range(0, 4).Sum(r => t.GiftSlotRole[1, r]), s2 = Enumerable.Range(0, 4).Sum(r => t.GiftSlotRole[2, r]);
                string R(int slot, long tot) => string.Join(" ／ ", new[] { 0, 1, 3 }.Select(r => Pct(t.GiftSlotRole[slot, r], tot)));
                Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | {Per(t.Burnouts, t.N)} ／ {Per(t.Unleashes, t.N)} ／ {Per(t.Embers, t.N)} | {Per(t.GiftHands, t.N)}（{Per(t.GiftAtLevel[3], t.N)} ／ {Per(t.GiftAtLevel[4], t.N)}） | {string.Join(" ／ ", Enumerable.Range(1, 4).Select(i => Pct(c.Interval[i], iv)))} | {Per(t.GiftRecips, t.N)} | {R(1, s1)} | {R(2, s2)}（{Per(s2, t.N)}） | {Per(t.GiftPairs, t.N)} ／ {(IsTarget(w) ? "—" : Per(t.GiftPairChance, t.N))} |");
            }
        Console.WriteLine();
    }

    static void TableD()
    {
        Console.WriteLine("## 表D —— 指名でギフトを受けたときのボルグの火勢（4 で放てた ／ 4 未満で通常の攻撃）");
        Console.WriteLine();
        Console.WriteLine("「ボルグのギフト」は指名を問わない（T0 はこちらだけ）。「手番なし」＝ 指名の後、ギフトの手番の前に敵が尽きた・ボルグが倒れた。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | 指名/戦 | 指名の火勢 1 ／ 2 ／ 3 ／ 4 % | 放った ／ 通常の攻撃 ／ 手番なし % | 印 付いた ／ 重ねず捨てた ／ 燃えず消えた /戦 | ボルグのギフト/戦 | その火勢 1 ／ 2 ／ 3 ／ 4 % | そのうち放った % |");
        Console.WriteLine("|---|---|---|--:|---|---|---|--:|---|--:|");
        foreach (var (b, w, s) in CoreCells.Concat(TargetCells))
            foreach (var v in Versions)
            {
                var t = At(b, v.Name, w, s).T;
                long cn = t.CalledLv.Sum(), bg = t.BorgGiftLv.Sum();
                Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | {Per(cn, t.N)} | {string.Join(" ／ ", Enumerable.Range(1, 4).Select(l => Pct(t.CalledLv[l], cn)))} | {Pct(t.CalledUnleash, cn)} ／ {Pct(t.CalledNormal, cn)} ／ {Pct(t.CalledNoHand, cn)} | {Per(t.CallMarks, t.N)} ／ {(IsTarget(w) ? "—" : Per(t.CallStacked, t.N))} ／ {Per(t.CallLost, t.N)} | {Per(bg, t.N)} | {string.Join(" ／ ", Enumerable.Range(1, 4).Select(l => Pct(t.BorgGiftLv[l], bg)))} | {Pct(t.BorgGiftUnleash, bg)} |");
            }
        Console.WriteLine();
    }

    static void TableE()
    {
        Console.WriteLine("## 表E —— ヒヨの火勢の推移（周回の頭の平均 ／ 火勢4 の割合）と育ちの出どころ（量/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => "T" + t)) + " | 煽り ／ 味方の燃え広がり ／ 火の粉（焼き尽くす）／ 火の粉（放つ） | 火の粉の回数 焼き尽くす ／ 放つ /戦 |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", TM)) + "---|---|");
        foreach (var (b, w, s) in CoreCells.Concat(TargetCells))
            foreach (var v in Versions)
            {
                var t = At(b, v.Name, w, s).T;
                string L(int tt) => t.HiyoLvCnt[tt] == 0 ? "—" : $"{(double)t.HiyoLvSum[tt] / t.HiyoLvCnt[tt]:F2}（{Pct(t.HiyoLvHist[tt, 4], t.HiyoLvCnt[tt])}）";
                Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | " + string.Join(" | ", Enumerable.Range(1, TM).Select(L)) + $" | {Per(t.GrowStoke, t.N)} ／ {Per(t.GrowSpread, t.N)} ／ {Per(t.SparkBurnoutGrew, t.N)} ／ {Per(t.SparkUnleashGrew, t.N)} | {Per(t.SparkBurnout, t.N)} ／ {Per(t.SparkUnleash, t.N)} |");
            }
        Console.WriteLine();
    }

    static readonly string[] TargetBoards = { "T3-244", "T3-238", "T3" };

    static void TableF()
    {
        Console.WriteLine("## 表F —— 的の波（HP 9999・攻1・倍率なし・1〜8 ターン目だけを読む・勝率は出さない）");
        Console.WriteLine();
        Console.WriteLine("撃破の読み手（追撃・撃破の衝撃・延焼ほか）は働かない。燃え広がりと敵の火勢は実戦より早く育つ。**毒の層が伸びる台（雷）では的が倒れる**。");
        Console.WriteLine();
        long Sum(FC.CAgg c, string k, int t) => c.TDealt.TryGetValue(k, out var a) ? a[t] : 0;
        long TrioT(FC.CAgg c, int t) => Trio.Sum(k => Sum(c, k, t)) + Sum(c, "tick", t);
        long MoveT(FC.CAgg c, int t) => Sum(c, "yomi", t) + Sum(c, "sero", t);
        long AllT(FC.CAgg c, int t) => c.TDealt.Values.Sum(a => a[t]);
        foreach (int w in new[] { WaveTarget1, WaveTarget9 })
        {
            var mv = At("参考 移動", "T0", w, 0).C; var th = At("参考 雷", "T0", w, 0).C;
            Console.WriteLine($"### F-1 ターンごとの与ダメ（{WaveNames[w]}・/戦・3体＋燃焼の刻み）と 8 ターンの周回（三角 ／ 焼き尽くす ／ 放つ ／ ギフト）");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => "T" + t)) + " | 1〜8T | 累積で ヨミ＋セロ を抜いたT | 1発の最大 | 三角 ／ 焼き尽くす ／ 放つ ／ ギフト |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("--:|", TM + 1)) + "---|--:|---|");
            foreach (string b in TargetBoards)
                foreach (var v in Versions)
                {
                    var cell = At(b, v.Name, w, 0); var c = cell.C; var t = cell.T;
                    int cumT = 0; double cs = 0, cm = 0;
                    for (int tt = 1; tt <= TM; tt++)
                    {
                        cs += (double)TrioT(c, tt) / c.N; cm += (double)MoveT(mv, tt) / mv.N;
                        if (cumT == 0 && cs > cm) cumT = tt;
                    }
                    Console.WriteLine($"| {b} | {v.Name} | " + string.Join(" | ", Enumerable.Range(1, TM).Select(tt => Per(TrioT(c, tt), c.N))) + $" | **{Per(Enumerable.Range(1, TM).Sum(tt => TrioT(c, tt)), c.N)}** | {(cumT == 0 ? "—" : "T" + cumT)} | {c.TMaxHit.Max()} | {Per(t.Tri, t.N)} ／ {Per(t.Burnouts, t.N)} ／ {Per(t.Unleashes, t.N)} ／ {Per(t.GiftHands, t.N)} |");
                }
            Console.WriteLine("| 参考 移動 | ヨミ＋セロ | " + string.Join(" | ", Enumerable.Range(1, TM).Select(tt => Per(MoveT(mv, tt), mv.N))) + $" | **{Per(Enumerable.Range(1, TM).Sum(tt => MoveT(mv, tt)), mv.N)}** | | {mv.TMaxHit.Max()} | |");
            Console.WriteLine("| 参考 雷 | 台の全員 | " + string.Join(" | ", Enumerable.Range(1, TM).Select(tt => Per(AllT(th, tt), th.N))) + $" | **{Per(Enumerable.Range(1, TM).Sum(tt => AllT(th, tt)), th.N)}** | 倒れた的 {Per(th.TFoeDeaths, th.N)} | {th.TMaxHit.Max()} | |");
            Console.WriteLine();
        }
        Console.WriteLine("### F-2 重い波（城塞の重装兵 ×3）: 与ダメ/戦 ボルグ ／ ホタ ／ 燃焼の刻み ／ 相方・決着T");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 勝率 ／ 決着T | ボルグ ／ ホタ ／ 刻み ／ 相方 | 合計 |");
        Console.WriteLine("|---|---|---|---|---|--:|");
        foreach (string b in new[] { "T3-244", "T3-238", "T3" })
            for (int s = 0; s < 2; s++)
                foreach (var v in Versions)
                {
                    var c = At(b, v.Name, WaveHeavy, s).C;
                    long partner = c.Dealt.Where(kv => kv.Key is not ("borg" or "hota" or "hiyo") && !kv.Key.StartsWith("tick")).Sum(kv => kv.Value);
                    Console.WriteLine($"| {b} | {BA.Scales[s].Name} | {v.Name} | {F1(WinP(c))} ／ {Per(c.Turns, c.N)} | {Dl(c, "borg")} ／ {Dl(c, "hota")} ／ {Dl(c, "tick")} ／ {Per(partner, c.N)} | {Per(c.Dealt.Values.Sum(), c.N)} |");
                }
        foreach (var (rn, _) in Refs)
            for (int s = 0; s < 2; s++)
            {
                var c = At(rn, "T0", WaveHeavy, s).C;
                Console.WriteLine($"| {rn} | {BA.Scales[s].Name} | — | {F1(WinP(c))} ／ {Per(c.Turns, c.N)} | ヨミ {Dl(c, "yomi")} ／ セロ {Dl(c, "sero")} | {Per(c.Dealt.Values.Sum(), c.N)} |");
            }
        Console.WriteLine();
    }
    static string Dl(FC.CAgg c, string id) => c.Dealt.TryGetValue(id, out long v) ? Per(v, c.N) : "—";

    static void TableG()
    {
        Console.WriteLine("## 表G —— ギフトの直後に倒れた敵（ギフトの手番の中で倒れた敵の数）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | ギフトの手番（相手）/戦 | 倒れた敵 /ギフトの手番 | 倒れた敵 /戦 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|");
        foreach (var (b, w, s) in CoreCells)
            foreach (var v in Versions)
            {
                var c = At(b, v.Name, w, s).C;
                Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | {Per(c.GiftHands, c.N)} | {Per(c.GiftKills, c.GiftHands)} | {Per(c.GiftKills, c.N)} |");
            }
        Console.WriteLine();
    }

    static void TableH()
    {
        Console.WriteLine("## 表H —— 手番の絵の単調さ（第239期の定義・九/新兵）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | 駒 | 手番/戦 | 絵の種類/戦 | 最多の絵 % | 続けて同じ % | 多い絵（上位3） |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|---|");
        foreach (string b in new[] { "T3-244", "T3-238", "T3" })
            foreach (var v in Versions)
                for (int s = 0; s < 2; s++)
                {
                    var a = At(b, v.Name, BA.MainWave, s).L;
                    foreach (string id in Trio)
                    {
                        if (!a.Pics.TryGetValue(id, out var pa) || pa.Hands == 0) continue;
                        var top = pa.Freq.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key} {Pct(kv.Value, pa.Hands)}");
                        Console.WriteLine($"| {b} | {v.Name} | {BA.Scales[s].Name} | {id} | {Per(pa.Hands, pa.Battles)} | {Per(pa.DistinctSum, pa.Battles)} | {Pct(pa.TopSum, pa.Hands)} | {Pct(pa.Repeats, pa.Pairs)} | {string.Join(" ／ ", top)} |");
                    }
                }
        Console.WriteLine();
    }

    static void TableI()
    {
        Console.WriteLine("## 表I —— 粛の波（本編 第二波）・軛の波（本編 第四波）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 倍率 | 版 | 勝率 ／ 全員生存 ／ 決着T | 落ちた/戦 ボルグ ／ ホタ ／ ヒヨ ／ 相方 | 三角/戦 | 放つ ／ 焼き尽くす /戦 | ギフト/戦 |");
        Console.WriteLine("|---|---|---|---|---|---|--:|---|--:|");
        foreach (string b in new[] { "T3-244", "T3-238", "T3" })
            foreach (int w in new[] { WaveHush, WaveYoke })
                for (int s = 0; s < 2; s++)
                    foreach (var v in Versions)
                    {
                        var cell = At(b, v.Name, w, s); var c = cell.C; var t = cell.T;
                        Console.WriteLine($"| {b} | {WaveNames[w]} | {BA.Scales[s].Name} | {v.Name} | {F1(WinP(c))} ／ {F1(SurvP(c))} ／ {Per(c.Turns, c.N)} | {string.Join(" ／ ", Enumerable.Range(0, 4).Select(r => Per(c.FellRole[r], c.N)))} | {Per(t.Tri, t.N)} | {Per(t.Unleashes, t.N)} ／ {Per(t.Burnouts, t.N)} | {Per(t.GiftHands, t.N)} |");
                    }
        Console.WriteLine();
    }

    static void CompareMoves()
    {
        Console.WriteLine("## `compare` 61 行 × 第1〜5波 × seed 0..199（版ごとに T0 からの動き）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var w = CompareRates();
        Console.WriteLine("| 版 | 動いたセル | 上がった ／ 下がった | 動いた行 |");
        Console.WriteLine("|---|--:|---|---|");
        for (int v = 1; v < Versions.Length; v++)
        {
            int moved = 0, up = 0, down = 0; var names = new List<string>();
            for (int i = 0; i < rows.Length; i++)
            {
                var d = Enumerable.Range(0, 5).Select(st => w[v, i, st] - w[0, i, st]).ToArray();
                int m = d.Count(x => Math.Abs(x) > 1e-9);
                if (m == 0) continue;
                moved += m; up += d.Count(x => x > 1e-9); down += d.Count(x => x < -1e-9);
                names.Add($"`{rows[i].Name}` " + string.Join(" ／ ", Enumerable.Range(0, 5).Where(st => Math.Abs(d[st]) > 1e-9).Select(st => $"第{st + 1}波 {w[0, i, st]:F1} → {w[v, i, st]:F1}")));
            }
            Console.WriteLine($"| {Versions[v].Name} | {moved} | {up} ／ {down} | {(names.Count == 0 ? "—" : string.Join("<br>", names))} |");
        }
        Console.WriteLine();
    }

    internal static double[,,] CompareRates()
    {
        var rows = CompareBuilds();
        var w = new double[Versions.Length, rows.Length, 5];
        for (int v = 0; v < Versions.Length; v++)
            for (int i = 0; i < rows.Length; i++)
            {
                var f = Apply(rows[i].F, Versions[v]);
                for (int st = 0; st < 5; st++)
                {
                    var stage = EnemyCatalog.Stages[st].Enemy;
                    var res = new bool[BA.Seeds];
                    Parallel.For(0, BA.Seeds, seed => res[seed] = BattleEngine.Run(f, stage, seed, verbose: false, ember: EmberRule.Pre256).PlayerWon);
                    w[v, i, st] = 100.0 * res.Count(x => x) / BA.Seeds;
                }
            }
        return w;
    }
}
