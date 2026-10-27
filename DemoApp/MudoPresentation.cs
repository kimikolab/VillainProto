using BattleCore;
using System.Collections.Generic;
using System.Linq;

// 暴発の見せ場と、その後に記録された割り込みの打撃だけを読む。怒りの量は推測しない。
internal sealed class MudoPresentation
{
    internal sealed record Burst(int Actor, int End, int Hits);
    internal readonly Dictionary<int, Burst> Starts = new();
    internal readonly Dictionary<int, List<int>> Ends = new();

    internal static MudoPresentation Build(IReadOnlyList<BattleEvent> events, IReadOnlyList<DemoOpening> openings)
    {
        var result = new MudoPresentation();
        var ids = openings.Where(o => o.UnitId == "mudo").Select(o => o.InstanceId).ToHashSet();
        for (int i = 0; i < events.Count; i++)
        {
            var cue = events[i];
            if (cue.Kind != BattleEventKind.Highlight || cue.ActorId is not int actor || !ids.Contains(actor)) continue;
            int end = i + 1, hits = 0;
            for (int j = i + 1; j < events.Count; j++)
            {
                var next = events[j];
                if (next.Kind == BattleEventKind.TurnStart || next.Turn != cue.Turn
                    || next.Kind == BattleEventKind.Highlight && next.ActorId == actor
                    || !next.Reaction && next.Kind is BattleEventKind.Attack or BattleEventKind.Damage or BattleEventKind.Parry
                    || next.Kind is BattleEventKind.Skill or BattleEventKind.Charge) break;
                if (next.ActorId == actor)
                {
                    end = j + 1;
                    if (next.Kind == BattleEventKind.Attack && next.Reaction) hits++;
                }
                if (next.Kind == BattleEventKind.Death && next.TargetId == actor) { end = j + 1; break; }
            }
            if (hits == 0) continue;
            result.Starts[i] = new(actor, end, hits);
            if (!result.Ends.TryGetValue(end, out var owners)) result.Ends[end] = owners = new();
            owners.Add(actor);
        }
        return result;
    }
}
