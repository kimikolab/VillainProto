using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S386 = Sh386Diag;

// =====================================================================================
// sh387 —— 第387期【軽い期】段0: 胞子の「弾けて毒」（P）を規定に ／ 本体: 残りの駒の棚卸し（数えるだけ）。
// 指示書は design/PHASE387_ROSTER_LEFTOVERS_SPEC.md、報告は design/PHASE387_ROSTER_LEFTOVERS.md。
//
//     dotnet run --project BattleSim -c Release 0 sh387 zero      # §3-2: `compare` の動き（第386期の規定 ↔ 今・seed 0..199）／ ムグのいる試遊の台 4 × 5 物差し（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh387 use       # §4-2 の 1 ／ 2: 10 枚のいる `compare` の行（第1〜5波・seed 0..199）／ 素体に替えた差（本編 第2〜5波・seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh387 swap      # §4-3: 試遊の台 × 1枚を 10 枚に替えた台（段1 seed 0..9 × 4 波 ＋ 強い条件 → 段2 駒ごとの上位 3 台 × 8 物差し seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh387 overlap   # §4-4: ロスター 52 枚を試遊の台の席に替えた差の並び（seed 0..9 × 4 波）で近い駒 ／ 替え合いの差（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh387 dolga     # §4-5: ドルガ ／ ガンのいる `compare` の行 ／ 試遊の台 × ボス ／ 近衛 ／ 大隊 ／ 強い条件（seed 0..99）と、ほかの試遊の台
//     dotnet run --project BattleSim -c Release 0 sh387 check     # 自己検査
//
// **規定の駒は1つも変えない**（本体）。素体 ／ 替えた台は器具の中だけ（`Presets` に足さない）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p387/predict.md`）。
// =====================================================================================
static partial class Sh387Diag
{
    internal const string PredictFile = ".tmp/p387/predict.md";
    const string Tag = "第387期";
    const int Seeds = 100, CompareSeeds = 200, S1Seeds = 10;
    /// <summary>「上がった」の線（pt）。第88期のノイズ床（フィルタ前 3.26pt）に合わせて丸めた。</summary>
    const double Up = 3.0;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "zero" or "use" or "swap" or "overlap" or "dolga" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh387: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "zero": Zero(); return;
            case "use": Use(); return;
            case "swap": Swap(); return;
            case "overlap": Overlap(); return;
            case "dolga": DolgaGan(); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh387: モードは zero / use / swap / overlap / dolga / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => x.ToString("#,0");
    static string Sg(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Short(UnitDef d) => S307.Short(d);

    // ---------------------------------------------------------------------------------
    // 棚卸しの 10 枚 ／ 素体 ／ 試遊の台 ／ 波
    // ---------------------------------------------------------------------------------
    internal static readonly UnitDef[] Ten =
        { UnitCatalog.Kari, UnitCatalog.Uro, UnitCatalog.TomoH393, UnitCatalog.Nomi, UnitCatalog.Sekki, UnitCatalog.HagiH389, UnitCatalog.Kubi, UnitCatalog.Ban, UnitCatalog.DolgaH395, UnitCatalog.Gan };   // 第396期: ドルガは第395期までの規定に固定

    /// <summary>素体（同じ体・札なし・ふつうに殴る・型 ／ 踏込はそのまま）。器具の中だけ（`Id` の尾に `_plain387`）。</summary>
    internal static UnitDef Plain(UnitDef d) => new()
    {
        Id = d.Id + "_plain387", Name = "素体の" + d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern, Advances = d.Advances, PlusText = "", MinusText = "",
    };

    /// <summary>今の試遊の台（`Presets.Versions` の「試遊・」の台すべて）。</summary>
    internal static (string Name, Formation F)[] Play() => VersionsH389().Where(   /* 第390期: 第389期の一覧（「試遊・ハギ 台B」「試遊・死神と墓守」を足す前）から */r => r.Name.StartsWith("試遊・", StringComparison.Ordinal)).ToArray();

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }
    static Formation Replace(Formation f, string id, UnitDef to) => I.Swap(f, id, to);
    static int[] Seats5(Formation f) => f.Occupied().Select(o => o.Slot).Where(s => s < 5).OrderBy(s => s).ToArray();
    static string SeatName(int s) => s switch { 0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3", _ => $"席{s}" };

    static readonly I.W[] W4 = { I.Boss, I.Guard, I.Bat, I.M5 };
    static readonly string[] W4Names = { "ボス", "近衛", "大隊", "本編 第5波" };
    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;   // ボス ／ 近衛 ／ 大隊 ／ 本編 第2 ／ 4 ／ 5波 ／ 九体 ／ 強 第2〜5波
    static readonly int[] W8Of4 = { 0, 1, 2, 5 };   // 8 物差しのうち段1 の 4 波
    const int W8Strong = 7;

    /// <summary>計数の規則を渡さない速い1戦（勝ちかどうかだけ）。</summary>
    static bool Won(Formation f, I.W w, int seed) => I.Raw(f, w, seed, census: false).R.PlayerWon;
    static double WinRate(Formation f, I.W[] ws, int seeds)
    {
        int n = 0;
        foreach (var w in ws) for (int s = 0; s < seeds; s++) if (Won(f, w, s)) n++;
        return 100.0 * n / (ws.Length * seeds);
    }

    /// <summary>`compare` と同じ回し方の勝率（本編の波 `EnemyCatalog.Stages`）。</summary>
    static double[,] CGrid((string Name, Formation F)[] rows, int seeds, int w0 = 0)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw, wins = 0;
            if (wi < w0) { g[ri, wi] = double.NaN; return; }
            for (int s = 0; s < seeds; s++) if (BattleEngine.Run(rows[ri].F, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / seeds;
        });
        return g;
    }

