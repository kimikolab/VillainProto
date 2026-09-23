using BattleCore;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Main
{
    internal static int? FindTormentEnd(IReadOnlyList<BattleEvent> events, int attackIndex)
    {
        var attack = events[attackIndex];
        if (attack.Reaction || attack.Relayed) return null;
        for (int i = attackIndex + 1; i < events.Count; i++)
        {
            var next = events[i];
            if (next.Kind == BattleEventKind.TurnStart) break;
            if (next.ActorId != attack.ActorId) continue;
            if (next.Kind is BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.Charge) break;
            // 再生ループの次のindex。ダメージ表示（または受け流し）が済んでから戻る。
            if (IsTormentHit(events, i)) return i + 1;
        }
        return null;
    }

    private async Task PlayTormentHit(int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (actor?.UnitId != "shiga" || target is null || _result is null) return;
        if (IsTormentHit(_result.Events, index))
            await _battleField.ShowTormentHit(actor, target);
    }

    // シガの見せ場の後に来る、型なしの直接ダメージ（または受け流し）。
    // ログ文や敵の拘束状態から追い打ちを推測しない。実際の通知がない空振りも描かない。
    internal static bool IsTormentHit(IReadOnlyList<BattleEvent> events, int index)
    {
        var hit = events[index];
        if (hit.Kind is not (BattleEventKind.Damage or BattleEventKind.Parry)
            || hit.Pattern is not null || hit.Reaction || hit.Relayed || hit.ShareFromId is not null) return false;
        for (int i = index - 1; i >= 0; i--)
        {
            var before = events[i];
            if (before.Kind == BattleEventKind.Highlight) return before.ActorId == hit.ActorId;
            if (before.Kind is BattleEventKind.TurnStart or BattleEventKind.Attack or BattleEventKind.Skill) return false;
            if (before.ActorId == hit.ActorId && before.Kind is BattleEventKind.Damage or BattleEventKind.Parry
                && !before.Relayed && before.ShareFromId is null) return false;
        }
        return false;
    }
}
