using BattleCore;
using static Common;
using I = Inv348Diag;

// db353 §5 —— 版の測定（代表台 × 波 × 版 ／ ボスへの届き方 ／ 呪いの流れ ／ `compare`）。
static partial class Db353Diag
{
    static bool Applies(I.Board b, Ver v) => (v != RefUKr || b.Name == "ウケ入りの台") && Applies0(b, v);
    static bool Applies0(I.Board b, Ver v) =>
        (v.Uke == UnitCatalog.Uke || b.F.Occupied().Any(o => o.Def.Id == "uke")) && (v.Utsu == UnitCatalog.UtsuH353 || b.F.Occupied().Any(o => o.Def.Id == "utsu"));

    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var keys = boards.SelectMany(b => vers.Where(v => Applies(b, v)).SelectMany(v => Waves.Select(w => (b, v, w)))).ToArray();
        var res = new Dictionary<(string, string, string), Fx>();
        foreach (var (b, v, w) in keys) res[(b.Name, v.Ascii, w.Name)] = Measure(Apply(b.F, v), w);
        Fx S(I.Board b, Ver v, I.W w) => res[(b.Name, v.Ascii, w.Name)];
        Console.WriteLine($"# {Tag} §5 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Ascii))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("版はウツ ／ ウケだけを差し替える（その駒がいない台では回さない）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();

        // ---- 1. ボスへの届き方 ----
        Console.WriteLine("## 1. ボスへの届き方（代表台 × ボス × 版）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率・倒しT | 負けた戦の最後のボスの HP 平均 ／ 最小（戦の中で最も減った HP の平均） | ウツの与ダメ ／戦 | ウケの与ダメ ／戦 | T1〜T12 の味方の与ダメ ／T（ボスの回復 ／T） |");
        Console.WriteLine("|---|---|---|---|--:|--:|---|");
        foreach (var b in boards)
            foreach (var v in vers.Where(v => Applies(b, v)))
            {
                var a = S(b, v, I.Boss);
                long n = a.TCnt.Sum(), al = a.TAlly.Sum(), he = a.THeal.Sum();
                Console.WriteLine($"| {b.Name} | {v.Ascii} | {F1(a.Win)}・{a.WinTs}T | {Per(a.LostBossHp, a.Lost)} ／ {(a.Lost == 0 ? "—" : a.LostBossHpMin.ToString())}（{Per(a.LostBossLow, a.Lost)}）| {Per(a.UtsuDealt, a.UtsuN)} | {Per(a.UkeDealt, a.UkeN)} | {Per(al, n)}（{Per(he, n)}） |");
            }
        Console.WriteLine();
        foreach (var b in boards.Take(2))
            foreach (var v in vers.Where(v => Applies(b, v)))
                PrintTraj($"{b.Name} × ボス × {v.Ascii}", S(b, v, I.Boss));

        // ---- 2. 呪いの流れ ----
        Console.WriteLine("## 2. 呪いの流れ（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("ウツの下げ幅 ＝ `-AtkBonus` の戦ごとの最大。上限超え ＝ 下げ幅 − 12（0 未満は 0）の平均 ／ 超えた戦の割合。ウケ: 奪った量（うちボスから）／ 奪えた回数 ／ 奪える分が無かった回数 ／ 累計 0 の敵を殴った回数（−1）／ 奪った相手（敵ごとの量）。");
        Console.WriteLine();
        Console.WriteLine("| 台 × 波 | 版 | ウツ 下げ幅 平均 | 上限超え 平均 ／ 割合 | ウツ 与ダメ | ウケ 奪った（ボス）| 奪えた ／ 空 ／ −1（回） | 奪った相手 | ウケ 与ダメ |");
        Console.WriteLine("|---|---|--:|---|--:|---|---|---|--:|");
        foreach (var b in boards)
            foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M5 })
                foreach (var v in vers.Where(v => Applies(b, v)))
                {
                    var a = S(b, v, w);
                    string from = string.Join("、", a.PlFrom.OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Key} {Per(kv.Value, a.N)}"));
                    Console.WriteLine($"| {b.Name} × {w.Name} | {v.Ascii} | {Per(a.UtsuMax, a.UtsuN)} | {Per(a.UtsuOver, a.UtsuN)} ／ {Pct(a.UtsuOverN, a.UtsuN)}% | {Per(a.UtsuDealt, a.UtsuN)} | "
                        + (a.UkeN == 0 ? "— | — | — | — |" : $"{Per(a.PlGain, a.N)}（{Per(a.PlBoss, a.N)}）| {Per2(a.PlHits, a.N)} ／ {Per2(a.PlDry, a.N)} ／ {Per2(a.PlMiss, a.N)} | {from} | {Per(a.UkeDealt, a.UkeN)} |"));
                }
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
        foreach (var v in vers.Skip(1).Where(v => v != RefUKr))
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
        var rows = CompareBuilds().Select(r => (r.Name, F: Pin354(r.F))).ToArray();   // 第356期: ムド ／ ワタ ／ ネルは第353期の規定に固定（第355期 ／ 第356期に規定が動いた）
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        var b = Grid(rows, f => Apply(f, vers[0]));
        bool Has(Formation f) => f.Occupied().Any(o => o.Def.Id is "utsu" or "uke");
        Console.WriteLine($"# {Tag} §5-4 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"規定の列は `docs/balance.md` と同じ帯。ウツ ／ ウケのいる行は {rows.Count(r => Has(r.F))} 行（{string.Join("・", rows.Where(r => Has(r.F)).Select(r => r.Name))}）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | うちウツ ／ ウケのいない行 | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
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
}
