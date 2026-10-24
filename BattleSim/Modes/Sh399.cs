using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S390 = Sh390Diag;
using S392 = Sh392Diag;

// =====================================================================================
// sh399 —— 第399期 段0: セッキ K1 を規定に ／ 本体: 放つターンに 3,000 を出す（トモの版 L1 ／ ドルガの版 X1 ／ X2 とその組み合わせ）。
// 指示書は design/PHASE399_TEMPO_BURST_SPEC.md、報告は design/PHASE399_TEMPO_BURST.md。
//
//     dotnet run --project BattleSim -c Release 0 sh399 zero       # §2: `compare` の動き（第398期の HEAD の表と ／ 第398期の K1 の列と・(G2)）＋ 試遊の 4 台 × 5 物差し（規定 ／ 第398期）
//     dotnet run --project BattleSim -c Release 0 sh399 boards     # §4-1 の 1・2: 台 × 列（規定 ／ L1 ／ X1 ／ X2 ／ L1+X1 ／ L1+X2 ／ ドルガの素体）× 8 物差し——放つターン ／ 譲り ／ 休み明け ／ 止め
//     dotnet run --project BattleSim -c Release 0 sh399 cmp        # §4-1 の 3: `compare` 全 66 行 × 列（seed 0..199）＋ (G2)
//     dotnet run --project BattleSim -c Release 0 sh399 log <台の頭> <列> <物差し 0..7> <seed>
//     dotnet run --project BattleSim -c Release 0 sh399 check      # 自己検査
//
// **本体の版は規定にしない。** 替えた台 ／ 素体は器具の中だけ（試遊の台に足す「版・目覚め ドルガ」を除く）。**予測のファイルが無ければ測定を走らせない**（`.tmp/p399/predict.md`）。
// 「勇者」＝ 敵のうち最大 HP がいちばん大きく、2 番目の 2 倍を超える駒（器具の中の数え方だけ・engine はボスで分岐しない）。
// =====================================================================================
static partial class Sh399Diag
{
    internal const string PredictFile = ".tmp/p399/predict.md";
    const string Tag = "第399期";
    const int Seeds = 100, CompareSeeds = 200, TT = 9;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "zero" or "boards" or "cmp" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh399: 予測のファイル `{PredictFile}` がまだ無い。予測を書いてから回すこと。");
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
            default: Console.WriteLine("sh399: モードは zero / boards / cmp / log / check。"); return;
        }
    }

    /// <summary>第400期: `compare` の行を第399期の姿で（トモは旧 `TomoH399`）。</summary>
    static (string Name, Formation F)[] CB399() => CompareBuilds().Select(r => (r.Name, Pin399(r.F))).ToArray();
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
    static bool SameF(Formation a, Formation b) => Enumerable.Range(0, 5).All(s => ReferenceEquals(a[s], b[s]));

    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    static readonly int[] W4 = { 0, 1, 2, 5 };
    const int WBoss = 0, WGuard = 1, WBat = 2, WM2 = 3, WM4 = 4, WM5 = 5, WNine = 6, WStrong = 7;
    static readonly string[] BurnIds = { "borg", "hota", "hiyo", "tou", "beni" };

    // ---------------------------------------------------------------------------------
    // 列 ／ 台
    // ---------------------------------------------------------------------------------
    internal static UnitDef PlainDolga => _plainD ??= S392.Plain(UnitCatalog.Dolga);
    static UnitDef? _plainD;
    /// <summary>§3-3 の 6 列 ＋ ドルガの素体（規定のトモで）。</summary>
    internal static (string Name, UnitDef Tomo, UnitDef Dolga)[] Cols => new[]
    {
        ("規定", UnitCatalog.TomoH399, UnitCatalog.Dolga), ("L1", UnitCatalog.TomoL1, UnitCatalog.Dolga), ("X1", UnitCatalog.TomoH399, UnitCatalog.DolgaX1), ("X2", UnitCatalog.TomoH399, UnitCatalog.DolgaX2),
        ("L1+X1", UnitCatalog.TomoL1, UnitCatalog.DolgaX1), ("L1+X2", UnitCatalog.TomoL1, UnitCatalog.DolgaX2), ("ドルガ素体", UnitCatalog.TomoH399, PlainDolga),
    };
    internal static Formation Apply(Formation f, UnitDef tomo, UnitDef dolga)
    {
        var g = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == "tomo" ? tomo : d.Id == "dolga" ? dolga : d;
        return g;
    }

    /// <summary>§4-1 の 1・2 の台。`Burn` は燃焼の駒がいる台。</summary>
    internal static (string Name, Formation F, bool Burn)[] BoardList()
    {
        var all = CB399();
        Formation Row(string n) => all.First(r => r.Name == n).F;
        var tame = Row("溜め改 (クグ×バン×ガン)"); var bind = Row("縛め収入型 (クグ×バン×ガン)");
        return new (string, Formation, bool)[]
        {
            ("試遊・手番 縛め", Pin399(Presets.BindTempo395()), false),
            ("試遊・手番 芯4枚", Pin399(Presets.FourCore397()), false),
            ("溜め改（バンの席にトモ）", Put(tame, SlotOf(tame, "ban"), UnitCatalog.TomoH399), false),
            ("縛め収入型（バンの席にトモ）", Put(bind, SlotOf(bind, "ban"), UnitCatalog.TomoH399), false),
            ("試遊・手番 芯3枚", Pin399(Presets.TempoLamp393()), true),
            ("試遊・手番 包み", Pin399(Presets.SlimeLamp396()), true),
            ("試遊・手番 探索1位", Pin399(Presets.TempoSeek392()), true),
        };
    }
    static string BurnNote(Formation f) { var b = f.Occupied().Where(o => BurnIds.Contains(o.Def.Id)).Select(o => Short(o.Def)).ToList(); return b.Count == 0 ? "燃焼の駒なし" : "燃焼の駒: " + string.Join("・", b); }

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    static void Add(S377.B b, string k, long v) { if (v != 0) b.X[k] = b.X.GetValueOrDefault(k) + v; }
    static long X(S377.B b, string k) => b.X.GetValueOrDefault(k);
    static readonly object MaxLock = new();
    static long _dHitPeak, _turnPeak;

    static readonly Action<BattleResult, List<UnitState>, List<UnitState>, S377.B> Extra = (r, p, e, b) =>
    {
        var form = p.ToDictionary(u => u.InstanceId);
        var foe = e.ToDictionary(u => u.InstanceId);
        var sumTeam = new Dictionary<int, int>();
        var dolga = p.FirstOrDefault(u => u.Def.Id == "dolga" || u.Def.Id == "dolga_plain392");
        var tomo = p.FirstOrDefault(u => u.Def.Id == "tomo");
        int did = dolga?.InstanceId ?? -9, tid = tomo?.InstanceId ?? -8;
        var byHp = e.OrderByDescending(u => u.Def.MaxHp).ToList();
        UnitState? hero = byHp.Count > 0 && (byHp.Count == 1 || byHp[0].Def.MaxHp > 2 * byHp[1].Def.MaxHp) ? byHp[0] : null;
        int hid = hero?.InstanceId ?? -7;
        bool Foe(int id) => foe.ContainsKey(id) || (sumTeam.TryGetValue(id, out int st) && st == BattleContext.EnemyTeam);
        var heroT = new long[BattleEngine.MaxTurns + 2];
        var foeT = new long[BattleEngine.MaxTurns + 2];
        string? pending = null;   // 次のドルガの振りの出どころ（ガンの叩き起こし ／ トモの譲り）
        (int Act, AttackPattern? Pat) lastHeroHit = (-1, null);
        string Src(int act, AttackPattern? pat) => pat is null ? "刻み" : form.TryGetValue(act, out var u) ? u.Def.Id : "召喚";
        foreach (var x in r.Events)
        {
            int t = Math.Clamp(x.Turn, 0, BattleEngine.MaxTurns + 1);
            int tgt = x.TargetId ?? -3, act = x.ActorId ?? -2;
            switch (x.Kind)
            {
                case BattleEventKind.Summon: sumTeam[tgt] = x.Team ?? -1; break;
                case BattleEventKind.Reveille:
                    if (x.Text == ReveilleLabels.Woken && tgt == did) { pending = "gan"; Add(b, "revD", 1); }
                    break;
                case BattleEventKind.Highlight:
                    if (act == tid && x.Text is { } tx && tx.Contains("道を譲る", StringComparison.Ordinal)) pending = "tomo";
                    break;
                case BattleEventKind.Attack:
                    if (act == did)
                    {
                        string route = pending ?? "own"; pending = null;
                        Add(b, "dSwing", 1); Add(b, "dSw:" + route, 1);
                        if (t < TT) Add(b, $"dSw{t}:{route}", 1);
                    }
                    break;
                case BattleEventKind.Damage:
                {
                    bool byUs = form.ContainsKey(act) || (sumTeam.TryGetValue(act, out int sa) && sa == BattleContext.PlayerTeam);
                    if (Foe(tgt) && byUs) foeT[t] += x.Amount;
                    if (act == did && Foe(tgt))
                    {
                        if (tgt == hid) { Add(b, "dHeroHits", 1); Add(b, "dHeroDmg", x.Amount); }
                        else { Add(b, "dFoeHits", 1); Add(b, "dFoeDmg", x.Amount); }
                        b.X["dHitMax"] = Math.Max(X(b, "dHitMax"), x.Amount);
                        lock (MaxLock) if (x.Amount > _dHitPeak) _dHitPeak = x.Amount;
                    }
                    if (tgt == hid)
                    {
                        heroT[t] += x.Amount;
                        Add(b, "heroDmg", x.Amount); Add(b, "hdBy:" + Src(act, x.Pattern), x.Amount);
                        lastHeroHit = (act, x.Pattern);
                    }
                    break;
                }
                case BattleEventKind.Death:
                    if (tgt == hid) { Add(b, "heroDead", 1); Add(b, "heroDeadT", t); Add(b, "hkBy:" + Src(lastHeroHit.Act, lastHeroHit.Pat), 1); }
                    break;
            }
        }
        if (hero is not null)
        {
            Add(b, "heroSeen", 1);
            for (int t = 1; t < TT; t++) Add(b, $"heroT{t}", heroT[t]);
            int first = Enumerable.Range(1, BattleEngine.MaxTurns).FirstOrDefault(t => heroT[t] >= 3000);
            if (first > 0) { Add(b, "r3k", 1); Add(b, "r3kT", first); }
        }
        // 1ターンに敵へ与えた量の最大と、3,000 を超えた最初のT（全物差し）
        long mx = foeT.Max(); Add(b, "maxTurn", mx);
        int f3 = Enumerable.Range(1, BattleEngine.MaxTurns).FirstOrDefault(t => foeT[t] >= 3000);
        if (f3 > 0) Add(b, $"f3k:{f3}", 1);
        lock (MaxLock) if (mx > _turnPeak) _turnPeak = mx;
        foreach (var (id, tl) in r.TallyByUnit)
        {
            if (tl.R399IdleYieldTo > 0) Add(b, "yto:" + id, tl.R399IdleYieldTo);
            if (id == "tomo")
            {
                Add(b, "tYield", tl.TaillightYields); Add(b, "tIdleY", tl.R399IdleYields); Add(b, "tNoDeath", tl.TaillightNoDeath); Add(b, "tNoTarget", tl.TaillightNoTarget);
                Add(b, "tHop", tl.TaillightBlockedHop); Add(b, "tNoFoe", tl.TaillightNoFoe); Add(b, "tStall", tl.TaillightYieldStalls); Add(b, "tYAtk", tl.TaillightYieldAttack);
            }
            if (id == "dolga")
            {
                Add(b, "run1", tl.R399Run1); Add(b, "run2", tl.R399Run2); Add(b, "run3", tl.R399Run3);
                Add(b, "dblN", tl.R399DoubleHits); Add(b, "dblAdd", tl.R399DoubleAdd); Add(b, "stkN", tl.R399StackHits); Add(b, "stkAdd", tl.R399StackAdd);
            }
        }
    };

    static S377.B[] M8(Formation f, string tag, int seeds = Seeds)   // tag は呼び出し側の目印（計数には使わない）
    {
        var g = new S377.B[W8.Length];
        Parallel.For(0, W8.Length, wi => g[wi] = S377.Measure(f, W8[wi].Ws, seeds, Extra));
        return g;
    }
    static double A4(S377.B[] a) => W4.Average(wi => a[wi].Win);
    static double D4(S377.B[] a, S377.B[] b) => W4.Average(wi => a[wi].Win - b[wi].Win);

    // ---------------------------------------------------------------------------------
    // §2: 段0
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

    /// <summary>(G2): <paramref name="baseG"/> から −10pt 以上落ちた行（第2〜5波）を、その行の駒の「その駒を含む他の行」の第2〜5波の平均の変化で分ける。</summary>
    static void G2Split(string ver, (string Name, Formation F)[] all, double[,] baseG, double[,] g, IEnumerable<int> rowsToCheck)
    {
        int nw = EnemyCatalog.Stages.Count;
        foreach (int ri in rowsToCheck)
        {
            var drops = Enumerable.Range(1, nw - 1).Where(w => g[ri, w] - baseG[ri, w] <= -10.0).ToArray();
            if (drops.Length == 0) continue;
            var parts = new List<string>(); bool broken = false;
            foreach (var id in all[ri].F.Occupied().Select(o => o.Def.Id).Distinct())
            {
                var others = Enumerable.Range(0, all.Length).Where(k => k != ri && Has(all[k].F, id)).ToArray();
                if (others.Length == 0) { parts.Add($"{Nm(id)} —（0 行・分解が成立しない）"); continue; }
                double ch = others.Average(k => Enumerable.Range(1, nw - 1).Average(w => g[k, w] - baseG[k, w]));
                if (ch <= -3.0) broken = true;
                parts.Add($"{Nm(id)} {Sg(ch)}（{others.Length}）");
            }
            Console.WriteLine($"| {ver} | {all[ri].Name} | {string.Join("・", drops.Select(w => $"第{w + 1}波 {F1(baseG[ri, w])} → {F1(g[ri, w])}"))} | {string.Join("・", parts)} | {(broken ? "**壊れ**" : "制約")} |");
        }
    }

    static void Zero()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CB399();
        int nw = EnemyCatalog.Stages.Count;
        var now = CGrid(rows, CompareSeeds);
        var k1 = CGrid(rows.Select(r => (r.Name, Has(r.F, "sekki") ? I.Swap(Pin398(r.F), "sekki", UnitCatalog.SekkiK1) : r.F)).ToArray(), CompareSeeds);
        var old = CGrid(rows.Select(r => (r.Name, Pin398(r.F))).ToArray(), CompareSeeds);
        var map = S390.BalanceMap(S390.GitShow($"{HeadSha}:docs/balance.md"));
        int moved = 0, up = 0, same = 0, cells = 0, oldSame = 0; double sum = 0;
        var movedRows = new List<string>();
        for (int ri = 0; ri < rows.Length; ri++)
        {
            var cc = map[rows[ri].Name]; var parts = new List<string>();
            for (int w = 0; w < nw; w++)
            {
                cells++;
                if (now[ri, w] == k1[ri, w]) same++;
                double o = double.Parse(cc[w].TrimEnd('%'));
                if (o == old[ri, w]) oldSame++;
                if (o != now[ri, w]) { moved++; if (now[ri, w] > o) up++; sum += now[ri, w] - o; parts.Add($"第{w + 1}波 {F1(o)} → {F1(now[ri, w])}"); }
            }
            if (parts.Count > 0) movedRows.Add($"| {rows[ri].Name} | {string.Join("・", parts)} |");
        }
        Console.WriteLine($"# {Tag} §2 段0: セッキの規定 ＝ 第398期の K1");
        Console.WriteLine();
        Console.WriteLine($"## 1. `compare`（全 {rows.Length} 行 × {nw} 波 × seed 0..{CompareSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"- 第398期の HEAD（{HeadSha}）の `docs/balance.md` と違うセル **{moved}**（上がり {up} ／ 落ち {moved - up}・差の合計 {Sg(sum)}pt）");
        Console.WriteLine($"- 第398期の K1 の列（セッキを `SekkiK1` に替えた全行）と同じセル {same} ／ {cells}・旧（`SekkiH398`）に戻した全行が HEAD の表と同じセル {oldSame} ／ {cells}");
        Console.WriteLine();
        Console.WriteLine("| 行 | 動いたセル |");
        Console.WriteLine("|---|---|");
        foreach (var l in movedRows) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine("### (G2) 旧の規定から −10.0pt 以上落ちた行（分母 `compare` 全 66 行・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 落ちた行 | 落ち（波）| 駒ごとの他の行の平均の変化（行数）| 判定 |");
        Console.WriteLine("|---|---|---|---|---|");
        G2Split("規定（K1）", rows, old, now, Enumerable.Range(0, rows.Length).Where(ri => Has(rows[ri].F, "sekki")));
        Console.WriteLine("（行が無ければ −10pt 以上落ちた行は無い）");
        Console.WriteLine();

        var boards = new (string Name, Formation F)[] { ("試遊・手番 芯3枚", Pin399(Presets.TempoLamp393())), ("試遊・手番 包み", Pin399(Presets.SlimeLamp396())), ("試遊・手番 縛め", Pin399(Presets.BindTempo395())), ("試遊・手番 芯4枚", Pin399(Presets.FourCore397())) };
        var ws = new[] { WBoss, WGuard, WBat, WM5, WStrong };
        Console.WriteLine($"## 2. 試遊の 4 台 × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件（seed 0..{Seeds - 1}）・規定 ／ 第398期（セッキを旧に）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", ws.Select(w => W8[w].Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(ws.Select(_ => "---|")));
        foreach (var bd in boards)
            foreach (var (cn, f) in new[] { ("規定（第399期）", bd.F), ("第398期（旧）", Pin398(bd.F)) })
            {
                var g = new S377.B[ws.Length]; Parallel.For(0, ws.Length, k => g[k] = S377.Measure(f, W8[ws[k]].Ws, Seeds));
                Console.WriteLine($"| {(cn.StartsWith("規定") ? $"{bd.Name}（{Seats(bd.F)}）" : "")} | {cn} | {string.Join(" | ", g.Select(x => x.Cell))} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-1 の 1・2: 台 × 列
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        var cols = Cols;
        int nb = boards.Length, nc = cols.Length, iPlain = nc - 1;
        var g = new S377.B[nb, nc][];
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++) g[bi, ci] = M8(Apply(boards[bi].F, cols[ci].Tomo, cols[ci].Dolga), $"{boards[bi].Name} × {cols[ci].Name}");

        Console.WriteLine($"# {Tag} §4-1 の 1・2 台 {nb} × 列（{string.Join(" ／ ", cols.Select(c => c.Name))}）× 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var (name, f, burn) in boards) Console.WriteLine($"- **{name}**: {Seats(f)}（{BurnNote(f)}）");
        for (int a = 0; a < nb; a++) for (int c = a + 1; c < nb; c++) if (SameF(boards[a].F, boards[c].F)) Console.WriteLine($"- **注**: 「{boards[a].Name}」と「{boards[c].Name}」は同じ並び（数字も同じになる）");
        Console.WriteLine();

        Console.WriteLine("## 1. 勝率・決着T（ボスの列はボスの倒しT）・倒れた編成の駒 ／ 4 波 ／ 規定との差（4 波 ／ ボス ／ 強）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 | 規定との差 |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
            {
                var a = g[bi, ci]; var d0 = g[bi, 0];
                Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {cols[ci].Name} | {string.Join(" | ", a.Select(x => x.Cell))} | {F1(A4(a))} | {(ci == 0 ? "—" : $"{Sg(D4(a, d0))} ／ {Sg(a[WBoss].Win - d0[WBoss].Win)} ／ {Sg(a[WStrong].Win - d0[WStrong].Win)}")} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 2. 放つターン（ボス・1戦の平均）——ターンごとのドルガの振り（自分 ／ ガンの叩き起こし ／ トモの譲り）と、そのターンに勇者へ与えた合計（全員から）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", Enumerable.Range(1, TT - 1).Select(t => $"T{t}")) + " | 1ターンで 3,000 に届いた戦 %（最初のT）|");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(1, TT - 1).Select(_ => "---|")) + "---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
            {
                var x = g[bi, ci][WBoss]; long n = x.N, r3 = X(x, "r3k");
                string Cell(int t) { long o = X(x, $"dSw{t}:own"), gn = X(x, $"dSw{t}:gan"), tm = X(x, $"dSw{t}:tomo"); return (o + gn + tm == 0 ? "—" : $"{Per(o, n)} ／ {Per(gn, n)} ／ {Per(tm, n)}") + $"・{F0((double)X(x, $"heroT{t}") / n)}"; }
                Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {cols[ci].Name} | {string.Join(" | ", Enumerable.Range(1, TT - 1).Select(Cell))} | {Pc(r3, X(x, "heroSeen"))}（T{Per(X(x, "r3kT"), r3)}）|");
            }
        Console.WriteLine();

        Console.WriteLine("## 3. トモの譲り（1戦・ボス ／ 強い条件）——譲った（うち L1 で増えた）／ 譲った手番で振った・譲れなかった理由（敵が倒れていない ／ 灯の相手がいない ／ 1ホップ ／ 敵がいない）／ 譲った手番が潰れた・L1 の相手（誰に）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 譲った（L1）| 振った | 譲れなかった: 倒れていない ／ 相手なし ／ 1ホップ ／ 敵なし | 潰れた | L1 の相手 |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                foreach (int w in new[] { WBoss, WStrong })
                {
                    var x = g[bi, ci][w]; long n = x.N;
                    string to = string.Join("・", x.X.Where(kv => kv.Key.StartsWith("yto:", StringComparison.Ordinal)).OrderByDescending(kv => kv.Value).Select(kv => $"{Nm(kv.Key[4..])} {Per(kv.Value, n)}"));
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? boards[bi].Name : "")} | {(w == WBoss ? cols[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "tYield"), n)}（{Per(X(x, "tIdleY"), n)}）| {Per(X(x, "tYAtk"), n)} | {Per(X(x, "tNoDeath"), n)} ／ {Per(X(x, "tNoTarget"), n)} ／ {Per(X(x, "tHop"), n)} ／ {Per(X(x, "tNoFoe"), n)} | {Per(X(x, "tStall"), n)} | {(to.Length == 0 ? "—" : to)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 4. ドルガの休み明け（1戦）——続けて動けなかったターン数の分布（1 ／ 2 ／ 3 以上・使い切った振りで数える）・X1 の倍（回数 ／ 足した量）・X2 の上乗せ（回数 ／ 量）・叩き起こし・振り（自分 ／ ガン ／ トモ）・一撃の平均（勇者に ／ 勇者以外の敵に）と最大");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 休みの連続 1 ／ 2 ／ 3+ | X1 倍 | X2 上乗せ | 叩き起こし | 振り 自 ／ ガン ／ トモ | 一撃 勇者に ／ 敵に | 最大 |");
        Console.WriteLine("|---|---|---|---|---|---|--:|---|---|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc - 1; ci++)
                foreach (int w in new[] { WBoss, WGuard, WBat, WStrong })
                {
                    var x = g[bi, ci][w]; long n = x.N, hh = X(x, "dHeroHits"), fh = X(x, "dFoeHits");
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? boards[bi].Name : "")} | {(w == WBoss ? cols[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "run1"), n)} ／ {Per(X(x, "run2"), n)} ／ {Per(X(x, "run3"), n)} | {Per(X(x, "dblN"), n)} ／ {Per(X(x, "dblAdd"), n)} | {Per(X(x, "stkN"), n)} ／ {Per(X(x, "stkAdd"), n)} | {Per(X(x, "revD"), n)} | {Per(X(x, "dSw:own"), n)} ／ {Per(X(x, "dSw:gan"), n)} ／ {Per(X(x, "dSw:tomo"), n)} | {(hh == 0 ? "—" : F0((double)X(x, "dHeroDmg") / hh))} ／ {(fh == 0 ? "—" : F0((double)X(x, "dFoeDmg") / fh))} | {Per(X(x, "dHitMax"), n)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 5. 勇者に止めを刺したのは誰か（勇者が倒れた戦の %）・勇者へのダメージの内訳（誰から %・1戦の合計）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 勇者が倒れた %（T）| 止め | 勇者への与ダメ（内訳）| 合計 |");
        Console.WriteLine("|---|---|---|---|---|--:|");
        string Mix(S377.B x, string pre, long den) => string.Join("・", x.X.Where(kv => kv.Key.StartsWith(pre, StringComparison.Ordinal)).OrderByDescending(kv => kv.Value).Select(kv => $"{Nm(kv.Key[pre.Length..])} {Pc(kv.Value, den)}"));
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
            {
                var x = g[bi, ci][WBoss]; long hd = X(x, "heroDead"), dm = X(x, "heroDmg");
                Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {cols[ci].Name} | {Pc(hd, x.N)}%（T{Per(X(x, "heroDeadT"), hd)}）| {(hd == 0 ? "—" : Mix(x, "hkBy:", hd))} | {Mix(x, "hdBy:", dm)} | {Per(dm, x.N)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 6. 誰が何Tに倒れたか（規定 ／ ボスの勝率がいちばん良い列・ボス ／ 近衛 ／ 大隊 ／ 強い条件）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
        {
            int best = Enumerable.Range(1, nc - 2).OrderByDescending(ci => g[bi, ci][WBoss].Win).ThenByDescending(ci => A4(g[bi, ci])).First();
            Console.WriteLine($"- **{boards[bi].Name}**（ボスがいちばん良い列 {cols[best].Name}・ボス {F1(g[bi, best][WBoss].Win)} ／ 規定 {F1(g[bi, 0][WBoss].Win)}）");
            foreach (int w in new[] { WBoss, WGuard, WBat, WStrong })
                foreach (int ci in new[] { 0, best })
                    Console.WriteLine($"  - {W8[w].Name}: {cols[ci].Name} {F1(g[bi, ci][w].Win)}%・{g[bi, ci][w].WinTs}T — {g[bi, ci][w].FellCell}");
        }
        Console.WriteLine();

        var pick = Enumerable.Range(0, nb).Where(bi => !boards[bi].Burn).SelectMany(bi => Enumerable.Range(1, nc - 2).Select(ci => (bi, ci)))
            .OrderByDescending(p => g[p.bi, p.ci][WBoss].Win).ThenByDescending(p => A4(g[p.bi, p.ci])).ThenBy(p => p.bi).ThenBy(p => p.ci).First();
        Console.WriteLine($"## 7. 試遊の台「版・目覚め ドルガ」の候補: 燃焼の駒がいない台 × 列でボスにいちばん勝った組 ＝ **{boards[pick.bi].Name} × {cols[pick.ci].Name}**・ボス {F1(g[pick.bi, pick.ci][WBoss].Win)}%（規定 {F1(g[pick.bi, 0][WBoss].Win)}%）・4 波 {F1(A4(g[pick.bi, pick.ci]))}・強 {F1(g[pick.bi, pick.ci][WStrong].Win)}");
        Console.WriteLine($"- 台: {Seats(Apply(boards[pick.bi].F, cols[pick.ci].Tomo, cols[pick.ci].Dolga))}");
        Console.WriteLine();

        Signs(boards.Select((bd, bi) => (bd.Name, g[bi, 0], Enumerable.Range(1, nc - 2).Select(ci => (cols[ci].Name, g[bi, ci])).ToArray())).ToArray());
        Console.WriteLine($"- ドルガの一撃の最大 {_dHitPeak}・1ターンに敵へ与えた量の最大 {_turnPeak}（全台 × 全列 × 全物差しの1戦・int のあふれまで遠い）");
        int F3(S377.B x) => x.X.Keys.Where(k => k.StartsWith("f3k:", StringComparison.Ordinal)).Select(k => int.Parse(k[4..])).DefaultIfEmpty(int.MaxValue).Min();
        var f3s = Enumerable.Range(0, nb).SelectMany(bi => Enumerable.Range(0, nc).SelectMany(ci => Enumerable.Range(0, W8.Length).Select(w => (bi, ci, w, T: F3(g[bi, ci][w]))))).Where(z => z.T != int.MaxValue).OrderBy(z => z.T).ToList();
        Console.WriteLine($"- 1ターンで敵へ 3,000 を超えた最初のT（全台 × 全列 × 全物差し）: {(f3s.Count == 0 ? "—" : string.Join("・", f3s.TakeWhile(z => z.T == f3s[0].T).Take(8).Select(z => $"T{z.T}（{boards[z.bi].Name} × {cols[z.ci].Name} × {W8[z.w].Name}）")))}");
        Console.WriteLine($"- ボスの波で 1ターンで勇者へ 3,000 に届いた最初のT: {string.Join("・", Enumerable.Range(0, nb).SelectMany(bi => Enumerable.Range(0, nc).Select(ci => (bi, ci, T: F3(g[bi, ci][WBoss])))).Where(z => z.T != int.MaxValue).OrderBy(z => z.T).Take(5).Select(z => $"T{z.T}（{boards[z.bi].Name} × {cols[z.ci].Name}）"))}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上（規定との差）／ あふれ）。</summary>
    static void Signs((string Name, S377.B[] Orig, (string Col, S377.B[] M)[] Cells)[] items)
    {
        Console.WriteLine("## 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上（規定との差）／ あふれ）");
        Console.WriteLine();
        var signs = new List<string>();
        foreach (var (name, o, cells) in items)
        {
            foreach (int w in new[] { WGuard, WBat }) { var x = o[w]; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{name} × 規定 × {W8[w].Name}: 1T 決着 {Pc(x.Win1T, x.N)}%"); }
            if (o[WBoss].BossFast > 0) signs.Add($"{name} × 規定 × ボス: 4T 未満の勝ち {Pc(o[WBoss].BossFast, o[WBoss].N)}%");
            foreach (var (col, a) in cells)
            {
                string at = $"{name} × {col}";
                foreach (int w in new[] { WGuard, WBat }) { var x = a[w]; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{at} × {W8[w].Name}: 1T 決着 {Pc(x.Win1T, x.N)}%（規定 {Pc(o[w].Win1T, o[w].N)}%）"); }
                if (a[WBoss].BossFast > 0) signs.Add($"{at} × ボス: 4T 未満の勝ち {Pc(a[WBoss].BossFast, a[WBoss].N)}%");
                for (int w = 0; w < W8.Length; w++)
                {
                    var x = a[w]; var x0 = o[w];
                    if (100.0 * x.Cap / x.N - 100.0 * x0.Cap / x0.N >= 5) signs.Add($"{at} × {W8[w].Name}: 30T の打ち切り {Pc(x0.Cap, x0.N)} → {Pc(x.Cap, x.N)}%");
                    if (x.Overflow > 0) signs.Add($"{at} × {W8[w].Name}: あふれの兆し {x.Overflow} 件");
                }
            }
        }
        if (signs.Count == 0) Console.WriteLine("- 無し。");
        foreach (var s in signs.Take(60)) Console.WriteLine("- " + s);
        if (signs.Count > 60) Console.WriteLine($"- ほか {signs.Count - 60} 件");
    }

    // ---------------------------------------------------------------------------------
    // §4-1 の 3: `compare` ＋ (G2)
    // ---------------------------------------------------------------------------------
    static void Cmp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var all = CB399();
        int nw = EnemyCatalog.Stages.Count;
        var cols = Cols.Take(6).ToArray();
        Console.WriteLine($"# {Tag} §4-1 の 3 `compare` 全 {all.Length} 行 × 列（第1〜5波・seed 0..{CompareSeeds - 1}）");
        Console.WriteLine();
        var grids = cols.Select(c => CGrid(all.Select(r => (r.Name, Apply(r.F, c.Tomo, c.Dolga))).ToArray(), CompareSeeds)).ToArray();
        bool In(int ri) => Has(all[ri].F, "tomo") || Has(all[ri].F, "dolga");
        var rows = Enumerable.Range(0, all.Length).Where(In).ToArray();
        Console.WriteLine($"## 列（トモ ／ ドルガのいる {rows.Length} 行・トモ {rows.Count(ri => Has(all[ri].F, "tomo"))} 行 ／ ドルガ {rows.Count(ri => Has(all[ri].F, "dolga"))} 行）");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", cols.Select(c => c.Name + " 第1〜5波")) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "---|")));
        foreach (int ri in rows.Where(ri => Enumerable.Range(1, cols.Length - 1).Any(c => Enumerable.Range(0, nw).Any(w => grids[c][ri, w] != grids[0][ri, w]))))
            Console.WriteLine($"| {all[ri].Name} | {string.Join(" | ", grids.Select(g => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w])))))} |");
        Console.WriteLine("（どの列でも規定と同じ行は略）");
        Console.WriteLine();
        for (int c = 1; c < cols.Length; c++)
        {
            int moved = 0, up = 0, movedOther = 0; double sum = 0;
            for (int ri = 0; ri < all.Length; ri++)
                for (int w = 0; w < nw; w++)
                    if (grids[c][ri, w] != grids[0][ri, w])
                    {
                        if (!In(ri)) { movedOther++; continue; }
                        moved++; if (grids[c][ri, w] > grids[0][ri, w]) up++; sum += grids[c][ri, w] - grids[0][ri, w];
                    }
            Console.WriteLine($"- {cols[c].Name}: 規定と違うセル {moved}（上がり {up} ／ 落ち {moved - up}・差の合計 {Sg(sum)}pt）・**トモ ／ ドルガのいない行で動いたセル {movedOther}**（0 であること）");
        }
        Console.WriteLine();
        Console.WriteLine("### (G2) 規定から −10.0pt 以上落ちた行（分母 `compare` 全 66 行・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 列 | 落ちた行 | 落ち（波）| 駒ごとの他の行の平均の変化（行数）| 判定 |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int c = 1; c < cols.Length; c++) G2Split(cols[c].Name, all, grids[0], grids[c], rows);
        Console.WriteLine("（行が無ければ −10pt 以上落ちた行は無い）");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Log1(string[] args)
    {
        string bn = args.Length > 3 ? args[3] : "試遊・手番 芯4枚";
        string cn = args.Length > 4 ? args[4] : "L1";
        int wi = args.Length > 5 && int.TryParse(args[5], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 6 && int.TryParse(args[6], out int s0) ? s0 : 0;
        var b = BoardList().FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal));
        if (b.F is null) { Console.WriteLine($"台が無い: {bn}"); return; }
        var c = Cols.FirstOrDefault(x => x.Name == cn); if (c.Tomo is null) c = Cols[0];
        var (r, _, _) = I.Raw(Apply(b.F, c.Tomo, c.Dolga), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name}（{c.Name}）× {W8[wi].Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
