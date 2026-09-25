using BattleCore;
using static Common;

// form2 run / check（第200期）。Phase 0 は `Formation2.cs`。

static partial class Formation2Diag
{
    static partial void RunMoreImpl(string mode, string arg, ref bool handled)
    {
        switch (mode)
        {
            case "run": RunTables(); handled = true; return;
            case "check": Check(arg); handled = true; return;
            case "log": Log(arg); handled = true; return;
            case "phase201": Phase201(); handled = true; return;       // 第201期（`Formation2.P201.cs`）
            case "run201": Run201(arg); handled = true; return;
            case "check201": Check201(arg); handled = true; return;
            case "phase202": Phase202(); handled = true; return;       // 第202期（`Formation2.P202.cs`）
            case "run202": Run202(arg); handled = true; return;
            case "check202": Check202(arg); handled = true; return;
        }
    }

    // =================================================================================
    // 組み方
    // =================================================================================

    static readonly int[] XOrder = { 0, 1, 2, 3, 4 };

    /// <summary>X 字の編成から、指定の駒を D（と C）に置き、残りを X 字の席の順で残りの枠に詰めたパターン2。</summary>
    /// <param name="rest">残りを入れるパターン2の枠（A=0,B=1,C=2,D=3,E=4）の順。</param>
    static Formation ToDiamond(Formation x, UnitDef d, UnitDef? c, int[] rest)
    {
        var p = new Formation { Shape = FormationShape.Diamond };
        p[3] = d;
        if (c is not null) p[2] = c;
        var left = XOrder.Select(i => x[i]).Where(u => u is not null && !ReferenceEquals(u, d) && !ReferenceEquals(u, c)).ToList();
        if (left.Count != rest.Length) throw new InvalidOperationException("残りの枚数が合わない: " + left.Count);
        for (int i = 0; i < rest.Length; i++) p[rest[i]] = left[i];
        return p;
    }

    static Formation Replace(Formation f, string id, UnitDef to)
    {
        var g = new Formation { Shape = f.Shape };
        foreach ((int slot, UnitDef d) in f.Occupied()) g[slot] = d.Id == id ? to : d;
        return g;
    }

    static string Seats(Formation f)
    {
        string[] names = f.Shape == FormationShape.Diamond ? new[] { "A", "B", "C", "D", "E" } : new[] { "前1", "前3", "中央", "後1", "後3" };
        return string.Join(" ", f.Occupied().Select(o => names[o.Slot] + ":" + o.Def.Name));
    }

    // =================================================================================
    // 測る
    // =================================================================================

    sealed class M
    {
        public double[] W = new double[5];
        public int N;                       // 第2〜5波の戦数
        public double FrontDead, FrontDeadTurn, FrontByPat0, FrontByPat1, FrontByPat2, FrontByPat3, FrontHits;
        public double AoeSwings, AoeHit; public double SweepSwings, SweepHit, PierceSwings, PierceHit;
        public double NumbedFoes, BeniRing; public int BeniN;
        public double SidEarly, SidDead; public int SidN;
        public double FrontEarly;           // 前衛が2ターン目までに倒れた（前衛1体あたり）
        public int _fdCount;
    }

