using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S293 = Shock293Diag;
using S307 = Som307Diag;
using S309 = Som309Diag;

// =====================================================================================
// som310 —— 第310期「背いた獣を『喚んだ瞬間に弾ける』に（E1 ／ E2）＋ シガの的の対照（SG-f）」。
// 指示書は design/PHASE310_SOM_BURST_SPEC.md ／ 報告は design/PHASE310_SOM_BURST.md。
//
//     dotnet run --project BattleSim -c Release 0 som310 p0          # Phase 0（§3）: 規定のまま、獣の帳簿・シガの主目標・喚んだときの隣の帯電・ボスの火力の帳簿
//     dotnet run --project BattleSim -c Release 0 som310 boards      # §4-1: 代表台 × 波（ボス ／ 近衛 ／ 大隊 ／ 九体の波 ／ 本編の5波）× 版（規定 ／ E1 ／ E2 ／ SG-f）
//     dotnet run --project BattleSim -c Release 0 som310 compare     # §4-2: `compare` 64 行 × 版（(G2) の判定は版ごと）
//     dotnet run --project BattleSim -c Release 0 som310 swap        # §4-3: ソム ↔ ツギ（第309期 §5-1 と同じ入れ替え）× 版
//     dotnet run --project BattleSim -c Release 0 som310 grid <E1|E2> <boss|guard|bat>   # §4-4: ソムの格子（第309期と同じ作り・ソムの側だけ）
//     dotnet run --project BattleSim -c Release 0 som310 memo [版] [台の一部] [波] [seed] [最初のT] [最後のT]   # 台本の抜粋（`PHASE291_CODEX_MEMO.md` §14）
//     dotnet run --project BattleSim -c Release 0 som310 check       # 自己検査
//
// 規定の駒のまま組む（固定しない）。版は台の中のソム（SG-f はシガ）を同じ席で替えるだけ（`S309.Swap`・陣形とレリックを保つ）。
// =====================================================================================
static class Som310Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": BoardsAll(); return;
            case "compare": CompareAll(); return;
            case "swap": SwapAll(); return;
            case "grid": Grid(A(3, "E1"), A(4, "boss")); return;
            case "memo": Memo(A(3, "E1"), A(4, "光の盾"), A(5, "boss"), int.Parse(A(6, "0")), int.Parse(A(7, "1")), int.Parse(A(8, "3"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("som310: モードは p0 / boards / compare / swap / grid / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・台・波（測る前に固定）
    // ---------------------------------------------------------------------------------
    const int Seeds = S307.Seeds;
    static UnitDef Som => UnitCatalog.SomH310;   // 第311期: 第310期の規定（LV-a）に固定（`Common.Pin310`）
    static UnitDef Shiga => UnitCatalog.Shiga;
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);
    static string F1(double x) => S307.F1(x);
    static string Per1(long a, long n) => S307.Per1(a, n);
    static string Per0(long a, long n) => S307.Per0(a, n);
    static string Pct(long a, long n) => S307.Pct(a, n);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Dt(long died, long t, long n) => died == 0 ? "—" : $"{Per1(t, died)}（{Pct(died, n)}）";

    internal sealed record Ver(string Name, Func<Formation, Formation> Apply, bool ShigaOnly);
    internal static readonly Ver[] Vers =
    {
        new("規定", f => f, false),
        new("E1", f => S309.Swap(f, UnitCatalog.SomH310, UnitCatalog.SomE1), false),
        new("E2", f => S309.Swap(f, UnitCatalog.SomH310, UnitCatalog.SomE2), false),
        new("SG-f", f => S309.Swap(f, UnitCatalog.Shiga, UnitCatalog.ShigaSGf), true),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n);
    static bool Has(Formation f, UnitDef d) => f.Occupied().Any(o => ReferenceEquals(o.Def, d));

    /// <summary>波: ボス ／ 近衛 ／ 大隊 ／ 九体の波（検証・九 / 新兵 ／ 農兵・既定の倍率 115/115・DemoApp の検証の波と同じ構成）／ 本編の5波。</summary>
    internal static readonly S287.Wave[] Waves = S307.Waves.Take(3)
        .Append(new S287.Wave("九体 新兵", () => BattleEngine.MaterializeEnemy(EnemyCatalog.TestStages[0].Enemy, EnemyScaleRule.Default), false))
        .Append(new S287.Wave("九体 農兵", () => BattleEngine.MaterializeEnemy(EnemyCatalog.TestStages[1].Enemy, EnemyScaleRule.Default), false))
        .Concat(S307.Waves.Skip(3)).ToArray();
    static S287.Wave WaveOf(string n) => n switch { "nine" or "九体" => Waves[3], "nine2" => Waves[4], _ => S307.WaveOf(n) };
    static bool IsNine(S287.Wave w) => w.Name.StartsWith("九体", StringComparison.Ordinal);

    static Formation Playtest(string n) => S309.Playtest(n);
    static Formation Row(string n) => Pin310(CompareBuilds().First(r => r.Name == n).F);
    static Formation WinSeat() => B283.Seat(S307.Order(S307.WinBoard));

    /// <summary>§4-1 の代表台（規定の駒・ソムの席は出典のまま）。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("光の盾", Playtest(S309.Shield)),
        ("光の盾 重", Playtest(S309.ShieldHeavy)),
        ("感電 行", Row("感電 (シガ×カタ×ソム)")),
        ("雷の型 ドハ→ソム", S309.Swap(Playtest("試遊・感電 雷の型"), UnitCatalog.Doha, Som)),
        ("感電 糸 ガルド→ソム", S309.Swap(Playtest("試遊・感電 糸"), UnitCatalog.Gald, Som)),
        ("勝ち台 ツギ→ソム", S309.Swap(WinSeat(), UnitCatalog.Tsugi, Som)),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose の台本 ＋ 帳簿）
    // ---------------------------------------------------------------------------------
    internal sealed class St
    {
        public long N, Wins, WinT, LoseT, Caps, Turns, FirstDeathN, FirstDeathT, SomDied, SomDeathT;
        public Dictionary<string, long> FoeDmg = new();     // 敵（獣を除く）の HP に届いた量・出どころ別（味方は駒の名・獣の放電・敵の側）
        public long FoeHeal;                                // 敵が受けた回復（ボスなら勇者の自前の 40% ほか）
        // 獣
        public long Calls, Charged, SeatTaken, Bursts, BurstSeat, BurstUnits, BurstKills, Stood, BeastPops, BeastChainUnits, AtkOnBeast, DmgOnBeast;
        public Dictionary<string, long> BeastKillers = new();
        public long[] CallT = new long[9], ChargedT = new long[9];
        // シガ
        public long ShigaOwn, ShigaOwnBeast, ShigaInt, ShigaIntBeast, SwFires, SwMultSum, SwMultN, ShigaDmg;
        // カタ
        public long KataCloud, KataDmg, KataN;
        // 光
        public long Lights, Healed, VeilAdded, VeilSoaked;
        // 九体の波
        public long FirstCallN, FirstCallT, Light13;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Merge(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LoseT += o.LoseT; Caps += o.Caps; Turns += o.Turns; FirstDeathN += o.FirstDeathN; FirstDeathT += o.FirstDeathT; SomDied += o.SomDied; SomDeathT += o.SomDeathT;
            foreach (var (k, v) in o.FoeDmg) FoeDmg[k] = FoeDmg.GetValueOrDefault(k) + v;
            FoeHeal += o.FoeHeal;
            Calls += o.Calls; Charged += o.Charged; SeatTaken += o.SeatTaken; Bursts += o.Bursts; BurstSeat += o.BurstSeat; BurstUnits += o.BurstUnits; BurstKills += o.BurstKills; Stood += o.Stood;
            BeastPops += o.BeastPops; BeastChainUnits += o.BeastChainUnits; AtkOnBeast += o.AtkOnBeast; DmgOnBeast += o.DmgOnBeast;
            foreach (var (k, v) in o.BeastKillers) BeastKillers[k] = BeastKillers.GetValueOrDefault(k) + v;
            for (int i = 0; i < 9; i++) { CallT[i] += o.CallT[i]; ChargedT[i] += o.ChargedT[i]; }
            ShigaOwn += o.ShigaOwn; ShigaOwnBeast += o.ShigaOwnBeast; ShigaInt += o.ShigaInt; ShigaIntBeast += o.ShigaIntBeast; SwFires += o.SwFires; SwMultSum += o.SwMultSum; SwMultN += o.SwMultN; ShigaDmg += o.ShigaDmg;
            KataCloud += o.KataCloud; KataDmg += o.KataDmg; KataN += o.KataN;
            Lights += o.Lights; Healed += o.Healed; VeilAdded += o.VeilAdded; VeilSoaked += o.VeilSoaked;
            FirstCallN += o.FirstCallN; FirstCallT += o.FirstCallT; Light13 += o.Light13;
        }
        public long FoeDmgAll => FoeDmg.Values.Sum();
    }

    internal static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var a = new St { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else { a.LoseT = r.Turns; if (r.Turns >= BattleEngine.MaxTurns) a.Caps = 1; }
        var mine = p.ToDictionary(u => u.InstanceId, u => u);
        UnitState? som = p.FirstOrDefault(u => u.Def.Id == "som"), shiga = p.FirstOrDefault(u => u.Def.Id == "shiga"), kata = p.FirstOrDefault(u => u.Def.Id == "kata");
        var beasts = new HashSet<int>(); var phantoms = new HashSet<int>();
        int chainRoot = -1; bool chainBeast = false;
        bool shigaPending = false;
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Summon when som is not null && ev.ActorId == som.InstanceId && ev.TargetId is int sid:
                    beasts.Add(sid);
                    if (a.FirstCallN == 0) { a.FirstCallN = 1; a.FirstCallT = ev.Turn; }
                    break;
                case BattleEventKind.BeastBurst when ev.TargetId is int bid: phantoms.Add(bid); break;
                case BattleEventKind.Death when ev.TargetId is int t && mine.ContainsKey(t):
                    if (a.FirstDeathN == 0) { a.FirstDeathN = 1; a.FirstDeathT = ev.Turn; }
                    if (som is not null && t == som.InstanceId && a.SomDied == 0) { a.SomDied = 1; a.SomDeathT = ev.Turn; }
                    break;
                case BattleEventKind.Death when ev.TargetId is int t2 && beasts.Contains(t2):
                    {
                        string k = ev.ActorId is int ka && mine.TryGetValue(ka, out var ku) ? Short(ku.Def) : ev.ActorId is null ? "（出どころなし）" : "敵の側";
                        a.BeastKillers[k] = a.BeastKillers.GetValueOrDefault(k) + 1;
                        break;
                    }
                case BattleEventKind.ShockSpent:
                    if (ev.Slot == 0) { chainRoot = ev.TargetId ?? -1; chainBeast = beasts.Contains(chainRoot); if (chainBeast) a.BeastPops++; }
                    if (chainBeast) a.BeastChainUnits++;
                    break;
                case BattleEventKind.ShockGauge when shiga is not null && ev.ActorId == shiga.InstanceId && ev.Text == ShockGaugeLabels.Interrupt: shigaPending = true; break;
                case BattleEventKind.Attack when ev.ActorId is int at && mine.ContainsKey(at):
                    {
                        bool onBeast = ev.TargetId is int tb && beasts.Contains(tb);
                        if (onBeast) a.AtkOnBeast++;
                        if (shiga is not null && at == shiga.InstanceId)
                        {
                            if (shigaPending) { a.ShigaInt++; if (onBeast) a.ShigaIntBeast++; shigaPending = false; }
                            else { a.ShigaOwn++; if (onBeast) a.ShigaOwnBeast++; }
                        }
                        break;
                    }
                case BattleEventKind.Damage when ev.TargetId is int dt && !mine.ContainsKey(dt):
                    if (beasts.Contains(dt)) { if (ev.ActorId is int da && mine.ContainsKey(da)) a.DmgOnBeast += ev.Amount; break; }
                    {
                        string k = ev.ActorId is int ka && mine.TryGetValue(ka, out var ku) ? Short(ku.Def) : ev.ActorId is int kb && beasts.Contains(kb) ? "獣の放電" : ev.ActorId is null ? "刻み" : "敵の側";
                        a.FoeDmg[k] = a.FoeDmg.GetValueOrDefault(k) + ev.Amount;
                    }
                    break;
                case BattleEventKind.Heal when ev.TargetId is int ht && !mine.ContainsKey(ht) && !beasts.Contains(ht): a.FoeHeal += ev.Amount; break;
                case BattleEventKind.Spark when ev.Text == SparkLabels.Release && ev.Turn <= 3: a.Light13 += ev.Amount; break;
            }
        }
        a.Stood = beasts.Count - phantoms.Count;
        if (som is not null && r.TallyByUnit.TryGetValue("som", out var mt))
        {
            a.Calls = mt.BeastCalls; a.Charged = mt.BeastCharged; a.SeatTaken = mt.BeastSeatTaken; a.Bursts = mt.BeastBursts; a.BurstSeat = mt.BeastBurstSeatTaken;
            a.BurstUnits = mt.BeastBurstUnits; a.BurstKills = mt.BeastBurstKills;
            for (int i = 0; i < 9; i++) { a.CallT[i] = mt.BeastCallT?[i] ?? 0; a.ChargedT[i] = mt.BeastChargedT?[i] ?? 0; }
            a.Lights = mt.SparkRainLights; a.Healed = mt.SparkHealed; a.VeilAdded = mt.SparkVeilAdded;
        }
        foreach (var u in p) if (r.TallyByUnit.TryGetValue(u.Def.Id, out var ut)) a.VeilSoaked += ut.SparkVeilSoaked;
        if (shiga is not null && r.TallyByUnit.TryGetValue("shiga", out var st)) { a.SwFires = st.SwFires; a.SwMultSum = st.SwMultSum; a.SwMultN = st.SwMultN; a.ShigaDmg = st.DamageToEnemy; }
        if (kata is not null && r.TallyByUnit.TryGetValue("kata", out var kt)) { a.KataN = 1; a.KataCloud = ThundercloudTrait.Of(kata); a.KataDmg = kt.DamageToEnemy; }
        return a;
    }

    internal static St Measure(Formation f, S287.Wave w, int n = Seeds)
    {
        var parts = new St[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, i));
        var all = new St();
        foreach (var x in parts) all.Merge(x);
        return all;
    }

    static int WinsLite(Formation f, S287.Wave w, int n, out long winT)
    {
        int wins = 0; long wt = 0;
        Parallel.For(0, n, s =>
        {
            var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
            if (r.PlayerWon) { Interlocked.Increment(ref wins); Interlocked.Add(ref wt, r.Turns); }
        });
        winT = wt;
        return wins;
    }

    static string FoeDmgLine(St a, Formation f)
    {
        var order = f.Occupied().Select(o => Short(o.Def)).Append("獣の放電").Append("刻み").Append("敵の側");
        return string.Join("・", order.Where(k => a.FoeDmg.GetValueOrDefault(k) > 0).Select(k => $"{k} {Per1(a.FoeDmg[k], a.Turns)}"));
    }
    static string Killers(St a) => a.BeastKillers.Count == 0 ? "—" : string.Join("・", a.BeastKillers.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per1(kv.Value, a.N)}"));

    // ---------------------------------------------------------------------------------
    // Phase 0（§3）—— 規定のまま
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var waves = Waves.Take(5).ToArray();   // ボス ／ 近衛 ／ 大隊 ／ 九体 新兵 ／ 九体 農兵
        var cells = new List<(string B, Formation F, S287.Wave W, St S)>();
        foreach (var (n, f) in Boards()) foreach (var w in waves) cells.Add((n, f, w, Measure(f, w)));

        Console.WriteLine("# 第310期 Phase 0（§3）—— 規定のソム（LV-a）のまま・seed 0..199・verbose");
        Console.WriteLine();
        foreach (var (n, f) in Boards()) Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine();

        Console.WriteLine("## §3-1 シガの主目標が獣だった割合（手番 ／ 割り込み）と SG-f（動けない敵の優先から獣を外す）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定: 手番 獣 ／ 振った | 割り込み 獣 ／ 振った | SG-f: 手番 獣 ／ 振った | 割り込み 獣 ／ 振った |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var (n, f) in Boards().Where(b => Has(b.F, Shiga)))
            foreach (var w in waves)
            {
                var a = cells.First(c => c.B == n && c.W == w).S;
                var g = Measure(V("SG-f").Apply(f), w);
                Console.WriteLine($"| {n} | {w.Name} | {Pct(a.ShigaOwnBeast, a.ShigaOwn)}（{Per1(a.ShigaOwnBeast, a.N)} ／ {Per1(a.ShigaOwn, a.N)}）| {Pct(a.ShigaIntBeast, a.ShigaInt)}（{Per1(a.ShigaIntBeast, a.N)} ／ {Per1(a.ShigaInt, a.N)}）"
                    + $"| {Pct(g.ShigaOwnBeast, g.ShigaOwn)}（{Per1(g.ShigaOwnBeast, g.N)} ／ {Per1(g.ShigaOwn, g.N)}）| {Pct(g.ShigaIntBeast, g.ShigaInt)}（{Per1(g.ShigaIntBeast, g.N)} ／ {Per1(g.ShigaInt, g.N)}）|");
            }
        Console.WriteLine();
        Console.WriteLine("（/戦。手番 ＝ シガの手番の一振りの主目標、割り込み ＝ 割り込みの鞭の主目標。SG-f の手番で獣が残るのは、獣が前列 ○前2 にいて無作為の候補に入るため）");

        Console.WriteLine();
        Console.WriteLine("## §3-2 獣の1戦（/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 決着T | 喚ぼうとした | 立った | 席が塞がって立てなかった | 獣が起点で弾けた（連鎖の大きさ） | 獣に振った味方の一撃 ／ 量 | 獣を倒した駒 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|---|--:|---|");
        foreach (var c in cells)
        {
            var a = c.S;
            Console.WriteLine($"| {c.B} | {c.W.Name} | {F1(a.Win)} | {Per1(a.Turns, a.N)} | {Per1(a.Calls, a.N)} | {Per1(a.Stood, a.N)} | {Per1(a.SeatTaken, a.N)} | {Per1(a.BeastPops, a.N)}（{Per1(a.BeastChainUnits, a.BeastPops)}） | {Per1(a.AtkOnBeast, a.N)} ／ {Per0(a.DmgOnBeast, a.N)} | {Killers(a)} |");
        }
        Console.WriteLine();
        Console.WriteLine("（喚ぼうとした ＝ 立っている獣がいない手番の喚び出し。席が塞がって ＝ 湧く席 ○前2 に生きている駒（死体を数えるのは規則が死体を数えるときだけ）。連鎖の大きさ ＝ 獣が深さ 0 の連鎖で弾けた駒の数（獣を含む））");

        Console.WriteLine();
        Console.WriteLine("## §3-3 喚んだ瞬間に、湧く席の隣の敵に帯電した駒がいた割合（E1 が弾ける見込み）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 全体 | T1 | T2 | T3 | T4 | T5 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var c in cells)
        {
            var a = c.S;
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Pct(a.Charged, a.Calls)}（{Per1(a.Calls, a.N)}） | " + string.Join(" | ", Enumerable.Range(1, 5).Select(t => a.CallT[t] == 0 ? "—" : $"{Pct(a.ChargedT[t], a.CallT[t])}")) + " |");
        }

        Console.WriteLine();
        Console.WriteLine("## §3-4 ボスの火力の帳簿（1ターンあたり・勇者の HP に届いた量）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 決着T | 味方の与ダメ /T（駒ごと） | 計 /T | 勇者の回復 /T | 差 /T | シガの割り込み /戦 ・ 鞭の倍率 | カタの雷雲（終わり） |");
        Console.WriteLine("|---|--:|--:|---|--:|--:|--:|---|--:|");
        foreach (var c in cells.Where(c => c.W.Boss))
        {
            var a = c.S;
            Console.WriteLine($"| {c.B} | {F1(a.Win)} | {Per1(a.Turns, a.N)} | {FoeDmgLine(a, c.F)} | {Per1(a.FoeDmgAll, a.Turns)} | {Per1(a.FoeHeal, a.Turns)} | {Sg((double)(a.FoeDmgAll - a.FoeHeal) / Math.Max(1, a.Turns))} | "
                + (a.SwFires + a.SwMultN > 0 ? $"{Per1(a.SwFires, a.N)} ・ ×{Per1(a.SwMultSum, a.SwMultN)}" : "—") + $" | {(a.KataN > 0 ? Per1(a.KataCloud, a.KataN) : "—")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-1 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cells = new List<(string B, Formation F0, S287.Wave W, Ver V, St S)>();
        foreach (var (n, f) in Boards())
            foreach (var v in Vers)
            {
                if (v.ShigaOnly && !Has(f, Shiga)) continue;
                var fv = v.Apply(f);
                foreach (var w in Waves) cells.Add((n, f, w, v, Measure(fv, w)));
            }
        // 対照: 勝率 50% 以上のセルで、ソム → ドルガ（同じ席）
        var ctl = new Dictionary<(string, string, string), double>();
        foreach (var c in cells.Where(c => c.S.Win >= 50))
        {
            var fv = c.V.Apply(c.F0);
            ctl[(c.B, c.W.Name, c.V.Name)] = 100.0 * WinsLite(S309.Swap(fv, fv.Occupied().First(o => o.Def.Id == "som").Def, UnitCatalog.Dolga), c.W, Seeds, out _) / Seeds;
        }

        Console.WriteLine("# 第310期 §4-1 代表台 × 波 × 版（規定 ＝ LV-a ／ E1 ／ E2 ／ SG-f・seed 0..199・verbose）");
        Console.WriteLine();
        foreach (var (n, f) in Boards()) Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）{(Has(f, Shiga) ? "・シガあり" : "")}");
        Console.WriteLine("- 九体の波 ＝ 検証・九 / 新兵 ／ 農兵（既定の倍率 115/115）");
        Console.WriteLine();
        Console.WriteLine("## 表A 勝率（括弧は規定との差・[ ] は ソム → ドルガ（勝率 50% 以上のセルだけ））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")));
        foreach (var (n, _) in Boards())
            foreach (var v in Vers)
            {
                if (!cells.Any(c => c.B == n && c.V == v)) continue;
                Console.WriteLine($"| {n} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var c = cells.First(x => x.B == n && x.V == v && x.W == w);
                    var b = cells.First(x => x.B == n && x.V == Vers[0] && x.W == w);
                    string d = v == Vers[0] ? "" : $"（{Sg(c.S.Win - b.S.Win)}）";
                    string k = ctl.TryGetValue((n, w.Name, v.Name), out var cv) ? $" [{F1(cv)}]" : "";
                    return $"{F1(c.S.Win)}{d}{k}";
                })) + " |");
            }

        foreach (var w in Waves)
        {
            Console.WriteLine();
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 勝率 | 倒しT | 負けT（上限） | 寿命 | ソム 倒れたT | 喚ぼうとした ／ 弾けた（うち席で）／ 立った | 弾けの連鎖の大きさ ・ 倒した | 獣に振った一撃 ・ 獣を倒した駒 | シガ 割り込み ・ ×倍率 ・ 主目標が獣 ・ 与ダメ | カタ 雷雲 ・ 与ダメ | 光 ・ 衣になった ・ 受け止めた |"
                + (IsNine(w) ? " 初めて喚べたT ・ T1〜3 の光 |" : "") + (w.Boss ? " 味方の与ダメ /T ・ 勇者の回復 /T ・ 差 |" : ""));
            Console.WriteLine("|---|---|--:|--:|--:|---|---|---|---|---|---|---|---|" + (IsNine(w) ? "---|" : "") + (w.Boss ? "---|" : ""));
            foreach (var c in cells.Where(x => x.W == w))
            {
                var a = c.S;
                string beast = $"{Per1(a.Calls, a.N)} ／ {Per1(a.Bursts, a.N)}（{Per1(a.BurstSeat, a.N)}）／ {Per1(a.Stood, a.N)}";
                string burst = a.Bursts == 0 ? "—" : $"{Per1(a.BurstUnits, a.Bursts)} ・ {Per1(a.BurstKills, a.N)}";
                string sh = Has(c.V.Apply(c.F0), UnitCatalog.Shiga) || Has(c.V.Apply(c.F0), UnitCatalog.ShigaSGf)
                    ? $"{Per1(a.SwFires, a.N)} ・ ×{Per1(a.SwMultSum, a.SwMultN)} ・ {Pct(a.ShigaOwnBeast + a.ShigaIntBeast, a.ShigaOwn + a.ShigaInt)} ・ {Per0(a.ShigaDmg, a.N)}" : "—";
                string ka = a.KataN > 0 ? $"{Per1(a.KataCloud, a.KataN)} ・ {Per0(a.KataDmg, a.N)}" : "—";
                Console.WriteLine($"| {c.B} | {c.V.Name} | {F1(a.Win)} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)}{(a.Caps > 0 ? $"（{Pct(a.Caps, a.N)}）" : "")} | {Dt(a.FirstDeathN, a.FirstDeathT, a.N)} | {Dt(a.SomDied, a.SomDeathT, a.N)} | {beast} | {burst} | "
                    + $"{Per1(a.AtkOnBeast, a.N)} ・ {Killers(a)} | {sh} | {ka} | {Per1(a.Lights, a.N)} ・ {Per0(a.VeilAdded, a.N)} ・ {Per0(a.VeilSoaked, a.N)} |"
                    + (IsNine(w) ? $" {Per1(a.FirstCallT, a.FirstCallN)} ・ {Per1(a.Light13, a.N)} |" : "")
                    + (w.Boss ? $" {Per1(a.FoeDmgAll, a.Turns)}（{FoeDmgLine(a, c.V.Apply(c.F0))}）・ {Per1(a.FoeHeal, a.Turns)} ・ {Sg((double)(a.FoeDmgAll - a.FoeHeal) / Math.Max(1, a.Turns))} |" : ""));
            }
        }

        // §4-4 の門（格子を回す版）
        Console.WriteLine();
        Console.WriteLine("## §4-4 格子を回す版（代表台のボスで規定から +5pt 以上、または精鋭で +10pt 以上動いた版）");
        Console.WriteLine();
        foreach (var v in Vers.Skip(1))
        {
            var moved = new List<string>();
            foreach (var c in cells.Where(x => x.V == v && (x.W.Boss || x.W.Name is "近衛" or "大隊")))
            {
                var b = cells.First(x => x.B == c.B && x.V == Vers[0] && x.W == c.W);
                double d = c.S.Win - b.S.Win;
                if (c.W.Boss ? d >= 5 : d >= 10) moved.Add($"{c.B} × {c.W.Name}（{Sg(d)}）");
            }
            Console.WriteLine($"- {v.Name}: {(moved.Count == 0 ? "**回さない**（該当なし）" : $"**回す**（{string.Join("・", moved)}）")}");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-2 `compare` 64 行 × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().Select(r => (r.Name, F: Pin310(r.F))).ToArray();   // 第311期: ソムは第310期の規定に
        int nw = EnemyCatalog.Stages.Count;
        var grids = Vers.Select(v =>
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = v.Apply(rows[ri].F);
                int wins = 0;
                for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / Seeds;
            });
            return g;
        }).ToArray();

        Console.WriteLine("# 第310期 §4-2 `compare` 64 行 × 版（seed 0..199・版の駒が在席する行だけ差し替わる・基準は規定）");
        Console.WriteLine();
        // 基準の列が docs/balance.md と一致するか（規定は動かさない）
        var bal = File.Exists("docs/balance.md") ? File.ReadAllText("docs/balance.md") : "";
        Console.WriteLine($"規定の列: `docs/balance.md` に行名がある行 {rows.Count(r => bal.Contains(r.Name, StringComparison.Ordinal))} ／ {rows.Length}（勝率の一致は check の (h) で見る）");
        double M(double[,] g, int ri) => Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
        for (int vi = 1; vi < Vers.Length; vi++)
        {
            var v = Vers[vi]; var g = grids[vi]; var b = grids[0];
            Console.WriteLine();
            Console.WriteLine($"## {v.Name}");
            Console.WriteLine();
            Console.WriteLine("| 行 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | 差 | 最大の落ち |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|--:|");
            int movedCells = 0, movedRows = 0, outside = 0;
            var drops = new List<int>();
            for (int ri = 0; ri < rows.Length; ri++)
            {
                bool any = Enumerable.Range(0, nw).Any(w => g[ri, w] != b[ri, w]);
                bool holds = !ReferenceEquals(v.Apply(rows[ri].F), rows[ri].F) && !v.Apply(rows[ri].F).Occupied().Select(o => o.Def).SequenceEqual(rows[ri].F.Occupied().Select(o => o.Def));
                if (any && !holds) outside++;
                if (!any) continue;
                movedRows++;
                movedCells += Enumerable.Range(0, nw).Count(w => g[ri, w] != b[ri, w]);
                double drop = Enumerable.Range(0, nw).Min(w => g[ri, w] - b[ri, w]);
                if (drop <= -10) drops.Add(ri);
                Console.WriteLine($"| {rows[ri].Name} | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => g[ri, w] == b[ri, w] ? F1(g[ri, w]) : $"{F1(g[ri, w])}（{Sg(g[ri, w] - b[ri, w])}）")) + $" | {F1(M(g, ri))} | {Sg(M(g, ri) - M(b, ri))} | {Sg(drop)} |");
            }
            if (movedRows == 0) Console.WriteLine("| （動いた行なし） |" + string.Concat(Enumerable.Range(0, nw + 3).Select(_ => " |")));
            Console.WriteLine();
            var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
            double p5b = pr.Average(ri => b[ri, nw - 1]), p5 = pr.Average(ri => g[ri, nw - 1]);
            double all = Enumerable.Range(0, rows.Length).Average(ri => M(g, ri)), allB = Enumerable.Range(0, rows.Length).Average(ri => M(b, ri));
            Console.WriteLine($"動いた行 {movedRows} ／ セル {movedCells}・版の駒がいない行で動いたセル **{outside}**（0 であること）・全64行の第2〜5波平均 {F1(allB)} → {F1(all)}（{Sg(all - allB)}）・主判定19行の第五波 {F1(p5b)} → {F1(p5)}（歯止め {F1(Baseline.PrimaryFifthFloor)}）");
            // (G2)
            if (drops.Count == 0) { Console.WriteLine("(G2): −10.0pt 以上落ちた行なし → 判定の対象なし。"); continue; }
            Console.WriteLine("(G2): −10.0pt 以上落ちた行ごとに、その行の駒それぞれの「その駒を含む他の行」の第2〜5波平均の変化:");
            foreach (int ri in drops)
            {
                var defs = rows[ri].F.Occupied().Select(o => o.Def).Distinct().ToList();
                var parts = new List<string>(); bool broken = false;
                foreach (var d in defs)
                {
                    var others = Enumerable.Range(0, rows.Length).Where(rj => rj != ri && rows[rj].F.Occupied().Any(o => ReferenceEquals(o.Def, d))).ToList();
                    if (others.Count == 0) { parts.Add($"{Short(d)} 他の行 0（分解が成立しない）"); continue; }
                    double ch = others.Average(rj => M(g, rj) - M(b, rj));
                    if (ch <= -3.0) broken = true;
                    parts.Add($"{Short(d)} {Sg(ch)}（{others.Count} 行）");
                }
                Console.WriteLine($"- {rows[ri].Name}: {string.Join("・", parts)} → **{(broken ? "壊れ" : "編成上の制約")}**");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-3 ソム ↔ ツギ × 版（第309期 §5-1 と同じ入れ替え・同じ 8 波）
    // ---------------------------------------------------------------------------------
    static void SwapAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var waves = S307.Waves;
        var pairs = S309.Pairs();
        Console.WriteLine("# 第310期 §4-3 ソム ↔ ツギ × 版（第309期 §5-1 と同じ台・同じ席・同じ 8 波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("ソムの側は版を当てた台、ツギの側は規定（SG-f はシガのいる台なら両側のシガを SG-f に替えて回し直す）。差 ＝ ソム − ツギ。");
        foreach (var v in Vers)
        {
            Console.WriteLine();
            Console.WriteLine($"## {v.Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "---|")));
            int plus = 0, minus = 0, n2 = 0; var minusCells = new List<string>();
            foreach (var (n, _, s, t) in pairs)
            {
                if (v.ShigaOnly && !Has(s, Shiga)) continue;
                var fs = v.Apply(s); var ft = t is null ? null : v.ShigaOnly ? v.Apply(t) : t;
                var line = new List<string>();
                foreach (var w in waves)
                {
                    double a = 100.0 * WinsLite(fs, w, Seeds, out _) / Seeds;
                    if (ft is null) { line.Add($"{F1(a)} ／ —"); continue; }
                    double b = 100.0 * WinsLite(ft, w, Seeds, out _) / Seeds;
                    line.Add($"{F1(a)} ／ {F1(b)} ／ **{Sg(a - b)}**");
                    if (w.Name == "本編 第1波") continue;
                    n2++;
                    if (a - b >= 10) plus++;
                    if (a - b <= -10) { minus++; minusCells.Add($"{n} × {w.Name}（{Sg(a - b)}）"); }
                }
                Console.WriteLine($"| {n} | {string.Join(" | ", line)} |");
            }
            Console.WriteLine();
            Console.WriteLine($"§4-3 目安（本編 第1波を除く {n2} セル）: ソム − ツギ ≥ +10pt **{plus}** ／ ≤ −10pt **{minus}** → **{(plus > minus ? "満たす" : "満たさない")}**");
            if (minusCells.Count > 0) Console.WriteLine($"- −10pt 以下: {string.Join("・", minusCells)}");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-4 ソムの格子（第309期と同じ作り・同じ候補・ソムの側だけを版で）
    // ---------------------------------------------------------------------------------
    static void Grid(string ver, string wave)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var v = V(ver);
        var w = S307.WaveOf(wave);
        UnitDef[] fixedBase = w.Boss ? new[] { UnitCatalog.Kugu, UnitCatalog.Kata } : new[] { UnitCatalog.Tou };
        var pool = w.Boss ? S307.BossPool : S307.ElitePool;
        int k = w.Boss ? 2 : 3;
        int need = w.Boss ? 1 : 5;
        var lu = S293.Combos(pool, k);
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedBase.Append(Som).Concat(t).ToArray()));
        var res = new (int Wins, int Ctl)?[boards.Count];
        int cutPass = 0;
        static int W(Formation f, S287.Wave w, int n)
        {
            int wins = 0;
            for (int s = 0; s < n; s++) if (BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false).PlayerWon) wins++;
            return wins;
        }
        Parallel.For(0, boards.Count, i =>
        {
            var f = v.Apply(B283.Seat(boards[i]));
            if (W(f, w, S307.CutSeeds) < need) return;
            Interlocked.Increment(ref cutPass);
            int wins = W(f, w, Seeds);
            int ctl = -1;
            if (wins * 2 >= Seeds) { var som = f.Occupied().First(o => o.Def.Id == "som").Def; ctl = W(S309.Swap(f, som, UnitCatalog.Dolga), w, Seeds); }
            res[i] = (wins, ctl);
        });
        var all = res.Where(r => r is not null).Select(r => r!.Value).ToList();
        int half = all.Count(r => r.Wins * 2 >= Seeds), reach = all.Count(r => r.Wins * 2 >= Seeds && r.Ctl >= 0 && r.Ctl * 2 <= r.Wins);
        Console.WriteLine($"# 第310期 §4-4 ソムの格子 × {w.Name} × 版 {v.Name}（固定枠 {S307.OrderName(fixedBase)} ＋ ソム・探索枠{k}・{boards.Count:N0} 台）");
        Console.WriteLine();
        Console.WriteLine($"足切り seed 0..{S307.CutSeeds - 1} で {need} 勝以上（{cutPass:N0} 台）→ seed 0..{Seeds - 1} → 勝率 50% 以上でソム → ドルガ。");
        Console.WriteLine();
        Console.WriteLine($"| 版 | 勝率 ≧ 50% | **届いた（ソム → ドルガで半分以下）** | 勝率 ≧ 90% | 勝率 ＞ 0% | 最大の勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        Console.WriteLine($"| {v.Name} | {half:N0} | {reach:N0} | {all.Count(r => r.Wins * 10 >= Seeds * 9):N0} | {all.Count(r => r.Wins > 0):N0} | {(all.Count == 0 ? "—" : F1(100.0 * all.Max(r => r.Wins) / Seeds))} |");
        Console.WriteLine();
        Console.WriteLine("規定のソム ／ ツギの数は第309期の報告（`design/PHASE309_SOM_REGULATE.md` §5-2）を使う。");
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 台本の抜粋
    // ---------------------------------------------------------------------------------
    static void Memo(string ver, string part, string wave, int seed, int t0, int t1)
    {
        var v = V(ver);
        var (name, f0) = Boards().First(b => b.Name.Contains(part, StringComparison.Ordinal));
        var f = v.Apply(f0);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        foreach (var ev in r.Events.Where(x => x.Kind is BattleEventKind.Summon or BattleEventKind.SilkBall && x.TargetId is int)) names.TryAdd(ev.TargetId!.Value, ev.Kind == BattleEventKind.SilkBall ? "糸玉" : "背いた獣");
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# som310 memo —— {name}（{v.Name}・{Seats(f)}）× {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}・T{t0}〜T{t1}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        for (int i = 0; i < r.Events.Count; i++)
        {
            var x = r.Events[i];
            if (x.Turn < t0 || x.Turn > t1) continue;
            if (x.Kind is BattleEventKind.StatSnapshot or BattleEventKind.StatusSnapshot) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"#{i,-4} {x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  hp={x.HpAfter}  Slot {x.Slot}{(x.Pattern is { } pt ? $"  {pt}" : "")}{(x.Reaction ? "  Reaction" : "")}{(x.FriendlyFire ? "  ff" : "")}");
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査（§5）
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    sealed class Ck
    {
        public long Calls, Charged, Bursts, BurstSeat, Stood, PhantomDeaths, PhantomInUnits, RootSom, RootOther, SomKills, LightSummon, Fights, BurstBeforeEnd;
        public void Add(Ck o)
        {
            Calls += o.Calls; Charged += o.Charged; Bursts += o.Bursts; BurstSeat += o.BurstSeat; Stood += o.Stood; PhantomDeaths += o.PhantomDeaths; PhantomInUnits += o.PhantomInUnits;
            RootSom += o.RootSom; RootOther += o.RootOther; SomKills += o.SomKills; LightSummon += o.LightSummon; Fights += o.Fights; BurstBeforeEnd += o.BurstBeforeEnd;
        }
    }

    static Ck CheckFight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var c = new Ck { Fights = 1 };
        var som = p.First(u => u.Def.Id == "som");
        var phantoms = r.Events.Where(x => x.Kind == BattleEventKind.BeastBurst).Select(x => x.TargetId!.Value).ToHashSet();
        var units = p.Concat(e).Select(u => u.InstanceId).ToHashSet();
        c.Stood = r.Events.Count(x => x.Kind == BattleEventKind.Summon && x.ActorId == som.InstanceId && !phantoms.Contains(x.TargetId!.Value));
        c.PhantomDeaths = r.Events.Count(x => x.Kind == BattleEventKind.Death && x.TargetId is int t && phantoms.Contains(t));
        c.PhantomInUnits = phantoms.Count(units.Contains);
        foreach (var x in r.Events.Where(x => x.Kind == BattleEventKind.ShockSpent && x.Slot == 0 && x.TargetId is int t && phantoms.Contains(t)))
            if (x.ActorId == som.InstanceId) c.RootSom++; else c.RootOther++;
        // 弾けの直後（同じ連鎖の中）に、ソムを撃破者とする敵の死が出たか
        bool inBurst = false;
        foreach (var x in r.Events)
        {
            if (x.Kind == BattleEventKind.ShockSpent && x.Slot == 0) inBurst = x.TargetId is int t && phantoms.Contains(t);
            if (inBurst && x.Kind == BattleEventKind.Death && x.ActorId == som.InstanceId) c.SomKills++;
        }
        if (r.TallyByUnit.TryGetValue("som", out var mt))
        {
            c.Calls = mt.BeastCalls; c.Charged = mt.BeastCharged; c.Bursts = mt.BeastBursts; c.BurstSeat = mt.BeastBurstSeatTaken; c.LightSummon = mt.SparkLightSummon;
        }
        return c;
    }

    static string? GitShow(string spec)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", $"show {spec}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
            using var pr = System.Diagnostics.Process.Start(psi)!;
            string o = pr.StandardOutput.ReadToEnd();
            pr.WaitForExit();
            return pr.ExitCode == 0 ? o : null;
        }
        catch { return null; }
    }

    static Ck CheckMany(Formation f, S287.Wave w, int n)
    {
        var parts = new Ck[n];
        Parallel.For(0, n, s => parts[s] = CheckFight(f, w, s));
        var all = new Ck();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# som310 check —— 第310期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var som = UnitCatalog.SomH310;   // 第311期: 第310期の規定は `SomH310`

        // (a) 版の定義
        {
            bool ok = UnitCatalog.SomE1.Traits.SequenceEqual(som.Traits.Append(TraitId.BeastBurstCharged)) && UnitCatalog.SomE2.Traits.SequenceEqual(som.Traits.Append(TraitId.BeastBurstAlways))
                && UnitCatalog.ShigaSGf.Traits.SequenceEqual(UnitCatalog.Shiga.Traits.Append(TraitId.ShameSkipFodder))
                && new[] { UnitCatalog.SomE1, UnitCatalog.SomE2 }.All(d => d.Id == som.Id && d.MaxHp == som.MaxHp && d.Attack == som.Attack && d.Speed == som.Speed && d.Advances == som.Advances && d.Pattern == som.Pattern)
                && UnitCatalog.ShigaSGf.Id == "shiga" && UnitCatalog.ShigaSGf.MaxHp == UnitCatalog.Shiga.MaxHp && UnitCatalog.ShigaSGf.Attack == UnitCatalog.Shiga.Attack && UnitCatalog.ShigaSGf.Speed == UnitCatalog.Shiga.Speed
                && !UnitCatalog.Everyone.Contains(UnitCatalog.SomE1) && !UnitCatalog.Everyone.Contains(UnitCatalog.SomE2) && !UnitCatalog.Everyone.Contains(UnitCatalog.ShigaSGf)
                && UnitCatalog.All.Contains(UnitCatalog.Som) && !UnitCatalog.Everyone.Contains(som) && UnitCatalog.All.Contains(UnitCatalog.Shiga)
                && UnitCatalog.Fodder.MaxHp == 12 && UnitCatalog.Fodder.Speed == 1;
            Expect("(a) 版 ＝ 規定の末尾に札1枚（E1 `BeastBurstCharged` ／ E2 `BeastBurstAlways` ／ SG-f `ShameSkipFodder`）・数値は規定のまま・版は `Everyone` の外・獣の定義は触らない", ok);
        }
        var nine = Waves[3];
        var boss = Waves[0];
        var shock = Row("感電 (シガ×カタ×ソム)");
        // (b) E1
        {
            var a = new Ck();
            foreach (var (_, f) in Boards()) foreach (var w in new[] { boss, Waves[1], nine }) a.Add(CheckMany(V("E1").Apply(f), w, 8));
            bool ok = a.Bursts > 0 && a.Bursts == a.Charged && a.Stood > 0 && a.Stood <= a.Calls - a.Charged;
            Expect("(b) E1: 隣に帯電した敵がいれば弾けて消え（弾けた ＝ 帯電した隣がいた喚び出し）、いなければ立つ（立った ≦ 帯電した隣がいなかった喚び出し）", ok,
                $"喚んだ {a.Calls}・帯電した隣 {a.Charged}・弾けた {a.Bursts}・立った {a.Stood}（代表台 6 × ボス ／ 近衛 ／ 九体 新兵 × seed 0..7）");
        }
        // (c) E2
        {
            var a = new Ck();
            foreach (var (_, f) in Boards()) foreach (var w in new[] { boss, Waves[1], nine }) a.Add(CheckMany(V("E2").Apply(f), w, 8));
            Expect("(c) E2: いつも弾けて消える（弾けた ＝ 喚ぼうとした・立った獣 0）", a.Bursts > 0 && a.Bursts == a.Calls && a.Stood == 0, $"喚んだ {a.Calls}・弾けた {a.Bursts}・立った {a.Stood}");
        }
        // (d) 倒れた扱いにならない・起点と撃破はソム・光になる
        {
            var a = new Ck();
            foreach (var v in new[] { V("E1"), V("E2") }) foreach (var (_, f) in Boards()) foreach (var w in new[] { boss, Waves[1], Waves[2], nine }) a.Add(CheckMany(v.Apply(f), w, 6));
            Expect("(d-1) 弾けた獣は倒れた扱いにならない（`Death` 0・盤面の駒の列に入らない）", a.PhantomDeaths == 0 && a.PhantomInUnits == 0 && a.Bursts > 0, $"弾けた {a.Bursts}・Death {a.PhantomDeaths}・列の中 {a.PhantomInUnits}");
            Expect("(d-2) 弾けの連鎖の起点（`ShockSpent` 深さ 0 の `ActorId`）はソム・その連鎖の撃破者もソム", a.RootSom == a.Bursts && a.RootOther == 0 && a.SomKills > 0, $"起点 ソム {a.RootSom} ／ ほか {a.RootOther}・ソムの撃破 {a.SomKills}");
            Expect("(d-3) 獣の弾けは光になる（光の燃料の「喚ばれたもの」≧ 弾けた数）", a.LightSummon >= a.Bursts, $"喚ばれたものの光 {a.LightSummon} ／ 弾けた {a.Bursts}");
        }
        // (e) 死亡の読み手が起きない（E2 ＋ 追い打ちのハギ ／ 墓守 を同じ台に置いても、ソムの弾けで獣の死の通知が流れない）——(d-1) の `Death` 0 が本体。
        //     ここでは「弾けで倒れていない敵の数」が `HandleDeath` の数と一致することを、帳簿（倒した）と `Death` の数で突き合わせる。
        {
            long kills = 0, deaths = 0;
            foreach (var w in new[] { boss, Waves[1], nine })
                for (int s = 0; s < 6; s++)
                {
                    var p = BattleEngine.Materialize(V("E2").Apply(Playtest(S309.Shield)), BattleContext.PlayerTeam);
                    var e = w.Make();
                    var r = BattleEngine.Run(p, e, s, verbose: true);
                    var foes = e.Select(u => u.InstanceId).ToHashSet();
                    deaths += r.Events.Count(x => x.Kind == BattleEventKind.Death && x.TargetId is int t && foes.Contains(t));
                    kills += e.Count(u => !u.IsAlive);
                }
            Expect("(e) 敵の `Death` の数 ＝ 倒れた敵の数（弾けた獣が通知に混ざらない）", kills == deaths && kills > 0, $"{deaths} ／ {kills}（光の盾 E2 × 3 波 × seed 0..5）");
        }
        // (f) 空き席が無いとき、召喚枠の席で弾ける（E1 は帯電した隣がいるときだけ）・規定はそのとき喚べない
        {
            var e1 = CheckMany(V("E1").Apply(shock), nine, 30); var e2 = CheckMany(V("E2").Apply(shock), nine, 30);
            var b0 = Measure(shock, nine, 30);
            Expect("(f) 九体の波（湧く席が埋まっている）で E1 ／ E2 は席で弾ける・E1 の席の弾けは帯電した隣があるときだけ（弾けた ＝ 帯電）・規定はその間立てない",
                e2.BurstSeat > 0 && e1.BurstSeat > 0 && e1.Bursts == e1.Charged && b0.SeatTaken > 0,
                $"E1 席で {e1.BurstSeat} ／ 弾けた {e1.Bursts}・E2 席で {e2.BurstSeat} ／ 弾けた {e2.Bursts}・規定の塞がり {b0.SeatTaken}（感電 行 × 九体 新兵 × seed 0..29）");
        }
        // (g) SG-f: 優先の判定に獣が入らない・ほかの動けない敵はそのまま
        {
            var ctx = new BattleContext(0, false);
            var add = typeof(BattleContext).GetMethod("Add", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            var foes = BattleEngine.MaterializeEnemy(EnemyCatalog.TestStages[0].Enemy, EnemyScaleRule.Default).Take(2).ToList();
            foreach (var u in foes) add.Invoke(ctx, new object[] { u });
            var beast = ctx.Summon(UnitCatalog.Fodder, BattleContext.EnemyTeam, BetrayedTrait.FodderSlot, overCorpse: true)!;
            foreach (var u in ctx.AllUnits) u.SetCounter(StatusKeys.IdleTurn, 99);   // ターン 0 の盤なので「このターンに手番を空けた」を外す
            beast.SetCounter(StatusKeys.Stun, 1);
            var pool1 = new List<UnitState> { beast, foes[0] };
            bool onlyBeast = ShameTrait.Preferred(ctx, pool1) == beast && ShameTrait.PreferredSkipFodder(ctx, pool1) is null;
            foes[0].SetCounter(StatusKeys.Stun, 1);
            bool other = ShameTrait.PreferredSkipFodder(ctx, pool1) == foes[0];
            // 実戦: SG-f の割り込みの鞭の主目標が獣で、そのとき動けない敵が別にいた、は数えられないので、版の差を数で見る
            var b = Measure(shock, Waves[1], 40); var g = Measure(V("SG-f").Apply(shock), Waves[1], 40);
            Expect("(g) SG-f: 見せしめの優先に獣が入らない（獣だけが動けないとき優先は空・ほかの動けない敵は選ばれる）・実戦で割り込みの主目標が獣になる割合が規定以下",
                onlyBeast && other && (double)g.ShigaIntBeast / Math.Max(1, g.ShigaInt) <= (double)b.ShigaIntBeast / Math.Max(1, b.ShigaInt),
                $"割り込みの主目標が獣 規定 {Pct(b.ShigaIntBeast, b.ShigaInt)} ／ SG-f {Pct(g.ShigaIntBeast, g.ShigaInt)}（感電 行 × 近衛 × seed 0..39）");
        }
        // (h) 規定は動かない（`compare` の規定の列 ＝ docs/balance.md の一部を抜き取りで）
        {
            var rows = CompareBuilds().Select(r => (r.Name, F: Pin310(r.F))).ToArray();
            // 第311期: 第310期の `docs/balance.md`（コミット e8c8821）と照らす（段0 で `感電` 行が動いたので、いまの docs とは照らさない）
            var bal = GitShow("e8c8821:docs/balance.md")?.Split('\n').Select(l => l.TrimEnd('\r')).ToArray() ?? Array.Empty<string>();
            int checkedRows = 0, bad = 0;
            foreach (var (n, f) in rows.Where(r => Has(r.F, som) || Has(r.F, UnitCatalog.Shiga)))
            {
                var line = bal.FirstOrDefault(l => l.StartsWith($"| {n} |", StringComparison.Ordinal));
                if (line is null) continue;
                checkedRows++;
                var cellsTxt = line.Split('|', StringSplitOptions.TrimEntries).Skip(2).Take(EnemyCatalog.Stages.Count).ToArray();
                for (int wi = 0; wi < EnemyCatalog.Stages.Count; wi++)
                {
                    int wins = 0;
                    for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                    string want = (100.0 * wins / Seeds).ToString("F1");
                    if (wi >= cellsTxt.Length || !cellsTxt[wi].StartsWith(want, StringComparison.Ordinal)) bad++;
                }
            }
            Expect("(h) 規定は動かない: ソム ／ シガのいる `compare` の行の勝率が `docs/balance.md` と一致", checkedRows > 0 && bad == 0, $"{checkedRows} 行・不一致 {bad} セル");
        }
        // (i) 決定的・verbose の有無で勝敗と決着T が同じ
        {
            int nd = 0, n = 0;
            foreach (var v in Vers) foreach (var (_, f) in Boards())
                {
                    if (v.ShigaOnly && !Has(f, UnitCatalog.Shiga)) continue;
                    foreach (var w in Waves)
                        for (int sd = 0; sd < 2; sd++)
                        {
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(v.Apply(f), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(v.Apply(f), BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            n++;
                            if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) nd++;
                        }
                }
            Expect("(i) 決定的・verbose の有無で勝敗と決着T が変わらない（版 × 代表台 × 10 波）", nd == 0, $"{nd} ／ {n} 件");
        }
        // (j) 乱数
        {
            static int Count(string text, string pat) { int n = 0, k = 0; while ((k = text.IndexOf(pat, k, StringComparison.Ordinal)) >= 0) { n++; k += pat.Length; } return n; }
            static string? Head(string path)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 502e562:{path}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
                    using var pr = System.Diagnostics.Process.Start(psi)!;
                    string o = pr.StandardOutput.ReadToEnd();
                    pr.WaitForExit();
                    return pr.ExitCode == 0 ? o : null;
                }
                catch { return null; }
            }
            var notes = new List<string>(); bool clean = true;
            foreach (var path in new[] { "BattleCore/BattleEngine.cs", "BattleCore/Traits.cs" })
            {
                string now = File.ReadAllText(path); string? head = Head(path);
                if (head is null) { clean = false; notes.Add($"{path}: 第309期の版を読めない"); continue; }
                foreach (var pat in new[] { "PickOne(", "Roll(" }) { int a0 = Count(head, pat), a1 = Count(now, pat); if (a1 > a0) clean = false; notes.Add($"{Path.GetFileName(path)} {pat} {a0}→{a1}"); }
            }
            Expect("(j) `PickOne(` ／ `Roll(` を新たに使っていない（BattleCore の出現数が第309期のコミット 502e562 以下）", clean, string.Join("・", notes));
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
