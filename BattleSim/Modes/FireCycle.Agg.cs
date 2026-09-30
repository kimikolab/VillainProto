using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;

// firecycle の集計（1つのセル ＝ 台 × 版 × 波 × 倍率）。台本（verbose）と火勢の帳簿を読むだけで、盤面は1ビットも動かさない。
static partial class FireCycleDiag
{
    const int TM = 8;   // ターンの列（的の波は 1〜8 ターン目・ほかは 8 以上を 8 に畳む）
    /// <summary>ホタの手番の型（表E）。</summary>
    internal static readonly string[] HandKinds = { "燃えていない", "段1 単体", "段2 貫き", "5連撃", "大火槍", "臨界", "焼き尽くす", "残り火" };

    internal sealed class CAgg
    {
        public long N, Wins, AllSurv, Turns, Fell;
        public readonly long[] FellRole = new long[4];
        // 表B（ギフトの間隔）
        public long GiftBattles, Gifts, GiftsMulti;
        public readonly long[] FirstGift = new long[TM + 1], Interval = new long[5];   // 間隔 1,2,3,4 以上（添字 1〜4）
        // 表C（三角の循環）
        public long Burnouts, Radiates, RadiateUsed, RadiateGrew, Unleashes, Triangles, TriBattles, BurnThenUnleash, BurnThenUnleashBattles, Sparks, SparkGrew, SparkSkipped;
        public readonly long[] TriBreak = new long[5];   // 途切れた場所: 0 放熱を蓄えなかった ／ 1 使う前に戦が終わった ／ 2 使ったときボルグが既に火勢4 ／ 3 使ったとき燃えていなかった ／ 4 育ったが放たなかった
        // 表D（相手選び）
        public long Stokes, StokeForm, StokeFormAvail;
        public readonly long[] StokeRole = new long[4];
        public long GiftRecips, GiftReady, GiftReadyAvail;
        public readonly long[] GiftRole = new long[4];
        // 表E（ホタの手番）
        public readonly long[] HandN = new long[HandKinds.Length], HandDmg = new long[HandKinds.Length], HandMax = new long[HandKinds.Length], HitMax = new long[HandKinds.Length];
        public readonly long[] StageReachT = new long[5], StageReachN = new long[5];   // ホタの火勢 2・3・4 に初めて届いたターン（和 ／ 届いた戦）
        // 表F（与ダメ）・重い波
        public readonly Dictionary<string, long> Dealt = new();
        public long TickFoe;
        public readonly long[] FoeLv4Turn = new long[TM + 1];
        public long FoeLv4Battles, FoeReach4;
        // 表G（ギフトの直後に倒れた敵）
        public long GiftHands, GiftKills;
        // 的の波（ターンごと・1〜8）
        public readonly Dictionary<string, long[]> TDealt = new();   // 駒の Id（"tick" ＝ 燃焼の刻み）× ターン
        public readonly long[] TMaxHit = new long[TM + 1], TFoeLvSum = new long[TM + 1], TFoeLvCnt = new long[TM + 1], TFoe4 = new long[TM + 1];
        public readonly long[,] TLvSum = new long[3, TM + 1], TLvCnt = new long[3, TM + 1];
        public long TGifts, TBurnouts, TUnleashes, TRadiateUsed, TTriangles, TFoeDeaths;