    /// <summary>勝率（5波・seed 0..199）と、第2〜5波の帳簿。前衛は「一番前の列に立っていた編成の駒」（X 字は前1・前3 の2体）。</summary>
    static M Measure(Formation f, bool ledger, int seed0 = 0, int seeds = Seeds)
    {
        var m = new M();
        object gate = new();
        for (int st = 0; st < 5; st++)
        {
            int wins = 0;
            bool led = ledger && st >= 1;
            Formation enemy = EnemyCatalog.Stages[st].Enemy;
            Parallel.For(seed0, seed0 + seeds, seed =>
            {
                List<UnitState> pl = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
                List<UnitState> en = BattleEngine.Materialize(enemy, BattleContext.EnemyTeam);
                var front = pl.Where(u => u.Row == Row.Front).ToList();
                UnitState? beni = pl.FirstOrDefault(u => u.Def.Id == "beni");
                int ring = beni is null ? 0 : pl.Count(u => u != beni && FormationRules.AreAdjacent(beni, u));
                UnitState? sid = pl.FirstOrDefault(u => u.Def.Id == "sid");
                BattleResult r = BattleEngine.Run(pl, en, seed, verbose: led);
                if (r.PlayerWon) Interlocked.Increment(ref wins);
                if (!led) return;

                var frontIds = front.Select(u => u.InstanceId).ToHashSet();
                var playerIds = pl.Select(u => u.InstanceId).ToHashSet();
                int fd = 0, fe = 0; double fdt = 0; double[] pat = new double[4]; double hits = 0;
                double sw = 0, swh = 0, pi = 0, pih = 0;
                double sidEarly = 0, sidDead = 0;
                var frontAlive = new HashSet<int>(frontIds);
                AttackPattern? cur = null; var curHit = new HashSet<int>(); bool curEnemy = false;
                void Close()
                {
                    if (cur is AttackPattern p && curEnemy && (p == AttackPattern.Sweep || p == AttackPattern.Pierce))
                    {
                        if (p == AttackPattern.Sweep) { sw++; swh += curHit.Count; } else { pi++; pih += curHit.Count; }
                    }
                    cur = null; curHit.Clear();
                }
                foreach (BattleEvent e in r.Events)
                {
                    switch (e.Kind)
                    {
                        case BattleEventKind.Attack:
                        case BattleEventKind.TurnStart:
                            Close();
                            if (e.Kind == BattleEventKind.Attack)
                            {
                                cur = e.Pattern;
                                curEnemy = e.ActorId is int a && !playerIds.Contains(a);
                            }
                            break;
                        case BattleEventKind.Damage:
                            if (e.TargetId is int t && playerIds.Contains(t) && !e.FriendlyFire && !e.Relayed && e.Pattern is AttackPattern dp)
                            {
                                if (curEnemy && cur == dp) curHit.Add(t);
                                if (frontAlive.Contains(t)) { pat[(int)dp]++; hits++; }
                            }
                            break;
                        case BattleEventKind.Death:
                            if (e.TargetId is int dt && frontAlive.Remove(dt)) { fd++; fdt += e.Turn; if (e.Turn <= 2) fe++; }
                            if (sid is not null && e.TargetId == sid.InstanceId) { sidDead++; if (e.Turn <= 2) sidEarly++; }
                            break;
                    }
                }
                Close();
                int numbed = en.Count(u => u.RawCounter(StatusKeys.Numbed) > 0);
                lock (gate)
                {
                    m.N++;
                    m.FrontDead += (double)fd / front.Count; m.FrontEarly += (double)fe / front.Count; m.FrontDeadTurn += fdt; m.FrontHits += hits;
                    m.FrontByPat0 += pat[0]; m.FrontByPat1 += pat[1]; m.FrontByPat2 += pat[2]; m.FrontByPat3 += pat[3];
                    m.SweepSwings += sw; m.SweepHit += swh; m.PierceSwings += pi; m.PierceHit += pih;
                    m.NumbedFoes += numbed;
                    if (beni is not null) { m.BeniN++; m.BeniRing += ring; }
                    if (sid is not null) { m.SidN++; m.SidDead += sidDead; m.SidEarly += sidEarly; }
                    m._fdCount += fd;
                }
            });
            m.W[st] = 100.0 * wins / seeds;
        }
        return m;
    }

    static double Mean25(double[] w) => (w[1] + w[2] + w[3] + w[4]) / 4.0;
    static string Cells(double[] w) => string.Join(" / ", w.Skip(1).Select(x => x.ToString("F1")));
    static string Dp(double x) => x.ToString("+0.0;-0.0;0.0");
    static string Pc(double a, double n) => n == 0 ? "—" : (100.0 * a / n).ToString("F1") + "%";
    static string Av(double a, double n) => n == 0 ? "—" : (a / n).ToString("F2");

    // =================================================================================
    // run
    // =================================================================================

