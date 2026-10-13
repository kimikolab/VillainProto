using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// 台本の放電連鎖と粉塵爆発の直後の着弾だけを束ねる。隣接や打点は再計算しない。
internal sealed class DustBurstPresentation
{
    internal sealed record Burst(int Index, int[] Hits);
    internal sealed record Chain(int Start, int End, int[] Discharges, Burst[] Bursts,
        IReadOnlyDictionary<int, int> Totals);
    internal readonly Dictionary<int, Chain> Starts = new();
    internal readonly Dictionary<int, Chain> Ends = new();
    internal readonly HashSet<int> Members = new();
    internal readonly HashSet<int> DamageMembers = new();

    private static bool InChain(BattleEvent e) => e.Kind switch {
        BattleEventKind.ShockSpent or BattleEventKind.Discharge or BattleEventKind.Damage
            or BattleEventKind.Heal or BattleEventKind.HealBlocked or BattleEventKind.Death
            or BattleEventKind.Plank or BattleEventKind.Sealed or BattleEventKind.Highlight => true,
        BattleEventKind.StatusGain => e.Text == StatusKeys.Shock,
        BattleEventKind.BeastBurst => e.Text == BeastBurstLabels.Popped,
        BattleEventKind.SilkBall => e.Text is SilkBallLabels.Pop or SilkBallLabels.Recharge,
        BattleEventKind.ShockGauge => e.Text is ShockGaugeLabels.DensePop or ShockGaugeLabels.DenseBurn
            or ShockGaugeLabels.DustBurst or ShockGaugeLabels.Contagion or ShockGaugeLabels.PowderMark
            or ShockGaugeLabels.Cloud or ShockGaugeLabels.ChargeGain or ShockGaugeLabels.ChargeSpent,
        _ => false,
    };

    internal static DustBurstPresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var plan = new DustBurstPresentation();
        for (int start = 0; start < events.Count; start++)
        {
            if (events[start].Kind is not (BattleEventKind.ShockSpent or BattleEventKind.Discharge)) continue;
            int end = start + 1;
            while (end < events.Count && InChain(events[end])) end++;
            var bursts = new List<Burst>();
            var totals = new Dictionary<int, int>();
            for (int i = start; i < end; i++)
            {
                var cue = events[i];
                if (cue.Kind != BattleEventKind.ShockGauge || cue.Text != ShockGaugeLabels.DustBurst) continue;
                var hits = new List<int>();
                for (int j = i + 1; j < end; j++)
                {
                    var next = events[j];
                    if (next.Kind is BattleEventKind.Discharge or BattleEventKind.ShockSpent or BattleEventKind.ShockGauge) break;
                    if (next.Kind != BattleEventKind.Damage || next.ActorId != cue.TargetId || next.TargetId is not int target) continue;
                    hits.Add(j); plan.DamageMembers.Add(j);
                    totals[target] = totals.GetValueOrDefault(target) + next.Amount;
                }
                bursts.Add(new(i, hits.ToArray()));
            }
            if (bursts.Count == 0) continue;
            var chain = new Chain(start, end - 1, Enumerable.Range(start, end - start)
                .Where(i => events[i].Kind == BattleEventKind.Discharge).ToArray(), bursts.ToArray(), totals);
            plan.Starts.Add(start, chain); plan.Ends.Add(end - 1, chain);
            foreach (int i in Enumerable.Range(start, end - start)) plan.Members.Add(i);
            start = end - 1;
        }
        return plan;
    }
}
