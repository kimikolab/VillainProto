using Godot;
using System;

public partial class BattlePawn3D
{
    private bool _vendettaMoving;
    internal void EndVendetta()
    {
        if (!_vendettaMoving) return;
        _vendettaMoving = false;
        _motion?.Kill();
        if (_alive && !_victory) Position = RestPosition;
        if (_movementPortrait == "zan_vendetta") ClearMovementPortrait();
    }
    // 席とHomeを変えず、短剣の間合いにだけ踏み込んで帰る。
    internal void BeginVendetta(Vector3 target, double seconds)
    {
        if (!_alive || _victory) return;
        Vector3 origin = RestPosition;
        Vector3 direction = new(target.X - origin.X, 0, target.Z - origin.Z);
        Vector3 contact = target - direction.Normalized() * 1.75f;
        contact.Y = origin.Y;
        ShowMovementPortrait("zan_vendetta", seconds, flip: direction.X < 0);
        BeginBonusAfterimage(new Color("bb234c"), seconds / AnimationSpeed, false, 0.6f);
        var tween = BeginMotion();
        _vendettaMoving = true;
        tween.TweenInterval(0.10 / AnimationSpeed);
        tween.TweenProperty(this, "position", contact, 0.13 / AnimationSpeed)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(Math.Max(0.01, seconds - 0.40) / AnimationSpeed);
        tween.TweenProperty(this, "position", origin, 0.17 / AnimationSpeed)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tween.TweenCallback(Callable.From(() => _vendettaMoving = false));
    }
}
