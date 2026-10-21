using Godot;
using System;

public partial class BattlePawn3D
{
    private Vector3? _gareRecoilOrigin;
    private float _gareRecoilAngle, _gareShadowY, _gareRingY, _gareTurnRingY;
    internal float GatherRecoilDistance { get; private set; }
    internal float GatherRecoilLift { get; private set; }
    // 待機ループは縮小した連番。アンカーは原画の1024×1536座標で指定する。
    internal Vector3 GatherPortraitPoint(Vector2 pixel, Camera3D camera)
        => MovementPortraitPoint(new Vector2(pixel.X * _sprite.Texture.GetWidth() / 1024f,
            pixel.Y * _sprite.Texture.GetHeight() / 1536f), camera);
    internal void BeginGatherPose(string key)
    {
        ShowMovementPortrait(key, 3600);
        if (key != "gare_fire" || _gareRecoilOrigin is not null || !PresentationAlive) return;
        _motion?.Kill();
        _gareRecoilOrigin = Position;
        _gareShadowY = _shadow.Position.Y; _gareRingY = _ring.Position.Y; _gareTurnRingY = _turnRing.Position.Y;
    }
    internal void EndGatherPose()
    {
        if (_movementPortrait is "gare_load" or "gare_fire" or "gare_empty" or "susu_read" or "susu_cast")
            ClearMovementPortrait();
        ResetGatherRecoil();
    }
    internal void GatherBlastRecoil(float age, float power, bool focused, float fireAt)
    {
        if (_movementPortrait != "gare_fire" || !PresentationAlive || _gareRecoilOrigin is not Vector3 origin) return;
        float time = MathF.Max(0, age - fireAt);
        float kick = time < .12f ? 1 - MathF.Pow(1 - time / .12f, 3)
            : time < .47f ? 1 - .2f * (time - .12f) / .35f
            : .8f * (1 - Mathf.SmoothStep(0, 1, Mathf.Clamp((time - .47f) / .55f, 0, 1)));
        float side = Team == BattleCore.BattleContext.PlayerTeam ? -1 : 1;
        GatherRecoilDistance = kick * (focused ? 1.05f : .78f) * (.65f + .35f * power);
        GatherRecoilLift = MathF.Sin(Mathf.Clamp(time / .48f, 0, 1) * Mathf.Pi) * (focused ? .52f : .36f);
        _gareRecoilAngle = -side * kick * (focused ? .32f : .22f);
        Position = origin + new Vector3(side * GatherRecoilDistance, GatherRecoilLift, 0);
        _shadow.Position = new Vector3(_shadow.Position.X, _gareShadowY - GatherRecoilLift, _shadow.Position.Z);
        _ring.Position = new Vector3(_ring.Position.X, _gareRingY - GatherRecoilLift, _ring.Position.Z);
        _turnRing.Position = new Vector3(_turnRing.Position.X, _gareTurnRingY - GatherRecoilLift, _turnRing.Position.Z);
        ApplyGatherRecoilPose();
    }
    private void ApplyGatherRecoilPose()
    {
        if (_gareRecoilOrigin is not null && _movementPortrait == "gare_fire")
            _sprite.Rotation = new Vector3(0, 0, _gareRecoilAngle);
    }
    private void ResetGatherRecoil()
    {
        if (_gareRecoilOrigin is not Vector3 origin) return;
        Position = origin; _gareRecoilOrigin = null;
        _shadow.Position = new Vector3(_shadow.Position.X, _gareShadowY, _shadow.Position.Z);
        _ring.Position = new Vector3(_ring.Position.X, _gareRingY, _ring.Position.Z);
        _turnRing.Position = new Vector3(_turnRing.Position.X, _gareTurnRingY, _turnRing.Position.Z);
        GatherRecoilDistance = GatherRecoilLift = _gareRecoilAngle = 0;
        _sprite.Rotation = Vector3.Zero;
    }
}
