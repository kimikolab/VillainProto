using BattleCore;
using System.Collections.Generic;
using System.Linq;

// 被弾とその帰結を台本のIDで結ぶ。火勢から回数・回復量を作らない。
internal sealed class FireHitCue
{
    internal int Contact, Damage = -1, Target;
    internal readonly TurnTickBeat Beat = new();
}

internal sealed class FireHitPresentation
{
    internal readonly Dictionary<int, List<FireHitCue>> Contacts = new();
    internal readonly Dictionary<int, FireHitCue> Sources = new();
    internal readonly List<int> Unpaired = new();

    internal static FireHitPresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var plan = new FireHitPresentation();
        var ticks = TickPresentation.Build(events);
        var actions = new Dictionary<int, int>();
        var hits = new Dictionary<(int Actor, int Target), (int Contact, int Damage)>();
        var latest = new Dictionary<int, (int Contact, int Damage)>();
        bool head = false;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.TurnStart)
            { head = true; actions.Clear(); hits.Clear(); latest.Clear(); }
            if (e.Kind is BattleEventKind.StatusSnapshot or BattleEventKind.StatSnapshot) head = false;
            if (e.Kind is BattleEventKind.Attack or BattleEventKind.Thunder or BattleEventKind.Spring)
            {
                head = false;
                if (e.ActorId is int attacker)
                {
                    actions[attacker] = i;
                    foreach (var key in hits.Keys.Where(k => k.Actor == attacker).ToArray()) hits.Remove(key);
                }
                latest.Clear();
            }
            // 爆炎の巻き込みは被弾の刻みと別の因果。前の敵への一撃に結ばない。
            if (e.Kind == BattleEventKind.FireArmor && e.Text == FireArmorLabels.BlazeAlly) latest.Clear();
            if (e.Kind == BattleEventKind.Death && e.TargetId is int dead)
            {
                latest.Remove(dead);
                foreach (var key in hits.Keys.Where(k => k.Target == dead).ToArray()) hits.Remove(key);
            }
            if (e.Kind == BattleEventKind.Damage && e.ActorId is int a && e.TargetId is int t
                && !e.Relayed && e.ShareFromId is null && e.DeflectFromId is null
                && !ticks.Outcomes.ContainsKey(i) && !plan.Sources.ContainsKey(i))
            {
                int contact = actions.GetValueOrDefault(a, i);
                hits[(a, t)] = (contact, i);
                latest[t] = (contact, i);
            }
            bool status = e.Kind == BattleEventKind.Status && e.Text == "燃焼" && e.ActorId is not null;
            bool conversion = !head && e.Kind == BattleEventKind.FireArmor
                && e.Text is FireArmorLabels.Convert or FireArmorLabels.Mend;
            if ((!status && !conversion) || e.TargetId is not int target) continue;
            (int Contact, int Damage) anchor;
            bool found = status ? hits.TryGetValue((e.ActorId!.Value, target), out anchor)
                : latest.TryGetValue(target, out anchor);
            if (!found && status && actions.TryGetValue(e.ActorId!.Value, out int action)
                && events[action].TargetId == target) { anchor = (action, -1); found = true; }
            if (!found) { if (status) plan.Unpaired.Add(i); continue; }
            if (!plan.Contacts.TryGetValue(anchor.Contact, out var cues))
                plan.Contacts[anchor.Contact] = cues = new();
            var cue = cues.FirstOrDefault(c => c.Target == target && c.Damage == anchor.Damage);
            if (cue is null)
            {
                cue = new() { Contact = anchor.Contact, Damage = anchor.Damage, Target = target };
                cue.Beat.TargetId = target;
                cues.Add(cue);
            }
            cue.Beat.Roots.Add(i);
            cue.Beat.Burn = true;
            cue.Beat.FirePulses++;
            cue.Beat.FireHeal |= conversion || e.SourceTrait == TraitId.Inverse;
            plan.Sources[i] = cue;
            if (status)
            {
                foreach (var outcome in ticks.Outcomes.Where(p => p.Value.Start == i))
                { cue.Beat.AddNumber(true, events[outcome.Key]); plan.Sources[outcome.Key] = cue; }
            }
            else
            {
                for (int j = i + 1; j < events.Count; j++)
                {
                    var next = events[j];
                    if (next.Kind is BattleEventKind.Status or BattleEventKind.FireArmor or BattleEventKind.FireLevel
                        or BattleEventKind.Attack or BattleEventKind.Death or BattleEventKind.TurnStart) break;
                    if (next.Kind != BattleEventKind.Heal || next.TargetId != target || next.ActorId != e.ActorId) continue;
                    cue.Beat.AddNumber(true, next); plan.Sources[j] = cue; break;
                }
            }
        }
        return plan;
    }
}
