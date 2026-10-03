using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// firetri の集計（1つのセル ＝ 台 × 版 × 波 × 倍率）。台本（verbose）と火勢の帳簿を読むだけで、盤面は1ビットも動かさない。
// 表A・F・G・H・I は第246期の集計（`FireCycleDiag.CAgg` / `FireBurstDiag.LAgg`）をそのまま使い、
// 表B〜E（三角の循環・大技・指名・ヒヨの火勢）をここで数える。
static partial class FireTriDiag
{
    const int TM = 8;
    /// <summary>三角の途切れた場所（放熱の札のある版だけ）。</summary>
    internal static readonly string[] BreakNames = { "印が無い", "指名の前に次の焼き尽くす／戦の終わり", "指名・火勢4 未満（通常の手番）", "指名・火勢4・放たず", "放ったが呼び火なし" };

    internal sealed class TAgg
    {
        public long N;
        // 表B: 三角の循環（焼き尽くす → 指名 → ボルグの放つ → 呼び火）と、指名を問わない緩い版（焼き尽くす → ボルグへのギフト → 放つ → 呼び火）
        public long Burnouts, Tri, TriBattles, Loose, LooseBattles, Unleashes, Embers, CallFires;
        public readonly long[] Break = new long[5];
        // 表C: ギフト（手番 ／ 相手）・相手の内訳（何体目 × 役）
        public long GiftHands, GiftRecips;
        public readonly long[,] GiftSlotRole = new long[3, 4];   // [何体目 1・2（0 は使わない）, 役]
        public readonly long[] GiftAtLevel = new long[5], GiftAtLevelN = new long[5];   // ヒヨの火勢ごとの ギフトの手番 ／ 相手の数
        // 表D: 指名（ボルグの火勢 0〜4）と、その手番で放ったか ／ 通常の手番だったか ／ 手番が来なかった
        public readonly long[] CalledLv = new long[5];
        public long CalledUnleash, CalledNormal, CalledNoHand;
        public readonly long[] BorgGiftLv = new long[5];   // 指名を問わず、ボルグがギフトの手番を得たときの火勢
        public long BorgGiftUnleash;
        public long CallMarks, CallLost, CallStacked, GiftPairs, GiftPairChance;
        // 表E: ヒヨの火勢（周回の頭・1〜8）・育ちの出どころ（のべ・量）
        public readonly long[] HiyoLvSum = new long[TM + 1], HiyoLvCnt = new long[TM + 1];
        public readonly long[,] HiyoLvHist = new long[TM + 1, 5];
        public long GrowStoke, GrowSpread, SparkBurnout, SparkUnleash, SparkBurnoutGrew, SparkUnleashGrew;

        public void Merge(TAgg o)
        {
            N += o.N; Burnouts += o.Burnouts; Tri += o.Tri; TriBattles += o.TriBattles; Loose += o.Loose; LooseBattles += o.LooseBattles;
            Unleashes += o.Unleashes; Embers += o.Embers; CallFires += o.CallFires;
            for (int i = 0; i < 5; i++)
            {
                Break[i] += o.Break[i]; GiftAtLevel[i] += o.GiftAtLevel[i]; GiftAtLevelN[i] += o.GiftAtLevelN[i];
                CalledLv[i] += o.CalledLv[i]; BorgGiftLv[i] += o.BorgGiftLv[i];
            }
            GiftHands += o.GiftHands; GiftRecips += o.GiftRecips;
            for (int a = 0; a < 3; a++) for (int b = 0; b < 4; b++) GiftSlotRole[a, b] += o.GiftSlotRole[a, b];
            CalledUnleash += o.CalledUnleash; CalledNormal += o.CalledNormal; CalledNoHand += o.CalledNoHand; BorgGiftUnleash += o.BorgGiftUnleash;
            CallMarks += o.CallMarks; CallLost += o.CallLost; CallStacked += o.CallStacked; GiftPairs += o.GiftPairs; GiftPairChance += o.GiftPairChance;
            for (int t = 0; t <= TM; t++)
            {
                HiyoLvSum[t] += o.HiyoLvSum[t]; HiyoLvCnt[t] += o.HiyoLvCnt[t];
                for (int l = 0; l < 5; l++) HiyoLvHist[t, l] += o.HiyoLvHist[t, l];
            }
            GrowStoke += o.GrowStoke; GrowSpread += o.GrowSpread; SparkBurnout += o.SparkBurnout; SparkUnleash += o.SparkUnleash;
            SparkBurnoutGrew += o.SparkBurnoutGrew; SparkUnleashGrew += o.SparkUnleashGrew;
        }

