using System.Reflection;
using BattleCore;
using static Common;
using S287 = Shock287Diag;
using S307 = Som307Diag;

// =====================================================================================
// doha315 —— 第315期「ドハの背を押す 改（DQ-a ／ DQ-b）: 狙われた味方を、殴る駒だけ」。
// 指示書は design/PHASE315_DOHA_PUSH2_SPEC.md ／ 報告は design/PHASE315_DOHA_PUSH2.md。
//
//     dotnet run --project BattleSim -c Release 0 doha315 strikers           # §3-1 手番で殴る駒 ／ 殴らない駒（ロスター 52 枚・実測と定義 `BattleContext.StrikesOnTurn` の突き合わせ）
//     dotnet run --project BattleSim -c Release 0 doha315 p0                 # §3-2〜§3-4 Phase 0（規定のドハ・計数器 `PushCensus`）: 主目標の内訳・窓の最多（新 ／ 第313期の数え方）・敵を強くした条件
//     dotnet run --project BattleSim -c Release 0 doha315 boards [seeds]     # §4-2 代表台 × 波 × 版（規定 ／ DP-a ／ DP-b ／ DQ-a ／ DQ-b ／ ドルガ）・規定の倍率 ＋ 強くした条件
//     dotnet run --project BattleSim -c Release 0 doha315 compare            # §4-3 `compare` 64 行 × DQ-a ／ DQ-b（(G2) つき）
//     dotnet run --project BattleSim -c Release 0 doha315 grid <guard|boss> <a|b|pa|pb> [strong] [席=6] [seed=10]   # §4-4 第297期の「ドハが要る台」の格子（pa ／ pb は第313期の版・参考）
//     dotnet run --project BattleSim -c Release 0 doha315 memo <台の一部> <boss|guard|bat|1..5> <seed> <規定|a|b|qa|qb> [strong]   # §5 台本の並び
//     dotnet run --project BattleSim -c Release 0 doha315 diff <台の一部> <波> <版A> <版B> [strong]   # 勝敗が割れた seed
//     dotnet run --project BattleSim -c Release 0 doha315 check              # 自己検査
//
// 規定の駒・札・数値・波は1つも変えない。版は `UnitCatalog.DohaDQa` ／ `DohaDQb`（第313期の `DohaDPa` ／ `DohaDPb` を対照に並べる）を規定のドハと同じ席で差し替えるだけ。
// 強くした条件 ＝ 各波の敵の倍率に HP ×5 ／ 攻 ×3 を重ねる（`EnemyScaleRule` の口・ボスは倍率なし → 500/300、近衛 ／ 大隊は 1000/300 → 5000/900、本編は 115/115 → 575/345）。指示書の 200/150 では守り型が天井のまま（Phase 0 §3-4）。
// **予測のファイルが無ければ本測定（boards ／ compare ／ grid）を走らせない**（R397・`.tmp/p315/predict.md`）。
// =====================================================================================
static class Doha315Diag
{
    const string PredictFile = ".tmp/p315/predict.md";
    const int Seeds = 200;
    /// <summary>強くした条件の倍率（各波の倍率に重ねる・百分率）。Phase 0 §3-4 で決めた値。</summary>
    internal const int StrongHp = 500, StrongAtk = 300;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"doha315: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "strikers": Strikers(); return;
            case "p0": P0(); return;
            case "boards": BoardsAll(int.Parse(A(3, Seeds.ToString()))); return;
            case "compare": CompareAll(); return;
            case "grid":
            {
                bool strong = args.Skip(5).Contains("strong");
                var nums = args.Skip(5).Where(x => x != "strong").ToArray();
                Grid(A(3, "guard"), A(4, "a"), strong, nums.Length > 0 ? int.Parse(nums[0]) : 6, nums.Length > 1 ? int.Parse(nums[1]) : 10);
                return;
            }
            case "memo": Memo(A(3, "守り型"), A(4, "guard"), int.Parse(A(5, "0")), A(6, "qa"), A(7, "") == "strong"); return;
            case "diff": Diff(A(3, "守り型"), A(4, "guard"), A(5, "pb"), A(6, "qb"), A(7, "") == "strong"); return;
            case "check": Check(); return;
            default: Console.WriteLine("doha315: モードは strikers / p0 / boards / compare / grid / memo / diff / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・台・波（測る前に固定）
    // ---------------------------------------------------------------------------------
    internal static readonly (string Tag, UnitDef Def)[] Versions =
    {
        ("規定", UnitCatalog.Doha), ("DP-a", UnitCatalog.DohaDPa), ("DP-b", UnitCatalog.DohaDPb), ("DQ-a", UnitCatalog.DohaDQa), ("DQ-b", UnitCatalog.DohaDQb),
    };
    static UnitDef Ver(string s) => s switch
    {
        "a" or "qa" or "DQ-a" => UnitCatalog.DohaDQa, "b" or "qb" or "DQ-b" => UnitCatalog.DohaDQb,
        "pa" or "DP-a" => UnitCatalog.DohaDPa, "pb" or "DP-b" => UnitCatalog.DohaDPb, _ => UnitCatalog.Doha,
    };
    static string VerTag(UnitDef d) => Versions.First(v => ReferenceEquals(v.Def, d)).Tag;

    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);
    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x >= 0 ? $"+{x:F1}" : $"{x:F1}";
    static readonly string[] SeatName = { "前1", "前3", "中央", "後1", "後3", "○中1", "○中3", "○前2", "○後2" };

