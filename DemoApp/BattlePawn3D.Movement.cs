using Godot;
using System;

public partial class BattlePawn3D
{
    private float _movementPoseTime;
    private float _movementPoseLength;
    private float _movementLean;

    public void MovementPose(float lean, float seconds = 0.30f)
    {
        if (!_alive || _victory) return;
        if (_movementPoseTime > 0 && Math.Abs(_movementLean) > Math.Abs(lean)) return;
        _movementLean = lean * (Team == 0 ? 1 : -1);
        _movementPoseTime = _movementPoseLength = seconds;
    }

    private void ProcessMovementPose(float delta)
    {
        if (_movementPoseTime <= 0) return;
        _movementPoseTime = Math.Max(0, _movementPoseTime - delta);
        float wave = Mathf.Sin((1 - _movementPoseTime / _movementPoseLength) * Mathf.Pi);
        // 差分に描かれた姿勢へ大きな回転を二重に掛けない。
        if (_movementPortrait is not null) wave *= 0.15f;
        _sprite.Rotation += new Vector3(0, 0, _movementLean * wave);
        _sprite.Position += Vector3.Down * Math.Abs(_movementLean) * wave * 0.25f;
    }

    public void AnimateMovement(Vector3 target, float arc = 0.12f, double seconds = 0.18, Action? landed = null, bool windCarry = false)
    {
        if (!_alive || _victory) return;
        if (_blastActive) Position = _blastVisualPosition;
        ClearBlastPose();
        _guardPosition = null;
        _comboPosition = null;
        _home = target;
        Vector3 start = Position;
        var tween = BeginMotion();
        _windCarryActive = windCarry;
        Vector3 side = (target - start).Cross(Vector3.Up).Normalized();
        tween.TweenMethod(Callable.From<float>(t => {
            float travel = windCarry ? Mathf.SmoothStep(0, 1, t) : t;
            Position = start.Lerp(target, travel) + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * arc;
            if (windCarry)
            {
                _windCarryProgress = t;
                Position += side * Mathf.Sin(t * Mathf.Tau) * Mathf.Sin(t * Mathf.Pi) * 0.48f;
            }
        }), 0f, 1f, Math.Max(0.005, seconds / AnimationSpeed));
        if (windCarry) tween.TweenCallback(Callable.From(ClearWindCarry));
        if (landed is not null) tween.TweenCallback(Callable.From(() => { if (_alive && !_victory) landed(); }));
    }

    private bool _windCarryActive;
    private float _windCarryProgress;
    internal bool WindCarried => _windCarryActive;
    internal Vector3 WindCarryCenter => GlobalPosition + Vector3.Up * _portraitBaseY;

    private void ClearWindCarry()
    {
        _windCarryActive = false;
        _windCarryProgress = 0;
    }

    private void ProcessWindCarry()
    {
        if (!_windCarryActive) return;
        // 名前やHPバーは回さず、本体だけを渦に沿って一周させる。
        _sprite.Rotation += Vector3.Back * Mathf.Tau * Mathf.SmoothStep(0, 1, _windCarryProgress) * (Team == 0 ? -1 : 1);
    }
}
