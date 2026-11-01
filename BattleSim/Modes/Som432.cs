using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S430 = Som430Diag;

// =====================================================================================
// som432 —— 第432期【軽い期・版 3 つ】光の衣の雷の返しに歯止め（RBb-t 1 ターン 1 回 ／ RBb-n 砕いた敵だけ ／ RBb-x 勇者には返さない・土台は RB-b）。規定は動かさない。
// 指示書は design/PHASE432_SOM_VEIL_BRAKE_SPEC.md ／ 報告は design/PHASE432_SOM_VEIL_BRAKE.md。
//
//     dotnet run --project BattleSim -c Release 0 som432 boards   # §2-2: 第430〜431期の 7 台 × {規定, RB-b, RBb-t, RBb-n, RBb-x} × ボス ／ 近衛 ／ 大隊 ／ 本編 第2・4波・seed 0..199
//                                                                 # §2-3: 組み合わせ RBb-tx ／ RBb-nx を 台1 のボスと 台6・7 の精鋭だけで
//     dotnet run --project BattleSim -c Release 0 som432 log <0|b|t|n|x|tx|nx> <台の番号 1..7> <波 boss|guard|bat|m2|m4> [seed]
//     dotnet run --project BattleSim -c Release 0 som432 check    # 自己検査
//
// **予測のファイルが無ければ boards を走らせない**（R397・`.tmp/p432/predict.md`）。
// =====================================================================================
static class Som432Diag
{
    const string PredictFile = ".tmp/p432/predict.md";
    const int Seeds = 200;
    const string PlayRow = "版・光の衣 ソム";

    /// <summary>§2-3 の組み合わせ（版の数は増やさない・器具の中でだけ作る）。</summary>
    static readonly UnitDef SomRBbtx = UnitCatalog.SomOn432("。光の衣が砕けるとき、砕いた敵とその両隣が痺れる（同じ者の衣は 1 ターンに 1 度まで・勇者には効かない）",
        TraitId.VeilShockBreak, TraitId.VeilShockOnce, TraitId.VeilShockSpareHero);
    static readonly UnitDef SomRBbnx = UnitCatalog.SomOn432("。光の衣が砕けるとき、砕いた敵が痺れる（勇者には効かない）",
        TraitId.VeilShockBreak, TraitId.VeilShockLone, TraitId.VeilShockSpareHero);

