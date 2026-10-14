using BattleCore;
using static Common;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;
using ST331 = Stun331Diag;

// veil332 の Phase 0 と本測定（第332期・軽い期）。
static partial class Veil332Diag
{
    static string VeilCells(Z z) => $"{Per0(z.Added, z.N)} ／ {Per0(z.Soaked, z.N)} ／ {Per0(z.Capped, z.N)} ／ {Per0(z.Expired, z.N)} ／ {Per0(z.Left, z.N)}";
    static string Share(Z z) => z.ArmorLost == 0 ? "—" : $"衣 {Pct(z.Soaked, z.ArmorLost)}% ／ 板ほか {Pct(z.ArmorLost - z.Soaked, z.ArmorLost)}%";

    // ---------------------------------------------------------------------------------
    // §4 Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boss = WaveOf("boss");
        Console.WriteLine($"# {Tag} §4 Phase 0 —— いまの規定（段0 入り）・seed 0..{Seeds - 1}");
        Console.WriteLine();
        Console.WriteLine("## 1. ソムの衣に頼ってボスに勝っている台");
        Console.WriteLine();
        Console.WriteLine("ソムのいる `compare` の行 ＋ 試遊の台それぞれで、ボスの勝率を 規定のソム ／ 衣を外したソム（参考・器具の中だけで組んだ駒・`SparkVeil` の札だけを外す）で比べる。落ち ＝ 衣を外して落ちた勝率。"
            + "衣 ＝ 1戦に衣で得た破片 ／ 受け止めた（規定）。落ちの大きい順・勝率が 0 のまま ／ 100 のままの台も並べる。");
        Console.WriteLine();
        var rows = SomRows();
        var res = rows.Select(r => (r.Name, r.F, A: Measure(r.F, boss), B: Measure(S316.SwapId(r.F, "som", SomNoVeil), boss))).ToList();
        Console.WriteLine("| 台 | 並び | 規定 勝率 ／ 倒しT | 衣を外す 勝率 ／ 倒しT | 落ち | 衣 得た ／ 受け止めた |");
        Console.WriteLine("|---|---|---|---|--:|---|");
        foreach (var x in res.OrderByDescending(x => x.A.Win - x.B.Win).ThenBy(x => x.Name, StringComparer.Ordinal))
            Console.WriteLine($"| {x.Name} | {S307.Seats(x.F)} | {F1(x.A.Win)}% ／ {Per(x.A.D.Base.WinT, x.A.Wins)} | {F1(x.B.Win)}% ／ {Per(x.B.D.Base.WinT, x.B.Wins)} | {Sg(x.A.Win - x.B.Win)} | {Per0(x.A.Added, x.A.N)} ／ {Per0(x.A.Soaked, x.A.N)} |");
        Console.WriteLine();
        var top = res.Where(x => x.A.Win - x.B.Win >= 5).OrderByDescending(x => x.A.Win - x.B.Win).GroupBy(x => S307.Seats(x.F)).Select(g => g.First()).Take(3).Select(x => x.Name).ToList();   // 同じ並びの台は1つに
        Console.WriteLine($"衣を外すとボスの勝率が 5pt 以上落ちる台（上から3つまで）: {(top.Count == 0 ? "**なし**" : string.Join("・", top))}。");
        Console.WriteLine();

