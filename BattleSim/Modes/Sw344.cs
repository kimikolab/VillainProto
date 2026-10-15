using BattleCore;
using static Common;
using G343 = Golm343Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// sw344 —— 第344期【軽い期】ゴルムの「精気を呑む」の条件（SW-a 腹を使う ／ SW-b 弱った敵だけ ／ SW-a ＋ b）。呑んだ後はどれも第343期の GT-a2（吐き捨てる）。
// 指示書は design/PHASE344_GOLM_SWALLOW_LIMIT_SPEC.md ／ 報告は design/PHASE344_GOLM_SWALLOW_LIMIT.md。台と波と1戦の計数は `golm343` のものを使う。
//
//     dotnet run --project BattleSim -c Release 0 sw344 boards [版,…]           # §4 代表台 5 × 5 波 × 版（規定 ／ GT-a2 ／ SW-a ／ SW-b ／ SW-a＋b）・seed 0..99
//     dotnet run --project BattleSim -c Release 0 sw344 compare [版,…]          # §4 `compare` 64 行 × 版（seed 0..199・基準は規定）
//     dotnet run --project BattleSim -c Release 0 sw344 log [台] [波] [seed] [版] # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 sw344 check                   # §7 自己検査
//
// 版の名前（ASCII）: base（規定）／ gt（第343期の GT-a2・対照）／ a ／ b ／ ab。台は `golm343` と同じ（guard ／ havoc ／ utsu ／ shock ／ poison）。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・`.tmp/p344/predict.md`）。
// =====================================================================================
static partial class Sw344Diag
{
    internal const string PredictFile = ".tmp/p344/predict.md";
    const int Seeds = 100;
    const int CompareSeeds = 200;
    const string Tag = "第344期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sw344: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "log": Log(A(3, "guard"), A(4, "2"), int.Parse(A(5, "0")), V(A(6, "ab"))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "check": Check(); return;
            default: Console.WriteLine("sw344: モードは log / boards / compare / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);

    internal sealed record Ver(string Name, string Ascii, UnitDef? Golm);
    internal static readonly Ver[] Vers =
    {
        new("規定", "base", null),
        new("GT-a2（第343期）", "gt", UnitCatalog.GolmGTa2),
        new("SW-a 腹を使う", "a", UnitCatalog.GolmSWa),
        new("SW-b 弱った敵だけ", "b", UnitCatalog.GolmSWb),
        new("SW-a＋b", "ab", UnitCatalog.GolmSWab),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => v.Golm is null ? f : S316.SwapId(f, "golm", v.Golm);
    static S287.Wave[] Waves => G343.Waves;
    static bool IsElite(S287.Wave w) => w.Name is "近衛" or "大隊";

    static void Log(string board, string wave, int seed, Ver v)
    {
        var (name, f0) = board switch { "guard" => G343.Boards()[0], "havoc" => G343.Boards()[1], "utsu" => G343.Boards()[2], "shock" => G343.Boards()[3], "poison" => G343.Boards()[4], _ => G343.Boards().First(b => b.Name == board) };
        var w = T323.WaveOf(wave);
        var r = BattleEngine.Run(BattleEngine.Materialize(Apply(f0, v), BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# {name} × {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = G343.Boards();
        var res = new Dictionary<(string, string, string), G343.St>();
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = G343.Measure(Apply(bf, v), w, Seeds);
        Console.WriteLine($"# {Tag} §4 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine("## 1. ゴルムの手番の中身（1戦あたり）: 手番 ／ 腹（手番の頭の平均）／ 呑んだ（1回の HP ・最大 HP に対する %）／ 吐いた（毒・火・雷・濃・標）／ 殴った");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 手番 | 腹 | 呑んだ | 1回の HP（%） | 吐いた ／戦 | 殴った |");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|---|--:|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                foreach (var v in vers.Skip(1))
                {
                    var z = res[(bn, v.Name, w.Name)]; var g = z.G;
                    string sp = g.Gt343Spits == 0 ? "—" : $"{Per(g.Gt343SpitPoison, z.N)}・{Per(g.Gt343SpitBurn, z.N)}・{Per(g.Gt343SpitShock, z.N)}・{Per(g.Gt343SpitDensity, z.N)}・{Per(g.Gt343SpitMark, z.N)}";
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Ascii} | {Per(g.Gt343Turns, z.N)} | {Per(g.Gt343BellySum, g.Gt343Turns)} | {Per2(g.Gt343Swallows, z.N)} | {Per(g.Gt343SwallowHp, g.Gt343Swallows)}（{Per(g.Sw344HpPctSum, g.Gt343Swallows)}%） | {sp} | {Per(g.Gt343Attacks, z.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 2. 代表台の勝率（規定との差）・決着T（ボスは倒しT）・倒れた味方 ／戦");
        Console.WriteLine();
        foreach (var (bn, _) in boards)
        {
            Console.WriteLine($"### {bn}");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
            foreach (var v in vers)
                Console.WriteLine($"| {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var z = res[(bn, v.Name, w.Name)]; double b0 = res[(bn, vers[0].Name, w.Name)].Win;
                    string win = v == vers[0] || z.Win == b0 ? F1(z.Win) : $"{F1(z.Win)}（{Sg(z.Win - b0)}）";
                    return $"{win}・{Per(z.WinT, z.Wins)}T・倒 {Per2(z.AllyDeaths, z.N)}";
                })) + " |");
            Console.WriteLine();
        }

        Console.WriteLine("## 3. 倒れたときの還し（ゴルム・1戦あたりの届いた量 ／ 還した回数）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | " + string.Join(" | ", vers.Select(v => v.Ascii)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(vers.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                Console.WriteLine($"| {bn} | {w.Name} | " + string.Join(" | ", vers.Select(v => { var z = res[(bn, v.Name, w.Name)]; return $"{Per(z.G.Refunded, z.N)} ／ {Per2(z.G.Refunds, z.N)}"; })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 4. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ 勝率が規定から 15pt 以上落ちる ／ ボスを 4T 未満で倒す——規定で 4T 以上だった台だけ）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var bad = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                {
                    var z = res[(bn, v.Name, w.Name)]; var b = res[(bn, vers[0].Name, w.Name)];
                    if (IsElite(w) && z.N > 0 && 100.0 * z.OneT / z.N >= 5) bad.Add($"{bn} × {w.Name} 1T 決着 {Pct(z.OneT, z.N)}%");
                    if (z.Win - b.Win <= -15) bad.Add($"{bn} × {w.Name} {Sg(z.Win - b.Win)}");
                    if (w.Name == "ボス" && z.Wins > 0 && (double)z.WinT / z.Wins < 4.0 && !(b.Wins > 0 && (double)b.WinT / b.Wins < 4.0)) bad.Add($"{bn} × ボス {Per(z.WinT, z.Wins)}T");
                }
            Console.WriteLine($"- **{v.Name}**: {(bad.Count == 0 ? "なし" : string.Join("・", bad))}");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    static void CompareAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        double[,] Grid(Ver v)
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = Apply(rows[ri].F, v);
                int wins = 0;
                for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / CompareSeeds;
            });
            return g;
        }
        var b = Grid(vers[0]);
        Console.WriteLine($"# {Tag} §4 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        int last = nw - 1;
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちたセル | 第五波 95% 超の行（規定 → 版・新たに） |");
        Console.WriteLine("|---|---|--:|---|");
        var grids = vers.Skip(1).Select(v => (v, g: Grid(v))).ToArray();
        int b95 = Enumerable.Range(0, rows.Length).Count(r => b[r, last] > 95);
        foreach (var (v, g) in grids)
        {
            int upRows = 0, upCells = 0, down = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                bool any = false;
                for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upCells++; any = true; } else if (g[r, w] < b[r, w]) down++; }
                if (any) upRows++;
            }
            int g95 = Enumerable.Range(0, rows.Length).Count(r => g[r, last] > 95), newly = Enumerable.Range(0, rows.Length).Count(r => g[r, last] > 95 && b[r, last] <= 95);
            Console.WriteLine($"| {v.Name} | {upRows} ／ {upCells} | {down} | {b95} → {g95}（新たに {newly}） |");
        }
        foreach (var (v, g) in grids)
        {
            Console.WriteLine();
            Console.WriteLine($"## {v.Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, g, "golm");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
