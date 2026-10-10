using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using H304 = Hush304Diag;

// =====================================================================================
// som308 —— 第308期「ソム SH-a の規定化 ＋ 光の衣（溢れた光を破片に）」。
// 指示書は design/PHASE308_SOM_VEIL_SPEC.md ／ 報告は design/PHASE308_SOM_VEIL.md。
//
//     dotnet run --project BattleSim -c Release 0 som308 p0          # Phase 0（§4）: 溢れの内訳（満タンに降った ／ 満タンに届いて余った・ターン・席）・衣の見込み・4割の上限で捨てる見込み・ツギの板
//     dotnet run --project BattleSim -c Release 0 som308 compare     # `compare` 64 行 × 版（ソムの在席行だけ差し替える・規定は段0 の SH-a）
//     dotnet run --project BattleSim -c Release 0 som308 boards      # 代表台 5 台 × ボス ／ 近衛 ／ 大隊 ／ 本編の5波 × 規定 ／ LV-a ／ LV-c ／ FO（衣・寄せ・対照 ソム → ドルガ・元の勝ち台）
//     dotnet run --project BattleSim -c Release 0 som308 gridboss [lva|lvc|fo]       # ボスの格子（第307期と同じ作り・規定 ＋ 版）
//     dotnet run --project BattleSim -c Release 0 som308 grid <guard|bat> <lva|lvc|fo>  # 精鋭の格子（第307期と同じ作り）
//     dotnet run --project BattleSim -c Release 0 som308 check       # 自己検査
//
// 規定の駒（段0 の後・ソム ＝ SH-a）のまま組む（固定しない）。版は規定のソムと同じ席で差し替える。
// =====================================================================================
static class Som308Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": P0(); return;
            case "compare": CompareAll(); return;
            case "boards": BoardsAll(); return;
            case "gridboss": GridBoss(A(3, "")); return;
            case "grid": GridElite(A(3, "guard"), A(4, "lva")); return;
            case "check": Check(); return;
            default: Console.WriteLine("som308: モードは p0 / compare / boards / gridboss / grid / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・台（測る前に固定）
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef To);
    internal static readonly Ver[] Vers =
    {
        new("規定（SH-a）", "sh", UnitCatalog.Som), new("LV-a", "lva", UnitCatalog.SomLVa), new("LV-c", "lvc", UnitCatalog.SomLVc), new("FO", "fo", UnitCatalog.SomFO),
    };
    static Ver AnyVer(string n) => Vers.First(v => v.Ascii == n || v.Name == n);
    static Formation Apply(Formation f, Ver v) => ReferenceEquals(v.To, UnitCatalog.Som) ? f : FvSwap(f, UnitCatalog.Som, v.To);
    static bool HasSom(Formation f) => f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Som));

    const int Seeds = S307.Seeds, TMax = 8;
    static S287.Wave[] Waves => S307.Waves;
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);
    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => S307.Per(a, n);
    static string Per1(long a, long n) => S307.Per1(a, n);
    static string Per0(long a, long n) => S307.Per0(a, n);
    static string Pct(long a, long n) => S307.Pct(a, n);
    static string Dt(long died, long t, long n) => died == 0 ? "—" : $"{Per1(t, died)}（{Pct(died, n)}）";
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;
    static Formation WinSeat() => B283.Seat(S307.Order(S307.WinBoard));

    /// <summary>代表台（第307期 §4-1 と同じ5台・規定のソム ＝ SH-a で組む）。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("感電 (シガ×カタ×ソム)", Row("感電 (シガ×カタ×ソム)")),
        ("雷の型 ドハ→ソム", FvSwap(Playtest("試遊・感電 雷の型"), UnitCatalog.Doha, UnitCatalog.Som)),
        ("感電 糸 ガルド→ソム", FvSwap(Playtest("試遊・感電 糸"), UnitCatalog.Gald, UnitCatalog.Som)),
        ("勝ち台 ゴルム→ソム", FvSwap(WinSeat(), UnitCatalog.Golm, UnitCatalog.Som)),
        ("勝ち台 ツギ→ソム", FvSwap(WinSeat(), UnitCatalog.Tsugi, UnitCatalog.Som)),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    sealed class Deep
    {
        public long N, Wins, WinT, LoseT, Caps, FirstDeathN, FirstDeathT, SomN, SomDied, SomDeathT;
        public long Born, LFoe, LSummon, LBall, HushedL, BlockedL, Rains, Healed, Overflow, OverFull, OverTop, Inverted, Refused;
        public long VeilAdded, VeilCapped, VeilSoaked, VeilLeft, ArmorLeft, FocusPicks, FocusNone, HeroDmg, TsugiOut;
        public long[] ReachT = new long[TMax + 1], HealT = new long[TMax + 1], SoakT = new long[TMax + 1], OverT = new long[TMax + 1], VeilStockT = new long[TMax + 1];
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Merge(Deep o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LoseT += o.LoseT; Caps += o.Caps; FirstDeathN += o.FirstDeathN; FirstDeathT += o.FirstDeathT; SomN += o.SomN; SomDied += o.SomDied; SomDeathT += o.SomDeathT;
            Born += o.Born; LFoe += o.LFoe; LSummon += o.LSummon; LBall += o.LBall; HushedL += o.HushedL; BlockedL += o.BlockedL; Rains += o.Rains; Healed += o.Healed; Overflow += o.Overflow; OverFull += o.OverFull; OverTop += o.OverTop;
            Inverted += o.Inverted; Refused += o.Refused; VeilAdded += o.VeilAdded; VeilCapped += o.VeilCapped; VeilSoaked += o.VeilSoaked; VeilLeft += o.VeilLeft; ArmorLeft += o.ArmorLeft;
            FocusPicks += o.FocusPicks; FocusNone += o.FocusNone; HeroDmg += o.HeroDmg; TsugiOut += o.TsugiOut;
            for (int i = 0; i <= TMax; i++) { ReachT[i] += o.ReachT[i]; HealT[i] += o.HealT[i]; SoakT[i] += o.SoakT[i]; OverT[i] += o.OverT[i]; VeilStockT[i] += o.VeilStockT[i]; }
        }
    }

    static Deep FightDeep(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var d = new Deep { N = 1 };
        if (r.PlayerWon) { d.Wins = 1; d.WinT = r.Turns; } else { d.LoseT = r.Turns; if (r.Turns >= BattleEngine.MaxTurns) d.Caps = 1; }
        for (int t = 1; t <= Math.Min(TMax, r.Turns); t++) d.ReachT[t]++;
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        UnitState? som = p.FirstOrDefault(u => u.Def.Id == "som"), tsugi = p.FirstOrDefault(u => u.Def.Id == "tsugi");
        UnitState? hero = e.FirstOrDefault(u => u.Def == EnemyCatalog.BossRegular);
        if (som is not null && r.TallyByUnit.TryGetValue("som", out var mt))
        {
            d.SomN = 1;
            d.LFoe = mt.SparkLightFoe; d.LSummon = mt.SparkLightSummon; d.LBall = mt.SparkLightBall; d.Born = d.LFoe + d.LSummon + d.LBall;
            d.HushedL = mt.SparkHushedLights; d.BlockedL = mt.SparkBlockedLights; d.Rains = mt.SparkRains; d.Healed = mt.SparkHealed; d.Overflow = mt.SparkOverflow;
            d.Inverted = mt.SparkInverted; d.Refused = mt.SparkRefused; d.VeilAdded = mt.SparkVeilAdded; d.VeilCapped = mt.SparkVeilCapped; d.FocusPicks = mt.SparkFocusPicks; d.FocusNone = mt.SparkFocusNone;
        }
        // 受けた味方の側の帳簿（溢れの内訳・衣が受け止めた量）と、戦の終わりに残った衣
        foreach (var u in p)
        {
            if (!r.TallyByUnit.TryGetValue(u.Def.Id, out var at)) continue;
            d.OverFull += at.SparkOverRecvFull; d.OverTop += at.SparkOverRecvTop; d.VeilSoaked += at.SparkVeilSoaked;
            for (int t = 1; t <= TMax; t++) { d.OverT[t] += at.SparkOverRecvT?[t] ?? 0; d.SoakT[t] += at.SparkVeilSoakedT?[t] ?? 0; }
            if (u.IsAlive) { d.VeilLeft += Math.Min(u.RawCounter(SparkTrait.VeilKey), u.RawCounter(StatusKeys.Armor)); d.ArmorLeft += u.RawCounter(StatusKeys.Armor); }
        }
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Death when ev.TargetId is int t && mine.Contains(t):
                    if (d.FirstDeathN == 0) { d.FirstDeathN = 1; d.FirstDeathT = ev.Turn; }
                    if (som is not null && t == som.InstanceId && d.SomDied == 0) { d.SomDied = 1; d.SomDeathT = ev.Turn; }
                    break;
                case BattleEventKind.Heal when som is not null && ev.ActorId == som.InstanceId && ev.Turn <= TMax: d.HealT[ev.Turn] += ev.Amount; break;
                case BattleEventKind.Spark when ev.Text == SparkLabels.Veil && ev.Turn <= TMax: d.VeilStockT[ev.Turn] += ev.Amount; break;
                case BattleEventKind.Damage when hero is not null && ev.ActorId == hero.InstanceId && ev.TargetId is int h2 && mine.Contains(h2): d.HeroDmg += ev.Amount; break;
                case BattleEventKind.Plank when tsugi is not null && ev.ActorId == tsugi.InstanceId && (ev.Text == PlankLabels.Paste || ev.Text == PlankLabels.FirstAid): d.TsugiOut += ev.Amount; break;
                case BattleEventKind.Heal when tsugi is not null && ev.ActorId == tsugi.InstanceId: d.TsugiOut += ev.Amount; break;
            }
        }
        return d;
    }

    static Deep MeasureDeep(Formation f, S287.Wave w, int n)
    {
        var parts = new Deep[n];
        Parallel.For(0, n, i => parts[i] = FightDeep(f, w, i));
        var all = new Deep();
        foreach (var x in parts) all.Merge(x);
        return all;
    }

    static bool FightLite(Formation f, S287.Wave w, int seed) => BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), seed, verbose: false).PlayerWon;
    static int WinsPar(Formation f, S287.Wave w, int n)
    {
        int wins = 0;
        Parallel.For(0, n, s => { if (FightLite(f, w, s)) Interlocked.Increment(ref wins); });
        return wins;
    }

    // ---------------------------------------------------------------------------------
    // Phase 0（§4）
    // ---------------------------------------------------------------------------------
    sealed class P0Acc
    {
        public long N, Over, OverFull, OverTop, Healed;
        public long[] ReachT = new long[TMax + 1], OverT = new long[TMax + 1], StockT = new long[TMax + 1], CapT = new long[TMax + 1];
        public long[] OverSeat = new long[5];
        public long CapDiscard;
        public void Merge(P0Acc o)
        {
            N += o.N; Over += o.Over; OverFull += o.OverFull; OverTop += o.OverTop; Healed += o.Healed; CapDiscard += o.CapDiscard;
            for (int i = 0; i <= TMax; i++) { ReachT[i] += o.ReachT[i]; OverT[i] += o.OverT[i]; StockT[i] += o.StockT[i]; CapT[i] += o.CapT[i]; }
            for (int i = 0; i < 5; i++) OverSeat[i] += o.OverSeat[i];
        }
    }

    /// <summary>
    /// Phase 0 の1戦（規定 ＝ SH-a）。溢れは受けた味方の帳簿（`SparkOverRecvFull` ／ `Top` ／ `T`）から。
    /// 衣の見込み ＝ その味方の、前のターンまでの溢れの累計（受け止めて減る分を引かない上限）を、ターンの頭に生きている味方で足した量。
    /// 4割の上限で捨てる見込み ＝ 同じ累計のうち、その味方の最大HPの 4 割を超えた分（戦の終わり）。
    /// </summary>
    static P0Acc FightP0(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: true);
        var a = new P0Acc { N = 1 };
        for (int t = 1; t <= Math.Min(TMax, r.Turns); t++) a.ReachT[t]++;
        if (r.TallyByUnit.TryGetValue("som", out var mt)) { a.Over = mt.SparkOverflow; a.Healed = mt.SparkHealed; }
        var deathT = new Dictionary<int, int>();
        foreach (var ev in r.Events) if (ev.Kind == BattleEventKind.Death && ev.TargetId is int id && !deathT.ContainsKey(id)) deathT[id] = ev.Turn;
        foreach (var u in p)
        {
            if (!r.TallyByUnit.TryGetValue(u.Def.Id, out var at)) continue;
            a.OverFull += at.SparkOverRecvFull; a.OverTop += at.SparkOverRecvTop;
            if (u.Slot is >= 0 and < 5) a.OverSeat[u.Slot] += at.SparkOverRecvFull + at.SparkOverRecvTop;
            long cum = 0, cap = u.MaxHp * SparkTrait.VeilCapPercent / 100;
            int dead = deathT.TryGetValue(u.InstanceId, out int dt) ? dt : int.MaxValue;
            for (int t = 1; t <= 11; t++)
            {
                if (t <= TMax && t <= r.Turns && t <= dead) { a.StockT[t] += cum; a.CapT[t] += Math.Max(0, cum - cap); }
                long o = at.SparkOverRecvT?[t] ?? 0;
                if (t <= TMax) a.OverT[t] += o;
                cum += o;
            }
            a.CapDiscard += Math.Max(0, cum - cap);
        }
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
        Console.WriteLine("# 第308期 Phase 0（指示書 §4・段0 の規定 ＝ SH-a・seed 0..199・verbose）");
        Console.WriteLine();
        foreach (var (n, f) in Boards()) Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）");

        Console.WriteLine();
        Console.WriteLine("## 1. 溢れの内訳（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("溢れ ＝ 光の量 − 実際に増えた HP（`Healed` ／ `Full` のときだけ）。満タンに降った ＝ 降る前から満タンだった味方 ／ 満タンに届いて余った ＝ 傷を埋めて余った分。席 ＝ 前1・前3・中央・後1・後3 が受けた溢れ。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 台 | HP に入った | 溢れ | 満タンに降った ／ 届いて余った | 席ごと（前1 ／ 前3 ／ 中央 ／ 後1 ／ 後3） | " + string.Join(" | ", Enumerable.Range(1, TMax).Select(t => $"T{t}")) + " |");
        Console.WriteLine("|---|---|--:|--:|---|---|" + string.Concat(Enumerable.Range(1, TMax).Select(_ => "--:|")));
        var cells = new List<(S287.Wave W, string B, P0Acc A)>();
        foreach (var w in Waves.Take(3))
            foreach (var (n, f) in Boards())
            {
                var a = MeasureP0(f, w, Seeds);
                cells.Add((w, n, a));
                Console.WriteLine($"| {w.Name} | {n} | {Per0(a.Healed, a.N)} | {Per0(a.Over, a.N)} | {Pct(a.OverFull, a.OverFull + a.OverTop)} ／ {Pct(a.OverTop, a.OverFull + a.OverTop)} | {string.Join(" ／ ", a.OverSeat.Select(x => Per0(x, a.N)))} | "
                    + string.Join(" | ", Enumerable.Range(1, TMax).Select(t => Per0(a.OverT[t], a.ReachT[t]))) + " |");
            }

        Console.WriteLine();
        Console.WriteLine("## 2. 衣の見込み（溢れがそのまま衣になり、受け止めで減らないとした上限）");
        Console.WriteLine();
        Console.WriteLine("ターンの頭に生きている味方が持っている衣（前のターンまでの溢れの累計）の合計。上限 ＝ そのうち各味方の最大HPの 4 割を超えた分（LV-c で捨てる見込み）。1戦の値は戦の終わりの累計で見た捨てる量。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 台 | " + string.Join(" | ", Enumerable.Range(1, TMax).Select(t => $"T{t} 衣 ／ 4割超")) + " | 4割で捨てる（1戦） |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(1, TMax).Select(_ => "---|")) + "--:|");
        foreach (var (w, n, a) in cells)
            Console.WriteLine($"| {w.Name} | {n} | " + string.Join(" | ", Enumerable.Range(1, TMax).Select(t => $"{Per0(a.StockT[t], a.ReachT[t])} ／ {Per0(a.CapT[t], a.ReachT[t])}")) + $" | {Per0(a.CapDiscard, a.N)} |");

        // 3. ツギの板
        Console.WriteLine();
        Console.WriteLine("## 3. ツギの板（元の勝ち台 × ボス）");
        Console.WriteLine();
        long pastes = 0, amt = 0, pastes5 = 0, amt5 = 0, turns5 = 0, n0 = 0;
        int tsugiMax = 0;
        foreach (var s in Enumerable.Range(0, Seeds))
        {
            var p = BattleEngine.Materialize(WinSeat(), BattleContext.PlayerTeam);
            var r = BattleEngine.Run(p, Waves[0].Make(), s, verbose: true);
            var ts = p.First(u => u.Def.Id == "tsugi");
            tsugiMax = ts.MaxHp; n0++; turns5 += Math.Min(5, r.Turns);
            foreach (var ev in r.Events.Where(x => x.Kind == BattleEventKind.Plank && x.ActorId == ts.InstanceId && x.Text == PlankLabels.Paste))
            { pastes++; amt += ev.Amount; if (ev.Turn <= 5) { pastes5++; amt5 += ev.Amount; } }
        }
        Console.WriteLine("| 量 | 値 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| ツギの最大HP ／ その 4 割 | {tsugiMax} ／ {tsugiMax * SparkTrait.VeilCapPercent / 100} |");
        Console.WriteLine($"| 手番の板 1枚の平均（1戦 {Per1(pastes, n0)} 枚） | {Per1(amt, pastes)} |");
        Console.WriteLine($"| T1〜T5 の手番の板 ／ ターン（第307期 Phase 0 の 91.6 は板 ＋ 応急処置 ＋ 回復） | {Per1(amt5, turns5)} |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare`（§5-2）
    // ---------------------------------------------------------------------------------
    static void CompareAll()
    {
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        Console.WriteLine("# 第308期 `compare` 64 行 × ソムの版（seed 0..199・ソムの在席行だけ差し替える・基準は段0 の規定 ＝ SH-a）");
        Console.WriteLine();
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
        Console.WriteLine("| 行 | 版 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | 差 | 最大の落ち |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|--:|");
        int moved = 0, somRows = 0;
        for (int ri = 0; ri < rows.Length; ri++)
        {
            var b = grids[0];
            if (!HasSom(rows[ri].F)) { for (int vi = 1; vi < Vers.Length; vi++) for (int w = 0; w < nw; w++) if (grids[vi][ri, w] != b[ri, w]) moved++; continue; }
            somRows++;
            double m0 = Enumerable.Range(1, nw - 1).Average(w => b[ri, w]);
            for (int vi = 0; vi < Vers.Length; vi++)
            {
                var g = grids[vi];
                double m = Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
                double drop = Enumerable.Range(0, nw).Min(w => g[ri, w] - b[ri, w]);
                Console.WriteLine($"| {rows[ri].Name} | {Vers[vi].Name} | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w]))) + $" | {F1(m)} | {(vi == 0 ? "" : (m - m0).ToString("+0.0;-0.0;0.0"))} | {(vi == 0 ? "" : drop.ToString("+0.0;-0.0;0.0"))} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"ソムのいる行: {somRows} 行。ソムのいない行で動いたセル: **{moved}**（0 であること）");
    }

    // ---------------------------------------------------------------------------------
    // 代表台 × 波 × 版（§5-1 ／ §5-4）
    // ---------------------------------------------------------------------------------
    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cells = new List<(string Board, S287.Wave W, Ver V, Formation F)>();
        foreach (var w in Waves)
        {
            foreach (var (name, f) in Boards())
                foreach (var v in Vers) cells.Add((name, w, v, Apply(f, v)));
            cells.Add(("元の勝ち台（ツギ）", w, Vers[0], WinSeat()));
        }
        var res = new Deep[cells.Count];
        for (int i = 0; i < cells.Count; i++) res[i] = MeasureDeep(cells[i].F, cells[i].W, Seeds);

        Console.WriteLine($"# 第308期 代表台 × 波 × 版（規定 ＝ SH-a ／ LV-a ／ LV-c ／ FO・量 {SparkTrait.Amount}・seed 0..199・verbose）");
        Console.WriteLine();
        foreach (var (name, f) in Boards()) Console.WriteLine($"- {name}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine($"- 元の勝ち台（ツギ）: {Seats(WinSeat())}");
        Console.WriteLine();
        Console.WriteLine("## 表A 勝率（台 × 波・括弧は規定との差）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")));
        foreach (var name in Boards().Select(b => b.Name).Append("元の勝ち台（ツギ）"))
            foreach (var v in name.StartsWith("元の") ? new[] { Vers[0] } : Vers)
                Console.WriteLine($"| {name} | {(name.StartsWith("元の") ? "—" : v.Name)} | " + string.Join(" | ", Waves.Select(w =>
                {
                    int i = cells.FindIndex(c => c.Board == name && c.W == w && c.V == v);
                    int bi = cells.FindIndex(c => c.Board == name && c.W == w && c.V == Vers[0]);
                    return F1(res[i].Win) + (v == Vers[0] ? "" : $"（{(res[i].Win - res[bi].Win):+0.0;-0.0;0.0}）");
                })) + " |");

        foreach (var w in Waves)
        {
            Console.WriteLine();
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 勝率 | 差 | 倒しT | 負けT | 寿命 | ソム 倒れたT | 光 生まれた | 止まった 粛 ／ 痺れ | HP に入った | 溢れ（満タンに ／ 余り） | 衣になった ／ 受け止めた ／ 4割で捨てた | 戦の終わりの衣 ／ 破片 | FO 選んだ ／ 空振り |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|---|---|--:|---|--:|---|---|---|---|");
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].W != w) continue;
                var a = res[i];
                int bi = cells.FindIndex(c => c.Board == cells[i].Board && c.W == w && c.V == Vers[0]);
                bool orig = cells[i].Board.StartsWith("元の");
                Console.WriteLine($"| {cells[i].Board} | {(orig ? "—" : cells[i].V.Name)} | {F1(a.Win)} | {(bi == i ? "" : (a.Win - res[bi].Win).ToString("+0.0;-0.0;0.0"))} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)}{(a.Caps > 0 ? $"（上限 {Pct(a.Caps, a.N)}）" : "")} | "
                    + $"{Dt(a.FirstDeathN, a.FirstDeathT, a.N)} | {(a.SomN == 0 ? "—" : Dt(a.SomDied, a.SomDeathT, a.N))} | {(orig ? "—" : Per1(a.Born, a.N))} | {Per1(a.HushedL, a.N)} ／ {Per1(a.BlockedL, a.N)} | {Per0(a.Healed, a.N)} | "
                    + $"{Per0(a.Overflow, a.N)}（{Per0(a.OverFull, a.N)} ／ {Per0(a.OverTop, a.N)}） | {Per0(a.VeilAdded, a.N)} ／ {Per0(a.VeilSoaked, a.N)} ／ {Per0(a.VeilCapped, a.N)} | {Per0(a.VeilLeft, a.N)} ／ {Per0(a.ArmorLeft, a.N)} | "
                    + $"{(cells[i].V.To.Traits.Contains(TraitId.SparkFocus) ? $"{Per1(a.FocusPicks, a.N)} ／ {Per1(a.FocusNone, a.N)}" : "—")} |");
            }
            if (w.Boss)
            {
                Console.WriteLine();
                Console.WriteLine("### ボス: 1ターンあたりの「HP に入った光 ＋ 衣が受け止めた量」と、勇者の与ダメに対する割合（元の勝ち台はツギが書いた量 ＝ 板 ＋ 回復）");
                Console.WriteLine();
                Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Enumerable.Range(1, TMax).Select(t => $"T{t}")) + " | 勇者の与ダメ ／ 戦 | 割合 |");
                Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(1, TMax).Select(_ => "--:|")) + "--:|--:|");
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].W != w) continue;
                    var a = res[i];
                    bool orig = cells[i].Board.StartsWith("元の");
                    long eff = orig ? a.TsugiOut : a.Healed + a.VeilSoaked;
                    Console.WriteLine($"| {cells[i].Board} | {(orig ? "—" : cells[i].V.Name)} | " + string.Join(" | ", Enumerable.Range(1, TMax).Select(t => orig ? "" : Per0(a.HealT[t] + a.SoakT[t], a.ReachT[t])))
                        + $" | {Per0(a.HeroDmg, a.N)} | {Pct(eff, a.HeroDmg)} |");
                }
            }
        }

        Console.WriteLine();
        Console.WriteLine("## 対照（勝率 50% 以上のセル）: ソム → ドルガ（同じ席・seed 0..199・非 verbose）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 台 | 版 | 勝率 | ソム → ドルガ | ソムが要る（半分以下） |");
        Console.WriteLine("|---|---|---|--:|--:|---|");
        for (int i = 0; i < cells.Count; i++)
        {
            if (res[i].Win < 50 || res[i].SomN == 0) continue;
            var f = cells[i].F;
            var cur = f.Occupied().First(o => o.Def.Id == "som").Def;
            double dv = 100.0 * WinsPar(FvSwap(f, cur, UnitCatalog.Dolga), cells[i].W, Seeds) / Seeds;
            Console.WriteLine($"| {cells[i].W.Name} | {cells[i].Board} | {cells[i].V.Name} | {F1(res[i].Win)} | {F1(dv)} | {(dv * 2 <= res[i].Win ? "○" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 格子（§5-3）——第307期と同じ作り・同じ候補（`Som307Diag.GridCore` ／ `BossPool` ／ `ElitePool`）
    // ---------------------------------------------------------------------------------
    static void GridBoss(string ver)
    {
        var vars = new List<(string, Func<Formation, Formation>)> { ("規定（SH-a）", f => f) };
        if (ver != "") { var v = AnyVer(ver); vars.Add((v.Name, f => Apply(f, v))); }
        S307.GridCore($"固定枠 クグ ＋ カタ ＋ ソム（{string.Join(" ／ ", vars.Select(x => x.Item1))}）", Waves[0], new[] { UnitCatalog.Kugu, UnitCatalog.Kata, UnitCatalog.Som }, S307.BossPool, 2, vars.ToArray(), "第308期");
    }

    static void GridElite(string waveName, string ver)
    {
        var v = AnyVer(ver);
        var vars = new (string, Func<Formation, Formation>)[] { ("規定（SH-a）", f => f), (v.Name, f => Apply(f, v)) };
        S307.GridCore($"固定枠 トウ ＋ ソム（規定 ／ {v.Name}）", S307.WaveOf(waveName), new[] { UnitCatalog.Tou, UnitCatalog.Som }, S307.ElitePool, 3, vars, "第308期");
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

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# som308 check —— 第308期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var som = UnitCatalog.Som; var old = UnitCatalog.SomH307; var sha = UnitCatalog.SomSHa;

        // (a) 段0 の定義
        {
            bool ok = som.Traits.SequenceEqual(old.Traits.Append(TraitId.SparkRain)) && som.Traits.SequenceEqual(sha.Traits) && som.PlusText == sha.PlusText && som.Flavor == sha.Flavor
                && som.MinusText == old.MinusText && som.MaxHp == old.MaxHp && som.Attack == old.Attack && som.Speed == old.Speed && som.Name == old.Name
                && UnitCatalog.All.Contains(som) && !UnitCatalog.Everyone.Contains(old) && old.Traits.SequenceEqual(new[] { TraitId.BetrayedShockNoThunder })
                && UnitCatalog.SomSMa.Traits.SequenceEqual(old.Traits.Append(TraitId.StaticMembrane)) && UnitCatalog.SomSHb.Traits.SequenceEqual(old.Traits.Append(TraitId.SparkStore))
                && Vers.Skip(1).All(v => !UnitCatalog.Everyone.Contains(v.To) && v.To.MaxHp == som.MaxHp && v.To.MinusText == som.MinusText)
                && UnitCatalog.SomLVa.Traits.SequenceEqual(som.Traits.Append(TraitId.SparkVeil)) && UnitCatalog.SomLVc.Traits.SequenceEqual(som.Traits.Append(TraitId.SparkVeil).Append(TraitId.SparkVeilCap))
                && UnitCatalog.SomFO.Traits.SequenceEqual(som.Traits.Append(TraitId.SparkFocus));
            Expect("(a) 段0: 規定のソム ＝ 旧の規定（`SomH307`）＋ `SparkRain`（＝ 第307期の SH-a と同じ札・文面・フレーバー）・旧は `All` ／ `Everyone` の外・第294 ／ 307期の版は旧から作る・段1 の版は規定 ＋ 札", ok);
        }
        // (b) ヒーラーの一覧
        {
            var hp = B283.HealPool.Select(Short).ToArray(); var h306 = B283.HealPool306.Select(Short).ToArray(); var h295 = B283.HealPool295.Select(Short).ToArray();
            bool ok = hp.Contains("ソム") && !h306.Contains("ソム") && !h295.Contains("ソム") && hp.Where(x => x != "ソム").SequenceEqual(h306)
                && h306.SequenceEqual(new[] { "ヴェル", "リリ", "シオ", "ササ", "ヒビ", "ツギ", "ベニ", "ヒサ" }) && h295.SequenceEqual(new[] { "ヴェル", "リリ", "シオ", "ササ", "ヒビ", "ツギ", "ベニ" });
            Expect("(b) `HealPool` に規定のソムが入る・`HealPool306` ／ `HealPool295` は第307期の一覧のまま", ok, $"{string.Join("・", hp)} ／ {string.Join("・", h306)} ／ {string.Join("・", h295)}");
        }

        BattleContext Ctx(UnitDef somDef, out List<UnitState> p, out List<UnitState> e, Formation? pl = null, UnitDef? enemyCenter = null)
        {
            var ctx = H304.Ctx(pl ?? Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Golm, center: somDef),
                               Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Dolga, center: enemyCenter ?? UnitCatalog.Dolga), out p, out e);
            foreach (var u in p.Concat(e)) { u.MaxHp = 1000; u.Hp = 1000; }
            return ctx;
        }
        UnitState SomOf(List<UnitState> p) => p.First(u => u.Def.Id == "som");
        UnitState Pop(BattleContext ctx, List<UnitState> e, UnitState by) { ctx.MarkShock(e[0], by); ctx.ApplyDamage(e[0], 10, by); return e[0]; }

        // (c) 溢れだけが衣になる
        {
            var ctx = Ctx(UnitCatalog.SomLVa, out var p, out var e);
            var s = SomOf(p); var d = p.First(u => u.Def.Id == "dolga"); var g = p.First(u => u.Def.Id == "golm");
            d.Hp = 997;   // 3 だけ傷ついている → 7 のうち 3 が HP・4 が衣
            g.Hp = 500;   // 深い傷 → 7 全部が HP・衣 0
            Pop(ctx, e, d);
            int dArm = d.RawCounter(StatusKeys.Armor), gArm = g.RawCounter(StatusKeys.Armor), sArm = s.RawCounter(StatusKeys.Armor);
            Expect("(c) 溢れだけが衣になる（HP に入った分は衣にならない）", d.Hp == 1000 && dArm == 4 && g.Hp == 507 && gArm == 0 && sArm == 7 && H304.Tal(ctx, "som").SparkVeilAdded == 11,
                $"ドルガ HP {d.Hp}・衣 {dArm} ／ ゴルム HP {g.Hp}・衣 {gArm} ／ ソム 衣 {sArm}");
        }
        // (d) 支援拒否 ／ 渇き ／ ベニの反転で通らなかった光は衣にならない
        {
            var ctx = Ctx(UnitCatalog.SomLVa, out var p, out var e, pl: Formation.Build(front1: UnitCatalog.Gald, center: UnitCatalog.Beni, back3: UnitCatalog.SomLVa));
            var s = SomOf(p); var gald = p.First(u => u.Def.Id == "gald"); var beni = p.First(u => u.Def.Id == "beni");
            foreach (var u in p) u.SetCounter(StatusKeys.Armor, 0);
            Pop(ctx, e, s);
            bool stoicBeni = gald.RawCounter(StatusKeys.Armor) == 0 && s.RawCounter(StatusKeys.Armor) == 0 && beni.RawCounter(StatusKeys.Armor) == 0;
            var ctxd = Ctx(UnitCatalog.SomLVa, out var pd, out var ed, enemyCenter: EnemyCatalog.Droughter);
            var dd2 = pd.First(u => u.Def.Id == "dolga");
            Pop(ctxd, ed, dd2);
            bool drought = pd.All(u => u.RawCounter(StatusKeys.Armor) == 0) && H304.Tal(ctxd, "som").SparkRefused > 0;
            Expect("(d) 支援拒否（ガルド）／ ベニの反転 ／ 渇きで通らなかった光は衣にならない", stoicBeni && drought,
                $"ガルド {gald.RawCounter(StatusKeys.Armor)}・ベニ {beni.RawCounter(StatusKeys.Armor)}・ソム（ベニの隣）{s.RawCounter(StatusKeys.Armor)}・渇き 通らない {H304.Tal(ctxd, "som").SparkRefused}");
        }
        // (e) LV-c: 衣は最大HPの 4 割で止まり、ほかの書き手の破片も合算
        {
            var ctx = Ctx(UnitCatalog.SomLVc, out var p, out var e);
            var d = p.First(u => u.Def.Id == "dolga");
            d.MaxHp = 100; d.Hp = 100; d.SetCounter(StatusKeys.Armor, 38);   // ほかの書き手の破片 38 → 衣は 2 だけ
            Pop(ctx, e, d);
            int a1 = d.RawCounter(StatusKeys.Armor);
            Pop(ctx, e, d);   // もう一度降っても 40 のまま
            Expect("(e) LV-c: 衣は最大HPの 4 割で止まる（ほかの書き手の破片も合算・超えた分は捨てる）", a1 == 40 && d.RawCounter(StatusKeys.Armor) == 40 && H304.Tal(ctx, "som").SparkVeilCapped >= 12,
                $"破片 38 → {a1} → {d.RawCounter(StatusKeys.Armor)}・捨てた {H304.Tal(ctx, "som").SparkVeilCapped}");
        }
        // (f) FO: 光1つごとに最も傷ついた味方を選び直す
        {
            var ctx = Ctx(UnitCatalog.SomFO, out var p, out var e);
            var s = SomOf(p); var d = p.First(u => u.Def.Id == "dolga"); var g = p.First(u => u.Def.Id == "golm");
            d.MaxHp = 100; d.Hp = 50; g.MaxHp = 100; g.Hp = 60;
            ctx.MarkShock(e[0], s); ctx.MarkShock(e[2], s);   // 隣り合う2体（前1 ・中央）が連鎖で弾ける → 光 2
            ctx.ApplyDamage(e[0], 10, s);
            var t = H304.Tal(ctx, "som");
            // 1つ目: ドルガ（50%）に 35 → 85。2つ目: ゴルム（60%）に 35 → 95
            Expect("(f) FO: 光1つにつき 35 を最も傷ついた味方に・光ごとに選び直す（ソム自身は選ばない）", t.SparkRainLights == 2 && t.SparkFocusPicks == 2 && d.Hp == 85 && g.Hp == 95 && s.RawCounter(StatusKeys.Armor) == 0,
                $"光 {t.SparkRainLights}・ドルガ {d.Hp}・ゴルム {g.Hp}");
        }
        // (g) 衣が受け止めた量: 破片が減ると衣から先に減る
        {
            var ctx = Ctx(UnitCatalog.SomLVa, out var p, out var e);
            var d = p.First(u => u.Def.Id == "dolga");
            Pop(ctx, e, d);   // 満タン → 7 が衣
            int before = d.Hp;
            ctx.ApplyDamage(d, 5, e[1]);
            var t = H304.Tal(ctx, "dolga");
            Expect("(g) 衣は一撃を受け止める（破片の減りが衣の帳簿に数えられる）", d.Hp == before && d.RawCounter(StatusKeys.Armor) == 2 && t.SparkVeilSoaked == 5 && d.RawCounter(SparkTrait.VeilKey) == 2,
                $"受け止めた {t.SparkVeilSoaked}・残りの衣 {d.RawCounter(SparkTrait.VeilKey)}");
        }
        // (h) 規定のソム（SH-a）の戦では衣が1度も生まれない
        {
            long veil = 0; int ev = 0, foc = 0;
            foreach (var (n, f) in Boards())
                foreach (var w in Waves.Take(4))
                    for (int sd = 0; sd < 3; sd++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        ev += r.Events.Count(x => x.Kind == BattleEventKind.Spark && x.Text == SparkLabels.Veil);
                        foreach (var t in r.TallyByUnit.Values) { veil += t.SparkVeilAdded + t.SparkVeilSoaked + t.SparkVeilCapped; foc += (int)(t.SparkFocusPicks + t.SparkFocusNone); }
                    }
            Expect("(h) 規定のソム（SH-a）の戦では衣も寄せも1度も起きない（計数 0・「衣」の出来事 0）", veil == 0 && ev == 0 && foc == 0, $"{veil} ／ {ev} ／ {foc}");
        }
        // (i) 決定的・verbose の有無で勝敗と決着T が同じ
        {
            int nd = 0, n = 0;
            foreach (var (bn, f) in Boards())
                foreach (var v in Vers)
                    foreach (var w in Waves)
                        for (int sd = 0; sd < 4; sd++)
                        {
                            var fv = Apply(f, v);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            n++;
                            if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) nd++;
                        }
            Expect("(i) 決定的・verbose の有無で勝敗と決着T が変わらない（規定 ＋ 版 3 枚 × 代表台 × 8 波）", nd == 0, $"{nd} ／ {n} 件");
        }
        // (j) 乱数
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
            var notes = new List<string>(); bool clean = true;
            foreach (var path in new[] { "BattleCore/BattleEngine.cs", "BattleCore/Traits.cs" })
            {
                string now = File.ReadAllText(path); string? head = Head(path);
                if (head is null) { clean = false; notes.Add($"{path}: HEAD を読めない"); continue; }
                foreach (var pat in new[] { "PickOne(", "Roll(" }) { int a0 = Count(head, pat), a1 = Count(now, pat); if (a1 > a0) clean = false; notes.Add($"{Path.GetFileName(path)} {pat} {a0}→{a1}"); }
            }
            Expect("(j) `PickOne(` ／ `Roll(` を新たに使っていない（BattleCore の出現数が HEAD 以下・FO は既存の `MostHurtAlly` を呼ぶ）", clean, string.Join("・", notes));
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
