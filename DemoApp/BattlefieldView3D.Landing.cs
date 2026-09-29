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
        if (actor?.UnitId != "hane" || !actor.CanReceiveMovementImpact || target is null || !target.CanReceiveMovementImpact) return;
        bool rebound = _landingVariants.TryGetValue(actor.InstanceId, out string? previous)
            ? previous != "hane_bump_rebound" : _landingRandom.Next(2) == 0;
        string portrait = rebound ? "hane_bump_rebound" : "hane_bump_apology";
        _landingVariants[actor.InstanceId] = portrait;
        // 両原画とも接触側は画面右。陣営ではなく対象への画面上の方向に合わせる。
        float dx = (target.Home - actor.Home).Dot(_camera.GlobalBasis.X);
        bool flip = Math.Abs(dx) < 0.01f ? actor.Team != 0 : dx < 0;
        actor.AnimationSpeed = speed;
        int generation = _specialGeneration;
        int audioGeneration = _attackAudio.MovementSoundGeneration;
        Vector3 edge = _camera.GlobalBasis.X * (flip ? 0.40f : -0.40f);
        Vector3 last = target.FxPoint + edge;
        Vector3 Contact() => IsInstanceValid(target) ? target.FxPoint + edge : last;
        // 対象の足元を先に示し、接触の一拍に星と音を揃える。
        MakeGroundRing(target.Home, new Color("ffcb70"), 0.78f, 0.38 / speed);
        actor.BeginAllyBump(portrait, flip, _camera, Contact, () => {
            if (generation != _specialGeneration || audioGeneration != _attackAudio.MovementSoundGeneration
                || !IsInsideTree() || !IsInstanceValid(target) || !target.IsInsideTree()
                || !target.CanReceiveMovementImpact) return;
            MovementFx.Smash(_fxRoot, _camera, Contact(), new Color("ffcb70"), 0.22 / speed, 0.95f);
            _attackAudio.PlayMovementSound(MovementSound.AllyBump);
        });
    }
}
