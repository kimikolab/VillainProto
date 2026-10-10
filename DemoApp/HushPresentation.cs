using BattleCore;
using System.Collections.Generic;
using System.Linq;

// 斬り返しの肩代わり分を別の一撃にしない。回数・的は台本からのみ取る。
internal sealed class HushPresentation
{
    internal readonly Dictionary<int, int> Ripostes = new();
    internal static HushPresentation Build(IReadOnlyList<BattleEvent> events, IEnumerable<DemoOpening> openings)
    {
        var knights = openings.Where(o => o.UnitId == "knight_g").Select(o => o.InstanceId).ToHashSet();
        var plan = new HushPresentation();
        var hits = events.Select((e, index) => (Event: e, Index: index))
            .Where(p => p.Event.Kind == BattleEventKind.Damage && p.Event.Reaction
                && p.Event.ActorId is int id && knights.Contains(id));
        foreach (var group in hits.GroupBy(p => (p.Event.Turn, p.Event.ActorId)))
        {
            // 騎士は1ターン1回。中継の間の通知を飛び越えて元の被弾先を読む。
            var first = group.First();
            var direct = group.FirstOrDefault(p => !p.Event.Relayed && !p.Event.FriendlyFire);
            int? target = (direct.Event ?? first.Event).TargetId;
            if (target is int victim) plan.Ripostes[first.Index] = victim;
        }
        return plan;
    }
}
