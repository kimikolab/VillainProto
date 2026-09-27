using BattleCore;
using static Common;

// drift run —— 表A〜E（第222期）。台 D1〜D4 × 波（第2〜5波 ＋ 九/新兵・九/農兵）× 版 × 倍率（115/115・150/115）。
static partial class DriftDiag
{
    static partial void RunImpl(string arg)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第222期 —— 移動軸の加速（`0 drift run`）");
        Console.WriteLine();
        Console.WriteLine($"seed 0..{Seeds - 1}。版: V0 今のまま ／ V1 シオに手番 ／ V2 ヨミ薙ぎ（閾値30）／ V3 両方 ／ V3b 両方・閾値20。");
        Console.WriteLine();

        var benches = new List<(string Name, Formation F)> { ("D1 ポン", BenchD1) };
        foreach (var r in CompareRowsWith()) benches.Add(((r.Name.StartsWith("移動改") ? "D2 " : "D3 ") + r.Name, r.F));
        benches = benches.OrderBy(b => b.Name).ToList();
        var d4 = PickD4();
        benches.Add(("D4 席を選んだ", d4));
        Console.WriteLine("## 台");
        Console.WriteLine();
        foreach (var (n, f) in benches) Console.WriteLine($"- {n}: {SeatsNamed(f)}");
        Console.WriteLine();

        int B = benches.Count, S = Scales.Length, W = WaveNames.Length, V = Versions.Length;
        var res = new Agg[B, S, W, V];
        var jobs = new List<(int b, int s, int w, int v)>();
        for (int b = 0; b < B; b++) for (int s = 0; s < S; s++) for (int w = 0; w < W; w++) for (int v = 0; v < V; v++) jobs.Add((b, s, w, v));
        foreach (var (b, s, w, v) in jobs)
            res[b, s, w, v] = Measure(Apply(benches[b].F, Versions[v].Shio, Versions[v].Yomi), WaveOf(w, Scales[s].Sc));

        Agg Sum(int b, int s, IEnumerable<int> ws, int v) { var a = new Agg(); foreach (int w in ws) a.Merge(res[b, s, w, v]); return a; }
        var mainW = new[] { 0, 1, 2, 3 };
        var nineW = new[] { 4, 5 };
        var groups = new (string Name, int[] Ws)[] { ("本編 第2〜5波", mainW), ("九/新兵", new[] { 4 }), ("九/農兵", new[] { 5 }) };

        // ---- 表A ----
        Console.WriteLine("## 表A0 —— 要約（勝率 ／ 全員生存）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | " + string.Join(" | ", Versions.Select(v => v.Tag)) + " | V1−V0 | V2−V0 | V3−V0 | V3b−V3 |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("--:|", V + 4)));
        for (int b = 0; b < B; b++) for (int s = 0; s < S; s++) foreach (var (gn, ws) in groups)
        {
            var a = Enumerable.Range(0, V).Select(v => Sum(b, s, ws, v)).ToArray();
            Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} | {gn} | {string.Join(" | ", a.Select(x => $"{F1(x.Win)} ／ {F1(x.Surv)}"))} | {D1(a[1].Win - a[0].Win)} | {D1(a[2].Win - a[0].Win)} | {D1(a[3].Win - a[0].Win)} | {D1(a[4].Win - a[3].Win)} |");
        }
        Console.WriteLine();

        Console.WriteLine("## 表A —— 台 × 波 × 版 × 倍率（勝率 ／ 全員生存 ／ 勝った戦の決着T ／ 落ちた駒: 落ちた率/平均ターン）");
        Console.WriteLine();
        for (int b = 0; b < B; b++)
        {
            var units = benches[b].F.Occupied().Select(o => (o.Def.Id, o.Def.Name)).ToList();
            Console.WriteLine($"### {benches[b].Name}");
            Console.WriteLine();
            Console.WriteLine("| 倍率 | 波 | 版 | 勝率 | 全員生存 | 決着T | 落ちた駒 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|---|");
            for (int s = 0; s < S; s++) for (int w = 0; w < W; w++) for (int v = 0; v < V; v++)
            {
                var a = res[b, s, w, v];
                Console.WriteLine($"| {Scales[s].Name} | {WaveNames[w]} | {Versions[v].Tag} | {F1(a.Win)} | {F1(a.Surv)} | {F2(a.WinT)} | {a.FellText(units)} |");
            }
            Console.WriteLine();
        }

