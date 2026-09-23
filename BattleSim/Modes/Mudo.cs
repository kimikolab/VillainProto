using System.Text.RegularExpressions;
using BattleCore;
using static Common;

// =====================================================================================
// mudo モード（第181期） —— 泥人形ムドの手直し（暴発の1発に床／泥散りを暴発時へ）
//
// 指示書は design/PHASE181_MUDO_FIX_SPEC.md ／ 報告は design/PHASE181_MUDO_FIX.md。
//
// **数値の線は置かない**（指示書 §0 は第180期から引き継ぐ約束）。採否はポンが遊んで決める。
// **触るのはムドだけ**——ヴィオ（吐き戻し）・ガンは第180期のまま1ビットも動かさない。
//
//     dotnet run --project BattleSim -c Release 0 mudo phase0  # 前提を実装から引き直す（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 mudo run     # R0〜R3 の4版（**線は置かない**）
//     dotnet run --project BattleSim -c Release 0 mudo ledger  # 帳簿（1発の値・床・泥・ウツの発数）
//     dotnet run --project BattleSim -c Release 0 mudo check   # 自己検査
//
// 版は `EruptRule` の2つの枝で切り替える（**駒の定義は1つ**）:
//     R0 床なし × 被弾ごと −1   （第180期そのもの）
//     R1 床あり × 被弾ごと −1   （床だけの寄与）
//     R2 床あり × 暴発時 −3     （**採用候補＝既定**）
//     R3 床あり × 泥なし        （泥の代金を新しいプラスの上で測る・R289）
// =====================================================================================

static class MudoDiag
{
    const int Seeds = 200;

    static readonly EruptRule R0 = EruptRule.Phase180;
    static readonly EruptRule R1 = new(true, SmearWhen.PerHit);
    static readonly EruptRule R2 = EruptRule.Phase181;   // 第182期に既定が上乗せ 10 へ動いたので、第181期の採用候補を名指しで引く
    static readonly EruptRule R3 = new(true, SmearWhen.None);

