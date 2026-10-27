using BattleCore;
using static Common;
using M = Mudo412Diag;
using S307 = Som307Diag;
using I = Inv348Diag;

// =====================================================================================
// wata413 —— 第413期 段0: ムド M-ud を規定に ／ 本体【軽い期・版だけ】: ワタの渡し手番（W-u 刃を呼ぶ ／ W-m 泥を呼ぶ）。
// 指示書は design/PHASE413_WATA_GIFT_SPEC.md ／ 報告は design/PHASE413_WATA_GIFT.md。
//
//     dotnet run --project BattleSim -c Release 0 wata413 s0       # 段0 の確かめ: 「試遊・呪い」「試遊・カーズ 呪い」× 4 波・seed 0..99 が第412期 §4-1 の M-ud の行と同じか（違えば exit 1）
//     dotnet run --project BattleSim -c Release 0 wata413 hang [行名] # compare の1行 × 近衛 ／ 大隊 × seed 0..999 を verbose で回し、5 秒で決着しない戦を探す
//     dotnet run --project BattleSim -c Release 0 wata413 boards   # §3-2: 「試遊・呪い」× {規定, W-u, W-m} × ボス ／ 近衛 ／ 大隊 ／ 強 第4波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 wata413 seats    # §3-3: ワタの席だけ 5 通り（ほかの4枚は相対順を保って詰める）× {規定, W-u, W-m} × 4 波
//     dotnet run --project BattleSim -c Release 0 wata413 mixed    # §3-4: compare のワタを含む行のワタを版に替えた写し × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..199
//     dotnet run --project BattleSim -c Release 0 wata413 log <0|u|m> <boss|guard|bat|s4> [seed]
//     dotnet run --project BattleSim -c Release 0 wata413 check    # 自己検査
//
// **予測のファイルが無ければ boards ／ seats ／ mixed を走らせない**（R397・`.tmp/p413/predict.md`）。
// =====================================================================================
static class Wata413Diag
{
    /// <summary>第412期 §4-1 の M-ud の行（勝率・倒しT ／ 倒れた味方 ／ 尻尾の直 ／T・尻尾の長さ）。</summary>
    static readonly (string Board, string[] Cells)[] Expect412 =
    {
        ("試遊・呪い", new[] { "100.0・6.0T ／ 0.00 ／ 510.0 ／ 6.0T", "100.0・4.0T ／ 0.82 ／ 1.0 ／ 1.0T", "100.0・4.0T ／ 0.94 ／ 9.5 ／ 1.0T", "100.0・6.1T ／ 1.00 ／ 54.2 ／ 1.6T" }),
        ("試遊・カーズ 呪い", new[] { "100.0・7.0T ／ 0.00 ／ 438.8 ／ 7.0T", "100.0・4.8T ／ 0.31 ／ 28.9 ／ 1.2T", "100.0・4.5T ／ 0.62 ／ 17.1 ／ 1.1T", "100.0・6.3T ／ 0.15 ／ 42.0 ／ 1.4T" }),
    };

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "s0";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "seats" or "mixed" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"wata413: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "s0": S0(); break;
            case "hang": Hang(args.Length > 3 ? args[3] : "速攻 (ボルグ×ムド)"); break;
            case "boards": Boards(); break;
            case "seats": Seats(); break;
            case "mixed": Mixed(); break;
            case "log": Log(A(3, "u"), A(4, "boss"), int.Parse(A(5, "0"))); return;
            case "check": Check(); break;
            default: Console.WriteLine("wata413: モードは s0 / hang / boards / seats / mixed / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static string Cell(M.Fx a) => $"{a.WT} ／ {(a.N == 0 ? "—" : ((double)a.Fell / a.N).ToString("F2"))} ／ {a.Tail}";

    static void S0()
    {
        Console.WriteLine("# 第413期 段0 の確かめ —— 規定のムド（M-ud）の2台 × 4 波・seed 0..99 が第412期 §4-1 の M-ud の行と同じか");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 第412期 M-ud | いま（規定） | 同じ |");
        Console.WriteLine("|---|---|---|---|---|");
        int same = 0, all = 0;
        foreach (var (board, cells) in Expect412)
        {
            var f = M.BoardOf(board);
            for (int i = 0; i < M.Waves.Length; i++)
            {
                string now = Cell(M.Measure(f, M.Waves[i]));
                bool s = now == cells[i]; all++; if (s) same++;
                Console.WriteLine($"| {board} | {M.Waves[i].Name} | {cells[i]} | {now} | {(s ? "はい" : "**いいえ**")} |");
            }
        }
        Console.WriteLine();
        bool mudoOk = UnitCatalog.Mudo.Traits.SequenceEqual(UnitCatalog.MudoMud.Traits) && UnitCatalog.All.Contains(UnitCatalog.Mudo) && !UnitCatalog.All.Contains(UnitCatalog.MudoH412);
        Console.WriteLine($"- 規定のムドの札が第412期の M-ud と同じ・旧（`MudoH412`）は `All` に無い: {(mudoOk ? "○" : "**×**")}");
        Console.WriteLine($"- 「版・M-ud 呪い」が試遊の台に無い: {(!Presets.Versions.Any(r => r.Name == "版・M-ud 呪い") ? "○" : "**×**")}");
        Console.WriteLine();
        Console.WriteLine($"{all} セルのうち同じ {same}。");
        if (same != all || !mudoOk || Presets.Versions.Any(r => r.Name == "版・M-ud 呪い")) Environment.ExitCode = 1;
    }

    /// <summary>止まる戦を探す（verbose・1戦ごとに 5 秒で打ち切り）。</summary>
    static void Hang(string row)
    {
        var f = CompareBuilds().First(r => r.Name == row).F;
        var waves = new (string, EnemyWave, EnemyScaleRule)[] { ("近衛", EnemyCatalog.EliteGuardWave, EnemyCatalog.EliteScale), ("大隊", EnemyCatalog.EliteBattalionWave, EnemyCatalog.EliteScale) };
        foreach (var (wn, w, sc) in waves)
            for (int seed = 0; seed < 1000; seed++)
            {
                var t = System.Threading.Tasks.Task.Run(() => BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(w, sc), seed, verbose: true));
                if (!t.Wait(5000)) { Console.WriteLine($"止まった: {row} × {wn} × seed {seed}"); Environment.Exit(3); }
                if (t.Result.Turns >= 30) Console.WriteLine($"{wn} seed {seed}: T30");
            }
    }

