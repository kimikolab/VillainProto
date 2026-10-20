using BattleCore;
using static Common;
using I = Inv348Diag;

// sh364 §4 ／ §6 —— 席の総当たり（アカ ／ ガレ）・勇者への届き方・代表台・`compare`。
static partial class Sh364Diag
{
    internal sealed record Seat(string Key, string Combo, Formation F);
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
    static IEnumerable<int[]> Choose(int n, int k, int start = 0)
    {
        if (k == 0) { yield return Array.Empty<int>(); yield break; }
        for (int i = start; i <= n - k; i++)
            foreach (var rest in Choose(n, k - 1, i + 1)) yield return new[] { i }.Concat(rest).ToArray();
    }
    /// <summary>固定 + 母集団から (5 − 固定) 枚 × 席の全並び 120。</summary>
    internal static Seat[] AllSeats(UnitDef[] fixd, UnitDef[] pool)
    {
        var list = new List<Seat>();
        var perms = Perms(5).ToArray();
        foreach (var pick in Choose(pool.Length, 5 - fixd.Length))
        {
            var units = fixd.Concat(pick.Select(i => pool[i])).ToArray();
            string combo = string.Join("＋", pick.Select(i => Short(pool[i])));
            foreach (var perm in perms)
            {
                var f = new Formation();
                for (int s = 0; s < 5; s++) f[s] = units[perm[s]];
                list.Add(new Seat(Seats(f), combo, f));
            }
        }
        return list.ToArray();
    }

