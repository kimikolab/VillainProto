using Godot;
using System;

public partial class BattlePawn3D
{
    private Func<(bool Paused, double Speed)>? _kubiBellClock;
    internal double KubiBellAge { get; private set; } = KubiBellFx3D.Duration;

    internal void TrembleWithKubiBell(Func<(bool, double)> clock)
    {
        if (_unitId != "kubi" || !CanReceiveMovementImpact) return;
        _kubiBellClock = clock;
        KubiBellAge = 0;
    }

    internal void ClearKubiBell() => KubiBellAge = KubiBellFx3D.Duration;
    private bool KubiPaused => _unitId == "kubi" && (_kubiBellClock?.Invoke().Item1 ?? false);

    // 手番で漏れた鐘音の間だけ震える。常時の待機ループではない。
    private void ProcessKubiBell(double delta)
    {
        if (_unitId != "kubi" || KubiBellAge >= KubiBellFx3D.Duration) return;
        var (paused, speed) = _kubiBellClock?.Invoke() ?? (false, AnimationSpeed);
        if (!paused) KubiBellAge = Math.Min(KubiBellFx3D.Duration, KubiBellAge + delta * speed);
        float t = (float)(KubiBellAge / KubiBellFx3D.Duration);
        float envelope = Mathf.Sin(t * Mathf.Pi);
        float tremble = Mathf.Sin(t * Mathf.Tau * 7) * envelope;
        float shrink = envelope * .055f;
        _sprite.Rotation += Vector3.Back * tremble * .026f;
        _sprite.Scale *= new Vector3(1 - shrink * .3f, 1 - shrink, 1);
        _sprite.Position -= Vector3.Up * (_portraitGroundDistance * shrink);
    }

    internal Vector3 KubiBellPoint(Camera3D camera)
        => MovementPortraitPoint(new Vector2(670, 645), camera);
}
