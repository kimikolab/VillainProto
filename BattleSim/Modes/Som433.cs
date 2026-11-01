using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S430 = Som430Diag;

// =====================================================================================
// som433 —— 第433期【軽い期・版 2 つ】光の衣の雷の返しの歯止め②（RBb-gx ソムの全体で 1 ターン 1 回 ／ RBb-lx 左右だけ・どちらも勇者には返さない）。規定は動かさない。
// 指示書は design/PHASE433_SOM_VEIL_BRAKE2_SPEC.md ／ 報告は design/PHASE433_SOM_VEIL_BRAKE2.md。
//
//     dotnet run --project BattleSim -c Release 0 som433 boards   # §2-2: 第430〜432期の 7 台 × {規定, RBb-tx, RBb-nx, RBb-gx, RBb-lx} × ボス ／ 近衛 ／ 大隊 ／ 本編 第2・4波・seed 0..199
//                                                                 # §2-3: 組み合わせ RBb-glx を 台1 と 台6・7 の精鋭だけで
//     dotnet run --project BattleSim -c Release 0 som433 log <0|tx|nx|gx|lx|glx> <台の番号 1..7> <波 boss|guard|bat|m2|m4> [seed]
//     dotnet run --project BattleSim -c Release 0 som433 check    # 自己検査
//
// **予測のファイルが無ければ boards を走らせない**（R397・`.tmp/p433/predict.md`）。
// =====================================================================================
static class Som433Diag
{
    const string PredictFile = ".tmp/p433/predict.md";
    const int Seeds = 200;
    const string PlayRow = "版・光の衣 ソム";

    /// <summary>対照（第432期の組み合わせ・器具の中だけ）と §2-3 の組み合わせ（版の数は増やさない）。</summary>
    static readonly UnitDef SomRBbtx = UnitCatalog.SomOn432("。光の衣が砕けるとき、砕いた敵とその両隣が痺れる（同じ者の衣は 1 ターンに 1 度まで・勇者には効かない）",
        TraitId.VeilShockBreak, TraitId.VeilShockOnce, TraitId.VeilShockSpareHero);
    static readonly UnitDef SomRBbnx = UnitCatalog.SomOn432("。光の衣が砕けるとき、砕いた敵が痺れる（勇者には効かない）",
        TraitId.VeilShockBreak, TraitId.VeilShockLone, TraitId.VeilShockSpareHero);
    static readonly UnitDef SomRBbglx = UnitCatalog.SomOn432("。光の衣が砕けるとき、砕いた敵とその左右が痺れる（1 ターンに 1 度・勇者には効かない）",
        TraitId.VeilShockBreak, TraitId.VeilShockOnceAll, TraitId.VeilShockLateral, TraitId.VeilShockSpareHero);

