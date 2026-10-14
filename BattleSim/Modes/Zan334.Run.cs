using BattleCore;
using static Common;
using S287 = Shock287Diag;
using S307 = Som307Diag;
using S316 = Shiga316Diag;

// zan334 の Phase 0 と本測定（第334期・軽い期）。
static partial class Zan334Diag
{
    static string Share(Dictionary<string, long> d, int k = 5)
    {
        long t = d.Values.Sum();
        return t == 0 ? "—" : string.Join("・", d.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Take(k).Select(x => $"{x.Key} {100.0 * x.Value / t:F0}%"));
    }
    static string PerTo(Dictionary<string, long> d, long n) => d.Count == 0 ? "—" : string.Join("・", d.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key} {Per(x.Value, n)}"));
    static string DmgShare(St z, string who) => Pct(z.DmgBy.GetValueOrDefault(who), z.DmgAll);
    static readonly string[] Five = { "ゴルム", "ザン", "ドハ", "ミサ", "ヒサ" };
    static string WinCell(St z) => $"{F1(z.Win)}%・{Per(z.WinT, z.Wins)}T";

    /// <summary>ザンの割り込みの内訳（1戦あたり 回 ／ 与ダメ）。</summary>
    static string KindCell(St z, int k) => $"{Per(z.Kind[k], z.N)} ／ {Per0(z.KindDealt[k], z.N)}";

    // ---------------------------------------------------------------------------------
    // §3 Phase 0
    // ---------------------------------------------------------------------------------
    static void P0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var g = Boards()[0].F;
        var waves = Waves.Concat(StrongWaves).ToArray();
        var res = waves.Select(w => (W: w, Z: Measure(g, w))).ToArray();
        string WN(S287.Wave w) => StrongWaves.Contains(w) ? $"強 {w.Name}" : w.Name;
        Console.WriteLine($"# {Tag} §3 Phase 0 —— 段0 を入れた規定・`{GuardRow}`（{S307.Seats(g)}）・seed 0..{Seeds - 1}・1戦あたり");
        Console.WriteLine();
        Console.WriteLine("強 ＝ 強くした条件（第315期と同じ HP 500% ／ 攻 300% を各波の倍率に重ねる・参考）。");
        Console.WriteLine();

        Console.WriteLine("## 1. ザンの攻撃の内訳（回 ／ 与ダメ）");
        Console.WriteLine();
        Console.WriteLine("割り込み（仇討ち）を、いま解決中の標の味方への一撃の札で分けた: 直 ＝ 敵が標の味方を直に殴った（肩代わりの残りを含む）／ ドハ経由 ＝ ドハ（分かち）が肩代わりした中継の一撃 ／ "
            + "ゴルム経由 ＝ ゴルム（巨躯）が肩代わりした中継の一撃（標の付いたゴルムに入った分）／ 濡れ衣 ＝ 味方の攻撃から（ヒサの「あいつがやった！」）。"
            + "手番 ＝ 仇巡りの手番（標の敵を巡って斬る・太刀の数）と普通の手番。手番の与ダメは手番の間に敵陣の HP が減った量（手番の中の連鎖を含む）。1ターンの最多 ＝ 1ラウンドの割り込みの最多（戦ごとの平均 ／ 全戦の最大）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 勝率・決着T | 割り込み 計 | " + string.Join(" | ", KindNames) + " | 1ターンの最多 | 仇巡り 手番 ／ 太刀 ／ 与ダメ | 普通の手番 ／ 与ダメ | ザンの与ダメ 計 | うち割り込み |");
        Console.WriteLine("|---|---|--:|" + string.Concat(KindNames.Select(_ => "--:|")) + "--:|---|---|--:|--:|");
        foreach (var (w, z) in res)
            Console.WriteLine($"| {WN(w)} | {WinCell(z)} | {Per(z.ZanAvenges, z.N)} ／ {Per0(z.ZanAvengeDealt, z.N)} | " + string.Join(" | ", Enumerable.Range(0, 5).Select(k => KindCell(z, k)))
                + $" | {Per(z.RoundMaxSum, z.N)} ／ {z.RoundPeak} | {Per(z.RoundTurns, z.N)} ／ {Per(z.RoundSlashes, z.N)} ／ {Per0(z.RoundDealt, z.N)} | {Per(z.ZanTurns - z.RoundTurns, z.N)} ／ {Per0(z.NormalDealt, z.N)} | {Per0(z.ZanDealt, z.N)} | {Pct(z.ZanAvengeDealt, z.ZanDealt)}% |");
        Console.WriteLine();

