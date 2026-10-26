using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S388 = Sh388Diag;
using S390 = Sh390Diag;
using S401 = Sh401Diag;
using S402 = Sh402Diag;

// =====================================================================================
// sh403 —— 第403期【軽い期】段0: ハギの B を規定に ／ 本体: 単体の敵を削る（版 T1 ／ T2 ／ T12 ／ T12A）。本体の版は規定にしない。
// 指示書は design/PHASE403_HAGI_SINGLE_SPEC.md、報告は design/PHASE403_HAGI_SINGLE.md。
//
//     dotnet run --project BattleSim -c Release 0 sh403 zero      # §2 段0: `compare` の動き（第402期の表と ／ 第402期の B の列と）＋ 試遊の 4 台 × 規定 ／ 旧 × 5 物差し
//     dotnet run --project BattleSim -c Release 0 sh403 boards    # §4-1 の 1〜2: キャリーの手組み 3 ／ 試遊の 4 台 × 列（規定 ／ T1 ／ T2 ／ T12 ／ T12A）× 8 物差し
//     dotnet run --project BattleSim -c Release 0 sh403 cmp       # §4-1 の 3: ハギのいる `compare` の行 × 版（seed 0..199）＋ (G2)
//     dotnet run --project BattleSim -c Release 0 sh403 log <台> <列> <物差し 0..7> <seed>   # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 sh403 check     # 自己検査
//
// 替えた台は器具の中だけ（試遊の台に足す「版・二度斬り ハギ」を除く）。**予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p403/predict.md`）。
// =====================================================================================
static partial class Sh403Diag
{
    internal const string PredictFile = ".tmp/p403/predict.md";
    const string Tag = "第403期";
    const int Seeds = 100, CompareSeeds = 200, TT = 9;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "zero" or "boards" or "cmp" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh403: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "zero": Zero(); return;
            case "boards": Boards(); return;
            case "cmp": Cmp(); return;
            case "log": Log1(args); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh403: モードは zero / boards / cmp / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => double.IsNaN(x) ? "—" : x.ToString("#,0");
    static string Sg(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pc(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static long X(S377.B b, string k) => S401.X(b, k);
    static void Add(S377.B b, string k, long v) => S401.Add(b, k, v);
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }
    static Formation ColBoard(S388.Board b, UnitDef? d) => d is null ? b.F : Put(b.F, b.Slot, d);

    internal static readonly (string Name, UnitDef D)[] Vers =
    {
        ("規定", UnitCatalog.HagiH403), ("T1", UnitCatalog.HagiSingleT1), ("T2", UnitCatalog.HagiSingleT2), ("T12", UnitCatalog.HagiSingleT12), ("T12A", UnitCatalog.HagiSingleT12A),
    };
    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    static readonly int[] W4 = { 0, 1, 2, 5 };
    const int WBoss = 0, WGuard = 1, WBat = 2, WStrong = 7;

    internal static S388.Board[] CarryHand() => S401.CarryBoards();
    /// <summary>試遊・ハギ 台B ／ 死神と墓守 ／ キャリー ／ 版・移動 ハギ（第402期と同じ）。</summary>
    internal static S388.Board[] StopBoards()
    {
        var b = S401.StopBoards();
        return new[] { b[0], b[1], new S388.Board("試遊・ハギ キャリー", Presets.HagiCarry401(), 3, "ハギ"), b[2] };
    }

    static double[,] CGrid((string Name, Formation F)[] rows, int seeds)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw, wins = 0;
            for (int s = 0; s < seeds; s++) if (BattleEngine.Run(rows[ri].F, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / seeds;
        });
        return g;
    }

