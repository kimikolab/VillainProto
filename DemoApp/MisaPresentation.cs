using BattleCore;
using System.Collections.Generic;

// 1発の見出しを、その発の実際の標的が書かれたイベントへ結ぶ。戦闘規則を再計算しない。
internal sealed class MisaPresentation
{
    internal readonly Dictionary<int, int> HitsByCue = new();
    internal readonly HashSet<int> Attacks = new();
    internal readonly HashSet<int> LosingSprays = new();

    internal static MisaPresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var result = new MisaPresentation();
        var sprays = new Dictionary<int, List<int>>();
        for (int i = 0; i < events.Count; i++)
        {
            var cue = events[i];
            if (cue.Kind == BattleEventKind.TurnStart) sprays.Clear();
            if (cue.Kind == BattleEventKind.Feather && cue.ActorId is { } owner)
            {
                if (cue.Text == FeatherLabels.Volley) sprays[owner] = new();
                if (cue.Text == FeatherLabels.Spray && sprays.TryGetValue(owner, out var shots)) shots.Add(i);
                if (cue.Text == FeatherLabels.Lost && sprays.TryGetValue(owner, out var spent))
                    result.LosingSprays.UnionWith(spent);
            }
            if (cue.Kind != BattleEventKind.Feather || cue.Text is not
                (FeatherLabels.Chase or FeatherLabels.Flow or FeatherLabels.Spray)) continue;
            bool spray = cue.Text == FeatherLabels.Spray;
            for (int j = i + 1; j < events.Count; j++)
            {
                var e = events[j];
                if (e.Kind is BattleEventKind.Feather or BattleEventKind.TurnStart or BattleEventKind.Skill) break;
                bool hit = spray
                    ? (e.Kind is BattleEventKind.Damage or BattleEventKind.Parry) && !e.Relayed && e.ShareFromId is null
                    : e.Kind == BattleEventKind.Attack;
                if (hit && e.ActorId == cue.ActorId && e.TargetId is not null)
                {
                    result.HitsByCue[i] = j;
                    if (e.Kind == BattleEventKind.Attack) result.Attacks.Add(j);
                    break;
                }
                // 別の攻撃の的を借りない。
                if (e.Kind == BattleEventKind.Attack) break;
            }
        }
        return result;
    }
}
