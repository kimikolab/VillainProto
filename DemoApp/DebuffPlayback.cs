using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

// 元の台本を保存したまま、同じ手番のウツの間にあるムドの暴発を後ろへ運ぶ表示用の写し。
// 打撃・共有・回復・死亡はブロックごと移し、件数・打点・対象・戦闘結果を変えない。
internal sealed class DebuffPlayback
{
    private static readonly MethodInfo CloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;
    internal BattleResult Source { get; }
    internal BattleResult Result { get; }
    internal int DeferredBursts { get; }
    internal int[] OriginalIndices { get; }
    internal readonly HashSet<int> FinishUtsuBefore = new();
    private static T Copy<T>(T source) where T : class => (T)CloneMethod.Invoke(source, null)!;

    internal DebuffPlayback(BattleResult source, IReadOnlyList<DemoOpening> openings, bool mayReorder = true)
    {
        Source = source;
        var events = source.Events;
        var utsu = UtsuPresentation.Build(events, openings);
        var mudo = MudoPresentation.Build(events, openings);
        var moved = new HashSet<int>();
        var tails = new Dictionary<int, List<int>>();
        int bursts = 0;
        if (mayReorder)
        foreach (var first in utsu.Swings.Where(p => p.Value.Ordinal == 1 && p.Value.Count > 1))
        {
            int end = utsu.Ends.First(p => p.Key > first.Key && p.Value.Contains(first.Value.Actor)).Key;
            int last = utsu.Swings.Where(p => p.Key >= first.Key && p.Key < end && p.Value.Actor == first.Value.Actor).Max(p => p.Key);
            var ranges = mudo.Starts.Where(p => p.Key > first.Key && p.Key < last && p.Value.End <= end).OrderBy(p => p.Key).ToArray();
            // 出現・蘇生・移動を跨ぐ区間では席や個体の寿命を先送りしない。
            if (ranges.Any(r => Enumerable.Range(r.Key, r.Value.End - r.Key).Any(i =>
                events[i].Kind is BattleEventKind.Summon or BattleEventKind.Revive or BattleEventKind.Move
                || events[i].Kind == BattleEventKind.Death && events[i].TargetId == first.Value.Actor))) continue;
            foreach (var r in ranges)
            {
                if (moved.Contains(r.Key)) continue;
                if (!tails.TryGetValue(end, out var tail)) tails[end] = tail = new();
                for (int i = r.Key; i < r.Value.End; i++) if (moved.Add(i)) tail.Add(i);
                bursts++;
            }
            if (tails.TryGetValue(end, out var pending))
            {
                var affected = pending.Where(i => events[i].Kind == BattleEventKind.Damage && events[i].TargetId is not null)
                    .Select(i => events[i].TargetId!.Value).ToHashSet();
                // 先送りした泥のダメージが止めの前提なら、死亡も泥の後へ運ぶ。
                for (int i = first.Key; i < end; i++)
                    if (events[i].Kind == BattleEventKind.Death && events[i].TargetId is int dead && affected.Contains(dead) && moved.Add(i)) pending.Add(i);
            }
        }
        var order = new List<int>();
        for (int i = 0; i <= events.Count; i++)
        {
            if (tails.TryGetValue(i, out var tail)) { FinishUtsuBefore.Add(order.Count); order.AddRange(tail); }
            if (i < events.Count && !moved.Contains(i)) order.Add(i);
        }
        OriginalIndices = order.ToArray(); DeferredBursts = bursts;
        if (bursts == 0) { Result = source; return; }

        // HPAfterは元の順序の絶対値。表示順では、元の確定したHP差分を足してバーが逆戻りするのを防ぐ。
        var originalHp = openings.ToDictionary(o => o.InstanceId, o => o.Hp);
        var originalMax = openings.ToDictionary(o => o.InstanceId, o => o.MaxHp);
        var maxDeltas = new Dictionary<int, int>();
        var deltas = new Dictionary<int, int>();
        static bool WritesHp(BattleEvent e) => e.Kind is BattleEventKind.Damage or BattleEventKind.Heal
            or BattleEventKind.Death or BattleEventKind.Revive or BattleEventKind.Scar;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (!WritesHp(e) || e.TargetId is not int id) continue;
            deltas[i] = e.HpAfter - originalHp.GetValueOrDefault(id, e.HpAfter);
            originalHp[id] = e.HpAfter;
            if (e.Kind == BattleEventKind.Scar)
            {
                maxDeltas[i] = e.Slot - originalMax.GetValueOrDefault(id, e.Slot);
                originalMax[id] = e.Slot;
            }
        }
        var hp = openings.ToDictionary(o => o.InstanceId, o => o.Hp);
        var maxHp = openings.ToDictionary(o => o.InstanceId, o => o.MaxHp);
        var display = new List<BattleEvent>();
        foreach (int i in order)
        {
            var e = events[i];
            if (!deltas.TryGetValue(i, out int delta) || e.TargetId is not int id) { display.Add(e); continue; }
            hp[id] = e.Kind is BattleEventKind.Death or BattleEventKind.Revive ? e.HpAfter : hp.GetValueOrDefault(id) + delta;
            var copy = Copy(e);
            typeof(BattleEvent).GetProperty(nameof(BattleEvent.HpAfter))!.SetValue(copy, Math.Max(0, hp[id]));
            if (maxDeltas.TryGetValue(i, out int maxDelta))
            {
                maxHp[id] = maxHp.GetValueOrDefault(id) + maxDelta;
                typeof(BattleEvent).GetProperty(nameof(BattleEvent.Slot))!.SetValue(copy, maxHp[id]);
            }
            display.Add(copy);
        }
        Result = Copy(source);
        typeof(BattleResult).GetProperty(nameof(BattleResult.Events))!.SetValue(Result, display);
    }
}
