using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S430 = Som430Diag;

// =====================================================================================
// som431 —— 第431期【軽い期・版 3 つ】ソムの光の衣に雷の返し（RB-1 一撃一感電 ／ RB-b 砕けたときだけ ／ RB-c 返し＋放電）。規定は動かさない。
// 指示書は design/PHASE431_SOM_VEIL_SHOCK_SPEC.md ／ 報告は design/PHASE431_SOM_VEIL_SHOCK.md。
//
//     dotnet run --project BattleSim -c Release 0 som431 boards   # §2: 第430期の 7 台 × {規定, RB-1, RB-b, RB-c} × ボス ／ 近衛 ／ 大隊 ／ 本編 第2・4波・seed 0..199
//     dotnet run --project BattleSim -c Release 0 som431 log <0|1|b|c> <台の番号 1..7> <波 boss|guard|bat|m2|m4> [seed]
//     dotnet run --project BattleSim -c Release 0 som431 check    # 自己検査
//
// **予測のファイルが無ければ boards を走らせない**（R397・`.tmp/p431/predict.md`）。
// =====================================================================================
static class Som431Diag
{
    const string PredictFile = ".tmp/p431/predict.md";
    const int Seeds = 200;
    static readonly (string Key, string Label, UnitDef D)[] Vers =
        { ("0", "規定", UnitCatalog.Som), ("1", "RB-1", UnitCatalog.SomRB1), ("b", "RB-b", UnitCatalog.SomRBb), ("c", "RB-c", UnitCatalog.SomRBc) };
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
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
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
            Console.WriteLine($"som431: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "boards": Boards(); break;
            case "log": Log(A(3, "1"), int.Parse(A(4, "1")), A(5, "guard"), int.Parse(A(6, "0"))); break;
            case "check": Check(); break;
            default: Console.WriteLine("som431: モードは boards / log / check。"); return;
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

        Console.WriteLine($"# 第431期 —— ソムの光の衣に雷の返し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var (b, i) in boards.Select((b, i) => (b, i))) Console.WriteLine($"- {i + 1}. {b.Name}（{S307.Seats(b.F)}）");
        Console.WriteLine();

        Console.WriteLine("## 表1 勝率・倒しT（規定との差）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定 | RB-1 | RB-b | RB-c |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in waves.Select((w, i) => (w, i)))
            {
                var d = res[(bi, wi, "0")];
                string C(string k) { var x = res[(bi, wi, k)]; return $"{x.WT}（{Delta(x.Win - d.Win)}）"; }
                Console.WriteLine($"| {bi + 1} | {w.Name} | {d.WT} | {C("1")} | {C("b")} | {C("c")} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 表2 返しと輪（1 戦あたり・輪の比は規定に対する 弾け ／ 光 ／ 衣）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 削った一撃 | 帯電させた | もう帯電 | 砕けの返し | 砕けで帯電 | 衣の帯電が弾けた（うち獣が起こし手） | 敵の側の弾け | 光 | 衣 | 輪の比 弾け ／ 光 ／ 衣 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in waves.Select((w, i) => (w, i)))
            {
                var d = res[(bi, wi, "0")];
                foreach (var v in Vers)
                {
                    var x = res[(bi, wi, v.Key)]; var s = x.Som; long n = x.N;
                    string ring = v.Key == "0" ? "1 ／ 1 ／ 1" : $"{Ratio(x.FoePops, d.FoePops)} ／ {Ratio(s.SparkRainLights, d.Som.SparkRainLights)} ／ {Ratio(s.SparkVeilAdded, d.Som.SparkVeilAdded)}";
                    Console.WriteLine($"| {bi + 1} | {w.Name} | {v.Label} | {Per1(s.Vs431Hits, n)} | {Per1(s.Vs431Marked, n)} | {Per1(s.Vs431Already, n)} | {Per1(s.Vs431Breaks, n)} | {Per1(s.Vs431BreakMarked, n)} | {Per1(s.Vs431Popped, n)}（{Per1(s.Vs431PoppedBeast, n)}） | {Per1(x.FoePops, n)} | {Per1(s.SparkRainLights, n)} | {Per1(s.SparkVeilAdded, n)} | {ring} |");
                }
            }
        Console.WriteLine();

        Console.WriteLine("## 表3 RB-c の放電（1 戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 溜めた光 | 放電の回数 | 撒いた名目 | 減った HP＋破片 | 上限 20 で切れた量 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in waves.Select((w, i) => (w, i)))
            {
                var x = res[(bi, wi, "c")]; var s = x.Som; long n = x.N;
                Console.WriteLine($"| {bi + 1} | {w.Name} | {Per1(s.Vd431Stored, n)} | {Per1(s.Vd431Shots, n)} | {Per1(s.Vd431Amt, n)} | {Per1(s.Vd431Dealt, n)} | {Per1(s.Vd431Cut, n)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 表4 倒れた味方（規定 ／ RB-1 ／ RB-b ／ RB-c）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定 | RB-1 | RB-b | RB-c |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in waves.Select((w, i) => (w, i)))
                Console.WriteLine($"| {bi + 1} | {w.Name} | {res[(bi, wi, "0")].Fall} | {res[(bi, wi, "1")].Fall} | {res[(bi, wi, "b")].Fall} | {res[(bi, wi, "c")].Fall} |");
        Console.WriteLine();

        // §4 の線
        Console.WriteLine("## §4 の線");
        Console.WriteLine();
        foreach (var v in Vers.Skip(1))
        {
            int up = 0, down = 0, mixUp = 0; double ringMax = 0;
            for (int bi = 1; bi <= 4; bi++) for (int wi = 1; wi <= 2; wi++)
            {
                double dd = res[(bi, wi, v.Key)].Win - res[(bi, wi, "0")].Win;
                if (dd >= 5) up++; if (dd <= -10) down++;
            }
            for (int bi = 5; bi <= 6; bi++) for (int wi = 1; wi <= 2; wi++)
                if (res[(bi, wi, v.Key)].Win - res[(bi, wi, "0")].Win >= 5) mixUp++;
            for (int wi = 1; wi <= 2; wi++)
            {
                var x = res[(0, wi, v.Key)]; var d = res[(0, wi, "0")];
                foreach (var (a, c) in new[] { (x.FoePops, d.FoePops), (x.Som.SparkRainLights, d.Som.SparkRainLights), (x.Som.SparkVeilAdded, d.Som.SparkVeilAdded) })
                    if (c > 0) ringMax = Math.Max(ringMax, (double)a / c);
            }
            double boss = res[(0, 0, v.Key)].Win;
            Console.WriteLine($"- {v.Label}: 線1 感電の台の精鋭で +5 以上 {up} セル ／ −10 以下 {down} セル（{(up >= 2 && down == 0 ? "通る" : "通らない")}）・線2 打ち返しの行の精鋭の輪の比の最大 {ringMax:F2}（{(ringMax <= 3.0 ? "閉じていない" : "歯止め不足")}）・線3 寄せ集めの精鋭で +5 以上 {mixUp} セル・線4 打ち返しの行のボス {F1(boss)}（{(boss > 50 ? "強すぎ" : "50 以下")}）");
        }
        Console.WriteLine();
    }

    static I.W WaveOf(string k) => S430.Waves[Array.IndexOf(WaveKeys, k)];

    static void Log(string vk, int board, string wk, int seed)
    {
        var v = Vers.First(x => x.Key == vk);
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
        Req(UnitCatalog.SomRB1.Traits.SequenceEqual(som.Traits.Append(TraitId.VeilShock)), "RB-1 ＝ 規定 ＋ VeilShock");
        Req(UnitCatalog.SomRBb.Traits.SequenceEqual(som.Traits.Append(TraitId.VeilShockBreak)), "RB-b ＝ 規定 ＋ VeilShockBreak");
        Req(UnitCatalog.SomRBc.Traits.SequenceEqual(som.Traits.Append(TraitId.VeilShock).Append(TraitId.VeilDischarge)), "RB-c ＝ 規定 ＋ VeilShock ＋ VeilDischarge");
        var news = new[] { TraitId.VeilShock, TraitId.VeilShockBreak, TraitId.VeilDischarge };
        Req(!UnitCatalog.Everyone.Any(d => d.Traits.Any(news.Contains)), "新しい札の保持者は `All` ／ `Retired` に 0 枚");
        Req(!Presets.Playtest.Concat(CompareBuilds()).Concat(CrossBuilds()).Any(r => r.F.Occupied().Any(o => o.Def.Traits.Any(news.Contains))), "`Playtest` ／ `compare` ／ 交差帯に新しい札の駒はいない");
        Req(Presets.Versions.Count(r => r.Name == "版・光の衣 ソム") == 1 && !VersionsH407().Any(r => r.Name == "版・光の衣 ソム"), "試遊の台「版・光の衣 ソム」は Versions に 1 つ・鎖の終端 VersionsH407 が外す");

        var boards = S430.Boards();
        var play = Presets.Versions.First(r => r.Name == "版・光の衣 ソム").F;
        var want = S430.Swap(boards[0].F, UnitCatalog.SomRBbx);   // 第432期に RB-b → RBb-x へ差し替え（design/PHASE432_SOM_VEIL_BRAKE.md）
        Req(Enumerable.Range(0, 5).All(i => ReferenceEquals(play[i], want[i])), "「版・光の衣 ソム」＝ 台 1（打ち返しの行）のソムを版に替えた並び（第431期 RB-b ／ 第432期から RBb-x）");
        bool same = true, rbbNoHit = true, defNone = true, ringSane = true;
        foreach (var b in boards)
            foreach (var w in S430.Waves)
                for (int s = 0; s < 10; s++)
                {
                    foreach (var v in Vers)
                    {
                        var f = S430.Swap(b.F, v.D);
                        var (r0, _, _) = I.Raw(f, w, s);
                        var (r1, _, _) = I.Raw(f, w, s, verbose: true);
                        if (r0.PlayerWon != r1.PlayerWon || r0.Turns != r1.Turns) same = false;
                        var t = r0.TallyByUnit.GetValueOrDefault("som") ?? new UnitTally();
                        if (v.Key == "0" && t.Vs431Hits + t.Vs431Breaks + t.Vd431Stored != 0) defNone = false;
                        if (v.Key == "b" && (t.Vs431Hits != 0 || t.Vs431Marked != 0)) rbbNoHit = false;
                        if (v.Key != "c" && t.Vd431Stored + t.Vd431Shots != 0) rbbNoHit = false;
                        if (t.Vs431Popped > t.Vs431Marked + t.Vs431BreakMarked || t.Vd431Amt > t.Vd431Shots * DischargeTargetsMax * BattleContext.VeilDischargeCap) ringSane = false;
                    }
                }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（台 7 × 5 波 × 4 版 × seed 0..9）");
        Req(defNone, "規定のソムは返さない ／ 溜めない");
        Req(rbbNoHit, "RB-b は削っただけでは返さない・RB-c 以外は溜めない");
        Req(ringSane, "弾けた数 ≦ 帯電させた数・撒いた名目 ≦ 放電 × 5 体 × 20");
        Console.WriteLine(ok ? "som431 check: すべて ○" : "som431 check: × あり");
        if (!ok) Environment.ExitCode = 1;
    }
    /// <summary>放電が届く最大の数（砕いた敵 ＋ 両隣。隣接の表は最大 4 だが、ここは上限の検査なので 5 で見る）。</summary>
    const int DischargeTargetsMax = 5;
}
