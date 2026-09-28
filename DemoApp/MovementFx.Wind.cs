using Godot;
using System;

internal static partial class MovementFx
{
    // 段0/1は1本を太く、段2/3は2本へ。進行中の段通知も同じ竜巻へ反映する。
    internal static int TornadoCount(int stage) => stage >= 2 ? 2 : 1;
    internal static float TornadoHeight(int stage) => 2.35f + Math.Clamp(stage, 0, 3) * 0.34f;

    internal static void Tornado(Node3D parent, Camera3D camera, Vector3 center,
        Func<int> stageOf, int index, double seconds)
    {
        WindMesh(parent, "BasaTornado", seconds, (node, mesh, t) => {
            int stage = stageOf();
            if (index >= TornadoCount(stage)) return;
            float envelope = Mathf.SmoothStep(0, 0.12f, t) * (1 - Mathf.SmoothStep(0.76f, 1, t));
            float height = TornadoHeight(stage) * (0.65f + envelope * 0.35f);
            float radius = 0.90f + stage * 0.16f;
            Vector3 origin = center + (stage >= 2 ? Vector3.Back * (index == 0 ? -1.22f : 1.22f) : Vector3.Zero);
            float spin = t * Mathf.Tau * 3.4f + index * 2.3f;
            for (int band = 0; band < 3; band++)
            {
                float phase = spin + band * Mathf.Tau / 3;
                Vector3 Spiral(float u)
                {
                    float angle = phase + u * Mathf.Tau * 2.35f;
                    float r = (0.16f + Mathf.Pow(u, 0.85f) * radius) * (0.7f + envelope * 0.3f);
                    return origin + new Vector3(Mathf.Cos(angle) * r, u * height, Mathf.Sin(angle) * r);
                }
                WindStrip(node, mesh, camera, Spiral, new Color(0.63f, 0.83f, 0.84f, 0.22f * envelope), 0.17f + stage * 0.025f, 72);
                WindStrip(node, mesh, camera, u => Spiral(u) + Vector3.Up * 0.07f,
                    new Color(0.9f, 1, 0.95f, 0.67f * envelope), 0.025f, 72);
            }
            // 地面を巻く輪と舞い上がる短い破片で、ただの螺旋ではなく回転する風にする。
            WindStrip(node, mesh, camera, u => origin + new Vector3(Mathf.Cos(u * Mathf.Tau + spin) * radius,
                0.03f + Mathf.Sin(u * Mathf.Tau) * 0.04f, Mathf.Sin(u * Mathf.Tau + spin) * radius),
                new Color(0.82f, 0.92f, 0.76f, 0.40f * envelope), 0.055f);
            for (int i = 0; i < 9; i++)
            {
                float y = Mathf.PosMod(i * 0.117f + t * 1.7f, 1);
                float phase = spin * 1.6f + i * 2.4f;
                float r = 0.22f + y * radius;
                Vector3 p = origin + new Vector3(Mathf.Cos(phase) * r, y * height, Mathf.Sin(phase) * r);
                WindStrip(node, mesh, camera, u => p + new Vector3(-Mathf.Sin(phase), 0.3f, Mathf.Cos(phase)) * u * 0.20f,
                    new Color(0.8f, 0.85f, 0.59f, envelope * 0.8f), 0.038f, 2);
            }
        });
    }

    internal static void Gust(Node3D parent, Camera3D camera, Vector3 from, Vector3 to, double seconds, float strength)
    {
        Vector3 forward = (to - from).Normalized();
        if (forward.LengthSquared() < 0.01f) return;
        Vector3 side = forward.Cross(Vector3.Up).Normalized();
        if (side.LengthSquared() < 0.01f) side = Vector3.Right;
        Vector3 up = side.Cross(forward).Normalized();
        WindMesh(parent, "TailwindGust", seconds, (node, mesh, t) => {
            float fade = Mathf.Sin(t * Mathf.Pi);
            float head = Mathf.Min(1.15f, t * 1.6f);
            for (int i = 0; i < 9; i++)
            {
                float phase = i * 2.4f;
                float spread = (0.28f + (i % 3) * 0.17f) * strength;
                Vector3 offset = (side * Mathf.Cos(phase) + up * Mathf.Sin(phase)) * spread;
                WindStrip(node, mesh, camera, u => from.Lerp(to, head - 0.46f + u * 0.46f) + offset
                    + up * Mathf.Sin(u * Mathf.Pi) * 0.10f,
                    new Color(0.86f, 0.98f, 0.91f, fade * (i % 3 == 0 ? 0.62f : 0.85f)),
                    (i % 3 == 0 ? 0.13f : 0.027f) * strength, 20);
            }
            // 前へ押し出す三つの湾曲した風圧面。輪郭だけ光らせて駒を隠さない。
            for (int i = 0; i < 3; i++)
            {
                float distance = head - i * 0.14f;
                Vector3 center = from.Lerp(to, distance);
                float radius = (0.45f + t * 0.40f + i * 0.06f) * strength;
                WindStrip(node, mesh, camera, u => {
                    float angle = u * Mathf.Tau * 0.78f + i * 1.6f;
                    return center + (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius
                        - forward * Mathf.Sin(u * Mathf.Pi) * 0.22f;
                }, new Color(0.74f, 0.92f, 0.88f, fade * 0.55f), 0.06f * strength);
            }
        });
    }

    private static void WindMesh(Node3D parent, string name, double seconds, Action<MeshInstance3D, ImmediateMesh, float> draw)
    {
        var mesh = new ImmediateMesh();
        var node = new MeshInstance3D { Name = name, Mesh = mesh, ExtraCullMargin = 16,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
            } };
        parent.AddChild(node, true);
        void Draw(float t)
        {
            mesh.ClearSurfaces();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            draw(node, mesh, t);
            // 非表示の2本目でも空サーフェスをGodotへ渡さない。
            mesh.SurfaceSetColor(Colors.Transparent);
            for (int i = 0; i < 3; i++) mesh.SurfaceAddVertex(Vector3.Zero);
            mesh.SurfaceEnd();
        }
        Draw(0.001f);
        var tween = node.CreateTween();
        tween.TweenMethod(Callable.From<float>(Draw), 0.001f, 1f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(node.QueueFree));
    }

    private static void WindStrip(MeshInstance3D node, ImmediateMesh mesh, Camera3D camera,
        Func<float, Vector3> path, Color tint, float width, int segments = 40)
    {
        void V(Vector3 p, float alpha)
        {
            mesh.SurfaceSetColor(new Color(tint, tint.A * alpha));
            mesh.SurfaceAddVertex(node.ToLocal(p));
        }
        for (int i = 0; i < segments; i++)
        {
            float u = i / (float)segments, v = (i + 1f) / segments;
            Vector3 a = path(u), b = path(v);
            Vector3 Normal(float at, Vector3 point) =>
                (path(Mathf.Min(1, at + 0.001f)) - path(Mathf.Max(0, at - 0.001f)))
                    .Cross(camera.GlobalPosition - point).Normalized();
            float start = Mathf.Sin(u * Mathf.Pi), end = Mathf.Sin(v * Mathf.Pi);
            Vector3 wa = Normal(u, a) * width * (0.2f + start), wb = Normal(v, b) * width * (0.2f + end);
            V(a - wa, start); V(b - wb, end); V(a + wa, start);
            V(a + wa, start); V(b - wb, end); V(b + wb, end);
        }
    }
}
