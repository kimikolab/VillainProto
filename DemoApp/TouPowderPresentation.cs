using BattleCore;
using System.Collections.Generic;

// 台本の接続だけを読む。粉の量・隣接・伝染の成否をここで計算しない。
internal sealed class TouPowderPresentation
{
    internal readonly Dictionary<int, BattleEvent> ThunderReturns = new();
    internal readonly HashSet<int> ContagionGains = new();
    internal readonly Dictionary<int, BattleEvent> ContagionArrivals = new();

    internal static TouPowderPresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var plan = new TouPowderPresentation();
        int thunder = -1, gain = -1;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind is BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.TurnStart)
                thunder = gain = -1;
            if (e.Kind == BattleEventKind.Thunder) thunder = i;
            if (e.Kind == BattleEventKind.StatusGain) gain = i;
            if (e.Kind != BattleEventKind.ShockGauge) continue;
            if (e.Text == ShockGaugeLabels.DenseMove && thunder >= 0
                && events[thunder].ActorId == e.ActorId && events[thunder].TargetId == e.TargetId)
                plan.ThunderReturns[thunder] = e;
            if (e.Text == ShockGaugeLabels.Contagion && gain >= 0
                && events[gain].Text == StatusKeys.Shock && events[gain].TargetId == e.TargetId
                && events[gain].SpreadFromId == e.ActorId)
            {
                plan.ContagionGains.Add(gain);
                plan.ContagionArrivals[i] = events[gain];
            }
        }
        return plan;
    }
}
