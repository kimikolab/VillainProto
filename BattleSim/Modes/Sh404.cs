using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S388 = Sh388Diag;
using S390 = Sh390Diag;
using S401 = Sh401Diag;
using S403 = Sh403Diag;

// =====================================================================================
// sh404 —— 第404期【軽い期】段0: ハギの T1 を規定に ／ 本体: 斬った直後に刈る ＋ 線の上限を下げる（版 L45 ／ R50 ／ R45）。本体の版は規定にしない。
// 指示書は design/PHASE404_HAGI_AFTER_REAP_SPEC.md、報告は design/PHASE404_HAGI_AFTER_REAP.md。
//
//     dotnet run --project BattleSim -c Release 0 sh404 zero      # §2 段0: `compare` の動き（第403期の表と ／ 第403期の T1 の列と）＋ 試遊の 5 台 × 規定 ／ 旧 × 5 物差し
//     dotnet run --project BattleSim -c Release 0 sh404 boards    # §4-1 の 1〜3: 試遊の 5 台 ／ キャリーの手組み 2 × 列（規定 ／ L45 ／ R50 ／ R45）× 8 物差し
//     dotnet run --project BattleSim -c Release 0 sh404 cmp       # §4-1 の 4: ハギのいる `compare` の行 × 版（seed 0..199）＋ (G2)
//     dotnet run --project BattleSim -c Release 0 sh404 log <台> <列> <物差し 0..7> <seed>   # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 sh404 check     # 自己検査
//
// 替えた台は器具の中だけ（試遊の台に足す「版・刈り続け ハギ」を除く）。**予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p404/predict.md`）。
// =====================================================================================
static partial class Sh404Diag
{
    internal const string PredictFile = ".tmp/p404/predict.md";
    const string Tag = "第404期";
    const int Seeds = 100, CompareSeeds = 200, TT = 9;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "zero" or "boards" or "cmp" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh404: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
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
            default: Console.WriteLine("sh404: モードは zero / boards / cmp / log / check。"); return;
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
        ("規定", UnitCatalog.HagiH404), ("L45", UnitCatalog.HagiReapL45), ("R50", UnitCatalog.HagiReapR50), ("R45", UnitCatalog.HagiReapR45),   // 第405期: 規定の列は第404期の規定（旧 `HagiH404`）
    };
    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    static readonly int[] W4 = { 0, 1, 2, 5 };
    const int WBoss = 0, WGuard = 1, WBat = 2, WStrong = 7;

    /// <summary>§4-1 の 1: 試遊・ハギ 台B ／ 死神と墓守 ／ キャリー ／ 哀歌 ／ 版・移動 ハギ。</summary>
    internal static S388.Board[] StopBoards()
    {
        var b = S401.StopBoards();
        return new[] { b[0], b[1], new S388.Board("試遊・ハギ キャリー", Presets.HagiCarry401(), 3, "ハギ"), new S388.Board("試遊・ハギ 哀歌", Presets.HagiTwin403(), 3, "ハギ"), b[2] }
            .Select(x => new S388.Board(x.Name, Pin404(x.F), x.Slot, x.Who)).ToArray();   // 第405期: 台のハギは第404期の規定に
    }
    /// <summary>§4-1 の 2: キャリーの手組みのうち支えだけの 2 台（灯と号令 ／ 移り木）。</summary>
    internal static S388.Board[] CarryHand() => S401.CarryBoards().Take(2).Select(x => new S388.Board(x.Name, Pin404(x.F), x.Slot, x.Who)).ToArray();   // 第405期: ハギは第404期の規定に

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

    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> Max = new();
    static void NoteMax(string k, long v) => Max.AddOrUpdate(k, v, (_, o) => Math.Max(o, v));

    /// <summary>第403期の計数 ＋ ハギの1手番の撃破数 ／ 当たった後の刈り取り ／ 勇者を刈ったときの線。</summary>
    static Action<BattleResult, List<UnitState>, List<UnitState>, S377.B> Extra(bool boss, string label)
    {
        var baseX = S403.Extra(boss, label);
        return (r, p, e, b) =>
        {
            baseX(r, p, e, b);
            if (!r.TallyByUnit.TryGetValue("hagi", out var h) || !p.Any(u => u.Def.Id == "hagi")) return;
            Add(b, "after", h.R404AfterReaps); Add(b, "afterKills", h.R404AfterReapKills); Add(b, "afterChase", h.R404AfterReapsChase); Add(b, "afterKillsChase", h.R404AfterReapKillsChase);
            Add(b, "dAfter", h.R404DmgAfter); Add(b, "dAfterChase", h.R404DmgAfterChase);
            if (h.R404TurnKills is { } tk) for (int k = 0; k < tk.Length; k++) Add(b, $"tk{k}", tk[k]);
            NoteMax(label + ":turnKillMax", h.R404TurnKillMax);
            if (boss && X(b, "heroReaped") > 0 && h.R401FirstKillTurn > 0) { Add(b, "heroReapLine", h.R389LastReapLine); }
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
        Console.WriteLine($"# {Tag} §2 段0 —— ハギの T1（孤立した敵は二度斬る）を規定に（旧は `UnitCatalog.HagiH403`・H123 ＋ B）");
        Console.WriteLine();
        var rows = CompareBuilds().Select(r => (r.Name, F: Pin404(r.F))).ToArray();   // 第405期: ハギは第404期の規定に
        var now = CGrid(rows, CompareSeeds);
        var old = CGrid(rows.Select(r => (r.Name, F: Pin403(r.F))).ToArray(), CompareSeeds);
        var tcol = CGrid(rows.Select(r => (r.Name, F: I.Swap(Pin403(r.F), "hagi", UnitCatalog.HagiSingleT1))).ToArray(), CompareSeeds);
        var head = S390.BalanceMap(S390.GitShow("350ebf20:docs/balance.md"));
        int nw = EnemyCatalog.Stages.Count, moved = 0, sameT = 0, sameOld = 0, cells = 0;
        var mv = new List<string>();
        for (int ri = 0; ri < rows.Length; ri++)
            for (int w = 0; w < nw; w++)
            {
                cells++;
                string c = head.TryGetValue(rows[ri].Name, out var a) ? a[w].TrimEnd('%') : "?";
                if (c != F1(now[ri, w])) { moved++; mv.Add($"{rows[ri].Name} 第{w + 1}波 {c} → {F1(now[ri, w])}"); }
                if (now[ri, w] == tcol[ri, w]) sameT++;
                if (c == F1(old[ri, w])) sameOld++;
            }
        Console.WriteLine($"- 第403期の HEAD（350ebf20）の `docs/balance.md` と違うセル **{moved}**{(mv.Count > 0 ? "（" + string.Join("・", mv) + "）" : "")}");
        Console.WriteLine($"- 第403期の T1 の列（ハギを `HagiSingleT1` に替えた全行）と同じセル {sameT} ／ {cells}・旧（`HagiH403`）に戻した全行が HEAD の表と同じセル {sameOld} ／ {cells}");
        Console.WriteLine();
        Console.WriteLine($"（seed 0..{Seeds - 1}・セルは「勝率・決着T・倒れた編成の駒」・ボスの T はボスの倒しT）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | ボス | 近衛 | 大隊 | 本編 第5波 | 強 第2〜5波 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (var b in StopBoards())
            foreach (var (cn, d) in new[] { ("規定（第404期）", UnitCatalog.HagiH404), ("第403期（旧）", UnitCatalog.HagiH403) })
            {
                var m = M5(ColBoard(b, d), $"zero:{b.Name}:{cn}");
                Console.WriteLine($"| {(cn.StartsWith("規定") ? b.Name : "")} | {cn} | {string.Join(" | ", new[] { WBoss, WGuard, WBat, 5, WStrong }.Select(w => m[w].Cell))} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-1: 台 × 版 × 8 物差し
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §4-1 台 × 版（{string.Join(" ／ ", Vers.Select(v => v.Name))}）× 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var (title, boards) in new (string, S388.Board[])[] { ("A. 試遊の台（止め役 ／ キャリー ／ 哀歌 ／ 移動）", StopBoards()), ("B. キャリーの手組み（支えだけの台）", CarryHand()) })
        {
            Console.WriteLine($"## {title}");
            Console.WriteLine();
            Console.WriteLine("台: " + string.Join("・", boards.Select(b => $"{b.Name}（{S307.Seats(ColBoard(b, UnitCatalog.HagiH404))}）")) + "。");
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

        Console.WriteLine("### 2. 連続で狩るか（1戦）: ハギの手番のうち倒した数が 0 ／ 1 ／ 2 ／ 3+ 体の割合・再行動・当たった後の刈り取り（回 ／ 倒した・うち追撃）・ハギの撃破が隊の撃破に占める割合");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 決着T | 手番の撃破 0 ／ 1 ／ 2 ／ 3+（%）| 再行動 | 当たった後の刈り取り（回 ／ 倒した ／ 追撃）| ハギの撃破 ／ 隊 |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
                foreach (int w in new[] { WGuard, WBat, 3, 4, 5, 6, WStrong })
                {
                    var x = g[bi, vi][w];
                    long tot = Enumerable.Range(0, 10).Sum(k => X(x, $"tk{k}"));
                    string dist = $"{Pc(X(x, "tk0"), tot)} ／ {Pc(X(x, "tk1"), tot)} ／ {Pc(X(x, "tk2"), tot)} ／ {Pc(Enumerable.Range(3, 7).Sum(k => X(x, $"tk{k}")), tot)}";
                    Console.WriteLine($"| {(vi == 0 && w == WGuard ? names[bi] : "")} | {(w == WGuard ? Vers[vi].Name : "")} | {W8[w].Name} | {x.WinTs} | {dist} | {Per(X(x, "encores"), x.N)} | {Per(X(x, "after"), x.N)} ／ {Per(X(x, "afterKills"), x.N)} ／ {Per(X(x, "afterChase"), x.N)} | {Per(X(x, "kHagi"), x.N)} ／ {Per(X(x, "kAll"), x.N)}（{Pc(X(x, "kHagi"), X(x, "kAll"))}%）|");
                }
        Console.WriteLine();

        Console.WriteLine("### 3. ボス: 倒しT・ハギ → 勇者 1ターンの最大（平均 ／ 最大）・勇者の止め（ハギ ／ リィカ ／ ほか %）・ハギが刈って止めた戦と、その刈り取りの種類（手番の刈り取り ／ 追撃の刈り取り ／ 当たった後（手番）／ 当たった後（追撃）・%）・刈ったときの線");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 勝率 | 倒しT | ハギ → 勇者（平均 ／ 最大）| 勇者の止め | 刈って止めた | 種類（手番 ／ 追撃 ／ 当たった後 ／ 当たった後・追撃 ／ 斬撃 ・ 薙ぎ ・ 追撃 %）| 線 |");
        Console.WriteLine("|---|---|--:|--:|---|---|--:|---|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
            {
                var x = g[bi, vi][WBoss];
                long hn = X(x, "heroKRica") + X(x, "heroKHagi") + X(x, "heroKOther"), fk = X(x, "fk"), hr = X(x, "heroReaped");
                string kinds = $"{Pc(X(x, "fkK4"), fk)} ／ {Pc(X(x, "fkK5"), fk)} ／ {Pc(X(x, "fkK6"), fk)} ／ {Pc(X(x, "fkK7"), fk)} ／ {Pc(X(x, "fkK1") + X(x, "fkK2") + X(x, "fkK3"), fk)}";
                Console.WriteLine($"| {(vi == 0 ? names[bi] : "")} | {Vers[vi].Name} | {F1(x.Win)} | {x.WinTs} | {Per(X(x, "heroHagiTurn"), x.N)} ／ {F0(S401.Mx(Lab(bi, vi) + "|0:heroHagiTurn"))} | {Pc(X(x, "heroKHagi"), hn)} ／ {Pc(X(x, "heroKRica"), hn)} ／ {Pc(X(x, "heroKOther"), hn)} | {Pc(hr, x.N)}% | {kinds} | {(hr == 0 ? "—" : F1(X(x, "heroReapLine") / 10.0 / hr) + "%")} |");
            }
        Console.WriteLine();

        Console.WriteLine("### 4. 刈り取りの必殺感: ハギの与ダメ（敵に入った量・1戦）と、そのうち刈り取りの割合（手番 ／ 追撃 ／ 当たった後（手番）／ 当たった後（追撃）・%）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | ハギの与ダメ | 刈り取り合計 | 手番 | 追撃 | 当たった後 | 当たった後・追撃 | 隊に占める |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
                foreach (int w in new[] { WBoss, WGuard, WBat, 5, 6 })
                {
                    var x = g[bi, vi][w];
                    long all = X(x, "dAll"), rr = X(x, "dR"), cc = X(x, "dC"), aa = X(x, "dAfter"), ac = X(x, "dAfterChase");
                    Console.WriteLine($"| {(vi == 0 && w == 0 ? names[bi] : "")} | {(w == 0 ? Vers[vi].Name : "")} | {W8[w].Name} | {Per(all, x.N)} | {Pc(rr + cc + aa + ac, all)}% | {Pc(rr, all)} | {Pc(cc, all)} | {Pc(aa, all)} | {Pc(ac, all)} | {Pc(X(x, "hagiEv"), X(x, "teamDmg"))}% |");
                }
        Console.WriteLine();

        Console.WriteLine("### 5. 誰が何Tに倒れたか（規定 ／ 4 波の平均がいちばん良い版・ボス ／ 近衛 ／ 大隊）");
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

        Console.WriteLine("### 6. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上（規定と比べて）／ あふれ）と、ハギの1手番の最大撃破数（近衛 ／ 大隊 ／ 第5波 ／ 九体 ／ 強）");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int vi = 0; vi < nv; vi++)
            {
                string at = $"{names[bi]} × {Vers[vi].Name}";
                foreach (int w in new[] { WGuard, WBat }) { var x = g[bi, vi][w]; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{at} × {W8[w].Name}: 1T 決着 {Pc(x.Win1T, x.N)}%"); }
                { var x = g[bi, vi][WBoss]; if (x.BossFast > 0) signs.Add($"{at} × ボス: 4T 未満の勝ち {Pc(x.BossFast, x.N)}%"); }
                if (vi == 0) continue;
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
            Console.WriteLine($"| {names[bi]} | {string.Join(" | ", Enumerable.Range(0, nv).Select(vi => string.Join(" ／ ", new[] { WGuard, WBat, 5, 6, WStrong }.Select(w => Max.GetValueOrDefault($"{Lab(bi, vi)}|{w}:turnKillMax").ToString()))))} |");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §4-1 の 4: `compare` のハギの行 ＋ (G2)
    // ---------------------------------------------------------------------------------
    static void Cmp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().Where(r => Has(r.F, "hagi")).Select(r => (r.Name, F: Pin404(r.F))).ToArray();   // 第405期
        int nw = EnemyCatalog.Stages.Count;
        var grids = Vers.Select(c => CGrid(rows.Select(r => (r.Name, I.Swap(r.F, "hagi", c.D))).ToArray(), CompareSeeds)).ToArray();
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
        string bn = args.Length > 3 ? args[3] : "試遊・ハギ キャリー";
        string cn = args.Length > 4 ? args[4] : "R45";
        int wi = args.Length > 5 && int.TryParse(args[5], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 6 && int.TryParse(args[6], out int s0) ? s0 : 0;
        var all = StopBoards().Concat(CarryHand()).ToList();
        var b = all.FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal)) ?? all[0];
        var c = Vers.FirstOrDefault(x => x.Name == cn) is { Name: not null } cc ? cc : Vers[1];
        var (r1, _, _) = I.Raw(ColBoard(b, c.D), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name} × {c.Name} × {W8[wi].Name} × seed {seed}: {(r1.PlayerWon ? "勝ち" : "負け")}・{r1.Turns}T");
        foreach (var l in r1.Log) Console.WriteLine(l.Text);
    }
}
