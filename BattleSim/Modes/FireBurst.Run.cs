using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FS = FireScaleDiag;

// fireburst run —— 段2 ＋ 表A〜J（指示書 §6.3）と、`compare` 61 行の版ごとの動き（受け入れ 1・2）。
static partial class FireBurstDiag
{
    internal const int TM = 7;

    /// <summary>1つのセル（台 × 版 × 波 × 倍率）の集計。</summary>
    internal sealed class LAgg
    {
        public long N, Wins, AllSurv, Turns, Fell;
        public readonly long[] FellRole = new long[4], DealtRole = new long[4], BrittleRole = new long[4];
        public readonly BA.Agg Death = new();
        // 表B（ギフト）
        public long HiyoBattles, GiftT2, GiftBattles, Gifts, Recips, Gifts1, Gifts2, GiftTurns, GiftStalled, GiftHush;
        public readonly long[] FirstGift = new long[TM + 1], RecipRole = new long[4], RecipLevel = new long[5], StokeRole = new long[4];
        // 表C（大技）
        public long Unleash, Burnout, Embers, EmbersExtra, RainDrops, CallFires, CallGrowth, MoveBattles, Multi;
        public readonly long[] FirstMove = new long[TM + 1];
        public readonly long[,] MoveTurn = new long[4, TM + 1];
        // 表D（火勢）
        public long Stokes, Spreads, SpreadGrowth, SpreadCapped, SelfGrowth, SelfCapped, Wilts, Outs;
        public readonly long[,] LvSum = new long[4, TM + 1], LvCnt = new long[4, TM + 1], Lv4 = new long[4, TM + 1];
        public readonly long[,] LvHist = new long[4, 5];
        public readonly long[] Simul4 = new long[TM + 1], SimulCnt = new long[TM + 1];
        // 表E（ヒヨの手番の中身）
        public long HiyoHands, HiyoStoke, HiyoGift1, HiyoGift2, HiyoOther;
        // 表F（火力）
        public readonly long[] Reached = new long[TM + 1];
        public readonly Dictionary<string, long[]> OutSum = new(), OutMax = new();
        public readonly Dictionary<string, long> HitMax = new(), HitSum = new(), HitN = new();
        public readonly long[] HotaSrc = new long[4], HotaSrcMax = new long[4], BorgSrc = new long[2], BorgSrcMax = new long[2];
        // 表G（脆さ）
        public long TrioDealt, TrioBrittle, AllDealt, AllBrittle;
        // 表H（絵）
        public readonly Dictionary<string, FS.PicAgg> Pics = new();
        // 表J（火の雨）: 焼き尽くすごと（前半 ＝ 最初の雨の時点で生きている敵 4 体以上 ／ 終盤 ＝ 3 体以下）
        public readonly long[] JBurn = new long[2], JDrops = new long[2], JDistinct = new long[2], JTop = new long[2], JKills = new long[2], JDmg = new long[2];

