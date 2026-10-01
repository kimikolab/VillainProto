using BattleCore;
using static Common;
using BA = BurnAuditDiag;
using FB = FireBurstDiag;
using FC = FireCycleDiag;

// firefinish の集計（1つのセル ＝ 台 × 版 × 波 × 倍率）。台本（verbose）と火勢の帳簿を読むだけで、盤面は1ビットも動かさない。
// 表A・H は第244〜246期の集計（`FireBurstDiag.LAgg` / `FireCycleDiag.CAgg`）をそのまま使い、表B〜G をここで数える。
static partial class FireFinishDiag
{
    /// <summary>1発・1ターンの与ダメを駒ごとに（表C）。</summary>
    internal sealed class HitRow
    {
        public long N, MaxSum, MaxMax, Total, Hits;
        public readonly long[] TurnSum = new long[TM + 1], TurnMax = new long[TM + 1], TurnN = new long[TM + 1];
        public void Merge(HitRow o)
        {
            N += o.N; MaxSum += o.MaxSum; MaxMax = Math.Max(MaxMax, o.MaxMax); Total += o.Total; Hits += o.Hits;
            for (int t = 0; t <= TM; t++) { TurnSum[t] += o.TurnSum[t]; TurnMax[t] = Math.Max(TurnMax[t], o.TurnMax[t]); TurnN[t] += o.TurnN[t]; }
        }
    }
    internal static readonly string[] HitIds = { "hota", "borg", "hiyo", "yomi", "sero" };

    internal sealed class KAgg
    {
        public long N, HotaN, HotaFell;
        // 表B: ホタの攻撃力（ターン頭の写し `StatSnapshot` ＝ 画面の攻撃力 ／ 振った攻撃力 `Attack` の Amount）
        public readonly long[] AtkSum = new long[TM + 1], AtkCnt = new long[TM + 1], AtkMaxT = new long[TM + 1];
        public long AtkMaxSum, AtkMaxMax, Atk100, SwingMaxSum, SwingMaxMax, Swing100;
        // 表C
        public readonly Dictionary<string, HitRow> Hits = new();
        // 表D: 爆炎
        public long Blazes, BlazeFoeDealt, BlazeFoeKills, BlazeAllyKills;
        public readonly long[] BlazeNom = new long[5], BlazeHp = new long[5];
        public readonly Dictionary<string, long[]> BlazeById = new();
        // 表E: ホタの火の癒し
        public readonly long[] MendNom = new long[3], MendHp = new long[3];
        // 表F: 残り火
        public long EmbersHands, EmbersDmg, EmbersMax, EmbersKills, EmbersChains, EmbersChainHits, EmbersChainDistinct;
        // 表G: 刻み
        public long FoeTickHp, FoeTickN, AllyTickDmg, AllyTickHeal;
        public readonly long[] TickOnceN = new long[5];
        // 受け入れ 4（死因の合計 ＝ 落ちた駒）
        public long CauseSum, DeathEv, FellSum, FellRes;

        public void Merge(KAgg o)
        {
            N += o.N; HotaN += o.HotaN; HotaFell += o.HotaFell;
            for (int t = 0; t <= TM; t++) { AtkSum[t] += o.AtkSum[t]; AtkCnt[t] += o.AtkCnt[t]; AtkMaxT[t] = Math.Max(AtkMaxT[t], o.AtkMaxT[t]); }
            AtkMaxSum += o.AtkMaxSum; AtkMaxMax = Math.Max(AtkMaxMax, o.AtkMaxMax); Atk100 += o.Atk100;
            SwingMaxSum += o.SwingMaxSum; SwingMaxMax = Math.Max(SwingMaxMax, o.SwingMaxMax); Swing100 += o.Swing100;
            foreach (var (k, v) in o.Hits) { if (!Hits.TryGetValue(k, out var h)) Hits[k] = h = new HitRow(); h.Merge(v); }
            Blazes += o.Blazes; BlazeFoeDealt += o.BlazeFoeDealt; BlazeFoeKills += o.BlazeFoeKills; BlazeAllyKills += o.BlazeAllyKills;
            for (int i = 0; i < 5; i++) { BlazeNom[i] += o.BlazeNom[i]; BlazeHp[i] += o.BlazeHp[i]; TickOnceN[i] += o.TickOnceN[i]; }
            foreach (var (k, v) in o.BlazeById) { if (!BlazeById.TryGetValue(k, out var a)) BlazeById[k] = a = new long[4]; for (int i = 0; i < 4; i++) a[i] += v[i]; }
            for (int i = 0; i < 3; i++) { MendNom[i] += o.MendNom[i]; MendHp[i] += o.MendHp[i]; }
            EmbersHands += o.EmbersHands; EmbersDmg += o.EmbersDmg; EmbersMax = Math.Max(EmbersMax, o.EmbersMax); EmbersKills += o.EmbersKills;
            EmbersChains += o.EmbersChains; EmbersChainHits += o.EmbersChainHits; EmbersChainDistinct += o.EmbersChainDistinct;
            FoeTickHp += o.FoeTickHp; FoeTickN += o.FoeTickN; AllyTickDmg += o.AllyTickDmg; AllyTickHeal += o.AllyTickHeal;
            CauseSum += o.CauseSum; DeathEv += o.DeathEv; FellSum += o.FellSum; FellRes += o.FellRes;
        }

