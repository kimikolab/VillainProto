using BattleCore;
using static Common;

// lastdodge run —— 表A〜D（第227期）。台 M-ハネ（版ごとに総当たりの1位 ／ セロ前列の1位）・M-ハネ（第225期の席）・参考（雷の編成・ポンの席）
// × 版 L0〜L3 × 波（九/新兵・九/農兵・本編の第2〜5波）× 倍率（200/200・200/115・115/115）・seed 0..199。
static partial class LastDodgeDiag
{
    static void PrintPick(string ver, List<(Formation F, int W, int Sv, int Fell, long T)> list)
    {
        var top = list[0];
        int fi = list.FindIndex(x => SeroFront(x.F));
        Console.WriteLine($"### {ver} の席（九/新兵 × 200/200 × seed {PickSeed0}..{PickSeed0 + PickSeeds - 1}・120 通り）");
        Console.WriteLine();
        Console.WriteLine($"全員生存が最大と同値の並び: {list.Count(x => x.Sv == top.Sv)} 通り。セロ前列の1位は総当たりの {fi + 1} 位。");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 席 | 全員生存/" + PickSeeds + " | 落ちた駒（計） | 勝ち数 | 決着T |");
        Console.WriteLine("|--:|---|--:|--:|--:|--:|");
        foreach (int i in Enumerable.Range(0, 5).Append(fi).Distinct())
            Console.WriteLine($"| {i + 1}{(i == fi ? "（セロ前）" : "")} | {SeatsNamed(list[i].F)} | {list[i].Sv} | {list[i].Fell} | {list[i].W} | {(list[i].W == 0 ? "—" : ((double)list[i].T / list[i].W).ToString("F2"))} |");
        Console.WriteLine();
    }

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第227期 —— 必死の逃げ足と回避盾の測り直し（`0 lastdodge run`）");
        Console.WriteLine();
        var vers = Versions;
        Console.WriteLine("版: " + string.Join(" ／ ", vers.Select(v => v.Tag)) + "。**線は置かない。**");
        Console.WriteLine();

        // 台: (名前, 版 → 席)
        var seatTop = new Dictionary<string, Formation>();
        var seatFront = new Dictionary<string, Formation>();
        foreach (var v in vers)
        {
            var list = PickSeats(MHane225, v);
            PrintPick(v.Tag, list);
            seatTop[v.Tag] = list[0].F;
            seatFront[v.Tag] = list.First(x => SeroFront(x.F)).F;
        }
        var benches = new List<(string Name, Func<Ver, Formation> F, bool Versions)>
        {
            ("総当たり", v => seatTop[v.Tag], true),
            ("セロ前", v => seatFront[v.Tag], true),
            ("225の席", v => Apply(MHane225, v), true),
            ("参考 雷（ポンの席）", _ => Thunder, false),
        };
        foreach (var v in vers) Console.WriteLine($"- {v.Tag}: 総当たり {SeatsNamed(seatTop[v.Tag])} ／ セロ前 {SeatsNamed(seatFront[v.Tag])}");
        Console.WriteLine($"- 225の席: {SeatsNamed(MHane225)} ／ 参考 雷: {SeatsNamed(Thunder)}");
        Console.WriteLine();

        var res = new Dictionary<(int, string, int, int), Agg>();
        for (int b = 0; b < benches.Count; b++)
            foreach (var v in benches[b].Versions ? vers : vers.Take(1).ToArray())
                for (int w = 0; w < WaveNames.Length; w++)
                    for (int s = 0; s < Scales.Length; s++)
                        res[(b, v.Tag, w, s)] = Measure(benches[b].F(v), w, Scales[s].Sc);
        int[] waveOrder = { 4, 5, 0, 1, 2, 3 };
        IEnumerable<Ver> VersOf(int b) => benches[b].Versions ? vers : vers.Take(1);

