using BattleCore;
using static Common;
using I = Inv348Diag;

// db351 §5 —— 版の測定（代表台 ／ 軸の横並び ／ `compare`）。
static partial class Db351Diag
{
    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var keys = boards.SelectMany(b => vers.SelectMany(v => Waves.Select(w => (b, v, w)))).ToArray();
        var res = new Dictionary<(string, string, string), I.St>();
        foreach (var (b, v, w) in keys) res[(b.Name, v.Name, w.Name)] = I.Measure(Apply(b.F, v), w);
        I.St S(I.Board b, Ver v, I.W w) => res[(b.Name, v.Name, w.Name)];
        Console.WriteLine($"# {Tag} §5-2 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Ascii))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("版はワタ ／ ウケだけを差し替える（いなければ規定と同じ）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();
        Console.WriteLine("## 1. 代表台 × 波（勝率（規定との差）・決着T（ボスは倒しT）・倒れた ／戦）");
        Console.WriteLine();
        foreach (var b in boards)
        {
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
            foreach (var v in vers)
                Console.WriteLine($"| {v.Ascii} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var s = S(b, v, w); var z = S(b, vers[0], w);
                    string d = v == vers[0] || Math.Abs(s.Win - z.Win) < 0.05 ? "" : $"（{Sg(s.Win - z.Win)}）";
                    return $"{F1(s.Win)}{d}・{s.WinTs}T・倒 {Per2(s.AllyFell, s.N)}";
                })) + " |");
            Console.WriteLine();
        }
        Console.WriteLine("## 2. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ 勝率が規定から 15pt 以上落ちる台 ／ ボス 4T 未満）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var bad = new List<string>();
            foreach (var b in boards)
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
        Console.WriteLine("精鋭の 1T 決着は「勝った戦がすべて 1T」のときだけ拾う粗い判定（決着T の平均が 1.0）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 軸の横並び（第348期 §3）のデバフの行 × 版
    // ---------------------------------------------------------------------------------
    static void AxesAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var others = I.Boards().Where(b => b.Axis != "デバフ").ToArray();
        var mine = Boards().Where(b => b.Name is "呪いの台 1" or "呪いの台 ウケ入り" or "渡し (ワタ×ドハ)" or "弱体×破片 (ウケ×ネル×ウロ)").ToArray();
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
                + $"{Per(r[I.T1.Name].Dmg8, r[I.T1.Name].N)} | {Per(r[I.T9.Name].Dmg8, r[I.T9.Name].N)} | {F1(I.StrongWaves.Average(w => r[w.Name].Win))} | "
                + $"{Per2(r[I.Boss.Name].AllyFell, r[I.Boss.Name].N)} ／ {Per2(r[I.Bat.Name].AllyFell, r[I.Bat.Name].N)} ／ {Per2(r[I.M5.Name].AllyFell, r[I.M5.Name].N)} |";
        }
        Dictionary<string, I.St> M(Formation f) => aw.ToDictionary(w => w.Name, w => I.Measure(f, w));
        var oth = others.ToDictionary(b => b.Name, b => M(b.F));
        // 軸ごとの1枚（台の良いほう）と、デバフの行 × 版の各物差しでの順位
        var axes = others.GroupBy(b => b.Axis).ToDictionary(g => g.Key, g => Enumerable.Range(0, 11).Select(i => g.Max(b => Vals(oth[b.Name])[i])).ToArray());
        Console.WriteLine($"# {Tag} §5-3 軸の横並び（第348期 §3 の物差し）—— デバフの行を版ごとに・seed 0..{I.Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("| 軸 ／ 台 ／ 版 | ボス 勝率・T | 近衛 | 大隊 | 本編2 | 本編4 | 本編5 | 九体 | 的・一 8T | 的・九 8T | 強 2〜5 | 倒れた（ボス ／ 大隊 ／ 第5波） |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|---|--:|--:|--:|---|");
        foreach (var b in others) Console.WriteLine(Line($"{b.Axis}: {b.Name}", oth[b.Name]));
        var deb = new List<(string Label, double[] V)>();
        foreach (var b in mine)
            foreach (var v in Vers)
            {
                if (v.Ascii == "r" && !b.F.Occupied().Any(o => o.Def.Id == "uke")) continue;
                if (v.Ascii is "a" or "x" or "u" && !b.F.Occupied().Any(o => o.Def.Id == "wata")) continue;
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
            Console.WriteLine($"| {label} | " + string.Join(" | ", Enumerable.Range(0, cols.Length).Select(i => $"{1 + axes.Values.Count(a => a[i] > v[i])}"))+ " |");
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
        var b = Grid(rows, f => Apply(f, vers[0]));   // 第352期: 規定の列は第351期の規定（`WataH351`）に固定
        bool Has(Formation f) => f.Occupied().Any(o => o.Def.Id is "wata" or "uke");
        Console.WriteLine($"# {Tag} §5-4 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"規定の列は `docs/balance.md` と同じ帯。ワタ ／ ウケのいる行は {rows.Count(r => Has(r.F))} 行（{string.Join("・", rows.Where(r => Has(r.F)).Select(r => r.Name))}）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | うちワタ ／ ウケのいない行 | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
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
                if ((u || d) && !Has(rows[r].F)) other++;
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

    // ---------------------------------------------------------------------------------
    // 第352期 §3: 規定化の確かめ（規定のワタ ＝ 第351期の WT-u）
    // ---------------------------------------------------------------------------------
    static void Settle352()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        I.W[] ws = { I.Boss, I.Guard, I.Bat, I.M2, I.StrongWaves[2] };
        Console.WriteLine($"# 第352期 §3 規定化の確かめ —— 「{PlayRow}」（{Seats(Top1)}・規定のワタ）× 波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("| 波 | 規定（第352期） | 第351期の WT-u（旧に版を差した写し） | 第351期の規定（旧） | 一致 |");
        Console.WriteLine("|---|---|---|---|:-:|");
        foreach (var w in ws)
        {
            var n = I.Measure(Top1, w); var u = I.Measure(Apply(Top1, V("u")), w); var o = I.Measure(Apply(Top1, Vers[0]), w);
            string C(I.St x) => $"{F1(x.Win)}・{x.WinTs}T・倒 {Per2(x.AllyFell, x.N)}";
            Console.WriteLine($"| {w.Name} | {C(n)} | {C(u)} | {C(o)} | {(C(n) == C(u) ? "○" : "×")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
