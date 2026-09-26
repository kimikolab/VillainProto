using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private PlankPresentation _planks = new(Array.Empty<BattleEvent>());
    private readonly HashSet<int> _plankKnockouts = new();
    private int _plankVolleys, _plankFlights, _plankPastes, _plankScraps;
    private int _plankFirstAids, _plankSkillUps;

    private void ResetPlankPlayback()
    {
        _planks = new PlankPresentation(_result!.Events);
        _plankKnockouts.Clear();
        _plankVolleys = _plankFlights = _plankPastes = _plankScraps = 0;
        _plankFirstAids = _plankSkillUps = 0;
    }

    private async Task<bool> PlayPlank(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        var events = _result!.Events;
        if (_planks.DamageToReflect.ContainsKey(index))
        {
            // 一斉射の数値は着弾時に表示済み。HP は台本の位置で反映して順序を守る。
            target?.SetHp(e.HpAfter);
            if (e.HpAfter <= 0 && e.TargetId is int dead) _plankKnockouts.Add(dead);
            return true;
        }
        if (e.Kind is BattleEventKind.Heal or BattleEventKind.Revive && e.HpAfter > 0 && e.TargetId is int alive)
            _plankKnockouts.Remove(alive);
        if (e.Kind == BattleEventKind.Skill && actor?.UnitId == "tsugi") return true;
        if (e.Kind != BattleEventKind.Plank) return false;
        int token = _playToken;
        switch (e.Text)
        {
            case PlankLabels.FirstAid:
                _plankFirstAids++;
                if (actor is null || target is null) break;
                actor.RushToPlank(target);
                if (actor != target && actor.Position.DistanceTo(target.Position) > 1.4f)
                    _battleField.PlayPlankRushSound();
                try
                {
                    await Delay(0.10);
                    if (token != _playToken || !_battleMode) return true;
                    // 手番外の処置は「駆け寄り→2打」。HPは回復させず、台本の破片だけを反映。
                    for (int hit = 0; hit < 2; hit++)
                    {
                        actor.HammerPlank();
                        if (hit == 0) ApplyPlankPatch(e, actor, target);
                        _battleField.PlankImpact(target, e.Amount / 2, _speed);
                        _battleField.PlayPlankPasteSound();
                        await Delay(0.085);
                        if (token != _playToken || !_battleMode) return true;
                    }
                }
                finally
                {
                    if (token == _playToken && Godot.GodotObject.IsInstanceValid(actor)) actor.ReturnFromPlank();
                }
                await Delay(0.12);
                break;
            case PlankLabels.Skill:
                _plankSkillUps++;
                if (actor is not null)
                {
                    actor.RaisePlankCraft(e.Slot);
                    _battleField.PlankCraftSpark(actor, e.Slot, _speed);
                    _battleField.PlayPlankSkillUpSound();
                }
                // 段上昇で攻撃と反射の間に待ちを挟まない。
                break;
            case PlankLabels.Reflect:
                // 残った厚さは HpAfter。これはHPではない。
                actor?.SetPlank(e.HpAfter);
                if (actor is not null) ApplyLiliStatus(actor, StatusKeys.Armor, e.HpAfter);
                if (!_planks.Volleys.TryGetValue(index, out var volley)) return true;
                _plankVolleys++;
                foreach (int j in volley)
                {
                    var shot = events[j];
                    var source = _battleField.FindPawn(shot.ActorId);
                    var enemy = _battleField.FindPawn(shot.TargetId);
                    if (source is null || enemy is null) continue;
                    _battleField.PlankReflect(source.FxPoint, enemy.FxPoint, shot.Amount, _speed);
                    _plankFlights++;
                }
                await Delay(0.50);
                if (token != _playToken || !_battleMode) return true;
                if (target is not null)
                {
                    int total = volley.Sum(j => events[j].Amount);
                    _battleField.PlankReflectImpact(target, total, _speed);
                    target.AnimateHit();
                    _battleField.PlayPlankReflectSound();
                    int damage = _planks.DamageToReflect.Where(pair => volley.Contains(pair.Value)).Sum(pair => events[pair.Key].Amount);
                    _battleField.DamagePopup(target, damage, "", PlankFx.Rust, volley.Count > 1 || damage >= 25, false);
                }
                // 本数によらず一拍。2倍速なら計0.34秒で、後続の反射には待ちを足さない。
                await Delay(0.18);
                break;
            case PlankLabels.Paste:
                _plankPastes++;
                if (actor is not null && target is not null)
                    _battleField.PlankFlight(actor.ScrapPoint, target.FxPoint, e.Amount, 0.24 / _speed, true);
                await Delay(0.24);
                if (token != _playToken || !_battleMode) return true;
                ApplyPlankPatch(e, actor, target);
                if (target is not null)
                {
                    _battleField.PlankImpact(target, e.Amount / 2, _speed);
                    _battleField.PlayPlankPasteSound();
                }
                await Delay(0.12);
                break;
            case PlankLabels.Scrap:
                _plankScraps++;
                var origin = _battleField.FindPawn(e.SpreadFromId ?? e.TargetId);
                if (actor is not null && origin is not null)
                    _battleField.PlankFlight(origin.FxPoint, actor.ScrapPoint, Math.Max(1, e.Amount), 0.22 / _speed, true);
                actor?.SetScrapStock(e.StatusRemaining ?? 0);
                // 回収のために範囲攻撃と反射の間を止めない。
                break;
            case PlankLabels.Flare:
                if (target is not null) _battleField.PlankImpact(target, 25, _speed, true);
                break;
        }
        return true;
    }

    private void ApplyPlankPatch(BattleEvent e, BattlePawn3D? actor, BattlePawn3D? target)
    {
        actor?.SetScrapStock(0);
        if (target is null) return;
        target.SetPlank(e.StatusRemaining ?? e.Amount);
        ApplyLiliStatus(target, StatusKeys.Armor, e.StatusRemaining ?? e.Amount);
        SetDisplayedStatus(target, DisplayStatusKey(StatusKeys.Plank), 1);
    }
}
