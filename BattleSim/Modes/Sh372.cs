using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S366 = Sh366Diag;
using S367 = Sh367Diag;
using S370 = Sh370Diag;

// =====================================================================================
// sh372 —— 第372期【軽い期】段0: W5（グザ ／ ヴィオ ／ スィド）を規定に ／ 本体: ヴィオの連撃の段（版 Va ／ Vb ／ Vc）。
// 指示書は design/PHASE372_VIO_FLURRY_SPEC.md ／ 報告は design/PHASE372_VIO_FLURRY.md。本体の版は規定にしない。
//
//     dotnet run --project BattleSim -c Release 0 sh372 boards               # §3-2: 5 台 × 版（規定 ／ Va ／ Vb ／ Vc）× 6 波（seed 0..99）・誰が何Tに倒れたか・ループの表（撃った数つき）・敵ごとの毒・段に上がったターン・壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh372 compare <Va|Vb|Vc>   # `compare` 64 行 × 版（ヴィオのいる行だけを版で回す・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh372 check                # 自己検査
//
// 1戦の計数は第370期の器具（`Sh370Diag.One` ／ `Agg`・第372期に撃った数と敵ごとの毒を足した）。版の駒は `UnitCatalog.VioVa` ／ `VioVb` ／ `VioVc`（`All` に入れない）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p372/predict.md`）。
// =====================================================================================
static partial class Sh372Diag
{
    internal const string PredictFile = ".tmp/p372/predict.md";
    const string Tag = "第372期";
    const int Seeds = 100, CompareSeeds = 200, LoopT = 8;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh372: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "boards": Boards(); return;
            case "compare": CompareVer(args.Length > 3 ? args[3] : "Va"); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh372: モードは boards / compare <Va|Vb|Vc> / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Sg(double x) => x.ToString("+0.0;-0.0;±0.0");
    static Formation Row(string n) => Pin372(CompareBuilds().First(r => r.Name == n).F);   // 第373期: 第372期の規定のヴィオ（`VioH372`）に固定
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    internal static readonly S370.Ver[] Vers =
    {
        new("規定（W5）", null, null, null),
        new("Va 散らす（50 ／ 150）", null, UnitCatalog.VioVa, null),
        new("Vb 高い段（100 ／ 300）", null, UnitCatalog.VioVb, null),
        new("Vc 集中（50 ／ 150）", null, UnitCatalog.VioVc, null),
    };
    internal static Formation Apply(Formation f, S370.Ver v) => S370.Apply(f, v);

