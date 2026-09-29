using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int HaneBlastPlays { get; private set; }
    internal int HanePinPlays { get; private set; }
    internal int HaneDropkickPlays { get; private set; }
    internal int HanePalmPlays { get; private set; }

    private void ShowHanePalmStrike(BattlePawn3D? actor, BattlePawn3D? target, BattlePawn3D? guarded = null)
    {
        if (actor?.UnitId != "hane" || target is null || !actor.CanReceiveMovementImpact || !target.CanReceiveMovementImpact) return;
        int generation = _specialGeneration;
        int audioGeneration = _attackAudio.MovementSoundGeneration;
        double speed = Math.Max(0.1, actor.AnimationSpeed);
        // 手前側の胴へ両掌を当てる。掌の高さはハネの接地した構えから取る。
        Vector3 edge = -_camera.GlobalBasis.X * (actor.Team == 0 ? 1 : -1) * 0.65f
            + _camera.GlobalBasis.Z * 0.10f;
        Vector3 last = target.FxPoint + edge;
        Vector3 Contact()
        {
            Vector3 point = IsInstanceValid(target) ? target.FxPoint + edge : last;
            point.Y = actor.PalmStrikeHeight(_camera.GlobalBasis.Y);
            return point;
        }
        actor.BeginPalmStrike(Contact, _camera.GlobalBasis.X, _camera.GlobalBasis.Y, () => {
            if (generation != _specialGeneration || audioGeneration != _attackAudio.MovementSoundGeneration
                || !IsInsideTree() || !IsInstanceValid(actor) || !IsInstanceValid(target)
                || !actor.IsInsideTree() || !target.IsInsideTree() || !actor.CanReceiveMovementImpact || !target.CanReceiveMovementImpact) return;
            HanePalmPlays++;
            _attackAudio.PlayMovementSound(MovementSound.SpringBlock);
            MovementFx.Smash(_fxRoot, _camera, Contact(), MovementFx.Bounce, 0.24 / speed, 1.10f);
            CameraPunch(Contact(), AttackPattern.Single);
        }, guarded?.FxPoint, guarded is not null
            && FormationRules.AreSameRowPair(actor.Slot, guarded.Slot)
            && !(actor.Shape?.AreAdjacent(actor.Slot, guarded.Slot) ?? FormationRules.AreAdjacent(actor.Slot, guarded.Slot)));
    }

    private async Task<bool> ShowHaneDropkick(BattlePawn3D actor, BattlePawn3D target)
    {
        if (actor.UnitId != "hane" || !actor.CanReceiveMovementImpact || !target.CanReceiveMovementImpact) return false;
        int generation = _specialGeneration;
        int audioGeneration = _attackAudio.MovementSoundGeneration;
        double speed = Math.Max(0.1, actor.AnimationSpeed);
        Vector3 impact = target.FxPoint;
        var sound = MovementCompletionSound(target, MovementSound.Dropkick);
        bool contacted = false;
        actor.BeginDropkick(() => IsInstanceValid(target) ? target.FxPoint : impact, _camera.GlobalBasis.X, _camera.GlobalBasis.Y, () => {
            if (generation != _specialGeneration || audioGeneration != _attackAudio.MovementSoundGeneration
                || !IsInsideTree() || !IsInstanceValid(actor) || !IsInstanceValid(target)
                || !actor.IsInsideTree() || !target.IsInsideTree() || !actor.CanReceiveMovementImpact || !target.CanReceiveMovementImpact) return;
            HaneDropkickPlays++;
            contacted = true;
            sound();
        });
        MovementFx.Flow(_fxRoot, _camera, actor.FxPoint, impact, MovementFx.Bounce, 0.22 / speed, 0.18f, 3);
        // 接触してから射出する。中断時には元の吹っ飛ばしへ進めない。
        await actor.DropkickImpact;
        return contacted;
    }

    private async Task ShowHaneBlast(BattlePawn3D from, BattlePawn3D to,
        IReadOnlyList<BattlePawn3D> hits, int? destination)
    {
        int generation = _specialGeneration;
        int audioGeneration = _attackAudio.MovementSoundGeneration;
        double speed = Math.Max(0.1, from.AnimationSpeed);
        _attackAudio.PlayMovementSound(MovementSound.Windup);
        if (!await ShowHaneDropkick(from, to)) return;
        if (generation != _specialGeneration || audioGeneration != _attackAudio.MovementSoundGeneration
            || !IsInstanceValid(from) || !IsInstanceValid(to) || !from.IsInsideTree()) return;
        HaneBlastPlays++;
        Vector3 impact = to.FxPoint;
        Vector3 forward = to.Position - from.Position;
        forward.Y = 0;
        forward = forward.Normalized();
        if (forward.LengthSquared() < 0.01f) forward = Vector3.Right * (from.Team == 0 ? 1 : -1);
        Vector3 side = new(-forward.Z, 0, forward.X);
        Vector3 landing = destination is int slot ? PawnPosition(to.Team, slot) : to.Position + forward * 2.2f;
        to.AnimationSpeed = speed;
        to.BlastFlight(landing, destination is not null, true, from.Team == 0 ? -1 : 1,
            landed: MovementLandingSound(to));
        // 蹴りの接触音に、敵を射出する氷の衝撃を重ねて手番の威力を強調する。
        _attackAudio.PlayMovementSound(MovementSound.Collision);
        MovementFx.Smash(_fxRoot, _camera, impact, MovementFx.Bounce, 0.32 / speed, 1.6f);
        MovementFx.Flow(_fxRoot, _camera, impact, landing + Vector3.Up * 1.2f,
            MovementFx.Bounce, 0.35 / speed, 0.12f, 6);
        CameraPunch(impact, AttackPattern.All);

        // 台本の被弾者だけを、前から順にピンのように弾く。衝突判定や追加ダメージは作らない。
        int n = 0;
        foreach (var pin in hits.Where(p => p != to && IsInstanceValid(p)).OrderBy(p => p.Position.DistanceSquaredTo(to.Position)))
        {
            int index = n++;
            float sign = index % 2 == 0 ? 1 : -1;
            double delay = 0.055 + index * 0.045;
            pin.AnimationSpeed = speed;
            pin.BlastFlight(pin.Position + forward * (0.75f + index * 0.1f) + side * sign * 0.9f,
                false, false, sign, delay, MovementLandingSound(pin, secondary: true));
            HanePinPlays++;
            Vector3 point = pin.FxPoint;
            var fx = new Node3D();
            _fxRoot.AddChild(fx);
            var tween = fx.CreateTween();
            tween.TweenInterval(delay / speed);
            tween.TweenCallback(Callable.From(() => {
                MovementFx.Smash(fx, _camera, point, MovementFx.Bounce, 0.22 / speed, 0.85f);
                if (generation == _specialGeneration && audioGeneration == _attackAudio.MovementSoundGeneration
                    && IsInstanceValid(pin) && pin.Hp > 0)
                    _attackAudio.PlayMovementSound(MovementSound.Collision, secondary: true);
            }));
            tween.TweenInterval(0.28 / speed);
            tween.TweenCallback(Callable.From(fx.QueueFree));
        }
        await ToSignal(GetTree().CreateTimer(0.16 / speed), SceneTreeTimer.SignalName.Timeout);
    }
}