    static Formation Swap(Formation f, UnitDef from, UnitDef to)
    {
        var g = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied()) if (ReferenceEquals(d, from)) g[slot] = to;
        return g;
    }

    static Formation SwapSeats(Formation f, UnitDef a, UnitDef b)
    {
        var g = f.Clone();
        int sa = f.Occupied().First(o => ReferenceEquals(o.Def, a)).Slot, sb = f.Occupied().First(o => ReferenceEquals(o.Def, b)).Slot;
        g[sa] = b; g[sb] = a;
        return g;
    }

    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    static Formation Row(string n) => CompareBuilds().First(r => r.Name == n).F;
    internal const string Guard = "試遊・標 守り型", Thunder = "試遊・感電 雷の型", Ret1 = "反撃改 (ドハ×カド)", Ret2 = "反撃改2 (ガン×カド)";

    /// <summary>§4-2 の代表台。強くした条件は守り型の2台だけ。</summary>
    internal static (string Name, Formation F, bool Strong)[] Boards()
    {
        var g = Playtest(Guard);
        return new[]
        {
            ("守り型", g, true),
            ("守り型 ゴルム↔ドハ", SwapSeats(g, UnitCatalog.Golm, UnitCatalog.Doha), true),
            ("雷の型", Playtest(Thunder), false),
            ("反撃改", Row(Ret1), false),
            ("反撃改2", Row(Ret2), false),
        };
    }

    /// <summary>8 波（ボス ／ 近衛 ／ 大隊 ／ 本編 第1〜5波）。<paramref name="hp"/> ／ <paramref name="atk"/> は各波の倍率に重ねる百分率（100 ／ 100 で規定）。</summary>
    internal static S287.Wave[] MakeWaves(int hp, int atk)
    {
        EnemyScaleRule On(EnemyScaleRule r) => hp == 100 && atk == 100 ? r : new EnemyScaleRule(r.HpPercent * hp / 100, r.AtkPercent * atk / 100);
        var l = new List<S287.Wave>
        {
            new("ボス", () => BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, On(EnemyScaleRule.None)), true),
            new("近衛", () => BattleEngine.MaterializeEnemy(EnemyCatalog.EliteGuardWave, On(EnemyCatalog.EliteScale)), false),
            new("大隊", () => BattleEngine.MaterializeEnemy(EnemyCatalog.EliteBattalionWave, On(EnemyCatalog.EliteScale)), false),
        };
        for (int i = 0; i < EnemyCatalog.Stages.Count; i++)
        {
            var st = EnemyCatalog.Stages[i];
            l.Add(new($"本編 第{i + 1}波", () => BattleEngine.Materialize(st.Enemy, BattleContext.EnemyTeam, On(BossRule.Default.Scale)), false));
        }
        return l.ToArray();
    }
    static readonly S287.Wave[] Waves = MakeWaves(100, 100);
    static readonly S287.Wave[] StrongWaves = MakeWaves(StrongHp, StrongAtk);
    static readonly string[] StrongPick = { "ボス", "近衛", "大隊", "本編 第5波" };
    static S287.Wave WaveOf(string n, bool strong = false) => (strong ? StrongWaves : Waves).First(w => w.Name == (n switch
    {
        "boss" => "ボス", "guard" => "近衛", "bat" => "大隊", "1" => "本編 第1波", "2" => "本編 第2波", "3" => "本編 第3波", "4" => "本編 第4波", "5" => "本編 第5波", _ => n,
    }));

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    sealed class St
    {
        public long N, Wins, WinT, LossT, FirstDeathN, FirstDeathT, DohaDeathN, DohaDeathT, DohaTurns, DohaAttacks, DohaDealt;
        public long PushTurns, Pushes, PushZero, PushInGift, PushCapped, PushTies, GiftTurns, GiftAttacks, GiftDealt, GiftSkipped;
        public long Passes, Passed, PassNone, PassPeak, DohaWhet, DohaRegurg;
        public long AimTurns, Aims, AimZero, AimNoEligible, AimTies, AimSkipNoStrike, AimSkipStoic, AimSelf, AimSelfAmt, AimWinInTurn, AimedHits;
        public long OldPushTurns, OldPushes, OldPushZero;
        public readonly Dictionary<string, long> PushTo = new(), PushSeat = new(), AimTo = new(), OldTo = new(), Skipped = new(), Aimed = new(), PassTo = new();
        public void Add(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LossT += o.LossT; FirstDeathN += o.FirstDeathN; FirstDeathT += o.FirstDeathT; DohaDeathN += o.DohaDeathN; DohaDeathT += o.DohaDeathT;
            DohaTurns += o.DohaTurns; DohaAttacks += o.DohaAttacks; DohaDealt += o.DohaDealt;
            PushTurns += o.PushTurns; Pushes += o.Pushes; PushZero += o.PushZero; PushInGift += o.PushInGift; PushCapped += o.PushCapped; PushTies += o.PushTies;
            GiftTurns += o.GiftTurns; GiftAttacks += o.GiftAttacks; GiftDealt += o.GiftDealt; GiftSkipped += o.GiftSkipped;
            Passes += o.Passes; Passed += o.Passed; PassNone += o.PassNone; PassPeak = Math.Max(PassPeak, o.PassPeak); DohaWhet += o.DohaWhet; DohaRegurg += o.DohaRegurg;
            AimTurns += o.AimTurns; Aims += o.Aims; AimZero += o.AimZero; AimNoEligible += o.AimNoEligible; AimTies += o.AimTies; AimSkipNoStrike += o.AimSkipNoStrike; AimSkipStoic += o.AimSkipStoic;
            AimSelf += o.AimSelf; AimSelfAmt += o.AimSelfAmt; AimWinInTurn += o.AimWinInTurn; AimedHits += o.AimedHits;
            OldPushTurns += o.OldPushTurns; OldPushes += o.OldPushes; OldPushZero += o.OldPushZero;
            foreach (var (src, dst) in new[] { (o.PushTo, PushTo), (o.PushSeat, PushSeat), (o.AimTo, AimTo), (o.OldTo, OldTo), (o.Skipped, Skipped), (o.Aimed, Aimed), (o.PassTo, PassTo) })
                foreach (var (k, v) in src) dst[k] = dst.GetValueOrDefault(k) + v;
        }
        public string Win => N == 0 ? "—" : F1(100.0 * Wins / N);
        public double WinRate => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTurn => Wins == 0 ? "—" : F1((double)WinT / Wins);
        public string LossTurn => N - Wins == 0 ? "—" : F1((double)LossT / (N - Wins));
        public string Per(long x) => F1(N == 0 ? 0 : (double)x / N);
        public string Avg(long x, long n) => n == 0 ? "—" : F1((double)x / n);
        public string Pct(long x, long n) => n == 0 ? "—" : F1(100.0 * x / n);
        public string Share(Dictionary<string, long> d, int k = 5) { long t = d.Values.Sum(); return t == 0 ? "—"
            : string.Join("・", d.OrderByDescending(x => x.Value).Take(k).Select(x => $"{x.Key} {100.0 * x.Value / t:F0}%")); }
        public double ShareOf(Dictionary<string, long> d, string key) { long t = d.Values.Sum(); return t == 0 ? 0 : 100.0 * d.GetValueOrDefault(key) / t; }
    }

    static void Bump(Dictionary<string, long> d, string k, long v) { if (v > 0) d[k] = d.GetValueOrDefault(k) + v; }

    static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false);
        var a = new St { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else a.LossT = r.Turns;
        var dead = p.Where(u => u.LastDeathTurn > 0).Select(u => u.LastDeathTurn).ToList();
        if (dead.Count > 0) { a.FirstDeathN = 1; a.FirstDeathT = dead.Min(); }
        var doha = p.FirstOrDefault(u => u.Def.Id == "doha");
        if (doha is not null && doha.LastDeathTurn > 0) { a.DohaDeathN = 1; a.DohaDeathT = doha.LastDeathTurn; }
        var names = p.GroupBy(u => u.Def.Id).ToDictionary(g => g.Key, g => Short(g.First().Def));
        var seats = f.Occupied().GroupBy(o => o.Def.Id).ToDictionary(g => g.Key, g => SeatName[g.First().Slot]);
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!names.TryGetValue(id, out var nm)) continue;   // 敵側の帳簿は数えない
            Bump(a.PushTo, nm, t.PushGot);
            if (seats.TryGetValue(id, out var sn)) Bump(a.PushSeat, sn, t.PushGot);
            Bump(a.AimTo, nm, t.AimGot); Bump(a.Skipped, nm, t.AimSkipped); Bump(a.Aimed, nm, t.AimedHits); Bump(a.PassTo, nm, t.PushPassGot);
            a.GiftTurns += t.ShareGiftTurns; a.GiftAttacks += t.ShareGiftAttacks; a.GiftDealt += t.ShareGiftDealt;
            if (id != "doha") continue;
            a.DohaTurns += t.TurnsTaken; a.DohaAttacks += t.Attacks; a.DohaDealt += t.DamageToEnemy; a.DohaWhet += t.Whetted; a.DohaRegurg += t.WhetGotRegurg;
            a.PushTurns += t.PushTurns; a.Pushes += t.Pushes; a.PushZero += t.PushZero; a.PushInGift += t.PushInGift; a.PushCapped += t.PushCapped; a.PushTies += t.PushTies;
            a.GiftSkipped += t.ShareGiftSkipped;
            a.Passes += t.PushPasses; a.Passed += t.PushPassed; a.PassNone += t.PushPassNone; a.PassPeak = Math.Max(a.PassPeak, t.PushPassPeak);
            a.AimTurns += t.AimTurns; a.Aims += t.Aims; a.AimZero += t.AimZero; a.AimNoEligible += t.AimNoEligible; a.AimTies += t.AimTies;
            a.AimSkipNoStrike += t.AimSkipNoStrike; a.AimSkipStoic += t.AimSkipStoic; a.AimSelf += t.AimSelf; a.AimSelfAmt += t.AimSelfAmt; a.AimWinInTurn += t.AimWinInTurn;
        }
        foreach (var (id, t) in r.TallyByUnit) if (names.ContainsKey(id)) a.AimedHits += t.AimedHits;
        // 計数器の第313期の数え方（`PushGot` は計数器では「肩代わりの窓で選ばれたはず」）
        if (BattleContext.PushCensus) { foreach (var (k, v) in a.PushTo) a.OldTo[k] = v; a.OldPushTurns = a.PushTurns; a.OldPushes = a.Pushes; a.OldPushZero = a.PushZero; }
        return a;
    }

    static St Measure(Formation f, S287.Wave w, int n = Seeds)
    {
        var parts = new St[n];
        Parallel.For(0, n, i => parts[i] = Fight(f, w, i));
        var all = new St();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    // ---------------------------------------------------------------------------------
    // §3-1 手番で殴る駒 ／ 殴らない駒
    // ---------------------------------------------------------------------------------
    /// <summary>
    /// 実測: その駒が入った `compare` の行 ＋ 埋め草の台（その駒を前1・ほかはドルガ）× 本編 第2〜5波 × seed 0..9（verbose）。
    /// 手番（`Hands`・ギフトの手番も含む）のうち、その駒が出どころの攻撃（`Attack`・反撃 ／ 割り込みでない）か、敵への被害（`Damage`）が1つでもある手番の割合。
    /// </summary>
    static void Strikers()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds();
        var res = new System.Collections.Concurrent.ConcurrentDictionary<string, (long Hands, long Strike, long Stalled, long Skill)>();
        var units = UnitCatalog.All.ToArray();
        Parallel.ForEach(units, d => res[d.Id] = StrikeCensus(d, rows));
        Console.WriteLine("# 第315期 §3-1 手番で殴る駒 ／ 殴らない駒（ロスター 52 枚）");
        Console.WriteLine();
        Console.WriteLine("実測 ＝ その駒が入った `compare` の行 ＋ 埋め草の台（その駒を前1・ほか4枠はドルガ）× 本編 第2〜5波 × seed 0..9。**殴った手番** ＝ その駒が出どころの攻撃（反撃 ／ 割り込みでない）か敵への被害が1つでもある手番。"
            + "定義 ＝ `BattleContext.StrikesOnTurn`（`Actions` が無いか `Attack` を含む駒は殴る・ただし追い打ち `Pursuer` ／ 不動 `Immobile` は殴らない ／ 術だけの駒は術の手番の過半で殴る駒だけ）。");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 手番の型（`Actions`） | 手番 | 殴った手番（%） | 術の手番（%） | 潰れた手番（%） | 定義 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|---|");
        int disagree = 0;
        foreach (var d in units.OrderBy(d => res[d.Id].Hands == 0 ? 0 : (double)res[d.Id].Strike / res[d.Id].Hands).ThenBy(d => d.Id))
        {
            var (h, s, st, sk) = res[d.Id];
            string acts = d.Actions is null || d.Actions.Count == 0 ? "（なし）" : string.Join("・", d.Actions.Select(a => a.Kind.ToString()));
            bool def = BattleContext.StrikesOnTurn(d);
            bool emp = h > 0 && s * 2 > h;
            if (def != emp) disagree++;
            Console.WriteLine($"| {Short(d)} | {acts} | {h} | {(h == 0 ? "—" : F1(100.0 * s / h))} | {(h == 0 ? "—" : F1(100.0 * sk / h))} | {(h == 0 ? "—" : F1(100.0 * st / h))} | {(def ? "殴る" : "**殴らない**")}{(def != emp ? "（実測と違う）" : "")} |");
        }
        Console.WriteLine();
        Console.WriteLine($"定義と実測（殴った手番が過半か）が違う駒: **{disagree}** 枚。殴らない駒: {string.Join("・", units.Where(d => !BattleContext.StrikesOnTurn(d)).Select(Short))}");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    static (long Hands, long Strike, long Stalled, long Skill) StrikeCensus(UnitDef d, (string Name, Formation F)[] rows)
    {
        var boards = rows.Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, d))).Select(r => r.F).ToList();
        boards.Add(Formation.Build(front1: d, front3: UnitCatalog.Dolga, center: UnitCatalog.Dolga, back1: UnitCatalog.Dolga, back3: UnitCatalog.Dolga));
        long hands = 0, strike = 0, stalled = 0, skill = 0;
        foreach (var f in boards)
            for (int wi = 1; wi < EnemyCatalog.Stages.Count; wi++)
                for (int sd = 0; sd < 10; sd++)
                {
                    var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, BattleEngine.Materialize(EnemyCatalog.Stages[wi].Enemy, BattleContext.EnemyTeam, BossRule.Default.Scale), sd, verbose: true);
                    var mine = p.Where(u => ReferenceEquals(u.Def, d)).Select(u => u.InstanceId).ToHashSet();
                    var ours = p.Select(u => u.InstanceId).ToHashSet();
                    foreach (var h in r.Hands)
                    {
                        if (!mine.Contains(h.ActorId)) continue;
                        hands++;
                        if (h.Outcome == TurnOutcome.Stalled) stalled++;
                        if (h.Outcome == TurnOutcome.Skill) skill++;
                        bool hit = false;
                        for (int k = h.EventStart; k < h.EventEnd && !hit; k++)
                        {
                            var x = r.Events[k];
                            if (x.ActorId != h.ActorId || x.Reaction) continue;
                            if (x.Kind == BattleEventKind.Attack) hit = true;
                            else if (x.Kind == BattleEventKind.Damage && !x.FriendlyFire && !x.Relayed && x.TargetId is int t && !ours.Contains(t)) hit = true;
                        }
                        if (hit) strike++;
                    }
                }
        return (hands, strike, stalled, skill);
    }

    // ---------------------------------------------------------------------------------
    // §3-2〜§3-4 Phase 0（規定のドハ・計数器を立てる）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第315期 Phase 0 —— 規定のドハ・計数器 `PushCensus` を立てて回す（盤面は動かない）");
        Console.WriteLine();
        BattleContext.PushCensus = true;
        try
        {
            Console.WriteLine("## §3-3 窓の最多の分布（seed 0..199・規定の倍率）");
            Console.WriteLine();
            Console.WriteLine("**新** ＝ 窓（ドハの前の手番の終わりから今の手番の頭まで）に敵の攻撃が主目標に選んだ味方（量 → 回数 → 攻撃力 → 席・殴らない駒 ／ 支援を拒む駒は飛ばす）。"
                + "**旧** ＝ 第313期の数え方（窓の肩代わりの実額）。どちらも規定のドハの手番の頭で数えて閉じる（同じ手番）。");
            Console.WriteLine();
            var boards = new[] { ("守り型", Playtest(Guard)), ("守り型 ゴルム↔ドハ", SwapSeats(Playtest(Guard), UnitCatalog.Golm, UnitCatalog.Doha)), ("雷の型", Playtest(Thunder)), ("反撃改2", Row(Ret2)) };
            foreach (var (bn, bf) in boards)
            {
                Console.WriteLine($"### {bn}（{Seats(bf)}）");
                Console.WriteLine();
                Console.WriteLine("| 波 | 勝率 | 窓を読んだ手番 /戦 | 新: 窓が空 ／ 相手なし（%） | 新: 選ばれた相手 | 新: ゴルム（%） | 新: 飛ばした最多（殴らない ／ 支援拒否）/戦 ・顔ぶれ | 旧: 選ばれた相手 | 旧: ゴルム（%） | 主目標に選ばれた /戦（顔ぶれ） | ドハ自身が主目標 /戦 |");
                Console.WriteLine("|---|--:|--:|---|---|--:|---|---|--:|---|--:|");
                foreach (var w in Waves)
                {
                    var s = Measure(bf, w);
                    Console.WriteLine($"| {w.Name} | {s.Win} | {s.Per(s.AimTurns)} | {s.Pct(s.AimZero - s.AimNoEligible, s.AimTurns)} ／ {s.Pct(s.AimNoEligible, s.AimTurns)} | {s.Share(s.AimTo)} | {F1(s.ShareOf(s.AimTo, "ゴルム"))} | "
                        + $"{s.Per(s.AimSkipNoStrike)} ／ {s.Per(s.AimSkipStoic)}・{s.Share(s.Skipped, 3)} | {s.Share(s.OldTo)} | {F1(s.ShareOf(s.OldTo, "ゴルム"))} | {s.Per(s.AimedHits)}（{s.Share(s.Aimed, 4)}） | {s.Per(s.AimSelf)} |");
                }
                Console.WriteLine();
            }

            Console.WriteLine("## §3-4 敵を強くした条件（守り型・規定のドハ・seed 0..199）");
            Console.WriteLine();
            Console.WriteLine("各波の敵の倍率に HP ／ 攻 の百分率を重ねる（ボス 100/100・近衛 ／ 大隊 1000/300・本編 115/115 が基準）。ドハの手番 ＝ `TurnsTaken`（ギフトを含まない・ドハは受け手にならない）。");
            Console.WriteLine();
            Console.WriteLine("| 倍率（HP ／ 攻） | 波 | 勝率 | 倒しT | 負けT | 寿命 | ドハの手番 /戦 | 新: 選ばれた相手 | 新: 窓が空 ／ 相手なし（%） |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|---|---|");
            foreach (var (hp, atk) in new[] { (100, 100), (200, 150), (300, 200), (400, 250), (500, 300), (700, 350), (1000, 400) })
            {
                var ws = MakeWaves(hp, atk);
                foreach (var wn in StrongPick)
                {
                    var s = Measure(Playtest(Guard), ws.First(w => w.Name == wn));
                    Console.WriteLine($"| {hp} ／ {atk} | {wn} | {s.Win} | {s.WinTurn} | {s.LossTurn} | {s.Avg(s.FirstDeathT, s.FirstDeathN)} | {s.Per(s.DohaTurns)} | {s.Share(s.AimTo)} | {s.Pct(s.AimZero - s.AimNoEligible, s.AimTurns)} ／ {s.Pct(s.AimNoEligible, s.AimTurns)} |");
                }
            }
            Console.WriteLine();
            Console.WriteLine($"採った条件: **HP {StrongHp}% ／ 攻 {StrongAtk}%**（`StrongHp` ／ `StrongAtk`）。");
            Console.WriteLine();
        }
        finally { BattleContext.PushCensus = false; }
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §4-2 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(int seeds)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# 第315期 §4-2 代表台 × 波 × 版（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"勝率 ／ 倒しT ／ 負けT ／ 寿命（初めて倒れたT）／ ドハの手番。対照 ＝ ドハ → ドルガ（同じ席・どれかの版で 50% 以上のセルだけ）。差は規定から。強くした条件 ＝ HP {StrongHp}% ／ 攻 {StrongAtk}% を各波の倍率に重ねる。");
        var gate = new List<string>();
        foreach (var (bn0, bf, hasStrong) in Boards())
            foreach (bool strong in hasStrong ? new[] { false, true } : new[] { false })
            {
                string bn = strong ? $"{bn0}【強】" : bn0;
                var waves = strong ? StrongWaves.Where(w => StrongPick.Contains(w.Name)).ToArray() : Waves;
                Console.WriteLine();
                Console.WriteLine($"## {bn}（{Seats(bf)}）");
                Console.WriteLine();
                Console.WriteLine("| 波 | 規定 | DP-a | DP-b | DQ-a | DQ-b | ドルガ | 倒しT（規定 ／ Pa ／ Pb ／ Qa ／ Qb） | 負けT | 寿命 | ドハの手番 /戦 |");
                Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|---|---|---|");
                var detail = new List<(S287.Wave W, St[] V)>();
                foreach (var w in waves)
                {
                    var v = Versions.Select(x => Measure(Swap(bf, UnitCatalog.Doha, x.Def), w, seeds)).ToArray();
                    detail.Add((w, v));
                    string dolga = v.Any(x => x.WinRate >= 50) ? Measure(Swap(bf, UnitCatalog.Doha, UnitCatalog.Dolga), w, seeds).Win : "—";
                    string Cell(int i) => i == 0 ? v[0].Win : $"{v[i].Win}（{Sg(v[i].WinRate - v[0].WinRate)}）";
                    Console.WriteLine($"| {w.Name} | {Cell(0)} | {Cell(1)} | {Cell(2)} | {Cell(3)} | {Cell(4)} | {dolga} | {string.Join(" ／ ", v.Select(x => x.WinTurn))} | {string.Join(" ／ ", v.Select(x => x.LossTurn))} | "
                        + $"{string.Join(" ／ ", v.Select(x => x.Avg(x.FirstDeathT, x.FirstDeathN)))} | {string.Join(" ／ ", v.Select(x => x.Per(x.DohaTurns)))} |");
                    for (int i = 3; i < v.Length; i++)
                        if (v[i].WinRate - v[0].WinRate >= (w.Boss ? 5 : 10)) gate.Add($"{Versions[i].Tag}: {bn} × {w.Name}（{Sg(v[i].WinRate - v[0].WinRate)}）");
                }
                Console.WriteLine();
                Console.WriteLine("背を押した（1戦あたり）:");
                Console.WriteLine();
                Console.WriteLine("| 波 | 版 | 押した ／ 窓を読んだ手番 | 殴った: 窓が空 ／ 相手なし ／ ギフト中 ／ 上限 | 送り出した相手 | 席 | 飛ばした最多（殴らない ／ 支援拒否）・顔ぶれ | 送り出された手番の与ダメ（1回） | 渡した上乗せ（1回）／ 受け手の最大 ／ 吐き戻しの割合 |");
                Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
                foreach (var (w, v) in detail)
                    for (int i = 1; i < v.Length; i++)
                    {
                        var s = v[i];
                        bool q = i >= 3, pw = i is 2 or 4;
                        Console.WriteLine($"| {w.Name} | {Versions[i].Tag} | {s.Per(s.Pushes)} ／ {s.Per(s.PushTurns)} | {s.Per(s.PushZero - (q ? s.AimNoEligible : 0))} ／ {(q ? s.Per(s.AimNoEligible) : "—")} ／ {s.Per(s.PushInGift)} ／ {s.Per(s.PushCapped)} | "
                            + $"{s.Share(s.PushTo)} | {s.Share(s.PushSeat)} | {(q ? $"{s.Per(s.AimSkipNoStrike)} ／ {s.Per(s.AimSkipStoic)}・{s.Share(s.Skipped, 3)}" : "—")} | {s.Per(s.GiftDealt)}（{s.Avg(s.GiftDealt, s.GiftTurns)}） | "
                            + $"{(pw ? $"{s.Per(s.Passed)}（{s.Avg(s.Passed, s.Passes)}）／ {s.PassPeak} ／ {s.Pct(s.DohaRegurg, s.DohaWhet)}%" : "—")} |");
                    }
            }
        Console.WriteLine();
        Console.WriteLine("## §4-4 格子の門（DQ-a ／ DQ-b が規定から +10pt 以上・ボスは +5pt 以上動いたセル）");
        Console.WriteLine();
        if (gate.Count == 0) Console.WriteLine("**該当なし**（格子は回さない）。");
        else foreach (var g in gate) Console.WriteLine($"- {g}");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §4-3 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds();
        int nw = EnemyCatalog.Stages.Count;
        var vers = new[] { Versions[0], Versions[3], Versions[4] };
        var grids = vers.Select(v =>
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = Swap(rows[ri].F, UnitCatalog.Doha, v.Def);
                int wins = 0;
                for (int s = 0; s < Seeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / Seeds;
            });
            return g;
        }).ToArray();
        Console.WriteLine("# 第315期 §4-3 `compare` 64 行 × 版（seed 0..199・基準は規定のドハ）");
        double M(double[,] g, int ri) => Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
        for (int vi = 1; vi < vers.Length; vi++)
        {
            var g = grids[vi]; var b = grids[0];
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Tag}");
            Console.WriteLine();
            Console.WriteLine("| 行 | " + string.Join(" | ", Enumerable.Range(1, nw).Select(w => $"第{w}波")) + " | 第2〜5波 平均 | 差 | 最大の落ち |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Range(0, nw).Select(_ => "--:|")) + "--:|--:|--:|");
            int movedRows = 0, movedCells = 0, outside = 0;
            var drops = new List<int>();
            for (int ri = 0; ri < rows.Length; ri++)
            {
                bool any = Enumerable.Range(0, nw).Any(w => g[ri, w] != b[ri, w]);
                bool holds = rows[ri].F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Doha));
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
            Console.WriteLine($"動いた行 {movedRows} ／ セル {movedCells}・ドハのいない行で動いたセル **{outside}**・全64行の第2〜5波平均 {F1(allB)} → {F1(all)}（{Sg(all - allB)}）・主判定19行の第五波 {F1(p5b)} → {F1(p5)}（歯止め {F1(Baseline.PrimaryFifthFloor)}）");
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
    // §4-4 格子（第297期 `doha297 grid` ／ 第313期 `doha313 grid` と同じ作り・基準は規定のドハ）
    // ---------------------------------------------------------------------------------
    internal static readonly UnitDef[] Attackers = { UnitCatalog.Tome, UnitCatalog.Zan, UnitCatalog.Kata, UnitCatalog.Shiga };

    static void Grid(string waveKey, string ver, bool strong, int seats, int seeds)
    {
        var w = WaveOf(waveKey, strong);
        var dv = Ver(ver);
        var pool = UnitCatalog.All.Where(d => !ReferenceEquals(d, UnitCatalog.Doha)).ToArray();
        int step = Math.Max(1, 120 / Math.Max(1, seats));
        var seatSet = Perms(5).Where((_, i) => i % step == 0).Take(seats).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# 第315期 格子 —— {w.Name}{(strong ? "【強】" : "")} × 規定 ／ {VerTag(dv)}（固定枠 ドハ ＋ アタッカー1枚 × 探索枠3・席 {seatSet.Length} 通り・seed 0..{seeds - 1}）");
        Console.WriteLine();
        int Wins(Formation g) { int c = 0; for (int sd = 0; sd < seeds; sd++) if (BattleEngine.Run(BattleEngine.Materialize(g, BattleContext.PlayerTeam), w.Make(), sd, verbose: false).PlayerWon) c++; return c; }
        Formation Seat(UnitDef[] five, int[] perm) { var g = new Formation(); for (int s = 0; s < 5; s++) g[perm[s]] = five[s]; return g; }
        var rows = new System.Collections.Concurrent.ConcurrentBag<(string Atk, string[] Free, double Base, double Ver, double Dolga, int[] Seat)>();
        foreach (var atk in Attackers)
        {
            var free = pool.Where(d => !ReferenceEquals(d, atk)).ToArray();
            var combos = new List<UnitDef[]>();
            for (int i = 0; i < free.Length; i++) for (int j = i + 1; j < free.Length; j++) for (int k = j + 1; k < free.Length; k++) combos.Add(new[] { free[i], free[j], free[k] });
            Parallel.ForEach(combos, c =>
            {
                double Best(UnitDef d, out int[] seat)
                {
                    var five = new[] { d, atk, c[0], c[1], c[2] };
                    int best = -1; seat = seatSet[0];
                    foreach (var perm in seatSet) { int x = Wins(Seat(five, perm)); if (x > best) { best = x; seat = perm; } if (best == seeds) break; }
                    return 100.0 * best / seeds;
                }
                double b = Best(UnitCatalog.Doha, out var sb), v = Best(dv, out var sv);
                if (Math.Max(b, v) < 50) return;
                var seat = v >= b ? sv : sb;
                double dolga = 100.0 * Wins(Seat(new[] { UnitCatalog.Dolga, atk, c[0], c[1], c[2] }, seat)) / seeds;
                rows.Add((Short(atk), c.Select(Short).ToArray(), b, v, dolga, seat));
            });
        }
        var all = rows.ToList();
        bool Need(double win, double dolga) => win >= 50 && dolga <= win / 2;
        Console.WriteLine($"台の数（規定か版で 50% 以上）{all.Count}・所要 {sw.Elapsed.TotalSeconds:F0} 秒。**ドハが要る台** ＝ その版で 50% 以上 かつ ドハ → ドルガで半分以下（R383）。");
        Console.WriteLine();
        Console.WriteLine($"| アタッカー | 台（規定 50%以上） | 台（{VerTag(dv)} 50%以上） | ドハが要る台（規定 ／ {VerTag(dv)}） | 版 − 規定 の平均 | 版が +10pt 以上 ／ −10pt 以下 | 最高（規定 ／ 版） |");
        Console.WriteLine("|---|--:|--:|---|--:|---|---|");
        foreach (var g in all.GroupBy(r => r.Atk))
            Console.WriteLine($"| {g.Key} | {g.Count(r => r.Base >= 50)} | {g.Count(r => r.Ver >= 50)} | {g.Count(r => Need(r.Base, r.Dolga))} ／ {g.Count(r => Need(r.Ver, r.Dolga))} | {g.Average(r => r.Ver - r.Base):+0.0;-0.0} | "
                + $"{g.Count(r => r.Ver - r.Base >= 10)} ／ {g.Count(r => r.Ver - r.Base <= -10)} | {g.Max(r => r.Base):F0} ／ {g.Max(r => r.Ver):F0} |");
        Console.WriteLine();
        string Kinds(IEnumerable<(string Atk, string[] Free, double Base, double Ver, double Dolga, int[] Seat)> rs)
            => string.Join("・", rs.SelectMany(r => r.Free).GroupBy(x => x).OrderByDescending(x => x.Count()).Take(15).Select(x => $"{x.Key} {x.Count()}"));
        Console.WriteLine($"自由枠に入った駒（ドハが要る台・{VerTag(dv)}）: " + Kinds(all.Where(r => Need(r.Ver, r.Dolga))));
        Console.WriteLine();
        Console.WriteLine($"自由枠に入った駒（版が +10pt 以上の台）: " + Kinds(all.Where(r => r.Ver - r.Base >= 10)));
        Console.WriteLine();
        Console.WriteLine("| 規定 | 版 | ドルガ | アタッカー | 自由枠 | 席（ドハ・アタッカー・自由枠の順の枠番号） |");
        Console.WriteLine("|--:|--:|--:|---|---|---|");
        foreach (var r in all.OrderByDescending(r => r.Ver - r.Base).ThenByDescending(r => r.Ver).Take(15))
            Console.WriteLine($"| {r.Base:F0} | {r.Ver:F0} | {r.Dolga:F0} | {r.Atk} | {string.Join("・", r.Free)} | {string.Join(",", r.Seat)} |");
        Console.WriteLine();
        Console.WriteLine("版で下がった台（下位 5）:");
        Console.WriteLine();
        Console.WriteLine("| 規定 | 版 | ドルガ | アタッカー | 自由枠 |");
        Console.WriteLine("|--:|--:|--:|---|---|");
        foreach (var r in all.OrderBy(r => r.Ver - r.Base).Take(5))
            Console.WriteLine($"| {r.Base:F0} | {r.Ver:F0} | {r.Dolga:F0} | {r.Atk} | {string.Join("・", r.Free)} |");
    }

    static IEnumerable<int[]> Perms(int n)
    {
        var a = Enumerable.Range(0, n).ToArray();
        IEnumerable<int[]> Rec(int k)
        {
            if (k == n) { yield return (int[])a.Clone(); yield break; }
            for (int i = k; i < n; i++)
            {
                (a[k], a[i]) = (a[i], a[k]);
                foreach (var x in Rec(k + 1)) yield return x;
                (a[k], a[i]) = (a[i], a[k]);
            }
        }
        return Rec(0);
    }

    // ---------------------------------------------------------------------------------
    // §5 台本の並び
    // ---------------------------------------------------------------------------------
    static void Memo(string boardPart, string wave, int seed, string ver, bool strong)
    {
        var (name, f0, _) = Boards().First(b => b.Name.Contains(boardPart, StringComparison.Ordinal));
        var f = Swap(f0, UnitCatalog.Doha, Ver(ver));
        var w = WaveOf(wave, strong);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# doha315 memo —— {name} × {VerTag(Ver(ver))} × {w.Name}{(strong ? "【強】" : "")} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}（`ShareGive` {r.Events.Count(x => x.Kind == BattleEventKind.ShareGive)} 件）");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        foreach (var x in r.Events)
        {
            if (x.Kind is not (BattleEventKind.ShareGive or BattleEventKind.Skill or BattleEventKind.Attack or BattleEventKind.Whet or BattleEventKind.Death or BattleEventKind.Move)) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.Reaction ? "  Reaction" : "")}");
        }
        Console.WriteLine("```");
    }

    /// <summary>2つの版で勝敗が割れた seed（seed 0..199）と、その seed の最初の「背を押す」の相手 ／ 窓の量 ／ 送り出された手番の与ダメ。</summary>
    static void Diff(string boardPart, string wave, string va, string vb, bool strong)
    {
        var (name, f0, _) = Boards().First(b => b.Name.Contains(boardPart, StringComparison.Ordinal));
        var w = WaveOf(wave, strong);
        Console.WriteLine($"# doha315 diff —— {name} × {w.Name}{(strong ? "【強】" : "")} × {VerTag(Ver(va))} ／ {VerTag(Ver(vb))}");
        Console.WriteLine();
        Console.WriteLine("| seed | 勝敗 | 押した（相手 ×回）| 送り出された手番の与ダメ | 上乗せ |");
        Console.WriteLine("|--:|---|---|---|---|");
        int na = 0, nb = 0;
        for (int sd = 0; sd < Seeds; sd++)
        {
            var ra = One(Swap(f0, UnitCatalog.Doha, Ver(va)), w, sd); var rb = One(Swap(f0, UnitCatalog.Doha, Ver(vb)), w, sd);
            if (ra.Won == rb.Won) continue;
            if (ra.Won) na++; else nb++;
            Console.WriteLine($"| {sd} | {(ra.Won ? "勝" : "負")} ／ {(rb.Won ? "勝" : "負")} | {ra.To} ／ {rb.To} | {ra.Gift} ／ {rb.Gift} | {ra.Pass} ／ {rb.Pass} |");
        }
        Console.WriteLine();
        Console.WriteLine($"{VerTag(Ver(va))} だけ勝ち {na}・{VerTag(Ver(vb))} だけ勝ち {nb}");
    }

    static (bool Won, string To, long Gift, long Pass) One(Formation f, S287.Wave w, int sd)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), sd, verbose: false);
        var names = p.GroupBy(u => u.Def.Id).ToDictionary(g => g.Key, g => Short(g.First().Def));
        string to = string.Join("・", r.TallyByUnit.Where(x => names.ContainsKey(x.Key) && x.Value.PushGot > 0).Select(x => $"{names[x.Key]}×{x.Value.PushGot}"));
        long gift = r.TallyByUnit.Where(x => names.ContainsKey(x.Key)).Sum(x => (long)x.Value.ShareGiftDealt);
        long pass = r.TallyByUnit.GetValueOrDefault("doha")?.PushPassed ?? 0;
        return (r.PlayerWon, to == "" ? "—" : to, gift, pass);
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

    static readonly MethodInfo AddUnit = typeof(BattleContext).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic, new[] { typeof(UnitState) })
                                         ?? throw new InvalidOperationException("BattleContext.Add が見つからない");
    static readonly PropertyInfo TurnProp = typeof(BattleContext).GetProperty("Turn") ?? throw new InvalidOperationException("Turn が見つからない");
    static readonly PropertyInfo TallyProp = typeof(BattleContext).GetProperty("TallyByUnit", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("TallyByUnit が見つからない");
    static readonly FieldInfo GiftQueue = typeof(BattleContext).GetField("_giftQueue", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_giftQueue が見つからない");
    static readonly FieldInfo InGift = typeof(BattleContext).GetField("_inGift", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_inGift が見つからない");
    static readonly FieldInfo Forced = typeof(BattleContext).GetField("_forcedTarget", BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("_forcedTarget が見つからない");
    static UnitTally Tal(BattleContext ctx, string id) => ((Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!).GetValueOrDefault(id) ?? new UnitTally();
    static int QueueCount(BattleContext ctx) => ((System.Collections.ICollection)GiftQueue.GetValue(ctx)!).Count;
    static UnitState? QueueFirstTo(BattleContext ctx)
    {
        foreach (var o in (System.Collections.IEnumerable)GiftQueue.GetValue(ctx)!)
            return (UnitState)o.GetType().GetField("Item2")!.GetValue(o)!;
        return null;
    }

    /// <summary>盤面を直に組む（`Run` を通さない・乱数は seed 0）。敵は2体（ドルガ ＝ 前1 ／ 前3・倍率なし）。</summary>
    static BattleContext Ctx(Formation pl, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        var foe = UnitCatalog.Dolga.WithStats(5000, 10);   // 倒れない敵（反撃で落ちて一撃が消えないように）・攻 10
        e = BattleEngine.Materialize(Formation.Build(front1: foe, front3: foe), BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnProp.SetValue(ctx, 1);
        return ctx;
    }

    /// <summary>敵 <paramref name="foe"/> の攻撃を1回、主目標 <paramref name="aim"/> に向けて振らせる（的の固定 `_forcedTarget` ＝ 主目標の選択を通る一撃）。</summary>
    static void Hit(BattleContext ctx, UnitState foe, UnitState aim)
    {
        Forced.SetValue(ctx, aim);
        ctx.PerformAttack(foe);
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# doha315 check —— 第315期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // (a) 主目標に選ばれた量で最多が決まる・巨躯の中継と味方由来は数えない
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Zan, front3: UnitCatalog.Golm, center: UnitCatalog.DohaDQa, back1: UnitCatalog.Tome, back3: UnitCatalog.Sora), out var p, out var e);
            var doha = p.First(u => u.Def.Id == "doha"); var zan = p.First(u => u.Def.Id == "zan"); var golm = p.First(u => u.Def.Id == "golm");
            var misa = p.First(u => u.Def.Id == "tome"); var sora = p.First(u => u.Def.Id == "sora");
            e[1].AtkBonus = 30;   // 前3 のドルガは強く殴る
            Hit(ctx, e[0], zan); Hit(ctx, e[0], zan);   // ザン 2 回（弱い一撃）
            Hit(ctx, e[1], misa);                        // ミサ 1 回（強い一撃 ＝ 量はミサが上）
            ctx.ApplyDamage(sora, 200, misa);            // 味方由来（主目標の選択を通らない）
            ctx.ApplyDamage(golm, 200, e[0]);            // 選択を通らない直のダメージ（中継の段と同じ扱い）
            var tg = Tal(ctx, "golm"); var tz = Tal(ctx, "zan"); var tm = Tal(ctx, "tome"); var ts = Tal(ctx, "sora");
            bool ok1 = ctx.SharePushTurn(doha);
            var to = QueueFirstTo(ctx);
            Expect("(a) 窓の最多は主目標に選ばれた量で決まる（ミサ 1 回・強い ＞ ザン 2 回・弱い）", ok1 && to == misa, $"相手 {(to is null ? "—" : Short(to.Def))}・ザン {tz.AimedHits} 回 ／ {tz.AimedAmt}（{(zan.IsAlive ? "生" : "死")}）・ミサ {tm.AimedHits} 回 ／ {tm.AimedAmt}・飛ばし {Tal(ctx, "doha").AimSkipNoStrike}/{Tal(ctx, "doha").AimSkipStoic}");
            Expect("(a) 巨躯（ゴルム）が飲んだ中継・味方由来・選択を通らない直のダメージは数えない", tg.AimedHits == 0 && ts.AimedHits == 0, $"ゴルム {tg.AimedHits}・ソラ {ts.AimedHits}");
            TurnProp.SetValue(ctx, 2);
            Hit(ctx, e[0], zan); Hit(ctx, e[0], zan); Hit(ctx, e[0], misa);
            ctx.SharePushTurn(doha);
            Expect("(a) 窓は読んだ手番で閉じ、次の窓は新しく数える（T2: ザン 2 撃 ＞ ミサ 1 撃・同じ打点）", QueueCount(ctx) == 2 && LastTo(ctx) == zan, $"控え {QueueCount(ctx)} 件・2つ目の相手 {(LastTo(ctx) is { } l ? Short(l.Def) : "—")}");
        }
        // (a2) 量 ・回数が同じなら攻撃力 → 席
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Zan, front3: UnitCatalog.Sora, center: UnitCatalog.DohaDQa), out var p, out var e);
            var doha = p.First(u => u.Def.Id == "doha"); var zan = p.First(u => u.Def.Id == "zan"); var sora = p.First(u => u.Def.Id == "sora");
            Hit(ctx, e[0], sora); Hit(ctx, e[0], zan);
            ctx.SharePushTurn(doha);
            var to = QueueFirstTo(ctx);
            UnitState expect = zan.CurrentAttack != sora.CurrentAttack ? (zan.CurrentAttack > sora.CurrentAttack ? zan : sora) : zan;
            Expect("(a) 量 ・回数が同じなら攻撃力の高い方 → 席の若い方", to == expect && Tal(ctx, "doha").PushTies == 1, $"相手 {(to is null ? "—" : Short(to.Def))}（ザン 攻 {zan.CurrentAttack}・ソラ 攻 {sora.CurrentAttack}）");
        }
        // (a3) 庇われても元の主目標が数えられる（ガルドの庇いは主目標を差し替えるだけ）
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Zan, front3: UnitCatalog.Gald, center: UnitCatalog.DohaDQa, back1: UnitCatalog.Tome), out var p, out var e);
            var zan = p.First(u => u.Def.Id == "zan"); var gald = p.First(u => u.Def.Id == "gald");
            for (int i = 0; i < 8; i++) ctx.PerformAttack(e[0]);
            long zh = Tal(ctx, "zan").AimedHits, gh = Tal(ctx, "gald").AimedHits;
            int onGald = ctx.Events.Count(x => x.Kind == BattleEventKind.Damage && x.ActorId == e[0].InstanceId && x.TargetId == gald.InstanceId && !x.Relayed);
            Expect("(a) 庇い（ガルド）の前の主目標を数える: 8 撃 ＝ ザン ＋ ガルド・8 撃すべてをガルドが受けても、ザンが主目標だった分はザンに数える", zh + gh == 8 && zh > 0 && onGald == 8,
                $"主目標 ザン {zh}・ガルド {gh}・ガルドが受けた一撃 {onGald}");
        }
        // (b) 殴らない駒 ／ 支援拒否は飛ばす・送り出せる相手がいなければ殴る
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Ban, front3: UnitCatalog.Gald, center: UnitCatalog.DohaDQa, back1: UnitCatalog.Zan), out var p, out var e);
            var doha = p.First(u => u.Def.Id == "doha"); var ban = p.First(u => u.Def.Id == "ban"); var gald = p.First(u => u.Def.Id == "gald"); var zan = p.First(u => u.Def.Id == "zan");
            Hit(ctx, e[0], ban); Hit(ctx, e[0], ban); Hit(ctx, e[0], ban); Hit(ctx, e[0], zan);
            bool ok = ctx.SharePushTurn(doha);
            Expect("(b) 窓の最多が殴らない駒（バン）なら飛ばして次に多く狙われた味方（ザン）", ok && QueueFirstTo(ctx) == zan && Tal(ctx, "doha").AimSkipNoStrike == 1 && Tal(ctx, "ban").AimSkipped == 1,
                $"相手 {(QueueFirstTo(ctx) is { } q ? Short(q.Def) : "—")}・飛ばした（殴らない）{Tal(ctx, "doha").AimSkipNoStrike}");
            TurnProp.SetValue(ctx, 2);
            Hit(ctx, e[0], gald); Hit(ctx, e[0], gald); Hit(ctx, e[0], ban);
            bool ok2 = ctx.SharePushTurn(doha);
            Expect("(b) 支援を拒む駒（ガルド）と殴らない駒だけなら送り出さない（殴る＝偽を返す）", !ok2 && QueueCount(ctx) == 1 && Tal(ctx, "doha").AimSkipStoic == 1 && Tal(ctx, "doha").AimNoEligible == 1,
                $"飛ばした（支援拒否）{Tal(ctx, "doha").AimSkipStoic}・相手なし {Tal(ctx, "doha").AimNoEligible}");
            TurnProp.SetValue(ctx, 3);
            bool ok3 = ctx.SharePushTurn(doha);
            Expect("(b) 窓が空なら殴る（偽を返す）", !ok3 && Tal(ctx, "doha").PushZero == 2, $"PushZero {Tal(ctx, "doha").PushZero}");
        }
        // (b2) 手番で殴る駒の定義
        {
            var no = UnitCatalog.All.Where(d => !BattleContext.StrikesOnTurn(d)).Select(Short).OrderBy(x => x).ToArray();
            string[] expect = { "カド", "クグ", "クビ", "シオ", "スィド", "ササ", "ツギ", "トモ", "ネル", "ハギ", "バン", "ヒサ", "ヒヨ", "ベニ", "ヴェル", "ガレ", "アカ" };
            Expect("(b) 手番で殴らない駒は Phase 0 §3-1 の一覧どおり（17 枚）", no.Length == expect.Length && expect.All(no.Contains), string.Join("・", no));
        }
        // (c) DQ-b: 上乗せがすべて移りドハは素に戻る
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Zan, center: UnitCatalog.DohaDQb), out var p, out var e);
            var doha = p.First(u => u.Def.Id == "doha"); var zan = p.First(u => u.Def.Id == "zan");
            ctx.Whet(doha, 30);
            Hit(ctx, e[0], zan);
            int atk0 = zan.CurrentAttack, bon0 = doha.AtkBonus;
            bool ok = ctx.SharePushTurn(doha);
            Expect("(c) DQ-b: ドハの上乗せがすべて相手に移り、ドハは素の攻撃力に戻る", ok && doha.AtkBonus == 0 && doha.CurrentAttack == doha.Def.Attack && zan.CurrentAttack == atk0 + bon0 && Tal(ctx, "doha").PushPassed == bon0,
                $"ドハ {doha.CurrentAttack}（素 {doha.Def.Attack}）・ザン {atk0} → {zan.CurrentAttack}・渡した {Tal(ctx, "doha").PushPassed}");
            var ctx2 = Ctx(Formation.Build(front1: UnitCatalog.Zan, center: UnitCatalog.DohaDQa), out var p2, out var e2);
            var doha2 = p2.First(u => u.Def.Id == "doha"); var zan2 = p2.First(u => u.Def.Id == "zan");
            ctx2.Whet(doha2, 30);
            Hit(ctx2, e2[0], zan2);
            int b2 = doha2.AtkBonus;
            ctx2.SharePushTurn(doha2);
            Expect("(c) DQ-a は上乗せを渡さない", doha2.AtkBonus == b2 && b2 > 0, $"ドハの上乗せ {doha2.AtkBonus}");
        }
        // (d) 送り出された手番の中で背を押さない・同じターンに2度押さない・窓は閉じない
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Zan, center: UnitCatalog.DohaDQa), out var p, out var e);
            var doha = p.First(u => u.Def.Id == "doha"); var zan = p.First(u => u.Def.Id == "zan");
            Hit(ctx, e[0], zan);
            InGift.SetValue(ctx, true);
            bool ok = ctx.SharePushTurn(doha);
            InGift.SetValue(ctx, false);
            bool ok2 = ctx.SharePushTurn(doha);
            Hit(ctx, e[0], zan);
            bool ok3 = ctx.SharePushTurn(doha);
            Expect("(d) ギフトの手番の中では押さない（窓は閉じない）・同じターンの2度目も押さない", !ok && ok2 && !ok3 && QueueCount(ctx) == 1 && Tal(ctx, "doha").PushInGift == 1 && Tal(ctx, "doha").PushCapped == 1,
                $"ギフト中 {Tal(ctx, "doha").PushInGift}・上限 {Tal(ctx, "doha").PushCapped}");
        }
        // (e) 全戦の帳尻: 送り出された手番 ＋ 飛ばした ＝ 押した（連鎖しない）
        {
            long pushes = 0, turns = 0, skipped = 0;
            foreach (var b in new[] { Playtest(Guard), Playtest(Thunder), Row(Ret2) })
                foreach (var w in Waves)
                    for (int sd = 0; sd < 30; sd++)
                    {
                        var r = BattleEngine.Run(BattleEngine.Materialize(Swap(b, UnitCatalog.Doha, UnitCatalog.DohaDQb), BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                        foreach (var (id, t) in r.TallyByUnit) { pushes += t.Pushes; turns += t.ShareGiftTurns; skipped += t.ShareGiftSkipped; }
                    }
            Expect("(e) 守り型 ／ 雷の型 ／ 反撃改2 × 8 波 × seed 0..29（DQ-b）: 送り出された手番 ＋ 飛ばした ＝ 押した・押した回数 > 0", pushes > 0 && turns + skipped == pushes, $"押した {pushes}・手番 {turns}・飛ばした {skipped}");
        }
        // (f) verbose の有無で勝敗が同じ（DQ-a ／ DQ-b × 守り型 ／ 雷の型 × 8 波 ＋ 強くした4波 × seed 0..19）
        {
            int diff = 0, n = 0;
            foreach (var b in new[] { Playtest(Guard), Playtest(Thunder) })
                foreach (var v in new[] { UnitCatalog.DohaDQa, UnitCatalog.DohaDQb })
                    foreach (var w in Waves.Concat(StrongWaves.Where(x => StrongPick.Contains(x.Name))))
                        for (int sd = 0; sd < 20; sd++)
                        {
                            var f = Swap(b, UnitCatalog.Doha, v);
                            var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            n++;
                            if (r0.PlayerWon != r1.PlayerWon || r0.Turns != r1.Turns) diff++;
                        }
            Expect("(f) verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"{n} 戦中 {diff} 戦が違う");
        }
        // (g) 計数器を立てても規定のドハの戦は1ビットも動かない・狙われの窓を数えている
        {
            int diff = 0, n = 0; long aims = 0;
            var rows = CompareBuilds().Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Doha))).Select(r => r.F).Append(Playtest(Guard)).Append(Playtest(Thunder)).ToArray();
            var off = new List<(bool, int)>();
            foreach (var f in rows) foreach (var w in Waves) for (int sd = 0; sd < 20; sd++) { var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false); off.Add((r.PlayerWon, r.Turns)); }
            BattleContext.PushCensus = true;
            try
            {
                int k = 0;
                foreach (var f in rows) foreach (var w in Waves) for (int sd = 0; sd < 20; sd++)
                {
                    var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                    n++;
                    if ((r.PlayerWon, r.Turns) != off[k++]) diff++;
                    aims += r.TallyByUnit.GetValueOrDefault("doha")?.AimTurns ?? 0;
                }
            }
            finally { BattleContext.PushCensus = false; }
            Expect("(g) 計数器を立てても勝敗 ／ 決着T が同じ（ドハの行 ＋ 試遊2台 × 8 波 × seed 0..19）・狙われの窓を数えている", diff == 0 && aims > 0, $"{n} 戦中 {diff} 戦が違う・窓を読んだ手番 {aims}");
        }
        // (h) 新しい窓口で ctx.PickOne ／ Roll を使っていない
        {
            string src = File.ReadAllText("BattleCore/BattleEngine.cs");
            int a = src.IndexOf("// ---- 第315期 —— 狙われた仲間の背を押す", StringComparison.Ordinal);
            int b = a < 0 ? -1 : src.IndexOf("void NoteWallOnSharer", a, StringComparison.Ordinal);
            string region = a < 0 || b < 0 ? "" : src[a..b];
            Expect("(h) 第315期の窓口（engine の区画）に `PickOne` も `Roll` も無い", region.Length > 0 && !region.Contains("PickOne") && !region.Contains("Roll("), $"区画 {region.Length} 字");
        }
        // (i) 版の定義
        {
            bool same(UnitDef d) => d.MaxHp == UnitCatalog.Doha.MaxHp && d.Attack == UnitCatalog.Doha.Attack && d.Speed == UnitCatalog.Doha.Speed && UnitCatalog.Doha.Traits.All(d.Traits.Contains);
            Expect("(i) DQ-a ／ DQ-b は規定のドハの数値と札をそのまま持ち、`SharePush` ＋ `SharePushAimed`（DQ-b は ＋ `SharePushPower`）・手番は術・`All` に入っていない",
                new[] { UnitCatalog.DohaDQa, UnitCatalog.DohaDQb }.All(d => same(d) && d.Traits.Contains(TraitId.SharePush) && d.Traits.Contains(TraitId.SharePushAimed) && d.Actions is { Count: 1 } && d.Actions[0].Kind == ActionKind.Skill && !UnitCatalog.All.Contains(d))
                && UnitCatalog.DohaDQb.Traits.Contains(TraitId.SharePushPower) && !UnitCatalog.DohaDQa.Traits.Contains(TraitId.SharePushPower), "");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }

    static UnitState? LastTo(BattleContext ctx)
    {
        UnitState? last = null;
        foreach (var o in (System.Collections.IEnumerable)GiftQueue.GetValue(ctx)!) last = (UnitState)o.GetType().GetField("Item2")!.GetValue(o)!;
        return last;
    }
}
