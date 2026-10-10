using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private SomPresentation _som = new();
    private readonly Dictionary<int, SomVeilLedger> _somArmor = new();
    private readonly Dictionary<int, int> _somArmorSnapshot = new();
    private readonly HashSet<(int Event, int Target)> _somContacts = new();
    private readonly HashSet<int> _somIntroduced = new();

    private void ResetSomPlayback()
    {
        _som = SomPresentation.Build(_result!.Events);
        _somArmor.Clear(); _somArmorSnapshot.Clear(); _somContacts.Clear(); _somIntroduced.Clear();
    }
    private SomVeilLedger SomArmor(int id)
    {
        if (!_somArmor.TryGetValue(id, out var ledger)) _somArmor[id] = ledger = new();
        return ledger;
    }
    private void ObserveSomArmor(int id, int total)
    {
        var ledger = SomArmor(id); ledger.Observe(total);
        if (_battleField.FindPawn(id) is { } pawn) _battleField.SetSomVeil(pawn, ledger.Veil, false, _speed);
    }
    private void ObserveSomEvent(BattleEvent e, int index)
    {
        if (e.Kind == BattleEventKind.TurnStart) _somArmorSnapshot.Clear();
        if (e.Kind == BattleEventKind.ShockSpent && _som.Pops.Contains(index))
            _battleField.RaiseSomLight(index, e.TargetId, _speed);
        if (e.Kind == BattleEventKind.StatusSnapshot && e.Text == StatusKeys.LabelOf(StatusKeys.Armor)
            && e.TargetId is int snapshot) _somArmorSnapshot[snapshot] = e.Amount;
        if (e.Kind == BattleEventKind.StatSnapshot && e.TargetId is int stat)
            ObserveSomArmor(stat, _somArmorSnapshot.GetValueOrDefault(stat)); // 行が無ければ0。
        if (e.Kind == BattleEventKind.Plank)
        {
            if (e.Text == PlankLabels.Reflect && e.ActorId is int reflector) ObserveSomArmor(reflector, e.HpAfter);
            else if (e.StatusRemaining is int total && e.TargetId is int patched
                && e.Text is PlankLabels.Opening or PlankLabels.Paste or PlankLabels.FirstAid)
                ObserveSomArmor(patched, total);
        }
        if (e.Kind == BattleEventKind.Kiss && e.Text == KissLabels.Armor && e.TargetId is int kissed)
            ObserveSomArmor(kissed, e.StatusRemaining ?? e.Amount);
        if (e.Kind == BattleEventKind.StatusTransfer && e.Text == StatusKeys.Armor)
        {
            if (e.SpreadFromId is int from) ObserveSomArmor(from, 0);
            if (e.TargetId is int to) ObserveSomArmor(to, e.StatusRemaining ?? e.Amount);
        }
        if (e.Kind == BattleEventKind.StatusGain && e.Text == StatusKeys.Armor && e.TargetId is int gained)
            ObserveSomArmor(gained, e.StatusRemaining ?? e.Amount);
        // Damageは破片を通過してHPが実際に減った通知。残量0が確定するので、その場で衣を散らす。
        // Attack.Amountとの差分から消費量を逆算することはしない。
        if (e.Kind == BattleEventKind.Damage && e.Amount > 0 && e.TargetId is int damaged)
            ObserveSomArmor(damaged, 0);
        if (e.Kind == BattleEventKind.Death && e.TargetId is int dead)
        {
            _somArmor.Remove(dead);
            _battleField.FindPawn(dead)?.SetSomVeil(0);
        }
    }

    private async Task<bool> PlaySom(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (e.Kind == BattleEventKind.Summon && actor?.UnitId == "som" && e.Text == UnitCatalog.Fodder.Name
            && e.TargetId is int summoned)
        {
            int token = _playToken;
            bool first = _somIntroduced.Add(actor.InstanceId);
            int team = e.Team ?? BattleContext.EnemyTeam;
            _battleField.BeginSomSummon(actor, team, e.Slot, _speed, first);
            await Delay(.18);
            if (token != _playToken || !_battleMode) return true;
            var def = UnitCatalog.Fodder;
            var opening = new DemoOpening(summoned, team, def.Id, def.Name, e.Slot, e.HpAfter,
                Math.Max(e.HpAfter, def.MaxHp), def.Attack, def.Pattern, def.Advances, def.Traits,
                team == 0 ? _playerShape : FormationShape.X);
            _openingById[summoned] = opening;
            _battleField.AddSummon(opening, ordinaryEffect: false, appearanceSpeed: Math.Max(.1, _speed));
            var beast = _battleField.FindPawn(summoned)!;
            beast.AnimationSpeed = Math.Max(.1, _speed);
            _battleField.SomBeastLooksBack(actor, beast);
            await Delay(.28);
            if (token != _playToken || !_battleMode) return true;
            _battleField.SomBeastSnubs(actor, beast, first);
            AppendLog($"  [color=#{SomFx.Violet.ToHtml(false)}]{actor.UnitName} が喚んだ {def.Name} は敵の側に立った[/color]");
            await Delay(.42);
            return true;
        }
        if (_som.Heals.Contains(index) && e.Kind == BattleEventKind.Heal)
        {
            target?.SetHp(e.HpAfter); target?.AnimateHeal();
            _battleField.HealPopup(target, e.Amount);
            AppendLog($"  [color=#{SomFx.Gold.ToHtml(false)}][降る光] {NameOf(e.TargetId)} HP＋{e.Amount}[/color]");
            return true; // 光の雨の到着で全員を癒す。人数ぶん待たない。
        }
        if (e.Kind != BattleEventKind.Spark) return false;
        if (e.Text == SparkLabels.Veil && target is not null)
        {
            var ledger = SomArmor(target.InstanceId);
            ledger.AddVeil(e.Amount, e.Slot);
            ApplyLiliStatus(target, StatusKeys.Armor, e.Slot);
            _battleField.SetSomVeil(target, ledger.Veil, true, _speed);
            AppendLog($"  [color=#{SomFx.Gold.ToHtml(false)}][光の衣] {target.UnitName} ＋{e.Amount}[/color]");
        }
        else if (_som.Rains.TryGetValue(index, out var rain) && actor is not null)
        {
            int token = _playToken;
            bool blocked = e.Text == SparkLabels.Silenced;
            // 放電段の待ちの省略を引き継がず、連鎖全体で一度だけ光を帰す。
            _tickDelayBudget = null; _fireFastEvent = _misaFastEvent = _zanFastEvent = false;
            _battleField.GatherSomLight(actor, rain.Pops, blocked, _speed);
            await Delay(.26);
            if (token != _playToken || !_battleMode) return true;
            if (!blocked)
            {
                _battleField.RainSomLight(actor, e.Amount, rain.InvertedTargets, _speed);
                await Delay(.34);
                if (token != _playToken || !_battleMode) return true;
            }
            AppendLog($"  [color=#{SomFx.Gold.ToHtml(false)}][{(blocked ? "光が消えた" : "降る光")}] {e.Amount}粒[/color]");
        }
        return true;
    }

    private IReadOnlyList<BattlePawn3D> SomImpactTargets(BattleEvent attack, IReadOnlyList<BattlePawn3D> targets)
    {
        // 全体攻撃でDamageが欠ける相手も接触だけ描く。打点と消費量は決して逆算しない。
        if (attack.Pattern == AttackPattern.All && _battleField.FindPawn(attack.ActorId) is { } actor)
            return targets.Concat(_battleField.Pawns.Values.Where(p => p.Team != actor.Team
                && p.PresentationAlive && p.SomVeilAmount > 0)).Distinct().ToArray();
        return targets;
    }
    private void SomAttackContact(int index, BattlePawn3D target)
    {
        if (target.SomVeilAmount <= 0 || !_somContacts.Add((index, target.InstanceId))) return;
        for (int j = index + 1; j < _result!.Events.Count; j++)
        {
            var next = _result.Events[j];
            if (next.Kind is BattleEventKind.Attack or BattleEventKind.TurnStart or BattleEventKind.Spark) break;
            if (next.TargetId == target.InstanceId && next.Kind is BattleEventKind.Parry or BattleEventKind.Evade) return;
        }
        _battleField.SomVeilContact(target, _speed);
    }
}
