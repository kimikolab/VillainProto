using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S289 = Shock289Diag;
using BA = BurnAuditDiag;

// =====================================================================================
// shock290 —— 第290期「シガ SI-b ／ カタ KR-b の規定化 ＋ シガの直し SI-c ＋ シガ×カタの同居 ＋ クグの糸 KG-a ／ KG-b」。
// 指示書は design/PHASE290_SHOCK_SQUAD_SPEC.md ／ 報告は design/PHASE290_SHOCK_SQUAD.md。
//
//     dotnet run --project BattleSim -c Release 0 shock290 p0          # Phase 0（§5-4）: 規定のクグの帯電と組み付きの重なり・弾けの経路・隣の放電・ボスの組み付き・第287期の格子のクグの台
//     dotnet run --project BattleSim -c Release 0 shock290 boards      # 代表台 × 版（シガ SG-a ／ SI-b ／ SI-c・クグ 旧 ／ KG-a ／ KG-b・seed 0..199・verbose）と対照
//     dotnet run --project BattleSim -c Release 0 shock290 duo         # 段2: シガの版（SG-a ／ SI-b ／ SI-c）× カタの版（旧 ／ KR-b）の6通り × 同居の台
//     dotnet run --project BattleSim -c Release 0 shock290 grid3 <guard|bat> [シガの版] [カタの版]   # 段2 の小さな格子（固定枠 トウ ＋ シガ ＋ カタ・探索枠2）
//     dotnet run --project BattleSim -c Release 0 shock290 gridk <guard|bat> [クグの版]              # 段3 の格子（固定枠 トウ ＋ クグ・探索枠3）
//     dotnet run --project BattleSim -c Release 0 shock290 grids <guard|bat> [シガの版]              # シガの精鋭の格子（第289期の `shock289 grid … shiga` と同じ台）
//     dotnet run --project BattleSim -c Release 0 shock290 compare     # `compare` 64 行 × 版（規定 ／ SI-c ／ KG-a ／ KG-b）
//     dotnet run --project BattleSim -c Release 0 shock290 check       # 自己検査
//     dotnet run --project BattleSim -c Release 0 shock290 log <boss|guard|bat> <席の並び（短い名前を ・ で5つ）> [seed] [版…]
//
// 版の名前（ASCII の別名）: シガ SG-a(sga) ／ SI-b(sib・規定) ／ SI-c(sic)、カタ 旧(old) ／ KR-b(krb・規定)、クグ 旧(kold・規定) ／ KG-a(kga) ／ KG-b(kgb)。
// **この器具の台は規定の駒（第290期の規定）で組み、版はその駒を差し替える。**
// =====================================================================================
static class Shock290Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": BoardsAll(); return;
            case "duo": Duo(); return;
            case "grid3": Grid3(A(3, "guard"), A(4, "sib"), A(5, "krb")); return;
            case "gridk": GridK(A(3, "guard"), A(4, "kold")); return;
            case "grids": GridS(A(3, "guard"), A(4, "sic")); return;
            case "compare": CompareAll(); return;
            case "check": Check(); return;
            case "log": LogOne(A(3, "guard"), A(4, ""), args.Length > 5 ? int.Parse(args[5]) : 0, args.Skip(6).ToArray()); return;
            default: Console.WriteLine("shock290: モードは p0 / boards / duo / grid3 / gridk / compare / check / log。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・波・台（測る前に固定）
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef From, UnitDef To);
    internal static readonly Ver[] ShigaVers =
    {
        new("SG-a", "sga", UnitCatalog.ShigaSIb, UnitCatalog.ShigaSGa), new("SI-b", "sib", UnitCatalog.ShigaSIb, UnitCatalog.ShigaSIb), new("SI-c", "sic", UnitCatalog.ShigaSIb, UnitCatalog.ShigaSIc),
    };
    internal static readonly Ver[] KataVers =
    {
        new("旧", "old", UnitCatalog.KataKRb, UnitCatalog.KataS3), new("KR-b", "krb", UnitCatalog.KataKRb, UnitCatalog.KataKRb),
    };
    internal static readonly Ver[] KuguVers =
    {
        new("旧", "kold", UnitCatalog.KuguKG0, UnitCatalog.KuguKG0), new("KG-a", "kga", UnitCatalog.KuguKG0, UnitCatalog.KuguKGa), new("KG-b", "kgb", UnitCatalog.KuguKG0, UnitCatalog.KuguKGb),
    };
    static Ver VerOf(string n, Ver[] set) => set.First(v => v.Name == n || v.Ascii == n);
    static Ver AnyVer(string n) => ShigaVers.Concat(KataVers).Concat(KuguVers).First(v => v.Ascii == n || v.Name == n);
    static Formation Apply(Formation f, Ver v) => ReferenceEquals(v.From, v.To) ? f : FvSwap(f, v.From, v.To);
    static Formation Apply(Formation f, params Ver[] vs) { foreach (var v in vs) f = Apply(f, v); return f; }
    static bool Has(Formation f, UnitDef d) => f.Occupied().Any(o => ReferenceEquals(o.Def, d));

    static readonly S287.Wave[] Main = Enumerable.Range(1, EnemyCatalog.Stages.Count - 1)
        .Select(wi => new S287.Wave($"第{wi + 1}波", () => BattleEngine.Materialize(EnemyCatalog.Stages[wi].Enemy, BattleContext.EnemyTeam, BossRule.Default.Scale), false)).ToArray();
    static S287.Wave WaveOf(string n) => S287.Waves.Concat(Main).First(w => w.Name == (n switch { "boss" => "ボス", "guard" => "近衛", "bat" => "大隊", _ => n }));
    const int Seeds = 200, CutSeeds = 20;

    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    /// <summary>第291期: 名前で引くクグ（規定は第291期から KG-b）を第290期の規定 `KuguKG0` に固定する（第290期の台を再現するため）。</summary>
    static UnitDef ByShort(string n) => UnitCatalog.All.First(d => Short(d) == n) is var d0 && ReferenceEquals(d0, UnitCatalog.Kugu) ? UnitCatalog.KuguKG0 : ReferenceEquals(d0, UnitCatalog.Kata) ? UnitCatalog.KataKRb : ReferenceEquals(d0, UnitCatalog.ShigaSWa) ? UnitCatalog.ShigaSIb : ReferenceEquals(d0, UnitCatalog.Sora) ? UnitCatalog.SoraSR0 : ReferenceEquals(d0, UnitCatalog.Doha) ? UnitCatalog.DohaD0 : d0;   // 第298期: ドハも   // 第293期: カタも第290期の規定（KR-b）へ・第294期: シガも（SI-b）
    /// <summary>第291期: `compare` の行のクグを第290期の規定 `KuguKG0` に固定した行。</summary>
    static (string Name, Formation F)[] Rows290() => CompareBuilds().Select(r => (r.Name, FvSwap(FvSwap(FvSwap(FvSwap(FvSwap(r.F, UnitCatalog.Kugu, UnitCatalog.KuguKG0), UnitCatalog.Kata, UnitCatalog.KataKRb), UnitCatalog.ShigaSWa, UnitCatalog.ShigaSIb), UnitCatalog.Sora, UnitCatalog.SoraSR0), UnitCatalog.Doha, UnitCatalog.DohaD0))).ToArray();   // 第298期: ドハも   // 第293期: カタも KR-b へ
    static UnitDef[] Order(string s) => s.Split('・', StringSplitOptions.RemoveEmptyEntries).Select(ByShort).ToArray();
    static string OrderName(UnitDef[] o) => string.Join("・", o.Select(Short));
    static Formation Seat(UnitDef[] o) => FvSwap(FvSwap(B283.Seat(o), UnitCatalog.Sora, UnitCatalog.SoraSR0), UnitCatalog.Doha, UnitCatalog.DohaD0);   // 第298期: ドハも   // 第295期: ソラを旧の規定（SR0）に固定（第287期の候補 `S287.Pool` は規定のソラを持つ）
    /// <summary>第289期の台（`shock289` は第289期の規定 SG-a ／ S3 に固定してある）を第290期の規定の駒に戻す。</summary>
    static Formation Unpin(Formation f) => FvSwap(FvSwap(f, UnitCatalog.ShigaSGa, UnitCatalog.ShigaSIb), UnitCatalog.KataS3, UnitCatalog.KataKRb);

    /// <summary>
    /// 代表台（指示書 §6-2）: 第289期の代表台 17 台 ＋ クグをトウの隣に置いた台（近衛 ／ 大隊 各1台）＋ クグ入りの届いた台（第287期・近衛の上位から2台）。
    /// クグ隣の作り方（測る前に固定・第288期の「カタ隣」と同じ作法）: 第287期の各波の上位1位（シガ・ガルド・ベニ・トウ・ソラ）で、探索枠3 のうち第287期 R5 の到達寄与が最も低い1枚をクグに替え、
    /// その席がトウ（後1）の隣でなければ、トウの隣の席にいる探索枠の駒（中央のベニ）と席を入れ替える。近衛: ソラ → クグ ＝ シガ・ガルド・クグ・トウ・ベニ ／ 大隊: ガルド → クグ ＝ シガ・ベニ・クグ・トウ・ソラ。
    /// クグ入りの届いた台は `shock290 p0` の §5（第287期の格子のうちクグを含む台を回し直した部分集合）の上位から、代表台にまだ無い2台を取った（決めたこと）。
    /// </summary>
    internal static (string Name, string Wave, Func<Formation> Make)[] Boards()
    {
        var l = new List<(string, string, Func<Formation>)>();
        foreach (var b in S289.Boards()) { var mk = b.Make; l.Add((b.Name, b.Wave, () => Unpin(mk()))); }
        l.Add(("クグ隣 近衛", "近衛", () => Seat(Order("シガ・ガルド・クグ・トウ・ベニ"))));
        l.Add(("クグ隣 大隊", "大隊", () => Seat(Order("シガ・ベニ・クグ・トウ・ソラ"))));
        foreach (var (name, order) in KuguReached) l.Add((name, "近衛", () => Seat(Order(order))));
        return l.ToArray();
    }
    /// <summary>`shock290 p0` §5 の上位から引いた2台（p0 を回した後に書き写した。p0 の表と一致することを `check` (m) で確かめる）。</summary>
    internal static readonly (string Name, string Order)[] KuguReached =
    {
        ("クグ届 近衛2", "トウ・ガルド・ベニ・シガ・クグ"),   // p0 §5 近衛 2位（1位 ＝ ガルド・トウ・ベニ・クグ・シガ は代表台の R4 近衛3 と同じ台なので飛ばした）
        ("クグ届 近衛3", "シガ・ガルド・ベニ・トウ・クグ"),   // p0 §5 近衛 3位
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Deep
    {
        public long N, Wins, WinT, FoeDis, AllyDis, FoeChains, FoeChainUnits;
        // シガ
        public long ShN, Swings, SwFires, Cower, Kept, ShDmg, SwWhip, ShDied, ShDeathT, ChargeEnd, SwHushed;
        // カタ
        public long KN, Casts, Hits, KNom, KDmg, CloudSum, CloudMax, KDied, KDeathT;
        public long[] CloudByCast = new long[8], CastsAt = new long[8];
        // クグ
        public long GN, GTurns, GShock, GHeld, GBoth, GHeldGrappled, PopHit, PopHitFoe, PopHitHeld, PopChain, PopChainHeld, DisIn, DisInHeld,
                    Grapples, Stalled, Breaks, ThSelf, ThRelay, ThDealt, ThKills, ThPopped, ThCross, ThCharged, ThMarkRoots, ThMarkUnits, GDied, GDeathT;
        public long[] BySrc = new long[5];
        // トウ
        public long TDied, TDeathT;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Merge(Deep o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; FoeDis += o.FoeDis; AllyDis += o.AllyDis; FoeChains += o.FoeChains; FoeChainUnits += o.FoeChainUnits;
            ShN += o.ShN; Swings += o.Swings; SwFires += o.SwFires; Cower += o.Cower; Kept += o.Kept; ShDmg += o.ShDmg; SwWhip += o.SwWhip; ShDied += o.ShDied; ShDeathT += o.ShDeathT;
            ChargeEnd += o.ChargeEnd; SwHushed += o.SwHushed;
            KN += o.KN; Casts += o.Casts; Hits += o.Hits; KNom += o.KNom; KDmg += o.KDmg; CloudSum += o.CloudSum; if (o.CloudMax > CloudMax) CloudMax = o.CloudMax; KDied += o.KDied; KDeathT += o.KDeathT;
            for (int i = 0; i < 8; i++) { CloudByCast[i] += o.CloudByCast[i]; CastsAt[i] += o.CastsAt[i]; }
            GN += o.GN; GTurns += o.GTurns; GShock += o.GShock; GHeld += o.GHeld; GBoth += o.GBoth; GHeldGrappled += o.GHeldGrappled; PopHit += o.PopHit; PopHitFoe += o.PopHitFoe;
            PopHitHeld += o.PopHitHeld; PopChain += o.PopChain; PopChainHeld += o.PopChainHeld; DisIn += o.DisIn; DisInHeld += o.DisInHeld; Grapples += o.Grapples; Stalled += o.Stalled;
            Breaks += o.Breaks; ThSelf += o.ThSelf; ThRelay += o.ThRelay; ThDealt += o.ThDealt; ThKills += o.ThKills; ThPopped += o.ThPopped; ThCross += o.ThCross; ThCharged += o.ThCharged;
            ThMarkRoots += o.ThMarkRoots; ThMarkUnits += o.ThMarkUnits; GDied += o.GDied; GDeathT += o.GDeathT;
            for (int i = 0; i < 5; i++) BySrc[i] += o.BySrc[i];
            TDied += o.TDied; TDeathT += o.TDeathT;
        }
    }

    internal static Deep FightDeep(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var d = new Deep { N = 1 };
        if (r.PlayerWon) { d.Wins = 1; d.WinT = r.Turns; }
        var mine = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (mine.Contains(id)) d.AllyDis += t.DischargeTaken;
            else { d.FoeDis += t.DischargeTaken; d.FoeChains += t.ChainRoots; d.FoeChainUnits += t.ChainUnits; }
        }
        UnitState? sh = p.FirstOrDefault(u => u.Def.Id == "shiga"), ka = p.FirstOrDefault(u => u.Def.Id == "kata"), kg = p.FirstOrDefault(u => u.Def.Id == "kugu"), to = p.FirstOrDefault(u => u.Def.Id == "tou");
        if (sh is not null && r.TallyByUnit.TryGetValue("shiga", out var st))
        {
            d.ShN = 1; d.Swings = st.WhipSwings; d.SwFires = st.SwFires; d.Cower = st.WhipCowered; d.Kept = st.SwKeptShock; d.ShDmg = st.DamageToEnemy; d.SwWhip = st.SwWhipDealt;
            d.ChargeEnd = sh.RawCounter(StoredChargeTrait.Key); d.SwHushed = st.SwHushed;
        }
        if (ka is not null && r.TallyByUnit.TryGetValue("kata", out var kt))
        {
            d.KN = 1; d.Casts = kt.ThunderCasts; d.Hits = kt.ThunderHits; d.KNom = kt.ThunderNominal; d.KDmg = kt.DamageToEnemy; d.CloudSum = kt.CloudAtCastSum; d.CloudMax = kt.CloudAtCastMax;
            if (kt.CloudByCast is not null) for (int i = 0; i < 8; i++) d.CloudByCast[i] = kt.CloudByCast[i];
            for (int i = 0; i < 8; i++) d.CastsAt[i] = i < 7 ? (kt.ThunderCasts > i ? 1 : 0) : Math.Max(0, kt.ThunderCasts - 7);
        }
        if (kg is not null && r.TallyByUnit.TryGetValue("kugu", out var gt))
        {
            d.GN = 1; d.GTurns = gt.KuguTurns; d.GShock = gt.KuguShockTurns; d.GHeld = gt.KuguHeldTurns; d.GBoth = gt.KuguBothTurns; d.GHeldGrappled = gt.KuguHeldGrappled;
            d.PopHit = gt.KuguPopHit; d.PopHitFoe = gt.KuguPopHitFoe; d.PopHitHeld = gt.KuguPopHitHeld; d.PopChain = gt.KuguPopChain; d.PopChainHeld = gt.KuguPopChainHeld;
            d.DisIn = gt.KuguDisIn; d.DisInHeld = gt.KuguDisInHeld; d.Grapples = gt.GrappleFires; d.Stalled = gt.GrappleStalled; d.Breaks = gt.GrappleBreaks;
            d.ThSelf = gt.ThreadSelf; d.ThRelay = gt.ThreadRelay; d.ThDealt = gt.ThreadDealt; d.ThKills = gt.ThreadKills; d.ThPopped = gt.ThreadPopped; d.ThCross = gt.ThreadCrossPops;
            d.ThCharged = gt.ThreadCharged; d.ThMarkRoots = gt.ThreadMarkRoots; d.ThMarkUnits = gt.ThreadMarkChainUnits;
            if (gt.KuguShockBySrc is not null) for (int i = 0; i < 5; i++) d.BySrc[i] = gt.KuguShockBySrc[i];
        }
        foreach (var ev in r.Events.Where(ev => ev.Kind == BattleEventKind.Death))
        {
            if (sh is not null && ev.TargetId == sh.InstanceId && d.ShDied == 0) { d.ShDied = 1; d.ShDeathT = ev.Turn; }
            if (ka is not null && ev.TargetId == ka.InstanceId && d.KDied == 0) { d.KDied = 1; d.KDeathT = ev.Turn; }
            if (kg is not null && ev.TargetId == kg.InstanceId && d.GDied == 0) { d.GDied = 1; d.GDeathT = ev.Turn; }
            if (to is not null && ev.TargetId == to.InstanceId && d.TDied == 0) { d.TDied = 1; d.TDeathT = ev.Turn; }
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
    static string Pct(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1") + "%";
    static string Ctl(Formation f, UnitDef d, S287.Wave w) => Has(f, d) ? F1(100.0 * WinsPar(FvSwap(f, d, UnitCatalog.Dolga), w, Seeds) / Seeds) : "—";

    // ---------------------------------------------------------------------------------
    // Phase 0（§5-4）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第290期 Phase 0 —— 規定のクグ（組み付き）・規定のシガ（SI-b）・規定のカタ（KR-b）（seed 0..199・verbose・計数だけ）");
        Console.WriteLine();
        var bs = new List<(string Name, string Wave, Formation F)>();
        foreach (var b in Boards().Where(b => Has(b.Make(), UnitCatalog.KuguKG0))) bs.Add((b.Name, b.Wave, b.Make()));
        // §5-4 の 4（ボス）: クグの台をそのままボスへ
        foreach (var b in Boards().Where(b => b.Name is "クグ隣 近衛" or "R4 近衛3")) bs.Add((b.Name + " → ボス", "ボス", b.Make()));
        var res = bs.Select(b => MeasureDeep(b.F, WaveOf(b.Wave), Seeds)).ToArray();

        Console.WriteLine("## §5-4 の 1 帯電と組み付きの重なり（手番の頭で数える）");
        Console.WriteLine();
        Console.WriteLine("生 ＝ クグが生きていた手番 ／ 帯 ＝ 帯電していた割合 ／ 組 ＝ 組み付いていた割合（組んだ相手が生きている）／ **両 ＝ 両方そろっていた割合**（糸が働きうる）／ 帯電の書き手（1戦・トウ ／ カタ ／ ソム ／ ほかの味方 ／ 敵・自分）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 倒しT | 生（1戦） | 帯 | 組 | **両** | 帯電の書き手（トウ ／ カタ ／ ソム ／ ほか ／ 敵） | 組み付いた（1戦） | 止めた敵の手番 | ほどけた |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|---|--:|--:|--:|");
        for (int i = 0; i < bs.Count; i++)
        {
            var a = res[i];
            Console.WriteLine($"| {bs[i].Name} | {bs[i].Wave} | {F1(a.Win)} | {Per(a.WinT, a.Wins)} | {Per(a.GTurns, a.N)} | {Pct(a.GShock, a.GTurns)} | {Pct(a.GHeld, a.GTurns)} | **{Pct(a.GBoth, a.GTurns)}** | "
                + $"{string.Join(" ／ ", a.BySrc.Select(x => Per(x, a.N)))} | {Per(a.Grapples, a.N)} | {Per(a.Stalled, a.N)} | {Per(a.Breaks, a.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## §5-4 の 2・3 クグの感電が弾ける経路と、隣の味方の放電（1戦）");
        Console.WriteLine();
        Console.WriteLine("一撃 ＝ 一撃で弾けた（うち 敵に殴られて ／ そのとき組み付いていた ＝ ① が働く）／ 連鎖 ＝ 隣の放電の連鎖で弾けた（うち組み付いていた）／ 隣の放電 ＝ 隣の味方の放電がクグに来た（うち組み付いていた ＝ ② が働く）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 一撃（敵 ／ 組） | 連鎖（組） | 隣の放電（組） | 放電 敵 ／ 味方 | 敵の連鎖（1戦 ／ 1回の大きさ） | クグが倒れたT（割合） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < bs.Count; i++)
        {
            var a = res[i];
            Console.WriteLine($"| {bs[i].Name} | {bs[i].Wave} | {Per(a.PopHit, a.N)}（{Per(a.PopHitFoe, a.N)} ／ {Per(a.PopHitHeld, a.N)}） | {Per(a.PopChain, a.N)}（{Per(a.PopChainHeld, a.N)}） | {Per(a.DisIn, a.N)}（{Per(a.DisInHeld, a.N)}） | "
                + $"{Per1(a.FoeDis, a.N)} ／ {Per1(a.AllyDis, a.N)} | {Per(a.FoeChains, a.N)} ／ {Per(a.FoeChainUnits, a.FoeChains)} | {Per1(a.GDeathT, a.GDied)}（{F1(100.0 * a.GDied / a.N)}%） |");
        }
        Console.WriteLine();
        Console.WriteLine("## §5-4 の 4 ボスでの組み付き");
        Console.WriteLine();
        Console.WriteLine("| 台 | 組み付いた（1戦） | 組んでいた手番の割合（`grappleTarget` が生きている勇者を指す） | うち勇者に `Grappled` が立っていた | 止めた勇者の手番 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        for (int i = 0; i < bs.Count; i++)
        {
            if (bs[i].Wave != "ボス") continue;
            var a = res[i];
            Console.WriteLine($"| {bs[i].Name} | {Per(a.Grapples, a.N)} | {Pct(a.GHeld, a.GTurns)} | {Pct(a.GHeldGrappled, a.GHeld)} | {Per(a.Stalled, a.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## §5-4 の 5 第287期の格子のうち、クグを含む台（固定枠 トウ ＋ シガ G3K・探索枠3 にクグ・席 120）を回し直した部分集合");
        Console.WriteLine();
        foreach (var w in new[] { S287.Waves[1], S287.Waves[2] })
        {
            var (all, reached) = KuguSubset287(w);
            int adj = reached.Count(r => KuguTouAdjacent(r.Order));
            int adjAll = all.Count(o => KuguTouAdjacent(o));
            Console.WriteLine($"### {w.Name}");
            Console.WriteLine();
            Console.WriteLine($"- 台 {all.Count:N0}（足切り前）・**届いた {reached.Count:N0} 台**（第287期の報告 R5 のクグの行と比べる）");
            Console.WriteLine($"- 届いた台のうち **クグとトウが隣** {adj:N0}（{Pct(adj, reached.Count)}）・全体の台では {adjAll:N0}（{Pct(adjAll, all.Count)}）");
            var top = reached.OrderByDescending(r => r.Wins).ThenBy(r => r.Wins == 0 ? double.MaxValue : (double)r.WinT / r.Wins).ThenBy(r => OrderName(r.Order), StringComparer.Ordinal).ToList();
            Console.WriteLine();
            Console.WriteLine("| # | 席（前1・前3・中央・後1・後3） | 勝率 | 倒しT | ドルガ対照 | クグとトウが隣 |");
            Console.WriteLine("|--:|---|--:|--:|--:|---|");
            for (int i = 0; i < Math.Min(10, top.Count); i++)
            {
                var r = top[i];
                Console.WriteLine($"| {i + 1} | {OrderName(r.Order)} | {F1(100.0 * r.Wins / Seeds)} | {Per(r.WinT, r.Wins)} | {F1(100.0 * r.Dolga / Seeds)} | {(KuguTouAdjacent(r.Order) ? "○" : "")} |");
            }
            Console.WriteLine();
        }
        Console.WriteLine();
        foreach (var b in bs) Console.WriteLine($"- {b.Name}: {BA.SeatsNamed(b.F)}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static bool KuguTouAdjacent(UnitDef[] o)
    {
        int k = Array.FindIndex(o, d => d.Id == "kugu"), t = Array.FindIndex(o, d => d.Id == "tou");
        return k >= 0 && t >= 0 && FormationRules.AreAdjacent(k, t);
    }

    sealed record SubRes(UnitDef[] Order, int Wins, long WinT, int Dolga);

    /// <summary>第287期の格子（`shock287 grid`）のうち、探索枠にクグを含む台だけを同じ切り方で回し直す（固定枠は第287期と同じ トウ ＋ シガ G3K）。</summary>
    static (List<UnitDef[]> All, List<SubRes> Reached) KuguSubset287(S287.Wave w)
    {
        var lu = S287.Lineups().Where(t => t.Contains(UnitCatalog.KuguKG0)).ToList();
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(S287.Fixed.Concat(t).ToArray()));
        var res = new SubRes?[boards.Count];
        Parallel.For(0, boards.Count, i =>
        {
            var f = Seat(boards[i]);
            if (Wins(f, w, CutSeeds, out _) < 5) return;
            int wins = Wins(f, w, Seeds, out long wt);
            if (wins * 2 < Seeds) return;
            int dg = Wins(FvSwap(f, UnitCatalog.Tou, UnitCatalog.Dolga), w, Seeds, out _);
            if (dg * 2 <= wins) res[i] = new SubRes(boards[i], wins, wt, dg);
        });
        return (boards, res.Where(r => r is not null).Select(r => r!).ToList());
    }

    // ---------------------------------------------------------------------------------
    // 代表台 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var bs = Boards();
        Console.WriteLine("# 第290期 代表台 × 版（seed 0..199・verbose・台は第290期の規定の駒で組み、その駒だけを版に差し替える）");
        Console.WriteLine();
        Console.WriteLine("## 表1 シガの版（SG-a ＝ 第289期の規定 ／ SI-b ＝ 規定 ／ SI-c）・シガ在席の台");
        Console.WriteLine();
        Console.WriteLine("振 ＝ 鞭の振り（手番 ／ 割り込み）／ 怖 ＝ 怖気（1戦）／ 残 ＝ 割り込みの鞭で感電を残した（SI-c）／ 与 ＝ シガの与ダメ（手番 ／ 割り込み）／ 蓄末 ＝ 戦の終わりの蓄電 ／ 対照 ＝ その駒 → ドルガ");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率 | 倒しT | 振（手番 ／ 割） | 怖 | 残 | 与（手番 ／ 割） | 蓄末 | 放電 敵 ／ 味方 | 敵の連鎖 | シガが倒れたT（割合） | シガ → ドルガ |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in bs)
        {
            if (!Has(b.Make(), UnitCatalog.ShigaSIb)) continue;
            var w = WaveOf(b.Wave);
            foreach (var v in ShigaVers)
            {
                var f = Apply(b.Make(), v);
                var a = MeasureDeep(f, w, Seeds);
                Console.WriteLine($"| {b.Name} | {b.Wave} | {v.Name} | {F1(a.Win)} | {Per(a.WinT, a.Wins)} | {Per(a.Swings, a.N)}（{Per(a.Swings - a.SwFires, a.N)} ／ {Per(a.SwFires, a.N)}） | {Per(a.Cower, a.N)} | {Per(a.Kept, a.N)} | "
                    + $"{Per1(a.ShDmg, a.N)}（{Per1(a.ShDmg - a.SwWhip, a.N)} ／ {Per1(a.SwWhip, a.N)}） | {Per(a.ChargeEnd, a.N)} | {Per1(a.FoeDis, a.N)} ／ {Per1(a.AllyDis, a.N)} | {Per(a.FoeChains, a.N)} | "
                    + $"{Per1(a.ShDeathT, a.ShDied)}（{F1(100.0 * a.ShDied / a.N)}%） | {Ctl(f, v.To, w)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 表2 クグの版（旧 ＝ 規定 ／ KG-a ／ KG-b）・クグ在席の台");
        Console.WriteLine();
        Console.WriteLine("両 ＝ 帯電と組み付きが重なった手番の割合 ／ 糸 ＝ 糸が働いた回数（① 自分の弾け ／ ② 隣の放電を移した）／ 糸の与 ＝ 糸を伝った放電で減らした HP（倒した）／ 糸弾 ＝ 糸の放電で弾けた敵 ／ 敵陣 ＝ 糸を伝って敵の陣で弾けた延べ ／ 帯 ＝ KG-b で帯電させた（そこから起きた連鎖・その大きさ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率 | 倒しT | 組み付き | 両 | 糸（① ／ ②） | 糸の与（倒） | 糸弾 | 敵陣 | 帯（連鎖 ／ 大きさ） | 放電 敵 ／ 味方 | 敵の連鎖 | トウが倒れたT（割合） | クグが倒れたT（割合） | トウ → ドルガ | クグ → ドルガ |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in bs)
        {
            if (!Has(b.Make(), UnitCatalog.KuguKG0)) continue;
            foreach (var wn in new[] { b.Wave }.Concat(b.Wave == "近衛" && b.Name.StartsWith("クグ隣") ? new[] { "ボス" } : Array.Empty<string>()))
            {
                var w = WaveOf(wn);
                foreach (var v in KuguVers)
                {
                    var f = Apply(b.Make(), v);
                    var a = MeasureDeep(f, w, Seeds);
                    Console.WriteLine($"| {b.Name} | {wn} | {v.Name} | {F1(a.Win)} | {Per(a.WinT, a.Wins)} | {Per(a.Grapples, a.N)} | {Pct(a.GBoth, a.GTurns)} | {Per(a.ThSelf + a.ThRelay, a.N)}（{Per(a.ThSelf, a.N)} ／ {Per(a.ThRelay, a.N)}） | "
                        + $"{Per1(a.ThDealt, a.N)}（{Per(a.ThKills, a.N)}） | {Per(a.ThPopped, a.N)} | {Per(a.ThCross, a.N)} | {Per(a.ThCharged, a.N)}（{Per(a.ThMarkRoots, a.N)} ／ {Per(a.ThMarkUnits, a.ThMarkRoots)}） | "
                        + $"{Per1(a.FoeDis, a.N)} ／ {Per1(a.AllyDis, a.N)} | {Per(a.FoeChains, a.N)} | {Per1(a.TDeathT, a.TDied)}（{F1(100.0 * a.TDied / a.N)}%） | {Per1(a.GDeathT, a.GDied)}（{F1(100.0 * a.GDied / a.N)}%） | "
                        + $"{Ctl(f, UnitCatalog.Tou, w)} | {Ctl(f, v.To, w)} |");
                }
            }
        }
        Console.WriteLine();
        foreach (var b in bs) Console.WriteLine($"- {b.Name}: {BA.SeatsNamed(b.Make())}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 段2: シガ × カタの同居（6 通り）
    // ---------------------------------------------------------------------------------
    static void Duo()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第290期 段2 —— シガの版 × カタの版（6 通り・seed 0..199・verbose）");
        Console.WriteLine();
        Console.WriteLine("振 ＝ シガの振り（手番 ／ 割り込み）／ 与 ＝ シガの与ダメ ／ 雷 ＝ カタの雷（1戦）／ 1発 ＝ 雷の名目の平均 ／ 雷雲 ＝ 落とした時点の雷雲（1〜4 回目の平均）／ 敵の連鎖 ＝ 1戦の数（1回の大きさ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | シガ | カタ | 勝率 | 倒しT | 振（手番 ／ 割） | 怖 | シガの与 | 雷 | 1発 | 雷雲（1 ／ 2 ／ 3 ／ 4 回目） | カタの与 | 敵の連鎖（大きさ） | 放電 敵 ／ 味方 | シガ → ドルガ | カタ → ドルガ |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var row = CompareBuilds().First(r => r.Name == "感電 (シガ×カタ×ソム)").F;
        var items = new List<(string Name, string Wave, Formation F)>();
        foreach (var b in Boards().Where(b => b.Name.StartsWith("トウ+シガ+カタ"))) items.Add((b.Name, b.Wave, b.Make()));
        foreach (var wn in new[] { "近衛", "大隊", "ボス" }.Concat(Main.Select(m => m.Name))) items.Add(("感電 (シガ×カタ×ソム)", wn, row));
        foreach (var it in items)
        {
            var w = WaveOf(it.Wave);
            foreach (var vs in ShigaVers)
                foreach (var vk in KataVers)
                {
                    var f = Apply(it.F, vs, vk);
                    var a = MeasureDeep(f, w, Seeds);
                    string clouds = string.Join(" ／ ", Enumerable.Range(0, 4).Select(i => Per(a.CloudByCast[i], a.CastsAt[i])));
                    Console.WriteLine($"| {it.Name} | {it.Wave} | {vs.Name} | {vk.Name} | {F1(a.Win)} | {Per(a.WinT, a.Wins)} | {Per(a.Swings, a.N)}（{Per(a.Swings - a.SwFires, a.N)} ／ {Per(a.SwFires, a.N)}） | {Per(a.Cower, a.N)} | {Per1(a.ShDmg, a.N)} | "
                        + $"{Per(a.Casts, a.N)} | {Per1(a.KNom, a.Hits)} | {clouds} | {Per1(a.KDmg, a.N)} | {Per(a.FoeChains, a.N)}（{Per(a.FoeChainUnits, a.FoeChains)}） | {Per1(a.FoeDis, a.N)} ／ {Per1(a.AllyDis, a.N)} | "
                        + $"{Ctl(f, vs.To, w)} | {Ctl(f, vk.To, w)} |");
                }
        }
        Console.WriteLine();
        foreach (var it in items.DistinctBy(i => i.Name)) Console.WriteLine($"- {it.Name}: {BA.SeatsNamed(it.F)}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 格子（段2 の小さな格子 ／ 段3 のクグの格子）
    // ---------------------------------------------------------------------------------
    sealed record BoardRes(UnitDef[] Order, int Wins, long WinT, int DolgaTou, int[] DolgaX, bool Reach);

    static List<UnitDef[]> Lineups(UnitDef[] pool, int k)
    {
        var heal = S287.Heal;
        var l = new List<UnitDef[]>();
        void Rec(int start, List<UnitDef> cur)
        {
            if (cur.Count == k) { if (cur.Count(heal.Contains) <= 1) l.Add(cur.ToArray()); return; }
            for (int i = start; i < pool.Length; i++) { cur.Add(pool[i]); Rec(i + 1, cur); cur.RemoveAt(cur.Count - 1); }
        }
        Rec(0, new List<UnitDef>());
        return l;
    }

    /// <summary>
    /// 格子の本体（第287期と同じ切り方）: 足切り seed 0..19（近衛 ／ 大隊 5 勝以上）→ seed 0..199 → 勝率 50% 以上ならドルガ対照（トウ → ドルガ ＋ 固定枠の各駒 → ドルガ・R383）。
    /// 届いた ＝ 勝率 50% 以上かつトウ → ドルガがその半分以下。駒が要る ＝ その駒 → ドルガがその半分以下。
    /// </summary>
    static void GridCore(string title, S287.Wave w, UnitDef[] fixedU, UnitDef[] pool, int k, UnitDef[] ctrl)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lu = Lineups(pool, k);
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedU.Concat(t).ToArray()));
        var res = new BoardRes?[boards.Count];
        int cutPass = 0, winPass = 0;
        Parallel.For(0, boards.Count, i =>
        {
            var f = Seat(boards[i]);
            if (Wins(f, w, CutSeeds, out _) < 5) return;
            Interlocked.Increment(ref cutPass);
            int wins = Wins(f, w, Seeds, out long wt);
            int dg = -1;
            var dx = new int[ctrl.Length];
            for (int c = 0; c < ctrl.Length; c++) dx[c] = -1;
            if (wins * 2 >= Seeds)
            {
                Interlocked.Increment(ref winPass);
                dg = Wins(FvSwap(f, UnitCatalog.Tou, UnitCatalog.Dolga), w, Seeds, out _);
                for (int c = 0; c < ctrl.Length; c++) dx[c] = Wins(FvSwap(f, ctrl[c], UnitCatalog.Dolga), w, Seeds, out _);
            }
            res[i] = new BoardRes(boards[i], wins, wt, dg, dx, dg >= 0 && dg * 2 <= wins);
        });
        var all = res.Where(r => r is not null).Select(r => r!).ToList();
        var reached = all.Where(r => r.Reach).ToList();
        bool IsHeal(UnitDef d) => S287.Heal.Contains(d);
        bool Need(BoardRes r, int c) => r.DolgaX[c] * 2 <= r.Wins;
        string Key(UnitDef[] o) => string.Join(",", o.Select(d => d.Id).OrderBy(s => s, StringComparer.Ordinal));

        Console.WriteLine($"# 第290期 格子 × {w.Name} × {title}");
        Console.WriteLine();
        Console.WriteLine($"探索枠{k}（候補 {pool.Length} 枚: {OrderName(pool)}・ヒーラー ≦ 1）× 席 120 ＝ {boards.Count:N0} 台。足切り seed 0..{CutSeeds - 1}（5 勝以上）→ seed 0..{Seeds - 1} → ドルガ対照（トウ ／ {string.Join(" ／ ", ctrl.Select(Short))} → ドルガ）。届いた ＝ 第287期と同じ（トウ → ドルガで落ちる）");
        Console.WriteLine();
        Console.WriteLine("## 表1 段ごとの台数");
        Console.WriteLine();
        Console.WriteLine("| 段 | 台 | 組 | うちヒーラー 0 枚の台 ／ 組 | 全体の台に対する割合 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        void RowOf(string label, IEnumerable<UnitDef[]> os)
        {
            var l = os.ToList();
            var h0 = l.Where(o => !o.Any(IsHeal)).ToList();
            Console.WriteLine($"| {label} | {l.Count:N0} | {l.Select(Key).Distinct().Count()} | {h0.Count:N0} ／ {h0.Select(Key).Distinct().Count()} | {Pct(l.Count, boards.Count)} |");
        }
        RowOf("全体", boards);
        RowOf("足切りを通った", all.Select(r => r.Order));
        RowOf("勝率 ≧ 50%", all.Where(r => r.DolgaTou >= 0).Select(r => r.Order));
        RowOf("**届いた**（トウ → ドルガで落ちる）", reached.Select(r => r.Order));
        for (int c = 0; c < ctrl.Length; c++) { int cc = c; RowOf($"届いた台のうち **{Short(ctrl[c])}が要る**（{Short(ctrl[c])} → ドルガでも落ちる）", reached.Where(r => Need(r, cc)).Select(r => r.Order)); }
        if (ctrl.Length == 2)
        {
            RowOf("届いた台のうち **両方が要る**", reached.Where(r => Need(r, 0) && Need(r, 1)).Select(r => r.Order));
            RowOf("届いた台のうち どちらも要らない", reached.Where(r => !Need(r, 0) && !Need(r, 1)).Select(r => r.Order));
        }
        if (fixedU.Any(d => d.Id == "kugu")) RowOf("届いた台のうち クグとトウが隣", reached.Where(r => KuguTouAdjacent(r.Order)).Select(r => r.Order));
        Console.WriteLine();
        Console.WriteLine("## 表2 駒別の到達寄与（届いた台にその駒が入っていた数 ÷ その駒を含む台）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 届いた台 | 全体の台（分母） | 割合 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var d in pool)
        {
            int m = reached.Count(r => r.Order.Contains(d)), denom = boards.Count(o => o.Contains(d));
            Console.WriteLine($"| {d.Name} | {m:N0} | {denom:N0} | {Pct(m, denom)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 表3 上位の台（届いた台・上位 15）");
        Console.WriteLine();
        Console.WriteLine($"| # | 席（前1・前3・中央・後1・後3） | 勝率 | 倒しT | トウ → ドルガ | {string.Join(" | ", ctrl.Select(c => Short(c) + " → ドルガ"))} |");
        Console.WriteLine("|--:|---|--:|--:|--:|" + string.Concat(ctrl.Select(_ => "--:|")));
        var top = reached.OrderByDescending(r => r.Wins).ThenBy(r => r.Wins == 0 ? double.MaxValue : (double)r.WinT / r.Wins).ThenBy(r => OrderName(r.Order), StringComparer.Ordinal).ToList();
        for (int i = 0; i < Math.Min(15, top.Count); i++)
        {
            var r = top[i];
            Console.WriteLine($"| {i + 1} | {OrderName(r.Order)} | {F1(100.0 * r.Wins / Seeds)} | {Per(r.WinT, r.Wins)} | {F1(100.0 * r.DolgaTou / Seeds)} | {string.Join(" | ", r.DolgaX.Select(x => F1(100.0 * x / Seeds)))} |");
        }
        Console.WriteLine();
        var reps = new List<BoardRes>();
        foreach (var r in top) { if (reps.Any(q => Key(q.Order) == Key(r.Order))) continue; reps.Add(r); if (reps.Count == 3) break; }
        Console.WriteLine("## 表4 中身（上位3台・組が重ならないように・seed 0..199・verbose）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 倒しT | シガ 振（割） | シガの与 | カタ 雷 | 1発 | カタの与 | クグ 両 | 糸（① ／ ②） | 糸の与 | 帯（連鎖） | 放電 敵 ／ 味方 | 敵の連鎖 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in reps)
        {
            var d = MeasureDeep(Seat(r.Order), w, Seeds);
            Console.WriteLine($"| {OrderName(r.Order)} | {F1(d.Win)} | {Per(d.WinT, d.Wins)} | {Per(d.Swings, d.N)}（{Per(d.SwFires, d.N)}） | {Per1(d.ShDmg, d.N)} | {Per(d.Casts, d.N)} | {Per1(d.KNom, d.Hits)} | {Per1(d.KDmg, d.N)} | "
                + $"{Pct(d.GBoth, d.GTurns)} | {Per(d.ThSelf, d.N)} ／ {Per(d.ThRelay, d.N)} | {Per1(d.ThDealt, d.N)} | {Per(d.ThCharged, d.N)}（{Per(d.ThMarkRoots, d.N)}） | {Per1(d.FoeDis, d.N)} ／ {Per1(d.AllyDis, d.N)} | {Per(d.FoeChains, d.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒（足切り {boards.Count:N0} 台 → 本段 {cutPass:N0} 台 → ドルガ対照 {winPass:N0} 台 × {1 + ctrl.Length}）。");
    }

    /// <summary>
    /// 段2 の小さな格子: 固定枠 トウ ＋ シガ ＋ カタ（版）・探索枠2 ＝ 第287期の候補 21 枚からカタを除いた 20 枚（トウ・シガは候補に入っていない・クグは規定のクグ）。
    /// </summary>
    static void Grid3(string waveName, string shigaVer, string kataVer)
    {
        var w = WaveOf(waveName);
        var vs = VerOf(shigaVer, ShigaVers); var vk = VerOf(kataVer, KataVers);
        var pool = S287.Pool.Where(d => d.Id != "kata").ToArray();
        GridCore($"固定枠 トウ ＋ シガ（{vs.Name}）＋ カタ（{vk.Name}）", w, new[] { UnitCatalog.Tou, vs.To, vk.To }, pool, 2, new[] { vs.To, vk.To });
    }

    /// <summary>
    /// 段3 の格子: 固定枠 トウ ＋ クグ（版）・探索枠3 ＝ 第287期の候補 21 枚からクグを抜き、規定のシガ（SI-b）を入れた 21 枚。カタは規定（KR-b）にする（決めたこと）。
    /// </summary>
    static void GridK(string waveName, string kuguVer)
    {
        var w = WaveOf(waveName);
        var v = VerOf(kuguVer, KuguVers);
        var pool = S287.Pool.Select(d => d.Id == "kugu" ? UnitCatalog.ShigaSIb : d.Id == "kata" ? UnitCatalog.KataKRb : d).ToArray();
        GridCore($"固定枠 トウ ＋ クグ（{v.Name}）", w, new[] { UnitCatalog.Tou, v.To }, pool, 3, new[] { v.To });
    }

    /// <summary>
    /// シガの精鋭の格子（第289期 `shock289 grid <波> shiga sib` と同じ台・同じ切り方）: 固定枠 トウ ＋ シガ（版）・探索枠3 ＝ 第287期の候補 21 枚（カタは第289期の規定 S3 に固定してある）。
    /// 指示書 §6-2: SI-c が代表台で SI-b を明確に上回ったときだけ回す。
    /// </summary>
    static void GridS(string waveName, string shigaVer)
    {
        var w = WaveOf(waveName);
        var v = VerOf(shigaVer, ShigaVers);
        GridCore($"固定枠 トウ ＋ シガ（{v.Name}）・第289期のシガの格子と同じ台", w, new[] { UnitCatalog.Tou, v.To }, S287.Pool, 3, new[] { v.To });
    }

    // ---------------------------------------------------------------------------------
    // `compare` 64 行 × 版
    // ---------------------------------------------------------------------------------
    static double[,] CompareGrid(params Ver[] vs)
    {
        var rows = Rows290();
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            var f = Apply(rows[ri].F, vs);
            int wins = 0;
            for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / Seeds;
        });
        return g;
    }

    static void CompareAll()
    {
        var rows = Rows290();
        int nw = EnemyCatalog.Stages.Count;
        Console.WriteLine("# 第290期 `compare` 64 行 × 版（seed 0..199・その駒の在席行だけ差し替える）");
        var prim = Baseline.PrimaryRows.Select(n => Array.FindIndex(rows, r => r.Name == n)).ToArray();
        var basis = CompareGrid();
        foreach (var set in new[] { ShigaVers.Skip(1).ToArray(), KuguVers })
        {
            var grids = set.ToDictionary(v => v.Name, v => ReferenceEquals(v.From, v.To) ? basis : CompareGrid(v));
            Console.WriteLine();
            Console.WriteLine($"## {Short(set[0].From)}（{string.Join(" ／ ", set.Select(v => v.Name))}・先頭 ＝ 規定）");
            Console.WriteLine();
            Console.WriteLine("| 行 | 版 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | 差 | 最大の落ち |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|--:|");
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!Has(rows[ri].F, set[0].From)) continue;
                double m0 = Enumerable.Range(1, nw - 1).Average(w => basis[ri, w]);
                foreach (var v in set)
                {
                    var g = grids[v.Name];
                    double m = Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
                    double drop = Enumerable.Range(0, nw).Min(w => g[ri, w] - basis[ri, w]);
                    bool b0 = v == set[0];
                    Console.WriteLine($"| {rows[ri].Name} | {v.Name} | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w]))) + $" | {F1(m)} | {(b0 ? "" : (m - m0).ToString("+0.0;-0.0;0.0"))} | {(b0 ? "" : drop.ToString("+0.0;-0.0;0.0"))} |");
                }
            }
            Console.WriteLine();
            foreach (var v in set.Skip(1))
            {
                int bad = 0, cells = 0;
                for (int ri = 0; ri < rows.Length; ri++)
                {
                    if (Has(rows[ri].F, set[0].From)) continue;
                    for (int w = 0; w < nw; w++) { cells++; if (grids[v.Name][ri, w] != basis[ri, w]) bad++; }
                }
                Console.WriteLine($"- {Short(set[0].From)}のいない行のずれ（{v.Name} − 規定）: {cells} セル中 **{bad} 件**");
            }
            Console.WriteLine();
            Console.WriteLine($"| 版 | 全64行 第1〜5波 | 主判定19行 | 主判定の第五波 − 歯止め（{Baseline.PrimaryFifthFloor}%） | 情報セル（全行 ／ 主判定） | −10pt 以上落ちた行 |");
            Console.WriteLine("|---|---|---|--:|--:|---|");
            foreach (var v in set)
            {
                var g = grids[v.Name];
                int info = 0, infoP = 0;
                for (int ri = 0; ri < rows.Length; ri++) for (int w = 1; w < nw; w++) if (g[ri, w] > 0 && g[ri, w] < 100) { info++; if (prim.Contains(ri)) infoP++; }
                var drops = new List<string>();
                for (int ri = 0; ri < rows.Length; ri++) for (int w = 0; w < nw; w++) if (g[ri, w] - basis[ri, w] <= -10.0) drops.Add($"{rows[ri].Name} 第{w + 1}波 {basis[ri, w]:F1} → {g[ri, w]:F1}");
                Console.WriteLine($"| {v.Name} | " + string.Join(" / ", Enumerable.Range(0, nw).Select(w => F1(Enumerable.Range(0, rows.Length).Average(ri => g[ri, w]))))
                    + " | " + string.Join(" / ", Enumerable.Range(0, nw).Select(w => F1(prim.Average(ri => g[ri, w]))))
                    + $" | {(prim.Average(ri => g[ri, nw - 1]) - Baseline.PrimaryFifthFloor):+0.0;-0.0} | {info} ／ {infoP} | {(drops.Count == 0 ? "なし" : string.Join("・", drops))} |");
            }
        }
        // 規定の行の値（docs/balance.md と突き合わせる用）
        Console.WriteLine();
        Console.WriteLine("## 規定（第290期）のシガ ／ カタ在席の行");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")));
        for (int ri = 0; ri < rows.Length; ri++)
            if (Has(rows[ri].F, UnitCatalog.ShigaSIb) || Has(rows[ri].F, UnitCatalog.KataKRb))
                Console.WriteLine($"| {rows[ri].Name} | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => F1(basis[ri, w]))) + " |");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int fail = 0;
        void Expect(string what, bool ok, string detail = "")
        {
            Console.WriteLine($"- {(ok ? "○" : "×")} {what}{(detail.Length > 0 ? "（" + detail + "）" : "")}");
            if (!ok) fail++;
        }
        Console.WriteLine("# shock290 check");
        Console.WriteLine();
        var sga = new[] { TraitId.Scourge, TraitId.Shame, TraitId.Lash, TraitId.LiveWire, TraitId.ScourgeShock, TraitId.StoredCharge };
        var s3 = new[] { TraitId.Thunder, TraitId.ThunderLeak, TraitId.ThunderPath, TraitId.ShockStunHalf };
        Expect("(a) 規定のシガ ＝ SI-b（SG-a ＋ 振りまくる）・`ShigaSIb` ＝ 規定・`ShigaSGa` ＝ 第289期の規定（`All` ／ `Retired` に入っていない）",
            UnitCatalog.ShigaSIb.Traits.SequenceEqual(sga.Append(TraitId.ShockWhipFlurry)) && ReferenceEquals(UnitCatalog.ShigaSIb, UnitCatalog.ShigaSIb)
            && UnitCatalog.ShigaSGa.Traits.SequenceEqual(sga) && UnitCatalog.Shiga.Traits.SequenceEqual(UnitCatalog.ShigaSIb.Traits.Append(TraitId.ShockWhipChain)) /* 第294期: 規定は SW-a・SI-b は ShigaSIb */ && !UnitCatalog.Everyone.Contains(UnitCatalog.ShigaSGa));
        Expect("(b) 規定のカタ ＝ KR-b（S3 ＋ 雷雲 ＋ 雷雲が残る）・`KataKRb` ＝ 規定・`KataS3` ＝ 第289期の規定（`All` ／ `Retired` に入っていない）",
            // 第293期: 規定は KR-∞ になった——第290期の規定（KR-b）は `KataKRb` に固定してあることを確かめる
            UnitCatalog.KataKRb.Traits.SequenceEqual(s3.Append(TraitId.Thundercloud).Append(TraitId.ThundercloudKeep)) && UnitCatalog.Kata.Traits.SequenceEqual(UnitCatalog.KataKRb.Traits.Append(TraitId.ThundercloudUncapped))
            && UnitCatalog.KataS3.Traits.SequenceEqual(s3) && !UnitCatalog.Everyone.Contains(UnitCatalog.KataKRb) && !UnitCatalog.Everyone.Contains(UnitCatalog.KataS3)
            && UnitCatalog.KataS3.MaxHp == UnitCatalog.KataKRb.MaxHp && UnitCatalog.KataS3.Attack == UnitCatalog.KataKRb.Attack && UnitCatalog.KataS3.Speed == UnitCatalog.KataKRb.Speed);
        Expect("(c) 規定の文面 ＝ 第289期の版の追記（シガ ＝ SG-a の文面 ＋ 割り込み・カタ ＝ S3 の文面 ＋ 雷雲）・SI-c の文面は SI-b のまま",
            UnitCatalog.ShigaSIb.PlusText == UnitCatalog.ShigaSGa.PlusText + "。そばで感電が弾けるたび、溜めた電気を1つ使って割り込み、鞭を振るう"
            && UnitCatalog.KataKRb.PlusText == UnitCatalog.KataS3.PlusText + "。盤上で感電が弾けるたび雷雲が湧き、雷は戦が進むほど重くなる"
            && UnitCatalog.ShigaSIc.PlusText == UnitCatalog.ShigaSIb.PlusText);
        Expect("(d) SI-c ＝ SI-b ＋ `ShockWhipKeep`・KG-a ＝ 規定のクグ ＋ `Thread`・KG-b ＝ KG-a ＋ `ThreadCharge`・旧のクグ（`KuguKG0`）は組み付きだけ・版は `All` ／ `Retired` に入っていない（第291期から KG-b ＝ 規定）",
            UnitCatalog.ShigaSIc.Traits.SequenceEqual(UnitCatalog.ShigaSIb.Traits.Append(TraitId.ShockWhipKeep))
            && UnitCatalog.KuguKG0.Traits.SequenceEqual(new[] { TraitId.Grapple })
            && UnitCatalog.KuguKGa.Traits.SequenceEqual(new[] { TraitId.Grapple, TraitId.Thread }) && UnitCatalog.KuguKGb.Traits.SequenceEqual(new[] { TraitId.Grapple, TraitId.Thread, TraitId.ThreadCharge })
            && new[] { UnitCatalog.KuguKGa, UnitCatalog.KuguKGb }.All(d => d.MaxHp == UnitCatalog.KuguKG0.MaxHp && d.Attack == UnitCatalog.KuguKG0.Attack && d.Speed == UnitCatalog.KuguKG0.Speed && d.Id == "kugu")
            && !new[] { UnitCatalog.ShigaSIc, UnitCatalog.KuguKGa, UnitCatalog.KuguKG0 }.Any(UnitCatalog.Everyone.Contains)
            && !UnitCatalog.All.Any(u => u.Traits.Contains(TraitId.ShockWhipKeep))
            && UnitCatalog.All.Where(u => u.Traits.Any(t => t is TraitId.Thread or TraitId.ThreadCharge)).SequenceEqual(new[] { UnitCatalog.Kugu }) && UnitCatalog.Kugu.Traits.Take(3).SequenceEqual(UnitCatalog.KuguKGb.Traits));   // 第291期: KG-b が規定になった

        // 帳簿の検査: 代表台 × 版 × seed 0..39
        var bs = Boards();
        long keptB = 0, keptC = 0, firesC = 0, thOld = 0, thA = 0, chargedA = 0, chargedB = 0, relayOver = 0, selfOver = 0, markOver = 0, memoLeft = 0, nondet = 0, bad = 0, thB = 0;
        foreach (var b in bs)
        {
            for (int s = 0; s < 40; s++)
            {
                var w = WaveOf(b.Wave);
                if (Has(b.Make(), UnitCatalog.ShigaSIb))
                    foreach (var v in ShigaVers.Skip(1))
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(Apply(b.Make(), v), BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                        if (!r.TallyByUnit.TryGetValue("shiga", out var t)) continue;
                        if (v.Name == "SI-b") keptB += t.SwKeptShock;
                        else { keptC += t.SwKeptShock; firesC += t.SwFires; }
                    }
                if (!Has(b.Make(), UnitCatalog.KuguKG0)) continue;
                foreach (var wn in new[] { b.Wave, "ボス" })
                {
                    var ww = WaveOf(wn);
                    foreach (var v in KuguVers)
                    {
                        var p = BattleEngine.Materialize(Apply(b.Make(), v), BattleContext.PlayerTeam);
                        var e = ww.Make();
                        var r = BattleEngine.Run(p, e, s, verbose: false);
                        if (p.Concat(e).Any(u => u.RawCounter(ThreadTrait.MemoKey) != 0)) memoLeft++;
                        if (!r.TallyByUnit.TryGetValue("kugu", out var t)) continue;
                        long th = t.ThreadSelf + t.ThreadRelay;
                        if (v.Name == "旧") thOld += th + t.ThreadCharged;
                        if (v.Name == "KG-a") { thA += th; chargedA += t.ThreadCharged; }
                        if (v.Name == "KG-b") { thB += th; chargedB += t.ThreadCharged; }
                        if (t.ThreadRelay > t.KuguDisIn) relayOver++;
                        if (t.ThreadSelf > t.KuguPopHit + t.KuguPopChain) selfOver++;
                        if (t.ThreadMarkRoots > t.ThreadCharged) markOver++;
                    }
                }
            }
        }
        Expect("(e) SI-c の割り込みの鞭は感電を残す（SI-b は 0 回）", keptB == 0 && keptC > 0 && firesC > 0, $"SI-b {keptB} ／ SI-c {keptC}（割り込み {firesC}）");
        Expect("(f) 規定のクグは糸を使わない・KG-a は糸を使い帯電させない・KG-b は帯電させる", thOld == 0 && thA > 0 && chargedA == 0 && thB > 0 && chargedB > 0, $"旧 {thOld} ／ KG-a 糸 {thA}・帯 {chargedA} ／ KG-b 糸 {thB}・帯 {chargedB}");
        Expect("(g) 糸 ② は隣の放電の数以下・糸 ① は弾けの数以下・帯電させた敵の連鎖は帯電させた数以下", relayOver == 0 && selfOver == 0 && markOver == 0, $"{relayOver} ／ {selfOver} ／ {markOver}");
        Expect("(h) ほどけた一撃の間だけの糸の控え（`threadMemo`）は戦の終わりに残っていない", memoLeft == 0, $"{memoLeft} 戦");

        // (i) 軽い口と詳しい口・決定性（版すべて × クグ・同居の台）
        foreach (var b in bs.Where(b => Has(b.Make(), UnitCatalog.KuguKG0) || b.Name.StartsWith("トウ+シガ+カタ")))
            foreach (var v in ShigaVers.Concat(KataVers).Concat(KuguVers))
                for (int s = 0; s < 10; s++)
                {
                    var f = Apply(b.Make(), v);
                    var w = WaveOf(b.Wave);
                    bool a = FightLite(f, w, s, out int ta);
                    var d = FightDeep(f, w, s);
                    if (a != (d.Wins == 1) || (a && ta != d.WinT)) bad++;
                    if (FightLite(f, w, s, out int tb) != a || tb != ta) nondet++;
                }
        Expect("(i) 軽い口と詳しい口で勝敗・決着T が一致（8 版）", bad == 0, $"{bad} 件");
        Expect("(j) seed 決定的", nondet == 0, $"{nondet} 件");
        // (k) 新しい本体は PickOne ／ Roll を呼ばない
        string root = Directory.GetCurrentDirectory();
        string en = File.ReadAllText(Path.Combine(root, "BattleCore", "BattleEngine.cs"));
        int j0 = en.IndexOf("UnitState? ThreadTarget(UnitState k)", StringComparison.Ordinal), j1 = en.IndexOf("public void NoteKuguCensus()", StringComparison.Ordinal);
        int k0 = en.IndexOf("void ThreadDischarge(", StringComparison.Ordinal), k1 = en.IndexOf("/// <summary>決着時に残っていた感電を数える", StringComparison.Ordinal);
        bool scanned = j0 > 0 && j1 > j0 && k0 > 0 && k1 > k0;
        Expect("(k) 糸の本体は `PickOne` ／ `Roll` を呼ばない（ソースの走査）", scanned && !en[j0..j1].Contains("PickOne") && !en[j0..j1].Contains("Roll(") && !en[k0..k1].Contains("PickOne") && !en[k0..k1].Contains("Roll("));
        // (l) 第289期の器具が第289期の規定に固定されている（`shock289` の台に規定の駒が残っていない）
        Expect("(l) `shock289` の台は第289期の規定（SG-a ／ S3）に固定されている",
            S289.Boards().All(b => !Has(b.Make(), UnitCatalog.ShigaSIb) && !Has(b.Make(), UnitCatalog.KataKRb)) && S289.Rows289().All(r => !Has(r.F, UnitCatalog.ShigaSIb) && !Has(r.F, UnitCatalog.KataKRb)));
        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "すべて ○。" : $"× が {fail} 件。");
        Environment.ExitCode = fail == 0 ? 0 : 1;
    }

    static void LogOne(string wave, string order, int seed, string[] vers)
    {
        var w = WaveOf(wave);
        var f = Seat(Order(order));
        foreach (var vn in vers) f = Apply(f, AnyVer(vn));
        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# {BA.SeatsNamed(f)} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
