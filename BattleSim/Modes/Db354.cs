using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;

// =====================================================================================
// db354 —— 第354期【軽い期】段0: ウツの上限超え（UT-o）を規定に ／ 本体: ムドを呪いで動く二人目の火力に（M1 ／ M2 ／ M1＋M2 ／ M1＋M2＋配り・版）。
// 指示書は design/PHASE354_UTSU_MUDO_SPEC.md ／ 報告は design/PHASE354_UTSU_MUDO.md。波と1戦の計数は `inv348` ／ `db351` をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 db354 p0                  # §3 Phase 0: ムドの働き ／ ワタがムドから拾った量 ／ ボスの1ターンの与ダメの内訳（試遊・呪い・規定・ボス ／ 近衛 ／ 大隊 × seed 0..99）
//     dotnet run --project BattleSim -c Release 0 db354 boards [版,…]       # §6 の 1〜3・5: 代表台 × 波 × 版（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 db354 compare [版,…]      # §6 の 4: `compare` 64 行 × 版（seed 0..199）
//     dotnet run --project BattleSim -c Release 0 db354 log <版> <台> <波> [seed]   # 1戦のログ（台 play ／ fast ／ 逆しま）
//     dotnet run --project BattleSim -c Release 0 db354 check               # 自己検査
//
// 版の名前（ASCII）: base（規定・段0 入り）／ m1 ／ m2 ／ m12 ／ m12f。版はムド ／ ワタだけを差し替える（Id で引く・規定の上）。
// **予測のファイルが無ければ版の本測定（boards ／ compare）を走らせない**（R397・`.tmp/p354/predict.md`）。
// =====================================================================================
static partial class Db354Diag
{
    internal const string PredictFile = ".tmp/p354/predict.md";
    const string Tag = "第354期";
    const int Seeds = 100, CompareSeeds = 200, TrajT = 12;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"db354: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": Phase0(); return;
            case "boards": BoardsAll(Pick(A(3, ""))); return;
            case "compare": CompareAll(Pick(A(3, ""))); return;
            case "log": Log(V(A(3, "base")), A(4, "play"), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("db354: モードは p0 / boards / compare / log / check。"); return;
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
    internal sealed record Ver(string Name, string Ascii, UnitDef Mudo, UnitDef Wata);
    internal static readonly Ver[] Vers =
    {
        new("規定（段0 入り）", "base", UnitCatalog.Mudo, UnitCatalog.Wata),
        new("M1 呪いが怒りに", "m1", UnitCatalog.MudoM1, UnitCatalog.Wata),
        new("M2 呪われるほど重い", "m2", UnitCatalog.MudoM2, UnitCatalog.Wata),
        new("M1 ＋ M2", "m12", UnitCatalog.MudoM12, UnitCatalog.Wata),
        new("M1 ＋ M2 ＋ 配り", "m12f", UnitCatalog.MudoM12, UnitCatalog.WataFeedMudo),
    };
    internal static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v) => S316.SwapId(S316.SwapId(f, "mudo", v.Mudo), "wata", v.Wata);

