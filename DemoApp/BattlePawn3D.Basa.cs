using Godot;
using System;

public partial class BattlePawn3D
{
    private float _basaFlightTime;
    internal bool BasaFlying => _basaFlightTime > 0;
    internal bool CanBasaFly => _unitId == "basa" && _alive && !_victory;

    internal void BeginBasaFlight()
    {
        if (!CanBasaFly) return;
        _basaFlightTime = 1.15f;
    }

    private void ProcessBasaFlight(float delta)
    {
        if (_basaFlightTime <= 0) return;
        _basaFlightTime = Math.Max(0, _basaFlightTime - delta);
        float elapsed = 1.15f - _basaFlightTime;
        float lift = Mathf.SmoothStep(0, 0.24f, elapsed)
            * (1 - Mathf.SmoothStep(0.72f, 1.15f, elapsed));
        // 席とHP表示は動かさず、羽ばたく本体だけを持ち上げる。
        _sprite.Position += Vector3.Up * (lift * 0.85f);
        float beat = Mathf.Sin(Mathf.Clamp((elapsed - 0.24f) / 0.30f, 0, 1) * Mathf.Pi);
        _sprite.Scale *= new Vector3(1 + beat * 0.10f, 1 - beat * 0.055f, 1);
    }
}