        static void Add(Dictionary<string, long> a, Dictionary<string, long> b) { foreach (var (k, v) in b) a[k] = a.GetValueOrDefault(k) + v; }
        public void Merge(CAgg o)
        {
            N += o.N; Wins += o.Wins; AllSurv += o.AllSurv; Turns += o.Turns; Fell += o.Fell;
            for (int i = 0; i < 4; i++) { FellRole[i] += o.FellRole[i]; StokeRole[i] += o.StokeRole[i]; GiftRole[i] += o.GiftRole[i]; }
            for (int i = 0; i < 5; i++) TriBreak[i] += o.TriBreak[i];
            GiftBattles += o.GiftBattles; Gifts += o.Gifts; GiftsMulti += o.GiftsMulti;
            for (int i = 0; i < 5; i++) { Interval[i] += o.Interval[i]; StageReachT[i] += o.StageReachT[i]; StageReachN[i] += o.StageReachN[i]; }
            Burnouts += o.Burnouts; Radiates += o.Radiates; RadiateUsed += o.RadiateUsed; RadiateGrew += o.RadiateGrew; Unleashes += o.Unleashes; Triangles += o.Triangles; TriBattles += o.TriBattles;
            BurnThenUnleash += o.BurnThenUnleash; BurnThenUnleashBattles += o.BurnThenUnleashBattles; Sparks += o.Sparks; SparkGrew += o.SparkGrew; SparkSkipped += o.SparkSkipped;
            Stokes += o.Stokes; StokeForm += o.StokeForm; StokeFormAvail += o.StokeFormAvail; GiftRecips += o.GiftRecips; GiftReady += o.GiftReady; GiftReadyAvail += o.GiftReadyAvail;
            for (int i = 0; i < HandKinds.Length; i++) { HandN[i] += o.HandN[i]; HandDmg[i] += o.HandDmg[i]; HandMax[i] = Math.Max(HandMax[i], o.HandMax[i]); HitMax[i] = Math.Max(HitMax[i], o.HitMax[i]); }
            Add(Dealt, o.Dealt); TickFoe += o.TickFoe; FoeLv4Battles += o.FoeLv4Battles; FoeReach4 += o.FoeReach4;
            GiftHands += o.GiftHands; GiftKills += o.GiftKills;
            for (int t = 0; t <= TM; t++)
            {
                FirstGift[t] += o.FirstGift[t]; FoeLv4Turn[t] += o.FoeLv4Turn[t];
                TMaxHit[t] = Math.Max(TMaxHit[t], o.TMaxHit[t]); TFoeLvSum[t] += o.TFoeLvSum[t]; TFoeLvCnt[t] += o.TFoeLvCnt[t]; TFoe4[t] += o.TFoe4[t];
                for (int r = 0; r < 3; r++) { TLvSum[r, t] += o.TLvSum[r, t]; TLvCnt[r, t] += o.TLvCnt[r, t]; }
            }
            foreach (var (k, v) in o.TDealt) { if (!TDealt.TryGetValue(k, out var a)) TDealt[k] = a = new long[TM + 1]; for (int t = 0; t <= TM; t++) a[t] += v[t]; }
            TGifts += o.TGifts; TBurnouts += o.TBurnouts; TUnleashes += o.TUnleashes; TRadiateUsed += o.TRadiateUsed; TTriangles += o.TTriangles; TFoeDeaths += o.TFoeDeaths;
        }

        /// <summary>
        /// 1戦を足す。<paramref name="target"/> なら的の波として 1〜8 ターン目だけを読む（決着の判定には触らない・勝率は数えない）。
        /// </summary>
        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, bool target)
        {
            N++;
            var ev = r.Events;
            int cut = target ? TargetTurns : int.MaxValue;
            if (!target)
            {
                Turns += r.Turns; Fell += r.PlayerStarterFallen.Count;
                if (r.PlayerWon) { Wins++; if (r.PlayerStarterFallen.Count == 0) AllSurv++; }
                foreach (string id in r.PlayerStarterFallen) FellRole[FB.RoleOf(id)]++;
            }
            var info = new Dictionary<int, (string Id, int Team, UnitState? U)>();
            foreach (var u in p.Concat(e)) info[u.InstanceId] = (u.Def.Id, u.TeamId, u);
            foreach (var x in ev)
                if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && !info.ContainsKey(sid))
                    info[sid] = ("summon", x.Team ?? BattleContext.EnemyTeam, null);
            bool Mine(int id) => info.TryGetValue(id, out var i) && i.Team == BattleContext.PlayerTeam;
            bool Foe(int id) => info.TryGetValue(id, out var i) && i.Team != BattleContext.PlayerTeam;
            UnitState? hota = p.FirstOrDefault(u => u.Def.Id == "hota"), borg = p.FirstOrDefault(u => u.Def.Id == "borg"), hiyo = p.FirstOrDefault(u => u.Def.Id == "hiyo");
            int? hotaId = hota?.InstanceId, borgId = borg?.InstanceId, hiyoId = hiyo?.InstanceId;

