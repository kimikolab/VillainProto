using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S392 = Sh392Diag;

// =====================================================================================
// sh394 —— 第394期【軽い期】段0: トモの T3 を規定に ／ 本体: ドルガ——休む巨兵は壁（版 D0 ／ D1 ／ D2）。ドルガの規定は変えない。
// 指示書は design/PHASE394_DOLGA_WALL_SPEC.md、報告は design/PHASE394_DOLGA_WALL.md。
//
//     dotnet run --project BattleSim -c Release 0 sh394 zero         # §2: 段0 の3台（試遊・手番 芯3枚 ／ 探索1位 ／ 灯×薙ぎ）× トモ（規定 ／ 第393期の T3 ／ 旧 H393）× 8 物差し（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh394 boards       # §4-3 の 1〜7・9: 台 × 列（規定 ／ D0 ／ D1 ／ D2 ／ 素体）× 8 物差し（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh394 cmp          # §4-3 の 8: ドルガのいる `compare` の行 × 列（規定 ／ D0 ／ D1 ／ D2・第1〜5波・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh394 log <台の頭> <列> <物差し 0..7> <seed>
//     dotnet run --project BattleSim -c Release 0 sh394 check        # 自己検査
//
// **規定のドルガは1つも変えない。** 替えた台 ／ 素体は器具の中だけ（試遊の台に足す「版・壁 ドルガ」を除く）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p394/predict.md`）。
// =====================================================================================
static partial class Sh394Diag
{
    internal const string PredictFile = ".tmp/p394/predict.md";
    const string Tag = "第394期";
    const int Seeds = 100, CompareSeeds = 200, TT = 7;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "zero" or "boards" or "cmp" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh394: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
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
            default: Console.WriteLine("sh394: モードは zero / boards / cmp / log / check。"); return;
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
    static int SlotOf(Formation f, string id) => f.Occupied().First(o => o.Def.Id == id).Slot;

    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    static readonly int[] W4 = { 0, 1, 2, 5 };
    const int WBoss = 0, WGuard = 1, WBat = 2, WM5 = 5, WStrong = 7;

    // ---------------------------------------------------------------------------------
    // 版 ／ 列 ／ 台
    // ---------------------------------------------------------------------------------
    internal static readonly (string Name, UnitDef D)[] Vers =
        { ("規定", UnitCatalog.DolgaH395), ("D0", UnitCatalog.DolgaD0), ("D1", UnitCatalog.DolgaD1), ("D2", UnitCatalog.DolgaD2) };
    internal static UnitDef PlainDolga => _plain ??= S392.Plain(UnitCatalog.DolgaH395);   // 第396期: 旧の規定（数値は同じ）
    static UnitDef? _plain;
    /// <summary>列: 規定 ／ D0 ／ D1 ／ D2 ／ ドルガを素体に替えた台（第392 ／ 393期と同じ素体）。</summary>
    internal static (string Name, UnitDef D)[] Cols => Vers.Append(("素体", PlainDolga)).ToArray();

