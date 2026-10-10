using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private readonly Dictionary<int, int[]> _shockStages = new();
    private readonly HashSet<int> _shockStageMembers = new();
    private readonly HashSet<int> _electricDeaths = new();
    private readonly Dictionary<int, int?> _thunderPrevious = new();

    private static bool ElectricBoundary(BattleEvent e) => e.Kind is BattleEventKind.Attack
        or BattleEventKind.Skill or BattleEventKind.TurnStart or BattleEventKind.StatusSnapshot
        or BattleEventKind.StatSnapshot or BattleEventKind.Thunder or BattleEventKind.Charge or BattleEventKind.Status
        or BattleEventKind.MireSlam or BattleEventKind.MireConduct or BattleEventKind.MireBurst or BattleEventKind.LiveWire
        or BattleEventKind.ShockGauge or BattleEventKind.Feather or BattleEventKind.MarkLayer or BattleEventKind.Scar
        or BattleEventKind.FeatherMark or BattleEventKind.Framed or BattleEventKind.VendettaRound
        or BattleEventKind.BeckonFeather or BattleEventKind.ShareGive
        or BattleEventKind.Command or BattleEventKind.CommandBall or BattleEventKind.Cover or BattleEventKind.Rouse
        or BattleEventKind.HushState
        or BattleEventKind.Web || e.Kind == BattleEventKind.SilkBall && e.Text != SilkBallLabels.Pop;

    private void IndexThunder(IReadOnlyList<BattleEvent> events)
    {
        _shockStages.Clear(); _shockStageMembers.Clear(); _electricDeaths.Clear(); _thunderPrevious.Clear();
        int? previous = null;
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind is BattleEventKind.Skill or BattleEventKind.TurnStart or BattleEventKind.Attack) previous = null;
            if (e.Kind == BattleEventKind.Thunder)
            {
                _thunderPrevious[i] = e.Slot == 1 ? null : previous;
                previous = e.TargetId;
            }
            if (e.Kind is BattleEventKind.Thunder or BattleEventKind.Discharge)
            {
                for (int j = i + 1; j < events.Count; j++)
                {
                    var next = events[j];
                    if (ElectricBoundary(next) || next.Kind is BattleEventKind.Discharge or BattleEventKind.ShockSpent) break;
                    if (next.Kind == BattleEventKind.Death && next.TargetId == e.TargetId) _electricDeaths.Add(j);
                }
            }
            if (e.Kind != BattleEventKind.ShockSpent || _shockStageMembers.Contains(i)) continue;
            int end = i + 1;
            while (end < events.Count && !ElectricBoundary(events[end])
                && !(events[end].Kind == BattleEventKind.ShockSpent && events[end].Slot != e.Slot)) end++;
            var indices = Enumerable.Range(i, end - i).ToArray();
            _shockStages[i] = indices;
            foreach (int j in indices) _shockStageMembers.Add(j);
        }
    }

    private async Task<bool> PlayThunder(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        int token = _playToken;
        if (e.Kind == BattleEventKind.Skill && e.Text == "雷を落とした")
        {
            actor?.ShowMovementPortrait("kata_thunder", 1.2);
            AppendLog($"{NameOf(e.ActorId)} — {e.Text}");
            return true;
        }
        if (e.Kind == BattleEventKind.Thunder)
        {
            _battleField.LaunchThunderStake(actor, target, _speed);
            await Delay(0.18);
            if (token != _playToken || !_battleMode) return true;
            _battleField.StrikeThunder(_battleField.FindPawn(_thunderPrevious.GetValueOrDefault(index)),
                target, e.Slot, e.StatusRemaining ?? 0, _speed, actor);
            if (target is not null) FireHitContact(index, target);
            _fireFastEvent |= _fireHits.Contacts.ContainsKey(index);
            await Delay(0.12);
            return true;
        }
        if (_shockStages.TryGetValue(index, out var stage))
        {
            _battleField.ShockStage(e.Slot);
            foreach (int j in stage)
            {
                var item = _result!.Events[j];
                if (item.Kind == BattleEventKind.Discharge)
                    ShowScriptDischarge(item);
            }
            // 同じ段の電弧は一斉に走らせ、HP・死亡・副作用は元の台本順で処理する。
            _tickDelayBudget = null;
            await Delay(0.24);
            if (token != _playToken || !_battleMode) return true;
        }
        if (_shockStageMembers.Contains(index)) _tickDelayBudget = 0;
        if (e.Kind == BattleEventKind.ShockSpent)
        {
            _battleField.RememberShockPoint(e.TargetId);
            target?.SetShocked(false);
            target?.SetStatusIcon(StatusKeys.Shock, false);
            if (target is not null) SetDisplayedStatus(target, DisplayStatusKey(StatusKeys.Shock), 0);
            return true;
        }
        if (e.Kind == BattleEventKind.Discharge)
        {
            if (!_shockStageMembers.Contains(index))
            {
                ShowScriptDischarge(e);
                await Delay(0.16);
                if (token != _playToken || !_battleMode) return true;
            }
            if (e.SourceTrait == TraitId.Inverse) _battleField.ShowShockInverse(target, _speed);
            return true;
        }
        if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Shock)
        {
            if (e.Amount > 0 && e.PowderRoute is { } powder)
            {
                await _battleField.ShowPowder(actor, _battleField.FindPawn(e.SpreadFromId), target, powder, _speed);
                if (token != _playToken || !_battleMode) return true;
            }
            else if (e.Amount > 0 && actor is not null && target is not null && actor.Team == target.Team)
            {
                _battleField.ShowDischarge(actor, target, _speed, leak: true);
                await Delay(0.18);
                if (token != _playToken || !_battleMode) return true;
            }
            target?.SetStatusIcon(StatusKeys.Shock, e.Amount > 0);
            if (target is not null) SetDisplayedStatus(target, DisplayStatusKey(StatusKeys.Shock), e.Amount);
            return true;
        }
        if (_electricDeaths.Contains(index))
        {
            target?.ShockCollapse();
            _tickDelayBudget = null;
            await Delay(0.16);
            if (token != _playToken || !_battleMode) return true;
        }
        return false;
    }

    private void ShowScriptDischarge(BattleEvent e)
    {
        if (_battleField.ShowSilkBallDischarge(e.ActorId, e.TargetId, _speed)) return;
        if (e.SourceTrait == TraitId.Thread)
            _battleField.ShowThreadDischarge(_battleField.FindPawn(e.ActorId), _battleField.FindPawn(e.TargetId),
                _battleField.FindPawn(e.PartnerId), e.Text == ThreadLabels.Release, _speed);
        else _battleField.ShowDischarge(_battleField.FindPawn(e.ActorId), _battleField.FindPawn(e.TargetId), _speed);
    }
}
