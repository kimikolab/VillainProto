using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;

// =====================================================================================
// kugu292 —— 第292期「クグの糸玉（ボスを多体に変えて感電の燃料を増やす）」。
// 指示書は design/PHASE292_KUGU_SILKBALL_SPEC.md ／ 報告は design/PHASE292_KUGU_SILKBALL.md。
//
//     dotnet run --project BattleSim -c Release 0 kugu292 p0           # Phase 0（§3 の 3・4）: 盤面の空き席と勇者の隣・規定のクグの組み付きと連鎖（ボス）
//     dotnet run --project BattleSim -c Release 0 kugu292 boards       # 代表台 × 波（ボス ／ 近衛 ／ 大隊）× クグ（KG-b ／ KB-a ／ KB-b）× カタ（KR-b ／ KR-∞）（seed 0..199・verbose）と対照
//     dotnet run --project BattleSim -c Release 0 kugu292 grid [boss|guard|bat]   # 格子（固定枠 トウ ＋ クグ ＋ カタ・探索枠2・× 席 120）を KG-b ／ KB-a（ボス）または KB-b（精鋭）× KR-b ／ KR-∞ で
//     dotnet run --project BattleSim -c Release 0 kugu292 check        # 自己検査
//     dotnet run --project BattleSim -c Release 0 kugu292 deep <boss|guard|bat> <席の並び> [版…]   # 1台 × 4 版（KG-b ／ 糸玉 × KR-b ／ KR-∞）のターンごとの推移（seed 0..199・verbose）
//     dotnet run --project BattleSim -c Release 0 kugu292 log <boss|guard|bat> <席の並び（短い名前を ・ で5つ）> [seed] [版…]   # 1戦のログ（版: kgb kba kbb krb krinf）
//
// 駒・札・数値・波は規定のまま。版はその駒を差し替える（`FvSwap`）。**規定のクグ・カタは本期中に切り替えない**（採否はポン）。
// =====================================================================================
static class Kugu292Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": BoardsAll(); return;
            case "grid": Grid(A(3, "boss")); return;
            case "check": Check(); return;
            case "deep": DeepOne(A(3, "boss"), A(4, "トウ・ゴルム・クグ・カタ・ツギ"), args.Skip(5).ToArray()); return;
            case "log": LogOne(A(3, "boss"), A(4, "ソラ・ガルド・クグ・トウ・カタ"), args.Length > 5 ? int.Parse(args[5]) : 0, args.Skip(6).ToArray()); return;
            default: Console.WriteLine("kugu292: モードは p0 / boards / grid / check / log。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・波・台（測る前に固定）
    // ---------------------------------------------------------------------------------
    internal sealed record Ver(string Name, string Ascii, UnitDef From, UnitDef To);
    internal static readonly Ver[] KuguVers =
    {
        // 第294期: 規定のクグは KW-a になった——台は `Pin294` で第292期の規定（KG-b ＝ `KuguKGb`）に固定してあるので、版の差し替えも `KuguKGb` から
        new("KG-b", "kgb", UnitCatalog.KuguKGb, UnitCatalog.KuguKGb), new("KB-a", "kba", UnitCatalog.KuguKGb, UnitCatalog.KuguKBa), new("KB-b", "kbb", UnitCatalog.KuguKGb, UnitCatalog.KuguKBb),
    };
    internal static readonly Ver[] KataVers =
    {
        new("KR-b", "krb", UnitCatalog.Kata, UnitCatalog.KataKRb), new("KR-∞", "krinf", UnitCatalog.Kata, UnitCatalog.KataKRinf),   // 第293期: 規定は KR-∞（KR-b は `KataKRb`）——台は同じ
    };
    static Ver AnyVer(string n) => KuguVers.Concat(KataVers).First(v => v.Ascii == n || v.Name == n);
    static Formation Apply(Formation f, params Ver[] vs) { foreach (var v in vs) if (!ReferenceEquals(v.From, v.To)) f = FvSwap(f, v.From, v.To); return f; }
    static bool Has(Formation f, UnitDef d) => f.Occupied().Any(o => ReferenceEquals(o.Def, d));

    static S287.Wave WaveOf(string n) => S287.Waves.First(w => w.Name == (n switch { "boss" => "ボス", "guard" => "近衛", "bat" => "大隊", _ => n }));
    const int Seeds = 200, CutSeeds = 20;

    static string Short(UnitDef d) { var m = System.Text.RegularExpressions.Regex.Match(d.Name, @"[ァ-ヴー]+$"); return m.Success ? m.Value : d.Name; }
    static UnitDef ByShort(string n) => UnitCatalog.All.First(d => Short(d) == n);
    static UnitDef[] Order(string s) => s.Split('・', StringSplitOptions.RemoveEmptyEntries).Select(ByShort).ToArray();
    static string OrderName(UnitDef[] o) => string.Join("・", o.Select(Short));
    static Formation Seat(UnitDef[] o) => Pin294(B283.Seat(o));
    /// <summary>第294期: クグ ／ シガを第292期の規定（KG-b ＝ `KuguKGb` ／ SI-b ＝ `ShigaSIb`）に固定する。第295期: ソラも旧の規定（`SoraSR0`）に。第296期: ヒサも旧の規定（`HisaHK0`）に。</summary>
    internal static Formation Pin294(Formation f)
    {
        var g = f.Clone();   // 陣形・レリックを保つ（`Common.OldFire` と同じ作法）
        foreach (var (slot, d) in f.Occupied())
            g[slot] = ReferenceEquals(d, UnitCatalog.Kugu) ? UnitCatalog.KuguKGb : ReferenceEquals(d, UnitCatalog.Shiga) ? UnitCatalog.ShigaSIb
                    : ReferenceEquals(d, UnitCatalog.Sora) ? UnitCatalog.SoraSR0   // 第295期: ソラも（規定は SR-b・旧は `SoraSR0`）
                    : ReferenceEquals(d, UnitCatalog.Hisa) ? UnitCatalog.HisaHK0   // 第296期: ヒサも（規定は HK-b・旧は `HisaHK0`）
                    : ReferenceEquals(d, UnitCatalog.Doha) ? UnitCatalog.DohaD0   // 第298期: ドハも（規定は DH-a なまりなし・旧は `DohaD0`）
                    : ReferenceEquals(d, UnitCatalog.Tome) ? UnitCatalog.TomeMb : ReferenceEquals(d, UnitCatalog.Zan) ? UnitCatalog.ZanZN0 : d;   // 第299期: ミサ ／ ザンも（規定は MF-b ／ ZN-b・旧は `TomeMb` ／ `ZanZN0`）
        return g;
    }

    /// <summary>
    /// 代表台（指示書 §4-2）。席は測る前に固定した（決めたこと）:
    /// 「試遊・感電 糸」はプリセットのまま。「試遊・感電 雷の型」（トウ・ソラ・シガ・カタ・ドハ）の探索枠（ソラ ／ ドハ）は2枚あるので<b>両方</b>をそれぞれクグに替えた2台にした。
    /// 「トウ ＋ シガ ＋ カタ ＋ クグ ＋ 1枚」は雷の型の席にそのまま当てる（前1 トウ ／ 前3 クグ ／ 中央 シガ ／ 後1 カタ ／ 後3 1枚）——1枚がドハの台は「雷の型 ソラ → クグ」と同じ台なので、ソラ ／ ヴェルの2台だけ足す。
    /// </summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("試遊・感電 糸", Pin294(Presets.Playtest.First(r => r.Name == "試遊・感電 糸").F)),
        ("雷の型 ソラ→クグ", Seat(Order("トウ・クグ・シガ・カタ・ドハ"))),
        ("雷の型 ドハ→クグ", Seat(Order("トウ・ソラ・シガ・カタ・クグ"))),
        ("四枚 ＋ ソラ", Seat(Order("トウ・クグ・シガ・カタ・ソラ"))),
        ("四枚 ＋ ヴェル", Seat(Order("トウ・クグ・シガ・カタ・ヴェル"))),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose の台本から）
    // ---------------------------------------------------------------------------------
    internal static readonly int[] Marks = { 1, 3, 5, 10, 15, 20, 30 };

    internal sealed class Deep
    {
        public long N, Wins, WinT, LoseT, FoeDis, AllyDis;
        // 糸玉
        public long Placed, NoRoom, Far, Pops, DisIn, DisOut, DisToUnit, Dealt, FirstBallT, FirstBallN;
        public double[] BallsAt = new double[Marks.Length];
        // カタ
        public long KN, Casts, Hits, KNom, KDmg, CloudMax, KDied, KDeathT;
        public double[] CloudAt = new double[Marks.Length];
        // シガ
        public long ShN, SwFires, ShDmg, SwWhip, ChargeGains, ShDied, ShDeathT;
        // クグ ・ トウ
        public long GDied, GDeathT, TDied, TDeathT;
        // 勇者（ボスだけ）
        public long HeroN, HeroTurns, OverTurns, HeroDmg, HeroHeal;
        public double[] HeroHpAt = new double[Marks.Length];
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public void Merge(Deep o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LoseT += o.LoseT; FoeDis += o.FoeDis; AllyDis += o.AllyDis;
            Placed += o.Placed; NoRoom += o.NoRoom; Far += o.Far; Pops += o.Pops; DisIn += o.DisIn; DisOut += o.DisOut; DisToUnit += o.DisToUnit; Dealt += o.Dealt;
            FirstBallT += o.FirstBallT; FirstBallN += o.FirstBallN;
            KN += o.KN; Casts += o.Casts; Hits += o.Hits; KNom += o.KNom; KDmg += o.KDmg; if (o.CloudMax > CloudMax) CloudMax = o.CloudMax; KDied += o.KDied; KDeathT += o.KDeathT;
            ShN += o.ShN; SwFires += o.SwFires; ShDmg += o.ShDmg; SwWhip += o.SwWhip; ChargeGains += o.ChargeGains; ShDied += o.ShDied; ShDeathT += o.ShDeathT;
            GDied += o.GDied; GDeathT += o.GDeathT; TDied += o.TDied; TDeathT += o.TDeathT;
            HeroN += o.HeroN; HeroTurns += o.HeroTurns; OverTurns += o.OverTurns; HeroDmg += o.HeroDmg; HeroHeal += o.HeroHeal;
            for (int i = 0; i < Marks.Length; i++) { BallsAt[i] += o.BallsAt[i]; CloudAt[i] += o.CloudAt[i]; HeroHpAt[i] += o.HeroHpAt[i]; }
        }
    }

    internal static Deep FightDeep(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var d = new Deep { N = 1 };
        if (r.PlayerWon) { d.Wins = 1; d.WinT = r.Turns; } else d.LoseT = r.Turns;
        var mine = p.Select(u => u.Def.Id).ToHashSet();
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (id == UnitCatalog.SilkBall.Id) continue;
            if (mine.Contains(id)) d.AllyDis += t.DischargeTaken; else d.FoeDis += t.DischargeTaken;
        }
        UnitState? sh = p.FirstOrDefault(u => u.Def.Id == "shiga"), ka = p.FirstOrDefault(u => u.Def.Id == "kata"),
                   kg = p.FirstOrDefault(u => u.Def.Id == "kugu"), to = p.FirstOrDefault(u => u.Def.Id == "tou");
        if (kg is not null && r.TallyByUnit.TryGetValue("kugu", out var gt))
        {
            d.Placed = gt.SilkPlaced; d.NoRoom = gt.SilkNoRoom; d.Far = gt.SilkFar; d.Pops = gt.SilkPops; d.DisIn = gt.SilkDisIn; d.DisOut = gt.SilkDisOut;
            d.DisToUnit = gt.SilkDisToUnit; d.Dealt = gt.SilkDealt;
        }
        if (ka is not null && r.TallyByUnit.TryGetValue("kata", out var kt))
        {
            d.KN = 1; d.Casts = kt.ThunderCasts; d.Hits = kt.ThunderHits; d.KNom = kt.ThunderNominal; d.KDmg = kt.DamageToEnemy; d.CloudMax = kt.CloudAtCastMax;
        }
        if (sh is not null && r.TallyByUnit.TryGetValue("shiga", out var st))
        {
            d.ShN = 1; d.SwFires = st.SwFires; d.ShDmg = st.DamageToEnemy; d.SwWhip = st.SwWhipDealt; d.ChargeGains = st.ChargeGains;
        }
        UnitState? hero = e.FirstOrDefault(u => u.Def == EnemyCatalog.BossRegular);
        // ターンごと: 糸玉の数・雷雲（ターンの終わりの値）・勇者の HP と与ダメ ／ 回復
        int balls = 0, cloud = 0, heroHp = hero?.MaxHp ?? 0;
        var dmgT = new Dictionary<int, long>(); var healT = new Dictionary<int, long>();
        var ballsEnd = new int[31]; var cloudEnd = new int[31]; var hpEnd = new int[31];
        int lastT = 0;
        void Close(int upto) { for (int t = lastT; t <= Math.Min(30, upto); t++) { ballsEnd[t] = balls; cloudEnd[t] = cloud; hpEnd[t] = heroHp; } lastT = Math.Min(31, upto + 1); }
        foreach (var ev in r.Events)
        {
            if (ev.Turn > lastT) Close(ev.Turn - 1);
            if (ev.Kind == BattleEventKind.SilkBall && ev.Text == SilkBallLabels.Place)
            {
                balls++;
                if (d.FirstBallN == 0) { d.FirstBallN = 1; d.FirstBallT = ev.Turn; }
            }
            else if (ev.Kind == BattleEventKind.ShockGauge && ev.Text == ShockGaugeLabels.Cloud && ka is not null && ev.TargetId == ka.InstanceId) cloud = ev.Amount;
            else if (hero is not null && ev.TargetId == hero.InstanceId && ev.Kind == BattleEventKind.Damage)
            {
                int hp = ev.HpAfter;
                dmgT[ev.Turn] = dmgT.GetValueOrDefault(ev.Turn) + Math.Max(0, heroHp - hp);
                heroHp = hp;
            }
            else if (hero is not null && ev.TargetId == hero.InstanceId && ev.Kind == BattleEventKind.Heal)
            {
                healT[ev.Turn] = healT.GetValueOrDefault(ev.Turn) + ev.Amount;
                heroHp = ev.HpAfter;
            }
            else if (ev.Kind == BattleEventKind.Death)
            {
                if (sh is not null && ev.TargetId == sh.InstanceId && d.ShDied == 0) { d.ShDied = 1; d.ShDeathT = ev.Turn; }
                if (ka is not null && ev.TargetId == ka.InstanceId && d.KDied == 0) { d.KDied = 1; d.KDeathT = ev.Turn; }
                if (kg is not null && ev.TargetId == kg.InstanceId && d.GDied == 0) { d.GDied = 1; d.GDeathT = ev.Turn; }
                if (to is not null && ev.TargetId == to.InstanceId && d.TDied == 0) { d.TDied = 1; d.TDeathT = ev.Turn; }
            }
        }
        Close(30);
        for (int i = 0; i < Marks.Length; i++)
        {
            int t = Math.Min(Marks[i], r.Turns);   // 決着の後は決着のターンの値を持ち越す
            d.BallsAt[i] = ballsEnd[t]; d.CloudAt[i] = cloudEnd[t]; d.HeroHpAt[i] = hpEnd[t];
        }
        if (hero is not null)
        {
            d.HeroN = 1;
            for (int t = 1; t <= r.Turns; t++)
            {
                long dm = dmgT.GetValueOrDefault(t), hl = healT.GetValueOrDefault(t);
                d.HeroTurns++; d.HeroDmg += dm; d.HeroHeal += hl;
                if (dm > hl) d.OverTurns++;
            }
        }
        return d;
    }

    internal static Deep MeasureDeep(Formation f, S287.Wave w, int n)
    {
        var parts = new Deep[n];
        Parallel.For(0, n, i => parts[i] = FightDeep(f, w, i));
        var all = new Deep();
        foreach (var d in parts) all.Merge(d);
        return all;
    }

    static bool FightLite(Formation f, S287.Wave w, int seed, out int turns)
    {
        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), seed, verbose: false);
        turns = r.Turns;
        return r.PlayerWon;
    }
    static int Wins(Formation f, S287.Wave w, int n, out long winT)
    {
        int wins = 0; winT = 0;
        for (int s = 0; s < n; s++) if (FightLite(f, w, s, out int t)) { wins++; winT += t; }
        return wins;
    }
    static int WinsPar(Formation f, S287.Wave w, int n)
    {
        int wins = 0;
        Parallel.For(0, n, s => { if (FightLite(f, w, s, out _)) Interlocked.Increment(ref wins); });
        return wins;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Per0(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F0");
    static string Pct(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1") + "%";
    static string Avg(double s, long n, string fmt = "F1") => n == 0 ? "—" : (s / n).ToString(fmt);

    // ---------------------------------------------------------------------------------
    // Phase 0（§3 の 3・4・5）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第292期 Phase 0 —— 盤面の空き席・勇者の隣・規定のクグ（KG-b）の組み付きと連鎖");
        Console.WriteLine();
        Console.WriteLine("## §3 の 3 盤面の空き席（敵陣・X 字・9 席）");
        Console.WriteLine();
        Console.WriteLine("| 席 | 名前 | 隣の席 |");
        Console.WriteLine("|--:|---|---|");
        for (int s = 0; s < FormationRules.TotalSlots; s++)
            Console.WriteLine($"| {s} | {FormationRules.SeatNames[s]} | {string.Join("・", Enumerable.Range(0, FormationRules.TotalSlots).Where(t => FormationRules.AreAdjacent(s, t)).Select(t => FormationRules.SeatNames[t]))} |");
        Console.WriteLine();
        foreach (var w in S287.Waves)
        {
            var e = w.Make();
            var occ = e.Select(u => u.Slot).ToHashSet();
            var empty = Enumerable.Range(0, FormationRules.TotalSlots).Where(s => !occ.Contains(s)).ToList();
            Console.WriteLine($"- **{w.Name}**: 駒 {e.Count} 体（{string.Join("・", e.Select(u => FormationRules.SeatNames[u.Slot]))}）・空き席 {empty.Count}（{string.Join("・", empty.Select(s => FormationRules.SeatNames[s]))}）・陣形 {e[0].Shape.GetType().Name}");
            if (w.Boss)
            {
                var hero = e.First();
                var adj = empty.Where(s => hero.Shape.AreAdjacent(hero.Slot, s)).ToList();
                Console.WriteLine($"  - 勇者（{FormationRules.SeatNames[hero.Slot]}）の隣の空き席 {adj.Count}（{string.Join("・", adj.Select(s => FormationRules.SeatNames[s]))}）");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## §3 の 4 糸玉の張り方の見込み（KB-a × ボス・決定的に張る順）");
        Console.WriteLine();
        {
            var e = S287.Waves[0].Make();
            var hero = e.First();
            var taken = new HashSet<int> { hero.Slot };
            var order = new List<int>();
            for (int k = 0; k < FormationRules.TotalSlots; k++)
            {
                int slot = -1;
                for (int s = 0; s < FormationRules.TotalSlots && slot < 0; s++) if (hero.Shape.AreAdjacent(hero.Slot, s) && !taken.Contains(s)) slot = s;
                for (int s = 0; s < FormationRules.TotalSlots && slot < 0; s++) if (!taken.Contains(s)) slot = s;
                if (slot < 0) break;
                taken.Add(slot); order.Add(slot);
            }
            Console.WriteLine("| n 個目 | 席 | 勇者の隣 | その時点で勇者の隣の糸玉 | 勇者が1回弾けたときに返る放電（本数 ／ 量） | 雷雲（KR-b）の増え（勇者 1 ＋ 弾けた糸玉） |");
            Console.WriteLine("|--:|---|---|--:|--:|--:|");
            for (int k = 0; k < order.Count; k++)
            {
                var placed = order.Take(k + 1).ToList();
                int nAdj = placed.Count(s => hero.Shape.AreAdjacent(hero.Slot, s));
                // 連鎖: 勇者 → 隣の糸玉 → 糸玉どうし（幅優先・1つ1回）。糸玉は勇者へ1本ずつ返す（勇者は既に弾けているので弾けない）。
                var popped = new HashSet<int> { hero.Slot };
                var q = new Queue<int>(); q.Enqueue(hero.Slot);
                int back = 0;
                while (q.Count > 0)
                {
                    int x = q.Dequeue();
                    foreach (int b in placed)
                        if (b != x && hero.Shape.AreAdjacent(x, b) && popped.Add(b)) q.Enqueue(b);
                    if (x != hero.Slot && hero.Shape.AreAdjacent(x, hero.Slot)) back++;
                }
                Console.WriteLine($"| {k + 1} | {FormationRules.SeatNames[placed[k]]} | {(hero.Shape.AreAdjacent(hero.Slot, placed[k]) ? "○" : "")} | {nAdj} | {back} ／ {back * ShockRule.Discharge} | +{popped.Count} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 規定のクグ（KG-b）× ボス（代表台・seed 0..199・verbose）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 勇者 与ダメ ／ 回復（1戦） | 上回ったターン（1戦 ／ 割合） | カタ 与ダメ（1戦） | シガ 与ダメ（1戦） | 雷雲 T5 ／ T10 ／ T20 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in Boards())
        {
            var a = MeasureDeep(Apply(f, KataVers[0]), S287.Waves[0], Seeds);   // 第293期: 第292期の規定のカタ（KR-b）に固定
            Console.WriteLine($"| {name} | {F1(a.Win)} | {Per0(a.HeroDmg, a.N)} ／ {Per0(a.HeroHeal, a.N)} | {Per1(a.OverTurns, a.N)} ／ {Pct(a.OverTurns, a.HeroTurns)} | {Per0(a.KDmg, a.KN)} | {Per0(a.ShDmg, a.ShN)} | "
                + $"{Avg(a.CloudAt[2], a.N)} ／ {Avg(a.CloudAt[3], a.N)} ／ {Avg(a.CloudAt[5], a.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 代表台 × 波 × クグ × カタ
    // ---------------------------------------------------------------------------------
    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var bs = Boards();
        var cells = new List<(string Board, S287.Wave W, Ver K, Ver C, Formation F)>();
        foreach (var w in S287.Waves)
            foreach (var (name, f) in bs)
                foreach (var kc in KataVers)
                    foreach (var kg in KuguVers)
                        cells.Add((name, w, kg, kc, Apply(f, kg, kc)));
        var res = cells.Select(c => MeasureDeep(c.F, c.W, Seeds)).ToArray();

        Console.WriteLine("# 第292期 代表台 × 波 × クグ（KG-b ／ KB-a ／ KB-b）× カタ（KR-b ／ KR-∞）（seed 0..199・verbose）");
        Console.WriteLine();
        foreach (var (name, f) in bs) Console.WriteLine($"- {name}: {string.Join("・", Enumerable.Range(0, 5).Select(i => f[i] is { } d ? Short(d) : "—"))}（前1・前3・中央・後1・後3）");
        Console.WriteLine();
        Console.WriteLine("**糸玉の勝ち ＝ 同じ台・同じカタの KG-b（糸玉なし）との差。** 糸玉の列は1戦あたり。勇者の列はボスだけ。");
        foreach (var w in S287.Waves)
        {
            Console.WriteLine();
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("### 表A 勝率と糸玉");
            Console.WriteLine();
            Console.WriteLine("| 台 | カタ | クグ | 勝率 | KG-b との差 | 倒しT | 負けT | 張った | 隣に空き無し ／ 席なし | 弾けた | 糸玉へ届いた放電 | 糸玉が駒へ返した放電（本 ／ 削った HP） | 最初に張ったT | 糸玉 T1 ／ T5 ／ T10 ／ T20 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].W != w) continue;
                var a = res[i];
                int bi = cells.FindIndex(c => c.W == w && c.Board == cells[i].Board && c.C == cells[i].C && c.K == KuguVers[0]);
                double diff = a.Win - res[bi].Win;
                Console.WriteLine($"| {cells[i].Board} | {cells[i].C.Name} | {cells[i].K.Name} | {F1(a.Win)} | {(cells[i].K == KuguVers[0] ? "" : diff.ToString("+0.0;-0.0;0.0"))} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)} | "
                    + $"{Per(a.Placed, a.N)} | {Per(a.Far, a.N)} ／ {Per(a.NoRoom, a.N)} | {Per(a.Pops, a.N)} | {Per(a.DisIn, a.N)} | {Per(a.DisToUnit, a.N)} ／ {Per1(a.Dealt, a.N)} | {Per1(a.FirstBallT, a.FirstBallN)} | "
                    + $"{Avg(a.BallsAt[0], a.N)} ／ {Avg(a.BallsAt[2], a.N)} ／ {Avg(a.BallsAt[3], a.N)} ／ {Avg(a.BallsAt[5], a.N)} |");
            }
            Console.WriteLine();
            Console.WriteLine("### 表B カタ・シガ・倒れた駒・放電");
            Console.WriteLine();
            Console.WriteLine("| 台 | カタ | クグ | 雷（1戦） | 雷の1発 | カタ 与ダメ | 雷雲 T1 ／ T5 ／ T10 ／ T20 ／ T30 | 雷雲の最大 | シガ 割り込み | シガ 蓄電 +（1戦） | シガ 与ダメ | 倒れたT（割合）カタ ／ シガ ／ クグ ／ トウ | 放電 敵 ／ 味方 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|---|--:|");
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i].W != w) continue;
                var a = res[i];
                string Dt(long died, long t) => died == 0 ? "—" : $"{Per1(t, died)}（{Pct(died, a.N)}）";
                Console.WriteLine($"| {cells[i].Board} | {cells[i].C.Name} | {cells[i].K.Name} | {Per(a.Casts, a.KN)} | {Per1(a.KNom, a.Hits)} | {Per0(a.KDmg, a.KN)} | "
                    + $"{Avg(a.CloudAt[0], a.N)} ／ {Avg(a.CloudAt[2], a.N)} ／ {Avg(a.CloudAt[3], a.N)} ／ {Avg(a.CloudAt[5], a.N)} ／ {Avg(a.CloudAt[6], a.N)} | {a.CloudMax} | "
                    + $"{Per(a.SwFires, a.ShN)} | {Per(a.ChargeGains, a.ShN)} | {Per0(a.ShDmg, a.ShN)} | {Dt(a.KDied, a.KDeathT)} ／ {Dt(a.ShDied, a.ShDeathT)} ／ {Dt(a.GDied, a.GDeathT)} ／ {Dt(a.TDied, a.TDeathT)} | "
                    + $"{Per1(a.FoeDis, a.N)} ／ {Per1(a.AllyDis, a.N)} |");
            }
            if (w.Boss)
            {
                Console.WriteLine();
                Console.WriteLine("### 表C 勇者（HP 3,000・ターン頭に最大HPの 40% 回復）");
                Console.WriteLine();
                Console.WriteLine("| 台 | カタ | クグ | 与ダメ ／ 回復（1戦） | 与ダメ ／ 回復（1ターン） | **与ダメが回復を上回ったターン**（1戦 ／ 割合） | 勇者の HP T1 ／ T5 ／ T10 ／ T20 ／ T30 |");
                Console.WriteLine("|---|---|---|--:|--:|--:|--:|");
                for (int i = 0; i < cells.Count; i++)
                {
                    if (cells[i].W != w) continue;
                    var a = res[i];
                    Console.WriteLine($"| {cells[i].Board} | {cells[i].C.Name} | {cells[i].K.Name} | {Per0(a.HeroDmg, a.N)} ／ {Per0(a.HeroHeal, a.N)} | {Per0(a.HeroDmg, a.HeroTurns)} ／ {Per0(a.HeroHeal, a.HeroTurns)} | "
                        + $"**{Per1(a.OverTurns, a.N)}** ／ {Pct(a.OverTurns, a.HeroTurns)} | {Avg(a.HeroHpAt[0], a.N, "F0")} ／ {Avg(a.HeroHpAt[2], a.N, "F0")} ／ {Avg(a.HeroHpAt[3], a.N, "F0")} ／ {Avg(a.HeroHpAt[5], a.N, "F0")} ／ {Avg(a.HeroHpAt[6], a.N, "F0")} |");
                }
            }
        }
        // 対照（R383）: 勝率 50% 以上の台で、固定枠の駒それぞれ → ドルガ（同じ版のまま）
        Console.WriteLine();
        Console.WriteLine("## 対照（R383）: 勝率 50% 以上のセルで駒 → ドルガ（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 台 | カタ | クグ | 勝率 | クグ → ドルガ | カタ → ドルガ | トウ → ドルガ | シガ → ドルガ |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|");
        for (int i = 0; i < cells.Count; i++)
        {
            if (res[i].Win < 50) continue;
            var f = cells[i].F;
            string C(string id) { var o = f.Occupied().FirstOrDefault(x => x.Def.Id == id); return o.Def is null ? "—" : F1(100.0 * WinsPar(FvSwap(f, o.Def, UnitCatalog.Dolga), cells[i].W, Seeds) / Seeds); }
            Console.WriteLine($"| {cells[i].W.Name} | {cells[i].Board} | {cells[i].C.Name} | {cells[i].K.Name} | {F1(res[i].Win)} | {C("kugu")} | {C("kata")} | {C("tou")} | {C("shiga")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 格子（指示書 §4-2）: 固定枠 トウ ＋ クグ ＋ カタ・探索枠2 × 席 120
    // ---------------------------------------------------------------------------------
    /// <summary>探索枠の候補 ＝ 第287期の候補（`Shock287Diag.Pool`）からトウ・クグ・カタを除いた枠（ヒーラー ≦ 1）。シガは第287期の固定枠だったので候補に無い（指示書のまま）。</summary>
    internal static UnitDef[] GridPool => S287.Pool.Where(d => d.Id is not ("tou" or "kugu" or "kata")).ToArray();

    internal static List<UnitDef[]> Pairs(UnitDef[] pool)
    {
        var heal = S287.Heal;
        var l = new List<UnitDef[]>();
        for (int a = 0; a < pool.Length; a++)
            for (int b = a + 1; b < pool.Length; b++)
                if ((heal.Contains(pool[a]) ? 1 : 0) + (heal.Contains(pool[b]) ? 1 : 0) <= 1) l.Add(new[] { pool[a], pool[b] });
        return l;
    }

    sealed record GridRes(UnitDef[] Order, int[] Wins, long[] WinT, int[][] Ctl);

    static void Grid(string waveName)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var w = WaveOf(waveName);
        var kSilk = w.Boss ? KuguVers[1] : KuguVers[2];   // ボスは KB-a、精鋭は KB-b（指示書 §4-2）
        var vars = new (Ver K, Ver C)[] { (KuguVers[0], KataVers[0]), (KuguVers[0], KataVers[1]), (kSilk, KataVers[0]), (kSilk, KataVers[1]) };
        var pool = GridPool;
        var lu = Pairs(pool);
        var fixedU = new[] { UnitCatalog.Tou, UnitCatalog.Kugu, UnitCatalog.Kata };
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedU.Concat(t).ToArray()));
        var res = new GridRes?[boards.Count];
        int cutPass = 0;
        Parallel.For(0, boards.Count, i =>
        {
            var f0 = Seat(boards[i]);
            var fs = vars.Select(v => Apply(f0, v.K, v.C)).ToArray();
            if (!fs.Any(f => Wins(f, w, CutSeeds, out _) >= 5)) return;
            Interlocked.Increment(ref cutPass);
            var wins = new int[vars.Length]; var wt = new long[vars.Length]; var ctl = new int[vars.Length][];
            for (int v = 0; v < vars.Length; v++)
            {
                wins[v] = Wins(fs[v], w, Seeds, out wt[v]);
                ctl[v] = new[] { -1, -1, -1 };
                if (wins[v] * 2 < Seeds) continue;
                var f = fs[v];
                ctl[v] = new[]
                {
                    Wins(FvSwap(f, UnitCatalog.Tou, UnitCatalog.Dolga), w, Seeds, out _),
                    Wins(FvSwap(f, vars[v].K.To, UnitCatalog.Dolga), w, Seeds, out _),
                    Wins(FvSwap(f, vars[v].C.To, UnitCatalog.Dolga), w, Seeds, out _),
                };
            }
            res[i] = new GridRes(boards[i], wins, wt, ctl);
        });
        var all = res.Where(r => r is not null).Select(r => r!).ToList();
        bool Reach(GridRes r, int v) => r.Wins[v] * 2 >= Seeds && r.Ctl[v][0] * 2 <= r.Wins[v];
        string VName(int v) => $"{vars[v].K.Name} × {vars[v].C.Name}";
        string Key(UnitDef[] o) => string.Join(",", o.Select(d => d.Id).OrderBy(s => s, StringComparer.Ordinal));

        Console.WriteLine($"# 第292期 格子 × {w.Name} × 固定枠 トウ ＋ クグ ＋ カタ（探索枠2）");
        Console.WriteLine();
        Console.WriteLine($"探索枠2（候補 {pool.Length} 枚: {OrderName(pool)}・ヒーラー ≦ 1）＝ {lu.Count} 組 × 席 120 ＝ {boards.Count:N0} 台 × 版 {vars.Length}（{string.Join(" ／ ", Enumerable.Range(0, vars.Length).Select(VName))}）。");
        Console.WriteLine($"足切り seed 0..{CutSeeds - 1}（どれかの版で 5 勝以上・{cutPass:N0} 台が通った）→ 4 版とも seed 0..{Seeds - 1} → 勝率 50% 以上の版でドルガ対照（トウ ／ クグ ／ カタ → ドルガ）。届いた ＝ トウ → ドルガで半分以下に落ちる（第287期と同じ）。");
        Console.WriteLine();
        Console.WriteLine("## 表1 段ごとの台数");
        Console.WriteLine();
        Console.WriteLine("| 段 | " + string.Join(" | ", Enumerable.Range(0, vars.Length).Select(VName)) + " |");
        Console.WriteLine("|---|" + string.Concat(vars.Select(_ => "--:|")));
        void Row(string label, Func<GridRes, int, bool> pred) => Console.WriteLine($"| {label} | " + string.Join(" | ", Enumerable.Range(0, vars.Length).Select(v => all.Count(r => pred(r, v)).ToString("N0"))) + " |");
        Row("勝率 ≧ 50%", (r, v) => r.Wins[v] * 2 >= Seeds);
        Row("**届いた**", Reach);
        Row("届いた台のうち クグが要る（クグ → ドルガで半分以下）", (r, v) => Reach(r, v) && r.Ctl[v][1] * 2 <= r.Wins[v]);
        Row("届いた台のうち カタが要る", (r, v) => Reach(r, v) && r.Ctl[v][2] * 2 <= r.Wins[v]);
        Row("勝率 100%", (r, v) => r.Wins[v] == Seeds);
        Console.WriteLine();
        Console.WriteLine("## 表2 糸玉の版と規定（KG-b）の同じ台の比較（同じカタ）");
        Console.WriteLine();
        Console.WriteLine("「届いた」はトウの要否で切る第287期の定義なので、糸玉の効きは**勝率 50% の線**でも並べる（決めたこと）。");
        Console.WriteLine();
        Console.WriteLine("| カタ | 糸玉で届いた台 | うち KG-b では届かない（**糸玉が届かせた**） | KG-b で届いて糸玉で届かない | 糸玉で 50% 以上 | うち KG-b では 50% 未満 | 全台の勝率の平均 KG-b → 糸玉 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        for (int c = 0; c < 2; c++)
        {
            int vb = c, vs = 2 + c;
            int rs = all.Count(r => Reach(r, vs)), only = all.Count(r => Reach(r, vs) && !Reach(r, vb)), lost = all.Count(r => Reach(r, vb) && !Reach(r, vs));
            int h = all.Count(r => r.Wins[vs] * 2 >= Seeds), hOnly = all.Count(r => r.Wins[vs] * 2 >= Seeds && r.Wins[vb] * 2 < Seeds);
            double mb = all.Count == 0 ? 0 : all.Average(r => 100.0 * r.Wins[vb] / Seeds), ms = all.Count == 0 ? 0 : all.Average(r => 100.0 * r.Wins[vs] / Seeds);
            Console.WriteLine($"| {KataVers[c].Name} | {rs:N0} | **{only:N0}** | {lost:N0} | {h:N0} | **{hOnly:N0}** | {F1(mb)} → {F1(ms)}（足切りを通った {all.Count:N0} 台） |");
        }
        Console.WriteLine();
        Console.WriteLine("## 表3 駒別の到達寄与（届いた台にその駒が入っていた数）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 全体の台（分母） | " + string.Join(" | ", Enumerable.Range(0, vars.Length).Select(VName)) + " |");
        Console.WriteLine("|---|--:|" + string.Concat(vars.Select(_ => "--:|")));
        foreach (var d in pool)
        {
            int denom = boards.Count(o => o.Contains(d));
            Console.WriteLine($"| {d.Name} | {denom:N0} | " + string.Join(" | ", Enumerable.Range(0, vars.Length).Select(v => all.Count(r => Reach(r, v) && r.Order.Contains(d)).ToString("N0"))) + " |");
        }
        for (int v = 0; v < vars.Length; v++)
        {
            Console.WriteLine();
            Console.WriteLine($"## 表4-{v + 1} 上位の台（{VName(v)}・勝率 50% 以上・上位 15）");
            Console.WriteLine();
            int vb = v % 2;   // 同じカタの KG-b
            Console.WriteLine($"| # | 席（前1・前3・中央・後1・後3） | 勝率 | 倒しT | 届いた | トウ → ドルガ | クグ → ドルガ | カタ → ドルガ | 同じ台の {VName(vb)} |");
            Console.WriteLine("|--:|---|--:|--:|---|--:|--:|--:|--:|");
            var top = all.Where(r => r.Wins[v] * 2 >= Seeds).OrderByDescending(r => r.Wins[v]).ThenBy(r => (double)r.WinT[v] / Math.Max(1, r.Wins[v])).ThenBy(r => OrderName(r.Order), StringComparer.Ordinal).ToList();
            for (int i = 0; i < Math.Min(15, top.Count); i++)
            {
                var r = top[i];
                Console.WriteLine($"| {i + 1} | {OrderName(r.Order)} | {F1(100.0 * r.Wins[v] / Seeds)} | {Per1(r.WinT[v], r.Wins[v])} | {(Reach(r, v) ? "○" : "")} | {F1(100.0 * r.Ctl[v][0] / Seeds)} | {F1(100.0 * r.Ctl[v][1] / Seeds)} | {F1(100.0 * r.Ctl[v][2] / Seeds)} | {(v == vb ? "" : F1(100.0 * r.Wins[vb] / Seeds))} |");
            }
            if (v >= 2 && top.Count > 0)
            {
                var reps = new List<GridRes>();
                foreach (var r in top) { if (reps.Any(q => Key(q.Order) == Key(r.Order))) continue; reps.Add(r); if (reps.Count == 3) break; }
                Console.WriteLine();
                Console.WriteLine($"中身（上位の組が重ならない3台・{VName(v)} と同じ台の {VName(vb)}・seed 0..199・verbose）");
                Console.WriteLine();
                Console.WriteLine("| 席 | 版 | 勝率 | 倒しT | 張った ／ 弾けた（1戦） | 返した放電（本） | 雷雲 T5 ／ T10 ／ T20 | カタ 与ダメ | 勇者 与ダメ ／ 回復（1戦） | 上回ったターン |");
                Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
                foreach (var r in reps)
                    foreach (int vv in new[] { v, vb })
                    {
                        var a = MeasureDeep(Apply(Seat(r.Order), vars[vv].K, vars[vv].C), w, Seeds);
                        Console.WriteLine($"| {OrderName(r.Order)} | {VName(vv)} | {F1(a.Win)} | {Per1(a.WinT, a.Wins)} | {Per(a.Placed, a.N)} ／ {Per(a.Pops, a.N)} | {Per(a.DisToUnit, a.N)} | "
                            + $"{Avg(a.CloudAt[2], a.N)} ／ {Avg(a.CloudAt[3], a.N)} ／ {Avg(a.CloudAt[5], a.N)} | {Per0(a.KDmg, a.KN)} | {Per0(a.HeroDmg, a.N)} ／ {Per0(a.HeroHeal, a.N)} | {Per1(a.OverTurns, a.N)} |");
                    }
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 表5 足切りを通った台すべて（4 版の勝率）");
        Console.WriteLine();
        Console.WriteLine("| 席（前1・前3・中央・後1・後3） | " + string.Join(" | ", Enumerable.Range(0, vars.Length).Select(VName)) + " |");
        Console.WriteLine("|---|" + string.Concat(vars.Select(_ => "--:|")));
        foreach (var r in all.OrderByDescending(r => r.Wins.Max()).ThenBy(r => OrderName(r.Order), StringComparer.Ordinal))
            Console.WriteLine($"| {OrderName(r.Order)} | " + string.Join(" | ", r.Wins.Select(x => F1(100.0 * x / Seeds))) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# kugu292 自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        // (a) 規定は動かない・版の札
        Expect("(a) 第292期の規定のクグ（KG-b）は `KuguKGb`（第294期から規定は KW-a）・カタの KR-b は `KataKRb`（第293期から規定は KR-∞）", UnitCatalog.KuguKGb.Traits.SequenceEqual(new[] { TraitId.Grapple, TraitId.Thread, TraitId.ThreadCharge })
            && UnitCatalog.KataKRb.Traits.Last() == TraitId.ThundercloudKeep && !UnitCatalog.KataKRb.Traits.Contains(TraitId.ThundercloudUncapped) && ReferenceEquals(UnitCatalog.KataKRinf, UnitCatalog.Kata));
        Expect("(b) 版は規定の末尾に札を1枚足しただけ", UnitCatalog.KuguKBa.Traits.SequenceEqual(UnitCatalog.KuguKGb.Traits.Append(TraitId.SilkBallSteadfast))
            && UnitCatalog.KuguKBb.Traits.SequenceEqual(UnitCatalog.KuguKGb.Traits.Append(TraitId.SilkBallEvery))
            && UnitCatalog.KataKRinf.Traits.SequenceEqual(UnitCatalog.KataKRb.Traits.Append(TraitId.ThundercloudUncapped))
            && UnitCatalog.KuguKBa.MaxHp == UnitCatalog.KuguKGb.MaxHp && UnitCatalog.KataKRinf.Attack == UnitCatalog.Kata.Attack);
        Expect("(c) 糸玉は編成に選べない（All ／ Retired ／ Everyone のどれにも無い）", !UnitCatalog.All.Contains(UnitCatalog.SilkBall) && !UnitCatalog.Retired.Contains(UnitCatalog.SilkBall)
            && !UnitCatalog.Everyone.Contains(UnitCatalog.SilkBall));
        Expect("(d) KR-∞ の文面は KR-b のまま", UnitCatalog.KataKRinf.PlusText == UnitCatalog.KataKRb.PlusText && !UnitCatalog.KataKRinf.PlusText.Contains('8'));

        var boss = S287.Waves[0];
        var f0 = Pin294(Presets.Playtest.First(r => r.Name == "試遊・感電 糸").F);
        // (e)〜(j) ボス × KB-a × KR-∞ を 40 seed・verbose で
        int balls = 0, ballDamage = 0, ballAttacked = 0, ballWhip = 0, winsWithBalls = 0, dupSlots = 0, cloudOver8Inf = 0, cloudOver8B = 0, recharge = 0, ballPops = 0, ballInUnits = 0;
        // ボス × KB-a × KR-∞ ／ ボス × KB-b × KR-b ／ 近衛 × KB-b × KR-b（勝てる波で「糸玉が残った勝ち」を見る）
        foreach (var (kv, cv, wv) in new[] { (KuguVers[1], KataVers[1], boss), (KuguVers[2], KataVers[0], boss), (KuguVers[2], KataVers[0], S287.Waves[1]) })
        {
            var f = Apply(f0, kv, cv);
            for (int s = 0; s < 40; s++)
            {
                var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var e = wv.Make();
                var r = BattleEngine.Run(p, e, s, verbose: true);
                var ballIds = new HashSet<int>();
                var slots = new HashSet<(int, int)>();
                foreach (var ev in r.Events)
                {
                    if (ev.Kind == BattleEventKind.SilkBall && ev.Text == SilkBallLabels.Place)
                    {
                        balls++; ballIds.Add(ev.TargetId!.Value);
                        if (!slots.Add((ev.Team ?? -1, ev.Slot))) dupSlots++;
                        if (p.Concat(e).Any(u => u.InstanceId == ev.TargetId)) ballInUnits++;
                    }
                    if (ev.Kind == BattleEventKind.SilkBall && ev.Text == SilkBallLabels.Recharge) recharge++;
                    if (ev.Kind == BattleEventKind.SilkBall && ev.Text == SilkBallLabels.Pop) ballPops++;
                    if (ev.TargetId is int tid && ballIds.Contains(tid))
                    {
                        if (ev.Kind is BattleEventKind.Damage or BattleEventKind.Heal) ballDamage++;
                        if (ev.Kind is BattleEventKind.Attack or BattleEventKind.Thunder) ballAttacked++;
                    }
                    if (ev.Kind == BattleEventKind.ShockGauge && ev.Text == ShockGaugeLabels.Cloud && ev.Amount > ThundercloudTrait.KeepCap) { if (cv == KataVers[1]) cloudOver8Inf++; else cloudOver8B++; }
                    if (ev.Kind == BattleEventKind.ShockGauge && ev.Text == ShockGaugeLabels.Interrupt && ev.TargetId is int wt && ballIds.Contains(wt)) ballWhip++;
                }
                if (r.PlayerWon && ballIds.Count > 0) winsWithBalls++;
            }
        }
        Expect("(e) 糸玉が張られ、帯電し直し、弾ける（ボス × KB-a ／ KB-b・近衛 × KB-b）", balls > 0 && recharge > 0 && ballPops > 0, $"張った {balls}・帯電し直し {recharge}・弾け {ballPops}（120 戦）");
        Expect("(f) 糸玉の HP は減らない（Damage ／ Heal の的に1度もならない）", ballDamage == 0, $"{ballDamage} 件");
        Expect("(g) 味方の攻撃・雷・割り込みの的に1度もならない", ballAttacked == 0 && ballWhip == 0, $"攻撃・雷 {ballAttacked} 件 ／ 割り込み {ballWhip} 件");
        Expect("(h) 糸玉は勝敗に数えない（糸玉が残っていても敵の駒が倒れきれば勝ち）", winsWithBalls > 0, $"糸玉の残った勝ち {winsWithBalls} 戦");
        Expect("(i) 糸玉は駒の列に入らず、同じ席に2つ張らない", ballInUnits == 0 && dupSlots == 0, $"駒の列 {ballInUnits} ／ 重複 {dupSlots}");
        Expect("(j) 雷雲: KR-∞ は 8 を超え、KR-b は超えない", cloudOver8Inf > 0 && cloudOver8B == 0, $"超えた KR-∞ {cloudOver8Inf} ／ KR-b {cloudOver8B}");

        // (k) 戦の終わりに残らない: 会戦（2戦）の2戦目で、糸玉を張る前に糸玉の出来事が出ない
        {
            int leaked = 0, battles = 0;
            var fk = Apply(f0, KuguVers[2]);
            for (int s = 0; s < 10; s++)
            {
                var er = EngagementEngine.Run(new[] { fk }, new[] { EnemyCatalog.Stages[1].Enemy, EnemyCatalog.Stages[2].Enemy }, s, verbose: true);
                foreach (var br in er.Battles)
                {
                    battles++;
                    var known = new HashSet<int>();
                    foreach (var ev in br.Events)
                    {
                        if (ev.Kind == BattleEventKind.SilkBall && ev.Text == SilkBallLabels.Place) known.Add(ev.TargetId!.Value);
                        else if (ev.Kind == BattleEventKind.SilkBall && ev.TargetId is int id && !known.Contains(id)) leaked++;
                        else if (ev.Kind == BattleEventKind.SilkBall && ev.Text == SilkBallLabels.Recharge && known.Count == 0) leaked++;
                    }
                }
            }
            Expect("(k) 糸玉は戦の終わりに消える（会戦の次の戦に持ち越さない）", leaked == 0 && battles > 10, $"持ち越し {leaked} 件・{battles} 戦");
        }
        // (l) KB-a × 精鋭（止められる相手）は規定と1ビットも違わない（糸玉を張らない）・乱数を引かない
        {
            int diff = 0, placed = 0;
            foreach (var w in new[] { S287.Waves[1], S287.Waves[2] })
                for (int s = 0; s < 40; s++)
                {
                    var ra = BattleEngine.Run(BattleEngine.Materialize(f0, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                    var rb = BattleEngine.Run(BattleEngine.Materialize(Apply(f0, KuguVers[1]), BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                    placed += rb.Events.Count(ev => ev.Kind == BattleEventKind.SilkBall);
                    if (ra.PlayerWon != rb.PlayerWon || ra.Turns != rb.Turns || ra.Events.Count != rb.Events.Count) diff++;
                }
            Expect("(l) KB-a × 精鋭は規定（KG-b）と同じ（糸玉 0・勝敗・決着T・台本の件数）", diff == 0 && placed == 0, $"違い {diff} 戦・糸玉 {placed}");
        }
        // (m) 決定性・verbose の有無で結果が変わらない
        {
            int nd = 0;
            var f = Apply(f0, KuguVers[2], KataVers[1]);
            foreach (var w in S287.Waves)
                for (int s = 0; s < 20; s++)
                {
                    var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                    var r2 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false);
                    var r3 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: true);
                    if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns || r1.Events.Count != r3.Events.Count) nd++;
                }
            Expect("(m) 決定的・verbose の有無で勝敗と決着T が変わらない", nd == 0, $"{nd} 件");
        }
        // (n) 乱数: 糸玉の札は PickOne も Roll も使わない（ソースの走査）
        {
            string src = File.ReadAllText("BattleCore/BattleEngine.cs");
            int a = src.IndexOf("readonly List<UnitState> _silkBalls", StringComparison.Ordinal), b = src.IndexOf("/// <summary>手番の頭のクグの帳簿", StringComparison.Ordinal);
            string grap = File.ReadAllText("BattleCore/Traits.cs");
            int g0 = grap.IndexOf("第292期（糸玉）", StringComparison.Ordinal), g1 = g0 < 0 ? -1 : grap.IndexOf("ctx.PlaceSilkBall(self, pick);", g0, StringComparison.Ordinal);
            string seg = a >= 0 && b > a ? src[a..b] : "", gseg = g0 >= 0 && g1 > g0 ? grap[g0..g1] : "";
            Expect("(n) 糸玉の口（張る・帯電し直し・糸玉への放電・組み付きの後の判定）に PickOne ／ Roll が無い", seg.Length > 0 && gseg.Length > 0
                && !seg.Contains("PickOne") && !seg.Contains("Roll(") && !gseg.Contains("PickOne") && !gseg.Contains("Roll("), $"{seg.Length} ／ {gseg.Length} 字");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }

    // ---------------------------------------------------------------------------------
    // 1台のターンごとの推移
    // ---------------------------------------------------------------------------------
    static void DeepOne(string wave, string order, string[] extra)
    {
        var w = WaveOf(wave);
        var f0 = Seat(Order(order));
        var kSilk = w.Boss ? KuguVers[1] : KuguVers[2];
        var vars = new (Ver K, Ver C)[] { (KuguVers[0], KataVers[0]), (KuguVers[0], KataVers[1]), (kSilk, KataVers[0]), (kSilk, KataVers[1]) };
        Console.WriteLine($"# kugu292 deep —— {order} × {w.Name}（seed 0..199・verbose・ターンの終わりの値・決着の後は決着のターンの値を持ち越す）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率 | 倒しT | 負けT | 張った ／ 弾けた | 返した放電（本 ／ HP） | カタ 雷 ／ 1発 ／ 与ダメ | 勇者 与ダメ ／ 回復 | 上回ったターン | 倒れたT カタ ／ クグ ／ トウ |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        var res = vars.Select(v => MeasureDeep(Apply(f0, v.K, v.C), w, Seeds)).ToArray();
        for (int v = 0; v < vars.Length; v++)
        {
            var a = res[v];
            string Dt(long died, long t) => died == 0 ? "—" : $"{Per1(t, died)}（{Pct(died, a.N)}）";
            Console.WriteLine($"| {vars[v].K.Name} × {vars[v].C.Name} | {F1(a.Win)} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)} | {Per(a.Placed, a.N)} ／ {Per(a.Pops, a.N)} | {Per(a.DisToUnit, a.N)} ／ {Per1(a.Dealt, a.N)} | "
                + $"{Per(a.Casts, a.KN)} ／ {Per1(a.KNom, a.Hits)} ／ {Per0(a.KDmg, a.KN)} | {Per0(a.HeroDmg, a.N)} ／ {Per0(a.HeroHeal, a.N)} | {Per1(a.OverTurns, a.N)} | {Dt(a.KDied, a.KDeathT)} ／ {Dt(a.GDied, a.GDeathT)} ／ {Dt(a.TDied, a.TDeathT)} |");
        }
        Console.WriteLine();
        Console.WriteLine("| 版 | 量 | " + string.Join(" | ", Marks.Select(m => $"T{m}")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Marks.Select(_ => "--:|")));
        for (int v = 0; v < vars.Length; v++)
        {
            var a = res[v];
            string n = $"{vars[v].K.Name} × {vars[v].C.Name}";
            Console.WriteLine($"| {n} | 糸玉 | " + string.Join(" | ", a.BallsAt.Select(x => Avg(x, a.N))) + " |");
            Console.WriteLine($"| {n} | 雷雲 | " + string.Join(" | ", a.CloudAt.Select(x => Avg(x, a.N))) + " |");
            if (w.Boss) Console.WriteLine($"| {n} | 勇者の HP | " + string.Join(" | ", a.HeroHpAt.Select(x => Avg(x, a.N, "F0"))) + " |");
        }
    }

    // ---------------------------------------------------------------------------------
    // 1戦のログ
    // ---------------------------------------------------------------------------------
    static void LogOne(string wave, string order, int seed, string[] vers)
    {
        var w = WaveOf(wave);
        var f = Seat(Order(order));
        foreach (var v in vers) f = Apply(f, AnyVer(v));
        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), seed, verbose: true);
        Console.WriteLine($"# kugu292 log —— {order} × {w.Name} × seed {seed} × [{string.Join(" ", vers)}] → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}");
        Console.WriteLine();
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }
}
