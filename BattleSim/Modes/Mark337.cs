using BattleCore;
using static Common;
using D315 = Doha315Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// mark337 —— 第337期【軽い期】標軸の期 その3: 標の引き継ぎ（MI-b 周りへ散る ／ MI-b ＋ 羽 ／ MI-a 隣へ全部）。
// 指示書は design/PHASE337_MARK_INHERIT_SPEC.md ／ 報告は design/PHASE337_MARK_INHERIT.md。測定の作法は design/MEASURE_LIGHT.md（軽い期）。
//
//     dotnet run --project BattleSim -c Release 0 mark337 p0                      # §3 Phase 0（守り型 × 近衛 ／ 大隊 ／ 第2波 ／ 第5波: 標の付いた敵が倒れた回数 ／ 層 ／ 隣・標を追えずに飛んだ羽）
//     dotnet run --project BattleSim -c Release 0 mark337 boards [版,…]           # §5 代表台 5 × 5 波（＋ 守り型の強くした近衛 ／ 大隊）× 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 mark337 compare [版,…]          # §5 `compare` 64 行 × 版（seed 0..199・基準は規定）
//     dotnet run --project BattleSim -c Release 0 mark337 log [台] [波] [seed] [版] # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 mark337 check                   # §8 自己検査
//
// 版の名前（ASCII）: base（規定）／ b（MI-b）／ bf（MI-b ＋ 羽）／ a（MI-a）。波: boss ／ guard ／ bat ／ 2 ／ 5 ／ sguard ／ sbat。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・`.tmp/p337/predict.md`）。
// =====================================================================================
static partial class Mark337Diag
{
    internal const string PredictFile = ".tmp/p337/predict.md";
    const int Seeds = 100;          // 軽い期
    const int CompareSeeds = 200;   // `compare` の帯（今どおり）
    const string Tag = "第337期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"mark337: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "log": Log(A(3, "guard"), A(4, "bat"), int.Parse(A(5, "0")), V(A(6, "base"))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "check": Check(); return;
            default: Console.WriteLine("mark337: モードは p0 / log / boards / compare / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("N0");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef? Hisa, UnitDef? Misa);

    internal static readonly Ver[] Vers =
    {
        new("規定", "base", null, null),
        new("MI-b", "b", UnitCatalog.HisaMIb, null),
        new("MI-b＋羽", "bf", UnitCatalog.HisaMIb, UnitCatalog.MisaMIf),
        new("MI-a", "a", UnitCatalog.HisaMIa, null),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v)
    {
        var g = f;
        if (v.Hisa is not null) g = S316.SwapId(g, "hisa", v.Hisa);
        if (v.Misa is not null) g = S316.SwapId(g, "tome", v.Misa);
        return g;
    }

    internal const string GuardRow = "試遊・標 守り型";
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;

    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("守り型", Playtest(GuardRow)),
        ("ボス台", Playtest("試遊・標 ボス台")),
        ("循環", Playtest("試遊・標 循環")),
        ("三人組", Playtest("試遊・標 三人組")),
        ("標経済", Row("標経済 (ヒサ×ザン×ミサ)")),
    };

    internal static readonly S287.Wave[] Waves = new[] { "boss", "guard", "bat", "2", "5" }.Select(T323.WaveOf).ToArray();
    internal static readonly S287.Wave[] StrongWaves = D315.MakeWaves(D315.StrongHp, D315.StrongAtk).Where(w => w.Name is "近衛" or "大隊").ToArray();
    static S287.Wave WaveOf(string n) => n switch { "sguard" => StrongWaves[0], "sbat" => StrongWaves[1], _ => T323.WaveOf(n) };
    static string WN(S287.Wave w) => StrongWaves.Contains(w) ? $"強 {w.Name}" : w.Name;
    static bool IsElite(S287.Wave w) => w.Name is "近衛" or "大隊";

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose なし・帳簿だけから）
    // ---------------------------------------------------------------------------------
    internal sealed class St
    {
        public long N, Wins, WinT, OneT, AllyDeaths;
        public readonly Dictionary<string, long> DmgBy = new();
        public long MarkedDeaths, DeadLayers, DeadLayerMax, DeadNeighbors;
        public long Fires, Recipients, Layers, Lost, ChainSum, ChainPeak;
        public long Volley, Sprayed, SprayFoe, SprayAlly, MfFoe, MfAlly, MiFeathers;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public long DmgAll => DmgBy.Values.Sum();
        public long Feathers => Volley + MfFoe + MfAlly;
        public void Add(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; OneT += o.OneT; AllyDeaths += o.AllyDeaths;
            MarkedDeaths += o.MarkedDeaths; DeadLayers += o.DeadLayers; DeadLayerMax = Math.Max(DeadLayerMax, o.DeadLayerMax); DeadNeighbors += o.DeadNeighbors;
            Fires += o.Fires; Recipients += o.Recipients; Layers += o.Layers; Lost += o.Lost; ChainSum += o.ChainSum; ChainPeak = Math.Max(ChainPeak, o.ChainPeak);
            Volley += o.Volley; Sprayed += o.Sprayed; SprayFoe += o.SprayFoe; SprayAlly += o.SprayAlly; MfFoe += o.MfFoe; MfAlly += o.MfAlly; MiFeathers += o.MiFeathers;
            foreach (var (k, v) in o.DmgBy) DmgBy[k] = DmgBy.GetValueOrDefault(k) + v;
        }
    }

    internal static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false);
        var a = new St { N = 1, AllyDeaths = p.Count(u => u.LastDeathTurn > 0) };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; if (r.Turns <= 1) a.OneT = 1; }
        var names = p.GroupBy(u => u.Def.Id).ToDictionary(g => g.Key, g => Short(g.First().Def));
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!names.TryGetValue(id, out var nm)) continue;
            if (t.DamageToEnemy > 0) a.DmgBy[nm] = a.DmgBy.GetValueOrDefault(nm) + t.DamageToEnemy;
            if (id == "hisa")
            {
                a.MarkedDeaths += t.Mi337MarkedDeaths; a.DeadLayers += t.Mi337DeadLayers; a.DeadLayerMax = Math.Max(a.DeadLayerMax, t.Mi337DeadLayerMax); a.DeadNeighbors += t.Mi337DeadNeighbors;
                a.Fires += t.Mi337Fires; a.Recipients += t.Mi337Recipients; a.Layers += t.Mi337Layers; a.Lost += t.Mi337Lost; a.ChainSum += t.Mi337ChainMax; a.ChainPeak = Math.Max(a.ChainPeak, t.Mi337ChainMax);
            }
            if (id == "tome")
            {
                a.Volley += t.FeatherShots; a.Sprayed += t.FeatherSprayed; a.SprayFoe += t.SprayFoe; a.SprayAlly += t.SprayAlly; a.MfFoe += t.MfShotsFoe; a.MfAlly += t.MfShotsAlly; a.MiFeathers += t.Mi337Feathers;
            }
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
        var (name, f0) = board == "guard" ? Boards()[0] : Boards().First(b => b.Name == board);
        var w = WaveOf(wave);
        var r = BattleEngine.Run(BattleEngine.Materialize(Apply(f0, v), BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# {name} × {WN(w)} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // §3 Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var g = Boards()[0].F;
        var ws = new[] { "guard", "bat", "2", "5" }.Select(WaveOf).ToArray();
        Console.WriteLine($"# {Tag} §3 Phase 0 —— 規定・`{GuardRow}`（{S307.Seats(g)}）・seed 0..{Seeds - 1}・1戦あたり");
        Console.WriteLine();
        Console.WriteLine("1. 標の付いた敵が倒れた ＝ 層 ≧ 1 のまま倒れた敵（倒した者・倒れ方を問わない）。層 ＝ そのときの層（平均 ／ 全戦の最大）・隣 ＝ そのとき隣にいた生きている敵の数（平均）。");
        Console.WriteLine("2. 羽 ＝ ミサの一振りの羽 ＋ 標が付いた瞬間の羽（敵へ ／ 味方へ）。標を追えずに飛んだ ＝ 一振りの中で標の敵がいなくて敵味方構わず飛んだ羽（当たった先 敵 ／ 味方）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率・決着T | 標の付いた敵が倒れた | 層 平均 ／ 最大 | 隣（平均） | 羽 計（一振り ／ 標が付いた瞬間 敵 ／ 味方） | 標を追えずに飛んだ（敵 ／ 味方） | ミサの与ダメの割合 |");
        Console.WriteLine("|---|---|--:|---|--:|---|---|--:|");
        foreach (var w in ws)
        {
            var z = Measure(g, w);
            Console.WriteLine($"| {w.Name} | {F1(z.Win)}%・{Per(z.WinT, z.Wins)}T | {Per(z.MarkedDeaths, z.N)} | {Per(z.DeadLayers, z.MarkedDeaths)} ／ {z.DeadLayerMax} | {Per(z.DeadNeighbors, z.MarkedDeaths)} | "
                + $"{Per(z.Feathers, z.N)}（{Per(z.Volley, z.N)} ／ {Per(z.MfFoe, z.N)} ／ {Per(z.MfAlly, z.N)}） | {Per(z.Sprayed, z.N)}（{Per(z.SprayFoe, z.N)} ／ {Per(z.SprayAlly, z.N)}） | {Pct(z.DmgBy.GetValueOrDefault("ミサ"), z.DmgAll)}% |");
        }
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
        IEnumerable<S287.Wave> WavesOf(string bn) => bn == boards[0].Name ? Waves.Concat(StrongWaves) : Waves;
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in WavesOf(bn)) res[(bn, v.Name, WN(w))] = Measure(Apply(bf, v), w);
        Console.WriteLine($"# {Tag} §5 代表台 {boards.Length} × 5 波（守り型は ＋ 強くした近衛 ／ 大隊）× 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("版はヒサ（MI-b ／ MI-a）とミサ（＋ 羽）だけを差し替える。強 ＝ HP 500% ／ 攻 300%。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();
        string g0 = boards[0].Name;
        var gw = WavesOf(g0).Select(WN).ToArray();

        Console.WriteLine("## 1. 仇の連鎖の量（守り型）: 引き継ぎ ／戦・1回で標が付いた敵・移った層 ／戦・連鎖の長さ（戦ごとの最大の平均 ／ 全戦の最大）・隣がいなくて消えた層 ／戦");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 引き継ぎ | 1回で付いた敵 | 移った層 | 連鎖の長さ | 消えた層 |");
        Console.WriteLine("|---|---|--:|--:|--:|---|--:|");
        foreach (var wn in gw)
            foreach (var v in vers.Skip(1))
            {
                var z = res[(g0, v.Name, wn)];
                Console.WriteLine($"| {wn} | {v.Name} | {Per(z.Fires, z.N)} | {Per(z.Recipients, z.Fires)} | {Per(z.Layers, z.N)} | {Per(z.ChainSum, z.N)} ／ {z.ChainPeak} | {Per(z.Lost, z.N)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 2. ミサの羽（守り型・1戦あたり）: 羽 計（一振り ／ 標が付いた瞬間）・引き継ぎで増えた羽・標を追えずに飛んだ（味方に当たった）・ミサの与ダメの割合");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 勝率・決着T | 羽 計 | 一振り ／ 瞬間 | 引き継ぎで増えた | 標を追えずに飛んだ（味方） | ミサの割合 |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|--:|");
        foreach (var wn in gw)
            foreach (var v in vers)
            {
                var z = res[(g0, v.Name, wn)];
                Console.WriteLine($"| {wn} | {v.Name} | {F1(z.Win)}%・{Per(z.WinT, z.Wins)}T | {Per(z.Feathers, z.N)} | {Per(z.Volley, z.N)} ／ {Per(z.MfFoe + z.MfAlly, z.N)} | {Per(z.MiFeathers, z.N)} | {Per(z.Sprayed, z.N)}（{Per(z.SprayAlly, z.N)}） | {Pct(z.DmgBy.GetValueOrDefault("ミサ"), z.DmgAll)}% |");
            }
        Console.WriteLine();

        Console.WriteLine("## 3. 代表台の勝率（規定との差）・決着T（ボスは倒しT）・倒れた味方 ／戦");
        Console.WriteLine();
        foreach (var (bn, _) in boards)
        {
            var wl = WavesOf(bn).Select(WN).ToArray();
            Console.WriteLine($"### {bn}");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", wl) + " |");
            Console.WriteLine("|---|" + string.Concat(wl.Select(_ => "---|")));
            foreach (var v in vers)
                Console.WriteLine($"| {v.Name} | " + string.Join(" | ", wl.Select(wn =>
                {
                    var z = res[(bn, v.Name, wn)]; double b0 = res[(bn, vers[0].Name, wn)].Win;
                    string win = v == vers[0] || z.Win == b0 ? F1(z.Win) : $"{F1(z.Win)}（{Sg(z.Win - b0)}）";
                    return $"{win}・{Per(z.WinT, z.Wins)}T・倒 {(z.N == 0 ? "—" : ((double)z.AllyDeaths / z.N).ToString("F2"))}";
                })) + " |");
            Console.WriteLine();
        }

        Console.WriteLine("## 4. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ 勝率が規定から 15pt 以上落ちる ／ 1戦の羽が規定の 3 倍超）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var bad = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in WavesOf(bn))
                {
                    var z = res[(bn, v.Name, WN(w))]; var b = res[(bn, vers[0].Name, WN(w))];
                    if (IsElite(w) && !StrongWaves.Contains(w) && z.N > 0 && 100.0 * z.OneT / z.N >= 5) bad.Add($"{bn} × {WN(w)} 1T 決着 {Pct(z.OneT, z.N)}%");
                    if (z.Win - b.Win <= -15) bad.Add($"{bn} × {WN(w)} {Sg(z.Win - b.Win)}");
                    if (b.Feathers > 0 && z.Feathers > 3 * b.Feathers) bad.Add($"{bn} × {WN(w)} 羽 {Per(b.Feathers, b.N)} → {Per(z.Feathers, z.N)}");
                }
            Console.WriteLine($"- **{v.Name}**: {(bad.Count == 0 ? "なし" : string.Join("・", bad))}");
        }
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
        Console.WriteLine($"ヒサのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "hisa")).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, Grid(vers[vi]), "hisa");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
