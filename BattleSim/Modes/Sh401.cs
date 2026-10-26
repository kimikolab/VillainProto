using System.Collections.Concurrent;
using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S388 = Sh388Diag;
using S390 = Sh390Diag;

// =====================================================================================
// sh401 —— 第401期【軽い期】本体: ハギ——自分で削って自分で刈る（版 H1 ／ H2 ／ H3 ／ H123 ／ H123L）。規定は変えない。
// 指示書は design/PHASE401_HAGI_CARRY_SPEC.md、報告は design/PHASE401_HAGI_CARRY.md。
//
//     dotnet run --project BattleSim -c Release 0 sh401 p0        # §3-1: トモの灯はハギに付くか ／ 規定のハギの与ダメの内訳 ／ 再行動の前の振り（H2 の前提）
//     dotnet run --project BattleSim -c Release 0 sh401 boards    # §3-2 の 2 ／ 3: 今の止め役の台 3 ／ キャリーの手組み 3 × 列 × 8 物差し（seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh401 search    # §3-2 の 1: ハギ（H123 ／ H123L）＋ 4 枚の席の探索 → 上位 5 台 × 8 物差し
//     dotnet run --project BattleSim -c Release 0 sh401 swap      # §3-2 の 4: 試遊の台 × アタッカー以外の席の最良にハギ（段1 seed 0..9 → 段2 seed 0..49）
//     dotnet run --project BattleSim -c Release 0 sh401 cmp       # §3-2 の 5: ハギのいる `compare` の行 × 版（第1〜5波・seed 0..199）
//     dotnet run --project BattleSim -c Release 0 sh401 log <台> <列> <物差し 0..7> <seed>   # 1戦のログ（台 ＝ 名前の頭）
//     dotnet run --project BattleSim -c Release 0 sh401 check     # 自己検査
//
// **規定の駒は1つも変えない。** 替えた台 ／ 素体は器具の中だけ（試遊の台に足す「版・死神 キャリー」を除く）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p401/predict.md`）。
// H2 は規定と同じ札（報告の頭の訂正）なので、重い測定（search ／ swap ／ cmp）では列に入れない（boards には同じであることを見せるために入れる）。
// =====================================================================================
static partial class Sh401Diag
{
    internal const string PredictFile = ".tmp/p401/predict.md";
    const string Tag = "第401期";
    const int Seeds = 100, CompareSeeds = 200, S1Seeds = 10, SwapSeeds = 50, TT = 9;
    const double Up = 3.0;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "p0" or "boards" or "search" or "swap" or "cmp" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh401: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": Boards(); return;
            case "search": Search(args.Length > 3 ? args[3] : "all"); return;
            case "swap": Swap(); return;
            case "cmp": Cmp(); return;
            case "log": Log1(args); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh401: モードは p0 / boards / search / swap / cmp / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => double.IsNaN(x) ? "—" : x.ToString("#,0");
    static string Sg(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pc(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string SeatName(int s) => s switch { 0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3", _ => $"席{s}" };

    // ---------------------------------------------------------------------------------
    // 版 ／ 列 ／ 波 ／ 台
    // ---------------------------------------------------------------------------------
    internal static readonly (string Name, UnitDef D)[] Vers =
    {
        ("規定", UnitCatalog.Hagi), ("H1", UnitCatalog.HagiCarryH1), ("H2", UnitCatalog.HagiCarryH2), ("H3", UnitCatalog.HagiCarryH3),
        ("H123", UnitCatalog.HagiCarryH123), ("H123L", UnitCatalog.HagiCarryH123L),
    };
    /// <summary>重い測定の版（H2 は規定と同じ札なので外す）。</summary>
    internal static readonly (string Name, UnitDef D)[] HeavyVers = Vers.Where(v => v.Name != "H2").ToArray();
    static readonly (string Name, UnitDef? D)[] StopCols = new (string, UnitDef?)[] { ("元", null), ("素体", S388.PlainHagi) }.Concat(Vers.Select(v => (v.Name, (UnitDef?)v.D))).ToArray();
    static readonly (string Name, UnitDef? D)[] CarryCols = new (string, UnitDef?)[] { ("素体", S388.PlainHagi) }.Concat(Vers.Select(v => (v.Name, (UnitDef?)v.D))).ToArray();

    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;   // ボス ／ 近衛 ／ 大隊 ／ 本編 第2 ／ 4 ／ 5波 ／ 九体 ／ 強 第2〜5波
    static readonly int[] W4 = { 0, 1, 2, 5 };
    static readonly int[] W5 = { 0, 1, 2, 5, 7 };
    const int WBoss = 0, WGuard = 1, WBat = 2, WStrong = 7;

    /// <summary>§3-2 の 3: 今の止め役の台（元 ＝ ハギを入れる前）。</summary>
    internal static S388.Board[] StopBoards() => new[]
    {
        new S388.Board("試遊・ハギ 台B", Presets.BoardBVio375(), 3, "カド"),
        new S388.Board("試遊・死神と墓守", Presets.DeathChain377(), 0, "ゾト"),
        new S388.Board("版・移動 ハギ", CompareBuilds().First(r => r.Name == S390.MoveRows[0]).F, 1, "ガルド"),
    };

    /// <summary>
    /// §3-2 の 2: キャリーの手組み（ハギに力を集める駒を並べた台・規定のハギを置いた並び）。
    /// 灯と号令: ハギが「自分の手番で攻撃する最も遅い味方」になるよう、ほかは速い駒か殴らない駒（ソラ 8 ／ バン ／ ガン 9）。
    /// 移り木: 前1 のハギが傷つくとシオが後ろへ下げて癒し、攻撃力を積む（溢れた癒しも攻撃力に）。
    /// 哀歌: ハギが攻撃力のいちばん高い味方になるよう、ほかは素の攻撃力の低い支え（ソラ ／ ガルド ／ ヒサ）。
    /// </summary>
    internal static S388.Board[] CarryBoards() => new[]
    {
        new S388.Board("キャリー・灯と号令", Formation.Build(front1: UnitCatalog.Sora, front3: UnitCatalog.Ban, center: UnitCatalog.Gan, back1: UnitCatalog.Hagi, back3: UnitCatalog.Tomo), 3, "ハギ"),
        new S388.Board("キャリー・移り木", Formation.Build(front1: UnitCatalog.Hagi, front3: UnitCatalog.Gald, center: UnitCatalog.Shio, back1: UnitCatalog.Hisa, back3: UnitCatalog.Gan), 0, "ハギ"),
        new S388.Board("キャリー・哀歌", Formation.Build(front1: UnitCatalog.Sora, front3: UnitCatalog.Gald, center: UnitCatalog.Ere, back1: UnitCatalog.Hagi, back3: UnitCatalog.Hisa), 3, "ハギ"),
    };

    // ---- §5 試遊の台（「版・死神 キャリー」・§3-2 の 1〜2 でいちばん良かった版と台）----
    internal const string PlayName = "版・死神 キャリー";
    internal static UnitDef PlayVersion => UnitCatalog.HagiCarryH123;
    internal const string PlayVersionName = "H123";
    internal static Formation PlayBoard() => Presets.HagiCarry401();

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }
    static int[] Seats5(Formation f) => f.Occupied().Select(o => o.Slot).Where(s => s < 5).OrderBy(s => s).ToArray();
    static Formation ColBoard(S388.Board b, UnitDef? d) => d is null ? b.F : Put(b.F, b.Slot, d);
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
    // 1戦の計数（`Sh377Diag.One` の `extra`）
    // ---------------------------------------------------------------------------------
    static void Add(S377.B b, string k, long v) { if (v != 0) b.X[k] = b.X.GetValueOrDefault(k) + v; }
    static long X(S377.B b, string k) => b.X.GetValueOrDefault(k);
    /// <summary>最大の値（`B.X` は足し合わせるので、最大は別に持つ）。鍵は「測った場所の名前 ＋ 量の名前」。</summary>
    static readonly ConcurrentDictionary<string, long> Max = new();
    static void NoteMax(string k, long v) => Max.AddOrUpdate(k, v, (_, o) => Math.Max(o, v));
    static long Mx(string k) => Max.GetValueOrDefault(k);
    internal static readonly string[] KindNames = { "種類の外", "斬撃", "薙ぎ", "追撃", "手番の刈り取り", "追撃の刈り取り" };

    /// <summary>ハギの帳簿（与ダメの内訳 ／ 足し分 ／ 再行動 ／ 最初の撃破 ／ 魂）／ 隊の与ダメ ／ 勇者（止め ／ ハギが1ターンに勇者へ与えた量）／ トモの灯。</summary>
    static Action<BattleResult, List<UnitState>, List<UnitState>, S377.B> Extra(bool boss, string label) => (r, p, e, b) =>
    {
        var form = p.ToDictionary(u => u.InstanceId);
        var foe = e.ToDictionary(u => u.InstanceId);
        var sumTeam = new Dictionary<int, int>();
        var hagi = p.FirstOrDefault(u => u.Def.Id == "hagi");
        int hid = hagi?.InstanceId ?? -9;
        int rid = p.FirstOrDefault(u => u.Def.Id == "rica")?.InstanceId ?? -9;
        var dmgT = new long[BattleEngine.MaxTurns + 2];
        var heroT = new long[BattleEngine.MaxTurns + 2];
        bool hAlive = hagi is not null;
        UnitState? hero = boss && e.Count > 0 ? e[0] : null;
        long team = 0, mine = 0;
        bool Foe(int id) => foe.ContainsKey(id) || (sumTeam.TryGetValue(id, out int st) && st == BattleContext.EnemyTeam);
        foreach (var x in r.Events)
        {
            int t = Math.Clamp(x.Turn, 0, BattleEngine.MaxTurns + 1);
            int tgt = x.TargetId ?? -3, act = x.ActorId ?? -2;
            switch (x.Kind)
            {
                case BattleEventKind.Summon: sumTeam[tgt] = x.Team ?? -1; break;
                case BattleEventKind.TurnStart:
                    if (hagi is not null && hAlive && t is >= 1 and < TT) Add(b, $"halive{t}", 1);
                    break;
                case BattleEventKind.Damage:
                {
                    bool toFoe = Foe(tgt);
                    bool byUs = form.ContainsKey(act) || (sumTeam.TryGetValue(act, out int sa) && sa == BattleContext.PlayerTeam);
                    if (toFoe && byUs) { dmgT[t] += x.Amount; if (act == hid) mine += x.Amount; }
                    if (toFoe && !Foe(act)) team += x.Amount;   // 隊の与ダメ: 敵に入った全量（出どころの無い刻み ＝ 燃焼 ／ 毒なども含む・敵どうしの分は除く）
                    if (hero is not null && tgt == hero.InstanceId)
                    {
                        if (act == hid) heroT[t] += x.Amount;
                        Add(b, act == rid ? "hdRica" : act == hid ? "hdHagi" : "hdOther", x.Amount);
                    }
                    break;
                }
                case BattleEventKind.Revive:
                    if (tgt == hid) hAlive = true;
                    break;
                case BattleEventKind.Lamp:
                    if (x.Text == LampLabels.Light) Add(b, tgt == hid ? "lampHagi" : "lampOther", 1);
                    if (x.Text == LampLabels.Yield && tgt == hid) Add(b, "yieldHagi", 1);
                    break;
                case BattleEventKind.Whet:
                    if (tgt == hid) Add(b, "whetHagi", x.Amount);
                    break;
                case BattleEventKind.Death:
                    if (tgt == hid) hAlive = false;
                    if (Foe(tgt)) { Add(b, "kAll", 1); Add(b, act == rid ? "kRica" : act == hid ? "kHagi" : "kOther", 1); }
                    if (hero is not null && tgt == hero.InstanceId)
                    {
                        Add(b, act == rid ? "heroKRica" : act == hid ? "heroKHagi" : "heroKOther", 1);
                        bool reaped = r.Log.Any(l => l.Text.Contains($"の大鎌が {hero.Name} を刈り取った", StringComparison.Ordinal));
                        if (reaped && act == hid) Add(b, "heroReaped", 1);
                    }
                    break;
            }
        }
        Add(b, "maxTurnDmg", dmgT.Max());
        Add(b, "teamDmg", team); Add(b, "hagiEv", mine);
        if (hero is not null && hagi is not null)
        {
            long hm = heroT.Max();
            Add(b, "heroHagiTurn", hm); if (hm > 1200) Add(b, "heroHagiOver", 1);
            NoteMax(label + ":heroHagiTurn", hm);
        }
        if (r.TallyByUnit.TryGetValue("hagi", out var h) && hagi is not null)
        {
            Add(b, "strikes", h.R388Strikes); Add(b, "sweeps", h.R389Sweeps); Add(b, "reaps", h.R388Reaps); Add(b, "encores", h.R388Encores);
            Add(b, "encR", h.R401EncoreReap); Add(b, "encS", h.R401EncoreStrike);
            Add(b, "pursuits", h.R388Pursuits); Add(b, "chase", h.R391ChaseReaps);
            Add(b, "dS", h.R401DmgStrike); Add(b, "dW", h.R401DmgSweep); Add(b, "dP", h.R401DmgPursuit); Add(b, "dR", h.R401DmgReap); Add(b, "dC", h.R401DmgChase); Add(b, "dAll", h.DamageToEnemy);
            Add(b, "markAdd", h.R401MarkAdd); Add(b, "markHits", h.R401MarkHits); Add(b, "mulAdd", h.R401MulAdd); Add(b, "mulHits", h.R401MulHits);
            Add(b, "reapKills", h.R390ReapKills); Add(b, "chaseKills", h.R391ChaseReapKills); Add(b, "kills", h.Kills);
            Add(b, "marks", h.R389MarksPlaced);
            if (h.R389Soul3Turn > 0) { Add(b, "s3", 1); Add(b, "s3T", h.R389Soul3Turn); }
            if (h.R389Soul6Turn > 0) { Add(b, "s6", 1); Add(b, "s6T", h.R389Soul6Turn); }
            if (h.R389Soul9Turn > 0) { Add(b, "s9", 1); Add(b, "s9T", h.R389Soul9Turn); }
            if (h.R389SoulByTurn is { } sb) for (int t = 1; t < TT && t < sb.Length; t++) Add(b, $"soul{t}", sb[t]);
            if (h.R401FirstKillTurn > 0)
            {
                Add(b, "fk", 1); Add(b, "fkT", h.R401FirstKillTurn); Add(b, $"fkK{h.R401FirstKillKind}", 1); Add(b, "fkSoul", h.R401FirstKillSoul); Add(b, "fkMarks", h.R401FirstKillMarks);
                Add(b, $"fkT{Math.Min(h.R401FirstKillTurn, 6)}", 1);
            }
            Add(b, "hatkEnd", hagi.CurrentAttack);
            NoteMax(label + ":hitMax", h.R401HitMax);
            NoteMax(label + ":mulAtkMax", h.R401MulAtkMax);
            if (h.R401HitMax >= 1000) Add(b, "hit1000", 1);
        }
    };

    static S377.B[] M8(Formation f, string label)
    {
        var g = new S377.B[W8.Length];
        Parallel.For(0, W8.Length, wi => g[wi] = S377.Measure(f, W8[wi].Ws, Seeds, Extra(wi == WBoss, $"{label}|{wi}")));
        return g;
    }
    static double D4(S377.B[] a, S377.B[] b) => W4.Average(wi => a[wi].Win - b[wi].Win);
    static double A4(S377.B[] a) => W4.Average(wi => a[wi].Win);
    static double Dead4(S377.B[] x) => W4.Average(wi => (double)x[wi].FormDeaths / x[wi].N);
    static bool HagiCol((string Name, UnitDef? D) c) => c.D is not null && c.D.Id == "hagi" && c.D != S388.PlainHagi;

    // ---------------------------------------------------------------------------------
    // §3-1
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §3-1 まず確かめること（規定のハギ・seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("## 1. トモの灯はハギに付くか");
        Console.WriteLine();
        bool atk = TaillightTrait.AttacksInOwnTurn(UnitCatalog.Hagi);
        var never = UnitCatalog.Hagi.Traits.Where(t => TraitCatalog.Get(t).NeverAttacksOwnTurn).ToList();
        Console.WriteLine($"- 規定のハギの札: {string.Join("・", UnitCatalog.Hagi.Traits)}。`NeverAttacksOwnTurn` の札: {(never.Count == 0 ? "無し" : string.Join("・", never))}。旧の追い打ち `Pursuer` は {(UnitCatalog.Hagi.Traits.Contains(TraitId.Pursuer) ? "**残っている**" : "残っていない")}。");
        Console.WriteLine($"- `TaillightTrait.AttacksInOwnTurn(規定のハギ)` ＝ {atk}（偽なら W1 で飛ばされる）。旧（第389期までの `HagiH389`・追い打ち）＝ {TaillightTrait.AttacksInOwnTurn(UnitCatalog.HagiH389)}。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 物差し | 灯がハギに（1戦）| 灯がほかの味方に | 譲られた手番（ハギ）| ハギに積まれた力（1戦）|");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        var lampBoards = CarryBoards().Take(1).Select(b => (b.Name, b.F)).Concat(Presets.Versions.Concat(Presets.Playtest).Where(r => Has(r.F, "tomo") && Has(r.F, "hagi")).Select(r => (r.Name, r.F))).DistinctBy(x => x.Name).ToList();
        foreach (var (name, f) in lampBoards)
            foreach (int w in new[] { WBoss, WGuard, WBat })
            {
                var x = S377.Measure(f, W8[w].Ws, 20, Extra(w == WBoss, "p0lamp"));
                Console.WriteLine($"| {name}（{S307.Seats(f)}）| {W8[w].Name} | {Per(X(x, "lampHagi"), x.N)} | {Per(X(x, "lampOther"), x.N)} | {Per(X(x, "yieldHagi"), x.N)} | {Per(X(x, "whetHagi"), x.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine("## 2. 規定のハギの1戦の与ダメの内訳（敵に入った量・1戦の平均）／ 再行動の前の振り（H2 の前提）");
        Console.WriteLine();
        var boards = StopBoards().Take(2).ToArray();
        Console.WriteLine("| 台 | 物差し | 勝率 | ハギの与ダメ | 斬撃 | 薙ぎ | 追撃 | 手番の刈り取り | 追撃の刈り取り | ほか | 隊に占める | 再行動（刈り取りの後 ／ ふつうの斬撃 ・ 薙ぎの後）|");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var b in boards)
            foreach (int w in new[] { WBoss, WGuard, WBat })
            {
                var x = S377.Measure(ColBoard(b, UnitCatalog.Hagi), W8[w].Ws, Seeds, Extra(w == WBoss, "p0dmg"));
                Console.WriteLine($"| {b.Name} | {W8[w].Name} | {F1(x.Win)} | {Per(X(x, "dAll"), x.N)} | {DmgCells(x)} | {Pc(X(x, "hagiEv"), X(x, "teamDmg"))}% | {Per(X(x, "encR"), x.N)} ／ {Per(X(x, "encS"), x.N)} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static string DmgCells(S377.B x)
    {
        long all = X(x, "dAll"), parts = X(x, "dS") + X(x, "dW") + X(x, "dP") + X(x, "dR") + X(x, "dC");
        string C(long v) => $"{Per(v, x.N)}（{Pc(v, all)}%）";
        return $"{C(X(x, "dS"))} | {C(X(x, "dW"))} | {C(X(x, "dP"))} | {C(X(x, "dR"))} | {C(X(x, "dC"))} | {C(all - parts)}";
    }

    // ---------------------------------------------------------------------------------
    // §3-2 の 2 ／ 3: 台 × 列 × 8 物差し
    // ---------------------------------------------------------------------------------
    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §3-2 の 2 ／ 3 台 × 列 × 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        var stop = StopBoards(); var carry = CarryBoards();
        Console.WriteLine("台: " + string.Join("・", stop.Select(b => $"{b.Name}（{S307.Seats(b.F)}・{SeatName(b.Slot)} の {b.Who} の席にハギ・元 ＝ ハギを入れる前）")) + "。");
        Console.WriteLine("キャリーの手組み: " + string.Join("・", carry.Select(b => $"{b.Name}（{S307.Seats(b.F)}・ハギは {SeatName(b.Slot)}）")) + "。");
        Console.WriteLine();
        Section("A. 今の止め役の台（§3-2 の 3）", stop, StopCols, origin: 0);
        Section("B. キャリーの手組み（§3-2 の 2・差は規定との差）", carry, CarryCols, origin: 1);
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Section(string title, S388.Board[] boards, (string Name, UnitDef? D)[] cols, int origin)
    {
        var g = new S377.B[boards.Length, cols.Length][];
        for (int bi = 0; bi < boards.Length; bi++) for (int ci = 0; ci < cols.Length; ci++) g[bi, ci] = M8(ColBoard(boards[bi], cols[ci].D), $"{boards[bi].Name}|{cols[ci].Name}");
        Console.WriteLine($"## {title}");
        Console.WriteLine();
        Report(boards.Select(b => b.Name).ToArray(), cols, g, origin);
    }

    static void Report(string[] names, (string Name, UnitDef? D)[] cols, S377.B[,][] g, int origin, string[]? labels = null)
    {
        int nb = names.Length, nc = cols.Length;
        string Lab(int bi, int ci) => labels is null ? $"{names[bi]}|{cols[ci].Name}" : labels[bi * nc + ci];
        var hc = Enumerable.Range(0, nc).Where(ci => HagiCol(cols[ci])).ToArray();
        Console.WriteLine("### 1. 勝率・決着T（勝った戦）・倒れた編成の駒（1戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波の平均 | 差 |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                Console.WriteLine($"| {(ci == 0 ? names[bi] : "")} | {cols[ci].Name} | {string.Join(" | ", g[bi, ci].Select(x => x.Cell))} | {F1(A4(g[bi, ci]))} | {(ci == origin || origin < 0 ? "—" : Sg(D4(g[bi, ci], g[bi, origin])))} |");
        Console.WriteLine();
        Console.WriteLine("### 1'. ボスの倒しT ／ 隊の1ターンの最大の与ダメ（1戦の平均）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | ボスの倒しT | " + string.Join(" | ", W5.Select(w => W8[w].Name)) + " |");
        Console.WriteLine("|---|---|--:|" + string.Concat(W5.Select(_ => "--:|")));
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                Console.WriteLine($"| {(ci == 0 ? names[bi] : "")} | {cols[ci].Name} | {g[bi, ci][WBoss].WinTs} | {string.Join(" | ", W5.Select(w => F0((double)X(g[bi, ci][w], "maxTurnDmg") / Math.Max(1, g[bi, ci][w].N))))} |");
        Console.WriteLine();

        Console.WriteLine("### 2. ハギの与ダメの内訳（敵に入った量・1戦）と隊に占める割合（分母は敵に入った全量——出どころの無い燃焼 ／ 毒の刻みも含む） ／ H1 の印の足し分 ／ H3 の倍率で増えた分（どちらも守りを通る前の名目・1戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | ハギの与ダメ | 斬撃 | 薙ぎ | 追撃 | 手番の刈り取り | 追撃の刈り取り | ほか | 隊に占める | 印の足し分（回）| 倍率で増えた分（回）|");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|---|---|");
        for (int bi = 0; bi < nb; bi++)
            foreach (int ci in hc)
                foreach (int w in W5)
                {
                    var x = g[bi, ci][w];
                    Console.WriteLine($"| {(ci == hc[0] && w == 0 ? names[bi] : "")} | {(w == 0 ? cols[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "dAll"), x.N)} | {DmgCells(x)} | {Pc(X(x, "hagiEv"), X(x, "teamDmg"))}% | {Per(X(x, "markAdd"), x.N)}（{Per(X(x, "markHits"), x.N)}）| {Per(X(x, "mulAdd"), x.N)}（{Per(X(x, "mulHits"), x.N)}）|");
                }
        Console.WriteLine();

        Console.WriteLine("### 3. 最初の一体（ハギが最初に敵を倒した戦の割合 ／ T の平均 ／ T の分布 T1・T2・T3・T4・T5・T6+ ／ 一撃の種類 ／ そのときの魂 ／ 倒れた敵の印）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 倒した戦 | T | T1・T2・T3・T4・T5・T6+（%）| 種類（斬撃 ／ 薙ぎ ／ 追撃 ／ 刈り取り ／ 追撃の刈り取り ／ ほか %）| 魂 | 印 |");
        Console.WriteLine("|---|---|---|--:|--:|---|---|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            foreach (int ci in hc)
                foreach (int w in new[] { WBoss, WGuard, WBat, 5 })
                {
                    var x = g[bi, ci][w]; long n = X(x, "fk");
                    string dist = string.Join("・", Enumerable.Range(1, 6).Select(t => Pc(X(x, $"fkT{t}"), x.N)));
                    string kinds = string.Join(" ／ ", new[] { 1, 2, 3, 4, 5, 0 }.Select(k => Pc(X(x, $"fkK{k}"), n)));
                    Console.WriteLine($"| {(ci == hc[0] && w == 0 ? names[bi] : "")} | {(w == 0 ? cols[ci].Name : "")} | {W8[w].Name} | {Pc(n, x.N)}% | {Per(X(x, "fkT"), n)} | {dist} | {kinds} | {Per(X(x, "fkSoul"), n)} | {Per(X(x, "fkMarks"), n)} |");
                }
        Console.WriteLine();

        Console.WriteLine("### 4. 魂（ターンの頭の魂 T1〜T8・生きていた戦の平均）・段 3 ／ 6 ／ 9 に届いた戦 %（T）・再行動（刈り取りの後 ／ ふつうの斬撃 ・ 薙ぎの後）・付けた印");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | T1〜T8 | 3 | 6 | 9 | 再行動 | 印 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|--:|");
        for (int bi = 0; bi < nb; bi++)
            foreach (int ci in hc)
                foreach (int w in new[] { WBoss, WGuard, WBat })
                {
                    var x = g[bi, ci][w];
                    string tl = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"halive{t}") == 0 ? "—" : F1((double)X(x, $"soul{t}") / X(x, $"halive{t}"))));
                    string St(string k) => $"{Pc(X(x, k), x.N)}（T{Per(X(x, k + "T"), X(x, k))}）";
                    Console.WriteLine($"| {(w == 0 && ci == hc[0] ? names[bi] : "")} | {(w == 0 ? cols[ci].Name : "")} | {W8[w].Name} | {tl} | {St("s3")} | {St("s6")} | {St("s9")} | {Per(X(x, "encR"), x.N)} ／ {Per(X(x, "encS"), x.N)} | {Per(X(x, "marks"), x.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("### 5. 勇者（ボス）: 止めを刺したのは誰か（勇者だけで数える）・刈り取りで止めた割合 ／ ハギが1ターンに勇者へ与えた量（1戦の最大の平均 ／ 全戦の最大 ／ 1,200 を超えたターンがあった戦）／ 勇者へのダメージの内訳");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 勝率 | 勇者の止め（リィカ ／ ハギ ／ ほか %）| ハギが刈り取りで止めた | ハギ → 勇者 1ターンの最大（平均 ／ 最大）| 1,200 超え | 勇者へのダメージ（リィカ ／ ハギ ／ ほか %）|");
        Console.WriteLine("|---|---|--:|---|--:|---|--:|---|");
        for (int bi = 0; bi < nb; bi++)
            foreach (int ci in hc)
            {
                var x = g[bi, ci][WBoss];
                long hn = X(x, "heroKRica") + X(x, "heroKHagi") + X(x, "heroKOther"), hs = X(x, "hdRica") + X(x, "hdHagi") + X(x, "hdOther");
                Console.WriteLine($"| {(ci == hc[0] ? names[bi] : "")} | {cols[ci].Name} | {F1(x.Win)} | {Pc(X(x, "heroKRica"), hn)} ／ {Pc(X(x, "heroKHagi"), hn)} ／ {Pc(X(x, "heroKOther"), hn)} | {Pc(X(x, "heroReaped"), hn)}% | {Per(X(x, "heroHagiTurn"), x.N)} ／ {F0(Mx(Lab(bi, ci) + "|0:heroHagiTurn"))} | {Pc(X(x, "heroHagiOver"), x.N)}% | {Pc(X(x, "hdRica"), hs)} ／ {Pc(X(x, "hdHagi"), hs)} ／ {Pc(X(x, "hdOther"), hs)} |");
            }
        Console.WriteLine();

        Console.WriteLine("### 6. 誰が何Tに倒れたか（規定 ／ 4 波の平均がいちばん良い版・ボス ／ 近衛 ／ 大隊）");
        Console.WriteLine();
        int def = Array.FindIndex(cols, c => c.Name == "規定");
        if (def < 0) def = hc[0];
        for (int bi = 0; bi < nb; bi++)
        {
            int best = hc.OrderByDescending(ci => A4(g[bi, ci])).ThenByDescending(ci => g[bi, ci][WStrong].Win).ThenBy(ci => ci).First();
            Console.WriteLine($"- **{names[bi]}**（最良の版 {cols[best].Name}・4 波 {F1(A4(g[bi, best]))} ／ {cols[def].Name} {F1(A4(g[bi, def]))}）");
            foreach (int w in new[] { WBoss, WGuard, WBat })
            {
                Console.WriteLine($"  - {W8[w].Name}: {cols[def].Name} {F1(g[bi, def][w].Win)}%・{g[bi, def][w].WinTs}T — {g[bi, def][w].FellCell}");
                if (best != def) Console.WriteLine($"  - {W8[w].Name}: {cols[best].Name} {F1(g[bi, best][w].Win)}%・{g[bi, best][w].WinTs}T — {g[bi, best][w].FellCell}");
            }
        }
        Console.WriteLine();

        Console.WriteLine("### 7. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上（差の列と比べて）／ あふれ）と、ハギの一撃の最大 ／ 倍率を掛けた攻撃力の最大 ／ 1,000 以上の一撃があった戦");
        Console.WriteLine();
        var signs = new List<string>();
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
            {
                if (ci == origin) continue;
                string at = $"{names[bi]} × {cols[ci].Name}";
                foreach (int w in new[] { WGuard, WBat }) { var x = g[bi, ci][w]; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{at} × {W8[w].Name}: 1T 決着 {Pc(x.Win1T, x.N)}%"); }
                { var x = g[bi, ci][WBoss]; if (x.BossFast > 0) signs.Add($"{at} × ボス: 4T 未満の勝ち {Pc(x.BossFast, x.N)}%"); }
                for (int w = 0; w < W8.Length; w++)
                {
                    var x = g[bi, ci][w]; var x0 = origin < 0 ? null : g[bi, origin][w];
                    if (x0 is not null && 100.0 * x.Cap / x.N - 100.0 * x0.Cap / x0.N >= 5) signs.Add($"{at} × {W8[w].Name}: 30T の打ち切り {Pc(x0.Cap, x0.N)} → {Pc(x.Cap, x.N)}%");
                    if (x.Overflow > 0) signs.Add($"{at} × {W8[w].Name}: あふれの兆し {x.Overflow} 件");
                }
            }
        if (signs.Count == 0) Console.WriteLine("- 無し。");
        foreach (var s in signs) Console.WriteLine("- " + s);
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 一撃の最大（ボス ／ 近衛 ／ 大隊 ／ 強）| 倍率を掛けた攻撃力の最大（同じ）| 終わりの攻撃力（1戦の平均・同じ）| 1,000 以上の一撃があった戦（近衛 ／ 大隊 %）|");
        Console.WriteLine("|---|---|---|---|---|---|");
        for (int bi = 0; bi < nb; bi++)
            foreach (int ci in hc)
            {
                string M(string k) => string.Join(" ／ ", new[] { WBoss, WGuard, WBat, WStrong }.Select(w => F0(Mx($"{Lab(bi, ci)}|{w}:{k}"))));
                Console.WriteLine($"| {(ci == hc[0] ? names[bi] : "")} | {cols[ci].Name} | {M("hitMax")} | {M("mulAtkMax")} | {string.Join(" ／ ", new[] { WBoss, WGuard, WBat, WStrong }.Select(w => Per(X(g[bi, ci][w], "hatkEnd"), g[bi, ci][w].N)))} | {Pc(X(g[bi, ci][WGuard], "hit1000"), g[bi, ci][WGuard].N)} ／ {Pc(X(g[bi, ci][WBat], "hit1000"), g[bi, ci][WBat].N)} |");
            }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §3-2 の 1: ハギ ＋ 4 枚の席の探索（第392期の作りを縮めた）
    // ---------------------------------------------------------------------------------
    static UnitDef[]? _heal;
    static UnitDef[] HealPool => _heal ??= Boss283Diag.HealPoolNow;
    static bool IsHealer(UnitDef d) => HealPool.Any(h => h.Id == d.Id);
    /// <summary>残りの枠の候補: ロスター 52 枚からハギとツギ ／ リリを除いた駒。ヒーラー（`Boss283Diag.HealPoolNow`）は残りの枠に1枚まで。</summary>
    internal static UnitDef[] Cands() => UnitCatalog.All.Where(d => d.Id is not "hagi" and not "tsugi" and not "lili").ToArray();
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
        double win = 0; long deaths = 0, n = 0;
        foreach (var w in ws)
        {
            int k = 0;
            for (int s = 0; s < seeds; s++)
            {
                var (r, p, _) = I.Raw(f, w, s, census: false);
                if (r.PlayerWon) k++;
                deaths += p.Count(u => u.LastDeathTurn > 0); n++;
            }
            win += 100.0 * k / seeds;
        }
        return new Score(win / ws.Length, (double)deaths / n);
    }
    static readonly I.W[] SW1 = { I.Boss, I.Guard, I.Bat };
    static readonly I.W[] SW2 = { I.Boss, I.Guard, I.Bat, I.M5 };
    const int Pool = 16, S0Combos = 4000, S0Perms = 8, S0Seeds = 2, SS1Seeds = 4, S2Perms = 5, S2Seeds = 20, S3Top = 10;
    static List<UnitDef[]> Combos(UnitDef[] pool, int k)
    {
        var res = new List<UnitDef[]>();
        void Rec(int start, List<UnitDef> cur)
        {
            if (cur.Count == k) { if (cur.Count(IsHealer) <= 1) res.Add(cur.ToArray()); return; }
            for (int i = start; i < pool.Length; i++) { cur.Add(pool[i]); Rec(i + 1, cur); cur.RemoveAt(cur.Count - 1); }
        }
        Rec(0, new List<UnitDef>());
        return res;
    }

    /// <summary>
    /// 段0: ロスターから無作為の 4 枚の組（ヒーラー 1 枚まで・`Random(401)`）× 無作為の並び 8 × ボス ／ 近衛 ／ 大隊 × seed 0..1 → 組ごとに並びの最良 → 駒ごとに、その駒を含む組の平均。上位 16 枚を候補にする。
    /// 段1: 候補 16 枚の 4 枚の組の全数 × 120 並び × ボス ／ 近衛 ／ 大隊 × seed 0..3 → 段2: 組ごとに段1 の上位 5 並び × 4 波 × seed 0..19 → 段3: 段2 の上位 10 組（組ごとに最良の並び）× 8 物差し × seed 0..99。
    /// </summary>
    static List<(Formation F, string Free, S377.B[] M)> SearchOne(UnitDef hagi, string vname, List<string> notes)
    {
        var cands = Cands();
        var rng = new Random(401);
        var s0 = new List<(UnitDef[] Free, Formation[] Fs)>();
        var seen = new HashSet<string>();
        while (s0.Count < S0Combos)
        {
            var pick = cands.OrderBy(_ => rng.Next()).Take(4).ToArray();
            if (pick.Count(IsHealer) > 1) continue;
            string key = string.Join(",", pick.Select(d => d.Id).OrderBy(x => x));
            if (!seen.Add(key)) continue;
            var five = new[] { hagi }.Concat(pick).ToArray();
            s0.Add((pick, Enumerable.Range(0, S0Perms).Select(_ => Board(five, Perms[rng.Next(Perms.Length)])).ToArray()));
        }
        var s0Score = new double[s0.Count];
        Parallel.For(0, s0.Count, k => s0Score[k] = s0[k].Fs.Max(f => Quick(f, SW1, S0Seeds).Mean));
        var marg = cands.Select(d => (D: d, M: Enumerable.Range(0, s0.Count).Where(k => s0[k].Free.Contains(d)).Select(k => s0Score[k]).DefaultIfEmpty(0).Average()))
            .OrderByDescending(x => x.M).ThenBy(x => Array.IndexOf(cands, x.D)).ToList();
        var pool = marg.Take(Pool).Select(x => x.D).ToArray();
        notes.Add($"{vname} 段0（{s0.Count} 組 × {S0Perms} 並び）の上位 {Pool} 枚: {string.Join("・", marg.Take(Pool).Select(x => $"{Short(x.D)} {F1(x.M)}"))}（17 位以下: {string.Join("・", marg.Skip(Pool).Take(6).Select(x => $"{Short(x.D)} {F1(x.M)}"))} …）");
        var combos = Combos(pool, 4);
        var all = combos.SelectMany(fr => Perms.Select(pm => (Free: fr, F: Board(new[] { hagi }.Concat(fr).ToArray(), pm)))).ToArray();
        var s1 = new Score[all.Length];
        Parallel.For(0, all.Length, k => s1[k] = Quick(all[k].F, SW1, SS1Seeds));
        var pick2 = Enumerable.Range(0, all.Length).GroupBy(k => k / Perms.Length)
            .SelectMany(gr => gr.OrderByDescending(k => s1[k].Mean).ThenBy(k => s1[k].Deaths).Take(S2Perms)).ToArray();
        var s2 = new Score[pick2.Length];
        Parallel.For(0, pick2.Length, k => s2[k] = Quick(all[pick2[k]].F, SW2, S2Seeds));
        var top = Enumerable.Range(0, pick2.Length).OrderByDescending(k => s2[k].Mean).ThenBy(k => s2[k].Deaths)
            .GroupBy(k => pick2[k] / Perms.Length).Select(gr => gr.First()).Take(S3Top).Select(k => all[pick2[k]]).ToList();
        notes.Add($"{vname} 段1: 組 {combos.Count} × 120 並び ＝ {all.Length:#,0} 台・段2: {pick2.Length:#,0} 台");
        var res = new List<(Formation, string, S377.B[])>();
        foreach (var t in top) res.Add((t.F, string.Join(" ＋ ", t.Free.Select(Short)), M8(t.F, $"search:{vname}:{S307.Seats(t.F)}")));
        return res.OrderByDescending(x => A4(x.Item3)).ThenBy(x => Dead4(x.Item3)).ThenByDescending(x => x.Item3[WStrong].Win).ToList();
    }

    static void Search(string which)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §3-2 の 1 ハイパーキャリーの探索 —— ハギ（版）＋ 4 枚（ツギ ／ リリは除く・ヒーラーは 1 枚まで）");
        Console.WriteLine();
        Console.WriteLine($"ヒーラー（`Boss283Diag.HealPoolNow`）: {string.Join("・", HealPool.Select(Short))}。段0: 無作為の組 {S0Combos} × 並び {S0Perms} × ボス ／ 近衛 ／ 大隊 × seed 0..{S0Seeds - 1} → 駒ごとの平均の上位 {Pool} 枚。段1: その {Pool} 枚の 4 枚の組 × 120 並び × ボス ／ 近衛 ／ 大隊 × seed 0..{SS1Seeds - 1}。段2: 組ごとに上位 {S2Perms} 並び × 4 波 × seed 0..{S2Seeds - 1}。段3: 上位 {S3Top} 組 × 8 物差し × seed 0..{Seeds - 1}。並べる順: 4 波（ボス ／ 近衛 ／ 大隊 ／ 本編 第5波）の平均 → 倒れた編成の駒の少ない方 → 強い条件。");
        Console.WriteLine();
        var vers = HeavyVers.Where(v => v.Name is "H123" or "H123L").Where(v => which == "all" || v.Name == which).ToArray();
        foreach (var v in vers)
        {
            var notes = new List<string>();
            var res = SearchOne(v.D, v.Name, notes);
            Console.WriteLine($"## {v.Name}");
            Console.WriteLine();
            foreach (var n in notes) Console.WriteLine("- " + n);
            Console.WriteLine();
            Console.WriteLine($"### 上位 5 台（段3・seed 0..{Seeds - 1}）と、同じ並びで ハギ ＝ 規定 ／ 素体");
            Console.WriteLine();
            Console.WriteLine("| # | 残りの枠 | 並び（前1 ／ 前3 ／ 中央 ／ 後1 ／ 後3）| 列 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 | ハギの与ダメの割合（ボス ／ 近衛 ／ 大隊 ／ 第5波 ／ 強）| ボスの倒しT |");
            Console.WriteLine("|--:|---|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|---|--:|");
            var top5 = res.Take(5).ToList();
            var cmpCols = new (string Name, UnitDef? D)[] { (v.Name, v.D), ("規定", UnitCatalog.Hagi), ("素体", S388.PlainHagi) };
            var grid = new S377.B[top5.Count, cmpCols.Length][];
            var labels = new string[top5.Count * cmpCols.Length];
            for (int i = 0; i < top5.Count; i++)
            {
                int hs = top5[i].F.Occupied().First(o => o.Def.Id == "hagi").Slot;
                for (int c = 0; c < cmpCols.Length; c++)
                {
                    labels[i * cmpCols.Length + c] = c == 0 ? $"search:{v.Name}:{S307.Seats(top5[i].F)}" : $"search:{v.Name}:{S307.Seats(top5[i].F)}|{cmpCols[c].Name}";
                    grid[i, c] = c == 0 ? top5[i].M : M8(Put(top5[i].F, hs, cmpCols[c].D!), labels[i * cmpCols.Length + c]);
                }
            }
            for (int i = 0; i < top5.Count; i++)
                for (int c = 0; c < cmpCols.Length; c++)
                {
                    var m = grid[i, c];
                    string share = string.Join(" ／ ", W5.Select(w => Pc(X(m[w], "hagiEv"), X(m[w], "teamDmg"))));
                    Console.WriteLine($"| {(c == 0 ? (i + 1).ToString() : "")} | {(c == 0 ? top5[i].Free : "")} | {(c == 0 ? S307.Seats(top5[i].F) : "")} | {cmpCols[c].Name} | {string.Join(" | ", m.Select(x => x.Cell))} | {F1(A4(m))} | {share}% | {m[WBoss].WinTs} |");
                }
            Console.WriteLine();
            Console.WriteLine($"### 6〜{res.Count} 位（段3・4 波 ／ 強 ／ ハギの割合 近衛 ／ 大隊）");
            Console.WriteLine();
            for (int i = 5; i < res.Count; i++)
                Console.WriteLine($"- {i + 1} 位: {res[i].Free}（{S307.Seats(res[i].F)}）・4 波 {F1(A4(res[i].M))} ／ 強 {F1(res[i].M[WStrong].Win)} ／ ハギ {Pc(X(res[i].M[WGuard], "hagiEv"), X(res[i].M[WGuard], "teamDmg"))} ／ {Pc(X(res[i].M[WBat], "hagiEv"), X(res[i].M[WBat], "teamDmg"))}%");
            Console.WriteLine();
            Console.WriteLine($"### 上位 5 台の中身（{v.Name} の列・§3-4 の 2〜8）");
            Console.WriteLine();
            var sub = new S377.B[top5.Count, 1][];
            for (int i = 0; i < top5.Count; i++) sub[i, 0] = grid[i, 0];
            Report(top5.Select((x, i) => $"{i + 1} 位（{S307.Seats(x.F)}）").ToArray(), new[] { cmpCols[0] }, sub, origin: -1, labels: top5.Select(x => $"search:{v.Name}:{S307.Seats(x.F)}").ToArray());
            Console.WriteLine($"（{v.Name} まで {sw.Elapsed.TotalSeconds:F0} 秒）");
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-2 の 4: 寄せ集め（試遊の台 × アタッカー以外の席の最良にハギ）
    // ---------------------------------------------------------------------------------
    sealed record S1(int V, int B, int S, double D4, double DS);
    static void Swap()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = S390.Play().Where(b => !Has(b.F, "hagi")).ToArray();
        int nb = boards.Length;
        var vers = HeavyVers;
        var s1W = new[] { I.Boss, I.Guard, I.Bat, I.M5 };
        var bs = new double[nb][]; var atk = new int[nb];
        var grid = new double[vers.Length, nb, 5][];
        var jobs = new List<(int V, int B, int S)>();
        for (int b = 0; b < nb; b++) jobs.Add((-1, b, -1));
        for (int v = 0; v < vers.Length; v++) for (int b = 0; b < nb; b++) foreach (int s in Seats5(boards[b].F)) jobs.Add((v, b, s));
        Parallel.ForEach(jobs, j =>
        {
            var f = j.V < 0 ? boards[j.B].F : Put(boards[j.B].F, j.S, vers[j.V].D);
            var r = new double[5];
            for (int w = 0; w < 4; w++) r[w] = WinRate(f, new[] { s1W[w] }, S1Seeds);
            r[4] = WinRate(f, I.StrongWaves, S1Seeds);
            if (j.V < 0) { bs[j.B] = r; atk[j.B] = Sh389Diag.AttackerSeat(f); } else grid[j.V, j.B, j.S] = r;
        });
        var best = new S1[vers.Length, nb];
        for (int v = 0; v < vers.Length; v++)
            for (int b = 0; b < nb; b++)
                best[v, b] = Enumerable.Range(0, 5).Where(s => s != atk[b] && grid[v, b, s] is not null).Select(s => new S1(v, b, s, Enumerable.Range(0, 4).Average(w => grid[v, b, s][w] - bs[b][w]), grid[v, b, s][4] - bs[b][4]))
                    .OrderByDescending(x => x.D4).ThenByDescending(x => x.DS).ThenBy(x => x.S).First();
        Console.WriteLine($"# {Tag} §3-2 の 4 寄せ集め —— 試遊の台（ハギのいない {nb} 台）× アタッカー以外の席の最良にハギ（版）・同じ席に素体");
        Console.WriteLine();
        Console.WriteLine($"段1: seed 0..{S1Seeds - 1} × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波（強い条件は並べ替えの2番目の鍵）で版ごとに席を選ぶ。段2: seed 0..{SwapSeeds - 1}（指示書の許し・重いので）× ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強。アタッカーは第389期と同じ決め方。H2 は規定と同じ札なので外した。");
        Console.WriteLine();
        Console.WriteLine("| 台（アタッカー）| 元 4 波 ／ 強 | " + string.Join(" | ", vers.Select(v => $"{v.Name}: 席（誰を）4 波の差 ／ 強の差")) + " | 素体（規定の席）|");
        Console.WriteLine("|---|---|" + string.Concat(vers.Select(_ => "---|")) + "---|");
        var w5 = new[] { new[] { I.Boss }, new[] { I.Guard }, new[] { I.Bat }, new[] { I.M5 }, I.StrongWaves };
        double[] M5(Formation f) { var r = new double[5]; Parallel.For(0, 5, w => r[w] = WinRate(f, w5[w], SwapSeeds)); return r; }
        var d4 = new double[vers.Length, nb];
        var dsx = new double[vers.Length, nb];
        for (int b = 0; b < nb; b++)
        {
            var b0 = M5(boards[b].F);
            var cells = new List<string>();
            for (int v = 0; v < vers.Length; v++)
            {
                var x = best[v, b]; var a = M5(Put(boards[b].F, x.S, vers[v].D));
                d4[v, b] = Enumerable.Range(0, 4).Average(w => a[w] - b0[w]); dsx[v, b] = a[4] - b0[4];
                cells.Add($"{SeatName(x.S)}（{Short(boards[b].F[x.S]!)}）{Sg(d4[v, b])} ／ {Sg(dsx[v, b])}");
            }
            var pl = M5(Put(boards[b].F, best[0, b].S, S388.PlainHagi));
            Console.WriteLine($"| {boards[b].Name}（{SeatName(atk[b])} {Short(boards[b].F[atk[b]]!)}）| {F1(b0.Take(4).Average())} ／ {F1(b0[4])} | {string.Join(" | ", cells)} | {Sg(Enumerable.Range(0, 4).Average(w => pl[w] - b0[w]))} ／ {Sg(pl[4] - b0[4])} |");
        }
        Console.WriteLine();
        Console.WriteLine("| 版 | 4 波で +3pt 以上の台 | 強で +3pt 以上 | 4 波の差の平均 | 強の差の平均 | 最良の台 |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        for (int v = 0; v < vers.Length; v++)
        {
            int bb = Enumerable.Range(0, nb).OrderByDescending(b => d4[v, b]).First();
            Console.WriteLine($"| {vers[v].Name} | {Enumerable.Range(0, nb).Count(b => d4[v, b] >= Up)} ／ {nb} | {Enumerable.Range(0, nb).Count(b => dsx[v, b] >= Up)} | {Sg(Enumerable.Range(0, nb).Average(b => d4[v, b]))} | {Sg(Enumerable.Range(0, nb).Average(b => dsx[v, b]))} | {boards[bb].Name} {Sg(d4[v, bb])} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-2 の 5: `compare` のハギの行
    // ---------------------------------------------------------------------------------
    static void Cmp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().Where(r => Has(r.F, "hagi")).ToArray();
        int nw = EnemyCatalog.Stages.Count;
        var grids = HeavyVers.Select(c => CGrid(rows.Select(r => (r.Name, I.Swap(r.F, "hagi", c.D))).ToArray(), CompareSeeds)).ToArray();
        Console.WriteLine($"# {Tag} §3-2 の 5 `compare` のハギの行 —— {rows.Length} 行 × 版（第1〜5波・seed 0..{CompareSeeds - 1}・ハギだけ替える・H2 は規定と同じ札なので外した）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 席 | " + string.Join(" | ", HeavyVers.Select(c => c.Name + " 第1〜5波")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(HeavyVers.Select(_ => "---|")));
        for (int ri = 0; ri < rows.Length; ri++)
        {
            string seat = string.Join("・", rows[ri].F.Occupied().Where(o => o.Def.Id == "hagi").Select(o => SeatName(o.Slot)));
            Console.WriteLine($"| {rows[ri].Name} | {seat} | {string.Join(" | ", grids.Select(g => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w])))))} |");
        }
        Console.WriteLine();
        Console.WriteLine("| 版 | 第2〜5波の平均 | 規定との差 | 動いたセル（第2〜5波）| −10pt 以上落ちたセル |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        double Avg(double[,] g) => Enumerable.Range(0, rows.Length).Average(ri => Enumerable.Range(1, nw - 1).Average(w => g[ri, w]));
        for (int c = 0; c < HeavyVers.Length; c++)
        {
            int moved = 0, drop = 0;
            for (int ri = 0; ri < rows.Length; ri++) for (int w = 1; w < nw; w++) { if (grids[c][ri, w] != grids[0][ri, w]) moved++; if (grids[c][ri, w] - grids[0][ri, w] <= -10) drop++; }
            Console.WriteLine($"| {HeavyVers[c].Name} | {F1(Avg(grids[c]))} | {Sg(Avg(grids[c]) - Avg(grids[0]))} | {moved} | {drop} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Log1(string[] args)
    {
        string bn = args.Length > 3 ? args[3] : "試遊・ハギ 台B";
        string cn = args.Length > 4 ? args[4] : "H123";
        int wi = args.Length > 5 && int.TryParse(args[5], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 6 && int.TryParse(args[6], out int s0) ? s0 : 0;
        var all = StopBoards().Concat(CarryBoards()).ToList();
        foreach (var r in Presets.Versions.Where(r => r.Name.StartsWith("版・死神", StringComparison.Ordinal)))
            all.Add(new S388.Board(r.Name, r.F, r.F.Occupied().First(o => o.Def.Id == "hagi").Slot, "ハギ"));
        var b = all.FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal)) ?? all[0];
        var c = StopCols.FirstOrDefault(x => x.Name == cn) is { Name: not null } cc ? cc : StopCols[2];
        var (r1, _, _) = I.Raw(ColBoard(b, c.D), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name} × {c.Name} × {W8[wi].Name} × seed {seed}: {(r1.PlayerWon ? "勝ち" : "負け")}・{r1.Turns}T");
        foreach (var l in r1.Log) Console.WriteLine(l.Text);
    }
}
