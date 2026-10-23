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
            if (next.Kind == BattleEventKind.Attack) return false;
            if (next.Kind == BattleEventKind.Highlight) return true;
        }
        return false;
    }
}
