using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;

// =====================================================================================
// firecycle —— 第246期「三角の循環（ホタから返す）・ホタの段の作り直し・ヒヨの相手選び」＋ 追記（検証用の「的」の波）。
// 指示書は design/PHASE246_FIRE_CYCLE_SPEC.md ／ design/PHASE246_ADDENDUM.md ／ 報告は design/PHASE246_FIRE_CYCLE.md。
// 前段で第245期 E2（敵の火勢）を規定にした（旧は `UnitCatalog.BorgE0`・第245期の器具 `enemyfire` は旧に固定）。
// 版（Q0〜Q2-HP・Q1-選）は札の差し替えだけで作り、`Run` の引数は増やさない。
//
//     dotnet run --project BattleSim -c Release 0 firecycle phase0          # Q0-4（重い波の決着T）・Q0-5（台の駒の数え物）
//     dotnet run --project BattleSim -c Release 0 firecycle pick [dir] [版]  # 段1（版ごとに T3 1,081 組 × 席 120 × seed 40）
//     dotnet run --project BattleSim -c Release 0 firecycle run [dir]       # 段2 ＋ 表A〜I ＋ 的の表 F-1〜F-5
//     dotnet run --project BattleSim -c Release 0 firecycle check           # 自己検査（受け入れ 2〜5・追記 D）
//     dotnet run --project BattleSim -c Release 0 firecycle digest [path]   # Q0 の台本の指紋（engine を触る前後で突き合わせる）
//     dotnet run --project BattleSim -c Release 0 firecycle log <版> <前1,前3,中央,後1,後3> [seed] [波] [倍率 0/1/2]
// =====================================================================================
static partial class FireCycleDiag
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
                LogOne(args.Length > 3 ? args[3] : "Q1", args.Length > 4 ? args[4] : "golm,hisa,borg,hota,hiyo", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("firecycle: モードは phase0 / pick / run / check / digest / log。");
                return;
        }
    }
    static string[]? _args;
    static partial void Phase0();
    static partial void PickImpl();
    static partial void RunImpl();
    static partial void CheckImpl();
    static partial void Digest();

    // ---------------------------------------------------------------------------------
    // 版（§4）——札の差し替えだけ。Q0 は規定の駒そのもの。
    // ---------------------------------------------------------------------------------
    internal static UnitDef With(UnitDef g, IEnumerable<TraitId> tr) => new()
    {
        Id = g.Id, Name = g.Name, MaxHp = g.MaxHp, Attack = g.Attack, Speed = g.Speed, Traits = tr.ToArray(), Pattern = g.Pattern,
        Advances = g.Advances, Actions = g.Actions, PlusText = g.PlusText, MinusText = g.MinusText, Flavor = g.Flavor,
    };
    static UnitDef Plus(UnitDef g, params TraitId[] tr) => With(g, g.Traits.Concat(tr));
    internal static readonly UnitDef BorgR = Plus(UnitCatalog.Borg, TraitId.BorgRadiate);
    internal static readonly UnitDef HiyoQ1 = Plus(UnitCatalog.Hiyo, TraitId.StokePick, TraitId.HiyoSpark);
    internal static readonly UnitDef HiyoPick = Plus(UnitCatalog.Hiyo, TraitId.StokePick);
    internal static readonly UnitDef HotaH5 = Plus(UnitCatalog.HotaQ0, TraitId.PyreCritical);
    internal static readonly UnitDef HotaHP = Plus(UnitCatalog.HotaQ0, TraitId.PyreLance, TraitId.PyreCritical);

    internal static readonly FB.Ver[] Versions =
    {
        new("Q0", "規定（第245期 E2・対照）", UnitCatalog.Borg, UnitCatalog.HotaQ0, UnitCatalog.Hiyo),
        new("Q1", "Q0 ＋ ヒヨの相手選び・育ちの置き換え ＋ ボルグの放熱", BorgR, UnitCatalog.HotaQ0, HiyoQ1),
        new("Q2-H5", "Q1 ＋ ホタ H5 に臨界", BorgR, HotaH5, HiyoQ1),
        new("Q2-HP", "Q1 ＋ ホタ HP（段3 大火槍）＋ 臨界", BorgR, HotaHP, HiyoQ1),
        new("Q1-選", "Q0 ＋ ヒヨの相手選びだけ（切り分け）", UnitCatalog.Borg, UnitCatalog.HotaQ0, HiyoPick),
    };
    internal static FB.Ver VerOf(string name) => Versions.First(v => v.Name == name);

    // ---------------------------------------------------------------------------------
    // 波（§6.2 ＋ Q0-4 の重い波 ＋ 追記 A の的の波）
    // ---------------------------------------------------------------------------------
    /// <summary>0〜5 は第225期以来の6波（第二〜五波・九/新兵・九/農兵）。6 重い波 ／ 7 的・一 ／ 8 的・九。</summary>
    internal static readonly string[] WaveNames = BA.WaveNames.Concat(new[] { "重/城塞", "的・一", "的・九" }).ToArray();
    internal const int WaveHush = 0, WaveYoke = 2, WaveHeavy = 6, WaveTarget1 = 7, WaveTarget9 = 8;
    internal static bool IsTarget(int w) => w >= WaveTarget1;
    /// <summary>的の波の打ち切り（追記 A）: 決着の判定には触らず、台本の 1〜8 ターン目だけを読む。</summary>
    internal const int TargetTurns = 8;

    /// <summary>重い波（Q0-4）: 城塞の重装兵（145/12/3・特性なし）を X 字の前1・前3・中央に3体。軛・詠唱兵・従軍司祭を抜いた第四波。</summary>
    internal static Formation HeavyWave => Formation.Build(front1: EnemyCatalog.Warden, front3: EnemyCatalog.Warden, center: EnemyCatalog.Warden);

    /// <summary>的（追記 A）: HP 9999・攻1・速5・単体・特性なし。<b>この器具のローカル</b>（`EnemyCatalog` にも `compare` にも入れない）。</summary>
    internal static readonly UnitDef Target = new()
    {
        Id = "mato", Name = "的", MaxHp = 9999, Attack = 1, Speed = 5, Traits = Array.Empty<TraitId>(), Pattern = AttackPattern.Single,
        PlusText = "倒れない的（検証用）", MinusText = "", Flavor = "",
    };
    internal static EnemyWave Target1 => EnemyWave.Of((2, Target));
    internal static EnemyWave Target9 => EnemyWave.FillAll(Target);

    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => w switch
    {
        < 6 => BA.WaveOf(w, sc),
        WaveHeavy => () => BattleEngine.Materialize(HeavyWave, BattleContext.EnemyTeam, sc),
        WaveTarget1 => () => BattleEngine.MaterializeEnemy(Target1, EnemyScaleRule.None),   // 倍率は掛けない（追記 A）
        WaveTarget9 => () => BattleEngine.MaterializeEnemy(Target9, EnemyScaleRule.None),
        _ => throw new ArgumentOutOfRangeException(nameof(w)),
    };

    internal static (BattleResult R, List<UnitState> P, List<UnitState> E) Fight(Formation f, int w, EnemyScaleRule sc, int seed, bool verbose = true)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = WaveOf(w, sc)();
        var r = BattleEngine.Run(p, e, seed, verbose: verbose);
        return (r, p, e);
    }

    // ---------------------------------------------------------------------------------
    // 台（§6.1）
    // ---------------------------------------------------------------------------------
    internal static Formation T3238 => FB.T3238;
    internal static Formation T3244 => EnemyFireDiag.T3244;
    internal static Formation ThunderBorg => FB.ThunderBorg;
    internal static Formation Apply(Formation f, FB.Ver v) => EnemyFireDiag.Apply(f, v.Borg, v.Hota, v.Hiyo);

    static void LogOne(string ver, string seats, int seed, int wave, int sc)
    {
        var f = Apply(FB.Dec(seats), VerOf(ver));
        var (r, _, _) = Fight(f, wave, BA.Scales[sc].Sc, seed);
        Console.WriteLine($"# {ver} × {BA.SeatsNamed(f)} × {WaveNames[wave]} × {BA.Scales[sc].Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    internal static string Pct(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1");
    internal static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
}