    static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("0", "規定", UnitCatalog.Som), ("tx", "RBb-tx", SomRBbtx), ("nx", "RBb-nx", SomRBbnx),
        ("gx", "RBb-gx", UnitCatalog.SomRBbgx), ("lx", "RBb-lx", UnitCatalog.SomRBblx),
    };
    static readonly (string Key, string Label, UnitDef D)[] Combos = { ("glx", "RBb-glx", SomRBbglx) };
    static IEnumerable<(string Key, string Label, UnitDef D)> AllVers => Vers.Concat(Combos);
    /// <summary>§2-3 の組み合わせを回すセル（台の添字・波の添字）: 台1 の近衛・大隊 ／ 台6・7 の近衛・大隊（台1 のボスは x が全部止めるので 3.0 のまま・確かめに入れる）。</summary>
    static readonly (int B, int W)[] ComboCells = { (0, 0), (0, 1), (0, 2), (5, 1), (5, 2), (6, 1), (6, 2) };
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
            Console.WriteLine($"som433: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "boards": Boards(); break;
            case "log": Log(A(3, "gx"), int.Parse(A(4, "1")), A(5, "guard"), int.Parse(A(6, "0"))); break;
            case "check": Check(); break;
            default: Console.WriteLine("som433: モードは boards / log / check。"); return;
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

        Console.WriteLine($"# 第433期 —— 光の衣の雷の返しの歯止め②（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var (b, i) in boards.Select((b, i) => (b, i))) Console.WriteLine($"- {i + 1}. {b.Name}（{S307.Seats(b.F)}）");
        Console.WriteLine();

        Console.WriteLine("## 表1 勝率・倒しT（規定との差 ／ RBb-tx との差）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定 | RBb-tx | RBb-nx | RBb-gx | RBb-lx |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in waves.Select((w, i) => (w, i)))
            {
                var d = res[(bi, wi, "0")]; var tx = res[(bi, wi, "tx")];
                string C(string k) { var x = res[(bi, wi, k)]; return $"{x.WT}（{Delta(x.Win - d.Win)} ／ {Delta(x.Win - tx.Win)}）"; }
                Console.WriteLine($"| {bi + 1} | {w.Name} | {d.WT} | {tx.WT}（{Delta(tx.Win - d.Win)}） | {C("nx")} | {C("gx")} | {C("lx")} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 表2 返しと歯止めと輪（1 戦あたり・輪の比は規定に対する 弾け ／ 光 ／ 衣）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 砕けの返し | 砕けで帯電（砕いた敵 ／ 両隣） | 1 回あたり | もう帯電 | 止めた 2 度目（t ／ gx） | 走らなかった隣（n ／ lx） | 勇者で止めた | 衣の帯電が弾けた | 敵の側の弾け | 輪の比 弾け ／ 光 ／ 衣 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|---|---|--:|--:|--:|---|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in waves.Select((w, i) => (w, i)))
            {
                var d = res[(bi, wi, "0")];
                foreach (var v in AllVers)
                {
                    if (!res.TryGetValue((bi, wi, v.Key), out var x)) continue;
                    var s = x.Som; long n = x.N;
                    string ring = v.Key == "0" ? "1 ／ 1 ／ 1" : $"{Ratio(x.FoePops, d.FoePops)} ／ {Ratio(s.SparkRainLights, d.Som.SparkRainLights)} ／ {Ratio(s.SparkVeilAdded, d.Som.SparkVeilAdded)}";
                    Console.WriteLine($"| {bi + 1} | {w.Name} | {v.Label} | {Per1(s.Vs431Breaks, n)} | {Per1(s.Vs431BreakMarked, n)}（{Per1(s.Vs433BreakFoeMarked, n)} ／ {Per1(s.Vs431BreakMarked - s.Vs433BreakFoeMarked, n)}） | {(s.Vs431Breaks == 0 ? "—" : ((double)(s.Vs431BreakMarked + s.Vs431BreakAlready) / s.Vs431Breaks).ToString("F2"))} | {Per1(s.Vs431BreakAlready, n)} | {Per1(s.Vs432StopOnce, n)} ／ {Per1(s.Vs433StopOnceAll, n)} | {Per1(s.Vs432StopNeighbors, n)} ／ {Per1(s.Vs433StopSide, n)} | {Per1(s.Vs432StopHero, n)} | {Per1(s.Vs431Popped, n)} | {Per1(x.FoePops, n)} | {ring} |");
                }
            }
        Console.WriteLine();

        Console.WriteLine("## 表3 組み合わせの下見（§2-3・勝率・倒しT（規定との差・弾けの輪の比））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定 | RBb-tx | RBb-nx | RBb-gx | RBb-lx | RBb-glx |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        foreach (var (bi, wi) in ComboCells)
        {
            var d = res[(bi, wi, "0")];
            string C(string k) { var x = res[(bi, wi, k)]; return $"{x.WT}（{Delta(x.Win - d.Win)}・輪 {Ratio(x.FoePops, d.FoePops)}）"; }
            Console.WriteLine($"| {bi + 1} | {waves[wi].Name} | {d.WT} | {C("tx")} | {C("nx")} | {C("gx")} | {C("lx")} | {C("glx")} |");
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

        // §4 の線（線1 寄せ集めの精鋭 3 セルのうち 2 セル以上が +20〜+40・残りも +60 以下 ／ 線2 台1 のボス 3.0 ／ 線3 台1 の精鋭で規定 +2 以上 ／ 線4 寄せ集めの輪 5 倍以内）
        Console.WriteLine("## §4 の線");
        Console.WriteLine();
        (int B, int W)[] mix = { (5, 2), (6, 1), (6, 2) };
        foreach (var v in AllVers.Skip(1))
        {
            var ups = mix.Select(c => res[(c.B, c.W, v.Key)].Win - res[(c.B, c.W, "0")].Win).ToList();
            int inBand = ups.Count(u => u >= 20 && u <= 40);
            bool l1 = inBand >= 2 && ups.All(u => u <= 60);
            string boss = res.TryGetValue((0, 0, v.Key), out var bx) ? F1(bx.Win) : "—";
            string l3 = $"{Delta(res[(0, 1, v.Key)].Win - res[(0, 1, "0")].Win)} ／ {Delta(res[(0, 2, v.Key)].Win - res[(0, 2, "0")].Win)}";
            double ringMix = mix.Where(c => res[(c.B, c.W, "0")].FoePops > 0).Select(c => (double)res[(c.B, c.W, v.Key)].FoePops / res[(c.B, c.W, "0")].FoePops).DefaultIfEmpty(0).Max();
            Console.WriteLine($"- {v.Label}: 線1 寄せ集めの精鋭の上がり {string.Join(" ／ ", ups.Select(Delta))}（帯の中 {inBand} セル・{(l1 ? "通る" : "通らない")}）・線2 打ち返しの行のボス {boss}・線3 打ち返しの行の近衛 ／ 大隊 {l3}・線4 寄せ集めの輪の比の最大 {ringMix:F2}（{(ringMix <= 5 ? "5 倍以内" : "超える")}）");
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
        Req(UnitCatalog.SomRBbgx.Traits.SequenceEqual(som.Traits.Concat(new[] { TraitId.VeilShockBreak, TraitId.VeilShockOnceAll, TraitId.VeilShockSpareHero })), "RBb-gx ＝ 規定 ＋ VeilShockBreak ＋ VeilShockOnceAll ＋ VeilShockSpareHero");
        Req(UnitCatalog.SomRBblx.Traits.SequenceEqual(som.Traits.Concat(new[] { TraitId.VeilShockBreak, TraitId.VeilShockLateral, TraitId.VeilShockSpareHero })), "RBb-lx ＝ 規定 ＋ VeilShockBreak ＋ VeilShockLateral ＋ VeilShockSpareHero");
        var news = new[] { TraitId.VeilShockOnceAll, TraitId.VeilShockLateral };
        Req(!UnitCatalog.Everyone.Any(d => d.Traits.Any(news.Contains)), "新しい札の保持者は `All` ／ `Retired` に 0 枚");
        Req(!Presets.Playtest.Concat(CompareBuilds()).Concat(CrossBuilds()).Any(r => r.F.Occupied().Any(o => o.Def.Traits.Any(news.Contains))), "`Playtest` ／ `compare` ／ 交差帯に新しい札の駒はいない");
        Req(Presets.Versions.Count(r => r.Name == PlayRow) == 1 && !VersionsH407().Any(r => r.Name == PlayRow), $"試遊の台「{PlayRow}」は Versions に 1 つ・鎖の終端 VersionsH407 が外す");

        // 左右の表（X 字）: 前1 ↔ 前3・後1 ↔ 後3 だけ（中央と召喚枠には左右がいない）
        var lat = new List<(int, int)>();
        for (int a = 0; a < FormationRules.TotalSlots; a++)
            for (int b = 0; b < FormationRules.TotalSlots; b++)
                if (a != b && FormationRules.RowOf(a) == FormationRules.RowOf(b) && Math.Abs(a - b) == 1 && !FormationRules.IsSummonSlot(a) && !FormationRules.IsSummonSlot(b)) lat.Add((a, b));
        Req(lat.OrderBy(p => p).SequenceEqual(new[] { (0, 1), (1, 0), (3, 4), (4, 3) }), $"X 字の「左右」は 前1 ↔ 前3・後1 ↔ 後3 の 2 組だけ（{string.Join(" ", lat.Select(p => $"{p.Item1}-{p.Item2}"))}）");

        var boards = S430.Boards();
        var play = Presets.Versions.First(r => r.Name == PlayRow).F;
        var want = S430.Swap(boards[0].F, PlayDef);
        Req(Enumerable.Range(0, 5).All(i => ReferenceEquals(play[i], want[i])), $"「{PlayRow}」＝ 台 1（打ち返しの行）のソムを {PlayLabel} に替えた並び");
        bool same = true, defNone = true, stops = true, sane = true, oneTurn = true;
        long heroStops = 0, heroBreaks = 0;
        foreach (var b in boards)
            foreach (var (w, wi) in S430.Waves.Select((w, i) => (w, i)))
                for (int s = 0; s < 10; s++)
                    foreach (var v in AllVers)
                    {
                        var f = S430.Swap(b.F, v.D);
                        var (r0, _, _) = I.Raw(f, w, s);
                        var (r1, _, _) = I.Raw(f, w, s, verbose: true);
                        if (r0.PlayerWon != r1.PlayerWon || r0.Turns != r1.Turns) same = false;
                        var t = r0.TallyByUnit.GetValueOrDefault("som") ?? new UnitTally();
                        if (v.Key == "0" && t.Vs431Breaks + t.Vs433StopOnceAll + t.Vs433StopSide + t.Vs433BreakFoeMarked != 0) defNone = false;
                        bool hasG = v.D.Traits.Contains(TraitId.VeilShockOnceAll), hasL = v.D.Traits.Contains(TraitId.VeilShockLateral);
                        if ((!hasG && t.Vs433StopOnceAll != 0) || (!hasL && t.Vs433StopSide != 0)) stops = false;
                        if (hasG && t.Vs431Breaks > r0.Turns) oneTurn = false;   // 全体で 1 ターン 1 回 ＝ 返しの回数 ≦ ターン数
                        if (hasL && t.Vs431BreakMarked + t.Vs431BreakAlready > 2 * t.Vs431Breaks) sane = false;   // 左右だけ ＝ 1 回の返しで 2 体まで（X 字では砕いた敵 ＋ 1）
                        if (t.Vs433BreakFoeMarked > t.Vs431BreakMarked || t.Vs431Popped > t.Vs431BreakMarked || t.Vs431Hits + t.Vd431Stored != 0) sane = false;
                        if (wi == 0 && b == boards[0] && v.Key != "0") { heroStops += t.Vs432StopHero; heroBreaks += t.Vs431Breaks; }
                    }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（台 7 × 5 波 × 6 版 × seed 0..9）");
        Req(defNone, "規定のソムは返さない ／ 歯止めの計数も 0");
        Req(stops, "歯止めの計数はその札を持つ版にだけ立つ");
        Req(oneTurn, "RBb-gx ／ glx の砕けの返しは 1 戦のターン数以下");
        Req(sane, "削っただけでは返さない・RBb-lx は 1 回の返しで 2 体まで・砕いた敵の帯電 ≦ 砕けの帯電・弾けた数 ≦ 帯電させた数");
        Req(heroStops > 0 && heroBreaks == 0, $"台 1 × ボスで x を含む版は砕けを全部止める（止めた {heroStops}・返した {heroBreaks}・seed 0..9）");
        Console.WriteLine(ok ? "som433 check: すべて ○" : "som433 check: × あり");
        if (!ok) Environment.ExitCode = 1;
    }

    /// <summary>試遊の台「版・光の衣 ソム」に載せた版（§2-5）。打ち返しの行でボス 3.0・精鋭 +3.5 ／ +2.0 が残り、寄せ集めの耐久 大隊が帯に入るのは RBb-gx。</summary>
    static UnitDef PlayDef => UnitCatalog.SomRBbgx;
    const string PlayLabel = "RBb-gx";
}
