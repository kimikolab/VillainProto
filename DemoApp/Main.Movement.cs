using BattleCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Main
{
    private MovementPresentation _movement = new([]);

    private async Task<bool> PlayMovement(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (MovementPresentation.IsCue(e.Kind))
        {
            double delay = _battleField.ShowMovementCue(e, _speed, _movement.SpringGuards.GetValueOrDefault(index));
            // 掌・味方弾きの接触前に次のMoveを始めない。中断時も待機を解除する。
            if (e.Kind == BattleEventKind.Spring && actor is not null) await actor.PalmStrikeImpact;
            else if (e.Kind == BattleEventKind.Landing && actor is not null) await actor.AllyBumpImpact;
            else if (delay > 0) await Delay(delay);
            return true;
        }
        if (e.Kind == BattleEventKind.Heal && actor?.UnitId == "shio" && target is not null)
            _battleField.MovementLeaves(target);
        if (e.Kind != BattleEventKind.Move) return false;
        _movement.Moves.TryGetValue(index, out var cue);
        bool storm = actor?.UnitId == "basa" && _movement.ShuffleMoves.Contains(index);
        if (storm && _movement.ShuffleStarts.TryGetValue(index, out var shuffles))
        {
            await _battleField.ShowBasaShuffle(actor!, shuffles, _speed);
            if (target is not null && _battleField.FindPawn(e.TargetId) != target) return true;
        }
        if (target is not null)
        {
            // 連撃中の古い立ち位置を次の帰還に持ち越さない。
            _comboEnds.Remove(target);
            _battleField.MoveWithCue(target, e.Slot, cue, actor, storm);
        }
        bool thrown = cue is not null && cue.TargetId == e.TargetId && (cue.Kind is BattleEventKind.Blast or BattleEventKind.Spring
            || cue.Kind == BattleEventKind.KillImpact && cue.Text == ImpactLabels.Blow);
        AppendLog($"  {NameOf(e.TargetId)} → {FormationRules.SeatNames[Math.Clamp(e.Slot, 0, FormationRules.TotalSlots - 1)]}"
            + $"{WriterSuffix(e.ActorId, e.TargetId)}");
        await Delay(storm ? (_movement.ShuffleEnds.Contains(index) ? 0.64 : 0.18)
            : cue?.Kind == BattleEventKind.Tailwind ? 0.20 : thrown ? 0.34 : 0.085);
        return true;
    }
}
