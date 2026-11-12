using BattleCore;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private async Task<bool> PlayDebuffCast(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        // 手数・引受の補助通知は数値の浮き文字にしない。
        if (e.Kind == BattleEventKind.StatusGain && e.Text is BattleContext.UtsuHitsText or BattleContext.WataTakeText) return true;
        if (e.Kind == BattleEventKind.StatusGain && e.Text == BattleContext.KarzMarkText)
        {
            if (e.Amount > 0 && actor is not null && target is not null) _battleField.ShowKarzMark(actor, target);
            return true;
        }
        BattlePawn3D[] Recipients(DebuffPresentation.Batch batch)
            => batch.Targets.Select(id => _battleField.FindPawn(id)).OfType<BattlePawn3D>().ToArray();
        if (_debuff.NelLeaks.TryGetValue(index, out var leak) && _battleField.FindPawn(leak.Actor) is { } writer)
            _battleField.ShowNelLeak(writer, Recipients(leak));
        if (_debuff.NelCurseGains.Contains(index) && actor is not null && target is not null)
        {
            target.UseNelCurseTalisman();
            target.SetStatusIcon(StatusKeys.Curse, true);
            _hexMarksShown++;
            await _battleField.ShowNelTalisman(actor, target);
            return true;
        }
        if (_debuff.NelSinkMembers.Contains(index))
        {
            ReadUtsuDebuff(e, target);
            if (_debuff.NelSinks.TryGetValue(index, out var batch))
                await _battleField.ShowNelSink(Recipients(batch));
            return true;
        }
        if (_debuff.NelDirectLeaks.Contains(index) && target is not null)
        {
            ReadUtsuDebuff(e, target);
            _battleField.ShowNelLeakReceipt(target);
            return true;
        }
        if (_debuff.KarzDulls.Contains(index) && target is not null)
        {
            ReadUtsuDebuff(e, target);
            _battleField.ShowKarzDull(target);
            await Delay(.16);
            return true;
        }
        if (_debuff.KubiGains.Contains(index) && target is not null)
        {
            if (_debuff.KubiGroups.TryGetValue(index, out var batch))
                await _battleField.ShowKubiDaunt(Recipients(batch), _battleField.FindPawn(batch.Actor));
            target.SetStatusIcon(StatusKeys.Daunted, true);
            SetDisplayedStatus(target, DisplayStatusKey(StatusKeys.Daunted), e.Amount);
            return true;
        }
        return false;
    }
}
