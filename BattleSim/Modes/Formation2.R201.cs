using BattleCore;
using static Common;

// form2 run201 / check201（第201期）。席の選び方と Phase 0 は `Formation2.P201.cs`。

static partial class Formation2Diag
{
    static Formation ReplaceRef(Formation f, UnitDef from, UnitDef to)
    {
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = ReferenceEquals(d, from) ? to : d;
        return g;
    }

    static M MeasB(Formation f, bool ledger = true) => Measure(f, ledger, MeasSeed0, MeasSeeds);

    static List<UnitDef> Members(Formation f) => f.Occupied().Select(o => o.Def).ToList();

    static string SeatOf(Formation f, Func<UnitDef, bool> who)
    {
        foreach ((int slot, UnitDef d) in f.Occupied()) if (who(d)) return SeatName(f.Shape, slot);
        return "—";
    }

    static string PickNote(Pick p) => PickPct(p.Score).ToString("F1") + "（鏡像 " + (p.MirrorScore < 0 ? "—" : PickPct(p.MirrorScore).ToString("F1")) + "・2位 " + PickPct(p.Second).ToString("F1") + "）";

    static partial void Run201Impl(string arg)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("# 第201期 `form2 run201` —— 席を選んで測り直す");
        Console.WriteLine();
        Console.WriteLine("**選ぶ**: 120 通り × 第2〜5波 × seed " + PickSeed0 + ".." + (PickSeed0 + PickSeeds - 1) + " の勝ち数の合計が最大（同値は列挙順で最初）。"
                          + "**測る**: 選んだ並びを seed " + MeasSeed0 + ".." + (MeasSeed0 + MeasSeeds - 1) + " で（**平均は第2〜5波**・規約 (G10)）。"
                          + "`選ぶ試行` の列は選んだ並びの選ぶ試行での勝率（鏡像＝1レーン↔3レーンを入れ替えた並び・2位＝次点の並び）。");
        Console.WriteLine();
        Console.WriteLine("第200期の列（`X・200` ／ `P2・200`）は第200期の並びそのままを**同じ測る帯で測り直した値**（第200期の報告の数字は seed 0..199）。");
        Console.WriteLine();

        var ledgerRows = new List<(string Row, string Col, Formation F, M X)>();
        var bestRows = new List<(string Row, string Col, Formation F)>();

        // ---------------- 表A ----------------
        Console.WriteLine("## 表A —— 毒パ（第200期と同じ7行）");
        Console.WriteLine();
        Console.WriteLine("元 ＝ `compare` の行のまま（X 字・ガルドあり）。X・選 ／ P2・選 は第200期の表A の P2 と同じ5枚（スィド・ベニ入り・ガルド抜き）を並べ替える。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 元 | X・200 | P2・200 | **X・選** | **P2・選** | P2・選 − X・選 | P2・選 − P2・200 | P2・選 の前衛 | 選ぶ試行 X・選 | 選ぶ試行 P2・選 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|---|---|---|");
        var rowsA = SidDiag.MainForms197();
        double[] sum = new double[5]; int nA = 0;
        var picksP = new List<(string Name, Pick P)>();
        foreach (var (name, orig, _, s) in rowsA)
        {
            UnitDef sid = s.Occupied().First(o => ReferenceEquals(o.Def, SidDiag.SidS)).Def;
            UnitDef beni = s.Occupied().First(o => o.Def.Id == "beni").Def;
            Formation p200 = ToDiamond(s, sid, beni, new[] { 0, 4, 1 });
            var mem = Members(s);
            Pick px = Select(mem, FormationShape.X), pp = Select(mem, FormationShape.Diamond);
            picksP.Add((name, pp));
            M mo = MeasB(orig, false), mx2 = MeasB(s, false), mp2 = MeasB(p200, true);
            M mx = MeasB(px.Best), mp = MeasB(pp.Best);
            ledgerRows.Add((name, "X・選", px.Best, mx)); ledgerRows.Add((name, "P2・選", pp.Best, mp)); ledgerRows.Add((name, "P2・200", p200, mp2));
            bestRows.Add((name, "X・選", px.Best)); bestRows.Add((name, "P2・選", pp.Best));
            double o = Mean25(mo.W), x2 = Mean25(mx2.W), p2 = Mean25(mp2.W), x = Mean25(mx.W), p = Mean25(mp.W);
            bool broken = name.StartsWith("追撃×毒");
            if (!broken) { sum[0] += o; sum[1] += x2; sum[2] += p2; sum[3] += x; sum[4] += p; nA++; }
            Console.WriteLine("| " + name + (broken ? "（**読まない**）" : "") + " | " + o.ToString("F1") + " | " + x2.ToString("F1") + " | " + p2.ToString("F1") + " | **"
                              + x.ToString("F1") + "** | **" + p.ToString("F1") + "** | " + Dp(p - x) + " | " + Dp(p - p2) + " | " + pp.Best[3]!.Name + " | "
                              + PickNote(px) + " | " + PickNote(pp) + " |");
        }
        Console.WriteLine("| **平均（`追撃×毒` を除く " + nA + " 行）** | " + string.Join(" | ", sum.Select((v, i) => (i >= 3 ? "**" : "") + (v / nA).ToString("F1") + (i >= 3 ? "**" : "")))
                          + " | " + Dp((sum[4] - sum[3]) / nA) + " | " + Dp((sum[4] - sum[2]) / nA) + " | | | |");
        Console.WriteLine();
        Console.WriteLine("- 第200期の P2 − X・スィド（この帯で測り直し）: " + Dp((sum[2] - sum[1]) / nA) + "pt ／ 今期の P2・選 − X・選: " + Dp((sum[4] - sum[3]) / nA) + "pt");
        Console.WriteLine("- P2・選 で前衛にスィドが来た行: " + picksP.Count(t => ReferenceEquals(t.P.Best[3], SidDiag.SidS)) + " / " + picksP.Count);
        Console.WriteLine();

