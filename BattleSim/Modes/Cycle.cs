using BattleCore;
using static Common;

// =====================================================================================
// cycle —— 第230期「移動軸の循環：ヨミの撃破の衝撃・シオの溢れを攻撃力に・追い風は戦う者が踏み込む」。
// 指示書は design/PHASE230_CYCLE_SPEC.md ／ 報告は design/PHASE230_CYCLE.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 cycle phase0   # Q0-1〜Q0-6（W0 ＝ 前段の規定だけで回る）
//     dotnet run --project BattleSim -c Release 0 cycle run      # 表A〜F
//     dotnet run --project BattleSim -c Release 0 cycle check    # 自己検査（受け入れ 2〜4・7）
// =====================================================================================
static partial class CycleDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            default:
                Console.WriteLine("cycle: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void Phase0();
    static partial void RunImpl();
    static partial void CheckImpl();

    internal const int Seeds = 200;

    // 台（§8.1）。バサ・ハネは前段の規定（嵐・追い風）。
    /// <summary>第228期 H3 の総当たりの1位（前1 バサ ／ 前3 セロ ／ 中央 ヨミ ／ 後1 シオ ／ 後3 ハネ）。</summary>
    internal static Formation MHane228 => Formation.Build(front1: UnitCatalog.Basa, front3: UnitCatalog.Sero,
        center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.Hane);
    /// <summary>第229期 G4 の総当たりの1位（前1 シオ ／ 前3 バサ ／ 中央 セロ ／ 後1 ヨミ ／ 後3 ハネ）。Phase 0 の仮の席。</summary>
    internal static Formation MHane229 => Formation.Build(front1: UnitCatalog.Shio, front3: UnitCatalog.Basa,
        center: UnitCatalog.Sero, back1: UnitCatalog.Yomi, back3: UnitCatalog.Hane);
    /// <summary>参考: ポンの席（前1 シガ ／ 前3 ツギ ／ 中央 ベニ ／ 後1 カタ ／ 後3 ミオ）。</summary>
    internal static Formation Thunder => GaleDiag.Thunder;

    internal static readonly string[] WaveNames = GaleDiag.WaveNames;
    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => GaleDiag.WaveOf(w, sc);
    internal const int MainWave = GaleDiag.MainWave;
    internal static readonly (string Name, EnemyScaleRule Sc)[] Scales = GaleDiag.Scales;
    internal static string SeatsNamed(Formation f) => GaleDiag.SeatsNamed(f);
    internal static string F1(double x) => GaleDiag.F1(x);
    internal static string F2(double x) => GaleDiag.F2(x);
    internal static long Tally(UnitTally t, string name) => GaleDiag.Tally(t, name);

    /// <summary>1戦を回す（台本つき・<b>今の既定の規則</b>＝転倒の穴あり）。</summary>
    internal static (BattleResult R, List<UnitState> P, List<UnitState> E, Dictionary<int, int> Slot0) Fight(
        Formation f, int w, EnemyScaleRule sc, int seed, bool verbose = true)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = WaveOf(w, sc)();
        var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
        var r = BattleEngine.Run(p, e, seed, verbose: verbose);
        var slot0 = slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value);
        return (r, p, e, slot0);
    }
}
