using System.Reflection;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using S366 = Sh366Diag;
using S367 = Sh367Diag;
using S370 = Sh370Diag;

// =====================================================================================
// sh371 —— 第371期【軽い期】毒軸のループ③: ヴィオ型を伸ばす（W4 ＝ W1 ＋ 上限なし）／ スィドの引き受けをやめる（W5 ＝ W4 ＋ スィド Y）／ 取り合いは配置で解けるか（W2 ／ W5 の席）。
// 指示書は design/PHASE371_POISON_CARRIERS_SPEC.md ／ 報告は design/PHASE371_POISON_CARRIERS.md。規定は1つも変えない。
//
//     dotnet run --project BattleSim -c Release 0 sh371 boards               # §3-1: 5 台 × 版（規定 ／ W1 ／ W2 ／ W4 ／ W5）× 6 波（seed 0..99）・誰が何Tに倒れたか・ループの表・勇者へ書いた層・跳ねた毒の行き先・壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh371 seats                # §3-2: 試遊・毒の5枚の並べ替え 120 通り × W2 ／ W5 × ボス ／ 蝕みなし ／ 近衛（seed 0..19）
//     dotnet run --project BattleSim -c Release 0 sh371 compare <W4|W5>      # `compare` 64 行 × 版（スィド ／ グザ ／ ヴィオのいる行だけを版で回す・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh371 check                # 自己検査
//
// 1戦の計数は第370期の器具（`Sh370Diag.One` ／ `Measure` ／ `Agg`）をそのまま使う。版の駒は第370期の `GuzaW` ／ `GuzaW3` ／ `VioW`・第369期の `SidZ`・第368期の `SidR`・この期の `SidY`。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p371/predict.md`）。
// =====================================================================================
static partial class Sh371Diag
{
    internal const string PredictFile = ".tmp/p371/predict.md";
    const string Tag = "第371期";
    const int Seeds = 100, SeatSeeds = 20, CompareSeeds = 200, LoopT = 8;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "boards" or "seats" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh371: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "boards": Boards(); return;
            case "seats": Seats(); return;
            case "compare": CompareVer(args.Length > 3 ? args[3] : "W5"); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh371: モードは boards / seats / compare <W4|W5> / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Sg(double x) => x.ToString("+0.0;-0.0;±0.0");
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    // ---------------------------------------------------------------------------------
    // 版と台
    // ---------------------------------------------------------------------------------
    internal static readonly S370.Ver[] Vers =
    {
        new("規定", null, null, null),
        new("W1（対照）", UnitCatalog.GuzaW, UnitCatalog.VioW, UnitCatalog.SidR),
        new("W2（対照）", UnitCatalog.GuzaW, UnitCatalog.VioW, UnitCatalog.SidZ),
        new("W4 ヴィオ型", UnitCatalog.GuzaW3, UnitCatalog.VioW, UnitCatalog.SidR),
        new("W5 両立", UnitCatalog.GuzaW3, UnitCatalog.VioW, UnitCatalog.SidY),
    };
    internal static Formation Apply(Formation f, S370.Ver v) => S370.Apply(f, v);

    internal static (string Name, Formation F)[] BoardList() => new (string, Formation)[]
    {
        ("試遊・毒", Presets.Versions.First(r => r.Name == "試遊・毒").F),
        ("毒特化・集団", Row(S367.BombRow)),
        ("澱み喰い (グザ×ヴィオ)", Row("澱み喰い (グザ×ヴィオ)")),
        ("毒+ベニ+ラウ", Row("毒+ベニ+ラウ")),
        ("毒 (グザ×ミオ×ラウ)", Row("毒 (グザ×ミオ×ラウ)")),
    };
    internal static readonly I.W[] Waves = S367.PWaves;
    static readonly string[] RouteCols = { "噴き出し", "瘴気", "ヴィオの毒", "スィドの返し ／ 毒撃", "スィドの吐き", "触媒", "ほか" };

