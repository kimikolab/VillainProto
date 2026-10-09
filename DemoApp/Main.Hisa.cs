using BattleCore;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Main
{
    private HisaCommandPresentation _hisaCommand = new();
    private async Task<bool> PlayHisaCommand(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (_hisaCommand.DeferredLayers.Contains(index) || _hisaCommand.DeferredMarks.Contains(index)) return true;
        switch (e.Kind)
        {
            case BattleEventKind.CommandBall:
                _battleField.ShowCommandBall(actor, e, _speed);
                return true;
            case BattleEventKind.Command:
                await _battleField.ShowCommand(actor, target, e, _hisaCommand.Layers.GetValueOrDefault(index), _speed,
                    _hisaCommand.FreshCommands.Contains(index));
                return true;
            case BattleEventKind.Cover:
                await _battleField.ShowHisaCover(actor, target, _battleField.FindPawn(e.PartnerId), _speed);
                return true;
            case BattleEventKind.Framed when e.Text == FramedLabels.Silenced:
                _battleField.ShowHisaQuiet(actor, e.Turn, _speed);
                return true;
            case BattleEventKind.Sealed when e.Text == SealedLabels.Hush:
                _battleField.ShowHisaQuiet(target, e.Turn, _speed);
                break;
        }
        return false;
    }
}
