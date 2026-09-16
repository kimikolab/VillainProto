using BattleCore;

// =====================================================================================
// shard モード（第137期） —— 砕けの鍵を自前にする（ヒビ）
//
// 指示書は design/PHASE137_SHARD_SPEC.md。
// **engine に規則は1本も足していない**——`ShatterRule` は診断が版を差し替えるための窓口で、
// 既定（`Passive` / `SelfCostPerTurn = 0`）ではヒビを入れない限り1バイトも動かない。
//
//     dotnet run --project BattleSim -c Release 0 shard phase0    # Q0-1 の実測（現行の帳簿）
//     dotnet run --project BattleSim -c Release 0 shard scan      # 段1 の台の下見（V0 の勝率が 40〜95% か）
//     dotnet run --project BattleSim -c Release 0 shard run       # 掃引（3点）× 台 × 既存5行
//     dotnet run --project BattleSim -c Release 0 shard check [段1のbalance.md]  # 自己検査
// =====================================================================================

static class ShardDiag
{
    const int Seeds = 200;

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "scan": Scan(); return;
            case "run": Sweep(arg); return;
            case "check": Check(arg); return;
            case "gscan": if (arg.StartsWith("probe")) GareProbe(arg.Length > 5 ? arg[6..] : ""); else GareScan(); return;   // 第138期 段4
            case "gare": GareSweep(arg); return;               // 第138期 段5（掃引）
            default:
                Console.WriteLine("shard: モードは phase0 / scan / run / check（第137期）／ gscan / gare（第138期）。");
                return;
        }
    }

    // =================================================================================
    // 行の集合
    // =================================================================================

    /// <summary>ヒビを含む既存行（`compare` 61行 ＋ 交差帯 12行）。**走らせて引く**（手で写さない）。</summary>
    static List<(string Band, string Name, Formation F)> HibiRows()
    {
        var rows = new List<(string, string, Formation)>();
        foreach ((string band, var src) in new[] { ("compare", Presets.Compare), ("交差帯", Presets.Cross) })
            foreach ((string name, Formation f) in src)
                if (f.Occupied().Any(o => o.Def.Id == "hibi")) rows.Add((band, name, f));
        return rows;
    }

    /// <summary>
    /// 段1 のローカル台（§4-3）。**`Presets` には置かない**（`compare` 61行を汚さない）。
    ///
    /// <para><b>指示書の4台のうち「ヒビ単騎」を「肩代わり」に差し替えた。</b>
    /// 「何も読まない駒4枚」は台2（素通り）と実体が同じ——<b>アーマーの読み手はウロ1枚だけ</b>で
    /// （`Traits.cs` の走査で確認）、ウロを外せばどの5枚でも「読まない」になる。
    /// 一方 §2-3 と予測7 が要求する<b>自弁率の切り分け</b>には巨躯（ゴルム）を同席させた台が要るので、
    /// 重複する台をそちらに充てた。<b>差し替えたことと理由をここに書く</b>（指示書 §7）。</para>
    ///
    /// <para><b>台3 は既存行 `範囲耐性 (ヒビ×ボルグ)` と同一構成</b>なので、新しく組まずにそれを使う。</para>
    /// </summary>
    static (string Name, Formation F)[] Rigs() => new (string, Formation)[]
    {
        // 変換率が高い: ウロが破片を読んで貫きになる。
        //
        // **死軸（リィカ）を入れていない——指示書 §4-3 から変えた。**
        // 理由は2つ: (i) リィカを入れた版は `scan` で **第2〜5波平均 33.0%**＝**床**だった
        // （第61期「素体の5波平均 40% 以上」・第63期「40〜95% の両側で切る」・
        // 第133期「段1 のローカル台にも床と天井の規則を当てる」）、
        // (ii) 鱗の供給が**砕けと味方の死の2本**になるので
        // `ShatterSoaked`（プール全体）の帰属が濁る。
        // ムド（泥人形）は破片を読まないし肩代わりもしない壁なので、
        // **破片の出どころが砕けだけになる**（`scan` で 65.0% ＝帯の中）。
        ("台1 変換 (ヒビ×ウロ)",
            Formation.Build(front1: UnitCatalog.Mudo, front3: UnitCatalog.Dolga,
                            center: UnitCatalog.Hibi, back1: UnitCatalog.Uro, back3: UnitCatalog.Borg)),

        // 変換率が低い: 破片の読み手ゼロ・肩代わりゼロ。火力はある（台が床に落ちないように）。
        ("台2 素通り (ヒビ×読み手なし)",
            Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Borg,
                            center: UnitCatalog.Hibi, back1: UnitCatalog.Nel, back3: UnitCatalog.Egu)),

        // ガルド同席: §1-5（破片の soak と受け流しの在庫が食い合うか）の切り分け。
        ("台3 ガルド (ヒビ×ガルド)",
            Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga,
                            center: UnitCatalog.Hibi, back1: UnitCatalog.Borg, back3: UnitCatalog.Rica)),

        // 肩代わり: 巨躯（ゴルム）が自傷の代金を横取りする＝自弁率が落ちる台。
        // **配る量は「実際に減った HP」なので、代金を肩代わりされると配布量も減る。**
        ("台4 肩代わり (ヒビ×ゴルム)",
            Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Dolga,
                            center: UnitCatalog.Hibi, back1: UnitCatalog.Borg, back3: UnitCatalog.Egu)),
    };

    // =================================================================================
    // 測定
    // =================================================================================

    sealed class Led
    {
        public long Battles, Wins, Turns;
        public long Ticks, Given, Soaked, Paid, PaidSelf;
        public long GainShatter, GainDeath, WornTurns, AliveTurns;
        public long HibiTaken, HibiLife, HibiDeaths, HibiDealt;
        public long GaldTaken, GaldParry, GaldParryAmt, GaldLife, GaldDeaths;
        public readonly double[] Win = new double[5];
    }

    static Led Run(Formation f, ShatterRule rule)
    {
        var L = new Led();
        for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                  verbose: false, shatter: rule);
                if (r.PlayerWon) wins++;
                if (st == 0) continue;   // 帳簿は第2〜5波（規約 (G10)）

                L.Battles++;
                if (r.PlayerWon) L.Wins++;
                L.Turns += r.Turns;
                L.Ticks += r.ShatterTicks;
                L.Given += r.ShatterGiven;
                L.Soaked += r.ShatterSoaked;
                L.Paid += r.ShatterPaid;
                L.PaidSelf += r.ShatterPaidSelf;
                L.GainShatter += r.ScaleGainShatter;
                L.GainDeath += r.ScaleGainDeath;
                L.WornTurns += r.ScaleWornTurns;
                L.AliveTurns += r.ScaleAliveTurns;
                if (r.TallyByUnit.TryGetValue("hibi", out UnitTally? th))
                {
                    L.HibiTaken += th.DamageTaken;
                    L.HibiLife += th.LastActiveTurn;
                    L.HibiDeaths += th.Deaths > 0 ? 1 : 0;
                    L.HibiDealt += th.DamageToEnemy;
                }
                if (r.TallyByUnit.TryGetValue("gald", out UnitTally? tg))
                {
                    L.GaldTaken += tg.DamageTaken;
                    L.GaldParry += tg.ParryFires;
                    L.GaldParryAmt += tg.ParryBlocked;
                    L.GaldLife += tg.LastActiveTurn;
                    L.GaldDeaths += tg.Deaths > 0 ? 1 : 0;
                }
            }
            L.Win[st] = wins * 100.0 / Seeds;
        }
        return L;
    }

    static double Per(long v, long n) => n == 0 ? 0 : v / (double)n;

    /// <summary>
    /// 脆弱（<c>dmg + dmg / 2</c>・<b>整数</b>）だけが効いたときの「自弁 ÷ 請求」。
    /// <b>cost = 1 では 100%</b>（切り捨てで増幅が 0 になる。第126期「整数の切り捨て」）。
    /// </summary>
    static double FrailOnly(ShatterRule r)
        => r.SelfCostPerTurn <= 0 ? 0
         : (r.SelfCostPerTurn + r.SelfCostPerTurn / 2) * 100.0 / r.SelfCostPerTurn;
    static string W(double[] v) => $"{v[0]:F1} / {v[1]:F1} / {v[2]:F1} / {v[3]:F1} / {v[4]:F1}";
    static double Avg25(double[] v) => (v[1] + v[2] + v[3] + v[4]) / 4.0;

    // =================================================================================
    // phase0 —— Q0-1（現行は何点配り、何点吸っているか）
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第137期 Phase 0 —— 砕けの現行の帳簿（Q0-1 / Q0-3）");
        Console.WriteLine();
        Console.WriteLine($"分母: **第2〜5波 × seed 0..{Seeds - 1}**（規約 (G10)。第一波は走らせるが分母に入れない）。");
        Console.WriteLine("**紙からは導かない**（第133期）——`ShatterTrait` のコメントの「1戦の被弾110 / 味方1体あたり30弱」は");
        Console.WriteLine("盤面も敵も違う時期の数字なので使わない。");
        Console.WriteLine();

        Console.WriteLine("## Q0-3 —— ヒビを含む行（**走らせて引いた**）");
        Console.WriteLine();
        Console.WriteLine($"`Presets.Compare` ＝ **{Presets.Compare.Length} 行** ／ `Presets.Cross` ＝ **{Presets.Cross.Length} 行**。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | ヒビの席 |");
        Console.WriteLine("|---|---|---|");
        foreach ((string band, string name, Formation f) in HibiRows())
        {
            string slot = f.Occupied().First(o => o.Def.Id == "hibi").Slot.ToString();
            Console.WriteLine($"| {band} | {name} | {slot} |");
        }
        Console.WriteLine();
        Console.WriteLine("> **指示書の「3行」は `compare` に限れば正しいが、交差帯の 2 行を数え落としている。**");
        Console.WriteLine("> 測れる既存行は **3 ではなく 5**。");
        Console.WriteLine();

        Console.WriteLine("## Q0-1 —— 現行（`Passive`）の帳簿");
        Console.WriteLine();
        Console.WriteLine("`配布/戦` は受け手の人数ぶん合算した総量（味方1体あたりは ÷ 4）。");
        Console.WriteLine("**`吸った` は破片のプール全体**（集約・鱗の死からの供給と混ざる）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 勝率(1..5波) | 発火/戦 | 配布/戦 | 1体あたり | 吸った/戦 | 吸率 | ヒビ被弾/戦 | ヒビ生存T | ヒビ死亡率 |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|");
        var tot = new Led();
        foreach ((string band, string name, Formation f) in HibiRows())
        {
            Led L = Run(f, ShatterRule.Default);
            tot.Battles += L.Battles; tot.Ticks += L.Ticks; tot.Given += L.Given;
            tot.Soaked += L.Soaked; tot.HibiTaken += L.HibiTaken; tot.HibiLife += L.HibiLife;
            tot.HibiDeaths += L.HibiDeaths;
            Console.WriteLine($"| {name} | {W(L.Win)} | {Per(L.Ticks, L.Battles):F2} | {Per(L.Given, L.Battles):F1} | "
                + $"{Per(L.Given, L.Battles) / 4.0:F1} | {Per(L.Soaked, L.Battles):F1} | "
                + $"{(L.Given == 0 ? 0 : L.Soaked * 100.0 / L.Given):F1}% | "
                + $"{Per(L.HibiTaken, L.Battles):F1} | {Per(L.HibiLife, L.Battles):F2} | "
                + $"{Per(L.HibiDeaths * 100, L.Battles):F1}% |");
        }
        Console.WriteLine($"| **5行 合計** | — | **{Per(tot.Ticks, tot.Battles):F2}** | **{Per(tot.Given, tot.Battles):F1}** | "
            + $"**{Per(tot.Given, tot.Battles) / 4.0:F1}** | **{Per(tot.Soaked, tot.Battles):F1}** | "
            + $"**{(tot.Given == 0 ? 0 : tot.Soaked * 100.0 / tot.Given):F1}%** | "
            + $"**{Per(tot.HibiTaken, tot.Battles):F1}** | **{Per(tot.HibiLife, tot.Battles):F2}** | "
            + $"**{Per(tot.HibiDeaths * 100, tot.Battles):F1}%** |");
        Console.WriteLine();

        // §4-2 の掃引の中心を実測から引く（紙から決めない）
        double given = Per(tot.Given, tot.Battles), life = Per(tot.HibiLife, tot.Battles);
        double center = life <= 0 ? 0 : given / 4.0 / life;
        Console.WriteLine("## §4-2 —— 掃引の中心（**実測から引く**）");
        Console.WriteLine();
        Console.WriteLine($"「現行の**味方1体あたり**の配布総量 ÷ ヒビの平均生存T」 ＝ {given / 4.0:F1} ÷ {life:F2} ＝ **{center:F2}**。");
        Console.WriteLine($"自前の鍵では 1 回の自傷 `cost` が（脆弱 ×1.5 を通って）そのまま1体あたりの配布量になるので、");
        Console.WriteLine($"**`SelfCostPerTurn` の中心は {Math.Max(1, (int)Math.Round(center / 1.5))} 前後**（脆弱のぶん割り戻す）。上下1点ずつ振る。");
        Console.WriteLine();
        Console.WriteLine("> **物差し**: ある駒が編成に加わる価値は、少なくとも「その駒が受ける被弾」を上回らなければならない。");
        Console.WriteLine($"> 現行は **吸った {Per(tot.Soaked, tot.Battles):F1} 対 ヒビ被弾 {Per(tot.HibiTaken, tot.Battles):F1}**"
            + $" ＝ 比 **{(tot.HibiTaken == 0 ? 0 : tot.Soaked / (double)tot.HibiTaken):F2}**。");
        Console.WriteLine();

        StockTable();
    }

    // =================================================================================
    // 第138期 Q0-1 —— 破片の在庫の時系列（**ターン頭**）
    //
    // **`shard` に新しいモードを作らない**（第138期 指示書 §4-1）ので `phase0` に節を足した。
    // 礫（`TraitId.Shrapnel`）が1回に砕ける量は「配布の総量」ではなく
    // **「その時点で最も多く纏っている1体の在庫」**で決まるので、そこを直接数える。
    // =================================================================================

    /// <summary>1行ぶんの在庫の帳簿（味方側だけを見る。敵側は在庫が構造的に 0＝Q0-7）。</summary>
    sealed class Stock
    {
        public long Battles, Turns;
        public long StockSum, TopSum, TurnsAny, Holders;
        public long StockMax, TopMax;
        public readonly Dictionary<string, (long Times, long Sum)> TopBy = new();
    }

    static Stock RunStock(Formation f)
    {
        var S = new Stock();
        for (int st = 1; st < EnemyCatalog.Stages.Count; st++)   // 第2〜5波（規約 (G10)）
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                  verbose: false, armor: new ArmorRule(true));
                ArmorLedger a = r.Armor;
                S.Battles++;
                S.Turns += a.Turns;
                S.StockSum += a.StockSum[1]; S.TopSum += a.TopSum[1];
                S.TurnsAny += a.TurnsAny[1]; S.Holders += a.Holders[1];
                if (a.StockMax[1] > S.StockMax) S.StockMax = a.StockMax[1];
                if (a.TopMax[1] > S.TopMax) S.TopMax = a.TopMax[1];
                foreach ((string id, (long t, long sum)) in a.TopBy)
                {
                    (long pt, long ps) = S.TopBy.TryGetValue(id, out var prev) ? prev : (0, 0);
                    S.TopBy[id] = (pt + t, ps + sum);
                }
            }
        return S;
    }

    static void StockTable()
    {
        Console.WriteLine("## 第138期 Q0-1 —— 破片の在庫（**ターン頭**・味方側）");
        Console.WriteLine();
        Console.WriteLine("分母は**ターン頭の数**（`ArmorLedger.Turns`）。写す位置は `TickStatuses` の後・`OnTurnStart` の**前**");
        Console.WriteLine("——**そのターンの供給が乗る前**の在庫で、これが礫（手番＝行動順ループ）が見る在庫に一番近い。");
        Console.WriteLine();
        Console.WriteLine("`盤面` は生存している味方全員の在庫の和、`最大保持` は**そのターン最も多く纏っている1体**の在庫。");
        Console.WriteLine("**礫が1回に砕ける量の見積もりは `最大保持` の列**である（`盤面` ではない）。");
        Console.WriteLine();
        Console.WriteLine("| 台／行 | ターン頭/戦 | 盤面 平均 | 盤面 最大 | **最大保持 平均** | 最大保持 最大 | 在庫のあるT率 | 保持者/T |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|");

        var all = new List<(string Name, Formation F)>();
        foreach ((string name, Formation f) in Rigs()) all.Add((name, f));
        foreach ((string band, string name, Formation f) in HibiRows()) all.Add((name, f));

        var agg = new Dictionary<string, (long Times, long Sum)>();
        foreach ((string name, Formation f) in all)
        {
            Stock S = RunStock(f);
            Console.WriteLine($"| {name} | {Per(S.Turns, S.Battles):F2} | {Per(S.StockSum, S.Turns):F1} | {S.StockMax} | "
                + $"**{Per(S.TopSum, S.Turns):F1}** | {S.TopMax} | {Per(S.TurnsAny * 100, S.Turns):F1}% | "
                + $"{Per(S.Holders, S.Turns):F2} |");
            foreach ((string id, (long t, long sum)) in S.TopBy)
            {
                (long pt, long ps) = agg.TryGetValue(id, out var prev) ? prev : (0, 0);
                agg[id] = (pt + t, ps + sum);
            }
        }
        Console.WriteLine();

        long total = agg.Values.Sum(v => v.Times);
        Console.WriteLine("### 最大保持者は誰か（全台・全行の合算）");
        Console.WriteLine();
        Console.WriteLine("**同値のときは走査順（スロット昇順）で先に来た1体を数えている**ので、同値のぶんだけ先着に偏る。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 最大保持だったT | 割合 | そのときの在庫 平均 | 鱗（ウロ）か |");
        Console.WriteLine("|---|---:|---:|---:|---|");
        foreach ((string id, (long t, long sum)) in agg.OrderByDescending(kv => kv.Value.Times))
        {
            UnitDef d = UnitCatalog.All.FirstOrDefault(u => u.Id == id)
                     ?? UnitCatalog.All.First();
            string nm = d.Id == id ? d.Name : id;
            bool scale = d.Id == id && d.Traits is not null && d.Traits.Contains(TraitId.Scale);
            Console.WriteLine($"| {nm} | {t} | {(total == 0 ? 0 : t * 100.0 / total):F1}% | "
                + $"{Per(sum, t):F1} | {(scale ? "**○**" : "—")} |");
        }
    }

    // =================================================================================
    // scan —— 段1 の台の下見（**版を1つも振らない**。第133期の則）
    // =================================================================================

    static void Scan()
    {
        Console.WriteLine("# 第137期 段1 の台の下見（**版を1つも振らない**）");
        Console.WriteLine();
        Console.WriteLine("第61期（素体の5波平均 40% 以上）・第63期（40〜95% の両側で切る）・第133期");
        Console.WriteLine("（段1 のローカル台にも床と天井の規則を当てる）。**`V0` の第2〜5波平均が 40〜95% に入る台だけを使う。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | V0 勝率(1..5波) | 第2〜5波平均 | 帯 |");
        Console.WriteLine("|---|---|---:|---|");
        foreach ((string name, Formation f) in Rigs())
        {
            Led L = Run(f, ShatterRule.Default);
            double a = Avg25(L.Win);
            string band = a < 40 ? "**床（使わない）**" : a > 95 ? "**天井（使わない）**" : "○";
            Console.WriteLine($"| {name} | {W(L.Win)} | {a:F1} | {band} |");
        }
        Console.WriteLine();
        Console.WriteLine("既存5行も同じ物差しで見る（回帰と拒否権に使うので床・天井でも外さないが、記録する）:");
        Console.WriteLine();
        Console.WriteLine("| 行 | V0 勝率(1..5波) | 第2〜5波平均 |");
        Console.WriteLine("|---|---|---:|");
        foreach ((string band, string name, Formation f) in HibiRows())
        {
            Led L = Run(f, ShatterRule.Default);
            Console.WriteLine($"| {name} | {W(L.Win)} | {Avg25(L.Win):F1} |");
        }
    }

    // =================================================================================
    // run —— 掃引
    // =================================================================================

    static void Sweep(string arg)
    {
        int[] pts = { 2, 4, 6 };
        if (!string.IsNullOrWhiteSpace(arg))
        {
            var p = arg.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(t => int.TryParse(t.Trim(), out int v) ? v : -1)
                       .Where(v => v > 0).ToArray();
            if (p.Length > 0) pts = p;
        }

        var versions = new List<(string Tag, ShatterRule R)> { ("V0 現行 (Passive)", ShatterRule.Default) };
        foreach (int c in pts) versions.Add(($"V1 自前 cost={c}", new ShatterRule(ShatterMode.Turn, c)));
        foreach (int c in pts) versions.Add(($"V2 両方 cost={c}", new ShatterRule(ShatterMode.Both, c)));

        Console.WriteLine("# 第137期 段1 —— 砕けの鍵を自前にする（掃引）");
        Console.WriteLine();
        Console.WriteLine($"第2〜5波 × seed 0..{Seeds - 1}。掃引点は `{string.Join(" / ", pts)}`。");
        Console.WriteLine("**`V2 両方` は対照で採用候補ではない**（どちらが効いたか分離できない）。");
        Console.WriteLine();

        var rigs = Rigs().Select(x => (x.Name, x.F)).ToList();
        var rows = HibiRows().Select(x => (x.Name, x.F)).ToList();

        foreach ((string label, List<(string Name, Formation F)> set) in
                 new[] { ("ローカル台（主判定）", rigs), ("既存行（回帰と拒否権）", rows) })
        {
            Console.WriteLine($"## {label}");
            Console.WriteLine();
            foreach ((string name, Formation f) in set)
            {
                Console.WriteLine($"### {name}");
                Console.WriteLine();
                Console.WriteLine("| 版 | 勝率(1..5波) | 2〜5平均 | Δ | 発火/戦 | 配布/戦 | 吸った/戦 | 物差し | 請求/戦 | 自弁/戦 | 自弁÷請求 | 脆弱のみの期待値 | 纏い率 | ヒビ生存T | ヒビ死亡率 |");
                Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
                double v0 = 0;
                foreach ((string tag, ShatterRule rule) in versions)
                {
                    Led L = Run(f, rule);
                    double a = Avg25(L.Win);
                    if (tag.StartsWith("V0")) v0 = a;
                    double soaked = Per(L.Soaked, L.Battles), taken = Per(L.HibiTaken, L.Battles);
                    string yard = taken <= 0 ? "—" : $"{soaked / taken:F2}";
                    // **「自弁率」ではない（第121期の則）。**
                    // 分子はヒビが<b>実際に失った HP</b>（脆弱の後・肩代わりの後）、
                    // 分母は<b>請求額（脆弱の前）</b>なので、比は3つの効果の積になる:
                    //   脆弱（+50%・ただし `dmg + dmg / 2` の**整数**なので cost=1 では 0）
                    //   HP1 のクランプ（−）と 肩代わり（−）。
                    // **脆弱のみの期待値を隣に並べる**ので、下回れば残り2つが動いていると読める。
                    string self = L.Paid == 0 ? "—" : $"{L.PaidSelf * 100.0 / L.Paid:F1}%";
                    string worn = L.AliveTurns == 0 ? "—" : $"{L.WornTurns * 100.0 / L.AliveTurns:F1}%";
                    Console.WriteLine($"| {tag} | {W(L.Win)} | {a:F1} | {a - v0:+0.0;-0.0;0.0} | "
                        + $"{Per(L.Ticks, L.Battles):F2} | {Per(L.Given, L.Battles):F1} | {soaked:F1} | {yard} | "
                        + $"{Per(L.Paid, L.Battles):F1} | {Per(L.PaidSelf, L.Battles):F1} | {self} | {FrailOnly(rule):F1}% | {worn} | "
                        + $"{Per(L.HibiLife, L.Battles):F2} | {Per(L.HibiDeaths * 100, L.Battles):F1}% |");
                }
                Console.WriteLine();
            }
        }

        // §1-5 の切り分け（ガルドを含む台・行だけ）
        Console.WriteLine("## §1-5 —— 破片の soak と受け流しの在庫は食い合うか");
        Console.WriteLine();
        Console.WriteLine("破片は `ApplyDamage` の中で受け流しより**手前**にあり、吸い切ると `amount <= 0` で return するので、");
        Console.WriteLine("**破片が先に吸うと受け流しの在庫が減らない**（＝ガルドが長持ちする）方向を予測した。");
        Console.WriteLine();
        Console.WriteLine("| 台・行 | 版 | **決着T** | ガルド被弾/戦 | 受け流し/戦 | 無効化量/戦 | ガルド生存T | 生存T÷決着T | ガルド死亡率 |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|");
        // **決着T を必ず併記する**（規約 (G6)）——`LastActiveTurn` は戦闘が長引けば伸びるので、
        // 生存T を単体で読むと「弱い版のほうが長生き」に見える。
        foreach ((string name, Formation f) in rigs.Concat(rows))
        {
            if (!f.Occupied().Any(o => o.Def.Id == "gald")) continue;
            foreach ((string tag, ShatterRule rule) in versions)
            {
                Led L = Run(f, rule);
                double turns = Per(L.Turns, L.Battles), glife = Per(L.GaldLife, L.Battles);
                Console.WriteLine($"| {name} | {tag} | {turns:F2} | {Per(L.GaldTaken, L.Battles):F1} | "
                    + $"{Per(L.GaldParry, L.Battles):F2} | {Per(L.GaldParryAmt, L.Battles):F1} | "
                    + $"{glife:F2} | {(turns <= 0 ? 0 : glife / turns):F2} | {Per(L.GaldDeaths * 100, L.Battles):F1}% |");
            }
        }
        Console.WriteLine();

        // 内訳（ウロを含む台・行だけ）
        Console.WriteLine("## 破片の供給の内訳（ウロを含む台・行だけ）");
        Console.WriteLine();
        Console.WriteLine("`ShatterSoaked` はプール全体なので、鱗の2つの供給（砕け / 味方の死）を分けて出す。");
        Console.WriteLine();
        Console.WriteLine("| 台・行 | 版 | ウロが砕けから | ウロが死から | 纏い率 |");
        Console.WriteLine("|---|---|---:|---:|---:|");
        foreach ((string name, Formation f) in rigs.Concat(rows))
        {
            if (!f.Occupied().Any(o => o.Def.Id == "uro")) continue;
            foreach ((string tag, ShatterRule rule) in versions)
            {
                Led L = Run(f, rule);
                Console.WriteLine($"| {name} | {tag} | {Per(L.GainShatter, L.Battles):F1} | "
                    + $"{Per(L.GainDeath, L.Battles):F1} | "
                    + $"{(L.AliveTurns == 0 ? 0 : L.WornTurns * 100.0 / L.AliveTurns):F1}% |");
            }
        }
    }

    // =================================================================================
    // check —— 自己検査
    // =================================================================================

    static void Check(string baseline)
    {
        Console.WriteLine("# 第137期 自己検査");
        Console.WriteLine();

        // (1) compare 305 セルが docs/balance.md と 0 件
        Console.WriteLine("## 必須1 —— 既定で `compare` 61行 × 5波 の 305 セルが `docs/balance.md` と 0 件差分");
        Console.WriteLine();
        if (string.IsNullOrWhiteSpace(baseline) || !File.Exists(baseline))
        {
            Console.WriteLine("`docs/balance.md` のパスを引数で渡すと突き合わせる（例: `shard check docs/balance.md`）。");
            Console.WriteLine("**この節を飛ばしたまま採用してはいけない。**");
        }
        else
        {
            var want = new Dictionary<string, double[]>(StringComparer.Ordinal);
            foreach (string line in File.ReadAllLines(baseline))
            {
                if (!line.StartsWith("| ") || !line.Contains('%')) continue;
                string[] c = line.Split('|', StringSplitOptions.TrimEntries);
                if (c.Length < 8) continue;
                var v = new double[5];
                bool ok = true;
                for (int i = 0; i < 5; i++)
                    if (!double.TryParse(c[2 + i].Replace("%", "").Replace("*", "").Trim(), out v[i])) { ok = false; break; }
                if (ok) want[c[1].Replace("*", "").Trim()] = v;
            }
            int cells = 0, diff = 0;
            foreach ((string name, Formation f) in Presets.Compare)
            {
                if (!want.TryGetValue(name, out double[]? w)) { Console.WriteLine($"- 行名が引けない: `{name}`"); continue; }
                for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
                {
                    int wins = 0;
                    for (int seed = 0; seed < Seeds; seed++)
                        if (BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false).PlayerWon) wins++;
                    double got = wins * 100.0 / Seeds;
                    cells++;
                    if (Math.Abs(got - w[st]) > 0.05) { diff++; Console.WriteLine($"- **差分**: {name} 第{st + 1}波 {w[st]:F1} → {got:F1}"); }
                }
            }
            Console.WriteLine($"{cells} セル中 **{diff} 件**差分。{(diff == 0 ? "**0 件。**" : "**0 件でない。採用してはいけない。**")}");
        }
        Console.WriteLine();

        // (a) 既定が Passive / 0 であること
        Console.WriteLine("## (a) —— ノブの既定");
        Console.WriteLine();
        ShatterRule d = ShatterRule.Default;
        Console.WriteLine($"`ShatterRule.Default` ＝ `Mode = {d.Mode}` / `SelfCostPerTurn = {d.SelfCostPerTurn}`。"
            + $"{(d.Mode == ShatterMode.Passive && d.SelfCostPerTurn == 0 ? "**現行のまま。**" : "**動いている。**")}");
        Console.WriteLine();

        // (b) SelfCostPerTurn = 0 の Turn/Both が V0 と 1 セルも違わない
        Console.WriteLine("## (b) —— `SelfCostPerTurn = 0` は `Passive` と1セルも違わない");
        Console.WriteLine();
        Console.WriteLine("**自前の鍵は供給を止めるので、`Turn` は 0 でも `Passive` と同値にはならない**（受動の供給が消える）。");
        Console.WriteLine("同値になるべきなのは **`Both` / 0** のほう——受動がそのまま走り、自傷だけが 0 になる。");
        Console.WriteLine();
        Console.WriteLine("| 行 | `Passive` | `Both`/0 | 一致 | `Turn`/0 | 一致 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach ((string band, string name, Formation f) in HibiRows())
        {
            Led a = Run(f, ShatterRule.Default);
            Led b = Run(f, new ShatterRule(ShatterMode.Both, 0));
            Led c = Run(f, new ShatterRule(ShatterMode.Turn, 0));
            bool okB = a.Win.Zip(b.Win).All(t => Math.Abs(t.First - t.Second) < 1e-9);
            bool okT = a.Win.Zip(c.Win).All(t => Math.Abs(t.First - t.Second) < 1e-9);
            Console.WriteLine($"| {name} | {W(a.Win)} | {W(b.Win)} | {(okB ? "○" : "**×**")} | {W(c.Win)} | {(okT ? "○（供給が元から 0 の行）" : "× （期待どおり・受動が消えた）")} |");
        }
        Console.WriteLine();

        // (c) ヒビを含まない行は版に依らない
        Console.WriteLine("## (c) —— ヒビを含まない行は版に依らない（特異性）");
        Console.WriteLine();
        var hibi = new HashSet<string>(HibiRows().Select(x => x.Name), StringComparer.Ordinal);
        int rowsChecked = 0, cellsDiff = 0;
        var test = new ShatterRule(ShatterMode.Turn, 4);
        foreach ((var src, string band) in new[] { (Presets.Compare, "compare"), (Presets.Cross, "交差帯") })
            foreach ((string name, Formation f) in src)
            {
                if (hibi.Contains(name)) continue;
                rowsChecked++;
                Led a = Run(f, ShatterRule.Default);
                Led b = Run(f, test);
                for (int i = 0; i < 5; i++)
                    if (Math.Abs(a.Win[i] - b.Win[i]) > 1e-9)
                    { cellsDiff++; Console.WriteLine($"- **差分**: {name} 第{i + 1}波 {a.Win[i]:F1} → {b.Win[i]:F1}"); }
            }
        Console.WriteLine($"ヒビを含まない **{rowsChecked} 行 / {rowsChecked * 5} セル**中 **{cellsDiff} 件**差分"
            + $"（版は `Turn` / cost 4）。{(cellsDiff == 0 ? "**0 件。**" : "**0 件でない。**")}");
        Console.WriteLine();

        // (d) HP1 で供給が止まること
        Console.WriteLine("## (d) —— HP1 では供給が止まる（Q0-5 の実証）");
        Console.WriteLine();
        Console.WriteLine("`lethal: false` のクランプが `amount` を 0 にして即 return するので、減る HP も配る量も 0 になる。");
        Console.WriteLine("**代金の総額（`ShatterPaid`）と自弁額（`ShatterPaidSelf`）が乖離する**ことで見える");
        Console.WriteLine("——乖離の出どころは (i) HP1 での不払い と (ii) 肩代わり の 2 つで、後者は台4 の自弁率で分ける。");
        Console.WriteLine();
        Console.WriteLine("| 台 | cost | 代金/戦 | 自弁/戦 | 自弁率 | 発火/戦 | ヒビ生存T | 発火 ÷ 生存T |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach ((string name, Formation f) in Rigs())
            foreach (int c in new[] { 2, 4, 6 })
            {
                Led L = Run(f, new ShatterRule(ShatterMode.Turn, c));
                double life = Per(L.HibiLife, L.Battles), fires = Per(L.Ticks, L.Battles);
                Console.WriteLine($"| {name} | {c} | {Per(L.Paid, L.Battles):F1} | {Per(L.PaidSelf, L.Battles):F1} | "
                    + $"{(L.Paid == 0 ? 0 : L.PaidSelf * 100.0 / L.Paid):F1}% | {fires:F2} | {life:F2} | "
                    + $"{(life <= 0 ? 0 : fires / life):F2} |");
            }
        Console.WriteLine();
        Console.WriteLine("> §3-2: **発火回数/戦がヒビの生存T とほぼ一致する**（毎ターン払っているのだから）。");
        Console.WriteLine("> 一致しないなら払えていないターンがある＝HP1 の張り付きが起きている。");
        Console.WriteLine();

        GareCheck();
    }

    // =================================================================================
    // 第138期 —— 礫（ガレ）のローカル台と測定
    //
    // **`Presets` には1行も足さない**（指示書 Q0-5）。ガレは `UnitCatalog.All` にも入っていないので、
    // `compare` 61行・交差帯 12行は `ShrapnelRule` を何に振っても1バイトも動かない。
    // =================================================================================

    /// <summary>素体（同じ数値・特性なし・<c>Actions</c> も落とす）。第69期の標準器具。</summary>
    static UnitDef Plain(UnitDef d) => new()
    {
        Id = d.Id + "_plain",
        Name = "素体の" + d.Name,
        MaxHp = d.MaxHp,
        Attack = d.Attack,
        Speed = d.Speed,
        Pattern = d.Pattern,
        Advances = d.Advances,
        Traits = Array.Empty<TraitId>()
    };

    static readonly UnitDef GarePlain = Plain(UnitCatalog.Gare);

    /// <summary>
    /// 段4 のローカル台（指示書 §4-2）。<b>V0 は「同じ台のガレを素体に落とした版」</b>
    /// ——席も他の4枚も1枚も動かさないので、差はまるごと礫の帰属になる。
    ///
    /// <para><b>台C（供給なし）は破片を1点も書かない4枚で組む</b>——
    /// 破片の書き手は 砕け（ヒビ）／集約（ウケ）／鱗（ウロ・味方の死から）の3本だけなので、
    /// その3枚を外せばどの5枚でも「供給なし」になる。</para>
    /// </summary>
    /// <summary>
    /// 埋め草2枚（前1・前3）。<b>破片を1点も書かない・読まない駒から選ぶ</b>
    /// ——書き手は 砕け（ヒビ）／集約（ウケ）／鱗（ウロ・味方の死から）の3本だけ、読み手は鱗だけ。
    /// <b>組は `gscan probe` で走査して決めた</b>（第137期と同じ作法。理由は報告書 §段4）。
    /// </summary>
    /// <b>前1 ドルガ ／ 前3 ガルド</b>。`gscan probe` の 42 組で<b>4台とも 40〜95% に入ったのは
    /// ドルガ×ガルドの2通りだけ</b>（左右を入れ替えた版）で、そのうち台D の天井に余裕がある側を採った
    /// （89.9% 対 94.9%）。<b>ガルドは受け流し（第136期）を持つが破片は1点も書かず読まない</b>ので、
    /// 礫の帰属には入らない——ただし<b>壁として台を成立させているのはこの1枚</b>である。
    static UnitDef[] _fillers = { UnitCatalog.Dolga, UnitCatalog.Gald };

    /// <summary>
    /// 段4 のローカル台（指示書 §4-2）。<b>V0 は「同じ台のガレを素体に落とした版」</b>
    /// ——席も他の4枚も1枚も動かさないので、差はまるごと礫の帰属になる。
    ///
    /// <para><b>4台は「後1 の1枚」と「中央の1枚」だけで分岐する梯子</b>にしてある:
    /// A→B は 後1（ウロ ＝ 読み手 → エグ ＝ 読まない）、B→C は 中央（ヒビ ＝ 供給 → ネル ＝ 供給なし）、
    /// B→D は 後1（エグ → ガン ＝ 手番の買い手）。<b>1枚しか違わないので差の帰属が濁らない。</b></para>
    /// </summary>
    static (string Name, Formation F)[] GareRigs()
    {
        Formation Rig(UnitDef center, UnitDef back1) => Formation.Build(
            front1: _fillers[0], front3: _fillers[1],
            center: center, back1: back1, back3: UnitCatalog.Gare);

        return new (string, Formation)[]
        {
            // 供給あり・読み手あり: 礫とウロが同じ在庫を取り合う（予測 P2）。
            ("台A 取り合い (ガレ×ヒビ×ウロ)", Rig(UnitCatalog.Hibi, UnitCatalog.Uro)),

            // 供給あり・読み手なし: 出口だけがある。**台A との差は 後1 の1枚だけ。**
            ("台B 出口だけ (ガレ×ヒビ)", Rig(UnitCatalog.Hibi, UnitCatalog.Egu)),

            // 供給なし: 破片を1点も書かない4枚。**台B との差は 中央 の1枚だけ。**
            // **手番を捨て続ける台**（予測 P3）。
            ("台C 供給なし (ガレ単騎)", Rig(UnitCatalog.Nel, UnitCatalog.Egu)),

            // 手番市場: 号令（ガン）が礫の捨てた手番を買い取るか（Q0-4）。**台B との差は 後1 の1枚だけ。**
            ("台D 手番市場 (ガレ×ヒビ×ガン)", Rig(UnitCatalog.Hibi, UnitCatalog.Gan)),
        };
    }

    /// <summary>
    /// 埋め草の組の走査（第138期 段4）。<b>版を1つも振らない。</b>
    /// 4台すべての V0（ガレを素体に落とした版）が <b>40〜95%</b> に入る組を探す
    /// ——第61期（40% 以上）・第63期（40〜95% の両側）・第133期（段1 のローカル台にも当てる）。
    /// </summary>
    static void GareProbe(string ids)
    {
        // 破片を書く・読む3枚（ヒビ／ウケ／ウロ）と、台の役の2枚（ガン）は候補から外す。
        var block = new HashSet<string>(StringComparer.Ordinal) { "hibi", "uke", "uro", "gan", "gare" };
        UnitDef[] cand = string.IsNullOrWhiteSpace(ids)
            ? new[] { UnitCatalog.Dolga, UnitCatalog.Borg, UnitCatalog.Mudo,
                      UnitCatalog.Zan, UnitCatalog.Sekki, UnitCatalog.Kiri, UnitCatalog.Sid }
            : ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
                 .Select(t => t.Trim())
                 .Where(t => !block.Contains(t))
                 .Select(t => UnitCatalog.All.FirstOrDefault(u => u.Id == t))
                 .Where(u => u is not null).Select(u => u!).ToArray();
        if (cand.Length < 2) { Console.WriteLine("候補が2枚未満。`gscan probe borg,dolga,...` の形で渡す。"); return; }

        Console.WriteLine("# 第138期 段4 —— 埋め草の組の走査（**版を1つも振らない**）");
        Console.WriteLine();
        Console.WriteLine("4台すべての **V0（ガレを素体に落とした版）が 40〜95%** に入る組を探す。");
        Console.WriteLine("候補は**破片を書かない・読まない駒**だけ（書き手 ヒビ／ウケ／ウロ・読み手 ウロ を除く）。");
        Console.WriteLine();
        Console.WriteLine("| 前1 | 前3 | 台A | 台B | 台C | 台D | 4台とも帯の中 |");
        Console.WriteLine("|---|---|---:|---:|---:|---:|---|");

        UnitDef[] save = _fillers;
        for (int i = 0; i < cand.Length; i++)
            for (int j = 0; j < cand.Length; j++)
            {
                if (i == j) continue;
                _fillers = new[] { cand[i], cand[j] };
                var v = new double[4];
                int k = 0;
                foreach ((string _, Formation f) in GareRigs())
                    v[k++] = Avg25(RunGare(Without(f), ShrapnelRule.Default).Win);
                bool ok = v.All(x => x >= 40 && x <= 95);
                Console.WriteLine($"| {cand[i].Name} | {cand[j].Name} | {v[0]:F1} | {v[1]:F1} | {v[2]:F1} | {v[3]:F1} | "
                    + $"{(ok ? "**○**" : "—")} |");
            }
        _fillers = save;
        Console.WriteLine();
        Console.WriteLine("> **席の左右（前1 / 前3）も振ってある**——盤面はX字で前1と前3は次数2で等価だが、");
        Console.WriteLine("> 薙ぎの巻き込み（`SweepTargets`）は非対称なので同じ2枚でも値が違う。");
    }

    /// <summary>ガレを素体に落とした版（V0）。<b>席も他の4枚も1枚も動かさない。</b></summary>
    static Formation Without(Formation f)
    {
        Formation c = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (d.Id == "gare") c[slot] = GarePlain;
        return c;
    }

    sealed class GLed
    {
        public long Battles, Turns;
        public long Fires, Idles, Shards, Dealt, Hits, SelfHarm, FoeTargets;
        public long Ticks, Given, WornTurns, AliveTurns;
        public long GareLife, GareDeaths, GareStun, GareDealt;
        public long HibiLife, UroLife;
        public readonly double[] Win = new double[5];
    }

    static GLed RunGare(Formation f, ShrapnelRule rule)
    {
        var L = new GLed();
        for (int st = 0; st < EnemyCatalog.Stages.Count; st++)
        {
            int wins = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                  verbose: false, shrapnel: rule);
                if (r.PlayerWon) wins++;
                if (st == 0) continue;   // 規約 (G10)

                L.Battles++;
                L.Turns += r.Turns;
                L.Fires += r.ShrapnelFires;
                L.Shards += r.ShrapnelShards;
                L.Dealt += r.ShrapnelDealt;
                L.Hits += r.ShrapnelHits;
                L.SelfHarm += r.ShrapnelSelfHarm;
                L.FoeTargets += r.ShrapnelFoeTargets;
                L.Ticks += r.ShatterTicks;
                L.Given += r.ShatterGiven;
                L.WornTurns += r.ScaleWornTurns;
                L.AliveTurns += r.ScaleAliveTurns;
                if (r.TallyByUnit.TryGetValue("gare", out UnitTally? tg))
                {
                    L.Idles += tg.StallCanAct;
                    L.GareLife += tg.LastActiveTurn;
                    L.GareDeaths += tg.Deaths > 0 ? 1 : 0;
                    L.GareStun += tg.StallStun + tg.StallSlumber + tg.StallImmobile;
                    L.GareDealt += tg.DamageToEnemy;
                }
                if (r.TallyByUnit.TryGetValue("hibi", out UnitTally? th)) L.HibiLife += th.LastActiveTurn;
                if (r.TallyByUnit.TryGetValue("uro", out UnitTally? tu)) L.UroLife += tu.LastActiveTurn;
            }
            L.Win[st] = wins * 100.0 / Seeds;
        }
        return L;
    }

    /// <summary>
    /// 段4 の台の下見（<b>版を1つも振らない</b>）。第61期（40% 以上）・第63期（40〜95% の両側）・
    /// 第133期（段1 のローカル台にも床と天井の規則を当てる）。<b>V0 が床でも天井でもないこと。</b>
    /// </summary>
    static void GareScan()
    {
        Console.WriteLine("# 第138期 段4 —— 台の下見（**V0 ＝ ガレを素体に落とした版**。版は振らない）");
        Console.WriteLine();
        Console.WriteLine("| 台 | V0 勝率(1..5波) | 第2〜5波平均 | 帯 |");
        Console.WriteLine("|---|---|---:|---|");
        foreach ((string name, Formation f) in GareRigs())
        {
            GLed L = RunGare(Without(f), ShrapnelRule.Default);
            double a = Avg25(L.Win);
            string band = a < 40 ? "**床（使わない）**" : a > 95 ? "**天井（使わない）**" : "○";
            Console.WriteLine($"| {name} | {W(L.Win)} | {a:F1} | {band} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 掃引の中心を実測から引く（§4-3）");
        Console.WriteLine();
        Console.WriteLine("**「1回に砕ける量 × 発火回数」から引く**（指示書 Q0-1 の注記。1戦の総量を発火回数で割る式は使わない）。");
        Console.WriteLine("**ガレを入れた版で測り直す**——礫は在庫を消費するので、Q0-1（ガレ抜き）の値は上振れしている。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 発火/戦 | 捨て/戦 | 発火率 | **1回に砕ける量** | 破片由来/発火 | 攻撃力由来 | 条件1 を満たす最小 M |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|");
        int atk = UnitCatalog.Gare.Attack;
        foreach ((string name, Formation f) in GareRigs())
        {
            GLed L = RunGare(f, ShrapnelRule.Default);
            double fire = Per(L.Fires, L.Battles), idle = Per(L.Idles, L.Battles);
            double per = L.Fires == 0 ? 0 : L.Shards / (double)L.Fires;
            double need = per <= 0 ? 0 : Math.Floor(atk / per) + 1;
            Console.WriteLine($"| {name} | {fire:F2} | {idle:F2} | "
                + $"{(fire + idle <= 0 ? 0 : fire * 100.0 / (fire + idle)):F1}% | **{per:F2}** | "
                + $"{per * ShrapnelRule.Default.Multiplier:F1} | {atk} | {need:F0} |");
        }
        Console.WriteLine();
        Console.WriteLine($"> **採用条件1**（破片由来が攻撃力由来を上回る）は「1回に砕ける量 × M > 攻{atk}」と同値なので、");
        Console.WriteLine("> 最後の列が「その台で条件1 を満たす最小の `Multiplier`」である。**掃引点はこれを跨ぐように置く。**");
    }

    // =================================================================================
    // 第138期 段5 —— 掃引
    // =================================================================================

    static void GareSweep(string arg)
    {
        // **掃引点は `gscan` の実測から引いて先に固定した**（第64期・結果を見てから広げない）。
        // 4台の「1回に砕ける量」は 3.87〜4.05 で、採用条件1（破片由来 > 攻撃力由来 ＝ 攻9）を
        // 満たす最小の `Multiplier` は 3 だった。**その 3 を跨ぐように 2 / 3 / 4 に置く。**
        int[] pts = { 2, 3, 4 };
        if (!string.IsNullOrWhiteSpace(arg))
        {
            var q = arg.Split(',', StringSplitOptions.RemoveEmptyEntries)
                       .Select(t => int.TryParse(t.Trim(), out int v) ? v : -1)
                       .Where(v => v > 0).ToArray();
            if (q.Length > 0) pts = q;
        }

        Console.WriteLine("# 第138期 段5 —— 礫の掃引");
        Console.WriteLine();
        Console.WriteLine($"第2〜5波 × seed 0..{Seeds - 1}（規約 (G10)。第一波は走らせるが分母に入れない）。");
        Console.WriteLine($"掃引点は `Multiplier = {string.Join(" / ", pts)}`。**`SelfDamagePercent` は 100 で固定**（指示書 §1-4）。");
        Console.WriteLine("**V0 ＝ 同じ台のガレを素体に落とした版**（席も他の4枚も動かさない）。");
        Console.WriteLine();

        foreach ((string name, Formation f) in GareRigs())
        {
            Console.WriteLine($"## {name}");
            Console.WriteLine();
            GLed v0 = RunGare(Without(f), ShrapnelRule.Default);
            Console.WriteLine("| 版 | 勝率(1..5波) | 第2〜5波平均 | 帰属 | 発火/戦 | 捨て/戦 | 砕いた/戦 | 名目/戦 | 実通/戦 | 通し率 | 自傷/戦 | 破片由来 | 決着T |");
            Console.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            Console.WriteLine($"| **V0 素体** | {W(v0.Win)} | {Avg25(v0.Win):F1} | — | — | — | — | — | — | — | — | — | {Per(v0.Turns, v0.Battles):F2} |");
            foreach (int m in pts)
            {
                GLed L = RunGare(f, new ShrapnelRule(m, 100));
                double nominal = Per(L.Dealt, L.Battles), actual = Per(L.GareDealt, L.Battles);
                double shardPart = L.Fires == 0 ? 0 : L.Shards / (double)L.Fires * m;
                double atkPart = UnitCatalog.Gare.Attack;
                Console.WriteLine($"| V1 M={m} | {W(L.Win)} | {Avg25(L.Win):F1} | {Avg25(L.Win) - Avg25(v0.Win):+0.0;-0.0;0.0} | "
                    + $"{Per(L.Fires, L.Battles):F2} | {Per(L.Idles, L.Battles):F2} | {Per(L.Shards, L.Battles):F1} | "
                    + $"{nominal:F1} | {actual:F1} | {(nominal <= 0 ? 0 : actual * 100.0 / nominal):F1}% | "
                    + $"{Per(L.SelfHarm, L.Battles):F1} | "
                    + $"{(shardPart + atkPart <= 0 ? 0 : shardPart * 100.0 / (shardPart + atkPart)):F1}% | "
                    + $"{Per(L.Turns, L.Battles):F2} |");
            }
            Console.WriteLine();
        }

        Console.WriteLine("## 予測 P2 —— ウロの纏い率（取り合い）");
        Console.WriteLine();
        Console.WriteLine("礫は「最大保持者」を選ぶので、破片を溜めたウロが真っ先に砕かれて `Armor > 0` が切れる。");
        Console.WriteLine("**纏い率（`ScaleWornTurns ÷ ScaleAliveTurns`）が下がり、かつ勝率が上がれば第137期の逆**（予測 P7）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | ウロ纏い率 | 第2〜5波平均 | ウロ生存T |");
        Console.WriteLine("|---|---|---:|---:|---:|");
        foreach ((string name, Formation f) in GareRigs())
        {
            if (!f.Occupied().Any(o => o.Def.Id == "uro")) continue;
            GLed v0 = RunGare(Without(f), ShrapnelRule.Default);
            Console.WriteLine($"| {name} | V0 素体 | {(v0.AliveTurns == 0 ? 0 : v0.WornTurns * 100.0 / v0.AliveTurns):F1}% | "
                + $"{Avg25(v0.Win):F1} | {Per(v0.UroLife, v0.Battles):F2} |");
            foreach (int m in pts)
            {
                GLed L = RunGare(f, new ShrapnelRule(m, 100));
                Console.WriteLine($"| {name} | V1 M={m} | {(L.AliveTurns == 0 ? 0 : L.WornTurns * 100.0 / L.AliveTurns):F1}% | "
                    + $"{Avg25(L.Win):F1} | {Per(L.UroLife, L.Battles):F2} |");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 予測 P6 —— 第四波（軛・1発 25 上限）で切られるか");
        Console.WriteLine();
        Console.WriteLine("`名目` は `砕いた量 × M ＋ 攻9` × 着弾体数、`実通` は `UnitTally.DamageToEnemy`（**軽減と軛の後**）。");
        Console.WriteLine("**M を上げるほど通し率が落ちれば P6 が当たり**——ただし軛は**1回の `ApplyDamage` を**切るので、");
        Console.WriteLine("全体攻撃は「1発 25 × 敵の体数」まで通る。**丸ごと潰れるのではなく、1体あたりの上振れだけが捨てられる。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | M | 第2波 通し率 | 第3波 | **第4波（軛）** | 第5波 |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|");
        foreach ((string name, Formation f) in GareRigs())
            foreach (int m in pts)
            {
                var pass = new double[5];
                for (int st = 1; st < EnemyCatalog.Stages.Count; st++)
                {
                    long nom = 0, act = 0;
                    for (int seed = 0; seed < Seeds; seed++)
                    {
                        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                          verbose: false, shrapnel: new ShrapnelRule(m, 100));
                        nom += r.ShrapnelDealt;
                        if (r.TallyByUnit.TryGetValue("gare", out UnitTally? tg)) act += tg.DamageToEnemy;
                    }
                    pass[st] = nom == 0 ? 0 : act * 100.0 / nom;
                }
                Console.WriteLine($"| {name} | {m} | {pass[1]:F1}% | {pass[2]:F1}% | **{pass[3]:F1}%** | {pass[4]:F1}% |");
            }
    }

    // =================================================================================
    // 第138期 —— 礫（ガレ）の自己検査
    // =================================================================================

    static void GareCheck()
    {
        Console.WriteLine("# 第138期 自己検査（礫のガレ）");
        Console.WriteLine();

        // (G-a) ロスターに入れていないこと
        bool inAll = UnitCatalog.All.Any(u => u.Id == "gare");
        bool inPresets = Presets.Compare.Concat(Presets.Cross)
            .Any(r => r.Item2.Occupied().Any(o => o.Def.Id == "gare"));

        Console.WriteLine("## (G-a) —— ガレは `UnitCatalog.All` にも `Presets` にも入っていない");
        Console.WriteLine();
        Console.WriteLine($"`UnitCatalog.All` ＝ **{UnitCatalog.All.Count} 枚**（ガレ在席: {(inAll ? "**×** いる" : "○ いない")}）。");
        Console.WriteLine($"`Presets.Compare` ＋ `Presets.Cross` ＝ {Presets.Compare.Length + Presets.Cross.Length} 行"
            + $"（ガレ在席: {(inPresets ? "**×** いる" : "○ いない")}）。");
        Console.WriteLine();
        Console.WriteLine("> **これが「61行 ＋ 12行が ±0.0」の構造的な保証**——`Formation` から参照されないので、");
        Console.WriteLine("> `ShrapnelRule` を何に振っても既存の行は1バイトも動かない（予測 P5）。");
        Console.WriteLine();

        // (G-b) 札が正しく解決されること
        Console.WriteLine("## (G-b) —— 札と行動の形");
        Console.WriteLine();
        UnitDef g = UnitCatalog.Gare;
        Console.WriteLine($"- `Traits` ＝ {string.Join(" / ", g.Traits ?? Array.Empty<TraitId>())}");
        Console.WriteLine($"- `Actions` ＝ {(g.Actions is null ? "なし" : string.Join(" / ", g.Actions.Select(x => x.Kind.ToString())))}"
            + $"（**`[Skill]` 1要素であること**: {(g.Actions is { Count: 1 } && g.Actions[0].Kind == ActionKind.Skill ? "○" : "**×**")}）");
        Console.WriteLine($"- `Def.Pattern` ＝ {g.Pattern}（**単体であること**: {(g.Pattern == AttackPattern.Single ? "○" : "**×**")}"
            + " ——砕け〈`ShatterTrait.OnDamaged`〉が `source.CurrentPattern == Single` で早期 return する条件)");
        Console.WriteLine($"- `Advances` ＝ {g.Advances}（第131期の基準 (a)：一度も通常攻撃をしない）");
        ShrapnelRule d = ShrapnelRule.Default;
        Console.WriteLine($"- `ShrapnelRule.Default` ＝ `Multiplier = {d.Multiplier}` / `SelfDamagePercent = {d.SelfDamagePercent}`");
        Console.WriteLine();

        // (G-c) ループ検査の実測（Q0-2）
        Console.WriteLine("## (G-c) —— ループ検査（Q0-2 の実測）");
        Console.WriteLine();
        Console.WriteLine("ガレを入れた台で、**砕けの発火回数（`ShatterTicks`）が礫の発火で増えていないこと**を見る。");
        Console.WriteLine("砕けは範囲攻撃（`source.CurrentPattern != Single`）にしか反応しないので、");
        Console.WriteLine("**礫の自傷（`source` ＝ ガレ・単体）では 1 回も増えないはず。**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 礫 発火/戦 | 砕け 発火/戦 | ヒビ生存T | 砕け発火 ÷ ヒビ生存T |");
        Console.WriteLine("|---|---:|---:|---:|---:|");
        foreach ((string name, Formation f) in GareRigs())
        {
            var L = RunGare(f, ShrapnelRule.Default);
            double life = Per(L.HibiLife, L.Battles);
            Console.WriteLine($"| {name} | {Per(L.Fires, L.Battles):F2} | {Per(L.Ticks, L.Battles):F2} | {life:F2} | "
                + $"{(life <= 0 ? 0 : Per(L.Ticks, L.Battles) / life):F2} |");
        }
        Console.WriteLine();
        Console.WriteLine("> **砕けは被弾の回数（範囲攻撃を浴びた回数）で決まる量**なので、礫の有無で動くのは");
        Console.WriteLine("> 決着の長さのぶんだけ。**1ターンに何度も増えていたらループしている。**");
        Console.WriteLine();

        // (G-d) 帳簿が閉じる
        Console.WriteLine("## (G-d) —— 帳簿が閉じる（§3-2）");
        Console.WriteLine();
        Console.WriteLine("**撃った回数 ＋ 捨てた回数 ＝ 礫の生存T**（撃つか捨てるかのどちらかしかない）。");
        Console.WriteLine("`捨てた` は engine が数えている `UnitTally.StallCanAct`。");
        Console.WriteLine("**痺れ・まどろみで潰れたターンは別に数えられる**ので、その分だけ下にずれる。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 撃った/戦 | 捨てた/戦 | 和 | ガレ生存T | 差 | 痺/眠 |");
        Console.WriteLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach ((string name, Formation f) in GareRigs())
        {
            var L = RunGare(f, ShrapnelRule.Default);
            double fire = Per(L.Fires, L.Battles), idle = Per(L.Idles, L.Battles), life = Per(L.GareLife, L.Battles);
            Console.WriteLine($"| {name} | {fire:F2} | {idle:F2} | {fire + idle:F2} | {life:F2} | {fire + idle - life:+0.00;-0.00;0.00} | "
                + $"{Per(L.GareStun, L.Battles):F2} |");
        }
        Console.WriteLine();
        Console.WriteLine("**砕いた量の合計 ≦ 盤面に配られた破片の合計**（在庫以上を砕いていない）と");
        Console.WriteLine("**敵側分岐の発火 0 件**（予測 P4・次期の点灯確認に使う回帰値）:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 砕いた/戦 | 配られた/戦 | 砕いた ≦ 配られた | **敵を砕いた回数** |");
        Console.WriteLine("|---|---:|---:|---|---:|");
        foreach ((string name, Formation f) in GareRigs())
        {
            var L = RunGare(f, ShrapnelRule.Default);
            double br = Per(L.Shards, L.Battles), gv = Per(L.Given, L.Battles);
            Console.WriteLine($"| {name} | {br:F1} | {gv:F1} | {(br <= gv + 1e-9 ? "○" : "**×**")} | **{L.FoeTargets}** |");
        }
        Console.WriteLine();
        Console.WriteLine("> **配られた（`ShatterGiven`）は砕けだけの供給**なので、鱗の死拾い・集約のぶんは含まない。");
        Console.WriteLine("> その2本がある台では `砕いた` が上回りうる——**上回ったらどちらの供給かを内訳で確かめること。**");
    }
}
