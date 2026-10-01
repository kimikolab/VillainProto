using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;
using FF = FireFinishDiag;

// fireatk run —— 段2 ＋ 表A〜G（指示書 §6.3）。phase0 は同じ表を L0 だけで出す（予測の材料・Q0-4）。
static partial class FireAtkDiag
{
    /// <summary>台。T3(L0選) ／ T3(L3選) は段1 の1位の席（どちらの席でも全版を並べる）。</summary>
    static readonly string[] FixedBoards = { "T3-244", "T3-238", "雷＋ボルグ" };
    static string[] BoardNames = FixedBoards;
    static Formation BoardOf(string name, FB.Ver v, Dictionary<string, List<FB.Pick>>? ranked) => name switch
    {
        "T3-244" => Apply(FC.T3244, v),
        "T3-238" => Apply(FC.T3238, v),
        "雷＋ボルグ" => Apply(FC.ThunderBorg, v),
        "T3(L0選)" => Apply(FB.Dec(ranked!["L0"][0].Best), v),
        "T3(L3選)" => Apply(FB.Dec(ranked!["L3"][0].Best), v),
        _ => throw new ArgumentException(name),
    };
    static readonly (string Name, Func<Formation> F)[] Refs = { ("参考 移動", () => BA.RefMove), ("参考 雷", () => BA.RefThunder) };
    const string RefVer = "—";
    static readonly int[] MainWaves = { 0, 1, 2, 3 };
    const int WaveHush = FC.WaveHush, WaveYoke = FC.WaveYoke, WaveHeavy = FC.WaveHeavy, WaveT1 = FC.WaveTarget1, WaveT9 = FC.WaveTarget9;

    static Dictionary<(string B, string V, int W, int S), Cell> _cells = new();
    static FB.Ver[] _vers = Versions;
    static Cell At(string b, string v, int w, int s) => _cells[(b, v, w, IsTarget(w) ? 0 : s)];

    static void Put(string b, string vn, Formation f)
    {
        for (int w = 0; w < WaveNames.Length; w++)
            for (int s = 0; s < (IsTarget(w) ? 1 : BA.Scales.Length); s++)
                _cells[(b, vn, w, s)] = Measure(f, w, IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc);
    }

