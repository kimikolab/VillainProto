using BattleCore;
using static Common;
using G343 = Golm343Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// rp345 —— 第345期【軽い期】段0: ゴルムの「呑む」S3（SW-a ＋ b ＋ 吐き捨てる）を規定に ／ 本体: 呑めない手番に何をするか（RP-a ロケットパンチ ／ RP-b ロケットパンチ ＋ 捕獲 ／ GA-a 踏み潰し）。
// 指示書は design/PHASE345_GOLM_ROCKET_PUNCH_SPEC.md ／ 報告は design/PHASE345_GOLM_ROCKET_PUNCH.md。台と波と1戦の計数は `golm343` のものを使う。
//
//     dotnet run --project BattleSim -c Release 0 rp345 s0                      # §2 段0: `compare` 64 行 × 旧（`GolmH344`）／ 新（規定）／ 第344期の SW-a＋b（seed 0..199）
//     dotnet run --project BattleSim -c Release 0 rp345 boards [版,…]           # §5 代表台 5 × 5 波 × 版（規定 ／ RP-a ／ RP-b ／ GA-a）・seed 0..99
//     dotnet run --project BattleSim -c Release 0 rp345 compare [版,…]          # §5 `compare` 64 行 × 版（seed 0..199・基準は段0 入りの規定）
//     dotnet run --project BattleSim -c Release 0 rp345 log [台] [波] [seed] [版] # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 rp345 check                   # §8 自己検査
//     dotnet run --project BattleSim -c Release 0 rp345 s346                    # 第346期 §3: 規定（RP-b）の `compare` と第345期の RP-b（`GolmH345` の上でゴルムだけ `GolmRPb`）の一致・守り型 × ボス ／ 大隊 ／ 第2波
//
// 版の名前（ASCII）: base（規定 ＝ S3）／ a（RP-a）／ b（RP-b）／ ga（GA-a）。台は `golm343` と同じ（guard ／ havoc ／ utsu ／ shock ／ poison）。
// 第346期: RP-b を規定にしたので、「規定」（base）と段0 の「新」は第345期の規定のゴルム（`GolmH345`・`Common.Pin345`）に固定した。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・`.tmp/p345/predict.md`）。
// =====================================================================================
static partial class Rp345Diag
{
    internal const string PredictFile = ".tmp/p345/predict.md";
    const int Seeds = 100;          // 軽い期
    const int CompareSeeds = 200;   // `compare` の帯（今どおり）
    const string Tag = "第345期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"rp345: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "s0": Stage0(); return;
            case "s346": Settle346(); return;   // 第346期
            case "log": Log(A(3, "guard"), A(4, "2"), int.Parse(A(5, "0")), V(A(6, "b"))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "check": Check(); return;
            default: Console.WriteLine("rp345: モードは s0 / log / boards / compare / check。"); return;
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
        new("規定（S3）", "base", null),
        new("RP-a ロケットパンチ", "a", UnitCatalog.GolmRPa),
        new("RP-b ロケットパンチ＋捕獲", "b", UnitCatalog.GolmRPb),
        new("GA-a 踏み潰し", "ga", UnitCatalog.GolmGAa),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    /// <summary>版はゴルムだけを差し替える（段0 入りの規定の上）。「規定」はいまの規定のゴルム（S3）のまま。</summary>
    internal static Formation Apply(Formation f, Ver v) => v.Golm is null ? Pin345(f) : S316.SwapId(f, "golm", v.Golm);   // 第346期: 「規定」は第345期の規定のゴルム（`GolmH345`）に固定
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

    // ---------------------------------------------------------------------------------
    // `compare` の格子（64 行 × 波・seed 0..199）
    // ---------------------------------------------------------------------------------
    static double[,] Grid((string Name, Formation F)[] rows, Func<Formation, Formation> map)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            var f = map(rows[ri].F);
            int wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        return g;
    }

