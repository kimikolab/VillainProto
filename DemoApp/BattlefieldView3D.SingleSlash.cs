using Godot;

public partial class BattlefieldView3D
{
    private static Shader? _singleSlashShader;

    private void MakeSingleSlash(BattlePawn3D from, BattlePawn3D target, Color color)
    {
        _singleSlashShader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;
uniform vec4 tint : source_color;
uniform float progress = 0.0;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    vec2 p = UV - vec2(0.5);
    float along = (p.x - p.y) * 0.7071;
    float across = (p.x + p.y) * 0.7071;
    float taper = max(0.0, 1.0 - abs(along) / 0.55);
    float width = 0.006 + 0.045 * taper * taper;
    float blade = 1.0 - smoothstep(width * 0.2, width, abs(across));
    float halo = exp(-abs(across) * 38.0) * taper * 0.28;
    float reveal = 1.0 - smoothstep(progress * 1.8 - 0.68, progress * 1.8 - 0.56, along);
    float fade = 1.0 - smoothstep(0.30, 1.0, progress);
    ALBEDO = mix(tint.rgb, vec3(1.0), blade * 0.8);
    ALPHA = (blade + halo) * taper * reveal * fade;
}" };
        var material = new ShaderMaterial { Shader = _singleSlashShader };
        material.SetShaderParameter("tint", color);
        var slash = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(2.5f, 2.5f) },
            Position = target.FxPoint,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 2,
        };
        _fxRoot.AddChild(slash);
        // 対象だけに一閃を残す。長い貫通線や複数対象の薙ぎと区別する。
        var tween = slash.CreateTween();
        tween.TweenMethod(Callable.From<float>(p => material.SetShaderParameter("progress", p)), 0f, 1f, 0.38);
        tween.TweenCallback(Callable.From(slash.QueueFree));
    }
}
