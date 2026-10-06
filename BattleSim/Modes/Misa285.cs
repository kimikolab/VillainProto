using BattleCore;
using static Common;
using BA = BurnAuditDiag;

// =====================================================================================
// misa285 —— 第285期「トメ → 見境なしのミサ（改名）＋ 炸裂の連射化（羽）」。
// 指示書は design/PHASE285_MISA_FEATHERS_SPEC.md ／ 報告は design/PHASE285_MISA_FEATHERS.md。
//
//     dotnet run --project BattleSim -c Release 0 misa285 p0        # Phase 0 の器具（流用・列越え・介入・乱射の引き・ボスでの等価・手番）
//     dotnet run --project BattleSim -c Release 0 misa285 compare   # `compare` 64 行 × 5 波 × 版 T1n ／ M-a ／ M-b（動くのはミサ在席の行だけのはず）
//     dotnet run --project BattleSim -c Release 0 misa285 run       # ミサ在席の行 × 本編第1〜5波・ボス × 3版の中身（羽・発・乱射・爪痕）
//     dotnet run --project BattleSim -c Release 0 misa285 boss      # 第283期の代表の台（`boss283 ctl` の 8 台）× 3版 × 帯A ／ 帯B
//     dotnet run --project BattleSim -c Release 0 misa285 elite     # 精鋭（近衛 ／ 大隊）× ミサ在席の行 × 3版
//     dotnet run --project BattleSim -c Release 0 misa285 check     # 自己検査
//     dotnet run --project BattleSim -c Release 0 misa285 log <行の頭 | boss:n> <T1n|Ma|Mb> <波 1..5 | ボス | 近衛 | 大隊> [seed]
// =====================================================================================
static class Misa285Diag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "run";
        switch (mode)
        {
            case "p0": P0(); return;
            case "compare": CompareAll(); return;
            case "run": RunRows(); return;
            case "boss": Boss(); return;
            case "elite": EliteRows(); return;
            case "check": Check(); return;
            case "log": LogOne(args.Length > 3 ? args[3] : "見境 (", args.Length > 4 ? args[4] : "Ma", args.Length > 5 ? args[5] : "5", args.Length > 6 ? int.Parse(args[6]) : 0); return;
            default: Console.WriteLine("misa285: モードは p0 / compare / run / boss / elite / check / log。"); return;
        }
    }

    const int Seeds = 200;

    internal sealed record Ver(string Name, string What, UnitDef D);
    internal static readonly Ver[] Vers =
    {
        new("T1n", "第283〜285期の規定（炸裂の一撃・層を残す・乱射）＝ 対照", UnitCatalog.TomeT1n),
        new("Ma", "M-a 羽の連射（乱射した羽は戻ってくる）", UnitCatalog.TomeMa),
        new("Mb", "M-b 羽の連射（乱射した羽は失う・下限 1）", UnitCatalog.TomeMb),
    };
    static Ver VerOf(string n) => Vers.First(v => v.Name == n || v.Name == n.Replace("-", ""));

    static bool HasMisa(Formation f) => f.Occupied().Any(o => o.Def.Id == UnitCatalog.Tome.Id);
    static (string Name, Formation F)[] Rows => CompareBuilds();
    static (string Name, Formation F)[] MisaRows => Rows.Where(r => HasMisa(r.F)).ToArray();
    static Formation With(Formation f, Ver v) => FvSwap(f, UnitCatalog.Tome, v.D);

    // ---------------------------------------------------------------------------------
    // 波（本編 5 ／ ボス ／ 精鋭 2）
    // ---------------------------------------------------------------------------------
    internal sealed record Wave(string Name, string Group, Func<List<UnitState>> Make);
    static readonly Wave[] Main = Enumerable.Range(0, EnemyCatalog.Stages.Count)
        .Select(w => new Wave($"第{w + 1}波", "本編", () => BattleEngine.Materialize(EnemyCatalog.Stages[w].Enemy, BattleContext.EnemyTeam))).ToArray();
    static readonly Wave BossW = new("ボス", "ボス", () => BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None));
    static readonly Wave[] EliteW =
    {
        new("近衛", "精鋭", () => BattleEngine.MaterializeEnemy(EliteDiag.Five, EliteDiag.Elite)),
        new("大隊", "精鋭", () => BattleEngine.MaterializeEnemy(EliteDiag.Nine, EliteDiag.Elite)),
    };
    static Wave WaveOf(string n) =>
        n == "ボス" ? BossW : EliteW.FirstOrDefault(w => w.Name == n) ?? Main[int.Parse(n) - 1];

    // ---------------------------------------------------------------------------------
    // 集計
    // ---------------------------------------------------------------------------------
    internal sealed class Agg
    {
        public long N, Wins, WinTurns, MisaDied;
        public long Volleys, Shots, Chased, Flow, Sprayed, Turned, Gained, Lost, EndSum, EndMax, PeakMax;
        public long RFires, RCross, RLayerSum, RDealt, RScar, RKills;
        public long STurns, SFoe, SAlly, SPulled, SAllyDealt, SAllyKills, SFoeDealt;
        public long MisaToFoe, Encores, YokeHits, YokeLost;

        public void Merge(Agg o)
        {
            N += o.N; Wins += o.Wins; WinTurns += o.WinTurns; MisaDied += o.MisaDied;
            Volleys += o.Volleys; Shots += o.Shots; Chased += o.Chased; Flow += o.Flow; Sprayed += o.Sprayed; Turned += o.Turned;
            Gained += o.Gained; Lost += o.Lost; EndSum += o.EndSum; EndMax = Math.Max(EndMax, o.EndMax); PeakMax = Math.Max(PeakMax, o.PeakMax);
            RFires += o.RFires; RCross += o.RCross; RLayerSum += o.RLayerSum; RDealt += o.RDealt; RScar += o.RScar; RKills += o.RKills;
            STurns += o.STurns; SFoe += o.SFoe; SAlly += o.SAlly; SPulled += o.SPulled; SAllyDealt += o.SAllyDealt; SAllyKills += o.SAllyKills; SFoeDealt += o.SFoeDealt;
            MisaToFoe += o.MisaToFoe; Encores += o.Encores; YokeHits += o.YokeHits; YokeLost += o.YokeLost;
        }

        public void Take(BattleResult r, List<UnitState> p)
        {
            N++;
            if (r.PlayerWon) { Wins++; WinTurns += r.Turns; }
            UnitState? misa = p.FirstOrDefault(u => u.Def.Id == UnitCatalog.Tome.Id);
            if (misa is not null && !misa.IsAlive) MisaDied++;
            if (r.Yoke.CutBy.TryGetValue(UnitCatalog.Tome.Id, out var yk)) { YokeHits += yk.Hits; YokeLost += yk.Lost; }
            if (!r.TallyByUnit.TryGetValue(UnitCatalog.Tome.Id, out var t)) { EndSum += 1; EndMax = Math.Max(EndMax, 1); return; }
            Volleys += t.FeatherVolleys; Shots += t.FeatherShots; Chased += t.FeatherChased; Flow += t.FeatherFlow;
            Sprayed += t.FeatherSprayed; Turned += t.FeatherTurned; Gained += t.FeatherGained; Lost += t.FeatherLost;
            long end = FeathersTrait.Initial + t.FeatherGained - t.FeatherLost;
            EndSum += end; EndMax = Math.Max(EndMax, end); PeakMax = Math.Max(PeakMax, t.FeatherMax);
            RFires += t.RuptureFires; RCross += t.RuptureCross; RLayerSum += t.RuptureLayerSum; RDealt += t.RuptureDealt; RScar += t.RuptureScar; RKills += t.RuptureKills;
            STurns += t.SprayTurns; SFoe += t.SprayFoe; SAlly += t.SprayAlly; SPulled += t.SprayPulled; SAllyDealt += t.SprayAllyDealt; SAllyKills += t.SprayAllyKills; SFoeDealt += t.SprayFoeDealt;
            MisaToFoe += t.DamageToEnemy; Encores += t.EncoreFires;
        }

        public double Win => N == 0 ? double.NaN : 100.0 * Wins / N;
    }

    static Agg Measure(Formation f, Wave w, int from = 0, int n = Seeds)
    {
        var parts = new Agg[n];
        Parallel.For(0, n, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var r = BattleEngine.Run(p, w.Make(), from + i, verbose: false);
            var a = new Agg(); a.Take(r, p); parts[i] = a;
        });
        var all = new Agg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string Per(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F2");
    static string Per1(long a, long n) => n == 0 ? "—" : ((double)a / n).ToString("F1");
    static string Sgn(double x) => x.ToString("+0.0;-0.0;0.0");
    static string WinT(Agg a) => a.Wins == 0 ? "—" : ((double)a.WinTurns / a.Wins).ToString("F2");

    // ---------------------------------------------------------------------------------
    // compare 64 行 × 3 版
    // ---------------------------------------------------------------------------------
    internal static double[,] CompareGrid(Ver v)
    {
        var rows = Rows;
        var g = new double[rows.Length, Main.Length];
        Parallel.For(0, rows.Length * Main.Length, k =>
        {
            int ri = k / Main.Length, wi = k % Main.Length;
            var f = With(rows[ri].F, v);
            int wins = 0;
            for (int s = 0; s < Seeds; s++)
                if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / Seeds;
        });
        return g;
    }

    static void CompareAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = Rows;
        var grids = Vers.ToDictionary(v => v.Name, CompareGrid);
        var basis = grids["T1n"];
        Console.WriteLine("# 第285期 `compare` 64 行 × 版（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("ミサは各行の `UnitCatalog.Tome` を同じ席で版に差し替える（ミサのいない行は版に依らず同じ編成）。");
        Console.WriteLine();
        Console.WriteLine("## 表1 ミサ在席の行");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 第1波 | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波 平均 | T1n 差 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int ri = 0; ri < rows.Length; ri++)
        {
            if (!HasMisa(rows[ri].F)) continue;
            double m0 = Enumerable.Range(1, 4).Average(w => basis[ri, w]);
            foreach (var v in Vers)
            {
                var g = grids[v.Name];
                double m = Enumerable.Range(1, 4).Average(w => g[ri, w]);
                Console.WriteLine($"| {rows[ri].Name} | {v.Name} | " + string.Join(" | ", Enumerable.Range(0, Main.Length).Select(w => F1(g[ri, w]))) + $" | {F1(m)} | {(v.Name == "T1n" ? "" : Sgn(m - m0))} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 表2 ミサのいない行のずれ（版 − T1n・0 件が正）");
        Console.WriteLine();
        foreach (var v in Vers.Where(v => v.Name != "T1n"))
        {
            int bad = 0, cells = 0;
            for (int ri = 0; ri < rows.Length; ri++)
            {
                if (HasMisa(rows[ri].F)) continue;
                for (int w = 0; w < Main.Length; w++) { cells++; if (grids[v.Name][ri, w] != basis[ri, w]) bad++; }
            }
            Console.WriteLine($"- {v.Name}: {cells} セル中 **{bad} 件**");
        }
        Console.WriteLine();
        Console.WriteLine("## 表3 全64行・主判定19行の平均（第1〜5波）と歯止め");
        Console.WriteLine();
        var prim = Baseline.PrimaryRows.Select(n => Array.FindIndex(rows, r => r.Name == n)).ToArray();
        Console.WriteLine($"主判定19行の行番号が全部引けた: {(prim.All(i => i >= 0) ? "○" : "×")}（歯止め {Baseline.PrimaryFifthFloor}%）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 全64行 第1〜5波 | 主判定19行 第1〜5波 | 主判定の第五波 − 歯止め | 情報セル（全64行・第2〜5波・0<x<100） |");
        Console.WriteLine("|---|---|---|--:|--:|");
        foreach (var v in Vers)
        {
            var g = grids[v.Name];
            string all = string.Join(" / ", Enumerable.Range(0, Main.Length).Select(w => F1(Enumerable.Range(0, rows.Length).Average(ri => g[ri, w]))));
            string pr = string.Join(" / ", Enumerable.Range(0, Main.Length).Select(w => F1(prim.Average(ri => g[ri, w]))));
            double p5 = prim.Average(ri => g[ri, 4]);
            int info = 0;
            for (int ri = 0; ri < rows.Length; ri++) for (int w = 1; w < Main.Length; w++) if (g[ri, w] > 0 && g[ri, w] < 100) info++;
            Console.WriteLine($"| {v.Name} | {all} | {pr} | {Sgn(p5 - Baseline.PrimaryFifthFloor)} | {info} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 中身（ミサ在席の行 × 本編・ボス）
    // ---------------------------------------------------------------------------------
    static void Mech(string title, IEnumerable<(string Board, Formation F)> boards, Wave[] waves, int from = 0, int n = Seeds)
    {
        Console.WriteLine($"## {title}");
        Console.WriteLine();
        Console.WriteLine("勝率 ／ 倒しT ／ ミ死 ＝ ミサが倒れた戦の割合 ／ 羽: 終 ＝ 戦の終わりの平均（最大）／ 発（1戦）: 追 ＝ 標を追った発（流 ＝ うち前の的が倒れて流れた発）・乱 ＝ 乱射の発 ／ 化 ＝ 途中で乱射に化けた回数 ／ "
                          + "乱味 ＝ 乱射が味方に当たった発（与ダメ・倒した味方）／ 引 ＝ 乱射が標に引かれた発 ／ 爪 ＝ 爪痕累計 ／ 越 ＝ 列越えの発 ／ 与 ＝ ミサの敵への与ダメ（1戦）／ 軛 ＝ ミサの一撃が軛（1発 25 まで）に切られた回数（切られた量）。T1n の「追」は炸裂の回数");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 勝率 | 倒しT | ミ死 | 羽 終（最大） | 追（流） | 乱 | 化 | 乱味（与・倒） | 引 | 爪 | 越 | 与 | 軛 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (board, f) in boards)
            foreach (var w in waves)
                foreach (var v in Vers)
                {
                    var a = Measure(With(f, v), w, from, n);
                    bool fe = v.Name != "T1n";
                    string chase = fe ? $"{Per(a.Chased, a.N)}（{Per(a.Flow, a.N)}）" : Per(a.RFires, a.N);
                    string spray = fe ? Per(a.Sprayed, a.N) : Per(a.SFoe + a.SAlly, a.N);
                    Console.WriteLine($"| {board} | {w.Name} | {v.Name} | {F1(a.Win)} | {WinT(a)} | {F1(100.0 * a.MisaDied / a.N)} | {(fe ? $"{Per(a.EndSum, a.N)}（{a.PeakMax}）" : "—")} | {chase} | {spray} | {(fe ? Per(a.Turned, a.N) : "—")} | "
                                      + $"{Per(a.SAlly, a.N)}（{Per1(a.SAllyDealt, a.N)}・{Per(a.SAllyKills, a.N)}）| {Per(a.SPulled, a.N)} | {Per1(a.RScar, a.N)} | {Per(a.RCross, a.N)} | {Per1(a.MisaToFoe, a.N)} | {Per(a.YokeHits, a.N)}（{Per1(a.YokeLost, a.N)}）|");
                }
        Console.WriteLine();
    }

    static void RunRows()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第285期 ミサ在席の行の中身（seed 0..199）");
        Console.WriteLine();
        foreach (var (name, f) in MisaRows) Console.WriteLine($"- {name} ＝ " + BA.SeatsNamed(f));
        foreach (var v in Vers) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();
        Mech("表1 本編第1〜5波・ボス", MisaRows.Select(r => (r.Name, r.F)), Main.Append(BossW).ToArray());
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static Formation Seat(UnitDef[] order)
    {
        var f = new Formation();
        for (int i = 0; i < order.Length; i++) f[i] = order[i];
        return f;
    }

    static void Boss()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第285期 ボス —— 第283期の代表の台（`boss283 ctl` の 8 台）× 版");
        Console.WriteLine();
        Console.WriteLine("**ボス（勇者・全体攻撃）はほとんど乱数を引かない**ので、100% は多くの台で「1本の軌跡」である（第283期）。");
        Console.WriteLine();
        Console.WriteLine("## 表1 勝率（帯A ＝ seed 0..199 ／ 帯B ＝ seed 200..599）と爪痕累計（帯A）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 席 | " + string.Join(" | ", Vers.Select(v => v.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Vers.Select(_ => "--:|")));
        foreach (var (label, order) in Boss283Diag.CtlBoards)
        {
            var cells = Vers.Select(v =>
            {
                var f = With(Seat(order), v);
                var a = Measure(f, BossW, 0, 200); var b = Measure(f, BossW, 200, 400);
                return $"{F1(a.Win)} ／ {F1(b.Win)} ／ {Per1(a.RScar, a.N)}";
            });
            Console.WriteLine($"| {label} | {BA.SeatsNamed(Seat(order))} | " + string.Join(" | ", cells) + " |");
        }
        Console.WriteLine();
        Mech("表2 中身（帯A）", Boss283Diag.CtlBoards.Select(b => (b.Label, Seat(b.Order))), new[] { BossW });
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void EliteRows()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# 第285期 精鋭（近衛 ／ 大隊・HP {EliteDiag.Elite.HpPercent}% ／ 攻 {EliteDiag.Elite.AtkPercent}%）× ミサ在席の行 × 版（seed 0..199）");
        Console.WriteLine();
        Mech("表1", MisaRows.Select(r => (r.Name, r.F)), EliteW);
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // Phase 0 の器具
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第285期 Phase 0 —— 器具の答え（seed 0..199）");
        Console.WriteLine();
        var ma = VerOf("Ma");
        // (1)(2)(4)(8): ミサ在席の行 × 本編第2〜5波
        Console.WriteLine("## (1)(2)(4)(8) ミサ在席の行 × 本編第2〜5波（M-a）");
        Console.WriteLine();
        Console.WriteLine("追 ＝ 標を追った発 ／ 炸 ＝ 標の段で標持ちに当たり打点が乗った発（`RuptureFires`）／ 越 ＝ うち列越え ／ 再 ＝ ミサの再行動 ／ 一振り ＝ 羽の一振りの数 ／ 撃 ＝ 追 ＋ 乱");
        Console.WriteLine();
        Console.WriteLine("| 行 | 追 | 炸 | 追 − 炸 | 越 | 再 | 一振り | 撃 | 撃 ≧ 一振り |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var (name, f) in MisaRows)
        {
            var a = new Agg();
            foreach (var w in Main.Skip(1)) a.Merge(Measure(With(f, ma), w));
            Console.WriteLine($"| {name} | {a.Chased} | {a.RFires} | {a.Chased - a.RFires} | {a.RCross} | {a.Encores} | {a.Volleys} | {a.Chased + a.Sprayed} | {(a.Chased + a.Sprayed >= a.Volleys ? "○" : "×")} |");
        }
        Console.WriteLine();
        // (5): 乱射の発が標に引かれた回数（敵の標持ちはいない ＝ 引くのは味方側の標だけ）
        Console.WriteLine("## (5) 羽の乱射が標に引かれた発（M-a・本編第2〜5波・1戦平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 乱射の発 | 引かれた発 | 味方に当たった発 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var (name, f) in MisaRows)
        {
            var a = new Agg();
            foreach (var w in Main.Skip(1)) a.Merge(Measure(With(f, ma), w));
            Console.WriteLine($"| {name} | {Per(a.Sprayed, a.N)} | {Per(a.SPulled, a.N)} | {Per(a.SAlly, a.N)} |");
        }
        Console.WriteLine();
        // (6): ボスでの等価
        Console.WriteLine("## (6) ボスでの等価（第283期の代表の台・帯A・1戦平均）");
        Console.WriteLine();
        Console.WriteLine("T1n: 炸裂の数 × 平均層 ＝ 打点の倍数の和（攻 × 2 × Σ層）。M-a: 標を追った発の数（攻 × 2 × Σ発）。**全弾がボスに当たるなら両者の比が総量の比**");
        Console.WriteLine();
        Console.WriteLine("| 台 | T1n 炸裂 | T1n 平均層 | T1n Σ層 | M-a 一振り | M-a 平均の羽 | M-a 追 | M-a 追 ÷ T1n Σ層 | 与 T1n | 与 M-a |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (label, order) in Boss283Diag.CtlBoards)
        {
            var t = Measure(With(Seat(order), VerOf("T1n")), BossW);
            var m = Measure(With(Seat(order), ma), BossW);
            Console.WriteLine($"| {label} | {Per1(t.RFires, t.N)} | {(t.RFires == 0 ? "—" : ((double)t.RLayerSum / t.RFires).ToString("F2"))} | {Per1(t.RLayerSum, t.N)} | {Per1(m.Volleys, m.N)} | {(m.Volleys == 0 ? "—" : ((double)m.Shots / m.Volleys).ToString("F2"))} | {Per1(m.Chased, m.N)} | {(t.RLayerSum == 0 ? "—" : ((double)m.Chased / t.RLayerSum).ToString("F2"))} | {Per1(t.MisaToFoe, t.N)} | {Per1(m.MisaToFoe, m.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int fail = 0;
        void Expect(string what, bool ok, string detail = "")
        {
            Console.WriteLine($"- {(ok ? "○" : "×")} {what}{(detail.Length > 0 ? "（" + detail + "）" : "")}");
            if (!ok) fail++;
        }
        Console.WriteLine("# misa285 check");
        Console.WriteLine();

        // (a) 改名: 規定の名前・Id・行名
        Expect("(a) 規定の名前は「見境なしのミサ」・Id は tome のまま・版 M-a ／ M-b も同じ Id",
            UnitCatalog.Tome.Name == "見境なしのミサ" && UnitCatalog.Tome.Id == "tome" && UnitCatalog.TomeMa.Id == "tome" && UnitCatalog.TomeMb.Id == "tome");
        string[] want = { "見境 (ミサ×ソラ)", "見境改 (ミサ×薙ぎ)", "標経済 (ヒサ×ザン×ミサ)" };
        var misaRows = MisaRows.Select(r => r.Name).ToArray();
        Expect("(a) ミサ在席の `compare` 行は3行で、行名は新しい名前", misaRows.Length == 3 && want.All(misaRows.Contains), string.Join("・", misaRows));
        Expect("(a) 旧い行名（止め・トメ）を持つ `compare` 行が無い", !Rows.Any(r => r.Name.Contains("トメ") || r.Name.StartsWith("止め")));

        // (b) 規定 ＝ M-b
        // 第286期に M-b を規定にしたので、(b)(c) は「規定 ＝ M-b」の形に直した（第285期の版は「規定 ＝ T1n・羽の保持者 0」を確かめていた）。
        bool onlyDefault = UnitCatalog.Everyone.Where(d => d.Traits.Contains(TraitId.Feathers) || d.Traits.Contains(TraitId.FeatherLoss)).All(d => ReferenceEquals(d, UnitCatalog.Tome))
                           && Rows.All(r => r.F.Occupied().All(o => !o.Def.Traits.Contains(TraitId.Feathers) || ReferenceEquals(o.Def, UnitCatalog.Tome)));
        Expect("(b) 羽の札の保持者は `Everyone` ／ `compare` で規定のミサだけ・規定 ＝ M-b（`TomeMb`）・T1n は `TomeT1n` が明示的に持つ",
            onlyDefault && ReferenceEquals(UnitCatalog.Tome, UnitCatalog.TomeMb) && !ReferenceEquals(UnitCatalog.Tome, UnitCatalog.TomeT1n));
        Expect("(b) M-b は M-a ＋ 羽を失う の1札だけが違う",
            UnitCatalog.TomeMb.Traits.SequenceEqual(UnitCatalog.TomeMa.Traits.Append(TraitId.FeatherLoss)));

        // (c) 規定（M-b）の `compare` が docs/balance.md と一致
        var bal = ReadBalance();
        var g0 = CompareGrid(VerOf("Mb"));
        int bad = 0, cells = 0;
        var rows = Rows;
        for (int ri = 0; ri < rows.Length; ri++)
        {
            if (!bal.TryGetValue(rows[ri].Name, out var cellsRow)) { bad++; continue; }
            for (int w = 0; w < Main.Length; w++) { cells++; if (Math.Abs(cellsRow[w] - g0[ri, w]) > 0.05) bad++; }
        }
        Expect("(c) 規定（M-b）の `compare` 64 行 × 5 波が `docs/balance.md` と一致", bad == 0, $"{cells} セル中 {bad} 件ずれ");

        // (d) 羽の帳簿: 初期 1・下限 1・増えるのは敵への書き込みだけ
        long minEnd = long.MaxValue, gainNoWriter = 0, shotsOver = 0, sprayLostOver = 0, logGain = 0, tallyGain = 0;
        var noWriter = Seat(new[] { UnitCatalog.Golm, UnitCatalog.Tome, UnitCatalog.Ban, UnitCatalog.Doha, UnitCatalog.Hisa });   // 敵に標を書く駒（ソラ・ザン）がいない台
        foreach (var v in Vers.Skip(1))
            foreach (var (name, f) in MisaRows.Append(("書き手なし", noWriter)))
                foreach (var w in Main.Skip(1).Append(BossW))
                    for (int s = 0; s < 20; s++)
                    {
                        var p = BattleEngine.Materialize(With(f, v), BattleContext.PlayerTeam);
                        var r = BattleEngine.Run(p, w.Make(), s, verbose: true);
                        if (!r.TallyByUnit.TryGetValue("tome", out var t)) continue;
                        long end = FeathersTrait.Initial + t.FeatherGained - t.FeatherLost;
                        minEnd = Math.Min(minEnd, end);
                        if (name == "書き手なし") gainNoWriter += t.FeatherGained;
                        if (t.FeatherChased + t.FeatherSprayed > t.FeatherShots) shotsOver++;
                        if (t.FeatherLost > t.FeatherSprayed) sprayLostOver++;
                        if (v.Name == "Ma" && t.FeatherLost != 0) sprayLostOver++;
                        logGain += r.Log.Count(l => l.Text.Contains("の羽が1枚増えた", StringComparison.Ordinal));
                        tallyGain += t.FeatherGained;
                    }
        Expect("(d) 羽は戦の終わりに 1 枚以上（下限 1）", minEnd >= 1, $"最小 {minEnd}");
        Expect("(d) 敵に標を書く駒がいない台では羽は増えない（味方への標・ヒサの矢面では増えない）", gainNoWriter == 0, $"{gainNoWriter}");
        Expect("(d) 撃った発 ≦ 一振りの初めの羽の和・失った羽 ≦ 乱射した発・M-a は羽を失わない", shotsOver == 0 && sprayLostOver == 0, $"{shotsOver} ／ {sprayLostOver}");
        Expect("(d) 羽が増えたログの数 ＝ 帳簿の増えた羽", logGain == tallyGain && tallyGain > 0, $"{logGain} ／ {tallyGain}");

        // (e) 標を追う発は必ず標持ちに当たる（介入に吸われない・層の打点が乗る）
        long chased = 0, fires = 0;
        foreach (var (name, f) in MisaRows)
            foreach (var w in Main.Skip(1))
            {
                var a = Measure(With(f, VerOf("Ma")), w, 0, 50);
                chased += a.Chased; fires += a.RFires;
            }
        Expect("(e) 標を追った発 ＝ 標持ちに当たった発（介入・回避で標の無い相手に流れた発が 0）", chased == fires && chased > 0, $"{chased} ／ {fires}");

        // (f) PickOne を新たに使っていない（BattleCore の呼び出しの数が第284期と同じ）
        int pick = CountPickOne();
        Expect($"(f) BattleCore の `PickOne(` の数が第284期と同じ（{PickOneAt284}）", pick == PickOneAt284, $"{pick}");

        Console.WriteLine();
        Console.WriteLine(fail == 0 ? "**全項目 ○**" : $"**× が {fail} 項目**");
    }

    /// <summary>第284期（ff2a1f9）の BattleCore/*.cs の `PickOne(` の出現数（定義 1 を含む）。</summary>
    const int PickOneAt284 = 36;

    static int CountPickOne()
    {
        string? d = AppContext.BaseDirectory;
        while (d is not null && !File.Exists(Path.Combine(d, "CLAUDE.md"))) d = Path.GetDirectoryName(d);
        d ??= Directory.GetCurrentDirectory();
        return Directory.GetFiles(Path.Combine(d, "BattleCore"), "*.cs")
            .Sum(f => System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f), @"PickOne\(").Count);
    }

    static Dictionary<string, double[]> ReadBalance()
    {
        string? d = AppContext.BaseDirectory;
        while (d is not null && !File.Exists(Path.Combine(d, "CLAUDE.md"))) d = Path.GetDirectoryName(d);
        d ??= Directory.GetCurrentDirectory();
        var res = new Dictionary<string, double[]>();
        foreach (string line in File.ReadAllLines(Path.Combine(d, "docs", "balance.md")))
        {
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            var cols = line.Split('|').Select(c => c.Trim()).Where(c => c.Length > 0).ToArray();
            if (cols.Length < 6) continue;
            var vals = cols.Skip(1).Take(5).Select(c => double.TryParse(c.TrimEnd('%'), out double x) ? x : double.NaN).ToArray();
            if (vals.Any(double.IsNaN)) continue;
            res[cols[0]] = vals;
        }
        return res;
    }

    // ---------------------------------------------------------------------------------
    // 1戦のログ
    // ---------------------------------------------------------------------------------
    static void LogOne(string board, string ver, string wave, int seed)
    {
        Formation f = board.StartsWith("boss:")
            ? Seat(Boss283Diag.CtlBoards[int.Parse(board[5..])].Order)
            : Rows.First(r => r.Name.StartsWith(board)).F;
        var v = VerOf(ver);
        var w = WaveOf(wave);
        var p = BattleEngine.Materialize(With(f, v), BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: true);
        Console.WriteLine($"# misa285 log —— {board} × {v.Name} × {w.Name} × seed {seed}（{(r.PlayerWon ? "勝ち" : "負け")}・{r.Turns}T）");
        Console.WriteLine();
        foreach (var l in r.Log) Console.WriteLine(l.Text);
    }
}
