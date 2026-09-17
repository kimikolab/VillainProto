using BattleCore;
using static Common;

// =====================================================================================
// scapegoat モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "scapegoat")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 scapegoat
// =====================================================================================

static class ScapegoatDiag
{
// scapegoat モード: 業（第49期）。**味方が背負っている状態異常の「種類数」を読む駒。**
//
// ロスターの10通貨はすべて「同じ通貨を厚くする」方向を向いていて（第48期の棚卸し）、
// **幅を要求する駒が1枚もない。** 業（ゴウ）はそこを埋める——量ではなく種類を読む。
//
// `CompareBuilds()` / `Stages` / `Columns` は触らない（phase0 は BattleCore にも触らない）。
//
//     dotnet run --project BattleSim -c Release 0 scapegoat phase0   # 実装前の地図（§2 Phase 0）
public static void Phase0(string[] args, int stageIndex)
{
    var sgBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> sgStages = EnemyCatalog.Stages;
    const int SgScan = 50;    // 50 行の走査（verbose=true なので compare の 200 とは分ける）
    const int SgSeeds = 200;  // 候補台の測り直し（compare / scale と揃える）

    // 数える対象の4種類。**Armor と IdleTurn は数えない**（§2-2。判断の根拠は本文）。
    (string Key, string Label)[] sgKinds =
    {
        (StatusKeys.Poison, "毒"),
        (StatusKeys.Marked, "標"),
        (StatusKeys.Stun,   "痺"),
        (StatusKeys.Burn,   "燃"),
        (StatusKeys.Wound,  "傷"),   // 味方に載る経路が本当に無いかを実測で確かめるために入れる
    };

    // `StatusSnapshot` の Text（`BattleEngine.StatusLabels`）→ 数える対象の索引。
    // **盤面には触らない。** verbose=true の Events を読み直すだけ。
    var sgLabelOf = new Dictionary<string, int>();
    for (int k = 0; k < sgKinds.Length; k++) sgLabelOf[sgKinds[k].Label] = k;

    // 1戦ぶんの走査。ターン頭のスナップショット（TickStatuses の直後）から
    // 「そのターン、味方の誰かに載っていた種類」を集める。
    //
    // **これは下限。** スナップショットはターン頭に1回だけ写されるので、同じターンの
    // OnTurnStart で撒かれるぶん（瘴気の味方漏れ・縛めの味方縛り）は次のターンまで出ない。
    (int[] TurnsWith, int Turns, int[] First, int Kinds2, int Kinds3, int Kinds4,
     int First3, Dictionary<string, int>[] Carriers, int[] Runs, int[] RunTurns,
     int CumKinds, int CumFirst2, int CumFirst3, int CumFirst4)
    SgTrace(Formation f, Formation enemy, int seed)
    {
        BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: true);

        // 味方の InstanceId。編成の駒は 0..N-1（Run が味方 → 敵の順で Add する）。
        // 増援（胞子）は Summon イベントの Team で拾う。
        var mine = new HashSet<int>(Enumerable.Range(0, f.Occupied().Count()));
        var nameOf = new Dictionary<int, string>();
        {
            int i = 0;
            foreach ((int _, UnitDef d) in f.Occupied()) nameOf[i++] = d.Name;
        }
        foreach (BattleEvent e in r.Events)
            if (e.Kind == BattleEventKind.Summon && e.Team == BattleContext.PlayerTeam
                && e.TargetId is int sid)
            {
                mine.Add(sid);
                nameOf[sid] = e.Text ?? "増援";
            }

        int kinds = sgKinds.Length;
        var turnsWith = new int[kinds];
        var first = new int[kinds];
        var carriers = new Dictionary<string, int>[kinds];
        for (int k = 0; k < kinds; k++) carriers[k] = new Dictionary<string, int>();
        var runs = new int[kinds];       // 「載り始めた」回数（駒ごとの連続区間の数）
        var runTurns = new int[kinds];   // その区間の総ターン数
        var wasOn = new Dictionary<(int Unit, int Kind), bool>();

        int turns = 0, k2 = 0, k3 = 0, k4 = 0, first3 = 0;
        // **累積**（この戦闘で一度でも盤面に出た種類）。業は引き取った種類を持ち越すので、
        // 同時成立ではなくこちらが到達の分母になる——毒と標は減らないから、
        // 一度引き取れば戦闘が終わるまでゴウの種類数に載り続ける。
        var everOn = new bool[kinds];
        int cumFirst2 = 0, cumFirst3 = 0, cumFirst4 = 0;
        int curTurn = -1;
        var onThisTurn = new bool[kinds];
        var seenThisTurn = new HashSet<(int, int)>();

        void Close()
        {
            if (curTurn < 0) return;
            turns++;
            int n = 0;
            for (int k = 0; k < kinds; k++)
                if (onThisTurn[k]) { n++; turnsWith[k]++; if (first[k] == 0) first[k] = curTurn; }
            if (n >= 2) k2++;
            if (n >= 3) { k3++; if (first3 == 0) first3 = curTurn; }
            if (n >= 4) k4++;
            for (int k = 0; k < kinds; k++) if (onThisTurn[k]) everOn[k] = true;
            int cum = everOn.Count(x => x);
            if (cum >= 2 && cumFirst2 == 0) cumFirst2 = curTurn;
            if (cum >= 3 && cumFirst3 == 0) cumFirst3 = curTurn;
            if (cum >= 4 && cumFirst4 == 0) cumFirst4 = curTurn;
            // 連続区間の更新（このターン載っていなかった (駒,種) は区間を閉じる）
            foreach (var key in wasOn.Keys.ToList())
                if (!seenThisTurn.Contains(key)) wasOn[key] = false;
            Array.Clear(onThisTurn, 0, onThisTurn.Length);
            seenThisTurn.Clear();
        }

        foreach (BattleEvent e in r.Events)
        {
            if (e.Kind == BattleEventKind.TurnStart)
            {
                Close();
                curTurn = e.Turn;
                continue;
            }
            if (e.Kind != BattleEventKind.StatusSnapshot) continue;
            if (e.TargetId is not int uid || !mine.Contains(uid)) continue;
            if (e.Text is null || !sgLabelOf.TryGetValue(e.Text, out int kk)) continue;
            if (e.Amount <= 0) continue;

            onThisTurn[kk] = true;
            seenThisTurn.Add((uid, kk));
            string nm = nameOf.TryGetValue(uid, out string? s) ? s : $"#{uid}";
            carriers[kk][nm] = carriers[kk].TryGetValue(nm, out int c) ? c + 1 : 1;

            runTurns[kk]++;
            if (!wasOn.TryGetValue((uid, kk), out bool on) || !on) { runs[kk]++; wasOn[(uid, kk)] = true; }
        }
        Close();

        return (turnsWith, turns, first, k2, k3, k4, first3, carriers, runs, runTurns,
                everOn.Count(x => x), cumFirst2, cumFirst3, cumFirst4);
    }

    Console.WriteLine("# 業 Phase 0 —— 数える対象の確定（第49期 §2）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 scapegoat phase0` の出力。");
    Console.WriteLine("**盤面は1つも動かない。** `BattleCore` には1文字も足していない状態で走らせる測定で、");
    Console.WriteLine("`docs/` には置かない。");
    Console.WriteLine();
    Console.WriteLine("> **実測はすべて下限。** 種類の在庫は `StatusSnapshot`（`TickStatuses` の直後・");
    Console.WriteLine("> ターン頭に1回だけ）から読んでいるので、**同じターンの `OnTurnStart` で撒かれるぶん**");
    Console.WriteLine("> （瘴気の味方漏れ・縛めの味方縛り）は次のターンの頭まで出てこない。");
    Console.WriteLine();