    static void RunTables()
    {
        Console.WriteLine("# 第200期 `form2 run` —— 表A（毒パ）・表B（タンクの土俵）・帳簿");
        Console.WriteLine();
        Console.WriteLine("勝率は seed 0..199・5波。**平均は第2〜5波**（規約 (G10)）。帳簿は第2〜5波の1戦あたり。");
        Console.WriteLine();

        // ---------------- 表A ----------------
        var rowsA = SidDiag.MainForms197();
        var ledgerRows = new List<(string Row, string Col, M X)>();
        Console.WriteLine("## 表A —— 毒パ（主表・第196期の7行）");
        Console.WriteLine();
        Console.WriteLine("P2 は **D＝スィド・C＝ベニ**、残り3枚を X・スィドの席の順（前1・前3・後1・後3）で A・E・B へ。**X・スィドと同じ5枚を並べ替えただけ**。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 元 | X・スィド | **P2** | P2 − X・スィド | P2 の波別 | X・スィドの波別 |");
        Console.WriteLine("|---|--:|--:|--:|--:|---|---|");
        var sumA = new double[3]; int nA = 0; int up = 0;
        var p2A = new List<(string, Formation)>();
        foreach (var (name, orig, _, s) in rowsA)
        {
            UnitDef sid = s.Occupied().First(o => ReferenceEquals(o.Def, SidDiag.SidS)).Def;
            UnitDef beni = s.Occupied().First(o => o.Def.Id == "beni").Def;
            Formation p2 = ToDiamond(s, sid, beni, new[] { 0, 4, 1 });
            p2A.Add((name, p2));
            M mo = Measure(orig, false), ms = Measure(s, true), mp = Measure(p2, true);
            ledgerRows.Add((name, "X・スィド", ms)); ledgerRows.Add((name, "P2", mp));
            double o = Mean25(mo.W), x = Mean25(ms.W), p = Mean25(mp.W);
            bool broken = name.StartsWith("追撃×毒");
            if (!broken) { sumA[0] += o; sumA[1] += x; sumA[2] += p; nA++; if (p > x) up++; }
            Console.WriteLine("| " + name + (broken ? "（**読まない**）" : "") + " | " + o.ToString("F1") + " | " + x.ToString("F1") + " | **" + p.ToString("F1") + "** | "
                              + Dp(p - x) + " | " + Cells(mp.W) + " | " + Cells(ms.W) + " |");
        }
        Console.WriteLine("| **平均（`追撃×毒` を除く " + nA + " 行）** | " + (sumA[0] / nA).ToString("F1") + " | " + (sumA[1] / nA).ToString("F1") + " | **"
                          + (sumA[2] / nA).ToString("F1") + "** | " + Dp((sumA[2] - sumA[1]) / nA) + " | P2 > X・スィド: " + up + " / " + nA + " 行 | |");
        Console.WriteLine();
        Console.WriteLine("`追撃×毒` はグザが唯一の毒の書き手で、第196期に参考の列が壊れた（指示書 §4.1）。値は出すが平均と判定には入れない。");
        Console.WriteLine();
        Console.WriteLine("並び（P2）:");
        Console.WriteLine();
        foreach (var (name, p2) in p2A) Console.WriteLine("- " + name + ": " + Seats(p2));
        Console.WriteLine();

        // ---------------- 表B ----------------
        Console.WriteLine("## 表B —— タンクの土俵（ガルド依存）");
        Console.WriteLine();
        Console.WriteLine("P2 は **D にタンク**、残り4枚を X 字の席の順で A・C・E・B へ。参考の X 字の列は**ガルドの席にそのタンクを置いただけ**（指示書の予測2 を X 字の差と比べるため）。");
        Console.WriteLine();
        (string Tag, UnitDef D)[] tanks = { ("ゴルム", UnitCatalog.Golm), ("ササ", UnitCatalog.Sasa), ("スィド", SidDiag.SidS) };
        Console.WriteLine("| 行 | X・ガルド | **P2・ガルド** | " + string.Join(" | ", tanks.Select(t => "P2・" + t.Tag)) + " | " + string.Join(" | ", tanks.Select(t => "(参考) X・" + t.Tag)) + " | 差 X（ガルド − 3枚平均） | 差 P2 |");
        Console.WriteLine("|---|--:|--:|" + string.Concat(tanks.Select(_ => "--:|")) + string.Concat(tanks.Select(_ => "--:|")) + "--:|--:|");
        double gapX = 0, gapP = 0, sumXg = 0, sumPg = 0; int nB = 0, pgDown = 0;
        var sumPT = new double[3]; var sumXT = new double[3];
        var waveGapP2mX = new double[5];
        foreach (string row in TankRows)
        {
            Formation x = CompareBuilds().First(r => r.Name == row).F;
            UnitDef gald = x.Occupied().First(o => o.Def.Id == "gald").Def;
            Formation p2g = ToDiamond(x, gald, null, new[] { 0, 2, 4, 1 });
            M mxg = Measure(x, true), mpg = Measure(p2g, true);
            ledgerRows.Add((row, "X・ガルド", mxg)); ledgerRows.Add((row, "P2・ガルド", mpg));
            var pt = tanks.Select(t => Measure(Replace(p2g, "gald", t.D), false)).ToArray();
            var xt = tanks.Select(t => Measure(Replace(x, "gald", t.D), false)).ToArray();
            double xg = Mean25(mxg.W), pg = Mean25(mpg.W);
            double gx = xg - xt.Average(q => Mean25(q.W)), gp = pg - pt.Average(q => Mean25(q.W));
            gapX += gx; gapP += gp; sumXg += xg; sumPg += pg; nB++; if (pg < xg) pgDown++;
            for (int i = 0; i < 3; i++) { sumPT[i] += Mean25(pt[i].W); sumXT[i] += Mean25(xt[i].W); }
            for (int w = 1; w < 5; w++) waveGapP2mX[w] += mpg.W[w] - mxg.W[w];
            Console.WriteLine("| " + row + " | " + xg.ToString("F1") + " | **" + pg.ToString("F1") + "** | " + string.Join(" | ", pt.Select(q => Mean25(q.W).ToString("F1"))) + " | "
                              + string.Join(" | ", xt.Select(q => Mean25(q.W).ToString("F1"))) + " | " + Dp(gx) + " | " + Dp(gp) + " |");
        }
        Console.WriteLine("| **平均** | " + (sumXg / nB).ToString("F1") + " | **" + (sumPg / nB).ToString("F1") + "** | " + string.Join(" | ", sumPT.Select(v => (v / nB).ToString("F1"))) + " | "
                          + string.Join(" | ", sumXT.Select(v => (v / nB).ToString("F1"))) + " | " + Dp(gapX / nB) + " | " + Dp(gapP / nB) + " |");
        Console.WriteLine();
        Console.WriteLine("- P2・ガルドが X・ガルドより下がった行: **" + pgDown + " / " + nB + "**");
        Console.WriteLine("- P2・ガルド − X・ガルド（6行平均）の波別: " + string.Join(" / ", Enumerable.Range(1, 4).Select(w => "第" + (w + 1) + "波 " + Dp(waveGapP2mX[w] / nB))));
        Console.WriteLine();

        // ---------------- 帳簿 ----------------
        Console.WriteLine("## 帳簿（第2〜5波・1戦あたり）");
        Console.WriteLine();
        Console.WriteLine("前衛＝開戦時に一番前の列に立っていた編成の駒（X 字は前1・前3 の2体、P2 は D の1体）。"
                          + "「倒れた」は前衛1体あたりの割合。受けた攻撃は前衛が倒れるまでに受けた攻撃のダメージの段数（単体／薙ぎ／貫き／全体）。"
                          + "巻き込み人数は敵の薙ぎ・貫き1回に当たった味方の人数。");
        Console.WriteLine();
        Console.WriteLine("| 行 | 列 | 前衛が倒れた | 倒れたT | 受けた段 単/薙/貫/全 | 薙ぎ1回の人数 | 貫き1回の人数 | 印の付いた敵 | ベニの隣 | スィド倒れ | うち≤2T |");
        Console.WriteLine("|---|---|--:|--:|---|--:|--:|--:|--:|--:|--:|");
        double[] sweepX = new double[2], sweepP = new double[2], pierceX = new double[2], pierceP = new double[2];
        foreach (var (row, col, q) in ledgerRows)
        {
            double n = q.N;
            bool isP = col.StartsWith("P2");
            if (isP) { sweepP[0] += q.SweepHit; sweepP[1] += q.SweepSwings; pierceP[0] += q.PierceHit; pierceP[1] += q.PierceSwings; }
            else { sweepX[0] += q.SweepHit; sweepX[1] += q.SweepSwings; pierceX[0] += q.PierceHit; pierceX[1] += q.PierceSwings; }
            Console.WriteLine("| " + row + " | " + col + " | " + Pc(q.FrontDead, n) + " | " + Av(q.FrontDeadTurn, q._fdCount) + " | "
                              + Av(q.FrontByPat0, n) + " / " + Av(q.FrontByPat1, n) + " / " + Av(q.FrontByPat2, n) + " / " + Av(q.FrontByPat3, n) + " | "
                              + Av(q.SweepHit, q.SweepSwings) + " | " + Av(q.PierceHit, q.PierceSwings) + " | " + Av(q.NumbedFoes, n) + " | "
                              + (q.BeniN == 0 ? "—" : Av(q.BeniRing, q.BeniN)) + " | " + (q.SidN == 0 ? "—" : Pc(q.SidDead, q.SidN)) + " | " + (q.SidN == 0 ? "—" : Pc(q.SidEarly, q.SidN)) + " |");
        }
        Console.WriteLine();
        Console.WriteLine("- 巻き込み人数（全行を合わせて）: 薙ぎ X 字 **" + Av(sweepX[0], sweepX[1]) + "** 人 対 P2 **" + Av(sweepP[0], sweepP[1]) + "** 人 ／ "
                          + "貫き X 字 **" + Av(pierceX[0], pierceX[1]) + "** 人 対 P2 **" + Av(pierceP[0], pierceP[1]) + "** 人");
        Console.WriteLine();
    }

