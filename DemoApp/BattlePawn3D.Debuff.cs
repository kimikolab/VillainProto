using Godot;
using System;

public partial class BattlePawn3D
{
    private Func<(bool Paused, double Speed)>? _debuffPoseClock;
    private double _debuffPoseAge = 1;
    private float _debuffPoseStrength;
    internal double DebuffPoseAge => _debuffPoseAge;
    internal bool NelCurseStyled { get; private set; }
    internal int? MudPower { get; set; }
    internal int UtsuHits { get; set; } = 1;
    internal string DebuffAttackSuffix => _unitId == "mudo" && MudPower is int mud ? $"  泥 {mud}"
        : _unitId == "utsu" ? $"  ×{UtsuHits}" : "";

    internal void EnsureDebuffPoseClock(Func<(bool, double)> clock) => _debuffPoseClock = clock;
    internal void SinkFromDebuff(float strength)
    {
        if (!CanReceiveMovementImpact) return;
        _debuffPoseAge = 0; _debuffPoseStrength = strength;
    }
    private void ClearDebuffPose() { _debuffPoseAge = 1; _debuffPoseStrength = 0; }
    private void ProcessDebuffPose(double delta)
    {
        if (_debuffPoseAge >= .34) return;
        var (paused, speed) = _debuffPoseClock?.Invoke() ?? (false, AnimationSpeed);
        if (!paused) _debuffPoseAge = Math.Min(.34, _debuffPoseAge + delta * speed);
        float shrink = Mathf.Sin((float)(_debuffPoseAge / .34) * Mathf.Pi) * _debuffPoseStrength;
        _sprite.Scale *= new Vector3(1 - shrink * .40f, 1 - shrink, 1);
        _sprite.Position -= Vector3.Up * (_portraitGroundDistance * shrink);
    }

    internal void UseNelCurseTalisman()
    {
        NelCurseStyled = true;
        if (_curseStain is not null) { _curseStain.QueueFree(); _curseStain = null; }
    }
}
