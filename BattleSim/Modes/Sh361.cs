using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using S8 = Sh358Diag;
using S9 = Sh359Diag;
using S0 = Sh360Diag;

// =====================================================================================
// sh361 —— 第361期【軽い期】段0: ササの SS-w を規定に ／ 本体: 行き場の無い切り落とし（SS-s 散る ／ SS-n 遠くへ）／ 破片軸の締め。
// 指示書は design/PHASE361_SHARD_CLOSE_SPEC.md ／ 報告は design/PHASE361_SHARD_CLOSE.md ／ まとめは design/SHARD_AXIS_SUMMARY.md。1戦の計数は `sh358` ／ `sh359` ／ `sh360` をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 sh361 p0                  # §3 Phase 0: 割れた破片を読む札の一覧（ソースの走査）・規定（段0 入り）の切り落としの行き先
//     dotnet run --project BattleSim -c Release 0 sh361 seats               # §5: ササ ／ ヒビ ／ ガレ ＋ 8枚から2枚（28 組）× 120 ＝ 3,360 並び × 規定 ／ SS-s ／ SS-n
//     dotnet run --project BattleSim -c Release 0 sh361 flow [版,…]         # §6 の 1・2: 切り落としの行き先 ／ ボスへの届き方 × 版（代表台 × ボス ／ 大隊）
//     dotnet run --project BattleSim -c Release 0 sh361 boards [版,…]       # §6 の 3・5: 代表台 × 11 波 × 版（seed 0..99）・壊れの兆し（打ち切りの増分を含む）
//     dotnet run --project BattleSim -c Release 0 sh361 compare [版,…]      # §6 の 4: `compare` 64 行 × 版 ＋ 段0 の前（`Pin360`）とのセルの差
//     dotnet run --project BattleSim -c Release 0 sh361 log <版> <台> <波> [seed]   # 1戦のログ（台 c ／ f ／ g ／ 並びの名前）
//     dotnet run --project BattleSim -c Release 0 sh361 check               # 自己検査
//
// 版の名前（ASCII）: base（規定・段0 入り）／ sss ／ ssn。版はササだけを差し替える（Id で引く・規定の上）。
// **予測のファイルが無ければ本測定（seats ／ flow ／ boards ／ compare）を走らせない**（R397・`.tmp/p361/predict.md`）。
// =====================================================================================
static partial class Sh361Diag
{
    internal const string PredictFile = ".tmp/p361/predict.md";
    const string Tag = "第361期";
    const int Seeds = 100, CompareSeeds = 200, BossSeeds = 5, EliteSeeds = 20, P0Seeds = 20, TrajT = 14;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "seats" or "flow" or "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh361: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": Phase0(); return;
            case "seats": SeatsAll(); return;
            case "flow": FlowAll(Pick(A(3, ""))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "log": Log(V(A(3, "base")), A(4, "g"), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh361: モードは p0 / seats / flow / boards / compare / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);

    internal sealed record Ver(string Name, string Ascii, UnitDef Sasa);
    internal static readonly Ver[] Vers =
    {
        new("規定（段0 入り・SS-w）", "base", UnitCatalog.SasaH361),
        new("SS-s 散る", "sss", UnitCatalog.SasaSSs),
        new("SS-n 遠くへ", "ssn", UnitCatalog.SasaSSn),
    };
    internal static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(f, "sasa", v.Sasa);
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    internal static readonly UnitDef[] Fixed = { UnitCatalog.SasaH361, UnitCatalog.Hibi, UnitCatalog.Gare };
    internal static readonly UnitDef[] Pool = S0.Pool;
    internal static Formation ByKey(string key)
    {
        var defs = key.Split('・').Select(n => Fixed.Concat(Pool).Append(UnitCatalog.Tsugi).First(d => Short(d) == n)).ToArray();
        var f = new Formation();
        for (int s = 0; s < 5; s++) f[s] = defs[s];
        return f;
    }
    internal const string KeyC = S0.KeyC, KeyF = "バン・ササ・ネル・ヒビ・ガレ", KeyG = "ヒビ・ガレ・ササ・ソム・ネル";
    static Formation Row(string n) => CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F;
    internal static I.Board[] Boards() => new[]
    {
        new I.Board("破片", $"C（{KeyC}）", ByKey(KeyC)),
        new I.Board("破片", $"F（{KeyF}）", ByKey(KeyF)),
        new I.Board("破片", $"G（{KeyG}）", ByKey(KeyG)),
        new I.Board("破片", $"H 試遊・破片（{PlayKey}）", ByKey(PlayKey)),
        new I.Board("ササ", S0.SasaRow, Row(S0.SasaRow)),
    };
    internal static readonly I.W[] Waves = S8.Waves;
    static Formation BoardOf(string n) => n switch
    {
        "c" => ByKey(KeyC), "f" => ByKey(KeyF), "g" => ByKey(KeyG),
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

    /// <summary>切り落としの行き先の1行（`sh360` の帳簿 ＋ 第361期の欄）。</summary>
    static string Dest(string title, S0.Bx b) =>
        $"- **{title}**（勝率 {F1(b.Win)}%・打ち切り {Pct(b.Draw, b.N)}%）: 切り落とした {Per(b.Refused, b.N)} → 隣に付けた {Per(b.WideGiven, b.N)} ／ 遠くへ付けた {Per(b.NearGiven, b.N)} ／ 散った {Per(b.Scattered, b.N)}"
        + $"（拾った: 礫 {Per(b.ScatterPebble, b.N)}・瓦礫 {Per(b.ScatterScrap, b.N)}）／ 消えた {Per(b.Lost, b.N)}・ササが倒れた {Pct(b.SasaDied, b.N)}%（T {Per(b.SasaT, b.SasaDied)}）";

    // ---------------------------------------------------------------------------------
    // §3 Phase 0
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} Phase 0 —— 割れた破片を読む札 ／ 規定（段0 入り・SS-w）の切り落としの行き先");
        Console.WriteLine();
        Console.WriteLine("## 1. 割れた破片（`StatusKeys.Armor` が減った ／ 敵の一撃で割れた）を読む札（engine の破片の段 `ApplyDamageBody` と `NoteArmorLost` を走査）");
        Console.WriteLine();
        string eng = File.ReadAllText("BattleCore/BattleEngine.cs");
        string trs = File.ReadAllText("BattleCore/Traits.cs");
        (string Who, string Trait, string What, string Hook, string Probe, bool Scatter)[] rows =
        {
            ("ガレ", "PebbleGather", "味方（自分を含む）の破片が敵の一撃で割れた量を礫に", "破片の段（`PebbleTrait.Gather`）", "PebbleTrait.Gather(", true),
            ("ツギ", "Scrap", "同じ陣営の破片が減った量の 50% を瓦礫（次の板の厚さ）に", "`NoteArmorLost`（`ScrapTrait.Pick`）", "ScrapTrait.Pick(", true),
            ("ツギ", "PlankSkill ／ AidSkill", "板の印のある駒が敵の一撃で失った破片の累計で腕が上がる", "破片の段（`NotePlankSkill`・板の返しと同じ条件）", "NotePlankSkill(", false),
            ("ツギ", "PlankRebound", "板が敵の一撃で割れた分を一撃の主へ返す（返し）", "破片の段（`ReflectPlank`）", "ReflectPlank(", false),
            ("ヒビ", "ShardGift ／ ShardFull", "返しの欠片が敵の一撃で割れたら一撃の主へ返す（返し）", "破片の段（`ShardReturn358`）", "ShardReturn358(", false),
            ("ソム", "SparkVeil", "衣の帳簿を減らす（計数のみ・規則は読まない）", "`NoteArmorLost`", "SparkTrait.VeilKey", false),
            ("ウロ", "Scale", "破片を纏っている間は貫き（二値・在庫を読む・割れた量は読まない）", "—", "class ScaleTrait", false),
        };
        Console.WriteLine("| 駒 | 札 | 読むもの | 口 | 散った破片を拾うか（SS-s） | ソースにあるか |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var r in rows)
            Console.WriteLine($"| {r.Who} | `{r.Trait}` | {r.What} | {r.Hook} | {(r.Scatter ? "**拾う**" : r.Trait.Contains("Rebound") || r.Trait.Contains("Shard") ? "拾わない（返しは起こさない）" : "拾わない")} | {(eng.Contains(r.Probe) || trs.Contains(r.Probe) ? "○" : "×")} |");
        Console.WriteLine();
        Console.WriteLine("→ 散った破片を拾うのは **ガレ（礫）とツギ（瓦礫）の2枚**。ツギの腕（板の印の破片の累計）と返し（板 ／ 欠片）は「敵の一撃が板 ／ 欠片を割った」ことが条件なので拾わない。");
        Console.WriteLine();
        Console.WriteLine($"## 2. 規定（段0 入り）の切り落としの行き先（代表台 × ボス ／ 大隊・seed 0..{P0Seeds - 1}）");
        Console.WriteLine();
        foreach (var b in Boards()) foreach (var w in new[] { I.Boss, I.Bat }) Console.WriteLine(Dest($"{b.Name} × {w.Name}", S0.BraceOf(Apply(b.F, Vers[0]), w, P0Seeds)));   // 第362期: 規定のササが SS-n になったので第361期の規定（`SasaH361`）に固定
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
