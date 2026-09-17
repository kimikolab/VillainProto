using BattleCore;
using static Common;

// =====================================================================================
// tempo モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "tempo")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 tempo
// =====================================================================================

static class TempoDiag
{
// 第105期 —— 手番の値段を測る（調査）。
//
// **盤面は1ビットも動かない。** 駒・特性・数値・engine の判定・境界の規則に触っていない
// （足したのは観測専用の計数と、`TakeTurn` に被せた枠だけ。受け入れ条件は `compare` 305 セル 0 件）。
//
// **診断の名前が `turn` ではないのは、`turn` が第60期（火選りを手番へ降ろす）で埋まっているから。**
// 指示書 §4 は「新しい診断 `turn` を1本足す」と書いているが、そこは
// 「既存の診断は1文字も書き換えない」と両立しない。名前だけを `tempo` に替えた。
//
//     dotnet run --project BattleSim -c Release 0 tempo phase0   # §2（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 tempo run      # 表B（駒ごとの生の表）
//     dotnet run --project BattleSim -c Release 0 tempo tables   # 表A〜D
//     dotnet run --project BattleSim -c Release 0 tempo check    # 表E（自己検査 (a)〜(f)）
public static void Run(string[] args, int stageIndex)
{
    string tpMode = args.Length > 2 ? args[2] : "tables";
    var tpCompare = CompareBuilds();
    var tpCross = CrossBuilds();
    var tpBuilds = tpCompare.Concat(tpCross).ToArray();
    IReadOnlyList<EnemyCatalog.Stage> tpStages = EnemyCatalog.Stages;
    const int TpSeeds = 200;   // compare と同じ帯

    var tpName = UnitCatalog.All.ToDictionary(d => d.Id, d => d.Name);

    if (tpMode == "phase0")
    {
        Console.WriteLine("# 手番の値段 —— 実装前の地図（第105期 Phase 0）");
        Console.WriteLine();
        Console.WriteLine("**戦闘を1回も回していない。** `docs/` には置かない。");
        Console.WriteLine();

        Console.WriteLine("## 0-1. 既存の計数で何が取れたか（足したものだけを列挙する）");
        Console.WriteLine();
        Console.WriteLine("`UnitTally` に**手番そのものを数えた列は1本も無かった**。");
        Console.WriteLine("近いものは3本あるが、どれも手番の代理にならない:");
        Console.WriteLine();
        Console.WriteLine("| 既存の列 | 何を数えているか | 手番の代理にならない理由 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| `Attacks` | `PerformAttack` を通った回数 | 術・溜め・潰れた手番が落ちる。反撃・割り込みが混じる |");
        Console.WriteLine("| `Charges` | 溜めた回数 | 溜めだけ |");
        Console.WriteLine("| `Slumbers` | まどろんだ回数 | 潰れ方の1つだけ |");
        Console.WriteLine();
        Console.WriteLine("**足したのは `UnitTally` の 17 本と ctx 側の総計 13 本。**");
        Console.WriteLine("どれも誰も読んで分岐しない・`verbose` 非依存・**版に依らない**");
        Console.WriteLine("（規則の分岐より手前、または規則を1つも見ない場所に置いた）。");
        Console.WriteLine();
        Console.WriteLine("    TurnsTaken / TurnAttacks / TurnSkills / TurnCharges / TurnStalls");
        Console.WriteLine("    StallStun / StallSlumber / StallImmobile / StallCanAct / TurnsSurrendered");
        Console.WriteLine("    DmgOutInTurn / DmgOutOffTurn / HealOutInTurn / HealOutOffTurn");
        Console.WriteLine("    StatusOutInTurn / StatusOutOffTurn");
        Console.WriteLine();

        Console.WriteLine("## 0-2. 手番の中と外をどこで切ったか（**境界の定義**）");
        Console.WriteLine();
        Console.WriteLine("定義は `BattleContext.InOwnTurn` の**1箇所だけ**。**3つの積**:");
        Console.WriteLine();
        Console.WriteLine("1. `TakeTurn` の枠の中であること（`TurnActor` が立っている）");
        Console.WriteLine("2. **出どころがその枠の主であること**");
        Console.WriteLine("3. `InReaction` / `InInterrupt` の中でないこと");
        Console.WriteLine();
        Console.WriteLine("| 経路 | 中／外 | 理由 |");
        Console.WriteLine("|---|:-:|---|");
        Console.WriteLine("| 自分の手番の通常攻撃・術・溜め | 中 | (1)(2)(3) すべて |");
        Console.WriteLine("| 責め苦の追撃（`OnAfterAttack`） | 中 | 自分の手番の中で自分が出どころ |");
        Console.WriteLine("| 棘の反撃（`OnDamaged`） | 外 | 殴った側の枠の中だが**出どころが違う**（(2) で外れる） |");
        Console.WriteLine("| `OnTurnStart`（瘴気・逸らし・駆り立て・吸い） | 外 | 行動順ループの**外側**（(1) で外れる） |");
        Console.WriteLine("| 仇討ち・軋み・追い打ち | 外 | (2) と (3) の両方で外れる |");
        Console.WriteLine("| `OnDeath` / `OnAnyDeath` / `OnAllyDeath` | 外 | 出どころが違う |");
        Console.WriteLine("| **再行動（第104期）で振った分** | **中** | `TakeTurn` を丸ごと渡しているので枠が立つ |");
        Console.WriteLine();
        Console.WriteLine("**`OnTurnStart` が「外」なのは意図した線**（第61期「`OnTurnStart` は speed = ∞ の席」）。");
        Console.WriteLine("あそこは手番の順序の外にあるので、手番の値段とは別の通貨で払われている。");
        Console.WriteLine();

        Console.WriteLine("## 0-3. 帰属できない出力（**誰のものでもない出力**）");
        Console.WriteLine();
        Console.WriteLine("| 通貨 | 帰属の手掛かり | 帰属できない経路 |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| 与ダメージ | `ApplyDamage` の `source` | **毒・燃焼の刻み**（`source` が null。第13期） |");
        Console.WriteLine("| 回復 | 第94期 (T2) の印（`Mark.Owner`） | `Heal` は源を引数で受け取らない |");
        Console.WriteLine("| 状態異常 | 同上（`NoteCarry` の中で読む） | engine が書く `IdleTurn`・境界の一括操作 |");
        Console.WriteLine();
        Console.WriteLine("3つとも `None` の桶へ入れて、`In + Off + None == All` を自己検査 (d) で突き合わせる。");
        Console.WriteLine();

        Console.WriteLine("## 0-4. 再行動が手番数に入ること");
        Console.WriteLine();
        Console.WriteLine("`EncoreRule` は `NoteEncore` の中から `TakeTurn` を呼ぶので、**枠がもう一度立つ**");
        Console.WriteLine("——`TurnsTaken` は自動で +1 される。**入れ子になる**（`HandleDeath` は");
        Console.WriteLine("誰かの `TakeTurn` の中から呼ばれる）ので、`TurnActor` は1本の変数ではなく");
        Console.WriteLine("**退避して戻す**形で持つ。自己検査 (c) が `TurnLoopCalls + EncoreFired` と突き合わせる。");
        Console.WriteLine();

        Console.WriteLine("## 0-5. `SurrenderedTurn` の読み方（第103期の訂正）");
        Console.WriteLine();
        Console.WriteLine("engine は `CanAct` が偽の駒にも **`IdleTurn` を無条件に立てる**ので、");
        Console.WriteLine("生の counter を見ると必ず「売れた」になる。数えるのは**買い手が通す判定**");
        Console.WriteLine("`Trait.SurrenderedTurn` のほう。**潰れた手番についてだけ1回**問う。");
        Console.WriteLine();
        Console.WriteLine("問い合わせが観測を汚さないように、印を落としてログを黙らせてから呼んでいる");
        Console.WriteLine("——`CanAct` は Sluggish / Sever がログを出し、Sever は counter を読む。");
        Console.WriteLine("**乱数は1つも引かない**（`CanAct` の4つの実装のどれも `Roll` を呼ばない）。");
        Console.WriteLine();

        Console.WriteLine("## 0-6. §1-4 の数え直し（**戦闘0回**）");
        Console.WriteLine();
        var tpAll = UnitCatalog.All;
        var tpSkillOnly = tpAll.Where(d => d.Actions is { Count: > 0 }
                                        && d.Actions.All(a => a.Kind == ActionKind.Skill)).ToArray();
        var tpNoActions = tpAll.Where(d => d.Actions is null || d.Actions.Count == 0).ToArray();
        var tpHasActions = tpAll.Where(d => d.Actions is { Count: > 0 }).ToArray();
        Console.WriteLine($"ロスターは **{tpAll.Count} 枚**（第103期に 52 枚で確定）。");
        Console.WriteLine();
        Console.WriteLine($"- `Actions` を持つ駒: **{tpHasActions.Length} 枚** — {string.Join(" / ", tpHasActions.Select(d => d.Name))}");
        Console.WriteLine($"- 術しか撃たない駒: **{tpSkillOnly.Length} 枚** — {string.Join(" / ", tpSkillOnly.Select(d => d.Name))}");
        Console.WriteLine($"- `Actions` を持たない駒: **{tpNoActions.Length} 枚**（{tpNoActions.Length * 100.0 / tpAll.Count:0.0}%）");
        Console.WriteLine();
        Console.WriteLine("**`TURN_SLOT_PLAN` の「46体中30体」は再現しない**——分母も分子も動いている。");
        Console.WriteLine("しかも「素の通常攻撃しか振らない」は `Actions` の有無だけでは決まらない");
        Console.WriteLine("——**不動のカドは `Actions = [Skill]` を持っている**（`Immobile` が拒むのは");
        Console.WriteLine("`ActionKind.Attack` だけなので、カドの手番は潰れずに術として通る）。**実測は表D で出す。**");
        Console.WriteLine();

        Console.WriteLine("## 0-7. 予測（**測る前に書く**）");
        Console.WriteLine();
        Console.WriteLine("1. **カドの手番外の割合は 100%、ドルガは 0%**（§1-3 の対照）。");
        Console.WriteLine("2. **ロスターの過半は手番型**（`Actions` を持たない駒が圧倒的多数）。");
        Console.WriteLine("3. **1手番あたりの与ダメージはドルガ（攻38）とキリ（攻1）で 38 倍開く。ノミは 10〜21。**");
        Console.WriteLine("4. **稼働率は 35〜40% 帯に集中する**（第86期）。**再行動で刻み手だけが上に外れる。**");
        Console.WriteLine();
        Console.WriteLine("**予測4 は測る前から怪しい**——第86期の 35〜40% は「**特性の発火** ÷ 決着T」で、");
        Console.WriteLine("この期が測るのは「**手番** ÷ 決着T」。生き残った駒は毎ターン手番を持つので上に寄るはず。");
        Console.WriteLine("**判定は指示書どおり出したうえで、ノノ・ハリの実測を併記する**（第86期の分子はこの2枚）。");
        return;
    }

    // ---------------------------------------------------------------------------------
    // 測定（73行 × 5波 × seed 0..199 = 73,000 戦）
    // ---------------------------------------------------------------------------------
    const int TpN = 21;
    const int TpTurns = 0, TpAtkT = 1, TpSkill = 2, TpCharge = 3, TpStall = 4,
              TpStun = 5, TpSlumber = 6, TpImmobile = 7, TpCanAct = 8, TpSold = 9,
              TpDmgIn = 10, TpDmgOff = 11, TpHealIn = 12, TpHealOff = 13,
              TpStatIn = 14, TpStatOff = 15, TpBattles = 16, TpSettle = 17,
              TpAttacks = 18, TpDmgEnemy = 19, TpEncore = 20;

    // tpAcc[0] = 第2〜5波（判定の分母・規約 (G10)）／ tpAcc[1] = 第一波（参考）
    var tpAcc = new Dictionary<string, long[]>[2];
    for (int i = 0; i < 2; i++) tpAcc[i] = new Dictionary<string, long[]>();

    long[] TpGet(int band, string id)
    {
        if (!tpAcc[band].TryGetValue(id, out long[]? a)) tpAcc[band][id] = a = new long[TpN];
        return a;
    }

    long gLoop = 0, gEncore = 0, gTurnsAll = 0;
    long gDmgAll = 0, gDmgIn = 0, gDmgOff = 0, gDmgNone = 0;
    long gHealAll = 0, gHealIn = 0, gHealOff = 0, gHealNone = 0;
    long gStatAll = 0, gStatIn = 0, gStatOff = 0, gStatNone = 0;
    int tpCollisions = 0, tpBattlesRun = 0;

    var tpSw = System.Diagnostics.Stopwatch.StartNew();
    for (int w = 0; w < tpStages.Count; w++)
    {
        int wband = w == 0 ? 1 : 0;
        var tpEnemyIds = new HashSet<string>(tpStages[w].Enemy.Occupied().Select(o => o.Item2.Id));
        for (int b = 0; b < tpBuilds.Length; b++)
        {
            // 味方側の駒だけを引く（`TallyByUnit` は両陣営を `Def.Id` で混ぜて持つ）。
            // 同じ Id が両陣営に出ると集計が壊れるので、衝突を数えて検算に出す。
            var tpMine = tpBuilds[b].F.Occupied().Select(o => o.Item2.Id).Distinct().ToArray();
            foreach (string id in tpMine) if (tpEnemyIds.Contains(id)) tpCollisions++;

            for (int seed = 0; seed < TpSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(tpBuilds[b].F, tpStages[w].Enemy, seed, verbose: false);
                tpBattlesRun++;
                gLoop += r.TurnLoopCalls; gEncore += r.EncoreFired;
                foreach (UnitTally t0 in r.TallyByUnit.Values) gTurnsAll += t0.TurnsTaken;
                gDmgAll += r.TurnDmgAll; gDmgIn += r.TurnDmgIn; gDmgOff += r.TurnDmgOff; gDmgNone += r.TurnDmgNone;
                gHealAll += r.TurnHealAll; gHealIn += r.TurnHealIn; gHealOff += r.TurnHealOff; gHealNone += r.TurnHealNone;
                gStatAll += r.TurnStatusAll; gStatIn += r.TurnStatusIn; gStatOff += r.TurnStatusOff; gStatNone += r.TurnStatusNone;

                foreach (string id in tpMine)
                {
                    if (tpEnemyIds.Contains(id)) continue;
                    long[] a = TpGet(wband, id);
                    a[TpBattles]++; a[TpSettle] += r.Turns;
                    if (!r.TallyByUnit.TryGetValue(id, out UnitTally? t)) continue;
                    a[TpTurns] += t.TurnsTaken; a[TpAtkT] += t.TurnAttacks;
                    a[TpSkill] += t.TurnSkills; a[TpCharge] += t.TurnCharges; a[TpStall] += t.TurnStalls;
                    a[TpStun] += t.StallStun; a[TpSlumber] += t.StallSlumber;
                    a[TpImmobile] += t.StallImmobile; a[TpCanAct] += t.StallCanAct;
                    a[TpSold] += t.TurnsSurrendered;
                    a[TpDmgIn] += t.DmgOutInTurn; a[TpDmgOff] += t.DmgOutOffTurn;
                    a[TpHealIn] += t.HealOutInTurn; a[TpHealOff] += t.HealOutOffTurn;
                    a[TpStatIn] += t.StatusOutInTurn; a[TpStatOff] += t.StatusOutOffTurn;
                    a[TpAttacks] += t.Attacks; a[TpDmgEnemy] += t.DamageToEnemy;
                    a[TpEncore] += t.EncoreFires;
                }
            }
        }
    }
    tpSw.Stop();

    double TpPer(long[] a, int i) => a[TpBattles] == 0 ? 0 : (double)a[i] / a[TpBattles];
    double TpUsed(long[] a) => a[TpAtkT] + a[TpSkill] + a[TpCharge];
    // 稼働率 ＝ **使えた手番** ÷ 決着ターン数（潰れた手番は稼働ではない）。
    double TpUtil(long[] a) => a[TpSettle] == 0 ? 0 : TpUsed(a) / a[TpSettle];
    // 手番外の割合。分母が 0 なら NaN（「その通貨を1点も出していない」）。
    double TpOff(long[] a, int inI, int offI)
    {
        double d = a[inI] + a[offI];
        return d == 0 ? double.NaN : a[offI] / d;
    }
    string TpPct(double v) => double.IsNaN(v) ? "—" : $"{v * 100:0.0}%";

    var tpIds = tpAcc[0].Keys.OrderBy(k => k).ToArray();
    string TpNm(string id) => tpName.TryGetValue(id, out string? n) ? n : id;

    // 分類（§1-3 の線: 手番外の割合が 50% を超えたら手番外型）。
    // **通貨ごとに割れる駒は「混合」**。分母が 0 の通貨は判定に使わない。
    (string Kind, string Detail) TpClass(long[] a)
    {
        var lab = new[] { "与ダメ", "回復", "状態" };
        var offs = new[] { TpOff(a, TpDmgIn, TpDmgOff), TpOff(a, TpHealIn, TpHealOff), TpOff(a, TpStatIn, TpStatOff) };
        var live = Enumerable.Range(0, 3).Where(i => !double.IsNaN(offs[i])).ToArray();
        if (live.Length == 0) return ("出力なし", "3通貨とも 0");
        string detail = string.Join(" / ", live.Select(i => $"{lab[i]} {TpPct(offs[i])}"));
        bool anyOff = live.Any(i => offs[i] > 0.5), anyIn = live.Any(i => offs[i] <= 0.5);
        return (anyOff && anyIn ? "混合" : anyOff ? "手番外型" : "手番型", detail);
    }

    void TpTableB()
    {
        Console.WriteLine("## 表B. 駒ごとの手番（第2〜5波・73行 × 4波 × seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("`手番/戦` は**回ってきた**手番（再行動を含む）。`使/戦` はそのうち使えた手番");
        Console.WriteLine("（通常攻撃 ＋ 術 ＋ 溜め）。`稼働` ＝ `使/戦` ÷ 決着T。");
        Console.WriteLine("`内ダメ/手番` は §1-2 の「1手番あたりの出力」＝**手番内の与ダメ ÷ 回ってきた手番**、");
        Console.WriteLine("`外ダメ/戦` は**手番を1つも使わずに出した与ダメ**。`外%` は手番外の割合。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 在席戦 | 手番/戦 | 使/戦 | 稼働 | 攻 | 術 | 溜 | 潰 | 売 | 再行動 | 内ダメ/手番 | 外ダメ/戦 | 打点/振 | 与ダメ外% | 回復外% | 状態外% |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        // 並びは §1-2 の「1手番あたりの出力」＝**手番内の与ダメ ÷ 手番数**の降順。
        foreach (string id in tpIds.OrderByDescending(k =>
                 {
                     long[] a = tpAcc[0][k];
                     return a[TpTurns] == 0 ? -1 : (double)a[TpDmgIn] / a[TpTurns];
                 }))
        {
            long[] a = tpAcc[0][id];
            double used = TpUsed(a);
            double perTurn = a[TpTurns] == 0 ? 0 : (double)a[TpDmgIn] / a[TpTurns];
            double offPer = a[TpBattles] == 0 ? 0 : (double)a[TpDmgOff] / a[TpBattles];
            double perSwing = a[TpAttacks] == 0 ? 0 : (double)a[TpDmgEnemy] / a[TpAttacks];
            Console.WriteLine($"| {TpNm(id)} | {a[TpBattles]} | {TpPer(a, TpTurns):0.00} | {used / Math.Max(1, a[TpBattles]):0.00} "
                + $"| {TpUtil(a):0.00} | {TpPer(a, TpAtkT):0.00} | {TpPer(a, TpSkill):0.00} | {TpPer(a, TpCharge):0.00} "
                + $"| {TpPer(a, TpStall):0.00} | {TpPer(a, TpSold):0.00} | {TpPer(a, TpEncore):0.00} "
                + $"| {perTurn:0.0} | {offPer:0.0} | {perSwing:0.0} "
                + $"| {TpPct(TpOff(a, TpDmgIn, TpDmgOff))} | {TpPct(TpOff(a, TpHealIn, TpHealOff))} | {TpPct(TpOff(a, TpStatIn, TpStatOff))} |");
        }
        Console.WriteLine();
    }

    if (tpMode == "run")
    {
        Console.WriteLine("# 手番の値段 —— 駒ごとの表（第105期 `tempo run`）");
        Console.WriteLine();
        Console.WriteLine($"73行（`compare` {tpCompare.Length} ＋ 交差帯 {tpCross.Length}） × 5波 × seed 0..{TpSeeds - 1} = **{tpBattlesRun:#,0} 戦**（{tpSw.Elapsed.TotalSeconds:0.0} 秒）。");
        Console.WriteLine();
        TpTableB();
        return;
    }

    if (tpMode == "check")
    {
        Console.WriteLine("# 手番の値段 —— 自己検査（第105期 `tempo check`）");
        Console.WriteLine();
        Console.WriteLine($"分母: **{tpBattlesRun:#,0} 戦**（{tpSw.Elapsed.TotalSeconds:0.0} 秒）。");
        Console.WriteLine();
        Console.WriteLine("| 検査 | 内容 | 実測 | 判定 |");
        Console.WriteLine("|---|---|---|:-:|");
        Console.WriteLine("| (a) | `compare` 305 セルが `docs/balance.md` と 0 件 | **別コマンド**（`0 compare` の diff） | — |");
        // (b) 版に依らないこと。**盤面が動かない台**で見る（第91期 (e) の直し方）——
        // 刻み手（キリ・ノミ）を含まない行では再行動が原理的に発火せず、ソムはどの行にも居ないので、
        // `EncoreRule` / `BetrayRule` を切っても盤面は1ビットも動かない。そこで計数が一致すること。
        {
            var tpQuiet = tpBuilds.Where(x => !x.F.Occupied().Any(o =>
                o.Item2.Id == "kiri" || o.Item2.Id == "nomi" || o.Item2.Id == "som")).ToArray();
            long[] v0 = new long[TpN], v1 = new long[TpN];
            long l0 = 0, l1 = 0;
            int nRow = Math.Min(8, tpQuiet.Length), nSeed = 20;
            for (int w = 0; w < tpStages.Count; w++)
                for (int b2 = 0; b2 < nRow; b2++)
                    for (int seed = 0; seed < nSeed; seed++)
                        for (int ver = 0; ver < 2; ver++)
                        {
                            BattleResult rr = ver == 0
                                ? BattleEngine.Run(tpQuiet[b2].F, tpStages[w].Enemy, seed, verbose: false)
                                : BattleEngine.Run(tpQuiet[b2].F, tpStages[w].Enemy, seed, verbose: false,
                                                   betray: new BetrayRule(false, false),
                                                   encore: new EncoreRule(false));
                            long[] v = ver == 0 ? v0 : v1;
                            if (ver == 0) l0 += rr.TurnLoopCalls; else l1 += rr.TurnLoopCalls;
                            foreach (UnitTally t in rr.TallyByUnit.Values)
                            {
                                v[TpTurns] += t.TurnsTaken; v[TpAtkT] += t.TurnAttacks;
                                v[TpSkill] += t.TurnSkills; v[TpCharge] += t.TurnCharges;
                                v[TpStall] += t.TurnStalls; v[TpStun] += t.StallStun;
                                v[TpSlumber] += t.StallSlumber; v[TpImmobile] += t.StallImmobile;
                                v[TpCanAct] += t.StallCanAct; v[TpSold] += t.TurnsSurrendered;
                                v[TpDmgIn] += t.DmgOutInTurn; v[TpDmgOff] += t.DmgOutOffTurn;
                                v[TpHealIn] += t.HealOutInTurn; v[TpHealOff] += t.HealOutOffTurn;
                                v[TpStatIn] += t.StatusOutInTurn; v[TpStatOff] += t.StatusOutOffTurn;
                            }
                        }
            int diff = Enumerable.Range(0, TpN).Count(i => v0[i] != v1[i]) + (l0 == l1 ? 0 : 1);
            Console.WriteLine($"| (b) | 足した計数が版に依らない（刻み手を含まない {nRow} 行 × 5波 × {nSeed} seed で "
                + $"`EncoreRule` / `BetrayRule` を切る） | 食い違った列 **{diff}** 件（延べ手番 {v0[TpTurns]:#,0}） | {(diff == 0 ? "○" : "×")} |");
        }
        bool okC = gTurnsAll == gLoop + gEncore;
        Console.WriteLine($"| (c) | 手番数の合計 ＝ 行動順ループ ＋ 再行動 | {gTurnsAll:#,0} ＝ {gLoop:#,0} ＋ {gEncore:#,0} = {gLoop + gEncore:#,0} | {(okC ? "○" : "×")} |");
        bool okD1 = gDmgIn + gDmgOff + gDmgNone == gDmgAll;
        bool okD2 = gHealIn + gHealOff + gHealNone == gHealAll;
        bool okD3 = gStatIn + gStatOff + gStatNone == gStatAll;
        Console.WriteLine($"| (d) 与ダメ | 中 ＋ 外 ＋ 誰のものでもない ＝ 全部 | {gDmgIn:#,0} ＋ {gDmgOff:#,0} ＋ {gDmgNone:#,0} = {gDmgAll:#,0} | {(okD1 ? "○" : "×")} |");
        Console.WriteLine($"| (d) 回復 | 同上 | {gHealIn:#,0} ＋ {gHealOff:#,0} ＋ {gHealNone:#,0} = {gHealAll:#,0} | {(okD2 ? "○" : "×")} |");
        Console.WriteLine($"| (d) 状態 | 同上（回数） | {gStatIn:#,0} ＋ {gStatOff:#,0} ＋ {gStatNone:#,0} = {gStatAll:#,0} | {(okD3 ? "○" : "×")} |");
        Console.WriteLine("| (e) | `docs/` 10ファイルの差分 | **別コマンド** | — |");
        Console.WriteLine("| (f) | `ctx.PickOne` の箇所数 | **別コマンド**（grep） | — |");
        Console.WriteLine($"| 追加 | 味方と敵で `Def.Id` が衝突した件数 | {tpCollisions} | {(tpCollisions == 0 ? "○" : "×")} |");
        Console.WriteLine();
        Console.WriteLine("## 誰のものでもない出力の割合");
        Console.WriteLine();
        Console.WriteLine("| 通貨 | 手番の中 | 手番の外 | 誰のものでもない |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine($"| 与ダメージ | {gDmgIn * 100.0 / Math.Max(1, gDmgAll):0.0}% | {gDmgOff * 100.0 / Math.Max(1, gDmgAll):0.0}% | {gDmgNone * 100.0 / Math.Max(1, gDmgAll):0.0}% |");
        Console.WriteLine($"| 回復 | {gHealIn * 100.0 / Math.Max(1, gHealAll):0.0}% | {gHealOff * 100.0 / Math.Max(1, gHealAll):0.0}% | {gHealNone * 100.0 / Math.Max(1, gHealAll):0.0}% |");
        Console.WriteLine($"| 状態異常（回数） | {gStatIn * 100.0 / Math.Max(1, gStatAll):0.0}% | {gStatOff * 100.0 / Math.Max(1, gStatAll):0.0}% | {gStatNone * 100.0 / Math.Max(1, gStatAll):0.0}% |");
        Console.WriteLine();
        return;
    }

    // ---------------------------------------------------------------------------------
    // 表A〜D
    // ---------------------------------------------------------------------------------
    Console.WriteLine("# 手番の値段（第105期 `tempo tables`）");
    Console.WriteLine();
    Console.WriteLine($"73行（`compare` {tpCompare.Length} ＋ 交差帯 {tpCross.Length}） × 5波 × seed 0..{TpSeeds - 1} = **{tpBattlesRun:#,0} 戦**（{tpSw.Elapsed.TotalSeconds:0.0} 秒）。");
    Console.WriteLine("**判定の分母は第2〜5波**（規約 (G10)）。第一波は参考として併記する。");
    Console.WriteLine();

    Console.WriteLine("## 表A. Q1 —— 既知の値の再現");
    Console.WriteLine();
    long[] TpA(string id) => tpAcc[0].TryGetValue(id, out long[]? a) ? a : new long[TpN];
    long[] aNomi = TpA("nomi"), aKiri = TpA("kiri"), aHagi = TpA("hagi"), aKado = TpA("kado");
    long[] aNono = TpA("nono"), aHari = TpA("hari");

    double nomiSwing = aNomi[TpAttacks] == 0 ? 0 : (double)aNomi[TpDmgEnemy] / aNomi[TpAttacks];
    double kiriSwing = aKiri[TpAttacks] == 0 ? 0 : (double)aKiri[TpDmgEnemy] / aKiri[TpAttacks];
    double hagiUsed = TpUsed(aHagi);
    bool q1a = nomiSwing >= 10 && nomiSwing <= 21;
    bool q1b = Math.Abs(kiriSwing - 1.0) < 0.005;
    bool q1c = hagiUsed == 0;
    bool q1d = aKado[TpTurns] > 0 && aKado[TpStall] == aKado[TpTurns] && aKado[TpImmobile] > 0;
    double utilNono = TpUtil(aNono), utilHari = TpUtil(aHari);
    bool q1e = utilNono >= 0.35 && utilNono <= 0.40 && utilHari >= 0.35 && utilHari <= 0.40;

    Console.WriteLine("| # | 既知の値 | 出どころ | 実測 | 判定 |");
    Console.WriteLine("|---|---|---|--:|:-:|");
    Console.WriteLine($"| 1 | ノミの打点/振が 10〜21 | 第104期 | **{nomiSwing:0.0}** | {(q1a ? "○" : "×")} |");
    Console.WriteLine($"| 2 | キリの打点/振が常に 1 | `ThinBlade` | **{kiriSwing:0.00}** | {(q1b ? "○" : "×")} |");
    Console.WriteLine($"| 3 | ハギの手番数が 0 | `CanAct => false` | 使えた手番 **{hagiUsed / Math.Max(1, aHagi[TpBattles]):0.00}**/戦（回ってきた手番は {TpPer(aHagi, TpTurns):0.00}） | {(q1c ? "○" : "×")} |");
    Console.WriteLine($"| 4 | カドの手番が全部潰れる | `Immobile` | 潰れ **{aKado[TpStall]:#,0}** / 手番 **{aKado[TpTurns]:#,0}**（うち不動 {aKado[TpImmobile]:#,0}・痺れ {aKado[TpStun]:#,0}） | {(q1d ? "○" : "×")} |");
    Console.WriteLine($"| 5 | 稼働率が 35〜40% 帯 | 第86期（分子はノノ・ハリ） | ノノ **{utilNono * 100:0.0}%** / ハリ **{utilHari * 100:0.0}%** | {(q1e ? "○" : "×")} |");
    Console.WriteLine();
    Console.WriteLine("### 稼働率の分布（ロスター全体・第2〜5波）");
    Console.WriteLine();
    var tpUtils = tpIds.Select(k => TpUtil(tpAcc[0][k])).Where(v => v > 0).OrderBy(v => v).ToArray();
    if (tpUtils.Length > 0)
    {
        double med = tpUtils[tpUtils.Length / 2];
        int inBand = tpUtils.Count(v => v >= 0.35 && v <= 0.40);
        Console.WriteLine($"最小 {tpUtils[0] * 100:0.0}% ／ 中央 {med * 100:0.0}% ／ 最大 {tpUtils[^1] * 100:0.0}%。");
        Console.WriteLine($"**35〜40% に入る駒は {inBand} / {tpUtils.Length} 枚。**");
    }
    Console.WriteLine();

    TpTableB();

    Console.WriteLine("## 表C. 分類（手番型 / 手番外型 / 混合）");
    Console.WriteLine();
    Console.WriteLine("線は §1-3 のとおり **手番外の割合 50%**。**通貨ごとに割れる駒は「混合」**。");
    Console.WriteLine();
    var tpKinds = tpIds.ToDictionary(k => k, k => TpClass(tpAcc[0][k]));
    foreach (string kind in new[] { "手番外型", "混合", "手番型", "出力なし" })
    {
        var members = tpIds.Where(k => tpKinds[k].Kind == kind).ToArray();
        Console.WriteLine($"### {kind}（{members.Length} 枚）");
        Console.WriteLine();
        if (members.Length == 0) { Console.WriteLine("（0 枚）"); Console.WriteLine(); continue; }
        Console.WriteLine("| 駒 | 内訳（手番外の割合） |");
        Console.WriteLine("|---|---|");
        foreach (string k in members) Console.WriteLine($"| {TpNm(k)} | {tpKinds[k].Detail} |");
        Console.WriteLine();
    }

    Console.WriteLine("### Q2 —— 両端");
    Console.WriteLine();
    double kadoOff = TpOff(aKado, TpDmgIn, TpDmgOff);
    long[] aDolga = TpA("dolga");
    double dolgaOff = TpOff(aDolga, TpDmgIn, TpDmgOff);
    bool q2 = kadoOff == 1.0 && dolgaOff == 0.0;
    Console.WriteLine($"- カド（`Immobile`）の与ダメの手番外の割合: **{TpPct(kadoOff)}**（期待 100%）");
    Console.WriteLine($"- ドルガ（素の通常攻撃のみ）: **{TpPct(dolgaOff)}**（期待 0%）");
    Console.WriteLine($"- **判定: {(q2 ? "○" : "×")}**");
    Console.WriteLine();

    Console.WriteLine("### Q3 —— 第103期の順位を説明するか");
    Console.WriteLine();
    Console.WriteLine("第103期は「餌の代金は手番。**上位3枚は手番で殴らない駒**（ハギ・カド・ヒヨ）」だった。");
    Console.WriteLine();
    Console.WriteLine("| 駒 | 第103期の Δ相乗の順位 | この器具の分類 | 期待 | 判定 |");
    Console.WriteLine("|---|--:|---|---|:-:|");
    var q3Rows = new (string Id, string Rank, string Want)[]
    {
        ("hagi", "1 / 51", "手番外型"), ("kado", "2 / 51", "手番外型"), ("hiyo", "3 / 51", "手番外型"),
        ("borg", "32 / 51", "手番型"), ("dolga", "46 / 51", "手番型"),
    };
    int q3ok = 0;
    foreach ((string id, string rank, string want) in q3Rows)
    {
        string got = tpKinds.TryGetValue(id, out var kk) ? kk.Kind : "（在席なし）";
        bool ok = got == want;
        if (ok) q3ok++;
        Console.WriteLine($"| {TpNm(id)} | {rank} | **{got}** | {want} | {(ok ? "○" : "×")} |");
    }
    Console.WriteLine();
    Console.WriteLine($"**{q3ok} / {q3Rows.Length} 一致。**");
    Console.WriteLine();

    Console.WriteLine("## 表D. 手番の内訳の分布と §1-4 の数え直し");
    Console.WriteLine();
    long sTurns = tpIds.Sum(k => tpAcc[0][k][TpTurns]);
    long sAtk = tpIds.Sum(k => tpAcc[0][k][TpAtkT]), sSk = tpIds.Sum(k => tpAcc[0][k][TpSkill]);
    long sCh = tpIds.Sum(k => tpAcc[0][k][TpCharge]), sSt = tpIds.Sum(k => tpAcc[0][k][TpStall]);
    long sSold = tpIds.Sum(k => tpAcc[0][k][TpSold]);
    long sStun = tpIds.Sum(k => tpAcc[0][k][TpStun]), sSlm = tpIds.Sum(k => tpAcc[0][k][TpSlumber]);
    long sImm = tpIds.Sum(k => tpAcc[0][k][TpImmobile]), sCan = tpIds.Sum(k => tpAcc[0][k][TpCanAct]);
    Console.WriteLine("| 内訳 | 延べ | 割合 |");
    Console.WriteLine("|---|--:|--:|");
    Console.WriteLine($"| 通常攻撃 | {sAtk:#,0} | {sAtk * 100.0 / Math.Max(1, sTurns):0.0}% |");
    Console.WriteLine($"| 術 | {sSk:#,0} | {sSk * 100.0 / Math.Max(1, sTurns):0.0}% |");
    Console.WriteLine($"| 溜め | {sCh:#,0} | {sCh * 100.0 / Math.Max(1, sTurns):0.0}% |");
    Console.WriteLine($"| **潰れた** | {sSt:#,0} | {sSt * 100.0 / Math.Max(1, sTurns):0.0}% |");
    Console.WriteLine($"| （うち痺れ） | {sStun:#,0} | 潰れの {sStun * 100.0 / Math.Max(1, sSt):0.0}% |");
    Console.WriteLine($"| （うちまどろみ） | {sSlm:#,0} | 潰れの {sSlm * 100.0 / Math.Max(1, sSt):0.0}% |");
    Console.WriteLine($"| （うち不動） | {sImm:#,0} | 潰れの {sImm * 100.0 / Math.Max(1, sSt):0.0}% |");
    Console.WriteLine($"| （うちその他の `CanAct` 偽） | {sCan:#,0} | 潰れの {sCan * 100.0 / Math.Max(1, sSt):0.0}% |");
    Console.WriteLine($"| **売れた手番** | {sSold:#,0} | 潰れの {sSold * 100.0 / Math.Max(1, sSt):0.0}% |");
    Console.WriteLine();

    var tpAll2 = UnitCatalog.All;
    var tpNever = tpIds.Where(k => TpUsed(tpAcc[0][k]) == 0).Select(TpNm).ToArray();
    var tpAllStall = tpIds.Where(k => tpAcc[0][k][TpTurns] > 0 && tpAcc[0][k][TpStall] == tpAcc[0][k][TpTurns]).Select(TpNm).ToArray();
    var tpSkillDefs = tpAll2.Where(d => d.Actions is { Count: > 0 } && d.Actions.All(a => a.Kind == ActionKind.Skill)).ToArray();
    var tpPlain = tpIds.Where(k =>
    {
        long[] a = tpAcc[0][k];
        return a[TpAtkT] > 0 && a[TpSkill] == 0 && a[TpCharge] == 0;
    }).ToArray();
    Console.WriteLine($"- **手番を1度も使えない駒**: {tpNever.Length} 枚 — {(tpNever.Length == 0 ? "—" : string.Join(" / ", tpNever))}");
    Console.WriteLine($"- **手番の全部が潰れる駒**: {tpAllStall.Length} 枚 — {(tpAllStall.Length == 0 ? "—" : string.Join(" / ", tpAllStall))}");
    Console.WriteLine($"- **術しか撃たない駒**（定義から）: {tpSkillDefs.Length} 枚 — {string.Join(" / ", tpSkillDefs.Select(d => d.Name))}");
    Console.WriteLine($"- **実測で通常攻撃しか振らない駒**: {tpPlain.Length} / {tpIds.Length} 枚（在席した駒が分母）");
    Console.WriteLine($"- 73行に在席した駒: **{tpIds.Length} / {tpAll2.Count} 枚**");
    Console.WriteLine();

    Console.WriteLine("## 参考. 第一波（**判定に使わない**・規約 (G10)）");
    Console.WriteLine();
    long w1Turns = tpAcc[1].Values.Sum(a => a[TpTurns]), w1St = tpAcc[1].Values.Sum(a => a[TpStall]);
    Console.WriteLine($"延べ手番 {w1Turns:#,0}・潰れた手番 {w1St:#,0}（{w1St * 100.0 / Math.Max(1, w1Turns):0.0}%）。");
    Console.WriteLine($"第2〜5波の潰れ率は {sSt * 100.0 / Math.Max(1, sTurns):0.0}%。");
    Console.WriteLine();
    return;
}
}
