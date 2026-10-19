using BattleCore;
using static Common;
using I = Inv348Diag;
using S8 = Sh358Diag;
using S9 = Sh359Diag;
using S0 = Sh360Diag;
using S316 = Shiga316Diag;

// sh361 §5 ／ §6 —— 席の総当たり ／ 切り落としの行き先と届き方 ／ 代表台 ／ `compare`。
static partial class Sh361Diag
{
    internal sealed record Res(S0.Seat S, S9.Cell Boss, S9.Cell Guard, S9.Cell Bat)
    {
        public long Elite => Guard.Wins + Bat.Wins;
        public double KillT => Boss.Wins == 0 ? double.MaxValue : (double)Boss.WinT / Boss.Wins;
        public long Fell => Boss.Fell + Guard.Fell + Bat.Fell;
        public bool Elite90 => Guard.Win >= 90 && Bat.Win >= 90;
    }
    static Res[] Rank(IEnumerable<Res> xs) => xs
        .OrderByDescending(r => r.Boss.Wins).ThenByDescending(r => r.Elite).ThenBy(r => r.KillT).ThenBy(r => r.Fell)
        .ThenBy(r => r.S.Key, StringComparer.Ordinal).ToArray();
    static string WT(S9.Cell c) => $"{F1(c.Win)}・{c.WinTs}T";
    static Res[] Grid(S0.Seat[] seats, Ver v)
    {
        var res = new Res[seats.Length];
        for (int k = 0; k < seats.Length; k++)
        {
            var f = Apply(seats[k].F, v);
            res[k] = new Res(seats[k], S9.CellOf(f, I.Boss, BossSeeds), S9.CellOf(f, I.Guard, EliteSeeds), S9.CellOf(f, I.Bat, EliteSeeds));
        }
        return res;
    }

