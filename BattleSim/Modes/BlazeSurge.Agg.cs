using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;
using FF = FireFinishDiag;
using FK = FireKindleDiag;
using GO = GiftOrderDiag;

// blazesurge の集計（1つのセル ＝ 台 × 版 × 波 × 倍率）。台本（verbose）と火勢の帳簿を読むだけで、盤面は1ビットも動かさない。
// 1戦を1回だけ回し、第246・249・252・253期の集計（CAgg / KAgg / MAgg / OAgg）と、この期の集計（SAgg）に同じ結果を流す。
// 的の波は台本を 8 ターンで切る（帳簿から読む量は戦全体なので参考）。
static partial class BlazeSurgeDiag
{
    const int TM = FK.TM;

    internal sealed class SAgg
    {
        public long N, BorgN, HotaN, HiyoN;
        public long Blazes, SoloBlazes;
        // Q0-2: 爆炎の直後に同じギフトの中でホタの手番が来たか・爆炎の瞬間のホタ ／ ヒヨの火勢（上げる前 ／ 後）
        public long BlazeHotaAlive, BlazeHotaNext;
        public readonly long[] HotaPre = new long[5], HotaPost = new long[5], HotaNextPre = new long[5], HotaNextPost = new long[5], HiyoPre = new long[5], HiyoPost = new long[5];
        // 表C: 爆炎の上げでホタに入った攻撃力（あぶれた火）・爆炎の後のホタの最初の1発（攻撃の Amount）・ホタの1発の平均
        public long SurgeHotaAtk, SurgeHotaOver, FirstHitN, FirstHitSum, FirstHitBurnN, HotaHitN, HotaHitSum, FirstHitMax;
        // 表D: 爆炎の上げでヒヨに溜まった渡す火（あぶれた火）／ 上限で捨てた
        public long SurgeHiyoHoard, SurgeHiyoCapped;
        // 表E: 延焼・爆炎で倒した敵（爆炎の一撃の中）・爆炎の後に倒れた敵（同じ周回 ／ 次の周回）
        public long FoeSpreads, FoeLv4Deaths, BlazeKills, KillsSame, KillsNext;
        // 表F: 爆炎の瞬間の火勢の分布（敵 ／ 味方・前 ／ 後）・上げの帳簿・脆さの名目
        public readonly long[] FoePre = new long[5], FoePost = new long[5], AllyPre = new long[5], AllyPost = new long[5];
        public long SurgeFoe, SurgeFoeSteps, SurgeFoeTo4, SurgeAlly, SurgeAllySteps, SurgeAllyTo4, SurgeAllyOver;
        public readonly long[] Brittle = new long[5], BrittleUp = new long[5];
        public readonly long[] FoeTickExtraHp = new long[5];
        // 表G: 爆炎・独りの味方の側（Id → [名目, 癒えた, 削られた, 倒れた]）・刻み
        public readonly Dictionary<string, long[]> SoloById = new();
        public readonly long[] SoloNom = new long[5], SoloHp = new long[5];
        public long SoloKills, AllyTickDmg, AllyTickExtraDmg, AllyTickDeaths, AllyTickExtraDeaths, AllyTickHeal, AllyTickExtraHeal;
        // 表H: 周回の頭に同時に火勢4 の味方の数（生きている味方）・爆炎の次の周回 ／ 次の次
        public readonly long[] Lv4Sum = new long[TM + 1], Lv4Cnt = new long[TM + 1], Lv4Ge3 = new long[TM + 1], Lv4Ge2 = new long[TM + 1];
        public long After1Sum, After1N, After2Sum, After2N;

