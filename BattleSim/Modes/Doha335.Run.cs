using BattleCore;
using static Common;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;

// doha335 の Phase 0 と本測定（第335期・軽い期）。
static partial class Doha335Diag
{
    static readonly string[] Five = { "ゴルム", "ザン", "ドハ", "ミサ", "ヒサ" };
    static string WinCell(St z) => $"{F1(z.Win)}%・{Per(z.WinT, z.Wins)}T";
    static string DmgShare(St z, string who) => Pct(z.DmgBy.GetValueOrDefault(who), z.DmgAll);
    static string RegurgCells(St z) => z.RegurgTo.Count == 0 ? "—" : string.Join("・", z.RegurgTo.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal)
        .Select(x => $"{x.Key}{(z.NonStrikers.Contains(x.Key) ? "（殴らない）" : "")} {Per(x.Value, z.N)}"));

    // ---------------------------------------------------------------------------------
    // §3 Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var g = Boards()[0].F;
        var res = Waves.Concat(StrongWaves).Select(w => (W: w, Z: Measure(g, w))).ToArray();
        Console.WriteLine($"# {Tag} §3 Phase 0 —— 段0 を入れた規定・`{GuardRow}`（{S307.Seats(g)}）・seed 0..{Seeds - 1}・1戦あたり");
        Console.WriteLine();
        Console.WriteLine("強 ＝ 強くした条件（第315期と同じ HP 500% ／ 攻 300% を各波の倍率に重ねる）。");
        Console.WriteLine();

        Console.WriteLine("## 1. ドハが倒れた割合・そのターン・倒れた時点の肩代わりの累計");
        Console.WriteLine();
        Console.WriteLine("肩代わりの累計 ＝ 分かちで味方の痛みを引き受けた名目の量（ゴルムがさらに9割飲む前・徴収 ／ 刻みの分かちも含む）。倒れた時点 ＝ ドハが倒れた戦の平均（倒れた後は肩代わりしないので、その戦の累計）。DM-c ならこの5割が溢れる。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率・決着T | ドハが倒れた | 倒れたT | 倒れた時点の累計（DM-c なら溢れる量） | 累計（全戦） | ドハの手番 |");
        Console.WriteLine("|---|---|--:|--:|---|--:|--:|");
        foreach (var (w, z) in res)
            Console.WriteLine($"| {WN(w)} | {WinCell(z)} | {Pct(z.DohaDeaths, z.N)}% | {Per(z.DohaDeathT, z.DohaDeaths)} | {Per0(z.LoadAtDeath, z.DohaDeaths)}（{Per0(z.LoadAtDeath / 2, z.DohaDeaths)}） | {Per0(z.Load, z.N)} | {Per(z.DohaTurns, z.N)} |");
        Console.WriteLine();

        Console.WriteLine("## 2. 敵の一撃がドハに直に当たった回数（DM-a がどれだけ起きうるか）");
        Console.WriteLine();
        Console.WriteLine("直の一撃 ＝ 敵が出どころで、刻み ／ 徴収 ／ 中継 ／ 共有でない一撃（主目標と巻き込みを含む）。単体の主目標 ＝ そのうち単体攻撃の主目標。ゴルムが引き受けた ＝ そのうち巨躯が9割飲んだ（DM-a では残りの1割だけが等分される）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 直の一撃 | うち単体の主目標 | うちゴルムが引き受けた |");
        Console.WriteLine("|---|--:|--:|--:|");
        foreach (var (w, z) in res)
            Console.WriteLine($"| {WN(w)} | {Per(z.Direct, z.N)} | {Per(z.DirectSingle, z.N)} | {Per(z.Walled, z.N)}（{Pct(z.Walled, z.Direct)}%） |");
        Console.WriteLine();

        Console.WriteLine("## 3. ゴルムが返した力の行き先（吐き戻しで受け取った攻撃力・1戦あたり・殴らない ＝ 手番で殴らない駒 `StrikesOnTurn`）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 行き先 | 殴らない駒へ回った割合 |");
        Console.WriteLine("|---|---|--:|");
        foreach (var (w, z) in res)
        {
            long all = z.RegurgTo.Values.Sum(), non = z.RegurgTo.Where(x => z.NonStrikers.Contains(x.Key)).Sum(x => x.Value);
            Console.WriteLine($"| {WN(w)} | {RegurgCells(z)} | {Pct(non, all)}% |");
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
        var res = new Dictionary<(string, string, string), St>();
        IEnumerable<S287.Wave> WavesOf(string bn) => bn == boards[0].Name ? Waves.Concat(StrongWaves) : Waves;
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in WavesOf(bn)) res[(bn, v.Name, WN(w))] = Measure(Apply(bf, v), w);
        Console.WriteLine($"# {Tag} §6 代表台 {boards.Length} × 5 波（守り型は ＋ 強くした近衛 ／ 大隊）× 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("規定 ＝ いまの規定（段0 入り・ザンは ZC-a）。版はドハ ／ ゴルムだけを差し替える（台にいなければ差し替えない＝規定と同じ）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();
        string g0 = boards[0].Name;
        var gw = WavesOf(g0).Select(WN).ToArray();

        Console.WriteLine("## 1. 守り型: 駒ごとの与ダメの割合と決着T");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 勝率・決着T | 倒れた味方 ／戦 | 味方の与ダメ 計 | " + string.Join(" | ", Five) + " |");
        Console.WriteLine("|---|---|---|--:|--:|" + string.Concat(Five.Select(_ => "--:|")));
        foreach (var wn in gw)
            foreach (var v in vers)
            {
                var z = res[(g0, v.Name, wn)];
                Console.WriteLine($"| {wn} | {v.Name} | {WinCell(z)} | {(z.N == 0 ? "—" : ((double)z.AllyDeaths / z.N).ToString("F2"))} | {Per0(z.DmgAll, z.N)} | " + string.Join(" | ", Five.Select(n => $"{DmgShare(z, n)}%")) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 2. DM-a: 等分（1戦あたり 回 ／ 量 ／ 欠片で倒れた味方）");
        Console.WriteLine();
        WriteDm(boards, vers, res, WavesOf, "a", z => $"{Per(z.Splits, z.N)} 回 ／ {Per0(z.SplitAmt, z.N)} ／ 倒 {Per(z.SplitKills, z.N)}");

        Console.WriteLine("## 3. DM-c ／ DM-c ＋ ゴルム: 溢れ（起きた割合・量・溢れで倒れた味方 ／戦）・ゴルムが飲んだ量とその後のゴルムの与ダメ");
        Console.WriteLine();
        Console.WriteLine("DM-c: 溢れた戦の割合 ／ 溢れた量（溢れた戦あたり）／ 溢れで倒れた味方 ／戦。DM-c ＋ ゴルム: 飲んだ戦の割合 ／ 飲んだ量 ／ 上がった攻撃力 ／ 飲んだT（飲んだ戦あたり）／ 飲んだ後のゴルムの与ダメ（飲んだ戦あたり）・残りはゴルムが先に倒れていて溢れた分。");
        Console.WriteLine();
        WriteDm(boards, vers, res, WavesOf, "c", z => $"{Pct(z.Overflows, z.N)}% ／ {Per0(z.OverflowAmt, z.Overflows)} ／ 倒 {Per(z.OverflowKills, z.N)}");
        WriteDm(boards, vers, res, WavesOf, "cg", z => $"飲 {Pct(z.Drinks, z.N)}% ／ {Per0(z.Drank, z.Drinks)} ／ +{Per(z.DrankGain, z.Drinks)} ／ T{Per(z.DrinkT, z.Drinks)} ／ 後 {Per0(z.GolmAfterDrink, z.Drinks)}・溢 {Pct(z.Overflows, z.N)}%（倒 {Per(z.OverflowKills, z.N)}）");

        Console.WriteLine("## 4. GL: ゴルムの力の返り先（1戦あたりの攻撃力）とその駒の与ダメ（規定 → GL）");
        Console.WriteLine();
        if (vers.Any(v => v.Ascii == "gl"))
        {
            var vg = vers.First(v => v.Ascii == "gl");
            Console.WriteLine("| 台 | 波 | 返り先 規定 | 返り先 GL（殴る味方へ回した 回 ／ 攻撃力） | 与ダメ 規定 → GL（返り先の駒） |");
            Console.WriteLine("|---|---|---|---|---|");
            foreach (var (bn, bf) in boards.Where(b => b.F.Occupied().Any(o => o.Def.Id == "golm")))
                foreach (var wn in WavesOf(bn).Select(WN))
                {
                    var b = res[(bn, vers[0].Name, wn)]; var z = res[(bn, vg.Name, wn)];
                    var who = z.RegurgTo.Keys.Union(b.RegurgTo.Keys).OrderBy(k => k, StringComparer.Ordinal).ToArray();
                    string dm = string.Join("・", who.Select(k => $"{k} {Per0(b.DmgBy.GetValueOrDefault(k), b.N)} → {Per0(z.DmgBy.GetValueOrDefault(k), z.N)}"));
                    Console.WriteLine($"| {bn} | {wn} | {RegurgCells(b)} | {RegurgCells(z)}（{Per(z.GlRedirects, z.N)} ／ {Per(z.GlAmt, z.N)}） | {dm} |");
                }
            Console.WriteLine();
        }

        Console.WriteLine("## 5. 代表台の勝率（規定との差）・決着T（ボスは倒しT）・倒れた味方 ／戦");
        Console.WriteLine();
        foreach (var (bn, _) in boards)
        {
            var wl = WavesOf(bn).Select(WN).ToArray();
            Console.WriteLine($"### {bn}");
            Console.WriteLine();
            Console.WriteLine("| 版 | " + string.Join(" | ", wl) + " |");
            Console.WriteLine("|---|" + string.Concat(wl.Select(_ => "---|")));
            foreach (var v in vers)
                Console.WriteLine($"| {v.Name} | " + string.Join(" | ", wl.Select(wn =>
                {
                    var z = res[(bn, v.Name, wn)]; double b0 = res[(bn, vers[0].Name, wn)].Win;
                    string win = v == vers[0] || z.Win == b0 ? F1(z.Win) : $"{F1(z.Win)}（{Sg(z.Win - b0)}）";
                    return $"{win}・{Per(z.WinT, z.Wins)}T・倒 {(z.N == 0 ? "—" : ((double)z.AllyDeaths / z.N).ToString("F2"))}";
                })) + " |");
            Console.WriteLine();
        }

        Console.WriteLine("## 6. 壊れの兆し（勝率が規定から 15pt 以上落ちる台・波）");
        Console.WriteLine();
        foreach (var v in vers.Skip(1))
        {
            var bad = new List<string>();
            foreach (var (bn, _) in boards)
                foreach (var wn in WavesOf(bn).Select(WN))
                {
                    double d = res[(bn, v.Name, wn)].Win - res[(bn, vers[0].Name, wn)].Win;
                    if (d <= -15) bad.Add($"{bn} × {wn} {Sg(d)}");
                }
            Console.WriteLine($"- **{v.Name}**: {(bad.Count == 0 ? "なし" : string.Join("・", bad))}");
        }
        Console.WriteLine();
        Console.WriteLine($"（{sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    static void WriteDm((string Name, Formation F)[] boards, Ver[] vers, Dictionary<(string, string, string), St> res, Func<string, IEnumerable<S287.Wave>> wavesOf, string ascii, Func<St, string> cell)
    {
        var v = vers.FirstOrDefault(x => x.Ascii == ascii);
        if (v is null) return;
        var dohaBoards = boards.Where(b => b.F.Occupied().Any(o => o.Def.Id == "doha")).ToArray();
        var cols = wavesOf(boards[0].Name).Select(WN).ToArray();
        Console.WriteLine($"**{v.Name}**（ドハのいる台だけ）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", cols) + " |");
        Console.WriteLine("|---|" + string.Concat(cols.Select(_ => "---|")));
        foreach (var (bn, _) in dohaBoards)
            Console.WriteLine($"| {bn} | " + string.Join(" | ", cols.Select(wn => res.TryGetValue((bn, v.Name, wn), out var z) ? cell(z) : "—")) + " |");
        Console.WriteLine();
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
        Console.WriteLine($"ドハのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "doha")).Select(r => r.Name))}");
        Console.WriteLine();
        Console.WriteLine($"ゴルムのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "golm")).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, Grid(vers[vi]), vers[vi].Holder);
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
