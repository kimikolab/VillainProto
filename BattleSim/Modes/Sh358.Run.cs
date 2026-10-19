using BattleCore;
using static Common;
using I = Inv348Diag;
using C6 = Db356Diag;

// sh358 §4 ／ §6 —— 席の総当たり ／ 破片の流れ ／ 代表台 ／ 軸の横並び ／ `compare`。
static partial class Sh358Diag
{
    // ---------------------------------------------------------------------------------
    // §4 母集団
    // ---------------------------------------------------------------------------------
    internal static readonly UnitDef[] Fixed = { UnitCatalog.Hibi, UnitCatalog.Gare };
    internal static readonly UnitDef[] Pool = { UnitCatalog.Uro, UnitCatalog.Tsugi, UnitCatalog.Sasa, UnitCatalog.Gald, UnitCatalog.Dolga, UnitCatalog.Golm };
    internal sealed record Seat(string Key, string Trio, Formation F);

    /// <summary>20 組 × 5枚の席の全並び 120 ＝ 2,400 並び（駒は規定・版は <see cref="Apply"/> で替える）。並びの順は組 → 席の辞書順（乱数を引かない）。</summary>
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
    /// <summary>並びの名前（「ヒビ・ガレ・…」）から規定の駒の盤を組む。</summary>
    internal static Formation ByKey(string key)
    {
        var defs = key.Split('・').Select(n => Fixed.Concat(Pool).First(d => Short(d) == n)).ToArray();
        var f = new Formation();
        for (int s = 0; s < 5; s++) f[s] = defs[s];
        return f;
    }

    internal sealed record Res(Seat S, C6.Cell Boss, C6.Cell Guard, C6.Cell Bat)
    {
        public long Elite => Guard.Wins + Bat.Wins;
        public double KillT => Boss.Wins == 0 ? double.MaxValue : (double)Boss.WinT / Boss.Wins;
        public long Fell => Boss.Fell + Guard.Fell + Bat.Fell;
        public bool Elite90 => Guard.Win >= 90 && Bat.Win >= 90;
    }
    static Res[] Rank(IEnumerable<Res> xs) => xs
        .OrderByDescending(r => r.Boss.Wins).ThenByDescending(r => r.Elite).ThenBy(r => r.KillT).ThenBy(r => r.Fell)
        .ThenBy(r => r.S.Key, StringComparer.Ordinal).ToArray();
    static string WT(C6.Cell c) => $"{F1(c.Win)}・{c.WinTs}T";

    internal static Res[] Grid(Seat[] seats, Ver v)
    {
        var res = new Res[seats.Length];
        for (int k = 0; k < seats.Length; k++)
        {
            var f = Apply(seats[k].F, v);
            res[k] = new Res(seats[k], C6.CellOf(f, I.Boss, BossSeeds), C6.CellOf(f, I.Guard, EliteSeeds), C6.CellOf(f, I.Bat, EliteSeeds));
        }
        return res;
    }

