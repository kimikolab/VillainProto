using BattleCore;
using static Common;

// =====================================================================================
// burst run（第220期） —— 表A〜F
//
// 版: B0 ＝ 規定のミオ（M5）／ B1 ＝ ＋`MireBurst` ／ B2 ＝ ＋`MireBurstStack` ／ B2x ＝ ＋`MireBurstAll`
// 台: R1〜R4 × X字 / P2（席は B2 × 倍率 200 で選ぶ）＋ R3 のポンの X字 ＋ R5（`compare` のミオの行）
// 第2〜5波 × seed 0..199 × 倍率 115・150・200。
// =====================================================================================

static partial class BurstDiag
{
    internal static readonly string[] Versions = { "B0", "B1", "B2", "B2x" };

    static (string Tag, UnitDef Def)[]? _versions;
    internal static (string Tag, UnitDef Def)[] VersionDefs => _versions ??= new[]
    {
        ("B0", UnitCatalog.Mio),
        ("B1", MioWith(TraitId.MireBurst)),
        ("B2", MioWith(TraitId.MireBurstStack)),
        ("B2x", MioWith(TraitId.MireBurstAll)),
    };
    internal static UnitDef VerOf(string tag) => VersionDefs.First(v => v.Tag == tag).Def;

    static UnitDef MioWith(TraitId extra)
    {
        UnitDef d = UnitCatalog.Mio;
        return new()
        {
            Id = d.Id, Name = d.Name, MaxHp = d.MaxHp, Attack = d.Attack, Speed = d.Speed, Advances = d.Advances,
            Pattern = d.Pattern, Actions = d.Actions, Traits = d.Traits.Append(extra).ToArray(),
            PlusText = d.PlusText, MinusText = d.MinusText, Flavor = d.Flavor,
        };
    }

    static List<(string Name, Formation F)>? _seats;
    internal static List<(string Name, Formation F)> Seats(bool print = false)
    {
        if (_seats is not null && !print) return _seats;
        var list = new List<(string, Formation)>();
        if (print)
        {
            Console.WriteLine("### 席（B2 × 倍率 200 × 第2〜5波 × seed 1000..1049・同値は 全員生存 → 決着T → 列挙順）");
            Console.WriteLine();
            Console.WriteLine("| 台 | 陣形 | 席 | 勝ち数/200 | 勝ち数が同値 | 3段で同値 |");
            Console.WriteLine("|---|---|---|--:|--:|--:|");
        }
        var b2 = VerOf("B2");
        foreach (var (tag, _, mem) in Rigs)
            foreach (var sh in new[] { FormationShape.X, FormationShape.Diamond })
            {
                var members = mem().Select(d => d.Id == "mio" ? b2 : d).ToList();
                var (best, w, ties, ta) = PickSeat(members, sh, 200);
                list.Add((tag + " " + ShapeName(sh), WithMio(best, UnitCatalog.Mio)));
                if (print) Console.WriteLine("| " + tag + " | " + ShapeName(sh) + " | " + SeatsNamed(best) + " | " + w + " | " + ties + " | " + ta + " |");
            }
        if (print) Console.WriteLine();
        return _seats = list;
    }

    internal static List<(string Name, Formation F, bool Rig)> Benches()
    {
        var b = Seats().Select(s => (s.Name, s.F, true)).ToList();
        b.Add(("R3 ポンの X字", PonX(), true));
        foreach (var (n, f) in R5Rows()) b.Add(("R5 " + n.Split(' ')[0], f, false));
        return b;
    }

