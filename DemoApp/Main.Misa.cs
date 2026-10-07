using BattleCore;
using System.Threading.Tasks;

public partial class Main
{
    private MisaPresentation _misa = new();
    private bool _misaFastEvent;

    private async Task<bool> PlayMisa(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (e.Kind != BattleEventKind.Feather) return false;
        switch (e.Text)
        {
            case FeatherLabels.Gain:
                target?.MisaFeathers?.SetCount(e.Amount);
                _battleField.ShowFeatherGain(target, _speed);
                break;
            case FeatherLabels.Lost:
                target?.MisaFeathers?.ConfirmLoss(e.Amount);
                break;
            case FeatherLabels.Volley:
                await _battleField.BeginMisaVolley(actor, e.Amount, _speed);
                break;
            case FeatherLabels.Chase:
            case FeatherLabels.Flow:
            case FeatherLabels.Spray:
                if (_result is not null && _misa.HitsByCue.TryGetValue(index, out int hitIndex))
                    await _battleField.PlayMisaShot(actor,
                        _battleField.FindPawn(_result.Events[hitIndex].TargetId), e, _speed,
                        _misa.LosingSprays.Contains(index), _misa.LastShots.Contains(index));
                break;
        }
        return true;
    }
}
