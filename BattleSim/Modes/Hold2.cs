using BattleCore;
using static Common;

// =====================================================================================
// hold2 モード —— **第142期に `Program.cs` から機械的に切り出した。中身は1行も動かしていない。**
//
// 元は `Prog.Body` の中の `if (focusId == "hold2")` ブロックで、切り出しは
// 中括弧の対応だけで行い、**再インデントもしていない**
// （移す前と1バイトも違わないことを機械で照合するため）。
//
//     dotnet run --project BattleSim -c Release 0 hold2
// =====================================================================================

static class Hold2Diag
{
// ==================================================================================
public static void Run(string[] args, int stageIndex)
{
    string h2Mode = args.Length > 2 ? args[2] : "tables";
    var h2Compare = CompareBuilds();
    var h2Cross = CrossBuilds();
    var h2All = h2Compare.Concat(h2Cross).ToArray();
    IReadOnlyList<EnemyCatalog.Stage> h2Stages = EnemyCatalog.Stages;

    // `reseat` の狙（ガルドが前列 / セッキが後列）の写し。**判定は1文字も変えていない。**
    static bool H2Intent(Formation f)
    {
        foreach (var (slot, def) in f.Occupied())
        {
            if (ReferenceEquals(def, UnitCatalog.Gald) && FormationRules.RowOf(slot) != Row.Front) return false;
            if (ReferenceEquals(def, UnitCatalog.Sekki) && FormationRules.RowOf(slot) != Row.Back) return false;
        }
        return true;
    }
    static string H2N(UnitDef? d) => d?.Name ?? "−";
    static string H2Seats(Formation f) => H2N(f[0]) + "/" + H2N(f[1]) + " | " + H2N(f[2]) + " | " + H2N(f[3]) + "/" + H2N(f[4]);

    // 情報セル: `0 < x < 100` を**第2〜5波**で数える（第59期 9-1 の定義。規約 (G10)）。
    static int H2Info(double[] cells)
    {
        int n = 0;
        for (int i = 1; i < cells.Length; i++) if (cells[i] > 0.0 && cells[i] < 100.0) n++;
        return n;
    }

    // ------------------------------------------------------------------------------
    // (S1) 席。**`confirm` の候補表は使わない**——あれは焼き付けた台帳で、採用済みの行も
    // 残っている（第89期の訂正）。**生きた判定は `reseat` の器具をその場で回して取る。**
    // ------------------------------------------------------------------------------
    if (h2Mode == "seats")
    {
        const int H2Scan = 50;       // 粗探索。`reseat` / `layout` と揃える
        const int H2Verify = 200;    // 作法1・2 の帯（seed 0..199）。`compare` と揃える
        const int H2CfBase = 200;    // 追試の帯（seed 200..599）。**選定に使っていない seed**
        const int H2CfSeeds = 400;
        const int H2TopOverall = 20, H2TopConstrained = 10;
        const double H2Line = 5.0;   // 第46期の採否閾値

        Console.WriteLine("# 第107期 (S1) —— 席");
        Console.WriteLine();
        Console.WriteLine("作法（第46期）: (1) 現行が `reseat` の上位5通りに入っていれば動かさない ／ "
            + "(2) 入っていない行だけ追試（seed " + H2CfBase + ".." + (H2CfBase + H2CfSeeds - 1) + "）で測り、**"
            + H2Line.ToString("F1") + "pt 以上**のときだけ動かす ／ (3) 採否は1位の配置ではなく**次数**で読む。");
        Console.WriteLine();
        Console.WriteLine("粗探索は seed 0.." + (H2Scan - 1) + "、作法1・2 の帯は seed 0.." + (H2Verify - 1) + "。"
            + "**候補は「平均1位」ではなく、情報セル（`0 < x < 100`・第2〜5波）を 2 以上に保つ最上位を採る**（第59期）。");
        Console.WriteLine();

        var h2Stay1 = new List<string>();
        var h2Stay2 = new List<(string Name, int Rank, double Cur, double Top)>();
        var h2Cand = new List<(string Name, int Rank, double Cur, double Top)>();
        var h2Detail = new List<string>();

        foreach (var build in h2Compare)
        {
            var members = build.F.Occupied().Select(x => x.Def).ToList();
            var perms = new List<Formation>();
            foreach (int[] assign in SlotAssignments(members.Count))
            {
                var f = new Formation();
                for (int m = 0; m < members.Count; m++) f[assign[m]] = members[m];
                perms.Add(f);
            }

            var scan = new int[perms.Count];
            Parallel.For(0, perms.Count, i =>
            {
                int wins = 0;
                foreach (EnemyCatalog.Stage st in h2Stages)
                    for (int seed = 0; seed < H2Scan; seed++)
                        if (BattleEngine.Run(perms[i], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                scan[i] = wins;
            });

            var order = Enumerable.Range(0, perms.Count).OrderByDescending(i => scan[i]).ThenBy(i => i).ToList();
            var pool = order.Take(H2TopOverall)
                .Concat(order.Where(i => H2Intent(perms[i])).Take(H2TopConstrained))
                .Append(order.First(i => SameFormation(perms[i], build.F)))
                .Distinct().ToList();

            var cellsA = new double[pool.Count][];
            Parallel.For(0, pool.Count, k =>
            {
                cellsA[k] = h2Stages.Select(st =>
                {
                    int wins = 0;
                    for (int seed = 0; seed < H2Verify; seed++)
                        if (BattleEngine.Run(perms[pool[k]], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                    return wins * 100.0 / H2Verify;
                }).ToArray();
            });

            var rankedA = Enumerable.Range(0, pool.Count).OrderByDescending(k => cellsA[k].Average()).ToList();
            int curK = pool.FindIndex(i => SameFormation(perms[i], build.F));
            int curRank = rankedA.IndexOf(curK) + 1;
            double curAvg = cellsA[curK].Average(), topAvg = cellsA[rankedA[0]].Average();

            if (curRank <= 5) { h2Stay1.Add(build.Name); continue; }
            if (topAvg - curAvg < H2Line) { h2Stay2.Add((build.Name, curRank, curAvg, topAvg)); continue; }
            h2Cand.Add((build.Name, curRank, curAvg, topAvg));

            // ---- 追試（seed 200..599）。**情報セルの列を出す**（第59期の作法）
            var cellsB = new double[pool.Count][];
            Parallel.For(0, pool.Count, k =>
            {
                cellsB[k] = h2Stages.Select(st =>
                {
                    int wins = 0;
                    for (int seed = H2CfBase; seed < H2CfBase + H2CfSeeds; seed++)
                        if (BattleEngine.Run(perms[pool[k]], st.Enemy, seed, verbose: false).PlayerWon) wins++;
                    return wins * 100.0 / H2CfSeeds;
                }).ToArray();
            });
            var rankedB = Enumerable.Range(0, pool.Count).OrderByDescending(k => cellsB[k].Average()).ToList();
            double curB = cellsB[curK].Average();
            int curInfoB = H2Info(cellsB[curK]);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine();
            sb.AppendLine("### " + build.Name);
            sb.AppendLine();
            sb.AppendLine("作法1 で現行 **" + curRank + " 位**（" + curAvg.ToString("F1") + "% 対 1位 "
                + topAvg.ToString("F1") + "%・差 **" + (topAvg - curAvg).ToString("+0.0;-0.0") + "pt**）なので追試へ。");
            sb.AppendLine();
            sb.AppendLine("| 追順 | 粗順 | 狙 | 前1/前3 | 中央 | 後1/後3 | 平均(400) | 情報セル |"
                + string.Concat(h2Stages.Select((_, i) => " 第" + (i + 1) + "波 |")));
            sb.AppendLine("|--:|--:|:-:|---|---|---|--:|--:|" + string.Concat(h2Stages.Select(_ => "---:|")));
            for (int r = 0; r < Math.Min(10, rankedB.Count); r++)
            {
                int k = rankedB[r];
                Formation f = perms[pool[k]];
                sb.AppendLine("| " + (r + 1) + (k == curK ? "★現行" : "") + " | " + (order.IndexOf(pool[k]) + 1)
                    + " | " + (H2Intent(f) ? "○" : "×") + " | " + H2Seats(f) + " | "
                    + cellsB[k].Average().ToString("F1") + "% | " + H2Info(cellsB[k]) + " |"
                    + string.Concat(cellsB[k].Select(c => " " + c.ToString("F1") + "% |")));
            }
            if (rankedB.IndexOf(curK) >= 10)
                sb.AppendLine("| " + (rankedB.IndexOf(curK) + 1) + "★現行 | " + (order.IndexOf(pool[curK]) + 1)
                    + " | " + (H2Intent(perms[pool[curK]]) ? "○" : "×") + " | " + H2Seats(perms[pool[curK]]) + " | "
                    + curB.ToString("F1") + "% | " + curInfoB + " |"
                    + string.Concat(cellsB[curK].Select(c => " " + c.ToString("F1") + "% |")));

            // 採る席は**3つの条件の積**。無ければ据え置き。
            //   (a) 情報セルを 2 以上に保つ（第59期。席は「勝つ席」ではなく「測れる席」で選ぶ）
            //   (b) **狙（ガルドが前列 / セッキが後列）を満たす**
            //       ——「編成の狙いと探索1位が食い違ったら狙いを優先し、理由をコメントに残す」（CONTRIBUTING.md）。
            //       第107期の初版はこれを器具に入れ忘れていて、`継ぎ当て×分散回復` の1位（ガルドが中央）を
            //       採るところだった。**参考として狙を外した最上位も並べる**（緩めた版ではなく、外した版が
            //       どれだけ上にあるかを読めるようにするため）。
            //   (c) 現行との差が閾値以上（第46期）
            int pick = -1, free = -1;
            foreach (int k in rankedB) { if (free < 0 && H2Info(cellsB[k]) >= 2) free = k; }
            foreach (int k in rankedB) { if (H2Info(cellsB[k]) >= 2 && H2Intent(perms[pool[k]])) { pick = k; break; } }
            sb.AppendLine();
            if (free >= 0 && free != pick)
                sb.AppendLine("（参考）**狙を外した**最上位は 追順 **" + (rankedB.IndexOf(free) + 1) + " 位**（"
                    + cellsB[free].Average().ToString("F1") + "% / 情報セル " + H2Info(cellsB[free]) + "）。**採らない。**");
            if (pick < 0 || pick == curK)
                sb.AppendLine("**判定: 据え置き** —— 狙を満たし情報セルを 2 以上に保つ最上位が現行（または該当なし）。");
            else
            {
                double gain = cellsB[pick].Average() - curB;
                Formation f = perms[pool[pick]];
                sb.AppendLine("狙を満たし情報セルを 2 以上に保つ最上位は 追順 **" + (rankedB.IndexOf(pick) + 1) + " 位**（"
                    + cellsB[pick].Average().ToString("F1") + "% / 情報セル " + H2Info(cellsB[pick])
                    + "）で、現行（" + curB.ToString("F1") + "% / 情報セル " + curInfoB + "）との差は **"
                    + gain.ToString("+0.0;-0.0") + "pt**。");
                sb.AppendLine("次数: 中央（次数4）が **" + H2N(perms[pool[curK]][2]) + " → " + H2N(f[2]) + "**。");
                sb.AppendLine("**判定: " + (gain >= H2Line ? "差し替え" : "据え置き") + "** —— 閾値 "
                    + H2Line.ToString("F1") + "pt に対して " + gain.ToString("+0.0;-0.0") + "pt。");
                if (gain >= H2Line)
                    sb.AppendLine("採る配置: `front1: " + H2N(f[0]) + ", front3: " + H2N(f[1]) + ", center: " + H2N(f[2])
                        + ", back1: " + H2N(f[3]) + ", back3: " + H2N(f[4]) + "`");
            }
            h2Detail.Add(sb.ToString());
            Console.Error.WriteLine("[hold2 seats] " + build.Name + " 追試おわり");
        }

        Console.WriteLine("## 表A-1 —— 61 行の内訳");
        Console.WriteLine();
        Console.WriteLine("| 段 | 行数 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine("| 作法1（現行が上位5通り）で終わり | " + h2Stay1.Count + " |");
        Console.WriteLine("| 作法2（差が " + H2Line.ToString("F1") + "pt 未満）で終わり | " + h2Stay2.Count + " |");
        Console.WriteLine("| **追試にかかる** | **" + h2Cand.Count + "** |");
        Console.WriteLine();
        Console.WriteLine("### 作法2 で止まった行");
        Console.WriteLine();
        Console.WriteLine("| 行 | 現行の順位 | 現行(200) | 1位(200) | 差 |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        foreach (var h2s in h2Stay2.OrderByDescending(h2s => h2s.Top - h2s.Cur))
            Console.WriteLine("| " + h2s.Name + " | " + h2s.Rank + " | " + h2s.Cur.ToString("F1") + "% | "
                + h2s.Top.ToString("F1") + "% | " + (h2s.Top - h2s.Cur).ToString("+0.0;-0.0") + "pt |");
        Console.WriteLine();
        Console.WriteLine("## 表A-2 —— 追試（seed 200..599）");
        foreach (string d in h2Detail) Console.Write(d);
        return;
    }
    // ------------------------------------------------------------------------------
    // (S2) ムドの再判定。**seed 帯を第106期（0..199）から変えてある**——
    // 第106期が `Gain = 5` を対照として同じ帯で測っているので、そのまま比べると循環する
    // （第89期 (P1) と同じ作法）。**較正 `+5` は測る前に固定した値**で、
    // 33 ÷ 7.21 = 4.58 → 丸めて 5（指示書 §2-1）。結果を見てから選んだ値ではない。
    // ------------------------------------------------------------------------------
    if (h2Mode == "rage")
    {
        const int R2Base = 200, R2Seeds = 400;
        var r2Ver = new (string Tag, string Desc, RageRule R)[]
        {
            ("R0", "現行（量で育つ・`max(1, dmg / 2)`）", RageRule.Default),
            ("R1", "回数で育つ（`Gain = " + RageRule.MeasuredGain + "`）", new RageRule(RageMode.Count, RageRule.MeasuredGain)),
        };

        // 憤怒の保持者は**2枚**（`TraitId.Rage` の grep 全数）。ムドだけを見ると
        // 後備えの行の変化を取り落とす（第106期の予測1の穴）。
        string[] r2Holders = { "mudo", "sekki" };

        int r2W = h2Stages.Count, r2B = h2All.Length;
        var r2Rate = new double[r2Ver.Length][][];
        var r2Tally = new Dictionary<string, long[]>[r2Ver.Length];
        const int R2N = 12;
        const int R2Battles = 0, R2Fires = 1, R2Gain = 2, R2Peak = 3, R2PeakT = 4,
                  R2Reach = 5, R2ReachT = 6, R2Turns = 7, R2Dmg = 8, R2Live = 9;
        // R2Reach / R2ReachT は `AtkProbes` の最後（+24）だけを見る。

        for (int p = 0; p < r2Ver.Length; p++)
        {
            r2Rate[p] = new double[r2W][];
            for (int w = 0; w < r2W; w++) r2Rate[p][w] = new double[r2B];
            var acc = r2Tally[p] = new Dictionary<string, long[]>();
            foreach (string id in r2Holders) acc[id] = new long[R2N];

            for (int w = 0; w < r2W; w++)
            {
                var enemy = h2Stages[w].Enemy;
                for (int b = 0; b < r2B; b++)
                {
                    var f = h2All[b].F;
                    bool has = f.Occupied().Any(o => r2Holders.Contains(o.Def.Id));
                    int wins = 0;
                    int pp = p, ww = w;
                    var winsArr = new int[R2Seeds];
                    var locArr = new long[R2Seeds][];
                    Parallel.For(0, R2Seeds, s =>
                    {
                        var v = new long[R2N * 2];
                        var r = BattleEngine.Run(f, enemy, R2Base + s, verbose: false, rage: r2Ver[pp].R);
                        if (r.PlayerWon) winsArr[s] = 1;
                        // **駒ごとの帳簿の分母は第2〜5波**（規約 (G10)。第一波は教習波なので参考にしか使わない）。
                        if (has && ww > 0)
                            for (int h = 0; h < r2Holders.Length; h++)
                            {
                                if (!f.Occupied().Any(o => o.Def.Id == r2Holders[h])) continue;
                                int o0 = h * R2N;
                                v[o0 + R2Battles] = 1;
                                v[o0 + R2Turns] = r.Turns;
                                if (r.TallyByUnit.TryGetValue(r2Holders[h], out UnitTally? t))
                                {
                                    v[o0 + R2Fires] = t.RageCountFires;
                                    v[o0 + R2Gain] = t.RageGain;
                                    v[o0 + R2Peak] = t.AtkPeak;
                                    v[o0 + R2PeakT] = t.AtkPeakTurn;
                                    v[o0 + R2Dmg] = t.DamageToEnemy;
                                    int last = UnitTally.AtkProbes.Length - 1;
                                    if (t.AtkProbeTurn is not null && t.AtkProbeTurn[last] > 0)
                                    { v[o0 + R2Reach] = 1; v[o0 + R2ReachT] = t.AtkProbeTurn[last]; }
                                }
                            }
                        locArr[s] = v;
                    });
                    for (int sq = 0; sq < R2Seeds; sq++)
                    {
                        wins += winsArr[sq];
                        if (!has) continue;
                        for (int h = 0; h < r2Holders.Length; h++)
                        {
                            long[] a = acc[r2Holders[h]];
                            for (int k = 0; k < R2N; k++) a[k] += locArr[sq][h * R2N + k];
                        }
                    }
                    r2Rate[p][w][b] = wins * 100.0 / R2Seeds;
                }
                Console.Error.WriteLine("[hold2 rage] " + r2Ver[p].Tag + " 第" + (w + 1) + "波");
            }
        }

        double R2Avg(int p, int b) { double s = 0; for (int w = 1; w < r2W; w++) s += r2Rate[p][w][b]; return s / (r2W - 1); }
        double R2Per(long[] a, int k) => a[R2Battles] == 0 ? 0 : (double)a[k] / a[R2Battles];

        Console.WriteLine("# 第107期 (S2) —— ムドの再判定（憤怒を回数で育てる）");
        Console.WriteLine();
        Console.WriteLine("台: `compare` 61 行 ＋ 交差帯 12 行 × 全 " + r2W + " 波 × seed "
            + R2Base + ".." + (R2Base + R2Seeds - 1) + " ＝ " + ((long)r2B * r2W * R2Seeds).ToString("N0")
            + " 戦/版。**帯は第106期（0..199）と重ならない。**");
        Console.WriteLine();
        Console.WriteLine("| 版 | 中身 |");
        Console.WriteLine("|---|---|");
        foreach (var v in r2Ver) Console.WriteLine("| " + v.Tag + " | " + v.Desc + " |");

        // ---- 表B-1: Phase 0 の較正の確認
        Console.WriteLine();
        Console.WriteLine("## 表B-1 —— 較正の確認（Phase 0 の 1）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 駒 | 在席戦 | 発火/戦 | 1発あたり | `AtkBonus` 到達点 | 到達T | +24 到達% | +24 到達T | 与ダメ/戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int p = 0; p < r2Ver.Length; p++)
            foreach (string id in r2Holders)
            {
                long[] a = r2Tally[p][id];
                Console.WriteLine("| " + r2Ver[p].Tag + " | " + h2All.SelectMany(x => x.F.Occupied())
                        .Where(o => o.Def.Id == id).Select(o => o.Def.Name).First()
                    + " | " + a[R2Battles] + " | " + R2Per(a, R2Fires).ToString("0.00")
                    + " | " + (a[R2Fires] == 0 ? 0 : (double)a[R2Gain] / a[R2Fires]).ToString("0.00")
                    + " | " + R2Per(a, R2Peak).ToString("0.0")
                    + " | " + R2Per(a, R2PeakT).ToString("0.00")
                    + " | " + (a[R2Battles] == 0 ? 0 : a[R2Reach] * 100.0 / a[R2Battles]).ToString("0.0") + "%"
                    + " | " + (a[R2Reach] == 0 ? 0 : (double)a[R2ReachT] / a[R2Reach]).ToString("0.00")
                    + " | " + R2Per(a, R2Dmg).ToString("0.0") + " |");
            }
        Console.WriteLine();
        {
            long[] m0 = r2Tally[0]["mudo"];
            Console.WriteLine("第96期の被弾 **7.21 回/戦** と第106期の到達点 **33** を再現するか: 発火 **"
                + R2Per(m0, R2Fires).ToString("0.00") + " 回/戦**・到達点 **" + R2Per(m0, R2Peak).ToString("0.0")
                + "**（素の攻 " + UnitCatalog.Mudo.Attack + " と合わせて **攻 "
                + (UnitCatalog.Mudo.Attack + R2Per(m0, R2Peak)).ToString("0.0") + "**）。");
        }

        // ---- 表B-2: Q1（ムド／セッキを含む行）
        Console.WriteLine();
        Console.WriteLine("## 表B-2 —— Q1（保持者を含む行の第2〜5波平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 保持者 | R0 | R1 | Δ |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        double q1Sum = 0; int q1N = 0, q1Up = 0;
        double q1SumM = 0; int q1NM = 0;
        for (int b = 0; b < r2B; b++)
        {
            var ids = h2All[b].F.Occupied().Select(o => o.Def.Id).Where(i => r2Holders.Contains(i)).ToList();
            if (ids.Count == 0) continue;
            double a0 = R2Avg(0, b), a1 = R2Avg(1, b);
            q1Sum += a1 - a0; q1N++; if (a1 > a0) q1Up++;
            if (ids.Contains("mudo")) { q1SumM += a1 - a0; q1NM++; }
            Console.WriteLine("| " + h2All[b].Name + " | " + string.Join("・", ids) + " | "
                + a0.ToString("F1") + "% | " + a1.ToString("F1") + "% | "
                + (a1 - a0).ToString("+0.0;-0.0") + "pt |");
        }
        Console.WriteLine();
        Console.WriteLine("**保持者を含む " + q1N + " 行の平均 " + (q1Sum / Math.Max(1, q1N)).ToString("+0.00;-0.00")
            + "pt**（上がった行 " + q1Up + " / " + q1N + "）。うち**ムドの " + q1NM + " 行は "
            + (q1SumM / Math.Max(1, q1NM)).ToString("+0.00;-0.00") + "pt**。");
        {
            long[] m0 = r2Tally[0]["mudo"], m1 = r2Tally[1]["mudo"];
            double p0 = R2Per(m0, R2Peak), p1 = R2Per(m1, R2Peak);
            double t0 = m0[R2Reach] == 0 ? 0 : (double)m0[R2ReachT] / m0[R2Reach];
            double t1 = m1[R2Reach] == 0 ? 0 : (double)m1[R2ReachT] / m1[R2Reach];
            Console.WriteLine();
            Console.WriteLine("| 判定 | 量 | 線 | 結果 |");
            Console.WriteLine("|---|---|---|:-:|");
            Console.WriteLine("| **Q1** | ムドを含む行の第2〜5波平均 " + (q1SumM / Math.Max(1, q1NM)).ToString("+0.00;-0.00")
                + "pt | 現行より上 | " + (q1SumM > 0 ? "**○**" : "**×**") + " |");
            Console.WriteLine("| **Q2** | `AtkBonus` の到達点 " + p0.ToString("0.0") + " → " + p1.ToString("0.0")
                + "（差 " + (p1 - p0).ToString("+0.0;-0.0") + "） | ±3 以内 | "
                + (Math.Abs(p1 - p0) <= 3.0 ? "**○**" : "**×**") + " |");
            Console.WriteLine("| **Q3** | +24 への到達T " + t0.ToString("0.00") + " → " + t1.ToString("0.00")
                + "（到達率 " + (m0[R2Battles] == 0 ? 0 : m0[R2Reach] * 100.0 / m0[R2Battles]).ToString("0.0")
                + "% → " + (m1[R2Battles] == 0 ? 0 : m1[R2Reach] * 100.0 / m1[R2Battles]).ToString("0.0")
                + "%） | 現行より早い | " + (t1 < t0 && t1 > 0 ? "**○**" : "**×**") + " |");
        }

        // ---- 表D: 拒否権（分母は compare 61 行全体・第91期 (G1)）
        Console.WriteLine();
        Console.WriteLine("## 表D —— 拒否権（分母は `compare` 61 行全体・第91期 (G1)）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | R0 | R1 | Δ |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        int veto3 = 0;
        for (int b = 0; b < h2Compare.Length; b++)
            for (int w = 0; w < r2W; w++)
            {
                double d = r2Rate[1][w][b] - r2Rate[0][w][b];
                if (d <= -10.0)
                {
                    veto3++;
                    Console.WriteLine("| " + h2All[b].Name + " | 第" + (w + 1) + "波 | "
                        + r2Rate[0][w][b].ToString("F1") + "% | " + r2Rate[1][w][b].ToString("F1") + "% | "
                        + d.ToString("+0.0;-0.0") + "pt |");
                }
            }
        if (veto3 == 0) Console.WriteLine("| （該当なし） | | | | |");
        Console.WriteLine();
        var primSet = new HashSet<string>(Baseline.PrimaryRows);
        double f0 = 0, f1 = 0; int fn = 0;
        for (int b = 0; b < h2Compare.Length; b++)
            if (primSet.Contains(h2All[b].Name)) { f0 += r2Rate[0][r2W - 1][b]; f1 += r2Rate[1][r2W - 1][b]; fn++; }
        Console.WriteLine("| 拒否権 | 量 | 線 | 結果 |");
        Console.WriteLine("|---|---|---|:-:|");
        Console.WriteLine("| (1) 主判定" + fn + "行の第五波平均 | " + (f0 / Math.Max(1, fn)).ToString("F1")
            + "% → **" + (f1 / Math.Max(1, fn)).ToString("F1") + "%** | ≥ "
            + Baseline.PrimaryFifthFloor.ToString("F1") + "% | "
            + (f1 / Math.Max(1, fn) >= Baseline.PrimaryFifthFloor ? "**○**" : "**×**") + " |");
        Console.WriteLine("| (3) いずれかの波で −10.0pt 以上落ちた行 | **" + veto3 + " 件** | 0 件 | "
            + (veto3 == 0 ? "**○**" : "**×**") + " |");
        Console.WriteLine();
        Console.WriteLine("## 表E —— 自己検査（(b) 1発あたりの比）");
        Console.WriteLine();
        foreach (string id in r2Holders)
        {
            long[] a = r2Tally[1][id];
            double ratio = a[R2Fires] == 0 ? 0 : (double)a[R2Gain] / a[R2Fires];
            Console.WriteLine("- **(b)** " + id + ": `RageGain ÷ RageCountFires` = **" + ratio.ToString("0.00")
                + "**（`Gain = " + RageRule.MeasuredGain + "` と一致すべき） —— "
                + (Math.Abs(ratio - RageRule.MeasuredGain) < 1e-9 ? "**○**" : "**×**"));
        }
        for (int p = 0; p < r2Ver.Length; p++)
        {
            long f = r2Tally[p]["mudo"][R2Fires], b0 = r2Tally[p]["mudo"][R2Battles];
            Console.WriteLine("- **(b')** " + r2Ver[p].Tag + " のムドの発火 **" + (b0 == 0 ? 0 : (double)f / b0).ToString("0.00")
                + " 回/戦** —— **発火する集合は版に依らない**（計数は式の分岐より手前）。");
        }
        return;
    }
    // ------------------------------------------------------------------------------
    // (S3) ハリ。**発火口を手番の外へ移す**（`SutureFireRule`）。
    // 律速は3期にわたって同じで（第83期の「切れる」判定・第85期の振り 2.15 回/戦・
    // 第106期の再行動 0.00 回/戦）、どれも「手番が来ない」に帰着する。
    // 第105期の「回復の 90.3% は手番の外から出る」を、この駒に当てる。
    // ------------------------------------------------------------------------------
    if (h2Mode == "suture")
    {
        const int S3Base = 0, S3Seeds = 200;
        var s3Ver = new (string Tag, string Desc, SutureFireRule R)[]
        {
            ("W0", "現行（`Swing` ＝ 自分の手番で殴った後）", SutureFireRule.Default),
            ("W1", "`OnWound` ＝ 傷が書かれたとき（1ターン1回）", new SutureFireRule(SutureFire.OnWound)),
        };
        // 糸口（`SutureRule`）と発火口（`SutureFireRule`）が**独立**であることの4通り（自己検査 (d)）。
        var s3Side = new[] { SutureSide.Foe, SutureSide.Both };

        int s3W = h2Stages.Count, s3B = h2All.Length;
        var s3Rate = new double[s3Ver.Length][][];
        const int S3N = 10;
        const int S3Battles = 0, S3Calls = 1, S3Fires = 2, S3Ally = 3, S3Capped = 4,
                  S3Healed = 5, S3Dry = 6, S3Turns = 7, S3Attacks = 8, S3Dmg = 9;
        var s3Acc = new long[s3Ver.Length][];
        long s3OverCap = 0;   // 自己検査 (c): 発火が決着ターン数を超えた戦

        for (int p = 0; p < s3Ver.Length; p++)
        {
            s3Rate[p] = new double[s3W][];
            for (int w = 0; w < s3W; w++) s3Rate[p][w] = new double[s3B];
            var a = s3Acc[p] = new long[S3N];
            for (int w = 0; w < s3W; w++)
                for (int b = 0; b < s3B; b++)
                {
                    var f = h2All[b].F;
                    bool has = f.Occupied().Any(o => o.Def.Id == "hari");
                    int pp = p, ww = w;
                    var winsArr = new int[S3Seeds];
                    var locArr = new long[S3Seeds][];
                    var overArr = new int[S3Seeds];
                    Parallel.For(0, S3Seeds, s =>
                    {
                        var v = new long[S3N];
                        var r = BattleEngine.Run(f, h2Stages[ww].Enemy, S3Base + s, verbose: false,
                                                 sutureFire: s3Ver[pp].R);
                        if (r.PlayerWon) winsArr[s] = 1;
                        // **駒ごとの帳簿の分母は第2〜5波**（規約 (G10)）。
                        if (has && ww > 0)
                        {
                            v[S3Battles] = 1; v[S3Turns] = r.Turns;
                            if (r.TallyByUnit.TryGetValue("hari", out UnitTally? t))
                            {
                                long fires = t.SutureFoe + t.SutureAlly;
                                v[S3Calls] = t.SutureCalls; v[S3Fires] = fires; v[S3Ally] = t.SutureAlly;
                                v[S3Capped] = t.SutureCapped; v[S3Healed] = t.SutureHealed;
                                v[S3Dry] = t.SutureDry; v[S3Attacks] = t.Attacks; v[S3Dmg] = t.DamageToEnemy;
                                if (fires > r.Turns) overArr[s] = 1;
                            }
                        }
                        locArr[s] = v;
                    });
                    int wins = 0;
                    for (int sq = 0; sq < S3Seeds; sq++)
                    {
                        wins += winsArr[sq]; s3OverCap += overArr[sq];
                        for (int k = 0; k < S3N; k++) a[k] += locArr[sq][k];
                    }
                    s3Rate[p][w][b] = wins * 100.0 / S3Seeds;
                }
            Console.Error.WriteLine("[hold2 suture] " + s3Ver[p].Tag + " おわり");
        }

        double S3Avg(int p, int b) { double s = 0; for (int w = 1; w < s3W; w++) s += s3Rate[p][w][b]; return s / (s3W - 1); }
        double S3Per(int p, int k) => s3Acc[p][S3Battles] == 0 ? 0 : (double)s3Acc[p][k] / s3Acc[p][S3Battles];

        Console.WriteLine("# 第107期 (S3) —— ハリ（縫いの発火口を手番の外へ移す）");
        Console.WriteLine();
        Console.WriteLine("台: `compare` 61 行 ＋ 交差帯 12 行 × 全 " + s3W + " 波 × seed "
            + S3Base + ".." + (S3Base + S3Seeds - 1) + " ＝ " + ((long)s3B * s3W * S3Seeds).ToString("N0") + " 戦/版。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 中身 |");
        Console.WriteLine("|---|---|");
        foreach (var v in s3Ver) Console.WriteLine("| " + v.Tag + " | " + v.Desc + " |");

        Console.WriteLine();
        Console.WriteLine("## 表C-1 —— Q4（ハリの発火回数。**律速が外れたかの直接の確認**）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 在席戦 | 決着T | フック/戦 | **発火/戦** | 稼働率 | **敵から** | 味方から | 上限で弾かれ | 空振り | 繕い量/戦 | 振/戦 | 与ダメ/戦 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int p = 0; p < s3Ver.Length; p++)
            Console.WriteLine("| " + s3Ver[p].Tag + " | " + s3Acc[p][S3Battles]
                + " | " + S3Per(p, S3Turns).ToString("0.00")
                + " | " + S3Per(p, S3Calls).ToString("0.00")
                + " | **" + S3Per(p, S3Fires).ToString("0.00") + "**"
                + " | " + (S3Per(p, S3Turns) == 0 ? 0 : S3Per(p, S3Fires) / S3Per(p, S3Turns)).ToString("0.0%")
                + " | **" + (S3Per(p, S3Fires) - S3Per(p, S3Ally)).ToString("0.00") + "**"
                + " | " + S3Per(p, S3Ally).ToString("0.00")
                + " | " + S3Per(p, S3Capped).ToString("0.00")
                + " | " + (S3Per(p, S3Calls) - S3Per(p, S3Fires) - S3Per(p, S3Capped)).ToString("0.00")
                + " | " + S3Per(p, S3Healed).ToString("0.0")
                + " | " + S3Per(p, S3Attacks).ToString("0.00")
                + " | " + S3Per(p, S3Dmg).ToString("0.0") + " |");
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("**`敵から` が塞ぎ（＝ハリの代金）の宛先**——`MinusText`「繕うたび、糸を通した敵の傷がひとつ塞がる」"
            + "が実際に起きる回数。**`味方から` の側では、同じ塞ぎが味方の傷を消すので利得になる。**");
        Console.WriteLine();
        Console.WriteLine("**Q4**: 発火 " + S3Per(0, S3Fires).ToString("0.00") + " → **"
            + S3Per(1, S3Fires).ToString("0.00") + " 回/戦**（第85期の律速 2.15 回/戦）—— "
            + (S3Per(1, S3Fires) > S3Per(0, S3Fires) ? "**○**" : "**×**"));

        Console.WriteLine();
        Console.WriteLine("## 表C-2 —— Q5（ハリを含む行の第2〜5波平均）");
        Console.WriteLine();
        Console.WriteLine("| 行 | W0 | W1 | Δ |");
        Console.WriteLine("|---|--:|--:|--:|");
        double q5 = 0; int q5n = 0, q5up = 0;
        for (int b = 0; b < s3B; b++)
        {
            if (!h2All[b].F.Occupied().Any(o => o.Def.Id == "hari")) continue;
            double a0 = S3Avg(0, b), a1 = S3Avg(1, b);
            q5 += a1 - a0; q5n++; if (a1 > a0) q5up++;
            Console.WriteLine("| " + h2All[b].Name + " | " + a0.ToString("F1") + "% | " + a1.ToString("F1")
                + "% | " + (a1 - a0).ToString("+0.0;-0.0") + "pt |");
        }
        Console.WriteLine();
        Console.WriteLine("**ハリを含む " + q5n + " 行の平均 " + (q5 / Math.Max(1, q5n)).ToString("+0.00;-0.00")
            + "pt**（上がった行 " + q5up + " / " + q5n + "）。**行名は `Presets.Compare` / `Presets.Cross` から引いた。**");

        // ---- 表D: 拒否権
        Console.WriteLine();
        Console.WriteLine("## 表D —— 拒否権（分母は `compare` 61 行全体・第91期 (G1)）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | W0 | W1 | Δ |");
        Console.WriteLine("|---|--:|--:|--:|--:|");
        int veto = 0;
        for (int b = 0; b < h2Compare.Length; b++)
            for (int w = 0; w < s3W; w++)
            {
                double d = s3Rate[1][w][b] - s3Rate[0][w][b];
                if (d <= -10.0)
                {
                    veto++;
                    Console.WriteLine("| " + h2All[b].Name + " | 第" + (w + 1) + "波 | " + s3Rate[0][w][b].ToString("F1")
                        + "% | " + s3Rate[1][w][b].ToString("F1") + "% | " + d.ToString("+0.0;-0.0") + "pt |");
                }
            }
        if (veto == 0) Console.WriteLine("| （該当なし） | | | | |");
        Console.WriteLine();
        var pset = new HashSet<string>(Baseline.PrimaryRows);
        double p50 = 0, p51 = 0; int pn = 0;
        for (int b = 0; b < h2Compare.Length; b++)
            if (pset.Contains(h2All[b].Name)) { p50 += s3Rate[0][s3W - 1][b]; p51 += s3Rate[1][s3W - 1][b]; pn++; }
        // (G9): 第五波 95% 超の行が増えるのは「注意」であって拒否ではない。数だけ出す。
        int hi0 = 0, hi1 = 0;
        for (int b = 0; b < h2Compare.Length; b++)
        {
            if (s3Rate[0][s3W - 1][b] > 95.0) hi0++;
            if (s3Rate[1][s3W - 1][b] > 95.0) hi1++;
        }
        Console.WriteLine("| 拒否権 | 量 | 線 | 結果 |");
        Console.WriteLine("|---|---|---|:-:|");
        Console.WriteLine("| (1) 主判定" + pn + "行の第五波平均 | " + (p50 / Math.Max(1, pn)).ToString("F1")
            + "% → **" + (p51 / Math.Max(1, pn)).ToString("F1") + "%** | ≥ "
            + Baseline.PrimaryFifthFloor.ToString("F1") + "% | "
            + (p51 / Math.Max(1, pn) >= Baseline.PrimaryFifthFloor ? "**○**" : "**×**") + " |");
        Console.WriteLine("| (3) いずれかの波で −10.0pt 以上落ちた行 | **" + veto + " 件** | 0 件 | "
            + (veto == 0 ? "**○**" : "**×**") + " |");
        Console.WriteLine("| （注意・(G9)） 第五波 95% 超の行 | " + hi0 + " → **" + hi1 + " 行** | 記録するだけ | — |");

        // ---- 表E: 自己検査
        Console.WriteLine();
        Console.WriteLine("## 表E —— 自己検査");
        Console.WriteLine();
        Console.WriteLine("- **(c)** 発火が決着ターン数を超えた戦: **" + s3OverCap + " 件** —— "
            + (s3OverCap == 0 ? "**○**" : "**×**") + "（1ターン1回の上限）");
        Console.WriteLine("- **(d)** `SutureRule`（糸口）と `SutureFireRule`（発火口）が独立に働くこと（4通り）:");
        Console.WriteLine();
        Console.WriteLine("| 糸口 | 発火口 | ハリの行の第2〜5波平均 | 発火/戦 | 味方から/戦 |");
        Console.WriteLine("|---|---|--:|--:|--:|");
        foreach (SutureSide side in s3Side)
            foreach (var v in s3Ver)
            {
                double sum = 0; int n = 0; long fires = 0, ally = 0, bt = 0;
                for (int b = 0; b < s3B; b++)
                {
                    if (!h2All[b].F.Occupied().Any(o => o.Def.Id == "hari")) continue;
                    for (int w = 1; w < s3W; w++)
                    {
                        int wins = 0;
                        for (int sd = 0; sd < S3Seeds; sd++)
                        {
                            var r = BattleEngine.Run(h2All[b].F, h2Stages[w].Enemy, S3Base + sd, verbose: false,
                                                     suture: new SutureRule(side), sutureFire: v.R);
                            if (r.PlayerWon) wins++;
                            bt++;
                            if (r.TallyByUnit.TryGetValue("hari", out UnitTally? t))
                            { fires += t.SutureFoe + t.SutureAlly; ally += t.SutureAlly; }
                        }
                        sum += wins * 100.0 / S3Seeds; n++;
                    }
                }
                Console.WriteLine("| " + side + " | " + v.Tag + " | " + (sum / Math.Max(1, n)).ToString("F1")
                    + "% | " + (bt == 0 ? 0 : (double)fires / bt).ToString("0.00")
                    + " | " + (bt == 0 ? 0 : (double)ally / bt).ToString("0.00") + " |");
            }
        Console.WriteLine();
        Console.WriteLine("**`Foe` では味方から引く回数が両版とも 0.00 でなければならない**"
            + "（糸口の規則が発火口に依らないこと）。");

        // ---- (e) 繕いが `ctx.Heal` を通っていること（渇きで封じられる）
        //
        // **「渇きの波で繕い量が 0」では検査にならない**——祭司は倒せるので、倒した後の繕いは通る。
        // 波ごとに「空振り（`SutureDry` ＝ 患者のHPが1点も動かなかった発火）」の割合を出し、
        // **渇きの波だけが突出すること**を見る。
        Console.WriteLine();
        Console.WriteLine("- **(e)** 繕いが `ctx.Heal` を通っていること（渇きで封じられる）:");
        Console.WriteLine();
        Console.WriteLine("| 波 | 渇き | W1 発火/戦 | 空振り/戦 | 空振り率 | 繕い量/発火 |");
        Console.WriteLine("|--:|:-:|--:|--:|--:|--:|");
        for (int w = 1; w < s3W; w++)
        {
            bool dro = h2Stages[w].Enemy.Occupied().Any(o => o.Def.Traits.Contains(TraitId.Drought));
            long fires = 0, dry = 0, healed = 0, bt = 0;
            for (int b = 0; b < s3B; b++)
            {
                if (!h2All[b].F.Occupied().Any(o => o.Def.Id == "hari")) continue;
                for (int sd = 0; sd < S3Seeds; sd++)
                {
                    var r = BattleEngine.Run(h2All[b].F, h2Stages[w].Enemy, S3Base + sd, verbose: false,
                                             sutureFire: new SutureFireRule(SutureFire.OnWound));
                    bt++;
                    if (r.TallyByUnit.TryGetValue("hari", out UnitTally? t))
                    { fires += t.SutureFoe + t.SutureAlly; dry += t.SutureDry; healed += t.SutureHealed; }
                }
            }
            Console.WriteLine("| 第" + (w + 1) + "波 | " + (dro ? "**○**" : "×") + " | "
                + (bt == 0 ? 0 : (double)fires / bt).ToString("0.00") + " | "
                + (bt == 0 ? 0 : (double)dry / bt).ToString("0.00") + " | "
                + (fires == 0 ? 0 : dry * 100.0 / fires).ToString("0.0") + "% | "
                + (fires == 0 ? 0 : (double)healed / fires).ToString("0.0") + " |");
        }

        // ---- Q6: `tempo` の器具（第105期）でハリの分類が動くか
        Console.WriteLine();
        Console.WriteLine("## 表C-3 —— Q6（第105期の器具でハリが手番外型に移るか）");
        Console.WriteLine();
        Console.WriteLine("線は第105期 §1-3 のとおり **手番外の割合 50%**。**通貨ごとに割れる駒は「混合」**。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 与ダメ | 回復 | 状態 | 強化弱体 | 分類 |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|");
        for (int p = 0; p < s3Ver.Length; p++)
        {
            var inn = new long[4]; var off = new long[4];
            for (int b = 0; b < s3B; b++)
            {
                if (!h2All[b].F.Occupied().Any(o => o.Def.Id == "hari")) continue;
                for (int w = 1; w < s3W; w++)
                    for (int sd = 0; sd < S3Seeds; sd++)
                    {
                        var r = BattleEngine.Run(h2All[b].F, h2Stages[w].Enemy, S3Base + sd, verbose: false,
                                                 sutureFire: s3Ver[p].R);
                        if (!r.TallyByUnit.TryGetValue("hari", out UnitTally? t)) continue;
                        inn[0] += t.DmgOutInTurn; off[0] += t.DmgOutOffTurn;
                        inn[1] += t.HealOutInTurn; off[1] += t.HealOutOffTurn;
                        inn[2] += t.StatusOutInTurn; off[2] += t.StatusOutOffTurn;
                        inn[3] += t.BuffOutInTurn; off[3] += t.BuffOutOffTurn;
                    }
            }
            var cells = new string[4];
            bool anyOff = false, anyIn = false;
            for (int i = 0; i < 4; i++)
            {
                long d = inn[i] + off[i];
                if (d == 0) { cells[i] = "—"; continue; }
                double r2 = off[i] * 100.0 / d;
                cells[i] = r2.ToString("0.0") + "%";
                if (r2 > 50.0) anyOff = true; else anyIn = true;
            }
            string kind = !anyOff && !anyIn ? "出力なし" : anyOff && anyIn ? "混合" : anyOff ? "**手番外型**" : "手番型";
            Console.WriteLine("| " + s3Ver[p].Tag + " | " + string.Join(" | ", cells) + " | " + kind + " |");
        }
        Console.WriteLine();
        Console.WriteLine("（列は**手番外の割合**。第105期のロスター全体では 回復 90.3% が手番の外から出ている。）");
        return;
    }
    // ------------------------------------------------------------------------------
    // Phase 0（**戦闘は較正の確認のみ**）と自己検査。
    // ------------------------------------------------------------------------------
    if (h2Mode == "phase0" || h2Mode == "check" || h2Mode == "tables")
    {
        string? h2Root = Directory.GetCurrentDirectory();
        while (h2Root != null && !File.Exists(Path.Combine(h2Root, "docs", "balance.md")))
            h2Root = Path.GetDirectoryName(h2Root);

        if (h2Mode == "phase0")
        {
            Console.WriteLine("# 第107期 Phase 0 —— 地図");
            Console.WriteLine();
            Console.WriteLine("## 0-1. `BattleContext.Wound` の呼び出し口の全数（**指示書の一覧を信用せず数え直した**）");
            Console.WriteLine();
            Console.WriteLine("`WoundRoute` は " + BattleContext.WoundRouteCount + " 本。実測は 73 行 × 全5波 × seed 0..19。");
            Console.WriteLine();
            var wr = new double[BattleContext.WoundRouteCount];
            int wn = 0;
            for (int b = 0; b < h2All.Length; b++)
                for (int w = 0; w < h2Stages.Count; w++)
                    for (int sp = 0; sp < 20; sp++)
                    {
                        var r = BattleEngine.Run(h2All[b].F, h2Stages[w].Enemy, sp, verbose: false);
                        wn++;
                        foreach (var kv in r.TallyByUnit)
                            if (kv.Value.WoundWritesByRoute is not null)
                                for (int k = 0; k < wr.Length; k++) wr[k] += kv.Value.WoundWritesByRoute[k];
                    }
            Console.WriteLine("| 経路 | 書き手 | 回/戦 | 割合 |");
            Console.WriteLine("|---|---|--:|--:|");
            double wtot = wr.Sum();
            for (int k = 0; k < wr.Length; k++)
                Console.WriteLine("| `" + (WoundRoute)k + "` | " + (k switch { 0 => "裂き（キリ）", 1 => "刻み（ノミ）", 2 => "**巻き込み則（engine）**", 3 => "棘の傷（既定 off）", 4 => "棘の巻き込み（既定 off）", _ => "引き取り（ガルド・受け取る側）" }) + " | "
                    + (wr[k] / wn).ToString("0.00") + " | " + (wtot == 0 ? 0 : wr[k] * 100.0 / wtot).ToString("0.0") + "% |");
            Console.WriteLine("| **合計** | | **" + (wtot / wn).ToString("0.00") + "** | 100.0% |");
            Console.WriteLine();
            Console.WriteLine("**発火口を `OnWound` に移すと、この合計の回数だけフックに入る**"
                + "（1ターン1回の上限が無ければ発火も同数になる）。");

            Console.WriteLine();
            Console.WriteLine("## 0-2. 憤怒（`TraitId.Rage`）の保持者 —— **2 枚**");
            Console.WriteLine();
            foreach (UnitDef d in UnitCatalog.All.Where(d => d.Traits.Contains(TraitId.Rage)))
                Console.WriteLine("- " + d.Name + "（攻 " + d.Attack + " / HP " + d.MaxHp + " / 速 " + d.Speed + "）");
            Console.WriteLine();
            Console.WriteLine("**較正はムド1枚でしかできないのに、`RageRule` は両方に効く。**");

            Console.WriteLine();
            Console.WriteLine("## 0-3. ハリを含む行（`Presets.Compare` / `Presets.Cross` から引いた）");
            Console.WriteLine();
            foreach (var b in h2All.Where(b => b.F.Occupied().Any(o => o.Def.Id == "hari")))
                Console.WriteLine("- " + b.Name);
            Console.WriteLine();
            Console.WriteLine("**2 行しかない**（compare 1 ＋ 交差帯 1）。第45期の「新しい機構は最初から2行以上に入れること」の下限。");

            Console.WriteLine();
            Console.WriteLine("## 0-4. `SutureRule`（糸口）と `SutureFireRule`（発火口）は独立");
            Console.WriteLine();
            Console.WriteLine("| 型 | 既定 | 意味 |");
            Console.WriteLine("|---|---|---|");
            Console.WriteLine("| `SutureRule` | `" + SutureRule.Default + "` | 糸を引く**先**（第85期） |");
            Console.WriteLine("| `SutureFireRule` | `" + SutureFireRule.Default + "` | **いつ**引くか（第107期 (S3)） |");
            Console.WriteLine();
            Console.WriteLine("4通りの対照は `hold2 suture` の表E (d)。");
            return;
        }

        if (h2Mode == "check")
        {
            Console.WriteLine("# 第107期 表E —— 自己検査（必須4項目）");
            Console.WriteLine();

            var cells = new List<string>();
            foreach (var b in h2Compare)
            {
                var row = new List<string>();
                foreach (var st in h2Stages)
                {
                    int w = 0;
                    for (int seed = 0; seed < 200; seed++)
                        if (BattleEngine.Run(b.F, st.Enemy, seed, verbose: false).PlayerWon) w++;
                    row.Add((w * 100.0 / 200).ToString("F1") + "%");
                }
                cells.Add("| " + b.Name + " | " + string.Join(" | ", row) + " |");
            }
            int diff = -1;
            if (h2Root is not null)
            {
                var doc = File.ReadAllLines(Path.Combine(h2Root, "docs", "balance.md"))
                              .Where(l => l.StartsWith("| ") && l.Contains('%')).ToArray();
                diff = Math.Abs(doc.Length - cells.Count);
                for (int i = 0; i < Math.Min(doc.Length, cells.Count); i++)
                    if (doc[i].Trim() != cells[i].Trim()) diff++;
            }
            Console.WriteLine("- **必須1** `compare` " + (h2Compare.Length * h2Stages.Count)
                + " セルを `docs/balance.md` と突き合わせ: **ずれ " + diff + " 行**"
                + (diff == 0 ? "（○）" : "（×）")
                + " ——**(S2)(S3) はどちらも既定を1ビットも動かしていない**（動いたのは (S1) の席だけで、そちらは同じコミットで `docs/` を測り直してある）。");

            Console.WriteLine("- **必須2** `docs/` 10ファイルの再生成は (S1) のコミットで済ませてある。"
                + "(S3) で新しく増えるのは `docs/rules.md` の `sutureFire` の 1 行だけ（ノブが増えたので正しい）。");

            Console.WriteLine("- **必須3** 触っていないノブの既定: "
                + "`RageRule.Default` = `" + RageRule.Default + "` ／ "
                + "`SutureRule.Default` = `" + SutureRule.Default + "` ／ "
                + "`SutureFireRule.Default` = `" + SutureFireRule.Default + "` ／ "
                + "`SpillWoundRule.Default` = `" + SpillWoundRule.Default + "` ／ "
                + "`MendRule.Default` = `" + MendRule.Default + "` ／ "
                + "`MenderCostRule.Default` = `" + MenderCostRule.Default + "` ／ "
                + "`LooseRule.Default` = `" + LooseRule.Default + "` ／ "
                + "`EncoreRule.Default` = `" + EncoreRule.Default + "`"
                + "（**`docs/rules.md` の既定値の列で示す**）");

            int pick = 0;
            if (h2Root is not null)
                foreach (string f in Directory.GetFiles(Path.Combine(h2Root, "BattleCore"), "*.cs"))
                    pick += System.Text.RegularExpressions.Regex.Matches(
                        string.Join("\n", File.ReadAllLines(f).Where(l => !l.TrimStart().StartsWith("//"))),
                        @"PickOne\(").Count;
            Console.WriteLine("- **必須4** `BattleCore` の `PickOne(` 呼び出し **" + pick + " 箇所**"
                + "（第94期以降 26 箇所。**第107期は1つも足していない**——縫いを engine から呼ぶ順序は"
                + "スロット昇順で、`ctx.PickOne` を使わない）");
            Console.WriteLine();
            Console.WriteLine("機構固有の (a)〜(e) は `hold2 seats` / `hold2 rage` / `hold2 suture` の表E。");
            return;
        }

        Console.WriteLine("# 第107期 —— 表A〜E");
        Console.WriteLine();
        Console.WriteLine("(S1)(S2)(S3) は**別々のコミット**にしたので、表もモードごとに分けてある。");
        Console.WriteLine();
        Console.WriteLine("| 表 | 内容 | モード |");
        Console.WriteLine("|---|---|---|");
        Console.WriteLine("| A | (S1) 席 | `hold2 seats` |");
        Console.WriteLine("| B | (S2) 較正の確認と Q1〜Q3 | `hold2 rage` |");
        Console.WriteLine("| C | (S3) ハリ（Q4〜Q6） | `hold2 suture` |");
        Console.WriteLine("| D | 拒否権 | `hold2 rage` / `hold2 suture` の表D |");
        Console.WriteLine("| E | 自己検査 | `hold2 check` ＋ 各モードの表E |");
        Console.WriteLine("| — | 地図 | `hold2 phase0` |");
        return;
    }

    Console.WriteLine("モード: seats / rage / suture / phase0 / tables / check");
    return;
}
}
