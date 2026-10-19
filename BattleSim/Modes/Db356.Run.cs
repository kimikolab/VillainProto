using BattleCore;
using static Common;
using I = Inv348Diag;
using D = Db351Diag;
using C5 = Db355Diag;

// db356 §3 ／ §5 —— 席の総当たり ／ 代表台 ／ 軸の横並び。
static partial class Db356Diag
{
    internal sealed record Res(Seat S, Cell Boss, Cell Guard, Cell Bat)
    {
        public long Elite => Guard.Wins + Bat.Wins;
        public double KillT => Boss.Wins == 0 ? double.MaxValue : (double)Boss.WinT / Boss.Wins;
        public long Fell => Boss.Fell + Guard.Fell + Bat.Fell;
    }
    static Res[] Rank(IEnumerable<Res> xs) => xs
        .OrderByDescending(r => r.Boss.Wins).ThenByDescending(r => r.Elite).ThenBy(r => r.KillT).ThenBy(r => r.Fell)
        .ThenBy(r => r.S.Key, StringComparer.Ordinal).ToArray();
    static bool HasId(Formation f, string id) => f.Occupied().Any(o => o.Def.Id == id);
    static string WT(Cell c) => $"{F1(c.Win)}・{c.WinTs}T";

    static void SeatsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var seats = AllSeats();
        var res = new Res[seats.Length];
        for (int k = 0; k < seats.Length; k++)
            res[k] = new Res(seats[k], CellOf(seats[k].F, I.Boss, BossSeeds), CellOf(seats[k].F, I.Guard, EliteSeeds), CellOf(seats[k].F, I.Bat, EliteSeeds));
        var ranked = Rank(res);
        var pairs = res.Select(r => r.S.Pair).Distinct().ToArray();
        Console.WriteLine($"# {Tag} §3 席の総当たり —— ネル ／ ワタ ／ ウツ ＋ {string.Join("・", Pool.Select(Short))} から2枚（{pairs.Length} 組）× 席の全並び 120 ＝ {seats.Length} 並び");
        Console.WriteLine();
        Console.WriteLine($"波: ボス（seed 0..{BossSeeds - 1}）／ 近衛 ／ 大隊（seed 0..{EliteSeeds - 1}）。段0 入りの規定（ネル ＝ N-100）。順位は「ボスの勝ち → 近衛 ＋ 大隊の勝ち → ボスの倒しT → 3波の倒れた数」。");
        Console.WriteLine();

