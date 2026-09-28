using BattleCore;
using static Common;

// spring2 phase0 —— Q0-1 取り合い ／ Q0-2 弾けなかった内訳（S0・S1 だけで回る）。
static partial class Spring2Diag
{
    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Formation f = MHane228;
        Console.WriteLine("# 第232期 Phase 0（前段の規定・台 M-ハネ（228 H3）・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"席: {SeatsNamed(f)}");
        Console.WriteLine();
        var s0 = new Dictionary<(int, int), Agg>();
        var s1 = new Dictionary<(int, int), Agg>();
        for (int s = 0; s < 2; s++)
            for (int w = 0; w < WaveNames.Length; w++)
            {
                s0[(s, w)] = Measure(f, VerOf("S0"), w, Scales[s].Sc);
                s1[(s, w)] = Measure(f, VerOf("S1"), w, Scales[s].Sc);
            }

        Console.WriteLine("## Q0-1 取り合い（S1・隣の弾き返し1回ごとに、同じ被弾の後に緊急退避がどうなったか・/戦）");
        Console.WriteLine();
        Console.WriteLine("順序は engine が決めている: 殴られた味方の被弾 → ハネの弾き返し（隣の分・ハネと味方の入れ替えまで）→ 味方への通知（シオの緊急退避の判定）。**ハネが必ず先に動く。**");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | 隣の弾き返し | 退避が同じ味方を下げた（両方） | 退避が別の駒を下げた | 5割未満なのに下げなかった | 5割以上（退避は要らない） | S0: 隣の被弾で退避 | 退避/戦 S0 → S1 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        for (int s = 0; s < 2; s++)
            for (int w = 0; w < WaveNames.Length; w++)
            {
                var a = s1[(s, w)]; var z = s0[(s, w)];
                Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | {F2(a.Per(a.GFires))} | {F2(a.Per(a.GuardBoth))} | {F2(a.Per(a.GuardOther))} | {F2(a.Per(a.GuardSkipped))} | {F2(a.Per(a.GuardNoNeed))} | "
                    + $"{F2(z.Per(z.AdjRetreat))} | {F2(z.Per(z.T.RetreatSwaps))} → {F2(a.Per(a.T.RetreatSwaps))} |");
            }
        Console.WriteLine();

        Console.WriteLine("## Q0-2 弾けなかった内訳（S1・隣の味方の被弾で判定に来た回数と、そのうち弾けなかった理由・/戦）");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | 判定に来た | 弾いた | 経路に属さない席（○席） | 経路の最後尾（弾く先が無い） | 上限 | 粛 | 割り込みの中・痺れ | 合計の検算 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
        for (int s = 0; s < 2; s++)
            for (int w = 0; w < WaveNames.Length; w++)
            {
                var a = s1[(s, w)];
                long sum = a.GFires + a.GOffLane + a.GTail + a.GCapped + a.GHushed + a.GHeld;
                Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | {F2(a.Per(a.GChances))} | {F2(a.Per(a.GFires))} | {F2(a.Per(a.GOffLane))} | {F2(a.Per(a.GTail))} | {F2(a.Per(a.GCapped))} | "
                    + $"{F2(a.Per(a.GHushed))} | {F2(a.Per(a.GHeld))} | {(sum == a.GChances ? "一致" : $"**不一致 {sum}/{a.GChances}**")} |");
            }
        Console.WriteLine();

        Console.WriteLine("## Q0-3 台の現状（S0 ＝ 前段の規定・全員生存 ／ 勝率・退避/戦・弾き返し/戦（自分）・混乱/戦）");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | S0 全員生存 ／ 勝率 | 退避 | 弾き返し（自分） | 混乱 | S1 全員生存 ／ 勝率 |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        for (int s = 0; s < 2; s++)
            for (int w = 0; w < WaveNames.Length; w++)
            {
                var z = s0[(s, w)]; var a = s1[(s, w)];
                Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | {F1(z.T.Surv)} ／ {F1(z.T.Win)} | {F2(z.Per(z.T.RetreatSwaps))} | {F2(z.Per(z.T.SpringSelf))} | {F2(z.Per(z.T.SpringConfused))} | {F1(a.T.Surv)} ／ {F1(a.T.Win)} |");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}
