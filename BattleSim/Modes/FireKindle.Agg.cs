using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;
using FF = FireFinishDiag;

// firekindle の集計（1つのセル ＝ 台 × 版 × 波 × 倍率）。台本（verbose）と火勢の帳簿を読むだけで、盤面は1ビットも動かさない。
// 表A と受け入れ 3 は第246・249期の集計（`FireCycleDiag.CAgg` / `FireFinishDiag.KAgg`）をそのまま使い、表B〜H をここで数える。
// 的の波は台本を 8 ターンで切る（帳簿から読む量——機会・溜め火の記録・鎧の火・癒しの灯——は戦全体なので参考）。
static partial class FireKindleDiag
{
    /// <summary>ボルグの火勢が上がった出どころ（表C・台本の「育つ」の見出しの <c>Text</c>）。</summary>
    internal static readonly (string Name, string[] Labels)[] GrowSrc =
    {
        ("燃え広がり", new[] { FireLevelLabels.GrowSpread }),
        ("B1 守り", new[] { FireLevelLabels.GrowGuard }),
        ("B2 開幕", new[] { FireLevelLabels.KindleOpen }),
        ("B3 放熱", new[] { FireLevelLabels.GrowRadiateNow }),
        ("煽り", new[] { FireLevelLabels.GrowStoke }),
        ("渡す火", new[] { FireLevelLabels.GiftHoard }),
    };

    internal sealed class MAgg
    {
        public long N, BorgN, HiyoN;
        // 表B: ボルグの爆炎
        public long Blazes, SoloBlazes, BlazeBattles, FirstBlazeSum, Blazes2Battles;
        public readonly long[] FirstBlazeHist = new long[TM + 2];
        // 表C: ボルグの火勢（周回の頭）・育ちの内訳（実際に上がった段の和）・火勢4 で来た育ち（機会）
        public readonly long[] LvSum = new long[TM + 1], LvCnt = new long[TM + 1], Lv4Cnt = new long[TM + 1];
        public readonly long[] Grow = new long[6];
        public long BorgMaxGrow, HiyoMaxGrow, HotaMaxGrow, Reach4Sum, Reach4N;
        // 表D: ヒヨのギフト
        public long GiftHands, GiftRecips, GiftBattles, FirstGiftSum;
        public readonly long[] GiftTo = new long[3];   // ボルグ ／ ホタ ／ ほか
        public long GiftBorgUnder4, GiftBorgUnder4Idle, GiftHoardEv, GiftHoardTo4, GiftHoardBig, GiftHoardAdds, GiftHoardCapped;
        // 表E: 味方の回復
        public long HealAll, HealBlaze, HealConvert, HealGlow, HealSelfFire, HealOther, HealBattles, FirstHealSum;
        public long Heal3;   // 3 ターン目までに入った回復
        // 表F: 溜め火 ／ 鎧の火
        public long HoardBlazes, HoardSpent, HoardAdds, HoardBlazePct, HoardMax, BlazeFoeDmg, BlazeFoeKills;
        public long ArmorFlameN, ArmorFlameAmt, GuardSaved0, GuardSaved1, GuardSteps, GuardRaised, GuardOff, MendGlowN, MendGlowHp;
        // 表G: ホタ
        public long Burnouts, HotaN;
        // 表H: 三角の循環
        public long Tri, TriBattles, CallFires;
        // Phase 0（Q0-1）: ボルグが火の鎧 ／ 盾の配りで切った被ダメ（駒ごとの帳簿・戦全体）
        public long TallyArmorSaved, TallyWardSaved;

