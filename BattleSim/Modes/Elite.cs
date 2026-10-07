using BattleCore;
using static Common;

// =====================================================================================
// elite —— 第284期「精鋭波（HP 1000% ／ 攻 300%）を耐久と火力の評価軸として正式化する」。
// 指示書は design/PHASE284_ELITE_WAVE_SPEC.md ／ 報告は design/PHASE284_ELITE_WAVE.md。
//
//     dotnet run --project BattleSim -c Release 0 elite > docs/elite.md   # 本体: compare 全行 × {近衛, 大隊} × seed 0..199 ＋ 参考の刻み
//     dotnet run --project BattleSim -c Release 0 elite p0       # Phase 0: 桁・写しのキャッシュ・打ち切りの値・乱射の累積・決定性（seed 0..19）
//     dotnet run --project BattleSim -c Release 0 elite poison   # §5-3: 毒 (グザ×ミオ×ラウ) × 大隊 × seed 0 の1本（層・毒の与ダメ・崩れT）
//     dotnet run --project BattleSim -c Release 0 elite check    # 自己検査 (a) 素の波の一致 (b) §7 の照合（seed 0..49）
//
// **精鋭の波は `TestStages` に足さない**（`nine check` が `TestStages.Count == 3` を門にしている・DemoApp の選択口も数える）。
// 定義はこのファイルの中で閉じ、**読むのは `elite` だけ**。波ルール（粛・渇き・軛・断罪）は1つも掛けない——差分はスケールだけ。
// =====================================================================================
static class EliteDiag
{
    public static void Run(string[] args, int stageIndex)
    {
        string mode = args.Length > 2 ? args[2] : "run";
        switch (mode)
        {
            case "run": Body(); return;
            case "p0": P0(); return;
            case "poison": Poison(args.Length > 3 ? int.Parse(args[3]) : 0); return;
            case "check": Check(); return;
            default: Console.WriteLine("elite: モードは run（既定）/ p0 / poison / check。"); return;
        }
    }

    // ---------------------------------------------------------------------------------
    // 定義（指示書 §2・測る前に固定）
    // ---------------------------------------------------------------------------------
    /// <summary>近衛 ＝ 討伐隊の新兵（45/11/6）× 5・X 字。素の構成は `TestStages[2]`（検証・五 / 新兵）と同じ。</summary>
    internal static readonly EnemyWave Five = EnemyWave.FillX(EnemyCatalog.Recruit);
    /// <summary>大隊 ＝ 駆り出された農兵（30/8/6）× 9・全席。素の構成は `TestStages[1]`（検証・九 / 農兵）と同じ。</summary>
    internal static readonly EnemyWave Nine = EnemyWave.FillAll(EnemyCatalog.Levy);
    /// <summary>照合用（§7 の「九・新兵」列）。主表には出さない。</summary>
    internal static readonly EnemyWave NineRecruit = EnemyWave.FillAll(EnemyCatalog.Recruit);

    /// <summary>採否に使う倍率（§2-2）。</summary>
    internal static readonly EnemyScaleRule Elite = new(1000, 300);
    /// <summary>参考の刻み（§5-2・**採否に使わない**）。</summary>
    static readonly EnemyScaleRule[] Ref = { new(1000, 200), new(1000, 250) };

    const int Seeds = 200;

    static readonly (string Name, EnemyWave W)[] Waves = { ("近衛", Five), ("大隊", Nine) };

    /// <summary>
    /// 解法の軸（§5-1）。**測る前に行名で固定**——先に当たった語で決める（「毒→被弾強化」は被弾強化、「燃焼×刻み」は燃焼）。
    /// </summary>
    static readonly (string Key, string Axis)[] AxisRules =
    {
        ("被弾強化", "被弾強化"), ("速攻", "被弾強化"),
        ("燃焼", "燃焼"), ("火選り", "燃焼"), ("灯", "燃焼"), ("ヒヨ", "燃焼"), ("範囲耐性", "燃焼"),
        ("毒", "毒"), ("澱み喰い", "毒"),
        ("見境", "標"), ("標経済", "標"), ("仇討ち", "標"),
        ("鱗", "鱗"), ("礫", "鱗"),
        ("反撃", "反撃"), ("逸らし (ソラ×カド)", "反撃"),
        ("隊列崩し", "移動"), ("突き出し", "移動"), ("移動", "移動"),
        ("逆しま", "逆しま"), ("突き返し", "逆しま"),
        ("刻み", "刻み"), ("逸らし改", "刻み"),
        ("死", "死の連鎖"), ("駆り立て改", "死の連鎖"),
        ("耐久", "回復・肩代わり"), ("分散回復", "回復・肩代わり"), ("引き受け", "回復・肩代わり"), ("渡し", "回復・肩代わり"),
        ("裂き", "裂き・責め苦"), ("責め苦", "裂き・責め苦"),
        ("溜め", "溜め・縛め"), ("縛め", "溜め・縛め"), ("据え", "溜め・縛め"),
        ("感電", "感電"), ("駆り立て", "駆り立て"), ("後備え", "後備え"),
    };
    internal static string AxisOf(string name) => AxisRules.FirstOrDefault(a => name.Contains(a.Key)).Axis ?? "その他";

