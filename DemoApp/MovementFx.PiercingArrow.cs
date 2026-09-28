using Godot;
using System;

internal static partial class MovementFx
{
    internal static void PiercingShot(Node3D parent, Camera3D camera, Vector3 from, Vector3 to,
        Vector3[] impacts, double flightSeconds, bool barrage = false)
    {
        Vector3 direction = (to - from).Normalized();
        if (direction.LengthSquared() < 0.01f) return;
        float distance = from.DistanceTo(to);
        Vector3 side = direction.Cross(camera.GlobalPosition - from).Normalized();
        var mesh = new ImmediateMesh();
        float lifetime = barrage ? 1.85f : 3.25f;
        float width = barrage ? 0.40f : 1;
        float light = barrage ? 0.48f : 1;
        float arrowSize = barrage ? 0.65f : 1;
        var node = new MeshInstance3D { Name = barrage ? "SeroBarrageArrow" : "SeroPiercingArrow", Mesh = mesh, ExtraCullMargin = 16,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
            } };
        parent.AddChild(node, true);
        void Draw(float t)
        {
            mesh.ClearSurfaces();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            float elapsed = t * lifetime;
            float head = Mathf.Min(1, elapsed);
            float tail = Mathf.Clamp((elapsed - 1.0f) * (barrage ? 1 / (lifetime - 1) : 0.42f), 0, head);
            float fade = (1 - Mathf.SmoothStep(barrage ? 1 / lifetime : 0.32f, 1, t)) * light;
            Vector3 start = from.Lerp(to, tail), tip = from.Lerp(to, head);
            // 広い淡い光、金の縁、白い芯。先端が進んだ部分にだけ軌跡を残す。
            WindStrip(node, mesh, camera, u => start.Lerp(tip, u), new Color(1, 0.72f, 0.28f, fade * 0.12f), 0.25f * width, 40);
            WindStrip(node, mesh, camera, u => start.Lerp(tip, u), new Color(1, 0.86f, 0.48f, fade * 0.42f), 0.087f * width, 40);
            WindStrip(node, mesh, camera, u => start.Lerp(tip, u), new Color(1, 1, 0.93f, fade), 0.027f * width, 40);
            void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
            {
                mesh.SurfaceSetColor(color);
                mesh.SurfaceAddVertex(node.ToLocal(tip + (a - tip) * arrowSize));
                mesh.SurfaceAddVertex(node.ToLocal(tip + (b - tip) * arrowSize));
                mesh.SurfaceAddVertex(node.ToLocal(tip + (c - tip) * arrowSize));
            }
            if (elapsed <= 1.08f)
            {
                // 矢じり・軸・矢羽を独立した形にし、長い光でも矢だと読める先端を残す。
                Triangle(tip + direction * 0.62f, tip - direction * 0.08f + side * 0.20f,
                    tip - direction * 0.08f - side * 0.20f, new Color(1, 0.86f, 0.46f));
                Triangle(tip + direction * 0.50f, tip + side * 0.075f, tip - side * 0.075f, Colors.White);
                WindStrip(node, mesh, camera, u => tip - direction * (0.95f * u * arrowSize), Colors.White, 0.035f * arrowSize, 12);
                for (int sign = -1; sign <= 1; sign += 2)
                    Triangle(tip - direction * 0.65f, tip - direction * 1.05f + side * sign * 0.15f,
                        tip - direction * 0.96f, new Color(1, 0.94f, 0.65f));
            }
            void Spark(Vector3 center, float age, float size)
            {
                if (age < 0 || age > 0.55f) return;
                float alpha = 1 - age / 0.55f;
                for (int i = 0; i < 4; i++)
                {
                    float angle = i * Mathf.Pi * 0.5f;
                    Vector3 ray = camera.GlobalBasis.X * Mathf.Cos(angle) + camera.GlobalBasis.Y * Mathf.Sin(angle);
                    WindStrip(node, mesh, camera, u => center + ray * (0.04f + u * size * (0.4f + age)),
                        new Color(1, 0.95f, 0.72f, alpha), i % 2 == 0 ? 0.075f : 0.035f, 10);
                }
            }
            if (!barrage) Spark(from, elapsed, 0.9f);
            foreach (Vector3 impact in impacts)
            {
                float arrival = Mathf.Clamp((impact - from).Dot(direction) / distance, 0, 1);
                Spark(impact, elapsed - arrival, 0.85f);
            }
            // 軌跡から小さな光の粒がほどける。直線が消える間も貫いた勢いを残す。
            for (int i = 1; !barrage && i <= 12; i++)
            {
                float at = i / 13f;
                if (at > head || at < tail) continue;
                float age = Mathf.Max(0, elapsed - at);
                Vector3 p = from.Lerp(to, at) + side * Mathf.Sin(i * 2.4f) * age * 0.14f;
                Vector3 ray = side * 0.06f + direction * 0.11f;
                WindStrip(node, mesh, camera, u => p + ray * u,
                    new Color(1, 0.88f, 0.46f, fade * 0.70f), 0.028f, 2);
            }
            mesh.SurfaceEnd();
        }
        Draw(0.001f);
        var tween = node.CreateTween();
        tween.TweenMethod(Callable.From<float>(Draw), 0.001f, 1f, Math.Max(0.015, flightSeconds * lifetime));
        tween.TweenCallback(Callable.From(node.QueueFree));
    }
}
