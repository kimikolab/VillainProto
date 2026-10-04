using BattleCore;
using static Common;

// =====================================================================================
// relic p0j ／ grid ／ junk —— 第271期「レリックの本判定（総当たり・固有の勝者・混成率・ゴミの成立）」。
// 指示書は design/PHASE271_RELIC_JUDGE_SPEC.md ／ 報告は design/PHASE271_RELIC_JUDGE.md。
//
//     dotnet run --project BattleSim -c Release 0 relic p0j     # Phase 0: 規模の見積もり・61 行の軸タグ・ウツ ／ ガンを含む行（戦闘0回）
//     dotnet run --project BattleSim -c Release 0 relic grid    # 総当たり: 61 行 × （素 ＋ 札7枚 × 枠5）× 波5（ボス規定形 ＋ 本編第2〜5波）× seed 0..199 と、四つの物差し
//     dotnet run --project BattleSim -c Release 0 relic mainwin # 参考（採否には使わない）: 固有の勝者の手続きを本編第2〜5波に当てる＋個性の保存
//     dotnet run --project BattleSim -c Release 0 relic junk    # ゴミの成立だけ（読み手の行 × ゴミの札 × 全枠・対照は読み手のいない行）
//
// **部隊に1枚だけ**（どの枠に付けるかを総当たり）。器の仕様（1枠1枚）は変えず、測りの範囲として絞る。
// 主指標は勝率と倒しT（勝った戦の平均ターン）。**与ダメ系は使わない（R364）。**
// 本編の波は `compare` と同じ呼び方（`BattleEngine.Run(編成, 敵, seed)`・倍率 115/115）、ボスは `bosswave` と同じ（倍率なし）。
// **測定結果は転生の評価に書き戻さない**（第270期の条件・継続）。
// =====================================================================================
static partial class RelicDiag
{
    const int GridSeeds = 200;
    /// <summary>固有の勝者の追試に使う帯（(G14): 席の追試は帯B でよい・選抜に使った帯A とは独立）。</summary>
    const int ConfirmFrom = 200, ConfirmSeeds = 200;
    /// <summary>「有意に正」: 帯A で 5 勝以上（0/200 との片側フィッシャーで p ≈ 0.03）かつ 帯B で素に対し片側フィッシャー p &lt; 0.05。</summary>
    const int MinWinsA = 5;

    static readonly string[] WaveNames = { "ボス", "第2波", "第3波", "第4波", "第5波" };

    // ---------------------------------------------------------------------------------
    // 軸（第264期の核リスト・指示書 §1.4）。両属の駒はいない（4つのリストに重なりが無い）。
    // ---------------------------------------------------------------------------------
    static readonly (string Axis, string[] Ids)[] AxisCores =
    {
        ("燃焼", new[] { "borg", "hota", "hiyo" }),
        ("移動", new[] { "basa", "sero", "yomi", "shio", "hane" }),
        ("雷", new[] { "shiga", "tsugi", "kata" }),
        ("毒", new[] { "guza", "mio", "rau" }),
    };
    static string[] AxesOf(Formation f) => AxisCores.Where(a => f.Occupied().Any(o => a.Ids.Contains(o.Def.Id))).Select(a => a.Axis).ToArray();
    static bool IsMixed(Formation f) => AxesOf(f).Length >= 2;
    static string AxisTag(Formation f) { var a = AxesOf(f); return a.Length == 0 ? "—" : string.Join("+", a); }
    static bool HasUnit(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);

    // ---------------------------------------------------------------------------------
    // 版: 0 ＝ 素、1.. ＝ 札 r × 枠 s（枠が空なら作らない）。
    // ---------------------------------------------------------------------------------
    sealed record GVar(int Ix, TraitId? Relic, int Frame);
    static GVar[] VarsOf(Formation f)
    {
        var list = new List<GVar> { new(0, null, -1) };
        foreach (var r in RelicCatalog.All)
            for (int s = 0; s < FormationRules.PlayableSlotCount; s++)
                if (f[s] is not null) list.Add(new(list.Count, r.Id, s));
        return list.ToArray();
    }
    static Formation Apply(Formation f, GVar v) => v.Relic is TraitId r ? f.WithRelic(v.Frame, r) : f;
    static string VarName(Formation f, GVar v) => v.Relic is TraitId r ? $"{RelicCatalog.Info(r).Name}→{f[v.Frame]!.Name}" : "素";

