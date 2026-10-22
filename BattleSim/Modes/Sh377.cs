using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;

// =====================================================================================
// sh377 —— 第377期【軽い期】本体1: 死軸の棚卸し（数えるだけ）／ 本体2: 亡者と増殖（版 R-繋 ／ R-積 ／ M ／ R-積M）。
// 指示書は design/PHASE377_DEATH_INVENTORY_SPEC.md（2026-10-22 に差し替えた第2版）／ 報告は design/PHASE377_DEATH_INVENTORY.md。規定は1つも変えない。
//
//     dotnet run --project BattleSim -c Release 0 sh377 inv           # §2-2: 死の台 5 × 本編 第5波 ／ 近衛 ／ 大隊 ／ ボス（seed 0..19・verbose）——誰が何Tに倒れたか・死の数・層・出た量・ボスの全滅の理由・勝ち方の質・リィカの火力の頭打ち（ボス ／ 的・一 ／ 的・九）
//     dotnet run --project BattleSim -c Release 0 sh377 attrib        # §2-3: ツギなしの死の台 4 × 各駒を素体に替えた差 × 4 波（seed 0..99）＋ `docs/roster_audit.md` の理想台の帰属
//     dotnet run --project BattleSim -c Release 0 sh377 vers          # §3-4: 死の台 5 × 版（規定 ／ R-繋 ／ R-積 ／ M ／ R-積M）× 8 物差し（seed 0..99）＋ 火力（的・一 ／ 的・九 8T）
//     dotnet run --project BattleSim -c Release 0 sh377 mix           # §3-4: 寄せ集めの台 50（リィカ ＋ 死の札を持たない4枚・席もランダム・乱数は固定）× 素体 ／ 規定 ／ R-繋 ／ R-積 × 8 物差し（seed 0..19）
//     dotnet run --project BattleSim -c Release 0 sh377 compare <R-繋|R-積|M|R-積M>   # `compare` 64 行 × 版（その版が替える駒のいる行だけを回す・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh377 row <行名>     # `compare` の1行 × 版 × 本編 第2〜5波（動いた理由を見る・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh377 check         # 自己検査
//
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p377/predict.md`）。素体の替え（§2-3）と寄せ集めの台は器具の中だけ。
// =====================================================================================
static partial class Sh377Diag
{
    internal const string PredictFile = ".tmp/p377/predict.md";
    const string Tag = "第377期";
    internal const int LoopT = 10, CompareSeeds = 200;
    const int TT = BattleEngine.MaxTurns + 2;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "inv" or "attrib" or "vers" or "mix" or "compare" or "row" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh377: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "inv": Inventory(); return;
            case "attrib": Attrib(); return;
            case "vers": Versions(); return;
            case "mix": Mix(); return;
            case "compare": CompareVer(args.Length > 3 ? args[3] : "R-繋"); return;
            case "row": RowDetail(string.Join(" ", args.Skip(3))); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh377: モードは inv / attrib / vers / mix / compare / row / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => x.ToString("#,0");
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Nm(string id) => id switch
    {
        "spore" => "胞子", "wight" => "亡者",
        _ => UnitCatalog.Everyone.FirstOrDefault(d => d.Id == id) is { } d ? S307.Short(d) : id,
    };

    // ---------------------------------------------------------------------------------
    // 台 ／ 版 ／ 波
    // ---------------------------------------------------------------------------------
    internal static readonly string[] DeathRows = { "死の連鎖 (リィカ軸)", "惨禍×死の連鎖", "追撃×死 (ハギ×リィカ)", "死の連鎖+後備え" };
    internal const string TsugiRow = "継ぎ当て×死の連鎖";
    internal static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;
    internal static (string Name, Formation F)[] Boards(bool tsugi) =>
        (tsugi ? DeathRows.Append(TsugiRow) : DeathRows).Select(n => (n, Row(n))).ToArray();

    internal static readonly string[] VerNames = { "規定", "R-繋", "R-積", "M", "R-積M" };
    internal static bool HasRica(Formation f) => f.Occupied().Any(o => o.Def.Id == "rica");
    internal static bool HasMug(Formation f) => f.Occupied().Any(o => o.Def.Id == "mug");
    /// <summary>版を当てる（R-繋 ／ R-積 はリィカ・M はムグ・R-積M は両方を版の駒に替える）。</summary>
    internal static Formation Apply(Formation f, int vi)
    {
        var g = f;
        if (vi == 1) g = I.Swap(g, "rica", UnitCatalog.RicaLink);
        if (vi is 2 or 4) g = I.Swap(g, "rica", UnitCatalog.RicaStack);
        if (vi is 3 or 4) g = I.Swap(g, "mug", UnitCatalog.MugSproutM);
        return g;
    }
    /// <summary>その版がその台で意味を持つか（M ／ R-積M はムグのいる台だけ・指示書 §3-4）。</summary>
    internal static bool Applies(Formation f, int vi) => vi switch { 0 => true, 1 or 2 => HasRica(f), _ => HasMug(f) };

    internal static readonly I.W[] InvWaves = { I.M5, I.Guard, I.Bat, I.Boss };
    static readonly I.W M3 = new("本編 第3波", () => BattleEngine.Materialize(EnemyCatalog.Stages[2].Enemy, BattleContext.EnemyTeam, EnemyScaleRule.Default), EnemyScaleRule.Default);
    /// <summary>§3-4 の物差し（強い条件は第2〜5波の4波をまとめて1列）。</summary>
    internal static readonly (string Name, I.W[] Ws)[] VerWaves =
    {
        ("ボス", new[] { I.Boss }), ("近衛", new[] { I.Guard }), ("大隊", new[] { I.Bat }), ("本編 第2波", new[] { I.M2 }),
        ("本編 第4波", new[] { I.M4 }), ("本編 第5波", new[] { I.M5 }), ("九体", new[] { I.Nine }), ("強 第2〜5波", I.StrongWaves),
    };
    /// <summary>第348期の火力（的・一 ／ 的・九 8T）——指示書 §3-4 の他の軸の値。</summary>
    static readonly (string Axis, string T1, string T9)[] OtherAxes =
        { ("燃焼", "6,241", "14,582"), ("移動", "8,444", "91,191"), ("感電", "3,781", "39,946"), ("標", "9,986", "47,477"), ("死（第348期）", "497", "5,123") };

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose の台本 ＋ 帳簿）
    // ---------------------------------------------------------------------------------
    internal sealed class B
    {
        public long N, Wins, WinT, Win1T, Cap, BossFast, Turns;
        public long FormDeaths, SporeDeaths, WightDeaths, SecondDeaths;
        public readonly long[] DeathByT = new long[7];
        public readonly Dictionary<string, (long N, long TSum)> Fell = new();
        public readonly Dictionary<string, long[]> FellHist = new();
        public readonly Dictionary<string, Dictionary<string, long>> KilledBy = new();
        public readonly long[] Layer = new long[TT], LayerAlive = new long[TT], RicaDmgT = new long[TT];
        public long PeakSum, PeakTSum, PeakN, AwakeN, AwakeTSum, RicaFellN, RicaFellTSum, RicaFellAllies, AloneN, AloneTSum, RicaIn, Peak10, RicaMaxTurn;
        public long RicaDmg, RicaLayerDmg, Dmg8, Overflow;
        public long ZotoBursts, ZotoBurstFoe, ZotoBurstAlly, ZotoIgnite, VelRevives, GolmDeathHeal, HagiDmg, SporeSummons, SporeDmg, Sprouts, SproutTSum, SproutNoSeat;
        public long Wights, WightTSum, WightTaken, WightRot, WightDmg, WightRotLost, CostSelf, ChainSaved, NoSeat, Raises;
        public long WinSurvSum, Win1Surv;
        public readonly Dictionary<string, (long Hits, long Dmg, long N)> FoeTo = new();
        public readonly Dictionary<string, long[]> HpAt = new();
        public long FoeAttacks, FoeAoE;

        public void Add(B o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Win1T += o.Win1T; Cap += o.Cap; BossFast += o.BossFast; Turns += o.Turns;
            FormDeaths += o.FormDeaths; SporeDeaths += o.SporeDeaths; WightDeaths += o.WightDeaths; SecondDeaths += o.SecondDeaths;
            for (int i = 0; i < DeathByT.Length; i++) DeathByT[i] += o.DeathByT[i];
            foreach (var (k, v) in o.Fell) { var c = Fell.GetValueOrDefault(k); Fell[k] = (c.N + v.N, c.TSum + v.TSum); }
            foreach (var (k, v) in o.FellHist) { if (!FellHist.TryGetValue(k, out var a)) FellHist[k] = a = new long[7]; for (int i = 0; i < 7; i++) a[i] += v[i]; }
            foreach (var (k, v) in o.KilledBy) { if (!KilledBy.TryGetValue(k, out var d)) KilledBy[k] = d = new(); foreach (var (n, c) in v) d[n] = d.GetValueOrDefault(n) + c; }
            for (int t = 0; t < TT; t++) { Layer[t] += o.Layer[t]; LayerAlive[t] += o.LayerAlive[t]; RicaDmgT[t] += o.RicaDmgT[t]; }
            PeakSum += o.PeakSum; PeakTSum += o.PeakTSum; PeakN += o.PeakN; AwakeN += o.AwakeN; AwakeTSum += o.AwakeTSum; RicaFellN += o.RicaFellN; RicaFellTSum += o.RicaFellTSum; RicaFellAllies += o.RicaFellAllies; AloneN += o.AloneN; AloneTSum += o.AloneTSum; RicaIn += o.RicaIn; Peak10 += o.Peak10; RicaMaxTurn += o.RicaMaxTurn;
            RicaDmg += o.RicaDmg; RicaLayerDmg += o.RicaLayerDmg; Dmg8 += o.Dmg8; Overflow += o.Overflow;
            ZotoBursts += o.ZotoBursts; ZotoBurstFoe += o.ZotoBurstFoe; ZotoBurstAlly += o.ZotoBurstAlly; ZotoIgnite += o.ZotoIgnite; VelRevives += o.VelRevives; GolmDeathHeal += o.GolmDeathHeal; HagiDmg += o.HagiDmg; SporeSummons += o.SporeSummons; SporeDmg += o.SporeDmg; Sprouts += o.Sprouts; SproutTSum += o.SproutTSum; SproutNoSeat += o.SproutNoSeat;
            Wights += o.Wights; WightTSum += o.WightTSum; WightTaken += o.WightTaken; WightRot += o.WightRot; WightDmg += o.WightDmg; WightRotLost += o.WightRotLost; CostSelf += o.CostSelf; ChainSaved += o.ChainSaved; NoSeat += o.NoSeat; Raises += o.Raises;
            WinSurvSum += o.WinSurvSum; Win1Surv += o.Win1Surv;
            foreach (var (k, v) in o.FoeTo) { var c = FoeTo.GetValueOrDefault(k); FoeTo[k] = (c.Hits + v.Hits, c.Dmg + v.Dmg, c.N + v.N); }
            foreach (var (k, v) in o.HpAt) { if (!HpAt.TryGetValue(k, out var a)) HpAt[k] = a = new long[6]; for (int i = 0; i < 6; i++) a[i] += v[i]; }
            FoeAttacks += o.FoeAttacks; FoeAoE += o.FoeAoE;
        }

        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : F1((double)WinT / Wins);
        public string Cell => $"{F1(Win)}・{WinTs}T・倒 {Per(FormDeaths, N)}";
        public string FellCell => Fell.Count == 0 ? "—" : string.Join("・", Fell.OrderBy(kv => (double)kv.Value.TSum / Math.Max(1, kv.Value.N)).Select(kv => $"{Nm(kv.Key)} {Pct(kv.Value.N, N)}%（T{F1((double)kv.Value.TSum / kv.Value.N)}）"));
        public string Layers(int upto = LoopT) => string.Join(" ", Enumerable.Range(1, upto).Select(t => LayerAlive[t] == 0 ? "—" : F1((double)Layer[t] / LayerAlive[t])));
        public string RicaPerTurn(int upto) => string.Join(" ", Enumerable.Range(1, upto).Select(t => LayerAlive[t] == 0 ? "—" : F0((double)RicaDmgT[t] / LayerAlive[t])));
    }

    static int Bucket(int t) => Math.Clamp(t, 1, 6);
    static bool IsBurn(string? s) => s is StatusKeys.Burn or "燃" or "燃焼";

    internal static B One(Formation f, I.W w, int seed)
    {
        var b = new B { N = 1 };
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        b.Turns = r.Turns;
        if (w.Target) b.Dmg8 = I.Take(r, p, w).Dmg8;
        var form = p.ToDictionary(u => u.InstanceId);
        var foe = e.ToDictionary(u => u.InstanceId);
        var summoned = new Dictionary<int, (string Name, int Team)>();
        UnitState? rica = p.FirstOrDefault(u => u.Def.Id == "rica");
        int Id(UnitState? u) => u?.InstanceId ?? -1;
        int ricaId = Id(rica), zotoId = Id(p.FirstOrDefault(u => u.Def.Id == "zoto")), golmId = Id(p.FirstOrDefault(u => u.Def.Id == "golm")),
            velId = Id(p.FirstOrDefault(u => u.Def.Id == "vel")), hagiId = Id(p.FirstOrDefault(u => u.Def.Id == "hagi"));
        if (r.PlayerWon && !w.Target)
        {
            b.Wins = 1; b.WinT = r.Turns; if (r.Turns <= 1) b.Win1T = 1; if (w.Boss && r.Turns < 4) b.BossFast = 1;
            int surv = p.Count(u => u.IsAlive); b.WinSurvSum = surv; if (surv == 1) b.Win1Surv = 1;
        }
        else if (!w.Target && r.PlayerSurvivors > 0) b.Cap = 1;

        var first = new Dictionary<int, int>();
        var alive = new HashSet<int>(form.Keys);
        var dead = new HashSet<int>();
        var hp = form.ToDictionary(kv => kv.Key, kv => (long)kv.Value.Def.MaxHp);
        var hits = form.Keys.ToDictionary(k => k, _ => (H: 0L, D: 0L));
        foreach (var u in p) b.HpAt[u.Def.Id] = new long[6];
        long[] ricaDmgT = new long[TT], ricaAtkT = new long[TT];
        string Who(int? id) => id is not int i ? "（なし）" : foe.TryGetValue(i, out var fu) ? fu.Def.Name : form.TryGetValue(i, out var mu) ? "味方の" + S307.Short(mu.Def) : summoned.TryGetValue(i, out var s) ? s.Name : "?";

        foreach (var x in r.Events)
        {
            int t = x.Turn;
            int tgt = x.TargetId ?? -3, act = x.ActorId ?? -2;   // 居ない駒の id（-1）と撃ち手なし（-2）を取り違えない
            switch (x.Kind)
            {
                case BattleEventKind.Summon:
                    summoned[tgt] = (x.Text ?? "?", x.Team ?? -1);
                    if (x.Team == BattleContext.PlayerTeam && x.Text == UnitCatalog.Spore.Name) b.SporeSummons++;
                    if (x.Team == BattleContext.PlayerTeam && x.Text == UnitCatalog.Wight.Name) { b.Wights++; b.WightTSum += t; }
                    break;
                case BattleEventKind.TurnStart:
                    if (t is >= 1 and <= 5) foreach (var (k, u) in form) b.HpAt[u.Def.Id][t] += alive.Contains(k) ? Math.Max(0, hp[k]) : 0;
                    break;
                case BattleEventKind.StatSnapshot:
                    if (tgt == ricaId && t is >= 0 and < TT) ricaAtkT[t] = x.Amount;
                    if (x.Amount > 1_000_000_000 || x.Amount < 0) b.Overflow++;
                    break;
                case BattleEventKind.Attack:
                    if (foe.ContainsKey(act)) { b.FoeAttacks++; if (x.Pattern is AttackPattern.All) b.FoeAoE++; }
                    break;
                case BattleEventKind.Damage:
                {
                    if (x.Amount > 1_000_000_000 || x.Amount < 0) b.Overflow++;
                    bool toFoe = foe.ContainsKey(tgt);
                    if (form.ContainsKey(tgt)) hp[tgt] = x.HpAfter;
                    if (foe.ContainsKey(act) && form.ContainsKey(tgt) && !first.ContainsKey(tgt)) { var c = hits[tgt]; hits[tgt] = (c.H + 1, c.D + x.Amount); }
                    if (act == ricaId && toFoe) { b.RicaDmg += x.Amount; if (t is >= 0 and < TT) ricaDmgT[t] += x.Amount; }
                    if (act == zotoId && dead.Contains(act)) { if (toFoe) b.ZotoBurstFoe += x.Amount; else b.ZotoBurstAlly += x.Amount; }
                    if (act == hagiId && toFoe) b.HagiDmg += x.Amount;
                    if (toFoe && summoned.TryGetValue(act, out var sa) && sa.Team == BattleContext.PlayerTeam)
                    {
                        if (sa.Name == UnitCatalog.Spore.Name) b.SporeDmg += x.Amount;
                        if (sa.Name == UnitCatalog.Wight.Name) b.WightDmg += x.Amount;
                    }
                    if (foe.ContainsKey(act) && summoned.TryGetValue(tgt, out var st) && st.Name == UnitCatalog.Wight.Name) b.WightTaken += x.Amount;
                    break;
                }
                case BattleEventKind.Heal:
                    if (form.ContainsKey(tgt)) hp[tgt] = x.HpAfter;
                    if (act == golmId && dead.Contains(act)) b.GolmDeathHeal += x.Amount;
                    break;
                case BattleEventKind.Revive:
                    if (form.ContainsKey(tgt)) { hp[tgt] = x.HpAfter; alive.Add(tgt); }
                    dead.Remove(tgt);
                    if (act == velId) b.VelRevives++;
                    break;
                case BattleEventKind.StatusGain:
                    if (act == zotoId && dead.Contains(act) && IsBurn(x.Text)) b.ZotoIgnite++;
                    break;
                case BattleEventKind.Death:
                {
                    dead.Add(tgt);
                    if (tgt == zotoId) b.ZotoBursts++;
                    bool ally = form.ContainsKey(tgt) || (summoned.TryGetValue(tgt, out var sd) && sd.Team == BattleContext.PlayerTeam);
                    if (ally) b.DeathByT[Bucket(t)]++;
                    if (form.TryGetValue(tgt, out var fu))
                    {
                        b.FormDeaths++;
                        alive.Remove(tgt); hp[tgt] = 0;
                        if (first.ContainsKey(tgt)) { b.SecondDeaths++; break; }
                        first[tgt] = t;
                        string id = fu.Def.Id;
                        var c = b.Fell.GetValueOrDefault(id); b.Fell[id] = (c.N + 1, c.TSum + t);
                        if (!b.FellHist.TryGetValue(id, out var hh)) b.FellHist[id] = hh = new long[7];
                        hh[Bucket(t)]++;
                        if (!b.KilledBy.TryGetValue(id, out var kb)) b.KilledBy[id] = kb = new();
                        string kn = Who(x.ActorId); kb[kn] = kb.GetValueOrDefault(kn) + 1;
                        var hc = hits[tgt]; var fc = b.FoeTo.GetValueOrDefault(id); b.FoeTo[id] = (fc.Hits + hc.H, fc.Dmg + hc.D, fc.N + 1);
                        if (tgt == ricaId) { b.RicaFellN = 1; b.RicaFellTSum = t; b.RicaFellAllies = alive.Count; }
                    }
                    else if (summoned.TryGetValue(tgt, out var s2) && s2.Team == BattleContext.PlayerTeam)
                    {
                        if (s2.Name == UnitCatalog.Spore.Name) b.SporeDeaths++;
                        if (s2.Name == UnitCatalog.Wight.Name) b.WightDeaths++;
                    }
                    break;
                }
            }
        }

        if (rica is not null && r.TallyByUnit.TryGetValue("rica", out var rt))
        {
            b.RicaIn = 1;
            for (int t = 1; t < TT; t++)
            {
                long alv = rt.R377AliveByTurn is { } al && t < al.Length ? al[t] : 0;
                b.Layer[t] += rt.R377LayerByTurn is { } la && t < la.Length ? la[t] : 0;
                b.LayerAlive[t] += alv;
                if (alv > 0) b.RicaDmgT[t] += ricaDmgT[t];
                long bonus = rt.R377BonusByTurn is { } bo && t < bo.Length ? bo[t] : 0;
                if (ricaAtkT[t] > 0 && bonus > 0) b.RicaLayerDmg += ricaDmgT[t] * Math.Min(bonus, ricaAtkT[t]) / ricaAtkT[t];
            }
            b.RicaMaxTurn = ricaDmgT.Max();
            b.PeakSum = rt.NecroPeak; if (rt.NecroPeak > 0) { b.PeakN = 1; b.PeakTSum = rt.R377PeakTurn; }
            if (rt.NecroPeak > 10) b.Peak10 = 1;
            if (rt.R377AwakeTurn > 0) { b.AwakeN = 1; b.AwakeTSum = rt.R377AwakeTurn; }
            b.CostSelf = rt.R377CostSelf; b.ChainSaved = rt.R377ChainSaved; b.NoSeat = rt.R377NoSeat; b.Raises = rt.R377Raises;
        }
        if (r.TallyByUnit.TryGetValue("mug", out var mt)) { b.Sprouts = mt.R377Sprouts; b.SproutTSum = mt.R377SproutTurnSum; b.SproutNoSeat = mt.R377SproutNoSeat; }
        if (r.TallyByUnit.TryGetValue(UnitCatalog.Wight.Id, out var wt)) { b.WightRot = wt.R377RotDeaths; b.WightRotLost = wt.R377RotLost; }
        int alone = r.TallyByUnit.Values.Where(t => t.AloneTurn > 0).Select(t => t.AloneTurn).DefaultIfEmpty(0).Min();
        if (alone > 0) { b.AloneN = 1; b.AloneTSum = alone; }
        return b;
    }

    internal static B Measure(Formation f, I.W[] ws, int seeds)
    {
        var parts = new B[ws.Length * seeds];
        Parallel.For(0, parts.Length, k => parts[k] = One(f, ws[k / seeds], k % seeds));
        var a = new B();
        foreach (var x in parts) a.Add(x);
        return a;
    }

    // ---------------------------------------------------------------------------------
    // §2-2 死の流れ
    // ---------------------------------------------------------------------------------
    static void Inventory()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int seeds = 20;
        var boards = Boards(tsugi: true);
        var g = new B[boards.Length, InvWaves.Length];
        Parallel.For(0, g.Length, k => g[k / InvWaves.Length, k % InvWaves.Length] = Measure(boards[k / InvWaves.Length].F, new[] { InvWaves[k % InvWaves.Length] }, seeds));
        Console.WriteLine($"# {Tag} §2-2 死の流れ —— 死の台 {boards.Length} × {InvWaves.Length} 波（seed 0..{seeds - 1}・規定の駒）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。継ぎ当て×死の連鎖はツギ入り（参考）。");
        Console.WriteLine();
        void Table(string title, Func<B, string> cell)
        {
            Console.WriteLine(title);
            Console.WriteLine();
            Console.WriteLine("| 台 | " + string.Join(" | ", InvWaves.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(InvWaves.Select(_ => "---|")));
            for (int bi = 0; bi < boards.Length; bi++) Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(0, InvWaves.Length).Select(wi => cell(g[bi, wi])))} |");
            Console.WriteLine();
        }
        Table("## 1. 勝率・倒しT（勝った戦）・倒れた編成の駒（1戦・2度目を含む）", x => x.Cell);
        Table("## 1'. 誰が何Tに倒れたか（駒 最初に倒れた戦の割合%（倒れたTの平均）・早い順）", x => x.FellCell);
        Console.WriteLine("## 1''. 倒れたTの分布と倒した敵（駒: T1 ／ T2 ／ T3 ／ T4 ／ T5 ／ 6+ の戦の数・倒した敵の上位2）");
        Console.WriteLine();
        for (int bi = 0; bi < boards.Length; bi++)
            for (int wi = 0; wi < InvWaves.Length; wi++)
            {
                var x = g[bi, wi];
                Console.WriteLine($"- {boards[bi].Name} × {InvWaves[wi].Name}: " + string.Join("・", x.FellHist.OrderBy(kv => (double)x.Fell[kv.Key].TSum / x.Fell[kv.Key].N).Select(kv =>
                    $"{Nm(kv.Key)} [{string.Join(" ", kv.Value.Skip(1))}]（{string.Join("・", x.KilledBy[kv.Key].OrderByDescending(q => q.Value).Take(2).Select(q => $"{q.Key} {q.Value}"))}）")));
            }
        Console.WriteLine();
        Table("## 2. 味方の死の数（1戦: 編成 ／ 胞子 ／ うち2度目（ヴェルが戻した駒））", x => $"{Per(x.FormDeaths, x.N)} ／ {Per(x.SporeDeaths, x.N)} ／ {Per(x.SecondDeaths, x.N)}");
        Table("## 2'. 味方の死のターン分布（1戦: T1 ／ T2 ／ T3 ／ T4 ／ T5 ／ 6+）", x => string.Join(" ／ ", x.DeathByT.Skip(1).Select(v => Per(v, x.N))));
        Table("## 3. リィカの層（ターンの頭・減衰の後・T1〜T10・生きていた戦の平均）", x => x.Layers());
        Table("## 3'. リィカ: 最大層（平均）・届いたT ／ 3 層（薙ぎ）に届いた戦%・届いたT ／ リィカが倒れた戦%・倒れたT・そのとき残っていた編成の駒 ／ 1人になった戦%・T",
            x => $"{Per(x.PeakSum, x.N)}・T{Per(x.PeakTSum, x.PeakN)} ／ {Pct(x.AwakeN, x.N)}%・T{Per(x.AwakeTSum, x.AwakeN)} ／ {Pct(x.RicaFellN, x.N)}%・T{Per(x.RicaFellTSum, x.RicaFellN)}・{Per(x.RicaFellAllies, x.RicaFellN)} 体 ／ {Pct(x.AloneN, x.N)}%・T{Per(x.AloneTSum, x.AloneN)}");
        Table("## 4. 死を読んで出た量（1戦: リィカの与ダメ（うち層の上乗せの推定）／ ゾトの破裂 敵へ ／ 味方へ ／ 点いた火 ／ ヴェルの戻し ／ ゴルムの倒れ時の回復 ／ ハギの追い打ち ／ 胞子 湧いた数・与ダメ）",
            x => $"{Per(x.RicaDmg, x.N)}（{Per(x.RicaLayerDmg, x.N)}）／ {Per(x.ZotoBurstFoe, x.N)} ／ {Per(x.ZotoBurstAlly, x.N)} ／ {Per(x.ZotoIgnite, x.N)} ／ {Per(x.VelRevives, x.N)} ／ {Per(x.GolmDeathHeal, x.N)} ／ {Per(x.HagiDmg, x.N)} ／ {Per(x.SporeSummons, x.N)}・{Per(x.SporeDmg, x.N)}");
        Console.WriteLine("層の上乗せの推定 ＝ ターンごとのリィカの与ダメ × （ターンの頭の層の上乗せ ÷ ターンの頭の攻撃力）。ターンの途中で層が増えた分は次のターンに回るので、少なめに出る。");
        Console.WriteLine();

        int bw = Array.IndexOf(InvWaves, I.Boss);
        Console.WriteLine("## 5. ボスで全滅する理由（勇者の手番・駒ごとに、最初に倒れるまでに敵から受けた一撃の数 ／ 1発の量・ターンの頭の HP T1〜T5）");
        Console.WriteLine();
        for (int bi = 0; bi < boards.Length; bi++)
        {
            var x = g[bi, bw];
            Console.WriteLine($"- **{boards[bi].Name}**（勇者の攻撃 1戦 {Per(x.FoeAttacks, x.N)} 回・うち全体 {Per(x.FoeAoE, x.N)}）: " + string.Join("・", x.FoeTo.OrderBy(kv => (double)x.Fell[kv.Key].TSum / x.Fell[kv.Key].N).Select(kv =>
                $"{Nm(kv.Key)} {Per(kv.Value.Hits, kv.Value.N)} 発 × {Per(kv.Value.Dmg, kv.Value.Hits)}（HP {string.Join(" → ", x.HpAt[kv.Key].Skip(1).Select(v => Per(v, x.N)))}）")));
        }
        Console.WriteLine();
        Table("## 6. 勝ち方の質（勝った戦の平均生存数（編成の駒）・生存1体で勝った割合%）", x => x.Wins == 0 ? "—" : $"{Per(x.WinSurvSum, x.Wins)}・{Pct(x.Win1Surv, x.Wins)}%");

        // ---- 7. リィカの火力の頭打ち ----
        var fw = new[] { I.Boss, I.T1, I.T9 };
        var gf = new B[boards.Length, fw.Length];
        Parallel.For(0, gf.Length, k => gf[k / fw.Length, k % fw.Length] = Measure(boards[k / fw.Length].F, new[] { fw[k % fw.Length] }, seeds));
        Console.WriteLine("## 7. リィカの火力の頭打ち（ボス ／ 的・一 ／ 的・九・seed 0..19）");
        Console.WriteLine();
        Console.WriteLine("各行: リィカの与ダメ（ターンごと・T1〜T8・生きていた戦の平均）／ ターンの頭の層（T1〜T8）／ 生きていた戦%（T1〜T8）／ 1ターンの最大の与ダメ（戦ごとの最大の平均）。的は 8T を読む（第348期の物差し・攻撃しない的）。");
        Console.WriteLine();
        for (int bi = 0; bi < boards.Length; bi++)
            for (int wi = 0; wi < fw.Length; wi++)
            {
                var x = gf[bi, wi];
                Console.WriteLine($"- {boards[bi].Name} × {fw[wi].Name}: 与ダメ [{x.RicaPerTurn(8)}] ／ 層 [{x.Layers(8)}] ／ 生存 [{string.Join(" ", Enumerable.Range(1, 8).Select(t => Pct(x.LayerAlive[t], x.N)))}] ／ 最大 {Per(x.RicaMaxTurn, x.N)}{(fw[wi].Target ? $" ／ 台の 8T の与ダメ {F0((double)x.Dmg8 / x.N)}" : "")}");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §2-3 駒ごとの効き（素体に替えた差）
    // ---------------------------------------------------------------------------------
    internal static double WinRate(Formation f, I.W w, int seeds)
    {
        int wins = 0;
        for (int s = 0; s < seeds; s++) if (I.Raw(f, w, s, census: false).R.PlayerWon) wins++;
        return 100.0 * wins / seeds;
    }

    static string Ideal(UnitDef d)
    {
        try
        {
            foreach (var l in File.ReadAllLines("docs/roster_audit.md"))
            {
                var c = l.Split('|');
                if (c.Length > 4 && c[2].Trim() == d.Name) return c[3].Trim();
            }
        }
        catch (IOException) { }
        return "—";
    }

    static void Attrib()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int seeds = 100;
        var boards = Boards(tsugi: false);
        Console.WriteLine($"# {Tag} §2-3 駒ごとの効き —— ツギなしの死の台 {boards.Length} × 各駒を素体（同じ席・同じ体・札なし・ふつうに殴る）に替えた差 × {InvWaves.Length} 波（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("各セル: 素体に替えた勝率 − 規定の勝率（pt）。規定の行は勝率。右端は `docs/roster_audit.md` の理想台の帰属（`checkup ideal`・`compare` の在席行の平均・第2〜5波）。**素体の替えは器具の中だけ**。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 駒 | " + string.Join(" | ", InvWaves.Select(w => w.Name)) + " | 理想台の帰属 |");
        Console.WriteLine("|---|---|" + string.Concat(InvWaves.Select(_ => "--:|")) + "--:|");
        foreach (var (name, f) in boards)
        {
            var units = f.Occupied().Select(o => o.Def).ToArray();
            var variants = new[] { f }.Concat(units.Select(d => I.Swap(f, d.Id, I.Plain(d)))).ToArray();
            var g = new double[variants.Length, InvWaves.Length];
            Parallel.For(0, g.Length, k => g[k / InvWaves.Length, k % InvWaves.Length] = WinRate(variants[k / InvWaves.Length], InvWaves[k % InvWaves.Length], seeds));
            Console.WriteLine($"| {name} | （規定） | {string.Join(" | ", Enumerable.Range(0, InvWaves.Length).Select(wi => F1(g[0, wi])))} | |");
            for (int ui = 0; ui < units.Length; ui++)
                Console.WriteLine($"| | {S307.Short(units[ui])} → 素体 | {string.Join(" | ", Enumerable.Range(0, InvWaves.Length).Select(wi => Sg(g[ui + 1, wi] - g[0, wi])))} | {Ideal(units[ui])} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-4 版（亡者と増殖）
    // ---------------------------------------------------------------------------------
    static void Versions()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int seeds = 100;
        var boards = Boards(tsugi: true);
        int nb = boards.Length, nv = VerNames.Length, nw = VerWaves.Length;
        var fw = new[] { I.T1, I.T9 };
        var g = new B?[nb, nv, nw];
        var gf = new B?[nb, nv, fw.Length];
        var keys = new List<(int B, int V, int W)>();
        for (int bi = 0; bi < nb; bi++) for (int vi = 0; vi < nv; vi++) if (Applies(boards[bi].F, vi)) for (int wi = 0; wi < nw + fw.Length; wi++) keys.Add((bi, vi, wi));
        Parallel.ForEach(keys, k =>
        {
            var f = Apply(boards[k.B].F, k.V);
            if (k.W < nw) g[k.B, k.V, k.W] = Measure(f, VerWaves[k.W].Ws, seeds);
            else gf[k.B, k.V, k.W - nw] = Measure(f, new[] { fw[k.W - nw] }, seeds);
        });
        // 毒の火力（第348期の物差しで、いまの規定の「試遊・毒」を測る——指示書 §3-4「毒は第375期の値があれば」の値が無かったので）
        var poison = Presets.Versions.First(r => r.Name == "試遊・毒").F;
        var pf = fw.Select(w => Measure(poison, new[] { w }, seeds)).ToArray();

        Console.WriteLine($"# {Tag} §3-4 亡者と増殖 —— 死の台 {nb} × 版（{string.Join(" ／ ", VerNames)}）× {nw} 物差し ＋ 火力（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("R-繋 ／ R-積 はリィカ（亡者起こし・代償 8 を HP から直接・1戦に3回）、M はムグ（亡骸の芽吹き・1戦に3回）、R-積M は両方。亡者 ＝ HP 24 ／ 攻 3 ／ 速 4・儚い ＋ 朽ち（毎ターン HP 10）。強 第2〜5波は4波をまとめた1列（HP 500% ／ 攻 300%）。ムグのいない台は M ／ R-積M を空欄。継ぎ当て×死の連鎖はツギ入り（参考）。");
        Console.WriteLine();
        void Grid(string title, Func<B, string> cell, int[]? only = null)
        {
            var ws = only ?? Enumerable.Range(0, nw).ToArray();
            Console.WriteLine(title);
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | " + string.Join(" | ", ws.Select(wi => VerWaves[wi].Name)) + " |");
            Console.WriteLine("|---|---|" + string.Concat(ws.Select(_ => "---|")));
            for (int bi = 0; bi < nb; bi++)
                for (int vi = 0; vi < nv; vi++)
                {
                    if (!Applies(boards[bi].F, vi)) continue;
                    Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {VerNames[vi]} | {string.Join(" | ", ws.Select(wi => cell(g[bi, vi, wi]!)))} |");
                }
            Console.WriteLine();
        }
        int[] key4 = { 0, 1, 2, 5 };
        Grid("## 1. 勝率・倒しT（勝った戦）・倒れた編成の駒（1戦・2度目を含む）", x => x.Cell);
        Grid("## 1'. 誰が何Tに倒れたか（駒 最初に倒れた戦%（T の平均）・早い順）", x => x.FellCell, key4);
        Grid("## 2. 湧いた体（1戦: 亡者 起きた数・T ／ 亡者1体が敵から受けた傷 ／ 朽ちた %（倒れた亡者のうち）／ リィカの代償 ‖ 芽吹いた胞子・T ／ 胞子の湧いた数（芽吹き ＋ ムグの分裂）・倒れた数 ／ 空きが無くて湧かなかった回数（亡者 ／ 芽吹き））",
            x => $"{(x.Wights == 0 ? "—" : $"{Per(x.Wights, x.N)}・T{Per(x.WightTSum, x.Wights)} ／ {Per(x.WightTaken, x.Wights)} ／ {Pct(x.WightRot, x.WightDeaths)}% ／ {Per(x.CostSelf, x.N)}")} ‖ {(x.Sprouts == 0 ? "—" : $"{Per(x.Sprouts, x.N)}・T{Per(x.SproutTSum, x.Sprouts)}")} ／ {Per(x.SporeSummons, x.N)}・{Per(x.SporeDeaths, x.N)} ／ {Per(x.NoSeat, x.N)} ／ {Per(x.SproutNoSeat, x.N)}", key4);
        Grid("## 3. リィカ（最大層・3 層に届いた戦%・届いたT ／ リィカが倒れた戦%・T ／ 層が薄れずに済んだターン（亡者の死だけで繋いだ・1戦）／ 1ターンの最大の与ダメ）",
            x => $"{Per(x.PeakSum, x.N)}・{Pct(x.AwakeN, x.N)}%・T{Per(x.AwakeTSum, x.AwakeN)} ／ {Pct(x.RicaFellN, x.N)}%・T{Per(x.RicaFellTSum, x.RicaFellN)} ／ {Per(x.ChainSaved, x.N)} ／ {Per(x.RicaMaxTurn, x.N)}", key4);
        Grid("## 4. 勝ち方の質（勝った戦の平均生存数（編成の駒）・生存1体で勝った割合%）", x => x.Wins == 0 ? "—" : $"{Per(x.WinSurvSum, x.Wins)}・{Pct(x.Win1Surv, x.Wins)}%");

        Console.WriteLine("## 5. 火力（的・一 ／ 的・九 の 8T・台の与ダメの合計・1戦の平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", VerNames) + " |");
        Console.WriteLine("|---|" + string.Concat(VerNames.Select(_ => "--:|")));
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(0, nv).Select(vi => gf[bi, vi, 0] is { } a && gf[bi, vi, 1] is { } c ? $"{F0((double)a.Dmg8 / a.N)} ／ {F0((double)c.Dmg8 / c.N)}" : "—"))} |");
        Console.WriteLine();
        Console.WriteLine("他の軸（第348期・`inv348 axes`・的・一 ／ 的・九）: " + string.Join("・", OtherAxes.Select(a => $"{a.Axis} {a.T1} ／ {a.T9}")) + $"・**毒（いまの「試遊・毒」をこの器具で測った）{F0((double)pf[0].Dmg8 / pf[0].N)} ／ {F0((double)pf[1].Dmg8 / pf[1].N)}**。");
        Console.WriteLine();
        Console.WriteLine("リィカの層（ターンの頭・T1〜T8）と1ターンの最大の与ダメ（的・九）: " + string.Join("・", Enumerable.Range(0, nb).SelectMany(bi => Enumerable.Range(0, nv).Where(vi => gf[bi, vi, 1] is not null).Select(vi =>
            $"{boards[bi].Name} {VerNames[vi]} [{gf[bi, vi, 1]!.Layers(8)}]・最大 {Per(gf[bi, vi, 1]!.RicaMaxTurn, gf[bi, vi, 1]!.N)}"))) + "。");
        Console.WriteLine();

        Console.WriteLine("## 6. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切りが規定から 5pt 以上増える ／ 層が 10 を超える戦 ／ int のあふれの兆し（与ダメ ／ 攻撃力が 10 億超か負））");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 1; vi < nv; vi++)
            {
                if (!Applies(boards[bi].F, vi)) continue;
                string at = $"{boards[bi].Name} × {VerNames[vi]}";
                foreach (int wi in new[] { 1, 2 }) { var x = g[bi, vi, wi]!; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{at} × {VerWaves[wi].Name}: 1T 決着 {Pct(x.Win1T, x.N)}%"); }
                if (g[bi, vi, 0]!.BossFast > 0) signs.Add($"{at} × ボス: 4T 未満の勝ち {g[bi, vi, 0]!.BossFast} 戦");
                for (int wi = 0; wi < nw; wi++)
                {
                    var x = g[bi, vi, wi]!; var x0 = g[bi, 0, wi]!;
                    double d = 100.0 * x.Cap / x.N - 100.0 * x0.Cap / x0.N;
                    if (d >= 5) signs.Add($"{at} × {VerWaves[wi].Name}: 30T の打ち切り {Pct(x0.Cap, x0.N)} → {Pct(x.Cap, x.N)}%");
                    if (x.Overflow > 0) signs.Add($"{at} × {VerWaves[wi].Name}: あふれの兆し {x.Overflow} 件");
                }
                for (int fi = 0; fi < fw.Length; fi++) if (gf[bi, vi, fi]!.Overflow > 0) signs.Add($"{at} × {fw[fi].Name}: あふれの兆し {gf[bi, vi, fi]!.Overflow} 件");
            }
        Console.WriteLine(signs.Count == 0 ? "- 精鋭の 1T ／ ボス 4T 未満 ／ 30T ／ あふれ: なし" : string.Join("\n", signs.Select(s => "- " + s)));
        Console.WriteLine();
        Console.WriteLine("層が 10 を超えた戦%（ボス ／ 近衛 ／ 大隊 ／ 第5波 ／ 九体 ／ 強 ／ 的・一 ／ 的・九）: " + string.Join("・", Enumerable.Range(0, nb).SelectMany(bi => Enumerable.Range(0, nv).Where(vi => Applies(boards[bi].F, vi)).Select(vi =>
            $"{boards[bi].Name} {VerNames[vi]} [{string.Join(" ", new[] { 0, 1, 2, 5, 6, 7 }.Select(wi => Pct(g[bi, vi, wi]!.Peak10, g[bi, vi, wi]!.N)).Concat(Enumerable.Range(0, 2).Select(fi => Pct(gf[bi, vi, fi]!.Peak10, gf[bi, vi, fi]!.N))))}]"))) + "。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-4 寄せ集めの台
    // ---------------------------------------------------------------------------------
    /// <summary>死の駒（§2-1 の書き手 ／ 読み手）とツギ ／ リリ。寄せ集めの台からは外す。</summary>
    internal static readonly string[] DeathIds = { "zoto", "mug", "rica", "vel", "gald", "lili", "rau", "yomi", "hagi", "tsugi", "tomo", "uro", "golm", "kugu", "susu", "nomi" };
    internal const int MixBoards = 50, MixSeed = 377;

    /// <summary>寄せ集めの台 50（リィカ ＋ 死の札を持たない4枚・席もランダム）。台を作る乱数は器具の中で固定（`new Random(377)`・盤面の乱数とは別）。</summary>
    internal static Formation[] MixBoardsOf(UnitDef rica)
    {
        var pool = UnitCatalog.All.Where(d => !DeathIds.Contains(d.Id)).ToArray();
        var rng = new Random(MixSeed);
        var res = new Formation[MixBoards];
        for (int i = 0; i < MixBoards; i++)
        {
            var pick = pool.OrderBy(_ => rng.Next()).Take(4).ToArray();
            var seats = Enumerable.Range(0, 5).OrderBy(_ => rng.Next()).ToArray();
            var f = new Formation();
            f[seats[0]] = rica;
            for (int k = 0; k < 4; k++) f[seats[k + 1]] = pick[k];
            res[i] = f;
        }
        return res;
    }

    static void Mix()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int seeds = 20;
        var vers = new (string Name, UnitDef D)[] { ("素体", I.Plain(UnitCatalog.Rica)), ("規定", UnitCatalog.Rica), ("R-繋", UnitCatalog.RicaLink), ("R-積", UnitCatalog.RicaStack) };
        var boards = vers.Select(v => MixBoardsOf(v.D)).ToArray();
        int nw = VerWaves.Length;
        var g = new double[vers.Length, MixBoards, nw];
        Parallel.For(0, vers.Length * MixBoards * nw, k =>
        {
            int vi = k / (MixBoards * nw), bi = k / nw % MixBoards, wi = k % nw;
            g[vi, bi, wi] = VerWaves[wi].Ws.Average(w => WinRate(boards[vi][bi], w, seeds));
        });
        Console.WriteLine($"# {Tag} §3-4 寄せ集めの台 —— リィカ ＋ 死の札を持たない4枚 × {MixBoards} 台（席もランダム・台の乱数 `new Random({MixSeed})`）× {nw} 物差し（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"外した駒（死の書き手 ／ 読み手・ツギ ／ リリ）: {string.Join("・", DeathIds.Select(Nm))}。残り {UnitCatalog.All.Count(d => !DeathIds.Contains(d.Id))} 枚から4枚。素体 ＝ リィカと同じ体・札なし（器具の中だけ）。");
        Console.WriteLine();
        Console.WriteLine("## 1. 50 台の平均勝率（%）と素体との差");
        Console.WriteLine();
        Console.WriteLine("| リィカ | " + string.Join(" | ", VerWaves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(VerWaves.Select(_ => "--:|")));
        for (int vi = 0; vi < vers.Length; vi++)
            Console.WriteLine($"| {vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, nw).Select(wi => { double m = Enumerable.Range(0, MixBoards).Average(bi => g[vi, bi, wi]); double m0 = Enumerable.Range(0, MixBoards).Average(bi => g[0, bi, wi]); return vi == 0 ? F1(m) : $"{F1(m)}（{Sg(m - m0)}）"; }))} |");
        Console.WriteLine();
        Console.WriteLine("## 2. 素体より 10pt 以上 強い ／ 弱い台の数（50 台のうち）");
        Console.WriteLine();
        Console.WriteLine("| リィカ | " + string.Join(" | ", VerWaves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(VerWaves.Select(_ => "---|")));
        for (int vi = 1; vi < vers.Length; vi++)
            Console.WriteLine($"| {vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, nw).Select(wi => $"{Enumerable.Range(0, MixBoards).Count(bi => g[vi, bi, wi] - g[0, bi, wi] >= 10)} ／ {Enumerable.Range(0, MixBoards).Count(bi => g[vi, bi, wi] - g[0, bi, wi] <= -10)}"))} |");
        Console.WriteLine();
        const int m5 = 5;
        var top = Enumerable.Range(0, MixBoards).OrderByDescending(bi => g[3, bi, m5] - g[0, bi, m5]).Take(5).ToArray();
        Console.WriteLine("R-積 で第5波が素体から最も上がった5台: " + string.Join("・", top.Select(bi => $"{S307.Seats(boards[1][bi])}（素体 {F1(g[0, bi, m5])} → 規定 {F1(g[1, bi, m5])} ／ R-積 {F1(g[3, bi, m5])}）")) + "。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` の1行 × 版（動いた理由を見る・本編 第2〜5波・seed 0..199）
    // ---------------------------------------------------------------------------------
    static void RowDetail(string name)
    {
        var f = CompareBuilds().First(r => r.Name == name).F;
        var ws = new[] { I.M2, M3, I.M4, I.M5 };
        var vs = Enumerable.Range(0, VerNames.Length).Where(vi => Applies(f, vi)).ToArray();
        var g = new B[vs.Length, ws.Length];
        Parallel.For(0, g.Length, k => g[k / ws.Length, k % ws.Length] = Measure(Apply(f, vs[k / ws.Length]), new[] { ws[k % ws.Length] }, CompareSeeds));
        Console.WriteLine($"# {Tag} `compare` の1行 × 版 —— {name}（{S307.Seats(f)}・本編 第2〜5波・seed 0..{CompareSeeds - 1}・verbose）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 勝率・倒しT・倒 | 誰が何Tに倒れたか | 亡者（数・T ／ 敵から受けた傷 ／ 与ダメ）| 胞子（湧いた ／ 倒れた・うち芽吹き）| リィカ 最大層・3層% ／ 倒れた%・T |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        for (int i = 0; i < vs.Length; i++)
            for (int wi = 0; wi < ws.Length; wi++)
            {
                var x = g[i, wi];
                Console.WriteLine($"| {VerNames[vs[i]]} | {ws[wi].Name} | {x.Cell} | {x.FellCell} | {(x.Wights == 0 ? "—" : $"{Per(x.Wights, x.N)}・T{Per(x.WightTSum, x.Wights)} ／ {Per(x.WightTaken, x.N)} ／ {Per(x.WightDmg, x.N)}")} | {Per(x.SporeSummons, x.N)} ／ {Per(x.SporeDeaths, x.N)}・{Per(x.Sprouts, x.N)} | {Per(x.PeakSum, x.N)}・{Pct(x.AwakeN, x.N)}% ／ {Pct(x.RicaFellN, x.N)}%・T{Per(x.RicaFellTSum, x.RicaFellN)} |");
            }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // `compare` × 版
    // ---------------------------------------------------------------------------------
    static double[,] CGrid((string Name, Formation F)[] rows, Func<Formation, Formation> map, Func<int, bool> only)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            if (!only(ri)) { g[ri, wi] = double.NaN; return; }
            var f = map(rows[ri].F);
            int wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        return g;
    }

    static void CompareVer(string ver)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int vi = Array.IndexOf(VerNames, ver);
        if (vi < 1) throw new ArgumentException("sh377 compare の版は R-繋 / R-積 / M / R-積M");
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        Func<int, bool> only = ri => vi switch { 1 or 2 => HasRica(rows[ri].F), 3 => HasMug(rows[ri].F), _ => HasRica(rows[ri].F) || HasMug(rows[ri].F) };
        var b = CGrid(rows, f => f, _ => true);
        var g0 = CGrid(rows, f => Apply(f, vi), only);
        var g = new double[rows.Length, nw];
        int touched = 0;
        for (int r = 0; r < rows.Length; r++) { if (only(r)) touched++; for (int w = 0; w < nw; w++) g[r, w] = only(r) ? g0[r, w] : b[r, w]; }
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] x) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => x[r, w]))));
        Console.WriteLine($"# {Tag} `compare` {rows.Length} 行 × 版 {VerNames[vi]}（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"版で回した行 {touched}（{(vi switch { 1 or 2 => "リィカ", 3 => "ムグ", _ => "リィカかムグ" })}のいる行・{string.Join("、", Enumerable.Range(0, rows.Length).Where(only).Select(ri => rows[ri].Name))}）。それ以外の行は版の写像で1枚も変わらない（回さない）。");
        Console.WriteLine();
        int upR = 0, upC = 0, dnR = 0, dnC = 0;
        var lines = new List<string>();
        for (int r = 0; r < rows.Length; r++)
        {
            bool u = false, d = false;
            for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
            if (u) upR++; if (d) dnR++;
            if (u || d) lines.Add($"{rows[r].Name}: " + string.Join("・", Enumerable.Range(1, nw - 1).Where(w => g[r, w] != b[r, w]).Select(w => $"第{w + 1}波 {F1(b[r, w])} → {F1(g[r, w])}")));
        }
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|--:|---|");
        Console.WriteLine($"| 規定 | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} |");
        Console.WriteLine($"| {VerNames[vi]} | {upR} ／ {upC} | {dnR} ／ {dnC} | {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} |");
        Console.WriteLine();
        foreach (var l in lines) Console.WriteLine("- " + l);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
