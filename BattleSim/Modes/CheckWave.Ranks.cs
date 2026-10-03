using BattleCore;
using static Common;

// checkwave ranks —— 第263期「物差しを連続量に替える」。第260〜263期の全版を6台で測り直し、連続量の順位と、ボス × 手数の順位の逆転を出す。
// 勝率（二値）は使わない。測るのは器具に残っている版だけ（新しい版を足していない）。
static partial class CheckWaveDiag
{
    /// <summary>ボスの版（列名, 版）。古い期の版は期の札を前に付ける。</summary>
    static (string Key, CWave W)[] RankBoss => new[]
    {
        ("260:B-全4", CWaveOf("260:B-全4")), ("261:B-全4", CWaveOf("261:B-全4")), ("B2-全4", CWaveOf("B2-全4")), ("B2-半4", CWaveOf("B2-半4")), ("B3-桁", CWaveOf("B3-桁")),
    };
    /// <summary>手数チェックの版。</summary>
    static (string Key, CWave W)[] RankHand => new[]
    {
        ("260:T-50後", CWaveOf("260:T-50後")), ("261:W-50後", CWaveOf("261:W-50後")), ("W2-50後", CWaveOf("W2-50後")), ("W2-50奥", CWaveOf("W2-50奥")), ("W3-割合", CWaveOf("W3-割合")),
    };

    /// <summary>手数の連続量（大きいほど良い向きに揃える）。</summary>
    static readonly (string Name, Func<Agg, double> F, string Fmt)[] HandMetrics =
    {
        ("回復を上回った窓", a => a.HealTurns == 0 ? double.NaN : 100.0 * a.OverTurns / a.HealTurns, "{0:F1}%"),
        ("窓あたり出力", a => a.WinCnt == 0 ? 0 : (double)a.WinDmg / a.WinCnt, "{0:F0}"),
        ("癒し手を割るT（早いほど良い）", a => a.HealerDead == 0 ? -99 : -(double)a.HealerDeadT / a.HealerDead, "T{0:F1}"),
    };

    /// <summary>ボスの連続量（大きいほど良い）: 倒しT が早いほど良い。倒せない台は最後に置き、その中は 20 ターンまでの総与ダメで並べる。</summary>
    static double BossScore(Agg a) => a.Kill > 0 ? 1000 - (double)a.KillT / a.Kill : -(1e6 / Math.Max(1.0, (double)a.WinDmg / a.N));

    static void Ranks()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        BossHp = ChosenBossHp;
        var res = new Dictionary<(string, string), Agg>();
        foreach (string b in Boards)
            foreach (var (k, c) in RankBoss.Concat(RankHand)) res[(b, k)] = MeasureWave(BoardOf(b), c);

        Console.WriteLine("# 第263期 連続量の順位（第260〜263期の全版・6台・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("勝率は使わない。ボスは倒しT（倒せない台は最後・その中は 20 ターンまでの総与ダメの順）、手数は回復を上回った窓の割合・窓あたり出力・癒し手を割ったT。");
        Console.WriteLine();

        // ---- 表R-1 ボス ----
        Console.WriteLine("## 表R-1 ボス —— 倒しT ／ 純実入り（ボスが入れた − 隊の回復・1戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", RankBoss.Select(x => x.Key)) + " |");
        Console.WriteLine("|---|" + string.Concat(RankBoss.Select(_ => "--:|")));
        foreach (string b in Boards)
            Console.WriteLine($"| {b} | " + string.Join(" | ", RankBoss.Select(x =>
            {
                var a = res[(b, x.Key)];
                string t = a.Kill == 0 ? $"倒せない（与ダメ {Per(a.WinDmg, a.N)}）" : "T" + Per(a.KillT, a.Kill);
                return $"{t} ／ {Per(a.BossDealt - a.MineHealed, a.N)}";
            })) + " |");
        Console.WriteLine();

