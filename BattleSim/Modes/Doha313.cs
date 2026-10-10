using System.Reflection;
using BattleCore;
using static Common;
using S287 = Shock287Diag;
using S307 = Som307Diag;

// =====================================================================================
// doha313 —— 第313期「ドハの手番: 背を押す（DP-a ／ DP-b）＋ 対照 罪の在り処（DP-c）」。
// 指示書は design/PHASE313_DOHA_PUSH_SPEC.md ／ 報告は design/PHASE313_DOHA_PUSH.md。
//
//     dotnet run --project BattleSim -c Release 0 doha313 p0                 # §3 Phase 0（規定のドハ・計数器 `PushCensus` を立てて回す）
//     dotnet run --project BattleSim -c Release 0 doha313 boards [seeds]     # §4-2 代表台 × 8 波 × 版（規定 ／ DP-a ／ DP-b ／ DP-c ／ ドルガ）
//     dotnet run --project BattleSim -c Release 0 doha313 compare            # §4-3 `compare` 64 行 × 版（(G2) つき）
//     dotnet run --project BattleSim -c Release 0 doha313 grid <guard|boss> <a|b|c> [席=6] [seed=10]   # §4-4 第297期の「ドハが要る台」の格子（規定と版を同じ台で）
//     dotnet run --project BattleSim -c Release 0 doha313 memo <台の一部> <boss|guard|bat|1..5> <seed> <規定|a|b|c>   # 台本の並び（§5）
//     dotnet run --project BattleSim -c Release 0 doha313 check              # 自己検査
//
// 規定の駒・札・数値・波は1つも変えない。版は `UnitCatalog.DohaDPa` ／ `DohaDPb` ／ `DohaDPc` を台の規定のドハと同じ席で差し替えるだけ。
// **予測のファイルが無ければ本測定（boards ／ compare ／ grid）を走らせない**（R397・`.tmp/p313/predict.md`）。
// =====================================================================================
static class Doha313Diag
{
    const string PredictFile = ".tmp/p313/predict.md";
    const int Seeds = 200;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "compare" or "grid" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"doha313: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "p0": P0(); return;
            case "boards": BoardsAll(int.Parse(A(3, Seeds.ToString()))); return;
            case "compare": CompareAll(); return;
            case "grid": Grid(A(3, "guard"), A(4, "a"), int.Parse(A(5, "6")), int.Parse(A(6, "10"))); return;
            case "memo": Memo(A(3, "守り型"), A(4, "guard"), int.Parse(A(5, "0")), A(6, "a")); return;
            case "check": Check(); return;
            default: Console.WriteLine("doha313: モードは p0 / boards / compare / grid / memo / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 版・台・波（測る前に固定）
    // ---------------------------------------------------------------------------------
    internal static readonly (string Tag, UnitDef Def)[] Versions =
    {
        ("規定", UnitCatalog.Doha), ("DP-a", UnitCatalog.DohaDPa), ("DP-b", UnitCatalog.DohaDPb), ("DP-c", UnitCatalog.DohaDPc),
    };
    static UnitDef Ver(string s) => s switch
    {
        "a" or "DP-a" => UnitCatalog.DohaDPa, "b" or "DP-b" => UnitCatalog.DohaDPb, "c" or "DP-c" => UnitCatalog.DohaDPc, _ => UnitCatalog.Doha,
    };
    static string VerTag(UnitDef d) => Versions.First(v => ReferenceEquals(v.Def, d)).Tag;

    static string Short(UnitDef d) => S307.Short(d);
    static string Seats(Formation f) => S307.Seats(f);
    static string F1(double x) => S307.F1(x);
    static string Sg(double x) => x >= 0 ? $"+{x:F1}" : $"{x:F1}";

    /// <summary>席を保ったまま <paramref name="from"/> を <paramref name="to"/> に替える（陣形・レリックも保つ）。</summary>
    static Formation Swap(Formation f, UnitDef from, UnitDef to)
    {
        var g = f.Clone();
        foreach ((int slot, UnitDef d) in f.Occupied()) if (ReferenceEquals(d, from)) g[slot] = to;
        return g;
    }

    /// <summary>2枚の席を入れ替える（陣形・レリックも保つ）。</summary>
    static Formation SwapSeats(Formation f, UnitDef a, UnitDef b)
    {
        var g = f.Clone();
        int sa = f.Occupied().First(o => ReferenceEquals(o.Def, a)).Slot, sb = f.Occupied().First(o => ReferenceEquals(o.Def, b)).Slot;
        g[sa] = b; g[sb] = a;
        return g;
    }

    static Formation Playtest(string n) => Presets.Playtest.First(r => r.Name == n).F;
    internal const string Guard = "試遊・標 守り型", Thunder = "試遊・感電 雷の型";

    /// <summary>§4-2 の代表台: 守り型 ／ 守り型のゴルム ↔ ドハ ／ 雷の型 ／ `compare` と交差帯のドハのいる行（規定の駒）。</summary>
    internal static (string Name, Formation F)[] Boards()
    {
        var g = Playtest(Guard);
        var l = new List<(string, Formation)>
        {
            ("守り型", g),
            ("守り型 ゴルム↔ドハ", SwapSeats(g, UnitCatalog.Golm, UnitCatalog.Doha)),
            ("雷の型", Playtest(Thunder)),
        };
        foreach (var (n, f) in CompareBuilds().Concat(CrossBuilds()))
            if (f.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Doha)) && !l.Any(x => x.Item1 == n)) l.Add((n, f));
        return l.ToArray();
    }

    static readonly S287.Wave[] Waves = S307.Waves;   // ボス ／ 近衛 ／ 大隊 ／ 本編 第1〜5波
    static S287.Wave WaveOf(string n) => S307.WaveOf(n);

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    sealed class St
    {
        public long N, Wins, WinT, LossT, FirstDeathN, FirstDeathT, DohaDeathN, DohaDeathT, DohaNoTurn, DohaAttacks, DohaDealt, DohaAtkEnd;
        public long PushTurns, Pushes, PushZero, PushInGift, PushCapped, PushTies, PushShoulder, PushWindow, GiftTurns, GiftAttacks, GiftDealt, GiftSkipped;
        public long Passes, Passed, PassNone, PassPeak, DohaWhet, DohaRegurg, SinTurns, SinZero, SinTies, SinFresh, SinLayer, SinNoop;
        public long WallHits, WallAmt, WallRelay, WallRelayAmt, Shoulder, AllyOrigin, WinInTurn;
        public readonly Dictionary<string, long> PushTo = new(), SinHits = new(), SinDealt = new(), PassTo = new(), ShoulderBy = new();
        public void Add(St o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; LossT += o.LossT; FirstDeathN += o.FirstDeathN; FirstDeathT += o.FirstDeathT; DohaDeathN += o.DohaDeathN; DohaDeathT += o.DohaDeathT;
            DohaNoTurn += o.DohaNoTurn; DohaAttacks += o.DohaAttacks; DohaDealt += o.DohaDealt; DohaAtkEnd += o.DohaAtkEnd;
            PushTurns += o.PushTurns; Pushes += o.Pushes; PushZero += o.PushZero; PushInGift += o.PushInGift; PushCapped += o.PushCapped; PushTies += o.PushTies;
            PushShoulder += o.PushShoulder; PushWindow += o.PushWindow; GiftTurns += o.GiftTurns; GiftAttacks += o.GiftAttacks; GiftDealt += o.GiftDealt; GiftSkipped += o.GiftSkipped;
            Passes += o.Passes; Passed += o.Passed; PassNone += o.PassNone; PassPeak = Math.Max(PassPeak, o.PassPeak); DohaWhet += o.DohaWhet; DohaRegurg += o.DohaRegurg;
            SinTurns += o.SinTurns; SinZero += o.SinZero; SinTies += o.SinTies; SinFresh += o.SinFresh; SinLayer += o.SinLayer; SinNoop += o.SinNoop;
            WallHits += o.WallHits; WallAmt += o.WallAmt; WallRelay += o.WallRelay; WallRelayAmt += o.WallRelayAmt; Shoulder += o.Shoulder; AllyOrigin += o.AllyOrigin; WinInTurn += o.WinInTurn;
            foreach (var (src, dst) in new[] { (o.PushTo, PushTo), (o.SinHits, SinHits), (o.SinDealt, SinDealt), (o.PassTo, PassTo), (o.ShoulderBy, ShoulderBy) })
                foreach (var (k, v) in src) dst[k] = dst.GetValueOrDefault(k) + v;
        }
        public string Win => N == 0 ? "—" : F1(100.0 * Wins / N);
        public double WinRate => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTurn => Wins == 0 ? "—" : F1((double)WinT / Wins);
        public string LossTurn => N - Wins == 0 ? "—" : F1((double)LossT / (N - Wins));
        public string Per(long x) => F1(N == 0 ? 0 : (double)x / N);
        public string Avg(long x, long n) => n == 0 ? "—" : F1((double)x / n);
        public string Pct(long x, long n) => n == 0 ? "—" : F1(100.0 * x / n);
        public string Top(Dictionary<string, long> d, int k = 5) => d.Count == 0 ? "—"
            : string.Join("・", d.OrderByDescending(x => x.Value).Take(k).Select(x => $"{x.Key} {F1((double)x.Value / N)}"));
        public string Share(Dictionary<string, long> d, int k = 5) { long t = d.Values.Sum(); return t == 0 ? "—"
            : string.Join("・", d.OrderByDescending(x => x.Value).Take(k).Select(x => $"{x.Key} {100.0 * x.Value / t:F0}%")); }
    }

    static St Fight(Formation f, S287.Wave w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false);
        var a = new St { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; } else a.LossT = r.Turns;
        var dead = p.Where(u => u.LastDeathTurn > 0).Select(u => u.LastDeathTurn).ToList();
        if (dead.Count > 0) { a.FirstDeathN = 1; a.FirstDeathT = dead.Min(); }
        var doha = p.FirstOrDefault(u => u.Def.Id == "doha");
        if (doha is not null)
        {
            if (doha.LastDeathTurn > 0) { a.DohaDeathN = 1; a.DohaDeathT = doha.LastDeathTurn; }
            if (doha.TakenTurn == 0) a.DohaNoTurn = 1;
            a.DohaAtkEnd = doha.AtkBonus;
        }
        var names = p.GroupBy(u => u.Def.Id).ToDictionary(g => g.Key, g => Short(g.First().Def));
        foreach (var (id, t) in r.TallyByUnit)
        {
            if (!names.TryGetValue(id, out var nm)) continue;   // 敵側の帳簿は数えない
            if (t.PushGot > 0) a.PushTo[nm] = a.PushTo.GetValueOrDefault(nm) + t.PushGot;
            if (t.PushPassGot > 0) a.PassTo[nm] = a.PassTo.GetValueOrDefault(nm) + t.PushPassGot;
            if (t.SinMarkHits > 0) { a.SinHits[nm] = a.SinHits.GetValueOrDefault(nm) + t.SinMarkHits; a.SinDealt[nm] = a.SinDealt.GetValueOrDefault(nm) + t.SinMarkDealt; }
            if (t.SharedAway > 0) a.ShoulderBy[nm] = a.ShoulderBy.GetValueOrDefault(nm) + t.SharedAway;
            a.GiftTurns += t.ShareGiftTurns; a.GiftAttacks += t.ShareGiftAttacks; a.GiftDealt += t.ShareGiftDealt;
            a.WallHits += t.WallOnSharer; a.WallAmt += t.WallOnSharerAmt; a.WallRelay += t.WallOnSharerRelay; a.WallRelayAmt += t.WallOnSharerRelayAmt;
            a.Shoulder += t.SharedAway;
            if (id != "doha") continue;
            a.DohaAttacks += t.Attacks; a.DohaDealt += t.DamageToEnemy; a.DohaWhet += t.Whetted; a.DohaRegurg += t.WhetGotRegurg;
            a.PushTurns += t.PushTurns; a.Pushes += t.Pushes; a.PushZero += t.PushZero; a.PushInGift += t.PushInGift; a.PushCapped += t.PushCapped; a.PushTies += t.PushTies;
            a.PushShoulder += t.PushShoulder; a.PushWindow += t.PushWindow; a.GiftSkipped += t.ShareGiftSkipped; a.WinInTurn += t.PushWinInTurn;
            a.Passes += t.PushPasses; a.Passed += t.PushPassed; a.PassNone += t.PushPassNone; a.PassPeak = Math.Max(a.PassPeak, t.PushPassPeak);
            a.SinTurns += t.SinTurns; a.SinZero += t.SinZero; a.SinTies += t.SinTies; a.SinFresh += t.SinFresh; a.SinLayer += t.SinLayer; a.SinNoop += t.SinNoop;
            a.AllyOrigin += t.ShareAllyOrigin;
        }
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
    // §3 Phase 0（規定のドハ・計数器を立てる）
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        BattleContext.PushCensus = true;
        Console.WriteLine("# 第313期 Phase 0 —— 規定のドハ（DH-a ＋ なまりなし）・計数器 `PushCensus` を立てて回す（盤面は動かない）");
        Console.WriteLine();
        foreach (var (name, f) in new[] { ("試遊・標 守り型", Playtest(Guard)), ("試遊・感電 雷の型", Playtest(Thunder)) })
        {
            Console.WriteLine($"## {name}（{Seats(f)}）× 8 波・seed 0..{Seeds - 1}");
            Console.WriteLine();
            var rows = Waves.Select(w => (w, s: Measure(f, w))).ToArray();
            Console.WriteLine("### §3-1 ゴルムとドハの重なり（1戦あたり）");
            Console.WriteLine();
            Console.WriteLine("ゴルムがドハへの一撃を飲んだ ＝ 巨躯の段がドハへの一撃の 9 割を取った回数 ／ 量。うち **中継** ＝ その一撃がドハの肩代わり（味方の痛みの 4 割）だった分"
                + "（1つの被弾が「ドハが喰う → ゴルムが喰ってドハに返す → ドハも味方に返す」と2回力に変わった分）。");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 | ゴルムがドハへの一撃を飲んだ 回数 ／ 量 | うち中継 回数 ／ 量（量の割合） | ドハが受けた強化 | うち吐き戻し（割合） | 戦の終わりのドハの上乗せ |");
            Console.WriteLine("|---|--:|---|---|--:|--:|--:|");
            foreach (var (w, s) in rows)
                Console.WriteLine($"| {w.Name} | {s.Win} | {s.Per(s.WallHits)} ／ {s.Per(s.WallAmt)} | {s.Per(s.WallRelay)} ／ {s.Per(s.WallRelayAmt)}（{s.Pct(s.WallRelayAmt, s.WallAmt)}%） | "
                    + $"{s.Per(s.DohaWhet)} | {s.Per(s.DohaRegurg)}（{s.Pct(s.DohaRegurg, s.DohaWhet)}%） | {s.Per(s.DohaAtkEnd)} |");
            Console.WriteLine();
            PeakAttack(f);
            Console.WriteLine("### §3-2 ドハの手番（規定・1戦あたり）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 勝率 | 倒しT | ドハの攻撃 | ドハの与ダメ | 1撃あたり | ドハの手番が来る前に戦が終わった（%） | ドハが倒れた（%）・T |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|---|");
            foreach (var (w, s) in rows)
                Console.WriteLine($"| {w.Name} | {s.Win} | {s.WinTurn} | {s.Per(s.DohaAttacks)} | {s.Per(s.DohaDealt)} | {s.Avg(s.DohaDealt, s.DohaAttacks)} | {s.Pct(s.DohaNoTurn, s.N)} | {s.Pct(s.DohaDeathN, s.N)}・{s.Avg(s.DohaDeathT, s.DohaDeathN)} |");
            Console.WriteLine();
            Console.WriteLine("### §3-3 そのターンの肩代わりの分布（背を押すなら選んだはずの相手・手番の頭で数える）");
            Console.WriteLine();
            Console.WriteLine("窓 ＝ ドハの前の手番の終わりから今の手番の頭まで。0 ＝ 窓の肩代わりが 0 の手番（背を押すなら殴る）。同額 ＝ 最多が2体以上で並んだ手番。罪の在り処（DP-c）の窓も同じ手番で数える。");
            Console.WriteLine();
            Console.WriteLine("| 波 | 窓を読んだ手番 /戦 | 肩代わり 0（%） | 同額（%） | 選んだ相手の量 ／ 窓の総量（1回） | 選ばれた相手の割合 | 罪 0（%） | 罪 同額（%） |");
            Console.WriteLine("|---|--:|--:|--:|---|---|--:|--:|");
            foreach (var (w, s) in rows)
                Console.WriteLine($"| {w.Name} | {s.Per(s.PushTurns)} | {s.Pct(s.PushZero, s.PushTurns)} | {s.Pct(s.PushTies, s.Pushes)} | {s.Avg(s.PushShoulder, s.Pushes)} ／ {s.Avg(s.PushWindow, s.Pushes)} | {s.Share(s.PushTo)} | "
                    + $"{s.Pct(s.SinZero, s.SinTurns)} | {s.Pct(s.SinTies, s.SinTurns - s.SinZero)} |");
            Console.WriteLine();
            Console.WriteLine("### §3-4 肩代わりのうち味方由来（同士討ち・巻き込み・羽）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 肩代わりの実額 /戦 | 痛みをくれた相手の割合 | うち味方由来 /戦（割合） | ドハ自身の手番の中の肩代わり /戦（窓に入れない） |");
            Console.WriteLine("|---|--:|---|---|--:|");
            foreach (var (w, s) in rows)
                Console.WriteLine($"| {w.Name} | {s.Per(s.Shoulder)} | {s.Share(s.ShoulderBy)} | {s.Per(s.AllyOrigin)}（{s.Pct(s.AllyOrigin, s.Shoulder)}%） | {s.Per(s.WinInTurn)} |");
            Console.WriteLine();
        }
        BattleContext.PushCensus = false;
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    /// <summary>ドハの攻撃力（ターンの頭の `StatSnapshot`）の T2 の値と戦の最大（seed 0..49・verbose）。</summary>
    static void PeakAttack(Formation f)
    {
        Console.WriteLine("ドハの攻撃力（ターンの頭の写し・seed 0..49）: 波ごとに **T2 の平均 ／ 戦の最大の平均 ／ 全戦の最大**。");
        Console.WriteLine();
        Console.WriteLine("| 波 | T2 | 戦の最大 | 全戦の最大 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var w in Waves)
        {
            var res = new (int T2, int Max)[50];
            Parallel.For(0, 50, sd =>
            {
                var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var r = BattleEngine.Run(p, w.Make(), sd, verbose: true);
                int id = p.First(u => u.Def.Id == "doha").InstanceId, t2 = -1, mx = 0;
                foreach (var x in r.Events)
                {
                    if (x.Kind != BattleEventKind.StatSnapshot || x.TargetId != id) continue;
                    if (x.Turn == 2) t2 = x.Amount;
                    mx = Math.Max(mx, x.Amount);
                }
                res[sd] = (t2, mx);
            });
            var t2s = res.Where(x => x.T2 >= 0).ToList();
            Console.WriteLine($"| {w.Name} | {(t2s.Count == 0 ? "—" : F1(t2s.Average(x => x.T2)))} | {F1(res.Average(x => x.Max))} | {res.Max(x => x.Max)} |");
        }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §4-2 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(int seeds)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# 第313期 §4-2 代表台 × 8 波 × 版（seed 0..{seeds - 1}）");
        Console.WriteLine();
        Console.WriteLine("勝率 ／ 倒しT ／ 負けT ／ 寿命（初めて倒れたT）／ ドハが倒れたT。対照 ＝ ドハ → ドルガ（同じ席・どれかの版で 50% 以上のセルだけ）。差は規定から。");
        var gate = new List<string>();
        foreach (var (bn, bf) in Boards())
        {
            Console.WriteLine();
            Console.WriteLine($"## {bn}（{Seats(bf)}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 規定 | DP-a | DP-b | DP-c | ドルガ | 倒しT（規定 ／ a ／ b ／ c）| 負けT（規定 ／ a ／ b ／ c） | 寿命（規定 ／ a ／ b ／ c） | ドハが倒れたT（規定 ／ a ／ b ／ c） |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|---|---|---|---|");
            var detail = new List<(S287.Wave W, St[] V)>();
            foreach (var w in Waves)
            {
                var v = Versions.Select(x => Measure(Swap(bf, UnitCatalog.Doha, x.Def), w, seeds)).ToArray();
                detail.Add((w, v));
                string dolga = v.Any(x => x.WinRate >= 50) ? Measure(Swap(bf, UnitCatalog.Doha, UnitCatalog.Dolga), w, seeds).Win : "—";
                string Cell(int i) => i == 0 ? v[0].Win : $"{v[i].Win}（{Sg(v[i].WinRate - v[0].WinRate)}）";
                Console.WriteLine($"| {w.Name} | {Cell(0)} | {Cell(1)} | {Cell(2)} | {Cell(3)} | {dolga} | {string.Join(" ／ ", v.Select(x => x.WinTurn))} | {string.Join(" ／ ", v.Select(x => x.LossTurn))} | "
                    + $"{string.Join(" ／ ", v.Select(x => x.Avg(x.FirstDeathT, x.FirstDeathN)))} | {string.Join(" ／ ", v.Select(x => x.Avg(x.DohaDeathT, x.DohaDeathN)))} |");
                bool boss = w.Boss;
                for (int i = 1; i < v.Length; i++)
                    if (v[i].WinRate - v[0].WinRate >= (boss ? 5 : 10)) gate.Add($"{Versions[i].Tag}: {bn} × {w.Name}（{Sg(v[i].WinRate - v[0].WinRate)}）");
            }
            Console.WriteLine();
            Console.WriteLine("背を押した（DP-a ／ DP-b）・上乗せ（DP-b）・罪の在り処（DP-c）（1戦あたり）:");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 押した ／ 窓を読んだ手番 | 0 で殴った ／ ギフト中 ／ 上限 | 送り出した相手 | 送り出された手番の与ダメ（1回） | 渡した上乗せ（1回）／ 受け手の攻撃力の最大 | 吐き戻しの割合 |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|");
            foreach (var (w, v) in detail)
                for (int i = 1; i <= 2; i++)
                {
                    var s = v[i];
                    Console.WriteLine($"| {w.Name} | {Versions[i].Tag} | {s.Per(s.Pushes)} ／ {s.Per(s.PushTurns)} | {s.Per(s.PushZero)} ／ {s.Per(s.PushInGift)} ／ {s.Per(s.PushCapped)} | {s.Share(s.PushTo)} | "
                        + $"{s.Per(s.GiftDealt)}（{s.Avg(s.GiftDealt, s.GiftTurns)}） | {(i == 2 ? $"{s.Per(s.Passed)}（{s.Avg(s.Passed, s.Passes)}）／ {s.PassPeak}" : "—")} | {(i == 2 ? $"{s.Pct(s.DohaRegurg, s.DohaWhet)}%" : "—")} |");
                }
            Console.WriteLine();
            Console.WriteLine("| 波 | DP-c 標（新 ／ 層 ／ 変化なし）| 0 で殴った | その標の敵への一撃（攻め手ごと・1戦あたり） | ゴルムがドハへの中継を飲んだ量（規定 ／ a ／ b ／ c） |");
            Console.WriteLine("|---|---|--:|---|---|");
            foreach (var (w, v) in detail)
            {
                var s = v[3];
                Console.WriteLine($"| {w.Name} | {s.Per(s.SinFresh)} ／ {s.Per(s.SinLayer)} ／ {s.Per(s.SinNoop)} | {s.Per(s.SinZero)} | {s.Top(s.SinHits)} | {string.Join(" ／ ", v.Select(x => x.Per(x.WallRelayAmt)))} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## §4-4 格子の門（規定から +10pt 以上・ボスは +5pt 以上動いた版とセル）");
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
        var grids = Versions.Select(v =>
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
        Console.WriteLine("# 第313期 §4-3 `compare` 64 行 × 版（seed 0..199・基準は規定のドハ）");
        double M(double[,] g, int ri) => Enumerable.Range(1, nw - 1).Average(w => g[ri, w]);
        for (int vi = 1; vi < Versions.Length; vi++)
        {
            var g = grids[vi]; var b = grids[0];
            Console.WriteLine();
            Console.WriteLine($"## {Versions[vi].Tag}");
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
    // §4-4 格子（第297期 `doha297 grid` と同じ作り・基準は規定のドハ）
    // ---------------------------------------------------------------------------------
    internal static readonly UnitDef[] Attackers = { UnitCatalog.Tome, UnitCatalog.Zan, UnitCatalog.Kata, UnitCatalog.Shiga };

    static void Grid(string waveKey, string ver, int seats, int seeds)
    {
        var w = WaveOf(waveKey);
        var dv = Ver(ver);
        var pool = UnitCatalog.All.Where(d => !ReferenceEquals(d, UnitCatalog.Doha)).ToArray();
        int step = Math.Max(1, 120 / Math.Max(1, seats));
        var seatSet = Perms(5).Where((_, i) => i % step == 0).Take(seats).ToArray();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# 第313期 格子 —— {w.Name} × 規定 ／ {VerTag(dv)}（固定枠 ドハ ＋ アタッカー1枚 × 探索枠3・席 {seatSet.Length} 通り・seed 0..{seeds - 1}）");
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
    static void Memo(string boardPart, string wave, int seed, string ver)
    {
        var (name, f0) = Boards().First(b => b.Name.Contains(boardPart, StringComparison.Ordinal));
        var f = Swap(f0, UnitCatalog.Doha, Ver(ver));
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var e = w.Make();
        var r = BattleEngine.Run(p, e, seed, verbose: true);
        var names = p.Concat(e).ToDictionary(u => u.InstanceId, u => Short(u.Def));
        string N(int? id) => id is int i ? (names.TryGetValue(i, out var s) ? $"{s}#{i}" : $"#{i}") : "—";
        Console.WriteLine($"# doha313 memo —— {name} × {VerTag(Ver(ver))} × {w.Name} × seed {seed} → {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns}（`ShareGive` {r.Events.Count(x => x.Kind == BattleEventKind.ShareGive)} 件）");
        Console.WriteLine();
        Console.WriteLine("```");
        int lastT = -1;
        foreach (var x in r.Events)
        {
            bool mark = x.Kind == BattleEventKind.StatusGain && x.Text == StatusKeys.Marked;
            if (x.Kind is not (BattleEventKind.ShareGive or BattleEventKind.Skill or BattleEventKind.Attack or BattleEventKind.Whet or BattleEventKind.MarkLayer or BattleEventKind.Death) && !mark) continue;
            if (x.Turn != lastT) { Console.WriteLine($"--- T{x.Turn}"); lastT = x.Turn; }
            Console.WriteLine($"{x.Kind} {x.Text}  {N(x.ActorId)} → {N(x.TargetId)}  Amount {x.Amount}  Slot {x.Slot}{(x.Reaction ? "  Reaction" : "")}");
        }
        Console.WriteLine("```");
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
    static UnitTally Tal(BattleContext ctx, string id) => ((Dictionary<string, UnitTally>)TallyProp.GetValue(ctx)!).GetValueOrDefault(id) ?? new UnitTally();
    static int QueueCount(BattleContext ctx) => ((System.Collections.ICollection)GiftQueue.GetValue(ctx)!).Count;
    static UnitState? QueueFirstTo(BattleContext ctx)
    {
        foreach (var o in (System.Collections.IEnumerable)GiftQueue.GetValue(ctx)!)
            return (UnitState)o.GetType().GetField("Item2")!.GetValue(o)!;
        return null;
    }

    /// <summary>盤面を直に組む（`Run` を通さない・乱数は seed 0）。敵は2体（ドルガ ＝ 前1 ／ 前3）。</summary>
    static BattleContext Ctx(Formation pl, out List<UnitState> p, out List<UnitState> e)
    {
        var ctx = new BattleContext(0, true);
        p = BattleEngine.Materialize(pl, BattleContext.PlayerTeam);
        e = BattleEngine.Materialize(Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Dolga), BattleContext.EnemyTeam, EnemyScaleRule.None);
        foreach (var u in p) AddUnit.Invoke(ctx, new object[] { u });
        foreach (var u in e) AddUnit.Invoke(ctx, new object[] { u });
        TurnProp.SetValue(ctx, 1);
        return ctx;
    }

    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# doha313 check —— 第313期の自己検査");
        Console.WriteLine();
        Console.WriteLine("| 項目 | 結果 | 備考 |");
        Console.WriteLine("|---|---|---|");

        // (a) DP-a: 最も肩代わりした味方に1回だけ・ドハ自身は選ばない
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Dolga, center: UnitCatalog.DohaDPa, back1: UnitCatalog.Dolga), out var p, out var e);
            var doha = p.First(u => u.Def.Id == "doha"); var dolga = p.First(u => u.Slot == 0); var mio = p.First(u => u.Slot == 3);
            ctx.ApplyDamage(dolga, 20, e[0]);
            ctx.ApplyDamage(mio, 30, e[0]);
            ctx.ApplyDamage(doha, 50, e[1]);   // ドハ自身への直接の一撃（窓に入らない）
            bool ok1 = ctx.SharePushTurn(doha);
            var to = QueueFirstTo(ctx);
            Expect("(a) DP-a: そのターンに最も肩代わりした味方（後1 のドルガ 12 ＞ 前1 のドルガ 8）に手番が控えられる", ok1 && to == mio && QueueCount(ctx) == 1, $"控え {QueueCount(ctx)} 件・相手の席 {to?.Slot}");
            bool ok2 = ctx.SharePushTurn(doha);
            Expect("(a) 同じターンにもう一度は押さない（上限）", !ok2 && Tal(ctx, "doha").PushCapped == 1 && QueueCount(ctx) == 1, $"PushCapped {Tal(ctx, "doha").PushCapped}");
            TurnProp.SetValue(ctx, 2);
            ctx.ApplyDamage(doha, 30, e[1]);
            bool ok3 = ctx.SharePushTurn(doha);
            Expect("(a) 窓は閉じている・ドハ自身への一撃だけなら肩代わり 0 で殴る（偽を返す）", !ok3 && Tal(ctx, "doha").PushZero == 1, $"PushZero {Tal(ctx, "doha").PushZero}");
        }
        // (b) 同じ額なら攻撃力 → 席
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Dolga, center: UnitCatalog.DohaDPa), out var p, out var e);
            var doha = p.First(u => u.Def.Id == "doha");
            var d0 = p.First(u => u.Slot == 0); var d1 = p.First(u => u.Slot == 1);
            ctx.ApplyDamage(d0, 20, e[0]); ctx.ApplyDamage(d1, 20, e[0]);
            ctx.SharePushTurn(doha);
            Expect("(b) 同じ額・同じ攻撃力なら席の若い方", QueueFirstTo(ctx) == d0 && Tal(ctx, "doha").PushTies == 1, $"相手の席 {QueueFirstTo(ctx)?.Slot}");
            var ctx2 = Ctx(Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Dolga, center: UnitCatalog.DohaDPa), out var p2, out var e2);
            var doha2 = p2.First(u => u.Def.Id == "doha");
            var x0 = p2.First(u => u.Slot == 0); var x1 = p2.First(u => u.Slot == 1);
            ctx2.Whet(x1, 5);
            ctx2.ApplyDamage(x0, 20, e2[0]); ctx2.ApplyDamage(x1, 20, e2[0]);
            ctx2.SharePushTurn(doha2);
            Expect("(b) 同じ額なら攻撃力の高い方（席の遅い方でも）", QueueFirstTo(ctx2) == x1, $"相手の席 {QueueFirstTo(ctx2)?.Slot}");
        }
        // (c) DP-b: 上乗せがすべて移り、ドハは素の攻撃力に戻る
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Dolga, center: UnitCatalog.DohaDPb), out var p, out var e);
            var doha = p.First(u => u.Def.Id == "doha"); var dolga = p.First(u => u.Def.Id == "dolga");
            ctx.Whet(doha, 30);
            ctx.ApplyDamage(dolga, 20, e[0]);   // 痛み ÷ 2 の返し（DH-a）は押す前に入る
            int atk0 = dolga.CurrentAttack;
            bool ok = ctx.SharePushTurn(doha);
            Expect("(c) DP-b: ドハの上乗せ 30 がすべて相手に移り、ドハは素の攻撃力に戻る", ok && doha.AtkBonus == 0 && doha.CurrentAttack == doha.Def.Attack && dolga.CurrentAttack == atk0 + 30 && Tal(ctx, "doha").PushPassed == 30,
                $"ドハ {doha.CurrentAttack}（素 {doha.Def.Attack}）・ドルガ {atk0} → {dolga.CurrentAttack}");
            var ctx2 = Ctx(Formation.Build(front1: UnitCatalog.Dolga, center: UnitCatalog.DohaDPa), out var p2, out var e2);
            var doha2 = p2.First(u => u.Def.Id == "doha"); var dolga2 = p2.First(u => u.Def.Id == "dolga");
            ctx2.Whet(doha2, 30);
            ctx2.ApplyDamage(dolga2, 20, e2[0]);
            ctx2.SharePushTurn(doha2);
            Expect("(c) DP-a は上乗せを渡さない（ドハの上乗せはそのまま）", doha2.AtkBonus == 30, $"ドハの上乗せ {doha2.AtkBonus}");
        }
        // (d) DP-c: 味方に最も多く与えた敵に標が1層
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Dolga, front3: UnitCatalog.Gald, center: UnitCatalog.DohaDPc), out var p, out var e);
            var doha = p.First(u => u.Def.Id == "doha"); var dolga = p.First(u => u.Def.Id == "dolga"); var gald = p.First(u => u.Def.Id == "gald");
            ctx.ApplyDamage(dolga, 10, e[0]);
            ctx.ApplyDamage(gald, 25, e[1]);
            bool ok = ctx.ShareSinTurn(doha);
            Expect("(d) DP-c: 味方に最も多く与えた敵（敵の前3）に標が1層・もう一方には付かない", ok && e[1].RawCounter(StatusKeys.Marked) == 1 && e[0].RawCounter(StatusKeys.Marked) == 0,
                $"前1 {e[0].RawCounter(StatusKeys.Marked)}・前3 {e[1].RawCounter(StatusKeys.Marked)}");
            TurnProp.SetValue(ctx, 2);
            bool ok2 = ctx.ShareSinTurn(doha);
            Expect("(d) 窓は閉じている（次の手番で 0 なら殴る）", !ok2 && Tal(ctx, "doha").SinZero == 1, $"SinZero {Tal(ctx, "doha").SinZero}");
        }
        // (e) 送り出された手番の中では押さない（連鎖しない）
        {
            var ctx = Ctx(Formation.Build(front1: UnitCatalog.Dolga, center: UnitCatalog.DohaDPa), out var p, out var e);
            var doha = p.First(u => u.Def.Id == "doha"); var dolga = p.First(u => u.Def.Id == "dolga");
            ctx.ApplyDamage(dolga, 20, e[0]);
            InGift.SetValue(ctx, true);
            bool ok = ctx.SharePushTurn(doha);
            InGift.SetValue(ctx, false);
            Expect("(e) ギフトの手番の中では背を押さない（殴る）・窓は閉じない", !ok && QueueCount(ctx) == 0 && Tal(ctx, "doha").PushInGift == 1 && ctx.SharePushTurn(doha),
                $"PushInGift {Tal(ctx, "doha").PushInGift}");
        }
        // (f) 全戦の帳尻: 送り出した手番 ＋ 飛ばした手番 ＝ 押した回数（連鎖していない）
        {
            long pushes = 0, turns = 0, skipped = 0, ingift = 0;
            foreach (var w in Waves)
                for (int sd = 0; sd < 40; sd++)
                {
                    var p = BattleEngine.Materialize(Swap(Playtest(Guard), UnitCatalog.Doha, UnitCatalog.DohaDPa), BattleContext.PlayerTeam);
                    var r = BattleEngine.Run(p, w.Make(), sd, verbose: false);
                    foreach (var (id, t) in r.TallyByUnit) { pushes += t.Pushes; turns += t.ShareGiftTurns; skipped += t.ShareGiftSkipped; ingift += t.PushInGift; }
                }
            Expect("(f) 守り型 × 8 波 × seed 0..39: 送り出された手番 ＋ 飛ばした ＝ 押した（連鎖なし）・押した回数 > 0", pushes > 0 && turns + skipped == pushes, $"押した {pushes}・手番 {turns}・飛ばした {skipped}・ギフト中 {ingift}");
        }
        // (g) verbose の有無で勝敗が同じ（版すべて × 守り型 ／ 雷の型 × 8 波 × seed 0..19）
        {
            int diff = 0, n = 0;
            foreach (var b in new[] { Playtest(Guard), Playtest(Thunder) })
                foreach (var v in Versions)
                    foreach (var w in Waves)
                        for (int sd = 0; sd < 20; sd++)
                        {
                            var f = Swap(b, UnitCatalog.Doha, v.Def);
                            var r0 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                            var r1 = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: true);
                            n++;
                            if (r0.PlayerWon != r1.PlayerWon || r0.Turns != r1.Turns) diff++;
                        }
            Expect("(g) verbose の有無で勝敗 ／ 決着T が同じ", diff == 0, $"{n} 戦中 {diff} 戦が違う");
        }
        // (h) 計数器（PushCensus）を立てても規定のドハの戦は1ビットも動かない
        {
            int diff = 0, n = 0;
            var rows = CompareBuilds().Where(r => r.F.Occupied().Any(o => ReferenceEquals(o.Def, UnitCatalog.Doha))).Select(r => r.F).Append(Playtest(Guard)).Append(Playtest(Thunder)).ToArray();
            var off = new List<(bool, int)>();
            foreach (var f in rows) foreach (var w in Waves) for (int sd = 0; sd < 20; sd++) { var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false); off.Add((r.PlayerWon, r.Turns)); }
            BattleContext.PushCensus = true;
            int k = 0; long census = 0;
            foreach (var f in rows) foreach (var w in Waves) for (int sd = 0; sd < 20; sd++)
            {
                var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), w.Make(), sd, verbose: false);
                n++;
                if ((r.PlayerWon, r.Turns) != off[k++]) diff++;
                census += r.TallyByUnit.GetValueOrDefault("doha")?.PushTurns ?? 0;
            }
            BattleContext.PushCensus = false;
            Expect("(h) 計数器を立てても勝敗 ／ 決着T が同じ（ドハの行 ＋ 試遊2台 × 8 波 × seed 0..19）・計数器は数えている", diff == 0 && census > 0, $"{n} 戦中 {diff} 戦が違う・窓を読んだ手番 {census}");
        }
        // (i) 新しい窓口で ctx.PickOne を使っていない
        {
            string src = File.ReadAllText("BattleCore/BattleEngine.cs");
            int a = src.IndexOf("// ---- 第313期 —— 背を押す", StringComparison.Ordinal);
            int b = a < 0 ? -1 : src.IndexOf("void NoteWallOnSharer", a, StringComparison.Ordinal);
            string region = a < 0 || b < 0 ? "" : src[a..b];
            string tr = File.ReadAllText("BattleCore/Traits.cs");
            int c = tr.IndexOf("public sealed class SharePushTrait", StringComparison.Ordinal);
            string trait = c < 0 ? "" : tr[c..tr.IndexOf("public sealed class SharerTrait", c, StringComparison.Ordinal)];
            Expect("(i) 第313期の窓口（engine の区画・`SharePushTrait`）に `PickOne` も `Roll` も無い", region.Length > 0 && trait.Length > 0 && !region.Contains("PickOne") && !region.Contains("Roll(") && !trait.Contains("PickOne"),
                $"区画 {region.Length} 字・札 {trait.Length} 字");
        }
        // (j) 版の定義: 規定の上に札を足しただけ・All に入っていない
        {
            bool same(UnitDef d) => d.MaxHp == UnitCatalog.Doha.MaxHp && d.Attack == UnitCatalog.Doha.Attack && d.Speed == UnitCatalog.Doha.Speed && UnitCatalog.Doha.Traits.All(d.Traits.Contains);
            Expect("(j) DP-a ／ DP-b ／ DP-c は規定のドハの数値と札をそのまま持ち、手番は術・`All` に入っていない",
                new[] { UnitCatalog.DohaDPa, UnitCatalog.DohaDPb, UnitCatalog.DohaDPc }.All(d => same(d) && d.Actions is { Count: 1 } && d.Actions[0].Kind == ActionKind.Skill && !UnitCatalog.All.Contains(d)),
                "");
        }
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて ○**" : $"**× が {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
