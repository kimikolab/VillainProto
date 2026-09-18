using BattleCore;
using static Common;

// =====================================================================================
// stall モード（第152期） —— 膠着（30 ターン上限で負ける局面）を塞ぐ
//
// 指示書は design/PHASE152_STALL_SPEC.md ／ 報告は design/PHASE152_STALL.md。
//
// **問いは「盤面が成立するか」であって「強いか」ではない。**
// 30 ターン何も起きない絵が流れ、勝敗が上限ターンというメタルールで決まる局面を塞ぐ。
//
//     dotnet run --project BattleSim -c Release 0 stall phase0  # 膠着の分母を数える（V0 だけ）
//     dotnet run --project BattleSim -c Release 0 stall run     # 段C（Off / WhenFull / WhenStocked × compare 61行）
//     dotnet run --project BattleSim -c Release 0 stall check   # 自己検査（必須1・必須4 ＋ (a)〜(f)）
//
// **台を作らない。** 膠着は `compare` の帯の中で起きているので、帯全体を分母にする
// （第149期と同じ判断。台を選ぶこと自体が答えを作る）。
// =====================================================================================

static class StallDiag
{
    const int Seeds = 200;

    // **第152期 段C で既定が動いたので、V0 は `Default` ではなく明示の `Off` を指す**
    // （第60期「採用で既定が動いた診断は、検算の相手が V0 から V1 へ移る。
    // 採用したら、その期のうちに診断の V0 を作り直すこと」——放置すると V0 と採用版が同じものを指す）。
    static readonly ParryRule V0 = ParryRule.Default with { Swing = ParrySwing.Off };
    static readonly ParryRule VF = ParryRule.Default with { Swing = ParrySwing.WhenFull };
    static readonly ParryRule VS = ParryRule.Default with { Swing = ParrySwing.WhenStocked };

    public static void Run(string mode, string arg)
    {
        switch (mode)
        {
            case "phase0": Phase0(); return;
            case "run": Body(); return;
            case "check": Check(arg); return;
            default:
                Console.WriteLine("stall: モードは phase0 / run / check。");
                return;
        }
    }

    // =================================================================================
    // phase0 —— 膠着の分母を数える（規約 Q0-2: 線を引く前に分母を出す）
    // =================================================================================

    static void Phase0()
    {
        Console.WriteLine("# 第152期 `stall phase0` —— 膠着の分母を数える");
        Console.WriteLine();
        Console.WriteLine("**上限ターン到達 ＝ `BattleResult.Turns >= " + BattleEngine.MaxTurns + "`。**");
        Console.WriteLine("単発戦では `playerWon` が「味方が生存 **かつ** 敵が全滅」なので、"
                          + "**上限到達はそのまま敗北**である（引き分けの枝は無い）。");
        Console.WriteLine();

        var rows = CompareBuilds();
        var cap = new double[rows.Length][];
        Parallel.For(0, rows.Length, i =>
        {
            cap[i] = new double[5];
            for (int st = 0; st < 5; st++)
            {
                int n = 0;
                for (int seed = 0; seed < Seeds; seed++)
                    if (BattleEngine.Run(rows[i].F, EnemyCatalog.Stages[st].Enemy, seed,
                                         verbose: false, parry: V0).Turns >= BattleEngine.MaxTurns) n++;
                cap[i][st] = 100.0 * n / Seeds;
            }
        });

        int cells = 0, rowsHit = 0, cellsGald = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            int c = 0;
            for (int st = 1; st < 5; st++) if (cap[i][st] > 0) c++;
            if (c > 0) rowsHit++;
            cells += c;
            if (HasGald(rows[i].F)) cellsGald += c;
        }

