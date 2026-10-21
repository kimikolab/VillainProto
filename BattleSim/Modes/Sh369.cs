using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using S366 = Sh366Diag;
using S367 = Sh367Diag;

// =====================================================================================
// sh369 —— 第369期【軽い期】毒軸のループ: グザの転生（噴き出し・段・返り毒）にヴィオ ／ スィドを合わせる（版 Z1 ／ Z2 ／ Z3・対照 RCS）。
// 指示書は design/PHASE369_POISON_LOOP_SPEC.md ／ 報告は design/PHASE369_POISON_LOOP.md。規定は1つも変えない。
//
//     dotnet run --project BattleSim -c Release 0 sh369 boards                # §4: 5 台 × 版（規定 ／ RCS ／ Z1 ／ Z2 ／ Z3）× 6 波（seed 0..99）・誰が何Tに倒れたか・ループ・味方の毒の行き先・壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh369 compare <RCS|Z1|Z2|Z3> # `compare` 64 行 × 版（スィド ／ グザ ／ ヴィオのいる行だけを版で回す・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh369 check                 # 自己検査
//
// 版の駒は `UnitCatalog.GuzaZ` ／ `VioZ` ／ `SidZ`（`All` に入れない）・RCS ／ R は第368期の駒。蝕みなしの勇者は第366期の器具の中の対照（`Sh366Diag.BossNoTax`）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p369/predict.md`）。
// =====================================================================================
static partial class Sh369Diag
{
    internal const string PredictFile = ".tmp/p369/predict.md";
    const string Tag = "第369期";
    const int Seeds = 100, CompareSeeds = 200, HeroT = 6, LoopT = 8;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh369: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "boards": Boards(); return;
            case "compare": CompareVer(args.Length > 3 ? args[3] : "Z3"); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh369: モードは boards / compare <RCS|Z1|Z2|Z3> / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Nm(string id) => Short(UnitCatalog.Everyone.Concat(new[] { UnitCatalog.Sid }).First(d => d.Id == id));
    static string Sg(double x) => x.ToString("+0.0;-0.0;±0.0");
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    // ---------------------------------------------------------------------------------
    // 版と台
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, UnitDef? Guza, UnitDef? Vio, UnitDef? Sid);
    internal static readonly Ver[] Vers =
    {
        new("規定", null, null, null),
        new("RCS（対照）", UnitCatalog.GuzaR, UnitCatalog.VioRCS, UnitCatalog.SidRC),
        new("Z1 グザ", UnitCatalog.GuzaZ, UnitCatalog.VioR, UnitCatalog.SidR),
        new("Z2 グザ＋ヴィオ", UnitCatalog.GuzaZ, UnitCatalog.VioZ, UnitCatalog.SidR),
        new("Z3 三人", UnitCatalog.GuzaZ, UnitCatalog.VioZ, UnitCatalog.SidZ),
    };
    internal static Formation Apply(Formation f, Ver v)
    {
        var g = f;
        if (v.Guza is not null) g = S316.SwapId(g, "guza", v.Guza);
        if (v.Vio is not null) g = S316.SwapId(g, "vio", v.Vio);
        if (v.Sid is not null) g = S316.SwapId(g, "sid", v.Sid);
        return g;
    }

    internal static (string Name, Formation F)[] BoardList() => new (string, Formation)[]
    {
        ("試遊・毒", Presets.Versions.First(r => r.Name == "試遊・毒").F),
        ("毒特化・集団", Row(S367.BombRow)),
        ("澱み喰い (グザ×ヴィオ)", Row("澱み喰い (グザ×ヴィオ)")),
        ("毒+ベニ+ラウ", Row("毒+ベニ+ラウ")),
        ("毒 (グザ×ミオ×ラウ)", Row("毒 (グザ×ミオ×ラウ)")),
    };
    internal static readonly I.W[] Waves = S367.PWaves;   // ボス ／ 近衛 ／ 大隊 ／ 本編 第2 ／ 5波 ／ 蝕みなしの勇者
    static readonly string[] RouteCols = { "噴き出し", "手番の瘴気", "返り毒", "触媒の倍", "注ぎ", "スィドの返し", "スィドの吐き", "ほか" };

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Agg
    {
        public long N, Wins, WinT, Win1, Draw, Fell;
        public readonly Dictionary<string, (long N, long TSum)> FellBy = new();
        /// <summary>段に上がった（添字 1〜3）: 届いた戦 ／ 届いたターンの和。</summary>
        public readonly long[] StageN = new long[4], StageT = new long[4];
        public readonly long[] Backlash = new long[LoopT + 1];
        public readonly long[] HLayer = new long[HeroT + 1], HAlive = new long[HeroT + 1];
        /// <summary>勇者（ボスの波）へ書かれた層の内訳（<see cref="RouteCols"/> の順・滲み込みの名目）。</summary>
        public readonly long[] HWrote = new long[8];
        public long Double, Pour, BacklashAll, Sipped, Drained, Shouldered, Retort, Ticked, VentFoe, VentAlly;
        public void Add(Agg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Win1 += o.Win1; Draw += o.Draw; Fell += o.Fell;
            foreach (var (k, v) in o.FellBy) { var c = FellBy.GetValueOrDefault(k); FellBy[k] = (c.N + v.N, c.TSum + v.TSum); }
            for (int i = 0; i < 4; i++) { StageN[i] += o.StageN[i]; StageT[i] += o.StageT[i]; }
            for (int t = 0; t <= LoopT; t++) Backlash[t] += o.Backlash[t];
            for (int t = 0; t <= HeroT; t++) { HLayer[t] += o.HLayer[t]; HAlive[t] += o.HAlive[t]; }
            for (int i = 0; i < HWrote.Length; i++) HWrote[i] += o.HWrote[i];
            Double += o.Double; Pour += o.Pour; BacklashAll += o.BacklashAll; Sipped += o.Sipped; Drained += o.Drained; Shouldered += o.Shouldered; Retort += o.Retort; Ticked += o.Ticked; VentFoe += o.VentFoe; VentAlly += o.VentAlly;
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : F1((double)WinT / Wins);
        public string Cell => $"{F1(Win)}・{WinTs}T・倒 {Per(Fell, N)}";
        public string FellCell => FellBy.Count == 0 ? "—" : string.Join("・", FellBy.OrderByDescending(kv => kv.Value.N).Select(kv => $"{Nm(kv.Key)} {Pct(kv.Value.N, N)}%（T{F1((double)kv.Value.TSum / kv.Value.N)}）"));
        public string StageCell => string.Join(" ／ ", Enumerable.Range(1, 3).Select(i => StageN[i] == 0 ? "—" : $"T{F1((double)StageT[i] / StageN[i])}（{Pct(StageN[i], N)}%）"));
    }

    static int RouteCol(PoisonRoute? r) => r switch
    {
        PoisonRoute.Vent => 0, PoisonRoute.Miasma => 1, PoisonRoute.Backlash => 2, PoisonRoute.Catalyst => 3, PoisonRoute.Venom => 5, PoisonRoute.Spew => 6, _ => 7,
    };

    internal static Agg One(Formation f, I.W w, int seed)
    {
        var a = new Agg { N = 1 };
        var (r, p, e) = S367.Raw(f, w, seed, verbose: true);
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; if (r.Turns <= 1) a.Win1 = 1; }
        else if (r.PlayerSurvivors > 0) a.Draw = 1;
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { a.Fell++; var c = a.FellBy.GetValueOrDefault(u.Def.Id); a.FellBy[u.Def.Id] = (c.N + 1, c.TSum + u.LastDeathTurn); }
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!p.Any(u => u.Def.Id == id)) continue;
            if (id == "guza")
            {
                if (t.Z369StageTurn is { } st) for (int i = 1; i <= 3; i++) if (st[i] > 0) { a.StageN[i]++; a.StageT[i] += st[i]; }
                if (t.Z369BacklashByTurn is { } bt) for (int k = 1; k <= LoopT; k++) a.Backlash[k] += bt[k];
                a.BacklashAll += t.Z369Backlash; a.VentFoe += t.Z369VentFoe; a.VentAlly += t.Z369VentAlly;
            }
            if (id == "vio") { a.Double += t.Cat367Doubled; a.Pour += t.Cat367Poured; a.Sipped += t.Z369Sipped; a.Drained += t.SpitStored; }
            if (id == "sid") { a.Shouldered += t.Z369Shouldered; a.Retort += t.Z369Retort; }
        }
        var mine = p.ToDictionary(u => u.InstanceId);
        var hero = w.Boss ? e.FirstOrDefault(u => u.HasTrait(TraitId.BossMend40)) : null;
        foreach (var x in r.Events)
        {
            switch (x.Kind)
            {
                case BattleEventKind.StatusSnapshot when x.Text == "毒" && hero is not null && x.TargetId == hero.InstanceId && x.Turn is >= 1 and <= HeroT:
                    a.HLayer[x.Turn] += x.Amount;
                    break;
                case BattleEventKind.StatusGain when x.Text == StatusKeys.Poison && hero is not null && x.TargetId == hero.InstanceId:
                    a.HWrote[RouteCol(x.PoisonRoute)] += x.Amount;
                    break;
                case BattleEventKind.Status when x.Text == "毒" && x.InverterId is null && x.TargetId is int ti && mine.ContainsKey(ti):
                    a.Ticked += x.Amount;   // 毒に強くない味方が刻まれた量（毒に強い駒は刻みの出来事が出ない）
                    break;
            }
        }
        // 触媒の倍 ／ 注ぎは同じ経路なので、ヴィオの帳簿の名目の比で分ける（勇者の戦は敵が勇者1体なので帳簿 ＝ 勇者へ）。
        if (hero is not null && a.Double + a.Pour > 0)
        {
            long cat = a.HWrote[3];
            a.HWrote[4] = cat * a.Pour / (a.Double + a.Pour);
            a.HWrote[3] = cat - a.HWrote[4];
        }
        if (hero is not null) { int last = hero.LastDeathTurn > 0 ? hero.LastDeathTurn : r.Turns; for (int t = 1; t <= Math.Min(HeroT, last); t++) a.HAlive[t] = 1; }
        return a;
    }

    internal static Agg Measure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Agg[n];
        Parallel.For(0, n, i => parts[i] = One(f, w, i));
        var all = new Agg();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    // ---------------------------------------------------------------------------------
    // §4 5台 × 版 × 波
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        Console.WriteLine($"# {Tag} §4 毒軸のループ —— 5台 × 版 × 波（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。版はグザ ／ ヴィオ ／ スィドだけを替える（いない駒は替えない）。");
        Console.WriteLine();
        var grid = new Agg[boards.Length, Vers.Length, Waves.Length];
        Parallel.For(0, boards.Length * Vers.Length * Waves.Length, k =>
        {
            int bi = k / (Vers.Length * Waves.Length), vi = k / Waves.Length % Vers.Length, wi = k % Waves.Length;
            grid[bi, vi, wi] = Measure(Apply(boards[bi].F, Vers[vi]), Waves[wi]);
        });
        int bw = 0, nw = Waves.Length - 1;

        Console.WriteLine("## 1. 台 × 版 × 波（勝率・倒しT（勝った戦）・倒れた味方（1戦あたり））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < Vers.Length; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {Vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, Waves.Length).Select(wi => grid[bi, vi, wi].Cell))} |");
        Console.WriteLine();

        Console.WriteLine("## 2. 誰が何Tに倒れたか（駒 倒れた戦の割合%（倒れたターンの平均））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < Vers.Length; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {Vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, Waves.Length).Select(wi => grid[bi, vi, wi].FellCell))} |");
        Console.WriteLine();

        Console.WriteLine("## 3. ループが回っているか（1戦あたり・グザ Z の版）");
        Console.WriteLine();
        Console.WriteLine("段 ＝ 段に上がったターンの平均（届いた戦の割合）。返り毒 ＝ 返り毒で敵へ移った層（ターンごと）。勇者の層はターンの頭・勇者が生きていたターンの平均。書き手は勇者へ書かれた層（`StatusGain` の経路・滲み込み・触媒の倍 ／ 注ぎはヴィオの帳簿の比で分けた）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 段1 ／ 段2 ／ 段3 | 返り毒 T1〜T8（計） | 勇者の層 T2〜T6 | 勇者へ書いた層（" + string.Join(" ／ ", RouteCols) + "） |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < Vers.Length; vi++)
                foreach (int wi in new[] { bw, nw, 1, 4 })
                {
                    var a = grid[bi, vi, wi];
                    bool boss = Waves[wi].Boss;
                    Console.WriteLine($"| {boards[bi].Name} | {Vers[vi].Name} | {Waves[wi].Name} | {a.StageCell} | {string.Join(" ", Enumerable.Range(1, LoopT).Select(t => Per(a.Backlash[t], a.N)))}（{Per(a.BacklashAll, a.N)}） | "
                        + $"{(boss ? string.Join(" ", Enumerable.Range(2, HeroT - 1).Select(t => a.HAlive[t] == 0 ? "—" : Per(a.HLayer[t], a.HAlive[t]))) : "—")} | {(boss ? string.Join(" ／ ", a.HWrote.Select(v => Per(v, a.N))) : "—")} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 4. 味方の毒の行き先（1戦あたり・層）");
        Console.WriteLine();
        Console.WriteLine("返り毒で敵へ ／ ヴィオが啜った（Z）・吸った（澱み喰い）／ スィドが引き受けた ／ 毒に強くない駒が刻まれた量（傷・名目）。噴き出しは名目（敵へ ／ 味方へ）。スィドの返し ＝ 衣の返しで殴った敵へ浴びせた層。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 噴き出し 敵へ ／ 味方へ | 返り毒で敵へ | ヴィオ 啜った ／ 吸った | スィド 引き受けた ／ 返し | 毒に強くない駒の刻み |");
        Console.WriteLine("|---|---|---|---|--:|---|---|--:|");
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < Vers.Length; vi++)
                foreach (int wi in new[] { bw, nw, 1, 4 })
                {
                    var a = grid[bi, vi, wi];
                    Console.WriteLine($"| {boards[bi].Name} | {Vers[vi].Name} | {Waves[wi].Name} | {Per(a.VentFoe, a.N)} ／ {Per(a.VentAlly, a.N)} | {Per(a.BacklashAll, a.N)} | {Per(a.Sipped, a.N)} ／ {Per(a.Drained, a.N)} | {Per(a.Shouldered, a.N)} ／ {Per(a.Retort, a.N)} | {Per(a.Ticked, a.N)} |");
                }
        Console.WriteLine();

        var bad = new List<string>();
        for (int bi = 0; bi < boards.Length; bi++)
            for (int wi = 0; wi < Waves.Length; wi++)
            {
                var b = grid[bi, 0, wi];
                for (int vi = 1; vi < Vers.Length; vi++)
                {
                    var a = grid[bi, vi, wi]; var w = Waves[wi];
                    if ((w == I.Guard || w == I.Bat) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 1T 決着 {Pct(a.Win1, a.N)}%");
                    if (w.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4 && !(b.Wins > 0 && (double)b.WinT / b.Wins < 4)) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 倒しT {a.WinTs}（規定 {b.WinTs}）");
                    if (100.0 * (a.Draw - b.Draw) / a.N >= 5) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 30T の打ち切り {Pct(b.Draw, b.N)} → {Pct(a.Draw, a.N)}%");
                    if (b.Win - a.Win >= 15) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 勝率 {F1(b.Win)} → {F1(a.Win)}（参考・落ちた側）");
                }
            }
        Console.WriteLine("## 5. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満（規定で既に 4T 未満は除く）／ 30T の打ち切りが 5pt 以上増える ／ 参考: 勝率が規定から 15pt 以上落ちる）");
        Console.WriteLine();
        Console.WriteLine(bad.Count == 0 ? "なし。" : string.Join(Environment.NewLine, bad.Select(x => "- " + x)));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` × 版
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

    static void CompareVer(string ver)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var v = ver switch { "RCS" => Vers[1], "Z1" => Vers[2], "Z2" => Vers[3], "Z3" => Vers[4], _ => throw new ArgumentException("sh369 compare の版は RCS / Z1 / Z2 / Z3") };
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        Func<int, bool> only = ri => new[] { "sid", "guza", "vio" }.Any(id => Has(rows[ri].F, id));
        var b = CGrid(rows, f => f, _ => true);
        var g0 = CGrid(rows, f => Apply(f, v), only);
        var g = new double[rows.Length, nw];
        int touched = 0;
        for (int r = 0; r < rows.Length; r++) { if (only(r)) touched++; for (int w = 0; w < nw; w++) g[r, w] = only(r) ? g0[r, w] : b[r, w]; }
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] x) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => x[r, w]))));
        Console.WriteLine($"# {Tag} `compare` {rows.Length} 行 × 版 {v.Name}（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"版で回した行 {touched}（スィド ／ グザ ／ ヴィオのいる行・いない行は規定と同じ台 ＝ 同じ結果）。");
        Console.WriteLine();
        int upR = 0, upC = 0, dnR = 0, dnC = 0;
        var lines = new List<string>();
        for (int r = 0; r < rows.Length; r++)
        {
            bool u = false, d = false;
            for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
            if (u) upR++; if (d) dnR++;
            if (u || d) lines.Add($"{rows[r].Name}: " + string.Join("・", Enumerable.Range(1, nw - 1).Where(w => g[r, w] != b[r, w]).Select(w => $"第{w + 1}波 {F1(b[r, w])} → {F1(g[r, w])}")));
        }
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|--:|---|");
        Console.WriteLine($"| 規定 | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} |");
        Console.WriteLine($"| {v.Name} | {upR} ／ {upC} | {dnR} ／ {dnC} | {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} |");
        Console.WriteLine();
        foreach (var l in lines) Console.WriteLine("- " + l);
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
            Console.WriteLine($"- (G2) {rows[r].Name}: {string.Join("・", parts)}");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
