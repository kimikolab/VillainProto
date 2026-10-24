using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S389 = Sh389Diag;
using S390 = Sh390Diag;
using S392 = Sh392Diag;
using S394 = Sh394Diag;

// =====================================================================================
// sh397 —— 第397期 段0: セッキの S2h を規定に（転生）／ 本体: スライムらしさ（セッキの版 B ／ R）と、ドルガの一撃をボスに届かせる（ドルガの版 G1 ／ G2 ／ G12）。
// 指示書は design/PHASE397_SLIME_DOLGA_BOSS_SPEC.md、報告は design/PHASE397_SLIME_DOLGA_BOSS.md。
//
//     dotnet run --project BattleSim -c Release 0 sh397 zero         # §2: `compare` の動き（第396期の HEAD の表と ／ 第396期の S2h の列と・(G2)）＋ 試遊・手番 芯3枚 ／ 包み × 5 物差し
//     dotnet run --project BattleSim -c Release 0 sh397 sboards      # §4-1 の 1: 試遊・手番 芯3枚 ／ 包み ／ 縛め × セッキの席（全通り・包みはセッキの席）× 列（規定 ／ B ／ R ／ 素体）× 8 物差し
//     dotnet run --project BattleSim -c Release 0 sh397 smix         # §4-1 の 2: 寄せ集め 21 台 × アタッカー以外の席の最良にセッキ（規定 ／ B ／ R・段2 seed 0..49）
//     dotnet run --project BattleSim -c Release 0 sh397 dboards      # §4-1 の 4: ドルガのいる台 × 列（規定 ／ G1 ／ G2 ／ G12 ／ 素体）× 8 物差し——勇者の止め ／ 勇者への与ダメ ／ 一振りの量
//     dotnet run --project BattleSim -c Release 0 sh397 cmp          # §4-3 の 10: `compare` 全 66 行 × セッキの版 ／ ドルガの版（seed 0..199）＋ (G2)
//     dotnet run --project BattleSim -c Release 0 sh397 log <s|d> <台の頭> <列> <物差し 0..7> <seed>
//     dotnet run --project BattleSim -c Release 0 sh397 check        # 自己検査
//
// **本体の版は規定にしない。** 替えた台 ／ 素体は器具の中だけ（試遊の台に足す台を除く）。**予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p397/predict.md`）。
// 「勇者」＝ 敵のうち最大 HP がいちばん大きく、2 番目の 2 倍を超える駒（器具の中の数え方だけ・engine はボスで分岐しない）。
// =====================================================================================
static partial class Sh397Diag
{
    internal const string PredictFile = ".tmp/p397/predict.md";
    const string Tag = "第397期";
    const int Seeds = 100, MixSeeds = 50, CompareSeeds = 200, TT = 9, S1Seeds = 10;
    const double Up = 3.0;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "zero" or "sboards" or "smix" or "dboards" or "cmp" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh397: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "zero": Zero(); return;
            case "sboards": SBoards(); return;
            case "smix": SMix(); return;
            case "dboards": DBoards(); return;
            case "cmp": Cmp(); return;
            case "log": Log1(args); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh397: モードは zero / sboards / smix / dboards / cmp / log / check。"); return;
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
    const int WBoss = 0, WGuard = 1, WBat = 2, WM2 = 3, WM4 = 4, WM5 = 5, WNine = 6, WStrong = 7;
    /// <summary>燃焼の駒（燃焼を入れる ／ 燃焼で育つ駒）。台の注記だけに使う。</summary>
    static readonly string[] BurnIds = { "borg", "hota", "hiyo", "tou", "beni" };

    // ---------------------------------------------------------------------------------
    // 版 ／ 列 ／ 台
    // ---------------------------------------------------------------------------------
    internal static readonly (string Name, UnitDef D)[] SVers = { ("規定", UnitCatalog.Sekki), ("B", UnitCatalog.SekkiB), ("R", UnitCatalog.SekkiR) };
    internal static readonly (string Name, UnitDef D)[] DVers = { ("規定", UnitCatalog.Dolga), ("G1", UnitCatalog.DolgaG1), ("G2", UnitCatalog.DolgaG2), ("G12", UnitCatalog.DolgaG12) };
    internal static UnitDef PlainSekki => _plainS ??= S392.Plain(UnitCatalog.SekkiH396);
    static UnitDef? _plainS;
    internal static (string Name, UnitDef D)[] SCols => SVers.Append(("素体", PlainSekki)).ToArray();
    internal static (string Name, UnitDef D)[] DCols => DVers.Append(("素体", S394.PlainDolga)).ToArray();

    /// <summary>§4-1 の 1 の台: 試遊・手番 芯3枚 ／ 試遊・手番 包み（セッキの席だけ）／ 試遊・手番 縛め。</summary>
    internal static (string Name, Formation F)[] SBoardList() => new (string, Formation)[]
    {
        ("試遊・手番 芯3枚", Presets.TempoLamp393(UnitCatalog.Tomo, UnitCatalog.Dolga)), ("試遊・手番 包み", Presets.SlimeLamp396()), ("試遊・手番 縛め", Presets.BindTempo395()),
    };