            // ---- 表B: ギフトの間隔（ヒヨがギフトを撃ったターン）----
            var giftTurns = new SortedSet<int>();
            var giftN = new Dictionary<int, int>();
            foreach (var x in ev)
                if (x is { Kind: BattleEventKind.FireLevel, Text: FireLevelLabels.Gift } && x.ActorId == hiyoId && x.Turn <= cut)
                { giftTurns.Add(x.Turn); giftN[x.Turn] = Math.Max(giftN.GetValueOrDefault(x.Turn), x.Slot); }
            if (giftTurns.Count > 0)
            {
                GiftBattles++; Gifts += giftTurns.Count; FirstGift[Math.Min(TM, giftTurns.Min)]++;
                GiftsMulti += giftN.Values.Count(n => n >= 2);
                int prev = -1;
                foreach (int t in giftTurns) { if (prev >= 0) Interval[Math.Min(4, t - prev)]++; prev = t; }
            }
            if (target) TGifts += giftTurns.Count;

            // ---- 表C: 三角の循環（焼き尽くす → 放熱を蓄える → 放熱で育つ → 放つ）----
            // 焼き尽くすごとに、その後の流れを追う。次の焼き尽くすで打ち切る。
            var burnIdx = new List<int>();
            for (int i = 0; i < ev.Count; i++) if (ev[i] is { Kind: BattleEventKind.FireLevel, Text: FireLevelLabels.Burnout } b && b.ActorId == hotaId && b.Turn <= cut) burnIdx.Add(i);
            int tri = 0, btu = 0;
            for (int k = 0; k < burnIdx.Count; k++)
            {
                int i0 = burnIdx[k], i1 = k + 1 < burnIdx.Count ? burnIdx[k + 1] : ev.Count;
                bool stored = false, used = false, grew = false, unl = false, at4 = false;
                for (int j = i0 + 1; j < i1; j++)
                {
                    var y = ev[j];
                    if (y.Turn > cut) break;
                    if (y.Kind != BattleEventKind.FireLevel || borgId is null) continue;
                    if (y.Text == FireLevelLabels.Radiate && y.TargetId == borgId) stored = true;
                    if (y.Text == FireLevelLabels.RadiateUse && y.TargetId == borgId && stored) { used = true; at4 = y.Amount >= FireLevelRule.Max; }
                    if (y.Text == FireLevelLabels.GrowRadiate && y.TargetId == borgId && used) grew = true;
                    if (y.Text == FireLevelLabels.Unleash && y.ActorId == borgId) { unl = true; break; }
                }
                if (unl) btu++;
                if (stored && used && grew && unl) tri++;
                else if (borg is not null && borg.Def.Traits.Contains(TraitId.BorgRadiate))
                    TriBreak[!stored ? 0 : !used ? 1 : !grew ? (at4 ? 2 : 3) : 4]++;
            }
            Burnouts += burnIdx.Count; BurnThenUnleash += btu; if (btu > 0) BurnThenUnleashBattles++;
            Triangles += tri; if (tri > 0) TriBattles++;
            if (target) { TBurnouts += burnIdx.Count; TTriangles += tri; }
            foreach (var x in ev)
            {
                if (x.Kind != BattleEventKind.FireLevel || x.Turn > cut) continue;
                if (x.Text == FireLevelLabels.Radiate) Radiates++;
                if (x.Text == FireLevelLabels.RadiateUse) { RadiateUsed++; if (target) TRadiateUsed++; }
                if (x.Text == FireLevelLabels.GrowRadiate) RadiateGrew++;
                if (x.Text == FireLevelLabels.Unleash) { Unleashes++; if (target) TUnleashes++; }
                if (x.Text == FireLevelLabels.Spark) Sparks++;
                if (x.Text == FireLevelLabels.GrowSpark) SparkGrew++;
            }
            if (r.FireLevels is FireLevelLedger fl && !target) SparkSkipped += fl.SparkSpreadSkipped;

            // ---- 表D: 相手選び（台本の「煽り」「ギフトの手番」と、そのときの火勢から版に依らず数え直す）----
            foreach (var x in ev)
            {
                if (x.Kind != BattleEventKind.FireLevel || x.Turn > cut || x.TargetId is not int tg || !info.TryGetValue(tg, out var ti)) continue;
                if (x.Text == FireLevelLabels.Stoke && x.ActorId == hiyoId)
                {
                    Stokes++; StokeRole[FB.RoleOf(ti.Id)]++;
                    if (ti.U is not null && FormChangesAt(ti.U, x.Amount)) StokeForm++;
                }
                if (x.Text == FireLevelLabels.GiftTurn && x.ActorId == hiyoId)
                {
                    GiftRecips++; GiftRole[FB.RoleOf(ti.Id)]++;
                    if (ti.U is not null && x.Amount >= FireLevelRule.Max && HasBigMove(ti.U)) GiftReady++;
                }
            }
            // 選べる相手がいたか（煽り: 型が変わる味方が燃えていたか ／ ギフト: 大技の準備ができた味方がいたか）——周回の頭の写しで数える（近似）。
            if (r.FireLevels is FireLevelLedger fs)
            {
                var byTurn = fs.Snaps.Where(s => s.Team == BattleContext.PlayerTeam && s.Turn <= cut).GroupBy(s => s.Turn);
                foreach (var g in byTurn)
                {
                    bool form = false, ready = false;
                    foreach (var s in g)
                    {
                        if (s.Id == hiyoId || !info.TryGetValue(s.Id, out var si) || si.U is null) continue;
                        if (s.Level is > 0 and < 4 && FormChangesAt(si.U, s.Level)) form = true;
                        if (s.Level >= 4 && HasBigMove(si.U)) ready = true;
                    }
                    if (form) StokeFormAvail++;
                    if (ready) GiftReadyAvail++;
                }
            }

