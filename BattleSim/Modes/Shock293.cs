using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using K292 = Kugu292Diag;

// =====================================================================================
// shock293 —— 第293期「カタ KR-∞ の規定化 ＋ クグの網（KW-a ／ KW-b）＋ シガの連鎖の鞭（SW-a ／ SW-b）」。
// 指示書は design/PHASE293_SHOCK_WEB_SPEC.md ／ 報告は design/PHASE293_SHOCK_WEB.md。
//
//     dotnet run --project BattleSim -c Release 0 shock293 p0          # Phase 0（§5）: 精鋭の組み付き手番・敵陣の隣の数・割り込みの合図の連鎖の大きさ
//     dotnet run --project BattleSim -c Release 0 shock293 compare     # `compare` 64 行 × 版（規定 ／ KW-a ／ KW-b ／ SW-a ／ SW-b・その駒の在席行だけ）
//     dotnet run --project BattleSim -c Release 0 shock293 boards      # 代表台 × ボス ／ 近衛 ／ 大隊 × クグの版 × シガの版（seed 0..199・verbose）と対照
//     dotnet run --project BattleSim -c Release 0 shock293 gridboss    # ボスの格子（固定枠 クグ KW-a ＋ カタ ＋ シガ・探索枠2・× 席 120）× シガ SI-b ／ SW-a ／ SW-b
//     dotnet run --project BattleSim -c Release 0 shock293 grid <guard|bat> <kugu|shiga> <版>   # 精鋭の格子（固定枠 トウ ＋ クグ ／ トウ ＋ シガ・探索枠3）
//     dotnet run --project BattleSim -c Release 0 shock293 deep <boss|guard|bat> <席の並び> [版…]
//     dotnet run --project BattleSim -c Release 0 shock293 check       # 自己検査
//
// 版の名前（ASCII）: クグ kgb（規定）／ kba ／ kwa ／ kwb、シガ sib（規定）／ swa ／ swb、カタ krinf（規定）／ krb。
// =====================================================================================
static class Shock293Diag
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
            case "gridboss": GridBoss(); return;
            case "grid": GridElite(A(3, "guard"), A(4, "kugu"), A(5, "kwa")); return;
            case "deep": DeepOne(A(3, "boss"), A(4, "トウ・ゴルム・クグ・カタ・ツギ"), args.Skip(5).ToArray()); return;
            case "check": Check(); return;
            default: Console.WriteLine("shock293: モードは p0 / compare / boards / gridboss / grid / deep / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・波・台
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef From, UnitDef To);
    // 第294期: 規定のクグ ／ シガは KW-a ／ SW-a になった。台は `Seat` ／ `K292.Pin294` で第293期の規定（KG-b ＝ `KuguKGb` ／ SI-b ＝ `ShigaSIb`）に固定してある。
    internal static readonly Ver[] KuguVers =
    {
        new("KG-b", "kgb", UnitCatalog.KuguKGb, UnitCatalog.KuguKGb), new("KB-a", "kba", UnitCatalog.KuguKGb, UnitCatalog.KuguKBa),
        new("KW-a", "kwa", UnitCatalog.KuguKGb, UnitCatalog.KuguKWa), new("KW-b", "kwb", UnitCatalog.KuguKGb, UnitCatalog.KuguKWb),
    };
    internal static readonly Ver[] ShigaVers =
    {
        new("SI-b", "sib", UnitCatalog.ShigaSIb, UnitCatalog.ShigaSIb), new("SW-a", "swa", UnitCatalog.ShigaSIb, UnitCatalog.ShigaSWa), new("SW-b", "swb", UnitCatalog.ShigaSIb, UnitCatalog.ShigaSWb),
    };
    internal static readonly Ver[] KataVers =
    {
        new("KR-∞", "krinf", UnitCatalog.Kata, UnitCatalog.Kata), new("KR-b", "krb", UnitCatalog.Kata, UnitCatalog.KataKRb),
    };
    static Ver AnyVer(string n) => KuguVers.Concat(ShigaVers).Concat(KataVers).First(v => v.Ascii == n || v.Name == n);
    static Formation Apply(Formation f, params Ver[] vs) { foreach (var v in vs) if (!ReferenceEquals(v.From, v.To)) f = FvSwap(f, v.From, v.To); return f; }
    static bool Has(Formation f, UnitDef d) => f.Occupied().Any(o => ReferenceEquals(o.Def, d));

    static S287.Wave WaveOf(string n) => S287.Waves.First(w => w.Name == (n switch { "boss" => "ボス", "guard" => "近衛", "bat" => "大隊", _ => n }));
    const int Seeds = 200, CutSeeds = 20;

    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static UnitDef ByShort(string n) => UnitCatalog.All.First(d => Short(d) == n);
    static UnitDef[] Order(string s) => s.Split('・', StringSplitOptions.RemoveEmptyEntries).Select(ByShort).ToArray();
    static string OrderName(UnitDef[] o) => string.Join("・", o.Select(Short));
    static string Seats(Formation f) => string.Join("・", Enumerable.Range(0, 5).Select(i => f[i] is { } d ? Short(d) : "—"));
    static Formation Seat(UnitDef[] o) => K292.Pin294(B283.Seat(o));   // 第294期: クグ ／ シガを第293期の規定（KG-b ／ SI-b）に固定

    /// <summary>代表台（指示書 §6-2）: 試遊プリセット3台 ＋ 第292期の代表台5台 ＋ 第292期のボスの勝ち台。</summary>
    internal static (string Name, Formation F)[] Boards()
    {
        var l = new List<(string, Formation)>();
        foreach (var n in new[] { "試遊・感電 火の型", "試遊・感電 雷の型", "試遊・感電 糸" }) l.Add((n, K292.Pin294(Presets.Playtest.First(r => r.Name == n).F)));
        foreach (var (n, f) in Kugu292Diag.Boards()) if (n != "試遊・感電 糸") l.Add(("292 " + n, f));
        l.Add(("292 ボスの勝ち台", Seat(Order("トウ・ゴルム・クグ・カタ・ツギ"))));
        return l.ToArray();
    }

    /// <summary>台の版の組（その駒がいる版だけ・クグ × シガ）。</summary>
    static IEnumerable<(string Name, Formation F, Ver? K, Ver? S)> Variants(Formation f)
    {
        var ks = Has(f, UnitCatalog.KuguKGb) ? KuguVers.Select(v => (Ver?)v) : new Ver?[] { null };
        var ss = Has(f, UnitCatalog.ShigaSIb) ? ShigaVers.Select(v => (Ver?)v) : new Ver?[] { null };
        foreach (var k in ks)
            foreach (var s in ss)
            {
                var g = f;
                if (k is not null) g = Apply(g, k);
                if (s is not null) g = Apply(g, s);
                yield return (string.Join(" × ", new[] { k?.Name, s?.Name }.Where(x => x is not null)), g, k, s);
            }
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal static readonly int[] Marks = { 1, 3, 5, 10, 20 };

    internal sealed class Deep
    {
        public long N, Wins, WinT, LoseT, FoeDis;
        // クグ
        public long GN, GHeld, GTurns, WebSpun, WebAdj, WebBalls, WebNone, WebRecharged, WebPops, SilkPlaced, SilkPops;
        public double[] WebbedAt = new double[Marks.Length], BallsAt = new double[Marks.Length];
        // シガ
        public long SN, SwFires, SwMultSum, SwMultN, SwWhip, ShDmg, ChargeGains, ChargeEnd, ChargeOnShocked;
        public long[] ChainHist = new long[12];
        // カタ
        public long KN, Casts, Hits, KNom, KDmg;
        public double[] CloudAt = new double[Marks.Length];
        // 勇者
        public long HeroN;
        public double[] HeroHpAt = new double[Marks.Length];
        // 倒れた
        public long GDied, GDeathT, SDied, SDeathT, KDied, KDeathT, TDied, TDeathT;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Merge(Deep o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LoseT += o.LoseT; FoeDis += o.FoeDis;
            GN += o.GN; GHeld += o.GHeld; GTurns += o.GTurns; WebSpun += o.WebSpun; WebAdj += o.WebAdj; WebBalls += o.WebBalls; WebNone += o.WebNone; WebRecharged += o.WebRecharged; WebPops += o.WebPops;
            SilkPlaced += o.SilkPlaced; SilkPops += o.SilkPops;
            SN += o.SN; SwFires += o.SwFires; SwMultSum += o.SwMultSum; SwMultN += o.SwMultN; SwWhip += o.SwWhip; ShDmg += o.ShDmg; ChargeGains += o.ChargeGains; ChargeEnd += o.ChargeEnd; ChargeOnShocked += o.ChargeOnShocked;
            for (int i = 0; i < 12; i++) ChainHist[i] += o.ChainHist[i];
            KN += o.KN; Casts += o.Casts; Hits += o.Hits; KNom += o.KNom; KDmg += o.KDmg; HeroN += o.HeroN;
            GDied += o.GDied; GDeathT += o.GDeathT; SDied += o.SDied; SDeathT += o.SDeathT; KDied += o.KDied; KDeathT += o.KDeathT; TDied += o.TDied; TDeathT += o.TDeathT;
            for (int i = 0; i < Marks.Length; i++) { WebbedAt[i] += o.WebbedAt[i]; BallsAt[i] += o.BallsAt[i]; CloudAt[i] += o.CloudAt[i]; HeroHpAt[i] += o.HeroHpAt[i]; }
        }
    }

    internal static Deep FightDeep(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var d = new Deep { N = 1 };
        if (r.PlayerWon) { d.Wins = 1; d.WinT = r.Turns; } else d.LoseT = r.Turns;
        var mine = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit) if (!mine.Contains(id) && id != UnitCatalog.SilkBall.Id) d.FoeDis += t.DischargeTaken;
        UnitState? kg = p.FirstOrDefault(u => u.Def.Id == "kugu"), sh = p.FirstOrDefault(u => u.Def.Id == "shiga"), ka = p.FirstOrDefault(u => u.Def.Id == "kata"), to = p.FirstOrDefault(u => u.Def.Id == "tou");
        if (kg is not null && r.TallyByUnit.TryGetValue("kugu", out var gt))
        {
            d.GN = 1; d.GHeld = gt.KuguHeldTurns; d.GTurns = gt.KuguTurns; d.WebSpun = gt.WebSpun; d.WebAdj = gt.WebAdjacent; d.WebBalls = gt.WebBalls; d.WebNone = gt.WebNone;
            d.WebRecharged = gt.WebRecharged; d.WebPops = gt.WebPops; d.SilkPlaced = gt.SilkPlaced; d.SilkPops = gt.SilkPops;
        }
        if (sh is not null && r.TallyByUnit.TryGetValue("shiga", out var st))
        {
            d.SN = 1; d.SwFires = st.SwFires; d.SwMultSum = st.SwMultSum; d.SwMultN = st.SwMultN; d.SwWhip = st.SwWhipDealt; d.ShDmg = st.DamageToEnemy; d.ChargeGains = st.ChargeGains;
            d.ChargeEnd = sh.RawCounter(StoredChargeTrait.Key); d.ChargeOnShocked = st.ChargeOnShocked;
        }
        if (ka is not null && r.TallyByUnit.TryGetValue("kata", out var kt)) { d.KN = 1; d.Casts = kt.ThunderCasts; d.Hits = kt.ThunderHits; d.KNom = kt.ThunderNominal; d.KDmg = kt.DamageToEnemy; }
        UnitState? hero = e.FirstOrDefault(u => u.Def == EnemyCatalog.BossRegular);
        if (hero is not null) d.HeroN = 1;
        var webbed = new HashSet<int>();
        int balls = 0, cloud = 0, heroHp = hero?.MaxHp ?? 0;
        var webEnd = new int[31]; var ballEnd = new int[31]; var cloudEnd = new int[31]; var hpEnd = new int[31];
        int lastT = 0;
        void Close(int upto) { for (int t = lastT; t <= Math.Min(30, upto); t++) { webEnd[t] = webbed.Count; ballEnd[t] = balls; cloudEnd[t] = cloud; hpEnd[t] = heroHp; } lastT = Math.Min(31, upto + 1); }
        foreach (var ev in r.Events)
        {
            if (ev.Turn > lastT) Close(ev.Turn - 1);
            switch (ev.Kind)
            {
                case BattleEventKind.Web when ev.Text == WebLabels.Spin: webbed.Add(ev.TargetId!.Value); break;
                case BattleEventKind.SilkBall when ev.Text == SilkBallLabels.Place: balls++; break;
                case BattleEventKind.ShockGauge when ev.Text == ShockGaugeLabels.Cloud && ka is not null && ev.TargetId == ka.InstanceId: cloud = ev.Amount; break;
                case BattleEventKind.ShockGauge when ev.Text == ShockGaugeLabels.Interrupt && sh is not null && ev.ActorId == sh.InstanceId: d.ChainHist[Math.Min(ev.Slot, 11)]++; break;
                case BattleEventKind.Damage when hero is not null && ev.TargetId == hero.InstanceId: heroHp = ev.HpAfter; break;
                case BattleEventKind.Heal when hero is not null && ev.TargetId == hero.InstanceId: heroHp = ev.HpAfter; break;
                case BattleEventKind.Death:
                    if (ev.TargetId is int dt) webbed.Remove(dt);
                    if (kg is not null && ev.TargetId == kg.InstanceId && d.GDied == 0) { d.GDied = 1; d.GDeathT = ev.Turn; }
                    if (sh is not null && ev.TargetId == sh.InstanceId && d.SDied == 0) { d.SDied = 1; d.SDeathT = ev.Turn; }
                    if (ka is not null && ev.TargetId == ka.InstanceId && d.KDied == 0) { d.KDied = 1; d.KDeathT = ev.Turn; }
                    if (to is not null && ev.TargetId == to.InstanceId && d.TDied == 0) { d.TDied = 1; d.TDeathT = ev.Turn; }
                    break;
            }
        }
        Close(30);
        for (int i = 0; i < Marks.Length; i++)
        {
            int t = Math.Min(Marks[i], r.Turns);
            d.WebbedAt[i] = webEnd[t]; d.BallsAt[i] = ballEnd[t]; d.CloudAt[i] = cloudEnd[t]; d.HeroHpAt[i] = hpEnd[t];
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
    static string Avg(double s, long n, string fmt = "F1") => n == 0 ? "—" : (s / n).ToString(fmt);
    static string Dt(long died, long t, long n) => died == 0 ? "—" : $"{Per1(t, died)}（{Pct(died, n)}）";
    static string ChainAvg(long[] h) { long n = h.Sum(); return n == 0 ? "—" : (h.Select((c, i) => (double)c * i).Sum() / n).ToString("F2"); }

    // ---------------------------------------------------------------------------------
    // Phase 0（§5）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第293期 Phase 0 —— 精鋭の組み付き手番・敵陣の隣・割り込みの合図の連鎖（規定の駒・段0 の後・seed 0..199・verbose）");
        Console.WriteLine();
        Console.WriteLine("## §5 の 1 クグが組み付いていた手番（1戦）と、KW で張れる糸の見込み");
        Console.WriteLine();
        Console.WriteLine("組んでいた ＝ クグの手番の頭で `grappleTarget` が生きている敵を指していた手番（第290期の計数）。KW はその手番ごとに1本（敵が残っていれば）なので、**見込みの本数 ≒ 組んでいた手番**（新しく組み付いた手番も含む）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 決着T | クグの手番 | 組んでいた | 敵の数 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in Boards().Where(b => Has(b.F, UnitCatalog.KuguKGb)))
            foreach (var w in S287.Waves.Skip(1))
            {
                var a = MeasureDeep(f, w, Seeds);
                Console.WriteLine($"| {name} | {w.Name} | {F1(a.Win)} | {Per1(a.WinT + a.LoseT, a.N)} | {Per(a.GTurns, a.N)} | {Per(a.GHeld, a.N)} | {w.Make().Count} |");
            }
        Console.WriteLine();
        Console.WriteLine("## §5 の 2 敵陣の席ごとの「隣の敵」の数（全員が生きているとき）");
        Console.WriteLine();
        foreach (var w in S287.Waves)
        {
            var e = w.Make();
            Console.WriteLine($"- **{w.Name}**: " + string.Join("・", e.OrderBy(u => u.Slot).Select(u => $"{FormationRules.SeatNames[u.Slot]} {e.Count(v => v != u && FormationRules.AreAdjacent(u, v))}")));
        }
        Console.WriteLine();
        Console.WriteLine("## §5 の 5 シガの割り込みの合図の連鎖で弾けた数（規定 SI-b・糸玉は第292期の KB-a）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 割り込み（1戦） | 合図の大きさの平均 | 分布 1 ／ 2 ／ 3 ／ 4 ／ 5+ | SW-a の倍率の見込み（1 ＋ 平均） |");
        Console.WriteLine("|---|---|---|--:|--:|---|--:|");
        foreach (var (name, f) in Boards().Where(b => Has(b.F, UnitCatalog.ShigaSIb)))
            foreach (var w in S287.Waves)
                foreach (var kv in Has(f, UnitCatalog.KuguKGb) ? new[] { KuguVers[0], KuguVers[1] } : new[] { KuguVers[0] })
                {
                    var a = MeasureDeep(Apply(f, kv), w, Seeds);
                    var h = a.ChainHist;
                    long n = h.Sum();
                    string avg = ChainAvg(h);
                    Console.WriteLine($"| {name} | {w.Name} | {kv.Name} | {Per(a.SwFires, a.SN)} | {avg} | {Pct(h[1], n)} ／ {Pct(h[2], n)} ／ {Pct(h[3], n)} ／ {Pct(h[4], n)} ／ {Pct(h.Skip(5).Sum(), n)} | {(n == 0 ? "—" : (1 + double.Parse(avg)).ToString("F2"))} |");
                }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` 64 行 × 版
    // ---------------------------------------------------------------------------------
    static double[,] CompareGrid(Ver? v)
    {
        var rows = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            var f = v is null ? rows[ri].F : Apply(rows[ri].F, v);
            int wins = 0;
            for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / Seeds;
        });
        return g;
    }

    static void CompareAll()
    {
        var rows = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count;
        Console.WriteLine("# 第293期 `compare` 64 行 × 版（seed 0..199・その駒の在席行だけ差し替える・基準は段0 の後の規定）");
        var basis = CompareGrid(null);
        var prim = Baseline.PrimaryRows.ToHashSet();
        foreach (var set in new[] { KuguVers.Skip(1).ToArray(), ShigaVers.Skip(1).ToArray() })
        {
            Console.WriteLine();
            Console.WriteLine($"## {Short(set[0].From)}（規定 ／ {string.Join(" ／ ", set.Select(v => v.Name))}）");
            Console.WriteLine();
            Console.WriteLine("| 行 | 版 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | 差 | 最大の落ち | 主判定 |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|--:|---|");
            var grids = set.Select(v => CompareGrid(v)).ToArray();
            int movedOther = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                bool has = Has(rows[ri].F, set[0].From);
                if (!has)
                {
                    for (int vi = 0; vi < set.Length; vi++) for (int w = 0; w < nw; w++) if (grids[vi][ri, w] != basis[ri, w]) movedOther++;
                    continue;
                }
                double m0 = Enumerable.Range(1, nw - 1).Average(w => basis[ri, w]);
                Console.WriteLine($"| {rows[ri].Name} | 規定 | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => F1(basis[ri, w]))) + $" | {F1(m0)} | | | {(prim.Contains(rows[ri].Name) ? "○" : "")} |");
                for (int vi = 0; vi < set.Length; vi++)
                {
                    var g = grids[vi];
                    double m = Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
                    double drop = Enumerable.Range(0, nw).Min(w => g[ri, w] - basis[ri, w]);
                    Console.WriteLine($"| {rows[ri].Name} | {set[vi].Name} | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w]))) + $" | {F1(m)} | {(m - m0):+0.0;-0.0;0.0} | {drop:+0.0;-0.0;0.0} | |");
                }
            }
            Console.WriteLine();
            Console.WriteLine($"その駒のいない行で動いたセル: **{movedOther}**（0 であること）");
        }
    }

    // ---------------------------------------------------------------------------------
    // 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cells = new List<(string Board, S287.Wave W, string V, Formation F, Ver? K, Ver? S)>();
        foreach (var w in S287.Waves)
            foreach (var (name, f) in Boards())
                foreach (var v in Variants(f)) cells.Add((name, w, v.Name, v.F, v.K, v.S));
        var res = new Deep[cells.Count];
        for (int i = 0; i < cells.Count; i++) res[i] = MeasureDeep(cells[i].F, cells[i].W, Seeds);

        Console.WriteLine("# 第293期 代表台 × 波 × クグ（KG-b ／ KB-a ／ KW-a ／ KW-b）× シガ（SI-b ／ SW-a ／ SW-b）（カタは規定 KR-∞・seed 0..199・verbose）");
        Console.WriteLine();
        foreach (var (name, f) in Boards()) Console.WriteLine($"- {name}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine();
        Console.WriteLine("差 ＝ 同じ台・同じ波の規定（KG-b × SI-b）との勝率の差。");
        foreach (var w in S287.Waves)
        {
            Console.WriteLine();
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("### 表A 勝率・クグ");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 勝率 | 差 | 倒しT | 負けT | 組んでいた手番 | 張った糸 ／ うち隣 | 糸玉（網 ／ 全体） | 糸の敵 T1 ／ T3 ／ T5 ／ T10 | 帯電し直し | 糸の敵が弾けた | 敵への放電 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].W != w) continue;
                var a = res[i];
                int bi = cells.FindIndex(c => c.W == w && c.Board == cells[i].Board);
                double diff = a.Win - res[bi].Win;
                Console.WriteLine($"| {cells[i].Board} | {cells[i].V} | {F1(a.Win)} | {(bi == i ? "" : diff.ToString("+0.0;-0.0;0.0"))} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)} | {Per(a.GHeld, a.GN)} | "
                    + $"{Per(a.WebSpun, a.GN)} ／ {Per(a.WebAdj, a.GN)} | {Per(a.WebBalls, a.GN)} ／ {Per(a.SilkPlaced, a.GN)} | {Avg(a.WebbedAt[0], a.N)} ／ {Avg(a.WebbedAt[1], a.N)} ／ {Avg(a.WebbedAt[2], a.N)} ／ {Avg(a.WebbedAt[3], a.N)} | "
                    + $"{Per(a.WebRecharged, a.GN)} | {Per(a.WebPops, a.GN)} | {Per1(a.FoeDis, a.N)} |");
            }
            Console.WriteLine();
            Console.WriteLine("### 表B シガ・カタ・倒れた駒");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 割り込み | 合図の大きさ | 倍率の平均 | 割り込みの鞭 与ダメ | 手番ほか 与ダメ | 蓄電 +（帯電中）| 蓄電 末 | 雷 ／ 1発 ／ カタ 与ダメ | 雷雲 T1 ／ T5 ／ T10 ／ T20 | 倒れたT クグ ／ シガ ／ カタ ／ トウ |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].W != w) continue;
                var a = res[i];
                Console.WriteLine($"| {cells[i].Board} | {cells[i].V} | {Per(a.SwFires, a.SN)} | {ChainAvg(a.ChainHist)} | {Per(a.SwMultSum, a.SwMultN)} | {Per0(a.SwWhip, a.SN)} | {Per0(a.ShDmg - a.SwWhip, a.SN)} | "
                    + $"{Per(a.ChargeGains, a.SN)}（{Per(a.ChargeOnShocked, a.SN)}） | {Per(a.ChargeEnd, a.SN)} | {Per(a.Casts, a.KN)} ／ {Per1(a.KNom, a.Hits)} ／ {Per0(a.KDmg, a.KN)} | "
                    + $"{Avg(a.CloudAt[0], a.N)} ／ {Avg(a.CloudAt[2], a.N)} ／ {Avg(a.CloudAt[3], a.N)} ／ {Avg(a.CloudAt[4], a.N)} | "
                    + $"{Dt(a.GDied, a.GDeathT, a.N)} ／ {Dt(a.SDied, a.SDeathT, a.N)} ／ {Dt(a.KDied, a.KDeathT, a.N)} ／ {Dt(a.TDied, a.TDeathT, a.N)} |");
            }
            if (w.Boss)
            {
                Console.WriteLine();
                Console.WriteLine("### 表C 勇者の HP（ターンの終わり・決着の後は決着の値を持ち越す）");
                Console.WriteLine();
                Console.WriteLine("| 台 | 版 | T1 | T3 | T5 | T10 | T20 |");
                Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].W != w) continue;
                    var a = res[i];
                    Console.WriteLine($"| {cells[i].Board} | {cells[i].V} | " + string.Join(" | ", a.HeroHpAt.Select(x => Avg(x, a.N, "F0"))) + " |");
                }
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 対照（R383）: 勝率 50% 以上のセルで駒 → ドルガ（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 台 | 版 | 勝率 | クグ → | シガ → | カタ → | トウ → |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|");
        for (int i = 0; i < cells.Count; i++)
        {
            if (res[i].Win < 50) continue;
            var f = cells[i].F;
            string C(string id) { var o = f.Occupied().FirstOrDefault(x => x.Def.Id == id); return o.Def is null ? "—" : F1(100.0 * WinsPar(FvSwap(f, o.Def, UnitCatalog.Dolga), cells[i].W, Seeds) / Seeds); }
            Console.WriteLine($"| {cells[i].W.Name} | {cells[i].Board} | {cells[i].V} | {F1(res[i].Win)} | {C("kugu")} | {C("shiga")} | {C("kata")} | {C("tou")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 格子
    // ---------------------------------------------------------------------------------
    internal static List<UnitDef[]> Combos(UnitDef[] pool, int k)
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

    sealed record GridRes(UnitDef[] Order, int[] Wins, long[] WinT, int[][] Ctl);

    /// <summary>
    /// 格子の本体。<paramref name="vars"/> の各版を同じ台で回す（足切りはどれかの版で seed 0..19 に 5 勝）。勝率 50% 以上の版で固定枠の駒それぞれ → ドルガ。
    /// 届いた ＝ 固定枠の駒のどれか1枚 → ドルガで半分以下（第292期の作法: 「固定枠の駒それぞれ」と「勝率 50%」を併記）。
    /// </summary>
    static void GridCore(string title, S287.Wave w, UnitDef[] fixedU, UnitDef[] pool, int k, (string Name, Func<Formation, Formation> Apply)[] vars)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lu = Combos(pool, k);
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

        Console.WriteLine($"# 第293期 格子 × {w.Name} × {title}");
        Console.WriteLine();
        Console.WriteLine($"探索枠{k}（候補 {pool.Length} 枚: {OrderName(pool)}・ヒーラー ≦ 1）＝ {lu.Count} 組 × 席 120 ＝ {boards.Count:N0} 台 × 版 {vars.Length}（{string.Join(" ／ ", vars.Select(v => v.Name))}）。");
        Console.WriteLine($"足切り seed 0..{CutSeeds - 1}（どれかの版で 5 勝以上・{cutPass:N0} 台が通った）→ 全版 seed 0..{Seeds - 1} → 勝率 50% 以上の版で固定枠の駒それぞれ → ドルガ。");
        Console.WriteLine("**届いた ＝ 固定枠の駒のどれか1枚 → ドルガで半分以下**。勝率 50% の線も併記する（第292期 R387）。");
        Console.WriteLine();
        Console.WriteLine("## 表1 段ごとの台数（組の数）");
        Console.WriteLine();
        Console.WriteLine("| 段 | " + string.Join(" | ", vars.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(vars.Select(_ => "--:|")));
        void Row(string label, Func<GridRes, int, bool> pred) =>
            Console.WriteLine($"| {label} | " + string.Join(" | ", Enumerable.Range(0, vars.Length).Select(v => { var l = all.Where(r => pred(r, v)).ToList(); return $"{l.Count:N0}（{l.Select(r => Key(r.Order)).Distinct().Count()}）"; })) + " |");
        Row("勝率 ≧ 50%", Half);
        Row("**届いた**", Reach);
        for (int c = 0; c < fixedU.Length; c++) { int cc = c; Row($"{Short(fixedU[c])}が要る（→ ドルガで半分以下）", (r, v) => Need(r, v, cc)); }
        Row("勝率 ≧ 90%", (r, v) => r.Wins[v] * 10 >= Seeds * 9);
        Console.WriteLine();
        Console.WriteLine("## 表2 駒別（勝率 50% 以上の台にその駒が入っていた数 ／ 全体の台）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 全体 | " + string.Join(" | ", vars.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|--:|" + string.Concat(vars.Select(_ => "--:|")));
        foreach (var d in pool)
            Console.WriteLine($"| {d.Name} | {boards.Count(o => o.Contains(d)):N0} | " + string.Join(" | ", Enumerable.Range(0, vars.Length).Select(v => all.Count(r => Half(r, v) && r.Order.Contains(d)).ToString("N0"))) + " |");
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
                Console.WriteLine($"| {i + 1} | {OrderName(r.Order)} | {F1(100.0 * r.Wins[v] / Seeds)} | {Per1(r.WinT[v], r.Wins[v])} | {(Reach(r, v) ? "○" : "")} | {string.Join(" | ", r.Ctl[v].Select(c => F1(100.0 * c / Seeds)))} | "
                    + string.Join(" | ", Enumerable.Range(0, vars.Length).Where(x => x != v).Select(x => F1(100.0 * r.Wins[x] / Seeds))) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>ボスの格子（§6-2）: 固定枠 クグ（KW-a）＋ カタ（規定 KR-∞）＋ シガ（版）・探索枠2 ＝ 第287期の候補からクグ・カタ・シガを除き、トウを足した枠。</summary>
    internal static UnitDef[] BossPool => S287.Pool.Where(d => d.Id is not ("kugu" or "kata" or "shiga")).Prepend(UnitCatalog.Tou).ToArray();

    static void GridBoss()
    {
        var w = S287.Waves[0];
        var vars = ShigaVers.Select(v => (v.Name, (Func<Formation, Formation>)(f => Apply(f, KuguVers[2], v)))).ToArray();
        GridCore("固定枠 クグ（KW-a）＋ カタ（規定 KR-∞）＋ シガ（SI-b ／ SW-a ／ SW-b）", w, new[] { UnitCatalog.KuguKGb, UnitCatalog.Kata, UnitCatalog.ShigaSIb }, BossPool, 2, vars);
    }

    /// <summary>精鋭の格子（§6-2）: 固定枠 トウ ＋ クグ（版 ／ 規定）または トウ ＋ シガ（版 ／ 規定）・探索枠3 ＝ 第287期の候補（クグ ／ シガは規定に替えた）。</summary>
    static void GridElite(string waveName, string who, string ver)
    {
        var w = WaveOf(waveName);
        bool kugu = who == "kugu";
        var v = AnyVer(ver);
        var basis = kugu ? KuguVers[0] : ShigaVers[0];
        UnitDef fixed2 = kugu ? UnitCatalog.KuguKGb : UnitCatalog.ShigaSIb;
        // 候補: 第287期の候補（感電の駒は規定に替える）からトウと固定の駒を除き、相方（クグの格子ならシガ・シガの格子ならクグ）を規定で入れる
        var pool = S287.Pool.Select(d => d.Id switch { "kata" => UnitCatalog.Kata, "kugu" => UnitCatalog.KuguKGb, _ => d })
                            .Where(d => d.Id != (kugu ? "kugu" : "shiga")).Append(kugu ? UnitCatalog.ShigaSIb : UnitCatalog.KuguKGb).Distinct().ToArray();
        var vars = new (string, Func<Formation, Formation>)[] { (basis.Name, f => f), (v.Name, f => Apply(f, v)) };
        GridCore($"固定枠 トウ ＋ {Short(fixed2)}（{basis.Name} ／ {v.Name}）", w, new[] { UnitCatalog.Tou, fixed2 }, pool, 3, vars);
    }

    // ---------------------------------------------------------------------------------
    // 1台の推移
    // ---------------------------------------------------------------------------------
    static void DeepOne(string wave, string order, string[] vers)
    {
        var w = WaveOf(wave);
        var f0 = Seat(Order(order));
        Console.WriteLine($"# shock293 deep —— {order} × {w.Name}（seed 0..199・verbose）");
        Console.WriteLine();
        var vs = vers.Length > 0 ? new[] { (string.Join(" ", vers), vers.Aggregate(f0, (f, x) => Apply(f, AnyVer(x)))) } : Variants(f0).Select(v => (v.Name, v.F)).ToArray();
        Console.WriteLine("| 版 | 勝率 | 倒しT | 負けT | 張った糸 | 糸玉 | 割り込み | 倍率 | 割り込みの鞭 | シガ 与ダメ | カタ 与ダメ | 雷雲 T5 ／ T10 | 勇者 T5 ／ T10 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (n, f) in vs)
        {
            var a = MeasureDeep(f, w, Seeds);
            Console.WriteLine($"| {n} | {F1(a.Win)} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)} | {Per(a.WebSpun, a.GN)} | {Per(a.SilkPlaced, a.GN)} | {Per(a.SwFires, a.SN)} | {Per(a.SwMultSum, a.SwMultN)} | {Per0(a.SwWhip, a.SN)} | {Per0(a.ShDmg, a.SN)} | {Per0(a.KDmg, a.KN)} | "
                + $"{Avg(a.CloudAt[2], a.N)} ／ {Avg(a.CloudAt[3], a.N)} | {Avg(a.HeroHpAt[2], a.N, "F0")} ／ {Avg(a.HeroHpAt[3], a.N, "F0")} |");
        }
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
        Console.WriteLine("# shock293 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        Expect("(a) 規定のカタ ＝ KR-∞（`KataKRinf` ＝ 規定）・旧の規定 KR-b は `KataKRb`（`All` ／ `Retired` の外）・文面は同じ",
            UnitCatalog.Kata.Traits.Contains(TraitId.ThundercloudUncapped) && ReferenceEquals(UnitCatalog.KataKRinf, UnitCatalog.Kata)
            && UnitCatalog.Kata.Traits.SequenceEqual(UnitCatalog.KataKRb.Traits.Append(TraitId.ThundercloudUncapped)) && !UnitCatalog.Everyone.Contains(UnitCatalog.KataKRb)
            && UnitCatalog.KataKRb.PlusText == UnitCatalog.Kata.PlusText && UnitCatalog.All.Contains(UnitCatalog.Kata));
        Expect("(b) 第293期の規定のクグ（KG-b）・シガ（SI-b）は `KuguKGb` ／ `ShigaSIb`・版は規定の末尾に札を足しただけ・`All` ／ `Retired` の外",
            UnitCatalog.KuguKGb.Traits.SequenceEqual(new[] { TraitId.Grapple, TraitId.Thread, TraitId.ThreadCharge })
            && UnitCatalog.KuguKWa.Traits.SequenceEqual(UnitCatalog.KuguKGb.Traits.Append(TraitId.WebCharge)) && UnitCatalog.KuguKWb.Traits.SequenceEqual(UnitCatalog.KuguKGb.Traits.Append(TraitId.WebSnare))
            && UnitCatalog.ShigaSWa.Traits.SequenceEqual(UnitCatalog.ShigaSIb.Traits.Append(TraitId.ShockWhipChain))
            && UnitCatalog.ShigaSWb.Traits.SequenceEqual(UnitCatalog.ShigaSIb.Traits.Append(TraitId.ShockWhipChain).Append(TraitId.StoredChargeEvery))
            && !new[] { UnitCatalog.KuguKGb, UnitCatalog.KuguKWb, UnitCatalog.ShigaSIb, UnitCatalog.ShigaSWb }.Any(UnitCatalog.Everyone.Contains)   /* 第294期: KW-a ／ SW-a は規定になった（旧の規定が外） */
            && !UnitCatalog.ShigaSIb.Traits.Contains(TraitId.ShockWhipChain));
        Expect("(c) 糸のキーは `StatusKeys.All` に入り、カタの雷の「帯びた種類」には入らない",
            StatusKeys.All.Contains(StatusKeys.Web) && !ThunderTrait.CountedKeys.Contains(StatusKeys.Web));
        Expect("(d) 文面: KW-a ／ KW-b ／ SW-a ／ SW-b の追記",
            UnitCatalog.KuguKWa.PlusText.EndsWith("組み付いている間、手番ごとに糸を1本張る。糸の掛かった敵は帯電し続ける。止められない相手には、代わりにその周りへ帯電した糸玉を張る")
            && UnitCatalog.KuguKWb.PlusText.EndsWith("組み付いている間、手番ごとに糸を1本張る。糸の掛かった敵は動きが鈍る。止められない相手には、代わりにその周りへ帯電した糸玉を張る")
            && UnitCatalog.ShigaSWa.PlusText.EndsWith("割り込みの鞭は、弾けた電気をまとってそのぶん重くなる")
            && UnitCatalog.ShigaSWb.PlusText.Contains("感電を浴びるたび電気が溜まり") && !UnitCatalog.ShigaSWb.PlusText.Contains("帯電するたび電気が溜まり"));

        // (e)〜(h) 網: 近衛 ／ 大隊 × 糸の台 × KW-a ／ KW-b
        var f0 = K292.Pin294(Presets.Playtest.First(r => r.Name == "試遊・感電 糸").F);
        int spun = 0, webOnHeld = 0, webDup = 0, rechargeBad = 0, snareCharged = 0, deadWeb = 0, ballsNoKw = 0, snareSlowSeen = 0;
        foreach (var kv in new[] { KuguVers[2], KuguVers[3] })
            foreach (var w in new[] { S287.Waves[1], S287.Waves[2] })
                for (int s = 0; s < 30; s++)
                {
                    var p = BattleEngine.Materialize(Apply(f0, kv), BattleContext.PlayerTeam);
                    var e = w.Make();
                    var r = BattleEngine.Run(p, e, s, verbose: true);
                    var webbed = new HashSet<int>();
                    foreach (var ev in r.Events)
                    {
                        if (ev.Kind == BattleEventKind.Web && ev.Text == WebLabels.Spin)
                        {
                            spun++;
                            if (ev.TargetId == ev.PartnerId) webOnHeld++;
                            if (!webbed.Add(ev.TargetId!.Value)) webDup++;
                        }
                        if (ev.Kind == BattleEventKind.Web && ev.Text == WebLabels.Recharge)
                        {
                            if (kv != KuguVers[2]) snareCharged++;
                            if (!webbed.Contains(ev.TargetId!.Value)) rechargeBad++;
                        }
                        if (ev.Kind == BattleEventKind.Death && ev.TargetId is int dt) webbed.Remove(dt);
                    }
                    foreach (var u in e) if (!u.IsAlive && u.RawCounter(StatusKeys.Web) > 0) deadWeb++;
                    if (kv == KuguVers[3] && e.Any(u => u.IsAlive && u.RawCounter(StatusKeys.Web) > 0)) snareSlowSeen++;
                }
        Expect("(e) 網は張られる（糸の敵は組み付いた敵ではなく、同じ敵に2本張らない）", spun > 0 && webOnHeld == 0 && webDup == 0, $"張った {spun}・組み付いた敵へ {webOnHeld}・重複 {webDup}（120 戦）");
        Expect("(f) 帯電し直しは KW-a の糸の敵だけ（KW-b は帯電しない）", rechargeBad == 0 && snareCharged == 0, $"糸の外 {rechargeBad}・KW-b {snareCharged}");
        Expect("(g) 倒れた敵の糸は消える", deadWeb == 0, $"{deadWeb} 体");
        {
            int balls = 0;
            foreach (var w in new[] { S287.Waves[1], S287.Waves[2] })
                for (int s = 0; s < 30; s++)
                    balls += BattleEngine.Run(BattleEngine.Materialize(f0, BattleContext.PlayerTeam), w.Make(), s, verbose: true).Events.Count(ev => ev.Kind is BattleEventKind.Web or BattleEventKind.SilkBall);
            Expect("(h) 規定のクグ（網なし）では網・糸玉の出来事が 0 件", balls == 0, $"{balls} 件");
        }
        // (i) 速さ −3 は行動順だけ: KW-b の糸の敵がいる戦がある（決定性は (l)）
        Expect("(i) KW-b で糸の敵が生き残る戦がある（速さ −3 が効く盤面）", snareSlowSeen > 0, $"{snareSlowSeen} 戦");
        // (j) ボス: KW-a は KB-a と同じ挙動（勇者1体では糸玉だけ）——勝敗・決着T・糸玉の数が一致
        {
            int diff = 0, nB = 0;
            var fb = Seat(Order("トウ・ゴルム・クグ・カタ・ツギ"));
            for (int s = 0; s < 40; s++)
            {
                var ra = BattleEngine.Run(BattleEngine.Materialize(Apply(fb, KuguVers[1]), BattleContext.PlayerTeam), S287.Waves[0].Make(), s, verbose: true);
                var rb = BattleEngine.Run(BattleEngine.Materialize(Apply(fb, KuguVers[2]), BattleContext.PlayerTeam), S287.Waves[0].Make(), s, verbose: true);
                int ba = ra.Events.Count(ev => ev.Kind == BattleEventKind.SilkBall && ev.Text == SilkBallLabels.Place), bb = rb.Events.Count(ev => ev.Kind == BattleEventKind.SilkBall && ev.Text == SilkBallLabels.Place);
                nB += bb;
                if (ra.PlayerWon != rb.PlayerWon || ra.Turns != rb.Turns || ba != bb) diff++;
            }
            Expect("(j) ボスでは KW-a ＝ KB-a（勝敗・決着T・糸玉の数）", diff == 0 && nB > 0, $"違い {diff} 戦・糸玉 {nB}");
        }
        // (k) SW-a: 倍率は割り込みの鞭だけ・(1 ＋ 合図の数)
        {
            int bad = 0, n = 0;
            var fs = Apply(K292.Pin294(Presets.Playtest.First(r => r.Name == "試遊・感電 雷の型").F), ShigaVers[1]);
            for (int s = 0; s < 40; s++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(fs, BattleContext.PlayerTeam), S287.Waves[1].Make(), s, verbose: true);
                var evs = r.Events;
                for (int i = 0; i < evs.Count; i++)
                {
                    if (evs[i].Kind != BattleEventKind.ShockGauge || evs[i].Text != ShockGaugeLabels.WhipChain) continue;
                    n++;
                    var head = evs.Take(i).LastOrDefault(x => x.Kind == BattleEventKind.ShockGauge && x.Text == ShockGaugeLabels.Interrupt);
                    if (head is null || evs[i].Amount != 1 + head.Slot || evs[i].Slot != head.Slot) bad++;
                }
            }
            Expect("(k) SW-a の倍率 ＝ 1 ＋ 合図の連鎖で弾けた数（割り込みの見出しの直後）", n > 0 && bad == 0, $"{n} 回・ずれ {bad}");
        }
        // (l) 決定性・verbose の有無
        {
            int nd = 0;
            foreach (var (fv, w) in new[] { (Apply(f0, KuguVers[3]), S287.Waves[1]), (Apply(f0, KuguVers[2]), S287.Waves[2]), (Apply(K292.Pin294(Presets.Playtest.First(r => r.Name == "試遊・感電 雷の型").F), ShigaVers[2]), S287.Waves[1]) })
                for (int s = 0; s < 20; s++)
                {
                    var r1 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                    var r2 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                    var r3 = BattleEngine.Run(BattleEngine.Materialize(fv, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                    if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns || r1.Events.Count != r3.Events.Count) nd++;
                }
            Expect("(l) 決定的・verbose の有無で勝敗と決着T が変わらない（KW-b ／ KW-a ／ SW-b）", nd == 0, $"{nd} 件");
        }
        // (m) SW-b: 帯電中に浴びた感電で蓄電が増える
        {
            long on = 0, onA = 0;
            var fr = K292.Pin294(Presets.Playtest.First(r => r.Name == "試遊・感電 雷の型").F);
            for (int s = 0; s < 40; s++)
            {
                on += BattleEngine.Run(BattleEngine.Materialize(Apply(fr, ShigaVers[2]), BattleContext.PlayerTeam), S287.Waves[1].Make(), s, verbose: false).TallyByUnit["shiga"].ChargeOnShocked;
                onA += BattleEngine.Run(BattleEngine.Materialize(Apply(fr, ShigaVers[1]), BattleContext.PlayerTeam), S287.Waves[1].Make(), s, verbose: false).TallyByUnit["shiga"].ChargeOnShocked;
            }
            Expect("(m) SW-b は帯電中に浴びた感電でも蓄電 +1（SW-a では 0）", on > 0 && onA == 0, $"SW-b {on} ／ SW-a {onA}（40 戦）");
        }
        // (n) 乱数: 網と倍率の口に PickOne ／ Roll が無い
        {
            string src = File.ReadAllText("BattleCore/BattleEngine.cs");
            int a = src.IndexOf("// 第293期 —— クグの網", StringComparison.Ordinal), b = src.IndexOf("public const int WebSnareSlow", StringComparison.Ordinal);
            string seg = a >= 0 && b > a ? src[a..b] : "";
            Expect("(n) 網の口（`SpinWeb` ／ `RechargeWebs` ／ `TurnSpeed`）に PickOne ／ Roll が無い", seg.Length > 0 && !seg.Contains("PickOne") && !seg.Contains("Roll("), $"{seg.Length} 字");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