        public void Merge(MAgg o)
        {
            N += o.N; BorgN += o.BorgN; HiyoN += o.HiyoN;
            Blazes += o.Blazes; SoloBlazes += o.SoloBlazes; BlazeBattles += o.BlazeBattles; FirstBlazeSum += o.FirstBlazeSum; Blazes2Battles += o.Blazes2Battles;
            for (int i = 0; i < FirstBlazeHist.Length; i++) FirstBlazeHist[i] += o.FirstBlazeHist[i];
            for (int t = 0; t <= TM; t++) { LvSum[t] += o.LvSum[t]; LvCnt[t] += o.LvCnt[t]; Lv4Cnt[t] += o.Lv4Cnt[t]; }
            for (int i = 0; i < Grow.Length; i++) Grow[i] += o.Grow[i];
            BorgMaxGrow += o.BorgMaxGrow; HiyoMaxGrow += o.HiyoMaxGrow; HotaMaxGrow += o.HotaMaxGrow; Reach4Sum += o.Reach4Sum; Reach4N += o.Reach4N;
            GiftHands += o.GiftHands; GiftRecips += o.GiftRecips; GiftBattles += o.GiftBattles; FirstGiftSum += o.FirstGiftSum;
            for (int i = 0; i < 3; i++) GiftTo[i] += o.GiftTo[i];
            GiftBorgUnder4 += o.GiftBorgUnder4; GiftBorgUnder4Idle += o.GiftBorgUnder4Idle; GiftHoardEv += o.GiftHoardEv; GiftHoardTo4 += o.GiftHoardTo4; GiftHoardBig += o.GiftHoardBig;
            GiftHoardAdds += o.GiftHoardAdds; GiftHoardCapped += o.GiftHoardCapped;
            HealAll += o.HealAll; HealBlaze += o.HealBlaze; HealConvert += o.HealConvert; HealGlow += o.HealGlow; HealSelfFire += o.HealSelfFire; HealOther += o.HealOther;
            HealBattles += o.HealBattles; FirstHealSum += o.FirstHealSum; Heal3 += o.Heal3;
            HoardBlazes += o.HoardBlazes; HoardSpent += o.HoardSpent; HoardAdds += o.HoardAdds; HoardBlazePct += o.HoardBlazePct; HoardMax = Math.Max(HoardMax, o.HoardMax);
            BlazeFoeDmg += o.BlazeFoeDmg; BlazeFoeKills += o.BlazeFoeKills;
            ArmorFlameN += o.ArmorFlameN; ArmorFlameAmt += o.ArmorFlameAmt; GuardSaved0 += o.GuardSaved0; GuardSaved1 += o.GuardSaved1; GuardSteps += o.GuardSteps;
            GuardRaised += o.GuardRaised; GuardOff += o.GuardOff; MendGlowN += o.MendGlowN; MendGlowHp += o.MendGlowHp;
            Burnouts += o.Burnouts; HotaN += o.HotaN;
            Tri += o.Tri; TriBattles += o.TriBattles; CallFires += o.CallFires;
            TallyArmorSaved += o.TallyArmorSaved; TallyWardSaved += o.TallyWardSaved;
        }

        static bool FL(BattleEvent x, string label) => x.Kind == BattleEventKind.FireLevel && x.Text == label;

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, bool target)
        {
            N++;
            var ev = r.Events;
            int cut = target ? TM : int.MaxValue;
            int n = ev.Count;
            while (n > 0 && ev[n - 1].Turn > cut) n--;
            var team = new Dictionary<int, int>();
            foreach (var u in p.Concat(e)) team[u.InstanceId] = u.TeamId;
            foreach (var x in ev)
                if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && !team.ContainsKey(sid)) team[sid] = x.Team ?? BattleContext.EnemyTeam;
            bool Foe(int? id) => id is int i && team.TryGetValue(i, out var t) && t != BattleContext.PlayerTeam;
            bool Ally(int? id) => id is int i && team.TryGetValue(i, out var t) && t == BattleContext.PlayerTeam;
            int? Id(string s) => p.FirstOrDefault(u => u.Def.Id == s)?.InstanceId;
            int? borg = Id("borg"), hota = Id("hota"), hiyo = Id("hiyo");
            var led = r.FireLevels;

