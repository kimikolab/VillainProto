using BattleCore;
using System.Collections.Generic;

// 1発の見出しを、その発の実際の標的が書かれたイベントへ結ぶ。戦闘規則を再計算しない。
internal sealed class MisaPresentation
{
    internal readonly Dictionary<int, int> HitsByCue = new();
    internal readonly HashSet<int> Attacks = new();
    internal readonly HashSet<int> LosingSprays = new();
    internal readonly HashSet<int> FastEvents = new();
    internal readonly HashSet<int> LastShots = new();

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
                if (e.Kind is BattleEventKind.Feather or BattleEventKind.FeatherMark
                    or BattleEventKind.Framed or BattleEventKind.TurnStart or BattleEventKind.Skill) break;
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
        // 続く羽が台本にある区間だけ、着弾・HP・死亡などの表示待ちを省く。
        // イベント自体は元の順で通し、次の手番や通常攻撃へ早送りを持ち越さない。
        int previous = -1;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (result.HitsByCue.ContainsKey(i))
            {
                if (previous >= 0 && events[previous].ActorId == e.ActorId
                    && events[previous].Turn == e.Turn && events[previous].Slot + 1 == e.Slot)
                {
                    for (int j = previous + 1; j < i; j++) result.FastEvents.Add(j);
                    result.LastShots.Remove(previous);
                }
                result.LastShots.Add(i);
                previous = i;
            }
            else if (e.Kind is BattleEventKind.TurnStart or BattleEventKind.FeatherMark or BattleEventKind.Framed
                || e.Kind == BattleEventKind.Feather && e.Text is FeatherLabels.Volley or FeatherLabels.Lost
                || !e.Reaction && (e.Kind is BattleEventKind.Skill or BattleEventKind.Charge
                    || e.Kind == BattleEventKind.Attack && !result.Attacks.Contains(i)))
                previous = -1;
        }
        return result;
    }
}
