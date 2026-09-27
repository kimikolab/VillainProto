using System.Reflection;
using BattleCore;
using static Common;

// drift phase0 —— Q0-1〜Q0-5（第222期）。Q0-1 だけ V0 で戦闘を回す（盤面は1ビットも動かさない）。
static partial class DriftDiag
{
    static void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第222期 Phase 0 —— 移動軸の加速（`0 drift phase0`）");
        Console.WriteLine();

        // ---- Q0-1 ----
        Console.WriteLine("## Q0-1 移動の供給（V0 ＝ 今のシオ・ヨミ・seed 0..199）");
        Console.WriteLine();
        var benches = new List<(string, Formation)> { ("D1 ポン", BenchD1) };
        benches.AddRange(CompareRowsWith().Select(r => ("D2/D3 " + r.Name, r.F)));
        foreach (var (bn, f) in benches)
        {
            if (!f.Occupied().Any(o => o.Def.Id == "yomi")) continue;
            Console.WriteLine($"### {bn} —— {SeatsNamed(f)}");
            Console.WriteLine();
            Console.WriteLine("| 倍率 | 波 | 勝率 | ヨミが動かされた ／ 戦 | 出どころ（／ 戦） | 前へ突き出された ／ 戦 | T1 | T2 | T3 | T4 | T5 | T6 | 攻 20 到達（率 ／ 平均T） | 攻 30 到達（率 ／ 平均T） |");
            Console.WriteLine("|---|---|--:|--:|---|--:|--:|--:|--:|--:|--:|--:|---|---|");
            foreach (var (sn, sc) in Scales)
                for (int w = 0; w < WaveNames.Length; w++)
                {
                    var a = Measure(f, WaveOf(w, sc));
                    string src = string.Join(" ", a.YomiMovedBy.OrderByDescending(kv => kv.Value).Select(kv => $"{Short(kv.Key)} {a.Per(kv.Value):F2}"));
                    Console.WriteLine($"| {sn} | {WaveNames[w]} | {F1(a.Win)} | {F2(a.Per(a.YomiMoved))} | {src} | {F2(a.Per(a.YomiPushedFwd))} | {string.Join(" | ", Enumerable.Range(1, 6).Select(t => F2(a.Per(a.MovedTurn[t]))))} | {F1(100.0 * a.Reach20 / a.N)}% ／ {F2(a.Reach20 == 0 ? double.NaN : (double)a.Reach20Sum / a.Reach20)} | {F1(100.0 * a.Reach30 / a.N)}% ／ {F2(a.Reach30 == 0 ? double.NaN : (double)a.Reach30Sum / a.Reach30)} |");
                }
            Console.WriteLine();
        }
        Console.WriteLine("到達は「ヨミが振った一撃の攻撃力（`Attack.Amount`）が初めて 20 ／ 30 以上になったターン」。率の分母は全戦（届かない戦を含む）。");
        Console.WriteLine();

        // ---- Q0-2 ----
        Console.WriteLine("## Q0-2 「後ろ側の隣」（隣接かつより後ろの行・編成の5席）");
        Console.WriteLine();
        foreach (var sh in new[] { FormationShape.X, FormationShape.Diamond })
        {
            Console.WriteLine($"### {sh.Name}");
            Console.WriteLine();
            Console.WriteLine("| 枠 | 席 | 行 | 隣接（編成の席） | 後ろ側の隣（入れ替える相手の候補・席番号の順） |");
            Console.WriteLine("|---|---|---|---|---|");
            var play = sh.PlayableSlots;
            for (int i = 0; i < play.Count; i++)
            {
                int s = play[i];
                var adj = play.Where(b => b != s && sh.AreAdjacent(s, b)).ToList();
                var back = adj.Where(b => FormationRules.DepthOf(FormationRules.RowOf(b)) > FormationRules.DepthOf(FormationRules.RowOf(s))).OrderBy(b => b).ToList();
                Console.WriteLine($"| {i} | {FormationRules.SeatNames[s]} | {FormationRules.RowOf(s)} | {string.Join(" ", adj.Select(b => FormationRules.SeatNames[b]))} | {(back.Count == 0 ? "なし" : string.Join(" → ", back.Select(b => FormationRules.SeatNames[b])))} |");
            }
            Console.WriteLine();
        }

        // ---- Q0-3 ----
        Console.WriteLine("## Q0-3 入れ替えで反応する札（`OnMoved` / `OnAllyMoved` を上書きしている札・リフレクション）");
        Console.WriteLine();
        var holders = UnitCatalog.All.SelectMany(d => d.Traits.Select(t => (t, d.Name))).GroupBy(x => x.t).ToDictionary(g => g.Key, g => string.Join("・", g.Select(x => x.Name)));
        Console.WriteLine("| 札 | 上書き | 保持者（`UnitCatalog.All`） |");
        Console.WriteLine("|---|---|---|");
        foreach (TraitId id in Enum.GetValues<TraitId>())
        {
            Trait t;
            try { t = TraitCatalog.Get(id); } catch { continue; }
            var hooks = new[] { "OnMoved", "OnAllyMoved" }.Where(h =>
                t.GetType().GetMethod(h, BindingFlags.Public | BindingFlags.Instance)?.DeclaringType != typeof(Trait)).ToList();
            if (hooks.Count == 0) continue;
            Console.WriteLine($"| {id} | {string.Join(" / ", hooks)} | {holders.GetValueOrDefault(id, "—（保持者 0 枚）")} |");
        }
        Console.WriteLine();
        Console.WriteLine("engine が行（`Row`）を読む窓口（`SwapSlots` の後の席で効く）: 庇う・殉教（**前列の Guardian / Martyr だけが庇う**）／ 後備え（標的が前列でないとき）／ 標的の池（前列が生きている限り前列だけ）／ `SwapSlots` の中の混乱（既定オフ）と `HasFallenBack`（後ろへ動いた記録・狙撃が読む）。");
        Console.WriteLine();

        // ---- Q0-5 ----
        Console.WriteLine("## Q0-5 台の駒");
        Console.WriteLine();
        Console.WriteLine("| 台 | 駒（席・HP・攻・速・型・札） |");
        Console.WriteLine("|---|---|");
        foreach (var (bn, f) in benches)
            Console.WriteLine($"| {bn} | {string.Join(" ／ ", f.Occupied().Select(o => $"{FormationRules.SeatNames[f.Shape.PlayableSlots[o.Slot]]} {o.Def.Name} {o.Def.MaxHp}/{o.Def.Attack}/{o.Def.Speed}/{o.Def.Pattern}/[{string.Join(",", o.Def.Traits)}]"))} |");
        Console.WriteLine();
        Console.WriteLine("### 検証の波（敵）");
        Console.WriteLine();
        foreach (var (sn, sc) in Scales)
            for (int w = 0; w < WaveNames.Length; w++)
            {
                var foes = WaveOf(w, sc)();
                Console.WriteLine($"- {sn} {WaveNames[w]}: {foes.Count} 体・総HP {foes.Sum(u => u.MaxHp)}・総攻 {foes.Sum(u => u.Def.Attack)}・速 {string.Join("/", foes.Select(u => u.Def.Speed).Distinct().OrderByDescending(x => x))}");
            }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒");
    }
}
