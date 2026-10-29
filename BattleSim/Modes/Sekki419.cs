using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// =====================================================================================
// sekki419 —— 第419期【軽い期・版だけ】人呑みのセッキに「呑む」を足す（SW-a 仲間を呑んで守る ／ SW-e 敵を呑む ／ SW-ae 両方）。
// 指示書は design/PHASE419_SEKKI_SWALLOW_SPEC.md ／ 報告は design/PHASE419_SEKKI_SWALLOW.md。
//
//     dotnet run --project BattleSim -c Release 0 sekki419 p0       # Phase 0 (a)(b): 手番の台 2 つ × 4 波・seed 0..99（倒れる一撃・包み ／ 弾き返し・動かなかった味方）
//     dotnet run --project BattleSim -c Release 0 sekki419 p0c      # Phase 0 (c): elite 分かち×逆しま (ドハ×ウツ) 近衛 の負けを 1 戦（旧のリリ LiliH417 と並べる）
//     dotnet run --project BattleSim -c Release 0 sekki419 boards   # §3-2 ／ §3-3: 台 3 つ × {規定, SW-a, SW-e, SW-ae} × 4 波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 sekki419 mixed    # §3-4: `compare` のセッキを含む行 × {規定, SW-a, SW-e, SW-ae} × 本編 5 波 ＋ 近衛 ／ 大隊・seed 0..199
//     dotnet run --project BattleSim -c Release 0 sekki419 log <0|a|e|ae> <台の鍵> <波の鍵> [seed]
//     dotnet run --project BattleSim -c Release 0 sekki419 check    # 自己検査
//
// **予測のファイルが無ければ boards ／ mixed を走らせない**（R397・`.tmp/p419/predict.md`）。
// =====================================================================================
static class Sekki419Diag
{
    const string PredictFile = ".tmp/p419/predict.md";
    const int Seeds = 100;