    /// <summary>
    /// §4-1 の 4 の台: ドルガのいる試遊の台（芯3枚 ／ 縛め ／ 包み ／ 探索1位）と手番の軸の台（灯×薙ぎ ／ 縛め収入型 ／ 溜め改（バンの席にトモ））、
    /// 第396期の芯 4 枚（トモ ＋ ガン ＋ ドルガ ＋ セッキ S2h）＋ 1枠の探索の上位 3 台（セッキは規定 ＝ S2h）。
    /// </summary>
    internal static (string Name, Formation F)[] DBoardList()
    {
        var all = CompareBuilds();
        Formation Row(string n) => all.First(r => r.Name == n).F;
        var tame = Row("溜め改 (クグ×バン×ガン)");
        return new (string, Formation)[]
        {
            ("試遊・手番 芯3枚", Presets.TempoLamp393(UnitCatalog.Tomo, UnitCatalog.Dolga)),
            ("試遊・手番 縛め", Presets.BindTempo395()),
            ("試遊・手番 包み", Presets.SlimeLamp396()),
            ("試遊・手番 探索1位", Presets.TempoSeek392()),
            ("灯×薙ぎ (トモ×ドルガ)", Row("灯×薙ぎ (トモ×ドルガ)")),
            ("縛め収入型 (クグ×バン×ガン)", Row("縛め収入型 (クグ×バン×ガン)")),
            ("溜め改（バンの席にトモ）", Put(tame, SlotOf(tame, "ban"), UnitCatalog.Tomo)),
            ("芯4枚 1位（＋シオ）", Formation.Build(front1: UnitCatalog.Gan, front3: UnitCatalog.Tomo, center: UnitCatalog.Dolga, back1: UnitCatalog.Sekki, back3: UnitCatalog.Shio)),
            ("芯4枚 2位（＋トウ）", Formation.Build(front1: UnitCatalog.Sekki, front3: UnitCatalog.Tou, center: UnitCatalog.Tomo, back1: UnitCatalog.Dolga, back3: UnitCatalog.Gan)),
            ("芯4枚 3位（＋ボルグ）", Formation.Build(front1: UnitCatalog.Sekki, front3: UnitCatalog.Tomo, center: UnitCatalog.Dolga, back1: UnitCatalog.Borg, back3: UnitCatalog.Gan)),
        };
    }
    static string BurnNote(Formation f) { var b = f.Occupied().Where(o => BurnIds.Contains(o.Def.Id)).Select(o => Short(o.Def)).ToList(); return b.Count == 0 ? "燃焼の駒なし" : "燃焼の駒: " + string.Join("・", b); }

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    static void Add(S377.B b, string k, long v) { if (v != 0) b.X[k] = b.X.GetValueOrDefault(k) + v; }
    static long X(S377.B b, string k) => b.X.GetValueOrDefault(k);
    static readonly object MaxLock = new();
    static long _storePeak, _burstPeak, _maxHpPeak, _dHitPeak;

    static readonly Action<BattleResult, List<UnitState>, List<UnitState>, S377.B> Extra = (r, p, e, b) =>
    {
        var form = p.ToDictionary(u => u.InstanceId);
        var foe = e.ToDictionary(u => u.InstanceId);
        var sumTeam = new Dictionary<int, int>();
        var sekki = p.FirstOrDefault(u => u.Def.Id == "sekki");
        var dolga = p.FirstOrDefault(u => u.Def.Id == "dolga");
        int sid = sekki?.InstanceId ?? -9, did = dolga?.InstanceId ?? -9;
        var byHp = e.OrderByDescending(u => u.Def.MaxHp).ToList();
        UnitState? hero = byHp.Count > 0 && (byHp.Count == 1 || byHp[0].Def.MaxHp > 2 * byHp[1].Def.MaxHp) ? byHp[0] : null;
        int hid = hero?.InstanceId ?? -7;
        bool Foe(int id) => foe.ContainsKey(id) || (sumTeam.TryGetValue(id, out int st) && st == BattleContext.EnemyTeam);
        var dmgT = new long[BattleEngine.MaxTurns + 2];
        var deadAt = new Dictionary<int, int>();
        (int Act, AttackPattern? Pat) lastHeroHit = (-1, null);
        foreach (var x in r.Events)
        {
            int t = Math.Clamp(x.Turn, 0, BattleEngine.MaxTurns + 1);
            int tgt = x.TargetId ?? -3, act = x.ActorId ?? -2;
            switch (x.Kind)
            {
                case BattleEventKind.Summon: sumTeam[tgt] = x.Team ?? -1; break;
                case BattleEventKind.Whet:
                    if (x.WhetRoute == WhetRoute.RallyTurn && form.ContainsKey(tgt)) { Add(b, "rally8", 1); if (tgt == sid) Add(b, "rally8Sekki", 1); }
                    break;
                case BattleEventKind.Reveille:
                    if (x.Text == ReveilleLabels.Woken) { Add(b, "revWoken", 1); Add(b, tgt == sid ? "revSekki" : tgt == did ? "revDolga" : "revOther", 1); }
                    break;
                case BattleEventKind.Attack:
                    if (act == did) { Add(b, "dSwing", 1); if (x.Pattern == AttackPattern.All) Add(b, "dAll", 1); }
                    break;
                case BattleEventKind.Damage:
                {
                    bool byUs = form.ContainsKey(act) || (sumTeam.TryGetValue(act, out int sa) && sa == BattleContext.PlayerTeam);
                    if (Foe(tgt) && byUs) dmgT[t] += x.Amount;
                    if (tgt == sid) Add(b, "sTaken", x.Amount);
                    if (act == did && Foe(tgt))
                    {
                        Add(b, "dDmg", x.Amount);
                        lock (MaxLock) if (x.Amount > _dHitPeak) _dHitPeak = x.Amount;
                        if (tgt == hid) { Add(b, "dHeroHits", 1); Add(b, "dHeroDmg", x.Amount); if (t < TT) Add(b, $"dHeroT{t}", x.Amount); }
                        else { Add(b, "dMinionHits", 1); Add(b, "dMinionDmg", x.Amount); }
                        b.X["dHitMax"] = Math.Max(X(b, "dHitMax"), x.Amount);
                    }
                    if (tgt == hid)
                    {
                        Add(b, "heroDmg", x.Amount);
                        Add(b, act == did ? "heroDmgDolga" : form.ContainsKey(act) && x.Pattern is not null ? "heroDmgPartner" : x.Pattern is null ? "heroDmgTick" : "heroDmgOther", x.Amount);
                        lastHeroHit = (act, x.Pattern);
                    }
                    break;
                }
                case BattleEventKind.Death:
                    if (form.ContainsKey(tgt) && !deadAt.ContainsKey(tgt)) deadAt[tgt] = t;
                    if (Foe(tgt)) { Add(b, "kAll", 1); Add(b, act == sid ? "kSekki" : act == did ? "kDolga" : form.ContainsKey(act) ? "kPartner" : "kOther", 1); }
                    if (tgt == hid)
                    {
                        Add(b, "heroDead", 1); Add(b, "heroDeadT", t);
                        string who = lastHeroHit.Act == did && lastHeroHit.Pat is not null ? "dolga" : lastHeroHit.Pat is null ? "tick" : form.ContainsKey(lastHeroHit.Act) ? "partner" : "other";
                        Add(b, "heroKill:" + who, 1);
                    }
                    break;
                case BattleEventKind.Revive: deadAt.Remove(tgt); break;
            }
        }
        if (hero is not null) { Add(b, "heroSeen", 1); Add(b, "heroMaxHp", hero.Def.MaxHp); }
        Add(b, "maxTurnDmg", dmgT.Max());
        if (sekki is not null && deadAt.TryGetValue(sid, out int st2)) { Add(b, "sFellN", 1); Add(b, "sFellT", st2); }
        foreach (var (id, tl) in r.TallyByUnit)
        {
            if (tl.R397HeldLost > 0) { Add(b, "held:" + id, tl.R397HeldLost); Add(b, "heldAll", tl.R397HeldLost); }
            Add(b, "dte:" + id, tl.DamageToEnemy);
            if (id == "dolga")
            {
                Add(b, "focusSwings", tl.R397FocusSwings); Add(b, "heavyAdd", tl.R397HeavyAdd);
                if (tl.R397FocusByN is { } fb) for (int n = 1; n < fb.Length; n++) Add(b, $"focusN{n}", fb[n]);
            }
            if (id != "sekki") continue;
            Add(b, "wrapN", tl.R396WrapHits); Add(b, "wrapAmt", tl.R396WrapAmt); Add(b, "wrapHp", tl.R396WrapHp);
            Add(b, "burst", tl.R396Bursts); Add(b, "burstAmt", tl.R396BurstAmt); Add(b, "burstDealt", tl.R396BurstDealt);
            Add(b, "bInt", tl.R396BurstInterrupt); Add(b, "bYield", tl.R396BurstYield); Add(b, "bGift", tl.R396BurstGift); Add(b, "bOver", tl.R396BurstOverflow); Add(b, "bOther", tl.R396BurstOther);
            Add(b, "swell", tl.R397SwellGain); Add(b, "maxHpPeak", tl.R397MaxHpPeak); Add(b, "havenHolds", tl.R397HavenHolds);
            lock (MaxLock) { _storePeak = Math.Max(_storePeak, tl.R396StoreMax); _burstPeak = Math.Max(_burstPeak, tl.R396BurstMax); _maxHpPeak = Math.Max(_maxHpPeak, tl.R397MaxHpPeak); }
        }
    };

