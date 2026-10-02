using Godot;

public partial class BattlePawn3D
{
    // 席や戦闘上の位置は動かさない。既存の移動Tweenで踏み込みと復帰を管理する。
    internal void FireBrace(Vector3 target, bool strike)
    {
        if (!_alive || _victory) return;
        Vector3 forward = new(target.X - RestPosition.X, 0, target.Z - RestPosition.Z);
        if (forward.LengthSquared() < 0.001f) return;
        var tween = BeginMotion();
        tween.TweenProperty(this, "position", RestPosition + forward.Normalized() * (strike ? 0.8f : -0.20f),
            (strike ? 0.09 : 0.35) / AnimationSpeed).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }
}
