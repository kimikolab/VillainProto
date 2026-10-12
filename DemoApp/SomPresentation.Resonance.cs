using BattleCore;
using System.Collections.Generic;
using System.Linq;

internal sealed partial class SomPresentation
{
    internal sealed record StandingCall(int[] Summons, int[] Throws);
    internal readonly Dictionary<int, StandingCall> StandingCalls = new();
    internal readonly HashSet<int> StandingMembers = new();
    internal readonly Dictionary<int, int[]> ThrowBatches = new(), ReturnArcs = new();
    internal readonly HashSet<int> BatchedThrows = new();
    internal readonly HashSet<int> BackswingAttacks = new();

    private void IndexResonance(IReadOnlyList<BattleEvent> events)
    {
        // 立つ一団は Summon と、その獣への帯電通知だけが続く区間で結ぶ。
        // 弾ける一団や緊急召喚、ほかの手番を跨いでまとめない。
        for (int i = 0; i < events.Count; i++)
        {
            var first = events[i];
            if (first.Kind != BattleEventKind.Summon || first.Text != UnitCatalog.Fodder.Name
                || first.ActorId is null || first.TargetId is null || StandingMembers.Contains(i)
                || i + 1 < events.Count && events[i + 1].Kind == BattleEventKind.BeastBurst
                    && events[i + 1].TargetId == first.TargetId) continue;
            var summons = new List<int>();
            var ids = new HashSet<int>();
            int j = i;
            for (; j < events.Count; j++)
            {
                var e = events[j];
                if (e.Kind == BattleEventKind.Summon && e.Text == first.Text && e.ActorId == first.ActorId
                    && e.TargetId is int id)
                { summons.Add(j); ids.Add(id); continue; }
                if (e.Kind == BattleEventKind.StatusGain && e.TargetId is int dressed && ids.Contains(dressed)) continue;
                break;
            }
            var throws = new List<int>();
            var thrown = new HashSet<int>();
            var living = new HashSet<int>(ids);
            for (; j < events.Count && living.Count > 0; j++)
            {
                var e = events[j];
                if (e.Kind == BattleEventKind.TurnStart) break;
                if (e.Kind == BattleEventKind.BeastThrow && e.ActorId is int beast && ids.Contains(beast)
                    && e.Text is not (BeastThrowLabels.Arc or BeastThrowLabels.Backswing) && thrown.Add(beast))
                    throws.Add(j);
                if (e.Kind is BattleEventKind.BeastBurst or BattleEventKind.Death && e.TargetId is int gone)
                    living.Remove(gone);
            }
            StandingCalls[i] = new(summons.ToArray(), throws.ToArray());
            StandingMembers.UnionWith(summons);
            if (throws.Count > 0)
            {
                ThrowBatches[throws[0]] = throws.ToArray();
                BatchedThrows.UnionWith(throws);
            }
        }
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind != BattleEventKind.BeastThrow) continue;
            if (e.Text == BeastThrowLabels.Resonance)
            {
                var arcs = new List<int>();
                for (int j = i + 1; j < events.Count; j++)
                {
                    var next = events[j];
                    if (next.Kind == BattleEventKind.BeastBurst && next.TargetId == e.ActorId) break;
                    if (next.Kind is BattleEventKind.Attack or BattleEventKind.TurnStart or BattleEventKind.Summon) break;
                    if (next.Kind == BattleEventKind.BeastThrow && next.Text == BeastThrowLabels.Arc && next.ActorId == e.ActorId)
                        arcs.Add(j);
                }
                ReturnArcs[i] = arcs.ToArray();
            }
            if (e.Text == BeastThrowLabels.Backswing)
                for (int j = i + 1; j < events.Count; j++)
                {
                    if (events[j].Kind == BattleEventKind.TurnStart) break;
                    if (events[j].Kind != BattleEventKind.Attack) continue;
                    if (events[j].ActorId == e.TargetId && events[j].TargetId == e.ActorId) BackswingAttacks.Add(j);
                    break;
                }
        }
    }
}
