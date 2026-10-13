using System.Reflection;
using BattleCore;
using static Common;
using S287 = Shock287Diag;
using S316 = Shiga316Diag;
using S321 = Shiga321Diag;

// =====================================================================================
// hisa321 —— 第321期 段2「ヒサの号令を『溜まったら吐く』」（HQ-a ／ HQ-b）。
// 指示書は design/PHASE321_SHIGA_CHARGE_HISA_BURST_SPEC.md ／ 報告は design/PHASE321_SHIGA_CHARGE_HISA_BURST.md。
//
//     dotnet run --project BattleSim -c Release 0 hisa321 p0                # §2-3 Phase 0（規定のヒサ）: 溢れ ／ 玉 ／ 捨てた溢れ ／ 手番の号令の層 ／ 羽・叫びが羽の割り込みの中で起きる割合
//     dotnet run --project BattleSim -c Release 0 hisa321 boards [版,…]     # §2-5 ／ §2-6: 代表台 5 × 8 波 × 版（規定 ／ HQ-a ／ HQ-b）と対照（ヒサ → ドルガ）
//     dotnet run --project BattleSim -c Release 0 hisa321 compare [版,…]    # §2-7: `compare` 64 行 × 版（(G2) つき・基準は規定）
//     dotnet run --project BattleSim -c Release 0 hisa321 memo <台の一部> <波> <seed> <版>   # §3: 吐き出しの並び（叫び → まとまりが閉じる → 号令 → 標 → 羽）
//     dotnet run --project BattleSim -c Release 0 hisa321 check             # §4 自己検査
//
// 版の名前（ASCII）: base（規定）／ qa（HQ-a）／ qb（HQ-b）。波: boss ／ guard ／ bat ／ 1..5。
// **予測のファイルが無ければ本測定（boards ／ compare）を走らせない**（R397・段1 と同じ `.tmp/p321/predict.md`）。
// =====================================================================================
static class Hisa321Diag
{
    const int Seeds = 200;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" && !File.Exists(S321.PredictFile))
        {
            Console.WriteLine($"hisa321: 予測のファイル `{S321.PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": BoardsAll(A(3, "")); return;
            case "compare": CompareAll(A(3, "")); return;
            case "memo": Memo(A(3, "ボス台"), A(4, "boss"), int.Parse(A(5, "0")), A(6, "qa")); return;
            case "check": Check(); return;
            default: Console.WriteLine("hisa321: モードは p0 / boards / compare / memo / check。"); return;
        }
    }

