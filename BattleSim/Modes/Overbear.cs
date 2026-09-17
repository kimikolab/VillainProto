using BattleCore;
using static Common;

// =====================================================================================
// overbear モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "overbear")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 overbear
// =====================================================================================

static class OverbearDiag
{
// overbear モード: 驕り（第46期）。**隣接を「量」ではなく「誰がいるか」で読む駒**を測る。
//
// 第45期の結論は「隣接を単調な量（隣に何人いるか）で読む駒は席が定数になる」で、
// 反例はロスターに1枚しかない非単調な読み手（囃し立てのヒサ）だけだった（n=1）。
// オゴは条件を隣接する味方**全員**への AND にするので、
//
//     隣接数が増えるほど条件が厳しくなる（中央は削る量が多い代わりに成立が遅い）
//     さらに「隣が誰か」で成立時刻が変わる（ドルガ38 の隣なら13ターン、ムグ6 の隣なら即座）
//
// **この期の核心は「成立時刻」**で、席（隣接数）と相方（隣の攻撃力）の両方で変わるはず。
// 変わらなければ非単調にした意味が無い。
//
// **陽性対照を2本置く**: OverbearRule(0)（削らない＝条件が永遠に成立しない＝プラス側だけを切る）と、
// オゴを外した4体版（＝第21期の飽和検査も兼ねる）。
// **発火しなかったことは盤面の値に痕跡を残さない**ので、計数は BattleResult の
// Overbear*（verbose に依存しない）から取る。
//
//     dotnet run --project BattleSim -c Release 0 overbear [絞り込み]
public static void Run(string[] args, int stageIndex)
{
    var obBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> obStages = EnemyCatalog.Stages;
    const int ObSeeds = 200;   // compare / spread / shove / dull / relay / slander と揃える
    const int ObMain = 2;      // 主表に使う Drain（採用値）
    int[] obDrains = { 0, 1, 2, 3 };
    int obRouteIdx = (int)DullRoute.Overbear;

    string obFilter = args.Length > 2 ? args[2] : "";

    static bool ObHasOgo(Formation f) => f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Ogo));

    // **測定した2編成は診断のローカルに持つ**（gradient / aim / guard / slander と同じ扱い）。
    // 第46期は驕りを**採用しなかった**ので `CompareBuilds()` には残していない
    // ——測ったときは 50 行に足して回し、棄却と同時に外した。ここに残しておけば、
    // `CompareBuilds()` を1行も動かさずに全部を測り直せる。
    //
    // 2行は「条件が早く成立する台」と「遅く成立する台」の対。律速は隣でいちばん攻撃力の高い1枚。
    //
    //   早い台 = 死の連鎖行の ゴルム(10)→ウケ・ヴェル(6)→オゴ。開幕の隣の最高は 6 で**どの席でも成立する**
    //   遅い台 = 分かち×逆しま行の ノノ(3) → オゴ。**ドルガ 38 と ウツ（削るほど育つ）が隣にいると成立しない**
    //
    // **早い台は「攻撃力の低い駒だけ」では組めなかった**（測った5通りが全部 20.0〜23.2% ＝
    // CLAUDE.md の「台が死んでいる」）——ロスターでは弱体・毒・支援の駒が軒並み攻2〜9 で、
    // 攻撃力と出力が強く相関している。抜け道は**攻撃力を経由しない出力**で、
    // 死の連鎖（破裂・胞子・墓守）がそれに当たる。
    static (string Name, Formation F)[] ObRows() => new (string, Formation)[]
    {
        ("驕り (オゴ×ウケ)", Formation.Build(front1: UnitCatalog.Zoto, front3: UnitCatalog.Mug,
                                        center: UnitCatalog.Uke, back1: UnitCatalog.Rica,
                                        back3: UnitCatalog.Ogo)),
        ("驕り改 (オゴ×ウツ)", Formation.Build(front1: UnitCatalog.Utsu, front3: UnitCatalog.Gald,
                                          center: UnitCatalog.Ogo, back1: UnitCatalog.Doha,
                                          back3: UnitCatalog.Dolga)),
    };

    var obTargets = ObRows();
    if (obFilter.Length > 0)
        obTargets = obTargets.Where(b => obFilter.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();

    // オゴを外した同じ編成（4体版）。**第21期の飽和検査を兼ねる**
    // ——4体版と5体版が同じ値なら、その台では中央の駒が何であっても結果が変わらない。
    static Formation ObWithoutOgo(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (!ReferenceEquals(d, UnitCatalog.Ogo)) g[slot] = d;
        return g;
    }

    // オゴの席だけを振った5変種。**他の4枚は元の相対順のまま**空いた席へ詰める
    // （reseat の120通りと違い、動かすのはオゴ1枚だけ＝席の効果を1変数に閉じる）。
    static Formation ObSeat(Formation f, int seat)
    {
        var others = f.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Ogo))
                      .Select(o => o.Def).ToList();
        var g = new Formation();
        g[seat] = UnitCatalog.Ogo;
        int k = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount && k < others.Count; i++)
            if (i != seat) g[i] = others[k++];
        return g;
    }

    (double Win, double Fired, double Total, double Taken, double Passed, double Zeroed,
     double MetRate, double First, double Never, double Doubled, double Swings,
     double Back, double BackHits, double Turns,
     Dictionary<string, double> To, Dictionary<string, double> Zero)
    MeasureOverbear(Formation f, Formation enemy, OverbearRule rule, int seed0 = 0, int seedN = ObSeeds)
    {
        var to = new Dictionary<string, double>();
        var zero = new Dictionary<string, double>();
        double win = 0, fired = 0, total = 0, taken = 0, passed = 0, zeroed = 0;
        double met = 0, obTurns = 0, first = 0, firstN = 0, never = 0;
        double doubled = 0, swings = 0, back = 0, backHits = 0, turns = 0;

        for (int seed = seed0; seed < seed0 + seedN; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false,
                                    null, null, null, null, null, null, null, null, null, rule);
            if (r.PlayerWon) win++;
            fired += r.OverbearFired; total += r.OverbearTotal;
            taken += r.DullTakenByRoute[obRouteIdx];
            passed += r.DullByRoute[obRouteIdx] - r.DullTakenByRoute[obRouteIdx];
            zeroed += r.DullZeroed;
            met += r.OverbearMetTurns; obTurns += r.OverbearTurns;
            if (r.OverbearFirstTurn > 0) { first += r.OverbearFirstTurn; firstN++; } else never++;
            doubled += r.OverbearDoubled; swings += r.OverbearSwings;
            back += r.OverbearBackfire; backHits += r.OverbearBackfireHits;
            turns += r.Turns;

            foreach (var kv in r.OverbearTo)
                to[kv.Key] = to.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            foreach (var kv in r.DullZeroedWho)
                zero[kv.Key] = zero.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
        }

        double n = seedN;
        foreach (string k in to.Keys.ToList()) to[k] /= n;
        foreach (string k in zero.Keys.ToList()) zero[k] /= n;
        return (win * 100 / n, fired / n, total / n, taken / n, passed / n, zeroed / n,
                obTurns > 0 ? met * 100 / obTurns : 0, firstN > 0 ? first / firstN : 0, never * 100 / n,
                doubled / n, swings / n, back / n, backHits / n, turns / n, to, zero);
    }

    // 全波を通した集計（機構の量は波で平均する。勝率だけは波ごとに出す）
    (double[] Wins, double Fired, double Total, double Taken, double Passed, double Zeroed,
     double MetRate, double First, double Never, double Doubled, double Swings,
     double Back, double BackHits, Dictionary<string, double> To, Dictionary<string, double> Zero)
    MeasureAll(Formation f, OverbearRule rule)
    {
        var wins = new double[obStages.Count];
        var to = new Dictionary<string, double>();
        var zero = new Dictionary<string, double>();
        double fired = 0, total = 0, taken = 0, passed = 0, zeroed = 0, met = 0, first = 0, firstN = 0;
        double never = 0, doubled = 0, swings = 0, back = 0, backHits = 0;
        for (int w = 0; w < obStages.Count; w++)
        {
            var z = MeasureOverbear(f, obStages[w].Enemy, rule);
            wins[w] = z.Win;
            fired += z.Fired; total += z.Total; taken += z.Taken; passed += z.Passed; zeroed += z.Zeroed;
            met += z.MetRate; never += z.Never; doubled += z.Doubled; swings += z.Swings;
            back += z.Back; backHits += z.BackHits;
            if (z.First > 0) { first += z.First; firstN++; }
            foreach (var kv in z.To) to[kv.Key] = to.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
            foreach (var kv in z.Zero) zero[kv.Key] = zero.TryGetValue(kv.Key, out double c) ? c + kv.Value : kv.Value;
        }
        double m = obStages.Count;
        foreach (string k in to.Keys.ToList()) to[k] /= m;
        foreach (string k in zero.Keys.ToList()) zero[k] /= m;
        return (wins, fired / m, total / m, taken / m, passed / m, zeroed / m, met / m,
                firstN > 0 ? first / firstN : 0, never / m, doubled / m, swings / m,
                back / m, backHits / m, to, zero);
    }

    static string ObCells(double[] w) => string.Concat(w.Select(x => $" {x:0.0}% |"));

    Console.WriteLine("# 驕り（overbear）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 overbear [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{ObSeeds - 1}。数字は特記なければ**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`Stages` / `Columns` は触っていない。`CompareBuilds()` には**2行足した**（第45期の残件 C）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 削り | 撒いた総量/戦（`OverbearTotal`）と延べ体数（`OverbearFired`） |");
    Console.WriteLine("| 成立率 | 条件を満たしていたターン数 ÷ 保持者がターン頭を迎えた回数（`MetTurns/Turns`） |");
    Console.WriteLine("| 成立T | **条件が初めて成立したターン**（成立した試行だけの平均）。`未成立` が成立しなかった試行の割合 |");
    Console.WriteLine("| 2倍 | 2倍が乗った攻撃回数/戦（`OverbearDoubled`）と振った回数（`OverbearSwings`） |");
    Console.WriteLine("| 横取り | ウケ／ワタが横取りした量/戦（`DullTakenByRoute[Overbear]`）＝**削りが資産に変わった量** |");
    Console.WriteLine("| 素通り | 読み手に届かずただの損になった量/戦 |");
    Console.WriteLine("| 逆行 | **削ったのに相手の `CurrentAttack` が上がった量**/戦（逆しまの自己矛盾） |");
    Console.WriteLine("| 攻ゼロ | 味方の `CurrentAttack` が 0 になった回数/戦（**崖の検算**・全経路） |");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準1・2）--------------------------------------------------------
    Console.WriteLine("## 0. 検算 —— 差分は驕りだけに閉じているか（受け入れ基準1・2）");
    Console.WriteLine();
    {
        var plain = obBuilds.Where(b => !ObHasOgo(b.F)).ToArray();   // 現状は 48 行すべて
        int cells = 0, diff = 0;
        foreach (var b in plain)
            for (int w = 0; w < obStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < ObSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, obStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, new OverbearRule(0)).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, obStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, new OverbearRule(9)).PlayerWon) c++;
                }
                cells++;
                if (a != c) diff++;
            }
        Console.WriteLine($"- **基準2**（オゴを含まない {plain.Length} 行が `OverbearRule` の値に対して不変）: "
            + $"**{cells} セル中 {diff} 件の食い違い**（{plain.Length} 行 × {obStages.Count} 波・`Drain` 0 対 9）");
        Console.WriteLine("- **基準1**（新駒を編成に入れない状態で `compare` が `docs/balance.md` と完全一致）は");
        Console.WriteLine("  行を足す前に `compare` の全文 diff で確認する（**240 セル中 0 件**）。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 1. 主表 ----------------------------------------------------------------------------
    Console.WriteLine($"## 1. 主表（`Drain = {ObMain}` と 陽性対照2本）");
    Console.WriteLine();
    Console.WriteLine("`D0` = `OverbearRule(0)`（削らない＝条件が永遠に成立しない＝**プラス側だけを切る**）。");
    Console.WriteLine("`4体` = オゴを外した同じ編成（**第21期の飽和検査**も兼ねる。4体版と5体版が同値なら台が測定になっていない）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
    Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
    var obMain = new Dictionary<string, (double[] Wins, double Fired, double Total, double Taken,
        double Passed, double Zeroed, double MetRate, double First, double Never, double Doubled,
        double Swings, double Back, double BackHits, Dictionary<string, double> To,
        Dictionary<string, double> Zero)>();
    var obZero = new Dictionary<string, (double[] Wins, double Fired, double Total, double Taken,
        double Passed, double Zeroed, double MetRate, double First, double Never, double Doubled,
        double Swings, double Back, double BackHits, Dictionary<string, double> To,
        Dictionary<string, double> Zero)>();

    foreach (var b in obTargets)
    {
        var z = MeasureAll(b.F, new OverbearRule(ObMain));
        var z0 = MeasureAll(b.F, new OverbearRule(0));
        obMain[b.Name] = z;
        obZero[b.Name] = z0;
        var four = new double[obStages.Count];
        for (int w = 0; w < obStages.Count; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < ObSeeds; seed++)
                if (BattleEngine.Run(ObWithoutOgo(b.F), obStages[w].Enemy, seed, false).PlayerWon) wins++;
            four[w] = wins * 100.0 / ObSeeds;
        }
        Console.WriteLine($"| {b.Name} | **D{ObMain}** |{ObCells(z.Wins)} {z.Wins.Average():0.0}% |");
        Console.WriteLine($"| | D0 |{ObCells(z0.Wins)} {z0.Wins.Average():0.0}% |");
        Console.WriteLine($"| | 4体 |{ObCells(four)} {four.Average():0.0}% |");
        Console.Out.Flush();
    }
    Console.WriteLine();

    Console.WriteLine($"### 機構の計数（`Drain` = {ObMain}・5波の平均）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 削り(量/体) | 成立率 | 成立T | 未成立 | 2倍/振り | 横取り | 素通り | 横取り率 | 逆行(量/回) | 攻ゼロ |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var b in obTargets)
    {
        var z = obMain[b.Name];
        double got = z.Taken + z.Passed;
        Console.WriteLine($"| {b.Name} | {z.Total:0.0} / {z.Fired:0.0} | {z.MetRate:0.0}% "
            + $"| {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% "
            + $"| {z.Doubled:0.00} / {z.Swings:0.00} | {z.Taken:0.00} | {z.Passed:0.00} "
            + $"| {(got > 0 ? $"{z.Taken * 100 / got:0.0}%" : "—")} "
            + $"| {z.Back:0.0} / {z.BackHits:0.00} | {z.Zeroed:0.00} |");
    }
    Console.WriteLine();

    // --- 2. 削った相手と攻ゼロの内訳 --------------------------------------------------------
    Console.WriteLine($"## 2. 誰を削ったか / 誰が攻ゼロになったか（`Drain = {ObMain}`）");
    Console.WriteLine();
    foreach (var b in obTargets)
    {
        var z = obMain[b.Name];
        var z0 = obZero[b.Name];
        Console.WriteLine($"### {b.Name}");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 素の攻 | 削られた量/戦 |");
        Console.WriteLine("|---|--:|--:|");
        foreach (var kv in z.To.OrderByDescending(k => k.Value))
        {
            var def = UnitCatalog.All.FirstOrDefault(u => u.Name == kv.Key);
            Console.WriteLine($"| {kv.Key} | {(def is null ? "—" : def.Attack.ToString())} | {kv.Value:0.00} |");
        }
        Console.WriteLine();
        Console.WriteLine("**攻ゼロの内訳（崖の検算・受け入れ基準4）。** 帰属は D0 との差で取る");
        Console.WriteLine("——`攻ゼロ` は窓口を通る全経路を数えるので、なまり・呪詛・萎縮のぶんが D0 側に載っている。");
        Console.WriteLine();
        Console.WriteLine($"| 駒 | 素の攻 | D0 | D{ObMain} | 差（＝驕りに帰属） |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        if (z.Zero.Count == 0 && z0.Zero.Count == 0) Console.WriteLine("| （0 件） | — | 0.00 | 0.00 | ±0.00 |");
        foreach (var kv in z.Zero.OrderByDescending(k => k.Value))
        {
            var def = UnitCatalog.All.FirstOrDefault(u => u.Name == kv.Key);
            double b0 = z0.Zero.TryGetValue(kv.Key, out double q) ? q : 0;
            Console.WriteLine($"| {kv.Key} | {(def is null ? "—" : def.Attack.ToString())} | {b0:0.00} "
                + $"| {kv.Value:0.00} | {kv.Value - b0:+0.00;-0.00;±0.00} |");
        }
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 3. 成立時刻 × 席（**この期の核心**）------------------------------------------------
    Console.WriteLine("## 3. 成立時刻 × 席（**この期の核心**）");
    Console.WriteLine();
    Console.WriteLine("**オゴ1枚だけを動かす。** 他の4枚は元の相対順のまま空いた席へ詰める");
    Console.WriteLine("（`reseat` の120通りと違い、動かすのは1枚だけ＝席の効果を1変数に閉じる）。");
    Console.WriteLine();
    Console.WriteLine("**席と相方の両方で成立時刻が動かなければ、非単調にした意味が無い。**");
    Console.WriteLine();
    Console.WriteLine("`勝率(D0)` は同じ席配置で `OverbearRule(0)`（削らない＝条件が成立しない）を回した対照。");
    Console.WriteLine("**オゴを動かすと他の4枚の席も動く**ので、対照を置かないと席の差が機構の差かどうか決まらない。");
    Console.WriteLine();
    foreach (var b in obTargets)
    {
        Console.WriteLine($"### {b.Name}");
        Console.WriteLine();
        Console.WriteLine("| オゴの席 | 次数 | 隣接する駒（開幕） | 成立率 | 成立T | 未成立 | 2倍/振り | 削り | 横取り | 逆行 | 勝率(D0) | 勝率(D2) | 差 |");
        Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
        {
            Formation g = ObSeat(b.F, seat);
            int deg = 0;
            var names = new List<string>();
            foreach ((int slot, UnitDef d) in g.Occupied())
                if (slot != seat && FormationRules.AreAdjacent(seat, slot)) { deg++; names.Add($"{d.Name}({d.Attack})"); }
            var z = MeasureAll(g, new OverbearRule(ObMain));
            var zc = MeasureAll(g, new OverbearRule(0));   // 席の効果から機構の効果を割る対照
            Console.WriteLine($"| {FormationRules.SeatNames[seat]} | {deg} | {string.Join("・", names)} "
                + $"| {z.MetRate:0.0}% | {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% "
                + $"| {z.Doubled:0.00} / {z.Swings:0.00} | {z.Total:0.0} | {z.Taken:0.00} | {z.Back:0.0} "
                + $"| {zc.Wins.Average():0.0}% | {z.Wins.Average():0.0}% "
                + $"| {z.Wins.Average() - zc.Wins.Average():+0.0;-0.0;±0.0} |");
            Console.Out.Flush();
        }
        Console.WriteLine();
    }

    // --- 4. 掃引 ----------------------------------------------------------------------------
    Console.WriteLine("## 4. 掃引（`Drain` 0 / 1 / 2 / 3）");
    Console.WriteLine();
    Console.WriteLine("**見るのは勝率ではなく「成立時刻」と「席」。** `Drain` は成立時刻を直接動かすノブなので、");
    Console.WriteLine("席の選び方も動くはず。動かなければ非単調化が効いていない証拠になる。");
    Console.WriteLine();
    Console.WriteLine("`最適席` は §3 の5変種のうち平均勝率が最大の席（**1位の値ではなく次数を読むこと**・第45期の残件 D）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | Drain | 平均勝率 | 成立率 | 成立T | 未成立 | 2倍/振り | 削り | 横取り | 素通り | 逆行 | 攻ゼロ | 最適席(次数) |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
    foreach (var b in obTargets)
    {
        foreach (int d in obDrains)
        {
            var z = MeasureAll(b.F, new OverbearRule(d));
            int best = -1; double bestAvg = double.MinValue;
            for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
            {
                var q = MeasureAll(ObSeat(b.F, seat), new OverbearRule(d));
                double avg = q.Wins.Average();
                if (avg > bestAvg) { bestAvg = avg; best = seat; }
            }
            int deg = 0;
            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                if (FormationRules.AreAdjacent(best, i)) deg++;
            Console.WriteLine($"| {b.Name} | {d} | {z.Wins.Average():0.0}% | {z.MetRate:0.0}% "
                + $"| {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% | {z.Doubled:0.00} / {z.Swings:0.00} "
                + $"| {z.Total:0.0} | {z.Taken:0.00} | {z.Passed:0.00} | {z.Back:0.0} | {z.Zeroed:0.00} "
                + $"| {FormationRules.SeatNames[best]}({deg}) {bestAvg:0.0}% |");
            Console.Out.Flush();
        }
    }
    Console.WriteLine();

    // --- 5. 席の分散（seats2 の写し・**受け入れ基準6**）--------------------------------------
    // `seats2` は `CompareBuilds()` の行しか見ないので、2行を外した後は同じ数字を出せない。
    // **探索と検証の作り方は `seats2` の写し**（粗探索 seed 0..49 の全120通り →
    // 上位20 + 現行 を seed 0..199 で測り直し）で、ここでローカルの2編成に対して回す。
    Console.WriteLine("## 5. 席の分散（`seats2` の写し・**受け入れ基準6・この期の主眼**）");
    Console.WriteLine();
    Console.WriteLine("粗探索 seed 0..49 の全 120 通り → 上位20 + 現行 を seed 0..199 で測り直し。");
    Console.WriteLine("**採否に使うのは1位の配置ではなく次数**（第45期の残件 D。1位は別 seed 帯で 48行中28行で");
    Console.WriteLine("入れ替わるが、次数の追試一致率は 98%）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 駒 | 最適席 | 次数 | 上位5の席（中央/角） | 幅 |");
    Console.WriteLine("|---|---|---|--:|---|--:|");
    foreach (var b in obTargets)
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
            foreach (EnemyCatalog.Stage st in obStages)
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
            foreach (EnemyCatalog.Stage st in obStages)
            {
                int wins = 0;
                for (int seed = 0; seed < ObSeeds; seed++)
                    if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
                avg += wins * 100.0 / ObSeeds;
            }
            return avg / obStages.Count;
        }

        var verified = pool.Select(i => (Idx: i, Avg: Avg(perms[i]))).OrderByDescending(x => x.Avg).ToList();
        double width = verified[0].Avg - verified[^1].Avg;
        var top5 = verified.Take(5).ToList();

        foreach (UnitDef d in members)
        {
            int bestSlot = -1;
            foreach ((int slot, UnitDef dd) in perms[verified[0].Idx].Occupied())
                if (ReferenceEquals(dd, d)) bestSlot = slot;
            int mid = 0, corner = 0;
            foreach (var v in top5)
                foreach ((int slot, UnitDef dd) in perms[v.Idx].Occupied())
                    if (ReferenceEquals(dd, d))
                    {
                        int deg2 = 0;
                        for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                            if (FormationRules.AreAdjacent(slot, i)) deg2++;
                        if (deg2 == 4) mid++; else corner++;
                    }
            int bdeg = 0;
            for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                if (FormationRules.AreAdjacent(bestSlot, i)) bdeg++;
            Console.WriteLine($"| {b.Name} | {d.Name} | {FormationRules.SeatNames[bestSlot]} | {bdeg} "
                + $"| 中央{mid} / 角{corner} | {width:0.0}pt |");
        }
        Console.Out.Flush();
    }
    Console.WriteLine();
    return;
}
}
