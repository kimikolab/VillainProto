using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;

// =====================================================================================
// db355 —— 第355期【軽い期】段0: ムドの呪い（M1＋M2＋配り）を規定に ／ 本体: 呪われた傷は癒えない（N-100 ／ N-50 ／ U・版）。
// 指示書は design/PHASE355_CURSE_SCAR_SPEC.md ／ 報告は design/PHASE355_CURSE_SCAR.md。波と1戦の計数は `inv348` ／ `db351` をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 db355 p0                  # §3 Phase 0: 「呪われている」の判定（勇者が呪われた T と書き手）・机上の到達（試遊・呪い × ボス・規定・seed 0..19）
//     dotnet run --project BattleSim -c Release 0 db355 boards [版,…]       # §6 の 1・2・5: 代表台 × 波 × 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 db355 axes                # §6 の 3: 軸の横並び（第348期 §3）のデバフの行 × 版
//     dotnet run --project BattleSim -c Release 0 db355 compare [版,…]      # §6 の 4: `compare` 64 行 × 版（seed 0..199）＋ 段0 の前とのセルの差
//     dotnet run --project BattleSim -c Release 0 db355 log <版> <台> <波> [seed]   # 1戦のログ（台 play ／ 逆しま ／ fast）
//     dotnet run --project BattleSim -c Release 0 db355 check               # 自己検査
//
// 版の名前（ASCII）: base（規定・段0 入り）／ n100 ／ n50 ／ u。版はネル ／ ウツだけを差し替える（Id で引く・規定の上）。
// **予測のファイルが無ければ版の本測定（boards ／ axes ／ compare）を走らせない**（R397・`.tmp/p355/predict.md`）。
// =====================================================================================
static partial class Db355Diag
{
    internal const string PredictFile = ".tmp/p355/predict.md";
    const string Tag = "第355期";
    const int Seeds = 100, CompareSeeds = 200, TrajT = 12, P0Seeds = 20;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "axes" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"db355: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": Phase0(); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "axes": AxesAll(); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "log": Log(V(A(3, "base")), A(4, "play"), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("db355: モードは p0 / boards / axes / compare / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);

    // ---------------------------------------------------------------------------------
    // 版
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef Nel, UnitDef Utsu);
    internal static readonly Ver[] Vers =
    {
        new("規定（段0 入り）", "base", UnitCatalog.NelH355, UnitCatalog.Utsu),   // 第356期: 規定のネルが N-100 になったので、第355期の「規定」は旧の定義に固定
        new("N-100 呪われた傷（全量）", "n100", UnitCatalog.NelN100, UnitCatalog.Utsu),
        new("N-50 呪われた傷（半分）", "n50", UnitCatalog.NelN50, UnitCatalog.Utsu),
        new("U ウツの爪痕", "u", UnitCatalog.NelH355, UnitCatalog.UtsuU),
    };
    internal static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(S316.SwapId(f, "nel", v.Nel), "utsu", v.Utsu);

    // ---------------------------------------------------------------------------------
    // 台
    // ---------------------------------------------------------------------------------
    internal const string PlayRow = D.PlayRow;   // 「試遊・呪い」（ワタ・ムド・クビ・ネル・ウツ・段0 のムド ／ ワタ）
    internal static Formation Play => Pin355(CurseBoardH356);   // 第356期: ネルは第355期までの規定に固定・第357期: 並びは第351〜356期の「試遊・呪い」に固定
    /// <summary>ネルのいる `compare` の行からもう1つ（決めたこと: ウツがいない・燃焼の刻み ／ ムドの暴発が削り手になる行・第354期のムドの行と同じ）。</summary>
    internal const string FastRow = "速攻 (ボルグ×ムド)";
    static Formation Row(string n) => Pin355(CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F);   // 第356期: ネルは旧
    internal static I.Board[] Boards() => new[]
    {
        new I.Board("呪い", PlayRow, Play),
        new I.Board("呪い", "逆しま (ネル×ウツ)", Row("逆しま (ネル×ウツ)")),
        new I.Board("呪い", FastRow, Row(FastRow)),
    };
    static I.Board BoardOf(string n) => n switch
    {
        "play" => Boards()[0], "fast" => Boards()[2],
        _ => Boards().FirstOrDefault(b => b.Name.StartsWith(n, StringComparison.Ordinal))
            ?? CompareBuilds().Concat(CrossBuilds()).Where(r => r.Name.StartsWith(n, StringComparison.Ordinal)).Select(r => new I.Board("行", r.Name, Pin355(r.F))).First(),   // `compare` ／ 交差帯の行も名前の頭で引ける
    };
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static bool Applies(I.Board b, Ver v) => v == Vers[0] || (v.Nel != UnitCatalog.NelH355 ? Has(b.F, "nel") : Has(b.F, "utsu"));

    internal static readonly I.W[] Waves = D.Waves;

    static void Log(Ver v, string board, string wave, int seed)
    {
        var b = BoardOf(board); var w = D.WaveOf(wave);
        var (r, _, _) = I.Raw(Apply(b.F, v), w, seed, verbose: true);
        Console.WriteLine($"# {b.Name}（{Seats(b.F)}）× {w.Name} × seed {seed} × {v.Name}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose・帳簿と台本を読む・盤面は動かさない）
    // ---------------------------------------------------------------------------------
    internal sealed class Fx
    {
        public long N, Wins, WinT, Fell, Lost, BossN, BossMax0, LostLow, LostLowMin = long.MaxValue, LostMaxEnd, LostMaxEndMin = long.MaxValue, WipeT, CursedT, CursedN;
        public readonly Dictionary<string, long> ScarBy = new(), CurseBy = new();
        public long ScarAll, ScarNoSrc;
        // ターンごと（そのターンまで戦が続いた戦の和 ／ 数）: ボスの最大HP（ターン末）／ 味方 → ボスの与ダメ（ウツ ／ ムド ／ ほか）／ ボスの回復 ／ 削った最大HP
        public readonly int T;   // 第356期: 追うターン数（既定 12・`db356` は 14）
        public readonly long[] TMax, TAll, TUtsu, TMudo, THeal, TScar, TCnt;
        public Fx(int t = TrajT) { T = t; TMax = new long[t + 1]; TAll = new long[t + 1]; TUtsu = new long[t + 1]; TMudo = new long[t + 1]; THeal = new long[t + 1]; TScar = new long[t + 1]; TCnt = new long[t + 1]; }
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Lost += o.Lost; BossN += o.BossN; BossMax0 += o.BossMax0;
            LostLow += o.LostLow; LostLowMin = Math.Min(LostLowMin, o.LostLowMin); LostMaxEnd += o.LostMaxEnd; LostMaxEndMin = Math.Min(LostMaxEndMin, o.LostMaxEndMin);
            WipeT += o.WipeT; CursedT += o.CursedT; CursedN += o.CursedN; ScarAll += o.ScarAll; ScarNoSrc += o.ScarNoSrc;
            foreach (var (k, x) in o.ScarBy) ScarBy[k] = ScarBy.GetValueOrDefault(k) + x;
            foreach (var (k, x) in o.CurseBy) CurseBy[k] = CurseBy.GetValueOrDefault(k) + x;
            for (int t = 0; t <= Math.Min(T, o.T); t++) { TMax[t] += o.TMax[t]; TAll[t] += o.TAll[t]; TUtsu[t] += o.TUtsu[t]; TMudo[t] += o.TMudo[t]; THeal[t] += o.THeal[t]; TScar[t] += o.TScar[t]; TCnt[t] += o.TCnt[t]; }
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
    }

    internal static Fx One(Formation f, I.W w, int seed, int tt = TrajT)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Fx(tt) { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else a.Lost = 1;
        a.Fell = p.Count(u => u.LastDeathTurn > 0);
        var utsu = p.FirstOrDefault(u => u.Def.Id == "utsu"); var mudo = p.FirstOrDefault(u => u.Def.Id == "mudo");
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        foreach (var x in r.Events) if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && x.Team == BattleContext.PlayerTeam) mine.Add(sid);
        var name = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        int last = r.Events.Where(x => x.Kind == BattleEventKind.StatSnapshot && x.TargetId is int sn && mine.Contains(sn)).Select(x => x.Turn).DefaultIfEmpty(0).Max();
        for (int t = 1; t <= Math.Min(tt, last); t++) a.TCnt[t] = 1;
        if (!r.PlayerWon) a.WipeT = last;
        foreach (var x in r.Events)
            if (x.Kind == BattleEventKind.Scar && x.Team != BattleContext.PlayerTeam)
            {
                string k = x.ActorId is int ai ? name.GetValueOrDefault(ai, "召喚") : "（出どころなし）";
                a.ScarBy[k] = a.ScarBy.GetValueOrDefault(k) + x.Amount; a.ScarAll += x.Amount;
                if (x.ActorId is null) a.ScarNoSrc += x.Amount;
            }
        var boss = e.FirstOrDefault(u => u.HasTrait(TraitId.BossRise11));
        if (boss is not null)
        {
            a.BossN = 1;
            int max0 = boss.Def.MaxHp; a.BossMax0 = max0;
            long low = max0, max = max0;
            var maxAt = new long[tt + 2];
            int curT = 0; bool cursed = false;
            foreach (var x in r.Events)
            {
                while (curT < x.Turn && curT <= tt) { maxAt[curT] = max; curT++; }
                if (x.TargetId == boss.InstanceId && x.Kind is BattleEventKind.Damage or BattleEventKind.Heal) low = Math.Min(low, x.HpAfter);
                if (x.Kind == BattleEventKind.Scar && x.TargetId == boss.InstanceId) max = x.Slot;
                if (!cursed && x.Kind == BattleEventKind.StatusGain && x.Text == StatusKeys.Curse && x.TargetId == boss.InstanceId)
                {
                    cursed = true; a.CursedN = 1; a.CursedT = x.Turn;
                    string k = x.ActorId is int ci ? name.GetValueOrDefault(ci, "?") : "?";
                    a.CurseBy[k] = a.CurseBy.GetValueOrDefault(k) + 1;
                }
                int t = x.Turn;
                if (t < 1 || t > tt || t > last) continue;
                if (x.Kind == BattleEventKind.Damage && x.TargetId == boss.InstanceId && x.ActorId is int ai && mine.Contains(ai))
                {
                    a.TAll[t] += x.Amount;
                    if (utsu is not null && ai == utsu.InstanceId) a.TUtsu[t] += x.Amount;
                    if (mudo is not null && ai == mudo.InstanceId) a.TMudo[t] += x.Amount;
                }
                if (x.Kind == BattleEventKind.Heal && x.TargetId == boss.InstanceId) a.THeal[t] += x.Amount;
                if (x.Kind == BattleEventKind.Scar && x.TargetId == boss.InstanceId) a.TScar[t] += x.Amount;
            }
            while (curT <= tt) { maxAt[curT] = max; curT++; }
            for (int t = 1; t <= tt; t++) if (a.TCnt[t] > 0) a.TMax[t] = maxAt[t];   // maxAt[t] ＝ ターン t の最後の出来事の後（ターン t+1 の最初の出来事の手前）
            if (!r.PlayerWon) { a.LostLow = low; a.LostLowMin = low; a.LostMaxEnd = boss.MaxHp; a.LostMaxEndMin = boss.MaxHp; }
        }
        return a;
    }

    internal static Fx Measure(Formation f, I.W w, int n = Seeds, int tt = TrajT)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, i => parts[i] = One(f, w, i, tt));
        var all = new Fx(tt);
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static string Kv(Dictionary<string, long> d, long n) => string.Join("、", d.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per(kv.Value, n)}"));

    internal static void PrintTraj(string title, Fx a)
    {
        Console.WriteLine($"#### {title}（勝率 {F1(a.Win)}%・倒しT {a.WinTs}・全滅 T {Per(a.WipeT, a.Lost)}・負けた戦: 最後の最大HP 平均 {Per(a.LostMaxEnd, a.Lost)} ／ 最小 {(a.Lost == 0 ? "—" : a.LostMaxEndMin.ToString())}・最も減った HP 平均 {Per(a.LostLow, a.Lost)}）");
        Console.WriteLine();
        Console.WriteLine($"削った最大HP（敵の側・1戦あたり・誰の一撃で）: {Per(a.ScarAll, a.N)}（{Kv(a.ScarBy, a.N)}）。ターン t まで戦が続いた戦の平均。与ダメはボスへの分だけ。");
        Console.WriteLine();
        Console.WriteLine("| T | 続いた戦 | ボスの最大HP（ターン末） | 削った | 味方の与ダメ | うちウツ | うちムド | ほか | ボスの回復 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int t = 1; t <= a.T; t++)
        {
            long c = a.TCnt[t];
            if (c == 0) continue;
            Console.WriteLine($"| {t} | {c} | {Per(a.TMax[t], c)} | {Per(a.TScar[t], c)} | {Per(a.TAll[t], c)} | {Per(a.TUtsu[t], c)} | {Per(a.TMudo[t], c)} | {Per(a.TAll[t] - a.TUtsu[t] - a.TMudo[t], c)} | {Per(a.THeal[t], c)} |");
        }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §3 Phase 0
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var a = Measure(Play, I.Boss, P0Seeds);
        Console.WriteLine($"# {Tag} §3 Phase 0 —— 「{PlayRow}」（{Seats(Play)}・規定・段0 のムド ／ ワタ）× ボス・seed 0..{P0Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("## 1. 「呪われている」の判定");
        Console.WriteLine();
        Console.WriteLine($"「呪われている」＝ 私有でない状態キー `{StatusKeys.Curse}`（`StatusKeys.Curse`・二値）が 1。付けるのはネルの `Hexer`（手番で、まだ呪われていない敵のうち最も攻撃力の高い1体）とムドの `Hex`（殴ってきた相手）。"
            + "ネルの開戦時の呪詛（`Curse`）は弱体（攻撃力を下げる）だけで、この印は付けない。");
        Console.WriteLine();
        Console.WriteLine($"勇者が呪われた T: 平均 {Per(a.CursedT, a.CursedN)}（呪われた戦 {a.CursedN} ／ {a.N}）・最初に付けた駒: {Kv(a.CurseBy, a.CursedN)}。");
        Console.WriteLine();
        Console.WriteLine("## 2. 机上の到達（ターンごとの味方の与ダメは規定の実測・削る割合 f）");
        Console.WriteLine();
        double m0 = (double)a.BossMax0 / Math.Max(1, a.BossN);
        int wipe = (int)Math.Round((double)a.WipeT / Math.Max(1, a.Lost));
        Console.WriteLine($"勇者の最大HP {m0:0}・ターン頭に最大HPの 40% 癒える（満タンまで）。味方の全滅は T{wipe}。モデル: ターン頭に回復 → 与ダメ D_t → 生き残れば最大HPから f × 減った HP を削る（今の HP は新しい最大HPで切る）。全滅の後も D は最後の値のまま続くとして延ばす。");
        Console.WriteLine();
        double[] dAll = new double[TrajT + 1], dUtsu = new double[TrajT + 1];
        for (int t = 1; t <= TrajT; t++) if (a.TCnt[t] > 0) { dAll[t] = (double)a.TAll[t] / a.TCnt[t]; dUtsu[t] = (double)a.TUtsu[t] / a.TCnt[t]; }
        double lastAll = Enumerable.Range(1, TrajT).Where(t => dAll[t] > 0).Select(t => dAll[t]).DefaultIfEmpty(0).Last(), lastUtsu = Enumerable.Range(1, TrajT).Where(t => dUtsu[t] > 0).Select(t => dUtsu[t]).DefaultIfEmpty(0).Last();
        Console.WriteLine("| 版のモデル | 削る分 | 倒せる T | 全滅との差 | T10 の最大HP |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        foreach (var (label, f, utsuOnly) in new[] { ("N-100", 1.0, false), ("N-50", 0.5, false), ("U（ウツの一撃だけ）", 1.0, true), ("削らない（規定）", 0.0, false) })
        {
            double max = m0, hp = m0; int kill = 0; double max10 = 0;
            for (int t = 1; t <= 40 && kill == 0; t++)
            {
                hp = Math.Min(max, hp + max * 0.4);
                double d = t <= TrajT && dAll[t] > 0 ? dAll[t] : lastAll, du = t <= TrajT && dUtsu[t] > 0 ? dUtsu[t] : lastUtsu;
                double lost = Math.Min(hp, d);
                hp -= lost;
                if (hp <= 0) { kill = t; break; }
                double cut = f * (utsuOnly ? Math.Min(du, lost) : lost);
                max = Math.Max(1, max - cut); hp = Math.Min(hp, max);
                if (t == 10) max10 = max;
            }
            Console.WriteLine($"| {label} | {(f == 0 ? "—" : utsuOnly ? "ウツの与ダメ × 1" : $"与ダメ × {f:0.0}")} | {(kill == 0 ? "40T でも倒せない" : $"T{kill}")} | {(kill == 0 ? "—" : Sg(kill - wipe) + "T")} | {max10:0} |");
        }
        Console.WriteLine();
        PrintTraj("規定 × ボス（実測）", a);
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
