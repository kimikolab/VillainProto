using BattleCore;
using System.Threading.Tasks;

public partial class Main
{
    private DustBurstPresentation _dustBurst = new();
    private bool _dustChainFastEvent;

    // 反撃の中で発生した放電・粉の着弾もReactionを引き継ぐ。
    // 爆発の出どころを追加攻撃の発動者としてカットインさせない。
    private bool IsElectricEffectDamage(int index)
    {
        var events = _result!.Events;
        var hit = events[index];
        if (hit.Kind != BattleEventKind.Damage) return false;
        if (_dustBurst.DamageMembers.Contains(index) || DischargePresentation.Cause(events, index) is not null)
            return true;
        for (int i = index - 1; i >= 0; i--)
        {
            var cue = events[i];
            if (cue.Kind == BattleEventKind.ShockGauge)
                return cue.Text == ShockGaugeLabels.DenseBurn
                    && cue.TargetId == hit.ActorId && cue.TargetId == hit.TargetId;
            if (cue.Kind is BattleEventKind.Damage or BattleEventKind.Parry or BattleEventKind.Heal
                or BattleEventKind.HealBlocked or BattleEventKind.Discharge or BattleEventKind.ShockSpent
                or BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.TurnStart) break;
        }
        return false;
    }

    private void StartDustBurst(int index)
    {
        _dustChainFastEvent = _dustBurst.Members.Contains(index);
        if (_dustBurst.Starts.TryGetValue(index, out var chain))
            _battleField.BeginDustBurstChain(chain, _result!.Events, _speed);
    }

    private async Task FinishDustBurst(int index)
    {
        if (!_dustBurst.Ends.ContainsKey(index) || _fastSmoke) return;
        // 一団の時計だけを待つ。何十件の着弾でも個別の待ちを足さない。
        await _battleField.DustBurstCompletion;
    }

    private bool ShowDustDamage(int index, BattleEvent e)
    {
        if (!_dustBurst.DamageMembers.Contains(index)) return false;
        // HPはShowDamageで台本順に反映済み。数字だけ連鎖の最後へまとめる。
        AppendLog($"  [color=#ff6575][粉塵爆発][/color] → {NameOf(e.TargetId)} −{e.Amount}");
        return true;
    }
}
