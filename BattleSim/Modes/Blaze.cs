using BattleCore;
using static Common;

// =====================================================================================
// blaze モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "blaze")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 blaze
// =====================================================================================

static class BlazeDiag
{
// blaze モード: 破裂（ゾト）に着火を足す（第59期）。
//
// 燃焼は第57〜58期で「通貨」になったが、**書き手はボルグ1枚しかない**。
// 燃焼を読む駒はすべてボルグとの AND ゲートになり、燃焼が出る行はボルグを含む行に閉じている。
// `BomberTrait` のコメントは既に「味方も巻き込む。**これが他の駒の起点になる**」と書いてある
// ——その起点を実際に使う期。**駒は1枚も作らない**（ロスターの残り枠を消費しない）。
//
// **engine に新しい規則は足さない。** `BomberTrait.OnDeath` の既存のループの中で
// `ctx.Ignite` を呼ぶだけ。`Ignite` は乱数を1つも引かない（`Roll` を呼ばない）ので、
// **着火を足しても乱数列は動かない**——受け入れ基準2（ゾトを含まない行が不変）がこれを検算する。
//
//     dotnet run --project BattleSim -c Release 0 blaze phase0   # 実装前の地図（破裂時点の盤面・層の現況・情報セル）
//     dotnet run --project BattleSim -c Release 0 blaze          # 主表（A / B / V0）・計数・Q2・Q4・Q6
//     dotnet run --project BattleSim -c Release 0 blaze rows     # 試験行（死軸×ホタ / 死軸×ヒヨ）だけ
//     dotnet run --project BattleSim -c Release 0 blaze seats    # 試験行の席（reseat の写し）
//     dotnet run --project BattleSim -c Release 0 blaze confirm  # 配置の追試（seed 200..599）
//     dotnet run --project BattleSim -c Release 0 blaze alt      # 帰属の符号を別 seed 帯で追試
public static void Phase0(string[] args, int stageIndex)
{
    var bpBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> bpStages = EnemyCatalog.Stages;
    const int BpSeeds = 200;   // compare / spread / whet / burn / favor と同じ帯

    static bool BpHas(Formation f, UnitDef d) => f.Occupied().Any(o => ReferenceEquals(o.Def, d));

    Console.WriteLine("# 火種の地図（第59期 Phase 0）");
    Console.WriteLine();
    Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 blaze phase0` の出力。seed 0..{BpSeeds - 1}。");
    Console.WriteLine("**盤面は1つも動かしていない**（`BlazeRule` が未実装の時点で測っている）。");
    Console.WriteLine();

    // ---- 0-1. BomberTrait の現況 -------------------------------------------------------
    Console.WriteLine("## 0-1. `BomberTrait` の現況（指示書 §1-1）");
    Console.WriteLine();
    Console.WriteLine($"- `EnemyBlast` = **{BomberTrait.EnemyBlast}** ／ `AllyBlast` = **{BomberTrait.AllyBlast}**");
    Console.WriteLine("- `OnDeath` は `LivingMembersShuffled` を**敵側・味方側の2回**回す（味方側は `ally == self` を飛ばす）");
    Console.WriteLine("- **`Shuffle` は `Roll` を消費する**が、それは今もしている——");
    Console.WriteLine("  **既存のループの中で `ctx.Ignite` を呼べば新しい `Roll` は1つも増えない**");
    Console.WriteLine("  （`BattleContext.Ignite` は `SetCounter` / `Log` / 計数だけで、乱数を引かない）");
    Console.WriteLine($"- `BurnRules.Damage` = **{BurnRules.Damage}** ／ `BurnRules.Turns` = **{BurnRules.Turns}**（触らない）");
    Console.WriteLine();

    // ---- 0-2. ゾトの行とボルグの行 -----------------------------------------------------
    Console.WriteLine("## 0-2. ゾトを含む行 / ボルグを含む行（指示書 §1-2）");
    Console.WriteLine();
    var bpZoto = bpBuilds.Where(b => BpHas(b.F, UnitCatalog.Zoto)).ToArray();
    var bpBorg = bpBuilds.Where(b => BpHas(b.F, UnitCatalog.Borg)).ToArray();
    Console.WriteLine("| 行 | ゾトの席 | 同席する駒 |");
    Console.WriteLine("|---|:-:|---|");
    foreach (var b in bpZoto)
    {
        int seat = b.F.Occupied().First(o => ReferenceEquals(o.Def, UnitCatalog.Zoto)).Slot;
        string mates = string.Join(" / ", b.F.Occupied()
            .Where(o => !ReferenceEquals(o.Def, UnitCatalog.Zoto))
            .Select(o => $"{FormationRules.SeatNames[o.Slot]}:{o.Def.Name}"));
        Console.WriteLine($"| {b.Name} | {FormationRules.SeatNames[seat]} | {mates} |");
    }
    Console.WriteLine();
    Console.WriteLine($"- **ゾトを含む行: {bpZoto.Length}** ／ **ボルグを含む行: {bpBorg.Length}**");
    Console.WriteLine($"- ボルグの行: {string.Join(" / ", bpBorg.Select(b => b.Name))}");
    int bpBoth = bpBuilds.Count(b => BpHas(b.F, UnitCatalog.Zoto) && BpHas(b.F, UnitCatalog.Borg));
    Console.WriteLine($"- **両者が同席する行: {bpBoth}**（指示書の前提の検算）");
    Console.WriteLine($"- `CompareBuilds()` の行数: **{bpBuilds.Length}**（× {bpStages.Count} 波 = **{bpBuilds.Length * bpStages.Count} セル**）");
    Console.WriteLine();

    // ---- 0-3. 破裂の時点の盤面 ---------------------------------------------------------
    //
    // イベント列を頭から舐めて生存集合を保つ。**ゾトの Death イベントは OnDeath（破裂）より
    // 先に Emit される**（`BattleEngine.Kill` の並び）ので、そのイベントを処理し終えた時点の
    // 生存集合が「破裂が当たる相手」そのものになる。
    Console.WriteLine("## 0-3. 破裂の時点の盤面（**着火の上限**・指示書 §1-3）");
    Console.WriteLine();
    Console.WriteLine("**イベント列から実測**（`Death` は `OnDeath` より先に Emit されるので、");
    Console.WriteLine("そのイベントを処理し終えた生存集合が「破裂が当たる相手」そのもの）。");
    Console.WriteLine("`破裂/戦` は1戦あたりの破裂回数——**蘇生（ヴェル）で2回以上になりうる**。");
    Console.WriteLine("`味方` / `敵` は**初回の破裂の瞬間**に生きていた数（ゾト自身を除く）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 波 | 破裂/戦 | 破裂率 | 初回T | 味方 | 敵 | 決着T |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|--:|");
    var bpUp = new List<(string Name, double A, double E)>();
    foreach (var b in bpZoto)
    {
        int zid = BattleEngine.Materialize(b.F, BattleContext.PlayerTeam).FindIndex(u => u.Def.Id == "zoto");
        int pc = b.F.Occupied().Count();
        double sumA = 0, sumE = 0; int cntAll = 0;
        for (int w = 0; w < bpStages.Count; w++)
        {
            int ec = bpStages[w].Enemy.Occupied().Count();
            double bursts = 0, hit = 0, turn = 0, allies = 0, foes = 0, turns = 0;
            for (int seed = 0; seed < BpSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(b.F, bpStages[w].Enemy, seed, verbose: true);
                turns += r.Turns;
                var alive = new HashSet<int>();
                var team = new Dictionary<int, int>();
                for (int i = 0; i < pc; i++) { alive.Add(i); team[i] = 0; }
                for (int i = pc; i < pc + ec; i++) { alive.Add(i); team[i] = 1; }
                bool first = true;
                foreach (BattleEvent e in r.Events)
                {
                    int id = e.TargetId ?? -1;
                    if (e.Kind == BattleEventKind.Summon) { team[id] = e.Team ?? 0; alive.Add(id); }
                    else if (e.Kind == BattleEventKind.Revive) alive.Add(id);
                    else if (e.Kind == BattleEventKind.Death)
                    {
                        alive.Remove(id);
                        if (id != zid) continue;
                        bursts++;
                        if (!first) continue;
                        first = false; hit++; turn += e.Turn;
                        allies += alive.Count(x => team[x] == 0);
                        foes += alive.Count(x => team[x] == 1);
                    }
                }
            }
            double n = BpSeeds;
            Console.WriteLine($"| {b.Name} | 第{w + 1}波 | {bursts / n:0.00} | {hit * 100 / n:0.0}% "
                + $"| {(hit > 0 ? turn / hit : 0):0.00} | **{(hit > 0 ? allies / hit : 0):0.00}** "
                + $"| **{(hit > 0 ? foes / hit : 0):0.00}** | {turns / n:0.0} |");
            if (hit > 0) { sumA += allies / hit; sumE += foes / hit; cntAll++; }
            Console.Out.Flush();
        }
        bpUp.Add((b.Name, cntAll > 0 ? sumA / cntAll : 0, cntAll > 0 ? sumE / cntAll : 0));
    }
    Console.WriteLine();
    Console.WriteLine("### 供給量の予測（**測る前に書く**）");
    Console.WriteLine();
    Console.WriteLine("A 案（味方だけ）= `味方` の平均、B 案（全員）= `味方 + 敵` の平均。");
    Console.WriteLine("**破裂は一度きり**なので、これが `着火` の期待値の上限になる（増える余地は蘇生の分だけ）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | A 案の予測（着火/戦） | B 案の予測（着火/戦） |");
    Console.WriteLine("|---|--:|--:|");
    foreach ((string nm, double a, double e) in bpUp)
        Console.WriteLine($"| {nm} | {a:0.00} | {a + e:0.00} |");
    Console.WriteLine();

    // ---- 0-4. リィカの層の現況 ---------------------------------------------------------
    Console.WriteLine("## 0-4. リィカの層の現況（**Q2 の分母**・指示書 §1-4）");
    Console.WriteLine();
    Console.WriteLine("`味方の死` は1戦あたりの味方側の `Deaths` の合計（蘇生されて再度倒れると2）。");
    Console.WriteLine("`リィカ攻(終)` は最後に観測した `StatSnapshot`（＝ターン頭の `CurrentAttack`）。");
    Console.WriteLine($"リィカは `ModifyAttack` を持たないので **`AtkBonus` = その値 − {UnitCatalog.Rica.Attack}**。");
    Console.WriteLine($"層は死亡ごとに `NecroTrait.AllyStep` = {NecroTrait.AllyStep} 積み、毎ターン1つ薄れる。");
    Console.WriteLine($"**{NecroTrait.AwakenAt} 層以上で薙ぎに変わる**（覚醒）。");
    Console.WriteLine();
    Console.WriteLine("| 行 | 波 | 味方の死/戦 | リィカ攻(終) | `AtkBonus` | リィカ与ダメ/戦 | 勝率 |");
    Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|");
    foreach (var b in bpZoto.Where(b => BpHas(b.F, UnitCatalog.Rica)))
    {
        var pl0 = BattleEngine.Materialize(b.F, BattleContext.PlayerTeam);
        int rid = pl0.FindIndex(u => u.Def.Id == "rica");
        var pids = pl0.Select(u => u.Def.Id).ToHashSet();
        for (int w = 0; w < bpStages.Count; w++)
        {
            double deaths = 0, atk = 0, dmg = 0, win = 0;
            for (int seed = 0; seed < BpSeeds; seed++)
            {
                BattleResult r = BattleEngine.Run(b.F, bpStages[w].Enemy, seed, verbose: true);
                if (r.PlayerWon) win++;
                foreach ((string id, UnitTally t) in r.TallyByUnit)
                    if (pids.Contains(id)) deaths += t.Deaths;
                if (r.TallyByUnit.TryGetValue("rica", out UnitTally? rt)) dmg += rt.DamageToEnemy;
                int last = UnitCatalog.Rica.Attack;
                foreach (BattleEvent e in r.Events)
                    if (e.Kind == BattleEventKind.StatSnapshot && e.TargetId == rid) last = e.Amount;
                atk += last;
            }
            double n = BpSeeds;
            Console.WriteLine($"| {b.Name} | 第{w + 1}波 | {deaths / n:0.00} | {atk / n:0.0} "
                + $"| **{atk / n - UnitCatalog.Rica.Attack:+0.0;-0.0;0.0}** | {dmg / n:0.0} | {win * 100 / n:0.0}% |");
            Console.Out.Flush();
        }
    }
    Console.WriteLine();

    // ---- 0-5. 情報セル -----------------------------------------------------------------
    Console.WriteLine("## 0-5. 情報セルの表（棚卸し §4-1 の 1）");
    Console.WriteLine();
    Console.WriteLine("第31期 3-1 の定義: **第2〜5波のうち勝率が開区間 (0, 100) にあるセル**。");
    Console.WriteLine("第1波は全行 100.0% なので分母に入れない。");
    Console.WriteLine();
    Console.WriteLine("| # | 行 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | **情報セル** |");
    Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|");
    var bpInfo = new List<(string Name, int N, double[] W)>();
    for (int i = 0; i < bpBuilds.Length; i++)
    {
        var v = new double[bpStages.Count];
        for (int w = 0; w < bpStages.Count; w++)
        {
            int wins = 0;
            for (int seed = 0; seed < BpSeeds; seed++)
                if (BattleEngine.Run(bpBuilds[i].F, bpStages[w].Enemy, seed, false).PlayerWon) wins++;
            v[w] = wins * 100.0 / BpSeeds;
        }
        int info = v.Skip(1).Count(x => x > 0 && x < 100);
        bpInfo.Add((bpBuilds[i].Name, info, v));
        Console.WriteLine($"| {i + 1} | {bpBuilds[i].Name} " + string.Concat(v.Select(x => $"| {x:0.0}% "))
            + $"| **{info}** |");
        Console.Out.Flush();
    }
    Console.WriteLine();
    Console.WriteLine("### 情報セルが 0 または 1 の行（全部名指しする）");
    Console.WriteLine();
    Console.WriteLine("| 行 | 情報セル | 第2波 | 第3波 | 第4波 | 第5波 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    foreach ((string nm, int info, double[] v) in bpInfo.Where(x => x.N <= 1))
        Console.WriteLine($"| {nm} | **{info}** " + string.Concat(v.Skip(1).Select(x => $"| {x:0.0}% ")) + "|");
    Console.WriteLine();
    Console.WriteLine($"- 情報セルの合計: **{bpInfo.Sum(x => x.N)}** ／ 平均 **{bpInfo.Average(x => (double)x.N):0.00}**");
    Console.WriteLine($"- 0 の行: **{bpInfo.Count(x => x.N == 0)}** ／ 1 の行: **{bpInfo.Count(x => x.N == 1)}**");
    Console.WriteLine();

    // ---- 0-6. 波ごとの平均（歯止めの現在値）--------------------------------------------
    Console.WriteLine("## 0-6. 波ごとの平均（指示書 §1-6）");
    Console.WriteLine();
    Console.WriteLine("| 分母 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
    Console.WriteLine("|---|--:|--:|--:|--:|--:|");
    Console.WriteLine($"| **{bpInfo.Count} 行（現行）** "
        + string.Concat(Enumerable.Range(0, bpStages.Count).Select(w => $"| {bpInfo.Average(x => x.W[w]):0.0} ")) + "|");
    var bp56 = bpInfo.Take(56).ToList();
    Console.WriteLine("| 56 行（第53期の分母） "
        + string.Concat(Enumerable.Range(0, bpStages.Count).Select(w => $"| {bp56.Average(x => x.W[w]):0.0} ")) + "|");
    Console.WriteLine();
    return;
}

// ---- ここから先は本体（phase0 は上で return 済み）---------------------------------------
public static void Run(string[] args, int stageIndex)
{
    var bzBuilds = CompareBuilds();
    IReadOnlyList<EnemyCatalog.Stage> bzStages = EnemyCatalog.Stages;
    const int BzSeeds = 200;   // compare / spread / whet / burn / favor と同じ帯

    string bzMode = args.Length > 2 ? args[2] : "";
    // 主版の既定は採用版（B）。`a` を渡すと A 案で回る。
    string bzPick = args.Length > 3 ? args[3].ToLowerInvariant() : "b";

    var bzV0 = new BlazeRule(BlazeTargets.None);
    var bzA = new BlazeRule(BlazeTargets.AllyOnly);
    var bzB = new BlazeRule(BlazeTargets.Both);
    // C は**ノブではなく対照**。B の「味方に在庫を作る」と「敵に打点を足す」を割るためだけにある。
    var bzC = new BlazeRule(BlazeTargets.FoeOnly);
    BlazeRule bzMain = bzPick == "a" ? bzA : bzB;
    string bzMainName = bzPick == "a" ? "A（味方だけ）" : "B（全員・採用版）";

    static bool BzHas(Formation f, UnitDef d) => f.Occupied().Any(o => ReferenceEquals(o.Def, d));
    // **既存7行**（第58期までの死軸）と**試験行2本**（第59期に足した）を分けて持つ。
    // どちらもゾトを含むので、名前で割る。
    var bzRowNames = new[] { "死軸×ホタ", "死軸×ヒヨ" };
    var bzTargets = bzBuilds.Where(b => BzHas(b.F, UnitCatalog.Zoto)
                                     && !bzRowNames.Any(n => b.Name.Contains(n))).ToArray();

    // 試験行（指示書 §2-3）。**既存7行は組み替えない。**
    // どちらも `死の連鎖 (リィカ軸)` の**ムグ1枠だけ**を差し替えた形——
    // 1枠差にしておくと compare の既存行がそのまま対照になる。
    // 席は `blaze seats` / `blaze confirm` で決める（ここは探索の出発点）。
    // **採用後は `CompareBuilds()` から引く**（配置は `confirm` で決めた形）。
    // 席の探索そのものは `blaze seats` / `blaze confirm` が 120 通りを回すので、
    // ここが探索の出発点であることに変わりはない。
    var bzRows = bzBuilds.Where(b => bzRowNames.Any(n => b.Name.Contains(n))).ToArray();

    // 敵側の def.Id 集合。**味方 = これに入っていない**（胞子のような戦闘中に湧いた味方も味方に入る）。
    HashSet<string> BzFoeIds(Formation enemy) => enemy.Occupied().Select(o => o.Def.Id).ToHashSet();

    BzStat MeasureBz(Formation f, Formation enemy, BlazeRule rule, int from, int to)
    {
        var z = new BzStat();
        var foeIds = BzFoeIds(enemy);
        var pl0 = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        int rid = pl0.FindIndex(u => u.Def.Id == "rica");
        for (int seed = from; seed < to; seed++)
        {
            BattleResult r = BattleEngine.Run(f, enemy, seed, verbose: true, blaze: rule);
            if (r.PlayerWon) z.Win++;
            z.Turns += r.Turns;
            foreach ((string id, UnitTally t) in r.TallyByUnit)
            {
                bool ally = !foeIds.Contains(id);
                z.Lit += t.BurnLit; z.Relit += t.BurnRelit; z.LitAlly += t.BurnLitAlly;
                if (ally)
                {
                    z.BurnDmgA += t.BurnTaken; z.BurnDeathA += t.BurnDeaths;
                    z.Deaths += t.Deaths;
                }
                else
                {
                    z.BurnDmgF += t.BurnTaken; z.BurnDeathF += t.BurnDeaths;
                }
                if (!z.Atk.ContainsKey(id)) { z.Atk[id] = 0; z.BurnAtk[id] = 0; z.Dmg[id] = 0; }
                z.Atk[id] += t.Attacks; z.BurnAtk[id] += t.BurnAttacks; z.Dmg[id] += t.DamageToEnemy;
            }
            if (rid >= 0)
            {
                int last = UnitCatalog.Rica.Attack;
                foreach (BattleEvent e in r.Events)
                    if (e.Kind == BattleEventKind.StatSnapshot && e.TargetId == rid) last = e.Amount;
                z.RicaAtk += last;
            }
        }
        double n = to - from;
        z.Win = z.Win * 100 / n; z.Turns /= n;
        z.Lit /= n; z.Relit /= n; z.LitAlly /= n;
        z.BurnDmgA /= n; z.BurnDmgF /= n; z.BurnDeathA /= n; z.BurnDeathF /= n;
        z.Deaths /= n; z.RicaAtk /= n;
        foreach (string k in z.Atk.Keys.ToList())
        { z.Atk[k] /= n; z.BurnAtk[k] /= n; z.Dmg[k] /= n; }
        return z;
    }

    (double[] Wins, BzStat Z) BzAll(Formation f, BlazeRule rule, int from = 0, int to = BzSeeds)
    {
        var wins = new double[bzStages.Count];
        var acc = new BzStat();
        for (int w = 0; w < bzStages.Count; w++)
        {
            BzStat z = MeasureBz(f, bzStages[w].Enemy, rule, from, to);
            wins[w] = z.Win;
            acc.Turns += z.Turns; acc.Lit += z.Lit; acc.Relit += z.Relit; acc.LitAlly += z.LitAlly;
            acc.BurnDmgA += z.BurnDmgA; acc.BurnDmgF += z.BurnDmgF;
            acc.BurnDeathA += z.BurnDeathA; acc.BurnDeathF += z.BurnDeathF;
            acc.Deaths += z.Deaths; acc.RicaAtk += z.RicaAtk;
            foreach ((string k, double v) in z.Atk)
            {
                if (!acc.Atk.ContainsKey(k)) { acc.Atk[k] = 0; acc.BurnAtk[k] = 0; acc.Dmg[k] = 0; }
                acc.Atk[k] += v; acc.BurnAtk[k] += z.BurnAtk[k]; acc.Dmg[k] += z.Dmg[k];
            }
        }
        double m = bzStages.Count;
        acc.Win = wins.Average();
        acc.Turns /= m; acc.Lit /= m; acc.Relit /= m; acc.LitAlly /= m;
        acc.BurnDmgA /= m; acc.BurnDmgF /= m; acc.BurnDeathA /= m; acc.BurnDeathF /= m;
        acc.Deaths /= m; acc.RicaAtk /= m;
        foreach (string k in acc.Atk.Keys.ToList())
        { acc.Atk[k] /= m; acc.BurnAtk[k] /= m; acc.Dmg[k] /= m; }
        return (wins, acc);
    }

    double[] BzWins(Formation f, BlazeRule rule, int from = 0, int to = BzSeeds)
    {
        var v = new double[bzStages.Count];
        for (int w = 0; w < bzStages.Count; w++)
        {
            int wins = 0;
            for (int seed = from; seed < to; seed++)
                if (BattleEngine.Run(f, bzStages[w].Enemy, seed, false, blaze: rule).PlayerWon) wins++;
            v[w] = wins * 100.0 / (to - from);
        }
        return v;
    }

    static string BzCells(double[] w) => string.Concat(w.Select(x => $" {x:0.0}% |"));
    static int BzInfo(double[] w) => w.Skip(1).Count(x => x > 0 && x < 100);
    static double BzGet(Dictionary<string, double> d, string k) => d.TryGetValue(k, out double v) ? v : 0;

    Console.WriteLine("# 火種（blaze）");
    Console.WriteLine();
    Console.WriteLine($"`dotnet run --project BattleSim -c Release 0 blaze [モード] [a|b]` の出力。");
    Console.WriteLine($"**docs/ には置かない**（標準出力で読むだけ）。seed 0..{BzSeeds - 1}。数字は特記なければ**1戦あたりの平均**。");
    Console.WriteLine();
    Console.WriteLine("| 版 | 着火の対象 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| **V0** | 着火なし（現行）。**`docs/balance.md` と一致することの検証** |");
    Console.WriteLine("| **A** | 味方の巻き込みを受けた者だけ |");
    Console.WriteLine("| **B** | 破裂が当たった全員（敵も味方も） |");
    Console.WriteLine("| **C** | 敵だけ。**ノブではなく対照**——B の2つの効果を割るためだけにある（第41期の作法） |");
    Console.WriteLine();
    Console.WriteLine("| 列 | 中身 |");
    Console.WriteLine("|---|---|");
    Console.WriteLine("| 着火 | `BurnLit`（点いた回数）/戦。**0 でないことが Q1 の前半** |");
    Console.WriteLine("| 着火(味) | そのうち `friendly: true` で点いた回数（＝味方側） |");
    Console.WriteLine("| 再着火 | `BurnRelit`（既燃への再付与＝**捨てられた供給**）/戦 |");
    Console.WriteLine("| 捨て率 | 再着火 ÷ (着火 + 再着火)。**ボルグ系は 35.5%**（第57期） |");
    Console.WriteLine("| 燃(味) / 燃(敵) | 燃焼の刻みで実際に HP を失った量。陣営は敵側の `Def.Id` 集合で割る |");
    Console.WriteLine("| **燃死(味)** | **燃焼の刻みで倒れた味方の数/戦。Q2 の分子** |");
    Console.WriteLine("| **味方の死** | 味方側の `Deaths` の合計/戦。**リィカの層の供給** |");
    Console.WriteLine("| **リィカ攻(終)** | 最後に観測した `StatSnapshot`。`AtkBonus` = その値 − 5 |");
    Console.WriteLine();

    // --- 0. 検算 ------------------------------------------------------------------------------
    if (bzMode.Length == 0 || bzMode == "check")
    {
        Console.WriteLine("## 0. 検算（受け入れ基準 1・2）");
        Console.WriteLine();
        var plain = bzBuilds.Where(b => !BzHas(b.F, UnitCatalog.Zoto)).ToArray();
        int cells = 0, diff = 0;
        foreach (var b in plain)
            for (int w = 0; w < bzStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < BzSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, bzStages[w].Enemy, seed, false, blaze: bzV0).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, bzStages[w].Enemy, seed, false, blaze: bzB).PlayerWon) c++;
                }
                cells++;
                if (a != c) diff++;
            }
        Console.WriteLine($"- **基準2**（ゾトを含まない {plain.Length} 行が `BlazeRule` の値に対して不変）: "
            + $"**{cells} セル中 {diff} 件の食い違い**（{plain.Length} 行 × {bzStages.Count} 波・`None` 対 `Both`）");
        Console.WriteLine("  **ここが 0 でなければ乱数列を動かしている**（`Ignite` は `Roll` を引かない）。");

        // V0 がそのまま現行（既定）であることの検算。**引数を渡さない Run と1セルも違わないはず。**
        int c2 = 0, d2 = 0;
        foreach (var b in bzTargets)
            for (int w = 0; w < bzStages.Count; w++)
            {
                int a = 0, c = 0;
                for (int seed = 0; seed < BzSeeds; seed++)
                {
                    if (BattleEngine.Run(b.F, bzStages[w].Enemy, seed, false, blaze: bzB).PlayerWon) a++;
                    if (BattleEngine.Run(b.F, bzStages[w].Enemy, seed, false).PlayerWon) c++;
                }
                c2++;
                if (a != c) d2++;
            }
        Console.WriteLine($"- **既定が採用版**（`BlazeRule.Default` = `Both` ＝ 引数なしと一致・ゾトの {bzTargets.Length} 行）: "
            + $"**{c2} セル中 {d2} 件の食い違い**");
        Console.WriteLine("- **基準1**（採用前・`BlazeRule.None` で `compare` が `docs/balance.md` と完全一致）は");
        Console.WriteLine("  規則を足した直後・行を足す前に `compare` の全文で確かめた（**295 セル中 0 件**）。");
        Console.WriteLine();
        Console.Out.Flush();
        if (bzMode == "check") return;
    }

    // --- 1. 主表 ------------------------------------------------------------------------------
    if (bzMode.Length == 0 || bzMode == "main")
    {
        Console.WriteLine("## 1. 主表（既存7行 × V0 / A / B）");
        Console.WriteLine();
        Console.WriteLine("**既存7行は組み替えていない**（火種を足したときの素の効き方を測る台）。");
        Console.WriteLine("`帰属` は V0 との差。**閾値 |帰属| ≥ 1.5pt**（第57期）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | **帰属** | 情報セル |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in bzTargets)
        {
            var v0 = BzWins(b.F, bzV0);
            var va = BzWins(b.F, bzA);
            var vb = BzWins(b.F, bzB);
            var vc = BzWins(b.F, bzC);
            Console.WriteLine($"| {b.Name} | V0 |{BzCells(v0)} {v0.Average():0.0}% | — | {BzInfo(v0)} |");
            Console.WriteLine($"| | **A** |{BzCells(va)} {va.Average():0.0}% "
                + $"| **{va.Average() - v0.Average():+0.0;-0.0;0.0}** | {BzInfo(va)} |");
            Console.WriteLine($"| | **B** |{BzCells(vb)} {vb.Average():0.0}% "
                + $"| **{vb.Average() - v0.Average():+0.0;-0.0;0.0}** | {BzInfo(vb)} |");
            Console.WriteLine($"| | C（対照・敵だけ） |{BzCells(vc)} {vc.Average():0.0}% "
                + $"| {vc.Average() - v0.Average():+0.0;-0.0;0.0} | {BzInfo(vc)} |");
            Console.WriteLine($"| | ↳ **加法性の検算 A + C − V0** | | | | | | {va.Average() + vc.Average() - v0.Average():0.0}% "
                + $"| （B は {vb.Average():0.0}%） | |");
            Console.Out.Flush();
        }
        Console.WriteLine();

        // --- 2. 機構の計数（Q1）---------------------------------------------------------------
        Console.WriteLine("## 2. 機構の計数（**Q1**・`着火` が 0 でないこと / 捨て率）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 着火 | 着火(味) | 再着火 | **捨て率** | 燃(味) | 燃(敵) | 決着T |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in bzTargets)
            foreach ((string nm, BlazeRule rule) in new[] { ("V0", bzV0), ("A", bzA), ("B", bzB), ("C", bzC) })
            {
                var (_, z) = BzAll(b.F, rule);
                double tot = z.Lit + z.Relit;
                Console.WriteLine($"| {b.Name} | {nm} | **{z.Lit:0.00}** | {z.LitAlly:0.00} | {z.Relit:0.00} "
                    + $"| {(tot > 0 ? z.Relit * 100 / tot : 0):0.0}% | {z.BurnDmgA:0.0} | {z.BurnDmgF:0.0} | {z.Turns:0.0} |");
                Console.Out.Flush();
            }
        Console.WriteLine();

        // --- 3. 層に乗ったか（Q2）-------------------------------------------------------------
        Console.WriteLine("## 3. 層に乗ったか（**Q2**・拒否権を持つ判定）");
        Console.WriteLine();
        Console.WriteLine("**燃焼で倒れた味方（`燃死(味)`）とリィカの `AtkBonus` の最終値を V0 と比べる。**");
        Console.WriteLine("**リィカを含む行が別 seed 帯でも +5.0pt 以上上振れしたら、その版は棄却**（`blaze alt`）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | **燃死(味)** | 燃死(敵) | **味方の死** | **リィカ攻(終)** | `AtkBonus` | リィカ与ダメ | 平均勝率 | **帰属** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in bzTargets)
        {
            double baseWin = 0;
            foreach ((string nm, BlazeRule rule) in new[] { ("V0", bzV0), ("A", bzA), ("B", bzB), ("C", bzC) })
            {
                var (wins, z) = BzAll(b.F, rule);
                if (nm == "V0") baseWin = wins.Average();
                Console.WriteLine($"| {b.Name} | {nm} | **{z.BurnDeathA:0.00}** | {z.BurnDeathF:0.00} "
                    + $"| **{z.Deaths:0.00}** | {z.RicaAtk:0.0} "
                    + $"| {z.RicaAtk - UnitCatalog.Rica.Attack:+0.0;-0.0;0.0} | {BzGet(z.Dmg, "rica"):0.0} "
                    + $"| {wins.Average():0.0}% | {(nm == "V0" ? "—" : $"**{wins.Average() - baseWin:+0.0;-0.0;0.0}**")} |");
                Console.Out.Flush();
            }
        }
        Console.WriteLine();
        if (bzMode == "main") return;
    }

    // --- 4. 試験行（Q3 / Q4）------------------------------------------------------------------
    if (bzMode.Length == 0 || bzMode == "rows")
    {
        Console.WriteLine($"## 4. 試験行（**Q3 / Q4**・主版は {bzMainName}）");
        Console.WriteLine();
        Console.WriteLine("どちらも `死の連鎖 (リィカ軸)` の**ムグ1枠だけ**を差し替えた形——");
        Console.WriteLine("1枠差にすると compare の既存行がそのまま対照になる。");
        Console.WriteLine("**Q3 は情報セル 2 以上**（第2〜5波のうち開区間 (0, 100) が2つ以上）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | **情報セル** |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in bzRows)
            foreach ((string nm, BlazeRule rule) in new[] { ("V0", bzV0), ("A", bzA), ("B", bzB), ("C", bzC) })
            {
                var v = BzWins(b.F, rule);
                Console.WriteLine($"| {b.Name} | {nm} |{BzCells(v)} {v.Average():0.0}% | **{BzInfo(v)}** |");
                Console.Out.Flush();
            }
        Console.WriteLine();
        Console.WriteLine("### 稼働率（**Q4**・ボルグ独占は外れたか）");
        Console.WriteLine();
        Console.WriteLine("`燃振り%` = `BurnAttacks / Attacks`（**燃えている状態で振った割合**）。");
        Console.WriteLine("**第57期のボルグ台では 全5波・1000戦で 100%。**");
        Console.WriteLine("**下がっていることが「窓が生まれた」の証拠**——100% のままなら常時供給。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 駒 | 振り/戦 | 燃振り/戦 | **燃振り%** | 与ダメ/戦 | 着火 | 再着火 | 捨て率 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in bzRows)
            foreach ((string nm, BlazeRule rule) in new[] { ("V0", bzV0), ("A", bzA), ("B", bzB), ("C", bzC) })
            {
                var (_, z) = BzAll(b.F, rule);
                double tot = z.Lit + z.Relit;
                foreach (string who in new[] { "hota", "hiyo", "rica", "golm", "vel" })
                {
                    if (!z.Atk.ContainsKey(who)) continue;
                    if (who != "hota" && who != "hiyo" && who != "rica") continue;
                    double at = z.Atk[who], ba = z.BurnAtk[who];
                    Console.WriteLine($"| {b.Name} | {nm} | {who} | {at:0.00} | {ba:0.00} "
                        + $"| **{(at > 0 ? ba * 100 / at : 0):0.0}%** | {z.Dmg[who]:0.0} "
                        + $"| {z.Lit:0.00} | {z.Relit:0.00} | {(tot > 0 ? z.Relit * 100 / tot : 0):0.0}% |");
                }
                Console.Out.Flush();
            }
        Console.WriteLine();
        Console.WriteLine("### 波ごとの燃振り%（窓の形）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 駒 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|");
        foreach (var b in bzRows)
            foreach ((string nm, BlazeRule rule) in new[] { ("A", bzA), ("B", bzB) })
                foreach (string who in new[] { "hota", "hiyo" })
                {
                    var cells2 = new List<string>();
                    bool any = false;
                    for (int w = 0; w < bzStages.Count; w++)
                    {
                        BzStat z = MeasureBz(b.F, bzStages[w].Enemy, rule, 0, BzSeeds);
                        if (!z.Atk.ContainsKey(who)) { cells2.Add(" — "); continue; }
                        any = true;
                        double at = z.Atk[who], ba = z.BurnAtk[who];
                        cells2.Add($" {(at > 0 ? ba * 100 / at : 0):0.0}% ");
                    }
                    if (any) Console.WriteLine($"| {b.Name} | {nm} | {who} |" + string.Join("|", cells2) + "|");
                    Console.Out.Flush();
                }
        Console.WriteLine();
        if (bzMode == "rows") return;
    }

    // --- 5. 席（reseat の写し）----------------------------------------------------------------
    if (bzMode == "seats")
    {
        Console.WriteLine($"## 5. 席の粗探索（`reseat` の写し・主版は {bzMainName}）");
        Console.WriteLine();
        Console.WriteLine("seed 0..49 × 全5波の 120 通り全探索。**1位を採否の根拠にしない**（第45期）");
        Console.WriteLine("——上位5通りを `blaze confirm`（seed 200..599）で測り直す。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 順位 | 配置 | 粗探索の平均 |");
        Console.WriteLine("|---|--:|---|--:|");
        foreach (var b in bzRows)
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
            Parallel.For(0, perms.Count, i =>
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in bzStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false, blaze: bzMain).PlayerWon) wins++;
                scan[i] = wins;
            });
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));
            foreach (int idx in order.Take(6))
                Console.WriteLine($"| {b.Name} | {order.IndexOf(idx) + 1} "
                    + $"| {string.Join(" / ", perms[idx].Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]}:{o.Def.Name}"))} "
                    + $"| {scan[idx] * 100.0 / (bzStages.Count * 50):0.0}% |");
            Console.WriteLine($"| {b.Name} | **{order.IndexOf(curIdx) + 1}（出発点）** "
                + $"| {string.Join(" / ", perms[curIdx].Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]}:{o.Def.Name}"))} "
                + $"| {scan[curIdx] * 100.0 / (bzStages.Count * 50):0.0}% |");
            Console.WriteLine($"| {b.Name} | 最下位 | — | {scan[order[^1]] * 100.0 / (bzStages.Count * 50):0.0}% |");
            Console.Out.Flush();
        }
        Console.WriteLine();
        return;
    }

    // --- 5b. 席 × 情報セル --------------------------------------------------------------------
    //
    // **`reseat` は勝率だけを最大化する**ので、天井へ押し上げる席を選ぶ。
    // 試験行は `CompareBuilds()` に載せる「測るための行」なので、
    // **上位の席が情報セルを潰していないか**を 120 通り全部について見る必要がある
    // （第22期「採否は情報セルで読む」／第49期「配置探索が機構を無効化する席を選ぶ」の情報版）。
    if (bzMode == "scan")
    {
        Console.WriteLine($"## 5b. 席 × 情報セル（120 通り・主版は {bzMainName}）");
        Console.WriteLine();
        Console.WriteLine("`情報A` = seed 0..199 の情報セル数、`情報B` = seed 200..599 の情報セル数。");
        Console.WriteLine("**両方で 2 以上**の席だけが「測るための行」として使える（Q3）。");
        Console.WriteLine("並びは粗探索（seed 0..49）の順位。**上位から最初に条件を満たす席を採る。**");
        Console.WriteLine();
        Console.WriteLine("| 行 | 粗順位 | 配置 | 粗探索 | 平均A | 情報A | 平均B | 情報B | 可 |");
        Console.WriteLine("|---|--:|---|--:|--:|--:|--:|--:|:-:|");
        foreach (var b in bzRows)
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
            Parallel.For(0, perms.Count, i =>
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in bzStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false, blaze: bzMain).PlayerWon) wins++;
                scan[i] = wins;
            });
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));
            foreach (int idx in order.Take(12).Concat(new[] { curIdx }).Distinct())
            {
                var wa = BzWins(perms[idx], bzMain);
                var wb = BzWins(perms[idx], bzMain, 200, 600);
                int ia = BzInfo(wa), ib = BzInfo(wb);
                Console.WriteLine($"| {b.Name} | {order.IndexOf(idx) + 1}{(idx == curIdx ? "（現行）" : "")} "
                    + $"| {string.Join(" / ", perms[idx].Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]}:{o.Def.Name}"))} "
                    + $"| {scan[idx] * 100.0 / (bzStages.Count * 50):0.0}% | {wa.Average():0.0}% | **{ia}** "
                    + $"| {wb.Average():0.0}% | **{ib}** | {(ia >= 2 && ib >= 2 ? "○" : "×")} |");
                Console.Out.Flush();
            }
        }
        Console.WriteLine();
        return;
    }

    // --- 6. 配置の追試 ------------------------------------------------------------------------
    if (bzMode == "confirm")
    {
        const int CfFrom = 200, CfTo = 600;
        Console.WriteLine($"## 6. 配置の追試（`confirm`・seed 200..599・主版は {bzMainName}）");
        Console.WriteLine();
        Console.WriteLine("**選定に使っていない seed 帯**で測り直す。採否閾値は **5.0pt**（第46期）。");
        Console.WriteLine("**`着火` と `捨て率` の列を必ず見る**——配置探索が機構を無効化する席を");
        Console.WriteLine("選んでいたらそこは採用しない（第49期の業改が引き取り 0.00 回/戦になった件）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 配置 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | 差 | 情報セル | 着火 | 捨て率 | 燃死(味) |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in bzRows)
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
            Parallel.For(0, perms.Count, i =>
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in bzStages)
                    for (int seed = 0; seed < 50; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false, blaze: bzMain).PlayerWon) wins++;
                scan[i] = wins;
            });
            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            int curIdx = order.First(i => SameFormation(perms[i], b.F));

            var (bw, bz) = BzAll(b.F, bzMain, CfFrom, CfTo);
            double baseAvg = bw.Average();
            double btot = bz.Lit + bz.Relit;
            Console.WriteLine($"| {b.Name} | **出発点** |{BzCells(bw)} **{baseAvg:0.0}%** | — | {BzInfo(bw)} "
                + $"| {bz.Lit:0.00} | {(btot > 0 ? bz.Relit * 100 / btot : 0):0.0}% | {bz.BurnDeathA:0.00} |");
            Console.Out.Flush();
            foreach (int idx in order.Take(5))
            {
                if (idx == curIdx) continue;
                var (vw, vz) = BzAll(perms[idx], bzMain, CfFrom, CfTo);
                double tot = vz.Lit + vz.Relit;
                Console.WriteLine($"| {b.Name} | 粗探索 {order.IndexOf(idx) + 1}位: "
                    + $"{string.Join(" / ", perms[idx].Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]}:{o.Def.Name}"))} "
                    + $"|{BzCells(vw)} {vw.Average():0.0}% | **{vw.Average() - baseAvg:+0.0;-0.0;0.0}pt** | {BzInfo(vw)} "
                    + $"| {vz.Lit:0.00} | {(tot > 0 ? vz.Relit * 100 / tot : 0):0.0}% | {vz.BurnDeathA:0.00} |");
                Console.Out.Flush();
            }
            Console.WriteLine($"| {b.Name} | ↳ 出発点の順位: **粗探索 {order.IndexOf(curIdx) + 1}位 / {perms.Count}通り** | | | | | | | | | | | |");
        }
        Console.WriteLine();
        return;
    }

    // --- 7. 別 seed の追試 --------------------------------------------------------------------
    if (bzMode == "alt")
    {
        const int AltFrom = 200, AltTo = 600;
        Console.WriteLine("## 7. 別 seed の追試（seed 200..599・**Q2 と Q5**）");
        Console.WriteLine();
        Console.WriteLine("**帰属の符号が再現するか**（Q5・閾値 |帰属| ≥ 1.5pt）と、");
        Console.WriteLine("**リィカを含む行が +5.0pt 以上上振れしていないか**（Q2 の拒否権）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 平均 | **帰属** | 燃死(味) | 味方の死 | リィカ攻(終) |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in bzTargets.Concat(bzRows))
        {
            double baseWin = 0;
            foreach ((string nm, BlazeRule rule) in new[] { ("V0", bzV0), ("A", bzA), ("B", bzB), ("C", bzC) })
            {
                var (wins, z) = BzAll(b.F, rule, AltFrom, AltTo);
                if (nm == "V0") baseWin = wins.Average();
                Console.WriteLine($"| {b.Name} | {nm} |{BzCells(wins)} {wins.Average():0.0}% "
                    + $"| {(nm == "V0" ? "—" : $"**{wins.Average() - baseWin:+0.0;-0.0;0.0}**")} "
                    + $"| {z.BurnDeathA:0.00} | {z.Deaths:0.00} | {z.RicaAtk:0.0} |");
                Console.Out.Flush();
            }
        }
        Console.WriteLine();
        return;
    }

    return;
}
}
