using BattleCore;
using System;
using System.Collections.Generic;

// 台本に書かれた腕と一撃・死亡だけを結ぶ。捕獲条件や標的選択は再計算しない。
internal sealed class GolmPresentation
{
    internal readonly Dictionary<int, string> Attacks = new();
    internal readonly HashSet<int> CapturingAttacks = new();
    internal readonly HashSet<int> ConsumedDeaths = new();
    internal readonly Dictionary<int, int> ReturnAfterDeath = new();
    internal readonly HashSet<int> FastEvents = new();

    internal static GolmPresentation Build(IReadOnlyList<BattleEvent> events)
    {
        var plan = new GolmPresentation();
        for (int i = 0; i < events.Count; i++)
        {
            var cue = events[i];
            if (cue.Kind == BattleEventKind.Swallow && cue.Text == SwallowLabels.Swallow)
            {
                for (int j = i + 1; j < events.Count; j++)
                {
                    var e = events[j];
                    if (e.Kind is BattleEventKind.TurnStart or BattleEventKind.Skill or BattleEventKind.Attack) break;
                    if (e.Kind == BattleEventKind.Death && e.TargetId == cue.TargetId && e.ActorId == cue.ActorId)
                    {
                        plan.ConsumedDeaths.Add(j);
                        for (int k = i + 1; k <= j; k++) plan.FastEvents.Add(k);
                        break;
                    }
                }
            }
            if (cue.Kind != BattleEventKind.GolmArm || cue.Text is not (GolmArmLabels.Launch or GolmArmLabels.Stomp)) continue;
            int foundAttack = -1;
            for (int j = i + 1; j < events.Count; j++)
            {
                var e = events[j];
                if (e.Kind == BattleEventKind.GolmArm)
                {
                    if (foundAttack >= 0 && e.Text == GolmArmLabels.Grab && e.Slot == 1
                        && e.ActorId == cue.ActorId && e.TargetId == cue.TargetId)
                        plan.CapturingAttacks.Add(foundAttack);
                    break;
                }
                if (e.Kind is BattleEventKind.TurnStart or BattleEventKind.Skill or BattleEventKind.Swallow) break;
                if (e.Kind == BattleEventKind.Attack)
                {
                    if (foundAttack >= 0 || e.ActorId != cue.ActorId) break;
                    plan.Attacks[j] = cue.Text; foundAttack = j;
                }
                if (e.ActorId == cue.ActorId && e.Kind is BattleEventKind.Attack or BattleEventKind.Damage or BattleEventKind.Parry)
                    plan.FastEvents.Add(j);
                if (cue.Text == GolmArmLabels.Launch && e.Kind == BattleEventKind.Death && e.TargetId == cue.TargetId)
                {
                    if (cue.ActorId is int owner) plan.ReturnAfterDeath[j] = owner;
                    break;
                }
            }
        }
        return plan;
    }

    internal readonly record struct SpitPart(string Label, int Amount);
    internal static IReadOnlyList<SpitPart> ParseSpit(string? text)
    {
        var parts = new List<SpitPart>();
        if (text is null || !text.StartsWith(SwallowLabels.Spit + ":", StringComparison.Ordinal)) return parts;
        foreach (string token in text[(text.IndexOf(':') + 1)..].Split(','))
        {
            if (token.Length < 2 || !int.TryParse(token[1..], out int amount) || amount <= 0) continue;
            if (token[..1] is "毒" or "火" or "雷" or "濃" or "標") parts.Add(new(token[..1], amount));
        }
        return parts;
    }
}