        static bool FL(BattleEvent x, string label) => x.Kind == BattleEventKind.FireLevel && x.Text == label;

        public void Take(BattleResult r, List<UnitState> p, bool target)
        {
            N++;
            var ev = r.Events;
            int cut = target ? TargetTurns : int.MaxValue;
            int n = ev.Count;
            while (n > 0 && ev[n - 1].Turn > cut) n--;   // 的の波は 1〜8 ターン目だけ（台本は時系列）
            int? hota = p.FirstOrDefault(u => u.Def.Id == "hota")?.InstanceId, borg = p.FirstOrDefault(u => u.Def.Id == "borg")?.InstanceId,
                 hiyo = p.FirstOrDefault(u => u.Def.Id == "hiyo")?.InstanceId;
            bool callCard = p.Any(u => u.Def.Id == "borg" && u.Def.Traits.Contains(TraitId.RadiateCall));
            var role = p.ToDictionary(u => u.InstanceId, u => FB.RoleOf(u.Def.Id));
            int Role(int? id) => id is int i && role.TryGetValue(i, out var ro) ? ro : 3;

            // 手番の枠: ギフトの手番の見出しの後ろで、相手の最初の手番
            (int S, int E)? HandAfter(int i, int actor)
            {
                var hd = r.Hands.Where(h => h.ActorId == actor && h.EventStart > i).OrderBy(h => h.EventStart).FirstOrDefault();
                return hd.EventEnd == 0 ? null : (hd.EventStart, Math.Min(hd.EventEnd, n));
            }
            bool UnleashIn((int S, int E)? h) => h is { } hh && Enumerable.Range(hh.S, Math.Max(0, hh.E - hh.S)).Any(j => FL(ev[j], FireLevelLabels.Unleash) && ev[j].ActorId == borg);

            // ---- 大技・印・火の粉・ギフト（のべ）----
            for (int i = 0; i < n; i++)
            {
                var x = ev[i];
                if (x.Kind != BattleEventKind.FireLevel) continue;
                switch (x.Text)
                {
                    case FireLevelLabels.Burnout when x.ActorId == hota: Burnouts++; break;
                    case FireLevelLabels.Unleash when x.ActorId == borg: Unleashes++; break;
                    case FireLevelLabels.Embers when x.ActorId == hota && x.Slot == 1: Embers++; break;
                    case FireLevelLabels.CallFire when x.ActorId == borg: CallFires++; break;
                    case FireLevelLabels.CallMark: CallMarks++; break;
                    case FireLevelLabels.CallLost: CallLost++; break;
                    case FireLevelLabels.GiftPair: GiftPairs++; break;
                    case FireLevelLabels.Gift when x.ActorId == hiyo:
                        GiftRecips++; GiftSlotRole[Math.Clamp(x.Slot, 1, 2), Role(x.TargetId)]++;
                        break;
                    case FireLevelLabels.GrowSpark when x.TargetId == hiyo:
                        if (x.ActorId == borg) { SparkUnleash++; SparkUnleashGrew += x.Amount - x.Slot; } else { SparkBurnout++; SparkBurnoutGrew += x.Amount - x.Slot; }
                        break;
                    case FireLevelLabels.GrowSelf when x.TargetId == hiyo:
                        if (x.ActorId == hiyo) GrowStoke += x.Amount - x.Slot; else GrowSpread += x.Amount - x.Slot;
                        break;
                    case FireLevelLabels.Called when x.TargetId == borg:
                    {
                        CalledLv[Math.Clamp(x.Amount, 0, 4)]++;
                        // 指名の後の、ボルグのギフトの手番
                        int gt = -1;
                        for (int j = i + 1; j < n; j++) if (FL(ev[j], FireLevelLabels.GiftTurn) && ev[j].TargetId == borg) { gt = j; break; }
                        var h = gt < 0 ? null : HandAfter(gt, borg!.Value);
                        if (h is null) CalledNoHand++; else if (UnleashIn(h)) CalledUnleash++; else CalledNormal++;
                        break;
                    }
                    case FireLevelLabels.GiftTurn when x.TargetId == borg:
                    {
                        BorgGiftLv[Math.Clamp(x.Amount, 0, 4)]++;
                        if (UnleashIn(HandAfter(i, borg!.Value))) BorgGiftUnleash++;
                        break;
                    }
                }
            }
            // 同じ周回に2回撃つことは無い（1手番1回）ので、周回ごとのヒヨの火勢で数える
            var gl = new Dictionary<int, (int Lv, int N)>();
            foreach (var x in ev.Take(n)) if (FL(x, FireLevelLabels.Gift) && x.ActorId == hiyo) gl[x.Turn] = (x.Amount, Math.Max(x.Slot, gl.TryGetValue(x.Turn, out var g) ? g.N : 0));
            GiftHands += gl.Count;
            foreach (var (_, (lv, k)) in gl) { GiftAtLevel[Math.Clamp(lv, 0, 4)]++; GiftAtLevelN[Math.Clamp(lv, 0, 4)] += k; }
            if (r.FireLevels is FireLevelLedger led && !target) { CallStacked += led.CallStacked; GiftPairChance += led.GiftPairChance; }

            // ---- 表B: 三角の循環（焼き尽くすごとに、次の焼き尽くすまで追う）----
            var burn = new List<int>();
            for (int i = 0; i < n; i++) if (FL(ev[i], FireLevelLabels.Burnout) && ev[i].ActorId == hota) burn.Add(i);
            // 印の状態（焼き尽くすの時点で既に灯っていたか）
            var markAt = new bool[n + 1];
            bool on = false;
            for (int i = 0; i < n; i++)
            {
                markAt[i] = on;
                var x = ev[i];
                if (x.TargetId == borg && (FL(x, FireLevelLabels.CallMark))) on = true;
                if (x.TargetId == borg && (FL(x, FireLevelLabels.Called) || FL(x, FireLevelLabels.CallLost))) on = false;
            }
            int tri = 0, loose = 0;
            for (int k = 0; k < burn.Count; k++)
            {
                int i0 = burn[k], i1 = k + 1 < burn.Count ? burn[k + 1] : n;
                bool marked = markAt[i0], called = false, giftBorg = false, unl = false, cf = false; int calledLv = -1;
                for (int j = i0 + 1; j < i1; j++)
                {
                    var y = ev[j];
                    if (y.Kind != BattleEventKind.FireLevel || borg is null) continue;
                    if (y.Text == FireLevelLabels.CallMark && y.TargetId == borg) marked = true;
                    if (y.Text == FireLevelLabels.Called && y.TargetId == borg && !called) { called = true; calledLv = y.Amount; }
                    if (y.Text == FireLevelLabels.GiftTurn && y.TargetId == borg) giftBorg = true;
                    if (y.Text == FireLevelLabels.Unleash && y.ActorId == borg && giftBorg) unl = true;
                    if (y.Text == FireLevelLabels.CallFire && y.ActorId == borg && unl) { cf = true; break; }
                }
                if (called && unl && cf) tri++;
                if (giftBorg && unl && cf) loose++;
                if (callCard && !(called && unl && cf))
                    Break[!marked ? 0 : !called ? 1 : calledLv < FireLevelRule.Max ? 2 : !unl ? 3 : 4]++;
            }
            Tri += tri; Loose += loose;
            if (tri > 0) TriBattles++;
            if (loose > 0) LooseBattles++;

            // ---- 表E: ヒヨの火勢（周回の頭の写し）----
            if (hiyo is int hy && r.FireLevels is FireLevelLedger fs)
                foreach (var s in fs.Snaps)
                {
                    if (s.Id != hy || s.Turn < 1 || s.Turn > cut) continue;
                    int t = Math.Min(TM, s.Turn);
                    HiyoLvSum[t] += s.Level; HiyoLvCnt[t]++; HiyoLvHist[t, Math.Clamp(s.Level, 0, 4)]++;
                }
        }
    }

    internal sealed record Cell(FB.LAgg L, FC.CAgg C, TAgg T);

    internal static Cell Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var ls = new FB.LAgg[seeds]; var cs = new FC.CAgg[seeds]; var ts = new TAgg[seeds];
        bool target = IsTarget(w);
        Parallel.For(0, seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = FC.WaveOf(w, sc)();
            var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);   // 席は Run の前（InstanceId は Run の中で振られる）
            var r = BattleEngine.Run(p, e, seed0 + i, verbose: true, ember: EmberRule.Pre256);
            var la = new FB.LAgg(); if (!target) la.Take(r, p, e, slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value)); ls[i] = la;
            var c = new FC.CAgg(); c.Take(r, p, e, target); cs[i] = c;
            var t = new TAgg(); t.Take(r, p, target); ts[i] = t;
        });
        var L = new FB.LAgg(); var C = new FC.CAgg(); var T = new TAgg();
        for (int i = 0; i < seeds; i++) { L.Merge(ls[i]); C.Merge(cs[i]); T.Merge(ts[i]); }
        return new Cell(L, C, T);
    }
}
