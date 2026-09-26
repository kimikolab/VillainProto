using BattleCore;
using System.Collections.Generic;
using System.Linq;

public partial class Main
{
    private void ShowAkaGather(int index, BattlePawn3D? aka)
    {
        if (_result is null || aka?.UnitId != "susu") return;
        var sources = new HashSet<int>();
        for (int i = index - 1; i >= 0; i--)
        {
            var e = _result.Events[i];
            if (e.ActorId == aka.InstanceId && (e.Kind == BattleEventKind.Charge
                || e.Kind == BattleEventKind.Skill && e.Text == AshActionLabels.Release)) break;
            if (e.Kind != BattleEventKind.Damage || e.Amount <= 0 || e.TargetId is not int id) continue;
            // 傷の演出の出どころだけを拾う。敵の攻撃から吸い取る絵は出さない。
            if (!e.FriendlyFire && !_statusCauseByDamageIndex.ContainsKey(i)) continue;
            if (_battleField.FindPawn(id)?.Team == aka.Team) sources.Add(id);
        }
        _battleField.ShowBloodGather(sources.Select(id => _battleField.FindPawn(id)).OfType<BattlePawn3D>().ToArray(), aka, _speed);
    }
}
