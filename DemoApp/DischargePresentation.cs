using BattleCore;
using System.Collections.Generic;

// 台本を読むだけの帰属。戦闘上の出どころ（放電した駒）は変更しない。
internal static class DischargePresentation
{
    internal readonly record struct Credit(int Enemy, int Ally);

    internal static BattleEvent? Cause(IReadOnlyList<BattleEvent> events, int index)
    {
        var hit = events[index];
        if (hit.Kind is not (BattleEventKind.Damage or BattleEventKind.Heal)) return null;
        for (int i = index - 1; i >= 0; i--)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.Discharge)
                return e.TargetId == hit.TargetId
                    && (hit.Kind == BattleEventKind.Heal
                        ? e.SourceTrait == TraitId.Inverse && e.InverterId == hit.ActorId
                        : e.SourceTrait != TraitId.Inverse && e.ActorId == hit.ActorId)
                    ? e : null;
            // 破片・血の計数などが着弾前に挟まる。別の着弾や行動は越えない。
            if (e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.HealBlocked
                or BattleEventKind.ShockSpent or BattleEventKind.Thunder or BattleEventKind.Attack
                or BattleEventKind.TurnStart or BattleEventKind.Skill or BattleEventKind.Status)
                break;
        }
        return null;
    }

    internal static Dictionary<int, Credit> Count(IReadOnlyList<BattleEvent> events,
        IReadOnlyDictionary<int, int> teams)
    {
        var writers = new Dictionary<int, int>();
        var spentWriters = new Dictionary<int, int>();
        var result = new Dictionary<int, Credit>();
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.StatusGain && e.Text == "shock" && e.TargetId is int target)
            {
                if (e.ActorId is int writer) writers[target] = writer;
                else writers.Remove(target);
            }
            if (e.Kind == BattleEventKind.ShockSpent && e.TargetId is int spent)
            {
                spentWriters.Remove(spent);
                if (writers.Remove(spent, out int writer)) spentWriters[spent] = writer;
            }
            if (e.Kind != BattleEventKind.Damage || Cause(events, i) is not { ActorId: int from } cause
                || !spentWriters.TryGetValue(from, out int source)
                || !teams.TryGetValue(source, out int sourceTeam)
                || cause.Team is not int targetTeam) continue;
            var credit = result.GetValueOrDefault(source);
            result[source] = targetTeam == sourceTeam
                ? credit with { Ally = credit.Ally + e.Amount }
                : credit with { Enemy = credit.Enemy + e.Amount };
        }
        return result;
    }
}
