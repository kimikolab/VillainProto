using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;
using FF = FireFinishDiag;

// fireatk の集計（1つのセル ＝ 台 × 版 × 波 × 倍率）。台本（verbose）と火勢の帳簿を読むだけで、盤面は1ビットも動かさない。
// 表A・C・G と受け入れ 4 は第244〜249期の集計（`FireBurstDiag.LAgg` / `FireCycleDiag.CAgg` / `FireFinishDiag.KAgg`）をそのまま使い、表B・D・E・F をここで数える。
static partial class FireAtkDiag
{
    /// <summary>ホタの攻撃力の上がり方の出どころ（表B）。</summary>
    internal static readonly string[] SrcNames = { "贔屓", "あぶれた火", "くべられる火" };
    /// <summary>上がった手番の持ち主（表B）。</summary>
    internal static readonly string[] OwnerNames = { "ボルグ", "ホタ", "ヒヨ", "ほか", "手番の外" };
    /// <summary>ホタの手番の種類（表D）。</summary>
    internal static readonly string[] HandKinds = { "段0（燃えていない）", "段1（単体 ×4）", "段2（貫き ×4）", "大火槍（×7）", "臨界（×7）", "焼き尽くす", "残り火" };

    internal sealed class AAgg
    {
        public long N, HotaN;
        // 表B: 出どころ × 手番の持ち主（量・回）、ターンごとの量、1戦の育ちの和の最大
        public readonly long[,] SrcAmt = new long[3, 5], SrcCnt = new long[3, 5];
        public readonly long[,] SrcTurn = new long[3, TM + 1];
        public long GainMaxSum, GainMaxMax;
        public readonly Dictionary<string, long> FedBy = new(), OverflowBy = new();
        // 表D: ホタの手番の種類ごと（画面の攻撃力 ／ 振った攻撃力 ／ 1発の最大 ／ 手番の与ダメ）
        public readonly long[] HandN = new long[7], HandShown = new long[7], HandSwing = new long[7], HandHitMax = new long[7], HandTotal = new long[7], HandHits = new long[7];
        public readonly long[] HandHitMaxMax = new long[7];
        // 表E: 焼き尽くす（全体の1発 ／ 火の雨）
        public long Burnouts, BlastSwing, BlastHits, BlastDmg, BlastHitMax, RainDmg, BurnoutDmgMax, BurnoutKills, BurnoutBlastKills;
        // 表F: 爆炎・独り
        public long Solos, SoloFoeDealt, SoloFoeKills, SoloAllyKills, HiyoFell, SoloBattles;
        public readonly long[] SoloNom = new long[5], SoloHp = new long[5];
        public readonly Dictionary<string, long[]> SoloById = new();

        public void Merge(AAgg o)
        {
            N += o.N; HotaN += o.HotaN;
            for (int s = 0; s < 3; s++) { for (int h = 0; h < 5; h++) { SrcAmt[s, h] += o.SrcAmt[s, h]; SrcCnt[s, h] += o.SrcCnt[s, h]; } for (int t = 0; t <= TM; t++) SrcTurn[s, t] += o.SrcTurn[s, t]; }
            GainMaxSum += o.GainMaxSum; GainMaxMax = Math.Max(GainMaxMax, o.GainMaxMax);
            foreach (var (k, v) in o.FedBy) FedBy[k] = FedBy.GetValueOrDefault(k) + v;
            foreach (var (k, v) in o.OverflowBy) OverflowBy[k] = OverflowBy.GetValueOrDefault(k) + v;
            for (int i = 0; i < 7; i++)
            {
                HandN[i] += o.HandN[i]; HandShown[i] += o.HandShown[i]; HandSwing[i] += o.HandSwing[i]; HandHitMax[i] += o.HandHitMax[i];
                HandTotal[i] += o.HandTotal[i]; HandHits[i] += o.HandHits[i]; HandHitMaxMax[i] = Math.Max(HandHitMaxMax[i], o.HandHitMaxMax[i]);
            }
            Burnouts += o.Burnouts; BlastSwing += o.BlastSwing; BlastHits += o.BlastHits; BlastDmg += o.BlastDmg; BlastHitMax = Math.Max(BlastHitMax, o.BlastHitMax);
            RainDmg += o.RainDmg; BurnoutDmgMax = Math.Max(BurnoutDmgMax, o.BurnoutDmgMax); BurnoutKills += o.BurnoutKills; BurnoutBlastKills += o.BurnoutBlastKills;
            Solos += o.Solos; SoloFoeDealt += o.SoloFoeDealt; SoloFoeKills += o.SoloFoeKills; SoloAllyKills += o.SoloAllyKills; HiyoFell += o.HiyoFell; SoloBattles += o.SoloBattles;
            for (int i = 0; i < 5; i++) { SoloNom[i] += o.SoloNom[i]; SoloHp[i] += o.SoloHp[i]; }
            foreach (var (k, v) in o.SoloById) { if (!SoloById.TryGetValue(k, out var a)) SoloById[k] = a = new long[4]; for (int i = 0; i < 4; i++) a[i] += v[i]; }
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, bool target)
        {
            N++;
            var ev = r.Events;
            int cut = target ? TM : int.MaxValue;
            int n = ev.Count;
            while (n > 0 && ev[n - 1].Turn > cut) n--;
            var info = new Dictionary<int, (string Id, int Team)>();
            foreach (var u in p.Concat(e)) info[u.InstanceId] = (u.Def.Id, u.TeamId);
            foreach (var x in ev)
                if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && !info.ContainsKey(sid))
                    info[sid] = ("summon", x.Team ?? BattleContext.EnemyTeam);
            bool Foe(int? id) => id is int i && info.TryGetValue(i, out var v) && v.Team != BattleContext.PlayerTeam;
            int? Id(string s) => p.FirstOrDefault(u => u.Def.Id == s)?.InstanceId;
            int? hota = Id("hota"), borg = Id("borg"), hiyo = Id("hiyo");
            // 手番の持ち主（いちばん内側の手番の枠）
            var owner = new int[n];
            var hands = r.Hands.Where(h => h.EventStart < n).OrderBy(h => h.EventEnd - h.EventStart).ToList();
            for (int i = 0; i < n; i++) owner[i] = -1;
            for (int k = hands.Count - 1; k >= 0; k--)
            {
                var h = hands[k];
                for (int i = h.EventStart; i < Math.Min(h.EventEnd, n); i++) owner[i] = h.ActorId;
            }
            int OwnerIx(int i) => owner[i] < 0 ? 4 : owner[i] == borg ? 0 : owner[i] == hota ? 1 : owner[i] == hiyo ? 2 : 3;

