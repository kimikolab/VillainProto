using BattleCore;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private int _hexMarksShown, _hexSharePlays, _hexShareHits;

    private Task PlayHexShares(int start)
    {
        if (_result is null) return Task.CompletedTask;
        var events = _result.Events;
        int end = HexShareBatch.End(events, start);
        var targets = Enumerable.Range(start, end - start)
            .Select(i => _battleField.FindPawn(events[i].TargetId)).OfType<BattlePawn3D>().Distinct().ToArray();
        foreach (var target in targets) target.AnimationSpeed = Math.Max(0.1, _speed);
        _battleField.ShowCurseTransferParallel(_battleField.FindPawn(events[start].ShareFromId), targets);
        // 共有のHPは確定した拍で反映する。筋の到着を待たず、次の連撃と並行して流す。
        for (int i = start; i < end; i++)
        {
            if (!_batchedDamageIndices.Add(i)) continue;
            ShowDamage(i, events[i], _battleField.FindPawn(events[i].ActorId),
                _battleField.FindPawn(events[i].TargetId), withSource: false);
            _hexShareHits++;
        }
        _hexSharePlays++;
        return Task.CompletedTask;
    }
}
