using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// 光の出どころと払出しを台本から結ぶ。戦闘判定・ダメージの逆算はしない。
internal sealed partial class SomPresentation
{
    internal sealed record Rain(int[] Pops, HashSet<int> InvertedTargets);
    internal sealed record Call(int[] Summons, int[] Bursts, bool Emergency, int? Partner);
    internal readonly Dictionary<int, Rain> Rains = new();
    internal readonly Dictionary<int, Call> Calls = new();
    internal readonly HashSet<int> CallMembers = new(), DauntedAttacks = new();
    internal readonly HashSet<int> Pops = new(), Heals = new();

    internal static SomPresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var plan = new SomPresentation();
        plan.IndexResonance(events);
        var beastGroups = new Dictionary<int, int>();
        // 群れは召喚と弾けが交互に並ぶが、走りと爆発は最初の召喚で一度に描く。
        for (int i = 0; i + 1 < events.Count; i++)
        {
            if (events[i].Kind != BattleEventKind.Summon || events[i].Text != UnitCatalog.Fodder.Name
                || events[i + 1].Kind != BattleEventKind.BeastBurst
                || events[i].TargetId != events[i + 1].TargetId) continue;
            int leader = i;
            var summons = new List<int>(); var bursts = new List<int>();
            var burst = events[i + 1];
            int count = burst.Text == BeastBurstLabels.Swarm ? burst.StatusRemaining ?? 1 : 1;
            for (int n = 0; n < count && i + 1 < events.Count; n++, i += 2)
            {
                var summon = events[i]; var pop = events[i + 1];
                if (summon.Kind != BattleEventKind.Summon || pop.Kind != BattleEventKind.BeastBurst
                    || summon.ActorId != events[leader].ActorId || summon.TargetId != pop.TargetId
                    || pop.Text != burst.Text) break;
                summons.Add(i); bursts.Add(i + 1);
                if (summon.TargetId is int id) beastGroups[id] = leader;
                plan.CallMembers.Add(i); plan.CallMembers.Add(i + 1);
            }
            plan.Calls[leader] = new(summons.ToArray(), bursts.ToArray(),
                burst.Text == BeastBurstLabels.Emergency, burst.PartnerId);
            i--;
        }
        var chain = new List<int>();
        int? rootGroup = null;
        var daunted = new HashSet<int>();
        var interrupted = new Stack<(int? Actor, List<int> Chain)>();
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Daunted && e.TargetId is int daunt)
            { if (e.Amount > 0) daunted.Add(daunt); else daunted.Remove(daunt); }
            if (e.Kind == BattleEventKind.Attack && e.ActorId is int attacker && daunted.Remove(attacker))
                plan.DauntedAttacks.Add(i);
            if (e.Kind == BattleEventKind.Death && e.TargetId is int dead) daunted.Remove(dead);
            if (e.Kind == BattleEventKind.TurnStart) { chain = new(); rootGroup = null; interrupted.Clear(); }
            if (e.Kind == BattleEventKind.ShockSpent)
            {
                int? group = e.TargetId is int beast && beastGroups.TryGetValue(beast, out int owner) ? owner : null;
                if (e.Slot == 0 && (group is null || group != rootGroup)) chain = new();
                if (e.Slot == 0) rootGroup = group;
                chain.Add(i);
            }
            if (e.Kind == BattleEventKind.ShockGauge)
            {
                if (e.Text == ShockGaugeLabels.Interrupt) interrupted.Push((e.ActorId, chain));
                if (e.Text is ShockGaugeLabels.ChargeSpent or ShockGaugeLabels.ChargeDrained
                    && interrupted.TryPeek(out var prior) && prior.Actor == e.ActorId)
                    chain = interrupted.Pop().Chain;
            }
            if (e.Kind != BattleEventKind.Spark || e.Text is not (SparkLabels.Release or SparkLabels.Silenced)) continue;
            int[] pops = chain.Where(j => events[j].Team != e.Team).Take(Math.Max(0, e.Amount)).ToArray();
            var inverted = new HashSet<int>();
            if (e.Text == SparkLabels.Release)
                for (int j = i + 1; j < events.Count; j++)
                {
                    var next = events[j];
                    if (next.Kind is BattleEventKind.Attack or BattleEventKind.TurnStart or BattleEventKind.Skill
                        or BattleEventKind.Thunder) break;
                    // 反転の払い出しに緊急召喚が挟まっても、元の雨の残りを拾う。
                    if (next.ActorId != e.ActorId) continue;
                    if (next.Kind == BattleEventKind.Heal) plan.Heals.Add(j);
                    if (next.Kind == BattleEventKind.HealInverted && next.TargetId is int id) inverted.Add(id);
                }
            plan.Rains[i] = new(pops, inverted);
            plan.Pops.UnionWith(pops);
        }
        return plan;
    }
}

// 表示用の帳簿。衣の増分と合算破片の既知残量だけで、衣から先に減ったことを追う。
internal sealed class SomVeilLedger
{
    internal int Total { get; private set; }
    internal int Veil { get; private set; }
    internal void Observe(int total)
    {
        total = Math.Max(0, total);
        Veil = Math.Max(0, Veil - Math.Max(0, Total - total));
        Total = total;
        Veil = Math.Min(Veil, Total);
    }
    internal void AddVeil(int amount, int totalAfter)
    {
        amount = Math.Max(0, amount);
        Observe(Math.Max(0, totalAfter - amount));
        Total = Math.Max(0, totalAfter);
        Veil = (int)Math.Min(Total, (long)Veil + amount);
    }
    internal static float Brightness(int amount)
        => amount <= 0 ? 0 : 0.14f + 0.24f * Math.Min(1, MathF.Log2(1 + amount / 7f) / 6f);
}
