using Godot;

public partial class BattlefieldView3D
{
    private static Shader? _punchImpactShader;

    // 打点を中心に白い芯・太い衝撃波・短い放射光を一瞬だけ広げる。
    private void MakePunchImpact(BattlePawn3D target, double speed)
    {
        _punchImpactShader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;
uniform float progress = 0.0;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    vec2 p = UV - vec2(0.5);
    float r = length(p);
    float a = atan(p.y, p.x);
    float radius = mix(0.08, 0.42, progress);
    float ring = 1.0 - smoothstep(0.012, 0.045, abs(r - radius));
    float core = (1.0 - smoothstep(0.02, 0.16, r)) * (1.0 - progress);
    float rays = pow(max(0.0, cos(a * 9.0 + 0.6)), 12.0);
    rays *= smoothstep(0.07, 0.13, r) * (1.0 - smoothstep(0.18, 0.43, r));
    float fade = 1.0 - smoothstep(0.15, 1.0, progress);
    ALBEDO = mix(vec3(1.0, 0.66, 0.28), vec3(1.0, 0.97, 0.88), core);
    ALPHA = clamp(core + ring * 0.7 + rays, 0.0, 1.0) * fade;
}" };
        var material = new ShaderMaterial { Shader = _punchImpactShader };
        var impact = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2.2f, 2.2f) },
            Position = target.FxPoint,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 2,
        };
        _fxRoot.AddChild(impact);
        var tween = impact.CreateTween();
        tween.TweenMethod(Callable.From<float>(p => material.SetShaderParameter("progress", p)),
            0f, 1f, 0.22 / System.Math.Max(0.1, speed));
        tween.TweenCallback(Callable.From(impact.QueueFree));
    }
}
