using Godot;
using System;

// 蔓・風・矢は短命なリボン。1本につき1メッシュで、再生し直しでは親ごと消える。
internal static partial class MovementFx
{
    internal static readonly Color Leaf = new("91bb58");
    internal static readonly Color Wind = new("b8b99b");
    internal static readonly Color Arrow = new("ddc597");
    internal static readonly Color Bounce = UiKit.Player;
    internal static readonly Color Creak = UiKit.Gold;

    internal static void Ribbon(Node3D parent, Camera3D camera, Func<float, Vector3> path,
        Color color, float width, double seconds, bool travelling = false, float revealFraction = 2f / 3f)
    {
        var mesh = new ImmediateMesh();
        var material = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
        };
        var node = new MeshInstance3D { Name = "MovementRibbon", Mesh = mesh, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 12 };
        parent.AddChild(node);
        void Draw(float t)
        {
            mesh.ClearSurfaces();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            float head = Mathf.Min(1, t / Mathf.Max(0.01f, revealFraction));
            float tail = travelling ? Mathf.Max(0, head - 0.24f) : 0;
            Color tint = new(color, (1 - Mathf.SmoothStep(0.55f, 1, t)) * 0.9f);
            void V(Vector3 p) { mesh.SurfaceSetColor(tint); mesh.SurfaceAddVertex(node.ToLocal(p)); }
            for (int i = 0; i < 32; i++)
            {
                float u = i / 32f, v = (i + 1) / 32f;
                Vector3 a = path(Mathf.Lerp(tail, head, u)), b = path(Mathf.Lerp(tail, head, v));
                Vector3 normal = (b - a).Cross(camera.GlobalPosition - a).Normalized();
                Vector3 wa = normal * width * (0.15f + Mathf.Sin(u * Mathf.Pi));
                Vector3 wb = normal * width * (0.15f + Mathf.Sin(v * Mathf.Pi));
                V(a - wa); V(b - wb); V(a + wa);
                V(a + wa); V(b - wb); V(b + wb);
            }
            mesh.SurfaceEnd();
        }
        Draw(0.01f);
        var tween = node.CreateTween();
        tween.TweenMethod(Callable.From<float>(Draw), 0.01f, 1f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(node.QueueFree));
    }

    internal static void Flow(Node3D parent, Camera3D camera, Vector3 from, Vector3 to,
        Color tint, double seconds, float curl = 0.25f, int strands = 3)
    {
        for (int i = 0; i < strands; i++)
        {
            float phase = i * Mathf.Tau / strands;
            Ribbon(parent, camera, t => from.Lerp(to, t) +
                new Vector3(0, Mathf.Sin(t * Mathf.Tau + phase), Mathf.Cos(t * Mathf.Tau + phase))
                    * Mathf.Sin(t * Mathf.Pi) * curl,
                tint, 0.026f, seconds, true);
        }
    }

    internal static void Coil(Node3D parent, Camera3D camera, Vector3 center,
        Color tint, float radius, double seconds, int turns = 2)
        => Coil(parent, camera, () => center, tint, radius, seconds, turns);

    internal static void Coil(Node3D parent, Camera3D camera, Func<Vector3> center,
        Color tint, float radius, double seconds, int turns = 2)
    {
        Ribbon(parent, camera, t => center() + new Vector3(Mathf.Cos(t * Mathf.Tau * turns) * radius,
            (t - 0.5f) * 1.25f, Mathf.Sin(t * Mathf.Tau * turns) * radius), tint, 0.038f, seconds);
    }

    internal static void Leaves(Node3D parent, BattlePawn3D pawn, double seconds)
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        mesh.SurfaceAddVertex(new Vector3(0, 0.16f, 0));
        mesh.SurfaceAddVertex(new Vector3(-0.055f, 0, 0));
        mesh.SurfaceAddVertex(new Vector3(0, -0.12f, 0));
        mesh.SurfaceAddVertex(new Vector3(0, 0.16f, 0));
        mesh.SurfaceAddVertex(new Vector3(0, -0.12f, 0));
        mesh.SurfaceAddVertex(new Vector3(0.065f, 0, 0));
        mesh.SurfaceEnd();
        var material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = Leaf, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
        for (int i = 0; i < 5; i++)
        {
            float phase = i * 2.4f;
            var leaf = new MeshInstance3D { Name = "ShioLeaf", Mesh = mesh, MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            parent.AddChild(leaf);
            Vector3 center = pawn.FxPoint;
            var tween = leaf.CreateTween();
            tween.TweenMethod(Callable.From<float>(t => {
                if (GodotObject.IsInstanceValid(pawn)) center = pawn.FxPoint;
                leaf.Position = parent.ToLocal(center + new Vector3(Mathf.Cos(phase + t * 3) * 0.50f,
                    -0.6f + t * 1.25f, Mathf.Sin(phase + t * 3) * 0.36f));
                leaf.Rotation = new Vector3(0, phase + t * 5, t * 4);
                leaf.Scale = Vector3.One * Mathf.Sin(t * Mathf.Pi);
            }), 0.01f, 1f, Math.Max(0.015, seconds));
            tween.TweenCallback(Callable.From(leaf.QueueFree));
        }
    }

    internal static void Shot(Node3D parent, Camera3D camera, Vector3 from, Vector3 to,
        Color tint, double seconds)
    {
        Vector3 direction = (to - from).Normalized();
        Vector3 side = direction.Cross(camera.GlobalPosition - from).Normalized();
        var arrow = new Node3D { Name = "MovementArrow", Position = parent.ToLocal(from) };
        parent.AddChild(arrow);
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        void V(Vector3 p) { mesh.SurfaceAddVertex(p); }
        V(direction * 0.30f); V(-direction * 0.04f + side * 0.10f); V(-direction * 0.04f - side * 0.10f);
        V(-direction * 0.55f - side * 0.025f); V(side * 0.025f); V(-side * 0.025f);
        V(-direction * 0.55f - side * 0.025f); V(-direction * 0.55f + side * 0.025f); V(side * 0.025f);
        mesh.SurfaceEnd();
        arrow.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = tint,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Ribbon(parent, camera, t => from.Lerp(to, t), new Color(tint, 0.5f), 0.022f, seconds * 1.3, true);
        var tween = arrow.CreateTween();
        tween.TweenProperty(arrow, "position", parent.ToLocal(to), Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(arrow.QueueFree));
    }
}
