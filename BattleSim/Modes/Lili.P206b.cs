using BattleCore;
using static Common;

// =====================================================================================
// lili モード（第206期）の本体 —— 版 W0〜W2・第四波／第五波の帳簿・自己検査
//
//   W0 第205期 V4（段は 40 ごと・儀式は敵ごとに1倍）／ W1 ＋段の刻みを三角数（`KissTri`）／ W2 ＋祝福の儀を5倍の等分（`KissRite5`・＝ `UnitCatalog.Lili`）
//
//     dotnet run --project BattleSim -c Release 0 lili run3      # 在席行 × W0〜W2（第2〜5波）
//     dotnet run --project BattleSim -c Release 0 lili ledger3   # 第四波・第五波を別々に（儀式で決着・段・与ダメ:回復ほか）
//     dotnet run --project BattleSim -c Release 0 lili check3 [第205期のbalance.md]  # 自己検査
// =====================================================================================

static partial class LiliDiag
{
    static readonly (string Tag, UnitDef D)[] Ladder206 =
    {
        ("W0", Clone(UnitCatalog.Lili, UnitCatalog.Lili.Traits.Where(t => t is not (TraitId.KissTri or TraitId.KissRite5)).ToArray())),
        ("W1", Clone(UnitCatalog.Lili, UnitCatalog.Lili.Traits.Where(t => t is not TraitId.KissRite5).ToArray())),
        ("W2", UnitCatalog.Lili),
    };

    // =================================================================================
    // run3
    // =================================================================================

