using BattleCore;
using static Common;
using I = Inv348Diag;
using S307 = Som307Diag;

// =====================================================================================
// lili416 —— 第416期【軽い期・版だけ】リリの駆け寄る口づけ（LK-1 ／ LK-2 ／ LK-1m）。
// 指示書は design/PHASE415_LILI_RUSH_SPEC.md（期の番号は第416期・第415期は表示専用の期に使われていた）／ 報告は design/PHASE416_LILI_RUSH.md。
//
//     dotnet run --project BattleSim -c Release 0 lili416 p0       # Phase 0 §1: 台 × {LK-1, LK-2} × 4 波・seed 0..9 の引き金の数 ／ 上限で切られた数 ／ 粛（本編 第2波）
//     dotnet run --project BattleSim -c Release 0 lili416 boards   # §4-2: 台 × {規定, LK-1, LK-2, LK-1m} × ボス ／ 近衛 ／ 大隊 ／ 強4 ＋ 本編 5 波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 lili416 seats    # §4-3: 台・呪い ／ 反撃改 × リリの席 5 通り × {規定, LK-1} × 4 波
//     dotnet run --project BattleSim -c Release 0 lili416 log <0|1|2|1m> <台の鍵> <波の鍵> [seed]   # 1戦のログ
//     dotnet run --project BattleSim -c Release 0 lili416 check    # 自己検査
//
// **予測のファイルが無ければ boards ／ seats を走らせない**（R397・`.tmp/p416/predict.md`）。
// =====================================================================================
static class Lili416Diag
{
    const string PredictFile = ".tmp/p416/predict.md";
    const int Seeds = 100;