        Console.WriteLine("## 表P-1. 分母（`compare` 61 行 × 第2〜5波 ＝ 244 セル・規約 (G10)）");
        Console.WriteLine();
        Console.WriteLine("| | 数 |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine("| 上限到達が 1 件でも出るセル | **" + cells + " / 244** |");
        Console.WriteLine("| そのセルを持つ行 | **" + rowsHit + " / " + rows.Length + "** |");
        Console.WriteLine("| うちガルドを含む行のセル | **" + cellsGald + " / " + cells + "** |");
        Console.WriteLine("| ガルドを含む行 | " + rows.Count(r => HasGald(r.F)) + " / " + rows.Length + " |");
        Console.WriteLine();
        Console.WriteLine("**この期の線（「膠着が解ける」）が読めるのはこの " + cells + " セルだけである。**");
        Console.WriteLine();

        Console.WriteLine("## 表P-2. 上限到達率が 0 でない行（第2〜5波・降順）");
        Console.WriteLine();
        Console.WriteLine("| 行 | ガルド | 第2波 | 第3波 | 第4波 | 第5波 | 第2〜5波 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|");
        foreach (int i in Enumerable.Range(0, rows.Length)
                     .Where(i => cap[i].Skip(1).Any(x => x > 0))
                     .OrderByDescending(i => cap[i].Skip(1).Average()))
            Console.WriteLine("| " + rows[i].Name + " | " + (HasGald(rows[i].F) ? "○" : "—") + " | "
                              + string.Join(" | ", cap[i].Skip(1).Select(F1))
                              + " | **" + F1(cap[i].Skip(1).Average()) + "** |");
        Console.WriteLine();
    }

    // =================================================================================
    // run —— 段C。**同じ帯・同じ seed で 3 版を並べる**（第147期の作法）
    // =================================================================================

