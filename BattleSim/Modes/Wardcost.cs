using BattleCore;
using static Common;

// =====================================================================================
// wardcost モード（第154期） —— 預かりの代金を付け替える（2枝を並べる）
//
// 指示書は design/PHASE154_WARDCOST_SPEC.md。第153期の `ward` は1行も触っていない
// （自己検査 (d) ＝「V1 が第153期の値を再現する」を厳密に取るため、台も版もあちらから引く）。
//
//     dotnet run --project BattleSim -c Release 0 wardcost phase0   # Q0-2 / Q0-4 / Q0-5（実装から引く）
//     dotnet run --project BattleSim -c Release 0 wardcost scan     # 台の下見（素体版が 40〜95% か）
//     dotnet run --project BattleSim -c Release 0 wardcost scan probe  # 埋め草の掃引（台1・台2 だけ）
//     dotnet run --project BattleSim -c Release 0 wardcost run      # 段B（V0/Vp/V1/V2/V3）
//     dotnet run --project BattleSim -c Release 0 wardcost sweep    # 段B' 強度の掃引
//     dotnet run --project BattleSim -c Release 0 wardcost band     # 段C（Percent と Threshold を同率で下げる）
//     dotnet run --project BattleSim -c Release 0 wardcost check    # 自己検査 (a)〜(h)
// =====================================================================================

