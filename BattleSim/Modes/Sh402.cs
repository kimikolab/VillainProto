using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S388 = Sh388Diag;
using S390 = Sh390Diag;
using S401 = Sh401Diag;

// =====================================================================================
// sh402 —— 第402期【軽い期】段0: ハギの H123 を規定に ／ 本体: ボスで魂を溜める（版 A ／ D ／ AD ／ B）。本体の版は規定にしない。
// 指示書は design/PHASE402_HAGI_SOUL_SPEC.md、報告は design/PHASE402_HAGI_SOUL.md。
//
//     dotnet run --project BattleSim -c Release 0 sh402 zero      # §2 段0: `compare` の動き（第401期の表と ／ 第401期の H123 の列と）＋ 試遊の 4 台 × 規定 ／ 旧 × 5 物差し
//     dotnet run --project BattleSim -c Release 0 sh402 p0        # 前提の確かめ: ボスで魂が溜まらない理由（勇者の HP ／ 線 ／ 魂の出どころ T1〜T8）・波ごとの最大 HP 1,000 以上の敵
//     dotnet run --project BattleSim -c Release 0 sh402 boards    # §4-1 の 1〜3: キャリーの手組み 3 ／ 止め役 ／ キャリーの 4 ／ 死の台 2 × 列（規定 ／ A ／ D ／ AD ／ B）× 8 物差し
//     dotnet run --project BattleSim -c Release 0 sh402 cmp       # §4-1 の 4: ハギのいる `compare` の行 × 版（第1〜5波・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh402 log <台> <列> <物差し 0..7> <seed>   # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 sh402 check     # 自己検査
//
// 替えた台は器具の中だけ（試遊の台に足す「版・大物 ハギ」を除く）。**予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p402/predict.md`）。
// =====================================================================================
static partial class Sh402Diag
{
    internal const string PredictFile = ".tmp/p402/predict.md";
    const string Tag = "第402期";
    const int Seeds = 100, CompareSeeds = 200, TT = 9;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "zero" or "p0" or "boards" or "cmp" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh402: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "zero": Zero(); return;
            case "p0": P0(); return;
            case "boards": Boards(); return;
            case "cmp": Cmp(); return;
            case "log": Log1(args); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh402: モードは zero / p0 / boards / cmp / log / check。"); return;
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

    // ---------------------------------------------------------------------------------
    // 版 ／ 波 ／ 台
    // ---------------------------------------------------------------------------------
    internal static readonly (string Name, UnitDef? D)[] Vers =
    {
        ("規定", UnitCatalog.Hagi), ("A", UnitCatalog.HagiSoulA), ("D", UnitCatalog.HagiSoulD), ("AD", UnitCatalog.HagiSoulAD), ("B", UnitCatalog.HagiSoulB),
    };
    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;   // ボス ／ 近衛 ／ 大隊 ／ 本編 第2 ／ 4 ／ 5波 ／ 九体 ／ 強 第2〜5波
    static readonly int[] W4 = { 0, 1, 2, 5 };
    const int WBoss = 0, WGuard = 1, WBat = 2, WStrong = 7;

    /// <summary>§4-1 の 1: キャリーの手組み（第401期と同じ並び・火力の無い支えで固めた台）。</summary>
    internal static S388.Board[] CarryHand() => S401.CarryBoards();
    /// <summary>§4-1 の 2: 止め役 ／ キャリーの台（試遊・ハギ 台B ／ 死神と墓守 ／ キャリー ／ 版・移動 ハギ）。台B ／ 死神と墓守 ／ 版・移動は「ハギを入れる前」の台とその席。</summary>
    internal static S388.Board[] StopBoards()
    {
        var b = S401.StopBoards();
        return new[] { b[0], b[1], new S388.Board("試遊・ハギ キャリー", Presets.HagiCarry401(), 3, "ハギ"), b[2] };
    }
    /// <summary>§4-1 の 3: B の死の台。試遊・死の連鎖にハギを入れた最良の席は前1（ゾト・第401期 `sh401 swap`）で、それは「試遊・死神と墓守」と同じ台なので、2 台目に試遊・死 エレのスィドの席（第389〜391期の台）を置いた。</summary>
    internal static S388.Board[] DeathBoards()
    {
        var de = Presets.Versions.First(r => r.Name == "試遊・死 エレ").F;
        return new[] { S401.StopBoards()[1], new S388.Board("試遊・死 エレ（スィドの席）", de, de.Occupied().First(o => o.Def.Id == "sid").Slot, "スィド") };
    }

