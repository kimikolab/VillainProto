using BattleCore;
using System.Threading.Tasks;

public partial class Main
{
    private TouPowderPresentation _touPowder = new();

    private async Task<bool> PlayTouPowder(BattleEvent e, int index, BattlePawn3D? target)
    {
        if (!_battleField.ShowTouPowder(e, _speed)) return false;
        int token = _playToken;
        // 演出自身の時計で到着を待つ。途中の一時停止・倍速でも先に帯電の印を出さない。
        if (!_fastSmoke && !await _battleField.PowderArrival) return true;
        if (token != _playToken || !_battleMode) return true;
        _battleField.FinishPowderFlow(e);
        if (_touPowder.ContagionArrivals.TryGetValue(index, out var gain) && target is not null)
        {
            target.SetShocked(gain.Amount > 0);
            target.SetStatusIcon(StatusKeys.Shock, gain.Amount > 0);
            SetDisplayedStatus(target, DisplayStatusKey(StatusKeys.Shock), gain.Amount);
        }
        return true;
    }
}
