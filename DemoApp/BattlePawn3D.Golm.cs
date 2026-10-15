using Godot;

public partial class BattlePawn3D
{
    private bool _golmConsumed, _golmProxy, _golmDiscarded, _golmRocketPose;
    private Vector3 _golmDiscardPoint;
    internal bool GolmConsumed => _golmConsumed;
    internal bool GolmUsesProxy => _golmProxy;
    internal bool GolmDiscarded => _golmDiscarded;
    internal bool GolmRocketPose => _golmRocketPose;
    internal Vector3 GolmDiscardPoint(Camera3D camera) => Home + Vector3.Up * .25f
        + camera.GlobalBasis.Y * _portraitHeight * .12f + camera.GlobalBasis.Z * .12f;
    internal Vector3 GolmMouth(Camera3D camera) => _golmRocketPose
        ? MovementPortraitPoint(new Vector2(578,631),camera) + camera.GlobalBasis.Z * .12f
        : _sprite.GlobalPosition + camera.GlobalBasis.Y * _portraitHeight * .11f
        - camera.GlobalBasis.X * _portraitHeight * .05f * (Team == 0 ? 1 : -1) + camera.GlobalBasis.Z * .12f;
    internal Vector3 GolmWrist(Camera3D camera) => _golmRocketPose
        ? MovementPortraitPoint(new Vector2(974,449),camera) + camera.GlobalBasis.Z * .16f
        : _sprite.GlobalPosition + camera.GlobalBasis.Y * _portraitHeight * .18f
        + camera.GlobalBasis.X * _portraitHeight * .19f * (Team == 0 ? 1 : -1) + camera.GlobalBasis.Z * .16f;
    // 腕の時計が帰還・中断を通知するまで、右向きの発射姿勢を保つ。
    internal void SetGolmRocketPose(bool active)
    {
        if (_unitId != "golm" || _golmRocketPose == active) return;
        _golmRocketPose = active;
        RefreshBattlePortrait();
    }

    internal Sprite3D MakeGolmProxy(Node3D parent)
    {
        var proxy = new Sprite3D {
            Texture = _sprite.Texture, PixelSize = _sprite.PixelSize,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, FlipH = _sprite.FlipH,
            MaterialOverride = (ShaderMaterial)_portraitMaterial.Duplicate(),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        parent.AddChild(proxy); proxy.GlobalPosition = _sprite.GlobalPosition;
        _golmProxy = true; _sprite.Hide(); _shadow.Hide(); _ring.Hide();
        return proxy;
    }

    internal void PrepareGolmSwallow() => _golmConsumed = true;
    internal void LeaveGolmDiscarded(Vector3 point)
    {
        _golmDiscarded = true; _golmProxy = false; _golmDiscardPoint = point;
        // 投げ捨てた体は元の色と輪郭を保ち、戦闘中はその場に残す。
        SetGolmDrain(0, 0);
        Hud.Retire(fade: false);
        ShowGolmDiscardedPose();
    }
    private void ShowGolmDiscardedPose()
    {
        if (!_golmDiscarded) return;
        _sprite.Show(); _shadow.Show(); _ring.Hide();
        _sprite.GlobalPosition = _golmDiscardPoint;
        _sprite.Rotation = new Vector3(0, 0, (Team == 0 ? -1 : 1) * 1.15f);
        _sprite.Scale = Vector3.One * .82f;
        _sprite.Modulate = Colors.White;
    }
    internal void SetGolmDrain(float gray, float crumble)
    {
        _portraitMaterial.SetShaderParameter("golm_gray", gray);
        _portraitMaterial.SetShaderParameter("golm_crumble", crumble);
    }

    internal void ClearGolmPresentation()
    {
        _golmProxy = _golmConsumed = _golmDiscarded = false;
        SetGolmDrain(0, 0); SetGolmRocketPose(false);
        _sprite.Visible = true; _shadow.Visible = true; _ring.Visible = _alive;
        _sprite.Modulate = Colors.White;
        if (_alive) Hud.Restore();
    }
}
