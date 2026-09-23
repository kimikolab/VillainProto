using BattleCore;
using System.Collections.Generic;

// 同じ起点の連続した共有 Damage だけをまとめる。割り込みや死亡を飛び越えない。
internal static class HexShareBatch
{
    public static int End(IReadOnlyList<BattleEvent> events, int start)
    {
        BattleEvent first = events[start];
        int end = start + 1;
        if (first.Kind != BattleEventKind.Damage || first.ShareFromId is null) return end;
        while (end < events.Count)
        {
            BattleEvent next = events[end];
            if (next.Kind != BattleEventKind.Damage || next.ShareFromId != first.ShareFromId
                || next.ActorId != first.ActorId || next.Turn != first.Turn) break;
            end++;
        }
        return end;
    }
}
