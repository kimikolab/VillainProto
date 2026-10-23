using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S388 = Sh388Diag;
using S390 = Sh390Diag;

// =====================================================================================
// sh391 —— 第391期【軽い期】本体: ハギ——追撃でも刈り取る（版 P1 ／ P1w ／ P1h）。規定は変えない。
// 指示書は design/PHASE391_HAGI_CHASE_REAP_SPEC.md、報告は design/PHASE391_HAGI_CHASE_REAP.md。
//
//     dotnet run --project BattleSim -c Release 0 sh391 cmp       # §3-3 の 8: ハギのいる `compare` の行 × 列（規定 ／ P1 ／ P1w ／ P1h・第1〜5波・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh391 swap      # §3-1 の 3: 試遊の台 × 1枚をハギ（版）に替えた台（段1 seed 0..9 → 台ごとのアタッカー以外の席の最良 ／ 版ごとの上位 3 台・seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh391 boards    # §3-1 の 1: 試遊・ハギ 台B ／ 試遊・死神と墓守 ／ 試遊・死 エレのスィドの席 × 列（元 ／ 素体 ／ 規定 ／ P1 ／ P1w ／ P1h）× 8 物差し
//     dotnet run --project BattleSim -c Release 0 sh391 move      # §3-1 の 2: 版・移動 ハギの台 × 列 ／ 移動軸の 3 行 × 5 席を版ごとに段1（seed 0..19）→ 行 × 版ごとの最良の台 × 8 物差し
//     dotnet run --project BattleSim -c Release 0 sh391 log <台> <列> <物差し 0..7> <seed>   # 1戦のログ（台 ＝ 名前の頭）
//     dotnet run --project BattleSim -c Release 0 sh391 check     # 自己検査
//
// **規定の駒は1つも変えない。** 替えた台 ／ 素体は器具の中だけ（試遊の台に足す「版・P1 ハギ」を除く）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p391/predict.md`）。
// =====================================================================================
static partial class Sh391Diag
{
    internal const string PredictFile = ".tmp/p391/predict.md";
    const string Tag = "第391期";
    const int Seeds = 100, CompareSeeds = 200, S1Seeds = 10, MoveS1Seeds = 20, TT = 9;
    const double Up = 3.0;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "cmp" or "swap" or "boards" or "move" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh391: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "cmp": Cmp(); return;
            case "swap": Swap(); return;
            case "boards": Boards(S390.MainBoards(), BoardCols, "§3-1 の 1 試遊・ハギ 台B ／ 試遊・死神と墓守 ／ 試遊・死 エレのスィドの席"); return;
            case "move": Move(); return;
            case "log": Log1(args); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh391: モードは cmp / swap / boards / move / log / check。"); return;
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
        { ("規定", UnitCatalog.HagiH391), ("P1", UnitCatalog.HagiP1), ("P1w", UnitCatalog.HagiP1w), ("P1h", UnitCatalog.HagiP1h) };
    /// <summary>列: 元の台（ハギを入れる前）／ 素体 ／ 規定 ／ P1 ／ P1w ／ P1h。素体は第388期と同じ（器具の中だけ）。</summary>
    internal static readonly (string Name, UnitDef? D)[] BoardCols =
        { ("元", null), ("素体", S388.PlainHagi), ("規定", UnitCatalog.HagiH391), ("P1", UnitCatalog.HagiP1), ("P1w", UnitCatalog.HagiP1w), ("P1h", UnitCatalog.HagiP1h) };

    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;   // ボス ／ 近衛 ／ 大隊 ／ 本編 第2 ／ 4 ／ 5波 ／ 九体 ／ 強 第2〜5波
    static readonly int[] W4 = { 0, 1, 2, 5 };
    static readonly int[] W5 = { 0, 1, 2, 5, 7 };
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

