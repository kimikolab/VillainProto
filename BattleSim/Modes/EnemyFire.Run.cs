using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;

// enemyfire run —— 段2 ＋ 表A〜F（指示書 §6.3）と、`compare` 61 行の版ごとの動き。
static partial class EnemyFireDiag
{
    const int TM = 7;
    static readonly string[] Roles = { "ボルグ", "ホタ", "ヒヨ", "相方", "延焼" };

    /// <summary>敵の火勢の集計（1つのセル）。</summary>
    internal sealed class EAgg
    {
        public long N;
        // 表B
        public readonly long[,] LvHead = new long[TM + 1, 5];
        public readonly long[] HeadCnt = new long[TM + 1];
        public long Reach3, Reach4, GrowHits, Growth, Lv4Deaths, Spreads, SpreadLit, SpreadGrow, BattlesWith4;
        public readonly long[] First4 = new long[TM + 1], GrowByRole = new long[5];
        // 表C
        public long FoeDmg, FoeDirect, FoeTickDmg, FoeBrittleAll;
        public readonly long[] TickN = new long[5], TickHp = new long[5], TickXN = new long[5], TickXHp = new long[5], Brit = new long[5], BritUp = new long[5];
        // 表E（味方の刻み）
        public readonly long[] ATickN = new long[5], ATickDmg = new long[5], ATickHeal = new long[5], ATickXN = new long[5], ATickXDmg = new long[5], ATickXHeal = new long[5];
        public long ATickDeaths, ATickXDeaths;
        // 表F（終盤: 生きている敵が 2 体以下）
        public long EndBattles, EndTurns, EndDmg, EndMaxHit, EndHota, EndBorg;
        public readonly long[] EndPerTurnMax = new long[1];
        // 表C′（追記 A）: 放つ ／ 放つ → 焼き尽くす の順で撃てた戦 ／ 放つの直後のホタの手番の与ダメ ／ ホタの手番の平均 ／ 放つで育てた敵
        public long Unleashes, UnleashThenBurn, HotaAfterU, HotaAfterUN, HotaHands, HotaHandDmg, UnleashStoked, UnleashRaised, UnleashAt4, UnleashKilled;

