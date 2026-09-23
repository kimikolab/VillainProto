using BattleCore;
using Godot;

public partial class BattlePawn3D
{
    private bool _cowed;
    private float _cowedPose, _cowedFlinch, _bracePulse, _fearClock;

    public void SetCowed(bool active)
    {
        if (active && !_cowed) _cowedFlinch = 1;
        _cowed = active;
    }

    public void BraceShield() => _bracePulse = 1;

    public void PulseCowedLost()
    {
        if (!_alive || _victory) return;
        _cowed = true;
        _cowedFlinch = 1.4f;
    }

    public void AddFooting(int amount)
        => _statusIcons.SetAmount(StatusKeys.Footing,
            System.Math.Max(0, _statusIcons.Amount(StatusKeys.Footing) + amount), true);

    private void ResetShieldCowed()
    {
        _cowed = false;
        _cowedPose = _cowedFlinch = _bracePulse = 0;
    }

    private void ProcessShieldCowed(float delta)
    {
        _fearClock += delta;
        _cowedPose = Mathf.MoveToward(_cowedPose, _cowed ? 1 : 0, delta * 5);
        _cowedFlinch = Mathf.MoveToward(_cowedFlinch, 0, delta * 2);
        _bracePulse = Mathf.MoveToward(_bracePulse, 0, delta * 2.4f);
        float shrink = _cowedPose * 0.12f + _cowedFlinch * 0.10f + _bracePulse * 0.09f;
        var scale = _sprite.Scale;
        _sprite.Scale = new Vector3(scale.X - _cowedPose * 0.05f + _bracePulse * 0.06f, scale.Y - shrink, scale.Z);
        // 足元は固定。既存の転倒姿勢にも重ねられる。
        _sprite.Position += new Vector3(Mathf.Sin(_fearClock * 39) * (0.017f * _cowedPose + 0.055f * _cowedFlinch),
            -_portraitGroundDistance * shrink, 0);
    }
}