        public void Merge(SAgg o)
        {
            N += o.N; BorgN += o.BorgN; HotaN += o.HotaN; HiyoN += o.HiyoN; Blazes += o.Blazes; SoloBlazes += o.SoloBlazes;
            BlazeHotaAlive += o.BlazeHotaAlive; BlazeHotaNext += o.BlazeHotaNext;
            for (int i = 0; i < 5; i++)
            {
                HotaPre[i] += o.HotaPre[i]; HotaPost[i] += o.HotaPost[i]; HotaNextPre[i] += o.HotaNextPre[i]; HotaNextPost[i] += o.HotaNextPost[i];
                HiyoPre[i] += o.HiyoPre[i]; HiyoPost[i] += o.HiyoPost[i];
                FoePre[i] += o.FoePre[i]; FoePost[i] += o.FoePost[i]; AllyPre[i] += o.AllyPre[i]; AllyPost[i] += o.AllyPost[i];
                Brittle[i] += o.Brittle[i]; BrittleUp[i] += o.BrittleUp[i]; FoeTickExtraHp[i] += o.FoeTickExtraHp[i];
                SoloNom[i] += o.SoloNom[i]; SoloHp[i] += o.SoloHp[i];
            }
            SurgeHotaAtk += o.SurgeHotaAtk; SurgeHotaOver += o.SurgeHotaOver; FirstHitN += o.FirstHitN; FirstHitSum += o.FirstHitSum; FirstHitBurnN += o.FirstHitBurnN;
            HotaHitN += o.HotaHitN; HotaHitSum += o.HotaHitSum; FirstHitMax = Math.Max(FirstHitMax, o.FirstHitMax);
            SurgeHiyoHoard += o.SurgeHiyoHoard; SurgeHiyoCapped += o.SurgeHiyoCapped;
            FoeSpreads += o.FoeSpreads; FoeLv4Deaths += o.FoeLv4Deaths; BlazeKills += o.BlazeKills; KillsSame += o.KillsSame; KillsNext += o.KillsNext;
            SurgeFoe += o.SurgeFoe; SurgeFoeSteps += o.SurgeFoeSteps; SurgeFoeTo4 += o.SurgeFoeTo4; SurgeAlly += o.SurgeAlly; SurgeAllySteps += o.SurgeAllySteps; SurgeAllyTo4 += o.SurgeAllyTo4; SurgeAllyOver += o.SurgeAllyOver;
            foreach (var (k, v) in o.SoloById)
            {
                if (!SoloById.TryGetValue(k, out var r)) SoloById[k] = r = new long[4];
                for (int i = 0; i < 4; i++) r[i] += v[i];
            }
            SoloKills += o.SoloKills; AllyTickDmg += o.AllyTickDmg; AllyTickExtraDmg += o.AllyTickExtraDmg; AllyTickDeaths += o.AllyTickDeaths; AllyTickExtraDeaths += o.AllyTickExtraDeaths;
            AllyTickHeal += o.AllyTickHeal; AllyTickExtraHeal += o.AllyTickExtraHeal;
            for (int t = 0; t <= TM; t++) { Lv4Sum[t] += o.Lv4Sum[t]; Lv4Cnt[t] += o.Lv4Cnt[t]; Lv4Ge3[t] += o.Lv4Ge3[t]; Lv4Ge2[t] += o.Lv4Ge2[t]; }
            After1Sum += o.After1Sum; After1N += o.After1N; After2Sum += o.After2Sum; After2N += o.After2N;
        }

        static bool FL(BattleEvent x, string label) => x.Kind == BattleEventKind.FireLevel && x.Text == label;

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, bool target)
        {
            N++;
            var ev = r.Events;
            int n = ev.Count;
            if (target) while (n > 0 && ev[n - 1].Turn > TM) n--;
            var foes = new HashSet<int>(e.Select(u => u.InstanceId));
            foreach (var x in ev)
                if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && x.Team is int tm && tm != BattleContext.PlayerTeam) foes.Add(sid);
            int? Id(string s) => p.FirstOrDefault(u => u.Def.Id == s)?.InstanceId;
            int? borg = Id("borg"), hota = Id("hota"), hiyo = Id("hiyo");
            var led = r.FireLevels;
            if (borg is not null) BorgN++;
            if (hota is not null) HotaN++;
            if (hiyo is not null) HiyoN++;
            int HandEnd(int i, int? actor)
            {
                int end = n;
                foreach (var h in r.Hands) if ((actor is null || h.ActorId == actor) && h.EventStart <= i && i < h.EventEnd) end = Math.Min(end, h.EventEnd);
                return end;
            }

