using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using S366 = Sh366Diag;
using S367 = Sh367Diag;

// =====================================================================================
// sh370 —— 第370期【軽い期】毒軸のループ②: 返り毒の向きを戻す（敵の毒が味方に跳ねる）／ ヴィオを「吸って攻撃力 → 攻撃力ぶんの毒」に（版 W1 ／ W2 ／ W3・対照 規定 ／ RCS）。
// 指示書は design/PHASE370_POISON_SPLASH_SPEC.md ／ 報告は design/PHASE370_POISON_SPLASH.md。規定は1つも変えない。
//
//     dotnet run --project BattleSim -c Release 0 sh370 boards               # §4: 6 台 × 版（規定 ／ RCS ／ W1 ／ W2 ／ W3）× 6 波（seed 0..99）・誰が何Tに倒れたか・ループのターンごと・勇者へ書いた層・跳ねた毒の行き先・壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh370 beni                 # §4-1: 毒+ベニ+ラウ × 第369期の Z1 から1つずつ戻す切り分け（勇者 ／ 蝕みなし・seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh370 compare <W1|W2|W3>   # `compare` 64 行 × 版（スィド ／ グザ ／ ヴィオのいる行だけを版で回す・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh370 check                # 自己検査
//
// 版の駒は `UnitCatalog.GuzaW` ／ `GuzaW3` ／ `VioW`（`All` に入れない）・スィドは第369期の `SidZ` ／ 第368期の `SidR`。攻撃力の台は試遊・毒のドルガ → ガン（決めたこと）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p370/predict.md`）。
// =====================================================================================
static partial class Sh370Diag
{
    internal const string PredictFile = ".tmp/p370/predict.md";
    const string Tag = "第370期";
    const int Seeds = 100, CompareSeeds = 200, LoopT = 8;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "boards" or "beni" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh370: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "boards": Boards(); return;
            case "beni": Beni(); return;
            case "compare": CompareVer(args.Length > 3 ? args[3] : "W2"); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh370: モードは boards / beni / compare <W1|W2|W3> / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Nm(string id) => Short(UnitCatalog.Everyone.First(d => d.Id == id));
    static string Sg(double x) => x.ToString("+0.0;-0.0;±0.0");
    static Formation Row(string n) => Pin371(CompareBuilds().First(r => r.Name == n).F);   // 第372期: 第371期までの規定（グザ ／ ヴィオ ／ スィド）に固定
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    // ---------------------------------------------------------------------------------
    // 版と台
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, UnitDef? Guza, UnitDef? Vio, UnitDef? Sid);
    internal static readonly Ver[] Vers =
    {
        new("規定", null, null, null),
        new("RCS（対照）", UnitCatalog.GuzaR, UnitCatalog.VioRCS, UnitCatalog.SidRC),
        new("W1 返り毒＋ヴィオ", UnitCatalog.GuzaW, UnitCatalog.VioW, UnitCatalog.SidR),
        new("W2 三人", UnitCatalog.GuzaW, UnitCatalog.VioW, UnitCatalog.SidZ),
        new("W3 上限なし", UnitCatalog.GuzaW3, UnitCatalog.VioW, UnitCatalog.SidZ),
    };
    internal static Formation Apply(Formation f, Ver v)
    {
        var g = f;
        if (v.Guza is not null) g = S316.SwapId(g, "guza", v.Guza);
        if (v.Vio is not null) g = S316.SwapId(g, "vio", v.Vio);
        if (v.Sid is not null) g = S316.SwapId(g, "sid", v.Sid);
        return g;
    }

    /// <summary>攻撃力の台: 試遊・毒のドルガ（後3）を鬨の号令ガンに替えた台（決めたこと: 後3 から後1 のヴィオへ届く攻撃力の上昇はガンの開戦の号令だけ——カリは隣接 ／ トモは最も遅い味方（グザ）に向く）。</summary>
    internal static Formation AtkBoard() => Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.SidH371, center: UnitCatalog.GuzaH371, back1: UnitCatalog.VioH371, back3: UnitCatalog.Gan);   // 第372期: 第371期までの規定に固定

    internal static (string Name, Formation F)[] BoardList() => new (string, Formation)[]
    {
        ("試遊・毒", Pin371(Presets.Versions.First(r => r.Name == "試遊・毒").F)),
        ("攻撃力の台（ガン）", AtkBoard()),
        ("毒特化・集団", Row(S367.BombRow)),
        ("澱み喰い (グザ×ヴィオ)", Row("澱み喰い (グザ×ヴィオ)")),
        ("毒+ベニ+ラウ", Row("毒+ベニ+ラウ")),
        ("毒 (グザ×ミオ×ラウ)", Row("毒 (グザ×ミオ×ラウ)")),
    };
    internal static readonly I.W[] Waves = S367.PWaves;
    static readonly string[] RouteCols = { "噴き出し", "瘴気", "ヴィオの毒", "スィドの返し ／ 毒撃", "スィドの吐き", "触媒", "ほか" };
    static int RouteCol(PoisonRoute? r) => r switch
    {
        PoisonRoute.Vent => 0, PoisonRoute.Miasma => 1, PoisonRoute.Strike => 2, PoisonRoute.Venom => 3, PoisonRoute.Spew => 4, PoisonRoute.Catalyst => 5, _ => 6,
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Agg
    {
        public long N, Wins, WinT, Win1, Draw, Fell;
        public readonly Dictionary<string, (long N, long TSum)> FellBy = new();
        public readonly long[] StageN = new long[4], StageT = new long[4];
        /// <summary>ターンごと（添字 1..LoopT）: 跳ねた層 ／ ヴィオの攻撃力（ターンの頭）／ ヴィオが乗せた毒 ／ スィドの層（ターンの頭）／ スィドの返し ／ 勇者の層（ターンの頭）と、それぞれ生きていたターン。</summary>
        public readonly long[] Splash = new long[LoopT + 1], VAtk = new long[LoopT + 1], VAlive = new long[LoopT + 1], Strike = new long[LoopT + 1],
            SLayer = new long[LoopT + 1], SAlive = new long[LoopT + 1], Retort = new long[LoopT + 1], HLayer = new long[LoopT + 1], HAlive = new long[LoopT + 1];
        public readonly long[] HWrote = new long[7];
        public long SplashAll, GotVio, GotSid, GotOther, Drained, Ticked, StrikeAll, VioWhet, RetortAll, Shouldered;
        /// <summary>第372期（`sh372` が読む・この器具の出力は変わらない）: ヴィオが撃った数（ターンごと）・2 ／ 3連撃に上がった戦 ／ ターンの和・攻撃力ぶんの毒を受けた敵の数 ／ 最も多く受けた敵の層（1戦）。</summary>
        public readonly long[] Hits = new long[LoopT + 1];
        public long Stage2N, Stage2T, Stage3N, Stage3T, StrikeTargets, StrikeTop;   // 第371期: Shouldered（スィドが引き受けた層・`sh371 seats` が読む）
        public void Add(Agg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Win1 += o.Win1; Draw += o.Draw; Fell += o.Fell;
            foreach (var (k, v) in o.FellBy) { var c = FellBy.GetValueOrDefault(k); FellBy[k] = (c.N + v.N, c.TSum + v.TSum); }
            for (int i = 0; i < 4; i++) { StageN[i] += o.StageN[i]; StageT[i] += o.StageT[i]; }
            for (int t = 0; t <= LoopT; t++)
            {
                Splash[t] += o.Splash[t]; VAtk[t] += o.VAtk[t]; VAlive[t] += o.VAlive[t]; Strike[t] += o.Strike[t];
                SLayer[t] += o.SLayer[t]; SAlive[t] += o.SAlive[t]; Retort[t] += o.Retort[t]; HLayer[t] += o.HLayer[t]; HAlive[t] += o.HAlive[t];
            }
            for (int i = 0; i < HWrote.Length; i++) HWrote[i] += o.HWrote[i];
            SplashAll += o.SplashAll; Shouldered += o.Shouldered;
            for (int t = 0; t <= LoopT; t++) Hits[t] += o.Hits[t];
            Stage2N += o.Stage2N; Stage2T += o.Stage2T; Stage3N += o.Stage3N; Stage3T += o.Stage3T; StrikeTargets += o.StrikeTargets; StrikeTop += o.StrikeTop; GotVio += o.GotVio; GotSid += o.GotSid; GotOther += o.GotOther; Drained += o.Drained; Ticked += o.Ticked; StrikeAll += o.StrikeAll; VioWhet += o.VioWhet; RetortAll += o.RetortAll;
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : F1((double)WinT / Wins);
        public string Cell => $"{F1(Win)}・{WinTs}T・倒 {Per(Fell, N)}";
        public string FellCell => FellBy.Count == 0 ? "—" : string.Join("・", FellBy.OrderByDescending(kv => kv.Value.N).Select(kv => $"{Nm(kv.Key)} {Pct(kv.Value.N, N)}%（T{F1((double)kv.Value.TSum / kv.Value.N)}）"));
        public string StageCell => string.Join(" ／ ", Enumerable.Range(1, 3).Select(i => StageN[i] == 0 ? "—" : $"T{F1((double)StageT[i] / StageN[i])}（{Pct(StageN[i], N)}%）"));
        public string Turns(long[] v, long[]? alive = null) => string.Join(" ", Enumerable.Range(1, LoopT).Select(t => alive is null ? Per(v[t], N) : alive[t] == 0 ? "—" : Per(v[t], alive[t])));
    }

    internal static Agg One(Formation f, I.W w, int seed)
    {
        var a = new Agg { N = 1 };
        var (r, p, e) = S367.Raw(f, w, seed, verbose: true);
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; if (r.Turns <= 1) a.Win1 = 1; }
        else if (r.PlayerSurvivors > 0) a.Draw = 1;
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { a.Fell++; var c = a.FellBy.GetValueOrDefault(u.Def.Id); a.FellBy[u.Def.Id] = (c.N + 1, c.TSum + u.LastDeathTurn); }
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!p.Any(u => u.Def.Id == id)) continue;
            if (id == "guza")
            {
                if (t.Z369StageTurn is { } st) for (int i = 1; i <= 3; i++) if (st[i] > 0) { a.StageN[i]++; a.StageT[i] += st[i]; }
                if (t.Z370SplashByTurn is { } sb) for (int k = 1; k <= LoopT; k++) a.Splash[k] += sb[k];
                a.SplashAll += t.Z370Splash;
            }
            if (id == "vio") { if (t.Z372HitsByTurn is { } hb) for (int k = 1; k <= LoopT; k++) a.Hits[k] += hb[k]; if (t.Z372Stage2Turn > 0) { a.Stage2N++; a.Stage2T += t.Z372Stage2Turn; } if (t.Z372Stage3Turn > 0) { a.Stage3N++; a.Stage3T += t.Z372Stage3Turn; } a.Drained += t.SpitStored; a.StrikeAll += t.Z370Strike; if (t.Z370StrikeByTurn is { } vb) for (int k = 1; k <= LoopT; k++) a.Strike[k] += vb[k]; a.GotVio += t.Z370SplashGot; }
            else if (id == "sid") { a.Shouldered += t.Z369Shouldered; a.GotSid += t.Z370SplashGot; a.RetortAll += t.Z369Retort; if (t.Z370RetortByTurn is { } rb) for (int k = 1; k <= LoopT; k++) a.Retort[k] += rb[k]; }
            else a.GotOther += t.Z370SplashGot;
        }
        var vio = p.FirstOrDefault(u => u.Def.Id == "vio"); var sid = p.FirstOrDefault(u => u.Def.Id == "sid");
        if (vio is not null) a.VioWhet = vio.WhetReceived;
        var mine = p.ToDictionary(u => u.InstanceId);
        var hero = w.Boss ? e.FirstOrDefault(u => u.HasTrait(TraitId.BossMend40)) : null;
        var strikeTo = new Dictionary<int, long>();   // 第372期: 攻撃力ぶんの毒を受けた敵ごと
        foreach (var x in r.Events)
        {
            if (x.Kind == BattleEventKind.StatusGain && x.PoisonRoute == PoisonRoute.Strike && x.TargetId is int st0) strikeTo[st0] = strikeTo.GetValueOrDefault(st0) + x.Amount;
            if (x.Turn is < 1 or > LoopT && x.Kind is BattleEventKind.StatusSnapshot or BattleEventKind.StatSnapshot) continue;
            switch (x.Kind)
            {
                case BattleEventKind.StatSnapshot when vio is not null && x.TargetId == vio.InstanceId:
                    a.VAtk[x.Turn] += x.Amount; a.VAlive[x.Turn] = 1;
                    break;
                case BattleEventKind.StatusSnapshot when x.Text == "毒" && sid is not null && x.TargetId == sid.InstanceId:
                    a.SLayer[x.Turn] += x.Amount;
                    break;
                case BattleEventKind.StatusSnapshot when x.Text == "毒" && hero is not null && x.TargetId == hero.InstanceId:
                    a.HLayer[x.Turn] += x.Amount;
                    break;
                case BattleEventKind.StatusGain when x.Text == StatusKeys.Poison && hero is not null && x.TargetId == hero.InstanceId:
                    a.HWrote[RouteCol(x.PoisonRoute)] += x.Amount;
                    break;
                case BattleEventKind.Status when x.Text == "毒" && x.InverterId is null && x.TargetId is int ti && mine.ContainsKey(ti):
                    a.Ticked += x.Amount;
                    break;
            }
        }
        a.StrikeTargets = strikeTo.Count; a.StrikeTop = strikeTo.Count == 0 ? 0 : strikeTo.Values.Max();
        if (sid is not null) { int last = sid.LastDeathTurn > 0 ? sid.LastDeathTurn : r.Turns; for (int t = 1; t <= Math.Min(LoopT, last); t++) a.SAlive[t] = 1; }
        if (hero is not null) { int last = hero.LastDeathTurn > 0 ? hero.LastDeathTurn : r.Turns; for (int t = 1; t <= Math.Min(LoopT, last); t++) a.HAlive[t] = 1; }
        return a;
    }

    internal static Agg Measure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Agg[n];
        Parallel.For(0, n, i => parts[i] = One(f, w, i));
        var all = new Agg();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    // ---------------------------------------------------------------------------------
    // §4 6台 × 版 × 波
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        Console.WriteLine($"# {Tag} §4 毒軸のループ② —— 6台 × 版 × 波（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。版はグザ ／ ヴィオ ／ スィドだけを替える（いない駒は替えない）。");
        Console.WriteLine();
        var grid = new Agg[boards.Length, Vers.Length, Waves.Length];
        Parallel.For(0, boards.Length * Vers.Length * Waves.Length, k =>
        {
            int bi = k / (Vers.Length * Waves.Length), vi = k / Waves.Length % Vers.Length, wi = k % Waves.Length;
            grid[bi, vi, wi] = Measure(Apply(boards[bi].F, Vers[vi]), Waves[wi]);
        });
        int bw = 0, nw = Waves.Length - 1;

        Console.WriteLine("## 1. 台 × 版 × 波（勝率・倒しT（勝った戦）・倒れた味方（1戦あたり））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < Vers.Length; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {Vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, Waves.Length).Select(wi => grid[bi, vi, wi].Cell))} |");
        Console.WriteLine();

        Console.WriteLine("## 2. 誰が何Tに倒れたか（駒 倒れた戦の割合%（倒れたターンの平均））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < Vers.Length; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {Vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, Waves.Length).Select(wi => grid[bi, vi, wi].FellCell))} |");
        Console.WriteLine();

        Console.WriteLine("## 3. ループが増幅しているか（ターンごと T1〜T8・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("跳ねた ＝ 返り毒で味方に跳ねた層（そのターン）。ヴィオの攻撃力 ＝ ターンの頭（`StatSnapshot`・生きていたターンの平均）。ヴィオの毒 ＝ 攻撃力ぶん乗せた毒（そのターン）。スィドの層 ＝ ターンの頭（生きていたターン）・返し ＝ 衣の返しで浴びせた層（そのターン）。勇者の層 ＝ ターンの頭（生きていたターン）。");
        Console.WriteLine();
        foreach (int bi in new[] { 0, 1, 2 })
            foreach (int wi in new[] { bw, nw })
            {
                Console.WriteLine($"### {boards[bi].Name} × {Waves[wi].Name}");
                Console.WriteLine();
                Console.WriteLine("| 版 | 段 1 ／ 2 ／ 3 | 跳ねた | ヴィオの攻撃力 | ヴィオの毒 | スィドの層 | スィドの返し | 勇者の層 |");
                Console.WriteLine("|---|---|---|---|---|---|---|---|");
                for (int vi = 0; vi < Vers.Length; vi++)
                {
                    var a = grid[bi, vi, wi];
                    Console.WriteLine($"| {Vers[vi].Name} | {a.StageCell} | {a.Turns(a.Splash)} | {a.Turns(a.VAtk, a.VAlive)} | {a.Turns(a.Strike)} | {a.Turns(a.SLayer, a.SAlive)} | {a.Turns(a.Retort)} | {a.Turns(a.HLayer, a.HAlive)} |");
                }
                Console.WriteLine();
            }

        Console.WriteLine("## 4. 勇者へ書いた層の内訳（1戦・`StatusGain` の経路・滲み込み）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | " + string.Join(" | ", RouteCols) + " |");
        Console.WriteLine("|---|---|---|" + string.Concat(RouteCols.Select(_ => "--:|")));
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < Vers.Length; vi++)
                foreach (int wi in new[] { bw, nw })
                {
                    var a = grid[bi, vi, wi];
                    Console.WriteLine($"| {(vi == 0 && wi == bw ? boards[bi].Name : "")} | {Vers[vi].Name} | {Waves[wi].Name} | {string.Join(" | ", a.HWrote.Select(v => Per(v, a.N)))} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 5. 跳ねた毒の行き先 ／ 攻撃力の上昇（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("跳ねた先 ＝ 跳ねた毒を受けた駒（ヴィオ ／ スィド ／ ほか）。ヴィオが吸った ＝ 澱み喰いが吸った層（跳ねた分 ＋ 噴き出し ＋ ほか）。毒に強くない駒の刻み ＝ 毒に強くない味方が刻まれた量（毒の出どころは問わない）。ヴィオの上昇（外から）＝ 号令などで外から届いた攻撃力（`WhetReceived`）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 跳ねた 計（ヴィオ ／ スィド ／ ほか） | ヴィオが吸った | 毒に強くない駒の刻み | ヴィオの毒 計 | ヴィオの上昇（外から） | スィドの返し 計 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < Vers.Length; vi++)
                foreach (int wi in new[] { bw, nw, 1, 4 })
                {
                    var a = grid[bi, vi, wi];
                    Console.WriteLine($"| {boards[bi].Name} | {Vers[vi].Name} | {Waves[wi].Name} | {Per(a.SplashAll, a.N)}（{Per(a.GotVio, a.N)} ／ {Per(a.GotSid, a.N)} ／ {Per(a.GotOther, a.N)}） | {Per(a.Drained, a.N)} | {Per(a.Ticked, a.N)} | {Per(a.StrikeAll, a.N)} | {Per(a.VioWhet, a.N)} | {Per(a.RetortAll, a.N)} |");
                }
        Console.WriteLine();

        var bad = new List<string>();
        for (int bi = 0; bi < boards.Length; bi++)
            for (int wi = 0; wi < Waves.Length; wi++)
            {
                var b = grid[bi, 0, wi];
                for (int vi = 1; vi < Vers.Length; vi++)
                {
                    var a = grid[bi, vi, wi]; var w = Waves[wi];
                    if ((w == I.Guard || w == I.Bat) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 1T 決着 {Pct(a.Win1, a.N)}%");
                    if (w.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4 && !(b.Wins > 0 && (double)b.WinT / b.Wins < 4)) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 倒しT {a.WinTs}（規定 {b.WinTs}）");
                    if (100.0 * (a.Draw - b.Draw) / a.N >= 5) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 30T の打ち切り {Pct(b.Draw, b.N)} → {Pct(a.Draw, a.N)}%");
                    if (b.Win - a.Win >= 15) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 勝率 {F1(b.Win)} → {F1(a.Win)}（参考・落ちた側）");
                }
            }
        Console.WriteLine("## 6. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満（規定で既に 4T 未満は除く）／ 30T の打ち切りが 5pt 以上増える ／ 参考: 勝率が規定から 15pt 以上落ちる）");
        Console.WriteLine();
        Console.WriteLine(bad.Count == 0 ? "なし。" : string.Join(Environment.NewLine, bad.Select(x => "- " + x)));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-1 ベニの台の切り分け
    // ---------------------------------------------------------------------------------
    /// <summary>器具の中だけの駒（札を足す ／ 外す）。`All` にも `UnitCatalog` にも入れない。</summary>
    internal static UnitDef With(UnitDef d, TraitId[] add, TraitId[] drop) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances, Pattern = d.Pattern, Actions = d.Actions,
        Traits = d.Traits.Where(t => !drop.Contains(t)).Concat(add).ToArray(), PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    internal static (string Name, Formation F)[] BeniCuts()
    {
        var b = Row("毒+ベニ+ラウ");
        Formation Z1(UnitDef guza, UnitDef sid) => S316.SwapId(S316.SwapId(b, "guza", guza), "sid", sid);
        var none = Array.Empty<TraitId>();
        return new (string, Formation)[]
        {
            ("規定", b),
            ("Z1（第369期）", Z1(UnitCatalog.GuzaZ, UnitCatalog.SidR)),
            ("(a) グザだけ規定に戻す", Z1(UnitCatalog.GuzaH371, UnitCatalog.SidR)),   // 第372期: 「規定」＝ 第371期までの規定
            ("(b) スィドの毒に強いだけ外す", Z1(UnitCatalog.GuzaZ, UnitCatalog.SidH371)),
            ("(c) グザの毒に強いだけ外す", Z1(With(UnitCatalog.GuzaZ, none, new[] { TraitId.PoisonProof }), UnitCatalog.SidR)),
            ("(d) 噴き出しだけ止める", Z1(With(UnitCatalog.GuzaZ, new[] { TraitId.VentOff370 }, none), UnitCatalog.SidR)),
            ("(e) 返り毒だけ止める", Z1(With(UnitCatalog.GuzaZ, new[] { TraitId.BacklashOff370 }, none), UnitCatalog.SidR)),
            ("(f) グザの味方全体への漏れを戻す（噴き出し ／ 返り毒 ／ 毒に強いはそのまま）", Z1(With(UnitCatalog.GuzaZ, new[] { TraitId.VentOff370, TraitId.BacklashOff370 }, new[] { TraitId.MiasmaVent }), UnitCatalog.SidR)),
        };
    }

    sealed class BA
    {
        public long N, Wins, WinT, Inv, Guren, GurenFires, Ticked, Leak;
        public readonly Dictionary<string, (long N, long TSum)> FellBy = new();
        public void Add(BA o) { N += o.N; Wins += o.Wins; WinT += o.WinT; Inv += o.Inv; Guren += o.Guren; GurenFires += o.GurenFires; Ticked += o.Ticked; Leak += o.Leak; foreach (var (k, v) in o.FellBy) { var c = FellBy.GetValueOrDefault(k); FellBy[k] = (c.N + v.N, c.TSum + v.TSum); } }
    }

    static BA BeniOne(Formation f, I.W w, int seed)
    {
        var a = new BA { N = 1 };
        var (r, p, _) = S367.Raw(f, w, seed, verbose: true);
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { var c = a.FellBy.GetValueOrDefault(u.Def.Id); a.FellBy[u.Def.Id] = (c.N + 1, c.TSum + u.LastDeathTurn); }
        if (r.TallyByUnit.TryGetValue("beni", out var bt)) { a.Inv = bt.InversePoisonHealed; a.Guren = bt.GurenGained; a.GurenFires = bt.GurenFires; }
        if (r.TallyByUnit.TryGetValue("guza", out var gt)) a.Leak = gt.Mia367ToAlly + gt.Z369VentAlly;
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        foreach (var x in r.Events) if (x.Kind == BattleEventKind.Status && x.Text == "毒" && x.InverterId is null && x.TargetId is int ti && mine.Contains(ti)) a.Ticked += x.Amount;
        return a;
    }

    static void Beni()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var cuts = BeniCuts();
        Console.WriteLine($"# {Tag} §4-1 ベニの台の切り分け —— 毒+ベニ+ラウ × 第369期の Z1 から1つずつ戻す（seed 0..{Seeds - 1}・器具の中だけの組み合わせ）");
        Console.WriteLine();
        Console.WriteLine("並び: " + S307.Seats(cuts[0].F) + "（ヴィオのいない台なので Z1 ＝ Z2）。反転で癒えた ＝ ベニの反転が毒の刻みを回復に変えて実際に癒えた量（`InversePoisonHealed`）。紅蓮 ＝ 溜めた紅蓮 ／ 奔流の回数。グザが味方へ書いた ＝ 瘴気の漏れ ＋ 噴き出し（名目）。刻み ＝ 反転されずに味方が刻まれた量。");
        Console.WriteLine("(f) は指示書に無い対照（決めたこと）——噴き出し ／ 返り毒を止めて、瘴気の味方全体への漏れだけを戻す（毒に強いは残す）。");
        Console.WriteLine();
        foreach (var w in new[] { I.Boss, S366.BossNoTax })
        {
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| 切り分け | 勝率・倒しT | 反転で癒えた | 紅蓮 ／ 奔流 | グザが味方へ書いた | 反転されない刻み | 誰が何Tに倒れたか |");
            Console.WriteLine("|---|---|--:|---|--:|--:|---|");
            foreach (var (name, f) in cuts)
            {
                var parts = new BA[Seeds];
                Parallel.For(0, Seeds, i => parts[i] = BeniOne(f, w, i));
                var a = new BA();
                foreach (var x in parts) a.Add(x);
                string fell = a.FellBy.Count == 0 ? "—" : string.Join("・", a.FellBy.OrderByDescending(kv => kv.Value.N).Select(kv => $"{Nm(kv.Key)} {Pct(kv.Value.N, a.N)}%（T{F1((double)kv.Value.TSum / kv.Value.N)}）"));
                Console.WriteLine($"| {name} | {Pct(a.Wins, a.N)}・{(a.Wins == 0 ? "—" : F1((double)a.WinT / a.Wins))}T | {Per(a.Inv, a.N)} | {Per(a.Guren, a.N)} ／ {Per(a.GurenFires, a.N)} | {Per(a.Leak, a.N)} | {Per(a.Ticked, a.N)} | {fell} |");
            }
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` × 版
    // ---------------------------------------------------------------------------------
    static double[,] CGrid((string Name, Formation F)[] rows, Func<Formation, Formation> map, Func<int, bool> only)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            if (!only(ri)) { g[ri, wi] = double.NaN; return; }
            var f = map(rows[ri].F);
            int wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        return g;
    }

    static void CompareVer(string ver)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var v = ver switch { "W1" => Vers[2], "W2" => Vers[3], "W3" => Vers[4], _ => throw new ArgumentException("sh370 compare の版は W1 / W2 / W3") };
        var rows = CompareBuilds().Select(r => (r.Name, F: Pin371(r.F))).ToArray();   // 第372期: 第371期までの規定に固定
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        Func<int, bool> only = ri => new[] { "sid", "guza", "vio" }.Any(id => Has(rows[ri].F, id));
        var b = CGrid(rows, f => f, _ => true);
        var g0 = CGrid(rows, f => Apply(f, v), only);
        var g = new double[rows.Length, nw];
        int touched = 0;
        for (int r = 0; r < rows.Length; r++) { if (only(r)) touched++; for (int w = 0; w < nw; w++) g[r, w] = only(r) ? g0[r, w] : b[r, w]; }
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] x) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => x[r, w]))));
        Console.WriteLine($"# {Tag} `compare` {rows.Length} 行 × 版 {v.Name}（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"版で回した行 {touched}（スィド ／ グザ ／ ヴィオのいる行・いない行は規定と同じ台 ＝ 同じ結果）。");
        Console.WriteLine();
        int upR = 0, upC = 0, dnR = 0, dnC = 0;
        var lines = new List<string>();
        for (int r = 0; r < rows.Length; r++)
        {
            bool u = false, d = false;
            for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
            if (u) upR++; if (d) dnR++;
            if (u || d) lines.Add($"{rows[r].Name}: " + string.Join("・", Enumerable.Range(1, nw - 1).Where(w => g[r, w] != b[r, w]).Select(w => $"第{w + 1}波 {F1(b[r, w])} → {F1(g[r, w])}")));
        }
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|--:|---|");
        Console.WriteLine($"| 規定 | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} |");
        Console.WriteLine($"| {v.Name} | {upR} ／ {upC} | {dnR} ／ {dnC} | {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} |");
        Console.WriteLine();
        foreach (var l in lines) Console.WriteLine("- " + l);
        for (int r = 0; r < rows.Length; r++)
        {
            if (!Enumerable.Range(1, nw - 1).Any(w => b[r, w] - g[r, w] >= 10.0)) continue;
            var parts = new List<string>();
            foreach (var id in rows[r].F.Occupied().Select(o => o.Def.Id).Distinct())
            {
                var oth = Enumerable.Range(0, rows.Length).Where(o => o != r && rows[o].F.Occupied().Any(x => x.Def.Id == id)).ToList();
                string nm = Short(rows[r].F.Occupied().First(o => o.Def.Id == id).Def);
                if (oth.Count == 0) { parts.Add($"{nm} 他の行 0（分解が成立しない）"); continue; }
                double dd = oth.Average(o => Enumerable.Range(1, nw - 1).Average(w => g[o, w] - b[o, w]));
                parts.Add($"{nm} {Sg(dd)}（{oth.Count} 行）");
            }
            Console.WriteLine($"- (G2) {rows[r].Name}: {string.Join("・", parts)}");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
