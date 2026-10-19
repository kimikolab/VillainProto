using BattleCore;
using static Common;
using I = Inv348Diag;

// sh362 §5 ／ §7 —— 席の総当たり ／ カドの一撃とボスへの届き方 ／ 代表台 ／ 軸の横並び ／ `compare`。
static partial class Sh362Diag
{
    internal sealed record Seat(string Key, string Trio, Formation F);
    static IEnumerable<int[]> Perms(int n)
    {
        if (n == 1) { yield return new[] { 0 }; yield break; }
        foreach (var p in Perms(n - 1))
            for (int i = 0; i <= p.Length; i++)
            {
                var q = new int[n];
                for (int j = 0, k = 0; j < n; j++) q[j] = j == i ? n - 1 : p[k++];
                yield return q;
            }
    }
    internal static Seat[] AllSeats()
    {
        var list = new List<Seat>();
        var perms = Perms(5).ToArray();
        for (int i = 0; i < Pool.Length; i++)
            for (int j = i + 1; j < Pool.Length; j++)
                for (int k = j + 1; k < Pool.Length; k++)
                {
                    var units = Fixed.Append(Pool[i]).Append(Pool[j]).Append(Pool[k]).ToArray();
                    string trio = $"{Short(Pool[i])}＋{Short(Pool[j])}＋{Short(Pool[k])}";
                    foreach (var perm in perms)
                    {
                        var f = new Formation();
                        for (int s = 0; s < 5; s++) f[s] = units[perm[s]];
                        list.Add(new Seat(Seats(f), trio, f));
                    }
                }
        return list.ToArray();
    }

