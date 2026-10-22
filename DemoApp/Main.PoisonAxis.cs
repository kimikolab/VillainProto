using BattleCore;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Main
{
    private readonly HashSet<int> _poisonAxisShown = new();
    private async Task<bool> PlayPoisonAxis(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        _battleField.SetPoisonClock(_paused, _speed);
        if (e.Kind == BattleEventKind.Attack && actor?.UnitId == "vio" && actor.AttackValue != e.Amount)
            actor.SetAttack(e.Amount); // 吸血に攻撃力の写しは無い。実際の攻撃通知に到達してから合わせる。
        if (e.Kind == BattleEventKind.BloodSip)
        {
            int token = _playToken;
            if (actor is not null && target is not null && e.Amount > 0)
            {
                _battleField.ShowBloodSip(actor, target, e.Amount);
                await Delay(0.45);
                if (token != _playToken || !_battleMode) return true;
                target.SetHp(e.HpAfter); // Damageを捏造せず、啜りの確定HPだけを表示する。
            }
            return true;
        }
        if (e.Kind != BattleEventKind.StatusGain || e.Text != StatusKeys.Poison || e.Amount <= 0) return false;
        if (e.PoisonRoute == PoisonRoute.Vent && actor is not null && !_poisonAxisShown.Contains(index))
        {
            var targets = new List<BattlePawn3D>();
            for (int i = index; i < _result!.Events.Count; i++)
            {
                var next = _result.Events[i];
                if (next.Kind != e.Kind || next.PoisonRoute != e.PoisonRoute || next.ActorId != e.ActorId || next.Turn != e.Turn) break;
                _poisonAxisShown.Add(i); _beniGiftGains.Add(i);
                if (_battleField.FindPawn(next.TargetId) is { } pawn) targets.Add(pawn);
            }
            _battleField.ShowGuzaVent(actor, targets, e.Amount, _speed);
            await Delay(0.32);
        }
        if (e.PoisonRoute == PoisonRoute.Backlash && target is not null)
        {
            var source = _battleField.FindPawn(PoisonAxisPresentation.BacklashSource(_result!.Events, index));
            if (source is not null) _battleField.ShowPoisonRetort(source, target, e.Amount, true);
            _beniGiftGains.Add(index);
            await Delay(0.18);
        }
        if (e.PoisonRoute is PoisonRoute.Strike or PoisonRoute.CloakOpening or PoisonRoute.VenomLeak)
            _beniGiftGains.Add(index);
        return false; // 残量の加算は既存StatusGainの1箇所だけに任せる。
    }
}

internal static class PoisonAxisPresentation
{
    internal static int? BacklashSource(IReadOnlyList<BattleEvent> events, int index)
    {
        var gain = events[index];
        for (int i = index - 1; i >= 0; i--)
        {
            var e = events[i];
            if (e.Turn != gain.Turn || e.Kind == BattleEventKind.TurnStart) break;
            if (e.Kind == BattleEventKind.Damage && e.TargetId == gain.TargetId && e.ActorId != gain.TargetId) return e.ActorId;
            if (e.Kind == BattleEventKind.Attack) return e.ActorId;
        }
        return null;
    }
}
