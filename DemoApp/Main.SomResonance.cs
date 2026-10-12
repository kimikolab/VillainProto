using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private readonly HashSet<int> _somBackswingActors = new();

    private async Task<bool> PlaySomResonance(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        // 次の本物の割り込み／蓄電通知が来たら、その経路へ所有権を戻す。
        // その後のAttackで打ち返しの後片付けをすると、連鎖の強い鞭まで消してしまう。
        if (e.Kind == BattleEventKind.ShockGauge)
        {
            if (e.Text == ShockGaugeLabels.Interrupt && e.ActorId is int interrupted) _somBackswingActors.Remove(interrupted);
            if (e.Text is ShockGaugeLabels.ChargeGain or ShockGaugeLabels.ChargeSpent or ShockGaugeLabels.ChargeDrained
                && e.TargetId is int charged) _somBackswingActors.Remove(charged);
        }
        if (e.Kind == BattleEventKind.Attack && actor is not null)
        {
            // 打ち返しは蓄電を使わない。次の別の攻撃へ割り込み札を漏らさない。
            if (_som.BackswingAttacks.Contains(index)) _somBackswingActors.Add(actor.InstanceId);
            else if (_somBackswingActors.Remove(actor.InstanceId))
            { actor.InterruptWhip = false; actor.WhipChainSize = 0; }
        }
        if (_som.StandingCalls.TryGetValue(index, out var call) && actor is not null)
        {
            int token = _playToken, count = call.Summons.Length;
            bool speak = count > _somLargestCall.GetValueOrDefault(actor.InstanceId);
            _somLargestCall[actor.InstanceId] = System.Math.Max(count, _somLargestCall.GetValueOrDefault(actor.InstanceId));
            _battleField.BeginSomCall(actor, count, false, null, speak, _speed);
            await DelaySom(.14);
            if (token != _playToken || !_battleMode) return true;
            int[] ids = call.Summons.Select(j => _result!.Events[j].TargetId!.Value).ToArray();
            for (int n = 0; n < count; n++)
            {
                var summon = _result!.Events[call.Summons[n]];
                var def = UnitCatalog.Fodder; int team = summon.Team ?? 1 - actor.Team;
                var opening = new DemoOpening(ids[n], team, def.Id, def.Name, summon.Slot,
                    summon.HpAfter, System.Math.Max(summon.HpAfter, def.MaxHp), def.Attack, def.Pattern,
                    def.Advances, def.Traits, team == 0 ? _playerShape : FormationShape.X);
                _openingById[ids[n]] = opening;
                _battleField.AddSomStandingPawn(opening, System.Math.Max(.1, _speed));
                _battleField.PlaceSomBurstBeast(actor, summon, false, null, n, count, _speed);
            }
            await DelaySom(speak ? .20 : .07);
            if (token != _playToken || !_battleMode) return true;
            _battleField.RunSomBurstBeasts(actor, ids, speak);
            await DelaySom(SomFx.BeastRunSeconds);
            if (token != _playToken || !_battleMode) return true;
            _battleField.TakeSomStance(actor, ids, false);
            await DelaySom(SomFx.BeastStanceSeconds);
            if (token != _playToken || !_battleMode) return true;
            AppendLog($"  [color=#{SomFx.Violet.ToHtml(false)}][寝返り] {count}体が敵陣で契約紋を砕き、雷を溜める[/color]");
            return true;
        }
        if (_som.StandingMembers.Contains(index)) return true;
        if (_som.ThrowBatches.TryGetValue(index, out var batch))
        {
            int token = _playToken;
            foreach (int j in batch) _battleField.ThrowSomLightning(_result!.Events[j], _speed);
            AppendLog($"  [color=#{ThunderFx.Cyan.ToHtml(false)}][背きの雷 · {e.Text}] {NameOf(e.ActorId)} → {NameOf(e.TargetId)}[/color]");
            await DelaySom(SomFx.ThrowSeconds);
            if (token != _playToken || !_battleMode) return true;
            foreach (int j in batch) _battleField.ReceiveSomLightning(_result!.Events[j], _speed);
            // 一団の往路・体表の共鳴・復路は一拍ずつ。HPや帯電の通知は元の台本順。
            await DelaySom(.09);
            if (token != _playToken || !_battleMode) return true;
            int[] returns = batch.Where(j => _result!.Events[j].Text == BeastThrowLabels.Resonance).ToArray();
            foreach (int j in returns) _battleField.ReturnSomLightning(_result!.Events[j], _speed);
            if (returns.Length > 0)
            {
                await DelaySom(SomFx.ReturnSeconds * .65);
                if (token != _playToken || !_battleMode) return true;
                foreach (int j in returns)
                    foreach (int arc in _som.ReturnArcs.GetValueOrDefault(j) ?? System.Array.Empty<int>())
                        _battleField.BranchSomLightning(_result!.Events[arc], _speed);
                await DelaySom(SomFx.ReturnSeconds * .35 + .04);
            }
            return true;
        }
        if (e.Kind == BattleEventKind.BeastThrow)
        {
            if (e.Text == BeastThrowLabels.Backswing && target is not null)
                _battleField.SomBackswing(target, _speed); // 鞭そのものは直後のAttackで一度だけ再生。
            else if (!_som.BatchedThrows.Contains(index) && e.Text == BeastThrowLabels.Arc
                && !_som.ReturnArcs.Values.Any(arcs => arcs.Contains(index)))
                _battleField.BranchSomLightning(e, _speed);
            AppendLog($"  [color=#{ThunderFx.Cyan.ToHtml(false)}][背きの雷 · {e.Text}] {NameOf(e.ActorId)} → {NameOf(e.TargetId)}[/color]");
            return true;
        }
        if (e.Kind == BattleEventKind.BeastBurst && !_som.CallMembers.Contains(index))
        {
            _battleField.RemoveSomBeast(e.TargetId, _speed, e.Text == BeastBurstLabels.Backfire);
            AppendLog($"  [color=#{SomFx.Violet.ToHtml(false)}][{e.Text}] {NameOf(e.TargetId)} の光核が散った[/color]");
            return true;
        }
        if (e.Kind == BattleEventKind.Death && target?.UnitId == "fodder"
            && _battleField.RemoveSomBeast(e.TargetId, _speed, scriptedBurst: false))
        {
            AppendLog($"  [color=#{ThunderFx.Cyan.ToHtml(false)}][雷の散開] {NameOf(e.TargetId)} の光核が散った[/color]");
            return true;
        }
        return false;
    }
}
