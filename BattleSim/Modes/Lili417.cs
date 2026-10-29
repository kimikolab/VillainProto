using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// =====================================================================================
// lili417 —— 第417期【軽い期・版だけ】駆け寄る口づけの絞り込み（LK-1m-d ／ LK-1m-s ／ LK-1m-p ／ LK-2m）。
// 指示書は design/PHASE417_LILI_RUSH2_SPEC.md ／ 報告は design/PHASE417_LILI_RUSH2.md。1戦の計数は lili416 の `Fx` をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 lili417 p0       # Phase 0 §3: 引き金の減り（主目標だけ）・儀式の基準値・差し引きの見込み（seed 0..9）
//     dotnet run --project BattleSim -c Release 0 lili417 boards   # §4-2: 8 台 × {規定, LK-1m, LK-1m-d, LK-1m-s, LK-1m-p, LK-2m} × 4 波 ＋ 本編 5 波・seed 0..99
//     dotnet run --project BattleSim -c Release 0 lili417 seats    # §4-3: 台・呪い × リリの席 5 通り × {規定, LK-1m, LK-1m-p} × 4 波
//     dotnet run --project BattleSim -c Release 0 lili417 log <0|1m|d|s|p|2m> <台の鍵> <波の鍵> [seed]
//     dotnet run --project BattleSim -c Release 0 lili417 check    # 自己検査
//
// **予測のファイルが無ければ boards ／ seats を走らせない**（R397・`.tmp/p417/predict.md`）。
// =====================================================================================
static class Lili417Diag
{
    const string PredictFile = ".tmp/p417/predict.md";
    const int Seeds = 100;

    static readonly (string Key, string Label, UnitDef D)[] Vers =
    {
        ("0", "規定", UnitCatalog.Lili), ("1m", "LK-1m", UnitCatalog.LiliLk1m), ("d", "LK-1m-d", UnitCatalog.LiliLk1md),
        ("s", "LK-1m-s", UnitCatalog.LiliLk1ms), ("p", "LK-1m-p", UnitCatalog.LiliLk1mp), ("2m", "LK-2m", UnitCatalog.LiliLk2m),
    };
    static readonly TraitId[] NewCards = { TraitId.KissRushDeduct, TraitId.KissRushNoStigma, TraitId.KissRushPrimary };

    static string Short(UnitDef d) => S307.Short(d);
    static string ShortId(string id) => Short(UnitCatalog.ById(id));
    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F0") + "%";
    static double Win(L.Fx a) => a.N == 0 ? double.NaN : 100.0 * a.Wins / a.N;
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");

