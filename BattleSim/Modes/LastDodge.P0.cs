using BattleCore;
using static Common;

// lastdodge phase0 —— Q0-1 一撃死の実数 ／ Q0-2 倒れ方の残り（第227期・L0 ＝ 前段の規定だけで回る）。
static partial class LastDodgeDiag
{
    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第227期 Phase 0（`0 lastdodge phase0`・L0 ＝ 前段の規定）");
        Console.WriteLine();
        Ver l0 = VerOf("L0");
        var pick = PickSeats(MHane225, l0);
        var top = pick[0];
        var front = pick.First(x => SeroFront(x.F));
        Console.WriteLine($"席（L0 × 九/新兵 × 200/200 × seed {PickSeed0}..{PickSeed0 + PickSeeds - 1}・120 通り）:");
        Console.WriteLine();
        Console.WriteLine($"- 総当たりの1位: {SeatsNamed(top.F)}（全員生存 {top.Sv}/{PickSeeds}・落ちた駒 {top.Fell}）");
        Console.WriteLine($"- セロ前列の1位: {SeatsNamed(front.F)}（全員生存 {front.Sv}/{PickSeeds}・落ちた駒 {front.Fell}・総当たりの {pick.IndexOf(front) + 1} 位）");
        Console.WriteLine($"- 第225期の席: {SeatsNamed(MHane225)}");
        Console.WriteLine();
        var benches = new (string Name, Formation F)[] { ("総当たり", top.F), ("セロ前", front.F), ("225の席", MHane225) };

        Console.WriteLine("## Q0-1 セロが倒れた一撃（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("4割以上 ＝ 倒れた一撃の直前の HP が最大HPの 4 割以上（緊急退避の窓の外で倒れた）。種類 ＝ 倒れた一撃（`Damage`）の出どころ——"
                          + "敵の手番の型 ／ 反撃・割り込み（`Reaction`）／ 状態異常（出どころなし）／ 味方（放電・爆発・巻き込み）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 全員生存 | 勝率 | セロが倒れた戦 | 4割以上 | " + string.Join(" | ", CauseNames) + " |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|" + string.Concat(Enumerable.Repeat("--:|", CauseNames.Length)));
        var res = new Dictionary<(int, int, int), Agg>();
        foreach (var (s, w) in new[] { (0, 4), (0, 5), (0, 0), (0, 1), (0, 2), (0, 3), (1, 4), (2, 4) })
            for (int b = 0; b < benches.Length; b++)
            {
                var a = res[(b, s, w)] = Measure(Apply(benches[b].F, l0), w, Scales[s].Sc);
                Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {F1(a.Surv)} | {F1(a.Win)} | {100.0 * a.SeroFell / a.N:F1}% | "
                                  + $"{(a.SeroFell == 0 ? "—" : (100.0 * a.SeroKillHigh / a.SeroFell).ToString("F0") + "%")} | "
                                  + string.Join(" | ", a.SeroCause.Select(x => a.SeroFell == 0 ? "—" : $"{100.0 * x / a.SeroFell:F0}%")) + " |");
            }
        Console.WriteLine();

        Console.WriteLine("## Q0-2 セロ以外で倒れる駒（倒れた戦の割合 ／ 倒れた平均ターン ／ 原因の最多）");
        Console.WriteLine();
        string[] ids = { "hane", "basa", "yomi", "shio", "sero" };
        Console.WriteLine("| 台 | 倍率・波 | 落ちた駒/戦 | " + string.Join(" | ", ids) + " |");
        Console.WriteLine("|---|---|--:|" + string.Concat(Enumerable.Repeat("---|", ids.Length)));
        foreach (var ((b, s, w), a) in res.OrderBy(kv => kv.Key.Item2).ThenBy(kv => Array.IndexOf(new[] { 4, 5, 0, 1, 2, 3 }, kv.Key.Item3)).ThenBy(kv => kv.Key.Item1))
        {
            string cell(string id)
            {
                long n = a.Fell.GetValueOrDefault(id);
                if (n == 0) return "0%";
                var c = a.FellCause[id];
                int mx = Array.IndexOf(c, c.Max());
                return $"{100.0 * n / a.N:F1}% ／ T{(double)a.FellT[id] / n:F1} ／ {CauseNames[mx]} {100.0 * c[mx] / n:F0}%";
            }
            Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {F2(a.Per(a.FellTotal))} | " + string.Join(" | ", ids.Select(cell)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}
