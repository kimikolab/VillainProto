using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// burnhit run —— 段2 ＋ 表A〜H（指示書 §4.3）。
static partial class BurnHitDiag
{
    internal static Dictionary<(string B, string V, int W, int S), Agg> _cells = new();
    internal static Agg At(string b, string v, int w, int s) => _cells[(b, v, w, IsTarget(w) ? 0 : s)];
    internal static Agg Grp(string b, string v, (int W, int S)[] cells)
    {
        var m = new Agg();
        foreach (var (w, s) in cells) m.Merge(At(b, v, w, s));
        return m;
    }
    internal static readonly int[] MainWaves = { 0, 1, 2, 3 };
    static string[] _boards = FixedBoards;

    static void Put(string b, string vn, Formation f)
    {
        var keys = new List<(int W, int S)>();
        for (int w = 0; w < WaveNames.Length; w++)
            for (int s = 0; s < (IsTarget(w) ? 1 : BA.Scales.Length); s++) keys.Add((w, s));
        foreach (var (w, s) in keys) _cells[(b, vn, w, s)] = Measure(f, w, ScOf(w, s));
    }

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ranked = Ranked();
        Console.WriteLine("# 第255期 段2 —— 被弾の燃焼（版 H0 ／ H-足す ／ H-分担 ／ H-分担1 ／ H-敵だけ）");
        Console.WriteLine();
        TableB(ranked);
        _boards = FixedBoards.Concat(new[] { "T3(H0選)", "T3(H-分担選)", "混ぜ(H0選)", "混ぜ(H-分担選)" }).Concat(RefBoards).ToArray();
        _cells = new();
        foreach (string bn in _boards)
        {
            var f = BaseOf(bn, ranked);
            foreach (var v in Versions) Put(bn, v.Name, Mark(f, v.Card));
        }
        Console.Error.WriteLine($"  段2 の測定 {sw.Elapsed.TotalSeconds:F0} 秒");
        Console.WriteLine("## 台（席は版に依らない・版は札1枚だけ）");
        Console.WriteLine();
        foreach (string bn in _boards) Console.WriteLine($"- {bn}: {BA.SeatsNamed(BaseOf(bn, ranked))}");
        Console.WriteLine();
        foreach (var v in Versions) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();
        TableA(); TableA2(); TableC(); TableD(); TableE(); TableF(); TableG(); TableTarget(); TableDeath();
        TableH();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒・seed 0..{BA.Seeds - 1}・verbose）");
    }

    static IEnumerable<(string B, string V)> Rows(IEnumerable<string>? boards = null)
    {
        foreach (string b in boards ?? _boards) foreach (var v in Versions) yield return (b, v.Name);
    }
    static double WinP(Agg c) => c.N == 0 ? double.NaN : 100.0 * c.Wins / c.N;
    static double SurvP(Agg c) => c.N == 0 ? double.NaN : 100.0 * c.AllSurv / c.N;
    static double MainAvg(string b, string v, int s, Func<Agg, double> f) => MainWaves.Average(w => f(At(b, v, w, s)));

    // ---------------------------------------------------------------------------------
    static void TableA()
    {
        Console.WriteLine("## 表A —— 台 × 版（勝率 ／ 全員生存 ／ 決着T ／ 落ちた駒）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 本編 400/300 勝率 ／ 全員生存（第2〜5波の平均） | 本編 200/200 勝率 ／ 全員生存 | 本編 115/115 全員生存 | 本編 400/300 決着T | 九/新兵 400/300 全員生存 ／ 決着T | 九/新兵 200/200 全員生存 ／ 決着T | 九/農兵 400/300 全員生存 | 第五波 400/300 勝率 ／ 全員生存 | 重い波 400/300 全員生存 ／ 決着T | 落ちた/戦（本編 400/300） |");
        Console.WriteLine("|---|---|---|---|--:|--:|---|---|--:|---|---|--:|");
        foreach (var (b, v) in Rows())
        {
            var n9 = At(b, v, BA.MainWave, 1); var n92 = At(b, v, BA.MainWave, 0); var h = At(b, v, FC.WaveHeavy, 1); var w5 = At(b, v, 3, 1);
            var main = Grp(b, v, MainWaves.Select(w => (w, 1)).ToArray());
            Console.WriteLine($"| {b} | {v} | {F1(MainAvg(b, v, 1, WinP))} ／ {F1(MainAvg(b, v, 1, SurvP))} | {F1(MainAvg(b, v, 0, WinP))} ／ {F1(MainAvg(b, v, 0, SurvP))} | {F1(MainAvg(b, v, 2, SurvP))} | {Per(main.Turns, main.N)} | "
                + $"{F1(SurvP(n9))} ／ {Per(n9.Turns, n9.N)} | {F1(SurvP(n92))} ／ {Per(n92.Turns, n92.N)} | {F1(SurvP(At(b, v, BA.MainWave + 1, 1)))} | {F1(WinP(w5))} ／ {F1(SurvP(w5))} | {F1(SurvP(h))} ／ {Per(h.Turns, h.N)} | {Per(main.Fell, main.N)} |");
        }
        Console.WriteLine();
    }

    static void TableA2()
    {
        Console.WriteLine("### 表A′ —— 全セル（勝率 ／ 全員生存 ／ 決着T ／ 落ちた/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | " + string.Join(" | ", WaveNames.Take(FC.WaveHeavy + 1)) + " |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", FC.WaveHeavy + 1)));
        foreach (var (b, v) in Rows())
            for (int s = 0; s < BA.Scales.Length; s++)
                Console.WriteLine($"| {b} | {v} | {BA.Scales[s].Name} | " + string.Join(" | ", Enumerable.Range(0, FC.WaveHeavy + 1).Select(w => { var c = At(b, v, w, s); return $"{F1(WinP(c))} ／ {F1(SurvP(c))} ／ {Per(c.Turns, c.N)} ／ {Per(c.Fell, c.N)}"; })) + " |");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    static string Sc(FB.Score4 s) => $"{s.S.Sv}/40・400/300 {s.Sv4}/40・落 {s.S.Fell}";
    static void TableB(Dictionary<string, List<FB.Pick>> ranked)
    {
        var many = ManyHands;
        Console.WriteLine("## 表B —— 手数の多い駒に価値が生まれたか（段1 の上位の相方）");
        Console.WriteLine();
        Console.WriteLine($"手数の多い駒（Phase 0 で固定・命中 ÷ 手番の上位 10）: {string.Join("・", many.Select(id => UnitCatalog.ById(id).Name))}。移動軸: {string.Join("・", MoveAxis.Select(id => UnitCatalog.ById(id).Name))}。");
        Console.WriteLine("T3 は 1,081 組 × 席 120 × seed 1000..1039（九/新兵 × 200/200 → 400/300 → 落 → 決着T）。"
            + $"混ぜ（ボルグ・ヒヨ ＋ 3枠・17,296 組）は一次を 400/300 × seed 1000..{1000 + MixFirstSeeds - 1} で回し、上位 {MixKeep} 組を T3 と同じ並びで選び直した。");
        Console.WriteLine();
        Console.WriteLine("| 段1 | 版 | 1位（相方） | 席 | 得点 | 2位 | 3位 | 上位10 の相方（のべ・多い順） | 上位10 の枠のうち 手数 ／ 移動軸 | 上位30 の枠のうち 手数 ／ 移動軸 | 200/200 で 40/40 の組 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|--:|");
        foreach (string kind in new[] { "T3", "混ぜ" })
            foreach (string vn in PickVersions)
            {
                var rk = ranked[PickKey(kind, vn)];
                var b = rk[0];
                string top10 = string.Join(" ", rk.Take(10).SelectMany(p => p.Partners).GroupBy(x => x).OrderByDescending(g => g.Count()).Take(8)
                    .Select(g => $"{UnitCatalog.ById(g.Key).Name} {g.Count()}"));
                (int m, int mv, int n) Cnt(int k) { var ps = rk.Take(k).SelectMany(p => p.Partners).ToList(); return (ps.Count(many.Contains), ps.Count(MoveAxis.Contains), ps.Count); }
                var c10 = Cnt(10); var c30 = Cnt(30);
                int full = rk.Count(p => p.BestS.S.Sv == BA.PickSeeds);
                string nm(FB.Pick p) => string.Join("・", p.Partners.Select(id => UnitCatalog.ById(id).Name));
                Console.WriteLine($"| {kind} | {vn} | {nm(b)} | {BA.SeatsNamed(FB.Dec(b.Best))} | {Sc(b.BestS)} | {nm(rk[1])} {Sc(rk[1].BestS)} | {nm(rk[2])} {Sc(rk[2].BestS)} | {top10} | {c10.m}/{c10.n} ／ {c10.mv}/{c10.n} | {c30.m}/{c30.n} ／ {c30.mv}/{c30.n} | {full} |");
            }
        Console.WriteLine();
        // 候補の中の割合（基準）
        foreach (string kind in new[] { "T3", "混ぜ" })
        {
            var cands = kind == "T3" ? BA.Candidates : MixCandidates;
            Console.WriteLine($"- {kind} の候補 {cands.Count} 枚のうち 手数 {cands.Count(c => many.Contains(c.Id))} 枚（{Pct(cands.Count(c => many.Contains(c.Id)), cands.Count)}%）・移動軸 {cands.Count(c => MoveAxis.Contains(c.Id))} 枚（{Pct(cands.Count(c => MoveAxis.Contains(c.Id)), cands.Count)}%）");
        }
        Console.WriteLine();
        // H0 と H-分担 の順位の入れ替わり（同じ組の順位）
        foreach (string kind in new[] { "T3", "混ぜ" })
        {
            var r0 = ranked[PickKey(kind, "H0")]; var r1 = ranked[PickKey(kind, "H-分担")];
            var pos0 = r0.Select((p, i) => (string.Join(",", p.Partners.OrderBy(x => x)), i)).ToDictionary(x => x.Item1, x => x.i);
            Console.WriteLine($"### {kind}: H-分担 の上位10 の組の H0 での順位");
            Console.WriteLine();
            Console.WriteLine("| H-分担 順 | 相方 | H-分担 得点 | H0 での順位 |");
            Console.WriteLine("|--:|---|---|--:|");
            for (int i = 0; i < 10 && i < r1.Count; i++)
            {
                string k = string.Join(",", r1[i].Partners.OrderBy(x => x));
                Console.WriteLine($"| {i + 1} | {string.Join("・", r1[i].Partners.Select(id => UnitCatalog.ById(id).Name))} | {Sc(r1[i].BestS)} | {(pos0.TryGetValue(k, out var j) ? (j + 1).ToString() : "（H0 の上位外）")} |");
            }
            Console.WriteLine();
        }
    }

    // ---------------------------------------------------------------------------------
    static readonly (string Name, (int W, int S)[] Cells)[] CGroups =
    {
        ("九/新兵 400/300", new[] { (BA.MainWave, 1) }),
        ("本編 400/300", new[] { (0, 1), (1, 1), (2, 1), (3, 1) }),
        ("重い波 400/300", new[] { (FC.WaveHeavy, 1) }),
    };
    static string TopActors(Agg m, string side, int k, int idx)
        => string.Join(" ", m.ByActor.Where(kv => kv.Key.StartsWith(side)).OrderByDescending(kv => kv.Value[idx]).Take(k)
            .Select(kv => $"{NameOf(kv.Key)[2..]} {Per(kv.Value[idx], m.N)}"));
    static IEnumerable<string> FireBoards => _boards.Where(b => b is not ("毒" or "参考 移動"));

    static void TableC()
    {
        Console.WriteLine("## 表C —— 燃焼ダメージの出どころ（敵に入った燃焼: ターン頭の刻み ／ 被弾の燃焼・/戦）");
        Console.WriteLine();
        Console.WriteLine("HP は刻みの前後の差（脆さ・軛を通った後）。殴り手は「その一撃で被弾の燃焼が起きた」味方の駒（削った HP/戦・上位4）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 刻み 回 ／ HP | 被弾 回 ／ 6 の刻み ／ HP | 被弾の割合 | 敵が被弾の燃焼で倒れた/戦 | 被弾の燃焼を起こした殴り手（HP/戦） |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|---|");
        foreach (string b in FireBoards)
            foreach (var (gn, cells) in CGroups)
                foreach (var v in Versions)
                {
                    var m = Grp(b, v.Name, cells);
                    long tot = m.TickDmg[1] + m.HitDmg[1];
                    Console.WriteLine($"| {b} | {gn} | {v.Name} | {Per(m.TickFires[1], m.N)} ／ {Per(m.TickDmg[1], m.N)} | {Per(m.Fires[1], m.N)} ／ {Per(m.Units[1], m.N)} ／ {Per(m.HitDmg[1], m.N)} | {Pct(m.HitDmg[1], tot)} | {Per(m.Kills[1], m.N)} | {TopActors(m, "0:", 4, 2)} |");
                }
        Console.WriteLine();
    }

    static void TableD()
    {
        Console.WriteLine("## 表D —— 被弾時の回復（味方側・/戦）");
        Console.WriteLine();
        Console.WriteLine("味方が燃えていて殴られると、被弾の燃焼が刻みと同じ規則を通る（ヒヨの火の変換・ベニの反転・ボルグとホタの火の癒しでは回復）。H-敵だけ は味方に被弾の燃焼が起きない版（差がその分）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 刻みで回復 ／ 削った | 被弾で回復 ／ 削った | 被弾の燃焼 回 | 回復の多い味方（被弾・HP/戦） | 全員生存 | 落ちた/戦 | 落ちやすい味方（%） |");
        Console.WriteLine("|---|---|---|---|---|--:|---|--:|--:|---|");
        foreach (string b in FireBoards)
            foreach (var (gn, cells) in CGroups)
                foreach (var v in Versions)
                {
                    var m = Grp(b, v.Name, cells);
                    string heal = string.Join(" ", m.ByTarget.Where(kv => kv.Key.StartsWith("0:")).OrderByDescending(kv => kv.Value[2]).Take(3)
                        .Select(kv => $"{NameOf(kv.Key)[2..]} {Per(kv.Value[2], m.N)}"));
                    string fell = string.Join(" ", m.FellBy.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{UnitCatalog.ById(kv.Key).Name} {Pct(kv.Value, m.N)}"));
                    Console.WriteLine($"| {b} | {gn} | {v.Name} | {Per(m.TickHeal[0], m.N)} ／ {Per(m.TickDmg[0], m.N)} | {Per(m.HitHeal[0], m.N)} ／ {Per(m.HitDmg[0], m.N)} | {Per(m.Fires[0], m.N)} | {heal} | {F1(SurvP(m))} | {Per(m.Fell, m.N)} | {fell} |");
                }
        Console.WriteLine();
    }

    static void TableE()
    {
        Console.WriteLine("## 表E —— 被弾の燃焼の回数・跳ね（敵 ＋ 味方・/戦）");
        Console.WriteLine();
        Console.WriteLine("跳ね ＝ 同じ1回の攻撃（手番の一振り全体・手番の外の1回の攻撃）の中で、同じ駒に2回目以上起きた被弾の燃焼。分担1 で止めた ＝ 分担1 がその2回目以上を止めた回数。1枠の回数 ＝ 被弾の燃焼が起きた枠のうち、1枠で3回以上起きた割合。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波 | 版 | 起きた 敵 ／ 味方 | 6 の刻み 敵 ／ 味方 | 跳ね | 分担1 で止めた | 1枠 3回以上 % | 跳ねの多い殴り手（起きた回/戦） |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|--:|---|");
        foreach (string b in FireBoards)
            foreach (var (gn, cells) in CGroups.Concat(new[] { ("的・九（8T）", new[] { (FC.WaveTarget9, 0) }) }))
                foreach (var v in Versions.Skip(1))
                {
                    var m = Grp(b, v.Name, cells);
                    long sc = m.PerScope.Sum(), sc3 = m.PerScope.Skip(3).Sum();
                    Console.WriteLine($"| {b} | {gn} | {v.Name} | {Per(m.Fires[1], m.N)} ／ {Per(m.Fires[0], m.N)} | {Per(m.Units[1], m.N)} ／ {Per(m.Units[0], m.N)} | {Per(m.Repeats.Sum(), m.N)} | {Per(m.Skipped.Sum(), m.N)} | {Pct(sc3, sc)} | {TopActors(m, "0:", 3, 0)} |");
                }
        Console.WriteLine();
    }

    static void TableF()
    {
        Console.WriteLine("## 表F —— 軛の波（第四波）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 400/300 勝率 ／ 全員生存 ／ 決着T | 200/200 勝率 ／ 全員生存 ／ 決着T | 115/115 勝率 ／ 全員生存 ／ 決着T | 敵の燃焼 刻み ／ 被弾 HP/戦（400/300） |");
        Console.WriteLine("|---|---|---|---|---|---|");
        foreach (var (b, v) in Rows(FireBoards))
        {
            string c(int s) { var a = At(b, v, FC.WaveYoke, s); return $"{F1(WinP(a))} ／ {F1(SurvP(a))} ／ {Per(a.Turns, a.N)}"; }
            var y = At(b, v, FC.WaveYoke, 1);
            Console.WriteLine($"| {b} | {v} | {c(1)} | {c(0)} | {c(2)} | {Per(y.TickDmg[1], y.N)} ／ {Per(y.HitDmg[1], y.N)} |");
        }
        Console.WriteLine();
    }

    static void TableG()
    {
        Console.WriteLine("## 表G —— 燃焼を使わない編成が動いていないこと（台本の指紋）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | H0 と台本の指紋が一致したセル | 本編 400/300 勝率 ／ 全員生存 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (string b in RefBoards)
            foreach (var v in Versions.Skip(1))
            {
                int same = 0, n = 0;
                foreach (var key in _cells.Keys.Where(k => k.B == b && k.V == "H0"))
                {
                    n++;
                    if (_cells[(b, v.Name, key.W, key.S)].Sig == _cells[key].Sig) same++;
                }
                Console.WriteLine($"| {b} | {v.Name} | {same} / {n} | {F1(MainAvg(b, v.Name, 1, WinP))} ／ {F1(MainAvg(b, v.Name, 1, SurvP))} |");
            }
        Console.WriteLine();
    }

    static void TableTarget()
    {
        Console.WriteLine("## 的の波（8 ターンまで・敵に入った HP/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 的・一 計 ／ 燃焼の刻み ／ 被弾の燃焼 | 的・九 計 ／ 燃焼の刻み ／ 被弾の燃焼 |");
        Console.WriteLine("|---|---|---|---|");
        foreach (var (b, v) in Rows(FireBoards))
        {
            var a = At(b, v, FC.WaveTarget1, 0); var c = At(b, v, FC.WaveTarget9, 0);
            Console.WriteLine($"| {b} | {v} | {Per(a.T8Total, a.N)} ／ {Per(a.T8Tick, a.N)} ／ {Per(a.T8Hit, a.N)} | {Per(c.T8Total, c.N)} ／ {Per(c.T8Tick, c.N)} ／ {Per(c.T8Hit, c.N)} |");
        }
        Console.WriteLine();
    }

    static void TableDeath()
    {
        long cs = 0, de = 0, fr = 0; var by = new long[Causes.Length];
        foreach (var c in _cells.Values) { cs += c.Cause.Sum(); de += c.DeathEv; fr += c.FellRes; for (int i = 0; i < by.Length; i++) by[i] += c.Cause[i]; }
        Console.WriteLine($"受け入れ 4: 死因の合計 {cs} ＝ 倒れた出来事 {de} ＝ 落ちた駒（結果）{fr}（{(cs == de && de == fr ? "一致" : "**不一致**")}）——全セル。内訳: {string.Join(" ／ ", Causes.Select((n, i) => $"{n} {by[i]}"))}");
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    /// <summary>表H: `compare` 61 行 × 5 波 × 版（札は各行のボルグ、いなければ最初の駒）。燃焼が付く行 ＝ H0 ＋ 計数の札で燃焼の刻みか機会が1度でもあった行。</summary>
    static void TableH()
    {
        var rows = CompareBuilds();
        var vs = Versions.Append(Probe).ToArray();
        var win = new double[vs.Length, rows.Length, 5];
        var burn = new bool[rows.Length];
        for (int v = 0; v < vs.Length; v++)
            for (int i = 0; i < rows.Length; i++)
            {
                var f = Mark(rows[i].F, vs[v].Card);
                for (int st = 0; st < 5; st++)
                {
                    var stage = EnemyCatalog.Stages[st].Enemy;
                    var res = new BattleResult[BA.Seeds];
                    Parallel.For(0, BA.Seeds, seed => res[seed] = BattleEngine.Run(f, stage, seed, verbose: false, ember: EmberRule.Pre256));
                    win[v, i, st] = 100.0 * res.Count(x => x.PlayerWon) / BA.Seeds;
                    if (vs[v] == Probe && res.Any(x => x.BurnHit is { } bh && (bh.TickFires.Sum() > 0 || bh.Chances.Sum() > 0))) burn[i] = true;
                }
            }
        Console.WriteLine("## 表H —— `compare` 61 行 × 5 波の変化（H0 ＝ 規定との差・seed 0..199）");
        Console.WriteLine();
        Console.WriteLine($"燃焼が付く行（H0 ＋ 計数の札で、燃焼の刻みか被弾の燃焼の機会が1度でもあった行）: {burn.Count(x => x)} / {rows.Length} 行。");
        Console.WriteLine();
        Console.WriteLine("| 版 | 動いたセル（燃焼が付く行 ／ 付かない行） | 動いた行 | 全61行 第1〜5波の平均 | 燃焼が付く行の 第2〜5波の平均（H0 → 版） |");
        Console.WriteLine("|---|---|--:|---|---|");
        for (int v = 0; v < vs.Length; v++)
        {
            int cb = 0, cn = 0, rowsMoved = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                bool any = false;
                for (int st = 0; st < 5; st++)
                    if (Math.Abs(win[v, i, st] - win[0, i, st]) > 1e-9) { any = true; if (burn[i]) cb++; else cn++; }
                if (any) rowsMoved++;
            }
            string avg = string.Join(" / ", Enumerable.Range(0, 5).Select(st => Enumerable.Range(0, rows.Length).Average(i => win[v, i, st]).ToString("F1")));
            var br = Enumerable.Range(0, rows.Length).Where(i => burn[i]).ToArray();
            double b0 = br.Length == 0 ? double.NaN : br.Average(i => Enumerable.Range(1, 4).Average(st => win[0, i, st]));
            double b1 = br.Length == 0 ? double.NaN : br.Average(i => Enumerable.Range(1, 4).Average(st => win[v, i, st]));
            Console.WriteLine($"| {vs[v].Name} | {cb} ／ {cn} | {rowsMoved} | {avg} | {F1(b0)} → {F1(b1)} |");
        }
        Console.WriteLine();
        Console.WriteLine("### 動いた行（H-分担 の第2〜5波の差・大きい順）");
        Console.WriteLine();
        Console.WriteLine("| 行 | 燃焼 | H0 第1〜5波 | H-足す | H-分担 | H-分担1 | H-敵だけ |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        string cells(int v, int i) => string.Join(" / ", Enumerable.Range(0, 5).Select(st => win[v, i, st].ToString("F1")));
        foreach (int i in Enumerable.Range(0, rows.Length)
                     .Where(i => Enumerable.Range(1, 4).Any(v => Enumerable.Range(0, 5).Any(st => Math.Abs(win[v, i, st] - win[0, i, st]) > 1e-9)))
                     .OrderByDescending(i => Math.Abs(Enumerable.Range(1, 4).Sum(st => win[2, i, st] - win[0, i, st]))))
            Console.WriteLine($"| {rows[i].Name} | {(burn[i] ? "○" : "—")} | {cells(0, i)} | {cells(1, i)} | {cells(2, i)} | {cells(3, i)} | {cells(4, i)} |");
        Console.WriteLine();
    }
}