            // ---- 表B: ボルグの爆炎 ----
            if (borg is int b)
            {
                BorgN++;
                if (r.TallyByUnit.TryGetValue("borg", out var bt)) { TallyArmorSaved += bt.FireArmorSaved; TallyWardSaved += bt.FireWardSaved; }
                int first = 0, cnt = 0;
                foreach (var x in ev.Take(n))
                    if (FL(x, FireLevelLabels.Blaze) && x.ActorId == b) { cnt++; if (first == 0) first = x.Turn; }
                    else if (FL(x, FireLevelLabels.BlazeSolo) && x.ActorId == b) SoloBlazes++;
                Blazes += cnt;
                if (cnt > 0) { BlazeBattles++; FirstBlazeSum += first; FirstBlazeHist[Math.Min(first, TM + 1)]++; }
                if (cnt >= 2) Blazes2Battles++;

                // ---- 表C: 火勢（周回の頭の写し）・育ちの内訳 ----
                if (led is not null)
                {
                    int reach = 0;
                    foreach (var s in led.Snaps)
                    {
                        if (s.Id != b || s.Turn > TM || s.Turn < 1) continue;
                        LvSum[s.Turn] += s.Level; LvCnt[s.Turn]++;
                        if (s.Level >= 4) { Lv4Cnt[s.Turn]++; if (reach == 0) reach = s.Turn; }
                    }
                    if (reach > 0) { Reach4Sum += reach; Reach4N++; }
                    BorgMaxGrow += led.MaxGrowChanceBy.GetValueOrDefault("borg");
                    HiyoMaxGrow += led.MaxGrowChanceBy.GetValueOrDefault("hiyo");
                    HotaMaxGrow += led.MaxGrowChanceBy.GetValueOrDefault("hota");
                }
                foreach (var x in ev.Take(n))
                {
                    if (x.Kind != BattleEventKind.FireLevel || x.TargetId != b) continue;
                    for (int k = 0; k < GrowSrc.Length; k++)
                        if (GrowSrc[k].Labels.Contains(x.Text) && x.Amount > x.Slot) Grow[k] += x.Amount - x.Slot;
                }

                // ---- 表F: 爆炎の敵への与ダメ（爆炎の見出しから、味方への燃焼ダメージが始まるか手番の枠が閉じるまで）----
                for (int i = 0; i < n; i++)
                {
                    if (!FL(ev[i], FireLevelLabels.Blaze) || ev[i].ActorId != b) continue;
                    int he = n;
                    foreach (var h in r.Hands) if (h.EventStart <= i && i < h.EventEnd) he = Math.Min(he, h.EventEnd);
                    for (int j = i + 1; j < Math.Min(he, n); j++)
                    {
                        var y = ev[j];
                        if (y.Kind == BattleEventKind.FireArmor && y.Text == FireArmorLabels.BlazeAlly) break;
                        if (y.Kind == BattleEventKind.Damage && y.ActorId == b && Foe(y.TargetId) && !y.Relayed) BlazeFoeDmg += y.Amount;
                        if (y.Kind == BattleEventKind.Death && Foe(y.TargetId)) BlazeFoeKills++;
                    }
                }
            }

            // ---- 表D: ヒヨのギフト ----
            if (hiyo is int hy)
            {
                HiyoN++;
                var turns = new HashSet<int>();
                int firstG = 0;
                int? giftActor = null;
                var lifted = new HashSet<int>();
                bool borgUnder = false, borgMoved = false;
                void CloseBorgGift() { if (borgUnder && !borgMoved) GiftBorgUnder4Idle++; borgUnder = false; borgMoved = false; }
                foreach (var x in ev.Take(n))
                {
                    if (FL(x, FireLevelLabels.Gift) && x.ActorId == hy)
                    {
                        turns.Add(x.Turn); if (firstG == 0) firstG = x.Turn;
                        GiftRecips++;
                        GiftTo[x.TargetId == borg ? 0 : x.TargetId == hota ? 1 : 2]++;
                    }
                    if (FL(x, FireLevelLabels.GiftHoard) && x.ActorId == hy) { GiftHoardEv++; if (x.Amount >= 4) { GiftHoardTo4++; if (x.TargetId is int t4) lifted.Add(t4); } }
                    if (FL(x, FireLevelLabels.GiftTurn))
                    {
                        CloseBorgGift();
                        giftActor = x.TargetId;
                        if (x.TargetId == borg && x.Amount < 4) { GiftBorgUnder4++; borgUnder = true; }
                    }
                    if (x.Kind == BattleEventKind.TurnStart) { CloseBorgGift(); giftActor = null; }
                    if ((FL(x, FireLevelLabels.Unleash) || FL(x, FireLevelLabels.Burnout)) && x.ActorId is int a && a == giftActor)
                    {
                        if (a == borg) borgMoved = true;
                        if (lifted.Remove(a)) GiftHoardBig++;
                    }
                }
                CloseBorgGift();
                GiftHands += turns.Count;
                if (turns.Count > 0) { GiftBattles++; FirstGiftSum += firstG; }
                if (led is not null) { GiftHoardAdds += led.GiftHoardAdds; GiftHoardCapped += led.GiftHoardCapped; }
            }

