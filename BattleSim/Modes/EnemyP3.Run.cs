using BattleCore;
using static Common;

// ep3 run / check（第203期）。Phase 0 は `EnemyP3.cs`。

static partial class EnemyP3Diag
{
    const int Seeds = 200;

    static partial void RunMoreImpl(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run": RunTables(); handled = true; return;
            case "check": Check(arg); handled = true; return;
        }
    }

    // =================================================================================
    // 測る
    // =================================================================================

    /// <summary>1つの（行 × 敵）の 200 戦ぶん。帳簿は1戦あたりに割る前の合計。</summary>
    sealed class M
    {
        public int Wins;
        public int FrontCollapsed; public double FrontCollapseTurn;           // 敵の前列（開幕の前列の駒）が全滅した戦とそのターン
        public double[] FrontHitsByPat = new double[4];                       // 前列が倒れるまでに受けた味方の攻撃（型別）
        public double PierceSwings, PierceBehind;                             // 前列が生きている間の味方の貫きと、そのうち前列より後ろに届いたもの
        public double SweepSwings, SweepHit, SweepSwingsLate, SweepHitLate;   // 味方の薙ぎ1回に当たった敵の数（全体／前列が崩れた後）
        public double MartyrGuards;                                           // 殉教の庇い
        public double ReboundThrusts, ReboundNoFront;                          // 突き返し
        public double Turns;
    }

    static M Measure(Formation f, Formation enemy, bool ledger)
    {
        var m = new M();
        object gate = new();
        Parallel.For(0, Seeds, seed =>
        {
            List<UnitState> pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            List<UnitState> en = BattleEngine.Materialize(enemy, BattleContext.EnemyTeam);
            var front = en.Where(u => u.Row == Row.Front).ToList();   // 席は Run の前に（戦闘中に動く）
            BattleResult r = BattleEngine.Run(pl, en, seed, verbose: ledger);
            // InstanceId は Run の中（`BattleContext.Add`）で振られるので、Run の後に引く
            var frontIds = front.Select(u => u.InstanceId).ToHashSet();
            var enemyIds = en.Select(u => u.InstanceId).ToHashSet();
            var playerIds = pl.Select(u => u.InstanceId).ToHashSet();

            var local = new M();
            if (r.PlayerWon) local.Wins++;
            local.Turns = r.Turns;
            if (r.TallyByUnit.TryGetValue("axeman_g", out UnitTally? mt)) local.MartyrGuards = mt.Intercepts;
            if (r.TallyByUnit.TryGetValue("hane", out UnitTally? ht)) { local.ReboundThrusts = ht.ReboundThrusts; local.ReboundNoFront = ht.ReboundNoFront; }

            if (ledger)
            {
                var frontAlive = new HashSet<int>(frontIds);
                int? cur = null; AttackPattern curPat = AttackPattern.Single; bool curFrontAlive = false;
                var hit = new HashSet<int>();
                void Close()
                {
                    if (cur is null) return;
                    bool hitFront = hit.Any(frontIds.Contains);
                    bool hitBehind = hit.Any(id => !frontIds.Contains(id));
                    if (curFrontAlive && hitFront) local.FrontHitsByPat[(int)curPat]++;
                    if (curPat == AttackPattern.Pierce && curFrontAlive) { local.PierceSwings++; if (hitBehind) local.PierceBehind++; }
                    if (curPat == AttackPattern.Sweep)
                    {
                        local.SweepSwings++; local.SweepHit += hit.Count;
                        if (!curFrontAlive) { local.SweepSwingsLate++; local.SweepHitLate += hit.Count; }
                    }
                    cur = null; hit.Clear();
                }
                foreach (BattleEvent e in r.Events)
                {
                    switch (e.Kind)
                    {
                        case BattleEventKind.TurnStart:
                            Close(); break;
                        case BattleEventKind.Attack:
                            Close();
                            if (e.ActorId is int a && playerIds.Contains(a))
                            {
                                cur = a; curPat = e.Pattern ?? AttackPattern.Single; curFrontAlive = frontAlive.Count > 0;
                            }
                            break;
                        case BattleEventKind.Damage:
                            if (cur is int c && e.ActorId == c && e.TargetId is int t && enemyIds.Contains(t)) hit.Add(t);
                            break;
                        case BattleEventKind.Death:
                            if (e.TargetId is int d && frontAlive.Remove(d) && frontAlive.Count == 0)
                            {
                                local.FrontCollapsed++; local.FrontCollapseTurn += e.Turn;
                            }
                            break;
                    }
                }
                Close();
            }

            lock (gate)
            {
                m.Wins += local.Wins; m.Turns += local.Turns;
                m.FrontCollapsed += local.FrontCollapsed; m.FrontCollapseTurn += local.FrontCollapseTurn;
                for (int i = 0; i < 4; i++) m.FrontHitsByPat[i] += local.FrontHitsByPat[i];
                m.PierceSwings += local.PierceSwings; m.PierceBehind += local.PierceBehind;
                m.SweepSwings += local.SweepSwings; m.SweepHit += local.SweepHit;
                m.SweepSwingsLate += local.SweepSwingsLate; m.SweepHitLate += local.SweepHitLate;
                m.MartyrGuards += local.MartyrGuards;
                m.ReboundThrusts += local.ReboundThrusts; m.ReboundNoFront += local.ReboundNoFront;
            }
        });
        return m;
    }

    static double Pct(M m) => m.Wins * 100.0 / Seeds;
    static string F1(double v) => v.ToString("F1");
    static string S1(double v) => (v >= 0 ? "+" : "") + v.ToString("F1");

    // =================================================================================
    // run
    // =================================================================================

    static void RunTables()
    {
        Console.WriteLine("# 第203期 `ep3 run` —— 敵のパターン3（前衛1枚）の写し 対 元の波");
        Console.WriteLine();
        Dictionary<TraitId, string>? pt = PatternTraits();
        if (pt is null) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var rows = CompareBuilds();
        (int St, string Tag, Formation X, Formation P)[] waves =
        {
            (2, "三", EnemyCatalog.Stages[2].Enemy, EnemyCatalog.Pattern3Copies[0].Enemy),
            (4, "五", EnemyCatalog.Stages[4].Enemy, EnemyCatalog.Pattern3Copies[1].Enemy),
        };
        foreach (var w in waves)
            Console.WriteLine("- " + w.Tag + ": 元 " + Seats(w.X) + " ／ 写し " + Seats(w.P));
        Console.WriteLine();

        var res = new M[rows.Length, 2, 2];   // 行 × 波 × (0 元, 1 写し)
        for (int i = 0; i < rows.Length; i++)
            for (int w = 0; w < 2; w++)
            {
                res[i, w, 0] = Measure(rows[i].F, waves[w].X, ledger: true);
                res[i, w, 1] = Measure(rows[i].F, waves[w].P, ledger: true);
            }

        // ---------------- 5.1 主表 ----------------
        Console.WriteLine("## 5.1 主表 —— `compare` 61 行 × 元の波／写し（seed 0..199）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 群 | 第三波（元） | P3-三 | Δ | 第五波（元） | P3-五 | Δ |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|--:|--:|");
        for (int i = 0; i < rows.Length; i++)
        {
            double a = Pct(res[i, 0, 0]), b = Pct(res[i, 0, 1]), c = Pct(res[i, 1, 0]), d = Pct(res[i, 1, 1]);
            Console.WriteLine("| " + rows[i].Name + " | " + KindNames[(int)RowKind(rows[i].F, pt)] + " | " + F1(a) + " | " + F1(b) + " | " + S1(b - a)
                              + " | " + F1(c) + " | " + F1(d) + " | " + S1(d - c) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("| 波 | 元の平均 | 写しの平均 | Δ平均 | r（元 対 写し） | max\\|Δ\\| | 上がった行 | 下がった行 | 情報セル（元 → 写し） |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|---|");
        for (int w = 0; w < 2; w++)
        {
            double[] x = Enumerable.Range(0, rows.Length).Select(i => Pct(res[i, w, 0])).ToArray();
            double[] y = Enumerable.Range(0, rows.Length).Select(i => Pct(res[i, w, 1])).ToArray();
            double[] dd = x.Zip(y, (p, q) => q - p).ToArray();
            int maxI = Enumerable.Range(0, dd.Length).OrderByDescending(i => Math.Abs(dd[i])).First();
            Console.WriteLine("| " + waves[w].Tag + " | " + F1(x.Average()) + " | " + F1(y.Average()) + " | " + S1(dd.Average()) + " | " + Pearson(x, y).ToString("F3")
                              + " | " + F1(Math.Abs(dd[maxI])) + "（" + rows[maxI].Name + "） | " + dd.Count(v => v > 0) + " | " + dd.Count(v => v < 0)
                              + " | " + x.Count(v => v > 0 && v < 100) + " → " + y.Count(v => v > 0 && v < 100) + " |");
        }
        Console.WriteLine();

        Console.WriteLine("**固有の勝者・敗者**（写しでだけ 100% ／ 0% になった行・元でだけそうだった行）:");
        Console.WriteLine();
        for (int w = 0; w < 2; w++)
        {
            string L(Func<int, bool> p) { var n = Enumerable.Range(0, rows.Length).Where(p).Select(i => rows[i].Name).ToList(); return n.Count + (n.Count == 0 ? "" : "（" + string.Join(" ／ ", n) + "）"); }
            Console.WriteLine("- " + waves[w].Tag + ": 写しでだけ 100% " + L(i => Pct(res[i, w, 1]) == 100 && Pct(res[i, w, 0]) < 100)
                              + " ／ 写しでだけ 0% " + L(i => Pct(res[i, w, 1]) == 0 && Pct(res[i, w, 0]) > 0)
                              + " ／ 元でだけ 100% " + L(i => Pct(res[i, w, 0]) == 100 && Pct(res[i, w, 1]) < 100)
                              + " ／ 元でだけ 0% " + L(i => Pct(res[i, w, 0]) == 0 && Pct(res[i, w, 1]) > 0));
        }
        Console.WriteLine();

        // ---------------- 5.2 攻撃の型 ----------------
        Console.WriteLine("## 5.2 攻撃の型で分けた読み（写し − 元 の平均）");
        Console.WriteLine();
        Console.WriteLine("| 群 | 行数 | Δ 三 | Δ 五 | 余地に対する取り分 三 | 取り分 五 |");
        Console.WriteLine("|---|--:|--:|--:|--:|--:|");
        foreach (Kind k in Enum.GetValues<Kind>())
        {
            var idx = Enumerable.Range(0, rows.Length).Where(i => RowKind(rows[i].F, pt) == k).ToList();
            if (idx.Count == 0) continue;
            double D(int w) => idx.Average(i => Pct(res[i, w, 1]) - Pct(res[i, w, 0]));
            // 取り分: 上がった分は (100 − 元)、下がった分は 元 で割る（R144 の「余地」）。元が 0 / 100 の行は分母が 0 なので外す
            string Share(int w)
            {
                var v = idx.Select(i => (a: Pct(res[i, w, 0]), b: Pct(res[i, w, 1])))
                           .Where(t => t.b >= t.a ? t.a < 100 : t.a > 0)
                           .Select(t => t.b >= t.a ? (t.b - t.a) / (100 - t.a) : (t.b - t.a) / t.a).ToList();
                return v.Count == 0 ? "—" : (v.Average() >= 0 ? "+" : "") + v.Average().ToString("F3") + "（" + v.Count + "）";
            }
            Console.WriteLine("| " + KindNames[(int)k] + " | " + idx.Count + " | " + S1(D(0)) + " | " + S1(D(1)) + " | " + Share(0) + " | " + Share(1) + " |");
        }
        Console.WriteLine();

        // ---------------- 5.3 帳簿 ----------------
        Console.WriteLine("## 5.3 帳簿（61 行 × 200 戦の合計を 1 戦あたりに）");
        Console.WriteLine();
        Console.WriteLine("「前列」は開幕に前列に立っていた敵（元は前1・前3 の2体、写しは C の1体）。");
        Console.WriteLine();
        Console.WriteLine("| 波 | 陣形 | 決着T | 前列が全滅した戦 | その T | 前列が受けた攻撃 単体/薙ぎ/貫き/全体 | 前列が生きている間の貫き | うち後ろへ届いた | 薙ぎ1回の敵数 | 前列が崩れた後の薙ぎ1回 | 殉教の庇い | 突き返し（成功/相手なし） |");
        Console.WriteLine("|---|---|--:|--:|--:|---|--:|--:|--:|--:|--:|---|");
        for (int w = 0; w < 2; w++)
            for (int s = 0; s < 2; s++)
            {
                var all = new M();
                for (int i = 0; i < rows.Length; i++)
                {
                    M m = res[i, w, s];
                    all.Wins += m.Wins; all.Turns += m.Turns; all.FrontCollapsed += m.FrontCollapsed; all.FrontCollapseTurn += m.FrontCollapseTurn;
                    for (int p = 0; p < 4; p++) all.FrontHitsByPat[p] += m.FrontHitsByPat[p];
                    all.PierceSwings += m.PierceSwings; all.PierceBehind += m.PierceBehind;
                    all.SweepSwings += m.SweepSwings; all.SweepHit += m.SweepHit; all.SweepSwingsLate += m.SweepSwingsLate; all.SweepHitLate += m.SweepHitLate;
                    all.MartyrGuards += m.MartyrGuards; all.ReboundThrusts += m.ReboundThrusts; all.ReboundNoFront += m.ReboundNoFront;
                }
                double n = rows.Length * (double)Seeds;
                Console.WriteLine("| " + waves[w].Tag + " | " + (s == 0 ? "元（X 字）" : "写し（P3）") + " | " + (all.Turns / n).ToString("F2")
                                  + " | " + (all.FrontCollapsed * 100.0 / n).ToString("F1") + "% | " + (all.FrontCollapsed == 0 ? "—" : (all.FrontCollapseTurn / all.FrontCollapsed).ToString("F2"))
                                  + " | " + string.Join(" / ", all.FrontHitsByPat.Select(v => (v / n).ToString("F2")))
                                  + " | " + (all.PierceSwings / n).ToString("F2") + " | " + (all.PierceBehind / n).ToString("F2")
                                  + " | " + (all.SweepSwings == 0 ? "—" : (all.SweepHit / all.SweepSwings).ToString("F2"))
                                  + " | " + (all.SweepSwingsLate == 0 ? "—" : (all.SweepHitLate / all.SweepSwingsLate).ToString("F2"))
                                  + " | " + (all.MartyrGuards / n).ToString("F3")
                                  + " | " + all.ReboundThrusts + " / " + all.ReboundNoFront + " |");
            }
        Console.WriteLine();

        // 殉教の庇い（行ごと・どちらかで 0.05 回/戦 以上）
        Console.WriteLine("**殉教の庇い（第五波・行ごと・1戦あたり）** —— 元か写しで 0.05 以上の行:");
        Console.WriteLine();
        Console.WriteLine("| 行 | 元 | 写し |");
        Console.WriteLine("|---|--:|--:|");
        for (int i = 0; i < rows.Length; i++)
        {
            double a = res[i, 1, 0].MartyrGuards / Seeds, b = res[i, 1, 1].MartyrGuards / Seeds;
            if (a >= 0.05 || b >= 0.05) Console.WriteLine("| " + rows[i].Name + " | " + a.ToString("F2") + " | " + b.ToString("F2") + " |");
        }
        Console.WriteLine();
        Console.WriteLine("所要 " + sw.Elapsed.TotalSeconds.ToString("F0") + " 秒");
    }

    static string Seats(Formation f)
    {
        IReadOnlyList<string> names = f.Shape.FrameNames;
        return string.Join(" ", f.Occupied().Select(o => names[o.Slot] + ":" + o.Def.Name));
    }

    // =================================================================================
    // check
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第203期 `ep3 check` —— 自己検査");
        Console.WriteLine();
        bool ok = true;
        FormationShape d = FormationShape.Spear;
        int[] seat = d.PlayableSlots.ToArray();   // A..E
        string N(int slot) { int i = Array.IndexOf(seat, slot); return i >= 0 ? ((char)('A' + i)).ToString() : FormationRules.SeatNames[slot]; }

        // (3) 表
        Console.WriteLine("## (3) パターン3の隣接・薙ぎ・貫通が §2 の表と一致する");
        Console.WriteLine();
        var wantAdj = new Dictionary<char, string> { ['A'] = "BD", ['B'] = "ACE", ['C'] = "BE", ['D'] = "AE", ['E'] = "BCD" };
        Console.WriteLine("| 位置 | 期待 | 実装（編成の席だけ） | 一致 |");
        Console.WriteLine("|---|---|---|:-:|");
        for (int i = 0; i < 5; i++)
        {
            char c = (char)('A' + i);
            string got = string.Concat(Enumerable.Range(0, 5).Where(j => d.AreAdjacent(seat[i], seat[j])).Select(j => (char)('A' + j)));
            bool sym = Enumerable.Range(0, 9).All(b => d.AreAdjacent(seat[i], b) == d.AreAdjacent(b, seat[i]));
            bool eq = got == wantAdj[c];
            ok &= eq && sym;
            Console.WriteLine("| " + c + " | " + wantAdj[c] + " | " + got + (sym ? "" : "（**非対称**）") + " | " + (eq && sym ? "○" : "**×**") + " |");
        }
        Console.WriteLine();
        int cs = seat[2];
        string sweepC = "C" + string.Concat(Enumerable.Range(0, 5).Where(j => seat[j] != cs && d.SweepTargets(cs).Contains(seat[j])).Select(j => (char)('A' + j)));
        string L(int lane) => string.Concat(d.LanePath(lane).Where(s => d.IsPlayable(s)).Select(N).OrderBy(x => x));
        string l12 = L(0), l23 = L(1);
        bool sOk = sweepC == "CBE", p1 = l12 == "ABC", p2 = l23 == "CDE";
        ok &= sOk && p1 && p2;
        Console.WriteLine("| 攻撃 | 期待 | 実装 | 一致 |");
        Console.WriteLine("|---|---|---|:-:|");
        Console.WriteLine("| C に薙ぎ | CBE | " + sweepC + " | " + (sOk ? "○" : "**×**") + " |");
        Console.WriteLine("| 1-2 レーンへの貫通 | ABC | " + l12 + "（走る順 " + string.Join("→", d.LanePath(0).Where(d.IsPlayable).Select(N)) + "） | " + (p1 ? "○" : "**×**") + " |");
        Console.WriteLine("| 2-3 レーンへの貫通 | CDE | " + l23 + "（走る順 " + string.Join("→", d.LanePath(1).Where(d.IsPlayable).Select(N)) + "） | " + (p2 ? "○" : "**×**") + " |");
        Console.WriteLine();
        Console.WriteLine("9マスに広げた表（前1・前3・中央・○後2 は召喚枠）:");
        Console.WriteLine();
        Console.WriteLine("| 席 | 陣形の名 | 列 | 隣接 | 薙ぎの巻き込み | レーン |");
        Console.WriteLine("|---|---|---|---|---|---|");
        for (int s = 0; s < 9; s++)
            Console.WriteLine("| " + FormationRules.SeatNames[s] + " | " + N(s) + " | " + FormationRules.RowOf(s) + " | " + string.Join(" ", Enumerable.Range(0, 9).Where(b => d.AreAdjacent(s, b)).Select(N))
                              + " | " + string.Join(" ", d.SweepTargets(s).Select(N)) + " | " + string.Join(",", d.LanesOf(s).Select(l => l == 0 ? "1-2" : "2-3")) + " |");
        Console.WriteLine();
        bool frontOnlyC = seat.Where(s => FormationRules.RowOf(s) == Row.Front).SequenceEqual(new[] { cs });
        ok &= frontOnlyC;
        Console.WriteLine("- 前列の編成の席は C だけ: " + (frontOnlyC ? "○" : "**×**"));
        // 写しの駒が元の波と同じ（数値・札）
        bool same = true;
        foreach (var (st, copy) in new[] { (2, EnemyCatalog.Pattern3Copies[0].Enemy), (4, EnemyCatalog.Pattern3Copies[1].Enemy) })
        {
            var a = EnemyCatalog.Stages[st].Enemy.Occupied().Select(o => o.Def).OrderBy(x => x.Id).ToList();
            var b = copy.Occupied().Select(o => o.Def).OrderBy(x => x.Id).ToList();
            same &= a.Count == b.Count && a.Zip(b).All(z => ReferenceEquals(z.First, z.Second)) && ReferenceEquals(copy.Shape, FormationShape.Spear);
        }
        ok &= same;
        Console.WriteLine("- 写しの駒が元の波と同じ `UnitDef`（参照同値）で、陣形がパターン3: " + (same ? "○" : "**×**"));
        bool cOk = ReferenceEquals(EnemyCatalog.Pattern3Copies[0].Enemy[2], EnemyCatalog.Stages[2].Enemy[0])
                   && ReferenceEquals(EnemyCatalog.Pattern3Copies[1].Enemy[2], EnemyCatalog.Stages[4].Enemy[0]);
        ok &= cOk;
        Console.WriteLine("- C（前衛）が元の前1 の駒（騎士／殉教者）: " + (cOk ? "○" : "**×**"));
        Console.WriteLine();

        // (1) compare
        Console.WriteLine("## (1) `compare` が 0 セル差分（元の5波は1ビットも変わらない）");
        Console.WriteLine();
        string path = string.IsNullOrWhiteSpace(arg) ? "docs/balance.md" : arg.Trim();
        if (File.Exists(path))
        {
            var want = File.ReadAllLines(path).Where(l => l.StartsWith("| ") && l.Contains('%')).ToList();
            var got = new List<string>();
            foreach (var (name, f) in CompareBuilds())
            {
                var w = new double[5];
                for (int st = 0; st < 5; st++)
                {
                    int wins = 0; Formation en = EnemyCatalog.Stages[st].Enemy;
                    Parallel.For(0, 200, s => { if (BattleEngine.Run(f, en, s, verbose: false).PlayerWon) Interlocked.Increment(ref wins); });
                    w[st] = wins / 2.0;
                }
                got.Add("| " + name + " | " + string.Join(" | ", w.Select(x => x.ToString("F1") + "%")) + " |");
            }
            int diff = 0;
            for (int i = 0; i < Math.Min(want.Count, got.Count); i++) if (want[i].Trim() != got[i].Trim()) diff++;
            diff += Math.Abs(want.Count - got.Count);
            ok &= diff == 0;
            Console.WriteLine("- `" + path + "` と行単位で突き合わせ: " + got.Count + " 行・差分 **" + diff + "** 行");
        }
        else { ok = false; Console.WriteLine("- `" + path + "` が無い"); }
        Console.WriteLine();

        // (2) 乱数
        Console.WriteLine("## (2) 新しい処理が乱数を引かない");
        Console.WriteLine();
        if (ParryScan.Init())
        {
            string models = ParryScan.Read(Path.Combine("BattleCore", "Models.cs"));
            int ms = models.IndexOf("public sealed class " + "FormationShape"), me = models.IndexOf("public sealed class " + "Formation\n", ms);
            if (me < 0) me = models.IndexOf("public sealed class " + "Formation\r", ms);
            string shapeBody = ms < 0 || me < 0 ? "" : models.Substring(ms, me - ms);
            int shapeRolls = Count(shapeBody, "Roll(") + Count(shapeBody, "Random.") + Count(shapeBody, "new Random");
            bool spearDet = FormationShape.Spear.DeterministicPierce && FormationShape.Spear.Pierce == PierceRule.Facing;
            bool rOk = shapeBody.Length > 0 && shapeRolls == 0 && spearDet;
            ok &= rOk;
            Console.WriteLine("- `FormationShape` の中の乱数: " + shapeRolls + " ／ パターン3の貫きは `Facing`（乱数を引かない枝）: " + (spearDet ? "○" : "**×**") + " → " + (rOk ? "○" : "**×**"));
            Console.WriteLine("- 貫きの枝の `Roll(` は第202期の検査（`form2 check202`）が見ている（生きている駒が0のときの落とし先1本だけ）。パターン3では9マスすべてがどちらかの経路に入るので、その落とし先にも来ない");
        }
        bool allOnPath = Enumerable.Range(0, 9).All(s => FormationShape.Spear.LanesOf(s).Count > 0);
        ok &= allOnPath;
        Console.WriteLine("- パターン3の9マスがすべて貫きの経路のどちらかに入る: " + (allOnPath ? "○" : "**×**"));
        bool det = true;
        foreach (var (_, f) in CompareBuilds().Take(20))
            foreach (EnemyCatalog.Stage stg in EnemyCatalog.Pattern3Copies)
                for (int s = 0; s < 5; s++)
                {
                    var r1 = BattleEngine.Run(f, stg.Enemy, s, verbose: true);
                    var r2 = BattleEngine.Run(f, stg.Enemy, s, verbose: true);
                    det &= r1.PlayerWon == r2.PlayerWon && r1.Turns == r2.Turns && r1.Events.Count == r2.Events.Count;
                }
        ok &= det;
        Console.WriteLine("- 写しの戦を同じ seed で2回（20 行 × 2 波 × 5 seed）: " + (det ? "○ 一致" : "**× ずれた**"));
        Console.WriteLine();

        // (4) 召喚・席の重なり
        Console.WriteLine("## (4) 敵陣の召喚（背かれの餌）がパターン3で落ちない・重ならない");
        Console.WriteLine();
        Formation som = Formation.Build(front1: UnitCatalog.Gald, front3: UnitCatalog.Borg, center: UnitCatalog.Som, back1: UnitCatalog.Hisa, back3: UnitCatalog.Lili);
        int overlap = 0, fodderAt7 = 0, fodder = 0, runs = 0;
        var fodderSlots = new Dictionary<int, int>();
        foreach (EnemyCatalog.Stage stg in EnemyCatalog.Pattern3Copies)
            for (int s = 0; s < 50; s++)
            {
                runs++;
                List<UnitState> pl = BattleEngine.Materialize(som, BattleContext.PlayerTeam);
                List<UnitState> en = BattleEngine.Materialize(stg.Enemy, BattleContext.EnemyTeam);
                var r = BattleEngine.Run(pl, en, s, verbose: true);
                foreach (BattleEvent e in r.Events)
                {
                    if (e.Kind == BattleEventKind.Summon && e.Team == BattleContext.EnemyTeam && e.TargetId is int id)
                    {
                        fodder++;
                        fodderSlots[e.Slot] = fodderSlots.GetValueOrDefault(e.Slot) + 1;
                        if (e.Slot == 7) fodderAt7++;
                    }
                }
                foreach (BattleEvent e in r.Events)
                    if (e.Kind == BattleEventKind.Summon && e.Team == BattleContext.EnemyTeam && !FormationShape.Spear.IsSummonSlot(e.Slot)) overlap++;
            }
        bool sumOk = overlap == 0 && fodderAt7 == 0 && fodder > 0;   // 湧いた回数 0 は「落ちない」ではなく「測っていない」
        ok &= sumOk;
        Console.WriteLine("- ソムを入れた台（ガルド・ボルグ・ソム・ヒサ・ノノ）× 写し2波 × 50 seed = " + runs + " 戦: 餌が湧いた " + fodder + " 回・席 "
                          + string.Join(" ", fodderSlots.OrderBy(kv => kv.Key).Select(kv => FormationRules.SeatNames[kv.Key] + "×" + kv.Value))
                          + " ／ C の席（○前2）に湧いた " + fodderAt7 + " ／ 編成の席に湧いた " + overlap + " → " + (sumOk ? "○" : "**×**"));
        Console.WriteLine();

        Console.WriteLine(ok ? "**すべて ○**" : "**× がある**");
    }

    static int Count(string s, string tok)
    {
        int n = 0, i = 0;
        while ((i = s.IndexOf(tok, i, StringComparison.Ordinal)) >= 0) { n++; i += tok.Length; }
        return n;
    }
}
