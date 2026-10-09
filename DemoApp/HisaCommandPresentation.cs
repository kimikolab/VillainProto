using BattleCore;
using System.Collections.Generic;

// 号令より先に記録される層だけを、玉の着弾まで表示上保留する。数値は台本の写し。
internal sealed class HisaCommandPresentation
{
    internal readonly HashSet<int> DeferredLayers = new();
    internal readonly HashSet<int> DeferredMarks = new(), FreshCommands = new();
    internal readonly Dictionary<int, List<BattleEvent>> Layers = new();
    internal readonly Dictionary<int, int> CoverEnds = new();

    internal static HisaCommandPresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var plan = new HisaCommandPresentation();
        for (int i = 0; i < events.Count; i++)
        {
            var cue = events[i];
            if (cue.Kind == BattleEventKind.Command)
            {
                var indices = new List<int>();
                var marks = new List<int>();
                for (int j = i - 1; j >= 0 && indices.Count < cue.Amount; j--)
                {
                    var e = events[j];
                    if (e.Turn != cue.Turn) break;
                    if (e.Kind == BattleEventKind.Highlight && e.ActorId == cue.ActorId) continue;
                    if (e.Kind == BattleEventKind.MarkLayer && e.ActorId == cue.ActorId && e.TargetId == cue.TargetId)
                        indices.Add(j);
                    else if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Marked
                        && e.Amount > 0 && e.ActorId == cue.ActorId && e.TargetId == cue.TargetId) marks.Add(j);
                    else if (!(e.Kind == BattleEventKind.Feather && e.Text == FeatherLabels.Gain && e.ActorId == cue.ActorId)
                        && !(e.Kind == BattleEventKind.StatusGain && e.ActorId == cue.ActorId && e.TargetId == cue.TargetId)) break;
                }
                // 古い・未知の台本で一致しない場合は通常表示へ戻す。
                if ((indices.Count != cue.Amount || indices.Count == 0) && !(indices.Count == 0 && marks.Count == 1 && cue.Amount == 1)) continue;
                foreach (int j in marks) plan.DeferredMarks.Add(j);
                if (marks.Count > 0) plan.FreshCommands.Add(i);
                indices.Reverse();
                plan.Layers[i] = new();
                foreach (int j in indices) { plan.DeferredLayers.Add(j); plan.Layers[i].Add(events[j]); }
            }
            else if (cue.Kind == BattleEventKind.Cover)
            {
                for (int j = i + 1; j < events.Count && events[j].Turn == cue.Turn; j++)
                {
                    var e = events[j];
                    if (e.Kind is BattleEventKind.TurnStart or BattleEventKind.Attack or BattleEventKind.Cover) break;
                    if (e.Kind == BattleEventKind.Damage && e.ActorId == cue.PartnerId && e.TargetId == cue.ActorId)
                    { plan.CoverEnds[j] = cue.ActorId ?? -1; break; }
                }
            }
        }
        return plan;
    }
}