    // ---------------------------------------------------------------------------------
    // 札が働いた回数（駒ごとに 2 つ・1戦あたり）
    // ---------------------------------------------------------------------------------
    internal sealed record Work(string A, string B, Func<BattleResult, UnitState, long> FA, Func<BattleResult, UnitState, long> FB);
    static UnitTally Tal(BattleResult r, UnitState u) => r.TallyByUnit.TryGetValue(u.Def.Id, out var t) ? t : new UnitTally();
    static long Logs(BattleResult r, string phrase) => r.Log.Count(l => l.Text.Contains(phrase, StringComparison.Ordinal));
    static long Swings(BattleResult r, UnitState u, AttackPattern? p = null) => r.Events.Count(x => x.Kind == BattleEventKind.Attack && x.ActorId == u.InstanceId && (p is null || x.Pattern == p));
    static long Shields(BattleResult r, UnitState u) => r.Events.Count(x => x.Kind == BattleEventKind.Intercept && x.ActorId == u.InstanceId && x.Text == InterceptLabels.RangeShield);

    /// <summary>駒ごとの札の働き（器具の中だけの数え方。ログの文は駒の札が出す文だけを拾う——画面の規則ではなく計数）。</summary>
    internal static Work WorkOf(string id) => id switch
    {
        "kari" => new("押し出した", "自分の与ダメ", (r, u) => Logs(r, "を前へ押し出した"), (r, u) => Tal(r, u).DamageToEnemy),
        "uro" => new("欠片を拾った", "貫きで振った", (r, u) => Logs(r, "の欠片を拾った"), (r, u) => Swings(r, u, AttackPattern.Pierce)),
        "tomo" => new("灯した", "手番を譲った", (r, u) => Tal(r, u).TaillightFires, (r, u) => Tal(r, u).TaillightYields),
        "nomi" => new("もう一度動いた", "与ダメ", (r, u) => Tal(r, u).EncoreFires, (r, u) => Tal(r, u).DamageToEnemy),
        "sekki" => new("後備えで庇った", "怒りで積んだ攻撃力", (r, u) => Tal(r, u).Intercepts, (r, u) => Tal(r, u).RageGain),
        "hagi" => new("追い打った", "与ダメ", (r, u) => Logs(r, "が倒れた隙に踏み込む"), (r, u) => Tal(r, u).DamageToEnemy),
        "kubi" => new("萎縮させた敵", "萎縮した味方", (r, u) => Tal(r, u).DauntFoes, (r, u) => Tal(r, u).DauntAllies),
        "ban" => new("踏みしめた", "範囲を受け止めた", (r, u) => Logs(r, "が踏みしめた（"), (r, u) => Shields(r, u)),
        "dolga" => new("振った", "全体で振った", (r, u) => Swings(r, u), (r, u) => Tal(r, u).ReaderAlls),
        "gan" => new("叩き起こした", "溜めに乗せた", (r, u) => Logs(r, "を叩き起こした"), (r, u) => Logs(r, "の溜めが乗った")),
        _ => new("—", "—", (_, _) => 0, (_, _) => 0),
    };

    /// <summary>`Sh377Diag.One` の `extra`: その駒の札の働き（`wA` ／ `wB`）。</summary>
    static Action<BattleResult, List<UnitState>, List<UnitState>, S377.B> WorkExtra(UnitDef d)
    {
        var w = WorkOf(d.Id);
        return (r, p, e, b) =>
        {
            var u = p.FirstOrDefault(x => x.Def.Id == d.Id);
            if (u is null) return;
            long a = w.FA(r, u), bb = w.FB(r, u);
            if (a != 0) b.X["wA"] = b.X.GetValueOrDefault("wA") + a;
            if (bb != 0) b.X["wB"] = b.X.GetValueOrDefault("wB") + bb;
        };
    }

    static S377.B[] M8(Formation f, Action<BattleResult, List<UnitState>, List<UnitState>, S377.B>? extra = null)
    {
        var g = new S377.B[W8.Length];
        Parallel.For(0, W8.Length, wi => g[wi] = S377.Measure(f, W8[wi].Ws, Seeds, extra));
        return g;
    }
    static double D4(S377.B[] a, S377.B[] b) => W8Of4.Average(wi => a[wi].Win - b[wi].Win);

