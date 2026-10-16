using BattleCore;
using static Common;
using I = Inv348Diag;
using S316 = Shiga316Diag;

// burn349 §6 —— 版の測定（代表台 ／ 席の総当たり ／ 軸の横並び ／ `compare`）。
static partial class Burn349Diag
{
    static bool IsBurnBoard(I.Board b) => b.Axis == "燃焼";

    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var res = new Dictionary<(string, string, string), I.St>();
        var src = new Dictionary<(string, string), (I.St S, Src A)>();
        foreach (var b in boards) foreach (var v in vers) foreach (var w in Waves)
        {
            if (b.Name == "ポンの台") { var x = MeasureSrc(Apply(b.F, v), w); src[(v.Name, w.Name)] = x; res[(b.Name, v.Name, w.Name)] = x.S; }
            else res[(b.Name, v.Name, w.Name)] = I.Measure(IsBurnBoard(b) ? Apply(b.F, v) : b.F, w);
        }
        Console.WriteLine($"# {Tag} §6 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Ascii))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("版はボルグ ／ ホタ ／ ヒヨだけを差し替える（燃焼の台だけ。移動 ／ 標 ／ 感電の台は物差しで、版の駒がいないので規定のまま回す）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- **{b.Axis}** {b.Name}: {Seats(b.F)}");
        Console.WriteLine();

        // ---- 1. ポンの台 ----
        Console.WriteLine("## 1. ポンの台の攻めと守り（規定 → 版）");
        Console.WriteLine();
        var ids = Pon.Occupied().Select(o => o.Def).ToArray();
        foreach (var w in Waves.Where(w => w == I.Boss || w == I.Guard || w == I.Bat || w == I.StrongWaves[0] || w == I.StrongWaves[3]))
        {
            Console.WriteLine($"### {w.Name}");
            Console.WriteLine();
            Console.WriteLine("| 版 | 勝率・T | 倒れた ／戦 | " + string.Join(" | ", ids.Select(d => $"{Short(d)} 倒% ／ 生T")) + " |");
            Console.WriteLine("|---|---|--:|" + string.Concat(ids.Select(_ => "---|")));
            foreach (var v in vers)
            {
                var s = res[("ポンの台", v.Name, w.Name)];
                Console.WriteLine($"| {v.Ascii} | {F1(s.Win)}・{s.WinTs}T | {Per2(s.AllyFell, s.N)} | " + string.Join(" | ", ids.Select(d => $"{Pct(s.Deaths.GetValueOrDefault(d.Id), s.N)} ／ {Per(s.AliveT.GetValueOrDefault(d.Id), s.N)}")) + " |");
            }
            Console.WriteLine();
            PrintSrc($"ポンの台 × {w.Name}", vers.Select(v => (v.Ascii, src[(v.Name, w.Name)].S, src[(v.Name, w.Name)].A)));
        }
        Console.WriteLine("強 第3 ／ 第4波の内訳は §2 の表に勝率だけ載せた。");
        Console.WriteLine();

        // ---- 2. 代表台 × 波（勝率・T）----
        Console.WriteLine("## 2. 代表台 × 波（勝率・決着T（ボスは倒しT）・倒れた ／戦）");
        Console.WriteLine();
        foreach (var b in boards)
        {
            var vs = IsBurnBoard(b) ? vers : new[] { vers[0] };
            Console.WriteLine($"### {b.Axis}: {b.Name}{(IsBurnBoard(b) ? "" : "（物差し・版の駒がいないので規定だけ）")}");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
            Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
            foreach (var v in vs)
                Console.WriteLine($"| {v.Ascii} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var s = res[(b.Name, v.Name, w.Name)]; var z = res[(b.Name, vers[0].Name, w.Name)];
                    string d = v == vers[0] || Math.Abs(s.Win - z.Win) < 0.05 ? "" : $"（{Sg(s.Win - z.Win)}）";
                    return $"{F1(s.Win)}{d}・{s.WinTs}T・倒 {Per2(s.AllyFell, s.N)}";
                })) + " |");
            Console.WriteLine();
        }

        // ---- 3. 壊れの兆し ----
        Console.WriteLine("## 3. 壊れの兆し（精鋭の 1T 決着 5% 以上 ／ 燃焼以外の台が規定から 15pt 以上落ちる ／ ボスを 4T 未満で倒す）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var bad = new List<string>();
            foreach (var b in boards.Where(IsBurnBoard))
                foreach (var w in Waves)
                {
                    var s = res[(b.Name, v.Name, w.Name)];
                    if (w == I.Boss && s.Wins > 0 && (double)s.WinT / s.Wins < 4.0) bad.Add($"{b.Name} × ボス {s.WinTs}T");
                }
            Console.WriteLine($"- **{v.Name}**: {(bad.Count == 0 ? "なし" : string.Join("・", bad))}（燃焼以外の台は版の駒がいないので動かない）");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 席の総当たり × 版（ボスだけ）
    // ---------------------------------------------------------------------------------
    static void SeatsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var seats = AllSeats();
        var res = Vers.ToDictionary(v => v.Name, v => GridBoss(v, seats));
        Console.WriteLine($"# {Tag} §6-2 席の総当たり × 版 —— 並び {seats.Length}（3枚 60 × 埋め方 {Fillers.Length}）× ボス・seed 0..{GridSeeds - 1}");
        Console.WriteLine();
        Console.WriteLine("| 版 | ヒヨの席 | " + string.Join(" | ", BucketNames) + " | ボスの勝率（並びの平均） | 最良の並び（勝率・T） |");
        Console.WriteLine("|---|---|" + string.Concat(BucketNames.Select(_ => "--:|")) + "--:|---|");
        foreach (var v in Vers)
            foreach (var row in new[] { "全部", "前列", "中央", "後列" })
            {
                var xs = seats.Where(s => row == "全部" || Row(s.Hiyo) == row).ToArray();
                var cnt = BucketNames.Select((_, b) => xs.Count(s => Bucket(res[v.Name][s]) == b)).ToArray();
                double avg = xs.Average(s => 100.0 * res[v.Name][s].Wins / res[v.Name][s].N);
                var best = xs.OrderByDescending(s => res[v.Name][s].Wins).ThenBy(s => res[v.Name][s].Wins == 0 ? 99 : (double)res[v.Name][s].WinT / res[v.Name][s].Wins).First();
                var bc = res[v.Name][best];
                Console.WriteLine($"| {(row == "全部" ? $"**{v.Ascii}**" : v.Ascii)} | {row} | " + string.Join(" | ", cnt) + $" | {F1(avg)} | {(row == "全部" ? $"{best.Key}（{Pct(bc.Wins, bc.N)}・{(bc.Wins == 0 ? "—" : F1((double)bc.WinT / bc.Wins))}T）" : "")} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 軸の横並び（第348期 §3）の燃焼の行 × 版
    // ---------------------------------------------------------------------------------
    static void AxesAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var others = I.Boards().Where(b => b.Axis is "移動" or "標" or "感電").ToArray();
        var burn = Boards().Where(IsBurnBoard).ToArray();
        I.W[] aw = I.AxisWaves;
        string Row(string label, Formation f)
        {
            var r = aw.ToDictionary(w => w.Name, w => I.Measure(f, w));
            string WT(I.W w) => $"{F1(r[w.Name].Win)}・{r[w.Name].WinTs}T";
            return $"| {label} | {WT(I.Boss)} | {WT(I.Guard)} | {WT(I.Bat)} | {F1(r[I.M2.Name].Win)} | {F1(r[I.M4.Name].Win)} | {F1(r[I.M5.Name].Win)} | {WT(I.Nine)} | "
                + $"{Per(r[I.T1.Name].Dmg8, r[I.T1.Name].N)} | {Per(r[I.T9.Name].Dmg8, r[I.T9.Name].N)} | {F1(I.StrongWaves.Average(w => r[w.Name].Win))} | "
                + $"{Per2(r[I.Boss.Name].AllyFell, r[I.Boss.Name].N)} ／ {Per2(r[I.Bat.Name].AllyFell, r[I.Bat.Name].N)} ／ {Per2(r[I.M5.Name].AllyFell, r[I.M5.Name].N)} |";
        }
        Console.WriteLine($"# {Tag} §6-3 軸の横並び（第348期 §3 の物差し）—— 燃焼の行を版ごとに・seed 0..{I.Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("| 軸 ／ 台 ／ 版 | ボス 勝率・T | 近衛 | 大隊 | 本編2 | 本編4 | 本編5 | 九体 | 的・一 8T | 的・九 8T | 強 2〜5 | 倒れた（ボス ／ 大隊 ／ 第5波） |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|---|--:|--:|--:|---|");
        foreach (var b in others) Console.WriteLine(Row($"{b.Axis}: {b.Name}", b.F));
        foreach (var b in burn) foreach (var v in Vers) Console.WriteLine(Row($"燃焼: {b.Name} × {v.Ascii}", Apply(b.F, v)));
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // `compare` × 版
    // ---------------------------------------------------------------------------------
    static double[,] Grid((string Name, Formation F)[] rows, Func<Formation, Formation> map)
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

    /// <summary>BN-d を「規則」として全行に掛ける写し（決めたこと）: 行の最初の駒に第255期の札 `BurnHitSplitOnce`（印だけ・保持者がいれば両陣営に門）を足す。</summary>
    internal static Formation RuleD(Formation f)
    {
        var g = f.Clone();
        var (slot, d) = f.Occupied().First();
        if (d.Traits.Contains(TraitId.BurnHitSplitOnce)) return g;
        g[slot] = new UnitDef
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Traits = d.Traits.Append(TraitId.BurnHitSplitOnce).ToArray(),
            Pattern = d.Pattern, Advances = d.Advances, Actions = d.Actions, PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
        };
        return g;
    }

    static void CompareAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count, last = nw - 1;
        var b = Grid(rows, f => f);
        var cols = vers.Skip(1).Select(v => (Name: v.Name, Map: (Func<Formation, Formation>)(f => Apply(f, v)))).ToList();
        if (vers.Any(v => v.Ascii == "d")) cols.Add(("BN-d（規則として全行・行の最初の駒に札）", RuleD));
        if (vers.Any(v => v.Ascii == "fd")) cols.Add(("BN-f＋d（d は規則として全行）", f => RuleD(Apply(f, V("fd")))));
        bool HasBurn(Formation f) => f.Occupied().Any(o => o.Def.Id is "borg" or "hota" or "hiyo");
        Console.WriteLine($"# {Tag} §6-4 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定）");
        Console.WriteLine();
        Console.WriteLine($"規定の列は `docs/balance.md` と同じ帯。ボルグ ／ ホタ ／ ヒヨのいる行は {rows.Count(r => HasBurn(r.F))} 行。版は札の持ち主（ボルグ）がいる戦だけ門が立つので、「規則として全行」の列は BN-d の門をすべての行に立てた写し。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 上がった行 ／ セル | 落ちた行 ／ セル | うち燃焼の駒のいない行 | 主判定19行の第五波 | 第五波 95% 超の行（規定 → 版） |");
        Console.WriteLine("|---|---|---|---|--:|---|");
        var grids = cols.Select(c => (c.Name, g: Grid(rows, c.Map))).ToArray();
        var pr = Enumerable.Range(0, rows.Length).Where(ri => Baseline.PrimaryRows.Contains(rows[ri].Name)).ToList();
        int b95 = Enumerable.Range(0, rows.Length).Count(r => b[r, last] > 95);
        foreach (var (name, g) in grids)
        {
            int upR = 0, upC = 0, dnR = 0, dnC = 0, other = 0;
            for (int r = 0; r < rows.Length; r++)
            {
                bool u = false, d = false;
                for (int w = 1; w < nw; w++) { if (g[r, w] > b[r, w]) { upC++; u = true; } else if (g[r, w] < b[r, w]) { dnC++; d = true; } }
                if (u) upR++; if (d) dnR++;
                if ((u || d) && !HasBurn(rows[r].F)) other++;
            }
            Console.WriteLine($"| {name} | {upR} ／ {upC} | {dnR} ／ {dnC} | {other} | {F1(pr.Average(ri => b[ri, last]))} → {F1(pr.Average(ri => g[ri, last]))} | {b95} → {Enumerable.Range(0, rows.Length).Count(r => g[r, last] > 95)} |");
        }
        foreach (var (name, g) in grids)
        {
            Console.WriteLine();
            Console.WriteLine($"## {name}");
            Console.WriteLine();
            Console.WriteLine("| 行 | 燃焼の駒 | 第2波 | 第3波 | 第4波 | 第5波 |");
            Console.WriteLine("|---|---|---|---|---|---|");
            for (int r = 0; r < rows.Length; r++)
            {
                if (Enumerable.Range(1, nw - 1).All(w => g[r, w] == b[r, w])) continue;
                Console.WriteLine($"| {rows[r].Name} | {(HasBurn(rows[r].F) ? "○" : "—")} | " + string.Join(" | ", Enumerable.Range(1, nw - 1).Select(w => g[r, w] == b[r, w] ? F1(b[r, w]) : $"{F1(b[r, w])} → **{F1(g[r, w])}**")) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
