using BattleCore;
using static Common;
using K342 = Kata342Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// golm343 —— 第343期【軽い期】段0: カタの K3（KT-b ＋ 避雷針・跳ねる）を規定に ／ 本体: ゴルムの手番「精気を呑む」（GT-a2 吐き捨てる ／ GT-a1 消化 ／ GT-a3 膨れる）。
// 指示書は design/PHASE343_KATA_SETTLE_GOLM_SWALLOW_SPEC.md ／ 報告は design/PHASE343_KATA_SETTLE_GOLM_SWALLOW.md。
//
//     dotnet run --project BattleSim -c Release 0 golm343 s0                      # §2 段0: カタの代表台 4 × 5 波 × 旧（`KataH342`）／ 新（K3）（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 golm343 p0                      # §4 Phase 0: ゴルムのいる台の手番の数・腹・呑める見込み
//     dotnet run --project BattleSim -c Release 0 golm343 boards [版,…]           # §6 代表台 5 × 5 波 × 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 golm343 compare [版,…]          # §6 `compare` 64 行 × 版（seed 0..199・基準は段0 入りの規定）
//     dotnet run --project BattleSim -c Release 0 golm343 log [台] [波] [seed] [版] # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 golm343 check                   # §9 自己検査
//
// 版の名前（ASCII）: base（規定）／ a2（吐き捨てる）／ a1（消化）／ a3（膨れる）。台: guard ／ havoc（突き出し）／ utsu（逆しま+後備え）／ shock ／ poison。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・`.tmp/p343/predict.md`）。
// =====================================================================================
static partial class Golm343Diag
{
    internal const string PredictFile = ".tmp/p343/predict.md";
    const int Seeds = 100;          // 軽い期
    const int CompareSeeds = 200;   // `compare` の帯（今どおり）
    const string Tag = "第343期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"golm343: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "s0": Stage0(); return;
            case "p0": P0(); return;
            case "log": Log(A(3, "guard"), A(4, "bat"), int.Parse(A(5, "0")), V(A(6, "a2"))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "check": Check(); return;
            default: Console.WriteLine("golm343: モードは s0 / p0 / log / boards / compare / check。"); return;
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
    internal sealed record Ver(string Name, string Ascii, UnitDef? Golm);

    internal static readonly Ver[] Vers =
    {
        new("規定", "base", null),
        new("GT-a2 吐き捨てる", "a2", UnitCatalog.GolmGTa2),
        new("GT-a1 消化", "a1", UnitCatalog.GolmGTa1),
        new("GT-a3 膨れる", "a3", UnitCatalog.GolmGTa3),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => v.Golm is null ? f : S316.SwapId(f, "golm", v.Golm);

    internal const string GuardRow = "試遊・標 守り型";
    // Phase 0 の後に決めた（決めたこと）: 惨禍×死の連鎖はゴルムの手番が 1戦 0.2 回で呑める敵 0 なので外し、呑む見込み（手番 × 呑める敵）の多い2行を取った（逆しまは自傷の軸）。
    internal const string HavocRow = "突き出し (セロ×ヨミ)";
    internal const string UtsuRow = "逆しま+後備え";
    internal const string PoisonRow = "毒 (グザ×ミオ×ラウ)";
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;

    /// <summary>§6 の感電の台にゴルム: 「試遊・感電 ポンの台」のカタ（前1）をゴルムに替えた（ゴルム・シガ・トウ・クグ・ソム）。</summary>
    internal static Formation ShockGolm() => S316.SwapId(Presets.Versions.First(r => r.Name == K342.PonRow).F, "kata", UnitCatalog.Golm);
    /// <summary>§6 の毒の台にゴルム: `毒 (グザ×ミオ×ラウ)` の前3（ガルド）をゴルムに替えた（ミオ・ゴルム・グザ・スィド・ラウ）。</summary>
    internal static Formation PoisonGolm() { var f = Row(PoisonRow).Clone(); f[1] = UnitCatalog.Golm; return f; }

    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("守り型", Presets.Playtest.First(r => r.Name == GuardRow).F),
        ("突き出し", Row(HavocRow)),
        ("逆しま+後備え", Row(UtsuRow)),
        ("感電にゴルム", ShockGolm()),
        ("毒にゴルム", PoisonGolm()),
    };
    static (string Name, Formation F) BoardOf(string n) => n switch
    {
        "guard" => Boards()[0], "havoc" => Boards()[1], "utsu" => Boards()[2], "shock" => Boards()[3], "poison" => Boards()[4], _ => Boards().First(b => b.Name == n),
    };

    internal static readonly S287.Wave[] Waves = new[] { "boss", "guard", "bat", "2", "5" }.Select(T323.WaveOf).ToArray();
    static bool IsElite(S287.Wave w) => w.Name is "近衛" or "大隊";

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class St
    {
        public long N, Wins, WinT, OneT, AllyDeaths, GolmWork, Mi337Fires;
        public readonly Dictionary<string, long> WorkBy = new();
        public UnitTally G = new();
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public long WorkAll => WorkBy.Values.Sum();
        public void Add(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; OneT += o.OneT; AllyDeaths += o.AllyDeaths; GolmWork += o.GolmWork; Mi337Fires += o.Mi337Fires;
            G.Add(o.G);
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
            if (id == "hisa") a.Mi337Fires += t.Mi337Fires;
            if (!names.TryGetValue(id, out var nm)) continue;
            long work = t.DamageToEnemy + t.CreditToEnemy;
            if (work > 0) a.WorkBy[nm] = a.WorkBy.GetValueOrDefault(nm) + work;
            if (id != "golm") continue;
            a.G.Add(t);
            a.GolmWork += work;
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
    // §2 段0
    // ---------------------------------------------------------------------------------
    static void Stage0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = K342.Boards();
        Console.WriteLine($"# {Tag} §2 段0 —— カタの代表台 {boards.Length} × {K342.Waves.Length} 波 × 旧（`KataH342`）／ 新（規定 ＝ K3）・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", K342.Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(K342.Waves.Select(_ => "---|")));
        string Cell(K342.St z) => $"{F1(z.Win)}%・{Per(z.WinT, z.Wins)}T";
        foreach (var (bn, bf) in boards)
            Console.WriteLine($"| {bn} | " + string.Join(" | ", K342.Waves.Select(w =>
            {
                var o = K342.Measure(Pin342(bf), w); var z = K342.Measure(bf, w);
                string extra = $"・カタ {Per(o.KataDealt, o.N)} → {Per(z.KataDealt, z.N)}・痺れ {Per2(o.StallShock, o.N)} → {Per2(z.StallShock, z.N)}";
                return (Cell(o) != Cell(z) ? $"{Cell(o)} → **{Cell(z)}**" : $"{Cell(z)}（同じ）") + extra;
            })) + " |");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §4 Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().Select(r => (Kind: "compare", r.Name, r.F)).Concat(Presets.Playtest.Concat(Presets.Versions).Select(r => (Kind: "試遊", r.Name, r.F)))
            .Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Golm))).ToArray();
        Console.WriteLine($"# {Tag} §4 Phase 0 —— 段0 入りの規定・ゴルム（規定）のいる台 {rows.Length} 行・波 ボス ／ 近衛 ／ 大隊 ／ 第2波 ／ 第5波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("手番 ＝ ゴルムが動けた手番 ／戦（5 波の平均）。腹 ＝ 手番の頭の腹（巨躯で飲み込んだ量の残高・平均 ／ 全戦の最大）。呑める見込み ＝ 手番の頭で HP が腹以下の敵の数（手番あたり）と、1体以上いた手番の割合は数えない（数の平均だけ）。席 ＝ ゴルムの席。");
        Console.WriteLine();
        Console.WriteLine("| 種 | 行 | 席 | 手番 ／戦 | 腹 平均 ／ 最大 | 呑める敵 ／手番 | 波ごとの手番（ボス ／ 近衛 ／ 大隊 ／ 第2 ／ 第5） |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|");
        foreach (var (kind, name, f) in rows)
        {
            var per = Waves.Select(w => Measure(f, w)).ToArray();
            var all = new St(); foreach (var z in per) all.Add(z);
            int slot = f.Occupied().First(o => ReferenceEquals(o.Def, UnitCatalog.Golm)).Slot;
            Console.WriteLine($"| {kind} | {name} | {FormationRules.SeatNames[slot]} | {Per(all.G.Gt343Turns, all.N)} | {Per(all.G.Gt343BellySum, all.G.Gt343Turns)} ／ {all.G.Gt343BellyMax} | {Per2(all.G.Gt343Edible, all.G.Gt343Turns)} | "
                + string.Join(" ／ ", per.Select(z => Per(z.G.Gt343Turns, z.N))) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("器具の中で組む台（§6）:");
        Console.WriteLine();
        foreach (var (bn, bf) in new[] { ("感電にゴルム", ShockGolm()), ("毒にゴルム", PoisonGolm()) })
        {
            var all = new St(); foreach (var w in Waves) all.Add(Measure(bf, w));
            Console.WriteLine($"- {bn}（{S307.Seats(bf)}）: 手番 {Per(all.G.Gt343Turns, all.N)} ／戦・腹 {Per(all.G.Gt343BellySum, all.G.Gt343Turns)} ／ {all.G.Gt343BellyMax}・呑める敵 {Per2(all.G.Gt343Edible, all.G.Gt343Turns)} ／手番");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §6 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var res = new Dictionary<(string, string, string), St>();
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = Measure(Apply(bf, v), w);
        Console.WriteLine($"# {Tag} §6 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("版はゴルムだけを差し替える（段0 入りの規定の上）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine("## 1. ゴルムの手番の中身（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("手番 ／ 呑んだ（呑んだ敵の HP ／帯びた状態異常: 毒・火・雷・濃・標・印）／ 殴った ／ 吐いた（毒・火・雷・濃・標 ／ 吐いた先の HP の平均）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 手番 | 呑んだ | 呑んだ敵の HP ／回 | 呑んだ状態異常 ／戦 | 殴った | 吐いた ／戦 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|--:|---|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                foreach (var v in vers.Skip(1))
                {
                    var z = res[(bn, v.Name, w.Name)]; var g = z.G;
                    string sw2 = $"{Per(g.Gt343SwPoison, z.N)}・{Per(g.Gt343SwBurn, z.N)}・{Per(g.Gt343SwShock, z.N)}・{Per(g.Gt343SwDensity, z.N)}・{Per(g.Gt343SwMark, z.N)}・{Per(g.Gt343SwConc, z.N)}";
                    string sp = g.Gt343Spits == 0 ? "—" : $"{Per(g.Gt343Spits, z.N)} 回（{Per(g.Gt343SpitPoison, z.N)}・{Per(g.Gt343SpitBurn, z.N)}・{Per(g.Gt343SpitShock, z.N)}・{Per(g.Gt343SpitDensity, z.N)}・{Per(g.Gt343SpitMark, z.N)} ／ 先 HP {Per(g.Gt343SpitOnTop, g.Gt343Spits)}）";
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Ascii} | {Per(g.Gt343Turns, z.N)} | {Per2(g.Gt343Swallows, z.N)} | {Per(g.Gt343SwallowHp, g.Gt343Swallows)} | {sw2} | {Per(g.Gt343Attacks, z.N)} | {sp} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 2. ゴルムの働き（1戦あたり）: 与(敵) ＋ 仕掛け(敵)（割合）・呑んだ HP・癒した量（a1）・増えた最大 HP（a3）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | " + string.Join(" | ", vers.Select(v => v.Ascii)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(vers.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                Console.WriteLine($"| {bn} | {w.Name} | " + string.Join(" | ", vers.Select(v =>
                {
                    var z = res[(bn, v.Name, w.Name)]; var g = z.G;
                    string s = $"{Per(z.GolmWork, z.N)}（{Pct(z.GolmWork, z.WorkAll)}%）";
                    if (g.Gt343SwallowHp > 0) s += $"・呑 {Per(g.Gt343SwallowHp, z.N)}";
                    if (g.Gt343Healed > 0) s += $"・癒 {Per(g.Gt343Healed, z.N)}";
                    if (g.Gt343Grown > 0) s += $"・膨 {Per(g.Gt343Grown, z.N)}";
                    return s;
                })) + " |");
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

        Console.WriteLine("## 4. 呑んだことで起きなかった引き継ぎ ／ 飛び火（1戦あたり）: 標の付いた敵を隣のいるまま呑んだ・呑んだ標 ／ 毒 ／ 印・ヒサの引き継ぎ（`Mi337Fires`）の規定 → 版");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 標の付いた敵（隣あり）を呑んだ | 呑んだ 標 ／ 毒 ／ 印 | 引き継ぎ 規定 → 版 |");
        Console.WriteLine("|---|---|---|--:|---|---|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                foreach (var v in vers.Skip(1))
                {
                    var z = res[(bn, v.Name, w.Name)]; var b = res[(bn, vers[0].Name, w.Name)];
                    if (z.G.Gt343Swallows == 0) continue;
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Ascii} | {Per2(z.G.Gt343SwMarkedNbr, z.N)} | {Per(z.G.Gt343SwMark, z.N)} ／ {Per(z.G.Gt343SwPoison, z.N)} ／ {Per(z.G.Gt343SwConc, z.N)} | {Per(b.Mi337Fires, b.N)} → {Per(z.Mi337Fires, z.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 5. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ 勝率が規定から 15pt 以上落ちる ／ ボスを 4T 未満で倒す——規定で 4T 以上だった台だけ）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var bad = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                {
                    var z = res[(bn, v.Name, w.Name)]; var b = res[(bn, vers[0].Name, w.Name)];
                    if (IsElite(w) && z.N > 0 && 100.0 * z.OneT / z.N >= 5) bad.Add($"{bn} × {w.Name} 1T 決着 {Pct(z.OneT, z.N)}%");
                    if (z.Win - b.Win <= -15) bad.Add($"{bn} × {w.Name} {Sg(z.Win - b.Win)}");
                    if (w.Name == "ボス" && z.Wins > 0 && (double)z.WinT / z.Wins < 4.0 && !(b.Wins > 0 && (double)b.WinT / b.Wins < 4.0)) bad.Add($"{bn} × ボス {Per(z.WinT, z.Wins)}T");
                }
            Console.WriteLine($"- **{v.Name}**: {(bad.Count == 0 ? "なし" : string.Join("・", bad))}");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §6 `compare` × 版
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
        Console.WriteLine($"# {Tag} §6 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は段0 入りの規定）");
        Console.WriteLine();
        Console.WriteLine($"ゴルムのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "golm")).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, Grid(vers[vi]), "golm");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