    /// <summary>殴られた本人（駆け寄りの受け取り手）の割合: 矢面（前1 ／ 前3）・読み手・リリ自身。</summary>
    static (long Front, long Reader, long Self, long All) RecvShare(KissRushBook b, Formation f)
    {
        var front = L.FrontOf(f); var readers = L.ReadersOf(f);
        long fr = 0, rd = 0, se = 0, all = 0;
        foreach (var (id, n) in b.RecvBy)
        {
            all += n;
            if (front.Contains(id)) fr += n;
            if (readers.Contains(id)) rd += n;
            if (id == "lili") se += n;
        }
        return (fr, rd, se, all);
    }

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode is "boards" or "seats" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"lili417: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "p0": Phase0(); break;
            case "boards": BoardsMode(); break;
            case "seats": Seats(); break;
            case "log": Log(A(3, "p"), A(4, "curse"), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "check": Check(); break;
            default: Console.WriteLine("lili417: モードは p0 / boards / seats / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static UnitDef V(string key) => Vers.First(v => v.Key == key).D;

    // ---------------------------------------------------------------------------------
    // Phase 0 §3
    // ---------------------------------------------------------------------------------
    static void Phase0()
    {
        var waves = L.Hard.Append(L.Mains[1]).ToArray();
        Console.WriteLine("# 第417期 Phase 0 §3-2 —— 引き金の減り（台 × {LK-1m-d, LK-1m-p} × ボス ／ 近衛 ／ 大隊 ／ 強4 ＋ 本編 第2波・seed 0..9）");
        Console.WriteLine();
        Console.WriteLine("引き金 ＝ 第416期 §1-1 と同じ（`Chances`）。主目標でない ＝ 巻き込み ／ 貫きの通過の一撃（`HitFrame.Splash`・LK-1m-p では鳴らない）。"
            + "LK-1m-d の列でも「主目標でない」を数える（札が無いので鳴るが、その割合は同じ）——ここでは LK-1m-p の帳簿の `NotPrimary` を引き金で割る。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 決着T（d）| 引き金 ／戦（d）| 鳴った ／戦（d）| 主目標でない（p）| 引き金 ／戦（p・主目標だけ）| 鳴った ／戦（p）| 鳴った の減り（p ÷ d）|");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (_, name, mk) in L.Boards)
            foreach (var w in waves)
            {
                var d = L.Measure(L.Swap(mk(), V("d")), w, 10);
                var p = L.Measure(L.Swap(mk(), V("p")), w, 10);
                Console.WriteLine($"| {name} | {w.Name} | {Per1(d.Turns, d.N)} | {Per1(d.Rush.Chances, d.N)} | {Per1(d.Rush.Fired, d.N)} | {Pc(p.Rush.NotPrimary, p.Rush.Chances)} | "
                    + $"{Per1(p.Rush.Chances - p.Rush.NotPrimary, p.N)} | {Per1(p.Rush.Fired, p.N)} | {(d.Rush.Fired == 0 ? "—" : (100.0 * p.Rush.Fired / d.Rush.Fired).ToString("F0") + "%")} |");
            }
        Console.WriteLine();

        Console.WriteLine("# Phase 0 §3-3 —— 儀式の周期（規定 ／ LK-1m ／ LK-1m-d ／ LK-1m-s × 4 波・seed 0..9）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 決着T | 儀式のあった戦 | 初回T | 回数 ／戦 | 儀式の回復 ／戦 | 駆け寄りで付けた聖痕 ／戦 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|");
        foreach (var (_, name, mk) in L.Boards)
            foreach (var w in L.Hard)
                foreach (var key in new[] { "0", "1m", "d", "s" })
                {
                    var a = L.Measure(L.Swap(mk(), V(key)), w, 10);
                    Console.WriteLine($"| {name} | {w.Name} | {Vers.First(v => v.Key == key).Label} | {Per1(a.Turns, a.N)} | {Pc(a.RiteN, a.N)} | {Per1(a.RiteT, a.RiteN)} | {Per(a.RiteFires, a.N)} | {Per1(a.HealRite, a.N)} | {(key == "0" ? "—" : Per(a.Rush.StigmaNew, a.N))} |");
                }
        Console.WriteLine();

        Console.WriteLine("# Phase 0 §3-4 —— 差し引きの影響（LK-1m ／ LK-1m-d × 4 波・seed 0..9）");
        Console.WriteLine();
        Console.WriteLine("重なり ＝ 駆け寄った一撃のうち、次の手番の口づけが痛みとしても読んだ量（第416期の `PainOverlap`）。差し引き ＝ LK-1m-d で痛みから外した量（`PainCut`）。"
            + "痛み ／手番 ＝ 手番の頭の痛み（吸う量はこの半分・最低8）。手番の回復 ＝ 手番の口づけで癒えた量 ／戦。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 重なり ／戦（1m）| 痛み ／手番（1m）| 手番の回復（1m）| 差し引き ／戦（d）| 重なり ／戦（d）| 痛み ／手番（d）| 手番の回復（d）| 勝率 1m → d |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var (_, name, mk) in L.Boards)
            foreach (var w in L.Hard)
            {
                var m = L.Measure(L.Swap(mk(), V("1m")), w, 10);
                var d = L.Measure(L.Swap(mk(), V("d")), w, 10);
                Console.WriteLine($"| {name} | {w.Name} | {Per1(m.Rush.PainOverlap, m.N)} | {Per1(m.KissPain, m.KissActs)} | {Per1(m.HealTurn, m.N)} | {Per1(d.Rush.PainCut, d.N)} | {Per1(d.Rush.PainOverlap, d.N)} | {Per1(d.KissPain, d.KissActs)} | {Per1(d.HealTurn, d.N)} | {F1(Win(m))} → {F1(Win(d))} |");
            }
    }

    // ---------------------------------------------------------------------------------
    // §4-2 台 × 版 × 波
    // ---------------------------------------------------------------------------------
    static void BoardsMode()
    {
        var waves = L.Hard.Concat(L.Mains).ToArray();
        Console.WriteLine($"# 第417期 §4-2 —— 台 × {{{string.Join(", ", Vers.Select(v => v.Label))}}} × {string.Join(" ／ ", L.Hard.Select(w => w.Name))} ＋ 本編 第1〜5波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("**表A**（勝ち方）: 勝率・倒しT ／ LK-1m との差（同じ seed）／ 倒れた味方 ／戦 ／ リリが倒れた割合・T ／ 倒れた駒（割合・平均T）。");
        Console.WriteLine();
        Console.WriteLine("**表B**（駆け寄り）: 鳴った ／戦 ／ 切られた（上限）／ 主目標でない（LK-1m-p で鳴らなかった）／ 粛 ／ 吸えた ／戦 ／ 癒えた ／戦 ／ "
            + "殴られた本人の割合（矢面 ＝ 前1・前3 ／ 読み手 ／ リリ自身）／ 殴られた味方（回 ／戦・上位3）／ 付けた聖痕 ／戦 ／ 差し引き ／戦 ／ 重なり ／戦（差し引く版は 0 のはず）。");
        Console.WriteLine();
        Console.WriteLine("**表C**（回復の内訳と儀式）: 手番 ／ 儀式 ／ 還る ／ 駆け寄り（癒えた量 ／戦）／ **手番外の割合**（(駆け寄り ＋ 還る) ÷ 4本）／ 儀式（あった戦の割合 ／ 初回T ／ 回数 ／戦）／ "
            + "弱体の持ち帰り（駆け寄り ＋ 手番・−／戦）／ 痛み ／手番 ／ 注ぎが傷になった ／戦（ベニの輪）。");
        Console.WriteLine();
        var res = new Dictionary<(string, string, string), L.Fx>();
        foreach (var (bk, _, mk) in L.Boards)
            foreach (var w in waves)
                foreach (var v in Vers)
                    res[(bk, w.Name, v.Key)] = L.Measure(L.Swap(mk(), v.D), w, Seeds);

        foreach (var (bk, name, mk) in L.Boards)
        {
            var f0 = mk();
            Console.WriteLine($"## {name}（{S307.Seats(f0)}）・矢面 {string.Join("・", L.FrontOf(f0).Select(ShortId))} ／ 読み手 {(L.ReadersOf(f0).Count == 0 ? "なし" : string.Join("・", L.ReadersOf(f0).Select(ShortId)))}");
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 勝率・倒しT | LK-1m との差 | 倒れた ／戦 | リリ倒れ・T | 倒れた駒 |");
            Console.WriteLine("|---|---|---|--:|--:|---|---|");
            foreach (var w in waves)
                foreach (var v in Vers)
                {
                    var a = res[(bk, w.Name, v.Key)];
                    string dd = v.Key == "1m" ? "—" : Delta(Win(a) - Win(res[(bk, w.Name, "1m")]));
                    Console.WriteLine($"| {w.Name} | {v.Label} | {a.WT} | {dd} | {Per(a.Fell, a.N)} | {Pc(a.LiliDied, a.N)}・T{Per1(a.LiliDeathT, a.LiliDied)} | {a.Fall} |");
                }
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 鳴った ／戦 | 切られた | 主目標でない | 粛 | 吸えた ／戦 | 癒えた ／戦 | 矢面 ／ 読み手 ／ リリ | 殴られた味方 | 聖痕 ／戦 | 差し引き ／戦 | 重なり ／戦 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|---|---|--:|--:|--:|");
            foreach (var w in waves)
                foreach (var v in Vers.Where(v => v.Key != "0"))
                {
                    var a = res[(bk, w.Name, v.Key)]; var b = a.Rush;
                    var (fr, rd, se, all) = RecvShare(b, f0);
                    string recv = string.Join("、", b.RecvBy.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{ShortId(kv.Key)} {Per(kv.Value, a.N)}"));
                    Console.WriteLine($"| {w.Name} | {v.Label} | {Per(b.Fired, a.N)} | {Pc(b.Capped, b.Chances)} | {Pc(b.NotPrimary, b.Chances)} | {Pc(b.Hushed, b.Chances)} | {Per1(b.Drained, a.N)} | {Per1(b.Healed, a.N)} | "
                        + $"{Pc(fr, all)} ／ {Pc(rd, all)} ／ {Pc(se, all)} | {recv} | {Per(b.StigmaNew, a.N)} | {Per1(b.PainCut, a.N)} | {Per1(b.PainOverlap, a.N)} |");
                }
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 手番 | 儀式 | 還る | 駆け寄り | 手番外 | 儀式（戦 ／ 初回T ／ 回 ／戦）| 弱体の持ち帰り ／戦 | 痛み ／手番 | 傷になった ／戦 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|--:|---|--:|--:|--:|");
            foreach (var w in waves)
                foreach (var v in Vers)
                {
                    var a = res[(bk, w.Name, v.Key)]; var b = a.Rush;
                    Console.WriteLine($"| {w.Name} | {v.Label} | {Per1(a.HealTurn, a.N)} | {Per1(a.HealRite, a.N)} | {Per1(a.HealReturn, a.N)} | {Per1(a.HealRush, a.N)} | {Pc(a.HealRush + a.HealReturn, a.HealAll)} | "
                        + $"{Pc(a.RiteN, a.N)} ／ {Per1(a.RiteT, a.RiteN)} ／ {Per(a.RiteFires, a.N)} | −{Per1(b.StealNeg + a.TurnStealNeg, a.N)} | {Per1(a.KissPain, a.KissActs)} | {Per1(a.Leak, a.N)} |");
                }
            Console.WriteLine();
            if (f0.Occupied().Any(o => o.Def.Id is "utsu" or "beni"))
            {
                Console.WriteLine("| 波 | 版 | ウツの与ダメ ／戦 | ウツが受け取った上げ下げ ／戦 | ベニの反転の名目 ／戦 | リリの注ぎが傷になった ／戦 |");
                Console.WriteLine("|---|---|--:|--:|--:|--:|");
                foreach (var w in waves)
                    foreach (var v in Vers)
                    {
                        var a = res[(bk, w.Name, v.Key)];
                        Console.WriteLine($"| {w.Name} | {v.Label} | {Per1(a.UtsuDmg, a.N)} | {Per(a.UtsuGot, a.N)} | {Per1(a.BeniInverse, a.N)} | {Per1(a.Leak, a.N)} |");
                    }
                Console.WriteLine();
            }
        }

        // 要約（4 波 ＝ ボス ／ 近衛 ／ 大隊 ／ 強4）
        Console.WriteLine("## 要約（4 波の和・勝率はボスと近衛 ／ 大隊 ／ 強4 を分ける）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | ボス | 近衛 ／ 大隊 ／ 強4 | 4 波の平均 | 手番外の割合 | 鳴った ／戦 | 殴られた本人（矢面 ／ 読み手 ／ リリ）| 移しの矢面 ／ 読み手（第416期の物差し）| 儀式 初回T ／ 回 ／戦（ボス）| 本編 5 波の勝率 |");
        Console.WriteLine("|---|---|--:|---|--:|--:|--:|---|---|---|---|");
        foreach (var (bk, name, mk) in L.Boards)
            foreach (var v in Vers)
            {
                var s = new L.Fx();
                foreach (var w in L.Hard) s.Add(res[(bk, w.Name, v.Key)]);
                var boss = res[(bk, L.Hard[0].Name, v.Key)];
                string others = string.Join(" ／ ", L.Hard.Skip(1).Select(w => F1(Win(res[(bk, w.Name, v.Key)]))));
                double avg = L.Hard.Average(w => Win(res[(bk, w.Name, v.Key)]));
                string mains = string.Join(" / ", L.Mains.Select(w => F1(Win(res[(bk, w.Name, v.Key)]))));
                var (fr, rd, se, all) = RecvShare(s.Rush, mk());
                Console.WriteLine($"| {name} | {v.Label} | {F1(Win(boss))} | {others} | {F1(avg)} | {Pc(s.HealRush + s.HealReturn, s.HealAll)} | {(v.Key == "0" ? "—" : Per(s.Rush.Fired, s.N))} | "
                    + $"{(v.Key == "0" ? "—" : $"{Pc(fr, all)} ／ {Pc(rd, all)} ／ {Pc(se, all)}")} | {(v.Key == "0" ? "—" : $"{Pc(s.RushMovesFront, s.Rush.Moves)} ／ {Pc(s.RushMovesReader, s.Rush.Moves)}")} | {Per1(boss.RiteT, boss.RiteN)} ／ {Per(boss.RiteFires, boss.N)} | {mains} |");
            }
        Console.WriteLine();

        // −10pt 以上の落ち（規定と比べる・8 台 × 9 波）
        Console.WriteLine("## 規定から −10pt 以上落ちたセル（8 台 × 9 波）");
        Console.WriteLine();
        int drops = 0;
        foreach (var (bk, name, _) in L.Boards)
            foreach (var w in waves)
                foreach (var v in Vers.Skip(1))
                {
                    double d = Win(res[(bk, w.Name, v.Key)]) - Win(res[(bk, w.Name, "0")]);
                    if (d <= -10.0) { Console.WriteLine($"- {name} {w.Name} {v.Label}: {F1(Win(res[(bk, w.Name, "0")]))} → {F1(Win(res[(bk, w.Name, v.Key)]))}（{Delta(d)}）"); drops++; }
                }
        if (drops == 0) Console.WriteLine("- なし");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §4-3 台・呪い × リリの席 5 通り × {規定, LK-1m, LK-1m-p}
    // ---------------------------------------------------------------------------------
    static void Seats()
    {
        string[] seatNames = { "前1", "前3", "中央", "後1", "後3" };
        Console.WriteLine($"# 第417期 §4-3 —— 台・呪い × リリの席 5 通り（ほかの4枚は相対順を保って詰める）× {{規定, LK-1m, LK-1m-p}} × 4 波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("| 席 | 並び | 版 | " + string.Join(" | ", L.Hard.Select(w => w.Name)) + " | 鳴った ／戦（4 波）| 主目標でない | 殴られた本人（矢面 ／ 読み手 ／ リリ）| 移しの矢面 | 殴られた味方（上位3）|");
        Console.WriteLine("|---|---|---|" + string.Concat(L.Hard.Select(_ => "---|")) + "--:|--:|---|--:|---|");
        var mk = L.Boards.First(b => b.Key == "curse").F;
        for (int seat = 0; seat < 5; seat++)
            foreach (var key in new[] { "0", "1m", "p" })
            {
                var f = L.Seat(mk(), V(key), seat);
                var s = new L.Fx(); var cells = new List<string>();
                foreach (var w in L.Hard) { var a = L.Measure(f, w, Seeds); s.Add(a); cells.Add(a.WT); }
                var (fr, rd, se, all) = RecvShare(s.Rush, f);
                string recv = string.Join("、", s.Rush.RecvBy.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{ShortId(kv.Key)} {Per(kv.Value, s.N)}"));
                bool k0 = key == "0";
                Console.WriteLine($"| {seatNames[seat]} | {S307.Seats(f)} | {Vers.First(v => v.Key == key).Label} | {string.Join(" | ", cells)} | {(k0 ? "—" : Per(s.Rush.Fired, s.N))} | "
                    + $"{(k0 ? "—" : Pc(s.Rush.NotPrimary, s.Rush.Chances))} | {(k0 ? "—" : $"{Pc(fr, all)} ／ {Pc(rd, all)} ／ {Pc(se, all)}")} | {(k0 ? "—" : Pc(s.RushMovesFront, s.Rush.Moves))} | {(k0 ? "—" : recv)} |");
            }
    }

    // ---------------------------------------------------------------------------------
    // 1戦のログ
    // ---------------------------------------------------------------------------------
    static void Log(string vk, string bk, string wk, int seed)
    {
        var f = L.Swap(L.Boards.First(b => b.Key == bk).F(), V(vk));
        var w = L.WaveKeys.First(x => x.Key == wk).W;
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
        Console.WriteLine("# 第417期 lili417 check");
        Console.WriteLine();
        // 1. 版の札 ＝ LK-1m ＋ 差し引き ＋ 足した札・数値は規定のまま・All の外・LK-1m は第416期のまま
        var baseD = UnitCatalog.LiliLk1m.Traits.Append(TraitId.KissRushDeduct).ToArray();
        Req(UnitCatalog.LiliLk1m.Traits.SequenceEqual(UnitCatalog.Lili.Traits.Append(TraitId.KissRush).Append(TraitId.KissSpillRushOnly)), "LK-1m（対照）は第416期のまま");
        Req(UnitCatalog.LiliLk1md.Traits.SequenceEqual(baseD), "LK-1m-d の札 ＝ LK-1m ＋ KissRushDeduct");
        Req(UnitCatalog.LiliLk1ms.Traits.SequenceEqual(baseD.Append(TraitId.KissRushNoStigma)), "LK-1m-s の札 ＝ LK-1m-d ＋ KissRushNoStigma");
        Req(UnitCatalog.LiliLk1mp.Traits.SequenceEqual(baseD.Append(TraitId.KissRushPrimary)), "LK-1m-p の札 ＝ LK-1m-d ＋ KissRushPrimary");
        Req(UnitCatalog.LiliLk2m.Traits.SequenceEqual(baseD.Append(TraitId.KissRush2)), "LK-2m の札 ＝ LK-1m-d ＋ KissRush2");
        Req(Vers.Skip(1).All(v => v.D.MaxHp == 78 && v.D.Attack == 3 && v.D.Speed == 6 && v.D.Id == "lili"), "版の HP ／ 攻 ／ 速 ／ Id は規定のまま");
        Req(!UnitCatalog.All.Any(d => d.Traits.Any(NewCards.Contains)), "`All` に第417期の札の保持者 0 枚");
        // 2. 試遊の台と鎖の終端
        Req(VersionRows417.All(n => Presets.Versions.Any(r => r.Name == n)), "試遊の台に第417期の4台がある");
        Req(!Presets.Versions.Any(r => r.Name is "版・LK-1 呪い" or "版・LK-1 紅" or "版・LK-2 呪い") && Presets.Versions.Any(r => r.Name == "版・LK-1m 呪い"), "「版・LK-1 呪い」「版・LK-1 紅」「版・LK-2 呪い」を外し、「版・LK-1m 呪い」は残す");
        Req(!VersionsH407().Any(r => VersionRows417.Contains(r.Name) || VersionRows416.Contains(r.Name)), "鎖の終端（`VersionsH407`）が第416 ／ 417期の台を外す");
        // 3. 規定の台で帳簿が無い
        var c0 = L.One(Presets.LiliCurse416(), I.Boss, 0);
        Req(c0.Rush.Chances == 0 && c0.Rush.Fired == 0, "規定のリリでは駆け寄りの帳簿が空");
        // 4. 差し引き: 差し引く版は重なり 0・差し引いた量 > 0 ／ LK-1m は重なりが残る
        var pool = new Dictionary<string, L.Fx>();
        foreach (var key in new[] { "1m", "d", "s", "p", "2m" })
        {
            var s = new L.Fx();
            foreach (var bk in new[] { "curse", "guren", "hangeki2", "nensho" })
                foreach (var w in new[] { I.Boss, I.Guard })
                    s.Add(L.Measure(L.Swap(L.Boards.First(b => b.Key == bk).F(), V(key)), w, 20));
            pool[key] = s;
        }
        Req(pool["1m"].Rush.PainOverlap > 0 && pool["1m"].Rush.PainCut == 0, $"LK-1m は差し引かない（重なり {pool["1m"].Rush.PainOverlap}・差し引き 0）");
        foreach (var key in new[] { "d", "s", "p", "2m" })
            Req(pool[key].Rush.PainOverlap == 0 && pool[key].Rush.PainCut > 0, $"{Vers.First(v => v.Key == key).Label} は差し引き後の重なり 0（差し引き {pool[key].Rush.PainCut}）");
        // 5. LK-1m-s は聖痕を付けない
        Req(pool["s"].Rush.StigmaNew == 0 && pool["s"].Rush.StigmaSkipped > 0, $"LK-1m-s は駆け寄りで聖痕を付けない（付けなかった {pool["s"].Rush.StigmaSkipped}）");
        Req(pool["d"].Rush.StigmaNew > 0 && pool["d"].Rush.StigmaSkipped == 0, "LK-1m-d は聖痕を付ける（第416期のまま）");
        // 6. LK-1m-p は主目標でない一撃で鳴らない・ほかの版は NotPrimary を数えない
        Req(pool["p"].Rush.NotPrimary > 0, $"LK-1m-p は主目標でない一撃で鳴らない（{pool["p"].Rush.NotPrimary} 回）");
        Req(new[] { "1m", "d", "s", "2m" }.All(k => pool[k].Rush.NotPrimary == 0), "ほかの版は「主目標でない」で止まらない");
        // 7. 上限: LK-2m は1ターン2回まで・ほかは1回まで
        var g2 = L.Measure(L.Swap(Presets.LiliCurse416(), V("2m")), I.Guard, 20);
        var g1 = L.Measure(L.Swap(Presets.LiliCurse416(), V("d")), I.Guard, 20);
        Req(g2.Rush.ByTurn.All(x => x <= 40) && g2.Rush.ByTurn.Any(x => x > 20), "LK-2m は1ターン2回まで（2回目が鳴るターンがある）");
        Req(g1.Rush.ByTurn.All(x => x <= 20), "LK-1m-d は1ターン1回まで");
        // 8. 手番の口づけは移さない（4 版とも LK-1m の上）
        Req(new[] { "d", "s", "p", "2m" }.All(k => pool[k].TurnMoves == 0 && pool[k].TurnStealNeg == 0 && pool[k].TurnStealPos == 0), "4 版とも手番の口づけは状態 ／ 強弱を移さない");
        // 9. verbose の有無で勝敗 ／ T が変わらない
        bool same = true;
        foreach (var key in new[] { "s", "p", "2m" })
            foreach (var (_, _, mk) in L.Boards.Take(3))
                for (int s = 0; s < 6; s++)
                {
                    var f = L.Swap(mk(), V(key));
                    var (r1, _, _) = I.Raw(f, I.Guard, s, verbose: false);
                    var (r2, _, _) = I.Raw(f, I.Guard, s, verbose: true);
                    if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) same = false;
                }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（3 版 × 3 台 × 近衛 × 6 seed）");
        // 10. 乱数を引かない
        string src = File.ReadAllText("BattleCore/Traits.cs");
        int i0 = src.IndexOf("public sealed class KissRushTrait", StringComparison.Ordinal);
        int i1 = src.IndexOf("public sealed class KissSpillTrait", StringComparison.Ordinal);
        Req(i0 > 0 && i1 > i0 && !src[i0..i1].Contains("PickOne") && !src[i0..i1].Contains("Roll("), "`KissRushTrait` は `PickOne` ／ `Roll` を使わない");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
