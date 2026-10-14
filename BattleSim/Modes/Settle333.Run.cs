using BattleCore;
using static Common;
using S307 = Som307Diag;

// settle333 の本測定（第333期・軽い期・規定の1列だけ）。
static partial class Settle333Diag
{
    static string FirstDeath(Z z)
    {
        if (z.FirstN == 0) return "倒れない";
        var (k, (n, t)) = z.First.OrderByDescending(x => x.Value.N).ThenBy(x => x.Key, StringComparer.Ordinal).First();
        return $"{k} T{Per(t, n)}（{Pct(n, z.V.N)}% の戦）";
    }

    static void BoardsAll()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var cols = new[] { ("旧", true), ("新", false) };
        var res = new Dictionary<(string, string, string), Z>();
        foreach (var (bn, bf) in boards) foreach (var (cn, old) in cols) foreach (var w in Waves) res[(bn, cn, w.Name)] = Measure(old ? Pin332(bf) : bf, w);
        Console.WriteLine($"# {Tag} §4 代表台 {boards.Length} × {Waves.Length} 波 × 旧 ／ 新・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("旧 ＝ 第320〜332期の規定のソム（LV-a・`SomH332`）。新 ＝ いまの規定（LV-c）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine("## 1. ボス: 勝率 ／ 倒しT（旧 → 新）と、最初に倒れた味方（いちばん多い駒・倒れたターンの平均・その駒が最初に倒れた戦の割合）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 勝率 | 倒しT | 最初に倒れた味方 旧 | 最初に倒れた味方 新 | 倒れた味方 ／戦 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var (bn, _) in boards)
        {
            var a = res[(bn, "旧", "ボス")]; var b = res[(bn, "新", "ボス")];
            Console.WriteLine($"| {bn} | {F1(a.V.Win)} → {F1(b.V.Win)} | {Per(a.V.D.Base.WinT, a.V.Wins)} → {Per(b.V.D.Base.WinT, b.V.Wins)} | {FirstDeath(a)} | {FirstDeath(b)} | {Per(a.V.D.Base.AllyDeaths, a.V.N)} → {Per(b.V.D.Base.AllyDeaths, b.V.N)} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 2. 精鋭 ／ 本編の勝率（旧 → 新・動いたセルだけ）");
        Console.WriteLine();
        var moved = new List<string>();
        foreach (var (bn, _) in boards)
            foreach (var w in Waves.Where(w => !w.Boss))
            {
                var a = res[(bn, "旧", w.Name)]; var b = res[(bn, "新", w.Name)];
                if (a.V.Win != b.V.Win) moved.Add($"{bn} × {w.Name} {F1(a.V.Win)} → {F1(b.V.Win)}（{Sg(b.V.Win - a.V.Win)}・決着T {Per(a.V.D.Base.WinT, a.V.Wins)} → {Per(b.V.D.Base.WinT, b.V.Wins)}）");
            }
        Console.WriteLine(moved.Count == 0 ? "- 動いたセルなし" : string.Join("\n", moved.Select(m => "- " + m)));
        Console.WriteLine();

        Console.WriteLine("## 3. ソムの破片（1戦あたり）: 得た ／ 受け止めた ／ 捨てた ／ 戦の終わりの残り");
        Console.WriteLine();
        Console.WriteLine("| 台 | 列 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (string bn in new[] { "ポンの台", "瓦礫（ソム ＋ ツギ）" })
            foreach (var (cn, _) in cols)
                Console.WriteLine($"| {bn} | {cn} | " + string.Join(" | ", Waves.Select(w => { var z = res[(bn, cn, w.Name)].V; return $"{Per0(z.Added, z.N)} ／ {Per0(z.Soaked, z.N)} ／ {Per0(z.Capped, z.N)} ／ {Per0(z.Left, z.N)}"; })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 4. 瓦礫の台: 減った破片のうち 衣 ／ 板ほか");
        Console.WriteLine();
        Console.WriteLine("| 列 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (cn, _) in cols)
            Console.WriteLine($"| {cn} | " + string.Join(" | ", Waves.Select(w => { var z = res[("瓦礫（ソム ＋ ツギ）", cn, w.Name)].V; return z.ArmorLost == 0 ? "—" : $"衣 {Pct(z.Soaked, z.ArmorLost)}% ／ 板ほか {Pct(z.ArmorLost - z.Soaked, z.ArmorLost)}%"; })) + " |");
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }
}
