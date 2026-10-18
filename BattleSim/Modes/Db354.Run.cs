using BattleCore;
using static Common;
using I = Inv348Diag;

// db354 §6 —— 版の測定（代表台 × 波 × 版 ／ ボスへの届き方 ／ ムドの働き ／ `compare`）。
static partial class Db354Diag
{
    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var keys = boards.SelectMany(b => vers.Where(v => Applies(b, v)).SelectMany(v => Waves.Select(w => (b, v, w)))).ToArray();
        var res = new Dictionary<(string, string, string), Fx>();
        foreach (var (b, v, w) in keys) res[(b.Name, v.Ascii, w.Name)] = Measure(Apply(b.F, v), w);
        Fx S(I.Board b, Ver v, I.W w) => res[(b.Name, v.Ascii, w.Name)];
        Console.WriteLine($"# {Tag} §6 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Ascii))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("版はムド ／ ワタだけを差し替える（その駒がいない台では回さない）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();

        // ---- 1. ボスへの届き方 ----
        Console.WriteLine("## 1. ボスへの届き方（代表台 × ボス × 版）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率・倒しT | 負けた戦で最も減った HP 平均 ／ 最小 | 与ダメ ／T（ウツ ／ ムド ／ ほか） | ボスの回復 ／T | 1,380 ／T に対して |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|");
        foreach (var b in boards)
            foreach (var v in vers.Where(v => Applies(b, v)))
            {
                var a = S(b, v, I.Boss);
                long n = 0, al = 0, ut = 0, mu = 0, he = 0;
                for (int t = 1; t <= TrajT; t++) if (a.TAll[t] > 0) { n += a.TCnt[t]; al += a.TAll[t]; ut += a.TUtsu[t]; mu += a.TMudo[t]; he += a.THeal[t]; }
                Console.WriteLine($"| {b.Name} | {v.Ascii} | {F1(a.Win)}・{a.WinTs}T | {Per(a.LostBossLow, a.Lost)} ／ {(a.Lost == 0 ? "—" : a.LostBossLowMin.ToString())} | "
                    + $"{Per(al, n)}（{Per(ut, n)} ／ {Per(mu, n)} ／ {Per(al - ut - mu, n)}） | {Per(he, n)} | {(n == 0 ? "—" : F1(100.0 * al / n / 1380) + "%")} |");
            }
        Console.WriteLine();
        foreach (var v in vers) PrintTraj($"{boards[0].Name} × ボス × {v.Ascii}", S(boards[0], v, I.Boss));

        // ---- 2. ムドの働き ----
        Console.WriteLine("## 2. ムドの働き（1戦あたり）");
        Console.WriteLine();
        MudoHead();
        foreach (var b in boards.Where(b => Has(b.F, "mudo")))
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M5, I.StrongWaves[2] })
                foreach (var v in vers.Where(v => Applies(b, v)))
                    MudoRow($"{b.Name} × {w.Name} × {v.Ascii}", S(b, v, w));
        Console.WriteLine();

        // ---- 3. 勝率と決着T ----
        Console.WriteLine("## 3. 代表台 × 波（勝率（規定との差）・決着T（ボスは倒しT）・倒れた ／戦）");
        Console.WriteLine();
        foreach (var b in boards)
        {
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
            foreach (var v in vers.Where(v => Applies(b, v)))
                Console.WriteLine($"| {v.Ascii} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var s = S(b, v, w); var z = S(b, vers[0], w);
                    string d = v == vers[0] || Math.Abs(s.Win - z.Win) < 0.05 ? "" : $"（{Sg(s.Win - z.Win)}）";
                    return $"{F1(s.Win)}{d}・{s.WinTs}T・倒 {Per2(s.Fell, s.N)}";
                })) + " |");
            Console.WriteLine();
        }

        // ---- 5. 壊れの兆し ----
        Console.WriteLine("## 5. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ 勝率が規定から 15pt 以上落ちる台 ／ ボス 4T 未満）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var bad = new List<string>();
            foreach (var b in boards.Where(b => Applies(b, v)))
                foreach (var w in Waves)
                {
                    var s = S(b, v, w); var z = S(b, vers[0], w);
                    if ((w == I.Guard || w == I.Bat) && s.Wins > 0 && s.WinT == s.Wins && 100.0 * s.Wins / s.N >= 5) bad.Add($"{b.Name} × {w.Name} 1T 決着");
                    if (s.Win - z.Win <= -15) bad.Add($"{b.Name} × {w.Name} {F1(z.Win)} → {F1(s.Win)}");
                    if (w == I.Boss && s.Wins > 0 && (double)s.WinT / s.Wins < 4.0) bad.Add($"{b.Name} × ボス {s.WinTs}T");
                }
            Console.WriteLine($"- **{v.Name}**: {(bad.Count == 0 ? "なし" : string.Join("・", bad))}");
        }
        Console.WriteLine();
        Console.WriteLine("精鋭の 1T 決着は「勝った戦がすべて 1T」のときだけ拾う粗い判定（第351期と同じ）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` × 版
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

    static void CompareAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        var b = Grid(rows, f => Apply(f, vers[0]));
        var b353 = Grid(rows, Pin353);   // 段0 の前（第353期までの規定のウツ）
        bool HasM(Formation f) => f.Occupied().Any(o => o.Def.Id is "mudo" or "wata");
        bool Deb(string n) => n.Contains("ウツ") || n.Contains("ワタ") || n.Contains("ネル") || n.Contains("ウケ") || n.Contains("逆しま") || n.Contains("弱体");
        Console.WriteLine($"# {Tag} §6-4 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定 ＝ 段0 入り）");
        Console.WriteLine();
        Console.WriteLine($"ムド ／ ワタのいる行は {rows.Count(r => HasM(r.F))} 行（{string.Join("・", rows.Where(r => HasM(r.F)).Select(r => r.Name))}）。");
        Console.WriteLine();
        int d0 = 0; for (int r = 0; r < rows.Length; r++) for (int w = 0; w < nw; w++) if (b[r, w] != b353[r, w]) d0++;
        Console.WriteLine($"段0（UT-o を規定に）: 第353期までの規定のウツとのセルの差 **{d0}**。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | うちデバフ以外の行（名前に ウツ ／ ワタ ／ ネル ／ ウケ ／ 逆しま ／ 弱体 が無い） | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|---|--:|---|");
        var grids = vers.Skip(1).Select(v => (v.Name, g: Grid(rows, f => Apply(f, v)))).ToArray();
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] g) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => g[r, w]))));
        Console.WriteLine($"| 規定 | — | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} |");
        foreach (var (name, g) in grids)
        {
            int upR = 0, upC = 0, dnR = 0, dnC = 0; var other = new List<string>();
            for (int r = 0; r < rows.Length; r++)
            {
                bool u = false, d = false;
                for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
                if (u) upR++; if (d) dnR++;
                if ((u || d) && !Deb(rows[r].Name)) other.Add(rows[r].Name);
            }
            Console.WriteLine($"| {name} | {upR} ／ {upC} | {dnR} ／ {dnC} | {other.Count}（{string.Join("・", other)}） | {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} |");
        }
        foreach (var (name, g) in grids)
        {
            Console.WriteLine();
            Console.WriteLine($"## {name}");
            Console.WriteLine();
            Console.WriteLine("| 行 | 第2波 | 第3波 | 第4波 | 第5波 |");
            Console.WriteLine("|---|---|---|---|---|");
            for (int r = 0; r < rows.Length; r++)
            {
                if (Enumerable.Range(1, nw - 1).All(w => g[r, w] == b[r, w])) continue;
                Console.WriteLine($"| {rows[r].Name} | " + string.Join(" | ", Enumerable.Range(1, nw - 1).Select(w => g[r, w] == b[r, w] ? F1(b[r, w]) : $"{F1(b[r, w])} → **{F1(g[r, w])}**")) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
