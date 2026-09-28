using Godot;
using System;
using System.Linq;

internal static partial class MovementFx
{
    internal static void CarryVortex(Node3D parent, Camera3D camera, BattlePawn3D pawn, double seconds)
    {
        WindMesh(parent, "WindCarriedTarget", seconds, (node, mesh, t) => {
            if (!GodotObject.IsInstanceValid(pawn) || !pawn.IsInsideTree() || !pawn.WindCarried) return;
            Vector3 center = pawn.WindCarryCenter;
            float fade = Mathf.Sin(t * Mathf.Pi);
            for (int i = 0; i < 2; i++)
            {
                float phase = t * Mathf.Tau * 2 + i * Mathf.Pi;
                Vector3 Helix(float u) => center + new Vector3(
                    Mathf.Cos(u * Mathf.Tau * 1.6f + phase) * 0.85f,
                    (u - 0.5f) * 2.0f,
                    Mathf.Sin(u * Mathf.Tau * 1.6f + phase) * 0.65f);
                WindStrip(node, mesh, camera, Helix, new Color(1, 0.92f, 0.65f, fade * 0.65f), 0.10f, 56);
                WindStrip(node, mesh, camera, u => Helix(u) + Vector3.Up * 0.06f,
                    new Color(1, 1, 0.91f, fade), 0.026f, 56);
            }
            Vector3 ground = new(pawn.GlobalPosition.X, pawn.Home.Y + 0.08f, pawn.GlobalPosition.Z);
            WindStrip(node, mesh, camera, u => ground + new Vector3(Mathf.Cos(u * Mathf.Tau), 0, Mathf.Sin(u * Mathf.Tau)) * 0.75f,
                new Color(1, 0.91f, 0.48f, fade * 0.85f), 0.09f);
        });
    }

    internal static void TailwindLane(Node3D parent, Camera3D camera, BattlePawn3D pawn, Vector3 destination, double seconds)
    {
        Vector3 start = pawn.Home;
        Vector3 forward = (destination - start).Normalized();
        if (forward.LengthSquared() < 0.01f) return;
        Vector3 side = forward.Cross(Vector3.Up).Normalized();
        WindMesh(parent, "TailwindDirection", seconds, (node, mesh, t) => {
            if (!GodotObject.IsInstanceValid(pawn) || !pawn.IsInsideTree() || pawn.Hp <= 0) return;
            float fade = Mathf.SmoothStep(0, 0.1f, t) * (1 - Mathf.SmoothStep(0.65f, 1, t));
            Color mint = new(0.55f, 1, 0.80f, fade * 0.95f);
            // 足元の大きな山形が移動先へ流れる。陣営の左右に依存しない。
            for (int i = 0; i < 4; i++)
            {
                float distance = Mathf.PosMod(i * 0.25f + t * 0.75f, 1);
                Vector3 tip = start.Lerp(destination, distance) + Vector3.Up * 0.10f;
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 tail = tip - forward * 0.55f + side * sign * 0.50f;
                    WindStrip(node, mesh, camera, u => tail.Lerp(tip, u), mint, 0.12f, 8);
                }
            }
            Vector3 ground = new(pawn.GlobalPosition.X, pawn.Home.Y + 0.12f, pawn.GlobalPosition.Z);
            WindStrip(node, mesh, camera, u => ground + new Vector3(Mathf.Cos(u * Mathf.Tau), 0, Mathf.Sin(u * Mathf.Tau)) * 0.88f,
                mint, 0.11f);
            for (int i = 0; i < 3; i++)
            {
                float height = 0.42f + i * 0.43f;
                Vector3 point = pawn.GlobalPosition + Vector3.Up * height;
                WindStrip(node, mesh, camera, u => point - forward * (1 - u) * 1.55f
                    + side * Mathf.Sin(u * Mathf.Pi) * (i % 2 == 0 ? 0.6f : -0.6f),
                    new Color(0.85f, 1, 0.88f, fade * 0.75f), 0.075f);
            }
        });
    }

    internal static void WindSlam(Node3D parent, Camera3D camera, Vector3 from, Vector3[] targets, double seconds)
    {
        if (targets.Length == 0) return;
        Vector3 center = targets.Aggregate(Vector3.Zero, (sum, p) => sum + p) / targets.Length;
        Vector3 forward = center - from;
        forward.Y = 0;
        if (forward.LengthSquared() < 0.01f) forward = Vector3.Right;
        forward = forward.Normalized();
        Vector3 side = forward.Cross(Vector3.Up).Normalized();
        float reach = Math.Max(1, targets.Max(p => (p - from).Dot(forward))) + 0.5f;
        float halfWidth = Math.Max(1.35f, targets.Max(p => Math.Abs((p - center).Dot(side))) + 0.9f);
        WindMesh(parent, "BasaSweepWave", seconds, (node, mesh, t) => {
            float travel = Mathf.SmoothStep(0, 0.60f, t);
            float fade = Mathf.SmoothStep(0, 0.08f, t) * (1 - Mathf.SmoothStep(0.60f, 1, t));
            Vector3 front = from + forward * reach * travel;
            float width = Mathf.Lerp(0.35f, halfWidth, travel);
            // 一枚の幅広い風圧面を羽ばたきから放つ。複数の被弾者を同じ波で覆う。
            for (int band = 0; band < 4; band++)
            {
                float y = (band - 1.5f) * 0.38f;
                WindStrip(node, mesh, camera, u => front + side * ((u * 2 - 1) * width)
                    - forward * (Mathf.Pow(u * 2 - 1, 2) * 0.8f + band * 0.13f)
                    + Vector3.Up * (y + Mathf.Sin(u * Mathf.Pi) * 0.24f),
                    new Color(0.79f, 0.96f, 0.93f, fade * 0.62f), 0.15f, 48);
            }
            for (int i = 0; i < 9; i++)
            {
                float lane = (i - 4) / 4f;
                Vector3 end = front + side * lane * width + Vector3.Up * Mathf.Sin(i * 2.4f) * 0.5f;
                Vector3 tail = from.Lerp(end, Mathf.Max(0, travel - 0.48f));
                WindStrip(node, mesh, camera, u => tail.Lerp(end, u),
                    new Color(0.91f, 1, 0.96f, fade * 0.65f), i % 2 == 0 ? 0.05f : 0.022f, 16);
            }
        });
    }

    internal static void WindBurst(Node3D parent, Camera3D camera, Vector3 center, double seconds)
    {
        Vector3 right = camera.GlobalBasis.X;
        Vector3 up = camera.GlobalBasis.Y;
        WindMesh(parent, "BasaWindImpact", seconds, (node, mesh, t) => {
            float fade = 1 - t;
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.Tau / 7;
                Vector3 ray = right * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                WindStrip(node, mesh, camera, u => center + ray * (0.18f + t * 0.9f + u * 0.65f)
                    + up * Mathf.Sin(u * Mathf.Pi) * 0.10f,
                    new Color(0.87f, 1, 0.92f, fade * 0.8f), 0.045f, 12);
            }
        });
    }
}
