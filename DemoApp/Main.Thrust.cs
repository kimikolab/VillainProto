using BattleCore;
using System.Collections.Generic;

public partial class Main
{
    // 先読みするのは当該 Attack の直接被弾だけ。肩代わりや逸らしの分は元の順に残す。
    private IEnumerable<int> ThrustDamageIndices(int index, BattleEvent attack)
    {
        if (_result is null) yield break;
        for (int i = index + 1; i < _result.Events.Count; i++)
        {
            var e = _result.Events[i];
            if (e.Kind == BattleEventKind.TurnStart || (e.Kind == BattleEventKind.Attack && e.ActorId == attack.ActorId)) yield break;
            if (e.Kind is not (BattleEventKind.Damage or BattleEventKind.Parry)) continue;
            if (e.ActorId != attack.ActorId || e.Pattern != attack.Pattern
                || e.Relayed || e.ShareFromId is not null || e.DeflectFromId is not null) continue;
            yield return i;
        }
    }

    private void ApplyThrustDamage(int index, BattleEvent attack, BattlePawn3D pawn)
    {
        foreach (int i in ThrustDamageIndices(index, attack))
        {
            var damage = _result!.Events[i];
            if (damage.TargetId != pawn.InstanceId || !_batchedDamageIndices.Add(i)) continue;
            if (damage.Kind == BattleEventKind.Parry) ShowParry(damage);
            else ShowDamage(i, damage, _battleField.FindPawn(damage.ActorId), pawn, withSource: false);
        }
    }
}
