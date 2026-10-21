using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using S366 = Sh366Diag;

// =====================================================================================
// sh367 —— 第367期【軽い期】本体1: 毒軸のテコ入れ①（グザの集まる瘴気 G ／ ヴィオの触媒 V ／ GV）／ 本体2: 波の規則「軛は刻みを切らない」（Y）／ 本体3: ヒサ Y1（H1）。
// 指示書は design/PHASE367_POISON_LEVERS_SPEC.md ／ 報告は design/PHASE367_POISON_LEVERS.md。規定は1つも変えない。
//
//     dotnet run --project BattleSim -c Release 0 sh367 pick               # §3-2: 毒特化・勇者の守り（ゴルム ／ ドルガ ／ ササ）を GV の勇者（seed 0..19）で選ぶ
//     dotnet run --project BattleSim -c Release 0 sh367 poison             # §3-3: 毒の4台 × 版（規定 ／ G ／ V ／ GV）× 波（ボス ／ 近衛 ／ 大隊 ／ 本編 第2 ／ 5波 ／ 蝕みなしの勇者）・seed 0..99
//     dotnet run --project BattleSim -c Release 0 sh367 yoke               # §4-2: 第4波 ／ 強い条件の第4波 × 規定 ／ Y（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh367 hisa               # §5-2: 標軸の5台 × 規定 ／ H1（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh367 compare <G|V|GV|Y|H1>   # `compare` 64 行 × 版（seed 0..199・基準は規定）
//     dotnet run --project BattleSim -c Release 0 sh367 check              # 自己検査
//
// 版の駒は `UnitCatalog.GuzaG` ／ `VioV` ／ `HisaY1`（`All` に入れない）、Y は `YokeRule { SparesTicks = true }` を器具が渡す。
// 蝕みなしの勇者は第366期の器具の中の対照（`Sh366Diag.BossNoTax`）をそのまま使う。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p367/predict.md`）。
// =====================================================================================
static partial class Sh367Diag
{
    internal const string PredictFile = ".tmp/p367/predict.md";
    const string Tag = "第367期";
    const int Seeds = 100, CompareSeeds = 200, PickSeeds = 20, HeroT = 8;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "pick" or "poison" or "yoke" or "hisa" or "compare" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh367: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "pick": Pick(); return;
            case "poison": Poison(); return;
            case "yoke": YokeBoards(); return;
            case "hisa": HisaBoards(); return;
            case "compare": CompareVer(args.Length > 3 ? args[3] : "G"); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh367: モードは pick / poison / yoke / hisa / compare <G|V|GV|Y|H1> / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Sg(double x) => x.ToString("+0.0;-0.0;±0.0");
    // 第368期: 台は第367期までの規定（`Common.Pin367`・グザ ／ ヴィオ ／ ヒサ）に固定する（この期の数字を保つ）。
    static Formation Row(string n) => Pin367(CompareBuilds().First(r => r.Name == n).F);
    static Formation Hero367() => Presets.PoisonHero367(UnitCatalog.GuzaH367, UnitCatalog.VioH367, UnitCatalog.SidH371);   // 第372期: スィドも第371期までの規定に
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    // ---------------------------------------------------------------------------------
    // 版と台
    // ---------------------------------------------------------------------------------
    internal sealed record PVer(string Name, UnitDef? Guza, UnitDef? Vio);
    internal static readonly PVer[] PVers =
    {
        new("規定", null, null),
        new("G 集まる瘴気", UnitCatalog.GuzaG, null),
        new("V 触媒", null, UnitCatalog.VioV),
        new("GV", UnitCatalog.GuzaG, UnitCatalog.VioV),
    };
    internal static Formation ApplyP(Formation f, PVer v)
    {
        var g = f;
        if (v.Guza is not null) g = S316.SwapId(g, "guza", v.Guza);
        if (v.Vio is not null) g = S316.SwapId(g, "vio", v.Vio);
        return g;
    }
    static Formation H1(Formation f) => S316.SwapId(f, "hisa", UnitCatalog.HisaY1);
    internal static readonly YokeRule YokeY = YokeRule.Default with { SparesTicks = true };

    internal const string BombRow = "毒爆弾 (ラウ×ヴィオ)";
    /// <summary>§3-2 の毒特化・勇者の守りの候補（ラウの席 ＝ 後3 に置く）。</summary>
    internal static readonly UnitDef[] Guards = { UnitCatalog.Golm, UnitCatalog.Dolga, UnitCatalog.Sasa };
    internal static Formation HeroBoardOf(UnitDef guard) => S316.SwapId(Row(BombRow), "rau", guard);

