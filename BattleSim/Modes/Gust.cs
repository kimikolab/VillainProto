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
//     dotnet run --project BattleSim -c Release 0 gust run      # 段A: 薙ぎ化（攻撃力の掃引）
//     dotnet run --project BattleSim -c Release 0 gust gale     # 段B/C: 転倒の確率と巻き込み
//     dotnet run --project BattleSim -c Release 0 gust compare  # 拒否権（compare 61行 ＋ 交差帯12行）
//     dotnet run --project BattleSim -c Release 0 gust check    # 自己検査
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
            case "scan": if (arg.StartsWith("probe")) ScanProbe(); else Scan(); return;
            case "run": StageA(arg); return;
            case "gale": StageBC(arg); return;
            case "compare": CompareRows(arg); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("gust: モードは phase0 / scan / run / gale / compare / check。");
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

    static UnitDef Fill(string id) => Plain(id, FillHp, FillAtk);

    /// <summary>埋め草の既定の強さ（第148期から。他の4台はこのまま）。</summary>
    const int FillHp = 110, FillAtk = 34;

    /// <summary><b>第152期 段0 で組み直した `被弾変換が厚い` の埋め草。</b>
    /// 第151期に V0 = 93.8%（余地 6.2pt）だったのがバサの採用で <b>98.2%（余地 1.8pt）</b>まで上がり、
    /// 「4台とも 40〜95% に収まる」が「勝率をほとんど上げないこと」と同義になっていた
    /// ——<b>埋め草だけを弱めて帯へ戻す</b>（肩代わり3枚の顔ぶれは1枚も触らない）。
    /// 値は <c>gust scan probe</c> の掃引から採った（<b>帯の中で情報セルが最多の点</b>）。
    /// <b>この台の第151期の数字はもう再現しない</b>——埋め草が変わったので、
    /// 過去の期と比べるときは 110/34 に戻すこと（他の4台は1枚も触っていない）。</summary>
    static UnitDef FillThick(string id) => Plain(id, ThickFillHp, ThickFillAtk);
    static int ThickFillHp = 70, ThickFillAtk = 16;

    static (string Name, Formation F)[] Rigs(UnitDef basa) => new (string, Formation)[]
    {
        ("被弾変換が厚い", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Mudo, center: UnitCatalog.Doha,
            back1: basa, back3: FillThick("pa"))),

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

    /// <summary>
    /// <b>段0（第152期）。</b> 埋め草の強さだけを掃引して、台を帯（40〜95%）へ戻す点を探す。
    /// <b>顔ぶれは1枚も動かさない</b>——動かすと台の名前（「被弾変換が厚い」）が意味を失う。
    /// </summary>
    static void ScanProbe()
    {
        (int Hp, int Atk)[] pts =
        {
            (110, 34), (100, 28), (90, 24), (80, 20), (70, 16), (60, 12), (56, 10), (50, 8), (45, 6),
        };

        Console.WriteLine("# 第152期 段0 `gust scan probe` —— 埋め草の強さを掃引する");
        Console.WriteLine();
        Console.WriteLine("**採る条件は第61・63期の帯: 第2〜5波平均が 40〜95%、かつ情報セル 3 以上**"
                          + "（Q0-6）。掃引するのは**埋め草1枚（後3）だけ**で、");
        Console.WriteLine("台の顔ぶれ（前1 ガルド / 前3 ムド / 中央 ドハ）は1枚も動かさない。");
        Console.WriteLine();
        Console.WriteLine("| 埋め草 HP/攻 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波 | 情報セル | 帯 | 採否 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|:-:|:-:|");

        int savedHp = ThickFillHp, savedAtk = ThickFillAtk;
        (int Hp, int Atk, double Avg, int Info)? best = null;
        foreach ((int hp, int atk) in pts)
        {
            ThickFillHp = hp; ThickFillAtk = atk;
            Formation f = Rigs(UnitCatalog.Basa)[0].F;
            double[] w = Rates(f, V0);
            double avg = w.Skip(1).Average();
            int info = w.Skip(1).Count(x => x > 0.0 && x < 100.0);
            bool ok = avg >= 40 && avg <= 95 && info >= 3;
            // 帯の中で**いちばん情報セルが多い**点を採り、同数なら帯の中央（67.5%）に近いほうを採る。
            if (ok && (best is null || info > best.Value.Info
                       || (info == best.Value.Info
                           && Math.Abs(avg - 67.5) < Math.Abs(best.Value.Avg - 67.5))))
                best = (hp, atk, avg, info);
            Console.WriteLine("| " + hp + " / " + atk + " | "
                              + string.Join(" | ", w.Select(x => x.ToString("F1")))
                              + " | **" + avg.ToString("F1") + "** | " + info + "/4 | "
                              + (avg >= 40 && avg <= 95 ? "○" : "**×**") + " | "
                              + (ok ? "○" : "—") + " |");
        }
        ThickFillHp = savedHp; ThickFillAtk = savedAtk;

        Console.WriteLine();
        Console.WriteLine(best is null
            ? "**帯に入る点が1つも無い。掃引の範囲を広げること**（第117期「走査が空なら止める」）。"
            : "**採った点: 埋め草 HP" + best.Value.Hp + " / 攻" + best.Value.Atk
              + "**（第2〜5波 " + best.Value.Avg.ToString("F1") + "% ・情報セル "
              + best.Value.Info + "/4）——帯の中で情報セルが最多の点。");
        Console.WriteLine();
        Console.WriteLine("**現行の設定は 埋め草 HP" + savedHp + " / 攻" + savedAtk + "。**");
    }

    // =================================================================================
    // run —— 段A: 薙ぎ化（攻撃力の掃引）
    // =================================================================================

    static void StageA(string arg)
    {
        int[] atks = arg.Length > 0
            ? arg.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()
            : new[] { 2, 3, 4, 5 };

        Console.WriteLine("# 第151期 `gust run` —— 段A: 薙ぎ化（転倒なし）");
        Console.WriteLine();
        Console.WriteLine("**V0 ＝ 現行**（攻" + UnitCatalog.Basa.Attack + "・単体）。"
                          + "**掃引は 攻" + string.Join(" / 攻", atks) + " × 薙ぎ。**");
        Console.WriteLine("`GustPercent = 0` なので転倒は1件も立たない（段A は型と攻撃力だけを振る）。");
        Console.WriteLine();

        var rigs0 = Rigs(UnitCatalog.Basa);

        Console.WriteLine("## 表A. 帰属（第2〜5波平均・Δ と**余地に対する取り分**を併記）—— 規約 Q0-1");
        Console.WriteLine();
        Console.WriteLine("`取り分` ＝ Δ ÷ (上がったなら 100 − V0 ／ 下がったなら V0)。");
        Console.WriteLine();
        Console.Write("| 台 | V0 攻7単体 |");
        foreach (int a in atks) Console.Write(" 攻" + a + "薙ぎ | 取り分 |");
        Console.WriteLine();
        Console.Write("|---|--:|");
        foreach (int _ in atks) Console.Write("--:|--:|");
        Console.WriteLine();
        foreach ((string name, Formation f) in rigs0)
        {
            double v0 = Rates(f, V0).Skip(1).Average();
            Console.Write("| " + name + " | " + v0.ToString("F1") + " |");
            foreach (int a in atks)
            {
                double v = Rates(Swap(f, Basa(a, AttackPattern.Sweep)), V0).Skip(1).Average();
                double room = v >= v0 ? 100.0 - v0 : v0;
                Console.Write(" " + v.ToString("F1") + " (" + (v - v0).ToString("+0.0;-0.0") + ") | "
                              + (room <= 0.001 ? "—" : ((v - v0) / room).ToString("+0.000;-0.000")) + " |");
            }
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("## 表B. バサの出力（第2〜5波平均・回/戦・量/戦）");
        Console.WriteLine();
        Console.WriteLine("**与ダメは総量。第127期の「段の価値は打点では測れない」があるので勝率と並べて読む。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 振り | 与ダメ | 1振りあたり | 撃破 | 生存T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in rigs0)
        {
            if (name == "動かす機構なし") continue;
            foreach ((string lab, UnitDef d) in
                     new[] { ("V0 攻7単体", UnitCatalog.Basa) }
                     .Concat(atks.Select(a => ("攻" + a + "薙ぎ", Basa(a, AttackPattern.Sweep)))))
            {
                Bas b = BasaOf(Swap(f, d), V0, UnitCatalog.Basa.Id);
                Console.WriteLine("| " + name + " | " + lab + " | " + b.Attacks.ToString("F2") + " | "
                                  + b.Damage.ToString("F1") + " | "
                                  + (b.Attacks <= 1e-9 ? "—" : (b.Damage / b.Attacks).ToString("F2")) + " | "
                                  + b.Kills.ToString("F2") + " | " + b.Last.ToString("F2") + " |");
            }
        }

        Console.WriteLine();
        Console.WriteLine("## 表C. 陣営全体（第2〜5波平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 味方の与ダメ | 決着T |");
        Console.WriteLine("|---|---|--:|--:|");
        foreach ((string name, Formation f) in rigs0)
        {
            if (name == "動かす機構なし") continue;
            foreach ((string lab, UnitDef d) in
                     new[] { ("V0 攻7単体", UnitCatalog.Basa) }
                     .Concat(atks.Select(a => ("攻" + a + "薙ぎ", Basa(a, AttackPattern.Sweep)))))
            {
                (double dmg, double turns) = TeamOf(Swap(f, d), V0);
                Console.WriteLine("| " + name + " | " + lab + " | " + dmg.ToString("F1") + " | "
                                  + turns.ToString("F2") + " |");
            }
        }
    }

    // =================================================================================
    // gale —— 段B / 段C: 転倒の確率と巻き込み
    // =================================================================================

    static void StageBC(string arg)
    {
        string[] a = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int atk = a.Length > 0 ? int.Parse(a[0]) : 4;
        int[] pcts = a.Length > 1
            ? a[1].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()
            : new[] { 10, 20, 30 };

        Console.WriteLine("# 第151期 `gust gale` —— 段B（確率の掃引）と 段C（巻き込みを外す）");
        Console.WriteLine();
        Console.WriteLine("**段A で確定した攻撃力 ＝ 攻" + atk + "・薙ぎ**。掃引は `GustPercent` = "
                          + string.Join(" / ", pcts) + "%。");
        Console.WriteLine("**段C は最良の確率で `GustSecondary = false`**（主目標だけ転ばせる）。");
        Console.WriteLine();

        UnitDef gust = Basa(atk, AttackPattern.Sweep);
        var rigs0 = Rigs(UnitCatalog.Basa);

        var versions = new List<(string Lab, ShufflerRule R)>();
        versions.Add(("段A 転倒なし", V0 with { GustPercent = 0 }));
        foreach (int p in pcts) versions.Add(("段B " + p + "%", V0 with { GustPercent = p }));
        versions.Add(("段C " + pcts[^1] + "% 主のみ",
                      V0 with { GustPercent = pcts[^1], GustSecondary = false }));
        // **参考の対照**（採用候補ではない）。敵陣を乱さない ＝ 喧噪が後列を前へ出さない版。
        // 表G の `後列` が喧噪の供給なのか、「前列が全滅して `pool` が後列へ落ちた」だけなのかを割る。
        // **混乱も同時に消える**ので勝率は比べない——見るのは**転倒の相手の列の分布だけ**。
        versions.Add(("参考 " + pcts[^1] + "% 敵を乱さない",
                      V0 with { Foes = false, GustPercent = pcts[^1] }));

        Console.WriteLine("## 表D. 帰属（第2〜5波平均・**Δ と取り分を併記**）");
        Console.WriteLine();
        Console.Write("| 台 | V0 攻7単体 |");
        foreach (var v in versions) Console.Write(" " + v.Lab + " | 取り分 |");
        Console.WriteLine();
        Console.Write("|---|--:|");
        foreach (var _ in versions) Console.Write("--:|--:|");
        Console.WriteLine();
        foreach ((string name, Formation f) in rigs0)
        {
            double v0 = Rates(f, V0).Skip(1).Average();
            Console.Write("| " + name + " | " + v0.ToString("F1") + " |");
            foreach (var v in versions)
            {
                double x = Rates(Swap(f, gust), v.R).Skip(1).Average();
                double room = x >= v0 ? 100.0 - v0 : v0;
                Console.Write(" " + x.ToString("F1") + " (" + (x - v0).ToString("+0.0;-0.0") + ") | "
                              + (room <= 0.001 ? "—" : ((x - v0) / room).ToString("+0.000;-0.000")) + " |");
            }
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine("## 表E. 波ごと（帯 ＝ 第2〜5波を分けて出す）—— 予測3");
        Console.WriteLine();
        Console.WriteLine("**生の pt ではなく取り分で並べる**（第144期・規約 Q0-1）。");
        Console.WriteLine();
        Console.Write("| 台 | 波 | V0 |");
        foreach (var v in versions) Console.Write(" " + v.Lab + " | 取り分 |");
        Console.WriteLine();
        Console.Write("|---|---|--:|");
        foreach (var _ in versions) Console.Write("--:|--:|");
        Console.WriteLine();
        foreach ((string name, Formation f) in rigs0)
        {
            if (name == "動かす機構なし") continue;
            double[] w0 = Rates(f, V0);
            var wv = versions.Select(v => Rates(Swap(f, gust), v.R)).ToList();
            for (int st = 1; st < 5; st++)
            {
                Console.Write("| " + name + " | 第" + (st + 1) + "波 | " + w0[st].ToString("F1") + " |");
                for (int i = 0; i < versions.Count; i++)
                {
                    double x = wv[i][st], room = x >= w0[st] ? 100.0 - w0[st] : w0[st];
                    Console.Write(" " + x.ToString("F1") + " (" + (x - w0[st]).ToString("+0.0;-0.0") + ") | "
                                  + (room <= 0.001 ? "—" : ((x - w0[st]) / room).ToString("+0.000;-0.000")) + " |");
                }
                Console.WriteLine();
            }
        }

        Console.WriteLine();
        Console.WriteLine("## 表F. 機構の帳簿（第2〜5波平均・回/戦）—— 受け入れ条件 §4-2");
        Console.WriteLine();
        Console.WriteLine("`当たった` は薙ぎが届いた延べ体数（主 ＋ 巻き込み）、"
                          + "`転倒` は実際に `StatusKeys.Stagger` を立てた回数。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 振り | 当たった | うち主 | うち巻き込み | 1振りの体数 | **転倒** | 転倒/当たった | 味方の転倒 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in rigs0)
        {
            if (name == "動かす機構なし") continue;
            foreach (var v in versions)
            {
                Gl g = Gale(Swap(f, gust), v.R);
                Console.WriteLine("| " + name + " | " + v.Lab + " | " + g.Swings.ToString("F2") + " | "
                                  + (g.Primary + g.Splash).ToString("F2") + " | " + g.Primary.ToString("F2")
                                  + " | " + g.Splash.ToString("F2") + " | "
                                  + (g.Swings <= 1e-9 ? "—" : ((g.Primary + g.Splash) / g.Swings).ToString("F2"))
                                  + " | **" + g.Fell.ToString("F2") + "** | "
                                  + (g.Primary + g.Splash <= 1e-9 ? "—"
                                     : (g.Fell / (g.Primary + g.Splash)).ToString("F3"))
                                  + " | " + g.AllyFell.ToString("F2") + " |");
            }
        }

        Console.WriteLine();
        Console.WriteLine("## 表G. 誰を転ばせたか（第2〜5波の合計・体）—— 予測3 の直接の証拠");
        Console.WriteLine();
        Console.WriteLine("**前列の壁を転ばせているのか、引きずり出した後列の駒を転ばせているのか。**");
        Console.WriteLine();
        Console.WriteLine("**列は「その波の開幕の席」で数える**——喧噪で前へ引きずり出された駒も"
                          + "`後列` のまま数えるので、`後列` の件数が**そのまま予測3 の証拠**になる。");
        Console.WriteLine("（薙ぎは `SweepTargets` で広がるので、**後列へは構造的に届かない**"
                          + "——届いたなら喧噪が前へ出したか、前列と中央が全滅して `pool` が後列に落ちたかのどちらか。）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 前列 | 中央 | **後列** | 後列の割合 | 相手（多い順） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|---|");
        foreach ((string name, Formation f) in rigs0)
        {
            if (name == "動かす機構なし") continue;
            foreach (var v in versions)
            {
                if (v.R.GustPercent == 0) continue;
                (Dictionary<string, int> who, double[] row) = Victims(Swap(f, gust), v.R);
                double tot = row.Sum();
                Console.WriteLine("| " + name + " | " + v.Lab + " | "
                                  + (row[0] / 4.0 / Seeds).ToString("F2") + " | "
                                  + (row[1] / 4.0 / Seeds).ToString("F2") + " | **"
                                  + (row[2] / 4.0 / Seeds).ToString("F2") + "** | "
                                  + (tot <= 0 ? "—" : (row[2] / tot).ToString("P1")) + " | "
                                  + (who.Count == 0 ? "—"
                                     : string.Join(" ／ ", who.OrderByDescending(k => k.Value).Take(4)
                                         .Select(k => k.Key + " " + (k.Value / 4.0 / Seeds).ToString("F2")))) + " |");
            }
        }
    }

    // =================================================================================
    // compare —— 拒否権（`compare` 61 行 ＋ 交差帯 12 行）
    // =================================================================================

    static void CompareRows(string arg)
    {
        string[] a = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int atk = a.Length > 0 ? int.Parse(a[0]) : 4;
        int pct = a.Length > 1 ? int.Parse(a[1]) : 20;
        bool sec = a.Length <= 2 || a[2] != "primary";

        Console.WriteLine("# 第151期 `gust compare` —— 拒否権");
        Console.WriteLine();
        Console.WriteLine("**V0 ＝ 現行の盤面そのもの。V1 ＝ 攻" + atk + "・薙ぎ ＋ `GustPercent = " + pct
                          + "` / `GustSecondary = " + sec + "`**");
        Console.WriteLine("（`Presets` のバサを差し替えられないので、**編成ごとにバサだけを入れ替えた写し**を組む）。");
        Console.WriteLine();

        UnitDef gust = Basa(atk, AttackPattern.Sweep);
        ShufflerRule V1 = V0 with { GustPercent = pct, GustSecondary = sec };

        var rows = new List<(string Name, double[] A, double[] B)>();
        foreach ((string name, Formation f) in CompareBuilds())
            rows.Add((name, Rates(f, V0), Rates(Swap(f, gust), V1)));

        int moved = rows.Count(r => Enumerable.Range(0, 5).Any(i => Math.Abs(r.A[i] - r.B[i]) > 0.001));
        Console.WriteLine("## 表H. 特異性（バサを含まない行が ±0.0 か）");
        Console.WriteLine();
        Console.WriteLine("- 動いた行: **" + moved + " / " + rows.Count + "**");
        Console.WriteLine("- バサを含む行: **"
                          + CompareBuilds().Count(b => b.F.Occupied().Any(o => o.Def.Id == UnitCatalog.Basa.Id))
                          + " / " + rows.Count + "**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | バサ在席 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|:-:|");
        foreach ((string name, double[] x, double[] y) in rows)
            if (Enumerable.Range(0, 5).Any(i => Math.Abs(x[i] - y[i]) > 0.001))
                Console.WriteLine("| " + name + " | " + string.Join(" | ", Enumerable.Range(0, 5)
                    .Select(i => (y[i] - x[i]).ToString("+0.0;-0.0;0.0"))) + " | "
                    + (CompareBuilds().First(b => b.Name == name).F.Occupied()
                        .Any(o => o.Def.Id == UnitCatalog.Basa.Id) ? "○" : "**×**") + " |");
        Console.WriteLine();

        int cross = 0, crossRows = 0;
        var crossNames = new List<string>();
        foreach ((string cname, Formation f) in CrossBuilds())
        {
            double[] x = Rates(f, V0), y = Rates(Swap(f, gust), V1);
            int n = Enumerable.Range(0, 5).Count(i => Math.Abs(x[i] - y[i]) > 0.001);
            cross += n;
            if (n > 0) { crossRows++; crossNames.Add(cname + "（" + n + " セル）"); }
        }
        Console.WriteLine("- 交差帯 12 行 / 60 セルで動いたセル: **" + cross + "**（" + crossRows + " 行）");
        Console.WriteLine("  - " + (crossNames.Count == 0 ? "—" : string.Join(" ／ ", crossNames)));
        Console.WriteLine();

        var pri = rows.Where(r => Baseline.PrimaryRows.Contains(r.Name)).ToList();
        double pa5 = pri.Average(r => r.A[4]), pb5 = pri.Average(r => r.B[4]);
        Console.WriteLine("## 表I. 拒否権1（主判定19行の第五波平均）");
        Console.WriteLine();
        Console.WriteLine("| | V0 | V1 | Δ |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int st = 0; st < 5; st++)
            Console.WriteLine("| 全61行・第" + (st + 1) + "波 | " + rows.Average(r => r.A[st]).ToString("F1")
                              + " | " + rows.Average(r => r.B[st]).ToString("F1") + " | "
                              + (rows.Average(r => r.B[st]) - rows.Average(r => r.A[st])).ToString("+0.0;-0.0") + " |");
        Console.WriteLine("| **主判定" + pri.Count + "行・第五波** | **" + pa5.ToString("F1") + "** | **"
                          + pb5.ToString("F1") + "** | **" + (pb5 - pa5).ToString("+0.0;-0.0") + "** |");
        Console.WriteLine();
        Console.WriteLine("**拒否権1（歯止め " + Baseline.PrimaryFifthFloor.ToString("F1") + "%）: "
                          + (pb5 < Baseline.PrimaryFifthFloor ? "発動" : "通る") + "**");
        Console.WriteLine();

        Console.WriteLine("## 表J. 拒否権3（いずれかの波で −10.0pt 以上）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | V0 | V1 | Δ | 主判定 |");
        Console.WriteLine("|---|---|--:|--:|--:|:-:|");
        int veto = 0;
        foreach ((string name, double[] x, double[] y) in rows)
            for (int st = 0; st < 5; st++)
                if (y[st] - x[st] <= -10.0)
                {
                    veto++;
                    Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | " + x[st].ToString("F1") + " | "
                                      + y[st].ToString("F1") + " | " + (y[st] - x[st]).ToString("+0.0;-0.0")
                                      + " | " + (Baseline.PrimaryRows.Contains(name) ? "**○**" : "—") + " |");
                }
        Console.WriteLine();
        Console.WriteLine("- **" + veto + " セル**が −10.0pt 以上");
        Console.WriteLine();

        Console.WriteLine("## 表K. 情報セル（第2〜5波で `0 < x < 100`）");
        Console.WriteLine();
        Console.WriteLine("| | V0 | V1 |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine("| 全61行 | " + rows.Sum(r => r.A.Skip(1).Count(x => x > 0 && x < 100))
                          + " | " + rows.Sum(r => r.B.Skip(1).Count(x => x > 0 && x < 100)) + " |");
        Console.WriteLine("| 主判定" + pri.Count + "行 | " + pri.Sum(r => r.A.Skip(1).Count(x => x > 0 && x < 100))
                          + " | " + pri.Sum(r => r.B.Skip(1).Count(x => x > 0 && x < 100)) + " |");
        Console.WriteLine("| 第五波 95% 超の行 | " + rows.Count(r => r.A[4] > 95)
                          + " | " + rows.Count(r => r.B[4] > 95) + " |");
    }

    // =================================================================================
    // check —— 自己検査
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第151期 自己検査 —— `gust check`");
        Console.WriteLine();

        string bal = arg.Length > 0 ? arg : "docs/balance.md";
        Console.WriteLine("## (a) 必須1: `compare` 305 セルが `" + bal + "` と 0 件");
        Console.WriteLine();
        if (!File.Exists(bal)) Console.WriteLine("**比較先が見つからない。手で `compare` を回して突き合わせること。**");
        else
        {
            var want = new Dictionary<string, double[]>();
            foreach (string line in File.ReadAllLines(bal))
            {
                if (!line.StartsWith("| ") || !line.Contains('%')) continue;
                string[] c = line.Split('|', StringSplitOptions.TrimEntries);
                if (c.Length < 7) continue;
                var v = new List<double>();
                for (int i = 2; i <= 6; i++)
                    if (double.TryParse(c[i].TrimEnd('%'), out double d)) v.Add(d);
                if (v.Count == 5) want[c[1]] = v.ToArray();
            }
            int diff = 0, cells = 0;
            foreach ((string name, Formation f) in CompareBuilds())
            {
                if (!want.TryGetValue(name, out double[]? w)) { Console.WriteLine("- 行が引けない: " + name); continue; }
                double[] got = Rates(f, null);
                for (int i = 0; i < 5; i++) { cells++; if (Math.Abs(got[i] - w[i]) > 0.001) diff++; }
            }
            Console.WriteLine("- " + cells + " セル中 **" + diff + " 件**" + (diff == 0 ? " ○" : " **×**"));
        }

        Console.WriteLine();
        Console.WriteLine("## (b) **採用で既定が動いたので、検算の相手が V0 から V1 へ移った**（第60期）");
        Console.WriteLine();
        Console.WriteLine("**段0（枝を足しただけ）のときは `GustPercent = 0` が既定と 305 セル 0 件だった。**");
        Console.WriteLine("採用後の既定は `GustPercent = 20` なので、**いま 0 件になるのは既定そのもの**で、");
        Console.WriteLine("`GustPercent = 0` との差は**採用したノブが盤面を動かした量**である（0 件だと逆に異常）。");
        Console.WriteLine();
        Console.WriteLine("- `ShufflerRule.Default` = `" + ShufflerRule.Default + "`");
        Console.WriteLine("- この診断の V0 = `" + V0 + "`");
        {
            int diff = 0;
            foreach ((string _, Formation f) in CompareBuilds())
            {
                double[] x = Rates(f, null), y = Rates(f, V0 with { GustPercent = 0 });
                for (int i = 0; i < 5; i++) if (Math.Abs(x[i] - y[i]) > 0.001) diff++;
            }
            Console.WriteLine("- `GustPercent = 0` との差: **" + diff + " 件**"
                              + (diff > 0 ? "（＝採用したノブが動かした量。**0 件だと異常**）" : " **×**"));
        }
        Console.WriteLine();
        Console.WriteLine("**`GustSecondary` は単独では盤面を動かさない**（`GustPercent = 0` のとき"
                          + "`GustSecondary` を落としても上と同じ件数）:");
        {
            int diff = 0;
            foreach ((string _, Formation f) in CompareBuilds())
            {
                double[] x = Rates(f, null), y = Rates(f, V0 with { GustPercent = 0, GustSecondary = false });
                for (int i = 0; i < 5; i++) if (Math.Abs(x[i] - y[i]) > 0.001) diff++;
            }
            Console.WriteLine("- 305 セル: **" + diff + " 件**（上と同数なら ○）");
        }
        {
            int diff = 0;
            foreach ((string _, Formation f) in CompareBuilds())
            {
                double[] x = Rates(f, V0), y = Rates(f, ShufflerRule.Default);
                for (int i = 0; i < 5; i++) if (Math.Abs(x[i] - y[i]) > 0.001) diff++;
            }
            Console.WriteLine("- **この診断の V0 と engine の既定**: **" + diff + " 件**"
                              + (diff == 0 ? " ○" : " **×**"));
        }

        Console.WriteLine();
        Console.WriteLine("## (c) §4-2: `転倒 ≦ 当たった延べ体数` ／ **味方側の転倒が 0**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 振り | 当たった | 転倒 | 味方の転倒 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|:-:|");
        UnitDef gust = Basa(4, AttackPattern.Sweep);
        ShufflerRule full = V0 with { GustPercent = 100 };
        bool ok = true;
        foreach ((string name, Formation f) in Rigs(UnitCatalog.Basa))
        {
            if (name == "動かす機構なし") continue;
            for (int st = 1; st < 5; st++)
            {
                Gl g = Gale(Swap(f, gust), full, st, st + 1, 50);
                bool o = g.Fell <= g.Primary + g.Splash + 1e-9 && g.AllyFell <= 1e-9;
                if (!o) ok = false;
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | " + g.Swings.ToString("F2") + " | "
                                  + (g.Primary + g.Splash).ToString("F2") + " | " + g.Fell.ToString("F2")
                                  + " | " + g.AllyFell.ToString("F2") + " | " + (o ? "○" : "**×**") + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("- 全 16 セル" + (ok ? " ○" : " **×**"));
        Console.WriteLine();
        Console.WriteLine("**`GustPercent = 100` では `転倒 ＝ 当たった` になるとは限らない**"
                          + "——既に転んでいる相手（二値）には立て直さないし、");
        Console.WriteLine("巻き込みで倒れた相手は `IsAlive` で落ちる。");

        Console.WriteLine();
        Console.WriteLine("## (d) 段C（`GustSecondary = false`）が主目標だけを転ばせる");
        Console.WriteLine();
        Console.WriteLine("| 台 | 当たった（主 / 巻き込み） | 転倒 | 転倒 ≦ 主 |");
        Console.WriteLine("|---|---|--:|:-:|");
        bool okC = true;
        foreach ((string name, Formation f) in Rigs(UnitCatalog.Basa))
        {
            if (name == "動かす機構なし") continue;
            Gl g = Gale(Swap(f, gust), V0 with { GustPercent = 100, GustSecondary = false });
            bool o = g.Fell <= g.Primary + 1e-9;
            if (!o) okC = false;
            Console.WriteLine("| " + name + " | " + g.Primary.ToString("F2") + " / " + g.Splash.ToString("F2")
                              + " | " + g.Fell.ToString("F2") + " | " + (o ? "○" : "**×**") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- " + (okC ? "○" : "**×**"));

        Console.WriteLine();
        Console.WriteLine("## (e) 必須4: `ctx.PickOne` を新たに使っていない");
        Console.WriteLine();
        string? tr = FindSource("BattleCore", "Traits.cs");
        string? eng = FindSource("BattleCore", "BattleEngine.cs");
        if (tr is null || eng is null) Console.WriteLine("**ソースが引けない。止める**（第117期）。");
        else
        {
            Console.WriteLine("- `BattleEngine.cs` の `PickOne(` は **"
                              + File.ReadAllLines(eng).Count(l => l.Contains("PickOne(")) + "** 行");
            Console.WriteLine("- `Traits.cs` の `PickOne(` は **"
                              + File.ReadAllLines(tr).Count(l => l.Contains("PickOne(")) + "** 行");
            Console.WriteLine("- 突風が引く乱数は `GustPercent < 100` のときの `Roll(100)` だけ: **"
                              + (File.ReadAllLines(tr).Any(l => l.Contains("ctx.Shuffler.GustPercent < 100"))
                                 ? "○" : "**×**（未実装なら段A の前）") + "**");
        }

        Console.WriteLine();
        Console.WriteLine("## (f) 必須3: `docs/rules.md` の既定値の列");
        Console.WriteLine();
        Console.WriteLine("`ShufflerRule` の既定 = `" + ShufflerRule.Default + "`");
        Console.WriteLine();
        Console.WriteLine("**`record struct` に計算プロパティを足していないこと**（第148期）——"
                          + "自動生成の `ToString` に載って既定値の列が動く。");
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

    /// <summary>味方全体の与ダメと決着ターン（第2〜5波の平均）。</summary>
    static (double Damage, double Turns) TeamOf(Formation f, ShufflerRule r)
    {
        var own = new HashSet<string>(f.Occupied().Select(o => o.Def.Id));
        double dmg = 0, turns = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                    verbose: false, shuffler: r);
                foreach (var kv in res.TallyByUnit)
                    if (own.Contains(kv.Key)) dmg += kv.Value.DamageToEnemy;
                turns += res.Turns;
            }
        double d = 4.0 * Seeds;
        return (dmg / d, turns / d);
    }

    readonly record struct Gl(double Swings, double Primary, double Splash, double Fell, double AllyFell);

    /// <summary>突風の帳簿（既定は第2〜5波の平均・回/戦）。</summary>
    static Gl Gale(Formation f, ShufflerRule r, int from = 1, int to = 5, int seeds = Seeds)
    {
        var own = new HashSet<string>(f.Occupied().Select(o => o.Def.Id));
        double sw = 0, pri = 0, spl = 0, fell = 0, ally = 0;
        int n = 0;
        for (int st = from; st < to; st++)
        {
            n++;
            for (int seed = 0; seed < seeds; seed++)
            {
                BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                    verbose: false, shuffler: r);
                foreach (var kv in res.TallyByUnit)
                {
                    UnitTally t = kv.Value;
                    sw += t.GustSwings; pri += t.GustPrimary; spl += t.GustSplash; fell += t.GustFell;
                    if (own.Contains(kv.Key)) ally += t.GustFellHere;
                }
            }
        }
        double d = Math.Max(1, n) * (double)seeds;
        return new Gl(sw / d, pri / d, spl / d, fell / d, ally / d);
    }

    /// <summary>
    /// 転倒した相手の名前ごとの延べ件数と、<b>その波の開幕の列</b>ごとの内訳（前 / 中 / 後）。
    /// <b>開幕の席で数える</b>——喧噪が前へ引きずり出した駒も `後列` のままなので、
    /// 「引きずり出した後列の駒を転ばせているか」（予測3）がこの列で直接読める。
    /// </summary>
    static (Dictionary<string, int> Who, double[] Row) Victims(Formation f, ShufflerRule r)
    {
        var own = new HashSet<string>(f.Occupied().Select(o => o.Def.Id));
        var who = new Dictionary<string, int>();
        var row = new double[3];
        for (int st = 1; st < 5; st++)
        {
            // その波の開幕の席（同じ `Id` が2枠に立つ波があるので、浅いほうを採る）。
            var opening = new Dictionary<string, int>();
            foreach (var o in EnemyCatalog.Stages[st].Enemy.Occupied())
            {
                int d = FormationRules.DepthOf(FormationRules.RowOf(o.Slot));
                if (!opening.TryGetValue(o.Def.Id, out int cur) || d < cur) opening[o.Def.Id] = d;
            }
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                    verbose: false, shuffler: r);
                foreach (var kv in res.TallyByUnit)
                {
                    if (own.Contains(kv.Key) || kv.Value.GustFellHere == 0) continue;
                    who[kv.Key] = who.GetValueOrDefault(kv.Key) + kv.Value.GustFellHere;
                    if (opening.TryGetValue(kv.Key, out int d)) row[d] += kv.Value.GustFellHere;
                }
            }
        }
        return (who, row);
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
