using System.Reflection;
using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using S321 = Shiga321Diag;

// =====================================================================================
// tou323 —— 第323期「トウのテコ入れ」段1 粉の濃さ（TD）／ 段2 粉の伝染（TP）。
// 指示書は design/PHASE323_TOU_POWDER_SPEC.md ／ 報告は design/PHASE323_TOU_POWDER.md。
//
//     dotnet run --project BattleSim -c Release 0 tou323 p0                 # §1-4 ／ §2-3 Phase 0（規定のトウ・§2-3 の 3 だけ TP-a）
//     dotnet run --project BattleSim -c Release 0 tou323 boards [版,…]      # §3-2: 代表台 5 × 10 波 × 版と対照（トウ → グザ ／ ドルガ）
//     dotnet run --project BattleSim -c Release 0 tou323 compare [版,…]     # §3-3: `compare` 64 行 × 版（(G2) つき・基準は規定）
//     dotnet run --project BattleSim -c Release 0 tou323 grid <boss|guard> <版>   # §3-5: トウのいる台の格子（固定枠 トウ ＋ カタ・ボスはさらにソム）
//     dotnet run --project BattleSim -c Release 0 tou323 memo <台の一部> <波> <seed> <版>   # §4: 濃さ ／ 伝染の台本の並び
//     dotnet run --project BattleSim -c Release 0 tou323 check              # §5 自己検査
//
// 版の名前（ASCII）: base（規定）／ tda ／ tdb ／ tdab（TD-a焼）／ tdbb（TD-b焼）／ td0 ／ tpa ／ tpb ／ tdtp（重ねた版・§3-4）。波: boss ／ guard ／ bat ／ nine ／ nine2 ／ 1..5。
// **予測のファイルが無ければ本測定（boards ／ compare ／ grid）を走らせない**（R397・`.tmp/p323/predict.md`）。
// =====================================================================================
static class Tou323Diag
{
    internal const string PredictFile = ".tmp/p323/predict.md";
    const int Seeds = 200;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"tou323: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": BoardsAll(A(3, "")); return;
            case "compare": CompareAll(A(3, "")); return;
            case "grid": Grid(A(3, "boss"), A(4, "tdb")); return;
            case "memo": Memo(A(3, "光の盾"), A(4, "guard"), int.Parse(A(5, "0")), A(6, "tdb")); return;
            case "check": Check(); return;
            default: Console.WriteLine("tou323: モードは p0 / boards / compare / grid / memo / check。"); return;
        }
    }

    static string Short(UnitDef d) => S307.Short(d);
    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("N0");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    /// <summary>版。<paramref name="Return"/> ＝ カタ ／ クグも返す働きの版（`KataTDR` ／ `KuguTDR`）に替える。</summary>
    internal sealed record Ver(string Name, string Ascii, UnitDef Tou, bool Return);

    /// <summary>§3-4 重ねた版（TD-b ＋ TP-a・返す働きあり）。段1 ／ 段2 のいちばん良い版を見てから決める——ここでは候補の組だけ持つ。</summary>
    internal static readonly UnitDef TouTDbTPa = new()
    {
        Id = UnitCatalog.TouH324.Id, Name = UnitCatalog.TouH324.Name, MaxHp = UnitCatalog.TouH324.MaxHp, Attack = UnitCatalog.TouH324.Attack, Speed = UnitCatalog.TouH324.Speed,
        Advances = UnitCatalog.TouH324.Advances, Pattern = UnitCatalog.TouH324.Pattern, Actions = UnitCatalog.TouH324.Actions,
        Traits = UnitCatalog.TouTDb.Traits.Append(TraitId.PowderContagion).ToArray(),
        PlusText = UnitCatalog.TouTDb.PlusText + UnitCatalog.TouContagionText, MinusText = UnitCatalog.TouTDb.MinusText, Flavor = UnitCatalog.TouH324.Flavor,
    };

    internal static readonly Ver[] Vers =
    {
        new("規定", "base", UnitCatalog.TouH324, false),
        new("TD-a", "tda", UnitCatalog.TouTDa, true),
        new("TD-b", "tdb", UnitCatalog.TouTDb, true),
        new("TD-a焼", "tdab", UnitCatalog.TouTDaB, true),
        new("TD-b焼", "tdbb", UnitCatalog.TouTDbB, true),
        new("TD-0", "td0", UnitCatalog.TouTDb, false),
        new("TP-a", "tpa", UnitCatalog.TouTPa, false),
        new("TP-b", "tpb", UnitCatalog.TouTPb, false),
        new("TP-a′", "tpad", UnitCatalog.TouTPaDry, false),
    };
    static readonly Ver Stack = new("TD-b×TP-a", "tdtp", TouTDbTPa, true);
    static Ver V(string n) => Vers.Append(Stack).First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    internal static Formation Apply(Formation f, Ver v)
    {
        var g = S316.SwapId(f, "tou", v.Tou);
        if (v.Return) { g = S316.SwapId(g, "kata", UnitCatalog.KataTDR); g = S316.SwapId(g, "kugu", UnitCatalog.KuguTDR); }
        return g;
    }

    internal static readonly S287.Wave[] Waves = S321.Waves;
    internal static S287.Wave WaveOf(string n) => S321.WaveOf(n);
    internal static bool IsElite(S287.Wave w) => w.Name is "近衛" or "大隊" || w.Name.StartsWith("九体", StringComparison.Ordinal);

    internal static Formation Playtest(string n) => Pin324(Presets.Playtest.First(r => r.Name == n).F);   // 第325期: トウ ／ カタ ／ クグを第324期の規定に固定
    static Formation Row(string n) => CompareBuildsH324().First(r => r.Name == n).F;   // 第325期: トウ ／ カタ ／ クグを第324期の規定に固定

    /// <summary>§3-2 の代表台（駒はいまの規定）。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("光の盾", Playtest("試遊・感電 光の盾")),
        ("雷の型", Playtest("試遊・感電 雷の型")),
        ("糸", Playtest("試遊・感電 糸")),
        ("火の型", Playtest("試遊・感電 火の型")),
        ("責め苦", Row("責め苦 (トウ×シガ)")),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Z
    {
        public long N, Wins, WinT, LoseT, OneT, TouN, TouLife;
        public UnitTally T = new(), K = new(), G = new();
        public long AllyDisTaken, AllyDisDeaths, AllyDeaths, AllyN;
        public long FoePops, FoeRoots, AllyPopsEv, DisMax, BurnMax, DenseMaxEv;
        public long TouDmg, AllFoeDmg;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Merge(Z o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LoseT += o.LoseT; OneT += o.OneT; TouN += o.TouN; TouLife += o.TouLife;
            T.Add(o.T); K.Add(o.K); G.Add(o.G);
            AllyDisTaken += o.AllyDisTaken; AllyDisDeaths += o.AllyDisDeaths; AllyDeaths += o.AllyDeaths; AllyN += o.AllyN;
            FoePops += o.FoePops; FoeRoots += o.FoeRoots; AllyPopsEv += o.AllyPopsEv;
            DisMax = Math.Max(DisMax, o.DisMax); BurnMax = Math.Max(BurnMax, o.BurnMax); DenseMaxEv = Math.Max(DenseMaxEv, o.DenseMaxEv);
            TouDmg += o.TouDmg; AllFoeDmg += o.AllFoeDmg;
        }
    }

    internal static Z Count(List<UnitState> p, List<UnitState> e, BattleResult r)
    {
        var z = new Z { N = 1 };
        if (r.PlayerWon) { z.Wins = 1; z.WinT = r.Turns; if (r.Turns <= 1) z.OneT = 1; } else z.LoseT = r.Turns;
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        UnitState? tou = p.FirstOrDefault(u => u.Def.Id == "tou");
        int life = r.Turns;
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.ShockSpent when ev.Team == BattleContext.EnemyTeam:
                    z.FoePops++; if (ev.Slot == 0) z.FoeRoots++; break;
                case BattleEventKind.ShockSpent when ev.Team == BattleContext.PlayerTeam:
                    z.AllyPopsEv++; break;
                case BattleEventKind.Discharge:
                    if (ev.Amount > z.DisMax) z.DisMax = ev.Amount; break;
                case BattleEventKind.ShockGauge when ev.Text == ShockGaugeLabels.DenseBurn:
                    if (ev.Amount > z.BurnMax) z.BurnMax = ev.Amount; break;
                case BattleEventKind.ShockGauge when ev.Text is ShockGaugeLabels.Dense or ShockGaugeLabels.DenseFlow or ShockGaugeLabels.DenseMove:
                    if (ev.Amount > z.DenseMaxEv) z.DenseMaxEv = ev.Amount; break;
                case BattleEventKind.Damage when ev.TargetId is int t && foes.Contains(t) && ev.ActorId is int a && mine.Contains(a):
                    z.AllFoeDmg += ev.Amount;
                    if (tou is not null && a == tou.InstanceId) z.TouDmg += ev.Amount;
                    break;
                case BattleEventKind.Death when ev.TargetId is int dt && mine.Contains(dt):
                    z.AllyDeaths++;
                    if (tou is not null && dt == tou.InstanceId) life = Math.Min(life, ev.Turn);
                    break;
            }
        }
        if (tou is not null) { z.TouN = 1; z.TouLife = life; }
        z.AllyN = p.Count;
        foreach (string id in p.Select(u => u.Def.Id).Distinct())
            if (r.TallyByUnit.TryGetValue(id, out var t)) { z.AllyDisTaken += t.DischargeTaken; z.AllyDisDeaths += t.DischargeDeaths; }
        if (r.TallyByUnit.TryGetValue("tou", out var tt)) z.T.Add(tt);
        if (r.TallyByUnit.TryGetValue("kata", out var kt)) z.K.Add(kt);
        if (r.TallyByUnit.TryGetValue("kugu", out var gt)) z.G.Add(gt);
        return z;
    }

    internal static Z Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        return Count(p, e, r);
    }

    internal static Z Measure(Formation f, S287.Wave w, int n = Seeds, int from = 0)
    {
        var parts = new Z[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, from + i));
        var z = new Z();
        foreach (var c in parts) z.Merge(c);
        return z;
    }

    static int WinsLite(Formation f, S287.Wave w, int n = Seeds)
    {
        int wins = 0;
        Parallel.For(0, n, sd =>
        {
            if (BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false).PlayerWon) Interlocked.Increment(ref wins);
        });
        return wins;
    }

    internal static long H(long[]? h, int i) => h is null || h.Length <= i ? 0 : h[i];
    static string Fell(Z z) => $"{Per(H(z.T.PwFellShocked, 0), z.N)} ／ {Per(H(z.T.PwFellShocked, 1), z.N)} ／ {Per(H(z.T.PwFellShocked, 2), z.N)}";
    static string FellAll(Z z) => $"{Per(H(z.T.PwFell, 0), z.N)} ／ {Per(H(z.T.PwFell, 1), z.N)} ／ {Per(H(z.T.PwFell, 2), z.N)}";
    static string Chain(Z z) => z.FoeRoots == 0 ? "—" : (z.FoePops / (double)z.FoeRoots).ToString("F2");
    static string WinT(Z z) => $"{Per(z.WinT, z.Wins)} ／ {Per(z.LoseT, z.N - z.Wins)} ／ {Per(z.TouLife, z.TouN)}";

    static string Seats(Formation f) => S307.Seats(f);
    static bool Adj(Formation f, string a, string b)
    {
        var sa = f.Occupied().Where(o => o.Def.Id == a).Select(o => o.Slot).ToList();
        var sb = f.Occupied().Where(o => o.Def.Id == b).Select(o => o.Slot).ToList();
        return sa.Any(x => sb.Any(y => FormationRules.AreAdjacent(x, y)));
    }

    // ---------------------------------------------------------------------------------
    // §1-4 ／ §2-3 Phase 0（規定のトウ）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var cells = new List<(string B, S287.Wave W, Z Z)>();
        foreach (var (n, f) in boards) foreach (var w in Waves) cells.Add((n, w, Measure(f, w)));
        Console.WriteLine("# 第323期 Phase 0 —— 規定のトウの粉（seed 0..199・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("台の並び（前1・前3・中央・後1・後3）と、トウの隣にカタ ／ クグがいるか:");
        Console.WriteLine();
        foreach (var (n, f) in boards)
            Console.WriteLine($"- {n}: {Seats(f)}・トウ×カタ {(f.Occupied().Any(o => o.Def.Id == "kata") ? (Adj(f, "tou", "kata") ? "隣" : "離れ") : "—")}・トウ×クグ {(f.Occupied().Any(o => o.Def.Id == "kugu") ? (Adj(f, "tou", "kugu") ? "隣" : "離れ") : "—")}");
        Console.WriteLine();
        Console.WriteLine("## 1. トウの粉が、手番の頭にすでに帯電していた駒に落ちた回数（§1-4-1 ＝ 濃さが付く見込み）");
        Console.WriteLine();
        Console.WriteLine("粉が落ちた ＝ 生きている駒に粉が落ちた延べ（主目標 ／ 舞う先 ／ 漏れ先）。頭に帯電 ＝ そのうちトウの手番の頭に帯電していた駒（TD で濃さ +1）。トウの手番 ＝ 1戦あたりの手番数。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 決着T | トウの手番 | 粉が落ちた 主 ／ 舞 ／ 漏 | 頭に帯電 主 ／ 舞 ／ 漏 | 頭に帯電の割合 % |");
        Console.WriteLine("|---|---|--:|--:|--:|---|---|--:|");
        foreach (var c in cells)
        {
            long all = c.Z.T.PwFell?.Sum() ?? 0, sh = c.Z.T.PwFellShocked?.Sum() ?? 0;
            Console.WriteLine($"| {c.B} | {c.W.Name} | {F1(c.Z.Win)} | {Per(c.Z.WinT + c.Z.LoseT, c.Z.N)} | {Per(c.Z.T.TurnsTaken, c.Z.N)} | {FellAll(c.Z)} | {Fell(c.Z)} | {Pct(sh, all)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 2. 濃さが付く見込みの敵が、次に弾けるまでのターンと、そのときの隣の数（§1-4-2・ボス ／ 精鋭 ／ 九体）");
        Console.WriteLine();
        Console.WriteLine("見張り ＝ 手番の頭に帯電していて粉が落ちた敵（同じ敵は弾けるまで1回）。弾けた ＝ 戦の中で次に弾けた割合。ターン ＝ 落ちてから弾けるまで（0 ＝ 同じターン）。隣 ＝ 弾けたときの生きている隣の敵の数（0／1／2／3／4／5+ の分布）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 見張り | 弾けた % | ターン | 隣（平均）| 隣の分布 0／1／2／3／4／5+ |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|---|");
        foreach (var c in cells.Where(c => c.W.Boss || IsElite(c.W)))
        {
            var h = c.Z.T.PwWatchNbrHist;
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Per(c.Z.T.PwWatch, c.Z.N)} | {Pct(c.Z.T.PwWatchPopped, c.Z.T.PwWatch)} | {Per2(c.Z.T.PwWatchTurnSum, c.Z.T.PwWatchPopped)} | {Per2(c.Z.T.PwWatchNbrSum, c.Z.T.PwWatchPopped)} | {string.Join("／", Enumerable.Range(0, 6).Select(i => H(h, i).ToString()))} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 3. 漏れでカタ ／ クグに付く見込み（§1-4-3）");
        Console.WriteLine();
        Console.WriteLine("漏れが落ちた（うち頭に帯電 ＝ TD で濃さが付く）。クグ 組み付き ＝ 漏れが落ちたときクグが生きている敵に組み付いていた割合（クグの版で敵へ流れる見込み）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | カタ 漏れ（頭に帯電） | クグ 漏れ（頭に帯電） | クグ 組み付き % |");
        Console.WriteLine("|---|---|---|---|--:|");
        foreach (var c in cells.Where(c => c.Z.T.PwLeakKata + c.Z.T.PwLeakKugu > 0))
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Per(c.Z.T.PwLeakKata, c.Z.N)}（{Per(c.Z.T.PwLeakKataShocked, c.Z.N)}） | {Per(c.Z.T.PwLeakKugu, c.Z.N)}（{Per(c.Z.T.PwLeakKuguShocked, c.Z.N)}） | {Pct(c.Z.T.PwLeakKuguHeld, c.Z.T.PwLeakKugu)} |");
        Console.WriteLine();
        Console.WriteLine("## 4. 味方が弾けた回数と、そのときの隣の味方の数（§1-4-4 ＝ マイナスの見込み）");
        Console.WriteLine();
        Console.WriteLine("敵の一撃 ＝ 連鎖を起こした一撃の主が敵。ほか ＝ 味方の一撃 ／ 刻み ／ 出どころ無し。隣 ＝ 弾けたときの生きている隣の味方（TD で 8 ×（1 ＋ n）を受ける数）。味方の放電被弾 ＝ 味方が受けた放電の合計（規定）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 味方が弾けた 敵の一撃 ／ ほか | 隣の味方（平均） | 味方の放電被弾 | 放電で倒れた味方 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|");
        foreach (var c in cells)
        {
            long ap = c.Z.T.PwAllyPopsFoeHit + c.Z.T.PwAllyPopsOther;
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Per(c.Z.T.PwAllyPopsFoeHit, c.Z.N)} ／ {Per(c.Z.T.PwAllyPopsOther, c.Z.N)} | {Per2(c.Z.T.PwAllyPopNbrSum, ap)} | {Per(c.Z.AllyDisTaken, c.Z.N)} | {Per2(c.Z.AllyDisDeaths, c.Z.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 5. 粉の付いた駒が弾けたとき、帯電していない隣がいた回数と数（§2-3-1 ／ §2-3-2 ＝ TP で新しく帯電する見込み）");
        Console.WriteLine();
        Console.WriteLine("粉の付いた ＝ 一度でもトウの粉が落ちた駒（倒れるまで）。弾けた ＝ その駒が弾けた延べ。乾いた隣あり ＝ そのとき帯電していない生きている隣が1体以上いた。乾いた隣 ＝ その数の合計（TP-a で新しく帯電する数の上限）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 敵 弾けた ／ 乾いた隣あり ／ 乾いた隣 | 味方 弾けた ／ 乾いた隣あり ／ 乾いた隣 | 敵の弾け（全体）／ 連鎖の大きさ |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var c in cells)
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Per(c.Z.T.PwMarkedPopsFoe, c.Z.N)} ／ {Per(c.Z.T.PwMarkedPopsFoeDry, c.Z.N)} ／ {Per(c.Z.T.PwMarkedDryFoe, c.Z.N)} | {Per(c.Z.T.PwMarkedPopsAlly, c.Z.N)} ／ {Per(c.Z.T.PwMarkedPopsAllyDry, c.Z.N)} ／ {Per(c.Z.T.PwMarkedDryAlly, c.Z.N)} | {Per(c.Z.FoePops, c.Z.N)} ／ {Chain(c.Z)} |");
        Console.WriteLine();
        Console.WriteLine("## 6. 新しく帯電した敵が、次にいつ弾けるか（§2-3-3・**TP-a で測った**——規定では伝染が起きない）");
        Console.WriteLine();
        Console.WriteLine("伝染 ＝ 伝染で新しく帯電した敵 ／ 味方。弾けた ＝ 伝染した敵が後で弾けた割合。ターン ＝ 帯電してから弾けるまで。起こし手 ＝ その弾けの連鎖を起こした一撃の主（トウ ／ カタ ／ シガ ／ ほかの味方 ／ 出どころ無し ／ 敵）。カタの雷は感電を起爆しないので、カタは起こし手に入らない。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 伝染 敵 ／ 味方 | 弾けた % | ターン | 起こし手 トウ／カタ／シガ／ほか／無し／敵 |");
        Console.WriteLine("|---|---|---|--:|--:|---|");
        var tpa = V("tpa");
        foreach (var (n, f) in boards)
            foreach (var w in Waves)
            {
                var z = Measure(Apply(f, tpa), w);
                var h = z.T.TpInfPopIniHist;
                Console.WriteLine($"| {n} | {w.Name} | {Per(z.T.TpNewFoe, z.N)} ／ {Per(z.T.TpNewAlly, z.N)} | {Pct(z.T.TpInfPopsFoe, z.T.TpNewFoe)} | {Per2(z.T.TpInfPopTurnSum, z.T.TpInfPopsFoe)} | {string.Join("／", Enumerable.Range(0, 6).Select(i => H(h, i).ToString()))} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 7. トウの手番の値打ち（規定・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | トウの手番の中で弾けた敵 | その放電が敵に与えたダメージ | トウの与ダメ（台本） |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        foreach (var c in cells)
            Console.WriteLine($"| {c.B} | {c.W.Name} | {Per(c.Z.T.TouTurnPops, c.Z.N)} | {Per0(c.Z.T.TouTurnDisDealt, c.Z.N)} | {Per0(c.Z.TouDmg, c.Z.N)} |");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §3-2 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(string only) => BoardsWith(Pick(only), "第323期");

    /// <summary>代表台 × 波 × 版（第324期: 版の列と見出しを引数にした——`tou324` が同じ表を出す。第323期の出力は1文字も変わらない）。</summary>
    internal static void BoardsWith(Ver[] vers, string tag)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var res = new Dictionary<(string, string, string), Z>();
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = Measure(Apply(bf, v), w);
        var guza = new Dictionary<(string, string), int>(); var dolga = new Dictionary<(string, string), int>();
        foreach (var (bn, bf) in boards) foreach (var w in Waves)
            if (vers.Any(v => res[(bn, v.Name, w.Name)].Wins * 2 >= Seeds))
            {
                guza[(bn, w.Name)] = WinsLite(S316.SwapId(bf, "tou", UnitCatalog.Guza), w);
                dolga[(bn, w.Name)] = WinsLite(S316.SwapId(bf, "tou", UnitCatalog.Dolga), w);
            }
        Console.WriteLine($"# {tag} §3-2 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("版の駒: TD-a ／ TD-b ／ 焼 ／ 重ねた版はカタ ／ クグも返す働きの版（`KataTDR` ／ `KuguTDR`）に替える。TD-0 ／ TP はカタ ／ クグを規定のまま。");
        Console.WriteLine();
        Console.WriteLine("## 1. 勝率（括弧は規定との差）と対照（トウ → グザ ／ ドルガ・どれかの版で 50% 以上のセルだけ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")));
        foreach (var (bn, _) in boards)
        {
            foreach (var v in vers)
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    double a = res[(bn, v.Name, w.Name)].Win, b0 = res[(bn, vers[0].Name, w.Name)].Win;
                    return v == vers[0] || a == b0 ? F1(a) : $"{F1(a)}（{Sg(a - b0)}）";
                })) + " |");
            Console.WriteLine($"| {bn} | 対照 グザ | " + string.Join(" | ", Waves.Select(w => guza.TryGetValue((bn, w.Name), out int d) ? F1(100.0 * d / Seeds) : "—")) + " |");
            Console.WriteLine($"| {bn} | 対照 ドルガ | " + string.Join(" | ", Waves.Select(w => dolga.TryGetValue((bn, w.Name), out int d) ? F1(100.0 * d / Seeds) : "—")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("## 2. トウ → グザ の差（版の勝率 − グザの勝率・版が 50% 以上のセルだけ・括弧は規定の差からの変化）");
        Console.WriteLine();
        Console.WriteLine("**トウが要る** ＝ 版の勝率 50% 以上かつグザで半分以下。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 50% 以上のセル | 差の平均 | トウが要るセル | セルごと（台 × 波: 差） |");
        Console.WriteLine("|---|--:|--:|--:|---|");
        foreach (var v in vers)
        {
            var items = new List<string>(); double sum = 0; int n = 0, need = 0;
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                {
                    var z = res[(bn, v.Name, w.Name)];
                    if (z.Wins * 2 < Seeds || !guza.TryGetValue((bn, w.Name), out int gz)) continue;
                    double d = z.Win - 100.0 * gz / Seeds, d0 = res[(bn, vers[0].Name, w.Name)].Win - 100.0 * gz / Seeds;
                    sum += d; n++;
                    if (gz * 2 <= z.Wins) need++;
                    items.Add(v == vers[0] || Math.Abs(d - d0) < 0.05 ? $"{bn}×{w.Name} {Sg(d)}" : $"{bn}×{w.Name} {Sg(d)}（{Sg(d - d0)}）");
                }
            Console.WriteLine($"| {v.Name} | {n} | {(n == 0 ? "—" : Sg(sum / n))} | {need} | {string.Join("・", items)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 3. 倒しT ／ 負けT ／ トウの寿命（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            foreach (var v in vers)
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", Waves.Select(w => WinT(res[(bn, v.Name, w.Name)]))) + " |");
        Console.WriteLine();
        var td = vers.Where(v => v.Tou.Traits.Contains(TraitId.PowderDense)).ToArray();
        if (td.Length > 0)
        {
            Console.WriteLine("## 4. 濃さ（TD・1戦あたり）");
            Console.WriteLine();
            Console.WriteLine("付いた ＝ 濃さ +1 の延べ（敵 ／ 味方・クグの流した分は敵）。弾けた ＝ 濃さ ≧1 で弾けた延べ（敵 ／ 味方）。濃さの分布 ＝ 弾けたときの濃さ 1／2／3／4／5+（全戦の延べ）・最大。"
                + "重い放電 ＝ 濃い放電が敵 ／ 味方に与えた量（上乗せの名目）。焼 ＝ 本人を焼いた量 敵 ／ 味方。1回の放電の最大 ＝ 台本の `Discharge` の最大（全戦）。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 波 | 版 | 付いた 敵 ／ 味方 | 弾けた 敵 ／ 味方 | 濃さの分布（最大） | 重い放電 敵 ／ 味方 | 焼 敵 ／ 味方 | 1回の放電の最大 |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|--:|");
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                    foreach (var v in td)
                    {
                        var z = res[(bn, v.Name, w.Name)];
                        var h = z.T.TdPopHist;
                        string dist = string.Join("／", new[] { H(h, 1), H(h, 2), H(h, 3), H(h, 4), Enumerable.Range(5, 4).Sum(i => H(h, i)) });
                        Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {Per(z.T.TdGainFoe, z.N)} ／ {Per(z.T.TdGainAlly, z.N)} | {Per(z.T.TdPopFoe, z.N)} ／ {Per(z.T.TdPopAlly, z.N)} | {dist}（{z.T.TdPopMax}） | {Per0(z.T.TdHeavyFoeDealt, z.N)}（{Per0(z.T.TdHeavyFoeExtra, z.N)}） ／ {Per0(z.T.TdHeavyAllyDealt, z.N)}（{Per0(z.T.TdHeavyAllyExtra, z.N)}） | {Per0(z.T.TdBurnFoe, z.N)} ／ {Per0(z.T.TdBurnAlly, z.N)} | {z.DisMax} |");
                    }
            Console.WriteLine();
            Console.WriteLine("## 5. 返す働き（カタ ／ クグがいる台・1戦あたり）");
            Console.WriteLine();
            Console.WriteLine("カタ 移した ＝ 雷で濃さを移した回数（量）／ 移す濃さが無かった雷。クグ 流した ＝ 濃さを糸で敵へ流した ／ 組み付いていなくて自分に付いた。返した分の放電 ＝ 返した濃さが乗った放電の上乗せ（名目 ／ 与えた量の按分）。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 波 | 版 | カタ 移した（量）／ 空 | クグ 流した ／ 自分に | 返した分の放電 名目 ／ 与えた | 味方の放電被弾 |");
            Console.WriteLine("|---|---|---|---|---|---|--:|");
            foreach (var (bn, bf) in boards.Where(b => b.F.Occupied().Any(o => o.Def.Id is "kata" or "kugu")))
                foreach (var w in Waves)
                    foreach (var v in td)
                    {
                        var z = res[(bn, v.Name, w.Name)];
                        Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {Per(z.K.TdKataMoves, z.N)}（{Per(z.K.TdKataMoved, z.N)}）／ {Per(z.K.TdKataDry, z.N)} | {Per(z.G.TdKuguFlowed, z.N)} ／ {Per(z.G.TdKuguKept, z.N)} | {Per0(z.T.TdRetNominal, z.N)} ／ {Per0(z.T.TdRetDealt, z.N)} | {Per(z.AllyDisTaken, z.N)} |");
                    }
            Console.WriteLine();
        }
        var tp = vers.Where(v => v.Tou.Traits.Contains(TraitId.PowderContagion)).ToArray();
        if (tp.Length > 0)
        {
            Console.WriteLine("## 6. 伝染（TP・1戦あたり）");
            Console.WriteLine();
            Console.WriteLine("新しく帯電 ＝ 伝染で帯電した敵 ／ 味方。後で弾けた ＝ 伝染した敵が後で弾けた延べ（味方は ／ の後）。同じ連鎖で止めた ＝ 新しく帯電した駒に同じ連鎖の放電が届いた（弾けさせない）。帯電し直した ＝ 新しく帯電した敵 ／ 味方のうち、同じ連鎖で先に弾けていた駒（弾けて乾いたところへ伝染が届いた）。印が広がった ＝ TP-b。弾けの総数 ／ 連鎖の大きさは敵の側。");
            Console.WriteLine();
            Console.WriteLine("| 台 | 波 | 版 | 新しく帯電 敵 ／ 味方 | うち帯電し直した | 後で弾けた 敵 ／ 味方 | 同じ連鎖で止めた | 印が広がった | 敵の弾け ／ 連鎖の大きさ |");
            Console.WriteLine("|---|---|---|---|--:|---|--:|--:|---|");
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                    foreach (var v in tp.Prepend(vers[0]))
                    {
                        var z = res[(bn, v.Name, w.Name)];
                        Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {Per(z.T.TpNewFoe, z.N)} ／ {Per(z.T.TpNewAlly, z.N)} | {Per(z.T.TpReArm, z.N)} | {Per(z.T.TpInfPopsFoe, z.N)} ／ {Per(z.T.TpInfPopsAlly, z.N)} | {Per(z.T.TpFreshSkip, z.N)} | {Per(z.T.TpMarkSpread, z.N)} | {Per(z.FoePops, z.N)} ／ {Chain(z)} |");
                    }
            Console.WriteLine();
        }
        Console.WriteLine("## 7. マイナスとトウの手番の値打ち（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("味方の放電被弾 ＝ 味方が受けた放電の合計（焼で本人を焼いた分は含まない・右に別記）。放電で倒れた ＝ 味方が放電で倒れた延べ ÷ 味方が倒れた延べ。トウの手番 ＝ トウの手番の中で弾けた敵 ／ その放電が敵に与えたダメージ。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 味方の弾け | 味方の放電被弾（焼） | 放電で倒れた ／ 倒れた味方 | トウの手番 弾け ／ 放電の与ダメ |");
        Console.WriteLine("|---|---|---|--:|---|---|---|");
        foreach (var (bn, _) in boards)
            foreach (var w in Waves)
                foreach (var v in vers)
                {
                    var z = res[(bn, v.Name, w.Name)];
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {Per(z.AllyPopsEv, z.N)} | {Per(z.AllyDisTaken, z.N)}（{Per(z.T.TdBurnAlly, z.N)}） | {Per2(z.AllyDisDeaths, z.N)} ／ {Per2(z.AllyDeaths, z.N)}（{Pct(z.AllyDisDeaths, z.AllyDeaths)}%） | {Per(z.T.TouTurnPops, z.N)} ／ {Per0(z.T.TouTurnDisDealt, z.N)} |");
                }
        Console.WriteLine();
        Console.WriteLine("## 8. 強すぎの兆し（§1-3: 1回の放電が 500 超 ／ 倒しT が規定の半分以下 ／ 精鋭 ／ 九体の 1T 決着 5% 以上）と格子を回す版（§3-5: ±10pt（ボスは ±5pt または倒しT ±3T））");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var hits = new List<string>(); var strong = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                {
                    var z = res[(bn, v.Name, w.Name)]; var b0 = res[(bn, vers[0].Name, w.Name)];
                    double d = z.Win - b0.Win;
                    double tv = z.Wins > 0 ? (double)z.WinT / z.Wins : 0, tb = b0.Wins > 0 ? (double)b0.WinT / b0.Wins : 0;
                    double dt = z.Wins > 0 && b0.Wins > 0 ? tv - tb : 0;
                    bool hit = w.Boss ? Math.Abs(d) >= 5 || Math.Abs(dt) >= 3 : Math.Abs(d) >= 10;
                    if (hit) hits.Add($"{bn} × {w.Name}（{Sg(d)}pt{(w.Boss ? $"・倒しT {Sg(dt)}" : "")}）");
                    if (z.Wins > 0 && b0.Wins > 0 && tv * 2 <= tb) strong.Add($"{bn} × {w.Name} 倒しT {F1(tb)} → {F1(tv)}");
                    if (IsElite(w) && z.OneT * 20 >= z.N) strong.Add($"{bn} × {w.Name} 1T 決着 {Pct(z.OneT, z.N)}%");
                    if (z.DisMax > 500) strong.Add($"{bn} × {w.Name} 1回の放電 {z.DisMax}");
                }
            Console.WriteLine($"- **{v.Name}**: 強すぎの兆し {(strong.Count == 0 ? "なし" : string.Join("・", strong))}。格子 {(hits.Count == 0 ? "該当なし（回さない）" : $"{hits.Count} セル → 回す。" + string.Join("・", hits))}");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §3-3 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll(string only) => CompareWith(Pick(only), "第323期");

    internal static void CompareWith(Ver[] vers, string tag)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuildsH324().ToArray();   // 第325期: トウ ／ カタ ／ クグを第324期の規定に固定
        int nw = EnemyCatalog.Stages.Count;
        double[,] Grid(Ver v)
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = Apply(rows[ri].F, v);
                int wins = 0;
                for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / Seeds;
            });
            return g;
        }
        var b = Grid(vers[0]);
        Console.WriteLine($"# {tag} §3-3 `compare` {rows.Length} 行 × 版（seed 0..{Seeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"トウのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "tou")).Select(r => r.Name))}");
        Console.WriteLine();
        Console.WriteLine($"カタ ／ クグのいる行（トウなし・返す働きの版で動きうる）: {string.Join("・", rows.Where(r => !r.F.Occupied().Any(o => o.Def.Id == "tou") && r.F.Occupied().Any(o => o.Def.Id is "kata" or "kugu")).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, Grid(vers[vi]), "tou");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-5 格子（固定枠 トウ ＋ カタ（ボスはさらにソム）・探索枠は残り・駒はいまの規定）
    // ---------------------------------------------------------------------------------
    static void Grid(string waveKey, string ver) => GridWith(waveKey, V(ver), "第323期");

    internal static void GridWith(string waveKey, Ver v, string tag)
    {
        var w = WaveOf(waveKey);
        var fixedU = w.Boss ? new[] { UnitCatalog.TouH324, UnitCatalog.KataH324, UnitCatalog.Som } : new[] { UnitCatalog.TouH324, UnitCatalog.KataH324 };
        int free = 5 - fixedU.Length;
        UnitDef Now(UnitDef d) => PinTKK324(UnitCatalog.All.FirstOrDefault(x => x.Id == d.Id) ?? d);   // 第325期: トウ ／ カタ ／ クグを第324期の規定に固定
        var heal = S287.Heal.Select(d => d.Id).ToHashSet();
        var fixedIds = fixedU.Select(d => d.Id).ToHashSet();
        var pool = S287.Pool.Select(Now).Append(UnitCatalog.Shiga).Where(d => !fixedIds.Contains(d.Id)).GroupBy(d => d.Id).Select(g => g.First()).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var combos = new List<UnitDef[]>();
        void Rec(int start, List<UnitDef> cur)
        {
            if (cur.Count == free) { if (cur.Count(d => heal.Contains(d.Id)) <= 1) combos.Add(cur.ToArray()); return; }
            for (int i = start; i < pool.Length; i++) { cur.Add(pool[i]); Rec(i + 1, cur); cur.RemoveAt(cur.Count - 1); }
        }
        Rec(0, new List<UnitDef>());
        // 席: 固定枠2（近衛）は探索枠が3つで台が増えるので、席 120 通りのうち 1/4（30 通り・乱数を引かない間引き）にする（決めたこと）。
        int step = free >= 3 ? 4 : 1;
        var boards = new List<UnitDef[]>();
        foreach (var c in combos) boards.AddRange(B283.Perms(fixedU.Concat(c).ToArray()).Where((_, i) => i % step == 0));
        const int cut = 20;
        var res = new (int B, int V, int Gb, int Gv, long Tb, long Tv)?[boards.Count];
        (int W, long T) Wins(Formation f, int n)
        {
            int c = 0; long t = 0;
            for (int sd = 0; sd < n; sd++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                if (r.PlayerWon) { c++; t += r.Turns; }
            }
            return (c, t);
        }
        Parallel.For(0, boards.Count, i =>
        {
            var f0 = B283.Seat(boards[i]);
            var fv = Apply(f0, v);
            if (Wins(f0, cut).W < 5 && Wins(fv, cut).W < 5) return;
            var (wb, tb) = Wins(f0, Seeds); var (wv, tv) = Wins(fv, Seeds);
            int gb = wb * 2 >= Seeds || wv * 2 >= Seeds ? Wins(S316.SwapId(f0, "tou", UnitCatalog.Guza), Seeds).W : -1;
            res[i] = (wb, wv, gb, gb, tb, tv);
        });
        var all = res.Where(r => r is not null).Select(r => r!.Value).ToList();
        bool Half(int x) => x * 2 >= Seeds;
        bool Need(int x, int g) => Half(x) && g >= 0 && g * 2 <= x;
        Console.WriteLine($"# {tag} §3-5 格子 —— {w.Name} × 規定 ／ {v.Name}（固定枠 {string.Join(" ＋ ", fixedU.Select(Short))}・探索枠{free}（候補 {pool.Length} 枚: {string.Join("・", pool.Select(Short))}・ヒーラーは 1 枚まで）{combos.Count} 組・席 {120 / step} ＝ {boards.Count:N0} 台）");
        Console.WriteLine();
        Console.WriteLine($"足切り seed 0..{cut - 1}（どちらかで 5 勝以上・{all.Count:N0} 台）→ seed 0..{Seeds - 1}。**トウが要る台** ＝ 50% 以上 かつ トウ → グザで半分以下。所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
        Console.WriteLine();
        long sb = all.Sum(r => (long)r.B), sv = all.Sum(r => (long)r.V);
        Console.WriteLine($"| 段 | 規定 | {v.Name} |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| 勝ちの延べ（届いた量） | {sb:N0} | {sv:N0} |");
        Console.WriteLine($"| 勝率 ≧ 50% | {all.Count(r => Half(r.B)):N0} | {all.Count(r => Half(r.V)):N0} |");
        Console.WriteLine($"| **トウが要る** | {all.Count(r => Need(r.B, r.Gb)):N0} | {all.Count(r => Need(r.V, r.Gv)):N0} |");
        Console.WriteLine($"| 勝率 ≧ 90% | {all.Count(r => r.B * 10 >= Seeds * 9):N0} | {all.Count(r => r.V * 10 >= Seeds * 9):N0} |");
        Console.WriteLine($"| 倒しT（勝ちの平均） | {Per(all.Sum(r => r.Tb), sb)} | {Per(all.Sum(r => r.Tv), sv)} |");
        Console.WriteLine($"| 版 − 規定 の平均（pt） | — | {(all.Count == 0 ? 0 : all.Average(r => 100.0 * (r.V - r.B) / Seeds)):+0.0;-0.0} |");
        Console.WriteLine($"| 版が +10pt 以上 ／ −10pt 以下 | — | {all.Count(r => r.V - r.B >= Seeds / 10):N0} ／ {all.Count(r => r.V - r.B <= -Seeds / 10):N0} |");
    }

    // ---------------------------------------------------------------------------------
    // §4 台本の並び（濃さ ／ 移した ／ 流した ／ 重い放電 ／ 本人を焼いた ／ 伝染）
    // ---------------------------------------------------------------------------------
    static readonly HashSet<string> MemoLabels = new()
    {
        ShockGaugeLabels.Dense, ShockGaugeLabels.DenseFlow, ShockGaugeLabels.DenseMove, ShockGaugeLabels.DensePop,
        ShockGaugeLabels.DenseBurn, ShockGaugeLabels.PowderMark, ShockGaugeLabels.Contagion,
    };

    static void Memo(string boardPart, string wave, int seed, string ver) => MemoWith(boardPart, wave, seed, V(ver), "tou323");

    internal static void MemoWith(string boardPart, string wave, int seed, Ver v, string tool)
    {
        var (name, f0) = Boards().First(b => b.Name.Contains(boardPart, StringComparison.Ordinal));
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(Apply(f0, v), BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"獣#{i}") : "—";
        Console.WriteLine($"# {tool} memo —— {name} × {v.Name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("第323期の `ShockGauge`（濃さ ／ 濃さ・流した ／ 濃さ・移した ／ 濃さ・弾けた ／ 濃さ・焼いた ／ 粉の印 ／ 伝染）と、その前後の `ShockSpent` ／ `Discharge` ／ 感電の `StatusGain`。");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1, lines = 0;
        var ev = r.Events;
        var near = new HashSet<int>();
        for (int i = 0; i < ev.Count; i++)
            if (ev[i].Kind == BattleEventKind.ShockGauge && ev[i].Text is string tx && MemoLabels.Contains(tx))
                for (int j = Math.Max(0, i - 2); j <= Math.Min(ev.Count - 1, i + 3); j++) near.Add(j);
        for (int i = 0; i < ev.Count; i++)
        {
            var x = ev[i];
            if (!near.Contains(i)) continue;
            if (x.Kind is not (BattleEventKind.ShockGauge or BattleEventKind.ShockSpent or BattleEventKind.Discharge or BattleEventKind.StatusGain or BattleEventKind.Damage or BattleEventKind.Thunder or BattleEventKind.Attack)) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.PartnerId is int pp ? $"  Partner {N(pp)}" : "")}{(x.SpreadFromId is int sf ? $"  SpreadFrom {N(sf)}" : "")}{(x.PowderRoute is { } pr ? $"  Powder {pr}" : "")}");
            if (++lines >= 220) { Console.WriteLine("…（以下略）"); break; }
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // §5 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    internal static readonly PropertyInfo TurnProp = typeof(BattleContext).GetProperty("Turn") ?? throw new InvalidOperationException("Turn が見つからない");
    static readonly PropertyInfo TallyProp = typeof(BattleContext).GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TallyByUnit が見つからない");
    internal static UnitTally Tal(BattleContext ctx, string id) => ((Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!).GetValueOrDefault(id) ?? new UnitTally();

    internal static BattleContext Ctx(Formation pl, Formation foes, out List<UnitState> p, out List<UnitState> e, bool verbose = true, int seed = 0)
    {
        var ctx = new BattleContext(seed, verbose);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(foes, BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnProp.SetValue(ctx, 1);
        return ctx;
    }
    internal static UnitDef Wall => UnitCatalog.Dolga.WithStats(5000, 1);
    internal static UnitDef Mate => UnitCatalog.Dolga.WithStats(5000, 1);
    internal static int Shock(UnitState u) => u.RawCounter(StatusKeys.Shock);
    internal static int Spent(BattleContext ctx, UnitState u, int from) => ctx.Events.Skip(from).Count(x => x.Kind == BattleEventKind.ShockSpent && x.TargetId == u.InstanceId);
    static List<int> DisAmounts(BattleContext ctx, UnitState src, int from) => ctx.Events.Skip(from).Where(x => x.Kind == BattleEventKind.Discharge && x.ActorId == src.InstanceId).Select(x => x.Amount).ToList();
    internal static UnitState Seat(List<UnitState> l, int slot) => l.First(u => u.Slot == slot);
    internal static int Burned(BattleContext ctx, UnitState u, int from) => ctx.Events.Skip(from).Where(x => x.Kind == BattleEventKind.ShockGauge && x.Text == ShockGaugeLabels.DenseBurn && x.TargetId == u.InstanceId).Sum(x => x.Amount);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# tou323 check —— 第323期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // ---- 版の定義
        {
            var T = UnitCatalog.TouH324;
            bool tda = UnitCatalog.TouTDa.Traits.SequenceEqual(T.Traits.Append(TraitId.PowderDense));
            bool tdb = UnitCatalog.TouTDb.Traits.SequenceEqual(T.Traits.Concat(new[] { TraitId.PowderDense, TraitId.PowderDenseKeep }));
            bool tdab = UnitCatalog.TouTDaB.Traits.SequenceEqual(T.Traits.Concat(new[] { TraitId.PowderDense, TraitId.PowderDenseBurn }));
            bool tdbb = UnitCatalog.TouTDbB.Traits.SequenceEqual(T.Traits.Concat(new[] { TraitId.PowderDense, TraitId.PowderDenseKeep, TraitId.PowderDenseBurn }));
            bool tpa = UnitCatalog.TouTPa.Traits.SequenceEqual(T.Traits.Append(TraitId.PowderContagion));
            bool tpb = UnitCatalog.TouTPb.Traits.SequenceEqual(T.Traits.Concat(new[] { TraitId.PowderContagion, TraitId.PowderContagionSpread }));
            bool ret = UnitCatalog.KataTDR.Traits.SequenceEqual(UnitCatalog.KataH324.Traits.Append(TraitId.ThunderPowderReturn)) && UnitCatalog.KuguTDR.Traits.SequenceEqual(UnitCatalog.KuguH324.Traits.Append(TraitId.ThreadPowderFlow));
            bool tpad = UnitCatalog.TouTPaDry.Traits.SequenceEqual(T.Traits.Concat(new[] { TraitId.PowderContagion, TraitId.PowderContagionDry }));
            tpb = tpb && tpad;
            var defs = new[] { UnitCatalog.TouTDa, UnitCatalog.TouTDb, UnitCatalog.TouTDaB, UnitCatalog.TouTDbB, UnitCatalog.TouTPa, UnitCatalog.TouTPb, UnitCatalog.TouTPaDry };
            bool stats = defs.All(d => d.Id == "tou" && d.MaxHp == T.MaxHp && d.Attack == T.Attack && d.Speed == T.Speed);
            bool text = UnitCatalog.TouTDa.PlusText == T.PlusText + UnitCatalog.TouDenseText && UnitCatalog.TouTDa.MinusText.Contains(UnitCatalog.TouLeakDenseClause, StringComparison.Ordinal)
                && T.MinusText.Contains(UnitCatalog.TouLeakClause, StringComparison.Ordinal) && UnitCatalog.TouTPa.MinusText == T.MinusText
                && UnitCatalog.KataTDR.PlusText == UnitCatalog.KataH324.PlusText + UnitCatalog.KataPowderReturnText && UnitCatalog.KuguTDR.PlusText == UnitCatalog.KuguH324.PlusText + UnitCatalog.KuguPowderFlowText;
            var cards = new[] { TraitId.PowderDense, TraitId.PowderDenseKeep, TraitId.PowderDenseBurn, TraitId.ThunderPowderReturn, TraitId.ThreadPowderFlow, TraitId.PowderContagion, TraitId.PowderContagionSpread, TraitId.PowderContagionDry };
            bool outside = defs.Append(UnitCatalog.KataTDR).Append(UnitCatalog.KuguTDR).All(d => !UnitCatalog.Everyone.Contains(d))
                && UnitCatalog.Everyone.Select(PinTKK324).All(d => !d.Traits.Intersect(cards).Any()) && UnitCatalog.All.Count == 52;   // 第325期: 規定のトウ ／ カタ ／ クグが札を持つようになった——第324期の規定（`PinTKK324`）で見る
            Expect("版: TD-a ／ TD-b ／ TD-a焼 ／ TD-b焼 ／ TP-a ／ TP-b ／ TP-a′ ＝ 規定のトウの末尾に札・カタ ／ クグの版 ＝ 規定の末尾に返す札・文面（叩き台）・数値は規定のまま・`Everyone` の外・規定の駒は札を持たない",
                tda && tdb && tdab && tdbb && tpa && tpb && ret && stats && text && outside, $"TD {tda}/{tdb}/{tdab}/{tdbb}・TP {tpa}/{tpb}・返す {ret}・数値 {stats}・文面 {text}・外 {outside}");
        }

        // ---- TD: 手番の頭に帯電していた駒だけ +1（主目標 ／ 舞う先 ／ 漏れ先・敵味方）
        //      敵: 前1（主目標・帯電）・中央（舞う先・帯電）・後1（舞う先・乾いている）／ 味方: 中央 トウ・前1（帯電）・後3（乾いている）
        foreach (var (def, tag, on) in new[] { (UnitCatalog.TouH324, "規定", false), (UnitCatalog.TouTDa, "TD-a", true) })
        {
            var ctx = Ctx(Formation.Build(center: def, front1: Mate, back3: Mate), Formation.Build(front1: Wall, center: Wall, back1: Wall), out var p, out var e);
            var tou = Seat(p, 2); var af = Seat(p, 0); var ab = Seat(p, 4);
            var ef = Seat(e, 0); var ec = Seat(e, 2); var eb = Seat(e, 3);
            ctx.MarkShock(ef, ef); ctx.MarkShock(ec, ec); ctx.MarkShock(af, af);
            ctx.TakeTurn(tou);
            int[] d = { ctx.DensityOf(ef), ctx.DensityOf(ec), ctx.DensityOf(eb), ctx.DensityOf(af), ctx.DensityOf(ab) };
            var t = Tal(ctx, "tou");
            bool ok = on ? d.SequenceEqual(new[] { 1, 1, 0, 1, 0 }) && t.TdGainFoe == 2 && t.TdGainAlly == 1 : d.All(x => x == 0) && t.TdGainFoe + t.TdGainAlly == 0;
            bool shocks = Shock(ef) > 0 && Shock(ec) > 0 && Shock(eb) > 0 && Shock(af) > 0 && Shock(ab) > 0;
            bool fell = H(t.PwFellShocked, 0) == 1 && H(t.PwFellShocked, 1) == 1 && H(t.PwFellShocked, 2) == 1 && H(t.PwFell, 1) == 2 && H(t.PwFell, 2) == 2;
            Expect($"{tag}: 手番の頭に帯電していた駒にだけ濃さ +1（主目標 前1 ／ 舞う先 中央 ／ 漏れ先 味方の前1）・乾いていた駒（舞う先 後1 ／ 漏れ先 味方の後3）は帯電するだけ{(on ? "" : "（規定は濃さが付かない）")}",
                ok && shocks && fell, $"濃さ 前1 {d[0]}・中央 {d[1]}・後1 {d[2]}・味方 前1 {d[3]}・後3 {d[4]}・付いた 敵 {t.TdGainFoe} ／ 味 {t.TdGainAlly}・頭に帯電 {string.Join("/", t.PwFellShocked ?? Array.Empty<long>())}");
        }

        // ---- TD: 主目標がトウの一撃で弾けても、頭に帯電していれば +1（弾けた後に粉で帯電し直す）
        {
            var ctx = Ctx(Formation.Build(back1: UnitCatalog.TouTDa), Formation.Build(front1: Wall), out var p, out var e);
            var tou = p[0]; var ef = e[0];
            ctx.MarkShock(ef, ef);
            int from = ctx.Events.Count;
            ctx.TakeTurn(tou);
            Expect("TD-a: トウの一撃で主目標の感電が弾けても、手番の頭に帯電していれば濃さ +1（弾けた後に粉で帯電し直す）",
                Spent(ctx, ef, from) == 1 && Shock(ef) > 0 && ctx.DensityOf(ef) == 1, $"弾けた {Spent(ctx, ef, from)}・感電 {Shock(ef)}・濃さ {ctx.DensityOf(ef)}");
        }

        // ---- TD: 放電 8 ×（1 ＋ n）・TD-a は弾けで 0 ／ TD-b は残る・焼は本人に 8 × n（敵の側と味方の側）
        foreach (var (def, tag, keep, burn) in new[] { (UnitCatalog.TouTDa, "TD-a", false, false), (UnitCatalog.TouTDb, "TD-b", true, false), (UnitCatalog.TouTDaB, "TD-a焼", false, true), (UnitCatalog.TouTDbB, "TD-b焼", true, true) })
        {
            // 2回の手番で 前1 の濃さを 2 にする（毎手番の頭に帯電している）。乾いた隣は 後1 だけ（中央は空席）。
            var ctx = Ctx(Formation.Build(back1: def, front1: Mate), Formation.Build(front1: Wall, back1: Wall), out var p, out var e);
            var tou = Seat(p, 3); var af = Seat(p, 0); var ef = Seat(e, 0); var eb = Seat(e, 3);
            UnitState hitter = af;
            ctx.MarkShock(ef, ef); ctx.MarkShock(af, af);
            ctx.TakeTurn(tou);   // 前1: 弾けて（濃さ 0 の放電 8）粉で帯電し直し 濃さ 1 ／ 味方の前1: 漏れで濃さ 1
            TurnProp.SetValue(ctx, 2);
            ctx.TakeTurn(tou);   // 前1: 濃さ 1 の放電 16 → 濃さ（TD-a は 0 → 粉で 1 ／ TD-b は 1 → 2）
            int nE = ctx.DensityOf(ef), nA = ctx.DensityOf(af);
            int from = ctx.Events.Count;
            ctx.ApplyDamage(ef, 1, hitter);   // 敵の前1 を弾けさせる
            var disE = DisAmounts(ctx, ef, from);
            int afterE = ctx.DensityOf(ef);
            int selfE = Burned(ctx, ef, from);
            int from2 = ctx.Events.Count;
            ctx.ApplyDamage(af, 1, ef);       // 味方の前1 を弾けさせる（敵の一撃）
            var disA = DisAmounts(ctx, af, from2);
            int afterA = ctx.DensityOf(af);
            int selfA = Burned(ctx, af, from2);
            int expE = 8 * (1 + nE), expA = 8 * (1 + nA);
            bool ok = disE.Count >= 1 && disE.All(a => a == expE) && disA.Count >= 1 && disA.All(a => a == expA)
                && afterE == (keep ? nE : 0) && afterA == (keep ? nA : 0)
                && selfE == (burn ? 8 * nE : 0) && selfA == (burn ? 8 * nA : 0);
            Expect($"{tag}: 濃さ n の駒の放電は隣1体ずつに 8 ×（1 ＋ n）（敵 ／ 味方）・弾けたら濃さ {(keep ? "は残る" : "0")}・本人を焼く {(burn ? "8 × n" : "なし")}",
                ok, $"敵 n {nE}: 放電 {string.Join(",", disE)}（期待 {expE}）・後 {afterE}・本人 {selfE} ／ 味方 n {nA}: 放電 {string.Join(",", disA)}（期待 {expA}）・後 {afterA}・本人 {selfA}");
        }

        // ---- 返す: カタの雷で濃さが最初の的へ全部移る（TD-0 のカタは返さない）
        foreach (var (kata, tag, back) in new[] { (UnitCatalog.KataTDR, "カタの版", true), (UnitCatalog.KataH324, "規定のカタ（TD-0）", false) })
        {
            var ctx = Ctx(Formation.Build(center: UnitCatalog.TouTDb, front1: kata), Formation.Build(front1: Wall), out var p, out var e);
            var tou = Seat(p, 2); var k = Seat(p, 0); var ef = e[0];
            ctx.MarkShock(k, k);
            ctx.TakeTurn(tou);               // 漏れでカタの濃さ 1（敵の前1 は頭に乾いている → 帯電だけ）
            int k0 = ctx.DensityOf(k), e0 = ctx.DensityOf(ef);
            TurnProp.SetValue(ctx, 2);
            ctx.TakeTurn(k);                 // 雷: 前1（帯電）に落ちる
            int k1 = ctx.DensityOf(k), e1 = ctx.DensityOf(ef);
            var kt = Tal(ctx, "kata");
            bool ok = k0 == 1 && e0 == 0 && (back ? k1 == 0 && e1 == 1 && kt.TdKataMoves == 1 && kt.TdKataMoved == 1 : k1 == 1 && e1 == 0 && kt.TdKataMoves == 0);
            Expect($"返す（{tag}）: 雷を落とすと、カタ自身の濃さが雷の最初の的へ{(back ? "全部移る" : "移らない（返さない）")}", ok, $"カタ {k0} → {k1}・的 {e0} → {e1}・移した {kt.TdKataMoves}（{kt.TdKataMoved}）");
        }

        // ---- 返す: クグの濃さは組み付いた敵へ流れる（組み付いていなければクグに付く・TD-0 のクグは流さない）
        foreach (var (kugu, tag, flow, held) in new[] { (UnitCatalog.KuguTDR, "クグの版・組み付き", true, true), (UnitCatalog.KuguTDR, "クグの版・組み付いていない", true, false), (UnitCatalog.KuguH324, "規定のクグ（TD-0）・組み付き", false, true) })
        {
            var ctx = Ctx(Formation.Build(center: UnitCatalog.TouTDb, front1: kugu), Formation.Build(front1: Wall, front3: Wall), out var p, out var e);
            var tou = Seat(p, 2); var k = Seat(p, 0); var g = Seat(e, 1);
            if (held) k.SetCounter(GrappleTrait.TargetKey, g.InstanceId + 1);
            ctx.MarkShock(k, k);
            ctx.TakeTurn(tou);
            int nk = ctx.DensityOf(k), ng = ctx.DensityOf(g);
            var gt = Tal(ctx, "kugu");
            bool ok = flow && held ? nk == 0 && ng == 1 && gt.TdKuguFlowed == 1 : nk == 1 && ng == 0 && gt.TdKuguFlowed == 0;
            Expect($"返す（{tag}）: クグに付く濃さは{(flow && held ? "組み付いた敵へ流れる" : "クグに付く")}", ok, $"クグ {nk}・組み付いた敵 {ng}・流した {gt.TdKuguFlowed}・自分に {gt.TdKuguKept}");
        }

        // ---- TP: 粉の印のある駒の放電は帯電していない隣を帯電させる・新しく帯電した駒は同じ連鎖で弾けない・TP-b は印も付く
        //      敵: 前1（乾いている）・中央（粉の印 ＋ 帯電）・後1（帯電・印なし）。中央 → 前1 に伝染、後1 が弾けて 前1 に放電が戻っても前1 は弾けない。
        foreach (var (def, tag, on, spread) in new[] { (UnitCatalog.TouH324, "規定", false, false), (UnitCatalog.TouTPa, "TP-a", true, false), (UnitCatalog.TouTPb, "TP-b", true, true) })
        {
            var ctx = Ctx(Formation.Build(back3: def, front1: Mate), Formation.Build(front1: Wall, center: Wall, back1: Wall), out var p, out var e);
            var tou = Seat(p, 4); var ef = Seat(e, 0); var ec = Seat(e, 2); var eb = Seat(e, 3);
            ctx.MarkShock(ec, ec); ctx.MarkShock(eb, eb);
            ctx.PowderFell(tou, ec, PowderRoute.Main);   // 印だけ付ける（トウの手番の外 ＝ 濃さは付かない）
            int from = ctx.Events.Count;
            ctx.ApplyDamage(ec, 1, Seat(p, 0));
            var t = Tal(ctx, "tou");
            bool ok = on
                ? Shock(ef) > 0 && Spent(ctx, ef, from) == 0 && Spent(ctx, ec, from) == 1 && Spent(ctx, eb, from) == 1 && t.TpNewFoe == 1 && t.TpFreshSkip == 1 && ctx.PowderMarked(ef) == spread && t.TpMarkSpread == (spread ? 1 : 0)
                : Shock(ef) == 0 && Spent(ctx, ec, from) == 1 && Spent(ctx, eb, from) == 1 && t.TpNewFoe == 0;
            Expect($"{tag}: 粉の印のある駒（中央）の放電は{(on ? "帯電していない隣（前1）を帯電させる・その前1 は同じ連鎖では弾けない（後1 の放電が戻っても）" : "帯電していない隣を帯電させない")}{(spread ? "・前1 にも粉の印" : "")}",
                ok, $"前1 感電 {Shock(ef)}・弾けた 前1 {Spent(ctx, ef, from)} ／ 中央 {Spent(ctx, ec, from)} ／ 後1 {Spent(ctx, eb, from)}・伝染 {t.TpNewFoe}・同じ連鎖で止めた {t.TpFreshSkip}・前1 の印 {ctx.PowderMarked(ef)}");
        }

        // ---- TP-a′（参考）: 同じ連鎖で先に弾けた駒には移らない ／ TP-a は帯電し直す
        //      敵: 前1（帯電・印）・中央（帯電・印）・後1（乾いている）。前1 を弾けさせる → 中央が弾ける → 中央の放電が 前1（弾けたばかり）と 後1（乾いている）に届く。
        foreach (var (def, tag, dry) in new[] { (UnitCatalog.TouTPa, "TP-a", false), (UnitCatalog.TouTPaDry, "TP-a′", true) })
        {
            var ctx = Ctx(Formation.Build(back3: def, front1: Mate), Formation.Build(front1: Wall, center: Wall, back1: Wall), out var p, out var e);
            var tou = Seat(p, 4); var ef = Seat(e, 0); var ec = Seat(e, 2); var eb = Seat(e, 3);
            ctx.MarkShock(ef, ef); ctx.MarkShock(ec, ec);
            ctx.PowderFell(tou, ef, PowderRoute.Main); ctx.PowderFell(tou, ec, PowderRoute.Main);
            ctx.ApplyDamage(ef, 1, Seat(p, 0));
            var t = Tal(ctx, "tou");
            bool ok = Shock(eb) > 0 && (dry ? Shock(ef) == 0 && t.TpReArm == 0 : Shock(ef) > 0 && t.TpReArm >= 1);
            Expect($"{tag}: 乾いた後1 は伝染で帯電する・中央の放電が届いた前1（同じ連鎖で先に弾けた）は{(dry ? "帯電させない" : "帯電し直す")}", ok, $"前1 {Shock(ef)}・後1 {Shock(eb)}・伝染 {t.TpNewFoe}・帯電し直した {t.TpReArm}");
        }

        // ---- TP: 敵味方を問わない（味方の側で伝染）
        {
            var ctx = Ctx(Formation.Build(center: UnitCatalog.TouTPa, front1: Mate, back1: Mate), Formation.Build(front1: Wall), out var p, out var e);
            var tou = Seat(p, 2); var af = Seat(p, 0); var ab = Seat(p, 3);
            ctx.MarkShock(af, af);
            ctx.PowderFell(tou, af, PowderRoute.Leak);
            ctx.ApplyDamage(af, 1, e[0]);
            var t = Tal(ctx, "tou");
            Expect("TP-a: 粉の印のある味方（前1）が弾けると、帯電していない隣の味方（後1・中央のトウ）も帯電する（敵味方を問わない）",
                Shock(ab) > 0 && Shock(tou) > 0 && t.TpNewAlly == 2, $"後1 {Shock(ab)}・トウ {Shock(tou)}・伝染 味方 {t.TpNewAlly}");
        }

        // ---- 戦の中で（代表台 × 近衛 ／ ボス・seed 0..19）: 版ごとに帳簿と台本が一致・verbose の有無で勝敗 ／ 決着T が同じ
        {
            var (bn, bf) = Boards()[0];
            foreach (var w in new[] { WaveOf("guard"), WaveOf("boss") })
                foreach (var v in Vers.Append(Stack))
                {
                    int diff = 0; long gainT = 0, gainEv = 0, tpT = 0, tpEv = 0, dens = 0;
                    for (int sd = 0; sd < 20; sd++)
                    {
                        var f = Apply(bf, v);
                        var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                        var t = r1.TallyByUnit["tou"];
                        gainT += t.TdGainFoe + t.TdGainAlly; tpT += t.TpNewFoe + t.TpNewAlly;
                        gainEv += r1.Events.Count(x => x.Kind == BattleEventKind.ShockGauge && x.Text is ShockGaugeLabels.Dense or ShockGaugeLabels.DenseFlow);
                        tpEv += r1.Events.Count(x => x.Kind == BattleEventKind.ShockGauge && x.Text == ShockGaugeLabels.Contagion);
                        dens += r1.Events.Count(x => x.Kind == BattleEventKind.ShockGauge && x.Text is string tx && MemoLabels.Contains(tx));
                    }
                    bool td = v.Tou.Traits.Contains(TraitId.PowderDense), tp = v.Tou.Traits.Contains(TraitId.PowderContagion);
                    // ボスは勇者1体（隣がいない）なので伝染は起きなくてよい（近衛では起きる）。
                    bool ok = diff == 0 && gainT == gainEv && tpT == tpEv && (td ? gainT > 0 : gainT == 0) && (tp ? tpT > 0 || w.Boss : tpT == 0) && (td || tp || dens == 0);
                    Expect($"{bn} × {w.Name} × {v.Name}（seed 0..19）: 濃さ ／ 伝染の帳簿と台本が一致・{(td || tp ? (tp && w.Boss ? "濃さは起きる（伝染は隣がいなければ 0 でよい）" : "起きる") : "規定は第323期の出来事を1件も出さない")}・verbose の有無で勝敗 ／ 決着T が同じ",
                        ok, $"濃さ {gainT}（台本 {gainEv}）・伝染 {tpT}（台本 {tpEv}）・第323期の出来事 {dens}・違い {diff}");
                }
        }

        // ---- 乱数を新たに引いていない（`PickOne` の呼び出し数が HEAD と同じ）
        {
            int Now(string f) => File.Exists(f) ? File.ReadAllText(f).Split("PickOne(").Length - 1 : -1;
            int engine = Now("BattleCore/BattleEngine.cs"), traits = Now("BattleCore/Traits.cs");
            string Head(string f) { try { var psi = new System.Diagnostics.ProcessStartInfo("git", $"show HEAD:{f}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 }; using var pr = System.Diagnostics.Process.Start(psi)!; string s = pr.StandardOutput.ReadToEnd(); pr.WaitForExit(); return s; } catch { return ""; } }
            int he = Head("BattleCore/BattleEngine.cs").Split("PickOne(").Length - 1, ht = Head("BattleCore/Traits.cs").Split("PickOne(").Length - 1;
            Expect("`ctx.PickOne` を新たに使っていない（BattleEngine.cs ／ Traits.cs の `PickOne(` の数が HEAD と同じ）", engine == he && traits == ht, $"engine {he} → {engine}・traits {ht} → {traits}");
        }

        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