        public void Merge(LAgg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; Turns += o.Turns; Fell += o.Fell;
            for (int i = 0; i < 4; i++)
            {
                FellRole[i] += o.FellRole[i]; DealtRole[i] += o.DealtRole[i]; BrittleRole[i] += o.BrittleRole[i]; RecipRole[i] += o.RecipRole[i]; StokeRole[i] += o.StokeRole[i];
                HotaSrc[i] += o.HotaSrc[i]; HotaSrcMax[i] = Math.Max(HotaSrcMax[i], o.HotaSrcMax[i]);
            }
            for (int i = 0; i < 2; i++)
            {
                BorgSrc[i] += o.BorgSrc[i]; BorgSrcMax[i] = Math.Max(BorgSrcMax[i], o.BorgSrcMax[i]);
                JBurn[i] += o.JBurn[i]; JDrops[i] += o.JDrops[i]; JDistinct[i] += o.JDistinct[i]; JTop[i] += o.JTop[i]; JKills[i] += o.JKills[i]; JDmg[i] += o.JDmg[i];
            }
            Death.Merge(o.Death);
            HiyoBattles += o.HiyoBattles; GiftT2 += o.GiftT2; GiftBattles += o.GiftBattles; Gifts += o.Gifts; Recips += o.Recips; Gifts1 += o.Gifts1; Gifts2 += o.Gifts2;
            GiftTurns += o.GiftTurns; GiftStalled += o.GiftStalled; GiftHush += o.GiftHush;
            Unleash += o.Unleash; Burnout += o.Burnout; Embers += o.Embers; EmbersExtra += o.EmbersExtra; RainDrops += o.RainDrops; CallFires += o.CallFires; CallGrowth += o.CallGrowth;
            MoveBattles += o.MoveBattles; Multi += o.Multi;
            Stokes += o.Stokes; Spreads += o.Spreads; SpreadGrowth += o.SpreadGrowth; SpreadCapped += o.SpreadCapped; SelfGrowth += o.SelfGrowth; SelfCapped += o.SelfCapped;
            Wilts += o.Wilts; Outs += o.Outs;
            HiyoHands += o.HiyoHands; HiyoStoke += o.HiyoStoke; HiyoGift1 += o.HiyoGift1; HiyoGift2 += o.HiyoGift2; HiyoOther += o.HiyoOther;
            TrioDealt += o.TrioDealt; TrioBrittle += o.TrioBrittle; AllDealt += o.AllDealt; AllBrittle += o.AllBrittle;
            for (int t = 0; t <= TM; t++)
            {
                FirstGift[t] += o.FirstGift[t]; FirstMove[t] += o.FirstMove[t]; Simul4[t] += o.Simul4[t]; SimulCnt[t] += o.SimulCnt[t]; Reached[t] += o.Reached[t];
                for (int r = 0; r < 4; r++) { LvSum[r, t] += o.LvSum[r, t]; LvCnt[r, t] += o.LvCnt[r, t]; Lv4[r, t] += o.Lv4[r, t]; MoveTurn[r, t] += o.MoveTurn[r, t]; }
            }
            for (int r = 0; r < 4; r++) for (int l = 0; l < 5; l++) LvHist[r, l] += o.LvHist[r, l];
            for (int l = 0; l < 5; l++) RecipLevel[l] += o.RecipLevel[l];
            foreach (var (k, v) in o.OutSum) { if (!OutSum.TryGetValue(k, out var a)) OutSum[k] = a = new long[TM + 1]; for (int t = 0; t <= TM; t++) a[t] += v[t]; }
            foreach (var (k, v) in o.OutMax) { if (!OutMax.TryGetValue(k, out var a)) OutMax[k] = a = new long[TM + 1]; for (int t = 0; t <= TM; t++) a[t] = Math.Max(a[t], v[t]); }
            foreach (var (k, v) in o.HitMax) HitMax[k] = Math.Max(HitMax.GetValueOrDefault(k), v);
            foreach (var (k, v) in o.HitSum) HitSum[k] = HitSum.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.HitN) HitN[k] = HitN.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.Pics) { if (!Pics.TryGetValue(k, out var x)) Pics[k] = x = new FS.PicAgg(); x.Merge(v); }
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, Dictionary<int, int> slot0)
        {
            N++; Turns += r.Turns;
            if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
            Fell += r.PlayerStarterFallen.Count;
            foreach (string id in r.PlayerStarterFallen) FellRole[RoleOf(id)]++;
            Death.Take(r, p, e, slot0);
            var ev = r.Events;
            var info = new Dictionary<int, (string Id, int Team)>();
            foreach (var u in p.Concat(e)) info[u.InstanceId] = (u.Def.Id, u.TeamId);
            foreach (var x in ev)
                if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && !info.ContainsKey(sid))
                    info[sid] = ("summon", x.Team ?? BattleContext.EnemyTeam);
            bool Mine(int id) => info.TryGetValue(id, out var i) && i.Team == BattleContext.PlayerTeam;
            bool Foe(int id) => info.TryGetValue(id, out var i) && i.Team != BattleContext.PlayerTeam;
            int? hiyo = p.FirstOrDefault(u => u.Def.Id == "hiyo")?.InstanceId, hota = p.FirstOrDefault(u => u.Def.Id == "hota")?.InstanceId, borg = p.FirstOrDefault(u => u.Def.Id == "borg")?.InstanceId;
            for (int t = 1; t <= TM; t++) if (r.Turns >= t) Reached[t]++;

            if (r.FireLevels is FireLevelLedger fl)
            {
                var simul = new long[TM + 1]; var seen = new bool[TM + 1];
                foreach (var s in fl.Snaps)
                {
                    if (s.Team != BattleContext.PlayerTeam || !info.TryGetValue(s.Id, out var ui) || ui.Id == "summon") continue;
                    int ro = RoleOf(ui.Id), t = Math.Min(TM, s.Turn);
                    LvSum[ro, t] += s.Level; LvCnt[ro, t]++; if (s.Level >= 4) { Lv4[ro, t]++; simul[t]++; }
                    LvHist[ro, s.Level]++;
                    seen[t] = true;
                }
                for (int t = 1; t <= TM; t++) if (seen[t]) { Simul4[t] += simul[t]; SimulCnt[t]++; }
                Gifts += fl.Gifts; Recips += fl.GiftRecipients; GiftTurns += fl.GiftTurns; GiftStalled += fl.GiftStalled; GiftHush += fl.GiftHushTurns;
                Stokes += fl.Stokes; Spreads += fl.SpreadHits; SpreadGrowth += fl.SpreadGrowth; SpreadCapped += fl.SpreadCapped; SelfGrowth += fl.SelfGrowth; SelfCapped += fl.SelfCapped;
                Wilts += fl.Wilts; Outs += fl.Outs;
                Unleash += fl.Unleashes; Burnout += fl.Burnouts; Embers += fl.Embers; EmbersExtra += fl.EmbersExtra; RainDrops += fl.RainDrops; CallFires += fl.CallFires; CallGrowth += fl.CallGrowth;
                if (fl.Gifts > 0) GiftBattles++;
                if (fl.FirstGiftTurn is > 0 and <= 2) GiftT2++;
                if (fl.FirstGiftTurn > 0) FirstGift[Math.Min(TM, fl.FirstGiftTurn)]++;
                var big = fl.Moves.Where(m => m.Kind is 1 or 2).ToList();
                if (big.Count > 0) { MoveBattles++; FirstMove[Math.Min(TM, big.Min(m => m.Turn))]++; }
                if (big.GroupBy(m => m.Id).Any(g => g.Count() >= 2)) Multi++;
                foreach (var m in fl.Moves) MoveTurn[m.Kind, Math.Min(TM, m.Turn)]++;
            }
            if (hiyo is int hy && r.Hands.Any(h => h.ActorId == hy)) HiyoBattles++;

            // 表B・E（ギフト・煽り・ヒヨの手番）
            var giftN = new Dictionary<(int T, int Giver), int>();
            foreach (var x in ev)
            {
                if (x.Kind != BattleEventKind.FireLevel || x.TargetId is not int gt || !info.TryGetValue(gt, out var gi)) continue;
                if (x.Text == FireLevelLabels.Gift)
                {
                    RecipRole[RoleOf(gi.Id)]++;
                    giftN[(x.Turn, x.ActorId ?? -1)] = Math.Max(giftN.GetValueOrDefault((x.Turn, x.ActorId ?? -1)), x.Slot);
                }
                if (x.Text == FireLevelLabels.GiftTurn) RecipLevel[Math.Clamp(x.Amount, 0, 4)]++;
                if (x.Text == FireLevelLabels.Stoke) StokeRole[RoleOf(gi.Id)]++;
            }
            foreach (var n in giftN.Values) { if (n >= 2) Gifts2++; else Gifts1++; }
            foreach (var h in r.Hands)
            {
                if (h.ActorId != hiyo) continue;
                HiyoHands++;
                int g = 0; bool st = false;
                for (int j = h.EventStart; j < h.EventEnd && j < ev.Count; j++)
                {
                    var y = ev[j];
                    if (y.Kind != BattleEventKind.FireLevel || y.ActorId != hiyo) continue;
                    if (y.Text == FireLevelLabels.Gift) g++;
                    if (y.Text == FireLevelLabels.Stoke) st = true;
                }
                if (g >= 2) HiyoGift2++; else if (g == 1) HiyoGift1++; else if (st) HiyoStoke++; else HiyoOther++;
            }

            // 表F・G・J（与ダメの出どころ・脆さ・火の雨）
            var perTurn = new Dictionary<(string Id, int T), long>();
            int hotaMode = 0, borgMode = 0; bool unleashPending = false;
            // 火の雨: いまの焼き尽くすの集計
            int jPhase = -1, jDrops = 0; var jHits = new Dictionary<int, int>(); int jKills = 0; long jDmg = 0; int rainTarget = -1;
            void CloseJ()
            {
                if (jPhase < 0) return;
                JBurn[jPhase]++; JDrops[jPhase] += jDrops; JDistinct[jPhase] += jHits.Count; JTop[jPhase] += jHits.Count == 0 ? 0 : jHits.Values.Max(); JKills[jPhase] += jKills; JDmg[jPhase] += jDmg;
                jPhase = -1; jDrops = 0; jHits.Clear(); jKills = 0; jDmg = 0;
            }
            foreach (var x in ev)
            {
                if (x.Kind == BattleEventKind.FireLevel && x.TargetId is int ft)
                {
                    if (ft == hota && x.ActorId == hota)
                    {
                        if (x.Text == FireLevelLabels.Stage) { hotaMode = 0; CloseJ(); }
                        else if (x.Text == FireLevelLabels.Burnout) { hotaMode = 1; CloseJ(); }
                        else if (x.Text == FireLevelLabels.Embers) { hotaMode = 3; CloseJ(); }
                    }
                    if (x.Text == FireLevelLabels.Rain && x.ActorId == hota)
                    {
                        hotaMode = 2;
                        if (jPhase < 0) jPhase = x.Amount >= 4 ? 0 : 1;
                        jDrops++; jHits[ft] = jHits.GetValueOrDefault(ft) + 1; rainTarget = ft;
                    }
                    if (x.Text == FireLevelLabels.Unleash && x.ActorId == borg) unleashPending = true;
                    continue;
                }
                if (x.Kind == BattleEventKind.Attack && x.ActorId == borg && borg is not null) { borgMode = unleashPending ? 1 : 0; unleashPending = false; }
                if (x.Kind == BattleEventKind.Damage && x.ActorId is int a && Mine(a) && x.TargetId is int tg && Foe(tg) && !x.Relayed)
                {
                    string id = info[a].Id;
                    int ro = RoleOf(id);
                    perTurn[(id, x.Turn)] = perTurn.GetValueOrDefault((id, x.Turn)) + x.Amount;
                    HitMax[id] = Math.Max(HitMax.GetValueOrDefault(id), x.Amount);
                    HitSum[id] = HitSum.GetValueOrDefault(id) + x.Amount; HitN[id] = HitN.GetValueOrDefault(id) + 1;
                    DealtRole[ro] += x.Amount; BrittleRole[ro] += x.BrittleExtra ?? 0;
                    AllDealt += x.Amount; AllBrittle += x.BrittleExtra ?? 0;
                    if (ro < 3) { TrioDealt += x.Amount; TrioBrittle += x.BrittleExtra ?? 0; }
                    if (a == hota) { HotaSrc[hotaMode] += x.Amount; HotaSrcMax[hotaMode] = Math.Max(HotaSrcMax[hotaMode], x.Amount); if (hotaMode == 2) { jDmg += x.Amount; if (x.HpAfter <= 0 && tg == rainTarget) jKills++; } }
                    if (a == borg) { BorgSrc[borgMode] += x.Amount; BorgSrcMax[borgMode] = Math.Max(BorgSrcMax[borgMode], x.Amount); }
                }
            }
            CloseJ();
            foreach (var ((id, t), v) in perTurn)
            {
                if (t < 1) continue;
                int ti = Math.Min(TM, t);
                if (!OutSum.TryGetValue(id, out var s)) OutSum[id] = s = new long[TM + 1];
                if (!OutMax.TryGetValue(id, out var m)) OutMax[id] = m = new long[TM + 1];
                s[ti] += v; m[ti] = Math.Max(m[ti], v);
                s[0] += v; m[0] = Math.Max(m[0], v);
            }

            // 表H（手番の絵・第239期の定義）
            var seqs = new Dictionary<int, List<(string, string)>>();
            foreach (var h in r.Hands.OrderBy(h => h.EventStart))
            {
                if (!info.TryGetValue(h.ActorId, out var ai) || ai.Team != BattleContext.PlayerTeam) continue;
                string pat = "—";
                var tags = new SortedSet<string>(StringComparer.Ordinal);
                for (int j = h.EventStart; j < h.EventEnd && j < ev.Count; j++)
                {
                    var y = ev[j];
                    if (pat == "—" && y.Kind == BattleEventKind.Attack && y.ActorId == h.ActorId && y.Pattern is not null) pat = FS.PatternName(y.Pattern);
                    string? tg = FS.TagOf(y);
                    if (tg is not null) tags.Add(tg);
                    if (y.Kind == BattleEventKind.FireLevel && y.ActorId == h.ActorId && y.Text is FireLevelLabels.Unleash or FireLevelLabels.Burnout or FireLevelLabels.Embers) tags.Add(y.Text);
                }
                string bone = FS.OutcomeName(h.Outcome) + "／" + pat;
                string pic = bone + "／" + (tags.Count == 0 ? "（なし）" : string.Join("・", tags));
                (seqs.TryGetValue(h.ActorId, out var l) ? l : seqs[h.ActorId] = new()).Add((pic, bone));
            }
            foreach (var (id, seq) in seqs)
            {
                string key = info[id].Id;
                if (!Pics.TryGetValue(key, out var pa)) Pics[key] = pa = new FS.PicAgg { Name = key };
                pa.TakeBattle(seq);
            }
        }
    }

    internal static LAgg Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var parts = new LAgg[seeds];
        Parallel.For(0, seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = BA.WaveOf(w, sc)();
            var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
            var r = BattleEngine.Run(p, e, seed0 + i, verbose: true);
            var a = new LAgg();
            a.Take(r, p, e, slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value));
            parts[i] = a;
        });
        var all = new LAgg();
        foreach (var a in parts) all.Merge(a);
        return all;
    }

    // ---------------------------------------------------------------------------------
    // 台（§6.1）
    // ---------------------------------------------------------------------------------
    internal static readonly string[] BoardNames = { "T3", "T3′", "T3-238", "T3-S2R席", "雷＋ボルグ" };
    internal static Formation BoardOf(string name, Ver v, Dictionary<string, List<Pick>> ranked) => name switch
    {
        "T3" => Apply(Dec(ranked[v.Name][0].Best), v),
        "T3′" => Apply(Dec(ranked[v.Name].First(IsT3P).Best), v),
        "T3-238" => Apply(T3238, v),
        "T3-S2R席" => Apply(Dec(ranked["S2R"][0].Best), v),   // S2R の段1 の1位の席を全版に（版の差を同じ席で見る）
        "雷＋ボルグ" => Apply(ThunderBorg, v),
        _ => throw new ArgumentException(name),
    };
    internal static readonly (string Name, Func<Formation> F)[] Refs = { ("参考 移動", () => BA.RefMove), ("参考 雷", () => BA.RefThunder) };

    static partial void RunImpl()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var ranked = new Dictionary<string, List<Pick>>();
        foreach (var v in Versions) ranked[v.Name] = Ranked(PicksOf(v, out _));
        Console.WriteLine("# 第244期 段2 —— 火勢の大技（版 S0〜S2R-g4）");
        Console.WriteLine();
        Stage1Summary(ranked);
        var cells = new Dictionary<(string B, string V, int W, int S), LAgg>();
        foreach (string bn in BoardNames)
            foreach (var v in Versions)
            {
                var f = BoardOf(bn, v, ranked);
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < BA.Scales.Length; s++)
                        cells[(bn, v.Name, w, s)] = Measure(f, w, BA.Scales[s].Sc);
            }
        foreach (var (rn, rf) in Refs)
            for (int w = 0; w < BA.WaveNames.Length; w++)
                for (int s = 0; s < BA.Scales.Length; s++)
                    cells[(rn, "S0", w, s)] = Measure(rf(), w, BA.Scales[s].Sc);
        Console.Error.WriteLine($"  段2 の測定 {sw.Elapsed.TotalSeconds:F0} 秒");
        Boards(ranked);
        TableA(cells);
        TableB(cells);
        TableC(cells);
        TableD(cells);
        TableE(cells);
        TableF(cells);
        TableG(cells);
        TableH(cells);
        TableI(cells);
        TableJ(cells);
        CompareMoves();
        Console.WriteLine($"（所要 {sw.Elapsed.TotalSeconds:F0} 秒）");
    }

    static string Named(string enc) => BA.SeatsNamed(Dec(enc));
    static string Sc(Score4 s) => $"{s.S.Sv}/40・400/300 {s.Sv4}/40・落 {s.S.Fell}・勝 {s.S.W}";

    static void Stage1Summary(Dictionary<string, List<Pick>> ranked)
    {
        Console.WriteLine("## 段1（T3 1,081 組 × 席 120 × seed 1000..1039・九/新兵 × 200/200 → 400/300 → 落 → 決着T）");
        Console.WriteLine();
        Console.WriteLine("| 版 | T3 の1位（相方） | 席 | 得点 | T3′ の1位 | 席 | 得点 | 上位10 の相方（シオ・ササを含む組） | 200/200 で 40/40 の組 | 400/300 も 40/40 の組 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|--:|--:|");
        foreach (var v in Versions)
        {
            var rk = ranked[v.Name];
            var b = rk[0]; var bp = rk.First(IsT3P);
            int prime10 = rk.Take(10).Count(p => !IsT3P(p));
            int full = rk.Count(p => p.BestS.S.Sv == BA.PickSeeds);
            int full4 = rk.Count(p => p.BestS.S.Sv == BA.PickSeeds && p.BestS.Sv4 == BA.PickSeeds);
            Console.WriteLine($"| {v.Name} | {string.Join("・", b.Partners)} | {Named(b.Best)} | {Sc(b.BestS)} | {string.Join("・", bp.Partners)} | {Named(bp.Best)} | {Sc(bp.BestS)} | {prime10} / 10 | {full} | {full4} |");
        }
        Console.WriteLine();
        Console.WriteLine("上位10 組（版ごと・相方 ／ 得点 ／ ボルグの席）:");
        Console.WriteLine();
        foreach (var v in Versions)
            Console.WriteLine($"- {v.Name}: " + string.Join(" ／ ", ranked[v.Name].Take(10).Select(p => $"{string.Join("・", p.Partners)} {p.BestS.S.Sv}/{p.BestS.Sv4}（ボルグ {BorgSeat(p.Best)}）")));
        Console.WriteLine();
    }
    static string BorgSeat(string enc)
    {
        var ids = enc.Split(',');
        int i = Array.IndexOf(ids, "borg");
        return i < 0 ? "—" : FormationRules.SeatNames[i];
    }

    static void Boards(Dictionary<string, List<Pick>> ranked)
    {
        Console.WriteLine("## 台（版ごとの席）");
        Console.WriteLine();
        foreach (var v in Versions)
            foreach (string bn in BoardNames)
                Console.WriteLine($"- {v.Name} × {bn}: {BA.SeatsNamed(BoardOf(bn, v, ranked))}");
        foreach (var (rn, rf) in Refs) Console.WriteLine($"- {rn}（版に依らない）: {BA.SeatsNamed(rf())}");
        Console.WriteLine();
    }

    static IEnumerable<(string B, string V)> BV()
    {
        foreach (string bn in BoardNames) foreach (var v in Versions) yield return (bn, v.Name);
        foreach (var (rn, _) in Refs) yield return (rn, "S0");
    }

    static void TableA(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表A —— 全員生存 ／ 勝率 ／ 決着T ／ 落ちた駒（/戦）");
        Console.WriteLine();
        Console.WriteLine("主判定（九/新兵）の3倍率:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 200/200 | 400/300 | 115/115 |");
        Console.WriteLine("|---|---|---|---|---|");
        foreach (var (b, v) in BV())
            Console.WriteLine($"| {b} | {v} | " + string.Join(" | ", Enumerable.Range(0, 3).Select(s => ACell(cells[(b, v, BA.MainWave, s)]))) + " |");
        Console.WriteLine();
        Console.WriteLine("ほかの波（全員生存 ／ 勝率 ／ 決着T ／ 落）:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | " + string.Join(" | ", Enumerable.Range(0, BA.WaveNames.Length).Where(w => w != BA.MainWave).Select(w => BA.WaveNames[w])) + " | 本編 第2〜5波の平均（全員生存 ／ 勝率） |");
        Console.WriteLine("|---|---|---|" + string.Concat(Enumerable.Repeat("---|", BA.WaveNames.Length)) + "");
        foreach (var (b, v) in BV())
            for (int s = 0; s < 3; s++)
            {
                var ws = Enumerable.Range(0, 4).Select(w => cells[(b, v, w, s)]).ToList();
                string avg = $"{(ws.Average(a => 100.0 * a.AllSurv / a.N)):F1} ／ {(ws.Average(a => 100.0 * a.Wins / a.N)):F1}";
                Console.WriteLine($"| {b} | {v} | {BA.Scales[s].Name} | " + string.Join(" | ", Enumerable.Range(0, BA.WaveNames.Length).Where(w => w != BA.MainWave).Select(w => ACell(cells[(b, v, w, s)]))) + $" | {avg} |");
            }
        Console.WriteLine();
    }
    static string ACell(LAgg a) => $"{Pct(a.AllSurv, a.N)} ／ {Pct(a.Wins, a.N)} ／ {Per(a.Turns, a.N)} ／ {Per(a.Fell, a.N)}";

    static void TableB(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表B —— ターンギフトと煽りの相手");
        Console.WriteLine();
        Console.WriteLine("分母はヒヨが手番を持った戦。「2T まで」＝ 初めてのギフトが周回 1〜2。相手の内訳はギフトの見出しの相手（のべ）。相手の火勢はギフトの手番の頭の値。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | **2T まで %** | 初回 周回1 ／ 2 ／ 3 ／ 4+ % | 出た戦 % | ギフト/戦（1体 ／ 2体） | 相手/戦 | 相手 ボルグ ／ ホタ ／ 相方 % | 相手の火勢 1 ／ 2 ／ 3 ／ 4 % | 煽りの相手 ボルグ ／ ホタ ／ 相方 % | 動けなかった手番 |");
        Console.WriteLine("|---|---|---|---|--:|---|--:|---|--:|---|---|---|--:|");
        foreach (string b in BoardNames.Take(4))
            foreach (var v in Versions)
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                    {
                        if (w != BA.MainWave && s != 1) continue;
                        var a = cells[(b, v.Name, w, s)];
                        long hb = a.HiyoBattles;
                        string first = string.Join(" ／ ", Pct(a.FirstGift[1], hb), Pct(a.FirstGift[2], hb), Pct(a.FirstGift[3], hb), Pct(a.FirstGift.Skip(4).Sum(), hb));
                        long rr = a.RecipRole.Sum(), rl = a.RecipLevel.Sum(), sr = a.StokeRole.Sum();
                        string role = $"{Pct(a.RecipRole[0], rr)} ／ {Pct(a.RecipRole[1], rr)} ／ {Pct(a.RecipRole[3], rr)}";
                        string lv = string.Join(" ／ ", Enumerable.Range(1, 4).Select(l => Pct(a.RecipLevel[l], rl)));
                        string sk = $"{Pct(a.StokeRole[0], sr)} ／ {Pct(a.StokeRole[1], sr)} ／ {Pct(a.StokeRole[3], sr)}";
                        Console.WriteLine($"| {b} | {v.Name} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | **{Pct(a.GiftT2, hb)}** | {first} | {Pct(a.GiftBattles, hb)} | {Per(a.Gifts, a.N)}（{Per(a.Gifts1, a.N)} ／ {Per(a.Gifts2, a.N)}） | {Per(a.Recips, a.N)} | {role} | {lv} | {sk} | {Per(a.GiftStalled, a.N)} |");
                    }
        Console.WriteLine();
    }

    static void TableC(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表C —— 大技（/戦）");
        Console.WriteLine();
        Console.WriteLine("「大技の出た戦」は放つか焼き尽くすが1回以上。「同じ駒が2回以上」は同じ駒が1戦に放つ・焼き尽くすを2回以上撃った戦（育つ → 放つ → また育つ が1周以上回った）。周回ごとは放つ ／ 焼き尽くす ／ 残り火の回数。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | 放つ | 焼き尽くす | 残り火（うち追加） | 火の雨/戦 | 呼び火/戦（育った） | 大技の出た戦 % | 同じ駒が2回以上 % | 初めての大技 周回1 ／ 2 ／ 3 ／ 4 ／ 5+ % | 周回1〜5 の 放つ ／ 焼き尽くす ／ 残り火 |");
        Console.WriteLine("|---|---|---|---|--:|--:|---|--:|---|--:|--:|---|---|");
        foreach (string b in BoardNames)
            foreach (var v in Versions.Skip(2))
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                    {
                        if (w != BA.MainWave && s != 1) continue;
                        var a = cells[(b, v.Name, w, s)];
                        string first = string.Join(" ／ ", Enumerable.Range(1, 4).Select(t => Pct(a.FirstMove[t], a.N)).Append(Pct(a.FirstMove.Skip(5).Sum(), a.N)));
                        string per = string.Join(" ・ ", Enumerable.Range(1, 5).Select(t => $"{Per(a.MoveTurn[1, t], a.N)}/{Per(a.MoveTurn[2, t], a.N)}/{Per(a.MoveTurn[3, t], a.N)}"));
                        Console.WriteLine($"| {b} | {v.Name} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Per(a.Unleash, a.N)} | {Per(a.Burnout, a.N)} | {Per(a.Embers, a.N)}（{Per(a.EmbersExtra, a.N)}） | {Per(a.RainDrops, a.N)} | {Per(a.CallFires, a.N)}（{Per(a.CallGrowth, a.N)}） | {Pct(a.MoveBattles, a.N)} | {Pct(a.Multi, a.N)} | {first} | {per} |");
                    }
        Console.WriteLine();
    }

    static void TableD(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表D —— 火勢4 に張り付いていないか（周回の頭・刻みの後）");
        Console.WriteLine();
        Console.WriteLine("九/新兵 × 200/200 と 400/300。役ごとに 周回1〜5 の平均火勢（燃えていない駒は 0）・火勢4 の割合・分布。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | 役 | 平均 T1 ／ T2 ／ T3 ／ T4 ／ T5 | 火勢4 % T1〜T5 | 分布 0 ／ 1 ／ 2 ／ 3 ／ 4 % |");
        Console.WriteLine("|---|---|---|---|---|---|---|");
        foreach (string b in BoardNames.Take(4))
            foreach (var v in Versions)
                for (int s = 0; s < 2; s++)
                {
                    var a = cells[(b, v.Name, BA.MainWave, s)];
                    for (int r = 0; r < 4; r++)
                    {
                        long hs = Enumerable.Range(0, 5).Sum(l => a.LvHist[r, l]);
                        if (hs == 0) continue;
                        string avg = string.Join(" ／ ", Enumerable.Range(1, 5).Select(t => Per(a.LvSum[r, t], a.LvCnt[r, t])));
                        string p4 = string.Join(" ／ ", Enumerable.Range(1, 5).Select(t => Pct(a.Lv4[r, t], a.LvCnt[r, t])));
                        string hist = string.Join(" ／ ", Enumerable.Range(0, 5).Select(l => Pct(a.LvHist[r, l], hs)));
                        Console.WriteLine($"| {b} | {v.Name} | {BA.Scales[s].Name} | {RoleNames[r]} | {avg} | {p4} | {hist} |");
                    }
                }
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | **同時に火勢4 の味方** T1 ／ T2 ／ T3 ／ T4 ／ T5 | 萎む/戦 | 消える/戦 | 燃え広がり（相手）/戦 | 育ち/戦（上限で捨てた） | ヒヨ自身の育ち/戦（上限で捨てた） | 煽り/戦 |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|---|---|--:|");
        foreach (string b in BoardNames)
            foreach (var v in Versions)
                for (int s = 0; s < 2; s++)
                {
                    var a = cells[(b, v.Name, BA.MainWave, s)];
                    string sim = string.Join(" ／ ", Enumerable.Range(1, 5).Select(t => Per(a.Simul4[t], a.SimulCnt[t])));
                    Console.WriteLine($"| {b} | {v.Name} | {BA.Scales[s].Name} | {sim} | {Per(a.Wilts, a.N)} | {Per(a.Outs, a.N)} | {Per(a.Spreads, a.N)} | {Per(a.SpreadGrowth, a.N)}（{Per(a.SpreadCapped, a.N)}） | {Per(a.SelfGrowth, a.N)}（{Per(a.SelfCapped, a.N)}） | {Per(a.Stokes, a.N)} |");
                }
        Console.WriteLine();
    }

    static void TableE(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表E —— ヒヨの手番の中身（煽り ／ ギフト1体 ／ ギフト2体）");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | 手番/戦 | 煽り % | ギフト1体 % | ギフト2体 % | ほか（相手なし・動けない）% |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|");
        foreach (string b in BoardNames.Take(4))
            foreach (var v in Versions)
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                    {
                        if (w != BA.MainWave && s != 1) continue;
                        var a = cells[(b, v.Name, w, s)];
                        long h = a.HiyoHands;
                        Console.WriteLine($"| {b} | {v.Name} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Per(h, a.N)} | {Pct(a.HiyoStoke, h)} | {Pct(a.HiyoGift1, h)} | {Pct(a.HiyoGift2, h)} | {Pct(a.HiyoOther, h)} |");
                    }
        Console.WriteLine();
    }

    static readonly string[] HotaSrcNames = { "段", "焼き尽くす（全体）", "火の雨", "残り火" };

    static void TableF(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表F —— 1発の最大 ／ 1ターンの総量（敵への `Damage`・中継を除く）");
        Console.WriteLine();
        Console.WriteLine("1ターンの総量は、その周回まで戦が続いた戦の平均（0 を含む）と、全戦の最大。九/新兵。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | 駒 | 1発 最大 ／ 平均 | 1Tの総量 平均 T1 ／ T2 ／ T3 ／ T4 ／ T5 | 1Tの総量の最大 | 1戦の与ダメ |");
        Console.WriteLine("|---|---|---|---|---|---|--:|--:|");
        var rows = new List<(string B, string V, string[] Ids)>();
        foreach (string b in new[] { "T3", "T3-238", "T3-S2R席" }) foreach (var v in Versions) rows.Add((b, v.Name, new[] { "hota", "borg" }));
        rows.Add(("参考 移動", "S0", new[] { "yomi", "sero" }));
        rows.Add(("参考 雷", "S0", new[] { "kata", "shiga" }));
        foreach (var (b, v, ids) in rows)
            for (int s = 0; s < 3; s++)
            {
                var a = cells[(b, v, BA.MainWave, s)];
                foreach (string id in ids)
                {
                    if (!a.HitN.ContainsKey(id)) continue;
                    var sum = a.OutSum[id]; var mx = a.OutMax[id];
                    string turns = string.Join(" ／ ", Enumerable.Range(1, 5).Select(t => Per(sum[t], a.Reached[t])));
                    Console.WriteLine($"| {b} | {v} | {BA.Scales[s].Name} | {id} | {a.HitMax[id]} ／ {Per(a.HitSum[id], a.HitN[id])} | {turns} | {mx[0]} | {Per(sum[0], a.N)} |");
                }
            }
        Console.WriteLine();
        Console.WriteLine("ホタ・ボルグの与ダメの内訳（/戦 ・ 1発の最大）:");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | ホタ 段 | 焼き尽くす（全体） | 火の雨 | 残り火 | ボルグ 通常 | 放つ |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        foreach (string b in new[] { "T3", "T3-238", "T3-S2R席" })
            foreach (var v in Versions)
                for (int s = 0; s < 3; s++)
                {
                    var a = cells[(b, v.Name, BA.MainWave, s)];
                    string H(int k) => $"{Per(a.HotaSrc[k], a.N)}・{a.HotaSrcMax[k]}";
                    string B(int k) => $"{Per(a.BorgSrc[k], a.N)}・{a.BorgSrcMax[k]}";
                    Console.WriteLine($"| {b} | {v.Name} | {BA.Scales[s].Name} | {H(0)} | {H(1)} | {H(2)} | {H(3)} | {B(0)} | {B(1)} |");
                }
        Console.WriteLine();
    }

    static void TableG(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表G —— 脆さの寄与（燃焼の脆さ F1・敵だけ +25%）");
        Console.WriteLine();
        Console.WriteLine("「脆さの分」＝ 敵への `Damage` の `BrittleExtra`（名目・破片や軛で削られる前）の和。÷ はその駒の敵への与ダメ。**敵の火勢で脆さを上げる版（次の期）の見積もり**に、脆さの分が今の何倍になれば与ダメがどれだけ増えるかを並べる（名目の比例・上限と過剰殺傷を無視した上界）。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | 3体の与ダメ/戦 | 脆さの分/戦 | 脆さ ÷ 与ダメ % | ボルグ ／ ホタ ／ ヒヨ % | 味方全体 % | 脆さ ×2（+50%）なら 3体の与ダメ +% | ×3（+75%）+% |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|---|--:|--:|--:|");
        foreach (string b in new[] { "T3", "T3-238", "T3-S2R席", "雷＋ボルグ" })
            foreach (var v in Versions)
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                    {
                        if (w != BA.MainWave && s != 1) continue;
                        var a = cells[(b, v.Name, w, s)];
                        string roles = string.Join(" ／ ", Enumerable.Range(0, 3).Select(r => Pct(a.BrittleRole[r], a.DealtRole[r])));
                        string x2 = a.TrioDealt == 0 ? "—" : (100.0 * a.TrioBrittle / a.TrioDealt).ToString("F1");
                        string x3 = a.TrioDealt == 0 ? "—" : (200.0 * a.TrioBrittle / a.TrioDealt).ToString("F1");
                        Console.WriteLine($"| {b} | {v.Name} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Per(a.TrioDealt, a.N)} | {Per(a.TrioBrittle, a.N)} | {Pct(a.TrioBrittle, a.TrioDealt)} | {roles} | {Pct(a.AllBrittle, a.AllDealt)} | {x2} | {x3} |");
                    }
        Console.WriteLine();
    }

    static void TableH(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表H —— 手番の絵の単調さ（第239期の定義・九/新兵）");
        Console.WriteLine();
        Console.WriteLine("最多の絵 % ＝ 戦ごとに「最も多い絵の手番数」の和 ÷ 手番の和。続けて同じ ＝ 隣り合う2手番が同じ絵。骨格 ＝ 種類／攻撃型。大技の手番には大技の名を絵に足した。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 倍率 | 駒 | 手番/戦 | 絵の種類/戦 | 最多の絵 % | 続けて同じ % | 骨格の最多 % | 骨格が続けて同じ % | 多い絵（上位3） |");
        Console.WriteLine("|---|---|---|---|--:|--:|--:|--:|--:|--:|---|");
        var rows = new List<(string B, string V, string[] Ids)>();
        foreach (string b in new[] { "T3", "T3-238", "T3-S2R席" }) foreach (var v in Versions) rows.Add((b, v.Name, new[] { "borg", "hota", "hiyo" }));
        rows.Add(("参考 移動", "S0", new[] { "yomi", "sero", "basa", "hane", "shio" }));
        rows.Add(("参考 雷", "S0", new[] { "kata", "shiga", "beni", "mio" }));
        foreach (var (b, v, ids) in rows)
            for (int s = 0; s < 2; s++)
            {
                var a = cells[(b, v, BA.MainWave, s)];
                foreach (string id in ids)
                {
                    if (!a.Pics.TryGetValue(id, out var pa) || pa.Hands == 0) continue;
                    var top = pa.Freq.OrderByDescending(kv => kv.Value).Take(3).Select(kv => $"{kv.Key} {Pct(kv.Value, pa.Hands)}");
                    Console.WriteLine($"| {b} | {v} | {BA.Scales[s].Name} | {id} | {Per(pa.Hands, pa.Battles)} | {Per(pa.DistinctSum, pa.Battles)} | {Pct(pa.TopSum, pa.Hands)} | {Pct(pa.Repeats, pa.Pairs)} | {Pct(pa.BoneTopSum, pa.Hands)} | {Pct(pa.BoneRepeats, pa.Pairs)} | {string.Join(" ／ ", top)} |");
                }
            }
        Console.WriteLine();
    }

    static void TableI(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表I —— 粛の波（本編 第二波）・軛の波（本編 第四波）での3体");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | 全員生存 ／ 勝率 ／ 決着T | 落ちた % ボルグ ／ ホタ ／ ヒヨ | 与ダメ/戦 ボルグ ／ ホタ ／ 相方 | ギフトの手番/戦（うち粛の下） | 放つ ／ 焼き尽くす ／ 残り火 /戦 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|");
        foreach (string b in BoardNames)
            foreach (var v in Versions)
                foreach (int w in new[] { WaveHush, WaveYoke })
                    for (int s = 0; s < 3; s++)
                    {
                        var a = cells[(b, v.Name, w, s)];
                        Console.WriteLine($"| {b} | {v.Name} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Pct(a.AllSurv, a.N)} ／ {Pct(a.Wins, a.N)} ／ {Per(a.Turns, a.N)} | "
                            + $"{Pct(a.FellRole[0], a.N)} ／ {Pct(a.FellRole[1], a.N)} ／ {Pct(a.FellRole[2], a.N)} | {Per(a.DealtRole[0], a.N)} ／ {Per(a.DealtRole[1], a.N)} ／ {Per(a.DealtRole[3], a.N)} | "
                            + $"{Per(a.GiftTurns, a.N)}（{Per(a.GiftHush, a.N)}） | {Per(a.Unleash, a.N)} ／ {Per(a.Burnout, a.N)} ／ {Per(a.Embers, a.N)} |");
                    }
        Console.WriteLine();
    }

    static void TableJ(Dictionary<(string B, string V, int W, int S), LAgg> cells)
    {
        Console.WriteLine("## 表J —— 火の雨 R ／ D（焼き尽くすごと）");
        Console.WriteLine();
        Console.WriteLine("前半 ＝ 最初の雨の時点で生きている敵が 4 体以上 ／ 終盤 ＝ 3 体以下（ボス1体の盤面に近い）。集中 ＝ いちばん多く雨を受けた敵の発数 ÷ 雨の発数。撃破 ＝ 雨の1発で倒れた敵。");
        Console.WriteLine();
        Console.WriteLine("| 台 | 版 | 波 | 倍率 | 焼き尽くす/戦 前半 ／ 終盤 | 雨/回 前半 ／ 終盤 | 受けた敵/回 前半 ／ 終盤 | 集中 % 前半 ／ 終盤 | 雨の撃破/回 前半 ／ 終盤 | 雨の与ダメ/回 前半 ／ 終盤 |");
        Console.WriteLine("|---|---|---|---|---|---|---|---|---|---|");
        foreach (string b in BoardNames.Take(4))
            foreach (var v in Versions.Skip(2))
                for (int w = 0; w < BA.WaveNames.Length; w++)
                    for (int s = 0; s < 3; s++)
                    {
                        if (w != BA.MainWave && s != 1) continue;
                        var a = cells[(b, v.Name, w, s)];
                        string P(long[] x, long[] d) => $"{Per(x[0], d[0])} ／ {Per(x[1], d[1])}";
                        Console.WriteLine($"| {b} | {v.Name} | {BA.WaveNames[w]} | {BA.Scales[s].Name} | {Per(a.JBurn[0], a.N)} ／ {Per(a.JBurn[1], a.N)} | {P(a.JDrops, a.JBurn)} | {P(a.JDistinct, a.JBurn)} | "
                            + $"{Pct(a.JTop[0], a.JDrops[0])} ／ {Pct(a.JTop[1], a.JDrops[1])} | {P(a.JKills, a.JBurn)} | {P(a.JDmg, a.JBurn)} |");
                    }
        Console.WriteLine();
    }

    // ---------------------------------------------------------------------------------
    // `compare` 61 行（受け入れ 1・2）: 版ごとにボルグ・ホタ・ヒヨを差し替え、第1〜5波 × seed 0..199 の勝率を S0 と比べる。
    // ---------------------------------------------------------------------------------
    internal static double[,,] CompareRates()
    {
        var rows = CompareBuilds();
        var w = new double[Versions.Length, rows.Length, 5];
        for (int v = 0; v < Versions.Length; v++)
            for (int i = 0; i < rows.Length; i++)
            {
                var f = Apply(rows[i].F, Versions[v]);
                for (int st = 0; st < 5; st++)
                {
                    var stage = EnemyCatalog.Stages[st].Enemy;
                    var res = new bool[BA.Seeds];
                    Parallel.For(0, BA.Seeds, seed => res[seed] = BattleEngine.Run(f, stage, seed, verbose: false).PlayerWon);
                    w[v, i, st] = 100.0 * res.Count(x => x) / BA.Seeds;
                }
            }
        return w;
    }

    static void CompareMoves()
    {
        Console.WriteLine("## `compare` 61 行 × 第1〜5波 × seed 0..199（受け入れ 1・2）");
        Console.WriteLine();
        var rows = CompareBuilds();
        var w = CompareRates();
        bool Has(Formation f) => f.Occupied().Any(o => o.Def.Id is "borg" or "hota" or "hiyo");
        Console.WriteLine("| 版 | 動いたセル | うち3枚のいない行 | 動いた行 | 上がったセル ／ 下がったセル | 全61行の第2〜5波の平均 |");
        Console.WriteLine("|---|--:|--:|--:|---|---|");
        for (int v = 0; v < Versions.Length; v++)
        {
            int moved = 0, movedNo = 0, rowsMoved = 0, up = 0, down = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                bool rm = false;
                for (int st = 0; st < 5; st++)
                {
                    double d = w[v, i, st] - w[0, i, st];
                    if (d == 0) continue;
                    moved++; rm = true; if (d > 0) up++; else down++;
                    if (!Has(rows[i].F)) movedNo++;
                }
                if (rm) rowsMoved++;
            }
            string avg = string.Join(" ／ ", Enumerable.Range(1, 4).Select(st => Enumerable.Range(0, rows.Length).Average(i => w[v, i, st]).ToString("F1")));
            Console.WriteLine($"| {Versions[v].Name} | {moved} | {movedNo} | {rowsMoved} | {up} ／ {down} | {avg} |");
        }
        Console.WriteLine();
        Console.WriteLine("動いた行（第1〜5波・S0 → S1 / S2R / S2D / S2R-g4）:");
        Console.WriteLine();
        for (int i = 0; i < rows.Length; i++)
        {
            bool any = false;
            for (int v = 1; v < Versions.Length; v++) for (int st = 0; st < 5; st++) if (w[v, i, st] != w[0, i, st]) any = true;
            if (!any) continue;
            Console.WriteLine($"- {rows[i].Name}: " + string.Join(" ／ ", Enumerable.Range(0, 5).Select(st =>
                $"{w[0, i, st]:F1}→" + string.Join("/", Enumerable.Range(1, Versions.Length - 1).Select(v => w[v, i, st].ToString("F1"))))));
        }
        Console.WriteLine();
    }
}
