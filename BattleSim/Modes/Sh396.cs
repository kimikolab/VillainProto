using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S389 = Sh389Diag;
using S390 = Sh390Diag;
using S392 = Sh392Diag;

// =====================================================================================
// sh396 —— 第396期【軽い期】段0: ドルガの E1 を規定に ／ 本体: セッキの転生——範囲攻撃を包むスライム（版 S1 ／ S2 ／ S2h）。セッキの規定は変えない。
// 指示書は design/PHASE396_SEKKI_SLIME_SPEC.md、報告は design/PHASE396_SEKKI_SLIME.md。
//
//     dotnet run --project BattleSim -c Release 0 sh396 zero         # §2: `compare` の動き（第395期の HEAD の表と・第395期の E1 の列と）＋ 3 台 × ドルガ（規定 ／ 第395期の E1 ／ 旧）× 5 物差し
//     dotnet run --project BattleSim -c Release 0 sh396 boards       # §4-1 の 1: 手番の軸の 3 台 × 1枚をセッキに（席は全通り・列ごとに最良）× 列（元 ／ 旧セッキ ／ S1 ／ S2 ／ S2h ／ 素体）× 8 物差し
//     dotnet run --project BattleSim -c Release 0 sh396 search <版>  # §4-1 の 2: 芯（トモ ＋ ガン ＋ ドルガ ＋ セッキの版）＋ 1枠 × 並び全通り → 上位 3 台 × 8 物差し
//     dotnet run --project BattleSim -c Release 0 sh396 mix          # §4-1 の 3: 試遊の台すべて × アタッカー以外の席の最良にセッキ（版ごと・同じ席に素体）
//     dotnet run --project BattleSim -c Release 0 sh396 cmp          # §4-3 の 7: `compare` 全 66 行 × 列（旧 ／ S1 ／ S2 ／ S2h・seed 0..199・セッキだけ替える）＋ (G2)
//     dotnet run --project BattleSim -c Release 0 sh396 log <台の頭> <列> <物差し 0..7> <seed> [席]
//     dotnet run --project BattleSim -c Release 0 sh396 check        # 自己検査
//
// **規定のセッキは1つも変えない。** 替えた台 ／ 素体は器具の中だけ（試遊の台に足す「版・包み セッキ」を除く）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p396/predict.md`）。
// =====================================================================================
static partial class Sh396Diag
{
    internal const string PredictFile = ".tmp/p396/predict.md";
    const string Tag = "第396期";
    const int Seeds = 100, CompareSeeds = 200, TT = 7, S1Seeds = 10;
    const double Up = 3.0;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "zero" or "boards" or "search" or "mix" or "cmp" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh396: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "zero": Zero(); return;
            case "boards": Boards(); return;
            case "search": Search(args.Length > 3 ? args[3] : "S2"); return;
            case "mix": Mix(); return;
            case "cmp": Cmp(); return;
            case "log": Log1(args); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh396: モードは zero / boards / search / mix / cmp / log / check。"); return;
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
    static string SeatName(int s) => s switch { 0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3", _ => $"席{s}" };
    static int[] Seats5(Formation f) => f.Occupied().Select(o => o.Slot).Where(s => s < 5).OrderBy(s => s).ToArray();

    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    static readonly int[] W4 = { 0, 1, 2, 5 };
    const int WBoss = 0, WGuard = 1, WBat = 2, WM5 = 5, WStrong = 7;

    // ---------------------------------------------------------------------------------
    // 版 ／ 列 ／ 台
    // ---------------------------------------------------------------------------------
    internal static readonly (string Name, UnitDef D)[] Vers =
        { ("旧セッキ", UnitCatalog.Sekki), ("S1", UnitCatalog.SekkiS1), ("S2", UnitCatalog.SekkiS2), ("S2h", UnitCatalog.SekkiS2h) };
    internal static UnitDef PlainSekki => _plain ??= S392.Plain(UnitCatalog.Sekki);
    static UnitDef? _plain;
    /// <summary>列: 旧セッキ（今の規定）／ S1 ／ S2 ／ S2h ／ セッキを素体にした駒（第392期と同じ素体）。元の台（セッキを入れる前）は別に並べる。</summary>
    internal static (string Name, UnitDef D)[] Cols => Vers.Append(("素体", PlainSekki)).ToArray();

    /// <summary>§4-1 の 1 の台: 試遊・手番 芯3枚 ／ 試遊・手番 縛め ／ 溜め改（バンの席にトモ・第393〜395期と同じ）。ドルガは規定（E1）。</summary>
    internal static (string Name, Formation F)[] TempoBoards()
    {
        var tame = CompareBuilds().First(r => r.Name == "溜め改 (クグ×バン×ガン)").F;
        return new (string, Formation)[]
        {
            ("試遊・手番 芯3枚", Presets.TempoLamp393(UnitCatalog.Tomo, UnitCatalog.Dolga)),
            ("試遊・手番 縛め", Presets.BindTempo395()),
            ("溜め改（バンの席にトモ）", Put(tame, SlotOf(tame, "ban"), UnitCatalog.Tomo)),
        };
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    static void Add(S377.B b, string k, long v) { if (v != 0) b.X[k] = b.X.GetValueOrDefault(k) + v; }
    static long X(S377.B b, string k) => b.X.GetValueOrDefault(k);
    static readonly object MaxLock = new();
    static long _storePeak, _burstPeak;

    static readonly Action<BattleResult, List<UnitState>, List<UnitState>, S377.B> Extra = (r, p, e, b) =>
    {
        var form = p.ToDictionary(u => u.InstanceId);
        var foe = e.ToDictionary(u => u.InstanceId);
        var sumTeam = new Dictionary<int, int>();
        var sekki = p.FirstOrDefault(u => u.Def.Id == "sekki");
        var dolga = p.FirstOrDefault(u => u.Def.Id == "dolga");
        int sid = sekki?.InstanceId ?? -9, did = dolga?.InstanceId ?? -9;
        bool Foe(int id) => foe.ContainsKey(id) || (sumTeam.TryGetValue(id, out int st) && st == BattleContext.EnemyTeam);
        var dmgT = new long[BattleEngine.MaxTurns + 2];
        var deadAt = new Dictionary<int, int>();
        foreach (var x in r.Events)
        {
            int t = Math.Clamp(x.Turn, 0, BattleEngine.MaxTurns + 1);
            int tgt = x.TargetId ?? -3, act = x.ActorId ?? -2;
            switch (x.Kind)
            {
                case BattleEventKind.Summon: sumTeam[tgt] = x.Team ?? -1; break;
                case BattleEventKind.Whet:
                    if (x.WhetRoute == WhetRoute.RallyTurn && form.TryGetValue(tgt, out var ru)) { Add(b, "rally8", 1); if (tgt == sid) Add(b, "rally8Sekki", 1); }
                    break;
                case BattleEventKind.Reveille:
                    if (x.Text == ReveilleLabels.Woken) { Add(b, "revWoken", 1); Add(b, tgt == sid ? "revSekki" : tgt == did ? "revDolga" : "revOther", 1); }
                    break;
                case BattleEventKind.Attack:
                    if (act == did) Add(b, "dSwing", 1);
                    break;
                case BattleEventKind.Damage:
                {
                    bool byUs = form.ContainsKey(act) || (sumTeam.TryGetValue(act, out int sa) && sa == BattleContext.PlayerTeam);
                    if (Foe(tgt) && byUs) dmgT[t] += x.Amount;
                    if (tgt == sid) Add(b, "sTaken", x.Amount);
                    break;
                }
                case BattleEventKind.Death:
                    if (form.ContainsKey(tgt) && !deadAt.ContainsKey(tgt)) deadAt[tgt] = t;
                    if (Foe(tgt)) { Add(b, "kAll", 1); Add(b, act == sid ? "kSekki" : act == did ? "kDolga" : form.ContainsKey(act) ? "kPartner" : "kOther", 1); }
                    break;
                case BattleEventKind.Revive: deadAt.Remove(tgt); break;
            }
        }
        Add(b, "maxTurnDmg", dmgT.Max());
        if (sekki is not null && deadAt.TryGetValue(sid, out int st2)) { Add(b, "sFellN", 1); Add(b, "sFellT", st2); }
        foreach (var (id, tl) in r.TallyByUnit)
        {
            if (tl.R396WrappedFor > 0) Add(b, "wf:" + id, tl.R396WrappedFor);
            if (id == "dolga") { Add(b, "dWall", tl.R394WallHits); Add(b, "dRetreat", tl.R395WallRetreats); }
            if (id != "sekki") continue;
            Add(b, "wrapN", tl.R396WrapHits); Add(b, "wrapAmt", tl.R396WrapAmt); Add(b, "wrapHp", tl.R396WrapHp);
            if (tl.R396WrapByTurn is { } byT) for (int t = 1; t < TT && t < byT.Length; t++) Add(b, $"wrapT{t}", byT[t]);
            Add(b, "burst", tl.R396Bursts); Add(b, "burstAmt", tl.R396BurstAmt); Add(b, "burstDealt", tl.R396BurstDealt);
            Add(b, "bInt", tl.R396BurstInterrupt); Add(b, "bYield", tl.R396BurstYield); Add(b, "bGift", tl.R396BurstGift); Add(b, "bOver", tl.R396BurstOverflow); Add(b, "bOther", tl.R396BurstOther);
            Add(b, "storeMax", tl.R396StoreMax);
            lock (MaxLock) { _storePeak = Math.Max(_storePeak, tl.R396StoreMax); _burstPeak = Math.Max(_burstPeak, tl.R396BurstMax); }
        }
    };

    static S377.B[] M8(Formation f)
    {
        var g = new S377.B[W8.Length];
        Parallel.For(0, W8.Length, wi => g[wi] = S377.Measure(f, W8[wi].Ws, Seeds, Extra));
        return g;
    }
    static S377.B[] M5(Formation f)
    {
        var ws = new[] { WBoss, WGuard, WBat, WM5, WStrong };
        var g = new S377.B[W8.Length];
        Parallel.For(0, ws.Length, k => g[ws[k]] = S377.Measure(f, W8[ws[k]].Ws, Seeds, Extra));
        return g;
    }
    static double A4(S377.B[] a) => W4.Average(wi => a[wi].Win);
    static double D4(S377.B[] a, S377.B[] b) => W4.Average(wi => a[wi].Win - b[wi].Win);
    static double WinRate(Formation f, I.W[] ws, int seeds)
    {
        int w = 0, n = 0;
        foreach (var wv in ws) for (int s = 0; s < seeds; s++) { if (I.Raw(f, wv, s, census: false).R.PlayerWon) w++; n++; }
        return 100.0 * w / n;
    }

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
    static void Zero()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count;
        var now = CGrid(rows, CompareSeeds);
        var e1 = CGrid(rows.Select(r => (r.Name, Has(r.F, "dolga") ? I.Swap(r.F, "dolga", UnitCatalog.DolgaE1) : r.F)).ToArray(), CompareSeeds);
        var map = S390.BalanceMap(S390.GitShow("ff751426:docs/balance.md"));
        int moved = 0, up = 0, sameE1 = 0, cells = 0; double sum = 0;
        var movedRows = new List<string>();
        for (int ri = 0; ri < rows.Length; ri++)
        {
            var cc = map[rows[ri].Name]; var parts = new List<string>();
            for (int w = 0; w < nw; w++)
            {
                cells++;
                if (now[ri, w] == e1[ri, w]) sameE1++;
                double old = double.Parse(cc[w].TrimEnd('%'));
                if (old != now[ri, w]) { moved++; if (now[ri, w] > old) up++; sum += now[ri, w] - old; parts.Add($"第{w + 1}波 {F1(old)} → {F1(now[ri, w])}"); }
            }
            if (parts.Count > 0) movedRows.Add($"| {rows[ri].Name} | {string.Join("・", parts)} |");
        }
        Console.WriteLine($"# {Tag} §2 段0: ドルガの規定 ＝ 第395期の E1");
        Console.WriteLine();
        Console.WriteLine($"## 1. `compare`（全 {rows.Length} 行 × {nw} 波 × seed 0..{CompareSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"- 第395期の HEAD（ff751426）の `docs/balance.md` と違うセル **{moved}**（上がり {up} ／ 落ち {moved - up}・差の合計 {Sg(sum)}pt）");
        Console.WriteLine($"- 第395期の E1 の列（ドルガを `DolgaE1` に替えた全行）と同じセル {sameE1} ／ {cells}");
        Console.WriteLine();
        Console.WriteLine("| 行 | 動いたセル |");
        Console.WriteLine("|---|---|");
        foreach (var l in movedRows) Console.WriteLine(l);
        Console.WriteLine();

        var boards = new (string Name, Formation F)[]
        {
            ("試遊・手番 芯3枚", Presets.TempoLamp393(UnitCatalog.Tomo, UnitCatalog.Dolga)), ("試遊・手番 縛め", Presets.BindTempo395()), ("試遊・手番 探索1位", Presets.TempoSeek392()),
        };
        var dols = new (string Name, UnitDef D)[] { ("規定（第396期）", UnitCatalog.Dolga), ("第395期の E1", UnitCatalog.DolgaE1), ("旧 DolgaH395", UnitCatalog.DolgaH395) };
        var ws = new[] { WBoss, WGuard, WBat, WM5, WStrong };
        Console.WriteLine($"## 2. 3 台 × ドルガ（{string.Join(" ／ ", dols.Select(x => x.Name))}）× ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 台 | ドルガ | " + string.Join(" | ", ws.Select(w => W8[w].Name)) + " | 規定と E1 の差 |");
        Console.WriteLine("|---|---|" + string.Concat(ws.Select(_ => "---|")) + "---|");
        foreach (var bd in boards)
        {
            int ds = SlotOf(bd.F, "dolga");
            var m = dols.Select(x => { var f = Put(bd.F, ds, x.D); var g = new S377.B[ws.Length]; Parallel.For(0, ws.Length, k => g[k] = S377.Measure(f, W8[ws[k]].Ws, Seeds, Extra)); return g; }).ToArray();
            for (int i = 0; i < dols.Length; i++)
                Console.WriteLine($"| {(i == 0 ? $"{bd.Name}（{Seats(bd.F)}）" : "")} | {dols[i].Name} | {string.Join(" | ", m[i].Select(x => x.Cell))} | {(i == 0 ? $"違うセル {Enumerable.Range(0, ws.Length).Count(k => m[0][k].Cell != m[1][k].Cell)}" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-1 の 1: 手番の軸の 3 台 × 1枚をセッキに
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = TempoBoards();
        var cols = Cols;
        int nb = boards.Length, nc = cols.Length;
        var orig = new S377.B[nb][];
        var g = new S377.B[nb, nc, 5][];
        var jobs = new List<(int B, int C, int S)>();
        for (int bi = 0; bi < nb; bi++) { jobs.Add((bi, -1, -1)); for (int ci = 0; ci < nc; ci++) foreach (int s in Seats5(boards[bi].F)) jobs.Add((bi, ci, s)); }
        foreach (var j in jobs)
        {
            if (j.C < 0) orig[j.B] = M8(boards[j.B].F);
            else g[j.B, j.C, j.S] = M8(Put(boards[j.B].F, j.S, cols[j.C].D));
        }
        int Best(int bi, int ci) => Seats5(boards[bi].F).OrderByDescending(s => A4(g[bi, ci, s]!)).ThenByDescending(s => g[bi, ci, s]![WStrong].Win).ThenBy(s => s).First();
        var best = new int[nb, nc];
        for (int bi = 0; bi < nb; bi++) for (int ci = 0; ci < nc; ci++) best[bi, ci] = Best(bi, ci);
        S377.B[] At(int bi, int ci) => g[bi, ci, best[bi, ci]]!;
        int iPlain = nc - 1;

        Console.WriteLine($"# {Tag} §4-1 の 1 手番の軸の台 {nb} × 1枚をセッキに替えた台（席は全通り・列ごとに 4 波 → 強い条件で最良の席）× 列（元 ／ {string.Join(" ／ ", cols.Select(c => c.Name))}）× 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"- **{boards[bi].Name}**: {Seats(boards[bi].F)}・最良の席: " + string.Join("・", Enumerable.Range(0, nc).Select(ci => $"{cols[ci].Name} {SeatName(best[bi, ci])}（{Short(boards[bi].F[best[bi, ci]]!)} を替える）")));
        Console.WriteLine();

        Console.WriteLine("## 1. 勝率・決着T・倒れた編成の駒 ／ 4 波の平均 ／ 元との差 ／ 同じ席の素体との差（4 波 ／ 強）／ 隊の1ターンの最大（ボス ／ 近衛 ／ 大隊）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列（席）| " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 | 元との差（4 波 ／ 強）| 同じ席の素体との差 | 1ターンの最大 |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|---|---|---|");
        string MaxT(S377.B[] x) => string.Join(" ／ ", new[] { WBoss, WGuard, WBat }.Select(w => F0((double)X(x[w], "maxTurnDmg") / Math.Max(1, x[w].N))));
        for (int bi = 0; bi < nb; bi++)
        {
            var o = orig[bi];
            Console.WriteLine($"| {boards[bi].Name} | 元 | {string.Join(" | ", o.Select(x => x.Cell))} | {F1(A4(o))} | — | — | {MaxT(o)} |");
            for (int ci = 0; ci < nc; ci++)
            {
                var a = At(bi, ci); var pl = g[bi, iPlain, best[bi, ci]]!;
                Console.WriteLine($"| | {cols[ci].Name}（{SeatName(best[bi, ci])}）| {string.Join(" | ", a.Select(x => x.Cell))} | {F1(A4(a))} | {Sg(D4(a, o))} ／ {Sg(a[WStrong].Win - o[WStrong].Win)} | {(ci == iPlain ? "—" : $"{Sg(D4(a, pl))} ／ {Sg(a[WStrong].Win - pl[WStrong].Win)}")} | {MaxT(a)} |");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 1'. 席ごとの 4 波 ／ 強い条件（どの席を替えたか・列ごと）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", Enumerable.Range(0, 5).Select(SeatName)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(0, 5).Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {cols[ci].Name} | {string.Join(" | ", Enumerable.Range(0, 5).Select(s => g[bi, ci, s] is { } x ? $"{Short(boards[bi].F[s]!)}: {F1(A4(x))} ／ {F1(x[WStrong].Win)}" : "—"))} |");
        Console.WriteLine();

        int[] w5 = { WBoss, WGuard, WBat, WM5, WStrong };
        Console.WriteLine("## 2. 包む（1戦）——包んだ回数 ／ 吸った量・吸った量 T1〜T6・誰の分を（回）・セッキが HP で受けた量 ／ 被ダメの合計・セッキが倒れた戦 %（T）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 回数 ／ 量 | 量 T1〜T6 | 誰の分を | HP で受けた ／ 被ダメ | 倒れた（T）|");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 1; ci < Vers.Length; ci++)
                foreach (int w in w5)
                {
                    var x = At(bi, ci)[w];
                    string who = string.Join("・", x.X.Keys.Where(k => k.StartsWith("wf:", StringComparison.Ordinal)).OrderByDescending(k => x.X[k]).Take(4).Select(k => $"{Nm(k[3..])} {Per(x.X[k], x.N)}"));
                    Console.WriteLine($"| {(ci == 1 && w == WBoss ? boards[bi].Name : "")} | {(w == WBoss ? cols[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "wrapN"), x.N)} ／ {Per(X(x, "wrapAmt"), x.N)} | {string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => Per(X(x, $"wrapT{t}"), x.N)))} | {(who.Length == 0 ? "—" : who)} | {Per(X(x, "wrapHp"), x.N)} ／ {Per(X(x, "sTaken"), x.N)} | {Pc(X(x, "sFellN"), x.N)}（T{Per(X(x, "sFellT"), X(x, "sFellN"))}）|");
                }
        Console.WriteLine();

        Console.WriteLine("## 3. 弾き返す（1戦）——回数 ／ 1回の量 ／ 敵が失った HP の合計・きっかけ（割り込み＝叩き起こしなど ／ 灯の譲り ／ ターンギフト ／ 溢れ ／ ほか）・叩き起こし（セッキ ／ ドルガ ／ ほか）・ガンの +8（うちセッキ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 回数 ／ 1回 ／ 与えた | きっかけ: 割り込み ／ 譲り ／ ギフト ／ 溢れ ／ ほか | 叩き起こし: セッキ ／ ドルガ ／ ほか | +8（セッキ）|");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < Vers.Length; ci++)
                foreach (int w in w5)
                {
                    var x = At(bi, ci)[w];
                    long n = X(x, "burst");
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? boards[bi].Name : "")} | {(w == WBoss ? cols[ci].Name : "")} | {W8[w].Name} | {Per(n, x.N)} ／ {(n == 0 ? "—" : F0((double)X(x, "burstAmt") / n))} ／ {Per(X(x, "burstDealt"), x.N)} | {Per(X(x, "bInt"), x.N)} ／ {Per(X(x, "bYield"), x.N)} ／ {Per(X(x, "bGift"), x.N)} ／ {Per(X(x, "bOver"), x.N)} ／ {Per(X(x, "bOther"), x.N)} | {Per(X(x, "revSekki"), x.N)} ／ {Per(X(x, "revDolga"), x.N)} ／ {Per(X(x, "revOther"), x.N)} | {Per(X(x, "rally8"), x.N)}（{Per(X(x, "rally8Sekki"), x.N)}）|");
                }
        Console.WriteLine();

        Console.WriteLine("## 4. 誰が何Tに倒れたか（元 ／ 各列の最良の席・ボス ／ 近衛 ／ 大隊 ／ 強い条件）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
        {
            Console.WriteLine($"- **{boards[bi].Name}**");
            foreach (int w in new[] { WBoss, WGuard, WBat, WStrong })
            {
                Console.WriteLine($"  - {W8[w].Name}: 元 {F1(orig[bi][w].Win)}%・{orig[bi][w].WinTs}T — {orig[bi][w].FellCell}");
                for (int ci = 0; ci < nc; ci++) Console.WriteLine($"  - {W8[w].Name}: {cols[ci].Name}（{SeatName(best[bi, ci])}）{F1(At(bi, ci)[w].Win)}%・{At(bi, ci)[w].WinTs}T — {At(bi, ci)[w].FellCell}");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 5. ドルガ（段0 の規定の確認を兼ねる・1戦）——壁で受けた回数 ／ 下がった回数 ／ 振った回数（ボス ／ 近衛 ／ 大隊）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 元 | " + string.Join(" | ", cols.Select(c => c.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(cols.Select(_ => "---|")));
        string DC(S377.B[] a) => string.Join(" ／ ", new[] { WBoss, WGuard, WBat }.Select(w => $"{Per(X(a[w], "dWall"), a[w].N)}・{Per(X(a[w], "dRetreat"), a[w].N)}・{Per(X(a[w], "dSwing"), a[w].N)}"));
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {DC(orig[bi])} | {string.Join(" | ", Enumerable.Range(0, nc).Select(ci => DC(At(bi, ci))))} |");
        Console.WriteLine();

        Console.WriteLine("## 6. 止めを刺したのは誰か（ボス ／ 近衛 ／ 大隊・セッキ ／ ドルガ ／ 相方 ／ ほか %）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 元 | " + string.Join(" | ", cols.Select(c => c.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(cols.Select(_ => "---|")));
        string KC(S377.B[] a) => string.Join(" ／ ", new[] { WBoss, WGuard, WBat }.Select(w => { var x = a[w]; long k = X(x, "kAll"); return $"{Pc(X(x, "kSekki"), k)}・{Pc(X(x, "kDolga"), k)}・{Pc(X(x, "kPartner"), k)}・{Pc(X(x, "kOther"), k)}"; }));
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {KC(orig[bi])} | {string.Join(" | ", Enumerable.Range(0, nc).Select(ci => KC(At(bi, ci))))} |");
        Console.WriteLine();

        Console.WriteLine("## 8. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上（元との差）／ あふれ）・帳簿の最大 ／ 1回の弾き返しの最大");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < Vers.Length; ci++)
                foreach (int s in Seats5(boards[bi].F))
                {
                    var a = g[bi, ci, s]!; string at = $"{boards[bi].Name} × {cols[ci].Name}（{SeatName(s)}）";
                    foreach (int w in new[] { WGuard, WBat }) { var x = a[w]; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{at} × {W8[w].Name}: 1T 決着 {Pc(x.Win1T, x.N)}%"); }
                    if (a[WBoss].BossFast > 0) signs.Add($"{at} × ボス: 4T 未満の勝ち {Pc(a[WBoss].BossFast, a[WBoss].N)}%");
                    for (int w = 0; w < W8.Length; w++)
                    {
                        var x = a[w]; var x0 = orig[bi][w];
                        if (100.0 * x.Cap / x.N - 100.0 * x0.Cap / x0.N >= 5) signs.Add($"{at} × {W8[w].Name}: 30T の打ち切り {Pc(x0.Cap, x0.N)} → {Pc(x.Cap, x.N)}%");
                        if (x.Overflow > 0) signs.Add($"{at} × {W8[w].Name}: あふれの兆し {x.Overflow} 件");
                    }
                }
        if (signs.Count == 0) Console.WriteLine("- 無し（版の列・全部の席）。");
        foreach (var s in signs.Take(40)) Console.WriteLine("- " + s);
        if (signs.Count > 40) Console.WriteLine($"- ほか {signs.Count - 40} 件");
        Console.WriteLine($"- 帳簿の最大（全台 × 全席 × 全物差しの1戦）: {_storePeak}・1回の弾き返しの最大: {_burstPeak}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-1 の 2: 芯（トモ ＋ ガン ＋ ドルガ ＋ セッキの版）＋ 1枠
    // ---------------------------------------------------------------------------------
    static readonly int[][] Perms = Permutations(5).ToArray();
    static IEnumerable<int[]> Permutations(int n)
    {
        if (n == 1) { yield return new[] { 0 }; yield break; }
        foreach (var p in Permutations(n - 1))
            for (int k = 0; k < n; k++) { var q = p.ToList(); q.Insert(k, n - 1); yield return q.ToArray(); }
    }
    static Formation Board(UnitDef[] five, int[] perm) { var f = new Formation(); for (int i = 0; i < 5; i++) f[perm[i]] = five[i]; return f; }
    sealed record Score(double Mean, double Deaths);
    static Score Quick(Formation f, I.W[] ws, int seeds)
    {
        var win = new double[ws.Length]; long deaths = 0, n = 0;
        for (int wi = 0; wi < ws.Length; wi++)
        {
            int w = 0;
            for (int s = 0; s < seeds; s++) { var (r, p, _) = I.Raw(f, ws[wi], s, census: false); if (r.PlayerWon) w++; deaths += p.Count(u => u.LastDeathTurn > 0); n++; }
            win[wi] = 100.0 * w / seeds;
        }
        return new Score(win.Average(), (double)deaths / n);
    }
    static IOrderedEnumerable<T> Rank<T>(IEnumerable<T> xs, Func<T, Score> sc) => xs.OrderByDescending(x => sc(x).Mean).ThenBy(x => sc(x).Deaths);

    static void Search(string ver)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var v = Vers.FirstOrDefault(x => x.Name == ver);
        if (v.D is null) { Console.WriteLine($"版が無い: {ver}"); return; }
        var core = new[] { UnitCatalog.Tomo, UnitCatalog.Gan, UnitCatalog.Dolga, v.D };
        var cands = UnitCatalog.All.Where(d => d.Id is not "tomo" and not "gan" and not "dolga" and not "sekki" and not "tsugi" and not "lili").ToArray();
        var cs = cands.SelectMany(c => Perms.Select(pm => (Free: c, F: Board(core.Append(c).ToArray(), pm)))).ToArray();
        var s1 = new Score[cs.Length];
        Parallel.For(0, cs.Length, k => s1[k] = Quick(cs[k].F, new[] { I.Boss, I.Guard, I.Bat }, 4));
        var pick = Enumerable.Range(0, cs.Length).GroupBy(k => cs[k].Free.Id).SelectMany(g => Rank(g, k => s1[k]).Take(5)).Select(k => cs[k]).ToArray();
        var s2 = new Score[pick.Length];
        Parallel.For(0, pick.Length, k => s2[k] = Quick(pick[k].F, new[] { I.Boss, I.Guard, I.Bat, I.M5 }, 20));
        var ranked = Rank(Enumerable.Range(0, pick.Length).Select(k => (pick[k], s2[k])), x => x.Item2).GroupBy(x => x.Item1.Free.Id).Select(g => g.First()).Take(10).ToList();
        var res = ranked.Select(x => (Free: x.Item1.Free, x.Item1.F, M: M8(x.Item1.F))).OrderByDescending(x => A4(x.M)).ThenByDescending(x => x.M[WStrong].Win).ThenBy(x => W4.Average(w => (double)x.M[w].FormDeaths / x.M[w].N)).Take(3).ToList();
        Console.WriteLine($"# {Tag} §4-1 の 2 芯（トモ ＋ ガン ＋ ドルガ ＋ セッキ {v.Name}）＋ 1枠 × 並び全通り（ツギ ／ リリ除く・{cands.Length} 枚 × 120 並び）→ 上位 3 台 × 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("段1: ボス ／ 近衛 ／ 大隊 × seed 0..3（1枠ごとに上位 5 並び）→ 段2: ＋ 本編 第5波 × seed 0..19（1枠ごとに 1 並び・上位 10）→ 段3: 8 物差し × seed 0..99。");
        Console.WriteLine();
        Console.WriteLine("| # | 1枠 | 並び（前1 ／ 前3 ／ 中央 ／ 後1 ／ 後3）| " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 |");
        Console.WriteLine("|--:|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|");
        for (int i = 0; i < res.Count; i++) Console.WriteLine($"| {i + 1} | {Short(res[i].Free)} | {Seats(res[i].F)} | {string.Join(" | ", res[i].M.Select(x => x.Cell))} | {F1(A4(res[i].M))} |");
        Console.WriteLine();
        var top = res[0];
        foreach (int w in new[] { WBoss, WGuard, WBat, WStrong })
        {
            var x = top.M[w];
            Console.WriteLine($"- 1位 × {W8[w].Name}: 包んだ {Per(X(x, "wrapN"), x.N)} ／ {Per(X(x, "wrapAmt"), x.N)}・弾き返し {Per(X(x, "burst"), x.N)}（割り込み {Per(X(x, "bInt"), x.N)} ／ 溢れ {Per(X(x, "bOver"), x.N)}）・叩き起こし セッキ {Per(X(x, "revSekki"), x.N)} ／ ドルガ {Per(X(x, "revDolga"), x.N)}・誰が何Tに倒れたか {x.FellCell}");
        }
        Console.WriteLine();
        int ss = SlotOf(top.F, "sekki");
        var pl = M8(Put(top.F, ss, PlainSekki)); var old = M8(Put(top.F, ss, UnitCatalog.Sekki));
        Console.WriteLine($"- 1位の台のセッキを替えた差: 素体 4 波 {Sg(D4(top.M, pl))} ／ 強 {Sg(top.M[WStrong].Win - pl[WStrong].Win)}・旧セッキ 4 波 {Sg(D4(top.M, old))} ／ 強 {Sg(top.M[WStrong].Win - old[WStrong].Win)}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-1 の 3: 寄せ集め（試遊の台すべて × アタッカー以外の席の最良）
    // ---------------------------------------------------------------------------------
    internal static (string Name, Formation F)[] Play() => Presets.Versions.Where(r => r.Name.StartsWith("試遊・", StringComparison.Ordinal) && !Has(r.F, "sekki")).ToArray();

    static void Mix()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Play();
        int nb = boards.Length;
        var s1W = new[] { I.Boss, I.Guard, I.Bat, I.M5 };
        var bs = new double[nb][]; var atk = new int[nb];
        var grid = new double[Vers.Length, nb, 5][];
        var jobs = new List<(int V, int B, int S)>();
        for (int b = 0; b < nb; b++) jobs.Add((-1, b, -1));
        for (int v = 0; v < Vers.Length; v++) for (int b = 0; b < nb; b++) foreach (int s in Seats5(boards[b].F)) jobs.Add((v, b, s));
        Parallel.ForEach(jobs, j =>
        {
            var f = j.V < 0 ? boards[j.B].F : Put(boards[j.B].F, j.S, Vers[j.V].D);
            var r = new double[5];
            for (int w = 0; w < 4; w++) r[w] = WinRate(f, new[] { s1W[w] }, S1Seeds);
            r[4] = WinRate(f, I.StrongWaves, S1Seeds);
            if (j.V < 0) { bs[j.B] = r; atk[j.B] = S389.AttackerSeat(f); } else grid[j.V, j.B, j.S] = r;
        });
        var cache = new Dictionary<string, S377.B[]>();
        S377.B[] M(int b, int s, UnitDef? d) { string k = $"{b}:{s}:{d?.Name}:{d?.Traits.Count}"; lock (cache) if (cache.TryGetValue(k, out var x)) return x; var y = M8(d is null ? boards[b].F : Put(boards[b].F, s, d)); lock (cache) cache[k] = y; return y; }

        Console.WriteLine($"# {Tag} §4-1 の 3 寄せ集め —— 試遊の台 {nb}（セッキのいない台）× アタッカー以外の席の最良にセッキ（版ごと・第389〜391期のハギと同じやり方・同じ席に素体）");
        Console.WriteLine();
        Console.WriteLine($"段1: seed 0..{S1Seeds - 1} × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波（4 波の差 → 強い条件の差で席を選ぶ）。段2: seed 0..{Seeds - 1} × 8 物差し。アタッカー ＝ 元の台で敵への与ダメの合計がいちばん大きい駒の席（`Sh389Diag.AttackerSeat`）。");
        Console.WriteLine();
        Console.WriteLine("| 台（アタッカー）| 元 4 波 ／ 強 | " + string.Join(" | ", Vers.Select(v => $"{v.Name}: 席（誰を）4 波の差 ／ 強の差")) + " | 素体（S2 の席）4 波 ／ 強の差 |");
        Console.WriteLine("|---|---|" + string.Concat(Vers.Select(_ => "---|")) + "---|");
        var lists = Vers.Select(_ => new List<(int B, int S, double D4, double DS)>()).ToArray();
        var plainList = new List<(double D4, double DS)>();
        for (int b = 0; b < nb; b++)
        {
            var b0 = M(b, -1, null);
            var cells = new List<string>(); int s2Seat = -1;
            for (int v = 0; v < Vers.Length; v++)
            {
                var seats = Seats5(boards[b].F).Where(s => s != atk[b]).OrderByDescending(s => Enumerable.Range(0, 4).Average(w => grid[v, b, s]![w] - bs[b][w])).ThenByDescending(s => grid[v, b, s]![4] - bs[b][4]).ThenBy(s => s).ToArray();
                if (seats.Length == 0) { cells.Add("—"); continue; }
                int s0 = seats[0]; var a = M(b, s0, Vers[v].D);
                double d4 = D4(a, b0), ds = a[WStrong].Win - b0[WStrong].Win;
                lists[v].Add((b, s0, d4, ds));
                if (Vers[v].Name == "S2") s2Seat = s0;
                cells.Add($"{SeatName(s0)}（{Short(boards[b].F[s0]!)}）{Sg(d4)} ／ {Sg(ds)}");
            }
            string pls = "—";
            if (s2Seat >= 0) { var p = M(b, s2Seat, PlainSekki); double pd4 = D4(p, b0), pds = p[WStrong].Win - b0[WStrong].Win; plainList.Add((pd4, pds)); pls = $"{Sg(pd4)} ／ {Sg(pds)}"; }
            Console.WriteLine($"| {boards[b].Name}（{SeatName(atk[b])} {Short(boards[b].F[atk[b]]!)}）| {F1(A4(b0))} ／ {F1(b0[WStrong].Win)} | {string.Join(" | ", cells)} | {pls} |");
        }
        Console.WriteLine();
        for (int v = 0; v < Vers.Length; v++)
        {
            var l = lists[v];
            Console.WriteLine($"- **{Vers[v].Name}**: 4 波で +{Up:F0}pt 以上上がった台 **{l.Count(x => x.D4 >= Up)}** ／ {l.Count}・強い条件で +{Up:F0}pt 以上 {l.Count(x => x.DS >= Up)}・4 波の差の平均 {Sg(l.Average(x => x.D4))}・強の差の平均 {Sg(l.Average(x => x.DS))}" + (l.Count == 0 ? "" : $"・最良 {boards[l.OrderByDescending(x => x.D4).ThenByDescending(x => x.DS).First().B].Name}"));
        }
        if (plainList.Count > 0) Console.WriteLine($"- 素体（S2 の席）: 4 波で +{Up:F0}pt 以上 {plainList.Count(x => x.D4 >= Up)} ／ {plainList.Count}・4 波の差の平均 {Sg(plainList.Average(x => x.D4))}・強の差の平均 {Sg(plainList.Average(x => x.DS))}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-3 の 7: `compare` ＋ (G2)
    // ---------------------------------------------------------------------------------
    internal static (string Name, Formation F)[] RowsFor(UnitDef d) => CompareBuilds().Select(r => (r.Name, Has(r.F, "sekki") ? I.Swap(r.F, "sekki", d) : r.F)).ToArray();

    static void Cmp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var all = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count;
        var grids = Vers.Select(v => CGrid(RowsFor(v.D), CompareSeeds)).ToArray();
        var sRows = Enumerable.Range(0, all.Length).Where(ri => Has(all[ri].F, "sekki")).ToArray();
        Console.WriteLine($"# {Tag} §4-3 の 7 `compare` 全 {all.Length} 行 × 列（第1〜5波・seed 0..{CompareSeeds - 1}・セッキだけ替える）——セッキのいる {sRows.Length} 行を並べる");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", Vers.Select(c => c.Name + " 第1〜5波")) + " |");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "---|")));
        foreach (int ri in sRows) Console.WriteLine($"| {all[ri].Name} | {string.Join(" | ", grids.Select(g => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w])))))} |");
        Console.WriteLine();
        for (int c = 1; c < Vers.Length; c++)
        {
            int moved = 0, up = 0, movedOther = 0; double sum = 0;
            for (int ri = 0; ri < all.Length; ri++)
                for (int w = 0; w < nw; w++)
                    if (grids[c][ri, w] != grids[0][ri, w])
                    {
                        if (!Has(all[ri].F, "sekki")) { movedOther++; continue; }
                        moved++; if (grids[c][ri, w] > grids[0][ri, w]) up++; sum += grids[c][ri, w] - grids[0][ri, w];
                    }
            Console.WriteLine($"- {Vers[c].Name}: 旧セッキと違うセル {moved}（上がり {up} ／ 落ち {moved - up}・差の合計 {Sg(sum)}pt）・**セッキのいない行で動いたセル {movedOther}**（0 であること）");
        }
        Console.WriteLine();
        Console.WriteLine("## (G2) 旧セッキから −10.0pt 以上落ちた行の分け（分母 `compare` 全 66 行・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 落ちた行 | 落ち（波）| 駒ごとの他の行の平均の変化（行数）| 判定 |");
        Console.WriteLine("|---|---|---|---|---|");
        int found = 0;
        for (int c = 1; c < Vers.Length; c++)
            foreach (int ri in sRows)
            {
                var drops = Enumerable.Range(1, nw - 1).Where(w => grids[c][ri, w] - grids[0][ri, w] <= -10.0).ToArray();
                if (drops.Length == 0) continue;
                found++;
                var parts = new List<string>(); bool broken = false;
                foreach (var id in all[ri].F.Occupied().Select(o => o.Def.Id).Distinct())
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
        string bn = args.Length > 3 ? args[3] : "溜め改";
        string cn = args.Length > 4 ? args[4] : "S2";
        int wi = args.Length > 5 && int.TryParse(args[5], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 2, seed = args.Length > 6 && int.TryParse(args[6], out int s0) ? s0 : 0;
        var b = TempoBoards().FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal));
        if (b.F is null) { Console.WriteLine($"台が無い: {bn}"); return; }
        int seat = args.Length > 7 && int.TryParse(args[7], out int st) ? st : 2;
        var c = Cols.FirstOrDefault(x => x.Name == cn); if (c.D is null) c = Cols[0];
        var (r, _, _) = I.Raw(Put(b.F, seat, c.D), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name}（{SeatName(seat)} を {c.Name}）× {W8[wi].Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
