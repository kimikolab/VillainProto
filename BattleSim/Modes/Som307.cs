using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S293 = Shock293Diag;

// =====================================================================================
// som307 —— 第307期「ソムを感電軸のヒーラーに（連鎖の恵み・SH-a ／ SH-b）」。
// 指示書は design/PHASE307_SOM_SPARK_SPEC.md ／ 報告は design/PHASE307_SOM_SPARK.md。
//
//     dotnet run --project BattleSim -c Release 0 som307 p0          # Phase 0（§3）: 量の決め方・光の燃料の内訳と推移・粛の第2波で止まる見込み・ベニの台
//
// 規定の駒（第306期の HEAD）のまま組む。ソムの版は旧の規定のソム（`UnitCatalog.SomH307`）と同じ席で差し替える。
// 第308期: 規定のソムが SH-a になったので、台のソムは旧の規定（`SomH307`・`Common.Pin308`）に固定した。
// =====================================================================================
static partial class Som307Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": P0(); return;
            default: RunMore(mode, args, A); return;
        }
    }

    static partial void RunMoreCore(string mode, string[] args, Func<int, string, string> a, ref bool done);
    static void RunMore(string mode, string[] args, Func<int, string, string> a)
    {
        bool done = false;
        RunMoreCore(mode, args, a, ref done);
        if (!done) Console.WriteLine("som307: モードは p0 / compare / boards / gridboss / grid / check / log。");
    }

    // ---------------------------------------------------------------------------------
    // 波・台（測る前に固定）
    // ---------------------------------------------------------------------------------
    /// <summary>波: ボス規定形 ／ 近衛 ／ 大隊（第287期の作り）＋ 本編の5波（`EnemyCatalog.Stages`・`compare` と同じ倍率）。</summary>
    internal static readonly S287.Wave[] Waves = S287.Waves.Concat(EnemyCatalog.Stages.Select((s, i) =>
        new S287.Wave($"本編 第{i + 1}波", () => BattleEngine.Materialize(s.Enemy, BattleContext.EnemyTeam, BossRule.Default.Scale), false))).ToArray();
    internal static S287.Wave WaveOf(string n) => Waves.First(w => w.Name == (n switch
    {
        "boss" => "ボス", "guard" => "近衛", "bat" => "大隊", "1" => "本編 第1波", "2" => "本編 第2波", "3" => "本編 第3波", "4" => "本編 第4波", "5" => "本編 第5波", _ => n,
    }));
    internal const int Seeds = 200, CutSeeds = 20;

    internal static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    internal static UnitDef ByShort(string n) => UnitCatalog.All.First(d => Short(d) == n);
    internal static UnitDef[] Order(string s) => s.Split('・', StringSplitOptions.RemoveEmptyEntries).Select(ByShort).ToArray();
    internal static string OrderName(UnitDef[] o) => string.Join("・", o.Select(Short));
    internal static string Seats(Formation f) => string.Join("・", Enumerable.Range(0, 5).Select(i => f[i] is { } d ? Short(d) : "—"));
    static Formation Playtest(string n) => Pin308(Presets.Playtest.First(r => r.Name == n).F);
    static Formation Row(string n) => Pin308(CompareBuilds().First(r => r.Name == n).F);   // 第308期: 規定のソム（SH-a）は旧（`SomH307`）に固定

    internal const string WinBoard = "シガ・ゴルム・クグ・カタ・ツギ";

    /// <summary>代表台（指示書 §4-1 ＝ 第294期 §5-2 ／ 報告 §4-2 のソムの台）。席は出典のまま、差し替えは同じ席で（`FvSwap`）。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("感電 (シガ×カタ×ソム)", Row("感電 (シガ×カタ×ソム)")),
        ("雷の型 ドハ→ソム", FvSwap(Playtest("試遊・感電 雷の型"), UnitCatalog.Doha, UnitCatalog.SomH307)),
        ("感電 糸 ガルド→ソム", FvSwap(Playtest("試遊・感電 糸"), UnitCatalog.Gald, UnitCatalog.SomH307)),
        ("勝ち台 ゴルム→ソム", FvSwap(B283.Seat(Order(WinBoard)), UnitCatalog.Golm, UnitCatalog.SomH307)),
        ("勝ち台 ツギ→ソム", FvSwap(B283.Seat(Order(WinBoard)), UnitCatalog.Tsugi, UnitCatalog.SomH307)),
    };

    internal static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    internal static string F2(double x) => double.IsNaN(x) ? "—" : x.ToString("F2");
    internal static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    internal static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    internal static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F0");
    internal static string Pct(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1") + "%";

    // ---------------------------------------------------------------------------------
    // Phase 0（§3）
    // ---------------------------------------------------------------------------------
    const int TMax = 8;

    sealed class P0Acc
    {
        public long N, Wins, Turns5;
        // 光の燃料（ソムの敵の側で弾けた駒・ソムが生きている間）: 0 敵の駒 ／ 1 喚ばれたもの ／ 2 糸玉
        public long[] Fuel = new long[3];
        public long FuelDead;                       // ソムが倒れた後に敵の側で弾けた数（光にならない）
        public long[] FuelT = new long[TMax + 1];   // ターンごとの燃料（ソムが生きている間）
        public long[] ReachT = new long[TMax + 1];  // そのターンを迎えた戦の数
        public long Fuel5;                          // T1〜T5 の燃料
        public long Chains, ChainsHushed;           // 敵の側で弾けた連鎖（ソムが生きている間）／ うち粛が黙らせていた間
        public long TsugiOut5;                      // ツギが T1〜T5 に味方へ書いた量（板 ＋ 回復）
        public long AllyPops;
        public void Merge(P0Acc o)
        {
            N += o.N; Wins += o.Wins; Turns5 += o.Turns5; FuelDead += o.FuelDead; Fuel5 += o.Fuel5; Chains += o.Chains; ChainsHushed += o.ChainsHushed; TsugiOut5 += o.TsugiOut5; AllyPops += o.AllyPops;
            for (int i = 0; i < 3; i++) Fuel[i] += o.Fuel[i];
            for (int i = 0; i <= TMax; i++) { FuelT[i] += o.FuelT[i]; ReachT[i] += o.ReachT[i]; }
        }
    }

    /// <summary>
    /// Phase 0 の1戦（verbose の台本から）。弾けは <c>ShockSpent</c>（`Team` が弾けた駒の陣営）、連鎖の区切りは深さ 0 の弾け（`Slot` ＝ 深さ）。
    /// 喚ばれたもの ＝ ソムが呼んだ <c>Summon</c> の駒、糸玉 ＝ <c>SilkBall</c> の出来事の駒。粛が黙らせていた間 ＝ 粛の保持者が生きていて、砕け（`HushState` の `Shatter`）の前。
    /// </summary>
    static P0Acc FightP0(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var a = new P0Acc { N = 1, Wins = r.PlayerWon ? 1 : 0, Turns5 = Math.Min(5, r.Turns) };
        for (int t = 1; t <= Math.Min(TMax, r.Turns); t++) a.ReachT[t]++;
        UnitState? som = p.FirstOrDefault(u => u.Def.Id == "som"), tsugi = p.FirstOrDefault(u => u.Def.Id == "tsugi");
        var husher = e.Where(u => u.HasTrait(TraitId.Hush)).Select(u => u.InstanceId).ToHashSet();
        var summoned = new HashSet<int>(); var balls = new HashSet<int>();
        bool somAlive = som is not null, hushOn = husher.Count > 0, inChain = false, chainCounted = false;
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Summon when som is not null && ev.ActorId == som.InstanceId && ev.TargetId is int sid: summoned.Add(sid); break;
                case BattleEventKind.SilkBall when ev.TargetId is int bid: balls.Add(bid); break;
                case BattleEventKind.HushState when ev.Text == HushStateLabels.Shatter: hushOn = false; break;
                case BattleEventKind.Death when ev.TargetId is int d:
                    if (som is not null && d == som.InstanceId) somAlive = false;
                    if (husher.Remove(d) && husher.Count == 0) hushOn = false;
                    break;
                case BattleEventKind.Plank when tsugi is not null && ev.ActorId == tsugi.InstanceId && ev.Turn is >= 1 and <= 5 && (ev.Text == PlankLabels.Paste || ev.Text == PlankLabels.FirstAid):
                    a.TsugiOut5 += ev.Amount; break;
                case BattleEventKind.Heal when tsugi is not null && ev.ActorId == tsugi.InstanceId && ev.Turn is >= 1 and <= 5:
                    a.TsugiOut5 += ev.Amount; break;
                case BattleEventKind.ShockSpent:
                    if (ev.Slot == 0) { inChain = true; chainCounted = false; }
                    if (ev.Team == BattleContext.PlayerTeam) { a.AllyPops++; break; }
                    if (!somAlive) { a.FuelDead++; break; }
                    int id = ev.TargetId ?? -1;
                    a.Fuel[balls.Contains(id) ? 2 : summoned.Contains(id) ? 1 : 0]++;
                    if (ev.Turn <= TMax) a.FuelT[ev.Turn]++;
                    if (ev.Turn is >= 1 and <= 5) a.Fuel5++;
                    if (inChain && !chainCounted) { chainCounted = true; a.Chains++; if (hushOn) a.ChainsHushed++; }
                    break;
            }
        }
        return a;
    }

    static P0Acc MeasureP0(Formation f, S287.Wave w, int n)
    {
        var parts = new P0Acc[n];
        Parallel.For(0, n, i => parts[i] = FightP0(f, w, i));
        var all = new P0Acc();
        foreach (var x in parts) all.Merge(x);
        return all;
    }

    /// <summary>§3 の 1 の量（光1つ・味方1体あたり）＝ ツギの1ターン平均 ÷（弾けの1ターン平均 × 5）を整数に丸めた値（最低 1）。</summary>
    internal static int AmountFrom(double tsugiPerTurn, double popsPerTurn) => Math.Max(1, (int)Math.Round(tsugiPerTurn / (popsPerTurn * 5), MidpointRounding.AwayFromZero));

    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第307期 Phase 0（指示書 §3・規定の駒・seed 0..199・verbose）");
        Console.WriteLine();
        foreach (var (n, f) in Boards()) Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine($"- 元の勝ち台: {Seats(B283.Seat(Order(WinBoard)))}");

        // 1. 量の決め方
        var boss = Waves[0];
        var baseA = MeasureP0(B283.Seat(Order(WinBoard)), boss, Seeds);
        var swapA = MeasureP0(Boards().First(b => b.Name == "勝ち台 ツギ→ソム").F, boss, Seeds);
        double tsugi = (double)baseA.TsugiOut5 / baseA.Turns5, pops = (double)swapA.Fuel5 / swapA.Turns5;
        Console.WriteLine();
        Console.WriteLine("## 1. 量の決め方（ボス規定形）");
        Console.WriteLine();
        Console.WriteLine("| 量 | 値 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine($"| 元の勝ち台（{WinBoard}）の勝率 | {Pct(baseA.Wins, baseA.N)} |");
        Console.WriteLine($"| ツギが T1〜T5 に味方へ書いた量（板 ＋ 応急処置 ＋ 回復）の合計 ／ 迎えたターン | {baseA.TsugiOut5} ／ {baseA.Turns5} |");
        Console.WriteLine($"| **ツギの1ターン平均** | **{F2(tsugi)}** |");
        Console.WriteLine($"| ツギ → ソムの台の勝率 | {Pct(swapA.Wins, swapA.N)} |");
        Console.WriteLine($"| ツギ → ソムの台で T1〜T5 に敵の側で弾けた数（ソムが生きている間）／ 迎えたターン | {swapA.Fuel5} ／ {swapA.Turns5} |");
        Console.WriteLine($"| **弾けの1ターン平均** | **{F2(pops)}** |");
        Console.WriteLine($"| ツギ ÷（弾け × 5） | {F2(tsugi / (pops * 5))} |");
        Console.WriteLine($"| **量（整数に丸め・最低 1）** | **{AmountFrom(tsugi, pops)}** |");

        // 2. 光の燃料の内訳と推移
        Console.WriteLine();
        Console.WriteLine("## 2. 光の燃料（規定のソム・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("敵の側 ＝ ソムの敵の陣営で弾けた駒（ソムが生きている間）。ソムが倒れた後の弾けは別の列。T1〜T8 ＝ そのターンを迎えた戦で割った1ターンあたり。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 台 | 勝率 | 敵の側 計 | 敵の駒 ／ 喚ばれたもの ／ 糸玉 | ソムが倒れた後 | 味方の側で弾けた | " + string.Join(" | ", Enumerable.Range(1, TMax).Select(t => $"T{t}")) + " |");
        Console.WriteLine("|---|---|--:|--:|---|--:|--:|" + string.Concat(Enumerable.Range(1, TMax).Select(_ => "--:|")));
        var cells = new List<(S287.Wave W, string B, P0Acc A)>();
        foreach (var w in Waves)
            foreach (var (n, f) in Boards())
            {
                var a = MeasureP0(f, w, Seeds);
                cells.Add((w, n, a));
                long tot = a.Fuel.Sum();
                Console.WriteLine($"| {w.Name} | {n} | {Pct(a.Wins, a.N)} | {Per1(tot, a.N)} | {Per1(a.Fuel[0], a.N)} ／ {Per1(a.Fuel[1], a.N)} ／ {Per1(a.Fuel[2], a.N)} | {Per1(a.FuelDead, a.N)} | {Per1(a.AllyPops, a.N)} | "
                    + string.Join(" | ", Enumerable.Range(1, TMax).Select(t => Per(a.FuelT[t], a.ReachT[t]))) + " |");
            }

        // 3. 粛の第2波
        Console.WriteLine();
        Console.WriteLine("## 3. 粛の第2波（本編 第2波・HCD15）で SH-a が止まる見込み");
        Console.WriteLine();
        Console.WriteLine("連鎖 ＝ 敵の側の弾けを1つ以上含む連鎖（ソムが生きている間）。粛の間 ＝ 粛の伝令が生きていて沈黙が砕ける前（SH-a の光はこの間は止まる・止まった数は粛のひびに入る）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 連鎖 ／ 戦 | うち粛の間 | 割合 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var c in cells.Where(c => c.W.Name == "本編 第2波"))
            Console.WriteLine($"| {c.B} | {Pct(c.A.Wins, c.A.N)} | {Per(c.A.Chains, c.A.N)} | {Per(c.A.ChainsHushed, c.A.N)} | {Pct(c.A.ChainsHushed, c.A.Chains)} |");

        // 4. ベニ
        Console.WriteLine();
        Console.WriteLine("## 4. ベニのいる台");
        Console.WriteLine();
        bool any = false;
        foreach (var (n, f) in Boards())
            if (f.Occupied().Any(o => o.Def.Id == "beni")) { any = true; Console.WriteLine($"- {n}: ベニあり"); }
        if (!any) Console.WriteLine("- 代表台 5 台にベニはいない（反転は代表台では起きない）。");
        var fire = FvSwap(Playtest("試遊・感電 火の型"), UnitCatalog.Sora, UnitCatalog.SomH307);
        var bf = BattleEngine.Materialize(fire, BattleContext.PlayerTeam);
        var beni = bf.First(u => u.Def.Id == "beni");
        int adj = bf.Count(u => u != beni && FormationRules.AreAdjacent(u, beni));
        int inv = adj + (InverseTrait.IncludesSelf ? 1 : 0);
        Console.WriteLine($"- 参考: 試遊・感電 火の型 ソラ → ソム（{Seats(fire)}）では、ベニ（{beni.Slot} 番の席）の隣の味方は {adj} ／ {bf.Count - 1} 体{(InverseTrait.IncludesSelf ? "・ベニ自身も反転の内側（`InverseTrait.IncludesSelf`）" : "")}——光が味方全員に降ると、全員が生きている間は **{inv} ／ {bf.Count} 体ぶんの癒しが傷に変わる**。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
