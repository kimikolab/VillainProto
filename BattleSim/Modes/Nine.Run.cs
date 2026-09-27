using BattleCore;
using static Common;

// nine run —— 表A〜D（第221期）。敵は MaterializeEnemy（検証の波）と Materialize（第一波の参考）。
static partial class NineDiag
{
    static readonly EnemyScaleRule S115 = EnemyScaleRule.Default;

    static partial void RunImpl(string arg)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第221期 —— 9体の検証波（`0 nine run`）");
        Console.WriteLine();
        Console.WriteLine($"倍率は既定（{S115.HpPercent}/{S115.AtkPercent}）、seed 0..{Seeds - 1}。比べる相手は **検証・五 / 新兵**（同じ駒の5体）。");
        Console.WriteLine();
        if (arg != "b") TableA();
        if (arg != "a") TableB();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒");
    }

    static EnemyWave W(int i) => EnemyCatalog.TestStages[i].Enemy;
    // 列の並び: 第一波（本編） ／ 五 / 新兵 ／ 九 / 新兵 ／ 九 / 農兵
    static readonly string[] ColNames = { "第一波", "五/新兵", "九/新兵", "九/農兵" };
    static Func<List<UnitState>>[] Cols(EnemyScaleRule sc) => new[]
    {
        Stage(EnemyCatalog.Stages[0].Enemy, sc), Wave(W(2), sc), Wave(W(0), sc), Wave(W(1), sc),
    };

    // =================================================================================
    // 表A・C・D（compare の 61 行）
    // =================================================================================

    static void TableA()
    {
        var rows = CompareBuilds();
        var cols = Cols(S115);
        var res = new NAgg[rows.Length, cols.Length];
        Parallel.For(0, rows.Length * cols.Length, j =>
        {
            int r = j / cols.Length, c = j % cols.Length;
            res[r, c] = Measure(rows[r].F, cols[c]);
        });

        Console.WriteLine("## 表A —— 61 行 × 検証の3波（勝率 ／ 全員生存 ／ 勝った戦の決着T）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 第一波 | 五/新兵 | 九/新兵 | 九/農兵 | Δ 九新−五 | 全員生存 五 → 九新 | 決着T 五 → 九新 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int r = 0; r < rows.Length; r++)
            Console.WriteLine($"| {rows[r].Name} | {F1(res[r, 0].Win)} | {F1(res[r, 1].Win)} | {F1(res[r, 2].Win)} | {F1(res[r, 3].Win)} | {D1(res[r, 2].Win - res[r, 1].Win)} | {F1(res[r, 1].Surv)} → {F1(res[r, 2].Surv)} | {F2(res[r, 1].WinT)} → {F2(res[r, 2].WinT)} |");
        Console.WriteLine();

        Console.WriteLine("### 61 行の平均");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | 全員生存 | 決着T（勝ち） | 勝率 100% の行 | 勝率 0% の行 | 情報セル（0<x<100） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        for (int c = 0; c < cols.Length; c++)
        {
            var ws = Enumerable.Range(0, rows.Length).Select(r => res[r, c].Win).ToList();
            Console.WriteLine($"| {ColNames[c]} | {F1(ws.Average())} | {F1(Enumerable.Range(0, rows.Length).Average(r => res[r, c].Surv))} | {F2(Enumerable.Range(0, rows.Length).Select(r => res[r, c].WinT).Where(x => !double.IsNaN(x)).DefaultIfEmpty(double.NaN).Average())} | {ws.Count(x => x >= 100)} | {ws.Count(x => x <= 0)} | {ws.Count(x => x > 0 && x < 100)} |");
        }
        Console.WriteLine();

        Console.WriteLine("### 群ごと（指示書 §4 P2・群は Phase 0 で固定）");
        Console.WriteLine();
        Console.WriteLine("余地で割った取り分 ＝ Δ ÷ 五（落ちうる幅）。");
        Console.WriteLine();
        Console.WriteLine("| 群 | 行数 | 五/新兵 | 九/新兵 | Δ 九新−五 | 取り分 | 九/農兵 | Δ 九農−五 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        var groups = new List<(string, Func<Formation, bool>)> { ("61 行すべて", _ => true) };
        groups.AddRange(Groups);
        foreach (var (name, inG) in groups)
        {
            var idx = Enumerable.Range(0, rows.Length).Where(r => inG(rows[r].F)).ToList();
            if (idx.Count == 0) continue;
            double five = idx.Average(r => res[r, 1].Win), nine = idx.Average(r => res[r, 2].Win), lev = idx.Average(r => res[r, 3].Win);
            double share = idx.Where(r => res[r, 1].Win > 0).Select(r => (res[r, 2].Win - res[r, 1].Win) / res[r, 1].Win).DefaultIfEmpty(double.NaN).Average();
            Console.WriteLine($"| {name} | {idx.Count} | {F1(five)} | {F1(nine)} | {D1(nine - five)} | {F2(share)} | {F1(lev)} | {D1(lev - five)} |");
        }
        Console.WriteLine();

        Console.WriteLine("### 9体でだけ負ける ／ 9体でだけ勝てる（線は測る前に固定: 五 ≥ 95 かつ 九新 < 50 ／ 九新 > 五）");
        Console.WriteLine();
        var only9Lose = Enumerable.Range(0, rows.Length).Where(r => res[r, 1].Win >= 95 && res[r, 2].Win < 50).ToList();
        var only9Win = Enumerable.Range(0, rows.Length).Where(r => res[r, 2].Win > res[r, 1].Win).ToList();
        Console.WriteLine($"- 9体でだけ負ける（{only9Lose.Count} 行）: " + (only9Lose.Count == 0 ? "なし" : string.Join(" ／ ", only9Lose.Select(r => $"{rows[r].Name} {F1(res[r, 1].Win)}→{F1(res[r, 2].Win)}"))));
        Console.WriteLine($"- 9体でだけ勝てる（{only9Win.Count} 行）: " + (only9Win.Count == 0 ? "なし" : string.Join(" ／ ", only9Win.Select(r => $"{rows[r].Name} {F1(res[r, 1].Win)}→{F1(res[r, 2].Win)}"))));
        var worst = Enumerable.Range(0, rows.Length).OrderBy(r => res[r, 2].Win - res[r, 1].Win).Take(8).ToList();
        Console.WriteLine("- 落ち幅の大きい 8 行: " + string.Join(" ／ ", worst.Select(r => $"{rows[r].Name} {D1(res[r, 2].Win - res[r, 1].Win)}")));
        var best = Enumerable.Range(0, rows.Length).OrderByDescending(r => res[r, 2].Win - res[r, 1].Win).Take(8).ToList();
        Console.WriteLine("- 落ち幅の小さい 8 行: " + string.Join(" ／ ", best.Select(r => $"{rows[r].Name} {D1(res[r, 2].Win - res[r, 1].Win)}")));
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C —— 貫きの行き止まり（経路が空で単体1発に落ちた回数 ／ 戦）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 第一波 | 五/新兵 | 九/新兵 | 九/農兵 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (int r in Enumerable.Range(0, rows.Length).Where(r => HasPattern(rows[r].F, AttackPattern.Pierce)))
            Console.WriteLine($"| {rows[r].Name} | {string.Join(" | ", Enumerable.Range(0, cols.Length).Select(c => F2(res[r, c].Per(res[r, c].PierceDead))))} |");
        Console.WriteLine($"| 61 行すべて | {string.Join(" | ", Enumerable.Range(0, cols.Length).Select(c => F2(Enumerable.Range(0, rows.Length).Average(r => res[r, c].Per(res[r, c].PierceDead)))))} |");
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D —— 敵の手番と味方の被ダメ（61 行の合計を戦の数で割る）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 戦の長さ（T） | 敵の手番 ／ 戦 | 敵が振った回数 ／ 戦 | 味方の被ダメ ／ 戦 | 敵の被ダメ ／ 戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        for (int c = 0; c < cols.Length; c++)
        {
            var sum = new NAgg();
            for (int r = 0; r < rows.Length; r++) sum.Merge(res[r, c]);
            Console.WriteLine($"| {ColNames[c]} | {F2(sum.Per(sum.Turns))} | {F2(sum.Per(sum.FoeTurns))} | {F2(sum.Per(sum.FoeAttacks))} | {F1(sum.Per(sum.PlayerTaken))} | {F1(sum.Per(sum.FoeTaken))} |");
        }
        Console.WriteLine();
        Console.WriteLine("（第一波の敵の手番は戦斧兵を含むので `recruit` だけでは数えない——第一波の列の手番・被ダメは参考に留める）");
        Console.WriteLine();
    }

    // =================================================================================
    // 表B（雷の台）
    // =================================================================================

    static void TableB()
    {
        var benches = BurstDiag.Seats().Where(s => s.Name.EndsWith("X字")).Select(s => (s.Name, s.F)).ToList();
        benches.Add(("R3 ポンの X字", BurstDiag.PonX()));
        Console.WriteLine("## 表B —— 雷の台 × 検証の波（感電の連鎖 ／ 澱みの爆発）");
        Console.WriteLine();
        Console.WriteLine("台と席は第220期 `burst` の X 字の席（B2 × 倍率 200 で選んだ席）と、ポンの X字。ミオは規定（B2）。");
        Console.WriteLine();
        foreach (var (name, f) in benches)
            Console.WriteLine($"- {name}: {BurstDiag.SeatsNamed(f)}");
        Console.WriteLine();
        foreach (int scale in new[] { 115, 150 })
        {
            var sc = new EnemyScaleRule(scale, scale);
            var cols = Cols(sc);
            var res = new NAgg[benches.Count, cols.Length];
            for (int b = 0; b < benches.Count; b++)
                for (int c = 1; c < cols.Length; c++)
                    res[b, c] = Measure(benches[b].F, cols[c]);
            Console.WriteLine($"### 倍率 {scale}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 波 | 勝率 | 全員生存 | 決着T | 敵の感電の連鎖 ／ 戦 | 連鎖の平均の大きさ | 3体以上の割合 | 爆発の連鎖 ／ 戦 | 爆発1本の平均の長さ | 2体以上の割合 | 倒れた瞬間の隣 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            var tot = new NAgg[cols.Length];
            for (int c = 1; c < cols.Length; c++) tot[c] = new NAgg();
            for (int b = 0; b < benches.Count; b++)
                for (int c = 1; c < cols.Length; c++)
                {
                    var a = res[b, c]; tot[c].Merge(a);
                    Console.WriteLine(Line(benches[b].Name, ColNames[c], a));
                }
            for (int c = 1; c < cols.Length; c++) Console.WriteLine(Line("**台の合計**", ColNames[c], tot[c]));
            Console.WriteLine();
        }

        static string Line(string bench, string col, NAgg a)
            => $"| {bench} | {col} | {F1(a.Win)} | {F1(a.Surv)} | {F2(a.WinT)} | {F2(a.Per(a.EChainRoots))} | {F2(a.ChainMean)} | {F1(a.ChainBigPct)} | {F2(a.Per(a.BurstChains))} | {F2(a.BurstLenMean)} | {F1(a.BurstMultiPct)} | {F2(a.NeighborMean)} |";
    }
}