    static S377.B[] M8(Formation f, int seeds = Seeds)
    {
        var g = new S377.B[W8.Length];
        Parallel.For(0, W8.Length, wi => g[wi] = S377.Measure(f, W8[wi].Ws, seeds, Extra));
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
    static string MaxT(S377.B[] x) => string.Join(" ／ ", new[] { WBoss, WGuard, WBat }.Select(w => F0((double)X(x[w], "maxTurnDmg") / Math.Max(1, x[w].N))));

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
    static (string Name, Formation F)[] SwapRows(string id, UnitDef d) => CompareBuilds().Select(r => (r.Name, Has(r.F, id) ? I.Swap(r.F, id, d) : r.F)).ToArray();

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
        var rows = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count;
        var now = CGrid(rows, CompareSeeds);
        var s2h = CGrid(SwapRows("sekki", UnitCatalog.SekkiS2h), CompareSeeds);
        var old = CGrid(SwapRows("sekki", UnitCatalog.SekkiH396), CompareSeeds);
        var map = S390.BalanceMap(S390.GitShow("4220eab3:docs/balance.md"));
        int moved = 0, up = 0, same = 0, cells = 0, oldSame = 0; double sum = 0;
        var movedRows = new List<string>();
        for (int ri = 0; ri < rows.Length; ri++)
        {
            var cc = map[rows[ri].Name]; var parts = new List<string>();
            for (int w = 0; w < nw; w++)
            {
                cells++;
                if (now[ri, w] == s2h[ri, w]) same++;
                double o = double.Parse(cc[w].TrimEnd('%'));
                if (o == old[ri, w]) oldSame++;
                if (o != now[ri, w]) { moved++; if (now[ri, w] > o) up++; sum += now[ri, w] - o; parts.Add($"第{w + 1}波 {F1(o)} → {F1(now[ri, w])}"); }
            }
            if (parts.Count > 0) movedRows.Add($"| {rows[ri].Name} | {string.Join("・", parts)} |");
        }
        Console.WriteLine($"# {Tag} §2 段0: セッキの規定 ＝ 第396期の S2h（転生）");
        Console.WriteLine();
        Console.WriteLine($"## 1. `compare`（全 {rows.Length} 行 × {nw} 波 × seed 0..{CompareSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"- 第396期の HEAD（4220eab3）の `docs/balance.md` と違うセル **{moved}**（上がり {up} ／ 落ち {moved - up}・差の合計 {Sg(sum)}pt）");
        Console.WriteLine($"- 第396期の S2h の列（セッキを `SekkiS2h` に替えた全行）と同じセル {same} ／ {cells}・旧（`SekkiH396`）に戻した全行が HEAD の表と同じセル {oldSame} ／ {cells}");
        Console.WriteLine();
        Console.WriteLine("| 行 | 動いたセル |");
        Console.WriteLine("|---|---|");
        foreach (var l in movedRows) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine("### (G2) 旧の規定から −10.0pt 以上落ちた行（分母 `compare` 全 66 行・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 落ちた行 | 落ち（波）| 駒ごとの他の行の平均の変化（行数）| 判定 |");
        Console.WriteLine("|---|---|---|---|---|");
        G2Split("規定（S2h）", rows, old, now, Enumerable.Range(0, rows.Length).Where(ri => Has(rows[ri].F, "sekki")));
        Console.WriteLine("（行が無ければ −10pt 以上落ちた行は無い）");
        Console.WriteLine();

        var boards = new (string Name, Formation F)[] { ("試遊・手番 芯3枚", Presets.TempoLamp393(UnitCatalog.Tomo, UnitCatalog.Dolga)), ("試遊・手番 包み", Presets.SlimeLamp396()) };
        var ws = new[] { WBoss, WGuard, WBat, WM5, WStrong };
        Console.WriteLine($"## 2. 試遊・手番 芯3枚 ／ 試遊・手番 包み × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件（seed 0..{Seeds - 1}）・包みはセッキ（規定 ／ 第396期の S2h ／ 旧）");
        Console.WriteLine();
        Console.WriteLine("| 台 | セッキ | " + string.Join(" | ", ws.Select(w => W8[w].Name)) + " | 規定と S2h の差 |");
        Console.WriteLine("|---|---|" + string.Concat(ws.Select(_ => "---|")) + "---|");
        foreach (var bd in boards)
        {
            var defs = Has(bd.F, "sekki") ? new (string Name, UnitDef D)[] { ("規定（第397期）", UnitCatalog.Sekki), ("第396期の S2h", UnitCatalog.SekkiS2h), ("旧 SekkiH396", UnitCatalog.SekkiH396) } : new (string Name, UnitDef D)[] { ("—（セッキなし）", null!) };
            var m = defs.Select(x => { var f = x.D is null ? bd.F : Put(bd.F, SlotOf(bd.F, "sekki"), x.D); var g = new S377.B[ws.Length]; Parallel.For(0, ws.Length, k => g[k] = S377.Measure(f, W8[ws[k]].Ws, Seeds, Extra)); return g; }).ToArray();
            for (int i = 0; i < defs.Length; i++)
                Console.WriteLine($"| {(i == 0 ? $"{bd.Name}（{Seats(bd.F)}）" : "")} | {defs[i].Name} | {string.Join(" | ", m[i].Select(x => x.Cell))} | {(i == 0 && defs.Length > 1 ? $"違うセル {Enumerable.Range(0, ws.Length).Count(k => m[0][k].Cell != m[1][k].Cell)}" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-1 の 1: セッキの台
    // ---------------------------------------------------------------------------------
    static void SBoards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = SBoardList();
        var cols = SCols;
        int nb = boards.Length, nc = cols.Length, iDef = 0;
        var orig = new S377.B[nb][];
        var g = new S377.B[nb, nc, 5][];
        int[] SeatsOf(int bi) => Has(boards[bi].F, "sekki") ? new[] { SlotOf(boards[bi].F, "sekki") } : Seats5(boards[bi].F);
        for (int bi = 0; bi < nb; bi++)
        {
            orig[bi] = M8(boards[bi].F);
            for (int ci = 0; ci < nc; ci++) foreach (int s in SeatsOf(bi)) g[bi, ci, s] = M8(Put(boards[bi].F, s, cols[ci].D));
        }
        int Best(int bi, int ci) => SeatsOf(bi).OrderByDescending(s => A4(g[bi, ci, s]!)).ThenByDescending(s => g[bi, ci, s]![WStrong].Win).ThenBy(s => s).First();
        var best = new int[nb, nc];
        for (int bi = 0; bi < nb; bi++) for (int ci = 0; ci < nc; ci++) best[bi, ci] = Best(bi, ci);
        S377.B[] At(int bi, int ci) => g[bi, ci, best[bi, ci]]!;
        int iPlain = nc - 1;
        int[] w5 = { WBoss, WGuard, WBat, WM5, WStrong };

        Console.WriteLine($"# {Tag} §4-1 の 1 セッキの台 {nb} × 列（元 ／ {string.Join(" ／ ", cols.Select(c => c.Name))}）× 8 物差し（seed 0..{Seeds - 1}）——席は全通り（包みはセッキの席だけ）・列ごとに 4 波 → 強い条件で最良の席");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"- **{boards[bi].Name}**: {Seats(boards[bi].F)}・最良の席: " + string.Join("・", Enumerable.Range(0, nc).Select(ci => $"{cols[ci].Name} {SeatName(best[bi, ci])}（{Short(boards[bi].F[best[bi, ci]]!)}）")));
        Console.WriteLine();
        Console.WriteLine("## 1. 勝率・決着T・倒れた編成の駒 ／ 4 波 ／ 元との差 ／ 規定との差（4 波 ／ 強）／ 同じ席の素体との差 ／ 隊の1ターンの最大");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列（席）| " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 | 元との差 | 規定との差 | 素体との差 | 1ターンの最大 |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|---|---|---|---|");
        for (int bi = 0; bi < nb; bi++)
        {
            var o = orig[bi];
            Console.WriteLine($"| {boards[bi].Name} | 元 | {string.Join(" | ", o.Select(x => x.Cell))} | {F1(A4(o))} | — | — | — | {MaxT(o)} |");
            for (int ci = 0; ci < nc; ci++)
            {
                var a = At(bi, ci); var pl = g[bi, iPlain, best[bi, ci]]!; var df = g[bi, iDef, best[bi, ci]]!;
                Console.WriteLine($"| | {cols[ci].Name}（{SeatName(best[bi, ci])}）| {string.Join(" | ", a.Select(x => x.Cell))} | {F1(A4(a))} | {Sg(D4(a, o))} ／ {Sg(a[WStrong].Win - o[WStrong].Win)} | {(ci == 0 || ci == iPlain ? "—" : $"{Sg(D4(a, df))} ／ {Sg(a[WStrong].Win - df[WStrong].Win)}")} | {(ci == iPlain ? "—" : $"{Sg(D4(a, pl))} ／ {Sg(a[WStrong].Win - pl[WStrong].Win)}")} | {MaxT(a)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 1'. 席ごとの 4 波 ／ 強い条件");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", Enumerable.Range(0, 5).Select(SeatName)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(0, 5).Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
                Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {cols[ci].Name} | {string.Join(" | ", Enumerable.Range(0, 5).Select(s => g[bi, ci, s] is { } x ? $"{Short(boards[bi].F[s]!)}: {F1(A4(x))} ／ {F1(x[WStrong].Win)}" : "—"))} |");
        Console.WriteLine();

        Console.WriteLine("## 2. 包む ／ 膨らむ ／ 弾き返す（1戦）——包んだ回数 ／ 吸った量・セッキの HP で受けた ／ 被ダメ・最大 HP の最大（B）・セッキが倒れた戦 %（T）・弾き返し 回数 ／ 1回 ／ 与えた・きっかけ（叩き起こしなど ／ 譲り ／ ギフト ／ 溢れ ／ ほか）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 包む ／ 吸った | HP ／ 被ダメ | 最大 HP | 倒れた（T）| 弾き返し | きっかけ |");
        Console.WriteLine("|---|---|---|---|---|--:|---|---|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < SVers.Length; ci++)
                foreach (int w in w5)
                {
                    var x = At(bi, ci)[w]; long n = X(x, "burst");
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? boards[bi].Name : "")} | {(w == WBoss ? cols[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "wrapN"), x.N)} ／ {Per(X(x, "wrapAmt"), x.N)} | {Per(X(x, "wrapHp"), x.N)} ／ {Per(X(x, "sTaken"), x.N)} | {(X(x, "maxHpPeak") == 0 ? "70" : Per(X(x, "maxHpPeak"), x.N))} | {Pc(X(x, "sFellN"), x.N)}（T{Per(X(x, "sFellT"), X(x, "sFellN"))}）| {Per(n, x.N)} ／ {(n == 0 ? "—" : F0((double)X(x, "burstAmt") / n))} ／ {Per(X(x, "burstDealt"), x.N)} | {Per(X(x, "bInt"), x.N)} ／ {Per(X(x, "bYield"), x.N)} ／ {Per(X(x, "bGift"), x.N)} ／ {Per(X(x, "bOver"), x.N)} ／ {Per(X(x, "bOther"), x.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 3. R の手番（1戦）——包まれて手番を失った回数（誰が）・ガンの +8 ／ 叩き起こし（規定 → R）・**手番を失った駒の与ダメの差**（R − 規定・同じ席）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 物差し | 失った手番（誰が）| +8: 規定 → R | 叩き起こし: 規定 → R | 失った駒の与ダメ: 規定 → R（差）|");
        Console.WriteLine("|---|---|---|---|---|---|");
        int iR = Array.FindIndex(SVers, v => v.Name == "R");
        for (int bi = 0; bi < nb; bi++)
            foreach (int w in w5)
            {
                int s = best[bi, iR]; var xr = g[bi, iR, s]![w]; var x0 = g[bi, iDef, s]![w];
                var heldIds = xr.X.Keys.Where(k => k.StartsWith("held:", StringComparison.Ordinal)).Select(k => k[5..]).OrderByDescending(id => xr.X["held:" + id]).ToList();
                string who = string.Join("・", heldIds.Select(id => $"{Nm(id)} {Per(xr.X["held:" + id], xr.N)}"));
                string dte = string.Join("・", heldIds.Select(id => { long a = X(x0, "dte:" + id), r2 = X(xr, "dte:" + id); return $"{Nm(id)} {Per(a, x0.N)} → {Per(r2, xr.N)}（{Sg((double)r2 / xr.N - (double)a / x0.N)}）"; }));
                Console.WriteLine($"| {(w == WBoss ? $"{boards[bi].Name}（{SeatName(s)}）" : "")} | {W8[w].Name} | {Per(X(xr, "heldAll"), xr.N)}（{(who.Length == 0 ? "—" : who)}）| {Per(X(x0, "rally8"), x0.N)} → {Per(X(xr, "rally8"), xr.N)} | {Per(X(x0, "revWoken"), x0.N)} → {Per(X(xr, "revWoken"), xr.N)} | {(dte.Length == 0 ? "—" : dte)} |");
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
        Signs(boards.Select((bd, bi) => (bd.Name, orig[bi], Enumerable.Range(0, SVers.Length).SelectMany(ci => SeatsOf(bi).Select(s => ($"{cols[ci].Name}（{SeatName(s)}）", g[bi, ci, s]!))).ToArray())).ToArray());
        Console.WriteLine($"- 帳簿の最大 {_storePeak}・1回の弾き返しの最大 {_burstPeak}・セッキの最大 HP の最大 {(_maxHpPeak == 0 ? 70 : _maxHpPeak)}（全台 × 全列 × 全物差しの1戦）");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上（元との差）／ あふれ）。</summary>
    static void Signs((string Name, S377.B[] Orig, (string Col, S377.B[] M)[] Cells)[] items)
    {
        Console.WriteLine("## 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上（元との差）／ あふれ）");
        Console.WriteLine();
        var signs = new List<string>();
        foreach (var (name, o, cells) in items)
            foreach (var (col, a) in cells)
            {
                string at = $"{name} × {col}";
                foreach (int w in new[] { WGuard, WBat }) { var x = a[w]; if (x.Win1T * 20 >= x.N && x.Win1T > 0) signs.Add($"{at} × {W8[w].Name}: 1T 決着 {Pc(x.Win1T, x.N)}%"); }
                if (a[WBoss].BossFast > 0) signs.Add($"{at} × ボス: 4T 未満の勝ち {Pc(a[WBoss].BossFast, a[WBoss].N)}%");
                for (int w = 0; w < W8.Length; w++)
                {
                    var x = a[w]; var x0 = o[w];
                    if (100.0 * x.Cap / x.N - 100.0 * x0.Cap / x0.N >= 5) signs.Add($"{at} × {W8[w].Name}: 30T の打ち切り {Pc(x0.Cap, x0.N)} → {Pc(x.Cap, x.N)}%");
                    if (x.Overflow > 0) signs.Add($"{at} × {W8[w].Name}: あふれの兆し {x.Overflow} 件");
                }
            }
        if (signs.Count == 0) Console.WriteLine("- 無し。");
        foreach (var s in signs.Take(40)) Console.WriteLine("- " + s);
        if (signs.Count > 40) Console.WriteLine($"- ほか {signs.Count - 40} 件");
    }

    // ---------------------------------------------------------------------------------
    // §4-1 の 2: 寄せ集め
    // ---------------------------------------------------------------------------------
    internal static (string Name, Formation F)[] Play() => Presets.Versions.Where(r => r.Name.StartsWith("試遊・", StringComparison.Ordinal) && !Has(r.F, "sekki")).ToArray();

    static void SMix()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Play();
        int nb = boards.Length;
        var s1W = new[] { I.Boss, I.Guard, I.Bat, I.M5 };
        var bs = new double[nb][]; var atk = new int[nb];
        var grid = new double[SVers.Length, nb, 5][];
        var jobs = new List<(int V, int B, int S)>();
        for (int b = 0; b < nb; b++) jobs.Add((-1, b, -1));
        for (int v = 0; v < SVers.Length; v++) for (int b = 0; b < nb; b++) foreach (int s in Seats5(boards[b].F)) jobs.Add((v, b, s));
        Parallel.ForEach(jobs, j =>
        {
            var f = j.V < 0 ? boards[j.B].F : Put(boards[j.B].F, j.S, SVers[j.V].D);
            var r = new double[5];
            for (int w = 0; w < 4; w++) r[w] = WinRate(f, new[] { s1W[w] }, S1Seeds);
            r[4] = WinRate(f, I.StrongWaves, S1Seeds);
            if (j.V < 0) { bs[j.B] = r; atk[j.B] = S389.AttackerSeat(f); } else grid[j.V, j.B, j.S] = r;
        });
        var cache = new Dictionary<string, S377.B[]>();
        S377.B[] M(int b, int s, UnitDef? d, string key) { string k = $"{b}:{s}:{key}"; lock (cache) if (cache.TryGetValue(k, out var x)) return x; var y = M8(d is null ? boards[b].F : Put(boards[b].F, s, d), MixSeeds); lock (cache) cache[k] = y; return y; }

        Console.WriteLine($"# {Tag} §4-1 の 2 寄せ集め —— 試遊の台 {nb}（セッキのいない台）× アタッカー以外の席の最良にセッキ（規定 ／ B ／ R・第396期と同じやり方・段2 は seed 0..{MixSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 台（アタッカー）| 元 4 波 ／ 強 | " + string.Join(" | ", SVers.Select(v => $"{v.Name}: 席（誰を）4 波の差 ／ 強の差")) + " | 素体（規定の席）|");
        Console.WriteLine("|---|---|" + string.Concat(SVers.Select(_ => "---|")) + "---|");
        var lists = SVers.Select(_ => new List<(int B, int S, double D4, double DS)>()).ToArray();
        var plainList = new List<(double D4, double DS)>();
        for (int b = 0; b < nb; b++)
        {
            var b0 = M(b, -1, null, "orig");
            var cells = new List<string>(); int defSeat = -1;
            for (int v = 0; v < SVers.Length; v++)
            {
                var seats = Seats5(boards[b].F).Where(s => s != atk[b]).OrderByDescending(s => Enumerable.Range(0, 4).Average(w => grid[v, b, s]![w] - bs[b][w])).ThenByDescending(s => grid[v, b, s]![4] - bs[b][4]).ThenBy(s => s).ToArray();
                if (seats.Length == 0) { cells.Add("—"); continue; }
                int s0 = seats[0]; var a = M(b, s0, SVers[v].D, SVers[v].Name);
                double d4 = D4(a, b0), ds = a[WStrong].Win - b0[WStrong].Win;
                lists[v].Add((b, s0, d4, ds));
                if (v == 0) defSeat = s0;
                cells.Add($"{SeatName(s0)}（{Short(boards[b].F[s0]!)}）{Sg(d4)} ／ {Sg(ds)}");
            }
            string pls = "—";
            if (defSeat >= 0) { var p = M(b, defSeat, PlainSekki, "plain"); double pd4 = D4(p, b0), pds = p[WStrong].Win - b0[WStrong].Win; plainList.Add((pd4, pds)); pls = $"{Sg(pd4)} ／ {Sg(pds)}"; }
            Console.WriteLine($"| {boards[b].Name}（{SeatName(atk[b])} {Short(boards[b].F[atk[b]]!)}）| {F1(A4(b0))} ／ {F1(b0[WStrong].Win)} | {string.Join(" | ", cells)} | {pls} |");
        }
        Console.WriteLine();
        for (int v = 0; v < SVers.Length; v++)
        {
            var l = lists[v];
            Console.WriteLine($"- **{SVers[v].Name}**: 4 波で +{Up:F0}pt 以上上がった台 **{l.Count(x => x.D4 >= Up)}** ／ {l.Count}・強い条件で +{Up:F0}pt 以上 **{l.Count(x => x.DS >= Up)}**・4 波で −{Up:F0}pt 以上下がった台 {l.Count(x => x.D4 <= -Up)}・4 波の差の平均 {Sg(l.Average(x => x.D4))}・強の差の平均 {Sg(l.Average(x => x.DS))}");
        }
        if (plainList.Count > 0) Console.WriteLine($"- 素体（規定の席）: 4 波で +{Up:F0}pt 以上 {plainList.Count(x => x.D4 >= Up)} ／ {plainList.Count}・強い条件で +{Up:F0}pt 以上 {plainList.Count(x => x.DS >= Up)}・4 波の差の平均 {Sg(plainList.Average(x => x.D4))}・強の差の平均 {Sg(plainList.Average(x => x.DS))}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-1 の 4: ドルガの台
    // ---------------------------------------------------------------------------------
    static void DBoards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = DBoardList();
        var cols = DCols;
        int nb = boards.Length, nc = cols.Length;
        var g = new S377.B[nb, nc][];
        for (int bi = 0; bi < nb; bi++) for (int ci = 0; ci < nc; ci++) g[bi, ci] = M8(Put(boards[bi].F, SlotOf(boards[bi].F, "dolga"), cols[ci].D));

        Console.WriteLine($"# {Tag} §4-1 の 4 ドルガの台 {nb} × 列（{string.Join(" ／ ", cols.Select(c => c.Name))}）× 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var bd in boards) Console.WriteLine($"- **{bd.Name}**: {Seats(bd.F)}（ドルガの席 {SeatName(SlotOf(bd.F, "dolga"))}・{BurnNote(bd.F)}）");
        Console.WriteLine();
        Console.WriteLine("## 1. 勝率・決着T（ボスの列はボスの倒しT）・倒れた編成の駒 ／ 4 波 ／ 規定との差（4 波 ／ ボス ／ 強）／ 隊の1ターンの最大");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 | 規定との差 | 1ターンの最大 |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|---|---|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < nc; ci++)
            {
                var a = g[bi, ci]; var d0 = g[bi, 0];
                Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {cols[ci].Name} | {string.Join(" | ", a.Select(x => x.Cell))} | {F1(A4(a))} | {(ci == 0 ? "—" : $"{Sg(D4(a, d0))} ／ {Sg(a[WBoss].Win - d0[WBoss].Win)} ／ {Sg(a[WStrong].Win - d0[WStrong].Win)}")} | {MaxT(a)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 2. ボス（1戦）——勇者の最大 HP（見えた値）・**勇者に止めを刺したのは誰か**（ドルガ ／ 相方 ／ 刻み（型なし）／ ほか・勇者が倒れた戦の %）・**勇者へのダメージの内訳**（ドルガ ／ 相方 ／ 刻み ／ ほか %）・勇者の倒しT");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 勝ち | 勇者 HP | 勇者が倒れた（T）| 止め: ドルガ ／ 相方 ／ 刻み ／ ほか | 勇者への与ダメ: ドルガ ／ 相方 ／ 刻み ／ ほか | 勇者への与ダメの合計 |");
        Console.WriteLine("|---|---|--:|--:|---|---|---|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < DVers.Length; ci++)
            {
                var x = g[bi, ci][WBoss]; long hd = X(x, "heroDead"), dm = X(x, "heroDmg");
                Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {cols[ci].Name} | {F1(x.Win)} | {Per(X(x, "heroMaxHp"), X(x, "heroSeen"))} | {Pc(hd, x.N)}%（T{Per(X(x, "heroDeadT"), hd)}）| {Pc(X(x, "heroKill:dolga"), hd)} ／ {Pc(X(x, "heroKill:partner"), hd)} ／ {Pc(X(x, "heroKill:tick"), hd)} ／ {Pc(X(x, "heroKill:other"), hd)} | {Pc(X(x, "heroDmgDolga"), dm)} ／ {Pc(X(x, "heroDmgPartner"), dm)} ／ {Pc(X(x, "heroDmgTick"), dm)} ／ {Pc(X(x, "heroDmgOther"), dm)} | {Per(dm, x.N)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 3. ドルガの一振り（ボス・1戦）——勇者への一撃の平均 ／ 取り巻きへの一撃の平均・勇者へ T1〜T8 の量・振った ／ うち全体・全体の薙ぎで当たった敵の数（G1 ／ G12 の振りの分布）・G2 の足し分の合計・1戦の一撃の最大");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 勇者へ ／ 取り巻きへ（1撃）| 勇者へ T1〜T8 | 振った（全体）| 全体で当たった数 1〜5+ | G2 の足し分 | 一撃の最大 |");
        Console.WriteLine("|---|---|---|---|---|---|--:|--:|");
        for (int bi = 0; bi < nb; bi++)
            for (int ci = 0; ci < DVers.Length; ci++)
            {
                var x = g[bi, ci][WBoss];
                long hh = X(x, "dHeroHits"), mh = X(x, "dMinionHits");
                string fN = X(x, "focusSwings") == 0 ? "—" : string.Join(" ", Enumerable.Range(1, 5).Select(n => n < 5 ? Per(X(x, $"focusN{n}"), x.N) : Per(Enumerable.Range(5, 5).Sum(k => X(x, $"focusN{k}")), x.N)));
                Console.WriteLine($"| {(ci == 0 ? boards[bi].Name : "")} | {cols[ci].Name} | {(hh == 0 ? "—" : F0((double)X(x, "dHeroDmg") / hh))} ／ {(mh == 0 ? "—" : F0((double)X(x, "dMinionDmg") / mh))} | {string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => F0((double)X(x, $"dHeroT{t}") / x.N)))} | {Per(X(x, "dSwing"), x.N)}（{Per(X(x, "dAll"), x.N)}）| {fN} | {Per(X(x, "heavyAdd"), x.N)} | {Per(X(x, "dHitMax"), x.N)} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 4. 精鋭 ／ 本編で強くなりすぎていないか——精鋭の 1T 決着 %（近衛 ／ 大隊）・本編の決着T（第2 ／ 4 ／ 5波）・九体の決着T");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", DVers.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(DVers.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(0, DVers.Length).Select(ci => { var a = g[bi, ci]; return $"1T {Pc(a[WGuard].Win1T, a[WGuard].N)} ／ {Pc(a[WBat].Win1T, a[WBat].N)}・本編 {a[WM2].WinTs} ／ {a[WM4].WinTs} ／ {a[WM5].WinTs}T・九 {a[WNine].WinTs}T"; }))} |");
        Console.WriteLine();

        Console.WriteLine("## 5. 誰が何Tに倒れたか（規定 ／ ボスの勝率がいちばん良い版・ボス ／ 近衛 ／ 大隊 ／ 強い条件）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
        {
            int best = Enumerable.Range(1, DVers.Length - 1).OrderByDescending(ci => g[bi, ci][WBoss].Win).ThenByDescending(ci => A4(g[bi, ci])).First();
            Console.WriteLine($"- **{boards[bi].Name}**（ボスがいちばん良い版 {DVers[best].Name}・ボス {F1(g[bi, best][WBoss].Win)} ／ 規定 {F1(g[bi, 0][WBoss].Win)}）");
            foreach (int w in new[] { WBoss, WGuard, WBat, WStrong })
                foreach (int ci in new[] { 0, best })
                    Console.WriteLine($"  - {W8[w].Name}: {DVers[ci].Name} {F1(g[bi, ci][w].Win)}%・{g[bi, ci][w].WinTs}T — {g[bi, ci][w].FellCell}");
        }
        Console.WriteLine();
        Console.WriteLine("## 6. 止めを刺したのは誰か（ボス ／ 近衛 ／ 大隊・ドルガ ／ 相方 ／ ほか %・敵の撃破全部）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", cols.Select(c => c.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "---|")));
        for (int bi = 0; bi < nb; bi++)
            Console.WriteLine($"| {boards[bi].Name} | {string.Join(" | ", Enumerable.Range(0, nc).Select(ci => string.Join(" ／ ", new[] { WBoss, WGuard, WBat }.Select(w => { var x = g[bi, ci][w]; long k = X(x, "kAll"); return $"{Pc(X(x, "kDolga"), k)}・{Pc(X(x, "kPartner") + X(x, "kSekki"), k)}・{Pc(X(x, "kOther"), k)}"; }))))} |");
        Console.WriteLine();
        Signs(boards.Select((bd, bi) => (bd.Name, g[bi, 0], Enumerable.Range(1, DVers.Length - 1).Select(ci => (cols[ci].Name, g[bi, ci])).ToArray())).ToArray());
        Console.WriteLine($"- ドルガの一撃の最大（全台 × 全列 × 全物差しの1戦）: {_dHitPeak}（int のあふれまで遠い）");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-3 の 10: `compare` ＋ (G2)
    // ---------------------------------------------------------------------------------
    static void Cmp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var all = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count;
        Console.WriteLine($"# {Tag} §4-3 の 10 `compare` 全 {all.Length} 行 × 版（第1〜5波・seed 0..{CompareSeeds - 1}）");
        Console.WriteLine();
        foreach (var (id, label, vers) in new[] { ("sekki", "セッキ", SVers), ("dolga", "ドルガ", DVers) })
        {
            var grids = vers.Select(v => CGrid(all.Select(r => (r.Name, Has(r.F, id) ? I.Swap(r.F, id, v.D) : r.F)).ToArray(), CompareSeeds)).ToArray();
            var rows = Enumerable.Range(0, all.Length).Where(ri => Has(all[ri].F, id)).ToArray();
            Console.WriteLine($"## {label}の版（{label}のいる {rows.Length} 行・{label}だけ替える）");
            Console.WriteLine();
            Console.WriteLine("| 行 | " + string.Join(" | ", vers.Select(c => c.Name + " 第1〜5波")) + " |");
            Console.WriteLine("|---|" + string.Concat(vers.Select(_ => "---|")));
            foreach (int ri in rows.Where(ri => Enumerable.Range(1, vers.Length - 1).Any(c => Enumerable.Range(0, nw).Any(w => grids[c][ri, w] != grids[0][ri, w]))))
                Console.WriteLine($"| {all[ri].Name} | {string.Join(" | ", grids.Select(g => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w])))))} |");
            Console.WriteLine("（どの版でも規定と同じ行は略）");
            Console.WriteLine();
            for (int c = 1; c < vers.Length; c++)
            {
                int moved = 0, up = 0, movedOther = 0; double sum = 0;
                for (int ri = 0; ri < all.Length; ri++)
                    for (int w = 0; w < nw; w++)
                        if (grids[c][ri, w] != grids[0][ri, w])
                        {
                            if (!Has(all[ri].F, id)) { movedOther++; continue; }
                            moved++; if (grids[c][ri, w] > grids[0][ri, w]) up++; sum += grids[c][ri, w] - grids[0][ri, w];
                        }
                Console.WriteLine($"- {vers[c].Name}: 規定と違うセル {moved}（上がり {up} ／ 落ち {moved - up}・差の合計 {Sg(sum)}pt）・**{label}のいない行で動いたセル {movedOther}**（0 であること）");
            }
            Console.WriteLine();
            Console.WriteLine($"### (G2) 規定から −10.0pt 以上落ちた行（分母 `compare` 全 66 行・第2〜5波）");
            Console.WriteLine();
            Console.WriteLine("| 版 | 落ちた行 | 落ち（波）| 駒ごとの他の行の平均の変化（行数）| 判定 |");
            Console.WriteLine("|---|---|---|---|---|");
            for (int c = 1; c < vers.Length; c++) G2Split(vers[c].Name, all, grids[0], grids[c], rows);
            Console.WriteLine("（行が無ければ −10pt 以上落ちた行は無い）");
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Log1(string[] args)
    {
        bool dol = args.Length > 3 && args[3] == "d";
        string bn = args.Length > 4 ? args[4] : "試遊・手番 芯3枚";
        string cn = args.Length > 5 ? args[5] : "G1";
        int wi = args.Length > 6 && int.TryParse(args[6], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 7 && int.TryParse(args[7], out int s0) ? s0 : 0;
        var list = dol ? DBoardList() : SBoardList();
        var b = list.FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal));
        if (b.F is null) { Console.WriteLine($"台が無い: {bn}"); return; }
        var cols = dol ? DCols : SCols;
        var c = cols.FirstOrDefault(x => x.Name == cn); if (c.D is null) c = cols[0];
        string id = dol ? "dolga" : "sekki";
        int slot = Has(b.F, id) ? SlotOf(b.F, id) : 2;
        var (r, _, _) = I.Raw(Put(b.F, slot, c.D), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name}（{SeatName(slot)} を {c.Name}）× {W8[wi].Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
