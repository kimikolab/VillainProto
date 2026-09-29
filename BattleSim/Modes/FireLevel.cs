using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FS = FireScaleDiag;

// =====================================================================================
// firelevel —— 第242期「火勢を入れる（燃え広がり・ヒヨのターンギフト・ホタの段）」。
// 指示書は design/PHASE242_FIRE_LEVEL_SPEC.md ／ 報告は design/PHASE242_FIRE_LEVEL.md。
// **この期から盤面が変わる**（影ではなく実装）。**規定は変えない**——版（R0〜R3g4）は札の差し替えだけで作り、`Run` の引数は増やさない。
//
//     dotnet run --project BattleSim -c Release 0 firelevel phase0          # Q0-7（R0 の台の数え物）
//     dotnet run --project BattleSim -c Release 0 firelevel pick [dir]      # 段1（版ごとに T3 1,081 組 × 席 120 × seed 40）
//     dotnet run --project BattleSim -c Release 0 firelevel run [dir]       # 段2 ＋ 表A〜H
//     dotnet run --project BattleSim -c Release 0 firelevel check           # 自己検査（受け入れ 3〜5）
//     dotnet run --project BattleSim -c Release 0 firelevel digest [path]   # 台本の指紋（R0 が第241期と一致すること）
//     dotnet run --project BattleSim -c Release 0 firelevel log <版> <前1,前3,中央,後1,後3> [seed] [波] [倍率 0/1/2]
// =====================================================================================
static partial class FireLevelDiag
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
            case "digest": FS.Run(new[] { args[0], args[1], "digest", args.Length > 3 ? args[3] : "firelevel_digest.txt" }, stageIndex); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "R3", args.Length > 4 ? args[4] : "hiyo,hota,borg,doha,sora", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("firelevel: モードは phase0 / pick / run / check / digest / log。");
                return;
        }
    }
    static string[]? _args;
    static partial void Phase0();
    static partial void PickImpl();
    static partial void RunImpl();
    static partial void CheckImpl();
    static partial void LogOne(string ver, string seats, int seed, int wave, int sc);

    /// <summary>主な波: 本編の第2〜5波（0..3）・九/新兵（4・主判定）・九/農兵（5）。</summary>
    internal const int WaveHush = 0, WaveYoke = 2;

    /// <summary>1戦（駒は渡されたまま・固定しない）。</summary>
    internal static (BattleResult R, List<UnitState> P, List<UnitState> E) Fight(Formation f, int w, EnemyScaleRule sc, int seed, bool verbose = true)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = BA.WaveOf(w, sc)();
        var r = BattleEngine.Run(p, e, seed, verbose: verbose);
        return (r, p, e);
    }

    internal static string Pct(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1");
    internal static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
}
