using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// firekindle run —— 段2 ＋ 表A〜H（指示書 §5.3）。phase0 は同じ集計を M0 だけで出す（予測の材料・Q0-1〜Q0-5）。
static partial class FireKindleDiag
{
    static readonly string[] FixedBoards = { "T3-244", "T3-238", "雷＋ボルグ" };
    static string[] BoardNames = FixedBoards;
    static Formation BoardOf(string name, FB.Ver v, Dictionary<string, List<FB.Pick>>? ranked) => name switch
    {
        "T3-244" => Apply(FC.T3244, v),
        "T3-238" => Apply(FC.T3238, v),
        "雷＋ボルグ" => Apply(FC.ThunderBorg, v),
        "T3(M0選)" => Apply(FB.Dec(ranked!["M0"][0].Best), v),
        "T3(M1選)" => Apply(FB.Dec(ranked!["M1"][0].Best), v),
        _ => throw new ArgumentException(name),
    };
    static readonly (string Name, Func<Formation> F)[] Refs = { ("参考 移動", () => BA.RefMove), ("参考 雷", () => BA.RefThunder) };
    const string RefVer = "—";
    static readonly int[] MainWaves = { 0, 1, 2, 3 };
    const int WaveHush = FC.WaveHush, WaveYoke = FC.WaveYoke, WaveHeavy = FC.WaveHeavy, WaveT1 = FC.WaveTarget1, WaveT9 = FC.WaveTarget9;

    static Dictionary<(string B, string V, int W, int S), Cell> _cells = new();
    static FB.Ver[] _vers = Versions;
    static Cell At(string b, string v, int w, int s) => _cells[(b, v, w, IsTarget(w) ? 0 : s)];

    static void Put(string b, string vn, Formation f)
    {
        for (int w = 0; w < WaveNames.Length; w++)
            for (int s = 0; s < (IsTarget(w) ? 1 : BA.Scales.Length); s++)
                _cells[(b, vn, w, s)] = Measure(f, w, IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc);
    }