            // ---- 爆炎ごと ----
            var blazeTurns = new List<int>();
            var hotaLog = led?.BlazeAllyLog.Where(x => x.Id == hota).ToList() ?? new();
            var hiyoLog = led?.BlazeAllyLog.Where(x => x.Id == hiyo).ToList() ?? new();
            int hk = 0, yk = 0;
            for (int i = 0; i < n; i++)
            {
                var x = ev[i];
                if (FL(x, FireLevelLabels.BlazeSolo) && x.ActorId == borg) SoloBlazes++;
                if (!FL(x, FireLevelLabels.Blaze) || x.ActorId != borg) continue;
                Blazes++;
                blazeTurns.Add(x.Turn);
                int he = HandEnd(i, borg);
                // 爆炎の瞬間のホタ・ヒヨの火勢（帳簿・爆炎の順に1件ずつ）
                (int Turn, int Id, int Pre, int Post)? hl = null;
                while (hk < hotaLog.Count && hotaLog[hk].Turn < x.Turn) hk++;
                if (hk < hotaLog.Count && hotaLog[hk].Turn == x.Turn) { hl = hotaLog[hk]; hk++; }
                while (yk < hiyoLog.Count && hiyoLog[yk].Turn < x.Turn) yk++;
                if (yk < hiyoLog.Count && hiyoLog[yk].Turn == x.Turn) { HiyoPre[hiyoLog[yk].Pre]++; HiyoPost[hiyoLog[yk].Post]++; yk++; }
                if (hl is { } h0)
                {
                    BlazeHotaAlive++; HotaPre[h0.Pre]++; HotaPost[h0.Post]++;
                    // 同じギフトの中でホタの手番が続くか（次の「手番の頭」より前にホタへの「ターンギフト」）
                    for (int j = he; j < n; j++)
                    {
                        var y = ev[j];
                        if (y.Kind == BattleEventKind.TurnStart) break;
                        if (FL(y, FireLevelLabels.GiftTurn) && y.TargetId == hota) { BlazeHotaNext++; HotaNextPre[h0.Pre]++; HotaNextPost[h0.Post]++; break; }
                        if (FL(y, FireLevelLabels.GiftTurn)) break;
                    }
                }
                // 爆炎の上げでホタ・ヒヨに入ったあぶれた火——その駒の「爆炎・味方」から「爆炎・上げ」までのあいだ（呼び火・燃え広がりの分は数えない）
                {
                    long hoAtk = 0, hoN = 0, hyN = 0; bool inHo = false, inHy = false;
                    for (int j = i + 1; j < he; j++)
                    {
                        var y = ev[j];
                        if (y.Kind == BattleEventKind.FireArmor && y.Text == FireArmorLabels.BlazeAlly) { if (y.TargetId == hota) inHo = true; if (y.TargetId == hiyo) inHy = true; }
                        if (inHo && FL(y, FireLevelLabels.Overflow) && y.TargetId == hota && y.ActorId == borg) { hoAtk += y.Amount; hoN++; }
                        if (inHy && FL(y, FireLevelLabels.GiftHoardAdd) && y.TargetId == hiyo && y.ActorId == borg) hyN++;
                        if (FL(y, FireLevelLabels.BlazeSurge) && y.TargetId == hota && inHo) { SurgeHotaAtk += hoAtk; SurgeHotaOver += hoN; inHo = false; hoAtk = hoN = 0; }
                        if (FL(y, FireLevelLabels.BlazeSurge) && y.TargetId == hiyo && inHy) { SurgeHiyoHoard += hyN; inHy = false; hyN = 0; }
                    }
                }
                // 爆炎の後のホタの最初の1発（攻撃の Amount）——その戦の中で
                if (hota is int ho)
                    for (int j = he; j < n; j++)
                    {
                        var y = ev[j];
                        if (y.Kind == BattleEventKind.Attack && y.ActorId == ho)
                        {
                            FirstHitN++; FirstHitSum += y.Amount; FirstHitMax = Math.Max(FirstHitMax, y.Amount);
                            int hh = HandEnd(j, ho);
                            for (int k = j; k >= 0 && k >= j - 400; k--) { if (FL(ev[k], FireLevelLabels.Burnout) && ev[k].ActorId == ho) { if (HandEnd(k, ho) == hh) FirstHitBurnN++; break; } if (ev[k].Kind == BattleEventKind.TurnStart) break; }
                            break;
                        }
                    }
                // 爆炎の後に倒れた敵（爆炎の手番の後・同じ周回 ／ 次の周回）
                for (int j = he; j < n; j++)
                {
                    var y = ev[j];
                    if (y.Kind != BattleEventKind.Death || y.TargetId is not int dt || !foes.Contains(dt)) continue;
                    if (y.Turn == x.Turn) KillsSame++; else if (y.Turn == x.Turn + 1) KillsNext++;
                    else if (y.Turn > x.Turn + 1) break;
                }
                // 爆炎の一撃で倒した敵（味方への燃焼ダメージが始まるまで）
                for (int j = i + 1; j < he; j++)
                {
                    var y = ev[j];
                    if (y.Kind == BattleEventKind.FireArmor && y.Text == FireArmorLabels.BlazeAlly) break;
                    if (y.Kind == BattleEventKind.Death && y.TargetId is int dt && foes.Contains(dt)) BlazeKills++;
                }
            }
            if (hota is int hoa) foreach (var x in ev.Take(n)) if (x.Kind == BattleEventKind.Attack && x.ActorId == hoa) { HotaHitN++; HotaHitSum += x.Amount; }