        // ---------------- 表B ----------------
        Console.WriteLine("## 表B —— タンクの土俵（第200期と同じ6行・同じタンクの入れ替え）");
        Console.WriteLine();
        Console.WriteLine("**前衛を固定しない**。括弧はタンクが座った席。第200期の列（`X・ガルド・200` ／ `P2・ガルド・200`）は同じ帯で測り直した値。");
        Console.WriteLine();
        (string Tag, UnitDef D)[] tanks = { ("ゴルム", UnitCatalog.Golm), ("ササ", UnitCatalog.Sasa), ("スィド", SidDiag.SidS) };
        Console.WriteLine("| 行 | X・ガルド・200 | P2・ガルド・200 | **X・ガルド・選** | **P2・ガルド・選** | " + string.Join(" | ", tanks.Select(t => "P2・" + t.Tag + "・選"))
                          + " | 差 P2・選（ガルド − 3枚平均） |");
        Console.WriteLine("|---|--:|--:|--:|--:|" + string.Concat(tanks.Select(_ => "--:|")) + "--:|");
        var sumB = new double[7]; int nB = 0, xOver = 0;
        var tankSeats = new List<string>();
        var tankPicks = new List<(string Row, string Tag, Pick P)>();
        foreach (string row in TankRows)
        {
            Formation x = CompareBuilds().First(r => r.Name == row).F;
            UnitDef gald = x.Occupied().First(o => o.Def.Id == "gald").Def;
            Formation p200 = ToDiamond(x, gald, null, new[] { 0, 2, 4, 1 });
            var mem = Members(x);
            Pick pxg = Select(mem, FormationShape.X), ppg = Select(mem, FormationShape.Diamond);
            M mx200 = MeasB(x, false), mp200 = MeasB(p200, false), mxg = MeasB(pxg.Best), mpg = MeasB(ppg.Best);
            ledgerRows.Add((row, "X・ガルド・選", pxg.Best, mxg)); ledgerRows.Add((row, "P2・ガルド・選", ppg.Best, mpg));
            bestRows.Add((row, "X・ガルド・選", pxg.Best)); bestRows.Add((row, "P2・ガルド・選", ppg.Best));
            tankPicks.Add((row, "ガルド", ppg));
            var cells = new List<string>(); var vals = new List<double>();
            foreach (var (tag, d) in tanks)
            {
                var memT = mem.Select(u => ReferenceEquals(u, gald) ? d : u).ToList();
                Pick pt = Select(memT, FormationShape.Diamond);
                M mt = MeasB(pt.Best);
                ledgerRows.Add((row, "P2・" + tag + "・選", pt.Best, mt)); bestRows.Add((row, "P2・" + tag + "・選", pt.Best));
                tankPicks.Add((row, tag, pt));
                double v = Mean25(mt.W); vals.Add(v);
                cells.Add(v.ToString("F1") + "（" + SeatOf(pt.Best, u => ReferenceEquals(u, d)) + "）");
            }
            double xg = Mean25(mxg.W), pg = Mean25(mpg.W);
            if (xg > pg) xOver++;
            double[] rowv = { Mean25(mx200.W), Mean25(mp200.W), xg, pg, vals[0], vals[1], vals[2] };
            for (int i = 0; i < 7; i++) sumB[i] += rowv[i];
            nB++;
            Console.WriteLine("| " + row + " | " + rowv[0].ToString("F1") + " | " + rowv[1].ToString("F1") + " | **" + xg.ToString("F1") + "**（" + SeatOf(pxg.Best, u => ReferenceEquals(u, gald))
                              + "） | **" + pg.ToString("F1") + "**（" + SeatOf(ppg.Best, u => ReferenceEquals(u, gald)) + "） | " + string.Join(" | ", cells) + " | " + Dp(pg - vals.Average()) + " |");
        }
        Console.WriteLine("| **平均** | " + string.Join(" | ", sumB.Select(v => (v / nB).ToString("F1"))) + " | " + Dp((sumB[3] - (sumB[4] + sumB[5] + sumB[6]) / 3) / nB) + " |");
        Console.WriteLine();
        Console.WriteLine("- X・ガルド・選 が P2・ガルド・選 より上の行: **" + xOver + " / " + nB + "**");
        Console.WriteLine("- タンクが前衛に座った並び（P2・選 の 24 並び）: " + tankPicks.Count(t => t.P.Best[3]!.Id == (t.Tag switch { "ガルド" => "gald", "ゴルム" => "golm", "ササ" => "sasa", _ => "sid" })) + " / " + tankPicks.Count);
        Console.WriteLine("- 選ぶ試行（P2・選）: " + string.Join(" ／ ", tankPicks.Select(t => t.Row.Split(' ')[0] + "・" + t.Tag + " " + PickPct(t.P.Score).ToString("F1") + "（鏡像 " + PickPct(t.P.MirrorScore).ToString("F1") + "）")));
        Console.WriteLine();

