using BattleCore;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private int _hexMarksShown, _hexSharePlays, _hexShareHits;

    private async Task PlayHexShares(int start)
    {
        if (_result is null) return;
        var events = _result.Events;
        int end = HexShareBatch.End(events, start);
        int token = _playToken;
        var targets = Enumerable.Range(start, end - start)
            .Select(i => _battleField.FindPawn(events[i].TargetId)).OfType<BattlePawn3D>().Distinct().ToArray();
        foreach (var target in targets) target.AnimationSpeed = Math.Max(0.1, _speed);
        await _battleField.ShowCurseTransfer(_battleField.FindPawn(events[start].ShareFromId), targets, _speed);
        if (token != _playToken || !_battleMode) return;
        // 飛行が終わった同じ拍で全員に数字を出す。元の攻撃者から線や追加攻撃の帯を出さない。
        for (int i = start; i < end; i++)
        {
            if (!_batchedDamageIndices.Add(i)) continue;
            ShowDamage(i, events[i], _battleField.FindPawn(events[i].ActorId),
                _battleField.FindPawn(events[i].TargetId), withSource: false);
            _hexShareHits++;
        }
        _hexSharePlays++;
        await Delay(0.10);
    }
}
