using Godot;
using System;
using System.Collections.Generic;

// 駒の周囲で星が順に開いて消える。飛来・着弾・中心への収束は描かない。
public partial class HealingDrops3D : Node3D
{
    private readonly List<(MeshInstance3D Mesh, Vector3 Home, float Delay)> _stars = new();
    private float _age;
    private int _count;

    public void Configure(float height, int amount)
    {
        _count = Math.Clamp(5 + amount / 15, 5, 8);
        var shader = new Shader { Code = @"shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;
uniform float brightness = 0.0;
uniform float opening = 0.5;
uniform vec4 tint : source_color;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
        INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    vec2 p = abs((UV * 2.0 - 1.0) / max(opening, 0.05));
    // 四方へ伸びる尖った星。外縁は淡青、芯は白。丸い粒の輪郭を残さない。
    float shape = pow(p.x, 0.55) + pow(p.y, 0.55);
    float star = 1.0 - smoothstep(0.85, 1.0, shape);
    float halo = exp(-dot(p,p) * 24.0) * 0.25;
    float ray = exp(-p.x * p.x * 1600.0) * max(0.0, 1.0 - p.y) * 0.35;
    float core = exp(-dot(p,p) * 65.0);
    ALBEDO = mix(tint.rgb, vec3(1.0), core);
    ALPHA = clamp(star + halo + ray, 0.0, 1.0) * brightness;
}" };
        Vector2[] locations =
        [
            new(-0.70f, 0.60f), new(0.62f, 1.28f), new(-0.46f, 1.66f),
            new(0.78f, 0.38f), new(0.35f, 0.82f), new(-0.82f, 1.12f),
            new(0.12f, 1.75f), new(-0.18f, 0.22f),
        ];
        for (int i = 0; i < locations.Length; i++)
        {
            var material = new ShaderMaterial { Shader = shader };
            material.SetShaderParameter("tint", Color.FromHtml(i % 3 == 0 ? "#fff0b8" : "#a9dcff"));
            float size = i % 3 == 0 ? 0.83f : 0.64f;
            var star = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = new Vector2(size, size * 1.25f) },
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(star);
            var home = new Vector3(locations[i].X, locations[i].Y * height / 1.35f, 0.65f);
            _stars.Add((star, home, i * 0.045f));
        }
        _Process(0);
    }

    public void Refresh(int amount)
    {
        _count = Math.Max(_count, Math.Clamp(5 + amount / 15, 5, 8));
        if (_age > 0.65f) _age = 0;
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= 1.0f) { QueueFree(); return; }
        for (int i = 0; i < _stars.Count; i++)
        {
            var (star, home, delay) = _stars[i];
            float t = (_age - delay) / 0.62f;
            star.Visible = i < _count && t >= 0 && t <= 1;
            if (!star.Visible) continue;
            float pulse = Mathf.Sin(t * Mathf.Pi);
            star.Position = home + Vector3.Up * (t * 0.12f);
            var material = (ShaderMaterial)star.MaterialOverride;
            material.SetShaderParameter("brightness", Math.Min(1f, pulse * 1.5f));
            material.SetShaderParameter("opening", 0.20f + pulse * 0.80f);
        }
    }
}
