using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// =====================================================================================
// firefinish —— 第249期「燃焼の軸の仕上げ（ホタの火力・ボルグの爆炎・ホタの火の癒し・残した論点の片付け）」。
// 指示書は design/PHASE249_FIRE_FINISH_SPEC.md ／ 報告は design/PHASE249_FIRE_FINISH.md。
// 版（§4・積み上げ）は札の差し替えとホタの元の攻撃力だけで作り、`Run` の引数は増やさない。
// 第250期 前段: K4 が規定になったので、K0 は第249期までの規定の駒（`BorgK0` / `HotaK0` / `HiyoK0`）に固定した。波・倍率・台は第246〜248期と同じ。
//   K0 ＝ 規定（第248期 U2・対照）       K1 ＝ K0 ＋ ホタの火の癒し（`PyreMend`）
//   K2a ＝ K1 ＋ 贔屓・火勢（`FavorLevel`） K2b ＝ K1 ＋ ホタの元の攻撃力 6 → 10
//   K3 ＝ K2a ＋ 放つ・爆炎（`UnleashBlaze`） K4 ＝ K3 ＋ 残り火・連撃（`EmbersChain`）  K4t ＝ K4 ＋ 刻み・一撃（`TickOnce`）
//
//     dotnet run --project BattleSim -c Release 0 firefinish phase0          # Q0-6（K0 で台を数える・予測の材料）
//     dotnet run --project BattleSim -c Release 0 firefinish pick [dir] [版]  # T3 の段1（K0 と K4 だけ・1,081 組 × 席 120 × seed 40）
//     dotnet run --project BattleSim -c Release 0 firefinish run [dir]       # 段2 ＋ 表A〜H
//     dotnet run --project BattleSim -c Release 0 firefinish check           # 自己検査（受け入れ 2〜4）
//     dotnet run --project BattleSim -c Release 0 firefinish digest [path]   # K0 の台本の指紋（このファイルだけで閉じる・engine を触る前のコードにも置ける）
//     dotnet run --project BattleSim -c Release 0 firefinish log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]
// =====================================================================================
static partial class FireFinishDiag
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
            case "digest": FireFinishDigestDiag.Run(args); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "K4", args.Length > 4 ? args[4] : "golm,hisa,borg,hota,hiyo", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("firefinish: モードは phase0 / pick / run / check / digest / log。");
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
    static UnitDef Atk(UnitDef g, int atk) => new()
    {
        Id = g.Id, Name = g.Name, MaxHp = g.MaxHp, Attack = atk, Speed = g.Speed, Traits = g.Traits.ToArray(), Pattern = g.Pattern,
        Advances = g.Advances, Actions = g.Actions, PlusText = g.PlusText, MinusText = g.MinusText, Flavor = g.Flavor,
    };
    internal const int Hota10 = 10;
    internal static readonly UnitDef HotaK1 = Plus(UnitCatalog.HotaK0, TraitId.PyreMend);
    internal static readonly UnitDef HotaK2b = Atk(HotaK1, Hota10);
    internal static readonly UnitDef HotaK4 = Plus(HotaK1, TraitId.EmbersChain);
    internal static readonly UnitDef HiyoK2a = Plus(UnitCatalog.HiyoK0, TraitId.FavorLevel);
    internal static readonly UnitDef BorgK3 = Plus(UnitCatalog.BorgK0, TraitId.UnleashBlaze);
    internal static readonly UnitDef BorgK4t = Plus(BorgK3, TraitId.TickOnce);

    internal static readonly FB.Ver[] Versions =
    {
        new("K0", "規定（第248期 U2・対照）", UnitCatalog.BorgK0, UnitCatalog.HotaK0, UnitCatalog.HiyoK0),
        new("K1", "K0 ＋ ホタの火の癒し", UnitCatalog.BorgK0, HotaK1, UnitCatalog.HiyoK0),
        new("K2a", "K1 ＋ 贔屓・火勢（+3 × 相手の火勢）", UnitCatalog.BorgK0, HotaK1, HiyoK2a),
        new("K2b", "K1 ＋ ホタの元の攻撃力 6 → 10", UnitCatalog.BorgK0, HotaK2b, UnitCatalog.HiyoK0),
        new("K3", "K2a ＋ 放つ・爆炎", BorgK3, HotaK1, HiyoK2a),
        new("K4", "K3 ＋ 残り火・連撃", BorgK3, HotaK4, HiyoK2a),
        new("K4t", "K4 ＋ 刻み・一撃", BorgK4t, HotaK4, HiyoK2a),
    };
    internal static FB.Ver VerOf(string name) => Versions.First(v => v.Name == name);
    internal static Formation Apply(Formation f, FB.Ver v) => FC.Apply(f, v);

    internal static string[] WaveNames => FC.WaveNames;
    internal static bool IsTarget(int w) => FC.IsTarget(w);
    internal const int TM = FC.TargetTurns;

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
    // 段1（第246期の器具をそのまま・K0 と K4 だけ）
    // ---------------------------------------------------------------------------------
    static string Dir => _args is { Length: > 3 } ? _args[3] : Path.GetTempPath();
    static string PickPath(string ver) => Path.Combine(Dir, $"firefinish249_pick_T3_{ver}.tsv");
    internal static readonly string[] PickVersions = { "K0", "K4" };

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