    static partial void RunImpl(string arg)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第220期 burst run —— 澱みが爆ぜる（B0〜B2x）");
        Console.WriteLine();
        Console.WriteLine("版: " + string.Join(" ／ ", VersionDefs.Select(v => v.Tag + " = [" + string.Join(", ", v.Def.Traits) + "]")));
        Console.WriteLine();
        if (arg != "rigs") CompareTable();
        if (arg != "compare") RigTables();
        Console.WriteLine("所要 " + (sw.ElapsedMilliseconds / 1000.0).ToString("F0") + " 秒");
    }

    // =================================================================================
    // compare（受け入れ 2 の分母と P2）
    // =================================================================================

    static void CompareTable()
    {
        Console.WriteLine("## `compare` 61 行 × 第2〜5波 × seed 0..199（倍率 115）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var w = new double[Versions.Length, rows.Length, 5];
        var aggs = new BAgg[Versions.Length, rows.Length];
        for (int v = 0; v < Versions.Length; v++)
            for (int i = 0; i < rows.Length; i++)
            {
                var a = Measure(WithMio(rows[i].F, VerOf(Versions[v])), 115);
                aggs[v, i] = a;
                foreach (int st in Waves25) w[v, i, st] = a.WinPct(st);
            }
        Console.WriteLine("| 版 | 動いたセル（±0.5 超） | うちミオのいない行 | 動いた行 | max\\|Δ\\| | 爆ぜた /戦（ミオの5行） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        var mioRows = Enumerable.Range(0, rows.Length).Where(i => rows[i].F.Occupied().Any(o => o.Def.Id == "mio")).ToList();
        for (int v = 0; v < Versions.Length; v++)
        {
            int moved = 0, movedNo = 0, rowsMoved = 0; double maxd = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                bool rm = false;
                foreach (int st in Waves25)
                {
                    double d = w[v, i, st] - w[0, i, st];
                    if (Math.Abs(d) > 0.5) { moved++; rm = true; if (!mioRows.Contains(i)) movedNo++; }
                    maxd = Math.Max(maxd, Math.Abs(d));
                }
                if (rm) rowsMoved++;
            }
            double bursts = mioRows.Sum(i => aggs[v, i].B.Bursts[0] + aggs[v, i].B.Bursts[1]) / (double)mioRows.Sum(i => aggs[v, i].Battles);
            Console.WriteLine("| " + Versions[v] + " | " + moved + " | " + movedNo + " | " + rowsMoved + " | " + F1(maxd) + " | " + F2(bursts) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("ミオの5行（第2〜5波・B0 → B1 / B2 / B2x）:");
        Console.WriteLine();
        foreach (int i in mioRows)
            Console.WriteLine("- " + rows[i].Name + ": " + string.Join(" ／ ", Waves25.Select(st => F1(w[0, i, st]) + "→" + F1(w[1, i, st]) + "/" + F1(w[2, i, st]) + "/" + F1(w[3, i, st]))));
        Console.WriteLine();
    }

    // =================================================================================
    // 表A〜F（台）
    // =================================================================================

    static void RigTables()
    {
        Console.WriteLine("## 表A. 台 × 陣形 × 版 × 倍率（第2〜5波 × seed 0..199）");
        Console.WriteLine();
        Seats(print: true);
        var benches = Benches();
        var res = new Dictionary<(int, int, int), BAgg>();
        for (int b = 0; b < benches.Count; b++)
            for (int v = 0; v < Versions.Length; v++)
                foreach (int sc in Scales)
                    res[(b, v, sc)] = Measure(WithMio(benches[b].F, VerOf(Versions[v])), sc);

        Console.WriteLine("| 台 | 倍率 | 勝率 B0 → B1 / B2 / B2x | 全員生存 B0 → B1 / B2 / B2x | 決着T B0 / B1 / B2 / B2x |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int b = 0; b < benches.Count; b++)
            foreach (int sc in Scales)
            {
                var a = Enumerable.Range(0, Versions.Length).Select(v => res[(b, v, sc)]).ToArray();
                string Row(Func<BAgg, double> f) => F1(f(a[0])) + " → " + string.Join(" / ", a.Skip(1).Select(x => F1(f(x))));
                Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + Row(x => x.Mean25) + " | " + Row(x => x.AllSurvPct) + " | " + string.Join(" / ", a.Select(x => F2(x.MeanWinT))) + " |");
            }
        Console.WriteLine();

        // 表B
        Console.WriteLine("## 表B. 爆発の帳簿（敵側・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 版 | 爆ぜた | 燃料 0 | 当たった | 名目の和 | 1発の平均 | 当たった駒の HP の減り（爆発が起こした放電も含む） | 撃破 | 脆さの分（% ） | 連鎖の長さ 1 / 2 / 3 / 4〜 %（爆ぜる資格のある死の数） |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        for (int b = 0; b < benches.Count; b++)
            foreach (int sc in Scales)
                for (int v = 1; v < Versions.Length; v++)
                {
                    var a = res[(b, v, sc)]; var B = a.B;
                    long chains = B.ChainLenHist.Sum();
                    string hist = chains == 0 ? "—" : P1(B.ChainLenHist[1], chains) + " / " + P1(B.ChainLenHist[2], chains) + " / " + P1(B.ChainLenHist[3], chains) + " / " + P1(B.ChainLenHist.Skip(4).Sum(), chains);
                    Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + Versions[v] + " | " + F2(a.Per(B.Bursts[0])) + " | " + F2(a.Per(B.BurstsEmpty[0])) + " | " + F2(a.Per(B.Hits[0])) + " | "
                                      + F1(a.Per(B.Nominal[0])) + " | " + F1(B.Hits[0] == 0 ? double.NaN : (double)B.Nominal[0] / B.Hits[0]) + " | " + F1(a.Per(B.Removed[0])) + " | "
                                      + F2(a.Per(B.Kills[0])) + " | " + F1(a.Per(a.BrittleBurst)) + "（" + P1(a.BrittleBurst, a.BrittleBurstBase) + "） | " + hist + " |");
                }
        Console.WriteLine();

        // 表C
        Console.WriteLine("## 表C. 段ごと・連鎖の順ごとの1発の平均（B2・倍率 150 と 200）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 段 1 / 2 / 3 / 4〜 の1発（当たった数） | 戦の 1 / 2 / 3 / 4 / 5 本目の連鎖の1発（爆ぜた1回あたり） |");
        Console.WriteLine("|---|---|---|---|");
        int vb2 = Array.IndexOf(Versions, "B2");
        for (int b = 0; b < benches.Count; b++)
            foreach (int sc in new[] { 150, 200 })
            {
                var B = res[(b, vb2, sc)].B;
                string St(int lo, int hi)
                {
                    long n = 0, s = 0;
                    for (int k = lo; k <= hi; k++) { n += B.StageHits[k]; s += B.StageNominal[k]; }
                    return n == 0 ? "—" : F1((double)s / n) + "（" + n + "）";
                }
                string Ci(int k) => B.ChainIdxBursts[k] == 0 ? "—" : F1((double)B.ChainIdxNominal[k] / B.ChainIdxBursts[k]);
                Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + St(0, 0) + " / " + St(1, 1) + " / " + St(2, 2) + " / " + St(3, 9) + " | "
                                  + string.Join(" / ", Enumerable.Range(0, 5).Select(Ci)) + " |");
            }
        Console.WriteLine();

        // 表D
        Console.WriteLine("## 表D. 雷と澱みの交互（B2・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 爆発で弾けた感電 | 連鎖の最初の死が放電 % ／ 雷 % | 放電で倒れた敵（感電の帳簿） | ミオの印が運ばれた（放電で） |");
        Console.WriteLine("|---|---|--:|---|--:|--:|");
        for (int b = 0; b < benches.Count; b++)
            foreach (int sc in Scales)
            {
                var a = res[(b, vb2, sc)]; var B = a.B;
                long chains = B.ChainLenHist.Sum();
                var mio = a.ByUnit.GetValueOrDefault("mio") ?? new UnitTally();
                Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + F2(a.Per(B.ShockPops[0])) + " | " + P1(B.RootByDischarge, chains) + " ／ " + P1(B.RootByThunder, chains) + " | "
                                  + F2(a.Per(a.E.DischargeDeaths)) + " | " + F2(a.Per(mio.MireCarried)) + " |");
            }
        Console.WriteLine();

        // 表E
        Console.WriteLine("## 表E. ラウ（倍率 150・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | ラウが振った | 弾いた感電 | うつした（回 ／ 層） | 死骸から飛んだ（回 ／ 層） | 同じ死で印の移りと重なった | 爆発の1発の平均 |");
        Console.WriteLine("|---|---|--:|--:|---|---|--:|--:|");
        int T = (int)PoisonRoute.Touch, C = (int)PoisonRoute.Contagion;
        for (int b = 0; b < benches.Count; b++)
        {
            if (!benches[b].F.Occupied().Any(o => o.Def.Id == "rau")) continue;
            for (int v = 0; v < Versions.Length; v++)
            {
                var a = res[(b, v, 150)]; var B = a.B;
                var rt = a.ByUnit.GetValueOrDefault("rau") ?? new UnitTally();
                Console.WriteLine("| " + benches[b].Name + " | " + Versions[v] + " | " + F2(a.Per(rt.Attacks)) + " | " + F2(a.Per(rt.ShockTriggered)) + " | "
                                  + F2(a.Per(B.PoisonWrites[T])) + " ／ " + F1(a.Per(B.PoisonAmount[T])) + " | " + F2(a.Per(B.PoisonWrites[C])) + " ／ " + F1(a.Per(B.PoisonAmount[C])) + " | "
                                  + F2(a.Per(B.HandoffAndContagion)) + " | " + F1(B.Hits[0] == 0 ? double.NaN : (double)B.Nominal[0] / B.Hits[0]) + " |");
            }
        }
        Console.WriteLine();

        // 表F
        Console.WriteLine("## 表F. B2x の味方側（1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 倍率 | 味方で爆ぜた | 燃料 0 | 味方に当たった（名目） | 反転で回復（回 ／ 癒えた量） | 味方が受けた HP の減り | それで倒れた味方 | 勝率 B2 → B2x |");
        Console.WriteLine("|---|---|--:|--:|--:|---|--:|--:|---|");
        int vx = Array.IndexOf(Versions, "B2x");
        for (int b = 0; b < benches.Count; b++)
            foreach (int sc in Scales)
            {
                var a = res[(b, vx, sc)]; var B = a.B;
                Console.WriteLine("| " + benches[b].Name + " | " + sc + " | " + F2(a.Per(B.Bursts[1])) + " | " + F2(a.Per(B.BurstsEmpty[1])) + " | " + F1(a.Per(B.Nominal[1])) + " | "
                                  + F2(a.Per(B.InverseHits)) + " ／ " + F1(a.Per(B.InverseHealed)) + " | " + F1(a.Per(B.Removed[1])) + " | " + F2(a.Per(B.Kills[1])) + " | "
                                  + F1(res[(b, vb2, sc)].Mean25) + " → " + F1(a.Mean25) + " |");
            }
        Console.WriteLine();
    }
}
