using BattleCore;
using static Common;

// =====================================================================================
// lili モード（第204期）の本体 —— 版の対照・帳簿・自己検査
//
// **旧ノノは `UnitCatalog.Nono`（対照として残した定義）**、ほかの版はこの診断のローカルに写してある
// （`UnitCatalog` の `All` にはリリしか居ない）。版の切り替えは札の差し替えだけ（`Kiss` / `KissSpill` / `KissBare` / `Kiss30`）。
// =====================================================================================

static partial class LiliDiag
{
    static partial void RunMoreImpl(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run": RunVersions(); handled = true; return;
            case "ledger": RunLedger(); handled = true; return;
            case "check": Check(arg); handled = true; return;
            case "run2": Run205(); handled = true; return;
            case "ledger2": Ledger205(); handled = true; return;
            case "check2": Check205(arg); handled = true; return;
            case "run3": Run206(); handled = true; return;
            case "ledger3": Ledger206(); handled = true; return;
            case "check3": Check206(arg); handled = true; return;
        }
    }

    // =================================================================================
    // 版
    // =================================================================================

    static UnitDef Clone(UnitDef d, TraitId[] traits) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Pattern = d.Pattern,
        Advances = d.Advances, Actions = d.Actions, Traits = traits,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    static readonly (string Tag, UnitDef D)[] Versions =
    {
        ("旧", UnitCatalog.Nono),
        ("新", UnitCatalog.Lili),
        ("移さない", Clone(UnitCatalog.Lili, new[] { TraitId.Kiss })),
        ("吸うだけ", Clone(UnitCatalog.Lili, new[] { TraitId.KissBare, TraitId.KissSpill })),
        ("新・30", Clone(UnitCatalog.Lili, new[] { TraitId.Kiss30, TraitId.KissSpill })),
    };

    static Formation Apply(Formation f, UnitDef d)
    {
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef u) in f.Occupied()) g[slot] = IsLili(u) ? d : u;
        return g;
    }

    static IEnumerable<(string Band, string Name, Formation F)> LiliRows()
    {
        foreach (var (n, f) in CompareBuilds()) if (HasLili(f)) yield return ("compare", n, f);
        foreach (var (n, f) in CrossBuilds()) if (HasLili(f)) yield return ("交差帯", n, f);
    }

    static double[] Rates(Formation f, int seeds = Seeds) => RatesAndStall(f, seeds).W;

    static (double[] W, int[] Stall) RatesAndStall(Formation f, int seeds = Seeds)
    {
        var w = new double[5];
        var s = new int[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0, stall = 0;
            Formation enemy = EnemyCatalog.Stages[st].Enemy;
            Parallel.For(0, seeds, seed =>
            {
                BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false);
                if (r.PlayerWon) Interlocked.Increment(ref wins);
                else if (r.Turns >= BattleEngine.MaxTurns) Interlocked.Increment(ref stall);
            });
            w[st] = 100.0 * wins / seeds;
            s[st] = stall;
        }
        return (w, s);
    }

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static string Cells(double[] w) => string.Join(" / ", w.Skip(1).Select(x => x.ToString("F1")));
    static string D(double x) => x.ToString("+0.0;-0.0;0.0");

    /// <summary>情報セル（`0 < x < 100`・第2〜5波）。</summary>
    static int InfoCells(double[] w) => w.Skip(1).Count(x => x > 0 && x < 100);

    // =================================================================================
    // run —— 在席行 × 版（§4.1）
    // =================================================================================

    static void RunVersions()
    {
        Console.WriteLine("# 第204期 `lili run` —— 在席行 × 版（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("**旧** ＝ 継ぎ当てのノノ（第203期）／ **新** ＝ リリ（20%・状態を移す・儀式と還る）／ **移さない** ＝ `KissSpill` を外した `yP` ／"
                          + " **吸うだけ** ＝ 儀式と還るを外した版 ／ **新・30** ＝ 吸う量 30%。seed 0..199・平均は第2〜5波（規約 (G10)）。");
        Console.WriteLine();
        Console.WriteLine("**Δ旧** ＝ 新 − 旧 ／ **代金** ＝ 新 − 移さない ／ **儀式** ＝ 新 − 吸うだけ ／ **30** ＝ 新・30 − 新。`膠着` は 30 ターン上限の戦（第2〜5波・800 戦中）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 旧 | 新 | 移さない | 吸うだけ | 新・30 | 旧 平均 | **新 平均** | Δ旧 | 代金 | 儀式 | 30 | 情報セル 旧→新 | 膠着 旧→新 |");
        Console.WriteLine("|---|---|---|---|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        var sum = new Dictionary<string, double>();
        var waveUp = new double[5];
        int n = 0, infoOld = 0, infoNew = 0;
        foreach (var (band, name, f) in LiliRows())
        {
            var r = Versions.ToDictionary(v => v.Tag, v => RatesAndStall(Apply(f, v.D)));
            var m = r.ToDictionary(kv => kv.Key, kv => Mean25(kv.Value.W));
            if (band == "compare")
            {
                n++;
                foreach (var kv in m) sum[kv.Key] = sum.GetValueOrDefault(kv.Key) + kv.Value;
                for (int st = 1; st < 5; st++) waveUp[st] += r["新"].W[st] - r["旧"].W[st];
                infoOld += InfoCells(r["旧"].W); infoNew += InfoCells(r["新"].W);
            }
            Console.WriteLine("| " + band + " | " + name + " | " + Cells(r["旧"].W) + " | " + Cells(r["新"].W) + " | " + Cells(r["移さない"].W) + " | "
                              + Cells(r["吸うだけ"].W) + " | " + Cells(r["新・30"].W) + " | " + m["旧"].ToString("F1") + " | **" + m["新"].ToString("F1") + "** | "
                              + D(m["新"] - m["旧"]) + " | " + D(m["新"] - m["移さない"]) + " | " + D(m["新"] - m["吸うだけ"]) + " | " + D(m["新・30"] - m["新"]) + " | "
                              + InfoCells(r["旧"].W) + " → " + InfoCells(r["新"].W) + " | "
                              + r["旧"].Stall.Skip(1).Sum() + " → " + r["新"].Stall.Skip(1).Sum() + " |");
        }
        Console.WriteLine();
        if (n > 0)
        {
            Console.WriteLine("`compare` の在席 " + n + " 行の平均: " + string.Join(" ／ ", Versions.Select(v => v.Tag + " " + (sum[v.Tag] / n).ToString("F1"))) + "。");
            Console.WriteLine("波ごとの上げ幅（新 − 旧）: 第2波 " + D(waveUp[1] / n) + " ／ 第3波（渇き） " + D(waveUp[2] / n)
                              + " ／ 第4波（軛） " + D(waveUp[3] / n) + " ／ 第5波 " + D(waveUp[4] / n) + "。情報セル " + infoOld + " → " + infoNew + "。");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // ledger —— 帳簿（§4.2・§4.3）
    // =================================================================================

    sealed class Led
    {
        public int N, Lost, Died, RiteBattles;
        public double DiedTurnSum, LostHealPerTurnSum, HealOutSum, TurnsSum, RiteFirstSum;
        public double LiliArmorPeakSum, OtherArmorPeakSum; public int LiliArmorPeakMax, OtherArmorPeakMax;
        public UnitTally T = new();
    }

    static Led Collect(Formation f, int[] stages, int seeds = Seeds)
    {
        var led = new Led();
        var gate = new object();
        foreach (int st in stages)
        {
            Formation enemy = EnemyCatalog.Stages[st].Enemy;
            Parallel.For(0, seeds, seed =>
            {
                BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false);
                string id = r.TallyByUnit.ContainsKey("lili") ? "lili" : "nono";
                if (!r.TallyByUnit.TryGetValue(id, out UnitTally? me)) return;
                long healOut = me.HealOutInTurn + me.HealOutOffTurn;
                int otherPeak = r.TallyByUnit.Where(kv => kv.Key != id).Select(kv => kv.Value.ArmorPeakSeen).DefaultIfEmpty(0).Max();
                lock (gate)
                {
                    led.N++;
                    led.T.Add(me);
                    led.HealOutSum += healOut;
                    led.TurnsSum += r.Turns;
                    if (me.Deaths > 0) { led.Died++; led.DiedTurnSum += me.LastActiveTurn; }
                    if (!r.PlayerWon) { led.Lost++; led.LostHealPerTurnSum += (double)healOut / Math.Max(1, r.Turns); }
                    if (me.RiteFirstTurn > 0) { led.RiteBattles++; led.RiteFirstSum += me.RiteFirstTurn; }
                    led.LiliArmorPeakSum += me.ArmorPeakSeen; led.LiliArmorPeakMax = Math.Max(led.LiliArmorPeakMax, me.ArmorPeakSeen);
                    led.OtherArmorPeakSum += otherPeak; led.OtherArmorPeakMax = Math.Max(led.OtherArmorPeakMax, otherPeak);
                }
            });
        }
        return led;
    }

    static readonly int[] Waves25 = { 1, 2, 3, 4 };

    static void RunLedger()
    {
        Console.WriteLine("# 第204期 `lili ledger` —— 帳簿（1戦あたり・第2〜5波・seed 0..199）");
        Console.WriteLine();
        var rows = LiliRows().ToList();
        var leds = rows.ToDictionary(r => r.Name, r => Versions.ToDictionary(v => v.Tag, v => Collect(Apply(r.F, v.D), Waves25)));
        string P(double x, int n) => n == 0 ? "—" : (x / n).ToString("F2");

        Console.WriteLine("## 表B. 吸い取り・儀式・還る（新）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 吸った回数 | 1回の名目 | 1回の吸えた量 | 1回の癒えた量 | 1回の破片 | 止められた/戦 | 自分が受けた/戦 | 吸って倒した/戦 | 儀式/戦 | 儀式のある戦 | 初回T | 儀式の吸えた量/回 | 儀式の癒えた量/回 | 還る/戦 | 還るの癒え/回 | リリの与ダメ/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (band, name, f) in rows)
        {
            Led l = leds[name]["新"]; UnitTally t = l.T;
            double fires = t.KissFires;
            Console.WriteLine("| " + name + " | " + P(fires, l.N) + " | " + (fires == 0 ? "—" : (t.KissNominal / fires).ToString("F1")) + " | "
                              + (fires == 0 ? "—" : (t.KissDrained / fires).ToString("F1")) + " | " + (fires == 0 ? "—" : (t.KissHealed / fires).ToString("F1")) + " | "
                              + (fires == 0 ? "—" : (t.KissArmor / fires).ToString("F1")) + " | " + P(t.KissBlocked, l.N) + " | " + P(t.KissToSelf, l.N) + " | "
                              + P(t.KissKills, l.N) + " | " + P(t.RiteFires, l.N) + " | " + (100.0 * l.RiteBattles / Math.Max(1, l.N)).ToString("F1") + "% | "
                              + (l.RiteBattles == 0 ? "—" : (l.RiteFirstSum / l.RiteBattles).ToString("F2")) + " | "
                              + (t.RiteFires == 0 ? "—" : ((double)t.RiteDrained / t.RiteFires).ToString("F1")) + " | "
                              + (t.RiteFires == 0 ? "—" : ((double)t.RiteHealed / t.RiteFires).ToString("F1")) + " | "
                              + P(t.ReturnFires, l.N) + " | " + (t.ReturnFires == 0 ? "—" : ((double)t.ReturnHealed / t.ReturnFires).ToString("F1")) + " | "
                              + P(t.DamageToEnemy, l.N) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表C. 回復・倒れ方・破片（版ごと）");
        Console.WriteLine();
        Console.WriteLine("**回復/戦** ＝ リリ（旧はノノ）が配った実額（`HealOutInTurn + HealOutOffTurn`）。**負け戦の回復/T** ＝ 負けた戦に限った 回復 ÷ 決着T（§4.3）。"
                          + "**1手番の回復** ＝ 旧は繕い、新は1体ずつ吸った回の癒えた量 ÷ 回数。破片は**観測できた最大値**（リリの手番の頭の走査と、リリが足した直後）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 回復/戦 | 1手番の回復 | 負け戦 | 負け戦の回復/T | 倒れた戦 | 倒れたT | 破片 最大（リリ・平均/最大） | 破片 最大（ほか・平均/最大） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (band, name, f) in rows)
        foreach (var (tag, _) in Versions)
        {
            Led l = leds[name][tag]; UnitTally t = l.T;
            double perAct = tag == "旧" ? (t.MendFires == 0 ? double.NaN : (double)t.MendHealed / t.MendFires)
                                       : (t.KissFires == 0 ? double.NaN : (double)t.KissHealed / t.KissFires);
            Console.WriteLine("| " + name + " | " + tag + " | " + P(l.HealOutSum, l.N) + " | " + (double.IsNaN(perAct) ? "—" : perAct.ToString("F1")) + " | "
                              + l.Lost + " | " + (l.Lost == 0 ? "—" : (l.LostHealPerTurnSum / l.Lost).ToString("F2")) + " | "
                              + (100.0 * l.Died / Math.Max(1, l.N)).ToString("F1") + "% | " + (l.Died == 0 ? "—" : (l.DiedTurnSum / l.Died).ToString("F2")) + " | "
                              + P(l.LiliArmorPeakSum, l.N) + " / " + l.LiliArmorPeakMax + " | " + P(l.OtherArmorPeakSum, l.N) + " / " + l.OtherArmorPeakMax + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表B'. リリが自分で受け取った回の理由（新・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("**満タン** ＝ ほかの味方が全員満タン ／ **支援拒否** ＝ 傷ついているのが支援拒否の駒（ガルド）だけ ／ **全滅** ＝ ほかの味方が全員倒れた。"
                          + "括弧はそのうちリリ自身も満タンだった回（与えた全額が破片になる）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 吸い取り 満タン | 支援拒否 | 全滅 | (リリも満タン) | 儀式の余り 満タン | 支援拒否 | 全滅 | 還る 満タン | 支援拒否 | 全滅 | 自分で受けた 計/戦 | 吸い取りに占める割合 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (band, name, f) in rows)
        {
            Led l = leds[name]["新"]; UnitTally t = l.T;
            long[] w = t.KissSelfWhy ?? new long[9];
            long[] fu = t.KissSelfFull ?? new long[3];
            string c(int i) => P(w[i], l.N);
            Console.WriteLine("| " + name + " | " + c(0) + " | " + c(1) + " | " + c(2) + " | (" + P(fu[0], l.N) + ") | "
                              + c(3) + " | " + c(4) + " | " + c(5) + " | " + c(6) + " | " + c(7) + " | " + c(8) + " | "
                              + P(w.Sum(), l.N) + " | " + (t.KissFires == 0 ? "—" : (100.0 * (w[0] + w[1] + w[2]) / t.KissFires).ToString("F1") + "%") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表C'. 1手番の回復（渇きの第三波を除く＝第2・4・5波・予測 P2）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 旧（繕い） | 新（1体ずつ吸った回） | 新・30 |");
        Console.WriteLine("|---|--:|--:|--:|");
        int[] noDrought = { 1, 3, 4 };
        double so = 0, sn = 0, s3 = 0; int nr = 0;
        foreach (var (band, name, f) in rows)
        {
            UnitTally o = Collect(Apply(f, UnitCatalog.Nono), noDrought).T;
            UnitTally nw = Collect(Apply(f, UnitCatalog.Lili), noDrought).T;
            UnitTally t3 = Collect(Apply(f, Versions[4].D), noDrought).T;
            double vo = (double)o.MendHealed / Math.Max(1, o.MendFires), vn = (double)nw.KissHealed / Math.Max(1, nw.KissFires), v3 = (double)t3.KissHealed / Math.Max(1, t3.KissFires);
            if (band == "compare") { so += vo; sn += vn; s3 += v3; nr++; }
            Console.WriteLine("| " + name + " | " + vo.ToString("F1") + " | " + vn.ToString("F1") + " | " + v3.ToString("F1") + " |");
        }
        if (nr > 0) Console.WriteLine("| **`compare` 平均** | " + (so / nr).ToString("F1") + " | **" + (sn / nr).ToString("F1") + "** | " + (s3 / nr).ToString("F1") + " |");
        Console.WriteLine();

        Console.WriteLine("## 表D. 移した状態（新・キー × 受け取った駒・件数/戦 と 値の合計/戦）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 移した件数/戦 | キー | 受け取った駒 | 件数/戦 | 値/戦 |");
        Console.WriteLine("|---|--:|---|---|--:|--:|");
        foreach (var (band, name, f) in rows)
        {
            Led l = leds[name]["新"]; UnitTally t = l.T;
            if (t.KissMovedBy is null || t.KissMovedBy.Count == 0) { Console.WriteLine("| " + name + " | 0.00 | — | — | — | — |"); continue; }
            bool first = true;
            foreach (var kv in t.KissMovedBy.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                string[] kp = kv.Key.Split('|');
                string who = UnitCatalog.Everyone.FirstOrDefault(u => u.Id == kp[1])?.Name ?? kp[1];
                Console.WriteLine("| " + (first ? name : "") + " | " + (first ? P(t.KissMoves, l.N) : "") + " | " + StatusKeys.LabelOf(kp[0]) + " | "
                                  + who + " | " + P(kv.Value.N, l.N) + " | " + P(kv.Value.Sum, l.N) + " |");
                first = false;
            }
        }
        Console.WriteLine();
        // P4: 燃焼のうちホタが受けた割合
        foreach (var (band, name, f) in rows)
        {
            UnitTally t = leds[name]["新"].T;
            if (t.KissMovedBy is null) continue;
            long burn = t.KissMovedBy.Where(kv => kv.Key.StartsWith("burn|")).Sum(kv => kv.Value.N);
            if (burn == 0) continue;
            long hota = t.KissMovedBy.Where(kv => kv.Key == "burn|hota").Sum(kv => kv.Value.N);
            Console.WriteLine("- " + name + ": 移った燃焼 " + burn + " 件のうちホタ " + hota + " 件（**" + (100.0 * hota / burn).ToString("F1") + "%**）");
        }
        Console.WriteLine();
    }

    // =================================================================================
    // check —— 自己検査（§6）
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第204期 `lili check` —— 自己検査");
        Console.WriteLine();
        bool ok = true;

        // (a) リリを含まない行が第203期の表と一致・「旧」が第203期の表と一致
        string path = string.IsNullOrWhiteSpace(arg) ? Path.Combine("docs", "balance.md") : arg.Trim();
        var old = ReadBalance(path);
        Console.WriteLine("## (a) `compare` の回帰（比べる表: `" + path + "`・" + old.Count + " 行）");
        Console.WriteLine();
        int noLiliRows = 0, noLiliDiff = 0, oldRows = 0, oldDiff = 0;
        foreach (var (name, f) in CompareBuilds())
        {
            string key = name.Replace("リリ", "ノノ");
            if (!old.TryGetValue(key, out double[]? want)) { Console.WriteLine("- 行が見つからない: " + key); ok = false; continue; }
            if (!HasLili(f))
            {
                noLiliRows++;
                double[] got = Rates(f);
                for (int st = 0; st < 5; st++) if (Math.Abs(got[st] - want[st]) > 0.05) noLiliDiff++;
            }
            else
            {
                oldRows++;
                double[] got = Rates(Apply(f, UnitCatalog.Nono));
                for (int st = 0; st < 5; st++) if (Math.Abs(got[st] - want[st]) > 0.05) oldDiff++;
            }
        }
        Console.WriteLine("- リリを含まない " + noLiliRows + " 行 × 5 波: 差 **" + noLiliDiff + "** セル " + (noLiliDiff == 0 ? "○" : "×"));
        Console.WriteLine("- リリの在席 " + oldRows + " 行を旧ノノに戻した版 × 5 波: 差 **" + oldDiff + "** セル " + (oldDiff == 0 ? "○" : "×"));
        ok &= noLiliDiff == 0 && oldDiff == 0;
        Console.WriteLine();

        // (c) 台本での自己検査
        Console.WriteLine("## (c) 台本（verbose）で見る規則（在席行 × 第2〜5波 × seed 0..39）");
        Console.WriteLine();
        int battles = 0, riteTransfer = 0, stigmaAlly = 0, stigmaMoved = 0, droughtArmor = 0, riteThenRite = 0, rites = 0, drains = 0,
            transfers = 0, riteBadCount = 0, droughtGives = 0, armorEvents = 0;
        foreach (var (band, name, f) in LiliRows())
        foreach (int st in Waves25)
        for (int seed = 0; seed < 40; seed++)
        {
            // 自分で駒を作って渡す（`Run(Formation…)` と同じ入口）。InstanceId は Run の中で振られるので、後から陣営を引ける。
            var pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var en = BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam, BossRule.Default.Scale);
            BattleResult r = BattleEngine.Run(pl, en, seed, verbose: true);
            battles++;
            var ev = r.Events;
            var allies = pl.Select(u => u.InstanceId).ToHashSet();
            int? droughtId = en.FirstOrDefault(u => u.HasTrait(TraitId.Drought))?.InstanceId;
            bool droughtAlive = droughtId is not null;
            bool inRite = false;
            string lastKiss = "";
            int drainsSinceRite = 0;
            foreach (var e in ev)
            {
                if (e.Kind == BattleEventKind.Death && e.TargetId == droughtId) droughtAlive = false;
                if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Stigma && e.TargetId is int tg && allies.Contains(tg)) stigmaAlly++;
                if (e.Kind == BattleEventKind.StatusTransfer)
                {
                    transfers++;
                    if (inRite) riteTransfer++;
                    if (e.Text == StatusKeys.Stigma) stigmaMoved++;
                }
                if (e.Kind != BattleEventKind.Kiss) continue;
                switch (e.Text)
                {
                    case KissLabels.Rite:
                        rites++;
                        if (lastKiss == KissLabels.RiteEnd) riteThenRite++;
                        if (drainsSinceRite < e.Slot) riteBadCount++;
                        inRite = true; drainsSinceRite = 0; break;
                    case KissLabels.RiteEnd: inRite = false; break;
                    case KissLabels.Drain: drains++; drainsSinceRite++; break;
                    case KissLabels.Armor: armorEvents++; if (droughtAlive) droughtArmor++; break;
                    case KissLabels.Give: if (droughtAlive) droughtGives++; break;
                }
                lastKiss = e.Text ?? "";
            }
        }
        void Row(string what, int bad, string note = "")
        {
            Console.WriteLine("| " + what + " | " + bad + " | " + (bad == 0 ? "○" : "**×**") + " | " + note + " |");
            ok &= bad == 0;
        }
        Console.WriteLine("戦 " + battles + " ／ 吸い取り " + drains + " ／ 移した件数 " + transfers + " ／ 儀式 " + rites + "。");
        Console.WriteLine();
        Console.WriteLine("| 規則 | 違反 | 判定 | 注 |");
        Console.WriteLine("|---|--:|:-:|---|");
        Row("儀式の中で状態が移らない", riteTransfer);
        Row("聖痕が味方に付かない", stigmaAlly);
        Row("聖痕が移らない", stigmaMoved);
        Row("渇きの祭司が生きている間に破片が増えない（止められた回復は溢れにしない）", droughtArmor, "渇きの間の口移し " + droughtGives + " 件・破片の件数（全体） " + armorEvents);
        Row("儀式の直後にまた儀式が来ない（儀式の後に聖痕が 0 になる）", riteThenRite, "儀式のあとは必ず吸い取りから始まる");
        Row("儀式の前に、儀式で吸う敵の数以上の吸い取りがある（一巡した）", riteBadCount, "一巡の途中で倒れた敵のぶん多くなるのは正しい");
        Console.WriteLine();
        Console.WriteLine(ok ? "**全部 ○。**" : "**× がある。**");
    }

    /// <summary>`docs/balance.md` を行名で読む（`| 行 | 第1波 | … |`）。</summary>
    static Dictionary<string, double[]> ReadBalance(string path)
    {
        var d = new Dictionary<string, double[]>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            string[] c = line.Split('|', StringSplitOptions.TrimEntries);
            if (c.Length < 8) continue;
            var v = new double[5];
            bool okRow = true;
            for (int i = 0; i < 5; i++)
                if (!double.TryParse(c[2 + i].TrimEnd('%'), out v[i])) okRow = false;
            if (okRow) d[c[1]] = v;
        }
        return d;
    }
}