        // ---- 表R-2 手数 ----
        Console.WriteLine("## 表R-2 手数 —— 回復を上回った窓 ／ 窓あたり出力 ／ 癒し手を割ったT");
        Console.WriteLine();
        Console.WriteLine("| 台 | " + string.Join(" | ", RankHand.Select(x => x.Key)) + " |");
        Console.WriteLine("|---|" + string.Concat(RankHand.Select(_ => "---|")));
        foreach (string b in Boards)
            Console.WriteLine($"| {b} | " + string.Join(" | ", RankHand.Select(x =>
            {
                var a = res[(b, x.Key)];
                return $"{Pct(a.OverTurns, a.HealTurns)}% ／ {Per(a.WinDmg, a.WinCnt)} ／ {(a.HealerDead == 0 ? "割れない" : "T" + Per(a.HealerDeadT, a.HealerDead))}";
            })) + " |");
        Console.WriteLine();

        // ---- 表R-3 順位 ----
        static string RankOf(IEnumerable<(string B, double S)> xs)
        {
            var list = xs.Where(x => !double.IsNaN(x.S)).OrderByDescending(x => x.S).ToList();
            return string.Join(" ＞ ", list.Select(x => x.B));
        }
        Console.WriteLine("## 表R-3 順位（左が良い）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 物差し | 順位 |");
        Console.WriteLine("|---|---|---|");
        foreach (var (k, _) in RankBoss) Console.WriteLine($"| {k} | 倒しT | {RankOf(Boards.Select(b => (b, BossScore(res[(b, k)]))))} |");
        foreach (var (k, _) in RankHand)
            foreach (var m in HandMetrics) Console.WriteLine($"| {k} | {m.Name} | {RankOf(Boards.Select(b => (b, m.F(res[(b, k)]))))} |");
        Console.WriteLine();

        // ---- 表R-4 逆転 ----
        Console.WriteLine("## 表R-4 順位の逆転（ボスの倒しT × 手数の物差し・同じ値の組は数えない）");
        Console.WriteLine();
        Console.WriteLine("台の組 (i, j) で、ボスでは i が j より良く、手数では j が i より良い（またはその逆）組を数える。倒せない台どうし・手数で同じ値の組は数えない。");
        Console.WriteLine();
        Console.WriteLine("| ボス ＼ 手数 | " + string.Join(" | ", RankHand.SelectMany(h => HandMetrics.Select(m => $"{h.Key} {m.Name}"))) + " |");
        Console.WriteLine("|---|" + string.Concat(RankHand.SelectMany(h => HandMetrics.Select(_ => "---|"))));
        foreach (var (bk, _) in RankBoss)
        {
            var cells = new List<string>();
            foreach (var (hk, _) in RankHand)
                foreach (var m in HandMetrics)
                {
                    var flips = new List<string>();
                    for (int i = 0; i < Boards.Length; i++)
                        for (int j = i + 1; j < Boards.Length; j++)
                        {
                            string bi = Boards[i], bj = Boards[j];
                            double si = BossScore(res[(bi, bk)]), sj = BossScore(res[(bj, bk)]);
                            double hi = m.F(res[(bi, hk)]), hj = m.F(res[(bj, hk)]);
                            if (double.IsNaN(hi) || double.IsNaN(hj) || Math.Abs(si - sj) < 1e-9 || Math.Abs(hi - hj) < 1e-9) continue;
                            if (Math.Sign(si - sj) != Math.Sign(hi - hj)) flips.Add($"{Short(bi)}×{Short(bj)}");
                        }
                    cells.Add($"{flips.Count}{(flips.Count == 0 ? "" : "（" + string.Join("・", flips) + "）")}");
                }
            Console.WriteLine($"| {bk} | " + string.Join(" | ", cells) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒。");
    }

    static string Short(string b) => b switch { "燃焼 T3-244" => "燃244", "燃焼 T3-255" => "燃255", "混ぜ-255" => "混ぜ", _ => b };
}