    static string Short(UnitDef d) => Som307Diag.Short(d);
    static string F1(double x) => Som307Diag.F1(x);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("N0");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);

    internal sealed record Ver(string Name, string Ascii, UnitDef Def);
    internal static readonly Ver[] Vers =
    {
        new("規定", "base", UnitCatalog.Hisa),
        new("HQ-a", "qa", UnitCatalog.HisaHQa),
        new("HQ-b", "qb", UnitCatalog.HisaHQb),
        new("HQ-a3", "qa3", UnitCatalog.HisaHQa3),
        new("HQ-a6", "qa6", UnitCatalog.HisaHQa6),
        new("HQ-a9", "qa9", UnitCatalog.HisaHQa9),
        new("HQ-b3", "qb3", UnitCatalog.HisaHQb3),
        new("HQ-b6", "qb6", UnitCatalog.HisaHQb6),
        new("HQ-b9", "qb9", UnitCatalog.HisaHQb9),
    };
    static Ver V(string n) => Vers.First(v => v.Name == n || v.Ascii == n);
    static Ver[] Pick(string only) => (string.IsNullOrEmpty(only) ? Vers : new[] { Vers[0] }.Concat(only.Split(',').Select(V).Where(v => v != Vers[0]))).ToArray();
    static Formation Apply(Formation f, UnitDef d) => S316.SwapId(f, "hisa", d);

    /// <summary>波: ボス ／ 近衛 ／ 大隊 ／ 本編の5波（段1 の 10 波から九体を抜いた 8 波）。</summary>
    static readonly S287.Wave[] Waves = S321.Waves.Where(w => !w.Name.StartsWith("九体", StringComparison.Ordinal)).ToArray();
    static S287.Wave WaveOf(string n) => S321.WaveOf(n);

    /// <summary>§2-5 の代表台（試遊・標のヒサ入り4台 ＋ 道中（見境改・ヒサはいない——版で動かないことの確かめ））。</summary>
    static (string Name, Formation F)[] Boards() => new[] { "試遊・標 守り型", "試遊・標 ボス台", "試遊・標 循環", "試遊・標 三人組", "試遊・標 道中" }
        .Select(n => (n.Replace("試遊・", ""), Presets.Playtest.First(r => r.Name == n).F)).ToArray();

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    sealed class X
    {
        public long N, Wins, WinT, HisaN, Overflow, Dropped, DropEvents, TurnCmds, TurnLayers, Bursts, BurstLayers, BurstHushed, BurstBlocked, BurstNoTarget, BurstGuard,
            CmdFeathers, ChainSkipped, MfChainSkipped, Feathers, MisaDmg, ZanDmg, RallyFires, RoundMax, Rounds10, RallyInFeather, RallyAll, TurnMax, MarkPeak, BurstCapped;
        public long[] BurstHist = new long[13];
        public void Merge(X o)
        {
            foreach (var f in typeof(X).GetFields())
            {
                if (f.FieldType != typeof(long)) continue;
                long a = (long)f.GetValue(this)!, b = (long)f.GetValue(o)!;
                f.SetValue(this, f.Name is "RoundMax" or "MarkPeak" ? Math.Max(a, b) : a + b);
            }
            for (int i = 0; i < BurstHist.Length; i++) BurstHist[i] += o.BurstHist[i];
        }
    }

    static X Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var x = new X { N = 1 };
        if (r.PlayerWon) { x.Wins = 1; x.WinT = r.Turns; }
        UnitState? hisa = p.FirstOrDefault(u => u.Def.Id == "hisa"), misa = p.FirstOrDefault(u => u.Def.Id == "tome");
        if (r.TallyByUnit.TryGetValue("hisa", out var ht) && hisa is not null)
        {
            x.HisaN = 1;
            x.Overflow = ht.RallyOverflow; x.Dropped = ht.CommandDropped; x.DropEvents = ht.CommandDropEvents; x.RallyFires = ht.RallyFires;
            x.Bursts = ht.BurstFires; x.BurstLayers = ht.BurstLayers; x.BurstHushed = ht.BurstHushed; x.BurstBlocked = ht.BurstBlocked; x.BurstNoTarget = ht.BurstNoTarget; x.BurstGuard = ht.BurstGuard;
            x.TurnLayers = ht.CommandLayers - ht.BurstLayers; x.TurnCmds = ht.CommandFires - ht.BurstFires;
            if (ht.BurstHist is { } bh) for (int i = 0; i < Math.Min(bh.Length, x.BurstHist.Length); i++) x.BurstHist[i] = bh[i];
            x.MarkPeak = ht.CommandPeak; x.BurstCapped = ht.BurstCapped;
        }
        if (r.TallyByUnit.TryGetValue("tome", out var mt))
        {
            x.CmdFeathers = mt.CommandFeathers; x.ChainSkipped = mt.CmdChainSkipped; x.MfChainSkipped = mt.MfChainSkipped;
            x.Feathers = mt.MfShotsFoe + mt.MfShotsAlly; x.MisaDmg = mt.DamageToEnemy;
        }
        if (r.TallyByUnit.TryGetValue("zan", out var zt)) x.ZanDmg = zt.DamageToEnemy;
        // 台本: 号令の層（1巡あたり）・叫びが羽の割り込みの中で起きたか（直前の羽の発射と同じ割り込みの中＝ FeatherMark の後、次の手番の頭より前の MarkRally）
        if (hisa is not null)
        {
            var perTurn = new Dictionary<int, long>();
            bool inFeather = false;
            foreach (var ev in r.Events)
            {
                switch (ev.Kind)
                {
                    case BattleEventKind.Command when ev.ActorId == hisa.InstanceId: perTurn[ev.Turn] = perTurn.GetValueOrDefault(ev.Turn) + ev.Amount; break;
                    case BattleEventKind.FeatherMark: inFeather = true; break;
                    case BattleEventKind.Attack when misa is null || ev.ActorId != misa.InstanceId || !ev.Reaction: inFeather = false; break;
                    case BattleEventKind.MarkRally when ev.ActorId == hisa.InstanceId:
                        x.RallyAll++; if (inFeather) x.RallyInFeather++; break;
                }
            }
            x.RoundMax = perTurn.Count == 0 ? 0 : perTurn.Values.Max();
            x.Rounds10 = perTurn.Values.Count(v => v >= 10);
        }
        return x;
    }

    static X Measure(Formation f, S287.Wave w, int n = Seeds)
    {
        var parts = new X[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, i));
        var all = new X();
        foreach (var x in parts) all.Merge(x);
        return all;
    }

    static int WinsLite(Formation f, S287.Wave w, int n = Seeds)
    {
        int wins = 0;
        Parallel.For(0, n, sd => { if (BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
        return wins;
    }

    // ---------------------------------------------------------------------------------
    // §2-3 Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第321期 段2 Phase 0 —— 規定のヒサの号令（玉 3 つまで・手番に吐く）（seed 0..199・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("溢れ ＝ 叫びの回復のうち満タンで入らなかった量（20 で玉1つ）。捨てた ＝ 玉 3 つ（60）を越えて入らなかった溢れ。見込み ＝ 捨てずに全部層にしたときの層（溢れ ÷ 20・HQ-a は 3 の倍数に切り下げた端数が残る）。");
        Console.WriteLine("叫びが羽の中 ＝ ミサの羽（`FeatherMark`）の発射の後・次の手番の攻撃より前に出た叫び（`MarkRally`）の割合——いまの号令はヒサの手番なので羽の中で刻むことは無いが、吐き出しを「叫びの直後」にするとこの割合が羽を呼ばない層になる。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 溢れ | 玉（溢れ ÷ 20） | 捨てた溢れ（回） | 手番の号令 回 ／ 層 | 号令が呼んだ羽 | 層の見込み HQ-a ／ HQ-b | 叫び ／ うち羽の中 |");
        Console.WriteLine("|---|---|--:|--:|--:|---|---|--:|---|---|");
        foreach (var (bn, bf) in Boards())
            foreach (var w in Waves)
            {
                var x = Measure(bf, w);
                long kept = x.Overflow - x.Dropped;
                Console.WriteLine($"| {bn} | {w.Name} | {Pct(x.Wins, x.N)} | {Per0(x.Overflow, x.N)} | {Per(x.Overflow / 20, x.N)} | {Per0(x.Dropped, x.N)}（{Per(x.DropEvents, x.N)}） | {Per(x.TurnCmds, x.N)} ／ {Per(x.TurnLayers, x.N)} | {Per(x.CmdFeathers, x.N)} | {Per(x.Overflow / 60 * 3, x.N)} ／ {Per(x.Overflow / 20, x.N)} | {Per(x.RallyAll, x.N)} ／ {Pct(x.RallyInFeather, x.RallyAll)}% |");
            }
        Console.WriteLine();
        Console.WriteLine("## 「まとまりが閉じた直後」を取る口");
        Console.WriteLine();
        Console.WriteLine("攻撃のまとまりは `BundlePush` ／ `BundlePop`（手番 `TakeTurnFramed` ・反撃 `Reaction` ・割り込み `Interrupt` の3箇所）で積み、叫びは `BundlePop` の中（そのまとまりの回復）で出る。");
        Console.WriteLine("第301期の HL-i（`CommandNowFire`）はこの `BundlePop` の中で刻んだ——羽の割り込み（`DrainFeatherMarks` → `Interrupt`）の中で閉じたまとまりでは、刻んだ層が `_mfFiring` の間に書かれ、`QueueFeatherMark` が「羽の連鎖の中で書かれた標は控えない」（羽が羽を呼ばない）で落とした。");
        Console.WriteLine("→ 版の吐き出しは、3箇所の枠が閉じた後の `DrainFeatherMarks` の直前（`CommandBurst`）で、**まとまりの積みが空（`_bundles.Count == 0`）・羽の発射の外・割り込み ／ 反撃の外**のときだけ行う。刻んだ層は羽を控え、続く `DrainFeatherMarks` が撃つ。羽のまとまりの叫びがまた玉を溜めたら、吐き出しと羽を往復する。");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §2-5 ／ §2-6
    // ---------------------------------------------------------------------------------
    static void BoardsAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = Pick(only);
        var boards = Boards();
        var res = new Dictionary<(string, string, string), X>();
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = Measure(Apply(bf, v.Def), w);
        var dolga = new Dictionary<(string, string), int>();
        foreach (var (bn, bf) in boards) foreach (var w in Waves)
            if (bf.Occupied().Any(o => o.Def.Id == "hisa") && vers.Any(v => res[(bn, v.Name, w.Name)].Wins * 2 >= Seeds)) dolga[(bn, w.Name)] = WinsLite(Apply(bf, UnitCatalog.Dolga), w);
        Console.WriteLine($"# 第321期 §2-5 ／ §2-6 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("## 1. 勝率（括弧は規定との差）・倒しT と対照（ヒサ → ドルガ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
        {
            foreach (var v in vers)
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var x = res[(bn, v.Name, w.Name)]; var b0 = res[(bn, vers[0].Name, w.Name)];
                    double a = 100.0 * x.Wins / x.N, b = 100.0 * b0.Wins / b0.N;
                    return (v == vers[0] || a == b ? F1(a) : $"{F1(a)}（{Sg(a - b)}）") + $"・T{Per(x.WinT, x.Wins)}";
                })) + " |");
            Console.WriteLine($"| {bn} | 対照 ドルガ | " + string.Join(" | ", Waves.Select(w => dolga.TryGetValue((bn, w.Name), out int d) ? F1(100.0 * d / Seeds) : "—")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("## 2. 号令（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("吐いた ＝ 号令の回数（規定は手番の号令・版はあふれ）／ 層 ／ 号令の層が呼んだ羽。1回 ＝ 1回の吐き出しの層 ／ 羽。持ち越し ＝ 吐き出せる玉があったのに粛 ／ 痺れで止めた回数。");
        Console.WriteLine("羽を呼ばなかった層 ＝ 号令の層のうち羽の連鎖の中で刻まれて控えられなかった層（版では 0 のはず）。1巡の最大 ＝ 1ターンに刻んだ号令の層の最大（≧ 10 の巡の数・200 戦の延べ）。往復の上限 ＝ 1回の閉じ目で吐き出しと羽の往復が 64 回に達した回数。張り付き ＝ 上限の版で1巡の上限に達して玉を次の巡へ回した巡（1戦あたり）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 吐いた ／ 層 ／ 羽 | 1回 層 ／ 羽 | 捨てた溢れ | 持ち越し 粛 ／ 痺れ | 的なし | 羽を呼ばなかった層 | 1巡の最大（≧10 の巡） | 往復の上限 | 張り付き | ミサ ／ ザンの与ダメ | 標の最深 |");
        Console.WriteLine("|---|---|---|---|---|--:|---|--:|--:|---|--:|--:|---|--:|");
        foreach (var (bn, bf) in boards)
        {
            if (!bf.Occupied().Any(o => o.Def.Id == "hisa")) continue;
            foreach (var w in Waves)
                foreach (var v in vers)
                {
                    var x = res[(bn, v.Name, w.Name)];
                    long fires = x.TurnCmds + x.Bursts, layers = x.TurnLayers + x.BurstLayers;
                    Console.WriteLine($"| {bn} | {w.Name} | {v.Name} | {Per(fires, x.N)} ／ {Per(layers, x.N)} ／ {Per(x.CmdFeathers, x.N)} | {Per(layers, fires)} ／ {Per(x.CmdFeathers, fires)} | {Per0(x.Dropped, x.N)} | {Per(x.BurstHushed, x.N)} ／ {Per(x.BurstBlocked, x.N)} | {Per(x.BurstNoTarget, x.N)} | {x.ChainSkipped} | {x.RoundMax}（{x.Rounds10}） | {x.BurstGuard} | {Per(x.BurstCapped, x.N)} | {Per0(x.MisaDmg, x.N)} ／ {Per0(x.ZanDmg, x.N)} | {x.MarkPeak} |");
                }
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §2-7 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = Pick(only);
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        double[,] Grid(UnitDef d)
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = Apply(rows[ri].F, d);
                int wins = 0;
                for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / Seeds;
            });
            return g;
        }
        var b = Grid(vers[0].Def);
        Console.WriteLine($"# 第321期 §2-7 `compare` {rows.Length} 行 × 版（seed 0..{Seeds - 1}・基準は規定のヒサ）");
        Console.WriteLine();
        Console.WriteLine($"ヒサのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "hisa")).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, Grid(vers[vi].Def), "hisa");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3 台本の並び
    // ---------------------------------------------------------------------------------
    static void Memo(string boardPart, string wave, int seed, string ver)
    {
        var (name, f0) = Boards().First(b => b.Name.Contains(boardPart, StringComparison.Ordinal));
        var v = V(ver);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(Apply(f0, v.Def), BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        int hisa = p.First(u => u.Def.Id == "hisa").InstanceId;
        Console.WriteLine($"# hisa321 memo —— {name} × {v.Name} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1, lines = 0;
        bool after = false;
        foreach (var x in r.Events)
        {
            bool cmd = x.Kind is BattleEventKind.Command or BattleEventKind.CommandBall && x.ActorId == hisa;
            if (cmd && x.Kind == BattleEventKind.Command) after = true;
            bool keep = cmd || x.Kind is BattleEventKind.MarkRally or BattleEventKind.MarkLayer && x.ActorId == hisa
                || after && (x.Kind is BattleEventKind.FeatherMark or BattleEventKind.MarkLayer || x.Kind == BattleEventKind.StatusGain && x.Text == StatusKeys.Marked);
            if (x.Kind == BattleEventKind.TurnStart) after = false;
            if (!keep) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}");
            if (++lines >= 200) { Console.WriteLine("…（以下略）"); break; }
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // §4 自己検査
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
    static readonly PropertyInfo TallyProp = typeof(BattleContext).GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TallyByUnit が見つからない");
    static readonly MethodInfo PoolAdd = typeof(BattleContext).GetMethod("CommandPoolAdd", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("CommandPoolAdd が見つからない");
    static UnitTally Tal(BattleContext ctx, string id) => ((Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!).GetValueOrDefault(id) ?? new UnitTally();
    static UnitDef Wall => UnitCatalog.Dolga.WithStats(5000, 1);
    static readonly UnitDef Husher = new() { Id = "hush321", Name = "粛の壁", MaxHp = 5000, Attack = 1, Speed = 1, Traits = new[] { TraitId.Hush } };

    static BattleContext Ctx(UnitDef hisa, out UnitState h, out UnitState misa, out List<UnitState> e, bool hush = false)
    {
        var ctx = new BattleContext(0, true);
        var p = BattleEngine.Materialize(Formation.Build(front1: UnitCatalog.Golm, center: UnitCatalog.Tome, back3: hisa), BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(hush ? Formation.Build(front1: Wall, front3: Husher) : Formation.Build(front1: Wall, front3: Wall), BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnProp.SetValue(ctx, 1);
        h = p.First(u => u.Def.Id == "hisa"); misa = p.First(u => u.Def.Id == "tome");
        // 羽の叫びが溢れて玉を作り直さないよう、ミサ（羽の主 ＝ 叫びの回復の受け手）を減らしておく（溢れ ＝ 満タンで入らなかった量）。
        // 減らさないと、壁（倒れない的）に羽が当たるたびに 6 × 層 の溢れが出て「吐く → 羽 → 叫び → 玉」が往復で育つ（実戦では的が倒れる・本測定の `BurstGuard` ／ 1巡の最大で見る）。
        misa.Hp = 1;
        return ctx;
    }
    static int Cmds(BattleContext ctx, string label) => ctx.Events.Count(x => x.Kind == BattleEventKind.Command && x.Text == label);
    static int Pool(UnitState h) => h.RawCounter(CommandTrait.PoolKey);

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# hisa321 check —— 第321期 段2の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        {
            bool a = UnitCatalog.HisaHQa.Traits.SequenceEqual(UnitCatalog.Hisa.Traits.Select(t => t == TraitId.CommandBall ? TraitId.CommandBurst3 : t));
            bool b = UnitCatalog.HisaHQb.Traits.SequenceEqual(UnitCatalog.Hisa.Traits.Select(t => t == TraitId.CommandBall ? TraitId.CommandBurst1 : t));
            bool outside = Vers.Skip(1).All(v => !UnitCatalog.All.Contains(v.Def) && !UnitCatalog.Retired.Contains(v.Def) && v.Def.PlusText != UnitCatalog.Hisa.PlusText);
            bool none = UnitCatalog.All.All(d => !d.Traits.Contains(TraitId.CommandBurst3) && !d.Traits.Contains(TraitId.CommandBurst1));
            Expect("定義: HQ-a ／ HQ-b ＝ 規定の号令の玉を あふれ3 ／ あふれ1 に差し替え・`All` ／ `Retired` の外・文面の差し替え・規定の駒は札を持たない", a && b && outside && none, $"HQ-a {a}・HQ-b {b}・外 {outside}・規定 {none}");
        }
        // HQ-a: 玉 3 つで3層・閉じた直後・羽を呼ぶ・端数は持ち越す・手番の号令は無い
        {
            var ctx = Ctx(UnitCatalog.HisaHQa, out var h, out var misa, out var e);
            PoolAdd.Invoke(ctx, new object[] { h, 50, Tal(ctx, "hisa") });
            ctx.Interrupt(() => { });
            int c50 = Cmds(ctx, CommandLabels.Burst);
            PoolAdd.Invoke(ctx, new object[] { h, 30, Tal(ctx, "hisa") });
            int inside = -1;
            ctx.Interrupt(() => { ctx.Reaction(() => { }); inside = Cmds(ctx, CommandLabels.Burst); });
            int after = Cmds(ctx, CommandLabels.Burst);
            var foe = e.OrderByDescending(x => x.RawCounter(StatusKeys.Marked)).First();
            var ht = Tal(ctx, "hisa"); var mt = Tal(ctx, "tome");
            Expect("HQ-a: 玉 2 つ（50）では吐かない・80 に溜まると、内側のまとまり（反撃）が閉じても吐かず、外のまとまり（割り込み）が閉じた直後に3層まとめて吐く・端数 20 は持ち越す・溢れを捨てない",
                c50 == 0 && inside == 0 && after == 1 && ht.BurstLayers == 3 && foe.RawCounter(StatusKeys.Marked) == 3 && Pool(h) == 20 && ht.CommandDropped == 0,
                $"50 で {c50} 回・内側 {inside}・閉じた後 {after}・層 {ht.BurstLayers}（的の標 {foe.RawCounter(StatusKeys.Marked)}）・残り {Pool(h)}・捨てた {ht.CommandDropped}");
            Expect("HQ-a: 吐いた3層が羽を呼ぶ（号令の層が呼んだ羽 3・羽の連鎖の中で控えられなかった号令の層 0）", mt.CommandFeathers == 3 && mt.CmdChainSkipped == 0, $"羽 {mt.CommandFeathers}・控えなかった {mt.CmdChainSkipped}");
            PoolAdd.Invoke(ctx, new object[] { h, 100, Tal(ctx, "hisa") });
            Expect("HQ-a: 上限で捨てない（溜まり 120 まで入る）", Pool(h) == 120 && ht.CommandDropped == 0, $"溜まり {Pool(h)}");
        }
        {
            var ctx = Ctx(UnitCatalog.HisaHQa, out var h, out _, out _);
            PoolAdd.Invoke(ctx, new object[] { h, 60, Tal(ctx, "hisa") });
            ctx.TakeTurn(h);
            var ht = Tal(ctx, "hisa");
            Expect("HQ-a: ヒサの手番に号令しない（手番の号令 0）・手番のまとまりが閉じた直後にあふれ（3 層・端数 0）", ht.CommandTurns == 0 && Cmds(ctx, CommandLabels.Turn) == 0 && ht.BurstFires == 1 && ht.BurstLayers == 3 && Pool(h) == 0,
                $"手番の号令 {ht.CommandTurns}・あふれ {ht.BurstFires} 回 ／ {ht.BurstLayers} 層・残り {Pool(h)}");
        }
        // HQ-b: 玉1つごとに1層
        {
            var ctx = Ctx(UnitCatalog.HisaHQb, out var h, out _, out _);
            PoolAdd.Invoke(ctx, new object[] { h, 50, Tal(ctx, "hisa") });
            ctx.Interrupt(() => { });
            var ht = Tal(ctx, "hisa");
            Expect("HQ-b: 溜まり 50 ＝ 玉 2 つで、閉じた直後に2層（端数 10 は持ち越す）", ht.BurstFires == 1 && ht.BurstLayers == 2 && Pool(h) == 10, $"{ht.BurstFires} 回 ／ {ht.BurstLayers} 層・残り {Pool(h)}");
        }
        // 上限の版: 1巡に吐き出す層は上限まで・超えた玉は次の巡へ
        foreach (var (def, cap, unit) in new[] { (UnitCatalog.HisaHQa3, 3, 3), (UnitCatalog.HisaHQa6, 6, 3), (UnitCatalog.HisaHQb3, 3, 1), (UnitCatalog.HisaHQb9, 9, 1) })
        {
            var ctx = Ctx(def, out var h, out _, out _);
            PoolAdd.Invoke(ctx, new object[] { h, 400, Tal(ctx, "hisa") });
            ctx.Interrupt(() => { });
            ctx.Interrupt(() => { });
            var ht = Tal(ctx, "hisa");
            long l1 = ht.BurstLayers, capped = ht.BurstCapped;
            TurnProp.SetValue(ctx, 2);
            ctx.Interrupt(() => { });
            long l2 = ht.BurstLayers;
            Expect($"{def.Name}（{Vers.First(v => ReferenceEquals(v.Def, def)).Name}）: 溜まり 400（玉 20）でも1巡に {cap} 層まで（同じ巡の2度目の閉じ目は張り付き 1）・次の巡にまた {cap} 層",
                l1 == cap && capped == 1 && l2 == 2 * cap && Pool(h) >= 400 - 2 * cap * 20, $"1巡目 {l1} 層（張り付き {capped}）・2巡目まで {l2} 層・残り {Pool(h)}（羽の叫びの溢れで増える）");
        }
        // 痺れ ／ 粛で持ち越す
        {
            var ctx = Ctx(UnitCatalog.HisaHQa, out var h, out _, out _);
            PoolAdd.Invoke(ctx, new object[] { h, 60, Tal(ctx, "hisa") });
            h.SetCounter(StatusKeys.Stun, 1);
            ctx.Interrupt(() => { });
            var ht = Tal(ctx, "hisa");
            bool held = ht.BurstFires == 0 && ht.BurstBlocked == 1 && Pool(h) == 60;
            h.SetCounter(StatusKeys.Stun, 0);
            ctx.Interrupt(() => { });
            Expect("HQ-a: 痺れていれば吐かずに玉を持ち越し、痺れが解けた次の閉じ目で吐く", held && ht.BurstFires == 1 && Pool(h) == 0, $"痺れ中 {held}・その後 {ht.BurstFires} 回");
        }
        {
            var ctx = Ctx(UnitCatalog.HisaHQa, out var h, out _, out var e, hush: true);
            PoolAdd.Invoke(ctx, new object[] { h, 60, Tal(ctx, "hisa") });
            ctx.Interrupt(() => { });
            var ht = Tal(ctx, "hisa");
            bool held = ht.BurstFires == 0 && ht.BurstHushed == 1 && Pool(h) == 60;
            e.First(u => u.Def.Id == "hush321").Hp = 0;
            ctx.Interrupt(() => { });
            Expect("HQ-a: 粛の保持者が生きている間は吐かずに玉を持ち越し（粛で止めた 1）、粛が消えた次の閉じ目で吐く", held && ht.BurstFires == 1, $"粛の間 {held}（粛 {ht.BurstHushed}）・その後 {ht.BurstFires} 回");
        }
        // 戦の中: 規定は手番の号令・版はあふれだけ・捨てない・verbose の有無で同じ
        {
            var bf = Presets.Playtest.First(r => r.Name == "試遊・標 ボス台").F;
            foreach (var v in Vers)
                foreach (var w in new[] { WaveOf("boss"), WaveOf("guard"), WaveOf("2") })
                {
                    long turnCmd = 0, bursts = 0, dropped = 0, skipped = 0, guard = 0; int diff = 0;
                    for (int sd = 0; sd < 20; sd++)
                    {
                        var f = Apply(bf, v.Def);
                        var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                        var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        if (r1.PlayerWon != r0.PlayerWon || r1.Turns != r0.Turns) diff++;
                        var t = r1.TallyByUnit["hisa"];
                        turnCmd += r1.Events.Count(x => x.Kind == BattleEventKind.Command && x.Text == CommandLabels.Turn);
                        bursts += t.BurstFires; dropped += t.CommandDropped; guard += t.BurstGuard;
                        if (r1.TallyByUnit.TryGetValue("tome", out var mt)) skipped += mt.CmdChainSkipped;
                    }
                    bool isBase = v == Vers[0];
                    Expect($"標 ボス台 × {w.Name} × {v.Name}（seed 0..19）: {(isBase ? "手番の号令だけ・あふれ 0" : "あふれだけ・手番の号令 0・捨てた溢れ 0・羽を呼ばなかった号令の層 0")}・verbose の有無で勝敗 ／ 決着T が同じ",
                        (isBase ? bursts == 0 : turnCmd == 0 && dropped == 0 && skipped == 0) && diff == 0, $"手番の号令 {turnCmd}・あふれ {bursts}・捨てた {dropped}・控えなかった {skipped}・往復の上限 {guard}・違い {diff}");
                }
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
