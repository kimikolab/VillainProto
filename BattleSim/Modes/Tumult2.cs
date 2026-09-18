using BattleCore;
using static Common;

// =====================================================================================
// tumult2 モード（第148期） —— バサの手番を混乱に使う
//
// 指示書は design/PHASE148_TUMULT2_SPEC.md ／ 報告は design/PHASE148_TUMULT2.md。
//
// **問いは「手番に移して良くなるか」**であって「混乱を足して良くなるか」ではない
// ——だから **V0 は現行の既定**（`ShufflerRule.Default` ＝ ターン頭・在庫3回・通常攻撃）。
//
//     dotnet run --project BattleSim -c Release 0 tumult2 phase0   # 前提を実装から引き直す（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 tumult2 seats    # 段0: 席の棚卸し（docs/reseat.md を読む・戦闘0回）
//     dotnet run --project BattleSim -c Release 0 tumult2 scan     # 台の下見（V0 だけ）
//     dotnet run --project BattleSim -c Release 0 tumult2 run      # 段B: ターン頭 → 手番
//     dotnet run --project BattleSim -c Release 0 tumult2 compare  # 拒否権（compare 61行 ＋ 交差帯12行）
//     dotnet run --project BattleSim -c Release 0 tumult2 check    # 自己検査
//
// **版の切り替えは規則ではできない**（第60期）。手番を持つかどうかは `UnitDef.Actions` が
// 決めるので `Run` の引数では振れない——V1 は**この診断のローカルの `UnitDef`** で作る。
// =====================================================================================

static class Tumult2Diag
{
    const int Seeds = 200;

    /// <summary>対照。<b>現行の既定</b>（ターン頭・在庫3回）＋ 通常攻撃のバサ。</summary>
    static readonly ShufflerRule V0 = ShufflerRule.Default;

    /// <summary>この期の版。<b>手番で1体だけ</b>（在庫は読まない・上限は手番そのもの）。</summary>
    static readonly ShufflerRule V1 = new(true, ShuffleStagger.ConfuseOnAction);

