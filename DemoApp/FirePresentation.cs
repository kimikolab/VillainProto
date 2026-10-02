using BattleCore;
using System.Collections.Generic;

// 台本の見出しを、その書き手の次の攻撃だけへ結ぶ。戦闘ルールは読み直さない。
public sealed record FireAttackCue(string Kind, int Level, int Ordinal);
internal enum FireAllyOutcome { Quiet, Heal, Damage }

internal sealed class FirePresentation
{
    internal readonly Dictionary<int, FireAttackCue> Attacks = new();
    internal readonly HashSet<int> FastEvents = new();
    internal readonly Dictionary<int, FireAllyOutcome> AllyOutcomes = new();
    internal static bool ChangesLevel(string? label) => label is FireLevelLabels.Lit
        or FireLevelLabels.GrowSpread or FireLevelLabels.GrowStoke or FireLevelLabels.GrowSelf
        or FireLevelLabels.GrowCall or FireLevelLabels.GrowFoe or FireLevelLabels.GrowSpark
        or FireLevelLabels.GrowCritical or FireLevelLabels.GrowRadiate
        or FireLevelLabels.Wilt or FireLevelLabels.Out or FireLevelLabels.Spent;

    internal FirePresentation(IReadOnlyList<BattleEvent> events)
    {
        var pending = new Dictionary<int, FireAttackCue>();
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.FireArmor && e.Text == FireArmorLabels.BlazeAlly)
                AllyOutcomes[i] = ReadAllyOutcome(events, i);
            if (e.Kind == BattleEventKind.TurnStart) pending.Clear();
            if (e.Kind == BattleEventKind.Death && e.TargetId is int dead) pending.Remove(dead);
            if (e.ActorId is not int actor) continue;
            if (e.Kind == BattleEventKind.FireLevel && e.Text is FireLevelLabels.Stage
                or FireLevelLabels.Lance or FireLevelLabels.Critical or FireLevelLabels.Unleash
                or FireLevelLabels.Burnout or FireLevelLabels.Rain or FireLevelLabels.Embers
                or FireLevelLabels.Blaze or FireLevelLabels.EmbersHit)
                pending[actor] = new(e.Text!, e.Text is FireLevelLabels.Critical or FireLevelLabels.Blaze ? 4
                    : e.Text == FireLevelLabels.Lance ? 3 : e.Text == FireLevelLabels.EmbersHit ? 1 : e.Amount, e.Slot);
            if (e.Kind == BattleEventKind.Attack && pending.Remove(actor, out var cue))
                Attacks[i] = cue;
        }
        foreach (var (index, cue) in Attacks)
        {
            if (cue.Level is 2 or 3 && cue.Kind is FireLevelLabels.Stage or FireLevelLabels.Lance)
                FastEvents.Add(index); // 火槍の着弾とHP表示を揃える。
            if (cue.Kind is not (FireLevelLabels.Burnout or FireLevelLabels.Unleash or FireLevelLabels.Rain
                or FireLevelLabels.Blaze or FireLevelLabels.EmbersHit)) continue;
            // 大技は着弾したフレームからHPへ。通常攻撃の後隙を重ねない。
            FastEvents.Add(index);
            if (cue.Kind is not (FireLevelLabels.Rain or FireLevelLabels.EmbersHit)) continue;
            for (int j = index + 1; j < events.Count; j++)
            {
                var next = events[j];
                if (next.Kind is BattleEventKind.Attack or BattleEventKind.TurnStart or BattleEventKind.Charge or BattleEventKind.Skill) break;
                // 他者の反撃・回復などの間は維持する。イベントを省略・並べ替えない。
                if (next.ActorId == events[index].ActorId && (next.Kind is
                    BattleEventKind.Damage or BattleEventKind.Parry or BattleEventKind.StatusGain
                    || next.Kind == BattleEventKind.FireLevel && (ChangesLevel(next.Text)
                        || next.Text is FireLevelLabels.Rain or FireLevelLabels.EmbersHit)))
                    FastEvents.Add(j);
            }
        }
    }

    // 同じ巻き込み区間の結果だけを読む。ヒヨの生死・特性から結果を推測しない。
    private static FireAllyOutcome ReadAllyOutcome(IReadOnlyList<BattleEvent> events, int index)
    {
        var source = events[index];
        for (int j = index + 1; j < events.Count; j++)
        {
            var e = events[j];
            if (e.Kind is BattleEventKind.Attack or BattleEventKind.TurnStart or BattleEventKind.Skill or BattleEventKind.Charge
                || e.Kind == BattleEventKind.FireArmor && e.Text == FireArmorLabels.BlazeAlly) break;
            if (e.TargetId != source.TargetId) continue;
            if (e.Kind == BattleEventKind.FireArmor && e.Text is FireArmorLabels.Mend or FireArmorLabels.Convert)
                return FireAllyOutcome.Heal; // 満タンでHealが省かれても癒し。
            if (e.Kind == BattleEventKind.Heal) return FireAllyOutcome.Heal;
            if (e.Kind == BattleEventKind.Damage && e.ActorId == source.ActorId) return FireAllyOutcome.Damage;
        }
        return FireAllyOutcome.Quiet;
    }

    internal static string PowerText(FireAttackCue cue, int power) =>
        $"{(cue.Kind == FireLevelLabels.Stage ? $"段{cue.Level}" : cue.Kind)}  一撃の攻 {power}";
}
