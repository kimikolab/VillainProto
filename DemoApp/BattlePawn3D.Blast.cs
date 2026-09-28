using Godot;
using System;

public partial class BattlePawn3D
{
    private Tween? _blastTween;
    private bool _blastActive;
    private Vector3 _blastVisualPosition;
    private Vector3? _blastLanding;
    private float _blastRoll;
    private float _blastStretch;
    private Action? _blastLanded;
    internal bool BlastActive => _blastActive;
    internal Vector3 BlastVisualPosition => _blastActive ? _blastVisualPosition : Position;

    // 見た目だけを投げる。被弾の小さな揺れに上書きされず、席の更新は後続のMoveに任せる。
    internal void BlastFlight(Vector3 destination, bool holdForMove, bool primary, float side, double delay = 0, Action? landed = null)
    {
        if (!_alive || _victory) return;
        ClearBlastPose();
        _blastLanded = landed;
        Vector3 start = Position;
        _blastVisualPosition = start;
        _blastActive = true;
        _blastLanding = holdForMove ? destination : null;
        double speed = Math.Max(0.1, AnimationSpeed);
        _blastTween = CreateTween();
        if (delay > 0) _blastTween.TweenInterval(delay / speed);
        _blastTween.TweenMethod(Callable.From<float>(t => {
            float travel = 1 - Mathf.Pow(1 - t, 3);
            float hop = Mathf.Sin(t * Mathf.Pi);
            Vector3 flight = start.Lerp(destination, travel) + Vector3.Up * hop * (primary ? 1.75f : 1.10f);
            if (!holdForMove && t > 0.65f)
                flight = flight.Lerp(start, Mathf.SmoothStep(0.65f, 1, t));
            _blastVisualPosition = flight;
            _blastRoll = side * hop * (primary ? 1.7f : 2.5f);
            _blastStretch = Mathf.Sin(Mathf.Min(1, t * 3) * Mathf.Pi) * 0.20f;
        }), 0f, 1f, (primary ? 0.42 : 0.36) / speed);
        _blastTween.TweenCallback(Callable.From(NotifyBlastLanded));
        if (!holdForMove) _blastTween.TweenCallback(Callable.From(ClearBlastPose));
    }

    private void ProcessBlastPose()
    {
        if (!_blastActive) return;
        _sprite.Position += _blastVisualPosition - Position;
        _sprite.Rotation += Vector3.Back * _blastRoll;
        _sprite.Scale *= new Vector3(1 + _blastStretch, 1 - _blastStretch * 0.5f, 1);
    }

    internal bool FinishBlastMove(Vector3 destination)
    {
        if (_blastLanding is not Vector3 landing || landing.DistanceSquaredTo(destination) > 0.001f) return false;
        NotifyBlastLanded();
        ClearBlastPose();
        _motion?.Kill();
        _guardPosition = _comboPosition = null;
        Position = _home = destination;
        return true;
    }

    private void NotifyBlastLanded()
    {
        var landed = _blastLanded;
        _blastLanded = null;
        if (_alive && !_victory) landed?.Invoke();
    }

    private void ClearBlastPose()
    {
        _blastLanded = null;
        _blastTween?.Kill();
        _blastTween = null;
        _blastActive = false;
        _blastLanding = null;
        _blastRoll = _blastStretch = 0;
    }
}
