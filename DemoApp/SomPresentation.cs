using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// 光の出どころと払出しを台本から結ぶ。戦闘判定・ダメージの逆算はしない。
internal sealed class SomPresentation
{
    internal sealed record Rain(int[] Pops, HashSet<int> InvertedTargets);
    internal readonly Dictionary<int, Rain> Rains = new();
    internal readonly HashSet<int> Pops = new(), Heals = new();

    internal static SomPresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var plan = new SomPresentation();
        var chain = new List<int>();
        var interrupted = new Stack<(int? Actor, List<int> Chain)>();
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.TurnStart) { chain = new(); interrupted.Clear(); }
            if (e.Kind == BattleEventKind.ShockSpent)
            {
                if (e.Slot == 0) chain = new();
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
                        or BattleEventKind.ShockSpent or BattleEventKind.Thunder
                        || next.Kind == BattleEventKind.Spark && next.Text != SparkLabels.Veil) break;
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
