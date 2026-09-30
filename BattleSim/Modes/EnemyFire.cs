using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;

// =====================================================================================
// enemyfire —— 第245期「敵の火勢（敵には災害）と、煽りの比べ方の直し」。
// 指示書は design/PHASE245_ENEMY_FIRE_SPEC.md ／ 報告は design/PHASE245_ENEMY_FIRE.md。
// 前段で煽りの比べ方（P- ／ P′）を決めて大技を規定にする。本線の版（E0〜E2）は札の差し替えだけで作り、`Run` の引数は増やさない。
//
//     dotnet run --project BattleSim -c Release 0 enemyfire pre              # 前段（P- ／ P′ ／ 参考 S2R × T3-238 ／ T3-244）
//     dotnet run --project BattleSim -c Release 0 enemyfire phase0           # Q0-6（E0 ＝ 規定の駒で §6 の台を数える）
//     dotnet run --project BattleSim -c Release 0 enemyfire pick [dir] [版]  # 段1（版ごとに T3 1,081 組 × 席 120 × seed 40）
//     dotnet run --project BattleSim -c Release 0 enemyfire run [dir]        # 段2 ＋ 表A〜F
//     dotnet run --project BattleSim -c Release 0 enemyfire check            # 自己検査（受け入れ 2〜5）
//     dotnet run --project BattleSim -c Release 0 enemyfire digest [path]    # E0 の台本の指紋
//     dotnet run --project BattleSim -c Release 0 enemyfire log <版> <前1,前3,中央,後1,後3> [seed] [波] [倍率 0/1/2]
// =====================================================================================
static partial class EnemyFireDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        _args = args;
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "pre": PreImpl(); return;
            case "phase0": Phase0(); return;
            case "pick": PickImpl(); return;
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            case "digest": Digest(); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "E1", args.Length > 4 ? args[4] : "golm,hisa,borg,hota,hiyo", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("enemyfire: モードは pre / phase0 / pick / run / check / digest / log。");
                return;
        }
    }
    static string[]? _args;
    static partial void PreImpl();
    static partial void Phase0();
    static partial void PickImpl();
    static partial void RunImpl();
    static partial void CheckImpl();
    static partial void Digest();
    static partial void LogOne(string ver, string seats, int seed, int wave, int sc);

    internal static UnitDef With(UnitDef g, IEnumerable<TraitId> tr, string? plus = null) => new()
    {
        Id = g.Id, Name = g.Name, MaxHp = g.MaxHp, Attack = g.Attack, Speed = g.Speed, Traits = tr.ToArray(), Pattern = g.Pattern,
        Advances = g.Advances, Actions = g.Actions, PlusText = plus ?? g.PlusText, MinusText = g.MinusText, Flavor = g.Flavor,
    };

    /// <summary>台（駒は何でもよい——版は <c>Apply</c> で差し替える）。</summary>
    internal static Formation T3238 => FB.T3238;
    /// <summary>第244期 S2R の段1 の1位（前1 ゴルム ／ 前3 ヒサ ／ 中央 ボルグ ／ 後1 ホタ ／ 後3 ヒヨ）。</summary>
    internal static Formation T3244 => Formation.Build(front1: UnitCatalog.Golm, front3: UnitCatalog.Hisa, center: UnitCatalog.Borg, back1: UnitCatalog.Hota, back3: UnitCatalog.Hiyo);
    internal static Formation ThunderBorg => FB.ThunderBorg;

    /// <summary>ボルグ・ホタ・ヒヨを渡された駒に差し替える（ほかの駒・席はそのまま）。</summary>
    internal static Formation Apply(Formation f, UnitDef borg, UnitDef hota, UnitDef hiyo)
    {
        UnitDef? M(UnitDef? d) => d is null ? null : d.Id switch { "borg" => borg, "hota" => hota, "hiyo" => hiyo, _ => d };
        return Formation.Build(front1: M(f[0]), front3: M(f[1]), center: M(f[2]), back1: M(f[3]), back3: M(f[4]));
    }

    internal static string Pct(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1");
    internal static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
}
