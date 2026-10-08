using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S293 = Shock293Diag;

// =====================================================================================
// guard294 —— 第294期「クグ KW-a ／ シガ SW-a の規定化 ＋ 守りの期（ヒサ・ソラ・ソム）」。
// 指示書は design/PHASE294_GUARD_SPEC.md ／ 報告は design/PHASE294_GUARD.md。
//
//     dotnet run --project BattleSim -c Release 0 guard294 p0          # Phase 0（§4）: ボスで味方が倒れる内訳・ヒサの踏みとどまりの見込み・ソラの被弾の型・標の層・ソムの喚び出し・味方の帯電
//     dotnet run --project BattleSim -c Release 0 guard294 compare     # `compare` 64 行 × 版（その駒の在席行だけ差し替える・規定は動かさない）
//     dotnet run --project BattleSim -c Release 0 guard294 boards      # 代表台 × ボス ／ 近衛 ／ 大隊 × 版（seed 0..199・verbose）と対照（R383）
//     dotnet run --project BattleSim -c Release 0 guard294 gridboss <hisa|sora|som> <版>   # ボスの格子（規定 ／ 版を同じ台で・R386）
//     dotnet run --project BattleSim -c Release 0 guard294 grid <guard|bat> <hisa|sora|som> <版>   # 精鋭の格子（固定枠 トウ ＋ その駒・探索枠3・第293期 §4-4 の作り）
//     dotnet run --project BattleSim -c Release 0 guard294 deep <boss|guard|bat> <席の並び> [版…]
//     dotnet run --project BattleSim -c Release 0 guard294 check       # 自己検査
//
// 版の名前（ASCII）: ヒサ hs0（規定）／ hsa ／ hsc ／ hsd、ソラ sr0（規定）／ sra ／ srb、ソム sm0（規定）／ sma ／ smb。
// 規定のクグ・シガ・カタは段0 の後（KW-a ・ SW-a ・ KR-∞）のまま組む（固定しない）。
// =====================================================================================
static class Guard294Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": P0(); return;
            case "compare": CompareAll(); return;
            case "boards": BoardsAll(A(3, "")); return;
            case "gridboss": GridBoss(A(3, "som"), A(4, "sma")); return;
            case "grid": GridElite(A(3, "guard"), A(4, "som"), A(5, "sma")); return;
            case "deep": DeepOne(A(3, "boss"), A(4, "シガ・ゴルム・クグ・カタ・ツギ"), args.Skip(5).ToArray()); return;
            case "check": Check(); return;
            case "memo": Memo(A(3, "糸"), A(4, "guard"), int.Parse(A(5, "0")), A(6, "web"), int.Parse(A(7, "40"))); return;
            default: Console.WriteLine("guard294: モードは p0 / compare / boards / gridboss / grid / deep / check / memo。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・波・台（測る前に固定）
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef From, UnitDef To);
    internal static readonly Ver[] HisaVers =
    {
        new("規定", "hs0", UnitCatalog.HisaHK0, UnitCatalog.HisaHK0), new("HS-a", "hsa", UnitCatalog.HisaHK0, UnitCatalog.HisaHSa),
        new("HS-c", "hsc", UnitCatalog.HisaHK0, UnitCatalog.HisaHSc), new("HS-d", "hsd", UnitCatalog.HisaHK0, UnitCatalog.HisaHSd),
        new("HS-a′", "hsa1", UnitCatalog.HisaHK0, UnitCatalog.HisaHSa1),   // 参考（指示書に無い・1体1戦1度）
    };
    internal static readonly Ver[] SoraVers =
    {
        new("規定", "sr0", UnitCatalog.SoraSR0, UnitCatalog.SoraSR0), new("SR-a", "sra", UnitCatalog.SoraSR0, UnitCatalog.SoraSRa), new("SR-b", "srb", UnitCatalog.SoraSR0, UnitCatalog.SoraSRb),
    };
    internal static readonly Ver[] SomVers =
    {
        new("規定", "sm0", UnitCatalog.Som, UnitCatalog.Som), new("SM-a", "sma", UnitCatalog.Som, UnitCatalog.SomSMa), new("SM-b", "smb", UnitCatalog.Som, UnitCatalog.SomSMb),
    };
    static Ver[] SetOf(string who) => who switch { "hisa" => HisaVers, "sora" => SoraVers, "som" => SomVers, _ => throw new ArgumentException(who) };
    static Ver AnyVer(string n) => HisaVers.Concat(SoraVers).Concat(SomVers).First(v => v.Ascii == n || v.Name == n);
    static Formation Apply(Formation f, params Ver[] vs) { foreach (var v in vs) if (!ReferenceEquals(v.From, v.To)) f = FvSwap(f, v.From, v.To); return f; }
    static bool Has(Formation f, UnitDef d) => f.Occupied().Any(o => ReferenceEquals(o.Def, d));

    static S287.Wave WaveOf(string n) => S287.Waves.First(w => w.Name == (n switch { "boss" => "ボス", "guard" => "近衛", "bat" => "大隊", _ => n }));
    const int Seeds = 200, CutSeeds = 20;

    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static UnitDef ByShort(string n) => UnitCatalog.All.First(d => Short(d) == n);
    static UnitDef[] Order(string s) => s.Split('・', StringSplitOptions.RemoveEmptyEntries).Select(ByShort).ToArray();
    static string OrderName(UnitDef[] o) => string.Join("・", o.Select(Short));
    static string Seats(Formation f) => string.Join("・", Enumerable.Range(0, 5).Select(i => f[i] is { } d ? Short(d) : "—"));
    static Formation Seat(UnitDef[] o) => Pin295(B283.Seat(o));
    static Formation Playtest(string n) => Pin295(Presets.Playtest.First(r => r.Name == n).F);
    static Formation Row(string n) => Pin295(CompareBuilds().First(r => r.Name == n).F);
    /// <summary>第295期: ソラを第294期の規定（`SoraSR0`）に固定する（規定のソラは SR-b になった）。陣形とレリックは保つ。</summary>
    internal static Formation Pin295(Formation f)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied()) if (ReferenceEquals(d, UnitCatalog.Sora)) g[slot] = UnitCatalog.SoraSR0;
        foreach (var (slot, d) in f.Occupied()) if (ReferenceEquals(d, UnitCatalog.Hisa)) g[slot] = UnitCatalog.HisaHK0;   // 第296期: ヒサも旧の規定（`HisaHK0`）に
        foreach (var (slot, d) in f.Occupied()) if (ReferenceEquals(d, UnitCatalog.Doha)) g[slot] = UnitCatalog.DohaD0;   // 第298期: ドハも旧の規定（`DohaD0`）に
        foreach (var (slot, d) in f.Occupied()) if (ReferenceEquals(d, UnitCatalog.Tome)) g[slot] = UnitCatalog.TomeMb;   // 第299期: ミサ ／ ザンも旧の規定（`TomeMb` ／ `ZanZN0`）に
        foreach (var (slot, d) in f.Occupied()) if (ReferenceEquals(d, UnitCatalog.Zan)) g[slot] = UnitCatalog.ZanZN0;
        return g;
    }

    /// <summary>
    /// 代表台（指示書 §5-2）。席は出典のまま、差し替えは同じ席で（`FvSwap`）。ヒサ ／ ソラが同じ台にいる場合として「標 ボス台 バン → ソラ」を足した（決めたこと）。
    /// </summary>
    internal static (string Name, string Who, Formation F)[] Boards() => new (string, string, Formation)[]
    {
        ("標 ボス台", "hisa", Playtest("試遊・標 ボス台")),
        ("標 ボス台 バン→ツギ", "hisa", FvSwap(Playtest("試遊・標 ボス台"), UnitCatalog.Ban, UnitCatalog.Tsugi)),
        ("標 ボス台 バン→リリ", "hisa", FvSwap(Playtest("試遊・標 ボス台"), UnitCatalog.Ban, UnitCatalog.Lili)),
        ("標経済", "hisa", Row("標経済 (ヒサ×ザン×ミサ)")),
        ("標 ボス台 バン→ソラ", "hisa+sora", FvSwap(Playtest("試遊・標 ボス台"), UnitCatalog.Ban, UnitCatalog.SoraSR0)),
        ("標 道中（見境改）", "sora", Playtest("試遊・標 道中")),
        ("見境", "sora", Row("見境 (ミサ×ソラ)")),
        ("感電 糸", "sora", Playtest("試遊・感電 糸")),
        ("293 四枚 ＋ ソラ", "sora", Seat(Order("トウ・クグ・シガ・カタ・ソラ"))),
        ("感電 (シガ×カタ×ソム)", "som", Row("感電 (シガ×カタ×ソム)")),
        ("雷の型 ドハ→ソム", "som", FvSwap(Playtest("試遊・感電 雷の型"), UnitCatalog.DohaD0, UnitCatalog.Som)),   // 第298期: `Pin295` が先にドハを旧に替える
        ("感電 糸 ガルド→ソム", "som", FvSwap(Playtest("試遊・感電 糸"), UnitCatalog.Gald, UnitCatalog.Som)),
        ("293 ボスの勝ち台", "base", Seat(Order("シガ・ゴルム・クグ・カタ・ツギ"))),
        ("勝ち台 ゴルム→ソム", "som", FvSwap(Seat(Order("シガ・ゴルム・クグ・カタ・ツギ")), UnitCatalog.Golm, UnitCatalog.Som)),
        ("勝ち台 ツギ→ソム", "som", FvSwap(Seat(Order("シガ・ゴルム・クグ・カタ・ツギ")), UnitCatalog.Tsugi, UnitCatalog.Som)),
    };

    /// <summary>台の版の組（その駒がいる版だけ・ヒサ × ソラ × ソム）。</summary>
    static IEnumerable<(string Name, Formation F)> Variants(Formation f)
    {
        var hs = Has(f, UnitCatalog.HisaHK0) ? HisaVers : new Ver[] { HisaVers[0] };
        var rs = Has(f, UnitCatalog.SoraSR0) ? SoraVers : new Ver[] { SoraVers[0] };
        var ms = Has(f, UnitCatalog.Som) ? SomVers : new Ver[] { SomVers[0] };
        foreach (var h in hs) foreach (var r in rs) foreach (var m in ms)
        {
            var parts = new List<string>();
            if (Has(f, UnitCatalog.HisaHK0)) parts.Add(h.Name == "規定" ? "ヒサ規定" : h.Name);
            if (Has(f, UnitCatalog.SoraSR0)) parts.Add(r.Name == "規定" ? "ソラ規定" : r.Name);
            if (Has(f, UnitCatalog.Som)) parts.Add(m.Name == "規定" ? "ソム規定" : m.Name);
            if (parts.Count == 0) parts.Add("規定");
            yield return (string.Join(" × ", parts), Apply(f, h, r, m));
        }
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Deep
    {
        public long N, Wins, WinT, LoseT, Caps, FirstDeathN, FirstDeathT, HeroN, HeroTurns, HeroOver;
        // ヒサ
        public long HN, Holds, Granted, GraceStops, Bridge, BridgeGain, BridgePlank, BridgeKiss, NextHit, NextHealed, NextKilled, Aided, HealerLeft, NoHealer, LostStripped, StrippedHits;
        public long HDied, HDeathT;
        // ソラ
        public long RN, Shoulders, ShoulderAmt, Deflects, WideDeflects, SoraDmg, SoraAttacks, PressHits, PressCut, RDied, RDeathT;
        public long[] PressByLayer = new long[4], PressHitsByLayer = new long[4], SoraHitPat = new long[5];
        // ソム
        public long MN, Spread, MemHits, MemSaved, StunSkipped, ShockStalls, Summons, MDied, MDeathT, AllyDis, AllyPops;
        public long[] MemBySrc = new long[5];
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Merge(Deep o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LoseT += o.LoseT; Caps += o.Caps; FirstDeathN += o.FirstDeathN; FirstDeathT += o.FirstDeathT; HeroN += o.HeroN; HeroTurns += o.HeroTurns; HeroOver += o.HeroOver;
            HN += o.HN; Holds += o.Holds; Granted += o.Granted; GraceStops += o.GraceStops; Bridge += o.Bridge; BridgeGain += o.BridgeGain; BridgePlank += o.BridgePlank; BridgeKiss += o.BridgeKiss;
            NextHit += o.NextHit; NextHealed += o.NextHealed; NextKilled += o.NextKilled; Aided += o.Aided; HealerLeft += o.HealerLeft; NoHealer += o.NoHealer; LostStripped += o.LostStripped; StrippedHits += o.StrippedHits;
            HDied += o.HDied; HDeathT += o.HDeathT;
            RN += o.RN; Shoulders += o.Shoulders; ShoulderAmt += o.ShoulderAmt; Deflects += o.Deflects; WideDeflects += o.WideDeflects; SoraDmg += o.SoraDmg; SoraAttacks += o.SoraAttacks;
            PressHits += o.PressHits; PressCut += o.PressCut; RDied += o.RDied; RDeathT += o.RDeathT;
            for (int i = 0; i < 4; i++) { PressByLayer[i] += o.PressByLayer[i]; PressHitsByLayer[i] += o.PressHitsByLayer[i]; }
            for (int i = 0; i < 5; i++) { SoraHitPat[i] += o.SoraHitPat[i]; MemBySrc[i] += o.MemBySrc[i]; }
            AllyDis += o.AllyDis; AllyPops += o.AllyPops;
            MN += o.MN; Spread += o.Spread; MemHits += o.MemHits; MemSaved += o.MemSaved; StunSkipped += o.StunSkipped; ShockStalls += o.ShockStalls; Summons += o.Summons; MDied += o.MDied; MDeathT += o.MDeathT;
        }
    }

    static int PatIdx(AttackPattern? p) => p switch { AttackPattern.Single => 0, AttackPattern.Sweep => 1, AttackPattern.Pierce => 2, AttackPattern.All => 3, _ => 4 };

    internal static Deep FightDeep(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var d = new Deep { N = 1, Summons = r.BetraySummoned };
        if (r.PlayerWon) { d.Wins = 1; d.WinT = r.Turns; } else { d.LoseT = r.Turns; if (r.Turns >= BattleEngine.MaxTurns) d.Caps = 1; }
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        UnitState? hi = p.FirstOrDefault(u => u.Def.Id == "hisa"), so = p.FirstOrDefault(u => u.Def.Id == "sora"), sm = p.FirstOrDefault(u => u.Def.Id == "som");
        if (hi is not null && r.TallyByUnit.TryGetValue("hisa", out var ht))
        {
            d.HN = 1; d.Holds = ht.HoldFires; d.Granted = ht.GraceGranted; d.GraceStops = ht.GraceStops; d.Bridge = ht.BridgeFired; d.BridgeGain = ht.BridgeGain;
            d.BridgePlank = ht.BridgeByPlank; d.BridgeKiss = ht.BridgeByKiss; d.NextHit = ht.HoldNextHit; d.NextHealed = ht.HoldNextHealed; d.NextKilled = ht.HoldNextKilled;
            d.Aided = ht.HoldAided; d.HealerLeft = ht.HoldHealerLeft; d.NoHealer = ht.HoldNoHealer; d.LostStripped = ht.HoldLostStripped; d.StrippedHits = ht.BeckonStrippedHits;
        }
        if (so is not null && r.TallyByUnit.TryGetValue("sora", out var rt))
        {
            d.RN = 1; d.Shoulders = rt.WideShoulders; d.ShoulderAmt = rt.WideShoulderAmt; d.Deflects = rt.DeflectHits; d.WideDeflects = rt.WideDeflects;
            d.SoraDmg = rt.DamageToEnemy; d.SoraAttacks = rt.Attacks; d.PressHits = rt.PressureHits; d.PressCut = rt.PressureCut;
            for (int i = 0; i < 4; i++) { d.PressByLayer[i] = rt.PressureByLayer?[i] ?? 0; d.PressHitsByLayer[i] = rt.PressureHitsByLayer?[i] ?? 0; }
        }
        if (sm is not null && r.TallyByUnit.TryGetValue("som", out var mt))
        {
            d.MN = 1; d.Spread = mt.MembraneSpread; d.MemHits = mt.MembraneHits; d.MemSaved = mt.MembraneSaved;
            for (int i = 0; i < 5; i++) d.MemBySrc[i] = mt.MembraneBySrc?[i] ?? 0;
        }
        foreach (var id in p.Select(u => u.Def.Id).Distinct())
            if (r.TallyByUnit.TryGetValue(id, out var at)) { d.StunSkipped += at.MembraneStunSkipped; d.ShockStalls += at.StallShockStun; d.AllyDis += at.DischargeTaken; d.AllyPops += at.ShockSpent; }
        UnitState? hero = e.FirstOrDefault(u => u.Def == EnemyCatalog.BossRegular);
        var dmgT = new long[32]; var healT = new long[32];
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Damage when so is not null && ev.TargetId == so.InstanceId && ev.ActorId is int a && !mine.Contains(a) && !ev.Relayed:
                    d.SoraHitPat[PatIdx(ev.Pattern)]++; break;
                case BattleEventKind.Damage when hero is not null && ev.TargetId == hero.InstanceId: dmgT[Math.Min(31, ev.Turn)] += ev.Amount; break;
                case BattleEventKind.Heal when hero is not null && ev.TargetId == hero.InstanceId: healT[Math.Min(31, ev.Turn)] += ev.Amount; break;
                case BattleEventKind.Death when ev.TargetId is int t && mine.Contains(t):
                    if (d.FirstDeathN == 0) { d.FirstDeathN = 1; d.FirstDeathT = ev.Turn; }
                    if (hi is not null && t == hi.InstanceId && d.HDied == 0) { d.HDied = 1; d.HDeathT = ev.Turn; }
                    if (so is not null && t == so.InstanceId && d.RDied == 0) { d.RDied = 1; d.RDeathT = ev.Turn; }
                    if (sm is not null && t == sm.InstanceId && d.MDied == 0) { d.MDied = 1; d.MDeathT = ev.Turn; }
                    break;
            }
        }
        if (hero is not null)
        {
            d.HeroN = 1;
            for (int t = 1; t <= Math.Min(31, r.Turns); t++) { d.HeroTurns++; if (dmgT[t] > healT[t]) d.HeroOver++; }
        }
        return d;
    }

    internal static Deep MeasureDeep(Formation f, S287.Wave w, int n)
    {
        var parts = new Deep[n];
        Parallel.For(0, n, i => parts[i] = FightDeep(f, w, i));
        var all = new Deep();
        foreach (var d in parts) all.Merge(d);
        return all;
    }

    static bool FightLite(Formation f, S287.Wave w, int seed, out int turns)
    {
        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), seed, verbose: false);
        turns = r.Turns;
        return r.PlayerWon;
    }
    static int Wins(Formation f, S287.Wave w, int n, out long winT)
    {
        int wins = 0; winT = 0;
        for (int s = 0; s < n; s++) if (FightLite(f, w, s, out int t)) { wins++; winT += t; }
        return wins;
    }
    static int WinsPar(Formation f, S287.Wave w, int n)
    {
        int wins = 0;
        Parallel.For(0, n, s => { if (FightLite(f, w, s, out _)) Interlocked.Increment(ref wins); });
        return wins;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F0");
    static string Pct(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1") + "%";
    static string Dt(long died, long t, long n) => died == 0 ? "—" : $"{Per1(t, died)}（{Pct(died, n)}）";

    // ---------------------------------------------------------------------------------
    // Phase 0（§4）
    // ---------------------------------------------------------------------------------
    /// <summary>§4 の 1 の台: 第293期の代表台（感電・規定のクグ ／ シガのまま）＋ 第292 ／ 293期のボスの勝ち台 ＋ 標のボス台（決めたこと）。</summary>
    static (string Name, Formation F)[] BossBoards()
    {
        var l = new List<(string, Formation)>();
        foreach (var n in new[] { "試遊・感電 火の型", "試遊・感電 雷の型", "試遊・感電 糸" }) l.Add((n, Playtest(n)));
        foreach (var (n, o) in new[] { ("292 雷の型 ソラ→クグ", "トウ・クグ・シガ・カタ・ドハ"), ("292 雷の型 ドハ→クグ", "トウ・ソラ・シガ・カタ・クグ"), ("292 四枚 ＋ ソラ", "トウ・クグ・シガ・カタ・ソラ"), ("292 四枚 ＋ ヴェル", "トウ・クグ・シガ・カタ・ヴェル") })
            l.Add((n, Seat(Order(o))));
        l.Add(("292 ボスの勝ち台", Seat(Order("トウ・ゴルム・クグ・カタ・ツギ"))));
        l.Add(("293 ボスの勝ち台", Seat(Order("シガ・ゴルム・クグ・カタ・ツギ"))));
        l.Add(("標 ボス台", Playtest("試遊・標 ボス台")));
        return l.ToArray();
    }

    sealed class P0Acc
    {
        public long N, Wins, Deaths, DeathTurns, MultiTurns, AnyTurns;
        public long[] DeathT = new long[12], DeathPat = new long[6], HpBefore = new long[5];
        // 味方の帯電（生きている味方 × ターン）
        public long AliveTurns, ShockedTurns;
        public long[] ShockedBy = new long[5];
        // ソラ
        public long SoraN, ShoulderEst;
        public long[] SoraPat = new long[5];
        public void Merge(P0Acc o)
        {
            N += o.N; Wins += o.Wins; Deaths += o.Deaths; DeathTurns += o.DeathTurns; MultiTurns += o.MultiTurns; AnyTurns += o.AnyTurns;
            for (int i = 0; i < 12; i++) DeathT[i] += o.DeathT[i];
            for (int i = 0; i < 6; i++) DeathPat[i] += o.DeathPat[i];
            for (int i = 0; i < 5; i++) { HpBefore[i] += o.HpBefore[i]; ShockedBy[i] += o.ShockedBy[i]; SoraPat[i] += o.SoraPat[i]; }
            AliveTurns += o.AliveTurns; ShockedTurns += o.ShockedTurns; SoraN += o.SoraN; ShoulderEst += o.ShoulderEst;
        }
    }

    /// <summary>
    /// Phase 0 の1戦（verbose の台本から）: 味方が倒れた手番・倒した一撃の型（直前の Damage の `Pattern`・敵の出どころでなければ「刻み・放電ほか」）・倒れる直前の HP 割合・
    /// 同じターンに2体以上倒れたターン ／ 味方の帯電（ターンの終わりに帯電している生きている味方・書き手の種類）／ ソラへの敵の一撃の型と、SR-a の肩代わりの見込み（ソラ以外の味方への単体以外の敵の一撃 × 50%）。
    /// </summary>
    static P0Acc FightP0(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var a = new P0Acc { N = 1, Wins = r.PlayerWon ? 1 : 0 };
        var mine = p.ToDictionary(u => u.InstanceId);
        var hp = p.ToDictionary(u => u.InstanceId, u => u.MaxHp);
        var maxHp = p.ToDictionary(u => u.InstanceId, u => u.MaxHp);
        var alive = p.Select(u => u.InstanceId).ToHashSet();
        var shocked = new Dictionary<int, int>();   // 味方 → 書き手の種類
        UnitState? so = p.FirstOrDefault(u => u.Def.Id == "sora");
        if (so is not null) a.SoraN = 1;
        var all = p.Concat(e).ToDictionary(u => u.InstanceId);
        int Cat(int? writer, int target)
        {
            if (writer is not int wi || !mine.TryGetValue(wi, out var wu)) return 4;
            return wu.Def.Id switch { "tou" => 0, "kata" => 1, "som" => 2, _ => 3 };
        }
        var deathsByTurn = new Dictionary<int, int>();
        (AttackPattern? Pat, bool Foe, int Hp) last = default;
        var lastBy = new Dictionary<int, (AttackPattern? Pat, bool Foe, int Hp)>();
        int curT = 0;
        void CloseTurn()
        {
            if (curT <= 0) return;
            a.AliveTurns += alive.Count;
            foreach (var id in alive) if (shocked.TryGetValue(id, out int c)) { a.ShockedTurns++; a.ShockedBy[c]++; }
        }
        foreach (var ev in r.Events)
        {
            if (ev.Turn != curT) { CloseTurn(); curT = ev.Turn; }
            switch (ev.Kind)
            {
                case BattleEventKind.Damage when ev.TargetId is int t && mine.ContainsKey(t):
                {
                    bool foe = ev.ActorId is int ai && !mine.ContainsKey(ai) && !ev.FriendlyFire;
                    lastBy[t] = (ev.Pattern, foe, hp[t]);
                    hp[t] = ev.HpAfter;
                    if (so is not null && t == so.InstanceId && foe && !ev.Relayed) a.SoraPat[PatIdx(ev.Pattern)]++;
                    if (so is not null && t != so.InstanceId && foe && !ev.Relayed && ev.Pattern is AttackPattern pp && pp != AttackPattern.Single && alive.Contains(so.InstanceId)) a.ShoulderEst += ev.Amount / 2;
                    break;
                }
                case BattleEventKind.Heal when ev.TargetId is int t && mine.ContainsKey(t): hp[t] = ev.HpAfter; break;
                case BattleEventKind.StatusGain when ev.TargetId is int t && mine.ContainsKey(t) && ev.Text == StatusKeys.Shock: shocked[t] = Cat(ev.ActorId, t); break;
                case BattleEventKind.ShockSpent when ev.TargetId is int t && mine.ContainsKey(t): shocked.Remove(t); break;
                case BattleEventKind.Death when ev.TargetId is int t && mine.ContainsKey(t):
                {
                    alive.Remove(t); shocked.Remove(t);
                    a.Deaths++;
                    a.DeathT[Math.Min(11, ev.Turn)]++;
                    deathsByTurn[ev.Turn] = deathsByTurn.GetValueOrDefault(ev.Turn) + 1;
                    var lb = lastBy.TryGetValue(t, out var x) ? x : (null, false, maxHp[t]);
                    a.DeathPat[lb.Foe ? PatIdx(lb.Pat) : 5]++;
                    int pct = lb.Hp * 100 / Math.Max(1, maxHp[t]);
                    a.HpBefore[pct <= 10 ? 0 : pct <= 25 ? 1 : pct <= 50 ? 2 : pct <= 75 ? 3 : 4]++;
                    break;
                }
            }
        }
        CloseTurn();
        a.AnyTurns = deathsByTurn.Count;
        a.MultiTurns = deathsByTurn.Count(kv => kv.Value >= 2);
        a.DeathTurns = deathsByTurn.Values.Sum();
        return a;
    }

    static P0Acc MeasureP0(Formation f, S287.Wave w, int n)
    {
        var parts = new P0Acc[n];
        Parallel.For(0, n, i => parts[i] = FightP0(f, w, i));
        var all = new P0Acc();
        foreach (var x in parts) all.Merge(x);
        return all;
    }

    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第294期 Phase 0 —— 守りの期の下調べ（段0 の後の規定・seed 0..199・verbose）");
        Console.WriteLine();
        var boss = S287.Waves[0];

        Console.WriteLine("## §4 の 1 ボス規定形で味方が倒れる内訳（規定の駒）");
        Console.WriteLine();
        Console.WriteLine("型 ＝ 倒した一撃（直前の Damage）の型。敵の出どころでない一撃（刻み・放電・同士討ち）は「ほか」。HP ＝ 倒れる一撃を受ける直前の HP ／ 最大HP。");
        Console.WriteLine("2体以上 ＝ 味方が倒れたターンのうち、同じターンに2体以上倒れたターンの割合（HS-c の橋は1ターンに1度）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 倒れた（1戦） | 倒れたT 1 ／ 2 ／ 3 ／ 4 ／ 5 ／ 6〜 | 型 全体 ／ 単体 ／ 薙ぎ・貫き ／ ほか | 直前の HP ≦10% ／ ≦25% ／ ≦50% ／ ≦75% ／ 超 | 2体以上のターン |");
        Console.WriteLine("|---|--:|--:|---|---|---|--:|");
        var tot = new P0Acc();
        foreach (var (name, f) in BossBoards())
        {
            var a = MeasureP0(f, boss, Seeds);
            tot.Merge(a);
            Console.WriteLine(P0Row(name, a));
        }
        Console.WriteLine(P0Row("**合計**", tot));
        Console.WriteLine();

        Console.WriteLine("## §4 の 2 ／ 3 ヒサの踏みとどまり（HS-a を回して数える・engine の計数）");
        Console.WriteLine();
        Console.WriteLine("止めた ＝ 踏みとどまり（1戦）。手番の残る癒し手 ＝ 踏みとどまったターンに、まだ手番を終えていない癒し手（リリ ／ ツギ）がいた割合（HS-a のまま橋が架かる見込み）。");
        Console.WriteLine("応急処置 ＝ 同じ一撃でツギの応急処置（駆け込み）が先に届いた割合。剥がされて止められなかった ＝ ヒサの記憶はその駒を指しているが標が剥がれていた倒れる一撃（ソラの逸らしなど）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率（HS-a） | 止めた | 手番の残る癒し手 | 癒し手なし | 応急処置が先 | 次の一撃 ／ うち癒やされていた ／ その一撃で倒れた | 剥がされて止められなかった | 剥がされた被弾（矢面） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|---|--:|--:|");
        foreach (var (name, who, f) in Boards().Where(b => b.Who.Contains("hisa")))
            foreach (var w in S287.Waves)
            {
                var d = MeasureDeep(Apply(f, HisaVers[1]), w, Seeds);
                Console.WriteLine($"| {name} | {w.Name} | {F1(d.Win)} | {Per(d.Holds, d.HN)} | {Pct(d.HealerLeft, d.Holds)} | {Pct(d.NoHealer, d.Holds)} | {Pct(d.Aided, d.Holds)} | "
                    + $"{Per(d.NextHit, d.HN)} ／ {Pct(d.NextHealed, d.NextHit)} ／ {Pct(d.NextKilled, d.NextHit)} | {Per(d.LostStripped, d.HN)} | {Per(d.StrippedHits, d.HN)} |");
            }
        Console.WriteLine();
        Console.WriteLine("矢面の半減は型を問わない（engine の軽減の族の条件は「相手陣営の出どころ・刻み／徴収／中継／共有ではない」だけで `pattern` を読まない——全体攻撃でも半分）。");
        Console.WriteLine();

        Console.WriteLine("## §4 の 4 ソラが受けた一撃の型と、SR-a の肩代わりの見込み（規定の駒）");
        Console.WriteLine();
        Console.WriteLine("見込み ＝ ソラが生きている間にソラ以外の味方が受けた単体以外の敵の一撃 × 50%（1戦）。持つターン ＝ ソラの HP 96 ÷（見込み ÷ 決着T ＋ ソラが受けた量 ÷ 決着T）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 決着T | ソラへの一撃 単体 ／ 薙ぎ ／ 貫き ／ 全体 | 逸らした | 突き 1発 | ソラ 倒れたT | 肩代わりの見込み | 持つターン |");
        Console.WriteLine("|---|---|--:|--:|---|--:|--:|--:|--:|--:|");
        foreach (var (name, who, f) in Boards().Where(b => b.Who.Contains("sora")))
            foreach (var w in S287.Waves)
            {
                var d = MeasureDeep(f, w, Seeds);
                var a = MeasureP0(f, w, Seeds);
                double turns = (double)(d.WinT + d.LoseT) / d.N;
                // ソラが受けた量は p0 の帳簿に無いので、規定のソラの DamageTaken を別に引く
                long taken = 0;
                for (int s = 0; s < 40; s++) { var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false); if (r.TallyByUnit.TryGetValue("sora", out var t)) taken += t.DamageTaken; }
                double per = (a.ShoulderEst / (double)a.N + taken / 40.0) / Math.Max(1, turns);
                Console.WriteLine($"| {name} | {w.Name} | {F1(d.Win)} | {turns:F1} | {Per(a.SoraPat[0], a.N)} ／ {Per(a.SoraPat[1], a.N)} ／ {Per(a.SoraPat[2], a.N)} ／ {Per(a.SoraPat[3], a.N)} | {Per(d.Deflects, d.RN)} | {Per0(d.SoraDmg, d.SoraAttacks)} | {Dt(d.RDied, d.RDeathT, d.N)} | {Per0(a.ShoulderEst, a.N)} | {(per <= 0 ? "—" : (96 / per).ToString("F1"))} |");
            }
        Console.WriteLine();

        Console.WriteLine("## §4 の 5 標の層（SR-b を回して、重圧が掛かった敵の一撃を層ごとに数える）");
        Console.WriteLine();
        Console.WriteLine("層は炸裂（ミサ）の保持者がいる戦だけ 2 以上になる（`BattleContext.MarkLayers`）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | ミサ | 重圧の一撃（1戦） | 層 1 ／ 2 ／ 3+ |");
        Console.WriteLine("|---|---|---|--:|---|");
        foreach (var (name, who, f) in Boards().Where(b => b.Who.Contains("sora")))
            foreach (var w in S287.Waves)
            {
                var d = MeasureDeep(Apply(f, SoraVers[2]), w, Seeds);
                long n = d.PressHitsByLayer.Sum();
                Console.WriteLine($"| {name} | {w.Name} | {(f.Occupied().Any(o => o.Def.Id == UnitCatalog.Tome.Id) ? "○" : "")} | {Per(d.PressHits, d.RN)} | {Pct(d.PressHitsByLayer[1], n)} ／ {Pct(d.PressHitsByLayer[2], n)} ／ {Pct(d.PressHitsByLayer[3], n)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## §4 の 6 ソムの喚び出し（ボス規定形・規定のソム）");
        Console.WriteLine();
        Console.WriteLine($"- 喚ばれたもの（背いた獣）: HP {UnitCatalog.Fodder.MaxHp}・攻 {UnitCatalog.Fodder.Attack}・速 {UnitCatalog.Fodder.Speed}・札 {string.Join("・", UnitCatalog.Fodder.Traits)}（動かない・攻撃しない）。席は ○前2（{BetrayedTrait.FodderSlot}）。同時に1体。");
        Console.WriteLine($"- 勇者（中央・席 2）の隣か: {(FormationRules.AreAdjacent(BetrayedTrait.FodderSlot, 2) ? "**隣**" : "隣ではない")}（X 字の表）。");
        Console.WriteLine("- 勝敗: 勝ちは「敵の陣営に生きている駒がいない」（`TeamAlive`）——**喚ばれたものが生きていれば勇者を倒しても勝ちにならない**（攻撃しない駒なので、残った分は次の手番で倒す手間になる）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 喚んだ（1戦） | ソム 倒れたT |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var (name, who, f) in Boards().Where(b => b.Who.Contains("som")))
        {
            var d = MeasureDeep(f, boss, Seeds);
            Console.WriteLine($"| {name} | {F1(d.Win)} | {Per(d.Summons, d.N)} | {Dt(d.MDied, d.MDeathT, d.N)} |");
        }
        Console.WriteLine();

        Console.WriteLine("## §4 の 7 味方の帯電の供給（規定の駒・ターンの終わりに帯電している生きている味方 ÷ 生きている味方）");
        Console.WriteLine();
        Console.WriteLine("書き手 ＝ その帯電を付けた駒（トウの粉・カタの雷の漏れ・ソム・ほかの味方（ベニの反転ほか）・敵）。SM の膜は書き手を問わず帯電した味方に効く。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 帯電している割合 | 書き手 トウ ／ カタ ／ ソム ／ ほかの味方 ／ 敵 |");
        Console.WriteLine("|---|---|--:|---|");
        foreach (var (name, f) in BossBoards().Concat(Boards().Where(b => b.Who.Contains("som")).Select(b => (b.Name, b.F))).DistinctBy(b => b.Item1))
            foreach (var w in S287.Waves)
            {
                var a = MeasureP0(f, w, Seeds);
                long s = a.ShockedTurns;
                Console.WriteLine($"| {name} | {w.Name} | {Pct(a.ShockedTurns, a.AliveTurns)} | {Pct(a.ShockedBy[0], s)} ／ {Pct(a.ShockedBy[1], s)} ／ {Pct(a.ShockedBy[2], s)} ／ {Pct(a.ShockedBy[3], s)} ／ {Pct(a.ShockedBy[4], s)} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static string P0Row(string name, P0Acc a)
    {
        long d = a.Deaths;
        return $"| {name} | {Pct(a.Wins, a.N)} | {Per(a.Deaths, a.N)} | {Pct(a.DeathT[1], d)} ／ {Pct(a.DeathT[2], d)} ／ {Pct(a.DeathT[3], d)} ／ {Pct(a.DeathT[4], d)} ／ {Pct(a.DeathT[5], d)} ／ {Pct(a.DeathT.Skip(6).Sum(), d)} | "
            + $"{Pct(a.DeathPat[3], d)} ／ {Pct(a.DeathPat[0], d)} ／ {Pct(a.DeathPat[1] + a.DeathPat[2], d)} ／ {Pct(a.DeathPat[4] + a.DeathPat[5], d)} | "
            + $"{Pct(a.HpBefore[0], d)} ／ {Pct(a.HpBefore[1], d)} ／ {Pct(a.HpBefore[2], d)} ／ {Pct(a.HpBefore[3], d)} ／ {Pct(a.HpBefore[4], d)} | {Pct(a.MultiTurns, a.AnyTurns)} |";
    }

    // ---------------------------------------------------------------------------------
    // `compare` 64 行 × 版
    // ---------------------------------------------------------------------------------
    static readonly Dictionary<Ver, int[]> _caps = new();
    static int[] _capsBasis = Array.Empty<int>();
    static double[,] CompareGrid(Ver? v)
    {
        var rows = CompareBuilds().Select(r => (r.Name, F: Pin295(r.F))).ToArray();
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        var caps = new int[rows.Length];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            var f = v is null ? rows[ri].F : Apply(rows[ri].F, v);
            int wins = 0, cap = 0;
            for (int s = 0; s < Seeds; s++) { var r = BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false); if (r.PlayerWon) wins++; else if (wi > 0 && r.Turns >= BattleEngine.MaxTurns) cap++; }
            g[ri, wi] = 100.0 * wins / Seeds;
            if (cap > 0) Interlocked.Add(ref caps[ri], cap);
        });
        if (v is not null) lock (_caps) _caps[v] = caps; else _capsBasis = caps;
        return g;
    }

    static void CompareAll()
    {
        var rows = CompareBuilds().Select(r => (r.Name, F: Pin295(r.F))).ToArray();
        int nw = EnemyCatalog.Stages.Count;
        Console.WriteLine("# 第294期 `compare` 64 行 × 版（seed 0..199・その駒の在席行だけ差し替える・基準は段0 の後の規定）");
        var basis = CompareGrid(null);
        var prim = Baseline.PrimaryRows.ToHashSet();
        foreach (var set in new[] { HisaVers.Skip(1).ToArray(), SoraVers.Skip(1).ToArray(), SomVers.Skip(1).ToArray() })
        {
            Console.WriteLine();
            Console.WriteLine($"## {Short(set[0].From)}（規定 ／ {string.Join(" ／ ", set.Select(v => v.Name))}）");
            Console.WriteLine();
            Console.WriteLine("| 行 | 版 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | 差 | 最大の落ち | 30T 上限の負け（第2〜5波・戦） | 主判定 |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|--:|--:|---|");
            var grids = set.Select(v => CompareGrid(v)).ToArray();
            int movedOther = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!Has(rows[ri].F, set[0].From))
                {
                    for (int vi = 0; vi < set.Length; vi++) for (int w = 0; w < nw; w++) if (grids[vi][ri, w] != basis[ri, w]) movedOther++;
                    continue;
                }
                double m0 = Enumerable.Range(1, nw - 1).Average(w => basis[ri, w]);
                Console.WriteLine($"| {rows[ri].Name} | 規定 | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => F1(basis[ri, w]))) + $" | {F1(m0)} | | | {_capsBasis[ri]} | {(prim.Contains(rows[ri].Name) ? "○" : "")} |");
                for (int vi = 0; vi < set.Length; vi++)
                {
                    var g = grids[vi];
                    double m = Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
                    double drop = Enumerable.Range(0, nw).Min(w => g[ri, w] - basis[ri, w]);
                    Console.WriteLine($"| {rows[ri].Name} | {set[vi].Name} | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w]))) + $" | {F1(m)} | {(m - m0):+0.0;-0.0;0.0} | {drop:+0.0;-0.0;0.0} | {_caps[set[vi]][ri]} | |");
                }
            }
            Console.WriteLine();
            Console.WriteLine($"その駒のいない行で動いたセル: **{movedOther}**（0 であること）");
        }
    }

    // ---------------------------------------------------------------------------------
    // 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cells = new List<(string Board, string Who, S287.Wave W, string V, Formation F)>();
        foreach (var w in S287.Waves)
            foreach (var (name, who, f) in Boards())
                if (only == "" || who.Contains(only))
                    foreach (var v in Variants(f)) cells.Add((name, who, w, v.Name, v.F));
        var res = new Deep[cells.Count];
        for (int i = 0; i < cells.Count; i++) res[i] = MeasureDeep(cells[i].F, cells[i].W, Seeds);

        Console.WriteLine("# 第294期 代表台 × 波 × 版（ヒサ 規定 ／ HS-a ／ HS-c ／ HS-d・ソラ 規定 ／ SR-a ／ SR-b・ソム 規定 ／ SM-a ／ SM-b・seed 0..199・verbose）");
        Console.WriteLine();
        foreach (var (name, who, f) in Boards()) if (only == "" || who.Contains(only)) Console.WriteLine($"- {name}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine();
        Console.WriteLine("差 ＝ 同じ台・同じ波の規定（版の1行目）との勝率の差。寿命 ＝ 味方が初めて倒れたT（倒れた戦だけの平均・括弧は倒れた戦の割合）。勇者の上回り ＝ 味方の与ダメが勇者の回復を上回ったターン ÷ ターン。");
        foreach (var w in S287.Waves)
        {
            Console.WriteLine();
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("### 表A 勝率・寿命");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 勝率 | 差 | 倒しT | 負けT（30T 上限） | 寿命（初めて倒れたT） | ヒサ ／ ソラ ／ ソム 倒れたT | 勇者の上回り |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|---|--:|");
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].W != w) continue;
                var a = res[i];
                int bi = cells.FindIndex(c => c.W == w && c.Board == cells[i].Board);
                Console.WriteLine($"| {cells[i].Board} | {cells[i].V} | {F1(a.Win)} | {(bi == i ? "" : (a.Win - res[bi].Win).ToString("+0.0;-0.0;0.0"))} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)}{(a.Caps > 0 ? $"（{Pct(a.Caps, a.N)}）" : "")} | "
                    + $"{Dt(a.FirstDeathN, a.FirstDeathT, a.N)} | {Dt(a.HDied, a.HDeathT, a.N)} ／ {Dt(a.RDied, a.RDeathT, a.N)} ／ {Dt(a.MDied, a.MDeathT, a.N)} | {Pct(a.HeroOver, a.HeroTurns)} |");
            }
            if (cells.Any(c => c.W == w && c.Who.Contains("hisa")))
            {
                Console.WriteLine();
                Console.WriteLine("### 表B ヒサ（1戦）");
                Console.WriteLine();
                Console.WriteLine("| 台 | 版 | 止めた | 次の一撃 ／ 癒やされていた ／ 倒れた | 応急処置が先 | 橋（ツギ ／ リリ）／ 増えた (HP＋破片) | 猶予 与えた ／ 止めた | 剥がされて止められなかった |");
                Console.WriteLine("|---|---|--:|---|--:|---|---|--:|");
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].W != w || !cells[i].Who.Contains("hisa")) continue;
                    var a = res[i];
                    Console.WriteLine($"| {cells[i].Board} | {cells[i].V} | {Per(a.Holds, a.HN)} | {Per(a.NextHit, a.HN)} ／ {Pct(a.NextHealed, a.NextHit)} ／ {Pct(a.NextKilled, a.NextHit)} | {Pct(a.Aided, a.Holds)} | "
                        + $"{Per(a.Bridge, a.HN)}（{Per(a.BridgePlank, a.HN)} ／ {Per(a.BridgeKiss, a.HN)}）／ {Per0(a.BridgeGain, a.Bridge)} | {Per(a.Granted, a.HN)} ／ {Per(a.GraceStops, a.HN)} | {Per(a.LostStripped, a.HN)} |");
                }
            }
            if (cells.Any(c => c.W == w && c.Who.Contains("sora")))
            {
                Console.WriteLine();
                Console.WriteLine("### 表C ソラ（1戦）");
                Console.WriteLine();
                Console.WriteLine("| 台 | 版 | 肩代わり 回 ／ 量 | 逸らした（うち範囲・肩代わり） | 突き 1発 ／ 与ダメ | 重圧 回 ／ 軽くした量 | 層 1 ／ 2 ／ 3+ の量 |");
                Console.WriteLine("|---|---|---|---|---|---|---|");
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].W != w || !cells[i].Who.Contains("sora")) continue;
                    var a = res[i];
                    Console.WriteLine($"| {cells[i].Board} | {cells[i].V} | {Per(a.Shoulders, a.RN)} ／ {Per0(a.ShoulderAmt, a.RN)} | {Per(a.Deflects, a.RN)}（{Per(a.WideDeflects, a.RN)}） | {Per0(a.SoraDmg, a.SoraAttacks)} ／ {Per0(a.SoraDmg, a.RN)} | "
                        + $"{Per(a.PressHits, a.RN)} ／ {Per0(a.PressCut, a.RN)} | {Per0(a.PressByLayer[1], a.RN)} ／ {Per0(a.PressByLayer[2], a.RN)} ／ {Per0(a.PressByLayer[3], a.RN)} |");
                }
            }
            if (cells.Any(c => c.W == w && c.Who.Contains("som")))
            {
                Console.WriteLine();
                Console.WriteLine("### 表D ソム（1戦）");
                Console.WriteLine();
                Console.WriteLine("| 台 | 版 | 膜で帯電させた | 半分にした 回 ／ 量 | 帯電の書き手 トウ ／ カタ ／ ソム ／ ほか ／ 敵 | 漏れ由来（トウ＋カタ） | 味方が弾けた ／ 味方が浴びた放電 | 痺れで失った手番（味方） | 痺れなかった | 喚んだ |");
                Console.WriteLine("|---|---|--:|---|---|--:|---|--:|--:|--:|");
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].W != w || !cells[i].Who.Contains("som")) continue;
                    var a = res[i];
                    long m = a.MemHits;
                    Console.WriteLine($"| {cells[i].Board} | {cells[i].V} | {Per(a.Spread, a.MN)} | {Per(a.MemHits, a.MN)} ／ {Per0(a.MemSaved, a.MN)} | {Pct(a.MemBySrc[0], m)} ／ {Pct(a.MemBySrc[1], m)} ／ {Pct(a.MemBySrc[2], m)} ／ {Pct(a.MemBySrc[3], m)} ／ {Pct(a.MemBySrc[4], m)} | "
                        + $"{Per(a.MemBySrc[0] + a.MemBySrc[1], a.MN)} | {Per(a.AllyPops, a.N)} ／ {Per0(a.AllyDis, a.N)} | {Per(a.ShockStalls, a.N)} | {Per(a.StunSkipped, a.N)} | {Per(a.Summons, a.N)} |");
                }
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 対照（R383）: 勝率 50% 以上のセルで、台の駒それぞれ → ドルガ（seed 0..199・非 verbose）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 台 | 版 | 勝率 | 前1 → | 前3 → | 中央 → | 後1 → | 後3 → |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < cells.Count; i++)
        {
            if (res[i].Win < 50) continue;
            var f = cells[i].F;
            string C(int slot) => f[slot] is { } d && !ReferenceEquals(d, UnitCatalog.Dolga) ? $"{Short(d)} {F1(100.0 * WinsPar(FvSwap(f, d, UnitCatalog.Dolga), cells[i].W, Seeds) / Seeds)}" : "—";
            Console.WriteLine($"| {cells[i].W.Name} | {cells[i].Board} | {cells[i].V} | {F1(res[i].Win)} | {C(0)} | {C(1)} | {C(2)} | {C(3)} | {C(4)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 格子
    // ---------------------------------------------------------------------------------
    sealed record GridRes(UnitDef[] Order, int[] Wins, long[] WinT, int[][] Ctl);

    /// <summary>格子の本体（第293期の `GridCore` と同じ作り）。規定と版を同じ台で回す（R386）。届いた ＝ 固定枠の駒のどれか1枚 → ドルガで半分以下。</summary>
    static void GridCore(string title, S287.Wave w, UnitDef[] fixedU, UnitDef[] pool, int k, (string Name, Func<Formation, Formation> Apply)[] vars)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lu = S293.Combos(pool, k);
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedU.Concat(t).ToArray()));
        var res = new GridRes?[boards.Count];
        int cutPass = 0;
        Parallel.For(0, boards.Count, i =>
        {
            var f0 = Seat(boards[i]);
            var fs = vars.Select(v => v.Apply(f0)).ToArray();
            if (!fs.Any(f => Wins(f, w, CutSeeds, out _) >= 5)) return;
            Interlocked.Increment(ref cutPass);
            var wins = new int[vars.Length]; var wt = new long[vars.Length]; var ctl = new int[vars.Length][];
            for (int v = 0; v < vars.Length; v++)
            {
                wins[v] = Wins(fs[v], w, Seeds, out wt[v]);
                ctl[v] = fixedU.Select(_ => -1).ToArray();
                if (wins[v] * 2 < Seeds) continue;
                var f = fs[v];
                for (int c = 0; c < fixedU.Length; c++)
                {
                    var occ = f.Occupied().First(o => o.Def.Id == fixedU[c].Id).Def;
                    ctl[v][c] = Wins(FvSwap(f, occ, UnitCatalog.Dolga), w, Seeds, out _);
                }
            }
            res[i] = new GridRes(boards[i], wins, wt, ctl);
        });
        var all = res.Where(r => r is not null).Select(r => r!).ToList();
        bool Half(GridRes r, int v) => r.Wins[v] * 2 >= Seeds;
        bool Reach(GridRes r, int v) => Half(r, v) && r.Ctl[v].Any(c => c >= 0 && c * 2 <= r.Wins[v]);
        bool Need(GridRes r, int v, int c) => Half(r, v) && r.Ctl[v][c] * 2 <= r.Wins[v];
        string Key(UnitDef[] o) => string.Join(",", o.Select(d => d.Id).OrderBy(s => s, StringComparer.Ordinal));

        Console.WriteLine($"# 第294期 格子 × {w.Name} × {title}");
        Console.WriteLine();
        Console.WriteLine($"探索枠{k}（候補 {pool.Length} 枚: {OrderName(pool)}・ヒーラー ≦ 1）＝ {lu.Count} 組 × 席 120 ＝ {boards.Count:N0} 台 × 版 {vars.Length}（{string.Join(" ／ ", vars.Select(v => v.Name))}）。");
        Console.WriteLine($"足切り seed 0..{CutSeeds - 1}（どれかの版で 5 勝以上・{cutPass:N0} 台が通った）→ 全版 seed 0..{Seeds - 1} → 勝率 50% 以上の版で固定枠の駒それぞれ → ドルガ。");
        Console.WriteLine("**届いた ＝ 固定枠の駒のどれか1枚 → ドルガで半分以下**。勝率 50% の線も併記する（第292期 R387）。");
        Console.WriteLine();
        Console.WriteLine("## 表1 段ごとの台数（組の数）");
        Console.WriteLine();
        Console.WriteLine("| 段 | " + string.Join(" | ", vars.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(vars.Select(_ => "--:|")));
        void RowP(string label, Func<GridRes, int, bool> pred) =>
            Console.WriteLine($"| {label} | " + string.Join(" | ", Enumerable.Range(0, vars.Length).Select(v => { var l = all.Where(r => pred(r, v)).ToList(); return $"{l.Count:N0}（{l.Select(r => Key(r.Order)).Distinct().Count()}）"; })) + " |");
        RowP("勝率 ≧ 50%", Half);
        RowP("**届いた**", Reach);
        for (int c = 0; c < fixedU.Length; c++) { int cc = c; RowP($"{Short(fixedU[c])}が要る（→ ドルガで半分以下）", (r, v) => Need(r, v, cc)); }
        RowP("勝率 ≧ 90%", (r, v) => r.Wins[v] * 10 >= Seeds * 9);
        Console.WriteLine();
        Console.WriteLine("## 表2 組（届いた台のある組・版ごとの最大勝率）");
        Console.WriteLine();
        Console.WriteLine("| 組（探索枠） | " + string.Join(" | ", vars.Select(v => v.Name + " 最大")) + " |");
        Console.WriteLine("|---|" + string.Concat(vars.Select(_ => "--:|")));
        foreach (var g in all.Where(r => Enumerable.Range(0, vars.Length).Any(v => Reach(r, v))).GroupBy(r => Key(r.Order)).OrderByDescending(g => g.Max(r => r.Wins.Max())))
        {
            var o = g.First().Order.Where(d => !fixedU.Any(x => x.Id == d.Id));
            Console.WriteLine($"| {OrderName(o.ToArray())} | " + string.Join(" | ", Enumerable.Range(0, vars.Length).Select(v => F1(100.0 * g.Max(r => r.Wins[v]) / Seeds))) + " |");
        }
        for (int v = 0; v < vars.Length; v++)
        {
            Console.WriteLine();
            Console.WriteLine($"## 表3-{v + 1} 上位の台（{vars[v].Name}・勝率 50% 以上・上位 15）");
            Console.WriteLine();
            Console.WriteLine($"| # | 席（前1・前3・中央・後1・後3） | 勝率 | 倒しT | 届いた | {string.Join(" | ", fixedU.Select(d => Short(d) + " →"))} | {string.Join(" | ", Enumerable.Range(0, vars.Length).Where(x => x != v).Select(x => vars[x].Name))} |");
            Console.WriteLine("|--:|---|--:|--:|---|" + string.Concat(fixedU.Select(_ => "--:|")) + string.Concat(Enumerable.Range(0, vars.Length - 1).Select(_ => "--:|")));
            var top = all.Where(r => Half(r, v)).OrderByDescending(r => r.Wins[v]).ThenBy(r => (double)r.WinT[v] / Math.Max(1, r.Wins[v])).ThenBy(r => OrderName(r.Order), StringComparer.Ordinal).ToList();
            for (int i = 0; i < Math.Min(15, top.Count); i++)
            {
                var r = top[i];
                Console.WriteLine($"| {i + 1} | {OrderName(r.Order)} | {F1(100.0 * r.Wins[v] / Seeds)} | {Per1(r.WinT[v], r.Wins[v])} | {(Reach(r, v) ? "○" : "")} | {string.Join(" | ", r.Ctl[v].Select(c => c < 0 ? "—" : F1(100.0 * c / Seeds)))} | "
                    + string.Join(" | ", Enumerable.Range(0, vars.Length).Where(x => x != v).Select(x => F1(100.0 * r.Wins[x] / Seeds))) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>
    /// ボスの格子（§5-2）。ソム ＝ 固定枠 クグ ＋ カタ ＋ ソム・探索枠2（第293期のボスの格子の候補からソムを除き、規定のシガを足した枠——第293期はシガが固定枠だった・決めたこと）。
    /// ヒサ ／ ソラ ＝ 第283期の標軸の格子の作り（固定枠 ミサ ＋ ザン）にその駒を足した固定枠・探索枠2（第283期の候補からその駒を除いた枠）。
    /// </summary>
    static (UnitDef[] Fixed, UnitDef[] Pool) BossFrame(string who) => who switch
    {
        "som" => (new[] { UnitCatalog.Kugu, UnitCatalog.Kata, UnitCatalog.Som }, S293.BossPool.Where(d => d.Id != "som").Append(UnitCatalog.Shiga).ToArray()),
        "hisa" => (new[] { UnitCatalog.Tome, UnitCatalog.Zan, UnitCatalog.HisaHK0 }, B283.LifePool.Concat(B283.HealPool295.Where(h => !B283.LifePool.Contains(h))).Where(d => d.Id != "hisa").ToArray()),
        "sora" => (new[] { UnitCatalog.Tome, UnitCatalog.Zan, UnitCatalog.SoraSR0 }, B283.LifePool.Concat(B283.HealPool295.Where(h => !B283.LifePool.Contains(h))).Where(d => d.Id != "sora").ToArray()),
        _ => throw new ArgumentException(who),
    };

    static void GridBoss(string who, string ver)
    {
        var w = S287.Waves[0];
        var v = AnyVer(ver);
        var set = SetOf(who);
        var (fixedU, pool) = BossFrame(who);
        var vars = new (string, Func<Formation, Formation>)[] { ("規定", f => f), (v.Name, f => Apply(f, v)) };
        GridCore($"固定枠 {OrderName(fixedU)}（{Short(set[0].From)}: 規定 ／ {v.Name}）", w, fixedU, pool, 2, vars);
    }

    /// <summary>精鋭の格子（第293期 §4-4 の作り）: 固定枠 トウ ＋ その駒・探索枠3 ＝ 第287期の候補（クグ ／ カタは規定に替える）からその駒を除いた枠。</summary>
    static void GridElite(string waveName, string who, string ver)
    {
        var w = WaveOf(waveName);
        var v = AnyVer(ver);
        var set = SetOf(who);
        UnitDef fixed2 = set[0].From;
        var pool = S287.Pool.Select(d => d.Id switch { "kata" => UnitCatalog.Kata, "kugu" => UnitCatalog.Kugu, _ => d })
                            .Where(d => d.Id != fixed2.Id && d.Id != "tou").Distinct().ToArray();
        var vars = new (string, Func<Formation, Formation>)[] { ("規定", f => f), (v.Name, f => Apply(f, v)) };
        GridCore($"固定枠 トウ ＋ {Short(fixed2)}（規定 ／ {v.Name}）", w, new[] { UnitCatalog.Tou, fixed2 }, pool, 3, vars);
    }

    // ---------------------------------------------------------------------------------
    // 1台の推移
    // ---------------------------------------------------------------------------------
    static void DeepOne(string wave, string order, string[] vers)
    {
        var w = WaveOf(wave);
        var f0 = Seat(Order(order));
        Console.WriteLine($"# guard294 deep —— {order} × {w.Name}（seed 0..199・verbose）");
        Console.WriteLine();
        var vs = vers.Length > 0 ? new[] { (string.Join(" ", vers), vers.Aggregate(f0, (f, x) => Apply(f, AnyVer(x)))) } : Variants(f0).ToArray();
        Console.WriteLine("| 版 | 勝率 | 倒しT | 負けT | 寿命 | 止めた | 橋 | 猶予 | 肩代わり | 重圧 | 膜 | 勇者の上回り |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (n, f) in vs)
        {
            var a = MeasureDeep(f, w, Seeds);
            Console.WriteLine($"| {n} | {F1(a.Win)} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)} | {Dt(a.FirstDeathN, a.FirstDeathT, a.N)} | {Per(a.Holds, a.HN)} | {Per(a.Bridge, a.HN)} | {Per(a.GraceStops, a.HN)} | "
                + $"{Per(a.Shoulders, a.RN)} | {Per(a.PressHits, a.RN)} | {Per(a.MemHits, a.MN)} | {Pct(a.HeroOver, a.HeroTurns)} |");
        }
    }

    // ---------------------------------------------------------------------------------
    // Codex 向けメモの並び（段0 の DemoApp の追記）: 試遊プリセット × 試遊の波（`EnemyCatalog.PlaytestStages`・DemoApp と同じ倍率）で、
    // 網（`Web`）か連鎖の鞭（`ShockGaugeLabels.WhipChain`）の出来事と、その前後の並びを出す（規定の駒・固定しない）。
    // ---------------------------------------------------------------------------------
    static void Memo(string rowPart, string wave, int seed, string kind, int limit)
    {
        var (name, f0) = Presets.Playtest.First(r => r.Name.Contains(rowPart, StringComparison.Ordinal));
        var f = Pin295(f0);   // 第296期: ヒサも旧の規定に
        var w = EnemyCatalog.PlaytestStages[wave switch { "boss" => 0, "guard" => 1, "bat" => 2, _ => int.Parse(wave) }];
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BattleEngine.MaterializeEnemy(w.Enemy, w.Scale);
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        foreach (var ev in r.Events.Where(x => x.Kind is BattleEventKind.Summon or BattleEventKind.SilkBall && x.TargetId is int)) names.TryAdd(ev.TargetId!.Value, ev.Kind == BattleEventKind.SilkBall ? "糸玉" : "喚ばれたもの");
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        bool Key(BattleEvent x) => kind == "web" ? x.Kind == BattleEventKind.Web : x.Kind == BattleEventKind.ShockGauge && x.Text == ShockGaugeLabels.WhipChain;
        bool Ctx(BattleEvent x) => kind == "web"
            ? x.Kind is BattleEventKind.StatusGain && x.Text == StatusKeys.Shock || x.Kind is BattleEventKind.ShockSpent or BattleEventKind.Skill or BattleEventKind.Death
            : x.Kind == BattleEventKind.ShockGauge || x.Kind is BattleEventKind.Highlight or BattleEventKind.Attack or BattleEventKind.Damage && x.Reaction;
        Console.WriteLine($"# guard294 memo —— {name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}（{(kind == "web" ? "網 `Web`" : "連鎖の鞭 `WhipChain`")}）");
        Console.WriteLine();
        int count = r.Events.Count(Key);
        Console.WriteLine($"この戦の{(kind == "web" ? " `Web`" : " `WhipChain`")}: {count} 件（{string.Join(" ／ ", r.Events.Where(Key).GroupBy(x => x.Text).Select(g => $"{g.Key} {g.Count()}"))}）");
        Console.WriteLine();
        Console.WriteLine("```");
        var evs = r.Events;
        var show = new SortedSet<int>();
        for (int i = 0; i < evs.Count; i++)
            if (Key(evs[i]))
            {
                show.Add(i);
                for (int j = i - 1; j >= Math.Max(0, i - 4); j--) if (Ctx(evs[j]) || Key(evs[j])) show.Add(j);
                for (int j = i + 1; j < Math.Min(evs.Count, i + 4); j++) if (Ctx(evs[j]) || Key(evs[j])) show.Add(j);
            }
        int last = -1, shown = 0, lastT = -1;
        foreach (int i in show)
        {
            if (shown >= limit) { Console.WriteLine($"…（以下 {show.Count - shown} 件は省略）"); break; }
            var x = evs[i];
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            else if (last >= 0 && i != last + 1) Console.WriteLine(" …");
            string other = string.Join("  ", new[]
            {
                x.PartnerId is int pa ? $"Partner={N(pa)}" : null, x.StatusRemaining is int sr ? $"rem={sr}" : null,
                x.Reaction ? "Reaction" : null, x.Kind is BattleEventKind.Damage ? $"hp={x.HpAfter}" : null, x.Kind is BattleEventKind.Web ? $"Slot {x.Slot}" : null,
            }.Where(q => q is not null));
            string text = string.IsNullOrEmpty(x.Text) ? "" : (x.Kind == BattleEventKind.Highlight ? $"「{x.Text.Trim()}」" : x.Text);
            string amt = x.Kind is BattleEventKind.ShockGauge ? $"Amount {x.Amount}  Slot {x.Slot}" : x.Amount != 0 ? $"Amount {x.Amount}" : "";
            Console.WriteLine($"#{i,-4} {x.Kind} {text}  {N(x.ActorId)} → {N(x.TargetId)}  {amt}  {other}".TrimEnd());
            last = i; shown++;
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# guard294 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        // (a) 段0
        Expect("(a) 規定のクグ ＝ KW-a（`KuguKWa` ＝ 規定）・シガ ＝ SW-a（`ShigaSWa` ＝ 規定）・旧の規定 `KuguKGb` ／ `ShigaSIb` は `All` ／ `Retired` の外・文面は第293期の版のまま",
            ReferenceEquals(UnitCatalog.KuguKWa, UnitCatalog.Kugu) && ReferenceEquals(UnitCatalog.ShigaSWa, UnitCatalog.Shiga)
            && UnitCatalog.Kugu.Traits.SequenceEqual(UnitCatalog.KuguKGb.Traits.Append(TraitId.WebCharge)) && UnitCatalog.Shiga.Traits.SequenceEqual(UnitCatalog.ShigaSIb.Traits.Append(TraitId.ShockWhipChain))
            && !new[] { UnitCatalog.KuguKGb, UnitCatalog.ShigaSIb }.Any(UnitCatalog.Everyone.Contains) && UnitCatalog.All.Contains(UnitCatalog.Kugu) && UnitCatalog.All.Contains(UnitCatalog.Shiga)
            && UnitCatalog.Kugu.PlusText == UnitCatalog.KuguKGb.PlusText + "。組み付いている間、手番ごとに糸を1本張る。糸の掛かった敵は帯電し続ける。止められない相手には、代わりにその周りへ帯電した糸玉を張る"
            && UnitCatalog.Shiga.PlusText == UnitCatalog.ShigaSIb.PlusText + "。割り込みの鞭は、弾けた電気をまとってそのぶん重くなる"
            && UnitCatalog.KuguKWb.Traits.SequenceEqual(UnitCatalog.KuguKGb.Traits.Append(TraitId.WebSnare)) && UnitCatalog.ShigaSWb.Traits.SequenceEqual(UnitCatalog.Shiga.Traits.Append(TraitId.StoredChargeEvery)));
        // (b) 版は規定の末尾に札を足しただけ・`All` ／ `Retired` の外・文面
        var vers = new[] { UnitCatalog.HisaHSa, UnitCatalog.HisaHSc, UnitCatalog.HisaHSd, UnitCatalog.HisaHSa1, UnitCatalog.SoraSRa, UnitCatalog.SoraSRb, UnitCatalog.SomSMa, UnitCatalog.SomSMb };
        Expect("(b) 版は規定の末尾に札を足しただけ（体・型・行動は同じ）・`All` ／ `Retired` の外・規定は動いていない",
            UnitCatalog.HisaHSa.Traits.SequenceEqual(UnitCatalog.HisaHK0.Traits.Append(TraitId.BeckonHold))
            && UnitCatalog.HisaHSc.Traits.SequenceEqual(UnitCatalog.HisaHSa.Traits.Append(TraitId.BeckonBridge))
            && UnitCatalog.HisaHSd.Traits.SequenceEqual(UnitCatalog.HisaHSa.Traits.Append(TraitId.BeckonGrace))
            && UnitCatalog.HisaHSa1.Traits.SequenceEqual(UnitCatalog.HisaHSa.Traits.Append(TraitId.BeckonHoldOnce))
            && UnitCatalog.SoraSRa.Traits.SequenceEqual(UnitCatalog.SoraSR0.Traits.Append(TraitId.DeflectWide)) && UnitCatalog.SoraSRb.Traits.SequenceEqual(UnitCatalog.SoraSR0.Traits.Append(TraitId.DivertPressure))
            && UnitCatalog.SomSMa.Traits.SequenceEqual(UnitCatalog.Som.Traits.Append(TraitId.StaticMembrane)) && UnitCatalog.SomSMb.Traits.SequenceEqual(UnitCatalog.SomSMa.Traits.Append(TraitId.MembraneNoStun))
            && !vers.Any(UnitCatalog.Everyone.Contains)
            && UnitCatalog.All.Where(u => u.Traits.Contains(TraitId.DivertPressure)).SequenceEqual(new[] { UnitCatalog.Sora })   /* 第295期: SR-b の札は規定のソラが持つ */
            && vers.All(v => { var b = UnitCatalog.Everyone.First(x => x.Id == v.Id); return v.MaxHp == b.MaxHp && v.Attack == b.Attack && v.Speed == b.Speed && v.Pattern == b.Pattern && ReferenceEquals(v.Actions, b.Actions); })
            && !UnitCatalog.All.Any(u => u.Traits.Any(t => t is TraitId.BeckonHold or TraitId.BeckonBridge or TraitId.BeckonGrace or TraitId.BeckonHoldOnce or TraitId.DeflectWide or TraitId.StaticMembrane or TraitId.MembraneNoStun)));
        Expect("(c) 文面: 指示書 §3 の追記",
            UnitCatalog.HisaHSa.PlusText == UnitCatalog.HisaHK0.PlusText + "。標を付けられた味方は、倒れる一撃を受けても一度だけ踏みとどまる"
            && UnitCatalog.HisaHSc.PlusText == UnitCatalog.HisaHSa.PlusText + "。踏みとどまった味方へ、仲間の癒し手がすぐ駆けつける"
            && UnitCatalog.HisaHSd.PlusText == UnitCatalog.HisaHSa.PlusText + "。踏みとどまった味方は、次に動き終えるまで倒れない"
            && UnitCatalog.SoraSRa.PlusText == UnitCatalog.SoraSR0.PlusText + "。仲間に降りかかる広い攻撃も、半分は自分が代わりに浴びて逸らす"
            && UnitCatalog.SoraSRb.PlusText == UnitCatalog.SoraSR0.PlusText + "。指差された敵は手元が狂う。深く指差されるほど、その一撃は軽くなる"
            && UnitCatalog.SomSMa.PlusText == UnitCatalog.Som.PlusText + "。自分と隣の仲間に静電気の膜を張る。帯電した仲間は、敵の一撃を半分しか受けない"
            && UnitCatalog.SomSMb.PlusText == UnitCatalog.SomSMa.PlusText + "。膜の内側では、弾けても痺れない");

        var boss = S287.Waves[0];
        var hb = Playtest("試遊・標 ボス台");
        var hbT = FvSwap(hb, UnitCatalog.Ban, UnitCatalog.Tsugi);
        var hl = Seat(Order("ミサ・ガルド・ザン・リリ・ヒサ"));   // 踏みとどまりと橋が実際に起きる台（標経済のガン → リリ）
        // (d) 踏みとどまりは敵の攻撃だけ・HP 1・標が剥がれる（HS-a）——台本で、ヒサの標の味方が敵の攻撃で HP 0 になった件数と、刻み・放電で倒れた件数
        {
            long holds = 0, tickDeaths = 0;
            foreach (var w in S287.Waves)
                for (int s = 0; s < 40; s++)
                {
                    var p = BattleEngine.Materialize(Apply(hl, HisaVers[1]), BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                    holds += r.TallyByUnit.TryGetValue("hisa", out var t) ? t.HoldFires : 0;
                    var mine = p.Select(u => u.InstanceId).ToHashSet();
                    foreach (var ev in r.Events)
                        if (ev.Kind == BattleEventKind.Damage && ev.TargetId is int dt && mine.Contains(dt) && ev.HpAfter == 0
                            && !(ev.ActorId is int ai && !mine.Contains(ai) && !ev.FriendlyFire && !ev.Relayed)) tickDeaths++;
                }
            Expect("(d) HS-a で踏みとどまりが起きる（ボス ／ 近衛 ／ 大隊 × 40 seed）", holds > 0, $"止めた {holds} 回・敵の攻撃以外で倒れた一撃 {tickDeaths}（止めない側）");
        }
        // (e) 踏みとどまりの直後: 標が剥がれ HP 1（ログの行で確かめる——盤面は読まない）
        {
            int ok = 0, bad = 0, atOne = 0;
            foreach (var w in S287.Waves)
            for (int s = 0; s < 40; s++)
            {
                var p = BattleEngine.Materialize(Apply(hl, HisaVers[1]), BattleContext.PlayerTeam);
                var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                var log = r.Log.Select(l => l.Text).ToList();
                for (int i = 0; i < log.Count; i++)
                {
                    if (!log[i].Contains("倒れる一撃に踏みとどまった")) continue;
                    // 同じ一撃のダメージ行（次の攻撃の見出し「→」かターンの区切りより前・その駒の名前）は「(残り 1)」。無ければ HP 1 の駒を止めた一撃（量 0）
                    string name = System.Text.RegularExpressions.Regex.Match(log[i], "矢面の (.+?) は倒れる一撃").Groups[1].Value;
                    string? dmg = null;
                    for (int j = i + 1; j < log.Count && !log[j].Contains(" → ") && !log[j].StartsWith("--- "); j++)
                        if (log[j].Contains($"{name} に ") && log[j].Contains(" ダメージ (残り ")) { dmg = log[j]; break; }
                    if (dmg is null) atOne++;
                    else if (dmg.Contains("(残り 1)")) ok++; else bad++;
                }
            }
            Expect("(e) 踏みとどまった一撃は HP 1 で止まる（同じ一撃のダメージ行が「残り 1」・HP 1 の駒は量 0）", ok > 0 && bad == 0, $"{ok} 件・HP 1 の駒を止めた {atOne}・ずれ {bad}");
        }
        // (f) 刻み・放電では踏みとどまらない: 毒・燃焼・放電で HP 0 になった味方のうち、標を持っていた駒は踏みとどまりの行が出ない（標の味方に刻みで倒れた件を直接作る）
        {
            int tickKill = 0;
            // 感電の台にヒサを入れる（カタの漏れでヒサの標の味方が帯電して放電で削られる）
            var fx = Apply(Seat(Order("トウ・ヒサ・ゴルム・カタ・ツギ")), HisaVers[1]);
            foreach (var w in S287.Waves)
                for (int s = 0; s < 60; s++)
                {
                    var p = BattleEngine.Materialize(fx, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                    var mine = p.Select(u => u.InstanceId).ToHashSet();
                    foreach (var ev in r.Events)
                        if (ev.Kind == BattleEventKind.Damage && ev.TargetId is int dt && mine.Contains(dt) && ev.HpAfter == 0 && (ev.ActorId is null || mine.Contains(ev.ActorId.Value)))
                            tickKill++;
                }
            Expect("(f) 刻み・放電・同士討ちで倒れる一撃は止めない（そういう死が起きている台で、出どころが味方 ／ 無しの致死の一撃は HP 0 まで通る）", tickKill > 0, $"味方 ／ 刻みの出どころで倒れた一撃 {tickKill}");
        }
        // (g) HS-c: 橋は1ターンに1度・粛で止まる経路（`OutOfTurnRoute.Bridge`）を通る
        {
            long fired = 0;
            foreach (var w in S287.Waves)
                for (int s = 0; s < 40; s++)
                {
                    var r = BattleEngine.Run(BattleEngine.Materialize(Apply(hl, HisaVers[2]), BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                    if (r.TallyByUnit.TryGetValue("hisa", out var t)) fired += t.BridgeFired;
                }
            // 1ターン1度: 同じターンに2度架からない——台本の Highlight 行（「駆けつける」）をターンで数える
            int dup = 0;
            foreach (var w in S287.Waves)
            for (int s = 0; s < 40; s++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(Apply(hl, HisaVers[2]), BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                var byTurn = r.Events.Where(ev => ev.Kind == BattleEventKind.Highlight && (ev.Text ?? "").Contains("駆けつける")).GroupBy(ev => ev.Turn);
                dup += byTurn.Count(g => g.Count() > 1);
            }
            bool routed = OutOfTurnRoutes.Names[(int)OutOfTurnRoute.Bridge] == "橋" && OutOfTurnRoutes.Count == Enum.GetValues<OutOfTurnRoute>().Length;
            Expect("(g) HS-c の橋は架かる・1ターンに1度・経路は `OutOfTurnRoute.Bridge`（`CanActOutOfTurn` を通る＝粛・痺れ・組み付きで止まる）", fired > 0 && dup == 0 && routed, $"架かった {fired}・同じターンに2度 {dup}");
        }
        // (h) 粛で止まる: 粛の盤面ルールで HS-c を回すと橋は 0 で、粛で止まった数が立つ
        {
            long fired = 0, hushed = 0;
            var hushStage = EnemyCatalog.Stages.Select((st, i) => (st, i)).FirstOrDefault(x => x.st.Enemy.Occupied().Any(o => o.Def.Traits.Contains(TraitId.Hush)));
            if (hushStage.st is not null)
                for (int s = 0; s < 60; s++)
                {
                    var r = BattleEngine.Run(Apply(hl, HisaVers[2]), hushStage.st.Enemy, s, verbose: false);
                    if (r.TallyByUnit.TryGetValue("hisa", out var t)) { fired += t.BridgeFired; hushed += t.BridgeHushed; }
                }
            Expect("(h) 粛の波では橋が架からない（粛で止まった数が立つ）", hushStage.st is not null && fired == 0, hushStage.st is null ? "粛の波が無い" : $"第{hushStage.i + 1}波: 架かった {fired}・粛で止まった {hushed}");
        }
        // (i) HS-d: 猶予の間は何度でも HP 1・1体につき1戦1度
        {
            long granted = 0, stops = 0, twice = 0;
            foreach (var w in S287.Waves)
                for (int s = 0; s < 40; s++)
                {
                    var p = BattleEngine.Materialize(Apply(hl, HisaVers[3]), BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                    if (!r.TallyByUnit.TryGetValue("hisa", out var t)) continue;
                    granted += t.GraceGranted; stops += t.GraceStops;
                    var g = r.Log.Where(l => l.Text.Contains("次に動き終えるまで倒れない")).Select(l => l.Text).ToList();
                    twice += g.GroupBy(x => x).Count(x => x.Count() > 1);
                }
            Expect("(i) HS-d の猶予が与えられ、猶予の間に止めた一撃がある・同じ駒に2度与えない", granted > 0 && stops > 0 && twice == 0, $"与えた {granted}・猶予で止めた {stops}・2度 {twice}");
        }
        // (j) SR-a: 肩代わりは ソラ以外・ソラが倒れた後は肩代わりしない・`u != source`
        {
            long sh = 0, afterDeath = 0, onSora = 0;
            var fs = Apply(FvSwap(hb, UnitCatalog.Ban, UnitCatalog.SoraSR0), SoraVers[1]);
            foreach (var w in S287.Waves)
                for (int s = 0; s < 40; s++)
                {
                    var p = BattleEngine.Materialize(fs, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                    var so = p.First(u => u.Def.Id == "sora");
                    bool dead = false;
                    foreach (var l in r.Log)
                    {
                        if (l.Text.Contains($"{so.Name} は倒れた")) dead = true;
                        if (l.Text.Contains("への一撃を半分引き受けた"))
                        {
                            sh++;
                            if (dead) afterDeath++;
                            if (l.Text.Contains($"が {so.Name} への一撃")) onSora++;
                        }
                    }
                }
            Expect("(j) SR-a の肩代わりが起きる・ソラ自身の一撃は肩代わりしない・ソラが倒れた後は肩代わりしない", sh > 0 && onSora == 0 && afterDeath == 0, $"肩代わり {sh}・ソラ自身 {onSora}・倒れた後 {afterDeath}");
        }
        // (j2) SR-a: 範囲の一撃（ソラ自身への範囲・肩代わりの段）も逸らす——ボス（勇者1体・殴った本人には返さない）と精鋭（単体だけ）では起きないので、本編の波のソラの行で見る
        {
            long wide = 0, all = 0, shoulders = 0;
            foreach (var (n, f) in CompareBuilds().Select(r => (r.Name, F: Pin295(r.F))).Where(r => Has(r.F, UnitCatalog.SoraSR0)))
                for (int wi = 1; wi < EnemyCatalog.Stages.Count; wi++)
                    for (int sd = 0; sd < 20; sd++)
                    {
                        var r = BattleEngine.Run(Apply(f, SoraVers[1]), EnemyCatalog.Stages[wi].Enemy, sd, verbose: false);
                        if (r.TallyByUnit.TryGetValue("sora", out var t)) { wide += t.WideDeflects; all += t.DeflectHits; shoulders += t.WideShoulders; }
                    }
            Expect("(j2) SR-a は範囲の一撃と肩代わりの段も逸らす（本編の波・ソラの行 × 第2〜5波 × 20 seed）", wide > 0 && shoulders > 0, $"逸らした {all}・うち範囲 ／ 肩代わり {wide}・肩代わり {shoulders}");
        }
        // (k) SR-b: 上限 45%・ソラが倒れたら消える（重圧のログの割合とソラの生死）
        {
            int over = 0, afterDeath = 0, n = 0;
            var fs = Apply(Playtest("試遊・標 道中"), SoraVers[2]);
            foreach (var w in S287.Waves)
                for (int s = 0; s < 40; s++)
                {
                    var p = BattleEngine.Materialize(fs, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                    var so = p.First(u => u.Def.Id == "sora");
                    bool dead = false;
                    foreach (var l in r.Log)
                    {
                        if (l.Text.Contains($"{so.Name} は倒れた")) dead = true;
                        var m = System.Text.RegularExpressions.Regex.Match(l.Text, @"(?:手元が狂った|の一撃を見切った)（-(\d+)%");
                        if (!m.Success) continue;
                        n++;
                        if (int.Parse(m.Groups[1].Value) > DivertPressureTrait.MaxPercent) over++;
                        if (dead) afterDeath++;
                    }
                }
            Expect("(k) SR-b の重圧が掛かる・上限 45%・ソラが倒れた後は掛からない", n > 0 && over == 0 && afterDeath == 0, $"{n} 回・上限超え {over}・倒れた後 {afterDeath}");
        }
        // (l) SM: 膜は敵の攻撃だけを半分にする・放電・刻み・同士討ちは半分にしない ／ SM-b でも放電は流れる・痺れない
        {
            long memHits = 0, memLines = 0, disA = 0, disB = 0, stunA = 0, stunB = 0, skipB = 0;
            var fm = Row("感電 (シガ×カタ×ソム)");
            foreach (var (v, isB) in new[] { (SomVers[1], false), (SomVers[2], true) })
                foreach (var w in S287.Waves)
                    for (int s = 0; s < 40; s++)
                    {
                        var p = BattleEngine.Materialize(Apply(fm, v), BattleContext.PlayerTeam);
                        var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                        var mine = p.Select(u => u.InstanceId).ToHashSet();
                        if (r.TallyByUnit.TryGetValue("som", out var t)) memHits += t.MembraneHits;
                        // 膜の行の直後のダメージ行の出どころが味方なら誤り（ログの順: 半分の行 → ダメージの行）
                        var evs = r.Events;
                        long dis = evs.Count(ev => ev.Kind == BattleEventKind.Damage && ev.TargetId is int tt && mine.Contains(tt) && ev.ActorId is int aa && mine.Contains(aa) && ev.FriendlyFire);
                        long stun = evs.Count(ev => ev.Kind == BattleEventKind.StatusGain && ev.Text == StatusKeys.Stun && ev.TargetId is int st && mine.Contains(st));
                        long skip = p.Select(u => u.Def.Id).Distinct().Sum(id => r.TallyByUnit.TryGetValue(id, out var ut) ? ut.MembraneStunSkipped : 0);
                        if (isB) { disB += dis; stunB += stun; skipB += skip; } else { disA += dis; stunA += stun; }
                        foreach (var l in r.Log) if (l.Text.Contains("の膜が一撃を半分に抑えた")) memLines++;
                    }
            Expect("(l) SM の膜が働く（敵の攻撃だけ・判定は engine の条件）・SM-b でも味方への放電は流れ、味方は感電で痺れない（SM-a では痺れる）",
                memHits > 0 && memLines == memHits && disB > 0 && stunA > 0 && skipB > 0,
                $"半分 {memHits}・味方への同士討ち SM-a {disA} ／ SM-b {disB}・味方の痺れ SM-a {stunA} ／ SM-b {stunB}（痺れなかった {skipB}）");
        }
        // (m) 膜は放電・刻み・同士討ちを半分にしない: 帯電した味方への「出どころが味方」の一撃で膜の行が出ない（ログの並びで直前の行を見る）
        {
            int bad = 0, n = 0;
            var fm = Apply(FvSwap(Playtest("試遊・感電 雷の型"), UnitCatalog.DohaD0, UnitCatalog.Som), SomVers[1]);
            foreach (var w in S287.Waves)
                for (int s = 0; s < 40; s++)
                {
                    var p = BattleEngine.Materialize(fm, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                    var names = p.Select(u => u.Name).ToHashSet();
                    var log = r.Log.Select(l => l.Text).ToList();
                    for (int i = 0; i + 1 < log.Count; i++)
                    {
                        if (!log[i].Contains("の膜が一撃を半分に抑えた")) continue;
                        n++;
                        // 膜の行の直前には、敵の攻撃（「〜の攻撃」「全体」など）の見出しか別の段の行がある。直後の味方への行が「放電」の行なら誤り。
                        if (i > 0 && log[i - 1].Contains("放電")) bad++;
                    }
                }
            Expect("(m) 膜は放電の一撃を半分にしない（膜の行の直前が放電の行になっていない）", n > 0 && bad == 0, $"{n} 件・ずれ {bad}");
        }
        // (n) 決定性・verbose の有無で勝敗と決着T が変わらない（各版）
        {
            int nd = 0;
            var cases = new (Formation F, S287.Wave W)[]
            {
                (Apply(hl, HisaVers[2]), S287.Waves[2]), (Apply(hl, HisaVers[3]), S287.Waves[1]), (Apply(hbT, HisaVers[2]), boss),
                (Apply(FvSwap(hb, UnitCatalog.Ban, UnitCatalog.SoraSR0), SoraVers[1]), boss), (Apply(Playtest("試遊・標 道中"), SoraVers[2]), S287.Waves[2]),
                (Apply(Row("感電 (シガ×カタ×ソム)"), SomVers[2]), S287.Waves[1]),
            };
            foreach (var (fv, w) in cases)
                for (int s = 0; s < 20; s++)
                {
                    var r1 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                    var r2 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                    var r3 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                    if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns || r1.Events.Count != r3.Events.Count) nd++;
                }
            Expect("(n) 決定的・verbose の有無で勝敗と決着T が変わらない（HS-c ／ HS-d ／ SR-a ／ SR-b ／ SM-b）", nd == 0, $"{nd} 件");
        }
        // (o) 乱数: 新しい口に PickOne ／ Roll が無い——BattleCore の「PickOne(」「Roll(」の出現数が HEAD（`git show HEAD:`）と同じ（自己検査の必須4項目の 4）
        {
            static int Count(string text, string pat) { int n = 0, k = 0; while ((k = text.IndexOf(pat, k, StringComparison.Ordinal)) >= 0) { n++; k += pat.Length; } return n; }
            static string? Head(string path)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", $"show HEAD:{path}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
                    using var pr = System.Diagnostics.Process.Start(psi)!;
                    string o = pr.StandardOutput.ReadToEnd();
                    pr.WaitForExit();
                    return pr.ExitCode == 0 ? o : null;
                }
                catch { return null; }
            }
            var notes = new List<string>();
            bool clean = true;
            foreach (var path in new[] { "BattleCore/BattleEngine.cs", "BattleCore/Traits.cs" })
            {
                string now = File.ReadAllText(path);
                string? head = Head(path);
                if (head is null) { clean = false; notes.Add($"{path}: HEAD を読めない"); continue; }
                foreach (var pat in new[] { "PickOne(", "Roll(" })
                {
                    int a0 = Count(head, pat), a1 = Count(now, pat);
                    if (a1 > a0) clean = false;
                    notes.Add($"{Path.GetFileName(path)} {pat} {a0}→{a1}");
                }
            }
            Expect("(o) `PickOne(` ／ `Roll(` を新たに使っていない（BattleCore の出現数が HEAD 以下）", clean, string.Join("・", notes));
        }
        // (p) 規定は動かない: 規定の駒だけの台で、版の札の保持者がいないので第294期の計数は 0
        {
            long any = 0;
            foreach (var (n, f) in CompareBuilds().Select(r => (r.Name, F: Pin295(r.F))).Take(64))
                for (int s = 0; s < 2; s++)
                {
                    var r = BattleEngine.Run(f, EnemyCatalog.Stages[1].Enemy, s, verbose: false);
                    foreach (var t in r.TallyByUnit.Values) any += t.HoldFires + t.WideShoulders + t.PressureHits + t.MembraneHits + t.MembraneSpread + t.BridgeFired + t.GraceStops;
                }
            Expect("(p) 規定の行（`compare` 64 行・第2波・seed 0..1）では第294期の口が1度も働かない", any == 0, $"{any}");
        }
        // (q) HS-a′（参考）: 同じ味方は1戦に1度だけ踏みとどまる
        {
            int twice = 0; long holds = 0, spent = 0;
            foreach (var w in S287.Waves)
                for (int sd = 0; sd < 40; sd++)
                {
                    var r = BattleEngine.Run(BattleEngine.Materialize(Apply(hl, HisaVers[4]), BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                    var names = r.Log.Select(l => System.Text.RegularExpressions.Regex.Match(l.Text, "矢面の (.+?) は倒れる一撃")).Where(m => m.Success).Select(m => m.Groups[1].Value).ToList();
                    holds += names.Count; twice += names.GroupBy(x => x).Count(g => g.Count() > 1);
                    if (r.TallyByUnit.TryGetValue("hisa", out var t)) spent += t.HoldOnceSpent;
                }
            Expect("(q) HS-a′（参考）は同じ味方を1戦に1度しか踏みとどまらせない", holds > 0 && twice == 0, $"止めた {holds}・2度目を断った {spent}・同じ駒に2度 {twice}");
        }
        // (r) 踏みとどまったらヒサの記憶も消える: ソラの「自分に付ける」標で踏みとどまりが張り直されない（標 ボス台 バン → ソラ × ボスで 30 ターンの上限まで続く戦が無い）
        {
            var fs = Apply(FvSwap(hb, UnitCatalog.Ban, UnitCatalog.SoraSR0), HisaVers[1]);
            int caps2 = 0;
            for (int sd = 0; sd < 40; sd++) { var r = BattleEngine.Run(BattleEngine.Materialize(fs, BattleContext.PlayerTeam), boss.Make(), sd, verbose: false); if (!r.PlayerWon && r.Turns >= BattleEngine.MaxTurns) caps2++; }
            Expect("(r) ソラの自分への標が踏みとどまりを張り直さない（標 ボス台 バン → ソラ × HS-a × ボスで 30T 上限の負け 0）", caps2 == 0, $"{caps2} 戦（40 戦）");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
