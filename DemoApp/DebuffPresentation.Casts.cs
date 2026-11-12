using BattleCore;
using System.Collections.Generic;
using System.Linq;

internal sealed partial class DebuffPresentation
{
    internal sealed record Batch(int Actor, int[] Targets);
    internal readonly Dictionary<int, Batch> NelSinks = new(), NelLeaks = new(), KubiGroups = new();
    internal readonly HashSet<int> NelSinkMembers = new(), NelDirectLeaks = new(), NelCurseGains = new();
    internal readonly HashSet<int> KarzDulls = new(), KubiGains = new(), KubiWeakAttacks = new(), CastSkills = new();

    private void IndexCasts(IReadOnlyList<BattleEvent> events, IReadOnlyList<DemoOpening> openings)
    {
        var units = openings.ToDictionary(o => o.InstanceId);
        bool Is(int? id, string unit) => id is int n && units.TryGetValue(n, out var u) && u.UnitId == unit;
        bool SameTeam(int? a, int? b) => a is int x && b is int y && units.TryGetValue(x, out var u)
            && units.TryGetValue(y, out var v) && u.Team == v.Team;
        var dauntedByKubi = new HashSet<int>();
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.Skill && (Is(e.ActorId, "nel") || Is(e.ActorId, "kubi")
                || Is(e.ActorId, "kari") && e.Text == "呪いを刻む")) CastSkills.Add(i);
            if (e.Kind == BattleEventKind.StatusGain && e.Amount > 0)
            {
                if (Is(e.ActorId, "nel"))
                {
                    if (e.Text == StatusKeys.Curse) NelCurseGains.Add(i);
                    if (e.Text == "dull" && SameTeam(e.ActorId, e.TargetId) && !NelDirectLeaks.Contains(i))
                    {
                        var allies = new List<int>();
                        for (int j = i; j < events.Count; j++)
                        {
                            var n = events[j];
                            if (n.Kind != BattleEventKind.StatusGain || n.ActorId != e.ActorId || n.Text != "dull"
                                || n.Amount <= 0 || n.TargetId is not int id || !SameTeam(n.ActorId, id)) break;
                            NelDirectLeaks.Add(j); allies.Add(id);
                        }
                        NelLeaks[i] = new(e.ActorId!.Value, allies.Distinct().ToArray());
                    }
                    if (e.Text == "dull" && e.TargetId is not null && !SameTeam(e.ActorId, e.TargetId)
                        && !NelSinkMembers.Contains(i))
                    {
                        var targets = new List<int>();
                        for (int j = i; j < events.Count; j++)
                        {
                            var n = events[j];
                            if (n.Kind != BattleEventKind.StatusGain || n.ActorId != e.ActorId || n.Text != "dull"
                                || n.Amount <= 0 || n.TargetId is not int id || SameTeam(n.ActorId, id)) break;
                            NelSinkMembers.Add(j); targets.Add(id);
                        }
                        NelSinks[i] = new(e.ActorId!.Value, targets.Distinct().ToArray());
                    }
                }
                if (Is(e.ActorId, "kari"))
                {
                    if (e.Text == "dull" && e.TargetId is not null && !SameTeam(e.ActorId, e.TargetId)
                        && units[e.ActorId!.Value].Traits?.Contains(TraitId.KariMarkDeepen) == true) KarzDulls.Add(i);
                }
                if (e.Text == StatusKeys.Daunted && e.TargetId is int recipient)
                {
                    if (Is(e.ActorId, "kubi")) dauntedByKubi.Add(recipient);
                    else dauntedByKubi.Remove(recipient);
                    if (Is(e.ActorId, "kubi") && !KubiGains.Contains(i))
                    {
                        var targets = new List<int>();
                        for (int j = i; j < events.Count; j++)
                        {
                            var n = events[j];
                            if (n.Kind != BattleEventKind.StatusGain || n.ActorId != e.ActorId || n.Turn != e.Turn
                                || n.Text != StatusKeys.Daunted || n.Amount <= 0 || n.TargetId is not int id) break;
                            KubiGains.Add(j); targets.Add(id);
                        }
                        KubiGroups[i] = new(e.ActorId!.Value, targets.Distinct().ToArray());
                    }
                }
            }
            if (e.Kind == BattleEventKind.Attack && e.ActorId is int attacker && dauntedByKubi.Remove(attacker))
                KubiWeakAttacks.Add(i);
            if (e.Kind == BattleEventKind.Death && e.TargetId is int dead) dauntedByKubi.Remove(dead);
            if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Daunted && e.Amount <= 0 && e.TargetId is int clear)
                dauntedByKubi.Remove(clear);

            // 呪いの手番の実際の漏れだけを束ねる。隣接や横取り先を画面で選び直さない。
            if (e.Kind != BattleEventKind.Skill || !Is(e.ActorId, "nel")) continue;
            var leaks = new List<int>(); int first = -1;
            for (int j = i + 1; j < events.Count; j++)
            {
                var n = events[j];
                if (n.Kind is BattleEventKind.Skill or BattleEventKind.Attack or BattleEventKind.Charge or BattleEventKind.TurnStart) break;
                int? original = null;
                if (n.Kind == BattleEventKind.StatusGain && n.ActorId == e.ActorId && n.Text == "dull"
                    && n.Amount > 0 && SameTeam(n.ActorId, n.TargetId))
                { original = n.TargetId; NelDirectLeaks.Add(j); }
                else if (Passes.TryGetValue(j, out var pass) && pass.Intake && SameTeam(e.ActorId, pass.Actor)) original = pass.From;
                if (original is not int id) continue;
                if (first < 0) first = j;
                leaks.Add(id);
            }
            if (first >= 0) NelLeaks[first] = new(e.ActorId!.Value, leaks.Distinct().ToArray());
        }
    }
}
