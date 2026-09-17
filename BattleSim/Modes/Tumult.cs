using BattleCore;
using static Common;

// =====================================================================================
// tumult モード（第144期） —— バサの転生（敵陣を掻き回し、前に出した敵を転ばせる）
//
// 指示書は design/PHASE144_TUMULT_SPEC.md。
//
//     dotnet run --project BattleSim -c Release 0 tumult phase0   # Q0-1〜Q0-7（**戦闘は回すが盤面は動かさない**）
//     dotnet run --project BattleSim -c Release 0 tumult scan     # 台の下見だけ（V0 の第2〜5波平均が 40〜95% か）
//     dotnet run --project BattleSim -c Release 0 tumult run      # 段A〜C の測定
//     dotnet run --project BattleSim -c Release 0 tumult check    # 自己検査
// =====================================================================================

static class TumultDiag
{
    const int Seeds = 200;

    static readonly ShufflerRule V0 = ShufflerRule.Legacy;                 // 現行（第143期までの姿）
    static readonly ShufflerRule VA = new(true, ShuffleStagger.None);      // 段A
    static readonly ShufflerRule VB = new(true, ShuffleStagger.Advanced);  // 段B（採用候補）
    static readonly ShufflerRule VC = new(true, ShuffleStagger.Both);      // 段C（上限の測定）

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "scan": if (arg.StartsWith("probe")) Probe(arg.Length > 5 ? arg[5..].Trim() : ""); else Scan(); return;
            case "run": Sweep(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("tumult: モードは phase0 / scan / run / check。");
                return;
        }
    }

    // =================================================================================
    // 台
    // =================================================================================

    /// <summary>
    /// 埋め草は<b>特性を1つも持たない素体</b>（第143期の則）。
    /// <b>敵の数値に触る駒（呪詛官ネル・萎縮のクビ）を入れると、転倒で消える手番の価値が
    /// そのまま埋め草の性能になる。</b>
    /// </summary>
    static UnitDef Plain(string id, int hp, int atk, int spd = 8) => new()
    {
        Id = id, Name = $"素体{id}", MaxHp = hp, Attack = atk, Speed = spd,
        Advances = true, Traits = Array.Empty<TraitId>()
    };

    /// <summary>
    /// ローカル台。<b><c>Presets</c> には置かない</b>（<c>compare</c> 61行を汚さない）。
    /// バサは全台とも<b>後3</b>——<c>OnTurnStart</c> は行動順ループの外なので席は発火に効かないが、
    /// 攻7 の駒を前に出す理由も無い。
    /// </summary>
    static (string Name, Formation F)[] Rigs() => new (string, Formation)[]
    {
        // 味方側の入れ替えを供給として読む駒（ヨミ＝軋み・シオ＝移り木）を置いた台。
        // **現行のバサが唯一いくらか仕事をしている形**なので、敵側を足しても
        // 味方側の供給が痩せていないことがここで見える。
        ("台1 読み手あり (バサ×ヨミ×シオ)",
            Formation.Build(front1: Plain("c", 70, 12), front3: UnitCatalog.Gald,
                            center: UnitCatalog.Shio, back1: UnitCatalog.Yomi, back3: UnitCatalog.Basa)),

        // **主判定の台。** 移動を1ビットも読まない4枚なので、動いたぶんは全部敵側の効き。
        ("台2 読み手なし (バサ×移動を読まない4枚)",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga,
                            center: UnitCatalog.Gan, back1: Plain("d", 60, 9), back3: UnitCatalog.Basa)),

        // ササ（第143期・前1 で身を固めて隣を弾く）と同席。**2枚の乱れ方が干渉する。**
        // ササの切り落としの宛先（隣接する生存味方の席番号最小）をバサが毎ターン飛ばす。
        // 破片の読み手を2枚（ガレ＝量で読む礫・ウロ＝二値で読む鱗）置いてあるので、
        // 宛先が飛ばされたぶんがそのまま勝率に出る。
        ("台3 ササ同席 (バサ×ササ×ガレ×ウロ)",
            Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Uro,
                            center: UnitCatalog.Gare, back1: UnitCatalog.Kado, back3: UnitCatalog.Basa)),

        // 肩代わりと同席。**味方側の入れ替えで壁が後ろへ飛ぶ代金**が現行と同じかを見る
        // （味方側は1文字も変えていないので、**変わらないはず**）。
        ("台4 肩代わり (バサ×ゴルム)",
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Gald,
                            center: UnitCatalog.Gan, back1: UnitCatalog.Nomi, back3: UnitCatalog.Basa)),
    };

    /// <summary><c>compare</c> 61 行のうちバサを含む行。</summary>
    static (string Name, Formation F)[] BasaRows()
        => Presets.Compare.Where(b => b.F.Occupied().Any(o => o.Def.Id == "basa")).ToArray();

    // =================================================================================
    // Phase 0
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第144期 Phase 0 —— バサの転生（`tumult phase0`）");
        Console.WriteLine();
        Q01();
        Console.WriteLine();
        Q03();
        Console.WriteLine();
        Q04();
        Console.WriteLine();
        Q06();
        Console.WriteLine();
        Scan();
    }

    /// <summary>
    /// Q0-1 —— 敵は召喚枠に立つか。<b>走査ではなく実測で答える。</b>
    /// 背かれのソム（第103期）が<b>敵チームの ○前2（枠7）</b>へ餌を喚ぶので、その頻度を数える。
    /// </summary>
    static void Q01()
    {
        Console.WriteLine("## Q0-1 —— 敵は召喚枠に立つか（**立つ。だから編成枠だけを見る**）");
        Console.WriteLine();
        Console.WriteLine("`ctx.Summon(..., foe, FodderSlot)` は背かれのソム（第103期）が**敵チームの ○前2（枠7）**へ喚ぶ。");
        Console.WriteLine();

        var somRows = Presets.Compare.Concat(Presets.Cross)
            .Where(b => b.F.Occupied().Any(o => o.Def.Id == "som")).ToArray();
        Console.WriteLine($"**`compare` 61 行 ＋ 交差帯 12 行にソムを含む行は {somRows.Length} 行**"
            + "——だから `compare` を回しても、この穴は1件も見えない。");
        Console.WriteLine("ソムは `UnitCatalog.All`（編成に選べる 52 枚）に居るので、**ローカル台で実測して確かめる**。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 敵側に湧いた餌/戦 | バサが敵の召喚枠を掴みうるターン/戦 |");
        Console.WriteLine("|---|---:|---:|");
        {
            Formation f = Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga,
                                          center: UnitCatalog.Som, back1: UnitCatalog.Nomi,
                                          back3: UnitCatalog.Basa);
            long battles = 0, summoned = 0;
            for (int st = 1; st < EnemyCatalog.Stages.Count; st++)
                for (int seed = 0; seed < 50; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                      verbose: false, shuffler: VB);
                    battles++; summoned += r.BetraySummoned;
                }
            Console.WriteLine($"| ソム同席 (ソム×バサ) | {(double)summoned / battles:F2} | "
                + "餌が立っている間ずっと（同時に1体・`BetrayedTrait.FodderSlot` ＝ 枠7） |");
        }
        Console.WriteLine();
        Console.WriteLine("> **敵側の候補は `FormationRules.IsSummonSlot` で編成枠（0–4）に絞る。**");
        Console.WriteLine("> 除かないと「餌と入れ替えて前に出す」が起きる（逃亡・後退が `PlayableSlotsOfRow` を使うのと同じ作法）。");
        Console.WriteLine("> **味方側は従来どおり召喚枠も含める**——胞子は昔から入れ替えの対象だった。");
    }

    /// <summary>
    /// Q0-3 —— 敵に <c>IdleTurn</c> が立ったとき、それを読む味方の機構があるか。
    /// <b>実装から引く</b>（第117期「引けなかったときの分岐を必ず書く」）。
    /// </summary>
    static void Q03()
    {
        Console.WriteLine("## Q0-3 —— 敵の `IdleTurn` を読む味方の機構（**実装から引く**）");
        Console.WriteLine();
        string? path = FindSource("Traits.cs");
        if (path is null) { Console.WriteLine("**走査できなかった（`BattleCore/Traits.cs` が見つからない）。ここで止める。**"); return; }
        string src = File.ReadAllText(path);

        var hits = new List<string>();
        string[] lines = src.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string l = lines[i];
            string t = l.TrimStart();
            if (t.StartsWith("//")) continue;
            if (!l.Contains("StatusKeys.IdleTurn")) continue;
            string who = l.Contains("target.") ? "**target（＝殴った相手＝敵）**"
                       : l.Contains("ally.") ? "ally（味方）"
                       : l.Contains("self.") ? "self（自分）" : "その他";
            hits.Add($"| `Traits.cs:{i + 1}` | {who} | `{t}` |");
        }
        Console.WriteLine("| 位置 | 読む相手 | 行 |");
        Console.WriteLine("|---|---|---|");
        foreach (string h in hits) Console.WriteLine(h);

        int foes = EnemyCatalog.Stages.Sum(s => s.Enemy.Occupied().Count());
        int buyers = EnemyCatalog.Stages.Sum(s => s.Enemy.Occupied()
            .Count(o => o.Def.Traits.Contains(TraitId.Rally) || o.Def.Traits.Contains(TraitId.Bulwark)));
        Console.WriteLine();
        Console.WriteLine("> **`target.` を読む行だけが敵側に届く**——追い打ち（`TormentTrait`・シガ）で、");
        Console.WriteLine("> 「動けない敵にはもう一撃」。転倒した敵はここに乗るので、**これは噛み合う側の相互作用**である。");
        Console.WriteLine("> 号令（`RallyTrait`）は `LivingMembers(self.TeamId)` を、据え（`Bulwark`）は被弾側の `teammates` を");
        Console.WriteLine("> 見るので、敵の転倒を買い取るのは**敵に号令・据えの持ち主がいる場合だけ**——");
        Console.WriteLine($"> `Stages` の敵 {foes} 体に `Rally` / `Bulwark` の保持者は **{buyers} 体**。");
    }

    /// <summary>
    /// Q0-4 —— 各波の席順と特性。<b>段B の予測はこの表からしか書けない。</b>
    /// 「行が前に変わる」のは <see cref="FormationRules.DepthOf"/> が浅くなった側なので、
    /// <b>無作為に2体選んだときに「特性持ちが前へ出る」確率</b>を波ごとに紙で出す。
    /// </summary>
    static void Q04()
    {
        Console.WriteLine("## Q0-4 —— 各波の席順・特性と、**転倒が当たる確率**（紙）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 前1 | 前3 | 中央 | 後1 | 後3 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
        {
            Formation e = EnemyCatalog.Stages[st].Enemy;
            var cells = new List<string>();
            for (int slot = 0; slot < 5; slot++)
            {
                UnitDef? d = e[slot];
                if (d is null) { cells.Add("—"); continue; }
                string tr = d.Traits.Count == 0 ? "" : $"・{string.Join("/", d.Traits)}";
                cells.Add($"{d.Name}(HP{d.MaxHp}/攻{d.Attack}{tr})");
            }
            Console.WriteLine($"| 第{st + 1}波 | {string.Join(" | ", cells)} |");
        }

        Console.WriteLine();
        Console.WriteLine("**無作為に2体を入れ替えたとき**（全員生存・開戦時の並び）:");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵の数 | 組 | 誰も前に出ない組 | 前に出る組 | うち**特性持ち**が出る組 | 期待 特性持ち/ターン |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|");
        for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
        {
            var occ = EnemyCatalog.Stages[st].Enemy.Occupied().ToList();
            int pairs = 0, flat = 0, fwd = 0, traited = 0;
            for (int i = 0; i < occ.Count; i++)
                for (int j = i + 1; j < occ.Count; j++)
                {
                    pairs++;
                    int di = FormationRules.DepthOf(FormationRules.RowOf(occ[i].Slot));
                    int dj = FormationRules.DepthOf(FormationRules.RowOf(occ[j].Slot));
                    if (di == dj) { flat++; continue; }
                    fwd++;
                    UnitDef mover = di > dj ? occ[i].Def : occ[j].Def;   // 深いほうが前へ出る
                    if (mover.Traits.Count > 0) traited++;
                }
            Console.WriteLine($"| 第{st + 1}波 | {occ.Count} | {pairs} | {flat} | {fwd} | {traited} | {(double)traited / pairs:F2} |");
        }
        Console.WriteLine();
        Console.WriteLine("> **この表が段B の予測の根拠**（第143期の反省——分布を見ずに予測を書かない）。");
        Console.WriteLine("> 上の値は**開戦時の並び・全員生存**の紙で、実戦では駒が減るほど組の数が落ちる。");
    }

    /// <summary>Q0-6 —— バサの現在の帳簿（走らせて引く）。</summary>
    static void Q06()
    {
        Console.WriteLine("## Q0-6 —— バサの現在の帳簿（走らせて引く・seed 0..49・第2〜5波）");
        Console.WriteLine();
        var cmp = BasaRows();
        var cross = Presets.Cross.Where(b => b.F.Occupied().Any(o => o.Def.Id == "basa")).ToArray();
        Console.WriteLine($"`compare` 61 行のうちバサを含むのは **{cmp.Length} 行**: {string.Join(" / ", cmp.Select(r => r.Name))}");
        Console.WriteLine($"交差帯 12 行のうちバサを含むのは **{cross.Length} 行**: {string.Join(" / ", cross.Select(r => r.Name))}");
        Console.WriteLine();
        Console.WriteLine("| 行 | 第2〜5波平均 | 味方の入れ替え/戦 | 与ダメ/戦 | 被弾/戦 | 生存T | 死亡率 |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach ((string name, Formation f) in cmp)
        {
            long battles = 0, swaps = 0, dealt = 0, taken = 0, life = 0, deaths = 0;
            double win = 0;
            for (int st = 1; st < EnemyCatalog.Stages.Count; st++)
                for (int seed = 0; seed < 50; seed++)
                {
                    BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false);
                    battles++;
                    if (r.PlayerWon) win++;
                    if (r.TallyByUnit.TryGetValue("basa", out UnitTally? t))
                    {
                        swaps += t.ShuffleAllySwaps; dealt += t.DamageToEnemy; taken += t.DamageTaken;
                        life += t.LastActiveTurn; deaths += t.Deaths > 0 ? 1 : 0;
                    }
                }
            Console.WriteLine($"| {name} | {100.0 * win / battles:F1}% | {(double)swaps / battles:F2} | {(double)dealt / battles:F1} " +
                              $"| {(double)taken / battles:F1} | {(double)life / battles:F2} | {100.0 * deaths / battles:F1}% |");
        }
    }

    /// <summary>台の下見（第133期「段1 のローカル台にも床と天井の規則を当てる」）。<b>版を1つも振らない。</b></summary>
    static void Scan()
    {
        Console.WriteLine("## 台の下見（**版を1つも振らない**。V0 ＝ 現行バサ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | V0 勝率（1..5波） | 第2〜5波平均 | 帯（40〜95%） | 情報セル |");
        Console.WriteLine("|---|---|---:|---|---:|");
        foreach ((string name, Formation f) in Rigs())
        {
            double[] w = Rates(f, V0);
            double m = (w[1] + w[2] + w[3] + w[4]) / 4;
            int info = w.Skip(1).Count(x => x > 0 && x < 100);
            Console.WriteLine($"| {name} | {string.Join(" / ", w.Select(x => x.ToString("F1")))} | {m:F1} " +
                              $"| {(m >= 40 && m <= 95 ? "○" : "**×**")} | {info} |");
        }
    }

    /// <summary>台の埋め草の走査（帯 40〜95% かつ情報セル 3 以上の組を探す）。</summary>
    static void Probe(string only)
    {
        Console.WriteLine("## 台の埋め草の走査（V0 ＝ 現行バサ・seed 0..29）");
        Console.WriteLine();
        // **移動を読む3枚（ヨミ＝軋み・シオ＝移り木・ハネ＝突き返し）は候補に入れない。**
        // 台2・台3 は「敵側の効きだけ」を見る台なので、味方側の入れ替えを供給として読む駒が
        // 1枚でも混ざると、動いたぶんがどちら由来か割れなくなる。
        // **敵の数値に触る駒（ネル＝呪詛・クビ＝萎縮）も入れない**（第143期の則）。
        UnitDef[] cand = { UnitCatalog.Dolga, UnitCatalog.Borg, UnitCatalog.Gald, UnitCatalog.Rica,
                           UnitCatalog.Nomi, UnitCatalog.Mug, UnitCatalog.Vel, UnitCatalog.Zan,
                           UnitCatalog.Gan, UnitCatalog.Kado, UnitCatalog.Hagi, UnitCatalog.Uro,
                           Plain("c", 70, 12), Plain("d", 60, 9) };
        (string Bed, (int Slot, UnitDef Def)[] Fixed)[] beds =
        {
            ("台1 読み手あり", new[] { (2, UnitCatalog.Shio), (3, UnitCatalog.Yomi) }),
            ("台2 読み手なし", Array.Empty<(int, UnitDef)>()),
            ("台3 ササ同席", new[] { (0, UnitCatalog.Sasa), (2, UnitCatalog.Gare) }),
            ("台4 肩代わり", new[] { (0, UnitCatalog.Golm) }),
        };
        foreach ((string bed, var fix) in beds)
        {
            if (only.Length > 0 && !bed.Contains(only)) continue;
            var free = Enumerable.Range(0, 4).Where(i => !fix.Any(x => x.Slot == i)).ToArray();
            var hits = new List<(double M, string Name, double[] W, int Info)>();
            foreach (int[] pick in Pick(cand.Length, free.Length))
            {
                if (pick.Distinct().Count() != pick.Length) continue;
                var f = new Formation();
                f[4] = UnitCatalog.Basa;
                foreach ((int slot, UnitDef d) in fix) f[slot] = d;
                for (int i = 0; i < free.Length; i++) f[free[i]] = cand[pick[i]];
                if (f.Occupied().Select(o => o.Def.Id).Distinct().Count() != 5) continue;

                double[] w = Rates(f, V0, 30);
                double m = (w[1] + w[2] + w[3] + w[4]) / 4;
                int info = w.Skip(1).Count(x => x > 0 && x < 100);
                if (m >= 40 && m <= 95 && info >= 3)
                    hits.Add((m, string.Join(" / ", f.Occupied().Select(o => o.Def.Name)), w, info));
            }
            Console.WriteLine($"### {bed} —— 帯かつ情報セル 3 以上: **{hits.Count}** 組");
            Console.WriteLine();
            Console.WriteLine("| 組（席順） | 勝率（1..5波） | 第2〜5波平均 | 情報セル |");
            Console.WriteLine("|---|---|---:|---:|");
            foreach (var h in hits.OrderByDescending(x => x.Info).ThenBy(x => Math.Abs(x.M - 67.5)).Take(6))
                Console.WriteLine($"| {h.Name} | {string.Join(" / ", h.W.Select(x => x.ToString("F1")))} | {h.M:F1} | {h.Info} |");
            Console.WriteLine();
        }
    }

    static IEnumerable<int[]> Pick(int n, int k)
    {
        var idx = new int[k];
        while (true)
        {
            yield return (int[])idx.Clone();
            int i = k - 1;
            while (i >= 0 && ++idx[i] == n) idx[i--] = 0;
            if (i < 0) yield break;
        }
    }

    // =================================================================================
    // 段A〜C
    // =================================================================================

    static void Sweep()
    {
        Console.WriteLine("# 第144期 段A〜C —— `tumult run`");
        Console.WriteLine();
        Console.WriteLine($"seed 0..{Seeds - 1}。**判定は第2〜5波**（規約 (G10)。第一波は実行するが分母に入れない）。");
        Console.WriteLine();

        (string Tag, ShufflerRule R)[] versions =
        {
            ("V0 現行", V0), ("段A 敵も乱す", VA), ("段B ＋前に出た敵が転ぶ", VB), ("段C 2体とも転ぶ", VC),
        };

        foreach ((string label, (string, Formation)[] rows) in new (string, (string, Formation)[])[]
                 { ("ローカル台", Rigs()), ("`compare` のバサ行", BasaRows()) })
        {
            Console.WriteLine($"## {label}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波 | 帰属 |");
            Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|");
            foreach ((string name, Formation f) in rows)
            {
                double baseM = 0;
                foreach ((string tag, ShufflerRule r) in versions)
                {
                    double[] w = Rates(f, r);
                    double m = (w[1] + w[2] + w[3] + w[4]) / 4;
                    if (r.Equals(V0)) baseM = m;
                    Console.WriteLine($"| {name} | {tag} | {string.Join(" | ", w.Select(x => x.ToString("F1")))} " +
                                      $"| **{m:F1}** | {(r.Equals(V0) ? "—" : $"{m - baseM:+0.0;-0.0}")} |");
                }
                Console.Out.Flush();
            }
            Console.WriteLine();
        }

        Ledger();
    }

    /// <summary>機構が動いているか（受け入れ条件 4-2）。</summary>
    static void Ledger()
    {
        Console.WriteLine("## 機構の帳簿（受け入れ条件 4-2・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("`前出` ＝ 行が前に変わった敵の延べ体数 ／ `特性` ＝ そのうち特性を1つ以上持っていた体数 ／");
        Console.WriteLine("`後列発` ＝ そのうち `Row.Back` から出てきた体数 ／ `転倒` ＝ 実際に `Stagger` を立てた回数 ／");
        Console.WriteLine("`潰れ` ＝ 敵側で転倒によって落ちた手番（engine の `StallStagger`）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 味方入替/戦 | 敵入替/戦 | 前出/戦 | 特性/戦 | 後列発/戦 | 転倒/戦 | 潰れ/戦 | バサ生存T |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");

        foreach ((string name, Formation f) in Rigs().Concat(BasaRows()))
        {
            foreach ((string tag, ShufflerRule r) in new[] { ("V0", V0), ("段A", VA), ("段B", VB), ("段C", VC) })
            {
                long battles = 0, aswap = 0, fswap = 0, adv = 0, tra = 0, back = 0, stag = 0, stall = 0, life = 0;
                for (int st = 1; st < EnemyCatalog.Stages.Count; st++)
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                            verbose: false, shuffler: r);
                        battles++;
                        if (res.TallyByUnit.TryGetValue("basa", out UnitTally? t))
                        {
                            aswap += t.ShuffleAllySwaps; fswap += t.ShuffleFoeSwaps; adv += t.ShuffleAdvanced;
                            tra += t.ShuffleAdvancedTraited; back += t.ShuffleAdvancedFromBack;
                            stag += t.ShuffleStaggers; life += t.LastActiveTurn;
                        }
                        // 敵側の潰れた手番だけ。味方が転ぶ経路が無いことの検算は check の (d)。
                        foreach ((string id, UnitTally t2) in res.TallyByUnit)
                            if (EnemyIds.Contains(id)) stall += t2.StallStagger;
                    }
                double b = battles;
                Console.WriteLine($"| {name} | {tag} | {aswap / b:F2} | {fswap / b:F2} | {adv / b:F2} | {tra / b:F2} " +
                                  $"| {back / b:F2} | {stag / b:F2} | {stall / b:F2} | {life / b:F2} |");
                Console.Out.Flush();
            }
        }
    }

    static readonly HashSet<string> EnemyIds =
        EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def.Id))
            .Concat(new[] { UnitCatalog.Fodder.Id }).ToHashSet();

    // =================================================================================
    // 自己検査
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第144期 自己検査 —— `tumult check`");
        Console.WriteLine();

        string bal = arg.Length > 0 ? arg : "docs/balance.md";
        Console.WriteLine($"## (a) 必須1: `compare` 305 セルが `{bal}` と 0 件（既定の `ShufflerRule`）");
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
                if (!want.TryGetValue(name, out double[]? w)) { Console.WriteLine($"- 行が引けない: {name}"); continue; }
                double[] got = Rates(f, null);
                for (int i = 0; i < 5; i++) { cells++; if (Math.Abs(got[i] - w[i]) > 0.001) diff++; }
            }
            Console.WriteLine($"- {cells} セル中 **{diff} 件**の差分{(diff == 0 ? " ○" : " **×**")}");
        }

        Console.WriteLine();
        Console.WriteLine("## (b) `ShufflerRule.Legacy` と既定が 305 セル 0 件（**既定が現行と同値か**）");
        Console.WriteLine();
        {
            int diff = 0;
            foreach ((string _, Formation f) in Presets.Compare)
            {
                double[] a = Rates(f, null), c = Rates(f, V0);
                for (int i = 0; i < 5; i++) if (Math.Abs(a[i] - c[i]) > 0.001) diff++;
            }
            Console.WriteLine($"- **{diff} 件**{(diff == 0 ? " ○（既定＝現行）" : "（既定は採用版。段B を採った後はここが 0 でなくなる）")}");
        }

        Console.WriteLine();
        Console.WriteLine("## (c) バサを含まない行は V0 と段B で ±0.0（特異性）");
        Console.WriteLine();
        {
            int diff = 0, rows = 0;
            foreach ((string name, Formation f) in Presets.Compare.Concat(Presets.Cross))
            {
                if (f.Occupied().Any(o => o.Def.Id == "basa")) continue;
                rows++;
                double[] a = Rates(f, V0), b = Rates(f, VB);
                for (int i = 0; i < 5; i++)
                    if (Math.Abs(a[i] - b[i]) > 0.001) { diff++; Console.WriteLine($"- 動いた: {name} 第{i + 1}波 {a[i]:F1} → {b[i]:F1}"); }
            }
            Console.WriteLine($"- バサを含まない **{rows} 行 / {rows * 5} セル**中 **{diff} 件**{(diff == 0 ? " ○" : " **×**")}");
        }

        Console.WriteLine();
        Console.WriteLine("## (d) 味方側の `Stagger` は 0（この期の変更で味方が転ぶ経路が無いこと）");
        Console.WriteLine();
        {
            long ally = 0, foe = 0;
            foreach ((string _, Formation f) in Rigs().Concat(BasaRows()))
                for (int st = 1; st < EnemyCatalog.Stages.Count; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                          verbose: false, shuffler: VC);
                        foreach ((string id, UnitTally t) in r.TallyByUnit)
                            if (EnemyIds.Contains(id)) foe += t.StallStagger; else ally += t.StallStagger;
                    }
            Console.WriteLine($"- 味方側の潰れた手番: **{ally}**{(ally == 0 ? " ○" : " **×**")} ／ 敵側: {foe}（段C ＝ いちばん撒く版で測った）");
        }

        Console.WriteLine();
        Console.WriteLine("## (e) 帳簿が閉じる（段B: 転倒 ≦ 前出 ／ 段C: 転倒 ≦ 2 × 敵入替）");
        Console.WriteLine();
        foreach ((string tag, ShufflerRule r) in new[] { ("段B", VB), ("段C", VC) })
        {
            long adv = 0, stag = 0, fswap = 0;
            foreach ((string _, Formation f) in Rigs())
                for (int st = 1; st < EnemyCatalog.Stages.Count; st++)
                    for (int seed = 0; seed < 50; seed++)
                    {
                        BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                            verbose: false, shuffler: r);
                        if (res.TallyByUnit.TryGetValue("basa", out UnitTally? t))
                        { adv += t.ShuffleAdvanced; stag += t.ShuffleStaggers; fswap += t.ShuffleFoeSwaps; }
                    }
            bool ok = tag == "段B" ? stag <= adv : stag <= 2 * fswap;
            Console.WriteLine($"- {tag}: 前出 {adv} / 敵入替 {fswap} / 転倒 {stag} → {(ok ? "○" : "**×**")}");
        }

        Console.WriteLine();
        Console.WriteLine("## (f) 必須4: `ctx.PickOne` を新たに使っていない");
        Console.WriteLine();
        string? tp = FindSource("Traits.cs");
        if (tp is null) Console.WriteLine("**走査できなかった。ここで止める。**");
        else
        {
            string src = File.ReadAllText(tp);
            int i = src.IndexOf("public sealed class ShufflerTrait", StringComparison.Ordinal);
            int j = i < 0 ? -1 : src.IndexOf("\npublic ", i + 10, StringComparison.Ordinal);
            string body = i < 0 ? "" : src.Substring(i, (j < 0 ? src.Length : j) - i);
            int n = CountOf(body, "PickOne(");
            Console.WriteLine($"- `ShufflerTrait` の本体に `PickOne(` は **{n} 件**{(n == 0 ? " ○" : " **×**")}");
            Console.WriteLine($"- `Traits.cs` 全体の `PickOne(`: **{CountOf(src, "PickOne(")} 件**（第143期 HEAD と同数であること）");
        }
    }

    static int CountOf(string s, string needle)
    {
        int n = 0, i = 0;
        while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    // =================================================================================
    // 共有
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

    /// <summary>
    /// リポジトリ内のソースを探す。<b>引けなかったら呼び出し側で止めること</b>（第117期）
    /// ——実装から引く分類は、引けなかったときに「該当なし」と区別が付かない。
    /// </summary>
    static string? FindSource(string leaf)
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            string p = Path.Combine(dir.FullName, "BattleCore", leaf);
            if (File.Exists(p)) return p;
        }
        return null;
    }
}