    static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("0", "規定", UnitCatalog.Som), ("b", "RB-b", UnitCatalog.SomRBb),
        ("t", "RBb-t", UnitCatalog.SomRBbt), ("n", "RBb-n", UnitCatalog.SomRBbn), ("x", "RBb-x", UnitCatalog.SomRBbx),
    };
    static readonly (string Key, string Label, UnitDef D)[] Combos = { ("tx", "RBb-tx", SomRBbtx), ("nx", "RBb-nx", SomRBbnx) };
    static IEnumerable<(string Key, string Label, UnitDef D)> AllVers => Vers.Concat(Combos);
    /// <summary>§2-3 の組み合わせを回すセル（台の添字・波の添字）: 台1 のボス ／ 台6・7 の近衛・大隊。</summary>
    static readonly (int B, int W)[] ComboCells = { (0, 0), (5, 1), (5, 2), (6, 1), (6, 2) };
    static readonly string[] WaveKeys = { "boss", "guard", "bat", "m2", "m4" };

    static string Short(UnitDef d) => S307.Short(d);
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F0") + "%";
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");
    static string Ratio(long a, long b) => b == 0 ? (a == 0 ? "—" : "∞") : ((double)a / b).ToString("F2");

    sealed class Fx
    {
        public long N, Wins, WinT, FoePops;
        public UnitTally Som = new();
        public readonly Dictionary<string, long> Death = new(), DeathT = new();
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; FoePops += o.FoePops; Som.Add(o.Som);
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
        public string WT => Wins == 0 ? F1(Win) : $"{F1(Win)}・{(double)WinT / Wins:0.0}T";
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(5)
            .Select(kv => $"{kv.Key} {Pc(kv.Value, N)}・T{Per1(DeathT[kv.Key], kv.Value)}"));
    }

    static Fx One(Formation f, I.W w, int seed)
    {
        var (r, p, _) = I.Raw(f, w, seed);
        var a = new Fx { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { string k = Short(u.Def); a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + u.LastDeathTurn; }
        if (r.TallyByUnit.TryGetValue("som", out var s)) a.Som.Add(s);
        var ally = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit) if (!ally.Contains(id)) a.FoePops += t.ShockSpent;   // 敵の側で弾けた感電（獣・糸玉を含む）
        return a;
    }

    static Fx Measure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, s));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode == "boards" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"som432: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "boards": Boards(); break;
            case "log": Log(A(3, "n"), int.Parse(A(4, "1")), A(5, "guard"), int.Parse(A(6, "0"))); break;
            case "check": Check(); break;
            default: Console.WriteLine("som432: モードは boards / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Boards()
    {
        var boards = S430.Boards();
        var waves = S430.Waves;
        var res = new Dictionary<(int B, int W, string V), Fx>();
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in waves.Select((w, i) => (w, i)))
                foreach (var v in Vers)
                    res[(bi, wi, v.Key)] = Measure(S430.Swap(b.F, v.D), w);
        foreach (var (bi, wi) in ComboCells)
            foreach (var v in Combos)
                res[(bi, wi, v.Key)] = Measure(S430.Swap(boards[bi].F, v.D), waves[wi]);

        Console.WriteLine($"# 第432期 —— 光の衣の雷の返しに歯止め（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var (b, i) in boards.Select((b, i) => (b, i))) Console.WriteLine($"- {i + 1}. {b.Name}（{S307.Seats(b.F)}）");
        Console.WriteLine();

        Console.WriteLine("## 表1 勝率・倒しT（規定との差 ／ RB-b との差）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定 | RB-b | RBb-t | RBb-n | RBb-x |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in waves.Select((w, i) => (w, i)))
            {
                var d = res[(bi, wi, "0")]; var rb = res[(bi, wi, "b")];
                string C(string k) { var x = res[(bi, wi, k)]; return $"{x.WT}（{Delta(x.Win - d.Win)} ／ {Delta(x.Win - rb.Win)}）"; }
                Console.WriteLine($"| {bi + 1} | {w.Name} | {d.WT} | {rb.WT}（{Delta(rb.Win - d.Win)}） | {C("t")} | {C("n")} | {C("x")} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 表2 返しと歯止めと輪（1 戦あたり・輪の比は規定に対する 弾け ／ 光 ／ 衣）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 砕けの返し | 砕けで帯電 | もう帯電 | 止めた 2 度目（t） | 走らなかった両隣（n） | 勇者で止めた（x） | 衣の帯電が弾けた | 敵の側の弾け | 光 | 衣 | 輪の比 弾け ／ 光 ／ 衣 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in waves.Select((w, i) => (w, i)))
            {
                var d = res[(bi, wi, "0")];
                foreach (var v in AllVers)
                {
                    if (!res.TryGetValue((bi, wi, v.Key), out var x)) continue;
                    var s = x.Som; long n = x.N;
                    string ring = v.Key == "0" ? "1 ／ 1 ／ 1" : $"{Ratio(x.FoePops, d.FoePops)} ／ {Ratio(s.SparkRainLights, d.Som.SparkRainLights)} ／ {Ratio(s.SparkVeilAdded, d.Som.SparkVeilAdded)}";
                    Console.WriteLine($"| {bi + 1} | {w.Name} | {v.Label} | {Per1(s.Vs431Breaks, n)} | {Per1(s.Vs431BreakMarked, n)} | {Per1(s.Vs431BreakAlready, n)} | {Per1(s.Vs432StopOnce, n)} | {Per1(s.Vs432StopNeighbors, n)} | {Per1(s.Vs432StopHero, n)} | {Per1(s.Vs431Popped, n)} | {Per1(x.FoePops, n)} | {Per1(s.SparkRainLights, n)} | {Per1(s.SparkVeilAdded, n)} | {ring} |");
                }
            }
        Console.WriteLine();

        Console.WriteLine("## 表3 組み合わせの下見（§2-3・勝率・倒しT（規定との差）・輪の比は弾け）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定 | RB-b | RBb-t | RBb-n | RBb-x | RBb-tx | RBb-nx |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var (bi, wi) in ComboCells)
        {
            var d = res[(bi, wi, "0")];
            string C(string k) { var x = res[(bi, wi, k)]; return $"{x.WT}（{Delta(x.Win - d.Win)}・輪 {Ratio(x.FoePops, d.FoePops)}）"; }
            Console.WriteLine($"| {bi + 1} | {waves[wi].Name} | {d.WT} | {C("b")} | {C("t")} | {C("n")} | {C("x")} | {C("tx")} | {C("nx")} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表4 倒れた味方（割合・平均の倒れたT・上位 5）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 倒れた味方 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in waves.Select((w, i) => (w, i)))
                foreach (var v in AllVers)
                    if (res.TryGetValue((bi, wi, v.Key), out var x)) Console.WriteLine($"| {bi + 1} | {w.Name} | {v.Label} | {x.Fall} |");
        Console.WriteLine();

        // §4 の線（線1 寄せ集めの精鋭 台6 大隊・台7 近衛 ／ 大隊 の上がり ／ 線2 台1 のボス ／ 線3 台1 の精鋭で規定 +2 以上 ／ 線4 輪の比）
        Console.WriteLine("## §4 の線");
        Console.WriteLine();
        (int B, int W)[] mix = { (5, 2), (6, 1), (6, 2) };
        foreach (var v in AllVers.Skip(1))
        {
            var ups = mix.Where(c => res.ContainsKey((c.B, c.W, v.Key))).Select(c => res[(c.B, c.W, v.Key)].Win - res[(c.B, c.W, "0")].Win).ToList();
            string l1 = ups.Count == 0 ? "—" : string.Join(" ／ ", ups.Select(Delta)) + $"（{(ups.All(u => u >= 20 && u <= 40) ? "収まる" : ups.Any(u => u > 60) ? "足りない（+60 超あり）" : ups.Any(u => u < 5) ? "強すぎ（+5 未満あり）" : "一部外れ")}）";
            double boss = res[(0, 0, v.Key)].Win;
            string l3 = res.ContainsKey((0, 1, v.Key))
                ? $"{Delta(res[(0, 1, v.Key)].Win - res[(0, 1, "0")].Win)} ／ {Delta(res[(0, 2, v.Key)].Win - res[(0, 2, "0")].Win)}"
                : "—";
            double ringEl = 0, ringMix = 0;
            foreach (int wi in new[] { 1, 2 })
                if (res.TryGetValue((0, wi, v.Key), out var x) && res[(0, wi, "0")].FoePops > 0) ringEl = Math.Max(ringEl, (double)x.FoePops / res[(0, wi, "0")].FoePops);
            foreach (var c in mix)
                if (res.TryGetValue((c.B, c.W, v.Key), out var x) && res[(c.B, c.W, "0")].FoePops > 0) ringMix = Math.Max(ringMix, (double)x.FoePops / res[(c.B, c.W, "0")].FoePops);
            Console.WriteLine($"- {v.Label}: 線1 寄せ集めの精鋭の上がり {l1}・線2 打ち返しの行のボス {F1(boss)}（{(boss <= 30 ? "30 以下" : "超える")}）・線3 打ち返しの行の近衛 ／ 大隊 {l3}・線4 輪の比の最大 打ち返し {(ringEl == 0 ? "—" : ringEl.ToString("F2"))} ／ 寄せ集め {ringMix:F2}");
        }
        Console.WriteLine();
    }

    static I.W WaveOf(string k) => S430.Waves[Array.IndexOf(WaveKeys, k)];

    static void Log(string vk, int board, string wk, int seed)
    {
        var v = AllVers.First(x => x.Key == vk);
        var b = S430.Boards()[board - 1];
        var (r, _, _) = I.Raw(S430.Swap(b.F, v.D), WaveOf(wk), seed, verbose: true);
        Console.WriteLine($"# {b.Name} × {WaveOf(wk).Name} × {v.Label}・seed {seed}: PlayerWon={r.PlayerWon}・T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    static void Check()
    {
        bool ok = true;
        void Req(bool c, string what) { Console.WriteLine($"{(c ? "○" : "×")} {what}"); ok &= c; }
        var som = UnitCatalog.Som;
        Req(UnitCatalog.SomRBbt.Traits.SequenceEqual(som.Traits.Append(TraitId.VeilShockBreak).Append(TraitId.VeilShockOnce)), "RBb-t ＝ 規定 ＋ VeilShockBreak ＋ VeilShockOnce");
        Req(UnitCatalog.SomRBbn.Traits.SequenceEqual(som.Traits.Append(TraitId.VeilShockBreak).Append(TraitId.VeilShockLone)), "RBb-n ＝ 規定 ＋ VeilShockBreak ＋ VeilShockLone");
        Req(UnitCatalog.SomRBbx.Traits.SequenceEqual(som.Traits.Append(TraitId.VeilShockBreak).Append(TraitId.VeilShockSpareHero)), "RBb-x ＝ 規定 ＋ VeilShockBreak ＋ VeilShockSpareHero");
        var news = new[] { TraitId.VeilShockOnce, TraitId.VeilShockLone, TraitId.VeilShockSpareHero };
        Req(!UnitCatalog.Everyone.Any(d => d.Traits.Any(news.Contains)), "新しい札の保持者は `All` ／ `Retired` に 0 枚");
        Req(!Presets.Playtest.Concat(CompareBuilds()).Concat(CrossBuilds()).Any(r => r.F.Occupied().Any(o => o.Def.Traits.Any(news.Contains))), "`Playtest` ／ `compare` ／ 交差帯に新しい札の駒はいない");
        Req(Presets.Versions.Count(r => r.Name == PlayRow) == 1 && !VersionsH407().Any(r => r.Name == PlayRow), $"試遊の台「{PlayRow}」は Versions に 1 つ・鎖の終端 VersionsH407 が外す");

        var boards = S430.Boards();
        var play = Presets.Versions.First(r => r.Name == PlayRow).F;
        // 第433期: 台の版は期ごとに差し替える（第432期 RBb-x → 第433期 RBb-gx）ので、「台 1 の中央のソムを何かの版に替えた並び」で見る
        Req(Enumerable.Range(0, 5).All(i => i == 2 ? play[i]?.Id == "som" && ReferenceEquals(boards[0].F[i], UnitCatalog.Som) : ReferenceEquals(play[i], boards[0].F[i])), $"「{PlayRow}」＝ 台 1（打ち返しの行）の中央のソムを版に替えた並び（第432期は {PlayLabel}）");
        bool same = true, defNone = true, stops = true, rbbSame = true, sane = true;
        foreach (var b in boards)
            foreach (var w in S430.Waves)
                for (int s = 0; s < 10; s++)
                {
                    var tal = new Dictionary<string, UnitTally>();
                    var outc = new Dictionary<string, (bool, int)>();
                    foreach (var v in AllVers)
                    {
                        var f = S430.Swap(b.F, v.D);
                        var (r0, _, _) = I.Raw(f, w, s);
                        var (r1, _, _) = I.Raw(f, w, s, verbose: true);
                        if (r0.PlayerWon != r1.PlayerWon || r0.Turns != r1.Turns) same = false;
                        var t = r0.TallyByUnit.GetValueOrDefault("som") ?? new UnitTally();
                        tal[v.Key] = t; outc[v.Key] = (r0.PlayerWon, r0.Turns);
                        if (v.Key == "0" && t.Vs431Breaks + t.Vs432StopOnce + t.Vs432StopNeighbors + t.Vs432StopHero != 0) defNone = false;
                        if (t.Vs431Hits + t.Vd431Stored != 0) sane = false;   // RB-b の上なので削っただけでは返さない・溜めない
                        bool hasT = v.D.Traits.Contains(TraitId.VeilShockOnce), hasN = v.D.Traits.Contains(TraitId.VeilShockLone), hasX = v.D.Traits.Contains(TraitId.VeilShockSpareHero);
                        if ((!hasT && t.Vs432StopOnce != 0) || (!hasN && t.Vs432StopNeighbors != 0) || (!hasX && t.Vs432StopHero != 0)) stops = false;
                        if (hasN && t.Vs431BreakMarked + t.Vs431BreakAlready > t.Vs431Breaks) sane = false;   // 砕いた敵だけ ＝ 1 回の返しで 1 体まで
                        if (t.Vs431Popped > t.Vs431BreakMarked) sane = false;
                    }
                    // 歯止めが 1 度も効かなかった戦は RB-b と同じ戦になる（歯止めの枝が盤面を他に変えていない）
                    foreach (var k in new[] { "t", "n", "x", "tx", "nx" })
                    {
                        var t = tal[k];
                        if (t.Vs432StopOnce + t.Vs432StopNeighbors + t.Vs432StopHero == 0 && outc[k] != outc["b"]) rbbSame = false;
                    }
                }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（台 7 × 5 波 × 7 版 × seed 0..9）");
        Req(defNone, "規定のソムは返さない ／ 歯止めの計数も 0");
        Req(stops, "歯止めの計数はその札を持つ版にだけ立つ");
        Req(sane, "削っただけでは返さない・RBb-n は 1 回の返しで 1 体まで・弾けた数 ≦ 帯電させた数");
        Req(rbbSame, "歯止めが 1 度も効かなかった戦は RB-b と同じ勝敗 ／ T");
        long xBreaks = 0, xStops = 0;
        for (int s = 0; s < 10; s++)
        {
            var (r, _, _) = I.Raw(S430.Swap(boards[0].F, UnitCatalog.SomRBbx), S430.Waves[0], s);
            var t = r.TallyByUnit.GetValueOrDefault("som") ?? new UnitTally();
            xBreaks += t.Vs431Breaks; xStops += t.Vs432StopHero;
        }
        Req(xStops > 0 && xBreaks == 0, $"台 1 × ボス（勇者 1 体）で RBb-x は砕けを全部止める（止めた {xStops}・返した {xBreaks}・seed 0..9）");
        Console.WriteLine(ok ? "som432 check: すべて ○" : "som432 check: × あり");
        if (!ok) Environment.ExitCode = 1;
    }

    /// <summary>試遊の台「版・光の衣 ソム」に載せた版（§2-5）。打ち返しの行でボスが規定に戻り、精鋭の +4 ／ +3 が残るのは RBb-x（組み合わせ RBb-tx は版に足さない）。</summary>
    static UnitDef PlayDef => UnitCatalog.SomRBbx;
    const string PlayLabel = "RBb-x";
}
