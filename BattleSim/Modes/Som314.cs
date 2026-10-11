using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S293 = Shock293Diag;
using S307 = Som307Diag;
using S309 = Som309Diag;
using S310 = Som310Diag;

// =====================================================================================
// som314 —— 第314期「背いた獣が牙を剥いて立つ版（SB-a ／ SB-b）」。
// 指示書は design/PHASE314_SOM_STANCE_SPEC.md ／ 報告は design/PHASE314_SOM_STANCE.md。
//
//     dotnet run --project BattleSim -c Release 0 som314 p0          # Phase 0（§3）: 規定（E2）のまま、SB-a なら獣ごとにどうなったか（計数器 `StandCensus`）・空いた召喚枠・いまの弾けの回数
//     dotnet run --project BattleSim -c Release 0 som314 boards [版,…]  # §4-2: 代表台 7 × 10 波 × 版（規定 ／ SB-a ／ SB-b）
//     dotnet run --project BattleSim -c Release 0 som314 compare     # §4-3: `compare` 64 行 × 版
//     dotnet run --project BattleSim -c Release 0 som314 swap        # §4-4: ソム ↔ ツギ（第309期と同じ台を規定の駒で組み直す）× 版
//     dotnet run --project BattleSim -c Release 0 som314 grid <版> <boss|guard|bat>   # §4-4: ソムの格子（第311期と同じ作り・駒は規定）
//     dotnet run --project BattleSim -c Release 0 som314 find        # §5: 立った獣 ／ 暴発のある seed（台本の例）
//     dotnet run --project BattleSim -c Release 0 som314 hole        # 雷の代わりの一撃が、雷を纏って立つ獣に落ちていた回数（第309〜310期の規定のソム `SomH310`・計数のみ）
//     dotnet run --project BattleSim -c Release 0 som314 memo <版> <台の一部> <boss|guard|bat|nine|nine2|1..5> <seed> <T0> <T1>
//     dotnet run --project BattleSim -c Release 0 som314 check       # 自己検査（§6）
//
// **予測のファイルが無ければ本測定（boards ／ compare ／ swap ／ grid）を走らせない**（R397・`.tmp/p314/predict.md`）。
// 台の駒は第314期の規定のまま（固定していない）——規定が動いたら、この器具は第314期の規定に固定し直すこと。
// =====================================================================================
static class Som314Diag
{
    const string PredictFile = ".tmp/p314/predict.md";
    const int Seeds = S307.Seeds;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" or "swap" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"som314: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": BoardsAll(A(3, "")); return;
            case "compare": CompareAll(); return;
            case "swap": SwapAll(); return;
            case "grid": Grid(A(3, "SB-a"), A(4, "boss")); return;
            case "find": Find(); return;
            case "hole": Hole(); return;
            case "memo": Memo(A(3, "SB-a"), A(4, "光の盾"), A(5, "boss"), int.Parse(A(6, "0")), int.Parse(A(7, "1")), int.Parse(A(8, "5"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("som314: モードは p0 / boards / compare / swap / grid / find / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);
    static string F1(double x) => S307.F1(x);
    static string Per1(long a, long n) => S307.Per1(a, n);
    static string Per0(long a, long n) => S307.Per0(a, n);
    static string Pct(long a, long n) => S307.Pct(a, n);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Dt(long died, long t, long n) => died == 0 ? "—" : $"{Per1(t, died)}（{Pct(died, n)}）";

    internal sealed record Ver(string Name, UnitDef Som);
    internal static readonly Ver[] Vers =
    {
        new("規定", UnitCatalog.Som),
        new("SB-a", UnitCatalog.SomSBa),
        new("SB-b", UnitCatalog.SomSBb),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || (n == "base" && v.Name == "規定"));   // "base" ＝ 規定（コンソールの文字コードを避ける別名）
    internal static Formation Apply(Formation f, Ver v) => ReferenceEquals(v.Som, UnitCatalog.Som) ? f : S309.Swap(f, UnitCatalog.Som, v.Som);

    internal static readonly S287.Wave[] Waves = S310.Waves;
    static S287.Wave WaveOf(string n) => n switch { "nine" => Waves[3], "nine2" => Waves[4], _ => S307.WaveOf(n) };

    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation RowF(string n) => CompareBuilds().First(r => r.Name == n).F;
    static Formation WinSeat() => B283.Seat(S307.Order(S307.WinBoard));

    /// <summary>§4-2 の代表台（規定の駒・席は出典のまま）。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("光の盾", Playtest(S309.Shield)),
        ("光の盾 重", Playtest(S309.ShieldHeavy)),
        ("光の盾 鞭", Playtest(Som312Diag.Whip)),
        ("光の盾 重 鞭", Playtest(Som312Diag.WhipHeavy)),
        ("感電 行", RowF("感電 (シガ×カタ×ソム)")),
        ("勝ち台 ツギ→ソム", S309.Swap(WinSeat(), UnitCatalog.Tsugi, UnitCatalog.Som)),
        ("火の型 ソラ→ソム", S309.Swap(Playtest("試遊・感電 火の型"), UnitCatalog.Sora, UnitCatalog.Som)),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose の台本 ＋ 帳簿）
    // ---------------------------------------------------------------------------------
    internal sealed class St
    {
        public long N, Wins, WinT, LoseT, Caps, Turns, FirstDeathN, FirstDeathT, SomDied, SomDeathT;
        public Dictionary<string, long> FoeDmg = new();
        public long FoeHeal;
        // 獣（帳簿）
        public long Called, Charged, NoSeat, NoFoe, Stood, Overflow, OverflowDry, AliveSum, AliveN, Bursts, BurstUnits, EmergFired;
        public int AlivePeak;                                   // 戦をまたいだ最大
        public long PeakSum;                                    // 戦ごとの最大の合計（平均を出す）
        public long[] SwarmHist = new long[6];
        // 獣（台本）
        public long StandLife, StandLifeN, AtkOnStand, DmgOnStand, StandPopped, StandKilled, ThunderOnStand, StandAtEnd;
        public Dictionary<string, long> Poppers = new(), Killers = new();
        // シガ・カタ・光
        public long ShigaOwn, ShigaOwnBeast, ShigaInt, ShigaIntBeast, SwFires, SwMultSum, SwMultN, KataCloud, KataN, Lights, VeilAdded, VeilSoaked;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public long FoeDmgAll => FoeDmg.Values.Sum();
        public void Merge(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LoseT += o.LoseT; Caps += o.Caps; Turns += o.Turns; FirstDeathN += o.FirstDeathN; FirstDeathT += o.FirstDeathT; SomDied += o.SomDied; SomDeathT += o.SomDeathT;
            foreach (var (k, v) in o.FoeDmg) FoeDmg[k] = FoeDmg.GetValueOrDefault(k) + v;
            FoeHeal += o.FoeHeal;
            Called += o.Called; Charged += o.Charged; NoSeat += o.NoSeat; NoFoe += o.NoFoe; Stood += o.Stood; Overflow += o.Overflow; OverflowDry += o.OverflowDry;
            AliveSum += o.AliveSum; AliveN += o.AliveN; Bursts += o.Bursts; BurstUnits += o.BurstUnits; EmergFired += o.EmergFired;
            AlivePeak = Math.Max(AlivePeak, o.AlivePeak); PeakSum += o.PeakSum;
            for (int i = 0; i < 6; i++) SwarmHist[i] += o.SwarmHist[i];
            StandLife += o.StandLife; StandLifeN += o.StandLifeN; AtkOnStand += o.AtkOnStand; DmgOnStand += o.DmgOnStand; StandPopped += o.StandPopped; StandKilled += o.StandKilled; ThunderOnStand += o.ThunderOnStand; StandAtEnd += o.StandAtEnd;
            foreach (var (k, v) in o.Poppers) Poppers[k] = Poppers.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Killers) Killers[k] = Killers.GetValueOrDefault(k) + v;
            ShigaOwn += o.ShigaOwn; ShigaOwnBeast += o.ShigaOwnBeast; ShigaInt += o.ShigaInt; ShigaIntBeast += o.ShigaIntBeast; SwFires += o.SwFires; SwMultSum += o.SwMultSum; SwMultN += o.SwMultN;
            KataCloud += o.KataCloud; KataN += o.KataN; Lights += o.Lights; VeilAdded += o.VeilAdded; VeilSoaked += o.VeilSoaked;
        }
    }

    internal static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var a = new St { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else { a.LoseT = r.Turns; if (r.Turns >= BattleEngine.MaxTurns) a.Caps = 1; }
        var mine = p.ToDictionary(u => u.InstanceId, u => u);
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        UnitState? som = p.FirstOrDefault(u => u.Def.Id == "som"), shiga = p.FirstOrDefault(u => u.Def.Id == "shiga"), kata = p.FirstOrDefault(u => u.Def.Id == "kata");
        string Who(int? id) => id is int i && mine.TryGetValue(i, out var u) ? Short(u.Def) : id is null ? "（出どころなし）" : "敵の側";
        // 1巡目: 喚んだ獣 ／ 置物の獣（その場で弾けた）
        var summoned = new HashSet<int>(); var phantom = new HashSet<int>();
        foreach (var ev in r.Events)
        {
            if (ev.Kind == BattleEventKind.Summon && som is not null && ev.ActorId == som.InstanceId && ev.TargetId is int sid) summoned.Add(sid);
            else if (ev.Kind == BattleEventKind.BeastBurst && ev.Text != BeastBurstLabels.Overflow && ev.TargetId is int bid) phantom.Add(bid);
        }
        var standing = new Dictionary<int, int>();   // 立っている獣 → 立ったT
        bool shigaPending = false;
        int peak = 0;
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Summon when ev.TargetId is int sid && summoned.Contains(sid) && !phantom.Contains(sid):
                    standing[sid] = ev.Turn;
                    if (standing.Count > peak) peak = standing.Count;
                    break;
                case BattleEventKind.BeastBurst when ev.Text == BeastBurstLabels.Overflow && ev.TargetId is int ob && standing.Remove(ob, out int t0o):
                    a.StandLife += ev.Turn - t0o; a.StandLifeN++;
                    break;
                case BattleEventKind.Death when ev.TargetId is int t && mine.ContainsKey(t):
                    if (a.FirstDeathN == 0) { a.FirstDeathN = 1; a.FirstDeathT = ev.Turn; }
                    if (som is not null && t == som.InstanceId && a.SomDied == 0) { a.SomDied = 1; a.SomDeathT = ev.Turn; }
                    break;
                case BattleEventKind.Death when ev.TargetId is int db && standing.Remove(db, out int t0d):
                    a.StandLife += ev.Turn - t0d; a.StandLifeN++; a.StandKilled++;
                    { string k = Who(ev.ActorId); a.Killers[k] = a.Killers.GetValueOrDefault(k) + 1; }
                    break;
                case BattleEventKind.ShockSpent when ev.TargetId is int pb && standing.ContainsKey(pb):
                    a.StandPopped++;
                    { string k = Who(ev.ActorId); a.Poppers[k] = a.Poppers.GetValueOrDefault(k) + 1; }
                    break;
                case BattleEventKind.Thunder when ev.TargetId is int tb && standing.ContainsKey(tb): a.ThunderOnStand++; break;
                case BattleEventKind.ShockGauge when shiga is not null && ev.ActorId == shiga.InstanceId && ev.Text == ShockGaugeLabels.Interrupt: shigaPending = true; break;
                case BattleEventKind.Attack when ev.ActorId is int at && mine.ContainsKey(at):
                    {
                        bool onBeast = ev.TargetId is int tb2 && standing.ContainsKey(tb2);
                        if (onBeast) a.AtkOnStand++;
                        if (shiga is not null && at == shiga.InstanceId)
                        {
                            if (shigaPending) { a.ShigaInt++; if (onBeast) a.ShigaIntBeast++; shigaPending = false; }
                            else { a.ShigaOwn++; if (onBeast) a.ShigaOwnBeast++; }
                        }
                        break;
                    }
                case BattleEventKind.Damage when ev.TargetId is int dt && standing.ContainsKey(dt):
                    if (ev.ActorId is int da && mine.ContainsKey(da)) a.DmgOnStand += ev.Amount;
                    break;
                case BattleEventKind.Damage when ev.TargetId is int ft && foes.Contains(ft):
                    {
                        string k = ev.ActorId is int ka && mine.TryGetValue(ka, out var ku) ? Short(ku.Def) : ev.ActorId is int kb && summoned.Contains(kb) ? "獣の放電" : ev.ActorId is null ? "刻み" : "敵の側";
                        a.FoeDmg[k] = a.FoeDmg.GetValueOrDefault(k) + ev.Amount;
                        break;
                    }
                case BattleEventKind.Heal when ev.TargetId is int ht && foes.Contains(ht): a.FoeHeal += ev.Amount; break;
            }
        }
        foreach (var (_, t0) in standing) { a.StandLife += r.Turns - t0; a.StandLifeN++; a.StandAtEnd++; }
        a.PeakSum = peak;
        if (som is not null && r.TallyByUnit.TryGetValue("som", out var mt))
        {
            a.Called = mt.StandBeasts; a.Charged = mt.StandCharged; a.NoSeat = mt.StandNoSeat; a.NoFoe = mt.StandNoFoe; a.Stood = mt.StandStood; a.Overflow = mt.StandOverflow; a.OverflowDry = mt.StandOverflowDry;
            a.AliveSum = mt.StandAliveSum; a.AliveN = mt.StandAliveN; a.AlivePeak = mt.StandAlivePeak;
            a.Bursts = mt.BeastBursts; a.BurstUnits = mt.BeastBurstUnits; a.EmergFired = mt.EmergFired;
            if (mt.SwarmHist is { } sh) for (int i = 0; i < Math.Min(6, sh.Length); i++) a.SwarmHist[i] = sh[i];
            a.Lights = mt.SparkRainLights; a.VeilAdded = mt.SparkVeilAdded;
        }
        foreach (var u in p) if (r.TallyByUnit.TryGetValue(u.Def.Id, out var ut)) a.VeilSoaked += ut.SparkVeilSoaked;
        if (shiga is not null && r.TallyByUnit.TryGetValue("shiga", out var st)) { a.SwFires = st.SwFires; a.SwMultSum = st.SwMultSum; a.SwMultN = st.SwMultN; }
        if (kata is not null) { a.KataN = 1; a.KataCloud = ThundercloudTrait.Of(kata); }
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
        var order = f.Occupied().Select(o => Short(o.Def)).Distinct().Append("獣の放電").Append("刻み").Append("敵の側");
        return string.Join("・", order.Where(k => a.FoeDmg.GetValueOrDefault(k) > 0).Select(k => $"{k} {Per0(a.FoeDmg[k], a.Turns)}"));
    }

    static string Top(Dictionary<string, long> d, long n, int k = 3) =>
        d.Count == 0 ? "—" : string.Join("・", d.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Take(k).Select(x => $"{x.Key} {Per1(x.Value, n)}"));

    // ---------------------------------------------------------------------------------
    // Phase 0（§3）
    // ---------------------------------------------------------------------------------
    sealed class P0St
    {
        public long N, Wins, Turns, Bursts, BurstUnits, Emerg, SomCalls;
        public long[] Beasts = new long[9], Charged = new long[9], NoSeat = new long[9], Free = new long[10], FreeT = new long[30];
        public void Merge(P0St o)
        {
            N += o.N; Wins += o.Wins; Turns += o.Turns; Bursts += o.Bursts; BurstUnits += o.BurstUnits; Emerg += o.Emerg; SomCalls += o.SomCalls;
            for (int i = 0; i < 9; i++) { Beasts[i] += o.Beasts[i]; Charged[i] += o.Charged[i]; NoSeat[i] += o.NoSeat[i]; }
            for (int i = 0; i < 10; i++) Free[i] += o.Free[i];
            for (int i = 0; i < 30; i++) FreeT[i] += o.FreeT[i];
        }
    }

    static P0St P0Measure(Formation f, S287.Wave w)
    {
        var parts = new P0St[Seeds];
        Parallel.For(0, Seeds, i =>
        {
            var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), i, verbose: false);
            var a = new P0St { N = 1, Turns = r.Turns, Wins = r.PlayerWon ? 1 : 0 };
            if (r.TallyByUnit.TryGetValue("som", out var t))
            {
                a.Bursts = t.BeastBursts; a.BurstUnits = t.BeastBurstUnits; a.Emerg = t.EmergFired; a.SomCalls = t.CensusFreeHist?.Sum() ?? 0;
                void Copy(long[]? src, long[] dst) { if (src is null) return; for (int k = 0; k < Math.Min(src.Length, dst.Length); k++) dst[k] = src[k]; }
                Copy(t.CensusBeasts, a.Beasts); Copy(t.CensusCharged, a.Charged); Copy(t.CensusNoSeat, a.NoSeat); Copy(t.CensusFreeHist, a.Free); Copy(t.CensusFreeT, a.FreeT);
            }
            parts[i] = a;
        });
        var all = new P0St();
        foreach (var x in parts) all.Merge(x);
        return all;
    }

    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        BattleContext.StandCensus = true;
        var cells = new List<(string B, S287.Wave W, P0St S)>();
        try { foreach (var (n, f) in Boards()) foreach (var w in Waves) cells.Add((n, w, P0Measure(f, w))); }
        finally { BattleContext.StandCensus = false; }
        Console.WriteLine($"# 第314期 Phase 0（§3）—— 規定（ソム ＝ E2 ＋ 群れ ＋ 緊急 K-a）のまま・計数器 `StandCensus`・seed 0..{Seeds - 1}");
        Console.WriteLine();
        foreach (var (n, f) in Boards()) Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine();
        Console.WriteLine("計数器は、規定の手番の頭の喚び出し（群れを含む・緊急は含まない）の1回ごとに、SB-a の判定（`BattleContext.PlanBeasts`・獣ごとに空いた召喚枠へ・隣の敵（獣と糸玉を除く）に帯電した駒がいれば弾ける）を**盤面を変えずに**当てて数える。");
        Console.WriteLine("規定では獣は1体も立たないので、SB-a の2回目以降の喚び出しでは「前に立った獣が枠を塞ぐ」ぶんがこの見込みに入らない（§3-2 の空き枠は規定の盤のまま）。");
        Console.WriteLine();
        Console.WriteLine("## §3-1 獣の席の隣に帯電した敵がいた割合（ターンごと・獣1体あたり）と、SB-a で立つ獣の見込み（/喚び出し）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 決着T | T1 | T2 | T3 | T4 | T5 | 全体 | 枠なし | 立つ見込み /喚び出し（T1 ／ T2〜T5 ／ 全体） | 立つ見込み /戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|--:|");
        foreach (var c in cells)
        {
            var s = c.S;
            string T(int t) => s.Beasts[t] == 0 ? "—" : $"{Pct(s.Charged[t], s.Beasts[t]).Replace("%", "")}（{Per1(s.Beasts[t], s.N)}）";
            long all = s.Beasts.Sum(), ch = s.Charged.Sum(), ns = s.NoSeat.Sum();
            long calls1 = s.FreeT.Skip(5).Take(5).Sum(), calls25 = s.FreeT.Skip(10).Take(20).Sum();
            long st1 = s.Beasts[1] - s.Charged[1] - s.NoSeat[1];
            long st25 = Enumerable.Range(2, 4).Sum(t => s.Beasts[t] - s.Charged[t] - s.NoSeat[t]);
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Pct(s.Wins, s.N)} | {Per1(s.Turns, s.N)} | {T(1)} | {T(2)} | {T(3)} | {T(4)} | {T(5)} | {Pct(ch, all)} | {Pct(ns, all)} | {Per1(st1, calls1)} ／ {Per1(st25, calls25)} ／ {Per1(all - ch - ns, s.SomCalls)} | {Per1(all - ch - ns, s.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("（セル ＝ 帯電した敵の隣だった割合 %（その T の獣 /戦）。立つ見込み ＝ 獣 − 隣が帯電 − 枠なし。）");
        Console.WriteLine();
        Console.WriteLine("## §3-2 喚び出しの時点の空いた召喚枠（規定の盤・ターンごと・割合）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | T1 0／1／2／3／4 | T2 | T3 | T4 | T5 | 全体の平均 |");
        Console.WriteLine("|---|---|---|---|---|---|---|--:|");
        foreach (var c in cells)
        {
            var s = c.S;
            string T(int t) { long tot = Enumerable.Range(0, 5).Sum(k => s.FreeT[t * 5 + k]); return tot == 0 ? "—" : string.Join("／", Enumerable.Range(0, 5).Select(k => (100.0 * s.FreeT[t * 5 + k] / tot).ToString("F0"))); }
            long tot = s.Free.Sum(); double mean = tot == 0 ? double.NaN : Enumerable.Range(0, 10).Sum(k => (double)k * s.Free[k]) / tot;
            Console.WriteLine($"| {c.B} | {c.W.Name} | {T(1)} | {T(2)} | {T(3)} | {T(4)} | {T(5)} | {F1(mean)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## §3-4 版が置き換える経路の、いまの発火回数（規定・/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 手番の頭の喚び出し | うち獣（群れ込み） | 弾けた獣（緊急込み） | 弾けた連鎖の駒 /獣 | 緊急 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (var c in cells)
        {
            var s = c.S;
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Per1(s.SomCalls, s.N)} | {Per1(s.Beasts.Sum(), s.N)} | {Per1(s.Bursts, s.N)} | {Per1(s.BurstUnits, s.Bursts)} | {Per1(s.Emerg, s.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-2 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = string.IsNullOrEmpty(only) ? Vers : Vers.Where(v => v.Name == "規定" || only.Split(',').Contains(v.Name)).ToArray();
        var cells = new List<(string B, Formation F0, S287.Wave W, Ver V, St S)>();
        foreach (var (n, f) in Boards()) foreach (var v in vers) { var fv = Apply(f, v); foreach (var w in Waves) cells.Add((n, f, w, v, Measure(fv, w))); }
        var ctl = new Dictionary<(string, string, string), string>();
        foreach (var c in cells.Where(c => c.S.Win >= 50))
        {
            var fv = Apply(c.F0, c.V);
            ctl[(c.B, c.W.Name, c.V.Name)] = F1(100.0 * WinsLite(S309.Swap(fv, c.V.Som, UnitCatalog.Dolga), c.W, Seeds, out _) / Seeds);
        }

        Console.WriteLine($"# 第314期 §4-2 代表台 × 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}・seed 0..{Seeds - 1}・verbose）");
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
                    var c = cells.First(x => x.B == n && x.V == v && x.W == w);
                    var b = cells.First(x => x.B == n && x.V == vers[0] && x.W == w);
                    return $"{F1(c.S.Win)}{(v == vers[0] ? "" : $"（{Sg(c.S.Win - b.S.Win)}）")}{(ctl.TryGetValue((n, w.Name, v.Name), out var k) ? $" [{k}]" : "")}";
                })) + " |");

        foreach (var w in Waves)
        {
            Console.WriteLine();
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 勝率 | 倒しT | 負けT（上限） | 寿命 | ソム 倒れたT | 獣 喚んだ ・ 弾けた 隣帯電 ／ 枠なし ／ 敵なし ・ **立った** ・ 暴発（乾） | 同時に立つ 最大 ／ 戦の最大の平均 ／ 喚び出し後の平均 | 立っていたT | 立った獣への一撃 ・ 量 /戦 | 弾けさせた（起こし手）| 倒した | シガ 主目標が獣 手番 ／ 割り込み ・ ×倍率 | 群れ 1／2／3／4／5 | カタ 雷雲 | 光 ・ 衣 ・ 受け止めた |"
                + (w.Boss ? " 味方の与ダメ /T ・ 勇者の回復 /T ・ 差 |" : ""));
            Console.WriteLine("|---|---|--:|--:|--:|---|---|---|---|--:|---|---|---|---|---|--:|---|" + (w.Boss ? "---|" : ""));
            foreach (var c in cells.Where(x => x.W == w))
            {
                var a = c.S;
                long sh = a.SwarmHist.Sum();
                string beast = a.Called == 0 ? "—" : $"{Per1(a.Called, a.N)} ・ {Per1(a.Charged, a.N)} ／ {Per1(a.NoSeat, a.N)} ／ {Per1(a.NoFoe, a.N)} ・ **{Per1(a.Stood, a.N)}** ・ {Per1(a.Overflow, a.N)}（{Per1(a.OverflowDry, a.N)}）";
                string conc = a.AliveN == 0 ? "—" : $"{a.AlivePeak} ／ {Per1(a.PeakSum, a.N)} ／ {Per1(a.AliveSum, a.AliveN)}";
                string sw2 = sh == 0 ? "—" : string.Join("／", Enumerable.Range(1, 5).Select(k => Pct(a.SwarmHist[k], sh).Replace("%", "")));
                string shg = a.ShigaOwn + a.ShigaInt == 0 ? "—" : $"{Pct(a.ShigaOwnBeast, a.ShigaOwn)} ／ {Pct(a.ShigaIntBeast, a.ShigaInt)} ・ ×{Per1(a.SwMultSum, a.SwMultN)}";
                var fv = Apply(c.F0, c.V);
                Console.WriteLine($"| {c.B} | {c.V.Name} | {F1(a.Win)} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)}{(a.Caps > 0 ? $"（{Pct(a.Caps, a.N)}）" : "")} | {Dt(a.FirstDeathN, a.FirstDeathT, a.N)} | {Dt(a.SomDied, a.SomDeathT, a.N)} | {beast} | {conc} | {Per1(a.StandLife, a.StandLifeN)} | "
                    + $"{Per1(a.AtkOnStand, a.N)} ・ {Per0(a.DmgOnStand, a.N)} | {Top(a.Poppers, a.N)} | {Top(a.Killers, a.N)} | {shg} | {sw2} | {(a.KataN > 0 ? Per1(a.KataCloud, a.KataN) : "—")} | {Per1(a.Lights, a.N)} ・ {Per0(a.VeilAdded, a.N)} ・ {Per0(a.VeilSoaked, a.N)} |"
                    + (w.Boss ? $" {Per0(a.FoeDmgAll, a.Turns)}（{FoeDmgLine(a, fv)}）・ {Per0(a.FoeHeal, a.Turns)} ・ {Sg((double)(a.FoeDmgAll - a.FoeHeal) / Math.Max(1, a.Turns))} |" : ""));
            }
        }

        Console.WriteLine();
        Console.WriteLine("## §4-4 格子を回す版（代表台で規定から ±10pt 以上・ボスは ±5pt 以上。落ちる向きも数える）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
            foreach (var wn in new[] { "ボス", "近衛", "大隊" })
            {
                var moved = new List<string>();
                foreach (var c in cells.Where(x => x.V == v && (wn == "ボス" ? x.W.Boss : x.W.Name == wn)))
                {
                    double d = c.S.Win - cells.First(x => x.B == c.B && x.V == vers[0] && x.W == c.W).S.Win;
                    if (Math.Abs(d) >= (c.W.Boss ? 5 : 10)) moved.Add($"{c.B}（{Sg(d)}）");
                }
                Console.WriteLine($"- {v.Name} × {wn}: {(moved.Count == 0 ? "**該当なし**" : $"**該当**（{string.Join("・", moved)}）")}");
            }
        Console.WriteLine();
        Console.WriteLine("（九体 ／ 本編の波は格子が無いので、門には入れずに表だけ見る。）");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-3 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        var grids = Vers.Select(v =>
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
        Console.WriteLine($"# 第314期 §4-3 `compare` {rows.Length} 行 × 版（seed 0..{Seeds - 1}・基準は規定）");
        double M(double[,] g, int ri) => Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
        for (int vi = 1; vi < Vers.Length; vi++)
        {
            var v = Vers[vi]; var g = grids[vi]; var b = grids[0];
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
                bool holds = rows[ri].F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Som));
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
    // §4-4 ソム ↔ ツギ × 版
    // ---------------------------------------------------------------------------------
    /// <summary>第309期 §4-1 の入れ替えと同じ台・同じ席を、規定の駒で組み直したもの（(名前, ソムの台, ツギの台)）。</summary>
    static (string Name, Formation S, Formation? T)[] Pairs()
    {
        UnitDef som = UnitCatalog.Som, tsugi = UnitCatalog.Tsugi;
        var win = WinSeat();
        var shock = RowF("感電 (シガ×カタ×ソム)");
        return new (string, Formation, Formation?)[]
        {
            ("雷の型 ドハ→H", S309.Swap(Playtest("試遊・感電 雷の型"), UnitCatalog.Doha, som), S309.Swap(Playtest("試遊・感電 雷の型"), UnitCatalog.Doha, tsugi)),
            ("感電 糸 ガルド→H", S309.Swap(Playtest("試遊・感電 糸"), UnitCatalog.Gald, som), S309.Swap(Playtest("試遊・感電 糸"), UnitCatalog.Gald, tsugi)),
            ("火の型 ソラ→H", S309.Swap(Playtest("試遊・感電 火の型"), UnitCatalog.Sora, som), S309.Swap(Playtest("試遊・感電 火の型"), UnitCatalog.Sora, tsugi)),
            ("勝ち台 ツギの席（元の勝ち台）", S309.Swap(win, tsugi, som), win),
            ("勝ち台 ゴルム→H", S309.Swap(win, UnitCatalog.Golm, som), null),
            ("感電 行（もう片方 → ドルガ）", S309.Swap(shock, tsugi, UnitCatalog.Dolga), S309.Swap(shock, som, UnitCatalog.Dolga)),
            ("光の盾", Playtest(S309.Shield), S309.Swap(Playtest(S309.Shield), som, tsugi)),
            ("光の盾 重", Playtest(S309.ShieldHeavy), S309.Swap(Playtest(S309.ShieldHeavy), som, tsugi)),
        };
    }

    static void SwapAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var waves = S307.Waves;
        var pairs = Pairs();
        var tsugi = new Dictionary<(string, string), double>();
        foreach (var (n, _, t) in pairs) if (t is not null) foreach (var w in waves) tsugi[(n, w.Name)] = 100.0 * WinsLite(t, w, Seeds, out _) / Seeds;
        Console.WriteLine($"# 第314期 §4-4 ソム ↔ ツギ × 版（第309期 §5-1 と同じ台・同じ席を規定の駒で組み直した・{waves.Length} 波・seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("ソムの側は版を当てた台。ツギの側は規定（版に依らない）。差 ＝ ソム − ツギ。");
        foreach (var v in Vers)
        {
            Console.WriteLine();
            Console.WriteLine($"## {v.Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "---|")));
            int plus = 0, minus = 0, n2 = 0; var minusCells = new List<string>();
            foreach (var (n, s, t) in pairs)
            {
                var fs = Apply(s, v);
                var line = new List<string>();
                foreach (var w in waves)
                {
                    double a = 100.0 * WinsLite(fs, w, Seeds, out _) / Seeds;
                    if (t is null) { line.Add($"{F1(a)} ／ —"); continue; }
                    double b = tsugi[(n, w.Name)];
                    line.Add($"{F1(a)} ／ {F1(b)} ／ **{Sg(a - b)}**");
                    if (w.Name == "本編 第1波") continue;
                    n2++;
                    if (a - b >= 10) plus++;
                    if (a - b <= -10) { minus++; minusCells.Add($"{n} × {w.Name}（{Sg(a - b)}）"); }
                }
                Console.WriteLine($"| {n} | {string.Join(" | ", line)} |");
            }
            Console.WriteLine();
            Console.WriteLine($"目安（本編 第1波を除く {n2} セル）: ソム − ツギ ≥ +10pt **{plus}** ／ ≤ −10pt **{minus}**");
            if (minusCells.Count > 0) Console.WriteLine($"- −10pt 以下: {string.Join("・", minusCells)}");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-4 ソムの格子（第311期と同じ作り・駒は規定）
    // ---------------------------------------------------------------------------------
    static void Grid(string ver, string wave)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var v = V(ver);
        var w = S307.WaveOf(wave);
        UnitDef[] fixedBase = w.Boss ? new[] { UnitCatalog.Kugu, UnitCatalog.Kata } : new[] { UnitCatalog.Tou };
        var pool = w.Boss ? S307.BossPool : S307.ElitePool;
        int k = w.Boss ? 2 : 3, need = w.Boss ? 1 : 5;
        var lu = S293.Combos(pool, k);
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedBase.Append(UnitCatalog.Som).Concat(t).ToArray()));
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
        Console.WriteLine($"# 第314期 §4-4 ソムの格子 × {w.Name} × 版 {v.Name}（固定枠 {S307.OrderName(fixedBase)} ＋ ソム・探索枠{k}・{boards.Count:N0} 台）");
        Console.WriteLine();
        Console.WriteLine($"足切り seed 0..{S307.CutSeeds - 1} で {need} 勝以上（{cutPass:N0} 台）→ seed 0..{Seeds - 1} → 勝率 50% 以上でソム → ドルガ。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率 ≧ 50% | **届いた（ソム → ドルガで半分以下）** | 勝率 ≧ 90% | 勝率 ＞ 0% | 最大の勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        Console.WriteLine($"| {v.Name} | {half:N0} | {reach:N0} | {ninety:N0} | {all.Count(r => r.Wins > 0):N0} | {(all.Count == 0 ? "—" : F1(100.0 * all.Max(r => r.Wins) / Seeds))} |");
        Console.WriteLine();
        Console.WriteLine("勝率 90% 以上の台の倒しT の分布（台ごとの平均の倒しT）:");
        Console.WriteLine();
        var ts = all.Where(r => r.Wins * 10 >= Seeds * 9).Select(r => (double)r.WinT / r.Wins).ToList();
        var bins = new[] { 6, 8, 10, 12, 15, 20, 25, 31 };
        Console.WriteLine("| 倒しT | " + string.Join(" | ", bins.Select((b, i) => i == 0 ? $"≦{b}" : $"{bins[i - 1] + 1}〜{b}")) + " |");
        Console.WriteLine("|---|" + string.Concat(bins.Select(_ => "--:|")));
        Console.WriteLine("| 台 | " + string.Join(" | ", bins.Select((b, i) => ts.Count(t => t <= b + 0.5 && (i == 0 || t > bins[i - 1] + 0.5)).ToString("N0"))) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §5 台本の例
    // ---------------------------------------------------------------------------------
    static void Find()
    {
        Console.WriteLine("# som314 find —— 立った獣（`Summon` だけで `BeastBurst` の無い獣）と SB-b の暴発（`BeastBurst 暴発`）のある seed");
        Console.WriteLine();
        foreach (var vn in new[] { "SB-a", "SB-b" })
            foreach (var (bn, f0) in Boards().Where(b => b.Name is "光の盾" or "勝ち台 ツギ→ソム"))
                foreach (var wn in new[] { "boss", "guard" })
                {
                    var w = WaveOf(wn); var f = Apply(f0, V(vn));
                    for (int sd = 0, found = 0; sd < 20 && found < 2; sd++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var phantom = r.Events.Where(x => x.Kind == BattleEventKind.BeastBurst && x.Text != BeastBurstLabels.Overflow).Select(x => x.TargetId).ToHashSet();
                        var stood = r.Events.Where(x => x.Kind == BattleEventKind.Summon && x.Text == UnitCatalog.Fodder.Name && !phantom.Contains(x.TargetId)).Select(x => x.Turn).ToList();
                        var over = r.Events.Where(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Overflow).Select(x => x.Turn).ToList();
                        var mixed = r.Events.GroupBy(x => x.Turn).Where(g => g.Any(x => x.Kind == BattleEventKind.BeastBurst && x.Text != BeastBurstLabels.Overflow && x.Text != BeastBurstLabels.Emergency)
                                                                       && g.Any(x => x.Kind == BattleEventKind.Summon && x.Text == UnitCatalog.Fodder.Name && !phantom.Contains(x.TargetId))).Select(g => g.Key).ToList();
                        if (stood.Count == 0) continue;
                        found++;
                        Console.WriteLine($"- {vn} × {bn} × {w.Name} × seed {sd}: 立った T{string.Join(",", stood.Distinct().Take(6))}・弾けると立つが同じT {(mixed.Count == 0 ? "—" : "T" + string.Join(",", mixed.Take(4)))}・暴発 {(over.Count == 0 ? "—" : "T" + string.Join(",", over.Distinct().Take(6)))}（{(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}）");
                    }
                }
    }

    /// <summary>
    /// 第276期の S1x からの穴の大きさ: 雷を纏って立つ獣（`NoThunderKey`）に、帯びた敵がいないときの代わりの一撃（`Thunder` の 1 発目で、その手番に他の雷が無い）が落ちた回数。
    /// 獣が立つ最後の規定（第309〜310期 ＝ `SomH310`）の台で数える（第314期の塞ぎは SB の獣だけなので、この数は塞ぐ前と同じ）。
    /// </summary>
    static void Hole()
    {
        Console.WriteLine("# som314 hole —— 雷を纏って立つ獣に、雷の代わりの一撃が落ちた回数（ソム ＝ `SomH310`・第309〜310期の規定・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | カタの雷（手番）/戦 | うち代わりの一撃 | うち獣に落ちた | 獣に落ちた量 /戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (var (bn, f0) in Boards().Where(b => b.F.Occupied().Any(o => o.Def.Id == "kata") && b.Name is "光の盾" or "光の盾 重" or "勝ち台 ツギ→ソム"))
            foreach (var w in Waves.Take(3))
            {
                var f = Pin310(f0);
                long n = 0, wins = 0, casts = 0, fb = 0, onBeast = 0, amt = 0;
                var lk = new object();
                Parallel.For(0, Seeds, sd =>
                {
                    var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), sd, verbose: true);
                    var kata = p.First(u => u.Def.Id == "kata");
                    var beasts = r.Events.Where(x => x.Kind == BattleEventKind.Summon && x.Text == UnitCatalog.Fodder.Name && x.TargetId is int).Select(x => x.TargetId!.Value).ToHashSet();
                    long c = 0, b = 0, bf = 0, a = 0;
                    var thunders = r.Events.Where(x => x.Kind == BattleEventKind.Thunder && x.ActorId == kata.InstanceId).ToList();
                    for (int i = 0; i < thunders.Count; i++)
                    {
                        if (thunders[i].Slot != 1) continue;   // 1 発目
                        c++;
                        bool alone = i + 1 >= thunders.Count || thunders[i + 1].Slot == 1;   // 跳ねが無い（代わりの一撃は跳ねない）
                        if (!alone) continue;
                        if (thunders[i].TargetId is int t && beasts.Contains(t)) { bf++; a += thunders[i].Amount; }
                        b++;
                    }
                    var kt = r.TallyByUnit["kata"];
                    lock (lk) { n++; if (r.PlayerWon) wins++; casts += kt.ThunderCasts; fb += kt.ThunderFallback; onBeast += bf; amt += a; }
                });
                Console.WriteLine($"| {bn} | {w.Name} | {Pct(wins, n)} | {Per1(casts, n)} | {Per1(fb, n)} | {Per1(onBeast, n)} | {Per0(amt, n)} |");
            }
        Console.WriteLine();
        Console.WriteLine("（代わりの一撃は `ThunderFallback`（カタの帳簿）。獣に落ちたのは、跳ねの無い 1 発目の `Thunder` の的が背いた獣だったもの。）");
    }

    static void Memo(string ver, string part, string wave, int seed, int t0, int t1)
    {
        var v = V(ver);
        var (name, f0) = Boards().First(b => b.Name.Contains(part, StringComparison.Ordinal));
        var f = Apply(f0, v);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        foreach (var ev in r.Events.Where(x => x.Kind is BattleEventKind.Summon or BattleEventKind.SilkBall && x.TargetId is int)) names.TryAdd(ev.TargetId!.Value, ev.Kind == BattleEventKind.SilkBall ? "糸玉" : "背いた獣");
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# som314 memo —— {name}（{v.Name}・{Seats(f)}）× {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}・T{t0}〜T{t1}");
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
    // 自己検査（§6）
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(Formation f, List<UnitState> enemy)
    {
        var ctx = new BattleContext(0, true);
        var add = typeof(BattleContext).GetMethod("Add", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        foreach (var u in p.Concat(enemy)) add.Invoke(ctx, new object[] { u });
        foreach (var u in ctx.AllUnits) u.SetCounter(StatusKeys.IdleTurn, 99);
        return (ctx, p, enemy);
    }

    static UnitTally T(BattleContext ctx, UnitState u)
    {
        var d = (Dictionary<string, UnitTally>)typeof(BattleContext).GetProperty("TallyByUnit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(ctx)!;
        return d.TryGetValue(u.Def.Id, out var t) ? t : new UnitTally();
    }

    /// <summary>規定の駒の盤（光の盾のソムを版に替え）＋ 本編 第3波の敵（前列 ／ 中央に駒が立ち、召喚枠は空）。</summary>
    static (BattleContext Ctx, UnitState Som, List<UnitState> P, List<UnitState> E) StandBoard(UnitDef somDef)
    {
        var f = S309.Swap(Playtest(S309.Shield), UnitCatalog.Som, somDef);
        var (ctx, p, e) = Board(f, BattleEngine.Materialize(EnemyCatalog.Stages[2].Enemy, BattleContext.EnemyTeam));
        return (ctx, p.First(u => u.Def.Id == "som"), p, e);
    }

    static void TurnStart(BattleContext ctx, UnitState som) { foreach (var t in som.Traits.ToList()) t.OnTurnStart(ctx, som); }
    static List<UnitState> Stood(BattleContext ctx, UnitState som) => ctx.AllUnits.Where(u => u.IsAlive && BetrayedTrait.IsFodder(u) && u.RawCounter(BattleContext.StandOwnerKey) == som.InstanceId + 1).ToList();
    static void SetSwarm(UnitState som, int n) { som.SetCounter(BattleContext.SwarmTurnKey, 1); som.SetCounter(BattleContext.SwarmPrevKey, (n - 1) * BattleContext.SwarmPer); }   // ターン 0 の盤で群れの数を n に

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# som314 check —— 第314期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        UnitDef som = UnitCatalog.Som, sba = UnitCatalog.SomSBa, sbb = UnitCatalog.SomSBb;

        // (a) 版の定義・規定は動かない
        {
            bool defs = sba.Traits.SequenceEqual(som.Traits.Append(TraitId.BeastStand)) && sbb.Traits.SequenceEqual(sba.Traits.Append(TraitId.BeastStandBrief))
                && new[] { sba, sbb }.All(d => d.Id == som.Id && d.Name == som.Name && d.MaxHp == som.MaxHp && d.Attack == som.Attack && d.Speed == som.Speed && ReferenceEquals(d.Actions, som.Actions))
                && new[] { sba, sbb }.All(d => !UnitCatalog.Everyone.Contains(d));
            bool dflt = som.Traits.SequenceEqual(UnitCatalog.SomSWKa.Traits) && UnitCatalog.All.Contains(som) && UnitCatalog.All.Count == 52
                && !som.Traits.Contains(TraitId.BeastStand) && UnitCatalog.Fodder.MaxHp == 12 && UnitCatalog.Fodder.Attack == 0 && UnitCatalog.Fodder.Speed == 1;
            bool text = sba.MinusText == UnitCatalog.SomStandMinusText && sbb.MinusText == UnitCatalog.SomStandMinusText + "。立っていられるのは、ひととき" && !sba.PlusText.Contains("着いた瞬間に弾ける");
            Expect("(a) 版: SB-a ＝ 規定 ＋ `BeastStand`・SB-b ＝ SB-a ＋ `BeastStandBrief`（数値・行動は規定のまま・`Everyone` の外）・規定のソムは第312期の重ねのまま・獣は HP 12 ／ 攻 0 ／ 速 1・文面は叩き台",
                defs && dflt && text, $"版 {defs}・規定 {dflt}・文面 {text}");
        }
        // (b) SB-a: 隣に帯電した敵がいなければ立つ・いれば弾ける・枠が無ければ弾ける・立っている獣がいても空き枠があれば喚ぶ
        {
            // b1 立つ
            var (c1, s1, _, e1) = StandBoard(sba);
            int ev0 = c1.Events.Count;
            TurnStart(c1, s1);
            var st1 = Stood(c1, s1);
            var ev1 = c1.Events.Skip(ev0).ToList();
            bool b1 = st1.Count == 1 && st1[0].Slot == BetrayedTrait.FodderSlot && st1[0].RawCounter(StatusKeys.Shock) > 0 && st1[0].RawCounter(BetrayedShockTrait.NoThunderKey) > 0
                   && !ev1.Any(x => x.Kind == BattleEventKind.BeastBurst) && T(c1, s1).StandStood == 1;
            // b2 隣が帯電していれば弾ける（敵の駒すべてに感電）
            var (c2, s2, _, e2) = StandBoard(sba);
            foreach (var u in e2) u.SetCounter(StatusKeys.Shock, 1);
            ev0 = c2.Events.Count;
            TurnStart(c2, s2);
            var ev2 = c2.Events.Skip(ev0).ToList();
            bool b2 = Stood(c2, s2).Count == 0 && T(c2, s2).StandCharged == 1 && ev2.Any(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Charged && x.Amount == 0)
                   && ev2.Any(x => x.Kind == BattleEventKind.ShockSpent && x.ActorId == s2.InstanceId);
            // b3 空いた召喚枠が無ければ弾ける（召喚枠を敵の駒で埋める）
            var (c3, s3, _, e3) = StandBoard(sba);
            foreach (int sl in FormationShape.X.SummonSlots) c3.Summon(UnitCatalog.Spore, BattleContext.EnemyTeam, sl);
            ev0 = c3.Events.Count;
            TurnStart(c3, s3);
            var ev3 = c3.Events.Skip(ev0).ToList();
            bool b3 = Stood(c3, s3).Count == 0 && T(c3, s3).StandNoSeat == 1 && ev3.Any(x => x.Kind == BattleEventKind.BeastBurst && x.Amount == 1);
            // b4 立っている獣がいても空き枠があれば喚ぶ（2回目の喚び出しは次の枠へ・前の獣は帯電しているが「隣の敵」に数えない）
            TurnStart(c1, s1);
            var st4 = Stood(c1, s1);
            bool b4 = st4.Count == 2 && st4.Select(u => u.Slot).OrderBy(x => x).SequenceEqual(new[] { 5, 7 }) && T(c1, s1).StandStood == 2;
            Expect("(b) SB-a: 隣に帯電した敵がいなければ帯電して立つ（雷の印つき）・いれば弾ける（起こし手はソム）・空いた召喚枠が無ければ弾ける（`Amount` 1）・立っている獣がいても空き枠があれば喚ぶ（盤を直に組む）",
                b1 && b2 && b3 && b4, $"立つ {b1}・弾ける {b2}・枠なし {b3}・2体目 {b4}（席 {string.Join(",", st4.Select(u => u.Slot))}）");
        }
        // (c) SB-a: 群れのうち弾けるものは1つの連鎖・立つものは立つ
        {
            var (ctx, s, _, e) = StandBoard(sba);
            var shape = FormationShape.X;
            // ○前2（7）の隣で、○中1（5）の隣でない敵だけを帯電させる → 1体目（7）は弾け、2体目（5）は立つ。3体目（6）は 6 の隣次第
            var hot = e.Where(u => shape.AreAdjacent(7, u.Slot) && !shape.AreAdjacent(5, u.Slot)).ToList();
            foreach (var u in hot) u.SetCounter(StatusKeys.Shock, 1);
            bool sixHot = hot.Any(u => shape.AreAdjacent(6, u.Slot));
            SetSwarm(s, 3);
            var plan = ctx.PlanBeasts(BattleContext.EnemyTeam, 7, 3);
            int ev0 = ctx.Events.Count;
            TurnStart(ctx, s);
            var ev = ctx.Events.Skip(ev0).ToList();
            var t = T(ctx, s);
            int roots = ev.Count(x => x.Kind == BattleEventKind.ShockSpent && x.Slot == 0);
            int lights = ev.Count(x => x.Kind == BattleEventKind.Spark && x.Text == SparkLabels.Release);
            int nBurst = plan.Count(x => x.Fate != BattleContext.BeastFate.Stand);
            bool ok = hot.Count > 0 && t.StandBeasts == 3 && t.StandCharged == nBurst && t.StandStood == 3 - nBurst && Stood(ctx, s).Count == 3 - nBurst
                   && plan[0].Fate == BattleContext.BeastFate.Charged && plan[1].Fate == BattleContext.BeastFate.Stand && lights == (nBurst > 0 ? 1 : 0) && roots == nBurst
                   && Stood(ctx, s).All(u => u.Slot != 7);
            Expect("(c) SB-a: 群れ 3 体のうち、隣が帯電している席の獣だけが弾けて1つの連鎖（光は1回）・残りは立つ", ok,
                $"帯電させた敵 {hot.Count}（○中3 の隣 {sixHot}）・席 {string.Join(",", plan.Select(x => $"{x.Seat}:{x.Fate}"))}・弾けた {t.StandCharged}・立った {t.StandStood}・連鎖の起点 {roots}・光 {lights}");
        }
        // (d) SB-b: 立っている獣は次のソムの手番の頭に暴発する（起こし手はソム・光になる・倒れない）
        {
            var (ctx, s, _, e) = StandBoard(sbb);
            TurnStart(ctx, s);
            var first = Stood(ctx, s);
            foreach (var u in ctx.AllUnits.Where(u => u.IsAlive && u.TeamId != s.TeamId && !BetrayedTrait.IsFodder(u))) u.Hp = u.MaxHp / 2;
            foreach (var u in ctx.AllUnits.Where(u => u.TeamId == s.TeamId)) u.Hp = u.MaxHp / 2;   // 光が降る先
            int ev0 = ctx.Events.Count;
            TurnStart(ctx, s);
            var ev = ctx.Events.Skip(ev0).ToList();
            var t = T(ctx, s);
            int iOver = ev.FindIndex(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Overflow);
            int iSummon = ev.FindIndex(x => x.Kind == BattleEventKind.Summon);
            bool ok = first.Count == 1 && !first[0].IsAlive && t.StandOverflow == 1 && t.StandOverflowDry == 0
                   && iOver >= 0 && ev[iOver].TargetId == first[0].InstanceId && iSummon > iOver
                   && ev.Any(x => x.Kind == BattleEventKind.ShockSpent && x.TargetId == first[0].InstanceId && x.ActorId == s.InstanceId && x.Slot == 0)
                   && !ev.Any(x => x.Kind == BattleEventKind.Death && x.TargetId == first[0].InstanceId)
                   && ev.Any(x => x.Kind == BattleEventKind.Spark && x.Text == SparkLabels.Release) && Stood(ctx, s).Count == 1;
            Expect("(d) SB-b: 立っている獣は次のソムの手番の頭（喚び出しの前）に暴発する（`BeastBurst 暴発` → 連鎖の起点・起こし手はソム・`Death` は出ない・光になる）・そのあと新しい獣が立つ", ok,
                $"暴発 {t.StandOverflow}（乾 {t.StandOverflowDry}）・暴発の行 #{iOver} ／ 喚び出し #{iSummon}・いま立っている {Stood(ctx, s).Count}");
        }
        // (e) 緊急の喚び出しはいつもその場で弾ける
        {
            var (ctx, s, p, _) = StandBoard(sba);
            var ally = p.First(u => u != s);
            int ev0 = ctx.Events.Count;
            ctx.EmergencyBurst(s, ally);
            var ev = ctx.Events.Skip(ev0).ToList();
            bool ok = Stood(ctx, s).Count == 0 && ev.Count(x => x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Emergency) == 1 && T(ctx, s).StandBeasts == 0;
            Expect("(e) SB-a でも緊急の喚び出しはいつもその場で弾ける（立たない）", ok, $"立った {Stood(ctx, s).Count}");
        }
        // (f) 実戦: 立った獣にカタの雷は落ちない・立った獣はいる・暴発は SB-b だけ
        {
            long thunder = 0, stood = 0, overA = 0, overB = 0, stoodB = 0;
            foreach (var (_, f0) in Boards().Where(b => b.F.Occupied().Any(o => o.Def.Id == "kata")))
                foreach (var w in new[] { Waves[0], Waves[1] })
                    for (int sd = 0; sd < 10; sd++)
                    {
                        var a = Fight(Apply(f0, V("SB-a")), w, sd);
                        thunder += a.ThunderOnStand; stood += a.Stood; overA += a.Overflow;
                        var b = Fight(Apply(f0, V("SB-b")), w, sd);
                        thunder += b.ThunderOnStand; stoodB += b.Stood; overB += b.Overflow;
                    }
            Expect("(f) 実戦（カタのいる代表台 × ボス ／ 近衛 × seed 0..9）: 立った獣にカタの雷は落ちない・SB-a に暴発は無い・SB-b は暴発する", thunder == 0 && stood > 0 && overA == 0 && overB > 0,
                $"雷 {thunder}・立った SB-a {stood} ／ SB-b {stoodB}・暴発 SB-a {overA} ／ SB-b {overB}");
        }
        // (g) 計数器は盤面を変えない・規定は動かない（計数器の有無と、版の札を持たないソムで同じ勝敗と決着T）
        {
            int nd = 0, n = 0; long census = 0;
            var rows = Boards().Take(5).ToArray();
            foreach (var (_, f) in rows)
                foreach (var w in Waves.Take(3))
                    for (int sd = 0; sd < 3; sd++)
                    {
                        BattleContext.StandCensus = false;
                        var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        BattleContext.StandCensus = true;
                        var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        BattleContext.StandCensus = false;
                        n++;
                        if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) nd++;
                        census += r2.TallyByUnit.GetValueOrDefault("som")?.CensusBeasts?.Sum() ?? 0;
                        if (r1.TallyByUnit.GetValueOrDefault("som")?.CensusBeasts is not null) nd++;
                    }
            Expect("(g) 計数器 `StandCensus` は盤面を変えない（有無で勝敗と決着T が同じ・無いときは数えない・あるときは数える）", nd == 0 && census > 0, $"ずれ {nd} ／ {n} 件・数えた獣 {census}");
        }
        // (h) 決定的・verbose の有無で勝敗と決着T が同じ
        {
            int nd = 0, n = 0;
            foreach (var v in Vers.Skip(1))
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
            Expect("(h) 決定的・verbose の有無で勝敗と決着T が変わらない（SB-a ／ SB-b × 代表台 7 × 10 波 × seed 0..1）", nd == 0, $"{nd} ／ {n} 件");
        }
        // (i) 乱数
        {
            static int Count(string text, string pat) { int n = 0, k = 0; while ((k = text.IndexOf(pat, k, StringComparison.Ordinal)) >= 0) { n++; k += pat.Length; } return n; }
            static string? Head(string path)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", $"show c6613e4:{path}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
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
                if (head is null) { clean = false; notes.Add($"{path}: 第313期の版を読めない"); continue; }
                foreach (var pat in new[] { "PickOne(", "Roll(" }) { int a0 = Count(head, pat), a1 = Count(now, pat); if (a1 > a0) clean = false; notes.Add($"{Path.GetFileName(path)} {pat} {a0}→{a1}"); }
            }
            Expect("(i) `PickOne(` ／ `Roll(` を新たに使っていない（第313期のコミット c6613e4 以下）", clean, string.Join("・", notes));
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
