using BattleCore;
using static Common;

// =====================================================================================
// confuse モード（第146期） —— 混乱（動かされた駒は次の攻撃を自軍へ向ける）
//
// 指示書は design/PHASE146_CONFUSION_SPEC.md ／ 報告は design/PHASE146_CONFUSION.md。
//
//     dotnet run --project BattleSim -c Release 0 confuse phase0   # Phase 0 の答えを実装から引き直す（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 confuse scan     # 台の下見（V0 の第2〜5波平均が 40〜95% か）
//     dotnet run --project BattleSim -c Release 0 confuse run      # 段B: V0 対 V1 × 台4つ × 波
//     dotnet run --project BattleSim -c Release 0 confuse check    # 自己検査
// =====================================================================================

static class ConfuseDiag
{
    const int Seeds = 200;

    static readonly ConfusionRule V0 = ConfusionRule.Default;   // 掛けない（回帰の基準）
    static readonly ConfusionRule V1 = ConfusionRule.On;        // 両陣営に掛ける

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "scan": if (arg.StartsWith("probe")) Probe(); else Scan(); return;
            case "run": Sweep(); return;
            case "dir": Direction(); return;
            case "half": Half(); return;
            case "compare": CompareRows(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("confuse: モードは phase0 / scan / run / check。");
                return;
        }
    }

    // =================================================================================
    // 台
    // =================================================================================

    /// <summary>
    /// 埋め草は<b>特性を1つも持たない素体</b>（第143期の則）。
    /// <b>敵の数値に触る駒（呪詛官ネル・萎縮のクビ）を入れると、掃引点がそのまま埋め草の性能になる。</b>
    /// </summary>
    static UnitDef Plain(string id, int hp, int atk, int spd = 8) => new()
    {
        Id = id, Name = "素体" + id, MaxHp = hp, Attack = atk, Speed = spd,
        Advances = true, Traits = Array.Empty<TraitId>()
    };

    /// <summary>
    /// 埋め草の強さ（<c>scan probe</c> が振る）。<b>既定は下の掃引で選んだ値。</b>
    /// <b>台の V0 が床（第2〜5波平均 40% 未満）だと勝率の列が1ビットも情報を持たない</b>
    /// ——第133期の台1（V0 0.0%）と同じ轍。<b>台を作る条件に「V0 が 40〜95%」を入れること。</b>
    /// </summary>
    /// <para><b>採用値は HP 110 / 攻 34</b>（`scan probe` の 12 点のうち <b>4台とも帯に入る2点</b>
    /// ——(90, 34) と (110, 34)。情報セルはどちらも 14/16 だが、**200 seed で測り直すと (90, 34) の台4 が 39.2% で床を割る**ので大きいほうを採った）。
    /// <b>特性は1つも持たない</b>ので、敵の数値にも味方の通貨にも触らない（第143期の則）。</para>
    static int _fillHp = 110, _fillAtk = 34;

    static UnitDef Fill(string id) => Plain(id, _fillHp, _fillAtk);

    /// <summary>
    /// ローカル台。<b><c>Presets</c> には置かない</b>（<c>compare</c> 61行を汚さない）。
    ///
    /// <para><b>供給は喧噪のバサに寄せてある</b>——毎ターン敵2体と味方2体を入れ替えるので、
    /// <b>両陣営に混乱を立てられる唯一の札</b>（Phase 0 Q0-6）。台4 だけがバサを持たない。</para>
    ///
    /// <para><b>台4 の「発火 0」は第1〜4波でしか成り立たない。</b> 第五波には告発人（曝き）がいて、
    /// <b>敵が味方を引きずり出す</b>ので、動かす機構を1枚も入れなくても混乱が立つ
    /// ——これが「両陣営に等しくかかる」は名目だけである、の実証になる。</para>
    /// </summary>
    static (string Name, Formation F)[] Rigs() => new (string, Formation)[]
    {
        // 被弾変換が厚い。ガルド（庇い100%・受け流し）／ムド（怒り）／ドハ（分かち）。
        // **混乱が代金になるか**を読む台（指示書 §3 予測1 の負の側）。
        ("被弾変換が厚い", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Mudo, center: UnitCatalog.Doha,
            back1: UnitCatalog.Basa, back3: Fill("pa"))),

        // 打点だけ。被弾を資産に変える札が1枚も無いので、**混乱が純粋な得になるか**を読む
        // （指示書 §3 予測1 の正の側）。台4 とはバサの有無だけが違う。
        ("被弾変換が薄い", Formation.Build(
            front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Kiri,
            back1: UnitCatalog.Basa, back3: Fill("pa"))),

        // ササ（身構え・上限7で切って切り落としを破片へ）＋ ガレ（礫・破片を出口へ）。
        // **混乱の一撃が供給に化けるか**（指示書 §3 予測2）。
        ("ササ同席", Formation.Build(
            front1: UnitCatalog.Sasa, front3: UnitCatalog.Gare, center: UnitCatalog.Borg,
            back1: UnitCatalog.Basa, back3: Fill("pa"))),

        // 動かす機構なし（陰性対照）。**台2 のバサを同数値・特性なしの素体に落としただけ**
        // （HP56 / 攻7 / 速8 ＝ 喧噪のバサと1つも違わない）。差は札 `Shuffler` の1枚きりなので、
        // **「混乱が立たない」以外の理由で台2 と差が出ない**（第39・61期の同数値対照）。
        ("動かす機構なし", Formation.Build(
            front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Kiri,
            back1: Plain("pb", 56, 7), back3: Fill("pa"))),
    };

    // =================================================================================
    // phase0 —— Phase 0 の答えを**実装から引き直す**（戦闘0回）
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第146期 `confuse phase0` —— 陣営の前提を実装から引き直す（戦闘0回）");
        Console.WriteLine();

        string? eng = FindSource("BattleEngine.cs"), tr = FindSource("Traits.cs");
        if (eng is null || tr is null)
        {
            Console.WriteLine("**`BattleCore` のソースが引けない。止める**（第117期）。");
            return;
        }
        string[] E = File.ReadAllLines(eng), T = File.ReadAllLines(tr);

        Console.WriteLine("## Q0-1. `Opponent(` の全数と、標的集合を作る箇所");
        Console.WriteLine();
        Console.WriteLine("- `BattleEngine.cs` **" + E.Count(l => l.Contains("Opponent(")) + "** 行 ／ `Traits.cs` **"
                          + T.Count(l => l.Contains("Opponent(")) + "** 行");
        Console.WriteLine("- そのうち標的集合を作るのは **`FoesOf` の1箇所だけ**"
                          + "（第146期に寄せた。`SelectTargetChain` / `TargetPool` / `SecondaryTargets` が共有）");
        Console.WriteLine("- `FoesOf` の呼び出し: **" + (E.Count(l => l.Contains("FoesOf(")) - 1)
                          + "** 箇所（定義を除く。**3** なら3つとも通っている）");
        Console.WriteLine("- `ResolvePierce` が `entry.TeamId` を見る: "
                          + (E.Any(l => l.Contains("LaneOccupants(LivingMembers(entry.TeamId)"))
                             ? "**○**（自動で追従する）" : "**×**"));
        Console.WriteLine();

        Console.WriteLine("## Q0-2 / Q0-3. `source` の陣営を見て降りる札");
        Console.WriteLine();
        Console.WriteLine("| 行 | クラス | 条件 |");
        Console.WriteLine("|--:|---|---|");
        string cls = "";
        for (int i = 0; i < T.Length; i++)
        {
            if (T[i].StartsWith("public sealed class ") && T[i].Contains("Trait"))
                cls = T[i].Substring("public sealed class ".Length).Split(' ')[0];
            if (T[i].Contains("source.TeamId == self.TeamId") && T[i].Contains("return"))
                Console.WriteLine("| " + (i + 1) + " | `" + cls + "` | `" + T[i].Trim() + "` |");
        }
        Console.WriteLine();
        Console.WriteLine("**これ以外の被弾変換（怒り・分かち・後備え・集約・鱗・身構え・庇いの見返り）は陣営を見ない。**");
        Console.WriteLine();
        int parry = Array.FindIndex(E, l => l.Contains("Parry.Uses > 0 && source is not null"));
        if (parry >= 0)
        {
            Console.WriteLine("- 受け流し（`BattleEngine.cs:" + (parry + 1) + "`）: `" + E[parry].Trim() + "`");
            Console.WriteLine("  → **味方からの一撃は弾かない。** 指示書 §0 の表の1行目は偽（報告書 §Q0-2）。");
        }
        else Console.WriteLine("- **受け流しの条件が引けない。止める**（第117期）。");
        Console.WriteLine();

        Console.WriteLine("## Q0-6. `SwapSlots` の呼び出し元（＝混乱の供給）");
        Console.WriteLine();
        cls = "";
        Console.WriteLine("| 行 | クラス |");
        Console.WriteLine("|--:|---|");
        for (int i = 0; i < T.Length; i++)
        {
            if (T[i].StartsWith("public sealed class ") && T[i].Contains("Trait"))
                cls = T[i].Substring("public sealed class ".Length).Split(' ')[0];
            if (T[i].Contains("ctx.SwapSlots(")) Console.WriteLine("| " + (i + 1) + " | `" + cls + "` |");
        }
        Console.WriteLine();
        Console.WriteLine("**庇いの前出は1件も出ない**——鎖は主目標を差し替えるだけで席を1ミリも動かさない（第124期 Q0-7）。");
        Console.WriteLine("**弾きの共有ヘルパは散開と身構えの2者が使う**ので、箇所は 6・札は 7・");
        Console.WriteLine("保持者のいる札は 6（散開は第143期の転生で保持者 0 枚になった）。");
        Console.WriteLine();

        Console.WriteLine("## 敵側の供給（波ごと）");
        Console.WriteLine();
        TraitId[] movers =
        {
            TraitId.Coward, TraitId.ThornGuard, TraitId.Expose,
            TraitId.Shove, TraitId.Shuffler, TraitId.Loose, TraitId.Brace
        };
        Console.WriteLine("| 波 | 移動を起こす敵 |");
        Console.WriteLine("|---|---|");
        for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
        {
            var found = EnemyCatalog.Stages[st].Enemy.Occupied()
                .Where(o => o.Def.Traits.Any(t => movers.Contains(t)))
                .Select(o => o.Def.Name + "（"
                             + string.Join("/", o.Def.Traits.Where(t => movers.Contains(t))) + "）")
                .ToList();
            Console.WriteLine("| " + EnemyCatalog.Stages[st].Name + " | "
                              + (found.Count == 0 ? "**0 枚**" : string.Join(" ／ ", found)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**第五波の告発人（曝き）だけで、しかも動かすのは<u>味方</u>。**");
        Console.WriteLine("敵の駒が動く経路は、プレイヤーが ハネ／バサ を連れてきたときしか存在しない");
        Console.WriteLine("——第134期の「**在庫**」型の非対称（窓口は両陣営に開いているが、片側が一度も問い合わせない）。");
    }

    // =================================================================================
    // scan —— 台の下見（版を1つも振らない）
    // =================================================================================

    static void Scan()
    {
        Console.WriteLine("# 第146期 `confuse scan` —— 台の下見（V0 だけ。版は振らない）");
        Console.WriteLine();
        Console.WriteLine("**採る条件は第61・63期の帯: 第2〜5波平均が 40〜95%。** 情報セルは第2〜5波で `0 < x < 100`。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波 | 情報セル | 帯 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|:-:|");
        foreach ((string name, Formation f) in Rigs())
        {
            double[] w = Rates(f, V0);
            double avg = w.Skip(1).Average();
            int info = w.Skip(1).Count(x => x > 0.0 && x < 100.0);
            Console.WriteLine("| " + name
                              + " | " + w[0].ToString("F1") + " | " + w[1].ToString("F1")
                              + " | " + w[2].ToString("F1") + " | " + w[3].ToString("F1")
                              + " | " + w[4].ToString("F1")
                              + " | **" + avg.ToString("F1") + "** | " + info + "/4 | "
                              + (avg >= 40 && avg <= 95 ? "○" : "**×**") + " |");
        }
    }

    /// <summary>
    /// 埋め草の強さを振って、<b>4台とも帯（40〜95%）に入る組</b>を探す（第138期 `gscan probe` と同型）。
    /// <b>版は1つも振らない</b>——探しているのは台であって効き目ではない。
    /// </summary>
    static void Probe()
    {
        Console.WriteLine("# 第146期 `confuse scan probe` —— 埋め草の強さを振る（V0 だけ）");
        Console.WriteLine();
        Console.WriteLine("**4台とも第2〜5波平均が 40〜95% に入る組を探す。** 情報セルは第2〜5波で `0 < x < 100`。");
        Console.WriteLine();
        Console.WriteLine("| HP | 攻 | 厚い | 薄い | ササ | 機構なし | 帯に入る台 | 情報セル合計 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|");
        int savedHp = _fillHp, savedAtk = _fillAtk;
        foreach (int hp in new[] { 70, 90, 110 })
            foreach (int atk in new[] { 10, 18, 26, 34 })
            {
                _fillHp = hp; _fillAtk = atk;
                var avg = new List<double>();
                int info = 0;
                foreach ((string _, Formation f) in Rigs())
                {
                    double[] w = Rates(f, V0, 100);
                    avg.Add(w.Skip(1).Average());
                    info += w.Skip(1).Count(x => x > 0.0 && x < 100.0);
                }
                int inBand = avg.Count(a => a >= 40 && a <= 95);
                Console.WriteLine("| " + hp + " | " + atk + " | "
                                  + string.Join(" | ", avg.Select(a => a.ToString("F1")))
                                  + " | **" + inBand + "/4** | " + info + "/16 |");
            }
        _fillHp = savedHp; _fillAtk = savedAtk;
    }

    // =================================================================================
    // run —— 段B: V0 対 V1
    // =================================================================================

    static void Sweep()
    {
        Console.WriteLine("# 第146期 `confuse run` —— 段B: 混乱を掛ける（V0 対 V1）");
        Console.WriteLine();
        Console.WriteLine("**波ごとの効き方は生の pt で比べない**（第144期）——同じ波でも V0 の位置で");
        Console.WriteLine("動ける幅が違う。`余地に対する取り分` ＝ Δ ÷ (上がったなら 100 − V0 ／ 下がったなら V0)。");
        Console.WriteLine();
        Console.WriteLine("## 表A. 勝率（V1 − V0）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | V0 | V1 | Δ | 余地に対する取り分 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in Rigs())
        {
            double[] a = Rates(f, V0), b = Rates(f, V1);
            for (int st = 1; st < 5; st++)
            {
                double room = b[st] >= a[st] ? 100.0 - a[st] : a[st];
                string share = room <= 0.001 ? "—" : ((b[st] - a[st]) / room).ToString("+0.000;-0.000");
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | "
                                  + a[st].ToString("F1") + " | " + b[st].ToString("F1") + " | "
                                  + (b[st] - a[st]).ToString("+0.0;-0.0") + " | " + share + " |");
            }
            Console.WriteLine("| **" + name + "** | **第2〜5波** | **" + a.Skip(1).Average().ToString("F1")
                              + "** | **" + b.Skip(1).Average().ToString("F1") + "** | **"
                              + (b.Skip(1).Average() - a.Skip(1).Average()).ToString("+0.0;-0.0") + "** | |");
        }

        Console.WriteLine();
        Console.WriteLine("## 表B. 混乱の帳簿（V1・回/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 味方に立った | 味方が振った | 敵に立った | 敵が振った |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in Rigs())
            for (int st = 1; st < 5; st++)
            {
                (double am, double asw, double fm, double fsw) = Ledger(f, st, V1, Seeds);
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | "
                                  + am.ToString("F2") + " | " + asw.ToString("F2") + " | "
                                  + fm.ToString("F2") + " | " + fsw.ToString("F2") + " |");
            }
    }

    // =================================================================================
    // dir —— 供給の**向き**を分ける（段B の × の原因を割る）
    // =================================================================================

    /// <summary>
    /// <b>段B は4台とも下がった。その原因が「混乱そのもの」か「供給が両陣営に等量だから」かを割る。</b>
    ///
    /// <para><b>向きを振る器具は `ShufflerRule` にある</b>——<c>Legacy</c>（<c>Foes = false</c>）は
    /// <b>味方だけ</b>を乱し、<c>(true, None)</c> は<b>両陣営</b>を乱す。差はどの陣営を乱すかの1点だけで、
    /// <b>転倒は両方 `None` に固定してある</b>（第144期の転倒が混ざると混乱以外の変数が増える）。</para>
    ///
    /// <para><b>「敵だけ」は既存の駒では作れない。</b> ハネ（突き返し）は <c>OnMoved</c> /
    /// <c>OnAllyMoved</c> しか持たず<b>自分から移動を起こさない</b>ので（第41期の設計）、
    /// 供給の無い台では 1 回も発火しない——実測で第2〜4波の発火は <b>0.00</b> だった。
    /// セロ（逃亡）も <c>Row.Back</c> で即 return するので<b>後列に置くと 0 回</b>。
    /// <b>どちらも第117期の穴（土台が測る対象の入力を 0 にする）である。</b>
    /// そこで<b>診断のローカルに曝き（<c>TraitId.Expose</c>）を持つ同数値の駒を置く</b>
    /// ——曝きは <c>Opponent(self.TeamId)</c> を引きずり出すので、<b>味方が持てば敵だけが動く</b>
    /// （第118期の糧タンクと同じ「診断のローカルだけが持つ駒」の扱い）。</para>
    /// </summary>
    static void Direction()
    {
        Console.WriteLine("# 第146期 `confuse dir` —— 供給の向きを分ける");
        Console.WriteLine();
        Console.WriteLine("**土台は共通**（ドルガ 前1 ／ ボルグ 前3 ／ キリ 中央 ／ 素体 後3）で、**後1 の1枚だけ**を振る。");
        Console.WriteLine("**転倒は全版 `None` に固定**（第144期の転倒が混ざると混乱以外の変数が増える）。");
        Console.WriteLine("帰属 ＝ V1 − V0（同じ土台・同じ席なので、駒の素の強さは V0 に吸われる）。");
        Console.WriteLine();

        var allyOnly = ShufflerRule.Legacy;                            // 味方だけ
        var bothTeams = new ShufflerRule(true, ShuffleStagger.None);    // 両陣営（転倒なし）

        // 診断のローカル。曝きは Opponent を引きずり出すので、**味方が持てば敵だけが動く**。
        // 数値は喧噪のバサと同一（HP56 / 攻7 / 速8）——差を札1枚に閉じるため。
        UnitDef exposer = new()
        {
            Id = "pex", Name = "曝きの素体", MaxHp = 56, Attack = 7, Speed = 8,
            Advances = true, Traits = new[] { TraitId.Expose }
        };

        (string Name, UnitDef Def, ShufflerRule Rule, string Dir)[] supply =
        {
            ("バサ（両陣営）", UnitCatalog.Basa, bothTeams, "敵2体 ＋ 味方2体"),
            ("バサ（味方だけ）", UnitCatalog.Basa, allyOnly, "味方2体"),
            ("曝きの素体（敵だけ）", exposer, allyOnly, "敵1体"),
            ("素体（供給なし）", Plain("pc", 56, 7), allyOnly, "なし"),
        };

        Console.WriteLine("| 供給 | 動かす相手 | 波 | V0 | V1 | 帰属 | 味方が振った | 敵が振った |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|");
        foreach ((string sname, UnitDef def, ShufflerRule rule, string dir) in supply)
        {
            Formation f = Formation.Build(
                front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg, center: UnitCatalog.Kiri,
                back1: def, back3: Fill("pa"));
            double[] a = Rates(f, V0, Seeds, rule), b = Rates(f, V1, Seeds, rule);
            for (int st = 1; st < 5; st++)
            {
                (double _, double asw, double _2, double fsw) = Ledger(f, st, V1, Seeds, rule);
                Console.WriteLine("| " + sname + " | " + dir + " | 第" + (st + 1) + "波 | "
                                  + a[st].ToString("F1") + " | " + b[st].ToString("F1") + " | "
                                  + (b[st] - a[st]).ToString("+0.0;-0.0") + " | "
                                  + asw.ToString("F2") + " | " + fsw.ToString("F2") + " |");
            }
            Console.WriteLine("| **" + sname + "** | | **第2〜5波** | **" + a.Skip(1).Average().ToString("F1")
                              + "** | **" + b.Skip(1).Average().ToString("F1") + "** | **"
                              + (b.Skip(1).Average() - a.Skip(1).Average()).ToString("+0.0;-0.0") + "** | | |");
        }
    }

    // =================================================================================
    // half —— 段C: 指示書 §5 が求めた「弱めた版」1点
    // =================================================================================

    /// <summary>
    /// 段B の版は<b>天井と床に張り付く</b>（敵だけ版の第4波 5.5 → 100.0 ／ 味方だけ版の第2波 92.5 → 0.0）。
    /// 指示書 §5 は<b>そのとき「50% の確率で」に弱めた版を1点だけ測って報告する（採用はしない）</b>と
    /// 書いているので、<c>ConfusionRule.Half</c> をここで測る。
    /// </summary>
    static void Half()
    {
        Console.WriteLine("# 第146期 `confuse half` —— 段C: 弱めた版（50%）を1点だけ");
        Console.WriteLine();
        Console.WriteLine("**採用はしない**（指示書 §5）。段B が天井と床に張り付いたので、その1点を記録するだけ。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | V0 | V1（100%） | V1h（50%） | 100% の帰属 | 50% の帰属 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach ((string name, Formation f) in Rigs())
        {
            double[] a = Rates(f, V0), b = Rates(f, V1), h = Rates(f, ConfusionRule.Half);
            for (int st = 1; st < 5; st++)
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | " + a[st].ToString("F1")
                                  + " | " + b[st].ToString("F1") + " | " + h[st].ToString("F1") + " | "
                                  + (b[st] - a[st]).ToString("+0.0;-0.0") + " | "
                                  + (h[st] - a[st]).ToString("+0.0;-0.0") + " |");
            Console.WriteLine("| **" + name + "** | **第2〜5波** | **" + a.Skip(1).Average().ToString("F1")
                              + "** | **" + b.Skip(1).Average().ToString("F1") + "** | **"
                              + h.Skip(1).Average().ToString("F1") + "** | **"
                              + (b.Skip(1).Average() - a.Skip(1).Average()).ToString("+0.0;-0.0") + "** | **"
                              + (h.Skip(1).Average() - a.Skip(1).Average()).ToString("+0.0;-0.0") + "** |");
        }
    }

    // =================================================================================
    // compare —— 拒否権（`compare` 61 行）
    // =================================================================================

    /// <summary>
    /// <b>選択肢 (a)（保持者を置かない）を採ったので、指示書 §4 の 3「この波を含まない行が ±0.0」は
    /// 到達不能になった</b>——ノブは全波に掛かるので「この波を含まない行」が存在しない
    /// （第107・110期の「判定式の線が別の節によって到達不能になる」の、選択の帰結としての版）。
    ///
    /// <para>代わりに読むのは <b>(i) 混乱が1件も立たない行が ±0.0 か</b> と
    /// <b>(ii) 拒否権（第五波平均の歯止め・いずれかの波で −10.0pt 以上）</b>。</para>
    /// </summary>
    static void CompareRows()
    {
        Console.WriteLine("# 第146期 `confuse compare` —— 拒否権（`compare` 61 行）");
        Console.WriteLine();
        Console.WriteLine("**選択肢 (a) を採ったので指示書 §4 の 3 は到達不能**——ノブは全波に掛かるので");
        Console.WriteLine("「この波を含まない行」が存在しない。代わりに「混乱が1件も立たない行が ±0.0 か」を読む。");
        Console.WriteLine();

        var rows = new List<(string Name, double[] A, double[] B, double Marks)>();
        foreach ((string name, Formation f) in Presets.Compare)
        {
            double[] a = Rates(f, V0, 50), b = Rates(f, V1, 50);
            double marks = 0;
            for (int st = 0; st < 5; st++)
            {
                (double am, double _1, double fm, double _2) = Ledger(f, st, V1, 20);
                marks += am + fm;
            }
            rows.Add((name, a, b, marks));
        }

        Console.WriteLine("## 表A. 混乱が1件も立たない行（陰性対照）");
        Console.WriteLine();
        var dead = rows.Where(r => r.Marks == 0).ToList();
        Console.WriteLine("- **" + dead.Count + " / " + rows.Count + " 行**で混乱が 0 件");
        int deadDiff = dead.Sum(r => Enumerable.Range(0, 5).Count(i => Math.Abs(r.A[i] - r.B[i]) > 0.001));
        Console.WriteLine("- そのうち動いたセル: **" + deadDiff + " 件**" + (deadDiff == 0 ? " ○" : " **×**"));
        Console.WriteLine();

        Console.WriteLine("## 表B. 全体（第2〜5波平均と第五波）");
        Console.WriteLine();
        Console.WriteLine("| | V0 | V1 | Δ |");
        Console.WriteLine("|---|--:|--:|--:|");
        double a25 = rows.Average(r => r.A.Skip(1).Average()), b25 = rows.Average(r => r.B.Skip(1).Average());
        double a5 = rows.Average(r => r.A[4]), b5 = rows.Average(r => r.B[4]);
        Console.WriteLine("| 全61行・第2〜5波 | " + a25.ToString("F1") + " | " + b25.ToString("F1")
                          + " | " + (b25 - a25).ToString("+0.0;-0.0") + " |");
        Console.WriteLine("| 全61行・第五波 | " + a5.ToString("F1") + " | " + b5.ToString("F1")
                          + " | " + (b5 - a5).ToString("+0.0;-0.0") + " |");
        var pri = rows.Where(r => Baseline.PrimaryRows.Contains(r.Name)).ToList();
        double pa5 = pri.Average(r => r.A[4]), pb5 = pri.Average(r => r.B[4]);
        Console.WriteLine("| **主判定" + pri.Count + "行・第五波** | **" + pa5.ToString("F1") + "** | **"
                          + pb5.ToString("F1") + "** | **" + (pb5 - pa5).ToString("+0.0;-0.0") + "** |");
        Console.WriteLine();
        Console.WriteLine("**拒否権1（主判定19行の第五波平均が歯止め `Baseline.PrimaryFifthFloor` = "
                          + Baseline.PrimaryFifthFloor.ToString("F1") + "% を下回る）: "
                          + (pb5 < Baseline.PrimaryFifthFloor ? "**発動**" : "通る") + "**");
        Console.WriteLine();

        Console.WriteLine("## 表C. 拒否権3: いずれかの波で −10.0pt 以上落ちた行");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | V0 | V1 | Δ | 混乱/戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        int veto = 0;
        foreach ((string name, double[] a, double[] b, double marks) in rows)
            for (int st = 0; st < 5; st++)
                if (b[st] - a[st] <= -10.0)
                {
                    veto++;
                    Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | " + a[st].ToString("F1")
                                      + " | " + b[st].ToString("F1") + " | " + (b[st] - a[st]).ToString("+0.0;-0.0")
                                      + " | " + (marks / 5).ToString("F2") + " |");
                }
        Console.WriteLine();
        Console.WriteLine("- **" + veto + " セル**が −10.0pt 以上");
    }

    // =================================================================================
    // check —— 自己検査
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第146期 自己検査 —— `confuse check`");
        Console.WriteLine();

        var def = Presets.Compare.ToDictionary(b => b.Name, b => Rates(b.F, null));

        string bal = arg.Length > 0 ? arg : "docs/balance.md";
        Console.WriteLine("## (a) 必須1: `compare` 305 セルが `" + bal + "` と 0 件（既定の `ConfusionRule`）");
        Console.WriteLine();
        if (!File.Exists(bal)) Console.WriteLine("**比較先が見つからない。手で `compare` を回して突き合わせること。**");
        else
        {
            int diff = 0, cells = 0;
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
            foreach ((string name, Formation f) in Presets.Compare)
            {
                if (!want.TryGetValue(name, out double[]? w)) { Console.WriteLine("- 行が引けない: " + name); continue; }
                double[] got = def[name];
                for (int i = 0; i < 5; i++) { cells++; if (Math.Abs(got[i] - w[i]) > 0.001) diff++; }
            }
            Console.WriteLine("- " + cells + " セル中 **" + diff + " 件**の差分" + (diff == 0 ? " ○" : " **×**"));
        }

        Console.WriteLine();
        Console.WriteLine("## (b) `ConfusionRule.Default` と既定が 305 セル 0 件（**既定が現行と同値か**）");
        Console.WriteLine();
        {
            int diff = 0;
            foreach ((string name, Formation f) in Presets.Compare)
            {
                double[] a = def[name], c = Rates(f, V0);
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - c[i]) > 0.001) diff++;
            }
            Console.WriteLine("- 305 セル中 **" + diff + " 件**" + (diff == 0 ? " ○" : " **×**"));
        }

        Console.WriteLine();
        Console.WriteLine("## (c) `Active = false` では混乱が1件も立たない（**計数の検算**）");
        Console.WriteLine();
        {
            double m = 0, sw = 0;
            foreach ((string _, Formation f) in Rigs())
                for (int st = 0; st < 5; st++)
                {
                    (double a1, double a2, double f1, double f2) = Ledger(f, st, V0, 50);
                    m += a1 + f1; sw += a2 + f2;
                }
            Console.WriteLine("- 立った **" + m.ToString("F0") + "** ／ 振った **" + sw.ToString("F0") + "**"
                              + (m == 0 && sw == 0 ? " ○" : " **×**"));
        }

        Console.WriteLine();
        Console.WriteLine("## (d) 指示書 §4 の 6: `振った ≦ 立った`（**1回の攻撃で必ず1つ落ちる**）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 立った | 振った | 差 | 判定 |");
        Console.WriteLine("|---|---|--:|--:|--:|:-:|");
        bool ok6 = true;
        foreach ((string name, Formation f) in Rigs())
            for (int st = 0; st < 5; st++)
            {
                (double am, double asw, double fm, double fsw) = Ledger(f, st, V1, 50);
                double m = am + fm, sw = asw + fsw;
                bool ok = sw <= m + 1e-9;
                if (!ok) ok6 = false;
                Console.WriteLine("| " + name + " | 第" + (st + 1) + "波 | " + m.ToString("F2")
                                  + " | " + sw.ToString("F2") + " | " + (m - sw).ToString("F2")
                                  + " | " + (ok ? "○" : "**×**") + " |");
            }
        Console.WriteLine();
        Console.WriteLine("- 全 20 セル" + (ok6 ? " ○" : " **×**"));
        Console.WriteLine();
        Console.WriteLine("**台4「動かす機構なし」の第五波だけは 0 にならない**——告発人（曝き）が味方を引きずり出すため。");
        Console.WriteLine("指示書 §3 の予測5「動かす機構なしの台で発火 0」は**第1〜4波でしか成り立たない**（報告書 §Q0-6）。");

        Console.WriteLine();
        Console.WriteLine("## (e) 必須4: `ctx.PickOne` を新たに使っていない");
        Console.WriteLine();
        string? eng = FindSource("BattleEngine.cs");
        if (eng is null) Console.WriteLine("**ソースが引けない。止める**（第117期）。");
        else
        {
            string[] E = File.ReadAllLines(eng);
            Console.WriteLine("- `BattleEngine.cs` の `PickOne(` は **" + E.Count(l => l.Contains("PickOne("))
                              + "** 行（第145期 HEAD と同数なら ○）");
            Console.WriteLine("- `FoesOf` は乱数を1つも引かない: "
                              + (E.Any(l => l.Contains("internal List<UnitState> FoesOf")) ? "**○**（定義を確認）" : "**×**"));
        }
    }

    // =================================================================================
    // 器具
    // =================================================================================

    static double[] Rates(Formation f, ConfusionRule? r, int seeds = Seeds, ShufflerRule? sh = null)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false,
                                     shuffler: sh, confusion: r).PlayerWon)
                    wins++;
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    /// <summary>
    /// 混乱の帳簿を陣営別に引く（回/戦）。
    /// <b><c>TallyByUnit</c> は駒 <c>Id</c> のキーなので、編成に載っている <c>Id</c> の集合で味方を判別する。</b>
    /// 台のどれも敵と同じ <c>Id</c> を持たないことが前提（素体の <c>Id</c> に `pa` / `pb` を使ってあるのはそのため）。
    /// </summary>
    static (double AllyMarks, double AllySwings, double FoeMarks, double FoeSwings)
        Ledger(Formation f, int st, ConfusionRule r, int seeds, ShufflerRule? sh = null)
    {
        var own = new HashSet<string>(f.Occupied().Select(o => o.Def.Id));
        double am = 0, asw = 0, fm = 0, fsw = 0;
        for (int seed = 0; seed < seeds; seed++)
        {
            BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                verbose: false, shuffler: sh, confusion: r);
            foreach (var kv in res.TallyByUnit)
            {
                if (own.Contains(kv.Key)) { am += kv.Value.ConfusedMarks; asw += kv.Value.ConfusedSwings; }
                else { fm += kv.Value.ConfusedMarks; fsw += kv.Value.ConfusedSwings; }
            }
        }
        return (am / seeds, asw / seeds, fm / seeds, fsw / seeds);
    }

    /// <summary>
    /// リポジトリ内のソースを探す。<b>引けなかったら呼び出し側で止めること</b>（第117期）
    /// ——実装から引く分類は、引けなかったときに「該当なし」と区別が付かない。
    /// </summary>
    static string? FindSource(string leaf)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 6 && dir is not null; i++, dir = dir.Parent)
        {
            string p = Path.Combine(dir.FullName, "BattleCore", leaf);
            if (File.Exists(p)) return p;
        }
        return null;
    }
}