    /// <summary>波のまとまり（表B〜H の列）: 九/新兵 400/300 ／ 本編 第2〜5波 400/300 ／ 重い波 400/300 ／ 的・一 ／ 的・九。</summary>
    static readonly (string Name, (int W, int S)[] Cells)[] Groups =
    {
        ("九/新兵 400/300", new[] { (BA.MainWave, 1) }),
        ("九/新兵 200/200", new[] { (BA.MainWave, 0) }),
        ("本編 400/300", new[] { (0, 1), (1, 1), (2, 1), (3, 1) }),
        ("重い波 400/300", new[] { (WaveHeavy, 1) }),
        ("的・一（8T）", new[] { (WaveT1, 0) }),
        ("的・九（8T）", new[] { (WaveT9, 0) }),
    };
    static MAgg Grp(string b, string v, (int W, int S)[] cells)
    {
        var m = new MAgg();
        foreach (var (w, s) in cells) m.Merge(At(b, v, w, s).M);
        return m;
    }
    static IEnumerable<(string B, string V)> Rows()
    {
        foreach (string b in BoardNames) foreach (var v in _vers) yield return (b, v.Name);
    }

    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _vers = new[] { VerOf("M0") };
        BoardNames = FixedBoards;
        _cells = new();
        foreach (string bn in BoardNames) Put(bn, "M0", BoardOf(bn, _vers[0], null));
        Console.WriteLine("# 第252期 Phase 0 —— M0（＝ 規定）で台の駒を数える（予測の材料）");
        Console.WriteLine();
        foreach (string bn in BoardNames) Console.WriteLine($"- {bn}: {BA.SeatsNamed(BoardOf(bn, _vers[0], null))}");
        Console.WriteLine();
        Q01();
        TableB(); TableC(); TableD(); TableE(); TableG(); TableH();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒・seed 0..{BA.Seeds - 1}・verbose）");
    }

    /// <summary>Q0-1・Q0-2: M0 でボルグが火の鎧・盾の配りで切った量（B1 の燃料）と、火勢4 で来た育ち（あぶれた火の機会）。</summary>
    static void Q01()
    {
        Console.WriteLine("## Q0-1・Q0-2 —— 守るほど燃え上がるの燃料（ボルグが切った被ダメ/戦）と、火勢4 で捨てていた育ち（/戦）");
        Console.WriteLine();
        Console.WriteLine("切った量は M0 の駒ごとの帳簿（`FireArmorSaved` ／ `FireWardSaved`・燃えていない間に盾の配りで切った分も含む・戦全体）。÷ 30 が B1 の +1 の見込み（上限）。火勢4 の育ちは `MaxGrowChanceBy`（札が無くても数える・戦全体）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 決着T | 火の鎧で切った ／ 盾の配りで切った /戦 | ÷ 30 ＝ +1 の見込み/戦 | 火勢4 で来た育ち/戦 ボルグ ／ ヒヨ ／ ホタ |");
        Console.WriteLine("|---|---|--:|---|--:|---|");
        foreach (string b in BoardNames)
            foreach (var (gn, cells) in Groups)
            {
                long n = 0, turns = 0, sa = 0, sw = 0; var m = Grp(b, "M0", cells);
                foreach (var (w, s) in cells) { var c = At(b, "M0", w, s); n += c.C.N; turns += c.C.Turns; sa += c.M.TallyArmorSaved; sw += c.M.TallyWardSaved; }
                Console.WriteLine($"| {b} | {gn} | {Per(turns, n)} | {Per(sa, n)} ／ {Per(sw, n)} | {Per((sa + sw) / 30, n)} | {Per(m.BorgMaxGrow, m.N)} ／ {Per(m.HiyoMaxGrow, m.N)} ／ {Per(m.HotaMaxGrow, m.N)} |");
            }
        Console.WriteLine();
    }

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ranked = new Dictionary<string, List<FB.Pick>>();
        foreach (string vn in PickVersions) ranked[vn] = FB.Ranked(PicksOf(VerOf(vn), out _));
        Console.WriteLine("# 第252期 段2 —— ボルグが育つ口・ボルグとヒヨのあぶれた火（版 M0〜M2）");
        Console.WriteLine();
        Stage1Summary(ranked);
        _vers = Versions;
        BoardNames = FixedBoards.Concat(new[] { "T3(M0選)", "T3(M1選)" }).ToArray();
        _cells = new();
        foreach (string bn in BoardNames) foreach (var v in Versions) Put(bn, v.Name, BoardOf(bn, v, ranked));
        foreach (var (rn, rf) in Refs) Put(rn, RefVer, rf());
        Console.Error.WriteLine($"  段2 の測定 {sw.Elapsed.TotalSeconds:F0} 秒");
        Console.WriteLine("## 台（席は版に依らない・版はボルグ・ヒヨの札だけを差し替える）");
        Console.WriteLine();
        foreach (string bn in BoardNames) Console.WriteLine($"- {bn}: {BA.SeatsNamed(BoardOf(bn, Versions[^1], ranked))}");
        foreach (var (rn, rf) in Refs) Console.WriteLine($"- {rn}（版に依らない）: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
        foreach (var v in Versions) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();
        TableA(); TableA2(); TableB(); TableC(); TableD(); TableE(); TableF(); TableG(); TableH(); TableDeath();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒・seed 0..{BA.Seeds - 1}・verbose）");
    }

    static string Sc(FB.Score4 s) => $"{s.S.Sv}/40・400/300 {s.Sv4}/40・落 {s.S.Fell}・勝 {s.S.W}";
    static void Stage1Summary(Dictionary<string, List<FB.Pick>> ranked)
    {
        Console.WriteLine("## 段1（T3 1,081 組 × 席 120 × seed 1000..1039・九/新兵 × 200/200 → 400/300 → 落 → 決着T）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 1位（相方） | 席 | 得点 | 2位 | 3位 | 上位10 の相方（のべ） | 200/200 で 40/40 ／ 400/300 も 40/40 の組 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        foreach (string vn in PickVersions)
        {
            var rk = ranked[vn];
            var b = rk[0];
            int full = rk.Count(p => p.BestS.S.Sv == BA.PickSeeds);
            int full4 = rk.Count(p => p.BestS.S.Sv == BA.PickSeeds && p.BestS.Sv4 == BA.PickSeeds);
            string top10 = string.Join(" ", rk.Take(10).SelectMany(p => p.Partners).GroupBy(x => x).OrderByDescending(g => g.Count()).Take(6).Select(g => $"{g.Key} {g.Count()}"));
            Console.WriteLine($"| {vn} | {string.Join("・", b.Partners)} | {BA.SeatsNamed(FB.Dec(b.Best))} | {Sc(b.BestS)} | {string.Join("・", rk[1].Partners)} {Sc(rk[1].BestS)} | {string.Join("・", rk[2].Partners)} {Sc(rk[2].BestS)} | {top10} | {full} ／ {full4} |");
        }
        Console.WriteLine();
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static double MainAvg(string b, string v, int s, Func<FC.CAgg, double> f) => MainWaves.Average(w => f(At(b, v, w, s).C));
    static double WinP(FC.CAgg c) => c.N == 0 ? double.NaN : 100.0 * c.Wins / c.N;
    static double SurvP(FC.CAgg c) => c.N == 0 ? double.NaN : 100.0 * c.AllSurv / c.N;
    static string Avg(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");

    static void TableA()
    {
        Console.WriteLine("## 表A —— 台 × 版（勝率 ／ 全員生存 ／ 決着T ／ 落ちた駒）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 本編 400/300 勝率 ／ 全員生存（第2〜5波の平均） | 本編 200/200 勝率 ／ 全員生存 | 本編 115/115 全員生存 | 九/新兵 400/300 全員生存 ／ 決着T | 九/新兵 200/200 全員生存 | 九/農兵 400/300 全員生存 | 第五波 400/300 勝率 ／ 全員生存 | 重い波 400/300 全員生存 ／ 決着T | 落ちた/戦（本編 400/300）ボルグ ／ ホタ ／ ヒヨ ／ 相方 |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|--:|---|---|---|");
        foreach (var (b, v) in Rows().Concat(Refs.Select(x => (x.Name, RefVer))))
        {
            var n9 = At(b, v, BA.MainWave, 1).C; var h = At(b, v, WaveHeavy, 1).C; var w5 = At(b, v, 3, 1).C;
            long nn = MainWaves.Sum(w => At(b, v, w, 1).C.N);
            string fell = v == RefVer ? "—" : string.Join(" ／ ", Enumerable.Range(0, 4).Select(r => Per(MainWaves.Sum(w => At(b, v, w, 1).C.FellRole[r]), nn)));
            Console.WriteLine($"| {b} | {v} | {F1(MainAvg(b, v, 1, WinP))} ／ {F1(MainAvg(b, v, 1, SurvP))} | {F1(MainAvg(b, v, 0, WinP))} ／ {F1(MainAvg(b, v, 0, SurvP))} | {F1(MainAvg(b, v, 2, SurvP))} | {F1(SurvP(n9))} ／ {Per(n9.Turns, n9.N)} | {F1(SurvP(At(b, v, BA.MainWave, 0).C))} | {F1(SurvP(At(b, v, BA.MainWave + 1, 1).C))} | {F1(WinP(w5))} ／ {F1(SurvP(w5))} | {F1(SurvP(h))} ／ {Per(h.Turns, h.N)} | {fell} |");
        }
        Console.WriteLine();
    }

    static void TableA2()
    {
        Console.WriteLine("### 表A′ —— 全セル（勝率 ／ 全員生存 ／ 決着T ／ 落ちた/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | " + string.Join(" | ", WaveNames.Take(WaveHeavy + 1)) + " |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", WaveHeavy + 1)));
        foreach (var (b, v) in Rows())
            for (int s = 0; s < BA.Scales.Length; s++)
                Console.WriteLine($"| {b} | {v} | {BA.Scales[s].Name} | " + string.Join(" | ", Enumerable.Range(0, WaveHeavy + 1).Select(w => { var c = At(b, v, w, s).C; return $"{F1(WinP(c))} ／ {F1(SurvP(c))} ／ {Per(c.Turns, c.N)} ／ {Per(c.Fell, c.N)}"; })) + " |");
        Console.WriteLine();
    }

    static void TableB()
    {
        Console.WriteLine("## 表B —— ボルグの爆炎（1戦の回数 ／ 撃った戦 % ／ 初めて撃ったターン ／ 2回以上撃った戦 %・独りの爆炎/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Groups.Select(g => g.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", Groups.Length)));
        foreach (var (b, v) in Rows())
            Console.WriteLine($"| {b} | {v} | " + string.Join(" | ", Groups.Select(g =>
            {
                var m = Grp(b, v, g.Cells);
                return $"**{Per(m.Blazes, m.N)}** ／ {Pct(m.BlazeBattles, m.N)}% ／ T{Avg(m.FirstBlazeSum, m.BlazeBattles)} ／ {Pct(m.Blazes2Battles, m.N)}%・独り {Per(m.SoloBlazes, m.N)}";
            })) + " |");
        Console.WriteLine();
        Console.WriteLine("### 表B′ —— 初めて撃ったターンの分布（九/新兵 400/300 ／ 本編 400/300・撃った戦に占める %・T1〜T8・T9+）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | " + string.Join(" | ", Enumerable.Range(1, TM).Select(t => $"T{t}")) + " | T9+ |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("--:|", TM + 1)));
        foreach (var (b, v) in Rows())
            foreach (var g in Groups.Where(g => g.Name is "九/新兵 400/300" or "本編 400/300"))
            {
                var m = Grp(b, v, g.Cells);
                Console.WriteLine($"| {b} | {v} | {g.Name} | " + string.Join(" | ", Enumerable.Range(1, TM + 1).Select(t => Pct(m.FirstBlazeHist[t], m.BlazeBattles))) + " |");
            }
        Console.WriteLine();
    }

    static void TableC()
    {
        Console.WriteLine("## 表C —— ボルグの火勢（周回の頭の写し・生きているボルグの平均 ／ 火勢4 の割合）と育ちの内訳（実際に上がった段の和/戦）");
        Console.WriteLine();
        Console.WriteLine("「火勢4 に初めて届いた周回」は周回の頭の写しで 4 になった最初の周回（届いた戦の平均）。「火勢4 で来た育ち」は上限で捨てていた（または溜めた）育ちの回/戦（帳簿・戦全体）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | T1 | T2 | T3 | T4 | T5 | T6 | 4 に届いた周回（届いた %） | " + string.Join(" | ", GrowSrc.Select(x => x.Name)) + " | 火勢4 で来た育ち ボルグ ／ ヒヨ |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|---|" + string.Concat(Enumerable.Repeat("--:|", GrowSrc.Length)) + "---|");
        foreach (var (b, v) in Rows())
            foreach (var g in Groups)
            {
                var m = Grp(b, v, g.Cells);
                string lv = string.Join(" | ", Enumerable.Range(1, 6).Select(t => m.LvCnt[t] == 0 ? "—" : $"{(double)m.LvSum[t] / m.LvCnt[t]:F2}（{Pct(m.Lv4Cnt[t], m.LvCnt[t])}）"));
                Console.WriteLine($"| {b} | {v} | {g.Name} | {lv} | T{Avg(m.Reach4Sum, m.Reach4N)}（{Pct(m.Reach4N, m.BorgN)}%） | " + string.Join(" | ", m.Grow.Select(x => Per(x, m.N))) + $" | {Per(m.BorgMaxGrow, m.N)} ／ {Per(m.HiyoMaxGrow, m.N)} |");
            }
        Console.WriteLine();
    }

    static void TableD()
    {
        Console.WriteLine("## 表D —— ヒヨのギフト（ギフトの手番/戦 ／ 初めてのターン ／ 相手 ボルグ・ホタ・ほか %）と、火勢4 未満のボルグに渡った手番・渡す火");
        Console.WriteLine();
        Console.WriteLine("「4 未満のボルグ」は火勢4 未満でギフトの手番を始めたボルグ（括弧はそのうち大技を撃たなかった＝空振り）。渡す火は上げた相手/戦 ／ 4 に届いた/戦 ／ そのギフトの手番で大技を撃った/戦・溜めた/戦（上限で捨てた）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | ギフトの手番/戦（ある戦 %）| 初めて | 相手 ボルグ ／ ホタ ／ ほか % | 4 未満のボルグ/戦（空振り） | 渡す火 上げた ／ 4 に届いた ／ 大技 | 渡す火の溜め/戦（捨てた） |");
        Console.WriteLine("|---|---|---|---|--:|---|---|---|---|");
        foreach (var (b, v) in Rows())
            foreach (var g in Groups)
            {
                var m = Grp(b, v, g.Cells);
                if (m.HiyoN == 0) continue;
                Console.WriteLine($"| {b} | {v} | {g.Name} | {Per(m.GiftHands, m.N)}（{Pct(m.GiftBattles, m.N)}） | T{Avg(m.FirstGiftSum, m.GiftBattles)} | {string.Join(" ／ ", m.GiftTo.Select(x => Pct(x, m.GiftRecips)))} | {Per(m.GiftBorgUnder4, m.N)}（{Per(m.GiftBorgUnder4Idle, m.N)}） | {Per(m.GiftHoardEv, m.N)} ／ {Per(m.GiftHoardTo4, m.N)} ／ {Per(m.GiftHoardBig, m.N)} | {Per(m.GiftHoardAdds, m.N)}（{Per(m.GiftHoardCapped, m.N)}） |");
            }
        Console.WriteLine();
    }

    static void TableE()
    {
        Console.WriteLine("## 表E —— 味方の回復（HP/戦）と、回復が初めて入ったターン");
        Console.WriteLine();
        Console.WriteLine("「ヒヨ」は回復させた駒がヒヨの `Heal`（火の変換）から癒しの灯の分を引いたもの、「火の癒し」はボルグ・ホタ自身、「爆炎経由」は爆炎の味方への燃焼ダメージが回復になった分（ホタの火の癒し ＋ ヒヨの火の変換 ＋ ベニの反転・ヒヨ／火の癒しと重なる）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 合計/戦 | うち T1〜T3 | ヒヨ（火の変換） | 癒しの灯 | 火の癒し（ボルグ・ホタ） | ほか | 爆炎経由（重なり） | 初めて入った周回 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (b, v) in Rows())
            foreach (var g in Groups)
            {
                var m = Grp(b, v, g.Cells);
                Console.WriteLine($"| {b} | {v} | {g.Name} | {Per(m.HealAll, m.N)} | {Per(m.Heal3, m.N)} | {Per(m.HealConvert, m.N)} | {Per(m.HealGlow, m.N)} | {Per(m.HealSelfFire, m.N)} | {Per(m.HealOther, m.N)} | {Per(m.HealBlaze, m.N)} | T{Avg(m.FirstHealSum, m.HealBattles)} |");
            }
        Console.WriteLine();
    }

    static void TableF()
    {
        Console.WriteLine("## 表F —— 溜め火（1回の爆炎あたりの溜め・敵への倍率・爆炎の敵への与ダメ）／ 鎧の火・守るほど燃え上がる・癒しの灯の帳簿");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 爆炎/戦 | 溜めを使った爆炎の割合 | 溜め/回（最大） | 敵への倍率/回 | 爆炎1回の敵への与ダメ（倒した/回） | 溜めた/戦 | 鎧の火/戦（破片/戦） | 守り: 切った 鎧 ／ 盾 /戦・+1/戦（上がった）・燃えていない間/戦 | 癒しの灯/戦（癒えた/戦） |");
        Console.WriteLine("|---|---|---|--:|--:|---|--:|---|--:|---|---|---|");
        foreach (var (b, v) in Rows())
            foreach (var g in Groups)
            {
                var m = Grp(b, v, g.Cells);
                Console.WriteLine($"| {b} | {v} | {g.Name} | {Per(m.Blazes, m.N)} | {Pct(m.HoardBlazes, m.Blazes)} | {Avg(m.HoardSpent, m.HoardBlazes)}（{m.HoardMax}） | {(m.HoardBlazes == 0 ? "—" : $"×{(double)m.HoardBlazePct / m.HoardBlazes / 100:F2}")} | {Per(m.BlazeFoeDmg, m.Blazes)}（{Per(m.BlazeFoeKills, m.Blazes)}） | {Per(m.HoardAdds, m.N)} | {Per(m.ArmorFlameN, m.N)}（{Per(m.ArmorFlameAmt, m.N)}） | {Per(m.GuardSaved0, m.N)} ／ {Per(m.GuardSaved1, m.N)}・{Per(m.GuardSteps, m.N)}（{Per(m.GuardRaised, m.N)}）・{Per(m.GuardOff, m.N)} | {Per(m.MendGlowN, m.N)}（{Per(m.MendGlowHp, m.N)}） |");
            }
        Console.WriteLine();
    }

    static void TableG()
    {
        Console.WriteLine("## 表G —— ホタ（焼き尽くす/戦 ／ 画面の攻撃力の1戦の最大 平均 ／ 最大 ／ 落ちた %）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Groups.Select(g => g.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", Groups.Length)));
        foreach (var (b, v) in Rows())
            Console.WriteLine($"| {b} | {v} | " + string.Join(" | ", Groups.Select(g =>
            {
                var m = Grp(b, v, g.Cells);
                long kn = 0, ams = 0, amm = 0, hf = 0;
                foreach (var (w, s) in g.Cells) { var k = At(b, v, w, s).K; kn += k.HotaN; ams += k.AtkMaxSum; amm = Math.Max(amm, k.AtkMaxMax); hf += k.HotaFell; }
                return $"{Per(m.Burnouts, m.N)} ／ {Per(ams, kn)} ／ {amm} ／ {Pct(hf, kn)}%";
            })) + " |");
        Console.WriteLine();
    }

    static void TableH()
    {
        Console.WriteLine("## 表H —— 三角の循環（焼き尽くす → 指名 → ボルグの爆炎 → 呼び火・第247期の数え方）/戦（ある戦 %）・呼び火/戦");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Groups.Select(g => g.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", Groups.Length)));
        foreach (var (b, v) in Rows())
            Console.WriteLine($"| {b} | {v} | " + string.Join(" | ", Groups.Select(g =>
            {
                var m = Grp(b, v, g.Cells);
                return $"{Per(m.Tri, m.N)}（{Pct(m.TriBattles, m.N)}%）・呼び火 {Per(m.CallFires, m.N)}";
            })) + " |");
        Console.WriteLine();
    }

    static void TableDeath()
    {
        long cs = 0, de = 0, fs = 0, fr = 0;
        foreach (var c in _cells.Values) { cs += c.K.CauseSum; de += c.K.DeathEv; fs += c.K.FellSum; fr += c.K.FellRes; }
        Console.WriteLine($"受け入れ 3: 死因の合計 {cs} ＝ 倒れた出来事 {de}（{(cs == de ? "一致" : "**不一致**")}）／ 落ちた駒（死因の帳簿）{fs} ＝ 落ちた駒（結果）{fr}（{(fs == fr ? "一致" : "**不一致**")}）——全セル");
        Console.WriteLine();
    }
}
