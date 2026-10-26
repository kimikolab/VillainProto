using BattleCore;
using System.Collections.Generic;
using System.Linq;

internal sealed partial class HagiPresentation
{
    // 確定済みの命中列から見せ方を選ぶ。刈れる線・勝敗・打数は求め直さない。
    private void MarkAirCues(IReadOnlyList<BattleEvent> events, IReadOnlyList<DemoOpening> openings)
    {
        var ordered = Swings.Concat(Returns).OrderBy(p => p.Key).ToArray();
        var latest = new Dictionary<int, int>();
        var owner = new Dictionary<int, int>();
        var after = new Dictionary<int, List<int>>();
        foreach (var (index, cue) in ordered)
        {
            if (!cue.Return) { latest[cue.Actor] = index; owner[index] = index; after[index] = new(); }
            else if (latest.TryGetValue(cue.Actor, out int root) && events[root].Turn == events[index].Turn)
            { owner[index] = root; after[root].Add(index); }
        }
        void Set(int index, Swing cue) { if (cue.Return) Returns[index] = cue; else Swings[index] = cue; }
        var firstOwn = new HashSet<(int Turn, int Actor)>();
        foreach (var (index, cue) in Swings.OrderBy(p => p.Key).ToArray())
        {
            if (events[index].Reaction || cue.Pursuit) continue;
            bool first = firstOwn.Add((events[index].Turn, cue.Actor));
            if (first && !cue.Pursuit && !cue.Reverse && cue.Encore == 0
                && (cue.Damage.Any(Reaps.Contains) || after[index].Count > 0))
                Set(index, cue with { Air = AirKind.Opener });
        }
        var finishHits = FinalReapHits(events, openings);
        foreach (var (index, cue) in ordered.Where(p => p.Value.Damage.Any(finishHits.Contains)))
        {
            // 通常命中→刈るが直結する場合は、先読みして親の一振りから跳ぶ。
            // 薙ぎの二人目以降など、途中に別の刃が入る最後の刈り取りはその刃で跳ぶ。
            if (cue.Return && owner.TryGetValue(index, out int root) && Swings[root].Keep
                && Swings[root].NextReturn && after[root][0] == index)
                Set(root, Swings[root] with { Air = AirKind.Finisher });
            else Set(index, cue with { Air = AirKind.Finisher });
        }
        foreach (var (index, cue) in Swings.Where(p => p.Value.Air != AirKind.None).ToArray())
        {
            if (!cue.Keep || !cue.NextReturn || after[index].Count == 0) continue;
            int next = after[index][0];
            // 二重に跳ばず、同じ落下斬りの接触姿勢から刈り取りと着地へつなぐ。
            Set(next, Returns[next] with { Air = cue.Air, AirFollow = true });
        }
    }

    private HashSet<int> FinalReapHits(IReadOnlyList<BattleEvent> events, IReadOnlyList<DemoOpening> openings)
    {
        var teams = openings.ToDictionary(o => o.InstanceId, o => o.Team);
        var hp = openings.ToDictionary(o => o.InstanceId, o => o.Hp);
        var remaining = openings.GroupBy(o => o.Team).ToDictionary(g => g.Key, g => g.Count(o => o.Hp > 0));
        var lastEmpty = new Dictionary<int, int>();
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.TargetId is not int target) continue;
            if (e.Kind == BattleEventKind.Summon && e.Team is int team) teams[target] = team;
            if (e.Kind is not (BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Death
                or BattleEventKind.Summon or BattleEventKind.Revive)) continue;
            hp[target] = e.Kind == BattleEventKind.Death ? 0 : e.HpAfter;
            foreach (int side in teams.Values.Distinct())
            {
                int count = hp.Count(p => p.Value > 0 && teams.GetValueOrDefault(p.Key, -1) == side);
                if (remaining.GetValueOrDefault(side) > 0 && count == 0) lastEmpty[side] = i;
                remaining[side] = count;
            }
        }
        return lastEmpty.Where(p => remaining[p.Key] == 0 && Reaps.Contains(p.Value)
            && events[p.Value].ActorId is int actor && teams.TryGetValue(actor, out int side)
            && side != p.Key && remaining.GetValueOrDefault(side) > 0).Select(p => p.Value).ToHashSet();
    }
}
