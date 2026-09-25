using BattleCore;
using static Common;

// =====================================================================================
// galdlast phase199（第199期） —— 剣の段を直す前の数え物（盤面は第198期のまま）
//
// 指示書は design/PHASE199_GALD_LAST2_SPEC.md ／ 報告は design/PHASE199_GALD_LAST2.md。
// =====================================================================================

static partial class GaldLastDiag
{
    static void Phase199()
    {
        Console.WriteLine("# 第199期 `galdlast phase199` —— Q0-2 / Q0-3（第198期の盤面・剣＋傷）");
        Console.WriteLine();
        Console.WriteLine("ガルドの在席 35 行 × 第2〜5波 × seed 0..199 のうち、剣の段に入った戦だけ。"
                          + "`受け流した刃` ＝ その戦で受け流しが無かったことにした打点の合計（`UnitTally.ParryBlocked`。第198期は抜いた瞬間に在庫を 0 にするので、"
                          + "**戦の終わりの値 ＝ 抜いた瞬間の値**）。`身に受けた傷` ＝ 庇いで身に受けた傷の累計（`LastStandScarTaken`）。"
                          + "`上乗せ` はどちらも 10%・切り捨て。`在庫` ＝ 抜いた瞬間に捨てた受け流しの在庫（`LastStandStockDropped`）。");
        Console.WriteLine();
        int n = 0;
        double scar = 0, blocked = 0, add198 = 0, add199 = 0;
        var stock = new int[4];
        var bins = new int[6];   // 199 の上乗せ: 0 / 1-2 / 3-5 / 6-9 / 10-19 / 20+
        var perWave = new (int N, double Scar, double Blocked, double A198, double A199, int[] Stock)[5];
        for (int st = 1; st <= 4; st++) perWave[st] = (0, 0, 0, 0, 0, new int[4]);
        var rows = GaldRows().ToList();
        var rowOut = new List<string>();
        foreach (var (name, f) in rows)
        {
            int rn = 0; double rs = 0, rb = 0, ra = 0, rb2 = 0;
            for (int st = 1; st <= 4; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    UnitTally g = r.TallyByUnit["gald"];
                    if (g.LastStandTurn == 0) continue;
                    int s = g.LastStandScarTaken, b = (int)g.ParryBlocked;
                    int a0 = s * 10 / 100, a1 = (s + b) * 10 / 100;
                    int k = (int)Math.Min(3, g.LastStandStockDropped);
                    n++; scar += s; blocked += b; add198 += a0; add199 += a1; stock[k]++;
                    bins[a1 == 0 ? 0 : a1 <= 2 ? 1 : a1 <= 5 ? 2 : a1 <= 9 ? 3 : a1 <= 19 ? 4 : 5]++;
                    var w = perWave[st];
                    w.N++; w.Scar += s; w.Blocked += b; w.A198 += a0; w.A199 += a1; w.Stock[k]++;
                    perWave[st] = w;
                    rn++; rs += s; rb += b; ra += a0; rb2 += a1;
                }
            rowOut.Add("| " + name + " | " + rn + " | " + Avg(rs, rn, "F1") + " | " + Avg(rb, rn, "F1") + " | " + Avg(ra, rn) + " | " + Avg(rb2, rn) + " |");
        }
        Console.WriteLine("## 在席行の合計");
        Console.WriteLine();
        Console.WriteLine("| 入った戦 | 身に受けた傷 | 受け流した刃 | 上乗せ 198（傷だけ） | 上乗せ 199（傷＋刃） | 在庫 0 | 在庫 1 | 在庫 2 | 在庫 3+ |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        Console.WriteLine("| " + n + " | " + Avg(scar, n, "F1") + " | " + Avg(blocked, n, "F1") + " | " + Avg(add198, n) + " | " + Avg(add199, n) + " | "
                          + P(stock[0], n) + " | " + P(stock[1], n) + " | " + P(stock[2], n) + " | " + P(stock[3], n) + " |");
        Console.WriteLine();
        Console.WriteLine("上乗せ 199 の分布: 0 " + P(bins[0], n) + " ／ 1〜2 " + P(bins[1], n) + " ／ 3〜5 " + P(bins[2], n)
                          + " ／ 6〜9 " + P(bins[3], n) + " ／ 10〜19 " + P(bins[4], n) + " ／ 20+ " + P(bins[5], n) + "。");
        Console.WriteLine();
        Console.WriteLine("## 波ごと");
        Console.WriteLine();
        Console.WriteLine("| 波 | 入った戦 | 身に受けた傷 | 受け流した刃 | 上乗せ 198 | 上乗せ 199 | 在庫 0 | 在庫 1 | 在庫 2 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int st = 1; st <= 4; st++)
        {
            var w = perWave[st];
            Console.WriteLine("| 第" + (st + 1) + "波 | " + w.N + " | " + Avg(w.Scar, w.N, "F1") + " | " + Avg(w.Blocked, w.N, "F1") + " | "
                              + Avg(w.A198, w.N) + " | " + Avg(w.A199, w.N) + " | " + P(w.Stock[0], w.N) + " | " + P(w.Stock[1], w.N) + " | " + P(w.Stock[2], w.N) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("## 行ごと");
        Console.WriteLine();
        Console.WriteLine("| 行 | 入った戦 | 身に受けた傷 | 受け流した刃 | 上乗せ 198 | 上乗せ 199 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (string l in rowOut) Console.WriteLine(l);
    }
}
