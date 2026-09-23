using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using BattleCore;
using static Common;

// =====================================================================================
// mudohex モード（第182期） —— 泥人形ムドの呪いを既定でオンにする
//
// 指示書は design/PHASE182_MUDO_HEX_SPEC.md ／ 報告は design/PHASE182_MUDO_HEX.md。
//
// **数値の線は置かない**。採否はポンが遊んで決める。**触るのはムドの周りだけ**
// ——`HexTrait` と `ApplyDamage` の共有の段は1文字も触っていない（第96期のまま）。
//
//     dotnet run --project BattleSim -c Release 0 mudohex phase0  # Q0-1〜Q0-5（Q0-3 だけ戦闘を回す）
//     dotnet run --project BattleSim -c Release 0 mudohex run     # H0〜H3 の4版（**線は置かない**）
//     dotnet run --project BattleSim -c Release 0 mudohex ledger  # 帳簿（呪い・共有・出どころ・味方への共有）
//     dotnet run --project BattleSim -c Release 0 mudohex check   # 自己検査
//
// 版（比較先は第181期そのもの ＝ H0）:
//     H0 呪いオフ × 上乗せ 5   （第181期そのもの）
//     H1 呪いオン × 上乗せ 5   （第182期の初版の既定）
//     H2 呪いオフ × 上乗せ 10  （火力だけ上げた場合）
//     H3 呪いオン × 上乗せ 10  （**採用＝既定**。ポンの判断）
// =====================================================================================

static class MudoHexDiag
{
    const int Seeds = 200;

    readonly record struct Ver(string Tag, CurseRule Curse, EruptRule Erupt);

    static readonly EruptRule E5 = EruptRule.Phase181;
    static readonly EruptRule E10 = EruptRule.Phase181 with { Heavy = true };   // ＝ 第182期の既定（H3 を採った）

