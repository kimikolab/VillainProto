using BattleCore;
using static Common;

// =====================================================================================
// tsugi モード（第210期） —— ツギの板を「敵に左右されない厚さ」にする（基本・応急処置・腕）
//
// 指示書は design/PHASE210_TSUGI4_SPEC.md ／ 報告は design/PHASE210_TSUGI4.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 tsugi phase210  # Q0-1・Q0-5（W0 ＝ 第209期の規定で数える）
//     dotnet run --project BattleSim -c Release 0 tsugi run4      # ツギの在席 3 行 × W0〜W3
//     dotnet run --project BattleSim -c Release 0 tsugi swap4     # リリの席にツギ（L ／ W0〜W3）
//     dotnet run --project BattleSim -c Release 0 tsugi ledger4   # 板と反射・腕・応急処置の帳簿 ＋ 強すぎの確認
//     dotnet run --project BattleSim -c Release 0 tsugi check4 [第209期のbalance.md]  # 自己検査
//
// 版は札の差し替えだけ: W0 ＝ [Plank, PlankScorch, Scrap, PlankRebound, PlankThick]（第209期 R2）／ W1 ＝ ＋ PlankBase ／
// W2 ＝ ＋ FirstAid ／ W3（規定）＝ ＋ PlankSkill。ドハはどれも規定（`SharerArmored`）。
// **版の定義はこのファイルの中だけで作る**（partial class の静的初期化子はファイルをまたぐと順序が決まらない・第196期）。
// =====================================================================================

static partial class TsugiDiag
{
    static readonly TraitId[] W0Traits = { TraitId.Plank, TraitId.PlankScorch, TraitId.Scrap, TraitId.PlankRebound, TraitId.PlankThick };

    static UnitDef WDef(params TraitId[] more) => Clone(UnitCatalog.Tsugi, W0Traits.Concat(more).ToArray());

    static readonly (string Tag, UnitDef Tsugi)[] WVersions =
    {
        ("W0", WDef()),
        ("W1", WDef(TraitId.PlankBase)),
        ("W2", WDef(TraitId.PlankBase, TraitId.FirstAid)),
        ("W3", WDef(TraitId.PlankBase, TraitId.FirstAid, TraitId.PlankSkill)),
    };

