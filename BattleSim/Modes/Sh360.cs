using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using C5 = Db355Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using S8 = Sh358Diag;
using S9 = Sh359Diag;

// =====================================================================================
// sh360 —— 第360期【軽い期】段0: ヒビの HB-f を規定に（＋ ツギの駆け込み ／ 出撃前の板の宛先）／ 本体: ツギなしの勝ち筋をササで（SS-k 構えを解かない ／ SS-w 両隣へ）。
// 指示書は design/PHASE360_SHARD_SASA_SPEC.md ／ 報告は design/PHASE360_SHARD_SASA.md。1戦の計数は `sh358` ／ `sh359` ／ `db355` をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 sh360 p0                  # §3 Phase 0: ササの構え（固めていた ／ いなかった一撃）・倒れた T・ツギなしの台の負け方（1ターンの与ダメ ／ 勇者の最大HP）
//     dotnet run --project BattleSim -c Release 0 sh360 seats               # §5: ササ ／ ヒビ ／ ガレ ＋ 8枚から2枚（28 組）× 席の全並び 120 ＝ 3,360 並び × 規定 ／ SS-k ／ SS-w
//     dotnet run --project BattleSim -c Release 0 sh360 flow [版,…]         # §7 の 1・2: ササの構え ／ ボスへの届き方 × 版（代表台 × ボス ／ 大隊）
//     dotnet run --project BattleSim -c Release 0 sh360 boards [版,…]       # §7 の 3・6: 代表台 × 11 波 × 版（seed 0..99）・壊れの兆し（打ち切りを含む）
//     dotnet run --project BattleSim -c Release 0 sh360 axes                # §7 の 4: 軸の横並び（第348期 §3）の破片の行 × 版
//     dotnet run --project BattleSim -c Release 0 sh360 compare [版,…]      # §7 の 5: `compare` 64 行 × 版（seed 0..199）＋ 段0 の前（`Pin359`）とのセルの差
//     dotnet run --project BattleSim -c Release 0 sh360 log <版> <台> <波> [seed]   # 1戦のログ（台 c ／ cn ／ d ／ 並び「ササ・ゴルム・ドルガ・ガレ・ヒビ」）
//     dotnet run --project BattleSim -c Release 0 sh360 check               # 自己検査
//
// 版の名前（ASCII）: base（規定・段0 入り）／ ssk ／ ssw。版はササだけを差し替える（Id で引く・規定の上）。
// **予測のファイルが無ければ本測定（seats ／ flow ／ boards ／ axes ／ compare）を走らせない**（R397・`.tmp/p360/predict.md`）。
// =====================================================================================
static partial class Sh360Diag
{
    internal const string PredictFile = ".tmp/p360/predict.md";
    const string Tag = "第360期";
    const int Seeds = 100, CompareSeeds = 200, BossSeeds = 5, EliteSeeds = 20, P0Seeds = 20, TrajT = 14;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "seats" or "flow" or "boards" or "axes" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh360: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": Phase0(); return;
            case "seats": SeatsAll(); return;
            case "flow": FlowAll(Pick(A(3, ""))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "axes": AxesAll(); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "log": Log(V(A(3, "base")), A(4, "c"), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh360: モードは p0 / seats / flow / boards / axes / compare / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);

    // ---------------------------------------------------------------------------------
    // 版
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef Sasa);
    internal static readonly Ver[] Vers =
    {
        new("規定（段0 入り）", "base", UnitCatalog.SasaH360),
        new("SS-k 構えを解かない", "ssk", UnitCatalog.SasaSSk),
        new("SS-w 両隣へ", "ssw", UnitCatalog.SasaSSw),
    };
    internal static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(f, "sasa", v.Sasa);
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    // ---------------------------------------------------------------------------------
    // 台
    // ---------------------------------------------------------------------------------
    internal static readonly UnitDef[] Fixed = { UnitCatalog.SasaH360, UnitCatalog.Hibi, UnitCatalog.Gare };
    internal static readonly UnitDef[] Pool = { UnitCatalog.Uro, UnitCatalog.Gald, UnitCatalog.Dolga, UnitCatalog.Golm, UnitCatalog.Nel, UnitCatalog.Ban, UnitCatalog.Kubi, UnitCatalog.Som };
    internal static Formation ByKey(string key)
    {
        var defs = key.Split('・').Select(n => Fixed.Concat(Pool).Append(UnitCatalog.Tsugi).First(d => Short(d) == n)).ToArray();
        var f = new Formation();
        for (int s = 0; s < 5; s++) f[s] = defs[s];
        return f;
    }
    internal const string KeyC = "ササ・ゴルム・ドルガ・ガレ・ヒビ", KeyCN = "ササ・ネル・ドルガ・ガレ・ヒビ", KeyD = "ツギ・ゴルム・ガレ・ネル・ヒビ";
    static Formation Row(string n) => CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F;
    /// <summary>ササのいる `compare` の行から（決めたこと）: 継ぎ当て×分散回復（ツギ ＋ ササ）。</summary>
    internal const string SasaRow = "継ぎ当て×分散回復";
    internal static I.Board[] Boards() => new[]
    {
        new I.Board("破片", $"C 第359期のツギなし（{KeyC}）", ByKey(KeyC)),
    }.Concat(RepKeys.Select(r => new I.Board("破片", $"{r.Label}（{r.Key}）", ByKey(r.Key))))
     .Append(new I.Board("破片", $"D 第359期のツギ＋ネル（{KeyD}）", ByKey(KeyD)))
     .Append(new I.Board("ササ", SasaRow, Row(SasaRow))).ToArray();
    internal static readonly I.W[] Waves = S8.Waves;