    /// <summary>
    /// 手番版のバサ。<b>数値も特性も1つも触らず、`Actions` を <c>[Skill]</c> の1要素だけ足す。</b>
    ///
    /// <para><b>周期にしない。</b> <c>ActionIndex++</c> は <c>CanAct</c> 通過<b>後</b>なので、
    /// 永久に実行できない種別を周期に混ぜるとその要素で止まって二度と進まない
    /// （<c>TakeTurnCore</c> のコメント）。1要素なら <c>ActionIndex % 1</c> が常に 0 で、
    /// 撃てないターンがあっても周期は壊れない。</para>
    /// </summary>
    static readonly UnitDef BasaOnAction = new()
    {
        Id = UnitCatalog.Basa.Id,
        Name = UnitCatalog.Basa.Name,
        MaxHp = UnitCatalog.Basa.MaxHp,
        Attack = UnitCatalog.Basa.Attack,
        Speed = UnitCatalog.Basa.Speed,
        Traits = UnitCatalog.Basa.Traits,
        Advances = UnitCatalog.Basa.Advances,
        Actions = new[] { new UnitAction(ActionKind.Skill, Label: "敵の隊列を怒鳴りつけた") },
        PlusText = UnitCatalog.Basa.PlusText,
        MinusText = UnitCatalog.Basa.MinusText,
        Flavor = UnitCatalog.Basa.Flavor,
    };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "seats": Seats(); return;
            case "scan": Scan(); return;
            case "run": Stage(); return;
            case "compare": CompareRows(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("tumult2: モードは phase0 / seats / scan / run / compare / check。");
                return;
        }
    }

    // =================================================================================
    // 台 —— 第147期の4台 ＋ **手番市場の台**（撃てないターンが号令・据えに売れるか）
    // =================================================================================

    /// <summary>埋め草は<b>特性を1つも持たない素体</b>（第143期の則。敵の数値に触らせない）。</summary>
    static UnitDef Plain(string id, int hp, int atk, int spd = 8) => new()
    {
        Id = id, Name = "素体" + id, MaxHp = hp, Attack = atk, Speed = spd,
        Advances = true, Traits = Array.Empty<TraitId>()
    };

    static UnitDef Fill(string id) => Plain(id, 110, 34);

    /// <param name="basa">V0 は <c>UnitCatalog.Basa</c>、V1 は <see cref="BasaOnAction"/>。</param>
    static (string Name, Formation F)[] Rigs(UnitDef basa) => new (string, Formation)[]
    {
        // 被弾変換が厚い。**混乱が餌を減らす代金になるか**（予測3）。
        ("被弾変換が厚い", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Mudo, center: UnitCatalog.Doha,
            back1: basa, back3: Fill("pa"))),

        // 打点だけ。**混乱が純粋な得になるか**。
        ("被弾変換が薄い", Formation.Build(
            front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Kiri,
            back1: basa, back3: Fill("pa"))),

        // ササ ＋ ガレ。**混乱は敵→敵なのでササの供給には化けないはず**（予測6）。
        ("ササ同席", Formation.Build(
            front1: UnitCatalog.Sasa, front3: UnitCatalog.Gare, center: UnitCatalog.Borg,
            back1: basa, back3: Fill("pa"))),

        // **手番市場**（この期に足した台）。据え（バン・そのターンの被ダメ −50%）が
        // **撃てないターンの `IdleTurn` を買う**か（予測4）。`被弾変換が薄い` のキリをバンに
        // 差し替えただけ（第21期 swap の作法）。
        //
        // **号令（ガン）はこの台に入れない。** 号令の払い先は<b>攻撃力 +8</b> で、
        // `[Skill]` にしたバサは**一度も攻撃しない**ので換金できない
        // ——第36期「安い手番は売っても安い」の極端な例で、**買い手が払う通貨が
        // 売り手の使えない通貨**だと市場そのものが成立しない。
        // 据えは**被ダメ側**なので、殴られるバサには効く。
        ("手番市場（据え）", Formation.Build(
            front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Ban,
            back1: basa, back3: Fill("pa"))),

        // **号令同席**（負の相互作用の実測用）。`RallyTrait.OnTurnStart` は
        // **差し出した味方1体ずつに** `Whet(+8)` を払う（`ctx.SupportTargets(ally)` 経由）ので、
        // **バサが差し出した手番も号令の支払い対象になる**。
        // V1 のバサは攻撃しないから、そこへ流れた強化は**丸ごと死蔵**になる（表F）。
        ("号令同席", Formation.Build(
            front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Gan,
            back1: basa, back3: Fill("pa"))),

        // 陰性対照。**バサを同数値・特性なしの素体に落としただけ**（HP56 / 攻7 / 速8）。
        // 版を振っても1ビットも動かないことが特異性の検算（予測7）。
        ("動かす機構なし", Formation.Build(
            front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Kiri,
            back1: Plain("pb", 56, 7), back3: Fill("pa"))),
    };

    // =================================================================================
    // phase0 —— 前提を実装から引き直す（戦闘0回）
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第148期 `tumult2 phase0` —— 前提を実装から引き直す（戦闘0回）");
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

        Console.WriteLine("## Q0-2. `Actions` を持たせたときの周期の穴");
        Console.WriteLine();
        Console.WriteLine("| 問い | 実装 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| `ActionIndex++` は `CanAct` の前か後か | **後**（`TakeTurnCore`。"
                          + "偽なら `IdleTurn` を立てて `Stalled` で返る） |");
        Console.WriteLine("| バサの `Actions` | **`[Skill]` の1要素**（`ActionIndex % 1` は常に 0 ＝ 周期は壊れない） |");
        Console.WriteLine("| `CanAct` 偽のとき `IdleTurn` は立つか | **立つ**（`actor.SetCounter(StatusKeys.IdleTurn, Turn)`） |");
        Console.WriteLine("| 号令・据えが買えるか | `Trait.SurrenderedTurn` は **`ActionKind.Attack` で問う**ので、"
                          + "`kind != Skill` で早期 return する実装なら**買える** |");
        Console.WriteLine("| `SurrendersTurn` | **既定（真）のまま上書きしない**"
                          + "（偽にすると不動・追い打ちと同じ扱いになって市場が消える） |");
        Console.WriteLine();
        string[] body = ClassBody(T, "class ShufflerTrait");
        Console.WriteLine("- `ShufflerTrait` の本体は **" + body.Length + " 行**（走査が空なら止める・第117期）: "
                          + (body.Length > 0 ? "○" : "**×**"));
        Console.WriteLine("- `SurrendersTurn` を上書きしていないこと: **"
                          + (body.Any(l => l.Contains("override") && l.Contains("SurrendersTurn"))
                             ? "**×** 上書きしている" : "○ 上書きしていない") + "**");
        Console.WriteLine("- `CanAct` の本体が `Roll` を引かないこと（存在検査だけ）: **"
                          + (Method(body, "public override bool CanAct").Any(l => l.Contains("Roll("))
                             ? "**×** 引いている" : "○") + "**");
        Console.WriteLine("- `CanAct` が `kind != ActionKind.Skill` で早期 return すること"
                          + "（号令・据えの市場に乗る条件）: **"
                          + (Method(body, "public override bool CanAct")
                             .Any(l => l.Contains("kind != ActionKind.Skill")) ? "○" : "**×**") + "**");
        Console.WriteLine();

        Console.WriteLine("## Q0-3. ターン頭と手番の二重掛けを避ける");
        Console.WriteLine();
        Console.WriteLine("- `ShuffleStagger` の枝: **" + string.Join(" / ", Enum.GetNames(typeof(ShuffleStagger))) + "**");
        Console.WriteLine("- `ShufflerRule` の既定: `" + ShufflerRule.Default + "`");
        Console.WriteLine("- ターン頭の `Settle` に `ConfuseOnAction` の早期 return があるか: **"
                          + (T.Any(l => l.Contains("ShuffleStagger.ConfuseOnAction")
                                        && l.Contains("c.Shuffler.Stagger ==")) ? "○" : "**×**") + "**");
        Console.WriteLine("- **`ConfuseUses` は `ConfuseOnAction` では読まない**（上限は手番そのもの）: "
                          + "`Derange` の引数 `useStock` で分ける ＝ **"
                          + (T.Any(l => l.Contains("useStock")) ? "○" : "**×**") + "**");
        Console.WriteLine();

        Console.WriteLine("## Q0-4. 手番で狙う相手の選び方");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 決め |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| 候補 | **そのターン行が前に変わった敵**（印 `ShufflerTrait.AdvancedTurnKey` ＝ `ctx.Turn`） |");
        Console.WriteLine("| 既に混乱している敵 | **候補から外す**（他に候補が無ければ撃たずに `IdleTurn`） |");
        Console.WriteLine("| 複数いたら | `Roll` で1体（**`PickOne` は新たに使わない**・必須4） |");
        Console.WriteLine("| 記憶の持ち方 | **立てられる側にターン番号**——`InstanceId` を跨がないので `OnCarryOver` が要らない |");
        Console.WriteLine();
        Console.WriteLine("**`CanAct` と `OnAction` は同じ述語（`Fresh`）を見る。**");
        Console.WriteLine("これで「撃つ」か「手番を差し出す」かのどちらかしか起きず、");
        Console.WriteLine("受け入れ条件（立てた ＋ 差し出した ＝ 生存T）が成り立つ。");
        Console.WriteLine();

        Console.WriteLine("## Q0-5. 攻7 を捨てる影響");
        Console.WriteLine();
        Console.WriteLine("- バサの攻撃力: **" + UnitCatalog.Basa.Attack + "**（ロスター 52 枚中、下から "
                          + (UnitCatalog.All.Count(u => u.Attack <= UnitCatalog.Basa.Attack)) + " 番目）");
        Console.WriteLine("- `Skill` は攻撃を消費する（`TakeTurnCore` の `ActionKind.Skill` の枝）ので、"
                          + "**撃ったターンの与ダメは 0**。");
        Console.WriteLine("- 先例: 第143期のササ（攻7・`[Skill]` で身構え）は**与ダメ0にして4台すべてで上がった**。");
        Console.WriteLine();

        Console.WriteLine("## Q0-6. 測定台（各台の V0 が 40〜95%・情報セル 3 以上かは `tumult2 scan`）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 顔ぶれ | 見るもの |");
        Console.WriteLine("|---|---|---|");
        foreach ((string name, Formation f) in Rigs(UnitCatalog.Basa))
            Console.WriteLine("| " + name + " | " + string.Join(" / ", f.Occupied().Select(o => o.Def.Name)) + " | |");
        Console.WriteLine();
        Console.WriteLine("**埋め草に敵の数値へ触る駒（呪詛官ネル・萎縮のクビ）を入れていないこと**（第143期）: "
                          + (Rigs(UnitCatalog.Basa).All(r => r.F.Occupied()
                                .All(o => o.Def.Id != UnitCatalog.Nel.Id && o.Def.Id != UnitCatalog.Kubi.Id))
                             ? "**○**" : "**×**"));
        Console.WriteLine();
        Console.WriteLine("**土台の駒が『自分で動き出せるか』『その席から動けるか』**（第146期）——");
        Console.WriteLine("この期の供給は**バサの喧噪1本だけ**（毎ターン無条件・`OnTurnStart`）なので、");
        Console.WriteLine("ハネ（`OnMoved` しか持たない）・セロ（後列だと即 return）の穴には当たらない。");
        Console.WriteLine();

        Console.WriteLine("## Q0-7. 過去に「ターン頭の効果を手番へ移した」期");
        Console.WriteLine();
        Console.WriteLine("`design/*.md` と `CLAUDE.md` の走査（`OnTurnStart` と `OnAction` を同じ行で挙げている節）:");
        Console.WriteLine();
        Console.WriteLine("| 先例 | 何が起きたか |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| 第11期 Phase BB（継ぎ当て・ノノ） | 移設の作法そのもの。"
                          + "**`ActsOnPattern` が偽のときだけ従来どおり発火させる**（同じ特性を共有する敵駒が消えないため） |");
        Console.WriteLine("| 第60期（火選り・ヒヨ） | **係数が動かせない量があるときはフックの位置を疑う**"
                          + "——`Gain` を上げても動かなかった `撒いた` が、移設で4行とも下がった（−1.87〜−4.18 量/戦）。"
                          + "**第1ターンにちょうど載っていた代金が消える** |");
        Console.WriteLine("| 第60期（同上） | **手番へ降ろすと止められる経路が増えるとは限らない**。"
                          + "一方で**移設は経路を1つ閉じる**（断罪は振らなくなった駒に当たらない） |");
        Console.WriteLine("| 第61期（瘴気・グザ。**採用しなかった**） | **`OnTurnStart` は speed = ∞ の席**。"
                          + "移設すると既に手番へ降りている駒に対して**速さで順序が決まる**"
                          + "——グザ(速5)は澱み(ミオ・速8)に負けて第1ターンの毒が消え、帰属 −26.4pt |");
        Console.WriteLine();
        Console.WriteLine("**この期に当たる先例は第61期**（移設で順序が変わる）。");
        Console.WriteLine("バサは**速" + UnitCatalog.Basa.Speed + "**で、敵の速さは "
                          + string.Join(" / ", new[] { "勇者候補14", "槍騎兵12", "狙撃手11", "詠唱兵5", "城塞の重装兵3" })
                          + "。");
        Console.WriteLine("**ただし混乱は `TickStatuses` の一括消去に入っていない**"
                          + "（`StatusKeys.All` には入っているが、消えるのは `ConsumeConfusion` ＝");
        Console.WriteLine("本人が次に振ったときだけ）ので、**速い敵に立てた混乱は無駄にならず1ターン遅れる**。");
        Console.WriteLine("第61期の瘴気（増幅役が先に動かないと毒が1層も無い）とはここが違う。");
        Console.WriteLine();
        Console.WriteLine("- `ConsumeConfusion` の呼び出し口: **"
                          + E.Count(l => l.Contains("ConsumeConfusion(") && !l.Contains("private")) + "** 箇所");
        string[] all = E.Where(l => l.Contains("public static readonly string[] All")).ToArray();
        Console.WriteLine("- `StatusKeys.All` に `Confused` が入っているか（会戦の境界で消える一覧）: **"
                          + (all.Length == 1 && all[0].Contains("Confused") ? "○" : "**×**") + "**");
        Console.WriteLine("- `TickStatuses` が `Confused` を触るか（ターン頭の一括処理）: **"
                          + (Method(E, "private void TickStatuses").Any(l => l.Contains("Confused"))
                             ? "**×** 触る" : "○ 触らない") + "**");
        Console.WriteLine();
        Console.WriteLine("## Q0-8. 号令（ガン）の払い先は「動かなかった味方全員」か、対象が限られるか");
        Console.WriteLine();
        string[] rally = ClassBody(T, "class RallyTrait");
        Console.WriteLine("`RallyTrait.OnTurnStart` の本体 " + rally.Length + " 行を読む:");
        Console.WriteLine();
        Console.WriteLine("| 問い | 実装 |");
        Console.WriteLine("|---|---|");
        Console.WriteLine("| 誰に払うか | **`LivingMembers(自陣)` を1体ずつ回し、"
                          + "`IdleTurn == ctx.Turn - 1` かつ `Trait.SurrenderedTurn` が真の駒に**"
                          + "`ctx.Whet(t, " + RallyTrait.Gain + ", RallyTurn)` |");
        Console.WriteLine("| 「全員に配る」形か | **違う**——差し出した駒<b>それぞれ</b>に個別に払う"
                          + "（開戦の鬨 `+" + RallyTrait.OpeningGain + "` のほうは味方全体） |");
        Console.WriteLine("| 払う通貨 | **攻撃力**（`WhetRoute.RallyTurn`） |");
        Console.WriteLine();
        Console.WriteLine("- 毎ターンの払いに `SurrenderedTurn` の門があるか: **"
                          + (rally.Any(l => l.Contains("SurrenderedTurn(ctx, ally)")) ? "○" : "**×**") + "**");
        Console.WriteLine("- `ctx.SupportTargets(ally)` を通すか（支援拒否は隣へ漏れる）: **"
                          + (rally.Count(l => l.Contains("SupportTargets(ally)")) + "** 箇所"));
        Console.WriteLine();
        Console.WriteLine("**したがって `[Skill]` にしたバサは号令の負の相互作用を起こす。**");
        Console.WriteLine("撃てないターンに `IdleTurn` が立ち、バサは `SurrendersTurn` を上書きしていないので");
        Console.WriteLine("**号令はバサ本人に攻撃力 +" + RallyTrait.Gain + " を払う**——そしてバサは一度も攻撃しないので、");
        Console.WriteLine("その量は**丸ごと死蔵**になる（第56期「配る側は配る先が振るかを1ビットも見ていない」）。");
        Console.WriteLine("**実測は `tumult2 run` の表F。** 手番市場の台は据え（バン・被ダメ側）で組んである。");
    }

    /// <summary>
    /// クラス宣言から、その閉じ括弧（列 0 の <c>}</c>）までの本文を返す。
    /// <b>走査が空なら呼び出し側で止めること</b>（第117期）——
    /// 「引けなかった」と「該当なし」は区別が付かない。
    /// </summary>
    static string[] ClassBody(string[] lines, string decl)
    {
        int c = Array.FindIndex(lines, l => l.Contains(decl));
        if (c < 0) return Array.Empty<string>();
        var body = new List<string>();
        for (int i = c; i < lines.Length; i++)
        {
            body.Add(lines[i]);
            if (i > c && lines[i] == "}") break;
        }
        return body.ToArray();
    }

    /// <summary>本文の中の1つのメソッド（宣言行から、同じ深さの閉じ括弧まで）。</summary>
    static string[] Method(string[] body, string decl)
    {
        int s = Array.FindIndex(body, l => l.Contains(decl));
        if (s < 0) return Array.Empty<string>();
        string indent = new string(' ', body[s].Length - body[s].TrimStart().Length);
        var m = new List<string>();
        for (int i = s; i < body.Length; i++)
        {
            m.Add(body[i]);
            if (i > s && body[i] == indent + "}") break;
        }
        return m.ToArray();
    }

    // =================================================================================
    // seats —— 段0: 席の棚卸し（`docs/reseat.md` を読む・戦闘0回）
    // =================================================================================

    /// <summary>
    /// <b>線と採る条件は同じ集合で書く</b>（規約 (G16)）。ここで当てるのは3つ:
    /// (1) 狙 ○ ／ (2) 情報セルが現行以上 ／ (3) 帯A の5波平均が現行より +5.0pt 以上。
    /// <b>採否は `confirm`（帯B・seed 200..599）でもう一度同じ線を当てて決める。</b>
    /// </summary>
    static void Seats()
    {
        Console.WriteLine("# 第148期 `tumult2 seats` —— 席の棚卸し（`docs/reseat.md` を読む・戦闘0回）");
        Console.WriteLine();

        string? path = FindSource("docs", "reseat.md");
        if (path is null) { Console.WriteLine("**`docs/reseat.md` が引けない。止める**（第117期）。"); return; }

        var rows = new List<(string Name, List<Seat> Seats)>();
        List<Seat>? cur = null;
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.TrimEnd();
            if (line.StartsWith("## ")) { cur = new List<Seat>(); rows.Add((line[3..].Trim(), cur)); continue; }
            if (cur is null || !line.StartsWith("| ") || !line.Contains('%')) continue;
            string[] c = line.Trim('|').Split('|').Select(x => x.Trim()).ToArray();
            if (c.Length < 11) continue;
            if (!double.TryParse(c[5].TrimEnd('%'), out double avg)) continue;
            var w = new double[5];
            bool ok = true;
            for (int i = 0; i < 5; i++) ok &= double.TryParse(c[6 + i].TrimEnd('%'), out w[i]);
            if (!ok) continue;
            cur.Add(new Seat(c[0], c[1], c[2] + " | " + c[3] + " | " + c[4], avg, w,
                             w.Skip(1).Count(x => x > 0 && x < 100), c[0].Contains("現行")));
        }

        Console.WriteLine("**線**: (1) 狙 ○（その駒を含む行のみ） / (2) 情報セルが現行以上 / (3) 帯A +5.0pt 以上。");
        Console.WriteLine("**`情報セルが現行以上` を入れているのはこの期の目的が分解能の回復だから**"
                          + "——勝率が上がっても情報を減らす席は候補にしない。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 現行(帯A) | 現行info | 候補 | Δ | 候補info | 粗順 | 落ちた条件 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");

        int hit = 0, aimOut = 0, infoOut = 0;
        foreach ((string name, List<Seat> seats) in rows)
        {
            Seat? me = seats.FirstOrDefault(s => s.Cur);
            if (me is null) continue;
            var over = seats.Where(s => !s.Cur && s.Avg - me.Avg >= 5.0).ToList();
            if (over.Count == 0) continue;
            var pass = over.Where(s => s.Aim != "×" && s.Info >= me.Info).OrderByDescending(s => s.Avg).ToList();
            string why = pass.Count > 0 ? "—"
                : over.All(s => s.Aim == "×") ? "狙"
                : over.Where(s => s.Aim != "×").All(s => s.Info < me.Info) ? "情報セル" : "狙 ／ 情報セル";
            if (pass.Count == 0) { if (why.Contains("狙")) aimOut++; else infoOut++; }
            Seat b = pass.Count > 0 ? pass[0] : over.OrderByDescending(s => s.Avg).First();
            if (pass.Count > 0) hit++;
            Console.WriteLine("| " + name + " | " + me.Avg.ToString("F1") + " | " + me.Info + " | "
                              + b.Avg.ToString("F1") + " | " + (b.Avg - me.Avg).ToString("+0.0") + " | "
                              + b.Info + " | " + b.Rank + " | " + why + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 帯A で +5.0pt 以上の席がある行のうち、**3条件を全部通ったのは " + hit + " 行**"
                          + "（狙で落ちた " + aimOut + " ／ 情報セルで落ちた " + infoOut + "）。");
        Console.WriteLine();
        Console.WriteLine("**通った行を `confirm` に載せて帯B で測り直し、そこでも +5.0pt を超えたものだけを差し替える。**");
        Console.WriteLine("**さらに「編成の狙い」（隣接ペア・後列必須など行ごとの意図）と食い違う席は採らない**");
        Console.WriteLine("——`reseat.md` の `狙` 列はガルド前列 / セッキ後列しか見ていないので、");
        Console.WriteLine("**行ごとの狙いは `Presets.cs` のコメントを人が読むしかない**（第148期に 6/12 行がこれで落ちた）。");
    }

    sealed record Seat(string Rank, string Aim, string Where, double Avg, double[] W, int Info, bool Cur);

    // =================================================================================
    // scan —— 台の下見（版を振らない）
    // =================================================================================

    static void Scan()
    {
        Console.WriteLine("# 第148期 `tumult2 scan` —— 台の下見（V0 ＝ 現行の既定だけ）");
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
    // run —— 段B: ターン頭（在庫3回・通常攻撃） → 手番
    // =================================================================================

    static void Stage()
    {
        Console.WriteLine("# 第148期 `tumult2 run` —— 段B: 混乱をターン頭から手番へ移す");
        Console.WriteLine();
        Console.WriteLine("**V0 ＝ 現行の既定**（`" + V0 + "` ＋ 通常攻撃のバサ）。");
        Console.WriteLine("**V1 ＝ `" + V1 + "` ＋ `Actions = [Skill]` のバサ**（この診断のローカルの `UnitDef`）。");
        Console.WriteLine();

        Console.WriteLine("## 表A. 勝率（V1 − V0）");
        Console.WriteLine();
        Console.WriteLine("**波ごとの効き方は生の pt で比べない**（第144期）——`余地に対する取り分` ＝");
        Console.WriteLine("Δ ÷ (上がったなら 100 − V0 ／ 下がったなら V0)。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | V0 ターン頭 | V1 手番 | Δ | 余地に対する取り分 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        var rigs0 = Rigs(UnitCatalog.Basa);
        var rigs1 = Rigs(BasaOnAction);
        for (int r = 0; r < rigs0.Length; r++)
        {
            double[] a = Rates(rigs0[r].F, V0), b = Rates(rigs1[r].F, V1);
            for (int st = 1; st < 5; st++)
            {
                double room = b[st] >= a[st] ? 100.0 - a[st] : a[st];
                string share = room <= 0.001 ? "—" : ((b[st] - a[st]) / room).ToString("+0.000;-0.000");
                Console.WriteLine("| " + rigs0[r].Name + " | 第" + (st + 1) + "波 | " + a[st].ToString("F1")
                                  + " | " + b[st].ToString("F1") + " | " + (b[st] - a[st]).ToString("+0.0;-0.0")
                                  + " | " + share + " |");
            }
            Console.WriteLine("| **" + rigs0[r].Name + "** | **第2〜5波** | **" + a.Skip(1).Average().ToString("F1")
                              + "** | **" + b.Skip(1).Average().ToString("F1") + "** | **"
                              + (b.Skip(1).Average() - a.Skip(1).Average()).ToString("+0.0;-0.0") + "** | |");
        }

        Console.WriteLine();
        Console.WriteLine("## 表B. 供給（回/戦・第2〜5波）—— 予測1");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 前へ出た | **立てた** | 敵に立った | 敵が振った | 味方に立った |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        for (int r = 0; r < rigs0.Length; r++)
            foreach ((string lab, Formation f, ShufflerRule rule) in
                     new[] { ("V0", rigs0[r].F, V0), ("V1", rigs1[r].F, V1) })
            {
                Led l = Sum(f, rule);
                Console.WriteLine("| " + rigs0[r].Name + " | " + lab + " | " + l.Advanced.ToString("F2") + " | **"
                                  + l.Confuses.ToString("F2") + "** | " + l.FoeMarks.ToString("F2") + " | "
                                  + l.FoeSwings.ToString("F2") + " | " + l.AllyMarks.ToString("F2") + " |");
            }

        Console.WriteLine();
        Console.WriteLine("## 表C. バサの手番（V1・回/戦・第2〜5波）—— 受け入れ条件 §4-2 と予測4");
        Console.WriteLine();
        Console.WriteLine("`手番` は回ってきた回数、`撃った` は `Skill` を実行した回数、");
        Console.WriteLine("`潰れた` は撃てなかった回数、`売れた` はそのうち号令・据えが買える回数"
                          + "（`Trait.SurrenderedTurn`）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 手番 | 撃った | 潰れた | **売れた** | 与ダメ | 最終活動T | 立てた ＝ 撃った |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|:-:|");
        for (int r = 0; r < rigs0.Length; r++)
            foreach ((string lab, Formation f, ShufflerRule rule) in
                     new[] { ("V0", rigs0[r].F, V0), ("V1", rigs1[r].F, V1) })
            {
                Bas b = BasaOf(f, rule);
                Console.WriteLine("| " + rigs0[r].Name + " | " + lab + " | " + b.Turns.ToString("F2") + " | "
                                  + b.Skills.ToString("F2") + " | " + b.Stalls.ToString("F2") + " | **"
                                  + b.Sold.ToString("F2") + "** | " + b.Damage.ToString("F1") + " | "
                                  + b.Last.ToString("F2") + " | "
                                  + (Math.Abs(b.Skills - b.Confuses) < 0.005 ? "○" : "**×** " + b.Confuses.ToString("F2"))
                                  + " |");
            }

        Console.WriteLine();
        Console.WriteLine("## 表F. 号令はバサに払われて死蔵するか（量/戦・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("`RallyTrait.OnTurnStart` は**差し出した味方1体ずつ**に `Whet(+8)` を払う"
                          + "（`ctx.SupportTargets(ally)` 経由）ので、**バサの空き手番も支払い対象**になる。");
        Console.WriteLine("V1 のバサは攻撃しないので、そこへ流れた量は**丸ごと死蔵**である。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 号令の毎T がバサへ | 号令の開戦時がバサへ | バサの与ダメ | 死蔵 |");
        Console.WriteLine("|---|---|--:|--:|--:|:-:|");
        for (int r = 0; r < rigs0.Length; r++)
        {
            if (!rigs0[r].F.Occupied().Any(o => o.Def.Id == UnitCatalog.Gan.Id)) continue;
            foreach ((string lab, Formation f, ShufflerRule rule) in
                     new[] { ("V0", rigs0[r].F, V0), ("V1", rigs1[r].F, V1) })
            {
                double turnIn = WhetTo(f, rule, WhetRoute.RallyTurn);
                double openIn = WhetTo(f, rule, WhetRoute.RallyOpening);
                double dmg = BasaOf(f, rule).Damage;
                Console.WriteLine("| " + rigs0[r].Name + " | " + lab + " | " + turnIn.ToString("F2")
                                  + " | " + openIn.ToString("F2") + " | " + dmg.ToString("F1")
                                  + " | " + (dmg <= 1e-9 ? "**100%**" : "—") + " |");
            }
        }

        Console.WriteLine();
        Console.WriteLine("## 表D. 混乱の下流（V1・回/戦・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 同士討ちの撃破 | うち処刑が積まれた | 混乱を庇った |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        for (int r = 0; r < rigs0.Length; r++)
            foreach ((string lab, Formation f, ShufflerRule rule) in
                     new[] { ("V0", rigs0[r].F, V0), ("V1", rigs1[r].F, V1) })
            {
                Led l = Sum(f, rule);
                Console.WriteLine("| " + rigs0[r].Name + " | " + lab + " | " + l.Kills.ToString("F2") + " | "
                                  + l.ExecGain.ToString("F2") + " | " + l.Guards.ToString("F2") + " |");
            }
    }

    // =================================================================================
    // compare —— 拒否権（`compare` 61 行 ＋ 交差帯 12 行）
    // =================================================================================

    static void CompareRows()
    {
        Console.WriteLine("# 第148期 `tumult2 compare` —— 拒否権");
        Console.WriteLine();
        Console.WriteLine("**V0 ＝ 現行の盤面そのもの。V1 ＝ 手番版**"
                          + "（`Presets` のバサを差し替えられないので、**編成ごとにバサだけを入れ替えた写し**を組む）。");
        Console.WriteLine();

        var rows = new List<(string Name, double[] A, double[] B)>();
        foreach ((string name, Formation f) in CompareBuilds())
            rows.Add((name, Rates(f, V0), Rates(SwapBasa(f), V1)));

        int moved = rows.Count(r => Enumerable.Range(0, 5).Any(i => Math.Abs(r.A[i] - r.B[i]) > 0.001));
        Console.WriteLine("## 表E. 特異性（バサを含まない行が ±0.0 か）");
        Console.WriteLine();
        Console.WriteLine("- 動いた行: **" + moved + " / " + rows.Count + "**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach ((string name, double[] a, double[] b) in rows)
            if (Enumerable.Range(0, 5).Any(i => Math.Abs(a[i] - b[i]) > 0.001))
                Console.WriteLine("| " + name + " | " + string.Join(" | ", Enumerable.Range(0, 5)
                    .Select(i => (b[i] - a[i]).ToString("+0.0;-0.0;0.0"))) + " |");
        Console.WriteLine();

        int cross = 0, crossRows = 0;
        var crossNames = new List<string>();
        foreach ((string cname, Formation f) in CrossBuilds())
        {
            double[] a = Rates(f, V0), b = Rates(SwapBasa(f), V1);
            int n = Enumerable.Range(0, 5).Count(i => Math.Abs(a[i] - b[i]) > 0.001);
            cross += n;
            if (n > 0) { crossRows++; crossNames.Add(cname + "（" + n + " セル）"); }
        }
        Console.WriteLine("- 交差帯 12 行 / 60 セルで動いたセル: **" + cross + "**（" + crossRows + " 行）");
        Console.WriteLine("  - " + (crossNames.Count == 0 ? "—" : string.Join(" ／ ", crossNames)));
        Console.WriteLine();

        var pri = rows.Where(r => Baseline.PrimaryRows.Contains(r.Name)).ToList();
        double pa5 = pri.Average(r => r.A[4]), pb5 = pri.Average(r => r.B[4]);
        double aAll = rows.Average(r => r.A.Skip(1).Average()), bAll = rows.Average(r => r.B.Skip(1).Average());
        Console.WriteLine("## 表F. 拒否権1（主判定19行の第五波平均）");
        Console.WriteLine();
        Console.WriteLine("| | V0 | V1 | Δ |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine("| 全61行・第2〜5波 | " + aAll.ToString("F1") + " | " + bAll.ToString("F1")
                          + " | " + (bAll - aAll).ToString("+0.0;-0.0") + " |");
        Console.WriteLine("| **主判定" + pri.Count + "行・第五波** | **" + pa5.ToString("F1") + "** | **"
                          + pb5.ToString("F1") + "** | **" + (pb5 - pa5).ToString("+0.0;-0.0") + "** |");
        Console.WriteLine();
        Console.WriteLine("**拒否権1（歯止め " + Baseline.PrimaryFifthFloor.ToString("F1") + "%）: "
                          + (pb5 < Baseline.PrimaryFifthFloor ? "発動" : "通る") + "**");
        Console.WriteLine();

        Console.WriteLine("## 表G. 拒否権3（いずれかの波で −10.0pt 以上）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | V0 | V1 | Δ | 主判定 |");
        Console.WriteLine("|---|---|--:|--:|--:|:-:|");
        int veto = 0;
        foreach ((string name, double[] a, double[] b) in rows)
            for (int st = 0; st < 5; st++)
                if (b[st] - a[st] <= -10.0)
                {
                    veto++;
                    Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | " + a[st].ToString("F1") + " | "
                                      + b[st].ToString("F1") + " | " + (b[st] - a[st]).ToString("+0.0;-0.0")
                                      + " | " + (Baseline.PrimaryRows.Contains(name) ? "**○**" : "—") + " |");
                }
        Console.WriteLine();
        Console.WriteLine("- **" + veto + " セル**が −10.0pt 以上");
        Console.WriteLine();

        Console.WriteLine("## 表H. 情報セル（第2〜5波で `0 < x < 100`）");
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

    /// <summary>編成の中のバサだけを手番版に差し替えた写しを作る（`Presets` は1行も触らない）。</summary>
    static Formation SwapBasa(Formation f)
    {
        var defs = new UnitDef?[5];
        foreach (var o in f.Occupied())
            if (o.Slot < 5) defs[o.Slot] = o.Def.Id == UnitCatalog.Basa.Id ? BasaOnAction : o.Def;
        return Formation.Build(front1: defs[0], front3: defs[1], center: defs[2], back1: defs[3], back3: defs[4]);
    }

    // =================================================================================
    // check —— 自己検査
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第148期 自己検査 —— `tumult2 check`");
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
        Console.WriteLine("## (b) 既定が `ShufflerRule.Default` と同値（第60期: 既定が動いた診断は検算の相手が移る）");
        Console.WriteLine();
        Console.WriteLine("- `ShufflerRule.Default` = `" + ShufflerRule.Default + "`");
        Console.WriteLine("- この診断の V0 = `" + V0 + "` / V1 = `" + V1 + "`");
        {
            int diff = 0;
            foreach ((string _, Formation f) in CompareBuilds())
            {
                double[] a = Rates(f, null), b = Rates(f, V0);
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - b[i]) > 0.001) diff++;
            }
            Console.WriteLine("- V0 と既定の 305 セル: **" + diff + " 件**" + (diff == 0 ? " ○" : " **×**"));
        }

        Console.WriteLine();
        Console.WriteLine("## (c) **ターン頭で混乱が1件も立たない**（二重掛けの検算・Q0-3）");
        Console.WriteLine();
        Console.WriteLine("V1 のバサから `Actions` を外した版（＝手番を持たない）を回す。");
        Console.WriteLine("印は置かれるが**撃つ口が無い**ので、混乱は 0 件でなければならない。");
        Console.WriteLine();
        {
            double c = 0, m = 0;
            foreach ((string _, Formation f) in Rigs(UnitCatalog.Basa))
                for (int st = 0; st < 5; st++)
                {
                    Led l = Ledger(f, st, V1, 50);
                    c += l.Confuses; m += l.FoeMarks + l.AllyMarks;
                }
            Console.WriteLine("- 立てた **" + c.ToString("F0") + "** ／ 立った **" + m.ToString("F0") + "**"
                              + (c == 0 && m == 0 ? " ○" : " **×**"));
        }

        Console.WriteLine();
        Console.WriteLine("## (d) §4-2: `立てた ≦ 前へ出た` ／ `振った ≦ 立った` ／ **味方側の混乱が 0** ／ **与ダメが 0**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 前へ出た | 立てた | 敵に立った | 敵が振った | 味方に立った | バサの与ダメ | 判定 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|:-:|");
        bool ok = true;
        foreach ((string name, Formation f) in Rigs(BasaOnAction))
            for (int st = 0; st < 5; st++)
            {
                Led l = Ledger(f, st, V1, 50);
                Bas b = BasaOf(f, V1, st, st + 1, 50);
                bool o = l.Confuses <= l.Advanced + 1e-9
                         && l.FoeSwings <= l.FoeMarks + 1e-9
                         && l.AllyMarks <= 1e-9
                         && b.Damage <= 1e-9;
                if (!o) ok = false;
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | " + l.Advanced.ToString("F2")
                                  + " | " + l.Confuses.ToString("F2") + " | " + l.FoeMarks.ToString("F2")
                                  + " | " + l.FoeSwings.ToString("F2") + " | " + l.AllyMarks.ToString("F2")
                                  + " | " + b.Damage.ToString("F2") + " | " + (o ? "○" : "**×**") + " |");
            }
        Console.WriteLine();
        Console.WriteLine("- 全 25 セル" + (ok ? " ○" : " **×**"));

        Console.WriteLine();
        Console.WriteLine("## (e) §4-2: **撃った ＋ 潰れた ＝ 回ってきた手番**（撃つか捨てるかしかない）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 手番 | 撃った | 潰れた | 合計が一致 |");
        Console.WriteLine("|---|--:|--:|--:|:-:|");
        bool okT = true;
        foreach ((string name, Formation f) in Rigs(BasaOnAction))
        {
            Bas b = BasaOf(f, V1);
            bool o = Math.Abs(b.Turns - (b.Skills + b.Stalls)) < 0.005;
            if (!o) okT = false;
            Console.WriteLine("| " + name + " | " + b.Turns.ToString("F2") + " | " + b.Skills.ToString("F2")
                              + " | " + b.Stalls.ToString("F2") + " | " + (o ? "○" : "**×**") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- " + (okT ? "○" : "**×**") + "（バサは痺れ・まどろみ・転倒のどれも持たないので、"
                          + "潰れる理由は `CanAct` 偽だけ。**同席する縛め（クグ）がいる台では痺れが混ざる**）");

        Console.WriteLine();
        Console.WriteLine("## (f) 必須4: `ctx.PickOne` を新たに使っていない");
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
            Console.WriteLine("- 手番版が引く乱数は候補が複数のときの `Roll` 1本だけ: "
                              + (File.ReadAllLines(tr).Any(l => l.Contains("pool[ctx.Roll(pool.Count)]"))
                                 ? "**○**" : "**×**"));
        }
    }

    // =================================================================================
    // 器具
    // =================================================================================

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

    readonly record struct Led(double Advanced, double Confuses, double AllyMarks, double FoeMarks,
                               double FoeSwings, double Kills, double ExecGain, double Guards);

    /// <summary>第2〜5波の平均（回/戦）。</summary>
    static Led Sum(Formation f, ShufflerRule r)
    {
        double a = 0, c = 0, am = 0, fm = 0, fs = 0, k = 0, e = 0, g = 0;
        for (int st = 1; st < 5; st++)
        {
            Led l = Ledger(f, st, r, Seeds);
            a += l.Advanced; c += l.Confuses; am += l.AllyMarks; fm += l.FoeMarks;
            fs += l.FoeSwings; k += l.Kills; e += l.ExecGain; g += l.Guards;
        }
        return new Led(a / 4, c / 4, am / 4, fm / 4, fs / 4, k / 4, e / 4, g / 4);
    }

    /// <summary>
    /// 混乱の帳簿を陣営別に引く（回/戦）。
    /// <b><c>TallyByUnit</c> は駒 <c>Id</c> のキー</b>なので、編成に載っている <c>Id</c> の集合で味方を判別する
    /// （素体の <c>Id</c> に `pa` / `pb` を使ってあるので敵と衝突しない）。
    /// </summary>
    static Led Ledger(Formation f, int st, ShufflerRule r, int seeds)
    {
        var own = new HashSet<string>(f.Occupied().Select(o => o.Def.Id));
        double adv = 0, con = 0, am = 0, fm = 0, fsw = 0, kill = 0, exe = 0, grd = 0;
        for (int seed = 0; seed < seeds; seed++)
        {
            BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                verbose: false, shuffler: r);
            foreach (var kv in res.TallyByUnit)
            {
                UnitTally t = kv.Value;
                adv += t.ShuffleAdvanced; con += t.ShuffleConfuses;
                if (own.Contains(kv.Key)) am += t.ConfusedMarks;
                else
                {
                    fm += t.ConfusedMarks; fsw += t.ConfusedSwings;
                    kill += t.ConfusedKills; exe += t.ConfusedExecGain; grd += t.ConfusedGuards;
                }
            }
        }
        return new Led(adv / seeds, con / seeds, am / seeds, fm / seeds, fsw / seeds,
                       kill / seeds, exe / seeds, grd / seeds);
    }

    readonly record struct Bas(double Turns, double Skills, double Stalls, double Sold,
                               double Damage, double Last, double Confuses);

    /// <summary>バサ自身の手番の帳簿（既定は第2〜5波の平均）。</summary>
    static Bas BasaOf(Formation f, ShufflerRule r, int from = 1, int to = 5, int seeds = Seeds)
    {
        double turns = 0, skills = 0, stalls = 0, sold = 0, dmg = 0, last = 0, con = 0;
        int n = 0;
        for (int st = from; st < to; st++)
        {
            n++;
            for (int seed = 0; seed < seeds; seed++)
            {
                BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                    verbose: false, shuffler: r);
                if (!res.TallyByUnit.TryGetValue(UnitCatalog.Basa.Id, out UnitTally? t)) continue;
                turns += t.TurnsTaken; skills += t.TurnSkills; stalls += t.TurnStalls;
                sold += t.TurnsSurrendered; dmg += t.DamageToEnemy; last += t.LastActiveTurn;
                con += t.ShuffleConfuses;
            }
        }
        double d = Math.Max(1, n) * seeds;
        return new Bas(turns / d, skills / d, stalls / d, sold / d, dmg / d, last / d, con / d);
    }

    /// <summary>
    /// その経路の強化が<b>バサに</b>届いた量（量/戦・第2〜5波の平均）。
    /// <c>BattleResult.WhetToByRoute</c> は<b>経路ごとの受け手</b>を <c>Def.Id</c> で持つ
    /// （第65期。誰も読んで分岐しない・<c>verbose</c> に依存しない）。
    /// </summary>
    static double WhetTo(Formation f, ShufflerRule r, WhetRoute route)
    {
        double sum = 0;
        for (int st = 1; st < 5; st++)
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                    verbose: false, shuffler: r);
                if (res.WhetToByRoute[(int)route].TryGetValue(UnitCatalog.Basa.Id, out int v)) sum += v;
            }
        return sum / (4.0 * Seeds);
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