        // ---------------- 表C ----------------
        Console.WriteLine("## 表C —— スィドの版（パターン2の前衛）");
        Console.WriteLine();
        Console.WriteLine("表A の P2・選 の並びを固定し、スィドの版だけを替える。**スィドが前衛にいない行は、スィドが前衛の 24 通りの中の最良（選ぶ試行は規定の版 S）でも測る**。"
                          + "`≤2T` ＝ 前衛が2ターン目までに倒れた戦の割合（第2〜5波）。");
        Console.WriteLine();
        (string Tag, UnitDef D)[] vers = { ("S", SidDiag.SidS), ("S+反", SidDiag.SidRe), ("S+HP", SidDiag.SidHp) };
        Console.WriteLine("| 行 | 並び | 前衛 | 勝率 S ／ S+反 ／ S+HP | ≤2T S ／ S+反 ／ S+HP | 前衛が倒れた S ／ S+反 ／ S+HP |");
        Console.WriteLine("|---|---|---|---|---|---|");
        var sumC = new Dictionary<string, (double W, double E, double D, int N)>();
        foreach (var (name, pp) in picksP)
        {
            var arrs = new List<(string Tag, Formation F)> { ("P2・選", pp.Best) };
            if (!ReferenceEquals(pp.Best[3], SidDiag.SidS))
            {
                int bi = -1;
                for (int i = 0; i < pp.All.Count; i++)
                    if (ReferenceEquals(pp.All[i][3], SidDiag.SidS) && (bi < 0 || pp.Scores[i] > pp.Scores[bi])) bi = i;
                arrs.Add(("前衛スィドの最良", pp.All[bi]));
                bestRows.Add((name, "P2・前衛スィド", pp.All[bi]));
            }
            foreach (var (tag, f) in arrs)
            {
                var ms = vers.Select(v => MeasB(ReplaceRef(f, SidDiag.SidS, v.D))).ToArray();
                if (tag == arrs[^1].Tag)   // 前衛スィドの並び（D のスィドを見るのが目的）で7行平均を取る
                    for (int i = 0; i < vers.Length; i++)
                    {
                        var c = sumC.GetValueOrDefault(vers[i].Tag);
                        sumC[vers[i].Tag] = (c.W + Mean25(ms[i].W), c.E + ms[i].FrontEarly / ms[i].N, c.D + ms[i].FrontDead / ms[i].N, c.N + 1);
                    }
                Console.WriteLine("| " + name + " | " + tag + " | " + f[3]!.Name + " | " + string.Join(" ／ ", ms.Select(m => Mean25(m.W).ToString("F1"))) + " | "
                                  + string.Join(" ／ ", ms.Select(m => Pc(m.FrontEarly, m.N))) + " | " + string.Join(" ／ ", ms.Select(m => Pc(m.FrontDead, m.N))) + " |");
            }
        }
        Console.WriteLine();
        Console.WriteLine("前衛がスィドの並びの7行平均: " + string.Join(" ／ ", vers.Select(v => v.Tag + " 勝率 " + (sumC[v.Tag].W / sumC[v.Tag].N).ToString("F1")
                          + "・≤2T " + (100 * sumC[v.Tag].E / sumC[v.Tag].N).ToString("F1") + "%・倒れ " + (100 * sumC[v.Tag].D / sumC[v.Tag].N).ToString("F1") + "%")));
        Console.WriteLine();

