using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S387 = Sh387Diag;
using S388 = Sh388Diag;

// =====================================================================================
// sh389 —— 第389期【軽い期】本体: ハギ——魂は「鎌が届く深さ」（版 R5 ／ R4 ／ R5n）。
// 指示書は design/PHASE389_HAGI_DEPTH_SPEC.md、報告は design/PHASE389_HAGI_DEPTH.md。
//
//     dotnet run --project BattleSim -c Release 0 sh389 cmp       # §3-1 の 1: 今のハギのいる `compare` の行 × 列（第1〜5波・seed 0..199）とハギの働き
//     dotnet run --project BattleSim -c Release 0 sh389 swap      # §3-1 の 2: 試遊の台 × 1枚をハギ（版）に替えた台（段1 seed 0..9 → 版ごとの上位 3 台 ／ 台ごとのアタッカー以外の席の最良 × 8 物差し seed 0..99・同じ席に素体）
//     dotnet run --project BattleSim -c Release 0 sh389 rica      # §3-1 の 3: リィカと並べる台 2 × 列 × 8 物差し（seed 0..99）——§3-3 の 1〜7 と壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh389 best      # §5: 「版・R5 ハギ」の台（swap のアタッカー以外の席の R5 の最良）× 列 × 8 物差し——§3-3 の 1〜7
//     dotnet run --project BattleSim -c Release 0 sh389 log <台> <列> <物差し 0..7> <seed>   # 1戦のログ（台 ＝ 名前の頭・`best` の台は「版・R5」）
//     dotnet run --project BattleSim -c Release 0 sh389 check     # 自己検査
//
// **規定の駒は1つも変えない。** 替えた台 ／ 素体は器具の中だけ（試遊の台に足す 2 台を除く）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p389/predict.md`）。
// =====================================================================================
static partial class Sh389Diag
{
    internal const string PredictFile = ".tmp/p389/predict.md";
    const string Tag = "第389期";
    const int Seeds = 100, CompareSeeds = 200, S1Seeds = 10, TT = 9;
    const double Up = 3.0;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "cmp" or "swap" or "rica" or "best" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh389: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "cmp": Cmp(); return;
            case "swap": Swap(); return;
            case "rica": Boards(S388.RicaBoards(), "§3-1 の 3 リィカと並べる台"); return;
            case "best": Boards(new[] { PlayBoard() }, "§5 「版・R5 ハギ」の台（試遊・ヴィオ 台B のカドの席・アタッカー以外の席の最良の2位）"); return;
            case "log": Log1(args); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh389: モードは cmp / swap / rica / best / log / check。"); return;
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
        { ("H2", UnitCatalog.HagiH2), ("R5", UnitCatalog.HagiR5), ("R4", UnitCatalog.HagiR4), ("R5n", UnitCatalog.HagiR5n), ("R5m", UnitCatalog.HagiR5m) };
    /// <summary>列: 元の台 ／ 今のハギ ／ 素体 ／ H2 ／ R5 ／ R4 ／ R5n（`null` ＝ 元の台のまま）。素体は第388期と同じ（器具の中だけ）。</summary>
    internal static readonly (string Name, UnitDef? D)[] Cols =
        { ("元", null), ("今のハギ", UnitCatalog.HagiH389), ("素体", S388.PlainHagi), ("H2", UnitCatalog.HagiH2), ("R5", UnitCatalog.HagiR5), ("R4", UnitCatalog.HagiR4), ("R5n", UnitCatalog.HagiR5n), ("R5m", UnitCatalog.HagiR5m) };
    const int CR5 = 4, CR4 = 5, CR5n = 6, CR5m = 7;

    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    static readonly int[] W4 = { 0, 1, 2, 5 };
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
    static readonly int[] Lines = HagiDepthTrait.Lines;

