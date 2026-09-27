using BattleCore;
using static Common;

// =====================================================================================
// retreat —— 第225期「ガルド抜きの移動軸：シオの段と緊急退避・5枠目はハネ／カド」。
// 指示書は design/PHASE225_RETREAT_SPEC.md ／ 報告は design/PHASE225_RETREAT.md。**線は置かない。**
//
//     dotnet run --project BattleSim -c Release 0 retreat seats    # 前段: `compare` のセロの行の席の見直し（帯A で線 → 帯B で追試）
//     dotnet run --project BattleSim -c Release 0 retreat phase0   # Q0-1〜Q0-6
//     dotnet run --project BattleSim -c Release 0 retreat run      # 表A〜F
//     dotnet run --project BattleSim -c Release 0 retreat check    # 自己検査（受け入れ 2〜4・7）
// =====================================================================================
static partial class RetreatDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "";
        string arg = args.Length > 3 ? string.Join(" ", args.Skip(3)) : "";
        switch (mode)
        {
            case "seats": Seats(arg); return;
            case "phase0": Phase0(); return;
            case "run": RunImpl(arg); return;
            case "check": CheckImpl(); return;
            default:
                Console.WriteLine("retreat: モードは seats / phase0 / run / check。");
                return;
        }
    }

    static partial void Phase0();
    static partial void RunImpl(string arg);
    static partial void CheckImpl();

    internal const int Seeds = 200;

    // =================================================================================
    // 版（§3）。**札の差し替え**だけ。名前で引くのは Phase 0 のコミット（札が無い）でも回るように。
    //   J0 規定 ／ J1 土台（`DrifterMend` ＋ 手当て `RegroupTend` ＋ 自分にも `RegroupTendSelf`）／ J2 ＋ 段 `ShioStage` ／
    //   J3 ＋ 緊急退避 `Retreat`（1ターン1回で固定）／ J4 ＋ 段 ＋ 緊急退避
    // =================================================================================

    static TraitId? T(string name) => Enum.TryParse<TraitId>(name, out var t) ? t : null;
    internal static UnitDef Copy(UnitDef d, TraitId[] traits) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances,
        Pattern = d.Pattern, Actions = d.Actions, Traits = traits,
        PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };
    static UnitDef? ShioWith(params string[] names)
    {
        var extra = names.Select(T).ToList();
        if (extra.Any(x => x is null)) return null;
        return Copy(UnitCatalog.ShioV3, UnitCatalog.ShioV3.Traits.Concat(extra.Select(x => x!.Value)).ToArray());
    }
    static readonly string[] Base = { "DrifterMend", "RegroupTend", "RegroupTendSelf" };
    internal static readonly UnitDef J0 = UnitCatalog.ShioV3;
    internal static readonly UnitDef? J1 = ShioWith(Base);
    internal static readonly UnitDef? J2 = ShioWith(Base.Append("ShioStage").ToArray());
    internal static readonly UnitDef? J3 = ShioWith(Base.Append("Retreat").ToArray());
    internal static readonly UnitDef? J4 = ShioWith(Base.Append("ShioStage").Append("Retreat").ToArray());
    internal static (string Tag, UnitDef Def)[] Versions => new (string, UnitDef?)[]
        { ("J0", J0), ("J1", J1), ("J2", J2), ("J3", J3), ("J4", J4) }
        .Where(v => v.Item2 is not null).Select(v => (v.Item1, v.Item2!)).ToArray();

    /// <summary>編成のシオだけを版の駒に差し替える（陣形・席はそのまま）。</summary>
    internal static Formation WithShio(Formation f, UnitDef shio)
    {
        var g = f.Clone();
        foreach (var (slot, d) in f.Occupied()) if (d.Id == "shio") g[slot] = shio;
        return g;
    }

    // =================================================================================
    // 台（§6.1）。M-ハネ・M-カドの席は J4 × 九/新兵 × 200/115 の総当たりで選ぶ（`run`）。ここは仮の並び
    // ＝第222期 D1（ポンの台）のガルドの席に5枠目を入れた形。参考はガルド入りの D1 そのまま。
    // =================================================================================

    internal static Formation Raw(UnitDef fifth) => Formation.Build(front1: UnitCatalog.Yomi, front3: fifth,
        center: UnitCatalog.ShioV3, back1: UnitCatalog.Sero, back3: UnitCatalog.BasaK0);
    internal static Formation RawHane => Raw(UnitCatalog.HaneK0);
    internal static Formation RawKado => Raw(UnitCatalog.Kado);
    internal static Formation RefGald => Raw(UnitCatalog.Gald);

    // 波: 本編の第2〜5波 ＋ 検証・九 / 新兵 ＋ 検証・九 / 農兵（`DriftDiag.WaveOf` の 0..5）
    internal static readonly string[] WaveNames = DriftDiag.WaveNames;
    internal static Func<List<UnitState>> WaveOf(int w, EnemyScaleRule sc) => DriftDiag.WaveOf(w, sc);
    internal static readonly (string Name, EnemyScaleRule Sc)[] Scales =
    {
        ("200/115", new EnemyScaleRule(200, 115)),
        ("150/115", new EnemyScaleRule(150, 115)),
        ("115/115", new EnemyScaleRule(115, 115)),
    };
    internal static string SeatsNamed(Formation f) => DriftDiag.SeatsNamed(f);
    internal static string F1(double x) => DriftDiag.F1(x);
    internal static string F2(double x) => DriftDiag.F2(x);
    internal static string D1(double x) => DriftDiag.D1(x);

    /// <summary>後ろ側の隣（シオの組み替えと同じ規則: 隣接かつより後ろの行・召喚枠を除く・席番号の順）。盤面の席だけで引く（X 字）。</summary>
    internal static int? BackPartner(int slot, IReadOnlyDictionary<int, int> livingSlots)
    {
        int depth = FormationRules.DepthOf(FormationRules.RowOf(slot));
        return livingSlots.Values.Where(s => s != slot && !FormationRules.IsSummonSlot(s)
                && FormationRules.AreAdjacent(slot, s) && FormationRules.DepthOf(FormationRules.RowOf(s)) > depth)
            .OrderBy(s => s).Select(s => (int?)s).FirstOrDefault();
    }

    // =================================================================================
    // 前段: `compare` のセロの行の席（第148期の作法）。
    //   線 ＝ 狙 ○（ガルド前列・セッキ後列）／ 情報セル（第2〜5波の 0 < x < 100・帯A）が現行以上 ／ 帯A（seed 0..199）の第2〜5波平均が現行 +5.0pt 以上。
    //   線を通った並びを「情報セル → 帯A の平均」の順に上から5つまで、帯B（seed 200..599）で追試し、+5.0pt を超えた最上位を候補にする。
    //   行ごとの狙い（`Presets.cs` のコメント）との食い違いは人が見る（第148期: `狙` 列はガルド／セッキしか見ていない）。
    // =================================================================================

    static bool MeetsIntent(Formation f)
    {
        foreach (var (slot, def) in f.Occupied())
        {
            if (def.Id == "gald" && FormationRules.RowOf(slot) != Row.Front) return false;
            if (def.Id == "sekki" && FormationRules.RowOf(slot) != Row.Back) return false;
        }
        return true;
    }

    static double[] Cells(Formation f, int seed0, int seeds)
    {
        var st = EnemyCatalog.Stages;
        var r = new double[st.Count];
        for (int w = 0; w < st.Count; w++)
        {
            int wins = 0;
            for (int s = seed0; s < seed0 + seeds; s++)
                if (BattleEngine.Run(f, st[w].Enemy, s, verbose: false).PlayerWon) wins++;
            r[w] = wins * 100.0 / seeds;
        }
        return r;
    }

    static double Avg25(double[] c) => (c[1] + c[2] + c[3] + c[4]) / 4.0;
    static int Info(double[] c) => Enumerable.Range(1, 4).Count(w => c[w] > 0 && c[w] < 100);
    static string Seats(Formation f) => string.Join(" ／ ", f.Occupied().Select(o => $"{FormationRules.SeatNames[o.Slot]} {o.Def.Name}"));

    static void Seats(string filter)
    {
        var rows = CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "sero"))
            .Where(r => filter.Length == 0 || r.Name.Contains(filter)).ToList();
        Console.WriteLine("# 前段: `compare` のセロの行の席（第225期・規定のセロ）");
        Console.WriteLine();
        Console.WriteLine("線 ＝ 狙 ○ ／ 情報セル（帯A・第2〜5波）が現行以上 ／ 帯A 第2〜5波平均 +5.0pt 以上。帯B（seed 200..599）で +5.0pt を超えた最上位を候補にする。");
        Console.WriteLine();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var perms = rows.Select(r =>
        {
            var members = r.F.Occupied().Select(x => x.Def).ToList();
            var ps = new List<Formation>();
            foreach (int[] a in SlotAssignments(members.Count))
            {
                var f = new Formation();
                for (int m = 0; m < members.Count; m++) f[a[m]] = members[m];
                ps.Add(f);
            }
            return ps;
        }).ToList();
        var cellsA = perms.Select(ps => new double[ps.Count][]).ToArray();
        var jobs = new List<(int R, int P)>();
        for (int r = 0; r < rows.Count; r++) for (int p = 0; p < perms[r].Count; p++) jobs.Add((r, p));
        Parallel.For(0, jobs.Count, j => { var (r, p) = jobs[j]; cellsA[r][p] = Cells(perms[r][p], 0, 200); });

        Console.WriteLine("| 行 | セロの席 | 現行 帯A | 情報 | 線を通った | 追試した並び（帯A ／ 情報 ／ 帯B 対 現行 帯B） | 候補 |");
        Console.WriteLine("|---|---|--:|--:|--:|---|---|");
        var detail = new List<string>();
        for (int r = 0; r < rows.Count; r++)
        {
            var (name, cur) = rows[r];
            int ci = perms[r].FindIndex(f => SameFormation(f, cur));
            var cA = cellsA[r][ci];
            double curA = Avg25(cA); int curI = Info(cA);
            var line = Enumerable.Range(0, perms[r].Count)
                .Where(i => i != ci && MeetsIntent(perms[r][i]) && Info(cellsA[r][i]) >= curI && Avg25(cellsA[r][i]) >= curA + 5.0)
                .OrderByDescending(i => Info(cellsA[r][i])).ThenByDescending(i => Avg25(cellsA[r][i])).ThenBy(i => i).ToList();
            double curB = Avg25(Cells(cur, 200, 400));
            var tried = new List<string>(); var triedSeats = new List<string>(); int pick = -1; double pickB = 0;
            foreach (int i in line.Take(5))
            {
                double b = Avg25(Cells(perms[r][i], 200, 400));
                tried.Add($"{Avg25(cellsA[r][i]):F1} ／ {Info(cellsA[r][i])} ／ {b:F1}");
                triedSeats.Add($"  - 追試: {Seats(perms[r][i])} ／ 帯A {Avg25(cellsA[r][i]):F1} ／ 情報 {Info(cellsA[r][i])} ／ 帯B {b:F1}");
                if (pick < 0 && b >= curB + 5.0) { pick = i; pickB = b; }
            }
            string seroSeat = FormationRules.SeatNames[cur.Occupied().First(o => o.Def.Id == "sero").Slot];
            Console.WriteLine($"| {name} | {seroSeat} | {curA:F1} | {curI} | {line.Count} | {(tried.Count == 0 ? "—" : string.Join("<br>", tried))}（現行 帯B {curB:F1}） | {(pick < 0 ? "据え置き" : "差し替え")} |");
            detail.Add($"- **{name}** 現行: {Seats(cur)} ／ 帯A " + string.Join(" ", cA.Select(x => $"{x:F1}")));
            detail.AddRange(triedSeats);
            if (pick >= 0)
                detail.Add($"  - 候補: {Seats(perms[r][pick])} ／ 帯A " + string.Join(" ", cellsA[r][pick].Select(x => $"{x:F1}")) + $" ／ 帯B 平均 {pickB:F1}");
            // 参考: 狙 ○ の中で帯A 平均が最大の並び（線の外でも）
            int best = Enumerable.Range(0, perms[r].Count).Where(i => MeetsIntent(perms[r][i]))
                .OrderByDescending(i => Avg25(cellsA[r][i])).ThenByDescending(i => Info(cellsA[r][i])).ThenBy(i => i).First();
            detail.Add($"  - 参考（狙 ○ で帯A 最大）: {Seats(perms[r][best])} ／ 帯A " + string.Join(" ", cellsA[r][best].Select(x => $"{x:F1}")) + $" ／ 情報 {Info(cellsA[r][best])}");
        }
        Console.WriteLine();
        foreach (var d in detail) Console.WriteLine(d);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒");
    }
}
