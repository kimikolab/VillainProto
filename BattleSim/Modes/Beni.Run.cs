using BattleCore;
using static Common;

// =====================================================================================
// beni モード（第190期）の本体 —— 版の対照・差し替え表・帳簿・診断台・自己検査
//
// **旧の駒・マイナスなし・手番なしはこの診断のローカルに写してある**（`UnitCatalog` には新しいベニしか居ない）。
// Id は本物と同じ（帳簿を同じキーで引く）。素体だけは Id を変える（第69期からの器具）。
// =====================================================================================

static partial class BeniDiag
{
    static partial void RunMoreImpl(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run": RunVersions(); handled = true; return;
            case "ledger": RunLedger(); handled = true; return;
            case "bench": RunBenches(); handled = true; return;
            case "check": Check(arg); handled = true; return;
        }
    }

    // =================================================================================
    // 版
    // =================================================================================

    static UnitDef Clone(UnitDef d, TraitId[] traits, IReadOnlyList<UnitAction>? actions) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Pattern = d.Pattern,
        Advances = d.Advances, Actions = actions, Traits = traits,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    static readonly UnitDef BeniNew = UnitCatalog.Beni;
    /// <summary>旧ベニ（第189期まで）: 毒喰らい（毒の敵の数 × 4 を味方全体へ・味方の毒 ×2）・毎手番殴る。</summary>
    static readonly UnitDef BeniOld = Clone(UnitCatalog.Beni, new[] { TraitId.Devour }, null);
    /// <summary>新・マイナスなし（`InverseLeak` を外した `yP`）。</summary>
    static readonly UnitDef BeniYP = Clone(UnitCatalog.Beni, new[] { TraitId.Inverse, TraitId.Taint }, UnitCatalog.Beni.Actions);
    /// <summary>新・手番なし（`Taint` を外す。**手番は空のまま**＝攻撃もしない。澱み分けの値だけを抜く）。</summary>
    static readonly UnitDef BeniNoTaint = Clone(UnitCatalog.Beni, new[] { TraitId.Inverse, TraitId.InverseLeak }, UnitCatalog.Beni.Actions);
    /// <summary>素体（同じ数値・特性なし・毎手番殴る）。</summary>
    static readonly UnitDef BeniPlain = new()
    {
        Id = "beni_plain", Name = "素体のベニ", MaxHp = UnitCatalog.Beni.MaxHp, Attack = UnitCatalog.Beni.Attack,
        Speed = UnitCatalog.Beni.Speed, Pattern = UnitCatalog.Beni.Pattern, Traits = Array.Empty<TraitId>(),
    };

    static readonly (string Tag, UnitDef D)[] Versions =
    {
        ("旧", BeniOld), ("新", BeniNew), ("マイナスなし", BeniYP), ("手番なし", BeniNoTaint), ("素体", BeniPlain),
    };

    static Formation Apply(Formation f, UnitDef beni)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == "beni" ? beni : d;
        return g;
    }

    static Formation SwapAt(Formation f, int slotAt, UnitDef to)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = slot == slotAt ? to : d;
        return g;
    }

    static UnitDef Plain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Pattern = d.Pattern, Traits = Array.Empty<TraitId>(),
    };

    // =================================================================================
    // 勝率
    // =================================================================================

    static double[] Rates(Formation f, int seeds = Seeds)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            Formation enemy = EnemyCatalog.Stages[st].Enemy;
            Parallel.For(0, seeds, seed =>
            {
                if (BattleEngine.Run(f, enemy, seed, verbose: false).PlayerWon) Interlocked.Increment(ref wins);
            });
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static string Cells(double[] w) => string.Join(" / ", w.Skip(1).Select(x => x.ToString("F1")));
    static string D(double x) => x.ToString("+0.0;-0.0;0.0");

    static bool HasBeni(Formation f) => f.Occupied().Any(o => o.Def.Id == "beni");

    static IEnumerable<(string Band, string Name, Formation F)> BeniRows()
    {
        foreach (var (n, f) in CompareBuilds()) if (HasBeni(f)) yield return ("compare", n, f);
        foreach (var (n, f) in CrossBuilds()) if (HasBeni(f)) yield return ("交差帯", n, f);
    }

    // =================================================================================
    // run —— 在席行の版の対照（§5.1）と差し替え表（§5.2・主表）
    // =================================================================================

    static void RunVersions()
    {
        Console.WriteLine("# 第190期 `beni run` —— 版の対照と差し替え表（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("seed 0..199。平均は第2〜5波（規約 (G10)）。**旧** ＝ 第189期までの毒喰らい（この診断のローカルに写した）。"
                          + "**マイナスなし** ＝ `InverseLeak` を外した `yP`。**手番なし** ＝ `Taint` を外した版（手番は空のまま・攻撃しない）。"
                          + "**素体** ＝ 同じ数値・特性なしで毎手番殴る。**代金 ＝ 新 − マイナスなし**（R289）。");
        Console.WriteLine();

        Console.WriteLine("## 表A. ベニの在席行 × 版（§5.1）");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 版 | 第2 / 3 / 4 / 5 波 | 第2〜5波 | 味方の毒（額面/戦） |");
        Console.WriteLine("|---|---|---|---|--:|--:|");
        foreach (var (band, name, f) in BeniRows())
        {
            bool first = true;
            foreach (var (tag, d) in Versions)
            {
                Formation g = Apply(f, d);
                double[] w = Rates(g);
                Console.WriteLine("| " + (first ? band : "") + " | " + (first ? name : "") + " | " + tag + " | " + Cells(w) + " | "
                                  + Mean25(w).ToString("F1") + " | " + AllyPoisonBite(g).ToString("F1") + " |");
                first = false;
            }
        }
        Console.WriteLine();
        Console.WriteLine("`味方の毒（額面/戦）` は `BattleResult.PoisonBitePlayer`（反転しなかった刻みの額面・ベニの ×2 を含む）。第2〜5波。");
        Console.WriteLine();

        SwapTable();
    }

    static double AllyPoisonBite(Formation f)
    {
        long sum = 0; int n = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                sum += BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PoisonBitePlayer; n++;
            }
        return (double)sum / n;
    }

    static void SwapTable()
    {
        Console.WriteLine("## 表B. 差し替え表（§5.2・**主表**・測る前に固定）");
        Console.WriteLine();
        Console.WriteLine("行 ＝ `compare` のうち毒か燃焼の書き手（グザ・スィド・ミオ・ラウ・ボルグ・ゾト・カタ）を含み、ベニを含まない行。"
                          + "替える駒 ＝ その行で**書き手以外のうち**素体差し替えの帰属が最も低い1枚（第188期 表E と同じ規則）。席はその駒の席のまま。"
                          + "**Δ元** ＝ 新 − 元の行 ／ **Δ素体** ＝ 新 − 同じ席の素体（**ベニの値はこちらで読む**）／ "
                          + "**代金** ＝ 新 − マイナスなし ／ **澱み分け** ＝ 新 − 手番なし ／ 旧 ＝ 旧ベニを同じ席に置いた値。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 替えた駒（帰属）・席 | 隣の駒 | 元 | 素体 | 旧 | **新** | Δ元 | **Δ素体** | マイナスなし | 代金 | 手番なし | 澱み分け |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var agg = new List<(string Name, double DPlain, double Cost, double Taint, bool Burn, bool Kata, bool Golm, bool Vio, bool HealerAdj)>();
        foreach (var (name, f) in CompareBuilds())
        {
            var occ = f.Occupied().ToList();
            if (!occ.Any(o => Writers.Contains(o.Def.Id)) || HasBeni(f)) continue;
            double baseW = Mean25(Rates(f));
            var low = occ.Where(o => !Writers.Contains(o.Def.Id))
                         .Select(o => (o.Slot, o.Def, A: baseW - Mean25(Rates(SwapAt(f, o.Slot, Plain(o.Def))))))
                         .OrderBy(x => x.A).ThenBy(x => x.Slot).First();
            var r = Versions.ToDictionary(v => v.Tag, v => Mean25(Rates(SwapAt(f, low.Slot, v.D))));
            var adj = occ.Where(o => o.Slot != low.Slot && FormationRules.AreAdjacent(low.Slot, o.Slot)).Select(o => o.Def).ToList();
            double dPlain = r["新"] - r["素体"], cost = r["新"] - r["マイナスなし"], taint = r["新"] - r["手番なし"];
            var ids = occ.Select(o => o.Def.Id).ToHashSet();
            agg.Add((name, dPlain, cost, taint, ids.Contains("borg") || ids.Contains("zoto"), ids.Contains("kata"),
                     adj.Any(d => d.Id == "golm"), ids.Contains("vio"), adj.Any(d => Healers.Contains(d.Id))));
            Console.WriteLine("| " + name + " | " + low.Def.Name + "（" + D(low.A) + "）・" + SlotName(low.Slot) + " | "
                              + string.Join("・", adj.Select(d => d.Name)) + " | "
                              + baseW.ToString("F1") + " | " + r["素体"].ToString("F1") + " | " + r["旧"].ToString("F1") + " | **"
                              + r["新"].ToString("F1") + "** | " + D(r["新"] - baseW) + " | **" + D(dPlain) + "** | "
                              + r["マイナスなし"].ToString("F1") + " | " + D(cost) + " | " + r["手番なし"].ToString("F1") + " | " + D(taint) + " |");
        }
        Console.WriteLine();
        if (agg.Count == 0) { Console.WriteLine("**行が 0 本。走査が空なので止める**（R034）。"); return; }

        Console.WriteLine("### 表B の群ごとの平均");
        Console.WriteLine();
        Console.WriteLine("| 群 | 行数 | Δ素体 | 代金 | 澱み分け |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        void Grp(string label, Func<(string Name, double DPlain, double Cost, double Taint, bool Burn, bool Kata, bool Golm, bool Vio, bool HealerAdj), bool> sel)
        {
            var g = agg.Where(sel).ToList();
            if (g.Count == 0) { Console.WriteLine("| " + label + " | 0 | — | — | — |"); return; }
            Console.WriteLine("| " + label + " | " + g.Count + " | " + D(g.Average(x => x.DPlain)) + " | " + D(g.Average(x => x.Cost))
                              + " | " + D(g.Average(x => x.Taint)) + " |");
        }
        Grp("全体", _ => true);
        Grp("燃焼の書き手（ボルグ・ゾト）を含む", x => x.Burn);
        Grp("カタを含む", x => x.Kata);
        Grp("燃焼の書き手もカタも含まない", x => !x.Burn && !x.Kata);
        Grp("**ゴルムがベニの隣**", x => x.Golm);
        Grp("ゴルムが隣にいない", x => !x.Golm);
        Grp("回復役（ノノ・ヴェル・シオ・ナラ）がベニの隣", x => x.HealerAdj);
        Grp("ヴィオが同席", x => x.Vio);
        Grp("ヴィオが同席しない", x => !x.Vio);
        Console.WriteLine();
        Console.WriteLine("- Δ素体 が正 " + agg.Count(x => x.DPlain > 0.05) + " ／ 負 " + agg.Count(x => x.DPlain < -0.05)
                          + " ／ 代金が負 " + agg.Count(x => x.Cost < -0.05) + " ／ 代金が正 " + agg.Count(x => x.Cost > 0.05));
        Console.WriteLine();
    }

    // =================================================================================
    // ledger —— 帳簿（§5.4）
    // =================================================================================

    readonly record struct Led(double Poison, double Burn, double Det, double Nominal, double Recip, double MaxSimul,
                               double Acts, double Dry, double Layers, double LeakFires, double LeakDealt, double LeakKills,
                               double PostBite, double VioAte, Dictionary<string, double> LeakBy, double BeniDeath);

    static Led LedgerOf(Formation f)
    {
        double po = 0, bu = 0, de = 0, no = 0, re = 0, ms = 0, ac = 0, dr = 0, la = 0, lf = 0, ld = 0, lk = 0, pb = 0, va = 0, bd = 0;
        var by = new Dictionary<string, double>();
        int n = 0;
        var names = f.Occupied().Select(o => o.Def).DistinctBy(d => d.Id).ToDictionary(d => d.Id, d => d.Name);
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                n++;
                foreach (var (id, t) in r.TallyByUnit)
                {
                    va += t.VioAteTaint;
                    if (t.InverseLeakBy > 0)
                    {
                        string nm = names.TryGetValue(id, out string? s) ? s : id;
                        by[nm] = by.GetValueOrDefault(nm) + t.InverseLeakBy;
                    }
                }
                if (!r.TallyByUnit.TryGetValue("beni", out UnitTally? b)) continue;
                po += b.InversePoisonHealed; bu += b.InverseBurnHealed; de += b.InverseDetonateHealed; no += b.InverseNominal;
                re += b.InverseRecipientTurns; ms += b.InverseMaxSimul; ac += b.TaintActs; dr += b.TaintDry; la += b.TaintLayers;
                lf += b.InverseLeakFires; ld += b.InverseLeakDealt; lk += b.InverseLeakKills; pb += b.TaintPostBite;
                bd += b.Deaths;
            }
        return new Led(po / n, bu / n, de / n, no / n, re / n, ms / n, ac / n, dr / n, la / n, lf / n, ld / n, lk / n, pb / n, va / n,
                       by.ToDictionary(kv => kv.Key, kv => kv.Value / n), bd / n);
    }

    static void PrintLedgerHeader()
    {
        Console.WriteLine("| 台／行 | 版 | 癒えた 毒 | 燃焼 | 起爆 | 名目 | 延べ人数 | 最大同時 | 澱み分け 手番 / 空振り / 層 | 裏 回数 | 裏 実額 | 裏で倒れた | 離れた後の刻み | ヴィオが吸った層 | ベニの死 | 裏の出どころ（実額） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|---|--:|--:|--:|--:|--:|--:|---|");
    }

    static void PrintLedgerRow(string name, string tag, Led l)
    {
        Console.WriteLine("| " + name + " | " + tag + " | " + l.Poison.ToString("F1") + " | " + l.Burn.ToString("F1") + " | " + l.Det.ToString("F1")
                          + " | " + l.Nominal.ToString("F1") + " | " + l.Recip.ToString("F2") + " | " + l.MaxSimul.ToString("F2") + " | "
                          + l.Acts.ToString("F2") + " / " + l.Dry.ToString("F2") + " / " + l.Layers.ToString("F2") + " | "
                          + l.LeakFires.ToString("F2") + " | " + l.LeakDealt.ToString("F1") + " | " + l.LeakKills.ToString("F2") + " | "
                          + l.PostBite.ToString("F1") + " | " + l.VioAte.ToString("F2") + " | " + l.BeniDeath.ToString("F2") + " | "
                          + (l.LeakBy.Count == 0 ? "—" : string.Join("・", l.LeakBy.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value.ToString("F1"))))
                          + " |");
    }

    static void RunLedger()
    {
        Console.WriteLine("# 第190期 `beni ledger` —— 帳簿（1戦あたり・第2〜5波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("`癒えた` は反転で**実際に増えた** HP（渇き・満タン・支援拒否で目減りする）、`名目` は反転した削りの額面の合計。"
                          + "`延べ人数` は反転で癒えた味方の延べ（1ターンに同じ駒は1回）、`最大同時` は1ターンに癒えた味方の最大人数の戦ごとの平均。"
                          + "`裏` は反転の裏（回復がダメージになった）。`離れた後の刻み` は澱み分けの層を持ったまま隣から外れた味方が刻みで受けた額面。");
        Console.WriteLine();
        PrintLedgerHeader();
        foreach (var (_, name, f) in BeniRows())
        {
            PrintLedgerRow(name, "新", LedgerOf(f));
            PrintLedgerRow(name, "マイナスなし", LedgerOf(Apply(f, BeniYP)));
        }
        foreach (var (name, f) in Benches)
        {
            PrintLedgerRow(name, "新", LedgerOf(f));
            PrintLedgerRow(name, "マイナスなし", LedgerOf(Apply(f, BeniYP)));
            PrintLedgerRow(name, "手番なし", LedgerOf(Apply(f, BeniNoTaint)));
        }
        Console.WriteLine();

        Console.WriteLine("## 差し替え表（§5.2）の 25 行の帳簿（新）");
        Console.WriteLine();
        PrintLedgerHeader();
        foreach (var (name, g) in SwapRows()) PrintLedgerRow(name, "新", LedgerOf(g));
        Console.WriteLine();

        Console.WriteLine("## 味方が癒えた総量（旧 → 新・1戦あたり・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("`Healed`（実際に増えた HP）を出撃した味方全員で足した。**旧は毒喰らいの全体回復を含み、新は反転の回復を含む**"
                          + "（ほかの回復役の分も両方に入っている）。`ベニの与害` はベニ自身が敵に与えたダメージ。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 旧 癒えた | 新 癒えた | 旧 ベニの与害 | 新 ベニの与害 | 旧 ベニの死 | 新 ベニの死 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (_, name, f) in BeniRows())
        {
            var o = HealOf(Apply(f, BeniOld)); var n = HealOf(f);
            Console.WriteLine("| " + name + " | " + o.Healed.ToString("F1") + " | " + n.Healed.ToString("F1") + " | "
                              + o.Dealt.ToString("F1") + " | " + n.Dealt.ToString("F1") + " | " + o.Death.ToString("F2") + " | " + n.Death.ToString("F2") + " |");
        }
        Console.WriteLine();
    }

    static (double Healed, double Dealt, double Death) HealOf(Formation f)
    {
        var ids = f.Occupied().Select(o => o.Def.Id).Distinct().ToList();
        double h = 0, d = 0, de = 0; int n = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                n++;
                foreach (string id in ids)
                    if (r.TallyByUnit.TryGetValue(id, out UnitTally? t)) h += t.Healed;
                if (r.TallyByUnit.TryGetValue("beni", out UnitTally? b)) { d += b.DamageToEnemy; de += b.Deaths; }
            }
        return (h / n, d / n, de / n);
    }

    /// <summary>§5.2 の差し替え行（新ベニを置いた版）。選び方は <see cref="SwapTable"/> と同じ規則。</summary>
    static IEnumerable<(string Name, Formation G)> SwapRows()
    {
        foreach (var (name, f) in CompareBuilds())
        {
            var occ = f.Occupied().ToList();
            if (!occ.Any(o => Writers.Contains(o.Def.Id)) || HasBeni(f)) continue;
            double baseW = Mean25(Rates(f));
            var low = occ.Where(o => !Writers.Contains(o.Def.Id))
                         .Select(o => (o.Slot, A: baseW - Mean25(Rates(SwapAt(f, o.Slot, Plain(o.Def))))))
                         .OrderBy(x => x.A).ThenBy(x => x.Slot).First();
            yield return (name, SwapAt(f, low.Slot, BeniNew));
        }
    }

    // =================================================================================
    // bench —— 診断台（§5.3・`Presets` には足さない・席は測る前に固定）
    // =================================================================================

    static readonly (string Name, Formation F)[] Benches =
    {
        ("台P（毒）", Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Mudo, center: UnitCatalog.Beni,
                                    back1: UnitCatalog.Guza, back3: UnitCatalog.Kata)),
        ("台F（燃焼）", Formation.Build(front1: UnitCatalog.Borg, front3: UnitCatalog.Mudo, center: UnitCatalog.Beni,
                                      back1: UnitCatalog.Golm, back3: UnitCatalog.Kata)),
        ("台H（回復役）", Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Nono, center: UnitCatalog.Beni,
                                        back1: UnitCatalog.Vel, back3: UnitCatalog.Guza)),
    };

    static void RunBenches()
    {
        Console.WriteLine("# 第190期 `beni bench` —— 診断台3つ × 版");
        Console.WriteLine();
        Console.WriteLine("seed 0..199。平均は第2〜5波。**代金 ＝ 新 − マイナスなし** ／ **澱み分け ＝ 新 − 手番なし** ／ **Δ素体 ＝ 新 − 素体**。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 第1波 | 第2 / 3 / 4 / 5 波 | 第2〜5波 | Δ（素体比） |");
        Console.WriteLine("|---|---|--:|---|--:|--:|");
        foreach (var (name, f) in Benches)
        {
            var m = Versions.ToDictionary(v => v.Tag, v => Rates(Apply(f, v.D)));
            double basis = Mean25(m["素体"]);
            bool first = true;
            foreach (var (tag, _) in Versions)
            {
                double[] w = m[tag];
                Console.WriteLine("| " + (first ? name : "") + " | " + tag + " | " + w[0].ToString("F1") + " | " + Cells(w) + " | "
                                  + Mean25(w).ToString("F1") + " | " + D(Mean25(w) - basis) + " |");
                first = false;
            }
            Console.WriteLine("| | **代金 " + D(Mean25(m["新"]) - Mean25(m["マイナスなし"])) + " ／ 澱み分け "
                              + D(Mean25(m["新"]) - Mean25(m["手番なし"])) + "** | | | | |");
        }
        Console.WriteLine();

        Console.WriteLine("## 隣の駒の生存T（第2〜5波・戦ごとの平均。倒れなければ決着T）");
        Console.WriteLine();
        foreach (var (name, f) in Benches)
        {
            Console.WriteLine("**" + name + "**");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", f.Occupied().Select(o => o.Def.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(f.Occupied().Select(_ => "--:|")));
            foreach (var (tag, d) in Versions)
            {
                Formation g = Apply(f, d);
                var ids = g.Occupied().Select(o => o.Def.Id).ToList();
                var life = new double[ids.Count]; int n = 0;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(g, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                        n++;
                        for (int i = 0; i < ids.Count; i++)
                            if (r.TallyByUnit.TryGetValue(ids[i], out UnitTally? t))
                                life[i] += t.Deaths > 0 ? t.LastActiveTurn : r.Turns;
                    }
                Console.WriteLine("| " + tag + " | " + string.Join(" | ", life.Select(x => (x / n).ToString("F2"))) + " |");
            }
            Console.WriteLine();
        }
    }

    // =================================================================================
    // check —— 自己検査（§7）
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第190期 `beni check` —— 自己検査");
        Console.WriteLine();
        string path = string.IsNullOrWhiteSpace(arg) ? Path.Combine("docs", "balance.md") : arg.Trim();
        var want = new Dictionary<string, double[]>();
        if (File.Exists(path))
            foreach (string line in File.ReadAllLines(path))
            {
                if (!line.StartsWith("| ") || !line.Contains('%')) continue;
                string[] c = line.Split('|').Select(x => x.Trim()).ToArray();
                if (c.Length < 8) continue;
                want[c[1]] = c.Skip(2).Take(5).Select(x => double.Parse(x.TrimEnd('%'))).ToArray();
            }

        // (a) ベニを含まない行 ／ (b) 旧に戻すと 305 セル
        int diffA = 0, seenA = 0, diffB = 0, seenB = 0;
        foreach (var (name, f) in CompareBuilds())
        {
            if (!want.TryGetValue(name, out double[]? w)) { diffB += 5; continue; }
            double[] r = Rates(HasBeni(f) ? Apply(f, BeniOld) : f);
            for (int i = 0; i < 5; i++)
            {
                seenB++; if (Math.Abs(r[i] - w[i]) > 0.001) diffB++;
                if (!HasBeni(f)) { seenA++; if (Math.Abs(r[i] - w[i]) > 0.001) diffA++; }
            }
        }
        Console.WriteLine("- 比べた表: `" + path + "`（**採用前の balance.md を渡すこと**）");
        Console.WriteLine("- (a) ベニを含まない `compare` 行のセルのずれ: **" + diffA + " / " + seenA + "**（0 が正）");
        Console.WriteLine("- (b) ベニを旧に戻した `compare` 305 セルのずれ: **" + diffB + " / " + seenB + "**（0 が正）");

        // (c) 乱数を引かない
        if (ParryScan.Init())
        {
            string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            static string Body(string src, string head, string end)
            {
                int i = src.IndexOf(head);
                if (i < 0) return "";
                int j = src.IndexOf(end, i + head.Length);
                return src.Substring(i, (j < 0 ? src.Length : j) - i);
            }
            string b = Body(traits, "class " + "InverseTrait", "/// <summary>\n/// 突きの対照".Replace("\n", traits.Contains("\r\n") ? "\r\n" : "\n"))
                     + Body(engine, "static UnitState? " + "AdjacentHolder(", "ハネの「勢い余って」（第189期");
            string pick = "Pick" + "One", roll = "Roll" + "(", shuf = "Shuffl" + "ed";
            int Count(string x) => (b.Length - b.Replace(x, "").Length) / x.Length;
            Console.WriteLine("- (c) 新しい札の本体と engine の反転の本体が `" + pick + "` / `Roll` / `" + shuf + "` を呼ぶ回数: **"
                              + (Count(pick) + Count(roll) + Count(shuf)) + "**（0 が正・走査した本体 " + b.Length + " 字。空なら止める）");
        }

        // (d) 再反転しない ——反転由来の回復が裏でダメージになった回数（裏の保持者がいる新の版で、刻み・起爆の直後の裏を数える）
        {
            long leakAfterInverse = 0, inverseSteps = 0;
            foreach (var (_, f) in Benches)
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 30; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: true);
                        for (int i = 0; i < r.Log.Count; i++)
                        {
                            string t = r.Log[i].Text;
                            if (!t.Contains("で薬になる")) continue;
                            inverseSteps++;
                            if (i + 1 < r.Log.Count && r.Log[i + 1].Text.Contains("で毒に変わった")) leakAfterInverse++;
                        }
                    }
            Console.WriteLine("- (d) 反転の回復の直後に裏が走った回数: **" + leakAfterInverse + "** / 反転 " + inverseSteps + " 段（0 が正・反転が 1 段以上で意味がある）");
        }

        // (e) 旧の札の保持者・ロスター
        Console.WriteLine("- (e) `Devour` の保持者（All）: **" + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Devour)) + "**（0 が正）"
                          + " ／ ベニの札: " + string.Join("・", UnitCatalog.Beni.Traits) + " ／ `All` " + UnitCatalog.All.Count + " 枚");

        // (f) 経路の本数
        Console.WriteLine("- (f) `PoisonRoute` " + Enum.GetValues<PoisonRoute>().Length + " 本 ／ `SoakRouteCount` " + BattleContext.SoakRouteCount
                          + " ／ 燃焼の添字 " + BattleContext.SoakBurnRouteIx + "（重ならないこと: "
                          + (BattleContext.SoakBurnRouteIx >= Enum.GetValues<PoisonRoute>().Length ? "○" : "**×**") + "）");

        // (g) 台本: 反転は `Status` の直後に `Heal`
        {
            var f = Benches[0].F;
            int status = 0, linked = 0, seedUsed = -1;
            for (int seed = 0; seed < 20 && linked == 0; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[1].Enemy, seed, verbose: true);
                for (int i = 0; i + 1 < r.Events.Count; i++)
                {
                    if (r.Events[i].Kind != BattleEventKind.Status) continue;
                    if (r.Events[i + 1].Kind == BattleEventKind.Heal && r.Events[i + 1].TargetId == r.Events[i].TargetId) linked++;
                    status++;
                }
                seedUsed = seed;
            }
            Console.WriteLine("- (g) 1戦（" + Benches[0].Name + " × 第二波 × seed " + seedUsed + "）の台本: `Status` " + status
                              + " 件のうち直後が同じ駒の `Heal` **" + linked + "** 件（1 件以上が正）");
        }
        Console.WriteLine();
    }
}
