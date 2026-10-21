using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using X = Sh364Diag;

// =====================================================================================
// sh365 —— 第365期【軽い期】段0: アカ AKn-A ／ ガレ GR-A を規定に ／ 本体: ヒサ×ミサの連鎖の解き明かし（数えるだけ・規則は変えない）。
// 指示書は design/PHASE365_SETTLE_CHAIN_TRACE_SPEC.md ／ 報告は design/PHASE365_SETTLE_CHAIN_TRACE.md。
//
//     dotnet run --project BattleSim -c Release 0 sh365 trace [seed]        # §4-2: 台 B × 勇者 × seed の T1〜T3 を出来事の順に（後3 ＝ 規定のアカ ／ 何もしない素体）
//     dotnet run --project BattleSim -c Release 0 sh365 count              # §4-3: 台 B × 勇者・seed 0..99・T1〜T5 の輪の数え上げ（後3 ＝ 規定のアカ ／ 何もしない素体）
//     dotnet run --project BattleSim -c Release 0 sh365 cuts               # §4-4: 切り C0〜C6 × 台 B × 勇者 ／ 近衛 ／ 大隊・seed 0..99
//     dotnet run --project BattleSim -c Release 0 sh365 check              # 自己検査
//
// 切りは engine の計測専用のスイッチ `BattleContext.ChainCut365`（スレッドごと・既定 0）。数え上げは `BattleContext.ChainCount365`（同・既定は偽）。
// **予測のファイルが無ければ count ／ cuts を走らせない**（R397・`.tmp/p365/predict.md`）。
// =====================================================================================
static partial class Sh365Diag
{
    internal const string PredictFile = ".tmp/p365/predict.md";
    const string Tag = "第365期";
    const int Seeds = 100, TT = 5;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "count" or "cuts" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh365: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "trace": Trace(int.Parse(A(3, "0"))); return;
            case "count": Count(); return;
            case "cuts": Cuts(); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh365: モードは trace / count / cuts / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Short(UnitDef d) => S307.Short(d);

    /// <summary>台 B（前1 ボルグ ／ 前3 ミサ ／ 中央 ヒサ ／ 後1 カド ／ 後3 アカ・段0 入りの規定）と、後3 を何もしない素体に替えた台。</summary>
    internal static Formation BoardB() => Formation.Build(front1: UnitCatalog.Borg, front3: UnitCatalog.Tome, center: UnitCatalog.HisaH367, back1: UnitCatalog.Kado, back3: UnitCatalog.Susu);   // 第368期: ヒサは第367期までの規定に固定（規定は H1 になった）
    internal static Formation BoardIdle() { var f = BoardB(); f[4] = X.R4; return f; }

    /// <summary>計測のスイッチを立てて1戦（終われば必ず戻す）。</summary>
    internal static (BattleResult R, List<UnitState> P, List<UnitState> E) Raw(Formation f, I.W w, int seed, int cut, bool count, bool verbose)
    {
        int c0 = BattleContext.ChainCut365; bool n0 = BattleContext.ChainCount365;
        BattleContext.ChainCut365 = cut; BattleContext.ChainCount365 = count;
        try { return I.Raw(f, w, seed, verbose: verbose); }
        finally { BattleContext.ChainCut365 = c0; BattleContext.ChainCount365 = n0; }
    }

