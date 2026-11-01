using BattleCore;
using System.Collections.Generic;

// 爆発の後に記録された直のDamageだけを結ぶ。席・隣接・威力から対象を推測しない。
internal sealed class ChiriPresentation
{
    internal sealed record Burst(int Index, BattleEvent Event, int? Center, bool Chain);
    internal readonly Dictionary<int, Burst> Bursts = new();
    internal readonly Dictionary<int, Burst> DamageOwners = new();
    internal readonly HashSet<int> Highlights = new();
    internal readonly HashSet<int> PlantEnds = new();
    internal readonly HashSet<int> CastsWithPlant = new();
    internal readonly HashSet<int> FuseHits = new();
    internal static bool IsBomb(BattleEvent e) => e.Kind == BattleEventKind.StatusGain && e.Text is
        BattleContext.BombPlantText or BattleContext.BombFuseText or BattleContext.BombBlastText
        or BattleContext.BombMirrorText or BattleContext.BombDudText;
    internal static bool IsCast(BattleEvent e) => e.Kind == BattleEventKind.Skill && e.Text == "仕掛ける";
    private static bool Boundary(BattleEvent e) => e.Kind == BattleEventKind.TurnStart
        || !e.Reaction && e.Kind is BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.Charge;

    internal static ChiriPresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var result = new ChiriPresentation();
        bool chain = false;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (Boundary(e)) chain = false;
            if (IsCast(e))
                for (int j = i + 1; j < events.Count && !Boundary(events[j]); j++)
                    if (events[j].Kind == BattleEventKind.StatusGain && events[j].Text == BattleContext.BombPlantText
                        && events[j].ActorId == e.ActorId) { result.CastsWithPlant.Add(i); break; }
            if (e.Kind != BattleEventKind.StatusGain) continue;
            if (e.Text == BattleContext.BombFuseText)
                for (int j = i - 1; j >= 0 && !Boundary(events[j]) && !IsBomb(events[j]); j--)
                    if (events[j].Kind == BattleEventKind.Damage && events[j].TargetId == e.TargetId
                        && events[j].ActorId == e.ActorId) { result.FuseHits.Add(j); break; }
            if (e.Text == BattleContext.BombPlantText)
            {
                if (i + 1 == events.Count || events[i + 1].Kind != BattleEventKind.StatusGain
                    || events[i + 1].Text != BattleContext.BombPlantText || events[i + 1].ActorId != e.ActorId)
                    result.PlantEnds.Add(i);
                continue;
            }
            if (e.Text is not (BattleContext.BombBlastText or BattleContext.BombMirrorText)) continue;
            var burst = new Burst(i, e, e.Text == BattleContext.BombMirrorText ? e.FromId : e.TargetId, chain);
            result.Bursts.Add(i, burst);
            chain = true;
            // engineが爆発通知の直後に置く見出しだけ。ログの本文は解析しない。
            if (i + 1 < events.Count && events[i + 1].Kind == BattleEventKind.Highlight
                && events[i + 1].ActorId == e.TargetId) result.Highlights.Add(i + 1);
            for (int j = i + 1; j < events.Count; j++)
            {
                var next = events[j];
                if (Boundary(next) || next.Kind == BattleEventKind.StatusGain
                    && next.Text is BattleContext.BombBlastText or BattleContext.BombMirrorText) break;
                if (next.Kind == BattleEventKind.Damage && next.ActorId == e.ActorId
                    && !next.Relayed && !next.Reaction && next.TargetId is not null)
                    result.DamageOwners[j] = burst;
            }
        }
        return result;
    }
}
