using BattleCore;
using System.Collections.Generic;

// 勝敗は結果を読む。台本上、最後に敵が退場する位置だけを選ぶ。
public static class FinishSoundCue
{
    public static int Find(bool won, IReadOnlyList<DemoOpening> openings, IReadOnlyList<BattleEvent> events)
    {
        if (!won) return -1;
        var enemies = new HashSet<int>();
        var living = new HashSet<int>();
        foreach (var pawn in openings)
            if (pawn.Team == BattleContext.EnemyTeam)
            {
                enemies.Add(pawn.InstanceId);
                if (pawn.Hp > 0) living.Add(pawn.InstanceId);
            }
        int cue = -1;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.TargetId is not { } id) continue;
            if (e.Kind == BattleEventKind.Summon && e.Team == BattleContext.EnemyTeam) enemies.Add(id);
            if (!enemies.Contains(id)) continue;
            if (e.Kind is BattleEventKind.Summon or BattleEventKind.Revive)
            {
                if (e.HpAfter > 0) living.Add(id);
                cue = -1;
            }
            if (e.Kind == BattleEventKind.Death && living.Remove(id) && living.Count == 0) cue = i;
        }
        return living.Count == 0 ? cue : -1;
    }
}
