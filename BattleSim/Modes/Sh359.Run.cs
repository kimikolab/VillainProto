using BattleCore;
using static Common;
using I = Inv348Diag;
using S8 = Sh358Diag;

// sh359 §5 ／ §7 —— 席の総当たり ／ ヒビの生存と配り ／ 代表台 ／ 軸の横並び ／ `compare`。
static partial class Sh359Diag
{
    internal sealed record Seat(string Key, string Trio, Formation F);
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
    static IEnumerable<int[]> Perms(int n)
    {
        var a = Enumerable.Range(0, n).ToArray();
        IEnumerable<int[]> Go(int k)
        {
            if (k == n) { yield return (int[])a.Clone(); yield break; }
            for (int i = k; i < n; i++)
            {
                (a[k], a[i]) = (a[i], a[k]);
                foreach (var p in Go(k + 1)) yield return p;
                (a[k], a[i]) = (a[i], a[k]);
            }
        }
        return Go(0).OrderBy(p => string.Join(",", p), StringComparer.Ordinal).ToList();
    }

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
    internal static Res[] Grid(Seat[] seats, Ver v)
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
        var grids = new[] { Vers[0], V("hbs"), V("hbf") }.Select(v => (v, res: Grid(seats, v))).ToArray();
        var bas = grids[0].res;
        Console.WriteLine($"# {Tag} §5 席の総当たり —— ヒビ ／ ガレ ＋ {string.Join("・", Pool.Select(Short))} から3枚（{trios.Length} 組）× 席の全並び 120 ＝ {seats.Length} 並び × 規定 ／ HB-s ／ HB-f");
        Console.WriteLine();
        Console.WriteLine($"波: ボス（seed 0..{BossSeeds - 1}）／ 近衛 ／ 大隊（seed 0..{EliteSeeds - 1}）。段0 入りの規定（ガレ ＝ GR-r）。順位は「ボスの勝ち → 近衛 ＋ 大隊の勝ち → ボスの倒しT → 3波の倒れた数」（第356 ／ 358期と同じ）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | ボスに勝てる並び | 精鋭 90% 以上 | 両立 | ボスで最初に倒れる駒 | ヒビがボス戦で倒れた割合 ／ T |");
        Console.WriteLine("|---|--:|--:|--:|---|---|");
        foreach (var (v, res) in grids)
        {
            var c = new Cell(); foreach (var r in res) c.Add(r.Boss);
            Console.WriteLine($"| {v.Name} | {res.Count(r => r.Boss.Wins > 0)} | {res.Count(r => r.Elite90)} | {res.Count(r => r.Boss.Wins > 0 && r.Elite90)} | {FirstOf(c)} | {Pct(c.HibiDied, c.N)}% ／ T{Per(c.HibiT, c.HibiDied)} |");
        }
        Console.WriteLine();
        foreach (var (v, res) in grids.Skip(1))
        {
            var ranked = Rank(res);
            Console.WriteLine($"## {v.Name}");
            Console.WriteLine();
            Console.WriteLine("### 1. 組ごと（ボスに勝てる並びがある組 ／ 精鋭 90% 以上がある組）");
            Console.WriteLine();
            Console.WriteLine("| 組 | ボスに勝てる並び（／120）版 ／ 規定 | 精鋭 90% 以上 版 ／ 規定 | 組の最良（版） |");
            Console.WriteLine("|---|--:|--:|---|");
            foreach (var t in trios.OrderByDescending(t => res.Count(r => r.S.Trio == t && r.Boss.Wins > 0)).ThenByDescending(t => res.Count(r => r.S.Trio == t && r.Elite90)).ThenBy(t => t, StringComparer.Ordinal))
            {
                var xs = res.Where(r => r.S.Trio == t).ToArray(); var bs = bas.Where(r => r.S.Trio == t).ToArray();
                if (xs.All(r => r.Boss.Wins == 0) && xs.All(r => !r.Elite90)) continue;
                var best = Rank(xs)[0];
                Console.WriteLine($"| {t} | {xs.Count(r => r.Boss.Wins > 0)} ／ {bs.Count(r => r.Boss.Wins > 0)} | {xs.Count(r => r.Elite90)} ／ {bs.Count(r => r.Elite90)} | {best.S.Key}（ボス {WT(best.Boss)} ／ 近衛 {F1(best.Guard.Win)} ／ 大隊 {F1(best.Bat.Win)}） |");
            }
            Console.WriteLine();
            Console.WriteLine($"どちらも 0 の組は {trios.Count(t => res.Where(r => r.S.Trio == t).All(r => r.Boss.Wins == 0 && !r.Elite90))} 組（表から省いた）。");
            Console.WriteLine();
            void Top(string title, IEnumerable<Res> xs, int n)
            {
                Console.WriteLine(title);
                Console.WriteLine();
                Console.WriteLine("| # | 並び（前1・前3・中央・後1・後3） | 組 | ボス 勝率・倒しT | 全滅 T | 近衛 | 大隊 | 倒れた ／戦 | ヒビが倒れた T（ボス） |");
                Console.WriteLine("|--:|---|---|---|--:|---|---|--:|--:|");
                int i = 0;
                foreach (var r in xs.Take(n))
                    Console.WriteLine($"| {++i} | {r.S.Key} | {r.S.Trio} | {WT(r.Boss)} | {Per(r.Boss.WipeT, r.Boss.Lost)} | {WT(r.Guard)} | {WT(r.Bat)} | {Per2(r.Fell, r.Boss.N + r.Guard.N + r.Bat.N)} | {(r.Boss.HibiDied == 0 ? "立っていた" : Per(r.Boss.HibiT, r.Boss.HibiDied))} |");
                Console.WriteLine();
            }
            Top("### 2. 上位10並び", ranked, 10);
            Top("### 3. ツギを含まない並びの最良5", ranked.Where(r => !Has(r.S.F, "tsugi")), 5);
            Top("### 4. ネルを含む並びの最良5", ranked.Where(r => Has(r.S.F, "nel")), 5);
            Top("### 5. ツギなし ／ ネルなしの最良5", ranked.Where(r => !Has(r.S.F, "tsugi") && !Has(r.S.F, "nel")), 5);
            Console.WriteLine("### 6. 駒ごと（その駒を含む 15 組 × 120 ＝ 1,800 並び）");
            Console.WriteLine();
            Console.WriteLine("| 駒 | ボスに勝てる並び | ボスの勝率の平均 | 近衛 ／ 大隊の勝率の平均 | 精鋭 90% 以上 |");
            Console.WriteLine("|---|--:|--:|---|--:|");
            foreach (var d in Pool)
            {
                var xs = res.Where(r => Has(r.S.F, d.Id)).ToArray();
                Console.WriteLine($"| {Short(d)} | {xs.Count(r => r.Boss.Wins > 0)} | {F1(xs.Average(r => r.Boss.Win))} | {F1(xs.Average(r => r.Guard.Win))} ／ {F1(xs.Average(r => r.Bat.Win))} | {xs.Count(r => r.Elite90)} |");
            }
            Console.WriteLine();
            Console.WriteLine("席ごとのヒビ（ボスの勝率 ／ 近衛 ＋ 大隊の平均）: " + string.Join("・", Enumerable.Range(0, 5).Select(i =>
            {
                var xs = res.Where(r => r.S.F[i]?.Id == "hibi").ToArray();
                return $"{new[] { "前1", "前3", "中央", "後1", "後3" }[i]} {F1(xs.Average(r => r.Boss.Win))} ／ {F1(xs.Average(r => (r.Guard.Win + r.Bat.Win) / 2))}";
            })) + "。");
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    /// <summary>§5 の追い（決めたこと）: 規定では精鋭がどちらも 90% 以上なのに HB-f で落ちる並び——ヒビの席 ／ 組 ／ 近衛 ／ 大隊の勝率の内訳。</summary>
    static void SeatDiff()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var seats = AllSeats();
        var vs = new[] { Vers[0], V("hbs"), V("hbf") };
        var g = vs.Select(v => seats.Select(s => (G: CellOf(Apply(s.F, v), I.Guard, EliteSeeds), B: CellOf(Apply(s.F, v), I.Bat, EliteSeeds))).ToArray()).ToArray();
        bool E((Cell G, Cell B) x) => x.G.Win >= 90 && x.B.Win >= 90;
        string[] slot = { "前1", "前3", "中央", "後1", "後3" };
        Console.WriteLine($"# {Tag} §5 の追い —— 規定で精鋭 90% 以上 → 版で落ちる並び（近衛 ／ 大隊・seed 0..{EliteSeeds - 1}）");
        Console.WriteLine();
        for (int vi = 1; vi < vs.Length; vi++)
        {
            var lost = Enumerable.Range(0, seats.Length).Where(i => E(g[0][i]) && !E(g[vi][i])).ToArray();
            var gain = Enumerable.Range(0, seats.Length).Where(i => !E(g[0][i]) && E(g[vi][i])).ToArray();
            Console.WriteLine($"## {vs[vi].Name}: 落ちた {lost.Length} ／ 上がった {gain.Length}");
            Console.WriteLine();
            Console.WriteLine("落ちた並びのヒビの席: " + string.Join("・", Enumerable.Range(0, 5).Select(k => $"{slot[k]} {lost.Count(i => seats[i].F[k]?.Id == "hibi")}")) + "（全並びでは各席 840）。");
            Console.WriteLine("落ちた並びで落ちた波: 近衛だけ " + lost.Count(i => g[vs.Length - vs.Length + vi][i].G.Win < 90 && g[vi][i].B.Win >= 90) + " ／ 大隊だけ " + lost.Count(i => g[vi][i].G.Win >= 90 && g[vi][i].B.Win < 90) + " ／ 両方 " + lost.Count(i => g[vi][i].G.Win < 90 && g[vi][i].B.Win < 90)
                + "・近衛の平均 " + F1(lost.Select(i => g[0][i].G.Win).DefaultIfEmpty(0).Average()) + " → " + F1(lost.Select(i => g[vi][i].G.Win).DefaultIfEmpty(0).Average())
                + "・大隊の平均 " + F1(lost.Select(i => g[0][i].B.Win).DefaultIfEmpty(0).Average()) + " → " + F1(lost.Select(i => g[vi][i].B.Win).DefaultIfEmpty(0).Average()) + "。");
            Console.WriteLine("落ちた並びの組（上位）: " + string.Join("、", lost.GroupBy(i => seats[i].Trio).OrderByDescending(x => x.Count()).Take(6).Select(x => $"{x.Key} {x.Count()}")) + "。");
            Console.WriteLine("上がった並びの組（上位）: " + string.Join("、", gain.GroupBy(i => seats[i].Trio).OrderByDescending(x => x.Count()).Take(6).Select(x => $"{x.Key} {x.Count()}")) + "。");
            Console.WriteLine("落ちた並びの例: " + string.Join("、", lost.Take(5).Select(i => $"{seats[i].Key}（近衛 {F1(g[0][i].G.Win)} → {F1(g[vi][i].G.Win)} ／ 大隊 {F1(g[0][i].B.Win)} → {F1(g[vi][i].B.Win)}）")) + "。");
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §7 の 1・2 ／ 3・6
    // ---------------------------------------------------------------------------------
    static void FlowAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        Console.WriteLine($"# {Tag} §7 の 1・2 ヒビの生存と配り ／ 返しと礫 ／ ボスへの届き方 —— 代表台 {boards.Length} × ボス ／ 大隊 × 版・seed 0..{Seeds - 1}");
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
                foreach (var w in new[] { I.Bat, I.Boss })
                {
                    var a = S8.Measure(f, w);
                    var c = CellOf(f, w, Seeds);
                    S8.PrintFlow($"{v.Name} × {w.Name}", a);
                    Console.WriteLine($"- 最初に倒れる駒: {FirstOf(c)}");
                    Console.WriteLine();
                    if (w == I.Boss) S8.PrintTraj($"{v.Name} × ボス", a);
                }
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
        Console.WriteLine("勝率・決着T（ボスは倒しT）・倒れた ／戦。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        var bad = new List<string>();
        foreach (var b in boards)
        {
            S8.Fx[]? baseRow = null;
            foreach (var v in vers)
            {
                var f = Apply(b.F, v);
                var row = Waves.Select(w => S8.Measure(f, w)).ToArray();
                if (v == Vers[0]) baseRow = row;
                Console.WriteLine($"| {b.Name} | {v.Name} | " + string.Join(" | ", row.Select(a => $"{F1(a.Win)}・{a.WinTs}T・倒 {Per2(a.Fell, a.N)}")) + " |");
                for (int i = 0; i < Waves.Length; i++)
                {
                    var a = row[i]; var w = Waves[i];
                    if ((w == I.Guard || w == I.Bat) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 1T 決着 {Pct(a.Win1, a.N)}%");
                    if (baseRow is not null && v != Vers[0] && baseRow[i].Win - a.Win >= 15) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 勝率 {F1(baseRow[i].Win)} → {F1(a.Win)}");
                    if (w == I.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4) bad.Add($"{b.Name} × {v.Name} × ボス: 倒しT {a.WinTs}");
                    if (a.RetAmt > 0 && a.RetDealt > 5 * a.RetAmt) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 返しの名目 {Per(a.RetAmt, a.N)} → 削った {Per(a.RetDealt, a.N)}（{(double)a.RetDealt / a.RetAmt:0.0} 倍）");
                }
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ 勝率が規定から 15pt 以上落ちる ／ ボス 4T 未満 ／ 返しで削った量が返しの名目の 5 倍を超える）");
        Console.WriteLine();
        Console.WriteLine(bad.Count == 0 ? "なし。" : string.Join(Environment.NewLine, bad.Select(x => "- " + x)));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 軸の横並び（第348期 §3）—— 破片の行 × 版
    // ---------------------------------------------------------------------------------
    static void AxesAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var others = I.Boards().Where(b => b.Axis != "破片").ToList();
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
        Console.WriteLine($"# {Tag} §7-4 軸の横並び（第348期 §3 の物差し）—— 破片の行 × 版・seed 0..{I.Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("ほかの軸の行は `inv348` の台（燃焼の3枚は第349期までの規定・古い口の終端でウツ ／ ムド ／ ワタ ／ ネル ／ ガレも旧）。破片の行は代表台 × 版（段0 入りの規定の上）。");
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
                Console.WriteLine(Line($"**破片: {label}**", r));
                mine.Add((label, Vals(r)));
            }
        Console.WriteLine();
        string[] cols = { "ボス 勝率", "ボス 倒しT", "近衛", "大隊", "本編2", "本編4", "本編5", "九体", "的・一", "的・九", "強 2〜5" };
        Console.WriteLine($"## 物差しごとの順位（ほかの軸 {axes.Count} 本の1枚 ＋ その行。同じ値は上の順位）");
        Console.WriteLine();
        Console.WriteLine("| 破片の行 | " + string.Join(" | ", cols) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "--:|")));
        foreach (var (label, v) in mine)
            Console.WriteLine($"| {label} | " + string.Join(" | ", Enumerable.Range(0, cols.Length).Select(i => $"{1 + axes.Values.Count(a => a[i] > v[i])}")) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` 64 行 × 版
    // ---------------------------------------------------------------------------------
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
        bool HasH(Formation f) => Has(f, "hibi");
        var b = CGrid(rows, f => Apply(f, vers[0]));
        var b358 = CGrid(rows, Pin358);   // 段0 の前（第358期までの規定のガレ）
        Console.WriteLine($"# {Tag} §7-5 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定 ＝ 段0 入り）");
        Console.WriteLine();
        var moved = new List<string>();
        for (int r = 0; r < rows.Length; r++) for (int w = 0; w < nw; w++) if (b[r, w] != b358[r, w]) moved.Add($"{rows[r].Name} 第{w + 1}波 {F1(b358[r, w])} → {F1(b[r, w])}");
        Console.WriteLine($"段0（ガレの GR-r を規定に）: 第358期までの規定とのセルの差 **{moved.Count}**{(moved.Count == 0 ? "" : "（" + string.Join("・", moved) + "）")}。");
        Console.WriteLine();
        Console.WriteLine($"ヒビのいる行は {rows.Count(r => HasH(r.F))} 行（{string.Join("・", rows.Where(r => HasH(r.F)).Select(r => r.Name))}）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | うちヒビのいない行 | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|---|--:|---|");
        var grids = vers.Skip(1).Select(v => (v.Name, g: CGrid(rows, f => Apply(f, v)))).ToArray();
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] g) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => g[r, w]))));
        Console.WriteLine($"| 第358期までの規定 | — | — | — | {F1(pr.Average(ri => b358[ri, last]))} | {Means(b358)} |");
        Console.WriteLine($"| 規定（段0 入り） | — | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} |");
        foreach (var (name, g) in grids)
        {
            int upR = 0, upC = 0, dnR = 0, dnC = 0, other = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                bool u = false, d = false;
                for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
                if (u) upR++; if (d) dnR++;
                if ((u || d) && !HasH(rows[r].F)) other++;
            }
            Console.WriteLine($"| {name} | {upR} ／ {upC} | {dnR} ／ {dnC} | {other} | {F1(pr.Average(ri => g[ri, last]))} | {Means(g)} |");
        }
        foreach (var (name, g) in grids)
        {
            Console.WriteLine();
            Console.WriteLine($"## {name}");
            Console.WriteLine();
            Console.WriteLine("| 行 | 第2波 | 第3波 | 第4波 | 第5波 |");
            Console.WriteLine("|---|---|---|---|---|");
            for (int r = 0; r < rows.Length; r++)
            {
                if (Enumerable.Range(1, nw - 1).All(w => g[r, w] == b[r, w])) continue;
                Console.WriteLine($"| {rows[r].Name}{(Baseline.PrimaryRows.Contains(rows[r].Name) ? "（主判定）" : "")} | " + string.Join(" | ", Enumerable.Range(1, nw - 1).Select(w => g[r, w] == b[r, w] ? F1(b[r, w]) : $"{F1(b[r, w])} → **{F1(g[r, w])}**")) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
