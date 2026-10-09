using BattleCore;
using System.Collections.Generic;

// 明示された見出しと、その直後の攻撃・着弾だけを結ぶ。層や敵数から太刀数を作らない。
internal sealed class MarkLoopPresentation
{
    internal readonly HashSet<int> FeatherAttacks = new(), FeatherHits = new(), RoundAttacks = new();
    internal readonly HashSet<int> LastSlashes = new();
    internal readonly Dictionary<int, int> RoundEnds = new();

    internal static MarkLoopPresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var plan = new MarkLoopPresentation();
        int lastSlash = -1, lastHit = -1, roundActor = -1;
        void Finish()
        {
            if (lastSlash >= 0) plan.LastSlashes.Add(lastSlash);
            if (lastHit >= 0) plan.RoundEnds[lastHit] = roundActor;
            lastSlash = lastHit = roundActor = -1;
        }
        for (int i = 0; i < events.Count; i++)
        {
            var cue = events[i];
            if (cue.Kind == BattleEventKind.TurnStart
                || cue.Kind == BattleEventKind.VendettaRound && cue.Text == VendettaRoundLabels.Start
                || !cue.Reaction && (cue.Kind is BattleEventKind.Skill or BattleEventKind.Charge
                    || cue.Kind == BattleEventKind.Attack && !plan.RoundAttacks.Contains(i))) Finish();
            bool feather = cue.Kind == BattleEventKind.FeatherMark;
            bool slash = cue.Kind == BattleEventKind.VendettaRound && cue.Text == VendettaRoundLabels.Slash;
            if (!feather && !slash) continue;
            if (slash) { lastSlash = i; roundActor = cue.ActorId ?? -1; }
            for (int j = i + 1; j < events.Count && events[j].Turn == cue.Turn; j++)
            {
                var e = events[j];
                if (e.Kind is BattleEventKind.FeatherMark or BattleEventKind.VendettaRound
                    or BattleEventKind.Framed or BattleEventKind.TurnStart or BattleEventKind.Skill) break;
                if (e.Kind == BattleEventKind.Attack)
                {
                    if (e.ActorId != cue.ActorId || e.TargetId != cue.TargetId) break;
                    (feather ? plan.FeatherAttacks : plan.RoundAttacks).Add(j);
                }
                if (e.Kind is BattleEventKind.Damage or BattleEventKind.Parry
                    && e.ActorId == cue.ActorId && e.TargetId == cue.TargetId && !e.Relayed && e.ShareFromId is null)
                {
                    if (feather) plan.FeatherHits.Add(j);
                    else lastHit = j;
                    break;
                }
            }
        }
        Finish();
        return plan;
    }
}