            if (led is not null)
            {
                FoeSpreads += led.FoeSpreads; FoeLv4Deaths += led.FoeLv4Deaths;
                for (int i = 0; i < 5; i++)
                {
                    FoePre[i] += led.BlazeFoeLvPre[i]; FoePost[i] += led.BlazeFoeLvPost[i]; AllyPre[i] += led.BlazeAllyLvPre[i]; AllyPost[i] += led.BlazeAllyLvPost[i];
                    Brittle[i] += led.FoeBrittle[i]; BrittleUp[i] += led.FoeBrittleUp[i]; FoeTickExtraHp[i] += led.FoeTickExtraHp[i];
                    SoloNom[i] += led.BlazeSoloNom[i]; SoloHp[i] += led.BlazeSoloHp[i];
                    AllyTickDmg += led.AllyTickDmg[i]; AllyTickExtraDmg += led.AllyTickExtraDmg[i]; AllyTickHeal += led.AllyTickHeal[i]; AllyTickExtraHeal += led.AllyTickExtraHeal[i];
                }
                SurgeFoe += led.SurgeFoe; SurgeFoeSteps += led.SurgeFoeSteps; SurgeFoeTo4 += led.SurgeFoeTo4;
                SurgeAlly += led.SurgeAlly; SurgeAllySteps += led.SurgeAllySteps; SurgeAllyTo4 += led.SurgeAllyTo4; SurgeAllyOver += led.SurgeAllyOver;
                foreach (var (k, v) in led.BlazeSoloById)
                {
                    if (!SoloById.TryGetValue(k, out var rr)) SoloById[k] = rr = new long[4];
                    for (int i = 0; i < 4; i++) rr[i] += v[i];
                }
                SoloKills += led.BlazeSoloAllyKills; AllyTickDeaths += led.AllyTickDeaths; AllyTickExtraDeaths += led.AllyTickExtraDeaths;

                // 表H: 周回の頭の写し（生きている味方・火勢4 の数）
                var byTurn = new Dictionary<int, int>();
                foreach (var s in led.Snaps)
                {
                    if (s.Team != BattleContext.PlayerTeam || s.Turn < 1) continue;
                    byTurn[s.Turn] = byTurn.GetValueOrDefault(s.Turn) + (s.Level >= 4 ? 1 : 0);
                }
                foreach (var (t, c) in byTurn)
                {
                    if (t > TM) continue;
                    Lv4Sum[t] += c; Lv4Cnt[t]++;
                    if (c >= 2) Lv4Ge2[t]++;
                    if (c >= 3) Lv4Ge3[t]++;
                }
                foreach (int bt in blazeTurns)
                {
                    if (byTurn.TryGetValue(bt + 1, out var c1)) { After1Sum += c1; After1N++; }
                    if (byTurn.TryGetValue(bt + 2, out var c2)) { After2Sum += c2; After2N++; }
                }
            }
        }
    }

    internal static (FK.Cell Cell, SAgg S, GO.OAgg O) Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var cs = new FC.CAgg[seeds]; var ks = new FF.KAgg[seeds]; var ms = new FK.MAgg[seeds]; var ss = new SAgg[seeds]; var os = new GO.OAgg[seeds];
        bool target = FK.IsTarget(w);
        Parallel.For(0, seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = FC.WaveOf(w, sc)();
            var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
            var r = BattleEngine.Run(p, e, seed0 + i, verbose: true, ember: EmberRule.Pre256);
            var slot0 = slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value);
            var c = new FC.CAgg(); c.Take(r, p, e, target); cs[i] = c;
            var k = new FF.KAgg(); k.Take(r, p, e, target, slot0); ks[i] = k;
            var m = new FK.MAgg(); m.Take(r, p, e, target); ms[i] = m;
            var s = new SAgg(); s.Take(r, p, e, target); ss[i] = s;
            var o = new GO.OAgg(); o.Take(r, p, e, target); os[i] = o;
        });
        var C = new FC.CAgg(); var K = new FF.KAgg(); var M = new FK.MAgg(); var S = new SAgg(); var O = new GO.OAgg();
        for (int i = 0; i < seeds; i++) { C.Merge(cs[i]); K.Merge(ks[i]); M.Merge(ms[i]); S.Merge(ss[i]); O.Merge(os[i]); }
        return (new FK.Cell(C, K, M), S, O);
    }

    static SAgg SG(string b, string v, (int W, int S)[] cells) { var m = new SAgg(); foreach (var (w, s) in cells) m.Merge(SAt(b, v, w, s)); return m; }
    static GO.OAgg OG(string b, string v, (int W, int S)[] cells) { var m = new GO.OAgg(); foreach (var (w, s) in cells) m.Merge(_o[(b, v, w, FK.IsTarget(w) ? 0 : s)]); return m; }
    static FC.CAgg CG(string b, string v, (int W, int S)[] cells) { var m = new FC.CAgg(); foreach (var (w, s) in cells) m.Merge(FK.At(b, v, w, s).C); return m; }
    static IEnumerable<(string B, string V)> Rows() { foreach (string b in FK.BoardNames) foreach (var v in _vers) yield return (b, v.Name); }
    static string Per(long a, long n) => FK.Per(a, n);
    static string Pct(long a, long n) => FK.Pct(a, n);
    static string Avg(long a, long n) => FK.Avg(a, n);
    static string Dist(long[] h) { long t = h.Sum(); return t == 0 ? "—" : string.Join(" ／ ", Enumerable.Range(0, 5).Select(i => Pct(h[i], t))); }

    // ---------------------------------------------------------------------------------
    // 表
    // ---------------------------------------------------------------------------------
    static void TableQ02()
    {
        Console.WriteLine("## 表Q —— 爆炎の瞬間のホタ ／ ヒヨの火勢と、同じギフトの中のホタの手番（Q0-2）");
        Console.WriteLine();
        Console.WriteLine("火勢は爆炎の味方の側（着火と燃焼ダメージの後）の帳簿で、上げる前 → 上げた後（V0 は同じ）。分布は 0 ／ 1 ／ 2 ／ 3 ／ 4 の %。「次がホタ」は爆炎の手番の後、次の周回の頭より前に同じギフトでホタの手番が来た爆炎。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 爆炎/戦 | ホタが生きていた % | 次がホタ % | ホタ 前 | ホタ 後 | 次がホタの爆炎: 前 → 後で 4 % | ヒヨ 前 | ヒヨ 後 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|---|---|---|---|---|");
        foreach (var (b, v) in Rows())
            foreach (var (gn, cells) in Groups)
            {
                var m = SG(b, v, cells);
                if (m.Blazes == 0) { Console.WriteLine($"| {b} | {v} | {gn} | 0.00 | — | — | — | — | — | — | — |"); continue; }
                long np = m.HotaNextPre.Sum();
                Console.WriteLine($"| {b} | {v} | {gn} | {Per(m.Blazes, m.N)} | {Pct(m.BlazeHotaAlive, m.Blazes)} | {Pct(m.BlazeHotaNext, m.Blazes)} | {Dist(m.HotaPre)} | {Dist(m.HotaPost)} | {Pct(m.HotaNextPre[4], np)} → {Pct(m.HotaNextPost[4], np)} | {Dist(m.HiyoPre)} | {Dist(m.HiyoPost)} |");
            }
        Console.WriteLine();
    }

    static void TableB()
    {
        Console.WriteLine("## 表B —— ホタの焼き尽くす（/戦）と、爆炎の直後（同じギフトで先に動いたボルグが爆炎）に撃った回数（/戦・与ダメ/回・倒した/回）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | " + string.Join(" | ", Groups.Select(g => g.Name)) + " |");
        Console.WriteLine("|---|---|" + string.Concat(Enumerable.Repeat("---|", Groups.Length)));
        foreach (var (b, v) in Rows())
            Console.WriteLine($"| {b} | {v} | " + string.Join(" | ", Groups.Select(g =>
            {
                var mm = FK.Grp(b, v, g.Cells); var o = OG(b, v, g.Cells);
                return $"{Per(mm.Burnouts, mm.N)} ／ 直後 **{Per(o.BurnAfter, o.N)}**（{Per(o.DmgAfter, o.BurnAfter)}・{Per(o.KillsAfter, o.BurnAfter)}）";
            })) + " |");
        Console.WriteLine();
    }

    static void TableC()
    {
        Console.WriteLine("## 表C —— 爆炎の上げでホタに入った攻撃力（あぶれた火・/戦 ／ /爆炎）と、爆炎の後のホタの最初の1発（攻撃の値・焼き尽くすだった %）");
        Console.WriteLine();
        Console.WriteLine("「1発の平均」はその戦のホタの攻撃すべての攻撃の値（`Attack` の `Amount`）の平均。最初の1発はその戦の中で爆炎の手番の後に初めて振った攻撃。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 爆炎/戦 | 上げのあぶれた火 回/戦（攻撃力/戦 ／ /爆炎） | 爆炎の後の最初の1発 平均（最大）| うち焼き尽くす % | ホタの1発の平均 |");
        Console.WriteLine("|---|---|---|--:|---|---|--:|--:|");
        foreach (var (b, v) in Rows())
            foreach (var (gn, cells) in Groups)
            {
                var m = SG(b, v, cells);
                Console.WriteLine($"| {b} | {v} | {gn} | {Per(m.Blazes, m.N)} | {Per(m.SurgeHotaOver, m.N)}（{Per(m.SurgeHotaAtk, m.N)} ／ {Per(m.SurgeHotaAtk, m.Blazes)}） | {Per(m.FirstHitSum, m.FirstHitN)}（{m.FirstHitMax}） | {Pct(m.FirstHitBurnN, m.FirstHitN)} | {Per(m.HotaHitSum, m.HotaHitN)} |");
            }
        Console.WriteLine();
    }

    static void TableD()
    {
        Console.WriteLine("## 表D —— ヒヨのギフトの間隔（回/戦・間隔 1 ／ 2 ／ 3 ／ 4以上 の %）と、爆炎の上げで溜まった渡す火（/戦）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | ギフト/戦 | 間隔 1 ／ 2 ／ 3 ／ 4+ % | 上げで溜まった渡す火/戦（/爆炎） | 渡す火の溜め 全体/戦（捨てた） | 渡す火 上げた ／ 4 に届いた/戦 |");
        Console.WriteLine("|---|---|---|--:|---|---|---|---|");
        foreach (var (b, v) in Rows())
            foreach (var (gn, cells) in Groups)
            {
                var m = SG(b, v, cells); var c = CG(b, v, cells); var mm = FK.Grp(b, v, cells);
                if (m.HiyoN == 0) continue;
                long iv = c.Interval.Sum();
                Console.WriteLine($"| {b} | {v} | {gn} | {Per(c.Gifts, c.N)} | {string.Join(" ／ ", Enumerable.Range(1, 4).Select(i => Pct(c.Interval[i], iv)))} | {Per(m.SurgeHiyoHoard, m.N)}（{Per(m.SurgeHiyoHoard, m.Blazes)}） | {Per(mm.GiftHoardAdds, mm.N)}（{Per(mm.GiftHoardCapped, mm.N)}） | {Per(mm.GiftHoardEv, mm.N)} ／ {Per(mm.GiftHoardTo4, mm.N)} |");
            }
        Console.WriteLine();
    }

    static void TableE()
    {
        Console.WriteLine("## 表E —— 延焼（/戦・火勢4 で倒れた敵/戦）と、爆炎で倒した敵 ／ 爆炎の後に倒れた敵（同じ周回 ／ 次の周回・/爆炎）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 延焼/戦 | 火勢4 で倒れた敵/戦 | 爆炎/戦 | 爆炎の一撃で倒した/回 | 爆炎の後・同じ周回/回 | 次の周回/回 | 決着T |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|");
        foreach (var (b, v) in Rows())
            foreach (var (gn, cells) in Groups)
            {
                var m = SG(b, v, cells); var c = CG(b, v, cells);
                Console.WriteLine($"| {b} | {v} | {gn} | {Per(m.FoeSpreads, m.N)} | {Per(m.FoeLv4Deaths, m.N)} | {Per(m.Blazes, m.N)} | {Per(m.BlazeKills, m.Blazes)} | {Per(m.KillsSame, m.Blazes)} | {Per(m.KillsNext, m.Blazes)} | {Per(c.Turns, c.N)} |");
            }
        Console.WriteLine();
    }

    static void TableF()
    {
        Console.WriteLine("## 表F —— 爆炎の瞬間の敵の火勢（爆炎の一撃の後に生きていた敵・上げる前 → 後・0〜4 の %）と、上げの帳簿・脆さの上乗せの名目（/戦）");
        Console.WriteLine();
        Console.WriteLine("脆さの名目は敵の脆さの上乗せ（`FoeBrittle`・火勢 1〜4 の和）と、うち 25% を超えた分（`FoeBrittleUp`）。刻みの追加は敵の燃焼の刻みの2回目以降で削った HP（火勢 2〜4 の和）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 生き残った敵/爆炎 | 敵 前 | 敵 後 | 上げた敵/爆炎（段/体 ・4 に届いた %） | 上げた味方/爆炎（段/体・4 に届いた %・あぶれた/爆炎） | 脆さの名目/戦（25% を超えた分） | 敵の刻みの追加/戦 |");
        Console.WriteLine("|---|---|---|--:|---|---|---|---|---|--:|");
        foreach (var (b, v) in Rows())
            foreach (var (gn, cells) in Groups)
            {
                var m = SG(b, v, cells);
                Console.WriteLine($"| {b} | {v} | {gn} | {Per(m.FoePre.Sum(), m.Blazes)} | {Dist(m.FoePre)} | {Dist(m.FoePost)} | {Per(m.SurgeFoe, m.Blazes)}（{Per(m.SurgeFoeSteps, m.SurgeFoe)}・{Pct(m.SurgeFoeTo4, m.SurgeFoe)}） | {Per(m.SurgeAlly, m.Blazes)}（{Per(m.SurgeAllySteps, m.SurgeAlly)}・{Pct(m.SurgeAllyTo4, m.SurgeAlly)}・{Per(m.SurgeAllyOver, m.Blazes)}） | {Per(m.Brittle.Sum(), m.N)}（{Per(m.BrittleUp.Sum(), m.N)}） | {Per(m.FoeTickExtraHp.Sum(), m.N)} |");
            }
        Console.WriteLine();
    }

    static readonly string[] SoloKinds = { "火の癒し", "火の変換", "反転", "受けた", "焼かれない" };
    static void TableG()
    {
        Console.WriteLine("## 表G —— 爆炎・独り（雷＋ボルグ）: 味方への燃焼ダメージの行き先と、味方の燃焼の刻み");
        Console.WriteLine();
        Console.WriteLine("行き先は名目/戦（火の癒し ／ 火の変換 ／ 反転 ／ 受けた ／ 焼かれない）と、受けた駒ごとの 名目 ／ 癒えた ／ 削られた ／ 倒れた（/戦）。刻みは味方の燃焼の刻みで削られた HP（1回目 ／ 2回目以降）・癒えた HP・倒れた（/戦）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 独りの爆炎/戦 | 名目の行き先/戦 | 駒ごと（名目 ／ 癒えた ／ 削られた ／ 倒れた） | 爆炎で倒れた味方/戦 | 刻み 削られた（1回目 ／ 追加）・癒えた・倒れた /戦 |");
        Console.WriteLine("|---|---|---|--:|---|---|--:|---|");
        foreach (var v in _vers)
            foreach (var (gn, cells) in Groups)
            {
                const string b = "雷＋ボルグ";
                if (!FK.BoardNames.Contains(b)) continue;
                var m = SG(b, v.Name, cells);
                string units = string.Join("・", m.SoloById.OrderByDescending(kv => kv.Value[0]).Select(kv => $"{kv.Key} {Per(kv.Value[0], m.N)} ／ {Per(kv.Value[1], m.N)} ／ {Per(kv.Value[2], m.N)} ／ {Per(kv.Value[3], m.N)}"));
                Console.WriteLine($"| {b} | {v.Name} | {gn} | {Per(m.SoloBlazes, m.N)} | {string.Join(" ／ ", Enumerable.Range(0, 5).Select(i => Per(m.SoloNom[i], m.N)))} | {(units == "" ? "—" : units)} | {Per(m.SoloKills, m.N)} | {Per(m.AllyTickDmg - m.AllyTickExtraDmg, m.N)} ／ {Per(m.AllyTickExtraDmg, m.N)}・{Per(m.AllyTickHeal, m.N)}・{Per(m.AllyTickDeaths, m.N)} |");
            }
        Console.WriteLine();
    }

    static void TableH()
    {
        Console.WriteLine("## 表H —— 周回の頭に同時に火勢4 の味方の数（生きている味方の平均・括弧は 3 体以上の %）と、爆炎の次の周回 ／ 次の次の周回の平均");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | T1 | T2 | T3 | T4 | T5 | T6 | T7 | T8 | 爆炎の次 ／ 次の次 |");
        Console.WriteLine("|---|---|---|--:|--:|--:|--:|--:|--:|--:|--:|---|");
        foreach (var (b, v) in Rows())
            foreach (var (gn, cells) in Groups)
            {
                var m = SG(b, v, cells);
                string lv = string.Join(" | ", Enumerable.Range(1, TM).Select(t => m.Lv4Cnt[t] == 0 ? "—" : $"{(double)m.Lv4Sum[t] / m.Lv4Cnt[t]:F2}（{Pct(m.Lv4Ge3[t], m.Lv4Cnt[t])}）"));
                Console.WriteLine($"| {b} | {v} | {gn} | {lv} | {Avg(m.After1Sum, m.After1N)} ／ {Avg(m.After2Sum, m.After2N)} |");
            }
        Console.WriteLine();
    }
}