            // ---- 表B ----
            if (hota is int hi)
            {
                HotaN++;
                long gained = 0;
                for (int i = 0; i < n; i++)
                {
                    var x = ev[i];
                    int src = -1;
                    if (x.Kind == BattleEventKind.Whet && x.TargetId == hi && x.ActorId == hiyo) src = 0;
                    else if (x.Kind == BattleEventKind.FireLevel && x.TargetId == hi && x.Text == FireLevelLabels.Overflow) src = 1;
                    else if (x.Kind == BattleEventKind.FireLevel && x.TargetId == hi && x.Text == FireLevelLabels.Fed) src = 2;
                    if (src < 0) continue;
                    int o = OwnerIx(i);
                    SrcAmt[src, o] += x.Amount; SrcCnt[src, o]++;
                    SrcTurn[src, Math.Clamp(x.Turn, 0, TM)] += x.Amount;
                    gained += x.Amount;
                    if (src > 0 && x.ActorId is int a && info.TryGetValue(a, out var ai))
                    {
                        var d = src == 1 ? OverflowBy : FedBy;
                        d[ai.Id] = d.GetValueOrDefault(ai.Id) + 1;
                    }
                }
                GainMaxSum += gained; GainMaxMax = Math.Max(GainMaxMax, gained);

                // ---- 表D・E: ホタの手番 ----
                int shown = 0;
                int hiCursor = 0;
                foreach (var h in r.Hands.Where(h => h.ActorId == hi && h.EventStart < n).OrderBy(h => h.EventStart))
                {
                    for (; hiCursor < h.EventStart; hiCursor++)
                        if (ev[hiCursor].Kind == BattleEventKind.StatSnapshot && ev[hiCursor].TargetId == hi) shown = ev[hiCursor].Amount;
                    int end = Math.Min(h.EventEnd, n);
                    int kind = -1, stage = -1;
                    for (int i = h.EventStart; i < end; i++)
                    {
                        var x = ev[i];
                        if (x.Kind != BattleEventKind.FireLevel || x.TargetId != hi) continue;
                        if (x.Text == FireLevelLabels.Burnout && x.ActorId == hi) { kind = 5; break; }
                        if (x.Text == FireLevelLabels.Embers && x.ActorId == hi) { kind = 6; break; }
                        if (x.Text == FireLevelLabels.Critical) { kind = 4; break; }
                        if (x.Text == FireLevelLabels.Lance) { kind = 3; break; }
                        if (x.Text == FireLevelLabels.Stage && stage < 0) stage = x.Amount;
                    }
                    if (kind < 0) kind = stage switch { <= 0 => 0, 1 => 1, 2 => 2, _ => 3 };
                    long swing = 0, hitMax = 0, total = 0, hits = 0;
                    bool inRain = false; long blastDmg = 0, blastHits = 0, blastMax = 0, blastSwing = 0, rain = 0, kills = 0, blastKills = 0;
                    for (int i = h.EventStart; i < end; i++)
                    {
                        var x = ev[i];
                        if (x.Kind == BattleEventKind.FireLevel && x.Text == FireLevelLabels.Rain && x.ActorId == hi) inRain = true;
                        if (x.Kind == BattleEventKind.Attack && x.ActorId == hi)
                        {
                            if (swing == 0) swing = x.Amount;
                            if (kind == 5 && !inRain && blastSwing == 0) blastSwing = x.Amount;
                        }
                        if (x.Kind == BattleEventKind.Damage && x.ActorId == hi && Foe(x.TargetId) && !x.Relayed)
                        {
                            hitMax = Math.Max(hitMax, x.Amount); total += x.Amount; hits++;
                            if (kind == 5) { if (inRain) rain += x.Amount; else { blastDmg += x.Amount; blastHits++; blastMax = Math.Max(blastMax, x.Amount); } }
                        }
                        if (kind == 5 && x.Kind == BattleEventKind.Death && Foe(x.TargetId)) { kills++; if (!inRain) blastKills++; }
                    }
                    HandN[kind]++; HandShown[kind] += shown; HandSwing[kind] += swing; HandHitMax[kind] += hitMax; HandTotal[kind] += total; HandHits[kind] += hits;
                    HandHitMaxMax[kind] = Math.Max(HandHitMaxMax[kind], hitMax);
                    if (kind == 5)
                    {
                        Burnouts++; BlastSwing += blastSwing; BlastHits += blastHits; BlastDmg += blastDmg; BlastHitMax = Math.Max(BlastHitMax, blastMax);
                        RainDmg += rain; BurnoutDmgMax = Math.Max(BurnoutDmgMax, blastDmg + rain); BurnoutKills += kills; BurnoutBlastKills += blastKills;
                    }
                }
            }