        public void Merge(EAgg o)
        {
            N += o.N;
            for (int t = 0; t <= TM; t++) { HeadCnt[t] += o.HeadCnt[t]; First4[t] += o.First4[t]; for (int l = 0; l < 5; l++) LvHead[t, l] += o.LvHead[t, l]; }
            Reach3 += o.Reach3; Reach4 += o.Reach4; GrowHits += o.GrowHits; Growth += o.Growth; Lv4Deaths += o.Lv4Deaths; Spreads += o.Spreads; SpreadLit += o.SpreadLit; SpreadGrow += o.SpreadGrow; BattlesWith4 += o.BattlesWith4;
            for (int i = 0; i < 5; i++)
            {
                GrowByRole[i] += o.GrowByRole[i];
                TickN[i] += o.TickN[i]; TickHp[i] += o.TickHp[i]; TickXN[i] += o.TickXN[i]; TickXHp[i] += o.TickXHp[i]; Brit[i] += o.Brit[i]; BritUp[i] += o.BritUp[i];
                ATickN[i] += o.ATickN[i]; ATickDmg[i] += o.ATickDmg[i]; ATickHeal[i] += o.ATickHeal[i]; ATickXN[i] += o.ATickXN[i]; ATickXDmg[i] += o.ATickXDmg[i]; ATickXHeal[i] += o.ATickXHeal[i];
            }
            FoeDmg += o.FoeDmg; FoeDirect += o.FoeDirect; FoeTickDmg += o.FoeTickDmg; FoeBrittleAll += o.FoeBrittleAll;
            ATickDeaths += o.ATickDeaths; ATickXDeaths += o.ATickXDeaths;
            EndBattles += o.EndBattles; EndTurns += o.EndTurns; EndDmg += o.EndDmg; EndMaxHit = Math.Max(EndMaxHit, o.EndMaxHit); EndHota += o.EndHota; EndBorg += o.EndBorg;
            EndPerTurnMax[0] = Math.Max(EndPerTurnMax[0], o.EndPerTurnMax[0]);
            Unleashes += o.Unleashes; UnleashThenBurn += o.UnleashThenBurn; HotaAfterU += o.HotaAfterU; HotaAfterUN += o.HotaAfterUN; HotaHands += o.HotaHands; HotaHandDmg += o.HotaHandDmg; UnleashStoked += o.UnleashStoked; UnleashRaised += o.UnleashRaised; UnleashAt4 += o.UnleashAt4; UnleashKilled += o.UnleashKilled;
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e)
        {
            N++;
            var ev = r.Events;
            var foe = new HashSet<int>(e.Select(u => u.InstanceId));
            var ids = p.ToDictionary(u => u.InstanceId, u => u.Def.Id);
            foreach (var x in ev)
                if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && !ids.ContainsKey(sid) && (x.Team ?? BattleContext.EnemyTeam) == BattleContext.EnemyTeam) foe.Add(sid);
            int? hota = p.FirstOrDefault(u => u.Def.Id == "hota")?.InstanceId, borg = p.FirstOrDefault(u => u.Def.Id == "borg")?.InstanceId;
            if (r.FireLevels is FireLevelLedger fl)
            {
                foreach (var s in fl.Snaps)
                    if (foe.Contains(s.Id)) { int t = Math.Min(TM, s.Turn); LvHead[t, s.Level]++; }
                var headTurns = fl.Snaps.Where(s => foe.Contains(s.Id)).Select(s => Math.Min(TM, s.Turn)).Distinct();
                foreach (int t in headTurns) HeadCnt[t]++;
                GrowHits += fl.FoeGrowHits; Growth += fl.FoeGrowth; Lv4Deaths += fl.FoeLv4Deaths; Spreads += fl.FoeSpreads; SpreadLit += fl.FoeSpreadLit; SpreadGrow += fl.FoeSpreadGrow;
                for (int l = 0; l < 5; l++)
                {
                    TickN[l] += fl.FoeTickN[l]; TickHp[l] += fl.FoeTickHp[l]; TickXN[l] += fl.FoeTickExtraN[l]; TickXHp[l] += fl.FoeTickExtraHp[l]; Brit[l] += fl.FoeBrittle[l]; BritUp[l] += fl.FoeBrittleUp[l];
                    ATickN[l] += fl.AllyTickN[l]; ATickDmg[l] += fl.AllyTickDmg[l]; ATickHeal[l] += fl.AllyTickHeal[l]; ATickXN[l] += fl.AllyTickExtraN[l]; ATickXDmg[l] += fl.AllyTickExtraDmg[l]; ATickXHeal[l] += fl.AllyTickExtraHeal[l];
                }
                ATickDeaths += fl.AllyTickDeaths; ATickXDeaths += fl.AllyTickExtraDeaths;
                UnleashStoked += fl.UnleashFoeStoked; UnleashRaised += fl.UnleashFoeRaised; UnleashAt4 += fl.UnleashFoeAt4;
            }
            if (hota is int ht)
            {
                var hands = r.Hands.Where(h => h.ActorId == ht).OrderBy(h => h.EventStart).ToList();
                long HandDmg(HandRecord h) { long d = 0; for (int j = h.EventStart; j < h.EventEnd && j < ev.Count; j++) if (ev[j].Kind == BattleEventKind.Damage && ev[j].ActorId == ht && ev[j].TargetId is int tt && foe.Contains(tt)) d += ev[j].Amount; return d; }
                foreach (var h in hands) { HotaHands++; HotaHandDmg += HandDmg(h); }
                bool ub = false;
                for (int i = 0; i < ev.Count; i++)
                {
                    if (ev[i] is not { Kind: BattleEventKind.FireLevel, Text: FireLevelLabels.Unleash }) continue;
                    Unleashes++;
                    for (int j = i + 1; j < ev.Count; j++) { if (ev[j].Kind == BattleEventKind.FireLevel && ev[j].Text == FireLevelLabels.Spent) continue; if (ev[j].Kind == BattleEventKind.Attack && ev[j].ActorId != ev[i].ActorId) break; if (ev[j].Kind == BattleEventKind.FireLevel && ev[j].Text is FireLevelLabels.Spread or FireLevelLabels.CallFire) break; if (ev[j].Kind == BattleEventKind.Death && ev[j].ActorId == ev[i].ActorId && ev[j].TargetId is int kt && foe.Contains(kt)) UnleashKilled++; }
                    var nx = hands.FirstOrDefault(h => h.EventStart > i);
                    if (nx.EventEnd > 0) { HotaAfterU += HandDmg(nx); HotaAfterUN++; }
                    for (int j = i + 1; j < ev.Count; j++) if (ev[j] is { Kind: BattleEventKind.FireLevel, Text: FireLevelLabels.Burnout } b && b.ActorId == ht) { ub = true; break; }
                }
                if (ub) UnleashThenBurn++;
            }
            var r3 = new HashSet<int>(); var r4 = new HashSet<int>(); int first4 = 0;
            int alive = e.Count; var dead = new HashSet<int>();
            foreach (var x in ev) if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid2 && foe.Contains(sid2) && !e.Any(u => u.InstanceId == sid2)) alive++;
            int endStart = 0; long endTurnMax = 0, curTurnDmg = 0; int curTurn = 0;
            bool lastWasTick = false; int tickTarget = -1;
            foreach (var x in ev)
            {
                if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.GrowFoe && x.TargetId is int t && foe.Contains(t))
                {
                    if (x.Amount >= 3) r3.Add(t);
                    if (x.Amount >= 4 && r4.Add(t) && first4 == 0) first4 = x.Turn;
                    int ro = x.ActorId is int a && foe.Contains(a) ? 4 : x.ActorId is int a2 && ids.TryGetValue(a2, out var aid) ? FB.RoleOf(aid) : 3;
                    GrowByRole[ro]++;
                }
                if (x.Kind == BattleEventKind.Status && x.Text == "燃焼" && x.TargetId is int bt && foe.Contains(bt)) { lastWasTick = true; tickTarget = bt; continue; }
                if (x.Kind == BattleEventKind.Damage && x.TargetId is int d && foe.Contains(d))
                {
                    FoeDmg += x.Amount;
                    FoeBrittleAll += x.BrittleExtra ?? 0;
                    bool isTick = lastWasTick && tickTarget == d && x.ActorId is null;
                    if (isTick) FoeTickDmg += x.Amount; else if (x.ActorId is not null) FoeDirect += x.Amount;
                    if (endStart > 0)
                    {
                        EndDmg += x.Amount; EndMaxHit = Math.Max(EndMaxHit, x.Amount);
                        if (hota is not null && x.ActorId == hota) EndHota += x.Amount;
                        if (borg is not null && x.ActorId == borg) EndBorg += x.Amount;
                        if (x.Turn != curTurn) { endTurnMax = Math.Max(endTurnMax, curTurnDmg); curTurnDmg = 0; curTurn = x.Turn; }
                        curTurnDmg += x.Amount;
                    }
                }
                lastWasTick = false;
                if (x.Kind == BattleEventKind.Death && x.TargetId is int k && foe.Contains(k) && dead.Add(k))
                {
                    alive--;
                    if (alive is > 0 and <= 2 && endStart == 0) { endStart = x.Turn; curTurn = x.Turn; }
                }
            }
            endTurnMax = Math.Max(endTurnMax, curTurnDmg);
            Reach3 += r3.Count; Reach4 += r4.Count;
            if (r4.Count > 0) { BattlesWith4++; First4[Math.Min(TM, first4)]++; }
            if (endStart > 0) { EndBattles++; EndTurns += r.Turns - endStart + 1; EndPerTurnMax[0] = Math.Max(EndPerTurnMax[0], endTurnMax); }
        }
    }

    internal static (FB.LAgg L, EAgg E) MeasureE(Formation f, int w, EnemyScaleRule sc, int seeds = BA.Seeds)
    {
        var ls = new FB.LAgg[seeds]; var es = new EAgg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = BA.WaveOf(w, sc)();
            var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
            var r = BattleEngine.Run(p, e, i, verbose: true);
            var a = new FB.LAgg(); a.Take(r, p, e, slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value)); ls[i] = a;
            var b = new EAgg(); b.Take(r, p, e); es[i] = b;
        });
        var la = new FB.LAgg(); var ea = new EAgg();
        for (int i = 0; i < seeds; i++) { la.Merge(ls[i]); ea.Merge(es[i]); }
        return (la, ea);
    }

    static readonly string[] BoardNames = { "T3", "T3′", "T3-238", "T3-244", "雷＋ボルグ" };
    static Formation BoardOf(string name, FB.Ver v, Dictionary<string, List<FB.Pick>> ranked) => name switch
    {
        "T3" => Apply(FB.Dec(ranked[v.Name][0].Best), v),
        "T3′" => Apply(FB.Dec(ranked[v.Name].First(FB.IsT3P).Best), v),
        "T3-238" => Apply(T3238, v),
        "T3-244" => Apply(T3244, v),
        "雷＋ボルグ" => Apply(ThunderBorg, v),
        _ => throw new ArgumentException(name),
    };
    static readonly (string Name, Func<Formation> F)[] Refs = { ("参考 移動", () => BA.RefMove), ("参考 雷", () => BA.RefThunder) };

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ranked = new Dictionary<string, List<FB.Pick>>();
        foreach (var v in Versions) ranked[v.Name] = FB.Ranked(PicksOf(v, out _));
        Console.WriteLine("# 第245期 段2 —— 敵の火勢（版 E0〜E2）");
        Console.WriteLine();
        Stage1Summary(ranked);
        var cells = new Dictionary<(string B, string V, int W, int S), (FB.LAgg L, EAgg E)>();
        foreach (string bn in BoardNames)
            foreach (var v in Versions)
            {
                var f = BoardOf(bn, v, ranked);
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < BA.Scales.Length; s++)
                        cells[(bn, v.Name, w, s)] = MeasureE(f, w, BA.Scales[s].Sc);
            }
        foreach (var (rn, rf) in Refs)
            for (int w = 0; w < BA.WaveNames.Length; w++)
                for (int s = 0; s < BA.Scales.Length; s++)
                    cells[(rn, "E0", w, s)] = MeasureE(rf(), w, BA.Scales[s].Sc);
        Console.Error.WriteLine($"  段2 の測定 {sw.Elapsed.TotalSeconds:F0} 秒");
        Console.WriteLine("## 台（版ごとの席）");
        Console.WriteLine();
        foreach (var v in Versions) foreach (string bn in BoardNames) Console.WriteLine($"- {v.Name} × {bn}: {BA.SeatsNamed(BoardOf(bn, v, ranked))}");
        foreach (var (rn, rf) in Refs) Console.WriteLine($"- {rn}（版に依らない）: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
        TableA(cells); TableA2(cells); TableB(cells); TableC(cells); TableC2(cells); TableD(cells); TableE(cells); TableF(cells);
        CompareMoves();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    static string Sc(FB.Score4 s) => $"{s.S.Sv}/40・400/300 {s.Sv4}/40・落 {s.S.Fell}・勝 {s.S.W}";
    static void Stage1Summary(Dictionary<string, List<FB.Pick>> ranked)
    {
        Console.WriteLine("## 段1（T3 1,081 組 × 席 120 × seed 1000..1039・九/新兵 × 200/200 → 400/300 → 落 → 決着T）");
        Console.WriteLine();
        Console.WriteLine("| 版 | T3 の1位（相方） | 席 | 得点 | T3′ の1位 | 席 | 得点 | 上位10 のシオ・ササ | 200/200 で 40/40 の組 | 400/300 も 40/40 の組 |");
        Console.WriteLine("|---|---|---|---|---|---|---|--:|--:|--:|");
        foreach (var v in Versions)
        {
            var rk = ranked[v.Name]; var b = rk[0]; var bp = rk.First(FB.IsT3P);
            Console.WriteLine($"| {v.Name} | {string.Join("・", b.Partners)} | {BA.SeatsNamed(FB.Dec(b.Best))} | {Sc(b.BestS)} | {string.Join("・", bp.Partners)} | {BA.SeatsNamed(FB.Dec(bp.Best))} | {Sc(bp.BestS)} | {rk.Take(10).Count(p => !FB.IsT3P(p))} / 10 | {rk.Count(p => p.BestS.S.Sv == BA.PickSeeds)} | {rk.Count(p => p.BestS.S.Sv == BA.PickSeeds && p.BestS.Sv4 == BA.PickSeeds)} |");
        }
        Console.WriteLine();
        Console.WriteLine("上位10 組（相方 ／ 得点 200/200・400/300）:");
        Console.WriteLine();
        foreach (var v in Versions)
            Console.WriteLine($"- {v.Name}: " + string.Join(" ／ ", ranked[v.Name].Take(10).Select(p => $"{string.Join("・", p.Partners)} {p.BestS.S.Sv}/{p.BestS.Sv4}")));
        Console.WriteLine();
    }

    static IEnumerable<(string B, string V)> BV()
    {
        foreach (string bn in BoardNames) foreach (var v in Versions) yield return (bn, v.Name);
        foreach (var (rn, _) in Refs) yield return (rn, "E0");
    }
    static string ACell(FB.LAgg a) => $"{Pct(a.AllSurv, a.N)} ／ {Pct(a.Wins, a.N)} ／ {Per(a.Turns, a.N)} ／ {Per(a.Fell, a.N)}";

    static void TableA(Dictionary<(string B, string V, int W, int S), (FB.LAgg L, EAgg E)> cells)
    {
        Console.WriteLine("## 表A —— 全員生存 ／ 勝率 ／ 決着T ／ 落ちた駒（/戦）");
        Console.WriteLine();
        Console.WriteLine("主判定（九/新兵）の3倍率:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 200/200 | 400/300 | 115/115 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var (b, v) in BV())
            Console.WriteLine($"| {b} | {v} | " + string.Join(" | ", Enumerable.Range(0, 3).Select(s => ACell(cells[(b, v, BA.MainWave, s)].L))) + " |");
        Console.WriteLine();
        Console.WriteLine("ほかの波（全員生存 ／ 勝率 ／ 決着T ／ 落）と本編 第2〜5波の平均:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | " + string.Join(" | ", Enumerable.Range(0, BA.WaveNames.Length).Where(w => w != BA.MainWave).Select(w => BA.WaveNames[w])) + " | 本編の平均（全員生存 ／ 勝率 ／ 決着T） |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", BA.WaveNames.Length)));
        foreach (var (b, v) in BV())
            for (int s = 0; s < 3; s++)
            {
                var ws = Enumerable.Range(0, 4).Select(w => cells[(b, v, w, s)].L).ToList();
                string avg = $"{ws.Average(a => 100.0 * a.AllSurv / a.N):F1} ／ {ws.Average(a => 100.0 * a.Wins / a.N):F1} ／ {ws.Average(a => (double)a.Turns / a.N):F2}";
                Console.WriteLine($"| {b} | {v} | {BA.Scales[s].Name} | " + string.Join(" | ", Enumerable.Range(0, BA.WaveNames.Length).Where(w => w != BA.MainWave).Select(w => ACell(cells[(b, v, w, s)].L))) + $" | {avg} |");
            }
        Console.WriteLine();
    }

    static void TableA2(Dictionary<(string B, string V, int W, int S), (FB.LAgg L, EAgg E)> cells)
    {
        Console.WriteLine("## 表A′ —— 粛の波（本編 第二波）・軛の波（本編 第四波）× 400/300");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 全員生存 ／ 勝率 ／ 決着T ／ 落 | 火勢4 に届いた敵/戦 | 延焼/戦 | 敵への与ダメ/戦 | うち刻み ／ 脆さの分（名目） | 脆さの分のうち 25% を超えた分 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|---|--:|");
        foreach (string b in BoardNames)
            foreach (var v in Versions)
                foreach (int w in new[] { 0, 2 })
                {
                    var (l, e) = cells[(b, v.Name, w, 1)];
                    Console.WriteLine($"| {b} | {v.Name} | {BA.WaveNames[w]} | {ACell(l)} | {Per(e.Reach4, e.N)} | {Per(e.Spreads, e.N)} | {Per(e.FoeDmg, e.N)} | {Per(e.FoeTickDmg, e.N)} ／ {Per(e.FoeBrittleAll, e.N)} | {Per(e.BritUp.Sum(), e.N)} |");
                }
        Console.WriteLine();
    }

    static void TableB(Dictionary<(string B, string V, int W, int S), (FB.LAgg L, EAgg E)> cells)
    {
        Console.WriteLine("## 表B —— 敵の火勢の分布（周回の頭・刻みの後・生きている敵）・火勢4 に届いた敵・延焼");
        Console.WriteLine();
        Console.WriteLine("周回の頭の敵の数（/戦）を火勢 0（燃えていない）／ 1 ／ 2 ／ 3 ／ 4 で。「育ちの出どころ」は「育つ・敵」の殴り手（延焼は倒れた敵）。E0 は敵の火勢が無い（—）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | T1 | T2 | T3 | T4 | T5 | 火勢3 ／ 4 に届いた敵/戦 | 火勢4 が出た戦 %（初めて 周回1 ／ 2 ／ 3 ／ 4+ %） | 育つ判定 ／ 上がった /戦 | 育ちの出どころ ボルグ ／ ホタ ／ ヒヨ ／ 相方 ／ 延焼 % | 火勢4 で倒れた敵/戦 | 延焼/戦（点けた ／ 育てた） |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|--:|---|");
        foreach (string b in BoardNames)
            foreach (var v in Versions.Skip(1))
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                    {
                        if (w != BA.MainWave && s != 1) continue;
                        var e = cells[(b, v.Name, w, s)].E;
                        string H(int t) => string.Join("／", Enumerable.Range(0, 5).Select(l => Per(e.LvHead[t, l], e.N)));
                        long f4 = e.First4.Sum(), gr = e.GrowByRole.Sum();
                        string first = f4 == 0 ? "—" : string.Join(" ／ ", Pct(e.First4[1], f4), Pct(e.First4[2], f4), Pct(e.First4[3], f4), Pct(e.First4.Skip(4).Sum(), f4));
                        string role = string.Join(" ／ ", Enumerable.Range(0, 5).Select(i => Pct(e.GrowByRole[i], gr)));
                        Console.WriteLine($"| {b} | {v.Name} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {H(1)} | {H(2)} | {H(3)} | {H(4)} | {H(5)} | {Per(e.Reach3, e.N)} ／ {Per(e.Reach4, e.N)} | {Pct(e.BattlesWith4, e.N)}（{first}） | {Per(e.GrowHits, e.N)} ／ {Per(e.Growth, e.N)} | {role} | {Per(e.Lv4Deaths, e.N)} | {Per(e.Spreads, e.N)}（{Per(e.SpreadLit, e.N)} ／ {Per(e.SpreadGrow, e.N)}） |");
                    }
        Console.WriteLine();
    }

    static void TableC(Dictionary<(string B, string V, int W, int S), (FB.LAgg L, EAgg E)> cells)
    {
        Console.WriteLine("## 表C —— 敵への与ダメの内訳（/戦）");
        Console.WriteLine();
        Console.WriteLine("「刻み」＝ 燃焼の刻みの `Damage`（出どころなし）、「直接」＝ 出どころのある `Damage`（攻撃・反射・放電ほか）。「脆さの分」は `BrittleExtra`（名目・破片や軛で削られる前）。");
        Console.WriteLine("火勢ごとの列は E-刻み〜E2 だけ（敵の火勢の帳簿）: 刻みの HP（1回目 ＋ 追加の回数）と、脆さの名目のうち 25% を超えた分。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | 与ダメ | 直接 | 刻み | 脆さの分（名目） | 刻み HP 火勢1 ／ 2 ／ 3 ／ 4（うち追加の回数） | 脆さの 25% 超え 火勢2 ／ 3 ／ 4 | 追加の刻み ＋ 25% 超え ÷ 与ダメ % |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|---|---|--:|");
        foreach (string b in BoardNames)
            foreach (var v in Versions)
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                    {
                        if (w != BA.MainWave && s != 1) continue;
                        var e = cells[(b, v.Name, w, s)].E;
                        string tk = string.Join(" ／ ", Enumerable.Range(1, 4).Select(l => $"{Per(e.TickHp[l] + e.TickXHp[l], e.N)}（{Per(e.TickXHp[l], e.N)}）"));
                        string bu = string.Join(" ／ ", Enumerable.Range(2, 3).Select(l => Per(e.BritUp[l], e.N)));
                        long add = e.TickXHp.Sum() + e.BritUp.Sum();
                        Console.WriteLine($"| {b} | {v.Name} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Per(e.FoeDmg, e.N)} | {Per(e.FoeDirect, e.N)} | {Per(e.FoeTickDmg, e.N)} | {Per(e.FoeBrittleAll, e.N)} | {tk} | {bu} | {Pct(add, e.FoeDmg)} |");
                    }
        Console.WriteLine();
    }

    static void TableC2(Dictionary<(string B, string V, int W, int S), (FB.LAgg L, EAgg E)> cells)
    {
        Console.WriteLine("## 表C′ —— 放つ → 焼き尽くす（追記 A・E0 ／ E1 ／ E1+放）");
        Console.WriteLine();
        Console.WriteLine("放つ/戦・放つで育てた敵/戦（E1+放 の追加の育ち）・放つ → 焼き尽くす の順で撃てた戦・**放つの直後のホタの最初の手番の与ダメ** と ホタの手番の平均・ホタの1戦の与ダメ。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | 放つ/戦 | 放つで倒した/戦 | 放つで育てた/戦（上がった ／ 既に4） | 放つ → 焼き尽くす の戦 % | 放つの直後のホタの手番 ／ ホタの手番の平均 | ホタの1戦 | 全員生存 ／ 勝率 ／ 決着T |");
        Console.WriteLine("|---|---|---|---|--:|--:|---|--:|---|--:|---|");
        foreach (string b in BoardNames)
            foreach (var v in new[] { "E0", "E1", "E1+放" })
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                    {
                        if (w != BA.MainWave && s != 1) continue;
                        var (l, e) = cells[(b, v, w, s)];
                        string hota = l.OutSum.TryGetValue("hota", out var hs) ? Per(hs[0], l.N) : "—";
                        Console.WriteLine($"| {b} | {v} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Per(e.Unleashes, e.N)} | {Per(e.UnleashKilled, e.N)} | {Per(e.UnleashStoked, e.N)}（{Per(e.UnleashRaised, e.N)} ／ {Per(e.UnleashAt4, e.N)}） | {Pct(e.UnleashThenBurn, e.N)} | {Per(e.HotaAfterU, e.HotaAfterUN)} ／ {Per(e.HotaHandDmg, e.HotaHands)} | {hota} | {Pct(l.AllSurv, l.N)} ／ {Pct(l.Wins, l.N)} ／ {Per(l.Turns, l.N)} |");
                    }
        Console.WriteLine();
    }

    static void TableD(Dictionary<(string B, string V, int W, int S), (FB.LAgg L, EAgg E)> cells)
    {
        Console.WriteLine("## 表D —— ホタ・ボルグ vs ヨミ・セロ（九/新兵）");
        Console.WriteLine();
        Console.WriteLine("1発の最大 ／ 平均・1ターンの総量（その周回まで続いた戦の平均）・全戦の最大・1戦の与ダメ（敵への `Damage`・中継を除く・第244期の表F と同じ数え方）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | 駒 | 1発 最大 ／ 平均 | 1Tの総量 平均 T1 ／ T2 ／ T3 ／ T4 ／ T5 | 1Tの総量の最大 | 1戦の与ダメ |");
        Console.WriteLine("|---|---|---|---|---|---|--:|--:|");
        var rows = new List<(string B, string V, string[] Ids)>();
        foreach (string b in new[] { "T3", "T3-238", "T3-244" }) foreach (var v in Versions) rows.Add((b, v.Name, new[] { "hota", "borg" }));
        rows.Add(("参考 移動", "E0", new[] { "yomi", "sero" }));
        rows.Add(("参考 雷", "E0", new[] { "kata", "shiga" }));
        foreach (var (b, v, idsx) in rows)
            for (int s = 0; s < 3; s++)
            {
                var a = cells[(b, v, BA.MainWave, s)].L;
                foreach (string id in idsx)
                {
                    if (!a.HitN.ContainsKey(id)) continue;
                    var sum = a.OutSum[id]; var mx = a.OutMax[id];
                    string turns = string.Join(" ／ ", Enumerable.Range(1, 5).Select(t => Per(sum[t], a.Reached[t])));
                    Console.WriteLine($"| {b} | {v} | {BA.Scales[s].Name} | {id} | {a.HitMax[id]} ／ {Per(a.HitSum[id], a.HitN[id])} | {turns} | {mx[0]} | {Per(sum[0], a.N)} |");
                }
            }
        Console.WriteLine();
    }

    static void TableE(Dictionary<(string B, string V, int W, int S), (FB.LAgg L, EAgg E)> cells)
    {
        Console.WriteLine("## 表E —— 味方の刻み（E1 ／ E2）");
        Console.WriteLine();
        Console.WriteLine("味方（保持者の陣営）の燃焼の刻み（/戦）。「削った」「回復」は刻みの前後の HP の差（回復は火の変換・ベニの反転・火の癒し）。「追加」は E2 の火勢の回数の分。熾火（ホタ）の刻みは削らないので 0。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | 刻み 1回目（回 ／ 削った ／ 回復） | 追加（回 ／ 削った ／ 回復） | 刻みで倒れた（1回目 ／ 追加） | 全員生存 ／ 勝率 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|");
        foreach (string b in BoardNames)
            foreach (var v in Versions.Skip(3))
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                    {
                        if (w != BA.MainWave && s != 1) continue;
                        var (l, e) = cells[(b, v.Name, w, s)];
                        Console.WriteLine($"| {b} | {v.Name} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Per(e.ATickN.Sum(), e.N)} ／ {Per(e.ATickDmg.Sum(), e.N)} ／ {Per(e.ATickHeal.Sum(), e.N)} | {Per(e.ATickXN.Sum(), e.N)} ／ {Per(e.ATickXDmg.Sum(), e.N)} ／ {Per(e.ATickXHeal.Sum(), e.N)} | {Per(e.ATickDeaths, e.N)} ／ {Per(e.ATickXDeaths, e.N)} | {Pct(l.AllSurv, l.N)} ／ {Pct(l.Wins, l.N)} |");
                    }
        Console.WriteLine();
    }

    static void TableF(Dictionary<(string B, string V, int W, int S), (FB.LAgg L, EAgg E)> cells)
    {
        Console.WriteLine("## 表F —— 終盤（生きている敵が 2 体以下）の与ダメの集中（ボス戦の見込み）");
        Console.WriteLine();
        Console.WriteLine("終盤に入った戦の割合・終盤の周回（入った周回から決着まで）・終盤の与ダメ/周回・1ターンの総量の最大・1発の最大・ホタ ／ ボルグの取り分。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | 終盤に入った戦 % | 終盤の周回/戦 | 終盤の与ダメ/周回 | 1Tの総量の最大 | 1発の最大 | ホタ ／ ボルグ % |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|---|");
        foreach (string b in BoardNames.Concat(Refs.Select(r => r.Name)))
            foreach (var v in b.StartsWith("参考") ? new[] { "E0" } : Versions.Select(x => x.Name))
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                    {
                        if (w != BA.MainWave && s != 1) continue;
                        var e = cells[(b, v, w, s)].E;
                        Console.WriteLine($"| {b} | {v} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Pct(e.EndBattles, e.N)} | {Per(e.EndTurns, e.EndBattles)} | {Per(e.EndDmg, e.EndTurns)} | {e.EndPerTurnMax[0]} | {e.EndMaxHit} | {Pct(e.EndHota, e.EndDmg)} ／ {Pct(e.EndBorg, e.EndDmg)} |");
                    }
        Console.WriteLine();
    }

    static void CompareMoves()
    {
        Console.WriteLine("## `compare` 61 行 × 第1〜5波 × seed 0..199");
        Console.WriteLine();
        var rows = CompareBuilds();
        var w = CompareRates();
        Console.WriteLine("| 版 | 動いたセル | 上がった ／ 下がった | 動いた行 |");
        Console.WriteLine("|---|--:|---|---|");
        for (int v = 1; v < Versions.Length; v++)
        {
            int moved = 0, up = 0, down = 0; var names = new List<string>();
            for (int i = 0; i < rows.Length; i++)
            {
                var d = Enumerable.Range(0, 5).Select(st => w[v, i, st] - w[0, i, st]).ToArray();
                int m = d.Count(x => Math.Abs(x) > 1e-9);
                if (m == 0) continue;
                moved += m; up += d.Count(x => x > 1e-9); down += d.Count(x => x < -1e-9);
                names.Add($"`{rows[i].Name}` " + string.Join(" ／ ", Enumerable.Range(0, 5).Where(st => Math.Abs(d[st]) > 1e-9).Select(st => $"第{st + 1}波 {w[0, i, st]:F1} → {w[v, i, st]:F1}")));
            }
            Console.WriteLine($"| {Versions[v].Name} | {moved} | {up} ／ {down} | {(names.Count == 0 ? "—" : string.Join("<br>", names))} |");
        }
        Console.WriteLine();
    }
}
