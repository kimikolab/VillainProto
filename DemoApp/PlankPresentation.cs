using BattleCore;
using System.Collections.Generic;

// 台本の番号だけで一斉射を組む。0 は攻撃枠の外なので、隣同士でも束ねない。
public sealed class PlankPresentation
{
    public readonly Dictionary<int, List<int>> Volleys = new();
    public readonly Dictionary<int, int> DamageToReflect = new();
    public PlankPresentation(IReadOnlyList<BattleEvent> events)
    {
        var groups = new Dictionary<(int Turn, int Slot, int? Target), List<int>>();
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind != BattleEventKind.Plank || e.Text != PlankLabels.Reflect) continue;
            List<int> volley;
            var key = (e.Turn, e.Slot, e.TargetId);
            if (e.Slot <= 0 || !groups.TryGetValue(key, out volley!))
            {
                volley = new List<int>();
                Volleys[i] = volley;
                if (e.Slot > 0) groups[key] = volley;
            }
            volley.Add(i);
            // 軛・肩代わり等を挟んでも、次の行動へはまたがない。
            for (int j = i + 1; j < events.Count; j++)
            {
                var next = events[j];
                if (next.Kind is BattleEventKind.Attack or BattleEventKind.TurnStart or BattleEventKind.Skill
                    || next.Kind == BattleEventKind.Plank && next.Text == PlankLabels.Reflect) break;
                if (next.Kind == BattleEventKind.Damage && next.ActorId == e.ActorId
                    && next.TargetId == e.TargetId && next.ShareFromId is null)
                { DamageToReflect[j] = i; break; }
            }
        }
    }
}
