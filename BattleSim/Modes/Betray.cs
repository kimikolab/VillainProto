using BattleCore;
using static Common;

// =====================================================================================
// betray モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "betray")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 betray
// =====================================================================================

static class BetrayDiag
{
// betray モード（第103期）—— 背かれのソム（餌を敵陣に置く）
//
// 毎ターン敵陣の ○前2 に「背いた獣」を喚び出す。**読み手を1枚も作らない**——
// 敵の死を読む駒（追い打ちのハギ・疫みのラウ・抉りのエグ）は既に盤面にある。
//
//     dotnet run --project BattleSim -c Release 0 betray phase0            # 表A（事実・門・紙・予測）
//     dotnet run --project BattleSim -c Release 0 betray run <skip> <take> # 2×2（A = ソム。V0/V1 を両方吐く）
//     dotnet run --project BattleSim -c Release 0 betray tables <TSV...>   # 表B・C（主判定・副判定）
//     dotnet run --project BattleSim -c Release 0 betray engage            # 表D（会戦 Q4・代金 Q5）
//     dotnet run --project BattleSim -c Release 0 betray check             # 表E・F（拒否権・自己検査）
//
// **既存の診断は1文字も書き換えていない。**
public static void Run(string[] args, int stageIndex)
{
    string btMode = args.Length > 2 ? args[2] : "phase0";
    var btStages = EnemyCatalog.Stages;
    var btCompare = CompareBuilds();
    var btCross = CrossBuilds();
    var btRoster = UnitCatalog.All.ToArray();
    int btRN = btRoster.Length;
    var btIdx = new Dictionary<string, int>();
    for (int u = 0; u < btRN; u++) btIdx[btRoster[u].Id] = u;
    const double BtEps = 1e-9;
    var btInv = System.Globalization.CultureInfo.InvariantCulture;

    string? btRoot = Directory.GetCurrentDirectory();
    while (btRoot != null && !File.Exists(Path.Combine(btRoot, "docs", "balance.md")))
        btRoot = Path.GetDirectoryName(btRoot);

    const string BtAId = "som";

    // 版。**V0 = 現行（喚ばない）／ V1 = 背かれ（毎ターン）／ V2 = 背かれ（死体が席を塞ぐ）。**
    //
    // **V2 は §1-3 の擬似コードをそのまま写した版**——`Summon` の空き判定が生死を問わないので、
    // 餌の死体が ○前2 を永久に塞いで **1戦に1体しか湧かない**（Phase 0 の門で実測した）。
    // それでは `PlusText` の「毎ターン」が嘘になるので **V1 を機構として測り、V2 は対照に置く**。
    BetrayRule BtVer(int v) => v switch
    {
        0 => new BetrayRule(false, true),
        1 => new BetrayRule(true, true),
        _ => new BetrayRule(true, false)
    };
    string BtVName(int v) => v == 0 ? "V0（現行・喚ばない）"
                           : v == 1 ? "V1（背かれ・毎ターン）"
                                    : "V2（背かれ・死体が席を塞ぐ＝1戦に1体）";
    BattleResult BtRun(Formation f, Formation e, int seed, int v, bool verbose = false)
        => BattleEngine.Run(f, e, seed, verbose: verbose, betray: BtVer(v));

    // 意図した相手（§3-1）。**指示書が名指しで固定している5枚。**
    string[] btIntended = { "hagi", "rau", "egu", "borg", "dolga" };
    // 間接の読み手。**意図した相手に入れない**（入れると Q1-1 が通りやすくなり Q1-2 の分母が減る）。
    string[] btIndirect = { "gan", "ban", "beni", "mio", "guza" };
    // 損の側（可変コスト型の証拠）。単体高倍率と、餌に投資が消える傷の書き手。
    string[] btLoss = { "tome", "nata", "zan", "kiri", "nomi" };

    // ---- 規則配置 H（第81期の写し。HP 上位2枚を前・攻撃力上位2枚を後・残りを中央）----
    int[] BtSeats(UnitDef[] u)
    {
        var all5 = new[] { 0, 1, 2, 3, 4 };
        var front = all5.OrderByDescending(k => u[k].MaxHp)
                        .ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        var rest = all5.Where(k => k != front[0] && k != front[1]).ToArray();
        var back = rest.OrderByDescending(k => u[k].Attack)
                       .ThenBy(k => u[k].Id, StringComparer.Ordinal).Take(2).ToArray();
        int center = rest.Single(k => k != back[0] && k != back[1]);
        var r = new int[5];
        r[front[0]] = 0; r[front[1]] = 1; r[center] = 2; r[back[0]] = 3; r[back[1]] = 4;
        return r;
    }
    Formation BtFormOf(UnitDef[] u)
    {
        int[] seats = BtSeats(u);
        var f = new Formation();
        for (int k = 0; k < 5; k++) f[seats[k]] = u[k];
        return f;
    }

    // ---- 試験行（門と紙のための理想台）。`CompareBuilds()` は1行も触っていない ----
    // **席は規則配置 H で機械的に決める**（手で選ばない）。
    UnitDef Bt(string id) => UnitCatalog.ById(id);
    (string Name, Formation F)[] BtRows() => new[]
    {
        ("ソム×追い打ち (ソム×ハギ)", BtFormOf(new[] { Bt("som"), Bt("hagi"), Bt("gald"), Bt("golm"), Bt("dolga") })),
        ("ソム×疫み (ソム×ラウ×グザ)", BtFormOf(new[] { Bt("som"), Bt("rau"), Bt("guza"), Bt("gald"), Bt("mio") })),
        ("ソム×抉り (ソム×エグ×ガン)", BtFormOf(new[] { Bt("som"), Bt("egu"), Bt("gan"), Bt("ban"), Bt("gald") })),
        ("ソム×薙ぎ (ソム×ボルグ×ドルガ)", BtFormOf(new[] { Bt("som"), Bt("borg"), Bt("dolga"), Bt("gald"), Bt("hota") })),
        ("ソム（読み手なし・対照）", BtFormOf(new[] { Bt("som"), Bt("gald"), Bt("golm"), Bt("lili"), Bt("sekki") })),
    };

    const int BtGateSeeds = 200;

    // =================================================================================
    // 表A —— 事実の確定（§2-1）・門（§2-2）・紙（§2-3）・予測
    // =================================================================================
    if (btMode == "phase0")
    {
        Console.WriteLine("# 第103期 表A —— Phase 0（事実・門・紙）");
        Console.WriteLine();

        Console.WriteLine("## A-1. 事実の確定（**戦闘0回**）");
        Console.WriteLine();

        int callSites = 0, callWithSlot = 0;
        if (btRoot is not null)
            foreach (string ff in Directory.GetFiles(Path.Combine(btRoot, "BattleCore"), "*.cs"))
                foreach (string line in File.ReadAllLines(ff))
                {
                    string tt = line.TrimStart();
                    if (tt.StartsWith("//")) continue;
                    int n = System.Text.RegularExpressions.Regex.Matches(line, @"\.Summon\(").Count;
                    callSites += n;
                    if (n > 0 && line.Contains("FodderSlot")) callWithSlot += n;
                }
        Console.WriteLine("- **(1) `Summon` は陣営を引数に取る。** `Summon(def, teamId, at)` の空き判定は "
                          + "`_units.Where(u => u.TeamId == teamId)` なので、`teamId` に敵を渡せば敵陣の空きを走る。"
                          + "**engine の新しい窓口は作っていない**（席を指定する `at` を足しただけ）。");
        Console.WriteLine($"- **(3) `.Summon(` の呼び出し口は {callSites} 箇所**"
                          + $"（うち席を指定するのは {callWithSlot} ＝背かれ）。"
                          + "**残りは分裂（ムグ）の1本で、`at` を省略するので走査順も挙動も1ビットも変わらない**"
                          + "（自己検査 (h) で 305 セルを突き合わせる）。");

        var enemyDefs = btStages.SelectMany(st => st.Enemy.Occupied().Select(x => x.Def))
            .Concat(EnemyCatalog.Columns.SelectMany(c => c.Squads).SelectMany(f => f.Occupied().Select(x => x.Def)))
            .Distinct().ToArray();
        var enemyTraits = enemyDefs.SelectMany(d => d.Traits).Distinct()
                                   .OrderBy(t => t.ToString(), StringComparer.Ordinal).ToArray();
        bool enemySummons = enemyDefs.Any(d => d.Traits.Contains(TraitId.Splitter));
        Console.WriteLine($"- **(2) 敵側に召喚を持つ駒は {(enemySummons ? "いる" : "0 体")}**"
                          + $"（`Stages` ＋ `Columns` の敵 {enemyDefs.Length} 種を全数。持っている特性は "
                          + $"{string.Join("・", enemyTraits.Select(t => "`" + t + "`"))}）"
                          + " —— **湧き先を取り合う相手はいない。**");

        Trait imm = TraitCatalog.Get(TraitId.Immobile);
        var probeCtx = new BattleContext(0, false);
        var probe = new UnitState
        {
            Def = UnitCatalog.Fodder, TeamId = 0, Slot = 7, Hp = 12, MaxHp = 12,
            Traits = TraitCatalog.Resolve(UnitCatalog.Fodder.Traits)
        };
        Console.WriteLine($"- **(4) 餌の札**: `Immobile.CanAct(Attack)` = "
                          + $"**{imm.CanAct(probeCtx, probe, ActionKind.Attack)}** ／ "
                          + $"`Immobile.SurrendersTurn` = **{imm.SurrendersTurn}** ／ "
                          + $"`Trait.SurrenderedTurn(餌)` = **{Trait.SurrenderedTurn(probeCtx, probe)}**"
                          + " —— **号令・据えは買えない。**"
                          + " `ReviverTrait` は `dead.HasTrait(TraitId.Ephemeral)` で明示的に弾いている"
                          + "（自己検査 (g) で実測する）。"
                          + " 会戦の持ち越しは `current`（投入した駒のリスト）だけを見るので、"
                          + "**湧いた駒は構造的に持ち越さない**（胞子と同じ）。");

        Console.WriteLine($"- **(5) ロスターは {btRN} 枚**（上限 52）。"
                          + "**第80〜83期の派生値（独立の広さ・入口・発火口・キー0 の駒の数）は動く。"
                          + "この期では数え直さない。**");

        Console.WriteLine("- **(6) `TraitKeyMap[Betrayed]` = 空 ／ `TraitHookMap[Betrayed]` = `OnTurnStart` ／ "
                          + "`TraitEntryMap` は `Supplies` も `Reads` も無し。** "
                          + "**撃破は 11 本のキーに1つも無い**ので、背かれは通貨を1つも書かない"
                          + "（`derive scan` の欠落 0 / 過剰 0。ただし `CompareBuilds()` に"
                          + "ソムがいないので、観測はこの表の下の門で取る）。");
        Console.WriteLine();

        Console.WriteLine("## A-2. 門（**鎖が繋がっているか。大きさではない**。規約 (G7)）");
        Console.WriteLine();
        Console.WriteLine($"試験行 5 本 × 第2〜5波 × seed 0..{BtGateSeeds - 1}。**席は規則配置 H で機械的に決めた。**");
        Console.WriteLine();
        Console.WriteLine("**V1（毎ターン）と V2（死体が席を塞ぐ＝§1-3 の擬似コードそのまま）を並べる。**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 版 | 湧/戦 | 塞/戦 | 倒/戦 | 倒÷湧 | 追撃/戦 | 毒/戦 | 深追/戦 | 代金 回/戦 | 平均打点 | 決着T | 勝率 |");
        Console.WriteLine("|---|---|:-:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

        var killers = new Dictionary<string, int>();
        var gSummon = new double[3]; var gKilled = new double[3]; var gFireA = new double[3];
        var gFireP = new double[3]; var gFireO = new double[3]; var gHits = new double[3];
        var gAtk = new double[3]; var gWin = new double[3]; var gBlock = new double[3];
        int gN = 0;
        foreach (var (name, f) in BtRows())
            for (int st = 1; st < btStages.Count; st++)
            {
                for (int v = 1; v <= 2; v++)
                {
                    double su = 0, bl = 0, ki = 0, fa = 0, fp = 0, fo = 0, hi = 0, at = 0, tu = 0;
                    int win = 0;
                    for (int seed = 0; seed < BtGateSeeds; seed++)
                    {
                        BattleResult r = BtRun(f, btStages[st].Enemy, seed, v);
                        su += r.BetraySummoned; bl += r.BetrayBlocked; ki += r.BetrayKilled;
                        fa += r.BetrayFireAttack; fp += r.BetrayFirePoison; fo += r.BetrayFireOverreach;
                        hi += r.BetrayHits; at += r.BetrayHitAtkSum; tu += r.Turns;
                        if (r.PlayerWon) win++;
                        if (v == 1)
                            foreach (var kv in r.TallyByUnit)
                                if (kv.Value.BetrayFodderKills > 0)
                                    killers[kv.Key] = killers.GetValueOrDefault(kv.Key) + kv.Value.BetrayFodderKills;
                    }
                    double n = BtGateSeeds;
                    gSummon[v] += su; gKilled[v] += ki; gFireA[v] += fa; gFireP[v] += fp; gFireO[v] += fo;
                    gHits[v] += hi; gAtk[v] += at; gWin[v] += win; gBlock[v] += bl;
                    if (v == 1) gN += BtGateSeeds;
                    Console.WriteLine($"| {name} | 第{st + 1}波 | {(v == 1 ? "**V1**" : "V2")} "
                                      + $"| {su / n:F2} | {bl / n:F2} | {ki / n:F2} "
                                      + $"| {(su > 0 ? (ki / su).ToString("F2") : "—")} "
                                      + $"| {fa / n:F2} | {fp / n:F2} | {fo / n:F2} "
                                      + $"| {hi / n:F2} | {(hi > 0 ? (at / hi).ToString("F1") : "—")} "
                                      + $"| {tu / n:F1} | {win * 100.0 / n:F1}% |");
                }
            }
        Console.WriteLine();
        for (int v = 1; v <= 2; v++)
            Console.WriteLine($"**合計（20 セル・{BtVName(v)}）**: 湧 {gSummon[v] / gN:F2} ／ 塞 {gBlock[v] / gN:F2} "
                              + $"／ 倒 {gKilled[v] / gN:F2} "
                              + $"（{(gSummon[v] > 0 ? gKilled[v] / gSummon[v] * 100 : 0):F1}%）／ "
                              + $"追撃 {gFireA[v] / gN:F2} ／ 毒 {gFireP[v] / gN:F2} ／ 深追い {gFireO[v] / gN:F2} "
                              + $"／ 代金 {gHits[v] / gN:F2} 回・平均打点 {(gHits[v] > 0 ? gAtk[v] / gHits[v] : 0):F1}"
                              + $" ／ 勝率 {gWin[v] * 100.0 / gN:F1}%。");
        Console.WriteLine();
        Console.WriteLine($"> **§1-3 の擬似コードをそのまま写すと 1戦に {gSummon[2] / gN:F2} 体しか湧かない**"
                          + $"（塞がれた回数 {gBlock[2] / gN:F2} 回/戦）。"
                          + "`BattleContext.Summon` の空き判定は**生死を問わない**"
                          + "（死者の枠を空きと見なすと蘇生と衝突するため）ので、"
                          + "**餌の死体が ○前2 を永久に塞ぐ**。"
                          + "**それでは `PlusText` の「毎ターン」が嘘になる**ので、"
                          + $"**餌にだけ「死者は席を塞がない」を許した版（V1・湧 {gSummon[1] / gN:F2} 体/戦）を機構として測る。**");
        Console.WriteLine();
        Console.WriteLine("**門の判定**（3つとも 0 より大きいこと。分母は V1）:");
        Console.WriteLine($"- 1. 餌が湧く: **{(gSummon[1] > 0 ? "○" : "×")}**（{gSummon[1] / gN:F2} 回/戦）");
        Console.WriteLine($"- 2. 湧いた餌が倒される: **{(gKilled[1] > 0 ? "○" : "×")}**"
                          + $"（{gKilled[1] / gN:F2} 回/戦・{(gSummon[1] > 0 ? gKilled[1] / gSummon[1] * 100 : 0):F1}%）");
        Console.WriteLine($"- 3. 餌の撃破で読み手が発火する: **{(gFireA[1] + gFireP[1] + gFireO[1] > 0 ? "○" : "×")}**"
                          + $"（追撃 {gFireA[1] / gN:F2} ／ 毒 {gFireP[1] / gN:F2} ／ 深追い {gFireO[1] / gN:F2} 回/戦）");
        Console.WriteLine();
        Console.WriteLine("### 餌を倒した駒の内訳");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 撃破（延べ） | 割合 |");
        Console.WriteLine("|---|--:|--:|");
        int totKill = killers.Values.Sum();
        foreach (var kv in killers.OrderByDescending(x => x.Value))
            Console.WriteLine($"| {(btIdx.ContainsKey(kv.Key) ? btRoster[btIdx[kv.Key]].Name : kv.Key)} "
                              + $"| {kv.Value} | {kv.Value * 100.0 / Math.Max(1, totKill):F1}% |");
        Console.WriteLine();

        Console.WriteLine("## A-3. 紙のスループット（**門ではなく出力**。規約 (G7)）");
        Console.WriteLine();
        Console.WriteLine("**分子について3つを先に書く（規約 (G7)）:**");
        Console.WriteLine();
        Console.WriteLine("1. **線形**。餌1体の撃破が読み手を1回ずつ起動するだけで、"
                          + "撃破が撃破を呼ぶ項（ハギの連鎖は自傷 5 で止まる）を持たない。");
        Console.WriteLine("2. **門ではなく出力**。上の A-2 が門で、ここは大きさ。");
        Console.WriteLine("3. **分母を削らない。増やす側**"
                          + "——餌は敵の体を1つ増やすので、決着ターンが伸びこそすれ縮まない。"
                          + "**紙は下限のはず。外したら書く。**");
        Console.WriteLine();
        for (int v = 1; v <= 2; v++)
        {
            Console.WriteLine($"    {BtVName(v)}");
            Console.WriteLine($"      餌由来の出力/戦 ＝ 追撃 {gFireA[v] / gN:F2} 回 ＋ 毒 {gFireP[v] / gN:F2} 層 ＋ 深追い {gFireO[v] / gN:F2} 回");
            Console.WriteLine($"      餌の代金/戦     ＝ {gHits[v] / gN:F2} 回 × 平均打点 {(gHits[v] > 0 ? gAtk[v] / gHits[v] : 0):F1}"
                              + $" ＝ **{gAtk[v] / gN:F1} 点/戦**");
        }
        Console.WriteLine();

        Console.WriteLine("## A-4. 予測（**測る前に書いた**）");
        Console.WriteLine();
        Console.WriteLine("- **意図した相手の1位はハギ。**`PursuerTrait` は敵の死だけを読み、倒れると割り込んで薙ぎ。");
        Console.WriteLine("- **ラウは効かない**——餌に毒が乗っている必要があり、グザ／スィドが同席する行に限られる。");
        Console.WriteLine("- **エグは符号が割れる。** 撃破で手番を失うが `IdleTurn` がガン・バンに回る。");
        Console.WriteLine("- **単体高倍率（トメ・ナタ・ザン）は下がる。** 前列の標的が3体になり、本物に当たる確率が 2/3。");
        Console.WriteLine("- **裂きのキリ・刻みのノミも下がる**（傷を餌に刻むと投資が丸ごと無駄）。");

        var hushRow = BtRows()[0];
        double s2 = 0;
        for (int seed = 0; seed < BtGateSeeds; seed++)
            s2 += BtRun(hushRow.F, btStages[1].Enemy, seed, 1).BetraySummoned;
        Console.WriteLine($"- **第二波（粛）は関係ない**——召喚は `OnTurnStart`（ターン外の行動ではない）。"
                          + $"実測で第二波の湧きは **{s2 / BtGateSeeds:F2} 回/戦**で 0 ではない。**○**");
        Console.WriteLine();
        return;
    }

    // =================================================================================
    // 2×2（第81期 `pairs2` の写し。**定数は1つも変えていない**）
    //
    //   y11 = 両方が本物 / y10 = A 本物・B 素体 / y01 = A 素体・B 本物 / y00 = 両方が素体
    //   相乗(A,B) = y11 − y10 − y01 + y00
    //   Δ相乗     = 相乗(V1) − 相乗(V0)
    //
    // **A ＝ ソムに固定**（規約 (G5)：餌を供給しているのはこの駒だけ）。
    // =================================================================================
    const int BtTableSeed = 10_300_000;   // **第96期の 9,600,000 とは別の標本**（第89期の作法）
    const int BtK2 = 64, BtS2 = 2, BtBand = 0, BtM = 8;
    const int BtStrong = 7, BtWeakPct = 60, BtDrawCap = 20000;

    var btWeakCache = new Dictionary<string, UnitDef>();
    UnitDef BtWeakOf(UnitDef d)
    {
        if (btWeakCache.TryGetValue(d.Id, out UnitDef? w)) return w;
        w = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp * BtWeakPct / 100,
            Attack = d.Attack, Speed = d.Speed, Traits = d.Traits, Pattern = d.Pattern, Actions = d.Actions
        };
        btWeakCache[d.Id] = w;
        return w;
    }
    var btWeak = btStages.Select(st =>
    {
        var f = new Formation();
        foreach ((int sl, UnitDef d) in st.Enemy.Occupied()) f[sl] = BtWeakOf(d);
        return new EnemyCatalog.Stage(st.Name, f);
    }).ToArray();

    UnitDef BtPlain(UnitDef d) => new()
    {
        Id = d.Id + "_plain", Name = "素体の" + d.Name,
        MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    var btPlainMap = btRoster.ToDictionary(d => d.Id, BtPlain);

    UnitDef[] BtFill(UnitDef[] pool, int strong0, int seed)
    {
        int rn = pool.Length;
        var rng = new Random(seed);
        var idx = new int[rn];
        for (int k = 0; k < rn; k++) idx[k] = k;
        int remain = rn, strong = strong0;
        var picked = new UnitDef[3];
        for (int r = 0; r < 3; r++)
        {
            var offer = new UnitDef[3];
            for (int t = 0; t < 3; t++)
            {
                int j = t + rng.Next(remain - t);
                (idx[t], idx[j]) = (idx[j], idx[t]);
                offer[t] = pool[idx[t]];
            }
            UnitDef sel = strong < 2
                ? offer.OrderByDescending(x => x.Attack).ThenBy(x => x.Id, StringComparer.Ordinal).First()
                : offer.OrderByDescending(x => x.MaxHp).ThenBy(x => x.Id, StringComparer.Ordinal).First();
            picked[r] = sel;
            if (sel.Attack >= BtStrong) strong++;
            int pi = 0;
            for (int t = 0; t < 3; t++) if (ReferenceEquals(pool[idx[t]], sel)) { pi = t; break; }
            (idx[pi], idx[remain - 1]) = (idx[remain - 1], idx[pi]);
            remain--;
        }
        return picked;
    }
    int BtDrawSeed(int pairIx, int draw)
    {
        ulong x = (ulong)BtTableSeed + (ulong)pairIx * 1_000_003UL + (ulong)draw * 7_919UL;
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        x ^= x >> 31;
        return (int)(x & 0x7FFFFFFFUL);
    }
    // TSV の添字: 0 = y11 ／ 1 = y01（**A 素体**）／ 2 = y10（**B 素体**）／ 3 = y00
    Formation BtForm(UnitDef[] u, int[] seats, int cell)
    {
        var f = new Formation();
        for (int k = 0; k < 5; k++)
        {
            bool plain = (k == 0 && (cell & 1) != 0) || (k == 1 && (cell & 2) != 0);
            f[seats[k]] = plain ? btPlainMap[u[k].Id] : u[k];
        }
        return f;
    }
    double BtRate(Formation f, int v)
    {
        double sum = 0;
        for (int wi = 1; wi < btStages.Count; wi++)
        {
            int wins = 0;
            for (int seed = BtBand; seed < BtBand + BtM; seed++)
                if (BtRun(f, btWeak[wi].Enemy, seed, v).PlayerWon) wins++;
            sum += wins * 100.0 / BtM;
        }
        return sum / (btStages.Count - 1);
    }
    var btPairIxOf = new int[btRN, btRN];
    {
        int pi = 0;
        for (int a = 0; a < btRN; a++) for (int b = a + 1; b < btRN; b++) { btPairIxOf[a, b] = btPairIxOf[b, a] = pi; pi++; }
    }
    List<UnitDef[]> BtFills(int a, int b)
    {
        var pool = btRoster.Where((_, u) => u != a && u != b).ToArray();
        int strong0 = (btRoster[a].Attack >= BtStrong ? 1 : 0) + (btRoster[b].Attack >= BtStrong ? 1 : 0);
        var seen = new HashSet<(int, int, int)>();
        var fills = new List<UnitDef[]>();
        for (int draw = 0; fills.Count < BtS2 * BtK2 && draw < BtDrawCap; draw++)
        {
            var f = BtFill(pool, strong0, BtDrawSeed(btPairIxOf[a, b], draw));
            var t = f.Select(d => btIdx[d.Id]).OrderBy(x => x).ToArray();
            if (seen.Add((t[0], t[1], t[2]))) fills.Add(f);
        }
        return fills;
    }

    // ---------------------------------------------------------------------------------
    // `run` —— 2×2 を TSV へ。**V0 と V1 を両方吐く**（第91期 `soak run2` と同じ形）。
    // 同じ台・同じ席・同じ戦闘 seed の対にするので、真の効果が 0 なら Δ は厳密に 0 になる。
    // ---------------------------------------------------------------------------------
    if (btMode == "run")
    {
        int btSkip = args.Length > 3 ? int.Parse(args[3]) : 0;
        int btTake = args.Length > 4 ? int.Parse(args[4]) : int.MaxValue;
        int a0 = btIdx[BtAId];
        var others = Enumerable.Range(0, btRN).Where(u => u != a0).Skip(btSkip).Take(btTake).ToArray();
        foreach (int b in others)
        {
            var fills = BtFills(a0, b);
            var sb = new System.Text.StringBuilder();
            sb.Append(103).Append('\t').Append(a0).Append('\t').Append(b).Append('\t').Append(fills.Count);
            for (int t = 0; t < fills.Count; t++)
            {
                var team = new[] { btRoster[a0], btRoster[b], fills[t][0], fills[t][1], fills[t][2] };
                int[] seats = BtSeats(team);
                for (int v = 0; v < 2; v++)
                    for (int cell = 0; cell < 4; cell++)
                        sb.Append('\t').Append(BtRate(BtForm(team, seats, cell), v).ToString("F6", btInv));
            }
            Console.WriteLine(sb.ToString());
        }
        return;
    }

    // ---------------------------------------------------------------------------------
    // 表B・C —— 主判定と副判定
    // ---------------------------------------------------------------------------------
    if (btMode == "tables")
    {
        var files = args.Skip(3).ToArray();
        if (files.Length == 0) { Console.WriteLine("使い方: `betray tables <run の TSV...>`"); return; }

        // b -> 台ごとの [v0 の4セル, v1 の4セル]
        var data = new Dictionary<int, List<(double[] V0, double[] V1)>>();
        int aIx = -1;
        foreach (string path in files)
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length == 0) continue;
                var c = line.Split('\t');
                int a = int.Parse(c[1]), b = int.Parse(c[2]), nT = int.Parse(c[3]);
                if (aIx < 0) aIx = a; else if (a != aIx) throw new InvalidOperationException($"{path}: A が混ざっている");
                var list = data.TryGetValue(b, out var L) ? L : (data[b] = new List<(double[], double[])>());
                int at = 4;
                for (int t = 0; t < nT; t++)
                {
                    var y0 = new double[4]; var y1 = new double[4];
                    for (int k = 0; k < 4; k++) y0[k] = double.Parse(c[at++], btInv);
                    for (int k = 0; k < 4; k++) y1[k] = double.Parse(c[at++], btInv);
                    list.Add((y0, y1));
                }
            }

        double BtSyn(double[] y) => y[0] - y[2] - y[1] + y[3];
        bool BtInfo(double[] y) => !(y[3] < BtEps && y[1] < BtEps) && !(y[3] > 100 - BtEps && y[1] > 100 - BtEps);
        double BtPct(IReadOnlyList<double> xs, double q)
        {
            if (xs.Count == 0) return double.NaN;
            var s = xs.OrderBy(v => v).ToArray();
            if (s.Length == 1) return s[0];
            double pos = q * (s.Length - 1);
            int lo = (int)Math.Floor(pos), hi = Math.Min(lo + 1, s.Length - 1);
            return s[lo] + (pos - lo) * (s[hi] - s[lo]);
        }

        var bs = data.Keys.OrderBy(x => x).ToArray();
        var dAll = new Dictionary<int, double>();
        var dSer = new Dictionary<int, double[]>();
        var dInfo = new Dictionary<int, double>();
        var nInfo = new Dictionary<int, int>();
        int floorCells = 0, ceilCells = 0, allCells = 0;
        foreach (int b in bs)
        {
            var all = new List<double>(); var s0 = new List<double>(); var s1 = new List<double>();
            var inf = new List<double>();
            var L = data[b];
            for (int t = 0; t < L.Count; t++)
            {
                double d = BtSyn(L[t].V1) - BtSyn(L[t].V0);
                all.Add(d); (t % BtS2 == 0 ? s0 : s1).Add(d);
                if (BtInfo(L[t].V0)) inf.Add(d);
                allCells++;
                if (L[t].V0[3] < BtEps && L[t].V0[1] < BtEps) floorCells++;
                else if (L[t].V0[3] > 100 - BtEps && L[t].V0[1] > 100 - BtEps) ceilCells++;
            }
            if (all.Count == 0) continue;
            dAll[b] = all.Average();
            dSer[b] = new[] { s0.Count > 0 ? s0.Average() : double.NaN, s1.Count > 0 ? s1.Average() : double.NaN };
            dInfo[b] = inf.Count > 0 ? inf.Average() : double.NaN;
            nInfo[b] = inf.Count;
        }

        var intendedIx = btIntended.Where(btIdx.ContainsKey).Select(x => btIdx[x]).ToHashSet();
        var unintended = dAll.Keys.Where(b => !intendedIx.Contains(b)).ToArray();
        double floor = BtPct(unintended.Select(b => Math.Abs(dAll[b])).ToArray(), 0.95);
        var ranked = dAll.OrderByDescending(x => x.Value).ToArray();

        Console.WriteLine("# 第103期 表B —— 2×2 の主判定");
        Console.WriteLine();
        Console.WriteLine($"A ＝ {btRoster[aIx].Name}（`{btRoster[aIx].Id}`。規約 (G5)：餌を供給しているのはこの駒だけ）"
                          + $" ／ B ＝ 残り {bs.Length} 体 × {BtK2 * BtS2} 台 × 4 セル × 4 波 × seed {BtBand}..{BtBand + BtM - 1}。");
        Console.WriteLine();
        Console.WriteLine($"**情報帯の割合 ＝ {(allCells - floorCells - ceilCells) * 100.0 / Math.Max(1, allCells):F1}%**"
                          + $"（床 {floorCells * 100.0 / Math.Max(1, allCells):F1}% ／ 天井 {ceilCells * 100.0 / Math.Max(1, allCells):F1}%）。"
                          + "第88期 §8-5 の並び: カド 65.2% / ハリ 70.1% / ミオ 56.5%（第96期のムドは攻3）。");
        Console.WriteLine();
        Console.WriteLine("**背かれは駒の特性の中にある規則なので、規約 (G3) により主判定は情報帯フィルタ<u>有り</u>でよい**"
                          + "——ただし**両方を出す**（フィルタは判定に鈍い・第91期の実測）。以下の主判定はフィルタ無し。");
        Console.WriteLine();
        Console.WriteLine($"**増分尺度のノイズ床（第89期の規約：意図しない相手 {unintended.Length} 体の |Δ相乗| の 95%tile）"
                          + $" ＝ {floor:F2}pt。**");
        Console.WriteLine();
        Console.WriteLine("| 順位 | B | 意図 | Δ相乗 | 系列1 | 系列2 | 2系列とも正 | 床超え | 参考: フィルタ有り |");
        Console.WriteLine("|--:|---|:-:|--:|--:|--:|:-:|:-:|--:|");
        for (int i = 0; i < ranked.Length; i++)
        {
            int b = ranked[i].Key;
            bool both = dSer[b][0] > 0 && dSer[b][1] > 0;
            bool over = Math.Abs(dAll[b]) > floor;
            string tag = intendedIx.Contains(b) ? "**○**"
                       : btIndirect.Contains(btRoster[b].Id) ? "間接"
                       : btLoss.Contains(btRoster[b].Id) ? "損" : "";
            Console.WriteLine($"| {i + 1} | {btRoster[b].Name} | {tag} "
                              + $"| {dAll[b]:+0.00;-0.00;0.00} | {dSer[b][0]:+0.00;-0.00;0.00} | {dSer[b][1]:+0.00;-0.00;0.00} "
                              + $"| {(both ? "○" : "")} | {(over ? "○" : "")} "
                              + $"| {(double.IsNaN(dInfo[b]) ? "—" : dInfo[b].ToString("+0.00;-0.00;0.00"))}（{nInfo[b]} 台） |");
        }
        Console.WriteLine();
        var q11 = intendedIx.Where(dAll.ContainsKey)
                            .Where(b => dSer[b][0] > 0 && dSer[b][1] > 0 && Math.Abs(dAll[b]) > floor).ToArray();
        int q12 = unintended.Count(b => Math.Abs(dAll[b]) > floor);
        Console.WriteLine($"**Q1-1（意図した相手のうち少なくとも1枚が 2系列とも正 かつ 床超え）: "
                          + $"{q11.Length} / {intendedIx.Count(b => dAll.ContainsKey(b))} 枚**"
                          + $"{(q11.Length > 0 ? "（" + string.Join("・", q11.Select(b => btRoster[b].Name)) + "）" : "")}"
                          + $" —— {(q11.Length > 0 ? "**○**" : "**×**")}");
        Console.WriteLine();
        Console.WriteLine($"**Q1-2（意図しない相手で床を超えた体数）: {q12} 体** —— "
                          + $"{(q12 <= 3 ? "**○**" : "**×**")}"
                          + $"（床は 95%tile なので構成上おおよそ {unintended.Length * 5 / 100} 体が必ず超える）");
        Console.WriteLine();
        int bestRank = ranked.Select((x, i) => (x.Key, i)).Where(x => intendedIx.Contains(x.Key))
                             .Select(x => x.i + 1).DefaultIfEmpty(-1).Min();
        Console.WriteLine($"**Q2（意図した組の最良順位）: {bestRank} 位 / {ranked.Length}**");
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("# 第103期 表C —— 副判定（Q2 間接の読み手・Q3 損の側）");
        Console.WriteLine();
        void Group(string title, string[] ids, string note)
        {
            Console.WriteLine($"## {title}");
            Console.WriteLine();
            Console.WriteLine("| 駒 | Δ相乗 | 系列1 | 系列2 | 床超え | 順位 |");
            Console.WriteLine("|---|--:|--:|--:|:-:|--:|");
            double sum = 0; int cnt = 0, neg = 0;
            foreach (string id in ids)
            {
                if (!btIdx.TryGetValue(id, out int b) || !dAll.ContainsKey(b)) continue;
                int rk = Array.FindIndex(ranked, x => x.Key == b) + 1;
                Console.WriteLine($"| {btRoster[b].Name} | {dAll[b]:+0.00;-0.00;0.00} "
                                  + $"| {dSer[b][0]:+0.00;-0.00;0.00} | {dSer[b][1]:+0.00;-0.00;0.00} "
                                  + $"| {(Math.Abs(dAll[b]) > floor ? "○" : "")} | {rk} |");
                sum += dAll[b]; cnt++; if (dAll[b] < 0) neg++;
            }
            Console.WriteLine();
            Console.WriteLine($"平均 **{(cnt > 0 ? sum / cnt : 0):+0.00;-0.00;0.00}pt** ／ 負の枚数 **{neg} / {cnt}**。{note}");
            Console.WriteLine();
        }
        Group("C-1. Q2 —— 間接の読み手", btIndirect,
              "**意図した相手には入れていない**（入れると Q1-1 が通りやすくなり Q1-2 の分母が減る・第96期）。");
        Group("C-2. Q3 —— 損の側（可変コスト型の証拠）", btLoss,
              "**符号が負なら「読み手がいれば資産、いなければ災厄」が実測で立つ**（採否とは独立に記録する）。");
        return;
    }