    // ---------------------------------------------------------------------------------
    // §4-2 1戦の流れ
    // ---------------------------------------------------------------------------------
    static void Trace(int seed)
    {
        Console.WriteLine($"# {Tag} §4-2 1戦の流れ —— 台 B × 勇者 × seed {seed}（T1〜T3・出来事の順）");
        Console.WriteLine();
        Console.WriteLine("各行の右端は、その出来事の後の **羽の枚数 ／ 勇者の標の層 ／ 玉（溢れの溜まり）／ 叫びの回数（その戦の累計）**。`開戦` は T1 の頭より前。羽の1発ごとの「攻撃」と、その攻撃が勇者に入れた量は1行にまとめた。");
        Console.WriteLine();
        foreach (var (label, f) in new[] { ("後3 ＝ 規定のアカ（AKn-A）", BoardB()), ("後3 ＝ 何もしない素体", BoardIdle()) })
        {
            var (r, p, e) = Raw(f, I.Boss, seed, 0, true, verbose: true);
            var hero = e.First(u => u.HasTrait(TraitId.BossRise11));
            var misa = p.First(u => u.Def.Id == "tome");
            var mine = p.ToDictionary(u => u.InstanceId);
            string Nm(int? id) => id is int i ? (mine.TryGetValue(i, out var u) ? Short(u.Def) : i == hero.InstanceId ? "勇者" : "敵") : "—";
            int feathers = FeathersTrait.Initial, layer = 0, balls = 0, pool = 0, shouts = 0;
            bool lastRally = false;
            Console.WriteLine($"## {label}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
            Console.WriteLine();
            Console.WriteLine("| T | 出来事 | 羽 ／ 層 ／ 玉（溜まり）／ 叫び |");
            Console.WriteLine("|--:|---|---|");
            int turn = 0;
            string? pendingAtk = null;
            void Row(int t, string text) => Console.WriteLine($"| {(t == 0 ? "開戦" : t.ToString())} | {text} | {feathers} ／ {layer} ／ {balls}（{pool}）／ {shouts} |");
            foreach (var x in r.Events)
            {
                if (x.Turn > 3) break;
                if (x.Kind == BattleEventKind.TurnStart) { turn = x.Turn; Console.WriteLine($"| **{turn}** | **── ターン {turn} ──** | |"); continue; }
                bool rally = x.Kind == BattleEventKind.MarkRally;
                if (rally && !lastRally) shouts++;
                lastRally = rally;
                switch (x.Kind)
                {
                    case BattleEventKind.Feather:
                        if (x.Text == FeatherLabels.Gain) { feathers = x.Amount; Row(x.Turn, $"ミサの羽 +1（書き手 {Nm(x.ActorId)}）"); }
                        else if (x.Text == FeatherLabels.Volley) { feathers = x.Amount; Row(x.Turn, $"ミサの手番の羽の一振り（{x.Amount} 枚）"); }
                        else if (x.Text is FeatherLabels.Chase or FeatherLabels.Flow) pendingAtk = $"羽 {x.Slot} 発目が標を追う";
                        else if (x.Text == FeatherLabels.Spray) pendingAtk = $"羽 {x.Slot} 発目が乱射";
                        break;
                    case BattleEventKind.FeatherMark:
                        pendingAtk = $"ミサの羽が、指差された{(x.Text == FeatherMarkLabels.Ally ? "味方の " : " ")}{Nm(x.TargetId)} へ飛ぶ（標撃ち・層 {x.Amount}）";
                        if (x.TargetId == hero.InstanceId) layer = x.Amount;
                        break;
                    case BattleEventKind.MarkLayer:
                        if (x.TargetId == hero.InstanceId) { layer = x.Amount; Row(x.Turn, $"{Nm(x.ActorId)} が勇者に標（層 {x.Slot} → {x.Amount}）"); }
                        break;
                    case BattleEventKind.Framed:
                        if (x.Text == FramedLabels.Accuse) Row(x.Turn, $"**ヒサ『あいつがやった！』**（標の味方 {Nm(x.PartnerId)} が仲間に撃たれた → 勇者を指差す）");
                        break;
                    case BattleEventKind.MarkRally:
                        Row(x.Turn, $"ヒサの叫び『あいつを狙え！』→ {Nm(x.TargetId)} +{x.Amount}（攻撃したのは {Nm(x.PartnerId)}・層 {x.Slot}）");
                        break;
                    case BattleEventKind.CommandBall:
                        balls = x.Amount; pool = x.Slot;
                        Row(x.Turn, x.Text == CommandBallLabels.Gain ? $"叫びの溢れが玉になった（玉 {x.Amount}）" : x.Text == CommandBallLabels.Use ? "玉を使った" : "溢れを捨てた");
                        break;
                    case BattleEventKind.Command:
                        Row(x.Turn, $"**ヒサの号令（{x.Text}）** → 勇者に層 +{x.Amount}");
                        break;
                    case BattleEventKind.Attack:
                        if (x.ActorId is int ai && mine.ContainsKey(ai) && pendingAtk is null && x.TargetId == hero.InstanceId) pendingAtk = $"{Nm(ai)} が勇者を攻撃";
                        break;
                    case BattleEventKind.Damage:
                        if (x.ActorId is int di && mine.ContainsKey(di) && x.TargetId is int ti)
                        {
                            string what = pendingAtk ?? $"{Nm(di)} の一撃";
                            pendingAtk = null;
                            if (ti == hero.InstanceId) Row(x.Turn, $"{what} → 勇者に {x.Amount}");
                            else if (mine.ContainsKey(ti)) Row(x.Turn, $"{what} → 味方の {Nm(ti)} に {x.Amount}（同士討ち）");
                        }
                        else if (x.ActorId == hero.InstanceId && x.TargetId is int vi && mine.ContainsKey(vi)) Row(x.Turn, $"勇者 → {Nm(vi)} に {x.Amount}");
                        break;
                }
            }
            Console.WriteLine();
            var mt = r.TallyByUnit.GetValueOrDefault("tome")?.Ch365;
            Console.WriteLine($"ミサの羽 T3 の終わりまでに増えた入口: {Gains(mt, 0, 3)}。");
            Console.WriteLine();
        }
    }

    static long Sum(long[]? a, int field, int t0, int t1)
    {
        if (a is null) return 0;
        long s = 0;
        for (int t = t0; t <= t1; t++) s += a[field * BattleContext.Ch365.Width + t];
        return s;
    }
    static string Gains(long[]? a, int t0, int t1) =>
        $"『あいつがやった！』 {Sum(a, BattleContext.Ch365.GainAccuse, t0, t1)} ／ 号令 {Sum(a, BattleContext.Ch365.GainBurst, t0, t1)} ／ ほかの書き手 {Sum(a, BattleContext.Ch365.GainOther, t0, t1)} ／ 毎ターン +3 {Sum(a, BattleContext.Ch365.GainGrow, t0, t1)}";

    // ---------------------------------------------------------------------------------
    // §4-3 輪の数え上げ
    // ---------------------------------------------------------------------------------
    internal sealed class Agg
    {
        public long N, Wins, WinT;
        public readonly long[] Misa = new long[BattleContext.Ch365.Fields * BattleContext.Ch365.Width], Hisa = new long[BattleContext.Ch365.Fields * BattleContext.Ch365.Width];
        public readonly long[] Cnt = new long[TT + 1], Layer = new long[TT + 1], MisaDmg = new long[TT + 1], MaxMisaTurn = new long[1];
        public long T5Feathers, MisaMax;
        public void Add(Agg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; T5Feathers += o.T5Feathers; MisaMax += o.MisaMax;
            for (int i = 0; i < Misa.Length; i++) { Misa[i] += o.Misa[i]; Hisa[i] += o.Hisa[i]; }
            for (int t = 0; t <= TT; t++) { Cnt[t] += o.Cnt[t]; Layer[t] += o.Layer[t]; MisaDmg[t] += o.MisaDmg[t]; }
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
    }

    internal static Agg One(Formation f, I.W w, int seed, int cut)
    {
        var (r, p, e) = Raw(f, w, seed, cut, true, verbose: true);
        var a = new Agg { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var hero = e.FirstOrDefault(u => u.HasTrait(TraitId.BossRise11));
        var misa = p.FirstOrDefault(u => u.Def.Id == "tome");
        if (r.TallyByUnit.TryGetValue("tome", out var mt) && mt.Ch365 is { } m) for (int i = 0; i < m.Length; i++) a.Misa[i] += m[i];
        if (r.TallyByUnit.TryGetValue("hisa", out var ht) && ht.Ch365 is { } h) for (int i = 0; i < h.Length; i++) a.Hisa[i] += h[i];
        for (int t = 1; t <= Math.Min(TT, r.Turns); t++) a.Cnt[t] = 1;
        var turnDmg = new Dictionary<int, long>();
        var layerMax = new long[TT + 1];
        foreach (var x in r.Events)
        {
            int tn = x.Turn;
            if (misa is not null && x.Kind == BattleEventKind.Damage && x.ActorId == misa.InstanceId && x.TargetId is int ti && (hero is null ? ti >= 0 && !p.Any(u => u.InstanceId == ti) : ti == hero.InstanceId))
                turnDmg[tn] = turnDmg.GetValueOrDefault(tn) + x.Amount;
            if (hero is not null && x.TargetId == hero.InstanceId && tn >= 0 && tn <= TT && x.Kind is BattleEventKind.MarkLayer or BattleEventKind.FeatherMark)
                layerMax[tn] = Math.Max(layerMax[tn], x.Amount);
        }
        for (int t = 1; t <= TT; t++) { if (a.Cnt[t] > 0) { a.Layer[t] = layerMax[t]; a.MisaDmg[t] = turnDmg.GetValueOrDefault(t); } }
        a.MisaMax = turnDmg.Count == 0 ? 0 : turnDmg.Values.Max();
        // T5 の終わりの羽（初期 1 ＋ 開戦〜T5 に増えた羽・羽は減らない）。T5 まで続かなかった戦は決着の時点。
        a.T5Feathers = FeathersTrait.Initial + GainSum(a.Misa, 0, TT);
        return a;
    }
    static long GainSum(long[] m, int t0, int t1) =>
        Sum(m, BattleContext.Ch365.GainAccuse, t0, t1) + Sum(m, BattleContext.Ch365.GainBurst, t0, t1) + Sum(m, BattleContext.Ch365.GainOther, t0, t1) + Sum(m, BattleContext.Ch365.GainGrow, t0, t1);

    internal static Agg Measure(Formation f, I.W w, int cut, int n = Seeds)
    {
        var parts = new Agg[n];
        Parallel.For(0, n, i => parts[i] = One(f, w, i, cut));
        var all = new Agg();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static void Count()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §4-3 輪の数え上げ —— 台 B × 勇者・seed 0..{Seeds - 1}・T1〜T{TT}（1戦あたり・T の列は開戦 ＝ T0 を含む）");
        Console.WriteLine();
        Console.WriteLine("羽の枚数はターンの頭 ＝ 初期 1 ＋ それまでに増えた羽（羽は減らない）。各段は勝負が続いた戦も終わった戦も 100 戦で割った1戦あたり（決着の後は 0）。勇者の層はそのターンに付いた層の最大（ターンまで続いた戦の平均）。");
        Console.WriteLine();
        int W = BattleContext.Ch365.Width;
        foreach (var (label, f) in new[] { ("後3 ＝ 規定のアカ（AKn-A）", BoardB()), ("後3 ＝ 何もしない素体", BoardIdle()) })
        {
            var a = Measure(f, I.Boss, 0);
            Console.WriteLine($"## {label}（勝率 {F1(a.Win)}%・倒しT {a.WinTs}）");
            Console.WriteLine();
            Console.WriteLine("| 段 | 開戦 | " + string.Join(" | ", Enumerable.Range(1, TT).Select(t => $"T{t}")) + " |");
            Console.WriteLine("|---|--:|" + string.Concat(Enumerable.Range(1, TT).Select(_ => "--:|")));
            string R(long[] arr, int field) => string.Join(" | ", Enumerable.Range(0, TT + 1).Select(t => Per(arr[field * W + t], a.N)));
            long cum = FeathersTrait.Initial * a.N;
            var head = new List<string>();
            for (int t = 0; t <= TT; t++) { head.Add(Per(cum, a.N)); cum += GainSum(a.Misa, t, t); }
            Console.WriteLine($"| **羽の枚数（その段の頭）** | {string.Join(" | ", head)} |");
            Console.WriteLine($"| 羽の枚数（T{TT} の終わり） | | {string.Join(" | ", Enumerable.Range(1, TT).Select(t => t < TT ? "" : Per(cum, a.N)))} |");
            Console.WriteLine($"| 増えた羽: 『あいつがやった！』 | {R(a.Misa, BattleContext.Ch365.GainAccuse)} |");
            Console.WriteLine($"| 増えた羽: 号令（玉 → 指差し） | {R(a.Misa, BattleContext.Ch365.GainBurst)} |");
            Console.WriteLine($"| 増えた羽: ほかの書き手 | {R(a.Misa, BattleContext.Ch365.GainOther)} |");
            Console.WriteLine($"| 増えた羽: 標が無い間の +3 | {R(a.Misa, BattleContext.Ch365.GainGrow)} |");
            Console.WriteLine($"| ミサの手番の一振り（回） | {R(a.Misa, BattleContext.Ch365.Volleys)} |");
            Console.WriteLine($"| 手番の羽: 標を追った発（勇者へ） | {R(a.Misa, BattleContext.Ch365.VolleyChase)} |");
            Console.WriteLine($"| 手番の羽: 乱射（敵味方） | {R(a.Misa, BattleContext.Ch365.VolleySpray)} |");
            Console.WriteLine($"| 標撃ち（割り込み）: 勇者へ | {R(a.Misa, BattleContext.Ch365.MfFoe)} |");
            Console.WriteLine($"| 標撃ち（割り込み）: 標の味方へ | {R(a.Misa, BattleContext.Ch365.MfAlly)} |");
            Console.WriteLine($"| 標撃ちが控えられた（層の追加） | {R(a.Misa, BattleContext.Ch365.MfQueuedLayer)} |");
            Console.WriteLine($"| **ヒサの叫び（回）** | {R(a.Hisa, BattleContext.Ch365.Rally)} |");
            Console.WriteLine($"| うち羽のまとまりの叫び | {R(a.Hisa, BattleContext.Ch365.RallyFeather)} |");
            Console.WriteLine($"| 叫びの溢れ（量） | {R(a.Hisa, BattleContext.Ch365.Overflow)} |");
            Console.WriteLine($"| 玉ができた（溢れ 20 で1つ） | {R(a.Hisa, BattleContext.Ch365.Balls)} |");
            Console.WriteLine($"| **号令（あふれ）で刻んだ層** | {R(a.Hisa, BattleContext.Ch365.BurstLayers)} |");
            Console.WriteLine($"| 号令が上限（1ターン 9 層）で止まった（回） | {R(a.Hisa, BattleContext.Ch365.BurstCapped)} |");
            Console.WriteLine($"| 『あいつがやった！』（回） | {R(a.Hisa, BattleContext.Ch365.AccuseLayers)} |");
            Console.WriteLine($"| 勇者の標の層（そのターンの最大） | — | {string.Join(" | ", Enumerable.Range(1, TT).Select(t => Per(a.Layer[t], a.Cnt[t])))} |");
            Console.WriteLine($"| ミサの勇者への与ダメ（そのターン） | — | {string.Join(" | ", Enumerable.Range(1, TT).Select(t => Per(a.MisaDmg[t], a.Cnt[t])))} |");
            Console.WriteLine($"| 勝負が続いた戦 | — | {string.Join(" | ", Enumerable.Range(1, TT).Select(t => a.Cnt[t].ToString()))} |");
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-4 輪の切り分け
    // ---------------------------------------------------------------------------------
    internal static readonly (int Cut, string Name)[] CutList =
    {
        (0, "C0 切らない（規定）"),
        (1, "C1 羽の一撃では叫ばない"),
        (2, "C2 号令では羽が増えない"),
        (3, "C3 『あいつがやった！』では羽が増えない"),
        (4, $"C4 羽が増えるのは1ターン {BattleContext.ChainCap365} 枚まで"),
        (5, "C5 層が増えても羽が飛ばない（標撃ちは新しい標だけ）"),
        (6, "C6 叫びの溢れが玉にならない（号令が出ない）"),
    };

    static void Cuts()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var waves = new[] { I.Boss, I.Guard, I.Bat };
        Console.WriteLine($"# {Tag} §4-4 輪の切り分け —— 台 B（後3 ＝ 規定のアカ）× 勇者 ／ 近衛 ／ 大隊・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("切りは器具の中だけで立てる計測専用のスイッチ（engine の `ChainCut365`・既定 0）。C5 ／ C6 は指示書の C1〜C4 に足した切り（報告 §1）。羽（T5）は T5 の終わり（決着が早ければ決着の時点）の枚数。ミサの最大は1ターンにミサが敵へ入れた量の最大（1戦あたり）。");
        Console.WriteLine();
        Console.WriteLine("| 切り | 勇者 勝率・倒しT | 羽（T5） | ミサの1ターンの最大（勇者） | 近衛 | 大隊 | 近衛の羽（T5） |");
        Console.WriteLine("|---|---|--:|--:|---|---|--:|");
        foreach (var (cut, name) in CutList)
        {
            var b = Measure(BoardB(), I.Boss, cut); var g = Measure(BoardB(), I.Guard, cut); var t = Measure(BoardB(), I.Bat, cut);
            Console.WriteLine($"| {name} | {F1(b.Win)}・{b.WinTs}T | {Per(b.T5Feathers, b.N)} | {Per(b.MisaMax, b.N)} | {F1(g.Win)}・{g.WinTs}T | {F1(t.Win)}・{t.WinTs}T | {Per(g.T5Feathers, g.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