    static BattleResult FightWave(Formation f, int wave, int seed, bool verbose = false)
    {
        if (wave == 0)
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None);
            return BattleEngine.Run(p, e, seed, verbose: verbose);
        }
        return BattleEngine.Run(f, EnemyCatalog.Stages[wave].Enemy, seed, verbose: verbose);
    }

    readonly record struct Cell(int Wins, long TurnSum, int N)
    {
        public double Win => 100.0 * Wins / N;
        public double T => Wins == 0 ? double.PositiveInfinity : (double)TurnSum / Wins;
        /// <summary>良さの順（勝率 → 倒しT の速さ）。</summary>
        public static int Better(Cell a, Cell b) => a.Wins != b.Wins ? a.Wins.CompareTo(b.Wins) : b.T.CompareTo(a.T);
    }

    static Cell MeasureCell(Formation f, int wave, int from, int n)
    {
        int wins = 0; long ts = 0;
        for (int s = from; s < from + n; s++)
        {
            var r = FightWave(f, wave, s);
            if (r.PlayerWon) { wins++; ts += r.Turns; }
        }
        return new Cell(wins, ts, n);
    }

    /// <summary>片側フィッシャー（b の勝ちが a より多い向き）。</summary>
    static double FisherGreater(int winsA, int nA, int winsB, int nB)
    {
        int k = winsA + winsB, n = nA + nB;
        double LogC(int nn, int kk) => LogFact(nn) - LogFact(kk) - LogFact(nn - kk);
        double denom = LogC(n, k), p = 0;
        for (int x = winsB; x <= Math.Min(k, nB); x++)
            p += Math.Exp(LogC(nB, x) + LogC(nA, k - x) - denom);
        return Math.Min(1, p);
    }
    static double LogFact(int n) { double s = 0; for (int i = 2; i <= n; i++) s += Math.Log(i); return s; }

    // ---------------------------------------------------------------------------------
    // p0j
    // ---------------------------------------------------------------------------------
    static void P0Judge()
    {
        var rows = CompareBuilds();
        int vars = rows.Sum(r => VarsOf(r.F).Length);
        Console.WriteLine("# relic p0j —— 第271期 Phase 0（戦闘0回）");
        Console.WriteLine();
        Console.WriteLine("## 1. 規模");
        Console.WriteLine();
        Console.WriteLine($"- 版: 61 行それぞれ 素 ＋ 札 {RelicCatalog.All.Count} 枚 × 埋まった枠 ＝ **{vars} 版**（行あたり {(double)vars / rows.Length:F1}）");
        Console.WriteLine($"- 戦闘数: {vars} 版 × 波 {WaveNames.Length} × seed {GridSeeds} ＝ **{(long)vars * WaveNames.Length * GridSeeds:N0} 戦**（`compare` 本体は 61 × 5 × 200 ＝ 61,000 戦・5〜6 秒）");
        Console.WriteLine($"- 見積もり: `compare` の約 {(double)vars * WaveNames.Length / (rows.Length * 5):F0} 倍 ≒ 3〜4 分。**絞らない**（seed も波も減らさない）。固有の勝者の追試（帯B seed {ConfirmFrom}..{ConfirmFrom + ConfirmSeeds - 1}）は数行ぶんだけ足す");
        Console.WriteLine();
        Console.WriteLine("## 2. 61 行の軸タグ（第264期の核リスト・2軸以上の核を含む行 ＝ 混成）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 軸 | 混成 | ウツ | ガン |");
        Console.WriteLine("|---|---|:-:|:-:|:-:|");
        foreach (var (name, f) in rows)
            Console.WriteLine($"| {name} | {AxisTag(f)} | {(IsMixed(f) ? "○" : "")} | {(HasUnit(f, "utsu") ? "○" : "")} | {(HasUnit(f, "gan") ? "○" : "")} |");
        Console.WriteLine();
        Console.WriteLine($"- 混成 **{rows.Count(r => IsMixed(r.F))}** 行 ／ 単軸 {rows.Count(r => AxesOf(r.F).Length == 1)} 行 ／ 核なし {rows.Count(r => AxesOf(r.F).Length == 0)} 行");
        Console.WriteLine($"- ウツを含む行 **{rows.Count(r => HasUnit(r.F, "utsu"))}**・ガンを含む行 **{rows.Count(r => HasUnit(r.F, "gan"))}**（臨時の検証台は不要）");
        Console.WriteLine();
        Console.WriteLine("## 3. ガンを含む行に、動かされる駒がいるか（痺れる足の供給の条件）");
        Console.WriteLine();
        var movers = AxisCores.First(a => a.Axis == "移動").Ids;
        foreach (var (name, f) in rows.Where(r => HasUnit(r.F, "gan")))
            Console.WriteLine($"- {name}: {string.Join("・", f.Occupied().Select(o => o.Def.Name))}——移動の核 {f.Occupied().Count(o => movers.Contains(o.Def.Id))} 枚");
    }

    // ---------------------------------------------------------------------------------
    // grid: 総当たりと四つの物差し
    // ---------------------------------------------------------------------------------
    sealed class GridData
    {
        public required (string Name, Formation F)[] Rows;
        public required GVar[][] Vars;
        public required Cell[][][] C;   // [行][版][波]
    }

    static GridData RunGrid()
    {
        var rows = CompareBuilds();
        var vars = rows.Select(r => VarsOf(r.F)).ToArray();
        var c = rows.Select((r, i) => vars[i].Select(_ => new Cell[WaveNames.Length]).ToArray()).ToArray();
        var tasks = new List<(int R, int V, int W)>();
        for (int i = 0; i < rows.Length; i++)
            for (int v = 0; v < vars[i].Length; v++)
                for (int w = 0; w < WaveNames.Length; w++) tasks.Add((i, v, w));
        Parallel.ForEach(tasks, t =>
        {
            var f = Apply(rows[t.R].F, vars[t.R][t.V]);
            c[t.R][t.V][t.W] = MeasureCell(f, t.W, 0, GridSeeds);
        });
        return new GridData { Rows = rows, Vars = vars, C = c };
    }

    static (GVar V, Cell C) BestVar(GridData g, int row, int wave, bool relicOnly)
    {
        GVar? bv = null; Cell bc = default;
        foreach (var v in g.Vars[row])
        {
            if (relicOnly && v.Relic is null) continue;
            var c = g.C[row][v.Ix][wave];
            if (bv is null || Cell.Better(c, bc) > 0) { bv = v; bc = c; }
        }
        return (bv!, bc);
    }

    static string WT(Cell c) => $"{c.Win:F1}%" + (c.Wins > 0 ? $"・T{c.T:F1}" : "");

    static void Grid()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var g = RunGrid();
        var rows = g.Rows;
        Console.WriteLine($"# relic grid —— 第271期 総当たり（61 行 × 札7枚 × 枠5・部隊に1枚 × 波5・seed 0..{GridSeeds - 1}）");
        Console.WriteLine();
        Console.WriteLine($"総当たり {sw.Elapsed.TotalSeconds:F0} 秒。主指標は勝率と倒しT（勝った戦の平均ターン）。ボスは規定形（倍率なし）、本編は `compare` と同じ（倍率 115/115）。");
        Console.WriteLine();

        // ---- 回帰: 素の版の本編4波は docs/balance.md と一致するか ----
        {
            var bal = File.ReadAllLines("docs/balance.md")
                .Where(l => l.StartsWith("| ") && l.Contains('%'))
                .Select(l => l.Split('|', StringSplitOptions.TrimEntries)).ToDictionary(p => p[1], p => p);
            int ok = 0, ng = 0;
            for (int i = 0; i < rows.Length; i++)
                for (int w = 1; w < WaveNames.Length; w++)
                {
                    string want = bal[rows[i].Name][w + 2];   // 列: [空, 編成, 第1波, 第2波, …]
                    if ($"{g.C[i][0][w].Win:F1}%" == want) ok++; else ng++;
                }
            Console.WriteLine($"回帰: 素の版の本編第2〜5波 × 61 行 ＝ {ok + ng} セルのうち `docs/balance.md` と一致 **{ok}**・不一致 **{ng}**。");
            Console.WriteLine();
        }

        // ---- 物差し1: 固有の勝者（ボス） ----
        Console.WriteLine("## 1. 固有の勝者（ボスで素 0% → 札1枚で有意に正）");
        Console.WriteLine();
        Console.WriteLine($"選抜は帯A（seed 0..{GridSeeds - 1}）で最良の版（勝率 → 倒しT）。**帯A で {MinWinsA} 勝以上**の行だけを、帯B（seed {ConfirmFrom}..{ConfirmFrom + ConfirmSeeds - 1}）で素と最良の版を測り直し、片側フィッシャー p < 0.05 なら固有の勝者に数える（35 版から最良を選ぶ選抜の偏りを帯B で外す）。");
        Console.WriteLine();
        var zeroRows = Enumerable.Range(0, rows.Length).Where(i => g.C[i][0][0].Wins == 0).ToList();
        Console.WriteLine($"ボスで素 0% の行: **{zeroRows.Count}** ／ {rows.Length}");
        Console.WriteLine();
        Console.WriteLine("| 行 | 軸 | 最良の版（帯A） | 帯A | 帯A で 5 勝以上の版の数 | 帯B 素 | 帯B 版 | p | 判定 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|:-:|");
        var winners = new List<(int Row, GVar V)>();
        var lines = new (int I, string Line)[zeroRows.Count];
        Parallel.For(0, zeroRows.Count, k =>
        {
            int i = zeroRows[k];
            var (bv, bc) = BestVar(g, i, 0, relicOnly: true);
            int many = g.Vars[i].Count(v => v.Relic is not null && g.C[i][v.Ix][0].Wins >= MinWinsA);
            if (bc.Wins < MinWinsA) { lines[k] = (i, $"| {rows[i].Name} | {AxisTag(rows[i].F)} | {VarName(rows[i].F, bv)} | {WT(bc)} | {many} | | | | |"); return; }
            var b0 = MeasureCell(rows[i].F, 0, ConfirmFrom, ConfirmSeeds);
            var b1 = MeasureCell(Apply(rows[i].F, bv), 0, ConfirmFrom, ConfirmSeeds);
            double p = FisherGreater(b0.Wins, b0.N, b1.Wins, b1.N);
            bool win = p < 0.05;
            if (win) lock (winners) winners.Add((i, bv));
            lines[k] = (i, $"| {rows[i].Name} | {AxisTag(rows[i].F)} | {VarName(rows[i].F, bv)} | {WT(bc)} | {many} | {WT(b0)} | {WT(b1)} | {p:F3} | {(win ? "**○**" : "×")} |");
        });
        foreach (var (_, l) in lines.Where(x => x.Line.Contains(" | **○** |") || x.Line.Contains(" | × |"))) Console.WriteLine(l);
        Console.WriteLine();
        int below = lines.Count(x => !x.Line.Contains(" | **○** |") && !x.Line.Contains(" | × |"));
        Console.WriteLine($"（帯A で {MinWinsA} 勝に届かなかった {below} 行は省略——表4 にボスの最良の版を全行載せる）");
        Console.WriteLine();
        winners.Sort((a, b) => a.Row.CompareTo(b.Row));
        Console.WriteLine($"**固有の勝者: {winners.Count} 行**（下限 ≧ 3）。札の内訳: " + string.Join("・", winners.GroupBy(w => RelicCatalog.Info(w.V.Relic!.Value).Name).Select(gr => $"{gr.Key} {gr.Count()}")));
        Console.WriteLine();
        Console.WriteLine("帯A で 5 勝以上に届いた版を、札ごとに数えた（固有の勝者の候補の広がり・行の重複あり）:");
        Console.WriteLine();
        foreach (var r in RelicCatalog.All)
        {
            int rowsHit = zeroRows.Count(i => g.Vars[i].Any(v => v.Relic == r.Id && g.C[i][v.Ix][0].Wins >= MinWinsA));
            Console.WriteLine($"- {r.Name}: {rowsHit} 行");
        }
        Console.WriteLine();

        // ---- 物差し4: 個性の保存（固有の勝者の行） ----
        Console.WriteLine("## 2. 個性の保存（固有の勝者の行・札を付けた駒・ボス・seed 0..199・20 ターンまで）");
        Console.WriteLine();
        Console.WriteLine("付けた駒の「元の役割の指標」は素の版で最も大きい量（与ダメ ／ 回復 ／ 状態の付与回数 ／ 庇い〈肩代わりで受けた量〉）。札ありでその量が素の半分以上残れば ○。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 駒 | 与ダメ 素 → 札 | 回復 素 → 札 | 状態の付与 素 → 札 | 受けたダメ 素 → 札 | 主の指標 | 判定 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|---|:-:|");
        foreach (var (i, v) in winners) Console.WriteLine(KeepLine(rows[i].Name, rows[i].F, v));
        Console.WriteLine();

        // ---- 物差し2: 混成率 ----
        Console.WriteLine("## 3. 混成率（各波の最良の行が混成か・素 → 札あり）");
        Console.WriteLine();
        Console.WriteLine("最良 ＝ 勝率 → 倒しT（速いほど良い）で並べた1位。札ありは各行の最良の版（素を含む）。**混成** ＝ 2軸以上の核を含む行。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 素の1位 | 混成 | 札ありの1位 | 混成 | 素: 混成の最良 − 1位（倒しT・勝率） | 札あり: 同 |");
        Console.WriteLine("|---|---|:-:|---|:-:|---|---|");
        int turned = 0, narrowed = 0;
        for (int w = 0; w < WaveNames.Length; w++)
        {
            int Top(Func<int, Cell> cellOf, IEnumerable<int> idx) => idx.Aggregate((a, b) => Cell.Better(cellOf(b), cellOf(a)) > 0 ? b : a);
            var all = Enumerable.Range(0, rows.Length).ToList();
            var mixed = all.Where(i => IsMixed(rows[i].F)).ToList();
            Cell Base(int i) => g.C[i][0][w];
            Cell Rel(int i) => BestVar(g, i, w, relicOnly: false).C;
            int t0 = Top(Base, all), t1 = Top(Rel, all), m0 = Top(Base, mixed), m1 = Top(Rel, mixed);
            string Gap(Cell top, Cell m) => $"{m.Win - top.Win:+0.0;−0.0;±0.0}pt ／ {(m.Wins == 0 || top.Wins == 0 ? "—" : (m.T - top.T).ToString("+0.00;−0.00;±0.00") + "T")}";
            bool mix0 = IsMixed(rows[t0].F), mix1 = IsMixed(rows[t1].F);
            if (!mix0 && mix1) turned++;
            double gap0 = Base(m0).Win - Base(t0).Win, gap1 = Rel(m1).Win - Rel(t1).Win;
            double tg0 = Base(m0).T - Base(t0).T, tg1 = Rel(m1).T - Rel(t1).T;
            if (gap1 > gap0 || (gap1 == gap0 && tg1 < tg0)) narrowed++;
            var bv = BestVar(g, t1, w, relicOnly: false).V;
            Console.WriteLine($"| {WaveNames[w]} | {rows[t0].Name}（{WT(Base(t0))}） | {(mix0 ? "○" : "")} | {rows[t1].Name} 〔{VarName(rows[t1].F, bv)}〕（{WT(Rel(t1))}） | {(mix1 ? "○" : "")} | {Gap(Base(t0), Base(m0))} | {Gap(Rel(t1), Rel(m1))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"**最良が単軸 → 混成に変わった波: {turned}**（下限 ≧ 1）・混成の最良と1位の差が札で縮んだ波: **{narrowed}** ／ {WaveNames.Length}");
        Console.WriteLine();
        Console.WriteLine("参考: 混成の行と単軸の行で、札による伸び（最良の版 − 素の勝率）の平均（第2〜5波は勝率の余地がある行 ＝ 素 < 100% だけ）:");
        Console.WriteLine();
        Console.WriteLine("| 波 | 混成: 行数・平均の伸び | 単軸: 行数・平均の伸び | 核なし: 行数・平均の伸び |");
        Console.WriteLine("|---|--:|--:|--:|");
        for (int w = 0; w < WaveNames.Length; w++)
        {
            string Avg(Func<int, bool> pick)
            {
                var ix = Enumerable.Range(0, rows.Length).Where(i => pick(i) && g.C[i][0][w].Wins < GridSeeds).ToList();
                return ix.Count == 0 ? "0・—" : $"{ix.Count}・{ix.Average(i => BestVar(g, i, w, false).C.Win - g.C[i][0][w].Win):+0.0}pt";
            }
            Console.WriteLine($"| {WaveNames[w]} | {Avg(i => IsMixed(rows[i].F))} | {Avg(i => AxesOf(rows[i].F).Length == 1)} | {Avg(i => AxesOf(rows[i].F).Length == 0)} |");
        }
        Console.WriteLine();

        // ---- 物差し3: ゴミの成立 ----
        JunkTables(g);

        // ---- 2.3: 既存波への影響の桁 ----
        Console.WriteLine("## 5. 既存波への影響の桁（札1枚で勝率が動く幅・行 × 版の最大）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 最大の上げ（行・版） | 最大の下げ（行・版） | 素 < 100% の行のうち、最良の版で +5pt 以上 | 札ごとの最大の上げ |");
        Console.WriteLine("|---|---|---|--:|---|");
        for (int w = 1; w < WaveNames.Length; w++)
        {
            (double D, int I, GVar V) up = (double.MinValue, 0, g.Vars[0][0]), dn = (double.MaxValue, 0, g.Vars[0][0]);
            for (int i = 0; i < rows.Length; i++)
                foreach (var v in g.Vars[i].Where(v => v.Relic is not null))
                {
                    double d = g.C[i][v.Ix][w].Win - g.C[i][0][w].Win;
                    if (d > up.D) up = (d, i, v);
                    if (d < dn.D) dn = (d, i, v);
                }
            int room = Enumerable.Range(0, rows.Length).Count(i => g.C[i][0][w].Wins < GridSeeds);
            int lifted = Enumerable.Range(0, rows.Length).Count(i => g.C[i][0][w].Wins < GridSeeds && BestVar(g, i, w, true).C.Win - g.C[i][0][w].Win >= 5);
            string per = string.Join("・", RelicCatalog.All.Select(r =>
            {
                double m = Enumerable.Range(0, rows.Length).Max(i => g.Vars[i].Where(v => v.Relic == r.Id).Select(v => g.C[i][v.Ix][w].Win - g.C[i][0][w].Win).DefaultIfEmpty(0).Max());
                return $"{r.Name} {m:+0.0;−0.0;±0.0}";
            }));
            Console.WriteLine($"| {WaveNames[w]} | {up.D:+0.0;−0.0;±0.0}pt（{rows[up.I].Name}・{VarName(rows[up.I].F, up.V)}） | {dn.D:+0.0;−0.0;±0.0}pt（{rows[dn.I].Name}・{VarName(rows[dn.I].F, dn.V)}） | {lifted} ／ {room} | {per} |");
        }
        Console.WriteLine();

        // ---- 表4: 全行 × ボスの素と最良の版 ----
        Console.WriteLine("## 6. 表4 全 61 行 × ボス（素 ／ 最良の版・帯A）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 軸 | 素 | 最良の版 | 勝率・倒しT |");
        Console.WriteLine("|---|---|--:|---|--:|");
        for (int i = 0; i < rows.Length; i++)
        {
            var (bv, bc) = BestVar(g, i, 0, relicOnly: true);
            Console.WriteLine($"| {rows[i].Name} | {AxisTag(rows[i].F)} | {WT(g.C[i][0][0])} | {VarName(rows[i].F, bv)} | {WT(bc)} |");
        }
        Console.WriteLine();

        // ---- 身を固めるの符号（予測3） ----
        Console.WriteLine("## 7. 身を固める: ボスで勝率が上がった行 ／ 下がった行（最良の枠・最悪の枠）");
        Console.WriteLine();
        Console.WriteLine("| 軸 | 行数 | 最良の枠で +1pt 以上の行 | 全枠で −1pt 以下の行 |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var grp in Enumerable.Range(0, rows.Length).GroupBy(i => AxisTag(rows[i].F)).OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            int up = grp.Count(i => g.Vars[i].Where(v => v.Relic == TraitId.RelicHarden).Max(v => g.C[i][v.Ix][0].Win) - g.C[i][0][0].Win >= 1);
            int dn = grp.Count(i => g.Vars[i].Where(v => v.Relic == TraitId.RelicHarden).Max(v => g.C[i][v.Ix][0].Win) - g.C[i][0][0].Win <= -1);
            Console.WriteLine($"| {grp.Key} | {grp.Count()} | {up} | {dn} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 個性の保存の1行（ボス・verbose・seed 0..199・20 ターンまで）
    // ---------------------------------------------------------------------------------
    static string KeepLine(string name, Formation f0, GVar v, int wave = 0)
    {
        int holderIx = f0.Occupied().TakeWhile(o => o.Slot != v.Frame).Count();
        (double Dealt, double Healed, double Status, double Taken) M(Formation f)
        {
            long d = 0, h = 0, st = 0, tk = 0; var lk = new object();
            Parallel.For(0, GridSeeds, s =>
            {
                var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                var e = wave == 0 ? BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None)
                                  : BattleEngine.Materialize(EnemyCatalog.Stages[wave].Enemy, BattleContext.EnemyTeam);
                var r = BattleEngine.Run(p, e, s, verbose: true);
                int id = p[holderIx].InstanceId; long d1 = 0, h1 = 0, s1 = 0, t1 = 0;
                foreach (var x in r.Events)
                {
                    if (x.Turn > Window) continue;
                    if (x.Kind == BattleEventKind.Damage && x.ActorId == id && x.TargetId is int t && e.Any(u => u.InstanceId == t)) d1 += x.Amount;
                    else if (x.Kind == BattleEventKind.Heal && x.ActorId == id) h1 += x.Amount;
                    else if (x.Kind == BattleEventKind.StatusGain && x.ActorId == id) s1++;
                    if (x.Kind == BattleEventKind.Damage && x.TargetId == id) t1 += x.Amount;
                }
                lock (lk) { d += d1; h += h1; st += s1; tk += t1; }
            });
            return ((double)d / GridSeeds, (double)h / GridSeeds, (double)st / GridSeeds, (double)tk / GridSeeds);
        }
        var a = M(f0); var b = M(Apply(f0, v));
        var opts = new (string N, double A, double B)[] { ("与ダメ", a.Dealt, b.Dealt), ("回復", a.Healed, b.Healed), ("状態の付与", a.Status, b.Status), ("受けたダメ", a.Taken, b.Taken) };
        var main = opts.OrderByDescending(o => o.N == "受けたダメ" ? o.A * 0.25 : o.A).First();   // 受けた量は「壁」の駒でだけ主になる（重みを下げる）
        bool kept = main.A <= 0 || main.B >= main.A * 0.5;
        return $"| {name} | {VarName(f0, v)} | {f0[v.Frame]!.Name} | {a.Dealt:F0} → {b.Dealt:F0} | {a.Healed:F0} → {b.Healed:F0} | {a.Status:F1} → {b.Status:F1} | {a.Taken:F0} → {b.Taken:F0} | {main.N} | {(kept ? "○" : "**×**")} |";
    }

    // ---------------------------------------------------------------------------------
    // 参考（第271期・採否には使わない）: 固有の勝者の手続きを本編第2〜5波に当てる。
    // 指示書の物差しはボスで定義してある。結果を見てから分母を替えない（第64期）ので、これは次の期の判断材料。
    // ---------------------------------------------------------------------------------
    static void MainWin()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var g = RunGrid();
        var rows = g.Rows;
        Console.WriteLine("# relic mainwin —— 第271期 参考: 固有の勝者の手続きを本編第2〜5波に当てる（**採否には使わない**）");
        Console.WriteLine();
        Console.WriteLine($"手続きはボスと同じ（帯A で素 0% → 最良の版が {MinWinsA} 勝以上 → 帯B seed {ConfirmFrom}..{ConfirmFrom + ConfirmSeeds - 1} で片側フィッシャー p < 0.05）。個性の保存も同じ波で測る。");
        Console.WriteLine();
        var hits = new List<(int Row, int W, GVar V)>();
        for (int w = 1; w < WaveNames.Length; w++)
        {
            var zero = Enumerable.Range(0, rows.Length).Where(i => g.C[i][0][w].Wins == 0).ToList();
            Console.WriteLine($"## {WaveNames[w]}（素 0% の行: {zero.Count}）");
            Console.WriteLine();
            Console.WriteLine("| 行 | 軸 | 最良の版（帯A） | 帯A | 5 勝以上の版の数 | 帯B 素 | 帯B 版 | p | 判定 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|:-:|");
            var lines = new string?[zero.Count];
            Parallel.For(0, zero.Count, k =>
            {
                int i = zero[k];
                var (bv, bc) = BestVar(g, i, w, relicOnly: true);
                int many = g.Vars[i].Count(v => v.Relic is not null && g.C[i][v.Ix][w].Wins >= MinWinsA);
                if (bc.Wins < MinWinsA) return;
                var b0 = MeasureCell(rows[i].F, w, ConfirmFrom, ConfirmSeeds);
                var b1 = MeasureCell(Apply(rows[i].F, bv), w, ConfirmFrom, ConfirmSeeds);
                double p = FisherGreater(b0.Wins, b0.N, b1.Wins, b1.N);
                if (p < 0.05) lock (hits) hits.Add((i, w, bv));
                lines[k] = $"| {rows[i].Name} | {AxisTag(rows[i].F)} | {VarName(rows[i].F, bv)} | {WT(bc)} | {many} | {WT(b0)} | {WT(b1)} | {p:F3} | {(p < 0.05 ? "**○**" : "×")} |";
            });
            foreach (var l in lines) if (l is not null) Console.WriteLine(l);
            Console.WriteLine();
            Console.WriteLine($"固有の勝者（この波）: **{hits.Count(h => h.W == w)} 行**");
            Console.WriteLine();
        }
        Console.WriteLine($"## 合計: {hits.Count} （行 × 波）・行の重複を除くと {hits.Select(h => h.Row).Distinct().Count()} 行。札の内訳: "
            + string.Join("・", hits.GroupBy(h => RelicCatalog.Info(h.V.Relic!.Value).Name).OrderByDescending(x => x.Count()).Select(x => $"{x.Key} {x.Count()}")));
        Console.WriteLine();
        Console.WriteLine("## 個性の保存（上の固有の勝者 × その波・seed 0..199・20 ターンまで）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 版 | 駒 | 与ダメ 素 → 札 | 回復 素 → 札 | 状態の付与 素 → 札 | 受けたダメ 素 → 札 | 主の指標 | 判定 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|---|:-:|");
        foreach (var (i, w, v) in hits.OrderBy(h => h.W).ThenBy(h => h.Row))
            Console.WriteLine(KeepLine($"{rows[i].Name}（{WaveNames[w]}）", rows[i].F, v, w));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // ゴミの成立
    // ---------------------------------------------------------------------------------
    static void Junk()
    {
        var g = RunGrid();
        JunkTables(g);
    }

    static void JunkTables(GridData g)
    {
        var rows = g.Rows;
        Console.WriteLine("## 4. ゴミの成立（読み手の行 × ゴミの札・全枠・全波）");
        Console.WriteLine();
        Console.WriteLine("「付けた方が強い」＝ 勝率が上がる（帯A・+1pt 以上）か、勝率が同じ（100% どうし）で倒しT が 0.1T 以上速い。対照は同じ札を**読み手のいない行**に付けたとき。");
        Console.WriteLine();
        foreach (var (relic, reader, readerName) in new[] { (TraitId.RelicWilt, "utsu", "ウツ"), (TraitId.RelicNumbStep, "gan", "ガン") })
        {
            string rn = RelicCatalog.Info(relic).Name;
            Console.WriteLine($"### {readerName} × {rn}");
            Console.WriteLine();
            Console.WriteLine("| 行 | 付け先 | " + string.Join(" | ", WaveNames) + " |");
            Console.WriteLine("|---|---|" + string.Concat(WaveNames.Select(_ => "--:|")));
            int stronger = 0, cells = 0;
            var readerRows = Enumerable.Range(0, rows.Length).Where(i => HasUnit(rows[i].F, reader)).ToList();
            foreach (int i in readerRows)
                foreach (var v in g.Vars[i].Where(v => v.Relic == relic))
                {
                    var parts = new List<string>();
                    for (int w = 0; w < WaveNames.Length; w++)
                    {
                        Cell a = g.C[i][0][w], b = g.C[i][v.Ix][w];
                        bool str = b.Win - a.Win >= 1 || (a.Wins == b.Wins && a.Wins > 0 && b.T <= a.T - 0.1);
                        cells++; if (str) stronger++;
                        parts.Add($"{(str ? "**" : "")}{b.Win - a.Win:+0.0;−0.0;±0.0} ／ {(a.Wins > 0 && b.Wins > 0 ? (b.T - a.T).ToString("+0.0;−0.0;±0.0") : "—")}{(str ? "**" : "")}");
                    }
                    string who = f(rows[i].F, v.Frame);
                    Console.WriteLine($"| {rows[i].Name} | {who}{(rows[i].F[v.Frame]!.Id == reader ? "（読み手本人）" : "")} | " + string.Join(" | ", parts) + " |");
                }
            // 対照: 読み手のいない行
            int cs = 0, cc = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                if (HasUnit(rows[i].F, reader)) continue;
                foreach (var v in g.Vars[i].Where(v => v.Relic == relic))
                    for (int w = 0; w < WaveNames.Length; w++)
                    {
                        Cell a = g.C[i][0][w], b = g.C[i][v.Ix][w];
                        cc++; if (b.Win - a.Win >= 1 || (a.Wins == b.Wins && a.Wins > 0 && b.T <= a.T - 0.1)) cs++;
                    }
            }
            Console.WriteLine();
            Console.WriteLine($"セルの値は 勝率の差 pt ／ 倒しT の差（太字 ＝ 付けた方が強い）。**読み手の行: {stronger} ／ {cells} セルで付けた方が強い**（{100.0 * stronger / Math.Max(1, cells):F1}%）・対照（読み手のいない行）: {cs} ／ {cc}（{100.0 * cs / Math.Max(1, cc):F1}%）");
            Console.WriteLine();
        }
        static string f(Formation ff, int s) => ff[s]!.Name;
    }
}
