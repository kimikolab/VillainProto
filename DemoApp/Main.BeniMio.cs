using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private readonly HashSet<int> _beniMioShown = new();
    private readonly HashSet<int> _beniGiftGains = new();
    private readonly HashSet<int> _invertedDamage = new();
    private readonly HashSet<int> _sipHeals = new();

    private void IndexBeniMio(IReadOnlyList<BattleEvent> events)
    {
        _beniMioShown.Clear(); _beniGiftGains.Clear(); _invertedDamage.Clear(); _sipHeals.Clear();
        for (int i = 0; i < events.Count; i++)
        {
            var e = events[i];
            if (e.Kind is not (BattleEventKind.HealInverted or BattleEventKind.InverseSip)) continue;
            int? target = e.Kind == BattleEventKind.InverseSip ? e.ActorId : e.TargetId;
            var kind = e.Kind == BattleEventKind.InverseSip ? BattleEventKind.Heal : BattleEventKind.Damage;
            for (int j = i + 1; j < events.Count; j++)
            {
                var next = events[j];
                if (next.Kind is BattleEventKind.Status or BattleEventKind.HealInverted or BattleEventKind.InverseSip
                    or BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.TurnStart) break;
                if (next.Kind != kind || next.TargetId != target || next.ActorId != e.ActorId) continue;
                (kind == BattleEventKind.Heal ? _sipHeals : _invertedDamage).Add(j);
                break;
            }
        }
    }

    private async Task<bool> PlayBeniMio(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (_beniMioShown.Contains(index)) return true;
        if (_invertedDamage.Contains(index))
        {
            target?.SetHp(e.HpAfter);
            _battleField.DamagePopup(target, e.Amount, "", new Color("765287"), false, false);
            AppendLog($"  回復が反転 → {NameOf(e.TargetId)} −{e.Amount}");
            return true;
        }
        if (_sipHeals.Contains(index))
        {
            target?.SetHp(e.HpAfter);
            _battleField.HealPopup(target, e.Amount);
            AppendLog($"  啜り → {NameOf(e.TargetId)} ＋{e.Amount}");
            return true;
        }
        if (e.Kind == BattleEventKind.HealInverted)
        {
            _battleField.ShowInvertedHeal(actor, target, _speed);
            await Delay(0.48);
            return true;
        }
        if (e.Kind == BattleEventKind.InverseSip)
        {
            double seconds = Math.Min(0.36, _tickDelayBudget ?? 0.36);
            _battleField.ShowInverseSip(target, actor, _speed, seconds);
            await Delay(seconds);
            return true;
        }
        if (e.Kind is BattleEventKind.PoisonThicken or BattleEventKind.ConcentrateMark)
        {
            int token = _playToken;
            var events = _result!.Events;
            int end = index + 1;
            while (end < events.Count && events[end].Kind == e.Kind && events[end].ActorId == e.ActorId
                && events[end].Turn == e.Turn) end++;
            var batch = events.Skip(index).Take(end - index).ToArray();
            var center = batch.FirstOrDefault(x => x.Text == ConcentrateTrait.CenterLabel);
            if (e.Kind == BattleEventKind.ConcentrateMark && center is not null)
            {
                _battleField.ShowConcentrate(actor, _battleField.FindPawn(center.TargetId), true, _speed);
                await Delay(0.22);
                if (token != _playToken || !_battleMode) return true;
            }
            foreach (var item in batch)
            {
                var pawn = _battleField.FindPawn(item.TargetId);
                if (e.Kind == BattleEventKind.PoisonThicken) _battleField.ShowThicken(pawn, _speed);
                else if (item != center)
                    _battleField.ShowConcentrate(item.Text == ConcentrateTrait.LeakLabel ? actor
                        : _battleField.FindPawn(center?.TargetId), pawn, false, _speed);
            }
            await Delay(e.Kind == BattleEventKind.PoisonThicken ? 0.36 : 0.42);
            if (token != _playToken || !_battleMode) return true;
            for (int j = index; j < end; j++)
            {
                var item = events[j];
                var pawn = _battleField.FindPawn(item.TargetId);
                _beniMioShown.Add(j);
                if (e.Kind == BattleEventKind.PoisonThicken)
                {
                    pawn?.SetPoisonRemaining(item.Amount);
                    pawn?.PulsePoisonIcon();
                    if (pawn is not null) SetDisplayedStatus(pawn, DisplayStatusKey(StatusKeys.Poison), item.Amount);
                }
                else pawn?.SetConcentrated(item.Amount);
                AppendLog($"  {item.Text} → {NameOf(item.TargetId)} {item.Amount}");
            }
            return true;
        }
        if (e.Kind == BattleEventKind.Skill && actor is not null
            && _openingById.TryGetValue(actor.InstanceId, out var opening)
            && opening.Traits?.Contains(TraitId.Inverse) == true
            && e.Text is KindleTrait.Label or "澱みを分けた")
        {
            bool fire = e.Text == KindleTrait.Label;
            var recipients = new List<BattlePawn3D>();
            var events = _result!.Events;
            for (int j = index + 1; j < events.Count; j++)
            {
                var next = events[j];
                if (next.Kind is BattleEventKind.Skill or BattleEventKind.Attack or BattleEventKind.TurnStart) break;
                if (next.Kind != BattleEventKind.StatusGain || next.ActorId != e.ActorId
                    || next.Text != (fire ? StatusKeys.Burn : StatusKeys.Poison)) continue;
                _beniGiftGains.Add(j);
                if (_battleField.FindPawn(next.TargetId) is { } pawn) recipients.Add(pawn);
            }
            _battleField.ShowBeniGift(actor, recipients.Distinct().ToArray(), fire, _speed);
            AppendLog($"{NameOf(e.ActorId)} — {e.Text}");
            await Delay(0.48);
            return true;
        }
        // ミオの説明文はログに残し、画面には水滴・波紋・印で伝える。
        if (e.Kind is BattleEventKind.Skill or BattleEventKind.Highlight && actor is not null
            && _openingById.TryGetValue(actor.InstanceId, out var mio)
            && mio.Traits?.Contains(TraitId.Concentrate) == true)
        {
            AppendLog($"{NameOf(e.ActorId)} — {e.Text}");
            return true;
        }
        return false;
    }
}
