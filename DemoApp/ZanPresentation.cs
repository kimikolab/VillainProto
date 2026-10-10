using BattleCore;
using System.Collections.Generic;
using System.Linq;

// 台本を表示用に束ねるだけ。HP・回復・死亡は元の出来事の位置で読む。
internal sealed class ZanPresentation
{
    internal sealed class Flurry
    {
        internal int Actor, Enemy, Start, End;
        internal readonly List<int> Hits = new();
        internal readonly HashSet<int> Allies = new();
        internal readonly List<int> Recoils = new();
        internal readonly List<MarkRallyPresentation.Rally> Rallies = new();
        internal int Damage;
    }
    internal readonly Dictionary<int, Flurry> Starts = new(), Ends = new(), Hits = new(), Recoils = new();
    internal readonly HashSet<int> FastEvents = new(), RallyStarts = new(), RallyHeals = new();

    internal static ZanPresentation Build(IReadOnlyList<BattleEvent> events, IEnumerable<DemoOpening> opening,
        MarkRallyPresentation rallyPlan)
    {
        var plan = new ZanPresentation();
        var units = opening.ToDictionary(o => o.InstanceId);
        var teams = units.ToDictionary(p => p.Key, p => p.Value.Team);
        Flurry? group = null;
        var lastFlurry = new Dictionary<int, Flurry>();
        void Finish()
        {
            if (group is null) return;
            plan.Starts[group.Start] = group;
            plan.Ends[group.End] = group;
            for (int j = group.Start; j <= group.End; j++) plan.FastEvents.Add(j);
            group = null;
        }
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.Summon && e.TargetId is int summoned && e.Team is int side)
                teams[summoned] = side;
            if (e.Kind is BattleEventKind.Attack or BattleEventKind.TurnStart or BattleEventKind.Death
                or BattleEventKind.FeatherMark or BattleEventKind.Framed or BattleEventKind.VendettaRound
                or BattleEventKind.HushState
                or BattleEventKind.Revive or BattleEventKind.Move or BattleEventKind.Skill or BattleEventKind.Charge)
            {
                Finish();
                if (e.Kind != BattleEventKind.Death) lastFlurry.Clear();
            }
            bool hit = e.Kind is BattleEventKind.Damage or BattleEventKind.Parry
                && e.Reaction && e.Pattern is null && !e.FriendlyFire && !e.Relayed
                && e.ActorId is int id && units.TryGetValue(id, out var unit) && unit.UnitId == "zan"
                && e.TargetId is int target && teams.TryGetValue(target, out int foeTeam) && unit.Team != foeTeam;
            if (hit)
            {
                if (group is not null && (group.Actor != e.ActorId || group.Enemy != e.TargetId)) Finish();
                group ??= new Flurry { Actor = e.ActorId!.Value, Enemy = e.TargetId!.Value, Start = i };
                group.End = i;
                group.Hits.Add(i);
                lastFlurry[group.Actor] = group;
                if (e.Kind == BattleEventKind.Damage) group.Damage += e.Amount;
                // 肩代わり・巻き込みでザン自身への被弾を挟むことがある。
                // 引き金は同じ敵が傷つけた直近の「自分以外の味方」の記録。
                for (int j = i - 1; j >= 0 && events[j].Turn == e.Turn; j--)
                {
                    var hurtEvent = events[j];
                    if (hurtEvent.Kind == BattleEventKind.Framed && hurtEvent.Text == FramedLabels.Accuse)
                    {
                        if (hurtEvent.PartnerId is int framedAlly) group.Allies.Add(framedAlly);
                        break;
                    }
                    if (hurtEvent.Kind == BattleEventKind.Damage && hurtEvent.Amount > 0
                        && hurtEvent.ActorId == group.Enemy && hurtEvent.TargetId is int ally && ally != group.Actor
                        && teams.TryGetValue(ally, out int allyTeam) && allyTeam == units[group.Actor].Team)
                    { group.Allies.Add(ally); break; }
                }
                plan.Hits[i] = group;
            }
            if (e.Kind == BattleEventKind.Damage && e.Reaction && e.FriendlyFire
                && e.ActorId == e.TargetId && e.ActorId is int self && lastFlurry.TryGetValue(self, out var recoil))
            {
                recoil.Recoils.Add(i); plan.Recoils[i] = recoil;
                if (group == recoil) group.End = i;
            }
            if (group is not null && rallyPlan.Starts.TryGetValue(i, out var rally)
                && rally.Cues[0].PartnerId == group.Actor)
            {
                group.Rallies.Add(rally); plan.RallyStarts.Add(i);
            }
            if (group is not null && e.Kind == BattleEventKind.MarkRally && e.PartnerId == group.Actor)
            {
                group.End = i;
                if (rallyPlan.Heals.Contains(i - 1)) plan.RallyHeals.Add(i - 1);
            }
        }
        Finish();
        return plan;
    }
}