    /// <summary>第402期の計数 ＋ ハギ → 勇者のターンごとの量 ／ 勇者が 8 割を割った最初の T ／ ハギの1ターンの与ダメの最大 ／ 二度斬り ／ 印の最大。</summary>
    internal static Action<BattleResult, List<UnitState>, List<UnitState>, S377.B> Extra(bool boss, string label)
    {
        var baseX = S402.Extra(boss, label);
        return (r, p, e, b) =>
        {
            baseX(r, p, e, b);
            var hagi = p.FirstOrDefault(u => u.Def.Id == "hagi");
            int hid = hagi?.InstanceId ?? -9;
            var foes = new HashSet<int>(e.Select(u => u.InstanceId));
            var perT = new long[BattleEngine.MaxTurns + 2];
            var heroT = new long[BattleEngine.MaxTurns + 2];
            UnitState? hero = boss && e.Count > 0 ? e[0] : null;
            int first80 = 0;
            foreach (var x in r.Events)
            {
                if (x.Kind != BattleEventKind.Damage) continue;
                int t = Math.Clamp(x.Turn, 0, BattleEngine.MaxTurns + 1);
                int tgt = x.TargetId ?? -3;
                if (x.ActorId == hid && foes.Contains(tgt)) perT[t] += x.Amount;
                if (hero is not null && tgt == hero.InstanceId)
                {
                    if (x.ActorId == hid) heroT[t] += x.Amount;
                    if (first80 == 0 && (long)x.HpAfter * 100 <= (long)hero.Def.MaxHp * 80) first80 = Math.Max(1, t);
                }
            }
            if (hagi is not null)
            {
                long mx = perT.Max();
                Add(b, "hagiTurnMax", mx);
                S401Max(label + ":hagiTurnMax", mx);
                if (hero is not null)
                {
                    for (int t = 1; t < TT; t++) Add(b, $"heroHagiT{t}", heroT[t]);
                    if (first80 > 0) { Add(b, "h80", 1); Add(b, "h80T", first80); }
                }
                if (r.TallyByUnit.TryGetValue("hagi", out var h))
                {
                    Add(b, "twins", h.R403Twins); Add(b, "twinReaps", h.R403TwinReaps); Add(b, "twinKills", h.R403TwinKills);
                    if (h.R403TwinFoes is { } tf) for (int k = 0; k < tf.Length; k++) Add(b, $"twinFoes{k}", tf[k]);
                    Add(b, "markMax", h.R403MarkMax);
                    S401Max(label + ":markMax", h.R403MarkMax);
                    if (h.R403MarkMaxByTurn is { } mb) for (int t = 1; t < TT && t < mb.Length; t++) { Add(b, $"markT{t}", mb[t]); if (mb[t] > 0) Add(b, $"markTN{t}", 1); }
                }
            }
        };
    }
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> Max = new();
    static void S401Max(string k, long v) => Max.AddOrUpdate(k, v, (_, o) => Math.Max(o, v));
    internal static long Mx(string k) => Max.GetValueOrDefault(k);

    static S377.B[] M8(Formation f, string label)
    {
        var g = new S377.B[W8.Length];
        Parallel.For(0, W8.Length, wi => g[wi] = S377.Measure(f, W8[wi].Ws, Seeds, Extra(wi == WBoss, $"{label}|{wi}")));
        return g;
    }
    static S377.B[] M5(Formation f, string label)
    {
        int[] ws = { WBoss, WGuard, WBat, 5, WStrong };
        var g = new S377.B[W8.Length];
        Parallel.ForEach(ws, wi => g[wi] = S377.Measure(f, W8[wi].Ws, Seeds, Extra(wi == WBoss, $"{label}|{wi}")));
        return g;
    }
    static double A4(S377.B[] a) => W4.Average(wi => a[wi].Win);