    // =================================================================================
    // 本体: ワタの渡し手番
    // =================================================================================
    const string PredictFile = ".tmp/p413/predict.md";
    const int Seeds = 100, TT = 10, CompareSeeds = 200;
    static string F1(double x) => S307.F1(x);
    static string Per(long a, long n) => n == 0 ? "—" : F1((double)a / n);
    static string Per2(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Pct(long a, long n) => n == 0 ? "—" : F1(100.0 * a / n);
    static string Short(UnitDef d) => S307.Short(d);
    static readonly (string Key, string Label, UnitDef D)[] Vers =
        { ("0", "規定", UnitCatalog.Wata), ("u", "W-u", UnitCatalog.WataWu), ("m", "W-m", UnitCatalog.WataWm) };
    static Formation Play => M.BoardOf("試遊・呪い");
    static Formation SwapWata(Formation f, UnitDef w)
    {
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (o.Def.Id == "wata") g[o.Slot] = w;
        return g;
    }
    static I.W WaveOf(string k) => k switch { "boss" => I.Boss, "guard" => I.Guard, "bat" => I.Bat, _ => I.StrongWaves[2] };
    static void Log(string ver, string wave, int seed)
    {
        var v = Vers.First(x => x.Key == ver);
        var f = SwapWata(Play, v.D); var w = WaveOf(wave);
        var (r, _, _) = I.Raw(f, w, seed, verbose: true);
        Console.WriteLine($"# {S307.Seats(f)}（ワタ {v.Label}）× {w.Name} × seed {seed}: {(r.PlayerWon ? "勝ち" : "負け")} T{r.Turns} 生存 {r.PlayerSurvivors}");
        foreach (var l in r.Log) Console.WriteLine(l.ToString());
    }

    sealed class Gx
    {
        public long N, Wins, WinT, Fell, Turns, TailN, TailTurns, TailDirect;
        public readonly Dictionary<string, long> Death = new(), DeathT = new();
        // ワタ
        public long WTurns, Calls, Silent, NoAlly, Blocked, Reach, Dry, WataDied, WataDeathT, WataLiveTurns, Pushed, SelfHarm, WataDealt;
        public readonly long[] CallsByT = new long[TT + 1], LiveByT = new long[TT + 1];
        public readonly List<(int Live, int Calls)> Pairs = new();
        // 応えた側（駒 → 応えた回数 ／ 発 ／ 与ダメ ／ 軛で切られた発）
        public readonly Dictionary<string, long> Ans = new(), AnsSw = new(), AnsDmg = new(), AnsCut = new();
        // ムド（暴発 ／ 吐き）
        public long Fires, ESwings, Vents, VentRage, VentBelow, VentSwings, MDmg;
        // ウツ
        public long USwings;
        public void Add(Gx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Turns += o.Turns; TailN += o.TailN; TailTurns += o.TailTurns; TailDirect += o.TailDirect;
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
            WTurns += o.WTurns; Calls += o.Calls; Silent += o.Silent; NoAlly += o.NoAlly; Blocked += o.Blocked; Reach += o.Reach; Dry += o.Dry;
            WataDied += o.WataDied; WataDeathT += o.WataDeathT; WataLiveTurns += o.WataLiveTurns; Pushed += o.Pushed; SelfHarm += o.SelfHarm; WataDealt += o.WataDealt;
            for (int t = 0; t <= TT; t++) { CallsByT[t] += o.CallsByT[t]; LiveByT[t] += o.LiveByT[t]; }
            Pairs.AddRange(o.Pairs);
            foreach (var (k, v) in o.Ans) Ans[k] = Ans.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.AnsSw) AnsSw[k] = AnsSw.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.AnsDmg) AnsDmg[k] = AnsDmg.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.AnsCut) AnsCut[k] = AnsCut.GetValueOrDefault(k) + v;
            Fires += o.Fires; ESwings += o.ESwings; Vents += o.Vents; VentRage += o.VentRage; VentBelow += o.VentBelow; VentSwings += o.VentSwings; MDmg += o.MDmg;
            USwings += o.USwings;
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
        public string WT => $"{F1(Win)}・{WinTs}T";
        public string Tail => TailTurns == 0 ? "—" : $"{Per(TailDirect, TailTurns)} ／ {Per(TailTurns, TailN)}T";
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {Pct(kv.Value, N)}%・T{Per(DeathT[kv.Key], kv.Value)}"));
        /// <summary>戦ごとの（ワタが生きていた手番の数, 鳴った回数）の相関（ピアソン）。</summary>
        public string Corr
        {
            get
            {
                if (Pairs.Count < 3) return "—";
                double mx = Pairs.Average(p => p.Live), my = Pairs.Average(p => p.Calls);
                double sxy = Pairs.Sum(p => (p.Live - mx) * (p.Calls - my)), sxx = Pairs.Sum(p => (p.Live - mx) * (p.Live - mx)), syy = Pairs.Sum(p => (p.Calls - my) * (p.Calls - my));
                return sxx == 0 || syy == 0 ? "—（片方が定数）" : (sxy / Math.Sqrt(sxx * syy)).ToString("F2");
            }
        }
    }

    static Gx One(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: true);
        var a = new Gx { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        a.Fell = p.Count(u => u.LastDeathTurn > 0);
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { string k = Short(u.Def); a.Death[k] = 1; a.DeathT[k] = u.LastDeathTurn; }
        var mine = p.Select(u => u.InstanceId).ToHashSet();
        var foes = e.Select(u => u.InstanceId).ToHashSet();
        foreach (var x in r.Events) if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid) (x.Team == BattleContext.PlayerTeam ? mine : foes).Add(sid);
        var wata = p.FirstOrDefault(u => u.Def.Id == "wata"); var utsu = p.FirstOrDefault(u => u.Def.Id == "utsu");
        var dead = new HashSet<int>();
        int tailStart = foes.Count == 1 ? 0 : -1;
        foreach (var x in r.Events)
        {
            int t = x.Turn;
            if (x.Kind == BattleEventKind.Death && x.TargetId is int di && foes.Contains(di))
            {
                dead.Add(di);
                if (tailStart < 0 && foes.Count(id => !dead.Contains(id)) == 1) tailStart = t;
            }
            if (x.Kind == BattleEventKind.Revive && x.TargetId is int ri) dead.Remove(ri);
            if (x.Kind == BattleEventKind.Damage && x.Amount > 0 && x.TargetId is int ti && foes.Contains(ti) && x.ActorId is int ai && mine.Contains(ai) && x.ShareFromId is null && tailStart >= 0 && t > tailStart) a.TailDirect += x.Amount;
            if (wata is not null && x.Kind == BattleEventKind.StatSnapshot && x.TargetId == wata.InstanceId && t is >= 1 and <= TT) a.LiveByT[t] = 1;
            if (wata is not null && x.Kind == BattleEventKind.StatusGain && x.Text == BattleContext.DullKey && x.ActorId == wata.InstanceId && x.TargetId is int pt && foes.Contains(pt)) a.Pushed += x.Amount;
            if (wata is not null && x.Kind == BattleEventKind.Damage && x.TargetId == wata.InstanceId && x.ActorId is null) a.SelfHarm += x.Amount;
            if (utsu is not null && x.Kind == BattleEventKind.Attack && x.ActorId == utsu.InstanceId) a.USwings++;
        }
        if (tailStart >= 0 && r.Turns > tailStart) { a.TailN = 1; a.TailTurns = r.Turns - tailStart; } else a.TailDirect = 0;
        if (wata is not null && r.TallyByUnit.TryGetValue("wata", out var tw))
        {
            a.WTurns = tw.Wg413Turns; a.Calls = tw.Wg413Calls; a.Silent = tw.Wg413Silent; a.NoAlly = tw.Wg413NoAlly; a.Blocked = tw.Wg413Blocked; a.Reach = tw.Wg413Reach; a.Dry = tw.Wg413Dry;
            a.WataDealt = tw.DamageToEnemy;
            if (tw.Wg413CallsByTurn is { } cb) for (int t = 1; t <= TT && t < cb.Length; t++) a.CallsByT[t] = cb[t];
            if (wata.LastDeathTurn > 0) { a.WataDied = 1; a.WataDeathT = wata.LastDeathTurn; }
            int live = (int)a.LiveByT.Sum(); a.WataLiveTurns = live;
            a.Pairs.Add((live, (int)tw.Wg413Calls));
        }
        foreach (var u in p)
            if (r.TallyByUnit.TryGetValue(u.Def.Id, out var tu) && tu.Wg413Answers > 0)
            {
                string k = Short(u.Def);
                a.Ans[k] = tu.Wg413Answers; a.AnsSw[k] = tu.Wg413Swings; a.AnsDmg[k] = tu.Wg413Dealt; a.AnsCut[k] = tu.Wg413YokeCut;
            }
        if (r.TallyByUnit.TryGetValue("mudo", out var tm))
        {
            a.Fires = tm.EruptFires; a.ESwings = tm.EruptSwings; a.Vents = tm.Wg413Vents; a.VentRage = tm.Wg413VentRage; a.VentBelow = tm.Wg413VentBelow; a.VentSwings = tm.Wg413Swings; a.MDmg = tm.DamageToEnemy;
        }
        return a;
    }
    static Gx Measure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Gx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, s));
        var all = new Gx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    static void Boards()
    {
        var b = Play;
        Console.WriteLine($"# 第413期 §3-2 —— 「試遊・呪い」（{S307.Seats(b)}・ムドは段0 の規定）× {{規定, W-u, W-m}} × {string.Join(" ／ ", M.Waves.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("尻尾 ＝ 生きた敵が1体になった次のターンから決着まで（ボスは開戦から）・尻尾の直 ／T ＝ 味方 → 敵の直の与ダメ（共有を除く）÷ 尻尾のターン数。"
            + "鳴った ＝ ワタの手番で、前の手番から押し付けた敵へ味方が応えた回数（`Wg413Calls`）。押し付け無し ＝ 前の手番から敵へ1度も押し付けていなかった手番。");
        Console.WriteLine();
        var res = Vers.Select(v => M.Waves.Select(w => Measure(SwapWata(b, v.D), w)).ToArray()).ToArray();
        Console.WriteLine("## 1. 勝率・倒しT ／ 倒れた味方（／戦）／ 尻尾の直 ／T・尻尾の長さ（括弧は規定との差）");
        Console.WriteLine();
        Console.WriteLine("| 版 | " + string.Join(" | ", M.Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(M.Waves.Select(_ => "---|")));
        for (int v = 0; v < Vers.Length; v++)
            Console.WriteLine($"| {Vers[v].Label} | " + string.Join(" | ", M.Waves.Select((w, i) =>
            {
                var a = res[v][i]; var z = res[0][i];
                return $"{a.WT}{(v == 0 ? "" : $"（{a.Win - z.Win:+0.0;-0.0;0.0}）")} ／ {Per2(a.Fell, a.N)} ／ {a.Tail}";
            })) + " |");
        Console.WriteLine();
        Console.WriteLine("## 2. 倒れた味方（駒・倒れた割合・平均T）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 倒れた |");
        Console.WriteLine("|---|---|---|");
        for (int v = 0; v < Vers.Length; v++) for (int i = 0; i < M.Waves.Length; i++) Console.WriteLine($"| {Vers[v].Label} | {M.Waves[i].Name} | {res[v][i].Fall} |");
        Console.WriteLine();
        Console.WriteLine("## 3. ワタ（戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 手番 | 鳴った | 押し付け無し ／ 応え手なし ／ 止められた ／ 届かない ／ 怒り 0 | 押し付けた量 | 自傷 | ワタの与ダメ | 倒れた・T | 生きていた手番 ／ 鳴った の相関 |");
        Console.WriteLine("|---|---|--:|--:|---|--:|--:|--:|---|--:|");
        for (int v = 0; v < Vers.Length; v++) for (int i = 0; i < M.Waves.Length; i++)
        {
            var a = res[v][i];
            Console.WriteLine($"| {Vers[v].Label} | {M.Waves[i].Name} | {Per(a.WTurns, a.N)} | {Per(a.Calls, a.N)} | {Per(a.Silent, a.N)} ／ {Per(a.NoAlly, a.N)} ／ {Per(a.Blocked, a.N)} ／ {Per(a.Reach, a.N)} ／ {Per(a.Dry, a.N)} | {Per(a.Pushed, a.N)} | {Per(a.SelfHarm, a.N)} | {Per(a.WataDealt, a.N)} | {Pct(a.WataDied, a.N)}%・T{Per(a.WataDeathT, a.WataDied)} | {(v == 0 ? "—" : a.Corr)} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 3-2. ターンごとの鳴った回数（そのターンにワタが生きていた戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | " + string.Join(" | ", Enumerable.Range(1, 6).Select(t => $"T{t}")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Range(1, 6).Select(_ => "---|")));
        for (int v = 1; v < Vers.Length; v++) for (int i = 0; i < M.Waves.Length; i++)
        {
            var a = res[v][i];
            Console.WriteLine($"| {Vers[v].Label} | {M.Waves[i].Name} | " + string.Join(" | ", Enumerable.Range(1, 6).Select(t => a.LiveByT[t] == 0 ? "—" : $"{Per2(a.CallsByT[t], a.LiveByT[t])}（{a.LiveByT[t]}）")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("## 4. 応えた味方（戦あたり・応えた回数 ／ 発 ／ 与ダメ ／ 軛で切られた発の割合）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 応えた味方 |");
        Console.WriteLine("|---|---|---|");
        for (int v = 1; v < Vers.Length; v++) for (int i = 0; i < M.Waves.Length; i++)
        {
            var a = res[v][i];
            string cell = a.Ans.Count == 0 ? "—" : string.Join("、", a.Ans.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Per2(kv.Value, a.N)} ／ {Per(a.AnsSw[kv.Key], a.N)} ／ {Per(a.AnsDmg[kv.Key], a.N)} ／ {Pct(a.AnsCut[kv.Key], a.AnsSw[kv.Key])}%"));
            Console.WriteLine($"| {Vers[v].Label} | {M.Waves[i].Name} | {cell} |");
        }
        Console.WriteLine();
        Console.WriteLine("## 5. ムドとウツ（戦あたり）");
        Console.WriteLine();
        Console.WriteLine("暴発 ＝ 閾を越えた暴発（`EruptFires` ／ `EruptSwings`）。吐き ＝ W-m で吐いた回数 ／ 吐いた怒り（1回の発数）／ 閾に届かず吐いた割合。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 波 | 暴発 回 ／ 発（1回の発） | 吐き 回 ／ 怒り（1回の発）／ 閾に届かず | 暴発 ＋ 吐きの発 | ムドの与ダメ | ウツの発 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|");
        for (int v = 0; v < Vers.Length; v++) for (int i = 0; i < M.Waves.Length; i++)
        {
            var a = res[v][i];
            Console.WriteLine($"| {Vers[v].Label} | {M.Waves[i].Name} | {Per(a.Fires, a.N)} ／ {Per(a.ESwings, a.N)}（{Per2(a.ESwings, a.Fires)}） | {Per(a.Vents, a.N)} ／ {Per(a.VentRage, a.N)}（{Per2(a.VentRage, a.Vents)}）／ {Pct(a.VentBelow, a.Vents)}% | {Per(a.ESwings + a.VentSwings, a.N)} | {Per(a.MDmg, a.N)} | {Per(a.USwings, a.N)} |");
        }
        Console.WriteLine();
    }

    static void Seats()
    {
        var b = Play;
        var order = b.Occupied().OrderBy(o => o.Slot).Select(o => o.Def).ToArray();
        var others = order.Where(d => d.Id != "wata").ToArray();
        int[] slots = { 0, 1, 2, 3, 4 };
        string[] seat = { "前1", "前3", "中央", "後1", "後3" };
        Console.WriteLine($"# 第413期 §3-3 —— 「試遊・呪い」のワタの席だけ 5 通り（ほかの4枚 {string.Join("・", others.Select(Short))} は相対順を保って詰める）× {{規定, W-u, W-m}} × 4 波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("各セル: 勝率・倒しT ／ 鳴った回数 ／戦 ／ ワタが倒れた割合・T。");
        Console.WriteLine();
        foreach (var w in M.Waves)
        {
            Console.WriteLine($"## {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| ワタの席 | " + string.Join(" | ", Vers.Select(v => v.Label)) + " |");
            Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "---|")));
            for (int ws = 0; ws < 5; ws++)
            {
                var cells = Vers.Select(v =>
                {
                    var g = new Formation(); int k = 0;
                    foreach (int sl in slots) g[sl] = sl == ws ? v.D : others[k++];
                    var a = Measure(g, w);
                    return $"{a.WT} ／ {Per2(a.Calls, a.N)} ／ {Pct(a.WataDied, a.N)}%・T{Per(a.WataDeathT, a.WataDied)}";
                }).ToArray();
                Console.WriteLine($"| {seat[ws]}{(ws == 0 ? "（いま）" : "")} | {string.Join(" | ", cells)} |");
            }
            Console.WriteLine();
        }
    }

    sealed class MC { public long N, Wins, Calls, Ans, Turns, Silent, NoAlly, Reach; public readonly Dictionary<string, long> Who = new(); }
    static MC MCell(Formation f, Func<Formation, int, BattleResult> run)
    {
        var parts = new MC[CompareSeeds];
        Parallel.For(0, CompareSeeds, s =>
        {
            var r = run(f, s);
            var c = new MC { N = 1, Wins = r.PlayerWon ? 1 : 0 };
            if (r.TallyByUnit.TryGetValue("wata", out var t)) { c.Calls = t.Wg413Calls; c.Turns = t.Wg413Turns; c.Silent = t.Wg413Silent; c.NoAlly = t.Wg413NoAlly; c.Reach = t.Wg413Reach; }
            foreach (var (id, tu) in r.TallyByUnit) if (tu.Wg413Answers > 0) c.Who[id] = tu.Wg413Answers;
            parts[s] = c;
        });
        var a = new MC();
        foreach (var c in parts) { a.N += c.N; a.Wins += c.Wins; a.Calls += c.Calls; a.Turns += c.Turns; a.Silent += c.Silent; a.NoAlly += c.NoAlly; a.Reach += c.Reach; foreach (var (k, v) in c.Who) a.Who[k] = a.Who.GetValueOrDefault(k) + v; }
        return a;
    }
    static void Mixed()
    {
        var rows = CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "wata")).ToArray();
        var waves = EnemyCatalog.Stages.Select((st, i) => ($"第{i + 1}波", (Func<Formation, int, BattleResult>)((f, s) => BattleEngine.Run(f, st.Enemy, s, verbose: false))))
            .Concat(new[] { ("近衛", EnemyCatalog.EliteGuardWave), ("大隊", EnemyCatalog.EliteBattalionWave) }
                .Select(x => (x.Item1, (Func<Formation, int, BattleResult>)((f, s) => BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(x.Item2, EnemyCatalog.EliteScale), s, verbose: false)))))
            .ToArray();
        Console.WriteLine($"# 第413期 §3-4 —— `compare` のワタを含む {rows.Length} 行のワタを版に替えた写し × 本編 5 波（`compare` と同じ）＋ 精鋭 2 波（`elite` と同じ）・seed 0..{CompareSeeds - 1}");
        Console.WriteLine();
        Console.WriteLine("各セル: 勝率（規定との差）・鳴った回数 ／戦。行の下に応えた味方の内訳（全波の延べ）。");
        Console.WriteLine();
        foreach (var (name, f) in rows)
        {
            Console.WriteLine($"**{name}**（{S307.Seats(f)}）");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", waves.Select(w => w.Item1)) + " |");
            Console.WriteLine("|---|" + string.Concat(waves.Select(_ => "---|")));
            var base0 = waves.Select(w => MCell(f, w.Item2)).ToArray();
            var who = new Dictionary<string, Dictionary<string, long>>();
            foreach (var v in Vers)
            {
                var g = SwapWata(f, v.D);
                var cells = v.Key == "0" ? base0 : waves.Select(w => MCell(g, w.Item2)).ToArray();
                var wd = who[v.Label] = new();
                foreach (var c in cells) foreach (var (k, n) in c.Who) wd[k] = wd.GetValueOrDefault(k) + n;
                Console.WriteLine($"| {v.Label} | " + string.Join(" | ", cells.Select((c, i) =>
                    $"{Pct(c.Wins, c.N)}{(v.Key == "0" ? "" : $"（{100.0 * (c.Wins - base0[i].Wins) / c.N:+0.0;-0.0;0.0}）")}・{Per2(c.Calls, c.N)}")) + " |");
            }
            Console.WriteLine();
            foreach (var v in Vers.Skip(1))
            {
                var g = SwapWata(f, v.D);
                var all = waves.Select(w => MCell(g, w.Item2)).ToArray();
                Console.WriteLine($"- {v.Label} のワタの手番（全波の延べ・戦あたり）: 手番 {Per(all.Sum(c => c.Turns), all.Sum(c => c.N))} ／ 押し付け無し {Per(all.Sum(c => c.Silent), all.Sum(c => c.N))} ／ 応え手なし {Per(all.Sum(c => c.NoAlly), all.Sum(c => c.N))} ／ 届かない {Per(all.Sum(c => c.Reach), all.Sum(c => c.N))}");
            }
            foreach (var v in Vers.Skip(1))
                Console.WriteLine($"- {v.Label} の応え手: " + (who[v.Label].Count == 0 ? "—" : string.Join("、", who[v.Label].OrderByDescending(kv => kv.Value).Select(kv => $"{Short(UnitCatalog.ById(kv.Key))} {kv.Value}"))));
            Console.WriteLine();
        }
    }

    static void Check()
    {
        int bad = 0;
        void Expect(string what, bool ok) { Console.WriteLine($"- {(ok ? "○" : "**×**")} {what}"); if (!ok) bad++; }
        Console.WriteLine("# 第413期 wata413 check");
        Console.WriteLine();
        var w0 = UnitCatalog.Wata;
        foreach (var (_, label, d) in Vers.Skip(1))
            Expect($"{label}: `Id` ／ HP ／ 攻 ／ 速は規定と同じ・札は規定の後ろに1枚足しただけ・手番は Skill だけ・`All` に無い",
                d.Id == w0.Id && d.MaxHp == w0.MaxHp && d.Attack == w0.Attack && d.Speed == w0.Speed && d.Traits.Count == w0.Traits.Count + 1
                && d.Traits.Take(w0.Traits.Count).SequenceEqual(w0.Traits) && d.Actions.All(x => x.Kind == ActionKind.Skill) && !UnitCatalog.All.Contains(d));
        Expect("ロスター（`All`）に W-u ／ W-m の札の保持者が 0 枚", !UnitCatalog.All.Any(d => d.Traits.Contains(TraitId.WataCallBlade) || d.Traits.Contains(TraitId.WataCallMire)));
        Expect("「版・W-u 呪い」「版・W-m 呪い」が `Presets.Versions` の末尾2つ・過去の器具の一覧（`VersionsH407`）に無い",
            Presets.Versions[^2].Name == "版・W-u 呪い" && Presets.Versions[^1].Name == "版・W-m 呪い" && !VersionsH407().Any(r => r.Name.StartsWith("版・W-", StringComparison.Ordinal)));
        var z = Measure(Play, I.StrongWaves[2]);
        Expect("規定のワタの台で渡し手番の計数が 0", z.WTurns == 0 && z.Calls == 0);
        var u = Measure(SwapWata(Play, UnitCatalog.WataWu), I.Boss);
        var m = Measure(SwapWata(Play, UnitCatalog.WataWm), I.Boss);
        Expect($"W-u: ボスで鳴る（{Per(u.Calls, u.N)} ／戦）・ワタ自身は殴らない（与ダメ {Per(u.WataDealt, u.N)}）・吐きは 0", u.Calls > 0 && u.WataDealt == 0 && u.Vents == 0);
        Expect($"W-m: ボスで吐く（{Per(m.Vents, m.N)} ／戦）・ワタ自身は殴らない（与ダメ {Per(m.WataDealt, m.N)}）", m.Vents > 0 && m.WataDealt == 0);
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "**全部 ○**" : $"**× {bad} 件**");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
