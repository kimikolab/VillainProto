using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using BA = BurnAuditDiag;

// =====================================================================================
// shock289 —— 第289期「シガの割り込み雷霆 ＋ カタの雷雲 ＋ ベニ×トウの切り分け」。
// 指示書は design/PHASE289_SHOCK_ATTACKERS_SPEC.md ／ 報告は design/PHASE289_SHOCK_ATTACKERS.md。
//
//     dotnet run --project BattleSim -c Release 0 shock289 p0          # Phase 0（段1 §3-3 ／ 段2 §4-3）: 規定のシガ（SG-a）・規定のカタで、敵の感電の連鎖・その時点の蓄電・生き残り・雷までに弾けた数
//     dotnet run --project BattleSim -c Release 0 shock289 compare     # `compare` 64 行 × 版（シガ SG-a ／ SI-a ／ SI-b・カタ 旧 ／ KR-a ／ KR-b）
//     dotnet run --project BattleSim -c Release 0 shock289 boards      # 代表台 17 台 × 版（seed 0..199・verbose）と対照
//     dotnet run --project BattleSim -c Release 0 shock289 grid <boss|guard|bat> <shiga|kata> <版>   # 第287期の格子（固定枠 トウ ＋ シガ ／ トウ ＋ カタ）
//     dotnet run --project BattleSim -c Release 0 shock289 beni        # 段3: ベニの札を1枚ずつ外した版 × 台 × 波（＋ ベニ → ドルガ）
//     dotnet run --project BattleSim -c Release 0 shock289 check       # 自己検査
//     dotnet run --project BattleSim -c Release 0 shock289 log <boss|guard|bat> <版> <席の並び（短い名前を ・ で5つ）> [seed]
//
// 版の名前: シガ SG-a（規定）／ SI-a ／ SI-b、カタ old（規定）／ KR-a ／ KR-b（ASCII の別名 sga ／ sia ／ sib ／ old ／ kra ／ krb）。
// =====================================================================================
static class Shock289Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        switch (mode)
        {
            case "p0": P0(); return;
            case "compare": CompareAll(); return;
            case "boards": BoardsAll(); return;
            case "grid": Grid(args.Length > 3 ? args[3] : "boss", args.Length > 4 ? args[4] : "shiga", args.Length > 5 ? args[5] : "SG-a"); return;
            case "beni": BeniSplit(); return;
            case "check": Check(); return;
            case "log": LogOne(args.Length > 3 ? args[3] : "boss", args.Length > 4 ? args[4] : "SI-a", args.Length > 5 ? args[5] : "", args.Length > 6 ? int.Parse(args[6]) : 0); return;
            default: Console.WriteLine("shock289: モードは p0 / compare / boards / grid / beni / check / log。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・波・台（測る前に固定）
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, UnitDef From, UnitDef To);
    internal static readonly Ver[] ShigaVers =
    {
        new("SG-a", UnitCatalog.Shiga, UnitCatalog.Shiga), new("SI-a", UnitCatalog.Shiga, UnitCatalog.ShigaSIa), new("SI-b", UnitCatalog.Shiga, UnitCatalog.ShigaSIb),
    };
    internal static readonly Ver[] KataVers =
    {
        new("旧", UnitCatalog.Kata, UnitCatalog.Kata), new("KR-a", UnitCatalog.Kata, UnitCatalog.KataKRa), new("KR-b", UnitCatalog.Kata, UnitCatalog.KataKRb),
    };
    static Ver VerOf(string n)
    {
        string k = n switch { "sga" => "SG-a", "sia" => "SI-a", "sib" => "SI-b", "old" => "旧", "kra" => "KR-a", "krb" => "KR-b", _ => n };
        return ShigaVers.Concat(KataVers).First(v => v.Name == k);
    }
    static Formation Apply(Formation f, Ver v) => ReferenceEquals(v.From, v.To) ? f : FvSwap(f, v.From, v.To);
    static bool Has(Formation f, UnitDef d) => f.Occupied().Any(o => ReferenceEquals(o.Def, d));

    static S287.Wave WaveOf(string n) => S287.Waves.First(w => w.Name == (n switch { "boss" => "ボス", "guard" => "近衛", "bat" => "大隊", _ => n }));
    const int Seeds = 200, CutSeeds = 20;

    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static UnitDef ByShort(string n) => UnitCatalog.All.First(d => Short(d) == n);
    static UnitDef[] Order(string s) => s.Split('・', StringSplitOptions.RemoveEmptyEntries).Select(ByShort).ToArray();
    static string OrderName(UnitDef[] o) => string.Join("・", o.Select(Short));
    static Formation Seat(UnitDef[] o) => B283.Seat(o);

    /// <summary>
    /// 代表台（指示書 §6-2）: 第288期の 15 台 ＋ トウ ＋ シガ ＋ カタの台（近衛 ／ 大隊 各1台）。
    /// 新しい2台の作り方（測る前に固定）: 第287期の各波の上位1位（シガ・ガルド・ベニ・トウ・ソラ）の探索枠3 のうち、第287期 R5 の到達寄与が最も低い1枚を<b>その席のまま</b>カタに替える
    /// （第288期の「カタ隣」はシガの隣へ席を入れ替えた台なので、こちらは席を動かさない）。近衛: ソラ（後3）→ カタ ／ 大隊: ガルド（前3）→ カタ。
    /// </summary>
    internal static (string Name, string Wave, Func<Formation> Make)[] Boards()
    {
        var l = new List<(string, string, Func<Formation>)>();
        foreach (var b in Shiga288Diag.Boards()) l.Add((b.Name, b.Wave, b.Make));
        l.Add(("トウ+シガ+カタ 近衛", "近衛", () => Seat(Order("シガ・ガルド・ベニ・トウ・カタ"))));
        l.Add(("トウ+シガ+カタ 大隊", "大隊", () => Seat(Order("シガ・カタ・ベニ・トウ・ソラ"))));
        return l.ToArray();
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Deep
    {
        public long N, Wins, WinT, FoeDis, AllyDis;
        // シガ
        public long ShN, Swings, Cower, SwFires, SwHushed, SwBlocked, SwNoCharge, SwNoTarget, SwNested, SwAsked, SwWhip, SwBolt, BoltHits, BoltCasts, ShDmg, ChargeEnd, ShDied, ShDeathT, ChainOwn;
        public long[] Ini = new long[6], ChargeAt = new long[StoredChargeTrait.Cap + 1], AliveAt = new long[10];
        // カタ
        public long KN, Casts, Hits, KDmg, KNom, CloudSum, CloudMax, KDied, KDeathT;
        public long[] PopsAt = new long[16], Kinds = new long[17];
        // トウ
        public long TDied, TDeathT;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Merge(Deep o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; FoeDis += o.FoeDis; AllyDis += o.AllyDis;
            ShN += o.ShN; Swings += o.Swings; Cower += o.Cower; SwFires += o.SwFires; SwHushed += o.SwHushed; SwBlocked += o.SwBlocked; SwNoCharge += o.SwNoCharge;
            SwNoTarget += o.SwNoTarget; SwNested += o.SwNested; SwAsked += o.SwAsked; SwWhip += o.SwWhip; SwBolt += o.SwBolt; BoltHits += o.BoltHits; BoltCasts += o.BoltCasts;
            ShDmg += o.ShDmg; ChargeEnd += o.ChargeEnd; ShDied += o.ShDied; ShDeathT += o.ShDeathT; ChainOwn += o.ChainOwn;
            for (int i = 0; i < Ini.Length; i++) Ini[i] += o.Ini[i];
            for (int i = 0; i < ChargeAt.Length; i++) ChargeAt[i] += o.ChargeAt[i];
            for (int i = 0; i < AliveAt.Length; i++) AliveAt[i] += o.AliveAt[i];
            KN += o.KN; Casts += o.Casts; Hits += o.Hits; KDmg += o.KDmg; KNom += o.KNom; CloudSum += o.CloudSum; if (o.CloudMax > CloudMax) CloudMax = o.CloudMax;
            KDied += o.KDied; KDeathT += o.KDeathT;
            for (int i = 0; i < PopsAt.Length; i++) PopsAt[i] += o.PopsAt[i];
            for (int i = 0; i < Kinds.Length; i++) Kinds[i] += o.Kinds[i];
            TDied += o.TDied; TDeathT += o.TDeathT;
        }
    }

    static void AddArr(long[] a, long[]? b) { if (b is null) return; for (int i = 0; i < Math.Min(a.Length, b.Length); i++) a[i] += b[i]; }

    internal static Deep FightDeep(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var d = new Deep { N = 1 };
        if (r.PlayerWon) { d.Wins = 1; d.WinT = r.Turns; }
        var mine = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit) { if (mine.Contains(id)) d.AllyDis += t.DischargeTaken; else d.FoeDis += t.DischargeTaken; }
        UnitState? sh = p.FirstOrDefault(u => u.Def.Id == "shiga"), ka = p.FirstOrDefault(u => u.Def.Id == "kata"), to = p.FirstOrDefault(u => u.Def.Id == "tou");
        if (sh is not null && r.TallyByUnit.TryGetValue("shiga", out var st))
        {
            d.ShN = 1; d.Swings = st.WhipSwings; d.Cower = st.WhipCowered; d.SwFires = st.SwFires; d.SwHushed = st.SwHushed; d.SwBlocked = st.SwBlocked; d.SwNoCharge = st.SwNoCharge;
            d.SwNoTarget = st.SwNoTarget; d.SwNested = st.SwNested; d.SwAsked = st.SwAsked; d.SwWhip = st.SwWhipDealt; d.SwBolt = st.SwBoltDealt; d.BoltHits = st.BoltHits; d.BoltCasts = st.BoltCasts;
            d.ShDmg = st.DamageToEnemy; d.ChargeEnd = sh.RawCounter(StoredChargeTrait.Key); d.ChainOwn = st.ChainOwn;
            AddArr(d.Ini, st.ChainIniHist); AddArr(d.ChargeAt, st.ChainChargeHist); AddArr(d.AliveAt, st.ChainAliveHist);
        }
        if (ka is not null && r.TallyByUnit.TryGetValue("kata", out var kt))
        {
            d.KN = 1; d.Casts = kt.ThunderCasts; d.Hits = kt.ThunderHits; d.KDmg = kt.DamageToEnemy; d.KNom = kt.ThunderNominal; d.CloudSum = kt.CloudAtCastSum; d.CloudMax = kt.CloudAtCastMax;
            AddArr(d.PopsAt, kt.ThunderPopsHist); AddArr(d.Kinds, kt.ThunderKindsHist);
        }
        foreach (var ev in r.Events.Where(ev => ev.Kind == BattleEventKind.Death))
        {
            if (sh is not null && ev.TargetId == sh.InstanceId && d.ShDied == 0) { d.ShDied = 1; d.ShDeathT = ev.Turn; }
            if (ka is not null && ev.TargetId == ka.InstanceId && d.KDied == 0) { d.KDied = 1; d.KDeathT = ev.Turn; }
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
    static readonly string[] IniNames = { "トウ", "カタ", "—", "ほかの味方", "刻み", "敵" };

    // ---------------------------------------------------------------------------------
    // Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var bs = Boards();
        var res = bs.Select(b => MeasureDeep(b.Make(), WaveOf(b.Wave), Seeds)).ToArray();
        Console.WriteLine("# 第289期 Phase 0 —— 規定のシガ（SG-a）・規定のカタ（seed 0..199・verbose・計数だけ）");
        Console.WriteLine();
        Console.WriteLine("## §3-3 シガ: 敵の感電の連鎖（シガ自身の一撃で起きたものを除く）");
        Console.WriteLine();
        Console.WriteLine("連 ＝ 1戦の連鎖の数（起点の書き手: トウ ／ カタ ／ ほかの味方 ／ 刻み ／ 敵）／ 自 ＝ シガ自身の一撃で起きた連鎖（数えない）／ 蓄 ＝ 連鎖の時点の蓄電の分布（0〜4）／ 4 ＝ 蓄電 4 の割合（SI-a）／ ≧1 ＝ 蓄電 1 以上の割合（SI-b）／ 生 ＝ 連鎖の後に生きていた弾けた敵の数の平均（0 体の割合）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 倒しT | 連（トウ ／ カタ ／ ほか ／ 刻み ／ 敵） | 自 | 蓄 0／1／2／3／4 | 4 | ≧1 | 生（0 体） | 振 |");
        Console.WriteLine("|---|---|--:|--:|---|--:|---|--:|--:|--:|--:|");
        for (int i = 0; i < bs.Length; i++)
        {
            var a = res[i];
            if (a.ShN == 0) continue;
            long chains = a.Ini.Sum();
            long aliveSum = 0; for (int k = 0; k < a.AliveAt.Length; k++) aliveSum += k * a.AliveAt[k];
            Console.WriteLine($"| {bs[i].Name} | {bs[i].Wave} | {F1(a.Win)} | {Per(a.WinT, a.Wins)} | {Per(chains, a.N)}（{Per(a.Ini[0], a.N)} ／ {Per(a.Ini[1], a.N)} ／ {Per(a.Ini[3], a.N)} ／ {Per(a.Ini[4], a.N)} ／ {Per(a.Ini[5], a.N)}） | "
                + $"{Per(a.ChainOwn, a.N)} | {string.Join("／", a.ChargeAt.Select(c => Pct(c, chains)))} | {Pct(a.ChargeAt[4], chains)} | {Pct(chains - a.ChargeAt[0], chains)} | {Per(aliveSum, chains)}（{Pct(a.AliveAt[0], chains)}） | {Per(a.Swings, a.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## §4-3 カタ: 雷を落とすまでに弾けた敵の数（KR-a の雷雲と同じ量）・雷の発数・帯びた種類");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 雷（1戦） | 当たり ÷ 雷 | 弾け（雷の時点・平均） | 0 ／ 1〜2 ／ 3〜5 ／ 6+ の割合 | 帯びた種類（1発の平均） | カタの与 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|---|--:|--:|");
        for (int i = 0; i < bs.Length; i++)
        {
            var a = res[i];
            if (a.KN == 0) continue;
            long casts = a.PopsAt.Sum(), ps = 0; for (int k = 0; k < a.PopsAt.Length; k++) ps += k * a.PopsAt[k];
            long kh = a.Kinds.Sum(), ks = 0; for (int k = 0; k < a.Kinds.Length; k++) ks += k * a.Kinds[k];
            Console.WriteLine($"| {bs[i].Name} | {bs[i].Wave} | {F1(a.Win)} | {Per(a.Casts, a.N)} | {Per(a.Hits, a.Casts)} | {Per(ps, casts)} | "
                + $"{Pct(a.PopsAt[0], casts)} ／ {Pct(a.PopsAt[1] + a.PopsAt[2], casts)} ／ {Pct(a.PopsAt[3] + a.PopsAt[4] + a.PopsAt[5], casts)} ／ {Pct(a.PopsAt.Skip(6).Sum(), casts)} | {Per(ks, kh)} | {Per1(a.KDmg, a.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## §3-3 の4 敵側の殴り返しの札（本編・精鋭・ボスの敵の札）");
        Console.WriteLine();
        var reactive = new HashSet<TraitId> { TraitId.Thorns, TraitId.LastStand, TraitId.Erupt, TraitId.Evade, TraitId.Spring, TraitId.Avenge };
        var foes = EnemyCatalog.Stages.SelectMany(s => s.Enemy.Occupied().Select(o => o.Def))
            .Concat(new[] { EnemyCatalog.Recruit, EnemyCatalog.Levy, EnemyCatalog.BossRegular }).Distinct().ToList();
        foreach (var fd in foes)
        {
            var rx = fd.Traits.Where(reactive.Contains).ToList();
            if (rx.Count > 0) Console.WriteLine($"- {fd.Name}: {string.Join(", ", rx)}");
        }
        Console.WriteLine($"- （殴り返しの札 ＝ {string.Join(" ／ ", reactive)} を持つ敵だけを並べた。並ばなければ 0）");
        Console.WriteLine();
        foreach (var b in bs) Console.WriteLine($"- {b.Name}: {BA.SeatsNamed(b.Make())}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` 64 行 × 版
    // ---------------------------------------------------------------------------------
    static double[,] CompareGrid(Ver v)
    {
        var rows = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count;
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
    }

    static void CompareAll()
    {
        var rows = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count;
        Console.WriteLine("# 第289期 `compare` 64 行 × 版（seed 0..199・その駒の在席行だけ差し替える）");
        var prim = Baseline.PrimaryRows.Select(n => Array.FindIndex(rows, r => r.Name == n)).ToArray();
        foreach (var set in new[] { ShigaVers, KataVers })
        {
            var grids = set.ToDictionary(v => v.Name, CompareGrid);
            var basis = grids[set[0].Name];
            Console.WriteLine();
            Console.WriteLine($"## {Short(set[0].From)}");
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
                Console.WriteLine($"- {Short(set[0].From)}のいない行のずれ（{v.Name} − {set[0].Name}）: {cells} セル中 **{bad} 件**");
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
        // 割り込みが粛で止まった数（第2〜5波・シガ在席の行・SI-a ／ SI-b）
        Console.WriteLine();
        Console.WriteLine("## 割り込みと粛（シガ在席の行 × 本編の波 × seed 0..199・verbose なし）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 波 | 勝率 | 割り込み（1戦） | 粛で止まった | 痺れ・組み付きで止まった | 蓄電が足りない |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in rows.Where(r => Has(r.F, UnitCatalog.Shiga)))
            foreach (var v in ShigaVers.Skip(1))
                for (int wi = 1; wi < nw; wi++)
                {
                    long fires = 0, hush = 0, blk = 0, noc = 0; int wins = 0;
                    var g = Apply(f, v);
                    for (int s = 0; s < Seeds; s++)
                    {
                        var r = BattleEngine.Run(g, EnemyCatalog.Stages[wi].Enemy, s, verbose: false);
                        if (r.PlayerWon) wins++;
                        if (r.TallyByUnit.TryGetValue("shiga", out var t)) { fires += t.SwFires; hush += t.SwHushed; blk += t.SwBlocked; noc += t.SwNoCharge; }
                    }
                    Console.WriteLine($"| {name} | {v.Name} | 第{wi + 1}波 | {F1(100.0 * wins / Seeds)} | {Per(fires, Seeds)} | {Per(hush, Seeds)} | {Per(blk, Seeds)} | {Per(noc, Seeds)} |");
                }
    }

    // ---------------------------------------------------------------------------------
    // 代表台 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var bs = Boards();
        Console.WriteLine("# 第289期 代表台 × 版（seed 0..199・verbose）");
        Console.WriteLine();
        Console.WriteLine("## 表1 シガの版（SG-a ＝ 規定 ／ SI-a ／ SI-b）");
        Console.WriteLine();
        Console.WriteLine("振 ＝ 鞭の振り（手番 ／ 割り込み）／ 怖 ＝ 怖気 ／ 止 ＝ 割り込みが止まった（粛 ／ 痺れほか）／ 霆当 ＝ 雷霆の追加を当てた延べ ／ 与 ＝ シガの与ダメ（手番の鞭 ／ 割り込みの鞭 ／ 雷霆）／ 蓄末 ＝ 戦の終わりの蓄電 ／ 対照 ＝ その駒 → ドルガ");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率 | 倒しT | 振（手番 ／ 割） | 怖 | 止（粛 ／ 他） | 霆当 | 与（手番 ／ 割 ／ 霆） | 蓄末 | 放電 敵 ／ 味方 | シガが倒れたT（割合） | トウ → ドルガ | シガ → ドルガ |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in bs)
        {
            var w = WaveOf(b.Wave);
            foreach (var v in ShigaVers)
            {
                var f = Apply(b.Make(), v);
                var a = MeasureDeep(f, w, Seeds);
                int dt = WinsPar(FvSwap(f, UnitCatalog.Tou, UnitCatalog.Dolga), w, Seeds), ds = WinsPar(FvSwap(f, v.To, UnitCatalog.Dolga), w, Seeds);
                long turnDmg = a.ShDmg - a.SwWhip - a.SwBolt;
                Console.WriteLine($"| {b.Name} | {b.Wave} | {v.Name} | {F1(a.Win)} | {Per(a.WinT, a.Wins)} | {Per(a.Swings, a.N)}（{Per(a.Swings - a.SwFires, a.N)} ／ {Per(a.SwFires, a.N)}） | {Per(a.Cower, a.N)} | "
                    + $"{Per(a.SwHushed, a.N)} ／ {Per(a.SwBlocked, a.N)} | {Per(a.BoltHits, a.N)} | {Per1(a.ShDmg, a.N)}（{Per1(turnDmg, a.N)} ／ {Per1(a.SwWhip, a.N)} ／ {Per1(a.SwBolt, a.N)}） | {Per(a.ChargeEnd, a.N)} | "
                    + $"{Per1(a.FoeDis, a.N)} ／ {Per1(a.AllyDis, a.N)} | {Per1(a.ShDeathT, a.ShDied)}（{F1(100.0 * a.ShDied / a.N)}%） | {(Has(f, UnitCatalog.Tou) ? F1(100.0 * dt / Seeds) : "—")} | {F1(100.0 * ds / Seeds)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 表2 カタの版（旧 ＝ 規定 ／ KR-a ／ KR-b）・カタ在席の台だけ");
        Console.WriteLine();
        Console.WriteLine("雷 ＝ 1戦の雷の回数（当たり ÷ 雷 ＝ 跳ねも含めた1回の当たり数）／ 雷雲 ＝ 落とした時点の雷雲（平均 ／ 最大）／ 1発 ＝ 1発の名目の平均 ／ 与 ＝ カタの与ダメ");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率 | 倒しT | 雷 | 当たり ÷ 雷 | 雷雲（平均 ／ 最大） | 1発 | 与 | 放電 敵 ／ 味方 | カタが倒れたT（割合） | トウ → ドルガ | カタ → ドルガ |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var b in bs)
        {
            if (!Has(b.Make(), UnitCatalog.Kata)) continue;
            var w = WaveOf(b.Wave);
            foreach (var v in KataVers)
            {
                var f = Apply(b.Make(), v);
                var a = MeasureDeep(f, w, Seeds);
                int dt = WinsPar(FvSwap(f, UnitCatalog.Tou, UnitCatalog.Dolga), w, Seeds), dk = WinsPar(FvSwap(f, v.To, UnitCatalog.Dolga), w, Seeds);
                Console.WriteLine($"| {b.Name} | {b.Wave} | {v.Name} | {F1(a.Win)} | {Per(a.WinT, a.Wins)} | {Per(a.Casts, a.N)} | {Per(a.Hits, a.Casts)} | {Per(a.CloudSum, a.Casts)} ／ {a.CloudMax} | {Per1(a.KNom, a.Hits)} | {Per1(a.KDmg, a.N)} | "
                    + $"{Per1(a.FoeDis, a.N)} ／ {Per1(a.AllyDis, a.N)} | {Per1(a.KDeathT, a.KDied)}（{F1(100.0 * a.KDied / a.N)}%） | {(Has(f, UnitCatalog.Tou) ? F1(100.0 * dt / Seeds) : "—")} | {F1(100.0 * dk / Seeds)} |");
            }
        }
        Console.WriteLine();
        foreach (var b in bs) Console.WriteLine($"- {b.Name}: {BA.SeatsNamed(b.Make())}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 格子（固定枠 トウ ＋ シガ ／ トウ ＋ カタ）
    // ---------------------------------------------------------------------------------
    static int CutWins(S287.Wave w) => w.Boss ? 1 : 5;
    static bool Reached(S287.Wave w, int wins, int dolgaWins) => w.Boss ? wins > 0 && dolgaWins == 0 : wins * 2 >= Seeds && dolgaWins * 2 <= wins;
    static bool WinLine(S287.Wave w, int wins) => w.Boss ? wins > 0 : wins * 2 >= Seeds;
    static bool Needed(S287.Wave w, int wins, int dolgaWins) => w.Boss ? dolgaWins == 0 : dolgaWins * 2 <= wins;

    /// <summary>
    /// 探索枠の候補。シガ固定は第287期と同じ 21 枚。<b>カタ固定はカタを候補から抜き、代わりに規定のシガ（SG-a）を入れる</b>（21 枚のまま・決めたこと）。
    /// </summary>
    static UnitDef[] PoolFor(string fix) => fix == "kata" ? S287.Pool.Select(d => d == UnitCatalog.Kata ? UnitCatalog.Shiga : d).ToArray() : S287.Pool;

    static List<UnitDef[]> LineupsFor(UnitDef[] pool)
    {
        var heal = S287.Heal;
        var l = new List<UnitDef[]>();
        for (int a = 0; a < pool.Length; a++)
            for (int b = a + 1; b < pool.Length; b++)
                for (int c = b + 1; c < pool.Length; c++)
                {
                    var trio = new[] { pool[a], pool[b], pool[c] };
                    if (trio.Count(heal.Contains) <= 1) l.Add(trio);
                }
        return l;
    }

    sealed record BoardRes(UnitDef[] Order, int Wins, long WinT, int DolgaTou, int DolgaX, bool Reach);

    static void Grid(string waveName, string fix, string verName)
    {
        var w = WaveOf(waveName);
        var v = VerOf(verName);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var x = v.To;
        var fixedU = new[] { UnitCatalog.Tou, x };
        var pool = PoolFor(fix);
        var lu = LineupsFor(pool);
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedU.Concat(t).ToArray()));
        var res = new BoardRes?[boards.Count];
        int cutPass = 0, winPass = 0;
        Parallel.For(0, boards.Count, i =>
        {
            var f = Seat(boards[i]);
            if (Wins(f, w, CutSeeds, out _) < CutWins(w)) return;
            Interlocked.Increment(ref cutPass);
            int wins = Wins(f, w, Seeds, out long wt);
            int dg = -1, dx = -1;
            if (WinLine(w, wins))
            {
                Interlocked.Increment(ref winPass);
                dg = Wins(FvSwap(f, UnitCatalog.Tou, UnitCatalog.Dolga), w, Seeds, out _);
                dx = Wins(FvSwap(f, x, UnitCatalog.Dolga), w, Seeds, out _);
            }
            res[i] = new BoardRes(boards[i], wins, wt, dg, dx, dg >= 0 && Reached(w, wins, dg));
        });
        var all = res.Where(r => r is not null).Select(r => r!).ToList();
        var reached = all.Where(r => r.Reach).ToList();
        var heal = S287.Heal;
        bool IsHeal(UnitDef d) => heal.Contains(d);
        string Key(UnitDef[] o) => string.Join(",", o.Select(d => d.Id).OrderBy(s => s, StringComparer.Ordinal));
        string xs = Short(x);

        Console.WriteLine($"# 第289期 格子 × {w.Name} × 固定枠 トウ ＋ {xs}（{v.Name}）");
        Console.WriteLine();
        Console.WriteLine($"探索枠3（候補 {pool.Length} 枚・ヒーラー ≦ 1）× 席 120 ＝ {boards.Count:N0} 台。足切り seed 0..{CutSeeds - 1}（{CutWins(w)} 勝以上）→ seed 0..{Seeds - 1} → ドルガ対照（トウ → ドルガ ／ {xs} → ドルガ）。届いた ＝ 第287期と同じ（トウ → ドルガで落ちる）");
        Console.WriteLine();
        Console.WriteLine("## 表1 段ごとの台数");
        Console.WriteLine();
        Console.WriteLine("| 段 | 台 | 組 | うちヒーラー 0 枚の台 ／ 組 |");
        Console.WriteLine("|---|--:|--:|--:|");
        void RowOf(string label, IEnumerable<UnitDef[]> os)
        {
            var l = os.ToList();
            var h0 = l.Where(o => !o.Any(IsHeal)).ToList();
            Console.WriteLine($"| {label} | {l.Count:N0} | {l.Select(Key).Distinct().Count()} | {h0.Count:N0} ／ {h0.Select(Key).Distinct().Count()} |");
        }
        RowOf("全体", boards);
        RowOf("足切りを通った", all.Select(r => r.Order));
        RowOf(w.Boss ? "勝率 ＞ 0%" : "勝率 ≧ 50%", all.Where(r => r.DolgaTou >= 0).Select(r => r.Order));
        RowOf("**届いた**（トウ → ドルガで落ちる）", reached.Select(r => r.Order));
        RowOf($"届いた台のうち **{xs}が要る**（{xs} → ドルガでも落ちる）", reached.Where(r => Needed(w, r.Wins, r.DolgaX)).Select(r => r.Order));
        RowOf("勝率の線は越えたがトウ → ドルガで落ちない", all.Where(r => r.DolgaTou >= 0 && !r.Reach).Select(r => r.Order));
        Console.WriteLine();
        int xNeed = reached.Count(r => Needed(w, r.Wins, r.DolgaX));
        Console.WriteLine($"**{xs}が要る台: {xNeed:N0} ／ {reached.Count:N0}（{Pct(xNeed, reached.Count)}）**");
        Console.WriteLine();
        Console.WriteLine("## 表2 駒別の到達寄与（届いた台にその駒が入っていた数 ÷ その駒を含む台）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 届いた台 | 全体の台（分母） | 割合 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var d in pool)
        {
            int m = reached.Count(r => r.Order.Contains(d)), denom = boards.Count(o => o.Contains(d));
            bool bold = d.Id is "shiga" or "kata" or "beni";
            Console.WriteLine($"| {(bold ? "**" + d.Name + "**" : d.Name)} | {m:N0} | {denom:N0} | {Pct(m, denom)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 表3 上位の台（届いた台・上位 15）");
        Console.WriteLine();
        Console.WriteLine($"| # | 席（前1・前3・中央・後1・後3） | 勝率 | 倒しT | トウ → ドルガ | {xs} → ドルガ |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|");
        var top = reached.OrderByDescending(r => r.Wins).ThenBy(r => r.Wins == 0 ? double.MaxValue : (double)r.WinT / r.Wins).ThenBy(r => OrderName(r.Order), StringComparer.Ordinal).ToList();
        for (int i = 0; i < Math.Min(15, top.Count); i++)
        {
            var r = top[i];
            Console.WriteLine($"| {i + 1} | {OrderName(r.Order)} | {F1(100.0 * r.Wins / Seeds)} | {(r.Wins == 0 ? "—" : ((double)r.WinT / r.Wins).ToString("F2"))} | {F1(100.0 * r.DolgaTou / Seeds)} | {F1(100.0 * r.DolgaX / Seeds)} |");
        }
        Console.WriteLine();
        var reps = new List<BoardRes>();
        if (w.Boss) reps.AddRange(top);
        else foreach (var r in top) { if (reps.Any(q => Key(q.Order) == Key(r.Order))) continue; reps.Add(r); if (reps.Count == 3) break; }
        if (reps.Count == 0)
            foreach (var r in all.OrderByDescending(r => r.Wins).ThenBy(r => OrderName(r.Order), StringComparer.Ordinal))
            { if (reps.Any(q => Key(q.Order) == Key(r.Order))) continue; reps.Add(r); if (reps.Count == 3) break; }
        Console.WriteLine($"## 表4 中身（{(w.Boss ? "届いた台すべて" : "上位3台")}・seed 0..199・verbose）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 倒しT | シガ 振（割） | 霆当 | シガの与 | カタ 雷 | 雷雲（平均 ／ 最大） | カタの与 | 放電 敵 ／ 味方 | 感電で届いた |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        int viaShock = 0;
        foreach (var r in reps)
        {
            var d = MeasureDeep(Seat(r.Order), w, Seeds);
            // 感電で届いた（測る前に固定）: 届いた ∧ 固定枠の2枚目が要る ∧（シガなら割り込みが 1戦 1 回以上・カタなら雷雲の平均が 1 以上）
            bool via = r.Reach && Needed(w, r.Wins, r.DolgaX) && (fix == "kata" ? d.Casts > 0 && d.CloudSum >= d.Casts : d.SwFires >= d.N);
            if (via) viaShock++;
            Console.WriteLine($"| {OrderName(r.Order)} | {F1(d.Win)} | {Per(d.WinT, d.Wins)} | {Per(d.Swings, d.N)}（{Per(d.SwFires, d.N)}） | {Per(d.BoltHits, d.N)} | {Per1(d.ShDmg, d.N)} | {Per(d.Casts, d.N)} | {Per(d.CloudSum, d.Casts)} ／ {d.CloudMax} | {Per1(d.KDmg, d.N)} | "
                + $"{Per1(d.FoeDis, d.N)} ／ {Per1(d.AllyDis, d.N)} | {(via ? "**○**" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**感電で届いた台（届いた ∧ {xs}が要る ∧ {(fix == "kata" ? "雷雲の平均 ≧ 1" : "割り込み 1戦 1 回以上")}）: {viaShock} 台**（中身を測った台の中で）");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒（足切り {boards.Count:N0} 台 → 本段 {cutPass:N0} 台 → ドルガ対照 {winPass:N0} 台 × 2）。");
    }

    // ---------------------------------------------------------------------------------
    // 段3: ベニの札を1枚ずつ外す
    // ---------------------------------------------------------------------------------
    static UnitDef WithTraits(UnitDef d, TraitId[] traits) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances,
        Pattern = d.Pattern, Actions = d.Actions, Traits = traits,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    /// <summary>
    /// ベニの札の割り振り（`docs/units.md` とコードから・測る前に固定）。① 開戦の撒き ＝ `GurenOpeningBurn` ／ ② 反転（自分と隣の味方は毒・燃焼で癒える）＝ `Inverse` ／
    /// ③ 手番で隣に分ける ＝ `Kindle`（火の拍）と `Taint`（毒の拍）／ ④ 紅蓮 ＝ `Guren` ／ マイナス（回復を受けると傷つく）＝ `InverseLeak`。
    /// <para><b>決めたこと</b>: 「② だけを外す」と、③ がまだ隣の味方に火と毒を配るので、② の欠けが「回復を失う」だけでなく「味方を焼く」になる。
    /// そこで1枚ずつの版のほかに、③ の2枚をまとめて外す版（−③）と、② と ③ をまとめて外す版（−②③ ＝ 配らないし癒やさない）を足した。</para>
    /// </summary>
    internal static readonly (string Name, TraitId[] Drop)[] BeniVers =
    {
        ("規定", new TraitId[0]), ("−① 開戦の撒き", new[] { TraitId.GurenOpeningBurn }), ("−② 反転", new[] { TraitId.Inverse }), ("−③a 火を分ける", new[] { TraitId.Kindle }),
        ("−③b 澱み分け", new[] { TraitId.Taint }), ("−④ 紅蓮", new[] { TraitId.Guren }), ("−マイナス 反転の裏", new[] { TraitId.InverseLeak }),
        ("−③（a ＋ b）", new[] { TraitId.Kindle, TraitId.Taint }), ("−②③", new[] { TraitId.Inverse, TraitId.Kindle, TraitId.Taint }),
    };
    static UnitDef BeniOf(TraitId[] drop) => drop.Length == 0 ? UnitCatalog.Beni : WithTraits(UnitCatalog.Beni, UnitCatalog.Beni.Traits.Where(t => !drop.Contains(t)).ToArray());

    static void BeniSplit()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = new (string Name, Func<Formation> Make, string[] Waves)[]
        {
            ("毒+耐久 (ベニ×トウ)", () => CompareBuilds().First(r => r.Name == "毒+耐久 (ベニ×トウ)").F, new[] { "近衛", "大隊", "ボス" }),
            ("第287期 精鋭1位 シガ・ガルド・ベニ・トウ・ソラ", () => Seat(Order("シガ・ガルド・ベニ・トウ・ソラ")), new[] { "近衛", "大隊" }),
            ("第287期 近衛2位 ソラ・シガ・ベニ・ドハ・トウ", () => Seat(Order("ソラ・シガ・ベニ・ドハ・トウ")), new[] { "近衛" }),
            ("第287期 ボスの届いた台 シガ・トウ・ベニ・クビ・バン", () => Seat(Order("シガ・トウ・ベニ・クビ・バン")), new[] { "ボス" }),
            ("ボスの届いた台 クビ・シガ・バン・ベニ・トウ", () => Seat(Order("クビ・シガ・バン・ベニ・トウ")), new[] { "ボス" }),
            ("毒+ベニ+ラウ（トウなし）", () => CompareBuilds().First(r => r.Name.StartsWith("毒+ベニ+ラウ")).F, new[] { "近衛", "大隊" }),
        };
        Console.WriteLine("# 第289期 段3 —— ベニの札を1枚ずつ外した版（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("「最も落ちた札」は1枚ずつ外した6版の中で選ぶ。−③ ／ −②③ は組の版（② だけを外すと ③ の配りが味方を焼くので、その分を切り分ける）。");
        Console.WriteLine();
        Console.WriteLine("札の割り振り: ① 開戦の撒き `GurenOpeningBurn` ／ ② 反転 `Inverse`（自分と隣の味方は毒・燃焼で癒える）／ ③a 火を分ける `Kindle` ・③b 澱み分け `Taint`（手番で隣の味方に火・毒）／ ④ 紅蓮 `Guren` ／ マイナス 反転の裏 `InverseLeak`");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | " + string.Join(" | ", BeniVers.Select(v => v.Name)) + " | ベニ → ドルガ | 最も落ちた札 |");
        Console.WriteLine("|---|---|" + string.Concat(BeniVers.Select(_ => "--:|")) + "--:|---|");
        foreach (var b in boards)
            foreach (var wn in b.Waves)
            {
                var w = WaveOf(wn);
                var f0 = b.Make();
                var wr = BeniVers.Select(v => 100.0 * WinsPar(v.Drop.Length == 0 ? f0 : FvSwap(f0, UnitCatalog.Beni, BeniOf(v.Drop)), w, Seeds) / Seeds).ToArray();
                double dg = 100.0 * WinsPar(FvSwap(f0, UnitCatalog.Beni, UnitCatalog.Dolga), w, Seeds) / Seeds;
                int worst = Enumerable.Range(1, 6).OrderBy(i => wr[i]).First();   // 1枚ずつの版の中で
                Console.WriteLine($"| {b.Name} | {wn} | " + string.Join(" | ", wr.Select(F1)) + $" | {F1(dg)} | {(wr[worst] < wr[0] - 5 ? BeniVers[worst].Name + $"（{wr[worst] - wr[0]:+0.0;-0.0}）" : "（5pt 以上落ちる札なし）")} |");
            }
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {BA.SeatsNamed(b.Make())}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
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
        Console.WriteLine("# shock289 check");
        Console.WriteLine();
        var g3k = new[] { TraitId.Scourge, TraitId.Shame, TraitId.Lash, TraitId.LiveWire, TraitId.ScourgeShock };
        Expect("(a) 規定のシガ ＝ SG-a（G3K ＋ 蓄電）・`ShigaSGa` ＝ 規定・`ShigaG3K` ＝ 旧の規定",
            UnitCatalog.Shiga.Traits.SequenceEqual(g3k.Append(TraitId.StoredCharge)) && ReferenceEquals(UnitCatalog.ShigaSGa, UnitCatalog.Shiga)
            && UnitCatalog.ShigaG3K.Traits.SequenceEqual(g3k) && UnitCatalog.All.Contains(UnitCatalog.Shiga) && !UnitCatalog.Everyone.Contains(UnitCatalog.ShigaG3K));
        Expect("(b) 版は規定の札の末尾に1〜2枚足しただけ・体は規定のまま・`All` ／ `Retired` に入っていない",
            UnitCatalog.ShigaSIa.Traits.SequenceEqual(UnitCatalog.Shiga.Traits.Append(TraitId.ShockWhipBolt))
            && UnitCatalog.ShigaSIb.Traits.SequenceEqual(UnitCatalog.Shiga.Traits.Append(TraitId.ShockWhipFlurry))
            && UnitCatalog.KataKRa.Traits.SequenceEqual(UnitCatalog.Kata.Traits.Append(TraitId.Thundercloud))
            && UnitCatalog.KataKRb.Traits.SequenceEqual(UnitCatalog.Kata.Traits.Append(TraitId.Thundercloud).Append(TraitId.ThundercloudKeep))
            && new[] { UnitCatalog.ShigaSIa, UnitCatalog.ShigaSIb }.All(d => d.MaxHp == 52 && d.Attack == 9 && d.Speed == 3 && d.Id == "shiga")
            && new[] { UnitCatalog.KataKRa, UnitCatalog.KataKRb }.All(d => d.MaxHp == UnitCatalog.Kata.MaxHp && d.Attack == UnitCatalog.Kata.Attack && d.Speed == UnitCatalog.Kata.Speed && d.Id == "kata")
            && !new[] { UnitCatalog.ShigaSIa, UnitCatalog.ShigaSIb, UnitCatalog.KataKRa, UnitCatalog.KataKRb }.Any(UnitCatalog.Everyone.Contains));
        Expect("(c) 割り込み・雷雲の札の保持者は `All` に 0 枚",
            !UnitCatalog.All.Any(u => u.Traits.Any(t => t is TraitId.ShockWhipBolt or TraitId.ShockWhipFlurry or TraitId.Thundercloud or TraitId.ThundercloudKeep)));

        // 帳簿の検査: 代表台 17 台 × 版 × seed 0..39
        var bs = Boards();
        long badLedgerA = 0, badLedgerB = 0, firesA = 0, firesB = 0, askedVsChains = 0, overAsk = 0, cowerOver = 0, boltOnlyA = 0;
        long kraBad = 0, krbOver = 0, kraCasts = 0, krbCasts = 0, oldCloud = 0, nondet = 0;
        foreach (var b in bs)
        {
            var w = WaveOf(b.Wave);
            for (int s = 0; s < 40; s++)
            {
                foreach (var v in ShigaVers)
                {
                    var p = BattleEngine.Materialize(Apply(b.Make(), v), BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), s, verbose: false);
                    if (!r.TallyByUnit.TryGetValue("shiga", out var t)) continue;
                    var sh = p.First(u => u.Def.Id == "shiga");
                    int end = sh.RawCounter(StoredChargeTrait.Key);
                    long chains = t.ChainIniHist?.Sum() ?? 0;
                    if (t.SwAsked != (v.Name == "SG-a" ? 0 : chains)) askedVsChains++;   // 1つの連鎖につき1回だけ問う（規定は問わない）
                    if (t.SwFires > t.SwAsked) overAsk++;
                    if (t.WhipCowered > t.WhipSwings - t.SwFires) cowerOver++;           // 割り込みの振りは怖気の数に入らない
                    if (v.Name == "SI-a") { firesA += t.SwFires; if (t.ChargeGains - StoredChargeTrait.Cap * t.SwFires != end) badLedgerA++; if (t.BoltCasts != t.SwFires) boltOnlyA++; }
                    if (v.Name == "SI-b") { firesB += t.SwFires; if (t.ChargeGains - t.SwFires != end) badLedgerB++; if (t.BoltCasts != 0) boltOnlyA++; }
                    if (v.Name == "SG-a" && (t.SwFires != 0 || t.BoltCasts != 0)) boltOnlyA++;
                }
                if (!Has(b.Make(), UnitCatalog.Kata)) continue;
                foreach (var v in KataVers)
                {
                    var r = BattleEngine.Run(BattleEngine.Materialize(Apply(b.Make(), v), BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                    if (!r.TallyByUnit.TryGetValue("kata", out var t)) continue;
                    long pops = 0; if (t.ThunderPopsHist is not null) for (int k = 0; k < t.ThunderPopsHist.Length; k++) pops += k * t.ThunderPopsHist[k];
                    bool histCapped = t.ThunderPopsHist is not null && t.ThunderPopsHist[^1] > 0;
                    if (v.Name == "旧") oldCloud += t.CloudAtCastSum;
                    if (v.Name == "KR-a") { kraCasts += t.ThunderCasts; if (!histCapped && t.CloudAtCastSum != pops) kraBad++; }   // KR-a の雷雲 ＝ 前の雷から弾けた敵の数
                    if (v.Name == "KR-b") { krbCasts += t.ThunderCasts; if (t.CloudAtCastMax > ThundercloudTrait.KeepCap) krbOver++; }
                }
            }
        }
        Expect("(d) 割り込みは敵の連鎖1つにつき1回だけ問う（シガ自身の一撃の連鎖は除く）・撃つのは問うた数以下", askedVsChains == 0 && overAsk == 0, $"ずれ {askedVsChains} ／ 超過 {overAsk}");
        Expect("(e) SI-a: 蓄電の帳簿（増えた数 − 4 × 割り込み ＝ 終わりの蓄電）・雷霆は割り込みのときだけ", firesA > 0 && badLedgerA == 0 && boltOnlyA == 0, $"割り込み {firesA}・ずれ {badLedgerA} ／ 雷霆のずれ {boltOnlyA}");
        Expect("(f) SI-b: 蓄電の帳簿（増えた数 − 割り込み ＝ 終わりの蓄電）", firesB > 0 && badLedgerB == 0, $"割り込み {firesB}・ずれ {badLedgerB}");
        Expect("(g) 割り込みの振りは怖気づかない（怖気 ≦ 手番の振り）", cowerOver == 0, $"{cowerOver} 戦");
        Expect("(h) 規定のカタの雷雲は 0・KR-a の雷雲 ＝ 前の雷から弾けた敵の数・KR-b の雷雲 ≦ 8", oldCloud == 0 && kraBad == 0 && krbOver == 0 && kraCasts > 0 && krbCasts > 0, $"旧 {oldCloud} ／ KR-a ずれ {kraBad} ／ KR-b 超過 {krbOver}");

        // (i) 粛で割り込みが止まる（本編の第2〜5波・シガ在席の行）
        long hushed = 0, fires2 = 0;
        foreach (var (_, f) in CompareBuilds().Where(r => Has(r.F, UnitCatalog.Shiga)))
            foreach (var v in ShigaVers.Skip(1))
                for (int wi = 1; wi < EnemyCatalog.Stages.Count; wi++)
                    for (int s = 0; s < 20; s++)
                    {
                        var r = BattleEngine.Run(Apply(f, v), EnemyCatalog.Stages[wi].Enemy, s, verbose: false);
                        if (r.TallyByUnit.TryGetValue("shiga", out var t)) { hushed += t.SwHushed; fires2 += t.SwFires; }
                    }
        Expect("(i) 粛（盤面ルール）が生きている間は割り込みが止まる（`CanActOutOfTurn` を通る）", hushed > 0, $"止まった {hushed} ／ 割り込んだ {fires2}");
        // (j) 軽い口と詳しい口・決定性
        int bad = 0;
        foreach (var v in ShigaVers.Concat(KataVers))
            foreach (var b in bs.Where(b => b.Name.StartsWith("トウ+シガ+カタ") || b.Name.StartsWith("R4 ボス")))
                for (int s = 0; s < 20; s++)
                {
                    var f = Apply(b.Make(), v);
                    var w = WaveOf(b.Wave);
                    bool a = FightLite(f, w, s, out int ta);
                    var d = FightDeep(f, w, s);
                    if (a != (d.Wins == 1) || (a && ta != d.WinT)) bad++;
                    if (FightLite(f, w, s, out int tb) != a || tb != ta) nondet++;
                }
        Expect("(j) 軽い口と詳しい口で勝敗・決着T が一致（6 版）", bad == 0, $"{bad} 件");
        Expect("(k) seed 決定的", nondet == 0, $"{nondet} 件");
        // (l) 新しい本体は PickOne ／ Roll を呼ばない
        string root = Directory.GetCurrentDirectory();
        string en = File.ReadAllText(Path.Combine(root, "BattleCore", "BattleEngine.cs"));
        int j0 = en.IndexOf("void AfterChain(", StringComparison.Ordinal), j1 = en.IndexOf("/// 感電で痺れる（第216期・S1〜S3）", StringComparison.Ordinal);
        bool scanned = j0 > 0 && j1 > j0;
        Expect("(l) 連鎖の後の口・割り込みの本体は `PickOne` ／ `Roll` を呼ばない（ソースの走査）", scanned && !en[j0..j1].Contains("PickOne") && !en[j0..j1].Contains("Roll("));
        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "すべて ○。" : $"× が {fail} 件。");
        Environment.ExitCode = fail == 0 ? 0 : 1;
    }

    static void LogOne(string wave, string ver, string order, int seed)
    {
        var w = WaveOf(wave);
        var v = VerOf(ver);
        var f = Apply(Seat(Order(order)), v);
        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# shock289 log —— {BA.SeatsNamed(f)} × {v.Name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