    // --- 0-1. 味方に載る状態異常の経路 -------------------------------------------------------
    Console.WriteLine("## 0-1. 味方に載る状態異常の経路（`SetCounter` / `ctx.Ignite` の全呼び出しを走査）");
    Console.WriteLine();
    Console.WriteLine("`BattleCore/Traits.cs` と `BattleCore/BattleEngine.cs` の書き込み箇所を全部拾い、");
    Console.WriteLine("**対象が味方（`ally` / `self` / `victim` / 味方チームの駒）になっているものだけ**を抜き出した。");
    Console.WriteLine();
    Console.WriteLine("| 種類 | 経路 | 保持者 | 対象 | 発火 |");
    Console.WriteLine("|---|---|---|---|---|");
    Console.WriteLine("| `Poison` | 瘴気の味方漏れ（`MiasmaTrait.OnTurnStart`） | 瘴気袋のグザ | **味方全員（自分も含む）** +1 | 毎ターン |");
    Console.WriteLine("| `Poison` | 毒撃の隣への漏れ（`VenomTrait.OnDamaged`） | 毒吐きのスィド | 隣接する味方 +1 | スィドが殴られるたび |");
    Console.WriteLine("| `Marked` | 囃し立て（`MarkerTrait.OnBattleStart`） | 囃し立てのヒサ | 隣接する最大HPの味方1体 = 1 | **開戦時1回だけ** |");
    Console.WriteLine("| `Stun` | 縛め・味方縛り（`BindTrait.OnTurnStart`） | 縛めのクグ | 味方1体（`AcceptsSupport` かつ 痺れていない）= 1 | 第2ターン以降 毎ターン |");
    Console.WriteLine("| `Stun` | 怯み（`AvengeTrait.OnDamaged`） | 仇討ちのザン | **自分** = 1 | 敵に殴られるたび |");
    Console.WriteLine("| `Stun` | 怖気（`TormentTrait.OnAfterAttack`） | 責め苦のシガ | **自分** = 1 | 動ける敵を殴るたび |");
    Console.WriteLine("| `Stun` | 深追い（`GougeTrait.OnKill`） | 抉りのエグ | **自分** = 1 | 敵を倒すたび |");
    Console.WriteLine("| `Stun` | 断罪（`CondemnTrait.OnDamaged`） | **敵**・勇者候補 / 審問官（第五波の2体） | 反撃してきた味方 = 1 | 反撃を浴びるたび・45% |");
    Console.WriteLine("| `Burn` | 火の粉（`CinderTrait.OnAfterAttack`） | 焼け残りのボルグ | 隣接する味方（`ctx.Ignite(ally, friendly: true)`） | ボルグが殴るたび |");
    Console.WriteLine("| `Wound` | **無い** | — | 裂き・刻み・断ち・縫いは全部 `target`（＝敵） | — |");
    Console.WriteLine();
    Console.WriteLine("**分母は 4**（`Poison` / `Marked` / `Stun` / `Burn`）。`Wound` に味方へ載る経路は1つも無い。");
    Console.WriteLine();
    Console.WriteLine($"### 実測（`CompareBuilds()` の {sgBuilds.Length} 行 × {sgStages.Count} 波 × seed 0..{SgScan - 1}）");
    Console.WriteLine();
    Console.WriteLine("`在庫` はその種類が味方の誰かに載っていたターンの割合（分母は全ターン）。");
    Console.WriteLine("`初出` は初めて載ったターン（載った試行だけの平均）。");
    Console.WriteLine();
    Console.WriteLine("| 種類 | 在庫のある行数 | 在庫率（全行の平均） | 初出 | 主な保持者（延べターン） |");
    Console.WriteLine("|---|--:|--:|--:|---|");
    {
        int kinds = sgKinds.Length;
        var rowsWith = new int[kinds];
        var rate = new double[kinds];
        var firstSum = new double[kinds];
        var firstN = new int[kinds];
        var carriersAll = new Dictionary<string, int>[kinds];
        for (int k = 0; k < kinds; k++) carriersAll[k] = new Dictionary<string, int>();
        var perRow = new List<(string Name, double[] Rate, double K2, double K3, double K4,
                              double Cum, double CumFirst3, double CumNever3)>();

        foreach (var b in sgBuilds)
        {
            var tw = new long[kinds];
            var fs = new double[kinds];
            var fn = new int[kinds];
            long tt = 0, c2 = 0, c3 = 0, c4 = 0;
            double cf3 = 0, cumSum = 0; int cf3n = 0, cumNever3 = 0, trials = 0;
            foreach (EnemyCatalog.Stage st in sgStages)
                for (int seed = 0; seed < SgScan; seed++)
                {
                    var z = SgTrace(b.F, st.Enemy, seed);
                    trials++;
                    tt += z.Turns; c2 += z.Kinds2; c3 += z.Kinds3; c4 += z.Kinds4;
                    cumSum += z.CumKinds;
                    if (z.CumFirst3 > 0) { cf3 += z.CumFirst3; cf3n++; } else cumNever3++;
                    for (int k = 0; k < kinds; k++)
                    {
                        tw[k] += z.TurnsWith[k];
                        if (z.First[k] > 0) { fs[k] += z.First[k]; fn[k]++; }
                        foreach ((string nm, int c) in z.Carriers[k])
                            carriersAll[k][nm] = carriersAll[k].TryGetValue(nm, out int x) ? x + c : c;
                    }
                }
            var rr = new double[kinds];
            for (int k = 0; k < kinds; k++)
            {
                rr[k] = tt == 0 ? 0 : tw[k] * 100.0 / tt;
                if (rr[k] > 0) rowsWith[k]++;
                rate[k] += rr[k];
                if (fn[k] > 0) { firstSum[k] += fs[k] / fn[k]; firstN[k]++; }
            }
            perRow.Add((b.Name, rr, tt == 0 ? 0 : c2 * 100.0 / tt, tt == 0 ? 0 : c3 * 100.0 / tt,
                        tt == 0 ? 0 : c4 * 100.0 / tt, cumSum / trials,
                        cf3n > 0 ? cf3 / cf3n : 0, cumNever3 * 100.0 / trials));
            Console.Out.Flush();
        }

        for (int k = 0; k < kinds; k++)
        {
            var top = carriersAll[k].OrderByDescending(x => x.Value).Take(4)
                .Select(x => $"{x.Key} {x.Value}").ToList();
            Console.WriteLine($"| {sgKinds[k].Label} (`{sgKinds[k].Key}`) | {rowsWith[k]} / {sgBuilds.Length} "
                + $"| {rate[k] / sgBuilds.Length:0.0}% "
                + $"| {(firstN[k] > 0 ? $"{firstSum[k] / firstN[k]:0.00}" : "—")} "
                + $"| {(top.Count == 0 ? "**0 件**" : string.Join(" / ", top))} |");
        }
        Console.WriteLine();

        Console.WriteLine("### 行ごとの在庫と同時成立（同じ走査）");
        Console.WriteLine();
        Console.WriteLine("`2種`/`3種`/`4種` は**同時に成立していたターンの割合**。`初3` は3種が初めて揃ったターン、");
        Console.WriteLine("`未3` は 3種が最後まで揃わなかった試行の割合。**閾値 3 の到達可能性はこの2列で決まる。**");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 毒 | 標 | 痺 | 燃 | 傷 | 2種 | 3種 | 4種 | **累種** | **累3** | **累3未達** |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var p in perRow)
            Console.WriteLine($"| {p.Name} " + string.Concat(p.Rate.Select(x => $"| {x:0.0}% "))
                + $"| {p.K2:0.0}% | {p.K3:0.0}% | {p.K4:0.0}% "
                + $"| **{p.Cum:0.00}** | {(p.CumFirst3 > 0 ? $"{p.CumFirst3:0.00}" : "—")} "
                + $"| {p.CumNever3:0.0}% |");
        Console.WriteLine();
        Console.WriteLine($"**3種が1ターンでも同時に揃う行: {perRow.Count(p => p.K3 > 0)} / {perRow.Count}**"
            + $"（4種は {perRow.Count(p => p.K4 > 0)} 行）");
        Console.WriteLine();
        Console.WriteLine($"**累積で3種に届く行: {perRow.Count(p => p.CumNever3 < 100)} / {perRow.Count}**"
            + $"。`累種` の全行平均は {perRow.Average(p => p.Cum):0.00} 種。");
        Console.WriteLine();
    }

    // --- 0-2. Armor / IdleTurn を数えない判断 ------------------------------------------------
    Console.WriteLine("## 0-2. `Armor` と `IdleTurn` を数える対象から外す判断（engine の実装を確認）");
    Console.WriteLine();
    Console.WriteLine("- **`Armor`**: `ApplyDamage` が HP の前に削るプールで、**プラスの資源**。");
    Console.WriteLine("  数えるとヒビ（砕け）1枚で種類数を稼げる抜け道になる。**数えない。**");
    Console.WriteLine("- **`IdleTurn`**: `BattleEngine.Run` の行動順ループが");
    Console.WriteLine("  `Stun > 0` → `SetCounter(Stun, 0)` / `SetCounter(IdleTurn, turn)` と");
    Console.WriteLine("  **痺れを IdleTurn へ振り替える**。数えると1経路で2カウントになる。**数えない。**");
    Console.WriteLine();
    Console.WriteLine("### 検算: `Stun` と `IdleTurn` は本当に同時に立つか");
    Console.WriteLine();
    Console.WriteLine("**立たない。** 痺れは保持者の手番が回ってきた瞬間に `0` へ消費され、");
    Console.WriteLine("同じ行で `IdleTurn` にターン番号が入る（振り替えであって併存ではない）。");
    Console.WriteLine("`IdleTurn` を書く箇所は engine の3つだけ（痺れ / まどろみ / `CanAct` 偽）で、");
    Console.WriteLine("**`0` に戻す箇所は1つも無い**——一度でも手番を落とせば以後ずっと非ゼロのままになる。");
    Console.WriteLine("種類として数えると「一度動けなかった駒は永久に1種類を持つ」ことになり、");
    Console.WriteLine("**指示書の指定（数えない）は engine の実装と整合している。**");
    Console.WriteLine();

    // --- 0-3. 寿命 --------------------------------------------------------------------------
    Console.WriteLine("## 0-3. 状態異常の寿命");
    Console.WriteLine();
    Console.WriteLine("| 種類 | 減り方 | 定数 | 実測の持続（連続ターン数） |");
    Console.WriteLine("|---|---|---|--:|");
    {
        int kinds = sgKinds.Length;
        var runs = new long[kinds];
        var runT = new long[kinds];
        foreach (var b in sgBuilds)
            foreach (EnemyCatalog.Stage st in sgStages)
                for (int seed = 0; seed < SgScan; seed++)
                {
                    var z = SgTrace(b.F, st.Enemy, seed);
                    for (int k = 0; k < kinds; k++) { runs[k] += z.Runs[k]; runT[k] += z.RunTurns[k]; }
                }
        string[] how =
        {
            "**減らない。** `TickStatuses` は層の分だけ削るが層は減らさない（累積する）",
            "**減らない。** 書き手（囃し立て）が開戦時に 1 を置くだけで、消す箇所が1つも無い",
            "保持者の**手番が来た瞬間**に 0 へ消費され `IdleTurn` に振り替わる",
            "`TickStatuses` が毎ターン 1 減らす（非スタック・再付与は残ターンのリセット）",
            "**減らない。** `TickStatuses` に何も足していない（読み手がいて初めて意味を持つ）"
        };
        string[] konst = { "—", "1（固定）", "1（固定）",
                           $"`BurnRules.Turns` = {BurnRules.Turns} / 1T {BurnRules.Damage} ダメージ", "—" };
        for (int k = 0; k < kinds; k++)
            Console.WriteLine($"| {sgKinds[k].Label} | {how[k]} | {konst[k]} "
                + $"| {(runs[k] > 0 ? $"{(double)runT[k] / runs[k]:0.00}" : "—")} |");
    }
    Console.WriteLine();
    Console.WriteLine("**痺の実測が 1 に近いほど要注意。** 痺れは1ターンしか盤面に残らないので、");
    Console.WriteLine("**引き取りに来るのが1ターン遅れると在庫が消えている。**");
    Console.WriteLine();

    // --- 0-4. 状態異常の肩代わりが存在しないこと ---------------------------------------------
    Console.WriteLine("## 0-4. 状態異常そのものを移す経路が既に無いことの確認");
    Console.WriteLine();
    Console.WriteLine("| 機構 | 何をするか | 「移す」か |");
    Console.WriteLine("|---|---|---|");
    Console.WriteLine("| 集約（`BearTrait`・ウケ） | 隣の味方の**弱体**（`AtkBonus` の減算）を横取りしてアーマーに変える | 弱体であって状態異常ではない |");
    Console.WriteLine("| 転嫁（`RelayTrait`・ワタ） | 同上を横取りして**敵**へ流す | 同上 |");
    Console.WriteLine("| 澱み喰い（`BlightfedTrait`・ヴィオ） | 味方の毒を `0` にして自分の攻撃力に変える | **消すだけ**（自分には積まない） |");
    Console.WriteLine("| 疫み（`ContagionTrait`・ラウ） | 倒れた駒の毒を**残りの敵**へ撒き直す | 死体からの撒き直しで、生きた味方からは取らない |");
    Console.WriteLine("| 毒喰らい（`DevourTrait`・ベニ） | 敵の毒の数だけ味方を癒す | 読むだけ |");
    Console.WriteLine();
    Console.WriteLine("**生きている味方から状態異常のカウンタを取り上げて自分に積む経路は 0 件。**");
    Console.WriteLine("業の引き取りはロスターで初めての「状態異常の肩代わり」になる。");
    Console.WriteLine();

    // --- 0-5 / 0-6 --------------------------------------------------------------------------
    Console.WriteLine("## 0-5. `docs/balance.md` の分母");
    Console.WriteLine();
    Console.WriteLine($"編成 **{sgBuilds.Length}** 行 × 波 **{sgStages.Count}** = **{sgBuilds.Length * sgStages.Count} セル**。");
    Console.WriteLine();
    Console.WriteLine("## 0-6. 残り枠");
    Console.WriteLine();
    Console.WriteLine($"`UnitCatalog.All` は **{UnitCatalog.All.Count}** 体。上限 52 に対して残り "
        + $"**{52 - UnitCatalog.All.Count}**。この期で1枚使えば残り {52 - UnitCatalog.All.Count - 1} になる");
    Console.WriteLine("——**実際には業を採用しなかったので枠は減っていない**（`UnitCatalog.Gou` は");
    Console.WriteLine("`All` に載せず、定義だけを対照として残してある。逆位・まどろみ・誹り・驕りと同じ扱い）。");
    Console.WriteLine();

    // --- 0-7. 候補台の地図 -------------------------------------------------------------------
    // **`CompareBuilds()` は触らない。** 候補はここでローカルに組む（`gradient` / `aim` と同じ扱い）。
    // 5枠目には**業と同数値・特性なしの素体**を置く。これが §4 の陽性対照そのものになる。
    UnitDef SgPlain = new()
    {
        Id = "gou_plain", Name = "素体のゴウ", MaxHp = 88, Attack = 7, Speed = 4,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single
    };
    var sgCand = new (string Name, Formation F)[]
    {
        // --- 供給を3種類そろえた側（払い出しの枚数を変えながら測る）---
        ("A4 毒標燃 (グザ×ヒサ×ボルグ×ガルド)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Borg, center: SgPlain,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Guza)),
        ("A6 毒標痺燃 (グザ×ヒサ×ザン×ボルグ)", Formation.Build(
            front1: UnitCatalog.Zan, front3: UnitCatalog.Borg, center: SgPlain,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Guza)),
        ("A26 標痺燃 (カド×ヒサ×シガ×ボルグ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Shiga)),
        // --- 反撃台（カド×ヒサ×ボルグ）を固定し、5枚目だけを振る ---
        // **痺は使えない種類**（引き取ると手番が飛んで転写できない）ので、
        // 閾値 3 に届くには 毒・標・燃 の3つが要る。
        ("D1 毒標燃 (カド×ヒサ×ボルグ×グザ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Guza)),
        ("D2 毒標燃 (カド×ヒサ×ボルグ×スィド)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Sid)),
        ("D3 標燃 (カド×ヒサ×ボルグ×ガルド)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Gald)),
        ("D4 標燃 (カド×ヒサ×ボルグ×ネル)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Nel)),
        ("D5 標燃 (カド×ヒサ×ボルグ×ノノ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Nono)),
        ("D6 標燃 (カド×ヒサ×ボルグ×ムド)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Mudo)),
        ("D7 標燃 (カド×ヒサ×ボルグ×ホタ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Hota)),
        ("D8 標燃 (カド×ヒサ×ボルグ×ドルガ)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Dolga)),
        // --- 毒の供給元を変える（グザ＝全員に毎ターン / スィド＝隣に被弾のたび）---
        ("E1 毒標燃 (ガルド×ヒサ×ボルグ×スィド)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Sid)),
        ("E2 毒標燃 (ドルガ×ヒサ×ボルグ×スィド)", Formation.Build(
            front1: UnitCatalog.Sid, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Dolga)),
        ("E3 毒標燃 (カド×ヒサ×ボルグ×グザ・ゴウ中央)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: SgPlain,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Guza)),
        ("E4 毒標燃 (カド×ヒサ×ボルグ×スィド・ゴウ中央)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: SgPlain,
            back1: UnitCatalog.Hisa, back3: UnitCatalog.Sid)),
        // --- 揃いにくい側（1〜2種類）---
        ("B1 標のみ (反撃 ヒサ×カド のノノ枠)", Formation.Build(
            front1: UnitCatalog.Kado, front3: UnitCatalog.Gald, center: UnitCatalog.Hisa,
            back1: SgPlain, back3: UnitCatalog.Nel)),
        ("B2 痺のみ (刻み×抉り のヴェル枠)", Formation.Build(
            front1: UnitCatalog.Egu, front3: UnitCatalog.Golm, center: UnitCatalog.Nomi,
            back1: UnitCatalog.Dolga, back3: SgPlain)),
        ("B3 毒のみ (澱み喰い のヴィオ枠)", Formation.Build(
            front1: UnitCatalog.Sid, front3: UnitCatalog.Gald, center: UnitCatalog.Guza,
            back1: UnitCatalog.Mio, back3: SgPlain)),
        ("B4 燃のみ (燃焼 ボルグ×ホタ のノノ枠)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Borg, center: UnitCatalog.Hota,
            back1: SgPlain, back3: UnitCatalog.Mudo)),
        ("B5 なし (耐久 ガルド×ノノ のセロ枠)", Formation.Build(
            front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, center: UnitCatalog.Nono,
            back1: SgPlain, back3: UnitCatalog.Golm)),
    };

    Console.WriteLine("## 0-7. 候補台の地図（`CompareBuilds()` は触っていない）");
    Console.WriteLine();
    Console.WriteLine("5枠目に**業と同数値・特性なしの素体（88/7/4・単体）**を置いた版で測る。");
    Console.WriteLine("この素体版がそのまま §4 の陽性対照になる。");
    Console.WriteLine();
    Console.WriteLine($"seed 0..{SgSeeds - 1}。`3種` は同時成立したターンの割合、`初3` は初成立ターン、");
    Console.WriteLine("`未3` は最後まで揃わなかった試行の割合。");
    Console.WriteLine();
    Console.WriteLine("**同時成立（`3種`）ではなく累積（`累3`）が到達の分母。** 業は引き取った種類を持ち越すので、");
    Console.WriteLine("盤面に3種が同時に並ぶ必要はない——毒と標は減らないから、一度引き取れば戦闘の終わりまで残る。");
    Console.WriteLine();
    Console.WriteLine("| 候補 | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 毒 | 標 | 痺 | 燃 | 2種 | 3種 | **累種** | **累3** | **累3未達** |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
    foreach (var c in sgCand)
    {
        var wins = new double[sgStages.Count];
        int kinds = sgKinds.Length;
        var tw = new long[kinds];
        long tt = 0, c2 = 0, c3 = 0;
        double cf3 = 0, cumSum = 0; int cf3n = 0, cumNever3 = 0, trials = 0;
        for (int w = 0; w < sgStages.Count; w++)
        {
            int win = 0;
            for (int seed = 0; seed < SgSeeds; seed++)
            {
                if (BattleEngine.Run(c.F, sgStages[w].Enemy, seed, verbose: false).PlayerWon) win++;
                var z = SgTrace(c.F, sgStages[w].Enemy, seed);
                trials++;
                tt += z.Turns; c2 += z.Kinds2; c3 += z.Kinds3;
                cumSum += z.CumKinds;
                if (z.CumFirst3 > 0) { cf3 += z.CumFirst3; cf3n++; } else cumNever3++;
                for (int k = 0; k < kinds; k++) tw[k] += z.TurnsWith[k];
            }
            wins[w] = win * 100.0 / SgSeeds;
        }
        Console.WriteLine($"| {c.Name} | **{wins.Average():0.0}%** "
            + string.Concat(wins.Select(x => $"| {x:0.0}% "))
            + string.Concat(Enumerable.Range(0, 4).Select(k => $"| {(tt == 0 ? 0 : tw[k] * 100.0 / tt):0.0}% "))
            + $"| {(tt == 0 ? 0 : c2 * 100.0 / tt):0.0}% | {(tt == 0 ? 0 : c3 * 100.0 / tt):0.0}% "
            + $"| **{cumSum / trials:0.00}** | **{(cf3n > 0 ? $"{cf3 / cf3n:0.00}" : "—")}** "
            + $"| **{cumNever3 * 100.0 / trials:0.0}%** |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    return;
}

// scapegoat モード（本体）: 業（第49期）。**出力は `docs/` に置かない。**
// `Stages` / `Columns` は触っていない。`CompareBuilds()` には**2行足した**（供給の種類を変えた対）。
//
//     dotnet run --project BattleSim -c Release 0 scapegoat [絞り込み]
//     dotnet run --project BattleSim -c Release 0 scapegoat sweep   # 閾値の掃引だけ
//     dotnet run --project BattleSim -c Release 0 scapegoat seats   # 席の分散だけ
//     dotnet run --project BattleSim -c Release 0 scapegoat stun    # 痺のある台（§7-3）だけ
public static void Run(string[] args, int stageIndex)
{
    var sgBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> sgStages = EnemyCatalog.Stages;
    const int SgSeeds = 200;   // compare / spread / scale と揃える
    const int SgMain = 3;      // 主表に使う Threshold（探索段階の初期値）
    int[] sgThresholds = { 2, 3, 4 };

    string sgMode = args.Length > 2 ? args[2] : "";

    // **業は測って採用しなかった**ので、`CompareBuilds()` には1行も入っていない
    // （逆位・まどろみ・誹り・驕りと同じ扱い）。測った2編成はここにある——
    // `CompareBuilds()` を1行も動かさずに全部を測り直せる（`overbear` の `ObRows()` と同型）。
    //
    // **土台は反撃台（カド×ヒサ×ボルグ）で固定し、5枚目だけを差し替えてある**
    // ——第21期の swap と同じ作法で、差がそのまま「供給が3種類あるかどうか」の差になる。
    // 配置は §1 の3段（reseat 上位5 → confirm seed 200..599 → 次数で読む）で決めた:
    // 仮置き（ゴウ 後1）はどちらも上位5に入らず、追試で +6.7pt / +5.8pt だったので
    // **ゴウを中央へ動かした**（5.0pt 以上の候補はどちらの行でも過半が中央＝次数4）。
    static (string Name, Formation F)[] SgRows() => new (string, Formation)[]
    {
        // 揃う側。グザの瘴気が味方全員（ゴウを含む）に毒を撒くので**毒は引き取り不要で載る**。
        ("業 (ゴウ×グザ)", Formation.Build(front1: UnitCatalog.Kado, front3: UnitCatalog.Borg,
                                      center: UnitCatalog.Gou, back1: UnitCatalog.Hisa,
                                      back3: UnitCatalog.Guza)),
        // 揃わない側。**グザ1枚をガルドに差し替えただけ**で供給が 毒+標+燃 → 標+燃 に落ち、
        // 閾値3に構造的に届かなくなる。**ヒサが前3に来るとゴウ（最大HP 88）が囃し立ての
        // 対象になる**ので標は最初からゴウに載り、引き取りは 0.00 回/戦になる
        // ——**配置探索が機構を無効化する席を選んだ**形（報告書 §5）。
        ("業改 (ゴウ×ガルド)", Formation.Build(front1: UnitCatalog.Kado, front3: UnitCatalog.Hisa,
                                        center: UnitCatalog.Gou, back1: UnitCatalog.Gald,
                                        back3: UnitCatalog.Borg)),
    };

    var sgTargets = SgRows();
    if (sgMode.Length > 0 && sgMode != "sweep" && sgMode != "seats" && sgMode != "stun"
        && sgMode != "confirm" && sgMode != "alt")
        sgTargets = sgTargets.Where(b => sgMode.Split(',').Any(k => b.Name.Contains(k.Trim()))).ToArray();

    // **素体の対照。** ゴウと数値・型・速さが1つも違わず、特性だけを持たない駒。
    // **`ScapegoatRule` を 0 にする形の対照は使わない**（第47期の失敗）——閾値を上げても
    // 引き取り（マイナス側）は止まらないので、「機構が効いたのか、ただ 88/7/4 の体が
    // 入っただけか」が割れない。**規則にノブを増やさず、駒の側で塞ぐ**
    // （`gradient` / `aim` / `guard` / `scale` と同じ、診断のローカルの def）。
    UnitDef SgPlainDef = new()
    {
        Id = "gou_plain", Name = "素体のゴウ", MaxHp = UnitCatalog.Gou.MaxHp,
        Attack = UnitCatalog.Gou.Attack, Speed = UnitCatalog.Gou.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = UnitCatalog.Gou.Pattern
    };
    Formation SgPlain(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            g[slot] = ReferenceEquals(d, UnitCatalog.Gou) ? SgPlainDef : d;
        return g;
    }
    // ゴウを外した4体版。**第21期の飽和検査**を兼ねる
    // ——4体版と5体版が同じ値なら、その台では第5の駒が何であっても結果が変わらない。
    static Formation SgWithoutGou(Formation f)
    {
        var g = new Formation();
        foreach ((int slot, UnitDef d) in f.Occupied())
            if (!ReferenceEquals(d, UnitCatalog.Gou)) g[slot] = d;
        return g;
    }
    // ゴウの席だけを振った5変種（他の4枚は元の相対順のまま空いた席へ詰める）。
    static Formation SgSeat(Formation f, int seat)
    {
        var others = f.Occupied().Where(o => !ReferenceEquals(o.Def, UnitCatalog.Gou))
                      .Select(o => o.Def).ToList();
        var g = new Formation();
        g[seat] = UnitCatalog.Gou;
        int k = 0;
        for (int i = 0; i < FormationRules.PlayableSlotCount && k < others.Count; i++)
            if (i != seat) g[i] = others[k++];
        return g;
    }

    // 1つの（編成 × 波 × 規則）についての計測。**盤面は触らない**——`BattleResult` の計数を読むだけ。
    // **監査（`Audit`）を必ず立てる**——素体の対照でも自傷・味方の継続ダメージを数えるため。
    // `who` は「5枚目の席の駒」の Def.Id（業なら "gou"・素体なら "gou_plain"）。
    // **両方の版で同じ切り方**にするために、保持者かどうかではなく id で割る。
    SgStat MeasureSg(Formation f, Formation enemy, ScapegoatRule rule, string who)
    {
        var z = new SgStat();
        double aliveTurns = 0, kindSum = 0, metSum = 0, firstSum = 0; int firstN = 0;
        for (int seed = 0; seed < SgSeeds; seed++)
        {
            var r = BattleEngine.Run(f, enemy, seed, verbose: false,
                        null, null, null, null, null, null, null, null, null, null, null,
                        rule with { Audit = true });
            if (r.PlayerWon) z.Win++;
            z.Turns += r.Turns;
            z.Takes += r.ScapegoatTakes; z.Missed += r.ScapegoatMissed; z.Full += r.ScapegoatFull;
            aliveTurns += r.ScapegoatAliveTurns; kindSum += r.ScapegoatKindSum;
            metSum += r.ScapegoatMetTurns;
            if (r.ScapegoatKindMax > z.KindMax) z.KindMax = r.ScapegoatKindMax;
            if (r.ScapegoatFirstTurn > 0) { firstSum += r.ScapegoatFirstTurn; firstN++; } else z.Never++;
            z.Swings += r.ScapegoatSwings; z.Fired += r.ScapegoatFired;
            z.FoeDot += r.ScapegoatFoeDot; z.FoeSkips += r.ScapegoatFoeSkips;
            z.MarkPulls += r.ScapegoatMarkPulls;
            foreach ((string id, int v) in r.ScapegoatDotByUnit)
                if (id == who) z.SelfDot += v; else z.AllyDot += v;
            foreach ((string id, int v) in r.ScapegoatSkipByUnit)
                if (id == who) z.SelfSkips += v; else z.AllySkips += v;
            z.Life += r.TallyByUnit.TryGetValue(who, out UnitTally? tw) ? tw.LastActiveTurn : 0;
            foreach ((string k, int v) in r.ScapegoatTakeByKind)
                z.TakeByKind[k] = z.TakeByKind.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.ScapegoatWriteByKind)
                z.WriteByKind[k] = z.WriteByKind.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, int v) in r.ScapegoatTakeFrom)
                z.TakeFrom[k] = z.TakeFrom.TryGetValue(k, out double a) ? a + v : v;
        }
        double n = SgSeeds;
        z.Win = z.Win * 100 / n; z.Turns /= n;
        z.Takes /= n; z.Missed /= n; z.Full /= n;
        z.KindAvg = aliveTurns > 0 ? kindSum / aliveTurns : 0;
        z.Met = aliveTurns > 0 ? metSum * 100 / aliveTurns : 0;
        z.First = firstN > 0 ? firstSum / firstN : 0;
        z.Never = z.Never * 100 / n;
        z.Swings /= n; z.Fired /= n;
        z.FoeDot /= n; z.FoeSkips /= n; z.MarkPulls /= n;
        z.SelfDot /= n; z.SelfSkips /= n; z.AllyDot /= n; z.AllySkips /= n; z.Life /= n;
        foreach (string k in z.TakeByKind.Keys.ToList()) z.TakeByKind[k] /= n;
        foreach (string k in z.WriteByKind.Keys.ToList()) z.WriteByKind[k] /= n;
        foreach (string k in z.TakeFrom.Keys.ToList()) z.TakeFrom[k] /= n;
        return z;
    }

    // 全波を通した集計（機構の量は波で平均する。勝率だけは波ごとに残す）
    (double[] Wins, SgStat Z) SgAll(Formation f, ScapegoatRule rule, string who)
    {
        var wins = new double[sgStages.Count];
        var acc = new SgStat();
        int firstN = 0;
        for (int w = 0; w < sgStages.Count; w++)
        {
            var z = MeasureSg(f, sgStages[w].Enemy, rule, who);
            wins[w] = z.Win;
            acc.Turns += z.Turns; acc.Takes += z.Takes; acc.Missed += z.Missed; acc.Full += z.Full;
            acc.KindAvg += z.KindAvg; acc.Met += z.Met; acc.Never += z.Never;
            acc.Swings += z.Swings; acc.Fired += z.Fired;
            acc.FoeDot += z.FoeDot; acc.FoeSkips += z.FoeSkips; acc.MarkPulls += z.MarkPulls;
            acc.SelfDot += z.SelfDot; acc.SelfSkips += z.SelfSkips;
            acc.AllyDot += z.AllyDot; acc.AllySkips += z.AllySkips; acc.Life += z.Life;
            if (z.KindMax > acc.KindMax) acc.KindMax = z.KindMax;
            if (z.First > 0) { acc.First += z.First; firstN++; }
            foreach ((string k, double v) in z.TakeByKind)
                acc.TakeByKind[k] = acc.TakeByKind.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, double v) in z.WriteByKind)
                acc.WriteByKind[k] = acc.WriteByKind.TryGetValue(k, out double a) ? a + v : v;
            foreach ((string k, double v) in z.TakeFrom)
                acc.TakeFrom[k] = acc.TakeFrom.TryGetValue(k, out double a) ? a + v : v;
        }
        double m = sgStages.Count;
        acc.First = firstN > 0 ? acc.First / firstN : 0;
        acc.Win = wins.Average();
        acc.Turns /= m; acc.Takes /= m; acc.Missed /= m; acc.Full /= m;
        acc.KindAvg /= m; acc.Met /= m; acc.Never /= m;
        acc.Swings /= m; acc.Fired /= m;
        acc.FoeDot /= m; acc.FoeSkips /= m; acc.MarkPulls /= m;
        acc.SelfDot /= m; acc.SelfSkips /= m; acc.AllyDot /= m; acc.AllySkips /= m; acc.Life /= m;
        foreach (string k in acc.TakeByKind.Keys.ToList()) acc.TakeByKind[k] /= m;
        foreach (string k in acc.WriteByKind.Keys.ToList()) acc.WriteByKind[k] /= m;
        foreach (string k in acc.TakeFrom.Keys.ToList()) acc.TakeFrom[k] /= m;
        return (wins, acc);
    }

    double[] SgPlainWins(Formation f)
    {
        var v = new double[sgStages.Count];
        for (int w = 0; w < sgStages.Count; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < SgSeeds; seed++)
                if (BattleEngine.Run(f, sgStages[w].Enemy, seed, false).PlayerWon) wins++;
            v[w] = wins * 100.0 / SgSeeds;
        }
        return v;
    }

    static string SgCells(double[] w) => string.Concat(w.Select(x => $" {x:0.0}% |"));
    string SgByKind(Dictionary<string, double> d)
    {
        var parts = ScapegoatTrait.Kinds.Where(k => d.TryGetValue(k, out double v) && v > 0)
            .Select(k => $"{StatusKeys.LabelOf(k)} {d[k]:0.00}").ToList();
        return parts.Count == 0 ? "—" : string.Join(" / ", parts);
    }

    Console.WriteLine("# 業（scapegoat）");
    Console.WriteLine();
    Console.WriteLine("`dotnet run --project BattleSim -c Release 0 scapegoat [絞り込み]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{SgSeeds - 1}。数字は特記なければ**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("`Stages` / `Columns` / `CompareBuilds()` は触っていない。**業は測って採用しなかった**ので、");
    Console.WriteLine("測った2編成はこの診断のローカル（`SgRows()`）にある（`overbear` の `ObRows()` と同型）。");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 引き取り | 移した延べ量/戦・**種類ごとの内訳**・取った相手の内訳 |");
    Console.WriteLine("| 空振り | 引き取れる種類が盤面に無くて何もしなかった回数/戦（`満杯` は全種類を既に背負っていた回数） |");
    Console.WriteLine("| 種類数 | ゴウが背負っている種類数の 平均 / 最大（引き取りの**後**に数える） |");
    Console.WriteLine("| **到達** | **閾値に初めて達したターン**（達した試行だけの平均）。`未達` が一度も達しなかった試行の割合 |");
    Console.WriteLine("| 成立率 | 閾値を満たしていたターン数 ÷ ゴウが生きてターン頭を迎えた回数 |");
    Console.WriteLine("| 転写 | 発揮した回数/戦・**付けた種類の内訳** |");
    Console.WriteLine("| **転写の効き** | **毒燃**＝業が書いたぶんに帰属する継続ダメージ ／ **痺**＝飛ばした敵の手番 ／ **標**＝味方の単体攻撃が引かれた回数 |");
    Console.WriteLine("| 自傷 | ゴウが継続ダメージで受けた量 / 痺れで失った手番。**寿命**はゴウが最後に盤上にいたターン |");
    Console.WriteLine("| 味方 | **ゴウ以外の味方**が継続ダメージで受けた量 / 失った手番。**救済は素体との差で読む** |");
    Console.WriteLine();
    Console.WriteLine("> **「転写」と「転写の効き」は別の列。** 付けた回数は成果ではない");
    Console.WriteLine("> ——敵が次のターンに死ぬなら毒を付けても意味がない。");
    Console.WriteLine();
    Console.WriteLine("> **「自傷」と「味方」は絶対値では帰属が取れない。** 瘴気の毒はゴウが引き取らなくても");
    Console.WriteLine("> 味方全員に載るので、**素体版との差**でしか機構のぶんは割れない。");
    Console.WriteLine();

    // --- 0. 検算（受け入れ基準1・2）----------------------------------------------------------
    if (sgMode.Length == 0)
    {
        Console.WriteLine("## 0. 検算 —— 差分は業だけに閉じているか（受け入れ基準2）");
        Console.WriteLine();
        var plain = sgBuilds;   // 業は CompareBuilds に入っていないので全 50 行が対象
        int cells = 0, diff = 0;
        foreach (var b in plain)
            for (int w = 0; w < sgStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < SgSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, sgStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null,
                            new ScapegoatRule(1)).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, sgStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null,
                            new ScapegoatRule(9)).PlayerWon) c++;
                }
                cells++;
                if (a != c) diff++;
            }
        Console.WriteLine($"- **基準2**（ゴウを含まない {plain.Length} 行が `ScapegoatRule` の値に対して不変）: "
            + $"**{cells} セル中 {diff} 件の食い違い**（{plain.Length} 行 × {sgStages.Count} 波・`Threshold` 1 対 9）");

        // 監査（Audit）が盤面を動かしていないことの検算。**素体の対照が成立する前提そのもの。**
        int aCells = 0, aDiff = 0;
        foreach (var b in sgBuilds)
            for (int w = 0; w < sgStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < SgSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, sgStages[w].Enemy, seed, false).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, sgStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null,
                            new ScapegoatRule(ScapegoatRule.Default.Threshold, true)).PlayerWon) c++;
                }
                aCells++;
                if (a != c) aDiff++;
            }
        Console.WriteLine($"- **監査は盤面を動かさない**（`Audit` の有無で `compare` が変わらない）: "
            + $"**{aCells} セル中 {aDiff} 件の食い違い**（{sgBuilds.Length} 行 × {sgStages.Count} 波）");
        Console.WriteLine("- **基準1**（新駒を編成に入れない状態で `compare` が `docs/balance.md` と完全一致）は");
        Console.WriteLine("  `compare` の全文で確認済み（**250 セル中 0 件**・業は `UnitCatalog.All` にも");
        Console.WriteLine("  `CompareBuilds()` にも載っていないので、`docs/balance.md` は1バイトも動かない）。");
        Console.WriteLine("  engine に足した計数のフック（`TickStatuses` の2箇所・行動順ループの痺れ分岐・");
        Console.WriteLine("  標の引き）は**すべて `ScapegoatActive` で短絡**し、控えのカウンタは誰も読んで分岐しない。");
        Console.WriteLine();
        Console.Out.Flush();
    }

    // --- 1. 主表 -----------------------------------------------------------------------------
    if (sgMode.Length == 0 || (sgMode != "sweep" && sgMode != "seats" && sgMode != "stun"
                               && sgMode != "confirm" && sgMode != "alt"))
    {
        Console.WriteLine($"## 1. 主表（`Threshold = {SgMain}` と陽性対照）");
        Console.WriteLine();
        Console.WriteLine("`素体` = ゴウと**数値・型・速さが1つも違わず特性だけを持たない駒**に差し替えた版。");
        Console.WriteLine("**これが機構の帰属を取る唯一の窓口**——閾値を上げても引き取り（マイナス側）は止まらないので、");
        Console.WriteLine("`ScapegoatRule` を大きくする形は対照にならない（第47期 `ScaleRule(0)` と同じ穴）。");
        Console.WriteLine("`4体` = ゴウを外した4体版（**第21期の飽和検査**も兼ねる");
        Console.WriteLine("——4体版と5体版が同じ値なら、その台では5枚目が何であっても結果が変わらない）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
        {
            var (wins, _) = SgAll(b.F, new ScapegoatRule(SgMain), "gou");
            var pw = SgPlainWins(SgPlain(b.F));
            var fw = SgPlainWins(SgWithoutGou(b.F));
            Console.WriteLine($"| {b.Name} | **T{SgMain}** |{SgCells(wins)} {wins.Average():0.0}% |");
            Console.WriteLine($"| | 素体（特性なし・同数値） |{SgCells(pw)} {pw.Average():0.0}% |");
            Console.WriteLine($"| | 4体（ゴウ抜き） |{SgCells(fw)} {fw.Average():0.0}% |");
            Console.WriteLine($"| | **機構の帰属（T{SgMain} − 素体）** | "
                + string.Concat(Enumerable.Range(0, wins.Length).Select(i => $"{wins[i] - pw[i]:+0.0;-0.0;0.0} |"))
                + $" **{wins.Average() - pw.Average():+0.0;-0.0;0.0}** |");
            Console.WriteLine($"| | 体の値段（素体 − 4体） | "
                + string.Concat(Enumerable.Range(0, pw.Length).Select(i => $"{pw[i] - fw[i]:+0.0;-0.0;0.0} |"))
                + $" **{pw.Average() - fw.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine($"### 機構の計数（`Threshold = {SgMain}`・5波の平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 引き取り | 内訳 | 取った相手 | 空振り | 満杯 | 種類数(平均/最大) | **到達** | **未達** | **成立率** | 転写/振り | 付けた内訳 |");
        Console.WriteLine("|---|--:|---|---|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var b in sgTargets)
        {
            var (_, z) = SgAll(b.F, new ScapegoatRule(SgMain), "gou");
            string from = z.TakeFrom.Count == 0 ? "—"
                : string.Join(" / ", z.TakeFrom.OrderByDescending(x => x.Value).Take(3)
                    .Select(x => $"{x.Key} {x.Value:0.00}"));
            Console.WriteLine($"| {b.Name} | {z.Takes:0.00} | {SgByKind(z.TakeByKind)} | {from} "
                + $"| {z.Missed:0.00} | {z.Full:0.00} | {z.KindAvg:0.00} / {z.KindMax:0} "
                + $"| {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% | {z.Met:0.0}% "
                + $"| {z.Fired:0.00} / {z.Swings:0.00} | {SgByKind(z.WriteByKind)} |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine($"### 転写の効き・自傷・味方（`Threshold = {SgMain}`・5波の平均。素体との差つき）");
        Console.WriteLine();
        Console.WriteLine("**`効き/転写` は「付けた1件あたり何ダメージ・何手番になったか」ではない**");
        Console.WriteLine("（単位が混ざる）。毒燃だけは量なので `毒燃の効き ÷ 毒燃を付けた延べ数` を出す。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 転写 | **効き・毒燃** | 毒燃/件 | **効き・痺** | **効き・標** | 自傷(継続) | 自傷(手番) | 寿命 | 味方(継続) | 味方(手番) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
        {
            var (_, z) = SgAll(b.F, new ScapegoatRule(SgMain), "gou");
            var pz = SgAll(SgPlain(b.F), new ScapegoatRule(SgMain), "gou_plain").Z;
            double dotWrites = (z.WriteByKind.TryGetValue(StatusKeys.Poison, out double wp) ? wp : 0)
                             + (z.WriteByKind.TryGetValue(StatusKeys.Burn, out double wb) ? wb : 0);
            Console.WriteLine($"| {b.Name} | {z.Fired:0.00} | **{z.FoeDot:0.0}** "
                + $"| {(dotWrites > 0 ? $"{z.FoeDot / dotWrites:0.00}" : "—")} "
                + $"| **{z.FoeSkips:0.000}** | **{z.MarkPulls:0.000}** "
                + $"| {z.SelfDot:0.0} | {z.SelfSkips:0.00} | {z.Life:0.00} "
                + $"| {z.AllyDot:0.0} | {z.AllySkips:0.00} |");
            Console.WriteLine($"| | 素体 | — | — | — | — | {pz.SelfDot:0.0} | {pz.SelfSkips:0.00} "
                + $"| {pz.Life:0.00} | {pz.AllyDot:0.0} | {pz.AllySkips:0.00} |");
            Console.WriteLine($"| | **差（業 − 素体）** | — | — | — | — "
                + $"| **{z.SelfDot - pz.SelfDot:+0.0;-0.0;0.0}** | **{z.SelfSkips - pz.SelfSkips:+0.00;-0.00;0.00}** "
                + $"| **{z.Life - pz.Life:+0.00;-0.00;0.00}** "
                + $"| **{z.AllyDot - pz.AllyDot:+0.0;-0.0;0.0}** | **{z.AllySkips - pz.AllySkips:+0.00;-0.00;0.00}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        Console.WriteLine("> **`味方(継続)` の差は救済だけを測っていない。** 引き取ったぶんは味方の帳簿から");
        Console.WriteLine("> ゴウの帳簿へ移るが、同時に**決着ターンが動く**（負ければ長引き、毒は層で伸びる）ので、");
        Console.WriteLine("> 差には救済と戦闘長の両方が入る。**`決着T` と一緒に読むこと。**");
        Console.WriteLine();

        // --- 2. 波ごとの内訳 -------------------------------------------------------------------
        Console.WriteLine($"## 2. 波ごとの内訳（`Threshold = {SgMain}`）");
        Console.WriteLine();
        Console.WriteLine("**供給の時間分布がこの期の設計。** 燃はボルグが振ってから隣接味方に載るので");
        Console.WriteLine("第1ターンには存在せず、標は開戦時に1つだけ載る。");
        Console.WriteLine();
        foreach (var b in sgTargets)
        {
            Console.WriteLine($"### {b.Name}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 | 引き取り | 内訳 | 空振り | 種類数 | 到達 | 未達 | 成立率 | 転写/振り | 効き(毒燃/痺/標) | 自傷 | 寿命 | 決着T |");
            Console.WriteLine("|---|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            for (int w = 0; w < sgStages.Count; w++)
            {
                var z = MeasureSg(b.F, sgStages[w].Enemy, new ScapegoatRule(SgMain), "gou");
                Console.WriteLine($"| {sgStages[w].Name} | {z.Win:0.0}% | {z.Takes:0.00} | {SgByKind(z.TakeByKind)} "
                    + $"| {z.Missed:0.00} | {z.KindAvg:0.00} | {(z.First > 0 ? $"{z.First:0.00}" : "—")} "
                    + $"| {z.Never:0.0}% | {z.Met:0.0}% | {z.Fired:0.00} / {z.Swings:0.00} "
                    + $"| {z.FoeDot:0.0} / {z.FoeSkips:0.00} / {z.MarkPulls:0.00} "
                    + $"| {z.SelfDot:0.0} | {z.Life:0.00} | {z.Turns:0.0} |");
                Console.Out.Flush();
            }
            Console.WriteLine();
        }
    }

    // --- 3. 掃引（受け入れ基準9）---------------------------------------------------------------
    if (sgMode.Length == 0 || sgMode == "sweep")
    {
        Console.WriteLine("## 3. 掃引（`Threshold` 2 / 3 / 4）");
        Console.WriteLine();
        Console.WriteLine("**閾値は強度ではなく「発火するかしないか」を切る**はずのノブ。");
        Console.WriteLine("**味方に載る種類は4つしかなく、しかも痺は使えない**（引き取ると手番が飛ぶ）ので、");
        Console.WriteLine("実効の分母は 毒・標・燃 の3つ——`Threshold = 4` は構造的にほぼ到達不能。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 閾値 | 平均勝率 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 種類数 | 到達 | 未達 | 成立率 | 転写/振り | 効き(毒燃) | 自傷 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
        {
            foreach (int th in sgThresholds)
            {
                var (wins, z) = SgAll(b.F, new ScapegoatRule(th), "gou");
                Console.WriteLine($"| {b.Name} | {th} | **{wins.Average():0.0}%** |{SgCells(wins)} "
                    + $"{z.KindAvg:0.00} | {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% "
                    + $"| {z.Met:0.0}% | {z.Fired:0.00} / {z.Swings:0.00} | {z.FoeDot:0.0} | {z.SelfDot:0.0} |");
                Console.Out.Flush();
            }
            var pw = SgPlainWins(SgPlain(b.F));
            Console.WriteLine($"| {b.Name} | 素体 | **{pw.Average():0.0}%** |{SgCells(pw)} — | — | — | — | — | — | — |");
        }
        Console.WriteLine();
        foreach (var b in sgTargets)
        {
            var vals = sgThresholds.Select(th => SgAll(b.F, new ScapegoatRule(th), "gou").Wins.Average()).ToList();
            Console.WriteLine($"- **{b.Name} の掃引の全幅: {vals.Max() - vals.Min():0.0}pt**"
                + $"（{string.Join(" / ", sgThresholds.Zip(vals, (t, v) => $"T{t} {v:0.0}%"))}）");
        }
        Console.WriteLine();
    }

    // --- 4. 痺のある台（§7-3）-----------------------------------------------------------------
    // **`CompareBuilds()` は触らない。** 採用2行はどちらも痺の供給を持たない
    // （持たせると台が 20.0% に潰れる）ので、§7-3 の「引き取りと発揮が同じ資源を奪い合う」は
    // ローカルの台でしか観測できない。`gradient` / `aim` / `route` と同じ扱い。
    if (sgMode.Length == 0 || sgMode == "stun")
    {
        Console.WriteLine("## 4. 痺のある台（§7-3 の観測。`CompareBuilds()` には入れていない）");
        Console.WriteLine();
        Console.WriteLine("採用2行はどちらも痺の供給を持たない。**痺を足すと台が 20.0% に潰れる**ので、");
        Console.WriteLine("「痺を引き取ると手番が飛んで転写できない」は診断のローカルでしか測れない。");
        Console.WriteLine("台は採用行（業）のグザをシガ（責め苦・動ける敵を殴ると自分が痺れる）に差し替えたもの。");
        Console.WriteLine();
        var sgStunRows = new (string Name, Formation F)[]
        {
            ("痺台 (カド×ヒサ×ボルグ×シガ)", Formation.Build(
                front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
                back1: UnitCatalog.Gou, back3: UnitCatalog.Shiga)),
            ("痺台・素体", Formation.Build(
                front1: UnitCatalog.Kado, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
                back1: SgPlainDef, back3: UnitCatalog.Shiga)),
            ("毒痺台 (グザ×シガ×ボルグ×ゴウ)", Formation.Build(
                front1: UnitCatalog.Shiga, front3: UnitCatalog.Borg, center: UnitCatalog.Hisa,
                back1: UnitCatalog.Gou, back3: UnitCatalog.Guza)),
        };
        Console.WriteLine("| 台 | 平均勝率 | 引き取り | 内訳 | 種類数 | 到達 | 未達 | 成立率 | 転写/振り | **自傷(手番)** | 寿命 |");
        Console.WriteLine("|---|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in sgStunRows)
        {
            var (wins, z) = SgAll(r.F, new ScapegoatRule(SgMain), r.Name.Contains("素体") ? "gou_plain" : "gou");
            Console.WriteLine($"| {r.Name} | **{wins.Average():0.0}%** | {z.Takes:0.00} | {SgByKind(z.TakeByKind)} "
                + $"| {z.KindAvg:0.00} | {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% "
                + $"| {z.Met:0.0}% | {z.Fired:0.00} / {z.Swings:0.00} | **{z.SelfSkips:0.00}** | {z.Life:0.00} |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        Console.WriteLine("> **`成立率` が高いのに `転写/振り` が低ければ、それが §7-3 の症状。**");
        Console.WriteLine("> 痺を持っているターンのゴウは手番を飛ばすので、閾値を満たしていても振れない。");
        Console.WriteLine();
    }

    // --- 4a. 別 seed の追試（第44期の作法）------------------------------------------------------
    // 主表の「機構の帰属」は seed 0..199 で測っている。**符号が大きく出た波は別帯で追試する**
    // ——第44期は第二波の +1.0pt が別帯で −2.0pt に割れた。
    if (sgMode == "alt")
    {
        const int AltFrom = 200, AltTo = 600;
        Console.WriteLine("## 4a. 別 seed の追試（seed 200..599・機構の帰属）");
        Console.WriteLine();
        Console.WriteLine("`T3 − 素体` を**選定に使っていない seed 帯**で測り直す。");
        Console.WriteLine("seed 0..199 の値と符号が揃わない波は、そこで見えた差が乱数だったということ。");
        Console.WriteLine();
        double[] Cells(Formation f, ScapegoatRule? rule)
        {
            var v = new double[sgStages.Count];
            for (int w = 0; w < sgStages.Count; w++)
            {
                int wins = 0;
                for (int seed = AltFrom; seed < AltTo; seed++)
                    if (BattleEngine.Run(f, sgStages[w].Enemy, seed, false,
                            null, null, null, null, null, null, null, null, null, null, null,
                            rule).PlayerWon) wins++;
                v[w] = wins * 100.0 / (AltTo - AltFrom);
            }
            return v;
        }
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
        {
            var t3 = Cells(b.F, new ScapegoatRule(SgMain));
            var pl = Cells(SgPlain(b.F), null);
            Console.WriteLine($"| {b.Name} | T{SgMain} |{SgCells(t3)} {t3.Average():0.0}% |");
            Console.WriteLine($"| | 素体 |{SgCells(pl)} {pl.Average():0.0}% |");
            Console.WriteLine($"| | **帰属（追試）** | "
                + string.Concat(Enumerable.Range(0, t3.Length).Select(i => $"{t3[i] - pl[i]:+0.0;-0.0;0.0} |"))
                + $" **{t3.Average() - pl.Average():+0.0;-0.0;0.0}** |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        return;
    }

    // --- 4b. 配置の追試（§1 の作法・3段目）------------------------------------------------------
    // (1) 現行が reseat の上位5通りに入っていれば動かさない
    // (2) 入っていない行だけ confirm（**選定に使っていない seed 200..599**）で測り、
    //     **5.0pt 以上のときだけ**動かす（第46期に 2.0 → 5.0 へ変更）
    // (3) 採否は1位の配置ではなく**次数**で読む（1位は 28/48 行で入れ替わるが次数の一致率は 98%）
    if (sgMode == "confirm")
    {
        const int CfFrom = 200, CfTo = 600;   // reseat / seats の選定帯（0..199）と重ならない
        Console.WriteLine("## 4b. 配置の追試（`confirm`・seed 200..599）");
        Console.WriteLine();
        Console.WriteLine("**選定に使っていない seed 帯**で測り直す。採否閾値は **5.0pt**（第46期）。");
        Console.WriteLine("**1位の配置ではなく次数で読む**（第45期の残件 D）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 配置 | ゴウの席 | ゴウの次数 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 現行との差 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
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
                foreach (EnemyCatalog.Stage st in sgStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            }
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));

            double[] Cells(Formation f)
            {
                var v = new double[sgStages.Count];
                for (int w = 0; w < sgStages.Count; w++)
                {
                    int wins = 0;
                    for (int seed = CfFrom; seed < CfTo; seed++)
                        if (BattleEngine.Run(f, sgStages[w].Enemy, seed, verbose: false).PlayerWon) wins++;
                    v[w] = wins * 100.0 / (CfTo - CfFrom);
                }
                return v;
            }
            int GouSeat(Formation f)
            {
                foreach ((int slot, UnitDef d) in f.Occupied())
                    if (ReferenceEquals(d, UnitCatalog.Gou)) return slot;
                return -1;
            }
            int Deg(int slot)
            {
                int n = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(slot, i)) n++;
                return n;
            }

            var cur = Cells(b.F);
            Console.WriteLine($"| {b.Name} | **現行** | {FormationRules.SeatNames[GouSeat(b.F)]} "
                + $"| {Deg(GouSeat(b.F))} |{SgCells(cur)} **{cur.Average():0.0}%** | — |");
            Console.Out.Flush();
            // 粗探索の上位5通りを追試する（1位だけを見ない）
            foreach (int idx in order.Take(5))
            {
                if (idx == curIdx) continue;
                var v = Cells(perms[idx]);
                int seat = GouSeat(perms[idx]);
                Console.WriteLine($"| {b.Name} | 粗探索 {order.IndexOf(idx) + 1}位 | {FormationRules.SeatNames[seat]} "
                    + $"| {Deg(seat)} |{SgCells(v)} {v.Average():0.0}% "
                    + $"| **{v.Average() - cur.Average():+0.0;-0.0;0.0}pt** |");
                Console.WriteLine($"|   ↳ 配置 | {string.Join(" / ", perms[idx].Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]}:{o.Def.Name}"))} | | | | | | | | | |");
                Console.Out.Flush();
            }
        }
        Console.WriteLine();
        Console.WriteLine("**5.0pt 以上の候補があれば動かす。** 無ければ現行を据え置く。");
        Console.WriteLine();
        return;
    }

    // --- 5. 席の分散（seats2 の写し・受け入れ基準10）--------------------------------------------
    if (sgMode.Length == 0 || sgMode == "seats")
    {
        Console.WriteLine("## 5. 席の分散（`seats2` の写し・受け入れ基準10）");
        Console.WriteLine();
        Console.WriteLine("粗探索 seed 0..49 の全 120 通り → 上位20 + 現行 + 最下位 を seed 0..199 で測り直し。");
        Console.WriteLine("**採否に使うのは1位の配置ではなく次数**（第45期の残件 D）。閾値は 5.0pt。");
        Console.WriteLine("**業は隣接を1つも読まない**（引き取りの候補は生存味方全員）が、");
        Console.WriteLine("**供給の側は隣接で決まる**（火の粉はボルグの隣・囃し立てはヒサの隣）——");
        Console.WriteLine("第47期の鱗（読まないのに列で席が決まった）と同じ形になるかを見る。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 駒 | 最適席 | 次数 | 上位5の席（3値） | 最頻率 | 幅 | 現行の順位 | 1位との差 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
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
                foreach (EnemyCatalog.Stage st in sgStages)
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
                foreach (EnemyCatalog.Stage st in sgStages)
                {
                    int wins = 0;
                    for (int seed = 0; seed < SgSeeds; seed++)
                        if (BattleEngine.Run(f, st.Enemy, seed, verbose: false).PlayerWon) wins++;
                    avg += wins * 100.0 / SgSeeds;
                }
                return avg / sgStages.Count;
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
                int mid = 0, fcorner = 0, bcorner = 0;
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
                        }
                int bdeg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(bestSlot, i)) bdeg++;
                int top = Math.Max(mid, Math.Max(fcorner, bcorner));
                Console.WriteLine($"| {b.Name} | {d.Name} | {FormationRules.SeatNames[bestSlot]} | {bdeg} "
                    + $"| 前角{fcorner} / 中央{mid} / 後角{bcorner} | {top * 100 / 5}% | {width:0.0}pt "
                    + $"| {curRank} / {verified.Count} | {curGap:0.0}pt |");
            }
            Console.Out.Flush();
        }
        Console.WriteLine();

        Console.WriteLine("### ゴウ1枚だけを振った5変種（他の4枚は元の相対順のまま詰める）");
        Console.WriteLine();
        Console.WriteLine("| 行 | ゴウの席 | 次数 | 列 | 引き取り | 種類数 | 到達 | 未達 | 成立率 | 転写 | 自傷 | 寿命 | 平均勝率 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in sgTargets)
            for (int seat = 0; seat < FormationRules.PlayableSlotCount; seat++)
            {
                Formation g = SgSeat(b.F, seat);
                int deg = 0;
                for (int i = 0; i < FormationRules.PlayableSlotCount; i++)
                    if (FormationRules.AreAdjacent(seat, i)) deg++;
                var (wins, z) = SgAll(g, new ScapegoatRule(SgMain), "gou");
                Console.WriteLine($"| {b.Name} | {FormationRules.SeatNames[seat]} | {deg} "
                    + $"| {FormationRules.RowOf(seat)} | {z.Takes:0.00} | {z.KindAvg:0.00} "
                    + $"| {(z.First > 0 ? $"{z.First:0.00}" : "—")} | {z.Never:0.0}% | {z.Met:0.0}% "
                    + $"| {z.Fired:0.00} | {z.SelfDot:0.0} | {z.Life:0.00} | {wins.Average():0.0}% |");
                Console.Out.Flush();
            }
        Console.WriteLine();
    }

    return;
}
}
