using BattleCore;
using static Common;

// =====================================================================================
// sero —— 第223期「逃げ上手のセロ」（逃亡兵セロの作り直し）。指示書は design/PHASE223_SERO_EVADE_SPEC.md。
//
//     dotnet run --project BattleSim -c Release 0 sero phase0   # Q0-1〜Q0-6（E0 だけで回る）
//     dotnet run --project BattleSim -c Release 0 sero run      # 表A〜F（台 S1〜S4 × 波 × 版 × 倍率）
//     dotnet run --project BattleSim -c Release 0 sero check    # 自己検査（受け入れ 2〜4・7）
//
// 版（§4）は**札の差し替え**で作る（`Run` の引数は増やさない・旧札は消さない）:
//   E0 今のセロ（`Sniper` / `Coward`）／ E1 回避・追い撃ち・段・入れ替え（`Evade` / `EvadeSwap`）／ E2 ＋ 状態の矢（`StatusArrow`）
// 規定のセロ（`UnitCatalog.SeroOld`）は E0 のまま——版は診断のローカルの駒。
// =====================================================================================
static partial class SeroDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "";
        string arg = args.Length > 3 ? string.Join(" ", args.Skip(3)) : "";
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": RunImpl(arg); return;
            case "check": CheckImpl(); return;
            default:
                Console.WriteLine("sero: モードは phase0 / run / check。");
                return;
        }
    }

    static partial void RunImpl(string arg);
    static partial void CheckImpl();

    internal const int Seeds = 200;

    // =================================================================================
    // 台（指示書 §7.1）
    // =================================================================================

    /// <summary>S1: ポンの移動改（第222期 D1 の席）。シオ・ヨミは前段で規定になった V3。</summary>
    internal static Formation BenchS1Pon => Formation.Build(front1: UnitCatalog.Yomi, front3: UnitCatalog.Gald,
        center: UnitCatalog.Shio, back1: UnitCatalog.SeroOld, back3: UnitCatalog.Basa);

    /// <summary>S2 の5枚目（Phase 0 Q0-6 の規則で選んだ・`sero phase0` が出す）。</summary>
    internal static UnitDef S2Fifth => UnitCatalog.Nel;

    /// <summary>S2: 庇いの無い移動軸（S1 のガルドの席に5枚目。席は総当たりする。これは仮の並び）。</summary>
    internal static Formation BenchS2Raw => Formation.Build(front1: UnitCatalog.Yomi, front3: S2Fifth,
        center: UnitCatalog.Shio, back1: UnitCatalog.SeroOld, back3: UnitCatalog.Basa);

    /// <summary>S3: 状態の矢の台（席は E1 × 九/新兵 × 150/115 で総当たり。これは仮の並び）。</summary>
    internal static Formation BenchS3Raw => Formation.Build(front1: UnitCatalog.Kubi, front3: UnitCatalog.Beni,
        center: UnitCatalog.Kata, back1: UnitCatalog.SeroOld, back3: UnitCatalog.Mio);

    /// <summary>S4: セロのいる `compare` の既存の行（席はそのまま）。</summary>
    internal static List<(string Name, Formation F)> CompareRowsWithSero() =>
        CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "sero"))
            .Select(r => (r.Name, WithSero(r.F, UnitCatalog.SeroOld))).ToList();   // 第225期: 規定化の前のセロ（E0）に固定

    /// <summary>
    /// 「庇う・肩代わりする」駒（S2 の候補から外す）。`docs/harm.md` の8枚（ガルド・ゴルム・ドハ・セッキ・ウケ・ワタ・ササ・棘守りのカド）
    /// ＋ 矢面のヒサ ＋ 範囲の盾のバン。移動軸の4枚（バサ・ヨミ・シオ・セロ）も外す。
    /// </summary>
    internal static readonly HashSet<string> NotForS2 = new()
    {
        "gald", "golm", "doha", "sekki", "uke", "wata", "sasa", "kado", "hisa", "ban",
        "basa", "yomi", "shio", "sero",
    };

    // 波: 本編の第2〜5波 ＋ 検証・九 / 新兵（`DriftDiag.WaveOf` の 0..4）
    internal static readonly string[] WaveNames = { "第二波", "第三波", "第四波", "第五波", "九/新兵" };
    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => DriftDiag.WaveOf(w, sc);
    internal static (string Name, EnemyScaleRule Sc)[] Scales => DriftDiag.Scales;

    internal static string SeatsNamed(Formation f) => DriftDiag.SeatsNamed(f);
    internal static string F1(double x) => DriftDiag.F1(x);
    internal static string F2(double x) => DriftDiag.F2(x);
    internal static string D1(double x) => DriftDiag.D1(x);

    /// <summary>編成のセロだけを版の駒に差し替える（陣形・席はそのまま）。</summary>
    internal static Formation WithSero(Formation f, UnitDef sero)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied())
            if (d.Id == "sero") g[slot] = sero;
        return g;
    }

    internal static IEnumerable<UnitDef[]> Permute(IReadOnlyList<UnitDef> xs)
    {
        if (xs.Count == 1) { yield return new[] { xs[0] }; yield break; }
        for (int i = 0; i < xs.Count; i++)
        {
            var rest = xs.Where((_, j) => j != i).ToList();
            foreach (var p in Permute(rest)) yield return new[] { xs[i] }.Concat(p).ToArray();
        }
    }
}
