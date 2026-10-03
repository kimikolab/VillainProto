using BattleCore;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Main
{
    private FirePresentation _firePresentation = new(System.Array.Empty<BattleEvent>());
    private bool _fireFastEvent;

    private async Task<bool> PlayFire(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (e.Kind is BattleEventKind.TurnStart or BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.Charge)
            foreach (var pawn in _battleField.Pawns.Values) pawn.ClearFirePower();
        if (e.Kind == BattleEventKind.Attack && _firePresentation.Attacks.TryGetValue(index, out var attack))
            actor?.ShowFirePower(attack, e.Amount);
        // 状態の残りを表示専用に写す。火勢の量とは別に保持する。
        if (e.Kind == BattleEventKind.StatusSnapshot && e.Text == StatusKeys.LabelOf(StatusKeys.Burn))
            target?.SetFireRemaining(e.Amount);
        if (e.Kind == BattleEventKind.StatusGain && e.Text is StatusKeys.Burn or "燃" or "燃焼")
            target?.SetFireRemaining(e.Amount);
        if (e.Kind is not (BattleEventKind.FireLevel or BattleEventKind.FireArmor)) return false;
        if (_firePresentation.FoeSurges.TryGetValue(index, out var surge))
            _battleField.ShowFireFoeSurge(surge, _speed);
        double seconds = _battleField.ShowFireCue(e, actor, target, _speed,
            _firePresentation.AllyOutcomes.GetValueOrDefault(index), groupedFoeSurge: _firePresentation.FoeSurgeMembers.Contains(index));
        AppendLog($"  [color=#ffbd72]{e.Text}[/color] → {NameOf(e.TargetId)}");
        if (seconds > 0) await Delay(System.Math.Min(seconds, _tickDelayBudget ?? seconds));
        if (_firePresentation.FoeSurges.ContainsKey(index)) await Delay(0.24);
        return true;
    }
}
