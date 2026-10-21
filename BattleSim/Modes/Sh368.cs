using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using S366 = Sh366Diag;
using S367 = Sh367Diag;

// =====================================================================================
// sh368 —— 第368期【軽い期】段0: GV ／ H1 を規定に ／ 本体: 毒特化の耐久（R 毒に強い ／ RC ＋ 毒の衣 ／ RCS ＋ 吸い分け）。
// 指示書は design/PHASE368_POISON_WARD_SPEC.md ／ 報告は design/PHASE368_POISON_WARD.md。本体の版は規定にしない。
//
//     dotnet run --project BattleSim -c Release 0 sh368 boards             # §3-3: 6 台 × 版（規定 ／ R ／ RC ／ RCS）× 6 波（seed 0..99）・誰が何Tに倒れたか・漏れの行き先・スィドの層・反転・勇者の層・壊れの兆し
//     dotnet run --project BattleSim -c Release 0 sh368 compare <R|RC|RCS>  # `compare` 64 行 × 版（スィド ／ グザ ／ ヴィオのいる行だけを版で回す・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh368 check              # 自己検査
//
// 版の駒は `UnitCatalog.SidR` ／ `SidRC` ／ `GuzaR` ／ `VioR` ／ `VioRCS`（`All` に入れない）。蝕みなしの勇者は第366期の器具の中の対照（`Sh366Diag.BossNoTax`）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p368/predict.md`）。
// =====================================================================================
static partial class Sh368Diag
{
    internal const string PredictFile = ".tmp/p368/predict.md";
    const string Tag = "第368期";
    const int Seeds = 100, CompareSeeds = 200, SidT = 10, HeroT = 6;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "boards" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh368: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "boards": Boards(); return;
            case "compare": CompareVer(args.Length > 3 ? args[3] : "R"); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh368: モードは boards / compare <R|RC|RCS> / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Nm(string id) => Short(UnitCatalog.Everyone.First(d => d.Id == id));
    static string Sg(double x) => x.ToString("+0.0;-0.0;±0.0");
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    // ---------------------------------------------------------------------------------
    // 版と台
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, UnitDef? Sid, UnitDef? Guza, UnitDef? Vio);
    internal static readonly Ver[] Vers =
    {
        new("規定", null, null, null),
        new("R 毒に強い", UnitCatalog.SidR, UnitCatalog.GuzaR, UnitCatalog.VioR),
        new("RC ＋ 毒の衣", UnitCatalog.SidRC, UnitCatalog.GuzaR, UnitCatalog.VioR),
        new("RCS ＋ 吸い分け", UnitCatalog.SidRC, UnitCatalog.GuzaR, UnitCatalog.VioRCS),
    };
    internal static Formation Apply(Formation f, Ver v)
    {
        var g = f;
        if (v.Sid is not null) g = S316.SwapId(g, "sid", v.Sid);
        if (v.Guza is not null) g = S316.SwapId(g, "guza", v.Guza);
        if (v.Vio is not null) g = S316.SwapId(g, "vio", v.Vio);
        return g;
    }

    /// <summary>§3-3 の6台（駒はいまの規定 ＝ 段0 の後）。反転の確かめの2台はベニのいる行。</summary>
    internal static (string Name, Formation F)[] BoardList() => new (string, Formation)[]
    {
        ("試遊・毒", Presets.Versions.First(r => r.Name == "試遊・毒").F),
        ("毒特化・集団", Row(S367.BombRow)),
        ("澱み喰い (グザ×ヴィオ)", Row("澱み喰い (グザ×ヴィオ)")),
        ("毒 (グザ×ミオ×ラウ)", Row("毒 (グザ×ミオ×ラウ)")),
        ("反転 毒+耐久 (ベニ×トウ)", Row("毒+耐久 (ベニ×トウ)")),
        ("反転 毒+ベニ+ラウ", Row("毒+ベニ+ラウ")),
    };
    internal static readonly I.W[] Waves = S367.PWaves;   // ボス ／ 近衛 ／ 大隊 ／ 本編 第2 ／ 5波 ／ 蝕みなしの勇者

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Agg
    {
        public long N, Wins, WinT, Win1, Draw, Fell;
        public readonly Dictionary<string, (long N, long TSum)> FellBy = new();
        /// <summary>グザの漏れ（`CounterProbe`・グザが味方に書いた層・滲み込み）の行き先: スィド ／ ヴィオ ／ グザ自身 ／ ほかの味方。</summary>
        public long LeakSid, LeakVio, LeakSelf, LeakOther;
        /// <summary>ヴィオが吸った層: 衣の持ち主（スィド）から ／ ほかの味方から。</summary>
        public long DrawnSid, DrawnOther, DrawnSidAll;
        /// <summary>スィドの層（ターンの頭・生きていたターン）。</summary>
        public readonly long[] SLayer = new long[SidT + 1], SAlive = new long[SidT + 1];
        public long CloakSaved, CloakSelf;
        /// <summary>毒に強いで消えた刻み（駒ごと・名目）。</summary>
        public readonly Dictionary<string, long> Erased = new();
        /// <summary>ベニの反転で回復になった毒の刻み（名目）: 毒の3枚 ／ ほかの味方。</summary>
        public long InvPoisonTrio, InvOther;
        public readonly long[] HLayer = new long[HeroT + 1], HAlive = new long[HeroT + 1];
        public long VioDouble, VioPour;
        public void Add(Agg o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Win1 += o.Win1; Draw += o.Draw; Fell += o.Fell;
            foreach (var (k, v) in o.FellBy) { var c = FellBy.GetValueOrDefault(k); FellBy[k] = (c.N + v.N, c.TSum + v.TSum); }
            LeakSid += o.LeakSid; LeakVio += o.LeakVio; LeakSelf += o.LeakSelf; LeakOther += o.LeakOther;
            DrawnSid += o.DrawnSid; DrawnOther += o.DrawnOther; DrawnSidAll += o.DrawnSidAll;
            for (int t = 0; t <= SidT; t++) { SLayer[t] += o.SLayer[t]; SAlive[t] += o.SAlive[t]; }
            CloakSaved += o.CloakSaved; CloakSelf += o.CloakSelf;
            foreach (var (k, v) in o.Erased) Erased[k] = Erased.GetValueOrDefault(k) + v;
            InvPoisonTrio += o.InvPoisonTrio; InvOther += o.InvOther;
            for (int t = 0; t <= HeroT; t++) { HLayer[t] += o.HLayer[t]; HAlive[t] += o.HAlive[t]; }
            VioDouble += o.VioDouble; VioPour += o.VioPour;
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : F1((double)WinT / Wins);
        public string Cell => $"{F1(Win)}・{WinTs}T・倒 {Per(Fell, N)}";
        public string FellCell => FellBy.Count == 0 ? "—" : string.Join("・", FellBy.OrderByDescending(kv => kv.Value.N).Select(kv => $"{Nm(kv.Key)} {Pct(kv.Value.N, N)}%（T{F1((double)kv.Value.TSum / kv.Value.N)}）"));
    }

    static readonly HashSet<string> Trio = new() { "sid", "guza", "vio" };

    internal static Agg One(Formation f, I.W w, int seed)
    {
        var a = new Agg { N = 1 };
        CounterProbe probe = (trait, owner, target, key, delta) =>
        {
            if (delta <= 0 || key != UnitTally.CarryKeys[UnitTally.CarryPoison]) return;
            if (owner.Def.Id != "guza" || owner.TeamId != BattleContext.PlayerTeam || target.TeamId != owner.TeamId) return;
            lock (a)
            {
                if (ReferenceEquals(owner, target)) a.LeakSelf += delta;
                else if (target.Def.Id == "sid") a.LeakSid += delta;
                else if (target.Def.Id == "vio") a.LeakVio += delta;
                else a.LeakOther += delta;
            }
        };
        var (r, p, e) = S367.Raw(f, w, seed, verbose: true, probe: probe);
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; if (r.Turns <= 1) a.Win1 = 1; }
        else if (r.PlayerSurvivors > 0) a.Draw = 1;
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { a.Fell++; var c = a.FellBy.GetValueOrDefault(u.Def.Id); a.FellBy[u.Def.Id] = (c.N + 1, c.TSum + u.LastDeathTurn); }
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!p.Any(u => u.Def.Id == id)) continue;
            if (t.Pw368ProofErased > 0) a.Erased[id] = a.Erased.GetValueOrDefault(id) + t.Pw368ProofErased;
            if (id == "vio") { a.DrawnSid += t.Pw368DrawnCloak; a.DrawnOther += t.Pw368DrawnOther; a.VioDouble += t.Cat367Doubled; a.VioPour += t.Cat367Poured; }
            if (id == "sid") { a.CloakSaved += t.Pw368CloakSaved; a.CloakSelf += t.Pw368CloakSelf; }
        }
        var sid = p.FirstOrDefault(u => u.Def.Id == "sid");
        var vio = p.FirstOrDefault(u => u.Def.Id == "vio");
        var mine = p.ToDictionary(u => u.InstanceId);
        var hero = w.Boss ? e.FirstOrDefault(u => u.HasTrait(TraitId.BossMend40)) : null;
        foreach (var x in r.Events)
        {
            switch (x.Kind)
            {
                case BattleEventKind.StatusSnapshot when x.Text == "毒" && sid is not null && x.TargetId == sid.InstanceId && x.Turn is >= 1 and <= SidT:
                    a.SLayer[x.Turn] += x.Amount;
                    break;
                case BattleEventKind.StatusSnapshot when x.Text == "毒" && hero is not null && x.TargetId == hero.InstanceId && x.Turn is >= 1 and <= HeroT:
                    a.HLayer[x.Turn] += x.Amount;
                    break;
                case BattleEventKind.Status when x.Text == "毒" && x.InverterId is not null && x.TargetId is int ti && mine.TryGetValue(ti, out var who):
                    if (Trio.Contains(who.Def.Id)) a.InvPoisonTrio += x.Amount; else a.InvOther += x.Amount;
                    break;
                case BattleEventKind.StatusDrain when vio is not null && x.ActorId == vio.InstanceId && sid is not null && x.TargetId == sid.InstanceId:
                    a.DrawnSidAll += x.Amount;
                    break;
            }
        }
        if (sid is not null) { int last = sid.LastDeathTurn > 0 ? sid.LastDeathTurn : r.Turns; for (int t = 1; t <= Math.Min(SidT, last); t++) a.SAlive[t] = 1; }
        if (hero is not null) { int last = hero.LastDeathTurn > 0 ? hero.LastDeathTurn : r.Turns; for (int t = 1; t <= Math.Min(HeroT, last); t++) a.HAlive[t] = 1; }
        return a;
    }

    internal static Agg Measure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Agg[n];
        Parallel.For(0, n, i => parts[i] = One(f, w, i));
        var all = new Agg();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    // ---------------------------------------------------------------------------------
    // §3-3 6台 × 版 × 波
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        Console.WriteLine($"# {Tag} §3-3 毒特化の耐久 —— 6台 × 版 × 波（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。版はスィド ／ グザ ／ ヴィオだけを替える（いない駒は替えない）。駒は段0 の後の規定（グザ G ／ ヴィオ V ／ ヒサ H1）。");
        Console.WriteLine();
        var grid = new Agg[boards.Length, Vers.Length, Waves.Length];
        Parallel.For(0, boards.Length * Vers.Length * Waves.Length, k =>
        {
            int bi = k / (Vers.Length * Waves.Length), vi = k / Waves.Length % Vers.Length, wi = k % Waves.Length;
            grid[bi, vi, wi] = Measure(Apply(boards[bi].F, Vers[vi]), Waves[wi]);
        });
        int bw = 0, nw = Waves.Length - 1;

        Console.WriteLine("## 1. 台 × 版 × 波（勝率・倒しT（勝った戦）・倒れた味方（1戦あたり））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < Vers.Length; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {Vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, Waves.Length).Select(wi => grid[bi, vi, wi].Cell))} |");
        Console.WriteLine();

        Console.WriteLine("## 2. 誰が何Tに倒れたか（駒 倒れた戦の割合%（倒れたターンの平均））");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < Vers.Length; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {Vers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, Waves.Length).Select(wi => grid[bi, vi, wi].FellCell))} |");
        Console.WriteLine();

        Console.WriteLine("## 3. グザの漏れの行き先 ／ ヴィオが吸った層 ／ スィドの層と衣（1戦あたり・スィド ／ グザ ／ ヴィオのそろう台）");
        Console.WriteLine();
        Console.WriteLine("漏れはグザが味方に書いた層（`CounterProbe`・滲み込み）を受けた駒で分けた。吸った層はヴィオの澱み喰いが吸い上げた層を出どころ（スィド ／ ほかの味方）で分けた（スィドからの分は漏れだけでなく、スィドが殴られて自分に付けた +2 も含む）。");
        Console.WriteLine("スィドの層はターンの頭（刻みの後・吸い上げの前）の、スィドが生きていたターンの平均。衣で減った傷 ＝ 毒の衣の軽減の合計。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 漏れ スィドへ ／ ヴィオへ ／ グザ自身 ／ ほか | ヴィオが吸った スィドから ／ ほかから | 衣が自分に付けた | 衣で減った傷 | スィドの層 T1〜T10 |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|---|");
        for (int bi = 0; bi < boards.Length; bi++)
        {
            if (!(Has(boards[bi].F, "sid") && Has(boards[bi].F, "guza") && Has(boards[bi].F, "vio"))) continue;
            for (int vi = 0; vi < Vers.Length; vi++)
                foreach (int wi in new[] { bw, 1, 4, nw })
                {
                    var a = grid[bi, vi, wi];
                    Console.WriteLine($"| {boards[bi].Name} | {Vers[vi].Name} | {Waves[wi].Name} | {Per(a.LeakSid, a.N)} ／ {Per(a.LeakVio, a.N)} ／ {Per(a.LeakSelf, a.N)} ／ {Per(a.LeakOther, a.N)} | {Per(a.DrawnSidAll, a.N)} ／ {Per(a.DrawnOther, a.N)} | {Per(a.CloakSelf, a.N)} | {Per(a.CloakSaved, a.N)} | {string.Join(" ", Enumerable.Range(1, SidT).Select(t => a.SAlive[t] == 0 ? "—" : Per(a.SLayer[t], a.SAlive[t])))} |");
                }
        }
        Console.WriteLine();

        Console.WriteLine("## 4. 毒に強いで消えた刻み（駒ごと・1戦・名目）と、ベニの反転で回復になった毒の刻み（毒の3枚 ／ ほかの味方）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 消えた刻み | 反転で回復 毒の3枚 ／ ほか |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < Vers.Length; vi++)
                foreach (int wi in new[] { bw, 1, 4 })
                {
                    var a = grid[bi, vi, wi];
                    string er = a.Erased.Count == 0 ? "—" : string.Join("・", a.Erased.OrderByDescending(kv => kv.Value).Select(kv => $"{Nm(kv.Key)} {Per(kv.Value, a.N)}"));
                    Console.WriteLine($"| {boards[bi].Name} | {Vers[vi].Name} | {Waves[wi].Name} | {er} | {Per(a.InvPoisonTrio, a.N)} ／ {Per(a.InvOther, a.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 5. 勇者の毒の層（ターンの頭・勇者が生きていたターンの平均）と触媒の倍 ／ 注ぎ（1戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | " + string.Join(" | ", Enumerable.Range(2, HeroT - 1).Select(t => $"T{t}")) + " | 倍 ／ 注ぎ |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Range(2, HeroT - 1).Select(_ => "--:|")) + "---|");
        for (int bi = 0; bi < boards.Length; bi++)
        {
            if (!Has(boards[bi].F, "vio")) continue;
            for (int vi = 0; vi < Vers.Length; vi++)
                foreach (int wi in new[] { bw, nw })
                {
                    var a = grid[bi, vi, wi];
                    Console.WriteLine($"| {boards[bi].Name} | {Vers[vi].Name} | {Waves[wi].Name} | {string.Join(" | ", Enumerable.Range(2, HeroT - 1).Select(t => a.HAlive[t] == 0 ? "—" : Per(a.HLayer[t], a.HAlive[t])))} | {Per(a.VioDouble, a.N)} ／ {Per(a.VioPour, a.N)} |");
                }
        }
        Console.WriteLine();

        var bad = new List<string>();
        for (int bi = 0; bi < boards.Length; bi++)
            for (int wi = 0; wi < Waves.Length; wi++)
            {
                var b = grid[bi, 0, wi];
                for (int vi = 1; vi < Vers.Length; vi++)
                {
                    var a = grid[bi, vi, wi]; var w = Waves[wi];
                    if ((w == I.Guard || w == I.Bat) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 1T 決着 {Pct(a.Win1, a.N)}%");
                    if (w.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4 && !(b.Wins > 0 && (double)b.WinT / b.Wins < 4)) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 倒しT {a.WinTs}（規定 {b.WinTs}）");
                    if (100.0 * (a.Draw - b.Draw) / a.N >= 5) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 30T の打ち切り {Pct(b.Draw, b.N)} → {Pct(a.Draw, a.N)}%");
                    if (b.Win - a.Win >= 15) bad.Add($"{boards[bi].Name} × {Vers[vi].Name} × {w.Name}: 勝率 {F1(b.Win)} → {F1(a.Win)}（参考・落ちた側）");
                }
            }
        Console.WriteLine("## 6. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満（規定で既に 4T 未満は除く）／ 30T の打ち切りが 5pt 以上増える ／ 参考: 勝率が規定から 15pt 以上落ちる）");
        Console.WriteLine();
        Console.WriteLine(bad.Count == 0 ? "なし。" : string.Join(Environment.NewLine, bad.Select(x => "- " + x)));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` × 版
    // ---------------------------------------------------------------------------------
    static double[,] CGrid((string Name, Formation F)[] rows, Func<Formation, Formation> map, Func<int, bool> only)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            if (!only(ri)) { g[ri, wi] = double.NaN; return; }
            var f = map(rows[ri].F);
            int wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        return g;
    }

    static void CompareVer(string ver)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var v = ver switch { "R" => Vers[1], "RC" => Vers[2], "RCS" => Vers[3], _ => throw new ArgumentException("sh368 compare の版は R / RC / RCS") };
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        Func<int, bool> only = ri => Trio.Any(id => Has(rows[ri].F, id));
        var b = CGrid(rows, f => f, _ => true);
        var g0 = CGrid(rows, f => Apply(f, v), only);
        var g = new double[rows.Length, nw];
        int touched = 0;
        for (int r = 0; r < rows.Length; r++) { if (only(r)) touched++; for (int w = 0; w < nw; w++) g[r, w] = only(r) ? g0[r, w] : b[r, w]; }
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] x) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => x[r, w]))));
        Console.WriteLine($"# {Tag} `compare` {rows.Length} 行 × 版 {v.Name}（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"版で回した行 {touched}（スィド ／ グザ ／ ヴィオのいる行・いない行は規定と同じ台 ＝ 同じ結果）。");
        Console.WriteLine();
        int upR = 0, upC = 0, dnR = 0, dnC = 0;
        var lines = new List<string>();
        for (int r = 0; r < rows.Length; r++)
        {
            bool u = false, d = false;
            for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
            if (u) upR++; if (d) dnR++;
            if (u || d) lines.Add($"{rows[r].Name}: " + string.Join("・", Enumerable.Range(1, nw - 1).Where(w => g[r, w] != b[r, w]).Select(w => $"第{w + 1}波 {F1(b[r, w])} → {F1(g[r, w])}")));
        }
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|--:|---|");
        Console.WriteLine($"| 規定 | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} |");
        Console.WriteLine($"| {v.Name} | {upR} ／ {upC} | {dnR} ／ {dnC} | {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} |");
        Console.WriteLine();
        foreach (var l in lines) Console.WriteLine("- " + l);
        for (int r = 0; r < rows.Length; r++)
        {
            if (!Enumerable.Range(1, nw - 1).Any(w => b[r, w] - g[r, w] >= 10.0)) continue;
            var parts = new List<string>();
            foreach (var id in rows[r].F.Occupied().Select(o => o.Def.Id).Distinct())
            {
                var oth = Enumerable.Range(0, rows.Length).Where(o => o != r && rows[o].F.Occupied().Any(x => x.Def.Id == id)).ToList();
                string nm = Short(rows[r].F.Occupied().First(o => o.Def.Id == id).Def);
                if (oth.Count == 0) { parts.Add($"{nm} 他の行 0（分解が成立しない）"); continue; }
                double dd = oth.Average(o => Enumerable.Range(1, nw - 1).Average(w => g[o, w] - b[o, w]));
                parts.Add($"{nm} {Sg(dd)}（{oth.Count} 行）");
            }
            Console.WriteLine($"- (G2) {rows[r].Name}: {string.Join("・", parts)}");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
