using BattleCore;
using static Common;

// inv348 §3 —— 軸の横並び（代表台 × 物差し・seed 0..99）。
static partial class Inv348Diag
{
    static Dictionary<(string, string), St> MeasureAll(Board[] boards, W[] waves, Func<Formation, Formation>? map = null, EmberRule? ember = null)
    {
        var res = new Dictionary<(string, string), St>();
        foreach (var b in boards) foreach (var w in waves) res[(b.Name, w.Name)] = Measure(map is null ? b.F : map(b.F), w, ember: ember);
        return res;
    }

    static double StrongMean(Dictionary<(string, string), St> res, string b) => StrongWaves.Average(w => res[(b, w.Name)].Win);

    static void Axes()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var res = MeasureAll(boards, AxisWaves);
        Console.WriteLine($"# {Tag} §3 軸の横並び —— 代表台 {boards.Length} × 物差し {AxisWaves.Length} 波・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- **{b.Axis}** {b.Name}: {Seats(b.F)}");
        Console.WriteLine();
        Console.WriteLine("ボス ＝ 勇者（倍率なし・`EnemyCatalog.BossRegularWave`）／ 近衛 ／ 大隊 ＝ 精鋭の波（HP 1000% ／ 攻 300%）／ 本編 ＝ `compare` と同じ倍率（115 ／ 115）／ 九体 ＝ 九体 新兵（`TestStages[0]`）／"
            + "的 ＝ 倒れない的（HP 9999・攻 1）を 1 体 ／ 9 体・1〜8 ターン目の与ダメ（味方全員・BossDmgByTurn）／ 強 ＝ 本編 第2〜5波を HP 500% ／ 攻 300%（115 ／ 115 を置き換える）。");
        Console.WriteLine();

        // ---- 攻め ----
        string[] attackCols = { "ボス 勝率", "ボス 倒しT", "近衛 勝率・T", "大隊 勝率・T", "本編2（粛）", "本編4（軛）", "本編5", "九体 勝率・T", "的・一 8T（1T最大）", "的・九 8T（1T最大）", "強 2〜5波" };
        var attackVal = new Dictionary<string, double[]>();   // 1位の印を付けるための値（大きいほど上・T は小さいほど上なので符号を反転）
        foreach (var b in boards)
        {
            St S(W w) => res[(b.Name, w.Name)];
            double T(St s) => s.Wins == 0 ? double.NegativeInfinity : -(double)s.WinT / s.Wins;
            attackVal[b.Name] = new[]
            {
                S(Boss).Win, T(S(Boss)), S(Guard).Win, S(Bat).Win, S(M2).Win, S(M4).Win, S(M5).Win, S(Nine).Win,
                (double)S(T1).Dmg8 / S(T1).N, (double)S(T9).Dmg8 / S(T9).N, StrongMean(res, b.Name),
            };
        }
        var axisVal = AxisOrder.ToDictionary(a => a, a => Enumerable.Range(0, attackCols.Length)
            .Select(i => boards.Where(b => b.Axis == a).Max(b => attackVal[b.Name][i])).ToArray());
        string Mark(string axis, int col, double v)
        {
            double best = AxisOrder.Max(a => axisVal[a][col]);
            return v == axisVal[axis][col] && v == best && !double.IsNegativeInfinity(v) ? " ★" : "";
        }

