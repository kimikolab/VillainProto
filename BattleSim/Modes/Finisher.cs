using BattleCore;
using static Common;

// =====================================================================================
// finisher モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "finisher")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 finisher
// =====================================================================================

static class FinisherDiag
{
// 止め（第53期）の診断。**出力は docs/ に置かない**（標準出力で読むだけ）。
// `CompareBuilds()` には**2行足した**が、`Stages` / `Columns` は触っていない。
//
// **「発火」と「列越え」を分けること。** 第42期は「生成したアーマー」ではなく「実際に吸った量」、
// 第43期は「流した量」ではなく「効き」、第47期は「貫き」ではなく「後列到達」、
// 第50期は「焦点」ではなく「撃破順」、第52期は「渡した量」ではなく「効き」で判断した。
// **標を持つ敵を殴った回数は成果ではない**——第50期に「標だけが前列の壁を無視する」と判明した以上、
// **この駒の価値は倍率ではなく列越えにある可能性がある。**
//
// **「止めた砲火」が代金の実体。** 標を消すと engine の `MarkPullPercent`(75) が切れるので、
// 味方全体の集中砲火が終わる。**これが測れないと受け入れ基準7 の判定ができない。**
//
//     dotnet run --project BattleSim -c Release 0 finisher [絞り込み]
//     dotnet run --project BattleSim -c Release 0 finisher sweep    # Multiplier 1/2/3/4 の掃引だけ
//     dotnet run --project BattleSim -c Release 0 finisher cross    # ソラ・ザン・カリとの同居だけ
//     dotnet run --project BattleSim -c Release 0 finisher seats    # 席の分散だけ（seats2 の写し）
//     dotnet run --project BattleSim -c Release 0 finisher confirm  # 配置の追試（seed 200..599）
//     dotnet run --project BattleSim -c Release 0 finisher alt      # 機構の帰属を別 seed 帯で追試
public static void Run(string[] args, int stageIndex)
{
    var fnBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> fnStages = EnemyCatalog.Stages;
    const int FnSeeds = 200;   // compare / spread / divert / goad と揃える
    int FnMain = FinisherRule.Default.Multiplier;
    int[] fnMults = { 1, 2, 3, 4 };   // 1 は「倍率なし」の参照点（指示書の掃引 2/3/4 に足した）

    string fnMode = args.Length > 2 ? args[2] : "";

    static bool FnHasTome(Formation f) => f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Tome));

    var fnTargets = fnBuilds.Where(b => FnHasTome(b.F)).ToArray();
    if (fnMode.Length > 0 && fnMode != "sweep" && fnMode != "confirm"
        && fnMode != "alt" && fnMode != "cross" && fnMode != "seats")
        fnTargets = fnTargets.Where(b => fnMode.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();

    // **素体の対照（対照1）。** トメと数値・型・速さが1つも違わず、特性だけを持たない駒。
    // **`FinisherRule` を弱める形の対照は使わない**（第47期 `ScaleRule(0)` の失敗）——
    // `Multiplier = 1` にしても対象の強制と標の消費は走るので、
    // 「機構が効いたのか、ただ 58/12/6 の体が入っただけか」が割れない。
    UnitDef FnPlainDef = new()
    {
        Id = "tome_plain", Name = "素体のトメ", MaxHp = UnitCatalog.Tome.MaxHp,
        Attack = UnitCatalog.Tome.Attack, Speed = UnitCatalog.Tome.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Tome.Pattern
    };
    Formation FnPlain(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, UnitCatalog.Tome) ? FnPlainDef : d;
        return g;
    }
    // トメを外した4体版（対照3）。**第21期の飽和検査**も兼ねる。
    static Formation FnWithoutTome(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (!ReferenceEquals(d, UnitCatalog.Tome)) g[slot] = d;
        return g;
    }
    // ソラを外した版（**供給を断つ**）。トメが素の攻12として振る舞うことの確認（指示書 §7-4）。
    UnitDef FnSoraPlainDef = new()
    {
        Id = "sora_plain", Name = "素体のソラ", MaxHp = UnitCatalog.Sora.MaxHp,
        Attack = UnitCatalog.Sora.Attack, Speed = UnitCatalog.Sora.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Sora.Pattern
    };
    Formation FnNoSupply(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, UnitCatalog.Sora) ? FnSoraPlainDef : d;
        return g;
    }

    // トメの席だけを振った5変種（他の4枚は元の相対順のまま詰める）。
    static Formation FnSeat(Formation f, int seat)
    {
        var others = f.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Tome))
                      .Select(o => o.Def).ToList();
        var g = new Formation();
        g[seat] = UnitCatalog.Tome;
        int k = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount && k < others.Count; i++)
            if (i != seat) g[i] = others[k++];
        return g;
    }

    FnStat MeasureFn(Formation f, Formation enemy, FinisherRule rule)
    {
        var z = new FnStat();
        for (int seed = 0; seed < FnSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false, finisher: rule);
            if (r.PlayerWon) z.Win++;
            z.Turns += r.Turns;
            z.Fires += r.FinisherFires; z.Idle += r.FinisherIdle; z.Cross += r.FinisherCross;
            z.Consumed += r.FinisherConsumed; z.Kills += r.FinisherKills;
            z.WaitSum += r.FinisherWaitSum; z.WaitCount += r.FinisherWaitCount;
            z.AllySingles += r.FinisherAllySingles; z.Starved += r.FinisherStarved;
            z.Supply += r.DivertFocus; z.SupplyFresh += r.DivertFocusFresh;
            z.DvSingles += r.DivertAllySingles; z.DvOnMarked += r.DivertAllyOnMarked;
            foreach ((string k, int v) in r.FinisherTargetTo)
                z.TargetTo[k] = z.TargetTo.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string id, UnitTally t) in r.TallyByUnit)
            {
                if (!z.Dmg.ContainsKey(id))
                { z.Dmg[id] = 0; z.Taken[id] = 0; z.Last[id] = 0; z.Deaths[id] = 0; }
                z.Dmg[id] += t.DamageToEnemy;
                z.Taken[id] += t.DamageTaken;
                z.Last[id] += t.LastActiveTurn;
                z.Deaths[id] += t.Deaths;
            }
        }
        double n = FnSeeds;
        z.Win = z.Win * 100 / n; z.Turns /= n;
        z.Fires /= n; z.Idle /= n; z.Cross /= n; z.Consumed /= n; z.Kills /= n;
        z.WaitSum /= n; z.WaitCount /= n; z.AllySingles /= n; z.Starved /= n;
        z.Supply /= n; z.SupplyFresh /= n; z.DvSingles /= n; z.DvOnMarked /= n;
        foreach (string k in z.TargetTo.Keys.ToList()) z.TargetTo[k] /= n;
        foreach (string k in z.Dmg.Keys.ToList())
        { z.Dmg[k] /= n; z.Taken[k] /= n; z.Last[k] /= n; z.Deaths[k] /= n; }
        return z;
    }

    (double[] Wins, FnStat Z) FnAll(Formation f, FinisherRule rule)
    {
        var wins = new double[fnStages.Count];
        var acc = new FnStat();
        for (int w = 0; w < fnStages.Count; w++)
        {
            var z = MeasureFn(f, fnStages[w].Enemy, rule);
            wins[w] = z.Win;
            acc.Turns += z.Turns; acc.Fires += z.Fires; acc.Idle += z.Idle; acc.Cross += z.Cross;
            acc.Consumed += z.Consumed; acc.Kills += z.Kills;
            acc.WaitSum += z.WaitSum; acc.WaitCount += z.WaitCount;
            acc.AllySingles += z.AllySingles; acc.Starved += z.Starved;
            acc.Supply += z.Supply; acc.SupplyFresh += z.SupplyFresh;
            acc.DvSingles += z.DvSingles; acc.DvOnMarked += z.DvOnMarked;
            foreach ((string k, double v) in z.TargetTo)
                acc.TargetTo[k] = acc.TargetTo.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, double v) in z.Dmg)
            {
                if (!acc.Dmg.ContainsKey(k))
                { acc.Dmg[k] = 0; acc.Taken[k] = 0; acc.Last[k] = 0; acc.Deaths[k] = 0; }
                acc.Dmg[k] += v; acc.Taken[k] += z.Taken[k];
                acc.Last[k] += z.Last[k]; acc.Deaths[k] += z.Deaths[k];
            }
        }
        double m = fnStages.Count;
        acc.Win = wins.Average();
        acc.Turns /= m; acc.Fires /= m; acc.Idle /= m; acc.Cross /= m;
        acc.Consumed /= m; acc.Kills /= m; acc.WaitSum /= m; acc.WaitCount /= m;
        acc.AllySingles /= m; acc.Starved /= m; acc.Supply /= m; acc.SupplyFresh /= m;
        acc.DvSingles /= m; acc.DvOnMarked /= m;
        foreach (string k in acc.TargetTo.Keys.ToList()) acc.TargetTo[k] /= m;
        foreach (string k in acc.Dmg.Keys.ToList())
        { acc.Dmg[k] /= m; acc.Taken[k] /= m; acc.Last[k] /= m; acc.Deaths[k] /= m; }
        return (wins, acc);
    }

    double[] FnWins(Formation f)
    {
        var v = new double[fnStages.Count];
        for (int w = 0; w < fnStages.Count; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < FnSeeds; seed++)
                if (BattleEngine.Run(f, fnStages[w].Enemy, seed, false).PlayerWon) wins++;
            v[w] = wins * 100.0 / FnSeeds;
        }
        return v;
    }

    static string FnCells(double[] w) => string.Concat(w.Select(x => $" {x:0.0}% |"));
    static string FnTop(Dictionary<string, double> d, int n = 3)
    {
        var parts = d.Where(x => x.Value > 0).OrderByDescending(x => x.Value).Take(n)
            .Select(x => $"{x.Key} {x.Value:0.00}").ToList();
        return parts.Count == 0 ? "—" : string.Join(" / ", parts);
    }
    static double FnGet(Dictionary<string, double> d, string k)
        => k.Length > 0 && d.TryGetValue(k, out double v) ? v : 0;
    static string FnPct(double num, double den) => den <= 0 ? "—" : $"{num * 100 / den:0.0}%";

    Console.WriteLine("# 止め（finisher）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 finisher [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{FnSeeds - 1}。数字は特記なければ**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`Stages` / `Columns` は触っていない。`CompareBuilds()` には**2行足した**（代金が立つ台と立たない台の対）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 発火 | 標を持つ敵を殴った回数/戦。**0 になっていないことが受け入れ基準3・4** |");
    Console.WriteLine("| 空振り | 標を持つ敵が1体もいなくて通常攻撃になった回数/戦 |");
    Console.WriteLine("| 対象 | 誰を殴ったかの内訳（敵の名前ごと） |");
    Console.WriteLine("| **列越え** | **標が無ければ狙えなかった敵**（`pool` の外＝中列・後列）を殴った回数/戦。**主眼1** |");
    Console.WriteLine("| 消費 | 標を消した回数/戦 |");
    Console.WriteLine("| **止めた砲火** | 標を消したターンに味方が振った単体攻撃のうち、**盤上に標持ちが1体も残っていなかった**回数/戦。**主眼2**（推定値） |");
    Console.WriteLine("| 供給 | ソラが敵に標を付けた回数/戦（括弧内は新規） |");
    Console.WriteLine("| 遊休 | 標が付いてから止めが殴るまでの平均ターン数 |");
    Console.WriteLine("| 撃破 | 標を持つ敵を**実際に倒した**回数/戦 |");
    Console.WriteLine("| 焦点の効き | 味方の単体振りのうち標持ちの敵に当たった割合（engine の `MarkPullPercent` の実効値） |");
    Console.WriteLine();
    Console.WriteLine("> **発火は成果ではない。** 第50期に「標だけが `pool` ではなく `foes` から選ばれる」と");
    Console.WriteLine("> 判明した以上、**この駒の価値は倍率ではなく列越えかもしれない**（受け入れ基準6）。");
    Console.WriteLine("> **止めた砲火が代金の実体**——標を消すと `MarkPullPercent` も切れる（受け入れ基準7）。");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準1・2）-----------------------------------------------------------
    if (fnMode.Length == 0)
    {
        Console.WriteLine("## 0. 検算");
        Console.WriteLine();
        var plain = fnBuilds.Where(b => !FnHasTome(b.F)).ToArray();
        int cells = 0, diff = 0;
        foreach (var b in plain)
            for (int w = 0; w < fnStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < FnSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, fnStages[w].Enemy, seed, false,
                            finisher: new FinisherRule(1, false)).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, fnStages[w].Enemy, seed, false,
                            finisher: new FinisherRule(9, true)).PlayerWon) c++;
                }
                cells++;
                if (a != c) diff++;
            }
        Console.WriteLine($"- **基準2**（トメを含まない {plain.Length} 行が `FinisherRule` の値に対して不変）: "
            + $"**{cells} セル中 {diff} 件の食い違い**（{plain.Length} 行 × {fnStages.Count} 波・`(1, false)` 対 `(9, true)`）");
        Console.WriteLine("- **基準1**（新駒を編成に入れない状態で `compare` が `docs/balance.md` と完全一致）は");
        Console.WriteLine("  行を足す前に `compare` の全文で確認済み（**270 セル中 0 件**）。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 1. 主表 ------------------------------------------------------------------------------
    if (fnMode.Length == 0 || (fnMode != "sweep" && fnMode != "confirm"
                               && fnMode != "alt" && fnMode != "cross" && fnMode != "seats"))
    {
        Console.WriteLine($"## 1. 主表（`Multiplier = {FnMain}` と陽性対照）");
        Console.WriteLine();
        Console.WriteLine("`素体` = トメと**数値・型・速さが1つも違わず特性だけを持たない駒**（対照1）。");
        Console.WriteLine("**これが機構の帰属を取る唯一の窓口**——`Multiplier = 1` にしても対象の強制と消費は走る。");
        Console.WriteLine("`消費なし` = `FinisherRule.Consume = false`（対照2）。**標を消さない版**で、");
        Console.WriteLine("倍率で殴るだけが残る——**代金の分離**。**差が小さければサイクルは代金として働いていない。**");
        Console.WriteLine("`供給なし` = ソラを同数値・特性なしの素体に差し替えた版（指示書 §7-4）。");
        Console.WriteLine("**トメが素の攻12として振る舞う**ことの確認。");
        Console.WriteLine("`4体` = トメを外した4体版（対照3。**第21期の飽和検査**も兼ねる）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in fnTargets)
        {
            var (wins, _) = FnAll(b.F, new FinisherRule(FnMain));
            var (nc, _) = FnAll(b.F, new FinisherRule(FnMain, false));
            var (ns, _) = FnAll(FnNoSupply(b.F), new FinisherRule(FnMain));
            var pw = FnWins(FnPlain(b.F));
            var fw = FnWins(FnWithoutTome(b.F));
            Console.WriteLine($"| {b.Name} | **M{FnMain}** |{FnCells(wins)} {wins.Average():0.0}% |");
            Console.WriteLine($"| | 消費なし（標を消さない） |{FnCells(nc)} {nc.Average():0.0}% |");
            Console.WriteLine($"| | 供給なし（ソラを素体に） |{FnCells(ns)} {ns.Average():0.0}% |");
            Console.WriteLine($"| | 素体（特性なし・同数値） |{FnCells(pw)} {pw.Average():0.0}% |");
            Console.WriteLine($"| | 4体（トメ抜き） |{FnCells(fw)} {fw.Average():0.0}% |");
            Console.WriteLine($"| | **機構の帰属（M{FnMain} − 素体）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - pw[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - pw.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | **代金の値段（M{FnMain} − 消費なし）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - nc[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - nc.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | **供給の値段（M{FnMain} − 供給なし）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - ns[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - ns.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | 体の値段（素体 − 4体） | "
                + string.Concat(Enumerable.Range(0, pw.Length).Select(i => $"{pw[i] - fw[i]:+0.0;-0.0;0.0} |"))
                + $" **{pw.Average() - fw.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine($"### 機構の計数（`Multiplier = {FnMain}`・5波の平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | **発火** | 空振り | **列越え** | 列越え率 | 消費 | 撃破 | 遊休 | 供給(新規) | 対象 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var b in fnTargets)
        {
            var (_, z) = FnAll(b.F, new FinisherRule(FnMain));
            Console.WriteLine($"| {b.Name} | **{z.Fires:0.00}** | {z.Idle:0.00} | **{z.Cross:0.00}** "
                + $"| **{FnPct(z.Cross, z.Fires)}** | {z.Consumed:0.00} | {z.Kills:0.00} "
                + $"| {(z.WaitCount > 0 ? $"{z.WaitSum / z.WaitCount:0.00}T" : "—")} "
                + $"| {z.Supply:0.00} ({z.SupplyFresh:0.00}) | {FnTop(z.TargetTo)} |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // --- 2. 止めた砲火（代金の実体）-------------------------------------------------------
        Console.WriteLine("## 2. 止めた砲火（**主眼2**・受け入れ基準7）");
        Console.WriteLine();
        Console.WriteLine("**標を消すと engine の `MarkPullPercent`(75) が切れる。** 味方の単体攻撃はもう");
        Console.WriteLine("その敵へ引かれないので、**味方全体の集中砲火を自分で終わらせている。**");
        Console.WriteLine("`焦点の効き` は味方の単体振りのうち標持ちに当たった割合（第50期の 81.9% / 85.8% と比べる）。");
        Console.WriteLine("**対照2（消費なし）との差が代金の厳密な値。**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 味方の単体振り | **止めた砲火** | 割合 | **焦点の効き** | 発火 | 列越え | 平均勝率 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in fnTargets)
        {
            var (wins, z) = FnAll(b.F, new FinisherRule(FnMain));
            var (ncw, nc) = FnAll(b.F, new FinisherRule(FnMain, false));
            var (plw, pl) = FnAll(FnPlain(b.F), new FinisherRule(FnMain));
            Console.WriteLine($"| {b.Name} | **M{FnMain}** | {z.AllySingles:0.00} | **{z.Starved:0.00}** "
                + $"| {FnPct(z.Starved, z.AllySingles)} | **{FnPct(z.DvOnMarked, z.DvSingles)}** "
                + $"| {z.Fires:0.00} | {z.Cross:0.00} | {wins.Average():0.0}% |");
            Console.WriteLine($"| | 消費なし | {nc.AllySingles:0.00} | {nc.Starved:0.00} "
                + $"| {FnPct(nc.Starved, nc.AllySingles)} | **{FnPct(nc.DvOnMarked, nc.DvSingles)}** "
                + $"| {nc.Fires:0.00} | {nc.Cross:0.00} | {ncw.Average():0.0}% |");
            Console.WriteLine($"| | 素体 | {pl.AllySingles:0.00} | {pl.Starved:0.00} | — "
                + $"| **{FnPct(pl.DvOnMarked, pl.DvSingles)}** | — | — | {plw.Average():0.0}% |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // --- 3. 波ごとの内訳 -------------------------------------------------------------------
        Console.WriteLine($"## 3. 波ごとの内訳（`Multiplier = {FnMain}`）");
        Console.WriteLine();
        Console.WriteLine("**列越えは戦闘長と相関するはず**（開戦時の最高HPの敵はほぼ前列で、");
        Console.WriteLine("前列が削れて初めて中央・後列が最高HPになる）。予測2 の当否はここで見る。");
        Console.WriteLine();
        foreach (var b in fnTargets)
        {
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 | 発火 | 空振り | **列越え** | 列越え率 | 消費 | 撃破 | 止めた砲火 | 焦点の効き | 供給 | 遊休 | トメ寿命 | 決着T |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            for (int w = 0; w < fnStages.Count; w++)
            {
                var z = MeasureFn(b.F, fnStages[w].Enemy, new FinisherRule(FnMain));
                Console.WriteLine($"| {fnStages[w].Name} | {z.Win:0.0}% | {z.Fires:0.00} | {z.Idle:0.00} "
                    + $"| **{z.Cross:0.00}** | **{FnPct(z.Cross, z.Fires)}** | {z.Consumed:0.00} | {z.Kills:0.00} "
                    + $"| {z.Starved:0.00} | {FnPct(z.DvOnMarked, z.DvSingles)} | {z.Supply:0.00} "
                    + $"| {(z.WaitCount > 0 ? $"{z.WaitSum / z.WaitCount:0.00}" : "—")} "
                    + $"| {FnGet(z.Last, "tome"):0.00} | {z.Turns:0.0} |");
                Console.Out.Flush();
            }
            Console.WriteLine();
            Console.WriteLine("**対象の内訳（5波それぞれ）**");
            Console.WriteLine();
            Console.WriteLine("| 波 | 対象 |");
            Console.WriteLine("|---|---|");
            for (int w = 0; w < fnStages.Count; w++)
            {
                var z = MeasureFn(b.F, fnStages[w].Enemy, new FinisherRule(FnMain));
                Console.WriteLine($"| {fnStages[w].Name} | {FnTop(z.TargetTo, 5)} |");
                Console.Out.Flush();
            }
            Console.WriteLine();
        }
    }

    // --- 4. 掃引 ------------------------------------------------------------------------------
    if (fnMode.Length == 0 || fnMode == "sweep")
    {
        Console.WriteLine("## 4. 掃引（`Multiplier` 1 / 2 / 3 / 4）");
        Console.WriteLine();
        Console.WriteLine("**`Multiplier` が動かす量は打点1本だけ**——標の消費量も列越えの有無も");
        Console.WriteLine("倍率に依存しないので、第50期の `TargetCount`（総量と取り分が逆を向く打ち消し型）とは");
        Console.WriteLine("構造が違う。第52期の基準（**ノブが動かす量を1本にする**）で選んだノブ。");
        Console.WriteLine("**全幅が小さいときの切り分けは「ノブが機構の計数を動かしたか」で付ける**");
        Console.WriteLine("——(a) ノブが機構を動かさない（第41・47期）／(b) 動かすが出力が小さい（第49期）／");
        Console.WriteLine("(c) 動かすが2つの量が打ち消す（第50期）／(d) 動かす量が1本で素直に開く（第52期）。");
        Console.WriteLine();
        Console.WriteLine("> **第四波には軛（1回のダメージの上限 25）がある。** 攻12 × 3 = 36 は切られるので、");
        Console.WriteLine("> **M3 以上は第四波でだけ頭打ちになる**はず（予測5）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | M | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 発火 | 列越え | 撃破 | トメ与ダメ | トメ寿命 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in fnTargets)
        {
            foreach (int m in fnMults)
            {
                var (wins, z) = FnAll(b.F, new FinisherRule(m));
                Console.WriteLine($"| {b.Name} | {m} | **{wins.Average():0.0}%** |{FnCells(wins)} "
                    + $"{z.Fires:0.00} | {z.Cross:0.00} | {z.Kills:0.00} | {FnGet(z.Dmg, "tome"):0.0} "
                    + $"| {FnGet(z.Last, "tome"):0.00} |");
                Console.Out.Flush();
            }
            var pw = FnWins(FnPlain(b.F));
            Console.WriteLine($"| {b.Name} | 素体 | **{pw.Average():0.0}%** |{FnCells(pw)} — | — | — | — | — |");
        }
        Console.WriteLine();
        foreach (var b in fnTargets)
        {
            var vals = fnMults.Select(m => FnAll(b.F, new FinisherRule(m)).Wins.Average()).ToList();
            Console.WriteLine($"- **{b.Name} の掃引の全幅: {vals.Max() - vals.Min():0.0}pt**"
                + $"（{string.Join(" / ", fnMults.Zip(vals, (t, v) => $"M{t} {v:0.0}%"))}）");
        }
        Console.WriteLine();
        Console.WriteLine("### 第四波だけの掃引（軛の Cap 25 の効き）");
        Console.WriteLine();
        Console.WriteLine("| 行 | M1 | M2 | M3 | M4 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var b in fnTargets)
        {
            var v = fnMults.Select(m => MeasureFn(b.F, fnStages[3].Enemy, new FinisherRule(m)).Win).ToList();
            Console.WriteLine($"| {b.Name} |{string.Concat(v.Select(x => $" {x:0.0}% |"))}");
            Console.Out.Flush();
        }
        Console.WriteLine();
        if (fnMode == "sweep") return;
    }

    // --- 5. 干渉（ザン・カリとの同居）----------------------------------------------------------
    if (fnMode.Length == 0 || fnMode == "cross")
    {
        Console.WriteLine("## 5. 干渉（ザン＝仇討ち / カリ＝駆り立て との同居）");
        Console.WriteLine();
        Console.WriteLine("**潰さずに測る**（予測6・7）。ザンは**味方**の標を読み、トメは**敵**の標を読む");
        Console.WriteLine("——同じ通貨の逆の陣営なので、**互いに干渉しないはず**。");
        Console.WriteLine("カリは**味方**に標を付けるので、**トメの対象は1体も増えないはず**");
        Console.WriteLine("（供給源はソラだけ）。**発火の列で確かめる。**");
        Console.WriteLine();
        var cross = new (string Name, Formation F)[]
        {
            // ザン同居。**味方の標の読み手（ザン）と敵の標の読み手（トメ）が同じ盤に立つ。**
            // ソラが毎ターン味方の標を外すので、ザンはもともと飢えている（第50期）。
            ("ザン同居（トメ後1 / ザン後3）",
             Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Gald,
                             center: UnitCatalog.Sora, back1: UnitCatalog.Tome,
                             back3: UnitCatalog.Zan)),
            // カリ同居。**カリは味方に標を付ける**ので、トメの対象集合は1体も増えない。
            ("カリ同居（トメ後1 / カリ後3）",
             Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Gald,
                             center: UnitCatalog.Sora, back1: UnitCatalog.Tome,
                             back3: UnitCatalog.Kari)),
            // 対照（同じ顔ぶれで5枚目を素体に）。**この3行の差が干渉の帰属。**
            ("対照（トメ後1 / 5枚目は素体のカリ）",
             Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Gald,
                             center: UnitCatalog.Sora, back1: UnitCatalog.Tome,
                             back3: new UnitDef
                             {
                                 Id = "kari_plain", Name = "素体のカリ", MaxHp = UnitCatalog.Kari.MaxHp,
                                 Attack = UnitCatalog.Kari.Attack, Speed = UnitCatalog.Kari.Speed,
                                 Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Kari.Pattern
                             }))
        };
        Console.WriteLine("| 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 発火 | 空振り | 列越え | 消費 | 止めた砲火 | 対象 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach ((string name, Formation f) in cross)
        {
            var (wins, z) = FnAll(f, new FinisherRule(FnMain));
            Console.WriteLine($"| {name} |{FnCells(wins)} {wins.Average():0.0}% "
                + $"| {z.Fires:0.00} | {z.Idle:0.00} | {z.Cross:0.00} | {z.Consumed:0.00} | {z.Starved:0.00} "
                + $"| {FnTop(z.TargetTo, 2)} |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        if (fnMode == "cross") return;
    }

    // --- 6. 席の分散 --------------------------------------------------------------------------
    if (fnMode.Length == 0 || fnMode == "seats")
    {
        Console.WriteLine("## 6. 席の分散（`seats2` の写し・受け入れ基準11）");
        Console.WriteLine();
        Console.WriteLine("粗探索 seed 0..49 の全 120 通り → 上位20 + 現行 + 最下位 を seed 0..199 で測り直し。");
        Console.WriteLine("**トメは隣接も自分の列も読まない**（対象は敵陣の標持ちだけ）ので、");
        Console.WriteLine("第47期のウロと同じ側——**席を決めるのは列（前列に置くと早く落ちて振れない）**のはず。");
        Console.WriteLine("比較先は 第45期の 隣接を読む駒 85% / 隣接も列も読まない駒 65% / ヒサ 62%。");
        Console.WriteLine("**列で決まる駒には 2値（前列 / それ以外）も併記する**（第50期の残件）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 駒 | 最適席 | 次数 | 上位5の席（3値） | 最頻率 | 2値(前列/以外) | 幅 | 現行の順位 | 1位との差 |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|--:|--:|--:|");
        foreach (var b in fnTargets)
        {
            var members = b.F.Occupied().Select(x => x.Def).ToList();
            var perms = new List<Formation>();
            foreach (int[] assign in SlotAssignments(members.Count))
            {
                var g = new Formation();
                for (int m = 0; m < members.Count; m++) g[assign[m]] = members[m];
                perms.Add(g);
            }
            var scan = new int[perms.Count];
            for (int i = 0; i < perms.Count; i++)
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in fnStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            }
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));
            var pool = order.Take(20).Append(curIdx).Append(order[^1]).Distinct().ToList();

            double Avg(Formation f)
            {
                double avg = 0;
                foreach (EnemyCatalog.Stage st in fnStages)
                {
                    int wins = 0;
                    for (int seed = 0; seed < FnSeeds; seed++)
                        if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
                    avg += wins * 100.0 / FnSeeds;
                }
                return avg / fnStages.Count;
            }

            var verified = pool.Select(i => (Idx: i, Avg: Avg(perms[i]))).OrderByDescending(x => x.Avg).ToList();
            double width = verified[0].Avg - verified[^1].Avg;
            var top5 = verified.Take(5).ToList();
            int curRank = verified.FindIndex(x => x.Idx == curIdx) + 1;
            double curGap = verified[0].Avg - verified.First(x => x.Idx == curIdx).Avg;

            foreach (UnitDef d in members)
            {
                int bestSlot = -1;
                foreach ((int slot, UnitDef dd) in perms[verified[0].Idx].Occupied())
                    if (ReferenceEquals(dd, d)) bestSlot = slot;
                int mid = 0, fcorner = 0, bcorner = 0, front = 0;
                foreach (var v in top5)
                    foreach ((int slot, UnitDef dd) in perms[v.Idx].Occupied())
                        if (ReferenceEquals(dd, d))
                        {
                            int deg2 = 0;
                            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                                if (FormationRules.AreAdjacent(slot, i)) deg2++;
                            if (deg2 == 4) mid++;
                            else if (FormationRules.RowOf(slot) == Row.Front) fcorner++;
                            else bcorner++;
                            if (FormationRules.RowOf(slot) == Row.Front) front++;
                        }
                int bdeg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(bestSlot, i)) bdeg++;
                int top = Math.Max(mid, Math.Max(fcorner, bcorner));
                Console.WriteLine($"| {b.Name} | {d.Name} | {FormationRules.SeatNames[bestSlot]} | {bdeg} "
                    + $"| 前角{fcorner} / 中央{mid} / 後角{bcorner} | {top * 100 / 5}% "
                    + $"| {front} / {5 - front} | {width:0.0}pt "
                    + $"| {curRank} / {verified.Count} | {curGap:0.0}pt |");
            }
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine("### トメ1枚だけを振った5変種（他の4枚は元の相対順のまま詰める）");
        Console.WriteLine();
        Console.WriteLine("**トメは隣接を1つも読まない**ので、次数は関係ないはず。");
        Console.WriteLine("**効くのは列**（前列に置くと早く落ちて振れない＝第47期のウロと同じ）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | トメの席 | 次数 | 列 | 発火 | 空振り | 列越え | 消費 | トメ寿命 | 平均勝率 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in fnTargets)
            for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
            {
                Formation g = FnSeat(b.F, seat);
                int deg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(seat, i)) deg++;
                var (wins, z) = FnAll(g, new FinisherRule(FnMain));
                Console.WriteLine($"| {b.Name} | {FormationRules.SeatNames[seat]} | {deg} | {FormationRules.RowOf(seat)} "
                    + $"| {z.Fires:0.00} | {z.Idle:0.00} | {z.Cross:0.00} | {z.Consumed:0.00} "
                    + $"| {FnGet(z.Last, "tome"):0.00} | {wins.Average():0.0}% |");
                Console.Out.Flush();
            }
        Console.WriteLine();
        if (fnMode == "seats") return;
    }

    // --- 7. 別 seed の追試 --------------------------------------------------------------------
    if (fnMode == "alt")
    {
        const int AltFrom = 200, AltTo = 600;
        Console.WriteLine("## 7. 別 seed の追試（seed 200..599・機構の帰属）");
        Console.WriteLine();
        double[] Cells(Formation f, FinisherRule? rule)
        {
            var v = new double[fnStages.Count];
            for (int w = 0; w < fnStages.Count; w++)
            {
                int wins = 0;
                for (int seed = AltFrom; seed < AltTo; seed++)
                    if (BattleEngine.Run(f, fnStages[w].Enemy, seed, false, finisher: rule).PlayerWon) wins++;
                v[w] = wins * 100.0 / (AltTo - AltFrom);
            }
            return v;
        }
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in fnTargets)
        {
            var t = Cells(b.F, new FinisherRule(FnMain));
            var nc = Cells(b.F, new FinisherRule(FnMain, false));
            var ns = Cells(FnNoSupply(b.F), new FinisherRule(FnMain));
            var pl = Cells(FnPlain(b.F), null);
            Console.WriteLine($"| {b.Name} | M{FnMain} |{FnCells(t)} {t.Average():0.0}% |");
            Console.WriteLine($"| | 消費なし |{FnCells(nc)} {nc.Average():0.0}% |");
            Console.WriteLine($"| | 供給なし |{FnCells(ns)} {ns.Average():0.0}% |");
            Console.WriteLine($"| | 素体 |{FnCells(pl)} {pl.Average():0.0}% |");
            Console.WriteLine($"| | **帰属（追試）** | "
                + string.Concat(Enumerable.Range(0, t.Length).Select(i => $"{t[i] - pl[i]:+0.0;-0.0;0.0} |"))
                + $" **{t.Average() - pl.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | **代金（追試）** | "
                + string.Concat(Enumerable.Range(0, t.Length).Select(i => $"{t[i] - nc[i]:+0.0;-0.0;0.0} |"))
                + $" **{t.Average() - nc.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        return;
    }

    // --- 8. 配置の追試 ------------------------------------------------------------------------
    if (fnMode == "confirm")
    {
        const int CfFrom = 200, CfTo = 600;
        Console.WriteLine("## 8. 配置の追試（`confirm`・seed 200..599）");
        Console.WriteLine();
        Console.WriteLine("**選定に使っていない seed 帯**で測り直す。採否閾値は **5.0pt**（第46期）。");
        Console.WriteLine("**1位の配置ではなく次数で読む**（第45期の残件 D）。");
        Console.WriteLine("**`発火` と `列越え` と `止めた砲火` の列を必ず見る**——");
        Console.WriteLine("**「機構を 0 にする席」と「代金を 0 にする席」は別物**（第52期）。");
        Console.WriteLine("発火が 0 の席は機構が死んでいるので採らないが、");
        Console.WriteLine("**発火したまま代金だけが消える席は正当な配置解。**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 配置 | トメの席 | 次数 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 現行との差 | 発火 | 空振り | 列越え | 止めた砲火 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in fnTargets)
        {
            var members = b.F.Occupied().Select(x => x.Def).ToList();
            var perms = new List<Formation>();
            foreach (int[] assign in SlotAssignments(members.Count))
            {
                var g = new Formation();
                for (int m = 0; m < members.Count; m++) g[assign[m]] = members[m];
                perms.Add(g);
            }
            var scan = new int[perms.Count];
            for (int i = 0; i < perms.Count; i++)
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in fnStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            }
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));

            (double[] Cells, double Fires, double Idle, double Cross, double Starved) Measure(Formation f)
            {
                var v = new double[fnStages.Count];
                double fires = 0, idle = 0, cross = 0, starved = 0; int n = 0;
                for (int w = 0; w < fnStages.Count; w++)
                {
                    int wins = 0;
                    for (int seed = CfFrom; seed < CfTo; seed++)
                    {
                        var r = BattleEngine.Run(f, fnStages[w].Enemy, seed, false);
                        if (r.PlayerWon) wins++;
                        fires += r.FinisherFires; idle += r.FinisherIdle;
                        cross += r.FinisherCross; starved += r.FinisherStarved; n++;
                    }
                    v[w] = wins * 100.0 / (CfTo - CfFrom);
                }
                return (v, fires / n, idle / n, cross / n, starved / n);
            }
            int Seat(Formation f)
            {
                foreach ((int slot, UnitDef d) in f.Occupied())
                    if (ReferenceEquals(d, UnitCatalog.Tome)) return slot;
                return -1;
            }
            int Deg(int slot)
            {
                int n = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(slot, i)) n++;
                return n;
            }

            var cur = Measure(b.F);
            Console.WriteLine($"| {b.Name} | **現行** | {FormationRules.SeatNames[Seat(b.F)]} | {Deg(Seat(b.F))} "
                + $"|{FnCells(cur.Cells)} **{cur.Cells.Average():0.0}%** | — | {cur.Fires:0.00} | {cur.Idle:0.00} "
                + $"| {cur.Cross:0.00} | {cur.Starved:0.00} |");
            Console.Out.Flush();
            foreach (int idx in order.Take(5))
            {
                if (idx == curIdx) continue;
                var v = Measure(perms[idx]);
                int seat = Seat(perms[idx]);
                Console.WriteLine($"| {b.Name} | 粗探索 {order.IndexOf(idx) + 1}位 | {FormationRules.SeatNames[seat]} | {Deg(seat)} "
                    + $"|{FnCells(v.Cells)} {v.Cells.Average():0.0}% "
                    + $"| **{v.Cells.Average() - cur.Cells.Average():+0.0;-0.0;0.0}pt** | {v.Fires:0.00} | {v.Idle:0.00} "
                    + $"| {v.Cross:0.00} | {v.Starved:0.00} |");
                Console.WriteLine($"|   ↳ 配置 | {string.Join(" / ", perms[idx].Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]}:{o.Def.Name}"))} | | | | | | | | | | | | | |");
                Console.Out.Flush();
            }
            Console.WriteLine($"|   ↳ 現行の順位 | **粗探索 {order.IndexOf(curIdx) + 1}位 / {perms.Count}通り** | | | | | | | | | | | | | |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        return;
    }

    return;
}
}