    /// <summary>総当たりの1マス（盤面は verbose なしで回す・帳簿だけ読む）。</summary>
    internal sealed class Cell
    {
        public long N, Wins, WinT, Fell, Lost, WipeT, KDied, KT, Bare, BareT;
        public readonly Dictionary<string, long> First = new();
        public void Add(Cell o)
        {
            N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Lost += o.Lost; WipeT += o.WipeT; KDied += o.KDied; KT += o.KT; Bare += o.Bare; BareT += o.BareT;
            foreach (var (k, v) in o.First) First[k] = First.GetValueOrDefault(k) + v;
        }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
    }
    internal static Cell Fight(Formation f, I.W w, int seed)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false, boss: new BossRule(false) { Scale = w.Scale });
        var c = new Cell { N = 1, Fell = p.Count(u => u.LastDeathTurn > 0) };
        if (r.PlayerWon) { c.Wins = 1; c.WinT = r.Turns; }
        else { c.Lost = 1; c.WipeT = p.Max(u => u.LastDeathTurn); }
        var first = p.Where(u => u.LastDeathTurn > 0).OrderBy(u => u.LastDeathTurn).ThenBy(u => u.Slot).FirstOrDefault();
        if (first is not null) c.First[Short(first.Def)] = 1;
        var k = p.FirstOrDefault(u => u.Def.Id == "kado");
        if (k is not null && k.LastDeathTurn > 0) { c.KDied = 1; c.KT = k.LastDeathTurn; }
        if (r.TallyByUnit.TryGetValue("kado", out var t) && t.Sh362BareTurn > 0) { c.Bare = 1; c.BareT = t.Sh362BareTurn; }
        return c;
    }
    internal static Cell CellOf(Formation f, I.W w, int n)
    {
        var parts = new Cell[n];
        Parallel.For(0, n, s => parts[s] = Fight(f, w, s));
        var all = new Cell();
        foreach (var x in parts) all.Add(x);
        return all;
    }
    static string FirstOf(Cell c) => c.First.Count == 0 ? "—" : string.Join("、", c.First.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key} {Pct(kv.Value, c.N)}%"));

    internal sealed record Res(Seat S, Cell Boss, Cell Guard, Cell Bat)
    {
        public long Elite => Guard.Wins + Bat.Wins;
        public double KillT => Boss.Wins == 0 ? double.MaxValue : (double)Boss.WinT / Boss.Wins;
        public long Fell => Boss.Fell + Guard.Fell + Bat.Fell;
        public bool Elite90 => Guard.Win >= 90 && Bat.Win >= 90;
    }
    static Res[] Rank(IEnumerable<Res> xs) => xs
        .OrderByDescending(r => r.Boss.Wins).ThenByDescending(r => r.Elite).ThenBy(r => r.KillT).ThenBy(r => r.Fell)
        .ThenBy(r => r.S.Key, StringComparer.Ordinal).ToArray();
    static string WT(Cell c) => $"{F1(c.Win)}・{c.WinTs}T";
    static Res[] Grid(Seat[] seats, Ver v)
    {
        var res = new Res[seats.Length];
        for (int k = 0; k < seats.Length; k++)
        {
            var f = Apply(seats[k].F, v);
            res[k] = new Res(seats[k], CellOf(f, I.Boss, BossSeeds), CellOf(f, I.Guard, EliteSeeds), CellOf(f, I.Bat, EliteSeeds));
        }
        return res;
    }

    static void SeatsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var seats = AllSeats();
        var trios = seats.Select(s => s.Trio).Distinct().ToArray();
        var grids = Vers.Select(v => (v, res: Grid(seats, v))).ToArray();
        bool Lili(Res r) => Has(r.S.F, "lili");
        Console.WriteLine($"# {Tag} §5 席の総当たり —— カド ／ アカ ＋ {string.Join("・", Pool.Select(Short))} から3枚（{trios.Length} 組）× 席の全並び 120 ＝ {seats.Length} 並び × 規定 ／ KD-t ／ KD-r");
        Console.WriteLine();
        Console.WriteLine($"波: ボス（seed 0..{BossSeeds - 1}）／ 近衛 ／ 大隊（seed 0..{EliteSeeds - 1}）。段0 入りの規定。順位は第356〜361期と同じ（ボスの勝ち数 → 精鋭の勝ち数 → 倒しT → 倒れた数）。リリ（ヒーラー）は「含む ／ 含まない」に分けて数える。");
        Console.WriteLine();
        Console.WriteLine("| 版 | ボスに勝てる並び（リリなし ／ あり） | 精鋭 90% 以上（リリなし ／ あり） | 両立（リリなし ／ あり） | ボスで最初に倒れる駒 | カドがボス戦で倒れた % ／ T | 剥いた %（ボス）／ T |");
        Console.WriteLine("|---|--:|--:|--:|---|---|---|");
        foreach (var (v, res) in grids)
        {
            var c = new Cell(); foreach (var r in res) c.Add(r.Boss);
            int B(Func<Res, bool> f) => res.Count(r => r.Boss.Wins > 0 && f(r));
            int E(Func<Res, bool> f) => res.Count(r => r.Elite90 && f(r));
            int X(Func<Res, bool> f) => res.Count(r => r.Boss.Wins > 0 && r.Elite90 && f(r));
            Console.WriteLine($"| {v.Name} | {B(r => !Lili(r))} ／ {B(Lili)} | {E(r => !Lili(r))} ／ {E(Lili)} | {X(r => !Lili(r))} ／ {X(Lili)} | {FirstOf(c)} | {Pct(c.KDied, c.N)} ／ T{Per(c.KT, c.KDied)} | {Pct(c.Bare, c.N)} ／ T{Per(c.BareT, c.Bare)} |");
        }
        Console.WriteLine();
        foreach (var (v, res) in grids)
        {
            var ranked = Rank(res);
            Console.WriteLine($"## {v.Name}");
            Console.WriteLine();
            void Top(string title, IEnumerable<Res> xs, int n)
            {
                Console.WriteLine(title);
                Console.WriteLine();
                Console.WriteLine("| # | 並び（前1・前3・中央・後1・後3） | 組 | ボス 勝率・倒しT | 全滅 T | 近衛 | 大隊 | 倒れた ／戦 | ボスで最初に倒れる | 剥いた T（ボス） |");
                Console.WriteLine("|--:|---|---|---|--:|---|---|--:|---|--:|");
                int i = 0;
                foreach (var r in xs.Take(n))
                    Console.WriteLine($"| {++i} | {r.S.Key} | {r.S.Trio} | {WT(r.Boss)} | {Per(r.Boss.WipeT, r.Boss.Lost)} | {WT(r.Guard)} | {WT(r.Bat)} | {Per2(r.Fell, r.Boss.N + r.Guard.N + r.Bat.N)} | {FirstOf(r.Boss)} | {Per(r.Boss.BareT, r.Boss.Bare)} |");
                if (i == 0) Console.WriteLine("| — | （該当なし） | | | | | | | | |");
                Console.WriteLine();
            }
            Top("### 上位10並び", ranked, 10);
            Top("### ボスに勝ち、精鋭もどちらも 90% 以上の並び（両立）の最良5", ranked.Where(r => r.Boss.Wins > 0 && r.Elite90), 5);
            Top("### 精鋭（近衛 ＋ 大隊の勝ち数）の最良5", ranked.OrderByDescending(r => r.Elite).ThenBy(r => r.Fell).ThenBy(r => r.S.Key, StringComparer.Ordinal), 5);
            Top("### リリを含む最良3", ranked.Where(Lili), 3);
            Top("### リリを含まない最良3", ranked.Where(r => !Lili(r)), 3);
            Console.WriteLine("組ごと（ボスに勝てる並びがある組）: " + Join(res.GroupBy(r => r.S.Trio).Select(g => (g.Key, N: g.Count(r => r.Boss.Wins > 0)))) + "。");
            Console.WriteLine();
            Console.WriteLine("組ごと（精鋭 90% 以上の並びの数・上位10組）: " + Join(res.GroupBy(r => r.S.Trio).Select(g => (g.Key, N: g.Count(r => r.Elite90))), 10) + "。");
            Console.WriteLine();
            string SeatsOf(Func<Res, bool> f) => string.Join(" ／ ", Enumerable.Range(0, 5).Select(k => res.Count(r => f(r) && r.S.F[k]?.Id == "kado")));
            Console.WriteLine($"カドの席（前1 ／ 前3 ／ 中央 ／ 後1 ／ 後3）: ボスに勝つ並び {SeatsOf(r => r.Boss.Wins > 0)}・精鋭 90% 以上 {SeatsOf(r => r.Elite90)}。");
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
    static string Join(IEnumerable<(string Key, int N)> xs, int take = 100)
    {
        var l = xs.Where(x => x.N > 0).OrderByDescending(x => x.N).ThenBy(x => x.Key, StringComparer.Ordinal).Take(take).Select(x => $"{x.Key} {x.N}").ToArray();
        return l.Length == 0 ? "なし" : string.Join("、", l);
    }

    static void FlowAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        Console.WriteLine($"# {Tag} §7 の 1・2 カドの一撃 ／ ボスへの届き方 —— 代表台 {boards.Length} × ボス ／ 大隊 × 版・seed 0..{Seeds - 1}");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();
        foreach (var b in boards)
        {
            Console.WriteLine($"## {b.Name}");
            Console.WriteLine();
            foreach (var v in vers)
            {
                var f = Apply(b.F, v);
                var bat = Measure(f, I.Bat);
                var boss = Measure(f, I.Boss);
                Console.WriteLine($"- **{v.Name} × 大隊**（勝率 {F1(bat.Win)}%・決着T {bat.WinTs}）: {KadoLine(bat)}");
                Console.WriteLine($"- **{v.Name} × ボス**（勝率 {F1(boss.Win)}%・倒しT {boss.WinTs}）: {KadoLine(boss)}");
                Console.WriteLine();
                PrintTraj($"{v.Name} × ボス", boss);
            }
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        Console.WriteLine($"# {Tag} §7 の 3・6 代表台 {boards.Length} × {Waves.Length} 波 × 版・seed 0..{Seeds - 1}");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();
        Console.WriteLine("勝率・決着T（ボスは倒しT）・倒れた ／戦・打ち切り %。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        var bad = new List<string>();
        foreach (var b in boards)
        {
            Kx[]? baseRow = null;
            foreach (var v in vers)
            {
                var f = Apply(b.F, v);
                var row = Waves.Select(w => Measure(f, w)).ToArray();
                if (v == Vers[0]) baseRow = row;
                Console.WriteLine($"| {b.Name} | {v.Name} | " + string.Join(" | ", row.Select(a => $"{F1(a.Win)}・{a.WinTs}T・倒 {Per2(a.Fell, a.N)}{(a.Draw > 0 ? $"・分 {Pct(a.Draw, a.N)}" : "")}")) + " |");
                for (int i = 0; i < Waves.Length; i++)
                {
                    var a = row[i]; var w = Waves[i];
                    if ((w == I.Guard || w == I.Bat) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 1T 決着 {Pct(a.Win1, a.N)}%");
                    if (baseRow is not null && v != Vers[0] && baseRow[i].Win - a.Win >= 15) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 勝率 {F1(baseRow[i].Win)} → {F1(a.Win)}");
                    if (w == I.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4) bad.Add($"{b.Name} × {v.Name} × ボス: 倒しT {a.WinTs}");
                    if (baseRow is not null && v != Vers[0] && 100.0 * (a.Draw - baseRow[i].Draw) / a.N >= 5) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 打ち切り {Pct(baseRow[i].Draw, a.N)} → {Pct(a.Draw, a.N)}%");
                }
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ 勝率が規定から 15pt 以上落ちる ／ ボス 4T 未満 ／ 30T の打ち切りが規定から 5pt 以上増える）");
        Console.WriteLine();
        Console.WriteLine(bad.Count == 0 ? "なし。" : string.Join(Environment.NewLine, bad.Select(x => "- " + x)));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void AxesAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        // 決めたこと: ほかの軸は `inv348` の台のうち「反撃」（カドの行・版で動く）を除いた軸。「自傷」（逆しま）はカドを含まないのでほかの軸に残す。
        var others = I.Boards().Where(b => b.Axis != "反撃").ToList();
        I.W[] aw = I.AxisWaves;
        double[] Vals(Dictionary<string, I.St> r) => new[]
        {
            r[I.Boss.Name].Win, r[I.Boss.Name].Wins == 0 ? double.NegativeInfinity : -(double)r[I.Boss.Name].WinT / r[I.Boss.Name].Wins,
            r[I.Guard.Name].Win, r[I.Bat.Name].Win, r[I.M2.Name].Win, r[I.M4.Name].Win, r[I.M5.Name].Win, r[I.Nine.Name].Win,
            (double)r[I.T1.Name].Dmg8 / r[I.T1.Name].N, (double)r[I.T9.Name].Dmg8 / r[I.T9.Name].N, I.StrongWaves.Average(w => r[w.Name].Win),
        };
        string Line(string label, Dictionary<string, I.St> r)
        {
            string W(I.W w) => $"{F1(r[w.Name].Win)}・{r[w.Name].WinTs}T";
            return $"| {label} | {W(I.Boss)} | {W(I.Guard)} | {W(I.Bat)} | {F1(r[I.M2.Name].Win)} | {F1(r[I.M4.Name].Win)} | {F1(r[I.M5.Name].Win)} | {W(I.Nine)} | "
                + $"{Per(r[I.T1.Name].Dmg8, r[I.T1.Name].N)} | {Per(r[I.T9.Name].Dmg8, r[I.T9.Name].N)} | {F1(I.StrongWaves.Average(w => r[w.Name].Win))} |";
        }
        Dictionary<string, I.St> M(Formation f) => aw.ToDictionary(w => w.Name, w => I.Measure(f, w));
        var oth = others.ToDictionary(b => b.Name, b => M(b.F));
        var axes = others.GroupBy(b => b.Axis).ToDictionary(g => g.Key, g => Enumerable.Range(0, 11).Select(i => g.Max(b => Vals(oth[b.Name])[i])).ToArray());
        Console.WriteLine($"# {Tag} §7-4 軸の横並び（第348期 §3 の物差し）—— 自傷（カド）の行 × 版・seed 0..{I.Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("ほかの軸の行は `inv348` の台（古い口の終端で過去の器具の駒は旧）から「反撃」を除いたもの。自傷（カド）の行は代表台 × 版（段0 入りの規定の上）。");
        Console.WriteLine();
        Console.WriteLine("| 軸 ／ 台 | ボス 勝率・T | 近衛 | 大隊 | 本編2 | 本編4 | 本編5 | 九体 | 的・一 8T | 的・九 8T | 強 2〜5 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|---|--:|--:|--:|");
        foreach (var b in others) Console.WriteLine(Line($"{b.Axis}: {b.Name}", oth[b.Name]));
        var mine = new List<(string Label, double[] V)>();
        foreach (var b in Boards())
            foreach (var v in Vers)
            {
                var r = M(Apply(b.F, v));
                string label = $"{b.Name} × {v.Name}";
                Console.WriteLine(Line($"**自傷（カド）: {label}**", r));
                mine.Add((label, Vals(r)));
            }
        Console.WriteLine();
        string[] cols = { "ボス 勝率", "ボス 倒しT", "近衛", "大隊", "本編2", "本編4", "本編5", "九体", "的・一", "的・九", "強 2〜5" };
        Console.WriteLine($"## 物差しごとの順位（ほかの軸 {axes.Count} 本の1枚 ＋ その行。同じ値は上の順位）");
        Console.WriteLine();
        Console.WriteLine("| 自傷（カド）の行 | " + string.Join(" | ", cols) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "--:|")));
        foreach (var (label, v) in mine)
            Console.WriteLine($"| {label} | " + string.Join(" | ", Enumerable.Range(0, cols.Length).Select(i => $"{1 + axes.Values.Count(a => a[i] > v[i])}")) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static double[,] CGrid((string Name, Formation F)[] rows, Func<Formation, Formation> map)
    {
        int nw = EnemyCatalog.Stages.Count;
        var g = new double[rows.Length, nw];
        Parallel.For(0, rows.Length * nw, k =>
        {
            int ri = k / nw, wi = k % nw;
            var f = map(rows[ri].F);
            int wins = 0;
            for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
            g[ri, wi] = 100.0 * wins / CompareSeeds;
        });
        return g;
    }

    static void CompareAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        var b = CGrid(rows, f => Apply(f, vers[0]));
        var b361 = CGrid(rows, Pin361);
        Console.WriteLine($"# {Tag} §7-5 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定 ＝ 段0 入り）");
        Console.WriteLine();
        var moved = new List<string>();
        for (int r = 0; r < rows.Length; r++) for (int w = 0; w < nw; w++) if (b[r, w] != b361[r, w]) moved.Add($"{rows[r].Name} 第{w + 1}波 {F1(b361[r, w])} → {F1(b[r, w])}");
        Console.WriteLine($"段0（ササの SS-n を規定に）: 第361期までの規定とのセルの差 **{moved.Count}**{(moved.Count == 0 ? "" : "（" + string.Join("・", moved) + "）")}。");
        Console.WriteLine();
        Console.WriteLine($"カドのいる行（{rows.Count(r => Has(r.F, "kado"))}）: {string.Join("、", rows.Where(r => Has(r.F, "kado")).Select(r => r.Name))}。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|--:|---|");
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] g) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => g[r, w]))));
        Console.WriteLine($"| 第361期までの規定 | — | — | {F1(pr.Average(ri => b361[ri, last]))} | {Means(b361)} |");
        Console.WriteLine($"| 規定（段0 入り） | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} |");
        foreach (var v in vers.Skip(1))
        {
            var g = CGrid(rows, f => Apply(f, v));
            int upR = 0, upC = 0, dnR = 0, dnC = 0;
            var lines = new List<string>();
            for (int r = 0; r < rows.Length; r++)
            {
                bool u = false, d = false;
                for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
                if (u) upR++; if (d) dnR++;
                if (u || d) lines.Add($"{rows[r].Name}: " + string.Join("・", Enumerable.Range(1, nw - 1).Where(w => g[r, w] != b[r, w]).Select(w => $"第{w + 1}波 {F1(b[r, w])} → {F1(g[r, w])}")));
            }
            Console.WriteLine($"| {v.Name} | {upR} ／ {upC} | {dnR} ／ {dnC} | {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} |");
            foreach (var l in lines) Console.WriteLine($"|  | {l} | | | |");
            // (G2): いずれかの波で −10.0pt 以上落ちた行の駒それぞれについて、その駒を含む「他の」行の第2〜5波の平均の変化。
            for (int r = 0; r < rows.Length; r++)
            {
                if (!Enumerable.Range(1, nw - 1).Any(w => b[r, w] - g[r, w] >= 10.0)) continue;
                var parts = new List<string>();
                foreach (var id in rows[r].F.Occupied().Select(o => o.Def.Id).Distinct())
                {
                    var oth = Enumerable.Range(0, rows.Length).Where(o => o != r && rows[o].F.Occupied().Any(x => x.Def.Id == id)).ToList();
                    string nm = Short(rows[r].F.Occupied().First(o => o.Def.Id == id).Def);
                    if (oth.Count == 0) { parts.Add($"{nm} 他の行 0（分解が成立しない）"); continue; }
                    double d = oth.Average(o => Enumerable.Range(1, nw - 1).Average(w => g[o, w] - b[o, w]));
                    parts.Add($"{nm} {(d >= 0 ? "+" : "")}{d:F1}（{oth.Count} 行）");
                }
                Console.WriteLine($"|  | (G2) {rows[r].Name}: {string.Join("・", parts)} | | | |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
