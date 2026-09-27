using BattleCore;
using static Common;

// seroshio run —— 第224期 表A〜E。台 × 波（第2〜5波 ＋ 九/新兵）× 版 × 倍率（115/115・150/115）・seed 0..199。
static partial class SeroShioDiag
{
    internal static (string Tag, UnitDef Def)[] SeroVersions => new (string, UnitDef)[]
    {
        ("E0 規定", SeroDiag.E0), ("F0", F0), ("F1", F1!), ("F2", F2!), ("F3", F3!),
    };
    internal static (string Tag, UnitDef Def)[] ShioVersions => new (string, UnitDef)[] { ("H0", H0), ("H1", H1!), ("H2", H2!) };

    /// <summary>1台 × 1版の組の、倍率 × 波の測定。</summary>
    sealed class Cell
    {
        public readonly Agg[,] PerWave = new Agg[2, 5];
        public Agg Group(int s, int g) => Sum(w => PerWave[s, w], Groups[g].Ws);
    }

    static Cell MeasureCell(Formation f)
    {
        var c = new Cell();
        for (int s = 0; s < Scales.Length; s++)
            for (int w = 0; w < WaveNames.Length; w++)
                c.PerWave[s, w] = Measure(f, WaveOf(w, Scales[s].Sc));
        return c;
    }

    static partial void RunImpl(string arg)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第224期 —— セロの火力を移動に寄せる／シオの回復量（`0 seroshio run`）");
        Console.WriteLine();
        Console.WriteLine($"seed 0..{Seeds - 1}。波は本編の第2〜5波 ＋ 検証・九 / 新兵。倍率は 115/115 と 150/115。");
        Console.WriteLine("セロ: **E0 規定**（参考）／ F0 ＝ 第223期 E2 ／ F1 段 2/4/7 ／ F2 ＋ 動かされるたび +2 ／ F3 ＋ 段3 で 7 本。");
        Console.WriteLine("シオ: H0 規定（移り木 +10）／ H1 移り木を最大HPの 20% ／ H2 ＋ 下げた味方を最大HPの 20%（シオ自身を下げたときは癒さない）。");
        Console.WriteLine();

        var sv = SeroVersions; var hv = ShioVersions;
        var s4 = S4Rows();
        var benches = new List<(string Name, Formation F)> { ("S1 ポン", S1), ("S2 ネル", S2) };
        Console.WriteLine("## 台");
        Console.WriteLine();
        foreach (var (n, f) in benches) Console.WriteLine($"- {n}: {SeroDiag.SeatsNamed(f)}");
        Console.WriteLine($"- K-リリ: {SeroDiag.SeatsNamed(K(UnitCatalog.Lili))}");
        Console.WriteLine($"- K-ツギ: {SeroDiag.SeatsNamed(K(UnitCatalog.Tsugi))}");
        Console.WriteLine($"- S4: セロのいる `compare` の {s4.Count} 行（席はそのまま）");
        Console.WriteLine();

        // ---- 測る ----
        var grid = new Dictionary<(string B, string Sv, string Hv), Cell>();
        foreach (var (bn, bf) in benches)
            foreach (var (st, sd) in sv) foreach (var (ht, hd) in hv)
                grid[(bn, st, ht)] = MeasureCell(Apply(bf, sd, hd));
        foreach (var (hn, healer) in new[] { ("K-リリ", UnitCatalog.Lili), ("K-ツギ", UnitCatalog.Tsugi) })
            foreach (var st in new[] { "F0", "F3" })
                grid[(hn, st, "—")] = MeasureCell(Apply(K(healer), sv.First(x => x.Tag == st).Def, H0));
        foreach (var (rn, rf) in s4)
        {
            bool shio = rf.Occupied().Any(o => o.Def.Id == "shio");
            foreach (var (st, sd) in sv)
                foreach (var (ht, hd) in hv)
                    if (ht == "H0" || (shio && st is "F0" or "F3"))
                        grid[("S4 " + rn, st, ht)] = MeasureCell(Apply(rf, sd, hd));
        }

