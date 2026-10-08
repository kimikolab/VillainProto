using BattleCore;
using System.Threading.Tasks;

public partial class Main
{
    private MarkRallyPresentation _markRally = new();

    private async Task<bool> PlayMarkRally(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (!_zan.RallyStarts.Contains(index) && _markRally.Starts.TryGetValue(index, out var rally))
        {
            int token = _playToken;
            _battleField.ShowMarkRally(rally, _speed);
            await Delay(0.20);
            if (token != _playToken || !_battleMode) return true;
        }
        if (e.Kind == BattleEventKind.Insight)
        {
            _battleField.ShowInsight(actor, target, e, _speed);
            await Delay(0.10);
            return true;
        }
        if (e.Kind == BattleEventKind.MarkRally)
        {
            _battleField.MarkRallyCues++;
            if (_markRally.Ends.Contains(index)) await Delay(0.10);
            return true;
        }
        if (!_markRally.Heals.Contains(index)) return false;
        // HPはHealの本来の位置でだけ反映する。後続MarkRallyで二重に癒さない。
        target?.SetHp(e.HpAfter);
        if (!_zan.RallyHeals.Contains(index)) _battleField.HealPopup(target, e.Amount);
        AppendLog($"  [color=#ffbb63]【号令】＋{e.Amount} 回復  {NameOf(e.TargetId)}{WriterSuffix(e.ActorId, e.TargetId)}[/color]");
        return true;
    }
}
