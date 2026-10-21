using BattleCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private bool ReadsWounds(BattlePawn3D actor)
        => actor.UnitId == "susu" && _openingById.GetValueOrDefault(actor.InstanceId)?.Traits?.Contains(TraitId.BloodRead) == true;

    // 全体の直撃だけを束ねる。肩代わり・反撃・死亡時の飛散は元の台本順に残す。
    private int[] GatherHits(int index, int actorId, AttackPattern pattern)
    {
        var hits = new List<int>();
        if (_result is null) return hits.ToArray();
        for (int i = index + 1; i < _result.Events.Count; i++)
        {
            var e = _result.Events[i];
            if (e.Kind == BattleEventKind.TurnStart
                || (e.Kind is BattleEventKind.Attack or BattleEventKind.Skill) && !e.Reaction) break;
            if (e.Kind == BattleEventKind.Shard && e.Text == ShardLabels.Throw) break;
            if (e.Kind == BattleEventKind.Death && e.TargetId == actorId) break;
            if (e.Kind is not (BattleEventKind.Damage or BattleEventKind.Parry)
                || e.ActorId != actorId || e.Pattern != pattern || e.FriendlyFire || e.Reaction
                || e.Relayed || e.ShareFromId is not null || e.DeflectFromId is not null || _invertedDamage.Contains(i)) continue;
            hits.Add(i);
        }
        return hits.ToArray();
    }
    private void LandGatherHits(IEnumerable<int> hits)
    {
        foreach (int i in hits)
        {
            if (!_burstDamageIndices.Add(i)) continue;
            var e = _result!.Events[i];
            if (e.Kind == BattleEventKind.Parry) ShowParry(e);
            else ShowDamage(i, e, _battleField.FindPawn(e.ActorId), _battleField.FindPawn(e.TargetId));
        }
    }
    private async Task<bool> PlayGatherCast(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (actor is null) return false;
        _battleField.SetGatherClock(_paused, _speed);
        int token = _playToken;
        if (e.Kind == BattleEventKind.Shard && actor.UnitId == "gare")
        {
            if (e.Text == ShardLabels.Pick && target is not null)
            {
                var pick = _battleField.PickPebbles(actor, target, e.Amount, e.StatusRemaining ?? e.Amount);
                if (_fastSmoke) pick.Cancel(); else await pick.Finished;
                if (token != _playToken || !_battleMode) return true;
                AppendLog($"  {actor.UnitName} が礫を拾う ＋{e.Amount}（袋 {e.StatusRemaining}）");
                return true;
            }
            if (e.Text == ShardLabels.Empty)
            {
                var empty = _battleField.EmptyPebbles(actor);
                if (_fastSmoke) empty.Cancel(); else await empty.Finished;
                if (token != _playToken || !_battleMode) return true;
                AppendLog($"  {actor.UnitName} — 礫がない");
                return true;
            }
        }
        bool pebble = actor.UnitId == "gare" && e.Kind == BattleEventKind.Shard && e.Text == ShardLabels.Throw;
        bool blood = ReadsWounds(actor) && e.Kind == BattleEventKind.Skill && e.Text == AshActionLabels.Release;
        bool dry = ReadsWounds(actor) && e.Kind == BattleEventKind.Attack && !e.Reaction;
        if (!pebble && !blood && !dry) return false;
        var hits = GatherHits(index, actor.InstanceId, dry ? e.Pattern ?? AttackPattern.Single : AttackPattern.All);
        var targets = hits.Select(i => _battleField.FindPawn(_result!.Events[i].TargetId)).OfType<BattlePawn3D>().Distinct().ToArray();
        if (blood)
        {
            var read = _battleField.ReadWounds(actor);
            if (_fastSmoke) read.Cancel(); else if (!await read.Finished) return true;
            if (token != _playToken || !_battleMode) return true;
        }
        GatherCast3D volley = dry && target is not null ? _battleField.DryBlood(actor, target)
            : _battleField.GatherVolley(actor, targets, pebble, e.Slot);
        if (!_fastSmoke && !await volley.Impact) return true;
        if (token != _playToken || !_battleMode) { volley.Cancel(); return true; }
        LandGatherHits(hits);
        AppendLog($"  {actor.UnitName} — {(pebble ? $"礫 {e.Slot} を撃つ" : dry ? "小さな血の光" : "今の傷を詠む")}");
        if (_fastSmoke) volley.Cancel(); else await volley.Finished;
        return true;
    }
    private void ShowAkaFallout(int index, BattlePawn3D? aka)
    {
        if (aka is null || !ReadsWounds(aka) || _result is null) return;
        var targets = new List<BattlePawn3D>();
        for (int i = index + 1; i < _result.Events.Count; i++)
        {
            var e = _result.Events[i];
            if (e.Kind is BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.TurnStart) break;
            if (e.Kind == BattleEventKind.Damage && e.ActorId == aka.InstanceId && e.FriendlyFire
                && !e.Relayed && e.ShareFromId is null && _battleField.FindPawn(e.TargetId) is { } pawn)
                targets.Add(pawn);
        }
        _battleField.BloodFallout(aka, targets.Distinct().ToArray());
    }
}