    static void SeatsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var seats = S0.AllSeats().Select(s => s with { F = PinSasaNow(s.F) }).ToArray();
        var grids = Vers.Select(v => (v, res: Grid(seats, v))).ToArray();
        bool Som(Res r) => Has(r.S.F, "som");
        bool Nel(Res r) => Has(r.S.F, "nel");
        Console.WriteLine($"# {Tag} §5 席の総当たり（第360期 §5 と同じ母集団）—— ササ ／ ヒビ ／ ガレ ＋ {string.Join("・", Pool.Select(Short))} から2枚 × 120 ＝ {seats.Length} 並び × 規定（SS-w）／ SS-s ／ SS-n");
        Console.WriteLine();
        Console.WriteLine($"波: ボス（seed 0..{BossSeeds - 1}）／ 近衛 ／ 大隊（seed 0..{EliteSeeds - 1}）。順位は第356〜360期と同じ。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 勇者に勝てる並び（ソムなし ／ あり） | うちネルなし（ソムなし ／ あり） | 精鋭 90% 以上（ソムなし ／ あり） | 両立（ソムなし ／ あり） | 勝ち並びのササの席（前1 ／ 前3 ／ 中央 ／ 後1 ／ 後3） |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        foreach (var (v, res) in grids)
        {
            int C(Func<Res, bool> f) => res.Count(f);
            Console.WriteLine($"| {v.Name} | {C(r => r.Boss.Wins > 0 && !Som(r))} ／ {C(r => r.Boss.Wins > 0 && Som(r))} | {C(r => r.Boss.Wins > 0 && !Som(r) && !Nel(r))} ／ {C(r => r.Boss.Wins > 0 && Som(r) && !Nel(r))} | "
                + $"{C(r => r.Elite90 && !Som(r))} ／ {C(r => r.Elite90 && Som(r))} | {C(r => r.Boss.Wins > 0 && r.Elite90 && !Som(r))} ／ {C(r => r.Boss.Wins > 0 && r.Elite90 && Som(r))} | "
                + string.Join(" ／ ", Enumerable.Range(0, 5).Select(k => res.Count(r => r.Boss.Wins > 0 && r.S.F[k]?.Id == "sasa"))) + " |");
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
                Console.WriteLine("| # | 並び（前1・前3・中央・後1・後3） | 組 | ボス 勝率・倒しT | 全滅 T | 近衛 | 大隊 | 倒れた ／戦 |");
                Console.WriteLine("|--:|---|---|---|--:|---|---|--:|");
                int i = 0;
                foreach (var r in xs.Take(n))
                    Console.WriteLine($"| {++i} | {r.S.Key} | {r.S.Pair} | {WT(r.Boss)} | {Per(r.Boss.WipeT, r.Boss.Lost)} | {WT(r.Guard)} | {WT(r.Bat)} | {Per2(r.Fell, r.Boss.N + r.Guard.N + r.Bat.N)} |");
                if (i == 0) Console.WriteLine("| — | （該当なし） | | | | | | |");
                Console.WriteLine();
            }
            Top("### 上位5並び", ranked, 5);
            Top("### 勇者に勝ち、精鋭もどちらも 90% 以上の並び（両立）の最良5", ranked.Where(r => r.Boss.Wins > 0 && r.Elite90), 5);
            Top("### ソムなしの最良3", ranked.Where(r => !Som(r)), 3);
            Top("### ネルなしの最良3", ranked.Where(r => !Nel(r)), 3);
            Console.WriteLine("組ごと（勇者に勝てる並びがある組）: " + string.Join("、", res.GroupBy(r => r.S.Pair).Select(g => (g.Key, N: g.Count(r => r.Boss.Wins > 0))).Where(x => x.N > 0).OrderByDescending(x => x.N).Select(x => $"{x.Key} {x.N}")) + "。");
            Console.WriteLine();
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
    /// <summary>`sh360` の母集団はササを旧（`SasaH360`）で組むので、規定のササ（SS-w）に戻す（版は <see cref="Apply"/> で替える）。</summary>
    static Formation PinSasaNow(Formation f) => S316.SwapId(f, "sasa", UnitCatalog.Sasa);

    static void FlowAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        Console.WriteLine($"# {Tag} §6 の 1・2 切り落としの行き先 ／ ボスへの届き方 —— 代表台 {boards.Length} × ボス ／ 大隊 × 版・seed 0..{Seeds - 1}");
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
                foreach (var w in new[] { I.Bat, I.Boss }) Console.WriteLine(Dest($"{v.Name} × {w.Name}", S0.BraceOf(f, w, Seeds)));
                Console.WriteLine();
                var a = S8.Measure(f, I.Boss, Seeds, TrajT);
                S8.PrintTraj($"{v.Name} × ボス", a);
                if (Has(b.F, "nel")) Db355Diag.PrintTraj($"{v.Name} × ボス（勇者の最大HP）", Db355Diag.Measure(f, I.Boss, Seeds, TrajT));
            }
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        Console.WriteLine($"# {Tag} §6 の 3・5 代表台 {boards.Length} × {Waves.Length} 波 × 版・seed 0..{Seeds - 1}");
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
            S8.Fx[]? baseRow = null; long[]? baseDraw = null;
            foreach (var v in vers)
            {
                var f = Apply(b.F, v);
                var row = Waves.Select(w => S8.Measure(f, w)).ToArray();
                var draws = Waves.Select(w => S0.BraceOf(f, w, Seeds).Draw).ToArray();
                if (v == Vers[0]) { baseRow = row; baseDraw = draws; }
                Console.WriteLine($"| {b.Name} | {v.Name} | " + string.Join(" | ", row.Select((a, i) => $"{F1(a.Win)}・{a.WinTs}T・倒 {Per2(a.Fell, a.N)}{(draws[i] > 0 ? $"・分 {Pct(draws[i], a.N)}" : "")}")) + " |");
                for (int i = 0; i < Waves.Length; i++)
                {
                    var a = row[i]; var w = Waves[i];
                    if ((w == I.Guard || w == I.Bat) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 1T 決着 {Pct(a.Win1, a.N)}%");
                    if (baseRow is not null && v != Vers[0] && baseRow[i].Win - a.Win >= 15) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 勝率 {F1(baseRow[i].Win)} → {F1(a.Win)}");
                    if (w == I.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4) bad.Add($"{b.Name} × {v.Name} × ボス: 倒しT {a.WinTs}");
                    if (baseDraw is not null && v != Vers[0] && 100.0 * (draws[i] - baseDraw[i]) / a.N >= 5) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 打ち切り {Pct(baseDraw[i], a.N)} → {Pct(draws[i], a.N)}%");
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
        var b360 = CGrid(rows, Pin360);
        Console.WriteLine($"# {Tag} §6-4 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定 ＝ 段0 入り）");
        Console.WriteLine();
        var moved = new List<string>();
        for (int r = 0; r < rows.Length; r++) for (int w = 0; w < nw; w++) if (b[r, w] != b360[r, w]) moved.Add($"{rows[r].Name} 第{w + 1}波 {F1(b360[r, w])} → {F1(b[r, w])}");
        Console.WriteLine($"段0（ササの SS-w を規定に）: 第360期までの規定とのセルの差 **{moved.Count}**{(moved.Count == 0 ? "" : "（" + string.Join("・", moved) + "）")}。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|--:|---|");
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] g) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => g[r, w]))));
        Console.WriteLine($"| 第360期までの規定 | — | — | {F1(pr.Average(ri => b360[ri, last]))} | {Means(b360)} |");
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
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