    static void Run206()
    {
        Console.WriteLine("# 第206期 `lili run3` —— 在席行 × W0〜W2（**線は置かない**・seed 0..199・平均は第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("**W0** 第205期 V4 ／ **W1** ＋段の刻みを三角数 ／ **W2** ＋祝福の儀を5倍の等分（規定）。**段** ＝ W1−W0 ／ **儀式** ＝ W2−W1。`膠着` は 30 ターン上限（第2〜5波・800 戦中）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | W0 | W1 | W2 | W0 平均 | W1 | **W2** | 段 | 儀式 | W2−W0 | 情報セル W0→W2 | 膠着 W0→W2 |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        var sum = new double[3]; int n = 0, info0 = 0, info2 = 0;
        var wave = new double[3, 5];
        foreach (var (band, name, f) in LiliRows())
        {
            var r = Ladder206.Select(v => RatesAndStall(Apply(f, v.D))).ToArray();
            var m = r.Select(x => Mean25(x.W)).ToArray();
            if (band == "compare")
            {
                n++;
                for (int i = 0; i < 3; i++) { sum[i] += m[i]; for (int st = 1; st < 5; st++) wave[i, st] += r[i].W[st]; }
                info0 += InfoCells(r[0].W); info2 += InfoCells(r[2].W);
            }
            Console.WriteLine("| " + band + " | " + name + " | " + string.Join(" | ", r.Select(x => Cells(x.W))) + " | "
                              + m[0].ToString("F1") + " | " + m[1].ToString("F1") + " | **" + m[2].ToString("F1") + "** | "
                              + D(m[1] - m[0]) + " | " + D(m[2] - m[1]) + " | " + D(m[2] - m[0]) + " | "
                              + InfoCells(r[0].W) + " → " + InfoCells(r[2].W) + " | " + r[0].Stall.Skip(1).Sum() + " → " + r[2].Stall.Skip(1).Sum() + " |");
        }
        Console.WriteLine();
        if (n > 0)
        {
            Console.WriteLine("`compare` の在席 " + n + " 行の平均: " + string.Join(" ／ ", Ladder206.Select((v, i) => v.Tag + " " + (sum[i] / n).ToString("F1"))) + "。情報セル W0 " + info0 + " → W2 " + info2 + "。");
            Console.WriteLine();
            Console.WriteLine("| 波 | W0 | W1 | W2 |");
            Console.WriteLine("|---|--:|--:|--:|");
            string[] wn = { "", "第2波", "第3波（渇き）", "第4波（軛）", "第5波" };
            for (int st = 1; st < 5; st++)
                Console.WriteLine("| " + wn[st] + " | " + string.Join(" | ", Enumerable.Range(0, 3).Select(i => (wave[i, st] / n).ToString("F1"))) + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // ledger3
    // =================================================================================

    static void Ledger206()
    {
        Console.WriteLine("# 第206期 `lili ledger3` —— 第四波と第五波を別々に（1戦あたり・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("**決着T** 全戦の平均 ／ **儀式で決着** 儀式の吸い取りで敵が全員倒れた戦（分母は全戦）／ **敵 1/2/3+** 儀式の頭に生きていた敵の数の分布 ／"
                          + " **吸えた合計/回** 儀式1回で吸えた合計 ／ **1体最大** 儀式で1体から吸えた量の戦ごとの最大（平均/最大）／ **段** 戦の終わりの段 0/1/2/3以上 ／"
                          + " **与ダメ:回復** リリの敵への与ダメ ÷ 実際に癒した量。");
        Console.WriteLine();
        var rows = LiliRows().ToList();
        foreach (int st in new[] { 3, 4 })
        {
            Console.WriteLine("## " + (st == 3 ? "第四波（城塞守備隊・軛）" : "第五波"));
            Console.WriteLine();
            Console.WriteLine("| 行 | 版 | 勝率 | 決着T | 儀式で決着 | 儀式/戦 | 初回T | 敵 1/2/3+ | 吸えた合計/回 | 1体最大 | 段 0/1/2/3+ | 段1 T | 段2 T | 与ダメ/戦 | 回復/戦 | 与ダメ:回復 | 倒れた戦 | 膠着 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (var (band, name, f) in rows)
            foreach (var (tag, d) in Ladder206)
            {
                L3 l = Collect3(Apply(f, d), st); UnitTally t = l.T;
                long[] h = t.RiteFoeHist ?? new long[3]; long hs = Math.Max(1, h.Sum());
                Console.WriteLine("| " + (tag == "W0" ? name : "") + " | " + tag + " | " + Pct(l.Wins, l.N) + " | " + Avg(l.TurnsSum, l.N) + " | "
                                  + Pct(l.FinishBattles, l.N) + " | " + (t.RiteFires / Math.Max(1.0, l.N)).ToString("F2") + " | " + Avg(l.RiteFirstSum, l.RiteBattles) + " | "
                                  + (h.Sum() == 0 ? "—" : string.Join("/", h.Select(x => (100.0 * x / hs).ToString("F0")))) + " | "
                                  + (t.RiteFires == 0 ? "—" : ((double)t.RiteDrained / t.RiteFires).ToString("F1")) + " | "
                                  + Avg(l.RiteMaxSum, l.RiteBattles) + " / " + l.RiteMaxMax + " | "
                                  + string.Join("/", l.TierHist.Select(x => (100.0 * x / Math.Max(1, l.N)).ToString("F0"))) + " | "
                                  + Avg(l.TierTurnSum[0], l.TierTurnN[0]) + " | " + Avg(l.TierTurnSum[1], l.TierTurnN[1]) + " | "
                                  + Avg(l.DmgSum, l.N) + " | " + Avg(l.HealSum, l.N) + " | " + (l.HealSum == 0 ? "—" : (l.DmgSum / l.HealSum).ToString("F2")) + " | "
                                  + Pct(l.Died, l.N) + " | " + l.Stall + " |");
            }
            Console.WriteLine();
        }
    }

    // =================================================================================
    // check3
    // =================================================================================

    static void Check206(string arg)
    {
        Console.WriteLine("# 第206期 `lili check3` —— 自己検査");
        Console.WriteLine();
        bool ok = true;
        string path = string.IsNullOrWhiteSpace(arg) ? Path.Combine("docs", "balance.md") : arg.Trim();
        var old = ReadBalance(path);
        Console.WriteLine("## (1) `compare` の回帰（比べる表: `" + path + "`・" + old.Count + " 行）");
        Console.WriteLine();
        int noRows = 0, noDiff = 0, w0Rows = 0, w0Diff = 0;
        foreach (var (name, f) in CompareBuilds())
        {
            if (!old.TryGetValue(name, out double[]? want)) { Console.WriteLine("- 行が見つからない: " + name); ok = false; continue; }
            if (!HasLili(f)) { noRows++; double[] got = Rates(f); for (int st = 0; st < 5; st++) if (Math.Abs(got[st] - want[st]) > 0.05) noDiff++; }
            else { w0Rows++; double[] got = Rates(Apply(f, Ladder206[0].D)); for (int st = 0; st < 5; st++) if (Math.Abs(got[st] - want[st]) > 0.05) w0Diff++; }
        }
        Console.WriteLine("- リリを含まない " + noRows + " 行 × 5 波: 差 **" + noDiff + "** セル " + (noDiff == 0 ? "○" : "×"));
        Console.WriteLine("- リリの在席 " + w0Rows + " 行を W0 にした版 × 5 波: 差 **" + w0Diff + "** セル " + (w0Diff == 0 ? "○" : "×"));
        ok &= noDiff == 0 && w0Diff == 0;
        Console.WriteLine();

        Console.WriteLine("## (3) 台本（verbose）で見る規則（在席行 × 第2〜5波 × seed 0..39・W1 / W2）");
        Console.WriteLine();
        int battles = 0, tierEvents = 0, tierBad = 0, rites = 0, rite5 = 0, poolBad = 0, splitBad = 0, overPool = 0, overHp = 0, endBad = 0,
            riteTransfer = 0, riteSteal = 0;
        for (int vi = 1; vi < Ladder206.Length; vi++)
        {
            UnitDef d = Ladder206[vi].D;
            bool tri = d.Traits.Contains(TraitId.KissTri), five = d.Traits.Contains(TraitId.KissRite5);
            foreach (var (band, name, f0) in LiliRows())
            foreach (int st in Waves25)
            for (int seed = 0; seed < 40; seed++)
            {
                Formation f = Apply(f0, d);
                var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var en = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam, BossRule.Default.Scale);
                BattleResult r = BattleEngine.Run(pl, en, seed, verbose: true);
                battles++;
                int? lili = pl.FirstOrDefault(u => u.Def.Id == "lili")?.InstanceId;
                int painAmount = 0, pool = 0, nominalSum = 0, drainedSum = 0;
                bool inRite = false;
                int? pendingFoe = null; int pendingHp = 0;
                foreach (var e in r.Events)
                {
                    if (inRite && e.Kind == BattleEventKind.StatusTransfer) riteTransfer++;
                    if (pendingFoe is int pf && e.Kind == BattleEventKind.Damage && e.TargetId == pf && e.ActorId == lili)
                    {
                        int got = Math.Max(0, pendingHp - e.HpAfter);
                        if (got > pendingHp) overHp++;
                        drainedSum += got;
                        pendingFoe = null;
                    }
                    if (e.Kind != BattleEventKind.Kiss || e.ActorId != lili) continue;
                    switch (e.Text)
                    {
                        case KissLabels.Pain: painAmount = e.StatusRemaining ?? 0; break;
                        case KissLabels.Tier:
                            tierEvents++;
                            if (e.Slot != KissTrait.TierOf(e.StatusRemaining ?? 0, tri)) tierBad++;
                            break;
                        case KissLabels.Rite:
                            rites++; inRite = true; nominalSum = 0; drainedSum = 0; pool = e.Amount;
                            if (five)
                            {
                                rite5++;
                                if (pool != painAmount * KissTrait.RiteMultiplier) poolBad++;
                                int each = e.Slot == 0 ? 0 : pool / e.Slot;   // Slot ＝ 聖痕の敵の数（儀式の頭は全員生きている）
                                if (e.StatusRemaining != each) splitBad++;
                            }
                            break;
                        case KissLabels.RiteDrain:
                            nominalSum += e.Amount; pendingFoe = e.TargetId; pendingHp = e.HpAfter; break;
                        case KissLabels.RiteEnd:
                            inRite = false;
                            if (five && nominalSum > pool) overPool++;
                            if (five && drainedSum > pool) overPool++;
                            if (e.Amount != drainedSum) endBad++;
                            break;
                        case KissLabels.Steal: if (inRite) riteSteal++; break;
                    }
                }
            }
        }
        void Row(string what, int bad, string note = "")
        {
            Console.WriteLine("| " + what + " | " + bad + " | " + (bad == 0 ? "○" : "**×**") + " | " + note + " |");
            ok &= bad == 0;
        }
        Console.WriteLine("戦 " + battles + " ／ 段の出来事 " + tierEvents + " ／ 儀式 " + rites + "（うち5倍の等分 " + rite5 + "）。");
        Console.WriteLine();
        Console.WriteLine("| 規則 | 違反 | 判定 | 注 |");
        Console.WriteLine("|---|--:|:-:|---|");
        Row("段の閾値が刻みどおり（W1・W2 は三角数 40・120・240…）", tierBad);
        Row("儀式の総量 ＝ その手番の吸う量 × 5（W2）", poolBad);
        Row("見出しの「1体あたり」＝ 総量 ÷ 聖痕の敵の数（余りを足す前）", splitBad);
        Row("儀式で吸った名目の合計・実額の合計が総量を超えない（W2）", overPool);
        Row("1体から吸えた量が吸う前の HP を超えない", overHp);
        Row("儀式の終わりの「吸えた合計」＝ 台本の Damage から組み直した実額の合計", endBad);
        Row("儀式で状態が移らない", riteTransfer);
        Row("儀式で強弱が移らない", riteSteal);
        Console.WriteLine();
        Console.WriteLine(ok ? "**全部 ○。**" : "**× がある。**");
    }
}
