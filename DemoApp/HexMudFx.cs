using Godot;
using System;
using System.Collections.Generic;

// 呪い専用の表示部品。台本・戦闘判定には依存せず、座標と表示速度だけを受け取る。
// しみは駒の子として保持し、状態解除・死亡時の破棄は接続側が行う。
public static class HexMudFx
{
    public static readonly Color Tint = new("#bd9aed");
    private static Shader? _splat;

    public static MeshInstance3D Stain(Node3D parent, Vector3 localPosition)
        => Splat(parent, localPosition, 1.05f, 0.27f);

    public static void Splash(Node3D parent, Vector3 localPosition, double speed = 1)
    {
        var splash = Splat(parent, localPosition, 1.4f, 0.82f);
        splash.Scale = new Vector3(0.35f, 0.6f, 1);
        var tween = splash.CreateTween();
        tween.TweenProperty(splash, "scale", new Vector3(1.15f, 0.88f, 1), Seconds(0.09, speed))
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(splash, "transparency", 1f, Seconds(0.25, speed));
        tween.TweenCallback(Callable.From(splash.QueueFree));
    }

    // 1本ごとに待たず、全宛先へ同じ時刻に飛ばす。着弾通知もこの表示の時刻だけを返す。
    // 座標は parent のローカル座標。飛行中の筋は短い尾だけにして連撃でも盤面を覆わない。
    public static Node3D Transfer(Node3D parent, Vector3 from, IReadOnlyList<Vector3> targets,
        double speed = 1, bool application = false, Action? arrived = null)
    {
        var group = new Node3D { Name = "HexMudTransfer" };
        parent.AddChild(group);
        var trails = new List<MeshInstance3D[]>();
        foreach (var target in targets)
        {
            var beads = new MeshInstance3D[7];
            for (int i = 0; i < beads.Length; i++)
            {
                float radius = (application ? 0.12f : 0.075f) * (1f - i * 0.095f);
                beads[i] = new MeshInstance3D
                {
                    Mesh = new SphereMesh { Radius = radius, Height = radius * 2,
                        RadialSegments = 8, Rings = 4 },
                    MaterialOverride = new StandardMaterial3D
                    {
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                        AlbedoColor = new Color(Tint, 0.85f - i * 0.085f),
                    },
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                    Position = from,
                };
                group.AddChild(beads[i]);
            }
            trails.Add(beads);
        }
        var tween = group.CreateTween();
        tween.TweenMethod(Callable.From<float>(p =>
        {
            for (int n = 0; n < trails.Count; n++)
                for (int i = 0; i < trails[n].Length; i++)
                {
                    float t = Mathf.Clamp(p - i * 0.025f, 0, 1);
                    trails[n][i].Position = from.Lerp(targets[n], t)
                        + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * (application ? 0.7f : 0.20f);
                    trails[n][i].Visible = p > i * 0.025f;
                }
        }), 0f, 1f, Seconds(application ? 0.30 : 0.24, speed));
        tween.TweenCallback(Callable.From(() =>
        {
            foreach (var target in targets) Splash(parent, target, speed);
            arrived?.Invoke();
            group.QueueFree();
        }));
        return group;
    }

    private static double Seconds(double value, double speed) => value / Math.Max(0.1, speed);

    private static MeshInstance3D Splat(Node3D parent, Vector3 position, float size, float opacity)
    {
        _splat ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;
uniform vec4 tint : source_color;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
float drop(vec2 p, vec2 center, float radius) {
    return 1.0 - smoothstep(radius * 0.78, radius, length(p - center));
}
void fragment() {
    vec2 p = UV - vec2(0.5);
    float a = atan(p.y, p.x);
    float edge = 0.23 + 0.045 * sin(a * 5.0 + 1.2) + 0.026 * sin(a * 9.0);
    float body = 1.0 - smoothstep(edge - 0.025, edge, length(p * vec2(0.95, 1.2)));
    body = max(body, drop(p, vec2(-0.32, 0.20), 0.055));
    body = max(body, drop(p, vec2(0.30, 0.25), 0.038));
    body = max(body, drop(p, vec2(0.20, -0.31), 0.06));
    body = max(body, drop(p, vec2(-0.29, -0.20), 0.03));
    float grain = 0.85 + 0.15 * sin(p.x * 83.0) * sin(p.y * 71.0);
    ALBEDO = mix(tint.rgb * 0.52, tint.rgb, smoothstep(0.02, 0.34, length(p)));
    ALPHA = body * tint.a * grain;
}" };
        var material = new ShaderMaterial { Shader = _splat };
        material.SetShaderParameter("tint", new Color(Tint, opacity));
        var mesh = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = Vector2.One * size },
            Position = position,
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            ExtraCullMargin = 1,
        };
        parent.AddChild(mesh);
        return mesh;
    }
}
