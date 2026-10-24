using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S390 = Sh390Diag;

// =====================================================================================
// sh400 —— 第400期 段0: トモの L1 を規定に ／ 本体: 手番の軸の演出のための出来事（`Slime` ／ `Wall` ／ `Lamp`・表示専用・盤面は変えない）。
// 指示書は design/PHASE400_TEMPO_SETTLE_SPEC.md、報告は design/PHASE400_TEMPO_SETTLE.md。
//
//     dotnet run --project BattleSim -c Release 0 sh400 zero       # §2: `compare` の動き（第399期の HEAD の表と ／ 第399期の L1 の列と）＋ 試遊の 4 台 × 5 物差し（規定 ／ 第399期）
//     dotnet run --project BattleSim -c Release 0 sh400 events     # §4: 手番の軸の試遊の 5 台 × ボス ／ 近衛 ／ 本編 第5波 × seed 0..19 の1戦あたりの出来事の回数 ＋ 単発の例（台 × ボス ／ 近衛 × seed 0）
//     dotnet run --project BattleSim -c Release 0 sh400 check      # 自己検査
//
// **盤面は変えない。** 足した出来事は verbose のときだけ積む。**予測のファイルが無ければ測定を走らせない**（`.tmp/p400/predict.md`）。
// =====================================================================================
static partial class Sh400Diag
{
    internal const string PredictFile = ".tmp/p400/predict.md";
    const string Tag = "第400期";
    const int Seeds = 100, EvSeeds = 20, CompareSeeds = 200;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "zero" or "events" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh400: 予測のファイル `{PredictFile}` がまだ無い。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "zero": Zero(); return;
            case "events": Events(); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh400: モードは zero / events / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;0.0");
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => string.Join(" ／ ", Enumerable.Range(0, 5).Select(s => f[s] is { } d ? Short(d) : "—"));
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static Formation Apply(Formation f, UnitDef tomo) { var g = f.Clone(); foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == "tomo" ? tomo : d; return g; }

    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    const int WBoss = 0, WGuard = 1, WBat = 2, WM5 = 5, WStrong = 7;

