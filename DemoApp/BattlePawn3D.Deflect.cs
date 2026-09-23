using Godot;

public partial class BattlePawn3D
{
    // 席も向きも変えず、赤い布を払う間だけ半身を横へ逃がす。
    public void AnimateDeflection(Vector3 sideways, double duration)
    {
        if (!_alive) return;
        Vector3 side = new(sideways.X, 0, sideways.Z);
        side = side.Normalized();
        var tween = BeginMotion();
        tween.TweenProperty(this, "position", RestPosition + side * 0.34f, duration * 0.28)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(duration * 0.32);
        tween.TweenProperty(this, "position", RestPosition, duration * 0.40)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
    }
}