        // 1. 組ごとのボスに勝てる並び ＋ 上位10並び
        Console.WriteLine("## 1. ボスに勝てる並び（組ごと）と上位10並び");
        Console.WriteLine();
        Console.WriteLine("| 組 | ボスに勝てる並び（／120） | 割合 | 近衛 ＋ 大隊 90% 以上の並び | 両方を満たす並び | 組の最良（順位の並び） |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        foreach (var p in pairs.OrderByDescending(p => res.Count(r => r.S.Pair == p && r.Boss.Wins > 0)))
        {
            var xs = res.Where(r => r.S.Pair == p).ToArray();
            int boss = xs.Count(r => r.Boss.Wins > 0), elite = xs.Count(r => r.Guard.Win >= 90 && r.Bat.Win >= 90), both = xs.Count(r => r.Boss.Wins > 0 && r.Guard.Win >= 90 && r.Bat.Win >= 90);
            var best = Rank(xs)[0];
            Console.WriteLine($"| {p} | {boss} | {Pct(boss, xs.Length)}% | {elite} | {both} | {best.S.Key}（ボス {WT(best.Boss)} ／ 近衛 {F1(best.Guard.Win)} ／ 大隊 {F1(best.Bat.Win)}） |");
        }
        Console.WriteLine();
        Console.WriteLine($"ボスに勝てる並び: **{res.Count(r => r.Boss.Wins > 0)} ／ {res.Length}**。");
        Console.WriteLine();
        Console.WriteLine("上位10並び（勇者の最大HP ＝ ボスの戦 seed 0..4 の verbose・T6 ／ T9 ／ T12 のターン末と、負けた戦の最後）:");
        Console.WriteLine();
        Console.WriteLine("| # | 並び（前1・前3・中央・後1・後3） | 組 | ボス 勝率・倒しT | 全滅 T（負けた戦） | 近衛 | 大隊 | 倒れた（3波 ／戦） | 勇者の最大HP T6 ／ T9 ／ T12 ／ 最後 |");
        Console.WriteLine("|--:|---|---|---|--:|---|---|--:|---|");
        int k2 = 0;
        foreach (var r in ranked.Take(10))
        {
            var fx = C5.Measure(r.S.F, I.Boss, BossSeeds, TrajT);
            string M(int t) => fx.TCnt[t] == 0 ? "—" : Per(fx.TMax[t], fx.TCnt[t]);
            Console.WriteLine($"| {++k2} | {r.S.Key} | {r.S.Pair} | {WT(r.Boss)} | {Per(r.Boss.WipeT, r.Boss.Lost)} | {WT(r.Guard)} | {WT(r.Bat)} | {Per2(r.Fell, r.Boss.N + r.Guard.N + r.Bat.N)} | {M(6)} ／ {M(9)} ／ {M(12)} ／ {Per(fx.LostMaxEnd, fx.Lost)} |");
        }
        Console.WriteLine();

        // 2. ムドを残す並びの最良
        Console.WriteLine("## 2. ムドを残す並び（ムド ＋ 壁1枚の組）の最良5");
        Console.WriteLine();
        Console.WriteLine("| # | 並び | 組 | ボス | 全滅 T | 近衛 | 大隊 |");
        Console.WriteLine("|--:|---|---|---|--:|---|---|");
        k2 = 0;
        foreach (var r in ranked.Where(r => HasId(r.S.F, "mudo")).Take(5))
            Console.WriteLine($"| {++k2} | {r.S.Key} | {r.S.Pair} | {WT(r.Boss)} | {Per(r.Boss.WipeT, r.Boss.Lost)} | {WT(r.Guard)} | {WT(r.Bat)} |");
        Console.WriteLine();

        // 3. 精鋭も 90% 以上の並びのうちボスの勝率が最も高いもの
        Console.WriteLine("## 3. 精鋭（近衛 ／ 大隊）がどちらも 90% 以上の並びのうち、ボスの勝率が高い5");
        Console.WriteLine();
        var elite90 = ranked.Where(r => r.Guard.Win >= 90 && r.Bat.Win >= 90).ToArray();
        Console.WriteLine($"精鋭がどちらも 90% 以上の並び: {elite90.Length} ／ {res.Length}（うちボスに勝てる {elite90.Count(r => r.Boss.Wins > 0)}）。");
        Console.WriteLine();
        Console.WriteLine("| # | 並び | 組 | ボス | 全滅 T | 近衛 | 大隊 |");
        Console.WriteLine("|--:|---|---|---|--:|---|---|");
        k2 = 0;
        foreach (var r in elite90.Take(5))
            Console.WriteLine($"| {++k2} | {r.S.Key} | {r.S.Pair} | {WT(r.Boss)} | {Per(r.Boss.WipeT, r.Boss.Lost)} | {WT(r.Guard)} | {WT(r.Bat)} |");
        Console.WriteLine();

        // 4. 壁ごと（その駒を含む組すべて）のボスに勝てる並びの割合
        Console.WriteLine("## 4. どの駒が効くか（その駒を含む 5 組 × 120 ＝ 600 並びのうち）");
        Console.WriteLine();
        Console.WriteLine("| 駒 | ボスに勝てる並び | 割合 | ボスの勝率の平均 | ボスの全滅 T の平均（負けた戦） | 近衛 ／ 大隊の勝率の平均 |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        foreach (var d in Pool)
        {
            var xs = res.Where(r => HasId(r.S.F, d.Id)).ToArray();
            int boss = xs.Count(r => r.Boss.Wins > 0);
            long lost = xs.Sum(r => r.Boss.Lost), wipe = xs.Sum(r => r.Boss.WipeT);
            Console.WriteLine($"| {Short(d)} | {boss} | {Pct(boss, xs.Length)}% | {F1(xs.Average(r => r.Boss.Win))} | {Per(wipe, lost)} | {F1(xs.Average(r => r.Guard.Win))} ／ {F1(xs.Average(r => r.Bat.Win))} |");
        }
        Console.WriteLine();
        Console.WriteLine("席ごと（その駒がその席にいる並びのボスの勝率の平均・全組）:");
        Console.WriteLine();
        string[] slot = { "前1", "前3", "中央", "後1", "後3" };
        Console.WriteLine("| 駒 | " + string.Join(" | ", slot) + " |");
        Console.WriteLine("|---|" + string.Concat(slot.Select(_ => "--:|")));
        foreach (var d in Fixed.Concat(Pool))
            Console.WriteLine($"| {Short(d)} | " + string.Join(" | ", Enumerable.Range(0, 5).Select(i =>
            {
                var xs = res.Where(r => r.S.F[i]?.Id == d.Id).ToArray();
                return xs.Length == 0 ? "—" : F1(xs.Average(r => r.Boss.Win));
            })) + " |");
        Console.WriteLine();

        // 5. 負ける並びの死因
        Console.WriteLine("## 5. ボスに負ける並びの死因（全滅 T と最初に倒れる駒・負けた戦の延べ）");
        Console.WriteLine();
        var lostAll = new Cell();
        foreach (var r in res) lostAll.Add(r.Boss);
        Console.WriteLine($"ボスの戦 {lostAll.N} 戦のうち負け {lostAll.Lost}・全滅 T の平均 {Per(lostAll.WipeT, lostAll.Lost)}・最初に倒れる駒: {string.Join("、", lostAll.First.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {Pct(kv.Value, lostAll.N)}%"))}。");
        Console.WriteLine();
        Console.WriteLine("| 組 | 負けた戦の全滅 T | 最初に倒れる駒（ボスの戦） |");
        Console.WriteLine("|---|--:|---|");
        foreach (var p in pairs)
        {
            var c = new Cell();
            foreach (var r in res.Where(r => r.S.Pair == p)) c.Add(r.Boss);
            Console.WriteLine($"| {p} | {Per(c.WipeT, c.Lost)} | {string.Join("、", c.First.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key} {Pct(kv.Value, c.N)}%"))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // §5 代表台
    // ---------------------------------------------------------------------------------
    /// <summary>§5 の代表台（`seats` の上位から・決めたこと）。</summary>
    internal static readonly (string Label, string Key)[] Reps = RepKeys;
    internal static I.Board[] Boards() => new[] { new I.Board("呪い", D.PlayRow, Play) }
        .Concat(Reps.Select(r => new I.Board("呪い", $"{r.Label}（{r.Key}）", ByKey(r.Key)))).ToArray();
    internal static readonly I.W[] Waves = new[] { I.Boss, I.Guard, I.Bat, I.M2, I.M4, I.M5, I.Nine }.Concat(I.StrongWaves).ToArray();

    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var res = boards.SelectMany(b => Waves.Select(w => (b, w))).ToDictionary(x => (x.b.Name, x.w.Name), x => C5.Measure(x.b.F, x.w, Seeds, TrajT));
        Console.WriteLine($"# {Tag} §5 代表台 {boards.Length} × {Waves.Length} 波・seed 0..{Seeds - 1}（段0 入りの規定）");
        Console.WriteLine();
        foreach (var b in boards) Console.WriteLine($"- {b.Name}: {Seats(b.F)}");
        Console.WriteLine();
        Console.WriteLine("## 1. 勝率・決着T（ボスは倒しT）・倒れた ／戦");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var b in boards)
            Console.WriteLine($"| {b.Name} | " + string.Join(" | ", Waves.Select(w => { var a = res[(b.Name, w.Name)]; return $"{F1(a.Win)}・{a.WinTs}T・倒 {Per2(a.Fell, a.N)}"; })) + " |");
        Console.WriteLine();
        Console.WriteLine("## 2. ボスの戦（勇者の最大HPの推移・全滅 T・削った最大HP）");
        Console.WriteLine();
        foreach (var b in boards) C5.PrintTraj($"{b.Name} × ボス", res[(b.Name, I.Boss.Name)]);
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    // ---------------------------------------------------------------------------------
    // 軸の横並び（第348期 §3）—— デバフの行に代表台を足す
    // ---------------------------------------------------------------------------------
    static void AxesAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var others = I.Boards().Where(b => b.Axis != "デバフ").ToList();
        others.Add(new I.Board("燃焼", "試遊・燃焼 ポンの台（いまの規定）", Presets.Versions.First(r => r.Name == "試遊・燃焼 ポンの台").F));
        var mine = Boards();
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
        Console.WriteLine($"# {Tag} §5 軸の横並び（第348期 §3 の物差し）—— デバフの行に代表台を足す・seed 0..{I.Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("ほかの軸の行は `inv348` の台（第349期までの規定の燃焼の3枚・古い口の終端でウツ ／ ムド ／ ワタ ／ ネルも旧）＋「試遊・燃焼 ポンの台」（いまの規定）。デバフの行はいまの規定（段0 入り）。");
        Console.WriteLine();
        Console.WriteLine("| 軸 ／ 台 | ボス 勝率・T | 近衛 | 大隊 | 本編2 | 本編4 | 本編5 | 九体 | 的・一 8T | 的・九 8T | 強 2〜5 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|---|--:|--:|--:|");
        foreach (var b in others) Console.WriteLine(Line($"{b.Axis}: {b.Name}", oth[b.Name]));
        var deb = new List<(string Label, double[] V)>();
        foreach (var b in mine)
        {
            var r = M(b.F);
            Console.WriteLine(Line($"**デバフ: {b.Name}**", r));
            deb.Add((b.Name, Vals(r)));
        }
        Console.WriteLine();
        string[] cols = { "ボス 勝率", "ボス 倒しT", "近衛", "大隊", "本編2", "本編4", "本編5", "九体", "的・一", "的・九", "強 2〜5" };
        Console.WriteLine($"## 物差しごとの順位（ほかの軸 {axes.Count} 本の1枚 ＋ その行。同じ値は上の順位）");
        Console.WriteLine();
        Console.WriteLine("| デバフの行 | " + string.Join(" | ", cols) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "--:|")));
        foreach (var (label, v) in deb)
            Console.WriteLine($"| {label} | " + string.Join(" | ", Enumerable.Range(0, cols.Length).Select(i => $"{1 + axes.Values.Count(a => a[i] > v[i])}")) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
