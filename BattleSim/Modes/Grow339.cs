using BattleCore;
using static Common;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// grow339 —— 第339期【軽い期】標軸の期 その4: ミサの羽が毎ターン増える（FG-a 毎ターン ／ FG-b 標が無いときだけ）。
// 指示書は design/PHASE339_MISA_FEATHER_GROW_SPEC.md ／ 報告は design/PHASE339_MISA_FEATHER_GROW.md。測定の作法は design/MEASURE_LIGHT.md（軽い期）。
//
//     dotnet run --project BattleSim -c Release 0 grow339 p0                      # §3 Phase 0（書き手なしの台の乱射の量・守り型の標の無い一振りの割合）
//     dotnet run --project BattleSim -c Release 0 grow339 boards [版,…]           # §5 代表台 5 × 5 波 × 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 grow339 compare [版,…]          # §5 `compare` 64 行 × 版（seed 0..199・基準は規定）
//     dotnet run --project BattleSim -c Release 0 grow339 log [台] [波] [seed] [版] # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 grow339 check                   # §8 自己検査
//
// 第340期: FG-b を規定にしたので、「規定」（base）は第334〜339期の規定のミサ（`TomeH339`・`Common.Pin339`）に固定した。
// 版の名前（ASCII）: base（規定）／ a（FG-a）／ b（FG-b）。波: boss ／ guard ／ bat ／ 2 ／ 5。台: 守り型 ／ ボス台 ／ 三人組 ／ 標経済 ／ 書き手なし（ASCII は guard ／ nowriter）。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・`.tmp/p339/predict.md`）。
// =====================================================================================
static partial class Grow339Diag
{
    internal const string PredictFile = ".tmp/p339/predict.md";
    const int Seeds = 100;          // 軽い期
    const int CompareSeeds = 200;   // `compare` の帯（今どおり）
    const string Tag = "第339期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"grow339: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "log": Log(A(3, "nowriter"), A(4, "guard"), int.Parse(A(5, "0")), V(A(6, "a"))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "check": Check(); return;
            default: Console.WriteLine("grow339: モードは p0 / log / boards / compare / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef? Misa);

    internal static readonly Ver[] Vers =
    {
        new("規定", "base", null),
        new("FG-a", "a", UnitCatalog.TomeFGa),
        new("FG-b", "b", UnitCatalog.TomeFGb),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => v.Misa is null ? Pin339(f) : S316.SwapId(f, "tome", v.Misa);   // 第340期: 「規定」は第334〜339期の規定のミサ（`TomeH339`）に固定

    internal const string GuardRow = "試遊・標 守り型";
    internal const string NoWriter = "書き手なし";
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;

    /// <summary>§3-1 の書き手のいない台: 守り型のザン ／ ヒサ（標の書き手）をドルガ ／ バンに替えた（ゴルム・ドルガ・ドハ・ミサ・バン）。`Presets.Versions` の「版・FG-a 書き手なし」と同じ並び。</summary>
    internal static Formation NoWriterBoard() => Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Dolga, center: UnitCatalog.Doha, back1: UnitCatalog.Tome, back3: UnitCatalog.Ban);

    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("守り型", Playtest(GuardRow)),
        ("ボス台", Playtest("試遊・標 ボス台")),
        ("三人組", Playtest("試遊・標 三人組")),
        ("標経済", Row("標経済 (ヒサ×ザン×ミサ)")),
        (NoWriter, NoWriterBoard()),
    };
    static (string Name, Formation F) BoardOf(string n) => n switch { "guard" => Boards()[0], "nowriter" => Boards()[4], _ => Boards().First(b => b.Name == n) };

    internal static readonly S287.Wave[] Waves = new[] { "boss", "guard", "bat", "2", "5" }.Select(T323.WaveOf).ToArray();
    static bool IsElite(S287.Wave w) => w.Name is "近衛" or "大隊";
    static readonly int[] TurnsShown = { 1, 3, 5, 7 };

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose なし・帳簿だけから）
    // ---------------------------------------------------------------------------------
    internal sealed class St
    {
        public long N, Wins, WinT, OneT, AllyDeaths, Reached7;
        public readonly Dictionary<string, long> DmgBy = new();
        public long Volleys, UnmarkedHeads, Grows, FeathersEnd, Sprayed, SprayAlly, SprayAllyDealt, SprayKills, SprayKillTurnSum;
        public readonly long[] Spray = new long[UnitTally.Fg339Len], AllyHits = new long[UnitTally.Fg339Len], AllyDmg = new long[UnitTally.Fg339Len], Alive = new long[UnitTally.Fg339Len];
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public long DmgAll => DmgBy.Values.Sum();
        public void Add(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; OneT += o.OneT; AllyDeaths += o.AllyDeaths; Reached7 += o.Reached7;
            Volleys += o.Volleys; UnmarkedHeads += o.UnmarkedHeads; Grows += o.Grows; FeathersEnd += o.FeathersEnd; Sprayed += o.Sprayed;
            SprayAlly += o.SprayAlly; SprayAllyDealt += o.SprayAllyDealt; SprayKills += o.SprayKills; SprayKillTurnSum += o.SprayKillTurnSum;
            for (int i = 0; i < Spray.Length; i++) { Spray[i] += o.Spray[i]; AllyHits[i] += o.AllyHits[i]; AllyDmg[i] += o.AllyDmg[i]; Alive[i] += o.Alive[i]; }
            foreach (var (k, v) in o.DmgBy) DmgBy[k] = DmgBy.GetValueOrDefault(k) + v;
        }
    }

    internal static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false);
        var a = new St { N = 1, AllyDeaths = p.Count(u => u.LastDeathTurn > 0) };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; if (r.Turns <= 1) a.OneT = 1; }
        if (r.Turns >= 7) a.Reached7 = 1;
        for (int i = 1; i < a.Alive.Length && i <= r.Turns; i++) a.Alive[i] = 1;   // そのターンまで戦が続いた（ターンごとの平均の分母）
        var names = p.GroupBy(u => u.Def.Id).ToDictionary(g => g.Key, g => Short(g.First().Def));
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!names.TryGetValue(id, out var nm)) continue;
            if (t.DamageToEnemy > 0) a.DmgBy[nm] = a.DmgBy.GetValueOrDefault(nm) + t.DamageToEnemy;
            if (id != "tome") continue;
            a.Volleys += t.FeatherVolleys; a.UnmarkedHeads += t.Fg339UnmarkedHeads; a.Grows += t.Fg339Grows; a.Sprayed += t.FeatherSprayed;
            a.SprayAlly += t.SprayAlly; a.SprayAllyDealt += t.SprayAllyDealt; a.SprayKills += t.SprayAllyKills; a.SprayKillTurnSum += t.Fg339AllyKillTurnSum;
            for (int i = 0; i < a.Spray.Length; i++)
            {
                a.Spray[i] += t.Fg339Spray?[i] ?? 0; a.AllyHits[i] += t.Fg339AllyHits?[i] ?? 0; a.AllyDmg[i] += t.Fg339AllyDmg?[i] ?? 0;
            }
        }
        var misa = p.FirstOrDefault(u => u.Def.Id == "tome");
        if (misa is not null) a.FeathersEnd = FeathersTrait.Count(misa);
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
        var nw = Pin339(Boards()[4].F); var g = Pin339(Boards()[0].F);   // 第340期: 第334〜339期の規定のミサ
        Console.WriteLine($"# {Tag} §3 Phase 0 —— 規定・seed 0..{Seeds - 1}・1戦あたり");
        Console.WriteLine();
        Console.WriteLine($"## 1. 書き手のいない台「{NoWriter}」（{S307.Seats(nw)}）——守り型のザン ／ ヒサ（標の書き手）をドルガ ／ バンに替えた");
        Console.WriteLine();
        Console.WriteLine("乱射 ＝ 羽の一振りの中で標の敵がいなくて敵味方構わず飛んだ羽。標の無い一振り ＝ 一振りの頭で敵に標持ちが1体もいなかった割合（書き手がいないことの確かめ）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率・決着T | 一振り | 標の無い一振り | 乱射 | 味方に当たった | 味方が受けたダメージ | 乱射で倒れた味方 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var w in Waves)
        {
            var z = Measure(nw, w);
            Console.WriteLine($"| {w.Name} | {F1(z.Win)}%・{Per(z.WinT, z.Wins)}T | {Per(z.Volleys, z.N)} | {Pct(z.UnmarkedHeads, z.Volleys)}% | {Per(z.Sprayed, z.N)} | {Per(z.SprayAlly, z.N)} | {Per(z.SprayAllyDealt, z.N)} | {Per2(z.SprayKills, z.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"## 2. 守り型（{S307.Seats(g)}）: 敵に標持ちのいないミサの一振りの割合");
        Console.WriteLine();
        Console.WriteLine("| 波 | 一振り ／戦 | 標の無い一振り | 割合 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var w in Waves)
        {
            var z = Measure(g, w);
            Console.WriteLine($"| {w.Name} | {Per(z.Volleys, z.N)} | {Per(z.UnmarkedHeads, z.N)} | {Pct(z.UnmarkedHeads, z.Volleys)}% |");
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
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = Measure(Apply(bf, v), w);
        Console.WriteLine($"# {Tag} §5 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("版はミサだけを差し替える（規定のミサ − `FeatherLoss` ＋ 札1枚）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine($"## 1. 怖さの育ち方（{NoWriter}）: ターンごとの乱射 ／ 味方に当たった ／ 味方が受けたダメージ（そのターンまで続いた戦あたり）・乱射で倒れた味方 ／戦（平均のターン）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 勝率・決着T | " + string.Join(" | ", TurnsShown.Select(t => $"T{t}"))+ " | 乱射で倒れた味方（T） | 倒れた味方 ／戦 |");
        Console.WriteLine("|---|---|---|" + string.Concat(TurnsShown.Select(_ => "---|")) + "---|--:|");
        foreach (var w in Waves.Where(w => w.Name is "近衛" or "本編 第5波" or "ボス"))
            foreach (var v in vers)
            {
                var z = res[(NoWriter, v.Name, w.Name)];
                string Cell(int t) => z.Alive[t] == 0 ? "—" : $"{Per(z.Spray[t], z.Alive[t])} ／ {Per(z.AllyHits[t], z.Alive[t])} ／ {Per(z.AllyDmg[t], z.Alive[t])}";
                Console.WriteLine($"| {w.Name} | {v.Name} | {F1(z.Win)}%・{Per(z.WinT, z.Wins)}T | " + string.Join(" | ", TurnsShown.Select(Cell))
                    + $" | {Per2(z.SprayKills, z.N)}（{Per(z.SprayKillTurnSum, z.SprayKills)}） | {Per2(z.AllyDeaths, z.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine("セル ＝ 乱射 ／ 味方に当たった ／ 味方が受けたダメージ。— ＝ そのターンまで続いた戦が無い。");
        Console.WriteLine();

        Console.WriteLine("## 2. 守り型 ／ ボス台: ボスの倒しT・戦の終わりの羽の数・ミサの与ダメの割合・羽が増えた回数 ／戦（全 5 波）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率・決着T | 羽（終わり） | 増えた ／戦 | ミサの割合 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|");
        foreach (var bn in new[] { boards[0].Name, boards[1].Name })
            foreach (var w in Waves)
                foreach (var v in vers)
                {
                    var z = res[(bn, v.Name, w.Name)];
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {F1(z.Win)}%・{Per(z.WinT, z.Wins)}T | {Per(z.FeathersEnd, z.N)} | {Per(z.Grows, z.N)} | {Pct(z.DmgBy.GetValueOrDefault("ミサ"), z.DmgAll)}% |");
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

        Console.WriteLine("## 4. 壊れの兆し（ボスを 2T 以下で倒す ／ 精鋭の 1T 決着 5% 以上 ／ 勝率が規定から 15pt 以上落ちる）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var bad = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                {
                    var z = res[(bn, v.Name, w.Name)]; var b = res[(bn, vers[0].Name, w.Name)];
                    if (w.Name == "ボス" && z.Wins > 0 && (double)z.WinT / z.Wins <= 2.0) bad.Add($"{bn} × ボス {Per(z.WinT, z.Wins)}T");
                    if (IsElite(w) && z.N > 0 && 100.0 * z.OneT / z.N >= 5) bad.Add($"{bn} × {w.Name} 1T 決着 {Pct(z.OneT, z.N)}%");
                    if (z.Win - b.Win <= -15) bad.Add($"{bn} × {w.Name} {Sg(z.Win - b.Win)}");
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
        Console.WriteLine($"ミサのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "tome")).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, Grid(vers[vi]), "tome");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
