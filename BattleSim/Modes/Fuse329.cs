using BattleCore;
using static Common;
using D326 = Dense326Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using T323 = Tou323Diag;

// =====================================================================================
// fuse329 —— 第329期【軽い期】精鋭でも爆竹に: 粉の付いた駒は爆ぜる（PB-a ／ PB-a ＋ b ／ PB-c ／ PB-a ＋ b ＋ c）。
// 指示書は design/PHASE329_POWDER_FUSE_SPEC.md ／ 報告は design/PHASE329_POWDER_FUSE.md。測定の作法は design/MEASURE_LIGHT.md（軽い期）。
//
//     dotnet run --project BattleSim -c Release 0 fuse329 p0                     # §3 Phase 0（規定・ポンの台: 粉の落ちる数・弾けた敵の濃さ・粉の印は残るか）
//     dotnet run --project BattleSim -c Release 0 fuse329 log [波] [seed] [版]   # ポンの台の1戦のログ（読み物）
//     dotnet run --project BattleSim -c Release 0 fuse329 boards [版,…]          # §5 代表台 5 × 5 波 × 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 fuse329 compare [版,…]         # §5 `compare` 64 行 × 版（seed 0..199・基準は規定）
//     dotnet run --project BattleSim -c Release 0 fuse329 check                  # §8 自己検査
//
// 版の名前（ASCII）: base（規定 ＝ DX）／ a（PB-a）／ ab（PB-a ＋ b）／ c（PB-c）／ abc（PB-a ＋ b ＋ c）。波: boss ／ guard ／ bat ／ 2 ／ 5。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・`.tmp/p329/predict.md`）。
// =====================================================================================
static partial class Fuse329Diag
{
    internal const string PredictFile = ".tmp/p329/predict.md";
    const int Seeds = 100;          // 軽い期（design/MEASURE_LIGHT.md §1）
    const int CompareSeeds = 200;   // `compare` の帯（今どおり）
    const string Tag = "第329期";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"fuse329: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "log": Log(A(3, "guard"), int.Parse(A(4, "10")), V(A(5, "ab"))); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "check": Check(); return;
            default: Console.WriteLine("fuse329: モードは p0 / log / boards / compare / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("N0");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static long H(long[]? h, int i) => T323.H(h, i);

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    /// <summary>版。トウだけを Id で差し替える（いない台では何もしない）。</summary>
    internal sealed record Ver(string Name, string Ascii, UnitDef Tou);

    internal static readonly Ver[] Vers =
    {
        new("規定", "base", UnitCatalog.TouH328),   // 第330期: 第328〜329期の規定に固定（いまの規定は PB-a ＋ b）
        new("PB-a", "a", UnitCatalog.TouPBa),
        new("PB-a＋b", "ab", UnitCatalog.TouPBab),
        new("PB-c", "c", UnitCatalog.TouPBc),
        new("PB-全部", "abc", UnitCatalog.TouPBabc),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(f, "tou", v.Tou);

    /// <summary>ポンの台（第326〜328期と同じ席）: 前1 カタ・前3 シガ・中央 トウ・後1 クグ・後3 ソム。第330期: トウは第328〜329期の規定（`TouH328`）に固定。</summary>
    internal static Formation PonBoard() => Formation.Build(front1: UnitCatalog.KataH331, front3: UnitCatalog.Shiga, center: UnitCatalog.TouH328, back1: UnitCatalog.Kugu, back3: UnitCatalog.Som);

    static Formation Playtest(string n) => PlaytestH328.First(r => r.Name == n).F;   // 第332期: カタを第325〜331期の規定に固定（トウは版で差し替える）

    /// <summary>§5 の代表台（駒はいまの規定）。ポンの台を最初に。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("ポンの台", PonBoard()),
        ("光の盾 鞭", Playtest("試遊・感電 光の盾 鞭")),
        ("光の盾", Playtest("試遊・感電 光の盾")),
        ("粉 隣あり", Playtest("試遊・感電 粉 隣あり")),
        ("責め苦", CompareBuildsH328().First(r => r.Name == "責め苦 (トウ×シガ)").F),
    };

    internal static readonly S287.Wave[] Waves = new[] { "boss", "guard", "bat", "2", "5" }.Select(T323.WaveOf).ToArray();
    static S287.Wave WaveOf(string n) => T323.WaveOf(n);
    static bool IsElite(S287.Wave w) => T323.IsElite(w);

    // ---------------------------------------------------------------------------------
    // 1戦の計数（`dense326` の計数をそのまま使う・トウの帳簿は `z.T`）
    // ---------------------------------------------------------------------------------
    internal static D326.Z Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        return D326.CountZ(p, e, r, w);
    }

    internal static D326.Z Measure(Formation f, S287.Wave w, int n = Seeds, int from = 0)
    {
        var parts = new D326.Z[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, from + i));
        var z = new D326.Z();
        foreach (var c in parts) z.Merge(c);
        return z;
    }

    /// <summary>爆発で減らした HP（受け手 <paramref name="rk"/>（0 味方 ／ 1 敵 ／ 2 召喚獣 ／ 3 糸玉）→ 浴びた駒 <paramref name="vk"/>（0 味方 ／ 1 敵 ／ 2 召喚獣））。</summary>
    static long Dx(UnitTally t, int rk, int vk) => H(t.Dx327Dealt, rk * 3 + vk);
    static long DxTo(UnitTally t, int vk) => Enumerable.Range(0, 4).Sum(rk => Dx(t, rk, vk));
    static long DxHitsTo(UnitTally t, int vk) => Enumerable.Range(0, 4).Sum(rk => H(t.Dx327Hits, rk * 3 + vk));
    static long DxN(UnitTally t) => Enumerable.Range(0, 4).Sum(rk => H(t.Dx327N, rk));
    /// <summary>敵の側（敵 ／ 召喚獣 ／ 糸玉）が受け手の爆発の回数。</summary>
    static long DxFoeN(UnitTally t) => H(t.Dx327N, 1) + H(t.Dx327N, 2) + H(t.Dx327N, 3);

    // ---------------------------------------------------------------------------------
    // log（読み物）
    // ---------------------------------------------------------------------------------
    static void Log(string wave, int seed, Ver v)
    {
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(Apply(PonBoard(), v), BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: true);
        Console.WriteLine($"# ポンの台 × {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }
}