    /// <summary>手番の軸の試遊の台（§4）。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("試遊・手番 芯3枚", Presets.TempoLamp393(UnitCatalog.Tomo)), ("試遊・手番 包み", Presets.SlimeLamp396()), ("試遊・手番 縛め", Presets.BindTempo395()),
        ("試遊・手番 芯4枚", Presets.FourCore397()), ("試遊・手番 探索1位", Presets.TempoSeek392()),
    };
    static readonly (string Name, I.W W)[] EvWaves = { ("ボス", I.Boss), ("近衛", I.Guard), ("本編 第5波", I.M5) };

    // ---------------------------------------------------------------------------------
    // 1戦の出来事を数える
    // ---------------------------------------------------------------------------------
    /// <summary>数える欄（見出し ／ 1戦の出来事から数える関数）。</summary>
    internal static readonly (string Head, Func<BattleEvent, Dictionary<int, string>, bool> Hit)[] Cols =
    {
        ("セッキ 包む", (x, _) => x.Kind == BattleEventKind.Slime && x.Text == SlimeLabels.Wrap),
        ("膨らむ", (x, _) => x.Kind == BattleEventKind.Slime && x.Text == SlimeLabels.Swell),
        ("弾き返す", (x, _) => x.Kind == BattleEventKind.Slime && x.Text == SlimeLabels.Burst),
        ("ぷるぷる", (x, _) => x.Kind == BattleEventKind.Slime && x.Text == SlimeLabels.Idle),
        ("ドルガ 構える", (x, _) => x.Kind == BattleEventKind.Wall && x.Text == WallLabels.Rest),
        ("受ける", (x, _) => x.Kind == BattleEventKind.Wall && x.Text == WallLabels.Take),
        ("下がる ／ 戻る", (x, _) => x.Kind == BattleEventKind.Wall && x.Text is WallLabels.Down or WallLabels.Up),
        ("集める", (x, _) => x.Kind == BattleEventKind.Wall && x.Text == WallLabels.Focus),
        ("重い", (x, _) => x.Kind == BattleEventKind.Wall && x.Text == WallLabels.Heavy),
        ("トモ 灯す", (x, _) => x.Kind == BattleEventKind.Lamp && x.Text == LampLabels.Light),
        ("消える", (x, _) => x.Kind == BattleEventKind.Lamp && x.Text == LampLabels.Douse),
        ("譲る（倒れた）", (x, _) => x.Kind == BattleEventKind.Lamp && x.Text == LampLabels.Yield && x.Slot == LampLabels.ForDeath),
        ("譲る（止まっていた）", (x, _) => x.Kind == BattleEventKind.Lamp && x.Text == LampLabels.Yield && x.Slot == LampLabels.ForIdle),
        ("ガン +4 ／ +8", (x, id) => x.Kind == BattleEventKind.Whet && x.WhetRoute is WhetRoute.RallyOpening or WhetRoute.RallyTurn && x.ActorId is int a && id.GetValueOrDefault(a) == "gan"),
        ("叩き起こし", (x, id) => x.Kind == BattleEventKind.Reveille && x.Text == ReveilleLabels.Woken && x.ActorId is int a && id.GetValueOrDefault(a) == "gan"),
    };

    internal static (BattleResult R, Dictionary<int, string> Id) One(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var id = p.Concat(e).ToDictionary(u => u.InstanceId, u => u.Def.Id);
        return (r, id);
    }

    // ---------------------------------------------------------------------------------
    // §2: 段0
    // ---------------------------------------------------------------------------------
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

    static void Zero()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count;
        var now = CGrid(rows, CompareSeeds);
        var l1 = CGrid(rows.Select(r => (r.Name, Has(r.F, "tomo") ? Apply(Pin399(r.F), UnitCatalog.TomoL1) : r.F)).ToArray(), CompareSeeds);
        var old = CGrid(rows.Select(r => (r.Name, Pin399(r.F))).ToArray(), CompareSeeds);
        var map = S390.BalanceMap(S390.GitShow($"{HeadSha}:docs/balance.md"));
        int moved = 0, same = 0, cells = 0, oldSame = 0;
        var movedRows = new List<string>();
        for (int ri = 0; ri < rows.Length; ri++)
        {
            var cc = map[rows[ri].Name]; var parts = new List<string>();
            for (int w = 0; w < nw; w++)
            {
                cells++;
                if (now[ri, w] == l1[ri, w]) same++;
                double o = double.Parse(cc[w].TrimEnd('%'));
                if (o == old[ri, w]) oldSame++;
                if (o != now[ri, w]) { moved++; parts.Add($"第{w + 1}波 {F1(o)} → {F1(now[ri, w])}"); }
            }
            if (parts.Count > 0) movedRows.Add($"| {rows[ri].Name} | {string.Join("・", parts)} |");
        }
        Console.WriteLine($"# {Tag} §2 段0: トモの規定 ＝ 第399期の L1");
        Console.WriteLine();
        Console.WriteLine($"## 1. `compare`（全 {rows.Length} 行 × {nw} 波 × seed 0..{CompareSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"- 第399期の HEAD（{HeadSha}）の `docs/balance.md` と違うセル **{moved}**");
        Console.WriteLine($"- 第399期の L1 の列（トモを `TomoL1` に替えた全行）と同じセル {same} ／ {cells}・旧（`TomoH399`）に戻した全行が HEAD の表と同じセル {oldSame} ／ {cells}");
        Console.WriteLine($"- トモのいる行: {string.Join("・", rows.Where(r => Has(r.F, "tomo")).Select(r => r.Name))}");
        foreach (var l in movedRows) Console.WriteLine(l);
        Console.WriteLine();
        var boards = new (string Name, Formation F)[] { ("試遊・手番 芯3枚", Presets.TempoLamp393(UnitCatalog.Tomo)), ("試遊・手番 包み", Presets.SlimeLamp396()), ("試遊・手番 縛め", Presets.BindTempo395()), ("試遊・手番 芯4枚", Presets.FourCore397()) };
        var ws = new[] { WBoss, WGuard, WBat, WM5, WStrong };
        Console.WriteLine($"## 2. 試遊の 4 台 × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件（seed 0..{Seeds - 1}）・規定 ／ 第399期（トモを旧に）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", ws.Select(w => W8[w].Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(ws.Select(_ => "---|")));
        foreach (var bd in boards)
            foreach (var (cn, f) in new[] { ("規定（第400期）", bd.F), ("第399期（旧）", Pin399(bd.F)) })
            {
                var g = new S377.B[ws.Length]; Parallel.For(0, ws.Length, k => g[k] = S377.Measure(f, W8[ws[k]].Ws, Seeds));
                Console.WriteLine($"| {(cn.StartsWith("規定") ? $"{bd.Name}（{Seats(bd.F)}）" : "")} | {cn} | {string.Join(" | ", g.Select(x => x.Cell))} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4: 出来事の回数 ＋ 単発の例
    // ---------------------------------------------------------------------------------
    static void Events()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        Console.WriteLine($"# {Tag} §4 手番の軸の試遊の台 × 物差し × seed 0..{EvSeeds - 1}——1戦あたりの出来事の回数");
        Console.WriteLine();
        foreach (var (name, f) in boards) Console.WriteLine($"- **{name}**: {Seats(f)}");
        Console.WriteLine();
        Console.WriteLine("## 1. 1戦あたりの回数");
        Console.WriteLine();
        Console.WriteLine("| 台 | 物差し | 勝率 | " + string.Join(" | ", Cols.Select(c => c.Head)) + " |");
        Console.WriteLine("|---|---|--:|" + string.Concat(Cols.Select(_ => "--:|")));
        foreach (var (name, f) in boards)
            foreach (var (wn, w) in EvWaves)
            {
                var sum = new long[Cols.Length]; int wins = 0;
                var lk = new object();
                Parallel.For(0, EvSeeds, sd =>
                {
                    var (r, id) = One(f, w, sd);
                    var c = new long[Cols.Length];
                    foreach (var x in r.Events) for (int k = 0; k < Cols.Length; k++) if (Cols[k].Hit(x, id)) c[k]++;
                    lock (lk) { for (int k = 0; k < Cols.Length; k++) sum[k] += c[k]; if (r.PlayerWon) wins++; }
                });
                Console.WriteLine($"| {name} | {wn} | {F1(100.0 * wins / EvSeeds)} | {string.Join(" | ", sum.Select(v => F1((double)v / EvSeeds)))} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 2. 単発の再生の例（seed 0・ターンごとに「出来事 × 回数」）");
        Console.WriteLine();
        foreach (var (name, f) in boards)
            foreach (var (wn, w) in EvWaves.Where(x => x.Name != "本編 第5波"))
            {
                var (r, id) = One(f, w, 0);
                Console.WriteLine($"- **{name} × {wn} × seed 0**（{(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T）");
                foreach (var t in r.Events.Select(x => x.Turn).Distinct().OrderBy(t => t))
                {
                    var parts = Cols.Select((c, k) => (c.Head, N: r.Events.Count(x => x.Turn == t && c.Hit(x, id)))).Where(z => z.N > 0).Select(z => $"{z.Head} {z.N}").ToList();
                    if (parts.Count > 0) Console.WriteLine($"  - T{t}: {string.Join("・", parts)}");
                }
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
