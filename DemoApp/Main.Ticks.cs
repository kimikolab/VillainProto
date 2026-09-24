using BattleCore;
using System.Threading.Tasks;

public partial class Main
{
    private TickPresentation _ticks = new();
    private double? _tickDelayBudget;
    private int _tickPlays, _inverseTickPlays;

    private async Task<bool> PlayTickEvent(BattleEvent e, int index, BattlePawn3D? target)
    {
        if (_ticks.Starts.TryGetValue(index, out var cue))
        {
            bool inverse = e.SourceTrait == TraitId.Inverse;
            _battleField.ShowTick(target, e.Text == "燃焼", inverse,
                cue.Last && e.TickCount > 1, cue.Seconds / System.Math.Max(0.1, _speed));
            _tickPlays++;
            if (inverse) _inverseTickPlays++;
            AppendLog($"  [{e.Text}] → {NameOf(e.TargetId)}");
            await Delay(cue.Seconds * 0.55);
            return true;
        }
        if (!_ticks.Outcomes.TryGetValue(index, out cue)) return false;
        bool heal = e.Kind == BattleEventKind.Heal;
        target?.SetHp(e.HpAfter);
        // 色の反転は Status の絵の中で行う。通常の回復光を重ねて隠さない。
        var status = _result!.Events[cue.Start];
        _battleField.TickNumber(target, e.Amount, heal, cue.Ordinal, cue.Last && status.TickCount > 1,
            status.Text == "燃焼");
        if (!heal && e.Amount > 0)
            _battleField.PlayStatusDamageSound(status.Text);
        AppendLog($"  [color=#{(heal ? UiKit.Heal : UiKit.Poison).ToHtml(false)}]"
            + $"{(heal ? "＋" : "−")}{e.Amount}[/color] → {NameOf(e.TargetId)}");
        await Delay(cue.Seconds * 0.45);
        return true;
    }
}
