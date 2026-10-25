using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private DeathPresentation? _death;
    private int _deathFrame;
    private BattlePawn3D? _deathSinger;
    private int _deathRecipients;
    private async Task<bool> PlayDeathAxis(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        _battleField.SetDeathClock(_paused, _speed);
        if (index == 0) { _deathFrame = -1; _deathSinger = null; _deathRecipients = 0; }
        int token = _playToken;
        bool awakened = ApplyDeathFrames(index);
        if (awakened) await HoldDeathBeat(0.25, token);
        if (token != _playToken || !_battleMode) return true;
        if (e.Kind == BattleEventKind.TurnStart) { _deathSinger = null; _deathRecipients = 0; }
        if (e.Kind == BattleEventKind.Skill && actor?.UnitId == "ere" && _result is not null
            && ErePresentation.IsSongSkill(_result.Events, index))
        {
            _deathSinger = actor; _deathRecipients = 2;
        }
        // 贈られた実手番の直前に出る Highlight の宛先。強い駒を画面側では選ばない。
        if (e.Kind == BattleEventKind.Highlight && actor is not null && actor != _deathSinger
            && _deathSinger is not null && _deathRecipients > 0
            && _result!.Hands.Any(h => h.ActorId == actor.InstanceId && h.EventStart == index + 1))
        {
            _battleField.ShowDeathSong(_deathSinger, actor); _deathRecipients--;
            await HoldDeathBeat(0.30, token);
            if (token != _playToken || !_battleMode) return true;
        }
        if (e.Kind == BattleEventKind.Death && target is not null) _battleField.ShowDeathOffering(target);
        if (e.Kind == BattleEventKind.Damage && target is not null && e.SourceTrait is TraitId.Rot or TraitId.RicaRaiseDead or TraitId.RicaRaiseDeadStack or TraitId.RicaAwakenRaise)
        {
            target.SetHp(e.HpAfter);
            _battleField.ShowDeathCost(target, e.SourceTrait == TraitId.Rot);
            await HoldDeathBeat(e.SourceTrait == TraitId.Rot ? 0.16 : 0.08, token);
            return true; // 朽ちと代償にはダメージ数字を重ねない。
        }
        return false;
    }
    private bool ApplyDeathFrames(int index)
    {
        bool awakened = false;
        if (_death is not null)
            foreach (var frame in _death.Frames.Where(p => p.Key > _deathFrame && p.Key <= index))
                foreach (var state in frame.Value) awakened |= _battleField.ApplyDeathState(state);
        _deathFrame = index;
        return awakened;
    }
    private async Task HoldDeathBeat(double seconds, int token)
    {
        if (_fastSmoke) return;
        double age = 0;
        while (age < seconds && token == _playToken && _battleMode)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!_paused) age += GetProcessDeltaTime() * _speed;
        }
    }
}
