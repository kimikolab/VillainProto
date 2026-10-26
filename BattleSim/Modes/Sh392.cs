using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;
using S377 = Sh377Diag;
using S390 = Sh390Diag;

// =====================================================================================
// sh392 —— 第392期【軽い期】段0: ハギの P1h を規定に ／ 本体: 手番の軸の棚卸し（トモ ／ ドルガ ／ ガンほか・数えるだけ）。
// 指示書は design/PHASE392_TEMPO_INVENTORY_SPEC.md、報告は design/PHASE392_TEMPO_INVENTORY.md。
//
//     dotnet run --project BattleSim -c Release 0 sh392 zero      # §2: 段0 の `compare` の動き（第391期の HEAD の balance と比べる）／ ハギの台 3 × 規定 ／ 旧 × 5 物差し
//     dotnet run --project BattleSim -c Release 0 sh392 flow      # §3-2 ／ 3-3: 手番の軸の行 × 本編 第5波 ／ 近衛 ／ 大隊 ／ ボス（seed 0..19）——倒れた T・動かなかった手番・トモの灯・ドルガ・ガン
//     dotnet run --project BattleSim -c Release 0 sh392 plain     # §3-4: 手番の軸の行 × 手番の軸の駒を同じ席の素体に替えた差（本編 第2〜5波 ／ 近衛 ／ 大隊 ／ ボス・seed 0..99）
//     dotnet run --project BattleSim -c Release 0 sh392 search    # §3-5: 芯の組（トモ ＋ ドルガ ／ ガン ＋ ドルガ ／ トモ ＋ ガン ＋ ドルガ）＋ 残りの枠の席の探索 → 上位 5 台 × 8 物差し・他の軸の試遊の台
//     dotnet run --project BattleSim -c Release 0 sh392 log <行の頭> <物差し 0..3> <seed>     # 1戦のログ（物差し ＝ 第5波 ／ 近衛 ／ 大隊 ／ ボス）
//     dotnet run --project BattleSim -c Release 0 sh392 check     # 自己検査
//
// **手番の軸の駒の規則は1つも変えない**（版も作らない）。素体 ／ 探索の台は器具の中だけ（試遊の台に足す「試遊・手番 探索1位」を除く）。
// **予測のファイルが無ければ測定を走らせない**（R397・`.tmp/p392/predict.md`）。
// =====================================================================================
static partial class Sh392Diag
{
    internal const string PredictFile = ".tmp/p392/predict.md";
    const string Tag = "第392期";
    const int Seeds = 100, CompareSeeds = 200, FlowSeeds = 20, TT = 9;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        if (mode is "zero" or "flow" or "plain" or "search" or "seek" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sh392: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "zero": Zero(); return;
            case "flow": Flow(); return;
            case "plain": Plain(); return;
            case "search": Search(); return;
            case "seek": Seek(); return;
            case "log": Log1(args); return;
            case "check": Check(); return;
            default: Console.WriteLine("sh392: モードは zero / flow / plain / search / log / check。"); return;
        }
    }

    static string F1(double x) => S307.F1(x);
    static string F0(double x) => double.IsNaN(x) ? "—" : x.ToString("#,0");
    static string Sg(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;0.0");
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Pc(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static string SeatName(int s) => s switch { 0 => "前1", 1 => "前3", 2 => "中央", 3 => "後1", 4 => "後3", _ => $"席{s}" };
    static string Seats(Formation f) => string.Join(" ／ ", Enumerable.Range(0, 5).Select(s => f[s] is { } d ? Short(d) : "—"));
    static string Nm(string id) => UnitCatalog.Everyone.FirstOrDefault(d => d.Id == id) is { } d ? Short(d) : id switch { "spore" => "胞子", "wight" => "亡者", _ => id };

    static readonly (string Name, I.W[] Ws)[] W8 = S377.VerWaves;
    static readonly I.W M3 = new("本編 第3波", () => BattleEngine.Materialize(EnemyCatalog.Stages[2].Enemy, BattleContext.EnemyTeam, EnemyScaleRule.Default), EnemyScaleRule.Default);
    /// <summary>§3-2 の物差し: 本編 第5波 ／ 近衛 ／ 大隊 ／ ボス。</summary>
    static readonly (string Name, I.W W)[] FlowWaves = { ("本編 第5波", I.M5), ("近衛", I.Guard), ("大隊", I.Bat), ("ボス", I.Boss) };
    /// <summary>§3-4 の物差し: 本編 第2〜5波 ／ 近衛 ／ 大隊 ／ ボス。</summary>
    static readonly (string Name, I.W W)[] PlainWaves = { ("第2波", I.M2), ("第3波", M3), ("第4波", I.M4), ("第5波", I.M5), ("近衛", I.Guard), ("大隊", I.Bat), ("ボス", I.Boss) };

    static bool Has(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static Formation Put(Formation f, int slot, UnitDef d) { var g = f.Clone(); g[slot] = d; return g; }

    /// <summary>手番の軸の芯の駒と、行に居れば素体に替えて数える駒（§3-4）。</summary>
    internal static readonly string[] TempoIds = { "tomo", "dolga", "gan", "ban", "kugu" };

    /// <summary>素体（同じ体・札なし・`Actions` なし・同じ攻撃の型・ふつうに殴る・器具の中だけ）。</summary>
    internal static UnitDef Plain(UnitDef d) => new()
    {
        Id = d.Id + "_plain392", Name = "素体の" + Short(d), MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances,
        Traits = Array.Empty<TraitId>(), Pattern = d.Pattern, PlusText = "", MinusText = "",
    };

    // ---------------------------------------------------------------------------------
    // 行（§3-2）
    // ---------------------------------------------------------------------------------
    internal static readonly string[] NamedRows = { "灯×薙ぎ (トモ×ドルガ)", "溜め改 (クグ×バン×ガン)", "縛め収入型 (クグ×バン×ガン)", "縛め非収入型 (クグ×速攻)", "追撃×据え (ハギ×バン)" };

    /// <summary>
    /// 手番の軸の行: 指示書の 5 行 ＋ ドルガ ／ ガンのいる `compare` の行のうち、近衛 ＋ 大隊の勝率（seed 0..19）がいちばん低い 4 行（同点は `compare` の並び）。
    /// </summary>
    internal static (string Name, Formation F)[] Rows()
    {
        var all = CompareBuilds().Select(r => (r.Name, F: Pin393(r.F))).ToArray();   // 第394期: トモは第393期までの規定に固定
        var named = NamedRows.Select(n => all.First(r => r.Name == n)).ToList();
        var pool = all.Where(r => !NamedRows.Contains(r.Name) && (Has(r.F, "dolga") || Has(r.F, "gan"))).ToArray();
        var el = new double[pool.Length];
        Parallel.For(0, pool.Length, i =>
        {
            int w = 0;
            foreach (var wv in new[] { I.Guard, I.Bat }) for (int s = 0; s < FlowSeeds; s++) if (I.Raw(pool[i].F, wv, s, census: false).R.PlayerWon) w++;
            el[i] = w;
        });
        var extra = Enumerable.Range(0, pool.Length).OrderBy(i => el[i]).ThenBy(i => Array.IndexOf(all, pool[i])).Take(4).Select(i => pool[i]);
        return named.Concat(extra).ToArray();
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数（`Sh377Diag.One` の `extra`）
    // ---------------------------------------------------------------------------------
    static void Add(S377.B b, string k, long v) { if (v != 0) b.X[k] = b.X.GetValueOrDefault(k) + v; }
    static long X(S377.B b, string k) => b.X.GetValueOrDefault(k);

    static readonly Action<BattleResult, List<UnitState>, List<UnitState>, S377.B> Extra = (r, p, e, b) =>
    {
        var form = p.ToDictionary(u => u.InstanceId);
        var tomo = p.FirstOrDefault(u => u.Def.Id == "tomo");
        var dolga = p.FirstOrDefault(u => u.Def.Id == "dolga");
        var gan = p.FirstOrDefault(u => u.Def.Id == "gan");
        int did = dolga?.InstanceId ?? -9, gid = gan?.InstanceId ?? -9;
        var owner = new int[TT + 1]; for (int i = 0; i <= TT; i++) owner[i] = -1;
        var deadAt = new Dictionary<int, int>();
        var dolgaActed = new bool[BattleEngine.MaxTurns + 2];
        var dolgaAlive = new bool[BattleEngine.MaxTurns + 2];
        bool dAlive = dolga is not null;
        foreach (var x in r.Events)
        {
            int t = Math.Clamp(x.Turn, 0, BattleEngine.MaxTurns + 1);
            int tgt = x.TargetId ?? -3, act = x.ActorId ?? -2;
            switch (x.Kind)
            {
                case BattleEventKind.TurnStart:
                    if (dAlive && t >= 1) dolgaAlive[t] = true;
                    break;
                case BattleEventKind.Whet:
                    if (x.WhetRoute == WhetRoute.Taillight && t is >= 1 and < TT && form.ContainsKey(tgt)) owner[t] = tgt;
                    if (x.WhetRoute == WhetRoute.RallyTurn && form.TryGetValue(tgt, out var ru)) { Add(b, "rally8", 1); Add(b, "rally8:" + ru.Def.Id, 1); }
                    if (x.WhetRoute == WhetRoute.RallyOpening) Add(b, "rally4", 1);
                    break;
                case BattleEventKind.Reveille:
                    if (form.TryGetValue(tgt, out var rv)) Add(b, $"rev:{x.Text}:{rv.Def.Id}", 1);
                    Add(b, $"rev:{x.Text}", 1);
                    break;
                case BattleEventKind.Attack:
                    if (act == did && !x.Reaction) { dolgaActed[t] = true; Add(b, "dAtk", 1); if (x.Pattern == AttackPattern.All) Add(b, "dAll", 1); }
                    if (act == did && x.Reaction) Add(b, "dWoken", 1);
                    break;
                case BattleEventKind.Damage:
                    if (act == did && !form.ContainsKey(tgt)) Add(b, "dDmg", x.Amount);
                    break;
                case BattleEventKind.StatSnapshot:
                    if (tgt == did && t is >= 1 and < TT) { Add(b, $"dBonus{t}", x.Amount - dolga!.Def.Attack); Add(b, $"dN{t}", 1); if (x.Pattern == AttackPattern.All) Add(b, $"dAllT{t}", 1); }
                    break;
                case BattleEventKind.Death:
                    if (form.ContainsKey(tgt) && !deadAt.ContainsKey(tgt)) deadAt[tgt] = t;
                    if (tgt == did) dAlive = false;
                    break;
                case BattleEventKind.Revive:
                    if (tgt == did) dAlive = true;
                    deadAt.Remove(tgt);
                    break;
            }
        }
        // ドルガ: 動いた ／ 休んだターン（生きて始まったターンのうち、自分の手番で振ったか）
        if (dolga is not null)
            for (int t = 1; t <= Math.Min(r.Turns, BattleEngine.MaxTurns); t++)
                if (dolgaAlive[t]) { Add(b, "dTurns", 1); if (dolgaActed[t]) Add(b, "dActT", 1); else Add(b, (t % 2 == 0) ? "dRestEven" : "dRestOdd", 1); }
        // トモ: ターンごとの灯の持ち主 ／ 消えた理由 ／ ドルガに続いた灯
        if (tomo is not null)
        {
            int streakD = 0, maxD = 0;
            for (int t = 1; t < TT; t++)
            {
                if (owner[t] >= 0) { Add(b, $"lit{t}", 1); if (owner[t] == did) Add(b, $"litD{t}", 1); Add(b, "litOn:" + form[owner[t]].Def.Id, 1); }
                if (owner[t] == did) { streakD++; maxD = Math.Max(maxD, streakD); } else streakD = 0;
                if (t >= 2 && owner[t - 1] >= 0 && owner[t] != owner[t - 1])
                {
                    int prev = owner[t - 1];
                    string why = deadAt.TryGetValue(prev, out int dt) && dt <= t ? "dead"
                               : prev == did && t % 2 == 0 ? "dolgaRest"
                               : owner[t] < 0 ? "none" : "other";
                    Add(b, "douse:" + why, 1);
                }
            }
            Add(b, "litMaxD", maxD);
            if (r.TallyByUnit.TryGetValue("tomo", out var tt))
            {
                Add(b, "tomoPeak", tt.TaillightPeak); Add(b, "tomoYields", tt.TaillightYields); Add(b, "tomoNoDeath", tt.TaillightNoDeath); Add(b, "tomoNoTarget", tt.TaillightNoTarget);
                Add(b, "tomoIdle", tt.TaillightIdle); Add(b, "tomoFires", tt.TaillightFires);
            }
        }
        if (gan is not null && r.TallyByUnit.TryGetValue("gan", out var gt)) { Add(b, "revFires", gt.ReveilleFires); Add(b, "revMiss", gt.ReveilleMisses); }
        // 動かなかった手番（駒ごと）
        foreach (var u in p.Where(u => u.Slot < 5))
            if (r.TallyByUnit.TryGetValue(u.Def.Id, out var ut))
            {
                Add(b, "stall:" + u.Def.Id, ut.TurnStalls); Add(b, "sur:" + u.Def.Id, ut.TurnsSurrendered);
                Add(b, "stun:" + u.Def.Id, ut.StallStun); Add(b, "canact:" + u.Def.Id, ut.StallCanAct); Add(b, "immob:" + u.Def.Id, ut.StallImmobile + ut.StallSlumber);
            }
    };

    static S377.B MeasureW(Formation f, I.W w, int seeds) => S377.Measure(f, new[] { w }, seeds, Extra);

    // ---------------------------------------------------------------------------------
    // §2 段0
    // ---------------------------------------------------------------------------------
    internal const string HeadSha = "1cabf2f9";   // 第391期の HEAD（この期の作業を始めたときの BattleCore ／ BattleSim）

    static void Zero()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §2 段0 —— 第391期の P1h をハギの規定に");
        Console.WriteLine();
        var before = S390.BalanceMap(S390.GitShow($"{HeadSha}:docs/balance.md"));
        var rows = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count, moved = 0;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw, wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(rows[ri].F, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        var lines = new List<string>();
        for (int ri = 0; ri < rows.Length; ri++)
        {
            if (!before.TryGetValue(rows[ri].Name, out var c)) { lines.Add($"- {rows[ri].Name}: 第391期の表に無い"); continue; }
            var cells = Enumerable.Range(0, nw).Where(w => c[w].TrimEnd('%') != F1(g[ri, w])).ToList();
            moved += cells.Count;
            if (cells.Count > 0) lines.Add($"- {rows[ri].Name}: " + string.Join("・", cells.Select(w => $"第{w + 1}波 {c[w]} → {F1(g[ri, w])}")));
        }
        Console.WriteLine($"## 1. `compare` の動き（全 {rows.Length} 行 × {nw} 波 × seed 0..{CompareSeeds - 1}・第391期の HEAD（{HeadSha}）の `docs/balance.md` と比べる）: 動いたセル **{moved}**");
        Console.WriteLine();
        foreach (var l in lines) Console.WriteLine(l);
        Console.WriteLine();
        Console.WriteLine($"## 2. ハギの台 × 5 物差し（seed 0..{Seeds - 1}）——規定のハギ ／ 同じ席に旧のハギ（第390〜391期の規定 R5m）");
        Console.WriteLine();
        int[] w5 = { 0, 1, 2, 5, 7 };
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", w5.Select(w => W8[w].Name)) + " | 4 波の平均 |");
        Console.WriteLine("|---|---|" + string.Concat(w5.Select(_ => "---|")) + "--:|");
        var boards = new (string Name, Formation F, int Slot)[]
        {
            ("試遊・ハギ 台B", Presets.HagiPlay389(UnitCatalog.HagiH401), 3), ("試遊・死神と墓守", Presets.HagiDeath389(UnitCatalog.HagiH401), 0), ("版・移動 ハギ", Presets.HagiMove390(UnitCatalog.HagiH401), 1),
        };
        foreach (var bd in boards)
            foreach (var (cn, d) in new[] { ("規定", UnitCatalog.HagiH401), ("旧", UnitCatalog.HagiH391) })
            {
                var f = Put(bd.F, bd.Slot, d);
                var m = new S377.B[W8.Length];
                Parallel.For(0, W8.Length, wi => m[wi] = S377.Measure(f, W8[wi].Ws, Seeds));
                Console.WriteLine($"| {(cn == "規定" ? bd.Name : "")} | {cn} | {string.Join(" | ", w5.Select(w => m[w].Cell))} | {F1(new[] { 0, 1, 2, 5 }.Average(w => m[w].Win))} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-2 ／ 3-3 手番の流れ
    // ---------------------------------------------------------------------------------
    static void Flow()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = Rows();
        var g = new S377.B[rows.Length, FlowWaves.Length];
        Parallel.For(0, g.Length, k => g[k / FlowWaves.Length, k % FlowWaves.Length] = MeasureW(rows[k / FlowWaves.Length].F, FlowWaves[k % FlowWaves.Length].W, FlowSeeds));
        Console.WriteLine($"# {Tag} §3-2 ／ 3-3 手番の流れ —— 行 {rows.Length} × {string.Join(" ／ ", FlowWaves.Select(w => w.Name))}（seed 0..{FlowSeeds - 1}・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("行: 指示書の 5 行 ＋ ドルガ ／ ガンのいる `compare` の行のうち近衛 ＋ 大隊の勝率（seed 0..19）がいちばん低い 4 行。");
        Console.WriteLine();
        foreach (var r in rows) Console.WriteLine($"- **{r.Name}**: {Seats(r.F)}");
        Console.WriteLine();
        Console.WriteLine("## 1. 勝率・決着T・倒れた編成の駒 ／ 誰が何Tに倒れたか");
        Console.WriteLine();
        Console.WriteLine("| 行 | 物差し | 勝率・T・倒 | T1 ／ T2 ／ T3 に倒れた編成の駒 | 誰が何Tに倒れたか |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int ri = 0; ri < rows.Length; ri++)
            for (int wi = 0; wi < FlowWaves.Length; wi++)
            {
                var x = g[ri, wi];
                Console.WriteLine($"| {(wi == 0 ? rows[ri].Name : "")} | {FlowWaves[wi].Name} | {x.Cell} | {Per(x.DeathByT[1], x.N)} ／ {Per(x.DeathByT[2], x.N)} ／ {Per(x.DeathByT[3], x.N)} | {x.FellCell} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 2. 動かなかった手番（駒ごと・1戦）——潰れた手番（うち売れた ＝ 号令 ／ 据えが買える）・内訳（痺れ ／ CanAct 偽 ＝ のろまの休みなど ／ 不動 ・まどろみ）と、それを読んだ駒（ガンの +8 ／ 叩き起こし）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 物差し | 潰れた手番（売れた）[痺れ ／ CanAct 偽 ／ 不動] | ガンの +8（誰に）| 叩き起こし（誰を）／ 起こせなかった（動けない ／ 粛）／ 相手なし |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int ri = 0; ri < rows.Length; ri++)
            for (int wi = 0; wi < FlowWaves.Length; wi++)
            {
                var x = g[ri, wi];
                var ids = rows[ri].F.Occupied().Where(o => o.Slot < 5).Select(o => o.Def.Id).Distinct();
                string st = string.Join("・", ids.Where(id => X(x, "stall:" + id) > 0).Select(id => $"{Nm(id)} {Per(X(x, "stall:" + id), x.N)}（{Per(X(x, "sur:" + id), x.N)}）[{Per(X(x, "stun:" + id), x.N)} ／ {Per(X(x, "canact:" + id), x.N)} ／ {Per(X(x, "immob:" + id), x.N)}]"));
                bool hasGan = Has(rows[ri].F, "gan");
                string r8 = hasGan ? $"{Per(X(x, "rally8"), x.N)}（" + string.Join("・", x.X.Keys.Where(k => k.StartsWith("rally8:", StringComparison.Ordinal)).OrderByDescending(k => x.X[k]).Select(k => $"{Nm(k[7..])} {Per(x.X[k], x.N)}")) + "）" : "—";
                string rv = hasGan ? $"{Per(X(x, "rev:" + ReveilleLabels.Woken), x.N)}（" + string.Join("・", x.X.Keys.Where(k => k.StartsWith($"rev:{ReveilleLabels.Woken}:", StringComparison.Ordinal)).Select(k => $"{Nm(k.Split(':')[2])} {Per(x.X[k], x.N)}")) + $"）／ {Per(X(x, "rev:" + ReveilleLabels.Held), x.N)} ／ {Per(X(x, "rev:" + ReveilleLabels.Hushed), x.N)} ／ {Per(X(x, "revMiss"), x.N)}" : "—";
                Console.WriteLine($"| {(wi == 0 ? rows[ri].Name : "")} | {FlowWaves[wi].Name} | {(st == "" ? "—" : st)} | {r8} | {rv} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 3. トモ（§3-2 の 3 ／ §3-3）——灯した駒（ターン数）・灯が消えた回数と理由（ドルガの休み ＝ 前のターンの持ち主がドルガで、偶数ターンに外れた ／ 対象が倒れた ／ ほか ／ 照らす相手がいない）・ドルガに続いた灯の最長（ターン）・累積の最大・手番を譲った（譲れなかった: 敵が倒れていない ／ 灯した味方がいない）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 物差し | 灯した駒 | 消えた: ドルガの休み ／ 倒れた ／ ほか ／ 相手なし | ドルガに続いた最長 | 累積の最大 | 譲った ／ 敵が倒れていない ／ 灯した味方なし | ターンの持ち主がドルガの戦 % T1〜T8 |");
        Console.WriteLine("|---|---|---|---|--:|--:|---|---|");
        for (int ri = 0; ri < rows.Length; ri++)
        {
            if (!Has(rows[ri].F, "tomo")) continue;
            for (int wi = 0; wi < FlowWaves.Length; wi++)
            {
                var x = g[ri, wi];
                string lit = string.Join("・", x.X.Keys.Where(k => k.StartsWith("litOn:", StringComparison.Ordinal)).OrderByDescending(k => x.X[k]).Select(k => $"{Nm(k[6..])} {Per(x.X[k], x.N)}"));
                string tl = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"lit{t}") == 0 ? "—" : Pc(X(x, $"litD{t}"), X(x, $"lit{t}"))));
                Console.WriteLine($"| {(wi == 0 ? rows[ri].Name : "")} | {FlowWaves[wi].Name} | {lit} | {Per(X(x, "douse:dolgaRest"), x.N)} ／ {Per(X(x, "douse:dead"), x.N)} ／ {Per(X(x, "douse:other"), x.N)} ／ {Per(X(x, "douse:none") + X(x, "tomoIdle"), x.N)} | {Per(X(x, "litMaxD"), x.N)} | {Per(X(x, "tomoPeak"), x.N)} | {Per(X(x, "tomoYields"), x.N)} ／ {Per(X(x, "tomoNoDeath"), x.N)} ／ {Per(X(x, "tomoNoTarget"), x.N)} | {tl} |");
            }
        }
        Console.WriteLine();

        Console.WriteLine("## 4. ドルガ——生きて始まったターンのうち 振った ／ 休んだ（偶数 ／ 奇数）・叩き起こされて振った・外から積まれた力（ターンの頭の攻撃力 − 38・T1〜T8）・全体に変わったターン % T1〜T8・全体の振り ／ 1回の与ダメ");
        Console.WriteLine();
        Console.WriteLine("| 行 | 物差し | 振った ／ 休んだ（偶 ／ 奇）／ 起こされた | 積まれた力 T1〜T8 | 全体のターン % T1〜T8 | 振り（うち全体）| 1回の与ダメ |");
        Console.WriteLine("|---|---|---|---|---|---|--:|");
        for (int ri = 0; ri < rows.Length; ri++)
        {
            if (!Has(rows[ri].F, "dolga")) continue;
            for (int wi = 0; wi < FlowWaves.Length; wi++)
            {
                var x = g[ri, wi];
                string bonus = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"dN{t}") == 0 ? "—" : F1((double)X(x, $"dBonus{t}") / X(x, $"dN{t}"))));
                string all = string.Join(" ", Enumerable.Range(1, TT - 1).Select(t => X(x, $"dN{t}") == 0 ? "—" : Pc(X(x, $"dAllT{t}"), X(x, $"dN{t}"))));
                Console.WriteLine($"| {(wi == 0 ? rows[ri].Name : "")} | {FlowWaves[wi].Name} | {Per(X(x, "dActT"), x.N)} ／ {Per(X(x, "dRestEven"), x.N)} ／ {Per(X(x, "dRestOdd"), x.N)} ／ {Per(X(x, "dWoken"), x.N)} | {bonus} | {all} | {Per(X(x, "dAtk"), x.N)}（{Per(X(x, "dAll"), x.N)}）| {(X(x, "dAtk") == 0 ? "—" : F0((double)X(x, "dDmg") / X(x, "dAtk")))} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("ガンの開戦の +4 は鬨の1回が味方全体に届いた数（受け手の数）。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-4 素体に替えた差
    // ---------------------------------------------------------------------------------
    static double[] Wins(Formation f, int seeds)
    {
        var res = new double[PlainWaves.Length];
        Parallel.For(0, PlainWaves.Length, wi =>
        {
            int w = 0;
            for (int s = 0; s < seeds; s++) if (I.Raw(f, PlainWaves[wi].W, s, census: false).R.PlayerWon) w++;
            res[wi] = 100.0 * w / seeds;
        });
        return res;
    }

    static void Plain()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = Rows();
        Console.WriteLine($"# {Tag} §3-4 駒ごとの効き —— 手番の軸の行 × 手番の軸の駒（{string.Join(" ／ ", TempoIds.Select(Nm))}）を同じ席の素体に替えた差（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("素体 ＝ 同じ体（HP ／ 攻 ／ 速 ／ 攻撃の型）・札なし・`Actions` なし（ふつうに殴る）・器具の中だけ。差 ＝ 素体の台 − 元の行。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 替えた駒（席）| " + string.Join(" | ", PlainWaves.Select(w => w.Name)) + " | 平均の差 |");
        Console.WriteLine("|---|---|" + string.Concat(PlainWaves.Select(_ => "--:|")) + "--:|");
        var sum = new Dictionary<string, List<double>>();
        foreach (var (name, f) in rows)
        {
            var b0 = Wins(f, Seeds);
            Console.WriteLine($"| {name} | 元 | {string.Join(" | ", b0.Select(F1))} | — |");
            foreach (var (slot, d) in f.Occupied().Where(o => o.Slot < 5 && TempoIds.Contains(o.Def.Id)))
            {
                var a = Wins(Put(f, slot, Plain(d)), Seeds);
                double avg = Enumerable.Range(0, a.Length).Average(i => a[i] - b0[i]);
                (sum.TryGetValue(d.Id, out var l) ? l : sum[d.Id] = new List<double>()).Add(avg);
                Console.WriteLine($"|  | {Short(d)}（{SeatName(slot)}）| {string.Join(" | ", Enumerable.Range(0, a.Length).Select(i => $"{F1(a[i])}（{Sg(a[i] - b0[i])}）"))} | {Sg(avg)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("駒ごとの平均の差（その駒がいる行の平均）: " + string.Join("・", sum.Select(kv => $"{Nm(kv.Key)} {Sg(kv.Value.Average())}（{kv.Value.Count} 行）")) + "。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-5 席の探索
    // ---------------------------------------------------------------------------------
    internal static UnitDef[] HealPool => _heal ??= Boss283Diag.HealPoolNow;
    static UnitDef[]? _heal;
    static bool IsHealer(UnitDef d) => HealPool.Any(h => h.Id == d.Id);
    internal static readonly (string Name, UnitDef[] Core)[] Cores =
    {
        ("トモ ＋ ガン ＋ ドルガ", new[] { UnitCatalog.TomoH393, UnitCatalog.Gan, UnitCatalog.DolgaH395 }),   // 第396期: ドルガも旧に   // 第394期: トモは第393期までの規定に固定
        ("トモ ＋ ドルガ", new[] { UnitCatalog.TomoH393, UnitCatalog.DolgaH395 }),
        ("ガン ＋ ドルガ", new[] { UnitCatalog.Gan, UnitCatalog.DolgaH395 }),
    };
    /// <summary>残りの枠の候補: ロスター 52 枚から芯の駒とツギ ／ リリを除いた駒。ヒーラー（`Boss283Diag.HealPoolNow`）は残りの枠に1枚まで。</summary>
    internal static UnitDef[] Cands(UnitDef[] core) => UnitCatalog.All.Where(d => !core.Any(c => c.Id == d.Id) && d.Id is not "tsugi" and not "lili").ToArray();

    static readonly int[][] Perms = Permutations(5).ToArray();
    static IEnumerable<int[]> Permutations(int n)
    {
        if (n == 1) { yield return new[] { 0 }; yield break; }
        foreach (var p in Permutations(n - 1))
            for (int k = 0; k < n; k++) { var q = p.ToList(); q.Insert(k, n - 1); yield return q.ToArray(); }
    }
    internal static Formation Board(UnitDef[] five, int[] perm) { var f = new Formation(); for (int i = 0; i < 5; i++) f[perm[i]] = five[i]; return f; }

    sealed record Score(double Mean, double Deaths, double[] Win);
    static Score Quick(Formation f, I.W[] ws, int seeds)
    {
        var win = new double[ws.Length]; long deaths = 0, n = 0;
        for (int wi = 0; wi < ws.Length; wi++)
        {
            int w = 0;
            for (int s = 0; s < seeds; s++)
            {
                var (r, p, _) = I.Raw(f, ws[wi], s, census: false);
                if (r.PlayerWon) w++;
                deaths += p.Count(u => u.LastDeathTurn > 0); n++;
            }
            win[wi] = 100.0 * w / seeds;
        }
        return new Score(win.Average(), (double)deaths / n, win);
    }
    static readonly I.W[] SW1 = { I.Boss, I.Guard, I.Bat };
    static readonly I.W[] SW2 = { I.Boss, I.Guard, I.Bat, I.M5 };
    const int S1Seeds = 4, S2Perms = 5;
    static IOrderedEnumerable<T> Rank<T>(IEnumerable<T> xs, Func<T, Score> sc) => xs.OrderByDescending(x => sc(x).Mean).ThenBy(x => sc(x).Deaths);
    sealed record Cand(UnitDef[] Free, int[] Perm, Formation F);

    /// <summary>段1: 組 × 120 並び × ボス ／ 近衛 ／ 大隊 × seed 0..3 → 段2: 組ごとに段1 の上位 5 並び × seed 0..19 × 4 波（第380期の `seats` と同じ作り）。</summary>
    static List<(Cand C, Score S)> SearchSet(UnitDef[] core, List<UnitDef[]> frees)
    {
        var cands = frees.SelectMany(fr => Perms.Select(pm => new Cand(fr, pm, Board(core.Concat(fr).ToArray(), pm)))).ToArray();
        var s1 = new Score[cands.Length];
        Parallel.For(0, cands.Length, k => s1[k] = Quick(cands[k].F, SW1, S1Seeds));
        var byCombo = Enumerable.Range(0, cands.Length).GroupBy(k => string.Join(",", cands[k].Free.Select(d => d.Id)))
            .Select(g => Rank(g, k => s1[k]).Take(S2Perms).ToArray()).ToList();
        var pick = byCombo.SelectMany(ks => ks).Select(k => cands[k]).ToArray();
        var s2 = new Score[pick.Length];
        Parallel.For(0, pick.Length, k => s2[k] = Quick(pick[k].F, SW2, 20));
        return Rank(Enumerable.Range(0, pick.Length).Select(k => (pick[k], s2[k])), x => x.Item2).ToList();
    }
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
    static S377.B[] Stage3(Formation f)
    {
        var res = new S377.B[W8.Length];
        Parallel.For(0, W8.Length, wi => res[wi] = S377.Measure(f, W8[wi].Ws, Seeds, Extra));
        return res;
    }
    static double Mean4(S377.B[] x) => new[] { 0, 1, 2, 5 }.Average(wi => x[wi].Win);
    static double Dead4(S377.B[] x) => new[] { 0, 1, 2, 5 }.Average(wi => (double)x[wi].FormDeaths / x[wi].N);

    /// <summary>探索の結果（芯の組ごとの段3 の上位 10 台）。`search` と試遊の台の検査が使う。</summary>
    internal static List<(string Core, Formation F, string Free, S377.B[] M)> SearchAll(Action<string>? note = null)
    {
        var res = new List<(string, Formation, string, S377.B[])>();
        // 芯 3 枚 ＋ 2枠: 組の全数
        var c3 = Cores[0].Core;
        var pairs = Combos(Cands(c3), 2);
        var s2c = SearchSet(c3, pairs);
        note?.Invoke($"{Cores[0].Name}: 2枠の組 {pairs.Count}（× 120 並び）");
        // 芯 2 枚 ＋ 3枠: 候補を絞る（芯 3 枚の段2 の上位 60 組に入った回数の上位 18 枚 ＋ 芯から外した 1 枚）
        var freq = s2c.GroupBy(x => string.Join(",", x.C.Free.Select(d => d.Id))).Select(g => g.First()).Take(60)
            .SelectMany(x => x.C.Free).GroupBy(d => d.Id).OrderByDescending(g => g.Count()).ThenBy(g => Array.FindIndex(UnitCatalog.All.ToArray(), d => d.Id == g.Key)).Take(18).Select(g => g.First()).ToList();
        var sets = new List<(string Name, UnitDef[] Core, List<(Cand C, Score S)> S2)> { (Cores[0].Name, c3, s2c) };
        for (int ci = 1; ci < Cores.Length; ci++)
        {
            var core = Cores[ci].Core;
            var pool = freq.Concat(c3.Where(d => !core.Contains(d))).Where(d => !core.Any(c => c.Id == d.Id)).DistinctBy(d => d.Id).ToArray();
            var triples = Combos(pool, 3);
            note?.Invoke($"{Cores[ci].Name}: 3枠の候補 {pool.Length} 枚（{string.Join("・", pool.Select(Short))}）・組 {triples.Count}（× 120 並び）");
            sets.Add((Cores[ci].Name, core, SearchSet(core, triples)));
        }
        foreach (var (name, core, s2) in sets)
        {
            var top = s2.GroupBy(x => string.Join(",", x.C.Free.Select(d => d.Id))).Select(g => g.First()).Take(10).ToList();
            var m3 = new S377.B[top.Count][];
            for (int i = 0; i < top.Count; i++) m3[i] = Stage3(top[i].C.F);
            for (int i = 0; i < top.Count; i++) res.Add((name, top[i].C.F, string.Join(" ＋ ", top[i].C.Free.Select(Short)), m3[i]));
        }
        return res.OrderByDescending(x => Mean4(x.Item4)).ThenBy(x => Dead4(x.Item4)).ThenByDescending(x => x.Item4[7].Win).ToList();
    }

    static void Search()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §3-5 今の基準で戦えるか —— 芯の組を固定し、残りの枠をロスターから探す（ツギ ／ リリは除く・ヒーラーは残りの枠に1枚まで）");
        Console.WriteLine();
        Console.WriteLine($"ヒーラー（`Boss283Diag.HealPoolNow`）: {string.Join("・", HealPool.Select(Short))}。");
        Console.WriteLine($"段1: 組 × 席の全並び 120 × ボス ／ 近衛 ／ 大隊 × seed 0..{S1Seeds - 1}・段2: 組ごとに段1 の上位 {S2Perms} 並び × seed 0..19 × ボス ／ 近衛 ／ 大隊 ／ 本編 第5波・段3: 芯の組ごとに段2 の上位 10 組（組ごとに最良の並び）× seed 0..99 × 8 物差し。並べる順: 段3 の4波（ボス ／ 近衛 ／ 大隊 ／ 本編 第5波）の平均 → 倒れた編成の駒の少ない方 → 強い条件。");
        Console.WriteLine();
        var notes = new List<string>();
        var all = SearchAll(notes.Add);
        foreach (var n in notes) Console.WriteLine("- " + n);
        Console.WriteLine();
        Console.WriteLine($"## 1. 上位 5 台（3 つの芯の組を合わせて・段3・seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| # | 芯 | 残りの枠 | 並び（前1 ／ 前3 ／ 中央 ／ 後1 ／ 後3）| " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 |");
        Console.WriteLine("|--:|---|---|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|");
        for (int i = 0; i < Math.Min(5, all.Count); i++)
            Console.WriteLine($"| {i + 1} | {all[i].Core} | {all[i].Free} | {Seats(all[i].F)} | {string.Join(" | ", all[i].M.Select(x => x.Cell))} | {F1(Mean4(all[i].M))} |");
        Console.WriteLine();
        Console.WriteLine("## 2. 芯の組ごとの最良");
        Console.WriteLine();
        foreach (var (name, _) in Cores)
        {
            int k = all.FindIndex(x => x.Core == name);
            if (k < 0) continue;
            var x = all[k];
            Console.WriteLine($"- **{name}**（全体の {k + 1} 位）: {x.Free}（{Seats(x.F)}）・{string.Join(" ／ ", new[] { 0, 1, 2, 5, 7 }.Select(w => $"{W8[w].Name} {x.M[w].Cell}"))}");
        }
        Console.WriteLine();
        Console.WriteLine("## 3. 上位 3 台で手番の軸の札がどれだけ働いたか（1戦・ボス ／ 近衛 ／ 大隊）——ドルガの振り（うち全体）・積まれた力の最大 T ／ 灯のドルガに続いた最長 ／ 累積の最大 ／ ガンの +8 ／ 叩き起こし");
        Console.WriteLine();
        for (int i = 0; i < Math.Min(3, all.Count); i++)
        {
            var m = all[i].M;
            string W(int wi)
            {
                var x = m[wi];
                string db = Enumerable.Range(1, TT - 1).Where(t => X(x, $"dN{t}") > 0).Select(t => (double)X(x, $"dBonus{t}") / X(x, $"dN{t}")).DefaultIfEmpty(0).Max().ToString("0.#");
                return $"{W8[wi].Name}: ドルガ {Per(X(x, "dAtk"), x.N)}（{Per(X(x, "dAll"), x.N)}）・力の最大 {db}・灯 {Per(X(x, "litMaxD"), x.N)} ／ {Per(X(x, "tomoPeak"), x.N)}・+8 {Per(X(x, "rally8"), x.N)}・起こし {Per(X(x, "rev:" + ReveilleLabels.Woken), x.N)}";
            }
            Console.WriteLine($"- **{i + 1} 位**（{Seats(all[i].F)}）: {W(0)}・{W(1)}・{W(2)}");
            Console.WriteLine($"  - 誰が何Tに倒れたか: ボス {m[0].FellCell} ／ 近衛 {m[1].FellCell} ／ 大隊 {m[2].FellCell}");
        }
        Console.WriteLine();
        Console.WriteLine($"## 4. 他の軸の試遊の台と並べる（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        var axis = new[] { "試遊・死 探索1位", "試遊・死の連鎖", "試遊・ハギ 台B" }.Concat(Sh378Diag.AxisBoards).ToArray();
        var lists = Presets.Versions.Concat(Presets.Playtest).ToList();
        Console.WriteLine("| 台 | ボス | 近衛 | 大隊 | 本編 第5波 | 強い条件 | ボスの倒しT | 4 波 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        Console.WriteLine($"| **手番 探索1位** | {F1(all[0].M[0].Win)} | {F1(all[0].M[1].Win)} | {F1(all[0].M[2].Win)} | {F1(all[0].M[5].Win)} | {F1(all[0].M[7].Win)} | {all[0].M[0].WinTs} | {F1(Mean4(all[0].M))} |");
        foreach (var n in axis)
        {
            var hit = lists.FirstOrDefault(r => r.Name == n);
            if (hit.F is null) continue;
            var m = new S377.B[W8.Length];
            Parallel.For(0, W8.Length, wi => m[wi] = S377.Measure(hit.F, W8[wi].Ws, Seeds));
            Console.WriteLine($"| {n} | {F1(m[0].Win)} | {F1(m[1].Win)} | {F1(m[2].Win)} | {F1(m[5].Win)} | {F1(m[7].Win)} | {m[0].WinTs} | {F1(Mean4(m))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>§3-5 の補足: 「試遊・手番 探索1位」の駒を1枚ずつ同じ席の素体に替えた差（8 物差し・seed 0..99）——台を運んでいる駒。</summary>
    static void Seek()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var f = Pin393(Presets.TempoSeek392());   // 第394期: トモは第393期までの規定に固定
        S377.B[] M(Formation x) { var g = new S377.B[W8.Length]; Parallel.For(0, W8.Length, wi => g[wi] = S377.Measure(x, W8[wi].Ws, Seeds, Extra)); return g; }
        var b0 = M(f);
        Console.WriteLine($"# {Tag} §3-5 の補足 「試遊・手番 探索1位」（{Seats(f)}）の駒を1枚ずつ同じ席の素体に替えた差（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("| 替えた駒（席）| " + string.Join(" | ", W8.Select(w => w.Name)) + " | 4 波 | 強の差 |");
        Console.WriteLine("|---|" + string.Concat(W8.Select(_ => "---|")) + "--:|--:|");
        Console.WriteLine($"| 元 | {string.Join(" | ", b0.Select(x => x.Cell))} | {F1(Mean4(b0))} | — |");
        foreach (var (slot, d) in f.Occupied().Where(o => o.Slot < 5))
        {
            var a = M(Put(f, slot, Plain(d)));
            Console.WriteLine($"| {Short(d)}（{SeatName(slot)}）| {string.Join(" | ", a.Select(x => x.Cell))} | {F1(Mean4(a))} | {Sg(a[7].Win - b0[7].Win)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Log1(string[] args)
    {
        string rn = args.Length > 3 ? args[3] : "灯×薙ぎ";
        int wi = args.Length > 4 && int.TryParse(args[4], out int w0) ? Math.Clamp(w0, 0, FlowWaves.Length - 1) : 0, seed = args.Length > 5 && int.TryParse(args[5], out int s0) ? s0 : 0;
        var row = CompareBuilds().FirstOrDefault(r => r.Name.StartsWith(rn, StringComparison.Ordinal));
        if (row.F is null) { Console.WriteLine($"行が無い: {rn}"); return; }
        var (r, _, _) = I.Raw(row.F, FlowWaves[wi].W, seed, verbose: true);
        Console.WriteLine($"# {row.Name} × {FlowWaves[wi].Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