        Console.WriteLine($"## 2. ソム ＋ ツギの台（`{SomTsugiRow}`）: 味方の破片のうち衣と板の割合");
        Console.WriteLine();
        var tf = Playtest(SomTsugiRow);
        Console.WriteLine($"並び: {S307.Seats(tf)}。減った破片 ＝ 味方の破片が減った延べ（被弾など）。衣 ＝ そのうち衣の帳簿から減った分（衣から先に減らす）・板ほか ＝ 残り（ツギの板・ススの礫など）。貼った板 ＝ ツギが貼った破片の合計。1戦あたり。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 衣 得た | 貼った板 | 減った破片 | 衣 ／ 板ほか | 戦の終わりの衣の残り |");
        Console.WriteLine("|---|--:|--:|--:|---|--:|");
        foreach (var w in Waves)
        {
            var z = Measure(tf, w);
            Console.WriteLine($"| {w.Name} | {Per0(z.Added, z.N)} | {Per0(z.PlankPlaced, z.N)} | {Per0(z.ArmorLost, z.N)} | {Share(z)} | {Per0(z.Left, z.N)} |");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §6 代表台 × 波 × 版
    // ---------------------------------------------------------------------------------
    static void BoardsAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var boards = Boards();
        var res = new Dictionary<(string, string, string), Z>();
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in Waves) res[(bn, v.Name, w.Name)] = Measure(Apply(bf, v), w);
        Console.WriteLine($"# {Tag} §6 代表台 {boards.Length} × {Waves.Length} 波 × 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("規定 ＝ いまの規定（段0 入り・ソムは LV-a）。版はソムだけを差し替える。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();

        Console.WriteLine("## 1. ソムの破片（1戦あたり）: 衣で得た ／ 受け止めた ／ 捨てた（LV-c）／ 消えた（LV-d）／ 戦の終わりの残り");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            foreach (var v in vers)
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", Waves.Select(w => VeilCells(res[(bn, v.Name, w.Name)]))) + " |");
        Console.WriteLine();

        Console.WriteLine("## 2. ソム ＋ ツギの台: 減った破片のうち 衣 ／ 板ほか の割合");
        Console.WriteLine();
        Console.WriteLine("| 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|" + string.Concat(Waves.Select(_ => "---|")));
        string tb = boards[^1].Name;
        foreach (var v in vers)
            Console.WriteLine($"| {v.Name} | " + string.Join(" | ", Waves.Select(w => { var z = res[(tb, v.Name, w.Name)]; return $"{Share(z)}（減った {Per0(z.ArmorLost, z.N)}・板 {Per0(z.PlankPlaced, z.N)}）"; })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 3. 勝率（規定との差）・決着T（勝ち）・倒れた味方 ／戦");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Waves.Select(w => w.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Waves.Select(_ => "---|")));
        foreach (var (bn, _) in boards)
            foreach (var v in vers)
                Console.WriteLine($"| {bn} | {v.Name} | " + string.Join(" | ", Waves.Select(w =>
                {
                    var z = res[(bn, v.Name, w.Name)]; double b0 = res[(bn, vers[0].Name, w.Name)].Win;
                    string win = v == vers[0] || z.Win == b0 ? F1(z.Win) : $"{F1(z.Win)}（{Sg(z.Win - b0)}）";
                    return $"{win}・{Per(z.D.Base.WinT, z.Wins)}T・倒 {Per2(z.D.Base.AllyDeaths, z.N)}";
                })) + " |");
        Console.WriteLine();

        Console.WriteLine("## 4. 段0: ポンの台 × 近衛 ／ 大隊の、痺れで失った敵の手番（旧 ＝ 第330〜331期の規定 → 規定）");
        Console.WriteLine();
        foreach (var w in new[] { "guard", "bat" }.Select(WaveOf))
        {
            var o = ST331.Measure(Pin331(PonBoard()), w, Seeds); var n = ST331.Measure(PonBoard(), w, Seeds);
            Console.WriteLine($"- {w.Name}: {Pct(o.FoeShockLost, o.FoeTurns)}% → **{Pct(n.FoeShockLost, n.FoeTurns)}%**（敵が動いた手番 ／戦 {Per(o.FoeActed, o.N)} → {Per(n.FoeActed, n.N)}・勝率 {F1(o.Win)} → {F1(n.Win)}・決着T {Per(o.D.Base.WinT, o.Wins)} → {Per(n.D.Base.WinT, n.Wins)}）");
        }
        Console.WriteLine();

        Console.WriteLine("## 5. 壊れの兆し（勝率が規定から 15pt 以上落ちる台・波）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var bad = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var w in Waves)
                {
                    double d = res[(bn, v.Name, w.Name)].Win - res[(bn, vers[0].Name, w.Name)].Win;
                    if (d <= -15) bad.Add($"{bn} × {w.Name} {Sg(d)}");
                }
            Console.WriteLine($"- **{v.Name}**: {(bad.Count == 0 ? "なし" : string.Join("・", bad))}");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    // ---------------------------------------------------------------------------------
    // §6 `compare` × 版
    // ---------------------------------------------------------------------------------
    static void CompareAll(Ver[] vers)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rows = CompareBuilds().ToArray();
        int nw = EnemyCatalog.Stages.Count;
        double[,] Grid(Ver v)
        {
            var g = new double[rows.Length, nw];
            Parallel.For(0, rows.Length * nw, k =>
            {
                int ri = k / nw, wi = k % nw;
                var f = Apply(rows[ri].F, v);
                int wins = 0;
                for (int s = 0; s < CompareSeeds; s++) if (BattleEngine.Run(f, EnemyCatalog.Stages[wi].Enemy, s, verbose: false).PlayerWon) wins++;
                g[ri, wi] = 100.0 * wins / CompareSeeds;
            });
            return g;
        }
        var b = Grid(vers[0]);
        Console.WriteLine($"# {Tag} §6 `compare` {rows.Length} 行 × 版（seed 0..{CompareSeeds - 1}・基準は規定 ＝ 段0 入り）");
        Console.WriteLine();
        Console.WriteLine($"ソムのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "som")).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, Grid(vers[vi]), "som");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