    /// <summary>
    /// §4-1 の台（ドルガの席 ＝ `Slot`）。1. 試遊・手番 芯3枚 ／ 試遊・手番 探索1位 ／ 灯×薙ぎ ／ 2. 縛め収入型 ／ 溜め改（第393期と同じ・バンの席に規定のトモ）と元の行（トモ無し）
    /// ／ 3. そのほかのドルガのいる `compare` の行（第392期の 6 行の残り）。
    /// </summary>
    internal static (string Name, Formation F, int Slot)[] BoardList()
    {
        var all = CompareBuilds();
        Formation Row(string n) => all.First(r => r.Name == n).F;
        var bind = Row("縛め収入型 (クグ×バン×ガン)"); var tame = Row("溜め改 (クグ×バン×ガン)");
        var l = new List<(string, Formation)>
        {
            ("試遊・手番 芯3枚", Presets.TempoLamp393(UnitCatalog.TomoH399)),
            ("試遊・手番 探索1位", Presets.TempoSeek392()),
            ("灯×薙ぎ (トモ×ドルガ)", Row("灯×薙ぎ (トモ×ドルガ)")),
            ("縛め収入型（バンの席にトモ）", Put(bind, SlotOf(bind, "ban"), UnitCatalog.TomoH399)),
            ("溜め改（バンの席にトモ）", Put(tame, SlotOf(tame, "ban"), UnitCatalog.TomoH399)),
            ("縛め収入型 (クグ×バン×ガン)", bind),
            ("溜め改 (クグ×バン×ガン)", tame),
        };
        foreach (var r in all) if (Has(r.F, "dolga") && !l.Any(x => x.Item1 == r.Name)) l.Add((r.Name, r.F));
        return l.Select(x => (x.Item1, x.Item2, SlotOf(x.Item2, "dolga"))).ToArray();
    }
    internal static (string Name, Formation F)[] DolgaRows() => CompareBuilds().Where(r => Has(r.F, "dolga")).ToArray();

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
        var tomo = p.FirstOrDefault(u => u.Def.Id == "tomo");
        var dolga = p.FirstOrDefault(u => u.Def.Id == "dolga");
        int tid = tomo?.InstanceId ?? -9, did = dolga?.InstanceId ?? -9;
        bool Foe(int id) => foe.ContainsKey(id) || (sumTeam.TryGetValue(id, out int st) && st == BattleContext.EnemyTeam);
        var dmgT = new long[BattleEngine.MaxTurns + 2];
        var deadAt = new Dictionary<int, int>();
        bool yieldPending = false, ganPending = false;
        int firstT = -1; long snapBonus = 0, firstBonus = 0, firstDmg = 0, bonusMax = 0;
        foreach (var x in r.Events)
        {
            int t = Math.Clamp(x.Turn, 0, BattleEngine.MaxTurns + 1);
            int tgt = x.TargetId ?? -3, act = x.ActorId ?? -2;
            switch (x.Kind)
            {
                case BattleEventKind.Summon: sumTeam[tgt] = x.Team ?? -1; break;
                case BattleEventKind.TurnStart: yieldPending = false; ganPending = false; break;
                case BattleEventKind.Whet:
                    if (x.WhetRoute == WhetRoute.Taillight && form.ContainsKey(tgt)) { Add(b, "litAmt", x.Amount); Add(b, "litN", 1); if (tgt == did) Add(b, "litD", 1); }
                    if (x.WhetRoute == WhetRoute.RallyTurn && form.TryGetValue(tgt, out var ru)) { Add(b, "rally8", 1); Add(b, "rally8:" + ru.Def.Id, 1); }
                    break;
                case BattleEventKind.Highlight:
                    if (act == tid && x.Text is { } tx && tx.Contains("道を譲る", StringComparison.Ordinal)) yieldPending = true;
                    break;
                case BattleEventKind.Reveille:
                    if (tgt == did && x.Text == ReveilleLabels.Woken) ganPending = true;
                    if (x.Text == ReveilleLabels.Woken) Add(b, "revWoken", 1);
                    break;
                case BattleEventKind.Attack:
                    if (act == did)
                    {
                        if (x.Reaction && ganPending) { Add(b, "dGanSwing", 1); ganPending = false; }
                        else if (!x.Reaction && yieldPending) { Add(b, "dYieldSwing", 1); yieldPending = false; }
                        else if (!x.Reaction) Add(b, "dOwnSwing", 1);
                        else Add(b, "dOtherReact", 1);
                        Add(b, "dSwing", 1);
                        if (x.Pattern == AttackPattern.All) Add(b, "dAll", 1);
                        if (firstT < 0) { firstT = t; firstBonus = snapBonus; }
                    }
                    else if (!x.Reaction && yieldPending && form.ContainsKey(act)) yieldPending = false;   // 灯した相手がドルガ以外
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
                    if (tgt == did)
                    {
                        snapBonus = x.Amount - dolga!.Def.Attack; bonusMax = Math.Max(bonusMax, snapBonus);
                        if (t is >= 1 and < TT) { Add(b, $"dBonus{t}", snapBonus); Add(b, $"dN{t}", 1); if (x.Pattern == AttackPattern.All) Add(b, $"dAllT{t}", 1); }
                    }
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
        if (deadAt.TryGetValue(did, out int dt)) { Add(b, "dFellN", 1); Add(b, "dFellT", dt); }
        foreach (var (id, tl) in r.TallyByUnit)
        {
            if (tl.R394WalledFor > 0) Add(b, "wf:" + id, tl.R394WalledFor);
            if (id != "dolga") continue;
            Add(b, "wallN", tl.R394WallHits); Add(b, "wallAmt", tl.R394WallAmt); Add(b, "wallMit", tl.R394WallMitigated);
            if (tl.R394WallByTurn is { } byT) for (int t = 1; t < TT && t < byT.Length; t++) Add(b, $"wallT{t}", byT[t]);
        }
        if (tomo is not null && r.TallyByUnit.TryGetValue("tomo", out var tt)) { Add(b, "carried", tt.R393Carried); Add(b, "wakes", tt.R393Wakes); }
    };

    static readonly object MaxLock = new();
    static long _bonusPeak;   // 全戦のドルガの積まれた力の最大（あふれの兆しを見る・§4-3 の 9）
    static S377.B[] M8(Formation f)
    {
        var g = new S377.B[W8.Length];
        Parallel.For(0, W8.Length, wi => g[wi] = S377.Measure(f, W8[wi].Ws, Seeds, Extra));
        return g;
    }
    static double A4(S377.B[] a) => W4.Average(wi => a[wi].Win);

    // ---------------------------------------------------------------------------------
    // §2: 段0
    // ---------------------------------------------------------------------------------
    static void Zero()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList().Take(3).ToArray();
        var tomos = new (string Name, UnitDef D)[] { ("規定（第394期）", UnitCatalog.TomoH399), ("第393期の T3", UnitCatalog.TomoT3), ("旧 TomoH393", UnitCatalog.TomoH393) };
        var ws = new[] { WBoss, WGuard, WBat, WM5, WStrong };
        Console.WriteLine($"# {Tag} §2 段0: 3 台 × トモ（{string.Join(" ／ ", tomos.Select(x => x.Name))}）× ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 台 | トモ | " + string.Join(" | ", ws.Select(w => W8[w].Name)) + " | 規定と T3 の差 |");
        Console.WriteLine("|---|---|" + string.Concat(ws.Select(_ => "---|")) + "---|");
        foreach (var bd in boards)
        {
            int ts = SlotOf(bd.F, "tomo");
            var m = tomos.Select(x => { var f = Put(bd.F, ts, x.D); var g = new S377.B[ws.Length]; Parallel.For(0, ws.Length, k => g[k] = S377.Measure(f, W8[ws[k]].Ws, Seeds, Extra)); return g; }).ToArray();
            for (int i = 0; i < tomos.Length; i++)
            {
                int diff = Enumerable.Range(0, ws.Length).Count(k => m[0][k].Cell != m[1][k].Cell);
                Console.WriteLine($"| {(i == 0 ? bd.Name : "")} | {tomos[i].Name} | {string.Join(" | ", m[i].Select(x => x.Cell))} | {(i == 0 ? $"違うセル {diff}" : "")} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-3 の 1〜7・9: 台 × 列
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        var cols = Cols;
        int nb = boards.Length, nc = cols.Length;
        var g = new S377.B[nb, nc][];
        for (int bi = 0; bi < nb; bi++) for (int ci = 0; ci < nc; ci++) g[bi, ci] = M8(Put(boards[bi].F, boards[bi].Slot, cols[ci].D));
        int[] w3 = { WBoss, WGuard, WBat, WM5, WStrong };

        Console.WriteLine($"# {Tag} §4-3 台 {nb} × 列（{string.Join(" ／ ", cols.Select(c => c.Name))}）× 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var bd in boards) Console.WriteLine($"- **{bd.Name}**: {Seats(bd.F)}（ドルガの席 {bd.Slot}）");
        Console.WriteLine();

        Console.WriteLine("## 1. 勝率・決着T・倒れた編成の駒 ／ 4 波（ボス ／ 近衛 ／ 大隊 ／ 本編 第5波）の平均 ／ 隊の1ターンの最大（ボス ／ 近衛 ／ 大隊）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 | 規定との差 | 1ターンの最大 |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|--:|---|");
        string MaxT(S377.B[] x) => string.Join(" ／ ", new[] { WBoss, WGuard, WBat }.Select(w => F0((double)X(x[w], "maxTurnDmg") / Math.Max(1, x[w].N))));
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {cols[ci].Name} | {string.Join(" | ", g[bi, ci].Select(x => x.Cell))} | {F1(A4(g[bi, ci]))} | {(ci == 0 ? "—" : Sg(A4(g[bi, ci]) - A4(g[bi, 0])))} | {MaxT(g[bi, ci])} |");
        Console.WriteLine();

        Console.WriteLine("## 2. 素体との差（いちばん大事）——各列 − ドルガを素体に替えた台（4 波 ／ 強い条件）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 素体（4 波 ／ 強）| " + string.Join(" | ", Vers.Select(v => v.Name + " − 素体（4 波 ／ 強）")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Vers.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
        {
            var pl = g[bi, nc - 1];
            Console.WriteLine($"| {boards[bi].Name} | {F1(A4(pl))} ／ {F1(pl[WStrong].Win)} | {string.Join(" | ", Enumerable.Range(0, Vers.Length).Select(ci => $"{Sg(A4(g[bi, ci]) - A4(pl))} ／ {Sg(g[bi, ci][WStrong].Win - pl[WStrong].Win)}"))} |");
        }
        Console.WriteLine();
        Console.WriteLine("比べ: 第393期の芯 3 枚の台（T3）でドルガを素体に替えた差は強い条件 −2.0。");
        Console.WriteLine();

        Console.WriteLine("## 3. 壁（1戦）——肩代わりした回数 ／ 量（受ける前の半分）・T1〜T6 の量・誰の代わりに・ドルガの側の軽減（据えなど）と重なった回数・ドルガの被ダメの合計・ドルガが倒れた戦 %（T）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 回数 ／ 量 | 量 T1〜T6 | 誰の代わりに（回）| 重なった | ドルガの被ダメ | ドルガが倒れた（T）|");
        Console.WriteLine("|---|---|---|---|---|---|--:|--:|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < Vers.Length; ci++)
                foreach (int w in w3)
                {
                    var x = g[bi, ci][w];
                    string byT = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => Per(X(x, $"wallT{t}"), x.N)));
                    string who = string.Join("・", x.X.Keys.Where(k => k.StartsWith("wf:", StringComparison.Ordinal)).OrderByDescending(k => x.X[k]).Select(k => $"{Nm(k[3..])} {Per(x.X[k], x.N)}"));
                    bool wall = Vers[ci].D.Traits.Contains(TraitId.DolgaWall);
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? boards[bi].Name : "")} | {(w == WBoss ? Vers[ci].Name : "")} | {W8[w].Name} | {(wall ? $"{Per(X(x, "wallN"), x.N)} ／ {Per(X(x, "wallAmt"), x.N)}" : "—")} | {(wall ? byT : "—")} | {(wall ? (who.Length == 0 ? "—" : who) : "—")} | {(wall ? Per(X(x, "wallMit"), x.N) : "—")} | {Per(X(x, "dTaken"), x.N)} | {Pc(X(x, "dFellN"), x.N)}（T{Per(X(x, "dFellT"), X(x, "dFellN"))}）|");
                }
        Console.WriteLine();

        Console.WriteLine("## 4. 誰が何Tに倒れたか（規定 ／ 4 波の平均がいちばん良い版・ボス ／ 近衛 ／ 大隊 ／ 強い条件）——前列が崩れるTが遅れたか");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
        {
            int best = Enumerable.Range(1, Vers.Length - 1).OrderByDescending(ci => A4(g[bi, ci])).ThenByDescending(ci => g[bi, ci][WStrong].Win).First();
            Console.WriteLine($"- **{boards[bi].Name}**（最良の版 {Vers[best].Name}・4 波 {F1(A4(g[bi, best]))} ／ 規定 {F1(A4(g[bi, 0]))}・強 {F1(g[bi, best][WStrong].Win)} ／ {F1(g[bi, 0][WStrong].Win)}）");
            var show = bi < 7 ? Enumerable.Range(0, Vers.Length).ToArray() : new[] { 0, best };   // §4-1 の 1・2 の台は全部の版
            foreach (int w in new[] { WBoss, WGuard, WBat, WStrong })
                foreach (int ci in show)
                    Console.WriteLine($"  - {W8[w].Name}: {Vers[ci].Name} {F1(g[bi, ci][w].Win)}%・{g[bi, ci][w].WinTs}T — {g[bi, ci][w].FellCell}");
        }
        Console.WriteLine();

        Console.WriteLine("## 5. ドルガ（1戦）——振った: 自分 ／ トモに起こされた ／ ガンに起こされた（全体）・積まれた力 T1〜T6 ／ 最大・全体のターン % T1〜T6・1振りの与ダメ・**最初に振ったT ／ そのときの力 ／ そのターンの与ダメ**");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 振った: 自分 ／ トモ ／ ガン（全体）| 積まれた力 T1〜T6 | 最大 | 全体 % T1〜T6 | 1振り | 最初: T ／ 力 ／ 与ダメ |");
        Console.WriteLine("|---|---|---|---|---|--:|---|--:|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                foreach (int w in w3)
                {
                    var x = g[bi, ci][w];
                    if (ci == nc - 1) continue;   // 素体は札が無いので数えない
                    string bon = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"dN{t}") == 0 ? "—" : F1((double)X(x, $"dBonus{t}") / X(x, $"dN{t}"))));
                    string all = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"dN{t}") == 0 ? "—" : Pc(X(x, $"dAllT{t}"), X(x, $"dN{t}"))));
                    long fn = X(x, "firstN");
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? boards[bi].Name : "")} | {(w == WBoss ? cols[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "dOwnSwing"), x.N)} ／ {Per(X(x, "dYieldSwing"), x.N)} ／ {Per(X(x, "dGanSwing"), x.N)}（{Per(X(x, "dAll"), x.N)}）| {bon} | {Per(X(x, "dBonusMax"), x.N)} | {all} | {(X(x, "dSwing") == 0 ? "—" : F0((double)X(x, "dDmg") / X(x, "dSwing")))} | T{Per(X(x, "firstT"), fn)} ／ {Per(X(x, "firstBonus"), fn)} ／ {(fn == 0 ? "—" : F0((double)X(x, "firstDmg") / fn))} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 6. ガン ／ トモ（1戦）——+8（誰に）・叩き起こし ／ 灯（回・うちドルガ）・1回の灯・運んだ力・灯が起こした");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | +8（誰に）| 叩き起こし | 灯（ドルガ）| 1回の灯 | 運んだ | 起こした |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < Vers.Length; ci++)
                foreach (int w in new[] { WBoss, WGuard, WBat })
                {
                    var x = g[bi, ci][w];
                    string r8 = Has(boards[bi].F, "gan") ? $"{Per(X(x, "rally8"), x.N)}（" + string.Join("・", x.X.Keys.Where(k => k.StartsWith("rally8:", StringComparison.Ordinal)).OrderByDescending(k => x.X[k]).Select(k => $"{Nm(k[7..])} {Per(x.X[k], x.N)}")) + "）" : "—";
                    bool tm = Has(Put(boards[bi].F, boards[bi].Slot, Vers[ci].D), "tomo");
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? boards[bi].Name : "")} | {(w == WBoss ? Vers[ci].Name : "")} | {W8[w].Name} | {r8} | {(Has(boards[bi].F, "gan") ? Per(X(x, "revWoken"), x.N) : "—")} | {(tm ? $"{Per(X(x, "litN"), x.N)}（{Per(X(x, "litD"), x.N)}）" : "—")} | {(tm && X(x, "litN") > 0 ? F1((double)X(x, "litAmt") / X(x, "litN")) : "—")} | {(tm ? Per(X(x, "carried"), x.N) : "—")} | {(tm ? Per(X(x, "wakes"), x.N) : "—")} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 7. 止めを刺したのは誰か（敵の撃破・ボス ／ 近衛 ／ 大隊・ドルガ ／ 相方 ／ ほか %）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", cols.Select(c => c.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(0, nc).Select(ci => string.Join(" ／ ", new[] { WBoss, WGuard, WBat }.Select(w => { var x = g[bi, ci][w]; long a = X(x, "kAll"); return $"{Pc(X(x, "kDolga"), a)}・{Pc(X(x, "kPartner"), a)}・{Pc(X(x, "kOther"), a)}"; }))))} |");
        Console.WriteLine();

        Console.WriteLine("## 9. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上（規定との差）／ あふれ）・ドルガの積まれた力の最大（1戦の最大の平均 ／ 全戦の最大）");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 1; ci < nc; ci++)
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
        if (signs.Count == 0) Console.WriteLine("- 無し。");
        foreach (var s in signs) Console.WriteLine("- " + s);
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"- {boards[bi].Name}: ドルガの力の最大（ボス ／ 近衛 ／ 大隊 ／ 強）" + string.Join("・", Enumerable.Range(0, Vers.Length).Select(ci => $"{Vers[ci].Name} " + string.Join(" ／ ", new[] { WBoss, WGuard, WBat, WStrong }.Select(w => Per(X(g[bi, ci][w], "dBonusMax"), g[bi, ci][w].N))))));
        Console.WriteLine($"- 全台 × 全列 × 全物差しの1戦で、ドルガの積まれた力のいちばん大きい値: {_bonusPeak}（int のあふれまで遠い）");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-3 の 8: `compare`
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
    static void Cmp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = DolgaRows();
        int nw = EnemyCatalog.Stages.Count;
        var grids = Vers.Select(c => CGrid(rows.Select(r => (r.Name, I.Swap(r.F, "dolga", c.D))).ToArray(), CompareSeeds)).ToArray();
        Console.WriteLine($"# {Tag} §4-3 の 8 `compare` のドルガのいる行 —— {rows.Length} 行 × 列（第1〜5波・seed 0..{CompareSeeds - 1}・ドルガだけ替える）");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", Vers.Select(c => c.Name + " 第1〜5波")) + " |");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "---|")));
        for (int ri = 0; ri < rows.Length; ri++)
            Console.WriteLine($"| {rows[ri].Name} | {string.Join(" | ", grids.Select(g => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w])))))} |");
        Console.WriteLine();
        for (int c = 1; c < Vers.Length; c++)
        {
            int moved = 0, up = 0; double sum = 0;
            for (int ri = 0; ri < rows.Length; ri++) for (int w = 0; w < nw; w++) if (grids[c][ri, w] != grids[0][ri, w]) { moved++; if (grids[c][ri, w] > grids[0][ri, w]) up++; sum += grids[c][ri, w] - grids[0][ri, w]; }
            Console.WriteLine($"- {Vers[c].Name}: 規定と違うセル {moved}（上がり {up} ／ 落ち {moved - up}・差の合計 {Sg(sum)}pt）");
        }
        Console.WriteLine("- ドルガのいない行: 版の札は版のドルガにしか載らない（`_dolgaWalls` は空・`SluggishShift` の保持者なし）ので回さない。規定の全 66 行は `sh394 check` が `docs/balance.md` と照合する。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Log1(string[] args)
    {
        string bn = args.Length > 3 ? args[3] : "試遊・手番 芯3枚";
        string cn = args.Length > 4 ? args[4] : "D1";
        int wi = args.Length > 5 && int.TryParse(args[5], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 6 && int.TryParse(args[6], out int s0) ? s0 : 0;
        var b = BoardList().FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal));
        if (b.F is null) { Console.WriteLine($"台が無い: {bn}"); return; }
        var c = Cols.FirstOrDefault(x => x.Name == cn); if (c.D is null) c = Cols[0];
        var (r, _, _) = I.Raw(Put(b.F, b.Slot, c.D), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name} × {c.Name} × {W8[wi].Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
