using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// firescale run —— 表0（台）・表A（影の火勢 §3.2）・表B（手番の絵 §3.3）・表C（発火見込み §3.4）。
// 台 5 × 波 6 × 倍率 3 × seed 0..199（verbose）。
static partial class FireScaleDiag
{
    /// <summary>条件: 主判定（九/新兵 200/200）／ 負荷（九/新兵 400/300）／ 本編 200/200（第2〜5波を合わせる）／ 九/農兵 200/200。</summary>
    static readonly (string Name, int[] Waves, int Sc)[] Conds =
    {
        ("九/新兵 × 200/200（主判定）", new[] { BA.MainWave }, 0),
        ("九/新兵 × 400/300", new[] { BA.MainWave }, 1),
        ("本編 第2〜5波 × 200/200", new[] { 0, 1, 2, 3 }, 0),
        ("九/農兵 × 200/200", new[] { 5 }, 0),
    };

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards.Select(b => (b.Name, F: b.F())).ToArray();
        var res = new Dictionary<(int B, int W, int S), Agg>();
        var keys = new List<(int, int, int)>();
        for (int b = 0; b < boards.Length; b++) for (int w = 0; w < BA.WaveNames.Length; w++) for (int s = 0; s < BA.Scales.Length; s++) keys.Add((b, w, s));
        foreach (var k in keys) res[k] = Measure(boards[k.Item1].F, k.Item2, BA.Scales[k.Item3].Sc);
        Agg Cond(int b, int ci)
        {
            var a = new Agg();
            foreach (int w in Conds[ci].Waves) a.Merge(res[(b, w, Conds[ci].Sc)]);
            return a;
        }

        Console.WriteLine("# 第239期 `firescale run` —— 表0・表A〜C");
        Console.WriteLine();
        Console.WriteLine($"台 {boards.Length} × 波 {BA.WaveNames.Length} × 倍率 {BA.Scales.Length} × seed 0..{BA.Seeds - 1}（verbose・{(long)keys.Count * BA.Seeds:N0} 戦）。");
        Console.WriteLine();

