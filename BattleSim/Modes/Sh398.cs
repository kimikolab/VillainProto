using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S389 = Sh389Diag;
using S390 = Sh390Diag;
using S392 = Sh392Diag;

// =====================================================================================
// sh398 —— 第398期 段0: セッキ B ／ ドルガ G12 を規定に ／ 本体: スライムが勇者の全体攻撃をしのぐ（ボス戦の数え ＋ セッキの版 K1 ／ K2 ／ K12）。
// 指示書は design/PHASE398_SLIME_HOLD_SPEC.md、報告は design/PHASE398_SLIME_HOLD.md。
//
//     dotnet run --project BattleSim -c Release 0 sh398 zero       # §2: `compare` の動き（第397期の HEAD の表と ／ 第397期の B ＋ G12 の列と・(G2)）＋ 試遊の 4 台 × 5 物差し（規定 ／ 第397期）
//     dotnet run --project BattleSim -c Release 0 sh398 boards     # §3 ＋ §5-1 の 1・2: 台 × 列（規定 ／ K1 ／ K2 ／ K12 ／ 素体）× 8 物差し・ボス戦の数え（T1〜T8）
//     dotnet run --project BattleSim -c Release 0 sh398 smix       # §5-1 の 3: 寄せ集め 21 台 × アタッカー以外の席の最良にセッキ（規定 ／ K1 ／ K2 ／ K12・段2 seed 0..49）
//     dotnet run --project BattleSim -c Release 0 sh398 cmp        # §5-1 の 4: `compare` 全 66 行 × セッキの版（seed 0..199）＋ (G2)
//     dotnet run --project BattleSim -c Release 0 sh398 log <台の頭> <列> <物差し 0..7> <seed>
//     dotnet run --project BattleSim -c Release 0 sh398 check      # 自己検査
//
// **本体の版は規定にしない。** 替えた台 ／ 素体は器具の中だけ（試遊の台に足す「版・耐える セッキ」を除く）。**予測のファイルが無ければ測定を走らせない**（`.tmp/p398/predict.md`）。
// 「勇者」＝ 敵のうち最大 HP がいちばん大きく、2 番目の 2 倍を超える駒（器具の中の数え方だけ・engine はボスで分岐しない）。
// =====================================================================================
static partial class Sh398Diag
{
    internal const string PredictFile = ".tmp/p398/predict.md";
    const string Tag = "第398期";
    const int Seeds = 100, MixSeeds = 50, CompareSeeds = 200, TT = 9, S1Seeds = 10;
    const double Up = 3.0;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "zero" or "boards" or "smix" or "cmp" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh398: 予測のファイル `{PredictFile}` がまだ無い。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "zero": Zero(); return;
            case "boards": Boards(); return;
            case "smix": SMix(); return;
            case "cmp": Cmp(); return;
            case "log": Log1(args); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh398: モードは zero / boards / smix / cmp / log / check。"); return;
        }
    }

    /// <summary>第399期: `compare` の行を第398期の姿で（セッキは旧 `SekkiH398`）。</summary>
    static (string Name, Formation F)[] CB398() => CompareBuilds().Select(r => (r.Name, Pin398(r.F))).ToArray();
    static string F1(double x) => S307.F1(x);
    static string F0(double x) => double.IsNaN(x) ? "—" : x.ToString("#,0");
    static string Sg(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pc(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => string.Join(" ／ ", Enumerable.Range(0, 5).Select(s => f[s] is { } d ? Short(d) : "—"));
    static string Nm(string id) => UnitCatalog.Everyone.FirstOrDefault(d => d.Id == id) is { } d ? Short(d) : id switch { "spore" => "胞子", "wight" => "亡者", "刻み" => "刻み", "召喚" => "召喚", _ => id };
    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }
    static int SlotOf(Formation f, string id) => f.Occupied().First(o => o.Def.Id == id).Slot;
    static string SeatName(int s) => s switch { 0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3", _ => $"席{s}" };
    static int[] Seats5(Formation f) => f.Occupied().Select(o => o.Slot).Where(s => s < 5).OrderBy(s => s).ToArray();

    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    static readonly int[] W4 = { 0, 1, 2, 5 };
    const int WBoss = 0, WGuard = 1, WBat = 2, WM2 = 3, WM4 = 4, WM5 = 5, WNine = 6, WStrong = 7;
    static readonly string[] BurnIds = { "borg", "hota", "hiyo", "tou", "beni" };

    // ---------------------------------------------------------------------------------
    // 版 ／ 列 ／ 台
    // ---------------------------------------------------------------------------------
    internal static readonly (string Name, UnitDef D)[] Vers = { ("規定", UnitCatalog.SekkiH398), ("K1", UnitCatalog.SekkiK1), ("K2", UnitCatalog.SekkiK2), ("K12", UnitCatalog.SekkiK12) };
    internal static UnitDef PlainSekki => _plainS ??= S392.Plain(UnitCatalog.SekkiH398);
    static UnitDef? _plainS;
    internal static (string Name, UnitDef D)[] Cols => Vers.Append(("素体", PlainSekki)).ToArray();

    /// <summary>§3 の台（燃焼の駒がいない手番の軸の台）と §5-1 の 2 の台（燃焼の駒がいる台）。`Burn` は燃焼の駒がいる台。</summary>
    internal static (string Name, Formation F, bool Burn)[] BoardList()
    {
        var all = CB398();
        Formation Row(string n) => all.First(r => r.Name == n).F;
        var tame = Row("溜め改 (クグ×バン×ガン)");
        return new (string, Formation, bool)[]
        {
            ("試遊・手番 縛め", Pin398(Presets.BindTempo395()), false),
            ("縛め収入型 (クグ×バン×ガン)", Row("縛め収入型 (クグ×バン×ガン)"), false),
            ("溜め改（バンの席にトモ）", Put(tame, SlotOf(tame, "ban"), UnitCatalog.TomoH399), false),
            ("試遊・手番 芯4枚", Pin398(Presets.FourCore397()), false),
            ("試遊・手番 芯3枚", Pin398(Presets.TempoLamp393()), true),
            ("試遊・手番 包み", Pin398(Presets.SlimeLamp396()), true),
        };
    }
    static string BurnNote(Formation f) { var b = f.Occupied().Where(o => BurnIds.Contains(o.Def.Id)).Select(o => Short(o.Def)).ToList(); return b.Count == 0 ? "燃焼の駒なし" : "燃焼の駒: " + string.Join("・", b); }

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    static void Add(S377.B b, string k, long v) { if (v != 0) b.X[k] = b.X.GetValueOrDefault(k) + v; }
    static long X(S377.B b, string k) => b.X.GetValueOrDefault(k);
    static readonly object MaxLock = new();
    static long _storePeak, _burstPeak, _maxHpPeak, _hitPeak;

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
        var deadAt = new Dictionary<int, int>();
        (int Act, AttackPattern? Pat) lastHeroHit = (-1, null);
        bool d6 = false;   // T6 のドルガの最初の振り（攻撃力）
        int heroSwung = 0;  // 勇者がそのターンに振った（最初の振りの攻撃力だけ数える）
        string Src(int act, AttackPattern? pat) => pat is null ? "刻み" : form.TryGetValue(act, out var u) ? u.Def.Id : "召喚";
        foreach (var x in r.Events)
        {
            int t = Math.Clamp(x.Turn, 0, BattleEngine.MaxTurns + 1);
            int tgt = x.TargetId ?? -3, act = x.ActorId ?? -2;
            switch (x.Kind)
            {
                case BattleEventKind.Summon: sumTeam[tgt] = x.Team ?? -1; break;
                case BattleEventKind.Attack:
                    if (act == hid && t < TT && (heroSwung & (1 << t)) == 0) { heroSwung |= 1 << t; Add(b, $"hAtk{t}", x.Amount); Add(b, $"hAtkN{t}", 1); }
                    if (act == did) { Add(b, "dSwing", 1); if (t < TT) Add(b, $"dSw{t}", 1); if (t == 6 && !d6) { d6 = true; Add(b, "dAtk6N", 1); Add(b, "dAtk6", x.Amount); if (x.Amount >= 255) Add(b, "dAtk6ge255", 1); } }
                    break;
                case BattleEventKind.Damage:
                {
                    if (act == hid && form.ContainsKey(tgt) && t < TT)
                    {
                        if (tgt == sid) Add(b, $"sTk{t}", x.Amount);
                        else { Add(b, $"aGot{t}", x.Amount); Add(b, $"aHits{t}", 1); }
                    }
                    if (act == did && tgt == hid) { Add(b, "dHeroHits", 1); Add(b, "dHeroDmg", x.Amount); if (t < TT) Add(b, $"dHeroT{t}", x.Amount); }
                    if (tgt == hid)
                    {
                        Add(b, "heroDmg", x.Amount);
                        Add(b, "hdBy:" + Src(act, x.Pattern), x.Amount);
                        lastHeroHit = (act, x.Pattern);
                    }
                    if (Foe(tgt) && form.ContainsKey(act)) lock (MaxLock) if (x.Amount > _hitPeak) _hitPeak = x.Amount;
                    break;
                }
                case BattleEventKind.Death:
                    if (form.ContainsKey(tgt) && !deadAt.ContainsKey(tgt)) deadAt[tgt] = t;
                    if (tgt == hid) { Add(b, "heroDead", 1); Add(b, "heroDeadT", t); Add(b, "hkBy:" + Src(lastHeroHit.Act, lastHeroHit.Pat), 1); }
                    break;
                case BattleEventKind.Revive: deadAt.Remove(tgt); break;
            }
        }
        if (hero is not null) { Add(b, "heroSeen", 1); Add(b, "heroMaxHp", hero.Def.MaxHp); }
        // 立っている数（編成の駒・ターン t の終わり）: 決着の後は勝てば生き残りの数 ／ 負ければ 0
        int n = p.Count, alive = p.Count(u => u.IsAlive);
        for (int t = 1; t < TT; t++)
        {
            int c = t > r.Turns ? (r.PlayerWon ? alive : 0) : n - deadAt.Values.Count(d => d <= t);
            Add(b, $"alive{t}", c);
        }
        int at6 = 5 > r.Turns ? (r.PlayerWon ? alive : 0) : n - deadAt.Values.Count(d => d <= 5);   // T6 の頭 ＝ T5 の終わり
        Add(b, "st6", at6); if (at6 >= 2) Add(b, "st6ge2", 1); if (at6 >= 3) Add(b, "st6ge3", 1);
        if (sekki is not null)
        {
            Add(b, "sIn", 1);
            if (deadAt.TryGetValue(sid, out int sf))
            {
                Add(b, "sFellN", 1); Add(b, "sFellT", sf);
                for (int k = 0; k <= 3; k++) Add(b, $"after{k}", deadAt.Count(kv => kv.Key != sid && kv.Value >= sf && kv.Value <= sf + k));
                Add(b, "beforeS", deadAt.Count(kv => kv.Key != sid && kv.Value < sf));
            }
            else Add(b, "sNever", 1);
        }
        foreach (var (id, tl) in r.TallyByUnit)
        {
            if (id != "sekki") continue;
            Add(b, "wrapN", tl.R396WrapHits); Add(b, "wrapAmt", tl.R396WrapAmt); Add(b, "wrapHp", tl.R396WrapHp);
            Add(b, "burst", tl.R396Bursts); Add(b, "burstAmt", tl.R396BurstAmt); Add(b, "burstDealt", tl.R396BurstDealt);
            Add(b, "bInt", tl.R396BurstInterrupt); Add(b, "bYield", tl.R396BurstYield); Add(b, "bGift", tl.R396BurstGift); Add(b, "bOver", tl.R396BurstOverflow); Add(b, "bOther", tl.R396BurstOther);
            Add(b, "swell", tl.R397SwellGain); Add(b, "maxHpPeak", tl.R397MaxHpPeak);
            Add(b, "melt", tl.R398MeltSaved); Add(b, "hardN", tl.R398HardenHits); Add(b, "hard", tl.R398HardenSaved);
            long store = 0;
            for (int t = 1; t < TT; t++)
            {
                long wr = tl.R396WrapByTurn is { } wa && t < wa.Length ? wa[t] : 0, bu = tl.R398BurstByTurn is { } ba && t < ba.Length ? ba[t] : 0;
                store += wr - bu;
                Add(b, $"wrap{t}", wr); Add(b, $"store{t}", store); Add(b, $"bu{t}", bu);
                Add(b, $"wHp{t}", tl.R398WrapHpByTurn is { } ha && t < ha.Length ? ha[t] : 0);
            }
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
    static Formation Swap2(Formation f, UnitDef sekki, UnitDef dolga)
    {
        var g = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == "sekki" ? sekki : d.Id == "dolga" ? dolga : d;
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
        var rows = CB398();
        int nw = EnemyCatalog.Stages.Count;
        var now = CGrid(rows, CompareSeeds);
        var bg = CGrid(rows.Select(r => (r.Name, Swap2(r.F, UnitCatalog.SekkiB, UnitCatalog.DolgaG12))).ToArray(), CompareSeeds);
        var oldRows = rows.Select(r => (r.Name, Pin397(r.F))).ToArray();
        var old = CGrid(oldRows, CompareSeeds);
        var map = S390.BalanceMap(S390.GitShow($"{HeadSha}:docs/balance.md"));
        int moved = 0, up = 0, same = 0, cells = 0, oldSame = 0; double sum = 0;
        var movedRows = new List<string>();
        for (int ri = 0; ri < rows.Length; ri++)
        {
            var cc = map[rows[ri].Name]; var parts = new List<string>();
            for (int w = 0; w < nw; w++)
            {
                cells++;
                if (now[ri, w] == bg[ri, w]) same++;
                double o = double.Parse(cc[w].TrimEnd('%'));
                if (o == old[ri, w]) oldSame++;
                if (o != now[ri, w]) { moved++; if (now[ri, w] > o) up++; sum += now[ri, w] - o; parts.Add($"第{w + 1}波 {F1(o)} → {F1(now[ri, w])}"); }
            }
            if (parts.Count > 0) movedRows.Add($"| {rows[ri].Name} | {string.Join("・", parts)} |");
        }
        Console.WriteLine($"# {Tag} §2 段0: セッキの規定 ＝ 第397期の B ／ ドルガの規定 ＝ 第397期の G12");
        Console.WriteLine();
        Console.WriteLine($"## 1. `compare`（全 {rows.Length} 行 × {nw} 波 × seed 0..{CompareSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"- 第397期の HEAD（{HeadSha}）の `docs/balance.md` と違うセル **{moved}**（上がり {up} ／ 落ち {moved - up}・差の合計 {Sg(sum)}pt）");
        Console.WriteLine($"- 第397期の B ＋ G12 の列（セッキを `SekkiB`・ドルガを `DolgaG12` に替えた全行）と同じセル {same} ／ {cells}・旧（`SekkiH397` ／ `DolgaH397`）に戻した全行が HEAD の表と同じセル {oldSame} ／ {cells}");
        Console.WriteLine();
        Console.WriteLine("| 行 | 動いたセル |");
        Console.WriteLine("|---|---|");
        foreach (var l in movedRows) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine("### (G2) 旧の規定から −10.0pt 以上落ちた行（分母 `compare` 全 66 行・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 落ちた行 | 落ち（波）| 駒ごとの他の行の平均の変化（行数）| 判定 |");
        Console.WriteLine("|---|---|---|---|---|");
        G2Split("規定（B ＋ G12）", rows, old, now, Enumerable.Range(0, rows.Length).Where(ri => Has(rows[ri].F, "sekki") || Has(rows[ri].F, "dolga")));
        Console.WriteLine("（行が無ければ −10pt 以上落ちた行は無い）");
        Console.WriteLine();

        var boards = new (string Name, Formation F)[] { ("試遊・手番 芯3枚", Pin398(Presets.TempoLamp393())), ("試遊・手番 包み", Pin398(Presets.SlimeLamp396())), ("試遊・手番 縛め", Pin398(Presets.BindTempo395())), ("試遊・手番 芯4枚", Pin398(Presets.FourCore397())) };
        var ws = new[] { WBoss, WGuard, WBat, WM5, WStrong };
        Console.WriteLine($"## 2. 試遊の 4 台 × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件（seed 0..{Seeds - 1}）・規定 ／ 第397期（セッキ ／ ドルガを旧に）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", ws.Select(w => W8[w].Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(ws.Select(_ => "---|")));
        foreach (var bd in boards)
        {
            foreach (var (cn, f) in new[] { ("規定（第398期）", bd.F), ("第397期（旧）", Pin397(bd.F)) })
            {
                var g = new S377.B[ws.Length]; Parallel.For(0, ws.Length, k => g[k] = S377.Measure(f, W8[ws[k]].Ws, Seeds, Extra));
                Console.WriteLine($"| {(cn.StartsWith("規定") ? $"{bd.Name}（{Seats(bd.F)}）" : "")} | {cn} | {string.Join(" | ", g.Select(x => x.Cell))} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3 ＋ §5-1 の 1・2: 台 × 列
    // ---------------------------------------------------------------------------------
    /// <summary>セッキのいない台では、列ごとに 5 席を全部試し、ボスの勝率 → 倒れた数（少ない方）→ ボス戦の長さ → 勇者への与ダメで最良の席（ボス・seed 0..99）。</summary>
    static int BestSeat(Formation f, UnitDef d, out string table)
    {
        var seats = Seats5(f);
        var res = seats.Select(s => { var x = Put(f, s, d); var bo = S377.Measure(x, W8[WBoss].Ws, Seeds, Extra); return (S: s, Boss: bo.Win, Fall: (double)bo.FormDeaths / bo.N, T: (double)bo.Turns / bo.N, Hd: (double)X(bo, "heroDmg") / bo.N); }).ToArray();
        // ボスの勝率 → 倒れた数（少ない方）→ 同点ならボス戦の長さ（長い方）→ 勇者への与ダメ（多い方）→ 席番号
        int best = res.OrderByDescending(r => r.Boss).ThenBy(r => r.Fall).ThenByDescending(r => r.T).ThenByDescending(r => r.Hd).ThenBy(r => r.S).First().S;
        table = string.Join("・", res.Select(r => $"{SeatName(r.S)}（{Short(f[r.S]!)}）{F1(r.Boss)}% 倒 {F1(r.Fall)}・{F1(r.T)}T・勇者へ {F0(r.Hd)}"));
        return best;
    }

    static void Boards()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = BoardList();
        var cols = Cols;
        int nb = boards.Length, nc = cols.Length, iPlain = nc - 1;
        var seat = new int[nb, nc]; var seatTab = new string[nb, nc];
        var orig = new S377.B[nb][];
        var g = new S377.B[nb, nc][];
        for (int bi = 0; bi < nb; bi++)
        {
            var f = boards[bi].F;
            if (!Has(f, "sekki")) orig[bi] = M8(f);
            for (int ci = 0; ci < nc; ci++)
            {
                // 席は規定のセッキで選び、版 ／ 素体も同じ席に置く（版ごとの最良の席は参考に出す）
                if (Has(f, "sekki")) { seat[bi, ci] = SlotOf(f, "sekki"); seatTab[bi, ci] = ""; }
                else if (ci == iPlain) { seat[bi, ci] = seat[bi, 0]; seatTab[bi, ci] = "規定の席"; }
                else { int bs = BestSeat(f, cols[ci].D, out seatTab[bi, ci]); seat[bi, ci] = ci == 0 ? bs : seat[bi, 0]; if (ci > 0) seatTab[bi, ci] = $"（この版だけの最良は {SeatName(bs)}）" + seatTab[bi, ci]; }
                g[bi, ci] = M8(Put(f, seat[bi, ci], cols[ci].D));
            }
        }

        var hero = EnemyCatalog.BossRegular;
        Console.WriteLine($"# {Tag} §3 ／ §5-1 の 1・2 台 {nb} × 列（{string.Join(" ／ ", cols.Select(c => c.Name))}）× 8 物差し（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("## 0. 勇者の規則（コードから・`EnemyCatalog.BossRegular` ＝ 測ったボスの波 `Inv348Diag.Boss` の唯一の敵）");
        Console.WriteLine();
        Console.WriteLine($"- 名前「{hero.Name}」・最大 HP {hero.MaxHp}・攻 {hero.Attack}・速 {hero.Speed}・型 {hero.Pattern}（毎手番、味方全員に同じ量）・札 {string.Join("・", hero.Traits)}");
        Console.WriteLine($"- 攻撃力はターン t で {hero.Attack} ＋ {BossRiseTrait.Calc}×(t−1)（`BossRiseTrait.ModifyAttack`・積まずに再計算）: " + string.Join(" ／ ", Enumerable.Range(1, TT - 1).Select(t => $"T{t} {hero.Attack + BossRiseTrait.Calc * (t - 1)}")));
        Console.WriteLine($"- ターンの頭に最大 HP の 40%（{hero.MaxHp * 40 / 100}）癒える（`BossMend40`）・毒の層 × {VenomTaxTrait.Seal} だけ塞がりきらない（`VenomTaxSeal`）・手番を奪う状態が付かない（`BossSteadfast`）");
        Console.WriteLine($"- 本文: 「{hero.PlusText}」");
        Console.WriteLine();
        foreach (var (name, f, burn) in boards) Console.WriteLine($"- **{name}**: {Seats(f)}（{BurnNote(f)}）");
        Console.WriteLine();
        Console.WriteLine("### セッキの席（セッキのいない台・規定のセッキで 5 席: ボスの勝率・倒れた数・ボス戦の長さ・勇者への与ダメで選び、版 ／ 素体も同じ席・版ごとの 5 席は参考）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
            if (!Has(boards[bi].F, "sekki"))
                for (int ci = 0; ci < nc - 1; ci++) Console.WriteLine($"- {boards[bi].Name} × {cols[ci].Name}: **{SeatName(seat[bi, ci])}**（{Short(boards[bi].F[seat[bi, ci]]!)} の代わり）— {seatTab[bi, ci]}");
        Console.WriteLine();

        Console.WriteLine("## 1. 勝率・決着T（ボスの列はボスの倒しT）・倒れた編成の駒 ／ 4 波 ／ 規定との差（4 波 ／ ボス ／ 強）／ 素体との差（同じ席）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列（席）| " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 | 規定との差 | 素体との差 |");
        Console.WriteLine("|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|---|---|");
        for (int bi = 0; bi < nb; bi++)
        {
            if (orig[bi] is { } o) Console.WriteLine($"| {boards[bi].Name} | 元（セッキなし）| {string.Join(" | ", o.Select(x => x.Cell))} | {F1(A4(o))} | — | — |");
            for (int ci = 0; ci < nc; ci++)
            {
                var a = g[bi, ci]; var d0 = g[bi, 0]; var pl = g[bi, iPlain];
                Console.WriteLine($"| {(orig[bi] is null && ci == 0 ? boards[bi].Name : "")} | {cols[ci].Name}（{SeatName(seat[bi, ci])}）| {string.Join(" | ", a.Select(x => x.Cell))} | {F1(A4(a))} | {(ci == 0 || ci == iPlain ? "—" : $"{Sg(D4(a, d0))} ／ {Sg(a[WBoss].Win - d0[WBoss].Win)} ／ {Sg(a[WStrong].Win - d0[WStrong].Win)}")} | {(ci == iPlain ? "—" : $"{Sg(D4(a, pl))} ／ {Sg(a[WBoss].Win - pl[WBoss].Win)}")} |");
            }
        }
        Console.WriteLine();

        // ---- ボス戦の数え（§3 の 2〜5 を版ごとに）----
        void PerTurn(string title, Func<S377.B, int, string> cell, bool withOrig = false)
        {
            Console.WriteLine(title);
            Console.WriteLine();
            Console.WriteLine("| 台 | 列 | " + string.Join(" | ", Enumerable.Range(1, TT - 1).Select(t => $"T{t}")) + " |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(1, TT - 1).Select(_ => "--:|")));
            for (int bi = 0; bi < nb; bi++)
            {
                if (withOrig && orig[bi] is { } o) Console.WriteLine($"| {boards[bi].Name} | 元 | {string.Join(" | ", Enumerable.Range(1, TT - 1).Select(t => cell(o[WBoss], t)))} |");
                for (int ci = 0; ci < nc; ci++)
                    Console.WriteLine($"| {(ci == 0 && !(withOrig && orig[bi] is not null) ? boards[bi].Name : "")} | {cols[ci].Name} | {string.Join(" | ", Enumerable.Range(1, TT - 1).Select(t => cell(g[bi, ci][WBoss], t)))} |");
            }
            Console.WriteLine();
        }
        Console.WriteLine("## 2. ボス戦（1戦の平均・T1〜T8）");
        Console.WriteLine();
        PerTurn("### 2-1. 勇者の攻撃力（その戦の最初の振り・振った戦の平均）／ 味方1体に届いた量（包みの前 ＝ (セッキ以外の味方が HP に受けた量 ＋ セッキが吸った量) ÷ セッキ以外の味方が HP に受けた回数・0 の一撃は回数に入らないので目安）", (x, t) => X(x, $"hAtkN{t}") == 0 ? "—" : $"{F0((double)X(x, $"hAtk{t}") / X(x, $"hAtkN{t}"))} ／ {(X(x, $"aHits{t}") == 0 ? "—" : F0((double)(X(x, $"aGot{t}") + X(x, $"wrap{t}")) / X(x, $"aHits{t}")))}", withOrig: true);
        PerTurn("### 2-2. セッキ以外の味方が受けた量（包みの後・1体あたり）", (x, t) => X(x, $"aHits{t}") == 0 ? "—" : F0((double)X(x, $"aGot{t}") / X(x, $"aHits{t}")), withOrig: true);
        PerTurn("### 2-3. セッキが吸った量（1戦・そのターンの合計）", (x, t) => Per(X(x, $"wrap{t}"), x.N));
        PerTurn("### 2-4. セッキが包みで HP に受けた量 ／ セッキが勇者から受けた合計（包み ＋ 自分への一撃・1戦）", (x, t) => $"{Per(X(x, $"wHp{t}"), x.N)} ／ {Per(X(x, $"sTk{t}"), x.N)}");
        PerTurn("### 2-5. 帳簿（ターンの終わり・1戦）／ そのターンに弾き返した量", (x, t) => $"{Per(X(x, $"store{t}"), x.N)} ／ {Per(X(x, $"bu{t}"), x.N)}");
        PerTurn("### 2-6. 立っている編成の駒（ターンの終わり・決着の後は勝ちなら生き残り ／ 負けなら 0）", (x, t) => Per(X(x, $"alive{t}"), x.N), withOrig: true);
        PerTurn("### 2-7. ドルガの振り（1戦・そのターンの回数）／ 勇者への一撃（1戦・そのターンの量）", (x, t) => $"{Per(X(x, $"dSw{t}"), x.N)} ／ {F0((double)X(x, $"dHeroT{t}") / x.N)}", withOrig: true);

        Console.WriteLine("## 3. セッキが倒れたT ／ その後の隊の崩れ ／ T6 に立っている数（ボス・1戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 勝ち | セッキが倒れた %（T）| 倒れない % | セッキより先に倒れた味方 | 倒れた同じT ／ +1T ／ +2T ／ +3T までに倒れた味方（累計）| T6 の頭に立つ数 ／ 2体以上 % ／ 3体以上 % |");
        Console.WriteLine("|---|---|--:|---|--:|--:|---|---|");
        for (int bi = 0; bi < nb; bi++)
        {
            if (orig[bi] is { } o) { var x = o[WBoss]; Console.WriteLine($"| {boards[bi].Name} | 元 | {F1(x.Win)} | — | — | — | — | {Per(X(x, "st6"), x.N)} ／ {Pc(X(x, "st6ge2"), x.N)} ／ {Pc(X(x, "st6ge3"), x.N)} |"); }
            for (int ci = 0; ci < nc; ci++)
            {
                var x = g[bi, ci][WBoss]; long sf = X(x, "sFellN");
                Console.WriteLine($"| {(ci == 0 && orig[bi] is null ? boards[bi].Name : "")} | {cols[ci].Name} | {F1(x.Win)} | {(ci == iPlain ? "—" : $"{Pc(sf, X(x, "sIn"))}（T{Per(X(x, "sFellT"), sf)}）")} | {(ci == iPlain ? "—" : Pc(X(x, "sNever"), X(x, "sIn")))} | {(ci == iPlain ? "—" : Per(X(x, "beforeS"), sf))} | {(ci == iPlain ? "—" : string.Join(" ／ ", Enumerable.Range(0, 4).Select(k => Per(X(x, $"after{k}"), sf))))} | {Per(X(x, "st6"), x.N)} ／ {Pc(X(x, "st6ge2"), x.N)} ／ {Pc(X(x, "st6ge3"), x.N)} |");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 4. 勇者に止めを刺したのは誰か（勇者が倒れた戦の %）・勇者へのダメージの内訳（誰から %・1戦の合計）・ドルガの振り（1戦）・T6 のドルガの最初の振りの攻撃力（255 ＝ 2 振り × (攻 × 5 ＋ 150) ＋ ガンの一撃でおおむね 3000 に届く線・ログ 8 戦から読んだ目安）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 勇者が倒れた %（T）| 止め | 勇者への与ダメ（内訳）| 合計 | ドルガ: 振り ／ 勇者へ（1撃）| T6 のドルガ: 振った % ／ 攻撃力 ／ 255 以上 % |");
        Console.WriteLine("|---|---|---|---|---|--:|---|---|");
        string Mix(S377.B x, string pre, long den) => string.Join("・", x.X.Where(kv => kv.Key.StartsWith(pre, StringComparison.Ordinal)).OrderByDescending(kv => kv.Value).Select(kv => $"{Nm(kv.Key[pre.Length..])} {Pc(kv.Value, den)}"));
        for (int bi = 0; bi < nb; bi++)
        {
            var rowsX = new List<(string Col, S377.B X)>();
            if (orig[bi] is { } o) rowsX.Add(("元", o[WBoss]));
            for (int ci = 0; ci < nc; ci++) rowsX.Add((cols[ci].Name, g[bi, ci][WBoss]));
            for (int k = 0; k < rowsX.Count; k++)
            {
                var (cn, x) = rowsX[k]; long hd = X(x, "heroDead"), dm = X(x, "heroDmg"), hh = X(x, "dHeroHits");
                Console.WriteLine($"| {(k == 0 ? boards[bi].Name : "")} | {cn} | {Pc(hd, x.N)}%（T{Per(X(x, "heroDeadT"), hd)}）| {(hd == 0 ? "—" : Mix(x, "hkBy:", hd))} | {Mix(x, "hdBy:", dm)} | {Per(dm, x.N)} | {Per(X(x, "dSwing"), x.N)} ／ {(hh == 0 ? "—" : F0((double)X(x, "dHeroDmg") / hh))} | {Pc(X(x, "dAtk6N"), x.N)} ／ {(X(x, "dAtk6N") == 0 ? "—" : F0((double)X(x, "dAtk6") / X(x, "dAtk6N")))} ／ {Pc(X(x, "dAtk6ge255"), X(x, "dAtk6N"))} |");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 5. 包む ／ 弾き返す ／ 固まる ／ 消す（1戦・ボス ／ 近衛 ／ 大隊 ／ 本編 第5波 ／ 強い条件）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | 物差し | 包む ／ 吸った | HP で受けた（包み）| 消した（K1）| 固まって軽くした 回数 ／ 量（K2）| 最大 HP | 倒れた %（T）／ 倒れない % | 弾き返し 回数 ／ 1回 ／ 与えた | きっかけ（叩き起こし等 ／ 譲り ／ ギフト ／ 溢れ ／ ほか）|");
        Console.WriteLine("|---|---|---|---|--:|--:|---|--:|---|---|---|");
        foreach (var (bi, bd) in boards.Select((b, i) => (i, b)))
            for (int ci = 0; ci < nc - 1; ci++)
                foreach (int w in new[] { WBoss, WGuard, WBat, WM5, WStrong })
                {
                    var x = g[bi, ci][w]; long n = X(x, "burst"), sf = X(x, "sFellN");
                    Console.WriteLine($"| {(ci == 0 && w == WBoss ? bd.Name : "")} | {(w == WBoss ? cols[ci].Name : "")} | {W8[w].Name} | {Per(X(x, "wrapN"), x.N)} ／ {Per(X(x, "wrapAmt"), x.N)} | {Per(X(x, "wrapHp"), x.N)} | {Per(X(x, "melt"), x.N)} | {Per(X(x, "hardN"), x.N)} ／ {Per(X(x, "hard"), x.N)} | {(X(x, "maxHpPeak") == 0 ? "70" : Per(X(x, "maxHpPeak"), x.N))} | {Pc(sf, X(x, "sIn"))}（T{Per(X(x, "sFellT"), sf)}）／ {Pc(X(x, "sNever"), X(x, "sIn"))} | {Per(n, x.N)} ／ {(n == 0 ? "—" : F0((double)X(x, "burstAmt") / n))} ／ {Per(X(x, "burstDealt"), x.N)} | {Per(X(x, "bInt"), x.N)} ／ {Per(X(x, "bYield"), x.N)} ／ {Per(X(x, "bGift"), x.N)} ／ {Per(X(x, "bOver"), x.N)} ／ {Per(X(x, "bOther"), x.N)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 6. 誰が何Tに倒れたか（規定 ／ ボスの勝率がいちばん良い版・ボス ／ 近衛 ／ 大隊 ／ 強い条件）");
        Console.WriteLine();
        for (int bi = 0; bi < nb; bi++)
        {
            int best = Enumerable.Range(1, nc - 2).OrderByDescending(ci => g[bi, ci][WBoss].Win).ThenByDescending(ci => A4(g[bi, ci])).First();
            Console.WriteLine($"- **{boards[bi].Name}**（ボスがいちばん良い版 {cols[best].Name}・ボス {F1(g[bi, best][WBoss].Win)} ／ 規定 {F1(g[bi, 0][WBoss].Win)}）");
            foreach (int w in new[] { WBoss, WGuard, WBat, WStrong })
                foreach (int ci in new[] { 0, best })
                    Console.WriteLine($"  - {W8[w].Name}: {cols[ci].Name}（{SeatName(seat[bi, ci])}）{F1(g[bi, ci][w].Win)}%・{g[bi, ci][w].WinTs}T — {g[bi, ci][w].FellCell}");
        }
        Console.WriteLine();

        // ---- 試遊の台に足す台: 燃焼の駒がいない台 × 版（K1 ／ K2 ／ K12）でボスにいちばん勝った組 ----
        var pick = Enumerable.Range(0, nb).Where(bi => !boards[bi].Burn).SelectMany(bi => Enumerable.Range(1, nc - 2).Select(ci => (bi, ci)))
            .OrderByDescending(p => g[p.bi, p.ci][WBoss].Win).ThenByDescending(p => A4(g[p.bi, p.ci])).ThenBy(p => p.bi).ThenBy(p => p.ci).First();
        Console.WriteLine($"## 7. 試遊の台「版・耐える セッキ」の候補: 燃焼の駒がいない台 × 版でボスにいちばん勝った組 ＝ **{boards[pick.bi].Name} × {cols[pick.ci].Name}（{SeatName(seat[pick.bi, pick.ci])}）**・ボス {F1(g[pick.bi, pick.ci][WBoss].Win)}%（規定 {F1(g[pick.bi, 0][WBoss].Win)}%）・4 波 {F1(A4(g[pick.bi, pick.ci]))}");
        Console.WriteLine($"- 台: {Seats(Put(boards[pick.bi].F, seat[pick.bi, pick.ci], cols[pick.ci].D))}");
        Console.WriteLine();

        Signs(boards.Select((bd, bi) => (bd.Name, g[bi, 0], Enumerable.Range(1, nc - 2).Select(ci => (cols[ci].Name, g[bi, ci])).ToArray())).ToArray());
        Console.WriteLine($"- 帳簿の最大 {_storePeak}・1回の弾き返しの最大 {_burstPeak}・セッキの最大 HP の最大 {(_maxHpPeak == 0 ? 70 : _maxHpPeak)}・味方の一撃の最大 {_hitPeak}（全台 × 全列 × 全物差しの1戦・int のあふれまで遠い）");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上（規定との差）／ あふれ ／ セッキが倒れない戦 50% 以上）。</summary>
    static void Signs((string Name, S377.B[] Orig, (string Col, S377.B[] M)[] Cells)[] items)
    {
        Console.WriteLine("## 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ ボス 4T 未満 ／ 30T の打ち切り +5pt 以上（規定との差）／ あふれ ／ セッキが倒れない戦（規定より +20pt 以上））");
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
                    long si = X(x, "sIn"), si0 = X(x0, "sIn");
                    if (si > 0 && si0 > 0 && 100.0 * X(x, "sNever") / si - 100.0 * X(x0, "sNever") / si0 >= 20) signs.Add($"{at} × {W8[w].Name}: セッキが倒れない戦 {Pc(X(x0, "sNever"), si0)} → {Pc(X(x, "sNever"), si)}%");
                }
            }
        if (signs.Count == 0) Console.WriteLine("- 無し。");
        foreach (var s in signs.Take(60)) Console.WriteLine("- " + s);
        if (signs.Count > 60) Console.WriteLine($"- ほか {signs.Count - 60} 件");
    }

    // ---------------------------------------------------------------------------------
    // §5-1 の 3: 寄せ集め
    // ---------------------------------------------------------------------------------
    internal static (string Name, Formation F)[] Play() => VersionsH398().Where(r => r.Name.StartsWith("試遊・", StringComparison.Ordinal) && !Has(r.F, "sekki")).ToArray();

    static void SMix()
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
        S377.B[] M(int b, int s, UnitDef? d, string key) { string k = $"{b}:{s}:{key}"; lock (cache) if (cache.TryGetValue(k, out var x)) return x; var y = M8(d is null ? boards[b].F : Put(boards[b].F, s, d), MixSeeds); lock (cache) cache[k] = y; return y; }

        Console.WriteLine($"# {Tag} §5-1 の 3 寄せ集め —— 試遊の台 {nb}（セッキのいない台）× アタッカー以外の席の最良にセッキ（{string.Join(" ／ ", Vers.Select(v => v.Name))}・第396〜397期と同じやり方・段2 は seed 0..{MixSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 台（アタッカー）| 元 4 波 ／ 強 | " + string.Join(" | ", Vers.Select(v => $"{v.Name}: 席（誰を）4 波の差 ／ 強の差")) + " | 素体（規定の席）|");
        Console.WriteLine("|---|---|" + string.Concat(Vers.Select(_ => "---|")) + "---|");
        var lists = Vers.Select(_ => new List<(int B, int S, double D4, double DS, S377.B[] A)>()).ToArray();
        var plainList = new List<(double D4, double DS)>();
        for (int b = 0; b < nb; b++)
        {
            var b0 = M(b, -1, null, "orig");
            var cells = new List<string>(); int defSeat = -1;
            for (int v = 0; v < Vers.Length; v++)
            {
                var seats = Seats5(boards[b].F).Where(s => s != atk[b]).OrderByDescending(s => Enumerable.Range(0, 4).Average(w => grid[v, b, s]![w] - bs[b][w])).ThenByDescending(s => grid[v, b, s]![4] - bs[b][4]).ThenBy(s => s).ToArray();
                if (seats.Length == 0) { cells.Add("—"); continue; }
                int s0 = seats[0]; var a = M(b, s0, Vers[v].D, Vers[v].Name);
                double d4 = D4(a, b0), ds = a[WStrong].Win - b0[WStrong].Win;
                lists[v].Add((b, s0, d4, ds, a));
                if (v == 0) defSeat = s0;
                cells.Add($"{SeatName(s0)}（{Short(boards[b].F[s0]!)}）{Sg(d4)} ／ {Sg(ds)}");
            }
            string pls = "—";
            if (defSeat >= 0) { var p = M(b, defSeat, PlainSekki, "plain"); double pd4 = D4(p, b0), pds = p[WStrong].Win - b0[WStrong].Win; plainList.Add((pd4, pds)); pls = $"{Sg(pd4)} ／ {Sg(pds)}"; }
            Console.WriteLine($"| {boards[b].Name}（{SeatName(atk[b])} {Short(boards[b].F[atk[b]]!)}）| {F1(A4(b0))} ／ {F1(b0[WStrong].Win)} | {string.Join(" | ", cells)} | {pls} |");
        }
        Console.WriteLine();
        for (int v = 0; v < Vers.Length; v++)
        {
            var l = lists[v];
            long sIn = l.Sum(x => x.A.Sum(w => X(w, "sIn"))), sNev = l.Sum(x => x.A.Sum(w => X(w, "sNever")));
            Console.WriteLine($"- **{Vers[v].Name}**: 4 波で +{Up:F0}pt 以上上がった台 **{l.Count(x => x.D4 >= Up)}** ／ {l.Count}・強い条件で +{Up:F0}pt 以上 **{l.Count(x => x.DS >= Up)}**・4 波で −{Up:F0}pt 以上下がった台 {l.Count(x => x.D4 <= -Up)}・4 波の差の平均 {Sg(l.Average(x => x.D4))}・強の差の平均 {Sg(l.Average(x => x.DS))}・セッキが倒れない戦 {Pc(sNev, sIn)}%（全台 × 8 物差し）");
        }
        if (plainList.Count > 0) Console.WriteLine($"- 素体（規定の席）: 4 波で +{Up:F0}pt 以上 {plainList.Count(x => x.D4 >= Up)} ／ {plainList.Count}・強い条件で +{Up:F0}pt 以上 {plainList.Count(x => x.DS >= Up)}・4 波の差の平均 {Sg(plainList.Average(x => x.D4))}・強の差の平均 {Sg(plainList.Average(x => x.DS))}");
        Console.WriteLine();
        Console.WriteLine("### 版と規定の差（同じ台・それぞれの最良の席）");
        Console.WriteLine();
        for (int v = 1; v < Vers.Length; v++)
        {
            var d = lists[v].Join(lists[0], x => x.B, y => y.B, (x, y) => (D4: x.D4 - y.D4, DS: x.DS - y.DS)).ToList();
            Console.WriteLine($"- {Vers[v].Name} − 規定: 4 波で +{Up:F0}pt 以上 {d.Count(x => x.D4 >= Up)}・−{Up:F0}pt 以上 {d.Count(x => x.D4 <= -Up)}・強で +{Up:F0}pt 以上 {d.Count(x => x.DS >= Up)}・−{Up:F0}pt 以上 {d.Count(x => x.DS <= -Up)}・平均 {Sg(d.Average(x => x.D4))} ／ {Sg(d.Average(x => x.DS))}");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §5-1 の 4: `compare` ＋ (G2)
    // ---------------------------------------------------------------------------------
    static void Cmp()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var all = CB398();
        int nw = EnemyCatalog.Stages.Count;
        Console.WriteLine($"# {Tag} §5-1 の 4 `compare` 全 {all.Length} 行 × セッキの版（第1〜5波・seed 0..{CompareSeeds - 1}）");
        Console.WriteLine();
        var grids = Vers.Select(v => CGrid(all.Select(r => (r.Name, Has(r.F, "sekki") ? I.Swap(r.F, "sekki", v.D) : r.F)).ToArray(), CompareSeeds)).ToArray();
        var rows = Enumerable.Range(0, all.Length).Where(ri => Has(all[ri].F, "sekki")).ToArray();
        Console.WriteLine($"## セッキの版（セッキのいる {rows.Length} 行・セッキだけ替える）");
        Console.WriteLine();
        Console.WriteLine("| 行 | " + string.Join(" | ", Vers.Select(c => c.Name + " 第1〜5波")) + " |");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "---|")));
        foreach (int ri in rows) Console.WriteLine($"| {all[ri].Name} | {string.Join(" | ", grids.Select(g => string.Join(" ／ ", Enumerable.Range(0, nw).Select(w => F1(g[ri, w])))))} |");
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
            Console.WriteLine($"- {Vers[c].Name}: 規定と違うセル {moved}（上がり {up} ／ 落ち {moved - up}・差の合計 {Sg(sum)}pt）・**セッキのいない行で動いたセル {movedOther}**（0 であること）");
        }
        Console.WriteLine();
        Console.WriteLine("### (G2) 規定から −10.0pt 以上落ちた行（分母 `compare` 全 66 行・第2〜5波）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 落ちた行 | 落ち（波）| 駒ごとの他の行の平均の変化（行数）| 判定 |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int c = 1; c < Vers.Length; c++) G2Split(Vers[c].Name, all, grids[0], grids[c], rows);
        Console.WriteLine("（行が無ければ −10pt 以上落ちた行は無い）");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Log1(string[] args)
    {
        string bn = args.Length > 3 ? args[3] : "試遊・手番 芯4枚";
        string cn = args.Length > 4 ? args[4] : "K1";
        int wi = args.Length > 5 && int.TryParse(args[5], out int w0) ? Math.Clamp(w0, 0, W8.Length - 1) : 0, seed = args.Length > 6 && int.TryParse(args[6], out int s0) ? s0 : 0;
        int slotArg = args.Length > 7 && int.TryParse(args[7], out int sl) ? sl : -1;
        var b = BoardList().FirstOrDefault(x => x.Name.StartsWith(bn, StringComparison.Ordinal));
        if (b.F is null) { Console.WriteLine($"台が無い: {bn}"); return; }
        var c = Cols.FirstOrDefault(x => x.Name == cn); if (c.D is null) c = Cols[0];
        int slot = Has(b.F, "sekki") ? SlotOf(b.F, "sekki") : slotArg >= 0 ? slotArg : 2;
        var (r, _, _) = I.Raw(Put(b.F, slot, c.D), W8[wi].Ws[0], seed, verbose: true);
        Console.WriteLine($"# {b.Name}（{SeatName(slot)} を {c.Name}）× {W8[wi].Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