            // ---- 表E: ホタの手番の型と与ダメ ----
            if (hotaId is int hi)
            {
                bool lance = hota!.Def.Traits.Contains(TraitId.PyreLance);
                foreach (var h in r.Hands.Where(h => h.ActorId == hi))
                {
                    if (h.EventStart < ev.Count && ev[h.EventStart].Turn > cut) continue;
                    int kind = -1, st = -1; long dmg = 0, max = 0;
                    for (int j = h.EventStart; j < h.EventEnd && j < ev.Count; j++)
                    {
                        var y = ev[j];
                        if (y.Kind == BattleEventKind.FireLevel && y.ActorId == hi && y.TargetId == hi)
                        {
                            if (y.Text == FireLevelLabels.Burnout) kind = 6;
                            else if (y.Text == FireLevelLabels.Embers && kind < 0) kind = 7;
                            else if (y.Text == FireLevelLabels.Critical && kind < 0) kind = 5;
                            else if (y.Text == FireLevelLabels.Stage && st < 0) st = y.Amount;
                        }
                        if (y.Kind == BattleEventKind.Damage && y.ActorId == hi && y.TargetId is int dt && Foe(dt) && !y.Relayed) { dmg += y.Amount; max = Math.Max(max, y.Amount); }
                    }
                    if (kind < 0) kind = st <= 0 ? 0 : st == 1 ? 1 : st == 2 ? 2 : lance ? 4 : 3;
                    HandN[kind]++; HandDmg[kind] += dmg; HandMax[kind] = Math.Max(HandMax[kind], dmg); HitMax[kind] = Math.Max(HitMax[kind], max);
                }
                if (r.FireLevels is FireLevelLedger fh)
                {
                    var first = new int[5];
                    foreach (var s in fh.Snaps.Where(s => s.Id == hi && s.Turn <= cut))
                        for (int l = 2; l <= 4; l++) if (s.Level >= l && first[l] == 0) first[l] = s.Turn;
                    // 周回の頭の写しだけでは周回の中で届いたのを落とすので、台本の「育つ」も見る
                    foreach (var y in ev)
                        if (y.Kind == BattleEventKind.FireLevel && y.TargetId == hi && y.Text.StartsWith("育つ") && y.Turn <= cut)
                            for (int l = 2; l <= 4; l++) if (y.Amount >= l && (first[l] == 0 || y.Turn < first[l])) first[l] = y.Turn;
                    for (int l = 2; l <= 4; l++) if (first[l] > 0) { StageReachT[l] += first[l]; StageReachN[l]++; }
                }
            }