    static partial void Phase0()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _vers = new[] { VerOf("L0") };
        BoardNames = FixedBoards;
        _cells = new();
        foreach (string bn in BoardNames) Put(bn, "L0", BoardOf(bn, _vers[0], null));
        foreach (var (rn, rf) in Refs) Put(rn, RefVer, rf());
        Console.WriteLine("# 第250期 Phase 0 —— Q0-4（L0 ＝ 前段の規定で台の駒を数える・予測の材料）");
        Console.WriteLine();
        foreach (string bn in BoardNames) Console.WriteLine($"- {bn}: {BA.SeatsNamed(BoardOf(bn, _vers[0], null))}");
        foreach (var (rn, rf) in Refs) Console.WriteLine($"- {rn}: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
        WouldBe();
        TableA(); TableB(); TableC(); TableD(); TableE(); TableF();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    /// <summary>
    /// Phase 0: L0 の帳簿で新しい札が働く「機会」を数える（札は足していない・engine の計数は札が無くても数える）——
    /// あぶれた火の機会 ＝ 火勢4 のホタが育ちを受けた（起こし手ごと）、くべられる火の機会 ＝ 燃えていたホタに味方が火を点けた（書き手ごと）、
    /// 爆炎・独りの機会 ＝ ヒヨが倒れた後のターン頭の写しでボルグが火勢4。
    /// </summary>
    static void WouldBe()
    {
        Console.WriteLine("## Q0-1〜Q0-3 —— L0 の帳簿で数えた機会（札は足していない）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | ホタの手番/戦 | あぶれた火の機会/戦（×4 ＝ 上乗せ/戦）・起こし手 | くべられる火の機会/戦（×2 ＝ 上乗せ/戦）・書き手 | ヒヨが落ちた % | ヒヨが落ちた後、ターン頭でボルグが火勢4 /戦 |");
        Console.WriteLine("|---|---|--:|---|---|--:|--:|");
        foreach (string b in BoardNames)
            foreach (var (w, s) in KeyCells)
            {
                long n = 0, hands = 0, hiyoFell = 0, solo = 0;
                var overBy = new Dictionary<string, long>(); var fedBy = new Dictionary<string, long>();
                var f = BoardOf(b, _vers[0], null);
                int cut = IsTarget(w) ? TM : int.MaxValue;
                for (int seed = 0; seed < BA.Seeds; seed++)
                {
                    var (r, p, _) = FC.Fight(f, w, IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc, seed);
                    n++;
                    int? hota = p.FirstOrDefault(u => u.Def.Id == "hota")?.InstanceId, borg = p.FirstOrDefault(u => u.Def.Id == "borg")?.InstanceId, hiyo = p.FirstOrDefault(u => u.Def.Id == "hiyo")?.InstanceId;
                    hands += r.Hands.Count(h => h.ActorId == hota && h.Turn <= cut);
                    if (hiyo is int && r.PlayerStarterFallen.Contains("hiyo")) hiyoFell++;
                    if (r.FireLevels is not FireLevelLedger fl) continue;
                    foreach (var (k, v) in fl.OverflowChanceBy) overBy[k] = overBy.GetValueOrDefault(k) + v;
                    foreach (var (k, v) in fl.FedChanceBy) fedBy[k] = fedBy.GetValueOrDefault(k) + v;
                    int hiyoDeathTurn = hiyo is null ? 0 : int.MaxValue;
                    foreach (var x in r.Events) if (x.Kind == BattleEventKind.Death && x.TargetId == hiyo) { hiyoDeathTurn = x.Turn; break; }
                    foreach (var sn in fl.Snaps)
                        if (sn.Id == borg && sn.Turn > hiyoDeathTurn && sn.Turn <= cut && sn.Level == 4) solo++;
                }
                long over = overBy.Values.Sum(), fed = fedBy.Values.Sum();
                string Top(Dictionary<string, long> d, long all) => all == 0 ? "—" : string.Join(" ", d.OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Key} {Pct(kv.Value, all)}%"));
                Console.WriteLine($"| {b} | {WN(w, s)} | {Per(hands, n)} | {Per(over, n)}（{Per(over * FireFeedRule.OverflowAtk, n)}）{Top(overBy, over)} | {Per(fed, n)}（{Per(fed * FireFeedRule.FedAtk, n)}）{Top(fedBy, fed)} | {(IsTarget(w) ? "—" : Pct(hiyoFell, n))} | {Per(solo, n)} |");
            }
        Console.WriteLine();
        Console.WriteLine("的の波（8T）は帳簿が戦全体（8 ターンで打ち切っていない）なので、機会/戦は参考。");
        Console.WriteLine();
    }

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ranked = new Dictionary<string, List<FB.Pick>>();
        foreach (string vn in PickVersions) ranked[vn] = FB.Ranked(PicksOf(VerOf(vn), out _));
        Console.WriteLine("# 第250期 段2 —— ホタの攻撃力の育ち・焼き尽くすの重さ・ヒヨのいない爆炎（版 L0〜L3）");
        Console.WriteLine();
        Stage1Summary(ranked);
        _vers = Versions;
        BoardNames = FixedBoards.Concat(new[] { "T3(L0選)", "T3(L3選)" }).ToArray();
        _cells = new();
        foreach (string bn in BoardNames) foreach (var v in Versions) Put(bn, v.Name, BoardOf(bn, v, ranked));
        foreach (var (rn, rf) in Refs) Put(rn, RefVer, rf());
        Console.Error.WriteLine($"  段2 の測定 {sw.Elapsed.TotalSeconds:F0} 秒");
        Console.WriteLine("## 台（席は版に依らない・版はボルグ・ホタの札だけを差し替える）");
        Console.WriteLine();
        foreach (string bn in BoardNames) Console.WriteLine($"- {bn}: {BA.SeatsNamed(BoardOf(bn, Versions[^1], ranked))}");
        foreach (var (rn, rf) in Refs) Console.WriteLine($"- {rn}（版に依らない）: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
        foreach (var v in Versions) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();
        TableA(); TableA2(); TableB(); TableC(); TableD(); TableE(); TableF(); TableG(); TableDeath();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒・seed 0..{BA.Seeds - 1}・verbose）");
    }

    static string Sc(FB.Score4 s) => $"{s.S.Sv}/40・400/300 {s.Sv4}/40・落 {s.S.Fell}・勝 {s.S.W}";
    static void Stage1Summary(Dictionary<string, List<FB.Pick>> ranked)
    {
        Console.WriteLine("## 段1（T3 1,081 組 × 席 120 × seed 1000..1039・九/新兵 × 200/200 → 400/300 → 落 → 決着T）");
        Console.WriteLine();
        Console.WriteLine("| 版 | 1位（相方） | 席 | 得点 | 2位 | 3位 | 上位10 の相方（のべ） | 200/200 で 40/40 ／ 400/300 も 40/40 の組 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        foreach (string vn in PickVersions)
        {
            var rk = ranked[vn];
            var b = rk[0];
            int full = rk.Count(p => p.BestS.S.Sv == BA.PickSeeds);
            int full4 = rk.Count(p => p.BestS.S.Sv == BA.PickSeeds && p.BestS.Sv4 == BA.PickSeeds);
            string top10 = string.Join(" ", rk.Take(10).SelectMany(p => p.Partners).GroupBy(x => x).OrderByDescending(g => g.Count()).Take(6).Select(g => $"{g.Key} {g.Count()}"));
            Console.WriteLine($"| {vn} | {string.Join("・", b.Partners)} | {BA.SeatsNamed(FB.Dec(b.Best))} | {Sc(b.BestS)} | {string.Join("・", rk[1].Partners)} {Sc(rk[1].BestS)} | {string.Join("・", rk[2].Partners)} {Sc(rk[2].BestS)} | {top10} | {full} ／ {full4} |");
        }
        Console.WriteLine();
    }

    static string F1(double x) => double.IsNaN(x) ? "—" : x.ToString("F1");
    static double MainAvg(string b, string v, int s, Func<FC.CAgg, double> f) => MainWaves.Average(w => f(At(b, v, w, s).C));
    static double WinP(FC.CAgg c) => c.N == 0 ? double.NaN : 100.0 * c.Wins / c.N;
    static double SurvP(FC.CAgg c) => c.N == 0 ? double.NaN : 100.0 * c.AllSurv / c.N;
    static IEnumerable<(string B, string V)> Rows()
    {
        foreach (string b in BoardNames) foreach (var v in _vers) yield return (b, v.Name);
    }

    static readonly (int W, int S)[] KeyCells =
    {
        (BA.MainWave, 1), (BA.MainWave, 0), (BA.MainWave + 1, 1), (3, 1), (WaveHush, 1), (WaveYoke, 1), (WaveHeavy, 1), (WaveT1, 0), (WaveT9, 0),
    };
    static string WN(int w, int s) => IsTarget(w) ? $"{WaveNames[w]}（8T）" : $"{WaveNames[w]} {BA.Scales[s].Name}";

    static void TableA()
    {
        Console.WriteLine("## 表A —— 台 × 版（勝率 ／ 全員生存 ／ 決着T ／ 落ちた駒）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 本編 400/300 勝率 ／ 全員生存（第2〜5波の平均） | 本編 200/200 勝率 ／ 全員生存 | 本編 115/115 全員生存 | 九/新兵 400/300 全員生存 ／ 決着T | 九/新兵 200/200 全員生存 | 九/農兵 400/300 全員生存 | 第五波 400/300 勝率 ／ 全員生存 | 重い波 400/300 全員生存 ／ 決着T | 落ちた/戦（本編 400/300）ボルグ ／ ホタ ／ ヒヨ ／ 相方 |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|--:|---|---|---|");
        foreach (var (b, v) in Rows().Concat(Refs.Select(x => (x.Name, RefVer))))
        {
            var n9 = At(b, v, BA.MainWave, 1).C; var h = At(b, v, WaveHeavy, 1).C; var w5 = At(b, v, 3, 1).C;
            long nn = MainWaves.Sum(w => At(b, v, w, 1).C.N);
            string fell = v == RefVer ? "—" : string.Join(" ／ ", Enumerable.Range(0, 4).Select(r => Per(MainWaves.Sum(w => At(b, v, w, 1).C.FellRole[r]), nn)));
            Console.WriteLine($"| {b} | {v} | {F1(MainAvg(b, v, 1, WinP))} ／ {F1(MainAvg(b, v, 1, SurvP))} | {F1(MainAvg(b, v, 0, WinP))} ／ {F1(MainAvg(b, v, 0, SurvP))} | {F1(MainAvg(b, v, 2, SurvP))} | {F1(SurvP(n9))} ／ {Per(n9.Turns, n9.N)} | {F1(SurvP(At(b, v, BA.MainWave, 0).C))} | {F1(SurvP(At(b, v, BA.MainWave + 1, 1).C))} | {F1(WinP(w5))} ／ {F1(SurvP(w5))} | {F1(SurvP(h))} ／ {Per(h.Turns, h.N)} | {fell} |");
        }
        Console.WriteLine();
    }

    static void TableA2()
    {
        Console.WriteLine("### 表A′ —— 全セル（勝率 ／ 全員生存 ／ 決着T ／ 落ちた/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | " + string.Join(" | ", WaveNames.Take(WaveHeavy + 1)) + " |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", WaveHeavy + 1)));
        foreach (var (b, v) in Rows())
            for (int s = 0; s < BA.Scales.Length; s++)
                Console.WriteLine($"| {b} | {v} | {BA.Scales[s].Name} | " + string.Join(" | ", Enumerable.Range(0, WaveHeavy + 1).Select(w => { var c = At(b, v, w, s).C; return $"{F1(WinP(c))} ／ {F1(SurvP(c))} ／ {Per(c.Turns, c.N)} ／ {Per(c.Fell, c.N)}"; })) + " |");
        Console.WriteLine();
        Console.WriteLine("### 表A″ —— 粛の波（第二波）・軛の波（第四波）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 粛 400/300 勝率 ／ 全員生存 ／ 決着T | 粛 200/200 | 軛 400/300 勝率 ／ 全員生存 ／ 決着T | 軛 200/200 |");
        Console.WriteLine("|---|---|---|---|---|---|");
        string Cl(FC.CAgg c) => $"{F1(WinP(c))} ／ {F1(SurvP(c))} ／ {Per(c.Turns, c.N)}";
        foreach (var (b, v) in Rows())
            Console.WriteLine($"| {b} | {v} | {Cl(At(b, v, WaveHush, 1).C)} | {Cl(At(b, v, WaveHush, 0).C)} | {Cl(At(b, v, WaveYoke, 1).C)} | {Cl(At(b, v, WaveYoke, 0).C)} |");
        Console.WriteLine();
    }