    // =================================================================================
    // check —— 自己検査
    // =================================================================================

    static void Check(string arg)
    {
        Console.WriteLine("# 第200期 `form2 check` —— 自己検査");
        Console.WriteLine();
        bool ok = true;
        FormationShape d = FormationShape.Diamond;
        int[] seat = d.PlayableSlots.ToArray();   // A..E
        string N(int slot) { int i = Array.IndexOf(seat, slot); return i >= 0 ? ((char)('A' + i)).ToString() : FormationRules.SeatNames[slot]; }

        // (3) 表の全セル
        Console.WriteLine("## (3) パターン2の隣接・薙ぎ・貫通が §2.2 の表と一致する");
        Console.WriteLine();
        var wantAdj = new Dictionary<char, string> { ['A'] = "BCD", ['B'] = "ACE", ['C'] = "ABDE", ['D'] = "ACE", ['E'] = "BCD" };
        Console.WriteLine("| 位置 | 期待 | 実装（編成の席だけ） | 一致 |");
        Console.WriteLine("|---|---|---|:-:|");
        for (int i = 0; i < 5; i++)
        {
            char c = (char)('A' + i);
            string got = string.Concat(Enumerable.Range(0, 5).Where(j => d.AreAdjacent(seat[i], seat[j])).Select(j => (char)('A' + j)));
            bool eq = got == wantAdj[c];
            // 対称性も見る
            bool sym = Enumerable.Range(0, 9).All(b => d.AreAdjacent(seat[i], b) == d.AreAdjacent(b, seat[i]));
            ok &= eq && sym;
            Console.WriteLine("| " + c + " | " + wantAdj[c] + " | " + got + (sym ? "" : "（**非対称**）") + " | " + (eq && sym ? "○" : "**×**") + " |");
        }
        Console.WriteLine();
        // 薙ぎ: D に薙ぎ → D,A,C,E
        string sweepD = "D" + string.Concat(Enumerable.Range(0, 5).Where(j => seat[j] != 7 && d.SweepTargets(7).Contains(seat[j])).Select(j => (char)('A' + j)));
        bool sOk = sweepD == "DACE";
        string L(int lane) => string.Concat(d.LanePath(lane).Where(s => d.IsPlayable(s)).Select(s => N(s)).OrderBy(x => x));
        string l12 = L(0), l23 = L(1);
        bool pOk = l12 == "ABCD" && l23 == "BCDE";
        ok &= sOk && pOk;
        Console.WriteLine("| 攻撃 | 期待 | 実装 | 一致 |");
        Console.WriteLine("|---|---|---|:-:|");
        Console.WriteLine("| D に薙ぎ | DACE | " + sweepD + " | " + (sOk ? "○" : "**×**") + " |");
        Console.WriteLine("| 1-2 レーンへの貫通 | ABCD | " + l12 + "（走る順 " + string.Join("→", d.LanePath(0).Where(d.IsPlayable).Select(N)) + "） | " + (l12 == "ABCD" ? "○" : "**×**") + " |");
        Console.WriteLine("| 2-3 レーンへの貫通 | BCDE | " + l23 + "（走る順 " + string.Join("→", d.LanePath(1).Where(d.IsPlayable).Select(N)) + "） | " + (l23 == "BCDE" ? "○" : "**×**") + " |");
        Console.WriteLine();
        Console.WriteLine("9マスに広げた表（四隅は召喚枠）:");
        Console.WriteLine();
        Console.WriteLine("| 席 | 陣形の名 | 隣接 | 薙ぎの巻き込み | レーン |");
        Console.WriteLine("|---|---|---|---|---|");
        for (int s = 0; s < 9; s++)
            Console.WriteLine("| " + FormationRules.SeatNames[s] + " | " + N(s) + " | " + string.Join(" ", Enumerable.Range(0, 9).Where(b => d.AreAdjacent(s, b)).Select(N))
                              + " | " + string.Join(" ", d.SweepTargets(s).Select(N)) + " | " + string.Join(",", d.LanesOf(s).Select(l => l == 0 ? "1-2" : "2-3")) + " |");
        Console.WriteLine();
        // X 字の表が FormationRules と同じ
        bool xSame = Enumerable.Range(0, 9).All(a => Enumerable.Range(0, 9).All(b => FormationShape.X.AreAdjacent(a, b) == FormationRules.AreAdjacent(a, b)))
                     && Enumerable.Range(0, 9).All(a => FormationShape.X.SweepTargets(a).SequenceEqual(FormationRules.SweepTargets(a)))
                     && Enumerable.Range(0, 9).All(a => FormationShape.X.LanesOf(a).SequenceEqual(FormationRules.LanesOf(a)))
                     && Enumerable.Range(0, 9).All(a => Enumerable.Range(0, 9).All(b => FormationShape.X.IsLanePredecessor(a, b) == FormationRules.IsLanePredecessor(a, b)))
                     && Enumerable.Range(0, 9).All(a => FormationShape.X.IsSummonSlot(a) == FormationRules.IsSummonSlot(a))
                     && new[] { Row.Front, Row.Mid, Row.Back }.All(r => FormationShape.X.PlayableSlotsOfRow(r).SequenceEqual(FormationRules.PlayableSlotsOfRow(r)));
        ok &= xSame;
        Console.WriteLine("- X 字の陣形の表が `FormationRules` の静的な表と全セル一致: " + (xSame ? "○" : "**×**"));
        Console.WriteLine();

        // (1) compare
        Console.WriteLine("## (1) `compare` が 0 セル差分");
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
        else Console.WriteLine("- `" + path + "` が無い（飛ばす）");
        Console.WriteLine();

        // (2) 乱数を引かない: パターン2の貫きは Roll を呼ばない（本文の走査）
        Console.WriteLine("## (2) 新しい処理が乱数を引かない");
        Console.WriteLine();
        if (ParryScan.Init())
        {
            string eng = ParryScan.Read(Path.Combine("BattleCore", "BattleEngine.cs"));
            int a = eng.IndexOf("if (shape." + "DeterministicPierce)");
            int b = a < 0 ? -1 : eng.IndexOf("var lanes = ", a);
            string body = a < 0 || b < 0 ? "" : eng.Substring(a, b - a);
            // フォールバック（生きている駒が0のとき）の1本だけは既存と同じ Roll。0 のときしか走らない
            int rolls = Count(body, "Roll(");
            string models = ParryScan.Read(Path.Combine("BattleCore", "Models.cs"));
            int ms = models.IndexOf("public sealed class " + "FormationShape"), me = models.IndexOf("public sealed class " + "Formation\n", ms);
            if (me < 0) me = models.IndexOf("public sealed class " + "Formation\r", ms);
            string shapeBody = ms < 0 || me < 0 ? "" : models.Substring(ms, me - ms);
            int shapeRolls = Count(shapeBody, "Roll(") + Count(shapeBody, "Random");
            bool rOk = body.Length > 0 && rolls == 1 && shapeBody.Length > 0 && shapeRolls == 0;
            ok &= rOk;
            Console.WriteLine("- パターン2の貫きの枝の `Roll(`: " + rolls + " 本（生きている駒が0のときの既存の落とし先1本だけ）／ `FormationShape` の中の乱数: " + shapeRolls + " → " + (rOk ? "○" : "**×**"));
        }
        // 実測: 同じ seed で2回回して一致、かつ P2 の戦で乱数列が編成の中身だけで決まる（決定的）
        Formation probe = Formation.BuildDiamond(UnitCatalog.Hisa, UnitCatalog.Nono, UnitCatalog.Beni, UnitCatalog.Gald, UnitCatalog.Kado);
        bool det = Enumerable.Range(0, 20).All(s =>
        {
            var r1 = BattleEngine.Run(probe, EnemyCatalog.Stages[4].Enemy, s, verbose: true);
            var r2 = BattleEngine.Run(probe, EnemyCatalog.Stages[4].Enemy, s, verbose: true);
            return r1.PlayerWon == r2.PlayerWon && r1.Turns == r2.Turns && r1.Events.Count == r2.Events.Count;
        });
        ok &= det;
        Console.WriteLine("- パターン2の戦を同じ seed で2回（20 seed）: " + (det ? "○ 一致" : "**× ずれた**"));
        Console.WriteLine();

        // (4) 召喚が落ちない・重ならない
        Console.WriteLine("## (4) 召喚がパターン2で落ちない・重ならない");
        Console.WriteLine();
        Formation mug = Formation.BuildDiamond(UnitCatalog.Mug, UnitCatalog.Mug, UnitCatalog.Mug, UnitCatalog.Mug, UnitCatalog.Mug);
        int summons = 0, overlap = 0, bad = 0, crashed = 0;
        foreach (int st in Enumerable.Range(0, 5))
            for (int s = 0; s < 200; s++)
            {
                try
                {
                    List<UnitState> pl = BattleEngine.Materialize(mug, BattleContext.PlayerTeam);
                    BattleResult r = BattleEngine.Run(pl, BattleEngine.Materialize(EnemyCatalog.Stages[st].Enemy, BattleContext.EnemyTeam), s, verbose: true);
                    var slotOf = new Dictionary<int, int>();   // 生きている味方の InstanceId → 席（イベントを追う）
                    foreach (UnitState u in pl) slotOf[u.InstanceId] = u.Slot;
                    void Look() { if (slotOf.Values.Count() != slotOf.Values.Distinct().Count()) overlap++; }
                    foreach (BattleEvent e in r.Events)
                    {
                        if (e.TargetId is not int id) { if (e.Kind == BattleEventKind.TurnStart) Look(); continue; }
                        bool mine = slotOf.ContainsKey(id) || e.Team == BattleContext.PlayerTeam;
                        switch (e.Kind)
                        {
                            case BattleEventKind.Summon when e.Team == BattleContext.PlayerTeam:
                                summons++;
                                if (!FormationShape.Diamond.SummonSlots.Contains(e.Slot)) bad++;
                                slotOf[id] = e.Slot; break;
                            case BattleEventKind.Move when slotOf.ContainsKey(id): slotOf[id] = e.Slot; break;
                            case BattleEventKind.Death: slotOf.Remove(id); break;
                            case BattleEventKind.Revive when pl.Any(u => u.InstanceId == id): slotOf[id] = e.Slot; break;
                        }
                    }
                    Look();
                }
                catch (Exception ex) { crashed++; if (crashed == 1) Console.WriteLine("  例外: " + ex.Message); }
            }
        bool sumOk = crashed == 0 && bad == 0 && overlap == 0 && summons > 0;
        ok &= sumOk;
        Console.WriteLine("- 胞子体ムグ5枚のパターン2 × 5波 × 200 seed: 召喚 " + summons + " 回・四隅の外 " + bad + "・終局の席の重なり " + overlap + "・例外 " + crashed + " → " + (sumOk ? "○" : "**×**"));
        Console.WriteLine();

        Console.WriteLine("**総合: " + (ok ? "ok=True" : "ok=False") + "**");
    }
}

static partial class Formation2Diag
{
    /// <summary>1戦のログ（検算用）: `form2 log "行名" 波(1-5) seed [x]`。表B の P2・ガルド（x で X 字）を回す。</summary>
    internal static void Log(string arg)
    {
        var parts = System.Text.RegularExpressions.Regex.Matches(arg, @"""[^""]*""|\S+").Select(m => m.Value.Trim('"')).ToList();
        Formation x = CompareBuilds().First(r => r.Name == parts[0]).F;
        UnitDef gald = x.Occupied().First(o => o.Def.Id == "gald").Def;
        Formation f = parts.Count > 3 && parts[3] == "x" ? x : ToDiamond(x, gald, null, new[] { 0, 2, 4, 1 });
        BattleResult r = BattleEngine.Run(f, EnemyCatalog.Stages[int.Parse(parts[1]) - 1].Enemy, int.Parse(parts[2]), verbose: true);
        Console.WriteLine(Seats(f));
        foreach (LogLine l in r.Log) Console.WriteLine(l);
    }
}