    static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("0", "規定", UnitCatalog.Sekki), ("a", "SW-a", UnitCatalog.SekkiSWa), ("e", "SW-e", UnitCatalog.SekkiSWe), ("ae", "SW-ae", UnitCatalog.SekkiSWae),
    };

    /// <summary>台（§3-2 ／ §3-3）。鍵 ／ 名前 ／ 並び（規定のセッキ）。</summary>
    static readonly (string Key, string Name, Func<UnitDef, Formation> F)[] Boards =
    {
        ("wrap", "試遊・手番 包み", s => Presets.SlimeLamp396(s)),
        ("core4", "試遊・手番 芯4枚", s => Presets.FourCore397(sekki: s)),
        ("curse", "試遊・呪い（クビ → セッキ）", s => Formation.Build(front1: UnitCatalog.Wata, front3: UnitCatalog.Mudo, center: UnitCatalog.Nel, back1: s, back3: UnitCatalog.Utsu)),
    };
    static readonly (string Key, I.W W)[] WaveKeys = { ("boss", I.Boss), ("guard", I.Guard), ("bat", I.Bat), ("s4", I.StrongWaves[2]) };

    static string Short(UnitDef d) => S307.Short(d);
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F0") + "%";
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");

    static Formation Swap(Formation f, UnitDef sekki)
    {
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (o.Def.Id == "sekki") g[o.Slot] = sekki;
        return g;
    }

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    sealed class Fx
    {
        public long N, Wins, WinT, Turns, Fell, FirstFellT, FirstFellN;
        public long LethalSingle, LethalAoe, LethalOther;   // Phase 0 (a)（verbose のときだけ）
        public long WrapHits, WrapAmt, Bursts, BurstAmt, BurstDealt, StoreMax;
        public long IdleHead, IdleHeadSold, RallyAmt;
        public long Gulps, GulpAmt, GulpMax, GulpRest, GulpSpent, BlockHits, BlockTurns, GulpedLost, RallyRead, LampRead, GulpedLaterFell, GulpedUnits;
        public long FoeGulps, FoeSpits, FoeHeld, SpitDmg, SpitDealt, SpitKilled, BurstBlocked, FoeNone;
        public readonly Dictionary<string, long> Death = new(), DeathT = new(), GulpWho = new(), FoeWho = new();
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Turns += o.Turns; Fell += o.Fell; FirstFellT += o.FirstFellT; FirstFellN += o.FirstFellN;
            LethalSingle += o.LethalSingle; LethalAoe += o.LethalAoe; LethalOther += o.LethalOther;
            WrapHits += o.WrapHits; WrapAmt += o.WrapAmt; Bursts += o.Bursts; BurstAmt += o.BurstAmt; BurstDealt += o.BurstDealt; StoreMax = Math.Max(StoreMax, o.StoreMax);
            IdleHead += o.IdleHead; IdleHeadSold += o.IdleHeadSold; RallyAmt += o.RallyAmt;
            Gulps += o.Gulps; GulpAmt += o.GulpAmt; GulpMax = Math.Max(GulpMax, o.GulpMax); GulpRest += o.GulpRest; GulpSpent += o.GulpSpent; BlockHits += o.BlockHits; BlockTurns += o.BlockTurns;
            GulpedLost += o.GulpedLost; RallyRead += o.RallyRead; LampRead += o.LampRead; GulpedLaterFell += o.GulpedLaterFell; GulpedUnits += o.GulpedUnits;
            FoeGulps += o.FoeGulps; FoeSpits += o.FoeSpits; FoeHeld += o.FoeHeld; SpitDmg += o.SpitDmg; SpitDealt += o.SpitDealt; SpitKilled += o.SpitKilled; BurstBlocked += o.BurstBlocked; FoeNone += o.FoeNone;
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.GulpWho) GulpWho[k] = GulpWho.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.FoeWho) FoeWho[k] = FoeWho.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
        public string WT => $"{F1(Win)}・{(Wins == 0 ? "—" : ((double)WinT / Wins).ToString("0.0"))}T";
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {Pc(kv.Value, N)}・T{Per1(DeathT[kv.Key], kv.Value)}"));
        public static string Who(Dictionary<string, long> d, long n) => d.Count == 0 ? "—" : string.Join("、", d.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key} {Per(kv.Value, n)}"));
    }

    static Fx One(Formation f, I.W w, int seed, bool verbose = false)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: verbose);
        var a = new Fx { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        var fell = p.Where(u => u.LastDeathTurn > 0).ToList();
        a.Fell = fell.Count;
        if (fell.Count > 0) { a.FirstFellN = 1; a.FirstFellT = fell.Min(u => u.LastDeathTurn); }
        foreach (var u in fell) { string k = Short(u.Def); a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + u.LastDeathTurn; }
        if (verbose)
        {
            var pid = p.Select(u => u.InstanceId).ToHashSet(); var eid = e.Select(u => u.InstanceId).ToHashSet();
            foreach (var ev in r.Events)
            {
                if (ev.Kind != BattleEventKind.Damage || ev.TargetId is not int t || !pid.Contains(t) || ev.HpAfter > 0 || ev.Relayed) continue;
                if (ev.ActorId is not int s || !eid.Contains(s)) { a.LethalOther++; continue; }
                if (ev.Pattern is null) a.LethalOther++; else if (ev.Pattern == AttackPattern.Single) a.LethalSingle++; else a.LethalAoe++;
            }
        }
        a.RallyAmt = r.WhetByRoute[(int)WhetRoute.RallyTurn];
        foreach (var u in p)
        {
            if (!r.TallyByUnit.TryGetValue(u.Def.Id, out var t)) continue;
            a.IdleHead += t.Sw419IdleHead; a.IdleHeadSold += t.Sw419IdleHeadSold;
            a.GulpedLost += t.Sw419GulpedLost; a.RallyRead += t.Sw419RallyRead; a.LampRead += t.Sw419LampRead;
            if (t.Sw419Gulped > 0)
            {
                string k = Short(u.Def); a.GulpWho[k] = a.GulpWho.GetValueOrDefault(k) + t.Sw419Gulped; a.GulpedUnits++;
                if (u.LastDeathTurn > 0) a.GulpedLaterFell++;
            }
        }
        if (r.TallyByUnit.TryGetValue("sekki", out var s0))
        {
            a.WrapHits = s0.R396WrapHits; a.WrapAmt = s0.R396WrapAmt; a.Bursts = s0.R396Bursts; a.BurstAmt = s0.R396BurstAmt; a.BurstDealt = s0.R396BurstDealt; a.StoreMax = s0.R396StoreMax;
            a.Gulps = s0.Sw419Gulps; a.GulpAmt = s0.Sw419GulpAmt; a.GulpMax = s0.Sw419GulpMax; a.GulpRest = s0.Sw419GulpRest; a.GulpSpent = s0.Sw419GulpSpent;
            a.BlockHits = s0.Sw419WrapBlockedHits; a.BlockTurns = s0.Sw419WrapBlockedTurns;
            a.FoeGulps = s0.Sw419FoeGulps; a.FoeSpits = s0.Sw419FoeSpits; a.FoeHeld = s0.Sw419FoeHeldTurns; a.SpitDmg = s0.Sw419FoeSpitDmg; a.SpitDealt = s0.Sw419FoeSpitDealt;
            a.SpitKilled = s0.Sw419FoeSpitKilled; a.BurstBlocked = s0.Sw419BurstBlocked; a.FoeNone = s0.Sw419FoeNone;
        }
        foreach (var u in e.DistinctBy(x => x.Def.Id))
            if (r.TallyByUnit.TryGetValue(u.Def.Id, out var t) && t.Sw419FoeGulped > 0) a.FoeWho[u.Def.Name] = a.FoeWho.GetValueOrDefault(u.Def.Name) + t.Sw419FoeGulped;
        return a;
    }

    static Fx Measure(Formation f, I.W w, int n = Seeds, int from = 0, bool verbose = false)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, from + s, verbose));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "mixed" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"sekki419: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        BattleContext.IdleCensus419 = true;   // 計数のみ（盤面は動かない・check の (5) で確かめる）
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "p0": Phase0(); break;
            case "p0c": Phase0C(int.Parse(A(3, "-1"))); break;
            case "boards": BoardsMode(); break;
            case "mixed": Mixed(); break;
            case "veil": Veil(); break;
            case "log": Log(A(3, "a"), A(4, "core4"), A(5, "guard"), int.Parse(A(6, "0"))); return;
            case "check": Check(); break;
            default: Console.WriteLine("sekki419: モードは p0 / p0c / boards / mixed / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // Phase 0 (a)(b)
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        Console.WriteLine("# 第419期 Phase 0 (a)(b) —— 手番の台 × 4 波・規定のセッキ・seed 0..99");
        Console.WriteLine();
        Console.WriteLine("倒れる一撃 ＝ 味方の HP を 0 以下にした `Damage`（中継を除く）。敵の攻撃を型で割る（単体 ／ 範囲 ＝ 薙ぎ・貫き・全体 ／ ほか ＝ 刻み・出どころが敵でないもの）。"
            + "動かなかった味方 ＝ ターンの頭（T2 以降）に `IdleTurn` ＝ 前のターンの生きている味方の延べ数 ／ そのうち差し出した手番（号令が買える）。号令の +8 ＝ 経路 RallyTurn の量 ÷ 8。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 勝率・倒しT | 決着T | 倒れた ／戦 | 倒れる一撃 ／戦（単体 ／ 範囲 ／ ほか）| 包んだ ／戦 | 吸った ／戦 | 弾き返し ／戦 | 弾き返した量 ／戦 | 敵が失った ／戦 | 帳簿の最大 | 動かなかった味方 ／ターン頭 | うち差し出した | 号令の +8 ／戦 |");
        Console.WriteLine("|---|---|---|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (_, name, mk) in Boards.Take(2))
            foreach (var (_, w) in WaveKeys)
            {
                var a = Measure(mk(UnitCatalog.Sekki), w, verbose: true);
                long heads = a.Turns - a.N;   // T2 以降のターン頭（決着T − 1 の和）
                Console.WriteLine($"| {name} | {w.Name} | {a.WT} | {Per1(a.Turns, a.N)} | {Per(a.Fell, a.N)} | {Per(a.LethalSingle + a.LethalAoe + a.LethalOther, a.N)}（{Per(a.LethalSingle, a.N)} ／ {Per(a.LethalAoe, a.N)} ／ {Per(a.LethalOther, a.N)}）| "
                    + $"{Per1(a.WrapHits, a.N)} | {Per1(a.WrapAmt, a.N)} | {Per(a.Bursts, a.N)} | {Per1(a.BurstAmt, a.N)} | {Per1(a.BurstDealt, a.N)} | {a.StoreMax} | {Per(a.IdleHead, heads)} | {Per(a.IdleHeadSold, heads)} | {Per(a.RallyAmt / 8, a.N)} |");
            }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // Phase 0 (c): 第418期の宿題
    // ---------------------------------------------------------------------------------
    static Formation LiliSwap(Formation f, UnitDef lili)
    {
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (o.Def.Id == "lili") g[o.Slot] = lili;
        return g;
    }

    static void Phase0C(int pick)
    {
        var row = CompareBuilds().First(r => r.Name.StartsWith("分かち×逆しま"));
        var f1 = row.F; var f0 = LiliSwap(row.F, UnitCatalog.LiliH417);
        Console.WriteLine($"# 第419期 Phase 0 (c) —— elite {row.Name}（{S307.Seats(f1)}）× 近衛 × seed 0..199（規定のリリ ／ 旧のリリ LiliH417）");
        Console.WriteLine();
        var lost = new List<int>();
        int w0 = 0, w1 = 0;
        for (int s = 0; s < 200; s++)
        {
            var a = EliteDiag.Fight(f0, EliteDiag.Five, EliteDiag.Elite, s); var b = EliteDiag.Fight(f1, EliteDiag.Five, EliteDiag.Elite, s);
            if (a.Won) w0++; if (b.Won) w1++;
            if (a.Won && !b.Won) lost.Add(s);
        }
        Console.WriteLine($"- 勝率 旧 {w0 / 2.0:F1} ／ 規定 {w1 / 2.0:F1}。旧で勝って規定で負けた seed: {string.Join(", ", lost)}");
        if (lost.Count == 0) return;
        int seed = pick >= 0 ? pick : lost[0];
        foreach (var (label, f) in new[] { ("旧のリリ", f0), ("規定のリリ", f1) })
        {
            var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(EliteDiag.Five, EliteDiag.Elite), seed, verbose: true);
            Console.WriteLine();
            Console.WriteLine($"## {label}・seed {seed}: PlayerWon={r.PlayerWon}・T{r.Turns}");
            Console.WriteLine("```");
            foreach (var l in r.Log) Console.WriteLine(new string(' ', l.Indent * 2) + l.Text);
            Console.WriteLine("```");
        }
    }

    // ---------------------------------------------------------------------------------
    // §3-2 ／ §3-3: 台 × 版 × 4 波
    // ---------------------------------------------------------------------------------
    static void BoardsMode()
    {
        Console.WriteLine($"# 第419期 §4 —— 台 × {{{string.Join(", ", Vers.Select(v => v.Label))}}} × {string.Join(" ／ ", WaveKeys.Select(w => w.W.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        var res = new Dictionary<(string, string, string), Fx>();
        foreach (var (bk, _, mk) in Boards)
            foreach (var (wk, w) in WaveKeys)
                foreach (var v in Vers)
                    res[(bk, wk, v.Key)] = Measure(mk(v.D), w);
        foreach (var (bk, name, mk) in Boards)
        {
            Console.WriteLine($"## {name}（{S307.Seats(mk(UnitCatalog.Sekki))}）");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 勝率・倒しT | 規定との差 | 倒れた ／戦 | 最初に倒れたT | 倒れた駒 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|---|");
            foreach (var (wk, w) in WaveKeys)
                foreach (var v in Vers)
                {
                    var a = res[(bk, wk, v.Key)];
                    Console.WriteLine($"| {w.Name} | {v.Label} | {a.WT} | {(v.Key == "0" ? "—" : Delta(a.Win - res[(bk, wk, "0")].Win))} | {Per(a.Fell, a.N)} | {Per1(a.FirstFellT, a.FirstFellN)} | {a.Fall} |");
                }
            Console.WriteLine();
            Console.WriteLine("SW-a: 呑んだ ／戦・誰を（回 ／戦）・一撃の量（平均 ／ 最大）・後で倒れた（呑まれた駒のうち、その戦で倒れた）・失った手番 ／戦・号令が読んだ ／ 灯が読んだ ／戦・休み番のドルガ ／戦・回数切れ ／戦・包めなかったターン ／戦（一撃 ／戦）。");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 呑んだ ／戦 | 誰を | 一撃（平均 ／ 最大）| 後で倒れた | 失った手番 | 号令 ／ 灯 | 休み番ドルガ | 回数切れ | 包めなかったT（一撃）| 包んだ ／戦 | 弾き返し ／戦 | 号令の +8 ／戦 |");
            Console.WriteLine("|---|---|--:|---|---|--:|--:|---|--:|--:|---|--:|--:|--:|");
            foreach (var (wk, w) in WaveKeys)
                foreach (var v in Vers)
                {
                    var a = res[(bk, wk, v.Key)];
                    Console.WriteLine($"| {w.Name} | {v.Label} | {Per(a.Gulps, a.N)} | {Fx.Who(a.GulpWho, a.N)} | {Per1(a.GulpAmt, a.Gulps)} ／ {a.GulpMax} | {Pc(a.GulpedLaterFell, a.GulpedUnits)} | {Per(a.GulpedLost, a.N)} | "
                        + $"{Per(a.RallyRead, a.N)} ／ {Per(a.LampRead, a.N)} | {Per(a.GulpRest, a.N)} | {Per(a.GulpSpent, a.N)} | {Per(a.BlockTurns, a.N)}（{Per(a.BlockHits, a.N)}）| {Per1(a.WrapHits, a.N)} | {Per(a.Bursts, a.N)} | {Per(a.RallyAmt / 8, a.N)} |");
                }
            Console.WriteLine();
            Console.WriteLine("SW-e: 呑んだ ／戦・誰を・吐き出した ／戦（戻る前に決着 ＝ 呑んだ − 吐き出した）・呑んでいたターン頭 ／回・吐き出しの傷（平均）・減った HP ・吐き出しで倒れた・弾き返せなかった ／戦・呑める敵がいなかった（弾き返した）／戦。");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 呑んだ ／戦 | 誰を | 吐き出した ／戦 | 戻る前に決着 | 呑んでいた長さ | 傷（平均）| 減った HP（平均）| 吐き出しで倒れた | 弾き返せなかった ／戦 | 呑めず弾き返した ／戦 |");
            Console.WriteLine("|---|---|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|");
            foreach (var (wk, w) in WaveKeys)
                foreach (var v in Vers.Where(v => v.Key is "e" or "ae"))
                {
                    var a = res[(bk, wk, v.Key)];
                    Console.WriteLine($"| {w.Name} | {v.Label} | {Per(a.FoeGulps, a.N)} | {Fx.Who(a.FoeWho, a.N)} | {Per(a.FoeSpits, a.N)} | {Pc(a.FoeGulps - a.FoeSpits, a.FoeGulps)} | {Per1(a.FoeHeld, a.FoeGulps)} | "
                        + $"{Per1(a.SpitDmg, a.FoeSpits)} | {Per1(a.SpitDealt, a.FoeSpits)} | {Pc(a.SpitKilled, a.FoeSpits)} | {Per(a.BurstBlocked, a.N)} | {Per(a.FoeNone, a.N)} |");
                }
            Console.WriteLine();
        }
        Console.WriteLine("## まとめ（4 波の平均勝率・規定との差）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Vers.Select(v => v.Label)) + " |");
        Console.WriteLine("|---|" + string.Concat(Vers.Select(_ => "--:|")));
        foreach (var (bk, name, _) in Boards)
        {
            double b0 = WaveKeys.Average(w => res[(bk, w.Key, "0")].Win);
            Console.WriteLine($"| {name} | " + string.Join(" | ", Vers.Select(v => { double x = WaveKeys.Average(w => res[(bk, w.Key, v.Key)].Win); return v.Key == "0" ? F1(x) : $"{F1(x)}（{Delta(x - b0)}）"; })) + " |");
        }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §3-4: 寄せ集め（`compare` のセッキを含む行）
    // ---------------------------------------------------------------------------------
    static void Mixed()
    {
        var rows = CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "sekki")).ToArray();
        var waves = L.Mains.Append(I.Guard).Append(I.Bat).ToArray();
        Console.WriteLine($"# 第419期 §3-4 —— `compare` のセッキを含む行（{rows.Length} 行）× {{規定, SW-a, SW-e, SW-ae}} × 本編 第1〜5波 ＋ 近衛 ／ 大隊・seed 0..199");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | " + string.Join(" | ", waves.Select(w => w.Name)) + " | 第2〜5波の平均 | 呑んだ ／戦（本編 5 波）| 敵を呑んだ ／戦（本編 5 波）|");
        Console.WriteLine("|---|---|" + string.Concat(waves.Select(_ => "--:|")) + "--:|--:|--:|");
        foreach (var (name, f) in rows)
        {
            Fx[] b = waves.Select(w => Measure(f, w, 200)).ToArray();
            double m0 = Enumerable.Range(1, 4).Average(i => b[i].Win);
            Console.WriteLine($"| {name} | 規定 | " + string.Join(" | ", b.Select(a => F1(a.Win))) + $" | {F1(m0)} | — | — |");
            foreach (var v in Vers.Skip(1))
            {
                Fx[] x = waves.Select(w => Measure(Swap(f, v.D), w, 200)).ToArray();
                double m1 = Enumerable.Range(1, 4).Average(i => x[i].Win);
                long g = x.Take(5).Sum(a => a.Gulps), fg = x.Take(5).Sum(a => a.FoeGulps);
                Console.WriteLine($"| {name} | {v.Label} | " + string.Join(" | ", x.Select((a, i) => $"{F1(a.Win)}（{Delta(a.Win - b[i].Win)}）")) + $" | {F1(m1)}（{Delta(m1 - m0)}）| {Per(g, 1000)} | {Per(fg, 1000)} |");
            }
        }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §7 の下見: ソムの光の衣（「試遊・感電 光の盾」× 4 波・seed 0..99）
    // ---------------------------------------------------------------------------------
    static void Veil()
    {
        var f = Presets.Playtest.First(r => r.Name == "試遊・感電 光の盾").F;
        Console.WriteLine($"# 第419期 §7 の下見 —— ソムの光の衣（試遊・感電 光の盾・{S307.Seats(f)}）× 4 波・seed 0..99");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率・倒しT | 衣になった ／戦 | 衣が受け止めた ／戦 | 衣が砕けた ／戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        foreach (var (_, w) in WaveKeys)
        {
            long n = 0, wins = 0, winT = 0, added = 0, soaked = 0, broke = 0;
            var parts = new (long Win, long T, long Add, long Soak, long Broke)[Seeds];
            Parallel.For(0, Seeds, s =>
            {
                var (r, p, _) = I.Raw(f, w, s);
                long ad = 0, so = 0, br = 0;
                foreach (var id in p.Select(u => u.Def.Id).Distinct())
                    if (r.TallyByUnit.TryGetValue(id, out var t)) { ad += t.SparkVeilAdded; so += t.SparkVeilSoaked; br += t.VeilBreaks419; }
                parts[s] = (r.PlayerWon ? 1 : 0, r.PlayerWon ? r.Turns : 0, ad, so, br);
            });
            foreach (var x in parts) { n++; wins += x.Win; winT += x.T; added += x.Add; soaked += x.Soak; broke += x.Broke; }
            Console.WriteLine($"| {w.Name} | {F1(100.0 * wins / n)}・{(wins == 0 ? "—" : ((double)winT / wins).ToString("0.0"))}T | {Per1(added, n)} | {Per1(soaked, n)} | {Per(broke, n)} |");
        }
        Console.WriteLine();
    }

    static void Log(string vk, string bk, string wk, int seed)
    {
        // 台の鍵が `row:<行名の頭>` なら `compare` の行（セッキだけ版に替える）。波の鍵は boss ／ guard ／ bat ／ s4 ／ m1〜m5。
        var vd = Vers.First(v => v.Key == vk).D;
        var (bname, f) = bk.StartsWith("row:") ? CompareBuilds().Where(r => r.Name.StartsWith(bk[4..])).Select(r => (r.Name, Swap(r.F, vd))).First() : (Boards.First(b => b.Key == bk).Name, Boards.First(b => b.Key == bk).F(vd));
        var w = wk.StartsWith("m") ? L.Mains[int.Parse(wk[1..]) - 1] : WaveKeys.First(x => x.Key == wk).W;
        var (r, _, _) = I.Raw(f, w, seed, verbose: true);
        Console.WriteLine($"# {bname} × {w.Name} × {Vers.First(v => v.Key == vk).Label}・seed {seed}: PlayerWon={r.PlayerWon}・T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(new string(' ', l.Indent * 2) + l.Text);
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int ok = 0, ng = 0;
        void Ok(string what, bool c) { Console.WriteLine($"- {(c ? "○" : "×")} {what}"); if (c) ok++; else ng++; }
        Console.WriteLine("# sekki419 check");
        Console.WriteLine();
        // (1) 版の定義
        foreach (var v in Vers.Skip(1))
            Ok($"(1) {v.Label}: Id = sekki・数値は規定のまま・規定の札が先頭に並ぶ・`All` ／ `Retired` に入っていない",
                v.D.Id == "sekki" && v.D.MaxHp == UnitCatalog.Sekki.MaxHp && v.D.Attack == UnitCatalog.Sekki.Attack && v.D.Speed == UnitCatalog.Sekki.Speed
                && v.D.Traits.Take(UnitCatalog.Sekki.Traits.Count).SequenceEqual(UnitCatalog.Sekki.Traits) && !UnitCatalog.All.Contains(v.D) && !UnitCatalog.Retired.Contains(v.D));
        Ok("(1) 規定のセッキは呑む札を持たない", !UnitCatalog.Sekki.Traits.Contains(TraitId.SlimeGulpAlly) && !UnitCatalog.Sekki.Traits.Contains(TraitId.SlimeGulpFoe));
        Ok("(1) 呑む札の保持者は `All` に 0 枚", !UnitCatalog.All.Any(d => d.Traits.Contains(TraitId.SlimeGulpAlly) || d.Traits.Contains(TraitId.SlimeGulpFoe)));
        // (2) SW-a が鳴る・呑まれた仲間は倒れない一撃で残る
        var core = Boards.First(b => b.Key == "core4").F;
        var a = Measure(core(UnitCatalog.SekkiSWa), I.Bat, 20);
        Ok($"(2) SW-a は 芯4枚 × 大隊 で呑む（{a.Gulps} 回 ／ 20 戦）・1戦 2 回まで（最大の戦で ≤ 2）", a.Gulps > 0 && Enumerable.Range(0, 20).All(s => One(core(UnitCatalog.SekkiSWa), I.Bat, s).Gulps <= 2));
        // (3) SW-e はボスを呑まない
        var eb = Measure(core(UnitCatalog.SekkiSWe), I.Boss, 20);
        Ok($"(3) SW-e は ボス（勇者だけ）を呑まない（呑んだ {eb.FoeGulps}・呑めず弾き返した {eb.FoeNone}）", eb.FoeGulps == 0);
        // (4) 決定的
        bool same = Enumerable.Range(0, 5).All(s => { var x = One(core(UnitCatalog.SekkiSWae), I.Bat, s); var y = One(core(UnitCatalog.SekkiSWae), I.Bat, s); return x.Wins == y.Wins && x.Turns == y.Turns && x.Gulps == y.Gulps && x.FoeGulps == y.FoeGulps; });
        Ok("(4) SW-ae × 大隊 × seed 0..4 を2回回して同じ", same);
        // (5) 計数の規則（IdleCensus419）は盤面を動かさない
        bool cens = true;
        foreach (var s in Enumerable.Range(0, 10))
        {
            BattleContext.IdleCensus419 = false; var x = I.Raw(core(UnitCatalog.Sekki), I.Guard, s).R;
            BattleContext.IdleCensus419 = true; var y = I.Raw(core(UnitCatalog.Sekki), I.Guard, s).R;
            cens &= x.PlayerWon == y.PlayerWon && x.Turns == y.Turns && x.PlayerSurvivors == y.PlayerSurvivors;
        }
        Ok("(5) 動かなかった味方の計数（`IdleCensus419`）の有無で 芯4枚 × 近衛 × seed 0..9 の勝敗 ／ T ／ 生存が同じ", cens);
        // (6) 試遊の台
        var row = Presets.Versions.FirstOrDefault(r => r.Name == "版・呑み セッキ");
        Ok("(6) 「版・呑み セッキ」が `Presets.Versions` の末尾にあり、並びは 芯4枚（セッキだけ版）", row.Name is not null && Presets.Versions[^1].Name == "版・呑み セッキ"
            && row.F.Occupied().Count() == 5 && row.F.Occupied().All(o => o.Def.Id == "sekki" ? Vers.Skip(1).Any(v => v.D == o.Def) : Presets.FourCore397()[o.Slot] == o.Def));
        Ok("(6) 鎖の終端（`VersionsH407`）は「版・呑み セッキ」を外す", !VersionsH407().Any(r => r.Name == "版・呑み セッキ"));
        Console.WriteLine();
        Console.WriteLine($"○ {ok} ／ × {ng}");
        if (ng > 0) Environment.ExitCode = 1;
    }
}
