using BattleCore;
using System.Collections.Generic;

// Skill は音が足りず殴る手番にも出る。直後の台本で、奏でた結果の見せ場だけを拾う。
// 音の閾値やログ本文から歌唱を推測しない。
internal static class ErePresentation
{
    public static bool IsSongSkill(IReadOnlyList<BattleEvent> events, int index)
    {
        BattleEvent skill = events[index];
        if (skill.Kind != BattleEventKind.Skill || skill.ActorId is null) return false;
        for (int i = index + 1; i < events.Count; i++)
        {
            BattleEvent next = events[i];
            if (next.Kind is BattleEventKind.TurnStart or BattleEventKind.Skill or BattleEventKind.Charge)
                return false;
            if (next.ActorId != skill.ActorId) continue;
            if (next.Kind == BattleEventKind.Dirge && next.Text == DirgeLabels.Gift && next.Slot == 1) return true;
            if (next.Kind == BattleEventKind.Attack) return false;
            if (next.Kind == BattleEventKind.Highlight) return true;
        }
        return false;
    }

    public static bool IsGiftHighlight(IReadOnlyList<BattleEvent> events, int index)
    {
        if (index == 0 || events[index].Kind != BattleEventKind.Highlight) return false;
        BattleEvent previous = events[index - 1];
        return previous.Kind == BattleEventKind.Dirge
            && events[index].ActorId == (previous.Text == DirgeLabels.Act ? previous.TargetId : previous.ActorId);
    }

    public static bool HasFollowingGift(IReadOnlyList<BattleEvent> events, int index, int? singer)
    {
        for (int i = index + 1; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind is BattleEventKind.TurnStart or BattleEventKind.Skill or BattleEventKind.Attack or BattleEventKind.Charge
                || e.Kind == BattleEventKind.Dirge && e.Text == DirgeLabels.Act) return false;
            if (e.Kind == BattleEventKind.Dirge && e.Text == DirgeLabels.Gift && e.ActorId == singer) return true;
        }
        return false;
    }
}
