using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S390 = Sh390Diag;
using S392 = Sh392Diag;

// =====================================================================================
// sh393 —— 第393期【軽い期】本体: 手番の軸——灯が休む者に溜まり、起こし、力を運ぶ（トモの版 T1 ／ T2 ／ T3）。規定は変えない。
// 指示書は design/PHASE393_TEMPO_LAMP_SPEC.md、報告は design/PHASE393_TEMPO_LAMP.md。
//
//     dotnet run --project BattleSim -c Release 0 sh393 boards       # §3-1 の 1〜3: 台 5 × 列（元 ／ 規定 ／ T1 ／ T2 ／ T3 ／ 素体）× 8 物差し（seed 0..99）——灯 ／ ドルガ ／ 流れ ／ ガン ／ 倒れた T ／ 止め
//     dotnet run --project BattleSim -c Release 0 sh393 cmp          # §3-3 の 8: トモ ／ ガンのいる `compare` の行 × 列（規定 ／ T1 ／ T2 ／ T3・第1〜5波・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh393 search <版>  # §3-1 の 4: 芯 3 枚（トモの版 ＋ ガン ＋ ドルガ）＋ 2枠の席の探索（第392期と同じやり方）→ 上位 3 台 × 8 物差し・1位の台のトモ ／ ドルガを素体にした差
//     dotnet run --project BattleSim -c Release 0 sh393 log <台の頭> <列> <物差し 0..7> <seed>
//     dotnet run --project BattleSim -c Release 0 sh393 check        # 自己検査
//
// **規定の駒は1つも変えない。** 替えた台 ／ 素体は器具の中だけ（試遊の台に足す「版・灯 手番」を除く）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p393/predict.md`）。
// =====================================================================================
static partial class Sh393Diag
{
    internal const string PredictFile = ".tmp/p393/predict.md";
    const string Tag = "第393期";
    const int Seeds = 100, CompareSeeds = 200, TT = 9;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "boards" or "cmp" or "search" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh393: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "boards": Boards(); return;
            case "cmp": Cmp(); return;
            case "search": Search(args.Length > 3 ? args[3] : "T3"); return;
            case "log": Log1(args); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh393: モードは boards / cmp / search / log / check。"); return;
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
    const int WBoss = 0, WGuard = 1, WBat = 2, WStrong = 7;

    // ---------------------------------------------------------------------------------
    // 版 ／ 列 ／ 台
    // ---------------------------------------------------------------------------------
    internal static readonly (string Name, UnitDef D)[] Vers =
        { ("規定", UnitCatalog.TomoH393), ("T1", UnitCatalog.TomoT1), ("T2", UnitCatalog.TomoT2), ("T3", UnitCatalog.TomoT3) };
    internal static UnitDef PlainTomo => _plain ??= S392.Plain(UnitCatalog.Tomo);
    static UnitDef? _plain;
    /// <summary>列: 規定 ／ T1 ／ T2 ／ T3 ／ トモを素体に替えた台（`null` の列は無い——元の台は別に並べる）。</summary>
    internal static (string Name, UnitDef D)[] Cols => Vers.Append(("素体", PlainTomo)).ToArray();