static class WardcostDiag
{
    const int Seeds = 200;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "scan": if (arg.StartsWith("probe")) Probe(); else Scan(); return;
            case "run": Stage(arg); return;
            case "sweep": Sweep(); return;
            case "band": Band(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("wardcost: モードは phase0 / scan / run / sweep / band / check。");
                return;
        }
    }

    // =================================================================================
    // 台（Q0-3） —— **変換器の在席で割れる切り口**にする
    // =================================================================================

    /// <summary>
    /// ノチ（第153期の数値のまま）。<b>プラス側（<see cref="TraitId.Ward"/>）は1ビットも変えない。</b>
    /// 版は <c>Traits</c> の配列だけで作る——札が分けてあるので代金を差し替えるだけで済む。
    /// </summary>
    static UnitDef Nochi(params TraitId[] traits) => new()
    {
        Id = traits.Length == 0 ? "nochi_plain" : "nochi",
        Name = traits.Length == 0 ? "素体のノチ" : "後払いのノチ",
        MaxHp = WardDiag.NochiHp, Attack = WardDiag.NochiAtk, Speed = WardDiag.NochiSpd,
        Advances = false,
        Traits = traits
    };

    /// <summary>
    /// 台ごとの埋め草の強さ（攻, HP）。台3〜5 は第153期の点をそのまま引く。
    ///
    /// <para><b>選び方は第153期と同じ規則</b>——「帯（40〜95%）に入る点のうち情報セルが最大」。
    /// <b>同点のときは掃引の順（攻の昇順 → HP の昇順）で最初の点</b>を採る
    /// （後から見て選ばないため・第64期）。</para>
    ///
    /// <para><b>台2 は情報セルの最大が 2 である。</b> カドを含む台の第二波は
    /// 埋め草を 22/110 まで厚くするまで 0.0% に張り付く——<b>第27期の粛 × カド</b>で、
    /// 第152期 Q0-3 が「設計として許容する」と決めた既知の穴。
    /// <b>台2 の第二波は判定に使わない。</b></para>
    /// </summary>
    static readonly (int Atk, int Hp)[] FillerOf =
    {
        (18, 110),                // 台1 ササ（段0 の掃引で決めた）
        (10, 70),                 // 台2 カド（段0 の掃引で決めた）
        WardDiag.FillerOf[1],     // 台3 ＝ 第153期の台2（素体4枚＋ノチ）
        WardDiag.FillerOf[3],     // 台4 ＝ 第153期の台4（支援拒否）
        WardDiag.FillerOf[4],     // 台5 ＝ 第153期の台5（陰性対照）
    };

    /// <summary>
    /// ローカル台。<b><c>Presets</c> には置かない。</b> ノチは全台とも後1（第153期と同じ席）。
    ///
    /// <para><b>台1 と台2 の対比がこの期の器具の中心。</b> どちらも
    /// 「前列に立って殴られるが、自分では通常攻撃を1度も振らない駒」だが、
    /// <b>ササは <c>CurrentAttack</c> を1度も読まず、カドは棘の反撃量として毎回読む</b>
    /// ——指示書の素案「攻撃しない駒には <c>Laden</c> の払うものが無い」が
    /// 成り立つ側と成り立たない側を1組で持つ（Q0-2）。</para>
    /// </summary>
    static (string Name, Formation F) Rig(int i, UnitDef nochi, int atk, int hp)
    {
        UnitDef A() => WardDiag.Filler("a", hp, atk);
        UnitDef B() => WardDiag.Filler("b", hp, atk);
        UnitDef C() => WardDiag.Filler("c", hp, atk);
        return i switch
        {
            // 台1: `Burden` の唯一の量比例変換器（切り落とした超過が隣の味方の破片になる）。
            //      **ササは攻撃を1度も振らず、`CurrentAttack` も1度も読まない。**
            0 => ("台1 身構え (ノチ×ササ)",
                Formation.Build(front1: UnitCatalog.Sasa, front3: A(),
                                center: B(), back1: nochi, back3: C())),

            // 台2: 攻撃しない前列。**ただし棘の反撃量は `CurrentAttack` で決まる。**
            1 => ("台2 棘鎧 (ノチ×カド)",
                Formation.Build(front1: UnitCatalog.Kado, front3: A(),
                                center: B(), back1: nochi, back3: C())),

            // 台3〜5 は第153期の台をそのまま引く（期をまたぐ比較点）。
            2 => ("台3 変換器なし (ノチ単独)", WardDiag.Rig(1, nochi, atk, hp).F),
            3 => ("台4 支援拒否 (ノチ×ガルド)", WardDiag.Rig(3, nochi, atk, hp).F),
            _ => ("台5 陰性対照 (ノチ非在席)", WardDiag.Rig(4, nochi, atk, hp).F),
        };
    }

    static (string Name, Formation F)[] Rigs(UnitDef nochi)
    {
        var r = new (string, Formation)[5];
        for (int i = 0; i < 5; i++) r[i] = Rig(i, nochi, FillerOf[i].Atk, FillerOf[i].Hp);
        return r;
    }

    // =================================================================================
    // 版（段B）
    // =================================================================================

    /// <summary>
    /// 5版。<b>代金の札だけが変数で、プラス側は5版とも同一。</b>
    /// <c>Vp</c>（預かりのみ）が<b>プラス側の上限</b>で、代金の値段は <c>Vn − Vp</c> で取る（第153期と同じ器具）。
    /// </summary>
    static (string Tag, UnitDef Nochi, WardRule Rule)[] Versions(WardRule b) => new[]
    {
        ("V0 素体",       Nochi(),                              b),
        ("Vp 預かりのみ", Nochi(TraitId.Ward),                  b),
        ("V1 没収",       Nochi(TraitId.Ward, TraitId.Forfeit), b with { Cost = WardCost.Forfeit }),
        ("V2 荷",         Nochi(TraitId.Ward, TraitId.Burden),  b with { Cost = WardCost.Burden }),
        ("V3 重り",       Nochi(TraitId.Ward, TraitId.Laden),   b with { Cost = WardCost.Laden }),
    };

    sealed class Acc
    {
        public long Battles, Wins, Turns, Life, Stacked, Released, Forfeited, Residual;
        public long Bursts, Drips, Dry, Forfeits, ForfeitHealed, Unbalanced;
        public long BurdenHits, BurdenAdded, LadenSwings, LadenFloored, LadenLost, LadenNominal, LadenCarriers;
        public long BraceRefused, BraceGiven, Damage;
        public readonly Dictionary<string, (long Burden, long Laden, long Refused, long Out)> By = new();
        public double[] WaveWin = new double[5];
    }

    static Acc Measure(Formation f, WardRule rule, int stFrom = 1, int stTo = 5)
    {
        var a = new Acc();
        for (int st = stFrom; st < stTo; st++)
        {
            long w = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, ward: rule);
                a.Battles++; a.Turns += r.Turns;
                if (r.PlayerWon) { a.Wins++; w++; }
                foreach (var o in f.Occupied())
                    if (r.TallyByUnit.TryGetValue(o.Def.Id, out UnitTally? t))
                    {
                        a.Life += t.LastActiveTurn;
                        a.BraceRefused += t.BraceRefused;
                        a.BraceGiven += t.BraceGiven;
                        a.Damage += t.DamageTaken;
                        a.By.TryGetValue(o.Def.Id, out var c);
                        a.By[o.Def.Id] = (c.Burden + t.WardBurdenTaken, c.Laden + t.WardLadenLost,
                                          c.Refused + t.BraceRefused,
                                          c.Out + t.DmgOutInTurn + t.DmgOutOffTurn);
                    }
                WardLedger L = r.Ward;
                a.Stacked += L.Stacked; a.Released += L.Released; a.Forfeited += L.Forfeited;
                a.Residual += L.Residual; a.Bursts += L.Bursts; a.Drips += L.Drips; a.Dry += L.Dry;
                a.Forfeits += L.Forfeits; a.ForfeitHealed += L.ForfeitHealed;
                a.BurdenHits += L.BurdenHits; a.BurdenAdded += L.BurdenAdded;
                a.LadenSwings += L.LadenSwings; a.LadenFloored += L.LadenFloored;
                a.LadenLost += L.LadenSwingLost; a.LadenNominal += L.LadenNominal;
                a.LadenCarriers += L.LadenCarriers;
                if (!L.Balanced) a.Unbalanced++;
            }
            a.WaveWin[st] = 100.0 * w / Seeds;
        }
        return a;
    }

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第154期 `wardcost phase0` —— 前提を実装から引き直す");
        Console.WriteLine();
        string? tr = FindSource("BattleCore", "Traits.cs");
        string? eng = FindSource("BattleCore", "BattleEngine.cs");
        if (tr is null || eng is null)
        {
            Console.WriteLine("**`BattleCore` のソースが引けない。止める**（第117期）。");
            return;
        }
        string[] T = File.ReadAllLines(tr), E = File.ReadAllLines(eng);

        Q02a(T, E);
        Console.WriteLine();
        Q02b(T);
        Console.WriteLine();
        Q04(E);
        Console.WriteLine();
        Q05();
        Console.WriteLine();
        Scan();
    }

    /// <summary>クラス宣言ごとに本文を切る（第130期の則: `record struct` も宣言として数える）。</summary>
    static Dictionary<string, List<string>> Bodies(string[] lines)
    {
        var map = new Dictionary<string, List<string>>();
        string cls = "(宣言の外)";
        foreach (string line in lines)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                line, @"^\s*(?:public |internal |)(?:abstract |sealed |readonly |static |partial )*(?:record struct|record class|record|struct|class|interface)\s+(\w+)");
            if (m.Success) cls = m.Groups[1].Value;
            if (!map.TryGetValue(cls, out var l)) map[cls] = l = new List<string>();
            l.Add(line);
        }
        return map;
    }

    /// <summary>Q0-2(a) —— `Burden`（被ダメ増）を実利に変える駒を実装から数え直す。</summary>
    static void Q02a(string[] T, string[] E)
    {
        Console.WriteLine("## Q0-2(a) —— `Burden` の変換器（`Traits.cs` の走査）");
        Console.WriteLine();
        Console.WriteLine("**量に比例するか**（被ダメが2倍になったら得も2倍か）と");
        Console.WriteLine("**回数に比例するか**（`Burden` では被弾回数は1回も増えない）を分けて数える。");
        Console.WriteLine("判定は「被弾量の引数（`dmg` / `amount`）が算術に使われているか」。");
        Console.WriteLine();
        Console.WriteLine("| 札（クラス） | 被弾の通知 | 量を算術に使う行 | 分類 |");
        Console.WriteLine("|---|---|---|---|");

        var bodies = Bodies(T);
        int prop = 0, cnt = 0;
        foreach (var kv in bodies.OrderBy(k => k.Key))
        {
            bool own = kv.Value.Any(l => l.Contains("override void OnDamaged("));
            bool ally = kv.Value.Any(l => l.Contains("override void OnAllyDamaged("));
            if (!own && !ally) continue;
            var uses = kv.Value.Where(l =>
                System.Text.RegularExpressions.Regex.IsMatch(l, @"(?<![\w.])(dmg|amount)\s*[*/+-]")
                || System.Text.RegularExpressions.Regex.IsMatch(l, @"[*/]\s*(dmg|amount)(?![\w])"))
                .Select(l => l.Trim())
                .Where(l => !l.StartsWith("//") && !l.StartsWith("///"))
                .ToList();
            bool proportional = uses.Count > 0;
            if (proportional) prop++; else cnt++;
            Console.WriteLine("| `" + kv.Key + "` | " + (own ? "`OnDamaged`" : "") + (own && ally ? " ＋ " : "")
                              + (ally ? "`OnAllyDamaged`" : "") + " | "
                              + (uses.Count == 0 ? "—" : "`" + string.Join("` / `", uses.Take(2)) + "`") + " | "
                              + (proportional ? "**量に比例**" : "回数だけ") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("合計 **" + (prop + cnt) + "** 枚（量に比例 **" + prop + "** ／ 回数だけ " + cnt + "）。");
        Console.WriteLine();
        Console.WriteLine("**engine 側の変換器**（駒ごとのフックには出てこない）:");
        Console.WriteLine();
        int braceLine = Array.FindIndex(E, l => l.Contains("int refused = amount - Brace.Cap;"));
        Console.WriteLine("- 身構え（`BraceTrait`・ササ）: `BattleEngine.cs:" + (braceLine + 1)
                          + "` の `int refused = amount - Brace.Cap;` ——**切り落とした超過がそのまま隣の味方の破片になる。**");
        Console.WriteLine("  **入口で増やした分は満額ここへ落ちる**（上限は出口の手前・荷は入口）。");
        int tgLine = Array.FindIndex(E, l => l.Contains("int overflow = amount - ThornGuardTrait.AbsorbCap;"));
        Console.WriteLine("- 棘守り（`ThornGuardTrait`・カド）: `BattleEngine.cs:" + (tgLine + 1)
                          + "` の `int overflow = amount - ThornGuardTrait.AbsorbCap;` ——**超過は庇った味方へ素のまま中継**。");
        Console.WriteLine("  量に比例はするが**行き先は味方**なので、増えた分は味方の HP で払われる（＝変換器ではない）。");
        Console.WriteLine();
        Console.WriteLine("**在席で数え直した実利**（`UnitCatalog.All` の 52 枚のうち、量に比例する変換器を持つ駒）:");
        Console.WriteLine();
        foreach (UnitDef u in UnitCatalog.All)
        {
            var hit = u.Traits.Where(t => t is TraitId.Brace or TraitId.Rage or TraitId.Shatter
                                            or TraitId.Guardian or TraitId.Hex).ToList();
            if (hit.Count == 0) continue;
            Console.WriteLine("- " + u.Name + "（`" + u.Id + "`）: " + string.Join(" / ", hit));
        }
    }

    /// <summary>Q0-2(b) —— `Laden`（攻撃力減）が無料になる駒。</summary>
    static void Q02b(string[] T)
    {
        Console.WriteLine("## Q0-2(b) —— `Laden` が無料になる駒（**通常攻撃を振らない ＋ `CurrentAttack` を読まない**）");
        Console.WriteLine();
        Console.WriteLine("**2条件の積である。** 「通常攻撃を振らない」だけでは足りない");
        Console.WriteLine("——棘・仇討ち・責め苦の反撃量は自分の `CurrentAttack` で決まる（`BattleEngine.cs` の明文）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 行動 | 札 | `CurrentAttack` を読む札 | `Laden` は無料か |");
        Console.WriteLine("|---|---|---|---|---|");

        var bodies = Bodies(T);
        int free = 0, paid = 0;
        foreach (UnitDef u in UnitCatalog.All)
        {
            bool swings = u.Actions is null || u.Actions.Count == 0
                          || u.Actions.Any(a => a.Kind == ActionKind.Attack);
            if (swings) continue;
            var readers = new List<string>();
            foreach (TraitId id in u.Traits)
            {
                string cls = TraitCatalog.Resolve(new[] { id })[0].GetType().Name;
                if (bodies.TryGetValue(cls, out var b)
                    && b.Any(l => l.Contains("CurrentAttack") && !l.TrimStart().StartsWith("//")
                                  && !l.TrimStart().StartsWith("///")))
                    readers.Add(cls);
            }
            if (readers.Count == 0) free++; else paid++;
            Console.WriteLine("| " + u.Name + " | " + string.Join(" / ", u.Actions!.Select(a => a.Kind))
                              + " | " + string.Join(" / ", u.Traits)
                              + " | " + (readers.Count == 0 ? "**無し**" : "`" + string.Join("` / `", readers) + "`")
                              + " | " + (readers.Count == 0 ? "**○ 無料**" : "**× 払う**") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("通常攻撃を振らない駒は **" + (free + paid) + "** 枚／ 52 枚。");
        Console.WriteLine("そのうち **" + free + "** 枚が無料・**" + paid + "** 枚は払う。");
        Console.WriteLine();
        Console.WriteLine("**ガルドは別枠**——`ParrySwing.WhenStocked`（第152期）で在庫が残る手番は殴るので、");
        Console.WriteLine("`Actions` を持たない（＝通常攻撃を振る）側に数えてある。");
    }

    /// <summary>Q0-4 —— 荷が並ぶ入口の段と、既存の倍率との順序。</summary>
    static void Q04(string[] E)
    {
        Console.WriteLine("## Q0-4 —— `ApplyDamageBody` の段（荷をどこに置いたか）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 段 |");
        Console.WriteLine("|---:|---|");
        var marks = new (string Needle, string Label)[]
        {
            ("if (amount > ThornGuardTrait.AbsorbCap", "棘守りの肩代わり上限（素の入力で切る）"),
            ("amount = t.ModifyIncomingDamage(target, amount);", "**入口の族**（脆弱 ×1.5 など・`self` しか見ない札）"),
            ("amount += amount * HavocTrait.Percent / 100;", "惨禍 +50%（味方全体・engine 側）"),
            ("if (Ward.Cost == WardCost.Burden", "**荷（第154期）＝ここに置いた**"),
            ("int eased = amount * BulwarkTrait.ReductionPercent / 100;", "据え −50%"),
            ("amount -= amount * LooseTrait.ReductionPercent / 100;", "散開 −35%"),
            ("if (teammates.Any(u => u.HasTrait(TraitId.Cower)))", "萎縮 −30%"),
            ("int blocked = amount * Colossus.Percent / 100;", "巨躯の肩代わり"),
            ("if (!lethal) amount = Math.Min(amount, Math.Max(0, target.Hp - 1));", "「殺さない」クランプ"),
            ("if (Parry.Uses > 0 && source is not null", "受け流し（丸ごと無効化）"),
            ("if (Brace.Cap > 0 && amount > Brace.Cap", "**身構えの上限（切り落としが破片になる）**"),
            ("bool yokeBinding = amount > Yoke.Cap ? YokeBinding : false;", "軛（波の上限）"),
            ("target.Hp -= amount;", "HP を引く"),
        };
        foreach ((string needle, string label) in marks)
        {
            int ix = Array.FindIndex(E, l => l.Contains(needle));
            Console.WriteLine("| " + (ix + 1) + " | " + label + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**荷は惨禍の直後・身構えの上限より前**——入口で増やした分はそのまま");
        Console.WriteLine("切り落としになる。**出口に置くと切り落としが1点も増えず、測りたい変換が消える。**");
        Console.WriteLine("増幅は**加算**（惨禍と同じ `amount += amount * p / 100`）。");
    }

    /// <summary>Q0-5 —— 過去に同型を測っていないか。</summary>
    static void Q05()
    {
        Console.WriteLine("## Q0-5 —— 同型の前例（`design/` の走査）");
        Console.WriteLine();
        string? dir = FindSource("design", "PHASE153_WARD.md");
        if (dir is null) { Console.WriteLine("**`design/` が引けない。止める**（第117期）。"); return; }
        string root = Path.GetDirectoryName(dir)!;
        var files = Directory.GetFiles(root, "*.md");
        Console.WriteLine("`design/` の `.md`: **" + files.Length + "** ファイル");
        Console.WriteLine();
        Console.WriteLine("| 語 | 当たったファイル数 | 代表 |");
        Console.WriteLine("|---|---:|---|");
        foreach (string k in new[] { "被ダメージが増える", "被ダメージを増やす", "攻撃力が下がる", "萎縮", "脆弱", "惨禍" })
        {
            var hit = files.Where(f => File.ReadAllText(f).Contains(k)).ToList();
            Console.WriteLine("| " + k + " | " + hit.Count + " | "
                              + string.Join(" / ", hit.Take(3).Select(Path.GetFileName)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("**萎縮のクビ（`Cower`）が `Laden` と近い**——味方全体の攻撃 −"
                          + CowerTrait.AttackPenalty + " と被ダメ −" + CowerTrait.ReductionPercent + "%。");
        Console.WriteLine("**二重取りにはならない**——あちらは `ctx.Dull` で `AtkBonus` を一度だけ引く");
        Console.WriteLine("（第42期の窓口）。重りは `CurrentAttack` の**毎回の評価**で引くので、窓口が違う。");
        Console.WriteLine("同席すると `AtkBonus` が下がった上から更に引かれるが、**下限 1 で止まる。**");
    }

    // =================================================================================
    // 段0 —— 台の下見
    // =================================================================================

    static void Scan()
    {
        Console.WriteLine("## 段0 —— 台の下見（**素体のノチ**・seed 0.." + (Seeds - 1) + "）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 埋め草（攻/HP） | 勝率（1..5波） | 第2〜5波平均 | 帯（40〜95%） | 情報セル |");
        Console.WriteLine("|---|---|---|---:|---|---:|");
        var rigs = Rigs(Nochi());
        for (int i = 0; i < rigs.Length; i++)
        {
            double[] w = Waves(rigs[i].F);
            double m = (w[1] + w[2] + w[3] + w[4]) / 4;
            Console.WriteLine("| " + rigs[i].Name + " | " + FillerOf[i].Atk + " / " + FillerOf[i].Hp
                              + " | " + string.Join(" / ", w.Select(x => x.ToString("F1")))
                              + " | " + m.ToString("F1") + " | " + (m >= 40 && m <= 95 ? "○" : "**×**")
                              + " | " + w.Skip(1).Count(x => x > 0.0 && x < 100.0) + " |");
        }
    }

    static void Probe()
    {
        Console.WriteLine("## 段0 —— 埋め草の掃引（**素体のノチ**・台1 と台2 だけ。台3〜5 は第153期の点を引き継ぐ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 攻 | HP | 勝率（1..5波） | 第2〜5波平均 | 帯 | 情報セル |");
        Console.WriteLine("|---|---:|---:|---|---:|---|---:|");
        for (int i = 0; i < 2; i++)
            foreach (int atk in new[] { 10, 14, 18, 22, 26, 30 })
                foreach (int hp in new[] { 70, 90, 110, 130 })
                {
                    (string name, Formation f) = Rig(i, Nochi(), atk, hp);
                    double[] w = Waves(f);
                    double m = (w[1] + w[2] + w[3] + w[4]) / 4;
                    Console.WriteLine("| " + name + " | " + atk + " | " + hp + " | "
                                      + string.Join(" / ", w.Select(x => x.ToString("F1")))
                                      + " | " + m.ToString("F1") + " | " + (m >= 40 && m <= 95 ? "○" : "×")
                                      + " | " + w.Skip(1).Count(x => x > 0.0 && x < 100.0) + " |");
                }
    }

    // =================================================================================
    // 段B —— 3枝の比較
    // =================================================================================

    static void Stage(string arg)
    {
        WardRule b = Base(arg);
        Console.WriteLine("# 第154期 段B —— 代金の3枝（同じ台・同じ seed 0.." + (Seeds - 1) + "・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("基準の規則: `" + b + "`");
        Console.WriteLine();
        Console.WriteLine("`帰属` は素体差し替え比（版 − V0）。`取り分` は Δ ÷ (100 − V0)。");
        Console.WriteLine("**代金の値段は `Vn − Vp`**（Vp ＝ 代金の札を外した版 ＝ プラス側の上限）。");
        Console.WriteLine();

        var rigs = Rigs(Nochi(TraitId.Ward));
        for (int i = 0; i < rigs.Length; i++)
        {
            Console.WriteLine("## " + rigs[i].Name);
            Console.WriteLine();
            Console.WriteLine("| 版 | 勝率 2/3/4/5波 | 第2〜5波平均 | 帰属 | 取り分 | **代金** | 決着T | 味方生存T | 生存T÷決着T |");
            Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|");
            double baseM = 0, vpM = 0;
            var accs = new List<(string Tag, Acc A)>();
            foreach ((string tag, UnitDef nochi, WardRule rule) in Versions(b))
            {
                Formation f = Rig(i, nochi, FillerOf[i].Atk, FillerOf[i].Hp).F;
                Acc a = Measure(f, rule);
                accs.Add((tag, a));
                double m = 100.0 * a.Wins / a.Battles;
                double life = (double)a.Life / a.Battles / 5.0;
                double turns = (double)a.Turns / a.Battles;
                if (tag.StartsWith("V0")) baseM = m;
                if (tag.StartsWith("Vp")) vpM = m;
                string share = tag.StartsWith("V0") ? "—" : baseM >= 100.0 ? "—"
                    : ((m - baseM) / (100 - baseM)).ToString("F3");
                string cost = tag.StartsWith("V0") || tag.StartsWith("Vp") ? "—" : (m - vpM).ToString("+0.0;-0.0;0.0");
                Console.WriteLine("| " + tag + " | "
                    + string.Join(" / ", a.WaveWin.Skip(1).Select(x => x.ToString("F1"))) + " | "
                    + m.ToString("F1") + " | " + (tag.StartsWith("V0") ? "—" : (m - baseM).ToString("+0.0;-0.0;0.0"))
                    + " | " + share + " | **" + cost + "** | " + turns.ToString("F2")
                    + " | " + life.ToString("F2") + " | " + (life / turns).ToString("F3") + " |");
            }
            Console.WriteLine();
            Console.WriteLine("| 版 | 積んだ | 返した | 返却率 | 没収 | 残額 | 収支 | 荷の発火 | **増えた被ダメ** | 重りの振り | **振られなかった打点** | 下限 | 在庫（下がった攻撃力/T） | 味方の総被ダメ | **切り落とし** | 配った破片 |");
            Console.WriteLine("|---|---:|---:|---:|---:|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach ((string tag, Acc a) in accs)
            {
                double n = a.Battles;
                Console.WriteLine("| " + tag + " | " + (a.Stacked / n).ToString("F2") + " | " + (a.Released / n).ToString("F2")
                    + " | " + (a.Stacked > 0 ? (100.0 * a.Released / a.Stacked).ToString("F1") + "%" : "—")
                    + " | " + (a.Forfeited / n).ToString("F2") + " | " + (a.Residual / n).ToString("F2")
                    + " | " + (a.Unbalanced == 0 ? "**閉じる**" : "**ずれ " + a.Unbalanced + "**")
                    + " | " + (a.BurdenHits / n).ToString("F2") + " | **" + (a.BurdenAdded / n).ToString("F2") + "**"
                    + " | " + (a.LadenSwings / n).ToString("F2") + " | **" + (a.LadenLost / n).ToString("F2") + "**"
                    + " | " + (a.LadenFloored / n).ToString("F2")
                    + " | " + (a.LadenNominal / n).ToString("F2")
                    + " | " + (a.Damage / n).ToString("F1")
                    + " | **" + (a.BraceRefused / n).ToString("F2") + "**"
                    + " | " + (a.BraceGiven / n).ToString("F2") + " |");
            }
            Console.WriteLine();
            // 代金を誰が払ったか（**駒ごと**）。第153期は「返せなかった比率が代金を決める」を後から気づいた。
            Console.WriteLine("| 版 | 代金を払った駒（荷で増えた被ダメ ／ 重りで振られなかった打点） |");
            Console.WriteLine("|---|---|");
            foreach ((string tag, Acc a) in accs)
            {
                var rows = a.By.Where(k => k.Value.Burden > 0 || k.Value.Laden > 0)
                    .OrderByDescending(k => k.Value.Burden + k.Value.Laden)
                    .Select(k => k.Key + " " + (k.Value.Burden / (double)a.Battles).ToString("F2")
                                 + " ／ " + (k.Value.Laden / (double)a.Battles).ToString("F2"));
                Console.WriteLine("| " + tag + " | " + (rows.Any() ? string.Join(" ・ ", rows) : "—") + " |");
            }
            Console.WriteLine();
        }
    }

    // =================================================================================
    // 段B' —— 強度の掃引
    // =================================================================================

    static void Sweep()
    {
        Console.WriteLine("# 第154期 段B' —— 強度の掃引（`BurdenPercent` と `LadenPer`）");
        Console.WriteLine();
        Console.WriteLine("**代金は `版 − Vp`。** `Vp`（代金なし）は枝に依らないので1台1つ。");
        Console.WriteLine();
        var rigs = Rigs(Nochi(TraitId.Ward));
        for (int i = 0; i < rigs.Length - 1; i++)
        {
            Acc v0 = Measure(Rig(i, Nochi(), FillerOf[i].Atk, FillerOf[i].Hp).F, WardRule.Default);
            Acc vp = Measure(Rig(i, Nochi(TraitId.Ward), FillerOf[i].Atk, FillerOf[i].Hp).F, WardRule.Default);
            double b0 = 100.0 * v0.Wins / v0.Battles, bp = 100.0 * vp.Wins / vp.Battles;
            Console.WriteLine("## " + rigs[i].Name + "（V0 素体 " + b0.ToString("F1") + "% ／ Vp 預かりのみ " + bp.ToString("F1") + "%）");
            Console.WriteLine();
            Console.WriteLine("| 枝 | ノブ | 勝率 | 帰属 | **代金** | 増えた被ダメ | 振られなかった打点 | 切り落とし |");
            Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|");
            var pts = new List<WardRule>();
            foreach (int pc in new[] { 25, 50, 75 })
                pts.Add(WardRule.Default with { Cost = WardCost.Burden, BurdenPercent = pc });
            foreach (int per in new[] { 5, 10, 20 })
                pts.Add(WardRule.Default with { Cost = WardCost.Laden, LadenPer = per });
            foreach (WardRule r in pts)
            {
                bool bd = r.Cost == WardCost.Burden;
                UnitDef n = Nochi(TraitId.Ward, bd ? TraitId.Burden : TraitId.Laden);
                Acc a = Measure(Rig(i, n, FillerOf[i].Atk, FillerOf[i].Hp).F, r);
                double m = 100.0 * a.Wins / a.Battles;
                Console.WriteLine("| " + (bd ? "荷" : "重り") + " | "
                    + (bd ? "BurdenPercent " + r.BurdenPercent : "LadenPer " + r.LadenPer)
                    + " | " + m.ToString("F1") + " | " + (m - b0).ToString("+0.0;-0.0;0.0")
                    + " | **" + (m - bp).ToString("+0.0;-0.0;0.0") + "**"
                    + " | " + ((double)a.BurdenAdded / a.Battles).ToString("F2")
                    + " | " + ((double)a.LadenLost / a.Battles).ToString("F2")
                    + " | " + ((double)a.BraceRefused / a.Battles).ToString("F2") + " |");
            }
            Console.WriteLine();
        }
    }

    // =================================================================================
    // 段C —— プラス側を帯へ下ろす
    // =================================================================================

    static void Band()
    {
        Console.WriteLine("# 第154期 段C —— プラス側を帯へ下ろす（`Percent` と `Threshold` を同率で下げる）");
        Console.WriteLine();
        Console.WriteLine("**返却率が保たれるか**を実測で見る（保たれなければ「供給 ÷ 閾値」の読みが間違い・第153期 §5-3）。");
        Console.WriteLine("`Vp` の帰属が **+15 〜 +25** に入る点を採る。");
        Console.WriteLine();
        var rigs = Rigs(Nochi(TraitId.Ward));
        for (int i = 0; i < rigs.Length - 1; i++)
        {
            Acc v0 = Measure(Rig(i, Nochi(), FillerOf[i].Atk, FillerOf[i].Hp).F, WardRule.Default);
            double b0 = 100.0 * v0.Wins / v0.Battles;
            Console.WriteLine("## " + rigs[i].Name + "（V0 素体 " + b0.ToString("F1") + "%）");
            Console.WriteLine();
            Console.WriteLine("| Percent | Threshold | Vp の勝率 | **Vp の帰属** | 積んだ | 返した | **返却率** | 帯 |");
            Console.WriteLine("|---:|---:|---:|---:|---:|---:|---:|---|");
            foreach ((int pc, int th) in new[] { (50, 40), (40, 32), (30, 24), (25, 20), (20, 16), (15, 12) })
            {
                WardRule r = WardRule.Default with { Percent = pc, Threshold = th };
                Acc a = Measure(Rig(i, Nochi(TraitId.Ward), FillerOf[i].Atk, FillerOf[i].Hp).F, r);
                double m = 100.0 * a.Wins / a.Battles, d = m - b0;
                Console.WriteLine("| " + pc + " | " + th + " | " + m.ToString("F1") + " | **" + d.ToString("+0.0;-0.0;0.0")
                    + "** | " + ((double)a.Stacked / a.Battles).ToString("F2")
                    + " | " + ((double)a.Released / a.Battles).ToString("F2")
                    + " | " + (a.Stacked > 0 ? (100.0 * a.Released / a.Stacked).ToString("F1") + "%" : "—")
                    + " | " + (d >= 15 && d <= 25 ? "**○**" : "") + " |");
            }
            Console.WriteLine();
        }
    }

    // =================================================================================
    // 自己検査（§6）
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第154期 `wardcost check` —— 自己検査");
        Console.WriteLine();

        string bal = arg.Trim().Length > 0 ? arg.Trim() : "docs/balance.md";
        Console.WriteLine("## (a) `compare` 305 セルが `" + bal + "` と 0 件");
        Console.WriteLine();
        if (!File.Exists(bal)) Console.WriteLine("**`" + bal + "` が無い。** 手で `compare` を回して差分を見ること。");
        else
        {
            int cells = File.ReadAllLines(bal).Where(l => l.StartsWith("| ") && l.Contains("%"))
                .Sum(l => l.Split('|').Select(c => c.Trim()).Count(c => c.EndsWith("%")));
            Console.WriteLine("`" + bal + "` の `%` セルは **" + cells + "** 個。突き合わせは");
            Console.WriteLine("`0 compare > /tmp/x && diff docs/balance.md /tmp/x`——**ノチは `UnitCatalog.All` にも");
            Console.WriteLine("`Presets` にも無く、既定は `WardCost.Forfeit` なので、差分が出たら実装が既定を動かしている。**");
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
            Console.WriteLine("`BattleCore` の `PickOne(` は **" + n + "** 行（第153期の基準は 27）。"
                              + (n == 27 ? "**一致**" : "**要確認**"));
            Console.WriteLine();
        }

        var rigs = Rigs(Nochi(TraitId.Ward));

        Console.WriteLine("## (c) 収支が閉じる（積んだ ＝ 返した ＋ 没収 ＋ 残額）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 戦数 | 積んだ | 返した＋没収＋残額 | ずれた戦 |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|");
        long bad = 0;
        for (int i = 0; i < rigs.Length; i++)
            foreach ((string tag, UnitDef nochi, WardRule rule) in Versions(WardRule.Default))
            {
                if (tag.StartsWith("V0")) continue;
                Acc a = Measure(Rig(i, nochi, FillerOf[i].Atk, FillerOf[i].Hp).F, rule);
                bad += a.Unbalanced;
                Console.WriteLine("| " + rigs[i].Name + " | " + tag + " | " + a.Battles + " | " + a.Stacked
                    + " | " + (a.Released + a.Forfeited + a.Residual) + " | " + a.Unbalanced + " |");
            }
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "**1点もずれていない。**" : "**ずれている。止める。**");
        Console.WriteLine();

        Console.WriteLine("## (d) V1 が第153期の値を再現する");
        Console.WriteLine();
        Console.WriteLine("台3〜台5 は第153期の台をそのまま引いている（埋め草も席も同じ）ので、");
        Console.WriteLine("**V0 と V1 は1ビットも違わないはず。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | この期 | 第153期の報告値 | 一致 |");
        Console.WriteLine("|---|---|---:|---:|---|");
        var recorded = new (int Rig, string Tag, double V)[]
        {
            (2, "V0 素体", 42.6), (2, "V1 没収", 92.6),
            (3, "V0 素体", 73.9), (3, "V1 没収", 78.9),
            (4, "V0 素体", 68.4), (4, "V1 没収", 68.4),
        };
        foreach ((int rig, string tag, double v) in recorded)
        {
            (string t2, UnitDef nochi, WardRule rule) = Versions(WardRule.Default).First(x => x.Tag == tag);
            Acc a = Measure(Rig(rig, nochi, FillerOf[rig].Atk, FillerOf[rig].Hp).F, rule);
            double m = 100.0 * a.Wins / a.Battles;
            Console.WriteLine("| " + rigs[rig].Name + " | " + tag + " | " + m.ToString("F1") + " | " + v.ToString("F1")
                              + " | " + (Math.Abs(m - v) < 0.05 ? "**○**" : "**× 止める**") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## (e) 荷がササの切り落としを増やしている");
        Console.WriteLine();
        Console.WriteLine("| 版 | ササの被ダメ/戦 | 荷で増えた分 | **ササの切り落とし/戦** | 配った破片/戦 |");
        Console.WriteLine("|---|---:|---:|---:|---:|");
        foreach ((string tag, UnitDef nochi, WardRule rule) in Versions(WardRule.Default))
        {
            if (tag.StartsWith("V0")) continue;
            Acc a = Measure(Rig(0, nochi, FillerOf[0].Atk, FillerOf[0].Hp).F, rule);
            a.By.TryGetValue(UnitCatalog.Sasa.Id, out var c);
            Console.WriteLine("| " + tag + " | " + (a.Damage / (double)a.Battles).ToString("F1")
                + " | " + (c.Burden / (double)a.Battles).ToString("F2")
                + " | **" + (c.Refused / (double)a.Battles).ToString("F2") + "**"
                + " | " + (a.BraceGiven / (double)a.Battles).ToString("F2") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## (f) 重りが「攻撃しない駒」で 0 の代金になるか");
        Console.WriteLine();
        Console.WriteLine("| 台 | 前列の駒 | 版 | 振られなかった打点/戦 | **その駒の与ダメ/戦** | 台の合計 |");
        Console.WriteLine("|---|---|---|---:|---:|---:|");
        foreach ((int rig, string id) in new[] { (0, UnitCatalog.Sasa.Id), (1, UnitCatalog.Kado.Id) })
            foreach (string tag in new[] { "Vp 預かりのみ", "V3 重り" })
            {
                (string t3, UnitDef nochi, WardRule rule) = Versions(WardRule.Default).First(x => x.Tag == tag);
                Acc a = Measure(Rig(rig, nochi, FillerOf[rig].Atk, FillerOf[rig].Hp).F, rule);
                a.By.TryGetValue(id, out var c);
                Console.WriteLine("| " + rigs[rig].Name + " | " + id + " | " + tag + " | "
                    + (c.Laden / (double)a.Battles).ToString("F2") + " | **"
                    + (c.Out / (double)a.Battles).ToString("F1") + "** | "
                    + (a.LadenLost / (double)a.Battles).ToString("F2") + " |");
            }
        Console.WriteLine();
        Console.WriteLine("**カドは通常攻撃を1度も振らないが、棘の反撃量が `CurrentAttack` なので払う。**");
        Console.WriteLine("ただし反撃は `PerformAttack` を通らないので `振られなかった打点` には載らない");
        Console.WriteLine("——**この列が 0 でも代金が 0 とは限らない**（勝率の側で読むこと）。");
        Console.WriteLine();

        Console.WriteLine("## (h) ノチ非在席で版が1ビットも違わない");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Versions(WardRule.Default).Select(v => v.Tag)) + " | 差 |");
        Console.WriteLine("|---|" + string.Concat(Versions(WardRule.Default).Select(_ => "---|")) + "---|");
        {
            Formation f = rigs[4].F;
            var ws = Versions(WardRule.Default).Select(v => Measure(f, v.Rule).WaveWin).ToList();
            bool same = ws.All(w => Enumerable.Range(1, 4).All(st => w[st] == ws[0][st]));
            Console.WriteLine("| " + rigs[4].Name + " | "
                + string.Join(" | ", ws.Select(w => string.Join(" / ", w.Skip(1).Select(x => x.ToString("F1")))))
                + " | " + (same ? "**0 件**" : "**差あり。止める**") + " |");
        }
    }

    /// <summary>`run` の基準規則（引数 "percent threshold" で振れる）。</summary>
    static WardRule Base(string arg)
    {
        var p = arg.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        WardRule r = WardRule.Default;
        if (p.Length >= 1 && int.TryParse(p[0], out int pc)) r = r with { Percent = pc };
        if (p.Length >= 2 && int.TryParse(p[1], out int th)) r = r with { Threshold = th };
        return r;
    }

    static double[] Waves(Formation f)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < Seeds; seed++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
            w[st] = 100.0 * wins / Seeds;
        }
        return w;
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
}
