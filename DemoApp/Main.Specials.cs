using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private readonly HashSet<int> _specialShown = new();
    private readonly HashSet<int> _riposteDamage = new();
    private readonly Dictionary<int, float> _numbDamage = new();

    private void IndexNumbDamage(int index, BattleEvent attack)
    {
        if (attack.NumbPercent is not > 0) return;
        for (int j = index + 1; j < _result!.Events.Count; j++)
        {
            var next = _result.Events[j];
            if (next.Kind == BattleEventKind.TurnStart || next.Kind == BattleEventKind.Attack && next.ActorId == attack.ActorId) break;
            if (next.Kind == BattleEventKind.Damage && next.ActorId == attack.ActorId && next.Pattern == attack.Pattern)
                _numbDamage[j] = 1f - System.Math.Clamp(attack.NumbPercent.Value / 60f, 0, 1) * 0.85f;
        }
    }

    private async Task<bool> PlaySpecial(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (_specialShown.Contains(index)) return true;
        int token = _playToken;
        var events = _result!.Events;
        if (e.Kind == BattleEventKind.GurenGain)
        {
            _battleField.ShowGurenGain(target, actor, e.StatusRemaining ?? 0, _speed);
            if (actor is not null) SetDisplayedStatus(actor, DisplayStatusKey(StatusKeys.Guren), e.StatusRemaining ?? 0);
            await Delay(0.24);
            return true;
        }
        if (e.Kind == BattleEventKind.GurenRelease)
        {
            if (e.TargetId is not null) return true;
            var gains = new List<int>();
            bool poisonPhase = false;
            for (int j = index + 1; j < events.Count; j++)
            {
                var next = events[j];
                if (next.Kind == BattleEventKind.Highlight && next.ActorId == e.ActorId)
                { _specialShown.Add(j); AppendLog(next.Text ?? ""); continue; }
                if (next.Kind is BattleEventKind.Skill or BattleEventKind.Attack or BattleEventKind.TurnStart) break;
                if (next.ActorId != e.ActorId) break;
                if (next.Kind == BattleEventKind.GurenRelease) poisonPhase = true;
                if (poisonPhase && next.Kind == BattleEventKind.StatusGain && next.Text == StatusKeys.Burn) break;
                if (next.Kind == BattleEventKind.StatusGain && (next.Text == StatusKeys.Burn
                    || next.Text == StatusKeys.Poison && next.PoisonRoute == PoisonRoute.Guren)) gains.Add(j);
                else if (next.Kind != BattleEventKind.GurenRelease) break;
            }
            var targets = gains.Select(j => _battleField.FindPawn(events[j].TargetId)).OfType<BattlePawn3D>().Distinct().ToArray();
            _battleField.ShowGurenRelease(actor, targets, _speed);
            if (actor is not null) SetDisplayedStatus(actor, DisplayStatusKey(StatusKeys.Guren), 0);
            await Delay(0.38);
            if (token != _playToken || !_battleMode) return true;
            // 着火と毒は同じフレーム。後続の台本は再表示しない。
            foreach (var pawn in targets) pawn.FlashGurenImpact();
            foreach (int j in gains)
            {
                var gain = events[j];
                var pawn = _battleField.FindPawn(gain.TargetId);
                if (gain.Text == StatusKeys.Burn) { pawn?.SetBurning(true); pawn?.SetStatusIcon(StatusKeys.Burn, true); }
                else { pawn?.AddPoisonIconAmount(gain.Amount); pawn?.SetPoisoned(true); }
                if (pawn is not null) SetDisplayedStatus(pawn, DisplayStatusKey(gain.Text!),
                    gain.Text == StatusKeys.Poison ? pawn.PoisonIconAmount : gain.Amount);
                _specialShown.Add(j);
            }
            await Delay(0.26);
            if (token == _playToken && GodotObject.IsInstanceValid(actor)) actor?.SetGurenRelease(false);
            return true;
        }
        if (e.Kind == BattleEventKind.LastStand)
        {
            if (actor is not null) await _battleField.ShowSwordDraw(actor, e.StatusRemaining ?? 0, _speed);
            if (token == _playToken) await Delay(0.28);
            return true;
        }
        if (e.Kind == BattleEventKind.LastStandRiposte)
        {
            _battleField.ShowSwordSlash(actor, target is null ? [] : [target], _speed, true);
            for (int j = index + 1; j < events.Count; j++)
            {
                var next = events[j];
                if (next.Kind is BattleEventKind.Attack or BattleEventKind.LastStandRiposte or BattleEventKind.TurnStart) break;
                if (next.Kind is BattleEventKind.Damage or BattleEventKind.Parry && next.ActorId == e.ActorId && next.TargetId == e.TargetId)
                { _riposteDamage.Add(j); break; }
            }
            await Delay(0.18);
            return true;
        }
        if (e.Kind == BattleEventKind.LastStandVictory)
        {
            if (actor is not null) actor.QuietLastStand = true;
            await Delay(0.18); // 敵の崩れを見届けてから、次の Death で膝をつく。
            return true;
        }
        if (e.Kind == BattleEventKind.Highlight &&
            (index > 0 && events[index - 1].Kind == BattleEventKind.LastStand
             || index + 1 < events.Count && events[index + 1].Kind == BattleEventKind.LastStandVictory))
        {
            AppendLog(e.Text ?? "");
            return true;
        }
        if (e.Kind == BattleEventKind.Skill && actor is not null
            && _openingById.TryGetValue(actor.InstanceId, out var opening)
            && opening.Traits?.Contains(TraitId.Spew) == true)
        {
            AppendLog($"{NameOf(e.ActorId)} — {e.Text}");
            return true;
        }
        if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Poison
            && e.PoisonRoute is PoisonRoute.Spew or PoisonRoute.Venom)
        {
            _battleField.ShowVenom(actor, target, e.PoisonRoute == PoisonRoute.Venom, _speed);
            await Delay(e.PoisonRoute == PoisonRoute.Venom ? 0.22 : 0.36);
            if (token != _playToken || !_battleMode) return true;
            // 残量更新は既存の StatusGain に一度だけ任せる。
            _beniGiftGains.Add(index);
        }
        return false;
    }
}
