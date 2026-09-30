using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;

// firecycle run —— 段2 ＋ 表A〜I（指示書 §6.3）と的の表 F-1〜F-5（追記 B）、`compare` 61 行の版ごとの動き。
static partial class FireCycleDiag
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

    sealed record Cell(FB.LAgg L, CAgg C, EnemyFireDiag.EAgg E);
    static Dictionary<(string B, string V, int W, int S), Cell> _cells = new();
    static Cell At(string b, string v, int w, int s) => _cells[(b, v, w, IsTarget(w) ? 0 : s)];

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ranked = new Dictionary<string, List<FB.Pick>>();
        foreach (var v in Versions) ranked[v.Name] = FB.Ranked(PicksOf(v, out _));
        Console.WriteLine("# 第246期 段2 —— 三角の循環（版 Q0〜Q2-HP・Q1-選）＋ 的の波");
        Console.WriteLine();
        Stage1Summary(ranked);
        _cells = new();
        void Put(string b, string vn, Formation f)
        {
            for (int w = 0; w < WaveNames.Length; w++)
                for (int s = 0; s < (IsTarget(w) ? 1 : BA.Scales.Length); s++)
                {
                    var (l, c, e) = Measure(f, w, IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc);
                    _cells[(b, vn, w, s)] = new Cell(l, c, e);
                }
        }
        foreach (string bn in BoardNames) foreach (var v in Versions) Put(bn, v.Name, BoardOf(bn, v, ranked));
        foreach (var (rn, rf) in Refs) Put(rn, "Q0", rf());
        Console.Error.WriteLine($"  段2 の測定 {sw.Elapsed.TotalSeconds:F0} 秒");
        Console.WriteLine("## 台（版ごとの席）");
        Console.WriteLine();
        foreach (var v in Versions) foreach (string bn in BoardNames) Console.WriteLine($"- {v.Name} × {bn}: {BA.SeatsNamed(BoardOf(bn, v, ranked))}");
        foreach (var (rn, rf) in Refs) Console.WriteLine($"- {rn}（版に依らない）: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
        TableA(); TableB(); TableC(); TableD(); TableE(); TableF(); TableTarget(); TableG(); TableH(); TableI();
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
    static double MainAvg(string b, string v, int s, Func<CAgg, double> f) => MainWaves.Average(w => f(At(b, v, w, s).C));
    static double WinP(CAgg c) => c.N == 0 ? double.NaN : 100.0 * c.Wins / c.N;
    static double SurvP(CAgg c) => c.N == 0 ? double.NaN : 100.0 * c.AllSurv / c.N;

    static void TableA()
    {
        Console.WriteLine("## 表A —— 台 × 版（勝率・全員生存・決着T・落ちた駒）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 本編 400/300 勝率 ／ 全員生存（第2〜5波の平均） | 本編 200/200 勝率 ／ 全員生存 | 九/新兵 400/300 全員生存 ／ 決着T | 九/新兵 200/200 全員生存 | 第五波 400/300 勝率 | 重い波 400/300 全員生存 ／ 決着T | 落ちた/戦（本編 400/300）ボルグ ／ ホタ ／ ヒヨ ／ 相方 |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|---|---|");
        foreach (string b in BoardNames)
            foreach (var v in Versions)
            {
                var n9 = At(b, v.Name, BA.MainWave, 1).C; var h = At(b, v.Name, WaveHeavy, 1).C;
                long nn = MainWaves.Sum(w => At(b, v.Name, w, 1).C.N);
                string fell = string.Join(" ／ ", Enumerable.Range(0, 4).Select(r => Per(MainWaves.Sum(w => At(b, v.Name, w, 1).C.FellRole[r]), nn)));
                Console.WriteLine($"| {b} | {v.Name} | {F1(MainAvg(b, v.Name, 1, WinP))} ／ {F1(MainAvg(b, v.Name, 1, SurvP))} | {F1(MainAvg(b, v.Name, 0, WinP))} ／ {F1(MainAvg(b, v.Name, 0, SurvP))} | {F1(SurvP(n9))} ／ {Per(n9.Turns, n9.N)} | {F1(SurvP(At(b, v.Name, BA.MainWave, 0).C))} | {F1(WinP(At(b, v.Name, 3, 1).C))} | {F1(SurvP(h))} ／ {Per(h.Turns, h.N)} | {fell} |");
            }
        foreach (var (rn, _) in Refs)
        {
            var n9 = At(rn, "Q0", BA.MainWave, 1).C; var h = At(rn, "Q0", WaveHeavy, 1).C;
            Console.WriteLine($"| {rn} | — | {F1(MainAvg(rn, "Q0", 1, WinP))} ／ {F1(MainAvg(rn, "Q0", 1, SurvP))} | {F1(MainAvg(rn, "Q0", 0, WinP))} ／ {F1(MainAvg(rn, "Q0", 0, SurvP))} | {F1(SurvP(n9))} ／ {Per(n9.Turns, n9.N)} | {F1(SurvP(At(rn, "Q0", BA.MainWave, 0).C))} | {F1(WinP(At(rn, "Q0", 3, 1).C))} | {F1(SurvP(h))} ／ {Per(h.Turns, h.N)} | — |");
        }
        Console.WriteLine();
        Console.WriteLine("### 表A′ —— 全セル（台 × 版 × 波 × 倍率・勝率 ／ 全員生存 ／ 決着T）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | " + string.Join(" | ", WaveNames.Take(WaveHeavy + 1)) + " |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", WaveHeavy + 1)));
        foreach (string b in BoardNames)
            foreach (var v in Versions)
                for (int s = 0; s < BA.Scales.Length; s++)
                    Console.WriteLine($"| {b} | {v.Name} | {BA.Scales[s].Name} | " + string.Join(" | ", Enumerable.Range(0, WaveHeavy + 1).Select(w => { var c = At(b, v.Name, w, s).C; return $"{F1(WinP(c))} ／ {F1(SurvP(c))} ／ {Per(c.Turns, c.N)}"; })) + " |");
        Console.WriteLine();
    }

    static readonly (string B, int W, int S)[] CoreCells =
    {
        ("T3-244", BA.MainWave, 1), ("T3-244", WaveYoke, 1), ("T3-244", WaveHeavy, 1), ("T3-244", BA.MainWave, 0),
        ("T3-238", BA.MainWave, 1), ("T3-238", WaveYoke, 1), ("T3-238", WaveHeavy, 1),
        ("T3", BA.MainWave, 1), ("T3", WaveYoke, 1),
    };

    static void TableB()
    {
        Console.WriteLine("## 表B —— ギフトの間隔（ヒヨがギフトを撃ったターン・同じ戦の続けて2回の差）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 倍率 | 版 | ギフト/戦（撃った戦 %） | 初めてのギフトのT | 間隔 1 ／ 2 ／ 3 ／ 4+ % | 2体に渡した % |");
        Console.WriteLine("|---|---|---|---|---|--:|---|--:|");
        foreach (var (b, w, s) in CoreCells.Append(("T3-244", WaveTarget1, 0)))
            foreach (var v in Versions)
            {
                var c = At(b, v.Name, w, s).C; long iv = c.Interval.Sum();
                string first = c.GiftBattles == 0 ? "—" : Per(Enumerable.Range(0, TM + 1).Sum(t => (long)t * c.FirstGift[t]), c.GiftBattles);
                Console.WriteLine($"| {b} | {WaveNames[w]} | {(IsTarget(w) ? "—" : BA.Scales[s].Name)} | {v.Name} | {Per(c.Gifts, c.N)}（{Pct(c.GiftBattles, c.N)}） | {first} | {string.Join(" ／ ", Enumerable.Range(1, 4).Select(i => Pct(c.Interval[i], iv)))} | {Pct(c.GiftsMulti, c.Gifts)} |");
            }
        Console.WriteLine();
    }

    static void TableC()
    {
        Console.WriteLine("## 表C —— 三角の循環（焼き尽くす → 放熱を蓄える → 放熱でボルグ +1 → ボルグが放つ・次の焼き尽くすまでに）");
        Console.WriteLine();
        Console.WriteLine("「途切れた」は放熱の札のある版だけ。場所は 蓄えなかった ／ 使う前に戦が終わった ／ 使ったときボルグが既に火勢4（上がる余地が無い）／ 使ったとき燃えていなかった ／ 育ったが放たなかった。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 倍率 | 版 | 焼き尽くす/戦 | 放熱 蓄えた ／ 使った ／ 育った /戦 | 放つ/戦 | 焼き尽くす → 放つ /戦 | **三角/戦**（1回以上の戦 %） | 途切れた 蓄えず ／ 使えず ／ 既に4 ／ 燃えず ／ 放たず | 火の粉で育った/戦 | 燃え広がりで育たなかった/戦 |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|--:|---|---|--:|--:|");
        foreach (var (b, w, s) in CoreCells.Append(("T3-244", WaveTarget1, 0)).Append(("T3-244", WaveTarget9, 0)).Append(("T3-238", WaveTarget1, 0)))
            foreach (var v in Versions)
            {
                var c = At(b, v.Name, w, s).C;
                Console.WriteLine($"| {b} | {WaveNames[w]} | {(IsTarget(w) ? "—" : BA.Scales[s].Name)} | {v.Name} | {Per(c.Burnouts, c.N)} | {Per(c.Radiates, c.N)} ／ {Per(c.RadiateUsed, c.N)} ／ {Per(c.RadiateGrew, c.N)} | {Per(c.Unleashes, c.N)} | {Per(c.BurnThenUnleash, c.N)} | **{Per(c.Triangles, c.N)}**（{Pct(c.TriBattles, c.N)}） | {string.Join(" ／ ", c.TriBreak.Select(x => Per(x, c.N)))} | {Per(c.SparkGrew, c.N)} | {Per(c.SparkSkipped, c.N)} |");
            }
        Console.WriteLine();
    }

    static void TableD()
    {
        Console.WriteLine("## 表D —— ヒヨの相手選び（台本の煽り・ギフトの手番とそのときの火勢から、版に依らず数え直した）");
        Console.WriteLine();
        Console.WriteLine("「型が変わる」＝ その相手の火勢 +1 で手番の型が変わる（ホタの段）か、火勢4 に届いて大技の準備ができる。「選べた周回 %」＝ 周回の頭に、その相手が味方にいた周回の割合（近似）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 倍率 | 版 | 煽り/戦 | 煽りの相手 ボルグ ／ ホタ ／ 相方 % | 型が変わる相手 % | （型が変わる相手がいた周回/戦） | ギフトの相手/戦 | 相手 ボルグ ／ ホタ ／ 相方 % | 大技の準備 % | （準備の味方がいた周回/戦） |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|--:|--:|---|--:|--:|");
        foreach (var (b, w, s) in CoreCells)
            foreach (var v in Versions)
            {
                var c = At(b, v.Name, w, s).C;
                Console.WriteLine($"| {b} | {WaveNames[w]} | {BA.Scales[s].Name} | {v.Name} | {Per(c.Stokes, c.N)} | {string.Join(" ／ ", new[] { 0, 1, 3 }.Select(i => Pct(c.StokeRole[i], c.Stokes)))} | {Pct(c.StokeForm, c.Stokes)} | {Per(c.StokeFormAvail, c.N)} | {Per(c.GiftRecips, c.N)} | {string.Join(" ／ ", new[] { 0, 1, 3 }.Select(i => Pct(c.GiftRole[i], c.GiftRecips)))} | {Pct(c.GiftReady, c.GiftRecips)} | {Per(c.GiftReadyAvail, c.N)} |");
            }
        Console.WriteLine();
    }

    static void TableE()
    {
        Console.WriteLine("## 表E —— ホタの手番の型（手番数/戦 ／ 1手番の総量の平均 ／ 1手番の総量の最大 ／ 1発の最大）と、火勢 2・3・4 に初めて届いたターン");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 倍率 | 版 | " + string.Join(" | ", HandKinds.Skip(1)) + " | 届いたT 2・3・4 |");
        Console.WriteLine("|---|---|---|---|" + string.Concat(Enumerable.Repeat("---|", HandKinds.Length - 1)) + "---|");
        foreach (var (b, w, s) in CoreCells.Append(("T3-244", WaveTarget1, 0)).Append(("T3-244", WaveTarget9, 0)))
            foreach (var v in Versions)
            {
                var c = At(b, v.Name, w, s).C;
                string cell(int k) => c.HandN[k] == 0 ? "—" : $"{Per(c.HandN[k], c.N)} ／ {Per(c.HandDmg[k], c.HandN[k])} ／ {c.HandMax[k]} ／ {c.HitMax[k]}";
                string reach = string.Join("・", Enumerable.Range(2, 3).Select(l => c.StageReachN[l] == 0 ? "—" : Per(c.StageReachT[l], c.StageReachN[l])));
                Console.WriteLine($"| {b} | {WaveNames[w]} | {(IsTarget(w) ? "—" : BA.Scales[s].Name)} | {v.Name} | " + string.Join(" | ", Enumerable.Range(1, HandKinds.Length - 1).Select(cell)) + $" | {reach} |");
            }
        Console.WriteLine();
    }

    static string Dl(CAgg c, string id) => c.Dealt.TryGetValue(id, out long v) ? Per(v, c.N) : "—";

    static void TableF()
    {
        Console.WriteLine("## 表F —— 重い波（城塞の重装兵 ×3）: 3体の1戦の与ダメ・決着T・敵の火勢");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 勝率 ／ 決着T | 与ダメ/戦 ボルグ ／ ホタ ／ 燃焼の刻み ／ 相方 | 火勢4 に届いた敵/戦（届いた戦 %・初めて届いたT） | 敵の脆さの分/戦（うち 25% 超え） | 1戦の与ダメの合計 |");
        Console.WriteLine("|---|---|---|---|---|---|---|--:|");
        foreach (string b in new[] { "T3-244", "T3-238", "T3" })
            for (int s = 0; s < 2; s++)
                foreach (var v in Versions)
                {
                    var cell = At(b, v.Name, WaveHeavy, s); var c = cell.C; var e = cell.E;
                    long partner = c.Dealt.Where(kv => kv.Key is not ("borg" or "hota" or "hiyo") && !kv.Key.StartsWith("tick")).Sum(kv => kv.Value);
                    long all = c.Dealt.Values.Sum();
                    string f4 = c.FoeLv4Battles == 0 ? "—" : Per(Enumerable.Range(0, TM + 1).Sum(t => (long)t * c.FoeLv4Turn[t]), c.FoeLv4Battles);
                    Console.WriteLine($"| {b} | {BA.Scales[s].Name} | {v.Name} | {F1(WinP(c))} ／ {Per(c.Turns, c.N)} | {Dl(c, "borg")} ／ {Dl(c, "hota")} ／ {Dl(c, "tick")} ／ {Per(partner, c.N)} | {Per(c.FoeReach4, c.N)}（{Pct(c.FoeLv4Battles, c.N)}・{f4}） | {Per(e.FoeBrittleAll, e.N)}（{Per(e.BritUp.Sum(), e.N)}） | {Per(all, c.N)} |");
                }
        foreach (var (rn, _) in Refs)
            for (int s = 0; s < 2; s++)
            {
                var c = At(rn, "Q0", WaveHeavy, s).C;
                Console.WriteLine($"| {rn} | {BA.Scales[s].Name} | — | {F1(WinP(c))} ／ {Per(c.Turns, c.N)} | ヨミ {Dl(c, "yomi")} ／ セロ {Dl(c, "sero")} | — | — | {Per(c.Dealt.Values.Sum(), c.N)} |");
            }
        Console.WriteLine();
    }

    static readonly string[] TargetBoards = { "T3-244", "T3-238", "T3" };

    static void TableTarget()
    {
        Console.WriteLine("## 表F-1〜F-5 —— 的の波（HP 9999・攻1・速5・倍率なし・1〜8 ターン目だけを読む・勝率は出さない）");
        Console.WriteLine();
        Console.WriteLine("撃破の読み手（追撃・撃破の衝撃・延焼ほか）は働かない（的は倒れない想定）。ただし**毒の層が伸びる台では的が倒れる**ので、倒れた的の数を列に出した。燃え広がりと敵の火勢は実戦より早く育つ。");
        Console.WriteLine();
        long Sum(CAgg c, string k, int t) => c.TDealt.TryGetValue(k, out var a) ? a[t] : 0;
        long TrioT(CAgg c, int t) => Trio.Sum(k => Sum(c, k, t)) + Sum(c, "tick", t);
        long MoveT(CAgg c, int t) => Sum(c, "yomi", t) + Sum(c, "sero", t);
        long AllT(CAgg c, int t) => c.TDealt.Values.Sum(a => a[t]);
        foreach (int w in new[] { WaveTarget1, WaveTarget9 })
        {
            var mv = At("参考 移動", "Q0", w, 0).C; var th = At("参考 雷", "Q0", w, 0).C;
            Console.WriteLine($"### F-1 ターンごとの与ダメ（{WaveNames[w]}・/戦）");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 駒 | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => "T" + t)) + " | 1〜8T | 累積で ヨミ＋セロ を抜いたT ／ ターン単独で抜いたT |");
            Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("--:|", TM + 1)) + "---|");
            foreach (string b in TargetBoards)
                foreach (var v in Versions)
                {
                    var c = At(b, v.Name, w, 0).C;
                    foreach (string k in new[] { "borg", "hota", "hiyo", "tick" })
                        Console.WriteLine($"| {b} | {v.Name} | {k} | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => Per(Sum(c, k, t), c.N))) + $" | {Per(Enumerable.Range(1, TM).Sum(t => Sum(c, k, t)), c.N)} | |");
                    int cumT = 0, oneT = 0; double cs = 0, cm = 0;
                    for (int t = 1; t <= TM; t++)
                    {
                        double a = (double)TrioT(c, t) / c.N, m = (double)MoveT(mv, t) / mv.N;
                        cs += a; cm += m;
                        if (cumT == 0 && cs > cm) cumT = t;
                        if (oneT == 0 && a > m) oneT = t;
                    }
                    Console.WriteLine($"| {b} | {v.Name} | **3体＋刻み** | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => Per(TrioT(c, t), c.N))) + $" | **{Per(Enumerable.Range(1, TM).Sum(t => TrioT(c, t)), c.N)}** | {(cumT == 0 ? "—" : "T" + cumT)} ／ {(oneT == 0 ? "—" : "T" + oneT)} |");
                    Console.WriteLine($"| {b} | {v.Name} | 台の全員 | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => Per(AllT(c, t), c.N))) + $" | {Per(Enumerable.Range(1, TM).Sum(t => AllT(c, t)), c.N)} | 倒れた的 {Per(c.TFoeDeaths, c.N)} |");
                }
            Console.WriteLine("| 参考 移動 | — | **ヨミ＋セロ** | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => Per(MoveT(mv, t), mv.N))) + $" | **{Per(Enumerable.Range(1, TM).Sum(t => MoveT(mv, t)), mv.N)}** | |");
            Console.WriteLine("| 参考 移動 | — | 台の全員 | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => Per(AllT(mv, t), mv.N))) + $" | {Per(Enumerable.Range(1, TM).Sum(t => AllT(mv, t)), mv.N)} | 倒れた的 {Per(mv.TFoeDeaths, mv.N)} |");
            Console.WriteLine("| 参考 雷 | — | 台の全員 | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => Per(AllT(th, t), th.N))) + $" | {Per(Enumerable.Range(1, TM).Sum(t => AllT(th, t)), th.N)} | 倒れた的 {Per(th.TFoeDeaths, th.N)} |");
            Console.WriteLine();

            Console.WriteLine($"### F-2 1発の最大（ターンごと・{WaveNames[w]}）");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => "T" + t)) + " |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("--:|", TM)));
            foreach (string b in TargetBoards)
                foreach (var v in Versions)
                {
                    var c = At(b, v.Name, w, 0).C;
                    Console.WriteLine($"| {b} | {v.Name} | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => c.TMaxHit[t].ToString())) + " |");
                }
            Console.WriteLine("| 参考 移動 | — | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => mv.TMaxHit[t].ToString())) + " |");
            Console.WriteLine("| 参考 雷 | — | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => th.TMaxHit[t].ToString())) + " |");
            Console.WriteLine();

            Console.WriteLine($"### F-3 火勢の推移（周回の頭・{WaveNames[w]}）: ボルグ ／ ホタ ／ ヒヨ ／ 的の平均（的の火勢4 %）");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => "T" + t)) + " | 的が火勢4 に初めて届いたT |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", TM)) + "--:|");
            foreach (string b in TargetBoards)
                foreach (var v in Versions)
                {
                    var c = At(b, v.Name, w, 0).C;
                    string L(int r, int t) => c.TLvCnt[r, t] == 0 ? "—" : ((double)c.TLvSum[r, t] / c.TLvCnt[r, t]).ToString("F1");
                    string f4 = c.FoeLv4Battles == 0 ? "—" : Per(Enumerable.Range(0, TM + 1).Sum(t => (long)t * c.FoeLv4Turn[t]), c.FoeLv4Battles);
                    Console.WriteLine($"| {b} | {v.Name} | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t =>
                        $"{L(0, t)}・{L(1, t)}・{L(2, t)} ／ {(c.TFoeLvCnt[t] == 0 ? "—" : ((double)c.TFoeLvSum[t] / c.TFoeLvCnt[t]).ToString("F1"))}（{Pct(c.TFoe4[t], c.TFoeLvCnt[t])}）")) + $" | {f4} |");
                }
            Console.WriteLine();

            Console.WriteLine($"### F-4 循環の周回数（8 ターンで・{WaveNames[w]}・/戦）");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | ギフト | 焼き尽くす | 放熱を使った | 放つ | 焼き尽くす → 放つ | **三角** | 途切れた 蓄えず ／ 使えず ／ 既に4 ／ 燃えず ／ 放たず |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|---|");
            foreach (string b in TargetBoards)
                foreach (var v in Versions)
                {
                    var c = At(b, v.Name, w, 0).C;
                    Console.WriteLine($"| {b} | {v.Name} | {Per(c.TGifts, c.N)} | {Per(c.TBurnouts, c.N)} | {Per(c.TRadiateUsed, c.N)} | {Per(c.TUnleashes, c.N)} | {Per(c.BurnThenUnleash, c.N)} | **{Per(c.TTriangles, c.N)}** | {string.Join(" ／ ", c.TriBreak.Select(x => Per(x, c.N)))} |");
                }
            Console.WriteLine();
        }
        Console.WriteLine("### F-5 的・一 と 的・九 の差（1〜8T の与ダメ/戦・3体＋刻み ／ 台の全員）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 的・一 | 的・九 | 九 ÷ 一（3体＋刻み） | 九 ÷ 一（台の全員） |");
        Console.WriteLine("|---|---|---|---|--:|--:|");
        foreach (string b in TargetBoards.Concat(Refs.Select(r => r.Name)))
            foreach (var v in b.StartsWith("参考") ? new[] { VerOf("Q0") } : Versions)
            {
                var c1 = At(b, v.Name, WaveTarget1, 0).C; var c9 = At(b, v.Name, WaveTarget9, 0).C;
                double t1 = Enumerable.Range(1, TM).Sum(t => (double)TrioT(c1, t)) / c1.N, t9 = Enumerable.Range(1, TM).Sum(t => (double)TrioT(c9, t)) / c9.N;
                double a1 = Enumerable.Range(1, TM).Sum(t => (double)AllT(c1, t)) / c1.N, a9 = Enumerable.Range(1, TM).Sum(t => (double)AllT(c9, t)) / c9.N;
                Console.WriteLine($"| {b} | {(b.StartsWith("参考") ? "—" : v.Name)} | {t1:F0} ／ {a1:F0} | {t9:F0} ／ {a9:F0} | {(t1 == 0 ? "—" : (t9 / t1).ToString("F2"))} | {(a1 == 0 ? "—" : (a9 / a1).ToString("F2"))} |");
            }
        Console.WriteLine();
    }

    static void TableG()
    {
        Console.WriteLine("## 表G —— ギフトの直後に倒れた敵（ギフトの手番の中で倒れた敵の数）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 倍率 | 版 | ギフトの手番/戦 | 倒れた敵 /ギフトの手番 | 倒れた敵 /戦 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|");
        foreach (var (b, w, s) in CoreCells)
            foreach (var v in Versions)
            {
                var c = At(b, v.Name, w, s).C;
                Console.WriteLine($"| {b} | {WaveNames[w]} | {BA.Scales[s].Name} | {v.Name} | {Per(c.GiftHands, c.N)} | {Per(c.GiftKills, c.GiftHands)} | {Per(c.GiftKills, c.N)} |");
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
        Console.WriteLine("| 台 | 波 | 倍率 | 版 | 勝率 ／ 全員生存 ／ 決着T | 落ちた/戦 ボルグ ／ ホタ ／ ヒヨ ／ 相方 | ホタの与ダメ/戦 | ホタの1発の最大 | ギフト/戦 |");
        Console.WriteLine("|---|---|---|---|---|---|--:|--:|--:|");
        foreach (string b in new[] { "T3-244", "T3-238", "T3" })
            foreach (int w in new[] { WaveHush, WaveYoke })
                for (int s = 0; s < 2; s++)
                    foreach (var v in Versions)
                    {
                        var c = At(b, v.Name, w, s).C;
                        long hmax = c.HitMax.Max();
                        Console.WriteLine($"| {b} | {WaveNames[w]} | {BA.Scales[s].Name} | {v.Name} | {F1(WinP(c))} ／ {F1(SurvP(c))} ／ {Per(c.Turns, c.N)} | {string.Join(" ／ ", Enumerable.Range(0, 4).Select(r => Per(c.FellRole[r], c.N)))} | {Dl(c, "hota")} | {hmax} | {Per(c.Gifts, c.N)} |");
                    }
        Console.WriteLine();
    }

    static void CompareMoves()
    {
        Console.WriteLine("## `compare` 61 行 × 第1〜5波 × seed 0..199（版ごとに Q0 からの動き）");
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
                    Parallel.For(0, BA.Seeds, seed => res[seed] = BattleEngine.Run(f, stage, seed, verbose: false).PlayerWon);
                    w[v, i, st] = 100.0 * res.Count(x => x) / BA.Seeds;
                }
            }
        return w;
    }
}