    // ---------------------------------------------------------------------------------
    // §2 段0
    // ---------------------------------------------------------------------------------
    static void Zero()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §2 段0 —— ハギの B（味方の死でも魂）を規定に（旧は `UnitCatalog.HagiH402`・H123）");
        Console.WriteLine();
        var rows = CompareBuilds().Select(r => (r.Name, F: Pin403(r.F))).ToArray();   // 第404期: ハギは第403期の規定に
        var now = CGrid(rows, CompareSeeds);
        var old = CGrid(rows.Select(r => (r.Name, F: Pin402(r.F))).ToArray(), CompareSeeds);
        var bcol = CGrid(rows.Select(r => (r.Name, F: I.Swap(Pin402(r.F), "hagi", UnitCatalog.HagiSoulB))).ToArray(), CompareSeeds);
        var head = S390.BalanceMap(S390.GitShow("660fbb61:docs/balance.md"));
        int nw = EnemyCatalog.Stages.Count, moved = 0, sameB = 0, sameOld = 0, cells = 0;
        var mv = new List<string>();
        for (int ri = 0; ri < rows.Length; ri++)
            for (int w = 0; w < nw; w++)
            {
                cells++;
                string c = head.TryGetValue(rows[ri].Name, out var a) ? a[w].TrimEnd('%') : "?";
                if (c != F1(now[ri, w])) { moved++; mv.Add($"{rows[ri].Name} 第{w + 1}波 {c} → {F1(now[ri, w])}"); }
                if (now[ri, w] == bcol[ri, w]) sameB++;
                if (c == F1(old[ri, w])) sameOld++;
            }
        Console.WriteLine($"- 第402期の HEAD（660fbb61）の `docs/balance.md` と違うセル **{moved}**（{string.Join("・", mv)}）");
        Console.WriteLine($"- 第402期の B の列（ハギを `HagiSoulB` に替えた全行）と同じセル {sameB} ／ {cells}・旧（`HagiH402`）に戻した全行が HEAD の表と同じセル {sameOld} ／ {cells}");
        Console.WriteLine();
        Console.WriteLine($"（seed 0..{Seeds - 1}・セルは「勝率・決着T・倒れた編成の駒」・ボスの T はボスの倒しT）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | ボス | 近衛 | 大隊 | 本編 第5波 | 強 第2〜5波 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (var b in StopBoards())
            foreach (var (cn, d) in new[] { ("規定（第403期）", UnitCatalog.HagiH403), ("第402期（旧）", UnitCatalog.HagiH402) })
            {
                var m = M5(ColBoard(b, d), $"zero:{b.Name}:{cn}");
                Console.WriteLine($"| {(cn.StartsWith("規定") ? b.Name : "")} | {cn} | {string.Join(" | ", new[] { WBoss, WGuard, WBat, 5, WStrong }.Select(w => m[w].Cell))} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-1 の 1〜2: 台 × 版 × 8 物差し
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §4-1 の 1〜2 台 × 版（{string.Join(" ／ ", Vers.Select(v => v.Name))}）× 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var (title, boards) in new (string, S388.Board[])[] { ("A. キャリーの手組み（火力の無い支えで固めた台）", CarryHand()), ("B. 試遊の台（止め役 ／ キャリー）", StopBoards()) })
        {
            Console.WriteLine($"## {title}");
            Console.WriteLine();
            Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(ColBoard(b, UnitCatalog.HagiH403))}）")) + "。");
            Console.WriteLine();
            var g = new S377.B[boards.Length, Vers.Length][];
            for (int bi = 0; bi < boards.Length; bi++) for (int vi = 0; vi < Vers.Length; vi++) g[bi, vi] = M8(ColBoard(boards[bi], Vers[vi].D), $"{boards[bi].Name}|{Vers[vi].Name}");
            Report(boards.Select(b => b.Name).ToArray(), g);
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Report(string[] names, S377.B[,][] g)
    {
        int nb = names.Length, nv = Vers.Length;
        string Lab(int bi, int vi) => $"{names[bi]}|{Vers[vi].Name}";
        Console.WriteLine("### 1. 勝率・決着T（勝った戦）・倒れた編成の駒（1戦）・ボスの倒しT");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 | 規定との差 | ボスの倒しT |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
                Console.WriteLine($"| {(vi == 0 ? names[bi] : "")} | {Vers[vi].Name} | {string.Join(" | ", g[bi, vi].Select(x => x.Cell))} | {F1(A4(g[bi, vi]))} | {(vi == 0 ? "—" : Sg(A4(g[bi, vi]) - A4(g[bi, 0])))} | {g[bi, vi][WBoss].WinTs} |");
        Console.WriteLine();

        Console.WriteLine("### 2. ボス: ハギ → 勇者（1ターンの最大の平均 ／ 全戦の最大 ／ T1〜T8 の1戦の平均）・1,200 を超えたターンがあった戦・勇者の HP（ターンの頭 T1〜T8・%）・勇者が 8 割を割った戦 ／ 最初の T・勇者の止め（ハギ ／ リィカ ／ ほか %）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 勝率 | ハギ → 勇者（平均 ／ 最大）| T1〜T8 | 1,200 超え | 勇者の HP T1〜T8 | 8 割を割った（T）| 勇者の止め |");
        Console.WriteLine("|---|---|--:|---|---|--:|---|---|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
            {
                var x = g[bi, vi][WBoss];
                long hn = X(x, "heroKRica") + X(x, "heroKHagi") + X(x, "heroKOther");
                string tl = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => F0((double)X(x, $"heroHagiT{t}") / Math.Max(1, x.N))));
                string hh = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"heroHeadN{t}") == 0 ? "—" : F0((double)X(x, $"heroHead{t}") / X(x, $"heroHeadN{t}"))));
                Console.WriteLine($"| {(vi == 0 ? names[bi] : "")} | {Vers[vi].Name} | {F1(x.Win)} | {Per(X(x, "heroHagiTurn"), x.N)} ／ {F0(S401.Mx(Lab(bi, vi) + "|0:heroHagiTurn"))} | {tl} | {Pc(X(x, "heroHagiOver"), x.N)}% | {hh} | {Pc(X(x, "h80"), x.N)}%（T{Per(X(x, "h80T"), X(x, "h80"))}）| {Pc(X(x, "heroKHagi"), hn)} ／ {Pc(X(x, "heroKRica"), hn)} ／ {Pc(X(x, "heroKOther"), hn)} |");
            }
        Console.WriteLine();

        Console.WriteLine("### 3. T1: 孤立の二度斬り（1戦の回数 ／ 2 発目で刈った ／ 二度斬りの手番で倒した）と、二度斬りのときの敵の生きている数（その的を含む・割合）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 二度斬り | 2 発目で刈った | 倒した | 敵の数 1 ／ 2 ／ 3 ／ 4 ／ 5+（%）|");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
            {
                if (!Vers[vi].D.Traits.Contains(TraitId.HagiTwinIsolated)) continue;
                foreach (int w in new[] { WBoss, WGuard, WBat, 3, 5, 6, WStrong })
                {
                    var x = g[bi, vi][w];
                    long tw = X(x, "twins");
                    string dist = string.Join(" ／ ", new[] { 1, 2, 3, 4 }.Select(k => Pc(X(x, $"twinFoes{k}"), tw)).Append(Pc(Enumerable.Range(5, 5).Sum(k => X(x, $"twinFoes{k}")), tw)));
                    Console.WriteLine($"| {(vi == 1 && w == 0 ? names[bi] : "")} | {(w == 0 ? Vers[vi].Name : "")} | {W8[w].Name} | {Per(tw, x.N)} | {Per(X(x, "twinReaps"), x.N)} | {Per(X(x, "twinKills"), x.N)} | {dist} |");
                }
            }
        Console.WriteLine();

        Console.WriteLine("### 4. 印（ハギが刻んだ印の最大・1戦の平均 ／ 全戦の最大・ターンごとの最大 T1〜T8（その T に刻んだ戦の平均））・H1 の足し分（1戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 印の最大（平均 ／ 最大）| T1〜T8 | H1 の足し分 |");
        Console.WriteLine("|---|---|---|---|---|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
                foreach (int w in new[] { WBoss, WGuard, WBat })
                {
                    var x = g[bi, vi][w];
                    string tl = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"markTN{t}") == 0 ? "—" : F1((double)X(x, $"markT{t}") / X(x, $"markTN{t}"))));
                    Console.WriteLine($"| {(vi == 0 && w == 0 ? names[bi] : "")} | {(w == 0 ? Vers[vi].Name : "")} | {W8[w].Name} | {Per(X(x, "markMax"), x.N)} ／ {F0(Mx($"{Lab(bi, vi)}|{w}:markMax"))} | {tl} | {Per(X(x, "markAdd"), x.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("### 5. 魂（ターンの頭 T1〜T8・生きていた戦の平均・ボス）と魂の出どころ（1戦: 敵の死 ／ 線 8・5・2 割 ／ A の上乗せ ／ B の味方の死）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | T1〜T8 | 敵の死 | 線（8 ／ 5 ／ 2）| A | B |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
            {
                var x = g[bi, vi][WBoss];
                string tl = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"halive{t}") == 0 ? "—" : F1((double)X(x, $"soul{t}") / X(x, $"halive{t}"))));
                Console.WriteLine($"| {(vi == 0 ? names[bi] : "")} | {Vers[vi].Name} | {tl} | {Per(X(x, "srcDeath"), x.N)} | {Per(X(x, "src80"), x.N)} ／ {Per(X(x, "src50"), x.N)} ／ {Per(X(x, "src20"), x.N)} | {Per(X(x, "srcBig"), x.N)} | {Per(X(x, "srcAlly"), x.N)} |");
            }
        Console.WriteLine();

        Console.WriteLine("### 6. 誰が何Tに倒れたか（規定 ／ 4 波の平均がいちばん良い版・ボス ／ 近衛 ／ 大隊）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
        {
            int best = Enumerable.Range(0, nv).OrderByDescending(vi => A4(g[bi, vi])).ThenByDescending(vi => g[bi, vi][WBoss].Win).ThenBy(vi => vi).First();
            Console.WriteLine($"- **{names[bi]}**（最良の版 {Vers[best].Name}・4 波 {F1(A4(g[bi, best]))} ／ 規定 {F1(A4(g[bi, 0]))}）");
            foreach (int w in new[] { WBoss, WGuard, WBat })
            {
                Console.WriteLine($"  - {W8[w].Name}: 規定 {F1(g[bi, 0][w].Win)}%・{g[bi, 0][w].WinTs}T — {g[bi, 0][w].FellCell}");
                if (best != 0) Console.WriteLine($"  - {W8[w].Name}: {Vers[best].Name} {F1(g[bi, best][w].Win)}%・{g[bi, best][w].WinTs}T — {g[bi, best][w].FellCell}");
            }
        }
        Console.WriteLine();

        Console.WriteLine("### 7. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上（規定と比べて）／ あふれ）と、ハギの一撃の最大 ／ ハギの1ターンの与ダメの最大（ボス ／ 近衛 ／ 大隊 ／ 強）");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 1; vi < nv; vi++)
            {
                string at = $"{names[bi]} × {Vers[vi].Name}";
                foreach (int w in new[] { WGuard, WBat }) { var x = g[bi, vi][w]; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{at} × {W8[w].Name}: 1T 決着 {Pc(x.Win1T, x.N)}%"); }
                { var x = g[bi, vi][WBoss]; if (x.BossFast > 0) signs.Add($"{at} × ボス: 4T 未満の勝ち {Pc(x.BossFast, x.N)}%"); }
                for (int w = 0; w < W8.Length; w++)
                {
                    var x = g[bi, vi][w]; var x0 = g[bi, 0][w];
                    if (100.0 * x.Cap / x.N - 100.0 * x0.Cap / x0.N >= 5) signs.Add($"{at} × {W8[w].Name}: 30T の打ち切り {Pc(x0.Cap, x0.N)} → {Pc(x.Cap, x.N)}%");
                    if (x.Overflow > 0) signs.Add($"{at} × {W8[w].Name}: あふれの兆し {x.Overflow} 件");
                }
            }
        if (signs.Count == 0) Console.WriteLine("- 無し。");
        foreach (var s in signs) Console.WriteLine("- " + s);
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 一撃の最大 | 1ターンの最大（全戦の最大）|");
        Console.WriteLine("|---|---|---|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
            {
                int[] ws = { WBoss, WGuard, WBat, WStrong };
                Console.WriteLine($"| {(vi == 0 ? names[bi] : "")} | {Vers[vi].Name} | {string.Join(" ／ ", ws.Select(w => F0(S401.Mx($"{Lab(bi, vi)}|{w}:hitMax"))))} | {string.Join(" ／ ", ws.Select(w => F0(Mx($"{Lab(bi, vi)}|{w}:hagiTurnMax"))))} |");
            }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §4-1 の 3: `compare` のハギの行 ＋ (G2)
    // ---------------------------------------------------------------------------------
    static void Cmp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().Where(r => Has(r.F, "hagi")).Select(r => (r.Name, F: Pin403(r.F))).ToArray();   // 第404期
        int nw = EnemyCatalog.Stages.Count;
        var grids = Vers.Select(c => CGrid(rows.Select(r => (r.Name, I.Swap(r.F, "hagi", c.D))).ToArray(), CompareSeeds)).ToArray();
        Console.WriteLine($"# {Tag} §4-1 の 3 `compare` のハギの行 —— {rows.Length} 行 × 版（第1〜5波・seed 0..{CompareSeeds - 1}・ハギだけ替える）");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", Vers.Select(c => c.Name + " 第1〜5波")) + " |");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "---|")));
        for (int ri = 0; ri < rows.Length; ri++)
            Console.WriteLine($"| {rows[ri].Name} | {string.Join(" | ", grids.Select(g => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w])))))} |");
        Console.WriteLine();
        Console.WriteLine("| 版 | 第2〜5波の平均 | 規定との差 | 動いたセル（第2〜5波）| −10pt 以上落ちたセル |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        double Avg(double[,] g) => Enumerable.Range(0, rows.Length).Average(ri => Enumerable.Range(1, nw - 1).Average(w => g[ri, w]));
        for (int c = 0; c < Vers.Length; c++)
        {
            int moved = 0, drop = 0;
            for (int ri = 0; ri < rows.Length; ri++) for (int w = 1; w < nw; w++) { if (grids[c][ri, w] != grids[0][ri, w]) moved++; if (grids[c][ri, w] - grids[0][ri, w] <= -10) drop++; }
            Console.WriteLine($"| {Vers[c].Name} | {F1(Avg(grids[c]))} | {Sg(Avg(grids[c]) - Avg(grids[0]))} | {moved} | {drop} |");
        }
        Console.WriteLine();
        var all = CompareBuilds();
        var lines = new List<string>();
        for (int c = 1; c < Vers.Length; c++)
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!Enumerable.Range(0, nw).Any(w => grids[c][ri, w] - grids[0][ri, w] <= -10)) continue;
                var parts = new List<string>(); bool broken = false;
                foreach (var id in rows[ri].F.Occupied().Select(o => o.Def.Id).Distinct())
                {
                    var others = all.Where(r => r.Name != rows[ri].Name && r.F.Occupied().Any(o => o.Def.Id == id)).ToList();
                    if (others.Count == 0) { parts.Add($"{id}: 他の行 0（分解が成立しない）"); continue; }
                    double sum = 0;
                    foreach (var o in others) { int k = Array.FindIndex(rows, r => r.Name == o.Name); if (k >= 0) sum += Enumerable.Range(0, nw).Average(w => grids[c][k, w] - grids[0][k, w]); }
                    double dlt = sum / others.Count; if (dlt <= -3.0) broken = true;
                    parts.Add($"{S307.Short(UnitCatalog.ById(id))} {Sg(dlt)}（他の {others.Count} 行）");
                }
                lines.Add($"- **{Vers[c].Name} × {rows[ri].Name}**（{string.Join("・", Enumerable.Range(0, nw).Where(w => grids[c][ri, w] - grids[0][ri, w] <= -10).Select(w => $"第{w + 1}波 {F1(grids[0][ri, w])} → {F1(grids[c][ri, w])}"))}）: {string.Join("・", parts)} → **{(broken ? "壊れ" : "編成上の制約")}**");
            }
        Console.WriteLine("## (G2) −10pt 以上落ちた行の分解");
        Console.WriteLine();
        if (lines.Count == 0) Console.WriteLine("- 無し。");
        foreach (var l in lines) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Log1(string[] args)
    {
        string bn = args.Length > 3 ? args[3] : "キャリー・哀歌";
        string cn = args.Length > 4 ? args[4] : "T12";
        int wi = args.Length > 5 && int.TryParse(args[5], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 6 && int.TryParse(args[6], out int s0) ? s0 : 0;
        var all = CarryHand().Concat(StopBoards()).ToList();
        var b = all.FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal)) ?? all[0];
        var c = Vers.FirstOrDefault(x => x.Name == cn) is { Name: not null } cc ? cc : Vers[1];
        var (r1, _, _) = I.Raw(ColBoard(b, c.D), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name} × {c.Name} × {W8[wi].Name} × seed {seed}: {(r1.PlayerWon ? "勝ち" : "負け")}・{r1.Turns}T");
        foreach (var l in r1.Log) Console.WriteLine(l.Text);
    }
}
