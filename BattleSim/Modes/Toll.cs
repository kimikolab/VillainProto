using BattleCore;
using static Common;

// =====================================================================================
// toll モード（第155期） —— 贖いのアガ（前借りと取り立て）
//
// 指示書は design/PHASE155_TOLL_SPEC.md。
//
//     dotnet run --project BattleSim -c Release 0 toll phase0  # Q0-1〜Q0-8（実装前に答える）
//     dotnet run --project BattleSim -c Release 0 toll scan    # 段0 台の下見（素体版が 40〜95% か・決着T）
//     dotnet run --project BattleSim -c Release 0 toll scan probe  # 埋め草の掃引（台ごとに帯へ入れる）
//     dotnet run --project BattleSim -c Release 0 toll run     # 段B（V0 / Vp / Single / All）
//     dotnet run --project BattleSim -c Release 0 toll sweep   # 段C（Threshold / Contracts / Advance）
//     dotnet run --project BattleSim -c Release 0 toll check   # 自己検査（§6）
// =====================================================================================

static class TollDiag
{
    const int Seeds = 200;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "scan": if (arg.StartsWith("probe")) Probe(); else Scan(); return;
            case "run": Stage(); return;
            case "sweep": Sweep(arg); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("toll: モードは phase0 / scan / run / sweep / check。");
                return;
        }
    }

    // =================================================================================
    // 駒と台（§1-7・§4 段0）
    // =================================================================================

    public static int AgaHp = 56, AgaAtk = 4, AgaSpd = 6;

    /// <summary>
    /// 贖いのアガ（仮名）。<b>この期は <c>UnitCatalog.All</c> に入れない</b>ので、
    /// 診断のローカルにしか存在しない（第138期の礫のガレ・第153期のノチと同じ扱い）。
    /// </summary>
    /// <param name="plain">特性を1つも持たない素体（第69期の標準器具の対照）。<b>手番も通常攻撃に戻す。</b></param>
    /// <param name="noToll">
    /// <b>マイナスを外した版（<c>yP</c>）。</b> 札を3枚に切ってあるので
    /// <c>Traits</c> の配列から <see cref="TraitId.Toll"/> を抜くだけで作れる（第74期の作法）。
    /// <b><see cref="TraitId.Brand"/> は <c>Toll</c> の下流なので、この版では燃料が1点も溜まらない。</b>
    /// </param>
    public static UnitDef Aga(bool plain = false, bool noToll = false) => new()
    {
        Id = plain ? "aga_plain" : "aga",
        Name = plain ? "素体のアガ" : noToll ? "贖いのアガ（前借りのみ）" : "贖いのアガ",
        MaxHp = AgaHp, Attack = AgaAtk, Speed = AgaSpd,
        Advances = false,
        Traits = plain ? Array.Empty<TraitId>()
               : noToll ? new[] { TraitId.Indulgence, TraitId.Brand }
               : new[] { TraitId.Indulgence, TraitId.Toll, TraitId.Brand },
        // 素体は通常攻撃に戻す（`Skill` のままだと「何もしない駒」になり、対照が
        // 「特性を外した版」ではなく「手番を捨てた版」になる。第138期の則）。
        Actions = plain ? null : new UnitAction[] { new(ActionKind.Skill, Label: "赦しを説いている") },
        PlusText = "手番で味方1体を大きく癒す。取り立てた分は炎に変わる",
        MinusText = "癒した分は負債として積まれ、溜まりきると本人から取り立てられる",
        Flavor = "救いは無料ではない。誰もその但し書きを読まなかった。"
    };

    /// <summary>
    /// 埋め草（<b>特性を1つも持たない素体</b>・第143期の則）。
    /// 台ごとに別の点を採る（第153期 §4 段0 と同じ手順）——1つの目盛りでは 5 台が同時に帯へ入らない。
    /// </summary>
    public static UnitDef Filler(string id, int hp, int atk, int spd = 7) => new()
    {
        Id = "t_" + id, Name = "素体" + id, MaxHp = hp, Attack = atk, Speed = spd,
        Advances = true, Traits = Array.Empty<TraitId>()
    };

    /// <summary>台ごとの埋め草の強さ（攻, HP）。段0 の掃引で決めた。</summary>
    public static readonly (int Atk, int Hp)[] FillerOf = { (30, 70), (14, 130), (14, 110), (18, 130), (14, 110) };

    /// <summary>
    /// ローカル台（§4 段0）。<b><c>Presets</c> には置かない</b>（`compare` 61行を汚さない）。
    /// アガは全台とも <b>後1</b>（贖いは隣接を1つも読まないので、席は「殴られない後列」で固定する）。
    ///
    /// <para><b>台1 と台2 は決着の速さで切ってある</b>——この期の本丸は
    /// 「取り立ての代金が決着の速さで符号を変えるか」なので、
    /// 台1 は出力役を厚く（速く終わる＝取り立てが来る前に勝つ）、
    /// 台2 は耐久寄り（長引く＝取り立てが来る）にする。</para>
    /// </summary>
    public static (string Name, Formation F)[] Rigs(UnitDef aga)
    {
        var r = new (string, Formation)[5];
        for (int i = 0; i < 5; i++) r[i] = Rig(i, aga, FillerOf[i].Atk, FillerOf[i].Hp);
        return r;
    }

    public static (string Name, Formation F) Rig(int i, UnitDef aga, int atk, int hp)
    {
        UnitDef A() => Filler("a", hp, atk);
        UnitDef B() => Filler("b", hp, atk);
        UnitDef C() => Filler("c", hp, atk);
        UnitDef D() => Filler("d", hp, atk);
        return i switch
        {
            // 台1 速い: 埋め草は高攻・低HP。決着が短いので取り立てが来る前に勝つはず（予測1）。
            0 => ("台1 速い (アガ×高出力)",
                Formation.Build(front1: A(), front3: B(),
                                center: C(), back1: aga, back3: D())),

            // 台2 遅い: 埋め草は低攻・高HP。決着が長いので取り立てが何度も来るはず（予測1）。
            1 => ("台2 遅い (アガ×耐久)",
                Formation.Build(front1: A(), front3: B(),
                                center: C(), back1: aga, back3: D())),

            // 台3 タンク同席: 前借りの宛先がゴルムに固定される。**Q0-3（肩代わり）もここで測る**
            // ——巨躯は `ApplyDamage` の中で肩代わりするので、アガ自身への削りを飲みうる。
            2 => ("台3 タンク (アガ×ゴルム)",
                Formation.Build(front1: UnitCatalog.Golm, front3: A(),
                                center: B(), back1: aga, back3: C())),

            // 台4 回復が既にいる: 5枚目として重複しないか（ノノ＝継ぎ当て）。
            3 => ("台4 回復同席 (アガ×ノノ)",
                Formation.Build(front1: A(), front3: B(),
                                center: UnitCatalog.Nono, back1: aga, back3: C())),

            // 台5 陰性対照（予測6）。**アガを1枚も含まない。**
            _ => ("台5 陰性対照 (アガ非在席)",
                Formation.Build(front1: A(), front3: B(),
                                center: C(), back1: D(), back3: Filler("e", hp, atk))),
        };
    }

    // =================================================================================
    // Phase 0（§2）
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第155期 `toll phase0` —— 前提を実装から引き直す");
        Console.WriteLine();

        string? tr = FindSource("BattleCore", "Traits.cs");
        string? eng = FindSource("BattleCore", "BattleEngine.cs");
        if (tr is null || eng is null)
        {
            Console.WriteLine("**`BattleCore` のソースが引けない。止める**（第117期）。");
            return;
        }
        string[] T = File.ReadAllLines(tr), E = File.ReadAllLines(eng);

        Q02(); Console.WriteLine();
        Q03(); Console.WriteLine();
        Q04(E); Console.WriteLine();
        Q05(); Console.WriteLine();
        Q07(); Console.WriteLine();
        Q08(E, T); Console.WriteLine();
        Scan();
    }

    /// <summary>Q0-2 —— 中央値を <c>UnitCatalog.All</c> の 52 枚から数え直す。</summary>
    static void Q02()
    {
        Console.WriteLine("## Q0-2 —— 現在の中央値（`UnitCatalog.All` から数え直す）");
        Console.WriteLine();
        var all = UnitCatalog.All;
        Console.WriteLine("枚数: **" + all.Count + "**");
        Console.WriteLine();
        Console.WriteLine("| 量 | 最小 | p25 | **中央値** | p75 | 最大 | 平均 |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|");
        Row("攻撃力", all.Select(u => u.Attack).ToArray());
        Row("最大HP", all.Select(u => u.MaxHp).ToArray());
        Row("速さ", all.Select(u => u.Speed).ToArray());
        Console.WriteLine();
        Console.WriteLine("**アガの値は 攻 " + AgaAtk + " / HP " + AgaHp + " / 速 " + AgaSpd + "。**");
        Console.WriteLine();
        Console.WriteLine("既存の回復役4枚（比較用）:");
        Console.WriteLine();
        Console.WriteLine("| 駒 | HP | 攻 | 速 | 手番 |");
        Console.WriteLine("|---|---:|---:|---:|---|");
        foreach (string id in new[] { "nono", "beni", "shio", "nara" })
        {
            UnitDef u = UnitCatalog.ById(id);
            Console.WriteLine("| " + u.Name + " | " + u.MaxHp + " | " + u.Attack + " | " + u.Speed
                + " | " + (u.Actions is null ? "通常攻撃"
                           : string.Join(" → ", u.Actions.Select(a => a.Kind.ToString()))) + " |");
        }

        static void Row(string name, int[] v)
        {
            var s = v.OrderBy(x => x).ToArray();
            Console.WriteLine("| " + name + " | " + s[0] + " | " + Pct(s, 25) + " | **" + Pct(s, 50)
                              + "** | " + Pct(s, 75) + " | " + s[^1] + " | " + v.Average().ToString("F1") + " |");
        }
        static int Pct(int[] sorted, int p)
        {
            int i = (int)Math.Round((sorted.Length - 1) * p / 100.0, MidpointRounding.AwayFromZero);
            return sorted[Math.Clamp(i, 0, sorted.Length - 1)];
        }
    }

    /// <summary>Q0-3 —— アガへの肩代わりの削り（§1-3 (c)）が庇いの対象外か。<b>走らせて数える。</b></summary>
    static void Q03()
    {
        Console.WriteLine("## Q0-3 —— 踏み倒しの削りは肩代わりされるか（**実測**）");
        Console.WriteLine();
        Console.WriteLine("`TollTrait.OnAllyDeath` は `ctx.ApplyDamage(self, debt, self, ...)` を呼ぶ。");
        Console.WriteLine("**庇い（`Guardian`）・後備え（`RearGuard`）・標的は `SelectTarget` の差し替えなので通らない**");
        Console.WriteLine("（`ApplyDamage` を直接叩くと標的選択そのものが走らない）。");
        Console.WriteLine("**巨躯（ゴルム）・分かち（ドハ）は `ApplyDamage` の中で肩代わりするので通りうる。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 踏み倒しの回数 | 引き受けた負債（名目） | **アガの HP が実際に減った量** | 差 ＝ 肩代わりされた量 |");
        Console.WriteLine("|---|---:|---:|---:|---:|");
        var rigs = Rigs(Aga());
        for (int i = 0; i < rigs.Length; i++)
        {
            TlAcc a = Measure(rigs[i].F, IndulgenceRule.Default);
            double b = a.Battles;
            Console.WriteLine("| " + rigs[i].Name + " | " + (a.Forgives / b).ToString("F2")
                + " | " + (a.Forgiven / b).ToString("F2")
                + " | " + (a.ForgivenSelfHp / b).ToString("F2")
                + " | " + ((a.Forgiven - a.ForgivenSelfHp) / b).ToString("F2") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**差が 0 でなければ、肩代わり（巨躯・分かち）か HP の下限（0）で吸われている。**");
        Console.WriteLine("第153期の実例（リィカの代償 8 のうち本人が払ったのは 1）と同じ形なら、測定値の読みが変わる。");
        Console.WriteLine("**この期では塞がない**（指示書 Q0-3）。");
    }

    /// <summary>Q0-4 —— 焼きの標的選択。<b>`Pierce` に engine の改修が要るか。</b></summary>
    static void Q04(string[] E)
    {
        Console.WriteLine("## Q0-4 —— 焼きの標的選択（`Single` / `All` / `Pierce`）");
        Console.WriteLine();
        foreach (string name in new[] { "TargetPool", "SelectPierceEntry", "ResolvePierce", "LaneOccupants", "Roll" })
        {
            string? decl = E.FirstOrDefault(l => l.Contains(" " + name + "("));
            string vis = decl is null ? "**引けない**"
                : decl.TrimStart().StartsWith("public") ? "`public`"
                : decl.TrimStart().StartsWith("internal") ? "`internal`" : "**`private`**";
            Console.WriteLine("- `" + name + "` … " + vis);
        }
        Console.WriteLine();
        int afterAttack = E.Count(l => l.Contains("t.OnAfterAttack(this, actor"));
        Console.WriteLine("`ResolvePierce` が `OnAfterAttack` を発火させる箇所: **" + afterAttack
                          + "** 箇所（`PerformAttack` と共有）。");
        Console.WriteLine();
        Console.WriteLine("**答え**: `Single` と `All` は公開されている窓口だけで書ける"
                          + "（`TargetPool` ＋ `LivingMembers` ＋ `Opponent` ＋ `SecondaryPercent`）。");
        Console.WriteLine("`Pierce` は `SelectPierceEntry` / `ResolvePierce` が `private` で、しかも");
        Console.WriteLine("**`ResolvePierce` はレーンの先頭に対して `OnAfterAttack` を発火させる**");
        Console.WriteLine("——焼きは攻撃ではないので、通すと「特性の発動は攻撃1回につき1度」の契約が破れる。");
        Console.WriteLine("**指示書 §1-5 に従って `Pierce` は落とした。`BrandBlast` に枝そのものを置いていない**");
        Console.WriteLine("（列挙に枝だけ足して読む側を開けない形にはしない＝第148期）。");
    }

    /// <summary>Q0-5 —— 焼きの出力が帳簿に載るか（第154期の申し送り）。</summary>
    static void Q05()
    {
        Console.WriteLine("## Q0-5 —— 焼きの出力は帳簿に載るか（**第154期の申し送り**）");
        Console.WriteLine();
        Console.WriteLine("第154期は「振られなかった打点」が `PerformAttack` しか数えず、カドの反撃が1件も載らなかった。");
        Console.WriteLine("この期は**攻撃側の帳簿が本体**なので、`ApplyDamage` を直接叩いた分が");
        Console.WriteLine("`DamageByUnit` / `UnitTally.DamageToEnemy` に載ることを先に確かめる。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 焼きの回数 | 焼きが削った HP（自前の帳簿） | `DamageByUnit[\"aga\"]` | `DamageToEnemy` | 一致 |");
        Console.WriteLine("|---|---:|---:|---:|---:|---|");
        var rigs = Rigs(Aga());
        for (int i = 0; i < rigs.Length - 1; i++)
        {
            long fires = 0, removed = 0, byUnit = 0, toEnemy = 0;
            for (int st = 1; st < 5; st++)
                for (int seed = 0; seed < 40; seed++)
                {
                    BattleResult r = BattleEngine.Run(rigs[i].F, EnemyCatalog.Stages[st].Enemy, seed,
                                                      verbose: false, indulgence: IndulgenceRule.Default);
                    fires += r.Indulgence.BrandFires;
                    removed += r.Indulgence.BrandRemoved;
                    if (r.DamageByUnit.TryGetValue("aga", out int d)) byUnit += d;
                    if (r.TallyByUnit.TryGetValue("aga", out UnitTally? t)) toEnemy += t.DamageToEnemy;
                }
            Console.WriteLine("| " + rigs[i].Name + " | " + fires + " | " + removed + " | " + byUnit
                              + " | " + toEnemy + " | " + (byUnit >= removed && removed > 0 ? "**載る**"
                                 : removed == 0 ? "（焼きが 0）" : "**載らない**") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**`DamageByUnit` は名目量（過剰分を含む）**なので `焼きが削った HP` 以上になるのが正しい。");
        Console.WriteLine("アガは通常攻撃を1度も振らない（`Actions = [Skill]`）ので、**この列は全部が焼きの出力**である。");
    }

    /// <summary>Q0-7 —— 過去に同型を測っていないか（`design/` の走査）。</summary>
    static void Q07()
    {
        Console.WriteLine("## Q0-7 —— 過去に同型を測っていないか（`design/` の走査）");
        Console.WriteLine();
        string? root = FindDir("design");
        if (root is null) { Console.WriteLine("**`design/` が引けない。止める**（第117期）。"); return; }

        var files = Directory.GetFiles(root, "*.md").OrderBy(x => x).ToArray();
        Console.WriteLine("`design/` の md: **" + files.Length + "** ファイル。");
        Console.WriteLine();
        Console.WriteLine("| 語 | 当たったファイル数 | 上位 |");
        Console.WriteLine("|---|---:|---|");
        foreach (string w in new[] { "前借り", "後払い", "負債", "取り立て", "利子", "贖", "溜めた量をダメージ", "回復に代償" })
        {
            var hit = files.Where(f => File.ReadAllText(f).Contains(w)).Select(Path.GetFileName).ToArray();
            Console.WriteLine("| " + w + " | " + hit.Length + " | "
                              + (hit.Length == 0 ? "—" : string.Join(" / ", hit.Take(4))) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**近い2枚を実装から引く**（指示書 Q0-7 が名指し）:");
        Console.WriteLine();
        string? tr = FindSource("BattleCore", "Traits.cs");
        if (tr is not null)
        {
            string t = File.ReadAllText(tr);
            foreach ((string cls, string what) in new[]
                     { ("MenderTrait", "繕った量の半分だけ自分が減る（ノノ）"),
                       ("ReviverTrait", "1回縫うごとに自分の最大HPが半分（ヴェル）") })
            {
                int i = t.IndexOf("class " + cls, StringComparison.Ordinal);
                Console.WriteLine("- `" + cls + "`（" + what + "）… " + (i < 0 ? "**引けない**" : "実装あり"));
            }
        }
        Console.WriteLine();
        Console.WriteLine("**どちらも「自分が減る」型で、代金の宛先は保持者自身**。");
        Console.WriteLine("アガの取り立ては**代金の宛先が貸した本人**で、しかも**その代金が保持者の出力になる**");
        Console.WriteLine("——第153期（代金 ＝ 敵への回復 ＝ 味方に読み手のいない通貨）と");
        Console.WriteLine("第154期（代金が抱えている全員に掛かる）の2本を、構造として最初から満たす形である。");
    }

    /// <summary>Q0-8 —— 状態キーを足すか。</summary>
    static void Q08(string[] E, string[] T)
    {
        Console.WriteLine("## Q0-8 —— 負債は状態キーか私有カウンタか");
        Console.WriteLine();
        Console.WriteLine("**状態キーにした**（指示書の推奨）。他の駒が後から読めるようになる＝盤面の汚れになる。");
        Console.WriteLine();
        Console.WriteLine("- `StatusKeys.All`: **" + StatusKeys.All.Length + "** 本（`Debt` は "
                          + (StatusKeys.All.Contains(StatusKeys.Debt) ? "**入っている**" : "**入っていない。止める**") + "）");
        Console.WriteLine("- `StatusKeys.LabelOf(Debt)`: `" + StatusKeys.LabelOf(StatusKeys.Debt) + "`");
        Console.WriteLine("- `UnitTally.CarryKeys`: **" + UnitTally.CarryKeys.Length
                          + "** 本（**足さない**——増やすと過去の期の帳簿の添字が動く。第147期・第153期と同じ判断）");
        Console.WriteLine("- `NoteStatusGain` の `switch`: **" + E.Count(l => l.Contains("case StatusKeys.")) + "** 本"
                          + "（未知のキーは既定で落ちるので足さなくても落ちない）");
        Console.WriteLine("- `Engagement` の境界は `StatusKeys.All` を回すので、**`All` に足せば会戦で消える**（自己検査 (h)）");
        Console.WriteLine();
        Console.WriteLine("**燃料（`BrandTrait.FuelKey`）は私有カウンタのまま**にした"
                          + "——保持者の中だけで閉じていて、他の駒が読む意味が無い（`ParryTrait.StockKey` と同じ扱い）。");
        Console.WriteLine();
        Console.WriteLine("`Traits.cs` の `TraitId` は **" + T.Count(l => System.Text.RegularExpressions.Regex.IsMatch(l, @"^    [A-Z]\w*,")) + "** 本。");
    }

    // =================================================================================
    // 段0 —— 台の下見（Q0-6）
    // =================================================================================

    static void Scan()
    {
        Console.WriteLine("## 段0 —— 台の下見（**素体のアガ**・seed 0.." + (Seeds - 1) + "）");
        Console.WriteLine();
        Console.WriteLine("**決着Tは台の設計変数**（§4）。台1 と台2 が十分に離れていなければ本丸は測れない。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 埋め草（攻/HP） | 勝率（1..5波） | 第2〜5波平均 | 帯（40〜95%） | 情報セル | **決着T** |");
        Console.WriteLine("|---|---|---|---:|---|---:|---:|");
        var rigs = Rigs(Aga(plain: true));
        for (int i = 0; i < rigs.Length; i++)
        {
            (double[] w, double turns) = Waves(rigs[i].F);
            double m = (w[1] + w[2] + w[3] + w[4]) / 4;
            int info = w.Skip(1).Count(x => x > 0.0 && x < 100.0);
            Console.WriteLine("| " + rigs[i].Name + " | " + FillerOf[i].Atk + " / " + FillerOf[i].Hp
                              + " | " + string.Join(" / ", w.Select(x => x.ToString("F1")))
                              + " | " + m.ToString("F1") + " | " + (m >= 40 && m <= 95 ? "○" : "**×**")
                              + " | " + info + " | " + turns.ToString("F2") + " |");
        }
    }

    /// <summary>埋め草の掃引（§4 段0）。<b>台ごとに点を選ぶ。</b></summary>
    static void Probe()
    {
        Console.WriteLine("## 段0 —— 埋め草の掃引（**素体のアガ**・台ごとに帯 40〜95% の点を探す）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 攻 | HP | 勝率（1..5波） | 第2〜5波平均 | 帯 | 情報セル | 決着T |");
        Console.WriteLine("|---|---:|---:|---|---:|---|---:|---:|");
        for (int i = 0; i < 5; i++)
            foreach (int atk in new[] { 10, 14, 18, 22, 26, 30 })
                foreach (int hp in new[] { 70, 90, 110, 130 })
                {
                    (string name, Formation f) = Rig(i, Aga(plain: true), atk, hp);
                    (double[] w, double turns) = Waves(f);
                    double m = (w[1] + w[2] + w[3] + w[4]) / 4;
                    int info = w.Skip(1).Count(x => x > 0.0 && x < 100.0);
                    Console.WriteLine("| " + name + " | " + atk + " | " + hp + " | "
                                      + string.Join(" / ", w.Select(x => x.ToString("F1")))
                                      + " | " + m.ToString("F1") + " | " + (m >= 40 && m <= 95 ? "○" : "×")
                                      + " | " + info + " | " + turns.ToString("F2") + " |");
                }
    }

    // =================================================================================
    // 版（段B）
    // =================================================================================

    /// <summary>素体差し替えの対照（第69期の標準器具）。<b>この期の線はこの期の対照で引く。</b></summary>
    static (string Tag, UnitDef Aga, IndulgenceRule Rule)[] Versions() => new[]
    {
        ("V0 素体", Aga(plain: true), IndulgenceRule.Default),
        ("Vp 前借りのみ", Aga(noToll: true), IndulgenceRule.Default),
        ("V1 Single", Aga(), IndulgenceRule.Default),
        ("V2 All", Aga(), IndulgenceRule.Default with { Blast = BrandBlast.All }),
    };

    sealed class TlAcc
    {
        public long Battles, Wins, Turns, Life;
        public long Fires, Asked, Stacked, Dry, DryDrought, NoPatient;
        public long TollFires, Collected, TollTaken, TollFloored, TollYokeCut, TollKills;
        public long Forgives, Forgiven, ForgivenSelfHp;
        public long BrandFires, BrandSpent, BrandRemoved, BrandYokeCut, BrandHits, BrandKills, BrandDry, BrandResidual;
        public long Residual, Unbalanced;
        public double[] WaveWin = new double[5];
    }

    static TlAcc Measure(Formation f, IndulgenceRule rule, int stFrom = 1, int stTo = 5)
    {
        var a = new TlAcc();
        for (int st = stFrom; st < stTo; st++)
        {
            long w = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                  verbose: false, indulgence: rule);
                a.Battles++; a.Turns += r.Turns;
                if (r.PlayerWon) { a.Wins++; w++; }
                foreach (var o in f.Occupied())
                    if (r.TallyByUnit.TryGetValue(o.Def.Id, out UnitTally? t)) a.Life += t.LastActiveTurn;
                IndulgenceLedger L = r.Indulgence;
                a.Fires += L.Fires; a.Asked += L.Asked; a.Stacked += L.Stacked;
                a.Dry += L.Dry; a.DryDrought += L.DryDrought; a.NoPatient += L.NoPatient;
                a.TollFires += L.TollFires; a.Collected += L.Collected; a.TollTaken += L.TollTaken;
                a.TollFloored += L.TollFloored; a.TollYokeCut += L.TollYokeCut; a.TollKills += L.TollKills;
                a.Forgives += L.Forgives; a.Forgiven += L.Forgiven; a.ForgivenSelfHp += L.ForgivenSelfHp;
                a.BrandFires += L.BrandFires; a.BrandSpent += L.BrandSpent; a.BrandRemoved += L.BrandRemoved;
                a.BrandYokeCut += L.BrandYokeCut; a.BrandHits += L.BrandHits; a.BrandKills += L.BrandKills;
                a.BrandDry += L.BrandDry; a.BrandResidual += L.BrandResidual;
                a.Residual += L.Residual;
                if (!L.Balanced) a.Unbalanced++;
            }
            a.WaveWin[st] = 100.0 * w / Seeds;
        }
        return a;
    }

    /// <summary>段B —— 版を**同じ台・同じ seed** で並べる。</summary>
    static void Stage()
    {
        Console.WriteLine("# 第155期 段B —— 版の比較（同じ台・同じ seed 0.." + (Seeds - 1) + "・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("`帰属` は素体差し替え比（版 − V0）。`取り分` は Δ ÷ (100 − V0)（§8-1）。");
        Console.WriteLine("**代金の値段は `Vn − Vp`**（§段B）。");
        Console.WriteLine("`生存T÷決着T` は第137期の則（生の生存Tは決着Tの短縮に食われる）。");
        Console.WriteLine();

        var rigs = Rigs(Aga());
        for (int i = 0; i < rigs.Length; i++)
        {
            Console.WriteLine("## " + rigs[i].Name);
            Console.WriteLine();
            Console.WriteLine("| 版 | 勝率 2/3/4/5波 | 第2〜5波平均 | 帰属 | 取り分 | **代金(Vn−Vp)** | 決着T | 味方生存T | 生存T÷決着T | 比の差 |");
            Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");
            double baseM = 0, baseRatio = 0, vpM = 0;
            var accs = new List<(string Tag, TlAcc A)>();
            foreach ((string tag, UnitDef aga, IndulgenceRule rule) in Versions())
            {
                Formation f = Rig(i, aga, FillerOf[i].Atk, FillerOf[i].Hp).F;
                TlAcc a = Measure(f, rule);
                accs.Add((tag, a));
                double m = 100.0 * a.Wins / a.Battles;
                double life = (double)a.Life / a.Battles / 5.0;
                double ratio = life / ((double)a.Turns / a.Battles);
                if (tag.StartsWith("V0")) { baseM = m; baseRatio = ratio; }
                if (tag.StartsWith("Vp")) vpM = m;
                string share = tag.StartsWith("V0") ? "—"
                    : baseM >= 100.0 ? "—" : ((m - baseM) / (100 - baseM)).ToString("F3");
                Console.WriteLine("| " + tag + " | "
                    + string.Join(" / ", a.WaveWin.Skip(1).Select(x => x.ToString("F1"))) + " | "
                    + m.ToString("F1") + " | " + (tag.StartsWith("V0") ? "—" : (m - baseM).ToString("+0.0;-0.0;0.0"))
                    + " | " + share
                    + " | " + (tag.StartsWith("V0") || tag.StartsWith("Vp") ? "—" : (m - vpM).ToString("+0.0;-0.0;0.0"))
                    + " | " + ((double)a.Turns / a.Battles).ToString("F2")
                    + " | " + life.ToString("F2")
                    + " | " + ratio.ToString("F3")
                    + " | " + (tag.StartsWith("V0") ? "—" : (ratio - baseRatio).ToString("+0.000;-0.000;0.000")) + " |");
            }
            Console.WriteLine();
            Ledger(accs);
            Console.WriteLine();
        }
    }

    static void Ledger(List<(string Tag, TlAcc A)> accs)
    {
        Console.WriteLine("| 版 | 前借り回数 | 増えたHP(=負債) | 空振り | うち渇き | 相手なし | 取立回数 | 取立(名目) | **取立(実HP)** | HP1で止 | 軛で切 |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach ((string tag, TlAcc a) in accs)
        {
            double b = a.Battles;
            Console.WriteLine("| " + tag + " | " + (a.Fires / b).ToString("F2") + " | " + (a.Stacked / b).ToString("F2")
                + " | " + (a.Dry / b).ToString("F2") + " | " + (a.DryDrought / b).ToString("F2")
                + " | " + (a.NoPatient / b).ToString("F2") + " | " + (a.TollFires / b).ToString("F2")
                + " | " + (a.Collected / b).ToString("F2") + " | **" + (a.TollTaken / b).ToString("F2") + "**"
                + " | " + (a.TollFloored / b).ToString("F2") + " | " + (a.TollYokeCut / b).ToString("F2") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("| 版 | 踏み倒し回数 | 引受(名目) | アガの実HP減 | 焼き回数 | 叩き込み(名目) | **焼きの実HP** | 軛で切 | 当たり体数 | 撃破 | 相手なし | 燃え残り | 未回収 | 収支 |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|");
        foreach ((string tag, TlAcc a) in accs)
        {
            double b = a.Battles;
            Console.WriteLine("| " + tag + " | " + (a.Forgives / b).ToString("F2") + " | " + (a.Forgiven / b).ToString("F2")
                + " | " + (a.ForgivenSelfHp / b).ToString("F2") + " | " + (a.BrandFires / b).ToString("F2")
                + " | " + (a.BrandSpent / b).ToString("F2") + " | **" + (a.BrandRemoved / b).ToString("F2") + "**"
                + " | " + (a.BrandYokeCut / b).ToString("F2") + " | " + (a.BrandHits / b).ToString("F2")
                + " | " + (a.BrandKills / b).ToString("F2") + " | " + (a.BrandDry / b).ToString("F2")
                + " | " + (a.BrandResidual / b).ToString("F2") + " | " + (a.Residual / b).ToString("F2")
                + " | " + (a.Unbalanced == 0 ? "**閉じる**" : "**ずれ " + a.Unbalanced + " 戦**") + " |");
        }
    }

    // =================================================================================
    // 段C —— 掃引
    // =================================================================================

    static void Sweep(string arg)
    {
        bool adv = arg.StartsWith("advance");
        bool con = arg.StartsWith("contract");
        Console.WriteLine(adv ? "# 第155期 段C-3 —— `Advance` の掃引（**強度のノブとして働くか**）"
                        : con ? "# 第155期 段C-2 —— `Contracts` の掃引（同時に負債を抱えられる味方の数）"
                              : "# 第155期 段C-1 —— `Threshold` の掃引（取り立てが来る速さ）");
        Console.WriteLine();

        var rigs = Rigs(Aga());
        var plainRigs = Rigs(Aga(plain: true));
        var vpRigs = Rigs(Aga(noToll: true));
        for (int i = 0; i < rigs.Length - 1; i++)   // 台5（陰性対照）は掃引しない
        {
            TlAcc v0 = Measure(plainRigs[i].F, IndulgenceRule.Default);
            TlAcc vp = Measure(vpRigs[i].F, IndulgenceRule.Default);
            double baseM = 100.0 * v0.Wins / v0.Battles;
            double vpM = 100.0 * vp.Wins / vp.Battles;
            Console.WriteLine("## " + rigs[i].Name + "（V0 素体 " + baseM.ToString("F1")
                              + "% / Vp 前借りのみ " + vpM.ToString("F1") + "%）");
            Console.WriteLine();
            Console.WriteLine("| ノブ | 出口 | 勝率 | 帰属 | 代金(−Vp) | 前借り | 負債 | 取立回数 | 取立(実HP) | 焼き回数 | 焼き(実HP) | 未回収 | 決着T |");
            Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

            var points = new List<(string Knob, IndulgenceRule R)>();
            if (adv)
                foreach (int v in new[] { 10, 20, 30, 45, 60 })
                    points.Add(("Advance " + v, IndulgenceRule.Default with { Advance = v }));
            else if (con)
                foreach (int v in new[] { 1, 2, 0 })
                    points.Add(("Contracts " + (v == 0 ? "0（無制限）" : v.ToString()),
                                IndulgenceRule.Default with { Contracts = v }));
            else
                foreach (int v in new[] { 30, 45, 60, 80, 100, 130, 160 })
                    points.Add(("Threshold " + v, IndulgenceRule.Default with { Threshold = v }));

            foreach ((string knob, IndulgenceRule r0) in points)
                foreach (BrandBlast blast in new[] { BrandBlast.Single, BrandBlast.All })
                {
                    IndulgenceRule r = r0 with { Blast = blast };
                    TlAcc a = Measure(rigs[i].F, r);
                    double m = 100.0 * a.Wins / a.Battles;
                    double b = a.Battles;
                    Console.WriteLine("| " + knob + " | " + blast + " | " + m.ToString("F1")
                        + " | " + (m - baseM).ToString("+0.0;-0.0;0.0")
                        + " | " + (m - vpM).ToString("+0.0;-0.0;0.0")
                        + " | " + (a.Fires / b).ToString("F2") + " | " + (a.Stacked / b).ToString("F2")
                        + " | " + (a.TollFires / b).ToString("F2") + " | " + (a.TollTaken / b).ToString("F2")
                        + " | " + (a.BrandFires / b).ToString("F2") + " | " + (a.BrandRemoved / b).ToString("F2")
                        + " | " + (a.Residual / b).ToString("F2")
                        + " | " + ((double)a.Turns / b).ToString("F2") + " |");
                }
            Console.WriteLine();
        }
    }

    // =================================================================================
    // 自己検査（§6）
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第155期 `toll check` —— 自己検査");
        Console.WriteLine();

        string bal = arg.Trim().Length > 0 ? arg.Trim() : "docs/balance.md";
        Console.WriteLine("## (a) `compare` 305 セルが `" + bal + "` と 0 件");
        Console.WriteLine();
        if (!File.Exists(bal)) Console.WriteLine("**`" + bal + "` が無い。手で `compare` を回して差分を見ること。**");
        else
        {
            int cells = 0;
            foreach (string line in File.ReadAllLines(bal).Where(l => l.StartsWith("| ") && l.Contains("%")))
                cells += line.Split('|').Select(c => c.Trim()).Count(c => c.EndsWith("%"));
            Console.WriteLine("`" + bal + "` の `%` セルは **" + cells + "** 個。"
                              + "突き合わせは `0 compare` の出力と `diff` で行う"
                              + "——**アガは `UnitCatalog.All` にも `Presets` にも無いので、"
                              + "差分が出たら実装が既定を動かしている。**");
        }
        Console.WriteLine();

        string? eng = FindSource("BattleCore", "BattleEngine.cs");
        string? tr = FindSource("BattleCore", "Traits.cs");
        if (eng is not null && tr is not null)
        {
            int n = File.ReadAllLines(eng).Count(l => l.Contains("PickOne("))
                  + File.ReadAllLines(tr).Count(l => l.Contains("PickOne("));
            Console.WriteLine("## (b) `ctx.PickOne` を新たに使っていない");
            Console.WriteLine();
            Console.WriteLine("`BattleCore` の `PickOne(` は **" + n + "** 行（第153・154期の基準は 27）。"
                              + (n == 27 ? "**一致**" : "**要確認**"));
            Console.WriteLine();
        }

        var rigs = Rigs(Aga());

        Console.WriteLine("## (c) 収支が閉じる（積んだ ＝ 取り立て ＋ 踏み倒し ＋ 未回収）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 戦数 | 積んだ | 取立＋踏み倒し＋未回収 | ずれた戦 |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|");
        long badTotal = 0;
        for (int i = 0; i < rigs.Length; i++)
            foreach ((string tag, UnitDef aga, IndulgenceRule rule) in Versions())
            {
                if (tag.StartsWith("V0")) continue;
                TlAcc a = Measure(Rig(i, aga, FillerOf[i].Atk, FillerOf[i].Hp).F, rule);
                badTotal += a.Unbalanced;
                Console.WriteLine("| " + rigs[i].Name + " | " + tag + " | " + a.Battles + " | " + a.Stacked
                    + " | " + (a.Collected + a.Forgiven + a.Residual) + " | " + a.Unbalanced + " |");
            }
        Console.WriteLine();
        Console.WriteLine(badTotal == 0 ? "**1点もずれていない。**" : "**ずれている。止める。**");
        Console.WriteLine();

        Console.WriteLine("## (d) 取り立てで死んだ味方が 0 体（`lethal: false` が効いている）");
        Console.WriteLine();
        Console.WriteLine("`TollTrait.Collect` は `ApplyDamage` の直後に `ally.IsAlive` を見て数える"
                          + "（`IndulgenceLedger.TollKills`）。**1件でもあればクランプが効いていない。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 取立回数 | **取り立てで死んだ** | HP1 で止まった | 踏み倒し |");
        Console.WriteLine("|---|---:|---:|---:|---:|");
        for (int i = 0; i < rigs.Length - 1; i++)
        {
            TlAcc a = Measure(rigs[i].F, IndulgenceRule.Default);
            double b = a.Battles;
            Console.WriteLine("| " + rigs[i].Name + " | " + (a.TollFires / b).ToString("F2")
                + " | " + (a.TollKills == 0 ? "**0**" : "**" + a.TollKills + "。止める**")
                + " | " + (a.TollFloored / b).ToString("F2") + " | " + (a.Forgives / b).ToString("F2") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## (e) 満タンの味方に前借りを撃っても負債が積まれない");
        Console.WriteLine();
        Console.WriteLine("`MostHurtAlly` は `Hp < MaxHp` を条件にしているので、**満タンの味方は候補に入らない**。");
        Console.WriteLine("観測は `相手なし`（`NoPatient`）の列——第1ターンは全員満タンなので必ず立つ。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 前借り回数 | 相手なし | 増えたHP(=負債) | 負債 ÷ 発火（満タンに撃てば 0 に寄る） |");
        Console.WriteLine("|---|---:|---:|---:|---:|");
        for (int i = 0; i < rigs.Length - 1; i++)
        {
            TlAcc a = Measure(rigs[i].F, IndulgenceRule.Default);
            double b = a.Battles;
            Console.WriteLine("| " + rigs[i].Name + " | " + (a.Fires / b).ToString("F2")
                + " | " + (a.NoPatient / b).ToString("F2") + " | " + (a.Stacked / b).ToString("F2")
                + " | " + (a.Fires == 0 ? "—" : ((double)a.Stacked / a.Fires).ToString("F2")) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## (f) 渇き下で負債が 0（`ctx.Heal` の入口で止まっている）");
        Console.WriteLine();
        Console.WriteLine("第三波（渇きの祭司）だけを回す。**祭司が生きている間**は1点も積まれないが、");
        Console.WriteLine("**祭司を割れば積まれる**ので戦全体では 0 にならない。読むのは `うち渇き` の側。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 前借り回数 | 空振り | **うち渇き** | 積んだ負債 | 取り立て |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|");
        for (int i = 0; i < rigs.Length - 1; i++)
        {
            TlAcc a = Measure(rigs[i].F, IndulgenceRule.Default, stFrom: 2, stTo: 3);
            double b = a.Battles;
            Console.WriteLine("| " + rigs[i].Name + " | " + (a.Fires / b).ToString("F2")
                + " | " + (a.Dry / b).ToString("F2") + " | **" + (a.DryDrought / b).ToString("F2") + "**"
                + " | " + (a.Stacked / b).ToString("F2") + " | " + (a.TollTaken / b).ToString("F2") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## (g) `Toll` を外した版で `Brand` の燃料が 0（下流であることの確認）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 取立(実HP) | 焼き回数 | 焼き(実HP) |");
        Console.WriteLine("|---|---|---:|---:|---:|");
        for (int i = 0; i < rigs.Length - 1; i++)
            foreach ((string tag, UnitDef aga, IndulgenceRule rule) in Versions())
            {
                if (tag.StartsWith("V0")) continue;
                TlAcc a = Measure(Rig(i, aga, FillerOf[i].Atk, FillerOf[i].Hp).F, rule);
                double b = a.Battles;
                Console.WriteLine("| " + rigs[i].Name + " | " + tag + " | " + (a.TollTaken / b).ToString("F2")
                    + " | " + (a.BrandFires / b).ToString("F2") + " | " + (a.BrandRemoved / b).ToString("F2") + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## (h) `StatusKeys.All` に負債が入っている（会戦の境界で消える）");
        Console.WriteLine();
        Console.WriteLine("- `StatusKeys.All`: "
                          + (StatusKeys.All.Contains(StatusKeys.Debt) ? "**入っている**" : "**入っていない。止める**")
                          + "（" + StatusKeys.All.Length + " 本）");
        {
            // 会戦を2戦またぐ。境界が消していなければ2戦目は「積んでいない負債を取り立てる」ので、
            // 収支（積んだ ＝ 取立 ＋ 踏み倒し ＋ 未回収）が必ず破れる。
            var squads = new[] { rigs[0].F, rigs[1].F };
            var foes = EnemyCatalog.Stages.Skip(1).Take(3).Select(x => x.Enemy).ToList();
            int battles = 0, bad = 0;
            for (int seed = 0; seed < 40; seed++)
            {
                EngagementResult er = EngagementEngine.Run(squads, foes, seed, verbose: false);
                foreach (BattleResult r in er.Battles) { battles++; if (!r.Indulgence.Balanced) bad++; }
            }
            Console.WriteLine("- 会戦 40 試行の部隊戦 **" + battles + "** 戦で、収支が破れた戦: **" + bad + "**"
                              + (bad == 0 ? "（**境界で消えている**）" : "（**止める**）"));
        }
        Console.WriteLine();

        Console.WriteLine("## (i) 触っていないノブの既定が1つも動いていない");
        Console.WriteLine();
        Console.WriteLine("`docs/rules.md` の差分で示す（**既定値の列だけ**を比べること——第131期）。");
        Console.WriteLine("この期に足したノブは `IndulgenceRule` の1本だけ（既定 " + IndulgenceRule.Default + "）。");
        Console.WriteLine();

        Console.WriteLine("## (j) アガ非在席で版が1ビットも違わない");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Versions().Select(v => v.Tag)) + " | 差 |");
        Console.WriteLine("|---|" + string.Concat(Versions().Select(_ => "---|")) + "---|");
        {
            Formation f = rigs[^1].F;   // 台5（陰性対照）
            var ws = Versions().Select(v => Measure(f, v.Rule).WaveWin).ToList();
            bool same = ws.All(w => Enumerable.Range(1, 4).All(st => w[st] == ws[0][st]));
            Console.WriteLine("| " + rigs[^1].Name + " | "
                + string.Join(" | ", ws.Select(w => string.Join(" / ", w.Skip(1).Select(x => x.ToString("F1")))))
                + " | " + (same ? "**0 件**" : "**差あり。止める**") + " |");
        }
    }

    static (double[] Win, double Turns) Waves(Formation f)
    {
        var w = new double[5];
        long turns = 0; int battles = 0;
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                if (r.PlayerWon) wins++;
                if (st >= 1) { turns += r.Turns; battles++; }
            }
            w[st] = 100.0 * wins / Seeds;
        }
        return (w, battles == 0 ? 0 : (double)turns / battles);
    }

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

    static string? FindDir(string dirName)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            string p = Path.Combine(dir.FullName, dirName);
            if (Directory.Exists(p)) return p;
        }
        return null;
    }
}
