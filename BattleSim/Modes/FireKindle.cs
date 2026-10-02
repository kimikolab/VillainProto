using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// =====================================================================================
// firekindle —— 第252期「ボルグが育つ口・ボルグとヒヨのあぶれた火」。
// 指示書は design/PHASE252_BORG_KINDLE_SPEC.md ／ 報告は design/PHASE252_BORG_KINDLE.md。
// 版（§3）は札の差し替えだけで作り、`Run` の引数は増やさない。波・倍率・台は第250期（`fireatk`）と同じ。
//   M0 ＝ 規定（第251期・対照）
//   M-B1 ＝ M0 ＋ 守るほど燃え上がる（`KindleGuard`）   M-B2 ＝ M0 ＋ 開幕の火勢（`KindleOpen`）   M-B3 ＝ M0 ＋ 放熱で育つ（`RadiateGrow`）
//   M1 ＝ M0 ＋ B1 ＋ 溜め火（`BlazeHoard`）＋ 渡す火（`GiftHoard`）   M1+ ＝ M1 ＋ B2 ＋ B3
//   M2 ＝ M0 ＋ B1 ＋ 鎧の火（`ArmorFlame`）＋ 癒しの灯（`MendGlow`）
//
//     dotnet run --project BattleSim -c Release 0 firekindle phase0          # Q0-1〜Q0-5（M0 で台を数える・予測の材料）
//     dotnet run --project BattleSim -c Release 0 firekindle pick [dir] [版]  # T3 の段1（M0 と M1 だけ・1,081 組 × 席 120 × seed 40）
//     dotnet run --project BattleSim -c Release 0 firekindle run [dir]       # 段2 ＋ 表A〜H
//     dotnet run --project BattleSim -c Release 0 firekindle check           # 自己検査（受け入れ 1〜3）
//     dotnet run --project BattleSim -c Release 0 firekindle digest [path]   # M0 の台本の指紋（`FireKindleDigestDiag`・このファイルだけで閉じる）
//     dotnet run --project BattleSim -c Release 0 firekindle log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]
// =====================================================================================
static partial class FireKindleDiag
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
            case "digest": FireKindleDigestDiag.Run(args); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "M1", args.Length > 4 ? args[4] : "golm,hisa,borg,hota,hiyo", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("firekindle: モードは phase0 / pick / run / check / digest / log。");
                return;
        }
    }
    static string[]? _args;
    static partial void Phase0();
    static partial void RunImpl();
    static partial void CheckImpl();

    // ---------------------------------------------------------------------------------
    // 版（§3）
    // ---------------------------------------------------------------------------------
    static UnitDef Plus(UnitDef g, params TraitId[] tr) => FC.With(g, g.Traits.Concat(tr));
    internal static readonly UnitDef BorgB1 = Plus(UnitCatalog.Borg, TraitId.KindleGuard);
    internal static readonly UnitDef BorgB2 = Plus(UnitCatalog.Borg, TraitId.KindleOpen);
    internal static readonly UnitDef BorgB3 = Plus(UnitCatalog.Borg, TraitId.RadiateGrow);
    internal static readonly UnitDef BorgM1 = Plus(UnitCatalog.Borg, TraitId.KindleGuard, TraitId.BlazeHoard);
    internal static readonly UnitDef BorgM1P = Plus(BorgM1, TraitId.KindleOpen, TraitId.RadiateGrow);
    internal static readonly UnitDef BorgM2 = Plus(UnitCatalog.Borg, TraitId.KindleGuard, TraitId.ArmorFlame);
    internal static readonly UnitDef HiyoH1 = Plus(UnitCatalog.Hiyo, TraitId.GiftHoard);
    internal static readonly UnitDef HiyoH2 = Plus(UnitCatalog.Hiyo, TraitId.MendGlow);

    internal static readonly FB.Ver[] Versions =
    {
        new("M0", "規定（第251期・対照）", UnitCatalog.Borg, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("M-B1", "M0 ＋ 守るほど燃え上がる（切った被ダメ 30 ごとに火勢 +1）", BorgB1, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("M-B2", "M0 ＋ 開幕の火勢（くすぶりを火勢2 から）", BorgB2, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("M-B3", "M0 ＋ 放熱で育つ（焼き尽くすで火勢 +1）", BorgB3, UnitCatalog.Hota, UnitCatalog.Hiyo),
        new("M1", "M0 ＋ B1 ＋ 溜め火 ＋ 渡す火", BorgM1, UnitCatalog.Hota, HiyoH1),
        new("M1+", "M1 ＋ B2 ＋ B3", BorgM1P, UnitCatalog.Hota, HiyoH1),
        new("M2", "M0 ＋ B1 ＋ 鎧の火 ＋ 癒しの灯", BorgM2, UnitCatalog.Hota, HiyoH2),
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
    // 段1（第246期の器具をそのまま・M0 と M1 だけ）
    // ---------------------------------------------------------------------------------
    static string Dir => _args is { Length: > 3 } ? _args[3] : Path.GetTempPath();
    static string PickPath(string ver) => Path.Combine(Dir, $"firekindle252_pick_T3_{ver}.tsv");
    internal static readonly string[] PickVersions = { "M0", "M1" };

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