        // ---------------- 帳簿 ----------------
        Console.WriteLine("## 帳簿（最良の並びの戦・第2〜5波・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("前衛＝開戦時に一番前の列の編成の駒（X 字は前1・前3 の2体、P2 は前衛の1体）。受けた段は前衛が倒れるまでに受けた攻撃の段数。巻き込みは敵の薙ぎ・貫き1回に当たった味方の人数。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 列 | 前衛 | 前衛が倒れた | ≤2T | 倒れたT | 受けた段 単/薙/貫/全 | 薙ぎ1回の人数 | 貫き1回の人数 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|--:|--:|");
        double[] swX = new double[2], swP = new double[2], piX = new double[2], piP = new double[2];
        foreach (var (row, col, f, q) in ledgerRows)
        {
            double n = q.N;
            bool isP = f.Shape == FormationShape.Diamond;
            if (isP) { swP[0] += q.SweepHit; swP[1] += q.SweepSwings; piP[0] += q.PierceHit; piP[1] += q.PierceSwings; }
            else { swX[0] += q.SweepHit; swX[1] += q.SweepSwings; piX[0] += q.PierceHit; piX[1] += q.PierceSwings; }
            string front = isP ? f[3]!.Name : (f[0]?.Name ?? "—") + "・" + (f[1]?.Name ?? "—");
            Console.WriteLine("| " + row + " | " + col + " | " + front + " | " + Pc(q.FrontDead, n) + " | " + Pc(q.FrontEarly, n) + " | " + Av(q.FrontDeadTurn, q._fdCount) + " | "
                              + Av(q.FrontByPat0, n) + " / " + Av(q.FrontByPat1, n) + " / " + Av(q.FrontByPat2, n) + " / " + Av(q.FrontByPat3, n) + " | "
                              + Av(q.SweepHit, q.SweepSwings) + " | " + Av(q.PierceHit, q.PierceSwings) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 巻き込み人数（全行を合わせて）: 薙ぎ X 字 **" + Av(swX[0], swX[1]) + "** 人 対 P2 **" + Av(swP[0], swP[1]) + "** 人 ／ 貫き X 字 **" + Av(piX[0], piX[1]) + "** 人 対 P2 **" + Av(piP[0], piP[1]) + "** 人");
        Console.WriteLine();

        // ---------------- 席の傾向 ----------------
        Console.WriteLine("## 最良の並び（ポンが遊ぶときの初期配置）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 列 | 並び |");
        Console.WriteLine("|---|---|---|");
        foreach (var (row, col, f) in bestRows) Console.WriteLine("| " + row + " | " + col + " | " + SeatsNamed(f) + " |");
        Console.WriteLine();
        var p2Best = bestRows.Where(b => b.F.Shape == FormationShape.Diamond && !b.Col.Contains("前衛スィド")).ToList();
        Console.WriteLine("席の傾向（P2・選 の " + p2Best.Count + " 並び）:");
        Console.WriteLine();
        foreach (int seat in new[] { 3, 1 })
            Console.WriteLine("- " + DiamondSeatNames[seat] + ": " + string.Join("、", p2Best.GroupBy(b => b.F[seat]!.Name).OrderByDescending(g => g.Count()).Select(g => g.Key + " " + g.Count())));
        var xBest = bestRows.Where(b => b.F.Shape == FormationShape.X).ToList();
        Console.WriteLine("- （X・選 の " + xBest.Count + " 並びの前列）: " + string.Join("、", xBest.SelectMany(b => new[] { b.F[0]!.Name, b.F[1]!.Name }).GroupBy(n => n).OrderByDescending(g => g.Count()).Select(g => g.Key + " " + g.Count())));
        Console.WriteLine();
        Console.WriteLine("所要 " + sw.Elapsed.TotalSeconds.ToString("F0") + " 秒");
    }

    // =================================================================================
    // check201
    // =================================================================================

    static partial void Check201Impl(string arg)
    {
        Console.WriteLine("# 第201期 `form2 check201` —— 自己検査");
        Console.WriteLine();
        bool ok = true;

        Console.WriteLine("## 席の選び方が決定的（同じ行を2回選んで一致）");
        Console.WriteLine();
        var (name, _, _, s) = SidDiag.MainForms197()[0];
        Pick a = Select(Members(s), FormationShape.Diamond), b = Select(Members(s), FormationShape.Diamond);
        bool det = a.Index == b.Index && a.Scores.SequenceEqual(b.Scores);
        ok &= det;
        Console.WriteLine("- " + name + " の P2・選 を2回: 最良 #" + a.Index + " ／ #" + b.Index + "・120 通りの勝ち数 " + (a.Scores.SequenceEqual(b.Scores) ? "一致" : "**ずれた**") + " → " + (det ? "○" : "**×**"));
        Console.WriteLine("- 新しいコード（`Formation2.P201.cs` / `Formation2.R201.cs`）は `BattleEngine.Run` を決まった順（120 通りの列挙順 × 波 × seed）で呼ぶだけ。乱数を自分で引かない");
        string root = FindRoot();
        string src = File.ReadAllText(Path.Combine(root, "BattleSim", "Modes", "Formation2.P201.cs")) + File.ReadAllText(Path.Combine(root, "BattleSim", "Modes", "Formation2.R201.cs"));
        int rnd = Count(src, "new " + "Random") + Count(src, ".Pick" + "One(") + Count(src, ".Ro" + "ll(");
        ok &= rnd == 0;
        Console.WriteLine("- 乱数の呼び出し（`new " + "Random` / `PickOne` / `Roll`）: " + rnd + " → " + (rnd == 0 ? "○" : "**×**"));
        Console.WriteLine();

        Console.WriteLine("## パターン2の席名が盤の位置と合う（編成画面の表示名）");
        Console.WriteLine();
        Console.WriteLine("| 枠 | 表示名 | 盤の席 | 列 | 格子のレーン | 一致 |");
        Console.WriteLine("|---|---|---|---|--:|:-:|");
        string[] wantRow = { "Mid", "Back", "Mid", "Front", "Mid" };
        int[] wantLane = { 1, 2, 2, 2, 3 };
        for (int i = 0; i < 5; i++)
        {
            int slot = FormationShape.Diamond.PlayableSlots[i];
            bool eq = FormationRules.RowOf(slot).ToString() == wantRow[i] && FormationShape.GridLane(slot) == wantLane[i];
            ok &= eq;
            Console.WriteLine("| " + (char)('A' + i) + " | " + DiamondSeatNames[i] + " | " + FormationRules.SeatNames[slot] + " | " + FormationRules.RowOf(slot) + " | " + FormationShape.GridLane(slot) + " | " + (eq ? "○" : "**×**") + " |");
        }
        Console.WriteLine();

        Console.WriteLine("## 第200期の自己検査（`compare` 0 セル差分・表の全セル・召喚）");
        Console.WriteLine();
        Check(arg);
        Console.WriteLine();
        Console.WriteLine("**第201期の総合（上の2節）: " + (ok ? "ok=True" : "ok=False") + "**");
    }
}
