using BattleCore;
using static Common;

// seroshio phase0 —— 第224期 Q0-1〜Q0-5（F0・H0 だけで回る）。
static partial class SeroShioDiag
{
    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第224期 Phase 0 —— セロの移動・シオの回復（`0 seroshio phase0`）");
        Console.WriteLine();
        Console.WriteLine($"seed 0..{Seeds - 1}。セロは F0（＝第223期 E2: `Evade` / `EvadeSwap` / `StatusArrow`）、シオは H0（規定 `Drifter` / `Regroup`）。");
        Console.WriteLine("回復の量は**実際に増えた HP**（溢れ・渇き・反転を除く）。溢れ ＝ 名目 − 増えた。");
        Console.WriteLine();

        // ---- Q0-5 台 ----
        Console.WriteLine("## Q0-5 台");
        Console.WriteLine();
        var benches = new List<(string Name, Formation F)>
        {
            ("S1 ポン（＝K-シオ）", S1),
            ("S2 ネル", S2),
            ("K-リリ", K(UnitCatalog.Lili)),
            ("K-ツギ", K(UnitCatalog.Tsugi)),
        };
        foreach (var (n, f) in benches) Console.WriteLine($"- {n}: {SeroDiag.SeatsNamed(f)}");
        var s4 = S4Rows();
        Console.WriteLine($"- S4（セロのいる `compare` の行・{s4.Count} 行）: " + string.Join(" ／ ", s4.Select(r => r.Name)));
        Console.WriteLine($"  - うちシオもいる行: {string.Join(" ／ ", s4.Where(r => r.F.Occupied().Any(o => o.Def.Id == "shio")).Select(r => r.Name).DefaultIfEmpty("なし"))}");
        var shioRows = CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id == "shio")).Select(r => (r.Name, F: OldTune(OldBasaHane(r.F)))).ToList();   // 第227期: バサ・ハネは旧に固定
        Console.WriteLine($"- シオのいる `compare` の行（{shioRows.Count} 行）: " + string.Join(" ／ ", shioRows.Select(r => r.Name)));
        Console.WriteLine();
        Console.WriteLine("S1 の隣（中央のシオの隣 4 枚）: " + string.Join("・", S1.Occupied().Where(o => o.Def.Id != "shio").Select(o => o.Def.Name)) + "。S2 の隣: "
            + string.Join("・", Neighbors(S2, "shio")) + "。セロの隣: S1 " + string.Join("・", Neighbors(S1, "sero")) + " ／ S2 " + string.Join("・", Neighbors(S2, "sero")) + "。");
        Console.WriteLine();

        // ---- Q0-1 シオの回復の実数 ----
        Console.WriteLine("## Q0-1 シオの回復の実数（H0・F0。／ 戦）");
        Console.WriteLine();
        Console.WriteLine("出どころ ＝ 移り木を起こした移動を動かした駒。「シオの手番」＝ `Regroup` の入れ替え。溢れ ＝ 名目 − 実際に増えた。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 勝率 | 組み替え | 移り木 回 | 名目 | 増えた | 溢れ（渇き・反転を含む） | " + string.Join(" | ", SrcNames.Take(6).Select(n => n + "（増えた／名目）")) + " |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|" + string.Concat(Enumerable.Repeat("---|", 6)));
        var shioBenches = new List<(string, Formation)> { ("S1 ポン", S1), ("S2 ネル", S2) };
        shioBenches.AddRange(shioRows.Select(r => ("compare " + r.Name, Apply(r.F, F0, H0))));
        foreach (var (n, f0) in shioBenches)
        {
            var f = Apply(f0, F0, H0);
            for (int s = 0; s < Scales.Length; s++)
            {
                var per = Enumerable.Range(0, WaveNames.Length).Select(w => Measure(f, WaveOf(w, Scales[s].Sc))).ToArray();
                for (int w = 0; w < WaveNames.Length; w++) Row(n, s, WaveNames[w], per[w]);
                Row(n, s, "**第2〜5波**", Sum(w => per[w], new[] { 0, 1, 2, 3 }));
            }
        }
        Console.WriteLine();

        void Row(string n, int s, string wn, Agg a) =>
            Console.WriteLine($"| {n} | {Scales[s].Name} | {wn} | {F1s(a.Win)} | {F2s(a.Per(a.RgSwaps))} | {F2s(a.Per(a.DrFires))} | {F1s(a.Per(a.DrNom))} | {F1s(a.Per(a.DrGain))} | {F1s(a.Per(a.DrNom - a.DrGain))} | "
                + string.Join(" | ", Enumerable.Range(0, 6).Select(k => $"{F1s(a.Per(a.DrBySrc[k]))}／{F1s(a.Per(a.DrNomBySrc[k]))}")) + " |");

        // ---- Q0-2 リリ・ツギ ----
        Console.WriteLine("## Q0-2 リリ・ツギの実数（規定の駒・`compare` の行・115/115 と 150/115。／ 戦）");
        Console.WriteLine();
        Console.WriteLine("リリ ＝ リリが回復させた味方の HP の増分（台本の `Heal`・溢れは含まない）。ツギ ＝ 味方に書いた破片（`ArmorOut`）と、板の印がある間に減った破片（`PlankSoaked`・味方全員の和）。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 倍率 | 第2〜5波 勝率 | 回復 ／ 破片(与) | 破片が減った | その駒の与ダメ | 九/新兵 勝率 | 回復 ／ 破片(与) | 破片が減った | 与ダメ |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (name, f) in CompareBuilds().Where(r => r.F.Occupied().Any(o => o.Def.Id is "lili" or "tsugi")).Select(r => (r.Name, F: OldTune(r.F))))
        {
            bool lili = f.Occupied().Any(o => o.Def.Id == "lili");
            string id = lili ? "lili" : "tsugi";
            for (int s = 0; s < Scales.Length; s++)
            {
                var per = Enumerable.Range(0, WaveNames.Length).Select(w => Measure(f, WaveOf(w, Scales[s].Sc))).ToArray();
                var main = Sum(w => per[w], new[] { 0, 1, 2, 3 });
                var nine = per[4];
                string G(Agg a) => lili ? F1s(a.Per(a.LiliHeal)) : F1s(a.Per(a.TsugiArmorOut));
                string L(Agg a) => lili ? "—" : F1s(a.Per(a.PlankSoaked));
                Console.WriteLine($"| {name}（{(lili ? "リリ" : "ツギ")}） | {Scales[s].Name} | {F1s(main.Win)} | {G(main)} | {L(main)} | {F1s(main.DealtOf(id))} | {F1s(nine.Win)} | {G(nine)} | {L(nine)} | {F1s(nine.DealtOf(id))} |");
            }
        }
        Console.WriteLine();

        // ---- Q0-3 セロの移動 ----
        Console.WriteLine("## Q0-3 セロが動かされた回数（F0・H0。／ 戦）");
        Console.WriteLine();
        Console.WriteLine("「回避」はセロ自身の入れ替え。届いた率 ／ 平均ターン は、動かされた累計が n 回に届いた戦の割合とそのターン（台本の `Move`）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 動かされた | " + string.Join(" | ", SeroDiag.MoveSrcNames) + " | 2 回 | 3 回 | 4 回 | 6 回 | 7 回 | 10 回 |");
        Console.WriteLine("|---|---|---|--:|" + string.Concat(Enumerable.Repeat("--:|", 7)) + string.Concat(Enumerable.Repeat("---|", 6)));
        var seroBenches = new List<(string, Formation)> { ("S1 ポン", S1), ("S2 ネル", S2) };
        foreach (var (n, f0) in seroBenches)
        {
            var f = Apply(f0, F0, H0);
            for (int s = 0; s < Scales.Length; s++)
            {
                var per = Enumerable.Range(0, WaveNames.Length).Select(w => Measure(f, WaveOf(w, Scales[s].Sc))).ToArray();
                foreach (var (gn, ws) in Groups) MoveRow(n, s, gn, Sum(w => per[w], ws));
            }
        }
        {
            for (int s = 0; s < Scales.Length; s++)
            {
                var all = new Agg();
                foreach (var (_, f0) in s4) for (int w = 0; w < 4; w++) all.Merge(Measure(Apply(f0, F0, H0), WaveOf(w, Scales[s].Sc)));
                MoveRow($"S4 {s4.Count} 行の和", s, "本編 第2〜5波", all);
            }
        }
        Console.WriteLine();
        Console.WriteLine("ターン別（S1・150/115・九/新兵。1戦あたりの動かされた回数）:");
        Console.WriteLine();
        {
            var a = Measure(Apply(S1, F0, H0), WaveOf(4, Scales[1].Sc));
            Console.WriteLine("| T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8 |");
            Console.WriteLine("|--:|--:|--:|--:|--:|--:|--:|--:|");
            Console.WriteLine("| " + string.Join(" | ", Enumerable.Range(1, 8).Select(t => F2s(a.Per(a.MovesByTurn[t])))) + " |");
            Console.WriteLine();
            Console.WriteLine($"決着T（勝った戦）{F2s(a.WinT)}・段1〜3（F0 ＝ 3/6/10）: {a.StageText(1)} ・ {a.StageText(2)} ・ {a.StageText(3)}。");
        }
        Console.WriteLine();

        void MoveRow(string n, int s, string gn, Agg a) =>
            Console.WriteLine($"| {n} | {Scales[s].Name} | {gn} | {F2s(a.Per(a.SeroMoved))} | {string.Join(" | ", a.MoveSrc.Select(x => F2s(a.Per(x))))} | "
                + $"{a.Reach10Text(2)} | {a.Reach10Text(3)} | {a.Reach10Text(4)} | {a.Reach10Text(6)} | {a.Reach10Text(7)} | {a.Reach10Text(10)} |");

        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒");
    }

    static IEnumerable<string> Neighbors(Formation f, string id)
    {
        var occ = f.Occupied().ToList();
        var me = occ.First(o => o.Def.Id == id);
        int ms = f.Shape.PlayableSlots[me.Slot];
        return occ.Where(o => o.Def.Id != id && FormationRules.AreAdjacent(ms, f.Shape.PlayableSlots[o.Slot])).Select(o => o.Def.Name);
    }
}
