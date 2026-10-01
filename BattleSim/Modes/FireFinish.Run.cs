using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// firefinish run —— 段2 ＋ 表A〜H（指示書 §6.3）。phase0 は同じ表を K0 だけで出す（予測の材料・Q0-6）。
static partial class FireFinishDiag
{
    /// <summary>台。T3(K0選) ／ T3(K4選) は段1 の1位の席（どちらの席でも全版を並べる）。</summary>
    static readonly string[] FixedBoards = { "T3-244", "T3-238", "雷＋ボルグ" };
    static string[] BoardNames = FixedBoards;
    static Formation BoardOf(string name, FB.Ver v, Dictionary<string, List<FB.Pick>>? ranked) => name switch
    {
        "T3-244" => Apply(FC.T3244, v),
        "T3-238" => Apply(FC.T3238, v),
        "雷＋ボルグ" => Apply(FC.ThunderBorg, v),
        "T3(K0選)" => Apply(FB.Dec(ranked!["K0"][0].Best), v),
        "T3(K4選)" => Apply(FB.Dec(ranked!["K4"][0].Best), v),
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
        _vers = new[] { VerOf("K0") };
        BoardNames = FixedBoards;
        _cells = new();
        foreach (string bn in BoardNames) Put(bn, "K0", BoardOf(bn, _vers[0], null));
        foreach (var (rn, rf) in Refs) Put(rn, RefVer, rf());
        Console.WriteLine("# 第249期 Phase 0 —— Q0-6（K0 ＝ 規定で台の駒を数える・予測の材料）");
        Console.WriteLine();
        foreach (string bn in BoardNames) Console.WriteLine($"- {bn}: {BA.SeatsNamed(BoardOf(bn, _vers[0], null))}");
        foreach (var (rn, rf) in Refs) Console.WriteLine($"- {rn}: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
        TableA(); TableB(); TableC(); BlazeWouldBe(); TableF(); TableG();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    /// <summary>Phase 0: K0 の放つの瞬間に、爆炎なら味方がどこへ行くか（ヒヨが生きていれば燃えている味方は火の変換・ベニの結界・ホタ）。台本から読む。</summary>
    static void BlazeWouldBe()
    {
        Console.WriteLine("## Q0-6 —— K0 の「放つ」の瞬間の味方（爆炎にしたら燃焼ダメージがどこへ行くかの見込み）");
        Console.WriteLine();
        Console.WriteLine("爆炎は味方に着火してから燃焼ダメージを配るので、**ヒヨが生きていれば、ホタ以外の味方は全員が火の変換で回復**する（第238期 V1 は燃えている味方の燃焼ダメージ全部）。ホタは火の癒し（K1〜）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 放つ/戦 | 放つの瞬間 ヒヨが生きている % | 味方（ボルグ以外）の数/回 | ボルグの攻撃力（放つの瞬間・平均） |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (string b in BoardNames)
            foreach (var (w, s) in KeyCells)
            {
                long un = 0, hiyoAlive = 0, allies = 0, atk = 0; long n = 0;
                var f = BoardOf(b, _vers[0], null);
                for (int seed = 0; seed < BA.Seeds; seed++)
                {
                    var (r, p, _) = FC.Fight(f, w, IsTarget(w) ? EnemyScaleRule.None : BA.Scales[s].Sc, seed);
                    n++;
                    int? borg = p.FirstOrDefault(u => u.Def.Id == "borg")?.InstanceId, hiyo = p.FirstOrDefault(u => u.Def.Id == "hiyo")?.InstanceId;
                    var dead = new HashSet<int>(); var snapAtk = new Dictionary<int, int>();
                    foreach (var x in r.Events)
                    {
                        if (IsTarget(w) && x.Turn > TM) break;
                        if (x.Kind == BattleEventKind.Death && x.TargetId is int d) dead.Add(d);
                        if (x.Kind == BattleEventKind.StatSnapshot && x.TargetId is int st) snapAtk[st] = x.Amount;
                        if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Unleash && x.ActorId == borg)
                        {
                            un++;
                            if (hiyo is int h && !dead.Contains(h)) hiyoAlive++;
                            allies += p.Count(u => u.InstanceId != borg && !dead.Contains(u.InstanceId));
                            atk += borg is int bi ? snapAtk.GetValueOrDefault(bi) : 0;
                        }
                    }
                }
                Console.WriteLine($"| {b} | {WN(w, s)} | {Per(un, n)} | {Pct(hiyoAlive, un)} | {Per(allies, un)} | {Per(atk, un)} |");
            }
        Console.WriteLine();
    }

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ranked = new Dictionary<string, List<FB.Pick>>();
        foreach (string vn in PickVersions) ranked[vn] = FB.Ranked(PicksOf(VerOf(vn), out _));
        Console.WriteLine("# 第249期 段2 —— 燃焼の軸の仕上げ（版 K0〜K4t）");
        Console.WriteLine();
        Stage1Summary(ranked);
        _vers = Versions;
        BoardNames = FixedBoards.Concat(new[] { "T3(K0選)", "T3(K4選)" }).ToArray();
        _cells = new();
        foreach (string bn in BoardNames) foreach (var v in Versions) Put(bn, v.Name, BoardOf(bn, v, ranked));
        foreach (var (rn, rf) in Refs) Put(rn, RefVer, rf());
        Console.Error.WriteLine($"  段2 の測定 {sw.Elapsed.TotalSeconds:F0} 秒");
        Console.WriteLine("## 台（席は版に依らない・版はボルグ・ホタ・ヒヨの札とホタの元の攻撃力だけを差し替える）");
        Console.WriteLine();
        foreach (string bn in BoardNames) Console.WriteLine($"- {bn}: {BA.SeatsNamed(BoardOf(bn, Versions[^1], ranked))}");
        foreach (var (rn, rf) in Refs) Console.WriteLine($"- {rn}（版に依らない）: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
        foreach (var v in Versions) Console.WriteLine($"- {v.Name}: {v.What}");
        Console.WriteLine();
        TableA(); TableA2(); TableB(); TableC(); TableD(); TableE(); TableF(); TableG(); TableH(); TableDeath();
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
    static string F2(double x) => double.IsNaN(x) ? "—" : x.ToString("F2");
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
        Console.WriteLine("| 台 | 版 | 本編 400/300 勝率 ／ 全員生存（第2〜5波の平均） | 本編 200/200 勝率 ／ 全員生存 | 本編 115/115 全員生存 | 九/新兵 400/300 全員生存 ／ 決着T | 九/農兵 400/300 全員生存 | 第五波 400/300 勝率 ／ 全員生存 | 重い波 400/300 全員生存 ／ 決着T | 落ちた/戦（本編 400/300）ボルグ ／ ホタ ／ ヒヨ ／ 相方 |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|---|---|---|");
        foreach (var (b, v) in Rows())
        {
            var n9 = At(b, v, BA.MainWave, 1).C; var h = At(b, v, WaveHeavy, 1).C; var w5 = At(b, v, 3, 1).C;
            long nn = MainWaves.Sum(w => At(b, v, w, 1).C.N);
            string fell = string.Join(" ／ ", Enumerable.Range(0, 4).Select(r => Per(MainWaves.Sum(w => At(b, v, w, 1).C.FellRole[r]), nn)));
            Console.WriteLine($"| {b} | {v} | {F1(MainAvg(b, v, 1, WinP))} ／ {F1(MainAvg(b, v, 1, SurvP))} | {F1(MainAvg(b, v, 0, WinP))} ／ {F1(MainAvg(b, v, 0, SurvP))} | {F1(MainAvg(b, v, 2, SurvP))} | {F1(SurvP(n9))} ／ {Per(n9.Turns, n9.N)} | {F1(SurvP(At(b, v, BA.MainWave + 1, 1).C))} | {F1(WinP(w5))} ／ {F1(SurvP(w5))} | {F1(SurvP(h))} ／ {Per(h.Turns, h.N)} | {fell} |");
        }
        foreach (var (rn, _) in Refs)
        {
            var n9 = At(rn, RefVer, BA.MainWave, 1).C; var h = At(rn, RefVer, WaveHeavy, 1).C; var w5 = At(rn, RefVer, 3, 1).C;
            Console.WriteLine($"| {rn} | — | {F1(MainAvg(rn, RefVer, 1, WinP))} ／ {F1(MainAvg(rn, RefVer, 1, SurvP))} | {F1(MainAvg(rn, RefVer, 0, WinP))} ／ {F1(MainAvg(rn, RefVer, 0, SurvP))} | {F1(MainAvg(rn, RefVer, 2, SurvP))} | {F1(SurvP(n9))} ／ {Per(n9.Turns, n9.N)} | {F1(SurvP(At(rn, RefVer, BA.MainWave + 1, 1).C))} | {F1(WinP(w5))} ／ {F1(SurvP(w5))} | {F1(SurvP(h))} ／ {Per(h.Turns, h.N)} | — |");
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
        Console.WriteLine("| 台 | 版 | 粛 400/300 勝率 ／ 全員生存 ／ 決着T | 粛 200/200 | 軛 400/300 勝率 ／ 全員生存 ／ 決着T | 軛 200/200 | 軛 400/300 の刻みの与ダメ/戦（敵） |");
        Console.WriteLine("|---|---|---|---|---|---|--:|");
        string Cl(FC.CAgg c) => $"{F1(WinP(c))} ／ {F1(SurvP(c))} ／ {Per(c.Turns, c.N)}";
        foreach (var (b, v) in Rows())
        {
            var y = At(b, v, WaveYoke, 1);
            Console.WriteLine($"| {b} | {v} | {Cl(At(b, v, WaveHush, 1).C)} | {Cl(At(b, v, WaveHush, 0).C)} | {Cl(y.C)} | {Cl(At(b, v, WaveYoke, 0).C)} | {Per(y.C.TickFoe, y.C.N)} |");
        }
        Console.WriteLine();
    }

    static void TableB()
    {
        Console.WriteLine("## 表B —— ホタの攻撃力（ポンの所感「1戦で 100 に届かない」）");
        Console.WriteLine();
        Console.WriteLine("「攻撃力」＝ ターン頭の写し（`StatSnapshot` ＝ 画面の攻撃力・熾火の ×4 ／ 段の倍率込み）。「振った」＝ 1回の攻撃に使った攻撃力（`Attack` の量・大技の倍率と ×3 も込み）。");
        Console.WriteLine("1戦の最大の平均 ／ 全戦の最大 ／ 100 に届いた戦 %。T1〜T8 は各ターンの頭の平均（8 以上は 8 に畳む）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | 攻撃力 1戦の最大 平均 ／ 最大 ／ 100 以上 % | 振った 平均 ／ 最大 ／ 100 以上 % | T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8+ | 落ちた % |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        foreach (string b in BoardNames)
            foreach (var (w, s) in KeyCells)
                foreach (var v in _vers)
                {
                    var k = At(b, v.Name, w, s).K;
                    if (k.HotaN == 0) continue;
                    string tr = string.Join(" | ", Enumerable.Range(1, TM).Select(t => k.AtkCnt[t] == 0 ? "—" : ((double)k.AtkSum[t] / k.AtkCnt[t]).ToString("F0")));
                    Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | {Per(k.AtkMaxSum, k.HotaN)} ／ {k.AtkMaxMax} ／ {Pct(k.Atk100, k.HotaN)} | {Per(k.SwingMaxSum, k.HotaN)} ／ {k.SwingMaxMax} ／ {Pct(k.Swing100, k.HotaN)} | {tr} | {(IsTarget(w) ? "—" : Pct(k.HotaFell, k.HotaN))} |");
                }
        Console.WriteLine();
    }

    static void TableC()
    {
        Console.WriteLine("## 表C —— 1発の最大・1ターンの総量（ホタ ／ ボルグ ／ ヨミ ／ セロ）");
        Console.WriteLine();
        Console.WriteLine("1発の最大は「1戦の最大の平均 ／ 全戦の最大」。1ターンの総量はそのターンまで戦が続いた戦の平均（的の波は 8 ターン全部）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | 駒 | 1発の最大 平均 ／ 最大 | 1戦の与ダメ | T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8+ |");
        Console.WriteLine("|---|---|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");
        IEnumerable<(string B, string V)> rows = Rows().Concat(Refs.Select(x => (x.Name, RefVer)));
        foreach (var (w, s) in new[] { (BA.MainWave, 1), (3, 1), (WaveYoke, 1), (WaveHeavy, 1), (WaveT1, 0), (WaveT9, 0) })
            foreach (var (b, v) in rows)
            {
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

    static readonly string[] BlazeKinds = { "ホタの火の癒し", "ヒヨの火の変換", "ベニの反転", "受けた", "焼かれない" };

    static void TableD()
    {
        Console.WriteLine("## 表D —— 爆炎（K3〜）: 1戦の回数・敵への与ダメ・味方への燃焼ダメージの行き先");
        Console.WriteLine();
        Console.WriteLine("行き先は名目/戦（括弧は実際に癒えた ／ 削られた HP/戦）: " + string.Join(" ／ ", BlazeKinds) + "。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | 爆炎/戦 | 敵への与ダメ/戦（倒した/戦） | " + string.Join(" | ", BlazeKinds) + " | 味方が倒れた/戦 |");
        Console.WriteLine("|---|---|---|--:|---|" + string.Concat(Enumerable.Repeat("---|", BlazeKinds.Length)) + "--:|");
        foreach (string b in BoardNames)
            foreach (var (w, s) in KeyCells)
                foreach (var v in _vers.Where(v => v.Borg.Traits.Contains(TraitId.UnleashBlaze)))
                {
                    var k = At(b, v.Name, w, s).K;
                    Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | {Per(k.Blazes, k.N)} | {Per(k.BlazeFoeDealt, k.N)}（{Per(k.BlazeFoeKills, k.N)}） | "
                        + string.Join(" | ", Enumerable.Range(0, 5).Select(i => $"{Per(k.BlazeNom[i], k.N)}（{Per(k.BlazeHp[i], k.N)}）")) + $" | {Per(k.BlazeAllyKills, k.N)} |");
                }
        Console.WriteLine();
        Console.WriteLine("### 表D′ —— 駒ごと（本編の第2〜5波 × 400/300 の合計・名目 ／ 癒えた ／ 削られた /戦・倒れた回）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 駒ごと |");
        Console.WriteLine("|---|---|---|");
        foreach (string b in BoardNames)
            foreach (var v in _vers.Where(v => v.Borg.Traits.Contains(TraitId.UnleashBlaze)))
            {
                var acc = new Dictionary<string, long[]>(); long n = 0;
                foreach (int w in MainWaves)
                {
                    var k = At(b, v.Name, w, 1).K; n += k.N;
                    foreach (var (id, a) in k.BlazeById) { if (!acc.TryGetValue(id, out var x)) acc[id] = x = new long[4]; for (int i = 0; i < 4; i++) x[i] += a[i]; }
                }
                Console.WriteLine($"| {b} | {v.Name} | " + (acc.Count == 0 ? "—" : string.Join(" ／ ", acc.OrderByDescending(kv => kv.Value[0]).Select(kv => $"{kv.Key} {Per(kv.Value[0], n)}・+{Per(kv.Value[1], n)}・−{Per(kv.Value[2], n)}・倒{kv.Value[3]}"))) + " |");
            }
        Console.WriteLine();
    }

    static readonly string[] MendSrc = { "刻み・起爆", "燃える巻き込み", "爆炎" };

    static void TableE()
    {
        Console.WriteLine("## 表E —— ホタの火の癒し（K1〜）: 1戦の回復（名目 ／ 癒えた HP）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | " + string.Join(" | ", MendSrc) + " | 合計 癒えた/戦 | ホタが落ちた % |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", MendSrc.Length)) + "--:|--:|");
        foreach (string b in BoardNames)
            foreach (var (w, s) in KeyCells.Where(c => !IsTarget(c.W)))
                foreach (var v in _vers)
                {
                    var k = At(b, v.Name, w, s).K;
                    Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | " + string.Join(" | ", Enumerable.Range(0, 3).Select(i => $"{Per(k.MendNom[i], k.N)} ／ {Per(k.MendHp[i], k.N)}")) + $" | {Per(k.MendHp.Sum(), k.N)} | {Pct(k.HotaFell, k.HotaN)} |");
                }
        Console.WriteLine();
        Console.WriteLine("### 表E′ —— ホタが落ちた %（本編の第2〜5波の平均 × 倍率）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 400/300 | 200/200 | 115/115 | 九/新兵 400/300 |");
        Console.WriteLine("|---|---|--:|--:|--:|--:|");
        foreach (var (b, v) in Rows())
        {
            string M(int s) { long f = 0, n = 0; foreach (int w in MainWaves) { var k = At(b, v, w, s).K; f += k.HotaFell; n += k.HotaN; } return Pct(f, n); }
            var n9 = At(b, v, BA.MainWave, 1).K;
            Console.WriteLine($"| {b} | {v} | {M(1)} | {M(0)} | {M(2)} | {Pct(n9.HotaFell, n9.HotaN)} |");
        }
        Console.WriteLine();
    }

    static void TableF()
    {
        Console.WriteLine("## 表F —— 残り火: 全体 ×2（K0〜K3）と5連撃（K4〜）");
        Console.WriteLine();
        Console.WriteLine("1回の残り火の手番でホタが敵に与えた量（平均 ／ 最大）と倒した数。5連撃は当てた敵の異なり/回も。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | 残り火/戦 | 与ダメ/回 ／ 最大 | 倒した/回 | 連撃 発/回 ／ 異なり/回 |");
        Console.WriteLine("|---|---|---|--:|---|--:|---|");
        foreach (string b in BoardNames)
            foreach (var (w, s) in new[] { (BA.MainWave, 1), (BA.MainWave, 0), (WaveT1, 0), (WaveT9, 0), (WaveHeavy, 1), (WaveYoke, 1), (3, 1) })
                foreach (var v in _vers)
                {
                    var k = At(b, v.Name, w, s).K;
                    Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | {Per(k.EmbersHands, k.N)} | {Per(k.EmbersDmg, k.EmbersHands)} ／ {k.EmbersMax} | {Per(k.EmbersKills, k.EmbersHands)} | {(k.EmbersChains == 0 ? "—" : $"{Per(k.EmbersChainHits, k.EmbersChains)} ／ {Per(k.EmbersChainDistinct, k.EmbersChains)}")} |");
                }
        Console.WriteLine();
    }

    static void TableG()
    {
        Console.WriteLine("## 表G —— 燃焼の刻み（回数 ／ 刻み・一撃）: 敵の刻みの与ダメ（HP を削った量）・味方の刻み（削られた ／ 回復）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 波・倍率 | 版 | 敵 刻み/戦 ／ 削った/戦 | 味方 削られた ／ 回復 /戦 | 1回にまとめた刻み/戦（火勢 2 ／ 3 ／ 4） | 勝率 ／ 全員生存 ／ 決着T |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (string b in BoardNames)
            foreach (var (w, s) in new[] { (WaveYoke, 1), (WaveYoke, 0), (BA.MainWave, 1), (3, 1), (WaveHeavy, 1), (WaveT1, 0) })
                foreach (var v in _vers.Where(v => v.Name is "K0" or "K4" or "K4t"))
                {
                    var c = At(b, v.Name, w, s); var k = c.K;
                    Console.WriteLine($"| {b} | {WN(w, s)} | {v.Name} | {Per(k.FoeTickN, k.N)} ／ {Per(k.FoeTickHp, k.N)} | {Per(k.AllyTickDmg, k.N)} ／ {Per(k.AllyTickHeal, k.N)} | {Per(k.TickOnceN[2], k.N)} ／ {Per(k.TickOnceN[3], k.N)} ／ {Per(k.TickOnceN[4], k.N)} | "
                        + (IsTarget(w) ? "—" : $"{F1(WinP(c.C))} ／ {F1(SurvP(c.C))} ／ {Per(c.C.Turns, c.C.N)}") + " |");
                }
        Console.WriteLine();
    }

    static void TableH()
    {
        Console.WriteLine("## 表H —— 手番の絵の単調さ（第239期の定義・九/新兵 × 400/300 と本編 第五波 × 400/300）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 駒 | 手番/戦 | 絵の種類/戦 | 最多の絵 % | 続けて同じ絵 % | 骨格の最多 % | 上位3 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|---|");
        foreach (string b in BoardNames)
            foreach (var v in _vers.Where(v => v.Name is "K0" or "K2a" or "K3" or "K4"))
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
