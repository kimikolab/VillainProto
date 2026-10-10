using BattleCore;
using static Common;
using B283 = Boss283Diag;
using S287 = Shock287Diag;
using S293 = Shock293Diag;
using S307 = Som307Diag;
using S309 = Som309Diag;
using S310 = Som310Diag;

// =====================================================================================
// som311 —— 第311期「ソム E2 の規定化 ＋ 緊急の喚び出し（K-a ／ K-b）／ 群れ ／ 痺れない相手の萎縮」。
// 指示書は design/PHASE311_SOM_SUMMONER_SPEC.md ／ 報告は design/PHASE311_SOM_SUMMONER.md。
//
//     dotnet run --project BattleSim -c Release 0 som311 p0          # Phase 0（§5）: 規定（E2）のまま、4 割を切る被弾・前のターンの弾け・ボスの痺れの判定
//     dotnet run --project BattleSim -c Release 0 som311 boards [版,…]  # §6-2: 代表台 8 × 10 波 × 版（規定 ／ K-a ／ K-b ／ 群れ ／ 萎縮 ／ 重ね）
//     dotnet run --project BattleSim -c Release 0 som311 compare     # §6-3: `compare` 64 行 × 版
//     dotnet run --project BattleSim -c Release 0 som311 swap        # §6-4: ソム ↔ ツギ（第309期の入れ替え）× 版
//     dotnet run --project BattleSim -c Release 0 som311 grid <版> <boss|guard|bat>   # §6-5: ソムの格子（第310期と同じ作り）
//     dotnet run --project BattleSim -c Release 0 som311 kubi        # §4（段2）: クビの3つの守りの寄与・カタへの漏れ
//     dotnet run --project BattleSim -c Release 0 som311 memo <版> <台の一部> <波> <seed> <T0> <T1>
//     dotnet run --project BattleSim -c Release 0 som311 check       # 自己検査
//
// **予測のファイルが無ければ本測定（boards ／ compare ／ swap ／ grid ／ kubi）を走らせない**（R397・`.tmp/p311/predict.md`）。
// 第312期: 規定のソム ／ カタ ／ トウが重ね（群れ ＋ 緊急 K-a ＋ 萎縮）になったので、台の駒は第311期の規定（`SomH311` ／ `KataH311` ／ `TouH311`・`Common.Pin311`）に固定した。
// K-a のベニ対策（engine の規則）は固定できないので、`火の型 ソラ→ソム` の K-a ／ 重ねの列だけは第312期から動く（報告書 design/PHASE312_SOM_REGULATE2.md §1）。
// =====================================================================================
static class Som311Diag
{
    const string PredictFile = ".tmp/p311/predict.md";

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "p0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" or "swap" or "grid" or "kubi" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"som311: 予測のファイル `{PredictFile}` がまだ無い（R397）。Phase 0 を読んで予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": BoardsAll(A(3, "")); return;
            case "compare": CompareAll(); return;
            case "swap": SwapAll(); return;
            case "grid": Grid(A(3, "K-a"), A(4, "boss")); return;
            case "kubi": KubiAll(); return;
            case "memo": Memo(A(3, "K-a"), A(4, "光の盾"), A(5, "boss"), int.Parse(A(6, "0")), int.Parse(A(7, "1")), int.Parse(A(8, "5"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("som311: モードは p0 / boards / compare / swap / grid / kubi / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・台・波
    // ---------------------------------------------------------------------------------
    const int Seeds = S307.Seeds;
    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);
    static string F1(double x) => S307.F1(x);
    static string Per1(long a, long n) => S307.Per1(a, n);
    static string Per0(long a, long n) => S307.Per0(a, n);
    static string Pct(long a, long n) => S307.Pct(a, n);
    static string Sg(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Dt(long died, long t, long n) => died == 0 ? "—" : $"{Per1(t, died)}（{Pct(died, n)}）";
    static bool Has(Formation f, UnitDef d) => f.Occupied().Any(o => ReferenceEquals(o.Def, d));
    static bool HasId(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    /// <summary>札を足した写し（重ねの版を組むため・数値と文面は元のまま）。</summary>
    internal static UnitDef With(UnitDef d, params TraitId[] add) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances, Pattern = d.Pattern,
        Traits = d.Traits.Concat(add).ToArray(), Actions = d.Actions, PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };
    /// <summary>札を1枚抜いた写し（段2 のクビの内訳）。</summary>
    internal static UnitDef Without(UnitDef d, TraitId t) => new()
    {
        Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances, Pattern = d.Pattern,
        Traits = d.Traits.Where(x => x != t).ToArray(), Actions = d.Actions, PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
    };

    internal sealed record Ver(string Name, Func<Formation, Formation> Apply);
    static Formation DT(Formation f) => S309.Swap(S309.Swap(f, UnitCatalog.KataH311, UnitCatalog.KataDT), UnitCatalog.TouH311, UnitCatalog.TouDT);

    internal static readonly Ver[] Singles =
    {
        new("規定", f => f),
        new("K-a", f => S309.Swap(f, UnitCatalog.SomH311, UnitCatalog.SomKa)),
        new("K-b", f => S309.Swap(f, UnitCatalog.SomH311, UnitCatalog.SomKb)),
        new("群れ", f => S309.Swap(f, UnitCatalog.SomH311, UnitCatalog.SomSW)),
        new("萎縮", DT),
    };
    internal static Ver[] Vers => Singles.Concat(Combos).ToArray();
    /// <summary>重ね（段1 §3-4）。単独の結果を見てから足す（空なら重ねは測らない）。</summary>
    internal static readonly Ver[] Combos =
    {
        new("重ね", f => DT(S309.Swap(f, UnitCatalog.SomH311, UnitCatalog.SomSWKa))),   // 単独の4版を測った後に決めた（報告書 §5）: K-a ＋ 群れ ＋ 萎縮
    };
    static Ver V(string n) => Vers.First(v => v.Name == n);

    internal static readonly S287.Wave[] Waves = S310.Waves;
    static S287.Wave WaveOf(string n) => n switch { "nine" => Waves[3], "nine2" => Waves[4], _ => S307.WaveOf(n) };
    static bool IsNine(S287.Wave w) => w.Name.StartsWith("九体", StringComparison.Ordinal);
    static Formation Playtest(string n) => Pin311(Presets.Playtest.First(r => r.Name == n).F);   // 第312期: 第311期の規定の駒に固定（`Pin311`）
    static Formation Row(string n) => Pin311(CompareBuilds().First(r => r.Name == n).F);
    static Formation WinSeat() => Pin311(B283.Seat(S307.Order(S307.WinBoard)));

    /// <summary>§6-2 の代表台（規定の駒・席は出典のまま）。</summary>
    internal static (string Name, Formation F)[] Boards() => new (string, Formation)[]
    {
        ("光の盾", Playtest(S309.Shield)),
        ("光の盾 重", Playtest(S309.ShieldHeavy)),
        ("光の盾 クビ→シガ", S309.Swap(Playtest(S309.Shield), UnitCatalog.Kubi, UnitCatalog.Shiga)),
        ("光の盾 重 クビ→シガ", S309.Swap(Playtest(S309.ShieldHeavy), UnitCatalog.Kubi, UnitCatalog.Shiga)),
        ("感電 行", Row("感電 (シガ×カタ×ソム)")),
        ("雷の型 ドハ→ソム", S309.Swap(Playtest("試遊・感電 雷の型"), UnitCatalog.Doha, UnitCatalog.SomH311)),
        ("勝ち台 ツギ→ソム", S309.Swap(WinSeat(), UnitCatalog.Tsugi, UnitCatalog.SomH311)),
        ("火の型 ソラ→ソム", S309.Swap(Playtest("試遊・感電 火の型"), UnitCatalog.Sora, UnitCatalog.SomH311)),
    };

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class St
    {
        public long N, Wins, WinT, LoseT, Caps, Turns, FirstDeathN, FirstDeathT, SomDied, SomDeathT;
        public Dictionary<string, long> FoeDmg = new();
        public long FoeHeal;
        // Phase 0
        public long Low, LowTurns, LowFront, LowBack;                        // 4 割を切った被弾 ／ そのあったターン ／ そのターンの最初の席
        public long[] PopT = new long[10], PopReach = new long[10];          // ターンごとの敵の側の弾け ／ そのターンを迎えた戦
        public long[] SwarmGuess = new long[6];                              // T2〜T8 の「前のターンの弾け」から見込む群れの数
        public long BossStunHits, BossDaunted, BossDauntedSwings, BossDauntedCut, BossSwings;
        // 版
        public long EmergNeed, EmergFired, EmergHushed, EmergBlocked, EmergSpent, EmergHeld, EmergFront, EmergBack, EmergAllyDied, EmergAllyHit, EmergFocusHealed, EmergFocusVeil;
        public long[] SwarmHist = new long[6];
        public long Bursts, BurstUnits, SwFires, SwMultSum, SwMultN, KataCloud, KataN, Lights, VeilAdded, VeilSoaked;
        public long KataDauntGain, KataDauntedSwings, KubiDaunts;
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public long FoeDmgAll => FoeDmg.Values.Sum();
        public void Merge(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LoseT += o.LoseT; Caps += o.Caps; Turns += o.Turns; FirstDeathN += o.FirstDeathN; FirstDeathT += o.FirstDeathT; SomDied += o.SomDied; SomDeathT += o.SomDeathT;
            foreach (var (k, v) in o.FoeDmg) FoeDmg[k] = FoeDmg.GetValueOrDefault(k) + v;
            FoeHeal += o.FoeHeal; Low += o.Low; LowTurns += o.LowTurns; LowFront += o.LowFront; LowBack += o.LowBack;
            for (int i = 0; i < 10; i++) { PopT[i] += o.PopT[i]; PopReach[i] += o.PopReach[i]; }
            for (int i = 0; i < 6; i++) { SwarmGuess[i] += o.SwarmGuess[i]; SwarmHist[i] += o.SwarmHist[i]; }
            BossStunHits += o.BossStunHits; BossDaunted += o.BossDaunted; BossDauntedSwings += o.BossDauntedSwings; BossDauntedCut += o.BossDauntedCut; BossSwings += o.BossSwings;
            EmergNeed += o.EmergNeed; EmergFired += o.EmergFired; EmergHushed += o.EmergHushed; EmergBlocked += o.EmergBlocked; EmergSpent += o.EmergSpent; EmergHeld += o.EmergHeld;
            EmergFront += o.EmergFront; EmergBack += o.EmergBack; EmergAllyDied += o.EmergAllyDied; EmergAllyHit += o.EmergAllyHit; EmergFocusHealed += o.EmergFocusHealed; EmergFocusVeil += o.EmergFocusVeil;
            Bursts += o.Bursts; BurstUnits += o.BurstUnits; SwFires += o.SwFires; SwMultSum += o.SwMultSum; SwMultN += o.SwMultN; KataCloud += o.KataCloud; KataN += o.KataN;
            Lights += o.Lights; VeilAdded += o.VeilAdded; VeilSoaked += o.VeilSoaked; KataDauntGain += o.KataDauntGain; KataDauntedSwings += o.KataDauntedSwings; KubiDaunts += o.KubiDaunts;
        }
    }

    internal static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var a = new St { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else { a.LoseT = r.Turns; if (r.Turns >= BattleEngine.MaxTurns) a.Caps = 1; }
        var mine = p.ToDictionary(u => u.InstanceId, u => u);
        UnitState? som = p.FirstOrDefault(u => u.Def.Id == "som"), shiga = p.FirstOrDefault(u => u.Def.Id == "shiga"), kata = p.FirstOrDefault(u => u.Def.Id == "kata"), kubi = p.FirstOrDefault(u => u.Def.Id == "kubi");
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        var beasts = new HashSet<int>();
        int lowTurn = -1;
        var pending = new HashSet<int>();   // 緊急の喚び出しの危ない味方（次の一撃を見る）
        for (int t = 1; t <= Math.Min(9, r.Turns); t++) a.PopReach[t]++;
        foreach (var ev in r.Events)
        {
            switch (ev.Kind)
            {
                case BattleEventKind.Summon when som is not null && ev.ActorId == som.InstanceId && ev.TargetId is int sid: beasts.Add(sid); break;
                case BattleEventKind.BeastBurst when ev.Text == BeastBurstLabels.Emergency && ev.PartnerId is int pa: pending.Add(pa); break;
                case BattleEventKind.Death when ev.TargetId is int t && mine.ContainsKey(t):
                    if (a.FirstDeathN == 0) { a.FirstDeathN = 1; a.FirstDeathT = ev.Turn; }
                    if (som is not null && t == som.InstanceId && a.SomDied == 0) { a.SomDied = 1; a.SomDeathT = ev.Turn; }
                    break;
                case BattleEventKind.ShockSpent when ev.Team != BattleContext.PlayerTeam && ev.Turn <= 9: a.PopT[ev.Turn]++; break;
                case BattleEventKind.StatusGain when kata is not null && ev.TargetId == kata.InstanceId && ev.Text == StatusKeys.Daunted: a.KataDauntGain++; break;
                case BattleEventKind.StatusGain when kubi is not null && ev.ActorId == kubi.InstanceId && ev.Text == StatusKeys.Daunted && ev.TargetId is int dt && foes.Contains(dt): a.KubiDaunts++; break;
                case BattleEventKind.Damage when ev.TargetId is int d && mine.TryGetValue(d, out var du):
                    if (pending.Remove(d)) { a.EmergAllyHit++; if (ev.HpAfter == 0) a.EmergAllyDied++; }
                    if (ev.HpAfter > 0 && ev.HpAfter * 100 < du.MaxHp * EmergencyCallTrait.Percent)
                    {
                        a.Low++;
                        if (lowTurn != ev.Turn) { lowTurn = ev.Turn; a.LowTurns++; if (du.Row == BattleCore.Row.Front) a.LowFront++; else a.LowBack++; }
                    }
                    break;
                case BattleEventKind.Damage when ev.TargetId is int ft && foes.Contains(ft):
                    {
                        string k = ev.ActorId is int ka && mine.TryGetValue(ka, out var ku) ? Short(ku.Def) : ev.ActorId is int kb && beasts.Contains(kb) ? "獣の放電" : ev.ActorId is null ? "刻み" : "敵の側";
                        a.FoeDmg[k] = a.FoeDmg.GetValueOrDefault(k) + ev.Amount;
                        break;
                    }
                case BattleEventKind.Heal when ev.TargetId is int ht && foes.Contains(ht): a.FoeHeal += ev.Amount; break;
                case BattleEventKind.Attack when ev.ActorId is int ba && foes.Contains(ba): a.BossSwings++; break;
            }
        }
        for (int t = 2; t <= Math.Min(8, r.Turns); t++) a.SwarmGuess[Math.Min(UnitCatalogSwarmCap, 1 + (int)(a.PopT[t - 1] / BattleContext.SwarmPer))]++;
        if (som is not null && r.TallyByUnit.TryGetValue("som", out var mt))
        {
            a.EmergNeed = mt.EmergNeed; a.EmergFired = mt.EmergFired; a.EmergHushed = mt.EmergHushed; a.EmergBlocked = mt.EmergBlocked; a.EmergSpent = mt.EmergSpent; a.EmergHeld = mt.EmergHeld;
            a.EmergFront = mt.EmergFront; a.EmergBack = mt.EmergBack; a.EmergFocusHealed = mt.EmergFocusHealed; a.EmergFocusVeil = mt.EmergFocusVeil;
            if (mt.SwarmHist is { } sh) for (int i = 0; i < Math.Min(6, sh.Length); i++) a.SwarmHist[i] = sh[i];
            a.Bursts = mt.BeastBursts; a.BurstUnits = mt.BeastBurstUnits; a.Lights = mt.SparkRainLights; a.VeilAdded = mt.SparkVeilAdded;
        }
        foreach (var u in p) if (r.TallyByUnit.TryGetValue(u.Def.Id, out var ut)) a.VeilSoaked += ut.SparkVeilSoaked;
        if (shiga is not null && r.TallyByUnit.TryGetValue("shiga", out var stt)) { a.SwFires = stt.SwFires; a.SwMultSum = stt.SwMultSum; a.SwMultN = stt.SwMultN; }
        if (kata is not null && r.TallyByUnit.TryGetValue("kata", out var kt)) { a.KataN = 1; a.KataCloud = ThundercloudTrait.Of(kata); a.KataDauntedSwings = kt.DauntedSwings; }
        if (w.Boss && e.Count > 0 && r.TallyByUnit.TryGetValue(e[0].Def.Id, out var bt))
        { a.BossStunHits = bt.ShockStunned + bt.ShockDaunted + bt.ShockDauntAlready; a.BossDaunted = bt.ShockDaunted; a.BossDauntedSwings = bt.DauntedSwings; a.BossDauntedCut = bt.DauntedCut; }
        return a;
    }
    const int UnitCatalogSwarmCap = BattleContext.SwarmCap;

    internal static St Measure(Formation f, S287.Wave w, int n = Seeds)
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

    static string FoeDmgLine(St a, Formation f)
    {
        var order = f.Occupied().Select(o => Short(o.Def)).Distinct().Append("獣の放電").Append("刻み").Append("敵の側");
        return string.Join("・", order.Where(k => a.FoeDmg.GetValueOrDefault(k) > 0).Select(k => $"{k} {Per1(a.FoeDmg[k], a.Turns)}"));
    }

    // ---------------------------------------------------------------------------------
    // Phase 0（§5）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var waves = Waves.Take(5).ToArray();
        var cells = new List<(string B, Formation F, S287.Wave W, St S)>();
        foreach (var (n, f) in Boards()) foreach (var w in waves) cells.Add((n, f, w, Measure(f, w)));
        Console.WriteLine("# 第311期 Phase 0（§5）—— 段0 の規定（ソム ＝ E2）のまま・seed 0..199・verbose");
        Console.WriteLine();
        foreach (var (n, f) in Boards()) Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine();
        Console.WriteLine("## §5-1 味方の HP（＋破片）が 4 割を切る被弾（/戦）と、緊急の喚び出しの見込み（1ターン1回 ＝ 切った被弾のあったターンの数）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率 | 決着T | 切った被弾 | あったターン（見込み） | そのターンの最初の席 前 ／ 中・後 | 寿命 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|---|---|");
        foreach (var c in cells)
            Console.WriteLine($"| {c.B} | {c.W.Name} | {F1(c.S.Win)} | {Per1(c.S.Turns, c.S.N)} | {Per1(c.S.Low, c.S.N)} | {Per1(c.S.LowTurns, c.S.N)} | {Pct(c.S.LowFront, c.S.LowTurns)} ／ {Pct(c.S.LowBack, c.S.LowTurns)} | {Dt(c.S.FirstDeathN, c.S.FirstDeathT, c.S.N)} |");
        Console.WriteLine();
        Console.WriteLine("（HP を削る被弾の後は破片が 0 なので、`Damage` の残り HP で「HP＋破片」の線を見られる。破片だけが減る被弾は数えない——緊急の喚び出しの判定と同じ）");
        Console.WriteLine();
        Console.WriteLine("## §5-2 ターンごとの、敵の側で弾けた数（/戦・そのターンを迎えた戦あたり）と群れの数の見込み");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8 | 群れの見込み（T2〜T8）1 ／ 2 ／ 3 ／ 4 ／ 5 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var c in cells)
        {
            long g = c.S.SwarmGuess.Sum();
            Console.WriteLine($"| {c.B} | {c.W.Name} | " + string.Join(" | ", Enumerable.Range(1, 8).Select(t => c.S.PopReach[t] == 0 ? "—" : Per1(c.S.PopT[t], c.S.PopReach[t]))) + " | "
                + string.Join(" ／ ", Enumerable.Range(1, 5).Select(k => Pct(c.S.SwarmGuess[k], g))) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("## §5-3 ボスが感電の弾けで痺れの判定に当たった回数（/戦・規定では動じないので何も付かない）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 決着T | 痺れの判定に当たった | 勇者の攻撃 | クビの萎縮（敵へ） | クビの漏れがカタに | 味方の与ダメ /T | 勇者の回復 /T |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var c in cells.Where(c => c.W.Boss))
            Console.WriteLine($"| {c.B} | {F1(c.S.Win)} | {Per1(c.S.Turns, c.S.N)} | {Per1(c.S.BossStunHits, c.S.N)} | {Per1(c.S.BossSwings, c.S.N)} | {Per1(c.S.KubiDaunts, c.S.N)} | {Per1(c.S.KataDauntGain, c.S.N)} | {Per1(c.S.FoeDmgAll, c.S.Turns)} | {Per1(c.S.FoeHeal, c.S.Turns)} |");
        Console.WriteLine();
        Console.WriteLine("## 動じない敵が本編にいるか（§6-3）");
        Console.WriteLine();
        var proof = EnemyCatalog.Stages.SelectMany((s, i) => s.Enemy.Occupied().Select(o => (i, o.Def))).Where(x => TraitCatalog.Resolve(x.Def.Traits).Any(t => t.BlocksControl)).ToList();
        Console.WriteLine(proof.Count == 0 ? "- 本編の5波に、手番を奪う状態を塞ぐ札（動じない・勇者の印など）を持つ敵は **0 体**——萎縮の版は `compare` を動かさない見込み。" : "- " + string.Join("・", proof.Select(x => $"第{x.i + 1}波 {x.Def.Name}")));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §6-2 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var vers = string.IsNullOrEmpty(only) ? Vers : Vers.Where(v => v.Name == "規定" || only.Split(',').Contains(v.Name)).ToArray();
        var cells = new List<(string B, Formation F0, S287.Wave W, Ver V, St S)>();
        foreach (var (n, f) in Boards()) foreach (var v in vers) { var fv = v.Apply(f); foreach (var w in Waves) cells.Add((n, f, w, v, Measure(fv, w))); }
        var ctl = new Dictionary<(string, string, string), string>();
        foreach (var c in cells.Where(c => c.S.Win >= 50))
        {
            var fv = c.V.Apply(c.F0);
            var somDef = fv.Occupied().First(o => o.Def.Id == "som").Def;
            string s = F1(100.0 * WinsLite(S309.Swap(fv, somDef, UnitCatalog.Dolga), c.W, Seeds, out _) / Seeds);
            if (c.V.Name is "萎縮" or "重ね" && HasId(fv, "kubi"))
                s += "／ク " + F1(100.0 * WinsLite(S309.Swap(fv, UnitCatalog.Kubi, UnitCatalog.Dolga), c.W, Seeds, out _) / Seeds);
            ctl[(c.B, c.W.Name, c.V.Name)] = s;
        }

        Console.WriteLine($"# 第311期 §6-2 代表台 × 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}・seed 0..199・verbose）");
        Console.WriteLine();
        foreach (var (n, f) in Boards()) Console.WriteLine($"- {n}: {Seats(f)}（前1・前3・中央・後1・後3）");
        Console.WriteLine("- 萎縮 ＝ カタ ／ トウを `KataDT` ／ `TouDT`（痺れの規則の札を持つ駒に `ShockDaunt`）。重ね ＝ 報告書 §5（版の定義は `Combos`）");
        Console.WriteLine();
        Console.WriteLine("## 表A 勝率（括弧は規定との差・[ ] は ソム → ドルガ（萎縮 ／ 重ねは ／ク ＝ クビ → ドルガも）・勝率 50% 以上のセル）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "--:|")));
        foreach (var (n, _) in Boards())
            foreach (var v in vers)
                Console.WriteLine($"| {n} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var c = cells.First(x => x.B == n && x.V == v && x.W == w);
                    var b = cells.First(x => x.B == n && x.V == vers[0] && x.W == w);
                    return $"{F1(c.S.Win)}{(v == vers[0] ? "" : $"（{Sg(c.S.Win - b.S.Win)}）")}{(ctl.TryGetValue((n, w.Name, v.Name), out var k) ? $" [{k}]" : "")}";
                })) + " |");

        foreach (var w in Waves)
        {
            Console.WriteLine();
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | 勝率 | 倒しT | 負けT（上限） | 寿命 | ソム 倒れたT | 緊急 喚んだ ／ 切った ・ 粛 ／ 痺れ ・ 前 ／ 後 ・ 次の一撃で倒れた | K-a 集めた HP ／ 衣 | 群れ 1／2／3／4／5 ・ 連鎖 | シガ 割り込み ・ ×倍率 | カタ 雷雲 | 光 ・ 衣 ・ 受け止めた |"
                + (w.Boss ? " 痺れの判定 ・ 勇者の萎縮 ・ 半分になった量 | 味方の与ダメ /T ・ 勇者の回復 /T ・ 差 |" : ""));
            Console.WriteLine("|---|---|--:|--:|--:|---|---|---|---|---|---|--:|---|" + (w.Boss ? "---|---|" : ""));
            foreach (var c in cells.Where(x => x.W == w))
            {
                var a = c.S;
                long sh = a.SwarmHist.Sum();
                string em = a.EmergFired + a.EmergNeed == 0 ? "—" : $"{Per1(a.EmergFired, a.N)} ／ {Per1(a.EmergNeed, a.N)} ・ {Per1(a.EmergHushed, a.N)} ／ {Per1(a.EmergBlocked, a.N)} ・ {Pct(a.EmergFront, a.EmergFired)} ／ {Pct(a.EmergBack, a.EmergFired)} ・ {Pct(a.EmergAllyDied, a.EmergAllyHit)}";
                string fo = a.EmergFocusHealed + a.EmergFocusVeil == 0 ? "—" : $"{Per0(a.EmergFocusHealed, a.N)} ／ {Per0(a.EmergFocusVeil, a.N)}";
                string sw2 = sh == 0 ? "—" : string.Join("／", Enumerable.Range(1, 5).Select(k => Pct(a.SwarmHist[k], sh).Replace("%", ""))) + $" ・ {Per1(a.BurstUnits, sh)}";
                var fv = c.V.Apply(c.F0);
                Console.WriteLine($"| {c.B} | {c.V.Name} | {F1(a.Win)} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)}{(a.Caps > 0 ? $"（{Pct(a.Caps, a.N)}）" : "")} | {Dt(a.FirstDeathN, a.FirstDeathT, a.N)} | {Dt(a.SomDied, a.SomDeathT, a.N)} | {em} | {fo} | {sw2} | "
                    + (a.SwMultN + a.SwFires > 0 ? $"{Per1(a.SwFires, a.N)} ・ ×{Per1(a.SwMultSum, a.SwMultN)}" : "—") + $" | {(a.KataN > 0 ? Per1(a.KataCloud, a.KataN) : "—")} | {Per1(a.Lights, a.N)} ・ {Per0(a.VeilAdded, a.N)} ・ {Per0(a.VeilSoaked, a.N)} |"
                    + (w.Boss ? $" {Per1(a.BossStunHits, a.N)} ・ {Per1(a.BossDaunted, a.N)} ・ {Per0(a.BossDauntedCut, a.N)} | {Per1(a.FoeDmgAll, a.Turns)}（{FoeDmgLine(a, fv)}）・ {Per1(a.FoeHeal, a.Turns)} ・ {Sg((double)(a.FoeDmgAll - a.FoeHeal) / Math.Max(1, a.Turns))} |" : ""));
            }
        }

        Console.WriteLine();
        Console.WriteLine("## §6-5 格子を回す版（代表台のボスで規定から +5pt 以上、または精鋭で +10pt 以上）・§3-4 重ねに入れる版");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var moved = new List<string>();
            foreach (var c in cells.Where(x => x.V == v && (x.W.Boss || x.W.Name is "近衛" or "大隊")))
            {
                double d = c.S.Win - cells.First(x => x.B == c.B && x.V == vers[0] && x.W == c.W).S.Win;
                if (c.W.Boss ? d >= 5 : d >= 10) moved.Add($"{c.B} × {c.W.Name}（{Sg(d)}）");
            }
            Console.WriteLine($"- {v.Name}: {(moved.Count == 0 ? "**該当なし**" : $"**該当**（{string.Join("・", moved)}）")}");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §6-3 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().Select(r => (r.Name, F: Pin311(r.F))).ToArray();   // 第312期: 第311期の規定の駒に固定
        int nw = EnemyCatalog.Stages.Count;
        var grids = Vers.Select(v =>
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = v.Apply(rows[ri].F);
                int wins = 0;
                for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / Seeds;
            });
            return g;
        }).ToArray();
        Console.WriteLine("# 第311期 §6-3 `compare` 64 行 × 版（seed 0..199・基準は段0 の規定）");
        double M(double[,] g, int ri) => Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
        for (int vi = 1; vi < Vers.Length; vi++)
        {
            var v = Vers[vi]; var g = grids[vi]; var b = grids[0];
            Console.WriteLine();
            Console.WriteLine($"## {v.Name}");
            Console.WriteLine();
            Console.WriteLine("| 行 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | 差 | 最大の落ち |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|--:|");
            int movedRows = 0, movedCells = 0, outside = 0;
            var drops = new List<int>();
            for (int ri = 0; ri < rows.Length; ri++)
            {
                bool any = Enumerable.Range(0, nw).Any(w => g[ri, w] != b[ri, w]);
                bool holds = !v.Apply(rows[ri].F).Occupied().Select(o => o.Def).SequenceEqual(rows[ri].F.Occupied().Select(o => o.Def));
                if (any && !holds) outside++;
                if (!any) continue;
                movedRows++;
                movedCells += Enumerable.Range(0, nw).Count(w => g[ri, w] != b[ri, w]);
                double drop = Enumerable.Range(0, nw).Min(w => g[ri, w] - b[ri, w]);
                if (drop <= -10) drops.Add(ri);
                Console.WriteLine($"| {rows[ri].Name} | " + string.Join(" | ", Enumerable.Range(0, nw).Select(w => g[ri, w] == b[ri, w] ? F1(g[ri, w]) : $"{F1(g[ri, w])}（{Sg(g[ri, w] - b[ri, w])}）")) + $" | {F1(M(g, ri))} | {Sg(M(g, ri) - M(b, ri))} | {Sg(drop)} |");
            }
            if (movedRows == 0) Console.WriteLine("| （動いた行なし） |" + string.Concat(Enumerable.Range(0, nw + 3).Select(_ => " |")));
            Console.WriteLine();
            var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
            double p5b = pr.Average(ri => b[ri, nw - 1]), p5 = pr.Average(ri => g[ri, nw - 1]);
            double all = Enumerable.Range(0, rows.Length).Average(ri => M(g, ri)), allB = Enumerable.Range(0, rows.Length).Average(ri => M(b, ri));
            Console.WriteLine($"動いた行 {movedRows} ／ セル {movedCells}・版の駒がいない行で動いたセル **{outside}**・全64行の第2〜5波平均 {F1(allB)} → {F1(all)}（{Sg(all - allB)}）・主判定19行の第五波 {F1(p5b)} → {F1(p5)}（歯止め {F1(Baseline.PrimaryFifthFloor)}）");
            if (drops.Count == 0) { Console.WriteLine("(G2): −10.0pt 以上落ちた行なし。"); continue; }
            foreach (int ri in drops)
            {
                var parts = new List<string>(); bool broken = false;
                foreach (var d in rows[ri].F.Occupied().Select(o => o.Def).Distinct())
                {
                    var others = Enumerable.Range(0, rows.Length).Where(rj => rj != ri && rows[rj].F.Occupied().Any(o => ReferenceEquals(o.Def, d))).ToList();
                    if (others.Count == 0) { parts.Add($"{Short(d)} 他の行 0"); continue; }
                    double ch = others.Average(rj => M(g, rj) - M(b, rj));
                    if (ch <= -3.0) broken = true;
                    parts.Add($"{Short(d)} {Sg(ch)}（{others.Count} 行）");
                }
                Console.WriteLine($"- (G2) {rows[ri].Name}: {string.Join("・", parts)} → **{(broken ? "壊れ" : "編成上の制約")}**");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §6-4 ソム ↔ ツギ × 版
    // ---------------------------------------------------------------------------------
    static void SwapAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var waves = S307.Waves;
        // 第309期の入れ替え（`S309.Pairs` は第311期から `SomH310` に固定）を、規定のソム（E2）で組み直す。
        var pairs = S309.Pairs().Select(p => (p.Name, S: Pin311(S309.Swap(p.Som, UnitCatalog.SomH310, UnitCatalog.SomH311)), T: p.Tsugi is null ? null! : Pin311(p.Tsugi))).ToArray();
        Console.WriteLine("# 第311期 §6-4 ソム ↔ ツギ × 版（第309期 §5-1 と同じ台・同じ席・同じ 8 波・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("ソムの側は版を当てた台（規定 ＝ E2）。ツギの側は規定（萎縮 ／ 重ねはツギの側のカタ ／ トウも替えて回し直す）。差 ＝ ソム − ツギ。");
        foreach (var v in Vers)
        {
            Console.WriteLine();
            Console.WriteLine($"## {v.Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | " + string.Join(" | ", waves.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "---|")));
            int plus = 0, minus = 0, n2 = 0; var minusCells = new List<string>();
            foreach (var (n, s, t) in pairs)
            {
                var fs = v.Apply(s); var ft = t is null ? null : v.Name is "萎縮" or "重ね" ? DT(t) : t;
                var line = new List<string>();
                foreach (var w in waves)
                {
                    double a = 100.0 * WinsLite(fs, w, Seeds, out _) / Seeds;
                    if (ft is null) { line.Add($"{F1(a)} ／ —"); continue; }
                    double b = 100.0 * WinsLite(ft, w, Seeds, out _) / Seeds;
                    line.Add($"{F1(a)} ／ {F1(b)} ／ **{Sg(a - b)}**");
                    if (w.Name == "本編 第1波") continue;
                    n2++;
                    if (a - b >= 10) plus++;
                    if (a - b <= -10) { minus++; minusCells.Add($"{n} × {w.Name}（{Sg(a - b)}）"); }
                }
                Console.WriteLine($"| {n} | {string.Join(" | ", line)} |");
            }
            Console.WriteLine();
            Console.WriteLine($"§4-3 目安（本編 第1波を除く {n2} セル）: ソム − ツギ ≥ +10pt **{plus}** ／ ≤ −10pt **{minus}** → **{(plus > minus ? "満たす" : "満たさない")}**");
            if (minusCells.Count > 0) Console.WriteLine($"- −10pt 以下: {string.Join("・", minusCells)}");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §6-5 ソムの格子（第310期と同じ作り）＋ 90% 以上の台・倒しT の分布
    // ---------------------------------------------------------------------------------
    static void Grid(string ver, string wave)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var v = V(ver);
        var w = S307.WaveOf(wave);
        UnitDef[] fixedBase = w.Boss ? new[] { UnitCatalog.Kugu, UnitCatalog.KataH311 } : new[] { UnitCatalog.TouH311 };
        var pool = w.Boss ? S307.BossPool : S307.ElitePool;
        int k = w.Boss ? 2 : 3, need = w.Boss ? 1 : 5;
        var lu = S293.Combos(pool, k);
        var boards = new List<UnitDef[]>();
        foreach (var t in lu) boards.AddRange(B283.Perms(fixedBase.Append(UnitCatalog.SomH311).Concat(t).ToArray()));
        var res = new (int Wins, long WinT, int Ctl)?[boards.Count];
        int cutPass = 0;
        static int W(Formation f, S287.Wave w, int n, out long wt)
        {
            int wins = 0; wt = 0;
            for (int s = 0; s < n; s++) { var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), s, verbose: false); if (r.PlayerWon) { wins++; wt += r.Turns; } }
            return wins;
        }
        Parallel.For(0, boards.Count, i =>
        {
            var f = v.Apply(Pin311(B283.Seat(boards[i])));
            if (W(f, w, S307.CutSeeds, out _) < need) return;
            Interlocked.Increment(ref cutPass);
            int wins = W(f, w, Seeds, out long wt);
            int ctl = -1;
            if (wins * 2 >= Seeds) { var som = f.Occupied().First(o => o.Def.Id == "som").Def; ctl = W(S309.Swap(f, som, UnitCatalog.Dolga), w, Seeds, out _); }
            res[i] = (wins, wt, ctl);
        });
        var all = res.Where(r => r is not null).Select(r => r!.Value).ToList();
        int half = all.Count(r => r.Wins * 2 >= Seeds), reach = all.Count(r => r.Wins * 2 >= Seeds && r.Ctl >= 0 && r.Ctl * 2 <= r.Wins), ninety = all.Count(r => r.Wins * 10 >= Seeds * 9);
        Console.WriteLine($"# 第311期 §6-5 ソムの格子 × {w.Name} × 版 {v.Name}（固定枠 {S307.OrderName(fixedBase)} ＋ ソム・探索枠{k}・{boards.Count:N0} 台）");
        Console.WriteLine();
        Console.WriteLine($"足切り seed 0..{S307.CutSeeds - 1} で {need} 勝以上（{cutPass:N0} 台）→ seed 0..{Seeds - 1} → 勝率 50% 以上でソム → ドルガ。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勝率 ≧ 50% | **届いた（ソム → ドルガで半分以下）** | 勝率 ≧ 90% | 勝率 ＞ 0% | 最大の勝率 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        Console.WriteLine($"| {v.Name} | {half:N0} | {reach:N0} | {ninety:N0} | {all.Count(r => r.Wins > 0):N0} | {(all.Count == 0 ? "—" : F1(100.0 * all.Max(r => r.Wins) / Seeds))} |");
        Console.WriteLine();
        Console.WriteLine("勝率 90% 以上の台の倒しT の分布（台ごとの平均の倒しT）:");
        Console.WriteLine();
        var ts = all.Where(r => r.Wins * 10 >= Seeds * 9).Select(r => (double)r.WinT / r.Wins).ToList();
        var bins = new[] { 6, 8, 10, 12, 15, 20, 25, 31 };
        Console.WriteLine("| 倒しT | " + string.Join(" | ", bins.Select((b, i) => i == 0 ? $"≦{b}" : $"{bins[i - 1] + 1}〜{b}")) + " |");
        Console.WriteLine("|---|" + string.Concat(bins.Select(_ => "--:|")));
        Console.WriteLine("| 台 | " + string.Join(" | ", bins.Select((b, i) => ts.Count(t => t <= b + 0.5 && (i == 0 || t > bins[i - 1] + 0.5)).ToString("N0"))) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §4 段2 —— クビの寄与（測るだけ）
    // ---------------------------------------------------------------------------------
    static void KubiAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boss = Waves[0];
        Console.WriteLine("# 第311期 §4 段2 —— クビの3つの守りの寄与（段0 の規定・ボス・seed 0..199・verbose）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 外したもの | 勝率 | 倒しT | 負けT（上限） | 寿命 | 勇者の攻撃 /戦 | クビの萎縮（敵へ）/戦 | 漏れがカタに /戦（カタの萎縮した攻撃） | 味方の与ダメ /T | カタの与ダメ /T |");
        Console.WriteLine("|---|---|--:|--:|--:|---|--:|--:|---|--:|--:|");
        foreach (var name in new[] { "光の盾", "光の盾 重" })
        {
            var f = Boards().First(b => b.Name == name).F;
            foreach (var (label, g) in new (string, Formation)[]
            {
                ("—（規定）", f),
                ("被ダメ −30%（`Huddle`）", S309.Swap(f, UnitCatalog.Kubi, Without(UnitCatalog.Kubi, TraitId.Huddle))),
                ("萎縮（`Daunt`）", S309.Swap(f, UnitCatalog.Kubi, Without(UnitCatalog.Kubi, TraitId.Daunt))),
                ("漏れ（`DauntLeak`）", S309.Swap(f, UnitCatalog.Kubi, Without(UnitCatalog.Kubi, TraitId.DauntLeak))),
                ("クビ → ドルガ", S309.Swap(f, UnitCatalog.Kubi, UnitCatalog.Dolga)),
                ("クビ → シガ", S309.Swap(f, UnitCatalog.Kubi, UnitCatalog.Shiga)),
            })
            {
                var a = Measure(g, boss);
                Console.WriteLine($"| {name} | {label} | {F1(a.Win)} | {Per1(a.WinT, a.Wins)} | {Per1(a.LoseT, a.N - a.Wins)}{(a.Caps > 0 ? $"（{Pct(a.Caps, a.N)}）" : "")} | {Dt(a.FirstDeathN, a.FirstDeathT, a.N)} | {Per1(a.BossSwings, a.N)} | {Per1(a.KubiDaunts, a.N)} | {Per1(a.KataDauntGain, a.N)}（{Per1(a.KataDauntedSwings, a.N)}） | {Per1(a.FoeDmgAll, a.Turns)} | {Per1(a.FoeDmg.GetValueOrDefault("カタ"), a.Turns)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("カタの手番は雷（`Skill`）で通常攻撃を振らないので、萎縮（次の1回の攻撃が半分・`PerformAttack` で消費）は付いても減らす一撃が無い——（ ）の「カタの萎縮した攻撃」が 0 ならカタの与ダメは漏れで減っていない。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 台本の抜粋
    // ---------------------------------------------------------------------------------
    static void Memo(string ver, string part, string wave, int seed, int t0, int t1)
    {
        var v = V(ver);
        var (name, f0) = Boards().First(b => b.Name.Contains(part, StringComparison.Ordinal));
        var f = v.Apply(f0);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        foreach (var ev in r.Events.Where(x => x.Kind is BattleEventKind.Summon or BattleEventKind.SilkBall && x.TargetId is int)) names.TryAdd(ev.TargetId!.Value, ev.Kind == BattleEventKind.SilkBall ? "糸玉" : "背いた獣");
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# som311 memo —— {name}（{v.Name}・{Seats(f)}）× {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}・T{t0}〜T{t1}");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        for (int i = 0; i < r.Events.Count; i++)
        {
            var x = r.Events[i];
            if (x.Turn < t0 || x.Turn > t1) continue;
            if (x.Kind is BattleEventKind.StatSnapshot or BattleEventKind.StatusSnapshot) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"#{i,-4} {x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  hp={x.HpAfter}  Slot {x.Slot}{(x.PartnerId is int pp ? $"  partner {N(pp)}" : "")}{(x.StatusRemaining is int sr ? $"  rem {sr}" : "")}{(x.Reaction ? "  Reaction" : "")}{(x.FriendlyFire ? "  ff" : "")}");
        }
        Console.WriteLine("```");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査（§8）
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string label, bool ok, string note = "")
    {
        if (!ok) _fail++;
        Console.WriteLine($"| {label} | {(ok ? "○" : "**×**")} | {note} |");
    }

    static (BattleContext Ctx, List<UnitState> P, List<UnitState> E) Board(Formation f, List<UnitState> enemy)
    {
        var ctx = new BattleContext(0, false);
        var add = typeof(BattleContext).GetMethod("Add", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        foreach (var u in p.Concat(enemy)) add.Invoke(ctx, new object[] { u });
        foreach (var u in ctx.AllUnits) u.SetCounter(StatusKeys.IdleTurn, 99);
        return (ctx, p, enemy);
    }

    static UnitTally T(BattleContext ctx, UnitState u)
    {
        var d = (Dictionary<string, UnitTally>)typeof(BattleContext).GetProperty("TallyByUnit", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(ctx)!;
        return d.TryGetValue(u.Def.Id, out var t) ? t : new UnitTally();
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# som311 check —— 第311期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");
        var som = UnitCatalog.SomH311;

        // (a) 段0 の定義
        {
            bool ok = som.Traits.SequenceEqual(UnitCatalog.SomE2.Traits) && som.Traits.SequenceEqual(UnitCatalog.SomH310.Traits.Append(TraitId.BeastBurstAlways))
                && som.PlusText == UnitCatalog.SomE2.PlusText && som.PlusText.EndsWith("。喚ばれたものは、向こう側に着いた瞬間に弾ける", StringComparison.Ordinal)
                && som.MinusText == "喚ばれたものは背いて敵につくが、立つことはない" && som.Name == UnitCatalog.SomH310.Name && som.Flavor == UnitCatalog.SomH310.Flavor
                && som.MaxHp == UnitCatalog.SomH310.MaxHp && som.Attack == UnitCatalog.SomH310.Attack && som.Speed == UnitCatalog.SomH310.Speed && ReferenceEquals(som.Actions, UnitCatalog.SomH310.Actions)
                && !UnitCatalog.Everyone.Contains(som) /* 第312期: 第311期の規定は `SomH311`（`All` の外）になった */ && !UnitCatalog.Everyone.Contains(UnitCatalog.SomH310)
                && UnitCatalog.SomH310.Traits.SequenceEqual(UnitCatalog.SomLVa.Traits)
                && UnitCatalog.SomE1.Traits.SequenceEqual(UnitCatalog.SomH310.Traits.Append(TraitId.BeastBurstCharged));
            Expect("(a) 段0: 規定のソム ＝ 第310期の `SomE2`（LV-a ＋ `BeastBurstAlways`・文面は叩き台・名前とフレーバーはそのまま）・旧は `SomH310`（`All` の外・`SomLVa` と同じ札）・第310期の版は旧から作る", ok);
        }
        // (b) 版の定義
        {
            bool ok = UnitCatalog.SomKb.Traits.SequenceEqual(som.Traits.Append(TraitId.EmergencyCall))
                && UnitCatalog.SomKa.Traits.SequenceEqual(som.Traits.Append(TraitId.EmergencyCall).Append(TraitId.EmergencyFocus))
                && UnitCatalog.SomSW.Traits.SequenceEqual(som.Traits.Append(TraitId.SwarmCall))
                && UnitCatalog.KataDT.Traits.SequenceEqual(UnitCatalog.KataH311.Traits.Append(TraitId.ShockDaunt)) && UnitCatalog.TouDT.Traits.SequenceEqual(UnitCatalog.TouH311.Traits.Append(TraitId.ShockDaunt))
                && UnitCatalog.SomSWKa.Traits.SequenceEqual(som.Traits.Append(TraitId.SwarmCall).Append(TraitId.EmergencyCall).Append(TraitId.EmergencyFocus))
                && new[] { UnitCatalog.SomKa, UnitCatalog.SomKb, UnitCatalog.SomSW, UnitCatalog.SomSWKa, UnitCatalog.KataDT, UnitCatalog.TouDT }.All(d => !UnitCatalog.Everyone.Contains(d));
            Expect("(b) 版 ＝ 規定の末尾に札（K-b `EmergencyCall` ／ K-a ＋ `EmergencyFocus` ／ 群れ `SwarmCall` ／ 萎縮 カタ ／ トウ ＋ `ShockDaunt` ／ 重ね `SomSWKa`）・`Everyone` の外", ok);
        }
        var boss = Waves[0];
        var shield = Playtest(S309.Shield);
        // (c) 緊急: 4 割を切ったときだけ・1ターン1回・ソムが倒れていれば起きない（盤を直に組む）
        {
            var enemy = BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None);
            var (ctx, p, e) = Board(S309.Swap(shield, som, UnitCatalog.SomKa), enemy);
            var s = p.First(u => u.Def.Id == "som"); var kata = p.First(u => u.Def.Id == "kata");
            var t = T(ctx, s);
            ctx.ApplyDamage(kata, kata.MaxHp * 50 / 100, e[0]);       // 5 割（クビの −30% で 6 割強）→ まだ
            long f0 = t.EmergFired;
            ctx.ApplyDamage(kata, (kata.Hp - kata.MaxHp * 30 / 100) * 10 / 7 + 1, e[0]);   // 3 割 → 喚ぶ（クビの −30% を見込む）
            long f1 = t.EmergFired;
            kata.Hp = kata.MaxHp * 20 / 100; kata.SetCounter(StatusKeys.Armor, 0);   // 1回目の光（HP ＋ 衣）を消してから
            ctx.ApplyDamage(kata, 4, e[0]);                             // 同じターン → 使い終えた
            long f2 = t.EmergFired, spent = t.EmergSpent;
            s.Hp = 0;
            var (ctx2, p2, e2) = Board(S309.Swap(shield, som, UnitCatalog.SomKa), BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None));
            var s2 = p2.First(u => u.Def.Id == "som"); var k2 = p2.First(u => u.Def.Id == "kata");
            s2.Hp = 0;
            ctx2.ApplyDamage(k2, k2.MaxHp * 90 / 100, e2[0]);
            long dead = T(ctx2, s2).EmergFired + T(ctx2, s2).EmergNeed;
            Expect("(c) 緊急: 4 割を切った被弾でだけ喚ぶ（5 割では喚ばない）・同じターンの2回目は喚ばない・ソムが倒れていれば判定もしない", f0 == 0 && f1 == 1 && f2 == 1 && spent >= 1 && dead == 0,
                $"5 割 {f0}・3 割 {f1}・2回目 {f2}（使い終えた {spent}）・ソムが倒れた盤 {dead}");
        }
        // (d) 緊急は粛 ／ 痺れで止まる（実戦: 第2波（粛の伝令）× 光の盾 K-a）・止まった数は粛のひびに入る
        {
            long hushed = 0, blocked = 0, fired = 0;
            var w2 = Waves[6];   // 本編 第2波
            var fs = S309.Swap(shield, som, UnitCatalog.SomKa);
            for (int sd = 0; sd < 60; sd++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(fs, BattleContext.PlayerTeam), w2.Make(), sd, verbose: false);
                var t = r.TallyByUnit["som"]; hushed += t.EmergHushed; blocked += t.EmergBlocked; fired += t.EmergFired;
            }
            // 痺れ: 盤を直に組んでソムを痺れさせる
            var (ctx, p, e) = Board(S309.Swap(shield, som, UnitCatalog.SomKa), BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None));
            var s = p.First(u => u.Def.Id == "som"); var kata = p.First(u => u.Def.Id == "kata");
            s.SetCounter(StatusKeys.Stun, 1);
            ctx.ApplyDamage(kata, kata.MaxHp * 90 / 100, e[0]);
            var t2 = T(ctx, s);
            Expect("(d) 緊急は粛で止まる（本編 第2波・粛の伝令）・痺れでも止まる（盤を直に組む）", hushed > 0 && t2.EmergBlocked == 1 && t2.EmergFired == 0,
                $"第2波 × seed 0..59: 喚んだ {fired}・粛 {hushed}・ほか {blocked}／痺れた盤: 止まった {t2.EmergBlocked}");
        }
        // (e) K-a: その連鎖の光は危ない味方1体に集まる（溢れは衣）・K-b はいつもどおり全員
        {
            long kaFocus = 0, kaRainsToOthers = 0, kbFocus = 0, kaFired = 0;
            foreach (var (vn, d) in new[] { ("K-a", UnitCatalog.SomKa), ("K-b", UnitCatalog.SomKb) })
                for (int sd = 0; sd < 20; sd++)
                {
                    var p = BattleEngine.Materialize(S309.Swap(shield, som, d), BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, Waves[1].Make(), sd, verbose: true);
                    var t = r.TallyByUnit["som"];
                    if (vn == "K-a") { kaFocus += t.EmergFocusHealed + t.EmergFocusVeil; kaFired += t.EmergFired; } else kbFocus += t.EmergFocusHealed + t.EmergFocusVeil;
                    if (vn != "K-a") continue;
                    // 緊急の `BeastBurst` から次の `Spark 降る` まで: 集まった光の後に、ほかの味方への Heal ／ 衣が出ない
                    var ev = r.Events; int? ally = null; bool inRain = false;
                    for (int i = 0; i < ev.Count; i++)
                    {
                        var x = ev[i];
                        if (x.Kind == BattleEventKind.BeastBurst && x.Text == BeastBurstLabels.Emergency) { ally = x.PartnerId; continue; }
                        if (ally is int al && x.Kind == BattleEventKind.Spark && x.Text == SparkLabels.Release) { inRain = x.TargetId == al; if (!inRain) ally = null; continue; }
                        if (inRain && x.Kind is BattleEventKind.Heal or BattleEventKind.Spark && x.Text != SparkLabels.Release)
                        {
                            if (x.TargetId != ally) kaRainsToOthers++;
                            continue;
                        }
                        if (inRain) { inRain = false; ally = null; }
                    }
                }
            Expect("(e) K-a: 緊急の連鎖の光は危ない味方1体に集まる（ほかの味方への回復 ／ 衣は 0・溢れは衣）・K-b は集めない", kaFired > 0 && kaFocus > 0 && kaRainsToOthers == 0 && kbFocus == 0,
                $"K-a 喚んだ {kaFired}・集めた HP ＋ 衣 {kaFocus}・ほかの味方へ {kaRainsToOthers}／K-b 集めた {kbFocus}（光の盾 × 近衛 × seed 0..19）");
        }
        // (f) 群れ: 喚ぶ数の式と上限 5・群れは1つの連鎖にまとまる
        {
            long bad = 0, n = 0, oneChain = 0, multi = 0, maxN = 0;
            for (int sd = 0; sd < 20; sd++)
                foreach (var w in new[] { boss, Waves[1], Waves[3] })
                {
                    var p = BattleEngine.Materialize(S309.Swap(Boards()[4].F, som, UnitCatalog.SomSW), BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), sd, verbose: true);
                    var s = p.First(u => u.Def.Id == "som");
                    var pops = new Dictionary<int, int>();
                    foreach (var x in r.Events) if (x.Kind == BattleEventKind.ShockSpent && x.Team != BattleContext.PlayerTeam) pops[x.Turn] = pops.GetValueOrDefault(x.Turn) + 1;
                    // 手番の頭の群れ: その手番の最初の `BeastBurst` の数（緊急は無い版）
                    foreach (var g in r.Events.Where(x => x.Kind == BattleEventKind.BeastBurst && x.ActorId == s.InstanceId).GroupBy(x => x.Turn))
                    {
                        int cnt = g.Count();
                        n++;
                        int want = Math.Min(5, 1 + pops.GetValueOrDefault(g.Key - 1) / 3);
                        if (g.Key == 1) want = 1;
                        if (cnt != want) bad++;
                        if (cnt > maxN) maxN = cnt;
                        if (cnt > 1)
                        {
                            multi++;
                            // 群れの獣はどれも深さ 0 の起点として同じ連鎖に入る（最初の獣の `ShockSpent` から、次の深さ 0 が群れの外の駒になるまで）
                            var ids = g.Select(x => x.TargetId!.Value).ToHashSet();
                            int first = r.Events.ToList().FindIndex(x => x.Kind == BattleEventKind.ShockSpent && x.TargetId is int t && ids.Contains(t));
                            int seen = 0;
                            for (int i = first; i < r.Events.Count && i >= 0; i++)
                            {
                                var x = r.Events[i];
                                if (x.Kind != BattleEventKind.ShockSpent) continue;
                                if (x.TargetId is int t && ids.Contains(t)) { if (x.Slot != 0) break; seen++; continue; }
                                if (x.Slot == 0) break;
                            }
                            if (seen == ids.Count) oneChain++;
                        }
                    }
                }
            Expect("(f) 群れ: 喚ぶ数 ＝ 1 ＋ 前のターンの敵の側の弾け ÷ 3（上限 5）・群れは1つの連鎖（全部が深さ 0 の起点）", bad == 0 && multi > 0 && oneChain == multi && maxN <= 5,
                $"喚び出し {n}・式とずれた {bad}・群れ（2体以上）{multi}・1つの連鎖 {oneChain}・最大 {maxN}（感電 行 × ボス ／ 近衛 ／ 九体 × seed 0..19）");
        }
        // (g) 萎縮: 痺れが付く相手には付かない・付かない相手には痺れの判定に当たったときだけ・二値
        {
            long stunned = 0, dauntNormal = 0, bossDaunt = 0, bossHits = 0, bossAlready = 0, plain = 0;
            for (int sd = 0; sd < 20; sd++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(DT(shield), BattleContext.PlayerTeam), boss.Make(), sd, verbose: true);
                var t = r.TallyByUnit["boss_regular"]; bossDaunt += t.ShockDaunted; bossAlready += t.ShockDauntAlready; bossHits += t.ShockStunned;
                var r2 = BattleEngine.Run(BattleEngine.Materialize(shield, BattleContext.PlayerTeam), boss.Make(), sd, verbose: false);
                plain += r2.TallyByUnit["boss_regular"].ShockDaunted;
                var r3 = BattleEngine.Run(BattleEngine.Materialize(DT(Boards()[4].F), BattleContext.PlayerTeam), Waves[1].Make(), sd, verbose: false);
                foreach (var (id, tt) in r3.TallyByUnit) { stunned += tt.ShockStunned; dauntNormal += tt.ShockDaunted; }
            }
            Expect("(g) 萎縮: 痺れの付く駒（近衛）には萎縮が付かず痺れる・動じない勇者は痺れの判定に当たると萎縮する（既に萎縮なら重ねない）・札が無ければ 0",
                bossDaunt > 0 && bossHits == 0 && dauntNormal == 0 && stunned > 0 && plain == 0,
                $"勇者 萎縮 {bossDaunt}・既に {bossAlready}・痺れ {bossHits}／近衛の戦 痺れ {stunned}・萎縮 {dauntNormal}／札なしの勇者 {plain}");
        }
        // (h) 決定的・verbose の有無で勝敗と決着T が同じ
        {
            int nd = 0, n = 0;
            foreach (var v in Vers) foreach (var (_, f) in Boards())
                    foreach (var w in Waves)
                        for (int sd = 0; sd < 2; sd++)
                        {
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(v.Apply(f), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            var r2 = BattleEngine.Run(BattleEngine.Materialize(v.Apply(f), BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            n++;
                            if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) nd++;
                        }
            Expect("(h) 決定的・verbose の有無で勝敗と決着T が変わらない（版 × 代表台 × 10 波）", nd == 0, $"{nd} ／ {n} 件");
        }
        // (i) 乱数
        {
            static int Count(string text, string pat) { int n = 0, k = 0; while ((k = text.IndexOf(pat, k, StringComparison.Ordinal)) >= 0) { n++; k += pat.Length; } return n; }
            static string? Head(string path)
            {
                try
                {
                    var psi = new System.Diagnostics.ProcessStartInfo("git", $"show 8682c14:{path}") { RedirectStandardOutput = true, UseShellExecute = false, StandardOutputEncoding = System.Text.Encoding.UTF8 };
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
                if (head is null) { clean = false; notes.Add($"{path}: 第310期の版を読めない"); continue; }
                foreach (var pat in new[] { "PickOne(", "Roll(" }) { int a0 = Count(head, pat), a1 = Count(now, pat); if (a1 > a0) clean = false; notes.Add($"{Path.GetFileName(path)} {pat} {a0}→{a1}"); }
            }
            Expect("(i) `PickOne(` ／ `Roll(` を新たに使っていない（第310期のコミット 8682c14 以下）", clean, string.Join("・", notes));
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
