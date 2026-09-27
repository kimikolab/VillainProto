using BattleCore;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Main
{
    private readonly HashSet<int> _mireBurstsShown = new();

    private bool MireFollowsDeath(int index, int? dead)
    {
        for (int j = index + 1; j < _result!.Events.Count; j++)
        {
            var next = _result.Events[j];
            if (next.Kind == BattleEventKind.MireBurst) return next.SpreadFromId == dead;
            if (next.Kind is BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.TurnStart
                or BattleEventKind.Status or BattleEventKind.Death) break;
        }
        return false;
    }

    private async Task<bool> PlayMire(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (e.Kind == BattleEventKind.Damage)
        {
            for (int j = index - 1; j >= 0; j--)
            {
                var prior = _result!.Events[j];
                if (prior.Kind == BattleEventKind.MireBurst)
                {
                    if (prior.TargetId == e.TargetId && prior.ActorId == e.ActorId) _tickDelayBudget = 0.04;
                    break;
                }
                if (prior.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Attack
                    or BattleEventKind.TurnStart or BattleEventKind.Status or BattleEventKind.Death) break;
            }
            return false;
        }
        if (e.Kind == BattleEventKind.LiveWire)
        {
            actor?.SetStatusIcon(StatusKeys.Shock, false);
            if (actor is not null) SetDisplayedStatus(actor, DisplayStatusKey(StatusKeys.Shock), 0);
            return true;
        }
        if (e.Kind == BattleEventKind.ConcentrateMark && index + 1 < _result!.Events.Count
            && _result.Events[index + 1].Kind is BattleEventKind.MireCarried or BattleEventKind.MireHandedOff)
        {
            // 印の更新は移動の着地で行う。同じ付与を通常の渦でもう一度描かない。
            _battleField.ConcentratePlays++;
            return true;
        }
        int token = _playToken;
        if (e.Kind == BattleEventKind.MireSlam)
        {
            _tickDelayBudget = null;
            _battleField.ShowMireSlam(actor, target, e.Slot == 1, _speed);
            await Delay(0.42);
            return true;
        }
        if (e.Kind == BattleEventKind.MireConduct)
        {
            _tickDelayBudget = null;
            _battleField.ShowMireConduct(_battleField.FindPawn(e.SpreadFromId), target, _speed);
            await Delay(0.16);
            return true;
        }
        if (e.Kind is BattleEventKind.MireCarried or BattleEventKind.MireHandedOff)
        {
            _tickDelayBudget = null;
            _battleField.ShowMireFlow(_battleField.FindPawn(e.SpreadFromId), target,
                e.Kind == BattleEventKind.MireCarried, e.Amount, _speed);
            await Delay(0.26);
            if (token == _playToken && _battleMode) target?.SetConcentrated(e.StatusRemaining ?? 0);
            return true;
        }
        if (e.Kind != BattleEventKind.MireBurst) return false;
        if (!_mireBurstsShown.Add(index)) return true;
        // 同じ死から隣へ飛ぶ複数の通知は一発。HPと死亡は台本の元の順序で読む。
        var batch = new List<BattleEvent>();
        for (int j = index; j < _result!.Events.Count; j++)
        {
            var next = _result.Events[j];
            if (next.Kind == BattleEventKind.TurnStart || next.Turn != e.Turn) break;
            if (next.Kind != BattleEventKind.MireBurst || next.SpreadFromId != e.SpreadFromId) continue;
            batch.Add(next);
            _mireBurstsShown.Add(j);
        }
        _tickDelayBudget = null;
        _battleField.ShowMireBurst(_battleField.FindPawn(e.SpreadFromId), batch, _speed);
        await Delay(e.Slot > 1 ? 0.24 : 0.34);
        return true;
    }
}
