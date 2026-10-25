using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// 戦闘中に観測した実値と、確定済みイベントの索引だけを持つ。標的・ダメージ・魂を計算しない。
internal sealed class HagiPresentation
{
    internal readonly record struct State(int Id, int Souls, int Marks, int LinePermil);
    internal sealed record Swing(int Actor, int[] Damage, bool Reap, bool Pursuit, bool Link, bool Keep = false);
    internal readonly SortedDictionary<int, List<State>> Frames = new();
    internal readonly Dictionary<int, Swing> Swings = new();
    internal readonly HashSet<int> Reaps = new();
    private readonly Dictionary<int, State> _last = new();
    private static readonly MethodInfo Line = typeof(BattleContext).GetMethod("ReapLineFor389", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("刈り取り線の観測先が見つかりません");

    internal void Capture(int index, IReadOnlyList<UnitState> units, BattleContext context)
    {
        var holders = units.Where(u => u.IsAlive && u.HasTrait(TraitId.HagiReap)).ToArray();
        foreach (var unit in units)
        {
            int line = 0;
            foreach (var holder in holders.Where(h => h.TeamId != unit.TeamId))
                line = Math.Max(line, (int)Line.Invoke(context, new object[] { holder, unit, HagiReaperTrait.ReapLinePermil(holder) })!);
            var state = new State(unit.InstanceId, unit.RawCounter(HagiDepthTrait.SoulKey), unit.RawCounter(HagiDepthTrait.MarkKey), line);
            if (_last.TryGetValue(state.Id, out var old) && state == old) continue;
            _last[state.Id] = state;
            if (!Frames.TryGetValue(index, out var frame)) Frames[index] = frame = new();
            frame.RemoveAll(s => s.Id == state.Id); frame.Add(state);
        }
    }

    internal static HagiPresentation Build(IReadOnlyList<BattleEvent> events, IReadOnlyList<DemoOpening> openings, HagiPresentation? recorded)
    {
        var result = new HagiPresentation();
        if (recorded is not null)
            foreach (var frame in recorded.Frames) result.Frames[frame.Key] = frame.Value;
        var hagi = openings.Where(o => o.UnitId == "hagi").Select(o => o.InstanceId).ToHashSet();
        // ReapBody388 は Highlight → Damage をこの順で出す。文言や残HPから必殺を推測しない。
        for (int i = 1; i < events.Count; i++)
        {
            var e = events[i]; var before = events[i - 1];
            if (e.Kind == BattleEventKind.Damage && e.ActorId is int id && hagi.Contains(id)
                && !e.FriendlyFire && e.HpAfter == 0 && before.Kind == BattleEventKind.Highlight && before.ActorId == id)
                result.Reaps.Add(i);
        }
        var latest = new Dictionary<int, int>();
        var damage = new Dictionary<int, List<int>>();
        var linked = new HashSet<int>();
        int? lastAttacker = null;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.TurnStart) { latest.Clear(); lastAttacker = null; }
            if (e.Kind == BattleEventKind.Attack)
            {
                if (e.ActorId is int id && hagi.Contains(id))
                {
                    if (lastAttacker == id) linked.Add(i);
                    latest[id] = i; damage[i] = new();
                }
                lastAttacker = e.ActorId;
            }
            if (e.Kind == BattleEventKind.Damage && !e.FriendlyFire && e.ActorId is int source
                && hagi.Contains(source) && latest.TryGetValue(source, out int attack))
                damage[attack].Add(i);
        }
        foreach (var (index, hits) in damage)
            result.Swings[index] = new(events[index].ActorId!.Value, hits.ToArray(), hits.Any(result.Reaps.Contains), events[index].Reaction, linked.Contains(index));
        foreach (int index in linked)
        {
            int prior = result.Swings.Keys.Where(i => i < index).DefaultIfEmpty(-1).Max();
            if (prior >= 0 && result.Swings[prior].Actor == result.Swings[index].Actor)
                result.Swings[prior] = result.Swings[prior] with { Keep = true };
        }
        return result;
    }
}
