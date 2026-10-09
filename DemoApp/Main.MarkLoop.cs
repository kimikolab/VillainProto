using BattleCore;
using System.Threading.Tasks;

public partial class Main
{
    private MarkLoopPresentation _markLoop = new();

    private async Task<bool> PlayMarkLoop(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        switch (e.Kind)
        {
            case BattleEventKind.FeatherMark:
                await _battleField.PlayFeatherMark(actor, target, e, _speed);
                return true;
            case BattleEventKind.Framed:
                _battleField.ShowFramed(actor, target, e, _speed);
                if (e.Text == FramedLabels.Accuse) await Delay(0.30);
                return true;
            case BattleEventKind.BeckonFeather:
                _battleField.ShowBeckonFeather(target, _battleField.FindPawn(e.PartnerId), e.Amount, _speed);
                await Delay(0.09);
                return true;
            case BattleEventKind.ShareGive when e.Text == ShareGiveLabels.Power:
                await _battleField.ShowSharePower(actor, target, e, _speed);
                return true;
            case BattleEventKind.VendettaRound:
                if (e.Text == VendettaRoundLabels.Start)
                {
                    _battleField.ShowRoundStart(actor, e, _speed);
                    await Delay(0.18);
                }
                else if (e.Text == VendettaRoundLabels.Slash)
                    await _battleField.ShowRoundSlash(actor, target, e, _markLoop.LastSlashes.Contains(index), _speed);
                return true;
        }
        if (_markLoop.RoundAttacks.Contains(index))
        {
            // 太刀の見出しで絵を出した。通常の踏み込みと帰還を重ねない。
            _attackPlays++;
            AppendLog($"  [color=#ff8199]【仇巡り】{NameOf(e.ActorId)} → {NameOf(e.TargetId)}  {e.Amount}[/color]");
            return true;
        }
        return false;
    }

    private async Task FinishMarkLoop(int index)
    {
        if (!_markLoop.RoundEnds.TryGetValue(index, out int actor)) return;
        _battleField.FindPawn(actor)?.ReturnFromRound();
        await Delay(0.16);
    }
}