    // ---------------------------------------------------------------------------------
    // 表D —— 会戦（Q4）と餌の代金（Q5）
    //
    // 規約 (G10): **分母は第2〜3波。第一波を入れない。**
    // 規約 (G12) として指示書が挙げた「抜けた試行数の併記」も出す。
    // 会戦は地点（3波）だけ——第100期に「5波の列は3波の列に誰も届かない2波を足しただけ」
    // （73行すべてで突破波数が厳密に一致）と確定している。
    // ---------------------------------------------------------------------------------
    if (btMode == "engage")
    {
        const int BtEngSeeds = 200;
        var rows = btCompare.Concat(btCross).Concat(BtRows()).ToArray();
        EnemyCatalog.Column col3 = EnemyCatalog.Columns
            .Where(c => c.Squads.Count == 3).OrderBy(c => c.Name).First();

        Console.WriteLine("# 第103期 表D —— 会戦（Q4）と餌の代金（Q5）");
        Console.WriteLine();
        Console.WriteLine($"地点（{col3.Name}・3波）× seed 0..{BtEngSeeds - 1} ／ 味方部隊は1つ。"
                          + $"行は `compare` {btCompare.Length} ＋ 交差帯 {btCross.Length} ＋ 試験行 {BtRows().Length}。");
        Console.WriteLine();
        Console.WriteLine("**規約 (G10): 残存・圧勝率・全滅勝ちの分母は第2〜3波**（第一波は教習波なので入れない）。");
        Console.WriteLine();

        (double Broke, int Win, int Q, double Surv, int Blow, int Narrow) Eng(Formation f, int v)
        {
            var column = new[] { f };
            double broke = 0; int win = 0, q = 0, blow = 0, narrow = 0; double surv = 0;
            for (int seed = 0; seed < BtEngSeeds; seed++)
            {
                EngagementResult r = EngagementEngine.Run(column, col3.Squads, seed, verbose: false,
                                                          betray: BtVer(v));
                int battles = r.Battles.Count;
                if (r.PlayerWon) { win++; broke += col3.Squads.Count; }
                else broke += battles - 1;
                // 第2〜3波だけを分母にする（(G10)）
                for (int b = 1; b < battles && b < col3.Squads.Count; b++)
                {
                    BattleResult br = r.Battles[b];
                    if (!br.PlayerWon) continue;
                    q++; surv += br.PlayerSurvivors;
                    if (br.PlayerSurvivors >= 4) blow++;
                    if (br.PlayerSurvivors <= 1) narrow++;
                }
            }
            return (broke, win, q, surv, blow, narrow);
        }

        Console.WriteLine("| 行 | 版 | 突破率 | 突破波数 | 抜けた試行 | 第2〜3波の勝ち | 残存 | 圧勝率 | 全滅勝ち |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|--:|");
        double w0 = 0, w1 = 0; int rowsWithSom = 0;
        foreach (var (name, f) in rows)
        {
            bool hasSom = f.Occupied().Any(x => x.Def.Id == BtAId);
            if (hasSom) rowsWithSom++;
            for (int v = 0; v < 2; v++)
            {
                // ソムがいない行は版で1ビットも動かないので V1 は出さない（表E で 0 件を示す）
                if (!hasSom && v == 1) continue;
                var e = Eng(f, v);
                double n = BtEngSeeds;
                if (v == 0) w0 += e.Win; else w1 += e.Win;
                Console.WriteLine($"| {name} | {(hasSom ? (v == 0 ? "V0" : "**V1**") : "—")} "
                                  + $"| {e.Win * 100.0 / n:F2}% | {e.Broke / n:F2} | {e.Win} "
                                  + $"| {e.Q} | {(e.Q > 0 ? (e.Surv / e.Q).ToString("F2") : "—")} "
                                  + $"| {(e.Q > 0 ? (e.Blow * 100.0 / e.Q).ToString("F1") + "%" : "—")} "
                                  + $"| {(e.Q > 0 ? (e.Narrow * 100.0 / e.Q).ToString("F1") + "%" : "—")} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"**ソムを含む行は {rowsWithSom} 行**（`compare` 61 ＋ 交差帯 12 にソムは1体もいないので、"
                          + "版が動かせるのは試験行だけ）。");
        Console.WriteLine();

        // ---- Q5: 餌の代金（波別） ----
        Console.WriteLine("## D-2. Q5 —— 餌の代金（波別・単発戦）");
        Console.WriteLine();
        Console.WriteLine("**本物の敵に当たらなかった手番の数**と、そのときの平均打点。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 代金 回/戦 | 平均打点 | 代金 点/戦 | 与ダメ/戦 | 代金の割合 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in BtRows())
            for (int st = 1; st < btStages.Count; st++)
            {
                double hi = 0, at = 0, dmg = 0;
                for (int seed = 0; seed < BtGateSeeds; seed++)
                {
                    BattleResult r = BtRun(f, btStages[st].Enemy, seed, 1);
                    hi += r.BetrayHits; at += r.BetrayHitAtkSum;
                    dmg += r.TallyByUnit.Values.Sum(t => (double)t.DamageToEnemy);
                }
                double n = BtGateSeeds;
                double cost = at / n;
                Console.WriteLine($"| {name} | 第{st + 1}波 | {hi / n:F2} | {(hi > 0 ? (at / hi).ToString("F1") : "—")} "
                                  + $"| {cost:F1} | {dmg / n:F1} | {(dmg > 0 ? cost / (dmg / n) * 100 : 0):F1}% |");
            }
        Console.WriteLine();
        return;
    }

    // ---------------------------------------------------------------------------------
    // 表E・F —— 拒否権と自己検査
    // ---------------------------------------------------------------------------------
    if (btMode == "check")
    {
        const int BtVetoSeeds = 200;
        Console.WriteLine("# 第103期 表E —— 拒否権（`compare` 61行・交差帯12行）");
        Console.WriteLine();
        Console.WriteLine("**ソムは 61 行にも交差帯 12 行にも1体もいない**ので、"
                          + "**拒否権1（壊れ）も拒否権2（第五波の歯止め）も原理的に立たない。**"
                          + "立たないこと自体を自己検査として数える（規約 (G4)：分母を書く）。");
        Console.WriteLine();

        int somRows = btCompare.Concat(btCross).Count(r => r.F.Occupied().Any(x => x.Def.Id == BtAId));
        Console.WriteLine($"- ソムを含む行: **{somRows} / {btCompare.Length + btCross.Length}**");
        Console.WriteLine();

        double[] BtRow(Formation f, int v)
        {
            var r = new double[btStages.Count];
            for (int st = 0; st < btStages.Count; st++)
            {
                int wins = 0;
                for (int seed = 0; seed < BtVetoSeeds; seed++)
                    if (BtRun(f, btStages[st].Enemy, seed, v).PlayerWon) wins++;
                r[st] = wins * 100.0 / BtVetoSeeds;
            }
            return r;
        }

        int diffCells = 0, diffRows = 0;
        foreach (var (name, f) in btCompare.Concat(btCross))
        {
            double[] a = BtRow(f, 0), b = BtRow(f, 1);
            int d = 0;
            for (int st = 0; st < a.Length; st++) if (Math.Abs(a[st] - b[st]) > BtEps) d++;
            if (d > 0) { diffRows++; diffCells += d; Console.WriteLine($"- **{name}**: {d} セルが動いた"); }
        }
        Console.WriteLine($"**V0 対 V1 の差: {diffCells} セル / {diffRows} 行**"
                          + $"（{(btCompare.Length + btCross.Length) * btStages.Count} セル中）—— "
                          + $"{(diffCells == 0 ? "**○**" : "**×**")}");
        Console.WriteLine();

        Console.WriteLine("# 第103期 表F —— 自己検査 (a)〜(k)");
        Console.WriteLine();

        // (a) compare 305 セルが docs/balance.md と 0 件
        string balPath = btRoot is null ? "" : Path.Combine(btRoot, "docs", "balance.md");
        int aMiss = 0, aCells = 0;
        if (File.Exists(balPath))
        {
            var doc = new Dictionary<string, double[]>();
            foreach (string line in File.ReadAllLines(balPath))
            {
                if (!line.StartsWith("|")) continue;
                var c = line.Split('|').Select(x => x.Trim()).ToArray();
                if (c.Length < 3 + btStages.Count) continue;
                var vals = new List<double>();
                for (int k = 2; k < 2 + btStages.Count; k++)
                    if (double.TryParse(c[k].Replace("%", "").Replace("*", ""), System.Globalization.NumberStyles.Any, btInv, out double d)) vals.Add(d);
                if (vals.Count == btStages.Count) doc[c[1].Replace("*", "")] = vals.ToArray();
            }
            foreach (var (name, f) in btCompare)
            {
                if (!doc.TryGetValue(name, out double[]? want)) { aMiss++; continue; }
                double[] got = BtRow(f, 0);
                for (int st = 0; st < got.Length; st++) { aCells++; if (Math.Abs(got[st] - want[st]) > 0.05) aMiss++; }
            }
        }
        Console.WriteLine($"- **(a)** `BetrayRule.Enabled = false` のとき `compare` が `docs/balance.md` と "
                          + $"**{aMiss} 件**（{aCells} セル）—— {(aMiss == 0 && aCells > 0 ? "**○**" : "**×**")}");

        // (b)〜(g) は試験行を V1 で回して数える
        int bMismatch = 0, cAlly = 0, dWrong = 0, eMax = 0, fSell = 0, gRev = 0;
        int summoned = 0, killed = 0;
        foreach (var (_, f) in BtRows())
            for (int st = 1; st < btStages.Count; st++)
                for (int seed = 0; seed < BtGateSeeds; seed++)
                {
                    BattleResult r = BtRun(f, btStages[st].Enemy, seed, 1);
                    cAlly += r.BetrayAllySide; dWrong += r.BetrayWrongSlot;
                    eMax = Math.Max(eMax, r.BetrayMaxAlive);
                    fSell += r.BetrayIdleSellable; gRev += r.BetrayRevived;
                    summoned += r.BetraySummoned; killed += r.BetrayKilled;
                }

        // (b) ソム素体の2セル（y00 / y01）が版で完全一致
        {
            int a0 = btIdx[BtAId];
            int checkedCells = 0;
            foreach (int b in new[] { btIdx["hagi"], btIdx["borg"], btIdx["gald"] })
            {
                var fills = BtFills(a0, b);
                for (int t = 0; t < Math.Min(8, fills.Count); t++)
                {
                    var team = new[] { btRoster[a0], btRoster[b], fills[t][0], fills[t][1], fills[t][2] };
                    int[] seats = BtSeats(team);
                    foreach (int cell in new[] { 1, 3 })   // 1 = y01（A 素体）／ 3 = y00
                    {
                        checkedCells++;
                        if (Math.Abs(BtRate(BtForm(team, seats, cell), 0)
                                     - BtRate(BtForm(team, seats, cell), 1)) > BtEps) bMismatch++;
                    }
                }
            }
            Console.WriteLine($"- **(b)** ソム素体の 2 セル（`y00` / `y01`）が版で一致: "
                              + $"ずれ **{bMismatch} / {checkedCells} セル** —— {(bMismatch == 0 ? "**○**" : "**×**")}"
                              + "（情報帯の選別に V0 のセルしか使っていないことの根拠・第91期 (G3)）");
        }

        Console.WriteLine($"- **(c)** 餌が味方陣に湧いた回数: **{cAlly}** —— {(cAlly == 0 ? "**○**" : "**×**")}");
        Console.WriteLine($"- **(d)** 餌が ○前2（slot {BetrayedTrait.FodderSlot}）以外に湧いた回数: **{dWrong}** —— "
                          + $"{(dWrong == 0 ? "**○**" : "**×**")}");
        Console.WriteLine($"- **(e)** 同時に存在した餌の最大数: **{eMax}** —— {(eMax <= 1 ? "**○**" : "**×**")}");
        Console.WriteLine($"- **(f)** 餌の空き手番が号令・据えに売れると判定された回数: **{fSell}** —— "
                          + $"{(fSell == 0 ? "**○**" : "**×**")}"
                          + "（engine は `IdleTurn` そのものは立てる。買い手が通す `Trait.SurrenderedTurn` が偽なので売れない）");
        Console.WriteLine($"- **(g)** 餌が蘇生された回数: **{gRev}** —— {(gRev == 0 ? "**○**" : "**×**")}"
                          + "（会戦の持ち越しは `current` だけを見るので構造的に 0）");

        // (h) 既存の Summon の呼び出し口が挙動を変えていない
        //     分裂（ムグ）を含む compare の行が V0 で docs と一致していれば、走査順は動いていない。
        var mugRows = btCompare.Where(r => r.F.Occupied().Any(x => x.Def.Id == "mug")).ToArray();
        Console.WriteLine($"- **(h)** 既存の `Summon` の呼び出し口（分裂・ムグ）を含む行は "
                          + $"**{mugRows.Length} 行**で、(a) の 0 件にそのまま含まれている —— "
                          + $"{(aMiss == 0 && mugRows.Length > 0 ? "**○**" : "**×**")}");

        // (i) 紙と実測
        Console.WriteLine($"- **(i)** 紙と実測の照合は表A（A-3）。**線形・出力・分母は増やす側**と"
                          + $"先に書いた。実測の湧き **{summoned}** ／ 倒 **{killed}** 件。");

        // (j) 主判定が2系列で同符号 → tables 側で出す
        Console.WriteLine("- **(j)** 主判定が2系列で同符号: **表B の「2系列とも正」列**を見ること。");

        // (k) PickOne
        int pick = 0, pickLoose = 0;
        if (btRoot is not null)
            foreach (string ff in Directory.GetFiles(Path.Combine(btRoot, "BattleCore"), "*.cs"))
            {
                string body = string.Join("\n", File.ReadAllLines(ff).Where(l => !l.TrimStart().StartsWith("//")));
                pick += System.Text.RegularExpressions.Regex.Matches(body, @"ctx\.PickOne\(").Count;
                pickLoose += System.Text.RegularExpressions.Regex.Matches(body, @"PickOne\(").Count;
            }
        Console.WriteLine($"- **(k)** `BattleCore` の `ctx.PickOne(` 呼び出し **{pick} 箇所**"
                          + $"（CLAUDE.md の 8 箇所と同数か: {(pick == 8 ? "**○**" : "**×**")}）。"
                          + $"参考: `PickOne(` の全一致は {pickLoose} 箇所"
                          + "（定義と engine 内部の呼び出しを含む数え方。`hex` / `curse` の自己検査はこちらを出している）");
        Console.WriteLine();
        return;
    }

    Console.WriteLine("使い方: `betray phase0` / `betray run <skip> <take>` / `betray tables <TSV...>` "
                      + "/ `betray engage` / `betray check`");
    return;
}
}