    // ---------------------------------------------------------------------------------
    // §3-2 段0
    // ---------------------------------------------------------------------------------
    static void Zero()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds();
        var old = rows.Select(r => (r.Name, Pin386(r.F))).ToArray();
        var g0 = CGrid(old, CompareSeeds); var g1 = CGrid(rows, CompareSeeds);
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        Console.WriteLine($"# {Tag} §3-2 段0 弾けて毒（P）を規定に —— `compare` {rows.Length} 行 × {nw} 波（seed 0..{CompareSeeds - 1}）・旧 ＝ 第386期の規定（ムグを `MugH386` に固定）");
        Console.WriteLine();
        Console.WriteLine("## 1. 動いたセル");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | 旧 | 今 | 差 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        int moved = 0, up = 0, down = 0, movedRows = 0;
        for (int ri = 0; ri < rows.Length; ri++)
        {
            bool any = false;
            for (int w = 0; w < nw; w++)
                if (g0[ri, w] != g1[ri, w])
                {
                    any = true; if (w >= 1) { moved++; if (g1[ri, w] > g0[ri, w]) up++; else down++; }
                    Console.WriteLine($"| {rows[ri].Name} | 第{w + 1}波 | {F1(g0[ri, w])} | {F1(g1[ri, w])} | {Sg(g1[ri, w] - g0[ri, w])} |");
                }
            if (any) movedRows++;
        }
        Console.WriteLine();
        Console.WriteLine($"- 動いた行 {movedRows}・動いたセル（第2〜5波）{moved}（上がった {up} ／ 落ちた {down}）。ムグのいない行で動いたセル: {Enumerable.Range(0, rows.Length).Where(ri => !S377.HasMug(rows[ri].F)).Sum(ri => Enumerable.Range(0, nw).Count(w => g0[ri, w] != g1[ri, w]))}。");
        var prim = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToArray();
        string Avg(double[,] g, int[] set) => string.Join(" / ", Enumerable.Range(0, nw).Select(w => F1(set.Average(ri => g[ri, w]))));
        var all = Enumerable.Range(0, rows.Length).ToArray();
        int Info(double[,] g, int[] set) => set.Sum(ri => Enumerable.Range(1, nw - 1).Count(w => g[ri, w] is > 0 and < 100));
        int Over95(double[,] g) => all.Count(ri => g[ri, last] > 95);
        Console.WriteLine($"- 全{rows.Length}行の平均（第1〜5波）: 旧 {Avg(g0, all)} → 今 {Avg(g1, all)}。");
        Console.WriteLine($"- 主判定19行（{prim.Length} 行）: 旧 {Avg(g0, prim)} → 今 {Avg(g1, prim)}・第五波の歯止め {F1(Baseline.PrimaryFifthFloor)}%。");
        Console.WriteLine($"- 第五波 95% 超の行: 旧 {Over95(g0)} → 今 {Over95(g1)}。情報セル（第2〜5波の 0 < x < 100）: 全行 {Info(g0, all)} → {Info(g1, all)} ／ 主判定 {Info(g0, prim)} → {Info(g1, prim)}。");
        Console.WriteLine();

