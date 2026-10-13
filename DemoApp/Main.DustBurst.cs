using BattleCore;
using System.Threading.Tasks;

public partial class Main
{
    private DustBurstPresentation _dustBurst = new();
    private bool _dustChainFastEvent;

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
