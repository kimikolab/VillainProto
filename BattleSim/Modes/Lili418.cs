using BattleCore;
using static Common;
using I = Inv348Diag;
using L = Lili416Diag;
using S307 = Som307Diag;

// =====================================================================================
// lili418 —— 第418期 段0: リリ LK-1m-s の規定化 ／ 本体【軽い期・版1つ】上限2（LK-2m-s）。
// 指示書は design/PHASE418_LILI_SETTLE_SPEC.md ／ 報告は design/PHASE418_LILI_SETTLE.md。1戦の計数は lili416 の `Fx` をそのまま使う。
//
//     dotnet run --project BattleSim -c Release 0 lili418 settle   # 段0 の確かめ: 「試遊・リリ 呪い」「試遊・リリ 紅」× 4 波・seed 0..99 が第417期の LK-1m-s（`LiliLk1ms`）と同じ
//     dotnet run --project BattleSim -c Release 0 lili418 boards   # 本体 §3-2: 8 台 × {規定, LK-2m-s} × 4 波 ＋ 本編 5 波・seed 0..99（予測 `.tmp/p418/predict.md` が無ければ走らない）
//     dotnet run --project BattleSim -c Release 0 lili418 log <0|2> <台の鍵> <波の鍵> [seed]   # 1戦のログ（台の鍵・波の鍵は lili416 と同じ）
//     dotnet run --project BattleSim -c Release 0 lili418 check    # 自己検査
// =====================================================================================
static class Lili418Diag
{
    const int Seeds = 100;
    const string PredictFile = ".tmp/p418/predict.md";
    static readonly (string Key, string Label, UnitDef D)[] Vers = { ("0", "規定", UnitCatalog.Lili), ("2", "LK-2m-s", UnitCatalog.LiliLk2ms) };
    static string Short(UnitDef d) => S307.Short(d);
    static string ShortId(string id) => Short(UnitCatalog.ById(id));
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Pc(long a, long n) => n == 0 ? "—" : (100.0 * a / n).ToString("F0") + "%";
    static string Delta(double x) => double.IsNaN(x) ? "—" : (x >= 0 ? "+" : "") + x.ToString("F1");
    static readonly string[] PlayRows = { "試遊・リリ 呪い", "試遊・リリ 紅" };

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static double Win(L.Fx a) => a.N == 0 ? double.NaN : 100.0 * a.Wins / a.N;
    static Formation Row(string name) => Presets.Versions.First(r => r.Name == name).F;

    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "check";
        string A(int i, string d) => args.Length > i ? args[i] : d;
        if (mode == "boards" && !File.Exists(PredictFile))
        {
            Console.WriteLine($"lili418: 予測のファイル `{PredictFile}` がまだ無い（R397）。予測を書いてから回すこと。");
            Environment.ExitCode = 2;
            return;
        }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        switch (mode)
        {
            case "settle": Settle(); break;
            case "boards": BoardsMode(); break;
            case "log": Log(A(3, "2"), A(4, "curse"), A(5, "boss"), int.Parse(A(6, "0"))); return;
            case "check": Check(); break;
            default: Console.WriteLine("lili418: モードは settle / boards / log / check。"); return;
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 段0 の確かめ
    // ---------------------------------------------------------------------------------
    static void Settle()
    {
        Console.WriteLine($"# 第418期 段0 の確かめ —— 試遊の台（規定のリリ）× {string.Join(" ／ ", L.Hard.Select(w => w.Name))}・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("規定 ＝ 段0 の規定のリリ。LK-1m-s ＝ 第417期の版 `LiliLk1ms`（同じ札の別の定義）。勝率・倒しT・決着T の和・駆け寄りの回数が一致すれば同じ。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 規定 | LK-1m-s | 一致 |");
        Console.WriteLine("|---|---|---|---|:-:|");
        foreach (var name in PlayRows)
            foreach (var w in L.Hard)
            {
                var a = L.Measure(Row(name), w, Seeds);
                var b = L.Measure(L.Swap(Row(name), UnitCatalog.LiliLk1ms), w, Seeds);
                bool same = a.Wins == b.Wins && a.WinT == b.WinT && a.Turns == b.Turns && a.Rush.Fired == b.Rush.Fired;
                Console.WriteLine($"| {name} | {w.Name} | {a.WT} | {b.WT} | {(same ? "○" : "**×**")} |");
            }
    }

    // ---------------------------------------------------------------------------------
    // 本体 §3-2 台 × 版 × 波
    // ---------------------------------------------------------------------------------
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

    static void BoardsMode()
    {
        var waves = L.Hard.Concat(L.Mains).ToArray();
        Console.WriteLine($"# 第418期 本体 §3-2 —— 台 × {{規定（LK-1m-s）, LK-2m-s}} × {string.Join(" ／ ", L.Hard.Select(w => w.Name))} ＋ 本編 第1〜5波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("台・呪い ／ 台・紅は試遊の台「試遊・リリ 呪い」「試遊・リリ 紅」と同じ並び（`Presets.LiliCurse416` ／ `LiliGuren416`）。差は同じ seed の LK-2m-s − 規定。");
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
            Console.WriteLine("| 波 | 版 | 勝率・倒しT | 規定との差 | 倒れた ／戦 | リリ倒れ・T | 倒れた駒 |");
            Console.WriteLine("|---|---|---|--:|--:|---|---|");
            foreach (var w in waves)
                foreach (var v in Vers)
                {
                    var a = res[(bk, w.Name, v.Key)];
                    string dd = v.Key == "0" ? "—" : Delta(Win(a) - Win(res[(bk, w.Name, "0")]));
                    Console.WriteLine($"| {w.Name} | {v.Label} | {a.WT} | {dd} | {Per(a.Fell, a.N)} | {Pc(a.LiliDied, a.N)}・T{Per1(a.LiliDeathT, a.LiliDied)} | {a.Fall} |");
                }
            Console.WriteLine();
            Console.WriteLine("| 波 | 版 | 鳴った ／戦 | 切られた | 吸えた ／戦 | 癒えた ／戦 | 矢面 ／ 読み手 ／ リリ | 殴られた味方（上位3）| 差し引き ／戦 | 重なり ／戦 |");
            Console.WriteLine("|---|---|--:|--:|--:|--:|---|---|--:|--:|");
            foreach (var w in waves)
                foreach (var v in Vers)
                {
                    var a = res[(bk, w.Name, v.Key)]; var b = a.Rush;
                    var (fr, rd, se, all) = RecvShare(b, f0);
                    string recv = string.Join("、", b.RecvBy.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{ShortId(kv.Key)} {Per(kv.Value, a.N)}"));
                    Console.WriteLine($"| {w.Name} | {v.Label} | {Per(b.Fired, a.N)} | {Pc(b.Capped, b.Chances)} | {Per1(b.Drained, a.N)} | {Per1(b.Healed, a.N)} | {Pc(fr, all)} ／ {Pc(rd, all)} ／ {Pc(se, all)} | {recv} | {Per1(b.PainCut, a.N)} | {Per1(b.PainOverlap, a.N)} |");
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

        Console.WriteLine("## 要約（4 波の和・勝率は ボス ／ 近衛 ／ 大隊 ／ 強4）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | ボス ／ 近衛 ／ 大隊 ／ 強4 | 4 波の平均 | 規定との差 | 手番外の割合 | 鳴った ／戦 | 切られた | 殴られた本人（矢面 ／ 読み手 ／ リリ）| 殴られた味方（上位3・回 ／戦）| 弱体の持ち帰り ／戦 | 本編 5 波 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|---|---|--:|---|");
        foreach (var (bk, name, mk) in L.Boards)
        {
            double avg0 = L.Hard.Average(w => Win(res[(bk, w.Name, "0")]));
            foreach (var v in Vers)
            {
                var s = new L.Fx();
                foreach (var w in L.Hard) s.Add(res[(bk, w.Name, v.Key)]);
                double avg = L.Hard.Average(w => Win(res[(bk, w.Name, v.Key)]));
                string cells = string.Join(" ／ ", L.Hard.Select(w => F1(Win(res[(bk, w.Name, v.Key)]))));
                string mains = string.Join(" / ", L.Mains.Select(w => F1(Win(res[(bk, w.Name, v.Key)]))));
                var (fr, rd, se, all) = RecvShare(s.Rush, mk());
                string recv = string.Join("、", s.Rush.RecvBy.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{ShortId(kv.Key)} {Per(kv.Value, s.N)}"));
                Console.WriteLine($"| {name} | {v.Label} | {cells} | {F1(avg)} | {(v.Key == "0" ? "—" : Delta(avg - avg0))} | {Pc(s.HealRush + s.HealReturn, s.HealAll)} | {Per(s.Rush.Fired, s.N)} | {Pc(s.Rush.Capped, s.Rush.Chances)} | "
                    + $"{Pc(fr, all)} ／ {Pc(rd, all)} ／ {Pc(se, all)} | {recv} | −{Per1(s.Rush.StealNeg + s.TurnStealNeg, s.N)} | {mains} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 規定から −10pt 以上落ちたセル（8 台 × 9 波）");
        Console.WriteLine();
        int drops = 0;
        foreach (var (bk, name, _) in L.Boards)
            foreach (var w in waves)
            {
                double d = Win(res[(bk, w.Name, "2")]) - Win(res[(bk, w.Name, "0")]);
                if (d <= -10.0) { Console.WriteLine($"- {name} {w.Name}: {F1(Win(res[(bk, w.Name, "0")]))} → {F1(Win(res[(bk, w.Name, "2")]))}（{Delta(d)}）"); drops++; }
            }
        if (drops == 0) Console.WriteLine("- なし");
        Console.WriteLine();
    }

    static void Log(string vk, string bk, string wk, int seed)
    {
        var f = L.Swap(L.Boards.First(b => b.Key == bk).F(), Vers.First(v => v.Key == vk).D);
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
        Console.WriteLine("# 第418期 lili418 check");
        Console.WriteLine();
        var lili = UnitCatalog.Lili; var old = UnitCatalog.LiliH417;
        Req(lili.Traits.SequenceEqual(old.Traits.Concat(new[] { TraitId.KissRush, TraitId.KissSpillRushOnly, TraitId.KissRushDeduct, TraitId.KissRushNoStigma })), "規定のリリ ＝ 旧の8枚 ＋ KissRush ＋ KissSpillRushOnly ＋ KissRushDeduct ＋ KissRushNoStigma");
        Req(lili.Traits.SequenceEqual(UnitCatalog.LiliLk1ms.Traits), "規定のリリの札 ＝ 第417期の LK-1m-s（`LiliLk1ms`）と同じ並び");
        Req(lili.MaxHp == 78 && lili.Attack == 3 && lili.Speed == 6 && lili.Id == "lili" && lili.Actions.Count == 1 && lili.Actions[0].Kind == ActionKind.Skill, "HP 78 ／ 攻 3 ／ 速 6 ／ Id ／ `[Skill]` は旧のまま");
        Req(lili.Flavor == old.Flavor && lili.PlusText.StartsWith(old.PlusText) && lili.PlusText.EndsWith("（1ターン1回）。駆け寄った敵に聖痕は残らない"), "文面: プラスは旧 ＋ 駆け寄りの節・フレーバーは旧のまま");
        Req(lili.MinusText == "駆け寄って吸った敵の状態（毒・火・呪い・痺れなど）と攻撃力の上げ下げは、殴られた仲間が背負う", "文面: マイナスは最終案");
        Req(UnitCatalog.All.Contains(lili) && !UnitCatalog.All.Contains(old) && UnitCatalog.All.Count(d => d.Id == "lili") == 1, "`All` のリリは規定（旧は入らない）");
        Req(UnitCatalog.LiliLk1.Traits.Take(8).SequenceEqual(old.Traits) && UnitCatalog.LiliLk1m.Traits.Take(8).SequenceEqual(old.Traits), "第416〜417期の版は旧から作ったまま");
        Req(ReferenceEquals(PinL418(lili), old) && ReferenceEquals(PinL418(UnitCatalog.Mudo), UnitCatalog.Mudo), "`PinL418` は規定のリリだけを旧へ");
        Req(PlayRows.All(n => Presets.Versions.Any(r => r.Name == n)), "試遊の台に「試遊・リリ 呪い」「試遊・リリ 紅」がある");
        Req(!Presets.Versions.Any(r => r.Name.StartsWith("版・LK-")), "「版・LK-…」の台は無い");
        Req(!VersionsH407().Any(r => VersionRows418.Contains(r.Name)), "鎖の終端（`VersionsH407`）が第418期の台を外す");
        var c = Row("試遊・リリ 呪い");
        Req(c[0]?.Id == "utsu" && c[1]?.Id == "mudo" && c[2]?.Id == "nel" && c[3]?.Id == "wata" && ReferenceEquals(c[4], lili), "「試遊・リリ 呪い」＝ 前1 ウツ ／ 前3 ムド ／ 中央 ネル ／ 後1 ワタ ／ 後3 リリ（規定）");
        var g = Row("試遊・リリ 紅");
        Req(g[0]?.Id == "borg" && g[1]?.Id == "sid" && g[2]?.Id == "beni" && ReferenceEquals(g[3], lili) && g[4]?.Id == "guza", "「試遊・リリ 紅」＝「試遊・毒 紅」のラウ（後1）をリリ（規定）に");
        // 本体: LK-2m-s
        var v2 = UnitCatalog.LiliLk2ms;
        Req(v2.Traits.SequenceEqual(lili.Traits.Append(TraitId.KissRush2)) && v2.MaxHp == 78 && v2.Attack == 3 && v2.Speed == 6, "LK-2m-s の札 ＝ 規定 ＋ KissRush2（数値は規定のまま）");
        Req(v2.PlusText == lili.PlusText.Replace("（1ターン1回）", "（1ターン2回）") && v2.PlusText != lili.PlusText && v2.MinusText == lili.MinusText, "LK-2m-s の文面は「（1ターン2回）」だけが違う");
        Req(!UnitCatalog.All.Contains(v2), "LK-2m-s は `All` の外");
        Req(Presets.Versions.Any(r => r.Name == "版・2回 リリ 呪い" && ReferenceEquals(r.F[4], v2)), "試遊の台に「版・2回 リリ 呪い」（後3 が LK-2m-s）");
        var g2 = L.Measure(L.Swap(Presets.LiliCurse416(), v2), I.Guard, 20);
        var g1 = L.Measure(Presets.LiliCurse416(), I.Guard, 20);
        Req(g2.Rush.ByTurn.All(x => x <= 40) && g2.Rush.ByTurn.Any(x => x > 20) && g1.Rush.ByTurn.All(x => x <= 20), "上限: 規定は1ターン1回・LK-2m-s は2回まで（2回目が鳴るターンがある）");
        Req(g2.Rush.PainOverlap == 0 && g2.Rush.PainCut > 0 && g2.Rush.StigmaNew == 0, "LK-2m-s も差し引き後の重なり 0・駆け寄りで聖痕を付けない");
        bool same = true;
        foreach (var (_, _, mk) in L.Boards.Take(3))
            for (int sd = 0; sd < 6; sd++)
            {
                var f = L.Swap(mk(), v2);
                var (r1, _, _) = I.Raw(f, I.Guard, sd, verbose: false);
                var (r2, _, _) = I.Raw(f, I.Guard, sd, verbose: true);
                if (r1.PlayerWon != r2.PlayerWon || r1.Turns != r2.Turns) same = false;
            }
        Req(same, "verbose の有無で勝敗 ／ T が変わらない（LK-2m-s × 3 台 × 近衛 × 6 seed）");
        Console.WriteLine();
        Console.WriteLine(bad == 0 ? "すべて ○。" : $"× が {bad} 件。");
        if (bad > 0) Environment.ExitCode = 1;
    }
}