        // ---- ムグのいる試遊の台 ----
        var boards = new (string Name, Formation F)[]
        {
            ("試遊・死の連鎖", Presets.DeathChain377()), ("試遊・死 探索1位", Presets.DeathSeek380()),
            ("試遊・死 ソム", Presets.DeathSom379()), ("試遊・死 エレ", Presets.DeathBard382(UnitCatalog.Ere)),
        };
        var ws = new[] { 0, 1, 2, 5, 7 };
        var t = new S377.B[boards.Length, 2, ws.Length];
        Parallel.For(0, t.Length, k =>
        {
            int bi = k / (2 * ws.Length), vi = k / ws.Length % 2, wi = k % ws.Length;
            var f = vi == 0 ? Pin386(boards[bi].F) : boards[bi].F;
            t[bi, vi, wi] = S377.Measure(f, W8[ws[wi]].Ws, Seeds, S386.Extra);
        });
        Console.WriteLine($"## 2. ムグのいる試遊の台（旧 → 今・seed 0..{Seeds - 1}）——勝率・決着T（勝った戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", ws.Select(w => W8[w].Name)) + " | 胞子の毒（勇者・1戦）| 弾けた（ボス・1戦）|");
        Console.WriteLine("|---|" + string.Concat(ws.Select(_ => "---|")) + "--:|--:|");
        for (int bi = 0; bi < boards.Length; bi++)
        {
            var o = t[bi, 0, 0]; var n = t[bi, 1, 0];
            Console.WriteLine($"| {boards[bi].Name}（{S307.Seats(boards[bi].F)}）| {string.Join(" | ", Enumerable.Range(0, ws.Length).Select(wi => $"{F1(t[bi, 0, wi].Win)}・{t[bi, 0, wi].WinTs}T → {F1(t[bi, 1, wi].Win)}・{t[bi, 1, wi].WinTs}T"))} | {Per(o.X.GetValueOrDefault("burstHero"), o.N)} → {Per(n.X.GetValueOrDefault("burstHero"), n.N)} | {Per(o.X.GetValueOrDefault("bursts"), o.N)} → {Per(n.X.GetValueOrDefault("bursts"), n.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 2'. 誰が何Tに倒れたか（ボス・今）");
        Console.WriteLine();
        for (int bi = 0; bi < boards.Length; bi++) Console.WriteLine($"- {boards[bi].Name}: 旧 {t[bi, 0, 0].FellCell} ／ 今 {t[bi, 1, 0].FellCell}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-2 の 1 ／ 2
    // ---------------------------------------------------------------------------------
    static void Use()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds();
        var g = CGrid(rows, CompareSeeds);
        int nw = EnemyCatalog.Stages.Count;
        Console.WriteLine($"# {Tag} §4-2 今どこで使われているか —— `compare` {rows.Length} 行のうち 10 枚のいる行");
        Console.WriteLine();
        Console.WriteLine($"素体 ＝ 同じ体（HP ／ 攻 ／ 速 ／ 型 ／ 踏込）・札なし・ふつうに殴る（器具の中だけ）。差 ＝ 素体 − 規定（本編 第2〜5波・seed 0..{Seeds - 1}・負なら駒が台を運んでいる）。");
        Console.WriteLine();
        foreach (var d in Ten)
        {
            var mine = Enumerable.Range(0, rows.Length).Where(ri => Has(rows[ri].F, d.Id)).ToArray();
            Console.WriteLine($"## {Short(d)}（`compare` {mine.Length} 行）");
            Console.WriteLine();
            if (mine.Length == 0) { Console.WriteLine("- `compare` の行に居ない。"); Console.WriteLine(); continue; }
            var plainRows = mine.Select(ri => (rows[ri].Name, Replace(rows[ri].F, d.Id, Plain(d)))).ToArray();
            var b100 = CGrid(mine.Select(ri => rows[ri]).ToArray(), Seeds, 1);
            var p100 = CGrid(plainRows, Seeds, 1);
            Console.WriteLine("| 行 | 席 | 規定 第1〜5波（seed 0..199）| 素体 第2〜5波（seed 0..99）| 差 第2 ／ 3 ／ 4 ／ 5波 | 差の平均 |");
            Console.WriteLine("|---|---|---|---|---|--:|");
            double sum = 0; int n = 0;
            for (int k = 0; k < mine.Length; k++)
            {
                int ri = mine[k];
                string seat = string.Join("・", rows[ri].F.Occupied().Where(o => o.Def.Id == d.Id).Select(o => SeatName(o.Slot)));
                var dif = Enumerable.Range(1, nw - 1).Select(w => p100[k, w] - b100[k, w]).ToArray();
                sum += dif.Sum(); n += dif.Length;
                Console.WriteLine($"| {rows[ri].Name} | {seat} | {string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w])))} | {string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(p100[k, w])))} | {string.Join(" ／ ", dif.Select(Sg))} | {Sg(dif.Average())} |");
            }
            Console.WriteLine();
            Console.WriteLine($"- 素体 − 規定の平均（{mine.Length} 行 × 4 波）: **{Sg(sum / n)}pt**。");
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 段1 の格子（試遊の台 × 席 × 駒・速い勝率）
    // ---------------------------------------------------------------------------------
    /// <summary>台ごとの段1（4 波 ＋ 強い条件・seed 0..9）。<c>r[u][b][seat]</c> は 4 波の勝率 ＋ 強い条件の勝率（その駒が元から居る台は null）。</summary>
    static (double[][] Base, double[]?[][][] Grid) Stage1((string Name, Formation F)[] boards, UnitDef[] units, bool strong)
    {
        int nb = boards.Length, nv = W4.Length + (strong ? 1 : 0);
        var bs = new double[nb][];
        var grid = new double[]?[units.Length][][];
        for (int u = 0; u < units.Length; u++) { grid[u] = new double[]?[nb][]; for (int b = 0; b < nb; b++) grid[u][b] = new double[]?[5]; }
        var jobs = new List<(int U, int B, int S)>();
        for (int b = 0; b < nb; b++) jobs.Add((-1, b, -1));
        for (int u = 0; u < units.Length; u++)
            for (int b = 0; b < nb; b++)
                if (!Has(boards[b].F, units[u].Id)) foreach (int s in Seats5(boards[b].F)) jobs.Add((u, b, s));
        Parallel.ForEach(jobs, j =>
        {
            var f = j.U < 0 ? boards[j.B].F : Put(boards[j.B].F, j.S, units[j.U]);
            var v = new double[nv];
            for (int w = 0; w < W4.Length; w++) v[w] = WinRate(f, new[] { W4[w] }, S1Seeds);
            if (strong) v[W4.Length] = WinRate(f, I.StrongWaves, S1Seeds);
            if (j.U < 0) bs[j.B] = v; else grid[j.U][j.B][j.S] = v;
        });
        return (bs, grid);
    }

    // ---------------------------------------------------------------------------------
    // §4-3 今の基準で戦えるか
    // ---------------------------------------------------------------------------------
    static void Swap()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Play();
        int nb = boards.Length;
        var (bs, grid) = Stage1(boards, Ten, strong: true);
        Console.WriteLine($"# {Tag} §4-3 今の基準で戦えるか —— 試遊の台 {nb} × 1枚を 10 枚に替えた台");
        Console.WriteLine();
        Console.WriteLine("試遊の台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。");
        Console.WriteLine();
        Console.WriteLine($"段1: seed 0..{S1Seeds - 1} × {string.Join(" ／ ", W4Names)}（＋ 強い条件 第2〜5波は並べ替えの2番目の鍵）。替えた席にそのまま置く・その駒が元から居る台は替えない。");
        Console.WriteLine($"段2: 駒ごとに、段1 の「元の台との差（4 波の平均）→ 強い条件の差」で上位 3 台を seed 0..{Seeds - 1} × 8 物差し。");
        Console.WriteLine();

        // ---- 段1 の要約 ----
        Console.WriteLine("## 1. 段1 の要約（駒ごと・替えた台の数 ／ 元の台より +3pt 以上上がった台の数・seed 0..9 なので目安）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 替えた台 | 4 波で上がった | 強い条件で上がった | どちらも | 4 波の差の平均 | 強い条件の差の平均 | 元から居る台 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");
        var picks = new List<(int U, int B, int S, double D4, double DS)>[Ten.Length];
        for (int u = 0; u < Ten.Length; u++)
        {
            var all = new List<(int U, int B, int S, double D4, double DS)>();
            for (int b = 0; b < nb; b++)
                for (int s = 0; s < 5; s++)
                    if (grid[u][b][s] is { } v)
                        all.Add((u, b, s, Enumerable.Range(0, W4.Length).Average(w => v[w] - bs[b][w]), v[W4.Length] - bs[b][W4.Length]));
            picks[u] = all.OrderByDescending(x => x.D4).ThenByDescending(x => x.DS).ThenBy(x => x.B).ThenBy(x => x.S).ToList();
            string home = string.Join("・", Enumerable.Range(0, nb).Where(b => Has(boards[b].F, Ten[u].Id)).Select(b => boards[b].Name));
            Console.WriteLine($"| {Short(Ten[u])} | {all.Count} | {all.Count(x => x.D4 >= Up)} | {all.Count(x => x.DS >= Up)} | {all.Count(x => x.D4 >= Up && x.DS >= Up)} | {Sg(all.Average(x => x.D4))} | {Sg(all.Average(x => x.DS))} | {(home == "" ? "—" : home)} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 1'. 台ごとの元の値（段1・seed 0..9）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", W4Names) + " | 強い条件 |");
        Console.WriteLine("|---|" + string.Concat(W4Names.Select(_ => "--:|")) + "--:|");
        for (int b = 0; b < nb; b++) Console.WriteLine($"| {boards[b].Name} | {string.Join(" | ", bs[b].Select(F1))} |");
        Console.WriteLine();

        // ---- 段2 ----
        var top = picks.Select(l => l.Take(3).ToList()).ToArray();
        var need = top.SelectMany(l => l.Select(x => x.B)).Distinct().ToArray();
        var baseM = new Dictionary<int, S377.B[]>();
        foreach (int b in need) baseM[b] = M8(boards[b].F);
        var res = new S377.B[Ten.Length, 3][];
        var pln = new S377.B[Ten.Length, 3][];   // 同じ席に素体を置いた台（札の働きの対照）
        for (int u = 0; u < Ten.Length; u++)
            for (int k = 0; k < top[u].Count; k++)
            {
                var x = top[u][k];
                res[u, k] = M8(Put(boards[x.B].F, x.S, Ten[u]), WorkExtra(Ten[u]));
                pln[u, k] = M8(Put(boards[x.B].F, x.S, Plain(Ten[u])));
            }

        Console.WriteLine($"## 2. 段2 —— 駒ごとの最良 3 台（seed 0..{Seeds - 1}・差 ＝ 替えた台 − 元の台・pt）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 順 | 台 | 替えた席（誰を）| " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波の差 | 素体を置いた差（4 波 ／ 強い条件）| 札の働き（1戦・ボス ／ 近衛）|");
        Console.WriteLine("|---|--:|---|---|" + string.Concat(W8.Select(_ => "--:|")) + "--:|---|---|");
        var up4 = new bool[Ten.Length]; var upS = new bool[Ten.Length]; var up4c = new bool[Ten.Length]; var upSc = new bool[Ten.Length];
        for (int u = 0; u < Ten.Length; u++)
        {
            var wk = WorkOf(Ten[u].Id);
            for (int k = 0; k < top[u].Count; k++)
            {
                var x = top[u][k]; var a = res[u, k]; var b0 = baseM[x.B];
                double d4 = D4(a, b0), ds = a[W8Strong].Win - b0[W8Strong].Win;
                if (d4 >= Up) up4[u] = true;
                if (ds >= Up) upS[u] = true;
                if (d4 >= Up && D4(a, pln[u, k]) >= Up) up4c[u] = true;
                if (ds >= Up && a[W8Strong].Win - pln[u, k][W8Strong].Win >= Up) upSc[u] = true;
                string pl = $"{Sg(D4(pln[u, k], b0))} ／ {Sg(pln[u, k][W8Strong].Win - b0[W8Strong].Win)}（駒 − 素体 {Sg(d4 - D4(pln[u, k], b0))} ／ {Sg(a[W8Strong].Win - pln[u, k][W8Strong].Win)}）";
                string work = $"{wk.A} {Per(a[0].X.GetValueOrDefault("wA"), a[0].N)} ／ {Per(a[1].X.GetValueOrDefault("wA"), a[1].N)}・{wk.B} {Per(a[0].X.GetValueOrDefault("wB"), a[0].N)} ／ {Per(a[1].X.GetValueOrDefault("wB"), a[1].N)}";
                Console.WriteLine($"| {(k == 0 ? Short(Ten[u]) : "")} | {k + 1} | {boards[x.B].Name} | {SeatName(x.S)}（{Short(boards[x.B].F[x.S]!)}）| {string.Join(" | ", Enumerable.Range(0, W8.Length).Select(wi => $"{F1(a[wi].Win)}（{Sg(a[wi].Win - b0[wi].Win)}）"))} | {Sg(d4)} | {pl} | {work} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("### 2'. 元の台（段2 で使った台・seed 0..99）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", W8.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(W8.Select(_ => "--:|")));
        foreach (int b in need) Console.WriteLine($"| {boards[b].Name} | {string.Join(" | ", baseM[b].Select(x => $"{F1(x.Win)}・{x.WinTs}T"))} |");
        Console.WriteLine();
        Console.WriteLine($"## 3. 元の台より上がる台があったか（段2 の上位 3 台・seed 0..99・+{F1(Up)}pt 以上を「上がった」）");
        Console.WriteLine();
        Console.WriteLine("「素体より上」＝ 同じ台・同じ席に素体を置いた台よりも +3pt 以上上（上がった分が札の働きか、席を空けたことの分かを分ける）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 4 波（ボス ／ 近衛 ／ 大隊 ／ 第5波の平均）| うち素体より上 | 強い条件 | うち素体より上 |");
        Console.WriteLine("|---|---|---|---|---|");
        string Yn(bool b) => b ? "上がった" : "無い";
        for (int u = 0; u < Ten.Length; u++) Console.WriteLine($"| {Short(Ten[u])} | {Yn(up4[u])} | {(up4[u] ? (up4c[u] ? "上" : "素体と同じか下") : "—")} | {Yn(upS[u])} | {(upS[u] ? (upSc[u] ? "上" : "素体と同じか下") : "—")} |");
        Console.WriteLine();
        Console.WriteLine("## 4. 誰が何Tに倒れたか（駒ごとの最良の台・ボス ／ 近衛・元の台 → 替えた台）");
        Console.WriteLine();
        for (int u = 0; u < Ten.Length; u++)
        {
            if (top[u].Count == 0) continue;
            var x = top[u][0]; var a = res[u, 0]; var b0 = baseM[x.B];
            Console.WriteLine($"- **{Short(Ten[u])}**（{boards[x.B].Name} の {SeatName(x.S)} の {Short(boards[x.B].F[x.S]!)} を替えた）");
            Console.WriteLine($"  - ボス: 元 {F1(b0[0].Win)}%・{b0[0].WinTs}T — {b0[0].FellCell}");
            Console.WriteLine($"  - ボス: 替 {F1(a[0].Win)}%・{a[0].WinTs}T — {a[0].FellCell}");
            Console.WriteLine($"  - 近衛: 元 {F1(b0[1].Win)}%・{b0[1].WinTs}T — {b0[1].FellCell}");
            Console.WriteLine($"  - 近衛: 替 {F1(a[1].Win)}%・{a[1].WinTs}T — {a[1].FellCell}");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-4 役割の重なり
    // ---------------------------------------------------------------------------------
    /// <summary>§4-4 の替え合い（A を B に替える）。第348期の重なり ＋ 指示書の例。</summary>
    static readonly (string A, string B)[] Pairs348 = { ("sekki", "golm"), ("golm", "sekki"), ("tomo", "kari"), ("kari", "tomo"), ("kubi", "ban"), ("ban", "kubi") };

    static void Overlap()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Play();
        var roster = UnitCatalog.All.ToArray();
        var (bs, grid) = Stage1(boards, roster, strong: false);
        int nb = boards.Length;
        // 駒ごとの差の並び（席 × 波）
        double?[] Vec(int u)
        {
            var v = new List<double?>();
            for (int b = 0; b < nb; b++) for (int s = 0; s < 5; s++) for (int w = 0; w < W4.Length; w++)
                        v.Add(grid[u][b][s] is { } x ? x[w] - bs[b][w] : null);
            return v.ToArray();
        }
        var raw = Enumerable.Range(0, roster.Length).Select(Vec).ToArray();
        // 席ごとの平均（52 枚）を引いた残り——「どの駒を置いても落ちる席 ／ 上限に張り付いた台」の共通の分を除く
        int len = raw[0].Length;
        var mean = Enumerable.Range(0, len).Select(i => raw.Where(v => v[i] is not null).Select(v => v[i]!.Value).DefaultIfEmpty(0).Average()).ToArray();
        var vecs = raw.Select(v => v.Select((x, i) => x is double d ? d - mean[i] : (double?)null).ToArray()).ToArray();
        (double R, double Mad, int N) Sim(int a, int b)
        {
            var xs = new List<(double X, double Y)>();
            for (int i = 0; i < vecs[a].Length; i++) if (vecs[a][i] is double x && vecs[b][i] is double y) xs.Add((x, y));
            if (xs.Count < 8) return (double.NaN, double.NaN, xs.Count);
            double mx = xs.Average(p => p.X), my = xs.Average(p => p.Y);
            double sxy = xs.Sum(p => (p.X - mx) * (p.Y - my)), sxx = xs.Sum(p => (p.X - mx) * (p.X - mx)), syy = xs.Sum(p => (p.Y - my) * (p.Y - my));
            double r = sxx == 0 || syy == 0 ? double.NaN : sxy / Math.Sqrt(sxx * syy);
            return (r, xs.Average(p => Math.Abs(p.X - p.Y)), xs.Count);
        }
        Console.WriteLine($"# {Tag} §4-4 役割の重なり —— ロスター {roster.Length} 枚 × 試遊の台 {nb} の席（seed 0..{S1Seeds - 1} × {string.Join(" ／ ", W4Names)}）");
        Console.WriteLine();
        Console.WriteLine("同じ席に替えたときの「元の台との差」の並び（台 × 席 × 4 波）から**席ごとの 52 枚の平均を引いた残り**が似ている駒を近い駒とする（r ＝ 残りの相関・差 ＝ 同じ席の差の差の絶対値の平均 pt）。どちらかが元から居る台は除く。**目安**（seed 0..9）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 近い駒（r ／ 差）1 | 2 | 3 | 差がいちばん小さい駒 |");
        Console.WriteLine("|---|---|---|---|---|");
        var tenIx = Ten.Select(d => Array.FindIndex(roster, x => x.Id == d.Id)).ToArray();
        var near = new Dictionary<string, string>();
        foreach (int a in tenIx)
        {
            var sims = Enumerable.Range(0, roster.Length).Where(b => b != a).Select(b => (B: b, S: Sim(a, b))).Where(x => !double.IsNaN(x.S.R)).ToList();
            var byR = sims.OrderByDescending(x => x.S.R).Take(3).ToList();
            var byM = sims.OrderBy(x => x.S.Mad).First();
            near[roster[a].Id] = roster[byR[0].B].Id;
            Console.WriteLine($"| {Short(roster[a])} | {string.Join(" | ", byR.Select(x => $"{Short(roster[x.B])}（{x.S.R:F2} ／ {F1(x.S.Mad)}）"))} | {Short(roster[byM.B])}（{byM.S.R:F2} ／ {F1(byM.S.Mad)}）|");
        }
        Console.WriteLine();

        // ---- 替え合い ----
        var pairs = Pairs348.Concat(Ten.Select(d => (d.Id, near[d.Id]))).Distinct().ToArray();
        var rows = CompareBuilds();
        Console.WriteLine($"## 替え合い（A のいる `compare` の行 ／ 試遊の台で、A を B に替えた差・本編 第2〜5波 ／ 試遊の台は 4 波・seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| A → B | `compare` の行（行の数・差の平均 pt・最も落ちた行）| 試遊の台（台の数・差の平均 pt）|");
        Console.WriteLine("|---|---|---|");
        foreach (var (a, b) in pairs)
        {
            var da = UnitCatalog.Everyone.First(d => d.Id == a); var db = UnitCatalog.Everyone.First(d => d.Id == b);
            var rs = rows.Where(r => Has(r.F, a) && !Has(r.F, b)).ToArray();
            string cmp = "—";
            if (rs.Length > 0)
            {
                var g0 = CGrid(rs, Seeds, 1); var g1 = CGrid(rs.Select(r => (r.Name, Replace(r.F, a, db))).ToArray(), Seeds, 1);
                var per = Enumerable.Range(0, rs.Length).Select(ri => Enumerable.Range(1, 4).Average(w => g1[ri, w] - g0[ri, w])).ToArray();
                int worst = Array.IndexOf(per, per.Min());
                cmp = $"{rs.Length} 行・{Sg(per.Average())}・{rs[worst].Name} {Sg(per[worst])}";
            }
            var ps = boards.Where(r => Has(r.F, a) && !Has(r.F, b)).ToArray();
            string play = "—";
            if (ps.Length > 0)
            {
                var d = new double[ps.Length];
                Parallel.For(0, ps.Length, i => d[i] = Enumerable.Range(0, W4.Length).Average(w => WinRate(Replace(ps[i].F, a, db), new[] { W4[w] }, Seeds) - WinRate(ps[i].F, new[] { W4[w] }, Seeds)));
                play = $"{ps.Length} 台・{Sg(d.Average())}（{string.Join("・", ps.Select((p, i) => $"{p.Name} {Sg(d[i])}"))}）";
            }
            Console.WriteLine($"| {Short(da)} → {Short(db)} | {cmp} | {play} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-5 ドルガ ／ ガンの台
    // ---------------------------------------------------------------------------------
    static void DolgaGan()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().Where(r => Has(r.F, "dolga") || Has(r.F, "gan")).Select(r => ("行・" + r.Name, r.F)).ToArray();
        var play = Play();
        var all = rows.Concat(play.Select(p => (p.Name, p.F))).ToArray();
        var ws = new[] { 0, 1, 2, 7 };
        var t = new S377.B[all.Length, ws.Length];
        Parallel.For(0, t.Length, k => t[k / ws.Length, k % ws.Length] = S377.Measure(all[k / ws.Length].Item2, W8[ws[k % ws.Length]].Ws, Seeds));
        Console.WriteLine($"# {Tag} §4-5 ドルガ ／ ガンの台 —— `compare` の行 {rows.Length} ＋ 試遊の台 {play.Length} × {string.Join(" ／ ", ws.Select(w => W8[w].Name))}（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 並び | ドルガ ／ ガン | " + string.Join(" | ", ws.Select(w => W8[w].Name)) + " | 4 物差しの平均 |");
        Console.WriteLine("|---|---|---|" + string.Concat(ws.Select(_ => "--:|")) + "--:|");
        for (int i = 0; i < all.Length; i++)
        {
            var f = all[i].Item2;
            string dg = string.Join("・", new[] { ("dolga", "ドルガ"), ("gan", "ガン") }.Where(x => Has(f, x.Item1)).Select(x => x.Item2));
            Console.WriteLine($"| {all[i].Item1} | {S307.Seats(f)} | {(dg == "" ? "—" : dg)} | {string.Join(" | ", Enumerable.Range(0, ws.Length).Select(wi => $"{F1(t[i, wi].Win)}・{t[i, wi].WinTs}T"))} | {F1(Enumerable.Range(0, ws.Length).Average(wi => t[i, wi].Win))} |");
        }
        Console.WriteLine();
        var withDG = Enumerable.Range(0, all.Length).Where(i => Has(all[i].Item2, "dolga") || Has(all[i].Item2, "gan")).ToArray();
        var playOther = Enumerable.Range(rows.Length, play.Length).Where(i => !withDG.Contains(i)).ToArray();
        string Avg(int[] set) => string.Join(" ／ ", Enumerable.Range(0, ws.Length).Select(wi => F1(set.Average(i => t[i, wi].Win))));
        Console.WriteLine($"- ドルガ ／ ガンのいる台（{withDG.Length} 台）の平均: {Avg(withDG)}。");
        Console.WriteLine($"- ドルガ ／ ガンのいない試遊の台（{playOther.Length} 台）の平均: {Avg(playOther)}。");
        var others = CompareBuilds().Where(r => !Has(r.F, "dolga") && !Has(r.F, "gan")).ToArray();
        var to = new S377.B[others.Length, ws.Length];
        Parallel.For(0, to.Length, k => to[k / ws.Length, k % ws.Length] = S377.Measure(others[k / ws.Length].F, W8[ws[k % ws.Length]].Ws, Seeds));
        Console.WriteLine($"- ドルガ ／ ガンのいない `compare` の行（{others.Length} 行）の平均: {string.Join(" ／ ", Enumerable.Range(0, ws.Length).Select(wi => F1(Enumerable.Range(0, others.Length).Average(i => to[i, wi].Win))))}（ドルガ ／ ガンのいる `compare` の行 {rows.Length} 行: {Avg(Enumerable.Range(0, rows.Length).ToArray())}）。");
        Console.WriteLine();
        Console.WriteLine("## 誰が何Tに倒れたか（ドルガ ／ ガンのいる台・ボス ／ 近衛）");
        Console.WriteLine();
        foreach (int i in withDG) Console.WriteLine($"- {all[i].Item1}: ボス {t[i, 0].FellCell} ／ 近衛 {t[i, 1].FellCell}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