    /// <summary>総当たりの1マス（verbose なし・帳簿だけ）。見る駒の勇者 ／ 敵への与ダメは帳簿の `DamageToEnemy`。</summary>
    internal sealed class Cell
    {
        public long N, Wins, WinT, Fell, Lost, WipeT, FocusDmg;
        public void Add(Cell o) { N += o.N; Wins += o.Wins; WinT += o.WinT; Fell += o.Fell; Lost += o.Lost; WipeT += o.WipeT; FocusDmg += o.FocusDmg; }
        public double Win => N == 0 ? 0 : 100.0 * Wins / N;
        public string WinTs => Wins == 0 ? "—" : (WinT / (double)Wins).ToString("0.0");
    }
    internal static Cell Fight(Formation f, I.W w, int seed, string focusId)
    {
        var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
        var r = BattleEngine.Run(p, w.Make(), seed, verbose: false, boss: new BossRule(false) { Scale = w.Scale });
        var c = new Cell { N = 1, Fell = p.Count(u => u.LastDeathTurn > 0) };
        if (r.PlayerWon) { c.Wins = 1; c.WinT = r.Turns; }
        else { c.Lost = 1; c.WipeT = p.Max(u => u.LastDeathTurn); }
        if (r.TallyByUnit.TryGetValue(focusId, out var t)) c.FocusDmg = t.DamageToEnemy;
        return c;
    }
    internal static Cell CellOf(Formation f, I.W w, int n, string focusId)
    {
        var parts = new Cell[n];
        Parallel.For(0, n, s => parts[s] = Fight(f, w, s, focusId));
        var all = new Cell();
        foreach (var x in parts) all.Add(x);
        return all;
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
    static Res[] Grid(Seat[] seats, Ver v)
    {
        var res = new Res[seats.Length];
        for (int k = 0; k < seats.Length; k++)
        {
            var f = Apply(seats[k].F, v);
            res[k] = new Res(seats[k], CellOf(f, I.Boss, BossSeeds, v.Id), CellOf(f, I.Guard, EliteSeeds, v.Id), CellOf(f, I.Bat, EliteSeeds, v.Id));
        }
        return res;
    }

    static bool Trio(Formation f) => Has(f, "tome") && Has(f, "borg") && Has(f, "hisa");

    static void SeatsAll(string only)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var sets = new List<(string Label, Ver[] Vers, Seat[] Seats, Func<Formation, bool> Split, string SplitName)>();
        if (only is "" or "aka") sets.Add(("アカ", AkaVers.Skip(1).ToArray(), AllSeats(AkaFixed, AkaPool), Trio, "ミサ＋ボルグ＋ヒサ"));
        if (only is "" or "gare") sets.Add(("ガレ", GareVers, AllSeats(GareFixed, GarePool), f => Has(f, "nel"), "ネル"));
        Console.WriteLine($"# {Tag} §4 席の総当たり —— 波: ボス（seed 0..{BossSeeds - 1}）／ 近衛 ／ 大隊（seed 0..{EliteSeeds - 1}）・規定の上（駒の版だけ）");
        Console.WriteLine();
        Console.WriteLine("順位は第356〜363期と同じ（ボスの勝ち数 → 精鋭の勝ち数 → 倒しT → 倒れた数）。「見る駒の与ダメ」は勇者戦で見る駒（アカ ／ ガレ）が敵へ与えた計 ／戦（帳簿の `DamageToEnemy`）。");
        Console.WriteLine();
        foreach (var (label, vers, seats, split, splitName) in sets)
        {
            var grids = vers.Select(v => (v, res: Grid(seats, v))).ToArray();
            Console.WriteLine($"## {label}（{seats.Length} 並び・固定 {string.Join("・", (label == "アカ" ? AkaFixed : GareFixed).Select(Short))}）");
            Console.WriteLine();
            Console.WriteLine($"| 版 | 勇者に勝てる並び（{splitName}を含む ／ 含まない） | 精鋭 90% 以上（含む ／ 含まない） | 両立（含む ／ 含まない） | 見る駒の与ダメ（勇者戦）／戦 |");
            Console.WriteLine("|---|--:|--:|--:|--:|");
            foreach (var (v, res) in grids)
            {
                int B(Func<Res, bool> f) => res.Count(r => r.Boss.Wins > 0 && f(r));
                int E(Func<Res, bool> f) => res.Count(r => r.Elite90 && f(r));
                int X(Func<Res, bool> f) => res.Count(r => r.Boss.Wins > 0 && r.Elite90 && f(r));
                var c = new Cell(); foreach (var r in res) c.Add(r.Boss);
                Console.WriteLine($"| {v.Name} | {B(r => split(r.S.F))} ／ {B(r => !split(r.S.F))} | {E(r => split(r.S.F))} ／ {E(r => !split(r.S.F))} | {X(r => split(r.S.F))} ／ {X(r => !split(r.S.F))} | {Per(c.FocusDmg, c.N)} |");
            }
            Console.WriteLine();
            foreach (var (v, res) in grids)
            {
                var ranked = Rank(res);
                Console.WriteLine($"### {label} × {v.Name}");
                Console.WriteLine();
                void Top(string title, IEnumerable<Res> xs, int n)
                {
                    Console.WriteLine(title);
                    Console.WriteLine();
                    Console.WriteLine("| # | 並び（前1・前3・中央・後1・後3） | 組 | ボス 勝率・倒しT | 近衛 | 大隊 | 倒れた ／戦 | 見る駒の与ダメ（勇者戦）／戦 |");
                    Console.WriteLine("|--:|---|---|---|---|---|--:|--:|");
                    int i = 0;
                    foreach (var r in xs.Take(n))
                        Console.WriteLine($"| {++i} | {r.S.Key} | {r.S.Combo} | {WT(r.Boss)} | {WT(r.Guard)} | {WT(r.Bat)} | {Per2(r.Fell, r.Boss.N + r.Guard.N + r.Bat.N)} | {Per(r.Boss.FocusDmg, r.Boss.N)} |");
                    if (i == 0) Console.WriteLine("| — | （該当なし） | | | | | | |");
                    Console.WriteLine();
                }
                Top("#### 上位5並び", ranked, 5);
                Top($"#### {splitName}を含まない上位5並び", ranked.Where(r => !split(r.S.F)), 5);
                Top("#### 両立（勇者に勝ち、精鋭もどちらも 90% 以上）の最良3", ranked.Where(r => r.Boss.Wins > 0 && r.Elite90), 3);
                Console.WriteLine("組ごと（勇者に勝てる並びがある組）: " + Join(res.GroupBy(r => r.S.Combo).Select(g => (g.Key, N: g.Count(r => r.Boss.Wins > 0)))) + "。");
                Console.WriteLine();
            }
        }
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
    static string Join(IEnumerable<(string Key, int N)> xs, int take = 100)
    {
        var l = xs.Where(x => x.N > 0).OrderByDescending(x => x.N).ThenBy(x => x.Key, StringComparer.Ordinal).Take(take).Select(x => $"{x.Key} {x.N}").ToArray();
        return l.Length == 0 ? "なし" : string.Join("、", l);
    }

    static IEnumerable<(I.Board B, Ver[] Vers)> AllBoards() =>
        AkaBoards().Select(b => (b, AkaVers)).Concat(GareBoards().Select(b => (b, GareVers)));

    static void ReachAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §6 の 2 勇者への届き方 —— 代表台 × 版 × ボス・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("見る駒 ＝ アカ ／ ガレ。勇者への与ダメは1戦あたり（1回 ＝ 勇者への与ダメ ÷ 勇者への一撃の数）。勇者への一撃 T1〜T3 は全員の和。撃った回数（跳ね撃ちの札の版だけ）・形 B の弾の数 ／ 形 A で集まった回数と倍率。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 勝率・倒しT | 見る駒の勇者への与ダメ ／戦（1回・回数） | 見る駒の敵への与ダメ ／戦 | 1ターンの与ダメの最大（出どころ） | 勇者への一撃 T1〜T3 | 羽の標撃ち T1〜T3 | 撃った ／戦（弾 ／ 集まり・倍率） |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|--:|---|");
        foreach (var (b, vers) in AllBoards())
            foreach (var v in vers)
            {
                var a = Measure(Apply(b.F, v), I.Boss, v.Id);
                Console.WriteLine($"| {b.Name} | {v.Name} | {F1(a.Win)}・{a.WinTs}T | {Per(a.FocusToBoss, a.N)}（{Per(a.FocusToBoss, a.FocusHits)}・{Per2(a.FocusHits, a.N)}） | {Per(a.FocusToFoe, a.N)} | {MaxBy(a)} | {Per(a.BossHits13, a.N)} | {Per(a.FeatherOnBoss13, a.N)} | {Per2(a.Volleys, a.N)}（{Per2(a.Shots, a.N)} ／ {Per2(a.Gathered, a.N)}・×{Per2(a.GatherMult, a.Gathered)}） |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine($"# {Tag} §6 の 3・5 代表台 × {Waves.Length} 波 × 版・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("勝率・決着T（ボスは倒しT）・倒れた ／戦・打ち切り %。基準の列はアカ ＝ AK-n（と規定）・ガレ ＝ 規定（壊れの兆しは基準との差）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        var bad = new List<string>();
        foreach (var (b, vers) in AllBoards())
        {
            Kx[]? baseRow = null;
            Ver basis = vers == AkaVers ? AkaVers[1] : GareVers[0];
            foreach (var v in vers)
            {
                var f = Apply(b.F, v);
                var row = Waves.Select(w => Measure(f, w, v.Id)).ToArray();
                if (v == basis) baseRow = row;
                Console.WriteLine($"| {b.Name} | {v.Name} | " + string.Join(" | ", row.Select(a => $"{F1(a.Win)}・{a.WinTs}T・倒 {Per2(a.Fell, a.N)}{(a.Draw > 0 ? $"・分 {Pct(a.Draw, a.N)}" : "")}")) + " |");
                bool check = vers == AkaVers ? v.Ascii is "akna" or "aknb" : v.Ascii is "gra" or "grb";
                if (!check) continue;
                for (int i = 0; i < Waves.Length; i++)
                {
                    var a = row[i]; var w = Waves[i];
                    if ((w == I.Guard || w == I.Bat) && 100.0 * a.Win1 / a.N >= 5) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 1T 決着 {Pct(a.Win1, a.N)}%");
                    if (baseRow is not null && baseRow[i].Win - a.Win >= 15) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 勝率 {F1(baseRow[i].Win)} → {F1(a.Win)}");
                    if (w == I.Boss && a.Wins > 0 && (double)a.WinT / a.Wins < 4) bad.Add($"{b.Name} × {v.Name} × ボス: 倒しT {a.WinTs}");
                    if (baseRow is not null && 100.0 * (a.Draw - baseRow[i].Draw) / a.N >= 5) bad.Add($"{b.Name} × {v.Name} × {w.Name}: 打ち切り {Pct(baseRow[i].Draw, a.N)} → {Pct(a.Draw, a.N)}%");
                }
            }
        }
        Console.WriteLine();
        Console.WriteLine("## 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ 勝率が基準から 15pt 以上落ちる ／ ボス 4T 未満 ／ 30T の打ち切りが基準から 5pt 以上増える・跳ね撃ちの版だけ）");
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

    static void CompareAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        var b = CGrid(rows, f => f);
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        string Means(double[,] g) => string.Join(" ／ ", Enumerable.Range(1, nw - 1).Select(w => F1(Enumerable.Range(0, rows.Length).Average(r => g[r, w]))));
        Console.WriteLine($"# {Tag} §6 の 4 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"アカのいる行 {rows.Count(r => Has(r.F, "susu"))}・ガレのいる行 {rows.Count(r => Has(r.F, "gare"))}（{string.Join("、", rows.Where(r => Has(r.F, "gare")).Select(r => r.Name))}）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | 主判定19行の第五波 | 全行の第2〜5波の平均 |");
        Console.WriteLine("|---|---|---|--:|---|");
        Console.WriteLine($"| 規定 | — | — | {F1(pr.Average(ri => b[ri, last]))} | {Means(b)} |");
        foreach (var v in AkaVers.Skip(1).Concat(GareVers.Skip(1)))
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
            for (int r = 0; r < rows.Length; r++)
            {
                if (!Enumerable.Range(1, nw - 1).Any(w => b[r, w] - g[r, w] >= 10.0)) continue;
                var parts = new List<string>();
                foreach (var id in rows[r].F.Occupied().Select(o => o.Def.Id).Distinct())
                {
                    var oth = Enumerable.Range(0, rows.Length).Where(o => o != r && rows[o].F.Occupied().Any(x => x.Def.Id == id)).ToList();
                    string nm = Short(rows[r].F.Occupied().First(o => o.Def.Id == id).Def);
                    if (oth.Count == 0) { parts.Add($"{nm} 他の行 0（分解が成立しない）"); continue; }
                    double dd = oth.Average(o => Enumerable.Range(1, nw - 1).Average(w => g[o, w] - b[o, w]));
                    parts.Add($"{nm} {(dd >= 0 ? "+" : "")}{dd:F1}（{oth.Count} 行）");
                }
                Console.WriteLine($"|  | (G2) {rows[r].Name}: {string.Join("・", parts)} | | | |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