    // ---------------------------------------------------------------------------------
    // §2 段0: 旧（`GolmH344`）／ 新（規定）／ 第344期の SW-a＋b（旧の上でゴルムだけ `GolmSWab`）
    // ---------------------------------------------------------------------------------
    static void Stage0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        var old = Grid(rows, Pin344);
        var now = Grid(rows, Pin345);   // 第346期: 「新」は第345期の規定（`GolmH345`）に固定
        var swab = Grid(rows, f => S316.SwapId(Pin344(f), "golm", UnitCatalog.GolmSWab));
        int same = 0, cells = rows.Length * nw;
        for (int r = 0; r < rows.Length; r++) for (int w = 0; w < nw; w++) if (now[r, w] == swab[r, w]) same++;
        Console.WriteLine($"# {Tag} §2 段0 —— `compare` {rows.Length} 行 × 旧（`GolmH344`）／ 新（規定 ＝ S3）・seed 0..{CompareSeeds - 1}");
        Console.WriteLine();
        Console.WriteLine($"**新（規定）と第344期の SW-a＋b（旧の上でゴルムだけ `GolmSWab`）の一致: {same} ／ {cells} セル**");
        Console.WriteLine();
        Console.WriteLine("## 旧 → 新");
        Console.WriteLine();
        S316.PrintCompare(rows, old, now, "golm");
        int upRows = 0, upCells = 0, down = 0, last = nw - 1;
        for (int r = 0; r < rows.Length; r++)
        {
            bool any = false;
            for (int w = 1; w < nw; w++) { if (now[r, w] > old[r, w]) { upCells++; any = true; } else if (now[r, w] < old[r, w]) down++; }
            if (any) upRows++;
        }
        int o95 = Enumerable.Range(0, rows.Length).Count(r => old[r, last] > 95), n95 = Enumerable.Range(0, rows.Length).Count(r => now[r, last] > 95);
        Console.WriteLine();
        Console.WriteLine($"上がった行 ／ セル（第2〜5波）{upRows} ／ {upCells}・落ちたセル {down}・第五波 95% 超の行 {o95} → {n95}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 第346期 §3: RP-b を規定にした確かめ（規定 ＝ いまの規定・対照 ＝ 第345期の規定の上でゴルムだけ `GolmRPb`）
    // ---------------------------------------------------------------------------------
    static void Settle346()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        var now = Grid(rows, f => f);
        var rpb = Grid(rows, f => S316.SwapId(Pin345(f), "golm", UnitCatalog.GolmRPb));
        var old = Grid(rows, Pin345);
        int same = 0, cells = rows.Length * nw;
        for (int r = 0; r < rows.Length; r++) for (int w = 0; w < nw; w++) if (now[r, w] == rpb[r, w]) same++;
        Console.WriteLine($"# 第346期 §3 —— 規定（RP-b）の `compare` {rows.Length} 行 × 第345期の RP-b（`GolmH345` の上でゴルムだけ `GolmRPb`）・seed 0..{CompareSeeds - 1}");
        Console.WriteLine();
        Console.WriteLine($"**規定と第345期の RP-b の一致: {same} ／ {cells} セル**");
        Console.WriteLine();
        Console.WriteLine("## 旧（`GolmH345`）→ 規定");
        Console.WriteLine();
        S316.PrintCompare(rows, old, now, "golm");
        Console.WriteLine();
        Console.WriteLine($"## 試遊・標 守り型 × ボス ／ 大隊 ／ 本編 第2波・seed 0..{Seeds - 1}（規定 ／ 第345期の RP-b）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 規定 | 第345期の RP-b | 旧（S3） |");
        Console.WriteLine("|---|---|---|---|");
        var guard = Presets.Playtest.First(r => r.Name == G343.GuardRow).F;
        foreach (var w in new[] { Waves[0], Waves[2], Waves[3] })
        {
            string Cell(G343.St z) => $"{F1(z.Win)}%・{Per(z.WinT, z.Wins)}T・手 {Per2(z.G.Rp345Punches + z.G.Rp345GrabNoPunch, z.N)}・呑 {Per2(z.G.Gt343Swallows, z.N)}";
            Console.WriteLine($"| {w.Name} | {Cell(G343.Measure(guard, w, Seeds))} | {Cell(G343.Measure(S316.SwapId(Pin345(guard), "golm", UnitCatalog.GolmRPb), w, Seeds))} | {Cell(G343.Measure(Pin345(guard), w, Seeds))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §5 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = G343.Boards();
        var res = new Dictionary<(string, string, string), G343.St>();
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = G343.Measure(Apply(bf, v), w, Seeds);
        Console.WriteLine($"# {Tag} §5 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("版はゴルムだけを差し替える（段0 入りの規定の上）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine("## 1. ゴルムの手番の中身（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("手番 ／ 腹（手番の頭の平均）／ 殴った（通常攻撃 ＋ 手を飛ばした ＋ 踏み潰した・RP は「後」＝ 狙った敵が前列でない回数）／ 殴って倒した（RP ／ GA）／ 呑んだ（RP-b は「殴らずに掴む ／ 殴って掴む」・"
            + "1回の HP）／ 殴った量（通常攻撃 ＋ RP ／ GA の主目標と隣）／ 量の素の値 ／回（攻 ＋ 腹の 1 割）／ 隣に当たった量（GA）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 手番 | 腹 | 殴った（後） | 殴って倒した | 呑んだ | 1回の HP（%） | 殴った量 | 素の量 ／回 | 隣 |");
        Console.WriteLine("|---|---|---|--:|--:|---|--:|---|---|--:|--:|--:|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                foreach (var v in vers)
                {
                    var z = res[(bn, v.Name, w.Name)]; var g = z.G;
                    long hits = g.Gt343Attacks + g.Rp345Punches + g.Ga345Stomps;
                    string hit = g.Rp345Punches > 0 ? $"{Per2(hits, z.N)}（後 {Per2(g.Rp345PunchBack, z.N)}）" : Per2(hits, z.N);
                    string sw2 = g.Rp345GrabNoPunch + g.Rp345GrabAfter > 0 ? $"{Per2(g.Gt343Swallows, z.N)}（{Per2(g.Rp345GrabNoPunch, z.N)} ／ {Per2(g.Rp345GrabAfter, z.N)}）" : Per2(g.Gt343Swallows, z.N);
                    long n1 = g.Rp345Punches + g.Ga345Stomps;
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Ascii} | {Per(g.Gt343Turns, z.N)} | {Per(g.Gt343BellySum, g.Gt343Turns)} | {hit} | {(n1 == 0 ? "—" : Per2(g.Rp345PunchKills, z.N))} | {sw2} | "
                        + $"{Per(g.Gt343SwallowHp, g.Gt343Swallows)}（{Per(g.Sw344HpPctSum, g.Gt343Swallows)}%） | {Per(g.Rp345NormalDealt + g.Rp345PunchDealt, z.N)} | {(n1 == 0 ? "—" : Per(g.Rp345PunchAmount, n1))} | {(g.Ga345Stomps == 0 ? "—" : Per(g.Ga345SplashDealt, z.N))} |");
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

    // ---------------------------------------------------------------------------------
    // §5 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        var b = Grid(rows, f => Apply(f, vers[0]));
        Console.WriteLine($"# {Tag} §5 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は段0 入りの規定）");
        Console.WriteLine();
        int last = nw - 1;
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちたセル | 主判定19行の第五波 | 第五波 95% 超の行（規定 → 版・新たに） |");
        Console.WriteLine("|---|---|--:|--:|---|");
        var grids = vers.Skip(1).Select(v => (v, g: Grid(rows, f => Apply(f, v)))).ToArray();
        int b95 = Enumerable.Range(0, rows.Length).Count(r => b[r, last] > 95);
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
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
            Console.WriteLine($"| {v.Name} | {upRows} ／ {upCells} | {down} | {F1(pr.Average(ri => b[ri, last]))} → {F1(pr.Average(ri => g[ri, last]))} | {b95} → {g95}（新たに {newly}） |");
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
