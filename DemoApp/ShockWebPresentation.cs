using BattleCore;
using System.Collections.Generic;

// 連鎖の規模は台本の Slot。糸玉にも ShockSpent が出るので二重には数えない。
internal static class ShockWebPresentation
{
    internal static int[] ChainSources(IReadOnlyList<BattleEvent> events, int index, int count)
    {
        var chain = new List<BattleEvent>();
        var interrupted = new Stack<(int? Actor, List<BattleEvent> Chain)>();
        for (int i = 0; i < index; i++)
        {
            var e = events[i];
            if (e.Kind == BattleEventKind.TurnStart) { chain = new(); interrupted.Clear(); }
            if (e.Kind == BattleEventKind.ShockSpent)
            {
                if (e.Slot == 0) chain = new();
                chain.Add(e);
            }
            if (e.Kind != BattleEventKind.ShockGauge) continue;
            if (e.Text == ShockGaugeLabels.Interrupt) interrupted.Push((e.ActorId, chain));
            // 割り込みの一撃が別の連鎖を起こしても、次のシガが受け取る合図は元の連鎖。
            if (e.Text is ShockGaugeLabels.ChargeSpent or ShockGaugeLabels.ChargeDrained
                && interrupted.TryPeek(out var prior) && prior.Actor == e.ActorId)
                chain = interrupted.Pop().Chain;
        }
        var ids = new List<int>();
        foreach (var e in chain)
            if (e.Team == events[index].Team && e.TargetId is int id && ids.Count < count) ids.Add(id);
        return ids.ToArray();
    }
}