    static void SeatsAll(Ver v)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var seats = AllSeats();
        var res = Grid(seats, v);
        var bas = v == Vers[0] ? res : Grid(seats, Vers[0]);
        var ranked = Rank(res);
        var trios = seats.Select(s => s.Trio).Distinct().ToArray();
        Console.WriteLine($"# {Tag} §4 席の総当たり —— ヒビ ／ ガレ ＋ {string.Join("・", Pool.Select(Short))} から3枚（{trios.Length} 組）× 席の全並び 120 ＝ {seats.Length} 並び × 版「{v.Name}」（対照は規定）");
        Console.WriteLine();
        Console.WriteLine($"波: ボス（seed 0..{BossSeeds - 1}）／ 近衛 ／ 大隊（seed 0..{EliteSeeds - 1}）。順位は「ボスの勝ち → 近衛 ＋ 大隊の勝ち → ボスの倒しT → 3波の倒れた数」（第356期と同じ）。");
        Console.WriteLine();
        Console.WriteLine("## 1. ボスに勝てる並び（組ごと）");
        Console.WriteLine();
        Console.WriteLine("| 組 | ボスに勝てる並び（／120）版 ／ 規定 | 精鋭 90% 以上 版 ／ 規定 | 両方 | 組の最良（版） |");
        Console.WriteLine("|---|--:|--:|--:|---|");
        foreach (var t in trios.OrderByDescending(t => res.Count(r => r.S.Trio == t && r.Boss.Wins > 0)).ThenBy(t => t, StringComparer.Ordinal))
        {
            var xs = res.Where(r => r.S.Trio == t).ToArray(); var bs = bas.Where(r => r.S.Trio == t).ToArray();
            var best = Rank(xs)[0];
            Console.WriteLine($"| {t} | {xs.Count(r => r.Boss.Wins > 0)} ／ {bs.Count(r => r.Boss.Wins > 0)} | {xs.Count(r => r.Elite90)} ／ {bs.Count(r => r.Elite90)} | {xs.Count(r => r.Boss.Wins > 0 && r.Elite90)} | {best.S.Key}（ボス {WT(best.Boss)} ／ 近衛 {F1(best.Guard.Win)} ／ 大隊 {F1(best.Bat.Win)}） |");
        }
        Console.WriteLine();
        Console.WriteLine($"ボスに勝てる並び: 版 **{res.Count(r => r.Boss.Wins > 0)}** ／ 規定 {bas.Count(r => r.Boss.Wins > 0)}（／ {res.Length}）。精鋭がどちらも 90% 以上: 版 {res.Count(r => r.Elite90)} ／ 規定 {bas.Count(r => r.Elite90)}。両立: 版 {res.Count(r => r.Boss.Wins > 0 && r.Elite90)} ／ 規定 {bas.Count(r => r.Boss.Wins > 0 && r.Elite90)}。");
        Console.WriteLine();
        void Top(string title, IEnumerable<Res> xs, int n)
        {
            Console.WriteLine(title);
            Console.WriteLine();
            Console.WriteLine("| # | 並び（前1・前3・中央・後1・後3） | 組 | ボス 勝率・倒しT | 全滅 T | 近衛 | 大隊 | 倒れた ／戦 | 規定のボス ／ 近衛 ／ 大隊 |");
            Console.WriteLine("|--:|---|---|---|--:|---|---|--:|---|");
            int i = 0;
            foreach (var r in xs.Take(n))
            {
                var b = bas.First(x => x.S.Key == r.S.Key);
                Console.WriteLine($"| {++i} | {r.S.Key} | {r.S.Trio} | {WT(r.Boss)} | {Per(r.Boss.WipeT, r.Boss.Lost)} | {WT(r.Guard)} | {WT(r.Bat)} | {Per2(r.Fell, r.Boss.N + r.Guard.N + r.Bat.N)} | {F1(b.Boss.Win)} ／ {F1(b.Guard.Win)} ／ {F1(b.Bat.Win)} |");
            }
            Console.WriteLine();
        }
        Top("## 2. 上位10並び", ranked, 10);
        Top("## 3. ツギを含まない並びの最良5（破片軸の基準）", ranked.Where(r => !Has(r.S.F, "tsugi")), 5);
        Top("## 4. 精鋭（近衛 ／ 大隊）がどちらも 90% 以上の並びのうち、ボスの勝率が高い5", ranked.Where(r => r.Elite90), 5);
        Top("## 4b. ツギなし・精鋭 90% 以上の最良5", ranked.Where(r => r.Elite90 && !Has(r.S.F, "tsugi")), 5);