    static partial void RunMore210(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "phase210": Phase210(); handled = true; return;
            case "run4": Run210(); handled = true; return;
            case "swap4": Swap210(); handled = true; return;
            case "ledger4": Ledger210(); handled = true; return;
            case "check4": Check210(arg); handled = true; return;
            case "stall4": Stall210(); handled = true; return;
            default: RunMore211(mode, arg, ref handled); return;
        }
    }

    static partial void RunMore211(string mode, string arg, ref bool handled);

    static IEnumerable<(string Band, string Name, Func<int, Agg> At)> WRows(string tag)
    {
        var v = WVersions.First(x => x.Tag == tag);
        foreach (var (name, f) in TsugiRows())
            yield return ("compare", name, st => Measure(name + "|" + tag, ApplyR(f, "tsugi", v.Tsugi), st));
        foreach (var (name, f) in LiliRows())
            yield return ("差し替え", name, st => Measure(name + "|S" + tag, ApplyR(f, "lili", v.Tsugi), st));
    }

    static string WaveLab(int st) => st < 0 ? "**2〜5**" : (st + 1).ToString();

    // =================================================================================
    // phase210 —— Q0-1・Q0-5（W0 で数える）
    // =================================================================================

    static void Phase210()
    {
        Console.WriteLine("# 第210期 `tsugi phase210` —— Q0-1・Q0-5（W0 ＝ 第209期の規定・seed 0..199・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("台はツギの在席 3 行（`compare`）と、リリの在席 8 行のリリの席にツギ（差し替え）。**盤面は第209期のまま**で、足したのは計数だけ。");
        Console.WriteLine();
        int b24 = UnitCatalog.Tsugi.MaxHp * PlankTrait.BasePercent / 100;

        Console.WriteLine("## Q0-1 手番の板の厚さ（在庫を除いた分 ＝ max(6, 生きている敵の最大攻撃力)）");
        Console.WriteLine();
        Console.WriteLine("W1 の基本は **" + b24 + "**（HP" + UnitCatalog.Tsugi.MaxHp + " × " + PlankTrait.BasePercent + "%）。**薄い** ＝ W0 の板が " + b24 + " 未満（W1 で厚くなる）／ **同じ** ＝ " + b24 + " ／ **厚い** ＝ " + b24 + " を超える（W1 で薄くなる）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 波 | 貼った/戦 | 敵の分 平均 | 在庫 平均 | 薄い | 同じ | 厚い | 分布 6〜11 / 12〜17 / 18〜23 / 24 / 25〜30 / 31+ |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var band in new[] { "compare", "差し替え", "合算" })
            foreach (int st in Waves.Append(-1))
            {
                var rows = WRows("W0").Where(r => band == "合算" || r.Band == band).ToList();
                SumAll((st < 0 ? Waves : new[] { st }).SelectMany(w => rows.Select(r => r.At(w))), out int n, out var ts);
                long p = ts.PlankPastes;
                long[] h = ts.PlankBaseHist ?? new long[UnitTally.RestHistSize];
                long Rng(int lo, int hi) => h.Skip(lo).Take(hi - lo + 1).Sum();
                string P(long x) => Pct(x, p);
                Console.WriteLine("| " + band + " | " + WaveLab(st) + " | " + ((double)p / Math.Max(1, n)).ToString("F2") + " | "
                                  + (p == 0 ? "—" : ((double)ts.PlankBaseGiven / p).ToString("F1")) + " | " + (p == 0 ? "—" : ((double)ts.PlankStockUsed / p).ToString("F1")) + " | "
                                  + P(Rng(0, b24 - 1)) + " | " + P(h[b24]) + " | " + P(Rng(b24 + 1, UnitTally.RestHistSize - 1)) + " | "
                                  + string.Join(" / ", new[] { Rng(6, 11), Rng(12, 17), Rng(18, 23), h[24], Rng(25, 30), Rng(31, UnitTally.RestHistSize - 1) }.Select(P)) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## Q0-5 応急処置の出番（W0 のまま・札は無い）と粛");
        Console.WriteLine();
        Console.WriteLine("**出番/戦** ＝ 応急処置の条件（破片 0・生きている・HP < 最大HP × " + FirstAidTrait.Percent + "%）を満たす被弾が起きたターンの数（1ターン1回・ツギが生きている間）／ "
                          + "**粛の間** ＝ そのうち粛の保持者が生きていた割合 ／ **跨ぎ** ＝ その一撃で閾値を跨いだ割合 ／ "
                          + "**条件の駒** ＝ 条件を満たした（戦 × 駒）/戦 ／ **生き延びた** ＝ その駒がその戦を生き延びた割合（W0 は応急処置が無いので、W2 の対照）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 波 | 出番/戦 | 粛の間 | 跨ぎ | 条件の駒/戦 | 生き延びた |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach (var band in new[] { "compare", "差し替え", "合算" })
            foreach (int st in Waves.Append(-1))
            {
                var rows = WRows("W0").Where(r => band == "合算" || r.Band == band).ToList();
                var ags = (st < 0 ? Waves : new[] { st }).SelectMany(w => rows.Select(r => r.At(w))).ToList();
                SumAll(ags, out int n, out var ts);
                int nu = ags.Sum(a => a.NeedNoAidUnits), ns = ags.Sum(a => a.NeedNoAidSurvived);
                Console.WriteLine("| " + band + " | " + WaveLab(st) + " | " + ((double)ts.FirstAidChance / Math.Max(1, n)).ToString("F2") + " | "
                                  + Pct(ts.FirstAidChanceHushed, ts.FirstAidChance) + " | " + Pct(ts.FirstAidChanceCross, ts.FirstAidChance) + " | "
                                  + ((double)nu / Math.Max(1, n)).ToString("F2") + " | " + Pct(ns, nu) + " |");
            }
        Console.WriteLine();
        int rowsC = TsugiRows().Count(), rowsS = LiliRows().Count();
        Console.WriteLine("セルの内訳: 在席 " + rowsC + " 行 × 4 波 ＋ 差し替え " + rowsS + " 行 × 4 波 ＝ " + (rowsC + rowsS) * 4 + " セル、うち第二波（粛）は " + (rowsC + rowsS) + " セル（25.0%）。");
        Console.WriteLine();
    }

    // =================================================================================
    // run4 —— §4.1
    // =================================================================================

    static void Run210()
    {
        Console.WriteLine("# 第210期 `tsugi run4` —— ツギの在席 3 行 × W0〜W3（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("セルは第2 / 3 / 4 / 5 波・seed 0..199。**全員生存** ＝ 全員生存勝ち（全戦に占める割合）。**膠着** は 800 戦中の 30 ターン上限。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 勝率 | 平均 | 全員生存 | 平均 | 倒れた | 決着T | 膠着 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|--:|--:|");
        var sumW = new Dictionary<string, double>(); var sumC = new Dictionary<string, double>(); var sumT = new Dictionary<string, double>();
        int n = 0;
        foreach (var (name, f) in TsugiRows())
        {
            n++;
            foreach (var (tag, t) in WVersions)
            {
                var ag = Waves.Select(st => Measure(name + "|" + tag, ApplyR(f, "tsugi", t), st)).ToArray();
                double mw = ag.Average(x => x.Win), mc = ag.Average(x => x.CleanAll), mt = ag.Average(x => x.Turns);
                sumW[tag] = sumW.GetValueOrDefault(tag) + mw; sumC[tag] = sumC.GetValueOrDefault(tag) + mc; sumT[tag] = sumT.GetValueOrDefault(tag) + mt;
                Console.WriteLine("| " + name + " | " + tag + " | " + string.Join(" / ", ag.Select(x => F1(x.Win))) + " | **" + F1(mw) + "** | "
                                  + string.Join(" / ", ag.Select(x => F1(x.CleanAll))) + " | **" + F1(mc) + "** | "
                                  + ag.Average(x => x.Fallen).ToString("F2") + " | " + mt.ToString("F2") + " | " + ag.Sum(x => x.Stall) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("在席 " + n + " 行の平均（勝率 ／ 全員生存 ／ 決着T）: "
                          + string.Join(" ・ ", WVersions.Select(v => v.Tag + " " + F1(sumW[v.Tag] / n) + " / " + F1(sumC[v.Tag] / n) + " / " + (sumT[v.Tag] / n).ToString("F2"))) + "。");
        Console.WriteLine();
    }

    // =================================================================================
    // swap4 —— §4.2
    // =================================================================================

    static void Swap210()
    {
        Console.WriteLine("# 第210期 `tsugi swap4` —— リリの席にツギ（L ＝ リリ・第206期の規定 ／ W0〜W3）");
        Console.WriteLine();
        Console.WriteLine("セルは **勝率 / 全員生存**（全戦に占める割合）。seed 0..199。ドハは規定（分かちは破片の後ろ）。");
        Console.WriteLine();
        var rows = LiliRows().ToList();
        var tags = new[] { "L", "W0", "W1", "W2", "W3" };
        Agg Get(string name, Formation f, string tag, int st)
            => tag == "L" ? Measure(name + "|L", f, st) : Measure(name + "|S" + tag, ApplyR(f, "lili", WVersions.First(x => x.Tag == tag).Tsugi), st);

        Console.WriteLine("| 行 | 波 | " + string.Join(" | ", tags) + " |");
        Console.WriteLine("|---|--:|" + string.Concat(tags.Select(_ => "---|")));
        var beatW = tags.ToDictionary(t => t, _ => 0); var beatC = tags.ToDictionary(t => t, _ => 0);
        var winSum = new Dictionary<(string, int), double>(); var cleanSum = new Dictionary<(string, int), double>();
        foreach (var (name, f) in rows)
            foreach (int st in Waves)
            {
                var a = tags.ToDictionary(t => t, t => Get(name, f, t, st));
                foreach (var t in tags)
                {
                    winSum[(t, st)] = winSum.GetValueOrDefault((t, st)) + a[t].Win;
                    cleanSum[(t, st)] = cleanSum.GetValueOrDefault((t, st)) + a[t].CleanAll;
                    if (t == "L") continue;
                    if (a[t].Win > a["L"].Win) beatW[t]++;
                    if (a[t].CleanAll > a["L"].CleanAll) beatC[t]++;
                }
                Console.WriteLine("| " + name + " | " + (st + 1) + " | " + string.Join(" | ", tags.Select(t => F1(a[t].Win) + " / " + F1(a[t].CleanAll))) + " |");
            }
        Console.WriteLine();
        int n = rows.Count;
        Console.WriteLine("## 波ごとの平均（" + n + " 行）");
        Console.WriteLine();
        Console.WriteLine("| 波 | " + string.Join(" | ", tags.Select(t => t + " 勝率 / 全員生存")) + " |");
        Console.WriteLine("|--:|" + string.Concat(tags.Select(_ => "---|")));
        foreach (int st in Waves)
            Console.WriteLine("| " + (st + 1) + " | " + string.Join(" | ", tags.Select(t => F1(winSum[(t, st)] / n) + " / " + F1(cleanSum[(t, st)] / n))) + " |");
        Console.WriteLine();
        Console.WriteLine("**ツギがリリを上回るセル（" + n * 4 + " セル中）**: 勝率 " + string.Join(" ／ ", tags.Skip(1).Select(t => t + " " + beatW[t]))
                          + "・全員生存 " + string.Join(" ／ ", tags.Skip(1).Select(t => t + " " + beatC[t])) + "。");
        Console.WriteLine();
        Console.WriteLine("**第五波の勝率（" + n + " 行の平均）**: " + string.Join(" ／ ", tags.Select(t => t + " " + F1(winSum[(t, 4)] / n))) + "。");
        Console.WriteLine();

        Console.WriteLine("## 倒れた味方・決着ターン・膠着（第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("**倒れた** は1戦あたりの平均、**決着T** は平均、**膠着** は 800 戦中の 30 ターン上限。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 倒れた " + string.Join(" / ", tags) + " | 決着T " + string.Join(" / ", tags) + " | 膠着 " + string.Join(" / ", tags) + " |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var (name, f) in rows)
        {
            var ag = tags.ToDictionary(t => t, t => Waves.Select(st => Get(name, f, t, st)).ToArray());
            Console.WriteLine("| " + name + " | " + string.Join(" / ", tags.Select(t => ag[t].Average(x => x.Fallen).ToString("F2"))) + " | "
                              + string.Join(" / ", tags.Select(t => ag[t].Average(x => x.Turns).ToString("F1"))) + " | "
                              + string.Join(" / ", tags.Select(t => ag[t].Sum(x => x.Stall))) + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // ledger4 —— §4.3〜§4.5
    // =================================================================================

    static void Ledger210()
    {
        Console.WriteLine("# 第210期 `tsugi ledger4` —— 板と反射・腕・応急処置の帳簿（ツギの在席 3 行 ＋ 差し替え 8 行を合算・1戦あたり）");
        Console.WriteLine();

        Console.WriteLine("## 手番の板と反射");
        Console.WriteLine();
        Console.WriteLine("**板/枚** ＝ 手番の板1枚の量（基本 ＋ 腕 ＋ 在庫）／ **残り** ＝ 反射の瞬間に残っていた破片の平均 ／ **反射 回・量/本・合計・倒した** は1戦あたり。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 貼った/戦 | 板/枚（基本 ＋ 腕 ＋ 在庫） | 反射/戦 | 残り | 量/本 | 合計/戦 | 削った/戦 | 倒した/戦 |");
        Console.WriteLine("|---|--:|--:|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (tag, _) in WVersions)
            foreach (int st in Waves.Append(-1))
            {
                var ags = (st < 0 ? Waves : new[] { st }).SelectMany(w => WRows(tag).Select(r => r.At(w)));
                var sum = SumAll(ags, out int n, out var ts);
                long p = ts.PlankPastes, c = sum.ReflectCount;
                string P(double x) => (x / Math.Max(1, n)).ToString("F2");
                Console.WriteLine("| " + tag + " | " + WaveLab(st) + " | " + P(p) + " | "
                                  + (p == 0 ? "—" : ((double)ts.PlankGiven / p).ToString("F1") + "（" + ((double)ts.PlankBaseGiven / p).ToString("F1") + " ＋ "
                                     + ((double)ts.PlankSkillGiven / p).ToString("F1") + " ＋ " + ((double)ts.PlankStockUsed / p).ToString("F1") + "）") + " | "
                                  + P(c) + " | " + (c == 0 ? "—" : ((double)sum.ReflectRestSum / c).ToString("F1")) + " | "
                                  + (c == 0 ? "—" : ((double)sum.ReflectNominal / c).ToString("F1")) + " | " + P(sum.ReflectNominal) + " | " + P(sum.ReflectDealt) + " | " + P(sum.ReflectKills) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 手番の板の厚さ（在庫を除いた分）の分布（第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 〜11 | 12〜17 | 18〜23 | 24 | 25〜28 | 29〜33 | 34〜38 | 39+ |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (tag, _) in WVersions)
        {
            SumAll(Waves.SelectMany(w => WRows(tag).Select(r => r.At(w))), out _, out var ts);
            long[] h = ts.PlankBaseHist ?? new long[UnitTally.RestHistSize];
            long R(int lo, int hi) => h.Skip(lo).Take(hi - lo + 1).Sum();
            Console.WriteLine("| " + tag + " | " + string.Join(" | ", new[] { R(0, 11), R(12, 17), R(18, 23), h[24], R(25, 28), R(29, 33), R(34, 38), R(39, UnitTally.RestHistSize - 1) }
                                                                   .Select(x => Pct(x, ts.PlankPastes))) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("W3 の腕の段ごとの厚さ: " + string.Join(" ／ ", Enumerable.Range(0, 5).Select(k => "段" + k + " " + 24 * (100 + PlankTrait.SkillPerStepPercent * k) / 100)) + "。");
        Console.WriteLine();

        Console.WriteLine("## 腕（W3）");
        Console.WriteLine();
        Console.WriteLine("**累計/戦** ＝ 砕かれた破片の累計 ／ **終わりの段 0 / 1 / 2 / 3+** ＝ 戦の終わりの段の分布 ／ **段1・段2・段3 に届いたT** ＝ 届いた戦の平均ターン。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 波 | 累計/戦 | 終わりの段 0 / 1 / 2 / 3+ | 段1 のT | 段2 のT | 段3 のT |");
        Console.WriteLine("|---|--:|--:|---|--:|--:|--:|");
        foreach (var band in new[] { "compare", "差し替え" })
            foreach (int st in Waves.Append(-1))
            {
                var ags = (st < 0 ? Waves : new[] { st }).SelectMany(w => WRows("W3").Where(r => r.Band == band).Select(r => r.At(w)));
                SumAll(ags, out int n, out var ts);
                long[] re = ts.PlankSkillReach ?? new long[4], rt = ts.PlankSkillReachTurn ?? new long[4];
                long[] end = { n - re[1], re[1] - re[2], re[2] - re[3], re[3] };
                Console.WriteLine("| " + band + " | " + WaveLab(st) + " | " + ((double)ts.PlankSkillLost / Math.Max(1, n)).ToString("F1") + " | "
                                  + string.Join(" / ", end.Select(x => Pct(x, n))) + " | "
                                  + string.Join(" | ", new[] { 1, 2, 3 }.Select(k => re[k] == 0 ? "—" : ((double)rt[k] / re[k]).ToString("F1"))) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 応急処置（W2・W3）");
        Console.WriteLine();
        Console.WriteLine("**出番/戦** ＝ 条件を満たす被弾が起きたターン（計数の探り）／ **貼った/戦** ＝ 応急処置の回数 ／ **量** ＝ 1回の量 ／ **T** ＝ 貼ったターンの平均 ／ "
                          + "**跨ぎ** ＝ 閾値を跨いだ一撃で貼った割合 ／ **自分** ＝ ツギ自身に貼った割合 ／ **粛** ・ **1回を使い切り** ・ **痺れ等** ＝ 止まった回数/戦 ／ "
                          + "**受けた駒の生存** ＝ 受けた（戦 × 駒）がその戦を生き延びた割合 ／ **受けない駒の生存** ＝ 条件を満たしたが受けなかった駒。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 帯 | 波 | 出番/戦 | 貼った/戦 | 量 | T | 跨ぎ | 自分 | 粛 | 使い切り | 痺れ等 | 受けた駒の生存 | 受けない駒の生存 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var tag in new[] { "W1", "W2", "W3" })
            foreach (var band in new[] { "compare", "差し替え" })
                foreach (int st in Waves.Append(-1))
                {
                    var ags = (st < 0 ? Waves : new[] { st }).SelectMany(w => WRows(tag).Where(r => r.Band == band).Select(r => r.At(w))).ToList();
                    SumAll(ags, out int n, out var ts);
                    long fa = ts.FirstAidFired;
                    string P(double x) => (x / Math.Max(1, n)).ToString("F2");
                    int au = ags.Sum(a => a.AidUnits), asv = ags.Sum(a => a.AidSurvived), nu = ags.Sum(a => a.NeedNoAidUnits), ns = ags.Sum(a => a.NeedNoAidSurvived);
                    Console.WriteLine("| " + tag + " | " + band + " | " + WaveLab(st) + " | " + P(ts.FirstAidChance) + " | " + P(fa) + " | "
                                      + (fa == 0 ? "—" : ((double)ts.FirstAidPaste / fa).ToString("F1")) + " | " + (fa == 0 ? "—" : ((double)ts.FirstAidTurnSum / fa).ToString("F1")) + " | "
                                      + Pct(ts.FirstAidCross, fa) + " | " + Pct(ts.FirstAidSelf, fa) + " | " + P(ts.FirstAidHushed) + " | " + P(ts.FirstAidSpent) + " | " + P(ts.FirstAidHeld) + " | "
                                      + Pct(asv, au) + "（" + au + "）| " + Pct(ns, nu) + "（" + nu + "）|");
                }
        Console.WriteLine();

        Console.WriteLine("### 応急処置を受けた駒（W3・第2〜5波・回/戦）");
        Console.WriteLine();
        foreach (var band in new[] { "compare", "差し替え" })
        {
            var ags = Waves.SelectMany(w => WRows("W3").Where(r => r.Band == band).Select(r => r.At(w))).ToList();
            int n = ags.Sum(a => a.N);
            var by = new Dictionary<string, long>();
            foreach (var a in ags) foreach (var (id, t) in a.T) by[id] = by.GetValueOrDefault(id) + t.FirstAidReceived;
            long tot = by.Values.Sum();
            Console.WriteLine("- " + band + ": " + string.Join(" ／ ", by.Where(p => p.Value > 0).OrderByDescending(p => p.Value)
                .Select(p => (UnitCatalog.Everyone.FirstOrDefault(u => u.Id == p.Key)?.Name ?? p.Key) + " " + ((double)p.Value / Math.Max(1, n)).ToString("F2") + "（" + Pct(p.Value, tot) + "）")));
        }
        Console.WriteLine();

        Console.WriteLine("## 行ごと（第2〜5波の合算・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 板/枚 W0 / W1 / W2 / W3 | 反射 量/本 W0 / W1 / W2 / W3 | 反射 合計/戦 | 応急 W2 / W3 | 腕の累計 W3 |");
        Console.WriteLine("|---|---|---|---|---|---|--:|");
        foreach (var r0 in WRows("W0"))
        {
            var per = WVersions.Select(v => WRows(v.Tag).First(r => r.Band == r0.Band && r.Name == r0.Name))
                               .Select(r => { var s = SumAll(Waves.Select(w => r.At(w)), out int n, out var ts); return (s, ts, n); }).ToArray();
            Console.WriteLine("| " + r0.Band + " | " + r0.Name + " | "
                              + string.Join(" / ", per.Select(p => p.ts.PlankPastes == 0 ? "—" : ((double)p.ts.PlankGiven / p.ts.PlankPastes).ToString("F1"))) + " | "
                              + string.Join(" / ", per.Select(p => p.s.ReflectCount == 0 ? "—" : ((double)p.s.ReflectNominal / p.s.ReflectCount).ToString("F1"))) + " | "
                              + string.Join(" / ", per.Select(p => ((double)p.s.ReflectNominal / Math.Max(1, p.n)).ToString("F1"))) + " | "
                              + string.Join(" / ", per.Skip(2).Select(p => ((double)p.ts.FirstAidFired / Math.Max(1, p.n)).ToString("F2"))) + " | "
                              + ((double)per[3].ts.PlankSkillLost / Math.Max(1, per[3].n)).ToString("F1") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## §4.5 強すぎの確認（第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("**張り付き** ＝ 第2〜5波すべて 100.0% の行の数 ／ **情報セル** ＝ 0 < 勝率 < 100 の行 × 波の数（帯A・seed 0..199）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 版 | 張り付き | 情報セル |");
        Console.WriteLine("|---|---|--:|--:|");
        foreach (var band in new[] { "compare", "差し替え" })
            foreach (var (tag, _) in WVersions)
            {
                var rows = WRows(tag).Where(r => r.Band == band).ToList();
                int stuck = rows.Count(r => Waves.All(w => r.At(w).Win >= 100.0));
                int info = rows.Sum(r => Waves.Count(w => r.At(w).Win > 0 && r.At(w).Win < 100));
                Console.WriteLine("| " + band + " | " + tag + " | " + stuck + " / " + rows.Count + " | " + info + " |");
            }
        Console.WriteLine();
    }

    // =================================================================================
    // check4 —— §6
    // =================================================================================

    static void Check210(string arg)
    {
        Console.WriteLine("# 第210期 `tsugi check4` —— 自己検査");
        Console.WriteLine();
        bool all = true;
        void Req(bool ok, string what) { all &= ok; Console.WriteLine("- " + (ok ? "○" : "**×**") + " " + what); }

        // (1) W0 が第209期の表と一致（ツギの 3 行）
        string path = string.IsNullOrWhiteSpace(arg) ? ".tmp/balance209.md" : arg.Trim();
        if (File.Exists(path))
        {
            var old = File.ReadAllLines(path).Where(l => l.StartsWith("| ")).Select(l => l.Split('|').Select(c => c.Trim()).ToArray())
                          .Where(c => c.Length >= 8).GroupBy(c => c[1]).ToDictionary(g => g.Key, g => g.First().Skip(2).Take(5).ToArray());
            int bad = 0, cells = 0;
            foreach (var (name, f) in TsugiRows())
            {
                if (!old.TryGetValue(name, out var o)) { bad++; continue; }
                Formation g = ApplyR(f, "tsugi", WVersions[0].Tsugi);
                for (int st = 0; st < 5; st++)
                {
                    int wins = 0;
                    Formation enemy = EnemyCatalog.Stages[st].Enemy;
                    Parallel.For(0, 200, seed => { if (BattleEngine.Run(g, enemy, seed, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
                    cells++;
                    if ((100.0 * wins / 200).ToString("F1") + "%" != o[st]) bad++;
                }
            }
            Req(bad == 0, "(1) W0 が第209期の表と一致（ツギの 3 行）: " + cells + " セル中ずれ " + bad + "（ツギを含まない行は `compare` 全体の差分で見る）");
        }
        else Console.WriteLine("- (1) 第209期の表が無い（" + path + "）");

        // (2) verbose の有無で勝敗・決着T が同じ（W3）
        int mism = 0;
        foreach (var f in TsugiRows().Select(r => ApplyR(r.F, "tsugi", WVersions[3].Tsugi)).Concat(LiliRows().Select(r => ApplyR(r.F, "lili", WVersions[3].Tsugi))))
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 30; seed++)
                {
                    var a = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    var b = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) mism++;
                }
        Req(mism == 0, "(2) verbose の有無で勝敗・決着T が同じ（W3・台本の処理が盤面を動かさない）: ずれ " + mism);
        Console.WriteLine("  - 新しい処理（基本・応急処置・腕・計数）は `ctx.PickOne` も `Roll` も呼ばない（応急処置の相手は通知の順・腕は累計だけ）。");

        // (3) 基本の厚さが敵の攻撃力に依存しない（式の検査）
        {
            var tsugi = BattleEngine.Materialize(new Formation { [2] = WVersions[1].Tsugi }, BattleContext.PlayerTeam)[0];
            var weak = BattleEngine.Materialize(EnemyCatalog.Stages[0].Enemy, BattleContext.EnemyTeam);
            var strong = BattleEngine.Materialize(EnemyCatalog.Stages[4].Enemy, BattleContext.EnemyTeam);
            var (b1, _) = PlankTrait.Thickness(tsugi, weak);
            var (b2, _) = PlankTrait.Thickness(tsugi, strong);
            var (b3, _) = PlankTrait.Thickness(tsugi, Array.Empty<UnitState>());
            Req(b1 == b2 && b2 == b3 && b1 == tsugi.MaxHp * PlankTrait.BasePercent / 100,
                "(3) 基本の厚さは敵に依らない（第一波 " + b1 + " ／ 第五波 " + b2 + " ／ 敵なし " + b3 + " ＝ floor(" + tsugi.MaxHp + " × " + PlankTrait.BasePercent + "%)）");
        }

        // (4)〜(7) 台本（W1〜W3）
        foreach (var (tag, tsugiDef) in WVersions.Skip(1))
        {
            long pastes = 0, badBase = 0, badSum = 0, badSkillPart = 0, aids = 0, aidArmored = 0, aidDead = 0, aidTwice = 0, aidHushed = 0,
                 skillUps = 0, skillBad = 0, skillDown = 0, lostMismatch = 0, skillWhenNo = 0, battles = 0, shrunk = 0;
            var tables = TsugiRows().Select(r => ApplyR(r.F, "tsugi", tsugiDef)).Concat(LiliRows().Select(r => ApplyR(r.F, "lili", tsugiDef))).ToList();
            bool hasAid = tsugiDef.Traits.Contains(TraitId.FirstAid), hasSkill = tsugiDef.Traits.Contains(TraitId.PlankSkill);
            foreach (Formation f in tables)
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 30; seed++)
                    {
                        battles++;
                        var players = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                        var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam);
                        var me = players.First(p => p.Def.Id == "tsugi");
                        int maxHp = me.MaxHp;
                        var r = BattleEngine.Run(players, enemies, seed, verbose: true);
                        // InstanceId は Run の中（`BattleContext.Add`）で振られるので、Run の後に引く
                        var hushIds = enemies.Where(e => e.HasTrait(TraitId.Hush)).Select(e => e.InstanceId).ToHashSet();
                        int lastBase = int.MaxValue;
                        int tier = 0; var aidTurns = new HashSet<int>(); var deadHush = new HashSet<int>();
                        foreach (var e in r.Events)
                        {
                            if (e.Kind == BattleEventKind.Death && e.TargetId is int d && hushIds.Contains(d)) deadHush.Add(d);
                            if (e.Kind != BattleEventKind.Plank) continue;
                            if (e.Text == PlankLabels.Skill)
                            {
                                skillUps++;
                                if (e.Slot <= tier) skillDown++;
                                if (PlankTrait.SkillTierOf(e.Amount) != e.Slot || e.Amount < PlankTrait.SkillStep * e.Slot * (e.Slot + 1) / 2) skillBad++;
                                tier = e.Slot;
                                continue;
                            }
                            if (e.Text != PlankLabels.Paste && e.Text != PlankLabels.FirstAid) continue;
                            pastes++;
                            int b = e.PlankBase ?? -1, sk = e.PlankSkill ?? -1;
                            // 最大HP は「その時点の値」（縫い合わせ・継ぎ目で減る）。台本は最大HPを運ばないので、
                            // 「開戦時の値以下のある最大HPの 40%」であり、戦のあいだ増えないことを見る。
                            if (b > maxHp * PlankTrait.BasePercent / 100 || b > lastBase || !Enumerable.Range(1, maxHp).Any(m => m * PlankTrait.BasePercent / 100 == b)) badBase++;
                            if (b < maxHp * PlankTrait.BasePercent / 100) shrunk++;
                            lastBase = b;
                            if (b + sk + e.Slot != e.Amount) badSum++;
                            if (sk != (hasSkill ? b * (100 + PlankTrait.SkillPerStepPercent * tier) / 100 - b : 0)) badSkillPart++;
                            if (e.Text != PlankLabels.FirstAid) continue;
                            aids++;
                            if ((e.StatusRemaining ?? 0) - e.Amount != 0) aidArmored++;
                            if (e.HpAfter <= 0) aidDead++;
                            if (!aidTurns.Add(e.Turn)) aidTwice++;
                            if (hushIds.Count > 0 && deadHush.Count < hushIds.Count) aidHushed++;
                        }
                        var tt = r.TallyByUnit.GetValueOrDefault("tsugi") ?? new UnitTally();
                        long lost = r.TallyByUnit.Values.Sum(t => t.ReflectLost + t.ReflectWastedLost);
                        if (hasSkill && tt.PlankSkillLost != lost) lostMismatch++;
                        if (!hasSkill && skillUps > 0) skillWhenNo++;
                    }
            Req(pastes > 0 && badBase == 0 && badSum == 0 && badSkillPart == 0,
                "(4) " + tag + ": 板の量 ＝ 基本（floor(その時点の最大HP × 40%)）＋ 腕 ＋ 在庫（" + pastes + " 枚中 基本のずれ " + badBase + " ／ 和のずれ " + badSum + " ／ 腕の分のずれ " + badSkillPart
                + "・最大HPが減った後の板 " + shrunk + "）");
            if (hasAid)
                Req(aids > 0 && aidArmored == 0 && aidDead == 0 && aidTwice == 0 && aidHushed == 0,
                    "(5) " + tag + ": 応急処置は破片 0 の味方だけ・倒れた味方に貼らない・1ターン1回・粛の保持者が生きている間は出ない（" + aids + " 件中 破片あり " + aidArmored
                    + " ／ 倒れた " + aidDead + " ／ 同じターン2回目 " + aidTwice + " ／ 粛の間 " + aidHushed + "）");
            else Req(aids == 0, "(5) " + tag + ": 応急処置の札が無ければ 0 件（" + aids + "）");
            if (hasSkill)
                Req(skillUps > 0 && skillBad == 0 && skillDown == 0 && lostMismatch == 0,
                    "(6) " + tag + ": 腕の閾値が三角数どおり・下がらない・累計 ＝ 反射の失った破片の累計（段の上がり " + skillUps + " 件中 閾値のずれ " + skillBad
                    + " ／ 下がった " + skillDown + " ／ 累計のずれ " + lostMismatch + " 戦 / " + battles + " 戦）");
            else Req(skillUps == 0, "(6) " + tag + ": 腕の札が無ければ段は上がらない（" + skillUps + "）");
        }

        // (7) 応急処置は連鎖しない: 割り込み・反撃の中の条件は止まり、止まった回数は計数で見える
        {
            var ag = WRows("W3").SelectMany(r => Waves.Select(w => r.At(w))).ToList();
            SumAll(ag, out int n, out var ts);
            Req(ts.FirstAidFired <= ts.FirstAidChance,
                "(7) W3: 貼った回数 ≤ 出番のターン数（" + ts.FirstAidFired + " ≤ " + ts.FirstAidChance + "）・割り込み／反撃の中で止まった " + ts.FirstAidHeld + " 回（痺れ等を含む）・粛 " + ts.FirstAidHushed + " 回");
        }
        Console.WriteLine();
        Console.WriteLine(all ? "**全項目 ○。**" : "**× がある。**");
    }

    // =================================================================================
    // stall4 —— 事後: 差し替えの `反撃` / `反撃改` の第四波が W1 で落ちる理由
    // =================================================================================

    static void Stall210()
    {
        Console.WriteLine("# 第210期 `tsugi stall4` —— 差し替えの `反撃` / `反撃改` の第四波（事後に足した・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("**膠着** ＝ 30 ターン上限の負け（200 戦中）。以下の量は**全戦**の1戦あたり。**与** ＝ 味方が敵に与えた総量 ／ **反射** ＝ そのうち反射 ／ **棘** ＝ カドの与えた量 ／ "
                          + "**カド被** ＝ カドが HP で受けた量 ／ **敵の回復** ＝ 敵が回復で実際に増やした HP ／ **板** ＝ 手番の板1枚の量 ／ **応急** ＝ 応急処置/戦。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 版 | 勝率 | 膠着 | 決着T | 与 | 反射 | 棘 | カド被 | 敵の回復 | 板 | 応急 |");
        Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string row in new[] { "反撃 (ヒサ×カド)", "反撃改 (ドハ×カド)" })
        {
            var (_, f) = CompareBuilds().First(r => r.Name == row);
            var players = f.Occupied().Select(o => o.Def.Id).ToHashSet();
            foreach (int st in new[] { 3, 4 })
            {
                Formation enemy = EnemyCatalog.Stages[st].Enemy;
                var foes = enemy.Occupied().Select(o => o.Def.Id).ToHashSet();
                foreach (var (tag, def) in new[] { ("L", (UnitDef?)null) }.Concat(WVersions.Select(v => (v.Tag, (UnitDef?)v.Tsugi))))
                {
                    Formation g = def is null ? f : ApplyR(f, "lili", def);
                    int wins = 0, stalls = 0; double turns = 0, dealt = 0, refl = 0, thorn = 0, kadoTaken = 0, heal = 0, pastes = 0, given = 0, aid = 0;
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        var r = BattleEngine.Run(g, enemy, seed, verbose: false);
                        if (r.PlayerWon) wins++;
                        else if (r.Turns >= BattleEngine.MaxTurns) stalls++;
                        turns += r.Turns;
                        foreach (var (id, t) in r.TallyByUnit)
                        {
                            if (players.Contains(id) || id == "tsugi") { dealt += t.DamageToEnemy; refl += t.ReflectDealt; }
                            if (foes.Contains(id)) heal += t.Healed;
                        }
                        var k = r.TallyByUnit.GetValueOrDefault("kado");
                        thorn += k?.DamageToEnemy ?? 0; kadoTaken += k?.DamageTaken ?? 0;
                        var ts = r.TallyByUnit.GetValueOrDefault("tsugi");
                        if (ts is not null) { pastes += ts.PlankPastes; given += ts.PlankGiven; aid += ts.FirstAidFired; }
                    }
                    double d = Seeds;
                    Console.WriteLine("| " + row + " | " + (st + 1) + " | " + tag + " | " + F1(100.0 * wins / Seeds) + " | " + stalls + " | " + (turns / d).ToString("F1") + " | "
                                      + (dealt / d).ToString("F0") + " | " + (refl / d).ToString("F0") + " | " + (thorn / d).ToString("F0") + " | " + (kadoTaken / d).ToString("F0") + " | "
                                      + (heal / d).ToString("F0") + " | " + (pastes == 0 ? "—" : (given / pastes).ToString("F1")) + " | " + (aid / d).ToString("F2") + " |");
                }
            }
        }
        Console.WriteLine();
    }
}
