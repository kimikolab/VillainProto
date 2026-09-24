using BattleCore;
using static Common;

// =====================================================================================
// sid モード（第195期）の本体 —— 主表（ガルド → スィド）・在席行の版の対照・帳簿・自己検査
//
// **旧の駒・札を外した版はこの診断のローカルに写してある**（`UnitCatalog` には新しいスィドしか居ない）。
// Id は本物と同じ（帳簿を同じキーで引く）。素体だけは Id を変える（第69期からの器具）。
// =====================================================================================

static partial class SidDiag
{
    static partial void RunMoreImpl(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run": RunVersions(); handled = true; return;
            case "ledger": RunLedger(); handled = true; return;
            case "check": Check(arg); handled = true; return;
            case "beni": RunBeni(); handled = true; return;
            case "phase196": Phase196(); handled = true; return;
            default: RunMore196(mode, arg, ref handled); return;
        }
    }

    // =================================================================================
    // beni —— 事後・参考（**採否には使わない**）。主表のベニのいる2行でベニを中央へ動かす
    // =================================================================================

    static Formation SwapSeats(Formation f, string a, string b)
    {
        int sa = f.Occupied().First(o => o.Def.Id == a).Slot, sb = f.Occupied().First(o => o.Def.Id == b).Slot;
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = slot == sa ? f[sb]! : slot == sb ? f[sa]! : d;
        return g;
    }

    static void RunBeni()
    {
        Console.WriteLine("# 第195期 `sid beni` —— 事後・参考: ベニを中央に動かした主表（**採否には使わない**）");
        Console.WriteLine();
        Console.WriteLine("主表（§4.1）のベニのいる2行は、どちらも**ベニがガルドの席の隣にいない**（予測 P3 が外れた理由）。"
                          + "ここではベニを**中央の駒と入れ替えて**（中央は隣4枚）同じ4列を測る。**行の組み方は事後に決めた**ので、結果は参考として読む。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 並び | 元 | **新** | 旧 | 素体 | 穴（元−新） | スィドの値 | 新の波別 | スィドが倒れた | うち1〜2T | ベニの隣にガルドの席 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|---|--:|--:|:-:|");
        foreach (var (name, f) in MainRows().Where(r => Has(r.F, "beni")))
        {
            string mid = f[2]!.Id;
            foreach (var (tag, g) in new[] { ("主表のまま", f), ("ベニ ↔ " + f[2]!.Name + "（ベニを中央へ）", SwapSeats(f, "beni", mid)) })
            {
                int gs = g.Occupied().First(o => o.Def.Id == "gald").Slot, bs = g.Occupied().First(o => o.Def.Id == "beni").Slot;
                M mo = Measure(g), mn = Measure(GaldTo(g, SidNew));
                double o = Mean25(mo.W), n = Mean25(mn.W), ol = Mean25(Rates(GaldTo(g, SidOld))), p = Mean25(Rates(GaldTo(g, SidPlainG)));
                Console.WriteLine("| " + name + " | " + tag + " | " + o.ToString("F1") + " | **" + n.ToString("F1") + "** | " + ol.ToString("F1") + " | " + p.ToString("F1") + " | "
                                  + D(o - n) + " | " + D(n - p) + " | " + Cells(mn.W) + " | " + P(mn.SidDeaths, mn.N) + " | " + Early(mn) + " | "
                                  + (FormationRules.AreAdjacent(gs, bs) ? "○" : "—") + " |");
            }
        }
        Console.WriteLine();
    }

    // =================================================================================
    // 版
    // =================================================================================

    static UnitDef Clone(UnitDef d, TraitId[] traits, IReadOnlyList<UnitAction>? actions) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Pattern = d.Pattern,
        Advances = d.Advances, Actions = actions, Traits = traits,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    static readonly UnitDef SidNew = UnitCatalog.Sid;
    /// <summary>旧スィド（第194期まで）: 毒撃（`Venom`）だけ・毎手番殴る。</summary>
    static readonly UnitDef SidOld = Clone(UnitCatalog.Sid, new[] { TraitId.Venom }, null);
    /// <summary>新・鈍らせなし（`Numb` を外す＝印が付かない）。</summary>
    static readonly UnitDef SidNoNumb = Clone(UnitCatalog.Sid, new[] { TraitId.Spew, TraitId.Venom }, UnitCatalog.Sid.Actions);
    /// <summary>新・手番なし（`Spew` を外す。`Actions` は `Skill` のまま＝手番に何もしない）。</summary>
    static readonly UnitDef SidNoSpew = Clone(UnitCatalog.Sid, new[] { TraitId.Venom, TraitId.Numb }, UnitCatalog.Sid.Actions);
    /// <summary>素体（スィドと同じ数値・特性なし・毎手番殴る）。</summary>
    static readonly UnitDef SidPlain = Plain("sid_plain");
    /// <summary>§4.1 でガルドの席に置く素体（スィドの元の席の素体と帳簿を分けるため Id だけ違う）。</summary>
    static readonly UnitDef SidPlainG = Plain("sid_plain_g");

    static UnitDef Plain(string id) => new()
    {
        Id = id, Name = "素体のスィド", MaxHp = UnitCatalog.Sid.MaxHp, Attack = UnitCatalog.Sid.Attack,
        Speed = UnitCatalog.Sid.Speed, Pattern = UnitCatalog.Sid.Pattern, Traits = Array.Empty<TraitId>(),
    };

    static readonly (string Tag, UnitDef D)[] Versions =
    {
        ("旧", SidOld), ("新", SidNew), ("鈍らせなし", SidNoNumb), ("手番なし", SidNoSpew), ("素体", SidPlain),
    };

    static Formation Apply(Formation f, UnitDef sid)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == "sid" ? sid : d;
        return g;
    }

    /// <summary>§4.1 の組み方: ガルドの席に <paramref name="atGald"/>、元のスィドの席（あれば）に同じ数値の素体。</summary>
    static Formation GaldTo(Formation f, UnitDef atGald)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = d.Id == "gald" ? atGald : d.Id == "sid" ? SidPlain : d;
        return g;
    }

    // =================================================================================
    // 測る（勝率と帳簿を1回の走行で）
    // =================================================================================

    sealed class M
    {
        public double[] W = new double[5];
        public int[] Stall = new int[5];
        // 以下は第2〜5波の合計（N 戦）
        public int N;
        public double TeamTakenFoe, SidDeaths, SidEarly, SidDeathTurnSum, SidTaken;
        public double[] SidHarm = new double[DamageRoutes.Names.Length];
        public double Cut, CutSpew, CutVenom, Swings, CapSwings, LayerSum; public long LayerMax;
        public double CapReached, CapTurnSum, Marks, SpewMarks, VenomMarks, SpewActs;
        public bool HasSid;
    }

    static M Measure(Formation f, int stFrom = 1, int stTo = 4, int seeds = Seeds)
    {
        var mine = f.Occupied().Select(o => o.Def.Id).ToHashSet();
        var m = new M { HasSid = mine.Contains("sid") };
        object gate = new();
        for (int st = 0; st < 5; st++)
        {
            bool led = st >= stFrom && st <= stTo;
            int wins = 0, stall = 0;
            Formation enemy = EnemyCatalog.Stages[st].Enemy;
            Parallel.For(0, seeds, seed =>
            {
                BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: false, harm: new HarmRule(true));
                if (r.PlayerWon) Interlocked.Increment(ref wins);
                else if (r.Turns >= BattleEngine.MaxTurns) Interlocked.Increment(ref stall);
                if (!led) return;
                double team = 0, cut = 0, cs = 0, cv = 0, sw = 0, cap = 0, ls = 0; long lm = 0, capTurn = 0;
                foreach (var (id, t) in r.TallyByUnit)
                {
                    if (mine.Contains(id)) { team += t.DamageTaken - t.TakenFromAlly; continue; }
                    cut += t.NumbedCut; cs += t.NumbedCutSpew; cv += t.NumbedCutVenom; sw += t.NumbedSwings; cap += t.NumbedCapSwings;
                    ls += t.NumbedLayerSum; lm = Math.Max(lm, t.NumbedLayerMax); capTurn = UnitTally.MinReach(capTurn, t.NumbedCapTurn);
                }
                r.TallyByUnit.TryGetValue("sid", out UnitTally? s);
                lock (gate)
                {
                    m.N++;
                    m.TeamTakenFoe += team; m.Cut += cut; m.CutSpew += cs; m.CutVenom += cv; m.Swings += sw; m.CapSwings += cap;
                    m.LayerSum += ls; m.LayerMax = Math.Max(m.LayerMax, lm);
                    if (capTurn > 0) { m.CapReached++; m.CapTurnSum += capTurn; }
                    if (s is not null)
                    {
                        m.SpewActs += s.SpewActs; m.SpewMarks += s.SpewMarks; m.VenomMarks += s.VenomMarks; m.Marks += s.SpewMarks + s.VenomMarks;
                        m.SidTaken += s.DamageTaken;
                        if (s.HarmAmount is not null) for (int i = 0; i < m.SidHarm.Length && i < s.HarmAmount.Length; i++) m.SidHarm[i] += s.HarmAmount[i];
                        if (s.Deaths > 0)
                        {
                            m.SidDeaths++;
                            m.SidDeathTurnSum += s.LastActiveTurn;
                            if (s.LastActiveTurn <= 2) m.SidEarly++;
                        }
                    }
                }
            });
            m.W[st] = 100.0 * wins / seeds;
            m.Stall[st] = stall;
        }
        return m;
    }

    static double[] Rates(Formation f, int seeds = Seeds)
    {
        var w = new double[5];
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            Formation enemy = EnemyCatalog.Stages[st].Enemy;
            Parallel.For(0, seeds, seed =>
            {
                if (BattleEngine.Run(f, enemy, seed, verbose: false).PlayerWon) Interlocked.Increment(ref wins);
            });
            w[st] = 100.0 * wins / seeds;
        }
        return w;
    }

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static string Cells(double[] w) => string.Join(" / ", w.Skip(1).Select(x => x.ToString("F1")));
    static string D(double x) => x.ToString("+0.0;-0.0;0.0");
    static string P(double a, int n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1") + "%";
    static string Per(double a, int n, string fmt = "F1") => n == 0 ? "—" : (a / n).ToString(fmt);
    static string DeathT(M m) => m.SidDeaths == 0 ? "—" : (m.SidDeathTurnSum / m.SidDeaths).ToString("F2");
    static string Early(M m) => m.SidDeaths == 0 ? "—" : (100.0 * m.SidEarly / m.SidDeaths).ToString("F0") + "%";
    static string CapT(M m) => m.CapReached == 0 ? "—" : (m.CapTurnSum / m.CapReached).ToString("F2") + "（" + P(m.CapReached, m.N) + "）";

    // =================================================================================
    // run —— 主表（§4.1）と在席行の版（§4.2）
    // =================================================================================

    static void RunVersions()
    {
        Console.WriteLine("# 第195期 `sid run` —— 主表（ガルド → スィド）・在席行 × 版（**線は置かない**）");
        Console.WriteLine();
        Console.WriteLine("seed 0..199。平均は第2〜5波（規約 (G10)）。**新** ＝ 第195期のスィド（手番で吐く・殴ってきた敵にも毒・痺れ毒）／ **旧** ＝ 第194期までのスィド（毒撃だけ・攻撃する）／"
                          + " **素体** ＝ スィドと同じ数値（HP84 攻4 速9）・特性なし・毎手番殴る。");
        Console.WriteLine();

        // ---------------- §4.1 ----------------
        Console.WriteLine("## 表A. 主表 —— ガルドの席にスィド（§4.1・**測る前に固定**）");
        Console.WriteLine();
        Console.WriteLine("**元** ＝ そのまま（ガルドあり・スィドがいれば新スィド）。スィドのいる行は、スィドをガルドの席へ移し、元のスィドの席を**同じ数値の素体**で埋める"
                          + "（素体の列ではガルドの席も素体）。**穴** ＝ 元 − 新（ガルドの穴の残り）／ **スィドの値** ＝ 新 − 素体。"
                          + "`被ダメ` は隊全体が敵から受けたダメージ（1戦あたり・味方由来を除く）。`倒れ` はガルドの席のスィドが倒れた戦の割合、`1〜2T` はそのうち2ターン目までに倒れた割合。");
        Console.WriteLine();
        Console.WriteLine("| 行 | ベニ | ミオ | 元 | **新** | 旧 | 素体 | 穴（元−新） | 新−旧 | **スィドの値** | 新の波別 | 被ダメ 元 → 新（旧・素体） | 倒れ（新） | 1〜2T | 倒れたT |");
        Console.WriteLine("|---|:-:|:-:|--:|--:|--:|--:|--:|--:|--:|---|---|--:|--:|--:|");
        var main = MainRows();
        var holes = new List<(string Name, bool Beni, bool Mio, double Hole, double Val, double VsOld)>();
        var mainNew = new List<(string Name, M Mn, bool Mio)>();
        foreach (var (name, f) in main)
        {
            M mo = Measure(f), mn = Measure(GaldTo(f, SidNew)), mold = Measure(GaldTo(f, SidOld)), mp = Measure(GaldTo(f, SidPlainG));
            double o = Mean25(mo.W), n = Mean25(mn.W), ol = Mean25(mold.W), p = Mean25(mp.W);
            bool beni = Has(f, "beni"), mio = Has(f, "mio");
            holes.Add((name, beni, mio, o - n, n - p, n - ol));
            mainNew.Add((name, mn, mio));
            Console.WriteLine("| " + name + " | " + (beni ? "○" : "—") + " | " + (mio ? "○" : "—") + " | " + o.ToString("F1") + " | **" + n.ToString("F1") + "** | "
                              + ol.ToString("F1") + " | " + p.ToString("F1") + " | " + D(o - n) + " | " + D(n - ol) + " | **" + D(n - p) + "** | " + Cells(mn.W) + " | "
                              + Per(mo.TeamTakenFoe, mo.N, "F0") + " → **" + Per(mn.TeamTakenFoe, mn.N, "F0") + "**（" + Per(mold.TeamTakenFoe, mold.N, "F0") + "・" + Per(mp.TeamTakenFoe, mp.N, "F0") + "） | "
                              + P(mn.SidDeaths, mn.N) + " | " + Early(mn) + " | " + DeathT(mn) + " |");
        }
        Console.WriteLine();
        if (holes.Count > 0)
        {
            var b = holes.Where(h => h.Beni).ToList(); var nb = holes.Where(h => !h.Beni).ToList();
            Console.WriteLine("- 新 > 旧 の行 **" + holes.Count(h => h.VsOld > 0.05) + " / " + holes.Count + "** ／ 新 > 素体 の行 **" + holes.Count(h => h.Val > 0.05) + " / " + holes.Count
                              + "** ／ 元に届かない（穴 > 0）行 **" + holes.Count(h => h.Hole > 0.05) + " / " + holes.Count + "**");
            Console.WriteLine("- 穴の平均: ベニあり " + b.Count + " 行 **" + (b.Count == 0 ? "—" : D(b.Average(h => h.Hole))) + "** ／ ベニなし " + nb.Count + " 行 **"
                              + (nb.Count == 0 ? "—" : D(nb.Average(h => h.Hole))) + "**");
            Console.WriteLine("- スィドの値の平均: **" + D(holes.Average(h => h.Val)) + "**（正 " + holes.Count(h => h.Val > 0.05) + " ／ 負 " + holes.Count(h => h.Val < -0.05) + "）");
        }
        Console.WriteLine();

        // ---------------- §4.2 ----------------
        Console.WriteLine("## 表B. スィドの在席行 × 版（§4.2・スィドは元の席のまま）");
        Console.WriteLine();
        Console.WriteLine("**代金ではなく札ごとの値**: **鈍らせ** ＝ 新 − 鈍らせなし ／ **手番** ＝ 新 − 手番なし ／ **Δ旧** ＝ 新 − 旧 ／ **Δ素体** ＝ 新 − 素体。"
                          + "`倒れ` `1〜2T` はスィドが倒れた戦の割合と、そのうち2ターン目までの割合（P5 の比較の相手）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | スィドの席 | 旧 | **新** | 鈍らせなし | 手番なし | 素体 | Δ旧 | 鈍らせ | 手番 | Δ素体 | 新の波別 | 倒れ（新） | 1〜2T | 倒れたT |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|--:|--:|--:|");
        foreach (var (name, f) in CompareBuilds())
        {
            if (!Has(f, "sid")) continue;
            int slot = f.Occupied().First(o => o.Def.Id == "sid").Slot;
            var r = Versions.ToDictionary(v => v.Tag, v => v.Tag == "新" ? Measure(Apply(f, v.D)) : new M { W = Rates(Apply(f, v.D)) });
            double n = Mean25(r["新"].W);
            M mn = r["新"];
            Console.WriteLine("| " + name + " | " + SlotName(slot) + " | " + Mean25(r["旧"].W).ToString("F1") + " | **" + n.ToString("F1") + "** | "
                              + Mean25(r["鈍らせなし"].W).ToString("F1") + " | " + Mean25(r["手番なし"].W).ToString("F1") + " | " + Mean25(r["素体"].W).ToString("F1") + " | "
                              + D(n - Mean25(r["旧"].W)) + " | " + D(n - Mean25(r["鈍らせなし"].W)) + " | " + D(n - Mean25(r["手番なし"].W)) + " | " + D(n - Mean25(r["素体"].W)) + " | "
                              + Cells(mn.W) + " | " + P(mn.SidDeaths, mn.N) + " | " + Early(mn) + " | " + DeathT(mn) + " |");
        }
        Console.WriteLine();

        // ---------------- P4 ----------------
        Console.WriteLine("## 表C. 上限 60% に届くまで（表A の「新」・P4）");
        Console.WriteLine();
        Console.WriteLine("`届いたT` は1戦の中で初めて上限の一撃が出たターンの平均（届いた戦だけ・括弧は届いた戦の割合）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | ミオ | 届いたT | 減った一撃/戦 | うち上限 | 層の平均 | 層の最大 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|");
        foreach (var (name, mn, mio) in mainNew)
            Console.WriteLine("| " + name + " | " + (mio ? "○" : "—") + " | " + CapT(mn) + " | " + Per(mn.Swings, mn.N, "F2") + " | " + Per(mn.CapSwings, mn.N, "F2") + " | "
                              + (mn.Swings == 0 ? "—" : (mn.LayerSum / mn.Swings).ToString("F1")) + " | " + mn.LayerMax + " |");
        Console.WriteLine();
        double CapAvg(IEnumerable<M> ms) { var l = ms.Where(m => m.CapReached > 0).ToList(); return l.Count == 0 ? double.NaN : l.Sum(m => m.CapTurnSum) / l.Sum(m => m.CapReached); }
        Console.WriteLine("- 届いたT（戦で重みづけ）: ミオあり **" + CapAvg(mainNew.Where(x => x.Mio).Select(x => x.Mn)).ToString("F2") + "** ／ ミオなし **"
                          + CapAvg(mainNew.Where(x => !x.Mio).Select(x => x.Mn)).ToString("F2") + "**");
        Console.WriteLine();
    }

    // =================================================================================
    // ledger —— 帳簿（§4.3）
    // =================================================================================

    static void RunLedger()
    {
        Console.WriteLine("# 第195期 `sid ledger` —— 帳簿（1戦あたり・第2〜5波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("`減らした量` は痺れ毒で削った打点の合計（印が付いた経路で分ける: 吐いた敵 ／ 殴ってきた敵）。`印` は新しく印を付けた敵の数。"
                          + "`層` は減った一撃のときの毒の層（平均・括弧は全戦の最大）。`上限` は減少が 60% に届いた一撃の数と、初めて届いたターン。");
        Console.WriteLine();
        var all = new List<(string Name, Formation F)>();
        foreach (var (name, f) in MainRows()) all.Add(("主表・新: " + name, GaldTo(f, SidNew)));
        foreach (var (name, f) in CompareBuilds()) if (Has(f, "sid")) all.Add(("在席: " + name, f));
        var cache = all.Select(x => (x.Name, M: Measure(x.F))).ToList();

        Console.WriteLine("## 表D. 痺れ毒");
        Console.WriteLine();
        Console.WriteLine("| 行 | 吐いた手番 | 印 吐き・殴られ | 減った一撃 | 減らした量 | うち吐き | うち殴られ | 層 | 上限の一撃 | 上限に届いたT |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, m) in cache)
            Console.WriteLine("| " + name + " | " + Per(m.SpewActs, m.N, "F2") + " | " + Per(m.SpewMarks, m.N, "F2") + "・" + Per(m.VenomMarks, m.N, "F2") + " | "
                              + Per(m.Swings, m.N, "F2") + " | " + Per(m.Cut, m.N) + " | " + Per(m.CutSpew, m.N) + " | " + Per(m.CutVenom, m.N) + " | "
                              + (m.Swings == 0 ? "—" : (m.LayerSum / m.Swings).ToString("F1")) + "（" + m.LayerMax + "） | " + Per(m.CapSwings, m.N, "F2") + " | " + CapT(m) + " |");
        Console.WriteLine();

        Console.WriteLine("## 表E. スィドの生き死にと、隊が敵から受けたダメージ");
        Console.WriteLine();
        Console.WriteLine("`スィドが受けた` は経路別（害の帳簿・第135期）。`隊の被ダメ` は味方全員が敵から受けたダメージ（味方由来を除く）。");
        Console.WriteLine();
        var routes = Enumerable.Range(0, DamageRoutes.Names.Length).Where(i => cache.Any(c => c.M.SidHarm[i] > 0)).ToList();
        Console.WriteLine("| 行 | 倒れた | 1〜2T | 倒れたT | スィドが受けた | " + string.Join(" | ", routes.Select(i => DamageRoutes.Names[i])) + " | 隊の被ダメ |");
        Console.WriteLine("|---|--:|--:|--:|--:|" + string.Concat(routes.Select(_ => "--:|")) + "--:|");
        foreach (var (name, m) in cache)
            Console.WriteLine("| " + name + " | " + P(m.SidDeaths, m.N) + " | " + Early(m) + " | " + DeathT(m) + " | " + Per(m.SidTaken, m.N) + " | "
                              + string.Join(" | ", routes.Select(i => Per(m.SidHarm[i], m.N))) + " | " + Per(m.TeamTakenFoe, m.N, "F0") + " |");
        Console.WriteLine();

        Console.WriteLine("## 表F. 波ごと（主表・新: 減らした量 ／ 隊の被ダメ ／ スィドが倒れた割合）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 第2波（粛） | 第3波（渇き） | 第4波（軛） | 第5波 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var (name, f) in MainRows())
        {
            Formation g = GaldTo(f, SidNew);
            var cells = Enumerable.Range(1, 4).Select(st =>
            {
                M m = Measure(g, st, st);
                return Per(m.Cut, m.N) + " ／ " + Per(m.TeamTakenFoe, m.N, "F0") + " ／ " + P(m.SidDeaths, m.N);
            });
            Console.WriteLine("| " + name + " | " + string.Join(" | ", cells) + " |");
        }
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
        Console.WriteLine("# 第195期 `sid check` —— 自己検査");
        Console.WriteLine();
        string path = string.IsNullOrWhiteSpace(arg) ? Path.Combine("docs", "balance.md") : arg.Trim();
        if (!File.Exists(path)) { Console.WriteLine("**`" + path + "` が無い。止める。**"); return; }
        var want = ReadBalance(path);

        // (a)(b)
        int diffA = 0, seenA = 0, diffB = 0, seenB = 0;
        foreach (var (name, f) in CompareBuilds())
        {
            if (!want.TryGetValue(name, out double[]? w)) { diffB += 5; continue; }
            double[] rOld = Rates(Apply(f, SidOld));
            for (int i = 0; i < 5; i++) { seenB++; if (Math.Abs(rOld[i] - w[i]) > 0.001) diffB++; }
            if (Has(f, "sid")) continue;
            double[] r = Rates(f);
            for (int i = 0; i < 5; i++) { seenA++; if (Math.Abs(r[i] - w[i]) > 0.001) diffA++; }
        }
        Console.WriteLine("- (a) スィドを含まない `compare` 行のセルが `" + path + "` とずれた数: **" + diffA + " / " + seenA + "**（0 が正）");
        Console.WriteLine("- (b) スィドを旧に戻した `compare` の全セルが `" + path + "` とずれた数: **" + diffB + " / " + seenB + "**（0 が正・採用前の表を渡すこと）");
        Console.WriteLine("- (a') 交差帯にスィドのいる行: **" + CrossBuilds().Count(r => Has(r.F, "sid")) + "**（0 なら交差帯は札に触れない）");

        // (c) 乱数を引かない
        if (ParryScan.Init())
        {
            string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
            string engine = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
            string b = Body(traits, "class " + "SpewTrait", "class " + "NumbTrait")
                     + Body(traits, "class " + "NumbTrait", "\n}\n")
                     + Body(traits, "class " + "VenomTrait", "class " + "ImmobileTrait")
                     + Body(engine, "public bool " + "MarkNumbed(", "public bool " + "MarkConcentrated(")
                     + Body(engine, "int numbCut = 0, numbPct = 0;", "string label = pattern switch")
                     + Body(traits, "public static UnitState? Pick(BattleContext ctx, UnitState self)\n    {\n        UnitState? pick = null;\n        foreach (UnitState u in ctx.LivingMembers(ctx.Opponent(self.TeamId)))\n        {\n            if (pick is null)", "public override void OnAction");
            string pick = "Pick" + "One", roll = "Roll" + "(", shuf = "Shuffl" + "e";
            int Count(string x) => (b.Length - b.Replace(x, "").Length) / x.Length;
            Console.WriteLine("- (c) 吐く・痺れ毒・毒撃・印・減少の本体が `" + pick + "` / `Roll` / `" + shuf + "` を呼ぶ回数: **"
                              + (Count(pick) + Count(roll) + Count(shuf)) + "**（0 が正・走査した本体 " + b.Length + " 字。空なら止める）");
        }

        // (d) 上限と、印・層の条件（台本で確かめる）
        {
            long attacks = 0, numbed = 0, over = 0, noMark = 0, badStep = 0, markedNoCut = 0, marks = 0, cutSum = 0, capHits = 0;
            object gate = new();
            var forms = MainRows().Select(r => GaldTo(r.F, SidNew)).Concat(CompareBuilds().Where(r => Has(r.F, "sid")).Select(r => r.F)).ToList();
            foreach (Formation f in forms)
                for (int st = 1; st < 5; st++)
                {
                    Formation enemy = EnemyCatalog.Stages[st].Enemy;
                    Parallel.For(0, 20, seed =>
                    {
                        BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: true);
                        var markedAt = new Dictionary<int, int>();
                        for (int i = 0; i < r.Events.Count; i++)
                        {
                            BattleEvent e = r.Events[i];
                            if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Numbed && e.TargetId is int tid) markedAt.TryAdd(tid, i);
                        }
                        long a = 0, n = 0, o = 0, nm = 0, bs = 0, mnc = 0, cs = 0, ch = 0;
                        for (int i = 0; i < r.Events.Count; i++)
                        {
                            BattleEvent e = r.Events[i];
                            if (e.Kind != BattleEventKind.Attack || e.ActorId is null) continue;
                            a++;
                            bool marked = markedAt.TryGetValue(e.ActorId.Value, out int at) && at < i;
                            if (e.NumbPercent is int pct)
                            {
                                n++; cs += e.NumbCut ?? 0;
                                if (pct > NumbTrait.MaxPercent) o++;
                                if (pct == NumbTrait.MaxPercent) ch++;
                                if (pct % NumbTrait.PercentPerLayer != 0 && pct != NumbTrait.MaxPercent) bs++;
                                if (!marked) nm++;
                            }
                            else if (marked) mnc++;
                        }
                        lock (gate) { attacks += a; numbed += n; over += o; noMark += nm; badStep += bs; markedNoCut += mnc; marks += markedAt.Count; cutSum += cs; capHits += ch; }
                    });
                }
            Console.WriteLine("- (d) 主表の新・在席行 × 第2〜5波 × seed 0..19 の台本: `Attack` " + attacks + " 件・うち痺れ毒で減った **" + numbed + "** 件（上限の一撃 " + capHits
                              + "・減らした量の合計 " + cutSum + "）・印 " + marks + " 体");
            Console.WriteLine("  - 上限 " + NumbTrait.MaxPercent + "% を超えた一撃: **" + over + "**（0 が正）／ 3% 刻みでない割合: **" + badStep + "**（0 が正）");
            Console.WriteLine("  - **印の無い駒**で減った一撃: **" + noMark + "**（0 が正）／ 印のある駒の一撃で減らなかったもの " + markedNoCut
                              + "（毒の層が 0 のときだけが正——層は台本に無いので下の (e) の計数で確かめる）");
        }

        // (e) 層 0 のときは減らない: 計数（NumbedSwings は層 > 0 のときだけ数える）と、刻みで毒が 0 になった印持ちの一撃
        {
            // 毒を持たせずに印だけ付く台: 旧の毒撃を持たない（新スィドだけが毒を書く）編成で、敵の毒が 0 になる瞬間は無い（毒は減らない）。
            // だから層 0 の印持ちが振るのは「毒の層を 0 にされた」とき——ヴィオの吸い上げは味方の毒しか吸わないので、敵では起きない。
            Console.WriteLine("- (e) 層 0 で減らないこと: engine の条件は `layers > 0` の1箇所（`NoteNumbed` もその内側）。敵の毒の層を 0 に戻す経路は `Traits.cs` に "
                              + (ParryScan.Root is null ? "?" : CountEnemyPoisonReset().ToString()) + " 本（ヴィオの吸い上げ・業の引き取りは味方側だけ）");
        }

        // (f) 保持者
        Console.WriteLine("- (f) `UnitCatalog.All` の保持者: `Spew` **" + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Spew)) + "** ／ `Numb` **"
                          + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Numb)) + "** ／ `Venom` **" + UnitCatalog.All.Count(d => d.Traits.Contains(TraitId.Venom))
                          + "**（3 つとも 1 が正）。スィドの `Actions`: " + (UnitCatalog.Sid.Actions is { Count: 1 } a && a[0].Kind == ActionKind.Skill ? "`[Skill]`（攻撃しない）" : "**違う**"));
        Console.WriteLine("- (g) `PoisonRoute` " + Enum.GetValues<PoisonRoute>().Length + " 本 ／ `SoakRouteCount` " + BattleContext.SoakRouteCount + " ／ 燃焼の添字 "
                          + BattleContext.SoakBurnRouteIx + "（毒の経路の本数 ＝ 燃焼の添字が正）");
        Console.WriteLine();
    }

    static int CountEnemyPoisonReset()
    {
        string traits = ParryScan.Read(Path.Combine("BattleCore", "Traits.cs"));
        string tok = "SetCounter(StatusKeys." + "Poison, 0)";
        return (traits.Length - traits.Replace(tok, "").Length) / tok.Length;
    }
}
