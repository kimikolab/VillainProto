using BattleCore;
using static Common;

// =====================================================================================
// tsugi モード（第209期） —— ツギの反射に「残った板の厚さ」を乗せる（倍率を並べる）
//
// 指示書は design/PHASE209_TSUGI3_SPEC.md ／ 報告は design/PHASE209_TSUGI3.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 tsugi phase209  # Q0-1〜Q0-3（R0 ＝ 第208期の規定で数える）
//     dotnet run --project BattleSim -c Release 0 tsugi run3      # ツギの在席 3 行 × R0〜R3
//     dotnet run --project BattleSim -c Release 0 tsugi swap3     # リリの席にツギ（L ／ R0〜R3）
//     dotnet run --project BattleSim -c Release 0 tsugi ledger3   # 反射の帳簿（版ごと・波ごと）＋ 強すぎの確認
//     dotnet run --project BattleSim -c Release 0 tsugi check3 [第208期のbalance.md]  # 自己検査
//     dotnet run --project BattleSim -c Release 0 tsugi stall3   # 差し替えの `反撃改` の膠着の内訳（事後に足した）
//
// 版は札の差し替えだけ: R0 ＝ [Plank, PlankScorch, Scrap, PlankRebound]（第208期 U3）／ R1 ＝ ＋ PlankThick25 ／
// R2 ＝ ＋ PlankThick（規定・50%）／ R3 ＝ ＋ PlankThick100。ドハはどれも規定（`SharerArmored`）。
// **版の定義はこのファイルの中だけで作る**（partial class の静的初期化子はファイルをまたぐと順序が決まらない・第196期）。
// =====================================================================================

static partial class TsugiDiag
{
    static readonly TraitId[] R0Traits = { TraitId.Plank, TraitId.PlankScorch, TraitId.Scrap, TraitId.PlankRebound };

    static UnitDef RDef(TraitId? thick)
        => Clone(UnitCatalog.Tsugi, thick is TraitId t ? R0Traits.Append(t).ToArray() : R0Traits);

    static readonly (string Tag, UnitDef Tsugi, int Ratio)[] RVersions =
    {
        ("R0", RDef(null), 0), ("R1", RDef(TraitId.PlankThick25), 25), ("R2", RDef(TraitId.PlankThick), 50), ("R3", RDef(TraitId.PlankThick100), 100),
    };