    /// <summary>攻撃力の台（第370期と同じ並び ＝ 試遊・毒のドルガ → ガン・駒は段0 の後の規定）。</summary>
    internal static Formation AtkBoard() => Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Sid, center: UnitCatalog.Guza, back1: UnitCatalog.VioH372, back3: UnitCatalog.Gan);   // 第373期: 第372期の規定に固定

    internal static (string Name, Formation F)[] BoardList() => new (string, Formation)[]
    {
        ("試遊・毒", Pin372(Presets.Versions.First(r => r.Name == "試遊・毒").F)),
        ("試遊・毒 席替え", Pin372(Presets.Versions.First(r => r.Name == "試遊・毒 席替え").F)),
        ("毒特化・集団", Row(S367.BombRow)),
        ("澱み喰い (グザ×ヴィオ)", Row("澱み喰い (グザ×ヴィオ)")),
        ("攻撃力の台（ガン）", AtkBoard()),
    };
    internal static readonly I.W[] Waves = S367.PWaves;

    // ---------------------------------------------------------------------------------
    // §3-2 5台 × 版 × 波
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        Console.WriteLine($"# {Tag} §3-2 ヴィオの連撃の段 —— 5台 × 版 × 波（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。版はヴィオだけを替える。駒は段0 の後の規定（グザ ／ ヴィオ ／ スィド ＝ 第371期の W5）。1戦の計数は `sh370` と同じ。");
        Console.WriteLine();
        var grid = new S370.Agg[boards.Length, Vers.Length, Waves.Length];
        var vmax = new long[boards.Length, Vers.Length, Waves.Length];
        var pmax = new long[boards.Length, Vers.Length, Waves.Length];
        Parallel.For(0, boards.Length * Vers.Length * Waves.Length, k =>
        {
            int bi = k / (Vers.Length * Waves.Length), vi = k / Waves.Length % Vers.Length, wi = k % Waves.Length;
            var f = Apply(boards[bi].F, Vers[vi]);
            var parts = new S370.Agg[Seeds];
            Parallel.For(0, Seeds, s => parts[s] = S370.One(f, Waves[wi], s));
            var all = new S370.Agg();
            foreach (var x in parts) all.Add(x);
            grid[bi, vi, wi] = all;
            vmax[bi, vi, wi] = parts.Max(a => Enumerable.Range(1, LoopT).Select(t => a.VAlive[t] > 0 ? a.VAtk[t] : 0).Max());
            pmax[bi, vi, wi] = parts.Max(a => a.Strike.Max());
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

        Console.WriteLine("## 3. ループの表（ターンごと T1〜T8・1戦あたり・第371期 §3-1 の 3 に「撃った数」を足した）");
        Console.WriteLine();
        Console.WriteLine("撃った数 ＝ ヴィオが手番に撃った数（そのターン・ヴィオが生きていた戦で割らず 1戦あたり）。段 ＝ 2連撃 ／ 3連撃に初めて上がったターンの平均（上がった戦の割合）。");
        Console.WriteLine();
        foreach (int bi in new[] { 0, 1, 4 })
            foreach (int wi in new[] { bw, nw })
            {
                Console.WriteLine($"### {boards[bi].Name} × {Waves[wi].Name}");
                Console.WriteLine();
                Console.WriteLine("| 版 | 2連撃 ／ 3連撃 | 跳ねた | ヴィオの攻撃力（頭） | 撃った数 | ヴィオの毒 | スィドの層（頭） | スィドの返し | 勇者の層（頭） |");
                Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
                for (int vi = 0; vi < Vers.Length; vi++)
                {
                    var a = grid[bi, vi, wi];
                    string st = $"{(a.Stage2N == 0 ? "—" : $"T{F1((double)a.Stage2T / a.Stage2N)}（{Pct(a.Stage2N, a.N)}%）")} ／ {(a.Stage3N == 0 ? "—" : $"T{F1((double)a.Stage3T / a.Stage3N)}（{Pct(a.Stage3N, a.N)}%）")}";
                    Console.WriteLine($"| {Vers[vi].Name} | {st} | {a.Turns(a.Splash)} | {a.Turns(a.VAtk, a.VAlive)} | {a.Turns(a.Hits)} | {a.Turns(a.Strike)} | {a.Turns(a.SLayer, a.SAlive)} | {a.Turns(a.Retort)} | {a.Turns(a.HLayer, a.HAlive)} |");
                }
                Console.WriteLine();
            }

        Console.WriteLine("## 4. ヴィオの毒の行き先（近衛 ／ 大隊 ／ 本編 第5波・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("毒が乗った敵 ＝ 攻撃力ぶんの毒を1回以上受けた敵の数。最も多く受けた敵の層 ＝ 1戦で一番多く受けた敵の受けた層。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 勝率 | ヴィオの毒 計 | 毒が乗った敵 | 最も多く受けた敵の層 | 撃った数 計 | 2連撃に上がった（戦の割合・平均T） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|---|");
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < Vers.Length; vi++)
                foreach (int wi in new[] { 1, 2, 4 })
                {
                    var a = grid[bi, vi, wi];
                    Console.WriteLine($"| {boards[bi].Name} | {Vers[vi].Name} | {Waves[wi].Name} | {F1(a.Win)} | {Per(a.StrikeAll, a.N)} | {Per(a.StrikeTargets, a.N)} | {Per(a.StrikeTop, a.N)} | {Per(a.Hits.Sum(), a.N)} | {Pct(a.Stage2N, a.N)}%・{(a.Stage2N == 0 ? "—" : "T" + F1((double)a.Stage2T / a.Stage2N))} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 5. 攻撃力の台: 段に上がったターン（試遊・毒 と 攻撃力の台・勇者 ／ 蝕みなし ／ 近衛）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 試遊・毒 2連撃 ／ 3連撃 | 攻撃力の台 2連撃 ／ 3連撃 | ヴィオが外から受けた攻撃力（攻撃力の台） |");
        Console.WriteLine("|---|---|---|---|--:|");
        for (int vi = 1; vi < Vers.Length; vi++)
            foreach (int wi in new[] { bw, nw, 1 })
            {
                string St(S370.Agg a) => $"{(a.Stage2N == 0 ? "—" : $"T{F1((double)a.Stage2T / a.Stage2N)}（{Pct(a.Stage2N, a.N)}%）")} ／ {(a.Stage3N == 0 ? "—" : $"T{F1((double)a.Stage3T / a.Stage3N)}（{Pct(a.Stage3N, a.N)}%）")}";
                Console.WriteLine($"| {Vers[vi].Name} | {Waves[wi].Name} | {St(grid[0, vi, wi])} | {St(grid[4, vi, wi])} | {Per(grid[4, vi, wi].VioWhet, grid[4, vi, wi].N)} |");
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
        Console.WriteLine("## 6. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満（規定で既に 4T 未満は除く）／ 30T の打ち切りが 5pt 以上増える ／ 参考: 勝率が規定から 15pt 以上落ちる）");
        Console.WriteLine();
        Console.WriteLine(bad.Count == 0 ? "なし。" : string.Join(Environment.NewLine, bad.Select(x => "- " + x)));
        Console.WriteLine();
        Console.WriteLine("ヴィオの攻撃力の最大 ／ 1ターンに乗せた毒の最大（その台 × 波の 100 戦の最大）: " + string.Join("・", Enumerable.Range(0, Vers.Length).SelectMany(vi => new[] { bw, nw }.Select(wi => $"試遊・毒 × {Vers[vi].Name} × {Waves[wi].Name} {vmax[0, vi, wi]} ／ {pmax[0, vi, wi]}"))) + "。");
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
        var v = ver switch { "Va" => Vers[1], "Vb" => Vers[2], "Vc" => Vers[3], _ => throw new ArgumentException("sh372 compare の版は Va / Vb / Vc") };
        var rows = CompareBuilds().Select(r => (r.Name, F: Pin372(r.F))).ToArray();   // 第373期: 第372期の規定のヴィオに固定
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        Func<int, bool> only = ri => Has(rows[ri].F, "vio");
        var b = CGrid(rows, f => f, _ => true);
        var g0 = CGrid(rows, f => Apply(f, v), only);
        var g = new double[rows.Length, nw];
        int touched = 0;
        for (int r = 0; r < rows.Length; r++) { if (only(r)) touched++; for (int w = 0; w < nw; w++) g[r, w] = only(r) ? g0[r, w] : b[r, w]; }
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] x) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => x[r, w]))));
        Console.WriteLine($"# {Tag} `compare` {rows.Length} 行 × 版 {v.Name}（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"版で回した行 {touched}（ヴィオのいる行・{string.Join("、", Enumerable.Range(0, rows.Length).Where(only).Select(ri => rows[ri].Name))}）。");
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
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
