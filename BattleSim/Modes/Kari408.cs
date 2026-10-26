using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using S307 = Som307Diag;

// =====================================================================================
// kari408 —— 第408期【軽い期・版だけ】カリの転生（デバフ軸へ）: K-k 刻む ／ K-d 駆り立てる。
// 指示書は design/PHASE408_KARI_VERSIONS_SPEC.md ／ 報告は design/PHASE408_KARI_VERSIONS.md。段0 は `kari407 cum`。
//
//     dotnet run --project BattleSim -c Release 0 kari408 seats    # §3-2: 「試遊・呪い」の1席をカリ（K-k ／ K-d ／ 素体）に替えた 5 並び × 4 波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 kari408 mixed    # §3-3: compare のカリの2行のカリを版に差し替えた写し × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..199
//     dotnet run --project BattleSim -c Release 0 kari408 log <版 kk|kd> <外す駒> <波> [seed]   # 1戦のログ（外す駒 wata ／ mudo ／ nel ／ kubi ／ utsu・波 boss ／ guard ／ bat ／ s4）
//     dotnet run --project BattleSim -c Release 0 kari408 check    # 自己検査
//
// **予測のファイルが無ければ seats ／ mixed を走らせない**（R397・`.tmp/p408/predict.md`）。
// =====================================================================================
static class Kari408Diag
{
    internal const string PredictFile = ".tmp/p408/predict.md";
    const int Seeds = 100, TT = 10, CompareSeeds = 200;
    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);

    static Formation Board => Presets.Versions.First(r => r.Name == D.PlayRow).F;
    internal static readonly I.W[] Waves = { I.Boss, I.Guard, I.Bat, I.StrongWaves[2] };
    static readonly string[] SeatIds = { "wata", "mudo", "nel", "kubi", "utsu" };

    internal static UnitDef PlainOf(UnitDef d) => new()
    {
        Id = $"kari_plain{d.Attack}", Name = $"素体のカリ（攻 {d.Attack}）", MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed,
        Advances = d.Advances, Traits = Array.Empty<TraitId>(), Pattern = d.Pattern
    };
    internal static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("kk", "K-k", UnitCatalog.KariKk), ("kd", "K-d", UnitCatalog.KariKd),
        ("p6", "素体 攻6", PlainOf(UnitCatalog.KariKk)), ("p4", "素体 攻4", PlainOf(UnitCatalog.KariKd)),
    };

    /// <summary>「試遊・呪い」の、<paramref name="outId"/> の席をカリ（<paramref name="d"/>）に替えた並び。</summary>
    internal static Formation Seat(string outId, UnitDef d)
    {
        var g = new Formation();
        foreach (var o in Board.Occupied()) g[o.Slot] = o.Def.Id == outId ? d : o.Def;
        return g;
    }

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "seats" or "mixed" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"kari408: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        switch (mode)
        {
            case "seats": Seats(); return;
            case "mixed": Mixed(); return;
            case "log": Log(A(3, "kk"), A(4, "kubi"), A(5, "s4"), int.Parse(A(6, "0"))); return;
            case "check": Check(); return;
            default: Console.WriteLine("kari408: モードは seats / mixed / log / check。"); return;
        }
    }

    static I.W WaveOf(string k) => k switch { "boss" => I.Boss, "guard" => I.Guard, "bat" => I.Bat, _ => I.StrongWaves[2] };
    static void Log(string ver, string outId, string wave, int seed)
    {
        var v = Vers.First(x => x.Key == ver);
        var f = Seat(outId, v.D); var w = WaveOf(wave);
        var (r, _, _) = I.Raw(f, w, seed, verbose: true);
        Console.WriteLine($"# {S307.Seats(f)}（{v.Label}・−{outId}）× {w.Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数（verbose・台本と帳簿を読む・盤面は動かさない）
    // ---------------------------------------------------------------------------------
    internal sealed class Fx
    {
        public long N, Wins, WinT, Fell, Turns;
        public long TailN, TailTurns, TailDirect, TailStart;          // 尻尾（生きた敵が1体になった次のターンから・ボスは開戦から）
        public long Carves, CarveHits, CumRead, CarveDealt, Plain, FedFromKari, PickedKari, Drags, DragAimed, Deepened, Redrags, Marks;
        public long KariTaken, KariDied, KariDeathT, KariDealt;
        public long BackN, BackDied, BackDeathT, WataEarly, WataN, MudoEarly, MudoN;
        public readonly long[] TKariD = new long[TT + 1], TKariN = new long[TT + 1];
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Turns += o.Turns;
            TailN += o.TailN; TailTurns += o.TailTurns; TailDirect += o.TailDirect; TailStart += o.TailStart;
            Carves += o.Carves; CarveHits += o.CarveHits; CumRead += o.CumRead; CarveDealt += o.CarveDealt; Plain += o.Plain; FedFromKari += o.FedFromKari; PickedKari += o.PickedKari;
            Drags += o.Drags; DragAimed += o.DragAimed; Deepened += o.Deepened; Redrags += o.Redrags; Marks += o.Marks;
            KariTaken += o.KariTaken; KariDied += o.KariDied; KariDeathT += o.KariDeathT; KariDealt += o.KariDealt;
            BackN += o.BackN; BackDied += o.BackDied; BackDeathT += o.BackDeathT; WataEarly += o.WataEarly; WataN += o.WataN; MudoEarly += o.MudoEarly; MudoN += o.MudoN;
            for (int t = 0; t <= TT; t++) { TKariD[t] += o.TKariD[t]; TKariN[t] += o.TKariN[t]; }
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
        public string WT => $"{F1(Win)}・{WinTs}T";
    }

    internal static Fx One(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Fx { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        a.Fell = p.Count(u => u.LastDeathTurn > 0);
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        foreach (var x in r.Events) if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid) (x.Team == BattleContext.PlayerTeam ? mine : foes).Add(sid);
        var back = e.Where(u => FormationRules.RowOf(u.Slot) == Row.Back).Select(u => u.InstanceId).ToHashSet();
        a.BackN = back.Count;
        var kari = p.FirstOrDefault(u => u.Def.Id.StartsWith("kari"));
        var dead = new HashSet<int>();
        int tailStart = foes.Count == 1 ? 0 : -1;
        foreach (var x in r.Events)
        {
            int t = x.Turn;
            if (x.Kind == BattleEventKind.Death && x.TargetId is int di && foes.Contains(di))
            {
                dead.Add(di);
                if (back.Contains(di)) { a.BackDied++; a.BackDeathT += t; }
                if (tailStart < 0 && foes.Count(id => !dead.Contains(id)) == 1) tailStart = t;
            }
            if (x.Kind == BattleEventKind.Revive && x.TargetId is int ri) dead.Remove(ri);
            if (x.Kind == BattleEventKind.Damage && x.Amount > 0 && x.TargetId is int ti && foes.Contains(ti) && x.ActorId is int ai && mine.Contains(ai)
                && x.ShareFromId is null && tailStart >= 0 && t > tailStart) a.TailDirect += x.Amount;
            if (kari is not null && x.Kind == BattleEventKind.StatSnapshot && x.TargetId == kari.InstanceId && t >= 1 && t <= TT)
            { a.TKariD[t] += Math.Max(0, kari.Def.Attack - x.Amount); a.TKariN[t]++; }
        }
        if (tailStart >= 0 && r.Turns > tailStart) { a.TailN = 1; a.TailTurns = r.Turns - tailStart; a.TailStart = tailStart; }
        else a.TailDirect = 0;
        if (kari is not null && r.TallyByUnit.TryGetValue(kari.Def.Id, out var tk))
        {
            a.Carves = tk.K408Carves; a.CarveHits = tk.K408CarveHits; a.CumRead = tk.K408CumRead; a.CarveDealt = tk.K408CarveDealt; a.Plain = tk.K408Plain;
            a.Drags = tk.K408Drags; a.DragAimed = tk.K408DragAimed; a.Deepened = tk.K408Deepened; a.Redrags = tk.K409Redrags; a.Marks = tk.K409Marks;
            a.KariTaken = tk.DamageTaken; a.KariDealt = tk.DamageToEnemy;
            if (kari.LastDeathTurn > 0) { a.KariDied = 1; a.KariDeathT = kari.LastDeathTurn; }
        }
        if (r.TallyByUnit.TryGetValue("wata", out var tw)) { a.FedFromKari = tw.K408FedFromKari; a.PickedKari = tw.K408PickedKari; }
        var wata = p.FirstOrDefault(u => u.Def.Id == "wata"); var mudo = p.FirstOrDefault(u => u.Def.Id == "mudo");
        if (wata is not null) { a.WataN = 1; if (wata.LastDeathTurn is > 0 and <= 2) a.WataEarly = 1; }
        if (mudo is not null) { a.MudoN = 1; if (mudo.LastDeathTurn is > 0 and <= 3) a.MudoEarly = 1; }
        return a;
    }

    internal static Fx Measure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, s));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    internal static string Tail(Fx a) => a.TailTurns == 0 ? "—" : $"{Per(a.TailDirect, a.TailTurns)} ／ {Per(a.TailTurns, a.TailN)}T";

    // ---------------------------------------------------------------------------------
    // §3-2 5 並び
    // ---------------------------------------------------------------------------------
    static void Seats()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# 第408期 §3-2 —— 「{D.PlayRow}」（{S307.Seats(Board)}）の1席をカリに替えた 5 並び × {string.Join(" ／ ", Waves.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine($"K-k: 攻 {UnitCatalog.KariKk.Attack}・累計 ÷ {KariCarveTrait.Per} 発（上限 {KariCarveTrait.MaxHits}）・刻むたび −{KariCarveTrait.SelfDull}。K-d: 攻 {UnitCatalog.KariKd.Attack}・引きずり出された敵は殴るたび −{KariDragTrait.Deepen}・単体の的はカリ。素体 ＝ 同じ HP ／ 攻 ／ 速・札なし。");
        Console.WriteLine("尻尾 ＝ 生きた敵が1体になった次のターンから決着まで（ボスは開戦から）。尻尾の直 ／T ＝ 味方 → 敵の直の与ダメ（共有を除く）÷ 尻尾のターン数。");
        Console.WriteLine();
        var baseRes = Waves.Select(w => Measure(Board, w)).ToArray();
        var res = new Dictionary<(string, string), Fx[]>();
        foreach (var o in SeatIds) foreach (var v in Vers) res[(o, v.Key)] = Waves.Select(w => Measure(Seat(o, v.D), w)).ToArray();

        Console.WriteLine("## 1. 勝率・倒しT ／ 倒れた味方（／戦）／ 尻尾の直 ／T・尻尾の長さ");
        Console.WriteLine();
        foreach (var (wi, w) in Waves.Select((w, i) => (i, w)))
        {
            Console.WriteLine($"### {w.Name}（「{D.PlayRow}」そのもの: {baseRes[wi].WT}・倒 {Per2(baseRes[wi].Fell, baseRes[wi].N)}・尻尾 {Tail(baseRes[wi])}）");
            Console.WriteLine();
            Console.WriteLine("| 外した駒 | " + string.Join(" | ", Vers.Select(v => v.Label)) + " | K-k − 素体6 | K-d − 素体4 |");
            Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "---|")) + "--:|--:|");
            foreach (var o in SeatIds)
            {
                var cells = Vers.Select(v => { var a = res[(o, v.Key)][wi]; return $"{a.WT}・倒 {Per2(a.Fell, a.N)}・尻尾 {Tail(a)}"; });
                double dk = res[(o, "kk")][wi].Win - res[(o, "p6")][wi].Win, dd = res[(o, "kd")][wi].Win - res[(o, "p4")][wi].Win;
                Console.WriteLine($"| −{Short(UnitCatalog.Everyone.First(d => d.Id == o))} | " + string.Join(" | ", cells) + $" | {dk:+0.0;-0.0;0.0} | {dd:+0.0;-0.0;0.0} |");
            }
            Console.WriteLine();
        }

        Console.WriteLine("## 2. K-k の中身（戦あたり）");
        Console.WriteLine();
        Console.WriteLine("刻んだ ＝ 刻んだ手番（`K408Carves`）／ 発 ／ 1手番の発 ／ 読んだ累計（1手番）／ 刻みの与ダメ・ふつう ＝ 呪われた敵が届かずふつうに殴った手番・ワタ: カリから拾った ／ そのうちウツへ回した。");
        Console.WriteLine();
        Console.WriteLine("| 外した駒 | 波 | 刻んだ ／ 発 | 1手番の発 | 読んだ累計 | 刻みの与ダメ | ふつう | カリの与ダメ（全体） | ワタ: カリから ／ ウツへ | カリ: 受けた ／ 倒れた・T |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|---|---|");
        foreach (var o in SeatIds)
            foreach (var (wi, w) in Waves.Select((w, i) => (i, w)))
            {
                var a = res[(o, "kk")][wi];
                Console.WriteLine($"| −{o} | {w.Name} | {Per(a.Carves, a.N)} ／ {Per(a.CarveHits, a.N)} | {Per2(a.CarveHits, a.Carves)} | {Per(a.CumRead, a.Carves)} | {Per(a.CarveDealt, a.N)} | {Per(a.Plain, a.N)} | {Per(a.KariDealt, a.N)} | {Per(a.PickedKari, a.N)} ／ {Per(a.FedFromKari, a.N)} | {Per(a.KariTaken, a.N)} ／ {Pct(a.KariDied, a.N)}%・{Per(a.KariDeathT, a.KariDied)} |");
            }
        Console.WriteLine();
        Console.WriteLine("K-k の自分の下げ幅（ターンの頭・カリが生きていた戦の平均・強4）:");
        Console.WriteLine();
        Console.WriteLine("| 外した駒 | " + string.Join(" | ", Enumerable.Range(1, TT).Select(t => $"T{t}")) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Range(1, TT).Select(_ => "--:|")));
        foreach (var o in SeatIds)
        {
            var a = res[(o, "kk")][3];
            Console.WriteLine($"| −{o} | " + string.Join(" | ", Enumerable.Range(1, TT).Select(t => a.TKariN[t] == 0 ? "—" : Per(a.TKariD[t], a.TKariN[t]))) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 3. K-d の中身（戦あたり）");
        Console.WriteLine();
        Console.WriteLine("後列の敵の倒れたT ＝ 開戦で後列にいた敵（強4 は詠唱兵 ／ 従軍司祭）の倒れた割合・平均T（「試遊・呪い」そのものは第407期の 5.1T）。ワタ ／ ムドの序盤の死 ＝ ワタが T2 まで ／ ムドが T3 までに倒れた割合。");
        Console.WriteLine();
        Console.WriteLine("| 外した駒 | 波 | 引きずり出した | ふつう | 的がカリに | 深めた呪い | カリ: 受けた ／ 倒れた・T | 後列の敵: 倒れた・T | ワタ T2 まで ／ ムド T3 まで | 素体4 の後列 ／ ワタ ／ ムド |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|---|---|---|---|");
        foreach (var o in SeatIds)
            foreach (var (wi, w) in Waves.Select((w, i) => (i, w)))
            {
                var a = res[(o, "kd")][wi]; var z = res[(o, "p4")][wi];
                Console.WriteLine($"| −{o} | {w.Name} | {Per2(a.Drags, a.N)} | {Per(a.Plain, a.N)} | {Per(a.DragAimed, a.N)} | {Per(a.Deepened, a.N)} | {Per(a.KariTaken, a.N)} ／ {Pct(a.KariDied, a.N)}%・{Per(a.KariDeathT, a.KariDied)} | {Pct(a.BackDied, a.BackN)}%・{Per(a.BackDeathT, a.BackDied)} | {Pct(a.WataEarly, a.WataN)}% ／ {Pct(a.MudoEarly, a.MudoN)}% | {Per(z.BackDeathT, z.BackDied)}T ／ {Pct(z.WataEarly, z.WataN)}% ／ {Pct(z.MudoEarly, z.MudoN)}% |");
            }
        Console.WriteLine();
        var b4 = baseRes[3];
        Console.WriteLine($"「{D.PlayRow}」そのもの（強4）: 後列の敵 {Pct(b4.BackDied, b4.BackN)}%・{Per(b4.BackDeathT, b4.BackDied)}T ／ ワタ T2 まで {Pct(b4.WataEarly, b4.WataN)}% ／ ムド T3 まで {Pct(b4.MudoEarly, b4.MudoN)}%。");
        Console.WriteLine();

        Console.WriteLine("## 4. 最良の席（試遊の台の候補・強4 の勝率 → ボスの倒しT → 近衛 ＋ 大隊）");
        Console.WriteLine();
        foreach (var v in Vers.Take(2))
        {
            var best = SeatIds.OrderByDescending(o => res[(o, v.Key)][3].Win)
                .ThenBy(o => res[(o, v.Key)][0].Wins == 0 ? 99 : (double)res[(o, v.Key)][0].WinT / res[(o, v.Key)][0].Wins)
                .ThenByDescending(o => res[(o, v.Key)][1].Win + res[(o, v.Key)][2].Win).First();
            Console.WriteLine($"- {v.Label}: −{best}（{S307.Seats(Seat(best, v.D))}）: " + string.Join(" ／ ", Waves.Select((w, i) => $"{w.Name} {res[(best, v.Key)][i].WT}")));
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §3-3 寄せ集め（compare のカリの2行・写しで走らせる）
    // ---------------------------------------------------------------------------------
    static Formation SwapKari(Formation f, UnitDef d)
    {
        var g = new Formation();
        foreach (var o in f.Occupied()) g[o.Slot] = o.Def.Id == "kari" ? d : o.Def;
        return g;
    }
    static void Mixed()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = Presets.Compare.Where(b => b.F.Occupied().Any(o => o.Def.Id == "kari")).ToArray();
        var waves = EnemyCatalog.Stages.Select((st, i) => ($"第{i + 1}波", (Func<Formation, int, BattleResult>)((f, s) => BattleEngine.Run(f, st.Enemy, s, verbose: false))))
            .Concat(new[] { ("近衛", EnemyCatalog.EliteGuardWave), ("大隊", EnemyCatalog.EliteBattalionWave) }
                .Select(x => (x.Item1, (Func<Formation, int, BattleResult>)((f, s) => BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(x.Item2, EnemyCatalog.EliteScale), s, verbose: false)))))
            .ToArray();
        var vs = new (string Label, UnitDef? D)[] { ("規定（旧のカリ）", null), ("K-k", UnitCatalog.KariKk), ("K-d", UnitCatalog.KariKd), ("素体 攻4", PlainOf(UnitCatalog.KariKd)), ("素体 攻6", PlainOf(UnitCatalog.KariKk)) };
        Console.WriteLine($"# 第408期 §3-3 寄せ集め —— `compare` のカリを含む {rows.Length} 行のカリを版に差し替えた写し × 本編 5 波（`compare` と同じ）＋ 近衛 ／ 大隊（`elite` と同じ）・seed 0..{CompareSeeds - 1}");
        Console.WriteLine();
        Console.WriteLine("規定の行は触らない（写しで走らせる）。素体 攻4 は第407期 §Q3 の素体と同じ。");
        Console.WriteLine();
        foreach (var (name, f) in rows)
        {
            Console.WriteLine($"**{name}**（{S307.Seats(f)}）");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", waves.Select(w => w.Item1)) + " | 平均 |");
            Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "--:|")) + "--:|");
            foreach (var (label, d) in vs)
            {
                var g = d is null ? f : SwapKari(f, d);
                var wins = waves.Select(w =>
                {
                    int n = 0; var o = new int[CompareSeeds];
                    Parallel.For(0, CompareSeeds, s => o[s] = w.Item2(g, s).PlayerWon ? 1 : 0);
                    n = o.Sum();
                    return 100.0 * n / CompareSeeds;
                }).ToArray();
                Console.WriteLine($"| {label} | " + string.Join(" | ", wins.Select(F1)) + $" | {F1(wins.Average())} |");
            }
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static int _fail;
    static void Expect(string what, bool ok, string detail = "")
    {
        Console.WriteLine($"- [{(ok ? "OK" : "NG")}] {what}{(detail == "" ? "" : $"（{detail}）")}");
        if (!ok) _fail++;
    }
    static void Check()
    {
        _fail = 0;
        Console.WriteLine("# 第408期 kari408 check —— 自己検査");
        Console.WriteLine();
        var kk = UnitCatalog.KariKk; var kd = UnitCatalog.KariKd; var k0 = UnitCatalog.Kari;
        Expect("版の定義: Id ＝ kari・HP ／ 速は旧のまま・攻 6 ／ 4・札は KariCarve ／ KariDrag だけ・手番は Skill",
            kk.Id == k0.Id && kd.Id == k0.Id && kk.MaxHp == k0.MaxHp && kd.MaxHp == k0.MaxHp && kk.Speed == k0.Speed && kd.Speed == k0.Speed
            && kk.Attack == 6 && kd.Attack == 4 && kk.Traits.SequenceEqual(new[] { TraitId.KariCarve }) && kd.Traits.SequenceEqual(new[] { TraitId.KariDrag })
            && kk.Actions.Count == 1 && kk.Actions[0].Kind == ActionKind.Skill && kd.Actions.Count == 1 && kd.Actions[0].Kind == ActionKind.Skill);
        var held = UnitCatalog.Everyone.Where(d => d.Traits.Any(t => t is TraitId.KariCarve or TraitId.KariDrag)).ToArray();
        Expect("保持者 0 枚（`All` ／ `Retired` ／ `Presets.Compare` に版が無い・規定のカリは Goad のまま）",
            held.Length == 0 && !UnitCatalog.All.Contains(kk) && !UnitCatalog.All.Contains(kd) && k0.Traits.SequenceEqual(new[] { TraitId.Goad })
            && !Presets.Compare.Any(b => b.F.Occupied().Any(o => o.Def.Traits.Any(t => t is TraitId.KariCarve or TraitId.KariDrag))),
            $"保持者 {held.Length}");
        // 刻み: 発の数 ＝ 累計 ÷ 4（1〜8）
        long carves = 0, hits = 0, bad = 0, picked = 0, fed = 0, drags = 0, aimed = 0, deep = 0;
        var f = Seat("kubi", kk); var g = Seat("kubi", kd);
        for (int s = 0; s < 20; s++)
        {
            foreach (var w in Waves)
            {
                var (r, _, _) = I.Raw(f, w, s, verbose: true);
                var t = r.TallyByUnit["kari"]; carves += t.K408Carves; hits += t.K408CarveHits;
                if (t.K408CarveHits > t.K408Carves * KariCarveTrait.MaxHits || t.K408CarveHits < t.K408Carves) bad++;
                foreach (var l in r.Log.Select(x => x.ToString()).Where(x => x.Contains("度刻む")))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(l, @"累計 (\d+)）を (\d+) 度");
                    if (m.Success && int.Parse(m.Groups[2].Value) != Math.Clamp(int.Parse(m.Groups[1].Value) / KariCarveTrait.Per, 1, KariCarveTrait.MaxHits)) bad++;
                }
                picked += r.TallyByUnit.TryGetValue("wata", out var tw) ? tw.K408PickedKari : 0; fed += tw?.K408FedFromKari ?? 0;
                var (r2, _, _) = I.Raw(g, w, s, verbose: false);
                var t2 = r2.TallyByUnit["kari"]; drags += t2.K408Drags; aimed += t2.K408DragAimed; deep += t2.K408Deepened;
            }
        }
        Expect("K-k: 刻んだ手番の発 ＝ 累計 ÷ 4（切り捨て・1〜8）・台の上で刻む（−クビ × 4 波 × seed 0..19）", carves > 0 && bad == 0, $"刻んだ {carves}・発 {hits}・食い違い {bad}");
        Expect("K-k: 刃こぼれをワタが拾った量（記録だけ・門にしない）", true, $"拾った {picked}・ウツへ {fed}");
        Expect("K-d: 台の上で引きずり出し、深めた呪い ＝ 4 の倍数", drags > 0 && deep % KariDragTrait.Deepen == 0, $"引きずり出し {drags}・的がカリ {aimed}・深めた {deep}");
        // verbose の有無で結果が変わらない
        int diff = 0;
        foreach (var x in new[] { f, g })
            foreach (var w in Waves)
                for (int s = 0; s < 10; s++)
                {
                    var (a, _, _) = I.Raw(x, w, s, verbose: true); var (b, _, _) = I.Raw(x, w, s, verbose: false);
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns || a.PlayerSurvivors != b.PlayerSurvivors) diff++;
                }
        Expect("verbose の有無で勝敗 ／ T ／ 生存が変わらない（2 版 × 4 波 × seed 0..9）", diff == 0, $"違い {diff}");
        // 乱数を新たに引かない: engine の第408期の口に PickOne ／ Roll が無い
        string src = File.Exists("BattleCore/BattleEngine.cs") ? File.ReadAllText("BattleCore/BattleEngine.cs") : "";
        int i0 = src.IndexOf("第408期 —— カリの版", StringComparison.Ordinal);
        int i1 = i0 < 0 ? -1 : src.IndexOf("/// <summary>第351期 WT-a", i0, StringComparison.Ordinal);
        string body = i0 >= 0 && i1 > i0 ? src[i0..i1] : "";
        Expect("第408期の口に `PickOne` ／ `Roll(` が無い（決定的に選ぶ）", body.Length > 0 && !body.Contains("PickOne") && !body.Contains("Roll("), $"{body.Length} 文字");
        // 試遊の台
        bool rows = Presets.Versions.Any(r => r.Name == "版・K-k 呪い" && r.F.Occupied().Any(o => o.Def == kk))
                 && Presets.Versions.Any(r => r.Name == "版・K-d 呪い" && r.F.Occupied().Any(o => o.Def == kd || o.Def == UnitCatalog.KariKd0));   // 第409期に K-d0 へ入れ替えた
        Expect("試遊の台「版・K-k 呪い」「版・K-d 呪い」が `Presets.Versions` にある", rows);
        Console.WriteLine();
        Console.WriteLine(_fail == 0 ? "**すべて OK**" : $"**NG {_fail} 件**");
        if (_fail > 0) Environment.ExitCode = 1;
    }
}
