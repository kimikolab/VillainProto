using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S392 = Sh392Diag;
using S394 = Sh394Diag;

// =====================================================================================
// sh395 —— 第395期【軽い期】本体: ドルガ——壁で倒れない（版 E1 ／ E2 ／ E12・第394期の D2 の上）。規定は変えない。
// 指示書は design/PHASE395_DOLGA_WALL_TRIM_SPEC.md、報告は design/PHASE395_DOLGA_WALL_TRIM.md。
//
//     dotnet run --project BattleSim -c Release 0 sh395 boards       # §3-3 の 1〜6・8: 台 × 列（規定 ／ D2 ／ E1 ／ E2 ／ E12 ／ 素体）× 8 物差し（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh395 cmp          # §3-3 の 7: `compare` 全 66 行 × 列（規定 ／ D2 ／ E1 ／ E2 ／ E12・第1〜5波・seed 0..199）＋ (G2) の分け
//     dotnet run --project BattleSim -c Release 0 sh395 log <台の頭> <列> <物差し 0..7> <seed>
//     dotnet run --project BattleSim -c Release 0 sh395 check        # 自己検査
//
// **規定の駒は1つも変えない。** 替えた台 ／ 素体は器具の中だけ（試遊の台に足す「版・壁 E」を除く）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p395/predict.md`）。
// 台は第394期の `sh394` と同じ（`Sh394Diag.BoardList`）。「版・壁 ドルガ」は「試遊・手番 芯3枚」の D2 の列と同じ台なので、別の台として並べない。
// =====================================================================================
static partial class Sh395Diag
{
    internal const string PredictFile = ".tmp/p395/predict.md";
    const string Tag = "第395期";
    const int Seeds = 100, CompareSeeds = 200, TT = 7;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "boards" or "cmp" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh395: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "boards": Boards(); return;
            case "cmp": Cmp(); return;
            case "log": Log1(args); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh395: モードは boards / cmp / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => double.IsNaN(x) ? "—" : x.ToString("#,0");
    static string Sg(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pc(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => string.Join(" ／ ", Enumerable.Range(0, 5).Select(s => f[s] is { } d ? Short(d) : "—"));
    static string Nm(string id) => UnitCatalog.Everyone.FirstOrDefault(d => d.Id == id) is { } d ? Short(d) : id switch { "spore" => "胞子", "wight" => "亡者", _ => id };
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }

    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    static readonly int[] W4 = { 0, 1, 2, 5 };
    const int WBoss = 0, WGuard = 1, WBat = 2, WM5 = 5, WStrong = 7;
    const int Main = 5;   // §3-1 の 1・2 のうち、全部の版を並べる台（芯 3 枚 ／ 探索1位 ／ 灯×薙ぎ ／ 縛め収入型・溜め改（トモ））

    // ---------------------------------------------------------------------------------
    // 版 ／ 列 ／ 台
    // ---------------------------------------------------------------------------------
    internal static readonly (string Name, UnitDef D)[] Vers =
        { ("規定", UnitCatalog.Dolga), ("D2", UnitCatalog.DolgaD2), ("E1", UnitCatalog.DolgaE1), ("E2", UnitCatalog.DolgaE2), ("E12", UnitCatalog.DolgaE12) };
    /// <summary>列: 規定 ／ D2 ／ E1 ／ E2 ／ E12 ／ ドルガを素体に替えた台（第392〜394期と同じ素体）。</summary>
    internal static (string Name, UnitDef D)[] Cols => Vers.Append(("素体", S394.PlainDolga)).ToArray();
    internal static (string Name, Formation F, int Slot)[] BoardList() => S394.BoardList();

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    static void Add(S377.B b, string k, long v) { if (v != 0) b.X[k] = b.X.GetValueOrDefault(k) + v; }
    static long X(S377.B b, string k) => b.X.GetValueOrDefault(k);

    static readonly Action<BattleResult, List<UnitState>, List<UnitState>, S377.B> Extra = (r, p, e, b) =>
    {
        var form = p.ToDictionary(u => u.InstanceId);
        var foe = e.ToDictionary(u => u.InstanceId);
        var sumTeam = new Dictionary<int, int>();
        var dolga = p.FirstOrDefault(u => u.Def.Id == "dolga");
        int did = dolga?.InstanceId ?? -9;
        bool Foe(int id) => foe.ContainsKey(id) || (sumTeam.TryGetValue(id, out int st) && st == BattleContext.EnemyTeam);
        var dmgT = new long[BattleEngine.MaxTurns + 2];
        var deadAt = new Dictionary<int, int>();
        int firstT = -1, swings = 0; long snapBonus = 0, firstBonus = 0, firstDmg = 0, bonusMax = 0;
        foreach (var x in r.Events)
        {
            int t = Math.Clamp(x.Turn, 0, BattleEngine.MaxTurns + 1);
            int tgt = x.TargetId ?? -3, act = x.ActorId ?? -2;
            switch (x.Kind)
            {
                case BattleEventKind.Summon: sumTeam[tgt] = x.Team ?? -1; break;
                case BattleEventKind.Attack:
                    if (act == did)
                    {
                        swings++; Add(b, "dSwing", 1);
                        if (x.Pattern == AttackPattern.All) Add(b, "dAll", 1);
                        if (firstT < 0) { firstT = t; firstBonus = snapBonus; }
                    }
                    break;
                case BattleEventKind.Damage:
                {
                    bool byUs = form.ContainsKey(act) || (sumTeam.TryGetValue(act, out int sa) && sa == BattleContext.PlayerTeam);
                    if (Foe(tgt) && byUs) dmgT[t] += x.Amount;
                    if (act == did && Foe(tgt)) { Add(b, "dDmg", x.Amount); if (t == firstT) firstDmg += x.Amount; }
                    if (tgt == did) Add(b, "dTaken", x.Amount);
                    break;
                }
                case BattleEventKind.StatSnapshot:
                    if (tgt == did) { snapBonus = x.Amount - dolga!.Def.Attack; bonusMax = Math.Max(bonusMax, snapBonus); }
                    break;
                case BattleEventKind.Death:
                    if (form.ContainsKey(tgt) && !deadAt.ContainsKey(tgt)) deadAt[tgt] = t;
                    if (Foe(tgt)) { Add(b, "kAll", 1); Add(b, act == did ? "kDolga" : form.ContainsKey(act) ? "kPartner" : "kOther", 1); }
                    break;
                case BattleEventKind.Revive: deadAt.Remove(tgt); break;
            }
        }
        Add(b, "maxTurnDmg", dmgT.Max());
        if (dolga is null) return;
        Add(b, "dBonusMax", bonusMax);
        lock (MaxLock) if (bonusMax > _bonusPeak) _bonusPeak = bonusMax;
        if (firstT > 0) { Add(b, "firstN", 1); Add(b, "firstT", firstT); Add(b, "firstBonus", firstBonus); Add(b, "firstDmg", firstDmg); }
        if (deadAt.TryGetValue(did, out int dt)) { Add(b, "dFellN", 1); Add(b, "dFellT", dt); if (swings == 0) Add(b, "dFellUnswung", 1); }
        foreach (var (id, tl) in r.TallyByUnit)
        {
            if (id != "dolga") continue;
            Add(b, "wallN", tl.R394WallHits); Add(b, "wallAmt", tl.R394WallAmt);
            if (tl.R394WallByTurn is { } byT) for (int t = 1; t < TT && t < byT.Length; t++) Add(b, $"wallT{t}", byT[t]);
            Add(b, "retreat", tl.R395WallRetreats);
            if (tl.R395RetreatByTurn is { } rb)
            {
                int first = Array.FindIndex(rb, v => v > 0);
                if (first > 0) { Add(b, "retreatBattles", 1); Add(b, "retreatFirstT", first); }
            }
        }
    };

    static readonly object MaxLock = new();
    static long _bonusPeak;
    static S377.B[] M8(Formation f)
    {
        var g = new S377.B[W8.Length];
        Parallel.For(0, W8.Length, wi => g[wi] = S377.Measure(f, W8[wi].Ws, Seeds, Extra));
        return g;
    }
    static double A4(S377.B[] a) => W4.Average(wi => a[wi].Win);

    // ---------------------------------------------------------------------------------
    // §3-3 の 1〜6・8: 台 × 列
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        var cols = Cols;
        int nb = boards.Length, nc = cols.Length, iD2 = 1;
        var g = new S377.B[nb, nc][];
        for (int bi = 0; bi < nb; bi++) for (int ci = 0; ci < nc; ci++) g[bi, ci] = M8(Put(boards[bi].F, boards[bi].Slot, cols[ci].D));
        int[] w5 = { WBoss, WGuard, WBat, WM5, WStrong };
        bool MainBoard(int bi) => bi < Main;

        Console.WriteLine($"# {Tag} §3-3 台 {nb} × 列（{string.Join(" ／ ", cols.Select(c => c.Name))}）× 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var bd in boards) Console.WriteLine($"- **{bd.Name}**: {Seats(bd.F)}（ドルガの席 {bd.Slot}）");
        Console.WriteLine();

        Console.WriteLine("## 1. 勝率・決着T・倒れた編成の駒 ／ 4 波（ボス ／ 近衛 ／ 大隊 ／ 本編 第5波）の平均 ／ 隊の1ターンの最大（ボス ／ 近衛 ／ 大隊）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 | 規定との差 | D2 との差（強）| 1ターンの最大 |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|--:|--:|---|");
        string MaxT(S377.B[] x) => string.Join(" ／ ", new[] { WBoss, WGuard, WBat }.Select(w => F0((double)X(x[w], "maxTurnDmg") / Math.Max(1, x[w].N))));
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {cols[ci].Name} | {string.Join(" | ", g[bi, ci].Select(x => x.Cell))} | {F1(A4(g[bi, ci]))} | {(ci == 0 ? "—" : Sg(A4(g[bi, ci]) - A4(g[bi, 0])))} | {(ci <= iD2 || ci == nc - 1 ? "—" : Sg(g[bi, ci][WStrong].Win - g[bi, iD2][WStrong].Win))} | {MaxT(g[bi, ci])} |");
        Console.WriteLine();

        Console.WriteLine("## 2. 素体との差（いちばん大事）——各列 − ドルガを素体に替えた台（4 波 ／ 強い条件）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 素体（4 波 ／ 強）| " + string.Join(" | ", Vers.Select(v => v.Name + " − 素体")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Vers.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
        {
            var pl = g[bi, nc - 1];
            Console.WriteLine($"| {boards[bi].Name} | {F1(A4(pl))} ／ {F1(pl[WStrong].Win)} | {string.Join(" | ", Enumerable.Range(0, Vers.Length).Select(ci => $"{Sg(A4(g[bi, ci]) - A4(pl))} ／ {Sg(g[bi, ci][WStrong].Win - pl[WStrong].Win)}"))} |");
        }
        Console.WriteLine();
        Console.WriteLine("比べ: 第394期の芯 3 枚の台で D2 − 素体 は強い条件 +17.3・縛め収入型（トモ）の近衛は 規定 22 ／ D2 0。");
        Console.WriteLine();

        Console.WriteLine("## 3. 壁（1戦）——受けた回数 ／ 量・量 T1〜T6・**下がった回数（E1 ／ E12）・下がった戦 %（最初に下がったT）**・ドルガの被ダメ・ドルガが倒れた戦 %（T）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 回数 ／ 量 | 量 T1〜T6 | 下がった（戦 %・T）| ドルガの被ダメ | ドルガが倒れた（T）|");
        Console.WriteLine("|---|---|---|---|---|---|--:|---|");
        for (int bi = 0; bi < nb; bi++)
        {
            if (!MainBoard(bi)) continue;
            for (int ci = 0; ci < Vers.Length; ci++)
                foreach (int w in w5)
                {
                    var x = g[bi, ci][w];
                    bool wall = Vers[ci].D.Traits.Contains(TraitId.DolgaWall), ret = Vers[ci].D.Traits.Contains(TraitId.DolgaWallRetreat);
                    string byT = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => Per(X(x, $"wallT{t}"), x.N)));
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? boards[bi].Name : "")} | {(w == WBoss ? Vers[ci].Name : "")} | {W8[w].Name} | {(wall ? $"{Per(X(x, "wallN"), x.N)} ／ {Per(X(x, "wallAmt"), x.N)}" : "—")} | {(wall ? byT : "—")} | {(ret ? $"{Per(X(x, "retreat"), x.N)}（{Pc(X(x, "retreatBattles"), x.N)}%・T{Per(X(x, "retreatFirstT"), X(x, "retreatBattles"))}）" : "—")} | {Per(X(x, "dTaken"), x.N)} | {Pc(X(x, "dFellN"), x.N)}（T{Per(X(x, "dFellT"), X(x, "dFellN"))}）|");
                }
        }
        Console.WriteLine();

