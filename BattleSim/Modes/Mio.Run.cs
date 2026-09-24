using BattleCore;
using static Common;

// =====================================================================================
// mio モード（第194期）の本体 —— 版の対照・差し替え表・帳簿・自己検査
//
// **旧の駒・マイナスなしはこの診断のローカルに写してある**（`UnitCatalog` には新しいミオしか居ない）。
// Id は本物と同じ（帳簿を同じキーで引く）。素体だけは Id を変える（第69期からの器具）。
// =====================================================================================

static partial class MioDiag
{
    static partial void RunMoreImpl(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run": RunVersions(); handled = true; return;
            case "ledger": RunLedger(); handled = true; return;
            case "check": Check(arg); handled = true; return;
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

    static readonly UnitDef MioNew = UnitCatalog.Mio;
    /// <summary>旧ミオ（第193期まで）: 澱み（+4 層・傷の着火）だけ。手番は同じ。</summary>
    static readonly UnitDef MioOld = Clone(UnitCatalog.Mio, new[] { TraitId.Amplifier });
    /// <summary>新・マイナスなし（`ConcentrateLeak` を外した `yP`）。</summary>
    static readonly UnitDef MioYP = Clone(UnitCatalog.Mio, new[] { TraitId.Concentrate });
    /// <summary>素体（同じ数値・特性なし・毎手番殴る）。</summary>
    static readonly UnitDef MioPlain = new()
    {
        Id = "mio_plain", Name = "素体のミオ", MaxHp = UnitCatalog.Mio.MaxHp, Attack = UnitCatalog.Mio.Attack,
        Speed = UnitCatalog.Mio.Speed, Pattern = UnitCatalog.Mio.Pattern, Traits = Array.Empty<TraitId>(),
    };

    static readonly (string Tag, UnitDef D)[] Versions =
    {
        ("旧", MioOld), ("新", MioNew), ("マイナスなし", MioYP), ("素体", MioPlain),
    };

    static Formation Apply(Formation f, UnitDef mio)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == "mio" ? mio : d;
        return g;
    }

