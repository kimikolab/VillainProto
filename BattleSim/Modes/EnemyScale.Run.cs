using System.Globalization;
using BattleCore;
using static Common;

// =====================================================================================
// escale run（第187期） —— §1-2 の 7 版を `compare` 61 行 × 5 波 × seed 0..199 で並べる。
// 版の切り替えは `BossRule.Scale`（Run の引数は増やしていない）。
// =====================================================================================

static partial class EnemyScaleDiag
{
    static readonly (string Tag, EnemyScaleRule R)[] Versions =
    {
        ("S0", new(100, 100)), ("S1", new(115, 115)), ("S2", new(130, 130)), ("S3", new(150, 150)),
        ("S4", new(175, 175)), ("H3", new(150, 100)), ("A3", new(100, 150)),
    };

    const int Seeds = 200;

    static double[] Rates(Formation f, EnemyScaleRule r)
    {
        var boss = new BossRule(false) { Scale = r };
        var w = new double[EnemyCatalog.Stages.Count];
        for (int st = 0; st < w.Length; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < Seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, boss: boss).PlayerWon) wins++;
            w[st] = Math.Round(100.0 * wins / Seeds, 1);
        }
        return w;
    }

    static (string Name, Formation F)[] BenchRows() => new[]
    {
        ("ハイパーキャリー（ソラ×ガン×ガルド＋ヒサ）", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Gan,
            center: UnitCatalog.Sora, back1: UnitCatalog.Dolga, back3: UnitCatalog.Hisa)),
        ("ヒサ×ムド×ザン×トメ（ヒサ中央）", Formation.Build(front1: UnitCatalog.Mudo, front3: UnitCatalog.Borg,
            center: UnitCatalog.Hisa, back1: UnitCatalog.Tome, back3: UnitCatalog.Zan)),
        ("ヒサ×ムド×ザン×トメ（ヒサ後列）", Formation.Build(front1: UnitCatalog.Mudo, front3: UnitCatalog.Tome,
            center: UnitCatalog.Zan, back1: UnitCatalog.Hisa, back3: UnitCatalog.Borg)),
        ("クグ×シガ", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Kugu, center: UnitCatalog.Shiga,
            back1: UnitCatalog.Borg, back3: UnitCatalog.Dolga)),
    };

    static bool Info(double x) => x > 0 && x < 100;

    /// <summary>
    /// 参考の刻み（**採否には使わない**。§1-2 の 7 版は測る前に固定した）。
    /// 山の頂上が S0 と S2 のあいだにあったので、つまみの値を選ぶ人のために 5% 刻みの要約だけを出す。
    /// </summary>
    static void Fine()
    {
        var rows = CompareBuilds();
        var prim = Baseline.PrimaryRows.Select(n => Array.FindIndex(rows, r => r.Name == n)).Where(i => i >= 0).ToArray();
        Console.WriteLine("# 第187期 `escale fine` —— 参考の刻み（**採否には使わない**）");
        Console.WriteLine();
        Console.WriteLine("| HP% / 攻% | 全61行 波平均 | 主判定19行 第五波 | 情報セル（2/3/4/5波） | 計 | 第五波 95% 超 | 0% のセル | 第2〜5波全部100%の行 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|");
        foreach (int p in new[] { 100, 105, 110, 115, 120, 125, 130 })
        {
            var r = new EnemyScaleRule(p, p);
            var t = new double[rows.Length][];
            Parallel.For(0, rows.Length, i => t[i] = Rates(rows[i].F, r));
            var perW = Enumerable.Range(1, 4).Select(w => t.Count(x => Info(x[w]))).ToArray();
            int zero = t.Sum(x => x.Skip(1).Count(y => y == 0));
            Console.WriteLine($"| {p} / {p} | {string.Join(" / ", Enumerable.Range(0, 5).Select(w => t.Average(x => x[w]).ToString("F1")))} | "
                              + $"{prim.Average(i => t[i][4]):F1} | {string.Join(" / ", perW)} | **{perW.Sum()}** | {t.Count(x => x[4] > 95)} | {zero} | "
                              + $"{t.Count(x => x.Skip(1).All(y => y == 100))} |");
        }
    }

    static void Sweep(string balancePath)
    {
        var rows = CompareBuilds();
        var bench = BenchRows();
        int nv = Versions.Length, nr = rows.Length, nb = bench.Length;
        var t = new double[nv][][];
        var tb = new double[nv][][];
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int v = 0; v < nv; v++)
        {
            t[v] = new double[nr][];
            tb[v] = new double[nb][];
            EnemyScaleRule r = Versions[v].R;
            Parallel.For(0, nr, i => t[v][i] = Rates(rows[i].F, r));
            Parallel.For(0, nb, i => tb[v][i] = Rates(bench[i].F, r));
        }

        Console.WriteLine("# 第187期 `escale run` —— 敵の数値の倍率 7 版（`compare` 61 行 × 5 波 × seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒（{nv} 版 × ({nr} ＋ 診断台 {nb}) 行 × 5 波 × {Seeds} seed）。");
        Console.WriteLine();

        // ---- 回帰: S0 が docs/balance.md と一致するか ----
        // 採用後は `docs/balance.md` が S1 になっているので、採用前の表（`git show <sha>:docs/balance.md`）を渡す。
        string bp = string.IsNullOrWhiteSpace(balancePath) ? "docs/balance.md" : balancePath.Trim();
        var bal = ReadBalance(bp);
        int diff = 0;
        for (int i = 0; i < nr; i++)
            for (int w = 0; w < 5; w++)
                if (!bal.TryGetValue(rows[i].Name, out var b) || Math.Abs(b[w] - t[0][i][w]) > 1e-9) diff++;
        Console.WriteLine($"**回帰（§4-1）: S0 と `{bp}` の差 {diff} セル / {nr * 5}**");
        Console.WriteLine();

        // ---- 版ごとの要約 ----
        var prim = Baseline.PrimaryRows.Select(n => Array.FindIndex(rows, r => r.Name == n)).Where(i => i >= 0).ToArray();
        var info = new int[nv];
        var over95 = new int[nv];
        var primW5 = new double[nv];
        Console.WriteLine("## 版ごとの要約（第2〜5波。情報セルは 0<x<100）");
        Console.WriteLine();
        Console.WriteLine("| 版 | HP% | 攻% | 全61行 波平均 | 主判定19行 波平均 | 情報セル（2/3/4/5波） | 計 | 第五波 95% 超 | 0% のセル | S0 に無い 0% | 100% のセル | 第2〜5波全部100%の行 |");
        Console.WriteLine("|---|--:|--:|---|---|---|--:|--:|--:|--:|--:|--:|");
        for (int v = 0; v < nv; v++)
        {
            var perW = Enumerable.Range(1, 4).Select(w => Enumerable.Range(0, nr).Count(i => Info(t[v][i][w]))).ToArray();
            info[v] = perW.Sum();
            over95[v] = Enumerable.Range(0, nr).Count(i => t[v][i][4] > 95);
            primW5[v] = prim.Average(i => t[v][i][4]);
            int zero = 0, newZero = 0, top = 0;
            for (int i = 0; i < nr; i++)
                for (int w = 1; w < 5; w++)
                {
                    if (t[v][i][w] == 0) { zero++; if (t[0][i][w] != 0) newZero++; }
                    if (t[v][i][w] == 100) top++;
                }
            int allTop = Enumerable.Range(0, nr).Count(i => t[v][i].Skip(1).All(x => x == 100));
            string avgAll = string.Join(" / ", Enumerable.Range(0, 5).Select(w => Enumerable.Range(0, nr).Average(i => t[v][i][w]).ToString("F1")));
            string avgPrim = string.Join(" / ", Enumerable.Range(0, 5).Select(w => prim.Average(i => t[v][i][w]).ToString("F1")));
            Console.WriteLine($"| {Versions[v].Tag} | {Versions[v].R.HpPercent} | {Versions[v].R.AtkPercent} | {avgAll} | {avgPrim} | "
                              + $"{string.Join(" / ", perW)} | **{info[v]}** | {over95[v]} | {zero} | {newZero} | {top} | {allTop} |");
        }
        Console.WriteLine();

        // ---- 採用基準（§3） ----
        Console.WriteLine("## 採用基準（§3）を当てる（S0〜S4 のみ）");
        Console.WriteLine();
        var cand = Enumerable.Range(0, 5).ToList();
        foreach (int v in cand)
            Console.WriteLine($"- {Versions[v].Tag}: 情報セル {info[v]} ／ 第五波 95% 超 {over95[v]} 行 ／ 主判定の第五波 {primW5[v]:F1}%"
                              + (primW5[v] < 40 ? " → **足切り（40% 未満）**" : ""));
        var alive = cand.Where(v => primW5[v] >= 40).ToList();
        if (alive.Count == 0) Console.WriteLine("- **足切りを通る版が無い**");
        else
        {
            int best = alive.Max(v => info[v]);
            var tie = alive.Where(v => best - info[v] <= 2).ToList();
            int minOver = tie.Min(v => over95[v]);
            var tie2 = tie.Where(v => over95[v] == minOver).ToList();
            int pick = tie2.OrderBy(v => Versions[v].R.HpPercent).First();
            Console.WriteLine($"- 基準1: 最大 {best}（足切りの内側）→ 2 セル以内の候補 {string.Join("・", tie.Select(v => Versions[v].Tag))}");
            Console.WriteLine($"- 基準2: 第五波 95% 超が最少 {minOver} 行 → {string.Join("・", tie2.Select(v => Versions[v].Tag))}");
            Console.WriteLine($"- 基準3: 低い方の倍率 → **{Versions[pick].Tag}（{Versions[pick].R.HpPercent} / {Versions[pick].R.AtkPercent}）**");
        }
        Console.WriteLine();

        // ---- H3 / A3 と S3 ----
        Console.WriteLine("## HP と攻撃のどちらが効くか（波別平均・全61行）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波 | 情報セル |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach (int v in new[] { 0, 5, 6, 3 })
        {
            var a = Enumerable.Range(1, 4).Select(w => Enumerable.Range(0, nr).Average(i => t[v][i][w])).ToArray();
            Console.WriteLine($"| {Versions[v].Tag} | {string.Join(" | ", a.Select(x => x.ToString("F1")))} | {a.Average():F1} | {info[v]} |");
        }
        Console.WriteLine();

        // ---- 転生した駒の在席行 ----
        Console.WriteLine("## 転生した駒の在席行（第2〜5波平均・括弧は S0 からの差 ／ S3 で失った割合 = (S0−S3)/S0）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 行 | " + string.Join(" | ", Versions.Select(x => x.Tag)) + " | S3 で失った割合 | A3−H3 |");
        Console.WriteLine("|---|--:|" + string.Concat(Versions.Select(_ => "--:|")) + "--:|--:|");
        var groups = Reborn.Select(u => (u.Name, Idx: Enumerable.Range(0, nr).Where(i => Has(rows[i].F, u.Id)).ToArray())).ToList();
        groups.Add(("（カド）", Enumerable.Range(0, nr).Where(i => Has(rows[i].F, "kado")).ToArray()));
        groups.Add(("（転生なし）", Enumerable.Range(0, nr).Where(i => !Reborn.Any(u => Has(rows[i].F, u.Id))).ToArray()));
        groups.Add(("（全61行）", Enumerable.Range(0, nr).ToArray()));
        foreach ((string nm, int[] idx) in groups)
        {
            if (idx.Length == 0) { Console.WriteLine($"| {nm} | 0 |" + string.Concat(Versions.Select(_ => " — |")) + " — | — |"); continue; }
            double M(int v) => idx.Average(i => t[v][i].Skip(1).Average());
            string cells = string.Join(" | ", Enumerable.Range(0, nv).Select(v => v == 0 ? M(0).ToString("F1") : $"{M(v):F1} ({M(v) - M(0):+0.0;-0.0})"));
            double lost = M(0) > 0 ? (M(0) - M(3)) / M(0) : 0;
            Console.WriteLine($"| {nm} | {idx.Length} | {cells} | {lost:P0} | {M(6) - M(5):+0.0;-0.0} |");
        }
        Console.WriteLine();

        // ---- 診断台 ----
        Console.WriteLine("## 診断台（第2〜5波平均・括弧は波別）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Versions.Select(x => x.Tag)) + " |");
        Console.WriteLine("|---|" + string.Concat(Versions.Select(_ => "---|")));
        for (int b = 0; b < nb; b++)
            Console.WriteLine($"| {bench[b].Name} | " + string.Join(" | ", Enumerable.Range(0, nv).Select(v =>
                $"{tb[v][b].Skip(1).Average():F1}（{string.Join("/", tb[v][b].Skip(1).Select(x => x.ToString("F0")))}）")) + " |");
        Console.WriteLine();

        // ---- 行ごとの表 ----
        Console.WriteLine("## 行ごと（第2〜5波平均 ／ 括弧は第2〜5波の情報セル数）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 主判定 | " + string.Join(" | ", Versions.Select(x => x.Tag)) + " |");
        Console.WriteLine("|---|:-:|" + string.Concat(Versions.Select(_ => "--:|")));
        for (int i = 0; i < nr; i++)
            Console.WriteLine($"| {rows[i].Name} | {(prim.Contains(i) ? "○" : "")} | " + string.Join(" | ", Enumerable.Range(0, nv).Select(v =>
                $"{t[v][i].Skip(1).Average():F1} ({t[v][i].Skip(1).Count(Info)})")) + " |");
        Console.WriteLine();

        // ---- 生の表（TSV・後で読み直す用） ----
        Console.WriteLine("## 生の表（TSV）");
        Console.WriteLine();
        Console.WriteLine("```");
        for (int v = 0; v < nv; v++)
            for (int i = 0; i < nr; i++)
                Console.WriteLine($"{Versions[v].Tag}\t{rows[i].Name}\t" + string.Join("\t", t[v][i].Select(x => x.ToString("F1", CultureInfo.InvariantCulture))));
        Console.WriteLine("```");
    }
}