        public void Take(BattleResult r, List<UnitState> p, List<UnitState> e, bool target, Dictionary<int, int>? slot0)
        {
            N++;
            var ev = r.Events;
            int cut = target ? TM : int.MaxValue;
            int n = ev.Count;
            while (n > 0 && ev[n - 1].Turn > cut) n--;   // 的の波は 1〜8 ターン目だけ（台本は時系列）
            var info = new Dictionary<int, (string Id, int Team)>();
            foreach (var u in p.Concat(e)) info[u.InstanceId] = (u.Def.Id, u.TeamId);
            foreach (var x in ev)
                if (x.Kind == BattleEventKind.Summon && x.TargetId is int sid && !info.ContainsKey(sid))
                    info[sid] = ("summon", x.Team ?? BattleContext.EnemyTeam);
            bool Foe(int? id) => id is int i && info.TryGetValue(i, out var v) && v.Team != BattleContext.PlayerTeam;
            int? Id(string s) => p.FirstOrDefault(u => u.Def.Id == s)?.InstanceId;
            int? hota = Id("hota"), borg = Id("borg");
            (int S, int E) HandOf(int i)
            {
                foreach (var h in r.Hands) if (h.EventStart <= i && i < h.EventEnd) return (h.EventStart, Math.Min(h.EventEnd, n));
                return (i, n);
            }

            // ---- 表B: ホタの攻撃力 ----
            if (hota is int hi)
            {
                HotaN++;
                if (!target && r.PlayerStarterFallen.Contains("hota")) HotaFell++;
                long bmax = 0, smax = 0;
                for (int i = 0; i < n; i++)
                {
                    var x = ev[i];
                    if (x.Kind == BattleEventKind.StatSnapshot && x.TargetId == hi && x.Turn >= 1)
                    {
                        int t = Math.Min(TM, x.Turn);
                        AtkSum[t] += x.Amount; AtkCnt[t]++; AtkMaxT[t] = Math.Max(AtkMaxT[t], x.Amount);
                        bmax = Math.Max(bmax, x.Amount);
                    }
                    if (x.Kind == BattleEventKind.Attack && x.ActorId == hi && Foe(x.TargetId)) smax = Math.Max(smax, x.Amount);
                }
                AtkMaxSum += bmax; AtkMaxMax = Math.Max(AtkMaxMax, bmax); if (bmax >= 100) Atk100++;
                SwingMaxSum += smax; SwingMaxMax = Math.Max(SwingMaxMax, smax); if (smax >= 100) Swing100++;
            }

            // ---- 表C: 1発の最大・1ターンの総量 ----
            foreach (string id in HitIds)
            {
                if (Id(id) is not int ui) continue;
                var row = new HitRow { N = 1 };
                var per = new long[TM + 1];
                long mx = 0;
                for (int i = 0; i < n; i++)
                {
                    var x = ev[i];
                    if (x.Kind != BattleEventKind.Damage || x.ActorId != ui || !Foe(x.TargetId) || x.Relayed || x.Turn < 1) continue;
                    mx = Math.Max(mx, x.Amount); row.Total += x.Amount; row.Hits++;
                    per[Math.Min(TM, x.Turn)] += x.Amount;
                }
                row.MaxSum = mx; row.MaxMax = mx;
                for (int t = 1; t <= TM; t++) if (r.Turns >= t || target) { row.TurnSum[t] = per[t]; row.TurnMax[t] = per[t]; row.TurnN[t] = 1; }
                if (!Hits.TryGetValue(id, out var acc)) Hits[id] = acc = new HitRow();
                acc.Merge(row);
            }

            // ---- 表D・F: 爆炎・残り火（台本から）----
            for (int i = 0; i < n; i++)
            {
                var x = ev[i];
                if (x.Kind != BattleEventKind.FireLevel) continue;
                if (x.Text == FireLevelLabels.Blaze && x.ActorId == borg)
                {
                    var (_, he) = HandOf(i);
                    for (int j = i + 1; j < he; j++)
                    {
                        var y = ev[j];
                        if (y.Kind == BattleEventKind.FireArmor && y.Text == FireArmorLabels.BlazeAlly) break;
                        if (y.Kind == BattleEventKind.Damage && y.ActorId == borg && Foe(y.TargetId) && !y.Relayed) BlazeFoeDealt += y.Amount;
                        if (y.Kind == BattleEventKind.Death && Foe(y.TargetId)) BlazeFoeKills++;
                    }
                }
                if (x.Text == FireLevelLabels.Embers && x.ActorId == hota && x.TargetId == hota && x.Slot == 1)
                {
                    var (_, he) = HandOf(i);
                    long d = 0;
                    for (int j = i + 1; j < he; j++)
                    {
                        var y = ev[j];
                        if (y.Kind == BattleEventKind.Damage && y.ActorId == hota && Foe(y.TargetId) && !y.Relayed) d += y.Amount;
                        if (y.Kind == BattleEventKind.Death && Foe(y.TargetId)) EmbersKills++;
                    }
                    EmbersHands++; EmbersDmg += d; EmbersMax = Math.Max(EmbersMax, d);
                }
            }

            // ---- 帳簿（戦全体・的の波は 8 ターンで打ち切っていないので参考）----
            if (r.FireLevels is FireLevelLedger fl)
            {
                Blazes += fl.Blazes; BlazeAllyKills += fl.BlazeAllyKills;
                for (int i = 0; i < 5; i++) { BlazeNom[i] += fl.BlazeNom[i]; BlazeHp[i] += fl.BlazeHp[i]; TickOnceN[i] += fl.TickOnceN[i]; }
                foreach (var (k, v) in fl.BlazeById) { if (!BlazeById.TryGetValue(k, out var a)) BlazeById[k] = a = new long[4]; for (int i = 0; i < 4; i++) a[i] += v[i]; }
                for (int i = 0; i < 3; i++) { MendNom[i] += fl.PyreMendNom[i]; MendHp[i] += fl.PyreMendHp[i]; }
                EmbersChains += fl.EmbersChains; EmbersChainHits += fl.EmbersChainHits; EmbersChainDistinct += fl.EmbersChainDistinct;
                for (int l = 0; l < 5; l++)
                {
                    FoeTickHp += fl.FoeTickHp[l] + fl.FoeTickExtraHp[l]; FoeTickN += fl.FoeTickN[l] + fl.FoeTickExtraN[l];
                    AllyTickDmg += fl.AllyTickDmg[l] + fl.AllyTickExtraDmg[l]; AllyTickHeal += fl.AllyTickHeal[l] + fl.AllyTickExtraHeal[l];
                }
            }

            // ---- 受け入れ 4 ----
            if (!target && slot0 is not null)
            {
                var agg = new BA.Agg(); agg.Take(r, p, e, slot0);
                CauseSum += agg.Cause.Sum(); DeathEv += agg.DeathEvents; FellSum += agg.FellSum; FellRes += r.PlayerStarterFallen.Count;
            }
        }
    }