    // ---------------------------------------------------------------------------------
    // 1戦と集計
    // ---------------------------------------------------------------------------------
    internal readonly record struct One(bool Won, int Turns, bool Timeout, bool AllSurv);

    internal static One Fight(Formation f, EnemyWave w, EnemyScaleRule sc, int seed)
    {
        var r = BattleEngine.Run(BattleEngine.Materialize(f, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(w, sc), seed, verbose: false);
        // 打ち切り ＝ 30T に達して両陣営とも立っている（Phase 0 §2）。30T 目に全滅した負けは打ち切りに数えない。
        bool timeout = !r.PlayerWon && r.Turns >= BattleEngine.MaxTurns && r.PlayerSurvivors > 0;
        return new One(r.PlayerWon, r.Turns, timeout, r.PlayerWon && r.PlayerStarterFallen.Count == 0);
    }

    internal sealed class Cell
    {
        public int N, Wins, Timeouts, AllSurv; public long WinT, LossT;
        public double Win => 100.0 * Wins / Math.Max(1, N);
        public double TO => 100.0 * Timeouts / Math.Max(1, N);
        public double Surv => 100.0 * AllSurv / Math.Max(1, N);
        public double WinTAvg => Wins == 0 ? double.NaN : (double)WinT / Wins;
        public double LossTAvg => N == Wins ? double.NaN : (double)LossT / (N - Wins);
        public void Add(One o) { N++; if (o.Won) { Wins++; WinT += o.Turns; } else LossT += o.Turns; if (o.Timeout) Timeouts++; if (o.AllSurv) AllSurv++; }
    }

    /// <summary>行 × 波 × seed を一括で回す（seed ごとの結果も返す——自己検査が戦ごとに突き合わせる）。</summary>
    internal static One[,,] Grid((string Name, Formation F)[] rows, EnemyWave[] ws, EnemyScaleRule sc, int seed0, int seeds)
    {
        var res = new One[rows.Length, ws.Length, seeds];
        Parallel.For(0, rows.Length * ws.Length * seeds, j =>
        {
            int s = j % seeds, c = j / seeds % ws.Length, r = j / seeds / ws.Length;
            res[r, c, s] = Fight(rows[r].F, ws[c], sc, seed0 + s);
        });
        return res;
    }

    static Cell[,] Cells(One[,,] g)
    {
        var c = new Cell[g.GetLength(0), g.GetLength(1)];
        for (int r = 0; r < g.GetLength(0); r++)
            for (int w = 0; w < g.GetLength(1); w++)
            {
                c[r, w] = new Cell();
                for (int s = 0; s < g.GetLength(2); s++) c[r, w].Add(g[r, w, s]);
            }
        return c;
    }

    static string P(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string T(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static string D(double x) => double.IsNaN(x) ? "—" : x.ToString("+0.0;-0.0;±0.0");

    /// <summary>`docs/balance.md` の第5波の列を行名で読む（無ければ null）。</summary>
    static Dictionary<string, double>? FifthWave()
    {
        string path = Path.Combine("docs", "balance.md");
        if (!File.Exists(path)) return null;
        var d = new Dictionary<string, double>();
        foreach (var line in File.ReadLines(path))
        {
            if (!line.StartsWith("| ") || !line.Contains('%')) continue;
            var cells = line.Split('|').Select(x => x.Trim()).ToArray();
            if (cells.Length < 7) continue;
            if (double.TryParse(cells[6].TrimEnd('%'), out double v)) d.TryAdd(cells[1], v);
        }
        return d;
    }

    // ---------------------------------------------------------------------------------
    // 本体（docs/elite.md）
    // ---------------------------------------------------------------------------------
    static void Body()
    {
        var rows = CompareBuilds();
        var ws = Waves.Select(w => w.W).ToArray();
        var g = Cells(Grid(rows, ws, Elite, 0, Seeds));

        Console.WriteLine("# 精鋭波");
        Console.WriteLine();
        Console.WriteLine("`dotnet run --project BattleSim -c Release 0 elite > docs/elite.md` の出力。手で編集しない。");
        Console.WriteLine($"`compare` の {rows.Length} 行 × 精鋭の2波、seed 0..{Seeds - 1} の {Seeds} 試行。第284期（`design/PHASE284_ELITE_WAVE.md`）。");
        Console.WriteLine();
        Console.WriteLine($"- **近衛** ＝ 討伐隊の新兵 × 5（X 字）、**大隊** ＝ 駆り出された農兵 × 9（全席）。倍率は HP {Elite.HpPercent}% ／ 攻 {Elite.AtkPercent}%（既定の 115/115 の代わりに掛ける）");
        Console.WriteLine($"- 実効値: 五 ＝ HP {Elite.Apply(EnemyCatalog.Recruit).MaxHp}・攻 {Elite.Apply(EnemyCatalog.Recruit).Attack} × 5 ／ 九 ＝ HP {Elite.Apply(EnemyCatalog.Levy).MaxHp}・攻 {Elite.Apply(EnemyCatalog.Levy).Attack} × 9");
        Console.WriteLine("- **波ルール（粛・渇き・軛・断罪）は掛けない**——本編の波との差分はスケールと構成だけ。`Stages` / `TestStages` には載せていない");
        Console.WriteLine($"- 打ち切り ＝ {BattleEngine.MaxTurns}T に達して両陣営とも立っている戦（負けに数える）。全員生存 ＝ 出撃した5枚が1枚も欠けずに勝った戦");
        Console.WriteLine("- **軸テコ入れの期は、この表の該当行を卒業指標として見る**");
        Console.WriteLine();

        Console.WriteLine($"## 主表（HP {Elite.HpPercent}% ／ 攻 {Elite.AtkPercent}%・採否に使う）");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 軸 | 五 勝率 | 五 決着T | 五 打ち切り | 五 全員生存 | 九 勝率 | 九 決着T | 九 打ち切り | 九 全員生存 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int r = 0; r < rows.Length; r++)
        {
            var a = g[r, 0]; var b = g[r, 1];
            Console.WriteLine($"| {rows[r].Name} | {AxisOf(rows[r].Name)} | {P(a.Win)} | {T(a.WinTAvg)} | {P(a.TO)} | {P(a.Surv)} | {P(b.Win)} | {T(b.WinTAvg)} | {P(b.TO)} | {P(b.Surv)} |");
        }
        Console.WriteLine();

        // ---- 要約（§5-1） ----
        Console.WriteLine("## 要約");
        Console.WriteLine();
        Console.WriteLine("| 波 | 平均勝率 | 100% の行 | 0% の行 | 中間（10〜90%）の行 | 情報セル（0<x<100） | 打ち切り率の平均 | 負け戦の決着T（平均） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int w = 0; w < ws.Length; w++)
        {
            var v = Enumerable.Range(0, rows.Length).Select(r => g[r, w].Win).ToArray();
            Console.WriteLine($"| {Waves[w].Name} | {P(v.Average())} | {v.Count(x => x >= 100)} | {v.Count(x => x <= 0)} | {v.Count(x => x >= 10 && x <= 90)} | {v.Count(x => x > 0 && x < 100)} | {P(Enumerable.Range(0, rows.Length).Average(r => g[r, w].TO))} | {T(LossMean(g, w))} |");
        }
        int bothZero = Enumerable.Range(0, rows.Length).Count(r => g[r, 0].Win <= 0 && g[r, 1].Win <= 0);
        Console.WriteLine();
        Console.WriteLine($"五・九とも 0% の行: **{bothZero} / {rows.Length}**。");
        Console.WriteLine();

        // 本編第5波との相関
        var fifth = FifthWave();
        if (fifth is not null)
        {
            Console.WriteLine("### 本編第5波（`docs/balance.md`）との比較");
            Console.WriteLine();
            Console.WriteLine("| 波 | 突き合わせた行 | r（ピアソン） | ρ（スピアマン） | max |Δ| | 同値塊（精鋭側が 0% の行の割合） |");
            Console.WriteLine("|---|--:|--:|--:|--:|--:|");
            for (int w = 0; w < ws.Length; w++)
            {
                var idx = Enumerable.Range(0, rows.Length).Where(r => fifth.ContainsKey(rows[r].Name)).ToArray();
                var x = idx.Select(r => fifth[rows[r].Name]).ToArray();
                var y = idx.Select(r => g[r, w].Win).ToArray();
                var (pr, sp, _) = PearsonSpearman(x, y);
                double maxd = idx.Max(r => Math.Abs(g[r, w].Win - fifth[rows[r].Name]));
                Console.WriteLine($"| {Waves[w].Name} | {idx.Length} | {pr:F3} | {sp:F3} | {P(maxd)} | {P(100.0 * y.Count(v => v <= 0) / y.Length)}% |");
            }
            Console.WriteLine();
            Console.WriteLine("同値塊が大きいので順位相関は判定に使わない（(G13)）。");
            Console.WriteLine();
        }

        // 軸ごと
        Console.WriteLine("### 解法の軸（行名で測る前に固定した分類）");
        Console.WriteLine();
        Console.WriteLine("| 軸 | 行数 | 五 0% を離れた行 | 九 0% を離れた行 | 五 平均 | 九 平均 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        var axes = Enumerable.Range(0, rows.Length).GroupBy(r => AxisOf(rows[r].Name)).OrderBy(gr => gr.Key, StringComparer.Ordinal);
        int axFive = 0, axNine = 0, axAny = 0;
        foreach (var ax in axes)
        {
            int f5 = ax.Count(r => g[r, 0].Win > 0), f9 = ax.Count(r => g[r, 1].Win > 0);
            if (f5 > 0) axFive++; if (f9 > 0) axNine++; if (f5 + f9 > 0) axAny++;
            Console.WriteLine($"| {ax.Key} | {ax.Count()} | {f5} | {f9} | {P(ax.Average(r => g[r, 0].Win))} | {P(ax.Average(r => g[r, 1].Win))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"0% を離れた行がある軸: 五 **{axFive}** ／ 九 **{axNine}** ／ どちらか **{axAny}**。");
        Console.WriteLine();

        // 五と九の符号
        Console.WriteLine("### 五と九で割れた行");
        Console.WriteLine();
        Console.WriteLine("割れ ＝ 片方が 50% 以上・もう片方が 50% 未満（勝ち負けの側が入れ替わる）。参考に「片方だけ 0%」も数える。");
        Console.WriteLine();
        Console.WriteLine("| 編成 | 五 | 九 | Δ 九−五 |");
        Console.WriteLine("|---|--:|--:|--:|");
        int split = 0, oneZero = 0;
        for (int r = 0; r < rows.Length; r++)
        {
            double a = g[r, 0].Win, b = g[r, 1].Win;
            if ((a >= 50) != (b >= 50)) { split++; Console.WriteLine($"| {rows[r].Name} | {P(a)} | {P(b)} | {D(b - a)} |"); }
            if ((a <= 0) != (b <= 0)) oneZero++;
        }
        Console.WriteLine();
        Console.WriteLine($"割れた行: **{split}** ／ 片方だけ 0%: **{oneZero}**。");
        Console.WriteLine();

        // ---- 参考の刻み（§5-2） ----
        Console.WriteLine("## 参考の刻み（**採否に使わない**）");
        Console.WriteLine();
        var refs = Ref.Select(sc => Cells(Grid(rows, ws, sc, 0, Seeds))).ToArray();
        string hdr = string.Concat(Ref.Select(sc => $" 五 {sc.HpPercent}/{sc.AtkPercent} | 九 {sc.HpPercent}/{sc.AtkPercent} |"));
        Console.WriteLine($"| 編成 |{hdr} 五 {Elite.HpPercent}/{Elite.AtkPercent} | 九 {Elite.HpPercent}/{Elite.AtkPercent} |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", Ref.Length * 2 + 2)));
        for (int r = 0; r < rows.Length; r++)
            Console.WriteLine($"| {rows[r].Name} |" + string.Concat(refs.Select(c => $" {P(c[r, 0].Win)} | {P(c[r, 1].Win)} |")) + $" {P(g[r, 0].Win)} | {P(g[r, 1].Win)} |");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 五 平均 | 九 平均 | 五 中間（10〜90%） | 九 中間（10〜90%） | 五 0% | 九 0% |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        var all = refs.Zip(Ref, (c, sc) => (c, sc)).Append((g, Elite)).ToArray();
        foreach (var (c, sc) in all)
        {
            var v5 = Enumerable.Range(0, rows.Length).Select(r => c[r, 0].Win).ToArray();
            var v9 = Enumerable.Range(0, rows.Length).Select(r => c[r, 1].Win).ToArray();
            Console.WriteLine($"| {sc.HpPercent}/{sc.AtkPercent} | {P(v5.Average())} | {P(v9.Average())} | {v5.Count(x => x >= 10 && x <= 90)} | {v9.Count(x => x >= 10 && x <= 90)} | {v5.Count(x => x <= 0)} | {v9.Count(x => x <= 0)} |");
        }
        Console.WriteLine();
    }

    static double LossMean(Cell[,] g, int w)
    {
        long t = 0, n = 0;
        for (int r = 0; r < g.GetLength(0); r++) { t += g[r, w].LossT; n += g[r, w].N - g[r, w].Wins; }
        return n == 0 ? double.NaN : (double)t / n;
    }

    static (double P, double S, int N) PearsonSpearman(double[] x, double[] y)
    {
        double pr = Pearson(x, y);
        double sp = Pearson(Ranks(x), Ranks(y));
        return (pr, sp, x.Length);
    }

    static double[] Ranks(double[] a)
    {
        var idx = Enumerable.Range(0, a.Length).OrderBy(i => a[i]).ToArray();
        var rk = new double[a.Length];
        for (int i = 0; i < idx.Length;)
        {
            int j = i;
            while (j + 1 < idx.Length && a[idx[j + 1]] == a[idx[i]]) j++;
            double avg = (i + j) / 2.0 + 1;
            for (int k = i; k <= j; k++) rk[idx[k]] = avg;
            i = j + 1;
        }
        return rk;
    }

    // ---------------------------------------------------------------------------------
    // Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        Console.WriteLine("# elite p0 —— 第284期 Phase 0");
        Console.WriteLine();

        // §1 桁とキャッシュ
        Console.WriteLine("## 1. 桁の安全と写しのキャッシュ");
        Console.WriteLine();
        Console.WriteLine("| 駒 | 素 HP/攻 | 1000/300 | 1000/200 | 1000/250 | 115/115 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var d in new[] { EnemyCatalog.Recruit, EnemyCatalog.Levy })
        {
            string S(EnemyScaleRule sc) { var x = sc.Apply(d); return $"{x.MaxHp}/{x.Attack}"; }
            Console.WriteLine($"| {d.Name} | {d.MaxHp}/{d.Attack} | {S(Elite)} | {S(Ref[0])} | {S(Ref[1])} | {S(EnemyScaleRule.Default)} |");
        }
        var a1 = Elite.Apply(EnemyCatalog.Recruit); var a2 = Elite.Apply(EnemyCatalog.Recruit);
        var b1 = Ref[0].Apply(EnemyCatalog.Recruit); var c1 = EnemyScaleRule.Default.Apply(EnemyCatalog.Recruit);
        Console.WriteLine();
        Console.WriteLine($"- 同じ倍率は同じ写し: {ReferenceEquals(a1, a2)} ／ 倍率が違えば別の写し: 1000/300≠1000/200 {!ReferenceEquals(a1, b1)}・1000/300≠115/115 {!ReferenceEquals(a1, c1)}");
        Console.WriteLine($"- 写しの由来: `EnemyScaleRule.Of` = {EnemyScaleRule.Of(a1).HpPercent}/{EnemyScaleRule.Of(a1).AtkPercent}（召喚が同じ倍率を引き継ぐ）");
        Console.WriteLine($"- 最大の積: {EnemyCatalog.Recruit.MaxHp} × 1000 = {EnemyCatalog.Recruit.MaxHp * 1000}（int の上限 {int.MaxValue} に対して桁あふれなし）");
        Console.WriteLine();

        // §2 打ち切り・§6 決定性
        var rows = CompareBuilds();
        var ws = Waves.Select(w => w.W).ToArray();
        var g = Grid(rows, ws, Elite, 0, 20);
        Console.WriteLine("## 2. 打ち切りの値（seed 0..19）");
        Console.WriteLine();
        int to = 0, wipe30 = 0, loss = 0, win30 = 0;
        foreach (var o in g) { if (!o.Won) loss++; if (o.Timeout) to++; if (!o.Won && !o.Timeout && o.Turns >= BattleEngine.MaxTurns) wipe30++; if (o.Won && o.Turns >= BattleEngine.MaxTurns) win30++; }
        Console.WriteLine($"- 全 {g.Length} 戦のうち 負け {loss}・うち打ち切り {to}・30T 目の全滅 {wipe30}・30T 目の勝ち {win30}");
        // 打ち切り1戦の中身
        for (int r = 0; r < rows.Length; r++)
            for (int w = 0; w < ws.Length; w++)
                for (int s = 0; s < 20; s++)
                    if (g[r, w, s].Timeout)
                    {
                        var res = BattleEngine.Run(BattleEngine.Materialize(rows[r].F, BattleContext.PlayerTeam), BattleEngine.MaterializeEnemy(ws[w], Elite), s, verbose: false);
                        Console.WriteLine($"- 打ち切りの例: {rows[r].Name} × {Waves[w].Name} × seed {s} → PlayerWon={res.PlayerWon}・Turns={res.Turns}・PlayerSurvivors={res.PlayerSurvivors}・欠けた出撃駒 {res.PlayerStarterFallen.Count}");
                        goto doneTo;
                    }
            doneTo:
        Console.WriteLine();

        Console.WriteLine("## 6. 決定性（seed 0..19 で 0% にも 100% にも張り付かない行）");
        Console.WriteLine();
        for (int w = 0; w < ws.Length; w++)
        {
            var mid = Enumerable.Range(0, rows.Length).Where(r =>
            {
                int k = Enumerable.Range(0, 20).Count(s => g[r, w, s].Won);
                return k > 0 && k < 20;
            }).ToList();
            Console.WriteLine($"- {Waves[w].Name}: {mid.Count} 行（{string.Join("・", mid.Take(8).Select(r => rows[r].Name))}{(mid.Count > 8 ? " ほか" : "")}）");
        }
        // 同じ seed を2回回して一致するか
        bool same = true;
        for (int r = 0; r < rows.Length; r++) { var x = Fight(rows[r].F, Five, Elite, 7); var y = Fight(rows[r].F, Five, Elite, 7); same &= x == y; }
        Console.WriteLine($"- 同じ seed の再実行で全行一致: {same}");
        Console.WriteLine();

        // §3 乱射の長期戦累積
        Console.WriteLine("## 3. 乱射の累積（見境改 (ミサ×薙ぎ) × 近衛 × seed 0・verbose）");
        Console.WriteLine();
        var row = rows.First(x => x.Name.StartsWith("見境改"));
        foreach (var (name, w) in Waves)
        {
            var p = BattleEngine.Materialize(row.F, BattleContext.PlayerTeam);
            var res = BattleEngine.Run(p, BattleEngine.MaterializeEnemy(w, Elite), 0, verbose: true);
            res.TallyByUnit.TryGetValue(UnitCatalog.Tome.Id, out var t);
            var mine = p.Select(u => u.InstanceId).ToHashSet();
            int allyDmgTotal = res.Events.Where(e => e.Kind == BattleEventKind.Damage && e.TargetId is int id && mine.Contains(id)).Sum(e => e.Amount);
            int allyMaxHp = p.Sum(u => u.MaxHp);
            Console.WriteLine($"- {name}: 勝ち={res.PlayerWon}・{res.Turns}T・乱射の手番 {t?.SprayTurns ?? 0}・味方への着弾 {t?.SprayAlly ?? 0} 発 / 与ダメ {t?.SprayAllyDealt ?? 0}（味方を倒した {t?.SprayAllyKills ?? 0}）・敵への着弾 {t?.SprayFoe ?? 0} 発 / 与ダメ {t?.SprayFoeDealt ?? 0}");
            Console.WriteLine($"  味方が受けた総ダメ {allyDmgTotal}（うち乱射 {(allyDmgTotal == 0 ? 0 : 100.0 * (t?.SprayAllyDealt ?? 0) / allyDmgTotal):F1}%）・隊の最大HP 合計 {allyMaxHp}");
        }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // §5-3 毒軸の一次切り分け
    // ---------------------------------------------------------------------------------
    static void Poison(int seed)
    {
        var rows = CompareBuilds();
        var row = rows.First(x => x.Name == "毒 (グザ×ミオ×ラウ)");
        Console.WriteLine($"# elite poison —— 毒 (グザ×ミオ×ラウ) × 大隊 × seed {seed}");
        Console.WriteLine();
        foreach (var (label, w, sc) in new[] { ("大隊 1000/300", Nine, Elite), ("同じ構成 115/115（対照）", Nine, EnemyScaleRule.Default), ("近衛 1000/300", Five, Elite) })
        {
            var p = BattleEngine.Materialize(row.F, BattleContext.PlayerTeam);
            var e = BattleEngine.MaterializeEnemy(w, sc);
            var res = BattleEngine.Run(p, e, seed, verbose: true);
            var mine = p.Select(u => u.InstanceId).ToHashSet();
            var foes = e.Select(u => u.InstanceId).ToHashSet();
            int foeHp = e.Sum(u => u.MaxHp);
            Console.WriteLine($"## {label}");
            Console.WriteLine();
            Console.WriteLine($"勝ち={res.PlayerWon}・{res.Turns}T・敵の総HP {foeHp}・敵の死 {res.Events.Count(x => x.Kind == BattleEventKind.Death && x.TargetId is int i && foes.Contains(i))}");
            Console.WriteLine();
            Console.WriteLine("| T | 敵の毒の層（ターン頭・合計） | 毒の層を持つ敵 | 毒の刻み（敵へ・そのT） | 敵への全ダメ（そのT） | 敵の残HP 合計（T 末） | 味方の生存（T 末） | 味方が受けたダメ（そのT） |");
            Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|");
            var hp = e.ToDictionary(u => u.InstanceId, u => u.MaxHp);
            var alive = p.ToDictionary(u => u.InstanceId, _ => true);
            long poisonTotal = 0, foeDmgTotal = 0; int firstDeathT = 0, peakLayers = 0, lastLayers = 0;
            for (int t = 1; t <= res.Turns; t++)
            {
                var ev = res.Events.Where(x => x.Turn == t).ToList();
                var snap = ev.Where(x => x.Kind == BattleEventKind.StatusSnapshot && x.Text == "毒" && x.TargetId is int i && foes.Contains(i)).ToList();
                int layers = snap.Sum(x => x.Amount);
                int ptick = ev.Where(x => x.Kind == BattleEventKind.Status && x.Text == "毒" && x.TargetId is int i && foes.Contains(i)).Sum(x => x.Amount);
                int fdmg = 0, adm = 0;
                foreach (var x in ev)
                {
                    if (x.Kind == BattleEventKind.Damage && x.TargetId is int i)
                    {
                        if (foes.Contains(i)) { fdmg += x.Amount; hp[i] = x.HpAfter; }
                        else if (mine.Contains(i)) adm += x.Amount;
                    }
                    if (x.Kind == BattleEventKind.Death && x.TargetId is int d && mine.Contains(d)) { alive[d] = false; if (firstDeathT == 0) firstDeathT = t; }
                    if (x.Kind == BattleEventKind.Death && x.TargetId is int d2 && foes.Contains(d2)) hp[d2] = 0;
                }
                poisonTotal += ptick; foeDmgTotal += fdmg; peakLayers = Math.Max(peakLayers, layers); lastLayers = layers;
                Console.WriteLine($"| {t} | {layers} | {snap.Count} | {ptick} | {fdmg} | {hp.Values.Sum(v => Math.Max(0, v))} | {alive.Values.Count(v => v)} | {adm} |");
            }
            Console.WriteLine();
            Console.WriteLine($"- 毒の刻みの総量（敵へ・額面） **{poisonTotal}** ／ 敵への全ダメ {foeDmgTotal}（毒の割合 {(foeDmgTotal == 0 ? 0 : 100.0 * poisonTotal / foeDmgTotal):F1}%）／ 敵の総HP に対して {100.0 * foeDmgTotal / foeHp:F1}%");
            Console.WriteLine($"- 層の最大（ターン頭の合計） **{peakLayers}** ／ 最終ターンの層 **{lastLayers}** ／ 隊の最初の死 **{(firstDeathT == 0 ? "なし" : firstDeathT + "T")}**");
            foreach (var kv in res.DamageByUnit.OrderByDescending(k => k.Value)) Console.Write($"{kv.Key} {kv.Value}  ");
            Console.WriteLine();
            Console.WriteLine();
        }
    }

    // ---------------------------------------------------------------------------------
    // 自己検査
    // ---------------------------------------------------------------------------------
    static void Check()
    {
        int fails = 0;
        void Ok(string what, bool ok) { Console.WriteLine($"- [{(ok ? "OK" : "NG")}] {what}"); if (!ok) fails++; }
        var rows = CompareBuilds();
        Console.WriteLine("# elite check —— 第284期 自己検査");
        Console.WriteLine();

        // (a) 素の構成が TestStages と同じ・倍率を既定／素に戻すと検証の波と全戦一致
        static bool SameWave(EnemyWave a, EnemyWave b) => Enumerable.Range(0, 9).All(i => ReferenceEquals(a[i], b[i]));
        Ok("(a-1) 近衛 の構成 ＝ `TestStages[2]`（検証・五 / 新兵）", SameWave(Five, EnemyCatalog.TestStages[2].Enemy));
        Ok("(a-2) 大隊 の構成 ＝ `TestStages[1]`（検証・九 / 農兵）", SameWave(Nine, EnemyCatalog.TestStages[1].Enemy));
        foreach (var sc in new[] { EnemyScaleRule.None, EnemyScaleRule.Default })
        {
            int diff = 0, n = 0;
            var idx = new[] { (Five, EnemyCatalog.TestStages[2].Enemy), (Nine, EnemyCatalog.TestStages[1].Enemy) };
            var lk = new object();
            Parallel.For(0, rows.Length * idx.Length * 50, j =>
            {
                int s = j % 50, c = j / 50 % idx.Length, r = j / 50 / idx.Length;
                var mine = Fight(rows[r].F, idx[c].Item1, sc, s);
                var nine = NineDiag.Measure(rows[r].F, NineDiag.Wave(idx[c].Item2, sc), s, 1);
                bool same = mine.Won == (nine.Wins == 1) && (!mine.Won || mine.Turns == nine.WinT);
                lock (lk) { n++; if (!same) diff++; }
            });
            Ok($"(a-3) 倍率 {sc.HpPercent}/{sc.AtkPercent} で `nine` の口（`NineDiag.Measure`）と戦ごとに一致（{n} 戦・seed 0..49・ずれ {diff}）", diff == 0);
        }

        // (b) §7 の照合（九・新兵 ／ 九・農兵・seed 0..49）
        var expect = new (string Row, double NineRecruit, double NineLevy)[]
        {
            ("見境改 (ミサ×薙ぎ)", 100, 100), ("燃焼 (ボルグ×ホタ)", 100, 100), ("速攻 (ボルグ×ムド)", 100, 100),
            ("毒→被弾強化 (グザ×ムド)", 100, 100), ("鱗改 (ウロ×ヒビ)", 100, 100), ("範囲耐性 (ヒビ×ボルグ)", 100, 100),
            ("逸らし (ソラ×カド)", 100, 100), ("移動改 (バサ×ヨミ×シオ)", 88, 98), ("継ぎ当て×分散回復", 6, 100),
            ("隊列崩し (バサ×ヨミ×セロ)", 0, 36), ("反撃 (ヒサ×カド)", 0, 58), ("感電 (シガ×カタ×ソム)", 0, 76),
            ("毒 (グザ×ミオ×ラウ)", 0, 0), ("毒+耐久 (ベニ×トウ)", 0, 0), ("毒+ベニ+ラウ", 0, 0), ("毒爆弾 (ラウ×ヴィオ)", 0, 0),
            ("耐久 (ガルド×リリ)", 0, 0),
        };
        // 第287期: §7 は第284期の記録なので、規定のトウ（第287期から T3）を旧の規定 `TouT0` に戻して照合する。
        var sel = expect.Select(e => rows.First(r => r.Name == e.Row)).Select(r => (r.Name, F: FvSwap(FvSwap(FvSwap(r.F, UnitCatalog.Tou, UnitCatalog.TouT0), UnitCatalog.Shiga, UnitCatalog.ShigaG3K), UnitCatalog.Kata, UnitCatalog.KataS3))).ToArray();   // 第290期: カタも旧の規定（S3）へ
        var gb = Cells(Grid(sel, new[] { NineRecruit, Nine }, Elite, 0, 50));
        Console.WriteLine();
        Console.WriteLine("| 行 | 九・新兵 §7 | 実測 | 九・農兵 §7 | 実測 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        double maxDev = 0;
        for (int i = 0; i < expect.Length; i++)
        {
            Console.WriteLine($"| {expect[i].Row} | {expect[i].NineRecruit} | {P(gb[i, 0].Win)} | {expect[i].NineLevy} | {P(gb[i, 1].Win)} |");
            maxDev = Math.Max(maxDev, Math.Max(Math.Abs(gb[i, 0].Win - expect[i].NineRecruit), Math.Abs(gb[i, 1].Win - expect[i].NineLevy)));
        }
        Console.WriteLine();
        Ok($"(b) §7 の照合 {expect.Length} 行 × 2 列（seed 0..49）の最大のずれ {maxDev:F1}pt ≦ 5.0pt", maxDev <= 5.0);

        // (c) 精鋭の倍率は他の駒の写しを汚さない（既定の写しが不変）
        var d0 = EnemyScaleRule.Default.Apply(EnemyCatalog.Recruit);
        _ = Elite.Apply(EnemyCatalog.Recruit);
        Ok("(c) 精鋭の写しを作っても既定（115/115）の写しは同じインスタンス・同じ値", ReferenceEquals(d0, EnemyScaleRule.Default.Apply(EnemyCatalog.Recruit)) && d0.MaxHp == EnemyCatalog.Recruit.MaxHp * 115 / 100);
        Ok("(d) `TestStages` は3本のまま（精鋭を足していない）", EnemyCatalog.TestStages.Count == 3 && EnemyCatalog.Stages.Count == 5);

        Console.WriteLine();
        Console.WriteLine(fails == 0 ? "自己検査: 全項目 OK" : $"自己検査: NG {fails} 項目");
    }
}
