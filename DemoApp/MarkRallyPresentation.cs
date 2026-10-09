using BattleCore;
using System.Collections.Generic;

// 回復の2件を1声に束ねる。同じターン・同じ攻撃者でも、間に次の攻撃があれば別の声。
// 対象・量・層は台本からだけ取り、回復対象や見切りの割合を選び直さない。
internal sealed class MarkRallyPresentation
{
    internal sealed class Rally
    {
        internal readonly List<BattleEvent> Cues = new();
        internal int? EnemyId;
    }
    internal readonly Dictionary<int, Rally> Starts = new();
    internal readonly HashSet<int> Heals = new();
    internal readonly HashSet<int> Ends = new();
    internal readonly Dictionary<int, List<BattleEvent>> Insights = new();

    internal static MarkRallyPresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var plan = new MarkRallyPresentation();
        Rally? group = null;
        int previous = -2;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.Insight)
            {
                int attack = i + 1;
                while (attack < events.Count && events[attack].Kind == BattleEventKind.Insight) attack++;
                if (attack < events.Count && events[attack].Kind == BattleEventKind.Attack
                    && events[attack].ActorId == e.TargetId && events[attack].Turn == e.Turn)
                {
                    if (!plan.Insights.TryGetValue(attack, out var cues)) plan.Insights[attack] = cues = new();
                    cues.Add(e);
                }
            }
            if (e.Kind != BattleEventKind.MarkRally) continue;
            int start = i;
            int before = i - 1;
            // 第302期以降は回復と叫びの間にも玉の通知が入る。音声の区切りにはしない。
            while (before >= 0 && events[before].Kind == BattleEventKind.CommandBall
                && events[before].ActorId == e.ActorId && events[before].Turn == e.Turn) before--;
            if (before >= 0 && events[before] is { Kind: BattleEventKind.Heal } heal
                && heal.ActorId == e.ActorId && heal.TargetId == e.TargetId && heal.Turn == e.Turn
                && heal.Amount == e.Amount && heal.HpAfter == e.HpAfter)
            {
                start = before;
                plan.Heals.Add(start);
            }
            bool adjacent = previous >= 0;
            for (int j = previous + 1; adjacent && j < start; j++)
                adjacent = events[j].Kind == BattleEventKind.CommandBall && events[j].ActorId == e.ActorId
                    && events[j].Turn == e.Turn;
            if (group is null || !adjacent || events[previous].ActorId != e.ActorId
                || events[previous].PartnerId != e.PartnerId || events[previous].Turn != e.Turn)
            {
                group = new Rally();
                plan.Starts[start] = group;
                for (int j = start - 1; j >= 0 && events[j].Turn == e.Turn; j--)
                {
                    var hit = events[j];
                    if (hit.Kind == BattleEventKind.Damage && hit.ActorId == e.PartnerId
                        && !hit.FriendlyFire && !hit.Relayed && hit.TargetId != hit.ActorId)
                    { group.EnemyId = hit.TargetId; break; }
                }
            }
            else plan.Ends.Remove(previous);
            group.Cues.Add(e);
            plan.Ends.Add(i);
            previous = i;
        }
        return plan;
    }
}