        // ---- 表B ----
        Console.WriteLine("## 表B —— 移動の帳簿（ヨミ）");
        Console.WriteLine();
        Console.WriteLine("攻撃力の推移はターン頭の値（`StatSnapshot`・生きているときだけ）。到達は振った一撃の攻撃力が初めて 20 ／ 30 以上になったターン（率の分母は全戦）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 版 | 動かされた ／ 戦 | 出どころ（／ 戦） | 突き出し ／ 戦 | 攻 T1 | T2 | T3 | T4 | T5 | T6 | 20 到達 | 30 到達 |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|--:|--:|--:|--:|--:|--:|---|---|");
        for (int b = 0; b < B; b++) for (int s = 0; s < S; s++) foreach (var (gn, ws) in groups) for (int v = 0; v < V; v++)
        {
            var a = Sum(b, s, ws, v);
            string src = string.Join(" ", a.YomiMovedBy.OrderByDescending(kv => kv.Value).Select(kv => $"{Short(kv.Key)} {a.Per(kv.Value):F2}"));
            string atk = string.Join(" | ", Enumerable.Range(1, 6).Select(t => a.AtkCnt[t] == 0 ? "—" : ((double)a.AtkSum[t] / a.AtkCnt[t]).ToString("F1")));
            Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} | {gn} | {Versions[v].Tag} | {F2(a.Per(a.YomiMoved))} | {src} | {F2(a.Per(a.YomiPushedFwd))} | {atk} | {F1(100.0 * a.Reach20 / a.N)}% ／ {F2(a.Reach20 == 0 ? double.NaN : (double)a.Reach20Sum / a.Reach20)}T | {F1(100.0 * a.Reach30 / a.N)}% ／ {F2(a.Reach30 == 0 ? double.NaN : (double)a.Reach30Sum / a.Reach30)}T |");
        }
        Console.WriteLine();

        // ---- 表C ----
        Console.WriteLine("## 表C —— シオの手番（V1・V3・V3b。／ 戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 版 | 手番 | 入れ替えた | うち自分を下げた | 相手なし | 全員満タン | 2体の HP 純増 | 受けた強化 | 入れ替え中に振られた | 下げた駒 | 押し出した駒 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|---|---|");
        for (int b = 0; b < B; b++) for (int s = 0; s < S; s++) foreach (var (gn, ws) in groups) foreach (int v in new[] { 1, 3, 4 })
        {
            var a = Sum(b, s, ws, v);
            if (a.ShioSkillTurns == 0) continue;
            string lo = string.Join(" ", a.Lowered.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {a.Per(kv.Value):F2}"));
            string pu = string.Join(" ", a.Pushed.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {a.Per(kv.Value):F2}"));
            Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} | {gn} | {Versions[v].Tag} | {F2(a.Per(a.ShioSkillTurns))} | {F2(a.Per(a.RgSwaps))} | {F2(a.Per(a.RgSelf))} | {F2(a.Per(a.RgStuck))} | {F2(a.Per(a.RgAllFull))} | {F1(a.Per(a.RgHeal))} | {F1(a.Per(a.RgWhet))} | {F2(a.Per(a.RgReaderSwings))} | {lo} | {pu} |");
        }
        Console.WriteLine();

        // ---- 表D ----
        Console.WriteLine("## 表D —— ヨミの薙ぎ（／ 戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 版 | 単体で振った | 薙いだ | 1振りで当たった数（薙ぎ） | 与ダメ 単体の分 | 与ダメ 薙ぎの分 | 与ダメ 計 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|--:|");
        for (int b = 0; b < B; b++) for (int s = 0; s < S; s++) foreach (var (gn, ws) in groups) for (int v = 0; v < V; v++)
        {
            var a = Sum(b, s, ws, v);
            if (a.YomiBattles == 0) continue;
            Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} | {gn} | {Versions[v].Tag} | {F2(a.Per(a.YAtkSingle))} | {F2(a.Per(a.YAtkSweep))} | {F2(a.YAtkSweep == 0 ? double.NaN : (double)a.YHitsSweep / a.YAtkSweep)} | {F1(a.Per(a.YDmgSingle))} | {F1(a.Per(a.YDmgSweep))} | {F1(a.Per(a.YDmgSingle + a.YDmgSweep))} |");
        }
        Console.WriteLine();

        // ---- 表E ----
        Console.WriteLine("## 表E —— 庇い（ガルド）");
        Console.WriteLine();
        Console.WriteLine("「前列にいない」ターンはガルドが生きていて前列にいないターン（庇いは前列のガルドにしか立たない）。被ダメは敵の出どころで、ガルド以外の味方が受けた量。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 波 | 版 | シオに前列から外された ／ 戦 | 庇った ／ 戦 | 前列にいたT ／ 戦 | いないT ／ 戦 | 味方の被ダメ／T（前列にいる間） | 同（いない間） |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|--:|");
        for (int b = 0; b < B; b++)
        {
            if (!benches[b].F.Occupied().Any(o => o.Def.Id == "gald")) continue;
            for (int s = 0; s < S; s++) foreach (var (gn, ws) in groups) for (int v = 0; v < V; v++)
            {
                var a = Sum(b, s, ws, v);
                Console.WriteLine($"| {benches[b].Name} | {Scales[s].Name} | {gn} | {Versions[v].Tag} | {F2(a.Per(a.GaldOutByShio))} | {F2(a.Per(a.GaldGuards))} | {F2(a.Per(a.TurnsGaldFront))} | {F2(a.Per(a.TurnsGaldAway))} | {F1(a.TurnsGaldFront == 0 ? double.NaN : (double)a.DmgGaldFront / a.TurnsGaldFront)} | {F1(a.TurnsGaldAway == 0 ? double.NaN : (double)a.DmgGaldAway / a.TurnsGaldAway)} |");
            }
        }
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F0} 秒");
    }

    /// <summary>
    /// D4: D1 の5枚の席の総当たり（120 通り）。**V3 × 九/新兵・九/農兵 × 敵 150/115 × seed 1000..1049** の勝ち数が最大の並び
    /// （同値は 全員生存 → 決着T の短い方 → 列挙順）。測る seed（0..199）とは別の帯で選ぶ。
    /// </summary>
    static List<(Formation F, int W, int Sv, long T)>? _d4;
    internal static Formation PickD4(bool print = true)
    {
        if (_d4 is null)
        {
            var sc = Scales[1].Sc;
            var members = BenchD1.Occupied().Select(o => o.Def).Select(d => d.Id == "shio" ? ShioReg : d.Id == "yomi" ? YomiS30 : d).ToList();
            var list = new List<(Formation, int, int, long)>();
            foreach (var p in Permute(members))
            {
                var f = Formation.Build(front1: p[0], front3: p[1], center: p[2], back1: p[3], back3: p[4]);
                int w = 0, sv = 0; long t = 0;
                foreach (int wv in new[] { 4, 5 })
                {
                    var q = Quick(f, WaveOf(wv, sc), 1000, 50);
                    w += q.Wins; sv += q.Surv; t += q.WinT;
                }
                list.Add((f, w, sv, t));
            }
            _d4 = list.Select((x, i) => (x, i)).OrderByDescending(z => z.x.Item2).ThenByDescending(z => z.x.Item3)
                      .ThenBy(z => z.x.Item2 == 0 ? long.MaxValue : z.x.Item4 * 1000 / z.x.Item2).ThenBy(z => z.i).Select(z => z.x).ToList();
            if (print)
            {
                var top = _d4[0];
                int ties = _d4.Count(x => x.W == top.W);
                int ties2 = _d4.Count(x => x.W == top.W && x.Sv == top.Sv);
                Console.WriteLine("## D4 の席（V3 × 九/新兵・九/農兵 × 敵 150/115 × seed 1000..1049・120 通り）");
                Console.WriteLine();
                Console.WriteLine($"勝ち数が最大の並びと同値の並び: 勝ち数 {ties} 通り ／ 勝ち数と全員生存 {ties2} 通り。");
                Console.WriteLine();
                Console.WriteLine("| 順位 | 席 | 勝ち数/100 | 全員生存/100 |");
                Console.WriteLine("|--:|---|--:|--:|");
                for (int i = 0; i < 8; i++) Console.WriteLine($"| {i + 1} | {SeatsNamed(_d4[i].F)} | {_d4[i].W} | {_d4[i].Sv} |");
                var pon = _d4.FindIndex(x => SeatsNamed(x.F) == SeatsNamed(Apply(BenchD1, ShioReg, YomiS30)));
                Console.WriteLine($"| {pon + 1}（ポンの席） | {SeatsNamed(_d4[pon].F)} | {_d4[pon].W} | {_d4[pon].Sv} |");
                Console.WriteLine();
            }
        }
        var best = _d4[0].F;
        // 版の差し替えは Apply が Id で行うので、ここでは規定の駒に戻しておく。
        return Apply(best, UnitCatalog.Shio, UnitCatalog.Yomi);
    }
}
