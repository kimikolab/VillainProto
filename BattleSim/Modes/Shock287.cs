using System.Collections.Concurrent;
using BattleCore;
using static Common;
using B283 = Boss283Diag;
using BA = BurnAuditDiag;

// =====================================================================================
// shock287 —— 第287期「感電軸の台探索（ボス・近衛・大隊）」と「シガの怖気（漏れが得に回る経路）」。
// 指示書は design/PHASE287_SHOCK_AXIS_GRID_SPEC.md ／ 報告は design/PHASE287_SHOCK_AXIS_GRID.md。
//
//     dotnet run --project BattleSim -c Release 0 shock287 p0           # Phase 0: 台数と所要の見積もり・ボスへの放電のログ・クグの組み付きが精鋭に掛かるか
//     dotnet run --project BattleSim -c Release 0 shock287 shiga        # 段1: 責め苦 × 近衛 ／ 大隊 × T3 ／ T3n のシガの振（電・怖）と帯電していた手番の割合
//     dotnet run --project BattleSim -c Release 0 shock287 grid <ボス|近衛|大隊>   # 段2: 固定枠（トウ T3 ＋ シガ）＋ 探索枠3 × 席 120 → 足切り（seed 0..19）→ seed 0..199 → ドルガ対照
//     dotnet run --project BattleSim -c Release 0 shock287 check        # 自己検査
//     dotnet run --project BattleSim -c Release 0 shock287 log <波> <席の並び（短い名前を ・ で5つ）> [seed]
//
// 駒・札・数値・波は1つも変えない（器具だけ）。トウは規定（第287期から T3）。
// =====================================================================================
static class Shock287Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        switch (mode)
        {
            case "p0": P0(); return;
            case "shiga": Shiga(); return;
            case "grid": Grid(args.Length > 3 ? args[3] : "ボス"); return;
            case "check": Check(); return;
            case "log": LogOne(args.Length > 3 ? args[3] : "ボス", args.Length > 4 ? args[4] : "", args.Length > 5 ? int.Parse(args[5]) : 0); return;
            default: Console.WriteLine("shock287: モードは p0 / shiga / grid / check / log。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 候補と波（測る前に固定・指示書 §4-1）
    // ---------------------------------------------------------------------------------
    /// <summary>固定枠: トウ（規定 T3）＋ シガ。</summary>
    internal static readonly UnitDef[] Fixed = { UnitCatalog.Tou, UnitCatalog.Shiga };

    /// <summary>感電の駒（カタ・クグ・ソム）。</summary>
    internal static readonly UnitDef[] ShockPool = { UnitCatalog.Kata, UnitCatalog.Kugu, UnitCatalog.Som };

    /// <summary>ヒーラー（第283期 Phase 0 の機械的定義）。`Boss283Diag.HealPool` は呼ぶたびにソースを走査するので、1度だけ引いて持つ。</summary>
    internal static readonly UnitDef[] Heal = B283.HealPool;

    /// <summary>探索枠の候補 ＝ 感電の駒 ＋ 第283期の寿命側 11 枚 ＋ ヒーラー（第283期 Phase 0 の機械的定義）。</summary>
    internal static readonly UnitDef[] Pool = ShockPool.Concat(B283.LifePool).Concat(Heal).Distinct().ToArray();

    /// <summary>探索枠3 の組（ヒーラーは 0 ／ 1 枚——2 枚の組は作らない＝ポンの憲法。ツギ × リリもここで除かれる）。</summary>
    internal static List<UnitDef[]> Lineups()
    {
        var heal = Heal;
        var pool = Pool;
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

    internal sealed record Wave(string Name, Func<List<UnitState>> Make, bool Boss);
    internal static readonly Wave[] Waves =
    {
        new("ボス", () => BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None), true),
        new("近衛", () => BattleEngine.MaterializeEnemy(EliteDiag.Five, EliteDiag.Elite), false),
        new("大隊", () => BattleEngine.MaterializeEnemy(EliteDiag.Nine, EliteDiag.Elite), false),
    };
    static Wave WaveOf(string n) => Waves.First(w => w.Name == n);

    // 段の切り方（測る前に固定・指示書 §4-1 ／ §4-2）
    const int CutSeeds = 20, Seeds = 200;
    /// <summary>足切り: ボスは seed 0..19 で1勝以上、近衛 ／ 大隊は 5 勝以上（25%）。届いたの線（50%）より緩く置く。</summary>
    static int CutWins(Wave w) => w.Boss ? 1 : 5;
    /// <summary>届いた（指示書 §4-2）: ボスは勝率が 0% を離れドルガ対照が 0%。近衛 ／ 大隊は勝率 50% 以上かつドルガ対照がその半分以下。</summary>
    static bool Reached(Wave w, int wins, int dolgaWins)
        => w.Boss ? wins > 0 && dolgaWins == 0 : wins * 2 >= Seeds && dolgaWins * 2 <= wins;
    /// <summary>勝率の線だけ（ドルガ対照を回すかどうか）。</summary>
    static bool WinLine(Wave w, int wins) => w.Boss ? wins > 0 : wins * 2 >= Seeds;

    static Formation Seat(UnitDef[] order) => B283.Seat(order);
    static Formation ToDolga(Formation f, UnitDef from) => FvSwap(f, from, UnitCatalog.Dolga);

    static bool FightLite(Formation f, Wave w, int seed, out int turns)
    {
        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), seed, verbose: false);
        turns = r.Turns;
        return r.PlayerWon;
    }

    static int Wins(Formation f, Wave w, int from, int n, out long winT)
    {
        int wins = 0; winT = 0;
        for (int s = from; s < from + n; s++) if (FightLite(f, w, s, out int t)) { wins++; winT += t; }
        return wins;
    }

    // ---------------------------------------------------------------------------------
    // 詳しい計数（代表の台・verbose）
    // ---------------------------------------------------------------------------------
    sealed class Deep
    {
        public long N, Wins, WinT, FoeDis, AllyDis, Chains, ChainUnits, FoeStall, Cower, Swings, Wired, ToumDied, ToumDeathT, ShigaDied, ShigaDeathT, Grapple, GrappleStall;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Merge(Deep o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; FoeDis += o.FoeDis; AllyDis += o.AllyDis; Chains += o.Chains; ChainUnits += o.ChainUnits; FoeStall += o.FoeStall;
            Cower += o.Cower; Swings += o.Swings; Wired += o.Wired; ToumDied += o.ToumDied; ToumDeathT += o.ToumDeathT; ShigaDied += o.ShigaDied; ShigaDeathT += o.ShigaDeathT;
            Grapple += o.Grapple; GrappleStall += o.GrappleStall;
        }
    }

    static Deep FightDeep(Formation f, Wave w, int seed)
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
            else { d.FoeDis += t.DischargeTaken; d.Chains += t.ChainRoots; d.ChainUnits += t.ChainUnits; d.FoeStall += t.StallStun + t.StallGrappled; }
        }
        if (r.TallyByUnit.TryGetValue(UnitCatalog.Shiga.Id, out var st)) { d.Cower += st.WhipCowered; d.Swings += st.WhipSwings; d.Wired += st.WiredSwings; }
        if (r.TallyByUnit.TryGetValue(UnitCatalog.Kugu.Id, out var kt)) { d.Grapple += kt.GrappleFires; d.GrappleStall += kt.GrappleStalled; }
        UnitState? tou = p.FirstOrDefault(u => u.Def.Id == UnitCatalog.Tou.Id), shiga = p.FirstOrDefault(u => u.Def.Id == UnitCatalog.Shiga.Id);
        foreach (var ev in r.Events.Where(ev => ev.Kind == BattleEventKind.Death))
        {
            if (tou is not null && ev.TargetId == tou.InstanceId && d.ToumDied == 0) { d.ToumDied = 1; d.ToumDeathT = ev.Turn; }
            if (shiga is not null && ev.TargetId == shiga.InstanceId && d.ShigaDied == 0) { d.ShigaDied = 1; d.ShigaDeathT = ev.Turn; }
        }
        return d;
    }

    static Deep MeasureDeep(Formation f, Wave w, int from, int n)
    {
        var parts = new Deep[n];
        Parallel.For(0, n, i => parts[i] = FightDeep(f, w, from + i));
        var all = new Deep();
        foreach (var d in parts) all.Merge(d);
        return all;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static string Order(UnitDef[] o) => string.Join("・", o.Select(Short));

    // ---------------------------------------------------------------------------------
    // Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lu = Lineups();
        int heal0 = lu.Count(t => !t.Any(Heal.Contains));
        Console.WriteLine("# 第287期 Phase 0 —— 感電軸の台探索");
        Console.WriteLine();
        Console.WriteLine($"## §1 台数と所要の見積もり");
        Console.WriteLine();
        Console.WriteLine($"候補 {Pool.Length} 枚（感電 {ShockPool.Length} ／ 寿命側 {B283.LifePool.Length} ／ ヒーラー {Heal.Length}: {Order(Heal)}）→ 組 {lu.Count}（ヒーラー 0 枚 {heal0} ／ 1 枚 {lu.Count - heal0}）× 席 120 ＝ **{lu.Count * 120:N0} 台 ／ 波**");
        Console.WriteLine();
        Console.WriteLine("| 波 | 1戦の所要（µs・32 並列の実効） | 足切り段（seed 0..19）の見積もり | 足切りを通る割合（無作為 600 台） | 本段（seed 0..199）＋ ドルガ対照の見積もり |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        var rng = new Random(287);
        var sample = Enumerable.Range(0, 600).Select(_ => { var t = lu[rng.Next(lu.Count)]; var o = Fixed.Concat(t).OrderBy(_ => rng.Next()).ToArray(); return o; }).ToArray();
        foreach (var w in Waves)
        {
            var st = System.Diagnostics.Stopwatch.StartNew();
            int pass = 0;
            Parallel.For(0, sample.Length, i => { if (Wins(Seat(sample[i]), w, 0, CutSeeds, out _) >= CutWins(w)) Interlocked.Increment(ref pass); });
            double us = st.Elapsed.TotalMilliseconds * 1000.0 / (sample.Length * CutSeeds);
            double cut = us * lu.Count * 120 * CutSeeds / 1e6;
            double frac = (double)pass / sample.Length;
            double main = us * lu.Count * 120 * frac * (Seeds * 2) / 1e6;
            Console.WriteLine($"| {w.Name} | {us:F1} | {cut / 60:F1} 分 | {100 * frac:F1}% | {main / 60:F1} 分（上限・全部がドルガ対照まで進んだとき） |");
        }
        Console.WriteLine();

        Console.WriteLine("## §2 ボスに対する感電（放電先が無いとき）——ログ1本");
        Console.WriteLine();
        var basis = Seat(new[] { UnitCatalog.Tou, UnitCatalog.Gald, UnitCatalog.Kata, UnitCatalog.Shiga, UnitCatalog.Hisa });
        var bp = BattleEngine.Materialize(basis, BattleContext.PlayerTeam);
        var br = BattleEngine.Run(bp, WaveOf("ボス").Make(), 0, verbose: true);
        Console.WriteLine($"台 ＝ {BA.SeatsNamed(basis)} × ボス × seed 0 → {(br.PlayerWon ? "勝ち" : "負け")} T{br.Turns}");
        Console.WriteLine();
        Console.WriteLine("```");
        int shown = 0;
        string? prev = null;
        foreach (var l in br.Log)
        {
            if (l.Text.Contains("勇者") && (l.Text.Contains("感電") || l.Text.Contains("帯電") || l.Text.Contains("放電") || l.Text.Contains("痺れ")))
            {
                if (prev is not null && shown < 24) { Console.WriteLine(prev); shown++; }
                if (shown < 24) { Console.WriteLine(l.Text); shown++; }
            }
            prev = l.Text;
        }
        Console.WriteLine("```");
        if (br.TallyByUnit.TryGetValue(EnemyCatalog.BossRegular.Id, out var bt))
            Console.WriteLine($"勇者: 感電が弾けた {bt.ShockSpent} 回 ／ 根の連鎖 {bt.ChainRoots} 回・弾けた駒の延べ {bt.ChainUnits} ／ 放電で受けた HP {bt.DischargeTaken} ／ 感電で痺れた {bt.ShockStunned} ／ 痺れを防いだ {bt.ShockStunGuarded}");
        Console.WriteLine();

        Console.WriteLine("## §4 クグの組み付きが精鋭の敵に掛かるか（責め苦の後3 ドルガ → クグ・seed 0..49）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 組み付いた（1戦） | 組んだまま維持 | 止めた敵の手番 | ほどけた |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        var kRow = FvSwap(CompareBuilds().First(r => r.Name == "責め苦 (トウ×シガ)").F, UnitCatalog.Dolga, UnitCatalog.Kugu);
        foreach (var w in Waves)
        {
            long f = 0, h = 0, s = 0, b = 0;
            for (int seed = 0; seed < 50; seed++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(kRow, BattleContext.PlayerTeam), w.Make(), seed, verbose: false);
                if (r.TallyByUnit.TryGetValue(UnitCatalog.Kugu.Id, out var t)) { f += t.GrappleFires; h += t.GrappleHolds; s += t.GrappleStalled; b += t.GrappleBreaks; }
            }
            Console.WriteLine($"| {w.Name} | {Per(f, 50)} | {Per(h, 50)} | {Per(s, 50)} | {Per(b, 50)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"台 ＝ {BA.SeatsNamed(kRow)}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 段1: シガの怖気
    // ---------------------------------------------------------------------------------
    static void Shiga()
    {
        Console.WriteLine("# 第287期 段1 —— シガの怖気（責め苦 (トウ×シガ) × 近衛 ／ 大隊 × T3 ／ T3n・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("振 ＝ シガの一振り ／ 電 ＝ 電気鞭（自分の感電を使って振った・怖気づかない）／ 怖 ＝ 怖気づいた ／ 縛 ＝ 主目標が動けない敵だった振（怖気の判定に入らない）＝ 振 − 電 − 怖 ／ "
                          + "帯電率 ＝ 電 ÷ 振（シガが帯電していた手番の割合）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 勝率 | 振 | 電 | 怖 | 縛 | 帯電率 | 怖 ÷ 振 | シガが倒れたT（倒れた戦の割合） | 潰れた敵の手番 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        var row = CompareBuilds().First(r => r.Name == "責め苦 (トウ×シガ)").F;
        foreach (var w in Waves.Where(w => !w.Boss))
            foreach (var (vn, d) in new[] { ("T3", UnitCatalog.TouT3), ("T3n", UnitCatalog.TouT3n) })
            {
                var a = MeasureDeep(FvSwap(row, UnitCatalog.Tou, d), w, 0, Seeds);
                long bound = a.Swings - a.Wired - a.Cower;
                Console.WriteLine($"| {w.Name} | {vn} | {F1(a.Win)} | {Per(a.Swings, a.N)} | {Per(a.Wired, a.N)} | {Per(a.Cower, a.N)} | {Per(bound, a.N)} | {(a.Swings == 0 ? "—" : (100.0 * a.Wired / a.Swings).ToString("F1") + "%")} | "
                    + $"{(a.Swings == 0 ? "—" : (100.0 * a.Cower / a.Swings).ToString("F1") + "%")} | {Per1(a.ShigaDeathT, a.ShigaDied)}（{F1(100.0 * a.ShigaDied / a.N)}%） | {Per(a.FoeStall, a.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine($"台 ＝ {BA.SeatsNamed(row)}");
    }

    // ---------------------------------------------------------------------------------
    // 段2: 格子
    // ---------------------------------------------------------------------------------
    sealed record BoardRes(UnitDef[] Order, int CutW, int Wins, long WinT, int Dolga, bool Reach);

    static void Grid(string waveName)
    {
        var w = WaveOf(waveName);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var lu = Lineups();
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(Fixed.Concat(t).ToArray()));
        var res = new BoardRes?[boards.Count];
        int cutPass = 0, winPass = 0;
        Parallel.For(0, boards.Count, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            var f = Seat(boards[i]);
            int cw = Wins(f, w, 0, CutSeeds, out _);
            if (cw < CutWins(w)) return;
            Interlocked.Increment(ref cutPass);
            int wins = Wins(f, w, 0, Seeds, out long wt);
            int dg = -1;
            if (WinLine(w, wins))
            {
                Interlocked.Increment(ref winPass);
                dg = Wins(ToDolga(f, UnitCatalog.Tou), w, 0, Seeds, out _);
            }
            res[i] = new BoardRes(boards[i], cw, wins, wt, dg, dg >= 0 && Reached(w, wins, dg));
        });
        var all = res.Where(r => r is not null).Select(r => r!).ToList();
        var reached = all.Where(r => r.Reach).ToList();
        bool IsHeal(UnitDef d) => Heal.Contains(d);
        string Key(UnitDef[] o) => string.Join(",", o.Select(d => d.Id).OrderBy(x => x, StringComparer.Ordinal));

        Console.WriteLine($"# 第287期 段2 —— 感電軸の格子 × {w.Name}");
        Console.WriteLine();
        Console.WriteLine($"固定枠 トウ（規定 T3）＋ シガ ／ 探索枠3（候補 {Pool.Length} 枚・ヒーラー ≦ 1）× 席 120 ＝ {boards.Count:N0} 台。足切り seed 0..{CutSeeds - 1}（{CutWins(w)} 勝以上）→ seed 0..{Seeds - 1} → ドルガ対照（トウ → ドルガ・同じ席・同じ seed）");
        Console.WriteLine($"届いた ＝ {(w.Boss ? "勝率が 0% を離れ、ドルガ対照が 0%" : "勝率 50% 以上、かつドルガ対照がその半分以下")}");
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
        RowOf(w.Boss ? "勝率 ＞ 0%" : "勝率 ≧ 50%", all.Where(r => r.Dolga >= 0).Select(r => r.Order));
        RowOf("**届いた**（ドルガ対照で落ちる）", reached.Select(r => r.Order));
        RowOf("勝率の線は越えたがドルガ対照で落ちない（守り・回復の到達）", all.Where(r => r.Dolga >= 0 && !r.Reach).Select(r => r.Order));
        Console.WriteLine();

        Console.WriteLine("## 表2 駒別の到達寄与（届いた台にその駒が入っていた数・候補だけ）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 種 | 届いた台 | 届いた組 | 全体の台（分母） | 割合 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var d in Pool)
        {
            string kind = ShockPool.Contains(d) ? "感電" : IsHeal(d) ? "ヒーラー" : "寿命側";
            var mine = reached.Where(r => r.Order.Contains(d)).ToList();
            int denom = boards.Count(o => o.Contains(d));
            Console.WriteLine($"| {(d == UnitCatalog.Kugu ? "**" + d.Name + "**" : d.Name)} | {kind} | {mine.Count:N0} | {mine.Select(r => Key(r.Order)).Distinct().Count()} | {denom:N0} | {(denom == 0 ? "—" : (100.0 * mine.Count / denom).ToString("F1") + "%")} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表3 上位の台（届いた台・勝率 → 倒しT の順・上位 15）");
        Console.WriteLine();
        Console.WriteLine("| # | 席（前1・前3・中央・後1・後3） | ヒーラー | 勝率 | 倒しT | ドルガ対照 |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|");
        var top = reached.OrderByDescending(r => r.Wins).ThenBy(r => r.Wins == 0 ? double.MaxValue : (double)r.WinT / r.Wins).ThenBy(r => Order(r.Order), StringComparer.Ordinal).ToList();
        for (int i = 0; i < Math.Min(15, top.Count); i++)
        {
            var r = top[i];
            Console.WriteLine($"| {i + 1} | {Order(r.Order)} | {r.Order.Count(IsHeal)} | {F1(100.0 * r.Wins / Seeds)} | {(r.Wins == 0 ? "—" : ((double)r.WinT / r.Wins).ToString("F2"))} | {F1(100.0 * r.Dolga / Seeds)} |");
        }
        Console.WriteLine();

        // 代表の台（上位3・組が重ならないように選ぶ）
        var reps = new List<BoardRes>();
        foreach (var r in top) { if (reps.Any(x => Key(x.Order) == Key(r.Order))) continue; reps.Add(r); if (reps.Count == 3) break; }
        if (reps.Count == 0)
        {
            // 届いた台が無いときは、勝率の上位（足切りを通った台）を代表にする（届かない理由を読むため）
            foreach (var r in all.OrderByDescending(r => r.Wins).ThenBy(r => Order(r.Order), StringComparer.Ordinal))
            { if (reps.Any(x => Key(x.Order) == Key(r.Order))) continue; reps.Add(r); if (reps.Count == 3) break; }
            Console.WriteLine("**届いた台は 0。** 下の代表は足切りを通った台の勝率の上位（届かない理由を読むため）。");
            Console.WriteLine();
        }
        Console.WriteLine("## 表4 代表の台の中身（seed 0..199・verbose）と対照");
        Console.WriteLine();
        Console.WriteLine("放敵 ／ 放味 ＝ 放電で削れた HP（受けた側・1戦）／ 連鎖 ＝ 敵側の1回の起爆で弾けた敵 ／ 潰敵 ＝ 痺れ・組み付きで潰れた敵の手番 ／ 怖 ＝ シガの怖気（振）／ ト死 ・シ死 ＝ トウ ・シガが倒れたT（倒れた戦の割合）／ 組 ＝ クグの組み付き（1戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 倒しT | 放敵 | 放味 | 連鎖 | 潰敵 | 怖（振） | ト死 | シ死 | 組 | トウ → ドルガ | シガ → ドルガ |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var r in reps)
        {
            var f = Seat(r.Order);
            var d = MeasureDeep(f, w, 0, Seeds);
            int dt = Wins(ToDolga(f, UnitCatalog.Tou), w, 0, Seeds, out _);
            int ds = Wins(ToDolga(f, UnitCatalog.Shiga), w, 0, Seeds, out _);
            Console.WriteLine($"| {Order(r.Order)} | {F1(d.Win)} | {Per(d.WinT, d.Wins)} | {Per1(d.FoeDis, d.N)} | {Per1(d.AllyDis, d.N)} | {Per(d.ChainUnits, d.Chains)} | {Per(d.FoeStall, d.N)} | {Per(d.Cower, d.N)}（{Per(d.Swings, d.N)}） | "
                + $"{Per1(d.ToumDeathT, d.ToumDied)}（{F1(100.0 * d.ToumDied / d.N)}%） | {Per1(d.ShigaDeathT, d.ShigaDied)}（{F1(100.0 * d.ShigaDied / d.N)}%） | {Per(d.Grapple, d.N)} | {F1(100.0 * dt / Seeds)} | {F1(100.0 * ds / Seeds)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒（足切り {boards.Count:N0} 台 → 本段 {cutPass:N0} 台 → ドルガ対照 {winPass:N0} 台）。");
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
        Console.WriteLine("# shock287 check");
        Console.WriteLine();
        Expect("(a) 固定枠のトウは規定（T3）", ReferenceEquals(Fixed[0], UnitCatalog.Tou) && ReferenceEquals(UnitCatalog.Tou, UnitCatalog.TouT3));
        var lu = Lineups();
        Expect("(b) 組にヒーラー 2 枚以上は 0・ツギ × リリは 0・固定枠と候補は重ならない",
            lu.All(t => t.Count(Heal.Contains) <= 1) && !lu.Any(t => t.Contains(UnitCatalog.Tsugi) && t.Contains(UnitCatalog.Lili))
            && !Pool.Any(Fixed.Contains) && !Pool.Contains(UnitCatalog.Dolga), $"{lu.Count} 組・候補 {Pool.Length} 枚");
        Expect("(c) 席の並べ方は 120 通りで重複なし", B283.Perms(Fixed.Concat(lu[0]).ToArray()).Select(o => string.Join(",", o.Select(d => d.Id))).Distinct().Count() == 120);
        // (d) 軽い口と詳しい口の勝敗が一致（3 波 × 3 台 × seed 0..19）
        int bad = 0;
        foreach (var w in Waves)
            foreach (var o in new[] { Fixed.Concat(lu[0]).ToArray(), Fixed.Concat(lu[lu.Count / 2]).ToArray(), Fixed.Concat(lu[^1]).ToArray() })
                for (int s = 0; s < 20; s++)
                {
                    bool a = FightLite(Seat(o), w, s, out int ta);
                    var d = FightDeep(Seat(o), w, s);
                    if (a != (d.Wins == 1) || (a && ta != d.WinT)) bad++;
                }
        Expect("(d) 軽い口（verbose なし）と詳しい口（verbose）で勝敗と決着T が一致", bad == 0, $"{bad} 件");
        // (e) ドルガ対照はトウだけを差し替える
        var f0 = Seat(Fixed.Concat(lu[0]).ToArray());
        var fd = ToDolga(f0, UnitCatalog.Tou);
        Expect("(e) ドルガ対照はトウの席だけがドルガに替わる",
            Enumerable.Range(0, 5).Count(i => !ReferenceEquals(f0[i], fd[i])) == 1 && fd.Occupied().Any(o => o.Def == UnitCatalog.Dolga) && !fd.Occupied().Any(o => o.Def.Id == UnitCatalog.Tou.Id));
        // (f) 決定性
        int nd = 0;
        for (int s = 0; s < 20; s++) if (FightLite(f0, Waves[1], s, out int t1) != FightLite(f0, Waves[1], s, out int t2) || t1 != t2) nd++;
        Expect("(f) seed 決定的", nd == 0, $"{nd} 件");
        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "すべて ○。" : $"× が {fail} 件。");
        Environment.ExitCode = fail == 0 ? 0 : 1;
    }

    static void LogOne(string wave, string order, int seed)
    {
        var w = WaveOf(wave);
        var names = order.Split('・', StringSplitOptions.RemoveEmptyEntries);
        var defs = names.Select(n => UnitCatalog.All.First(d => Short(d) == n)).ToArray();
        var f = Seat(defs);
        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# shock287 log —— {BA.SeatsNamed(f)} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