    // ---------------------------------------------------------------------------------
    // 台
    // ---------------------------------------------------------------------------------
    internal const string PlayRow = D.PlayRow;   // 「試遊・呪い」（ワタ・ムド・クビ・ネル・ウツ・段0 のウツ）
    internal static Formation Play => Presets.Versions.First(r => r.Name == PlayRow).F;
    /// <summary>ムドのいる `compare` の行から1つ（決めたこと: ワタ ／ ウツがいない・ネルの呪詛の漏れでムドに呪いが乗る行）。</summary>
    internal const string FastRow = "速攻 (ボルグ×ムド)";
    static Formation Row(string n) => CompareBuilds().Concat(CrossBuilds()).First(r => r.Name == n).F;
    internal static I.Board[] Boards() => new[]
    {
        new I.Board("呪い", PlayRow, Play),
        new I.Board("ムド", FastRow, Row(FastRow)),
        new I.Board("呪い", "逆しま (ネル×ウツ)", Row("逆しま (ネル×ウツ)")),
    };
    static I.Board BoardOf(string n) => n switch
    {
        "play" => Boards()[0], "fast" => Boards()[1],
        _ => Boards().First(b => b.Name.StartsWith(n, StringComparison.Ordinal)),
    };
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    /// <summary>その台で版が何かを差し替えるか（ムドがいなければ M は空・ワタがいなければ配りは空）。</summary>
    static bool Applies(I.Board b, Ver v) => v == Vers[0] || Has(b.F, "mudo") && (v.Wata == UnitCatalog.Wata || Has(b.F, "wata"));

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
        public long N, Wins, WinT, Fell, Lost, LostBossLow, LostBossLowMin = long.MaxValue, BossN;
        // ムド
        public long MudoN, Fuel, FuelAlly, Fires, Swings, SwingAtk, Peak, Held, CurseRage, Dulled, InTurn, OffTurn, YokeCut, YokeLost, MudoDied, MudoDeathT, PickedMudo, FedMudo, FedUtsu;
        public readonly Dictionary<string, long> DullFrom = new();
        // ターンごと（そのターンまで戦が続いた戦の和 ／ 数）: 味方 → ボスの与ダメ（ウツ ／ ムド ／ ほか）／ ボスの回復
        public readonly long[] TAll = new long[TrajT + 1], TUtsu = new long[TrajT + 1], TMudo = new long[TrajT + 1], THeal = new long[TrajT + 1], TCnt = new long[TrajT + 1];
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Lost += o.Lost; LostBossLow += o.LostBossLow; LostBossLowMin = Math.Min(LostBossLowMin, o.LostBossLowMin); BossN += o.BossN;
            MudoN += o.MudoN; Fuel += o.Fuel; FuelAlly += o.FuelAlly; Fires += o.Fires; Swings += o.Swings; SwingAtk += o.SwingAtk; Peak = Math.Max(Peak, o.Peak); Held += o.Held; CurseRage += o.CurseRage;
            Dulled += o.Dulled; InTurn += o.InTurn; OffTurn += o.OffTurn; YokeCut += o.YokeCut; YokeLost += o.YokeLost; MudoDied += o.MudoDied; MudoDeathT += o.MudoDeathT;
            PickedMudo += o.PickedMudo; FedMudo += o.FedMudo; FedUtsu += o.FedUtsu;
            foreach (var (k, x) in o.DullFrom) DullFrom[k] = DullFrom.GetValueOrDefault(k) + x;
            for (int t = 0; t <= TrajT; t++) { TAll[t] += o.TAll[t]; TUtsu[t] += o.TUtsu[t]; TMudo[t] += o.TMudo[t]; THeal[t] += o.THeal[t]; TCnt[t] += o.TCnt[t]; }
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
    }

    internal static Fx One(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Fx { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else a.Lost = 1;
        a.Fell = p.Count(u => u.LastDeathTurn > 0);
        var mudo = p.FirstOrDefault(u => u.Def.Id == "mudo"); var utsu = p.FirstOrDefault(u => u.Def.Id == "utsu");
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        foreach (var x in r.Events) if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && x.Team == BattleContext.PlayerTeam) mine.Add(sid);
        var name = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        if (mudo is not null && r.TallyByUnit.TryGetValue("mudo", out var tm))
        {
            a.MudoN = 1; a.Fuel = tm.EruptFuel; a.FuelAlly = tm.EruptFuelFromAlly; a.Fires = tm.EruptFires; a.Swings = tm.EruptSwings; a.SwingAtk = tm.EruptSwingAtk; a.Peak = tm.EruptPeak; a.Held = tm.EruptHeld;
            a.CurseRage = tm.Db354CurseRage; a.Dulled = tm.Dulled; a.InTurn = tm.DmgOutInTurn; a.OffTurn = tm.DmgOutOffTurn; a.YokeCut = tm.Db354BurstYokeCut; a.YokeLost = tm.Db354BurstYokeLost;
            if (mudo.LastDeathTurn > 0) { a.MudoDied = 1; a.MudoDeathT = mudo.LastDeathTurn; }
            foreach (var x in r.Events)
                if (x.Kind == BattleEventKind.StatusGain && x.Text == BattleContext.DullKey && x.TargetId == mudo.InstanceId)
                {
                    string k = x.ActorId is int ai ? name.GetValueOrDefault(ai, "召喚") : "（書き手なし）";
                    a.DullFrom[k] = a.DullFrom.GetValueOrDefault(k) + x.Amount;
                }
        }
        if (r.TallyByUnit.TryGetValue("wata", out var tw)) { a.PickedMudo = tw.Db354PickedMudo; a.FedMudo = tw.Db354FedMudo; a.FedUtsu = tw.Db351Fed; }
        var boss = e.FirstOrDefault(u => u.HasTrait(TraitId.BossRise11));
        int last = r.Events.Where(x => x.Kind == BattleEventKind.StatSnapshot && x.TargetId is int sn && mine.Contains(sn)).Select(x => x.Turn).DefaultIfEmpty(0).Max();
        for (int t = 1; t <= Math.Min(TrajT, last); t++) a.TCnt[t] = 1;
        if (boss is not null)
        {
            a.BossN = 1;
            long low = boss.MaxHp;
            foreach (var x in r.Events)
            {
                if (x.TargetId == boss.InstanceId && x.Kind is BattleEventKind.Damage or BattleEventKind.Heal) low = Math.Min(low, x.HpAfter);
                int t = x.Turn;
                if (t < 1 || t > TrajT || t > last) continue;
                if (x.Kind == BattleEventKind.Damage && x.TargetId == boss.InstanceId && x.ActorId is int ai && mine.Contains(ai))
                {
                    a.TAll[t] += x.Amount;
                    if (utsu is not null && ai == utsu.InstanceId) a.TUtsu[t] += x.Amount;
                    if (mudo is not null && ai == mudo.InstanceId) a.TMudo[t] += x.Amount;
                }
                if (x.Kind == BattleEventKind.Heal && x.TargetId == boss.InstanceId) a.THeal[t] += x.Amount;
            }
            if (!r.PlayerWon) { a.LostBossLow = low; a.LostBossLowMin = low; }
        }
        return a;
    }

    internal static Fx Measure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, i => parts[i] = One(f, w, i));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static string Kv(Dictionary<string, long> d, long n) => string.Join("、", d.Where(kv => kv.Value > 0).OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per(kv.Value, n)}"));

    // ---------------------------------------------------------------------------------
    // 表
    // ---------------------------------------------------------------------------------
    internal static void MudoHead()
    {
        Console.WriteLine("被弾 ＝ 数えた被弾（刻み ／ 自傷を除く・うち味方の刃）。呪いの怒り ＝ M1 で浴びた呪いから溜まった怒り（割合 ＝ 呪いの怒り ÷（被弾 ＋ 呪いの怒り））。1発 ＝ 暴発の1発の攻撃力の平均。手番の外 ＝ 暴発（割り込み）の与ダメ。軛 ＝ 暴発の発が上限 25 に切られた発数 ／ 量。");
        Console.WriteLine();
        Console.WriteLine("| 列 | 勝率・T | 被弾（味方の刃） | 呪いの怒り（割合） | 暴発 回 ／ 発 ／ 1回の発数 ／ 最大 | 1発 | 与ダメ 手番 ／ 手番の外 | 軛 発 ／ 量 | 受けた弱体（書き手） | ワタ: ムドから拾った ／ ムドへ回した ／ ウツへ回した | 倒れた（割合・T） |");
        Console.WriteLine("|---|---|---|---|---|--:|---|---|---|---|---|");
    }
    internal static void MudoRow(string label, Fx a) =>
        Console.WriteLine($"| {label} | {F1(a.Win)}・{a.WinTs}T | {Per(a.Fuel, a.MudoN)}（{Per(a.FuelAlly, a.MudoN)}） | {Per(a.CurseRage, a.MudoN)}（{Pct(a.CurseRage, a.CurseRage + a.Fuel)}%） | "
            + $"{Per2(a.Fires, a.MudoN)} ／ {Per(a.Swings, a.MudoN)} ／ {Per2(a.Swings, a.Fires)} ／ {a.Peak} | {Per(a.SwingAtk, a.Swings)} | {Per(a.InTurn, a.MudoN)} ／ {Per(a.OffTurn, a.MudoN)} | {Per2(a.YokeCut, a.MudoN)} ／ {Per(a.YokeLost, a.MudoN)} | "
            + $"{Per(a.Dulled, a.MudoN)}（{Kv(a.DullFrom, a.MudoN)}） | {Per(a.PickedMudo, a.N)} ／ {Per(a.FedMudo, a.N)} ／ {Per(a.FedUtsu, a.N)} | {Pct(a.MudoDied, a.MudoN)}%・{Per(a.MudoDeathT, a.MudoDied)}T |");

    internal static void PrintTraj(string title, Fx a)
    {
        Console.WriteLine($"#### {title}（勝率 {F1(a.Win)}%・負けた戦で最も減ったボスの HP 平均 {Per(a.LostBossLow, a.Lost)} ／ 最小 {(a.Lost == 0 ? "—" : a.LostBossLowMin.ToString())}）");
        Console.WriteLine();
        Console.WriteLine("ターン t まで戦が続いた（味方の誰かがターン頭の写しを持つ）戦の平均。与ダメはボスへの分だけ。差 ＝ 味方の与ダメ − ボスの回復。");
        Console.WriteLine();
        Console.WriteLine("| T | 続いた戦 | 味方の与ダメ | うちウツ | うちムド | ほか | ボスの回復 | 差 |");
        Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int t = 1; t <= TrajT; t++)
        {
            long c = a.TCnt[t];
            if (c == 0) continue;
            Console.WriteLine($"| {t} | {c} | {Per(a.TAll[t], c)} | {Per(a.TUtsu[t], c)} | {Per(a.TMudo[t], c)} | {Per(a.TAll[t] - a.TUtsu[t] - a.TMudo[t], c)} | {Per(a.THeal[t], c)} | {Sg((double)(a.TAll[t] - a.THeal[t]) / c)} |");
        }
        long n = 0, al = 0, ut = 0, mu = 0, he = 0;
        for (int t = 1; t <= TrajT; t++) if (a.TAll[t] > 0) { n += a.TCnt[t]; al += a.TAll[t]; ut += a.TUtsu[t]; mu += a.TMudo[t]; he += a.THeal[t]; }
        Console.WriteLine();
        Console.WriteLine($"味方が与ダメを出したターンの平均: **{Per(al, n)} ／T**（ウツ {Per(ut, n)} ／ ムド {Per(mu, n)} ／ ほか {Per(al - ut - mu, n)}）・ボスの回復 {Per(he, n)} ／T・T10 までに倒すのに要る 1,380 ／T の {(n == 0 ? "—" : F1(100.0 * al / n / 1380))}%。");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §3 Phase 0
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        I.W[] ws = { I.Boss, I.Guard, I.Bat };
        var res = ws.ToDictionary(w => w.Name, w => Measure(Play, w));
        Console.WriteLine($"# {Tag} §3 Phase 0 —— 「{PlayRow}」（{Seats(Play)}・規定・段0 のウツ）× ボス ／ 近衛 ／ 大隊・seed 0..{Seeds - 1}・1戦あたり");
        Console.WriteLine();
        Console.WriteLine("## 1〜2. ムドの働きと、ワタがムドから拾った量");
        Console.WriteLine();
        MudoHead();
        foreach (var w in ws) MudoRow(w.Name, res[w.Name]);
        Console.WriteLine();
        Console.WriteLine("## 3. ボスの戦の1ターンの与ダメ（ウツ ／ ムド ／ ほか）と回復");
        Console.WriteLine();
        PrintTraj("規定 × ボス", res[I.Boss.Name]);
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
