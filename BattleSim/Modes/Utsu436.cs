using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;

// =====================================================================================
// utsu436 —— 第436期【軽い期・版 3 つ】逆しまのウツが萎縮を読む（U-d1 一撃が倍 ／ U-d2 一振りが倍 ／ U-d3 怯えを呪いに）。規定は動かさない。
// 指示書は design/PHASE436_UTSU_DAUNT_SPEC.md ／ 報告は design/PHASE436_UTSU_DAUNT.md。
//
//     dotnet run --project BattleSim -c Release 0 utsu436 p0       # §1-1: ウツに萎縮が乗る出どころ（台 1〜4 ＋ `compare` のウツを含む行 × 本編 第2・4・5波）・seed 0..99
//     dotnet run --project BattleSim -c Release 0 utsu436 boards   # §3-2: 4 台 × {規定, U-d1, U-d2, U-d3} × ボス ／ 近衛 ／ 大隊 ／ 本編 第2・4・5波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 utsu436 log <0|d1|d2|d3> <台 1..4> <波 boss|guard|bat|m2|m4|m5> [seed]
//     dotnet run --project BattleSim -c Release 0 utsu436 check    # 自己検査
//
// **予測のファイルが無ければ boards を走らせない**（R397・`.tmp/p436/predict.md`）。
// =====================================================================================
static class Utsu436Diag
{
    const string PredictFile = ".tmp/p436/predict.md";
    const int Seeds = 100;
    const string PlayRow = "版・萎縮 ウツ";

