using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// 確定済みの台本だけを読む。線・魂・段・刈れる相手を戦闘の規則から求め直さない。
internal sealed class HagiPresentation
{
    internal readonly record struct State(int Id, int Souls, int Marks, int LinePermil, int Stage = 0, int BaseLinePermil = 0);
    internal sealed record Swing(int Actor, int[] Damage, bool Reap, bool Pursuit, bool Link, bool Keep = false,
        bool Return = false, bool Reverse = false, int Encore = 0, bool Weighted = false, int Kind = 0, int? Partner = null);
    internal readonly SortedDictionary<int, List<State>> Frames = new();
    internal readonly Dictionary<int, Swing> Swings = new();
    internal readonly Dictionary<int, Swing> Returns = new();
    internal readonly HashSet<int> Reaps = new();
    internal readonly HashSet<int> HeavyHits = new();
    internal readonly HashSet<int> FastEvents = new();
    internal readonly Dictionary<int, int> ReapKinds = new();

    // DeathPresentation の既存の観測口との互換だけ残す。反射・カウンタの副台本は持たない。
    internal void Capture(int index, IReadOnlyList<UnitState> units, BattleContext context) { }

    internal static HagiPresentation Build(IReadOnlyList<BattleEvent> events, IReadOnlyList<DemoOpening> openings, HagiPresentation? recorded)
    {
        var result = new HagiPresentation();
        var hagi = openings.Where(o => o.UnitId == "hagi").Select(o => o.InstanceId).ToHashSet();
        var states = openings.ToDictionary(o => o.InstanceId, o => new State(o.InstanceId, 0, 0, 0));
        result.Frames[0] = states.Values.ToList();
        void Save(int index, State state)
        {
            states[state.Id] = state;
            if (!result.Frames.TryGetValue(index, out var frame)) result.Frames[index] = frame = new();
            frame.RemoveAll(s => s.Id == state.Id); frame.Add(state);
        }
        // 「刈る」と対応する Damage を結ぶ。間の Highlight や振る前の Attack は表示順のまま通す。
        var pending = new Dictionary<(int Actor, int Target), (int Index, BattleEvent Event)>();
        var reapAt = new Dictionary<int, (int Hit, BattleEvent Event)>();
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.TurnStart) pending.Clear();
            if (e.Kind == BattleEventKind.Reap && e.ActorId is int id && hagi.Contains(id))
            {
                var state = states[id];
                switch (e.Text)
                {
                    case ReapLabels.Soul when e.StatusRemaining is int souls && souls is >= 0 and <= 9:
                        Save(i, state with { Souls = souls }); break;
                    case ReapLabels.Stage when e.Amount is >= 0 and <= 3 && e.Slot is >= 0 and <= 1000
                        && e.StatusRemaining is int souls && souls is >= 0 and <= 9:
                        Save(i, state with { Stage = e.Amount, Souls = souls, BaseLinePermil = e.Slot }); break;
                    case ReapLabels.Mark or ReapLabels.Line when e.TargetId is int target
                        && e.Amount is >= 0 and <= 5 && e.StatusRemaining is int line && line is >= 0 and <= 1000:
                        Save(i, states.GetValueOrDefault(target, new State(target, 0, 0, 0)) with { Marks = e.Amount, LinePermil = line }); break;
                    case ReapLabels.Reap when e.TargetId is int target && e.Slot is >= ReapLabels.KindTurn and <= ReapLabels.KindTwin:
                        pending[(id, target)] = (i, e); break;
                }
            }
            if (e.Kind == BattleEventKind.Damage && e.ActorId is int actor && e.TargetId is int victim
                && pending.Remove((actor, victim), out var reap) && !e.FriendlyFire && e.HpAfter == 0 && e.Amount == reap.Event.Amount)
            {
                result.Reaps.Add(i); result.ReapKinds[i] = reap.Event.Slot;
                reapAt[reap.Index] = (i, reap.Event);
            }
        }
        var latest = new Dictionary<int, int>();
        var hits = new Dictionary<int, List<int>>();
        var encore = new Dictionary<int, int>();
        var twin = new HashSet<int>();
        var pursuit = new Dictionary<int, int?>();
        var weighted = new HashSet<int>();
        var heavy = new HashSet<(int, int)>();
        var beforeReap = new Dictionary<int, int>();
        var cues = new SortedDictionary<int, Swing>();
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.TurnStart)
            { latest.Clear(); encore.Clear(); twin.Clear(); pursuit.Clear(); weighted.Clear(); heavy.Clear(); beforeReap.Clear(); }
            if (e.ActorId is not int id || !hagi.Contains(id)) continue;
            if (e.Kind == BattleEventKind.Reap)
            {
                switch (e.Text)
                {
                    case ReapLabels.Encore when e.Amount is 1 or 2: encore[id] = e.Amount; break;
                    case ReapLabels.Twin when e.Amount is 0 or 1: twin.Add(id); break;
                    case ReapLabels.Pursue when e.Slot is 0 or 1 && e.StatusRemaining is 0 or 1:
                        pursuit[id] = e.PartnerId;
                        // 仲間の命中後の待ちだけを詰める。別の攻撃や手番を越えて結ばない。
                        if (e.PartnerId is int partner)
                            for (int j = i - 1; j >= 0; j--)
                            {
                                var prior = events[j];
                                if (prior.Kind is BattleEventKind.TurnStart or BattleEventKind.Attack) break;
                                if (prior.Kind == BattleEventKind.Damage && prior.ActorId == partner && prior.TargetId == e.TargetId)
                                { for (int k = j; k < i; k++) result.FastEvents.Add(k); break; }
                            }
                        break;
                    case ReapLabels.Weight: weighted.Add(id); break;
                    case ReapLabels.Heavy when e.TargetId is int target: heavy.Add((id, target)); break;
                }
                if (reapAt.TryGetValue(i, out var reap))
                {
                    int kind = reap.Event.Slot;
                    if (kind is ReapLabels.KindAfter or ReapLabels.KindAfterChase or ReapLabels.KindColumn)
                    {
                        var prior = latest.TryGetValue(id, out int attack) ? cues[attack] : null;
                        cues[i] = new(id, new[] { reap.Hit }, true, false, true, Return: true,
                            Reverse: prior is null || !prior.Reverse, Encore: prior?.Encore ?? 0, Kind: kind);
                    }
                    else beforeReap[id] = kind;
                }
            }
            if (e.Kind == BattleEventKind.Attack)
            {
                int repeat = encore.GetValueOrDefault(id);
                bool reverse = twin.Remove(id);
                int kind = beforeReap.GetValueOrDefault(id); beforeReap.Remove(id);
                bool chasing = pursuit.Remove(id, out int? partner);
                cues[i] = new(id, Array.Empty<int>(), kind != 0, chasing, reverse || repeat > 0,
                    Reverse: reverse, Encore: repeat, Weighted: weighted.Remove(id), Kind: kind, Partner: partner);
                encore.Remove(id); latest[id] = i; hits[i] = new();
            }
            if (e.Kind == BattleEventKind.Damage && !e.FriendlyFire && e.TargetId is int victim)
            {
                if (heavy.Remove((id, victim))) result.HeavyHits.Add(i);
                if (latest.TryGetValue(id, out int attack)
                    && (!result.ReapKinds.TryGetValue(i, out int kind) || kind is not (ReapLabels.KindAfter or ReapLabels.KindAfterChase or ReapLabels.KindColumn)))
                    hits[attack].Add(i);
            }
        }
        foreach (var (index, damage) in hits)
        {
            cues[index] = cues[index] with { Damage = damage.ToArray() };
            if (cues[index].Weighted) result.HeavyHits.UnionWith(damage.Where(i => !result.Reaps.Contains(i)));
            if (damage.Any(result.HeavyHits.Contains)) cues[index] = cues[index] with { Weighted = true };
        }
        var ordered = cues.Keys.ToArray();
        for (int n = 0; n < ordered.Length; n++)
        {
            int i = ordered[n]; var cue = cues[i];
            if (n + 1 < ordered.Length)
            {
                int next = ordered[n + 1]; var following = cues[next];
                bool boundary = events.Skip(i + 1).Take(next - i - 1).Any(e => e.Kind == BattleEventKind.TurnStart
                    || e.Kind == BattleEventKind.Attack && e.ActorId != cue.Actor
                    || e.Kind == BattleEventKind.Death && e.TargetId == cue.Actor);
                if (following.Actor == cue.Actor && following.Link && !boundary)
                {
                    cue = cue with { Keep = true };
                    for (int k = i; k < next; k++) result.FastEvents.Add(k);
                }
            }
            if (cue.Return) result.Returns[i] = cue;
            else result.Swings[i] = cue;
        }
        return result;
    }
}
