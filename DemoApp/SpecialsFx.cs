using Godot;
using System;

// 花、炎の花びら、痺れの筋。既存の泥や水の渦とは形を共用しない。
public static class SpecialsFx
{
    private const string Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;
uniform int kind = 0;
uniform vec4 tint : source_color;
uniform float progress = 0.0;
uniform float strength = 1.0;
uniform bool ready = false;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * length(MODEL_MATRIX[0].xyz), INV_VIEW_MATRIX[1] * length(MODEL_MATRIX[1].xyz), INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    vec2 p = UV - vec2(0.5);
    float d = length(p);
    float a = atan(p.y, p.x);
    float alpha = 0.0;
    vec3 color = tint.rgb;
    if (kind == 0) {
        float edge = 0.24 + 0.10 * cos(a * 6.0);
        float petals = 1.0 - smoothstep(edge-0.03, edge, d);
        float core = exp(-d*d*140.0);
        float pulse = 0.65 + 0.35 * sin(TIME * (ready ? 10.0 : 4.0));
        color = mix(vec3(0.12,0.025,0.16), tint.rgb * (1.0 + strength * pulse * 2.0), core);
        alpha = max(petals, core * strength);
    } else if (kind == 1) {
        float spin = progress * 5.0;
        p = mat2(vec2(cos(spin),-sin(spin)),vec2(sin(spin),cos(spin))) * p;
        p.x += sin(p.y * 7.0 + progress * 9.0) * 0.055;
        float width = max(0.0, 0.17 * (1.0 - pow(abs(p.y)/0.38, 1.4)));
        float leaf = (1.0-smoothstep(width-0.015,width+0.015,abs(p.x))) * (1.0-smoothstep(0.33,0.39,abs(p.y)));
        float vein = exp(-abs(p.x)*60.0) * leaf;
        float mist = exp(-d*d*17.0) * 0.22;
        color = mix(vec3(0.25,0.025,0.40), mix(tint.rgb,vec3(1.0,0.52,0.12),vein*0.7), leaf);
        alpha = max(leaf,mist) * sin(progress * 3.14159);
    } else if (kind == 3) {
        float blob = 1.0-smoothstep(0.13,0.22,length(vec2(p.x*1.3,p.y)));
        float splash = (1.0-smoothstep(0.02,0.055,abs(d-0.25))) * pow(max(0.0,cos(a*5.0+progress*8.0)),5.0);
        color = mix(tint.rgb * 0.35, tint.rgb, smoothstep(-0.2,0.2,p.y));
        alpha = max(blob,splash*0.6) * sin(progress*3.14159);
    } else {
        float thread = abs(p.x + sin(p.y*28.0 + progress*24.0)*0.05);
        alpha = (1.0-smoothstep(0.02,0.07,thread)) * (1.0-smoothstep(0.28,0.48,abs(p.y))) * sin(progress*3.14159);
    }
    ALBEDO = color;
    EMISSION = color * (kind == 0 ? 0.3 : 0.6);
    ALPHA = alpha;
}";

    public static MeshInstance3D Card(Node3D parent, Vector3 position, Vector2 size, int kind, Color tint)
    {
        var material = new ShaderMaterial { Shader = new Shader { Code = Code } };
        material.SetShaderParameter("kind", kind);
        material.SetShaderParameter("tint", tint);
        var card = new MeshInstance3D { Mesh = new QuadMesh { Size = size }, Position = position,
            MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        parent.AddChild(card);
        return card;
    }

    public static void Travel(Node3D parent, Vector3 from, Vector3 to, Color tint, double seconds,
        int kind = 1, float arc = 0.5f, float size = 0.7f, double delay = 0)
    {
        from = parent.ToLocal(from); to = parent.ToLocal(to);
        var card = Card(parent, from, Vector2.One * size, kind, tint);
        var material = (ShaderMaterial)card.MaterialOverride;
        var tween = card.CreateTween();
        if (delay > 0) tween.TweenInterval(delay);
        tween.TweenMethod(Callable.From<float>(p => {
            card.Position = from.Lerp(to, p) + Vector3.Up * Mathf.Sin(p * Mathf.Pi) * arc;
            material.SetShaderParameter("progress", p);
        }), 0f, 1f, Math.Max(0.01, seconds));
        tween.TweenCallback(Callable.From(card.QueueFree));
    }
}
