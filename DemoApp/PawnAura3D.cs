using Godot;
using System;
using System.Collections.Generic;

// 駒に追従する短い霧。外部アセットや戦闘の乱数を使わず、色と流れで種類を分ける。
public partial class PawnAura3D : Node3D
{
    public enum AuraKind { PowerUp, PowerDown }
    private readonly List<(MeshInstance3D Mesh, Vector3 Start, float Phase)> _clouds = new();
    private readonly List<MeshInstance3D> _focusRings = new();
    private ShaderMaterial _material = null!;
    private AuraKind _kind;
    private float _age;
    private float _height;
    private float _strength;
    private float Lifetime => _kind == AuraKind.PowerDown ? 1.05f : 0.85f;

    public void Configure(AuraKind kind, float height, int amount)
    {
        _kind = kind;
        _height = height;
        _strength = Math.Clamp(Math.Abs((float)amount) / 50f, 0.2f, 1f);
        Color tint = kind switch
        {
            AuraKind.PowerUp => Color.FromHtml("#ff543b"),
            _ => Color.FromHtml("#426bbb"),
        };
        _material = new ShaderMaterial
        {
            Shader = new Shader { Code = @"shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;
uniform vec4 tint : source_color;
uniform float opacity = 0.0;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
        INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float soft = exp(-dot(p,p) * 3.5) * (1.0 - smoothstep(0.65, 1.0, length(p)));
    float swirl = 0.74 + 0.26 * sin(p.x * 9.0 + sin(p.y * 7.0 + TIME * 3.0));
    ALBEDO = tint.rgb;
    ALPHA = soft * swirl * opacity;
}" },
        };
        _material.SetShaderParameter("tint", tint);
        int cloudCount = kind == AuraKind.PowerUp ? 20 : 14;
        for (int i = 0; i < cloudCount; i++)
        {
            float phase = i / (float)cloudCount;
            float angle = i * 2.39996f;
            float radius = 0.30f + (i % 3) * 0.19f;
            bool streak = kind == AuraKind.PowerUp && i % 2 == 0;
            float width = kind == AuraKind.PowerDown ? 1.05f : streak ? 0.22f : 0.80f;
            var mesh = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(width, streak ? 1.15f : width * 0.85f) },
                MaterialOverride = _material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(mesh);
            // 手前にも薄い霧を置く。顔とHPバーまで覆わず、足元から胴へ流す。
            Vector3 start = new(Mathf.Cos(angle) * radius,
                0.18f + phase * height * 0.75f, Mathf.Sin(angle) * radius + 0.18f);
            _clouds.Add((mesh, start, phase));
        }
        if (kind == AuraKind.PowerUp)
        {
            // 赤い霧だけでは立ち絵へ埋もれるため、対象の足元を二重の発光輪で指す。
            // 常設の陣営リングより一回り大きくし、誰に上昇が入ったかを遠目でも追えるようにする。
            Color hot = Color.FromHtml("#ff4b32");
            foreach (var (inner, outer) in new[] { (0.78f, 0.92f), (1.02f, 1.13f) })
            {
                var ring = new MeshInstance3D
                {
                    Mesh = new TorusMesh { InnerRadius = inner, OuterRadius = outer, Rings = 36, RingSegments = 8 },
                    Position = new Vector3(0, 0.085f, 0),
                    MaterialOverride = GlowMaterial(hot),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                AddChild(ring);
                _focusRings.Add(ring);
            }
        }
        _Process(0);
    }

    private static StandardMaterial3D GlowMaterial(Color tint) => new()
    {
        AlbedoColor = new Color(tint, 0.92f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        EmissionEnabled = true,
        Emission = tint,
        EmissionEnergyMultiplier = 2.1f,
    };

    public void Refresh(int amount)
    {
        _age = Math.Min(_age, 0.12f);
        _strength = Math.Max(_strength, Math.Clamp(Math.Abs((float)amount) / 50f, 0.2f, 1f));
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        float t = _age / Lifetime;
        if (t >= 1) { QueueFree(); return; }
        float fade = Mathf.SmoothStep(0, 0.14f, t) * (1 - Mathf.SmoothStep(0.48f, 1, t));
        float baseOpacity = _kind == AuraKind.PowerUp ? 0.72f : 0.5f;
        _material.SetShaderParameter("opacity", fade * (baseOpacity + 0.22f * _strength));
        foreach (var (mesh, start, phase) in _clouds)
        {
            float direction = _kind == AuraKind.PowerDown ? -1 : 1;
            float rise = _kind == AuraKind.PowerUp ? 1.0f : 0.55f;
            mesh.Position = start + new Vector3(
                Mathf.Sin(t * 4 + phase * Mathf.Tau) * 0.13f,
                direction * t * _height * rise + (_kind == AuraKind.PowerDown ? _height * 0.3f : 0), 0);
        }
        for (int i = 0; i < _focusRings.Count; i++)
        {
            float pulse = 1.0f + Mathf.Sin(t * Mathf.Pi * 3 + i * 1.4f) * 0.08f;
            _focusRings[i].Scale = Vector3.One * (Mathf.Lerp(0.55f, 1.18f, Mathf.SmoothStep(0, 0.5f, t)) * pulse);
            _focusRings[i].Transparency = Mathf.SmoothStep(0.42f, 1.0f, t);
        }
    }
}