    // ---------------------------------------------------------------------------------
    // §3-1 5台 × 版 × 波
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        Console.WriteLine($"# {Tag} §3-1 毒軸のループ③ —— 5台 × 版 × 波（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。版はグザ ／ ヴィオ ／ スィドだけを替える（いない駒は替えない）。1戦の計数は `sh370` と同じ。");
        Console.WriteLine();
        var grid = new S370.Agg[boards.Length, Vers.Length, Waves.Length];
        var vmax = new long[boards.Length, Vers.Length, Waves.Length];
        Parallel.For(0, boards.Length * Vers.Length * Waves.Length, k =>
        {
            int bi = k / (Vers.Length * Waves.Length), vi = k / Waves.Length % Vers.Length, wi = k % Waves.Length;
            var f = Apply(boards[bi].F, Vers[vi]);
            var parts = new S370.Agg[Seeds]; var mx = new long[Seeds];
            Parallel.For(0, Seeds, s => { parts[s] = S370.One(f, Waves[wi], s); mx[s] = Enumerable.Range(1, LoopT).Select(t => parts[s].VAlive[t] > 0 ? parts[s].VAtk[t] : 0).Max(); });
            var all = new S370.Agg();
            foreach (var x in parts) all.Add(x);
            grid[bi, vi, wi] = all; vmax[bi, vi, wi] = mx.Max();
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

        Console.WriteLine("## 3. ループの表（ターンごと T1〜T8・1戦あたり・第370期 §4-3 と同じ形）");
        Console.WriteLine();
        foreach (int bi in new[] { 0, 1 })
            foreach (int wi in new[] { bw, nw })
            {
                Console.WriteLine($"### {boards[bi].Name} × {Waves[wi].Name}");
                Console.WriteLine();
                Console.WriteLine("| 版 | 段 1 ／ 2 ／ 3 | 跳ねた | ヴィオの攻撃力（頭） | ヴィオの毒 | スィドの層（頭） | スィドの返し | 勇者の層（頭） |");
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

        Console.WriteLine("## 5. 跳ねた毒の行き先（1戦あたり）・ヴィオの攻撃力の最大（その台 × 波の 100 戦で、ターンの頭の攻撃力の最大）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 跳ねた 計（ヴィオ ／ スィド ／ ほか） | ヴィオが吸った | スィドが引き受けた | 毒に強くない駒の刻み | ヴィオの毒 計 | スィドの返し 計 | ヴィオの攻撃力の最大 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < Vers.Length; vi++)
                foreach (int wi in new[] { bw, nw, 1, 4 })
                {
                    var a = grid[bi, vi, wi];
                    Console.WriteLine($"| {boards[bi].Name} | {Vers[vi].Name} | {Waves[wi].Name} | {Per(a.SplashAll, a.N)}（{Per(a.GotVio, a.N)} ／ {Per(a.GotSid, a.N)} ／ {Per(a.GotOther, a.N)}） | {Per(a.Drained, a.N)} | {Per(a.Shouldered, a.N)} | {Per(a.Ticked, a.N)} | {Per(a.StrikeAll, a.N)} | {Per(a.RetortAll, a.N)} | {vmax[bi, vi, wi]} |");
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
        Console.WriteLine("W4 ／ W5 の勇者の倒しT とヴィオの攻撃力の最大（§5 の最後の列）: " + string.Join("・", new[] { 3, 4 }.SelectMany(vi => new[] { bw, nw }.Select(wi => $"試遊・毒 × {Vers[vi].Name} × {Waves[wi].Name} {grid[0, vi, wi].WinTs}T ／ 最大 {vmax[0, vi, wi]}"))) + "。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-2 席の確かめ（試遊・毒の5枚の並べ替え 120 通り）
    // ---------------------------------------------------------------------------------
    internal static readonly I.W[] SeatWaves = { I.Boss, S366.BossNoTax, I.Guard };

    /// <summary>試遊・毒の5枚（規定の駒）を席 0..4 に置く並び。<paramref name="order"/>[席] ＝ 駒の添字。</summary>
    internal static IEnumerable<int[]> Perms(int n = 5)
    {
        var a = Enumerable.Range(0, n).ToArray();
        IEnumerable<int[]> Rec(int k)
        {
            if (k == n) { yield return a.ToArray(); yield break; }
            for (int i = k; i < n; i++)
            {
                (a[k], a[i]) = (a[i], a[k]);
                foreach (var p in Rec(k + 1)) yield return p;
                (a[k], a[i]) = (a[i], a[k]);
            }
        }
        return Rec(0);
    }

    internal static Formation Seat(int[] order, S370.Ver v)
    {
        var play = Presets.Versions.First(r => r.Name == "試遊・毒").F;
        var defs = Enumerable.Range(0, 5).Select(i => play[i]!).ToArray();
        var f = Formation.Build(front1: defs[order[0]], front3: defs[order[1]], center: defs[order[2]], back1: defs[order[3]], back3: defs[order[4]]);
        return Apply(f, v);
    }

    static void Seats()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var perms = Perms().ToArray();
        var vers = new[] { Vers[2], Vers[4] };
        Console.WriteLine($"# {Tag} §3-2 席の確かめ —— 試遊・毒の5枚（ガルド ／ スィド ／ グザ ／ ヴィオ ／ ドルガ）の並べ替え {perms.Length} 通り × {string.Join(" ／ ", vers.Select(v => v.Name))} × {string.Join(" ／ ", SeatWaves.Select(w => w.Name))}（seed 0..{SeatSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("並びは 前1・前3・中央・後1・後3 の順。隣接は X 字の表（前1 ↔ 中央 ／ 後1・前3 ↔ 中央 ／ 後3・中央 ↔ 四隅）。ヴィオが吸った ＝ 澱み喰いが吸った層（1戦）・スィドが引き受けた ＝ 引き受けの層（1戦・W2 だけ）・スィドの衣 T6 ＝ ターンの頭のスィドの層（T6 に生きていた戦の平均）。");
        Console.WriteLine();
        var res = new S370.Agg[vers.Length, perms.Length, SeatWaves.Length];
        Parallel.For(0, vers.Length * perms.Length * SeatWaves.Length, k =>
        {
            int vi = k / (perms.Length * SeatWaves.Length), pi = k / SeatWaves.Length % perms.Length, wi = k % SeatWaves.Length;
            res[vi, pi, wi] = S370.Measure(Seat(perms[pi], vers[vi]), SeatWaves[wi], SeatSeeds);
        });
        string Name(int pi) => S307.Seats(Seat(perms[pi], Vers[0]));
        double TOf(S370.Agg a) => a.Wins == 0 ? 99 : (double)a.WinT / a.Wins;

        for (int vi = 0; vi < vers.Length; vi++)
        {
            var order = Enumerable.Range(0, perms.Length).OrderByDescending(pi => res[vi, pi, 1].Win).ThenBy(pi => TOf(res[vi, pi, 1])).ThenByDescending(pi => res[vi, pi, 0].Win).ThenBy(pi => TOf(res[vi, pi, 0])).ToList();
            Console.WriteLine($"## {vers[vi].Name}: 上位5並び（蝕みなしの勇者の勝率 → 倒しT の順）");
            Console.WriteLine();
            Console.WriteLine("| 順位 | 並び | 蝕みなし | ボス | 近衛 | ヴィオが吸った（ボス） | スィドが引き受けた（ボス） | スィドの衣 T6（ボス） |");
            Console.WriteLine("|--:|---|---|---|---|--:|--:|--:|");
            foreach (var (pi, rank) in order.Take(5).Select((pi, i) => (pi, i + 1)))
            {
                var b = res[vi, pi, 0]; var nt = res[vi, pi, 1]; var g = res[vi, pi, 2];
                Console.WriteLine($"| {rank} | {Name(pi)} | {F1(nt.Win)}・{nt.WinTs}T | {F1(b.Win)}・{b.WinTs}T | {F1(g.Win)} | {Per(b.Drained, b.N)} | {Per(b.Shouldered, b.N)} | {(b.SAlive[6] == 0 ? "—" : Per(b.SLayer[6], b.SAlive[6]))} |");
            }
            int reach = Enumerable.Range(0, perms.Length).Count(pi => res[vi, pi, 1].Wins > 0), reach50 = Enumerable.Range(0, perms.Length).Count(pi => res[vi, pi, 1].Win >= 50);
            int bossAll = Enumerable.Range(0, perms.Length).Count(pi => res[vi, pi, 0].Win >= 100);
            int gBest = Enumerable.Range(0, perms.Length).OrderByDescending(pi => res[vi, pi, 2].Win).ThenBy(pi => TOf(res[vi, pi, 2])).First();
            int cur = Array.FindIndex(perms, p => p.SequenceEqual(new[] { 0, 1, 2, 3, 4 }));
            Console.WriteLine();
            Console.WriteLine($"蝕みなしの勇者に届く並び（1勝以上）{reach} ／ {perms.Length}・半分以上 {reach50}・ボス 100% の並び {bossAll}・近衛の最良 {Name(gBest)}（{F1(res[vi, gBest, 2].Win)}%・{res[vi, gBest, 2].WinTs}T）・試遊・毒の並びの順位 {order.IndexOf(cur) + 1}（蝕みなし {F1(res[vi, cur, 1].Win)}%・ボス {F1(res[vi, cur, 0].Win)}%・近衛 {F1(res[vi, cur, 2].Win)}%）。");
            Console.WriteLine($"1位の並び（`Presets` の席替えの台）: {string.Join(",", perms[order[0]])} ＝ {Name(order[0])}");
            Console.WriteLine();
        }

        // スィドの隣の組ごとの、ヴィオが吸った層（W2）
        Console.WriteLine("## W2 の取り合いは席で解けるか: スィドの隣の組ごとの、ヴィオが吸った ／ スィドが引き受けた層（ボス・1戦・その組の並びの平均）");
        Console.WriteLine();
        Console.WriteLine("| スィドの隣 | 並びの数 | ヴィオが吸った（W2） | スィドが引き受けた（W2） | ヴィオが吸った（W5） | 蝕みなし（W2 ／ W5 の平均） |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        var play = Presets.Versions.First(r => r.Name == "試遊・毒").F;
        int sidIx = Enumerable.Range(0, 5).First(i => play[i]!.Id == "sid");
        var groups = Enumerable.Range(0, perms.Length).GroupBy(pi =>
        {
            int slot = Array.IndexOf(perms[pi], sidIx);
            return string.Join("・", Enumerable.Range(0, 5).Where(s => FormationRules.AreAdjacent(slot, s)).Select(s => Short(play[perms[pi][s]]!)).OrderBy(x => x));
        }).OrderByDescending(g => g.Average(pi => (double)res[0, pi, 0].Drained / res[0, pi, 0].N));
        foreach (var g in groups)
        {
            var ps = g.ToList();
            double dr2 = ps.Average(pi => (double)res[0, pi, 0].Drained / res[0, pi, 0].N), sh2 = ps.Average(pi => (double)res[0, pi, 0].Shouldered / res[0, pi, 0].N), dr5 = ps.Average(pi => (double)res[1, pi, 0].Drained / res[1, pi, 0].N);
            Console.WriteLine($"| {g.Key} | {ps.Count} | {F1(dr2)} | {F1(sh2)} | {F1(dr5)} | {F1(ps.Average(pi => res[0, pi, 1].Win))} ／ {F1(ps.Average(pi => res[1, pi, 1].Win))} |");
        }
        Console.WriteLine();
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
        var v = ver switch { "W4" => Vers[3], "W5" => Vers[4], _ => throw new ArgumentException("sh371 compare の版は W4 / W5") };
        var rows = CompareBuilds().ToArray();
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
