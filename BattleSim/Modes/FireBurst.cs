using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FS = FireScaleDiag;

// =====================================================================================
// fireburst —— 第244期「火勢の大技（焼き尽くす・残り火・放つ・呼び火）」。
// 指示書は design/PHASE243_FIRE_BURST_SPEC.md（第243期はハネ・バサで使ったので第244期として回す）／ 報告は design/PHASE244_FIRE_BURST.md。
// 前段で第242期 R3 を規定にした（旧は `UnitCatalog.BorgL0` / `HotaL0` / `HiyoL0`・第239〜242期の器具は `Common.OldLevel` 側に固定）。
// 版（S0〜S2R-g4）は札の差し替えだけで作り、`Run` の引数は増やさない。
//
//     dotnet run --project BattleSim -c Release 0 fireburst phase0          # Q0-6（S0 ＝ 規定の駒で、§6 の台を数える）
//     dotnet run --project BattleSim -c Release 0 fireburst pick [dir] [版]  # 段1（版ごとに T3 1,081 組 × 席 120 × seed 40）
//     dotnet run --project BattleSim -c Release 0 fireburst run [dir]       # 段2 ＋ 表A〜J
//     dotnet run --project BattleSim -c Release 0 fireburst check           # 自己検査（受け入れ 2〜5）
//     dotnet run --project BattleSim -c Release 0 fireburst log <版> <前1,前3,中央,後1,後3> [seed] [波] [倍率 0/1/2]
// =====================================================================================
static partial class FireBurstDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        _args = args;
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "pick": PickImpl(); return;
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            case "digest": Digest(); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "S2R", args.Length > 4 ? args[4] : "hiyo,hota,borg,doha,sora", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("fireburst: モードは phase0 / pick / run / check / log。");
                return;
        }
    }
    static string[]? _args;
    static partial void Phase0();
    static partial void PickImpl();
    static partial void RunImpl();
    static partial void CheckImpl();
    static partial void LogOne(string ver, string seats, int seed, int wave, int sc);

    internal const int WaveHush = 0, WaveYoke = 2;

    /// <summary>1戦（駒は渡されたまま・固定しない）。</summary>
    internal static (BattleResult R, List<UnitState> P, List<UnitState> E) Fight(Formation f, int w, EnemyScaleRule sc, int seed, bool verbose = true)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BA.WaveOf(w, sc)();
        var r = BattleEngine.Run(p, e, seed, verbose: verbose, ember: EmberRule.Pre256);
        return (r, p, e);
    }

    /// <summary>台（規定の駒で組む。版は <c>Apply</c> で差し替える）。</summary>
    internal static Formation T3238 => Formation.Build(front1: UnitCatalog.Hiyo, front3: UnitCatalog.Hota, center: UnitCatalog.Borg, back1: UnitCatalog.Doha, back3: UnitCatalog.Sora);
    internal static Formation ThunderBorg => Formation.Build(front1: UnitCatalog.Borg, front3: UnitCatalog.Tsugi, center: UnitCatalog.Beni, back1: UnitCatalog.KataS3, back3: UnitCatalog.Mio);
    /// <summary>第242期 R3 の段1 の1位（ササ・ガレ・ボルグ前3）。Phase 0 の仮の T3。</summary>
    internal static Formation T3r3 => Formation.Build(front1: UnitCatalog.Sasa, front3: UnitCatalog.Borg, center: UnitCatalog.Hota, back1: UnitCatalog.Gare, back3: UnitCatalog.Hiyo);

    internal static string Pct(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1");
    internal static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    internal static int RoleOf(string id) => id switch { "borg" => 0, "hota" => 1, "hiyo" => 2, _ => 3 };
    internal static readonly string[] RoleNames = { "ボルグ", "ホタ", "ヒヨ", "相方" };
}
