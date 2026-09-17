using BattleCore;
using static Common;

// =====================================================================================
// spread モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "spread")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 spread
// =====================================================================================

static class SpreadDiag
{
public static void Run(string[] args, int stageIndex)
{
    var spreadBuilds = CompareBuilds();
    // 第3引数に除外語（カンマ区切りの部分一致）を渡すと、その行を外して測る。
    // **行を足した期に「同じ行数で前後を測り直す」ためだけの窓口**（CLAUDE.md の
    // 「計測器と測定対象を同時に動かさない」）。省略すれば従来どおり全行。
    string spreadDrop = args.Length > 2 ? args[2] : "";
    if (spreadDrop.Length > 0)
        spreadBuilds = spreadBuilds
            .Where(b => !spreadDrop.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();
    IReadOnlyList<EnemyCatalog.Stage> spreadStages = EnemyCatalog.Stages;
    const int SpreadSeeds = 200;   // compare と同じ。数字を突き合わせるので変えない

    int nb = spreadBuilds.Length, nw = spreadStages.Count;

    // rate[波][編成] = 勝率(%)。compare と同じ計算（同じ seed 帯・同じ Run）なので
    // docs/balance.md の表とセルが一致する。ずれたらどちらかの集計が間違っている。
    var rate = new double[nw][];
    for (int w = 0; w < nw; w++)
    {
        rate[w] = new double[nb];
        for (int b = 0; b < nb; b++)
        {
            int wins = 0;
            for (int seed = 0; seed < SpreadSeeds; seed++)
                if (BattleEngine.Run(spreadBuilds[b].F, spreadStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
            rate[w][b] = wins * 100.0 / SpreadSeeds;
        }
    }

    Console.WriteLine("# 波の分離度（spread）");
    Console.WriteLine();
    Console.WriteLine($"代表編成 {nb} × 全 {nw} 波、seed 0..{SpreadSeeds - 1} の {SpreadSeeds} 試行。");
    Console.WriteLine("compare と同じ計算なので、セルは docs/balance.md と一致する。");
    Console.WriteLine();

    Console.WriteLine("## 1. 波ごとの飽和");
    Console.WriteLine();
    Console.WriteLine("| 波 | 平均 | 100%の編成 | 0%の編成 | 中間帯(5〜95%) | 標準偏差 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    var sd = new double[nw];
    for (int w = 0; w < nw; w++)
    {
        double[] v = rate[w];
        double mean = v.Average();
        sd[w] = Math.Sqrt(v.Select(x => (x - mean) * (x - mean)).Sum() / v.Length);
        int top = v.Count(x => x >= 100.0), bottom = v.Count(x => x <= 0.0);
        int mid = v.Count(x => x > 5.0 && x < 95.0);
        Console.WriteLine($"| 第{w + 1}波 | {mean:F1} | {top} / {nb} | {bottom} | {mid} | {sd[w]:F1} |");
    }
    Console.WriteLine();
    int allTop = Enumerable.Range(0, nb).Count(b => Enumerable.Range(1, Math.Max(0, nw - 2)).All(w => rate[w][b] >= 100.0));
    Console.WriteLine($"第2〜{nw - 1}波すべて 100% の編成: **{allTop} / {nb}**"
                    + "（この編成たちにとって、中間の波は存在しないのと同じ）");
    Console.WriteLine();

    Console.WriteLine("## 2. 波間の相関");
    Console.WriteLine();
    Console.WriteLine("編成ごとの勝率を波の間で相関させる。高いほど「同じ資源に課金している」。");
    Console.WriteLine("分散 0 の波（全編成が同じ勝率）は相関が定義できないので `—`。");
    Console.WriteLine();
    Console.WriteLine("| |" + string.Concat(Enumerable.Range(1, nw - 1).Select(w => $" 第{w + 1}波 |")));
    Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, nw - 1).Select(_ => "--:|")));
    for (int i = 0; i < nw - 1; i++)
    {
        var cells = new List<string>();
        for (int j = 1; j < nw; j++)
        {
            if (j <= i) { cells.Add(" |"); continue; }   // 下三角は空欄（対称なので上だけ出す）
            double r = Corr(rate[i], rate[j]);
            cells.Add(double.IsNaN(r) ? " — |" : $" {r:+0.00;-0.00} |");
        }
        Console.WriteLine($"| **第{i + 1}波** |" + string.Concat(cells));
    }
    Console.WriteLine();

    Console.WriteLine("## 3. 固有の勝者・敗者");
    Console.WriteLine();
    Console.WriteLine("**固有の勝者** = その波でだけ 100%（他のどの波でも 100% 未満）の編成。");
    Console.WriteLine("**固有の敗者** = その波でだけ 0%（他のどの波でも 0% 超）の編成。");
    Console.WriteLine("両方とも空の波は、独立した波として存在していない。");
    Console.WriteLine();
    Console.WriteLine("**第一波は比較から外してある。** チュートリアル波として全編成 100% を意図的に");
    Console.WriteLine("維持しているので、比較に入れると第2〜5波の固有の勝者が**恒等的に 0** になる");
    Console.WriteLine("——第三波を何に作り替えても動かない指標だった。第一波自身も判定しない。");
    Console.WriteLine();
    for (int w = 0; w < nw; w++)
    {
        Console.WriteLine($"### 第{w + 1}波");
        Console.WriteLine();
        if (w == 0)
        {
            // 第一波は全編成 100%。「他のどの波でも 100% 未満」を要求する判定の比較対象に
            // 入れると、第2〜5波の固有の勝者が恒等的に 0 になる（第20期 逆位の副産物）。
            // ここを直さずに波を作り替えると、主判定が到達不能なまま前後比較をすることになる。
            Console.WriteLine("- （比較対象外。全編成 100% のチュートリアル波）");
            Console.WriteLine();
            continue;
        }

        var winners = new List<string>();
        var losers = new List<string>();
        for (int b = 0; b < nb; b++)
        {
            bool onlyTop = rate[w][b] >= 100.0
                        && Enumerable.Range(1, nw - 1).All(o => o == w || rate[o][b] < 100.0);
            bool onlyBottom = rate[w][b] <= 0.0
                           && Enumerable.Range(1, nw - 1).All(o => o == w || rate[o][b] > 0.0);
            if (onlyTop) winners.Add(spreadBuilds[b].Name);
            if (onlyBottom) losers.Add(spreadBuilds[b].Name);
        }
        Console.WriteLine($"- 固有の勝者 ({winners.Count}): " + (winners.Count == 0 ? "**なし**" : string.Join(" / ", winners)));
        Console.WriteLine($"- 固有の敗者 ({losers.Count}): " + (losers.Count == 0 ? "**なし**" : string.Join(" / ", losers)));
        Console.WriteLine();
    }

    // ---- 4. 主判定の固定行集合（第60期に確定）---------------------------------------
    Console.WriteLine($"## 4. 主判定 {Baseline.PrimaryRows.Length} 行（歯止めはこの集合の上で測る）");
    Console.WriteLine();
    Console.WriteLine("**全行平均は行を足すたびに勝手に動く量である。** 第41〜59期の「歯止めを割った」は");
    Console.WriteLine("すべて分母の話で、波そのものは第40期から1つも動いていない（第59期 9-4 → 第60期に確定）。");
    Console.WriteLine("主判定は**軸の被覆**で選んだ固定集合なので、新機構の行を足しても分母が動かない。");
    Console.WriteLine();
    Console.WriteLine("**情報セルの定義はここだけ第59期 9-1 に揃えてある**——");
    Console.WriteLine("`0 < x < 100` を**第2〜5波**で数える（§1 の中間帯は `5 < x < 95`。別の量なので混ぜない）。");
    Console.WriteLine();
    var primary = Baseline.PrimaryRows
        .Select(n => Array.FindIndex(spreadBuilds, b => b.Name == n))
        .Where(i => i >= 0).ToArray();
    var missing = Baseline.PrimaryRows.Where(n => !spreadBuilds.Any(b => b.Name == n)).ToArray();
    if (missing.Length > 0)
    {
        Console.WriteLine($"**警告: 主判定行のうち {missing.Length} 行が見つからない** —— "
                        + string.Join(" / ", missing));
        Console.WriteLine("（`CompareBuilds()` の行名を変えたら `Baseline.PrimaryRows` も直すこと）");
        Console.WriteLine();
    }
    int Info59(int b) => Enumerable.Range(1, nw - 1).Count(w => rate[w][b] > 0.0 && rate[w][b] < 100.0);
    Console.WriteLine("| # | 行 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 情報セル |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|");
    for (int k = 0; k < primary.Length; k++)
    {
        int b = primary[k];
        Console.WriteLine($"| {k + 1} | {spreadBuilds[b].Name} "
                        + string.Concat(Enumerable.Range(0, nw).Select(w => $"| {rate[w][b]:0.0}% "))
                        + $"| **{Info59(b)}** |");
    }
    Console.WriteLine();
    string Fold(string tag, IEnumerable<int> idx)
    {
        int[] a = idx.ToArray();
        int info = a.Sum(Info59);
        return $"| {tag} " + string.Concat(Enumerable.Range(0, nw)
                 .Select(w => $"| {a.Average(b => rate[w][b]):0.0}% "))
             + $"| {info} / {info / (double)a.Length:0.00} |";
    }
    Console.WriteLine("| 分母 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 情報セル 合計 / 平均 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
    Console.WriteLine(Fold($"**主判定 {primary.Length} 行**", primary));
    Console.WriteLine(Fold($"全 {nb} 行", Enumerable.Range(0, nb)));
    Console.WriteLine();
    double fifth = primary.Average(b => rate[nw - 1][b]);
    Console.WriteLine($"**第五波: 主判定 {fifth:0.0}% / 全 {nb} 行 {rate[nw - 1].Average():0.0}%**");
    Console.WriteLine($"**歯止め: 主判定 {Baseline.PrimaryFifthFloor:0.0}%** "
                    + $"——確定時（第60期）の主判定の値 − 5.0pt。現在の余裕は "
                    + $"**{fifth - Baseline.PrimaryFifthFloor:+0.0;-0.0;0.0}pt**。");
    Console.WriteLine();
    Console.WriteLine("**線は「明らかに成立しなくなる線」であって調整目標ではない。**");
    Console.WriteLine("第59期の提案20行がちょうど 40.0 で線上に乗ったのは偶然で、**線上に置くのが一番まずい**");
    Console.WriteLine("——だから確定時の値そのものではなく **−5.0pt** に置いてある。");
    Console.WriteLine();

    return;

    // ピアソン相関。片方の分散が 0 なら定義できないので NaN を返す（呼び出し側で — に置く）。
    static double Corr(double[] a, double[] b)
    {
        double ma = a.Average(), mb = b.Average();
        double num = 0, da = 0, db = 0;
        for (int i = 0; i < a.Length; i++)
        {
            num += (a[i] - ma) * (b[i] - mb);
            da += (a[i] - ma) * (a[i] - ma);
            db += (b[i] - mb) * (b[i] - mb);
        }
        return da <= 0 || db <= 0 ? double.NaN : num / Math.Sqrt(da * db);
    }
}
}
