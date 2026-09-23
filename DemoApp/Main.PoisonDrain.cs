using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private readonly HashSet<int> _poisonDrainIndices = new();
    private int _poisonDrainPlays, _poisonDrainHits;

    private void SetDisplayedStatus(BattlePawn3D pawn, string label, int amount)
    {
        if (!_statusByPawn.TryGetValue(pawn.InstanceId, out var amounts))
            _statusByPawn[pawn.InstanceId] = amounts = new();
        if (amount > 0) amounts[label] = amount;
        else amounts.Remove(label);
        pawn.SetStatus(string.Join("  ", amounts.Select(pair => $"{pair.Key}{pair.Value}")));
    }

    private async Task PlayPoisonDrain(int start)
    {
        if (_result is null) return;
        var events = _result.Events;
        int end = PoisonDrainBatch.End(events, start);
        var receiver = _battleField.FindPawn(events[start].ActorId);
        int token = _playToken;
        var sources = Enumerable.Range(start, end - start)
            .Select(i => _battleField.FindPawn(events[i].TargetId)).OfType<BattlePawn3D>().Distinct().ToArray();
        if (receiver is not null)
        {
            receiver.AnimationSpeed = Math.Max(0.1, _speed);
            await _battleField.ShowPoisonDrain(sources, receiver, _speed);
        }
        if (token != _playToken || !_battleMode) return;
        for (int i = start; i < end; i++)
        {
            if (!_poisonDrainIndices.Add(i)) continue;
            var e = events[i];
            var source = _battleField.FindPawn(e.TargetId);
            if (source is not null && e.StatusRemaining is int remaining)
            {
                source.SetPoisonRemaining(remaining);
                SetDisplayedStatus(source, DisplayStatusKey(StatusKeys.Poison), remaining);
                if (remaining <= 0) _poisonedSnapshot.Remove(source.InstanceId);
                else _poisonedSnapshot.Add(source.InstanceId);
            }
            _poisonDrainHits++;
        }
        // 受け取った毒はヴィオの毒状態には足さない。最後の台本の攻撃力へ一度だけ更新する。
        var last = events[end - 1];
        if (receiver is not null && last.DrainLast && last.AttackAfter is int attack)
        {
            int change = attack - receiver.AttackValue;
            receiver.SetAttack(attack);
            _battleField.PlayAttackChangeSound(change);
        }
        _poisonDrainPlays++;
        await Delay(0.20);
    }
}

// 同じ通し番号の連続部分だけを束ねる。別の吸収者・状態・ターンや末尾を越えない。
internal static class PoisonDrainBatch
{
    public static int End(IReadOnlyList<BattleEvent> events, int start)
    {
        var first = events[start];
        int end = start + 1;
        if (first.Kind != BattleEventKind.StatusDrain || first.DrainSeq is null) return end;
        while (!events[end - 1].DrainLast && end < events.Count)
        {
            var next = events[end];
            if (next.Kind != first.Kind || next.DrainSeq != first.DrainSeq
                || next.ActorId != first.ActorId || next.Text != first.Text || next.Turn != first.Turn) break;
            end++;
        }
        return end;
    }
}
