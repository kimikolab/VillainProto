using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S293 = Shock293Diag;
using S307 = Som307Diag;
using S309 = Som309Diag;
using S318 = Som318Diag;

// =====================================================================================
// som319 —— 第319期「段0: ソム CR-b ／ シガ SG の規定化 ／ 本体: 第2波の代金を消す版（WS-a ／ WS-b）＋ 大隊の格子」。
// 指示書は design/PHASE319_SOM_THROW_SPEC.md ／ 報告は design/PHASE319_SOM_THROW.md。
//
//     dotnet run --project BattleSim -c Release 0 som319 p0          # Phase 0（§4）: 新しい規定（CR-b ＋ SG）の立った獣の行方・第2波の時間の形・立った獣の後の連鎖・ソムが倒れる経緯
//     dotnet run --project BattleSim -c Release 0 som319 boards [版,…]  # §5-2: 代表台 7 × 10 波 × 版（旧 BD-E2 ／ 規定 ／ WS-a ／ WS-a3 ／ WS-b）
//     dotnet run --project BattleSim -c Release 0 som319 compare [版,…] # §5-3: `compare` 64 行 × 版（基準は規定）
//     dotnet run --project BattleSim -c Release 0 som319 grid <版> <boss|guard|bat>   # §5-4: ソムの格子（第311期と同じ作り・駒は規定）
//     dotnet run --project BattleSim -c Release 0 som319 noise <版> <台の一部> <波> <from> <n>   # 別の seed の帯で 旧 ／ 規定 ／ 版を並べる
//     dotnet run --project BattleSim -c Release 0 som319 memo <版> <台の一部> <boss|guard|bat|nine|nine2|1..5> <seed> <T0> <T1>
//     dotnet run --project BattleSim -c Release 0 som319 check       # 自己検査（§7）
//
// **予測のファイルが無ければ本測定（boards ／ compare ／ grid）を走らせない**（R397・`.tmp/p319/predict.md`）。
// 第320期: 規定のソムが WS-b になったので、台の駒は第319期の規定（CR-b・`UnitCatalog.SomH319`）に固定した（`compare` は `CompareBuildsH319`）。
// 検査の手助け（`One` ／ `TurnStart` ／ `T` ／ `Stood` ／ `AfterEv`）は `som320` が借りる（中身は変えていない）。
// 版の名前はコンソールの文字コードを避けて ASCII で引ける（`old` ＝ 旧 BD-E2・`base` ＝ 規定）。
// =====================================================================================
static class Som319Diag
{
    const string PredictFile = ".tmp/p319/predict.md";
    const int Seeds = S307.Seeds;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"som319: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": BoardsAll(A(3, "")); return;
            case "compare": CompareAll(A(3, "")); return;
            case "grid": Grid(A(3, "base"), A(4, "bat")); return;
            case "memo": Memo(A(3, "WS-b"), A(4, "感電"), A(5, "2"), int.Parse(A(6, "0")), int.Parse(A(7, "1")), int.Parse(A(8, "5"))); return;
            case "noise": Noise(A(3, "WS-b"), A(4, "感電"), A(5, "2"), int.Parse(A(6, "200")), int.Parse(A(7, "800"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("som319: モードは p0 / boards / compare / grid / noise / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);
    static string F1(double x) => S307.F1(x);
    static string Per1(long a, long n) => S307.Per1(a, n);
    static string Per2(long a, long n) => S307.Per(a, n);
    static string Per0(long a, long n) => S307.Per0(a, n);
    static string Pct(long a, long n) => S307.Pct(a, n);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Dt(long died, long t, long n) => died == 0 ? "—" : $"{Per1(t, died)}（{Pct(died, n)}）";

    /// <summary>版（ソムの版 ＋ シガの版）。</summary>
    internal sealed record Ver(string Name, string Ascii, UnitDef Som, UnitDef Shiga);

    internal static readonly Ver[] Vers =
    {
        new("規定", "base", UnitCatalog.SomH319, UnitCatalog.ShigaH321),
        new("旧 BD-E2", "old", UnitCatalog.SomH318, UnitCatalog.ShigaH318),
        new("WS-a", "WS-a", UnitCatalog.SomWSa, UnitCatalog.ShigaH321),
        new("WS-a3", "WS-a3", UnitCatalog.SomWSa3, UnitCatalog.ShigaH321),
        new("WS-b", "WS-b", UnitCatalog.SomWSb, UnitCatalog.ShigaH321),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();

    /// <summary>台（規定の駒）を版に替える。</summary>
    internal static Formation Apply(Formation f, Ver v)
    {
        var g = ReferenceEquals(v.Som, UnitCatalog.SomH319) ? f : S309.Swap(f, UnitCatalog.SomH319, v.Som);
        return ReferenceEquals(v.Shiga, UnitCatalog.ShigaH321) ? g : S309.Swap(g, UnitCatalog.ShigaH321, v.Shiga);
    }

    internal static readonly S287.Wave[] Waves = S318.Waves;
    static S287.Wave WaveOf(string n) => n switch { "nine" => Waves[3], "nine2" => Waves[4], _ => S307.WaveOf(n) };
    static S287.Wave Wave2 => Waves[6];

    /// <summary>第318期の器具の台（第318期の規定に固定してある）を規定の駒に戻す。</summary>
    static Formation Unpin(Formation f) => S309.Swap(S309.Swap(f, UnitCatalog.SomH318, UnitCatalog.SomH319), UnitCatalog.ShigaH318, UnitCatalog.ShigaH321);
    /// <summary>格子の候補（第317期までの器具が旧に固定した駒）を規定の駒に戻す。</summary>
    static UnitDef Cur(UnitDef d) => ReferenceEquals(d, UnitCatalog.ShigaSWa) ? UnitCatalog.ShigaH321 : ReferenceEquals(d, UnitCatalog.DohaH316) ? UnitCatalog.Doha : ReferenceEquals(d, UnitCatalog.SomH317) ? UnitCatalog.SomH319 : d;

    /// <summary>§5-2 の代表台（第318期と同じ7台・席は同じ・駒は規定）。</summary>
    internal static (string Name, Formation F)[] Boards() => S318.Boards().Select(b => (b.Name, Unpin(b.F))).ToArray();

    // ---------------------------------------------------------------------------------
    // 1戦の計数（第318期の計数 ＋ 立った獣の行方・ターンごと・ソムが倒れた経緯）
    // ---------------------------------------------------------------------------------
    internal sealed class X
    {
        public long N;
        // 立った獣の行方（獣ごと）: 投げた結末 × その後
        public long Stood, Res, Swing, Rod, Hushed, Unc, NoThrow, Backfire, BackfireDry, BackfireUnits;
        public long Left, LeftKilled, LeftPopped, LeftOverflow, LeftAtEnd, LeftLife, LeftLifeN;
        public long[] StoodT = new long[9], LeftT = new long[9], ThrowT = new long[9], ChargedT = new long[9], BackfireT = new long[9];
        public long[] DecideT = new long[32];
        public long PopChains, PopChainUnits, PopChainsAlly, PopChainUnitsAlly;
        // ソムが倒れた経緯
        public long SomDied, SomDeathFrontFirst, SomDeathLeftOnBoard;
        public Dictionary<string, long> SomKiller = new();
        public void Merge(X o)
        {
            N += o.N; Stood += o.Stood; Res += o.Res; Swing += o.Swing; Rod += o.Rod; Hushed += o.Hushed; Unc += o.Unc; NoThrow += o.NoThrow;
            Backfire += o.Backfire; BackfireDry += o.BackfireDry; BackfireUnits += o.BackfireUnits;
            Left += o.Left; LeftKilled += o.LeftKilled; LeftPopped += o.LeftPopped; LeftOverflow += o.LeftOverflow; LeftAtEnd += o.LeftAtEnd; LeftLife += o.LeftLife; LeftLifeN += o.LeftLifeN;
            for (int i = 0; i < 9; i++) { StoodT[i] += o.StoodT[i]; LeftT[i] += o.LeftT[i]; ThrowT[i] += o.ThrowT[i]; ChargedT[i] += o.ChargedT[i]; BackfireT[i] += o.BackfireT[i]; }
            for (int i = 0; i < DecideT.Length; i++) DecideT[i] += o.DecideT[i];
            PopChains += o.PopChains; PopChainUnits += o.PopChainUnits; PopChainsAlly += o.PopChainsAlly; PopChainUnitsAlly += o.PopChainUnitsAlly;
            SomDied += o.SomDied; SomDeathFrontFirst += o.SomDeathFrontFirst; SomDeathLeftOnBoard += o.SomDeathLeftOnBoard;
            foreach (var (k, v) in o.SomKiller) SomKiller[k] = SomKiller.GetValueOrDefault(k) + v;
        }
    }

    enum Outcome { None, Res, Swing, Rod, Hushed, Unc }

    internal static (S318.St S, X X, BattleResult R, List<UnitState> P, List<UnitState> E) FightX(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var s = S318.Count(p, e, r);
        var x = new X { N = 1 };
        x.DecideT[Math.Min(r.Turns, x.DecideT.Length - 1)]++;
        var mine = p.ToDictionary(u => u.InstanceId, u => u);
        UnitState? som = p.FirstOrDefault(u => u.Def.Id == "som");
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        // 立った獣（喚んだ獣のうち、その場で弾けた置物でないもの）
        var summoned = new HashSet<int>(); var phantom = new HashSet<int>();
        foreach (var ev in r.Events)
        {
            if (ev.Kind == BattleEventKind.Summon && som is not null && ev.ActorId == som.InstanceId && ev.TargetId is int sid) summoned.Add(sid);
            else if (ev.Kind == BattleEventKind.BeastBurst && ev.Text is not (BeastBurstLabels.Overflow or BeastBurstLabels.Rod or BeastBurstLabels.Popped or BeastBurstLabels.Resonance or BeastBurstLabels.Backfire) && ev.TargetId is int bid) phantom.Add(bid);
        }
        var standT = new Dictionary<int, int>();
        var outcome = new Dictionary<int, Outcome>();
        var gone = new HashSet<int>(); var backfired = new HashSet<int>();
        int frontDead = 0;
        void End(int id, int turn, string how)
        {
            if (!standT.TryGetValue(id, out int t0) || !gone.Add(id)) return;
            var o = outcome.GetValueOrDefault(id);
            if (how == "backfire") { x.Backfire++; backfired.Add(id); return; }
            bool left = o is Outcome.Unc or Outcome.Hushed;   // 投げる前に消えた獣（`None`）は残った獣に入れない
            if (o == Outcome.Swing && how is "popped" or "killed") return;   // 打ち返しで消えた
            if (o is Outcome.Res or Outcome.Rod) return;
            if (!left) return;
            x.LeftLife += turn - t0; x.LeftLifeN++;
            if (how == "killed") x.LeftKilled++; else if (how == "popped") x.LeftPopped++; else if (how == "overflow") x.LeftOverflow++;
        }
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Summon when ev.TargetId is int sid && summoned.Contains(sid) && !phantom.Contains(sid):
                    standT[sid] = ev.Turn; x.Stood++; x.StoodT[Math.Clamp(ev.Turn, 0, 8)]++;
                    if (som is not null) names[sid] = "獣";
                    break;
                case BattleEventKind.BeastThrow when ev.ActorId is int b && standT.ContainsKey(b):
                    switch (ev.Text)
                    {
                        case BeastThrowLabels.Resonance: outcome[b] = Outcome.Res; break;
                        case BeastThrowLabels.Rod: outcome[b] = Outcome.Rod; break;
                        case BeastThrowLabels.Hushed: outcome[b] = Outcome.Hushed; break;
                        case BeastThrowLabels.Hit or BeastThrowLabels.Thread: outcome[b] = Outcome.Unc; break;
                        case BeastThrowLabels.Backswing: outcome[b] = Outcome.Swing; break;
                    }
                    break;
                case BattleEventKind.BeastBurst when ev.TargetId is int bb && standT.ContainsKey(bb):
                    End(bb, ev.Turn, ev.Text switch { BeastBurstLabels.Backfire => "backfire", BeastBurstLabels.Popped => "popped", BeastBurstLabels.Overflow => "overflow", _ => "other" });
                    break;
                case BattleEventKind.Death when ev.TargetId is int db && standT.ContainsKey(db):
                    End(db, ev.Turn, "killed"); break;
                case BattleEventKind.Death when ev.TargetId is int t && mine.TryGetValue(t, out var mu):
                    if (FormationRules.RowOf(mu.Slot) == Row.Front) frontDead++;
                    if (som is not null && t == som.InstanceId && x.SomDied == 0)
                    {
                        x.SomDied = 1;
                        if (frontDead > 0) x.SomDeathFrontFirst = 1;
                        if (standT.Keys.Any(k => !gone.Contains(k))) x.SomDeathLeftOnBoard = 1;
                        string k = ev.ActorId is int ka && names.TryGetValue(ka, out var nm) ? nm : "（出どころ無し）";
                        x.SomKiller[k] = x.SomKiller.GetValueOrDefault(k) + 1;
                    }
                    break;
            }
        }
        foreach (var (id, t0) in standT)
        {
            var o = outcome.GetValueOrDefault(id);
            switch (o)
            {
                case Outcome.Res: x.Res++; break;
                case Outcome.Swing: x.Swing++; break;
                case Outcome.Rod: x.Rod++; break;
                case Outcome.Hushed: x.Hushed++; break;
                case Outcome.Unc: x.Unc++; break;
                default: x.NoThrow++; break;
            }
            bool left = o is Outcome.Unc or Outcome.Hushed;
            if (left && !backfired.Contains(id)) { x.Left++; x.LeftT[Math.Clamp(t0, 0, 8)]++; }
            if (!gone.Contains(id) && left) { x.LeftAtEnd++; x.LeftLife += r.Turns - t0; x.LeftLifeN++; }
        }
        if (som is not null && r.TallyByUnit.TryGetValue("som", out var mt))
        {
            if (mt.BtThrownT is { } a) for (int i = 0; i < 9; i++) x.ThrowT[i] = a[i];
            if (mt.BtAtChargedT is { } c) for (int i = 0; i < 9; i++) x.ChargedT[i] = c[i];
            if (mt.WsBackfireT is { } bf) for (int i = 0; i < 9; i++) x.BackfireT[i] = bf[i];
            x.BackfireDry = mt.WsBackfireDry; x.BackfireUnits = mt.WsBackfireUnits;
            x.PopChains = mt.StandPopChains; x.PopChainUnits = mt.StandPopChainUnits; x.PopChainsAlly = mt.StandPopChainsAlly; x.PopChainUnitsAlly = mt.StandPopChainUnitsAlly;
        }
        return (s, x, r, p, e);
    }

    internal static (S318.St S, X X) Measure(Formation f, S287.Wave w, int n = Seeds, int from = 0)
    {
        var ps = new S318.St[n]; var px = new X[n];
        Parallel.For(0, n, i => { var (s, x, _, _, _) = FightX(f, w, from + i); ps[i] = s; px[i] = x; });
        var a = new S318.St(); var b = new X();
        for (int i = 0; i < n; i++) { a.Merge(ps[i]); b.Merge(px[i]); }
        return (a, b);
    }

    static int WinsLite(Formation f, S287.Wave w, int n, int from = 0)
    {
        int wins = 0;
        Parallel.For(from, from + n, s =>
        {
            var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
            if (r.PlayerWon) Interlocked.Increment(ref wins);
        });
        return wins;
    }

    static string TurnSplit(long[] a, long[] of, long n, bool pct)
    {
        string C(int lo, int hi)
        {
            long x = 0, y = 0;
            for (int i = lo; i <= hi && i < a.Length; i++) { x += a[i]; y += of[i]; }
            return pct ? (y == 0 ? "—" : Pct(x, y)) : Per1(x, n);
        }
        return $"{C(1, 1)} ／ {C(2, 2)} ／ {C(3, 3)} ／ {C(4, 8)}";
    }

    static string Fate(X x) =>
        $"{Per1(x.Stood, x.N)} ・ {Per1(x.Res, x.N)} ・ {Per1(x.Swing, x.N)} ・ **{Per1(x.Unc, x.N)}** ・ {Per1(x.Hushed, x.N)} ・ {Per1(x.NoThrow, x.N)}" + (x.Rod > 0 ? $"（避雷針 {Per1(x.Rod, x.N)}）" : "");
    static string After(X x) =>
        x.Left == 0 && x.Backfire == 0 ? "—" : $"{Per1(x.Left, x.N)}（{Per1(x.LeftKilled, x.N)} ／ {Per1(x.LeftPopped, x.N)} ／ {Per1(x.LeftAtEnd, x.N)}{(x.LeftOverflow > 0 ? $" ／ 暴発 {Per1(x.LeftOverflow, x.N)}" : "")}）・ {Per1(x.LeftLife, x.LeftLifeN)}T";

    // ---------------------------------------------------------------------------------
    // Phase 0（§4）: 新しい規定（CR-b ＋ SG）を計数器つきで回す
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var cells = new List<(string B, Formation F, S287.Wave W, S318.St S, X X)>();
        foreach (var (n, f) in boards) foreach (var w in Waves) { var (s, x) = Measure(f, w); cells.Add((n, f, w, s, x)); }
        Console.WriteLine($"# 第319期 Phase 0（§4）—— 新しい規定（ソム CR-b ＝ BD-E2 ＋ 立つ ＋ 投げる ＋ 共鳴返し ＋ 走る・シガ SG ＝ SF-AC ＋ 打ち返し）・seed 0..{Seeds - 1}・verbose");
        Console.WriteLine();
        foreach (var (n, f) in boards) Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine();
        Console.WriteLine("## §4-1 立った獣の行方（/戦）");
        Console.WriteLine();
        Console.WriteLine("「立った」＝ 隣の敵が帯電しておらず、席に立った獣（その場で弾けた置物は入れない）。投げた雷の結末で分ける: **跳ね返り**（共鳴返し）・**打ち返し**（シガ）・**誰も受け止めず立った**（帯電していない味方に付いた ／ 糸へ流れた）・**粛で止まって立った**（共鳴が止まった）・**投げる前に弾けた**（同じ手番に先に投げた獣の連鎖で、投げる前に弾けた）。");
        Console.WriteLine("「残った獣」＝ 誰も受け止めず ／ 粛で止まって立ったままの獣。その後（味方の一撃で倒された ／ 弾けて消えた ／ 戦の終わりまで残った）と、立っていたターン数の平均。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 立った ・ 跳ね返り ・ 打ち返し ・ **誰も受け止めず立った** ・ 粛で止まって立った ・ 投げる前に弾けた | 残った獣（倒された ／ 弾けた ／ 戦の終わりまで）・ 立っていたT | 立った獣への味方の一撃 | ソム 倒れたT（割合） |");
        Console.WriteLine("|---|---|--:|---|---|--:|---|");
        foreach (var c in cells)
            Console.WriteLine($"| {c.B} | {c.W.Name} | {F1(c.S.Win)} | {Fate(c.X)} | {After(c.X)} | {Per1(c.S.AtkOnStand, c.S.N)} | {Dt(c.S.SomDied, c.S.SomDeathT, c.S.N)} |");
        Console.WriteLine();
        Console.WriteLine("### 台ごとの和（10 波・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 立った ・ 跳ね返り ・ 打ち返し ・ **誰も受け止めず立った** ・ 粛で止まって立った ・ 投げる前に弾けた | 残った獣（倒された ／ 弾けた ／ 戦の終わりまで）・ 立っていたT | 立った獣への味方の一撃 |");
        Console.WriteLine("|---|---|---|--:|");
        foreach (var (n, _) in boards)
        {
            var s = new S318.St(); var x = new X();
            foreach (var c in cells.Where(q => q.B == n)) { s.Merge(c.S); x.Merge(c.X); }
            long nn = x.N / Waves.Length;
            var xx = new X(); xx.Merge(x); xx.N = nn;
            Console.WriteLine($"| {n} | {Fate(xx)} | {After(xx)} | {Per1(s.AtkOnStand, nn)} |");
        }

        Console.WriteLine();
        Console.WriteLine("## §4-2 第2波の時間の形");
        Console.WriteLine();
        Console.WriteLine("決着T の分布（割合）と、戦のターンごと（T1 ／ T2 ／ T3 ／ T4 以降）の 投げた雷 /戦 ・ そのとき雷が向かった味方が帯電していた割合 ・ 立った獣 /戦 ・ 残った獣（誰も受け止めず ／ 粛で止まった）/戦。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 決着T 平均 | 決着 ≦T2 ／ T3 ／ T4 ／ T5 ／ ≧T6 | 投げた（T1 ／ T2 ／ T3 ／ T4〜） | 帯電していた割合 | 立った | 残った |");
        Console.WriteLine("|---|--:|--:|---|---|---|---|---|");
        foreach (var c in cells.Where(q => q.W == Wave2))
        {
            var x = c.X; long n = x.N;
            string D(int lo, int hi) { long k = 0; for (int i = lo; i <= hi; i++) k += x.DecideT[i]; return Pct(k, n); }
            Console.WriteLine($"| {c.B} | {F1(c.S.Win)} | {Per1(c.S.Turns, c.S.N)} | {D(0, 2)} ／ {D(3, 3)} ／ {D(4, 4)} ／ {D(5, 5)} ／ {D(6, x.DecideT.Length - 1)} | {TurnSplit(x.ThrowT, x.ThrowT, n, false)} | {TurnSplit(x.ChargedT, x.ThrowT, n, true)} | {TurnSplit(x.StoodT, x.StoodT, n, false)} | {TurnSplit(x.LeftT, x.LeftT, n, false)} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 同じ表（本編 第3波・参考）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 決着T 平均 | 投げた（T1 ／ T2 ／ T3 ／ T4〜） | 帯電していた割合 | 立った | 残った |");
        Console.WriteLine("|---|--:|--:|---|---|---|---|");
        foreach (var c in cells.Where(q => q.W == Waves[7]))
        {
            var x = c.X; long n = x.N;
            Console.WriteLine($"| {c.B} | {F1(c.S.Win)} | {Per1(c.S.Turns, c.S.N)} | {TurnSplit(x.ThrowT, x.ThrowT, n, false)} | {TurnSplit(x.ChargedT, x.ThrowT, n, true)} | {TurnSplit(x.StoodT, x.StoodT, n, false)} | {TurnSplit(x.LeftT, x.LeftT, n, false)} |");
        }

        Console.WriteLine();
        Console.WriteLine("## §4-3 立った獣が後で弾けて起きた連鎖（WS-b ではそれが「投げた直後の暴発」に置き換わる）");
        Console.WriteLine();
        Console.WriteLine("帳簿 `StandPopChains`: 立っていた獣の感電が後で弾けた連鎖（打ち返しの鞭の中は除く）の数 /戦 ・ 1連鎖の駒の数（獣を含む）。括弧はうち起こし手がソム以外の味方（味方の一撃）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 連鎖 /戦（味方の一撃） | 1連鎖の駒（味方の一撃） |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var c in cells.Where(q => q.X.PopChains > 0))
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Per2(c.X.PopChains, c.X.N)}（{Per2(c.X.PopChainsAlly, c.X.N)}） | {Per2(c.X.PopChainUnits, c.X.PopChains)}（{Per2(c.X.PopChainUnitsAlly, c.X.PopChainsAlly)}） |");

        Console.WriteLine();
        Console.WriteLine("## §4-4 第2波でソムが倒れる経緯");
        Console.WriteLine();
        Console.WriteLine("| 台 | ソムが倒れた（T） | 倒した駒 | 前列の味方が先に倒れていた | そのとき残った獣が盤にいた |");
        Console.WriteLine("|---|---|---|--:|--:|");
        foreach (var c in cells.Where(q => q.W == Wave2))
        {
            var x = c.X;
            string k = x.SomKiller.Count == 0 ? "—" : string.Join("・", x.SomKiller.OrderByDescending(q => q.Value).Select(q => $"{q.Key} {q.Value}"));
            Console.WriteLine($"| {c.B} | {Dt(c.S.SomDied, c.S.SomDeathT, c.S.N)} | {k} | {Pct(x.SomDeathFrontFirst, x.SomDied)} | {Pct(x.SomDeathLeftOnBoard, x.SomDied)} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 台本（ソムが倒れた戦・1行ずつ）");
        Console.WriteLine();
        foreach (var bn in new[] { "感電 行", S318.HagiBoard })
        {
            var f = boards.First(b => b.Name == bn).F;
            int shown = 0;
            for (int sd = 0; sd < Seeds && shown < 3; sd++)
            {
                var (_, _, r, p, e) = FightX(f, Wave2, sd);
                var som = p.First(u => u.Def.Id == "som");
                if (!r.Events.Any(ev => ev.Kind == BattleEventKind.Death && ev.TargetId == som.InstanceId)) continue;
                shown++;
                Console.WriteLine($"- {bn} × seed {sd}（{(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}）: {DeathLine(r, p, e, som)}");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>ソムが倒れるまでの並び（味方の死・立った獣・ソムを倒した一撃）を1行に。</summary>
    static string DeathLine(BattleResult r, List<UnitState> p, List<UnitState> e, UnitState som)
    {
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        var mine = p.ToDictionary(u => u.InstanceId, u => u);
        var parts = new List<string>();
        var stood = new Dictionary<int, int>();
        var phantom = new HashSet<int>(r.Events.Where(ev => ev.Kind == BattleEventKind.BeastBurst && ev.Text is BeastBurstLabels.Always or BeastBurstLabels.Charged or BeastBurstLabels.Swarm or BeastBurstLabels.Emergency && ev.TargetId is int).Select(ev => ev.TargetId!.Value));
        int alive = 0;
        foreach (var ev in r.Events)
        {
            if (ev.Kind == BattleEventKind.Summon && ev.ActorId == som.InstanceId && ev.TargetId is int sid && !phantom.Contains(sid)) { alive++; names[sid] = "獣"; stood[ev.Turn] = stood.GetValueOrDefault(ev.Turn) + 1; }
            else if (ev.Kind == BattleEventKind.BeastBurst && ev.TargetId is int bt && names.GetValueOrDefault(bt) == "獣" && ev.Text is BeastBurstLabels.Popped or BeastBurstLabels.Resonance or BeastBurstLabels.Backfire or BeastBurstLabels.Overflow or BeastBurstLabels.Rod) alive--;
            else if (ev.Kind == BattleEventKind.Death && ev.TargetId is int dt && names.GetValueOrDefault(dt) == "獣") alive--;
            else if (ev.Kind == BattleEventKind.Death && ev.TargetId is int t && mine.TryGetValue(t, out var mu))
            {
                string killer = ev.ActorId is int ka && names.TryGetValue(ka, out var nm) ? nm : "出どころ無し";
                if (t == som.InstanceId)
                {
                    parts.Add($"**T{ev.Turn} ソムが {killer} の一撃で倒れた**（そのとき盤に残った獣 {alive}）");
                    break;
                }
                parts.Add($"T{ev.Turn} {Short(mu.Def)}（{(FormationRules.RowOf(mu.Slot) == Row.Front ? "前列" : FormationRules.RowOf(mu.Slot) == Row.Back ? "後列" : "中央")}）が {killer} に倒された");
            }
        }
        string st = stood.Count == 0 ? "獣は立たなかった" : "立った獣 " + string.Join("・", stood.OrderBy(q => q.Key).Select(q => $"T{q.Key} {q.Value}"));
        return st + " → " + string.Join(" → ", parts);
    }

    // ---------------------------------------------------------------------------------
    // §5-2 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = Pick(only);
        var cells = new List<(string B, Formation F0, S287.Wave W, Ver V, S318.St S, X X)>();
        foreach (var (n, f) in Boards()) foreach (var v in vers) { var fv = Apply(f, v); foreach (var w in Waves) { var (s, x) = Measure(fv, w); cells.Add((n, f, w, v, s, x)); } }
        var ctl = new Dictionary<(string, string, string), string>();
        foreach (var c in cells.Where(c => c.S.Win >= 50))
            ctl[(c.B, c.W.Name, c.V.Name)] = F1(100.0 * WinsLite(S309.Swap(Apply(c.F0, c.V), c.V.Som, UnitCatalog.Dolga), c.W, Seeds) / Seeds);
        var bv = vers[0];
        (S318.St S, X X) Cell(string b, Ver v, S287.Wave w) { var c = cells.First(x => x.B == b && x.V == v && x.W == w); return (c.S, c.X); }

        Console.WriteLine($"# 第319期 §5-2 代表台 × 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}・seed 0..{Seeds - 1}・verbose・基準は規定 ＝ CR-b ＋ SG）");
        Console.WriteLine();
        foreach (var (n, f) in Boards()) Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine();
        Console.WriteLine("## 表A 勝率（括弧は規定との差・[ ] は ソム → ドルガ・勝率 50% 以上のセル）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")));
        foreach (var (n, _) in Boards())
            foreach (var v in vers)
                Console.WriteLine($"| {n} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var c = Cell(n, v, w).S; var b = Cell(n, bv, w).S;
                    return $"{F1(c.Win)}{(v == bv ? "" : $"（{Sg(c.Win - b.Win)}）")}{(ctl.TryGetValue((n, w.Name, v.Name), out var k) ? $" [{k}]" : "")}";
                })) + " |");

        Console.WriteLine();
        Console.WriteLine("## 表B 倒しT ／ 負けT（上限の割合）・ソムが倒れた割合");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (n, _) in Boards())
            foreach (var v in vers)
                Console.WriteLine($"| {n} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var a = Cell(n, v, w).S;
                    return $"{Per1(a.WinT, a.Wins)} ／ {Per1(a.LoseT, a.N - a.Wins)}{(a.Caps > 0 ? $"（{Pct(a.Caps, a.N)}）" : "")} ・ ソム {Pct(a.SomDied, a.N)}";
                })) + " |");

        Console.WriteLine();
        Console.WriteLine("## 表C 獣と裏切りの雷（10 波の和・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | その場で弾けた（場の判定 ／ 序盤 ／ 群れ ／ 緊急） | 立った ・ 跳ね返り ・ 打ち返し ・ **誰も受け止めず立った** ・ 粛で止まって立った ・ 投げる前に弾けた | **暴発（WS-b）**（乾いていた）・ 暴発の連鎖の駒 /回 | 残った獣（倒された ／ 弾けた ／ 戦の終わりまで）・ 立っていたT | 立った獣への一撃 | 投げた ・ 感電が付いた ・ 糸 | 帯電していた割合（T1 ／ T2 ／ T3 ／ T4〜） | 獣の死 数えた（場 ／ 緊急 ／ 立ち ／ 針 ／ 共鳴 ／ 暴発） |");
        Console.WriteLine("|---|---|---|---|---|---|--:|---|---|---|");
        foreach (var (n, _) in Boards())
            foreach (var v in vers)
            {
                var a = new S318.St(); var x = new X();
                foreach (var c in cells.Where(q => q.B == n && q.V == v)) { a.Merge(c.S); x.Merge(c.X); }
                long nn = a.N / Waves.Length;
                var xx = new X(); xx.Merge(x); xx.N = nn;
                string bf = x.Backfire == 0 ? "—" : $"**{Per1(x.Backfire, nn)}**（{Per1(x.BackfireDry, nn)}）・ {Per2(x.BackfireUnits, x.Backfire - x.BackfireDry)}";
                string bd = a.BdCounted == 0 ? "—" : $"{Per1(a.BdCounted, nn)}（{string.Join(" ／ ", Enumerable.Range(0, 6).Select(i => i < a.BdKind.Length ? Per1(a.BdKind[i], nn) : Per1(BdKind5(cells, n, v), nn)))}）";
                Console.WriteLine($"| {n} | {v.Name} | {Per1(a.Bursts, nn)} | {Fate(xx)} | {bf} | {After(xx)} | {Per1(a.AtkOnStand, nn)} | {Per1(a.Thrown, nn)} ・ {Per1(a.ThrowShocked, nn)} ・ {Per1(a.ThrowThread, nn)} | {TurnSplit(x.ChargedT, x.ThrowT, nn, true)} | {bd} |");
            }

        foreach (var w in Waves)
        {
            Console.WriteLine();
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 勝率 | 寿命 | ソム 倒れたT | 立った ・ 跳ね返り ・ 打ち返し ・ 誰も受け止めず ・ 粛 | 暴発 | 残った獣（倒された ／ 弾けた ／ 終わりまで）・ 立っていたT | 立った獣への一撃 | 投げた ・ 帯電していた割合 | 打ち返し（粛 ／ 痺れほか） | CR-b 走った敵 ／ 新しく感電 | ハギ 追い打ち（1T の最多）・ 倒れた ・ ツギ 瓦礫 | シガ 手番 ／ 失った | 味方の感電 弾けた ／ 痺れた | 光 ・ 衣 |"
                + (w.Boss ? " 味方の与ダメ /T ・ 勇者の回復 /T ・ 差 |" : ""));
            Console.WriteLine("|---|---|--:|---|---|---|--:|---|--:|---|---|---|---|---|---|---|" + (w.Boss ? "---|" : ""));
            foreach (var c in cells.Where(q => q.W == w))
            {
                var a = c.S; var x = c.X;
                string hg = (a.HagiN > 0 ? $"{Per1(a.HagiAtk, a.N)}（{a.HagiTopTurn}）・ {Pct(a.HagiDied, a.N)}" : "—") + (a.TsugiN > 0 ? $" ・ ツギ {Per1(a.TsugiFalls, a.TsugiN)}" : "");
                long all = 0, ch = 0; for (int i = 0; i < 9; i++) { all += x.ThrowT[i]; ch += x.ChargedT[i]; }
                Console.WriteLine($"| {c.B} | {c.V.Name} | {F1(a.Win)} | {Dt(a.FirstDeathN, a.FirstDeathT, a.N)} | {Dt(a.SomDied, a.SomDeathT, a.N)} | {Per1(x.Stood, x.N)} ・ {Per1(x.Res, x.N)} ・ {Per1(x.Swing, x.N)} ・ {Per1(x.Unc, x.N)} ・ {Per1(x.Hushed, x.N)} | {(x.Backfire == 0 ? "—" : Per1(x.Backfire, x.N))} | {After(x)} | {Per1(a.AtkOnStand, a.N)} | {Per1(a.Thrown, a.N)} ・ {Pct(ch, all)} | "
                    + $"{(a.SgFired + a.SgHushed + a.SgBlocked + a.SgNested == 0 ? "—" : $"{Per1(a.SgFired, a.N)}（{Per1(a.SgHushed, a.N)} ／ {Per1(a.SgBlocked + a.SgNested, a.N)}）")} | {(a.ArcFoes == 0 ? "—" : $"{Per1(a.ArcFoes, a.N)} ／ {Per1(a.ArcShocked, a.N)}")} | {hg} | {(a.ShigaN > 0 ? $"{Per1(a.ShigaTurns, a.ShigaN)} ／ {Per1(a.ShigaStalls, a.ShigaN)}" : "—")} | {Per1(a.AllyPops, a.N)} ／ {Per1(a.AllyStuns, a.N)} | {Per1(a.Lights, a.N)} ・ {Per0(a.VeilAdded, a.N)} |"
                    + (w.Boss ? $" {Per0(a.FoeDmgAll, a.Turns)} ・ {Per0(a.FoeHeal, a.Turns)} ・ {Sg((double)(a.FoeDmgAll - a.FoeHeal) / Math.Max(1, a.Turns))} |" : ""));
            }
        }

        Console.WriteLine();
        Console.WriteLine("## §5-4 格子の門（代表台で規定から落ちたセル・−3.0pt 以上を数える。「いちばん良い版」＝ 落ちるセルがいちばん少ない版）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 規定から −3.0pt 以上落ちたセル | 規定から +3.0pt 以上上がったセル | ボス ／ 近衛 ／ 大隊 で ±5pt 以上動いたセル |");
        Console.WriteLine("|---|--:|--:|---|");
        foreach (var v in vers.Skip(1))
        {
            int down = 0, up = 0; var big = new List<string>();
            foreach (var c in cells.Where(q => q.V == v))
            {
                double d = c.S.Win - Cell(c.B, bv, c.W).S.Win;
                if (d <= -3) down++; if (d >= 3) up++;
                if (Math.Abs(d) >= 5 && (c.W.Boss || c.W.Name is "近衛" or "大隊")) big.Add($"{c.B} × {c.W.Name}（{Sg(d)}）");
            }
            Console.WriteLine($"| {v.Name} | {down} | {up} | {(big.Count == 0 ? "なし" : string.Join("・", big))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>獣の死の種類 5（投げて暴発）は第318期の計数（`S318.St.BdKind` は 5 枠）に入らないので、暴発の数（`X.Backfire`・帯電の有無を問わず数える＝獣の死の数と同じ）で出す。</summary>
    static long BdKind5(List<(string B, Formation F0, S287.Wave W, Ver V, S318.St S, X X)> cells, string b, Ver v) =>
        cells.Where(q => q.B == b && q.V == v).Sum(q => q.X.Backfire);

    // ---------------------------------------------------------------------------------
    // §5-3 `compare` × 版（基準は規定）
    // ---------------------------------------------------------------------------------
    static void CompareAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = Pick(only);
        var rows = CompareBuildsH319().ToArray();   // 第320期: 第319期の規定（CR-b・`SomH319`）に固定
        int nw = EnemyCatalog.Stages.Count;
        var grids = vers.Select(v =>
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = Apply(rows[ri].F, v);
                int wins = 0;
                for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / Seeds;
            });
            return g;
        }).ToArray();
        Console.WriteLine($"# 第319期 §5-3 `compare` {rows.Length} 行 × 版（seed 0..{Seeds - 1}・基準は規定 ＝ CR-b ＋ SG）");
        double M(double[,] g, int ri) => Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
        for (int vi = 1; vi < vers.Length; vi++)
        {
            var v = vers[vi]; var g = grids[vi]; var b = grids[0];
            Console.WriteLine();
            Console.WriteLine($"## {v.Name}");
            Console.WriteLine();
            Console.WriteLine("| 行 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | 差 | 最大の落ち |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|--:|");
            int movedRows = 0, movedCells = 0, outside = 0;
            var drops = new List<int>();
            for (int ri = 0; ri < rows.Length; ri++)
            {
                bool any = Enumerable.Range(0, nw).Any(w => g[ri, w] != b[ri, w]);
                bool holds = rows[ri].F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.SomH319));
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
            Console.WriteLine($"動いた行 {movedRows} ／ セル {movedCells}・ソムがいない行で動いたセル **{outside}**・全{rows.Length}行の第2〜5波平均 {F1(allB)} → {F1(all)}（{Sg(all - allB)}）・主判定19行の第五波 {F1(p5b)} → {F1(p5)}（歯止め {F1(Baseline.PrimaryFifthFloor)}）");
            if (drops.Count == 0) { Console.WriteLine("(G2): −10.0pt 以上落ちた行なし。"); continue; }
            foreach (int ri in drops)
            {
                var parts = new List<string>(); bool broken = false;
                foreach (var d in rows[ri].F.Occupied().Select(o => o.Def).Distinct())
                {
                    var others = Enumerable.Range(0, rows.Length).Where(rj => rj != ri && rows[rj].F.Occupied().Any(o => ReferenceEquals(o.Def, d))).ToList();
                    if (others.Count == 0) { parts.Add($"{Short(d)} 他の行 0"); continue; }
                    double ch = others.Average(rj => M(g, rj) - M(b, rj));
                    if (ch <= -3.0) broken = true;
                    parts.Add($"{Short(d)} {Sg(ch)}（{others.Count} 行）");
                }
                Console.WriteLine($"- (G2) {rows[ri].Name}: {string.Join("・", parts)} → **{(broken ? "壊れ" : "編成上の制約")}**");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §5-4 ソムの格子（第311期と同じ作り・駒は規定。版のシガは候補のシガに当てる）
    // ---------------------------------------------------------------------------------
    static void Grid(string ver, string wave)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var v = V(ver);
        var w = S307.WaveOf(wave);
        UnitDef[] fixedBase = w.Boss ? new[] { UnitCatalog.Kugu, UnitCatalog.Kata } : new[] { UnitCatalog.Tou };
        var pool = (w.Boss ? S307.BossPool : S307.ElitePool).Select(Cur).ToArray();
        int k = w.Boss ? 2 : 3, need = w.Boss ? 1 : 5;
        var lu = S293.Combos(pool, k);
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedBase.Append(UnitCatalog.SomH319).Concat(t).ToArray()));
        var res = new (int Wins, long WinT, int Ctl)?[boards.Count];
        int cutPass = 0;
        static int W(Formation f, S287.Wave w, int n, out long wt)
        {
            int wins = 0; wt = 0;
            for (int s = 0; s < n; s++) { var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false); if (r.PlayerWon) { wins++; wt += r.Turns; } }
            return wins;
        }
        Parallel.For(0, boards.Count, i =>
        {
            var f = Apply(B283.Seat(boards[i]), v);
            if (W(f, w, S307.CutSeeds, out _) < need) return;
            Interlocked.Increment(ref cutPass);
            int wins = W(f, w, Seeds, out long wt);
            int ctl = -1;
            if (wins * 2 >= Seeds) ctl = W(S309.Swap(f, v.Som, UnitCatalog.Dolga), w, Seeds, out _);
            res[i] = (wins, wt, ctl);
        });
        var all = res.Where(r => r is not null).Select(r => r!.Value).ToList();
        int half = all.Count(r => r.Wins * 2 >= Seeds), reach = all.Count(r => r.Wins * 2 >= Seeds && r.Ctl >= 0 && r.Ctl * 2 <= r.Wins), ninety = all.Count(r => r.Wins * 10 >= Seeds * 9);
        Console.WriteLine($"# 第319期 §5-4 ソムの格子 × {w.Name} × 版 {v.Name}（固定枠 {S307.OrderName(fixedBase)} ＋ ソム・探索枠{k}・{boards.Count:N0} 台）");
        Console.WriteLine();
        Console.WriteLine($"足切り seed 0..{S307.CutSeeds - 1} で {need} 勝以上（{cutPass:N0} 台）→ seed 0..{Seeds - 1} → 勝率 50% 以上でソム → ドルガ。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率 ≧ 50% | **届いた（ソム → ドルガで半分以下）** | 勝率 ≧ 90% | 勝率 ＞ 0% | 最大の勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        Console.WriteLine($"| {v.Name} | {half:N0} | {reach:N0} | {ninety:N0} | {all.Count(r => r.Wins > 0):N0} | {(all.Count == 0 ? "—" : F1(100.0 * all.Max(r => r.Wins) / Seeds))} |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 別の seed の帯 ／ 1戦の並び
    // ---------------------------------------------------------------------------------
    static (string Name, Formation F) BoardOf(string part) => Boards().First(b => b.Name.Contains(part, StringComparison.Ordinal) || (part == "shield" && b.Name == "光の盾") || (part == "hagi" && b.Name == S318.HagiBoard)
        || (part == "shock" && b.Name == "感電 行") || (part == "win" && b.Name.StartsWith("勝ち台", StringComparison.Ordinal)) || (part == "fire" && b.Name.StartsWith("火の型", StringComparison.Ordinal)));

    /// <summary>別の seed の帯（<paramref name="from"/> から <paramref name="n"/> 戦）で、旧 ／ 規定 ／ 版の勝率を並べる。</summary>
    static void Noise(string ver, string part, string wave, int from, int n)
    {
        var v = V(ver);
        var (name, f0) = BoardOf(part);
        var w = WaveOf(wave);
        int o = WinsLite(Apply(f0, V("old")), w, n, from), b = WinsLite(f0, w, n, from), a = WinsLite(Apply(f0, v), w, n, from);
        Console.WriteLine($"| {name} | {w.Name} | seed {from}..{from + n - 1} | 旧 {F1(100.0 * o / n)} | 規定 {F1(100.0 * b / n)} | {v.Name} {F1(100.0 * a / n)} | 規定との差 {Sg(100.0 * (a - b) / n)} | 旧との差 {Sg(100.0 * (a - o) / n)} |");
    }

    static void Memo(string ver, string part, string wave, int seed, int t0, int t1)
    {
        var v = V(ver);
        var (name, f0) = BoardOf(part);
        var f = Apply(f0, v);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        foreach (var ev in r.Events.Where(x => x.Kind is BattleEventKind.Summon or BattleEventKind.SilkBall && x.TargetId is int)) names.TryAdd(ev.TargetId!.Value, ev.Kind == BattleEventKind.SilkBall ? "糸玉" : "背いた獣");
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# som319 memo —— {name}（{v.Name}・{Seats(f)}）× {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}・T{t0}〜T{t1}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        for (int i = 0; i < r.Events.Count; i++)
        {
            var x = r.Events[i];
            if (x.Turn < t0 || x.Turn > t1) continue;
            if (x.Kind is BattleEventKind.StatSnapshot or BattleEventKind.StatusSnapshot) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"#{i,-4} {x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  hp={x.HpAfter}  Slot {x.Slot}{(x.PartnerId is int pp ? $"  partner {N(pp)}" : "")}{(x.StatusRemaining is int sr ? $"  rem {sr}" : "")}{(x.Reaction ? "  Reaction" : "")}{(x.FriendlyFire ? "  ff" : "")}");
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査（§7）
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(Formation f, List<UnitState> enemy, int turn = 1)
    {
        var ctx = new BattleContext(0, true);
        var add = typeof(BattleContext).GetMethod("Add", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        foreach (var u in p.Concat(enemy)) add.Invoke(ctx, new object[] { u });
        foreach (var u in ctx.AllUnits) u.SetCounter(StatusKeys.IdleTurn, 99);
        typeof(BattleContext).GetProperty("Turn")!.SetValue(ctx, turn);
        return (ctx, p, enemy);
    }

    internal static UnitTally T(BattleContext ctx, UnitState u)
    {
        var d = (Dictionary<string, UnitTally>)typeof(BattleContext).GetProperty("TallyByUnit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(ctx)!;
        return d.TryGetValue(u.Def.Id, out var t) ? t : new UnitTally();
    }

    static List<UnitState> WaveN(int i) => BattleEngine.Materialize(EnemyCatalog.Stages[i].Enemy, BattleContext.EnemyTeam);
    internal static void TurnStart(BattleContext ctx, UnitState som) { foreach (var t in som.Traits.ToList()) t.OnTurnStart(ctx, som); }
    internal static List<UnitState> Stood(BattleContext ctx, UnitState som) => ctx.AllUnits.Where(u => u.IsAlive && BetrayedTrait.IsFodder(u) && u.RawCounter(BattleContext.StandOwnerKey) == som.InstanceId + 1).ToList();
    internal static List<BattleEvent> AfterEv(BattleContext ctx, int ev0) => ctx.Events.Skip(ev0).ToList();

    /// <summary>前1 に味方1体（狙われるのはその1体）・後3 にソムの版・前の味方以外は HP 半分（光が降る先）。<paramref name="charged"/> なら前の味方を先に帯電させる。</summary>
    internal static (BattleContext Ctx, UnitState Som, UnitState Ally, List<UnitState> E) One(UnitDef ally, UnitDef som, bool charged, int wave = 2, int turn = 5)
    {
        var f = Formation.Build(front1: ally, back3: som);
        var (ctx, p, e) = Board(f, WaveN(wave), turn);
        var s = p.First(u => u.Def.Id == "som"); var a = p.First(u => u != s);
        s.Hp = s.MaxHp / 2;
        if (charged) a.SetCounter(StatusKeys.Shock, 1);
        return (ctx, s, a, e);
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# som319 check —— 第319期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef som = UnitCatalog.SomH319, shiga = UnitCatalog.ShigaH321, wsa = UnitCatalog.SomWSa, wsa3 = UnitCatalog.SomWSa3, wsb = UnitCatalog.SomWSb;

        // (a) 段0: 規定のソム ＝ CR-b・規定のシガ ＝ SG・旧の定義（`SomH318` ／ `ShigaH318`）が残っている・固定の口
        {
            var h = UnitCatalog.SomH318; var hs = UnitCatalog.ShigaH318;
            bool d = som.Traits.SequenceEqual(UnitCatalog.SomCRb.Traits) && som.Traits.SequenceEqual(h.Traits.Concat(new[] { TraitId.BeastStand, TraitId.BeastBetray, TraitId.BeastResonance, TraitId.BeastResonanceArc }))
                && h.Traits.SequenceEqual(UnitCatalog.SomH317.Traits.Append(TraitId.BeastDeathCount)) && h.PlusText == UnitCatalog.SomH317.PlusText + UnitCatalog.SomBeastDeathText318
                && som.MinusText == "喚ばれたものは背いて敵につき、こちらに雷を投げる。隣の敵が帯電していれば、纏った雷が暴発する"
                && som.PlusText.EndsWith("。帯電した仲間に落ちた背きの雷は共鳴して跳ね返り、獣のまわりの敵にも走ってから獣を弾けさせる", StringComparison.Ordinal)
                && som.MaxHp == h.MaxHp && som.Attack == h.Attack && som.Speed == h.Speed && ReferenceEquals(som.Actions, h.Actions)
                && UnitCatalog.SomCRa.Traits.SequenceEqual(h.Traits.Concat(new[] { TraitId.BeastStand, TraitId.BeastBetray, TraitId.BeastResonance })) && UnitCatalog.SomBT318.Traits.SequenceEqual(h.Traits.Concat(new[] { TraitId.BeastStand, TraitId.BeastBetray }));
            bool ds = shiga.Traits.SequenceEqual(UnitCatalog.ShigaSG.Traits) && shiga.Traits.SequenceEqual(hs.Traits.Append(TraitId.BetrayBackswing))
                && hs.Traits.SequenceEqual(UnitCatalog.ShigaSWa.Traits.Concat(new[] { TraitId.FearNoStun, TraitId.ScourgeDaunt }))
                && shiga.PlusText == hs.PlusText + "。背いた獣の雷を浴びると、その場で打ち返す" && shiga.MinusText == hs.MinusText && shiga.MaxHp == 52 && shiga.Attack == 9 && shiga.Speed == 3;
            bool roster = UnitCatalog.All.Contains(UnitCatalog.Som) && ReferenceEquals(PinS319(UnitCatalog.Som), som) && UnitCatalog.All.Contains(UnitCatalog.Shiga) && ReferenceEquals(PinS319(UnitCatalog.Shiga), shiga) && UnitCatalog.All.Count == 52   // 第320期: 規定は WS-b・`som` は第319期の規定（`SomH319`）
                && new[] { h, hs, UnitCatalog.SomH317, wsa, wsa3, wsb, UnitCatalog.SomCRb, UnitCatalog.ShigaSG }.All(x => !UnitCatalog.Everyone.Contains(x));
            bool pin = ReferenceEquals(PinSS318(som), h) && ReferenceEquals(PinSS318(UnitCatalog.Shiga), hs)
                && ReferenceEquals(PinS317(som), UnitCatalog.SomH317) && ReferenceEquals(PinS317(h), UnitCatalog.SomH317) && ReferenceEquals(PinS317(UnitCatalog.Shiga), hs)
                && ReferenceEquals(PinDS316(UnitCatalog.Shiga), UnitCatalog.ShigaSWa) && ReferenceEquals(PinDS316(som), UnitCatalog.SomH317) && IsSomNow(h)
                && Pin318(Presets.Playtest.First(r => r.Name == S309.Shield).F).Occupied().Any(o => ReferenceEquals(o.Def, h))
                && CompareBuildsH318().First(r => r.Name == "感電 (シガ×カタ×ソム)").F.Occupied().Any(o => ReferenceEquals(o.Def, hs));
            Expect("(a) 段0: 規定のソム ＝ CR-b（札は第318期の `SomCRb` と同じ・文面は叩き台）・規定のシガ ＝ SG（札は `ShigaSG` と同じ）・旧の定義 `SomH318` ／ `ShigaH318` が `Everyone` の外に残り、第318期の版はそこから作る・`Pin318` が旧へ、`PinS317` ／ `PinDS316` がそれより前の旧へ替える",
                d && ds && roster && pin, $"ソム {d}・シガ {ds}・ロスター {roster}・固定 {pin}");
        }
        // (b) 段0: 規定の挙動（立つ ／ 投げる ／ 共鳴返し ／ 打ち返し）が第318期の版と同じ
        {
            var (c1, s1, g1, _) = One(UnitCatalog.Golm, som, charged: true);
            TurnStart(c1, s1);
            bool res = T(c1, s1).CrFired == 1 && Stood(c1, s1).Count == 0;
            var (c2, s2, sh2, _) = One(shiga, som, charged: false);
            TurnStart(c2, s2);
            bool sg = T(c2, s2).SgFired == 1 && Stood(c2, s2).Count == 0;
            var (c3, s3, g3, _) = One(UnitCatalog.Golm, som, charged: false);
            TurnStart(c3, s3);
            bool stand = Stood(c3, s3).Count == 1 && T(c3, s3).BtShocked == 1 && g3.RawCounter(StatusKeys.Shock) > 0;
            Expect("(b) 段0: 規定のソムは立って投げる・帯電した味方なら共鳴返し・帯電していないシガは打ち返す・帯電していないゴルムには感電が付き獣は立つ", res && sg && stand, $"共鳴 {res}・打ち返し {sg}・立つ {stand}");
        }
        // (c) WS-a: T1〜T2 は獣が立たず湧く席で弾ける（投げない）・T3 から立つ。WS-a3 は T3 も立たず T4 から立つ
        {
            (int Stood, int Throws, int Bursts) At(UnitDef d, int turn)
            {
                var (ctx, s, _, _) = One(UnitCatalog.Golm, d, charged: false, turn: turn);
                int ev0 = ctx.Events.Count;
                TurnStart(ctx, s);
                var ev = AfterEv(ctx, ev0);
                return (Stood(ctx, s).Count, ev.Count(x => x.Kind == BattleEventKind.BeastThrow), ev.Count(x => x.Kind == BattleEventKind.BeastBurst && x.Text is BeastBurstLabels.Always or BeastBurstLabels.Swarm));
            }
            var a1 = At(wsa, 1); var a2 = At(wsa, 2); var a3 = At(wsa, 3);
            var b3 = At(wsa3, 3); var b4 = At(wsa3, 4);
            var base1 = At(som, 1);
            bool ok = a1 == (0, 0, 1) && a2 == (0, 0, 1) && a3.Stood == 1 && a3.Throws == 1 && b3 == (0, 0, 1) && b4.Stood == 1 && b4.Throws == 1 && base1.Stood == 1;
            Expect("(c) WS-a: T1 ／ T2 の喚び出しは獣が立たず湧く席で弾ける（`BeastBurst 弾ける`・投げない）・T3 から立って投げる・WS-a3 は T3 も弾けて T4 から立つ・規定は T1 から立つ",
                ok, $"WS-a T1 {a1}・T2 {a2}・T3 {a3}・WS-a3 T3 {b3}・T4 {b4}・規定 T1 {base1}（立った ／ 投げた ／ 弾けた）");
        }
        // (d) WS-b: 受け止められなかった獣は投げた直後に暴発する（起こし手 ＝ ソム・光・獣の死）
        {
            var (ctx, s, g, _) = One(UnitCatalog.Golm, wsb, charged: false);
            int ev0 = ctx.Events.Count;
            TurnStart(ctx, s);
            var ev = AfterEv(ctx, ev0);
            var hit = ev.FirstOrDefault(x => x.Kind == BattleEventKind.BeastThrow && x.Text == BeastThrowLabels.Hit);
            int? beast = hit?.ActorId;
            int iHit = hit is null ? -1 : ev.IndexOf(hit);
            int iGain = ev.FindIndex(x => x.Kind == BattleEventKind.StatusGain && x.Text == StatusKeys.Shock && x.TargetId == g.InstanceId);
            int iBf = ev.FindIndex(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Backfire && x.TargetId == beast && x.ActorId == s.InstanceId);
            int iSpent = ev.FindIndex(x => x.Kind == BattleEventKind.ShockSpent && x.TargetId == beast && x.ActorId == s.InstanceId);
            var tl = T(ctx, s);
            bool d1 = hit is not null && hit.Amount == 1 && iGain > iHit && iBf > iGain && iSpent > iBf && g.RawCounter(StatusKeys.Shock) > 0
                && ev.Any(x => x.Kind == BattleEventKind.Spark && x.Text == SparkLabels.Release) && !ev.Any(x => x.Kind == BattleEventKind.Death && x.TargetId == beast)
                && Stood(ctx, s).Count == 0 && tl.WsBackfire == 1 && (tl.BdKind?[5] ?? 0) == 1;
            Expect("(d) WS-b: 帯電していない味方に雷が付いた（`BeastThrow 投げた` → 味方の `StatusGain 感電`）直後に `BeastBurst 投げて暴発` → 獣の `ShockSpent`（起こし手 ＝ ソム）・光が降る・`Death` なし・獣の死に数える（種類 5）・獣は盤に残らない",
                d1, $"並び {iHit} → {iGain} → {iBf} → {iSpent}・暴発 {tl.WsBackfire}・獣の死(5) {tl.BdKind?[5] ?? 0}");
        }
        // (e) WS-b: 跳ね返り ／ 打ち返しの獣は暴発しない（二重に弾けない）・粛で止まった共鳴返しの獣は暴発する・クグの糸へ流れた獣も暴発する
        {
            var (c1, s1, _, _) = One(UnitCatalog.Golm, wsb, charged: true);
            int e1 = c1.Events.Count; TurnStart(c1, s1);
            bool res = T(c1, s1).CrFired == 1 && T(c1, s1).WsBackfire == 0 && !AfterEv(c1, e1).Any(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Backfire);
            var (c2, s2, _, _) = One(shiga, wsb, charged: false);
            TurnStart(c2, s2);
            bool sg = T(c2, s2).SgFired == 1 && T(c2, s2).WsBackfire == 0 && Stood(c2, s2).Count == 0;
            var (c3, s3, g3, _) = One(UnitCatalog.Golm, wsb, charged: true, wave: 1);
            bool hushOn = c3.HushBindingNow;
            int e3 = c3.Events.Count; TurnStart(c3, s3);
            var ev3 = AfterEv(c3, e3);
            bool hush = hushOn && T(c3, s3).CrHushed == 1 && T(c3, s3).WsBackfire == 1 && Stood(c3, s3).Count == 0
                && ev3.FindIndex(x => x.Kind == BattleEventKind.BeastThrow && x.Text == BeastThrowLabels.Hushed) < ev3.FindIndex(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Backfire);
            // クグ: 帯電していないクグ（糸の先が生きている）→ 糸へ流れてから暴発
            var (c4, s4, k4, e4) = One(UnitCatalog.Kugu, wsb, charged: false);
            var foe = e4.First(u => u.IsAlive && FormationRules.RowOf(u.Slot) == Row.Front);
            k4.SetCounter(GrappleTrait.TargetKey, foe.InstanceId + 1);
            int q4 = c4.Events.Count; TurnStart(c4, s4);
            var ev4 = AfterEv(c4, q4);
            bool thread = ev4.Any(x => x.Kind == BattleEventKind.BeastThrow && x.Text == BeastThrowLabels.Thread) && T(c4, s4).WsBackfire == 1 && Stood(c4, s4).Count == 0
                && ev4.FindIndex(x => x.Kind == BattleEventKind.BeastThrow && x.Text == BeastThrowLabels.Thread) < ev4.FindIndex(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Backfire);
            // 規定（WS-b なし）は暴発しない
            var (c5, s5, _, _) = One(UnitCatalog.Golm, som, charged: false);
            TurnStart(c5, s5);
            bool none = T(c5, s5).WsBackfire == 0 && Stood(c5, s5).Count == 1;
            Expect("(e) WS-b: 跳ね返り ／ 打ち返しの獣は暴発しない（二重に弾けない）・粛で止まった共鳴返しの獣は `共鳴が止まった` の後に暴発する・クグの糸へ流れた獣も `糸` の後に暴発する・規定は暴発しない",
                res && sg && hush && thread && none, $"跳ね返り {res}・打ち返し {sg}・粛 {hush}（粛が効いていた {hushOn}）・糸 {thread}・規定 {none}");
        }
        // (f) WS-b: 実戦で立ったまま手番を越える獣が 0（代表台 7 × 10 波 × seed 0..19）・群れは1体ずつ「投げる → 受け止め ／ 暴発」
        {
            long stoodOver = 0, n = 0, backfire = 0, multi = 0, interleaved = 0;
            foreach (var (_, f0) in Boards())
                foreach (var w in Waves)
                    for (int sd = 0; sd < 20; sd++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(Apply(f0, V("WS-b")), BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        n++;
                        var stood = new Dictionary<int, int>();
                        var som0 = r.Events.FirstOrDefault(x => x.Kind == BattleEventKind.Summon && x.Text == UnitCatalog.Fodder.Name)?.ActorId;
                        var phantom = new HashSet<int>(r.Events.Where(x => x.Kind == BattleEventKind.BeastBurst && x.Text is BeastBurstLabels.Always or BeastBurstLabels.Charged or BeastBurstLabels.Swarm or BeastBurstLabels.Emergency && x.TargetId is int).Select(x => x.TargetId!.Value));
                        var gone = new Dictionary<int, int>();
                        foreach (var x in r.Events)
                        {
                            if (x.Kind == BattleEventKind.Summon && x.Text == UnitCatalog.Fodder.Name && x.TargetId is int sid && !phantom.Contains(sid)) stood[sid] = x.Turn;
                            else if ((x.Kind == BattleEventKind.BeastBurst || x.Kind == BattleEventKind.Death) && x.TargetId is int g && stood.ContainsKey(g) && !gone.ContainsKey(g)) gone[g] = x.Turn;
                            if (x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Backfire) backfire++;
                        }
                        foreach (var (id, t0) in stood) if (!gone.TryGetValue(id, out int t1) ? r.Turns > t0 : t1 > t0) stoodOver++;
                        // 群れ: 同じ手番に2体以上立ったとき、2体目の投げる前に1体目が盤から消えている
                        foreach (var grp in stood.GroupBy(q => q.Value).Where(q => q.Count() > 1))
                        {
                            multi++;
                            var ids = grp.Select(q => q.Key).ToList();
                            var evs = r.Events.ToList();
                            int iGone0 = evs.FindIndex(x => (x.Kind is BattleEventKind.BeastBurst or BattleEventKind.Death) && x.TargetId == ids[0]);
                            int iThrow1 = evs.FindIndex(x => x.Kind == BattleEventKind.BeastThrow && x.ActorId == ids[1]);
                            if (iThrow1 < 0 || iGone0 < iThrow1) interleaved++;
                        }
                    }
            Expect("(f) WS-b: 実戦（代表台 7 × 10 波 × seed 0..19）で、立った獣が立った手番を越えて盤に残ったことが 0 回・同じ手番に2体以上立ったら1体目は2体目が投げる前に盤から消えている",
                stoodOver == 0 && backfire > 0 && interleaved == multi, $"手番を越えた {stoodOver}・暴発 {backfire}・群れの手番 {multi}（1体ずつ {interleaved}）・{n} 戦");
        }
        // (g) 決定的・verbose の有無で勝敗と決着T が同じ
        {
            int nd = 0, n = 0;
            foreach (var v in Vers)
                foreach (var (_, f0) in Boards())
                    foreach (var w in Waves)
                        for (int sd = 0; sd < 2; sd++)
                        {
                            var f = Apply(f0, v);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            n++;
                            if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) nd++;
                        }
            Expect($"(g) 決定的・verbose の有無で勝敗と決着T が変わらない（{Vers.Length} 版 × 代表台 {Boards().Length} × {Waves.Length} 波 × seed 0..1）", nd == 0, $"{nd} ／ {n} 件");
        }
        // (h) 版の定義・規定のほかの駒は動かない
        {
            bool defs = wsa.Traits.SequenceEqual(som.Traits.Append(TraitId.BeastStandLate)) && wsa3.Traits.SequenceEqual(som.Traits.Append(TraitId.BeastStandLate3)) && wsb.Traits.SequenceEqual(som.Traits.Append(TraitId.BeastThrowBackfire))
                && wsb.MinusText == som.MinusText + "。受け止める者がいなければ、獣は投げた雷の残りで暴発する" && wsa.PlusText == som.PlusText
                && BetrayedTrait.StandLateTurns == 2 && BetrayedTrait.StandLateTurns3 == 3;
            bool dflt = !UnitCatalog.Kata.Traits.Contains(TraitId.LightningRod) && UnitCatalog.Fodder.MaxHp == 12 && UnitCatalog.Fodder.Attack == 0 && UnitCatalog.Fodder.Speed == 1
                && !som.Traits.Any(t => t is TraitId.BeastStandLate or TraitId.BeastStandLate3 or TraitId.BeastThrowBackfire)
                && UnitCatalog.Everyone.All(d => !d.Traits.Any(t => t is TraitId.BeastStandLate or TraitId.BeastStandLate3) && (ReferenceEquals(d, UnitCatalog.Som) || !d.Traits.Contains(TraitId.BeastThrowBackfire)));   // 第320期: `BeastThrowBackfire` は規定のソムが持つ
            Expect("(h) 版の定義（WS-a ＝ 規定 ＋ `BeastStandLate`・WS-a3 ＝ 規定 ＋ `BeastStandLate3`・WS-b ＝ 規定 ＋ `BeastThrowBackfire`・文面）・新しい札の保持者は `Everyone` に 0 枚・獣とカタは動かない",
                defs && dflt, $"版 {defs}・規定 {dflt}");
        }
        // (i) 乱数
        {
            static int Count(string text, string pat) { int n = 0, k = 0; while ((k = text.IndexOf(pat, k, StringComparison.Ordinal)) >= 0) { n++; k += pat.Length; } return n; }
            static string? Head(string path)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", $"show ff8be37:{path}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
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
                if (head is null) { clean = false; notes.Add($"{path}: 第318期の版を読めない"); continue; }
                foreach (var pat in new[] { "PickOne(", "Roll(" }) { int a0 = Count(head, pat), a1 = Count(now, pat); if (a1 > a0) clean = false; notes.Add($"{Path.GetFileName(path)} {pat} {a0}→{a1}"); }
            }
            Expect("(i) `PickOne(` ／ `Roll(` を新たに使っていない（第318期のコミット ff8be37 と比べる）", clean, string.Join("・", notes));
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
