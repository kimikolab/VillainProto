using Godot;
using System;

// 剣閃の面とスマッシュの星形。表示用メッシュのみで対象やダメージは計算しない。
internal static partial class MovementFx
{
    internal static void SweepSheet(Node3D parent, Camera3D camera, Vector3 origin,
        Func<float, Vector3> edge, Color color, float width, double seconds)
    {
        var mesh = new ImmediateMesh();
        var node = MakeImpactMesh(parent, mesh, "YomiSweepSheet");
        void Draw(float t)
        {
            float head = Mathf.Min(1, t / 0.20f);
            float fade = 1 - Mathf.SmoothStep(0.40f, 1, t);
            mesh.ClearSurfaces();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            void V(Vector3 p, Color c) { mesh.SurfaceSetColor(c); mesh.SurfaceAddVertex(node.ToLocal(p)); }
            for (int i = 0; i < 48; i++)
            {
                float u = i / 48f * head, v = (i + 1) / 48f * head;
                Vector3 a = edge(u), b = edge(v);
                Vector3 ia = a + (origin - a).Normalized() * width * Mathf.Sin(u * Mathf.Pi);
                Vector3 ib = b + (origin - b).Normalized() * width * Mathf.Sin(v * Mathf.Pi);
                Color outer = new(color.Lerp(Colors.White, 0.35f), fade * 0.85f);
                Color inner = new(color, fade * 0.06f);
                V(ia, inner); V(a, outer); V(b, outer);
                V(ia, inner); V(b, outer); V(ib, inner);
            }
            mesh.SurfaceEnd();
        }
        Draw(0.01f);
        var tween = node.CreateTween();
        tween.TweenMethod(Callable.From<float>(Draw), 0.01f, 1f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(node.QueueFree));
        Ribbon(parent, camera, edge, new Color("fff6ce"), 0.11f, seconds * 0.8, revealFraction: 0.12f);
    }

    internal static void Smash(Node3D parent, Camera3D camera, Vector3 center, Color color, double seconds, float size)
    {
        var mesh = new ImmediateMesh();
        var node = MakeImpactMesh(parent, mesh, "HaneSmash");
        Vector3 right = camera.GlobalBasis.X, up = camera.GlobalBasis.Y, toward = camera.GlobalBasis.Z * 0.15f;
        void Draw(float t)
        {
            float radius = size * (0.20f + Mathf.Sin(Mathf.Min(1, t * 2.2f) * Mathf.Pi * 0.5f));
            float fade = 1 - Mathf.SmoothStep(0.18f, 1, t);
            mesh.ClearSurfaces();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            void V(Vector3 p, Color c) { mesh.SurfaceSetColor(c); mesh.SurfaceAddVertex(node.ToLocal(p)); }
            for (int i = 0; i < 24; i++)
            {
                Vector3 Point(int k)
                {
                    float angle = k * Mathf.Tau / 24;
                    float r = k % 2 == 0 ? 1f + 0.18f * Mathf.Sin(k * 7) : 0.18f;
                    return center + toward + (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius * r;
                }
                V(center + toward, new Color(Colors.White, fade));
                V(Point(i), new Color(color, fade * 0.55f));
                V(Point(i + 1), new Color(color, fade * 0.55f));
            }
            mesh.SurfaceEnd();
        }
        Draw(0.01f);
        var tween = node.CreateTween();
        tween.TweenMethod(Callable.From<float>(Draw), 0.01f, 1f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(node.QueueFree));
        Ribbon(parent, camera, t => center + toward + (right * Mathf.Cos(t * Mathf.Tau) + up * Mathf.Sin(t * Mathf.Tau)) * size,
            color, 0.024f * size, seconds, revealFraction: 0.12f);
    }

    private static MeshInstance3D MakeImpactMesh(Node3D parent, ImmediateMesh mesh, string name)
    {
        var node = new MeshInstance3D { Name = name, Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 15,
            MaterialOverride = new StandardMaterial3D {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
            } };
        parent.AddChild(node);
        return node;
    }
}
