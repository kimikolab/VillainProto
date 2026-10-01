using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// =====================================================================================
// firetri —— 第247期「三角の循環を規則にする（放熱で指名・大技の火の粉）」。
// 指示書は design/PHASE247_FIRE_TRIANGLE_SPEC.md ／ 報告は design/PHASE247_FIRE_TRIANGLE.md。
// 前段で第246期 Q2-HP のホタの段（大火槍・臨界）を規定にした（旧は `UnitCatalog.HotaQ0`・第245・246期の器具は旧に固定）。
// 版（T0〜T1-放粉）は札の差し替えだけで作り、`Run` の引数は増やさない。波・倍率・台は第246期（`firecycle`）と同じ。
//
//     dotnet run --project BattleSim -c Release 0 firetri phase0          # Q0-4（段2・段3 の台本）・Q0-5（T0 で台の駒を数える）
//     dotnet run --project BattleSim -c Release 0 firetri pick [dir] [版]  # 段1（版ごとに T3 1,081 組 × 席 120 × seed 40）
//     dotnet run --project BattleSim -c Release 0 firetri run [dir]       # 段2 ＋ 表A〜I
//     dotnet run --project BattleSim -c Release 0 firetri check           # 自己検査（受け入れ 2〜5）
//     dotnet run --project BattleSim -c Release 0 firetri digest [path]   # T0 の台本の指紋（札の実装の前後で突き合わせる）
//     dotnet run --project BattleSim -c Release 0 firetri log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]
// =====================================================================================
static partial class FireTriDiag
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
            case "digest": FireTriDigestDiag.Run(args); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "T1", args.Length > 4 ? args[4] : "golm,hisa,borg,hota,hiyo", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("firetri: モードは phase0 / pick / run / check / digest / log。");
                return;
        }
    }
    static string[]? _args;
    static partial void Phase0();
    static partial void PickImpl();
    static partial void RunImpl();
    static partial void CheckImpl();

    // ---------------------------------------------------------------------------------
    // 版（§4）——札の差し替えだけ。T0 は規定の駒そのもの。
    // ---------------------------------------------------------------------------------
    static UnitDef Plus(UnitDef g, params TraitId[] tr) => FC.With(g, g.Traits.Concat(tr));
    /// <summary>ボルグ ＋ 放熱（指名）。</summary>
    internal static readonly UnitDef BorgC = Plus(UnitCatalog.BorgU0, TraitId.RadiateCall);
    /// <summary>旧育ち（規定の育ち・P′）＋ 火の粉（焼き尽くす・放つ）。</summary>
    internal static readonly UnitDef HiyoOld = Plus(UnitCatalog.HiyoU0, TraitId.SparkCatch, TraitId.SparkUnleash);
    /// <summary>新育ち（第246期 Q1: 相手選び・燃え広がりでは育たず焼き尽くすの火の粉）＋ 放つの火の粉。</summary>
    internal static readonly UnitDef HiyoNew = Plus(UnitCatalog.HiyoU0, TraitId.StokePick, TraitId.HiyoSpark, TraitId.SparkUnleash);
    internal static readonly UnitDef HiyoOldB = Plus(HiyoOld, TraitId.GiftPair);
    internal static readonly UnitDef HiyoNewB = Plus(HiyoNew, TraitId.GiftPair);
    /// <summary>T1 から「ボルグの放つでの火の粉」を抜いた（切り分け）。</summary>
    internal static readonly UnitDef HiyoOldNoU = Plus(UnitCatalog.HiyoU0, TraitId.SparkCatch);

    internal static readonly FB.Ver[] Versions =
    {
        new("T0", "前段の規定（対照）", UnitCatalog.BorgU0, UnitCatalog.Hota, UnitCatalog.HiyoU0),
        new("T1", "T0 ＋ 放熱（指名）＋ 火の粉（両大技）＋ 旧育ち", BorgC, UnitCatalog.Hota, HiyoOld),
        new("T2", "T0 ＋ 放熱（指名）＋ 火の粉（両大技）＋ 新育ち", BorgC, UnitCatalog.Hota, HiyoNew),
        new("T1b", "T1 ＋ (b) 準備のできた2体に渡す", BorgC, UnitCatalog.Hota, HiyoOldB),
        new("T2b", "T2 ＋ (b) 準備のできた2体に渡す", BorgC, UnitCatalog.Hota, HiyoNewB),
        new("T1-放粉", "T1 から放つの火の粉を抜く（切り分け）", BorgC, UnitCatalog.Hota, HiyoOldNoU),
    };
    internal static FB.Ver VerOf(string name) => Versions.First(v => v.Name == name);
    internal static Formation Apply(Formation f, FB.Ver v) => FC.Apply(f, v);

    // 波・台は第246期のまま（`FireCycleDiag`）
    internal static string[] WaveNames => FC.WaveNames;
    internal const int WaveHush = FC.WaveHush, WaveYoke = FC.WaveYoke, WaveHeavy = FC.WaveHeavy, WaveTarget1 = FC.WaveTarget1, WaveTarget9 = FC.WaveTarget9;
    internal static bool IsTarget(int w) => FC.IsTarget(w);
    internal const int TargetTurns = FC.TargetTurns;
    internal static Formation T3238 => FC.T3238;
    internal static Formation T3244 => FC.T3244;
    internal static Formation ThunderBorg => FC.ThunderBorg;

    static void LogOne(string ver, string seats, int seed, int wave, int sc)
    {
        var f = Apply(FB.Dec(seats), VerOf(ver));
        var (r, _, _) = FC.Fight(f, wave, BA.Scales[sc].Sc, seed);
        Console.WriteLine($"# {ver} × {BA.SeatsNamed(f)} × {WaveNames[wave]} × {BA.Scales[sc].Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    internal static string Pct(long a, long n) => FC.Pct(a, n);
    internal static string Per(long a, long n) => FC.Per(a, n);

    // ---------------------------------------------------------------------------------
    // 段1（第246期の器具をそのまま・版と置き場所だけ替える）
    // ---------------------------------------------------------------------------------
    static string Dir => _args is { Length: > 3 } ? _args[3] : Path.GetTempPath();
    static string PickPath(string ver) => Path.Combine(Dir, $"firetri247_pick_T3_{ver}.tsv");

    internal static List<FB.Pick> PicksOf(FB.Ver v, out double sec)
    {
        string path = PickPath(v.Name);
        if (File.Exists(path)) return FC.LoadPick(path, out sec);
        var ps = FC.RunPick(v, out sec);
        FC.SavePick(ps, path, sec);
        return ps;
    }

    static partial void PickImpl()
    {
        var only = _args is { Length: > 4 } ? _args[4] : null;
        foreach (var v in Versions)
        {
            if (only is not null && v.Name != only) continue;
            string path = PickPath(v.Name);
            if (File.Exists(path)) { Console.WriteLine($"{v.Name}: {path} は既にある（消せば回し直す）"); continue; }
            var ps = PicksOf(v, out double sec);
            Console.WriteLine($"{v.Name}: 段1 を {path} に書いた（{ps.Count} 組・{sec / 60:F1} 分）");
        }
    }
}