    static Formation ApplyR(Formation f, string seat, UnitDef tsugi)
    {
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef u) in f.Occupied()) g[slot] = u.Id == seat ? tsugi : u;
        return g;
    }

    static partial void RunMore209(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "phase209": Phase209(); handled = true; return;
            case "run3": Run209(); handled = true; return;
            case "swap3": Swap209(); handled = true; return;
            case "ledger3": Ledger209(); handled = true; return;
            case "check3": Check209(arg); handled = true; return;
            case "stall3": Stall209(); handled = true; return;
        }
    }

    /// <summary>ツギの在席行（`tsugi` の席）と差し替え行（`lili` の席）を版 <paramref name="tag"/> で。</summary>
    static IEnumerable<(string Band, string Name, Func<int, Agg> At)> AllRows(string tag)
    {
        var v = RVersions.First(x => x.Tag == tag);
        foreach (var (name, f) in TsugiRows())
            yield return ("compare", name, st => Measure(name + "|" + tag, ApplyR(f, "tsugi", v.Tsugi), st));
        foreach (var (name, f) in LiliRows())
            yield return ("差し替え", name, st => Measure(name + "|S" + tag, ApplyR(f, "lili", v.Tsugi), st));
    }

    static UnitTally SumAll(IEnumerable<Agg> ags, out int n, out UnitTally tsugi)
    {
        var sum = new UnitTally(); tsugi = new UnitTally(); n = 0;
        foreach (var a in ags) { n += a.N; foreach (var t in a.T.Values) sum.Add(t); tsugi.Add(a.Of("tsugi")); }
        return sum;
    }

    static string Pct(long a, long b) => b == 0 ? "—" : (100.0 * a / b).ToString("F1") + "%";

    // =================================================================================
    // phase209 —— Q0-1〜Q0-3（R0 で数える）
    // =================================================================================

    static void Phase209()
    {
        Console.WriteLine("# 第209期 `tsugi phase209` —— Q0-1〜Q0-3（R0 ＝ 第208期の規定・seed 0..199・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("台はツギの在席 3 行（`compare`）と、リリの在席 8 行のリリの席にツギ（差し替え）。**盤面は第208期のまま**で、足したのは計数だけ。");
        Console.WriteLine();

        Console.WriteLine("## Q0-1 反射が起きた瞬間に残っていた破片");
        Console.WriteLine();
        Console.WriteLine("**残り** ＝ その一撃を受けた後に板を持つ味方に残っていた破片（ツギの板か別の書き手の破片かを問わない）。**見当** ＝ 同じ反射に倍率を当てたときの1本あたりの上乗せ（floor で丸めた平均）。盤面が動くので実測とは違う。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 波 | 反射/戦 | 失った/本 | 残り 平均 | 中央値 | 0 の割合 | 見当 +25% | +50% | +100% |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var band in new[] { "compare", "差し替え", "合算" })
        {
            foreach (int st in Waves.Append(-1))
            {
                var rows = AllRows("R0").Where(r => band == "合算" || r.Band == band).ToList();
                var ags = (st < 0 ? Waves : new[] { st }).SelectMany(w => rows.Select(r => r.At(w)));
                var sum = SumAll(ags, out int n, out _);
                long[] h = sum.ReflectRestHist ?? new long[UnitTally.RestHistSize];
                long c = sum.ReflectCount;
                string Hyp(int r) => c == 0 ? "—" : (h.Select((x, i) => x * (long)(i * r / 100)).Sum() / (double)c).ToString("F1");
                Console.WriteLine("| " + band + " | " + (st < 0 ? "**2〜5**" : (st + 1).ToString()) + " | " + ((double)c / Math.Max(1, n)).ToString("F2") + " | "
                                  + (c == 0 ? "—" : ((double)sum.ReflectLost / c).ToString("F1")) + " | "
                                  + (c == 0 ? "—" : ((double)sum.ReflectRestSum / c).ToString("F1")) + " | " + Median(h) + " | "
                                  + Pct(sum.ReflectRestZero, c) + " | " + Hyp(25) + " | " + Hyp(50) + " | " + Hyp(100) + " |");
            }
        }
        Console.WriteLine();
        {
            var sum = SumAll(Waves.SelectMany(w => AllRows("R0").Select(r => r.At(w))), out _, out _);
            long[] h = sum.ReflectRestHist ?? new long[UnitTally.RestHistSize];
            long c = Math.Max(1, sum.ReflectCount);
            int[] edges = { 0, 1, 6, 11, 21, 31, 51, UnitTally.RestHistSize - 1 };
            Console.WriteLine("残りの分布（合算・第2〜5波）: " + string.Join(" ／ ", Enumerable.Range(0, edges.Length).Select(i =>
            {
                int lo = edges[i], hi = i + 1 < edges.Length ? edges[i + 1] - 1 : UnitTally.RestHistSize - 1;
                long x = h.Skip(lo).Take(hi - lo + 1).Sum();
                string lab = lo == hi ? (lo == UnitTally.RestHistSize - 1 ? lo + "+" : lo.ToString()) : lo + "〜" + hi;
                return lab + " " + (100.0 * x / c).ToString("F1") + "%";
            })) + "（最後の欄は " + (UnitTally.RestHistSize - 1) + " 以上）。");
            Console.WriteLine();
        }

        Console.WriteLine("## Q0-2 敵の1回の攻撃が味方の2体以上に当たったとき、板を持っていた数");
        Console.WriteLine();
        Console.WriteLine("**複数/戦** ＝ 敵の1回の攻撃（`PerformAttack`）がツギの陣営の2体以上に当たった回数/戦 ／ **当たった/回** ＝ そのとき当たった体数の平均 ／ "
                          + "**板の割合** ＝ 当たった延べ数のうち撃ち返す板の印を持っていた割合 ／ **板の数 0/1/2/3+** ＝ 1回の攻撃で板を持っていた体数の分布。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 波 | 複数/戦 | 当たった/回 | 板の割合 | 板の数 0/1/2/3+ | 反射の本数 1/2/3/4+ |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|---|");
        foreach (var band in new[] { "compare", "差し替え" })
            foreach (int st in Waves.Append(-1))
            {
                var rows = AllRows("R0").Where(r => r.Band == band).ToList();
                var ags = (st < 0 ? Waves : new[] { st }).SelectMany(w => rows.Select(r => r.At(w)));
                SumAll(ags, out int n, out var ts);
                long[] ph = ts.MultiHitPlankedHist ?? new long[4], gh = ts.ReflectGroupHist ?? new long[4];
                long m = ts.MultiHitAttacks, gs = gh.Sum();
                Console.WriteLine("| " + band + " | " + (st < 0 ? "**2〜5**" : (st + 1).ToString()) + " | " + ((double)m / Math.Max(1, n)).ToString("F2") + " | "
                                  + (m == 0 ? "—" : ((double)ts.MultiHitTargets / m).ToString("F2")) + " | " + Pct(ts.MultiHitPlanked, ts.MultiHitTargets) + " | "
                                  + (m == 0 ? "—" : string.Join("/", ph.Select(x => (100.0 * x / m).ToString("F0")))) + " | "
                                  + (gs == 0 ? "—" : string.Join("/", gh.Select(x => (100.0 * x / gs).ToString("F0")))) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## Q0-3 第四波（軛・上限 " + YokeTrait.Cap + "）で上限を超える反射の見当");
        Console.WriteLine();
        Console.WriteLine("軛が効いている間の反射に、倍率 0 / 25 / 50 / 100% を当てたと仮定して返す量が上限を超える割合（**盤面は R0 のまま**）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 軛の間の反射/戦 | 0% | 25% | 50% | 100% |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (var band in new[] { "compare", "差し替え", "合算" })
        {
            var rows = AllRows("R0").Where(r => band == "合算" || r.Band == band).ToList();
            var sum = SumAll(rows.Select(r => r.At(3)), out int n, out _);
            long[] hy = sum.ReflectYokeHyp ?? new long[4];
            Console.WriteLine("| " + band + " | " + ((double)sum.ReflectYoke / Math.Max(1, n)).ToString("F2") + " | "
                              + string.Join(" | ", hy.Select(x => Pct(x, sum.ReflectYoke))) + " |");
        }
        Console.WriteLine();
    }

    static string Median(long[] h)
    {
        long tot = h.Sum();
        if (tot == 0) return "—";
        long acc = 0;
        for (int i = 0; i < h.Length; i++) { acc += h[i]; if (acc * 2 >= tot) return i == h.Length - 1 ? i + "+" : i.ToString(); }
        return "—";
    }

    // =================================================================================
    // run3 —— §4.1
    // =================================================================================

    static void Run209()
    {
        Console.WriteLine("# 第209期 `tsugi run3` —— ツギの在席 3 行 × R0〜R3（**線は置かない**）");
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
            foreach (var (tag, t, _) in RVersions)
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
                          + string.Join(" ・ ", RVersions.Select(v => v.Tag + " " + F1(sumW[v.Tag] / n) + " / " + F1(sumC[v.Tag] / n) + " / " + (sumT[v.Tag] / n).ToString("F2"))) + "。");
        Console.WriteLine();
    }

    // =================================================================================
    // swap3 —— §4.2
    // =================================================================================

    static void Swap209()
    {
        Console.WriteLine("# 第209期 `tsugi swap3` —— リリの席にツギ（L ＝ リリ・第206期の規定 ／ R0〜R3）");
        Console.WriteLine();
        Console.WriteLine("セルは **勝率 / 全員生存**（全戦に占める割合）。seed 0..199。ドハは規定（分かちは破片の後ろ）。");
        Console.WriteLine();
        var rows = LiliRows().ToList();
        var tags = new[] { "L", "R0", "R1", "R2", "R3" };
        Agg Get(string name, Formation f, string tag, int st)
            => tag == "L" ? Measure(name + "|L", f, st) : Measure(name + "|S" + tag, ApplyR(f, "lili", RVersions.First(x => x.Tag == tag).Tsugi), st);

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
        Console.WriteLine("**ツギがリリを上回るセル（32 セル中）**: 勝率 " + string.Join(" ／ ", tags.Skip(1).Select(t => t + " " + beatW[t]))
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
    // ledger3 —— §4.3・§4.4
    // =================================================================================

    static void Ledger209()
    {
        Console.WriteLine("# 第209期 `tsugi ledger3` —— 反射の帳簿（ツギの在席 3 行 ＋ 差し替え 8 行を合算・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("**回** 返した回数/戦 ／ **量/本**（失った ＋ 厚さ）／ **合計/戦** 返した量 ／ **削った/戦** 実際に削った HP ／ **倒した/戦** 反射で倒れた敵 ／ "
                          + "**上限超え** 軛が効いている間の反射のうち返す量が " + YokeTrait.Cap + " を超えた割合 ／ **本数** 1回の敵の攻撃で返った本数 1・2・3・4以上の割合 ／ "
                          + "**主が倒れた** その攻撃の主が倒れた割合（本数ごと）／ **板の割合** 敵の1回の攻撃が2体以上に当たったとき、当たった味方のうち撃ち返す板を持っていた割合。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 回 | 量/本（失った ＋ 厚さ） | 合計/戦 | 削った/戦 | 倒した/戦 | 上限超え | 本数 1/2/3/4+ | 主が倒れた 1/2/3/4+ | 3本以上で主が倒れた/戦 | 板の割合 |");
        Console.WriteLine("|---|--:|--:|---|--:|--:|--:|--:|---|---|--:|--:|");
        foreach (var (tag, _, _) in RVersions)
            foreach (int st in Waves.Append(-1))
            {
                var ags = (st < 0 ? Waves : new[] { st }).SelectMany(w => AllRows(tag).Select(r => r.At(w)));
                var sum = SumAll(ags, out int n, out var ts);
                long c = sum.ReflectCount;
                long[] h = ts.ReflectGroupHist ?? new long[4], k = ts.ReflectGroupKilled ?? new long[4];
                long hs = h.Sum();
                string P(double x) => (x / Math.Max(1, n)).ToString("F2");
                Console.WriteLine("| " + tag + " | " + (st < 0 ? "**2〜5**" : (st + 1).ToString()) + " | " + P(c) + " | "
                                  + (c == 0 ? "—" : ((double)sum.ReflectNominal / c).ToString("F1") + "（" + ((double)sum.ReflectLost / c).ToString("F1") + " ＋ " + ((double)sum.ReflectThick / c).ToString("F1") + "）") + " | "
                                  + P(sum.ReflectNominal) + " | " + P(sum.ReflectDealt) + " | " + P(sum.ReflectKills) + " | "
                                  + (st == 3 || st < 0 ? Pct(sum.ReflectYokeOver, sum.ReflectYoke) : "—") + " | "
                                  + (hs == 0 ? "—" : string.Join("/", h.Select(x => (100.0 * x / hs).ToString("F0")))) + " | "
                                  + (hs == 0 ? "—" : string.Join("/", h.Zip(k).Select(p => p.First == 0 ? "—" : (100.0 * p.Second / p.First).ToString("F0")))) + " | "
                                  + P(k[2] + k[3]) + " | " + Pct(ts.MultiHitPlanked, ts.MultiHitTargets) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 行ごと（第2〜5波の合算・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 量/本 R0 / R1 / R2 / R3 | 合計/戦 R0 / R1 / R2 / R3 | 倒した/戦 R0 / R1 / R2 / R3 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var r0 in AllRows("R0"))
        {
            var per = RVersions.Select(v => AllRows(v.Tag).First(r => r.Band == r0.Band && r.Name == r0.Name))
                               .Select(r => SumAll(Waves.Select(w => r.At(w)), out int n, out _) is var s ? (s, n) : default).ToArray();
            Console.WriteLine("| " + r0.Band + " | " + r0.Name + " | "
                              + string.Join(" / ", per.Select(p => p.s.ReflectCount == 0 ? "—" : ((double)p.s.ReflectNominal / p.s.ReflectCount).ToString("F1"))) + " | "
                              + string.Join(" / ", per.Select(p => ((double)p.s.ReflectNominal / Math.Max(1, p.n)).ToString("F1"))) + " | "
                              + string.Join(" / ", per.Select(p => ((double)p.s.ReflectKills / Math.Max(1, p.n)).ToString("F2"))) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## §4.4 強すぎの確認（第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("**張り付き** ＝ 第2〜5波すべて 100.0% の行の数 ／ **情報セル** ＝ 0 < 勝率 < 100 の行 × 波の数（帯A・seed 0..199）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 版 | 張り付き | 情報セル |");
        Console.WriteLine("|---|---|--:|--:|");
        foreach (var band in new[] { "compare", "差し替え" })
            foreach (var (tag, _, _) in RVersions)
            {
                var rows = AllRows(tag).Where(r => r.Band == band).ToList();
                int stuck = rows.Count(r => Waves.All(w => r.At(w).Win >= 100.0));
                int info = rows.Sum(r => Waves.Count(w => r.At(w).Win > 0 && r.At(w).Win < 100));
                Console.WriteLine("| " + band + " | " + tag + " | " + stuck + " / " + rows.Count + " | " + info + " |");
            }
        Console.WriteLine();
    }

    // =================================================================================
    // check3 —— §6
    // =================================================================================

    static void Check209(string arg)
    {
        Console.WriteLine("# 第209期 `tsugi check3` —— 自己検査");
        Console.WriteLine();
        bool all = true;
        void Req(bool ok, string what) { all &= ok; Console.WriteLine("- " + (ok ? "○" : "**×**") + " " + what); }

        // (1) R0 が第208期の表と一致（ツギの 3 行）
        string path = string.IsNullOrWhiteSpace(arg) ? ".tmp/balance208.md" : arg.Trim();
        if (File.Exists(path))
        {
            var old = File.ReadAllLines(path).Where(l => l.StartsWith("| ")).Select(l => l.Split('|').Select(c => c.Trim()).ToArray())
                          .Where(c => c.Length >= 8).GroupBy(c => c[1]).ToDictionary(g => g.Key, g => g.First().Skip(2).Take(5).ToArray());
            int bad = 0, cells = 0;
            foreach (var (name, f) in TsugiRows())
            {
                if (!old.TryGetValue(name, out var o)) { bad++; continue; }
                Formation g = ApplyR(f, "tsugi", RVersions[0].Tsugi);
                for (int st = 0; st < 5; st++)
                {
                    int wins = 0;
                    Formation enemy = EnemyCatalog.Stages[st].Enemy;
                    Parallel.For(0, 200, seed => { if (BattleEngine.Run(g, enemy, seed, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
                    cells++;
                    if ((100.0 * wins / 200).ToString("F1") + "%" != o[st]) bad++;
                }
            }
            Req(bad == 0, "(1) R0 が第208期の表と一致（ツギの 3 行）: " + cells + " セル中ずれ " + bad + "（ツギを含まない行は `compare` 全体の差分で見る）");
        }
        else Console.WriteLine("- (1) 第208期の表が無い（" + path + "）");

        // (2) verbose の有無で勝敗・決着T が同じ（R3）
        int mism = 0;
        foreach (var (name, f) in TsugiRows().Select(r => (r.Name, ApplyR(r.F, "tsugi", RVersions[3].Tsugi)))
                     .Concat(LiliRows().Select(r => (r.Name, ApplyR(r.F, "lili", RVersions[3].Tsugi)))))
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 30; seed++)
                {
                    var a = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    var b = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) mism++;
                }
        Req(mism == 0, "(2) verbose の有無で勝敗・決着T が同じ（R3・台本の処理が盤面を動かさない）: ずれ " + mism);
        Console.WriteLine("  - 新しい処理（倍率を読む・厚さの分を足す・計数）は `ctx.PickOne` も `Roll` も呼ばない。");

        // (3) 台本と帳簿（R1〜R3）
        foreach (var (tag, tsugi, ratio) in RVersions)
        {
            long reflects = 0, noMark = 0, notFoe = 0, badSplit = 0, broken = 0, brokenBad = 0, over = 0, thickSum = 0, lostOverSoak = 0;
            var tables = TsugiRows().Select(r => ApplyR(r.F, "tsugi", tsugi)).Concat(LiliRows().Select(r => ApplyR(r.F, "lili", tsugi))).ToList();
            foreach (Formation f in tables)
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 30; seed++)
                    {
                        var players = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                        var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam);
                        var r = BattleEngine.Run(players, enemies, seed, verbose: true);
                        var foes = enemies.Select(e => e.InstanceId).ToHashSet();
                        var planked = new HashSet<int>();
                        foreach (var e in r.Events)
                        {
                            if (e.Kind != BattleEventKind.Plank) continue;
                            if (e.Text == PlankLabels.Paste && e.TargetId is int pt) planked.Add(pt);
                            if (e.Text != PlankLabels.Reflect) continue;
                            reflects++;
                            int thick = e.StatusRemaining ?? 0, rest = e.HpAfter, lost = e.Amount - thick;
                            thickSum += thick;
                            if (e.ActorId is not int h || !planked.Contains(h)) noMark++;
                            if (e.TargetId is not int ft || !foes.Contains(ft)) notFoe++;
                            if (thick != rest * ratio / 100 || lost <= 0) badSplit++;
                            if (rest == 0) { broken++; if (e.Amount != lost) brokenBad++; }
                            if (e.Amount > lost + rest * ratio / 100.0) over++;
                        }
                        foreach (var t in r.TallyByUnit.Values)
                            if (t.ReflectLost + t.ReflectWastedLost > t.PlankSoaked) lostOverSoak++;
                    }
            Req(reflects > 0 && noMark == 0 && notFoe == 0,
                "(3a) " + tag + ": 板の印の無い味方は返さない・返す先は敵（反射 " + reflects + " 件中 印なし " + noMark + " ／ 敵でない " + notFoe + "）");
            Req(badSplit == 0 && over == 0,
                "(3b) " + tag + ": 1本の反射は「失った量 ＋ floor(残り × " + ratio + "%)」ちょうどで、それを超えない（ずれ " + badSplit + " ／ 超え " + over + "・厚さの分の合計 " + thickSum + "）");
            Req(broken > 0 && brokenBad == 0, "(3c) " + tag + ": 板が割れきった反射（残り 0）は倍率に関係なく失った量と等しい（" + broken + " 件中ずれ " + brokenBad + "）");
            Req(lostOverSoak == 0, "(3d) " + tag + ": 返した量のうち失った破片の分は、印がある間に失った破片の量を超えない（駒ごと・超えた駒 " + lostOverSoak + "）");
            if (ratio == 0) Req(thickSum == 0, "(3e) R0: 厚さの分は 0（第208期と同じ量）: " + thickSum);
        }
        Console.WriteLine();
        Console.WriteLine(all ? "**全項目 ○。**" : "**× がある。**");
    }

    // =================================================================================
    // stall3 —— 事後: 差し替えの `反撃改` の膠着が倍率で減らない理由
    // =================================================================================

    static void Stall209()
    {
        const string Row = "反撃改 (ドハ×カド)";
        Console.WriteLine("# 第209期 `tsugi stall3` —— 差し替えの `" + Row + "` の膠着（事後に足した・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("**膠着** ＝ 30 ターン上限の負け（200 戦中）。以下の量は**膠着した戦だけ**の1戦あたり。**与** ＝ 味方が敵に与えた総量 ／ **反射** ＝ そのうち反射 ／ **棘** ＝ カドの与えた量 ／ "
                          + "**敵の回復** ＝ 敵が回復で実際に増やした HP ／ **量/本** ＝ 反射1本の量 ／ **残り** ＝ 反射の瞬間の残り破片の平均。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 勝率 | 膠着 | 与 | 反射 | 棘 | 敵の回復 | 反射/戦 | 量/本 | 残り |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var (_, f) = CompareBuilds().First(r => r.Name == Row);
        var players = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        foreach (int st in new[] { 1, 3, 4 })
        {
            Formation enemy = EnemyCatalog.Stages[st].Enemy;
            var foes = enemy.Occupied().Select(o => o.Def.Id).ToHashSet();
            foreach (var (tag, def) in new[] { ("L", (UnitDef?)null) }.Concat(RVersions.Select(v => (v.Tag, (UnitDef?)v.Tsugi))))
            {
                Formation g = def is null ? f : ApplyR(f, "lili", def);
                int wins = 0, stalls = 0; double dealt = 0, refl = 0, thorn = 0, heal = 0, cnt = 0, nom = 0, rest = 0;
                for (int seed = 0; seed < Seeds; seed++)
                {
                    var r = BattleEngine.Run(g, enemy, seed, verbose: false);
                    if (r.PlayerWon) wins++;
                    if (r.PlayerWon || r.Turns < BattleEngine.MaxTurns) continue;
                    stalls++;
                    foreach (var (id, t) in r.TallyByUnit)
                    {
                        if (players.Contains(id) || id == "tsugi") { dealt += t.DamageToEnemy; refl += t.ReflectDealt; cnt += t.ReflectCount; nom += t.ReflectNominal; rest += t.ReflectRestSum; }
                        if (foes.Contains(id)) heal += t.Healed;
                    }
                    thorn += r.TallyByUnit.GetValueOrDefault("kado")?.DamageToEnemy ?? 0;
                }
                double d = Math.Max(1, stalls);
                Console.WriteLine("| " + (st + 1) + " | " + tag + " | " + F1(100.0 * wins / Seeds) + " | " + stalls + " | " + (dealt / d).ToString("F0") + " | " + (refl / d).ToString("F0") + " | "
                                  + (thorn / d).ToString("F0") + " | " + (heal / d).ToString("F0") + " | " + (cnt / d).ToString("F1") + " | "
                                  + (cnt == 0 ? "—" : (nom / cnt).ToString("F1")) + " | " + (cnt == 0 ? "—" : (rest / cnt).ToString("F1")) + " |");
            }
        }
        Console.WriteLine();
    }
}
