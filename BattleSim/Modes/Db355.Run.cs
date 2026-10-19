using BattleCore;
using static Common;
using I = Inv348Diag;

// db355 §6 —— 版の測定（代表台 × 波 × 版 ／ ボスへの届き方 ／ 軸の横並び ／ `compare`）。
static partial class Db355Diag
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
        Console.WriteLine("版はネル ／ ウツだけを差し替える（その駒がいない台では回さない）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();

        // ---- 1. ボスへの届き方 ----
        Console.WriteLine("## 1. ボスへの届き方（代表台 × ボス × 版）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率・倒しT | 全滅 T | 負けた戦の最後の最大HP 平均 ／ 最小 | 最も減った HP 平均 | 削った最大HP ／戦（誰の一撃で） | 与ダメ ／T | ボスの回復 ／T |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|--:|--:|");
        foreach (var b in boards)
            foreach (var v in vers.Where(v => Applies(b, v)))
            {
                var a = S(b, v, I.Boss);
                long n = 0, al = 0, he = 0;
                for (int t = 1; t <= TrajT; t++) if (a.TAll[t] > 0) { n += a.TCnt[t]; al += a.TAll[t]; he += a.THeal[t]; }
                Console.WriteLine($"| {b.Name} | {v.Ascii} | {F1(a.Win)}・{a.WinTs}T | {Per(a.WipeT, a.Lost)} | {Per(a.LostMaxEnd, a.Lost)} ／ {(a.Lost == 0 ? "—" : a.LostMaxEndMin.ToString())} | {Per(a.LostLow, a.Lost)} | "
                    + $"{Per(a.ScarAll, a.N)}（{Kv(a.ScarBy, a.N)}） | {Per(al, n)} | {Per(he, n)} |");
            }
        Console.WriteLine();
        foreach (var v in vers.Where(v => Applies(boards[0], v))) PrintTraj($"{boards[0].Name} × ボス × {v.Ascii}", S(boards[0], v, I.Boss));

        // ---- 削った最大HP（精鋭 ／ 本編） ----
        Console.WriteLine("## 1-2. 削った最大HP（敵の側・1戦あたり・誰の一撃で）");
        Console.WriteLine();
        Console.WriteLine("| 台 × 波 | " + string.Join(" | ", vers.Skip(1).Select(v => v.Ascii)) + " |");
        Console.WriteLine("|---|" + string.Concat(vers.Skip(1).Select(_ => "---|")));
        foreach (var b in boards)
            foreach (var w in Waves)
                Console.WriteLine($"| {b.Name} × {w.Name} | " + string.Join(" | ", vers.Skip(1).Select(v => Applies(b, v) ? $"{Per(S(b, v, w).ScarAll, S(b, v, w).N)}（{Kv(S(b, v, w).ScarBy, S(b, v, w).N)}）" : "—")) + " |");
        Console.WriteLine();

        // ---- 2. 勝率と決着T ----
        Console.WriteLine("## 2. 代表台 × 波（勝率（規定との差）・決着T（ボスは倒しT）・倒れた ／戦）");
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
    // 軸の横並び（第348期 §3）のデバフの行 × 版（`db351 axes` と同じ物差し）
    // ---------------------------------------------------------------------------------
    static void AxesAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var others = I.Boards().Where(b => b.Axis != "デバフ").ToArray();
        var mine = new[]
        {
            new I.Board("デバフ", PlayRow, Play),
            new I.Board("デバフ", "弱体×破片 (ウケ×ネル×ウロ)", Row("弱体×破片 (ウケ×ネル×ウロ)")),
            new I.Board("デバフ", "渡し (ワタ×ドハ)", Row("渡し (ワタ×ドハ)")),
        };
        I.W[] aw = I.AxisWaves;
        double[] Vals(Dictionary<string, I.St> r) => new[]
        {
            r[I.Boss.Name].Win, r[I.Boss.Name].Wins == 0 ? double.NegativeInfinity : -(double)r[I.Boss.Name].WinT / r[I.Boss.Name].Wins,
            r[I.Guard.Name].Win, r[I.Bat.Name].Win, r[I.M2.Name].Win, r[I.M4.Name].Win, r[I.M5.Name].Win, r[I.Nine.Name].Win,
            (double)r[I.T1.Name].Dmg8 / r[I.T1.Name].N, (double)r[I.T9.Name].Dmg8 / r[I.T9.Name].N, I.StrongWaves.Average(w => r[w.Name].Win),
        };
        string Line(string label, Dictionary<string, I.St> r)
        {
            string WT(I.W w) => $"{F1(r[w.Name].Win)}・{r[w.Name].WinTs}T";
            return $"| {label} | {WT(I.Boss)} | {WT(I.Guard)} | {WT(I.Bat)} | {F1(r[I.M2.Name].Win)} | {F1(r[I.M4.Name].Win)} | {F1(r[I.M5.Name].Win)} | {WT(I.Nine)} | "
                + $"{Per(r[I.T1.Name].Dmg8, r[I.T1.Name].N)} | {Per(r[I.T9.Name].Dmg8, r[I.T9.Name].N)} | {F1(I.StrongWaves.Average(w => r[w.Name].Win))} |";
        }
        Dictionary<string, I.St> M(Formation f) => aw.ToDictionary(w => w.Name, w => I.Measure(f, w));
        var oth = others.ToDictionary(b => b.Name, b => M(b.F));
        var axes = others.GroupBy(b => b.Axis).ToDictionary(g => g.Key, g => Enumerable.Range(0, 11).Select(i => g.Max(b => Vals(oth[b.Name])[i])).ToArray());
        Console.WriteLine($"# {Tag} §6-3 軸の横並び（第348期 §3 の物差し）—— デバフの行を版ごとに・seed 0..{I.Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("ほかの軸の行は `inv348` の台（第349期までの規定の燃焼の3枚・古い口の終端でウツ ／ ムド ／ ワタも旧）。デバフの行はいまの規定（段0 入り）の上に版を差す。");
        Console.WriteLine();
        Console.WriteLine("| 軸 ／ 台 ／ 版 | ボス 勝率・T | 近衛 | 大隊 | 本編2 | 本編4 | 本編5 | 九体 | 的・一 8T | 的・九 8T | 強 2〜5 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|---|--:|--:|--:|");
        foreach (var b in others) Console.WriteLine(Line($"{b.Axis}: {b.Name}", oth[b.Name]));
        var deb = new List<(string Label, double[] V)>();
        foreach (var b in mine)
            foreach (var v in Vers.Where(v => Applies(b, v)))
            {
                var r = M(Apply(b.F, v));
                Console.WriteLine(Line($"**デバフ: {b.Name} × {v.Ascii}**", r));
                deb.Add(($"{b.Name} × {v.Ascii}", Vals(r)));
            }
        Console.WriteLine();
        string[] cols = { "ボス 勝率", "ボス 倒しT", "近衛", "大隊", "本編2", "本編4", "本編5", "九体", "的・一", "的・九", "強 2〜5" };
        Console.WriteLine($"## 物差しごとの順位（ほかの軸 {axes.Count} 本の1枚 ＋ その行。同じ値は上の順位）");
        Console.WriteLine();
        Console.WriteLine("| デバフの行 | " + string.Join(" | ", cols) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "--:|")));
        foreach (var (label, v) in deb)
            Console.WriteLine($"| {label} | " + string.Join(" | ", Enumerable.Range(0, cols.Length).Select(i => $"{1 + axes.Values.Count(a => a[i] > v[i])}")) + " |");
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
        var b354 = Grid(rows, Pin354);   // 段0 の前（第354期までの規定のムド ／ ワタ）
        bool HasN(Formation f) => f.Occupied().Any(o => o.Def.Id is "nel" or "utsu");
        Console.WriteLine($"# {Tag} §6-4 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定 ＝ 段0 入り）");
        Console.WriteLine();
        Console.WriteLine($"ネル ／ ウツのいる行は {rows.Count(r => HasN(r.F))} 行（{string.Join("・", rows.Where(r => HasN(r.F)).Select(r => r.Name))}）。");
        Console.WriteLine();
        var moved = new List<string>();
        for (int r = 0; r < rows.Length; r++) for (int w = 0; w < nw; w++) if (b[r, w] != b354[r, w]) moved.Add($"{rows[r].Name} 第{w + 1}波 {F1(b354[r, w])} → {F1(b[r, w])}");
        Console.WriteLine($"段0（ムドの M1＋M2 ／ ワタの配りを規定に）: 第354期までの規定とのセルの差 **{moved.Count}**{(moved.Count == 0 ? "" : "（" + string.Join("・", moved) + "）")}。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | うちネル ／ ウツのいない行 | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|---|--:|---|");
        var grids = vers.Skip(1).Select(v => (v.Name, g: Grid(rows, f => Apply(f, v)))).ToArray();
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] g) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => g[r, w]))));
        Console.WriteLine($"| 規定 | — | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} |");
        foreach (var (name, g) in grids)
        {
            int upR = 0, upC = 0, dnR = 0, dnC = 0, other = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                bool u = false, d = false;
                for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
                if (u) upR++; if (d) dnR++;
                if ((u || d) && !HasN(rows[r].F)) other++;
            }
            Console.WriteLine($"| {name} | {upR} ／ {upC} | {dnR} ／ {dnC} | {other} | {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} |");
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