            // ---- 表F: 爆炎・独り ----
            if (!target && hiyo is int && r.PlayerStarterFallen.Contains("hiyo")) HiyoFell++;
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                var x = ev[i];
                if (x.Kind != BattleEventKind.FireLevel || x.Text != FireLevelLabels.BlazeSolo || x.ActorId != borg) continue;
                any = true;
                Solos++;
                int he = n;
                foreach (var h in r.Hands) if (h.EventStart <= i && i < h.EventEnd) { he = Math.Min(he, h.EventEnd); }
                for (int j = i + 1; j < Math.Min(he, n); j++)
                {
                    var y = ev[j];
                    if (y.Kind == BattleEventKind.FireArmor && y.Text == FireArmorLabels.BlazeAlly) break;
                    if (y.Kind == BattleEventKind.Damage && y.ActorId == borg && Foe(y.TargetId) && !y.Relayed) SoloFoeDealt += y.Amount;
                    if (y.Kind == BattleEventKind.Death && Foe(y.TargetId)) SoloFoeKills++;
                }
            }
            if (any) SoloBattles++;
            if (r.FireLevels is FireLevelLedger fl)
            {
                SoloAllyKills += fl.BlazeSoloAllyKills;
                for (int i = 0; i < 5; i++) { SoloNom[i] += fl.BlazeSoloNom[i]; SoloHp[i] += fl.BlazeSoloHp[i]; }
                foreach (var (k, v) in fl.BlazeSoloById) { if (!SoloById.TryGetValue(k, out var a)) SoloById[k] = a = new long[4]; for (int i = 0; i < 4; i++) a[i] += v[i]; }
            }
        }
    }

    internal sealed record Cell(FB.LAgg L, FC.CAgg C, FF.KAgg K, AAgg A);

    internal static Cell Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var ls = new FB.LAgg[seeds]; var cs = new FC.CAgg[seeds]; var ks = new FF.KAgg[seeds]; var aa = new AAgg[seeds];
        bool target = IsTarget(w);
        Parallel.For(0, seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = FC.WaveOf(w, sc)();
            var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);
            var r = BattleEngine.Run(p, e, seed0 + i, verbose: true, ember: EmberRule.Pre256);
            var slot0 = slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value);
            var la = new FB.LAgg(); if (!target) la.Take(r, p, e, slot0); ls[i] = la;
            var c = new FC.CAgg(); c.Take(r, p, e, target); cs[i] = c;
            var k = new FF.KAgg(); k.Take(r, p, e, target, slot0); ks[i] = k;
            var a = new AAgg(); a.Take(r, p, e, target); aa[i] = a;
        });
        var L = new FB.LAgg(); var C = new FC.CAgg(); var K = new FF.KAgg(); var A = new AAgg();
        for (int i = 0; i < seeds; i++) { L.Merge(ls[i]); C.Merge(cs[i]); K.Merge(ks[i]); A.Merge(aa[i]); }
        return new Cell(L, C, K, A);
    }
}
