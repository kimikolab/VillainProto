using Godot;
using System;
using System.Collections.Generic;

// 台本の溜めから次の本来の行動まで残る予兆。時間経過から発動時刻を推測しない。
public partial class ChargeAura3D : Node3D
{
    private readonly List<MeshInstance3D> _lights = new();
    private MeshInstance3D _ring = null!;
    private MeshInstance3D _veil = null!;
    private float _height;
    private float _age;
    private float _releaseAge = -1;
    private float _strength;

    public void Configure(float height, int percent)
    {
        _height = height;
        SetPower(percent);
        _ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.86f, OuterRadius = 0.91f, Rings = 48, RingSegments = 6 },
            Position = new Vector3(0, 0.07f, 0),
            MaterialOverride = LightMaterial(Color.FromHtml("#ffe2a0")),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_ring);
        for (int i = 0; i < 12; i++)
        {
            var light = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.035f, Height = 0.16f, RadialSegments = 8, Rings = 4 },
                MaterialOverride = LightMaterial(i % 2 == 0 ? Colors.White : Color.FromHtml("#ffc773")),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(light);
            _lights.Add(light);
        }
        _veil = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(1.8f, height * 1.65f) },
            Position = new Vector3(0, height * 0.7f, 0),
            MaterialOverride = new ShaderMaterial { Shader = new Shader { Code = @"shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;
uniform float pulse = 0.5;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
        INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float radius = length(p);
    float rim = exp(-pow((radius - 0.78) * 15.0, 2.0));
    float streak = 0.45 + 0.55 * pow(sin(atan(p.y,p.x) * 6.0 + TIME * 2.0), 2.0);
    ALBEDO = vec3(1.0, 0.78, 0.38);
    ALPHA = rim * streak * pulse * 0.55;
}" } },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_veil);
        _Process(0);
    }

    private static StandardMaterial3D LightMaterial(Color tint) => new()
    {
        AlbedoColor = tint,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
    };

    public void SetPower(int percent) => _strength = Math.Clamp(percent / 300f, 0.3f, 1f);
    public void Release() => _releaseAge = 0;

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_releaseAge >= 0)
        {
            _releaseAge += (float)delta;
            float t = Math.Clamp(_releaseAge / 0.36f, 0, 1);
            _ring.Scale = Vector3.One * (1 + t * 1.6f);
            _ring.Transparency = t;
            _veil.Visible = false;
            foreach (var light in _lights) light.Visible = false;
            if (t >= 1) QueueFree();
            return;
        }
        float entrance = Math.Clamp(_age / 0.3f, 0, 1);
        float pulse = 0.65f + Mathf.Sin(_age * 7) * 0.25f;
        _ring.Scale = Vector3.One * (0.9f + pulse * 0.12f);
        _ring.Transparency = 1 - entrance * pulse;
        ((ShaderMaterial)_veil.MaterialOverride).SetShaderParameter("pulse", entrance * pulse * (0.7f + _strength * 0.3f));
        for (int i = 0; i < _lights.Count; i++)
        {
            float phase = (_age * 0.75f + i / 12f) % 1;
            float angle = i * Mathf.Tau / 12 + _age * 0.5f;
            float radius = 0.95f - phase * 0.55f;
            _lights[i].Position = new Vector3(Mathf.Cos(angle) * radius,
                0.12f + phase * _height * 1.15f, Mathf.Sin(angle) * radius);
            _lights[i].Transparency = 1 - Mathf.Sin(phase * Mathf.Pi) * entrance;
        }
    }
}
