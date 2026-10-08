using System.Reflection;
using BattleCore;
using static Common;

// =====================================================================================
// zan299 —— 第299期「ミサ MF-b ／ ザン ZN-b の規定化 ＋ ヒサ自身も癒す ＋ ザンの手番『仇巡り』」。
// 指示書は design/PHASE299_ZAN_ROUND_SPEC.md ／ 報告は design/PHASE299_ZAN_ROUND.md。
//
//     dotnet run --project BattleSim -c Release 0 zan299 p0              # Phase 0: ザンの手番の回数・手番の時点で標を持つ敵の数と層・仇巡りの見込み（規定・段0 の後）
//     dotnet run --project BattleSim -c Release 0 zan299 hisa [seeds]    # 段0-2: ヒサ（旧 `HisaHKb` ／ 規定）× 代表台 × 波・ヒサが倒れた T・ヒサへの回復
//     dotnet run --project BattleSim -c Release 0 zan299 rates [seeds]   # §6: 代表台 × 波 × 版（規定 ／ ZM-a ／ ZM-1）
//     dotnet run --project BattleSim -c Release 0 zan299 compare         # §6-2: `compare` 64 行 × 版・(G2)
//     dotnet run --project BattleSim -c Release 0 zan299 grid <guard|bat|boss> [seeds]   # §6-2: 固定枠 ヒサ ＋ ザン ＋ ミサ・探索枠2 × 席 120・規定と ZM-a で同じ台・対照（R383）
//     dotnet run --project BattleSim -c Release 0 zan299 memo <台の一部> <boss|guard|bat|1..5> <seed> <規定|ZM-a|ZM-1>   # 台本の並び（羽 ／ 濡れ衣 ／ 仇巡り）
//     dotnet run --project BattleSim -c Release 0 zan299 check           # 自己検査
// =====================================================================================
static class Zan299Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "rates";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": P0(int.Parse(A(3, "200"))); return;
            case "hisa": HisaRates(int.Parse(A(3, "200"))); return;
            case "rates": Rates(int.Parse(A(3, "200"))); return;
            case "compare": CompareRows(); return;
            case "grid": Grid(A(3, "boss"), int.Parse(A(4, "10"))); return;
            case "memo": Memo(A(3, "循環"), A(4, "guard"), int.Parse(A(5, "0")), A(6, "規定")); return;
            case "check": Check(); return;
            default: Console.WriteLine("zan299: モードは p0 / hisa / rates / compare / grid / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 共通
    // ---------------------------------------------------------------------------------
    static Doha297Diag.Wave[] Waves() => Doha297Diag.Waves();
    static Doha297Diag.Wave WaveOf(string k) => Waves().First(w => w.Key == k);
    static Doha297Diag.Wave[] MainWaves() => new[] { "2", "3", "4", "5", "guard", "bat", "boss" }.Select(WaveOf).ToArray();
    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation CompareRow(string n) => CompareBuilds().First(r => r.Name == n).F;
    static bool Has(Formation f, UnitDef d) => f.Occupied().Any(o => ReferenceEquals(o.Def, d));

    /// <summary>代表台（§6-2）。段0 の後の規定で組む。最後の2台はミサのいない ザンの行（ZM-a ＝ ZM-1 の確認）。</summary>
    internal static (string Name, Formation F)[] Boards() => new[]
    {
        ("試遊・標 循環", Playtest("試遊・標 循環")),
        ("試遊・標 三人組", Playtest("試遊・標 三人組")),
        ("試遊・標 守り型", Playtest("試遊・標 守り型")),
        ("標経済 (ヒサ×ザン×ミサ)", CompareRow("標経済 (ヒサ×ザン×ミサ)")),
        ("仇討ち×砕け (ヒビ×ザン)", CompareRow("仇討ち×砕け (ヒビ×ザン)")),
        ("仇討ち (ヒサ×ザン)", CompareRow("仇討ち (ヒサ×ザン)")),
        ("駆り立て (カリ×ドルガ)", CompareRow("駆り立て (カリ×ドルガ)")),
    };
    /// <summary>段0-2 の代表台（§3 の参考の数字）。</summary>
    static (string Name, Formation F)[] HisaBoards() => Boards().Take(4).ToArray();

    internal sealed record Ver(string Tag, UnitDef Zan);
    internal static Ver[] Versions() => new[] { new Ver("規定", UnitCatalog.Zan), new Ver("ZM-a", UnitCatalog.ZanZMa), new Ver("ZM-1", UnitCatalog.ZanZM1) };
    static Ver VerOf(string tag) => Versions().First(v => v.Tag == tag);
    internal static Formation Apply(Formation f, Ver v) => FvSwap(f, UnitCatalog.Zan, v.Zan);

    // ---------------------------------------------------------------------------------
    // 1戦の集計
    // ---------------------------------------------------------------------------------
    sealed class Agg
    {
        public long N, Wins, WinT, Turns, FirstDeathN, FirstDeathT,
                    ZanTurns, ZanNoMarked, MarkedFoes, Layers, PlanA, Plan1, PlanACap, Plan1Cap, TurnDealt,
                    RoundTurns, RoundFoes, Slashes, Capped, Crossed, Back, RoundDealt, Vend, VendDealt, Frames, FrameDealt, Recoil,
                    Rally, RallyHealed, HisaPresent, HisaDeathN, HisaDeathT, HisaHealed, HisaSelfN, HisaSelfAmt, MfFoe, MfAlly;
        public long[] Hist = new long[VendettaTrait.RoundCap + 1];
        public void Add(Agg o)
        {
            foreach (var f in typeof(Agg).GetFields()) if (f.FieldType == typeof(long)) f.SetValue(this, (long)f.GetValue(this)! + (long)f.GetValue(o)!);
            for (int i = 0; i < Hist.Length; i++) Hist[i] += o.Hist[i];
        }
        public double P(long x) => N == 0 ? 0 : (double)x / N;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public static double R(long a, long b) => b == 0 ? 0 : (double)a / b;
    }

    static Agg One(Formation f, Func<List<UnitState>> enemy, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, enemy(), seed, verbose: false);
        var a = new Agg { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var dead = p.Where(u => u.LastDeathTurn > 0).Select(u => u.LastDeathTurn).ToList();
        if (dead.Count > 0) { a.FirstDeathN = 1; a.FirstDeathT = dead.Min(); }
        var hisa = p.FirstOrDefault(u => u.Def.Id == "hisa");
        if (hisa is not null) { a.HisaPresent = 1; if (hisa.LastDeathTurn > 0) { a.HisaDeathN = 1; a.HisaDeathT = hisa.LastDeathTurn; } }
        var mine = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!mine.Contains(id)) continue;
            a.Rally += t.RallyFires; a.RallyHealed += t.RallyHealed; a.MfFoe += t.MfShotsFoe; a.MfAlly += t.MfShotsAlly;
            if (id == "hisa") { a.HisaHealed += t.Healed; a.HisaSelfN += t.RallySelfHeals; a.HisaSelfAmt += t.RallySelfHealed; }
            if (id != "zan") continue;
            a.ZanTurns += t.ZanTurns; a.ZanNoMarked += t.ZanTurnNoMarked; a.MarkedFoes += t.ZanTurnMarkedFoes; a.Layers += t.ZanTurnLayers;
            a.PlanA += t.ZanPlanA; a.Plan1 += t.ZanPlan1; a.PlanACap += t.ZanPlanACapped; a.Plan1Cap += t.ZanPlan1Capped; a.TurnDealt += t.ZanTurnDealt;
            a.RoundTurns += t.RoundTurns; a.RoundFoes += t.RoundFoes; a.Slashes += t.RoundSlashes; a.Capped += t.RoundCapped; a.Crossed += t.RoundCrossed;
            a.Back += t.RoundBack; a.RoundDealt += t.RoundDealt; a.Vend += t.VendettaFires; a.VendDealt += t.VendettaDealt; a.Frames += t.FrameVendettas; a.FrameDealt += t.FrameDealt;
            a.Recoil += t.RecoilTaken;
            if (t.RoundSlashHist is { } h) for (int i = 0; i < h.Length && i < a.Hist.Length; i++) a.Hist[i] += h[i];
        }
        return a;
    }

    static Agg Many(Formation f, Func<List<UnitState>> enemy, int seeds, int seed0 = 0)
    {
        var parts = new Agg[seeds];
        Parallel.For(0, seeds, s => parts[s] = One(f, enemy, seed0 + s));
        var a = new Agg();
        foreach (var x in parts) a.Add(x);
        return a;
    }

    static string FirstDeath(Agg a) => a.FirstDeathN == 0 ? "—" : $"{Agg.R(a.FirstDeathT, a.FirstDeathN):F1}（{100.0 * a.FirstDeathN / a.N:F0}%）";
    static string HisaDeath(Agg a) => a.HisaDeathN == 0 ? "—" : $"{Agg.R(a.HisaDeathT, a.HisaDeathN):F1}（{100.0 * a.HisaDeathN / a.N:F0}%）";

    // ---------------------------------------------------------------------------------
    // Phase 0
    // ---------------------------------------------------------------------------------
    static void P0(int seeds)
    {
        Console.WriteLine($"# 第299期 Phase 0 —— ザンの手番（規定・段0 の後・seed 0..{seeds - 1}・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("「標の敵」＝ 手番の時点で標を持つ敵（生きている）。見込みは仇巡りの太刀（ZM-a ＝ 層の合計・ZM-1 ＝ 敵の数・どちらも 8 で切る）を、**規定の手番の時点の盤面で**数えたもの（実際に仇巡りをすると盤面が変わる）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 決着T | ザンの手番 | 標の敵 0 の手番 | 標の敵（手番あたり） | 層の合計（手番あたり） | 見込み ZM-a ／ ZM-1（標の敵がいる手番あたり） | 8 に達する手番 ZM-a ／ ZM-1 | 手番の与ダメ（手番あたり） | 仇討ち（回・量） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|---|---|--:|---|");
        foreach (var (n, f) in Boards())
            foreach (var w in MainWaves())
            {
                var a = Many(f, w.Make, seeds);
                long withMarked = a.ZanTurns - a.ZanNoMarked;
                Console.WriteLine($"| {n} | {w.Name} | {a.Win:F1} | {a.P(a.Turns):F1} | {a.P(a.ZanTurns):F2} | {100.0 * Agg.R(a.ZanNoMarked, a.ZanTurns):F0}% | {Agg.R(a.MarkedFoes, a.ZanTurns):F2} | {Agg.R(a.Layers, a.ZanTurns):F2} | {Agg.R(a.PlanA, withMarked):F2} ／ {Agg.R(a.Plan1, withMarked):F2} | {100.0 * Agg.R(a.PlanACap, withMarked):F0}% ／ {100.0 * Agg.R(a.Plan1Cap, withMarked):F0}% | {Agg.R(a.TurnDealt, a.ZanTurns):F1} | {a.P(a.Vend):F1} ・ {a.P(a.VendDealt):F0} |");
            }
    }

    // ---------------------------------------------------------------------------------
    // 段0-2: ヒサ（旧 HK-b ＝ `HisaHKb` ／ 規定 ＝ 自分も癒す）
    // ---------------------------------------------------------------------------------
    static void HisaRates(int seeds)
    {
        Console.WriteLine($"# 第299期 段0-2 —— ヒサの叫びの回復先にヒサ自身を含める（旧 `HisaHKb` → 規定・seed 0..{seeds - 1}・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("ミサ ／ ザンは段0-1 の後の規定（MF-b ／ ZN-b）。「ヒサが倒れた T」は倒れた戦の平均（括弧は倒れた戦の割合）。ヒサへの回復はヒサの受けた回復(受) の合計（叫び以外も含む）・自分への叫びは回数 ／ 量。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 旧 → 規定 | 決着T 旧 → 規定 | 味方が初めて倒れたT 旧 → 規定 | ヒサが倒れたT 旧 → 規定 | ヒサへの回復 旧 → 規定 | 自分への叫び（回・量） | 叫び 回 ／ 総回復量 旧 → 規定 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var (n, f) in HisaBoards())
            foreach (var w in MainWaves())
            {
                var a = Many(FvSwap(f, UnitCatalog.Hisa, UnitCatalog.HisaHKb), w.Make, seeds);
                var b = Many(f, w.Make, seeds);
                string Bold(double x, double y, string s) => Math.Abs(y - x) >= 10 ? $"**{s}**" : s;
                Console.WriteLine($"| {n} | {w.Name} | {Bold(a.Win, b.Win, $"{a.Win:F1} → {b.Win:F1}")} | {a.P(a.Turns):F1} → {b.P(b.Turns):F1} | {FirstDeath(a)} → {FirstDeath(b)} | {HisaDeath(a)} → {HisaDeath(b)} | {a.P(a.HisaHealed):F0} → {b.P(b.HisaHealed):F0} | {b.P(b.HisaSelfN):F2} ・ {b.P(b.HisaSelfAmt):F0} | {a.P(a.Rally):F1} ／ {a.P(a.RallyHealed):F0} → {b.P(b.Rally):F1} ／ {b.P(b.RallyHealed):F0} |");
            }
    }

    // ---------------------------------------------------------------------------------
    // §6: 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void Rates(int seeds)
    {
        var vers = Versions();
        Console.WriteLine($"# 第299期 代表台 × 波 × 版（seed 0..{seeds - 1}・勝率 %）");
        Console.WriteLine();
        Console.WriteLine("太字 ＝ 規定から ±10pt 以上。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定 | ZM-a | ZM-1 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        var res = new Dictionary<(string, string, string), Agg>();
        foreach (var (n, f) in Boards())
            foreach (var w in Waves())
            {
                var cells = vers.Select(v => { var a = Many(Apply(f, v), w.Make, seeds); res[(n, w.Key, v.Tag)] = a; return a; }).ToArray();
                Console.WriteLine($"| {n} | {w.Name} | {cells[0].Win:F1} | " + string.Join(" | ", cells.Skip(1).Select(c => Math.Abs(c.Win - cells[0].Win) >= 10 ? $"**{c.Win:F1}**" : $"{c.Win:F1}")) + " |");
            }
        Console.WriteLine();
        foreach (var v in vers) Console.WriteLine($"- 代表台 {Boards().Length} 台 × 第2〜5波 ＋ 精鋭 ＋ ボスの平均 {v.Tag}: {Boards().SelectMany(b => MainWaves().Select(w => res[(b.Name, w.Key, v.Tag)].Win)).Average():F2}");
        Console.WriteLine();
        Console.WriteLine("## 中身（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("「手番の与ダメ」は手番の間に敵陣の HP が減った量（手番の中の連鎖を含む）。仇巡りの手番 ／ 普通の手番に分ける。仇討ちは手番の外（反撃の枠）の量。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率 | 決着T | 味方が初めて倒れたT | ザンの手番 | 仇巡りの手番 | 1手番の太刀（平均・8 に達した割合） | 巡った敵（手番あたり） | 列越え ／ 後列の太刀 | 与ダメ 普通の手番 ／ 仇巡りの手番（1手番あたり） | 与ダメ 手番 ／ 仇討ち（1戦） | 返り血 | 叫び ／ 回復量 |");
        Console.WriteLine("|---|---|---|--:|--:|---|--:|--:|---|--:|---|---|---|--:|---|");
        foreach (var (n, _) in Boards())
            foreach (var w in MainWaves())
                foreach (var v in vers)
                {
                    var a = res[(n, w.Key, v.Tag)];
                    long normal = a.ZanTurns - a.RoundTurns;
                    Console.WriteLine($"| {n} | {w.Name} | {v.Tag} | {a.Win:F1} | {a.P(a.Turns):F1} | {FirstDeath(a)} | {a.P(a.ZanTurns):F2} | {a.P(a.RoundTurns):F2} | {Agg.R(a.Slashes, a.RoundTurns):F2}（{100.0 * Agg.R(a.Capped, a.RoundTurns):F0}%） | {Agg.R(a.RoundFoes, a.RoundTurns):F2} | {a.P(a.Crossed):F2} ／ {a.P(a.Back):F2} | {Agg.R(a.TurnDealt, normal):F1} ／ {Agg.R(a.RoundDealt, a.RoundTurns):F1} | {a.P(a.TurnDealt + a.RoundDealt):F0} ／ {a.P(a.VendDealt):F0} | {a.P(a.Recoil):F1} | {a.P(a.Rally):F1} ／ {a.P(a.RallyHealed):F0} |");
                }
        Console.WriteLine();
        Console.WriteLine("## 1手番の太刀の分布（仇巡りの手番・代表台 × 第2〜5波 ＋ 精鋭 ＋ ボス）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | " + string.Join(" | ", Enumerable.Range(1, VendettaTrait.RoundCap).Select(i => $"{i}")) + " |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("--:|", VendettaTrait.RoundCap)));
        foreach (var (n, _) in Boards())
            foreach (var w in new[] { "guard", "bat", "boss" }.Select(WaveOf))
                foreach (var v in vers.Skip(1))
                {
                    var a = res[(n, w.Key, v.Tag)];
                    if (a.RoundTurns == 0) continue;
                    Console.WriteLine($"| {n} | {w.Name} | {v.Tag} | " + string.Join(" | ", Enumerable.Range(1, VendettaTrait.RoundCap).Select(i => $"{100.0 * a.Hist[i] / a.RoundTurns:F0}%")) + " |");
                }
    }

    // ---------------------------------------------------------------------------------
    // `compare` 64 行 × 版
    // ---------------------------------------------------------------------------------
    static double[] CompareRates(Formation f)
    {
        var w = new double[EnemyCatalog.Stages.Count];
        for (int i = 0; i < w.Length; i++)
        {
            int ii = i;
            var res = new bool[200];
            Parallel.For(0, 200, s => res[s] = BattleEngine.Run(f, EnemyCatalog.Stages[ii].Enemy, s, verbose: false).PlayerWon);
            w[i] = 100.0 * res.Count(x => x) / 200;
        }
        return w;
    }
    static string Row(double[] r) => string.Join(" / ", r.Select(x => x.ToString("0.0")));

    static void CompareRows()
    {
        Console.WriteLine("# 第299期 `compare` 64 行 × 版（seed 0..199・第1〜5波）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var baseR = rows.ToDictionary(r => r.Name, r => CompareRates(r.F));
        Console.WriteLine($"ザンのいる行: {string.Join(" ／ ", rows.Where(r => Has(r.F, UnitCatalog.Zan)).Select(r => r.Name))}");
        Console.WriteLine();
        foreach (var v in Versions().Skip(1))
        {
            Console.WriteLine($"## {v.Tag}");
            Console.WriteLine();
            Console.WriteLine("| 行 | 規定 | 版 | 最大の差（第2〜5波） |");
            Console.WriteLine("|---|---|---|--:|");
            var newR = new Dictionary<string, double[]>();
            foreach (var (n, f) in rows)
            {
                var g = Apply(f, v);
                newR[n] = Has(f, UnitCatalog.Zan) ? CompareRates(g) : baseR[n];
                if (!Has(f, UnitCatalog.Zan)) continue;
                double d = Enumerable.Range(1, 4).Select(i => newR[n][i] - baseR[n][i]).OrderByDescending(Math.Abs).First();
                Console.WriteLine($"| {n} | {Row(baseR[n])} | {Row(newR[n])} | {d:+0.0;-0.0} |");
            }
            Console.WriteLine();
            Console.WriteLine("全64行の平均: 規定 " + string.Join(" / ", Enumerable.Range(0, 5).Select(i => baseR.Values.Average(r => r[i]).ToString("F1"))) + " → 版 " + string.Join(" / ", Enumerable.Range(0, 5).Select(i => newR.Values.Average(r => r[i]).ToString("F1"))));
            var primary = Baseline.PrimaryRows;
            Console.WriteLine($"主判定19行の第五波: 規定 {primary.Average(n => baseR[n][4]):F1} → 版 {primary.Average(n => newR[n][4]):F1}");
            var dropped = rows.Where(r => Enumerable.Range(1, 4).Any(i => newR[r.Name][i] - baseR[r.Name][i] <= -10.0)).ToList();
            Console.WriteLine($"いずれかの波（第2〜5波）で −10.0pt 以上落ちた行: {(dropped.Count == 0 ? "0 行" : string.Join(" ／ ", dropped.Select(r => r.Name)))}");
            foreach (var (n, f) in dropped)
            {
                Console.WriteLine();
                Console.WriteLine($"(G2) {n}:");
                foreach (var (_, d) in f.Occupied())
                {
                    var others = rows.Where(r => r.Name != n && Has(r.F, d)).ToList();
                    if (others.Count == 0) { Console.WriteLine($"- {Short(d)}: 他の行 0（分解が成立しない）"); continue; }
                    double ch = others.Average(r => newR[r.Name].Skip(1).Average() - baseR[r.Name].Skip(1).Average());
                    Console.WriteLine($"- {Short(d)}: 他の行 {others.Count}・第2〜5波平均の変化 {ch:+0.00;-0.00} → {(ch <= -3.0 ? "**壊れ**" : Math.Abs(ch) < 3.0 ? "制約" : "—")}");
                }
            }
            Console.WriteLine();
        }
    }

    // ---------------------------------------------------------------------------------
    // 格子: 固定枠 ヒサ ＋ ザン ＋ ミサ・探索枠2 × 席 120（R386）＋ 対照（R383・固定枠の駒それぞれ → ドルガ）
    // ---------------------------------------------------------------------------------
    static IEnumerable<int[]> Perms(int n)
    {
        var a = Enumerable.Range(0, n).ToArray();
        IEnumerable<int[]> Rec(int k)
        {
            if (k == n) { yield return (int[])a.Clone(); yield break; }
            for (int i = k; i < n; i++)
            {
                (a[k], a[i]) = (a[i], a[k]);
                foreach (var p in Rec(k + 1)) yield return p;
                (a[k], a[i]) = (a[i], a[k]);
            }
        }
        return Rec(0);
    }

    static void Grid(string waveKey, int seeds)
    {
        var w = WaveOf(waveKey);
        Ver v0 = Versions()[0], va = VerOf("ZM-a");
        var fixedDefs = new[] { UnitCatalog.Hisa, UnitCatalog.Zan, UnitCatalog.Tome };
        var pool = UnitCatalog.All.Where(d => !fixedDefs.Contains(d)).ToArray();
        var perms = Perms(5).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# 第299期 格子 —— {w.Name} × 規定 ／ ZM-a（固定枠 ヒサ ＋ ザン ＋ ミサ・探索枠2 × 席 120・seed 0..{seeds - 1}）");
        Console.WriteLine();
        var rows = new System.Collections.Concurrent.ConcurrentBag<(string A, string B, double Base, double Ver, int[] Seat, UnitDef[] Five)>();
        var pairs = new List<(UnitDef, UnitDef)>();
        for (int i = 0; i < pool.Length; i++) for (int j = i + 1; j < pool.Length; j++) pairs.Add((pool[i], pool[j]));
        int WinsAt(Formation f, int bw)
        {
            int k = 0;
            for (int sd = 0; sd < seeds; sd++) { if (BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false).PlayerWon) k++; else if (k + (seeds - sd - 1) <= bw) break; }
            return k;
        }
        Formation At(int[] perm, UnitDef[] five) { var g = new Formation(); for (int s = 0; s < 5; s++) g[perm[s]] = five[s]; return g; }
        Parallel.ForEach(pairs, pr =>
        {
            var five = new[] { UnitCatalog.Hisa, UnitCatalog.Zan, UnitCatalog.Tome, pr.Item1, pr.Item2 };
            double Best(Ver c, out int[] seat)
            {
                int bw = -1; seat = perms[0];
                foreach (var perm in perms)
                {
                    int k = WinsAt(Apply(At(perm, five), c), bw);
                    if (k > bw) { bw = k; seat = perm; }
                    if (bw == seeds) break;
                }
                return 100.0 * bw / seeds;
            }
            double b = Best(v0, out _), x = Best(va, out var sv);
            if (Math.Max(b, x) < 50) return;
            rows.Add((Short(pr.Item1), Short(pr.Item2), b, x, sv, five));
        });
        var all = rows.ToList();
        Console.WriteLine($"組 {pairs.Count}・所要 {sw.Elapsed.TotalSeconds:F0} 秒。50% 以上の台: 規定 {all.Count(r => r.Base >= 50)} ／ ZM-a {all.Count(r => r.Ver >= 50)}。ZM-a − 規定 の平均 {(all.Count == 0 ? 0 : all.Average(r => r.Ver - r.Base)):+0.0;-0.0}・+10pt 以上 {all.Count(r => r.Ver - r.Base >= 10)} ／ −10pt 以下 {all.Count(r => r.Ver - r.Base <= -10)}。");
        Console.WriteLine();
        string Kinds(Func<(string A, string B, double Base, double Ver, int[] Seat, UnitDef[] Five), bool> q) =>
            string.Join("・", all.Where(q).SelectMany(r => new[] { r.A, r.B }).GroupBy(x => x).OrderByDescending(g => g.Count()).Take(15).Select(g => $"{g.Key} {g.Count()}"));
        Console.WriteLine($"自由枠（規定で 50% 以上）: {Kinds(r => r.Base >= 50)}");
        Console.WriteLine();
        Console.WriteLine($"自由枠（ZM-a で 50% 以上）: {Kinds(r => r.Ver >= 50)}");
        Console.WriteLine();
        Console.WriteLine($"自由枠（ZM-a が +10pt 以上）: {Kinds(r => r.Ver - r.Base >= 10)}");
        Console.WriteLine();
        Console.WriteLine("上位 12 台（ZM-a − 規定 の大きい順）と対照（R383・ZM-a の最良席で、固定枠の駒それぞれ → ドルガ）:");
        Console.WriteLine();
        Console.WriteLine("| 規定 | ZM-a | ヒサ → ドルガ | ザン → ドルガ | ミサ → ドルガ | 自由枠 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|---|");
        foreach (var r in all.OrderByDescending(r => r.Ver - r.Base).ThenByDescending(r => r.Ver).Take(12))
        {
            string Ctl(int idx)
            {
                var five = r.Five.Select((d, i) => i == idx ? UnitCatalog.Dolga : d).ToArray();
                return (100.0 * WinsAt(Apply(At(r.Seat, five), va), -1) / seeds).ToString("F0");
            }
            Console.WriteLine($"| {r.Base:F0} | {r.Ver:F0} | {Ctl(0)} | {Ctl(1)} | {Ctl(2)} | {r.A}・{r.B} |");
        }
    }

    // ---------------------------------------------------------------------------------
    // 台本
    // ---------------------------------------------------------------------------------
    static void Memo(string boardPart, string wave, int seed, string verTag)
    {
        var (name, f0) = Boards().First(b => b.Name.Contains(boardPart, StringComparison.Ordinal));
        var v = VerOf(verTag);
        var f = Apply(f0, v);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# zan299 memo —— {name} × {v.Tag} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        foreach (var x in r.Events)
        {
            if (x.Kind is not (BattleEventKind.Damage or BattleEventKind.FeatherMark or BattleEventKind.Framed or BattleEventKind.Feather or BattleEventKind.MarkRally
                or BattleEventKind.Death or BattleEventKind.Attack or BattleEventKind.VendettaRound)) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.PartnerId is int pa ? $"  Partner={N(pa)}" : "")}{(x.Reaction ? "  Reaction" : "")}{(x.FriendlyFire ? "  ff" : "")}{(x.Relayed ? "  relayed" : "")}");
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    static readonly PropertyInfo TurnProp = typeof(BattleContext).GetProperty("Turn") ?? throw new InvalidOperationException("Turn が見つからない");
    static readonly PropertyInfo TallyProp = typeof(BattleContext).GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TallyByUnit");
    static readonly MethodInfo TakeTurnM = typeof(BattleContext).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).First(m => m.Name == "TakeTurn" && m.GetParameters().Length == 1);
    static readonly MethodInfo DrainM = typeof(BattleContext).GetMethod("DrainFeatherMarksPublic", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("DrainFeatherMarksPublic");
    static UnitTally Tal(BattleContext ctx, string id) => ((Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!).GetValueOrDefault(id) ?? new UnitTally();

    static BattleContext Ctx(Formation pl, Formation en, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(en, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnProp.SetValue(ctx, 1);
        return ctx;
    }

    static int Count(string path, string needle)
    {
        string s = File.ReadAllText(path);
        int c = 0, i = 0;
        while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { c++; i += needle.Length; }
        return c;
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# zan299 check —— 第299期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // (a) 段0-1: 規定のミサ ＝ 旧（M-b `TomeMb`）＋ MF-b の札 ／ 規定のザン ＝ 旧（`ZanZN0`）＋ ZN-b の札・文面・旧は `All` の外
        var misa = UnitCatalog.Tome; var zan = UnitCatalog.Zan;
        Expect("(a) 段0-1: 規定のミサ ＝ `TomeMb` ＋ `FeatherMarkLayer`（＝ 第298期の MF-b）・規定のザン ＝ `ZanZN0` ＋ `VendettaFrameAll`（＝ ZN-b）・文面は第298期の版のまま・旧は `All` ／ `Everyone` の外・第298期の版は旧から作る",
            misa.Traits.SequenceEqual(UnitCatalog.TomeMb.Traits.Append(TraitId.FeatherMarkLayer)) && misa.Traits.SequenceEqual(UnitCatalog.MisaMFb.Traits)
            && misa.PlusText == UnitCatalog.MisaMFb.PlusText && misa.MinusText == UnitCatalog.MisaMFb.MinusText && misa.Flavor == UnitCatalog.TomeMb.Flavor
            && misa.PlusText.EndsWith("。指差されたものは、全部撃つ", StringComparison.Ordinal) && misa.MinusText.EndsWith("。味方が指差されても、撃つ", StringComparison.Ordinal)
            && zan.Traits.SequenceEqual(UnitCatalog.ZanZN0.Traits.Append(TraitId.VendettaFrameAll)) && zan.Traits.SequenceEqual(UnitCatalog.ZanZNb.Traits)
            && zan.PlusText == UnitCatalog.ZanZNb.PlusText && zan.PlusText.EndsWith("。仲間を撃った者が誰であれ、指差された敵を斬る", StringComparison.Ordinal) && zan.MinusText == UnitCatalog.ZanZN0.MinusText
            && misa.MaxHp == 58 && misa.Attack == 12 && misa.Speed == 6 && zan.MaxHp == 56 && zan.Attack == 10 && zan.Speed == 5
            && UnitCatalog.All.Contains(misa) && UnitCatalog.All.Contains(zan) && !UnitCatalog.Everyone.Contains(UnitCatalog.TomeMb) && !UnitCatalog.Everyone.Contains(UnitCatalog.ZanZN0)
            && UnitCatalog.MisaMFa.Traits.SequenceEqual(UnitCatalog.TomeMb.Traits.Append(TraitId.FeatherMark)) && UnitCatalog.ZanZNa.Traits.SequenceEqual(UnitCatalog.ZanZN0.Traits.Append(TraitId.VendettaFrame)));
        // (b) 段0-2: 規定のヒサ ＝ 旧（`HisaHKb`）＋ `MarkRallySelf`・文面は変えない
        var hisa = UnitCatalog.Hisa;
        Expect("(b) 段0-2: 規定のヒサ ＝ `HisaHKb` ＋ `MarkRallySelf`・文面 ／ 数値 ／ 手番は旧のまま・旧は `All` ／ `Everyone` の外",
            hisa.Traits.SequenceEqual(UnitCatalog.HisaHKb.Traits.Append(TraitId.MarkRallySelf)) && hisa.PlusText == UnitCatalog.HisaHKb.PlusText && hisa.MinusText == UnitCatalog.HisaHKb.MinusText
            && hisa.Flavor == UnitCatalog.HisaHKb.Flavor && hisa.MaxHp == UnitCatalog.HisaHKb.MaxHp && hisa.Speed == UnitCatalog.HisaHKb.Speed && hisa.Actions!.SequenceEqual(UnitCatalog.HisaHKb.Actions!)
            && !UnitCatalog.Everyone.Contains(UnitCatalog.HisaHKb));

        // 盤面を直に組む: 前1 ドルガ ／ 前3 ザン ／ 中央 ミサ ／ 後1 カタ ／ 後3 ヒサ
        Formation Sq(UnitDef m, UnitDef z, UnitDef h) => Formation.Build(front1: UnitCatalog.Dolga, front3: z, center: m, back1: UnitCatalog.Kata, back3: h);
        {
            // (c) 段0-2: 仲間が満タンでヒサだけ傷ついているとき、叫びの「最も傷ついた味方」はヒサ（旧ならヒサは癒されない）
            (long self, int hp) Shout(UnitDef h)
            {
                var ctx = Ctx(Sq(UnitCatalog.TomeMb, UnitCatalog.ZanZN0, h), Formation.Build(front1: UnitCatalog.Gald), out var p, out var e);
                var hi = p.First(u => u.Def.Id == "hisa");
                hi.Hp = hi.MaxHp / 2;
                e[0].SetCounter(StatusKeys.Marked, 1);
                var zz = p.First(u => u.Def.Id == "zan");
                TakeTurnM.Invoke(ctx, new object[] { zz });   // ザンの手番（標の敵へ・攻撃のひとまとまり1つ）
                return (Tal(ctx, "hisa").RallySelfHeals, hi.Hp);
            }
            var (s0, h0) = Shout(UnitCatalog.HisaHKb);
            var (s1, h1) = Shout(UnitCatalog.Hisa);
            Expect("(c) 段0-2: 仲間が満タンでヒサだけ傷ついているとき、叫びの「最も傷ついた味方」はヒサ（旧 `HisaHKb` は癒さない）", s0 == 0 && s1 == 1 && h1 > h0, $"旧: 自分への叫び {s0}・HP {h0} ／ 規定: {s1}・HP {h1}");
        }

        // 仇巡り: 敵の盤面 前1 ガルド ／ 前3 ドルガ ／ 後1 ドルガ（後列）。ザンの手番を直に1回回す。
        (UnitTally zt, List<UnitState> e, BattleContext ctx, int[] layers) Round(UnitDef zv, int[] marks, UnitDef? h = null, bool full = false)
        {
            var en = Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, back1: UnitCatalog.Dolga);
            var ctx = Ctx(Sq(UnitCatalog.TomeMb, zv, h ?? UnitCatalog.HisaHKb), en, out var p, out var e);
            for (int i = 0; i < e.Count && i < marks.Length; i++) { e[i].MaxHp = e[i].Hp = 10000; if (marks[i] > 0) e[i].SetCounter(StatusKeys.Marked, marks[i]); }
            if (!full) foreach (var u in p) u.Hp = u.MaxHp;
            var zz = p.First(u => u.Def.Id == "zan");
            TakeTurnM.Invoke(ctx, new object[] { zz });
            return (Tal(ctx, "zan"), e, ctx, e.Select(u => u.RawCounter(StatusKeys.Marked)).ToArray());
        }
        {
            var (zt, _, _, _) = Round(UnitCatalog.ZanZMa, new[] { 0, 0, 0 });
            Expect("(d) 仇巡り: 標を持つ敵がいなければ普通の攻撃（仇巡りの手番 0・太刀 0）", zt.RoundTurns == 0 && zt.RoundSlashes == 0 && zt.ZanTurns == 1 && zt.ZanTurnNoMarked == 1);
        }
        {
            var (zt, e, _, lay) = Round(UnitCatalog.ZanZMa, new[] { 2, 0, 3 });
            Expect("(e) ZM-a の太刀は層の数（層 3 の後列 ＋ 層 2 の前列 ＝ 5 太刀・2体）・前列の制限を受けない（後列へ 3 太刀）・標を消費しない・新しい標を書かない",
                zt.RoundTurns == 1 && zt.RoundSlashes == 5 && zt.RoundFoes == 2 && zt.RoundBack == 3 && zt.RoundCrossed == 3 && lay.SequenceEqual(new[] { 2, 0, 3 }),
                $"太刀 {zt.RoundSlashes}・巡った {zt.RoundFoes}・後列 {zt.RoundBack}・列越え {zt.RoundCrossed}・層 {string.Join("/", lay)}");
        }
        {
            var (zt, _, _, _) = Round(UnitCatalog.ZanZMa, new[] { 5, 4, 3 });
            Expect("(f) 合計 8 で止まる（層 5 ＋ 4 ＋ 3 ＝ 12 → 8 太刀・巡ったのは2体）", zt.RoundSlashes == 8 && zt.RoundCapped == 1 && zt.RoundFoes == 2 && (zt.RoundSlashHist?[8] ?? 0) == 1, $"太刀 {zt.RoundSlashes}・巡った {zt.RoundFoes}");
        }
        {
            var (zt, _, _, _) = Round(UnitCatalog.ZanZMa, new[] { 1, 1, 0 });
            var (z1, _, _, _) = Round(UnitCatalog.ZanZM1, new[] { 3, 2, 0 });
            Expect("(g) 2周目はしない（層 1 ＋ 1 ＝ 2 太刀で止まる）／ ZM-1 は1体に1太刀（層 3 ＋ 2 でも 2 太刀）", zt.RoundSlashes == 2 && zt.RoundCapped == 0 && z1.RoundSlashes == 2 && z1.RoundFoes == 2, $"ZM-a {zt.RoundSlashes} ／ ZM-1 {z1.RoundSlashes}");
        }
        {
            // (h) 返り血が付かない・羽を呼ばない（ミサは MF-b の規定）・叫びは1手番に1回
            var en = Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, back1: UnitCatalog.Dolga);
            var ctx = Ctx(Sq(UnitCatalog.Tome, UnitCatalog.ZanZMa, UnitCatalog.Hisa), en, out var p, out var e);
            foreach (var u in e) { u.MaxHp = u.Hp = 10000; }
            e[0].SetCounter(StatusKeys.Marked, 2); e[2].SetCounter(StatusKeys.Marked, 3);
            var zz = p.First(u => u.Def.Id == "zan");
            foreach (var u in p) u.Hp = Math.Max(1, u.MaxHp - 30);
            int z0 = zz.Hp;
            DrainM.Invoke(ctx, null);   // 盤面を組んだときの標（SetCounter）で控えた羽を先に撃ってしまう
            var tm0 = Tal(ctx, "tome"); long q0 = tm0.MfQueuedFresh + tm0.MfQueuedLayer, r0 = Tal(ctx, "hisa").RallyFires;
            TakeTurnM.Invoke(ctx, new object[] { zz });
            var zt = Tal(ctx, "zan"); var tm = Tal(ctx, "tome"); var th = Tal(ctx, "hisa");
            long dq = tm.MfQueuedFresh + tm.MfQueuedLayer - q0, dr = th.RallyFires - r0;
            Expect("(h) 仇巡りは返り血が付かない ／ 羽を呼ばない（規定のミサ MF-b がいても手番の間に控えた羽 0）／ 叫びは1手番に1回（5 太刀でも）",
                zt.RoundSlashes == 5 && zt.RecoilTaken == 0 && dq == 0 && dr == 1 && zz.Hp >= z0,
                $"太刀 {zt.RoundSlashes}・返り血 {zt.RecoilTaken}・控えた羽 {dq}・叫び {dr}");
        }
        {
            // (i) 規定のザン（札なし）は標の敵がいても普通の攻撃1回
            var (zt, _, _, _) = Round(UnitCatalog.Zan, new[] { 2, 0, 3 });
            Expect("(i) 規定のザンは標の敵がいても普通の攻撃（仇巡りしない・計数だけ）", zt.RoundTurns == 0 && zt.ZanTurns == 1 && zt.ZanPlanA == 5 && zt.ZanPlan1 == 2, $"見込み ZM-a {zt.ZanPlanA} ／ ZM-1 {zt.ZanPlan1}");
        }
        {
            // (j) 的が倒れたら次の敵へ（層 3 の1体目が1太刀で倒れる → 2体目へ）
            var en = Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Dolga, back1: UnitCatalog.Dolga);
            var ctx = Ctx(Sq(UnitCatalog.TomeMb, UnitCatalog.ZanZMa, UnitCatalog.HisaHKb), en, out var p, out var e);
            foreach (var u in e) { u.MaxHp = u.Hp = 10000; }
            e[2].Hp = 1; e[2].SetCounter(StatusKeys.Marked, 3); e[0].SetCounter(StatusKeys.Marked, 2);
            var zz = p.First(u => u.Def.Id == "zan");
            TakeTurnM.Invoke(ctx, new object[] { zz });
            var zt = Tal(ctx, "zan");
            Expect("(j) 的が倒れたら次の敵へ（層 3 の後列が1太刀で倒れる → 層 2 の前列へ 2 太刀・残りの 2 太刀は持ち越さない）", !e[2].IsAlive && zt.RoundSlashes == 3 && zt.RoundFoes == 2, $"太刀 {zt.RoundSlashes}・巡った {zt.RoundFoes}");
        }
        int pick = Directory.GetFiles("BattleCore", "*.cs").Sum(f => Count(f, "PickOne("));
        Expect("(k) `PickOne(` の出現数が第298期と同じ（BattleCore）", pick == 36, $"{pick}");
        // (l) verbose の有無で勝敗・決着T が同じ（版 × 代表台 × 試遊の波 × seed 0..9）
        int diff = 0, n2 = 0;
        foreach (var v in Versions()) foreach (var (_, f) in Boards()) foreach (var w in new[] { "guard", "bat", "boss" }.Select(WaveOf)) for (int s = 0; s < 10; s++)
                    {
                        var g = Apply(f, v);
                        var a = BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                        var b = BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                        n2++; if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                    }
        Expect("(l) verbose の有無で勝敗・決着T が同じ（規定 ／ ZM-a ／ ZM-1 × 代表台 7 × 試遊の波 × seed 0..9）", diff == 0, $"{n2} 戦・違い {diff}");
        // (m) 出来事: 規定は `VendettaRound` 0・ZM-a に始まり ／ 太刀・太刀の数は始まりの合計と一致
        long St(Ver v, string t) { long x = 0; for (int s = 0; s < 10; s++) x += BattleEngine.Run(BattleEngine.Materialize(Apply(Playtest("試遊・標 循環"), v), BattleContext.PlayerTeam), WaveOf("boss").Make(), s, verbose: true).Events.Count(e => e.Kind == BattleEventKind.VendettaRound && e.Text == t); return x; }
        long e0 = St(Versions()[0], VendettaRoundLabels.Start), e1 = St(VerOf("ZM-a"), VendettaRoundLabels.Start), e2 = St(VerOf("ZM-a"), VendettaRoundLabels.Slash);
        // 段0-1 の出来事（規定で羽 ／ 濡れ衣が出る）
        long fm = 0, fr = 0; for (int s = 0; s < 10; s++) { var r = BattleEngine.Run(BattleEngine.Materialize(Playtest("試遊・標 循環"), BattleContext.PlayerTeam), WaveOf("guard").Make(), s, verbose: true); fm += r.Events.Count(x => x.Kind == BattleEventKind.FeatherMark); fr += r.Events.Count(x => x.Kind == BattleEventKind.Framed); }
        Expect("(m) 出来事: 規定は `VendettaRound` 0・ZM-a に始まり ／ 太刀（循環 × ボス × seed 0..9）・規定の循環 × 近衛で `FeatherMark` ／ `Framed` が出る（段0-1）", e0 == 0 && e1 > 0 && e2 >= e1 && fm > 0 && fr > 0, $"{e0} ／ {e1} ／ {e2}・羽 {fm} ／ 濡れ衣 {fr}");
        // (n) 版の札と文面・`All` ／ `Everyone` ／ `Presets` の外
        var vers = new[] { UnitCatalog.ZanZMa, UnitCatalog.ZanZM1 };
        Expect("(n) 版の札と文面（ZM-a: プラス末尾「手番では、仇を巡って斬る。深く指差された仇ほど、何度も斬る」）・`All` ／ `Everyone` ／ `Presets` の外・上限 8",
            UnitCatalog.ZanZMa.Traits.SequenceEqual(zan.Traits.Append(TraitId.VendettaRound)) && UnitCatalog.ZanZM1.Traits.SequenceEqual(zan.Traits.Append(TraitId.VendettaRoundOne))
            && UnitCatalog.ZanZMa.PlusText == zan.PlusText + "。手番では、仇を巡って斬る。深く指差された仇ほど、何度も斬る" && UnitCatalog.ZanZMa.MinusText == zan.MinusText
            && VendettaTrait.RoundCap == 8
            && vers.All(v => !UnitCatalog.Everyone.Contains(v)) && Presets.Compare.Concat(Presets.Cross).Concat(Presets.Playtest).All(r => r.F.Occupied().All(o => !vers.Contains(o.Def))));
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "すべて ○" : $"**× {_fail} 件**");
    }
}