    internal sealed record Cell(FB.LAgg L, FC.CAgg C, KAgg K);

    internal static Cell Measure(Formation f, int w, EnemyScaleRule sc, int seed0 = 0, int seeds = BA.Seeds)
    {
        var ls = new FB.LAgg[seeds]; var cs = new FC.CAgg[seeds]; var ks = new KAgg[seeds];
        bool target = IsTarget(w);
        Parallel.For(0, seeds, i =>
        {
            var p = BattleEngine.Materialize(f, BattleContext.PlayerTeam);
            var e = FC.WaveOf(w, sc)();
            var slotOf = p.Concat(e).ToDictionary(u => u, u => u.Slot);   // 席は Run の前（InstanceId は Run の中で振られる）
            var r = BattleEngine.Run(p, e, seed0 + i, verbose: true);
            var slot0 = slotOf.ToDictionary(kv => kv.Key.InstanceId, kv => kv.Value);
            var la = new FB.LAgg(); if (!target) la.Take(r, p, e, slot0); ls[i] = la;
            var c = new FC.CAgg(); c.Take(r, p, e, target); cs[i] = c;
            var k = new KAgg(); k.Take(r, p, e, target, slot0); ks[i] = k;
        });
        var L = new FB.LAgg(); var C = new FC.CAgg(); var K = new KAgg();
        for (int i = 0; i < seeds; i++) { L.Merge(ls[i]); C.Merge(cs[i]); K.Merge(ks[i]); }
        return new Cell(L, C, K);
    }
}