        // ---- 表A ----
        Console.WriteLine("## 表A. 全員生存 ／ 勝率（%）");
        Console.WriteLine();
        foreach (int s in Enumerable.Range(0, Scales.Length))
        {
            Console.WriteLine($"### 倍率 {Scales[s].Name}");
            Console.WriteLine();
            Console.WriteLine("| 台 | 版 | " + string.Join(" | ", waveOrder.Select(w => WaveNames[w])) + " | 本編の平均 |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", waveOrder.Length)) + "---|");
            for (int b = 0; b < benches.Count; b++)
                foreach (var v in VersOf(b))
                {
                    double ms = Enumerable.Range(0, 4).Average(w => res[(b, v.Tag, w, s)].Surv), mw = Enumerable.Range(0, 4).Average(w => res[(b, v.Tag, w, s)].Win);
                    Console.WriteLine($"| {benches[b].Name} | {v.Tag} | " + string.Join(" | ", waveOrder.Select(w => res[(b, v.Tag, w, s)]).Select(a => $"{F1(a.Surv)} ／ {F1(a.Win)}")) + $" | {F1(ms)} ／ {F1(mw)} |");
                }
            Console.WriteLine();
        }

        Console.WriteLine("### 表A'. 決着T と落ちた駒（倒れた戦の割合 ／ 倒れた平均ターン）");
        Console.WriteLine();
        string[] ids = { "hane", "basa", "yomi", "shio", "sero" };
        Console.WriteLine("| 台 | 倍率・波 | 版 | 全員生存 | 勝率 | 決着T | 落ちた駒 | " + string.Join(" | ", ids) + " |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|" + string.Concat(Enumerable.Repeat("---|", ids.Length)));
        foreach (var (s, w) in new[] { (0, 4), (0, 5), (1, 4) })
            for (int b = 0; b < 3; b++)
                foreach (var v in vers)
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F1(a.Surv)} | {F1(a.Win)} | {F2(a.WinT)} | {F2(a.Per(a.FellTotal))} | "
                                      + string.Join(" | ", ids.Select(id => a.Fell.GetValueOrDefault(id) == 0 ? "0%" : $"{100.0 * a.Fell[id] / a.N:F1}% ／ T{(double)a.FellT[id] / a.Fell[id]:F1}")) + " |");
                }
        Console.WriteLine();

        // ---- 表B ----
        Console.WriteLine("## 表B. セロ（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("逃げ足 ＝ 必死の逃げ足の発動（1戦の回数）／ 発動した戦 ／ また倒れた ＝ 発動した戦のうちセロがその後に倒れた割合 ／ 回数切れ ＝ 回数を使い切った後に来た致命の一撃（発動しなかった）。"
                          + "段 ＝ 発動したときの段（0〜1 ／ 2 ／ 3 の内訳）。前列 ＝ ターンの頭にセロが敵の単体攻撃の的になれる列にいた割合。引きつけた ＝ 挑発。4割以上 ＝ 倒れた一撃の直前の HP が 4 割以上。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 逃げ足 | 発動した戦 | また倒れた | 回数切れ | 段0〜1 ／ 2 ／ 3 | 前列 | 引きつけた | 引き剥がされた | 判定 | 避けた（逃げ足を含む） | 撃ち返し | セロの与ダメ | セロが倒れた | 4割以上 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (s, w) in new[] { (0, 4), (0, 5), (0, 0), (0, 2), (1, 4) })
            for (int b = 0; b < 3; b++)
                foreach (var v in vers)
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F2(a.Per(a.Dodges))} | {100.0 * a.DodgeBattles / a.N:F1}% | "
                                      + $"{(a.DodgeBattles == 0 ? "—" : (100.0 * a.RefellAfterDodge / a.DodgeBattles).ToString("F1") + "%")} | {F2(a.Per(a.DodgeSpent))} | "
                                      + $"{a.DodgeByStage[0] + a.DodgeByStage[1]} ／ {a.DodgeByStage[2]} ／ {a.DodgeByStage[3]} | {100.0 * a.SeroFront / Math.Max(1, a.SeroTurns):F0}% | {F2(a.Per(a.Drew))} | {F2(a.Per(a.Stolen))} | "
                                      + $"{F2(a.Per(a.EvRolls))} | {F2(a.Per(a.Evades))} | {F2(a.Per(a.Ripostes))} | {F1(a.Per(a.SeroDealt))} | {100.0 * a.SeroFell / a.N:F1}% | "
                                      + $"{(a.SeroFell == 0 ? "—" : (100.0 * a.SeroKillHigh / a.SeroFell).ToString("F0") + "%")} |");
                }
        Console.WriteLine();

        Console.WriteLine("### 表B'. 逃げ足の後にまた倒れた一撃の種類（九/新兵 × 200/200・全台の合計）");
        Console.WriteLine();
        Console.WriteLine("| 版 | また倒れた（戦） | うち先に回数切れの致命打 | " + string.Join(" | ", CauseNames) + " |");
        Console.WriteLine("|---|--:|--:|" + string.Concat(Enumerable.Repeat("--:|", CauseNames.Length)));
        foreach (var v in vers)
        {
            var all = new Agg();
            for (int b = 0; b < 3; b++) all.Merge(res[(b, v.Tag, MainWave, 0)]);
            Console.WriteLine($"| {v.Tag} | {all.RefellAfterDodge} | {all.RefellSpent} | " + string.Join(" | ", all.RefellCause.Select(x => x.ToString())) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("### 表B''. セロが倒れた一撃の種類（九/新兵 × 200/200・倒れた回数の内訳）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倒れた | " + string.Join(" | ", CauseNames) + " |");
        Console.WriteLine("|---|---|--:|" + string.Concat(Enumerable.Repeat("--:|", CauseNames.Length)));
        for (int b = 0; b < 3; b++)
            foreach (var v in vers)
            {
                var a = res[(b, v.Tag, MainWave, 0)];
                Console.WriteLine($"| {benches[b].Name} | {v.Tag} | {a.SeroFell} | " + string.Join(" | ", a.SeroCause.Select(x => x.ToString())) + " |");
            }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C. 緊急退避（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("下げられた ＝ セロが緊急退避で下げられた回数 ／ 逃げ足の後に ＝ その戦で逃げ足が発動した後に下げられた回数（逃げ足で生き残った後に 4 割を切って下げられた流れ）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 版 | 下げられた | 逃げ足の後に | 逃げ足 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|");
        foreach (var (s, w) in new[] { (0, 4), (0, 5), (1, 4) })
            for (int b = 0; b < 3; b++)
                foreach (var v in vers)
                {
                    var a = res[(b, v.Tag, w, s)];
                    Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} {WaveNames[w]} | {v.Tag} | {F2(a.Per(a.Lowered))} | {F2(a.Per(a.LoweredAfterDodge))} | {F2(a.Per(a.Dodges))} |");
                }
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D. 目標（九/新兵 × 200/200・全員生存）");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 台 | 版 | 全員生存 | 勝率 | 決着T |");
        Console.WriteLine("|--:|---|---|--:|--:|--:|");
        var rank = res.Where(kv => kv.Key.Item3 == MainWave && kv.Key.Item4 == 0)
            .Select(kv => (Bench: benches[kv.Key.Item1].Name, V: kv.Key.Item2, A: kv.Value))
            .OrderByDescending(x => x.A.Surv).ThenByDescending(x => x.A.Win).ThenBy(x => x.A.WinT).ToList();
        for (int i = 0; i < rank.Count; i++)
            Console.WriteLine($"| {i + 1} | {rank[i].Bench} | {rank[i].V} | {F1(rank[i].A.Surv)} | {F1(rank[i].A.Win)} | {F2(rank[i].A.WinT)} |");
        Console.WriteLine();
        Console.WriteLine("### セロ前列の1位 − 総当たりの1位（全員生存・pt）");
        Console.WriteLine();
        Console.WriteLine("| 倍率・波 | " + string.Join(" | ", vers.Select(v => v.Tag)) + " |");
        Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("--:|", vers.Length)));
        foreach (var (s, w) in new[] { (0, 4), (0, 5), (1, 4), (2, 4) })
            Console.WriteLine($"| {Scales[s].Name} {WaveNames[w]} | " + string.Join(" | ", vers.Select(v => (res[(1, v.Tag, w, s)].Surv - res[(0, v.Tag, w, s)].Surv).ToString("+0.0;−0.0;0.0"))) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}
