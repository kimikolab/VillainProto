using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using X = Sh365Diag;

// =====================================================================================
// sh366 —— 第366期【軽い期】本体1: ヒサの玉を持ち越さない（Y3 の版 P-t ／ P-60 ／ P-180）／ 本体2: 毒軸の棚卸し（数えるだけ）。
// 指示書は design/PHASE366_HISA_POOL_POISON_P0_SPEC.md ／ 報告は design/PHASE366_HISA_POOL_POISON.md。規定は1つも変えない。
//
//     dotnet run --project BattleSim -c Release 0 sh366 boards             # §2-2: 代表台 × 波 × 版（seed 0..99）・台 B × 勇者の輪の数え上げ・壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh366 compare            # §2-2 の 4: `compare` 64 行 × 版（seed 0..199・基準は規定）
//     dotnet run --project BattleSim -c Release 0 sh366 flow               # §3-2: 毒の流れ（毒の行 × 本編 第5波 ／ 近衛 ／ 大隊 ／ ボス・seed 0..19・1戦あたり）
//     dotnet run --project BattleSim -c Release 0 sh366 swap               # §3-3: 毒の駒を素体に替えた差（毒の行 × 4 波・seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh366 check              # 自己検査
//
// 版の札は挙動を持たない印（`CommandPoolTurnDrop` ／ `CommandPoolCap60` ／ `CommandPoolCap180`）で、本体は engine（`CommandPoolTurnEnd` ／ `CommandTrait.PoolCapOf`）。
// 輪の数え上げは第365期の計測のスイッチ（`BattleContext.ChainCount365`・`Sh365Diag.Raw`）をそのまま使う。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p366/predict.md`）。
// =====================================================================================
static partial class Sh366Diag
{
    internal const string PredictFile = ".tmp/p366/predict.md";
    const string Tag = "第366期";
    const int Seeds = 100, CompareSeeds = 200, TT = 3;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "boards" or "compare" or "flow" or "swap" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh366: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "boards": Boards(); return;
            case "compare": CompareAll(); return;
            case "flow": Flow(); return;
            case "swap": SwapAll(); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh366: モードは boards / compare / flow / swap / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Balls(long pool, long n) => n == 0 ? "—" : F1((double)pool / CommandTrait.Every / n);   // 溢れ → 玉（1戦あたり）
    static string Sg(double x) => x.ToString("+0.0;-0.0;±0.0");

