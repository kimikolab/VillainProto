using Godot;

/// <summary>毒の在庫は漂う泡、毒の被弾は一度だけ強まる瘴気で見せる。</summary>
public partial class PoisonEffect3D : Node3D
{
    private ShaderMaterial _material = null!;
    private bool _active;
    private float _pulse;

    public void Configure(float phase)
    {
        var shader = new Shader { Code = @"shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;
uniform float phase = 0.0;
uniform float pulse = 0.0;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    float t = TIME * 0.45 + phase;
    float bubbles = 0.0;
    float glow = 0.0;
    for (int i = 0; i < 9; i++) {
        float k = float(i);
        float rise = fract(t * (0.65 + fract(k * 0.37) * 0.4) + k * 0.173);
        float x = 0.12 + fract(k * 0.618) * 0.76 + sin(t * 2.0 + k) * 0.055;
        vec2 p = vec2(x, 0.94 - rise * 0.82);
        float r = (0.022 + fract(k * 0.31) * 0.017) * (1.0 + pulse * 1.2);
        float d = length((UV - p) * vec2(1.25, 1.0));
        float fade = smoothstep(0.0, 0.12, rise) * (1.0 - smoothstep(0.65, 1.0, rise));
        bubbles = max(bubbles, (1.0 - smoothstep(r * 0.65, r, d)) * fade);
        glow = max(glow, (1.0 - smoothstep(r, r * 1.8, d)) * fade);
    }
    float mist = exp(-dot((UV - vec2(0.5, 0.79)) * vec2(3.2, 7.0), (UV - vec2(0.5, 0.79)) * vec2(3.2, 7.0)));
    mist *= 0.7 + 0.3 * sin(UV.x * 19.0 + t * 3.0 + sin(UV.y * 15.0 - t));
    float wave = (1.0 - smoothstep(0.025, 0.10, abs(length((UV - vec2(0.5, 0.6)) * vec2(1.25, 1.0)) - (1.0 - pulse) * 0.65))) * sqrt(pulse);
    vec3 tint = mix(vec3(0.48, 0.10, 0.70), vec3(0.87, 0.57, 1.0), bubbles * 0.65);
    ALBEDO = tint;
    EMISSION = tint * (0.35 + pulse);
    ALPHA = clamp(bubbles * 0.85 + glow * 0.15 + mist * (0.28 + pulse * 0.50) + wave * 0.90, 0.0, 0.90);
}" };
        _material = new ShaderMaterial { Shader = shader };
        _material.SetShaderParameter("phase", phase);
        AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2.25f, 1.8f) },
            Position = new Vector3(0, 0.95f, 0.45f),
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        Visible = false;
        SetProcess(false);
    }

    public void SetActive(bool active)
    {
        _active = active;
        Visible = active || _pulse > 0;
    }

    public void Pulse()
    {
        _pulse = 1;
        _material.SetShaderParameter("pulse", _pulse);
        Visible = true;
        SetProcess(true);
    }

    public void Clear()
    {
        _pulse = 0;
        _material.SetShaderParameter("pulse", 0.0f);
        SetActive(false);
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        _pulse = Mathf.Max(0, _pulse - (float)delta / 0.65f);
        _material.SetShaderParameter("pulse", _pulse);
        if (_pulse > 0) return;
        Visible = _active;
        SetProcess(false);
    }
}