        // ---------------- 表0 台 ----------------
        Console.WriteLine("## 表0 台（全員生存 ／ 勝率・%）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 席 | " + string.Join(" | ", BA.WaveNames.Select(w => w)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", BA.WaveNames.Length)));
        for (int s = 0; s < BA.Scales.Length; s++)
        {
            Console.WriteLine($"| **{BA.Scales[s].Name}** | | " + string.Concat(Enumerable.Repeat(" |", BA.WaveNames.Length)));
            for (int b = 0; b < boards.Length; b++)
                Console.WriteLine($"| {boards[b].Name} | {(s == 0 ? BA.SeatsNamed(boards[b].F) : "")} | "
                    + string.Join(" | ", Enumerable.Range(0, BA.WaveNames.Length).Select(w => { var a = res[(b, w, s)]; return $"{Pct(a.AllSurv, a.N)} ／ {Pct(a.Wins, a.N)}"; })) + " |");
        }
        Console.WriteLine();

        // ---------------- 表A 影の火勢 ----------------
        for (int ci = 0; ci < 3; ci++)
        {
            Console.WriteLine($"## 表A 影の火勢 —— {Conds[ci].Name}");
            Console.WriteLine();
            Console.WriteLine("ターンの終わり（着火の後・萎む前）の生きている駒で: 平均 ／ 火勢3 以上 % ／ 火勢4 %。駒ごと: 3 以上に届いた % ／ 4 に届いた %（届いたターンの平均）。着火/戦 は台本の `StatusGain` burn（うち影 ≥ 1 の駒へ %）。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 役 | 着火/戦（点け直し %） | K1 平均 ／ ≥3 ／ =4 | K1 届3 ／ 届4（T） | K2 平均 ／ ≥3 ／ =4 | K2 届3 ／ 届4（T） | K3 平均 ／ ≥3 ／ =4 | K3 届3 ／ 届4（T） |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
            for (int b = 0; b < boards.Length; b++)
            {
                var a = Cond(b, ci);
                for (int ro = 0; ro < Roles.Length; ro++)
                {
                    if (a.UnitN[0, ro] == 0) continue;
                    var cells = new List<string>();
                    for (int k = 0; k < 3; k++)
                    {
                        cells.Add($"{Avg(a.SSum[k, ro], a.SCnt[k, ro])} ／ {Pct(a.Ge3[k, ro], a.SCnt[k, ro])} ／ {Pct(a.Eq4[k, ro], a.SCnt[k, ro])}");
                        cells.Add($"{Pct(a.Reach3[k, ro], a.UnitN[k, ro])} ／ {Pct(a.Reach4[k, ro], a.UnitN[k, ro])}（{Avg(a.Reach3T[k, ro], a.Reach3[k, ro])} ／ {Avg(a.Reach4T[k, ro], a.Reach4[k, ro])}）");
                    }
                    Console.WriteLine($"| {boards[b].Name} | {Roles[ro]} | {(double)a.Ignites[ro] / a.N:F1}（{Pct(a.IgnitesRelit[ro], a.Ignites[ro])}） | {string.Join(" | ", cells)} |");
                }
            }
            Console.WriteLine();
        }

        // 表A' 影と燃焼のずれ・表A'' ターンごと（主判定）
        Console.WriteLine("## 表A' 影と燃焼のずれ（ターンの頭・生きている駒・%）—— 九/新兵 × 200/200");
        Console.WriteLine();
        Console.WriteLine("燃えている ＝ そのターンの頭の写し（`StatusSnapshot`）に燃焼がある。「影だけ」＝ 影 ≥ 1 なのに燃えていない（燃焼は切れたが萎みきっていない）、「燃焼だけ」＝ 燃えているのに影 0（点け直されずに萎みきった）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 役 | 燃えている | K1 影だけ ／ 燃焼だけ | K2 影だけ ／ 燃焼だけ | K3 影だけ ／ 燃焼だけ |");
        Console.WriteLine("|---|---|---|---|---|---|");
        for (int b = 0; b < boards.Length; b++)
        {
            var a = Cond(b, 0);
            for (int ro = 0; ro < Roles.Length; ro++)
            {
                if (a.StartCnt[0, ro] == 0 || a.Ignites[ro] == 0) continue;
                Console.WriteLine($"| {boards[b].Name} | {Roles[ro]} | {Pct(a.BurnStart[0, ro], a.StartCnt[0, ro])} | "
                    + string.Join(" | ", Enumerable.Range(0, 3).Select(k => $"{Pct(a.ShadowNoBurn[k, ro], a.StartCnt[k, ro])} ／ {Pct(a.BurnNoShadow[k, ro], a.StartCnt[k, ro])}")) + " |");
            }
        }
        Console.WriteLine();

        foreach (int ci in new[] { 0, 2 })
        {
            Console.WriteLine($"## 表A'' ターンごとの影の火勢（K1・平均）—— {Conds[ci].Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 役 | " + string.Join(" | ", Enumerable.Range(1, Agg.TMax).Select(t => t == Agg.TMax ? $"T{t}+" : $"T{t}")) + " |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", Agg.TMax)));
            for (int b = 0; b < boards.Length; b++)
            {
                var a = Cond(b, ci);
                for (int ro = 0; ro < Roles.Length; ro++)
                {
                    if (a.UnitN[0, ro] == 0 || a.Ignites[ro] == 0) continue;
                    Console.WriteLine($"| {boards[b].Name} | {Roles[ro]} | " + string.Join(" | ", Enumerable.Range(1, Agg.TMax).Select(t => a.TurnCnt[0, ro, t] == 0 ? "—" : $"{Avg(a.TurnSum[0, ro, t], a.TurnCnt[0, ro, t])}（{a.TurnCnt[0, ro, t] * 100 / Math.Max(1, a.TurnCnt[0, ro, 1])}%）")) + " |");
                }
            }
            Console.WriteLine();
            Console.WriteLine("括弧はそのターンまで生きて盤に残っている（戦が続いている）駒の数の、T1 に対する割合。");
            Console.WriteLine();
        }

        Console.WriteLine("## 表A''' 敵への着火の出どころ（/戦）と、K1 で火勢3 に届いた敵");
        Console.WriteLine();
        Console.WriteLine("ボルグの手番の中 ＝ 火の粉（薙ぎで斬った相手）・焼き返し。ボルグの手番の外 ＝ 火の鎧（殴ってきた敵に火を返す）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 条件 | ボルグの手番の中 | ボルグの手番の外（火の鎧） | ほかの駒 | 出どころなし | 火勢3 に届いた敵（駒・戦 /戦） | うち着火の過半が火の鎧 % |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        for (int b = 0; b < boards.Length; b++)
            for (int ci = 0; ci < 3; ci++)
            {
                var a = Cond(b, ci);
                if (a.Ignites[4] == 0) continue;
                Console.WriteLine($"| {boards[b].Name} | {Conds[ci].Name} | " + string.Join(" | ", a.FoeIgniteSrc.Select(x => ((double)x / a.N).ToString("F2")))
                    + $" | {(double)a.FoeReach3 / a.N:F2} | {Pct(a.FoeReach3OutBorg, a.FoeReach3)} |");
            }
        Console.WriteLine();

        // ---------------- 表B 手番の絵 ----------------
        foreach (int ci in new[] { 0, 2 })
        {
            Console.WriteLine($"## 表B 手番の絵 —— {Conds[ci].Name}");
            Console.WriteLine();
            Console.WriteLine("1戦ごとに駒の手番を並べ、その戦の 絵の種類数 ／ 同じ絵が続いた最長 ／ 最も多い絵の割合 を取って戦で平均（最長の最大は全戦で）。骨格 ＝ 種類／攻撃型だけ。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 駒 | 手番/戦 | 絵の種類 | 最長の連続（最大） | 最多の割合 | **続けて同じ絵** | 骨格の種類 | 骨格の最多 | 続けて同じ骨格 | 最も多い絵（全戦での割合） |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|");
            for (int b = 0; b < boards.Length; b++)
            {
                var a = Cond(b, ci);
                foreach (var (id, pa) in a.Pics.OrderBy(kv => kv.Key switch { "borg" => 0, "hota" => 1, "hiyo" => 2, _ => 3 }).ThenBy(kv => kv.Value.Name))
                {
                    if (pa.Battles == 0) continue;
                    double top = pa.Battles == 0 ? double.NaN : 0;
                    var top3 = pa.Freq.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key}（{100.0 * kv.Value / pa.Hands:F0}%）");
                    Console.WriteLine($"| {boards[b].Name} | {pa.Name} | {(double)pa.Hands / pa.Battles:F2} | {Avg(pa.DistinctSum, pa.Battles)} | {Avg(pa.RunSum, pa.Battles)}（{pa.RunMax}） | "
                        + $"{TopShare(pa)} | {Pct(pa.Repeats, pa.Pairs)} | {Avg(pa.BoneDistinctSum, pa.Battles)} | {BoneTopShare(pa)} | {Pct(pa.BoneRepeats, pa.Pairs)} | {string.Join("<br>", top3)} |");
                }
            }
            Console.WriteLine();
        }
        Console.WriteLine("最多の割合は「その戦で最も多い絵の手番数」の合計 ÷ 手番の合計（手番の多い戦に重み）。**続けて同じ絵** ＝ 同じ戦の隣り合う2手番が同じ絵だった割合（戦の長さにほぼ依らない・参考の台は 九/新兵 で 1〜2 手番で終わるので、種類数と最長はそちらでは比べられない）。");
        Console.WriteLine();

        // ---------------- 表C 発火見込み ----------------
        foreach (int ci in new[] { 0, 1, 2 })
        {
            Console.WriteLine($"## 表C 火勢で手番を変える候補の発火見込み —— {Conds[ci].Name}");
            Console.WriteLine();
            Console.WriteLine("ホタ ＝ 手番の頭の影で段を分けた手番の割合（1 単体 → 2 貫き → 3 貫き＋着火 → 4 全体）。ボルグ ＝ 影 4 の手番で「放つ」（影の中だけ 1 に戻す）。ヒヨ ＝ 手番の頭の味方の影の合計。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 規則 | ホタ 段0 ／ 1 ／ 2 ／ 3 ／ 4（%） | ボルグ 放つ/戦 ／ 放った戦 % ／ 最初の T | ボルグの手番の段 0 ／ 1 ／ 2 ／ 3 ／ 4（%） | ヒヨ 合計の平均（最大） | 合計 0-1 ／ 2-3 ／ 4-5 ／ 6-7 ／ 8-9 ／ 10+（%） |");
            Console.WriteLine("|---|---|---|---|---|---|---|");
            for (int b = 0; b < boards.Length; b++)
            {
                var a = Cond(b, ci);
                if (a.BorgBattles[0] == 0 && a.HotaStage[0, 0] + a.HotaStage[0, 1] == 0) continue;
                for (int k = 0; k < 3; k++)
                {
                    long hs = Enumerable.Range(0, Max + 1).Sum(i => a.HotaStage[k, i]);
                    long bs = Enumerable.Range(0, Max + 1).Sum(i => a.BorgStage[k, i]);
                    long hy = Enumerable.Range(0, 6).Sum(i => a.HiyoSum[k, i]);
                    string hota = hs == 0 ? "—" : string.Join(" ／ ", Enumerable.Range(0, Max + 1).Select(i => Pct(a.HotaStage[k, i], hs)));
                    string borg = a.BorgBattles[k] == 0 ? "—" : $"{(double)a.Releases[k] / a.BorgBattles[k]:F2} ／ {Pct(a.ReleaseBattles[k], a.BorgBattles[k])} ／ {Avg(a.ReleaseFirstT[k], a.ReleaseBattles[k])}";
                    string bst = bs == 0 ? "—" : string.Join(" ／ ", Enumerable.Range(0, Max + 1).Select(i => Pct(a.BorgStage[k, i], bs)));
                    string hiyo = hy == 0 ? "—" : $"{Avg(a.HiyoSumTotal[k], hy)}（{a.HiyoMax[k]}）";
                    string hst = hy == 0 ? "—" : string.Join(" ／ ", Enumerable.Range(0, 6).Select(i => Pct(a.HiyoSum[k, i], hy)));
                    Console.WriteLine($"| {(k == 0 ? boards[b].Name : "")} | {RuleNames[k]} | {hota} | {borg} | {bst} | {hiyo} | {hst} |");
                }
            }
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }

    static string TopShare(PicAgg p) => p.Hands == 0 ? "—" : (100.0 * p.TopSum / p.Hands).ToString("F1");
    static string BoneTopShare(PicAgg p) => p.Hands == 0 ? "—" : (100.0 * p.BoneTopSum / p.Hands).ToString("F1");
}
