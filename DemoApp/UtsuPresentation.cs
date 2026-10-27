using BattleCore;
using System.Collections.Generic;
using System.Linq;

// 確定したAttackの列だけから連撃の長さと順番を読む。呪いの量や打数の規則は計算しない。
internal sealed class UtsuPresentation
{
    internal sealed record Swing(int Actor, int Ordinal, int Count)
    {
        internal bool Keep => Ordinal < Count;
        internal string Clip => $"utsu_hit{System.Math.Min(Ordinal, 5)}_{(Keep && Ordinal < 5 ? "link" : "end")}_v3";
    }
    internal readonly Dictionary<int, Swing> Swings = new();
    internal readonly Dictionary<int, List<int>> Ends = new();
    internal readonly HashSet<int> FastEvents = new();

    internal static UtsuPresentation Build(IReadOnlyList<BattleEvent> events, IReadOnlyList<DemoOpening> openings)
    {
        var result = new UtsuPresentation();
        var ids = openings.Where(o => o.UnitId == "utsu").Select(o => o.InstanceId).ToHashSet();
        for (int i = 0; i < events.Count; i++)
        {
            var first = events[i];
            if (first.Kind != BattleEventKind.Attack || first.ActorId is not int actor || !ids.Contains(actor)
                || first.Relayed || result.Swings.ContainsKey(i)) continue;
            var hits = new List<int> { i }; int end = i + 1;
            for (; end < events.Count; end++)
            {
                var next = events[end];
                if (next.Kind == BattleEventKind.TurnStart) break;
                if (first.Reaction && !next.Reaction && next.Kind is BattleEventKind.Attack or BattleEventKind.Damage or BattleEventKind.Parry) break;
                if (next.Relayed || !first.Reaction && next.Reaction) continue;
                if (next.Kind is BattleEventKind.Skill or BattleEventKind.Charge) break;
                if (next.Kind != BattleEventKind.Attack) continue;
                if (next.ActorId != actor) break;
                hits.Add(end);
            }
            for (int k = 0; k < hits.Count; k++) result.Swings[hits[k]] = new(actor, k + 1, hits.Count);
            for (int k = i; k < end; k++)
                if (events[k].ActorId == actor && events[k].Reaction == first.Reaction) result.FastEvents.Add(k);
            if (!result.Ends.TryGetValue(end, out var owners)) result.Ends[end] = owners = new();
            owners.Add(actor);
        }
        return result;
    }
}
