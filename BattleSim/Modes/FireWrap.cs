using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;
using FT = FireTriDiag;

// =====================================================================================
// firewrap —— 第248期「燃焼の軸の一区切り（指名の直し・規定化）」。
// 指示書は design/PHASE248_FIRE_WRAP_SPEC.md ／ 報告は design/PHASE248_FIRE_WRAP.md。
// 版は3つだけ（総当たりは回さない・席は固定）:
//   U0 ＝ 第247期の規定（`UnitCatalog.BorgU0` / `HiyoU0`・対照）
//   U1 ＝ 第247期 T1（放熱・指名は火勢に関わらず ＋ 火の粉 両大技・旧育ち）
//   U2 ＝ U1 の指名を「印のボルグが火勢4 のときだけ」に直したもの（ボルグ ＋ `CallFull`）
// 集計は第247期の器具（`FireTriDiag.TAgg` / `FireCycleDiag.CAgg`）をそのまま使う。盤面は1ビットも動かさない。
//
//     dotnet run --project BattleSim -c Release 0 firewrap run     # 表A〜C と線（§3）
//     dotnet run --project BattleSim -c Release 0 firewrap check   # 自己検査（受け入れ 2）
//     dotnet run --project BattleSim -c Release 0 firewrap log <版> <前1,前3,中央,後1,後3> [seed] [波 0〜8] [倍率 0/1/2]
// =====================================================================================
static partial class FireWrapDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "";
        switch (mode)
        {
            case "run": RunImpl(); return;
            case "check": CheckImpl(); return;
            case "log":
                LogOne(args.Length > 3 ? args[3] : "U2", args.Length > 4 ? args[4] : "golm,hisa,borg,hota,hiyo", args.Length > 5 ? int.Parse(args[5]) : 0,
                    args.Length > 6 ? int.Parse(args[6]) : BA.MainWave, args.Length > 7 ? int.Parse(args[7]) : 0);
                return;
            default:
                Console.WriteLine("firewrap: モードは run / check / log。");
                return;
        }
    }
    static partial void CheckImpl();

    static UnitDef Plus(UnitDef g, params TraitId[] tr) => FC.With(g, g.Traits.Concat(tr));
    /// <summary>ボルグ ＋ 放熱（指名・火勢に関わらず）＝ 第247期 T1 のボルグ。</summary>
    internal static readonly UnitDef BorgU1 = Plus(UnitCatalog.BorgU0, TraitId.RadiateCall);
    /// <summary>ボルグ ＋ 放熱 ＋ 指名は火勢4 のときだけ。</summary>
    internal static readonly UnitDef BorgU2 = Plus(UnitCatalog.BorgU0, TraitId.RadiateCall, TraitId.CallFull);
    /// <summary>旧育ち ＋ 火の粉（焼き尽くす・放つ）＝ 第247期 T1 のヒヨ。</summary>
    internal static readonly UnitDef HiyoU1 = Plus(UnitCatalog.HiyoU0, TraitId.SparkCatch, TraitId.SparkUnleash);

    internal static readonly FB.Ver[] Versions =
    {
        new("U0", "第247期の規定（対照）", UnitCatalog.BorgU0, UnitCatalog.Hota, UnitCatalog.HiyoU0),
        new("U1", "第247期 T1（指名は火勢に関わらず ＋ 火の粉 両大技）", BorgU1, UnitCatalog.Hota, HiyoU1),
        new("U2", "U1 の指名を「ボルグが火勢4 のときだけ」に直す", BorgU2, UnitCatalog.Hota, HiyoU1),
    };
    internal static FB.Ver VerOf(string name) => Versions.First(v => v.Name == name);
    internal static Formation Apply(Formation f, FB.Ver v) => FC.Apply(f, v);

    static readonly (string Name, Func<Formation> F)[] Boards = { ("T3-244", () => FC.T3244), ("T3-238", () => FC.T3238), ("雷＋ボルグ", () => FC.ThunderBorg) };
    static readonly (string Name, Func<Formation> F)[] Refs = { ("参考 移動", () => BA.RefMove), ("参考 雷", () => BA.RefThunder) };
    static readonly int[] MainWaves = { 0, 1, 2, 3 };
    static string[] WaveNames => FC.WaveNames;
    static bool IsTarget(int w) => FC.IsTarget(w);

    static void LogOne(string ver, string seats, int seed, int wave, int sc)
    {
        var f = Apply(FB.Dec(seats), VerOf(ver));
        var (r, _, _) = FC.Fight(f, wave, BA.Scales[sc].Sc, seed);
        Console.WriteLine($"# {ver} × {BA.SeatsNamed(f)} × {WaveNames[wave]} × {BA.Scales[sc].Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    // ---------------------------------------------------------------------------------
    // 1つのセル ＝ 台 × 版 × 波 × 倍率（seed 0..199・verbose）
    // ---------------------------------------------------------------------------------
    internal sealed class Cell
    {
        public FC.CAgg C = new(); public FT.TAgg T = new();
        public long CallHeld, CallHeldGift, N;
    }
    internal static Cell Measure(Formation f, int w, EnemyScaleRule sc, int seeds = BA.Seeds)
    {
        var cs = new FC.CAgg[seeds]; var ts = new FT.TAgg[seeds]; var held = new long[seeds]; var heldG = new long[seeds];
        bool target = IsTarget(w);
        Parallel.For(0, seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = FC.WaveOf(w, sc)();
            var r = BattleEngine.Run(p, e, i, verbose: true);
            var c = new FC.CAgg(); c.Take(r, p, e, target); cs[i] = c;
            var t = new FT.TAgg(); t.Take(r, p, target); ts[i] = t;
            if (r.FireLevels is FireLevelLedger led) { held[i] = led.CallHeld; heldG[i] = led.CallHeldGift; }
        });
        var x = new Cell { N = seeds };
        for (int i = 0; i < seeds; i++) { x.C.Merge(cs[i]); x.T.Merge(ts[i]); x.CallHeld += held[i]; x.CallHeldGift += heldG[i]; }
        return x;
    }

    static readonly Dictionary<(string B, string V, int W, int S), Cell> _cells = new();
    static Cell At(string b, string v, int w, int s) => _cells[(b, v, w, IsTarget(w) ? 0 : s)];

    static string Pct(long a, long n) => FC.Pct(a, n);
    static string Per(long a, long n) => FC.Per(a, n);
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static double WinP(FC.CAgg c) => c.N == 0 ? double.NaN : 100.0 * c.Wins / c.N;
    static double SurvP(FC.CAgg c) => c.N == 0 ? double.NaN : 100.0 * c.AllSurv / c.N;
    static double MainAvg(string b, string v, int s, Func<FC.CAgg, double> f) => MainWaves.Average(w => f(At(b, v, w, s).C));
    static readonly string[] Trio = { "borg", "hota", "hiyo" };
    static long TrioDealt(FC.CAgg c) => Enumerable.Range(1, FC.TargetTurns).Sum(t => Trio.Concat(new[] { "tick" }).Sum(k => c.TDealt.TryGetValue(k, out var a) ? a[t] : 0));
    static long AllDealt(FC.CAgg c) => Enumerable.Range(1, FC.TargetTurns).Sum(t => c.TDealt.Values.Sum(a => a[t]));
    static long MoveDealt(FC.CAgg c) => Enumerable.Range(1, FC.TargetTurns).Sum(t => new[] { "yomi", "sero" }.Sum(k => c.TDealt.TryGetValue(k, out var a) ? a[t] : 0));

    /// <summary>§3 の線: U2 が U0 を、T3-244 と T3-238 の本編の第2〜5波 × 400/300 の勝率の平均で 1.0pt 以上下回らなければ U2。</summary>
    internal const double LinePt = 1.0;

    static void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var (bn, bf) in Boards)
            foreach (var v in Versions)
                for (int w = 0; w < WaveNames.Length; w++)
                    for (int s = 0; s < (IsTarget(w) ? 1 : BA.Scales.Length); s++)
                        _cells[(bn, v.Name, w, s)] = Measure(Apply(bf(), v), w, IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc);
        foreach (var (rn, rf) in Refs)
            for (int w = 0; w < WaveNames.Length; w++)
                for (int s = 0; s < (IsTarget(w) ? 1 : BA.Scales.Length); s++)
                    _cells[(rn, "—", w, s)] = Measure(rf(), w, IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc);
        double sec = sw.Elapsed.TotalSeconds;

        Console.WriteLine("# 第248期 firewrap —— 指名の直し（U0 ／ U1 ／ U2）");
        Console.WriteLine();
        Console.WriteLine("台（席は固定・版はボルグとヒヨの札だけを差し替える）:");
        Console.WriteLine();
        foreach (var (bn, bf) in Boards) Console.WriteLine($"- {bn}: {BA.SeatsNamed(Apply(bf(), Versions[2]))}");
        foreach (var (rn, rf) in Refs) Console.WriteLine($"- {rn}（版に依らない）: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
        foreach (var v in Versions) Console.WriteLine($"- {v.Name}: {v.What}——ボルグ ＋ [{string.Join(", ", v.Borg.Traits.Except(UnitCatalog.BorgU0.Traits))}] ／ ヒヨ ＋ [{string.Join(", ", v.Hiyo.Traits.Except(UnitCatalog.HiyoU0.Traits))}]");
        Console.WriteLine();
        Line(); TableA(); TableB(); TableC();
        Console.WriteLine($"（測定 {sec:F0} 秒・seed 0..{BA.Seeds - 1}・verbose）");
    }

    static void Line()
    {
        Console.WriteLine("## 線（§3）—— T3-244 と T3-238 の本編の第2〜5波 × 400/300 の勝率の平均");
        Console.WriteLine();
        Console.WriteLine("| 版 | T3-244 | T3-238 | 2席の平均 | U0 との差 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        double Avg(string v) => (MainAvg("T3-244", v, 1, WinP) + MainAvg("T3-238", v, 1, WinP)) / 2;
        double u0 = Avg("U0");
        foreach (var v in Versions)
            Console.WriteLine($"| {v.Name} | {F1(MainAvg("T3-244", v.Name, 1, WinP))} | {F1(MainAvg("T3-238", v.Name, 1, WinP))} | {Avg(v.Name):F2} | {Avg(v.Name) - u0:+0.00;-0.00;0.00} |");
        Console.WriteLine();
        double d = Avg("U2") - u0;
        string pick = d > -LinePt ? "U2" : (Avg("U1") >= u0 ? "U1" : "U0");
        Console.WriteLine($"**判定: U2 − U0 ＝ {d:+0.00;-0.00;0.00}pt（線 −{LinePt:F1}pt）→ 規定にするのは {pick}**");
        Console.WriteLine();
    }

    static void TableA()
    {
        Console.WriteLine("## 表A —— 台 × 版 × 波 × 倍率（全員生存 ／ 勝率 ／ 決着T）");
        Console.WriteLine();
        Console.WriteLine("的の波（HP 9999・攻1・倍率なし）は勝率を出さず、1〜8 ターン目の与ダメ（3体＋燃焼の刻み）/戦を出す。");
        Console.WriteLine();
        var nonT = Enumerable.Range(0, WaveNames.Length).Where(w => !IsTarget(w)).ToArray();
        var tg = Enumerable.Range(0, WaveNames.Length).Where(IsTarget).ToArray();
        Console.WriteLine("| 台 | 版 | 倍率 | " + string.Join(" | ", nonT.Select(w => WaveNames[w])) + " | 本編 第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", nonT.Length + 1)));
        string Cl(FC.CAgg c) => $"{F1(SurvP(c))} ／ {F1(WinP(c))} ／ {Per(c.Turns, c.N)}";
        foreach (var (bn, _) in Boards)
            foreach (var v in Versions)
                for (int s = 0; s < BA.Scales.Length; s++)
                    Console.WriteLine($"| {bn} | {v.Name} | {BA.Scales[s].Name} | " + string.Join(" | ", nonT.Select(w => Cl(At(bn, v.Name, w, s).C))) + $" | {F1(MainAvg(bn, v.Name, s, SurvP))} ／ {F1(MainAvg(bn, v.Name, s, WinP))} |");
        foreach (var (rn, _) in Refs)
            for (int s = 0; s < BA.Scales.Length; s++)
                Console.WriteLine($"| {rn} | — | {BA.Scales[s].Name} | " + string.Join(" | ", nonT.Select(w => Cl(At(rn, "—", w, s).C))) + $" | {F1(MainAvg(rn, "—", s, SurvP))} ／ {F1(MainAvg(rn, "—", s, WinP))} |");
        Console.WriteLine();
        Console.WriteLine("### 的の波（1〜8 ターン目の与ダメ/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", tg.Select(w => WaveNames[w] + "（3体＋刻み ／ 台の全員）")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", tg.Length)));
        foreach (var (bn, _) in Boards)
            foreach (var v in Versions)
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", tg.Select(w => { var c = At(bn, v.Name, w, 0).C; return $"{Per(TrioDealt(c), c.N)} ／ {Per(AllDealt(c), c.N)}"; })) + " |");
        Console.WriteLine("| 参考 移動 | ヨミ＋セロ | " + string.Join(" | ", tg.Select(w => { var c = At("参考 移動", "—", w, 0).C; return $"{Per(MoveDealt(c), c.N)} ／ {Per(AllDealt(c), c.N)}"; })) + " |");
        Console.WriteLine("| 参考 雷 | 台の全員 | " + string.Join(" | ", tg.Select(w => { var c = At("参考 雷", "—", w, 0).C; return $"— ／ {Per(AllDealt(c), c.N)}"; })) + " |");
        Console.WriteLine();
    }

    static readonly (int W, int S)[] KeyCells =
    {
        (BA.MainWave, 1), (BA.MainWave, 0), (BA.MainWave + 1, 1), (FC.WaveYoke, 1), (3, 1), (FC.WaveHeavy, 1), (FC.WaveTarget1, 0), (FC.WaveTarget9, 0),
    };
    static string WN(int w, int s) => IsTarget(w) ? $"{WaveNames[w]}（8T）" : $"{WaveNames[w]} {BA.Scales[s].Name}";

    static void TableB()
    {
        Console.WriteLine("## 表B —— 1戦の焼き尽くす・放つ・指名と、指名の空振り（火勢4 未満で指名した回数）");
        Console.WriteLine();
        Console.WriteLine("「指名しなかった」＝ ヒヨのギフトの手番に、印を持つが火勢4 未満のボルグがいた（U2 だけ・印は残る）。「印のまま受けた」＝ そのボルグが指名ではなくギフトを受けた（印は消えない）。");
        Console.WriteLine("「ギフト ボルグ ／ ホタ」はギフトの相手（のべ/戦）。空振りの差は U2 − U1。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | 焼き尽くす ／ 放つ /戦 | 指名/戦 | 空振り（指名・火勢4 未満）/戦 | U1 との差 | 指名 → 放った % | 指名しなかった ／ 印のまま受けた /戦 | ギフト ボルグ ／ ホタ /戦 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|---|---|");
        foreach (var (bn, _) in Boards)
            foreach (var (w, s) in KeyCells)
                foreach (var v in Versions)
                {
                    var cell = At(bn, v.Name, w, s); var t = cell.T;
                    long cn = t.CalledLv.Sum(), miss = t.CalledLv.Take(FireLevelRule.Max).Sum();
                    var t1 = At(bn, "U1", w, s).T; long miss1 = t1.CalledLv.Take(FireLevelRule.Max).Sum();
                    long gb = t.GiftSlotRole[1, 0] + t.GiftSlotRole[2, 0], gh = t.GiftSlotRole[1, 1] + t.GiftSlotRole[2, 1];
                    string diff = v.Name == "U2" ? ((double)(miss - miss1) / t.N).ToString("+0.00;-0.00;0.00") : "—";
                    Console.WriteLine($"| {bn} | {WN(w, s)} | {v.Name} | {Per(t.Burnouts, t.N)} ／ {Per(t.Unleashes, t.N)} | {Per(cn, t.N)} | {Per(miss, t.N)} | {diff} | {Pct(t.CalledUnleash, cn)} | {Per(cell.CallHeld, t.N)} ／ {Per(cell.CallHeldGift, t.N)} | {Per(gb, t.N)} ／ {Per(gh, t.N)} |");
                }
        Console.WriteLine();
        Console.WriteLine("### 表B′ —— 本編の第2〜5波の平均 × 400/300");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 焼き尽くす ／ 放つ /戦 | 指名/戦 | 空振り/戦 | 指名しなかった ／ 印のまま受けた /戦 |");
        Console.WriteLine("|---|---|---|--:|--:|---|");
        foreach (var (bn, _) in Boards)
            foreach (var v in Versions)
            {
                double M(Func<Cell, double> f) => MainWaves.Average(w => f(At(bn, v.Name, w, 1)));
                Console.WriteLine($"| {bn} | {v.Name} | {M(c => (double)c.T.Burnouts / c.N):F2} ／ {M(c => (double)c.T.Unleashes / c.N):F2} | {M(c => (double)c.T.CalledLv.Sum() / c.N):F2} | {M(c => (double)c.T.CalledLv.Take(FireLevelRule.Max).Sum() / c.N):F2} | {M(c => (double)c.CallHeld / c.N):F2} ／ {M(c => (double)c.CallHeldGift / c.N):F2} |");
            }
        Console.WriteLine();
    }

    static void TableC()
    {
        Console.WriteLine("## 表C —— 三角の循環（焼き尽くす → 指名 → 放つ → 呼び火）");
        Console.WriteLine();
        Console.WriteLine("「緩い三角」＝ 指名を問わない（焼き尽くす → ボルグへのギフト → 放つ → 呼び火）。的の波は 1〜8 ターン目＝8 ターンで何周。");
        Console.WriteLine("途切れた場所（指名の札のある版だけ・/戦）: " + string.Join(" ／ ", FT.BreakNames) + "。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | **三角/戦**（1回以上の戦 %） | 緩い三角/戦 | 途切れた " + string.Join(" ／ ", FT.BreakNames.Select((_, i) => (i + 1).ToString())) + " |");
        Console.WriteLine("|---|---|---|---|--:|---|");
        foreach (var (bn, _) in Boards.Take(2))
            foreach (var (w, s) in KeyCells)
                foreach (var v in Versions)
                {
                    var t = At(bn, v.Name, w, s).T;
                    Console.WriteLine($"| {bn} | {WN(w, s)} | {v.Name} | **{Per(t.Tri, t.N)}**（{Pct(t.TriBattles, t.N)}） | {Per(t.Loose, t.N)} | {string.Join(" ／ ", t.Break.Select(x => Per(x, t.N)))} |");
                }
        Console.WriteLine();
        Console.WriteLine("### 表C′ —— 本編の第2〜5波の平均 × 400/300 と、的・一の 8 ターン");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 本編 400/300 三角 ／ 緩い三角 /戦 | 的・一 8T の 三角 ／ 緩い三角 ／ 焼き尽くす ／ 放つ ／ ギフト |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var (bn, _) in Boards.Take(2))
            foreach (var v in Versions)
            {
                double M(Func<FT.TAgg, double> f) => MainWaves.Average(w => f(At(bn, v.Name, w, 1).T));
                var t = At(bn, v.Name, FC.WaveTarget1, 0).T;
                Console.WriteLine($"| {bn} | {v.Name} | {M(x => (double)x.Tri / x.N):F2} ／ {M(x => (double)x.Loose / x.N):F2} | {Per(t.Tri, t.N)} ／ {Per(t.Loose, t.N)} ／ {Per(t.Burnouts, t.N)} ／ {Per(t.Unleashes, t.N)} ／ {Per(t.GiftHands, t.N)} |");
            }
        Console.WriteLine();
    }
}