    static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("0", "規定", UnitCatalog.Utsu), ("d1", "U-d1", UnitCatalog.UtsuUd1), ("d2", "U-d2", UnitCatalog.UtsuUd2), ("d3", "U-d3", UnitCatalog.UtsuUd3),
    };
    static readonly I.W[] Waves = { I.Boss, I.Guard, I.Bat, I.M2, I.M4, I.M5 };
    static readonly string[] WaveKeys = { "boss", "guard", "bat", "m2", "m4", "m5" };
    static readonly string[] SrcNames = { "味方のクビの漏れ", "敵のクビ", "感電の萎縮", "痺れ慣れ", "バネ" };

    /// <summary>台 4（§3-2）: 試遊・呪い 席替え。前1 ウツ ／ 前3 ムド ／ 中央 ネル ／ 後1 クビ ／ 後3 ワタ——クビの隣がウツとネル（殴らない）だけ。</summary>
    internal static Formation Board4(UnitDef utsu) =>
        Formation.Build(front1: utsu, front3: UnitCatalog.Mudo, center: UnitCatalog.Nel, back1: UnitCatalog.Kubi, back3: UnitCatalog.Wata);

    internal static List<(string Name, Formation F)> Boards() => new()
    {
        ("逆しま改 (クビ×ウツ)", RowOf("逆しま改 (クビ×ウツ)")),
        ("逆しま+後備え", RowOf("逆しま+後備え")),
        ("試遊・呪い（陰性対照）", RowOf("試遊・呪い")),
        ("試遊・呪い 席替え", Board4(UnitCatalog.Utsu)),
    };
    static Formation RowOf(string name) => CompareBuilds().Concat(Presets.Playtest).Concat(Presets.Versions).First(r => r.Name == name).F;
    internal static Formation Swap(Formation f, UnitDef utsu)
    {
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (o.Def.Id == "utsu") g[o.Slot] = utsu;
        return g;
    }

    static string Short(UnitDef d) => S307.Short(d);
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F0") + "%";
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");

    sealed class Fx
    {
        public long N, Wins, WinT, YokeHits, YokeLost, TurnMaxSum, TurnMax, TeamTurnMax, Over1200;
        public UnitTally Utsu = new();
        public readonly Dictionary<string, long> Death = new(), DeathT = new();
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; YokeHits += o.YokeHits; YokeLost += o.YokeLost; Utsu.Add(o.Utsu);
            TurnMaxSum += o.TurnMaxSum; TurnMax = Math.Max(TurnMax, o.TurnMax); TeamTurnMax = Math.Max(TeamTurnMax, o.TeamTurnMax); Over1200 += o.Over1200;
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
        public double WinTurns => Wins == 0 ? double.NaN : (double)WinT / Wins;
        public string WT => Wins == 0 ? F1(Win) : $"{F1(Win)}・{WinTurns:0.0}T";
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).Take(5)
            .Select(kv => $"{kv.Key} {Pc(kv.Value, N)}・T{Per1(DeathT[kv.Key], kv.Value)}"));
        public long Src(int i) => Utsu.Dn436From is { } a ? a[i] : 0;
    }

    static Fx One(Formation f, I.W w, int seed)
    {
        bool v = w.Boss;   // ボスの波だけ台本を読む（1 手番の打点）
        var (r, p, _) = I.Raw(f, w, seed, verbose: v);
        var a = new Fx { N = 1 };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { string k = Short(u.Def); a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + u.LastDeathTurn; }
        if (r.TallyByUnit.TryGetValue("utsu", out var s)) a.Utsu.Add(s);
        if (r.Yoke.CutBy.TryGetValue("utsu", out var y)) { a.YokeHits = y.Hits; a.YokeLost = y.Lost; }
        if (v)
        {
            var ut = p.FirstOrDefault(u => u.Def.Id == "utsu");
            var pIds = p.Select(u => u.InstanceId).ToHashSet();
            var mine = new Dictionary<int, long>(); var team = new Dictionary<int, long>();
            foreach (var ev in r.Events)
            {
                if (ev.Kind != BattleEventKind.Damage || ev.ActorId is not int aid || !pIds.Contains(aid) || ev.TargetId is not int tid || pIds.Contains(tid)) continue;
                team[ev.Turn] = team.GetValueOrDefault(ev.Turn) + ev.Amount;
                if (ut is not null && aid == ut.InstanceId) mine[ev.Turn] = mine.GetValueOrDefault(ev.Turn) + ev.Amount;
            }
            long mx = mine.Count == 0 ? 0 : mine.Values.Max();
            a.TurnMaxSum = mx; a.TurnMax = mx;
            a.TeamTurnMax = team.Count == 0 ? 0 : team.Values.Max();
            a.Over1200 = mine.Values.Count(x => x > 1200);
        }
        return a;
    }

    static Fx Measure(Formation f, I.W w, int n = Seeds)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, s));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode == "boards" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"utsu436: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "p0": Phase0(); break;
            case "boards": BoardsRun(); break;
            case "log": Log(A(3, "d2"), int.Parse(A(4, "4")), A(5, "guard"), int.Parse(A(6, "0"))); break;
            case "check": Check(); break;
            default: Console.WriteLine("utsu436: モードは p0 / boards / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void Phase0()
    {
        Console.WriteLine($"# 第436期 Phase 0 —— ウツに萎縮が乗る出どころ（規定・seed 0..{Seeds - 1}・1 戦あたり）");
        Console.WriteLine();
        var rows = Boards().Select(b => (b.Name, b.F)).ToList();
        foreach (var r in CompareBuilds())
            if (r.F.Occupied().Any(o => o.Def.Id == "utsu") && !rows.Any(x => x.Name == r.Name)) rows.Add((r.Name, r.F));
        I.W[] ws = { I.Boss, I.Guard, I.Bat, I.M2, I.M4, I.M5 };
        Console.WriteLine($"| 行 | 並び | 波 | {string.Join(" | ", SrcNames)} | 萎縮して振った一撃 | 削られた量 | 1 撃あたり |");
        Console.WriteLine($"|---|---|---|{string.Concat(SrcNames.Select(_ => "--:|"))}--:|--:|--:|");
        foreach (var (name, f) in rows)
            foreach (var w in ws)
            {
                var x = Measure(f, w);
                var u = x.Utsu;
                Console.WriteLine($"| {name} | {S307.Seats(f)} | {w.Name} | {string.Join(" | ", Enumerable.Range(0, SrcNames.Length).Select(i => Per1(x.Src(i), x.N)))} | {Per1(u.DauntedSwings, x.N)} | {Per1(u.DauntedCut, x.N)} | {(u.DauntedSwings == 0 ? "—" : ((double)u.DauntedCut / u.DauntedSwings).ToString("F1"))} |");
            }
        Console.WriteLine();
    }

    static void BoardsRun()
    {
        var boards = Boards();
        var res = new Dictionary<(int B, int W, string V), Fx>();
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
                foreach (var v in Vers)
                    res[(bi, wi, v.Key)] = Measure(Swap(b.F, v.D), w);

        Console.WriteLine($"# 第436期 —— 逆しまのウツが萎縮を読む（seed 0..{Seeds - 1}）");
        Console.WriteLine();
        foreach (var (b, i) in boards.Select((b, i) => (b, i))) Console.WriteLine($"- 台 {i + 1}. {b.Name}（{S307.Seats(b.F)}）");
        Console.WriteLine();

        Console.WriteLine("## 表1 勝率・倒しT（規定との差）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定 | U-d1 | U-d2 | U-d3 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
            {
                var d = res[(bi, wi, "0")];
                string C(string k) { var x = res[(bi, wi, k)]; return $"{x.WT}（{Delta(x.Win - d.Win)}）"; }
                Console.WriteLine($"| {bi + 1} | {w.Name} | {d.WT} | {C("d1")} | {C("d2")} | {C("d3")} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 表2 ウツの萎縮（1 戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 萎縮した回数 | 萎縮を読んだ一撃（規定は半分の一撃） | 倍で足した量 ／ 規定は削られた量 | U-d2 の一振り | U-d3 の下げ幅 | 軛で切られた一撃 ／ 量（ウツの全発） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|---|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
                foreach (var v in Vers)
                {
                    var x = res[(bi, wi, v.Key)]; var u = x.Utsu; long n = x.N;
                    long got = Enumerable.Range(0, SrcNames.Length).Sum(x.Src);
                    string sw = v.Key == "0" ? Per1(u.DauntedSwings, n) : Per1(u.Ud436Swings, n);
                    string amt = v.Key == "0" ? "−" + Per1(u.DauntedCut, n) : "+" + Per1(u.Ud436Added, n);
                    Console.WriteLine($"| {bi + 1} | {w.Name} | {v.Label} | {Per1(got, n)} | {sw} | {amt} | {Per1(u.Ud436Volleys, n)} | {Per1(u.Ud436Curse, n)} | {Per1(x.YokeHits, n)} ／ {Per1(x.YokeLost, n)} |");
                }
        Console.WriteLine();

        Console.WriteLine("## 表3 ボス: ウツの 1 手番の最大打点（1 戦の最大の平均 ／ 全戦の最大 ／ 1,200 超えの手番）・味方全体の 1 ターンの最大");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率 | ウツの 1 手番の最大（平均 ／ 最大） | 1,200 超え | 味方全体の 1 ターンの最大 |");
        Console.WriteLine("|---|---|---|---|--:|--:|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var v in Vers)
            {
                var x = res[(bi, 0, v.Key)];
                Console.WriteLine($"| {bi + 1} | {v.Label} | {x.WT} | {Per1(x.TurnMaxSum, x.N)} ／ {x.TurnMax} | {x.Over1200} | {x.TeamTurnMax} |");
            }
        Console.WriteLine();

        Console.WriteLine("## 表4 倒れた味方（割合・平均の倒れたT・上位 5）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 倒れた味方 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
                foreach (var v in Vers) Console.WriteLine($"| {bi + 1} | {w.Name} | {v.Label} | {res[(bi, wi, v.Key)].Fall} |");
        Console.WriteLine();

        Console.WriteLine("## 表5 台 4（席替え）の規定 − 台 3 の規定（席替えだけで動く量）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 台 3 規定 | 台 4 規定 | 差 |");
        Console.WriteLine("|---|---|---|--:|");
        foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
            Console.WriteLine($"| {w.Name} | {res[(2, wi, "0")].WT} | {res[(3, wi, "0")].WT} | {Delta(res[(3, wi, "0")].Win - res[(2, wi, "0")].Win)} |");
        Console.WriteLine();

        Console.WriteLine("## §5 の線");
        Console.WriteLine();
        bool same3 = Vers.Skip(1).All(v => Enumerable.Range(0, Waves.Length).All(wi => res[(2, wi, v.Key)].Win == res[(2, wi, "0")].Win && res[(2, wi, v.Key)].WinT == res[(2, wi, "0")].WinT));
        Console.WriteLine($"- 線1 台 3 の全セルが規定と同じ（勝率 ／ 倒しT）: {(same3 ? "同じ" : "動いた")}");
        foreach (var v in Vers.Skip(1))
        {
            var ups = new List<string>(); int up5 = 0, down10 = 0; var half = new List<string>(); var boss1T = new List<string>();
            foreach (int bi in new[] { 0, 1, 3 })
            {
                foreach (int wi in new[] { 1, 2 })
                {
                    double dd = res[(bi, wi, v.Key)].Win - res[(bi, wi, "0")].Win;
                    ups.Add($"台{bi + 1} {Waves[wi].Name} {Delta(dd)}");
                    if (dd >= 5) up5++;
                }
                for (int wi = 0; wi < Waves.Length; wi++)
                {
                    var x = res[(bi, wi, v.Key)]; var d = res[(bi, wi, "0")];
                    if (x.Win - d.Win <= -10) down10++;
                    if (!double.IsNaN(x.WinTurns) && !double.IsNaN(d.WinTurns) && x.WinTurns <= d.WinTurns / 2) half.Add($"台{bi + 1} {Waves[wi].Name}");
                }
                var bx = res[(bi, 0, v.Key)];
                if (bx.Wins > 0 && bx.WinTurns <= 1.0) boss1T.Add($"台{bi + 1}");
            }
            long over = Enumerable.Range(0, 4).Sum(bi => res[(bi, 0, v.Key)].Over1200);
            Console.WriteLine($"- {v.Label}: 線2 精鋭 {string.Join(" ／ ", ups)}（+5 以上 {up5} セル・−10 以下 {down10} セル・{(up5 >= 2 && down10 == 0 ? "通る" : "通らない")}）・線3 ボス 1T {(boss1T.Count == 0 ? "なし" : string.Join("・", boss1T))} ／ 倒しT が半分以下 {(half.Count == 0 ? "なし" : string.Join("・", half))}・線4 1,200 超えの手番 {over}");
        }
        Console.WriteLine();
    }

    static I.W WaveOf(string k) => Waves[Array.IndexOf(WaveKeys, k)];

    static void Log(string vk, int board, string wk, int seed)
    {
        var v = Vers.First(x => x.Key == vk);
        var b = Boards()[board - 1];
        var (r, _, _) = I.Raw(Swap(b.F, v.D), WaveOf(wk), seed, verbose: true);
        Console.WriteLine($"# {b.Name} × {WaveOf(wk).Name} × {v.Label}・seed {seed}: PlayerWon={r.PlayerWon}・T{r.Turns}");
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }

    static void Check()
    {
        bool ok = true;
        void Req(bool c, string what) { Console.WriteLine($"{(c ? "○" : "×")} {what}"); ok &= c; }
        var ut = UnitCatalog.Utsu;
        var news = new[] { TraitId.UtsuDauntDouble, TraitId.UtsuDauntVolley, TraitId.UtsuDauntCurse };
        foreach (var (v, t) in Vers.Skip(1).Zip(news))
            Req(v.D.Traits.SequenceEqual(ut.Traits.Append(t)) && v.D.MaxHp == ut.MaxHp && v.D.Attack == ut.Attack && v.D.Speed == ut.Speed && v.D.Id == ut.Id, $"{v.Label} ＝ 規定のウツ ＋ {t}（HP ／ 攻 ／ 速 ／ Id は同じ）");
        Req(!UnitCatalog.Everyone.Any(d => d.Traits.Any(news.Contains)), "新しい札の保持者は `All` ／ `Retired` に 0 枚");
        Req(!Presets.Playtest.Concat(CompareBuilds()).Concat(CrossBuilds()).Any(r => r.F.Occupied().Any(o => o.Def.Traits.Any(news.Contains))), "`Playtest` ／ `compare` ／ 交差帯に新しい札の駒はいない");
        Req(Presets.Versions.Count(r => r.Name == PlayRow) == 1 && !VersionsH407().Any(r => r.Name == PlayRow), $"試遊の台「{PlayRow}」は Versions に 1 つ・鎖の終端 VersionsH407 が外す");
        var play = Presets.Versions.FirstOrDefault(r => r.Name == PlayRow).F;
        Req(play is not null && Enumerable.Range(0, 5).All(i => ReferenceEquals(play[i], Board4(PlayDef)[i])), $"「{PlayRow}」＝ 台 4（席替え）のウツを {PlayLabel} に替えた並び");

        var boards = Boards();
        bool Adj(Formation f, string a, string b) { var x = f.Occupied().First(o => o.Def.Id == a).Slot; var y = f.Occupied().First(o => o.Def.Id == b).Slot; return FormationRules.AreAdjacent(x, y); }
        Req(Adj(boards[0].F, "kubi", "utsu") && Adj(boards[1].F, "kubi", "utsu"), "台 1 ／ 台 2 はクビとウツが隣");
        Req(!Adj(boards[2].F, "kubi", "utsu"), "台 3（試遊・呪い）はクビとウツが隣でない");
        var f4 = boards[3].F; int ks = f4.Occupied().First(o => o.Def.Id == "kubi").Slot;
        var nb = f4.Occupied().Where(o => o.Slot != ks && FormationRules.AreAdjacent(ks, o.Slot)).Select(o => o.Def.Id).OrderBy(s => s, StringComparer.Ordinal).ToList();
        Req(nb.SequenceEqual(new[] { "nel", "utsu" }), $"台 4 のクビの隣はウツとネルだけ（{string.Join("・", nb)}）");

        bool same = true, defNone = true, stops = true, curse = true, volley = true, b3 = true;
        long d3Swings = 0, d2Volleys = 0;
        foreach (var (b, bi) in boards.Select((b, i) => (b, i)))
            foreach (var (w, wi) in Waves.Select((w, i) => (w, i)))
                for (int s = 0; s < 8; s++)
                    foreach (var v in Vers)
                    {
                        var f = Swap(b.F, v.D);
                        var (r0, _, _) = I.Raw(f, w, s);
                        var (r1, _, _) = I.Raw(f, w, s, verbose: true);
                        if (r0.PlayerWon != r1.PlayerWon || r0.Turns != r1.Turns) same = false;
                        var t = r0.TallyByUnit.GetValueOrDefault("utsu") ?? new UnitTally();
                        if (v.Key == "0" && t.Ud436Swings + t.Ud436Added + t.Ud436Curse + t.Ud436Volleys != 0) defNone = false;
                        if (v.Key != "0" && t.DauntedSwings != 0) stops = false;   // 版のウツは半分にならない
                        if (v.Key != "d2" && t.Ud436Volleys != 0) volley = false;
                        if (v.Key != "d3" && t.Ud436Curse != 0) curse = false;
                        if (v.Key == "d3") { if (t.Ud436Curse != t.Ud436Swings * BattleContext.UtsuDauntCurse436 || t.Ud436Added != 0) curse = false; d3Swings += t.Ud436Swings; }
                        if (v.Key == "d2") d2Volleys += t.Ud436Volleys;
                        if (bi == 2 && v.Key != "0" && t.Ud436Swings != 0) b3 = false;
                    }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（4 台 × 6 波 × 4 版 × seed 0..7）");
        Req(defNone, "規定のウツは版の計数が 0");
        Req(stops, "版のウツは萎縮で半分にならない（DauntedSwings ＝ 0）");
        Req(volley && d2Volleys > 0, $"U-d2 の一振りは U-d2 にだけ立つ（{d2Volleys} 回）");
        Req(curse && d3Swings > 0, $"U-d3 の下げ幅は 1 回ごとにちょうど 3（ワタ ／ 横取りに拾われない・{d3Swings} 回）・倍は足さない");
        Req(b3, "台 3 のウツは萎縮を 1 度も読まない");
        Console.WriteLine(ok ? "utsu436 check: すべて ○" : "utsu436 check: × あり");
        if (!ok) Environment.ExitCode = 1;
    }

    /// <summary>試遊の台「版・萎縮 ウツ」に載せた版（§3-4・boards の結果で決める）。</summary>
    static UnitDef PlayDef => UnitCatalog.UtsuUd2;
    const string PlayLabel = "U-d2";
}