        Console.WriteLine("## 1. 攻め（★ ＝ その物差しで1位の軸の、1位の台）");
        Console.WriteLine();
        Console.WriteLine("| 軸 | 台 | " + string.Join(" | ", attackCols) + " |");
        Console.WriteLine("|---|---|" + string.Concat(attackCols.Select(_ => "--:|")));
        foreach (var b in boards)
        {
            St S(W w) => res[(b.Name, w.Name)];
            var v = attackVal[b.Name];
            string WT(St s, int col) => $"{F1(s.Win)}・{s.WinTs}T{Mark(b.Axis, col, v[col])}";
            Console.WriteLine($"| {b.Axis} | {b.Name} | {F1(S(Boss).Win)}{Mark(b.Axis, 0, v[0])} | {S(Boss).WinTs}{Mark(b.Axis, 1, v[1])} | {WT(S(Guard), 2)} | {WT(S(Bat), 3)} | "
                + $"{F1(S(M2).Win)}{Mark(b.Axis, 4, v[4])} | {F1(S(M4).Win)}{Mark(b.Axis, 5, v[5])} | {F1(S(M5).Win)}{Mark(b.Axis, 6, v[6])} | {WT(S(Nine), 7)} | "
                + $"{Per(S(T1).Dmg8, S(T1).N)}（{Per(S(T1).Max1T, S(T1).N)}）{Mark(b.Axis, 8, v[8])} | {Per(S(T9).Dmg8, S(T9).N)}（{Per(S(T9).Max1T, S(T9).N)}）{Mark(b.Axis, 9, v[9])} | "
                + $"{F1(StrongMean(res, b.Name))}{Mark(b.Axis, 10, v[10])} |");
        }
        Console.WriteLine();
        Console.WriteLine("強 2〜5波の内訳（第2 ／ 第3 ／ 第4 ／ 第5波）:");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {string.Join(" ／ ", StrongWaves.Select(w => F1(res[(b.Name, w.Name)].Win)))}");
        Console.WriteLine();

        // ---- 守り ----
        W[] defW = { Boss, Bat, M5 };
        Console.WriteLine("## 2. 守り —— 1戦あたりの味方（編成の5体）の被ダメ ／ 倒れた味方の数 ／ 最初に味方が倒れた T（倒れた戦だけの平均・倒れた戦の割合）");
        Console.WriteLine();
        Console.WriteLine("★ ＝ 倒れた数が最も少ない軸（各波）。被ダメは「受けた量」で、壁が多く受けて倒れないほど大きく出る（少ないほど良いとは限らない）。");
        Console.WriteLine();
        Console.WriteLine("| 軸 | 台 | " + string.Join(" | ", defW.Select(w => $"{w.Name} 被ダメ ／ 倒れた ／ 最初T（割合）")) + " |");
        Console.WriteLine("|---|---|" + string.Concat(defW.Select(_ => "---|")));
        var fellAxis = defW.ToDictionary(w => w.Name, w => AxisOrder.ToDictionary(a => a, a => boards.Where(b => b.Axis == a).Min(b => (double)res[(b.Name, w.Name)].AllyFell / res[(b.Name, w.Name)].N)));
        foreach (var b in boards)
            Console.WriteLine($"| {b.Axis} | {b.Name} | " + string.Join(" | ", defW.Select(w =>
            {
                var s = res[(b.Name, w.Name)];
                double fell = (double)s.AllyFell / s.N;
                bool star = fell == fellAxis[w.Name][b.Axis] && fell == fellAxis[w.Name].Values.Min();
                return $"{Per(s.AllyTaken, s.N)} ／ {Per2(s.AllyFell, s.N)}{(star ? " ★" : "")} ／ {Per(s.FirstFellSum, s.FirstFellN)}（{Pct(s.FirstFellN, s.N)}%）";
            })) + " |");
        Console.WriteLine();

        // ---- 軸ごとの1枚（台の最大）----
        Console.WriteLine("## 3. 軸ごとの1枚（その軸の代表台のうち良いほう・★ ＝ 1位）");
        Console.WriteLine();
        string[] cols = { "ボス 勝率", "ボス 倒しT", "近衛", "大隊", "本編2", "本編4", "本編5", "九体", "的・一 8T", "的・九 8T", "強 2〜5", "倒れた（ボス）", "倒れた（大隊）", "倒れた（第5波）" };
        Console.WriteLine("| 軸 | " + string.Join(" | ", cols) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "--:|")));
        foreach (var a in AxisOrder)
        {
            var v = axisVal[a];
            var cells = new List<string>();
            for (int i = 0; i < attackCols.Length; i++)
            {
                double best = AxisOrder.Max(x => axisVal[x][i]);
                string s = i == 1 ? (double.IsNegativeInfinity(v[i]) ? "—" : F1(-v[i])) : i is 8 or 9 ? v[i].ToString("F0") : F1(v[i]);
                cells.Add(s + (v[i] == best && !double.IsNegativeInfinity(v[i]) ? " ★" : ""));
            }
            foreach (var w in defW)
            {
                double f = fellAxis[w.Name][a];
                cells.Add(f.ToString("F2") + (f == fellAxis[w.Name].Values.Min() ? " ★" : ""));
            }
            Console.WriteLine($"| {a} | " + string.Join(" | ", cells) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
