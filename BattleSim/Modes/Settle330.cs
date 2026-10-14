using BattleCore;
using static Common;
using D326 = Dense326Diag;
using F329 = Fuse329Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using T323 = Tou323Diag;

// =====================================================================================
// settle330 —— 第330期【重い期・規定化だけ】粉の導火線（PB-a ＋ b）を規定にする。
// 指示書は design/PHASE330_POWDER_FUSE_SETTLE_SPEC.md ／ 報告は design/PHASE330_POWDER_FUSE_SETTLE.md。
//
//     dotnet run --project BattleSim -c Release 0 settle330 boards                     # §4 代表台 5 × 10 波 × 旧 ／ 新（seed 0..199）
//     dotnet run --project BattleSim -c Release 0 settle330 grid                       # §4 格子1本: 近衛・固定枠 トウ ＋ カタ（精鋭が壊れていないか）
//     dotnet run --project BattleSim -c Release 0 settle330 log [台] [波] [seed] [old|new] # 1戦のログ（台 pon ／ whip ／ shield ／ near ／ torment）
//     dotnet run --project BattleSim -c Release 0 settle330 check                      # §7 自己検査
//
// 旧 ＝ 第328〜329期の規定（`TouH328`・`Common.Pin328`）。新 ＝ いまの規定（PB-a ＋ b）。
// **予測のファイルが無ければ本測定（boards ／ grid）を走らせない**（R397・`.tmp/p330/predict.md`）。
// =====================================================================================
static partial class Settle330Diag
{
    internal const string PredictFile = ".tmp/p330/predict.md";
    const int Seeds = 200;   // 重い期（design/MEASURE_LIGHT.md §1）
    const string Tag = "第330期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"settle330: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "log": Log(A(3, "pon"), A(4, "bat"), int.Parse(A(5, "0")), A(6, "new") == "old"); return;
            case "boards": BoardsAll(); return;
            case "grid": Grid(); return;
            case "check": Check(); return;
            default: Console.WriteLine("settle330: モードは log / boards / grid / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("N0");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static long H(long[]? h, int i) => T323.H(h, i);
    static string Short(UnitDef d) => S307.Short(d);

    /// <summary>ポンの台（第326〜329期と同じ席）。駒はいまの規定。</summary>
    internal static Formation PonBoard() => Formation.Build(front1: UnitCatalog.Kata, front3: UnitCatalog.Shiga, center: UnitCatalog.Tou, back1: UnitCatalog.Kugu, back3: UnitCatalog.Som);
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;

    /// <summary>§4 の代表台（駒はいまの規定）。旧は <see cref="Pin328"/> を通す。</summary>
    internal static (string Name, string Ascii, Formation F)[] Boards() => new (string, string, Formation)[]
    {
        ("ポンの台", "pon", PonBoard()),
        ("光の盾 鞭", "whip", Playtest("試遊・感電 光の盾 鞭")),
        ("光の盾", "shield", Playtest("試遊・感電 光の盾")),
        ("粉 隣あり", "near", Playtest("試遊・感電 粉 隣あり")),
        ("責め苦", "torment", Row("責め苦 (トウ×シガ)")),
    };

    internal static readonly S287.Wave[] Waves = T323.Waves;
    static S287.Wave WaveOf(string n) => T323.WaveOf(n);
    static bool IsElite(S287.Wave w) => T323.IsElite(w);

    static long Dx(UnitTally t, int rk, int vk) => H(t.Dx327Dealt, rk * 3 + vk);
    static long DxTo(UnitTally t, int vk) => Enumerable.Range(0, 4).Sum(rk => Dx(t, rk, vk));
    static long DxHitsTo(UnitTally t, int vk) => Enumerable.Range(0, 4).Sum(rk => H(t.Dx327Hits, rk * 3 + vk));
    static long DxN(UnitTally t) => Enumerable.Range(0, 4).Sum(rk => H(t.Dx327N, rk));
    static long DxFoeN(UnitTally t) => H(t.Dx327N, 1) + H(t.Dx327N, 2) + H(t.Dx327N, 3);

    static void Log(string board, string wave, int seed, bool old)
    {
        var (name, _, f0) = Boards().First(b => b.Ascii == board || b.Name == board);
        var w = WaveOf(wave);
        var f = old ? Pin328(f0) : f0;
        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# {name} × {w.Name} × seed {seed} × {(old ? "旧（第328〜329期の規定）" : "新（規定）")}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    static D326.Z Measure(Formation f, S287.Wave w) => F329.Measure(f, w, Seeds);
}