        Console.WriteLine("## 2. 駒ごとの与ダメの割合（敵への与ダメ・同士討ちを除く）");
        Console.WriteLine();
        Console.WriteLine("| 波 | 味方の与ダメ 計 | " + string.Join(" | ", Five) + " |");
        Console.WriteLine("|---|--:|" + string.Concat(Five.Select(_ => "--:|")));
        foreach (var (w, z) in res)
            Console.WriteLine($"| {WN(w)} | {Per0(z.DmgAll, z.N)} | " + string.Join(" | ", Five.Select(n => $"{DmgShare(z, n)}%")) + " |");
        Console.WriteLine();

        Console.WriteLine("## 3. ゴルム");
        Console.WriteLine();
        Console.WriteLine("肩代わり ＝ 巨躯で飲み込んだ量（名目・吐き戻しと同じ量）。吐き戻し ＝ 飲み込んだ力を返した攻撃力（受け手ごと・1戦あたり）。精気 ＝ 大喰らいで味方から吸った量（味方への削り）。還し ＝ 倒れたときの回復（届いた量）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 肩代わり | 吐き戻し（受け手ごと） | 精気 | 還し | 手番 ／ 攻撃 | 自分の与ダメ |");
        Console.WriteLine("|---|--:|---|--:|--:|---|--:|");
        foreach (var (w, z) in res)
            Console.WriteLine($"| {WN(w)} | {Per0(z.Swallowed, z.N)} | {PerTo(z.RegurgTo, z.N)} | {Per0(z.Drain, z.N)} | {Per0(z.Refunded, z.N)} | {Per(z.GolmTurns, z.N)} ／ {Per(z.GolmAttacks, z.N)} | {Per0(z.GolmDealt, z.N)} |");
        Console.WriteLine();

        Console.WriteLine("## 4. ドハ");
        Console.WriteLine();
        Console.WriteLine("背を押した ＝ 手番でいちばん痛みを肩代わりした仲間をもう一度動かした回数（相手ごと）。渡した力 ＝ 自分に宿った力を渡した量。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 手番 | 背を押した | 相手 | 渡した力 | 攻撃 ／ 与ダメ |");
        Console.WriteLine("|---|--:|--:|---|--:|---|");
        foreach (var (w, z) in res)
            Console.WriteLine($"| {WN(w)} | {Per(z.DohaTurns, z.N)} | {Per(z.Pushes, z.N)} | {PerTo(z.PushTo, z.N)} | {Per(z.Passed, z.N)} | {Per(z.DohaAttacks, z.N)} ／ {Per0(z.DohaDealt, z.N)} |");
        Console.WriteLine();

        Console.WriteLine("## 5. ミサ");
        Console.WriteLine();
        Console.WriteLine("一振り ＝ 羽の一振り（手番）。追った ／ 乱射 ＝ 標を追った羽 ／ 標が無くて敵味方構わず飛んだ羽。指差し撃ち ＝ 標が付いた瞬間に飛ぶ羽（敵へ ／ 味方へ）。味方に当たった ＝ ミサの羽が味方に当たった回数（受け手ごと）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 一振り | 追った ／ 乱射 | 指差し撃ち 敵 ／ 味方 | 味方に当たった（受け手ごと） | 敵への与ダメ |");
        Console.WriteLine("|---|--:|---|---|---|--:|");
        foreach (var (w, z) in res)
            Console.WriteLine($"| {WN(w)} | {Per(z.Volleys, z.N)} | {Per(z.Chased, z.N)} ／ {Per(z.Sprayed, z.N)} | {Per(z.MfFoe, z.N)} ／ {Per(z.MfAlly, z.N)} | {PerTo(z.FfTo, z.N)} | {Per0(z.MisaDealt, z.N)} |");
        Console.WriteLine();