    static Formation BoardOf(string n) => n switch
    {
        "c" => ByKey(KeyC), "cn" => ByKey(KeyCN), "d" => ByKey(KeyD),
        _ when n.Contains('・') => ByKey(n),
        _ => CompareBuilds().Concat(CrossBuilds()).Concat(Presets.Versions).First(r => r.Name.StartsWith(n, StringComparison.Ordinal)).F,
    };
    static void Log(Ver v, string board, string wave, int seed)
    {
        var f = Apply(BoardOf(board), v); var w = D.WaveOf(wave);
        var (r, _, _) = I.Raw(f, w, seed, verbose: true);
        Console.WriteLine($"# {Seats(f)} × {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // ササの構えの計数（帳簿だけ・盤面は動かさない）
    // ---------------------------------------------------------------------------------
    internal sealed class Bx
    {
        public long N, Wins, WinT, Draw, Braced, Bare, BareOver, BareOverAmt, Held, Cuts, Refused, Given, Shoves, Lost, SasaDied, SasaT, WideGiven, WideTargets, VeilSoaked, Picked, Broken;
        public long Scattered, ScatterPebble, ScatterScrap, NearGiven, ScrapPicked;   // 第361期（`sh361` が読む）
        public readonly Dictionary<string, long> First = new();
        public void Add(Bx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Draw += o.Draw; Braced += o.Braced; Bare += o.Bare; BareOver += o.BareOver; BareOverAmt += o.BareOverAmt; Held += o.Held; Cuts += o.Cuts; Refused += o.Refused;
            Given += o.Given; Shoves += o.Shoves; Lost += o.Lost; SasaDied += o.SasaDied; SasaT += o.SasaT; WideGiven += o.WideGiven; WideTargets += o.WideTargets; VeilSoaked += o.VeilSoaked; Picked += o.Picked; Broken += o.Broken;
            Scattered += o.Scattered; ScatterPebble += o.ScatterPebble; ScatterScrap += o.ScatterScrap; NearGiven += o.NearGiven; ScrapPicked += o.ScrapPicked;
            foreach (var (k, v) in o.First) First[k] = First.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
    }
    internal static Bx BraceOne(Formation f, I.W w, int seed)
    {
        var (r, p, _) = I.Raw(f, w, seed);
        var b = new Bx { N = 1 };
        if (r.PlayerWon) { b.Wins = 1; b.WinT = r.Turns; }
        else if (p.Any(u => u.IsAlive)) b.Draw = 1;
        var s = p.FirstOrDefault(u => u.Def.Id == "sasa");
        if (s is not null && s.LastDeathTurn > 0) { b.SasaDied = 1; b.SasaT = s.LastDeathTurn; }
        var first = p.Where(u => u.LastDeathTurn > 0).OrderBy(u => u.LastDeathTurn).ThenBy(u => u.Slot).FirstOrDefault();
        if (first is not null) b.First[Short(first.Def)] = 1;
        foreach (var u in p)
        {
            if (!r.TallyByUnit.TryGetValue(u.Def.Id, out var t)) continue;
            b.VeilSoaked += t.SparkVeilSoaked; b.Picked += t.Sh358Picked; b.Broken += t.Sh358ArmorBroken; b.ScrapPicked += t.ScrapArmor;
            if (u.Def.Id != "sasa") continue;
            b.Braced += t.Sh360HitBraced; b.Bare += t.Sh360HitBare; b.BareOver += t.Sh360BareOver; b.BareOverAmt += t.Sh360BareOverAmt; b.Held += t.Sh360HeldCuts;
            b.Cuts += t.BraceCuts; b.Refused += t.BraceRefused; b.Given += t.BraceGiven; b.Shoves += t.BraceShoves; b.Lost += t.BraceLost;
            b.WideGiven += t.Sh360WideGiven; b.WideTargets += t.Sh360WideTargets;
            b.Scattered += t.Sh361Scattered; b.ScatterPebble += t.Sh361ScatterPebble; b.ScatterScrap += t.Sh361ScatterScrap; b.NearGiven += t.Sh361NearGiven;
        }
        return b;
    }
    internal static Bx BraceOf(Formation f, I.W w, int n)
    {
        var parts = new Bx[n];
        Parallel.For(0, n, i => parts[i] = BraceOne(f, w, i));
        var all = new Bx();
        foreach (var x in parts) all.Add(x);
        return all;
    }
    internal static string BraceLine(string title, Bx b) =>
        $"- **{title}**（勝率 {F1(b.Win)}%・打ち切り {Pct(b.Draw, b.N)}%）: 敵の一撃を受けたとき 固めていた {Per2(b.Braced, b.N)} ／ いなかった {Per2(b.Bare, b.N)} 回 ／戦"
        + $"（固めていない一撃のうち 7 を超えた {Per2(b.BareOver, b.N)} 回・超えた量 {Per(b.BareOverAmt, b.N)}）・前のターンの構えで抑えた {Per2(b.Held, b.N)} 回・切り落とした {Per2(b.Cuts, b.N)} 回 ／ {Per(b.Refused, b.N)}"
        + $"・弾いた {Per2(b.Shoves, b.N)} 回・配った {Per(b.Given, b.N)}（両隣 {Per(b.WideGiven, b.N)} ／ 延べ {Per2(b.WideTargets, b.N)} 体）・宛先が無く消えた {Per(b.Lost, b.N)}"
        + $"・ササが倒れた {Pct(b.SasaDied, b.N)}%（T {Per(b.SasaT, b.SasaDied)}）・最初に倒れる駒 {(b.First.Count == 0 ? "—" : string.Join("、", b.First.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key} {Pct(kv.Value, b.N)}%")))}";

    // ---------------------------------------------------------------------------------
    // §3 Phase 0
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = new[] { ($"C（{KeyC}）", ByKey(KeyC)), ($"C-ネル（{KeyCN}）", ByKey(KeyCN)) };
        var waves = new[] { I.Boss, I.Guard, I.Bat };
        Console.WriteLine($"# {Tag} §3 Phase 0 —— 段0 入りの規定（ヒビ ＝ HB-f・ガレ ＝ GR-r）× ツギなしの台 {boards.Length} × ボス ／ 近衛 ／ 大隊・seed 0..{P0Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("## 1〜2. ササの構え・倒れた T・最初に倒れる駒");
        Console.WriteLine();
        Console.WriteLine("ササは速 12、勇者は速 14（近衛 ／ 大隊の新兵は速 6）。身構えは「手番で固め、そのターンのあいだ」。");
        Console.WriteLine();
        foreach (var (name, f) in boards) foreach (var w in waves) Console.WriteLine(BraceLine($"{name} × {w.Name}", BraceOf(f, w, P0Seeds)));
        Console.WriteLine();
        Console.WriteLine("## 3〜4. ボスの戦（1ターンの与ダメ ／ 勇者の最大HP・回復・全滅 T）");
        Console.WriteLine();
        Console.WriteLine("勇者はターン頭に最大HPの 40%（1,200）癒える（減った分まで）。**削りが積み上がるには、1ターンの与ダメが回復（≦ 1,200）を超え続けるか、1ターンで残りの HP を削り切る必要がある**——"
            + "最大HP 3,000 を癒え切らないうちに削り切るには、少なくとも1ターン 1,200 超えを2〜3ターン続ける。ネル（N-100）がいれば勇者が呪われた後の傷は最大HPを削るので、回復の天井ごと下がる。");
        Console.WriteLine();
        foreach (var (name, f) in boards)
        {
            var a = S8.Measure(f, I.Boss, P0Seeds, TrajT);
            S8.PrintTraj($"{name} × ボス（返し ／ 礫 ／ ほか）", a);
            var m = C5.Measure(f, I.Boss, P0Seeds, TrajT);
            C5.PrintTraj($"{name} × ボス（勇者の最大HP）", m);
            long best = Enumerable.Range(1, a.T).Where(t => a.TCnt[t] > 0).Select(t => (a.TRet[t] + a.TPeb[t] + a.TPlank[t] + a.TOther[t]) / a.TCnt[t]).DefaultIfEmpty(0).Max();
            Console.WriteLine($"→ {name}: 1ターンの与ダメの最大 {best}（要る 1,200 超えとの差 {1200 - best}）・全滅 T {Per(a.WipeT, a.Lost)}。");
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