    static Formation SwapAt(Formation f, int slotAt, UnitDef to)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = slot == slotAt ? to : d;
        return g;
    }

    static UnitDef Plain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Pattern = d.Pattern, Traits = Array.Empty<TraitId>(),
    };

    // =================================================================================
    // 勝率
    // =================================================================================

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

    static IEnumerable<(string Band, string Name, Formation F)> MioRows()
    {
        foreach (var (n, f) in CompareBuilds()) if (HasMio(f)) yield return ("compare", n, f);
        foreach (var (n, f) in CrossBuilds()) if (HasMio(f)) yield return ("交差帯", n, f);
    }

    /// <summary>§4.2 の差し替え行。替える席と、その席に置く版を受け取って組む。</summary>
    static List<(string Name, Formation F, int Slot, UnitDef Low, double LowA, double Base)> SwapRows()
    {
        var list = new List<(string, Formation, int, UnitDef, double, double)>();
        foreach (var (name, f) in CompareBuilds())
        {
            var occ = f.Occupied().ToList();
            if (!occ.Any(o => Writers.Contains(o.Def.Id)) || HasMio(f)) continue;
            double baseW = Mean25(Rates(f));
            var low = occ.Where(o => !Writers.Contains(o.Def.Id))
                         .Select(o => (o.Slot, o.Def, A: baseW - Mean25(Rates(SwapAt(f, o.Slot, Plain(o.Def))))))
                         .OrderBy(x => x.A).ThenBy(x => x.Slot).First();
            list.Add((name, f, low.Slot, low.Def, low.A, baseW));
        }
        return list;
    }

    // =================================================================================
    // run —— 在席行の版の対照（§4.1）と差し替え表（§4.2・主表）
    // =================================================================================

    static void RunVersions()
    {
        Console.WriteLine("# 第194期 `mio run` —— 版の対照・差し替え表（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("**旧** ＝ 第193期までのミオ（澱み +4 だけ）／ **新** ＝ 濃縮（+4 → 刻みの最も大きい敵とその隣に印 +1＝刻みが 1+n 回・隣の味方にも印 +1）／"
                          + " **マイナスなし** ＝ `ConcentrateLeak` を外した `yP` ／ **素体** ＝ 同じ数値・特性なし（攻2 で毎手番殴る）。"
                          + "seed 0..199。平均は第2〜5波（規約 (G10)）。");
        Console.WriteLine();

        Console.WriteLine("## 表A. ミオの在席行 × 版（第2〜5波の勝率・平均）");
        Console.WriteLine();
        Console.WriteLine("**Δ旧** ＝ 新 − 旧 ／ **代金** ＝ 新 − マイナスなし ／ **Δ素体** ＝ 新 − 素体。`膠着` は 30 ターン上限で終わった戦の数（第2〜5波・800 戦中）。");
        Console.WriteLine();
        Console.WriteLine("| 帯 | 行 | 旧 | 新 | マイナスなし | 素体 | 旧 平均 | **新 平均** | Δ旧 | 代金 | Δ素体 | 膠着 旧→新 |");
        Console.WriteLine("|---|---|---|---|---|---|--:|--:|--:|--:|--:|--:|");
        var waveUp = new double[5];
        int nRows = 0;
        foreach (var (band, name, f) in MioRows())
        {
            var r = Versions.ToDictionary(v => v.Tag, v => RatesAndStall(Apply(f, v.D)));
            double o = Mean25(r["旧"].W), n = Mean25(r["新"].W), y = Mean25(r["マイナスなし"].W), p = Mean25(r["素体"].W);
            for (int st = 1; st < 5; st++) waveUp[st] += r["新"].W[st] - r["旧"].W[st];
            nRows++;
            Console.WriteLine("| " + band + " | " + name + " | " + Cells(r["旧"].W) + " | " + Cells(r["新"].W) + " | "
                              + Cells(r["マイナスなし"].W) + " | " + Cells(r["素体"].W) + " | "
                              + o.ToString("F1") + " | **" + n.ToString("F1") + "** | " + D(n - o) + " | " + D(n - y) + " | " + D(n - p) + " | "
                              + r["旧"].Stall.Skip(1).Sum() + " → " + r["新"].Stall.Skip(1).Sum() + " |");
        }
        Console.WriteLine();
        if (nRows > 0)
            Console.WriteLine("波ごとの上げ幅（新 − 旧・在席 " + nRows + " 行の平均）: 第2波 " + D(waveUp[1] / nRows) + " ／ 第3波 " + D(waveUp[2] / nRows)
                              + " ／ **第4波（軛） " + D(waveUp[3] / nRows) + "** ／ 第5波 " + D(waveUp[4] / nRows));
        Console.WriteLine();

        Console.WriteLine("## 表B. 差し替え表（§4.2・**主表**・測る前に固定）");
        Console.WriteLine();
        Console.WriteLine("行 ＝ `compare` のうち毒の書き手（グザ・スィド・ラウ・ベニ・カタ）を含み、ミオを含まない行。"
                          + "替える駒 ＝ その行で**書き手以外のうち**素体差し替えの帰属が最も低い1枚（第188期 表E と同じ規則）。席はその駒の席のまま。"
                          + "**Δ元** ＝ 新 − 元の行 ／ **Δ素体** ＝ 新 − 同じ席の素体（**ミオの値はこちらで読む**）／ **代金** ＝ 新 − マイナスなし ／ 旧 ＝ 旧ミオを同じ席に置いた値。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 替えた駒（帰属）・席 | 隣の駒 | 元 | 素体 | 旧 | **新** | マイナスなし | Δ元 | **Δ素体** | Δ旧 | 代金 | 新の波別 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        var rows = SwapRows();
        if (rows.Count == 0) { Console.WriteLine("**行が 0 本。走査が空なので止める**（R034）。"); return; }
        var dp = new List<double>();
        foreach (var (name, f, slot, lowDef, lowA, baseW) in rows)
        {
            var r = Versions.ToDictionary(v => v.Tag, v => Rates(SwapAt(f, slot, v.D)));
            var adj = f.Occupied().Where(o => o.Slot != slot && FormationRules.AreAdjacent(slot, o.Slot)).Select(o => o.Def.Name).ToList();
            double n = Mean25(r["新"]);
            dp.Add(n - Mean25(r["素体"]));
            Console.WriteLine("| " + name + " | " + lowDef.Name + "（" + D(lowA) + "）・" + SlotName(slot) + " | " + string.Join("・", adj) + " | "
                              + baseW.ToString("F1") + " | " + Mean25(r["素体"]).ToString("F1") + " | " + Mean25(r["旧"]).ToString("F1") + " | **"
                              + n.ToString("F1") + "** | " + Mean25(r["マイナスなし"]).ToString("F1") + " | " + D(n - baseW) + " | **"
                              + D(n - Mean25(r["素体"])) + "** | " + D(n - Mean25(r["旧"])) + " | " + D(n - Mean25(r["マイナスなし"])) + " | "
                              + Cells(r["新"]) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- " + dp.Count + " 行 ／ Δ素体 の平均 **" + D(dp.Average()) + "**（正 " + dp.Count(x => x > 0.05) + " ／ 負 " + dp.Count(x => x < -0.05) + "）");
        Console.WriteLine();
    }

    // =================================================================================
    // ledger —— 帳簿（§4.3・印の版）
    // =================================================================================

    sealed class Led
    {
        public int N;
        public double Reach25Sum, Reach50Sum; public int Reach25N, Reach50N;
        public double PMaxSum, BMaxSum, PEarly, PLate, BEarly, BLate, PSecond, BSecond, PYoke, BYoke;
        public double Fires, Dry, MCenter, MAround, MAlly, CenterBurning, NoNew;
        public double AllyPSecond, AllyBSecond, AllyPTick, AllyBTick, AllyPoisonDeaths, AllyBurnDeaths;
        public double BeniPoison, BeniBurn, VioStored;
        public long MaxP, MaxB;
        public double MarkPeakSum, FoeFiresSum, AllyFiresSum; public long MarkPeakMax, FoeFiresMax, AllyFiresMax;
    }

    static Led LedgerOf(Formation f, int stFrom = 1, int stTo = 4)
    {
        var mine = f.Occupied().Select(o => o.Def.Id).Distinct().ToHashSet();
        var l = new Led();
        object gate = new();
        for (int st = stFrom; st <= stTo; st++)
        {
            Formation enemy = EnemyCatalog.Stages[st].Enemy;
            Parallel.For(0, Seeds, seed =>
            {
                BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false, harm: new HarmRule(true));
                long r25 = 0, r50 = 0, pmax = 0, bmax = 0, ffire = 0, afire = 0;
                double pe = 0, pl = 0, be = 0, bl = 0, ps = 0, bs = 0, py = 0, by = 0;
                double aps = 0, abs = 0, apt = 0, abt = 0, apd = 0, abd = 0;
                foreach (var (id, t) in r.TallyByUnit)
                {
                    if (mine.Contains(id))
                    {
                        aps += t.PoisonTickSecond; abs += t.BurnTickSecond; afire = Math.Max(afire, t.TickFiresMax);
                        apt += t.PoisonTickEarly + t.PoisonTickLate; abt += t.BurnTickEarly + t.BurnTickLate;
                        if (t.HarmFatal is not null)
                        {
                            apd += t.HarmFatal[(int)DamageRoute.Poison];
                            abd += t.HarmFatal[(int)DamageRoute.Burn];
                        }
                        continue;
                    }
                    if (id.EndsWith("_plain")) continue;
                    r25 = UnitTally.MinReach(r25, t.PoisonReach25);
                    r50 = UnitTally.MinReach(r50, t.PoisonReach50);
                    pmax = Math.Max(pmax, t.PoisonTickMax); bmax = Math.Max(bmax, t.BurnTickMax); ffire = Math.Max(ffire, t.TickFiresMax);
                    pe += t.PoisonTickEarly; pl += t.PoisonTickLate; be += t.BurnTickEarly; bl += t.BurnTickLate;
                    ps += t.PoisonTickSecond; bs += t.BurnTickSecond; py += t.PoisonYokeCut; by += t.BurnYokeCut;
                }
                r.TallyByUnit.TryGetValue("mio", out UnitTally? m);
                r.TallyByUnit.TryGetValue("beni", out UnitTally? b);
                r.TallyByUnit.TryGetValue("vio", out UnitTally? v);
                lock (gate)
                {
                    l.N++;
                    if (r25 > 0) { l.Reach25Sum += r25; l.Reach25N++; }
                    if (r50 > 0) { l.Reach50Sum += r50; l.Reach50N++; }
                    l.PMaxSum += pmax; l.BMaxSum += bmax; l.MaxP = Math.Max(l.MaxP, pmax); l.MaxB = Math.Max(l.MaxB, bmax);
                    l.PEarly += pe; l.PLate += pl; l.BEarly += be; l.BLate += bl; l.PSecond += ps; l.BSecond += bs;
                    l.PYoke += py; l.BYoke += by;
                    l.FoeFiresSum += ffire; l.AllyFiresSum += afire; l.FoeFiresMax = Math.Max(l.FoeFiresMax, ffire); l.AllyFiresMax = Math.Max(l.AllyFiresMax, afire);
                    if (m is not null) { l.MarkPeakSum += m.ConcMarkPeak; l.MarkPeakMax = Math.Max(l.MarkPeakMax, m.ConcMarkPeak); }
                    l.AllyPSecond += aps; l.AllyBSecond += abs; l.AllyPTick += apt; l.AllyBTick += abt;
                    l.AllyPoisonDeaths += apd; l.AllyBurnDeaths += abd;
                    if (m is not null)
                    {
                        l.Fires += m.ConcFires; l.Dry += m.ConcDry; l.MCenter += m.ConcMarkCenter; l.MAround += m.ConcMarkAround;
                        l.MAlly += m.ConcMarkAlly; l.CenterBurning += m.ConcCenterBurning; l.NoNew += m.ConcNoNew;
                    }
                    if (b is not null) { l.BeniPoison += b.InversePoisonHealed; l.BeniBurn += b.InverseBurnHealed; }
                    if (v is not null) l.VioStored += v.SpitStored;
                }
            });
        }
        return l;
    }

    static string R25(Led l) => l.Reach25N == 0 ? "—" : (l.Reach25Sum / l.Reach25N).ToString("F2") + "（" + (100.0 * l.Reach25N / l.N).ToString("F0") + "%）";
    static string R50(Led l) => l.Reach50N == 0 ? "—" : (l.Reach50Sum / l.Reach50N).ToString("F2") + "（" + (100.0 * l.Reach50N / l.N).ToString("F0") + "%）";
    static string F(double x, int n, string fmt = "F1") => (x / n).ToString(fmt);

    static void RunLedger()
    {
        Console.WriteLine("# 第194期 `mio ledger` —— 帳簿（印の版・1戦あたり・第2〜5波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("**敵の側は刻みの時点で記録する**（旧のミオでも同じ計数が取れる）。`毒25` は敵の誰かの毒の刻み（1回ぶん）が初めて 25 以上になったターンの平均"
                          + "（届いた戦だけ・括弧は届いた戦の割合）。`1回の最大` は1戦の中で敵1体が1回の刻みで受けた最大の平均（毒・燃焼を分ける）。"
                          + "`2回目` は印で来た2回目の刻みの名目。`軛超過` は軛が効いている間に上限 25 を越えた刻みの数（第4波だけで起きる）。");
        Console.WriteLine();

        var swaps = SwapRows();
        var all = new List<(string Name, Formation Old, Formation New, Formation YP)>();
        foreach (var (_, name, f) in MioRows()) all.Add((name, Apply(f, MioOld), f, Apply(f, MioYP)));
        foreach (var (name, f, slot, _, _, _) in swaps)
            all.Add(("差し替え: " + name, SwapAt(f, slot, MioOld), SwapAt(f, slot, MioNew), SwapAt(f, slot, MioYP)));
        var cache = all.ToDictionary(x => x.Name, x => (Old: LedgerOf(x.Old), New: LedgerOf(x.New), YP: LedgerOf(x.YP)));

        Console.WriteLine("## 表C. 敵の側（旧 → 新）——速さと刻み");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 毒25（T・届いた%） | 毒50 | 1回の最大 毒 | 1回の最大 燃焼 | 毒 1〜3T | 毒 4T〜 | うち2回目 毒 | 燃焼 1〜3T | 燃焼 4T〜 | うち2回目 燃焼 | 軛超過 毒・燃焼 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var x in all)
            foreach (var (tag, l) in new[] { ("旧", cache[x.Name].Old), ("新", cache[x.Name].New) })
                Console.WriteLine("| " + x.Name + " | " + tag + " | " + R25(l) + " | " + R50(l) + " | " + F(l.PMaxSum, l.N) + " | " + F(l.BMaxSum, l.N) + " | "
                                  + F(l.PEarly, l.N) + " | " + F(l.PLate, l.N) + " | " + F(l.PSecond, l.N) + " | "
                                  + F(l.BEarly, l.N) + " | " + F(l.BLate, l.N) + " | " + F(l.BSecond, l.N) + " | "
                                  + F(l.PYoke, l.N, "F2") + "・" + F(l.BYoke, l.N, "F2") + " |");
        Console.WriteLine();

        Console.WriteLine("## 表D. 印の帳簿と味方の側（新・マイナスなし）");
        Console.WriteLine();
        Console.WriteLine("`中心` `周り` `漏れ` は足した印の延べ（1手番に1体 +1）。`印の最大` は1戦で付けた印の最大値（平均・括弧は全戦の最大）、`発火の最大 敵・味方` は1回の刻みで発火した回数の最大（平均・括弧は全戦の最大。印の無い戦は 0）。"
                          + "`中心が燃焼` は中心が燃えていた手番。味方の `2回目` は印で来た2回目の刻みの名目（反転で回復になった分を含む）。"
                          + "`毒で倒れた` `火で倒れた` は倒れた一撃が毒・燃焼の刻みだった数（害の帳簿 `HarmFatal`）。`ベニの反転` は反転で実際に癒えた量。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 試みた | 空振り | 中心 | 周り | 漏れ | 印の最大 | 発火の最大 敵・味方 | 中心が燃焼 | 味方の刻み 毒・燃焼 | 味方の2回目 毒・燃焼 | 毒で倒れた | 火で倒れた | ベニの反転 毒・燃焼 | ヴィオの腹 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var x in all)
            foreach (var (tag, l) in new[] { ("新", cache[x.Name].New), ("マイナスなし", cache[x.Name].YP), ("旧", cache[x.Name].Old) })
                Console.WriteLine("| " + x.Name + " | " + tag + " | " + F(l.Fires, l.N, "F2") + " | " + F(l.Dry, l.N, "F2") + " | "
                                  + F(l.MCenter, l.N, "F2") + " | " + F(l.MAround, l.N, "F2") + " | " + F(l.MAlly, l.N, "F2") + " | "
                                  + F(l.MarkPeakSum, l.N, "F2") + "（" + l.MarkPeakMax + "） | " + F(l.FoeFiresSum, l.N, "F2") + "（" + l.FoeFiresMax + "）・" + F(l.AllyFiresSum, l.N, "F2") + "（" + l.AllyFiresMax + "） | " + F(l.CenterBurning, l.N, "F2") + " | "
                                  + F(l.AllyPTick, l.N) + "・" + F(l.AllyBTick, l.N) + " | " + F(l.AllyPSecond, l.N) + "・" + F(l.AllyBSecond, l.N) + " | "
                                  + F(l.AllyPoisonDeaths, l.N, "F2") + " | " + F(l.AllyBurnDeaths, l.N, "F2") + " | "
                                  + F(l.BeniPoison, l.N) + "・" + F(l.BeniBurn, l.N) + " | " + F(l.VioStored, l.N) + " |");
        Console.WriteLine();

        Console.WriteLine("## 表E. 波ごと（新・敵の毒の刻み 1〜3T ／ 4T〜 ／ うち2回目 ／ 軛超過の数）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 第2波 | 第3波 | 第4波（軛） | 第5波 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var x in all)
        {
            var cells = Enumerable.Range(1, 4).Select(st =>
            {
                Led l = LedgerOf(x.New, st, st);
                return F(l.PEarly, l.N) + " ／ " + F(l.PLate, l.N) + " ／ " + F(l.PSecond, l.N) + " ／ " + F(l.PYoke, l.N, "F2");
            });
            Console.WriteLine("| " + x.Name + " | " + string.Join(" | ", cells) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 1回の刻みの最大（新・全行・全戦）: 毒 **" + cache.Values.Max(c => c.New.MaxP) + "** ／ 燃焼 **" + cache.Values.Max(c => c.New.MaxB) + "**"
                          + " ／ 1戦の印の最大 **" + cache.Values.Max(c => c.New.MarkPeakMax) + "** ／ 1回の刻みで発火した回数の最大 敵 **" + cache.Values.Max(c => c.New.FoeFiresMax) + "**・味方 **" + cache.Values.Max(c => c.New.AllyFiresMax) + "**");
        Console.WriteLine();
    }

    // =================================================================================
    // 自己検査
    // =================================================================================

    static Dictionary<string, double[]> ReadBalance(string path)
    {
        var want = new Dictionary<string, double[]>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            string[] c = line.Split('|').Select(x => x.Trim()).ToArray();
            if (c.Length < 8) continue;
            want[c[1]] = c.Skip(2).Take(5).Select(x => double.Parse(x.TrimEnd('%'))).ToArray();
        }
        return want;
    }

    static void Check(string arg)
    {
        Console.WriteLine("# 第194期 `mio check` —— 自己検査（印＝回数の版）");
        Console.WriteLine();
        string path = string.IsNullOrWhiteSpace(arg) ? Path.Combine("docs", "balance.md") : arg.Trim();
        if (!File.Exists(path)) { Console.WriteLine("**`" + path + "` が無い。止める。**"); return; }
        var want = ReadBalance(path);

        // (a)(b)
        int diffA = 0, seenA = 0, diffB = 0, seenB = 0;
        foreach (var (name, f) in CompareBuilds())
        {
            if (!want.TryGetValue(name, out double[]? w)) { diffB += 5; continue; }
            double[] rOld = Rates(Apply(f, MioOld));
            for (int i = 0; i < 5; i++) { seenB++; if (Math.Abs(rOld[i] - w[i]) > 0.001) diffB++; }
            if (HasMio(f)) continue;
            double[] r = Rates(f);
            for (int i = 0; i < 5; i++) { seenA++; if (Math.Abs(r[i] - w[i]) > 0.001) diffA++; }
        }
        Console.WriteLine("- (a) ミオを含まない `compare` 行のセルが `" + path + "` とずれた数: **" + diffA + " / " + seenA + "**（0 が正）");
        Console.WriteLine("- (b) ミオを旧に戻した `compare` の全セルが `" + path + "` とずれた数: **" + diffB + " / " + seenB + "**（0 が正・採用前の表を渡すこと）");

        // (c) 乱数を引かない
        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
            static string Body(string src, string head, string end)
            {
                int i = src.IndexOf(head);
                if (i < 0) return "";
                int j = src.IndexOf(end, i + head.Length);
                return j < 0 ? "" : src.Substring(i, j - i);
            }
            string b = Body(traits, "class " + "ConcentrateTrait", "class " + "ConcentrateLeakTrait")
                     + Body(engine, "public bool " + "MarkConcentrated(", "void " + "NoteTickLayer(")
                     + Body(engine, "void " + "DetonateTwiceIfMarked(", "void " + "DetonateOne(");
            string pick = "Pick" + "One", roll = "Roll" + "(", shuf = "Shuffl" + "ed";
            int Count(string x) => (b.Length - b.Replace(x, "").Length) / x.Length;
            Console.WriteLine("- (c) 濃縮・印・起爆の2回目の本体が `" + pick + "` / `Roll` / `" + shuf + "` を呼ぶ回数: **"
                              + (Count(pick) + Count(roll) + Count(shuf)) + "**（0 が正・走査した本体 " + b.Length + " 字。空なら止める）");
        }

        // (d) 台本: 印が読めるか・印のある駒の刻みが1ターンに2組並ぶか・燃焼の残りターンは1回分しか減らないか
        {
            var f = CompareBuilds().First(r => r.Name.StartsWith("毒 (グザ×ミオ×ラウ)")).F;
            for (int seed = 0; seed < 20; seed++)
            {
                BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[1].Enemy, seed, verbose: true);
                var marks = r.Events.Where(e => e.Kind == BattleEventKind.ConcentrateMark).ToList();
                if (marks.Count == 0) continue;
                int foe = marks.First(e => e.Text == ConcentrateTrait.CenterLabel).TargetId ?? -1;
                var perTurn = r.Events.Where(e => e.Kind == BattleEventKind.Status && e.TargetId == foe && e.Text == "毒" && e.ActorId is null)
                                      .GroupBy(e => e.Turn).OrderBy(g => g.Key).ToList();
                int ok = 0, bad = 0;
                var shown = new List<string>();
                foreach (var g in perTurn)
                {
                    int n = marks.Count(e => e.TargetId == foe && e.Turn < g.Key);   // 印はミオの手番で付くので、その後のターン頭から効く
                    if (g.Count() == 1 + n) ok++; else bad++;
                    shown.Add(g.Count() + "/" + (1 + n));
                }
                Console.WriteLine("- (d) 台本（`毒 (グザ×ミオ×ラウ)` × 第二波 × seed " + seed + "）: `ConcentrateMark` **" + marks.Count + "** 件（中心 "
                                  + marks.Count(e => e.Text == ConcentrateTrait.CenterLabel) + " ／ 周り " + marks.Count(e => e.Text == ConcentrateTrait.AroundLabel)
                                  + " ／ 漏れ " + marks.Count(e => e.Text == ConcentrateTrait.LeakLabel) + "）・ミオが書き手でない件 " + marks.Count(e => e.ActorId is null)
                                  + "（0 が正）。中心の敵の毒の `Status` の件数/ターン（実際/1+n）: [" + string.Join(", ", shown) + "] ——一致 " + ok + " ／ 不一致 **" + bad
                                  + "**（不一致は途中で倒れたターンだけが正）");
                break;
            }
        }

        // (e) 燃焼の残りターンは1回分しか減らない: 印のある燃えている駒の残りターンの推移（1戦の中で追う）
        {
            // 燃焼の書き手がいる台（ボルグ）でミオの隣に置き、味方の漏れで印の付いた燃えている駒を探す
            Formation g = Formation.Build(front1: UnitCatalog.Borg, front3: UnitCatalog.Hota, center: UnitCatalog.Mio,
                                          back1: UnitCatalog.Guza, back3: UnitCatalog.Gald);
            int twice = 0, burnTurns = 0, burnMarked = 0, seedUsed = -1;
            for (int seed = 0; seed < 40 && burnMarked == 0; seed++)
            {
                BattleResult r = BattleEngine.Run(g, EnemyCatalog.Stages[1].Enemy, seed, verbose: true);
                var marked = r.Events.Where(e => e.Kind == BattleEventKind.ConcentrateMark).GroupBy(e => e.TargetId ?? -1).ToDictionary(g => g.Key, g => g.Min(e => e.Turn));
                foreach (var grp in r.Events.Where(e => e.Kind == BattleEventKind.Status && e.Text == "燃焼" && e.ActorId is null && e.TargetId is not null)
                                            .GroupBy(e => (e.TargetId!.Value, e.Turn)))
                {
                    if (!marked.TryGetValue(grp.Key.Item1, out int mt) || grp.Key.Item2 <= mt) continue;
                    int n = r.Events.Count(e => e.Kind == BattleEventKind.ConcentrateMark && e.TargetId == grp.Key.Item1 && e.Turn < grp.Key.Item2);
                    burnMarked++; if (grp.Count() == 1 + n) twice++;
                }
                burnTurns = r.Events.Count(e => e.Kind == BattleEventKind.Status && e.Text == "燃焼");
                seedUsed = seed;
            }
            Console.WriteLine("- (e) 燃焼（ボルグ＋ミオの台 × 第二波 × seed " + seedUsed + "）: 印のある駒の燃焼の刻み（駒×ターン）" + burnMarked
                              + " 組のうち、1ターンに 1+n 回来た組 **" + twice + "**（全部が正・0 組なら測れていない）");
        }

        // (g) 広い確認: 印のある駒 × ターンの刻み（毒・燃焼）の件数が 1+n か。ずれた組は、そのターンに倒れた駒だけが正
        {
            var forms = MioRows().Select(x => x.F).ToList();
            forms.Add(Formation.Build(front1: UnitCatalog.Borg, front3: UnitCatalog.Hota, center: UnitCatalog.Mio,
                                      back1: UnitCatalog.Guza, back3: UnitCatalog.Gald));
            long ok = 0, died = 0, bad = 0, maxN = 0, burnGroups = 0;
            object gate = new();
            foreach (Formation f in forms)
                for (int st = 1; st < 5; st++)
                {
                    Formation enemy = EnemyCatalog.Stages[st].Enemy;
                    Parallel.For(0, 20, seed =>
                    {
                        BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: true);
                        var marks = r.Events.Where(e => e.Kind == BattleEventKind.ConcentrateMark).ToList();
                        long o = 0, d = 0, b = 0, mx = 0, bg = 0;
                        foreach (var g in r.Events.Where(e => e.Kind == BattleEventKind.Status && e.ActorId is null && e.TargetId is not null
                                                            && (e.Text == "毒" || e.Text == "燃焼"))
                                                  .GroupBy(e => (T: e.TargetId!.Value, e.Turn, e.Text)))
                        {
                            int n = marks.Count(e => e.TargetId == g.Key.T && e.Turn < g.Key.Turn);
                            if (n == 0) continue;
                            mx = Math.Max(mx, n);
                            if (g.Key.Text == "燃焼") bg++;
                            if (g.Count() == 1 + n) { o++; continue; }
                            bool dead = r.Events.Any(e => e.Kind == BattleEventKind.Death && e.TargetId == g.Key.T && e.Turn == g.Key.Turn);
                            if (dead && g.Count() < 1 + n) d++; else b++;
                        }
                        lock (gate) { ok += o; died += d; bad += b; maxN = Math.Max(maxN, mx); burnGroups += bg; }
                    });
                }
            Console.WriteLine("- (g) 在席行＋ボルグの台 × 第2〜5波 × seed 0..19 の、印のある駒 × ターンの刻み（毒・燃焼）: 1+n 回 **" + ok + "** 組 ／ "
                              + "途中で倒れて足りない " + died + " 組 ／ **それ以外のずれ " + bad + " 組**（0 が正）。うち燃焼 " + burnGroups + " 組・見た印の最大 n = " + maxN);
        }

        // (f) 旧の札の保持者
        Console.WriteLine("- (f) `Amplifier` の保持者（`UnitCatalog.All`）: **" + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Amplifier))
                          + "** 枚（0 が正）／ `Concentrate` " + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Concentrate))
                          + " 枚 ／ `ConcentrateLeak` " + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.ConcentrateLeak)) + " 枚（1 が正）");
        Console.WriteLine();
    }
}
