using Godot;
using System;
using System.Collections.Generic;

public partial class BattlefieldView3D
{
    // 表示だけの乱数。直前の絵を除外する（2枚なので初回を無作為に選び、以後は交互）。
    private readonly Random _landingRandom = new();
    private readonly Dictionary<int, string> _landingVariants = new();

    private void ShowHaneLanding(BattlePawn3D? actor, BattlePawn3D? target, double speed)
    {
        if (actor?.UnitId != "hane" || !actor.CanReceiveMovementImpact || target is null) return;
        bool rebound = _landingVariants.TryGetValue(actor.InstanceId, out string? previous)
            ? previous != "hane_bump_rebound" : _landingRandom.Next(2) == 0;
        string portrait = rebound ? "hane_bump_rebound" : "hane_bump_apology";
        _landingVariants[actor.InstanceId] = portrait;
        // 両原画とも接触側は画面右。陣営ではなく対象への画面上の方向に合わせる。
        float dx = (target.Home - actor.Home).Dot(_camera.GlobalBasis.X);
        bool flip = Math.Abs(dx) < 0.01f ? actor.Team != 0 : dx < 0;
        actor.AnimationSpeed = speed;
        actor.ShowMovementPortrait(portrait, 0.38, flip: flip);
        actor.MovementPose((flip ? -0.30f : 0.30f) * (actor.Team == 0 ? 1 : -1), 0.18f);
        MakeGroundRing(actor.Home, new Color("e6d1af"), 0.70f, 0.22 / speed);
        MovementFx.Smash(_fxRoot, _camera, actor.FxPoint.Lerp(target.FxPoint, 0.6f),
            MovementFx.Bounce, 0.20 / speed, 0.65f);
        _attackAudio.PlayMovementSound(MovementSound.Collision, secondary: true);
    }
}