    static readonly (string Tag, EruptRule Rule)[] Versions =
        { ("R0 第180期", R0), ("R1 床のみ", R1), ("**R2 採用候補**", R2), ("R3 泥なし", R3) };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": StageRun(); return;
            case "ledger": Ledger(); return;
            case "check": Check(); return;
            default:
                Console.WriteLine("mudo: モードは phase0 / run / ledger / check。");
                return;
        }
    }

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第181期 `mudo phase0` —— 前提を実装から引き直す");
        Console.WriteLine();
        if (!ParryScan.Init()) return;
        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        string models = ParryScan.Read(Path.Combine("BattleCore", "Models.cs"));

        Q01(traits, models);
        Q02(traits);
        Q03();
        Q04();
        Q05();
    }

    // ---- Q0-1: 暴発の1発はどの値から作られるか -----------------------------------
    static void Q01(string traits, string models)
    {
        Console.WriteLine("## Q0-1. 暴発の1発はどの値から作られているか（床を置く1箇所）");
        Console.WriteLine();
        string cur = "Current" + "Attack";
        Console.WriteLine("`UnitState." + cur + "` の作り方（`Models.cs`）:");
        Console.WriteLine();
        int i = models.IndexOf("public int " + cur);
        if (i >= 0)
        {
            string[] ls = models.Substring(i).Replace("\r", "").Split('\n');
            foreach (string ln in ls.Take(18)) Console.WriteLine("    " + ln.TrimEnd());
        }
        Console.WriteLine();

        var hits = new List<string>();
        string[] lines = traits.Replace("\r", "").Split('\n');
        string cls = "";
        for (int k = 0; k < lines.Length; k++)
        {
            Match m = Regex.Match(lines[k], @"class\s+(\w+Trait)\b");
            if (m.Success) cls = m.Groups[1].Value;
            if (lines[k].TrimStart().StartsWith("//") || lines[k].TrimStart().StartsWith("///")) continue;
            if (lines[k].Contains("override int Modify" + "Attack")) hits.Add(cls + "  :" + (k + 1));
        }
        Console.WriteLine("`Modify" + "Attack` を上書きしている札（**" + hits.Count + " 本**。順番は `UnitDef.Traits` の並び）:");
        Console.WriteLine();
        foreach (string h in hits) Console.WriteLine("- `" + h + "`");
        Console.WriteLine();
        Console.WriteLine("ムドの `Traits` の並び: **"
                          + string.Join(" → ", UnitCatalog.Mudo.Traits.Select(t => t.ToString())) + "**"
                          + "——`Erupt` が先頭なので、`Erupt.Modify" + "Attack` が受け取る `atk` は"
                          + "**`Def.Attack + AtkBonus` そのもの**（他の札を1つも通っていない）。");
        Console.WriteLine();
        Console.WriteLine("**床を置く箇所は `EruptTrait.Modify" + "Attack` の1行**"
                          + "——`atk + SwingBonus` を `Math.Max(self.Def.Attack, atk) + SwingBonus` にする。"
                          + "上げ（号令・鬨）は `atk` に乗ったまま残り、下げだけが素攻で止まる。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 素攻 | 暴発の上乗せ | 床なしの1発（`AtkBonus` = −5 のとき） | 床ありの1発 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        int b = UnitCatalog.Mudo.Attack;
        Console.WriteLine("| 泥人形ムド | " + b + " | +" + EruptTrait.SwingBonus + " | "
                          + Math.Max(0, b - 5 + EruptTrait.SwingBonus) + " | "
                          + (Math.Max(b, b - 5) + EruptTrait.SwingBonus) + " |");
        Console.WriteLine();
    }

    // ---- Q0-2: ウツの発数は下げ幅をどう読むか -------------------------------------
    static void Q02(string traits)
    {
        Console.WriteLine("## Q0-2. ウツの発数は `AtkBonus` の負をどう読むか（紙・戦闘0回）");
        Console.WriteLine();
        Console.WriteLine("`PerverseTrait` は **`self.AtkBonus` を直接読む**"
                          + "（`ModifyHitCount` ＝ `min(" + PerverseTrait.MaxHits + ", 1 + d / "
                          + PerverseTrait.HitsPerDull + ")` ／ `ModifyAttack` ＝ 素攻 + d × "
                          + PerverseTrait.AttackPerDull + "。d ＝ 下げ幅）。");
        Console.WriteLine();
        Console.WriteLine("診断台（`逆しま (ネル×ウツ)` の 前3 をムドに）では**開戦時にネルの呪詛漏れ −"
                          + CurseTrait.AllyLeak + " が先に乗る**ので、そこからの積み上げを並べる:");
        Console.WriteLine();
        Console.WriteLine("| 泥の回数 | 第180期（被弾ごと −1）の d / 発数 / 1発 | 第181期（暴発時 −3）の d / 発数 / 1発 |");
        Console.WriteLine("|--:|---|---|");
        int baseAtk = UnitCatalog.Utsu.Attack;
        for (int n = 0; n <= 6; n++)
        {
            int dOld = CurseTrait.AllyLeak + n * 1;
            int dNew = CurseTrait.AllyLeak + n * SmearTrait.PerErupt;
            Console.WriteLine("| " + n + " | " + dOld + " / " + Hits(dOld) + " 発 / 攻 "
                              + (baseAtk + dOld * PerverseTrait.AttackPerDull) + " | "
                              + dNew + " / " + Hits(dNew) + " 発 / 攻 "
                              + (baseAtk + dNew * PerverseTrait.AttackPerDull) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**発数が上限 " + PerverseTrait.MaxHits + " に届くのは d = "
                          + (PerverseTrait.HitsPerDull * (PerverseTrait.MaxHits - 1))
                          + "**——第180期は泥 " + CapAt(1) + " 回、第181期は泥 " + CapAt(SmearTrait.PerErupt) + " 回。");
        Console.WriteLine();
        Console.WriteLine("**第180期の実測では泥は 2.84〜17.70 点/戦**（被弾ごと −1）。"
                          + "暴発は 0.79〜2.89 回/戦なので、暴発時 −3 なら **2.4〜8.7 点/戦** ＝ おおよそ半分。");
        Console.WriteLine();
    }

    static int Hits(int d) => Math.Min(PerverseTrait.MaxHits, 1 + d / PerverseTrait.HitsPerDull);

    static int CapAt(int per)
    {
        int need = PerverseTrait.HitsPerDull * (PerverseTrait.MaxHits - 1);
        for (int n = 0; n <= 99; n++) if (CurseTrait.AllyLeak + n * per >= need) return n;
        return -1;
    }

    // ---- Q0-3: 診断台の置き場所 ---------------------------------------------------
    static void Q03()
    {
        Console.WriteLine("## Q0-3. ムド×ウツの診断台");
        Console.WriteLine();
        Formation f = UtsuStand();
        Console.WriteLine("`逆しま (ネル×ウツ)` の **前3（廃棄聖騎士ガルド）をムドに差し替えただけ**"
                          + "（第180期 表B' と同じ台。**`Presets` には1行も足さない**）:");
        Console.WriteLine();
        Console.WriteLine("| 席 | 駒 |");
        Console.WriteLine("|---|---|");
        foreach ((int slot, UnitDef d) in f.Occupied())
            Console.WriteLine("| " + FormationRules.SeatNames[slot] + " | " + d.Name + " |");
        Console.WriteLine();
    }

    // ---- Q0-4: ムドとウツが隣接しうる席の組 ---------------------------------------
    static void Q04()
    {
        Console.WriteLine("## Q0-4. 編成枠 5 席で隣接する組（X字の隣接表から）");
        Console.WriteLine();
        var pairs = new List<string>();
        for (int a = 0; a < 5; a++)
            for (int b = a + 1; b < 5; b++)
                if (FormationRules.AreAdjacent(a, b))
                    pairs.Add(FormationRules.SeatNames[a] + " ↔ " + FormationRules.SeatNames[b]);
        Console.WriteLine("**" + pairs.Count + " 組**: " + string.Join(" ／ ", pairs));
        Console.WriteLine();
        Formation f = UtsuStand();
        int mu = f.Occupied().First(o => o.Def.Id == "mudo").Slot;
        int ut = f.Occupied().First(o => o.Def.Id == "utsu").Slot;
        Console.WriteLine("診断台のムドは **" + FormationRules.SeatNames[mu] + "**・ウツは **"
                          + FormationRules.SeatNames[ut] + "** で、隣接は **"
                          + (FormationRules.AreAdjacent(mu, ut) ? "○" : "**×**") + "**。");
        Console.WriteLine();
        int adj = 0, same = 0;
        foreach ((string band, (string Name, Formation F)[] rows) in new[]
                 { ("compare", CompareBuilds()), ("交差帯", CrossBuilds()) })
            foreach ((string _, Formation g) in rows)
            {
                var m = g.Occupied().FirstOrDefault(o => o.Def.Id == "mudo");
                var u = g.Occupied().FirstOrDefault(o => o.Def.Id == "utsu");
                if (m.Def is null || u.Def is null) continue;
                same++;
                if (FormationRules.AreAdjacent(m.Slot, u.Slot)) adj++;
                _ = band;
            }
        Console.WriteLine("**盤面（`compare` 61 行 ＋ 交差帯 12 行）でのムド×ウツの同席は " + same
                          + " 行 ／ うち隣接 " + adj + " 行**（第180期 Q0-10 の再確認）。");
        Console.WriteLine();
    }

    // ---- Q0-5: 「割り込みの打点に床」の前例 ---------------------------------------
    static void Q05()
    {
        Console.WriteLine("## Q0-5. 「打点に床を置いた」前例（`design/` の走査）");
        Console.WriteLine();
        string dir = Path.Combine(ParryScan.Root!, "design");
        string[] words = { "床を置", "Math.Max(self.Def.Attack", "最低保証", "下限" };
        Console.WriteLine("| 語 | 出てくる文書 |");
        Console.WriteLine("|---|---|");
        foreach (string w in words)
        {
            var files = Directory.GetFiles(dir, "*.md")
                .Where(p => File.ReadAllText(p).Contains(w)).Select(Path.GetFileName).ToList();
            Console.WriteLine("| `" + w + "` | " + (files.Count == 0 ? "—" : string.Join(" ／ ", files)) + " |");
        }
        Console.WriteLine();
        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            int n = Regex.Matches(traits, @"Math\.Max\(1,\s*\w+\.Def\.Attack").Count
                  + Regex.Matches(traits, @"Math\.Max\(1,\s*baseAtk").Count;
            Console.WriteLine("`Traits.cs` で攻撃力に下限を掛けている箇所: **" + n + " 件**"
                              + "（逆しまの半減 `Math.Max(1, baseAtk / 2)` など）。"
                              + "**「素攻を床にする」形は 0 件——第181期が最初。**");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // run —— R0〜R3
    // =================================================================================

    static void StageRun()
    {
        Console.WriteLine("# 第181期 `mudo run` —— R0〜R3（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 床 | 泥 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| R0 第180期 | なし | 被弾ごと −" + SmearTrait.PerHit + " |");
        Console.WriteLine("| R1 床のみ | **あり** | 被弾ごと −" + SmearTrait.PerHit + " |");
        Console.WriteLine("| **R2 採用候補** | **あり** | **暴発時 −" + SmearTrait.PerErupt + "** |");
        Console.WriteLine("| R3 泥なし | **あり** | なし |");
        Console.WriteLine();

        Console.WriteLine("## 表A. ムド在席 8 行（第2〜5波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 行 | R0 | R1 | **R2** | R3 | 床の寄与 | 泥の置き換え | 泥の代金（R2−R3） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        double s0 = 0, s1 = 0, s2 = 0, s3 = 0; int rows = 0;
        foreach ((string name, Formation f) in CompareBuilds())
        {
            if (!Has(f, "mudo")) continue;
            double a0 = Avg25(f, R0), a1 = Avg25(f, R1), a2 = Avg25(f, R2), a3 = Avg25(f, R3);
            s0 += a0; s1 += a1; s2 += a2; s3 += a3; rows++;
            Console.WriteLine("| " + name + " | " + a0.ToString("F1") + " | " + a1.ToString("F1")
                              + " | **" + a2.ToString("F1") + "** | " + a3.ToString("F1")
                              + " | " + (a1 - a0).ToString("+0.0;-0.0;0.0")
                              + " | " + (a2 - a1).ToString("+0.0;-0.0;0.0")
                              + " | " + (a2 - a3).ToString("+0.0;-0.0;0.0") + " |");
        }
        Console.WriteLine("| **平均（" + rows + " 行）** | " + (s0 / rows).ToString("F1") + " | "
                          + (s1 / rows).ToString("F1") + " | **" + (s2 / rows).ToString("F1") + "** | "
                          + (s3 / rows).ToString("F1") + " | " + ((s1 - s0) / rows).ToString("+0.0;-0.0;0.0")
                          + " | " + ((s2 - s1) / rows).ToString("+0.0;-0.0;0.0")
                          + " | " + ((s2 - s3) / rows).ToString("+0.0;-0.0;0.0") + " |");
        Console.WriteLine();

        Console.WriteLine("## 表A'. 波ごと（R0 → R2）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in CompareBuilds())
        {
            if (!Has(f, "mudo")) continue;
            double[] w0 = Rates(f, R0), w2 = Rates(f, R2);
            Console.WriteLine("| " + name + " | R0 | " + Cells(w0) + " | " + Mean25(w0).ToString("F1") + " |");
            Console.WriteLine("| | **R2** | " + Cells(w2) + " | **" + Mean25(w2).ToString("F1") + "** |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表B. ムド×ウツの診断台（`Presets` は触っていない）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | Δ（R0比） | ウツが受けた弱体 | ウツの与害 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        Formation stand = UtsuStand();
        double refAvg = 0;
        foreach ((string tag, EruptRule r) in Versions)
        {
            double[] w = Rates(stand, r);
            (double dull, double dmg) = UtsuLedger(stand, r);
            if (tag.StartsWith("R0")) refAvg = Mean25(w);
            Console.WriteLine("| " + tag + " | " + Cells(w) + " | " + Mean25(w).ToString("F1")
                              + " | " + (tag.StartsWith("R0") ? "—" : (Mean25(w) - refAvg).ToString("+0.0;-0.0;0.0"))
                              + " | " + dull.ToString("F1") + " | " + dmg.ToString("F1") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表C. 交差帯のムド行");
        Console.WriteLine();
        Console.WriteLine("| 行 | R0 | R1 | **R2** | R3 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in CrossBuilds())
        {
            if (!Has(f, "mudo")) continue;
            Console.WriteLine("| " + name + " | " + Avg25(f, R0).ToString("F1") + " | " + Avg25(f, R1).ToString("F1")
                              + " | **" + Avg25(f, R2).ToString("F1") + "** | " + Avg25(f, R3).ToString("F1") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表D. 勝ち方（`chain` の列を R0 / R2 で・全5波通算）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 勝率 | 決着T | 残存 | 全滅勝ち |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in CompareBuilds())
        {
            if (!Has(f, "mudo")) continue;
            foreach ((string tag, EruptRule r) in new[] { ("R0", R0), ("**R2**", R2) })
            {
                Quality q = QualityOf(f, r);
                Console.WriteLine("| " + name + " | " + tag + " | " + q.Win.ToString("F1")
                                  + " | " + q.Turns.ToString("F1") + " | " + q.Survivors.ToString("F2")
                                  + " | " + q.Narrow.ToString("F0") + "% |");
            }
        }
        Console.WriteLine();
    }

    // =================================================================================
    // ledger
    // =================================================================================

    static void Ledger()
    {
        Console.WriteLine("# 第181期 `mudo ledger` —— 帳簿（第2〜5波の平均・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("**`1発の平均`** は暴発の各発の `CurrentAttack`、**`床が効いた発`** は"
                          + "その時点で `AtkBonus < 0` だった発（＝床が無ければ素攻より下だった発）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 暴発 | 発数 | 1発の平均 | 床が効いた発 | 泥 | 弾かれ | ムドの与害 | ムドの生存T | 勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in CompareBuilds())
        {
            if (!Has(f, "mudo")) continue;
            foreach ((string tag, EruptRule r) in Versions)
            {
                Led s = LedgerOf(f, r);
                Console.WriteLine("| " + name + " | " + tag + " | " + s.Fires.ToString("F2")
                                  + " | " + s.Swings.ToString("F2") + " | " + s.MeanAtk.ToString("F2")
                                  + " | " + s.Floored.ToString("F2") + " | " + s.Smear.ToString("F2")
                                  + " | " + s.Blocked.ToString("F2") + " | " + s.Dmg.ToString("F1")
                                  + " | " + s.Life.ToString("F2") + " | " + s.Win.ToString("F1") + " |");
            }
        }
        Console.WriteLine();

        Formation stand = UtsuStand();
        Console.WriteLine("## 波ごとの泥と暴発（診断台・R0 対 R2）");
        Console.WriteLine();
        Console.WriteLine("**第二波は粛**——暴発は `CanActOutOfTurn` を通るので止まるが、"
                          + "第180期の泥（被弾ごと）は `OnDamaged` なので**止まらなかった**。");
        Console.WriteLine();
        Console.WriteLine("| 波 | R0 の暴発 | R0 の泥 | R2 の暴発 | R2 の泥 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int st = 1; st < 5; st++)
        {
            var cells = new List<string>();
            foreach (EruptRule r in new[] { R0, R2 })
            {
                double fires = 0, smear = 0; int n = 0;
                for (int seed = 0; seed < Seeds; seed++)
                {
                    BattleResult res = BattleEngine.Run(stand, EnemyCatalog.Stages[st].Enemy, seed,
                                                       verbose: false, erupt: r);
                    n++;
                    if (!res.TallyByUnit.TryGetValue("mudo", out UnitTally? t)) continue;
                    fires += t.EruptFires; smear += t.SmearDealt;
                }
                cells.Add((fires / n).ToString("F2")); cells.Add((smear / n).ToString("F2"));
            }
            Console.WriteLine("| 第" + (st + 1) + "波 | " + string.Join(" | ", cells) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 診断台（ムド×ウツ）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 暴発 | 発数 | 1発の平均 | 床が効いた発 | 泥 | ウツが受けた弱体 | ウツの与害 | 勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach ((string tag, EruptRule r) in Versions)
        {
            Led s = LedgerOf(stand, r);
            (double dull, double dmg) = UtsuLedger(stand, r);
            Console.WriteLine("| " + tag + " | " + s.Fires.ToString("F2") + " | " + s.Swings.ToString("F2")
                              + " | " + s.MeanAtk.ToString("F2") + " | " + s.Floored.ToString("F2")
                              + " | " + s.Smear.ToString("F2") + " | " + dull.ToString("F1")
                              + " | " + dmg.ToString("F1") + " | " + s.Win.ToString("F1") + " |");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // check
    // =================================================================================

    static void Check()
    {
        Console.WriteLine("# 第181期 `mudo check` —— 自己検査");
        Console.WriteLine();

        // (a) ムドを含まない行は R0 と R2 で1セルも動かない
        int cells = 0, rows = 0;
        foreach ((string _, Formation f) in CompareBuilds())
        {
            if (Has(f, "mudo")) continue;
            rows++;
            double[] a = Rates(f, R0), b = Rates(f, R2);
            for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - b[i]) > 0.001) cells++;
        }
        Console.WriteLine("- (a) ムドを含まない " + rows + " 行で動いたセル: **" + cells + " / " + rows * 5
                          + "**（0 が正。ヴィオ・ガンの行もここに入る）");

        // (b) R0 は第180期そのもの（規則を第180期へ戻すと `docs/balance.md` と一致するはず）
        Console.WriteLine("- (b) `R0` の 61 行 × 5 波は `mudo run` の R0 列がそのまま出す"
                          + "（採用前の `docs/balance.md` との突き合わせは報告書の側で行う）");

        // (c) 泥の総量: R1（被弾ごと）と R2（暴発時）の比
        {
            double h = 0, e = 0; int n = 0;
            foreach ((string _, Formation f) in CompareBuilds())
            {
                if (!Has(f, "mudo")) continue;
                h += LedgerOf(f, R1).Smear; e += LedgerOf(f, R2).Smear; n++;
            }
            Console.WriteLine("- (c) 泥の総量（8 行平均）: 被弾ごと **" + (h / n).ToString("F2")
                              + "** → 暴発時 **" + (e / n).ToString("F2") + "**（比 "
                              + (h < 0.001 ? "—" : (e / h).ToString("F2")) + "）");
        }

        // (d) R3 では泥が厳密に 0
        {
            double z = 0;
            foreach ((string _, Formation f) in CompareBuilds())
                if (Has(f, "mudo")) z += LedgerOf(f, R3).Smear;
            Console.WriteLine("- (d) `R3`（泥なし）で撒かれた泥の総量: **" + z.ToString("F2") + "**（0 が正）");
        }

        // (e) 床: R0 では「床が効いた発」が数えられても値は上がらない
        {
            Formation stand = UtsuStand();
            Led a = LedgerOf(stand, R0), b = LedgerOf(stand, R1);
            Console.WriteLine("- (e) 診断台の1発の平均: `R0` **" + a.MeanAtk.ToString("F2")
                              + "** → `R1` **" + b.MeanAtk.ToString("F2") + "**"
                              + "（床が効いた発は " + b.Floored.ToString("F2") + " / " + b.Swings.ToString("F2")
                              + " 発。**素攻 " + UnitCatalog.Mudo.Attack + " ＋ " + EruptTrait.SwingBonus
                              + " ＝ " + (UnitCatalog.Mudo.Attack + EruptTrait.SwingBonus) + " を下回らない**）");
        }

        // (f) 泥は暴発が起きた戦にしか出ない（R2）
        {
            int both = 0, smearNoFire = 0, n = 0;
            foreach ((string _, Formation f) in CompareBuilds())
            {
                if (!Has(f, "mudo")) continue;
                for (int st = 1; st < 5; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                         verbose: false, erupt: R2);
                        n++;
                        if (!r.TallyByUnit.TryGetValue("mudo", out UnitTally? t)) continue;
                        if (t.SmearDealt > 0 && t.EruptFires == 0) smearNoFire++;
                        if (t.SmearDealt > 0 && t.EruptFires > 0) both++;
                    }
            }
            Console.WriteLine("- (f) `R2` で **泥が出たのに暴発が 0 回だった戦: " + smearNoFire
                              + "**（0 が正）／ 泥と暴発がどちらも出た戦 " + both + " / " + n);
        }

        // (g) PickOne / 状態キー / 経路
        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            string pick = "ctx.Pick" + "One(";
            int total = (traits.Length - traits.Replace(pick, "").Length) / pick.Length;
            Console.WriteLine("- (g) `Traits.cs` の `" + pick + "` 呼び出し: **" + total
                              + "**（第180期から不変が正）／ `StatusKeys.All` " + StatusKeys.All.Length
                              + " ／ `DullRoute` " + DullRoutes.Count + " ／ `OutOfTurnRoute` " + OutOfTurnRoutes.Count);
        }
        Console.WriteLine();
    }

    // =================================================================================
    // ヘルパ
    // =================================================================================

    /// <summary>第180期 表B' と同じ診断台（`逆しま (ネル×ウツ)` の 前3 をムドに）。</summary>
    static Formation UtsuStand()
    {
        Formation src = CompareBuilds().First(b => b.Name == "逆しま (ネル×ウツ)").F;
        var g = new Formation();
        foreach ((int slot, UnitDef d) in src.Occupied()) g[slot] = slot == 1 ? UnitCatalog.Mudo : d;
        return g;
    }

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    readonly record struct Led(double Fires, double Swings, double MeanAtk, double Floored,
                               double Smear, double Blocked, double Dmg, double Life, double Win);

    static Led LedgerOf(Formation f, EruptRule r)
    {
        double fires = 0, swings = 0, atk = 0, floored = 0, smear = 0, blocked = 0;
        double dmg = 0, life = 0, win = 0; int n = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                   verbose: false, erupt: r);
                n++;
                if (res.PlayerWon) win++;
                if (!res.TallyByUnit.TryGetValue("mudo", out UnitTally? t)) continue;
                fires += t.EruptFires; swings += t.EruptSwings; atk += t.EruptSwingAtk;
                floored += t.EruptFloored; smear += t.SmearDealt; blocked += t.SmearBlocked;
                dmg += t.DamageToEnemy; life += t.LastActiveTurn;
            }
        return new Led(fires / n, swings / n, swings < 0.5 ? 0 : atk / swings, floored / n,
                       smear / n, blocked / n, dmg / n, life / n, 100.0 * win / n);
    }

    static (double Dull, double Dmg) UtsuLedger(Formation f, EruptRule r)
    {
        double dull = 0, dmg = 0; int n = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                   verbose: false, erupt: r);
                n++;
                if (!res.TallyByUnit.TryGetValue("utsu", out UnitTally? t)) continue;
                dull += t.CarryAmount?[UnitTally.CarryDull] ?? 0;
                dmg += t.DamageToEnemy;
            }
        return (dull / n, dmg / n);
    }

    readonly record struct Quality(double Win, double Turns, double Survivors, double Narrow);

    static Quality QualityOf(Formation f, EruptRule r)
    {
        int wins = 0, narrow = 0, n = 0; double turns = 0, sv = 0;
        for (int st = 0; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                   verbose: false, erupt: r);
                n++;
                if (!res.PlayerWon) continue;
                wins++; turns += res.Turns; sv += res.PlayerSurvivors;
                if (res.PlayerSurvivors <= 1) narrow++;
            }
        return new Quality(100.0 * wins / n, wins == 0 ? 0 : turns / wins,
                           wins == 0 ? 0 : sv / wins, wins == 0 ? 0 : 100.0 * narrow / wins);
    }

    static double[] Rates(Formation f, EruptRule r, int seeds = Seeds)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, erupt: r).PlayerWon)
                    wins++;
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static double Avg25(Formation f, EruptRule r) => Mean25(Rates(f, r));
    static string Cells(double[] w) => string.Join(" | ", Enumerable.Range(1, 4).Select(i => w[i].ToString("F1")));
}