        Console.WriteLine("## 4. 食い合いが断てたか（1戦）——ドルガが**一度も振らずに倒れた戦 %**・振った回数（全体）・最初に振ったT ／ そのときの力 ／ そのターンの与ダメ・1振りの与ダメ・積まれた力の最大");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 振らずに倒れた | 振った（全体）| 最初: T ／ 力 ／ 与ダメ | 1振り | 力の最大 |");
        Console.WriteLine("|---|---|---|--:|---|---|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
        {
            if (!MainBoard(bi)) continue;
            for (int ci = 0; ci < Vers.Length; ci++)
                foreach (int w in w5)
                {
                    var x = g[bi, ci][w];
                    long fn = X(x, "firstN");
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? boards[bi].Name : "")} | {(w == WBoss ? Vers[ci].Name : "")} | {W8[w].Name} | {Pc(X(x, "dFellUnswung"), x.N)}% | {Per(X(x, "dSwing"), x.N)}（{Per(X(x, "dAll"), x.N)}）| T{Per(X(x, "firstT"), fn)} ／ {Per(X(x, "firstBonus"), fn)} ／ {(fn == 0 ? "—" : F0((double)X(x, "firstDmg") / fn))} | {(X(x, "dSwing") == 0 ? "—" : F0((double)X(x, "dDmg") / X(x, "dSwing")))} | {Per(X(x, "dBonusMax"), x.N)} |");
                }
        }
        Console.WriteLine();

        Console.WriteLine("## 5. 誰が何Tに倒れたか（規定 ／ D2 ／ 4 波 ＋ 強い条件がいちばん良い E の版・ボス ／ 近衛 ／ 大隊 ／ 強い条件。§3-1 の 1・2 の台は全部の版）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
        {
            int best = Enumerable.Range(2, Vers.Length - 2).OrderByDescending(ci => A4(g[bi, ci]) + g[bi, ci][WStrong].Win).First();
            Console.WriteLine($"- **{boards[bi].Name}**（最良の E {Vers[best].Name}・4 波 {F1(A4(g[bi, best]))} ／ D2 {F1(A4(g[bi, iD2]))} ／ 規定 {F1(A4(g[bi, 0]))}・強 {F1(g[bi, best][WStrong].Win)} ／ {F1(g[bi, iD2][WStrong].Win)} ／ {F1(g[bi, 0][WStrong].Win)}）");
            var show = MainBoard(bi) ? Enumerable.Range(0, Vers.Length).ToArray() : new[] { 0, iD2, best };
            foreach (int w in new[] { WBoss, WGuard, WBat, WStrong })
                foreach (int ci in show)
                    Console.WriteLine($"  - {W8[w].Name}: {Vers[ci].Name} {F1(g[bi, ci][w].Win)}%・{g[bi, ci][w].WinTs}T — {g[bi, ci][w].FellCell}");
        }
        Console.WriteLine();

        Console.WriteLine("## 6. 止めを刺したのは誰か（敵の撃破・ボス ／ 近衛 ／ 大隊・ドルガ ／ 相方 ／ ほか %）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", cols.Select(c => c.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
        {
            if (!MainBoard(bi) && bi >= 7) continue;
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(0, nc).Select(ci => string.Join(" ／ ", new[] { WBoss, WGuard, WBat }.Select(w => { var x = g[bi, ci][w]; long a = X(x, "kAll"); return $"{Pc(X(x, "kDolga"), a)}・{Pc(X(x, "kPartner"), a)}・{Pc(X(x, "kOther"), a)}"; }))))} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 8. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上（規定との差）／ あふれ）・ドルガの積まれた力の最大");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 1; ci < nc - 1; ci++)
            {
                string at = $"{boards[bi].Name} × {cols[ci].Name}";
                foreach (int w in new[] { WGuard, WBat }) { var x = g[bi, ci][w]; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{at} × {W8[w].Name}: 1T 決着 {Pc(x.Win1T, x.N)}%"); }
                if (g[bi, ci][WBoss].BossFast > 0) signs.Add($"{at} × ボス: 4T 未満の勝ち {Pc(g[bi, ci][WBoss].BossFast, g[bi, ci][WBoss].N)}%");
                for (int w = 0; w < W8.Length; w++)
                {
                    var x = g[bi, ci][w]; var x0 = g[bi, 0][w];
                    if (100.0 * x.Cap / x.N - 100.0 * x0.Cap / x0.N >= 5) signs.Add($"{at} × {W8[w].Name}: 30T の打ち切り {Pc(x0.Cap, x0.N)} → {Pc(x.Cap, x.N)}%");
                    if (x.Overflow > 0) signs.Add($"{at} × {W8[w].Name}: あふれの兆し {x.Overflow} 件");
                }
            }
        if (signs.Count == 0) Console.WriteLine("- 無し（版の列）。");
        foreach (var s in signs) Console.WriteLine("- " + s);
        Console.WriteLine($"- 全台 × 全列 × 全物差しの1戦で、ドルガの積まれた力のいちばん大きい値: {_bonusPeak}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-3 の 7: `compare` 全 66 行 ＋ (G2)
    // ---------------------------------------------------------------------------------
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
    internal static (string Name, Formation F)[] RowsFor(UnitDef d) => CompareBuilds().Select(r => (r.Name, Has(r.F, "dolga") ? I.Swap(r.F, "dolga", d) : r.F)).ToArray();

    static void Cmp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var all = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count;
        var grids = Vers.Select(v => CGrid(RowsFor(v.D), CompareSeeds)).ToArray();
        var dolgaRows = Enumerable.Range(0, all.Length).Where(ri => Has(all[ri].F, "dolga")).ToArray();
        Console.WriteLine($"# {Tag} §3-3 の 7 `compare` 全 {all.Length} 行 × 列（第1〜5波・seed 0..{CompareSeeds - 1}・ドルガだけ替える）——ドルガのいる {dolgaRows.Length} 行を並べる");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", Vers.Select(c => c.Name + " 第1〜5波")) + " |");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "---|")));
        foreach (int ri in dolgaRows)
            Console.WriteLine($"| {all[ri].Name} | {string.Join(" | ", grids.Select(g => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w])))))} |");
        Console.WriteLine();
        for (int c = 1; c < Vers.Length; c++)
        {
            int moved = 0, up = 0, movedOther = 0; double sum = 0;
            for (int ri = 0; ri < all.Length; ri++)
                for (int w = 0; w < nw; w++)
                    if (grids[c][ri, w] != grids[0][ri, w])
                    {
                        if (!Has(all[ri].F, "dolga")) { movedOther++; continue; }
                        moved++; if (grids[c][ri, w] > grids[0][ri, w]) up++; sum += grids[c][ri, w] - grids[0][ri, w];
                    }
            Console.WriteLine($"- {Vers[c].Name}: 規定と違うセル {moved}（上がり {up} ／ 落ち {moved - up}・差の合計 {Sg(sum)}pt）・**ドルガのいない行で動いたセル {movedOther}**（0 であること）");
        }
        Console.WriteLine();

        // (G2): いずれかの波（第2〜5波）で −10.0pt 以上落ちた行 → その行の駒それぞれの「その駒を含む他の行」の第2〜5波の平均の変化
        Console.WriteLine("## (G2) 規定から −10.0pt 以上落ちた行の分け（分母 `compare` 全 66 行・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("「他の行」＝ その駒を含む `compare` の行のうち、落ちた行を除いた全部（ドルガのいない行は版で動かないので差 0 で数える）。どの駒も |変化| < 3.0pt → **制約**・いずれかが ≤ −3.0pt → **壊れ**。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 落ちた行 | 落ち（波）| 駒ごとの他の行の平均の変化（行数）| 判定 |");
        Console.WriteLine("|---|---|---|---|---|");
        int found = 0;
        for (int c = 1; c < Vers.Length; c++)
            foreach (int ri in dolgaRows)
            {
                var drops = Enumerable.Range(1, nw - 1).Where(w => grids[c][ri, w] - grids[0][ri, w] <= -10.0).ToArray();
                if (drops.Length == 0) continue;
                found++;
                var ids = all[ri].F.Occupied().Select(o => o.Def.Id).Distinct().ToArray();
                var parts = new List<string>(); bool broken = false;
                foreach (var id in ids)
                {
                    var others = Enumerable.Range(0, all.Length).Where(k => k != ri && Has(all[k].F, id)).ToArray();
                    if (others.Length == 0) { parts.Add($"{Nm(id)} —（0 行・分解が成立しない）"); continue; }
                    double ch = others.Average(k => Enumerable.Range(1, nw - 1).Average(w => grids[c][k, w] - grids[0][k, w]));
                    if (ch <= -3.0) broken = true;
                    parts.Add($"{Nm(id)} {Sg(ch)}（{others.Length}）");
                }
                Console.WriteLine($"| {Vers[c].Name} | {all[ri].Name} | {string.Join("・", drops.Select(w => $"第{w + 1}波 {F1(grids[0][ri, w])} → {F1(grids[c][ri, w])}"))} | {string.Join("・", parts)} | {(broken ? "**壊れ**" : "制約")} |");
            }
        if (found == 0) Console.WriteLine("| — | 無し | | | |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Log1(string[] args)
    {
        string bn = args.Length > 3 ? args[3] : "試遊・手番 芯3枚";
        string cn = args.Length > 4 ? args[4] : "E1";
        int wi = args.Length > 5 && int.TryParse(args[5], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 6 && int.TryParse(args[6], out int s0) ? s0 : 0;
        var b = BoardList().FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal));
        if (b.F is null) { Console.WriteLine($"台が無い: {bn}"); return; }
        var c = Cols.FirstOrDefault(x => x.Name == cn); if (c.D is null) c = Cols[0];
        var (r, _, _) = I.Raw(Put(b.F, b.Slot, c.D), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name} × {c.Name} × {W8[wi].Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
