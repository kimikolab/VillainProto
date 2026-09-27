using BattleCore;
using static Common;

// spring phase0 —— Q0-1 経路の並べ替え ／ Q0-3 行の変わり方 ／ Q0-4 ハネの被弾 ／ Q0-5 止まり続け（第228期・H0 ＝ 前段の規定だけで回る）。
static partial class SpringDiag
{
    /// <summary>X 字の席の名前。</summary>
    static string Seat(int s) => FormationRules.SeatNames[s];

    /// <summary>
    /// 吹っ飛ばしの並べ替え（§3・盤の外の純粋な関数）: 経路の上の「駒のいる席」の並びはそのまま、中の駒を「A を最後尾へ回した順」に。
    /// 入力は経路の席の順（前→後）と各席の駒（null ＝ 空席）。
    /// </summary>
    internal static string?[] Rotate(string?[] lane, int aIndex)
    {
        var occ = Enumerable.Range(0, lane.Length).Where(i => lane[i] is not null).ToList();
        var order = occ.Where(i => i >= aIndex).Select(i => lane[i]).ToList();   // A と、A より後ろ
        var head = order[0];
        order.RemoveAt(0); order.Add(head);
        var res = (string?[])lane.Clone();
        var seats = occ.Where(i => i >= aIndex).ToList();
        for (int k = 0; k < seats.Count; k++) res[seats[k]] = order[k];
        return res;
    }

    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第228期 Phase 0（`0 spring phase0`・H0 ＝ 前段の規定）");
        Console.WriteLine();

        // ---- Q0-1 / Q0-3 ----
        Console.WriteLine("## Q0-1 経路の並べ替え ／ Q0-3 行の変わり方");
        Console.WriteLine();
        Console.WriteLine("経路（X 字の貫きのレーン）: レーン0 ＝ " + string.Join(" → ", FormationRules.LanePath(0).Select(Seat))
                          + " ／ レーン1 ＝ " + string.Join(" → ", FormationRules.LanePath(1).Select(Seat)) + "（中央は両方に属する）。○前2・○後2 はどの経路にも属さない。");
        Console.WriteLine();
        Console.WriteLine("| 席 | 行 |");
        Console.WriteLine("|---|---|");
        foreach (int s in Enumerable.Range(0, 9)) Console.WriteLine($"| {Seat(s)} | {FormationRules.RowOf(s)} |");
        Console.WriteLine();
        var examples = new (string Name, int Lane, string?[] Occ)[]
        {
            ("(a) 空席がある経路（○中1 が空）", 0, new[] { "A", "B", null, "C" }),
            ("(b) 9体の波・中央を共有する経路（前3 の A）", 1, new[] { "A", "B", "C", "D" }),
            ("(c) A の後ろに誰もいない経路", 0, new[] { "A", null, null, null }),
        };
        foreach (var (name, lane, occ) in examples)
        {
            var path = FormationRules.LanePath(lane);
            var after = Rotate(occ, 0);
            Console.WriteLine($"**{name}**");
            Console.WriteLine();
            Console.WriteLine("| 席 | 行 | 前 | 後 | 行が前に変わった |");
            Console.WriteLine("|---|---|---|---|---|");
            for (int i = 0; i < path.Count; i++)
            {
                string? who = after[i];
                int from = who is null ? -1 : Array.IndexOf(occ, who);
                bool fwd = who is not null && from >= 0 && FormationRules.DepthOf(FormationRules.RowOf(path[i])) < FormationRules.DepthOf(FormationRules.RowOf(path[from]));
                Console.WriteLine($"| {Seat(path[i])} | {FormationRules.RowOf(path[i])} | {occ[i] ?? "—"} | {who ?? "—"} | {(who is null || who == occ[i] ? "" : fwd ? "○（混乱の候補）" : "×")} |");
            }
            Console.WriteLine();
        }
        Console.WriteLine("中央 ↔ ○中 は同じ中列なので「前に変わった」にならない。9体の波で前3 の A を吹っ飛ばすと、中央の B は前3 へ（前へ）、○中3 の C は中央へ（同じ列）、後3 の D は ○中3 へ（前へ）。");
        Console.WriteLine();

        // ---- Q0-4 / Q0-5 ----
        Ver h0 = VerOf("H0");
        var benches = new (string Name, Formation F)[]
        {
            ("227 L2 の席", MHane227),
            ("225 の席", Formation.Build(front1: UnitCatalog.HaneH0, front3: UnitCatalog.BasaG0, center: UnitCatalog.Yomi, back1: UnitCatalog.Shio, back3: UnitCatalog.Sero)),
        };
        Console.WriteLine("## Q0-4 ハネの被弾（seed 0..199・H0）");
        Console.WriteLine();
        Console.WriteLine("被弾 ＝ 敵が出どころの `Damage`（HP に届いた一撃）。経路 ＝ 殴った敵が経路に属する席（○前2・○後2 以外）にいた割合。後列 ＝ 後1・後3（後ろに席が無いので弾けない）。反撃 ＝ 敵の反撃・割り込み。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率・波 | 全員生存 | ハネの被弾/戦 | 経路 | 後列 | 反撃 | ハネの被ダメ/戦 | ハネが倒れた | 敵が手番を失った/戦（転倒 ／ 痺れ ／ 同士討ち） | 続けて2以上の戦 | 最大 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|---|--:|--:|");
        foreach (var (s, w) in new[] { (0, 4), (0, 5), (0, 0), (0, 1), (0, 2), (0, 3), (1, 4) })
            foreach (var (name, f) in benches)
            {
                var a = Measure(Apply(f, h0), w, Scales[s].Sc);
                var d = a.D;
                Console.WriteLine($"| {name} | {Scales[s].Name} {WaveNames[w]} | {F1(a.Surv)} | {F2(a.Per(a.HaneHits))} | {(a.HaneHits == 0 ? "—" : (100.0 * a.HaneHitsOnLane / a.HaneHits).ToString("F0") + "%")} | "
                                  + $"{(a.HaneHits == 0 ? "—" : (100.0 * a.HaneHitsBack / a.HaneHits).ToString("F0") + "%")} | {(a.HaneHits == 0 ? "—" : (100.0 * a.HaneHitsReaction / a.HaneHits).ToString("F0") + "%")} | "
                                  + $"{F1(a.Per(a.HaneTaken))} | {100.0 * a.HaneFell / a.N:F1}% | {F2(d.Per(d.LostStagger + d.LostStun + d.Struck))}（{F2(d.Per(d.LostStagger))} ／ {F2(d.Per(d.LostStun))} ／ {F2(d.Per(d.Struck))}） | {100.0 * d.Run2 / d.N:F1}% | {d.RunMax} |");
            }
        Console.WriteLine();
        Console.WriteLine("## ハネのいる `compare` の行");
        Console.WriteLine();
        foreach (var (n, f) in HaneRows()) Console.WriteLine($"- {n}: {SeatsNamed(f)}");
        Console.WriteLine();
        Console.WriteLine($"所要 {sw.Elapsed.TotalSeconds:F1} 秒");
    }
}
