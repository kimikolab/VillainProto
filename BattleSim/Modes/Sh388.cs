using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S387 = Sh387Diag;

// =====================================================================================
// sh388 —— 第388期【軽い期】本体: ハギの見直し——大鎌で刈る死神（版 H1 ／ H2 ／ H3 ／ H3s）。
// 指示書は design/PHASE388_HAGI_REAPER_SPEC.md、報告は design/PHASE388_HAGI_REAPER.md。
//
//     dotnet run --project BattleSim -c Release 0 sh388 cmp       # §3-1 の 1: 今のハギのいる `compare` の行 × 版（第1〜5波・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh388 swap      # §3-1 の 2: 試遊の台 × 1枚をハギ（版）に替えた台（段1 seed 0..9 → 版ごとの上位 3 台 × 8 物差し seed 0..99・同じ席に素体 ／ 今のハギ）
//     dotnet run --project BattleSim -c Release 0 sh388 atk       # §3-1 の 3: アタッカーの1枚をハギ（版）に替えた台 5 × 8 物差し（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh388 rica      # §3-1 の 4: リィカと並べる台 2 × 8 物差し（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh388 check     # 自己検査
//
// **規定の駒は1つも変えない。** 替えた台 ／ 素体は器具の中だけ（試遊の台に足す 2 台を除く）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p388/predict.md`）。
// =====================================================================================
static partial class Sh388Diag
{
    internal const string PredictFile = ".tmp/p388/predict.md";
    const string Tag = "第388期";
    const int Seeds = 100, CompareSeeds = 200, S1Seeds = 10, TT = 9;
    const double Up = 3.0;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "cmp" or "swap" or "atk" or "rica" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh388: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "cmp": Cmp(); return;
            case "swap": Swap(); return;
            case "atk": Boards(AtkBoards(), "§3-1 の 3 アタッカーとしての比べ"); return;
            case "rica": Boards(RicaBoards(), "§3-1 の 4 リィカと並べる台"); return;
            case "check": Check(); return;
            case "log": Log1(args); return;
            default: Console.WriteLine("sh388: モードは cmp / swap / atk / rica / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => double.IsNaN(x) ? "—" : x.ToString("#,0");
    static string Sg(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pc(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string SeatName(int s) => s switch { 0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3", _ => $"席{s}" };

    // ---------------------------------------------------------------------------------
    // 版 ／ 列 ／ 波
    // ---------------------------------------------------------------------------------
    internal static readonly (string Name, UnitDef D)[] Vers =
        { ("H1", UnitCatalog.HagiH1), ("H2", UnitCatalog.HagiH2), ("H3", UnitCatalog.HagiH3), ("H3s", UnitCatalog.HagiH3s) };
    /// <summary>素体（今のハギと同じ体・札なし・単体でふつうに殴る）。器具の中だけ（`Id` の尾に `_plain388`）。</summary>
    internal static readonly UnitDef PlainHagi = new()
    {
        Id = "hagi_plain388", Name = "素体のハギ", MaxHp = UnitCatalog.Hagi.MaxHp, Attack = UnitCatalog.Hagi.Attack, Speed = UnitCatalog.Hagi.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single, PlusText = "", MinusText = "",
    };
    /// <summary>列: 元の台 ／ 今のハギ ／ H1 ／ H2 ／ H3 ／ H3s ／ 素体（`null` ＝ 元の台のまま）。</summary>
    internal static readonly (string Name, UnitDef? D)[] Cols =
        { ("元", null), ("今のハギ", UnitCatalog.HagiH389), ("H1", UnitCatalog.HagiH1), ("H2", UnitCatalog.HagiH2), ("H3", UnitCatalog.HagiH3), ("H3s", UnitCatalog.HagiH3s), ("素体", PlainHagi) };

    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;   // ボス ／ 近衛 ／ 大隊 ／ 本編 第2 ／ 4 ／ 5波 ／ 九体 ／ 強 第2〜5波
    static readonly int[] W4 = { 0, 1, 2, 5 };                      // 4 波（ボス ／ 近衛 ／ 大隊 ／ 本編 第5波）
    const int WBoss = 0, WGuard = 1, WBat = 2, WStrong = 7;

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }
    static int[] Seats5(Formation f) => f.Occupied().Select(o => o.Slot).Where(s => s < 5).OrderBy(s => s).ToArray();
    static double WinRate(Formation f, I.W[] ws, int seeds)
    {
        int n = 0;
        foreach (var w in ws) for (int s = 0; s < seeds; s++) if (I.Raw(f, w, s, census: false).R.PlayerWon) n++;
        return 100.0 * n / (ws.Length * seeds);
    }
    static double[,] CGrid((string Name, Formation F)[] rows, int seeds)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw, wins = 0;
            for (int s = 0; s < seeds; s++) if (BattleEngine.Run(rows[ri].F, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / seeds;
        });
        return g;
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数（`Sh377Diag.One` の `extra`）
    // ---------------------------------------------------------------------------------
    static void Add(S377.B b, string k, long v) { if (v != 0) b.X[k] = b.X.GetValueOrDefault(k) + v; }
    static long X(S377.B b, string k) => b.X.GetValueOrDefault(k);

    /// <summary>ハギ（`Id` ＝ hagi）の帳簿 ／ 隊の1ターンの最大の与ダメ ／ 巻き込み ／ 勇者の HP（ボスの波だけ）。</summary>
    static Action<BattleResult, List<UnitState>, List<UnitState>, S377.B> Extra(bool boss) => (r, p, e, b) =>
    {
        var form = p.ToDictionary(u => u.InstanceId);
        var foe = e.ToDictionary(u => u.InstanceId);
        var sumTeam = new Dictionary<int, int>();
        var hagi = p.FirstOrDefault(u => u.Def.Id == "hagi");
        int hid = hagi?.InstanceId ?? -9;
        var dmgT = new long[BattleEngine.MaxTurns + 2];
        bool hAlive = hagi is not null;
        UnitState? hero = boss && e.Count > 0 ? e[0] : null;
        long heroHp = hero?.MaxHp ?? 0; int heroMax = hero?.MaxHp ?? 1; int heroT = 0; bool heroAlive = true;
        long crosses = 0, atkMax = 0;
        foreach (var x in r.Events)
        {
            int t = Math.Clamp(x.Turn, 0, BattleEngine.MaxTurns + 1);
            int tgt = x.TargetId ?? -3, act = x.ActorId ?? -2;
            switch (x.Kind)
            {
                case BattleEventKind.Summon: sumTeam[tgt] = x.Team ?? -1; break;
                case BattleEventKind.TurnStart:
                    if (hagi is not null && hAlive && t is >= 1 and < TT) Add(b, $"halive{t}", 1);
                    break;
                case BattleEventKind.StatSnapshot:
                    if (tgt == hid && x.Amount > atkMax) atkMax = x.Amount;
                    break;
                case BattleEventKind.Attack:
                    if (hero is not null && heroAlive && t is >= 1 and < TT && t > heroT) { heroT = t; Add(b, $"hhp{t}", heroHp); Add(b, $"hhn{t}", 1); }
                    break;
                case BattleEventKind.Damage:
                {
                    bool toFoe = foe.ContainsKey(tgt) || (sumTeam.TryGetValue(tgt, out int st) && st == BattleContext.EnemyTeam);
                    bool byUs = form.ContainsKey(act) || (sumTeam.TryGetValue(act, out int sa) && sa == BattleContext.PlayerTeam);
                    if (toFoe && byUs) dmgT[t] += x.Amount;
                    if (hero is not null && tgt == hero.InstanceId)
                    {
                        if (heroHp * 2 > heroMax && x.HpAfter > 0 && (long)x.HpAfter * 2 <= heroMax) crosses++;
                        heroHp = x.HpAfter;
                    }
                    break;
                }
                case BattleEventKind.Heal:
                    if (hero is not null && tgt == hero.InstanceId) heroHp = x.HpAfter;
                    break;
                case BattleEventKind.Revive:
                    if (tgt == hid) hAlive = true;
                    if (hero is not null && tgt == hero.InstanceId) { heroHp = x.HpAfter; heroAlive = true; }
                    break;
                case BattleEventKind.Death:
                    if (tgt == hid) hAlive = false;
                    if (hero is not null && tgt == hero.InstanceId)
                    {
                        heroAlive = false;
                        bool reaped = r.Log.Any(l => l.Text.Contains($"の大鎌が {hero.Name} を刈り取った", StringComparison.Ordinal));
                        if (reaped && act == hid && r.PlayerWon) { Add(b, "heroReaped", 1); Add(b, "heroReapedT", x.Turn); }
                    }
                    break;
            }
        }
        Add(b, "maxTurnDmg", dmgT.Max());
        if (hero is not null) Add(b, "heroCross", crosses);
        if (r.TallyByUnit.TryGetValue("hagi", out var h) && hagi is not null)
        {
            Add(b, "hIn", 1);
            Add(b, "strikes", h.R388Strikes); Add(b, "reaps", h.R388Reaps); Add(b, "reapHp", h.R388ReapHp); Add(b, "encores", h.R388Encores);
            Add(b, "pursuits", h.R388Pursuits); Add(b, "pursuitDmg", h.R388PursuitDmg); Add(b, "pursuitSelf", h.R388PursuitSelf);
            if (h.R388PursuitMax > 1000) Add(b, "p1000", 1);
            Add(b, "spill", h.R388Spill); Add(b, "spillHits", h.R388SpillHits);
            Add(b, "soulDeath", h.R388SoulDeath); Add(b, "soulCross", h.R388SoulCross); Add(b, "soulPeak", h.R388SoulPeak);
            if (h.R388SoulPeak > 20) Add(b, "soul20", 1);
            if (h.R388Soul3Turn > 0) { Add(b, "s3", 1); Add(b, "s3T", h.R388Soul3Turn); }
            if (h.R388Soul6Turn > 0) { Add(b, "s6", 1); Add(b, "s6T", h.R388Soul6Turn); }
            if (h.R388Soul9Turn > 0) { Add(b, "s9", 1); Add(b, "s9T", h.R388Soul9Turn); }
            Add(b, "atkPeak", Math.Max(atkMax, h.R388AtkPeak));
            Add(b, "haste", h.R388Haste);
            Add(b, "kills", h.Kills);
            Add(b, "hdmg", h.DamageToEnemy);
            if (h.R388SoulByTurn is { } sb) for (int t = 1; t < TT && t < sb.Length; t++) Add(b, $"soul{t}", sb[t]);
            Add(b, "pursueOld", r.Log.Count(l => l.Text.Contains("が倒れた隙に踏み込む", StringComparison.Ordinal)));
        }
        foreach (var (id, tl) in r.TallyByUnit)
        {
            if (tl.R388SpillTaken > 0) Add(b, "spillTo:" + id, tl.R388SpillTaken);
            if (tl.R388SpillDeaths > 0) Add(b, "spillDeath:" + id, tl.R388SpillDeaths);
        }
    };

    static S377.B[] M8(Formation f)
    {
        var g = new S377.B[W8.Length];
        Parallel.For(0, W8.Length, wi => g[wi] = S377.Measure(f, W8[wi].Ws, Seeds, Extra(wi == WBoss)));
        return g;
    }
    static double D4(S377.B[] a, S377.B[] b) => W4.Average(wi => a[wi].Win - b[wi].Win);
    static double A4(S377.B[] a) => W4.Average(wi => a[wi].Win);

    // ---------------------------------------------------------------------------------
    // §3-1 の 1: `compare` のハギの行
    // ---------------------------------------------------------------------------------
    static void Cmp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var all = CompareBuilds();
        var rows = all.Where(r => Has(r.F, "hagi")).ToArray();
        int nw = EnemyCatalog.Stages.Count;
        var cols = Cols.Skip(1).ToArray();   // 今のハギ ／ H1〜H3s ／ 素体
        var grids = cols.Select(c => CGrid(rows.Select(r => (r.Name, S387Replace(r.F, c.D!))).ToArray(), CompareSeeds)).ToArray();
        Console.WriteLine($"# {Tag} §3-1 の 1 `compare` のハギの行 —— {rows.Length} 行 × 版（第1〜5波・seed 0..{CompareSeeds - 1}・ハギだけ版にする）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 席 | " + string.Join(" | ", cols.Select(c => c.Name + " 第1〜5波")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(cols.Select(_ => "---|")));
        for (int ri = 0; ri < rows.Length; ri++)
        {
            string seat = string.Join("・", rows[ri].F.Occupied().Where(o => o.Def.Id == "hagi").Select(o => SeatName(o.Slot)));
            Console.WriteLine($"| {rows[ri].Name} | {seat} | {string.Join(" | ", grids.Select(g => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w])))))} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 平均（第2〜5波・行の平均）と、今のハギとの差");
        Console.WriteLine();
        Console.WriteLine("| 列 | 第2〜5波の平均 | 今のハギとの差 | 動いたセル（第2〜5波・今のハギと違う）| −10pt 以上落ちたセル |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        double Avg(double[,] g) => Enumerable.Range(0, rows.Length).Average(ri => Enumerable.Range(1, nw - 1).Average(w => g[ri, w]));
        for (int c = 0; c < cols.Length; c++)
        {
            int moved = 0, drop = 0;
            for (int ri = 0; ri < rows.Length; ri++) for (int w = 1; w < nw; w++) { if (grids[c][ri, w] != grids[0][ri, w]) moved++; if (grids[c][ri, w] - grids[0][ri, w] <= -10) drop++; }
            Console.WriteLine($"| {cols[c].Name} | {F1(Avg(grids[c]))} | {Sg(Avg(grids[c]) - Avg(grids[0]))} | {moved} | {drop} |");
        }
        Console.WriteLine();
        // 版ごとのハギの働き（`compare` の行 × 本編 第2〜5波・seed 0..19・台本）
        Console.WriteLine("## ハギの働き（`compare` の行 × 本編 第2〜5波・seed 0..19・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 列 | 斬撃 | 刈り取り（回 ／ HP）| 再行動 | 追撃（回 ／ 与ダメ）| 魂の最大 | 巻き込み（与えた HP）| 与ダメ |");
        Console.WriteLine("|---|--:|---|--:|---|--:|--:|--:|");
        var mainWs = Enumerable.Range(2, 4).Select(i => (I.W)new(i.ToString(), () => BattleEngine.Materialize(EnemyCatalog.Stages[i - 1].Enemy, BattleContext.EnemyTeam, EnemyScaleRule.Default), EnemyScaleRule.Default)).ToArray();
        foreach (var c in cols)
        {
            var parts = rows.Select(r => S377.Measure(S387Replace(r.F, c.D!), mainWs, 20, Extra(false))).ToArray();
            var a = new S377.B(); foreach (var x in parts) a.Add(x);
            Console.WriteLine($"| {c.Name} | {Per(X(a, "strikes"), a.N)} | {Per(X(a, "reaps"), a.N)} ／ {Per(X(a, "reapHp"), a.N)} | {Per(X(a, "encores"), a.N)} | {Per(X(a, "pursuits"), a.N)} ／ {Per(X(a, "pursuitDmg"), a.N)} | {Per(X(a, "soulPeak"), a.N)} | {Per(X(a, "spill"), a.N)} | {Per(X(a, "hdmg"), a.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
    static Formation S387Replace(Formation f, UnitDef to) => I.Swap(f, "hagi", to);

    // ---------------------------------------------------------------------------------
    // §3-1 の 3 ／ 4: 台 × 列 × 8 物差し
    // ---------------------------------------------------------------------------------
    internal sealed record Board(string Name, Formation F, int Slot, string Who);

    internal static Board[] AtkBoards()
    {
        var v = Presets.Versions; var pt = Presets.Playtest;
        Formation V(string n) => v.First(r => r.Name == n).F;
        Formation P(string n) => pt.First(r => r.Name == n).F;
        return new[]
        {
            new Board("試遊・燃焼 ポンの台", V("試遊・燃焼 ポンの台"), Slot(V("試遊・燃焼 ポンの台"), "hota"), "ホタ"),
            new Board("試遊・標 守り型", P("試遊・標 守り型"), Slot(P("試遊・標 守り型"), "tome"), "ミサ"),
            new Board("試遊・感電 ポンの台", V("試遊・感電 ポンの台"), Slot(V("試遊・感電 ポンの台"), "shiga"), "シガ"),
            new Board("試遊・毒", V("試遊・毒"), Slot(V("試遊・毒"), "vio"), "ヴィオ"),
            new Board("試遊・死 探索1位", V("試遊・死 探索1位"), Slot(V("試遊・死 探索1位"), "borg"), "ボルグ"),
        };
    }
    internal static Board[] RicaBoards()
    {
        var v = Presets.Versions;
        Formation V(string n) => v.First(r => r.Name == n).F;
        return new[]
        {
            new Board("試遊・死の連鎖", V("試遊・死の連鎖"), Slot(V("試遊・死の連鎖"), "zoto"), "ゾト"),
            new Board("試遊・死 エレ", V("試遊・死 エレ"), Slot(V("試遊・死 エレ"), "sid"), "スィド"),
        };
    }
    static int Slot(Formation f, string id) => f.Occupied().First(o => o.Def.Id == id).Slot;
    static Formation ColBoard(Board b, UnitDef? d) => d is null ? b.F : Put(b.F, b.Slot, d);

    static void Boards(Board[] boards, string title)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var g = new S377.B[boards.Length, Cols.Length][];
        for (int bi = 0; bi < boards.Length; bi++) for (int ci = 0; ci < Cols.Length; ci++) g[bi, ci] = M8(ColBoard(boards[bi], Cols[ci].D));
        Console.WriteLine($"# {Tag} {title} —— 台 {boards.Length} × 列 {Cols.Length}（{string.Join(" ／ ", Cols.Select(c => c.Name))}）× 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}・{SeatName(b.Slot)} の {b.Who} を替える）")) + "。");
        Console.WriteLine();
        Report(boards.Select(b => (b.Name, b.Who)).ToArray(), g);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>台 × 列の表（§3-3 の 1〜6 と 8）。<c>g[bi, ci]</c> は 8 物差し。</summary>
    static void Report((string Name, string Who)[] boards, S377.B[,][] g)
    {
        int nb = boards.Length, nc = Cols.Length;
        Console.WriteLine("## 1. 勝率・決着T（勝った戦）・倒れた編成の駒（1戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波の平均 | 元との差 |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {Cols[ci].Name} | {string.Join(" | ", g[bi, ci].Select(x => x.Cell))} | {F1(A4(g[bi, ci]))} | {(ci == 0 ? "—" : Sg(D4(g[bi, ci], g[bi, 0])))} |");
        Console.WriteLine();

        Console.WriteLine("## 1'. ボスの倒しT ／ 隊の1ターンの最大の与ダメ（1戦の平均・ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強）");
        Console.WriteLine();
        int[] ws5 = { 0, 1, 2, 5, 7 };
        Console.WriteLine("| 台 | 列 | ボスの倒しT | " + string.Join(" | ", ws5.Select(w => W8[w].Name)) + " |");
        Console.WriteLine("|---|---|--:|" + string.Concat(ws5.Select(_ => "--:|")));
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {Cols[ci].Name} | {g[bi, ci][WBoss].WinTs} | {string.Join(" | ", ws5.Select(w => F0((double)X(g[bi, ci][w], "maxTurnDmg") / Math.Max(1, g[bi, ci][w].N))))} |");
        Console.WriteLine();

        Console.WriteLine("## 2. ハギの働き（1戦・ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強）——斬撃・刈り取り（回 ／ HP）・再行動・追撃（回 ／ 与ダメ）・自分で割った（追撃しない）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 斬撃 | 刈り取り | 刈り取った HP | 再行動 | 追撃 | 追撃の与ダメ | 自分で割った | ハギの撃破 | ハギの与ダメ | 今のハギの追い打ち |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 1; ci < nc - 1; ci++)
                foreach (int w in ws5)
                {
                    var x = g[bi, ci][w];
                    Console.WriteLine($"| {(ci == 1 && w == 0 ? boards[bi].Name : "")} | {(w == 0 ? Cols[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "strikes"), x.N)} | {Per(X(x, "reaps"), x.N)} | {Per(X(x, "reapHp"), x.N)} | {Per(X(x, "encores"), x.N)} | {Per(X(x, "pursuits"), x.N)} | {Per(X(x, "pursuitDmg"), x.N)} | {Per(X(x, "pursuitSelf"), x.N)} | {Per(X(x, "kills"), x.N)} | {F0((double)X(x, "hdmg") / Math.Max(1, x.N))} | {Per(X(x, "pursueOld"), x.N)} |");
                }
        Console.WriteLine();
        Console.WriteLine("### 2'. ボスで勇者を刈り取った戦（勝った戦のうち・刈り取りが勇者の止めの一撃だった戦の割合 ／ そのTの平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Cols.Skip(2).Take(4).Select(c => c.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Cols.Skip(2).Take(4).Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(2, 4).Select(ci => { var x = g[bi, ci][WBoss]; return $"{Pc(X(x, "heroReaped"), x.Wins)}%（T{Per(X(x, "heroReapedT"), X(x, "heroReaped"))}）"; }))} |");
        Console.WriteLine();

        Console.WriteLine("## 3. 魂（H3 ／ H3s）——ターンの頭の魂（生きていた戦の平均・T1〜T8）・3 ／ 6 ／ 9 に届いた戦 %（T）・出どころ（敵の死 ／ 割った）・最大の攻撃力・魂 20 超の戦");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | T1〜T8 | 3 | 6 | 9 | 敵の死から ／ 割ったから | 魂の最大 | 最大の攻撃力 | 20 超 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|--:|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 4; ci <= 5; ci++)
                foreach (int w in new[] { 0, 1, 2, 5, 7 })
                {
                    var x = g[bi, ci][w];
                    string tl = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"halive{t}") == 0 ? "—" : F1((double)X(x, $"soul{t}") / X(x, $"halive{t}"))));
                    string St(string k) => $"{Pc(X(x, k), x.N)}（T{Per(X(x, k + "T"), X(x, k))}）";
                    Console.WriteLine($"| {(ci == 4 && w == 0 ? boards[bi].Name : "")} | {(w == 0 ? Cols[ci].Name : "")} | {W8[w].Name} | {tl} | {St("s3")} | {St("s6")} | {St("s9")} | {Per(X(x, "soulDeath"), x.N)} ／ {Per(X(x, "soulCross"), x.N)} | {Per(X(x, "soulPeak"), x.N)} | {Per(X(x, "atkPeak"), x.N)} | {Pc(X(x, "soul20"), x.N)}% |");
                }
        Console.WriteLine();

        Console.WriteLine("## 4. 巻き込み（1戦・ボス ／ 近衛 ／ 本編 第5波）——隣の味方へ与えた傷（実際に減った HP）・受けた駒と、それで倒れた回数");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 与えた傷 | 回数 | 受けた駒（減った HP ／ 倒れた回数・1戦）|");
        Console.WriteLine("|---|---|---|--:|--:|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 2; ci < nc - 1; ci++)
                foreach (int w in new[] { 0, 1, 5 })
                {
                    var x = g[bi, ci][w];
                    var who = x.X.Keys.Where(k => k.StartsWith("spillTo:", StringComparison.Ordinal)).Select(k => k[8..]).OrderByDescending(id => X(x, "spillTo:" + id))
                        .Select(id => $"{Nm(id)} {Per(X(x, "spillTo:" + id), x.N)} ／ {Per(X(x, "spillDeath:" + id), x.N)}");
                    Console.WriteLine($"| {(ci == 2 && w == 0 ? boards[bi].Name : "")} | {(w == 0 ? Cols[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "spill"), x.N)} | {Per(X(x, "spillHits"), x.N)} | {string.Join("・", who)} |");
                }
        Console.WriteLine();

        if (g[0, 0][0].RicaIn > 0 || Enumerable.Range(0, nb).Any(bi => g[bi, 0][0].RicaIn > 0))
        {
            Console.WriteLine("### 4'. リィカの層の最大（1戦・リィカのいる台・ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強）——巻き込みで倒れた味方が層の燃料になったか");
            Console.WriteLine();
            Console.WriteLine("| 台 | 列 | " + string.Join(" | ", ws5.Select(w => W8[w].Name)) + " |");
            Console.WriteLine("|---|---|" + string.Concat(ws5.Select(_ => "--:|")));
            for (int bi = 0; bi < nb; bi++)
            {
                if (g[bi, 0][0].RicaIn == 0) continue;
                for (int ci = 0; ci < nc; ci++)
                    Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {Cols[ci].Name} | {string.Join(" | ", ws5.Select(w => Per(g[bi, ci][w].PeakSum, g[bi, ci][w].N)))} |");
            }
            Console.WriteLine();
        }

        Console.WriteLine("## 5. 勇者の HP（ボス・最初の攻撃の直前 ＝ ターンの頭の回復の後・T1〜T8・生きていた戦の平均）と、5 割を割った回数（1戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | T1〜T8 | 5 割を割った |");
        Console.WriteLine("|---|---|---|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
            {
                var x = g[bi, ci][WBoss];
                Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {Cols[ci].Name} | {string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"hhn{t}") == 0 ? "—" : F0((double)X(x, $"hhp{t}") / X(x, $"hhn{t}"))))} | {Per(X(x, "heroCross"), x.N)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 6. 誰が何Tに倒れたか（元の台 ／ 4 波の平均がいちばん良い版・ボス ／ 近衛）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
        {
            int best = Enumerable.Range(2, 4).OrderByDescending(ci => A4(g[bi, ci])).ThenByDescending(ci => g[bi, ci][WStrong].Win).First();
            Console.WriteLine($"- **{boards[bi].Name}**（最良の版 {Cols[best].Name}・4 波 {F1(A4(g[bi, best]))} ／ 元 {F1(A4(g[bi, 0]))}）");
            Console.WriteLine($"  - ボス: 元 {F1(g[bi, 0][WBoss].Win)}%・{g[bi, 0][WBoss].WinTs}T — {g[bi, 0][WBoss].FellCell}");
            Console.WriteLine($"  - ボス: {Cols[best].Name} {F1(g[bi, best][WBoss].Win)}%・{g[bi, best][WBoss].WinTs}T — {g[bi, best][WBoss].FellCell}");
            Console.WriteLine($"  - 近衛: 元 {F1(g[bi, 0][WGuard].Win)}%・{g[bi, 0][WGuard].WinTs}T — {g[bi, 0][WGuard].FellCell}");
            Console.WriteLine($"  - 近衛: {Cols[best].Name} {F1(g[bi, best][WGuard].Win)}%・{g[bi, best][WGuard].WinTs}T — {g[bi, best][WGuard].FellCell}");
        }
        Console.WriteLine();

        Console.WriteLine("## 7. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上 ／ 魂 20 超 ／ 強い条件で追撃の1回が 1,000 超 ／ あふれ）");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 1; ci < nc; ci++)
            {
                string at = $"{boards[bi].Name} × {Cols[ci].Name}";
                foreach (int w in new[] { WGuard, WBat }) { var x = g[bi, ci][w]; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{at} × {W8[w].Name}: 1T 決着 {Pc(x.Win1T, x.N)}%"); }
                { var x = g[bi, ci][WBoss]; if (x.BossFast > 0) signs.Add($"{at} × ボス: 4T 未満の勝ち {Pc(x.BossFast, x.N)}%"); }
                for (int w = 0; w < W8.Length; w++)
                {
                    var x = g[bi, ci][w]; var x0 = g[bi, 0][w];
                    if (100.0 * x.Cap / x.N - 100.0 * x0.Cap / x0.N >= 5) signs.Add($"{at} × {W8[w].Name}: 30T の打ち切り {Pc(x0.Cap, x0.N)} → {Pc(x.Cap, x.N)}%");
                    if (x.Overflow > 0) signs.Add($"{at} × {W8[w].Name}: あふれの兆し {x.Overflow} 件");
                    if (X(x, "soul20") > 0) signs.Add($"{at} × {W8[w].Name}: 魂 20 超 {Pc(X(x, "soul20"), x.N)}%");
                }
                { var x = g[bi, ci][WStrong]; if (X(x, "p1000") > 0) signs.Add($"{at} × 強: 追撃の1回が 1,000 超 {Pc(X(x, "p1000"), x.N)}%"); }
            }
        if (signs.Count == 0) Console.WriteLine("- 無し。");
        foreach (var s in signs) Console.WriteLine("- " + s);
    }

    /// <summary>1戦のログ（`sh388 log <台の名前の頭> <列> <物差しの番号 0..7> <seed>`・強い条件は第2波）。台は §3-1 の 3 ／ 4 の台。</summary>
    static void Log1(string[] args)
    {
        string bn = args.Length > 3 ? args[3] : "試遊・燃焼";
        string cn = args.Length > 4 ? args[4] : "H3";
        int wi = args.Length > 5 && int.TryParse(args[5], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 6 && int.TryParse(args[6], out int s0) ? s0 : 0;
        var all = AtkBoards().Concat(RicaBoards()).ToArray();
        var b = all.FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal)) ?? all[0];
        var c = Cols.FirstOrDefault(x => x.Name == cn) is { Name: not null } cc ? cc : Cols[4];
        var (r, _, _) = I.Raw(ColBoard(b, c.D), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name} × {c.Name} × {W8[wi].Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    static string Nm(string id) => UnitCatalog.Everyone.FirstOrDefault(d => d.Id == id) is { } d ? Short(d) : id switch
    {
        "spore" => "胞子", "wight" => "亡者", _ => id,
    };

    // ---------------------------------------------------------------------------------
    // §3-1 の 2: 試遊の台に入れて（第387期 §4-3 と同じやり方）
    // ---------------------------------------------------------------------------------
    static void Swap()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = S387.Play();
        int nb = boards.Length;
        var s1W = new[] { I.Boss, I.Guard, I.Bat, I.M5 };
        // 段1（seed 0..9・4 波 ＋ 強い条件）
        var bs = new double[nb][];
        var grid = new double[Vers.Length, nb, 5][];
        var jobs = new List<(int V, int B, int S)>();
        for (int b = 0; b < nb; b++) jobs.Add((-1, b, -1));
        for (int v = 0; v < Vers.Length; v++) for (int b = 0; b < nb; b++) if (!Has(boards[b].F, "hagi")) foreach (int s in Seats5(boards[b].F)) jobs.Add((v, b, s));
        Parallel.ForEach(jobs, j =>
        {
            var f = j.V < 0 ? boards[j.B].F : Put(boards[j.B].F, j.S, Vers[j.V].D);
            var r = new double[5];
            for (int w = 0; w < 4; w++) r[w] = WinRate(f, new[] { s1W[w] }, S1Seeds);
            r[4] = WinRate(f, I.StrongWaves, S1Seeds);
            if (j.V < 0) bs[j.B] = r; else grid[j.V, j.B, j.S] = r;
        });
        Console.WriteLine($"# {Tag} §3-1 の 2 試遊の台に入れて —— 試遊の台 {nb} × 1枚をハギ（版）に替えた台（第387期 §4-3 と同じやり方）");
        Console.WriteLine();
        Console.WriteLine($"段1: seed 0..{S1Seeds - 1} × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波（＋ 強い条件 第2〜5波は並べ替えの2番目の鍵）。段2: 版ごとに上位 3 台を seed 0..{Seeds - 1} × 8 物差し（同じ席に素体 ／ 今のハギも）。");
        Console.WriteLine();
        Console.WriteLine("## 1. 段1 の要約（版ごと・替えた台 ／ 元の台より +3pt 以上上がった台の数・目安）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 替えた台 | 4 波で上がった | 強い条件で上がった | 4 波の差の平均 | 強い条件の差の平均 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        var top = new List<(int B, int S, double D4, double DS)>[Vers.Length];
        for (int v = 0; v < Vers.Length; v++)
        {
            var all = new List<(int B, int S, double D4, double DS)>();
            for (int b = 0; b < nb; b++) for (int s = 0; s < 5; s++) if (grid[v, b, s] is { } r) all.Add((b, s, Enumerable.Range(0, 4).Average(w => r[w] - bs[b][w]), r[4] - bs[b][4]));
            top[v] = all.OrderByDescending(x => x.D4).ThenByDescending(x => x.DS).ThenBy(x => x.B).ThenBy(x => x.S).Take(3).ToList();
            Console.WriteLine($"| {Vers[v].Name} | {all.Count} | {all.Count(x => x.D4 >= Up)} | {all.Count(x => x.DS >= Up)} | {Sg(all.Average(x => x.D4))} | {Sg(all.Average(x => x.DS))} |");
        }
        Console.WriteLine();

        // 段2
        var need = top.SelectMany(l => l.Select(x => x.B)).Distinct().ToArray();
        var baseM = need.ToDictionary(b => b, b => M8(boards[b].F));
        Console.WriteLine($"## 2. 段2 —— 版ごとの最良 3 台（seed 0..{Seeds - 1}・差 ＝ 替えた台 − 元の台）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 順 | 台 | 替えた席（誰を）| " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波の差 | 素体の差（4 波 ／ 強）| 今のハギの差（4 波 ／ 強）| 刈り取り ／ 追撃 ／ 魂の最大（ボス ／ 近衛）|");
        Console.WriteLine("|---|--:|---|---|" + string.Concat(W8.Select(_ => "--:|")) + "--:|---|---|---|");
        var bestOf = new (int B, int S, S377.B[] M)[Vers.Length];
        for (int v = 0; v < Vers.Length; v++)
            for (int k = 0; k < top[v].Count; k++)
            {
                var x = top[v][k]; var b0 = baseM[x.B];
                var a = M8(Put(boards[x.B].F, x.S, Vers[v].D));
                var pl = M8(Put(boards[x.B].F, x.S, PlainHagi));
                var cur = M8(Put(boards[x.B].F, x.S, UnitCatalog.HagiH389));   // 第390期: 旧に固定
                if (k == 0) bestOf[v] = (x.B, x.S, a);
                string W(S377.B y) => $"{Per(X(y, "reaps"), y.N)} ／ {Per(X(y, "pursuits"), y.N)} ／ {Per(X(y, "soulPeak"), y.N)}";
                Console.WriteLine($"| {(k == 0 ? Vers[v].Name : "")} | {k + 1} | {boards[x.B].Name} | {SeatName(x.S)}（{Short(boards[x.B].F[x.S]!)}）| {string.Join(" | ", Enumerable.Range(0, W8.Length).Select(wi => $"{F1(a[wi].Win)}（{Sg(a[wi].Win - b0[wi].Win)}）"))} | {Sg(D4(a, b0))} | {Sg(D4(pl, b0))} ／ {Sg(pl[WStrong].Win - b0[WStrong].Win)} | {Sg(D4(cur, b0))} ／ {Sg(cur[WStrong].Win - b0[WStrong].Win)} | ボス {W(a[0])}・近衛 {W(a[1])} |");
            }
        Console.WriteLine();
        Console.WriteLine("### 2'. 元の台（段2 で使った台・seed 0..99・勝率・決着T）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", W8.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(W8.Select(_ => "---|")));
        foreach (int b in need) Console.WriteLine($"| {boards[b].Name} | {string.Join(" | ", baseM[b].Select(x => $"{F1(x.Win)}・{x.WinTs}T"))} |");
        Console.WriteLine();
        Console.WriteLine("## 3. 誰が何Tに倒れたか（版ごとの最良の台・ボス ／ 近衛・元の台 → 替えた台）");
        Console.WriteLine();
        for (int v = 0; v < Vers.Length; v++)
        {
            var (b, s, a) = bestOf[v]; var b0 = baseM[b];
            Console.WriteLine($"- **{Vers[v].Name}**（{boards[b].Name} の {SeatName(s)} の {Short(boards[b].F[s]!)} を替えた）");
            Console.WriteLine($"  - ボス: 元 {F1(b0[0].Win)}%・{b0[0].WinTs}T — {b0[0].FellCell}");
            Console.WriteLine($"  - ボス: 替 {F1(a[0].Win)}%・{a[0].WinTs}T — {a[0].FellCell}");
            Console.WriteLine($"  - 近衛: 元 {F1(b0[1].Win)}%・{b0[1].WinTs}T — {b0[1].FellCell}");
            Console.WriteLine($"  - 近衛: 替 {F1(a[1].Win)}%・{a[1].WinTs}T — {a[1].FellCell}");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
