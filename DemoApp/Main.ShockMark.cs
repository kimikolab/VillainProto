using BattleCore;
using System.Threading.Tasks;

public partial class Main
{
    private async Task<bool> PlayShockMark(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (e.Kind == BattleEventKind.Web)
        {
            _battleField.ShowWeb(e, _speed);
            // 帯電は同じターン頭の網全体で重ねる。張るときだけ糸の到着を待つ。
            if (e.Text == WebLabels.Spin) await Delay(0.24);
            return true;
        }
        if (e.Kind == BattleEventKind.SilkBall)
        {
            _battleField.ShowSilkBall(e, _speed);
            if (e.Text == SilkBallLabels.Place) await Delay(0.24);
            return true;
        }
        if (e.Kind == BattleEventKind.MarkLayer)
        {
            _battleField.ShowMarkLayer(target, e.Amount, _speed);
            return true;
        }
        if (e.Kind == BattleEventKind.Scar)
        {
            _battleField.ShowScar(target, e.Slot, e.HpAfter, _speed);
            return true;
        }
        if (e.Kind != BattleEventKind.ShockGauge) return false;
        switch (e.Text)
        {
            case ShockGaugeLabels.ChargeGain:
            case ShockGaugeLabels.ChargeSpent:
            case ShockGaugeLabels.ChargeDrained:
                _battleField.ShowCharge(target, e.Amount, e.Text == ShockGaugeLabels.ChargeGain, _speed);
                break;
            case ShockGaugeLabels.Interrupt:
                _battleField.ShowInterrupt(actor, _battleField.FindPawn(e.PartnerId), _speed);
                await Delay(0.14);
                break;
            case ShockGaugeLabels.Cower:
                _battleField.ShowCower(target, _speed);
                break;
            case ShockGaugeLabels.WhipChain:
                _battleField.ShowWhipChain(actor, e.Slot,
                    ShockWebPresentation.ChainSources(_result!.Events, index, e.Slot), _speed);
                await Delay(0.20);
                break;
            case ShockGaugeLabels.Cloud:
                _battleField.ShowCloud(target, e.Amount, _speed);
                break;
            case ShockGaugeLabels.CloudStrike:
                actor?.SetThundercloud(e.Amount);
                break;
        }
        return true;
    }
}