        Console.WriteLine("## 6. 段0 の前後（旧 ＝ `TomeH333`・爪痕あり → 規定 ＝ 爪痕なし）");
        Console.WriteLine();
        Console.WriteLine("ミサのいる台（試遊の台 ＋ `compare` の行）。勝率・倒しT（勝った戦の決着T）・爪痕で削った最大HP（旧・1戦あたり）。");
        Console.WriteLine();
        var misaRows = PlaytestH334.Where(r => r.F.Occupied().Any(o => o.Def.Id == "tome")).Concat(CompareBuildsH334().Where(r => r.F.Occupied().Any(o => o.Def.Id == "tome"))).ToArray();   // 第335期: ザンを第301〜334期の規定に固定
        Console.WriteLine("| 台 | 波 | 旧 | 規定 | 勝率の差 | 倒しT の差 | 爪痕（旧） |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|");
        foreach (var (n, f) in misaRows)
            foreach (var w in n == GuardRow ? Waves : new[] { Waves[0] })
            {
                var o = Measure(Pin333(f), w); var z = Measure(f, w);
                double dt = (o.Wins == 0 || z.Wins == 0) ? double.NaN : (double)z.WinT / z.Wins - (double)o.WinT / o.Wins;
                Console.WriteLine($"| {n} | {w.Name} | {WinCell(o)} | {WinCell(z)} | {Sg(z.Win - o.Win)} | {(double.IsNaN(dt) ? "—" : Sg(dt))} | {Per0(o.Scar, o.N)} |");
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
        string WN(S287.Wave w) => StrongWaves.Contains(w) ? $"強 {w.Name}" : w.Name;
        foreach (var (bn, bf) in boards) foreach (var v in vers) foreach (var w in WavesOf(bn)) res[(bn, v.Name, WN(w))] = Measure(Apply(bf, v), w);
        Console.WriteLine($"# {Tag} §6 代表台 {boards.Length} × 5 波（守り型は ＋ 強くした近衛 ／ 大隊）× 版（{string.Join(" ／ ", vers.Select(v => v.Name))}）・seed 0..{Seeds - 1}（軽い期）");
        Console.WriteLine();
        Console.WriteLine("規定 ＝ いまの規定（段0 入り・ミサは爪痕なし）。版はザンだけを差し替える。強 ＝ HP 500% ／ 攻 300% を各波の倍率に重ねる（第315期と同じ）。台の並び（前1・前3・中央・後1・後3）:");
        Console.WriteLine();
        foreach (var (bn, bf) in boards) Console.WriteLine($"- {bn}: {S307.Seats(bf)}");
        Console.WriteLine();
        string g0 = boards[0].Name;
        var gw = WavesOf(g0).Select(WN).ToArray();

        Console.WriteLine("## 1. 守り型: ザンの攻撃（1戦あたり 回 ／ 与ダメ）と決着T");
        Console.WriteLine();
        Console.WriteLine("割り込みの内訳は Phase 0 §1 と同じ分け方。止めた ＝ 版が止めた割り込み（ZC-a 中継 ／ ZC-b 上限 ／ ZC-d 濡れ衣）。ザンの割合 ＝ 味方の与ダメのうちザン。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | 勝率・決着T | 割り込み 計 | " + string.Join(" | ", KindNames) + " | 1ターンの最多 | 止めた | 仇巡り 手番 ／ 太刀 | ザンの与ダメ ／ 割合 | 割り込みの割合 |");
        Console.WriteLine("|---|---|---|--:|" + string.Concat(KindNames.Select(_ => "--:|")) + "--:|--:|---|---|--:|");
        foreach (var wn in gw)
            foreach (var v in vers)
            {
                var z = res[(g0, v.Name, wn)];
                Console.WriteLine($"| {wn} | {v.Name} | {WinCell(z)} | {Per(z.ZanAvenges, z.N)} ／ {Per0(z.ZanAvengeDealt, z.N)} | " + string.Join(" | ", Enumerable.Range(0, 5).Select(k => KindCell(z, k)))
                    + $" | {Per(z.RoundMaxSum, z.N)} ／ {z.RoundPeak} | {Per(z.RelaySkipped + z.Capped + z.FrameCapped, z.N)} | {Per(z.RoundTurns, z.N)} ／ {Per(z.RoundSlashes, z.N)} | {Per0(z.ZanDealt, z.N)} ／ {DmgShare(z, "ザン")}% | {Pct(z.ZanAvengeDealt, z.ZanDealt)}% |");
            }
        Console.WriteLine();

        Console.WriteLine("## 2. 守り型: ドハの手番 ／ 背を押した・駒ごとの与ダメの割合");
        Console.WriteLine();
        Console.WriteLine("| 波 | 版 | ドハの手番 ／ 背を押した（相手） | 味方の与ダメ 計 | " + string.Join(" | ", Five) + " |");
        Console.WriteLine("|---|---|---|--:|" + string.Concat(Five.Select(_ => "--:|")));
        foreach (var wn in gw)
            foreach (var v in vers)
            {
                var z = res[(g0, v.Name, wn)];
                Console.WriteLine($"| {wn} | {v.Name} | {Per(z.DohaTurns, z.N)} ／ {Per(z.Pushes, z.N)}（{PerTo(z.PushTo, z.N)}） | {Per0(z.DmgAll, z.N)} | " + string.Join(" | ", Five.Select(n => $"{DmgShare(z, n)}%")) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## 3. 代表台の勝率（規定との差）・決着T（ボスは倒しT）・倒れた味方 ／戦");
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

        if (vers.Any(v => v.Ascii == "c"))
        {
            Console.WriteLine("## 4. ZC-c: ザンが返り血で倒れた割合と、倒れたターン（平均）");
            Console.WriteLine();
            var vc = vers.First(v => v.Ascii == "c");
            Console.WriteLine("| 台 | " + string.Join(" | ", gw) + " |");
            Console.WriteLine("|---|" + string.Concat(gw.Select(_ => "---|")));
            foreach (var (bn, _) in boards)
                Console.WriteLine($"| {bn} | " + string.Join(" | ", gw.Select(wn => res.TryGetValue((bn, vc.Name, wn), out var z)
                    ? $"{Pct(z.RecoilDeaths, z.N)}%{(z.RecoilDeaths > 0 ? $"・T{Per(z.RecoilDeathTurn, z.RecoilDeaths)}" : "")}（ザンが倒れた {Pct(z.ZanDeaths, z.N)}%・規定 {Pct(res[(bn, vers[0].Name, wn)].ZanDeaths, z.N)}%）" : "—")) + " |");
            Console.WriteLine();
        }

        Console.WriteLine("## 5. 壊れの兆し（勝率が規定から 15pt 以上落ちる台・波）");
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
        Console.WriteLine($"ザンのいる行: {string.Join("・", rows.Where(r => r.F.Occupied().Any(o => o.Def.Id == "zan")).Select(r => r.Name))}");
        for (int vi = 1; vi < vers.Length; vi++)
        {
            Console.WriteLine();
            Console.WriteLine($"## {vers[vi].Name}");
            Console.WriteLine();
            S316.PrintCompare(rows, b, Grid(vers[vi]), "zan");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }
}