        Console.WriteLine("## 5. どの駒が効くか（その駒を含む 10 組 × 120 ＝ 1,200 並びのうち）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | ボスに勝てる並び 版 ／ 規定 | ボスの勝率の平均 版 ／ 規定 | ボスの全滅 T（負けた戦）| 近衛 ／ 大隊の勝率の平均（版） |");
        Console.WriteLine("|---|--:|--:|--:|---|");
        foreach (var d in Pool)
        {
            var xs = res.Where(r => Has(r.S.F, d.Id)).ToArray(); var bs = bas.Where(r => Has(r.S.F, d.Id)).ToArray();
            long lost = xs.Sum(r => r.Boss.Lost), wipe = xs.Sum(r => r.Boss.WipeT);
            Console.WriteLine($"| {Short(d)} | {xs.Count(r => r.Boss.Wins > 0)} ／ {bs.Count(r => r.Boss.Wins > 0)} | {F1(xs.Average(r => r.Boss.Win))} ／ {F1(bs.Average(r => r.Boss.Win))} | {Per(wipe, lost)} | {F1(xs.Average(r => r.Guard.Win))} ／ {F1(xs.Average(r => r.Bat.Win))} |");
        }
        Console.WriteLine();
        Console.WriteLine("席ごと（その駒がその席にいる並びのボスの勝率の平均・版・全組）:");
        Console.WriteLine();
        string[] slot = { "前1", "前3", "中央", "後1", "後3" };
        Console.WriteLine("| 駒 | " + string.Join(" | ", slot) + " | 近衛 ＋ 大隊 前1 ／ 前3 ／ 中央 ／ 後1 ／ 後3 |");
        Console.WriteLine("|---|" + string.Concat(slot.Select(_ => "--:|")) + "---|");
        foreach (var d in Fixed.Concat(Pool))
        {
            string Cell(int i, Func<Res, double> f) { var xs = res.Where(r => r.S.F[i]?.Id == d.Id).ToArray(); return xs.Length == 0 ? "—" : F1(xs.Average(f)); }
            Console.WriteLine($"| {Short(d)} | " + string.Join(" | ", Enumerable.Range(0, 5).Select(i => Cell(i, r => r.Boss.Win))) + " | "
                + string.Join(" ／ ", Enumerable.Range(0, 5).Select(i => Cell(i, r => (r.Guard.Win + r.Bat.Win) / 2))) + " |");
        }
        Console.WriteLine();
        var lostAll = new C6.Cell();
        foreach (var r in res) lostAll.Add(r.Boss);
        Console.WriteLine($"ボスの戦 {lostAll.N} 戦のうち負け {lostAll.Lost}・全滅 T の平均 {Per(lostAll.WipeT, lostAll.Lost)}・最初に倒れる駒: {string.Join("、", lostAll.First.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Pct(kv.Value, lostAll.N)}%"))}。");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §6 代表台
    // ---------------------------------------------------------------------------------
    internal static I.Board[] Boards() => new[]
    {
        new I.Board("破片", GareRow, Row(GareRow)),
        new I.Board("破片", UroRow, Row(UroRow)),
    }.Concat(RepKeys.Select(r => new I.Board("破片", $"{r.Label}（{r.Key}）", ByKey(r.Key)))).ToArray();

    static void FlowAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        Console.WriteLine($"# {Tag} §6 の 1・2 破片の流れ ／ ボスへの届き方 —— 代表台 {boards.Length} × ボス ／ 大隊 × 版・seed 0..{Seeds - 1}");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();
        foreach (var b in boards)
        {
            Console.WriteLine($"## {b.Name}");
            Console.WriteLine();
            foreach (var v in vers)
            {
                if (v != Vers[0] && !Touches(b.F, v)) continue;
                var f = Apply(b.F, v);
                PrintFlow($"{v.Name} × 大隊", Measure(f, I.Bat));
                var bo = Measure(f, I.Boss);
                PrintFlow($"{v.Name} × ボス", bo);
                PrintTraj($"{v.Name} × ボス", bo);
            }
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        Console.WriteLine($"# {Tag} §6 の 3・6 代表台 {boards.Length} × {Waves.Length} 波 × 版・seed 0..{Seeds - 1}");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();
        Console.WriteLine("勝率・決着T（ボスは倒しT）・倒れた ／戦。版が台の駒に触らないときは「—」。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        var bad = new List<string>();
        foreach (var b in boards)
        {
            Fx[]? baseRow = null;
            foreach (var v in vers)
            {
                if (v != Vers[0] && !Touches(b.F, v)) { Console.WriteLine($"| {b.Name} | {v.Name} | " + string.Join(" | ", Waves.Select(_ => "—")) + " |"); continue; }
                var f = Apply(b.F, v);
                var row = Waves.Select(w => Measure(f, w)).ToArray();
                if (v == Vers[0]) baseRow = row;
                Console.WriteLine($"| {b.Name} | {v.Name} | " + string.Join(" | ", row.Select(a => $"{F1(a.Win)}・{a.WinTs}T・倒 {Per2(a.Fell, a.N)}")) + " |");
                for (int i = 0; i < Waves.Length; i++)
                {
                    var a = row[i]; var w = Waves[i];
                    if ((w == I.Guard || w == I.Bat) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 1T 決着 {Pct(a.Win1, a.N)}%");
                    if (baseRow is not null && v != Vers[0] && baseRow[i].Win - a.Win >= 15) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 勝率 {F1(baseRow[i].Win)} → {F1(a.Win)}");
                    if (w == I.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4) bad.Add($"{b.Name} × {v.Name} × ボス: 倒しT {a.WinTs}");
                }
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ 勝率が規定から 15pt 以上落ちる ／ ボス 4T 未満）");
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
        Console.WriteLine($"# {Tag} §6-4 軸の横並び（第348期 §3 の物差し）—— 破片の行 × 版・seed 0..{I.Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("ほかの軸の行は `inv348` の台（燃焼の3枚は第349期までの規定・古い口の終端でウツ ／ ムド ／ ワタ ／ ネルも旧）。破片の行は代表台 × 版（いまの規定の上）。");
        Console.WriteLine();
        Console.WriteLine("| 軸 ／ 台 | ボス 勝率・T | 近衛 | 大隊 | 本編2 | 本編4 | 本編5 | 九体 | 的・一 8T | 的・九 8T | 強 2〜5 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|---|--:|--:|--:|");
        foreach (var b in others) Console.WriteLine(Line($"{b.Axis}: {b.Name}", oth[b.Name]));
        var mine = new List<(string Label, double[] V)>();
        foreach (var b in Boards())
            foreach (var v in Vers)
            {
                if (v != Vers[0] && !Touches(b.F, v)) continue;
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
        bool HasHG(Formation f) => Has(f, "hibi") || Has(f, "gare");
        var b = CGrid(rows, f => Apply(f, vers[0]));
        Console.WriteLine($"# {Tag} §6-5 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"ヒビ ／ ガレのいる行は {rows.Count(r => HasHG(r.F))} 行（{string.Join("・", rows.Where(r => HasHG(r.F)).Select(r => r.Name))}）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | うちヒビ ／ ガレのいない行 | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|---|--:|---|");
        var grids = vers.Skip(1).Select(v => (v.Name, g: CGrid(rows, f => Apply(f, v)))).ToArray();
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] g) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => g[r, w]))));
        Console.WriteLine($"| 規定 | — | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} |");
        foreach (var (name, g) in grids)
        {
            int upR = 0, upC = 0, dnR = 0, dnC = 0, other = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                bool u = false, d = false;
                for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
                if (u) upR++; if (d) dnR++;
                if ((u || d) && !HasHG(rows[r].F)) other++;
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