    static double WinRate(Formation f, I.W[] ws, int seeds)
    {
        int n = 0;
        foreach (var w in ws) for (int s = 0; s < seeds; s++) if (I.Raw(f, w, s, census: false).R.PlayerWon) n++;
        return 100.0 * n / (ws.Length * seeds);
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

    // ---------------------------------------------------------------------------------
    // 1戦の計数（第401期の `Sh401Diag.Extra` ＋ 魂の出どころ ／ 刻み切り ／ 勇者の HP ／ 線）
    // ---------------------------------------------------------------------------------
    static Action<BattleResult, List<UnitState>, List<UnitState>, S377.B> Extra(bool boss, string label)
    {
        var baseX = S401.Extra(boss, label);
        return (r, p, e, b) =>
        {
            baseX(r, p, e, b);
            if (boss && e.Count > 0)
            {
                // 勇者の HP（ターンの頭・`TurnStart` の直後に最初に出る勇者の HP を読むかわりに、Damage ／ Heal の HpAfter を追う）
                var hero = e[0]; long hp = hero.Def.MaxHp; int maxHp = hero.Def.MaxHp;
                var head = new long[TT]; var headN = new long[TT];
                foreach (var x in r.Events)
                {
                    int t = x.Turn;
                    if (x.Kind == BattleEventKind.TurnStart && t is >= 1 and < TT) { head[t] += hp; headN[t]++; }
                    if ((x.Kind == BattleEventKind.Damage || x.Kind == BattleEventKind.Heal) && x.TargetId == hero.InstanceId) hp = x.HpAfter;
                }
                for (int t = 1; t < TT; t++) if (headN[t] > 0) { Add(b, $"heroHead{t}", head[t] * 100 / maxHp / headN[t]); Add(b, $"heroHeadN{t}", 1); }
            }
            if (r.TallyByUnit.TryGetValue("hagi", out var h) && p.Any(u => u.Def.Id == "hagi"))
            {
                Add(b, "srcDeath", h.R389SoulDeath); Add(b, "src80", h.R389Cross80); Add(b, "src50", h.R389Cross50); Add(b, "src20", h.R389Cross20);
                Add(b, "srcBig", h.R402SoulBig); Add(b, "srcMark", h.R402SoulMark); Add(b, "srcAlly", h.R402SoulAlly);
                Add(b, "markSouls", h.R402MarkSouls); Add(b, "markLost", h.R402MarkSoulLostAdd);
                // 魂 9 に届いた戦（`s9` ／ `s9T`）は第401期の計数（`Sh401Diag.Extra`）が数えている
            }
        };
    }
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
        Console.WriteLine($"# {Tag} §2 段0 —— ハギの H123 を規定に（旧は `UnitCatalog.HagiH401`・P1h）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var now = CGrid(rows, CompareSeeds);
        var old = CGrid(rows.Select(r => (r.Name, Pin401(r.F))).ToArray(), CompareSeeds);
        var h123 = CGrid(rows.Select(r => (r.Name, I.Swap(Pin401(r.F), "hagi", UnitCatalog.HagiCarryH123))).ToArray(), CompareSeeds);
        var head = S390.BalanceMap(S390.GitShow("c8871366:docs/balance.md"));
        int nw = EnemyCatalog.Stages.Count, moved = 0, sameH = 0, sameOld = 0, cells = 0;
        var mv = new List<string>();
        for (int ri = 0; ri < rows.Length; ri++)
            for (int w = 0; w < nw; w++)
            {
                cells++;
                string c = head.TryGetValue(rows[ri].Name, out var a) ? a[w].TrimEnd('%') : "?";
                if (c != F1(now[ri, w])) { moved++; mv.Add($"{rows[ri].Name} 第{w + 1}波 {c} → {F1(now[ri, w])}"); }
                if (now[ri, w] == h123[ri, w]) sameH++;
                if (c == F1(old[ri, w])) sameOld++;
            }
        Console.WriteLine($"- 第401期の HEAD（c8871366）の `docs/balance.md` と違うセル **{moved}**（{string.Join("・", mv)}）");
        Console.WriteLine($"- 第401期の H123 の列（ハギを `HagiCarryH123` に替えた全行）と同じセル {sameH} ／ {cells}・旧（`HagiH401`）に戻した全行が HEAD の表と同じセル {sameOld} ／ {cells}");
        Console.WriteLine($"- ハギのいる行: {string.Join("・", rows.Where(r => Has(r.F, "hagi")).Select(r => r.Name))}");
        Console.WriteLine();
        Console.WriteLine($"（seed 0..{Seeds - 1}・セルは「勝率・決着T・倒れた編成の駒」・ボスの列の T はボスの倒しT）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | ボス | 近衛 | 大隊 | 本編 第5波 | 強 第2〜5波 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (var b in StopBoards())
            foreach (var (cn, d) in new[] { ("規定（第402期）", UnitCatalog.Hagi), ("第401期（旧）", UnitCatalog.HagiH401) })
            {
                var m = M5(ColBoard(b, d), $"zero:{b.Name}:{cn}");
                Console.WriteLine($"| {(cn.StartsWith("規定") ? b.Name : "")} | {cn} | {string.Join(" | ", new[] { WBoss, WGuard, WBat, 5, WStrong }.Select(w => m[w].Cell))} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 前提の確かめ
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} 前提の確かめ —— ボスで魂が溜まらない理由（規定のハギ・seed 0..{Seeds - 1}）と、波ごとの最大 HP 1,000 以上の敵");
        Console.WriteLine();
        Console.WriteLine("## 1. ボス: 勇者の HP（ターンの頭・最大 HP の %）／ ハギの魂（ターンの頭）／ 魂の出どころ（1戦・上限の前）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 勇者の HP T1〜T8（%）| 魂 T1〜T8 | 敵の死 | 8 割 | 5 割 | 2 割 |");
        Console.WriteLine("|---|--:|---|---|--:|--:|--:|--:|");
        foreach (var b in StopBoards().Concat(CarryHand()))
        {
            var x = S377.Measure(ColBoard(b, UnitCatalog.Hagi), W8[WBoss].Ws, Seeds, Extra(true, $"p0:{b.Name}"));
            string hh = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"heroHeadN{t}") == 0 ? "—" : F0((double)X(x, $"heroHead{t}") / X(x, $"heroHeadN{t}"))));
            string sl = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"halive{t}") == 0 ? "—" : F1((double)X(x, $"soul{t}") / X(x, $"halive{t}"))));
            Console.WriteLine($"| {b.Name} | {F1(x.Win)} | {hh} | {sl} | {Per(X(x, "srcDeath"), x.N)} | {Per(X(x, "src80"), x.N)} | {Per(X(x, "src50"), x.N)} | {Per(X(x, "src20"), x.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 2. 波ごとの敵の数と、最大 HP 1,000 以上の敵（戦いの始まりの値・A の対象）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 敵の数 | 1,000 以上 | 最大 HP（多い順）|");
        Console.WriteLine("|---|--:|--:|---|");
        var waves = new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M4, I.M5, I.Nine }.Concat(I.StrongWaves)
            .Concat(Enumerable.Range(1, EnemyCatalog.Stages.Count).Where(i => i is 1 or 3).Select(i => new I.W($"本編 第{i}波", () => BattleEngine.Materialize(EnemyCatalog.Stages[i - 1].Enemy, BattleContext.EnemyTeam, EnemyScaleRule.Default), EnemyScaleRule.Default)));
        foreach (var w in waves)
        {
            var e = w.Make();
            Console.WriteLine($"| {w.Name} | {e.Count} | {e.Count(u => u.MaxHp >= HagiDepthTrait.BigHp)} | {string.Join("・", e.OrderByDescending(u => u.MaxHp).Select(u => $"{u.Def.Name} {u.MaxHp:#,0}"))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-1 の 1〜3: 台 × 版 × 8 物差し
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §4-1 の 1〜3 台 × 版（{string.Join(" ／ ", Vers.Select(v => v.Name))}）× 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        var sets = new (string Title, S388.Board[] B)[] { ("A. キャリーの手組み（火力の無い支えで固めた台）", CarryHand()), ("B. 止め役 ／ キャリーの台", StopBoards()), ("C. 死の台（B の台）", DeathBoards()) };
        foreach (var (title, boards) in sets)
        {
            Console.WriteLine($"## {title}");
            Console.WriteLine();
            Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(ColBoard(b, UnitCatalog.Hagi))}・ハギは {b.Slot switch { 0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", _ => "後3" }}）")) + "。");
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

        Console.WriteLine("### 2. 魂（ターンの頭 T1〜T8・生きていた戦の平均）と魂の出どころ（1戦・上限の前の量: 敵の死 ／ 線 8・5・2 割 ／ A の上乗せ ／ D の印 ／ B の味方の死）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | T1〜T8 | 敵の死 | 線（8 ／ 5 ／ 2）| A | D | B |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
                foreach (int w in new[] { WBoss, WGuard, WBat, WStrong })
                {
                    var x = g[bi, vi][w];
                    string tl = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"halive{t}") == 0 ? "—" : F1((double)X(x, $"soul{t}") / X(x, $"halive{t}"))));
                    Console.WriteLine($"| {(vi == 0 && w == 0 ? names[bi] : "")} | {(w == 0 ? Vers[vi].Name : "")} | {W8[w].Name} | {tl} | {Per(X(x, "srcDeath"), x.N)} | {Per(X(x, "src80"), x.N)} ／ {Per(X(x, "src50"), x.N)} ／ {Per(X(x, "src20"), x.N)} | {Per(X(x, "srcBig"), x.N)} | {Per(X(x, "srcMark"), x.N)} | {Per(X(x, "srcAlly"), x.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("### 3. 魂が 9 に届いた戦 %（T の平均）——雑魚で溜まりすぎていないか（本編 第2 ／ 4 ／ 5波 ／ 九体 ／ 強 ／ 精鋭 ／ ボス）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", W8.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
                Console.WriteLine($"| {(vi == 0 ? names[bi] : "")} | {Vers[vi].Name} | {string.Join(" | ", g[bi, vi].Select(x => $"{Pc(X(x, "s9"), x.N)}（T{Per(X(x, "s9T"), X(x, "s9"))}）"))} |");
        Console.WriteLine();

        Console.WriteLine("### 4. ボス: ハギ → 勇者 1ターンの最大（1戦の最大の平均 ／ 全戦の最大）・1,200 を超えたターンがあった戦・勇者の止め（ハギ ／ リィカ ／ ほか %）・勇者の HP（ターンの頭 T1〜T6・%）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 勝率 | ハギ → 勇者（平均 ／ 最大）| 1,200 超え | 勇者の止め（ハギ ／ リィカ ／ ほか）| 勇者の HP T1〜T6 |");
        Console.WriteLine("|---|---|--:|---|--:|---|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
            {
                var x = g[bi, vi][WBoss];
                long hn = X(x, "heroKRica") + X(x, "heroKHagi") + X(x, "heroKOther");
                string hh = string.Join(" ", Enumerable.Range(1, 6).Select(t => X(x, $"heroHeadN{t}") == 0 ? "—" : F0((double)X(x, $"heroHead{t}") / X(x, $"heroHeadN{t}"))));
                Console.WriteLine($"| {(vi == 0 ? names[bi] : "")} | {Vers[vi].Name} | {F1(x.Win)} | {Per(X(x, "heroHagiTurn"), x.N)} ／ {F0(S401.Mx(Lab(bi, vi) + "|0:heroHagiTurn"))} | {Pc(X(x, "heroHagiOver"), x.N)}% | {Pc(X(x, "heroKHagi"), hn)} ／ {Pc(X(x, "heroKRica"), hn)} ／ {Pc(X(x, "heroKOther"), hn)} | {hh} |");
            }
        Console.WriteLine();

        Console.WriteLine("### 5. D: 印を刻み切った回数（1戦）・そのとき戻した印 4 本ぶんの H1 の名目の足し分（＝刻み切った直後の一撃で減る量）・H1 の足し分の合計（1戦）・ハギの与ダメ");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 刻み切った | 戻した足し分 | H1 の足し分 | ハギの与ダメ | 隊に占める |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
            {
                if (Vers[vi].Name is not ("規定" or "D" or "AD")) continue;
                foreach (int w in new[] { WBoss, WGuard, WBat, WStrong })
                {
                    var x = g[bi, vi][w];
                    Console.WriteLine($"| {(vi == 0 && w == 0 ? names[bi] : "")} | {(w == 0 ? Vers[vi].Name : "")} | {W8[w].Name} | {Per(X(x, "markSouls"), x.N)} | {Per(X(x, "markLost"), x.N)} | {Per(X(x, "markAdd"), x.N)} | {Per(X(x, "dAll"), x.N)} | {Pc(X(x, "hagiEv"), X(x, "teamDmg"))}% |");
                }
            }
        Console.WriteLine();

        Console.WriteLine("### 6. B: 味方の死で入った魂（1戦）と、リィカの層（ターンの頭 T1〜T6・生きていた戦の平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 味方の死の魂 | 魂 T1〜T6 | リィカの層 T1〜T6 |");
        Console.WriteLine("|---|---|---|--:|---|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
            {
                if (Vers[vi].Name is not ("規定" or "B")) continue;
                foreach (int w in new[] { WBoss, WGuard, WBat })
                {
                    var x = g[bi, vi][w];
                    string tl = string.Join(" ", Enumerable.Range(1, 6).Select(t => X(x, $"halive{t}") == 0 ? "—" : F1((double)X(x, $"soul{t}") / X(x, $"halive{t}"))));
                    Console.WriteLine($"| {(vi == 0 && w == 0 ? names[bi] : "")} | {(w == 0 ? Vers[vi].Name : "")} | {W8[w].Name} | {Per(X(x, "srcAlly"), x.N)} | {tl} | {(x.RicaIn > 0 ? x.Layers(6) : "—")} |");
                }
            }
        Console.WriteLine();

        Console.WriteLine("### 7. 誰が何Tに倒れたか（規定 ／ 4 波の平均がいちばん良い版・ボス ／ 近衛 ／ 大隊）");
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

        Console.WriteLine("### 8. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上（規定と比べて）／ あふれ）と、ハギの一撃の最大（ボス ／ 近衛 ／ 大隊 ／ 強）");
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
        Console.WriteLine("| 台 | " + string.Join(" | ", Vers.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"| {names[bi]} | {string.Join(" | ", Enumerable.Range(0, nv).Select(vi => string.Join(" ／ ", new[] { WBoss, WGuard, WBat, WStrong }.Select(w => F0(S401.Mx($"{Lab(bi, vi)}|{w}:hitMax"))))))} |");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §4-1 の 4: `compare` のハギの行
    // ---------------------------------------------------------------------------------
    static void Cmp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().Where(r => Has(r.F, "hagi")).ToArray();
        int nw = EnemyCatalog.Stages.Count;
        var grids = Vers.Select(c => CGrid(rows.Select(r => (r.Name, I.Swap(r.F, "hagi", c.D!))).ToArray(), CompareSeeds)).ToArray();
        Console.WriteLine($"# {Tag} §4-1 の 4 `compare` のハギの行 —— {rows.Length} 行 × 版（第1〜5波・seed 0..{CompareSeeds - 1}・ハギだけ替える）");
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
        // (G2): いずれかの波で −10pt 以上落ちた行の駒それぞれについて、その駒を含む「他の」行（`compare` 全行）の全波平均の変化。ハギのいない行は版で動かないので差 0 として数える。
        var all = CompareBuilds();
        var dropRows = new List<string>();
        for (int c = 1; c < Vers.Length; c++)
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (!Enumerable.Range(0, nw).Any(w => grids[c][ri, w] - grids[0][ri, w] <= -10)) continue;
                var ids = rows[ri].F.Occupied().Select(o => o.Def.Id).Distinct().ToList();
                var parts = new List<string>(); bool broken = false;
                foreach (var id in ids)
                {
                    var others = all.Where(r => r.Name != rows[ri].Name && r.F.Occupied().Any(o => o.Def.Id == id)).ToList();
                    if (others.Count == 0) { parts.Add($"{id}: 他の行 0（分解が成立しない）"); continue; }
                    double sum = 0;
                    foreach (var o in others)
                    {
                        int k = Array.FindIndex(rows, r => r.Name == o.Name);
                        if (k >= 0) sum += Enumerable.Range(0, nw).Average(w => grids[c][k, w] - grids[0][k, w]);
                    }
                    double dlt = sum / others.Count;
                    if (dlt <= -3.0) broken = true;
                    parts.Add($"{Som307Diag.Short(UnitCatalog.ById(id))} {Sg(dlt)}（他の {others.Count} 行）");
                }
                dropRows.Add($"- **{Vers[c].Name} × {rows[ri].Name}**（{string.Join("・", Enumerable.Range(0, nw).Where(w => grids[c][ri, w] - grids[0][ri, w] <= -10).Select(w => $"第{w + 1}波 {F1(grids[0][ri, w])} → {F1(grids[c][ri, w])}"))}）: {string.Join("・", parts)} → **{(broken ? "壊れ（いずれかの駒で −3.0pt 以下）" : "編成上の制約（どの駒も |変化| < 3.0pt）")}**");
            }
        Console.WriteLine("## (G2) −10pt 以上落ちた行の分解");
        Console.WriteLine();
        if (dropRows.Count == 0) Console.WriteLine("- 無し。");
        foreach (var l in dropRows) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Log1(string[] args)
    {
        string bn = args.Length > 3 ? args[3] : "試遊・ハギ キャリー";
        string cn = args.Length > 4 ? args[4] : "A";
        int wi = args.Length > 5 && int.TryParse(args[5], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 6 && int.TryParse(args[6], out int s0) ? s0 : 0;
        var all = CarryHand().Concat(StopBoards()).Concat(DeathBoards()).ToList();
        var b = all.FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal)) ?? all[0];
        var c = Vers.FirstOrDefault(x => x.Name == cn) is { Name: not null } cc ? cc : Vers[1];
        var (r1, _, _) = I.Raw(ColBoard(b, c.D), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name} × {c.Name} × {W8[wi].Name} × seed {seed}: {(r1.PlayerWon ? "勝ち" : "負け")}・{r1.Turns}T");
        foreach (var l in r1.Log) Console.WriteLine(l.Text);
    }
}