        string Fell(Agg a, string id) => a.Fell.GetValueOrDefault(id) == 0 ? "0%" : $"{100.0 * a.Fell[id] / a.N:F0}%";

        // ---- 表A ----
        Console.WriteLine("## 表A —— セロの版（シオ H0）");
        Console.WriteLine();
        Console.WriteLine("勝率 ／ 全員生存 ／ 決着T（勝った戦）・セロの与ダメ（／戦）と台の中の与ダメの順位（5 枚）・ヨミの与ダメ・段2 と 段3 に届いた割合 ／ 平均ターン・セロが倒れた割合。");
        Console.WriteLine();
        foreach (var (bn, _) in benches)
        {
            Console.WriteLine($"### {bn}");
            Console.WriteLine();
            Console.WriteLine("| 倍率 | 波 | 版 | 勝率 | 全員生存 | 決着T | セロ 与ダメ | 順位 | ヨミ 与ダメ | 段2 | 段3 | セロ倒れ |");
            Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|---|---|--:|");
            for (int s = 0; s < 2; s++) for (int g = 0; g < Groups.Length; g++) foreach (var (st, _) in sv)
            {
                var a = grid[(bn, st, "H0")].Group(s, g);
                Console.WriteLine($"| {Scales[s].Name} | {Groups[g].Name} | {st} | {F1s(a.Win)} | {F1s(a.Surv)} | {F2s(a.WinT)} | {F1s(a.DealtOf("sero"))} | {a.RankOf("sero")} | {F1s(a.DealtOf("yomi"))} | {a.StageText(2)} | {a.StageText(3)} | {Fell(a, "sero")} |");
            }
            Console.WriteLine();
        }
        Console.WriteLine("### 波ごとの勝率 ／ 全員生存（シオ H0）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | " + string.Join(" | ", WaveNames) + " |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", WaveNames.Length)));
        foreach (var (bn, _) in benches) for (int s = 0; s < 2; s++) foreach (var (st, _) in sv)
        {
            var c = grid[(bn, st, "H0")];
            Console.WriteLine($"| {bn} | {Scales[s].Name} | {st} | " + string.Join(" | ", Enumerable.Range(0, 5).Select(w => $"{F1s(c.PerWave[s, w].Win)} ／ {F1s(c.PerWave[s, w].Surv)}")) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("### S4（セロのいる `compare` の 12 行・シオ H0）");
        Console.WriteLine();
        Console.WriteLine("平均の勝率 ／ 全員生存・セロの与ダメ（12 行の平均）・順位の平均・段2 に届いた割合の平均。");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | " + string.Join(" | ", sv.Select(v => v.Tag)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", sv.Length)));
        for (int s = 0; s < 2; s++) for (int g = 0; g < Groups.Length; g++)
        {
            var cells = sv.Select(v =>
            {
                var aa = s4.Select(r => grid[("S4 " + r.Name, v.Tag, "H0")].Group(s, g)).ToList();
                return $"{F1s(aa.Average(a => a.Win))} ／ {F1s(aa.Average(a => a.Surv))} ・ 与 {F1s(aa.Average(a => a.DealtOf("sero")))} ・ 順 {aa.Average(a => a.RankOf("sero")):F1} ・ 段2 {aa.Average(a => 100.0 * a.StageReach[2] / a.N):F0}%";
            });
            Console.WriteLine($"| {Scales[s].Name} | {Groups[g].Name} | {string.Join(" | ", cells)} |");
        }
        Console.WriteLine();
        Console.WriteLine("行ごとの第2〜5波の勝率（E0 規定 → F0 → F1 → F2 → F3）:");
        Console.WriteLine();
        Console.WriteLine("| 行 | 115/115 | 150/115 | F3 − F0 (115) | F3 − E0 (115) |");
        Console.WriteLine("|---|---|---|--:|--:|");
        foreach (var (rn, _) in s4)
        {
            string Line(int s) => string.Join(" → ", sv.Select(v => F1s(grid[("S4 " + rn, v.Tag, "H0")].Group(s, 0).Win)));
            double w(string t) => grid[("S4 " + rn, t, "H0")].Group(0, 0).Win;
            Console.WriteLine($"| {rn} | {Line(0)} | {Line(1)} | {D1(w("F3") - w("F0"))} | {D1(w("F3") - w("E0 規定"))} |");
        }
        Console.WriteLine();

        // ---- 表B ----
        Console.WriteLine("## 表B —— セロの与ダメの内訳（シオ H0。／ 戦）");
        Console.WriteLine();
        Console.WriteLine("手番 ＝ 計 − 追い撃ち − 乱れ撃ち。状態の矢の刻み ＝ 矢で積んだ毒の層が刻んだ名目（計には入らない・`ArrowTickDealt`）。乱れ撃ちは手番の数 ／ 矢の数。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 版 | 計 | 手番 | 追い撃ち | 乱れ撃ち | 矢の刻み | 乱れ撃ち 手番 ／ 矢 | 避けた |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|---|--:|");
        var bS4 = s4.Select(r => "S4 " + r.Name).ToList();
        foreach (var bn in benches.Select(b => b.Name).Append("S4 12 行の和"))
            for (int s = 0; s < 2; s++) for (int g = 0; g < Groups.Length; g++) foreach (var (st, _) in sv.Skip(1))
            {
                Agg a;
                if (bn.StartsWith("S4 12")) { a = new Agg(); foreach (var r in bS4) a.Merge(grid[(r, st, "H0")].Group(s, g)); }
                else a = grid[(bn, st, "H0")].Group(s, g);
                double turn = a.Per(a.SeroDealt - a.RipDealt - a.BarDealt);
                Console.WriteLine($"| {bn} | {Scales[s].Name} | {Groups[g].Name} | {st} | {F1s(a.Per(a.SeroDealt))} | {F1s(turn)} | {F1s(a.Per(a.RipDealt))} | {F1s(a.Per(a.BarDealt))} | {F1s(a.Per(a.ArrowTick))} | {F2s(a.Per(a.Barrages))} ／ {F2s(a.Per(a.Arrows))} | {F2s(a.Per(a.Evades))} |");
            }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C —— シオの版（セロ F3）");
        Console.WriteLine();
        Console.WriteLine("回復は実際に増えた HP（／戦）。溢れ ＝ 名目 − 増えた（渇き・反転を含む）。自分を下げた ＝ 組み替えでシオ自身を下げた回数（H2 の手当ては出ない）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 版 | 勝率 | 全員生存 | 決着T | 移り木 | うちシオの手番 | 手当て | 計 | 溢れ | 組み替え ／ 自分 | シオ倒れ | ヨミ 与ダメ | セロ 与ダメ |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|---|--:|--:|--:|");
        var cBenches = benches.Select(b => b.Name).Concat(s4.Where(r => r.F.Occupied().Any(o => o.Def.Id == "shio")).Select(r => "S4 " + r.Name)).ToList();
        foreach (var bn in cBenches) for (int s = 0; s < 2; s++) for (int g = 0; g < Groups.Length; g++) foreach (var (ht, _) in hv)
        {
            var a = grid[(bn, "F3", ht)].Group(s, g);
            Console.WriteLine($"| {bn} | {Scales[s].Name} | {Groups[g].Name} | {ht} | {F1s(a.Win)} | {F1s(a.Surv)} | {F2s(a.WinT)} | {F1s(a.Per(a.DrGain))} | {F1s(a.Per(a.DrBySrc[0]))} | {F1s(a.Per(a.TendGain))} | **{F1s(a.Per(a.ShioHeal))}** | {F1s(a.Per(a.DrNom + a.TendNom - a.ShioHeal))} | {F2s(a.Per(a.RgSwaps))} ／ {F2s(a.Per(a.RgSelf))} | {Fell(a, "shio")} | {F1s(a.DealtOf("yomi"))} | {F1s(a.DealtOf("sero"))} |");
        }
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D —— 物差し: K-シオ（H0〜H2）・K-リリ・K-ツギ × セロ F0 ／ F3");
        Console.WriteLine();
        Console.WriteLine("回復の総量 ＝ シオ: 移り木 ＋ 手当て ／ リリ: リリが回復させた HP（台本の `Heal`）／ ツギ: 破片(与)（`ArmorOut`）と、板の印がある間に減った破片（受け止めた量）。すべて実際に増えた量で溢れは含まない。");
        Console.WriteLine();
        Console.WriteLine("| 倍率 | 波 | セロ | 台 | 勝率 | 全員生存 | 回復の総量 | 受け止めた（ツギ） | その駒の与ダメ | ヨミ 与ダメ | セロ 与ダメ | ガルド倒れ |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int s = 0; s < 2; s++) for (int g = 0; g < Groups.Length; g++) foreach (var st in new[] { "F0", "F3" })
        {
            foreach (var (ht, _) in hv)
            {
                var a = grid[("S1 ポン", st, ht)].Group(s, g);
                Console.WriteLine($"| {Scales[s].Name} | {Groups[g].Name} | {st} | K-シオ {ht} | {F1s(a.Win)} | {F1s(a.Surv)} | {F1s(a.Per(a.ShioHeal))} | — | {F1s(a.DealtOf("shio"))} | {F1s(a.DealtOf("yomi"))} | {F1s(a.DealtOf("sero"))} | {Fell(a, "gald")} |");
            }
            {
                var a = grid[("K-リリ", st, "—")].Group(s, g);
                Console.WriteLine($"| {Scales[s].Name} | {Groups[g].Name} | {st} | K-リリ | {F1s(a.Win)} | {F1s(a.Surv)} | {F1s(a.Per(a.LiliHeal))} | — | {F1s(a.DealtOf("lili"))} | {F1s(a.DealtOf("yomi"))} | {F1s(a.DealtOf("sero"))} | {Fell(a, "gald")} |");
            }
            {
                var a = grid[("K-ツギ", st, "—")].Group(s, g);
                Console.WriteLine($"| {Scales[s].Name} | {Groups[g].Name} | {st} | K-ツギ | {F1s(a.Win)} | {F1s(a.Surv)} | {F1s(a.Per(a.TsugiArmorOut))} | {F1s(a.Per(a.PlankSoaked))} | {F1s(a.DealtOf("tsugi"))} | {F1s(a.DealtOf("yomi"))} | {F1s(a.DealtOf("sero"))} | {Fell(a, "gald")} |");
            }
        }
        Console.WriteLine();

        // ---- 表E ----
        Console.WriteLine("## 表E —— 組み合わせ（規定の候補の組）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | E0×H0（今の規定） | F0×H0 | F3×H0 | F3×H1 | F3×H2 | F3×H2 − F0×H0 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|--:|");
        foreach (var bn in cBenches) for (int s = 0; s < 2; s++) for (int g = 0; g < Groups.Length; g++)
        {
            string C(string st, string ht) { var a = grid[(bn, st, ht)].Group(s, g); return $"{F1s(a.Win)} ／ {F1s(a.Surv)}"; }
            double W(string st, string ht) => grid[(bn, st, ht)].Group(s, g).Win;
            string e0 = grid.ContainsKey((bn, "E0 規定", "H0")) ? C("E0 規定", "H0") : "—";
            Console.WriteLine($"| {bn} | {Scales[s].Name} | {Groups[g].Name} | {e0} | {C("F0", "H0")} | {C("F3", "H0")} | {C("F3", "H1")} | {C("F3", "H2")} | {D1(W("F3", "H2") - W("F0", "H0"))} |");
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒");
    }
}