    // ---------------------------------------------------------------------------------
    // 版と台
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, UnitDef Def);
    internal static readonly Ver[] Vers =
    {
        new("規定", UnitCatalog.Hisa),
        new("P-t ターンで捨てる", UnitCatalog.HisaPt),
        new("P-60 溜まり 60 まで", UnitCatalog.HisaP60),
        new("P-180 溜まり 180 まで", UnitCatalog.HisaP180),
    };
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(f, "hisa", v.Def);

    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;
    static Formation Play(string n) => Presets.Playtest.Concat(Presets.Versions).First(r => r.Name == n).F;

    /// <summary>§2-2 の代表台（駒はいまの規定）。決めたこと: 叫びの癒しで味方を保つ台として「試遊・標 三人組」と `compare` の「反撃 (ヒサ×カド)」を足した。</summary>
    internal static (string Name, Formation F)[] BoardList() => new (string, Formation)[]
    {
        ("台 B", Presets.HisaBoard366()),
        ("標の守り型", Play("試遊・標 守り型")),
        ("標経済 (ヒサ×ザン×ミサ)", Row("標経済 (ヒサ×ザン×ミサ)")),
        ("試遊・標 三人組", Play("試遊・標 三人組")),
        ("反撃 (ヒサ×カド)", Row("反撃 (ヒサ×カド)")),
    };

    internal static readonly I.W[] Waves = new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M4, I.M5, I.Nine }.Concat(I.StrongWaves).ToArray();

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Agg
    {
        public long N, Wins, WinT, Win1, Draw, Fell, Turns;
        public readonly long[] Hisa = new long[BattleContext.Ch365.Fields * BattleContext.Ch365.Width], Misa = new long[BattleContext.Ch365.Fields * BattleContext.Ch365.Width];
        public void Add(Agg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Win1 += o.Win1; Draw += o.Draw; Fell += o.Fell; Turns += o.Turns;
            for (int i = 0; i < Hisa.Length; i++) { Hisa[i] += o.Hisa[i]; Misa[i] += o.Misa[i]; }
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : F1((double)WinT / Wins);
        public long F(bool hisa, int field, int t) => (hisa ? Hisa : Misa)[field * BattleContext.Ch365.Width + t];
    }

    internal static Agg One(Formation f, I.W w, int seed, bool count)
    {
        var (r, p, _) = count ? X.Raw(f, w, seed, 0, true, verbose: false) : I.Raw(f, w, seed);
        var a = new Agg { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; if (r.Turns <= 1) a.Win1 = 1; }
        else if (r.PlayerSurvivors > 0) a.Draw = 1;   // 30T の打ち切り（味方が残ったまま決着しない）
        a.Fell = p.Count(u => u.LastDeathTurn > 0);
        if (count)
        {
            if (r.TallyByUnit.TryGetValue("hisa", out var ht) && ht.Ch365 is { } h) for (int i = 0; i < h.Length; i++) a.Hisa[i] += h[i];
            if (r.TallyByUnit.TryGetValue("tome", out var mt) && mt.Ch365 is { } m) for (int i = 0; i < m.Length; i++) a.Misa[i] += m[i];
        }
        return a;
    }

    internal static Agg Measure(Formation f, I.W w, bool count = false, int n = Seeds)
    {
        var parts = new Agg[n];
        Parallel.For(0, n, i => parts[i] = One(f, w, i, count));
        var all = new Agg();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    // ---------------------------------------------------------------------------------
    // §2-2 代表台
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        Console.WriteLine($"# {Tag} §2-2 代表台 × 波 × 版（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。強い条件は第2〜5波 × HP 500% ／ 攻 300% の4波の平均。");
        Console.WriteLine();

        // 1. 台 B × 勇者の輪の数え上げ
        Console.WriteLine("## 1. 台 B × 勇者（輪の数え上げ・1戦あたり・T の列は台本のターン番号）");
        Console.WriteLine();
        Console.WriteLine("羽はそのターンの終わりの枚数（初期 1 ＋ 増えた羽・羽は減らない）。玉は溢れ 20 で1つ。持ち越した ＝ ターンの終わりに溜まりに残った溢れ（玉換算）、捨てた ＝ P-t のターンの終わり ／ P-60 ／ P-180 の上限で捨てた溢れ（玉換算）。決着の後のターンは 0 として 100 戦で割った。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率・倒しT | " + string.Join(" | ", Enumerable.Range(1, TT).Select(t => $"T{t}: 羽 ／ 号令の層 ／ できた玉 ／ 持ち越した ／ 捨てた")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(1, TT).Select(_ => "---|")));
        var bAgg = new Dictionary<string, Agg>();
        foreach (var v in Vers)
        {
            var a = Measure(Apply(boards[0].F, v), I.Boss, count: true);
            bAgg[v.Name] = a;
            long cum = FeathersTrait.Initial * a.N;
            var cells = new List<string>();
            for (int t = 0; t <= TT; t++)
            {
                cum += a.F(false, BattleContext.Ch365.GainAccuse, t) + a.F(false, BattleContext.Ch365.GainBurst, t) + a.F(false, BattleContext.Ch365.GainOther, t) + a.F(false, BattleContext.Ch365.GainGrow, t);
                if (t == 0) continue;
                cells.Add($"{Per(cum, a.N)} ／ {Per(a.F(true, BattleContext.Ch365.BurstLayers, t), a.N)} ／ {Per(a.F(true, BattleContext.Ch365.Balls, t), a.N)} ／ "
                    + $"{Balls(a.F(true, BattleContext.Ch365.PoolCarry, t), a.N)} ／ {Balls(a.F(true, BattleContext.Ch365.PoolDropped, t), a.N)}");
            }
            Console.WriteLine($"| {v.Name} | {F1(a.Win)}・{a.WinTs}T | {string.Join(" | ", cells)} |");
        }
        Console.WriteLine();

        // 2〜3. 代表台 × 波 × 版
        var grid = new Agg[boards.Length, Vers.Length, Waves.Length];
        Parallel.For(0, boards.Length * Vers.Length * Waves.Length, k =>
        {
            int bi = k / (Vers.Length * Waves.Length), vi = k / Waves.Length % Vers.Length, wi = k % Waves.Length;
            grid[bi, vi, wi] = Measure(Apply(boards[bi].F, Vers[vi]), Waves[wi]);
        });
        int ns = I.StrongWaves.Length, s0 = Waves.Length - ns;
        string Cell(int bi, int vi, int wi) { var a = grid[bi, vi, wi]; return $"{F1(a.Win)}・{a.WinTs}T・倒 {Per(a.Fell, a.N)}"; }
        string Strong(int bi, int vi) => F1(Enumerable.Range(s0, ns).Average(wi => grid[bi, vi, wi].Win));

        Console.WriteLine("## 2. 標軸の台が勇者に勝てるか（ボス・勝率・倒しT）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Vers.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(0, Vers.Length).Select(vi => $"{F1(grid[bi, vi, 0].Win)}・{grid[bi, vi, 0].WinTs}T"))} |");
        Console.WriteLine();

        Console.WriteLine("## 3. 代表台の勝率・決着T（勝った戦）・倒れた味方（1戦あたり）");
        Console.WriteLine();
        for (int bi = 0; bi < boards.Length; bi++)
        {
            Console.WriteLine($"### {boards[bi].Name}");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", Waves.Take(s0).Select(w => w.Name)) + " | 強い条件（平均） |");
            Console.WriteLine("|---|" + string.Concat(Waves.Take(s0).Select(_ => "---|")) + "--:|");
            for (int vi = 0; vi < Vers.Length; vi++)
                Console.WriteLine($"| {Vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, s0).Select(wi => Cell(bi, vi, wi)))} | {Strong(bi, vi)} |");
            Console.WriteLine();
        }

        // 5. 壊れの兆し
        var bad = new List<string>();
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 1; vi < Vers.Length; vi++)
                for (int wi = 0; wi < Waves.Length; wi++)
                {
                    var a = grid[bi, vi, wi]; var b = grid[bi, 0, wi]; var w = Waves[wi];
                    if ((w == I.Guard || w == I.Bat || w == I.Nine) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 1T 決着 {Pct(a.Win1, a.N)}%");
                    if (b.Win - a.Win >= 15) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 勝率 {F1(b.Win)} → {F1(a.Win)}");
                    if (w == I.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4 && !(b.Wins > 0 && (double)b.WinT / b.Wins < 4)) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × ボス: 倒しT {a.WinTs}（規定 {b.WinTs}）");
                    if (100.0 * (a.Draw - b.Draw) / a.N >= 5) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 打ち切り {Pct(b.Draw, b.N)} → {Pct(a.Draw, a.N)}%");
                }
        var baseBoss = new List<string>();
        for (int bi = 0; bi < boards.Length; bi++)
            if (grid[bi, 0, 0].Wins > 0 && (double)grid[bi, 0, 0].WinT / grid[bi, 0, 0].Wins < 4) baseBoss.Add($"{boards[bi].Name} {grid[bi, 0, 0].WinTs}T");
        Console.WriteLine("## 5. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ 勝率が規定から 15pt 以上落ちる ／ ボス 4T 未満（規定で既に 4T 未満の台は除く）／ 30T の打ち切りが 5pt 以上増える）");
        Console.WriteLine();
        Console.WriteLine(bad.Count == 0 ? "なし。" : string.Join(Environment.NewLine, bad.Select(x => "- " + x)));
        Console.WriteLine();
        Console.WriteLine($"規定で既にボス 4T 未満の台: {(baseBoss.Count == 0 ? "なし" : string.Join("・", baseBoss))}。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §2-2 の 4 `compare`
    // ---------------------------------------------------------------------------------
    static double[,] CGrid((string Name, Formation F)[] rows, Func<Formation, Formation> map, Func<int, bool> only)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            if (!only(ri)) { g[ri, wi] = double.NaN; return; }
            var f = map(rows[ri].F);
            int wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        return g;
    }

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    static void CompareAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        var b = CGrid(rows, f => f, _ => true);
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        var hisaRows = Enumerable.Range(0, rows.Length).Where(ri => Has(rows[ri].F, "hisa")).ToHashSet();
        string Means(double[,] g) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => g[r, w]))));
        Console.WriteLine($"# {Tag} §2-2 の 4 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"ヒサのいる行 {hisaRows.Count}（{string.Join("、", hisaRows.Select(ri => rows[ri].Name))}）。版はヒサだけを替えるので、ヒサのいない行は規定と同じ台（同じ結果）——版の列ではヒサのいる行だけを回した。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|--:|---|");
        Console.WriteLine($"| 規定 | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} |");
        foreach (var v in Vers.Skip(1))
        {
            var g0 = CGrid(rows, f => Apply(f, v), ri => hisaRows.Contains(ri));
            var g = new double[rows.Length, nw];
            for (int r = 0; r < rows.Length; r++) for (int w = 0; w < nw; w++) g[r, w] = hisaRows.Contains(r) ? g0[r, w] : b[r, w];
            int upR = 0, upC = 0, dnR = 0, dnC = 0;
            var lines = new List<string>();
            for (int r = 0; r < rows.Length; r++)
            {
                bool u = false, d = false;
                for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
                if (u) upR++; if (d) dnR++;
                if (u || d) lines.Add($"{rows[r].Name}: " + string.Join("・", Enumerable.Range(1, nw - 1).Where(w => g[r, w] != b[r, w]).Select(w => $"第{w + 1}波 {F1(b[r, w])} → {F1(g[r, w])}")));
            }
            Console.WriteLine($"| {v.Name} | {upR} ／ {upC} | {dnR} ／ {dnC} | {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} |");
            foreach (var l in lines) Console.WriteLine($"|  | {l} | | | |");
            for (int r = 0; r < rows.Length; r++)
            {
                if (!Enumerable.Range(1, nw - 1).Any(w => b[r, w] - g[r, w] >= 10.0)) continue;
                var parts = new List<string>();
                foreach (var id in rows[r].F.Occupied().Select(o => o.Def.Id).Distinct())
                {
                    var oth = Enumerable.Range(0, rows.Length).Where(o => o != r && rows[o].F.Occupied().Any(x => x.Def.Id == id)).ToList();
                    string nm = Short(rows[r].F.Occupied().First(o => o.Def.Id == id).Def);
                    if (oth.Count == 0) { parts.Add($"{nm} 他の行 0（分解が成立しない）"); continue; }
                    double dd = oth.Average(o => Enumerable.Range(1, nw - 1).Average(w => g[o, w] - b[o, w]));
                    parts.Add($"{nm} {Sg(dd)}（{oth.Count} 行）");
                }
                Console.WriteLine($"|  | (G2) {rows[r].Name}: {string.Join("・", parts)} | | | |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