    static void TableB()
    {
        Console.WriteLine("## 表B —— ホタの攻撃力の上がり方（贔屓 ／ あぶれた火 ／ くべられる火）");
        Console.WriteLine();
        Console.WriteLine("量は1戦あたりの上乗せの和（括弧は回）。「手番」は上がった瞬間の手番の持ち主（いちばん内側の手番の枠・ギフトの手番は受けた駒の手番）。攻撃力の推移はターン頭の写し（画面の攻撃力・段の倍率込み）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | " + string.Join(" | ", SrcNames.Select(s => s + "/戦（回）")) + " | 合計/戦 ／ 1戦の最大 | 手番別（ボルグ ／ ホタ ／ ヒヨ ／ ほか ／ 外）の量/戦 | 攻撃力 1戦の最大 平均 ／ 最大 ／ 100 以上 % | T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8+ |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", SrcNames.Length)) + "---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string b in BoardNames)
            foreach (var (w, s) in KeyCells)
                foreach (var v in _vers)
                {
                    var c = At(b, v.Name, w, s); var a = c.A; var k = c.K;
                    if (a.HotaN == 0) continue;
                    string src = string.Join(" | ", Enumerable.Range(0, 3).Select(i => $"{Per(Enumerable.Range(0, 5).Sum(o => a.SrcAmt[i, o]), a.HotaN)}（{Per(Enumerable.Range(0, 5).Sum(o => a.SrcCnt[i, o]), a.HotaN)}）"));
                    string own = string.Join(" ／ ", Enumerable.Range(0, 5).Select(o => Per(Enumerable.Range(0, 3).Sum(i => a.SrcAmt[i, o]), a.HotaN)));
                    string tr = string.Join(" | ", Enumerable.Range(1, TM).Select(t => k.AtkCnt[t] == 0 ? "—" : ((double)k.AtkSum[t] / k.AtkCnt[t]).ToString("F0")));
                    Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | {src} | {Per(a.GainMaxSum, a.HotaN)} ／ {a.GainMaxMax} | {own} | {Per(k.AtkMaxSum, k.HotaN)} ／ {k.AtkMaxMax} ／ {Pct(k.Atk100, k.HotaN)} | {tr} |");
                }
        Console.WriteLine();
        Console.WriteLine("### 表B′ —— 出どころ × 手番の持ち主（本編 第2〜5波 × 400/300 と 九/新兵 × 400/300・量/戦）／ あぶれた火・くべられる火の起こし手");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | " + string.Join(" | ", SrcNames.Select(sn => sn + "（ボルグ ／ ホタ ／ ヒヨ ／ ほか ／ 外）")) + " | あぶれた火の起こし手（回の割合） | くべられる火の書き手（回の割合） | ターンごとの上乗せ（3種の和/戦）T1〜T8 |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", SrcNames.Length)) + "---|---|---|");
        foreach (string b in BoardNames)
            foreach (var v in _vers)
                foreach (var (lab, cells) in new[] { ("本編", MainWaves.Select(w => (w, 1)).ToArray()), ("九/新兵", new[] { (BA.MainWave, 1) }), ("的・一", new[] { (WaveT1, 0) }) })
                {
                    var acc = new AAgg();
                    foreach (var (w, s) in cells) acc.Merge(At(b, v.Name, w, s).A);
                    if (acc.HotaN == 0) continue;
                    string Src(int i) => string.Join(" ／ ", Enumerable.Range(0, 5).Select(o => Per(acc.SrcAmt[i, o], acc.HotaN)));
                    string By(Dictionary<string, long> d) { long all = d.Values.Sum(); return all == 0 ? "—" : string.Join(" ", d.OrderByDescending(kv => kv.Value).Take(4).Select(kv => $"{kv.Key} {Pct(kv.Value, all)}%")); }
                    string turns = string.Join(" ", Enumerable.Range(1, TM).Select(t => Per(acc.SrcTurn[0, t] + acc.SrcTurn[1, t] + acc.SrcTurn[2, t], acc.HotaN)));
                    Console.WriteLine($"| {b} | {v.Name} | {lab} | {Src(0)} | {Src(1)} | {Src(2)} | {By(acc.OverflowBy)} | {By(acc.FedBy)} | {turns} |");
                }
        Console.WriteLine();
    }

    static void TableC()
    {
        Console.WriteLine("## 表C —— 1発の最大・1ターンの総量（ホタ ／ ボルグ ／ 参考 ヨミ・セロ）");
        Console.WriteLine();
        Console.WriteLine("1発の最大は「1戦の最大の平均 ／ 全戦の最大」。1ターンの総量はそのターンまで戦が続いた戦の平均（的の波は 8 ターン全部）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | 駒 | 1発の最大 平均 ／ 最大 | 1戦の与ダメ | T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8+ |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        IEnumerable<(string B, string V)> rows = Rows().Concat(Refs.Select(x => (x.Name, RefVer)));
        foreach (var (w, s) in new[] { (BA.MainWave, 1), (3, 1), (WaveYoke, 1), (WaveHeavy, 1), (WaveT1, 0), (WaveT9, 0) })
            foreach (var (b, v) in rows)
            {
                if (!_cells.ContainsKey((b, v, w, IsTarget(w) ? 0 : s))) continue;
                var k = At(b, v, w, s).K;
                foreach (string id in b.StartsWith("参考") ? new[] { "yomi", "sero" } : new[] { "hota", "borg" })
                {
                    if (!k.Hits.TryGetValue(id, out var h) || h.N == 0) continue;
                    string tr = string.Join(" | ", Enumerable.Range(1, TM).Select(t => h.TurnN[t] == 0 ? "—" : ((double)h.TurnSum[t] / h.TurnN[t]).ToString("F0")));
                    Console.WriteLine($"| {b} | {WN(w, s)} | {v} | {id} | {Per(h.MaxSum, h.N)} ／ {h.MaxMax} | {Per(h.Total, h.N)} | {tr} |");
                }
            }
        Console.WriteLine();
    }

    static void TableD()
    {
        Console.WriteLine("## 表D —— 画面に出る攻撃力と実際の1発（ホタの手番の種類ごと）");
        Console.WriteLine();
        Console.WriteLine("「画面」＝ その手番の前の最後のターン頭の写し（`StatSnapshot`・画面の攻撃力そのもの）。「振った」＝ その手番の最初の `Attack` の量（段・大技の倍率込み）。「1発の最大」＝ その手番で敵に当てた `Damage` の最大（脆さ・軛ほか込み）。比 ＝ 1発の最大 ÷ 画面。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | 手番の種類 | 手番/戦 | 画面 | 振った | 1発の最大 平均 ／ 最大 | 比（1発 ÷ 画面） | 手番の与ダメ | 当てた/手番 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|---|--:|--:|--:|");
        foreach (string b in BoardNames)
            foreach (var (w, s) in new[] { (BA.MainWave, 1), (3, 1), (WaveYoke, 1), (WaveT1, 0) })
                foreach (var v in _vers)
                {
                    var a = At(b, v.Name, w, s).A;
                    for (int kd = 0; kd < HandKinds.Length; kd++)
                    {
                        if (a.HandN[kd] == 0) continue;
                        double ratio = a.HandShown[kd] == 0 ? double.NaN : (double)a.HandHitMax[kd] / a.HandShown[kd];
                        Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | {HandKinds[kd]} | {Per(a.HandN[kd], a.N)} | {Per(a.HandShown[kd], a.HandN[kd])} | {Per(a.HandSwing[kd], a.HandN[kd])} | {Per(a.HandHitMax[kd], a.HandN[kd])} ／ {a.HandHitMaxMax[kd]} | {(double.IsNaN(ratio) ? "—" : ratio.ToString("F2"))} | {Per(a.HandTotal[kd], a.HandN[kd])} | {Per(a.HandHits[kd], a.HandN[kd])} |");
                    }
                }
        Console.WriteLine();
    }

    static void TableE()
    {
        Console.WriteLine("## 表E —— 焼き尽くす（全体の1発 ×4 ／ ×7・火の雨）");
        Console.WriteLine();
        Console.WriteLine("「全体」は焼き尽くすの最初の全体攻撃（振った攻撃力 ／ 1体あたり ／ 1発の最大）、「火の雨」は続く 10 発の合計。倒した/回のうち全体で倒した分。ギフトの直後に倒れた敵/戦（第248期の表G と同じ数え方）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | 焼き尽くす/戦 | 全体 振った ／ 1体あたり ／ 最大 | 全体の与ダメ/回 | 火の雨の与ダメ/回 | 1回の与ダメ 平均 ／ 最大 | 倒した/回（うち全体） | ギフトの直後に倒れた敵/戦 |");
        Console.WriteLine("|---|---|---|--:|---|--:|--:|---|---|--:|");
        foreach (string b in BoardNames)
            foreach (var (w, s) in KeyCells)
                foreach (var v in _vers)
                {
                    var c = At(b, v.Name, w, s); var a = c.A;
                    if (a.Burnouts == 0) { Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | 0.00 | — | — | — | — | — | {Per(c.C.GiftKills, c.C.N)} |"); continue; }
                    Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | {Per(a.Burnouts, a.N)} | {Per(a.BlastSwing, a.Burnouts)} ／ {Per(a.BlastDmg, a.BlastHits)} ／ {a.BlastHitMax} | {Per(a.BlastDmg, a.Burnouts)} | {Per(a.RainDmg, a.Burnouts)} | {Per(a.BlastDmg + a.RainDmg, a.Burnouts)} ／ {a.BurnoutDmgMax} | {Per(a.BurnoutKills, a.Burnouts)}（{Per(a.BurnoutBlastKills, a.Burnouts)}） | {Per(c.C.GiftKills, c.C.N)} |");
                }
        Console.WriteLine();
    }

    static readonly string[] BlazeKinds = { "ホタの火の癒し", "ヒヨの火の変換", "ベニの反転", "受けた", "焼かれない" };

    static void TableF()
    {
        Console.WriteLine("## 表F —— 爆炎（ギフトの爆炎 ／ 独りの爆炎）: 1戦の回数・敵への与ダメ・味方への燃焼ダメージの行き先");
        Console.WriteLine();
        Console.WriteLine("「全体」は第249期の帳簿（ギフト ＋ 独り）、「独り」はそのうち爆炎・独りの分。行き先は名目/戦（括弧は実際に癒えた ／ 削られた HP/戦）: " + string.Join(" ／ ", BlazeKinds) + "。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | ヒヨが落ちた % | 爆炎/戦 全体 ／ 独り | 独りがあった戦 % | 独りの敵への与ダメ/戦（倒した/戦） | 独り: " + string.Join(" | 独り: ", BlazeKinds) + " | 独りで味方が倒れた/戦 | 全体: 受けた（削られた）/戦 |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|" + string.Concat(Enumerable.Repeat("---|", BlazeKinds.Length)) + "--:|---|");
        foreach (string b in BoardNames)
            foreach (var (w, s) in KeyCells)
                foreach (var v in _vers)
                {
                    var c = At(b, v.Name, w, s); var a = c.A; var k = c.K;
                    Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | {(IsTarget(w) ? "—" : Pct(a.HiyoFell, a.N))} | {Per(k.Blazes, k.N)} ／ {Per(a.Solos, a.N)} | {Pct(a.SoloBattles, a.N)} | {Per(a.SoloFoeDealt, a.N)}（{Per(a.SoloFoeKills, a.N)}） | "
                        + string.Join(" | ", Enumerable.Range(0, 5).Select(i => $"{Per(a.SoloNom[i], a.N)}（{Per(a.SoloHp[i], a.N)}）")) + $" | {Per(a.SoloAllyKills, a.N)} | {Per(k.BlazeNom[3], k.N)}（{Per(k.BlazeHp[3], k.N)}） |");
                }
        Console.WriteLine();
        Console.WriteLine("### 表F′ —— 独りの爆炎・駒ごと（全波・全倍率の合計: 名目 ／ 癒えた ／ 削られた /戦・倒れた回）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 駒ごと |");
        Console.WriteLine("|---|---|---|");
        foreach (string b in BoardNames)
            foreach (var v in _vers.Where(v => v.Borg.Traits.Contains(TraitId.BlazeSolo)))
            {
                var acc = new Dictionary<string, long[]>(); long n = 0;
                foreach (var ((bb, vv, _, _), c) in _cells)
                {
                    if (bb != b || vv != v.Name) continue;
                    n += c.A.N;
                    foreach (var (id, x) in c.A.SoloById) { if (!acc.TryGetValue(id, out var y)) acc[id] = y = new long[4]; for (int i = 0; i < 4; i++) y[i] += x[i]; }
                }
                Console.WriteLine($"| {b} | {v.Name} | " + (acc.Count == 0 ? "—" : string.Join(" ／ ", acc.OrderByDescending(kv => kv.Value[0]).Select(kv => $"{kv.Key} {Per(kv.Value[0], n)}・+{Per(kv.Value[1], n)}・−{Per(kv.Value[2], n)}・倒{kv.Value[3]}"))) + " |");
            }
        Console.WriteLine();
    }

    static void TableG()
    {
        Console.WriteLine("## 表G —— 手番の絵の単調さ（第239期の定義・九/新兵 × 400/300 と本編 第五波 × 400/300）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 駒 | 手番/戦 | 絵の種類/戦 | 最多の絵 % | 続けて同じ絵 % | 骨格の最多 % | 上位3 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|---|");
        foreach (string b in BoardNames)
            foreach (var v in _vers.Where(v => v.Name is "L0" or "L1" or "L2" or "L3"))
                foreach (int w in new[] { BA.MainWave, 3 })
                {
                    var a = At(b, v.Name, w, 1).L;
                    foreach (string id in new[] { "borg", "hota", "hiyo" })
                    {
                        if (!a.Pics.TryGetValue(id, out var pa) || pa.Hands == 0) continue;
                        var top = pa.Freq.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key} {Pct(kv.Value, pa.Hands)}");
                        Console.WriteLine($"| {b} | {v.Name} | {WaveNames[w]} | {id} | {Per(pa.Hands, pa.Battles)} | {Per(pa.DistinctSum, pa.Battles)} | {Pct(pa.TopSum, pa.Hands)} | {Pct(pa.Repeats, pa.Pairs)} | {Pct(pa.BoneTopSum, pa.Hands)} | {string.Join(" ／ ", top)} |");
                    }
                }
        Console.WriteLine();
    }

    static void TableDeath()
    {
        long cs = 0, de = 0, fs = 0, fr = 0;
        foreach (var c in _cells.Values) { cs += c.K.CauseSum; de += c.K.DeathEv; fs += c.K.FellSum; fr += c.K.FellRes; }
        Console.WriteLine($"受け入れ 4: 死因の合計 {cs} ＝ 倒れた出来事 {de}（{(cs == de ? "一致" : "**不一致**")}）／ 落ちた駒（死因の帳簿）{fs} ＝ 落ちた駒（結果）{fr}（{(fs == fr ? "一致" : "**不一致**")}）——全セル");
        Console.WriteLine();
    }
}
