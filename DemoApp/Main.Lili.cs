using BattleCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private readonly HashSet<int> _liliGiven = new();
    private int _liliTransfers, _liliDrains, _liliGifts;

    // 判定せず台本の残量を表示する。移動元は全量が抜け、移動先は StatusRemaining が正。
    private void ApplyLiliStatus(BattlePawn3D? pawn, string key, int remaining)
    {
        if (pawn is null) return;
        pawn.ApplyTransferredStatus(key, remaining);
        SetDisplayedStatus(pawn, DisplayStatusKey(key), remaining);
        if (key == StatusKeys.Burn)
        {
            if (remaining > 0) _burningSnapshot.Add(pawn.InstanceId);
            else _burningSnapshot.Remove(pawn.InstanceId);
        }
        if (key == StatusKeys.Poison)
        {
            if (remaining > 0) _poisonedSnapshot.Add(pawn.InstanceId);
            else _poisonedSnapshot.Remove(pawn.InstanceId);
        }
        var effects = _statusEffectSnapshot.GetValueOrDefault(pawn.InstanceId);
        if (key == StatusKeys.Marked) effects.Marked = remaining;
        if (key == StatusKeys.Stun) effects.Stunned = remaining;
        if (key == StatusKeys.Armor) effects.Armor = remaining;
        _statusEffectSnapshot[pawn.InstanceId] = effects;
        pawn.SetStatusEffects(effects.Marked, effects.Stunned, effects.Armor);
    }

    private async Task<bool> PlayLili(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (_specialShown.Contains(index)) return true;
        if (await PlayLiliRite(e, index, actor)) return true;
        var events = _result!.Events;
        int token = _playToken;
        if (e.Kind == BattleEventKind.Skill && actor is not null
            && _openingById.TryGetValue(actor.InstanceId, out var opening)
            && opening.Traits?.Contains(TraitId.Kiss) == true) return true;
        if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Stigma)
        {
            target?.SetStigma(e.Amount > 0);
            if (target is not null) SetDisplayedStatus(target, DisplayStatusKey(StatusKeys.Stigma), e.Amount);
            return true;
        }
        if (e.Kind == BattleEventKind.StatusTransfer)
        {
            // 同じ吸い取りの連続部分だけ。次の手番や別の書き手まで束ねない。
            var indices = new List<int>();
            for (int j = index; j < events.Count; j++)
            {
                var next = events[j];
                if (next.Kind != BattleEventKind.StatusTransfer || next.ActorId != e.ActorId
                    || next.SpreadFromId != e.SpreadFromId || next.TargetId != e.TargetId || next.Turn != e.Turn) break;
                indices.Add(j);
            }
            var source = _battleField.FindPawn(e.SpreadFromId);
            if (actor is not null && target is not null)
            {
                _battleField.LiliFlow(actor.ChalicePoint, target.FxPoint, 0.32 / _speed);
                for (int k = 0; k < indices.Count; k++)
                    _battleField.LiliIcon(actor.ChalicePoint, target.FxPoint, events[indices[k]].Text!, k, 0.32 / _speed);
            }
            await Delay(0.32);
            if (token != _playToken || !_battleMode) return true;
            foreach (int j in indices)
            {
                var transfer = events[j];
                ApplyLiliStatus(source, transfer.Text!, 0);
                ApplyLiliStatus(target, transfer.Text!, transfer.StatusRemaining ?? transfer.Amount);
                _specialShown.Add(j);
                _liliTransfers++;
            }
            int give = indices[^1] + 1;
            if (give < events.Count && events[give].Kind == BattleEventKind.Kiss
                && events[give].Text == KissLabels.Give && events[give].ActorId == e.ActorId
                && events[give].TargetId == e.TargetId && events[give].SpreadFromId == e.SpreadFromId)
                _liliGiven.Add(give);
            return true;
        }
        // 吸い取りは術。通常の殴打・衝撃・説明札を重ねず、台本の HP と打点だけを出す。
        if (e.Kind == BattleEventKind.Damage && index > 0
            && events[index - 1] is { Kind: BattleEventKind.Kiss } drain
            && drain.Text is KissLabels.Drain or KissLabels.RiteDrain
            && drain.ActorId == e.ActorId && drain.TargetId == e.TargetId)
        {
            target?.SetHp(e.HpAfter);
            _battleField.DamagePopup(target, e.Amount, "", LiliFx.Rose, e.Amount >= 25, false);
            if (!InLiliRite(index)) await Delay(0.04);
            return true;
        }
        if (e.Kind == BattleEventKind.Heal && index > 0
            && events[index - 1] is { Kind: BattleEventKind.Kiss } gift
            && gift.Text is KissLabels.Give or KissLabels.RiteGive or KissLabels.Return
            && gift.ActorId == e.ActorId && gift.TargetId == e.TargetId)
        {
            target?.SetHp(e.HpAfter);
            target?.AnimateHeal();
            _battleField.HealPopup(target, e.Amount);
            if (!InLiliRite(index)) await Delay(0.10);
            return true;
        }
        if (e.Kind != BattleEventKind.Kiss) return false;
        switch (e.Text)
        {
            case KissLabels.Drain:
            case KissLabels.RiteDrain:
                _liliDrains++;
                if (actor is not null && target is not null)
                {
                    actor.ReachWithChalice();
                    _battleField.LiliFlow(target.FxPoint, actor.ChalicePoint, 0.26 / _speed);
                    int ordinal = 0;
                    for (int j = index + 1; j < events.Count; j++)
                    {
                        var next = events[j];
                        if (next.Kind is BattleEventKind.TurnStart or BattleEventKind.Attack or BattleEventKind.Skill
                            || next.Kind == BattleEventKind.Kiss && next.Text is KissLabels.Drain or KissLabels.RiteDrain or KissLabels.Give) break;
                        if (next.Kind == BattleEventKind.StatusTransfer && next.ActorId == e.ActorId && next.SpreadFromId == e.TargetId)
                            _battleField.LiliIcon(target.FxPoint, actor.ChalicePoint, next.Text!, ordinal++, 0.26 / _speed);
                    }
                }
                await Delay(0.26);
                break;
            case KissLabels.Give:
            case KissLabels.RiteGive:
            case KissLabels.Return:
                _liliGifts++;
                if (_liliGiven.Contains(index)) break;
                if (actor is not null && target is not null)
                {
                    if (e.Text == KissLabels.Return && _battleField.FindPawn(e.SpreadFromId) is { } dead)
                    {
                        _battleField.LiliFlow(dead.FxPoint, actor.ChalicePoint, 0.20 / _speed);
                        await Delay(0.20);
                        if (token != _playToken || !_battleMode) return true;
                    }
                    _battleField.LiliFlow(actor.ChalicePoint, target.FxPoint, 0.32 / _speed);
                }
                await Delay(0.32);
                break;
            case KissLabels.Armor:
                ApplyLiliStatus(target, StatusKeys.Armor, e.StatusRemaining ?? e.Amount);
                if (target is not null) _battleField.LiliOverflow(target, _speed);
                break;
            case KissLabels.RiteEnd:
                if (actor is not null)
                    foreach (var pawn in _battleField.Pawns.Values.Where(p => p.Team != actor.Team))
                    {
                        pawn.SetStigma(false);
                        SetDisplayedStatus(pawn, DisplayStatusKey(StatusKeys.Stigma), 0);
                    }
                break;
        }
        return true;
    }
}