    /// <summary>
    /// §3-1 の 1〜3 の台（トモの席 ＝ `Slot`）。縛め収入型 ／ 溜め改にはトモがいないので、**第392期の素体の差で 0 だったバンの席にトモ**を置く（元 ＝ `compare` の行のまま）。
    /// 芯 3 枚の台 ＝ 第392期の探索の芯 3 枚の最良（19 位・ボルグ ＋ ベニ）。
    /// </summary>
    internal static (string Name, Formation F, int Slot, Formation? Orig)[] BoardList()
    {
        var all = CompareBuilds();
        Formation Row(string n) => all.First(r => r.Name == n).F;
        int SlotOf(Formation f, string id) => f.Occupied().First(o => o.Def.Id == id).Slot;
        var lamp = Row("灯×薙ぎ (トモ×ドルガ)"); var bind = Row("縛め収入型 (クグ×バン×ガン)"); var tame = Row("溜め改 (クグ×バン×ガン)");
        var core3 = Formation.Build(front1: UnitCatalog.Borg, front3: UnitCatalog.Dolga, center: UnitCatalog.Beni, back1: UnitCatalog.Gan, back3: UnitCatalog.Tomo);
        var seek = Presets.TempoSeek392();
        return new (string, Formation, int, Formation?)[]
        {
            ("灯×薙ぎ (トモ×ドルガ)", lamp, SlotOf(lamp, "tomo"), null),
            ("縛め収入型（バンの席にトモ）", bind, SlotOf(bind, "ban"), bind),
            ("溜め改（バンの席にトモ）", tame, SlotOf(tame, "ban"), tame),
            ("芯 3 枚（第392期 19 位）", core3, 4, null),
            ("試遊・手番 探索1位", seek, SlotOf(seek, "tomo"), null),
        };
    }

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
        var owner = new int[TT + 1]; for (int i = 0; i <= TT; i++) owner[i] = -1;
        var litAmt = new long[TT + 1];
        var deadAt = new Dictionary<int, int>();
        int lastFoeDeathTurn = -9, lastFoeDeathBy = -2;
        bool yieldPending = false, yieldToDolga = false, yieldLoop = false, ganPending = false;
        int loopsThis = 0, loopRun = 0, loopMax = 0;
        foreach (var x in r.Events)
        {
            int t = Math.Clamp(x.Turn, 0, BattleEngine.MaxTurns + 1);
            int tgt = x.TargetId ?? -3, act = x.ActorId ?? -2;
            switch (x.Kind)
            {
                case BattleEventKind.Summon: sumTeam[tgt] = x.Team ?? -1; break;
                case BattleEventKind.TurnStart: yieldPending = false; ganPending = false; break;
                case BattleEventKind.Whet:
                    if (x.WhetRoute == WhetRoute.Taillight && t is >= 1 and < TT && form.ContainsKey(tgt)) { owner[t] = tgt; litAmt[t] = x.Amount; Add(b, "litAmt", x.Amount); Add(b, "litN", 1); }
                    if (x.WhetRoute == WhetRoute.RallyTurn && form.TryGetValue(tgt, out var ru)) { Add(b, "rally8", 1); Add(b, "rally8:" + ru.Def.Id, 1); }
                    if (tgt == tid) Add(b, x.WhetRoute switch { WhetRoute.RallyTurn => "toTomo:gan", WhetRoute.RallyOpening => "toTomo:open", _ => "toTomo:other" }, x.Amount);
                    break;
                case BattleEventKind.Highlight:
                    if (act == tid && x.Text is { } tx && tx.Contains("道を譲る", StringComparison.Ordinal))
                    {
                        yieldPending = true;
                        Add(b, t % 2 == 0 ? "yieldEven" : "yieldOdd", 1);
                        if (tx.Contains("叩き起こす", StringComparison.Ordinal)) Add(b, "yieldWake", 1);
                        yieldLoop = lastFoeDeathBy == did && lastFoeDeathTurn >= t - 1;
                    }
                    break;
                case BattleEventKind.Reveille:
                    if (tgt == did && x.Text == ReveilleLabels.Woken) ganPending = true;
                    if (x.Text == ReveilleLabels.Woken) Add(b, "revWoken", 1);
                    break;
                case BattleEventKind.Attack:
                    if (act == did)
                    {
                        if (x.Reaction && ganPending) { Add(b, "dGanSwing", 1); ganPending = false; }
                        else if (!x.Reaction && yieldPending)
                        {
                            Add(b, "dYieldSwing", 1); yieldPending = false;
                            if (yieldLoop) { Add(b, "loop", 1); loopsThis++; loopRun++; loopMax = Math.Max(loopMax, loopRun); } else loopRun = 0;
                        }
                        else if (!x.Reaction) Add(b, "dOwnSwing", 1);
                        else Add(b, "dOtherReact", 1);
                        Add(b, "dSwing", 1);
                        if (x.Pattern == AttackPattern.All) Add(b, "dAll", 1);
                    }
                    else if (!x.Reaction && yieldPending && form.ContainsKey(act)) yieldPending = false;   // 灯した相手がドルガ以外
                    break;
                case BattleEventKind.Damage:
                {
                    bool byUs = form.ContainsKey(act) || (sumTeam.TryGetValue(act, out int sa) && sa == BattleContext.PlayerTeam);
                    if (Foe(tgt) && byUs) dmgT[t] += x.Amount;
                    if (act == did && Foe(tgt)) Add(b, "dDmg", x.Amount);
                    break;
                }
                case BattleEventKind.StatSnapshot:
                    if (tgt == did && t is >= 1 and < TT) { Add(b, $"dBonus{t}", x.Amount - dolga!.Def.Attack); Add(b, $"dN{t}", 1); if (x.Pattern == AttackPattern.All) Add(b, $"dAllT{t}", 1); Add(b, $"dBonusMaxSum{t}", 0); }
                    break;
                case BattleEventKind.Death:
                    if (form.ContainsKey(tgt) && !deadAt.ContainsKey(tgt)) deadAt[tgt] = t;
                    if (Foe(tgt))
                    {
                        lastFoeDeathTurn = t; lastFoeDeathBy = act;
                        Add(b, "kAll", 1); Add(b, act == did ? "kDolga" : form.ContainsKey(act) ? "kPartner" : "kOther", 1);
                    }
                    break;
                case BattleEventKind.Revive: deadAt.Remove(tgt); break;
            }
        }
        Add(b, "maxTurnDmg", dmgT.Max());
        Add(b, "loopMax", loopMax);
        if (dolga is not null)
        {
            long mx = 0;
            for (int t = 1; t < TT; t++) if (X(b, $"dN{t}") > 0) mx = Math.Max(mx, X(b, $"dBonus{t}"));   // 1戦なので和 ＝ 値
            Add(b, "dBonusMax", mx);
        }
        if (tomo is not null)
        {
            long cum = 0;
            for (int t = 1; t < TT; t++)
            {
                if (owner[t] >= 0) Add(b, $"lit{t}", 1);
                if (owner[t] == did && did >= 0) { cum = (t >= 2 && owner[t - 1] == did ? cum : 0) + litAmt[t]; Add(b, $"litD{t}", 1); Add(b, $"litDAmt{t}", cum); Add(b, "litDMaxCand", 0); b.X["litDMax"] = Math.Max(X(b, "litDMax"), cum); }
                if (t >= 2 && owner[t - 1] >= 0 && owner[t] != owner[t - 1])
                {
                    int prev = owner[t - 1];
                    string why = deadAt.TryGetValue(prev, out int dt) && dt <= t ? "dead" : prev == did && t % 2 == 0 ? "dolgaRest" : owner[t] < 0 ? "none" : "other";
                    Add(b, "douse:" + why, 1);
                }
            }
            if (r.TallyByUnit.TryGetValue("tomo", out var tt))
            {
                Add(b, "yields", tt.TaillightYields); Add(b, "noDeath", tt.TaillightNoDeath); Add(b, "noTarget", tt.TaillightNoTarget); Add(b, "tPeak", tt.TaillightPeak);
                Add(b, "carried", tt.R393Carried); Add(b, "carryN", tt.R393CarryLights); Add(b, "wakes", tt.R393Wakes); Add(b, "ateRally", tt.R393WakeAteRally); Add(b, "rallyToLamp", tt.R393RallyToLamp);
            }
        }
    };

    static S377.B[] M8(Formation f)
    {
        var g = new S377.B[W8.Length];
        Parallel.For(0, W8.Length, wi => g[wi] = S377.Measure(f, W8[wi].Ws, Seeds, Extra));
        return g;
    }
    static double A4(S377.B[] a) => W4.Average(wi => a[wi].Win);

    // ---------------------------------------------------------------------------------
    // §3-1 の 1〜3: 台 × 列
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        var cols = Cols;
        var g = new S377.B[boards.Length, cols.Length][];
        var orig = new S377.B[boards.Length][];
        for (int bi = 0; bi < boards.Length; bi++)
        {
            for (int ci = 0; ci < cols.Length; ci++) g[bi, ci] = M8(Put(boards[bi].F, boards[bi].Slot, cols[ci].D));
            if (boards[bi].Orig is { } o) orig[bi] = M8(o);
        }
        Console.WriteLine($"# {Tag} §3-1 の 1〜3 台 {boards.Length} × 列（{string.Join(" ／ ", cols.Select(c => c.Name))}）× 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var bd in boards) Console.WriteLine($"- **{bd.Name}**: {Seats(Put(bd.F, bd.Slot, UnitCatalog.Tomo))}（トモの席 {bd.Slot}）{(bd.Orig is null ? "" : $"・元 ＝ {Seats(bd.Orig)}")}");
        Console.WriteLine();

        Console.WriteLine("## 1. 勝率・決着T・倒れた編成の駒 ／ 4 波の平均 ／ ボスの倒しT ／ 隊の1ターンの最大（近衛）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 | 規定との差 | 1ターンの最大（ボス ／ 近衛 ／ 大隊）|");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|--:|---|");
        string MaxT(S377.B[] x) => string.Join(" ／ ", new[] { WBoss, WGuard, WBat }.Select(w => F0((double)X(x[w], "maxTurnDmg") / Math.Max(1, x[w].N))));
        for (int bi = 0; bi < boards.Length; bi++)
        {
            if (orig[bi] is { } o) Console.WriteLine($"| {boards[bi].Name} | 元（バン）| {string.Join(" | ", o.Select(x => x.Cell))} | {F1(A4(o))} | — | {MaxT(o)} |");
            for (int ci = 0; ci < cols.Length; ci++)
                Console.WriteLine($"| {(ci == 0 && orig[bi] is null ? boards[bi].Name : "")} | {cols[ci].Name} | {string.Join(" | ", g[bi, ci].Select(x => x.Cell))} | {F1(A4(g[bi, ci]))} | {(ci == 0 ? "—" : Sg(A4(g[bi, ci]) - A4(g[bi, 0])))} | {MaxT(g[bi, ci])} |");
        }
        Console.WriteLine();

        int[] w3 = { WBoss, WGuard, WBat, 5 };
        Console.WriteLine("## 2. 灯（ボス ／ 近衛 ／ 大隊 ／ 本編 第5波・1戦）——持ち主がドルガの戦 % T1〜T8・ドルガに載った灯の平均 T1〜T8 ／ 最大・消えた（ドルガの休み ／ 倒れた ／ ほか ／ 相手なし）・1回の灯 ／ 運んだ力（1回 ／ 1戦）・トモに入った力（ガン ／ 開戦 ／ ほか）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 持ち主がドルガ % T1〜T8 | ドルガの灯 T1〜T8 | 最大 | 消えた | 1回の灯 | 運んだ（1回 ／ 1戦）| トモに入った（ガン ／ 開戦 ／ ほか）|");
        Console.WriteLine("|---|---|---|---|---|--:|---|--:|---|---|");
        for (int bi = 0; bi < boards.Length; bi++)
        {
            if (!Has(boards[bi].F, "dolga")) continue;
            for (int ci = 0; ci < Vers.Length; ci++)
                foreach (int w in w3)
                {
                    var x = g[bi, ci][w];
                    string own = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"lit{t}") == 0 ? "—" : Pc(X(x, $"litD{t}"), X(x, $"lit{t}"))));
                    string amt = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"litD{t}") == 0 ? "—" : F0((double)X(x, $"litDAmt{t}") / X(x, $"litD{t}"))));
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? boards[bi].Name : "")} | {(w == WBoss ? Vers[ci].Name : "")} | {W8[w].Name} | {own} | {amt} | {Per(X(x, "litDMax"), x.N)} | {Per(X(x, "douse:dolgaRest"), x.N)} ／ {Per(X(x, "douse:dead"), x.N)} ／ {Per(X(x, "douse:other"), x.N)} ／ {Per(X(x, "douse:none"), x.N)} | {(X(x, "litN") == 0 ? "—" : F1((double)X(x, "litAmt") / X(x, "litN")))} | {(X(x, "carryN") == 0 ? "—" : F1((double)X(x, "carried") / X(x, "carryN")))} ／ {Per(X(x, "carried"), x.N)} | {Per(X(x, "toTomo:gan"), x.N)} ／ {Per(X(x, "toTomo:open"), x.N)} ／ {Per(X(x, "toTomo:other"), x.N)} |");
                }
        }
        Console.WriteLine();

        Console.WriteLine("## 3. ドルガ（1戦）——積まれた力 T1〜T8 ／ 最大・全体のターン % T1〜T8・振った（自分の手番 ／ トモに起こされた ／ ガンに起こされた）うち全体・1振りの与ダメ");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 積まれた力 T1〜T8 | 最大 | 全体のターン % T1〜T8 | 振った: 自分 ／ トモ ／ ガン（全体）| 1振りの与ダメ |");
        Console.WriteLine("|---|---|---|---|--:|---|---|--:|");
        for (int bi = 0; bi < boards.Length; bi++)
        {
            if (!Has(boards[bi].F, "dolga")) continue;
            for (int ci = 0; ci < cols.Length; ci++)
                foreach (int w in w3)
                {
                    var x = g[bi, ci][w];
                    string bon = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"dN{t}") == 0 ? "—" : F1((double)X(x, $"dBonus{t}") / X(x, $"dN{t}"))));
                    string all = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"dN{t}") == 0 ? "—" : Pc(X(x, $"dAllT{t}"), X(x, $"dN{t}"))));
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? boards[bi].Name : "")} | {(w == WBoss ? cols[ci].Name : "")} | {W8[w].Name} | {bon} | {Per(X(x, "dBonusMax"), x.N)} | {all} | {Per(X(x, "dOwnSwing"), x.N)} ／ {Per(X(x, "dYieldSwing"), x.N)} ／ {Per(X(x, "dGanSwing"), x.N)}（{Per(X(x, "dAll"), x.N)}）| {(X(x, "dSwing") == 0 ? "—" : F0((double)X(x, "dDmg") / X(x, "dSwing")))} |");
                }
        }
        Console.WriteLine();

        Console.WriteLine("## 4. 流れ ／ ガン（1戦）——トモが譲った（奇数 ／ 偶数ターン・うち休み番を起こした）・譲れなかった（敵が倒れていない ／ 灯した味方なし）・**ループ（ドルガの撃破 → トモが譲る → ドルガがまた振る）の回数 ／ 1戦の最長の連続**・ガンの +8（誰に・トモ）／ 叩き起こし ／ 起こされたドルガの分だけ +8 が減った（食い合い）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 譲った: 奇 ／ 偶（起こした）| 譲れない: 敵が倒れていない ／ 相手なし | ループ（最長）| +8（誰に）| 叩き起こし | 食い合い |");
        Console.WriteLine("|---|---|---|---|---|---|---|--:|--:|");
        for (int bi = 0; bi < boards.Length; bi++)
            for (int ci = 0; ci < Vers.Length; ci++)
                foreach (int w in w3)
                {
                    var x = g[bi, ci][w];
                    string r8 = Has(boards[bi].F, "gan") ? $"{Per(X(x, "rally8"), x.N)}（" + string.Join("・", x.X.Keys.Where(k => k.StartsWith("rally8:", StringComparison.Ordinal)).OrderByDescending(k => x.X[k]).Select(k => $"{Nm(k[7..])} {Per(x.X[k], x.N)}")) + "）" : "—";
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? boards[bi].Name : "")} | {(w == WBoss ? Vers[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "yieldOdd"), x.N)} ／ {Per(X(x, "yieldEven"), x.N)}（{Per(X(x, "yieldWake"), x.N)}）| {Per(X(x, "noDeath"), x.N)} ／ {Per(X(x, "noTarget"), x.N)} | {Per(X(x, "loop"), x.N)}（{Per(X(x, "loopMax"), x.N)}）| {r8} | {Per(X(x, "revWoken"), x.N)} | {Per(X(x, "ateRally"), x.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 5. 止めを刺したのは誰か（敵の撃破・ボス ／ 近衛 ／ 大隊・ドルガ ／ 相方 ／ ほか %）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", cols.Select(c => c.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "---|")));
        for (int bi = 0; bi < boards.Length; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(0, cols.Length).Select(ci => string.Join(" ／ ", new[] { WBoss, WGuard, WBat }.Select(w => { var x = g[bi, ci][w]; long a = X(x, "kAll"); return $"{Pc(X(x, "kDolga"), a)}・{Pc(X(x, "kPartner"), a)}・{Pc(X(x, "kOther"), a)}"; }))))} |");
        Console.WriteLine();

        Console.WriteLine("## 6. 誰が何Tに倒れたか（規定 ／ 4 波の平均がいちばん良い版・ボス ／ 近衛 ／ 大隊）");
        Console.WriteLine();
        for (int bi = 0; bi < boards.Length; bi++)
        {
            int best = Enumerable.Range(1, Vers.Length - 1).OrderByDescending(ci => A4(g[bi, ci])).ThenByDescending(ci => g[bi, ci][WStrong].Win).First();
            Console.WriteLine($"- **{boards[bi].Name}**（最良の版 {Vers[best].Name}・4 波 {F1(A4(g[bi, best]))} ／ 規定 {F1(A4(g[bi, 0]))}・強 {F1(g[bi, best][WStrong].Win)} ／ {F1(g[bi, 0][WStrong].Win)}）");
            foreach (int w in new[] { WBoss, WGuard, WBat })
            {
                Console.WriteLine($"  - {W8[w].Name}: 規定 {F1(g[bi, 0][w].Win)}%・{g[bi, 0][w].WinTs}T — {g[bi, 0][w].FellCell}");
                Console.WriteLine($"  - {W8[w].Name}: {Vers[best].Name} {F1(g[bi, best][w].Win)}%・{g[bi, best][w].WinTs}T — {g[bi, best][w].FellCell}");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 7. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上 ／ あふれ）・ドルガの積まれた力の最大（1戦の最大の平均）");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < boards.Length; bi++)
            for (int ci = 1; ci < cols.Length; ci++)
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
        for (int bi = 0; bi < boards.Length; bi++)
        {
            if (!Has(boards[bi].F, "dolga")) continue;
            Console.WriteLine($"- {boards[bi].Name}: ドルガの力の最大（ボス ／ 近衛 ／ 大隊 ／ 強）" + string.Join("・", Enumerable.Range(0, Vers.Length).Select(ci => $"{Vers[ci].Name} " + string.Join(" ／ ", new[] { WBoss, WGuard, WBat, WStrong }.Select(w => Per(X(g[bi, ci][w], "dBonusMax"), g[bi, ci][w].N))))));
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-3 の 8: `compare`
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
        var rows = CompareBuilds().Where(r => Has(r.F, "tomo") || Has(r.F, "gan")).ToArray();
        int nw = EnemyCatalog.Stages.Count;
        var grids = Vers.Select(c => CGrid(rows.Select(r => (r.Name, Has(r.F, "tomo") ? I.Swap(r.F, "tomo", c.D) : r.F)).ToArray(), CompareSeeds)).ToArray();
        Console.WriteLine($"# {Tag} §3-3 の 8 `compare` のトモ ／ ガンのいる行 —— {rows.Length} 行 × 列（第1〜5波・seed 0..{CompareSeeds - 1}・トモだけ替える）");
        Console.WriteLine();
        Console.WriteLine("| 行 | トモ | " + string.Join(" | ", Vers.Select(c => c.Name + " 第1〜5波")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Vers.Select(_ => "---|")));
        for (int ri = 0; ri < rows.Length; ri++)
            Console.WriteLine($"| {rows[ri].Name} | {(Has(rows[ri].F, "tomo") ? "○" : "—")} | {string.Join(" | ", grids.Select(g => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w])))))} |");
        Console.WriteLine();
        for (int c = 1; c < Vers.Length; c++)
        {
            int moved = 0, movedNoTomo = 0;
            for (int ri = 0; ri < rows.Length; ri++) for (int w = 0; w < nw; w++) if (grids[c][ri, w] != grids[0][ri, w]) { moved++; if (!Has(rows[ri].F, "tomo")) movedNoTomo++; }
            Console.WriteLine($"- {Vers[c].Name}: 規定と違うセル {moved}（うちトモのいない行 {movedNoTomo}）");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-1 の 4: 芯 3 枚の席の探索（第392期と同じやり方・2枠の全数）
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

    internal static List<(string Free, Formation F, S377.B[] M)> SearchCore(UnitDef tomo, int top)
    {
        var core = new[] { tomo, UnitCatalog.Gan, UnitCatalog.Dolga };
        var heal = S392.HealPool;
        var cands = UnitCatalog.All.Where(d => d.Id is not "tomo" and not "gan" and not "dolga" and not "tsugi" and not "lili").ToArray();
        var pairs = new List<UnitDef[]>();
        for (int i = 0; i < cands.Length; i++) for (int j = i + 1; j < cands.Length; j++) if ((heal.Any(h => h.Id == cands[i].Id) ? 1 : 0) + (heal.Any(h => h.Id == cands[j].Id) ? 1 : 0) <= 1) pairs.Add(new[] { cands[i], cands[j] });
        var cs = pairs.SelectMany(fr => Perms.Select(pm => (Free: fr, F: Board(core.Concat(fr).ToArray(), pm)))).ToArray();
        var s1 = new Score[cs.Length];
        Parallel.For(0, cs.Length, k => s1[k] = Quick(cs[k].F, new[] { I.Boss, I.Guard, I.Bat }, 4));
        var pick = Enumerable.Range(0, cs.Length).GroupBy(k => cs[k].Free[0].Id + "," + cs[k].Free[1].Id).SelectMany(g => Rank(g, k => s1[k]).Take(5)).Select(k => cs[k]).ToArray();
        var s2 = new Score[pick.Length];
        Parallel.For(0, pick.Length, k => s2[k] = Quick(pick[k].F, new[] { I.Boss, I.Guard, I.Bat, I.M5 }, 20));
        var ranked = Rank(Enumerable.Range(0, pick.Length).Select(k => (pick[k], s2[k])), x => x.Item2).GroupBy(x => x.Item1.Free[0].Id + "," + x.Item1.Free[1].Id).Select(g => g.First()).Take(10).ToList();
        var res = ranked.Select(x => (Free: string.Join(" ＋ ", x.Item1.Free.Select(Short)), x.Item1.F, M: M8(x.Item1.F))).ToList();
        return res.OrderByDescending(x => A4(x.M)).ThenBy(x => W4.Average(w => (double)x.M[w].FormDeaths / x.M[w].N)).ThenByDescending(x => x.M[WStrong].Win).Take(top).ToList();
    }

    static void Search(string ver)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var v = Vers.FirstOrDefault(x => x.Name == ver);
        if (v.D is null) { Console.WriteLine($"版が無い: {ver}"); return; }
        var top = SearchCore(v.D, 3);
        Console.WriteLine($"# {Tag} §3-1 の 4 芯 3 枚（トモ {v.Name} ＋ ガン ＋ ドルガ）＋ 2枠の席の探索（第392期 §3-5 と同じやり方・2枠の全数・ヒーラーは1枚まで・ツギ ／ リリ除く）");
        Console.WriteLine();
        Console.WriteLine("## 1. 上位 3 台（段3・seed 0..99）");
        Console.WriteLine();
        Console.WriteLine("| # | 2枠 | 並び（前1 ／ 前3 ／ 中央 ／ 後1 ／ 後3）| " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 |");
        Console.WriteLine("|--:|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|");
        for (int i = 0; i < top.Count; i++) Console.WriteLine($"| {i + 1} | {top[i].Free} | {Seats(top[i].F)} | {string.Join(" | ", top[i].M.Select(x => x.Cell))} | {F1(A4(top[i].M))} |");
        Console.WriteLine();
        Console.WriteLine("比べ: 第392期の芯 3 枚の最良（規定のトモ・ボルグ ＋ ベニ）は全体の 19 位・4 波 100 ／ 強い条件 47.5。");
        Console.WriteLine();
        Console.WriteLine("## 2. 1位の台の札の働き（ボス ／ 近衛 ／ 大隊）と、トモ ／ ドルガを素体に替えた差（8 物差し）");
        Console.WriteLine();
        var f = top[0].F; var m0 = top[0].M;
        foreach (int w in new[] { WBoss, WGuard, WBat })
        {
            var x = m0[w];
            Console.WriteLine($"- {W8[w].Name}: ドルガの力の最大 {Per(X(x, "dBonusMax"), x.N)}・振った 自分 {Per(X(x, "dOwnSwing"), x.N)} ／ トモ {Per(X(x, "dYieldSwing"), x.N)} ／ ガン {Per(X(x, "dGanSwing"), x.N)}（全体 {Per(X(x, "dAll"), x.N)}）・ループ {Per(X(x, "loop"), x.N)}・運んだ力 {Per(X(x, "carried"), x.N)}・+8 {Per(X(x, "rally8"), x.N)}・誰が何Tに倒れたか {x.FellCell}");
        }
        Console.WriteLine();
        Console.WriteLine("| 替えた駒 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 | 強の差 |");
        Console.WriteLine("|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|--:|");
        Console.WriteLine($"| 元 | {string.Join(" | ", m0.Select(x => x.Cell))} | {F1(A4(m0))} | — |");
        foreach (var (slot, d) in f.Occupied().Where(o => o.Def.Id is "tomo" or "dolga" or "gan"))
        {
            var a = M8(Put(f, slot, S392.Plain(d.Id == "tomo" ? UnitCatalog.Tomo : d)));
            Console.WriteLine($"| {Short(d)} → 素体 | {string.Join(" | ", a.Select(x => x.Cell))} | {F1(A4(a))} | {Sg(a[WStrong].Win - m0[WStrong].Win)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Log1(string[] args)
    {
        string bn = args.Length > 3 ? args[3] : "灯×薙ぎ";
        string cn = args.Length > 4 ? args[4] : "T2";
        int wi = args.Length > 5 && int.TryParse(args[5], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 6 && int.TryParse(args[6], out int s0) ? s0 : 0;
        var b = BoardList().FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal));
        if (b.F is null) { Console.WriteLine($"台が無い: {bn}"); return; }
        var c = Cols.FirstOrDefault(x => x.Name == cn); if (c.D is null) c = Cols[0];
        var (r, _, _) = I.Raw(Put(b.F, b.Slot, c.D), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name} × {c.Name} × {W8[wi].Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