    /// <summary>§3-2 の4台（駒はいまの規定）。毒特化・勇者は `pick` で選んだ守り（`Presets.PoisonHero367`）。</summary>
    internal static (string Name, Formation F)[] PBoards() => new (string, Formation)[]
    {
        ("毒特化・集団", Row(BombRow)),
        ("毒特化・勇者", Hero367()),
        ("澱み喰い (グザ×ヴィオ)", Row("澱み喰い (グザ×ヴィオ)")),
        ("毒 (グザ×ミオ×ラウ)", Row("毒 (グザ×ミオ×ラウ)")),
    };
    internal static readonly I.W[] PWaves = { I.Boss, I.Guard, I.Bat, I.M2, I.M5, S366.BossNoTax };

    /// <summary>1戦（`Inv348Diag.Raw` と同じ規則に、版の軛を足せる口）。</summary>
    internal static (BattleResult R, List<UnitState> P, List<UnitState> E) Raw(Formation f, I.W w, int seed, YokeRule? yoke = null, bool verbose = false, CounterProbe? probe = null)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: verbose, yoke: yoke, boss: new BossRule(true) { Scale = w.Scale }, harm: new HarmRule(true), probe: probe);
        return (r, p, e);
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数（勝率・倒しT・倒れた味方・毒の流れ・軛と刻み・叫び）
    // ---------------------------------------------------------------------------------
    internal sealed class Agg
    {
        public long N, Wins, WinT, Win1, Draw, Fell, Turns;
        /// <summary>勇者（ボスの波だけ）: ターンの頭の毒の層 ／ 生きていたターン（添字 1..HeroT）・勇者への与ダメ ／ うち毒の刻み（名目）。</summary>
        public readonly long[] HLayer = new long[HeroT + 1], HAlive = new long[HeroT + 1];
        public long HTick, HTaken;
        /// <summary>敵へ書いた毒の層（`CounterProbe`・書いた駒の Def.Id・味方の駒だけ）。</summary>
        public readonly Dictionary<string, long> Wrote = new();
        public long VioDouble, VioPour, VioBellyOnly, CatActs, CatDry, GuzaLeak, GuzaFoe, VioDrawn, TickFoe;
        /// <summary>軛と刻み（[0] 毒 ／ [1] 燃焼・敵側 ＝ 0..1 ／ 味方側 ＝ 2..3）: 切られた回数 ／ 量 ・通った回数 ／ 上限を超えた量。</summary>
        public readonly long[] YCut = new long[4], YLost = new long[4], YSpared = new long[4], YOver = new long[4];
        public long Rallies, RallyHealed, FeatherQuiet;
        /// <summary>倒れた味方（Def.Id）: 倒れた回数 ／ 倒れたターンの和（ユーザーの依頼「誰が何Tに倒れたか」）。</summary>
        public readonly Dictionary<string, (long N, long TSum)> FellBy = new();
        public void Add(Agg o)
        {
            foreach (var (k, v) in o.FellBy) { var c = FellBy.GetValueOrDefault(k); FellBy[k] = (c.N + v.N, c.TSum + v.TSum); }
            N += o.N; Wins += o.Wins; WinT += o.WinT; Win1 += o.Win1; Draw += o.Draw; Fell += o.Fell; Turns += o.Turns;
            for (int t = 0; t <= HeroT; t++) { HLayer[t] += o.HLayer[t]; HAlive[t] += o.HAlive[t]; }
            HTick += o.HTick; HTaken += o.HTaken;
            foreach (var (k, v) in o.Wrote) Wrote[k] = Wrote.GetValueOrDefault(k) + v;
            VioDouble += o.VioDouble; VioPour += o.VioPour; VioBellyOnly += o.VioBellyOnly; CatActs += o.CatActs; CatDry += o.CatDry; GuzaLeak += o.GuzaLeak; GuzaFoe += o.GuzaFoe; VioDrawn += o.VioDrawn; TickFoe += o.TickFoe;
            for (int i = 0; i < 4; i++) { YCut[i] += o.YCut[i]; YLost[i] += o.YLost[i]; YSpared[i] += o.YSpared[i]; YOver[i] += o.YOver[i]; }
            Rallies += o.Rallies; RallyHealed += o.RallyHealed; FeatherQuiet += o.FeatherQuiet;
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : F1((double)WinT / Wins);
        public string Cell => $"{F1(Win)}・{WinTs}T・倒 {Per(Fell, N)}";
        /// <summary>誰が何Tに倒れたか: 「駒 倒れた割合%（平均 T）」を倒れた回数の多い順に（倒れた駒が無ければ「—」）。</summary>
        public string FellCell => FellBy.Count == 0 ? "—" : string.Join("・", FellBy.OrderByDescending(kv => kv.Value.N).Select(kv => $"{Nm(kv.Key)} {Pct(kv.Value.N, N)}%（T{F1((double)kv.Value.TSum / kv.Value.N)}）"));
    }

    /// <summary>1戦。<paramref name="flow"/> が真なら台本を積んで毒の流れ（勇者の層 ／ 書き手 ／ 刻みの割合）も数える。</summary>
    internal static Agg One(Formation f, I.W w, int seed, bool flow = false, YokeRule? yoke = null)
    {
        var a = new Agg { N = 1 };
        CounterProbe? probe = null;
        if (flow)
            probe = (trait, owner, target, key, delta) =>
            {
                if (delta <= 0 || key != UnitTally.CarryKeys[UnitTally.CarryPoison]) return;
                if (owner.TeamId != BattleContext.PlayerTeam || target.TeamId == BattleContext.PlayerTeam) return;
                lock (a.Wrote) a.Wrote[owner.Def.Id] = a.Wrote.GetValueOrDefault(owner.Def.Id) + delta;
            };
        var (r, p, e) = Raw(f, w, seed, yoke, verbose: flow, probe: probe);
        a.Turns = r.Turns;
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; if (r.Turns <= 1) a.Win1 = 1; }
        else if (r.PlayerSurvivors > 0) a.Draw = 1;   // 30T の打ち切り（味方が残ったまま決着しない）
        a.Fell = p.Count(u => u.LastDeathTurn > 0);
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { var c = a.FellBy.GetValueOrDefault(u.Def.Id); a.FellBy[u.Def.Id] = (c.N + 1, c.TSum + u.LastDeathTurn); }
        var mineIds = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit)
        {
            int side = mineIds.Contains(id) ? 2 : 0;
            for (int k = 0; k < 2; k++)
            {
                if (t.Yoke367TickCut is { } c) a.YCut[side + k] += c[k];
                if (t.Yoke367TickLost is { } l) a.YLost[side + k] += l[k];
                if (t.Yoke367TickSpared is { } s) a.YSpared[side + k] += s[k];
                if (t.Yoke367TickOver is { } o) a.YOver[side + k] += o[k];
            }
            if (!mineIds.Contains(id)) continue;
            if (id == "vio") { a.VioDouble += t.Cat367Doubled; a.VioPour += t.Cat367Poured; a.VioBellyOnly += t.Cat367BellyOnly; a.CatActs += t.Cat367Acts; a.CatDry += t.Cat367Dry; a.VioDrawn += t.SpitStored; }
            if (id == "guza") { a.GuzaLeak += t.Mia367ToAlly; a.GuzaFoe += t.Mia367ToFoe; }
            if (id == "hisa") { a.Rallies += t.RallyFires; a.RallyHealed += t.RallyHealed; a.FeatherQuiet += t.Rally367FeatherQuiet; }
        }
        if (!flow) return a;
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        var hero = w.Boss ? e.FirstOrDefault(u => u.HasTrait(TraitId.BossMend40)) : null;
        foreach (var x in r.Events)
        {
            bool onFoe = x.TargetId is int ti && !mine.Contains(ti);
            switch (x.Kind)
            {
                case BattleEventKind.Status when x.Text == "毒" && x.InverterId is null:
                    if (onFoe) a.TickFoe += x.Amount;
                    if (hero is not null && x.TargetId == hero.InstanceId) a.HTick += x.Amount;
                    break;
                case BattleEventKind.Damage when hero is not null && x.TargetId == hero.InstanceId:
                    a.HTaken += x.Amount;
                    break;
                case BattleEventKind.StatusSnapshot when hero is not null && x.Text == "毒" && x.TargetId == hero.InstanceId && x.Turn is >= 1 and <= HeroT:
                    a.HLayer[x.Turn] += x.Amount;
                    break;
            }
        }
        if (hero is not null)
        {
            int last = hero.LastDeathTurn > 0 ? hero.LastDeathTurn : r.Turns;
            for (int t = 1; t <= Math.Min(HeroT, last); t++) a.HAlive[t] = 1;
        }
        return a;
    }

    internal static Agg Measure(Formation f, I.W w, int n = Seeds, bool flow = false, YokeRule? yoke = null)
    {
        var parts = new Agg[n];
        Parallel.For(0, n, i => parts[i] = One(f, w, i, flow, yoke));
        var all = new Agg();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    /// <summary>誰が何Tに倒れたかの表（台 × 版 の行・波 の列）。</summary>
    static void FellTable(string[] boards, string[] vers, string[] waves, Func<int, int, int, Agg> at)
    {
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", waves) + " |");
        Console.WriteLine("|---|---|" + string.Concat(waves.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < vers.Length; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi] : "")} | {vers[vi]} | {string.Join(" | ", Enumerable.Range(0, waves.Length).Select(wi => at(bi, vi, wi).FellCell))} |");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §3-2 毒特化・勇者の守りを選ぶ
    // ---------------------------------------------------------------------------------
    /// <summary>GV の勇者（seed 0..19）で最も勝つ守り（同じなら倒しT の短い方・それも同じなら候補の並びの先）。</summary>
    internal static (UnitDef Guard, Agg[] All) PickGuard()
    {
        var gv = PVers[3];
        var res = Guards.Select(g => Measure(ApplyP(HeroBoardOf(g), gv), I.Boss, PickSeeds)).ToArray();
        int best = 0;
        for (int i = 1; i < Guards.Length; i++)
        {
            double wb = res[best].Win, wi = res[i].Win;
            double tb = res[best].Wins == 0 ? double.MaxValue : (double)res[best].WinT / res[best].Wins, ti = res[i].Wins == 0 ? double.MaxValue : (double)res[i].WinT / res[i].Wins;
            if (wi > wb || (wi == wb && ti < tb)) best = i;
        }
        return (Guards[best], res);
    }

    static void Pick()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §3-2 毒特化・勇者の守り（毒爆弾のラウの席 ＝ 後3 に置く・GV の勇者・seed 0..{PickSeeds - 1}）");
        Console.WriteLine();
        var (g, res) = PickGuard();
        Console.WriteLine("| 守り | 並び | GV の勇者 | 規定の勇者 | GV の蝕みなし |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int i = 0; i < Guards.Length; i++)
        {
            var f = HeroBoardOf(Guards[i]);
            var b = Measure(f, I.Boss, PickSeeds); var nt = Measure(ApplyP(f, PVers[3]), S366.BossNoTax, PickSeeds);
            Console.WriteLine($"| {Short(Guards[i])} | {S307.Seats(f)} | {F1(res[i].Win)}・{res[i].WinTs}T | {F1(b.Win)}・{b.WinTs}T | {F1(nt.Win)}・{nt.WinTs}T |");
        }
        Console.WriteLine();
        Console.WriteLine($"選んだ守り: **{Short(g)}**（`Presets.PoisonHero367` は {Short(Presets.PoisonHero367()[4]!)}——{(g == Presets.PoisonHero367()[4] ? "一致" : "**不一致**")}）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-3 毒の版
    // ---------------------------------------------------------------------------------
    static string Nm(string id) => Short(UnitCatalog.Everyone.First(d => d.Id == id));

    static void Poison()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = PBoards();
        Console.WriteLine($"# {Tag} §3-3 毒の版 —— 毒の4台 × 版 × 波（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。版はグザ ／ ヴィオだけを替える（いない台では規定と同じ）。");
        Console.WriteLine();
        var grid = new Agg[boards.Length, PVers.Length, PWaves.Length];
        Parallel.For(0, boards.Length * PVers.Length * PWaves.Length, k =>
        {
            int bi = k / (PVers.Length * PWaves.Length), vi = k / PWaves.Length % PVers.Length, wi = k % PWaves.Length;
            grid[bi, vi, wi] = Measure(ApplyP(boards[bi].F, PVers[vi]), PWaves[wi], flow: true);
        });
        int bw = 0, nw = PWaves.Length - 1;

        Console.WriteLine("## 1. 4台 × 版 × 波（勝率・倒しT（勝った戦）・倒れた味方（1戦あたり））");
        Console.WriteLine();
        for (int bi = 0; bi < boards.Length; bi++)
        {
            Console.WriteLine($"### {boards[bi].Name}");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", PWaves.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(PWaves.Select(_ => "---|")));
            for (int vi = 0; vi < PVers.Length; vi++)
                Console.WriteLine($"| {PVers[vi].Name} | {string.Join(" | ", Enumerable.Range(0, PWaves.Length).Select(wi => grid[bi, vi, wi].Cell))} |");
            Console.WriteLine();
        }

        Console.WriteLine("## 2. 蝕みなしの勇者に届くか（勝率・倒しT）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", PVers.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(PVers.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(0, PVers.Length).Select(vi => $"{F1(grid[bi, vi, nw].Win)}・{grid[bi, vi, nw].WinTs}T"))} |");
        Console.WriteLine();

        foreach (var (wi, wname) in new[] { (bw, "勇者"), (nw, "蝕みなしの勇者") })
        {
            Console.WriteLine($"## 3. {wname}の毒の層（ターンの頭・勇者が生きていたターンの平均）と、敵へ書いた層の内訳（1戦あたり）");
            Console.WriteLine();
            Console.WriteLine("書いた層は計数の器具（`CounterProbe`）でそのとき動いていた札の持ち主に帰属させた（滲みの増分込み）。ヴィオの「倍 ／ 注ぎ」は触媒の名目（滲みの前）。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Enumerable.Range(1, HeroT).Select(t => $"T{t}")) + " | 書いた層（駒ごと）| ヴィオの倍 ／ 注ぎ |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(1, HeroT).Select(_ => "--:|")) + "---|---|");
            for (int bi = 0; bi < boards.Length; bi++)
                for (int vi = 0; vi < PVers.Length; vi++)
                {
                    var a = grid[bi, vi, wi];
                    string wrote = string.Join("・", a.Wrote.OrderByDescending(kv => kv.Value).Select(kv => $"{Nm(kv.Key)} {Per(kv.Value, a.N)}"));
                    Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {PVers[vi].Name} | {string.Join(" | ", Enumerable.Range(1, HeroT).Select(t => a.HAlive[t] == 0 ? "—" : Per(a.HLayer[t], a.HAlive[t])))} | {wrote} | {(Has(ApplyP(boards[bi].F, PVers[vi]), "vio") && PVers[vi].Vio is not null ? $"{Per(a.VioDouble, a.N)} ／ {Per(a.VioPour, a.N)}" : "—")} |");
                }
            Console.WriteLine();
        }

        Console.WriteLine("## 4. 勇者への与ダメのうち毒の刻み（名目）の割合");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", PVers.Select(v => v.Name + "（勇者 ／ 蝕みなし）")) + " |");
        Console.WriteLine("|---|" + string.Concat(PVers.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(0, PVers.Length).Select(vi => $"{Pct(Math.Min(grid[bi, vi, bw].HTick, grid[bi, vi, bw].HTaken), grid[bi, vi, bw].HTaken)}% ／ {Pct(Math.Min(grid[bi, vi, nw].HTick, grid[bi, vi, nw].HTaken), grid[bi, vi, nw].HTaken)}%"))} |");
        Console.WriteLine();

        Console.WriteLine("## 5. グザの味方への漏れ ／ ヴィオが吸った層 ／ 触媒（1戦あたり・グザ ／ ヴィオのいる台）");
        Console.WriteLine();
        Console.WriteLine("漏れは瘴気の名目（滲みの前）、吸った層は澱み喰いが腹に記帳した層（`SpitStored`）。触媒は手番 ／ 空振り（毒の敵も腹も無い）／ 毒の敵がいなくて腹だけ注いだ回数。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | グザ 敵へ ／ 味方へ | ヴィオが吸った | 触媒 手番 ／ 空振り ／ 腹だけ | 倍 ／ 注ぎ |");
        Console.WriteLine("|---|---|---|---|--:|---|---|");
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < PVers.Length; vi++)
                foreach (int wi in new[] { bw, 1, 4, nw })
                {
                    var a = grid[bi, vi, wi];
                    Console.WriteLine($"| {boards[bi].Name} | {PVers[vi].Name} | {PWaves[wi].Name} | {Per(a.GuzaFoe, a.N)} ／ {Per(a.GuzaLeak, a.N)} | {Per(a.VioDrawn, a.N)} | {Per(a.CatActs, a.N)} ／ {Per(a.CatDry, a.N)} ／ {Per(a.VioBellyOnly, a.N)} | {Per(a.VioDouble, a.N)} ／ {Per(a.VioPour, a.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 6. 誰が何Tに倒れたか（駒 倒れた戦の割合%（倒れたターンの平均））");
        Console.WriteLine();
        FellTable(boards.Select(b => b.Name).ToArray(), PVers.Select(v => v.Name).ToArray(), PWaves.Select(w => w.Name).ToArray(), (bi, vi, wi) => grid[bi, vi, wi]);

        var bad = new List<string>();
        var baseBoss = new List<string>();
        for (int bi = 0; bi < boards.Length; bi++)
        {
            for (int wi = 0; wi < PWaves.Length; wi++)
            {
                var b = grid[bi, 0, wi];
                if (PWaves[wi].Boss && b.Wins > 0 && (double)b.WinT / b.Wins < 4) baseBoss.Add($"{boards[bi].Name} × {PWaves[wi].Name} {b.WinTs}T");
                for (int vi = 1; vi < PVers.Length; vi++)
                {
                    var a = grid[bi, vi, wi]; var w = PWaves[wi];
                    if ((w == I.Guard || w == I.Bat) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{boards[bi].Name} × {PVers[vi].Name} × {w.Name}: 1T 決着 {Pct(a.Win1, a.N)}%");
                    if (w.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4 && !(b.Wins > 0 && (double)b.WinT / b.Wins < 4)) bad.Add($"{boards[bi].Name} × {PVers[vi].Name} × {w.Name}: 倒しT {a.WinTs}（規定 {b.WinTs}）");
                    if (100.0 * (a.Draw - b.Draw) / a.N >= 5) bad.Add($"{boards[bi].Name} × {PVers[vi].Name} × {w.Name}: 30T の打ち切り {Pct(b.Draw, b.N)} → {Pct(a.Draw, a.N)}%");
                    if (b.Win - a.Win >= 15) bad.Add($"{boards[bi].Name} × {PVers[vi].Name} × {w.Name}: 勝率 {F1(b.Win)} → {F1(a.Win)}（参考・落ちた側）");
                }
            }
        }
        Console.WriteLine("## 7. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満（規定で既に 4T 未満のセルは除く）／ 30T の打ち切りが 5pt 以上増える ／ 参考: 勝率が規定から 15pt 以上落ちる）");
        Console.WriteLine();
        Console.WriteLine(bad.Count == 0 ? "なし。" : string.Join(Environment.NewLine, bad.Select(x => "- " + x)));
        Console.WriteLine();
        Console.WriteLine($"規定で既にボス 4T 未満のセル: {(baseBoss.Count == 0 ? "なし" : string.Join("・", baseBoss))}。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-2 軛は刻みを切らない
    // ---------------------------------------------------------------------------------
    internal static (string Name, Formation F)[] YBoards() => new (string, Formation)[]
    {
        ("毒特化・集団", Row(BombRow)),
        ("毒特化・集団 GV", ApplyP(Row(BombRow), PVers[3])),
        ("毒特化・勇者", Hero367()),
        ("毒特化・勇者 GV", ApplyP(Hero367(), PVers[3])),
        ("混合 毒+耐久 (ベニ×トウ)", Row("毒+耐久 (ベニ×トウ)")),
        ("燃焼 試遊・燃焼 ポンの台", Presets.Versions.First(r => r.Name == "試遊・燃焼 ポンの台").F),
        ("対照 試遊・破片", Presets.Versions.First(r => r.Name == "試遊・破片").F),
        ("対照 台 B", Presets.HisaBoard366(UnitCatalog.HisaH367)),
    };
    internal static readonly I.W[] YWaves = { I.M4, I.StrongWaves[2] };

    static void YokeBoards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = YBoards();
        Console.WriteLine($"# {Tag} §4-2 軛は刻みを切らない（Y）—— 第4波 ／ 強い条件の第4波（HP 500% ／ 攻 300%）× 規定 ／ Y（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。");
        Console.WriteLine();
        Console.WriteLine("刻みと軛の量は受けた駒の側で、上限（25）を超えた刻み1回ごとに: 規定は「切られた回数 ／ 切り落とされた量」、Y は「切られずに通った回数 ／ 上限を超えて通った量」。敵側 ＝ 敵が受けた刻み、味方側 ＝ 味方が受けた刻み（毒 ／ 燃焼）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定 勝率・倒しT・倒 | Y 勝率・倒しT・倒 | 差 | 決着T（全戦）規定 → Y | 規定で切られた刻み 敵側 毒 ／ 燃焼（回・量）| Y で通った刻み 敵側 毒 ／ 燃焼（回・超えた量）| 味方側 規定 切られた ／ Y 通った（回） |");
        Console.WriteLine("|---|---|---|---|--:|---|---|---|---|");
        var res = new Agg[boards.Length, YWaves.Length, 2];
        Parallel.For(0, boards.Length * YWaves.Length * 2, k =>
        {
            int bi = k / (YWaves.Length * 2), wi = k / 2 % YWaves.Length, vi = k % 2;
            res[bi, wi, vi] = Measure(boards[bi].F, YWaves[wi], yoke: vi == 0 ? null : YokeY);
        });
        for (int bi = 0; bi < boards.Length; bi++)
            for (int wi = 0; wi < YWaves.Length; wi++)
            {
                var a = res[bi, wi, 0]; var y = res[bi, wi, 1];
                Console.WriteLine($"| {(wi == 0 ? boards[bi].Name : "")} | {YWaves[wi].Name} | {a.Cell} | {y.Cell} | {Sg(y.Win - a.Win)} | {Per(a.Turns, a.N)} → {Per(y.Turns, y.N)} | "
                    + $"{Per(a.YCut[0], a.N)}・{Per(a.YLost[0], a.N)} ／ {Per(a.YCut[1], a.N)}・{Per(a.YLost[1], a.N)} | {Per(y.YSpared[0], y.N)}・{Per(y.YOver[0], y.N)} ／ {Per(y.YSpared[1], y.N)}・{Per(y.YOver[1], y.N)} | "
                    + $"{Per(a.YCut[2] + a.YCut[3], a.N)} ／ {Per(y.YSpared[2] + y.YSpared[3], y.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 誰が何Tに倒れたか（駒 倒れた戦の割合%（倒れたターンの平均））");
        Console.WriteLine();
        FellTable(boards.Select(b => b.Name).ToArray(), new[] { "規定", "Y" }, YWaves.Select(w => w.Name).ToArray(), (bi, vi, wi) => res[bi, wi, vi]);
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §5-2 ヒサ H1
    // ---------------------------------------------------------------------------------
    internal static readonly I.W[] HWaves = new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M5 }.Concat(I.StrongWaves).ToArray();

    static void HisaBoards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = S366.BoardList();
        Console.WriteLine($"# {Tag} §5-2 ヒサ H1（羽の一撃では叫ばない）—— 標軸の5台 × 波 × 規定 ／ H1（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(b.F)}）")) + "。強い条件は第2〜5波 × HP 500% ／ 攻 300% の4波の平均。");
        Console.WriteLine();
        int s0 = HWaves.Length - I.StrongWaves.Length;
        var res = new Agg[boards.Length, 2, HWaves.Length];
        Parallel.For(0, boards.Length * 2 * HWaves.Length, k =>
        {
            int bi = k / (2 * HWaves.Length), vi = k / HWaves.Length % 2, wi = k % HWaves.Length;
            res[bi, vi, wi] = Measure(vi == 0 ? boards[bi].F : H1(boards[bi].F), HWaves[wi]);
        });
        Console.WriteLine("## 1. 勝率・倒しT（勝った戦）・倒れた味方（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", HWaves.Take(s0).Select(w => w.Name)) + " | 強い条件（平均） |");
        Console.WriteLine("|---|---|" + string.Concat(HWaves.Take(s0).Select(_ => "---|")) + "--:|");
        for (int bi = 0; bi < boards.Length; bi++)
            for (int vi = 0; vi < 2; vi++)
                Console.WriteLine($"| {(vi == 0 ? boards[bi].Name : "")} | {(vi == 0 ? "規定" : "H1")} | {string.Join(" | ", Enumerable.Range(0, s0).Select(wi => res[bi, vi, wi].Cell))} | {F1(Enumerable.Range(s0, I.StrongWaves.Length).Average(wi => res[bi, vi, wi].Win))} |");
        Console.WriteLine();
        Console.WriteLine("## 2. 叫びの回数 ／ 癒しの量（1戦あたり）・H1 で羽の一撃に叫ばなかった回数");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", HWaves.Take(s0).Select(w => w.Name + "（規定 → H1）")) + " |");
        Console.WriteLine("|---|" + string.Concat(HWaves.Take(s0).Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(0, s0).Select(wi => { var a = res[bi, 0, wi]; var h = res[bi, 1, wi]; return $"叫び {Per(a.Rallies, a.N)} → {Per(h.Rallies, h.N)}・癒し {Per(a.RallyHealed, a.N)} → {Per(h.RallyHealed, h.N)}・黙った {Per(h.FeatherQuiet, h.N)}"; }))} |");
        Console.WriteLine();
        Console.WriteLine("## 2b. 誰が何Tに倒れたか（駒 倒れた戦の割合%（倒れたターンの平均））");
        Console.WriteLine();
        FellTable(boards.Select(b => b.Name).ToArray(), new[] { "規定", "H1" }, HWaves.Take(s0).Select(w => w.Name).ToArray(), (bi, vi, wi) => res[bi, vi, wi]);

        var bad = new List<string>();
        for (int bi = 0; bi < boards.Length; bi++)
            for (int wi = 0; wi < HWaves.Length; wi++)
            {
                var a = res[bi, 1, wi]; var b = res[bi, 0, wi]; var w = HWaves[wi];
                if ((w == I.Guard || w == I.Bat) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{boards[bi].Name} × H1 × {w.Name}: 1T 決着 {Pct(a.Win1, a.N)}%");
                if (w == I.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4 && !(b.Wins > 0 && (double)b.WinT / b.Wins < 4)) bad.Add($"{boards[bi].Name} × H1 × ボス: 倒しT {a.WinTs}（規定 {b.WinTs}）");
                if (100.0 * (a.Draw - b.Draw) / a.N >= 5) bad.Add($"{boards[bi].Name} × H1 × {w.Name}: 30T の打ち切り {Pct(b.Draw, b.N)} → {Pct(a.Draw, a.N)}%");
                if (b.Win - a.Win >= 15) bad.Add($"{boards[bi].Name} × H1 × {w.Name}: 勝率 {F1(b.Win)} → {F1(a.Win)}");
            }
        Console.WriteLine("## 3. 壊れの兆し ／ 大きく落ちたセル（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満（規定で既に 4T 未満は除く）／ 30T の打ち切り +5pt ／ 勝率 −15pt 以上）");
        Console.WriteLine();
        Console.WriteLine(bad.Count == 0 ? "なし。" : string.Join(Environment.NewLine, bad.Select(x => "- " + x)));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` × 版
    // ---------------------------------------------------------------------------------
    static double[,] CGrid((string Name, Formation F)[] rows, Func<Formation, Formation> map, Func<int, bool> only, YokeRule? yoke = null)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            if (!only(ri)) { g[ri, wi] = double.NaN; return; }
            var f = map(rows[ri].F);
            int wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false, yoke: yoke).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        return g;
    }

    static void CompareVer(string ver)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().Select(r => (r.Name, F: Pin367(r.F))).ToArray();   // 第368期: 第367期までの規定に固定
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        (string Name, Func<Formation, Formation> Map, Func<int, bool> Only, YokeRule? Yoke) v = ver switch
        {
            "G" => ("G 集まる瘴気", f => ApplyP(f, PVers[1]), ri => Has(rows[ri].F, "guza"), null),
            "V" => ("V 触媒", f => ApplyP(f, PVers[2]), ri => Has(rows[ri].F, "vio"), null),
            "GV" => ("GV", f => ApplyP(f, PVers[3]), ri => Has(rows[ri].F, "guza") || Has(rows[ri].F, "vio"), null),
            "Y" => ("Y 軛は刻みを切らない", f => f, _ => true, YokeY),
            "H1" => ("H1 ヒサ", H1, ri => Has(rows[ri].F, "hisa"), null),
            _ => throw new ArgumentException("sh367 compare の版は G / V / GV / Y / H1"),
        };
        var b = CGrid(rows, f => f, _ => true);
        var g0 = CGrid(rows, v.Map, v.Only, v.Yoke);
        var g = new double[rows.Length, nw];
        int touched = 0;
        for (int r = 0; r < rows.Length; r++) { if (v.Only(r)) touched++; for (int w = 0; w < nw; w++) g[r, w] = v.Only(r) ? g0[r, w] : b[r, w]; }
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] x) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => x[r, w]))));
        Console.WriteLine($"# {Tag} `compare` {rows.Length} 行 × 版 {v.Name}（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"版で回した行 {touched}（版の駒がいない行は規定と同じ台 ＝ 同じ結果）。");
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