    static readonly Ver H0 = new("H0 第181期", CurseRule.Off, E5);
    static readonly Ver H1 = new("H1 呪いだけ", CurseRule.Default, E5);
    static readonly Ver H2 = new("H2 上乗せ10", CurseRule.Off, E10);
    static readonly Ver H3 = new("**H3 採用**", CurseRule.Default, E10);
    static readonly Ver[] Versions = { H0, H1, H2, H3 };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": StageRun(); return;
            case "ledger": Ledger(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("mudohex: モードは phase0 / run / ledger / check。");
                return;
        }
    }

    static BattleResult Fight(Formation f, int stage, int seed, Ver v)
        => BattleEngine.Run(f, EnemyCatalog.Stages[stage].Enemy, seed, verbose: false,
                            curse: v.Curse, erupt: v.Erupt);

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第182期 `mudohex phase0` —— 前提を実装から引き直す");
        Console.WriteLine();
        if (!ParryScan.Init()) return;
        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1. `TraitId.Hex` を持つ駒");
        Console.WriteLine();
        var holders = new List<string>();
        foreach (UnitDef d in UnitCatalog.Everyone)
            if (d.Traits.Contains(TraitId.Hex)) holders.Add("味方 " + d.Name + "（`" + d.Id + "`）");
        var seenEnemy = new HashSet<string>();
        foreach (var st in EnemyCatalog.Stages)
            foreach ((int _, UnitDef d) in st.Enemy.Occupied())
                if (d.Traits.Contains(TraitId.Hex) && seenEnemy.Add(d.Name)) holders.Add("敵 " + d.Name);
        foreach ((string _, Formation f) in CompareBuilds().Concat(CrossBuilds()))
            foreach ((int _, UnitDef d) in f.Occupied())
                if (d.Traits.Contains(TraitId.Hex) && d.Id != "mudo" && seenEnemy.Add(d.Name)) holders.Add("編成 " + d.Name);
        Console.WriteLine("- 保持者: **" + string.Join(" ／ ", holders) + "**（`UnitCatalog.Everyone` ・ `EnemyCatalog.Stages` ・ `Presets` の走査）");
        string key = "TraitId." + "Hex";
        int srcHits = 0;
        foreach (string file in new[] { "UnitCatalog.cs", "EnemyCatalog.cs" })
        {
            string path = Path.Combine("BattleCore", file);
            string txt = File.Exists(Path.Combine(ParryScan.Root!, path)) ? ParryScan.Read(path) : "";
            srcHits += Regex.Matches(txt, Regex.Escape(key) + @"\b").Count;
        }
        Console.WriteLine("- `UnitCatalog.cs` ／ `EnemyCatalog.cs` の中の `" + key + "`: **" + srcHits + " 件**（召喚専用の駒もここに定義がある）");
        string setCurse = "SetCounter(StatusKeys." + "Curse, 1)";
        int writes = (traits.Length - traits.Replace(setCurse, "").Length) / setCurse.Length
                   + (engine.Length - engine.Replace(setCurse, "").Length) / setCurse.Length;
        Console.WriteLine("- 呪いを書く箇所（`" + setCurse + "`）: **" + writes + " 件**（`HexTrait.OnDamaged` の1箇所なら、"
                          + "ムドがいない行は呪いが1つも立たず ±0.0 のはず）");
        Console.WriteLine();

        // ---- Q0-2 ----
        Console.WriteLine("## Q0-2. 共有を起こす一撃（`singleHit: true` の出口）");
        Console.WriteLine();
        string[] el = engine.Replace("\r", "").Split('\n');
        for (int i = 0; i < el.Length; i++)
            if (el[i].Contains("singleHit:") && !el[i].TrimStart().StartsWith("//") && !el[i].TrimStart().StartsWith("///"))
                Console.WriteLine("- `BattleEngine.cs:" + (i + 1) + "` —— `" + el[i].Trim() + "`");
        Console.WriteLine();
        Console.WriteLine("**立つのは `PerformAttack` の主目標（型が `Single` のとき）と共有そのものの2つだけ。**"
                          + "ウツの連撃（`ModifyHitCount` → `SwingTurn` が1発ずつ `PerformAttack`）・"
                          + "ムドの暴発（`EruptTrait` が `ctx.PerformAttack` を発数ぶん）・"
                          + "ガンの叩き起こし（起こされた駒の手番 → `PerformAttack`）はどれも同じ出口を通る。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 攻撃型 |");
        Console.WriteLine("|---|---|");
        foreach (string id in new[] { "mudo", "utsu", "gan" })
        {
            UnitDef d = UnitCatalog.ById(id);
            Console.WriteLine("| " + d.Name + " | `" + d.Pattern + "` |");
        }
        Console.WriteLine();

        // ---- Q0-3 ----
        Console.WriteLine("## Q0-3. 味方の刃がムドを殴る行（`CurseRule(true, 0)` ＝ 印だけ・盤面は H0 と同じ）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 味方がムドを殴った回数/戦 | 味方に付いた呪い/戦 | 殴った味方（回数/戦） |");
        Console.WriteLine("|---|--:|--:|---|");
        var gate = new CurseRule(true, 0);
        foreach ((string name, Formation f) in CompareBuilds().Concat(CrossBuilds()))
        {
            if (!Has(f, "mudo")) continue;
            double hits = 0, marks = 0; int n = 0;
            var by = new Dictionary<string, int>();
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false,
                                                      curse: gate, erupt: E5);
                    n++; hits += r.HexHitsFromAlly; marks += r.HexMarksOnPlayer;
                    foreach (var kv in r.HexHitByAlly) { by.TryGetValue(kv.Key, out int c); by[kv.Key] = c + kv.Value; }
                }
            string who = by.Count == 0 ? "—" : string.Join(" ／ ", by.OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Key + " " + (kv.Value / (double)n).ToString("F2")));
            Console.WriteLine("| " + name + " | " + (hits / n).ToString("F2") + " | " + (marks / n).ToString("F2") + " | " + who + " |");
        }
        Console.WriteLine();

        // ---- Q0-4 ----
        Console.WriteLine("## Q0-4. 共有の段の周り（第96期以降に変わったもの）");
        Console.WriteLine();
        int iCap = engine.IndexOf("amount = Yoke.Cap;");
        int iHp = engine.IndexOf("target.Hp -= amount;");
        int iShare = engine.IndexOf("if (Curse.Enabled && singleHit");
        Console.WriteLine("- 軛（`amount = Yoke.Cap`）@" + iCap + " → HP を引く @" + iHp + " → 共有の段 @" + iShare
                          + "：**" + (iCap < iHp && iHp < iShare ? "共有は上限で切った後の量から割る" : "**順序が想定と違う**") + "**");
        Console.WriteLine("- 共有の1発は `ApplyDamage` を最初から通る（`hexShare: true`）ので**軛にも掛かる**"
                          + "——ただし元の一撃が 25 以下なので共有は最大 " + (25 * CurseRule.Default.SharePercent / 100)
                          + "（上限を跨がない）");
        Console.WriteLine("- 暴発（第180期）・泥（第181期）は `ApplyDamage` の中に1行も無い（`Trait` の側）。"
                          + "暴発の各発は `PerformAttack` なので**共有を起こす側**に入る");
        Console.WriteLine("- 受け流し（第136期）・身構え（第143期）は共有の段より**前**——受け流された一撃は"
                          + "`amount = 0` で共有の段まで届かない");
        Console.WriteLine();

        // ---- Q0-5 ----
        Console.WriteLine("## Q0-5. ムドを含まない行は動かないか（紙）");
        Console.WriteLine();
        Console.WriteLine("共有の段の条件は `Curse.Enabled && singleHit && target.RawCounter(StatusKeys.Curse) > 0`。"
                          + "呪いを書くのは Q0-1 の " + writes + " 箇所（`HexTrait`）だけで、保持者はムドだけ"
                          + "——**ムドがいない盤面では `Curse` が 0 のまま**なので段に入らない。"
                          + "`Roll` も `PickOne` も呼ばないので乱数列も動かない。実測は `mudohex check` (a)。");
        Console.WriteLine();
    }

    // =================================================================================
    // run
    // =================================================================================

    static void StageRun()
    {
        Console.WriteLine("# 第182期 `mudohex run` —— H0〜H3（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 呪い | 暴発の上乗せ | 1発（素攻 3・床あり） |");
        Console.WriteLine("|---|---|--:|--:|");
        Console.WriteLine("| H0 第181期 | オフ | " + EruptTrait.SwingBonus + " | " + (3 + EruptTrait.SwingBonus) + " |");
        Console.WriteLine("| H1 呪いだけ | オン（50%） | " + EruptTrait.SwingBonus + " | " + (3 + EruptTrait.SwingBonus) + " |");
        Console.WriteLine("| H2 上乗せ10 | オフ | " + EruptTrait.HeavySwingBonus + " | " + (3 + EruptTrait.HeavySwingBonus) + " |");
        Console.WriteLine("| **H3 採用** | **オン（50%）** | " + EruptTrait.HeavySwingBonus + " | " + (3 + EruptTrait.HeavySwingBonus) + " |");
        Console.WriteLine();

        Console.WriteLine("## 表A. ムド在席の行（第2〜5波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 行 | H0 | **H1** | H2 | H3 | 呪い（H1−H0） | 上乗せ（H2−H0） | 両方（H3−H0） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        var sums = new double[4]; int rows = 0;
        foreach ((string name, Formation f) in CompareBuilds())
        {
            if (!Has(f, "mudo")) continue;
            double[] a = Versions.Select(v => Mean25(Rates(f, v))).ToArray();
            for (int i = 0; i < 4; i++) sums[i] += a[i];
            rows++;
            Console.WriteLine("| " + name + " | " + a[0].ToString("F1") + " | **" + a[1].ToString("F1") + "** | "
                              + a[2].ToString("F1") + " | " + a[3].ToString("F1") + " | " + D(a[1] - a[0])
                              + " | " + D(a[2] - a[0]) + " | " + D(a[3] - a[0]) + " |");
        }
        Console.WriteLine("| **平均（" + rows + " 行）** | " + (sums[0] / rows).ToString("F1") + " | **" + (sums[1] / rows).ToString("F1")
                          + "** | " + (sums[2] / rows).ToString("F1") + " | " + (sums[3] / rows).ToString("F1")
                          + " | " + D((sums[1] - sums[0]) / rows) + " | " + D((sums[2] - sums[0]) / rows)
                          + " | " + D((sums[3] - sums[0]) / rows) + " |");
        Console.WriteLine();

        Console.WriteLine("## 表A'. 波ごと（H0 → H1）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in CompareBuilds())
        {
            if (!Has(f, "mudo")) continue;
            double[] w0 = Rates(f, H0), w1 = Rates(f, H1);
            Console.WriteLine("| " + name + " | H0 | " + Cells(w0) + " | " + Mean25(w0).ToString("F1") + " |");
            Console.WriteLine("| | **H1** | " + Cells(w1) + " | **" + Mean25(w1).ToString("F1") + "** |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表B. ムド×ウツの診断台（`逆しま (ネル×ウツ)` の前3 をムドに・`Presets` は触っていない）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | Δ（H0比） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        Formation stand = UtsuStand();
        double refAvg = Mean25(Rates(stand, H0));
        foreach (Ver v in Versions)
        {
            double[] w = Rates(stand, v);
            Console.WriteLine("| " + v.Tag + " | " + Cells(w) + " | " + Mean25(w).ToString("F1") + " | "
                              + (v == H0 ? "—" : D(Mean25(w) - refAvg)) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表C. 交差帯のムド行");
        Console.WriteLine();
        Console.WriteLine("| 行 | H0 | **H1** | H2 | H3 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in CrossBuilds())
        {
            if (!Has(f, "mudo")) continue;
            double[] a = Versions.Select(v => Mean25(Rates(f, v))).ToArray();
            Console.WriteLine("| " + name + " | " + a[0].ToString("F1") + " | **" + a[1].ToString("F1") + "** | "
                              + a[2].ToString("F1") + " | " + a[3].ToString("F1") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表D. 勝ち方（全5波通算。残存・全滅勝ちの定義は第181期 `mudo run` 表D と同じ）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 勝率 | 決着T | 残存 | 残り1体以下の勝ち |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in CompareBuilds().Append(("ムド×ウツの診断台", stand)))
        {
            if (!Has(f, "mudo")) continue;
            foreach (Ver v in Versions)
            {
                Quality q = QualityOf(f, v);
                Console.WriteLine("| " + (v == H0 ? name : "") + " | " + v.Tag + " | " + q.Win.ToString("F1")
                                  + " | " + q.Turns.ToString("F1") + " | " + q.Survivors.ToString("F2")
                                  + " | " + q.Narrow.ToString("F0") + "% |");
            }
        }
        Console.WriteLine();

        G2(H1);
        G2(H3);
    }

    // ---- (G2) の再分解（61 行分母） ----------------------------------------------
    static void G2(Ver v)
    {
        Console.WriteLine("## 表E. (G2) の壊れ／制約（`compare` 61 行・" + v.Tag + " 対 H0・第2〜5波）");
        Console.WriteLine();
        var builds = CompareBuilds();
        var d = new Dictionary<string, double[]>();
        foreach ((string name, Formation f) in builds)
        {
            double[] a = Rates(f, H0), b = Rates(f, v);
            d[name] = Enumerable.Range(0, 5).Select(i => b[i] - a[i]).ToArray();
        }
        var fallen = builds.Where(b => Enumerable.Range(1, 4).Any(i => d[b.Name][i] <= -10.0)).ToList();
        if (fallen.Count == 0)
        {
            Console.WriteLine("**いずれかの波で −10.0pt 以上落ちた行は 0 行。** 分解の対象が無い。");
            Console.WriteLine();
            return;
        }
        Console.WriteLine("| 落ちた行 | 最大の低下 | 駒 | その駒を含む他の行 | 他の行の平均変化 | 判定 |");
        Console.WriteLine("|---|--:|---|--:|--:|---|");
        foreach ((string name, Formation f) in fallen)
        {
            double worst = Enumerable.Range(1, 4).Min(i => d[name][i]);
            bool first = true;
            foreach ((int _, UnitDef u) in f.Occupied())
            {
                var others = builds.Where(b => b.Name != name && Has(b.F, u.Id)).ToList();
                string avg = "—", verdict = "分解不能（他の行 0）";
                if (others.Count > 0)
                {
                    double m = others.Average(b => Mean25(d[b.Name]));
                    avg = D(m);
                    verdict = m <= -3.0 ? "**壊れ**" : Math.Abs(m) < 3.0 ? "制約" : "上がる側";
                }
                Console.WriteLine("| " + (first ? name : "") + " | " + (first ? D(worst) : "") + " | " + u.Name
                                  + " | " + others.Count + " | " + avg + " | " + verdict + " |");
                first = false;
            }
        }
        Console.WriteLine();
    }

    // =================================================================================
    // ledger
    // =================================================================================

    readonly record struct Led(double Win, double CursedFoe, double MaxCursedFoe, double ShareHits, double Shares,
                               double ShareDmg, double ShareMudo, double ShareUtsu, double ShareOther,
                               double CursedAlly, double ShareToAlly, double MudoDmg, double UtsuDmg, double Fires);

    static Led LedgerOf(Formation f, Ver v, int stFrom = 1, int stTo = 4)
    {
        double win = 0, cf = 0, mcf = 0, sh = 0, s = 0, sd = 0, sm = 0, su = 0, so = 0, ca = 0, sa = 0, md = 0, ud = 0, fi = 0;
        int n = 0;
        string mudo = UnitCatalog.Mudo.Name, utsu = UnitCatalog.Utsu.Name;
        for (int st = stFrom; st <= stTo; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = Fight(f, st, seed, v);
                n++;
                if (r.PlayerWon) win++;
                cf += r.HexMarksOnEnemy; mcf += r.HexMaxCursedEnemy; sh += r.HexShareHits; s += r.HexShares;
                sd += r.HexShareDamage - r.HexShareDamageToPlayer;
                ca += r.HexMarksOnPlayer; sa += r.HexShareDamageToPlayer;
                foreach (var kv in r.HexShareDamageBySource)
                {
                    if (kv.Key == mudo) sm += kv.Value;
                    else if (kv.Key == utsu) su += kv.Value;
                    else so += kv.Value;
                }
                if (r.TallyByUnit.TryGetValue("mudo", out UnitTally? t)) { md += t.DamageToEnemy; fi += t.EruptFires; }
                if (r.TallyByUnit.TryGetValue("utsu", out UnitTally? tu)) ud += tu.DamageToEnemy;
            }
        return new Led(100.0 * win / n, cf / n, mcf / n, sh / n, s / n, sd / n, sm / n, su / n, so / n,
                       ca / n, sa / n, md / n, ud / n, fi / n);
    }

    static void Ledger()
    {
        Console.WriteLine("# 第182期 `mudohex ledger` —— 帳簿（第2〜5波の平均・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("**共有ぶんの与害は元の攻撃者の `DamageToEnemy` に数える**（`ApplyDamage` の共有は "
                          + "`source` を元の攻撃者のまま渡すため）。`共有(ム)` / `共有(ウ)` / `共有(他)` は共有の総量を"
                          + "**共有を起こした一撃の出どころ**で割ったもの（敵の攻撃が起こした味方への共有は `共有(他)` に入る）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 勝率 | 呪われた敵 | 同時最大 | 共有を起こした一撃 | 共有(回) | 敵への共有量 | 共有(ム) | 共有(ウ) | 共有(他) | 呪われた味方 | 味方への共有量 | ムドの与害 | 暴発 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        Formation stand = UtsuStand();
        foreach ((string name, Formation f) in CompareBuilds().Append(("ムド×ウツの診断台", stand)))
        {
            if (!Has(f, "mudo")) continue;
            foreach (Ver v in Versions)
            {
                Led s = LedgerOf(f, v);
                Console.WriteLine("| " + (v == H0 ? name : "") + " | " + v.Tag + " | " + s.Win.ToString("F1") + " | "
                                  + s.CursedFoe.ToString("F2") + " | " + s.MaxCursedFoe.ToString("F2") + " | "
                                  + s.ShareHits.ToString("F2") + " | " + s.Shares.ToString("F2") + " | "
                                  + s.ShareDmg.ToString("F1") + " | " + s.ShareMudo.ToString("F1") + " | "
                                  + s.ShareUtsu.ToString("F1") + " | " + s.ShareOther.ToString("F1") + " | "
                                  + s.CursedAlly.ToString("F2") + " | " + s.ShareToAlly.ToString("F1") + " | "
                                  + s.MudoDmg.ToString("F1") + " | " + s.Fires.ToString("F2") + " |");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 波ごと（H1・ムド在席 8 行の平均）——敵の体数と呪い");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵の体数 | 呪われた敵 | 同時最大 | 敵への共有量 | 味方への共有量 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        var mrows = CompareBuilds().Where(b => Has(b.F, "mudo")).ToList();
        for (int st = 1; st < 5; st++)
        {
            double cf = 0, mx = 0, sd = 0, sa = 0;
            foreach ((string _, Formation f) in mrows)
            {
                Led s = LedgerOf(f, H1, st, st);
                cf += s.CursedFoe; mx += s.MaxCursedFoe; sd += s.ShareDmg; sa += s.ShareToAlly;
            }
            int k = mrows.Count;
            Console.WriteLine("| 第" + (st + 1) + "波 | " + EnemyCatalog.Stages[st].Enemy.Occupied().Count() + " | "
                              + (cf / k).ToString("F2") + " | " + (mx / k).ToString("F2") + " | "
                              + (sd / k).ToString("F1") + " | " + (sa / k).ToString("F1") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 診断台の格差（ムドの与害 ／ ウツの与害。共有ぶんを含む・直接ぶんは共有を引いたもの）");
        Console.WriteLine();
        Console.WriteLine("| 版 | ムド（全） | うち共有 | ウツ（全） | うち共有 | ウツ ÷ ムド |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (Ver v in Versions)
        {
            Led s = LedgerOf(stand, v);
            Console.WriteLine("| " + v.Tag + " | " + s.MudoDmg.ToString("F1") + " | " + s.ShareMudo.ToString("F1")
                              + " | " + s.UtsuDmg.ToString("F1") + " | " + s.ShareUtsu.ToString("F1") + " | "
                              + (s.MudoDmg < 0.01 ? "—" : (s.UtsuDmg / s.MudoDmg).ToString("F2")) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## ポンの1戦（診断台・第四波・seed 7）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝敗 | ムドの与害 | ムドの回復(受) | ウツの与害 | ウツの回復(受) | 共有(回) |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (Ver v in Versions)
        {
            BattleResult r = Fight(stand, 3, 7, v);
            r.TallyByUnit.TryGetValue("mudo", out UnitTally? m);
            r.TallyByUnit.TryGetValue("utsu", out UnitTally? u);
            Console.WriteLine("| " + v.Tag + " | " + (r.PlayerWon ? "勝ち" : "負け") + " | " + (m?.DamageToEnemy ?? 0)
                              + " | " + (m?.Healed ?? 0) + " | " + (u?.DamageToEnemy ?? 0) + " | " + (u?.Healed ?? 0)
                              + " | " + r.HexShares + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // check
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第182期 `mudohex check` —— 自己検査");
        Console.WriteLine();

        // (a) ムドを含まない行は H0 と H1 で1セルも動かない（compare ＋ 交差帯）
        int cells = 0, rows = 0, vg = 0, vgMoved = 0;
        foreach ((string _, Formation f) in CompareBuilds().Concat(CrossBuilds()))
        {
            if (Has(f, "mudo")) continue;
            rows++;
            double[] a = Rates(f, H0), b = Rates(f, H3);
            int moved = 0;
            for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - b[i]) > 0.001) moved++;
            cells += moved;
            if (Has(f, "vio") || Has(f, "gan")) { vg++; if (moved > 0) vgMoved++; }
        }
        Console.WriteLine("- (a) ムドを含まない " + rows + " 行（`compare` ＋ 交差帯）で H0 → H3（既定）に動いたセル: **" + cells + " / " + rows * 5
                          + "**（0 が正）／ うちヴィオ・ガン在席 " + vg + " 行で動いた行 **" + vgMoved + "**");

        // (b) H0 が採用前の docs/balance.md と一致する
        string path = string.IsNullOrEmpty(arg) ? Path.Combine("docs", "balance.md") : arg;
        if (File.Exists(path))
        {
            var table = new Dictionary<string, double[]>();
            foreach (string ln in File.ReadAllLines(path))
            {
                if (!ln.StartsWith("| ") || !ln.Contains('%')) continue;
                string[] c = ln.Split('|').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
                if (c.Length < 6) continue;
                table[c[0]] = c.Skip(1).Take(5).Select(x => double.Parse(x.TrimEnd('%'))).ToArray();
            }
            int miss = 0, diff = 0, hit = 0;
            foreach ((string name, Formation f) in CompareBuilds())
            {
                if (!table.TryGetValue(name, out double[]? t)) { miss++; continue; }
                double[] a = Rates(f, H0);
                hit++;
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - t[i]) > 0.051) diff++;
            }
            Console.WriteLine("- (b) H0 × `" + path + "`: 照合 " + hit + " 行・差 **" + diff + " セル**・行名が引けない " + miss
                              + "（**採用前の `docs/balance.md` に当てて 0 が正**。再生成後に当てると H1 との差が出る）");
        }
        else Console.WriteLine("- (b) `" + path + "` が無い");

        // (c) 門の版 `CurseRule(true, 0)` は盤面を動かさない
        {
            var gate = new Ver("gate", new CurseRule(true, 0), E5);
            int moved = 0, n = 0;
            foreach ((string _, Formation f) in CompareBuilds())
            {
                if (!Has(f, "mudo")) continue;
                double[] a = Rates(f, H0), b = Rates(f, gate);
                n++;
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - b[i]) > 0.001) moved++;
            }
            Console.WriteLine("- (c) `CurseRule(true, 0)`（印だけ）はムド在席 " + n + " 行で H0 から動いたセル **" + moved + "**（0 が正）");
        }

        // (d) 共有の構造（範囲は反応しない・1ホップ・陣営をまたがない）
        {
            long hop = 0, cross = 0, nonSingle = 0, shares = 0;
            foreach ((string _, Formation f) in CompareBuilds().Append(("stand", UtsuStand())))
            {
                if (!Has(f, "mudo")) continue;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = Fight(f, st, seed, H1);
                        hop += r.HexHopBlocked; nonSingle += r.HexNonSingleOnCursed; shares += r.HexShares;
                    }
            }
            _ = cross;
            Console.WriteLine("- (d) H1 の共有 " + shares + " 回 ／ 1ホップで止まった " + hop + " ／ 範囲が呪い持ちに入って反応しなかった "
                              + nonSingle + "（**共有が 0 でないこと**と、止まる側が数えられていることを見る）");
        }

        // (e) H2 の1発は 13
        {
            Formation stand = UtsuStand();
            double atk = 0, sw = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 50; seed++)
                {
                    BattleResult r = Fight(stand, st, seed, H2);
                    if (!r.TallyByUnit.TryGetValue("mudo", out UnitTally? t)) continue;
                    atk += t.EruptSwingAtk; sw += t.EruptSwings;
                }
            Console.WriteLine("- (e) H2 の診断台の暴発1発の平均: **" + (sw < 1 ? "—" : (atk / sw).ToString("F2"))
                              + "**（床あり・上乗せ 10 ＝ **" + (UnitCatalog.Mudo.Attack + EruptTrait.HeavySwingBonus) + " 以上**が正）");
        }

        // (g) 既定（引数なしの Run）が H3 と一致する
        {
            int moved = 0, n = 0;
            foreach ((string _, Formation f) in CompareBuilds())
            {
                if (!Has(f, "mudo")) continue;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        n++;
                        if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon
                            != Fight(f, st, seed, H3).PlayerWon) moved++;
                    }
            }
            Console.WriteLine("- (g) 引数なしの `Run`（既定）と H3 の勝敗が食い違った戦: **" + moved + " / " + n + "**（0 が正）");
        }

        // (f) PickOne / 状態キー
        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            string pick = "ctx.Pick" + "One(";
            int total = (traits.Length - traits.Replace(pick, "").Length) / pick.Length;
            Console.WriteLine("- (f) `Traits.cs` の `" + pick + "` 呼び出し: **" + total
                              + "**（第181期から不変が正）／ `StatusKeys.All` " + StatusKeys.All.Length);
        }
        Console.WriteLine();
    }

    // =================================================================================
    // ヘルパ
    // =================================================================================

    /// <summary>第180・181期と同じ診断台（`逆しま (ネル×ウツ)` の 前3 をムドに）。</summary>
    static Formation UtsuStand()
    {
        Formation src = CompareBuilds().First(b => b.Name == "逆しま (ネル×ウツ)").F;
        var g = new Formation();
        foreach ((int slot, UnitDef d) in src.Occupied()) g[slot] = slot == 1 ? UnitCatalog.Mudo : d;
        return g;
    }

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    readonly record struct Quality(double Win, double Turns, double Survivors, double Narrow);

    static Quality QualityOf(Formation f, Ver v)
    {
        int wins = 0, narrow = 0, n = 0; double turns = 0, sv = 0;
        for (int st = 0; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult res = Fight(f, st, seed, v);
                n++;
                if (!res.PlayerWon) continue;
                wins++; turns += res.Turns; sv += res.PlayerSurvivors;
                if (res.PlayerSurvivors <= 1) narrow++;
            }
        return new Quality(100.0 * wins / n, wins == 0 ? 0 : turns / wins,
                           wins == 0 ? 0 : sv / wins, wins == 0 ? 0 : 100.0 * narrow / wins);
    }

    static readonly ConcurrentDictionary<(Formation, Ver), double[]> RateCache = new();

    static double[] Rates(Formation f, Ver v) => RateCache.GetOrAdd((f, v), _ =>
    {
        var w = new double[5];
        Parallel.For(0, 5, st =>
        {
            int wins = 0;
            for (int seed = 0; seed < Seeds; seed++)
                if (Fight(f, st, seed, v).PlayerWon) wins++;
            w[st] = 100.0 * wins / Seeds;
        });
        return w;
    });

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static string Cells(double[] w) => string.Join(" | ", Enumerable.Range(1, 4).Select(i => w[i].ToString("F1")));
    static string D(double x) => x.ToString("+0.0;-0.0;0.0");
}
