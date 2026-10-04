using BattleCore;
using static Common;

// =====================================================================================
// relic shock276 —— 第276期「帯電の足の再測（供給待ちの解除）」。
// ソムの転生で `compare` に感電の行（`感電 (シガ×カタ×ソム)`・62 行目）が立ったので、`RELIC_MEASURES.md` §5 の
// 「供給の行が `compare` に立った時点で再測する」を、確定版の物差しのまま新しい帯で回す。
// 指示書は design/PHASE276_SOM_REBIRTH_SPEC.md §5 ／ 報告は design/PHASE276_SOM_REBIRTH.md。
//
//     dotnet run --project BattleSim -c Release 0 relic shock276   # 62 行 × 帯電の足 × 枠5 × 本編第2〜5波・帯A seed 1000..1199 ／ 帯B 1200..1399
// =====================================================================================
static partial class RelicDiag
{
    /// <summary>第276期の帯（使ったことの無い帯: 第271期 0..199 ／ 第272期 200..599 ／ 第273期 600..999）。</summary>
    const int BandA276 = 1000, BandB276 = 1200;

    static void Shock276()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds();
        var card = RelicCatalog.Info(TraitId.RelicShockStep);
        var cards = new[] { card };
        Console.WriteLine($"# relic shock276 —— 第276期 帯電の足の再測（{rows.Length} 行 × 帯電の足 × 枠5 × 本編第2〜5波・帯A seed {BandA276}..{BandA276 + GridSeeds - 1}・帯B {BandB276}..{BandB276 + GridSeeds - 1}・物差しは design/RELIC_MEASURES.md）");
        Console.WriteLine();
        var g = RunGrid(BandA276, MainWaves, null, cards);
        var hits = UniqueWinnersOf(g, cards, BandB276);
        int losing = Enumerable.Range(0, rows.Length).Sum(i => MainWaves.Count(w => g.C[i][0][w].Wins < GridSeeds));
        Console.WriteLine($"総当たり ＋ 追試 {sw.Elapsed.TotalSeconds:F0} 秒。分母（帯A で素が負けるセル）: **{losing}** ／ {rows.Length * MainWaves.Length}。");
        Console.WriteLine();

        // ---- 1. 固有の勝者 ----
        Console.WriteLine("## 1. 固有の勝者");
        Console.WriteLine();
        Console.WriteLine($"**{hits.Count} 件・{hits.Select(h => h.Row).Distinct().Count()} 行**（第273期 帯A 600..799 ／ 帯B 800..999 では 0 件——供給待ち）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 軸 | 波 | 札 → 枠 | 帯A 素 → 札 | 帯B 素 → 札 | 持ち込む軸 | 混成 | 橋の資源 |");
        Console.WriteLine("|---|---|---|---|--:|--:|---|:-:|---|");
        foreach (var h in hits)
        {
            string? ax = CarryAxis(h.Relic);
            bool mix = ax is not null && !AxesOf(rows[h.Row].F).Contains(ax);
            Console.WriteLine($"| {rows[h.Row].Name} | {AxisTag(rows[h.Row].F)} | {WaveNames[h.Wave]} | {RName(h.Relic)} → {rows[h.Row].F[h.V.Frame]!.Name} | {h.A0.Win:F1} → {h.A1.Win:F1} | {h.B0.Win:F1} → {h.B1.Win:F1} | {ax ?? "—"} | {(mix ? "○" : "")} | {BridgeResource(h.Relic) ?? "—"} |");
        }
        Console.WriteLine();
        int bridged = hits.Count(h => BridgeResource(h.Relic) is not null);
        int mixed = hits.Count(h => CarryAxis(h.Relic) is string ax && !AxesOf(rows[h.Row].F).Contains(ax));
        Console.WriteLine($"- 混成（札が行に無い軸を持ち込んだ件）: **{mixed}** ／ 感電を橋渡しした件（主・§2.2）: **{bridged}**");
        Console.WriteLine();

        // ---- 2. 個性の保存（繋ぎ） ----
        Console.WriteLine("## 2. 個性の保存（繋ぎ）");
        Console.WriteLine();
        var keep = new string[hits.Count];
        Parallel.For(0, hits.Count, k =>
        {
            var h = hits[k];
            keep[k] = KeepLine($"{rows[h.Row].Name}（{WaveNames[h.Wave]}）", rows[h.Row].F, h.V, h.Wave, BandA276);
        });
        int kept = keep.Count(l => l.EndsWith("| ○ |"));
        Console.WriteLine($"**{kept} ／ {keep.Length}**（全例が線）——機構が個性を消した件 **{keep.Length - kept}**（線 0）");
        Console.WriteLine();
        if (keep.Length > 0)
        {
            Console.WriteLine("| 行 | 版 | 駒 | 与ダメ 素 → 札 | 回復 素 → 札 | 状態の付与 素 → 札 | 受けたダメ 素 → 札 | 主の指標 | 判定 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|--:|---|:-:|");
            foreach (var l in keep) Console.WriteLine(l);
            Console.WriteLine();
        }

        // ---- 3. 感電の行の全枠（発火と勝率） ----
        int ri = Array.FindIndex(rows, r => r.Name.StartsWith("感電 ("));
        Console.WriteLine("## 3. 感電の行の全枠（帯A・発火 ＝ 「帯電の足が」のログ行・1戦平均）");
        Console.WriteLine();
        if (ri < 0) { Console.WriteLine("感電の行が `compare` に無い。"); return; }
        Console.WriteLine("| 枠 | 駒 | " + string.Join(" | ", MainWaves.Select(w => WaveNames[w] + " 素 → 札（倒しT）")) + " | 発火／戦（第2〜5波） |");
        Console.WriteLine("|---|---|" + string.Concat(MainWaves.Select(_ => "--:|")) + "--:|");
        foreach (var v in g.Vars[ri].Where(v => v.Relic == card.Id))
        {
            var f = Apply(rows[ri].F, v);
            long fires = 0, n = 0;
            var lockObj = new object();
            foreach (int w in MainWaves)
                Parallel.For(0, GridSeeds, s =>
                {
                    var r = BattleEngine.Run(f, EnemyCatalog.Stages[w].Enemy, BandA276 + s, verbose: true);
                    long c = r.Log.Count(l => l.Text.Contains("の帯電の足が"));
                    lock (lockObj) { fires += c; n++; }
                });
            Console.WriteLine($"| {v.Frame} | {rows[ri].F[v.Frame]!.Name} | " + string.Join(" | ", MainWaves.Select(w =>
            {
                Cell a = g.C[ri][0][w], b = g.C[ri][v.Ix][w];
                return $"{a.Win:F1} → {b.Win:F1}（{(b.Wins > 0 ? b.T.ToString("F2") : "—")}）";
            })) + $" | {(double)fires / n:F2} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
