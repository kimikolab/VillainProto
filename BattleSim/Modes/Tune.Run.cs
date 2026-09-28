using BattleCore;
using static Common;

// tune run —— 表A〜D（§8.2）。
static partial class TuneDiag
{
    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第231期 表A〜D（seed 0..199）");
        Console.WriteLine();

        // 席（§8.1）
        var picks = PickSeats(MHane228, VerOf("VX2"), Scales[0].Sc);
        var best = picks[0];
        Console.WriteLine($"## 席の選び直し（VX2 × 九/新兵 × 400/300 × seed {PickSeed0}..{PickSeed0 + PickSeeds - 1}・120 通り）");
        Console.WriteLine();
        Console.WriteLine("| 順位 | 席 | 全員生存 | 勝ち | 落ちた駒（計） |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int i = 0; i < 5; i++) Console.WriteLine($"| {i + 1} | {SeatsNamed(picks[i].F)} | {picks[i].Sv}/{PickSeeds} | {picks[i].W}/{PickSeeds} | {picks[i].Fell} |");
        int r228 = picks.FindIndex(x => SeatsNamed(x.F) == SeatsNamed(Apply(MHane228, VerOf("VX2"))));
        Console.WriteLine($"| {r228 + 1} | （228 H3 の席）{SeatsNamed(picks[r228].F)} | {picks[r228].Sv}/{PickSeeds} | {picks[r228].W}/{PickSeeds} | {picks[r228].Fell} |");
        int ties = picks.Count(x => x.Sv == best.Sv && x.Fell == best.Fell && x.W == best.W);
        Console.WriteLine();
        Console.WriteLine($"- 1位と同じ（全員生存・落ちた駒・勝ち）の並び: {ties} 通り");
        Console.WriteLine();

        // 版の駒に戻した席（raw ＝ 規定の駒で組んだ同じ並び）
        Formation seatPick = Formation.Build(
            front1: Raw(best.F[0]!), front3: Raw(best.F[1]!), center: Raw(best.F[2]!), back1: Raw(best.F[3]!), back3: Raw(best.F[4]!));
        var benches = new (string Name, Formation F)[] { ("228 の席", MHane228), ("VX2 の席", seatPick) };

        var res = new Dictionary<(int B, int V, int W, int S), Agg>();
        foreach (int b in new[] { 0, 1 })
            for (int v = 0; v < Versions.Length; v++)
                for (int w = 0; w < WaveNames.Length; w++)
                    for (int s = 0; s < Scales.Length; s++)
                        res[(b, v, w, s)] = Measure(benches[b].F, Versions[v], w, Scales[s].Sc);

        for (int b = 0; b < 2; b++)
        {
            Console.WriteLine($"## 表A {benches[b].Name}（{SeatsNamed(benches[b].F)}）—— 全員生存 ／ 勝率");
            Console.WriteLine();
            for (int s = 0; s < Scales.Length; s++)
            {
                Console.WriteLine($"### {Scales[s].Name}");
                Console.WriteLine();
                Console.WriteLine("| 版 | " + string.Join(" | ", WaveNames) + " | 本編の平均（全員生存） | 九の平均（全員生存） |");
                Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", WaveNames.Length + 2)));
                for (int v = 0; v < Versions.Length; v++)
                {
                    var cells = Enumerable.Range(0, WaveNames.Length).Select(w => res[(b, v, w, s)]).ToList();
                    Console.WriteLine($"| {Versions[v].Tag} | " + string.Join(" | ", cells.Select(a => $"{F1(a.Surv)} ／ {F1(a.Win)}"))
                        + $" | {F1(cells.Take(4).Average(a => a.Surv))} | {F1(cells.Skip(4).Average(a => a.Surv))} |");
                }
                Console.WriteLine();
            }
        }

        // 決着T と落ちた駒（主の波 九/新兵・400/300 と 200/200）
        for (int b = 0; b < 2; b++)
            foreach (int s in new[] { 0, 1 })
                foreach (int w in new[] { MainWave, 3 })
                {
                    Console.WriteLine($"### 表A' {benches[b].Name} × {WaveNames[w]} × {Scales[s].Name} —— 決着T・落ちた駒（割合 ／ 平均ターン）");
                    Console.WriteLine();
                    Console.WriteLine("| 版 | 決着T（勝ち） | " + string.Join(" | ", UnitNames) + " |");
                    Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", Units.Length)));
                    for (int v = 0; v < Versions.Length; v++)
                    {
                        var a = res[(b, v, w, s)];
                        Console.WriteLine($"| {Versions[v].Tag} | {F2(a.WinT)} | " + string.Join(" | ", Units.Select(id =>
                            a.Fell.GetValueOrDefault(id) == 0 ? "0.0%" : $"{F1(100.0 * a.Fell[id] / a.N)}% ／ T{F1((double)a.FellT[id] / a.Fell[id])}")) + " |");
                    }
                    Console.WriteLine();
                }

        // 表B 緊急退避
        for (int b = 0; b < 2; b++)
        {
            Console.WriteLine($"## 表B 緊急退避（{benches[b].Name}）—— 退避/戦（うち一撃3割の条件だけで）・倒れた駒の直前の HP（<40 ／ 40-50 ／ 50-60 ／ 60-80 ／ 80+）");
            Console.WriteLine();
            Console.WriteLine("| 倍率 | 波 | " + string.Join(" | ", Versions.Select(v => v.Tag)) + " |");
            Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", Versions.Length)));
            for (int s = 0; s < 2; s++)
                for (int w = 0; w < WaveNames.Length; w++)
                    Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | " + string.Join(" | ", Enumerable.Range(0, Versions.Length).Select(v =>
                    {
                        var a = res[(b, v, w, s)];
                        return $"{F2(a.Per(a.RetreatSwaps))}（{F2(a.Per(a.RetreatHeavy))}）";
                    })) + " |");
            Console.WriteLine();
            Console.WriteLine("倒れた駒の直前の HP（敵の一撃で倒れた数/戦 と区間の内訳・400/300）");
            Console.WriteLine();
            Console.WriteLine("| 波 | " + string.Join(" | ", Versions.Select(v => v.Tag)) + " |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", Versions.Length)));
            for (int w = 0; w < WaveNames.Length; w++)
                Console.WriteLine($"| {WaveNames[w]} | " + string.Join(" | ", Enumerable.Range(0, Versions.Length).Select(v =>
                {
                    var a = res[(b, v, w, 0)];
                    return $"{F2(a.Per(a.DeathPre.Sum()))}: " + string.Join("/", a.DeathPre.Select(x => F2(a.Per(x))));
                })) + " |");
            Console.WriteLine();
            Console.WriteLine("そのターンの回数で止まった退避/戦・後ろ側に相手がいなかった/戦（400/300）");
            Console.WriteLine();
            Console.WriteLine("| 波 | " + string.Join(" | ", Versions.Select(v => v.Tag)) + " |");
            Console.WriteLine("|---|" + string.Concat(Enumerable.Repeat("---|", Versions.Length)));
            for (int w = 0; w < WaveNames.Length; w++)
                Console.WriteLine($"| {WaveNames[w]} | " + string.Join(" | ", Enumerable.Range(0, Versions.Length).Select(v =>
                {
                    var a = res[(b, v, w, 0)];
                    return $"{F2(a.Per(a.RetreatSpent))} ／ {F2(a.Per(a.RetreatNoPartner))}";
                })) + " |");
            Console.WriteLine();
        }

        // 表C ハネ
        for (int b = 0; b < 2; b++)
        {
            Console.WriteLine($"## 表C ハネ（{benches[b].Name}）—— 弾き返し/戦（自分 ＋ 隣の味方）・隣で入れ替わった・弾いて前へ出た敵の混乱・上限で止まった");
            Console.WriteLine();
            Console.WriteLine("| 倍率 | 波 | 版 | 自分 | 隣（判定に来た） | 隣の入れ替わり | 自分の弾きの入れ替わり | 混乱 | 上限で止まった | 前列にいたターン |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
            for (int s = 0; s < 2; s++)
                for (int w = 0; w < WaveNames.Length; w++)
                    foreach (string tag in new[] { "V0", "VB", "VX2" })
                    {
                        var a = res[(b, Array.FindIndex(Versions, x => x.Tag == tag), w, s)];
                        Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | {tag} | {F2(a.Per(a.SpringSelf))} | {F2(a.Per(a.SpringGuard))}（{F2(a.Per(a.GuardChances))}） | {F2(a.Per(a.GuardSwaps))} | {F2(a.Per(a.SpringSwapsSelf))} | "
                            + $"{F2(a.Per(a.SpringConfused))} | {F2(a.Per(a.SpringCapped))} | {(a.HaneAliveTurns == 0 ? "—" : F1(100.0 * a.HaneFrontTurns / a.HaneAliveTurns) + "%")} |");
                    }
            Console.WriteLine();
        }

        // 表D セロ
        for (int b = 0; b < 2; b++)
        {
            Console.WriteLine($"## 表D セロ（{benches[b].Name}）—— 移動の追撃/戦・上限で止まった・与ダメ（手番 ／ 乱れ撃ち ／ 撃ち返し ／ 移動の追撃）・挑発が効いていたターン");
            Console.WriteLine();
            Console.WriteLine("| 倍率 | 波 | 版 | 追撃/戦 | 上限 | 粛 | 与ダメ計 | 手番 | 乱れ撃ち | 撃ち返し | 移動の追撃 | 挑発 |");
            Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|");
            for (int s = 0; s < 2; s++)
                for (int w = 0; w < WaveNames.Length; w++)
                    foreach (string tag in new[] { "V0", "VC", "VX2" })
                    {
                        var a = res[(b, Array.FindIndex(Versions, x => x.Tag == tag), w, s)];
                        long turnD = a.SeroDealt - a.Riposte - a.Barrage - a.MoveDealt;
                        Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | {tag} | {F2(a.Per(a.MoveShots))} | {F2(a.Per(a.MoveCapped))} | {F2(a.Per(a.MoveHushed))} | {F1(a.Per(a.SeroDealt))} | "
                            + $"{F1(a.Per(turnD))} | {F1(a.Per(a.Barrage))} | {F1(a.Per(a.Riposte))} | {F1(a.Per(a.MoveDealt))} | {(a.SeroAliveTurns == 0 ? "—" : F1(100.0 * a.DecoyOnTurns / a.SeroAliveTurns) + "%")} |");
                    }
            Console.WriteLine();
            Console.WriteLine("移動の追撃のターン別/戦（VX2・400/300）: " + string.Join(" ／ ", Enumerable.Range(0, WaveNames.Length).Select(w =>
            {
                var a = res[(b, Versions.Length - 1, w, 0)];
                return WaveNames[w] + " " + string.Join(",", Enumerable.Range(1, 6).Select(t => F2(a.Per(a.MoveByTurn[t]))));
            })));
            Console.WriteLine();
        }

        // 追補: 組み合わせの分解（九/新兵 × 400/300 × seed 0..999・228 の席）——VX1 が VA1 を下回るのは B と C のどちらか
        {
            var extra = new[]
            {
                VerOf("V0"), VerOf("VA1"), new Ver("VA1+B", ShioHalf, HaneGuard, UnitCatalog.SeroC0), new Ver("VA1+C", ShioHalf, UnitCatalog.HaneS0, SeroShot), VerOf("VX1"),
                VerOf("VA2"), new Ver("VA2+B", ShioHeavy, HaneGuard, UnitCatalog.SeroC0), new Ver("VA2+C", ShioHeavy, UnitCatalog.HaneS0, SeroShot), VerOf("VX2"),
            };
            Console.WriteLine("## 追補 組み合わせの分解（228 の席 × 九/新兵 × 400/300 × seed 0..999）");
            Console.WriteLine();
            Console.WriteLine("| 版 | 全員生存 | 勝率 | 倒れた/戦 | 退避/戦 | 隣の弾き返し/戦 | 移動の追撃/戦 | " + string.Join(" | ", UnitNames) + " |");
            Console.WriteLine("|---|---|---|---|---|---|---|" + string.Concat(Enumerable.Repeat("---|", Units.Length)));
            foreach (var v in extra)
            {
                var a = Measure(MHane228, v, MainWave, Scales[0].Sc, 0, 1000);
                Console.WriteLine($"| {v.Tag} | {F1(a.Surv)} | {F1(a.Win)} | {F2(a.Per(a.Fell.Values.Sum()))} | {F2(a.Per(a.RetreatSwaps))} | {F2(a.Per(a.SpringGuard))} | {F2(a.Per(a.MoveShots))} | "
                    + string.Join(" | ", Units.Select(id => F1(100.0 * a.Fell.GetValueOrDefault(id) / a.N) + "%")) + " |");
            }
            Console.WriteLine();
        }

        // 参考: 雷（版の札の保持者 0 枚 → 版で1ビットも動かない）
        Console.WriteLine("## 参考 雷（ポンの席）—— V0 と VX2（全員生存 ／ 勝率）");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 版 | " + string.Join(" | ", WaveNames) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", WaveNames.Length)));
        for (int s = 0; s < Scales.Length; s++)
            foreach (string tag in new[] { "V0", "VX2" })
                Console.WriteLine($"| {Scales[s].Name} | {tag} | " + string.Join(" | ", Enumerable.Range(0, WaveNames.Length).Select(w =>
                {
                    var a = Measure(Thunder, VerOf(tag), w, Scales[s].Sc);
                    return $"{F1(a.Surv)} ／ {F1(a.Win)}";
                })) + " |");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");

        static UnitDef Raw(UnitDef d) => d.Id switch { "shio" => UnitCatalog.ShioA0, "hane" => UnitCatalog.HaneS0, "sero" => UnitCatalog.SeroC0, _ => d };
    }
}