    static void Body()
    {
        Console.WriteLine("# 第152期 `stall run` —— 段C: 構えを解いて殴らせる");
        Console.WriteLine();
        Console.WriteLine("**V0 ＝ 既定（`ParrySwing.Off`）／ F ＝ `WhenFull`（在庫 ≧ Uses）／ "
                          + "S ＝ `WhenStocked`（在庫 > 0）。**");
        Console.WriteLine("`compare` 61 行 × 5 波 × seed 0.." + (Seeds - 1)
                          + "。**判定の分母は第2〜5波**（規約 (G10)）。");
        Console.WriteLine();

        var rows = CompareBuilds();
        var v0 = new Res[rows.Length];
        var vf = new Res[rows.Length];
        var vs = new Res[rows.Length];
        Parallel.For(0, rows.Length, i =>
        {
            v0[i] = Measure(rows[i].F, V0);
            vf[i] = Measure(rows[i].F, VF);
            vs[i] = Measure(rows[i].F, VS);
        });

        // ---- 表A. 膠着が解けたか（本命） ----
        Console.WriteLine("## 表A. 膠着（上限ターン到達）—— **採用の条件1**");
        Console.WriteLine();
        Console.WriteLine("| | V0 | F 満タン | S 在庫あり |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine("| 上限到達が出るセル（/ 244） | " + CapCells(v0) + " | " + CapCells(vf)
                          + " | " + CapCells(vs) + " |");
        Console.WriteLine("| 上限到達率（全61行・第2〜5波） | " + F2(v0.Average(CapRate)) + "% | "
                          + F2(vf.Average(CapRate)) + "% | " + F2(vs.Average(CapRate)) + "% |");
        Console.WriteLine("| 決着T（全61行・第2〜5波） | " + F2(v0.Average(r => r.Settle)) + " | "
                          + F2(vf.Average(r => r.Settle)) + " | " + F2(vs.Average(r => r.Settle)) + " |");
        Console.WriteLine();

        Console.WriteLine("### 膠着していた行の波ごと（V0 で上限到達率 > 0 のセルだけ）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 波 | V0 到達率 | F | S | V0 勝率 | F | S | V0 決着T | F | S |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < rows.Length; i++)
            for (int st = 1; st < 5; st++)
            {
                if (v0[i].Cap[st] <= 0) continue;
                Console.WriteLine("| " + rows[i].Name + " | 第" + (st + 1) + "波 | "
                                  + F1(v0[i].Cap[st]) + " | " + F1(vf[i].Cap[st]) + " | " + F1(vs[i].Cap[st]) + " | "
                                  + F1(v0[i].Win[st]) + " | " + F1(vf[i].Win[st]) + " | " + F1(vs[i].Win[st]) + " | "
                                  + F2(v0[i].TurnAt[st]) + " | " + F2(vf[i].TurnAt[st]) + " | "
                                  + F2(vs[i].TurnAt[st]) + " |");
            }
        Console.WriteLine();

        // ---- 表B. 帰属（Δ と取り分を併記・規約 Q0-1） ----
        Console.WriteLine("## 表B. 帰属（`compare` 61 行・波ごと）—— Δ と**余地に対する取り分**を併記");
        Console.WriteLine();
        Console.WriteLine("`取り分` ＝ Δ ÷ (上がったなら 100 − V0 ／ 下がったなら V0)。");
        Console.WriteLine();
        Console.WriteLine("| 波 | V0 | F | Δ(F) | 取り分(F) | S | Δ(S) | 取り分(S) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|");
        for (int st = 0; st < 5; st++)
        {
            double a = v0.Average(r => r.Win[st]), b = vf.Average(r => r.Win[st]), c = vs.Average(r => r.Win[st]);
            Console.WriteLine("| 第" + (st + 1) + "波" + (st == 0 ? "（参考・分母外）" : "") + " | "
                              + F1(a) + " | " + F1(b) + " | " + Sg(b - a) + " | " + Share(b - a, a) + " | "
                              + F1(c) + " | " + Sg(c - a) + " | " + Share(c - a, a) + " |");
        }
        double A0 = Mean25(v0), AF = Mean25(vf), AS = Mean25(vs);
        Console.WriteLine("| **第2〜5波** | **" + F1(A0) + "** | **" + F1(AF) + "** | **" + Sg(AF - A0)
                          + "** | " + Share(AF - A0, A0) + " | **" + F1(AS) + "** | **" + Sg(AS - A0)
                          + "** | " + Share(AS - A0, A0) + " |");
        Console.WriteLine();

        // ---- 表C. 特異性（ガルド在席 / 非在席） ----
        Console.WriteLine("## 表C. 特異性 —— **採用の条件4**（ガルドを含まない行が ±0.0）");
        Console.WriteLine();
        Console.WriteLine("| 群 | 行数 | V0 | Δ(F) | Δ(S) | 動いたセル(F) | 動いたセル(S) |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|");
        foreach (bool want in new[] { true, false })
        {
            var idx = Enumerable.Range(0, rows.Length).Where(i => HasGald(rows[i].F) == want).ToArray();
            if (idx.Length == 0) continue;
            double a = idx.Average(i => Mean25(v0[i])), b = idx.Average(i => Mean25(vf[i]));
            double c = idx.Average(i => Mean25(vs[i]));
            int mf = idx.Sum(i => Enumerable.Range(0, 5)
                .Count(st => Math.Abs(vf[i].Win[st] - v0[i].Win[st]) > 1e-9));
            int ms = idx.Sum(i => Enumerable.Range(0, 5)
                .Count(st => Math.Abs(vs[i].Win[st] - v0[i].Win[st]) > 1e-9));
            Console.WriteLine("| ガルド" + (want ? "在席" : "非在席") + " | " + idx.Length + " | " + F1(a)
                              + " | " + Sg(b - a) + " | " + Sg(c - a) + " | " + mf + " | " + ms + " |");
        }
        Console.WriteLine();

        // ---- 表D. ガルドの帳簿（薄まる量・採用の条件2） ----
        Console.WriteLine("## 表D. ガルドの帳簿 —— **採用の条件2**（受け流し・中継が薄まらないか）");
        Console.WriteLine();
        Console.WriteLine("1戦あたり（ガルド在席行 × 第2〜5波 × seed 0.." + (Seeds - 1) + " の平均）。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 振った手番 | うち満タン | 受け流し | 構え直しT | 構えで戻した | 庇いで戻した "
                          + "| 売れた手番 | カドの干渉 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        var gi = Enumerable.Range(0, rows.Length).Where(i => HasGald(rows[i].F)).ToArray();
        foreach (var (tag, v) in new[] { ("V0", v0), ("F 満タン", vf), ("S 在庫あり", vs) })
            Console.WriteLine("| " + tag + " | " + F2(gi.Average(i => v[i].Swings)) + " | "
                              + F2(gi.Average(i => v[i].SwingsFull)) + " | "
                              + F2(gi.Average(i => v[i].Fires)) + " | "
                              + F2(gi.Average(i => v[i].Stances)) + " | "
                              + F2(gi.Average(i => v[i].RefillTurn)) + " | "
                              + F2(gi.Average(i => v[i].RefillGuard)) + " | "
                              + F2(gi.Average(i => v[i].Surrendered)) + " | "
                              + F2(gi.Average(i => v[i].KadoActs)) + " |");
        double f0 = gi.Average(i => v0[i].Fires), s0 = gi.Average(i => v0[i].Surrendered);
        Console.WriteLine();
        Console.WriteLine("| 薄まった割合 | F | S | 線 |");
        Console.WriteLine("|---|--:|--:|---|");
        Console.WriteLine("| 受け流しの発火 | " + Pct(gi.Average(i => vf[i].Fires), f0) + " | "
                          + Pct(gi.Average(i => vs[i].Fires), f0) + " | −10% 以内 |");
        Console.WriteLine("| 中継（売れた手番） | " + Pct(gi.Average(i => vf[i].Surrendered), s0) + " | "
                          + Pct(gi.Average(i => vs[i].Surrendered), s0) + " | （記録のみ） |");
        Console.WriteLine();

        // ---- 表E. 拒否権と情報セル ----
        var priIdx = Enumerable.Range(0, rows.Length)
            .Where(i => Baseline.PrimaryRows.Contains(rows[i].Name)).ToArray();
        Console.WriteLine("## 表E. 拒否権と情報セル —— **採用の条件3・5**");
        Console.WriteLine();
        Console.WriteLine("| | V0 | F | S |");
        Console.WriteLine("|---|--:|--:|--:|");
        Console.WriteLine("| 主判定" + priIdx.Length + "行・第五波（歯止め "
                          + Baseline.PrimaryFifthFloor.ToString("F1") + "） | "
                          + F1(priIdx.Average(i => v0[i].Win[4])) + " | "
                          + F1(priIdx.Average(i => vf[i].Win[4])) + " | "
                          + F1(priIdx.Average(i => vs[i].Win[4])) + " |");
        Console.WriteLine("| 情報セル（全61行・第2〜5波） | " + Info(v0) + " | " + Info(vf) + " | " + Info(vs) + " |");
        Console.WriteLine("| 情報セル（主判定" + priIdx.Length + "行） | " + Info(priIdx.Select(i => v0[i]))
                          + " | " + Info(priIdx.Select(i => vf[i])) + " | " + Info(priIdx.Select(i => vs[i])) + " |");
        Console.WriteLine("| 第五波 95% 超の行 | " + v0.Count(r => r.Win[4] > 95) + " | "
                          + vf.Count(r => r.Win[4] > 95) + " | " + vs.Count(r => r.Win[4] > 95) + " |");
        Console.WriteLine();

        // 情報セルの内訳（(G9): 減ったら記録する。**記録はするが、それだけで機構を落とさない**）
        Console.WriteLine("### 情報セルの内訳（S 版・第2〜5波）—— **どこで失い、どこで得たか**");
        Console.WriteLine();
        var lost = new List<string>();
        var gained = new List<string>();
        for (int i = 0; i < rows.Length; i++)
            for (int st = 1; st < 5; st++)
            {
                bool a = v0[i].Win[st] > 0 && v0[i].Win[st] < 100;
                bool b = vs[i].Win[st] > 0 && vs[i].Win[st] < 100;
                string cell = rows[i].Name + " 第" + (st + 1) + "波 "
                              + F1(v0[i].Win[st]) + " → " + F1(vs[i].Win[st]);
                if (a && !b) lost.Add(cell + (vs[i].Win[st] >= 100 ? "（天井）" : "（床）"));
                else if (!a && b) gained.Add(cell);
            }
        Console.WriteLine("**失った " + lost.Count + " セル**:");
        foreach (string c in lost) Console.WriteLine("- " + c);
        Console.WriteLine();
        Console.WriteLine("**得た " + gained.Count + " セル**:");
        foreach (string c in gained) Console.WriteLine("- " + c);
        Console.WriteLine();
        Console.WriteLine("**差し引き " + Sg(gained.Count - lost.Count) + " セル。**"
                          + " 天井で失った数 **" + lost.Count(c => c.EndsWith("（天井）")) + "** ／ "
                          + "床で失った数 **" + lost.Count(c => c.EndsWith("（床）")) + "**"
                          + " —— **床から離れて得た分と、天井に張り付いて失った分は別の出来事である**（第116期）。");
        Console.WriteLine();

        foreach (var (tag, v) in new[] { ("F", vf), ("S", vs) })
        {
            var bad = new List<string>();
            int vetoPri = 0;
            for (int i = 0; i < rows.Length; i++)
                for (int st = 0; st < 5; st++)
                    if (v[i].Win[st] - v0[i].Win[st] <= -10.0)
                    {
                        bad.Add(rows[i].Name + " 第" + (st + 1) + "波 " + Sg(v[i].Win[st] - v0[i].Win[st]));
                        if (Baseline.PrimaryRows.Contains(rows[i].Name)) vetoPri++;
                    }
            Console.WriteLine("- **" + tag + "**: −10.0pt 以上のセル **" + bad.Count + "**（うち主判定 "
                              + vetoPri + "）" + (bad.Count == 0 ? "" : " —— " + string.Join(" ／ ", bad.Take(12))));
        }
        Console.WriteLine();

        // ---- 表F. 行ごと ----
        Console.WriteLine("## 表F. 行ごとの帰属（第2〜5波平均・|Δ(S)| の大きい順に上位15）");
        Console.WriteLine();
        Console.WriteLine("| 行 | ガルド | V0 | Δ(F) | Δ(S) | 取り分(S) | 振った手番/戦 |");
        Console.WriteLine("|---|:-:|--:|--:|--:|--:|--:|");
        foreach (int i in Enumerable.Range(0, rows.Length)
                     .OrderByDescending(i => Math.Abs(Mean25(vs[i]) - Mean25(v0[i]))).Take(15))
            Console.WriteLine("| " + rows[i].Name + " | " + (HasGald(rows[i].F) ? "○" : "—") + " | "
                              + F1(Mean25(v0[i])) + " | " + Sg(Mean25(vf[i]) - Mean25(v0[i])) + " | "
                              + Sg(Mean25(vs[i]) - Mean25(v0[i])) + " | "
                              + Share(Mean25(vs[i]) - Mean25(v0[i]), Mean25(v0[i])) + " | "
                              + F2(vs[i].Swings) + " |");
        Console.WriteLine();

        // ---- 判定 ----
        Console.WriteLine("## 判定（指示書 §5）");
        Console.WriteLine();
        int c0 = CapCells(v0);
        double fireF = 100.0 * (gi.Average(i => vf[i].Fires) - f0) / Math.Max(f0, 1e-9);
        double fireS = 100.0 * (gi.Average(i => vs[i].Fires) - f0) / Math.Max(f0, 1e-9);
        int noGaldF = Enumerable.Range(0, rows.Length).Where(i => !HasGald(rows[i].F))
            .Sum(i => Enumerable.Range(0, 5).Count(st => Math.Abs(vf[i].Win[st] - v0[i].Win[st]) > 1e-9));
        int noGaldS = Enumerable.Range(0, rows.Length).Where(i => !HasGald(rows[i].F))
            .Sum(i => Enumerable.Range(0, 5).Count(st => Math.Abs(vs[i].Win[st] - v0[i].Win[st]) > 1e-9));
        foreach (var (tag, capCells, fire, ng, v) in new[]
                 { ("F 満タン", CapCells(vf), fireF, noGaldF, vf),
                   ("S 在庫あり", CapCells(vs), fireS, noGaldS, vs) })
        {
            int veto = 0;
            for (int i = 0; i < rows.Length; i++)
                for (int st = 0; st < 5; st++)
                    if (v[i].Win[st] - v0[i].Win[st] <= -10.0) veto++;
            Console.WriteLine("### " + tag);
            Console.WriteLine();
            Console.WriteLine("| 条件 | 実測 | 判定 |");
            Console.WriteLine("|---|---|:-:|");
            Console.WriteLine("| 1. 膠着が解ける（上限到達のセルが減る） | " + c0 + " → " + capCells + " | "
                              + (capCells < c0 ? "○" : "**×**") + " |");
            Console.WriteLine("| 2. 受け流しが薄まらない（−10% 以内） | " + Sg(fire) + "% | "
                              + (fire >= -10.0 ? "○" : "**×**") + " |");
            Console.WriteLine("| 3. 拒否権（−10pt 以上のセル 0） | " + veto + " セル | "
                              + (veto == 0 ? "○" : "**×**") + " |");
            Console.WriteLine("| 4. 特異性（ガルド非在席が ±0.0） | " + ng + " セル | "
                              + (ng == 0 ? "○" : "**×**") + " |");
            Console.WriteLine("| 5. 情報セルが減らない | " + Info(v0) + " → " + Info(v) + " | "
                              + (Info(v) >= Info(v0) ? "○" : "**×**") + " |");
            Console.WriteLine();
        }
    }

    // =================================================================================
    // check —— 自己検査
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第152期 自己検査 —— `stall check`");
        Console.WriteLine();

        string bal = arg.Length > 0 ? arg : "docs/balance.md";
        Console.WriteLine("## (a) 必須1: 既定（`Swing = Off`）の `compare` 305 セルが `" + bal + "` と 0 件");
        Console.WriteLine();
        if (!File.Exists(bal)) Console.WriteLine("**比較先が見つからない。**");
        else
        {
            var want = new Dictionary<string, double[]>();
            foreach (string line in File.ReadAllLines(bal))
            {
                if (!line.StartsWith("| ") || !line.Contains('%')) continue;
                string[] c = line.Split('|', StringSplitOptions.TrimEntries);
                if (c.Length < 8) continue;
                var v = new double[5];
                bool ok = true;
                for (int k = 0; k < 5; k++)
                    if (!double.TryParse(c[2 + k].TrimEnd('%'), out v[k])) { ok = false; break; }
                if (ok) want[c[1]] = v;
            }
            int bad = 0, seen = 0;
            var rows = CompareBuilds();
            var got = new double[rows.Length][];
            Parallel.For(0, rows.Length, i =>
            {
                got[i] = new double[5];
                for (int st = 0; st < 5; st++)
                {
                    int w = 0;
                    for (int seed = 0; seed < Seeds; seed++)
                        if (BattleEngine.Run(rows[i].F, EnemyCatalog.Stages[st].Enemy, seed,
                                             verbose: false).PlayerWon) w++;
                    got[i][st] = 100.0 * w / Seeds;
                }
            });
            for (int i = 0; i < rows.Length; i++)
            {
                if (!want.TryGetValue(rows[i].Name, out double[]? e)) continue;
                for (int st = 0; st < 5; st++) { seen++; if (Math.Abs(e[st] - got[i][st]) > 1e-9) bad++; }
            }
            Console.WriteLine("- 突き合わせた " + seen + " セル中 **ずれ " + bad + " 件**"
                              + (bad == 0 ? "（○）" : "（**×**）"));
        }
        Console.WriteLine();

        Console.WriteLine("## (b) 必須4: `ctx.PickOne` を新たに使っていない");
        Console.WriteLine();
        string tr = ReadSrc("BattleCore/Traits.cs"), en = ReadSrc("BattleCore/BattleEngine.cs");
        Console.WriteLine("- `PickOne(` の出現数: `Traits.cs` **" + Count(tr, "PickOne(")
                          + "** ／ `BattleEngine.cs` **" + Count(en, "PickOne(")
                          + "**（第135期 HEAD は 8 / 18）");
        Console.WriteLine();

        Console.WriteLine("## (c) `Off` では構えを解いた手番が1つも無い（`ParrySwings` = 0）");
        Console.WriteLine();
        long sw = 0, swF = 0, swS = 0;
        foreach (var (_, f) in CompareBuilds())
            for (int st = 0; st < 5; st++)
                for (int seed = 0; seed < 20; seed++)
                {
                    sw += Swings(BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                  verbose: false, parry: V0));
                    swF += Swings(BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                   verbose: false, parry: VF));
                    swS += Swings(BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                   verbose: false, parry: VS));
                }
        Console.WriteLine("| 版 | `ParrySwings` の総和（seed 0..19） |");
        Console.WriteLine("|---|--:|");
        Console.WriteLine("| V0（明示の `Off`） | **" + sw + "**（0 なら ○） |");
        Console.WriteLine("| F（`WhenFull`） | " + swF + " |");
        Console.WriteLine("| S（`WhenStocked`） | " + swS + " |");
        Console.WriteLine();
        Console.WriteLine("- **F ⊆ S の検算**: 満タンの手番は在庫ありの手番でもあるので "
                          + "`F ≦ S` でなければならない —— **" + (swF <= swS ? "○" : "×") + "**");
        Console.WriteLine();

        Console.WriteLine("## (d) `ParrySwingsFull` は「満タンだった手番」だけを数える");
        Console.WriteLine();
        long fullF = 0, fullS = 0, swF2 = 0;
        foreach (var (_, f) in CompareBuilds())
            for (int st = 0; st < 5; st++)
                for (int seed = 0; seed < 20; seed++)
                {
                    BattleResult rF = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                       verbose: false, parry: VF);
                    BattleResult rS = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                       verbose: false, parry: VS);
                    fullF += Full(rF); swF2 += Swings(rF); fullS += Full(rS);
                }
        Console.WriteLine("- F 版: 振った手番 " + swF2 + " ／ うち満タン " + fullF
                          + " —— **全部が満タンでなければならない: " + (fullF == swF2 ? "○" : "×") + "**");
        Console.WriteLine("- S 版: うち満タン " + fullS + "（振った手番 " + swS + " の "
                          + F1(swS == 0 ? 0 : 100.0 * fullS / swS) + "%）");
        Console.WriteLine();

        Console.WriteLine("## (e) 触っていないノブの既定が動いていない");
        Console.WriteLine();
        Console.WriteLine("- `ParryRule.Default` = `" + ParryRule.Default + "`");
        Console.WriteLine("- **`docs/rules.md` の差分で示す**（(G8) 必須3）。動いてよいのは `ParryRule` の行だけ。");
        Console.WriteLine();

        Console.WriteLine("## (f) ガルドを持たない編成では3版が1ビットも違わない");
        Console.WriteLine();
        int diff = 0, checkedBattles = 0;
        foreach (var (_, f) in CompareBuilds().Where(r => !HasGald(r.F)))
            for (int st = 0; st < 5; st++)
                for (int seed = 0; seed < 40; seed++)
                {
                    var a = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, parry: V0);
                    var b = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed, verbose: false, parry: VS);
                    checkedBattles++;
                    if (a.PlayerWon != b.PlayerWon || a.Turns != b.Turns) diff++;
                }
        Console.WriteLine("- 突き合わせた " + checkedBattles + " 戦中 **違い " + diff + " 件**"
                          + (diff == 0 ? "（○）" : "（**×**）"));
        Console.WriteLine();
    }

    // =================================================================================
    // 器具
    // =================================================================================

    readonly record struct Res(double[] Win, double[] Cap, double[] TurnAt, double Settle,
                               double Swings, double SwingsFull, double Fires, double Stances,
                               double RefillTurn, double RefillGuard, double Surrendered, double KadoActs);

    static Res Measure(Formation f, ParryRule r)
    {
        var w = new double[5];
        var cap = new double[5];
        var turnAt = new double[5];
        double settle = 0, sw = 0, swF = 0, fire = 0, stance = 0, rt = 0, rg = 0, sur = 0, kado = 0;
        int b25 = 0;
        for (int st = 0; st < 5; st++)
        {
            int wins = 0, caps = 0;
            double turns = 0;
            for (int seed = 0; seed < Seeds; seed++)
            {
                BattleResult res = BattleEngine.Run(f, EnemyCatalog.Stages[st].Enemy, seed,
                                                    verbose: false, parry: r);
                if (res.PlayerWon) wins++;
                if (res.Turns >= BattleEngine.MaxTurns) caps++;
                turns += res.Turns;
                if (st >= 1)
                {
                    settle += res.Turns; b25++;
                    if (res.TallyByUnit.TryGetValue(UnitCatalog.Gald.Id, out UnitTally? g))
                    {
                        sw += g.ParrySwings; swF += g.ParrySwingsFull; fire += g.ParryFires;
                        stance += g.ParryStances; rt += g.ParryRefillTurn; rg += g.ParryRefillGuard;
                        sur += g.TurnsSurrendered;
                    }
                    if (res.TallyByUnit.TryGetValue(UnitCatalog.Kado.Id, out UnitTally? k))
                        kado += k.Interventions;
                }
            }
            w[st] = 100.0 * wins / Seeds;
            cap[st] = 100.0 * caps / Seeds;
            turnAt[st] = turns / Seeds;
        }
        double n = Math.Max(b25, 1);
        return new Res(w, cap, turnAt, settle / n, sw / n, swF / n, fire / n, stance / n,
                       rt / n, rg / n, sur / n, kado / n);
    }

    static bool HasGald(Formation f) => f.Occupied().Any(o => o.Def.Id == UnitCatalog.Gald.Id);

    static int Swings(BattleResult r)
        => r.TallyByUnit.TryGetValue(UnitCatalog.Gald.Id, out UnitTally? t) ? t.ParrySwings : 0;
    static int Full(BattleResult r)
        => r.TallyByUnit.TryGetValue(UnitCatalog.Gald.Id, out UnitTally? t) ? t.ParrySwingsFull : 0;

    static int CapCells(IEnumerable<Res> rs) => rs.Sum(r => r.Cap.Skip(1).Count(x => x > 0));
    static double CapRate(Res r) => r.Cap.Skip(1).Average();
    static double Mean25(Res r) => r.Win.Skip(1).Average();
    static double Mean25(IEnumerable<Res> rs) => rs.Average(Mean25);
    static int Info(IEnumerable<Res> rs) => rs.Sum(r => r.Win.Skip(1).Count(x => x > 0 && x < 100));
    static string F1(double v) => v.ToString("F1");
    static string F2(double v) => v.ToString("F2");
    static string Sg(double v) => v.ToString("+0.0;-0.0;0.0");

    static string Share(double d, double v0)
    {
        double room = d >= 0 ? 100.0 - v0 : v0;
        return room <= 1e-9 ? "—" : (d / room).ToString("+0.000;-0.000;0.000");
    }

    static string Pct(double now, double baseline)
        => baseline <= 1e-9 ? "—" : (100.0 * (now - baseline) / baseline).ToString("+0.0;-0.0;0.0") + "%";

    static int Count(string s, string needle)
    {
        int n = 0, i = 0;
        while ((i = s.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { n++; i += needle.Length; }
        return n;
    }

    static string ReadSrc(string rel) => File.Exists(rel) ? File.ReadAllText(rel) : "";
}
