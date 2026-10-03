using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// =====================================================================================
// burnhit —— 第255期「燃焼を『叩くと燃え上がる』に（被弾の燃焼）」。
// 指示書は design/PHASE255_BURN_ON_HIT_SPEC.md ／ 報告は design/PHASE255_BURN_ON_HIT.md。
// 版（§2.2）は札1枚の足し算だけで作り、`Run` の引数は増やさない。札は**盤面の規則のスイッチ**で、
// 持ち主が戦に出ていればその戦の燃えている駒すべてに効く（敵だけの版は持ち主の相手の陣営だけ）。
//   H0       ＝ 規定（第254期・対照）
//   H-足す   ＝ ＋ `BurnHitAdd`（ターン頭 6 × 火勢 のまま ＋ 被弾 6 × 火勢）
//   H-分担   ＝ ＋ `BurnHitSplit`（ターン頭 6 × 1 ＋ 被弾 6 × 火勢）
//   H-分担1  ＝ ＋ `BurnHitSplitOnce`（H-分担 ＋ 1回の攻撃で同じ駒は1回まで）
//   H-敵だけ ＝ ＋ `BurnHitFoeOnly`（持ち主の相手だけ H-分担・味方はターン頭のまま）
// 札はボルグがいればボルグに、いなければ編成の最初の駒に足す（どちらでも同じ規則になる）。
//
//     dotnet run --project BattleSim -c Release 0 burnhit phase0           # Q0-5（H0 ＋ 計数の札で機会を数える）・手数の表
//     dotnet run --project BattleSim -c Release 0 burnhit pick [dir] [何]   # 段1: T3（H0 ／ H-分担）・混ぜ（H0 ／ H-分担）
//     dotnet run --project BattleSim -c Release 0 burnhit run [dir]        # 段2 ＋ 表A〜H
//     dotnet run --project BattleSim -c Release 0 burnhit check            # 自己検査（受け入れ 1〜4）
//     dotnet run --project BattleSim -c Release 0 burnhit log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]
// =====================================================================================
static partial class BurnHitDiag
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
            case "log":
                LogOne(args.Length > 3 ? args[3] : "H-分担", args.Length > 4 ? args[4] : "golm,hisa,borg,hota,hiyo", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("burnhit: モードは phase0 / pick / run / check / log。");
                return;
        }
    }
    static string[]? _args;
    static partial void Phase0();
    static partial void PickImpl();
    static partial void RunImpl();
    static partial void CheckImpl();

    // ---------------------------------------------------------------------------------
    // 版（§2.2）
    // ---------------------------------------------------------------------------------
    internal sealed record HVer(string Name, string What, TraitId? Card);
    internal static readonly HVer[] Versions =
    {
        new("H0", "規定（第254期・対照）", null),
        new("H-足す", "ターン頭 6 × 火勢 のまま ＋ 被弾の燃焼 6 × 火勢", TraitId.BurnHitAdd),
        new("H-分担", "ターン頭 6 × 1 ＋ 被弾の燃焼 6 × 火勢", TraitId.BurnHitSplit),
        new("H-分担1", "H-分担 ＋ 1回の攻撃で同じ駒は1回まで", TraitId.BurnHitSplitOnce),
        new("H-敵だけ", "敵だけ H-分担・味方はターン頭のまま（被弾の燃焼なし）", TraitId.BurnHitFoeOnly),
    };
    internal static HVer VerOf(string name) => Versions.First(v => v.Name == name);
    /// <summary>Phase 0 の計数の版（H0 ＋ 計数の札・盤面は H0 と同じ）。</summary>
    internal static readonly HVer Probe = new("H0+数", "H0 ＋ 機会を数える札（盤面は動かない）", TraitId.BurnHitCount);

    /// <summary>札を足す（ボルグがいればボルグ、いなければ編成の最初の駒）。<paramref name="card"/> が null ならそのまま。</summary>
    internal static Formation Mark(Formation f, TraitId? card)
    {
        // 第258期: 規定のボルグに敵上げ満が入ったので、この期（第255期）の台のボルグは旧（`BorgW0`）に固定する。
        f = Pin(f);
        if (card is null) return f;
        var g = f.Clone();
        int slot = f.Occupied().Where(o => o.Def.Id == "borg").Select(o => o.Slot).DefaultIfEmpty(f.Occupied().First().Slot).First();
        var d = g[slot]!;
        g[slot] = FC.With(d, d.Traits.Concat(new[] { card.Value }));
        return g;
    }
    /// <summary>第258期: 規定のボルグ（参照）を旧（`BorgW0`）に差し替える。ほかの駒・席は触らない。</summary>
    internal static Formation Pin(Formation f)
    {
        if (!f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Borg))) return f;
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (ReferenceEquals(o.Def, UnitCatalog.Borg)) g[o.Slot] = UnitCatalog.BorgW0;
        return g;
    }
    /// <summary>段1 の器具（`FC.RunPick`）に渡す形（ボルグに札）。</summary>
    internal static FB.Ver PickVer(HVer v) => new(v.Name, v.What,
        v.Card is null ? UnitCatalog.BorgW0 : FC.With(UnitCatalog.BorgW0, UnitCatalog.BorgW0.Traits.Concat(new[] { v.Card.Value })),
        UnitCatalog.Hota, UnitCatalog.Hiyo);

    // ---------------------------------------------------------------------------------
    // 台（§4.1）
    // ---------------------------------------------------------------------------------
    internal const string PoisonRow = "毒 (グザ×ミオ×ラウ)";
    internal static Formation PoisonBoard => CompareBuilds().First(r => r.Name.StartsWith(PoisonRow)).F;
    internal static readonly string[] FixedBoards = { "T3-244", "T3-238", "雷＋ボルグ" };
    internal static readonly string[] RefBoards = { "毒", "参考 移動", "参考 雷" };
    internal static Formation BaseOf(string name, Dictionary<string, List<FB.Pick>>? ranked) => name switch
    {
        "T3-244" => FC.T3244,
        "T3-238" => FC.T3238,
        "雷＋ボルグ" => FC.ThunderBorg,
        "毒" => PoisonBoard,
        "参考 移動" => BA.RefMove,
        "参考 雷" => BA.RefThunder,
        _ when ranked is not null && name.StartsWith("T3(") => FB.Dec(ranked[PickKey("T3", name[3..^2])][0].Best),
        _ when ranked is not null && name.StartsWith("混ぜ(") => FB.Dec(ranked[PickKey("混ぜ", name[3..^2])][0].Best),
        _ => throw new ArgumentException(name),
    };
    internal static string PickKey(string kind, string ver) => $"{kind}:{ver}";

    // 波（`FC.WaveNames`）: 0〜3 本編 第2〜5波 ／ 4 九/新兵 ／ 5 九/農兵 ／ 6 重い波 ／ 7 的・一 ／ 8 的・九
    internal static string[] WaveNames => FC.WaveNames;
    internal static bool IsTarget(int w) => FC.IsTarget(w);
    internal static EnemyScaleRule ScOf(int w, int s) => IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc;

    static void LogOne(string ver, string seats, int seed, int wave, int sc)
    {
        var f = Mark(FB.Dec(seats), VerOf(ver).Card);
        var (r, _, _) = FC.Fight(f, wave, ScOf(wave, sc), seed);
        Console.WriteLine($"# {ver} × {BA.SeatsNamed(f)} × {WaveNames[wave]} × {(IsTarget(wave) ? "—" : BA.Scales[sc].Name)} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    internal static string Pct(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1");
    internal static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    internal static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    internal static string NameOf(string key)
    {
        var parts = key.Split(':');
        string side = parts[0] == "0" ? "味" : "敵";
        var d = UnitCatalog.Everyone.FirstOrDefault(u => u.Id == parts[1]) ?? AllEnemyDefs().FirstOrDefault(u => u.Id == parts[1]);
        return $"{side}:{d?.Name ?? parts[1]}";
    }
    static IEnumerable<UnitDef> AllEnemyDefs()
        => EnemyCatalog.Stages.SelectMany(st => st.Enemy.Occupied().Select(o => o.Def))
            .Concat(new[] { EnemyCatalog.Warden, FC.Target });
}
