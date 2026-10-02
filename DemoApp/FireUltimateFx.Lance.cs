using Godot;
using System;

internal static partial class FireUltimateFx
{
    internal static void Inferno(Node3D root, Vector3 ground, float width, float height, double seconds, Camera3D camera)
    {
        var material = Energy(2, new("ff9524"));
        var node = FireFx.Card(root, root.ToLocal(ground + Vector3.Up * height * 0.5f), new(width, height), material);
        Vector3 across = new Vector3(camera.GlobalBasis.X.X, 0, camera.GlobalBasis.X.Z).Normalized();
        node.Basis = new Basis(across, Vector3.Up, across.Cross(Vector3.Up));
        var tween = node.CreateTween();
        tween.TweenMethod(Callable.From<float>(p => {
            float rise = Mathf.Min(1, p * 9);
            node.Scale = new(0.75f + Mathf.Min(1, p * 7) * 0.25f, Math.Max(0.001f, rise), 1);
            node.Position = root.ToLocal(ground + Vector3.Up * height * rise * 0.5f);
            material.SetShaderParameter("progress", p);
            material.SetShaderParameter("clock", p * 3);
        }), 0f, 1f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(node.QueueFree));
    }

    internal static void Lance(Node3D root, Vector3 from, Vector3 to, Camera3D camera, bool grand, double seconds)
    {
        Vector3 direction = (to - from).Normalized();
        float distance = from.DistanceTo(to);
        if (distance < 0.01f) return;
        var node = Sword(root);
        var material = (ShaderMaterial)node.MaterialOverride;
        material.SetShaderParameter("tint", new Color(grand ? "ffd044" : "ff6320"));
        var tween = node.CreateTween();
        tween.TweenMethod(Callable.From<float>(p => {
            float travel = Mathf.Min(1, p / (grand ? 0.28f : 0.65f));
            Vector3 tip = from.Lerp(to, travel);
            Vector3 tail = grand ? from : tip - direction * Math.Min(3.1f, distance * travel + 0.03f);
            SwordPose(node, tail, tip + direction * 0.03f, camera.GlobalBasis.Z, grand ? 3.4f : 1.05f, p * 2);
            material.SetShaderParameter("opacity", (1 - Mathf.SmoothStep(grand ? 0.64f : 0.72f, 1, p)));
        }), 0f, 1f, seconds);
        tween.TweenCallback(Callable.From(node.QueueFree));
        Vector3 across = direction.Cross(camera.GlobalBasis.Z).Normalized();
        Vector3 second = direction.Cross(across).Normalized();
        if (grand)
        {
            // 太い光芯を螺旋が締め、槍の進行方向に垂直な衝撃輪が連続する。
            var helix = Strip(root, p => root.ToLocal(from.Lerp(to, p)
                + (across * Mathf.Cos(p * Mathf.Tau * 3) + second * Mathf.Sin(p * Mathf.Tau * 3)) * 0.65f),
                _ => across, 0.09f, new("ffe7a6"));
            AnimateTrail(helix, seconds, _ => { });
            for (int i = 1; i <= 3; i++)
            {
                Vector3 center = from.Lerp(to, i / 4f);
                float radius = 0.7f + i * 0.15f;
                Vector3 Radial(float p) => across * Mathf.Cos(p * Mathf.Tau) + second * Mathf.Sin(p * Mathf.Tau);
                var ring = Strip(root, p => Radial(p) * radius, Radial, 0.085f, new("fff0af"));
                ring.Position = root.ToLocal(center);
                AnimateTrail(ring, seconds, p => ring.Scale = Vector3.One * (0.85f + p * 0.6f));
            }
        }
        else
        {
            for (int k = -1; k <= 1; k++)
                FireFx.Ribbon(root, from + across * k * 0.22f, to + across * k * 0.22f,
                    new("ff9f32"), k == 0 ? 0.15f : 0.055f, k * 0.15f, seconds, camera.GlobalBasis.Z);
        }
    }
}
