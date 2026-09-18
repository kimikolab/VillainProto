using BattleCore;
using static Common;

// =====================================================================================
// gust モード（第151期） —— バサの手番を「突風」にする（薙ぎ化 ＋ 確率で転倒）
//
// 指示書は design/PHASE151_GUST_SPEC.md ／ 報告は design/PHASE151_GUST.md。
//
// **問いは「攻撃の形を変えて良くなるか」**であって「手番を捨てて良くなるか」ではない
// ——第148期が `[Skill]` で −5.9〜−23.0pt を出して閉じた道なので、**手番は捨てない。**
//
//     dotnet run --project BattleSim -c Release 0 gust phase0   # 前提を実装から引き直す（戦闘0回 ＋ 供給の見積り）
//     dotnet run --project BattleSim -c Release 0 gust scan     # 台の下見（V0 だけ）
//
// **攻撃力と型は規則では振れない**（`UnitDef` が決める）ので、段A は**この診断のローカルの
// `UnitDef`** で作る（第60期の火選りの移設と同じ扱い）。転倒だけが `ShufflerRule` の枝。
// =====================================================================================

static class GustDiag
{
    const int Seeds = 200;

    /// <summary>対照。<b>現行の既定</b>（ターン頭・混乱3回）＋ 攻7・単体のバサ。</summary>
    static readonly ShufflerRule V0 = ShufflerRule.Default;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "scan": Scan(); return;
            default:
                Console.WriteLine("gust: モードは phase0 / scan。"
                                  + "（run / gale / compare / check は段A 以降で足す）");
                return;
        }
    }

    // =================================================================================
    // 駒 —— 攻撃力と型だけを差し替えた写し（数値も特性も他は1つも触らない）
    // =================================================================================

    /// <summary><c>UnitCatalog.Basa</c> の写しに攻撃力と攻撃型だけを載せる。</summary>
    static UnitDef Basa(int attack, AttackPattern pattern) => new()
    {
        Id = UnitCatalog.Basa.Id,
        Name = UnitCatalog.Basa.Name,
        MaxHp = UnitCatalog.Basa.MaxHp,
        Attack = attack,
        Speed = UnitCatalog.Basa.Speed,
        Pattern = pattern,
        Traits = UnitCatalog.Basa.Traits,
        Advances = UnitCatalog.Basa.Advances,
        PlusText = UnitCatalog.Basa.PlusText,
        MinusText = UnitCatalog.Basa.MinusText,
        Flavor = UnitCatalog.Basa.Flavor,
    };

    // =================================================================================
    // 台 —— 第148期の4台（被弾変換が厚い／薄い／ササ同席／手番市場）＋ 陰性対照
    // =================================================================================

    /// <summary>埋め草は<b>特性を1つも持たない素体</b>（第143期の則。敵の数値に触らせない）。</summary>
    static UnitDef Plain(string id, int hp, int atk, int spd = 8) => new()
    {
        Id = id, Name = "素体" + id, MaxHp = hp, Attack = atk, Speed = spd,
        Advances = true, Traits = Array.Empty<TraitId>()
    };

    static UnitDef Fill(string id) => Plain(id, 110, 34);

    static (string Name, Formation F)[] Rigs(UnitDef basa) => new (string, Formation)[]
    {
        ("被弾変換が厚い", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Mudo, center: UnitCatalog.Doha,
            back1: basa, back3: Fill("pa"))),

        ("被弾変換が薄い", Formation.Build(
            front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Kiri,
            back1: basa, back3: Fill("pa"))),

        ("ササ同席", Formation.Build(
            front1: UnitCatalog.Sasa, front3: UnitCatalog.Gare, center: UnitCatalog.Borg,
            back1: basa, back3: Fill("pa"))),

        ("手番市場（据え）", Formation.Build(
            front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Ban,
            back1: basa, back3: Fill("pa"))),

        // 陰性対照。**バサを同数値・特性なしの素体に落としただけ**（HP56 / 攻7 / 速8）。
        ("動かす機構なし", Formation.Build(
            front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Kiri,
            back1: Plain("pb", 56, 7), back3: Fill("pa"))),
    };

    // =================================================================================
    // phase0 —— 前提を実装から引き直す
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第151期 `gust phase0` —— 前提を実装から引き直す");
        Console.WriteLine();

        string? tr = FindSource("BattleCore", "Traits.cs");
        string? eng = FindSource("BattleCore", "BattleEngine.cs");
        string? mo = FindSource("BattleCore", "Models.cs");
        if (tr is null || eng is null || mo is null)
        {
            Console.WriteLine("**`BattleCore` のソースが引けない。止める**（第117期）。");
            return;
        }
        string[] T = File.ReadAllLines(tr), E = File.ReadAllLines(eng), M = File.ReadAllLines(mo);

        // --- Q0-2 -------------------------------------------------------------------
        Console.WriteLine("## Q0-2. `ModifyAttack` / `ModifyPattern` の前例");
        Console.WriteLine();
        Console.WriteLine("**走査は `override ... ModifyPattern` の宣言から引く**"
                          + "（第127期の「手で並べると3期に1度は落ちる」）。");
        Console.WriteLine();
        Console.WriteLine("| クラス | 上がる先 | 条件 |");
        Console.WriteLine("|---|---|---|");
        int found = 0;
        for (int i = 0; i < T.Length; i++)
        {
            if (!T[i].Contains("override AttackPattern ModifyPattern")) continue;
            found++;
            // 直前のクラス宣言を遡って引く（`record struct` も宣言として数える・第130期）。
            string cls = "—";
            for (int j = i; j >= 0; j--)
                if (System.Text.RegularExpressions.Regex.IsMatch(
                        T[j], @"(class|record struct|struct|interface)\s+(\w+)"))
                {
                    cls = System.Text.RegularExpressions.Regex.Match(
                        T[j], @"(?:class|record struct|struct|interface)\s+(\w+)").Groups[1].Value;
                    break;
                }
            string body = string.Join(" ", T.Skip(i).Take(4)).Replace("  ", " ").Trim();
            string to = body.Contains("AttackPattern.Sweep") ? "薙ぎ"
                      : body.Contains("AttackPattern.Pierce") ? "貫き"
                      : body.Contains("AttackPattern.All") ? "全体" : "（複数）";
            Console.WriteLine("| `" + cls + "` | " + to + " | `"
                              + body.Substring(body.IndexOf("=>") >= 0 ? body.IndexOf("=>") : 0)
                                    .Replace("|", "\\|") + "` |");
        }
        Console.WriteLine();
        Console.WriteLine("- 実装 **" + found + " 本**（走査が空なら止める・第117期）: "
                          + (found > 0 ? "○" : "**×**"));
        Console.WriteLine();
        Console.WriteLine("**`ModifyPattern` は攻撃のたびに評価される**"
                          + "（`UnitState.CurrentPattern` が毎回 `Traits` を回す）: **"
                          + (M.Any(l => l.Contains("p = t.ModifyPattern(this, p);")) ? "○" : "**×**") + "**");
        Console.WriteLine();
        Console.WriteLine("### 決め —— **`Def.Pattern` を薙ぎにする。`ModifyPattern` は使わない**");
        Console.WriteLine();
        Console.WriteLine("| | `Def.Pattern = Sweep` | `ModifyPattern` で無条件に薙ぎ |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| `docs/units.md` の型の分布 | **出る** | 出ない（常時の型は単体のまま） |");
        Console.WriteLine("| 条件 | 無い（常時） | 無い（常時）——**無条件なら窓口を使う意味が無い** |");
        Console.WriteLine("| 毎回の評価コスト | 0 | 攻撃のたびに `Traits` を1周 |");
        Console.WriteLine();
        Console.WriteLine("**攻撃力も `Def.Attack` を下げる**（`ModifyAttack` で減らさない）"
                          + "——減らす形にすると「素は7だが実際は3」という二重帳簿になり、");
        Console.WriteLine("`CurrentAttack` を読む側（駆り立ての選択・転嫁の流し先・`StatSnapshot`）が"
                          + "**素の値で並べ替える**（第75期）。");
        Console.WriteLine();

        // --- Q0-3 -------------------------------------------------------------------
        Console.WriteLine("## Q0-3. 薙ぎの巻き込みの実装（**§0 の勘定を引き直す**）");
        Console.WriteLine();
        Console.WriteLine("- 副次目標の倍率 `BattleContext.SecondaryPercent` = **"
                          + BattleContext.SecondaryPercent + "%**");
        Console.WriteLine("- 適用は `ApplyDamage(extra, Math.Max(1, dealt * SecondaryPercent / 100), ...)` ＝ "
                          + "**整数の切り捨て・床は 1**: "
                          + (E.Any(l => l.Contains("Math.Max(1, dealt * SecondaryPercent / 100)")) ? "○" : "**×**"));
        Console.WriteLine();
        Console.WriteLine("`FormationRules.SweepTargets` の表（標的の席 → 巻き込む席）:");
        Console.WriteLine();
        Console.WriteLine("| 標的 | 巻き込み先 | 編成枠(0-4)だけなら |");
        Console.WriteLine("|---|---|---|");
        for (int s = 0; s < 5; s++)
        {
            var t2 = FormationRules.SweepTargets(s);
            Console.WriteLine("| " + SlotName(s) + " | " + string.Join(" / ", t2.Select(SlotName))
                              + " | **" + t2.Count(x => x < 5) + " 体** |");
        }
        Console.WriteLine();
        Console.WriteLine("> **§0 の「前1 を薙ぐと 前1 ＋ 中央 ＝ 2体」は誤り。**");
        Console.WriteLine("> 前1 の巻き込み先は **前3・中央・○前2** なので、"
                          + "敵が満席なら**主目標を含めて 3 体**に当たる。");
        Console.WriteLine();
        Console.WriteLine("**倍率を入れて出力を引き直す**（主 ＋ 巻き込み ×"
                          + BattleContext.SecondaryPercent + "%・整数切り捨て・床1）:");
        Console.WriteLine();
        Console.WriteLine("| 攻撃力 | 単体 | 薙ぎ 2体 | 薙ぎ 3体 | 現行(攻7単体=7) との差 |");
        Console.WriteLine("|--:|--:|--:|--:|---|");
        foreach (int a in new[] { 2, 3, 4, 5, 6, 7 })
        {
            int sec = Math.Max(1, a * BattleContext.SecondaryPercent / 100);
            Console.WriteLine("| " + a + " | " + a + " | " + (a + sec) + " | " + (a + sec + sec)
                              + " | 薙ぎ3体で **" + (a + sec + sec - 7).ToString("+0;-0;0") + "** |");
        }
        Console.WriteLine();
        Console.WriteLine("**出力中立は 攻4**（3体で 8）で、**攻3 は 5・攻2 は 4 で現行を下回る**"
                          + "——`SecondaryPercent = 60` の整数切り捨てが効く（攻3 の巻き込みは 1 点）。");
        Console.WriteLine("**指示書 §0 の「攻3 が出力中立に近い」は倍率を入れていない勘定だった。**");
        Console.WriteLine("**段A は 2 / 3 / 4 / 5 を振る**（指示書の 2・3 を含み、引き直した中立点の両側を足す）。");
        Console.WriteLine();
        Console.WriteLine("**ただし出力の総量は勝率ではない**（第127期: 貫きは与ダメが増えて勝率が下がる）ので、");
        Console.WriteLine("**薙ぎの値打ちは総量の外にもある**——");
        Console.WriteLine("(i) `SelectTargetChain` は `pattern != Single` で早期リターンするので"
                          + "**庇い・標的・殉教・棘守りを素通りする**（第127期）、");
        Console.WriteLine("(ii) 後列へ直接届く（巻き込み先は列で決まり、`pool` の前列優先を通らない）。");
        Console.WriteLine();

        // 敵の編成の占有（3体に当たる局面がどれだけあるか）
        Console.WriteLine("**敵側の占有**（各波の開幕・編成枠 0-4 と召喚枠）:");
        Console.WriteLine();
        Console.WriteLine("| 波 | 前1 | 前3 | 中央 | 後1 | 後3 | 前1 を薙いだときの体数 |");
        Console.WriteLine("|---|---|---|---|---|---|--:|");
        for (int st = 0; st < 5; st++)
        {
            Formation f = EnemyCatalog.Stages[st].Enemy;
            var occ = f.Occupied().ToDictionary(o => o.Slot, o => o.Def.Name);
            string Cell(int s) => occ.TryGetValue(s, out string? n) ? n : "—";
            int bodies = 1 + FormationRules.SweepTargets(0).Count(x => occ.ContainsKey(x));
            Console.WriteLine("| 第" + (st + 1) + "波 | " + string.Join(" | ", Enumerable.Range(0, 5).Select(Cell))
                              + " | **" + bodies + "** |");
        }
        Console.WriteLine();

        // --- Q0-4 -------------------------------------------------------------------
        Console.WriteLine("## Q0-4. 転倒を乗せる場所と、巻き込みに乗せるか");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 決め | 理由 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| 特性 | **`ShufflerTrait` に足す。新しい `TraitId` を作らない** | "
                          + "同じ駒の効果は1つの特性にまとめる（第147期に `ShuffleStagger` でやった形）。"
                          + "「原因は1つ（風）」という一文も壊れない |");
        Console.WriteLine("| フック | **`OnAfterAttack`** | "
                          + "engine の呼び口は「攻撃1回につき1度・主目標に対してのみ」。"
                          + "**巻き込み先は特性の側で `ctx.SecondaryTargets` を引き直す** |");
        Console.WriteLine("| 呼ばれる順 | **巻き込みのダメージが全部入った後** | "
                          + "`PerformAttack` は 主目標 → `extras` ループ → `OnAfterAttack` の順。"
                          + "**その振りで倒れた相手は既に死んでいる**ので `IsAlive` で自然に落ちる |");
        Console.WriteLine("| 巻き込みに乗せるか | **`GustSecondary` で別のノブ**（既定 true） | "
                          + "主目標だけなら供給が 1/3 になる ＝ **確率とは別の絞り方** |");
        Console.WriteLine("| 味方に乗るか | **乗らない**（`u.TeamId != self.TeamId` で弾く） | "
                          + "混乱したバサは `SecondaryTargets` が味方を返す（`FoesOf` が反転する）。"
                          + "**§4-2「味方側の `Stagger` は 0」を構造で成り立たせる** |");
        Console.WriteLine();
        Console.WriteLine("- `PerformAttack` が `OnAfterAttack` を `extras` ループの**後**で呼ぶこと: **"
                          + (Ordered(E, "foreach (UnitState extra in extras)", "t.OnAfterAttack(this, actor, target, dealt);")
                             ? "○" : "**×**") + "**");
        Console.WriteLine("- `BattleContext.SecondaryTargets` が特性から引けること（`public`）: **"
                          + (E.Any(l => l.Contains("public IReadOnlyList<UnitState> SecondaryTargets")) ? "○" : "**×**") + "**");
        Console.WriteLine();
        Console.WriteLine("**転倒（`StatusKeys.Stagger`）の実装は第144期のものがそのまま残っている**"
                          + "——engine 側の読み手（`TakeTurnCore`）に1行も足さない:");
        Console.WriteLine();
        Console.WriteLine("- `StatusKeys.Stagger` を読む箇所: **"
                          + E.Count(l => l.Contains("StatusKeys.Stagger")) + "** 行（`BattleEngine.cs`）");
        Console.WriteLine("- `StatusKeys.All` に `Stagger` が入っているか（会戦の境界で消える一覧）: **"
                          + (E.Any(l => l.Contains("public static readonly string[] All") && l.Contains("Stagger"))
                             ? "○" : "**×**") + "**");
        Console.WriteLine("- `EmitStagger`（表示専用・第145期）の呼び口: **"
                          + T.Count(l => l.Contains("EmitStagger(")) + "** 行（`Traits.cs`）");
        Console.WriteLine();
        Console.WriteLine("**転倒と混乱は同居する。**「同じトリガーに2つ積まない」（第147期）は"
                          + "**同じトリガー**についての則で、");
        Console.WriteLine("この期の転倒のトリガーは**攻撃が当たったこと**、混乱は**ターン頭に前へ出たこと**で別物である。");
        Console.WriteLine("**`ShuffleStagger` の枝を1つも足さない**のはそのため——枝は「前へ出た敵をどうするか」の軸で、");
        Console.WriteLine("突風は**その軸に乗っていない**（だから直交するノブ `GustPercent` でよい）。");
        Console.WriteLine();

        // --- Q0-5 -------------------------------------------------------------------
        Console.WriteLine("## Q0-5. 転倒の供給量を先に見積もる（**紙で決めない**）");
        Console.WriteLine();
        Console.WriteLine("**現行のバサ**（攻7・単体）の振りを4台 × 第2〜5波 × seed 0.." + (Seeds - 1) + " で実測する:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 振り/戦 | 生存T | 与ダメ/戦 | 薙ぎ化したときの延べ体数（振り × 平均体数） |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        double swAll = 0; int nRig = 0;
        foreach ((string name, Formation f) in Rigs(UnitCatalog.Basa))
        {
            if (name == "動かす機構なし") continue;
            Bas b = BasaOf(f, V0, UnitCatalog.Basa.Id);
            // 敵の編成の平均占有から「前1 を薙いだときの体数」を引く（開幕の値・上限）
            double bodies = 0;
            for (int st = 1; st < 5; st++)
            {
                var occ = EnemyCatalog.Stages[st].Enemy.Occupied().Select(o => o.Slot).ToHashSet();
                bodies += 1 + FormationRules.SweepTargets(0).Count(x => occ.Contains(x));
            }
            bodies /= 4;
            swAll += b.Attacks * bodies; nRig++;
            Console.WriteLine("| " + name + " | " + b.Attacks.ToString("F2") + " | " + b.Last.ToString("F2")
                              + " | " + b.Damage.ToString("F1") + " | **" + (b.Attacks * bodies).ToString("F1")
                              + "**（開幕の占有から引いた上限） |");
        }
        double avgSw = swAll / Math.Max(1, nRig);
        Console.WriteLine();
        Console.WriteLine("- 4台平均の**延べ体数 " + avgSw.ToString("F1") + " / 戦**"
                          + "（開幕の占有で引いた上限。実際は削れるほど痩せる）");
        Console.WriteLine();
        Console.WriteLine("| `GustPercent` | 転倒/戦（上限の見積り） | 第144期の 1.3 回/戦 との比 |");
        Console.WriteLine("|--:|--:|--:|");
        foreach (int p in new[] { 10, 20, 30, 50, 100 })
            Console.WriteLine("| " + p + "% | " + (avgSw * p / 100.0).ToString("F1")
                              + " | ×" + (avgSw * p / 100.0 / 1.3).ToString("F1") + " |");
        Console.WriteLine();
        Console.WriteLine("**掃引点は 10 / 20 / 30% にする**（指示書どおり下から）"
                          + "——20% で第144期の供給におおよそ並ぶ。");
        Console.WriteLine("**見積りは上限である**（開幕の占有で引いた・敵が削れると体数が減る）ので、"
                          + "**実測は `gust gale` の表で取り直す**（第137期・第143期）。");
        Console.WriteLine();

        // --- Q0-6 -------------------------------------------------------------------
        Console.WriteLine("## Q0-6. 測定台（帯は `gust scan`）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 顔ぶれ |");
        Console.WriteLine("|---|---|");
        foreach ((string name, Formation f) in Rigs(UnitCatalog.Basa))
            Console.WriteLine("| " + name + " | " + string.Join(" / ", f.Occupied().Select(o => o.Def.Name)) + " |");
        Console.WriteLine();
        Console.WriteLine("**埋め草に敵の数値へ触る駒（呪詛官ネル・萎縮のクビ）を入れていないこと**（第143期）: "
                          + (Rigs(UnitCatalog.Basa).All(r => r.F.Occupied()
                                .All(o => o.Def.Id != UnitCatalog.Nel.Id && o.Def.Id != UnitCatalog.Kubi.Id))
                             ? "**○**" : "**×**"));
        Console.WriteLine();
        Console.WriteLine("**号令同席の台は外した**（第148期の台5）——号令が払うのは攻撃力で、");
        Console.WriteLine("この期のバサは**攻撃を捨てない**ので死蔵の問題が起きない（測る対象が無い）。");
        Console.WriteLine();

        // --- Q0-7 -------------------------------------------------------------------
        Console.WriteLine("## Q0-7. 過去に「単体を薙ぎに変えた」期");
        Console.WriteLine();
        Console.WriteLine("| 期 | 何を測ったか | 値 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| 第127期 `grade` | **同じ攻撃力のまま**単体 → 薙ぎ | "
                          + "与ダメ 84.5 → 124.5（**+40.0**）・勝率 +15.5pt（台1）／ +21.4pt（台2） |");
        Console.WriteLine("| 第127期 | 薙ぎ → 貫き | 与ダメ +4.5 なのに勝率 **−3.1pt**（倒しきる力が落ちる） |");
        Console.WriteLine("| 第127期 | 薙ぎ → 全体 | +80.7 / +20.6pt（**段は線形でない**） |");
        Console.WriteLine("| 第116期 | 積み過ぎ（バン・`AtkBonus ≥ 5` で薙ぎ） | "
                          + "与ダメ +47% に対し**撃破は +11%**——巻き込みで削った相手を倒しきる保証が無い |");
        Console.WriteLine("| 第98期 / 第127期 | リィカ（墓守・3層で薙ぎ） | "
                          + "格上げの一覧の正本は `grade phase0` の Q0-1（走査 90 本 → 実装 " + found + " 本） |");
        Console.WriteLine();
        Console.WriteLine("> **第127期は攻撃力を据え置いたまま型だけを上げている。**");
        Console.WriteLine("> **この期は攻撃力を下げて型を上げる**ので、+40.0 はそのままは当たらない");
        Console.WriteLine("> ——引き直した勘定（Q0-3）では**攻4 でようやく出力が並ぶ**。");
        Console.WriteLine();
        Console.WriteLine("**味方の常時の攻撃型**（`UnitCatalog.All` 52 枚）:");
        Console.WriteLine();
        foreach (var g in UnitCatalog.All.GroupBy(u => u.Pattern).OrderBy(g => (int)g.Key))
            Console.WriteLine("- " + g.Key + ": **" + g.Count() + " 枚**"
                              + (g.Count() <= 5 ? "（" + string.Join(" / ", g.Select(u => u.Name)) + "）" : ""));
        Console.WriteLine();
        Console.WriteLine("**バサを薙ぎにすると常時の薙ぎは "
                          + UnitCatalog.All.Count(u => u.Pattern == AttackPattern.Sweep) + " → "
                          + (UnitCatalog.All.Count(u => u.Pattern == AttackPattern.Sweep) + 1) + " 枚**になる。");
    }

    static string SlotName(int s) => s switch
    {
        0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3",
        5 => "○中1", 6 => "○中3", 7 => "○前2", _ => "○後2"
    };

    /// <summary><paramref name="a"/> が <paramref name="b"/> より前に現れるか（走査が空なら偽）。</summary>
    static bool Ordered(string[] lines, string a, string b)
    {
        int ia = Array.FindIndex(lines, l => l.Contains(a));
        int ib = Array.FindIndex(lines, l => l.Contains(b));
        return ia >= 0 && ib > ia;
    }

    // =================================================================================
    // scan —— 台の下見（版を振らない）
    // =================================================================================

    static void Scan()
    {
        Console.WriteLine("# 第151期 `gust scan` —— 台の下見（V0 ＝ 現行の既定だけ）");
        Console.WriteLine();
        Console.WriteLine("**採る条件は第61・63期の帯: 第2〜5波平均が 40〜95%。** 情報セルは第2〜5波で `0 < x < 100`。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波 | 情報セル | 帯 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|:-:|");
        foreach ((string name, Formation f) in Rigs(UnitCatalog.Basa))
        {
            double[] w = Rates(f, V0);
            double avg = w.Skip(1).Average();
            Console.WriteLine("| " + name + " | " + string.Join(" | ", w.Select(x => x.ToString("F1")))
                              + " | **" + avg.ToString("F1") + "** | "
                              + w.Skip(1).Count(x => x > 0.0 && x < 100.0) + "/4 | "
                              + (avg >= 40 && avg <= 95 ? "○" : "**×**") + " |");
        }
    }

    // =================================================================================
    // 器具
    // =================================================================================

    /// <summary>編成の中のバサだけを差し替えた写しを作る（`Presets` は1行も触らない）。</summary>
    static Formation Swap(Formation f, UnitDef basa)
    {
        var defs = new UnitDef?[5];
        foreach (var o in f.Occupied())
            if (o.Slot < 5) defs[o.Slot] = o.Def.Id == UnitCatalog.Basa.Id ? basa : o.Def;
        return Formation.Build(front1: defs[0], front3: defs[1], center: defs[2], back1: defs[3], back3: defs[4]);
    }

    static double[] Rates(Formation f, ShufflerRule? r, int seeds = Seeds)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, shuffler: r).PlayerWon)
                    wins++;
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    readonly record struct Bas(double Attacks, double Damage, double Kills, double Last);

    /// <summary>バサ自身の帳簿（第2〜5波の平均）。</summary>
    static Bas BasaOf(Formation f, ShufflerRule r, string id, int seeds = Seeds)
    {
        double atk = 0, dmg = 0, kill = 0, last = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < seeds; seed++)
            {
                BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                    verbose: false, shuffler: r);
                if (!res.TallyByUnit.TryGetValue(id, out UnitTally? t)) continue;
                atk += t.Attacks; dmg += t.DamageToEnemy; kill += t.Kills; last += t.LastActiveTurn;
            }
        double d = 4.0 * seeds;
        return new Bas(atk / d, dmg / d, kill / d, last / d);
    }





    /// <summary>リポジトリ内のファイルを探す。<b>引けなかったら呼び出し側で止めること</b>（第117期）。</summary>
    static string? FindSource(string dirName, string leaf)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            string p = Path.Combine(dir.FullName, dirName, leaf);
            if (File.Exists(p)) return p;
        }
        return null;
    }
}
