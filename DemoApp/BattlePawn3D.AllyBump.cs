using Godot;
using System;
using System.Threading.Tasks;

public partial class BattlePawn3D
{
    private Tween? _allyBumpTween;
    private Vector3 _allyBumpOffset;
    private TaskCompletionSource? _allyBumpImpact;
    internal Task AllyBumpImpact => _allyBumpImpact?.Task ?? Task.CompletedTask;
    internal bool AllyBumpActive => _allyBumpTween is not null;

    // お尻の接点を味方の手前へ寄せる。名前・HP・席はその場に残す。
    internal void BeginAllyBump(string portrait, bool flip, Camera3D camera, Func<Vector3> contact, Action hit)
    {
        if (_unitId != "hane" || !CanReceiveMovementImpact) return;
        ClearMovementPortrait();
        ShowMovementPortrait(portrait, 0.60, flip: flip);
        _allyBumpImpact = new TaskCompletionSource();
        Vector2 pixel = portrait == "hane_bump_rebound" ? new(910, 475) : new(755, 740);
        ProcessAllyBumpPose();
        Vector3 anchor = MovementPortraitPoint(pixel, camera) - GlobalPosition;
        Vector3 offset = Vector3.Zero;
        double speed = Math.Max(0.1, AnimationSpeed);
        _allyBumpTween = CreateTween();
        _allyBumpTween.TweenMethod(Callable.From<float>(t => {
            // 直前のMoveがまだ着地していなくても、現在の本体位置から合わせ直す。
            offset = contact() - (GlobalPosition + anchor);
            _allyBumpOffset = offset * Mathf.SmoothStep(0, 1, t) + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * 0.16f;
        }), 0f, 1f, 0.16 / speed);
        _allyBumpTween.TweenCallback(Callable.From(() => {
            ProcessAllyBumpPose();
            if (CanReceiveMovementImpact) hit();
            _allyBumpImpact?.TrySetResult();
        }));
        _allyBumpTween.TweenInterval(0.045 / speed);
        _allyBumpTween.TweenMethod(Callable.From<float>(t =>
            _allyBumpOffset = offset * (1 - Mathf.SmoothStep(0, 1, t))), 0f, 1f, 0.20 / speed);
        _allyBumpTween.TweenCallback(Callable.From(ClearMovementPortrait));
    }

    private void ProcessAllyBumpPose()
    {
        if (_movementPortrait is not ("hane_bump_rebound" or "hane_bump_apology")) return;
        _sprite.Position = new Vector3(0, _portraitBaseY, 0) + _allyBumpOffset;
        _sprite.Rotation = Vector3.Zero;
        _sprite.Scale = Vector3.One;
    }

    private void ClearAllyBumpPose()
    {
        _allyBumpTween?.Kill();
        _allyBumpTween = null;
        _allyBumpOffset = Vector3.Zero;
        var pending = _allyBumpImpact;
        _allyBumpImpact = null;
        pending?.TrySetResult();
    }
}