    /// <summary>ハギの帳簿（手番の刈り取り ／ 追撃の刈り取り）／ 隊の1ターンの最大の与ダメ ／ 勇者を刈り取った戦（手番 ／ 追撃）／ 止めの内訳 ／ 勇者へのダメージ ／ 移動軸（シオ）。</summary>
    static Action<BattleResult, List<UnitState>, List<UnitState>, S377.B> Extra(bool boss) => (r, p, e, b) =>
    {
        var form = p.ToDictionary(u => u.InstanceId);
        var foe = e.ToDictionary(u => u.InstanceId);
        var sumTeam = new Dictionary<int, int>();
        var hagi = p.FirstOrDefault(u => u.Def.Id == "hagi");
        int hid = hagi?.InstanceId ?? -9;
        int rid = p.FirstOrDefault(u => u.Def.Id == "rica")?.InstanceId ?? -9;
        int sid = p.FirstOrDefault(u => u.Def.Id == "shio")?.InstanceId ?? -9;
        var dmgT = new long[BattleEngine.MaxTurns + 2];
        bool hAlive = hagi is not null;
        UnitState? hero = boss && e.Count > 0 ? e[0] : null;
        bool heroLastReaction = false;
        bool Foe(int id) => foe.ContainsKey(id) || (sumTeam.TryGetValue(id, out int st) && st == BattleContext.EnemyTeam);
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
                case BattleEventKind.Damage:
                {
                    bool toFoe = Foe(tgt);
                    bool byUs = form.ContainsKey(act) || (sumTeam.TryGetValue(act, out int sa) && sa == BattleContext.PlayerTeam);
                    if (toFoe && byUs) dmgT[t] += x.Amount;
                    if (hero is not null && tgt == hero.InstanceId)
                    {
                        heroLastReaction = x.Reaction;
                        Add(b, act == rid ? "hdRica" : act == hid ? "hdHagi" : "hdOther", x.Amount);
                    }
                    break;
                }
                case BattleEventKind.Revive:
                    if (tgt == hid) hAlive = true;
                    break;
                case BattleEventKind.Move:
                    if (tgt == hid && act == sid) Add(b, "shioMove", 1);
                    break;
                case BattleEventKind.Whet:
                    if (tgt == hid && x.WhetRoute == WhetRoute.Drifter) Add(b, "shioAtk", x.Amount);
                    break;
                case BattleEventKind.Death:
                    if (tgt == hid) hAlive = false;
                    if (Foe(tgt)) { Add(b, "kAll", 1); Add(b, act == rid ? "kRica" : act == hid ? "kHagi" : "kOther", 1); }
                    if (hero is not null && tgt == hero.InstanceId)
                    {
                        Add(b, act == rid ? "heroKRica" : act == hid ? "heroKHagi" : "heroKOther", 1);
                        bool reaped = r.Log.Any(l => l.Text.Contains($"の大鎌が {hero.Name} を刈り取った", StringComparison.Ordinal));
                        if (reaped && act == hid && r.PlayerWon)
                        {
                            Add(b, "heroReaped", 1); Add(b, "heroReapedT", x.Turn); Add(b, heroLastReaction ? "heroReapedChase" : "heroReapedTurn", 1);
                            if (x.Turn <= 3) Add(b, "heroReapedT3", 1);
                            if (r.TallyByUnit.TryGetValue("hagi", out var ht)) { Add(b, "heroLine", ht.R389LastReapLine); Add(b, "heroReapHp", ht.R389LastReapHp); }
                        }
                    }
                    break;
            }
        }
        Add(b, "maxTurnDmg", dmgT.Max());
        if (r.TallyByUnit.TryGetValue("hagi", out var h) && hagi is not null)
        {
            Add(b, "strikes", h.R388Strikes); Add(b, "sweeps", h.R389Sweeps); Add(b, "reaps", h.R388Reaps); Add(b, "reapHp", h.R388ReapHp + h.R389ColumnHp); Add(b, "encores", h.R388Encores);
            Add(b, "colReaps", h.R389ColumnReaps); Add(b, "colUnits", h.R389ColumnUnits);
            Add(b, "pursuits", h.R388Pursuits); Add(b, "pursuitDmg", h.R388PursuitDmg);
            Add(b, "chase", h.R391ChaseReaps); Add(b, "chaseHp", h.R391ChaseReapHp + h.R391ChaseColumnHp); Add(b, "chase2", h.R391ChaseReapSecond); Add(b, "chaseKills", h.R391ChaseReapKills);
            Add(b, "chaseCol", h.R391ChaseColumn); Add(b, "chaseColUnits", h.R391ChaseColumnUnits);
            Add(b, "reapKills", h.R390ReapKills); Add(b, "kills", h.Kills); Add(b, "hdmg", h.DamageToEnemy);
            Add(b, "soulPeak", h.R389SoulPeak);
            if (h.R389Soul6Turn > 0) { Add(b, "s6", 1); Add(b, "s6T", h.R389Soul6Turn); }
            if (h.R389Soul9Turn > 0) { Add(b, "s9", 1); Add(b, "s9T", h.R389Soul9Turn); }
            if (h.R389SoulByTurn is { } sb) for (int t = 1; t < TT && t < sb.Length; t++) Add(b, $"soul{t}", sb[t]);
            Add(b, "marks", h.R389MarksPlaced);
            Add(b, "hatkEnd", hagi.CurrentAttack);
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
    static Formation ColBoard(S388.Board b, UnitDef? d) => d is null ? b.F : Put(b.F, b.Slot, d);

    // ---------------------------------------------------------------------------------
    // §3-3 の 8: `compare` のハギの行
    // ---------------------------------------------------------------------------------
    static void Cmp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().Where(r => Has(r.F, "hagi")).ToArray();
        int nw = EnemyCatalog.Stages.Count;
        var grids = Vers.Select(c => CGrid(rows.Select(r => (r.Name, I.Swap(r.F, "hagi", c.D))).ToArray(), CompareSeeds)).ToArray();
        Console.WriteLine($"# {Tag} §3-3 の 8 `compare` のハギの行 —— {rows.Length} 行 × 列（第1〜5波・seed 0..{CompareSeeds - 1}・ハギだけ替える）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 席 | " + string.Join(" | ", Vers.Select(c => c.Name + " 第1〜5波")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Vers.Select(_ => "---|")));
        for (int ri = 0; ri < rows.Length; ri++)
        {
            string seat = string.Join("・", rows[ri].F.Occupied().Where(o => o.Def.Id == "hagi").Select(o => SeatName(o.Slot)));
            Console.WriteLine($"| {rows[ri].Name} | {seat} | {string.Join(" | ", grids.Select(g => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w])))))} |");
        }
        Console.WriteLine();
        Console.WriteLine("| 列 | 第2〜5波の平均 | 規定との差 | 動いたセル（規定と違う・第2〜5波）| −10pt 以上落ちたセル |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        double Avg(double[,] g) => Enumerable.Range(0, rows.Length).Average(ri => Enumerable.Range(1, nw - 1).Average(w => g[ri, w]));
        for (int c = 0; c < Vers.Length; c++)
        {
            int moved = 0, drop = 0;
            for (int ri = 0; ri < rows.Length; ri++) for (int w = 1; w < nw; w++) { if (grids[c][ri, w] != grids[0][ri, w]) moved++; if (grids[c][ri, w] - grids[0][ri, w] <= -10) drop++; }
            Console.WriteLine($"| {Vers[c].Name} | {F1(Avg(grids[c]))} | {Sg(Avg(grids[c]) - Avg(grids[0]))} | {moved} | {drop} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-1 の 3: 試遊の台に入れて（アタッカー以外の席を含む）
    // ---------------------------------------------------------------------------------
    sealed record S1(int V, int B, int S, double D4, double DS);
    static (double[][] Base, List<S1>[] All, int[] Atk) Stage1((string Name, Formation F)[] boards)
    {
        int nb = boards.Length;
        var s1W = new[] { I.Boss, I.Guard, I.Bat, I.M5 };
        var bs = new double[nb][];
        var grid = new double[Vers.Length, nb, 5][];
        var atk = new int[nb];
        var jobs = new List<(int V, int B, int S)>();
        for (int b = 0; b < nb; b++) jobs.Add((-1, b, -1));
        for (int v = 0; v < Vers.Length; v++) for (int b = 0; b < nb; b++) if (!Has(boards[b].F, "hagi")) foreach (int s in Seats5(boards[b].F)) jobs.Add((v, b, s));
        Parallel.ForEach(jobs, j =>
        {
            var f = j.V < 0 ? boards[j.B].F : Put(boards[j.B].F, j.S, Vers[j.V].D);
            var r = new double[5];
            for (int w = 0; w < 4; w++) r[w] = WinRate(f, new[] { s1W[w] }, S1Seeds);
            r[4] = WinRate(f, I.StrongWaves, S1Seeds);
            if (j.V < 0) { bs[j.B] = r; atk[j.B] = Sh389Diag.AttackerSeat(f); } else grid[j.V, j.B, j.S] = r;
        });
        var all = new List<S1>[Vers.Length];
        for (int v = 0; v < Vers.Length; v++)
        {
            all[v] = new List<S1>();
            for (int b = 0; b < nb; b++) for (int s = 0; s < 5; s++) if (grid[v, b, s] is { } r) all[v].Add(new S1(v, b, s, Enumerable.Range(0, 4).Average(w => r[w] - bs[b][w]), r[4] - bs[b][4]));
            all[v] = all[v].OrderByDescending(x => x.D4).ThenByDescending(x => x.DS).ThenBy(x => x.B).ThenBy(x => x.S).ToList();
        }
        return (bs, all, atk);
    }

    static void Swap()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = S390.Play();
        int nb = boards.Length;
        var (bs, all, atk) = Stage1(boards);
        Console.WriteLine($"# {Tag} §3-1 の 3 試遊の台に入れて —— 試遊の台 {nb}（うちハギのいない台 {boards.Count(b => !Has(b.F, "hagi"))}）× 1枚をハギ（版）に替えた台（第389 ／ 390期と同じやり方）");
        Console.WriteLine();
        Console.WriteLine($"段1: seed 0..{S1Seeds - 1} × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波（＋ 強い条件 第2〜5波は並べ替えの2番目の鍵）。段2: seed 0..{Seeds - 1} × 8 物差し・同じ席に素体。アタッカーは第389期と同じ決め方。");
        Console.WriteLine();
        Console.WriteLine("## 1. 段1 の要約（版ごと・替えた台 ／ 元の台より +3pt 以上上がった台の数・目安）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 替えた台 | 4 波で上がった | 強い条件で上がった | 4 波の差の平均 | アタッカー以外の席: 替えた台 ／ 4 波で上がった ／ 差の平均 |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        for (int v = 0; v < Vers.Length; v++)
        {
            var na = all[v].Where(x => x.S != atk[x.B]).ToList();
            Console.WriteLine($"| {Vers[v].Name} | {all[v].Count} | {all[v].Count(x => x.D4 >= Up)} | {all[v].Count(x => x.DS >= Up)} | {Sg(all[v].Average(x => x.D4))} | {na.Count} ／ {na.Count(x => x.D4 >= Up)} ／ {Sg(na.Average(x => x.D4))} |");
        }
        Console.WriteLine();

        var cache = new Dictionary<string, S377.B[]>();
        S377.B[] M(int b, int s, UnitDef? d) { string k = $"{b}:{s}:{d?.Id}:{d?.GetHashCode()}"; lock (cache) if (cache.TryGetValue(k, out var x)) return x; var y = M8(d is null ? boards[b].F : Put(boards[b].F, s, d)); lock (cache) cache[k] = y; return y; }
        Console.WriteLine($"## 2. 台ごとのアタッカー以外の席の最良（版ごと・段1 の 4 波の差 → 強い条件の差で選んだ席・seed 0..{Seeds - 1}・差 ＝ 替えた台 − 元の台）");
        Console.WriteLine();
        Console.WriteLine("| 台（アタッカー）| 元 4 波 ／ 強 ／ ボスの倒しT | " + string.Join(" | ", Vers.Select(v => $"{v.Name}: 席（誰を）4 波の差 ／ 強の差 ／ ボスT")) + " | 同じ席の素体（規定の席）|");
        Console.WriteLine("|---|---|" + string.Concat(Vers.Select(_ => "---|")) + "---|");
        var bestBy = Vers.ToDictionary(v => v.Name, _ => new List<(int B, int S, double D4, double DS)>());
        for (int b = 0; b < nb; b++)
        {
            if (Has(boards[b].F, "hagi")) continue;
            var b0 = M(b, -1, null);
            var cells = new List<string>(); int seat0 = -1;
            for (int v = 0; v < Vers.Length; v++)
            {
                var pick = all[v].FirstOrDefault(x => x.B == b && x.S != atk[b]);
                if (pick is null) { cells.Add("—"); continue; }
                var a = M(b, pick.S, Vers[v].D);
                double d4 = D4(a, b0), ds = a[WStrong].Win - b0[WStrong].Win;
                if (v == 0) seat0 = pick.S;
                bestBy[Vers[v].Name].Add((b, pick.S, d4, ds));
                cells.Add($"{SeatName(pick.S)}（{Short(boards[b].F[pick.S]!)}）{Sg(d4)} ／ {Sg(ds)} ／ {a[WBoss].WinTs}T");
            }
            string pl = "—";
            if (seat0 >= 0) { var p = M(b, seat0, S388.PlainHagi); pl = $"{Sg(D4(p, b0))} ／ {Sg(p[WStrong].Win - b0[WStrong].Win)} ／ {p[WBoss].WinTs}T"; }
            Console.WriteLine($"| {boards[b].Name}（{SeatName(atk[b])} {Short(boards[b].F[atk[b]]!)}）| {F1(A4(b0))} ／ {F1(b0[WStrong].Win)} ／ {b0[WBoss].WinTs}T | {string.Join(" | ", cells)} | {pl} |");
        }
        Console.WriteLine();
        foreach (var (vn, lst) in bestBy)
        {
            var br = lst.OrderByDescending(x => x.D4).ThenByDescending(x => x.DS).ThenBy(x => x.B).First();
            Console.WriteLine($"- **{vn} のアタッカー以外の席の最良**: {boards[br.B].Name} の {SeatName(br.S)}（{Short(boards[br.B].F[br.S]!)} を替える）・4 波 {Sg(br.D4)} ／ 強 {Sg(br.DS)}。4 波で +3pt 以上上がった台 {lst.Count(x => x.D4 >= Up)} ／ {lst.Count}・強い条件で +3pt 以上 {lst.Count(x => x.DS >= Up)}・4 波の差の平均 {Sg(lst.Average(x => x.D4))}・強の差の平均 {Sg(lst.Average(x => x.DS))}。");
        }
        Console.WriteLine();

        Console.WriteLine($"## 3. 版ごとの上位 3 台（席を問わず・seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 順 | 台 | 替えた席（誰を）| " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波の差 | 素体の差（4 波 ／ 強）| アタッカーの席か |");
        Console.WriteLine("|---|--:|---|---|" + string.Concat(W8.Select(_ => "--:|")) + "--:|---|---|");
        for (int v = 0; v < Vers.Length; v++)
            for (int k = 0; k < Math.Min(3, all[v].Count); k++)
            {
                var x = all[v][k]; var b0 = M(x.B, -1, null); var a = M(x.B, x.S, Vers[v].D); var pl = M(x.B, x.S, S388.PlainHagi);
                Console.WriteLine($"| {(k == 0 ? Vers[v].Name : "")} | {k + 1} | {boards[x.B].Name} | {SeatName(x.S)}（{Short(boards[x.B].F[x.S]!)}）| {string.Join(" | ", Enumerable.Range(0, W8.Length).Select(wi => $"{F1(a[wi].Win)}（{Sg(a[wi].Win - b0[wi].Win)}）"))} | {Sg(D4(a, b0))} | {Sg(D4(pl, b0))} ／ {Sg(pl[WStrong].Win - b0[WStrong].Win)} | {(x.S == atk[x.B] ? "アタッカー" : "—")} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-1 の 2: 移動軸
    // ---------------------------------------------------------------------------------
    /// <summary>移動軸の 3 行 × 版ごとに 5 席を段1（seed 0..19 × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波・4 波の差 → 強い条件の差）で並べ、行 × 版ごとの最良の席。</summary>
    internal static (string Row, Formation F, double[] Base, (int Seat, double D4, double DS)[] Best)[] MoveStage1()
    {
        var src = CompareBuilds().Where(r => S390.MoveRows.Contains(r.Name)).OrderBy(r => Array.IndexOf(S390.MoveRows, r.Name)).ToArray();
        var s1W = new[] { I.Boss, I.Guard, I.Bat, I.M5 };
        var outp = new (string, Formation, double[], (int, double, double)[])[src.Length];
        for (int ri = 0; ri < src.Length; ri++)
        {
            var f0 = src[ri].F;
            var seats = Seats5(f0);
            var jobs = new List<(int V, int S)> { (-1, -1) };
            for (int v = 0; v < Vers.Length; v++) foreach (int s in seats) jobs.Add((v, s));
            var res = new double[jobs.Count][];
            Parallel.For(0, jobs.Count, k =>
            {
                var f = jobs[k].V < 0 ? f0 : Put(f0, jobs[k].S, Vers[jobs[k].V].D);
                var r = new double[5];
                for (int w = 0; w < 4; w++) r[w] = WinRate(f, new[] { s1W[w] }, MoveS1Seeds);
                r[4] = WinRate(f, I.StrongWaves, MoveS1Seeds);
                res[k] = r;
            });
            var best = new (int, double, double)[Vers.Length];
            for (int v = 0; v < Vers.Length; v++)
                best[v] = Enumerable.Range(1, jobs.Count - 1).Where(k => jobs[k].V == v)
                    .Select(k => (jobs[k].S, Enumerable.Range(0, 4).Average(w => res[k][w] - res[0][w]), res[k][4] - res[0][4]))
                    .OrderByDescending(x => x.Item2).ThenByDescending(x => x.Item3).ThenBy(x => x.Item1).First();
            outp[ri] = (src[ri].Name, f0, res[0], best);
        }
        return outp;
    }

    static void Move()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §3-1 の 2 移動軸 —— 版・移動 ハギの台 × 列 ／ 移動軸の 3 行 × 版ごとの最良の席（段1 seed 0..{MoveS1Seeds - 1}）× 8 物差し");
        Console.WriteLine();
        // 版・移動 ハギ（移動改のガルドの席）× 全列
        var fixedB = new S388.Board("版・移動 ハギ（移動改 前3）", CompareBuilds().First(r => r.Name == S390.MoveRows[0]).F, 1, "ガルド");
        var g = new S377.B[1, BoardCols.Length][];
        for (int ci = 0; ci < BoardCols.Length; ci++) g[0, ci] = M8(ColBoard(fixedB, BoardCols[ci].D));
        Console.WriteLine("## A. 版・移動 ハギの台（`compare` の「移動改」のガルドの席・元 ＝ 移動改）");
        Console.WriteLine();
        Report(new[] { fixedB.Name }, BoardCols, g, origin: 0);
        Console.WriteLine();
        MoveCounts(new[] { fixedB.Name }, BoardCols, g);

        // 行 × 版ごとの最良の席
        var st = MoveStage1();
        Console.WriteLine("## B. 移動軸の 3 行 × 版ごとの最良の席（段1 で選び直す・段2 seed 0..99・差 ＝ 替えた台 − 元の行）");
        Console.WriteLine();
        Console.WriteLine("| 行（元 4 波 ／ 強・段1）| 版 | 席（誰を）段1 の差 4 波 ／ 強 | 段2 4 波 ／ 強 ／ ボス ／ 近衛 ／ 大隊 ／ 第5波 | 元との差 4 波 ／ 強 | 同じ席の素体 4 波 ／ 強 | 追撃 ／ 追撃の刈り取り（近衛 ／ 大隊）|");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (var r in st)
        {
            var b0 = M8(r.F);
            for (int v = 0; v < Vers.Length; v++)
            {
                var (seat, d4s, dss) = r.Best[v];
                var a = M8(Put(r.F, seat, Vers[v].D)); var pl = M8(Put(r.F, seat, S388.PlainHagi));
                string pc(int w) => $"{Per(X(a[w], "pursuits"), a[w].N)} ／ {Per(X(a[w], "chase"), a[w].N)}";
                Console.WriteLine($"| {(v == 0 ? $"{r.Row}（{F1(r.Base.Take(4).Average())} ／ {F1(r.Base[4])}）" : "")} | {Vers[v].Name} | {SeatName(seat)}（{Short(r.F[seat]!)}）{Sg(d4s)} ／ {Sg(dss)} | {F1(A4(a))} ／ {F1(a[WStrong].Win)} ／ {F1(a[WBoss].Win)} ／ {F1(a[WGuard].Win)} ／ {F1(a[WBat].Win)} ／ {F1(a[5].Win)} | {Sg(D4(a, b0))} ／ {Sg(a[WStrong].Win - b0[WStrong].Win)} | {Sg(D4(pl, b0))} ／ {Sg(pl[WStrong].Win - b0[WStrong].Win)} | {pc(WGuard)}・{pc(WBat)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void MoveCounts(string[] names, (string Name, UnitDef? D)[] cols, S377.B[,][] g)
    {
        Console.WriteLine("### 移動軸の数え（1戦・ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | シオがハギを動かした | シオで上がった攻撃力 | 終わりの攻撃力 | 追撃 | 追撃の刈り取り（回 ／ HP）| 手番の刈り取り（回 ／ HP）|");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|---|---|");
        for (int bi = 0; bi < names.Length; bi++)
            for (int ci = 0; ci < cols.Length; ci++)
            {
                if (!HagiCol(cols[ci])) continue;
                foreach (int w in W5)
                {
                    var x = g[bi, ci][w];
                    Console.WriteLine($"| {(w == 0 && ci == 2 ? names[bi] : "")} | {(w == 0 ? cols[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "shioMove"), x.N)} | {Per(X(x, "shioAtk"), x.N)} | {Per(X(x, "hatkEnd"), x.N)} | {Per(X(x, "pursuits"), x.N)} | {Per(X(x, "chase"), x.N)} ／ {Per(X(x, "chaseHp"), x.N)} | {Per(X(x, "reaps"), x.N)} ／ {Per(X(x, "reapHp"), x.N)} |");
                }
            }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // 台 × 列 × 8 物差し
    // ---------------------------------------------------------------------------------
    static void Boards(S388.Board[] boards, (string Name, UnitDef? D)[] cols, string title)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var g = new S377.B[boards.Length, cols.Length][];
        for (int bi = 0; bi < boards.Length; bi++) for (int ci = 0; ci < cols.Length; ci++) g[bi, ci] = M8(ColBoard(boards[bi], cols[ci].D));
        Console.WriteLine($"# {Tag} {title} —— 台 {boards.Length} × 列 {cols.Length}（{string.Join(" ／ ", cols.Select(c => c.Name))}）× 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}・{SeatName(b.Slot)} の {b.Who} の席にハギ）")) + "。元 ＝ ハギを入れる前の台。");
        Console.WriteLine();
        Report(boards.Select(b => b.Name).ToArray(), cols, g, origin: 0);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static bool HagiCol((string Name, UnitDef? D) c) => c.D is not null && c.D.Id == "hagi";

    static void Report(string[] names, (string Name, UnitDef? D)[] cols, S377.B[,][] g, int origin)
    {
        int nb = names.Length, nc = cols.Length;
        var hc = Enumerable.Range(0, nc).Where(ci => HagiCol(cols[ci])).ToArray();
        Console.WriteLine("## 1. 勝率・決着T（勝った戦）・倒れた編成の駒（1戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波の平均 | 元との差 |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                Console.WriteLine($"| {(ci == 0 ? names[bi] : "")} | {cols[ci].Name} | {string.Join(" | ", g[bi, ci].Select(x => x.Cell))} | {F1(A4(g[bi, ci]))} | {(ci == origin ? "—" : Sg(D4(g[bi, ci], g[bi, origin])))} |");
        Console.WriteLine();
        Console.WriteLine("## 1'. ボスの倒しT ／ 隊の1ターンの最大の与ダメ（1戦の平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | ボスの倒しT | " + string.Join(" | ", W5.Select(w => W8[w].Name)) + " |");
        Console.WriteLine("|---|---|--:|" + string.Concat(W5.Select(_ => "--:|")));
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                Console.WriteLine($"| {(ci == 0 ? names[bi] : "")} | {cols[ci].Name} | {g[bi, ci][WBoss].WinTs} | {string.Join(" | ", W5.Select(w => F0((double)X(g[bi, ci][w], "maxTurnDmg") / Math.Max(1, g[bi, ci][w].N))))} |");
        Console.WriteLine();

        Console.WriteLine("## 2. 刈り取り（1戦）——手番の刈り取り（回 ／ HP・列ごとの体を含む）／ 追撃（回）／ 追撃の刈り取り（回 ／ HP ／ 2 連の2発目で ／ 倒した数）／ P1w の追撃の列ごと（振り ／ 体）／ 再行動 ／ 付けた印");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 手番の刈り取り | 追撃 | 追撃の刈り取り | 2発目で | 追撃の列ごと | 再行動 | 印 | ハギの撃破 | ハギの与ダメ |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|---|--:|--:|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            foreach (int ci in hc)
                foreach (int w in W5)
                {
                    var x = g[bi, ci][w];
                    Console.WriteLine($"| {(ci == hc[0] && w == 0 ? names[bi] : "")} | {(w == 0 ? cols[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "reaps"), x.N)} ／ {Per(X(x, "reapHp"), x.N)} | {Per(X(x, "pursuits"), x.N)} | {Per(X(x, "chase"), x.N)} ／ {Per(X(x, "chaseHp"), x.N)} ／ 倒 {Per(X(x, "chaseKills"), x.N)} | {Per(X(x, "chase2"), x.N)} | {Per(X(x, "chaseCol"), x.N)} ／ {Per(X(x, "chaseColUnits"), x.N)} | {Per(X(x, "encores"), x.N)} | {Per(X(x, "marks"), x.N)} | {Per(X(x, "kills"), x.N)} | {F0((double)X(x, "hdmg") / Math.Max(1, x.N))} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 3. ボスで勇者を刈り取った戦（勝った戦のうち勇者の止めが刈り取りだった割合 ／ うち手番 ／ 追撃 ／ T の平均 ／ そのときの線 ／ 刈り取った HP ／ T3 以前の割合）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", hc.Select(ci => cols[ci].Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(hc.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"| {names[bi]} | {string.Join(" | ", hc.Select(ci => { var x = g[bi, ci][WBoss]; long n = X(x, "heroReaped"); return $"{Pc(n, x.Wins)}%（手番 {Pc(X(x, "heroReapedTurn"), x.Wins)} ／ 追撃 {Pc(X(x, "heroReapedChase"), x.Wins)}）・T{Per(X(x, "heroReapedT"), n)}・線 {(n == 0 ? "—" : F1(X(x, "heroLine") / 10.0 / n))}%・HP {Per(X(x, "heroReapHp"), n)}・T3 以前 {Pc(X(x, "heroReapedT3"), x.N)}%"; }))} |");
        Console.WriteLine();

        Console.WriteLine("## 4. 魂（ボス ／ 近衛 ／ 大隊・ターンの頭の魂 T1〜T8・生きていた戦の平均）・6 ／ 9 に届いた戦 %（T）・付けた印");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | T1〜T8 | 6 | 9 | 印 |");
        Console.WriteLine("|---|---|---|---|---|---|--:|");
        for (int bi = 0; bi < nb; bi++)
            foreach (int ci in hc)
            {
                if (!cols[ci].D!.Traits.Contains(TraitId.HagiDepthSoul)) continue;
                foreach (int w in new[] { WBoss, WGuard, WBat })
                {
                    var x = g[bi, ci][w];
                    string tl = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"halive{t}") == 0 ? "—" : F1((double)X(x, $"soul{t}") / X(x, $"halive{t}"))));
                    string St(string k) => $"{Pc(X(x, k), x.N)}（T{Per(X(x, k + "T"), X(x, k))}）";
                    Console.WriteLine($"| {(w == 0 && ci == hc[0] ? names[bi] : "")} | {(w == 0 ? cols[ci].Name : "")} | {W8[w].Name} | {tl} | {St("s6")} | {St("s9")} | {Per(X(x, "marks"), x.N)} |");
                }
            }
        Console.WriteLine();

        Console.WriteLine("## 5. 止めを刺したのは誰か（敵の撃破の内訳・1戦・ボス ／ 近衛 ／ 大隊）と、勇者に入ったダメージの内訳（ボス・1戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 敵の撃破 | リィカ | ハギの手番の刈り取り | ハギの追撃の刈り取り | ハギのその他 | ほか（出どころなしを含む）| 勇者の止め（リィカ ／ ハギ ／ ほか %）| 勇者へのダメージ（リィカ ／ ハギ ／ ほか）|");
        Console.WriteLine("|---|---|---|--:|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                foreach (int w in new[] { WBoss, WGuard, WBat })
                {
                    var x = g[bi, ci][w];
                    long all = X(x, "kAll"), hk = X(x, "kHagi"), rk = Math.Min(X(x, "reapKills"), hk), ck = Math.Min(X(x, "chaseKills"), rk);
                    string P(long a) => $"{Per(a, x.N)}（{Pc(a, all)}%）";
                    string hero = "", hd = "";
                    if (w == WBoss)
                    {
                        long hn = X(x, "heroKRica") + X(x, "heroKHagi") + X(x, "heroKOther");
                        hero = $"{Pc(X(x, "heroKRica"), hn)} ／ {Pc(X(x, "heroKHagi"), hn)} ／ {Pc(X(x, "heroKOther"), hn)}";
                        long hs = X(x, "hdRica") + X(x, "hdHagi") + X(x, "hdOther");
                        hd = $"{Pc(X(x, "hdRica"), hs)}% ／ {Pc(X(x, "hdHagi"), hs)}% ／ {Pc(X(x, "hdOther"), hs)}%";
                    }
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? names[bi] : "")} | {(w == WBoss ? cols[ci].Name : "")} | {W8[w].Name} | {Per(all, x.N)} | {P(X(x, "kRica"))} | {P(rk - ck)} | {P(ck)} | {P(hk - rk)} | {P(X(x, "kOther"))} | {hero} | {hd} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 6. 誰が何Tに倒れたか（元の台 ／ 4 波の平均がいちばん良い列・ボス ／ 近衛 ／ 大隊）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
        {
            int best = hc.OrderByDescending(ci => A4(g[bi, ci])).ThenByDescending(ci => g[bi, ci][WStrong].Win).First();
            Console.WriteLine($"- **{names[bi]}**（最良の列 {cols[best].Name}・4 波 {F1(A4(g[bi, best]))} ／ 元 {F1(A4(g[bi, origin]))}）");
            foreach (int w in new[] { WBoss, WGuard, WBat })
            {
                Console.WriteLine($"  - {W8[w].Name}: 元 {F1(g[bi, origin][w].Win)}%・{g[bi, origin][w].WinTs}T — {g[bi, origin][w].FellCell}");
                Console.WriteLine($"  - {W8[w].Name}: {cols[best].Name} {F1(g[bi, best][w].Win)}%・{g[bi, best][w].WinTs}T — {g[bi, best][w].FellCell}");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 7. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上 ／ あふれ ／ ボスで勇者を T3 以前に刈り取った戦）");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
            {
                if (ci == origin) continue;
                string at = $"{names[bi]} × {cols[ci].Name}";
                foreach (int w in new[] { WGuard, WBat }) { var x = g[bi, ci][w]; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{at} × {W8[w].Name}: 1T 決着 {Pc(x.Win1T, x.N)}%"); }
                { var x = g[bi, ci][WBoss]; if (x.BossFast > 0) signs.Add($"{at} × ボス: 4T 未満の勝ち {Pc(x.BossFast, x.N)}%"); if (X(x, "heroReapedT3") > 0) signs.Add($"{at} × ボス: 勇者を T3 以前に刈り取った {Pc(X(x, "heroReapedT3"), x.N)}%"); }
                for (int w = 0; w < W8.Length; w++)
                {
                    var x = g[bi, ci][w]; var x0 = g[bi, origin][w];
                    if (100.0 * x.Cap / x.N - 100.0 * x0.Cap / x0.N >= 5) signs.Add($"{at} × {W8[w].Name}: 30T の打ち切り {Pc(x0.Cap, x0.N)} → {Pc(x.Cap, x.N)}%");
                    if (x.Overflow > 0) signs.Add($"{at} × {W8[w].Name}: あふれの兆し {x.Overflow} 件");
                }
            }
        if (signs.Count == 0) Console.WriteLine("- 無し。");
        foreach (var s in signs) Console.WriteLine("- " + s);
    }

    static void Log1(string[] args)
    {
        string bn = args.Length > 3 ? args[3] : "試遊・ハギ 台B";
        string cn = args.Length > 4 ? args[4] : "P1";
        int wi = args.Length > 5 && int.TryParse(args[5], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 6 && int.TryParse(args[6], out int s0) ? s0 : 0;
        var all = S390.MainBoards().ToList();
        all.Add(new S388.Board("版・移動 ハギ", CompareBuilds().First(r => r.Name == S390.MoveRows[0]).F, 1, "ガルド"));
        var b = all.FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal)) ?? all[0];
        var c = BoardCols.FirstOrDefault(x => x.Name == cn) is { Name: not null } cc ? cc : BoardCols[3];
        var (r, _, _) = I.Raw(ColBoard(b, c.D), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name} × {c.Name} × {W8[wi].Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
