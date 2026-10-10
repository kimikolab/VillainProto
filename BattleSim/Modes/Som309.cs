using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S293 = Shock293Diag;
using S307 = Som307Diag;
using H304 = Hush304Diag;

// =====================================================================================
// som309 —— 第309期「ソムの光の衣 LV-a の規定化 ＋ 試遊の台 ＋ ソム対ツギの物差し」。
// 指示書は design/PHASE309_SOM_REGULATE_SPEC.md ／ 報告は design/PHASE309_SOM_REGULATE.md。
//
//     dotnet run --project BattleSim -c Release 0 som309 p0          # Phase 0（§5）: `感電` 行の帳簿（ソムの光 ＋ 衣 ／ ツギの板 ＋ 回復）
//     dotnet run --project BattleSim -c Release 0 som309 pick        # §3-1: ボスの格子の組（トウ ＋ クビ ／ クビ ＋ ガン）の 120 席を規定のソムで回し、試遊の台を選ぶ
//     dotnet run --project BattleSim -c Release 0 som309 playtest    # §3-1: 試遊の2台 × ボス ／ 近衛 ／ 大隊 ／ 本編の5波（勝率・倒しT・対照 ソム → ドルガ）
//     dotnet run --project BattleSim -c Release 0 som309 swap        # §4-1: 同じ台・同じ席で ソム ↔ ツギ（勝率・倒しT・寿命・差）
//     dotnet run --project BattleSim -c Release 0 som309 grid <boss|guard|bat>   # §4-2: 固定枠の3枚目（ソム ／ ツギ）だけを入れ替えた格子
//     dotnet run --project BattleSim -c Release 0 som309 memo [行名の一部] [boss|guard|bat|1..5] [seed] [最初のT] [最後のT]   # §3-2: 台本の抜粋
//     dotnet run --project BattleSim -c Release 0 som309 check       # 自己検査
//
// 規定の駒（段0 の後・ソム ＝ LV-a）のまま組む（固定しない）。ツギとの入れ替えは同じ席・同じ陣形・同じレリックで（`Swap`）。
// =====================================================================================
static class Som309Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": P0(); return;
            case "pick": Pick(); return;
            case "playtest": PlaytestAll(); return;
            case "swap": SwapAll(); return;
            case "grid": Grid(A(3, "boss")); return;
            case "memo": Memo(A(3, "光の盾"), A(4, "boss"), int.Parse(A(5, "0")), int.Parse(A(6, "1")), int.Parse(A(7, "3"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("som309: モードは p0 / pick / playtest / swap / grid / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 台・波（測る前に固定）
    // ---------------------------------------------------------------------------------
    const int Seeds = S307.Seeds;
    static S287.Wave[] Waves => S307.Waves;
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);
    static string F1(double x) => S307.F1(x);
    static string Per1(long a, long n) => S307.Per1(a, n);
    static string Per0(long a, long n) => S307.Per0(a, n);
    static string Pct(long a, long n) => S307.Pct(a, n);
    static string Dt(long died, long t, long n) => died == 0 ? "—" : $"{Per1(t, died)}（{Pct(died, n)}）";
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static UnitDef Som => UnitCatalog.Som;
    static UnitDef Tsugi => UnitCatalog.Tsugi;
    internal static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;
    static Formation WinSeat() => B283.Seat(S307.Order(S307.WinBoard));

    /// <summary>同じ席・同じ陣形・同じレリックのまま、<paramref name="from"/> を <paramref name="to"/> に替える（`FvSwap` は陣形とレリックを落とすので使わない）。</summary>
    internal static Formation Swap(Formation f, UnitDef from, UnitDef to)
    {
        var g = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied()) if (ReferenceEquals(d, from)) g[slot] = to;
        return g;
    }

    /// <summary>§3-1 の2台の名前（指示書の叩き台）と、ボスの格子の組（探索枠2）。</summary>
    internal const string Shield = "試遊・感電 光の盾", ShieldHeavy = "試遊・感電 光の盾 重";
    internal static readonly (string Name, string Pair)[] PickPairs = { (Shield, "トウ・クビ"), (ShieldHeavy, "クビ・ガン") };
    /// <summary>第308期のボスの格子の固定枠（クグ ＋ カタ ＋ ソム）。</summary>
    static UnitDef[] BossFixed(UnitDef h) => new[] { UnitCatalog.Kugu, UnitCatalog.Kata, h };

    /// <summary>
    /// §4-1 の比べる台。どれも (ソムの台, ツギの台) の対で、同じ席・同じ陣形・同じレリック。
    /// ツギの台が組めない（ツギが既に台にいる）ものは <c>Tsugi = null</c>。
    /// </summary>
    internal static (string Name, string Group, Formation Som, Formation? Tsugi)[] Pairs()
    {
        var win = WinSeat();
        var shock = Row("感電 (シガ×カタ×ソム)");
        var list = new List<(string, string, Formation, Formation?)>
        {
            ("雷の型 ドハ→H", "代表台 ／ 試遊", Swap(Playtest("試遊・感電 雷の型"), UnitCatalog.Doha, Som), Swap(Playtest("試遊・感電 雷の型"), UnitCatalog.Doha, Tsugi)),
            ("感電 糸 ガルド→H", "代表台 ／ 試遊", Swap(Playtest("試遊・感電 糸"), UnitCatalog.Gald, Som), Swap(Playtest("試遊・感電 糸"), UnitCatalog.Gald, Tsugi)),
            ("火の型 ソラ→H", "試遊", Swap(Playtest("試遊・感電 火の型"), UnitCatalog.Sora, Som), Swap(Playtest("試遊・感電 火の型"), UnitCatalog.Sora, Tsugi)),
            ("勝ち台 ツギの席（元の勝ち台）", "代表台 ／ 元の勝ち台", Swap(win, Tsugi, Som), win),
            ("勝ち台 ゴルム→H", "代表台", Swap(win, UnitCatalog.Golm, Som), null),
            ("感電 行（もう片方 → ドルガ）", "compare", Swap(shock, Tsugi, UnitCatalog.Dolga), Swap(shock, Som, UnitCatalog.Dolga)),
        };
        if (Presets.Playtest.Any(r => r.Name == Shield)) list.Add(("光の盾", "試遊（§3-1）", Playtest(Shield), Swap(Playtest(Shield), Som, Tsugi)));
        if (Presets.Playtest.Any(r => r.Name == ShieldHeavy)) list.Add(("光の盾 重", "試遊（§3-1）", Playtest(ShieldHeavy), Swap(Playtest(ShieldHeavy), Som, Tsugi)));
        return list.ToArray();
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    sealed class St
    {
        public long N, Wins, WinT, LoseT, Caps, FirstDeathN, FirstDeathT, HDied, HDeathT;
        public long SomHealed, VeilAdded, VeilSoaked, TsugiPaste, TsugiAid, TsugiHeal, PlankSoaked, HeroDmg, FrontDmg;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Merge(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LoseT += o.LoseT; Caps += o.Caps; FirstDeathN += o.FirstDeathN; FirstDeathT += o.FirstDeathT; HDied += o.HDied; HDeathT += o.HDeathT;
            SomHealed += o.SomHealed; VeilAdded += o.VeilAdded; VeilSoaked += o.VeilSoaked; TsugiPaste += o.TsugiPaste; TsugiAid += o.TsugiAid; TsugiHeal += o.TsugiHeal; PlankSoaked += o.PlankSoaked; HeroDmg += o.HeroDmg; FrontDmg += o.FrontDmg;
        }
    }

    /// <summary>1戦（verbose）。寿命 ＝ 味方が初めて倒れたT。H ＝ ソム ／ ツギ（台にいる方・両方なら ソム）。</summary>
    static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var a = new St { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else { a.LoseT = r.Turns; if (r.Turns >= BattleEngine.MaxTurns) a.Caps = 1; }
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        UnitState? som = p.FirstOrDefault(u => u.Def.Id == "som"), tsugi = p.FirstOrDefault(u => u.Def.Id == "tsugi");
        UnitState? h = som ?? tsugi;
        if (som is not null && r.TallyByUnit.TryGetValue("som", out var mt)) { a.SomHealed = mt.SparkHealed; a.VeilAdded = mt.SparkVeilAdded; }
        foreach (var u in p) if (r.TallyByUnit.TryGetValue(u.Def.Id, out var at)) { a.VeilSoaked += at.SparkVeilSoaked; a.PlankSoaked += at.PlankSoaked; }
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Death when ev.TargetId is int t && mine.Contains(t):
                    if (a.FirstDeathN == 0) { a.FirstDeathN = 1; a.FirstDeathT = ev.Turn; }
                    if (h is not null && t == h.InstanceId && a.HDied == 0) { a.HDied = 1; a.HDeathT = ev.Turn; }
                    break;
                case BattleEventKind.Plank when tsugi is not null && ev.ActorId == tsugi.InstanceId && ev.Text == PlankLabels.Paste: a.TsugiPaste += ev.Amount; break;
                case BattleEventKind.Plank when tsugi is not null && ev.ActorId == tsugi.InstanceId && ev.Text == PlankLabels.FirstAid: a.TsugiAid += ev.Amount; break;
                case BattleEventKind.Heal when tsugi is not null && ev.ActorId == tsugi.InstanceId: a.TsugiHeal += ev.Amount; break;
                case BattleEventKind.Damage when ev.ActorId is int s && !mine.Contains(s) && ev.TargetId is int d2 && mine.Contains(d2):
                    a.HeroDmg += ev.Amount;
                    if (p.FirstOrDefault(u => u.InstanceId == d2) is { Slot: 0 or 1 }) a.FrontDmg += ev.Amount;
                    break;
            }
        }
        return a;
    }

    static St Measure(Formation f, S287.Wave w, int n = Seeds)
    {
        var parts = new St[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, i));
        var all = new St();
        foreach (var x in parts) all.Merge(x);
        return all;
    }

    static int WinsLite(Formation f, S287.Wave w, int n, out long winT)
    {
        int wins = 0; long wt = 0;
        Parallel.For(0, n, s =>
        {
            var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
            if (r.PlayerWon) { Interlocked.Increment(ref wins); Interlocked.Add(ref wt, r.Turns); }
        });
        winT = wt;
        return wins;
    }

    // ---------------------------------------------------------------------------------
    // Phase 0（§5）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第309期 Phase 0（指示書 §5・段0 の後の規定 ＝ ソム LV-a・seed 0..199・verbose）");
        Console.WriteLine();
        Console.WriteLine("## 3. `感電 (シガ×カタ×ソム)` の行の帳簿（1戦あたり）");
        Console.WriteLine();
        var shock = Row("感電 (シガ×カタ×ソム)");
        Console.WriteLine($"席: {Seats(shock)}（前1・前3・中央・後1・後3）");
        Console.WriteLine();
        Console.WriteLine("ソム ＝ 光が HP に入った量 ＋ 衣が受け止めた量。ツギ ＝ 板（手番）＋ 応急処置 ＋ 回復（書いた量）と、板の印がある間に破片が吸った量（`PlankSoaked`）。敵の与ダメ ＝ 敵が味方の HP に届けたダメージ。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率 | ソム: HP に入った ／ 衣が受け止めた | ツギ: 板 ／ 応急処置 ／ 回復 | 板が吸った | 敵の与ダメ | ソム ÷ ツギ（書いた量） |");
        Console.WriteLine("|---|--:|---|---|--:|--:|--:|");
        foreach (var w in Waves)
        {
            var a = Measure(shock, w);
            long so = a.SomHealed + a.VeilSoaked, ts = a.TsugiPaste + a.TsugiAid + a.TsugiHeal;
            Console.WriteLine($"| {w.Name} | {F1(a.Win)} | {Per0(a.SomHealed, a.N)} ／ {Per0(a.VeilSoaked, a.N)} | {Per0(a.TsugiPaste, a.N)} ／ {Per0(a.TsugiAid, a.N)} ／ {Per0(a.TsugiHeal, a.N)} | {Per0(a.PlankSoaked, a.N)} | {Per0(a.HeroDmg, a.N)} | {(ts == 0 ? "—" : ((double)so / ts).ToString("F2"))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-1 試遊の台を選ぶ
    // ---------------------------------------------------------------------------------
    internal sealed record Cand(UnitDef[] Order, int Wins, long WinT, long Alive)
    {
        public double MeanT => Wins == 0 ? double.MaxValue : (double)WinT / Wins;
    }

    /// <summary>
    /// 組（探索枠2）の 120 席を規定のソムでボスに回し、勝率の高い順 → 倒しT の短い順 → 戦の終わりに立っている味方の多い順 → 席の名前の順（序数）に並べる。
    /// 1位が試遊の台（指示書 §3-1「100% の組のうち、倒しT が最も短い台」・倒しT が同じ席が多いので、残った味方で選ぶ）。
    /// </summary>
    internal static List<Cand> Rank(string pair)
    {
        var order = BossFixed(Som).Concat(S307.Order(pair)).ToArray();
        var perms = B283.Perms(order).ToList();
        var res = new Cand[perms.Count];
        for (int i = 0; i < perms.Count; i++)
        {
            var f = B283.Seat(perms[i]);
            int wins = 0; long wt = 0, alive = 0;
            Parallel.For(0, Seeds, s =>
            {
                var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var r = BattleEngine.Run(p, Waves[0].Make(), s, verbose: false);
                Interlocked.Add(ref alive, p.Count(u => u.IsAlive && u.Slot < FormationRules.PlayableSlotCount));
                if (r.PlayerWon) { Interlocked.Increment(ref wins); Interlocked.Add(ref wt, r.Turns); }
            });
            res[i] = new Cand(perms[i], wins, wt, alive);
        }
        return res.OrderByDescending(c => c.Wins).ThenBy(c => Math.Round(c.MeanT, 6)).ThenByDescending(c => c.Alive).ThenBy(c => S307.OrderName(c.Order), StringComparer.Ordinal).ToList();
    }

    static void Pick()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第309期 §3-1 試遊の台を選ぶ（ボス・固定枠 クグ ＋ カタ ＋ ソム（規定 ＝ LV-a）＋ 組・120 席・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("並び: 勝率の高い順 → 倒しT の短い順 → 戦の終わりに立っている味方（5 枠・200 戦の合計）の多い順 → 席の名前の順（序数）。**1位を試遊の台にする**。");
        foreach (var (name, pair) in PickPairs)
        {
            var l = Rank(pair);
            var top = l[0];
            int full = l.Count(c => c.Wins == Seeds), tie = l.Count(c => c.Wins == top.Wins && Math.Abs(c.MeanT - top.MeanT) < 1e-9), tie2 = l.Count(c => c.Wins == top.Wins && Math.Abs(c.MeanT - top.MeanT) < 1e-9 && c.Alive == top.Alive);
            Console.WriteLine();
            Console.WriteLine($"## {name}（組 {pair}）");
            Console.WriteLine();
            Console.WriteLine($"100% の席 {full} ／ 120。1位と同じ勝率・同じ倒しT の席 {tie}・そのうち残った味方も同じ席 {tie2}。");
            Console.WriteLine();
            Console.WriteLine("| # | 席（前1・前3・中央・後1・後3） | 勝率 | 倒しT | 残った味方 ／ 戦 |");
            Console.WriteLine("|--:|---|--:|--:|--:|");
            for (int i = 0; i < Math.Min(12, l.Count); i++) Console.WriteLine($"| {i + 1} | {S307.OrderName(l[i].Order)} | {F1(100.0 * l[i].Wins / Seeds)} | {l[i].MeanT:F2} | {(double)l[i].Alive / Seeds:F2} |");
            var inPreset = Presets.Playtest.FirstOrDefault(r => r.Name == name);
            Console.WriteLine();
            Console.WriteLine(inPreset.F is null ? "- `Presets.Playtest` にまだ無い。" : $"- `Presets.Playtest` の {name}: {Seats(inPreset.F)}（1位と{(Seats(inPreset.F) == S307.OrderName(top.Order) ? "一致" : "**不一致**")}）");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-1 試遊の2台 × 波
    // ---------------------------------------------------------------------------------
    static void PlaytestAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第309期 §3-1 試遊の2台 × 波（規定の駒・seed 0..199・verbose）");
        Console.WriteLine();
        foreach (var n in new[] { Shield, ShieldHeavy }) Console.WriteLine($"- {n}: {Seats(Playtest(n))}（前1・前3・中央・後1・後3）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 倒しT | 負けT | 寿命（初めて倒れたT・倒れた戦の割合） | ソム 倒れたT | 光が HP に ／ 衣が受け止めた ／ 衣になった | ソム → ドルガ |");
        Console.WriteLine("|---|---|--:|--:|--:|---|---|---|--:|");
        foreach (var n in new[] { Shield, ShieldHeavy })
            foreach (var w in Waves)
            {
                var f = Playtest(n);
                var a = Measure(f, w);
                int dv = WinsLite(Swap(f, Som, UnitCatalog.Dolga), w, Seeds, out _);
                Console.WriteLine($"| {n} | {w.Name} | {F1(a.Win)} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)}{(a.Caps > 0 ? $"（上限 {Pct(a.Caps, a.N)}）" : "")} | {Dt(a.FirstDeathN, a.FirstDeathT, a.N)} | {Dt(a.HDied, a.HDeathT, a.N)} | "
                    + $"{Per0(a.SomHealed, a.N)} ／ {Per0(a.VeilSoaked, a.N)} ／ {Per0(a.VeilAdded, a.N)} | {F1(100.0 * dv / Seeds)} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-1 ソム ↔ ツギ（同じ台・同じ席）
    // ---------------------------------------------------------------------------------
    static void SwapAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var pairs = Pairs();
        Console.WriteLine("# 第309期 §4-1 ソム ↔ ツギ（同じ台・同じ席・同じ陣形・同じレリック・seed 0..199・verbose）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 出典 | ソムの台 | ツギの台 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var (n, g, s, t) in pairs) Console.WriteLine($"| {n} | {g} | {Seats(s)} | {(t is null ? "—（ツギが既に台にいる）" : Seats(t))} |");
        var cells = new List<(string Name, S287.Wave W, St S, St? T)>();
        foreach (var (n, _, s, t) in pairs)
            foreach (var w in Waves) cells.Add((n, w, Measure(s, w), t is null ? null : Measure(t, w)));

        Console.WriteLine();
        Console.WriteLine("## 表A 勝率（ソム ／ ツギ ／ 差 ＝ ソム − ツギ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (n, _, _, _) in pairs)
            Console.WriteLine($"| {n} | " + string.Join(" | ", Waves.Select(w =>
            {
                var c = cells.First(x => x.Name == n && x.W == w);
                return c.T is null ? $"{F1(c.S.Win)} ／ —" : $"{F1(c.S.Win)} ／ {F1(c.T.Win)} ／ **{Sg(c.S.Win - c.T.Win)}**";
            })) + " |");

        Console.WriteLine();
        Console.WriteLine("## 表B 波ごと（倒しT ・ 寿命 ＝ 初めて倒れたT（倒れた戦の割合）・H 倒れたT ・ 書いた量）");
        Console.WriteLine();
        Console.WriteLine("ソムの書いた量 ＝ 光が HP に入った量 ／ 衣が受け止めた量。ツギの書いた量 ＝ 板 ／ 応急処置 ／ 回復。前列が受けた ＝ 敵が前1 ／ 前3 の HP に届けたダメージ。");
        foreach (var w in Waves)
        {
            Console.WriteLine();
            Console.WriteLine($"### {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 側 | 勝率 | 差 | 倒しT | 負けT | 寿命 | H 倒れたT | 書いた量 | 敵の与ダメ ／ うち前列 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|---|---|---|---|");
            foreach (var c in cells.Where(x => x.W == w))
            {
                void Line(string side, St a, string diff, bool som) =>
                    Console.WriteLine($"| {c.Name} | {side} | {F1(a.Win)} | {diff} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)}{(a.Caps > 0 ? $"（上限 {Pct(a.Caps, a.N)}）" : "")} | {Dt(a.FirstDeathN, a.FirstDeathT, a.N)} | {Dt(a.HDied, a.HDeathT, a.N)} | "
                        + (som ? $"光 {Per0(a.SomHealed, a.N)} ／ 衣 {Per0(a.VeilSoaked, a.N)}" : $"板 {Per0(a.TsugiPaste, a.N)} ／ 応急 {Per0(a.TsugiAid, a.N)} ／ 回復 {Per0(a.TsugiHeal, a.N)}（板が吸った {Per0(a.PlankSoaked, a.N)}）") + $" | {Per0(a.HeroDmg, a.N)} ／ {Per0(a.FrontDmg, a.N)} |");
                Line("ソム", c.S, c.T is null ? "" : Sg(c.S.Win - c.T.Win), true);
                if (c.T is not null) Line("ツギ", c.T, "", false);
            }
        }

        // §4-3 目安（代表台）
        int plus = 0, minus = 0, n2 = 0;
        var minusCells = new List<string>();
        foreach (var c in cells.Where(x => x.T is not null && x.W.Name != "本編 第1波"))
        {
            n2++;
            double d = c.S.Win - c.T!.Win;
            if (d >= 10) plus++;
            if (d <= -10) { minus++; minusCells.Add($"{c.Name} × {c.W.Name}（{Sg(d)}）"); }
        }
        Console.WriteLine();
        Console.WriteLine("## §4-3 目安（台）");
        Console.WriteLine();
        Console.WriteLine($"入れ替えた台 × 波（本編 第1波を除く・{n2} セル）のうち、ソム − ツギ ≥ +10pt: **{plus}** ／ ≤ −10pt: **{minus}** → **{(plus > minus ? "満たす" : "満たさない")}**（+10 の側が多いか）");
        if (minusCells.Count > 0) Console.WriteLine($"- −10pt 以下のセル: {string.Join("・", minusCells)}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4-2 格子（固定枠の3枚目 ＝ ソム ／ ツギ だけを入れ替える）
    // ---------------------------------------------------------------------------------
    sealed record GR(UnitDef[] Order, int[] Wins, long[] WinT, int[][] Ctl);

    static void Grid(string wave)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var w = S307.WaveOf(wave);
        UnitDef[] fixedBase = w.Boss ? new[] { UnitCatalog.Kugu, UnitCatalog.Kata } : new[] { UnitCatalog.Tou };
        var pool = w.Boss ? S307.BossPool : S307.ElitePool;
        int k = w.Boss ? 2 : 3;
        var hs = new[] { Som, Tsugi };
        string[] hn = { "ソム", "ツギ" };
        var lu = S293.Combos(pool, k);
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedBase.Append(Som).Concat(t).ToArray()));
        var res = new GR?[boards.Count];
        int cutPass = 0;
        int need = w.Boss ? 1 : 5;
        Parallel.For(0, boards.Count, i =>
        {
            var f0 = B283.Seat(boards[i]);
            var fs = new[] { f0, Swap(f0, Som, Tsugi) };
            static int W(Formation f, S287.Wave w, int n, out long wt)
            {
                int wins = 0; wt = 0;
                for (int s = 0; s < n; s++) { var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false); if (r.PlayerWon) { wins++; wt += r.Turns; } }
                return wins;
            }
            if (!fs.Any(f => W(f, w, S307.CutSeeds, out _) >= need)) return;
            Interlocked.Increment(ref cutPass);
            var wins = new int[2]; var wt = new long[2]; var ctl = new int[2][];
            for (int v = 0; v < 2; v++)
            {
                wins[v] = W(fs[v], w, Seeds, out wt[v]);
                var fixedV = fixedBase.Append(hs[v]).ToArray();
                ctl[v] = fixedV.Select(_ => -1).ToArray();
                if (w.Boss ? wins[v] == 0 : wins[v] * 2 < Seeds) continue;
                for (int c = 0; c < fixedV.Length; c++) ctl[v][c] = W(Swap(fs[v], fixedV[c], UnitCatalog.Dolga), w, Seeds, out _);
            }
            res[i] = new GR(boards[i], wins, wt, ctl);
        });
        var all = res.Where(r => r is not null).Select(r => r!).ToList();
        int hIdx = fixedBase.Length;   // 固定枠の3枚目（ソム ／ ツギ）の位置
        bool Half(GR r, int v) => r.Wins[v] * 2 >= Seeds;
        bool Reach(GR r, int v) => Half(r, v) && r.Ctl[v][hIdx] >= 0 && r.Ctl[v][hIdx] * 2 <= r.Wins[v];   // 指示書 §4-2: X → ドルガで半分以下
        bool Reach308(GR r, int v) => Half(r, v) && r.Ctl[v].Any(c => c >= 0 && c * 2 <= r.Wins[v]);    // 第308期: 固定枠のどれか1枚 → ドルガで半分以下
        string Key(UnitDef[] o) => string.Join("・", o.Where(d => !fixedBase.Contains(d) && !ReferenceEquals(d, Som)).OrderBy(d => Array.IndexOf(pool, d)).Select(Short));   // 組は候補の並びで（席の順に依らない）
        string Tot(Func<GR, int, bool> pred, int v) { var l = all.Where(r => pred(r, v)).ToList(); return $"{l.Count:N0}（{l.Select(r => Key(r.Order)).Distinct().Count()}）"; }

        Console.WriteLine($"# 第309期 §4-2 格子 × {w.Name} × 固定枠 {S307.OrderName(fixedBase)} ＋ X（ソム ／ ツギ）・探索枠{k}");
        Console.WriteLine();
        Console.WriteLine($"候補 {pool.Length} 枚（{S307.OrderName(pool)}・ヒーラーを除いた・第307 ／ 308期と同じ）＝ {lu.Count} 組 × 席 120 ＝ {boards.Count:N0} 台。ツギの台はソムの台の X をツギに替えた同じ席。");
        Console.WriteLine($"足切り seed 0..{S307.CutSeeds - 1}（ソム ／ ツギのどちらかで {need} 勝以上・{cutPass:N0} 台が通った）→ 両方 seed 0..{Seeds - 1} → {(w.Boss ? "勝った" : "勝率 50% 以上の")}側で固定枠それぞれ → ドルガ。");
        Console.WriteLine("**届いた ＝ 勝率 50% 以上で、X → ドルガで半分以下**（指示書 §4-2）。第308期の定義（固定枠のどれか1枚 → ドルガで半分以下）も併記。");
        Console.WriteLine();
        Console.WriteLine("## 表1 段ごとの台数（組の数）");
        Console.WriteLine();
        Console.WriteLine("| 段 | ソム | ツギ | ソム ÷ ツギ |");
        Console.WriteLine("|---|--:|--:|--:|");
        void RowP(string label, Func<GR, int, bool> pred)
        {
            int a = all.Count(r => pred(r, 0)), b = all.Count(r => pred(r, 1));
            Console.WriteLine($"| {label} | {Tot(pred, 0)} | {Tot(pred, 1)} | {(b == 0 ? (a == 0 ? "—" : "∞") : ((double)a / b).ToString("F2"))} |");
        }
        RowP("勝率 ≧ 50%", Half);
        RowP("**届いた（X → ドルガで半分以下）**", Reach);
        RowP("届いた（第308期の定義）", Reach308);
        for (int c = 0; c < fixedBase.Length; c++) { int cc = c; RowP($"{Short(fixedBase[c])}が要る（→ ドルガで半分以下）", (r, v) => Half(r, v) && r.Ctl[v][cc] >= 0 && r.Ctl[v][cc] * 2 <= r.Wins[v]); }
        RowP("勝率 ≧ 90%", (r, v) => r.Wins[v] * 10 >= Seeds * 9);
        RowP("勝率 ＞ 0%", (r, v) => r.Wins[v] > 0);
        Console.WriteLine($"| 最大の勝率 | " + string.Join(" | ", Enumerable.Range(0, 2).Select(v => all.Count == 0 ? "—" : $"{F1(100.0 * all.Max(r => r.Wins[v]) / Seeds)}")) + " | |");

        // 組ごとの分け（§4-2）
        Console.WriteLine();
        Console.WriteLine("## 表2 組ごと: ソムで届いてツギで届かない ／ ツギで届いてソムで届かない ／ 両方（届いた ＝ X → ドルガで半分以下）");
        Console.WriteLine();
        var groups = all.GroupBy(r => Key(r.Order)).Select(g => (Key: g.Key, S: g.Count(r => Reach(r, 0)), T: g.Count(r => Reach(r, 1)), BothB: g.Count(r => Reach(r, 0) && Reach(r, 1)), MS: g.Max(r => r.Wins[0]), MT: g.Max(r => r.Wins[1])))
            .Where(g => g.S > 0 || g.T > 0).ToList();
        var so = groups.Where(g => g.S > 0 && g.T == 0).ToList(); var to = groups.Where(g => g.T > 0 && g.S == 0).ToList(); var bo = groups.Where(g => g.S > 0 && g.T > 0).ToList();
        int bS = all.Count(r => Reach(r, 0) && !Reach(r, 1)), bT = all.Count(r => Reach(r, 1) && !Reach(r, 0)), bB = all.Count(r => Reach(r, 0) && Reach(r, 1));
        Console.WriteLine("| 分け | 組の数 | 台の数（同じ席で） |");
        Console.WriteLine("|---|--:|--:|");
        Console.WriteLine($"| ソムで届いて、ツギで届かない | {so.Count} | {bS:N0} |");
        Console.WriteLine($"| ツギで届いて、ソムで届かない | {to.Count} | {bT:N0} |");
        Console.WriteLine($"| 両方 | {bo.Count} | {bB:N0} |");
        Console.WriteLine();
        Console.WriteLine("| 組（探索枠） | 分け | ソム 届いた台 | ツギ 届いた台 | 同じ席で両方 | ソム 最大 | ツギ 最大 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|");
        foreach (var g in groups.OrderByDescending(g => g.S + g.T).ThenBy(g => g.Key, StringComparer.Ordinal).Take(60))
            Console.WriteLine($"| {g.Key} | {(g.S > 0 && g.T > 0 ? "両方" : g.S > 0 ? "ソムだけ" : "ツギだけ")} | {g.S} | {g.T} | {g.BothB} | {F1(100.0 * g.MS / Seeds)} | {F1(100.0 * g.MT / Seeds)} |");
        if (groups.Count > 60) Console.WriteLine($"| …（ほか {groups.Count - 60} 組） | | | | | | |");

        // ツギの上位3台をソムに
        Console.WriteLine();
        Console.WriteLine("## 表3 ツギの格子の上位の台（勝率 → 倒しT → 名前の順）と、同じ席をソムにした勝率");
        Console.WriteLine();
        Console.WriteLine("| # | 席（ツギの台・前1・前3・中央・後1・後3） | ツギ 勝率 | ツギ 倒しT | ソム 勝率 | ソム 倒しT | 差 | ツギ → ドルガ |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|");
        string TName(UnitDef[] o) => string.Join("・", o.Select(d => ReferenceEquals(d, Som) ? "ツギ" : Short(d)));
        var tt = all.Where(r => r.Wins[1] > 0).OrderByDescending(r => r.Wins[1]).ThenBy(r => (double)r.WinT[1] / Math.Max(1, r.Wins[1])).ThenBy(r => TName(r.Order), StringComparer.Ordinal).Take(3).ToList();
        for (int i = 0; i < tt.Count; i++)
        {
            var r = tt[i];
            Console.WriteLine($"| {i + 1} | {TName(r.Order)} | {F1(100.0 * r.Wins[1] / Seeds)} | {Per1(r.WinT[1], r.Wins[1])} | {F1(100.0 * r.Wins[0] / Seeds)} | {Per1(r.WinT[0], r.Wins[0])} | {Sg(100.0 * (r.Wins[0] - r.Wins[1]) / Seeds)} | {(r.Ctl[1][hIdx] < 0 ? "—" : F1(100.0 * r.Ctl[1][hIdx] / Seeds))} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 表4 ソムの格子の上位の台（同じ並べ方）と、同じ席をツギにした勝率");
        Console.WriteLine();
        Console.WriteLine("| # | 席（ソムの台） | ソム 勝率 | ソム 倒しT | ツギ 勝率 | ツギ 倒しT | 差 | ソム → ドルガ |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|--:|--:|");
        var st = all.Where(r => r.Wins[0] > 0).OrderByDescending(r => r.Wins[0]).ThenBy(r => (double)r.WinT[0] / Math.Max(1, r.Wins[0])).ThenBy(r => S307.OrderName(r.Order), StringComparer.Ordinal).Take(3).ToList();
        for (int i = 0; i < st.Count; i++)
        {
            var r = st[i];
            Console.WriteLine($"| {i + 1} | {S307.OrderName(r.Order)} | {F1(100.0 * r.Wins[0] / Seeds)} | {Per1(r.WinT[0], r.Wins[0])} | {F1(100.0 * r.Wins[1] / Seeds)} | {Per1(r.WinT[1], r.Wins[1])} | {Sg(100.0 * (r.Wins[0] - r.Wins[1]) / Seeds)} | {(r.Ctl[0][hIdx] < 0 ? "—" : F1(100.0 * r.Ctl[0][hIdx] / Seeds))} |");
        }

        int ra = all.Count(r => Reach(r, 0)), rb = all.Count(r => Reach(r, 1));
        Console.WriteLine();
        Console.WriteLine($"**§4-3 目安（格子）**: 届いた台 ソム {ra:N0} ／ ツギ {rb:N0} → {(rb == 0 ? (ra > 0 ? "ツギ 0 台" : "どちらも 0 台") : $"{(double)ra / rb:F2} 倍")} → **{(ra > 0 && ra * 2 >= rb * 3 ? "1.5 倍以上（満たす）" : "1.5 倍に届かない（満たさない）")}**");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-2 台本の抜粋
    // ---------------------------------------------------------------------------------
    static void Memo(string part, string wave, int seed, int t0, int t1)
    {
        var (name, f) = Presets.Playtest.First(r => r.Name.Contains(part, StringComparison.Ordinal));
        var w = S307.WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        foreach (var ev in r.Events.Where(x => x.Kind is BattleEventKind.Summon or BattleEventKind.SilkBall && x.TargetId is int)) names.TryAdd(ev.TargetId!.Value, ev.Kind == BattleEventKind.SilkBall ? "糸玉" : "背いた獣");
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# som309 memo —— {name}（{Seats(f)}）× {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}・T{t0}〜T{t1}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        for (int i = 0; i < r.Events.Count; i++)
        {
            var x = r.Events[i];
            if (x.Turn < t0 || x.Turn > t1) continue;
            if (x.Kind is BattleEventKind.StatSnapshot) continue;
            if (x.Kind == BattleEventKind.StatusSnapshot && x.Text != "破片") continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"#{i,-4} {x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  hp={x.HpAfter}  Slot {x.Slot}{(x.Pattern is { } pt ? $"  {pt}" : "")}{(x.Reaction ? "  Reaction" : "")}{(x.FriendlyFire ? "  ff" : "")}");
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査（§6）
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    /// <summary>第308期の試遊の8行（名前と並び・第309期に2行を末尾に足す前）。</summary>
    internal static readonly string[] Playtest308 = { "試遊・感電 火の型", "試遊・感電 雷の型", "試遊・感電 糸", "試遊・標 ボス台", "試遊・標 道中", "試遊・標 循環", "試遊・標 三人組", "試遊・標 守り型" };

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# som309 check —— 第309期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var som = UnitCatalog.Som; var old = UnitCatalog.SomH308; var lva = UnitCatalog.SomLVa;

        // (a) 段0 の定義
        {
            bool ok = som.Traits.SequenceEqual(lva.Traits) && som.Traits.SequenceEqual(old.Traits.Append(TraitId.SparkVeil))
                && som.MaxHp == lva.MaxHp && som.Attack == lva.Attack && som.Speed == lva.Speed && som.Advances == lva.Advances && som.Pattern == lva.Pattern && ReferenceEquals(som.Actions, lva.Actions)
                && som.Name == old.Name && som.MinusText == old.MinusText && som.Flavor == old.Flavor && som.Flavor == "喚んだものは、みな背いた。弾けた光だけが、帰ってくる。"
                && som.PlusText == old.PlusText + "。満ちた仲間に降った光は、光の衣になって次の一撃を受け止める"
                && old.Traits.SequenceEqual(UnitCatalog.SomSHa.Traits) && old.PlusText == UnitCatalog.SomSHa.PlusText && old.Traits.SequenceEqual(UnitCatalog.SomH307.Traits.Append(TraitId.SparkRain))
                && UnitCatalog.All.Contains(som) && !UnitCatalog.Everyone.Contains(old) && !UnitCatalog.Everyone.Contains(lva)
                && UnitCatalog.SomLVc.Traits.SequenceEqual(old.Traits.Append(TraitId.SparkVeil).Append(TraitId.SparkVeilCap)) && UnitCatalog.SomFO.Traits.SequenceEqual(old.Traits.Append(TraitId.SparkFocus));
            Expect("(a) 段0: 規定のソム ＝ `SomLVa` と同じ中身（札・数値・行動）＝ 旧の規定（`SomH308` ＝ SH-a）＋ `SparkVeil`・名前とフレーバーはそのまま・文面に衣・旧は `All` ／ `Everyone` の外・第308期の版は旧から作る", ok);
        }
        // (b) ヒーラーの一覧
        {
            var hp = B283.HealPool.Select(Short).ToArray(); var h306 = B283.HealPool306.Select(Short).ToArray(); var h295 = B283.HealPool295.Select(Short).ToArray();
            bool ok = hp.Contains("ソム") && hp.Where(x => x != "ソム").SequenceEqual(h306)
                && h306.SequenceEqual(new[] { "ヴェル", "リリ", "シオ", "ササ", "ヒビ", "ツギ", "ベニ", "ヒサ" }) && h295.SequenceEqual(new[] { "ヴェル", "リリ", "シオ", "ササ", "ヒビ", "ツギ", "ベニ" });
            Expect("(b) `HealPool` に規定のソムが入る（第308期と同じ顔ぶれ）・`HealPool306` ／ `HealPool295` は第307期の一覧のまま", ok, $"{string.Join("・", hp)}");
        }
        // (c) 試遊の2台・`compare` の行数
        {
            var pl = Presets.Playtest;
            bool shape = pl.Length == 10 && pl.Take(8).Select(r => r.Name).SequenceEqual(Playtest308) && pl.Skip(8).Select(r => r.Name).SequenceEqual(new[] { Shield, ShieldHeavy })
                && Presets.Compare.Length == 64 && CompareBuilds().Count() == 64 && Presets.Cross.Length == 12
                && pl.All(r => !Presets.Compare.Any(c => c.Name == r.Name) && r.F.Occupied().Count() == 5 && r.F.Occupied().All(o => UnitCatalog.All.Contains(o.Def)))
                && pl.Skip(8).All(r => r.F.Shape == FormationShape.X && !r.F.HasRelics && r.F.Occupied().Any(o => ReferenceEquals(o.Def, som)) && r.F.Occupied().All(o => o.Slot < 5));
            var picks = PickPairs.Select(pp => (pp.Name, Top: S307.OrderName(Rank(pp.Pair)[0].Order))).ToList();
            bool same = picks.All(x => Seats(Playtest(x.Name)) == x.Top);
            Expect("(c) 試遊の2台が `Presets.Playtest` の末尾にある（既存の8行は名前も並びもそのまま）・`compare` の行数は 64 のまま・2台は §3-1 の選び方の1位（ボス・120 席を回し直して一致）",
                shape && same, string.Join(" ／ ", picks.Select(x => $"{x.Name}: {x.Top}")));
        }
        // (d) 入れ替えは同じ席・同じ陣形・同じレリック
        {
            bool ok = true; var notes = new List<string>();
            foreach (var (n, _, s, t) in Pairs())
            {
                if (t is null) continue;
                var diff = Enumerable.Range(0, FormationRules.PlayableSlotCount).Where(i => !ReferenceEquals(s[i], t[i])).ToList();
                bool one = diff.Count == 1 && s.Shape == t.Shape && Enumerable.Range(0, FormationRules.PlayableSlotCount).All(i => s.RelicAt(i) == t.RelicAt(i));
                bool hs = n.StartsWith("感電 行", StringComparison.Ordinal)
                    ? diff.Count == 2 && ReferenceEquals(s[diff[0]], UnitCatalog.Dolga) != ReferenceEquals(t[diff[0]], UnitCatalog.Dolga)
                    : one && ReferenceEquals(s[diff[0]], som) && ReferenceEquals(t[diff[0]], UnitCatalog.Tsugi);
                if (!hs) { ok = false; notes.Add(n); }
            }
            // 陣形とレリックを持つ台で `Swap` が両方を保つ
            var f0 = Playtest(Shield).Clone(); f0.Shape = FormationShape.Diamond; f0.SetRelic(0, RelicCatalog.Initial[0].Id);
            var g0 = Swap(f0, som, UnitCatalog.Tsugi);
            bool keep = g0.Shape == FormationShape.Diamond && g0.RelicAt(0) == f0.RelicAt(0) && Enumerable.Range(0, FormationRules.PlayableSlotCount).Count(i => !ReferenceEquals(f0[i], g0[i])) == 1;
            var p9 = Pin309(f0);
            bool pin = p9.Shape == FormationShape.Diamond && p9.RelicAt(0) == f0.RelicAt(0) && p9.Occupied().Any(o => ReferenceEquals(o.Def, old)) && !p9.Occupied().Any(o => ReferenceEquals(o.Def, som));
            Expect("(d) ソム ↔ ツギは同じ席・同じ陣形・同じレリック（`感電` 行は ソム ／ ツギ → ドルガ の対）・`Swap` ／ `Pin309` は陣形とレリックを保つ", ok && keep && pin, notes.Count == 0 ? $"{Pairs().Length} 対" : string.Join("・", notes));
        }
        // (e) 規定のソムの戦では衣が生まれ、固定（`SomH308`）の戦では生まれない
        {
            int vNew = 0, vOld = 0;
            for (int sd = 0; sd < 4; sd++)
            {
                vNew += BattleEngine.Run(BattleEngine.Materialize(Playtest(Shield), BattleContext.PlayerTeam), Waves[0].Make(), sd, verbose: true).Events.Count(x => x.Kind == BattleEventKind.Spark && x.Text == SparkLabels.Veil);
                vOld += BattleEngine.Run(BattleEngine.Materialize(Pin309(Playtest(Shield)), BattleContext.PlayerTeam), Waves[0].Make(), sd, verbose: true).Events.Count(x => x.Kind == BattleEventKind.Spark && x.Text == SparkLabels.Veil);
            }
            Expect("(e) 規定のソムの戦では「衣」が出る・`Pin309`（`SomH308`）の戦では 0", vNew > 0 && vOld == 0, $"{vNew} ／ {vOld}（光の盾 × ボス × seed 0..3）");
        }
        // (f) 決定的・verbose の有無で勝敗と決着T が同じ
        {
            int nd = 0, n = 0;
            foreach (var (_, _, s, t) in Pairs())
                foreach (var f in t is null ? new[] { s } : new[] { s, t })
                    foreach (var w in Waves)
                        for (int sd = 0; sd < 3; sd++)
                        {
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            n++;
                            if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) nd++;
                        }
            Expect("(f) 決定的・verbose の有無で勝敗と決着T が変わらない（§4-1 の台 × 8 波）", nd == 0, $"{nd} ／ {n} 件");
        }
        // (g) 乱数
        {
            static int Count(string text, string pat) { int n = 0, k = 0; while ((k = text.IndexOf(pat, k, StringComparison.Ordinal)) >= 0) { n++; k += pat.Length; } return n; }
            static string? Head(string path)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", $"show HEAD:{path}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
                    using var pr = System.Diagnostics.Process.Start(psi)!;
                    string o = pr.StandardOutput.ReadToEnd();
                    pr.WaitForExit();
                    return pr.ExitCode == 0 ? o : null;
                }
                catch { return null; }
            }
            var notes = new List<string>(); bool clean = true;
            foreach (var path in new[] { "BattleCore/BattleEngine.cs", "BattleCore/Traits.cs" })
            {
                string now = File.ReadAllText(path); string? head = Head(path);
                if (head is null) { clean = false; notes.Add($"{path}: HEAD を読めない"); continue; }
                foreach (var pat in new[] { "PickOne(", "Roll(" }) { int a0 = Count(head, pat), a1 = Count(now, pat); if (a1 > a0) clean = false; notes.Add($"{Path.GetFileName(path)} {pat} {a0}→{a1}"); }
            }
            Expect("(g) `PickOne(` ／ `Roll(` を新たに使っていない（BattleCore の出現数が HEAD 以下）", clean, string.Join("・", notes));
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
