using BattleCore;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Main
{
    private DebuffPresentation _debuff = new();
    // 表示用の収支。攻撃力・打数・HPは書き換えず、Dull/Whetの実際の受取量だけを貯める。
    private readonly Dictionary<int, long> _utsuDullBalance = new();

    private void IndexDebuffs()
    {
        _debuff = DebuffPresentation.Build(_result!.Events, _battleOpening);
        _utsuDullBalance.Clear();
        MudoBurstPoseConflicts = 0;
        _debuffReadout.Begin(_battleOpening);
        foreach (var pawn in _battleField.Pawns.Values)
        {
            pawn.MudPower = _debuffReadout.MudPower(pawn.InstanceId);
            pawn.UtsuHits = 1;
        }
    }

    private void ReadUtsuDebuff(BattleEvent e, BattlePawn3D? target)
    {
        if (target?.UnitId != "utsu" || e.Amount <= 0) return;
        long delta = e.Kind == BattleEventKind.StatusGain && e.Text == "dull" ? e.Amount
            : e.Kind == BattleEventKind.Whet ? -(long)e.Amount : 0;
        if (delta == 0) return;
        long balance = _utsuDullBalance.GetValueOrDefault(target.InstanceId) + delta;
        _utsuDullBalance[target.InstanceId] = balance;
        // 1本は元の刃、下げ幅3ごとに追加の刃。5本で止め、超過は濃さだけにする。
        int layers = balance > 0 ? (int)System.Math.Min(PerverseTrait.MaxHits,
            1 + balance / PerverseTrait.HitsPerDull) : 0;
        target.UtsuHits = System.Math.Max(1, layers);
        _battleField.SetUtsuDebuff(target, layers, balance > PerverseOverTrait.Threshold);
    }

    private async Task PlayDebuffPass(int index)
    {
        if (!_debuff.Passes.TryGetValue(index, out var pass)) return;
        if (_battleField.FindPawn(pass.Actor) is not { } relay
            || _battleField.FindPawn(pass.From) is not { } from
            || _battleField.FindPawn(pass.To) is not { } to) return;
        await _battleField.ShowDebuffPass(relay, from, to, pass.Intake);
    }
}
