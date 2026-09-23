using Godot;
using System;

public partial class BattlePawn3D
{
    private Sprite3D? _rapierGlow;
    private ShaderMaterial? _rapierMaterial;
    private Vector3? _thrustPosition;
    internal int ThrustCharge { get; private set; }

    // 回数を数え直さず、台本の累計をそのまま読む。4段を超える値も表示上だけ丸める。
    public void SetThrustCharge(int amount)
    {
        ThrustCharge = _alive && !_victory ? Math.Max(0, amount) : 0;
        if (_unitId != "sora") return;
        if (_rapierGlow is null && ThrustCharge > 0)
        {
            _rapierMaterial = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;
uniform float charge = 0.0;
void fragment() {
    // sora_idle_right の鍔から剣先。Sprite3D の FlipH が敵側も反転させる。
    vec2 a = vec2(0.655, 0.235);
    vec2 b = vec2(0.984, 0.218);
    vec2 blade = b - a;
    float along = dot(UV - a, blade) / dot(blade, blade);
    float segment = floor(clamp(along, 0.0, 0.999) * 4.0);
    float lit = step(segment + 1.0, charge);
    float edge = smoothstep(0.0, 0.08, fract(along * 4.0))
        * (1.0 - smoothstep(0.84, 1.0, fract(along * 4.0)));
    float distance_to_blade = length((UV - (a + clamp(along, 0.0, 1.0) * blade)) * vec2(0.667, 1.0));
    float core = exp(-distance_to_blade * distance_to_blade / 0.000012);
    float halo = exp(-distance_to_blade * distance_to_blade / (0.000045 + charge * 0.000012));
    float pulse = 0.92 + 0.08 * sin(TIME * 4.0);
    ALBEDO = mix(vec3(1.0, 0.43, 0.08), vec3(1.0, 0.96, 0.76), core);
    ALPHA = lit * edge * (core + halo * 0.32) * pulse
        * step(0.0, along) * step(along, 1.0);
}" } };
            _rapierGlow = new Sprite3D
            {
                Texture = _sprite.Texture, PixelSize = _sprite.PixelSize,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FlipH = _sprite.FlipH, MaterialOverride = _rapierMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_rapierGlow);
        }
        if (_rapierGlow is null) return;
        _rapierGlow.Visible = ThrustCharge > 0;
        _rapierMaterial!.SetShaderParameter("charge", Math.Min(4, ThrustCharge));
        UpdateRapierGlow();
    }

    private void UpdateRapierGlow()
    {
        if (_rapierGlow is null || !_rapierGlow.Visible) return;
        var camera = GetViewport().GetCamera3D();
        Vector3 lift = camera is null ? Vector3.Zero : ToLocal(GlobalPosition + camera.GlobalBasis.Z * 0.035f);
        _rapierGlow.Position = _sprite.Position + lift;
        _rapierGlow.Scale = _sprite.Scale;
        _rapierGlow.Rotation = _sprite.Rotation;
    }

    public Vector3 RapierTip(Camera3D camera)
    {
        float width = _sprite.Texture.GetWidth() * _sprite.PixelSize;
        float height = _sprite.Texture.GetHeight() * _sprite.PixelSize;
        float facing = _sprite.FlipH ? -1 : 1;
        return _sprite.GlobalPosition + camera.GlobalBasis.X * (width * 0.484f * facing)
            + camera.GlobalBasis.Y * (height * 0.282f);
    }

    // X字の席順はそのまま。貫きの一拍だけ、台本の被弾者を直線上へ寄せる。
    // Home / Slot は書き換えず、被弾の揺れもこの表示位置へ戻す。
    public void BeginThrustPose(Vector3 position, double duration)
    {
        _thrustPosition = position;
        var tween = BeginMotion();
        tween.TweenProperty(this, "position", position, duration)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    public void EndThrustPose(double duration)
    {
        if (_thrustPosition is null) return;
        _thrustPosition = null;
        if (!_alive) return;
        var tween = BeginMotion();
        tween.TweenProperty(this, "position", RestPosition, duration)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
    }

    public void AnimateThrust(Vector3 direction, double duration)
    {
        if (!_alive) return;
        var tween = BeginMotion();
        tween.TweenProperty(this, "position", RestPosition + direction * 0.32f, duration * 0.25)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "position", RestPosition, duration * 0.75)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
    }
}
