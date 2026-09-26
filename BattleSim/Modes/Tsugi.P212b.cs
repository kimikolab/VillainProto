using BattleCore;
using static Common;

// =====================================================================================
// tsugi モード（第212期）の本体 —— 版（Z0〜Z3）と run6 / swap6 / pon6 / ledger6 / z3diff / check6
//
//     dotnet run --project BattleSim -c Release 0 tsugi run6      # ツギの在席 3 行 × Z0〜Z3
//     dotnet run --project BattleSim -c Release 0 tsugi swap6     # リリの席にツギ（L ／ Z0〜Z3）
//     dotnet run --project BattleSim -c Release 0 tsugi pon6      # ポンの編成（パターン2・ガン入り）× Z0〜Z3
//     dotnet run --project BattleSim -c Release 0 tsugi ledger6   # 応急処置・出撃前の板・身構えの帳簿 ＋ 強すぎの確認
//     dotnet run --project BattleSim -c Release 0 tsugi z3diff    # Z2 と Z3 を compare 61 行 × 5 波で
//     dotnet run --project BattleSim -c Release 0 tsugi check6 [第211期のbalance.md]  # 自己検査
//
// 版は札の差し替えだけ: Z0 ＝ 第211期 Y3 ／ Z1 ＝ 腕（`PlankSkill`）を `AidSkill` に ／ Z2 ＝ ＋ `PlankOpening` ／
// Z3 ＝ Z2 のツギ ＋ ササに `BraceArmored`。カドはどれも第211期の規定（`ThornsArmored`）。
// **版の定義はこのファイルの中だけで作る**（partial class の静的初期化子の順・第196期）。
// =====================================================================================

static partial class TsugiDiag
{
    static readonly TraitId[] Z0Traits =
    {
        TraitId.Plank, TraitId.PlankScorch, TraitId.Scrap, TraitId.PlankRebound, TraitId.PlankThick,
        TraitId.PlankBase, TraitId.FirstAid, TraitId.PlankSkill, TraitId.PlankNeediest, TraitId.FirstAidArmored,
    };

    static UnitDef ZTsugi(bool aid, bool opening)
    {
        var t = Z0Traits.Select(x => aid && x == TraitId.PlankSkill ? TraitId.AidSkill : x).ToList();
        if (opening) t.Add(TraitId.PlankOpening);
        return Clone(UnitCatalog.Tsugi, t.ToArray());
    }

    static readonly UnitDef Sasa0 = Clone(UnitCatalog.Sasa, UnitCatalog.Sasa.Traits.Where(t => t != TraitId.BraceArmored).ToArray());
    static readonly UnitDef Sasa3 = Clone(UnitCatalog.Sasa, UnitCatalog.Sasa.Traits.Where(t => t != TraitId.BraceArmored).Append(TraitId.BraceArmored).ToArray());
    static readonly UnitDef KadoY3 = Clone(UnitCatalog.Kado, UnitCatalog.Kado.Traits.Where(t => t != TraitId.ThornsArmored).Append(TraitId.ThornsArmored).ToArray());

    static readonly (string Tag, UnitDef Tsugi, UnitDef Sasa)[] ZVersions =
    {
        ("Z0", ZTsugi(false, false), Sasa0),
        ("Z1", ZTsugi(true, false), Sasa0),
        ("Z2", ZTsugi(true, true), Sasa0),
        ("Z3", ZTsugi(true, true), Sasa3),
    };

    static readonly string[] ZTags = { "Z0", "Z1", "Z2", "Z3" };