            // ---- 表E: 味方の回復 ----
            {
                long all = 0, conv = 0, self = 0, other = 0; int firstH = 0;
                foreach (var x in ev.Take(n))
                {
                    if (x.Kind != BattleEventKind.Heal || x.Amount <= 0 || !Ally(x.TargetId)) continue;
                    all += x.Amount;
                    if (firstH == 0) firstH = x.Turn;
                    if (x.Turn <= 3) Heal3 += x.Amount;
                    if (x.ActorId == hiyo && hiyo is not null) conv += x.Amount;
                    else if (x.ActorId == x.TargetId && (x.TargetId == borg || x.TargetId == hota)) self += x.Amount;
                    else other += x.Amount;
                }
                long glow = led?.MendGlowHp ?? 0;
                long blz = led is null ? 0 : led.BlazeHp[0] + led.BlazeHp[1] + led.BlazeHp[2];
                HealAll += all; HealGlow += glow; HealConvert += Math.Max(0, conv - glow); HealSelfFire += self; HealOther += other; HealBlaze += blz;
                if (firstH > 0) { HealBattles++; FirstHealSum += firstH; }
            }

            // ---- 表F（帳簿）: 溜め火・鎧の火・守るほど燃え上がる・癒しの灯 ----
            if (led is not null)
            {
                foreach (var (t, h, pct) in led.HoardLog)
                {
                    if (t > cut || h == 0) continue;
                    HoardBlazes++; HoardSpent += h; HoardBlazePct += pct; HoardMax = Math.Max(HoardMax, h);
                }
                HoardAdds += led.HoardAdds;
                ArmorFlameN += led.ArmorFlameN; ArmorFlameAmt += led.ArmorFlameAmt;
                GuardSaved0 += led.GuardSaved[0]; GuardSaved1 += led.GuardSaved[1]; GuardSteps += led.GuardSteps; GuardRaised += led.GuardRaised; GuardOff += led.GuardOff;
                MendGlowN += led.MendGlowN; MendGlowHp += led.MendGlowHp;
            }

            // ---- 表G・H: ホタの焼き尽くす ／ 三角の循環（焼き尽くす → 指名 → ボルグの爆炎 → 呼び火・第247期の数え方）----
            if (hota is int ho)
            {
                HotaN++;
                var burn = new List<int>();
                for (int i = 0; i < n; i++) if (FL(ev[i], FireLevelLabels.Burnout) && ev[i].ActorId == ho) burn.Add(i);
                Burnouts += burn.Count;
                int tri = 0;
                for (int k = 0; k < burn.Count && borg is int bb; k++)
                {
                    int i0 = burn[k], i1 = k + 1 < burn.Count ? burn[k + 1] : n;
                    bool called = false, giftBorg = false, unl = false;
                    for (int j = i0 + 1; j < i1; j++)
                    {
                        var y = ev[j];
                        if (y.Kind != BattleEventKind.FireLevel) continue;
                        if (y.Text == FireLevelLabels.Called && y.TargetId == bb) called = true;
                        if (y.Text == FireLevelLabels.GiftTurn && y.TargetId == bb) giftBorg = true;
                        if (y.Text == FireLevelLabels.Unleash && y.ActorId == bb && giftBorg) unl = true;
                        if (y.Text == FireLevelLabels.CallFire && y.ActorId == bb && unl) { if (called) tri++; break; }
                    }
                }
                Tri += tri; if (tri > 0) TriBattles++;
            }
            foreach (var x in ev.Take(n)) if (FL(x, FireLevelLabels.CallFire)) CallFires++;
        }
    }

    internal sealed record Cell(FC.CAgg C, FF.KAgg K, MAgg M);

    internal static Cell Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var cs = new FC.CAgg[seeds]; var ks = new FF.KAgg[seeds]; var ms = new MAgg[seeds];
        bool target = IsTarget(w);
        Parallel.For(0, seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = FC.WaveOf(w, sc)();
            var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
            var r = BattleEngine.Run(p, e, seed0 + i, verbose: true);
            var slot0 = slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value);
            var c = new FC.CAgg(); c.Take(r, p, e, target); cs[i] = c;
            var k = new FF.KAgg(); k.Take(r, p, e, target, slot0); ks[i] = k;
            var m = new MAgg(); m.Take(r, p, e, target); ms[i] = m;
        });
        var C = new FC.CAgg(); var K = new FF.KAgg(); var M = new MAgg();
        for (int i = 0; i < seeds; i++) { C.Merge(cs[i]); K.Merge(ks[i]); M.Merge(ms[i]); }
        return new Cell(C, K, M);
    }
}