    /// <summary>ハギ（`Id` ＝ hagi）の帳簿 ／ 隊の1ターンの最大の与ダメ ／ 巻き込み ／ 勇者の HP と線（ボスの波だけ）／ 勇者を刈り取った戦。</summary>
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
        var cross = new long[3];
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
                        for (int li = 0; li < 3; li++)
                            if (heroHp * 100 > (long)heroMax * Lines[li] && x.HpAfter > 0 && (long)x.HpAfter * 100 <= (long)heroMax * Lines[li]) cross[li]++;
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
                        if (reaped && act == hid && r.PlayerWon)
                        {
                            Add(b, "heroReaped", 1); Add(b, "heroReapedT", x.Turn);
                            if (x.Turn <= 3) Add(b, "heroReapedT3", 1);
                            if (r.TallyByUnit.TryGetValue("hagi", out var ht)) { Add(b, "heroLine", ht.R389LastReapLine); Add(b, "heroReapHp", ht.R389LastReapHp); }
                        }
                    }
                    break;
            }
        }
        Add(b, "maxTurnDmg", dmgT.Max());
        if (hero is not null) for (int li = 0; li < 3; li++) Add(b, $"heroCross{Lines[li]}", cross[li]);
        if (r.TallyByUnit.TryGetValue("hagi", out var h) && hagi is not null)
        {
            Add(b, "strikes", h.R388Strikes); Add(b, "sweeps", h.R389Sweeps); Add(b, "reaps", h.R388Reaps); Add(b, "reapHp", h.R388ReapHp); Add(b, "encores", h.R388Encores);
            Add(b, "colReaps", h.R389ColumnReaps); Add(b, "colUnits", h.R389ColumnUnits); Add(b, "colHp", h.R389ColumnHp);
            Add(b, "pursuits", h.R388Pursuits); Add(b, "pursuitDmg", h.R388PursuitDmg); Add(b, "kills", h.Kills); Add(b, "hdmg", h.DamageToEnemy);
            Add(b, "spill", h.R388Spill); Add(b, "spillHits", h.R388SpillHits);
            Add(b, "soulDeath", h.R389SoulDeath); Add(b, "c80", h.R389Cross80); Add(b, "c50", h.R389Cross50); Add(b, "c20", h.R389Cross20); Add(b, "soulPeak", h.R389SoulPeak);
            if (h.R389Soul3Turn > 0) { Add(b, "s3", 1); Add(b, "s3T", h.R389Soul3Turn); }
            if (h.R389Soul6Turn > 0) { Add(b, "s6", 1); Add(b, "s6T", h.R389Soul6Turn); }
            if (h.R389Soul9Turn > 0) { Add(b, "s9", 1); Add(b, "s9T", h.R389Soul9Turn); }
            if (h.R389SoulByTurn is { } sb) for (int t = 1; t < TT && t < sb.Length; t++) Add(b, $"soul{t}", sb[t]);
            Add(b, "pursueOld", r.Log.Count(l => l.Text.Contains("が倒れた隙に踏み込む", StringComparison.Ordinal)));
            Add(b, "marks", h.R389MarksPlaced); Add(b, "markReaps", h.R389MarkReaps);
        }
        // 第389期の補足: 勇者の刈りの印（そのターンの終わりの数・印が付かなかったターンは前のターンの数を持ち越す・倒れたターンまで）
        if (hero is not null && r.TallyByUnit.TryGetValue(hero.Def.Id, out var htl) && htl.R389MarkByTurn is { } mb)
        {
            long cur = 0; int last = heroAlive ? Math.Min(TT - 1, r.Turns) : Math.Min(TT - 1, Math.Max(1, r.Events.LastOrDefault(ev => ev.Kind == BattleEventKind.Death && ev.TargetId == hero.InstanceId)?.Turn ?? r.Turns));
            for (int t = 1; t <= last; t++) { if (t < mb.Length && mb[t] > 0) cur = mb[t]; Add(b, $"hmark{t}", cur); Add(b, $"hmarkn{t}", 1); }
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
        var rows = CompareBuilds().Where(r => Has(r.F, "hagi")).ToArray();
        int nw = EnemyCatalog.Stages.Count;
        var cols = Cols.Skip(1).ToArray();
        var grids = cols.Select(c => CGrid(rows.Select(r => (r.Name, I.Swap(r.F, "hagi", c.D!))).ToArray(), CompareSeeds)).ToArray();
        Console.WriteLine($"# {Tag} §3-1 の 1 `compare` のハギの行 —— {rows.Length} 行 × 列（第1〜5波・seed 0..{CompareSeeds - 1}・ハギだけ替える）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 席 | " + string.Join(" | ", cols.Select(c => c.Name + " 第1〜5波")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(cols.Select(_ => "---|")));
        for (int ri = 0; ri < rows.Length; ri++)
        {
            string seat = string.Join("・", rows[ri].F.Occupied().Where(o => o.Def.Id == "hagi").Select(o => SeatName(o.Slot)));
            Console.WriteLine($"| {rows[ri].Name} | {seat} | {string.Join(" | ", grids.Select(g => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w])))))} |");
        }
        Console.WriteLine();
        Console.WriteLine("| 列 | 第2〜5波の平均 | 今のハギとの差 | 動いたセル（今のハギと違う・第2〜5波）| −10pt 以上落ちたセル |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        double Avg(double[,] g) => Enumerable.Range(0, rows.Length).Average(ri => Enumerable.Range(1, nw - 1).Average(w => g[ri, w]));
        for (int c = 0; c < cols.Length; c++)
        {
            int moved = 0, drop = 0;
            for (int ri = 0; ri < rows.Length; ri++) for (int w = 1; w < nw; w++) { if (grids[c][ri, w] != grids[0][ri, w]) moved++; if (grids[c][ri, w] - grids[0][ri, w] <= -10) drop++; }
            Console.WriteLine($"| {cols[c].Name} | {F1(Avg(grids[c]))} | {Sg(Avg(grids[c]) - Avg(grids[0]))} | {moved} | {drop} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-1 の 2: 試遊の台に入れて（アタッカー以外の席を含む）
    // ---------------------------------------------------------------------------------
    /// <summary>台のアタッカー: 元の台を段1 と同じ 4 波 × seed 0..9 で回し、敵への与ダメの合計がいちばん大きい編成の駒（同じなら席の若い方）の席。</summary>
    internal static int AttackerSeat(Formation f)
    {
        var sum = new Dictionary<int, long>();
        foreach (var w in new[] { I.Boss, I.Guard, I.Bat, I.M5 })
            for (int s = 0; s < S1Seeds; s++)
            {
                var (r, p, _) = I.Raw(f, w, s);
                foreach (var u in p.Where(u => u.Slot < 5))
                    if (r.TallyByUnit.TryGetValue(u.Def.Id, out var t)) sum[u.Slot] = sum.GetValueOrDefault(u.Slot) + t.DamageToEnemy;
            }
        return Seats5(f).OrderByDescending(s => sum.GetValueOrDefault(s)).ThenBy(s => s).First();
    }

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
            if (j.V < 0) { bs[j.B] = r; atk[j.B] = AttackerSeat(f); } else grid[j.V, j.B, j.S] = r;
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
        var boards = S387.Play();
        int nb = boards.Length;
        var (bs, all, atk) = Stage1(boards);
        Console.WriteLine($"# {Tag} §3-1 の 2 試遊の台に入れて —— 試遊の台 {nb} × 1枚をハギ（版）に替えた台（第387期 §4-3 と同じやり方 ＋ アタッカー以外の席）");
        Console.WriteLine();
        Console.WriteLine($"段1: seed 0..{S1Seeds - 1} × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波（＋ 強い条件 第2〜5波は並べ替えの2番目の鍵）。段2: seed 0..{Seeds - 1} × 8 物差し・同じ席に素体。");
        Console.WriteLine("アタッカー ＝ 元の台を段1 と同じ 4 波 × seed 0..9 で回して、敵への与ダメの合計がいちばん大きい編成の駒（同じなら席の若い方）。");
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

        // ---- 段2: 台ごと × 版ごとのアタッカー以外の席の最良 ----
        var cache = new Dictionary<string, S377.B[]>();
        S377.B[] M(int b, int s, UnitDef? d) { string k = $"{b}:{s}:{d?.Id}:{d?.GetHashCode()}"; lock (cache) if (cache.TryGetValue(k, out var x)) return x; var y = M8(d is null ? boards[b].F : Put(boards[b].F, s, d)); lock (cache) cache[k] = y; return y; }
        Console.WriteLine($"## 2. 台ごとのアタッカー以外の席の最良（版ごと・段1 の 4 波の差 → 強い条件の差で選んだ席・seed 0..{Seeds - 1}・差 ＝ 替えた台 − 元の台）");
        Console.WriteLine();
        Console.WriteLine("| 台（アタッカー）| 元 4 波 ／ 強 ／ ボスの倒しT | " + string.Join(" | ", Vers.Select(v => $"{v.Name}: 席（誰を）4 波の差 ／ 強の差 ／ ボスT")) + " | 同じ席の素体（R5 の席）|");
        Console.WriteLine("|---|---|" + string.Concat(Vers.Select(_ => "---|")) + "---|");
        var bestBy = Vers.ToDictionary(v => v.Name, _ => new List<(int B, int S, double D4, double DS)>());
        for (int b = 0; b < nb; b++)
        {
            if (Has(boards[b].F, "hagi")) continue;
            var b0 = M(b, -1, null);
            var cells = new List<string>(); int r5Seat = -1;
            for (int v = 0; v < Vers.Length; v++)
            {
                var pick = all[v].FirstOrDefault(x => x.B == b && x.S != atk[b]);
                if (pick is null) { cells.Add("—"); continue; }
                var a = M(b, pick.S, Vers[v].D);
                double d4 = D4(a, b0), ds = a[WStrong].Win - b0[WStrong].Win;
                if (Vers[v].Name == "R5") r5Seat = pick.S;
                bestBy[Vers[v].Name].Add((b, pick.S, d4, ds));
                cells.Add($"{SeatName(pick.S)}（{Short(boards[b].F[pick.S]!)}）{Sg(d4)} ／ {Sg(ds)} ／ {a[WBoss].WinTs}T");
            }
            string pl = "—";
            if (r5Seat >= 0) { var p = M(b, r5Seat, S388.PlainHagi); pl = $"{Sg(D4(p, b0))} ／ {Sg(p[WStrong].Win - b0[WStrong].Win)} ／ {p[WBoss].WinTs}T"; }
            Console.WriteLine($"| {boards[b].Name}（{SeatName(atk[b])} {Short(boards[b].F[atk[b]]!)}）| {F1(A4(b0))} ／ {F1(b0[WStrong].Win)} ／ {b0[WBoss].WinTs}T | {string.Join(" | ", cells)} | {pl} |");
        }
        Console.WriteLine();
        foreach (var (vn, lst) in bestBy)
        {
            var br = lst.OrderByDescending(x => x.D4).ThenByDescending(x => x.DS).ThenBy(x => x.B).First();
            Console.WriteLine($"- **{vn} のアタッカー以外の席の最良**: {boards[br.B].Name} の {SeatName(br.S)}（{Short(boards[br.B].F[br.S]!)} を替える）・4 波 {Sg(br.D4)} ／ 強 {Sg(br.DS)}。4 波で +3pt 以上上がった台 {lst.Count(x => x.D4 >= Up)} ／ {lst.Count}・強い条件で +3pt 以上 {lst.Count(x => x.DS >= Up)}・4 波の差の平均 {Sg(lst.Average(x => x.D4))}・強の差の平均 {Sg(lst.Average(x => x.DS))}。");
        }
        Console.WriteLine();

        // ---- 段2: 版ごとの上位 3 台（席を問わず・第388期と同じ表）----
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

    /// <summary>「版・R5 ハギ」の台: swap と同じ段1 で、アタッカー以外の席の R5 の最良（段1 の 4 波の差 → 強い条件の差・同じなら台 ／ 席の若い方）。</summary>
    /// <summary>「版・R5 ハギ」の台（`Presets.HagiPlay389` の元の並び ＝ 試遊・ヴィオ 台B・後1 のカドを替える）。</summary>
    internal static S388.Board PlayBoard() => new("試遊・ヴィオ 台B", Presets.BoardBVio375(), 3, "カド");

    internal static S388.Board BestBoard(string ver = "R5")
    {
        var boards = S387.Play();
        var (_, all, atk) = Stage1(boards);
        int v = Array.FindIndex(Vers, x => x.Name == ver);
        var x = all[v].First(y => y.S != atk[y.B]);
        return new S388.Board(boards[x.B].Name, boards[x.B].F, x.S, Short(boards[x.B].F[x.S]!));
    }

    // ---------------------------------------------------------------------------------
    // §3-1 の 3 ／ §5: 台 × 列 × 8 物差し
    // ---------------------------------------------------------------------------------
    static Formation ColBoard(S388.Board b, UnitDef? d) => d is null ? b.F : Put(b.F, b.Slot, d);

    static void Boards(S388.Board[] boards, string title)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var g = new S377.B[boards.Length, Cols.Length][];
        for (int bi = 0; bi < boards.Length; bi++) for (int ci = 0; ci < Cols.Length; ci++) g[bi, ci] = M8(ColBoard(boards[bi], Cols[ci].D));
        Console.WriteLine($"# {Tag} {title} —— 台 {boards.Length} × 列 {Cols.Length}（{string.Join(" ／ ", Cols.Select(c => c.Name))}）× 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}・{SeatName(b.Slot)} の {b.Who} を替える）")) + "。");
        Console.WriteLine();
        Report(boards.Select(b => b.Name).ToArray(), g);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Report(string[] names, S377.B[,][] g)
    {
        int nb = names.Length, nc = Cols.Length;
        int[] ws5 = { 0, 1, 2, 5, 7 };
        Console.WriteLine("## 1. 勝率・決着T（勝った戦）・倒れた編成の駒（1戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波の平均 | 元との差 |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                Console.WriteLine($"| {(ci == 0 ? names[bi] : "")} | {Cols[ci].Name} | {string.Join(" | ", g[bi, ci].Select(x => x.Cell))} | {F1(A4(g[bi, ci]))} | {(ci == 0 ? "—" : Sg(D4(g[bi, ci], g[bi, 0])))} |");
        Console.WriteLine();
        Console.WriteLine("## 1'. ボスの倒しT ／ 隊の1ターンの最大の与ダメ（1戦の平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | ボスの倒しT | " + string.Join(" | ", ws5.Select(w => W8[w].Name)) + " |");
        Console.WriteLine("|---|---|--:|" + string.Concat(ws5.Select(_ => "--:|")));
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                Console.WriteLine($"| {(ci == 0 ? names[bi] : "")} | {Cols[ci].Name} | {g[bi, ci][WBoss].WinTs} | {string.Join(" | ", ws5.Select(w => F0((double)X(g[bi, ci][w], "maxTurnDmg") / Math.Max(1, g[bi, ci][w].N))))} |");
        Console.WriteLine();

        Console.WriteLine("## 2. ボスで勇者を刈り取った戦（勝った戦のうち勇者の止めが刈り取りだった割合 ／ T の平均 ／ そのときの線 ／ 刈り取った HP ／ T3 以前の割合）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Enumerable.Range(3, nc - 3).Select(ci => Cols[ci].Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(3, nc - 3).Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"| {names[bi]} | {string.Join(" | ", Enumerable.Range(3, nc - 3).Select(ci => { var x = g[bi, ci][WBoss]; long n = X(x, "heroReaped"); return $"{Pc(n, x.Wins)}%・T{Per(X(x, "heroReapedT"), n)}・線 {(n == 0 ? "—" : F1(X(x, "heroLine") / 10.0 / n))}%・HP {Per(X(x, "heroReapHp"), n)}・T3 以前 {Pc(X(x, "heroReapedT3"), x.N)}%"; }))} |");
        Console.WriteLine();

        Console.WriteLine("## 3. 魂（R5 ／ R4 ／ R5n ／ R5m）——ターンの頭の魂（生きていた戦の平均・T1〜T8）・3 ／ 6 ／ 9 に届いた戦 %（T）・出どころ（敵の死 ／ 8割 ／ 5割 ／ 2割）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | T1〜T8 | 3 | 6 | 9 | 敵の死 ／ 8割 ／ 5割 ／ 2割 | 魂の最大 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = CR5; ci <= CR5m; ci++)
                foreach (int w in new[] { 0, 1, 2, 5, 7 })
                {
                    var x = g[bi, ci][w];
                    string tl = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"halive{t}") == 0 ? "—" : F1((double)X(x, $"soul{t}") / X(x, $"halive{t}"))));
                    string St(string k) => $"{Pc(X(x, k), x.N)}（T{Per(X(x, k + "T"), X(x, k))}）";
                    Console.WriteLine($"| {(ci == CR5 && w == 0 ? names[bi] : "")} | {(w == 0 ? Cols[ci].Name : "")} | {W8[w].Name} | {tl} | {St("s3")} | {St("s6")} | {St("s9")} | {Per(X(x, "soulDeath"), x.N)} ／ {Per(X(x, "c80"), x.N)} ／ {Per(X(x, "c50"), x.N)} ／ {Per(X(x, "c20"), x.N)} | {Per(X(x, "soulPeak"), x.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 4. ハギの働き（1戦）——斬撃 ／ 薙ぎ ／ 刈り取り（回 ／ HP）／ 列をまとめて刈った（振り ／ 体数）／ 再行動 ／ 追撃（回 ／ 与ダメ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 斬撃 | うち薙ぎ | 刈り取り | 刈り取った HP | 列ごと（振り ／ 体）| 再行動 | 追撃 | 追撃の与ダメ | ハギの撃破 | ハギの与ダメ | 今のハギの追い打ち |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|---|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 1; ci < nc; ci++)
            {
                if (ci == 2) continue;   // 素体は帳簿を持たない
                foreach (int w in ws5)
                {
                    var x = g[bi, ci][w];
                    Console.WriteLine($"| {(ci == 1 && w == 0 ? names[bi] : "")} | {(w == 0 ? Cols[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "strikes"), x.N)} | {Per(X(x, "sweeps"), x.N)} | {Per(X(x, "reaps"), x.N)} | {Per(X(x, "reapHp") + X(x, "colHp"), x.N)} | {Per(X(x, "colReaps"), x.N)} ／ {Per(X(x, "colUnits"), x.N)} | {Per(X(x, "encores"), x.N)} | {Per(X(x, "pursuits"), x.N)} | {Per(X(x, "pursuitDmg"), x.N)} | {Per(X(x, "kills"), x.N)} | {F0((double)X(x, "hdmg") / Math.Max(1, x.N))} | {Per(X(x, "pursueOld"), x.N)} |");
                }
            }
        Console.WriteLine();

        Console.WriteLine("### 4'. 刈りの印（R5m）——勇者の印の数（ボス・そのターンの終わり・T1〜T8・まだ倒れていない戦の平均）・1戦に付けた印 ／ 印で深くなった線で刈った回数");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勇者の印 T1〜T8 | 付けた印（ボス ／ 近衛 ／ 大隊 ／ 第5波 ／ 強）| 印で深くなった線で刈った（同じ順）|");
        Console.WriteLine("|---|---|---|---|");
        for (int bi = 0; bi < nb; bi++)
        {
            var gb = g[bi, CR5m];
            var xb = gb[WBoss];
            Console.WriteLine($"| {names[bi]} | {string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(xb, $"hmarkn{t}") == 0 ? "—" : F1((double)X(xb, $"hmark{t}") / X(xb, $"hmarkn{t}"))))} | {string.Join(" ／ ", ws5.Select(w => Per(X(gb[w], "marks"), gb[w].N)))} | {string.Join(" ／ ", ws5.Select(w => Per(X(gb[w], "markReaps"), gb[w].N)))} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 5. 巻き込み（1戦・ボス ／ 近衛 ／ 本編 第5波）——隣の味方へ与えた傷（実際に減った HP）・受けた駒と、それで倒れた回数");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 与えた傷 | 回数 | 受けた駒（減った HP ／ 倒れた回数）|");
        Console.WriteLine("|---|---|---|--:|--:|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 3; ci < nc; ci++)
                foreach (int w in new[] { 0, 1, 5 })
                {
                    var x = g[bi, ci][w];
                    var who = x.X.Keys.Where(k => k.StartsWith("spillTo:", StringComparison.Ordinal)).Select(k => k[8..]).OrderByDescending(id => X(x, "spillTo:" + id))
                        .Select(id => $"{Nm(id)} {Per(X(x, "spillTo:" + id), x.N)} ／ {Per(X(x, "spillDeath:" + id), x.N)}");
                    Console.WriteLine($"| {(ci == 3 && w == 0 ? names[bi] : "")} | {(w == 0 ? Cols[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "spill"), x.N)} | {Per(X(x, "spillHits"), x.N)} | {string.Join("・", who)} |");
                }
        Console.WriteLine();
        if (Enumerable.Range(0, nb).Any(bi => g[bi, 0][0].RicaIn > 0))
        {
            Console.WriteLine("### 5'. リィカの層の最大（1戦・リィカのいる台）");
            Console.WriteLine();
            Console.WriteLine("| 台 | 列 | " + string.Join(" | ", ws5.Select(w => W8[w].Name)) + " |");
            Console.WriteLine("|---|---|" + string.Concat(ws5.Select(_ => "--:|")));
            for (int bi = 0; bi < nb; bi++)
            {
                if (g[bi, 0][0].RicaIn == 0) continue;
                for (int ci = 0; ci < nc; ci++)
                    Console.WriteLine($"| {(ci == 0 ? names[bi] : "")} | {Cols[ci].Name} | {string.Join(" | ", ws5.Select(w => Per(g[bi, ci][w].PeakSum, g[bi, ci][w].N)))} |");
            }
            Console.WriteLine();
        }

        Console.WriteLine("## 6. 勇者の HP（ボス・最初の攻撃の直前 ＝ ターンの頭の回復の後・T1〜T8・生きていた戦の平均）と、線を割った回数（1戦・8割 ／ 5割 ／ 2割）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | T1〜T8 | 8割 ／ 5割 ／ 2割 |");
        Console.WriteLine("|---|---|---|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
            {
                var x = g[bi, ci][WBoss];
                Console.WriteLine($"| {(ci == 0 ? names[bi] : "")} | {Cols[ci].Name} | {string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"hhn{t}") == 0 ? "—" : F0((double)X(x, $"hhp{t}") / X(x, $"hhn{t}"))))} | {string.Join(" ／ ", Lines.Select(l => Per(X(x, $"heroCross{l}"), x.N)))} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 7. 誰が何Tに倒れたか（元の台 ／ 4 波の平均がいちばん良い版・ボス ／ 近衛 ／ 大隊）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
        {
            int best = Enumerable.Range(3, nc - 3).OrderByDescending(ci => A4(g[bi, ci])).ThenByDescending(ci => g[bi, ci][WStrong].Win).First();
            Console.WriteLine($"- **{names[bi]}**（最良の版 {Cols[best].Name}・4 波 {F1(A4(g[bi, best]))} ／ 元 {F1(A4(g[bi, 0]))}）");
            foreach (int w in new[] { WBoss, WGuard, WBat })
            {
                Console.WriteLine($"  - {W8[w].Name}: 元 {F1(g[bi, 0][w].Win)}%・{g[bi, 0][w].WinTs}T — {g[bi, 0][w].FellCell}");
                Console.WriteLine($"  - {W8[w].Name}: {Cols[best].Name} {F1(g[bi, best][w].Win)}%・{g[bi, best][w].WinTs}T — {g[bi, best][w].FellCell}");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 8. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上 ／ あふれ ／ ボスで勇者を T3 以前に刈り取った戦）");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 1; ci < nc; ci++)
            {
                string at = $"{names[bi]} × {Cols[ci].Name}";
                foreach (int w in new[] { WGuard, WBat }) { var x = g[bi, ci][w]; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{at} × {W8[w].Name}: 1T 決着 {Pc(x.Win1T, x.N)}%"); }
                { var x = g[bi, ci][WBoss]; if (x.BossFast > 0) signs.Add($"{at} × ボス: 4T 未満の勝ち {Pc(x.BossFast, x.N)}%"); if (X(x, "heroReapedT3") > 0) signs.Add($"{at} × ボス: 勇者を T3 以前に刈り取った {Pc(X(x, "heroReapedT3"), x.N)}%"); }
                for (int w = 0; w < W8.Length; w++)
                {
                    var x = g[bi, ci][w]; var x0 = g[bi, 0][w];
                    if (100.0 * x.Cap / x.N - 100.0 * x0.Cap / x0.N >= 5) signs.Add($"{at} × {W8[w].Name}: 30T の打ち切り {Pc(x0.Cap, x0.N)} → {Pc(x.Cap, x.N)}%");
                    if (x.Overflow > 0) signs.Add($"{at} × {W8[w].Name}: あふれの兆し {x.Overflow} 件");
                }
            }
        if (signs.Count == 0) Console.WriteLine("- 無し。");
        foreach (var s in signs) Console.WriteLine("- " + s);
    }

    static string Nm(string id) => UnitCatalog.Everyone.FirstOrDefault(d => d.Id == id) is { } d ? Short(d) : id switch { "spore" => "胞子", "wight" => "亡者", _ => id };

    static void Log1(string[] args)
    {
        string bn = args.Length > 3 ? args[3] : "試遊・死の連鎖";
        string cn = args.Length > 4 ? args[4] : "R5";
        int wi = args.Length > 5 && int.TryParse(args[5], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 6 && int.TryParse(args[6], out int s0) ? s0 : 0;
        var all = S388.RicaBoards().ToList();
        all.Insert(0, PlayBoard());
        var b = all.FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal) || bn.StartsWith("版・R5", StringComparison.Ordinal)) ?? all[0];
        var c = Cols.FirstOrDefault(x => x.Name == cn) is { Name: not null } cc ? cc : Cols[CR5];
        var (r, _, _) = I.Raw(ColBoard(b, c.D), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name} × {c.Name} × {W8[wi].Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
