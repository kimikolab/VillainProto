using BattleCore;
using static Common;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// kata342 —— 第342期【軽い期】カタの見せ場: 味方の弾けも雷雲に（KT-b）／ 避雷針（KT-r・1発 ／ 跳ねる）。
// 指示書は design/PHASE342_KATA_ROD_SPEC.md ／ 報告は design/PHASE342_KATA_ROD.md。測定の作法は design/MEASURE_LIGHT.md（軽い期）。
//
//     dotnet run --project BattleSim -c Release 0 kata342 p0                      # §3 Phase 0（ポンの台 ／ 光の盾 鞭 × 近衛 ／ 大隊 ／ ボス: カタの痺れ ／ 雷雲 ／ 味方の弾け）
//     dotnet run --project BattleSim -c Release 0 kata342 boards [版,…]           # §5 代表台 4 × 5 波 × 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 kata342 compare [版,…]          # §5 `compare` 64 行 × 版（seed 0..199・基準は規定）
//     dotnet run --project BattleSim -c Release 0 kata342 log [台] [波] [seed] [版] # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 kata342 check                   # §8 自己検査
//
// 第343期: K3 を規定にしたので、「規定」（base）は第332〜342期の規定のカタ（`KataH342`・`Common.Pin342`）に固定した。
// 版の名前（ASCII）: base（規定）／ b（KT-b）／ r1（KT-b ＋ 避雷針 1発）／ rj（KT-b ＋ 避雷針 跳ねる）。波: boss ／ guard ／ bat ／ 2 ／ 5。台: pon ／ whip ／ shield ／ 感電。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・`.tmp/p342/predict.md`）。
// =====================================================================================
static partial class Kata342Diag
{
    internal const string PredictFile = ".tmp/p342/predict.md";
    const int Seeds = 100;          // 軽い期
    const int CompareSeeds = 200;   // `compare` の帯（今どおり）
    const string Tag = "第342期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"kata342: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "log": Log(A(3, "pon"), A(4, "bat"), int.Parse(A(5, "0")), V(A(6, "r1"))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "check": Check(); return;
            default: Console.WriteLine("kata342: モードは p0 / log / boards / compare / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef? Kata);

    internal static readonly Ver[] Vers =
    {
        new("規定", "base", null),
        new("KT-b", "b", UnitCatalog.KataKTb),
        new("KT-b＋避雷針1発", "r1", UnitCatalog.KataKTr1),
        new("KT-b＋避雷針跳ね", "rj", UnitCatalog.KataKTrJ),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => v.Kata is null ? Pin342(f) : S316.SwapId(f, "kata", v.Kata);   // 第343期: 「規定」は第332〜342期の規定のカタ（`KataH342`）に固定

    internal const string PonRow = "試遊・感電 ポンの台";
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("ポンの台", Presets.Versions.First(r => r.Name == PonRow).F),
        ("光の盾 鞭", Presets.Playtest.First(r => r.Name == "試遊・感電 光の盾 鞭").F),
        ("光の盾", Presets.Playtest.First(r => r.Name == "試遊・感電 光の盾").F),
        ("感電", CompareBuilds().First(r => r.Name == "感電 (シガ×カタ×ソム)").F),
    };
    static (string Name, Formation F) BoardOf(string n) => n switch { "pon" => Boards()[0], "whip" => Boards()[1], "shield" => Boards()[2], _ => Boards().First(b => b.Name == n) };

    internal static readonly S287.Wave[] Waves = new[] { "boss", "guard", "bat", "2", "5" }.Select(T323.WaveOf).ToArray();
    static bool IsElite(S287.Wave w) => w.Name is "近衛" or "大隊";

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose なし・帳簿だけから）
    // ---------------------------------------------------------------------------------
    internal sealed class St
    {
        public long N, Wins, WinT, OneT, AllyDeaths;
        public readonly Dictionary<string, long> WorkBy = new();   // 与(敵) ＋ 仕掛け(敵)
        public long KataDealt, KataCredit, Casts, StallShock, StallStun, CloudSum, CloudMax, ThunderDealt, AllyPops, RodFires, RodOnAtk, RodHits, RodDealt;
        public readonly long[] StunCause = new long[3];
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public long WorkAll => WorkBy.Values.Sum();
        public void Add(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; OneT += o.OneT; AllyDeaths += o.AllyDeaths;
            KataDealt += o.KataDealt; KataCredit += o.KataCredit; Casts += o.Casts; StallShock += o.StallShock; StallStun += o.StallStun; CloudSum += o.CloudSum; CloudMax = Math.Max(CloudMax, o.CloudMax);
            ThunderDealt += o.ThunderDealt; AllyPops += o.AllyPops; RodFires += o.RodFires; RodOnAtk += o.RodOnAtk; RodHits += o.RodHits; RodDealt += o.RodDealt;
            for (int i = 0; i < 3; i++) StunCause[i] += o.StunCause[i];
            foreach (var (k, v) in o.WorkBy) WorkBy[k] = WorkBy.GetValueOrDefault(k) + v;
        }
    }

    internal static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false);
        var a = new St { N = 1, AllyDeaths = p.Count(u => u.LastDeathTurn > 0) };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; if (r.Turns <= 1) a.OneT = 1; }
        var names = p.GroupBy(u => u.Def.Id).ToDictionary(g => g.Key, g => S307.Short(g.First().Def));
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!names.TryGetValue(id, out var nm)) continue;
            long work = t.DamageToEnemy + t.CreditToEnemy;
            if (work > 0) a.WorkBy[nm] = a.WorkBy.GetValueOrDefault(nm) + work;
            if (id != "kata") continue;
            a.KataDealt += t.DamageToEnemy; a.KataCredit += t.CreditToEnemy; a.Casts += t.ThunderCasts; a.StallShock += t.StallShockStun; a.StallStun += t.StallStun;
            a.CloudSum += t.CloudAtCastSum; a.CloudMax = Math.Max(a.CloudMax, t.CloudAtCastMax); a.ThunderDealt += t.ThunderDealt; a.AllyPops += t.Kt342AllyPops;
            a.RodFires += t.Kt342RodFires; a.RodOnAtk += t.Kt342RodOnAttacker; a.RodHits += t.Kt342RodHits; a.RodDealt += t.Kt342RodDealt;
            if (t.Kt342StunCause is { } sc) for (int i = 0; i < 3; i++) a.StunCause[i] += sc[i];
        }
        return a;
    }

    internal static St Measure(Formation f, S287.Wave w, int n = Seeds)
    {
        var parts = new St[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, i));
        var all = new St();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static void Log(string board, string wave, int seed, Ver v)
    {
        var (name, f0) = BoardOf(board);
        var w = T323.WaveOf(wave);
        var r = BattleEngine.Run(BattleEngine.Materialize(Apply(f0, v), BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# {name} × {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // §3 Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §3 Phase 0 —— 規定・seed 0..{Seeds - 1}・1戦あたり");
        Console.WriteLine();
        Console.WriteLine("痺れで失った手番 ＝ 感電の痺れで手番を失った数（割合の分母 ＝ 雷 ＋ 痺れで失った手番）。きっかけ ＝ カタに痺れが付いたときの連鎖（敵の一撃で起点 ／ 連鎖の放電で ／ ほか＝味方の一撃や刻み）。");
        Console.WriteLine("雷雲 ＝ 雷を落としたときの雷雲（平均 ／ 全戦の最大）。味方の弾け ＝ カタの側で感電が弾けた数（KT-b で雷雲に加わる見込み）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率・決着T | 雷 | 痺れで失った手番（割合） | きっかけ 敵の一撃 ／ 連鎖 ／ ほか | 雷雲 平均 ／ 最大 | 雷の与ダメ | カタの与(敵)・割合 | 味方の弾け |");
        Console.WriteLine("|---|---|---|--:|---|---|---|--:|---|--:|");
        foreach (var (bn, bf) in Boards().Take(2))
            foreach (var w in new[] { Waves[1], Waves[2], Waves[0] })
            {
                var z = Measure(Pin342(bf), w);   // 第343期: 第332〜342期の規定のカタ
                Console.WriteLine($"| {bn} | {w.Name} | {F1(z.Win)}%・{Per(z.WinT, z.Wins)}T | {Per(z.Casts, z.N)} | {Per2(z.StallShock, z.N)}（{Pct(z.StallShock, z.Casts + z.StallStun)}%） | {Per2(z.StunCause[0], z.N)} ／ {Per2(z.StunCause[1], z.N)} ／ {Per2(z.StunCause[2], z.N)} | "
                    + $"{Per(z.CloudSum, z.Casts)} ／ {z.CloudMax} | {Per(z.ThunderDealt, z.N)} | {Per(z.KataDealt, z.N)}・{Pct(z.KataDealt + z.KataCredit, z.WorkAll)}% | {Per(z.AllyPops, z.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine("割合 ＝ 味方の（与(敵) ＋ 仕掛け(敵)）の合計に占めるカタの分（第341期の手柄の数え方）。");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §5 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var res = new Dictionary<(string, string, string), St>();
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = Measure(Apply(bf, v), w);
        Console.WriteLine($"# {Tag} §5 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("版はカタだけを差し替える。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine("## 1. カタの働き（ポンの台 ／ 光の盾 鞭 × 近衛 ／ 大隊 ／ ボス・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("働き ＝ カタの与(敵)（仕掛け(敵) は 0）・割合 ＝ 味方の与(敵) ＋ 仕掛け(敵) に占める割合。逆雷 ＝ 回 ／ うち殴った敵へ ／ 当たった延べ ／ 与ダメ。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率・決着T | 働き（割合） | 痺れで失った手番 | 雷雲 平均 ／ 最大 | 逆雷 |");
        Console.WriteLine("|---|---|---|---|---|--:|---|---|");
        foreach (var (bn, _) in boards.Take(2))
            foreach (var w in new[] { Waves[1], Waves[2], Waves[0] })
                foreach (var v in vers)
                {
                    var z = res[(bn, v.Name, w.Name)];
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {F1(z.Win)}%・{Per(z.WinT, z.Wins)}T | {Per(z.KataDealt + z.KataCredit, z.N)}（{Pct(z.KataDealt + z.KataCredit, z.WorkAll)}%） | {Per2(z.StallShock, z.N)} | "
                        + $"{Per(z.CloudSum, z.Casts)} ／ {z.CloudMax} | {(z.RodFires == 0 ? "—" : $"{Per(z.RodFires, z.N)} ／ {Per(z.RodOnAtk, z.N)} ／ {Per(z.RodHits, z.N)} ／ {Per(z.RodDealt, z.N)}")} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 2. 駒ごとの働きの割合（ポンの台 ／ 光の盾 鞭 × 大隊）");
        Console.WriteLine();
        foreach (var (bn, _) in boards.Take(2))
        {
            Console.WriteLine($"- **{bn}**: " + string.Join("　", vers.Select(v =>
            {
                var z = res[(bn, v.Name, Waves[2].Name)];
                return $"{v.Name} " + string.Join("・", z.WorkBy.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {Pct(x.Value, z.WorkAll)}%"));
            })));
        }
        Console.WriteLine();

        Console.WriteLine("## 3. 代表台の勝率（規定との差）・決着T（ボスは倒しT）・倒れた味方 ／戦");
        Console.WriteLine();
        foreach (var (bn, _) in boards)
        {
            Console.WriteLine($"### {bn}");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
            foreach (var v in vers)
                Console.WriteLine($"| {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var z = res[(bn, v.Name, w.Name)]; double b0 = res[(bn, vers[0].Name, w.Name)].Win;
                    string win = v == vers[0] || z.Win == b0 ? F1(z.Win) : $"{F1(z.Win)}（{Sg(z.Win - b0)}）";
                    return $"{win}・{Per(z.WinT, z.Wins)}T・倒 {Per2(z.AllyDeaths, z.N)}";
                })) + " |");
            Console.WriteLine();
        }

        Console.WriteLine("## 4. 壊れの兆し（ボスを 4T 未満で倒す ／ 精鋭の 1T 決着 5% 以上 ／ 勝率が規定から 15pt 以上落ちる）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var bad = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                {
                    var z = res[(bn, v.Name, w.Name)]; var b = res[(bn, vers[0].Name, w.Name)];
                    if (w.Name == "ボス" && z.Wins > 0 && (double)z.WinT / z.Wins < 4.0 && !(b.Wins > 0 && (double)b.WinT / b.Wins < 4.0)) bad.Add($"{bn} × ボス {Per(z.WinT, z.Wins)}T");
                    if (IsElite(w) && z.N > 0 && 100.0 * z.OneT / z.N >= 5) bad.Add($"{bn} × {w.Name} 1T 決着 {Pct(z.OneT, z.N)}%");
                    if (z.Win - b.Win <= -15) bad.Add($"{bn} × {w.Name} {Sg(z.Win - b.Win)}");
                }
            Console.WriteLine($"- **{v.Name}**: {(bad.Count == 0 ? "なし" : string.Join("・", bad))}");
        }
        Console.WriteLine();
        Console.WriteLine("ボス 4T 未満は「規定で 4T 以上だった台」だけを数える（規定から 4T 未満の台は兆しに数えない）。");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §5 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        double[,] Grid(Ver v)
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = Apply(rows[ri].F, v);
                int wins = 0;
                for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / CompareSeeds;
            });
            return g;
        }
        var b = Grid(vers[0]);
        Console.WriteLine($"# {Tag} §5 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"カタのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "kata")).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, Grid(vers[vi]), "kata");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