            // ---- 表F・的: 与ダメ（駒ごと・刻み）・敵の火勢 ----
            string? lastTick = null; int tickTo = -1;
            foreach (var x in ev)
            {
                if (x.Turn > cut) break;
                // 刻み（出どころ null）: 燃焼は "tick"、ほか（毒など）は "tick:種類"
                if (x.Kind == BattleEventKind.Status && x.TargetId is int bt && Foe(bt)) { lastTick = x.Text == "燃焼" ? "tick" : "tick:" + x.Text; tickTo = bt; continue; }
                if (x.Kind == BattleEventKind.Damage && x.TargetId is int d && Foe(d) && !x.Relayed)
                {
                    string? key = x.ActorId is int a && Mine(a) ? info[a].Id : x.ActorId is null && lastTick is not null && tickTo == d ? lastTick : null;
                    if (key is not null)
                    {
                        Dealt[key] = Dealt.GetValueOrDefault(key) + x.Amount;
                        if (key == "tick") TickFoe += x.Amount;
                        if (target && x.Turn >= 1)
                        {
                            if (!TDealt.TryGetValue(key, out var arr)) TDealt[key] = arr = new long[TM + 1];
                            arr[x.Turn] += x.Amount;
                            if (key != "tick") TMaxHit[x.Turn] = Math.Max(TMaxHit[x.Turn], x.Amount);
                        }
                    }
                }
                lastTick = null;
                if (target && x.Kind == BattleEventKind.Death && x.TargetId is int kd && Foe(kd)) TFoeDeaths++;
            }
            var r4 = new HashSet<int>(); int first4 = 0;
            foreach (var x in ev)
                if (x.Kind == BattleEventKind.FireLevel && x.Turn <= cut && x.TargetId is int ft && Foe(ft) && x.Text.StartsWith("育つ") && x.Amount >= 4 && r4.Add(ft) && first4 == 0) first4 = x.Turn;
            FoeReach4 += r4.Count;
            if (first4 > 0) { FoeLv4Battles++; FoeLv4Turn[Math.Min(TM, first4)]++; }
            if (target && r.FireLevels is FireLevelLedger ft2)
                foreach (var s in ft2.Snaps.Where(s => s.Turn is >= 1 and <= TM))
                {
                    if (!info.TryGetValue(s.Id, out var si)) continue;
                    if (si.Team != BattleContext.PlayerTeam) { TFoeLvSum[s.Turn] += s.Level; TFoeLvCnt[s.Turn]++; if (s.Level >= 4) TFoe4[s.Turn]++; continue; }
                    int ro = FB.RoleOf(si.Id);
                    if (ro < 3) { TLvSum[ro, s.Turn] += s.Level; TLvCnt[ro, s.Turn]++; }
                }

            // ---- 表G: ギフトの手番の中で倒れた敵 ----
            for (int i = 0; i < ev.Count; i++)
            {
                if (ev[i] is not { Kind: BattleEventKind.FireLevel, Text: FireLevelLabels.GiftTurn } gt || gt.ActorId != hiyoId || gt.Turn > cut) continue;
                var hd = r.Hands.Where(h => h.ActorId == gt.TargetId && h.EventStart > i).OrderBy(h => h.EventStart).FirstOrDefault();
                if (hd.EventEnd == 0) continue;
                GiftHands++;
                for (int j = hd.EventStart; j < hd.EventEnd && j < ev.Count; j++) if (ev[j].Kind == BattleEventKind.Death && ev[j].TargetId is int kt && Foe(kt)) GiftKills++;
            }
        }
    }

    /// <summary>その駒が火勢 <paramref name="lv"/> のとき、+1 で型が変わるか（<see cref="FireStokeTrait.FormChanges"/> と同じ判定を、写しの火勢で）。</summary>
    internal static bool FormChangesAt(UnitState u, int lv)
    {
        if (lv <= 0 || lv >= FireLevelRule.Max) return false;
        if (PyreStageTrait.FormAt(u, lv) != PyreStageTrait.FormAt(u, lv + 1)) return true;
        return lv + 1 == FireLevelRule.Max && HasBigMove(u);
    }
    internal static bool HasBigMove(UnitState u) => u.HasTrait(TraitId.FireUnleash) || u.HasTrait(TraitId.PyreBurnout);

    internal static (FB.LAgg L, CAgg C, EnemyFireDiag.EAgg E) Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var ls = new FB.LAgg[seeds]; var cs = new CAgg[seeds]; var es = new EnemyFireDiag.EAgg[seeds];
        bool target = IsTarget(w);
        Parallel.For(0, seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = WaveOf(w, sc)();
            var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
            var r = BattleEngine.Run(p, e, seed0 + i, verbose: true);
            var la = new FB.LAgg(); if (!target) la.Take(r, p, e, slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value)); ls[i] = la;
            var c = new CAgg(); c.Take(r, p, e, target); cs[i] = c;
            var ea = new EnemyFireDiag.EAgg(); if (!target) ea.Take(r, p, e); es[i] = ea;
        });
        var L = new FB.LAgg(); var C = new CAgg(); var E = new EnemyFireDiag.EAgg();
        for (int i = 0; i < seeds; i++) { L.Merge(ls[i]); C.Merge(cs[i]); E.Merge(es[i]); }
        return (L, C, E);
    }
}
