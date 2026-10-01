using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// =====================================================================================
// fireatk —— 第250期「ホタの攻撃力の育ち（あぶれた火・くべられる火）・焼き尽くすの重さ・ヒヨのいない爆炎」。
// 指示書は design/PHASE250_FIRE_FEED_ATK_SPEC.md ／ 報告は design/PHASE250_FIRE_FEED_ATK.md。
// 版（§4・積み上げ）は札の差し替えだけで作り、`Run` の引数は増やさない。波・倍率・台は第249期（`firefinish`）と同じ。
//   L0 ＝ 規定（第250期 前段 ＝ 第249期 K4・対照）
//   L-A1 ＝ L0 ＋ あぶれた火（`PyreOverflow`）     L-A2 ＝ L0 ＋ くべられる火（`PyreFed`）
//   L1 ＝ L0 ＋ 両方                               L2 ＝ L1 ＋ 焼き尽くす・重（`BurnoutHeavy`）
//   L3 ＝ L2 ＋ 爆炎・独り（`BlazeSolo`）
//
//     dotnet run --project BattleSim -c Release 0 fireatk phase0          # Q0-1〜Q0-4（L0 で台を数える・予測の材料）
//     dotnet run --project BattleSim -c Release 0 fireatk pick [dir] [版]  # T3 の段1（L0 と L3 だけ・1,081 組 × 席 120 × seed 40）
//     dotnet run --project BattleSim -c Release 0 fireatk run [dir]       # 段2 ＋ 表A〜G
//     dotnet run --project BattleSim -c Release 0 fireatk check           # 自己検査（受け入れ 2〜5）
//     dotnet run --project BattleSim -c Release 0 fireatk digest [path]   # L0 の台本の指紋（`FireAtkDigestDiag`・このファイルだけで閉じる）
//     dotnet run --project BattleSim -c Release 0 fireatk log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]
// =====================================================================================
static partial class FireAtkDiag
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
            case "digest": FireAtkDigestDiag.Run(args); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "L3", args.Length > 4 ? args[4] : "golm,hisa,borg,hota,hiyo", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("fireatk: モードは phase0 / pick / run / check / digest / log。");
                return;
        }
    }
    static string[]? _args;
    static partial void Phase0();
    static partial void RunImpl();
    static partial void CheckImpl();

    // ---------------------------------------------------------------------------------
    // 版（§4）
    // ---------------------------------------------------------------------------------
    static UnitDef Plus(UnitDef g, params TraitId[] tr) => FC.With(g, g.Traits.Concat(tr));
    internal static readonly UnitDef HotaA1 = Plus(UnitCatalog.Hota, TraitId.PyreOverflow);
    internal static readonly UnitDef HotaA2 = Plus(UnitCatalog.Hota, TraitId.PyreFed);
    internal static readonly UnitDef HotaL1 = Plus(UnitCatalog.Hota, TraitId.PyreOverflow, TraitId.PyreFed);
    internal static readonly UnitDef HotaL2 = Plus(HotaL1, TraitId.BurnoutHeavy);
    internal static readonly UnitDef BorgL3 = Plus(UnitCatalog.Borg, TraitId.BlazeSolo);

    internal static readonly FB.Ver[] Versions =
    {
        new("L0", "規定（第250期 前段 ＝ 第249期 K4・対照）", UnitCatalog.Borg, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("L-A1", "L0 ＋ あぶれた火（火勢4 の育ち → 攻撃力 +4）", UnitCatalog.Borg, HotaA1, UnitCatalog.Hiyo),
        new("L-A2", "L0 ＋ くべられる火（味方の着火 → 攻撃力 +2）", UnitCatalog.Borg, HotaA2, UnitCatalog.Hiyo),
        new("L1", "L0 ＋ あぶれた火 ＋ くべられる火", UnitCatalog.Borg, HotaL1, UnitCatalog.Hiyo),
        new("L2", "L1 ＋ 焼き尽くす・重（全体の1発 ×4 → ×7）", UnitCatalog.Borg, HotaL2, UnitCatalog.Hiyo),
        new("L3", "L2 ＋ 爆炎・独り（ヒヨがいなければ自分の手番の火勢4 で爆炎）", BorgL3, HotaL2, UnitCatalog.Hiyo),
    };
    internal static FB.Ver VerOf(string name) => Versions.First(v => v.Name == name);
    internal static Formation Apply(Formation f, FB.Ver v) => FC.Apply(f, v);

    internal static string[] WaveNames => FC.WaveNames;
    internal static bool IsTarget(int w) => FC.IsTarget(w);
    internal const int TM = FC.TargetTurns;

    static void LogOne(string ver, string seats, int seed, int wave, int sc)
    {
        var f = Apply(FB.Dec(seats), VerOf(ver));
        var (r, _, _) = FC.Fight(f, wave, IsTarget(wave) ? EnemyScaleRule.None : BA.Scales[sc].Sc, seed);
        Console.WriteLine($"# {ver} × {BA.SeatsNamed(f)} × {WaveNames[wave]} × {BA.Scales[sc].Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    internal static string Pct(long a, long n) => FC.Pct(a, n);
    internal static string Per(long a, long n) => FC.Per(a, n);

    // ---------------------------------------------------------------------------------
    // 段1（第246期の器具をそのまま・L0 と L3 だけ）
    // ---------------------------------------------------------------------------------
    static string Dir => _args is { Length: > 3 } ? _args[3] : Path.GetTempPath();
    static string PickPath(string ver) => Path.Combine(Dir, $"fireatk250_pick_T3_{ver}.tsv");
    internal static readonly string[] PickVersions = { "L0", "L3" };

    internal static List<FB.Pick> PicksOf(FB.Ver v, out double sec)
    {
        string path = PickPath(v.Name);
        if (File.Exists(path)) return FC.LoadPick(path, out sec);
        var ps = FC.RunPick(v, out sec);
        FC.SavePick(ps, path, sec);
        return ps;
    }

    static void PickImpl()
    {
        var only = _args is { Length: > 4 } ? _args[4] : null;
        foreach (string vn in PickVersions)
        {
            if (only is not null && vn != only) continue;
            var v = VerOf(vn);
            string path = PickPath(vn);
            if (File.Exists(path)) { Console.WriteLine($"{vn}: {path} は既にある（消せば回し直す）"); continue; }
            var ps = PicksOf(v, out double sec);
            Console.WriteLine($"{vn}: 段1 を {path} に書いた（{ps.Count} 組・{sec / 60:F1} 分）");
        }
    }
}