    static Formation ApplyZ(Formation f, string seat, string tag)
    {
        var v = ZVersions.First(x => x.Tag == tag);
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef u) in f.Occupied())
            g[slot] = u.Id == seat ? v.Tsugi : u.Id == "sasa" ? v.Sasa : u.Id == "kado" ? KadoY3 : u;
        return g;
    }

    static IEnumerable<(string Band, string Name, Func<int, Agg> At)> ZRows(string tag)
    {
        foreach (var (name, f) in TsugiRows())
            yield return ("compare", name, st => Measure(name + "|" + tag, ApplyZ(f, "tsugi", tag), st));
        foreach (var (name, f) in LiliRows())
            yield return ("差し替え", name, st => Measure(name + "|S" + tag, ApplyZ(f, "lili", tag), st));
        yield return ("ポンの編成", "パターン2・ガン入り", st => Measure("pon|" + tag, ApplyZ(PonFormation(), "tsugi", tag), st));
    }

    static partial void RunMore212b(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run6": Run212(); handled = true; return;
            case "swap6": Swap212(); handled = true; return;
            case "pon6": Pon212(); handled = true; return;
            case "ledger6": Ledger212(); handled = true; return;
            case "z3diff": Z3Diff(); handled = true; return;
            case "check6": Check212(arg); handled = true; return;
            case "sasa6": Sasa212(); handled = true; return;
            default: RunMore213(mode, arg, ref handled); return;
        }
    }

    static partial void RunMore213(string mode, string arg, ref bool handled);

    // =================================================================================
    // run6 —— §4.2
    // =================================================================================

    static void Run212()
    {
        Console.WriteLine("# 第212期 `tsugi run6` —— ツギの在席 3 行 × Z0〜Z3（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("セルは第2 / 3 / 4 / 5 波・seed 0..199。**全員生存** ＝ 全員生存勝ち（全戦に占める割合）。**膠着** は 800 戦中の 30 ターン上限。");
        Console.WriteLine("**Z0** ＝ 第211期 Y3 ／ **Z1** ＝ 腕を応急処置の回数に ／ **Z2** ＝ ＋ 出撃前の板 ／ **Z3** ＝ ＋ ササの身構えは破片の前に。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 勝率 | 平均 | 全員生存 | 平均 | 倒れた | 決着T | 膠着 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|--:|--:|");
        var sumW = new Dictionary<string, double>(); var sumC = new Dictionary<string, double>(); var sumT = new Dictionary<string, double>(); var sumF = new Dictionary<string, double>();
        int n = 0;
        foreach (var (name, _) in TsugiRows())
        {
            n++;
            foreach (var tag in ZTags)
            {
                var r = ZRows(tag).First(x => x.Band == "compare" && x.Name == name);
                var ag = Waves.Select(r.At).ToArray();
                double mw = ag.Average(x => x.Win), mc = ag.Average(x => x.CleanAll), mt = ag.Average(x => x.Turns), mf = ag.Average(x => x.Fallen);
                sumW[tag] = sumW.GetValueOrDefault(tag) + mw; sumC[tag] = sumC.GetValueOrDefault(tag) + mc;
                sumT[tag] = sumT.GetValueOrDefault(tag) + mt; sumF[tag] = sumF.GetValueOrDefault(tag) + mf;
                Console.WriteLine("| " + name + " | " + tag + " | " + string.Join(" / ", ag.Select(x => F1(x.Win))) + " | **" + F1(mw) + "** | "
                                  + string.Join(" / ", ag.Select(x => F1(x.CleanAll))) + " | **" + F1(mc) + "** | "
                                  + mf.ToString("F2") + " | " + mt.ToString("F2") + " | " + ag.Sum(x => x.Stall) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("在席 " + n + " 行の平均（勝率 ／ 全員生存 ／ 倒れた ／ 決着T）: "
                          + string.Join(" ・ ", ZTags.Select(t => t + " " + F1(sumW[t] / n) + " / " + F1(sumC[t] / n) + " / " + (sumF[t] / n).ToString("F2") + " / " + (sumT[t] / n).ToString("F2"))) + "。");
        Console.WriteLine();
    }

    // =================================================================================
    // swap6 —— §4.3
    // =================================================================================

    static void Swap212()
    {
        Console.WriteLine("# 第212期 `tsugi swap6` —— リリの席にツギ（L ＝ リリ・第206期の規定 ／ Z0〜Z3）");
        Console.WriteLine();
        Console.WriteLine("セルは **勝率 / 全員生存**（全戦に占める割合）。seed 0..199。L のカドは第211期の規定（破片で受けても棘は鳴る）。");
        Console.WriteLine();
        var rows = LiliRows().ToList();
        var tags = new[] { "L" }.Concat(ZTags).ToArray();
        Agg Get(string name, Formation f, string tag, int st)
            => tag == "L" ? Measure(name + "|L3", ReplaceKado(f, KadoY3), st) : ZRows(tag).First(r => r.Band == "差し替え" && r.Name == name).At(st);

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
        Console.WriteLine("## 倒れた味方・決着ターン・膠着（第2〜5波）");
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
    // pon6 —— §4.1
    // =================================================================================

    static void Pon212()
    {
        Console.WriteLine("# 第212期 `tsugi pon6` —— ポンの編成（パターン2・中衛・上 ガン ／ 後衛 ツギ ／ 中衛・中央 ドルガ ／ 前衛 ササ ／ 中衛・下 セロ）× Z0〜Z3");
        Console.WriteLine();
        Console.WriteLine("seed 0..199。**ササ倒れ** ＝ ササが倒れた戦の割合・（ ）は倒れたターンの平均 ／ **身構え** ＝ 切った回数/戦（切り落とした量/戦）・配った量/戦 ／ "
                          + "**破片より先** ＝ 破片より先に上限で切った回数/戦（Z3）／ **受け切りで弾いた** ＝ 破片が受け切った一撃で弾きと配りを起こした回数/戦（Z3）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 勝率 | 全員生存 | ササ倒れ | 倒れた駒 | 応急/戦 | 止まった/戦 | 出撃前の板/戦 | 身構え 切った（量）・配った | 破片より先 | 受け切りで弾いた |");
        Console.WriteLine("|--:|---|--:|--:|---|---|--:|--:|--:|---|--:|--:|");
        Formation pf = PonFormation();
        foreach (int st in Waves)
            foreach (var tag in ZTags)
            {
                Formation g = ApplyZ(pf, "tsugi", tag);
                var a = Measure("pon|" + tag, g, st);
                var s = a.Of("sasa"); var ts = a.Of("tsugi");
                double deathT = SasaDeathTurn(g, st, out int deaths);
                string fallen = string.Join("・", a.FallenBy.Where(p => p.Value > 0).OrderByDescending(p => p.Value)
                    .Select(p => (UnitCatalog.Everyone.FirstOrDefault(u => u.Id == p.Key)?.Name ?? p.Key) + " " + F1(100.0 * p.Value / Math.Max(1, a.N)) + "%"));
                Console.WriteLine("| " + (st + 1) + " | " + tag + " | " + F1(a.Win) + " | " + F1(a.CleanAll) + " | "
                                  + F1(100.0 * a.FallenBy.GetValueOrDefault("sasa") / Math.Max(1, a.N)) + "%" + (deaths == 0 ? "" : "（T" + deathT.ToString("F1") + "）") + " | "
                                  + (fallen == "" ? "—" : fallen) + " | " + a.Per(ts.FirstAidFired).ToString("F2") + " | " + a.Per(ts.FirstAidSpent).ToString("F2") + " | "
                                  + a.Per(ts.OpeningPastes).ToString("F2") + " | " + a.Per(s.BraceCuts).ToString("F2") + "（" + a.Per(s.BraceRefused).ToString("F1") + "）・" + a.Per(s.BraceGiven).ToString("F1") + " | "
                                  + a.Per(s.BraceArmorEarly).ToString("F2") + " | " + a.Per(s.BraceArmorStruck).ToString("F2") + " |");
            }
        Console.WriteLine();
        Console.WriteLine("## 1戦ずつ（画面の seed 1 第三波・seed 6 第五波）");
        Console.WriteLine();
        foreach (var (st, seed) in new[] { (2, 1), (4, 6) })
            foreach (var tag in ZTags)
            {
                Formation g = ApplyZ(pf, "tsugi", tag);
                var players = BattleEngine.Materialize(g, BattleContext.PlayerTeam);
                var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam);
                var r = BattleEngine.Run(players, enemies, seed, verbose: true);
                var sasa = players.First(p => p.Def.Id == "sasa");
                string Nm(int? id) => (players.FirstOrDefault(p => p.InstanceId == id)?.Name ?? "?").Replace("継ぎ当ての", "").Replace("錯乱の", "").Replace("のろまの巨兵", "").Replace("逃亡兵", "").Replace("鬨の号令", "");
                var pastes = r.Events.Where(e => e.Kind == BattleEventKind.Plank && (e.Text == PlankLabels.Paste || e.Text == PlankLabels.FirstAid || e.Text == PlankLabels.Opening))
                    .Select(e => "T" + e.Turn + (e.Text == PlankLabels.FirstAid ? "応急" + e.AidOrdinal : e.Text == PlankLabels.Opening ? "出撃前" : "") + "→" + Nm(e.TargetId) + " " + e.Amount);
                var sd = r.Events.FirstOrDefault(e => e.Kind == BattleEventKind.Death && e.TargetId == sasa.InstanceId);
                Console.WriteLine("- 第" + (st + 1) + "波 seed " + seed + " " + tag + ": " + (r.PlayerWon ? "勝" : "負") + " " + r.Turns + "T・ササ" + (sd is null ? "生存（HP " + Math.Max(0, sasa.Hp) + "）" : "倒れ（T" + sd.Turn + "）")
                                  + " ／ 板: " + string.Join("、", pastes));
            }
        Console.WriteLine();
    }

    static double SasaDeathTurn(Formation g, int st, out int deaths)
    {
        long sum = 0; int n = 0;
        object lk = new();
        Parallel.For(0, Seeds, seed =>
        {
            var players = BattleEngine.Materialize(g, BattleContext.PlayerTeam);
            var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam);
            var r = BattleEngine.Run(players, enemies, seed, verbose: true);
            var sasa = players.First(p => p.Def.Id == "sasa");
            var d = r.Events.FirstOrDefault(e => e.Kind == BattleEventKind.Death && e.TargetId == sasa.InstanceId);
            if (d is null) return;
            lock (lk) { sum += d.Turn; n++; }
        });
        deaths = n;
        return n == 0 ? 0 : (double)sum / n;
    }

    // =================================================================================
    // ledger6 —— §4.4・§4.5・§4.7
    // =================================================================================

    static void Ledger212()
    {
        Console.WriteLine("# 第212期 `tsugi ledger6` —— 応急処置・出撃前の板・身構えの帳簿（1戦あたり・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("## 応急処置");
        Console.WriteLine();
        Console.WriteLine("**出番** ＝ 条件を満たす被弾が起きたターン（計数の探り・破片で受け切った一撃の後も含む）／ **貼った** ／ **止まった** ＝ 1ターンの上限で貼れなかった ／ **比** ＝ 止まった ÷ 貼った（R315）／ "
                          + "**2回目以降** ＝ 同じターンの2回目以降の応急処置 ／ **同じ味方へ** ＝ そのうち直前の応急処置と同じ味方 ／ **段ごと** ＝ 段 0 / 1 / 2 / 3+ で貼った回数 ／ **受けた駒の生存**。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 帯 | 出番 | 貼った | 止まった | 比 | 2回目以降 | 同じ味方へ | 段ごと 0 / 1 / 2 / 3+ | 受けた駒の生存 | 受けない駒の生存 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|---|---|---|");
        foreach (var tag in ZTags)
            foreach (var band in new[] { "compare", "差し替え", "ポンの編成" })
            {
                var ags = Waves.SelectMany(w => ZRows(tag).Where(r => r.Band == band).Select(r => r.At(w))).ToList();
                SumAll(ags, out int n, out var ts);
                string P(long x) => ((double)x / Math.Max(1, n)).ToString("F2");
                long[] ft = ts.FirstAidFiredByTier ?? new long[4];
                int au = ags.Sum(a => a.AidUnits), asv = ags.Sum(a => a.AidSurvived), nu = ags.Sum(a => a.NeedNoAidUnits), ns = ags.Sum(a => a.NeedNoAidSurvived);
                Console.WriteLine("| " + tag + " | " + band + " | " + P(ts.FirstAidChance + ts.FirstAidArmoredHits) + " | " + P(ts.FirstAidFired) + " | " + P(ts.FirstAidSpent) + " | "
                                  + (ts.FirstAidFired == 0 ? "—" : ((double)ts.FirstAidSpent / ts.FirstAidFired).ToString("F2")) + " | " + P(ts.FirstAidMulti) + " | " + P(ts.FirstAidSameAgain) + " | "
                                  + string.Join(" / ", ft.Select(P)) + " | " + Pct(asv, au) + "（" + au + "） | " + Pct(ns, nu) + "（" + nu + "） |");
            }
        Console.WriteLine();

        Console.WriteLine("## 出撃前の板と1ターン目");
        Console.WriteLine();
        Console.WriteLine("**出撃前の板** ＝ 枚数/戦（量/枚）／ **1T 破片** ＝ 1ターン目に破片が吸った被ダメ/戦 ／ **1T 反射** ＝ 1ターン目に板から返った反射/戦 ／ **1T 前衛倒れ** ＝ 前列で始めた駒が1ターン目に倒れた戦の割合 ／ **前衛倒れ** ＝ 戦の中で倒れた割合。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 帯 | 出撃前の板 | 1T 破片 | 1T 反射 | 1T 前衛倒れ | 前衛倒れ |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|");
        foreach (var tag in ZTags)
            foreach (var band in new[] { "compare", "差し替え", "ポンの編成" })
            {
                var rows = ZRows(tag).Where(r => r.Band == band).ToList();
                var ags = Waves.SelectMany(w => rows.Select(r => r.At(w))).ToList();
                var sum = SumAll(ags, out int n, out var ts);
                double early = FrontFellTurn1(tag, band);
                string P(long x) => ((double)x / Math.Max(1, n)).ToString("F2");
                Console.WriteLine("| " + tag + " | " + band + " | " + P(ts.OpeningPastes) + (ts.OpeningPastes == 0 ? "" : "（" + ((double)ts.OpeningGiven / ts.OpeningPastes).ToString("F1") + "）") + " | "
                                  + ((double)sum.FirstTurnArmorSoak / Math.Max(1, n)).ToString("F1") + " | " + ((double)sum.FirstTurnReflect / Math.Max(1, n)).ToString("F1") + " | "
                                  + F1(early) + "% | " + F1(100.0 * ags.Sum(a => a.FrontFell) / Math.Max(1, n)) + "% |");
            }
        Console.WriteLine();

        Console.WriteLine("## §4.7 強すぎの確認");
        Console.WriteLine();
        Console.WriteLine("**張り付き** ＝ 第2〜5波すべて 100.0% の行の数 ／ **情報セル** ＝ 0 < 勝率 < 100 の行 × 波の数（帯A・seed 0..199）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 版 | 張り付き | 情報セル |");
        Console.WriteLine("|---|---|--:|--:|");
        foreach (var band in new[] { "compare", "差し替え" })
            foreach (var tag in ZTags)
            {
                var rows = ZRows(tag).Where(r => r.Band == band).ToList();
                int stuck = rows.Count(r => Waves.All(w => r.At(w).Win >= 100.0));
                int info = rows.Sum(r => Waves.Count(w => r.At(w).Win > 0 && r.At(w).Win < 100));
                Console.WriteLine("| " + band + " | " + tag + " | " + stuck + " / " + rows.Count + " | " + info + " |");
            }
        Console.WriteLine();
    }

    /// <summary>前列で始めた駒が1ターン目に倒れた戦の割合（台本の Death のターン）。</summary>
    static double FrontFellTurn1(string tag, string band)
    {
        var forms = new List<Formation>();
        if (band == "compare") forms.AddRange(TsugiRows().Select(r => ApplyZ(r.F, "tsugi", tag)));
        else if (band == "差し替え") forms.AddRange(LiliRows().Select(r => ApplyZ(r.F, "lili", tag)));
        else forms.Add(ApplyZ(PonFormation(), "tsugi", tag));
        long hit = 0, n = 0;
        object lk = new();
        foreach (var f in forms)
            foreach (int st in Waves)
                Parallel.For(0, 50, seed =>
                {
                    var players = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam);
                    var front = players.Where(p => FormationRules.RowOf(p.Slot) == Row.Front).ToList();
                    var r = BattleEngine.Run(players, enemies, seed, verbose: true);
                    var ids = front.Select(p => p.InstanceId).ToHashSet();
                    bool fell = r.Events.Any(e => e.Kind == BattleEventKind.Death && e.Turn == 1 && e.TargetId is int d && ids.Contains(d));
                    lock (lk) { n++; if (fell) hit++; }
                });
        return 100.0 * hit / Math.Max(1, n);
    }

    // =================================================================================
    // z3diff —— §4.6
    // =================================================================================

    static void Z3Diff()
    {
        Console.WriteLine("# 第212期 `tsugi z3diff` —— `compare` 61 行 × 5 波で Z2 と Z3 を並べる（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("Z2 と Z3 の違いはササの札1枚（`BraceArmored`）だけ。");
        Console.WriteLine();
        Console.WriteLine("| 行 | ツギ | ササ | 波 | Z2 | Z3 | 差 |");
        Console.WriteLine("|---|:-:|:-:|--:|--:|--:|--:|");
        int moved = 0, movedNoTsugi = 0, cells = 0;
        foreach (var (name, f) in CompareBuilds())
        {
            Formation a = ApplyZ(f, "tsugi", "Z2"), b = ApplyZ(f, "tsugi", "Z3");
            for (int st = 0; st < 5; st++)
            {
                cells++;
                double wa = WinRate(a, st), wb = WinRate(b, st);
                if (wa == wb) continue;
                moved++;
                if (!HasTsugi(f)) movedNoTsugi++;
                Console.WriteLine("| " + name + " | " + (HasTsugi(f) ? "○" : "") + " | " + (f.Occupied().Any(o => o.Def.Id == "sasa") ? "○" : "") + " | "
                                  + (st + 1) + " | " + F1(wa) + " | " + F1(wb) + " | " + D(wb - wa) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("**動いたセル " + moved + " / " + cells + "（うちツギのいない行 " + movedNoTsugi + "）。**");
        Console.WriteLine();
    }

    // =================================================================================
    // check6 —— §6
    // =================================================================================

    static void Check212(string arg)
    {
        Console.WriteLine("# 第212期 `tsugi check6` —— 自己検査");
        Console.WriteLine();
        bool all = true;
        void Req(bool ok, string what) { all &= ok; Console.WriteLine("- " + (ok ? "○" : "**×**") + " " + what); }

        // (1) Z0 は第211期の表と一致（61 行）・Z1 / Z2 はツギを含まない行が一致
        string path = string.IsNullOrWhiteSpace(arg) ? ".tmp/balance211.md" : arg.Trim();
        if (File.Exists(path))
        {
            var old = File.ReadAllLines(path).Where(l => l.StartsWith("| ")).Select(l => l.Split('|').Select(c => c.Trim()).ToArray())
                          .Where(c => c.Length >= 8).GroupBy(c => c[1]).ToDictionary(g => g.Key, g => g.First().Skip(2).Take(5).ToArray());
            foreach (var tag in new[] { "Z0", "Z1", "Z2" })
            {
                int bad = 0, cells = 0;
                foreach (var (name, f) in CompareBuilds())
                {
                    if (tag != "Z0" && HasTsugi(f)) continue;
                    if (!old.TryGetValue(name, out var o)) { bad++; continue; }
                    Formation g = ApplyZ(f, "tsugi", tag);
                    for (int st = 0; st < 5; st++) { cells++; if (WinRate(g, st).ToString("F1") + "%" != o[st]) bad++; }
                }
                Req(bad == 0, "(1) " + tag + " が第211期の `docs/balance.md` と一致（" + (tag == "Z0" ? "61 行すべて" : "ツギを含まない行") + "）: " + cells + " セル中ずれ " + bad);
            }
        }
        else Console.WriteLine("- (1) 第211期の表が無い（" + path + "）");

        // (2) verbose の有無で勝敗・決着T が同じ（Z3）
        var tablesZ3 = TsugiRows().Select(r => ApplyZ(r.F, "tsugi", "Z3")).Concat(LiliRows().Select(r => ApplyZ(r.F, "lili", "Z3"))).Append(ApplyZ(PonFormation(), "tsugi", "Z3")).ToList();
        int mism = 0;
        foreach (var f in tablesZ3)
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 30; seed++)
                {
                    var a = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    var b = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) mism++;
                }
        Req(mism == 0, "(2) verbose の有無で勝敗・決着T が同じ（Z3・台本と計数が盤面を動かさない）: ずれ " + mism);
        Console.WriteLine("  - 新しい処理（応急処置の上限・出撃前の板・破片より先の身構え・受け切りの弾き）は `Roll` も `PickOne` も呼ばない。出撃前の板は席番号の順、弾きの宛先は第143期の `ShoveRules.Shove` のまま（乱数なし）。");

        // (3)〜(6) 台本
        foreach (var tag in ZTags)
        {
            var v = ZVersions.First(x => x.Tag == tag);
            bool aidSkill = v.Tsugi.Traits.Contains(TraitId.AidSkill), opening = v.Tsugi.Traits.Contains(TraitId.PlankOpening), brace3 = v.Sasa.Traits.Contains(TraitId.BraceArmored);
            long pastes = 0, badBase = 0, aids = 0, overLimit = 0, badOrd = 0, openings = 0, openNotFront = 0, openNotStart = 0, openBattles = 0, openMissing = 0, battles = 0, early = 0, struck = 0;
            var tables = TsugiRows().Select(r => ApplyZ(r.F, "tsugi", tag)).Concat(LiliRows().Select(r => ApplyZ(r.F, "lili", tag))).Append(ApplyZ(PonFormation(), "tsugi", tag)).ToList();
            foreach (Formation f in tables)
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 30; seed++)
                    {
                        battles++;
                        var players = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                        var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam);
                        int maxHp = players.First(p => p.Def.Id == "tsugi").MaxHp;
                        var frontUnits = players.Where(p => FormationRules.RowOf(p.Slot) == Row.Front).ToList();   // 戦の前の席で取る（戦の中でササが入れ替える）
                        var r = BattleEngine.Run(players, enemies, seed, verbose: true);
                        var startFront = frontUnits.Select(p => p.InstanceId).ToHashSet();
                        int tier = 0; var aidCount = new Dictionary<int, int>(); int openHere = 0;
                        foreach (var e in r.Events)
                        {
                            if (e.Kind != BattleEventKind.Plank) continue;
                            if (e.Text == PlankLabels.Skill) { tier = e.Slot; continue; }
                            if (e.Text == PlankLabels.Opening)
                            {
                                openings++; openHere++;
                                if (e.Turn > 0) openNotStart++;   // 開戦時（行動順ループの前）はターン 0
                                continue;
                            }
                            if (e.Text != PlankLabels.Paste && e.Text != PlankLabels.FirstAid) continue;
                            pastes++;
                            // (3) 腕の段が上がっても基本の厚さは変わらない（Z1 以降は基本 ＝ floor(最大HP × 40%) 以下・腕の分 0）
                            if (aidSkill && (e.PlankSkill ?? 0) != 0) badBase++;
                            if (e.Text != PlankLabels.FirstAid) continue;
                            aids++;
                            int c = aidCount[e.Turn] = aidCount.GetValueOrDefault(e.Turn) + 1;
                            if (c > 1 + (aidSkill ? tier : 0)) overLimit++;
                            if (e.AidOrdinal != c) badOrd++;
                        }
                        if (opening)
                        {
                            openBattles++;
                            if (openHere != startFront.Count)
                            {
                                openMissing++;
                                if (openMissing <= 3)
                                {
                                    var tgt = r.Events.Where(e => e.Kind == BattleEventKind.Plank && e.Text == PlankLabels.Opening).Select(e => players.FirstOrDefault(p => p.InstanceId == e.TargetId)?.Name ?? "?");
                                    var before = r.Events.TakeWhile(e => !(e.Kind == BattleEventKind.Plank && e.Text == PlankLabels.Opening)).Where(e => e.Turn == 0).Select(e => e.Kind + (e.Text is null ? "" : "(" + e.Text + ")"));
                                    Console.WriteLine("  - ずれの例 " + tag + " 第" + (st + 1) + "波 seed " + seed + ": 開幕の前列 " + string.Join("・", players.Where(p => startFront.Contains(p.InstanceId)).Select(p => p.Name))
                                                      + " ／ 板 " + string.Join("・", tgt) + " ／ 板より前の出来事 " + string.Join("・", before.Take(8)));
                                }
                            }
                        }
                        else if (openHere > 0) openMissing++;
                        var s = r.TallyByUnit.GetValueOrDefault("sasa");
                        if (s is not null) { early += s.BraceArmorEarly; struck += s.BraceArmorStruck; }
                    }
            Req(pastes > 0 && badBase == 0, "(3) " + tag + ": " + (aidSkill ? "腕の段が上がっても板に腕の分は乗らない" : "腕は基本の厚さに乗る（Z0 の対照）") + "（板 " + pastes + " 枚中 腕の分が乗った " + badBase + (aidSkill ? "" : "・対照なので数えない") + "）");
            Req(aids > 0 && overLimit == 0 && badOrd == 0,
                "(4) " + tag + ": 応急処置の1ターンの上限は " + (aidSkill ? "1 ＋ 腕の段" : "1") + "・台本の「何回目か」が数えた回数と一致（" + aids + " 件中 上限超え " + overLimit + " ／ 何回目のずれ " + badOrd + "）");
            Req(opening ? openBattles > 0 && openMissing == 0 && openNotStart == 0 : openMissing == 0 && openings == 0,
                "(5) " + tag + ": 出撃前の板は " + (opening ? "開戦時に1回・前列の全員に1枚ずつ（" + openings + " 枚・前列の数と違う戦 " + openMissing + " ／ 開戦後に貼った " + openNotStart + "）" : "無い（" + openings + " 枚）"));
            Req(brace3 ? early > 0 && struck > 0 : early == 0 && struck == 0,
                "(6) " + tag + ": 破片より先に上限で切った " + early + " 回・受け切りで弾いた " + struck + " 回（" + (brace3 ? "Z3 は 1 回以上" : "Z3 以外は 0") + "）");
        }

        // (7) 1回の被弾で応急処置の判定は1回: 同じ `ApplyDamage` の枠で判定が2回来た回数（engine の通し番号で数える）
        {
            long same = 0, n = 0;
            foreach (var f in tablesZ3)
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 30; seed++)
                    {
                        var r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                        var t = r.TallyByUnit.GetValueOrDefault("tsugi");
                        if (t is null) continue;
                        same += t.FirstAidSameHit; n += t.FirstAidFired;
                    }
            Req(same == 0, "(7) Z3: 1回の被弾（`ApplyDamage` の枠）につき応急処置の判定は1回（応急処置 " + n + " 件・同じ枠で2回目の判定 " + same + "）");
        }

        // (8) ササ以外は変わらない: ササのいない行は Z2 と Z3 で台本が同じ
        {
            int diff = 0, n = 0;
            foreach (var (_, f) in CompareBuilds().Concat(CrossBuilds()).Select(r => (r.Name, r.F)).Concat(LiliRows().Select(r => (r.Name, ApplyZ(r.F, "lili", "Z2")))))
            {
                if (f.Occupied().Any(o => o.Def.Id == "sasa")) continue;
                Formation a = ApplyZ(f, "tsugi", "Z2"), b = ApplyZ(f, "tsugi", "Z3");
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 10; seed++)
                    {
                        n++;
                        var ra = BattleEngine.Run(a, EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                        var rb = BattleEngine.Run(b, EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                        if (ra.Events.Count != rb.Events.Count || ra.PlayerWon != rb.PlayerWon || ra.Turns != rb.Turns) diff++;
                    }
            }
            Req(diff == 0, "(8) ササのいない行（`compare` ＋ 交差帯 ＋ 差し替え）は Z2 と Z3 で台本の長さ・勝敗・決着T が同じ（" + n + " 戦中ずれ " + diff + "）");
        }

        Console.WriteLine();
        Console.WriteLine(all ? "**全項目 ○。**" : "**× がある。**");
    }

    /// <summary>第212期（事後）: Z3 でササのいる2編成の全員生存が下がる理由（Z2 と Z3 を並べる）。</summary>
    static void Sasa212()
    {
        Console.WriteLine("# 第212期 `tsugi sasa6` —— ササのいる2編成で Z2 と Z3 を並べる（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("**弾き** ＝ ササが錯乱して味方を弾いた回数/戦 ／ **配った** ＝ 弾いた味方の破片にした量/戦 ／ **受け切りで弾いた** ＝ 破片が受け切った一撃で弾いた回数/戦（Z3）／ "
                          + "**ササの被ダメ** ＝ ササが HP で受けた量/戦 ／ **倒れた駒** ＝ 戦の割合。");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 波 | 版 | 全員生存 | 弾き | 配った | 受け切りで弾いた | 破片より先 | ササの被ダメ | 倒れた駒 |");
        Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|--:|---|");
        var forms = new[] { ("継ぎ当て×分散回復", TsugiRows().First(r => r.Name == "継ぎ当て×分散回復").F), ("ポンの編成", PonFormation()) };
        foreach (var (name, f) in forms)
            foreach (int st in Waves)
                foreach (var tag in new[] { "Z2", "Z3" })
                {
                    var a = Measure((name == "ポンの編成" ? "pon|" : name + "|") + tag, ApplyZ(f, "tsugi", tag), st);
                    var s = a.Of("sasa");
                    string fallen = string.Join("・", a.FallenBy.Where(p => p.Value > 0).OrderByDescending(p => p.Value)
                        .Select(p => (UnitCatalog.Everyone.FirstOrDefault(u => u.Id == p.Key)?.Name ?? p.Key) + " " + F1(100.0 * p.Value / Math.Max(1, a.N)) + "%"));
                    Console.WriteLine("| " + name + " | " + (st + 1) + " | " + tag + " | " + F1(a.CleanAll) + " | " + a.Per(s.BraceShoves).ToString("F2") + " | " + a.Per(s.BraceGiven).ToString("F1") + " | "
                                      + a.Per(s.BraceArmorStruck).ToString("F2") + " | " + a.Per(s.BraceArmorEarly).ToString("F2") + " | " + a.Per(s.DamageTaken).ToString("F1") + " | " + (fallen == "" ? "—" : fallen) + " |");
                }
        Console.WriteLine();
    }
}
