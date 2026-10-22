using BattleCore;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Main
{
    private async Task PlayRauTransfers(BattleEvent e, int index, BattlePawn3D? actor)
    {
        if (_poisonAxisShown.Contains(index)) return;
        var events = _result!.Events;
        int end = RauPresentation.BatchEnd(events, index);
        var arrivals = new List<Task>();
        bool leak = e.PoisonRoute == PoisonRoute.TouchLeak;
        bool contagion = e.PoisonRoute == PoisonRoute.Contagion;
        if (leak || contagion) actor?.RauPose(true, _speed);
        for (int i = index; i < end; i++)
        {
            var gain = events[i];
            _poisonAxisShown.Add(i);
            if (_battleField.FindPawn(gain.TargetId) is not { } target) continue;
            if (contagion)
                arrivals.Add(_battleField.ShowRauContagion(target, gain.Amount).Arrival);
            else
            {
                var source = _battleField.FindPawn(leak ? gain.ActorId : gain.SpreadFromId);
                if (source is null) continue;
                arrivals.Add(_battleField.ShowPoisonTransfer(source, target, leak, _speed, amount: gain.Amount));
                if (leak) _poisonLeakPlays++; else _poisonSpreadPlays++;
            }
        }
        // 同じ経路で続く実対象へ同時に飛ばす。残量は到着後のStatusGainだけが加算する。
        await Task.WhenAll(arrivals);
    }
}

internal static class RauPresentation
{
    internal static bool IsTransfer(BattleEvent e) => e.Kind == BattleEventKind.StatusGain
        && e.Text == StatusKeys.Poison && e.PoisonRoute is PoisonRoute.Touch or PoisonRoute.TouchLeak or PoisonRoute.Contagion;

    internal static int BatchEnd(IReadOnlyList<BattleEvent> events, int index)
    {
        var first = events[index];
        if (!IsTransfer(first) || first.Amount <= 0) return index + 1;
        int end = index + 1;
        while (end < events.Count)
        {
            var next = events[end];
            if (!IsTransfer(next) || next.Amount <= 0 || next.Turn != first.Turn
                || next.ActorId != first.ActorId || next.PoisonRoute != first.PoisonRoute
                || next.SpreadFromId != first.SpreadFromId) break;
            end++;
        }
        return end;
    }
}