    static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("0", "規定", UnitCatalog.Lili), ("1", "LK-1", UnitCatalog.LiliLk1), ("2", "LK-2", UnitCatalog.LiliLk2), ("1m", "LK-1m", UnitCatalog.LiliLk1m),
    };

    /// <summary>`compare` の行（無ければ交差帯 `Presets.Cross`——「傷×被弾」は交差帯の行）。</summary>
    static Formation CompareRow(string head) => CompareBuilds().Concat(Presets.Cross).First(r => r.Name.StartsWith(head)).F;
    static Formation VersionRow(string name) => Presets.Versions.First(r => r.Name == name).F;

    /// <summary>台（§4-2）。鍵 ／ 名前 ／ 並び（規定のリリ）。</summary>
    internal static readonly (string Key, string Name, Func<Formation> F)[] Boards =
    {
        ("curse", "台・呪い", () => Presets.LiliCurse416()),
        ("guren", "台・紅", () => Presets.LiliGuren416()),
        ("self", "試遊・自傷", () => VersionRow("試遊・自傷")),
        ("taikyu", "耐久 (ガルド×リリ)", () => CompareRow("耐久 (ガルド×リリ)")),
        ("hangeki", "反撃 (ヒサ×カド)", () => CompareRow("反撃 (ヒサ×カド)")),
        ("hangeki2", "反撃改 (ドハ×カド)", () => CompareRow("反撃改 (ドハ×カド)")),
        ("kizu", "傷×被弾 (カド×ノミ×リリ)・交差帯", () => CompareRow("傷×被弾 (カド×ノミ×リリ)")),
        ("nensho", "燃焼 (ボルグ×ホタ)", () => CompareRow("燃焼 (ボルグ×ホタ)")),
    };

    internal static I.W MainWave(int i) =>
        new($"本編 第{i}波", () => BattleEngine.Materialize(EnemyCatalog.Stages[i - 1].Enemy, BattleContext.EnemyTeam, EnemyScaleRule.Default), EnemyScaleRule.Default);
    internal static readonly I.W[] Hard = { I.Boss, I.Guard, I.Bat, I.StrongWaves[2] };
    internal static readonly I.W[] Mains = Enumerable.Range(1, 5).Select(MainWave).ToArray();
    internal static readonly (string Key, I.W W)[] WaveKeys =
        new[] { ("boss", I.Boss), ("guard", I.Guard), ("bat", I.Bat), ("s4", I.StrongWaves[2]) }
        .Concat(Enumerable.Range(1, 5).Select(i => ($"m{i}", Mains[i - 1]))).ToArray();

    internal static Formation Swap(Formation f, UnitDef lili)
    {
        var g = f.Clone();
        foreach (var o in f.Occupied()) if (o.Def.Id == "lili") g[o.Slot] = lili;
        return g;
    }

    static string Short(UnitDef d) => S307.Short(d);
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F0") + "%";

    // ---------------------------------------------------------------------------------
    // 1戦の計数
    // ---------------------------------------------------------------------------------
    internal sealed class Fx
    {
        public long N, Wins, WinT, Turns, Fell, LiliDied, LiliDeathT, Tier1T, Tier1N, RiteT, RiteN, RiteFires;   // 第417期: RiteFires（1戦の儀式の回数）
        public long HealTurn, HealRite, HealReturn, HealRush, Leak, UtsuDmg, UtsuGot, BeniInverse;
        public long TurnMoves, TurnLayers, TurnMovesFront, TurnStealNeg, TurnStealPos, TurnStealNegFront, KissActs, KissPain;
        public long RushMovesFront, RushMovesReader, RushStealNegFront, RushStealNegReader;
        public readonly KissRushBook Rush = new();
        public readonly Dictionary<string, long> Death = new(), DeathT = new();
        public readonly Dictionary<string, long> MovedKind = new();   // 駆け寄りで移した状態のキー → 件数
        public void Add(Fx o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Turns += o.Turns; Fell += o.Fell; LiliDied += o.LiliDied; LiliDeathT += o.LiliDeathT;
            Tier1T += o.Tier1T; Tier1N += o.Tier1N; RiteT += o.RiteT; RiteN += o.RiteN; RiteFires += o.RiteFires;
            HealTurn += o.HealTurn; HealRite += o.HealRite; HealReturn += o.HealReturn; HealRush += o.HealRush; Leak += o.Leak;
            UtsuDmg += o.UtsuDmg; UtsuGot += o.UtsuGot; BeniInverse += o.BeniInverse;
            TurnMoves += o.TurnMoves; TurnLayers += o.TurnLayers; TurnMovesFront += o.TurnMovesFront; TurnStealNeg += o.TurnStealNeg; TurnStealPos += o.TurnStealPos;
            TurnStealNegFront += o.TurnStealNegFront; KissActs += o.KissActs; KissPain += o.KissPain;
            RushMovesFront += o.RushMovesFront; RushMovesReader += o.RushMovesReader; RushStealNegFront += o.RushStealNegFront; RushStealNegReader += o.RushStealNegReader;
            Rush.Add(o.Rush);
            foreach (var (k, v) in o.Death) Death[k] = Death.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.DeathT) DeathT[k] = DeathT.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.MovedKind) MovedKind[k] = MovedKind.GetValueOrDefault(k) + v;
        }
        public string WT => $"{F1(N == 0 ? 0 : 100.0 * Wins / N)}・{(Wins == 0 ? "—" : ((double)WinT / Wins).ToString("0.0"))}T";
        public long HealAll => HealTurn + HealRite + HealReturn + HealRush;
        public string Fall => Death.Count == 0 ? "—" : string.Join("、", Death.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key} {Pc(kv.Value, N)}・T{Per1(DeathT[kv.Key], kv.Value)}"));
    }

    /// <summary>読み手（第416期 §3 の表）: ウツ（逆しま）・ムド（呪いで重くなる泥）・ボルグ（燃える体）・ベニの隣（紅の輪・ベニ自身を含む）。</summary>
    internal static HashSet<string> ReadersOf(Formation f)
    {
        var r = new HashSet<string>();
        foreach (var (slot, d) in f.Occupied())
        {
            if (d.Id is "utsu" or "borg" or "mudo") r.Add(d.Id);   // ムドは呪いの読み手（呪われているほど泥が重い）
            if (d.Id == "beni")
            {
                r.Add("beni");
                foreach (var (s2, d2) in f.Occupied()) if (s2 != slot && FormationRules.AreAdjacent(slot, s2)) r.Add(d2.Id);
            }
        }
        return r;
    }
    internal static HashSet<string> FrontOf(Formation f) => f.Occupied().Where(o => o.Slot is 0 or 1).Select(o => o.Def.Id).ToHashSet();

    internal static Fx One(Formation f, I.W w, int seed)
    {
        var (r, p, e) = I.Raw(f, w, seed, verbose: false);
        var a = new Fx { N = 1, Turns = r.Turns };
        if (r.PlayerWon) { a.Wins = 1; a.WinT = r.Turns; }
        a.Fell = p.Count(u => u.LastDeathTurn > 0);
        foreach (var u in p.Where(u => u.LastDeathTurn > 0)) { string k = Short(u.Def); a.Death[k] = a.Death.GetValueOrDefault(k) + 1; a.DeathT[k] = a.DeathT.GetValueOrDefault(k) + u.LastDeathTurn; }
        var lili = p.FirstOrDefault(u => u.Def.Id == "lili");
        if (lili is not null && lili.LastDeathTurn > 0) { a.LiliDied = 1; a.LiliDeathT = lili.LastDeathTurn; }
        var front = FrontOf(f); var readers = ReadersOf(f);
        if (r.TallyByUnit.TryGetValue("lili", out var t))
        {
            a.HealTurn = t.KissHealed; a.HealRite = t.RiteHealed; a.HealReturn = t.ReturnHealed; a.Leak = t.InverseLeakBy;
            a.KissActs = t.KissActs; a.KissPain = t.KissPainSum;
            if (t.KissTierTurn is { } tt && tt[0] > 0) { a.Tier1T = tt[0]; a.Tier1N = 1; }
            if (t.RiteFirstTurn > 0) { a.RiteT = t.RiteFirstTurn; a.RiteN = 1; }
            a.RiteFires = t.RiteFires;
            var rb = t.KissRush416;
            if (rb is not null) { a.Rush.Add(rb); a.HealRush = rb.Healed; }
            // 手番の移し ＝ 全体 − 駆け寄りの分。
            if (t.KissMovedBy is { } mb)
                foreach (var (k, v) in mb)
                {
                    long rn = rb is not null && rb.MovedBy.TryGetValue(k, out var rv) ? rv.N : 0, rl = rb is not null && rb.MovedBy.TryGetValue(k, out var rv2) ? rv2.Sum : 0;
                    long n = v.N - rn; a.TurnMoves += n; a.TurnLayers += v.Sum - rl;
                    if (front.Contains(k.Split('|')[1])) a.TurnMovesFront += n;
                }
            if (t.KissStolenBy is { } sb)
                foreach (var (id, v) in sb)
                {
                    long rs = rb is not null && rb.StolenBy.TryGetValue(id, out var rv) ? rv.Sum : 0;
                    long s = v.Sum - rs;
                    if (s < 0) { a.TurnStealNeg += -s; if (front.Contains(id)) a.TurnStealNegFront += -s; } else a.TurnStealPos += s;
                }
            if (rb is not null)
            {
                foreach (var (k, v) in rb.MovedBy)
                {
                    var parts = k.Split('|');
                    a.MovedKind[parts[0]] = a.MovedKind.GetValueOrDefault(parts[0]) + v.N;
                    if (front.Contains(parts[1])) a.RushMovesFront += v.N;
                    if (readers.Contains(parts[1])) a.RushMovesReader += v.N;
                }
                foreach (var (id, v) in rb.StolenBy)
                    if (v.Sum < 0) { if (front.Contains(id)) a.RushStealNegFront += -v.Sum; if (readers.Contains(id)) a.RushStealNegReader += -v.Sum; }
            }
            // ウツが受け取った攻撃力の上げ下げ（手番 ＋ 駆け寄り・符号つき）。
            if (t.KissStolenBy is { } sb2 && sb2.TryGetValue("utsu", out var ug)) a.UtsuGot = ug.Sum;
        }
        if (r.TallyByUnit.TryGetValue("utsu", out var tu)) a.UtsuDmg = tu.DamageToEnemy;
        if (r.TallyByUnit.TryGetValue("beni", out var tb)) a.BeniInverse = tb.InverseNominal;
        return a;
    }

    internal static Fx Measure(Formation f, I.W w, int n = Seeds, int from = 0)
    {
        var parts = new Fx[n];
        Parallel.For(0, n, s => parts[s] = One(f, w, from + s));
        var all = new Fx();
        foreach (var x in parts) all.Add(x);
        return all;
    }

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "seats" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"lili416: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "p0": Phase0(); break;
            case "boards": BoardsMode(); break;
            case "seats": Seats(); break;
            case "log": Log(A(3, "1"), A(4, "curse"), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "check": Check(); break;
            default: Console.WriteLine("lili416: モードは p0 / boards / seats / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // Phase 0 §1: 引き金の数
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        Console.WriteLine("# 第416期 Phase 0 §1 —— 引き金の数（台 × {LK-1, LK-2} × ボス ／ 近衛 ／ 大隊 ／ 強4 ＋ 本編 第2波（粛）・seed 0..9）");
        Console.WriteLine();
        Console.WriteLine("引き金 ＝ 生きている敵の直の一撃で味方の HP が減り、その味方が生きていた（`Chances`）。／T は決着T で割る。切られた ＝ そのターンの上限で鳴らなかった ／ 止まった ＝ 割り込み・反撃の中 ／ 痺れ・組み付き。");
        Console.WriteLine("倒れた ＝ その一撃で味方が倒れた（引き金に入れない）。粛 ＝ 粛で止まった（引き金に対する割合）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 決着T | 引き金 ／戦 | 引き金 ／T | 鳴った ／戦 | 切られた | 止まった | 粛 | 倒れた ／戦 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        var waves = Hard.Append(Mains[1]).ToArray();
        foreach (var (_, name, mk) in Boards)
            foreach (var w in waves)
                foreach (var v in Vers.Where(v => v.Key is "1" or "2"))
                {
                    var a = Measure(Swap(mk(), v.D), w, 10);
                    var b = a.Rush;
                    Console.WriteLine($"| {name} | {w.Name} | {v.Label} | {Per1(a.Turns, a.N)} | {Per1(b.Chances, a.N)} | {Per(b.Chances, a.Turns)} | {Per1(b.Fired, a.N)} | {Pc(b.Capped, b.Chances)} | {Pc(b.Held, b.Chances)} | {Pc(b.Hushed, b.Chances)} | {Per1(b.Fallen, a.N)} |");
                }
    }

    // ---------------------------------------------------------------------------------
    // §4-2 台 × 版 × 波
    // ---------------------------------------------------------------------------------
    static void BoardsMode()
    {
        Console.WriteLine($"# 第416期 §4-2 —— 台 × {{{string.Join(", ", Vers.Select(v => v.Label))}}} × {string.Join(" ／ ", Hard.Select(w => w.Name))} ＋ 本編 第1〜5波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("**表A**（勝ち方）: 勝率・倒しT ／ 倒れた味方 ／戦 ／ リリが倒れた割合・T ／ 倒れた駒（割合・平均T）。");
        Console.WriteLine();
        Console.WriteLine("**表B**（駆け寄り）: 鳴った ／戦 ／ ／T ／ 切られた ／ 粛 ／ 吸えた ／戦 ／ 注いで癒えた ／戦 ／ 支援拒否で捨てた（回 ／ 量）／ 痛みの重なり ／戦 ／ 殴られた味方（回 ／戦）。");
        Console.WriteLine();
        Console.WriteLine("**表C**（回復の内訳と移し）: 手番 ／ 儀式 ／ 還る ／ 駆け寄り（癒えた量 ／戦）／ **手番外の割合**（(駆け寄り ＋ 還る) ÷ 4本）／ 注ぎが傷になった ／戦（`InverseLeakBy`・ベニの輪）／ "
            + "駆け寄りの移し 件 ／戦（矢面 ／ 読み手の割合）／ 手番の移し 件 ／戦（矢面の割合）／ 弱体の移し 駆け寄り ／ 手番（−／戦 ＝ 敵が取り戻した攻撃力）／ 段1 T ／ 儀式の初回 T ／ 手番の頭の痛み ／手番。");
        Console.WriteLine();
        var waves = Hard.Concat(Mains).ToArray();
        var res = new Dictionary<(string, string, string), Fx>();
        foreach (var (bk, name, mk) in Boards)
            foreach (var w in waves)
                foreach (var v in Vers)
                    res[(bk, w.Name, v.Key)] = Measure(Swap(mk(), v.D), w);

        foreach (var (bk, name, mk) in Boards)
        {
            var f0 = mk();
            Console.WriteLine($"## {name}（{S307.Seats(f0)}）・矢面 {string.Join("・", FrontOf(f0).Select(id => Short(UnitCatalog.ById(id))))} ／ 読み手 {(ReadersOf(f0).Count == 0 ? "なし" : string.Join("・", ReadersOf(f0).Select(id => Short(UnitCatalog.ById(id)))))}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 勝率・倒しT | 倒れた ／戦 | リリ倒れ・T | 倒れた駒 |");
            Console.WriteLine("|---|---|---|--:|---|---|");
            foreach (var w in waves)
                foreach (var v in Vers)
                {
                    var a = res[(bk, w.Name, v.Key)];
                    Console.WriteLine($"| {w.Name} | {v.Label} | {a.WT} | {Per(a.Fell, a.N)} | {Pc(a.LiliDied, a.N)}・T{Per1(a.LiliDeathT, a.LiliDied)} | {a.Fall} |");
                }
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 鳴った ／戦 | ／T | 切られた | 粛 | 吸えた ／戦 | 癒えた ／戦 | 拒否で捨てた | 重なり ／戦 | 殴られた味方 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|---|--:|---|");
            foreach (var w in waves)
                foreach (var v in Vers.Where(v => v.Key != "0"))
                {
                    var a = res[(bk, w.Name, v.Key)]; var b = a.Rush;
                    string recv = string.Join("、", b.RecvBy.OrderByDescending(kv => kv.Value).Select(kv => $"{Short(UnitCatalog.ById(kv.Key))} {Per(kv.Value, a.N)}"));
                    Console.WriteLine($"| {w.Name} | {v.Label} | {Per(b.Fired, a.N)} | {Per(b.Fired, a.Turns)} | {Pc(b.Capped, b.Chances)} | {Pc(b.Hushed, b.Chances)} | {Per1(b.Drained, a.N)} | {Per1(b.Healed, a.N)} | {Per(b.Stoic, a.N)} ／ {Per1(b.StoicDrained, a.N)} | {Per1(b.PainOverlap, a.N)} | {recv} |");
                }
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 手番 | 儀式 | 還る | 駆け寄り | 手番外 | 傷になった | 駆け寄りの移し（矢面 ／ 読み手）| 手番の移し（矢面）| 弱体 駆け寄り ／ 手番 | 段1 T | 儀式 T | 痛み ／手番 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|---|---|---|--:|--:|--:|");
            foreach (var w in waves)
                foreach (var v in Vers)
                {
                    var a = res[(bk, w.Name, v.Key)]; var b = a.Rush;
                    Console.WriteLine($"| {w.Name} | {v.Label} | {Per1(a.HealTurn, a.N)} | {Per1(a.HealRite, a.N)} | {Per1(a.HealReturn, a.N)} | {Per1(a.HealRush, a.N)} | {Pc(a.HealRush + a.HealReturn, a.HealAll)} | {Per1(a.Leak, a.N)} | "
                        + $"{Per(b.Moves, a.N)}（{Pc(a.RushMovesFront, b.Moves)} ／ {Pc(a.RushMovesReader, b.Moves)}）| {Per(a.TurnMoves, a.N)}（{Pc(a.TurnMovesFront, a.TurnMoves)}）| "
                        + $"−{Per1(b.StealNeg, a.N)} ／ −{Per1(a.TurnStealNeg, a.N)} | {Per1(a.Tier1T, a.Tier1N)} | {Per1(a.RiteT, a.RiteN)} | {Per1(a.KissPain, a.KissActs)} |");
                }
            Console.WriteLine();
            if (f0.Occupied().Any(o => o.Def.Id == "utsu") || f0.Occupied().Any(o => o.Def.Id == "beni"))
            {
                Console.WriteLine("| 波 | 版 | ウツの与ダメ ／戦 | ウツが受け取った上げ下げ ／戦 | ベニの反転の名目 ／戦 | 駆け寄りで移した状態（件 ／戦） |");
                Console.WriteLine("|---|---|--:|--:|--:|---|");
                foreach (var w in waves)
                    foreach (var v in Vers)
                    {
                        var a = res[(bk, w.Name, v.Key)];
                        string kinds = a.MovedKind.Count == 0 ? "—" : string.Join("、", a.MovedKind.OrderByDescending(kv => kv.Value).Select(kv => $"{StatusKeys.LabelOf(kv.Key)} {Per(kv.Value, a.N)}"));
                        Console.WriteLine($"| {w.Name} | {v.Label} | {Per1(a.UtsuDmg, a.N)} | {Per(a.UtsuGot, a.N)} | {Per1(a.BeniInverse, a.N)} | {kinds} |");
                    }
                Console.WriteLine();
            }
        }

        // 波をまたいだ要約（§6 の読みの順）
        Console.WriteLine("## 要約（4 波 ＝ ボス ／ 近衛 ／ 大隊 ／ 強4 の和）");
        Console.WriteLine();
        Console.WriteLine("移しの受け取り手 ＝ 駆け寄りで移した状態と弱体（件）の受け取り手ごとの割合（前列に限らない・カドの身代わりのように後列が殴られる台がある）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率（4 波の平均）| 手番外の割合 | 移しの矢面 ／ 読み手の割合（駆け寄り）| 移しの受け取り手 | 弱体 駆け寄り ＋ 手番 ／戦 | 痛み ／手番 | 本編 5 波の勝率 |");
        Console.WriteLine("|---|---|--:|--:|---|---|--:|--:|---|");
        foreach (var (bk, name, _) in Boards)
            foreach (var v in Vers)
            {
                var s = new Fx();
                foreach (var w in Hard) s.Add(res[(bk, w.Name, v.Key)]);
                double win = Hard.Average(w => { var a = res[(bk, w.Name, v.Key)]; return 100.0 * a.Wins / a.N; });
                string mains = string.Join(" / ", Mains.Select(w => { var a = res[(bk, w.Name, v.Key)]; return F1(100.0 * a.Wins / a.N); }));
                var got = new Dictionary<string, long>();
                foreach (var (k, x) in s.Rush.MovedBy) { string id = k.Split('|')[1]; got[id] = got.GetValueOrDefault(id) + x.N; }
                foreach (var (id, x) in s.Rush.StolenBy) got[id] = got.GetValueOrDefault(id) + x.N;
                long gotAll = got.Values.Sum();
                string recv = gotAll == 0 ? "—" : string.Join("、", got.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{Short(UnitCatalog.ById(kv.Key))} {Pc(kv.Value, gotAll)}"));
                Console.WriteLine($"| {name} | {v.Label} | {F1(win)} | {Pc(s.HealRush + s.HealReturn, s.HealAll)} | {Pc(s.RushMovesFront, s.Rush.Moves)} ／ {Pc(s.RushMovesReader, s.Rush.Moves)} | {recv} | −{Per1(s.Rush.StealNeg + s.TurnStealNeg, s.N)} | {Per1(s.KissPain, s.KissActs)} | {mains} |");
            }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §4-3 リリの席だけ 5 通り
    // ---------------------------------------------------------------------------------
    internal static Formation Seat(Formation f, UnitDef lili, int seat)
    {
        var others = Enumerable.Range(0, 5).Select(i => f[i]).Where(d => d is not null && d.Id != "lili").ToList();
        var order = new UnitDef?[5];
        int k = 0;
        for (int i = 0; i < 5; i++) order[i] = i == seat ? lili : others[k++];
        return Formation.Build(front1: order[0], front3: order[1], center: order[2], back1: order[3], back3: order[4]);
    }

    static void Seats()
    {
        string[] seatNames = { "前1", "前3", "中央", "後1", "後3" };
        Console.WriteLine($"# 第416期 §4-3 —— 台・呪い ／ 反撃改 × リリの席 5 通り（ほかの4枚は相対順を保って詰める）× {{規定, LK-1}} × 4 波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("| 台 | 席 | 並び | 版 | " + string.Join(" | ", Hard.Select(w => w.Name)) + " | 鳴った ／戦（4 波）| 殴られた味方（上位3）| 移しの矢面の割合 |");
        Console.WriteLine("|---|---|---|---|" + string.Concat(Hard.Select(_ => "---|")) + "--:|---|--:|");
        foreach (var (bk, name, mk) in Boards.Where(b => b.Key is "curse" or "hangeki2"))
            for (int seat = 0; seat < 5; seat++)
                foreach (var v in Vers.Where(v => v.Key is "0" or "1"))
                {
                    var f = Seat(mk(), v.D, seat);
                    var s = new Fx(); var cells = new List<string>();
                    foreach (var w in Hard) { var a = Measure(f, w); s.Add(a); cells.Add(a.WT); }
                    string recv = string.Join("、", s.Rush.RecvBy.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{Short(UnitCatalog.ById(kv.Key))} {Per(kv.Value, s.N)}"));
                    Console.WriteLine($"| {name} | {seatNames[seat]} | {S307.Seats(f)} | {v.Label} | {string.Join(" | ", cells)} | {(v.Key == "0" ? "—" : Per(s.Rush.Fired, s.N))} | {(v.Key == "0" ? "—" : recv)} | {(v.Key == "0" ? "—" : Pc(s.RushMovesFront, s.Rush.Moves))} |");
                }
    }

    // ---------------------------------------------------------------------------------
    // 1戦のログ
    // ---------------------------------------------------------------------------------
    static void Log(string vk, string bk, string wk, int seed)
    {
        var v = Vers.First(x => x.Key == vk);
        var f = Swap(Boards.First(b => b.Key == bk).F(), v.D);
        var w = WaveKeys.First(x => x.Key == wk).W;
        var (r, _, _) = I.Raw(f, w, seed, verbose: true);
        foreach (var line in r.Log) Console.WriteLine(line.Text);
        Console.WriteLine($"勝ち: {r.PlayerWon}・{r.Turns}T");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int bad = 0;
        void Req(bool ok, string what) { Console.WriteLine($"- {what}: {(ok ? "○" : "**×**")}"); if (!ok) bad++; }
        Console.WriteLine("# 第416期 lili416 check");
        Console.WriteLine();
        // 1. 版の札 ＝ 規定の 8 枚 ＋ 足した札・数値は規定のまま・All の外
        Req(UnitCatalog.LiliLk1.Traits.SequenceEqual(UnitCatalog.Lili.Traits.Append(TraitId.KissRush)), "LK-1 の札 ＝ 規定 ＋ KissRush");
        Req(UnitCatalog.LiliLk2.Traits.SequenceEqual(UnitCatalog.Lili.Traits.Append(TraitId.KissRush).Append(TraitId.KissRush2)), "LK-2 の札 ＝ 規定 ＋ KissRush ＋ KissRush2");
        Req(UnitCatalog.LiliLk1m.Traits.SequenceEqual(UnitCatalog.Lili.Traits.Append(TraitId.KissRush).Append(TraitId.KissSpillRushOnly)), "LK-1m の札 ＝ 規定 ＋ KissRush ＋ KissSpillRushOnly");
        Req(Vers.Skip(1).All(v => v.D.MaxHp == 78 && v.D.Attack == 3 && v.D.Speed == 6 && v.D.Id == "lili"), "版の HP ／ 攻 ／ 速 ／ Id は規定のまま");
        Req(!UnitCatalog.All.Any(d => d.Traits.Contains(TraitId.KissRush) || d.Traits.Contains(TraitId.KissRush2) || d.Traits.Contains(TraitId.KissSpillRushOnly)), "`All` に保持者 0 枚");
        // 2. 試遊の台と鎖の終端
        // 第417期に「版・LK-1 呪い」「版・LK-1 紅」「版・LK-2 呪い」を外した（残るのは「版・LK-1m 呪い」だけ）。
        Req(Presets.Versions.Any(r => r.Name == "版・LK-1m 呪い") && VersionRows416.Count(n => Presets.Versions.Any(r => r.Name == n)) == 1, "試遊の台に「版・LK-1m 呪い」が残る（ほかの3台は第417期に外した）");
        Req(!VersionsH407().Any(r => VersionRows416.Contains(r.Name)), "鎖の終端（`VersionsH407`）が4台を外す");
        // 3. 規定の台で鳴らない・帳簿が無い
        var curse0 = One(Presets.LiliCurse416(), I.Boss, 0);
        Req(curse0.Rush.Chances == 0 && curse0.Rush.Fired == 0, "規定のリリでは駆け寄りの帳簿が空");
        // 4. 版では鳴る・上限が効く
        var lk1 = Measure(Swap(Presets.LiliCurse416(), UnitCatalog.LiliLk1), I.Guard, 20);
        var lk2 = Measure(Swap(Presets.LiliCurse416(), UnitCatalog.LiliLk2), I.Guard, 20);
        Req(lk1.Rush.Fired > 0, $"LK-1 は鳴る（近衛 20 戦で {lk1.Rush.Fired} 回）");
        Req(lk1.Rush.ByTurn.Zip(Enumerable.Range(0, 31)).All(x => x.First <= 20), "LK-1 は1ターン1回まで（ターンごとの回数 ≤ 戦数）");
        Req(lk2.Rush.ByTurn.All(x => x <= 40), "LK-2 は1ターン2回まで");
        // 5. LK-1m は手番で移さない
        var m = Measure(Swap(Presets.LiliCurse416(), UnitCatalog.LiliLk1m), I.Guard, 20);
        Req(m.TurnMoves == 0 && m.TurnStealNeg == 0 && m.TurnStealPos == 0, "LK-1m の手番の口づけは状態 ／ 強弱を移さない");
        // 6. verbose の有無で勝敗 ／ T が変わらない（版の台）
        bool same = true;
        foreach (var (_, _, mk) in Boards.Take(3))
            for (int s = 0; s < 10; s++)
            {
                var f = Swap(mk(), UnitCatalog.LiliLk2);
                var (r1, _, _) = I.Raw(f, I.Guard, s, verbose: false);
                var (r2, _, _) = I.Raw(f, I.Guard, s, verbose: true);
                if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) same = false;
            }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（LK-2 × 3 台 × 近衛 × 10 seed）");
        // 7. 駆け寄りは乱数を引かない: ソースに PickOne が無い
        string src = File.ReadAllText("BattleCore/Traits.cs");
        int i0 = src.IndexOf("public sealed class KissRushTrait", StringComparison.Ordinal);
        int i1 = src.IndexOf("public sealed class KissSpillTrait", StringComparison.Ordinal);
        Req(i0 > 0 && i1 > i0 && !src[i0..i1].Contains("PickOne") && !src[i0..i1].Contains("Roll("), "`KissRushTrait` は `PickOne` ／ `Roll` を使わない");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
