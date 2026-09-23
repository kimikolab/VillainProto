using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int ThrustPlays { get; private set; }
    internal int ThrustMarkedHits { get; private set; }
    private int _thrustGeneration;

    private async Task ShowThrust(BattlePawn3D from, IReadOnlyList<BattlePawn3D> targets,
        int charge, Action<BattlePawn3D>? impact)
    {
        if (targets.Count == 0) return;
        int generation = _thrustGeneration;
        ThrustPlays++;
        int level = Math.Clamp(charge, 0, 4);
        double speed = Math.Clamp(from.AnimationSpeed, 0.1, 2);
        float facing = targets.Average(p => p.GlobalPosition.X) >= from.GlobalPosition.X ? 1 : -1;
        var hits = targets.OrderBy(p => p.GlobalPosition.X * facing).ToArray();
        float lane = hits[0].Position.Z;
        // 中央を含むX字の経路を、一拍だけ先頭と同じ奥行きへ寄せる。
        // 席も移動イベントも変更しない。終了時は全員が元の位置へ戻る。
        foreach (var pawn in hits)
            pawn.BeginThrustPose(new Vector3(pawn.Position.X, pawn.Position.Y, lane), 0.16 / speed);
        from.BeginThrustPose(new Vector3(hits[0].Position.X - facing * 2.4f, from.Position.Y, lane), 0.16 / speed);
        await ToSignal(GetTree().CreateTimer(0.18 / speed), SceneTreeTimer.SignalName.Timeout);
        if (generation != _thrustGeneration || !IsInstanceValid(from) || !from.IsInsideTree()) return;
        from.SetThrustCharge(0);
        _attackAudio.PlayThrust(from.UnitId, from.Team, charge);
        CameraPunch(from.GlobalPosition.Lerp(hits[0].GlobalPosition, 0.5f),
            level == 4 ? AttackPattern.All : AttackPattern.Pierce);

        Vector3 start = from.RapierTip(_camera);
        Vector3 direction = Vector3.Right * facing;
        from.AnimateThrust(direction, 0.42 / speed);
        float reach = Math.Max(0.6f, (hits[^1].FxPoint - start).Dot(direction)) + 0.55f + level * 0.70f;
        Vector3 end = start + direction * reach;
        Vector3 normal = direction.Cross(_camera.GlobalPosition - start).Normalized();
        // 2段から光の槍、4段は白い芯と二重の尾。小さな太さの差だけにしない。
        float width = level switch { 0 => 0.016f, 1 => 0.055f, 2 => 0.16f, 3 => 0.24f, _ => 0.40f };
        double duration = (level == 4 ? 0.92 : level >= 2 ? 0.82 : 0.70) / speed;
        if (level >= 2)
        {
            for (int i = 0; i < (level == 4 ? 10 : 6); i++)
            {
                float angle = i * Mathf.Tau / (level == 4 ? 10 : 6);
                Vector3 ray = _camera.GlobalBasis.X * Mathf.Cos(angle) + _camera.GlobalBasis.Y * Mathf.Sin(angle);
                MakeBeam(start + ray * 0.06f, start + ray * (level == 4 ? 0.95f : 0.45f),
                    new Color("ffe6a1"), level == 4 ? 0.06f : 0.035f, 0.24 / speed);
            }
        }
        var mesh = new ImmediateMesh();
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
        };
        var beam = new MeshInstance3D
        {
            Name = "SoraThrust", Mesh = mesh, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 16,
        };
        _fxRoot.AddChild(beam);
        var landed = new HashSet<int>();
        void Vertex(Vector3 p, Color c) { mesh.SurfaceSetColor(c); mesh.SurfaceAddVertex(beam.ToLocal(p)); }
        void Strip(float tail, float head, float radius, Color color, float offset = 0, bool soft = false)
        {
            for (int i = 0; i < 24; i++)
            {
                float u = i / 24f, v = (i + 1) / 24f;
                Vector3 a = start.Lerp(end, Mathf.Lerp(tail, head, u)) + normal * offset;
                Vector3 b = start.Lerp(end, Mathf.Lerp(tail, head, v)) + normal * offset;
                Vector3 wa = normal * radius * Mathf.Sin(u * Mathf.Pi);
                Vector3 wb = normal * radius * Mathf.Sin(v * Mathf.Pi);
                if (soft)
                {
                    Color edge = new(color, 0);
                    Vertex(a - wa, edge); Vertex(b - wb, edge); Vertex(a, color);
                    Vertex(a, color); Vertex(b - wb, edge); Vertex(b, color);
                    Vertex(a, color); Vertex(b, color); Vertex(a + wa, edge);
                    Vertex(a + wa, edge); Vertex(b, color); Vertex(b + wb, edge);
                    continue;
                }
                Vertex(a - wa, color); Vertex(b - wb, color); Vertex(a + wa, color);
                Vertex(a + wa, color); Vertex(b - wb, color); Vertex(b + wb, color);
            }
        }
        void Draw(float t)
        {
            if (generation != _thrustGeneration) return;
            float head = Mathf.Clamp(t / (level >= 2 ? 0.60f : 0.75f), 0, 1);
            float fade = 1 - Mathf.SmoothStep(level >= 2 ? 0.60f : 0.72f, 1, t);
            mesh.ClearSurfaces();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            Strip(0, head, width * 3, new Color(1, 0.52f, 0.10f, fade * (0.08f + 0.055f * level)), soft: true);
            Strip(Mathf.Max(0, head - (level >= 2 ? 0.90f : 0.46f)), head, width,
                new Color(1, 0.86f, 0.45f, fade * 0.95f));
            Strip(Mathf.Max(0, head - (level >= 2 ? 0.82f : 0.34f)), head, width * 0.40f,
                new Color(1, 1, 0.96f, fade));
            if (level >= 2)
            {
                float separation = width * (1.5f + 0.4f * Mathf.Sin(t * Mathf.Pi));
                for (int side = -1; side <= 1; side += 2)
                {
                    Strip(Mathf.Max(0, head - 0.75f), head * 0.92f, width * 0.18f,
                        new Color(1, 0.75f, 0.26f, fade * 0.85f), separation * side);
                    if (level == 4)
                        Strip(Mathf.Max(0, head - 0.60f), head * 0.82f, width * 0.10f,
                            new Color(1, 0.95f, 0.75f, fade * 0.75f), separation * side * 1.7f);
                }
            }
            mesh.SurfaceEnd();
            foreach (var pawn in hits)
            {
                float distance = (pawn.FxPoint - start).Dot(direction);
                if (distance > head * reach || !landed.Add(pawn.InstanceId)) continue;
                Vector3 point = start + direction * Math.Max(0, distance);
                bool marked = pawn.Team != from.Team && pawn.HasStatusIcon(StatusKeys.Marked);
                if (marked) ThrustMarkedHits++;
                float radius = (marked ? 0.70f : 0.28f) * (1 + level * 0.32f);
                int rays = (marked ? 8 : 4) + (level == 4 ? 4 : 0);
                for (int i = 0; i < rays; i++)
                {
                    float angle = i * Mathf.Tau / rays;
                    Vector3 ray = _camera.GlobalBasis.X * Mathf.Cos(angle) + _camera.GlobalBasis.Y * Mathf.Sin(angle);
                    MakeBeam(point + ray * 0.07f, point + ray * radius,
                        new Color("fff0b4"), (marked ? 0.055f : 0.025f) * (1 + level * 0.20f),
                        (level >= 2 ? 0.42 : 0.24) / speed);
                }
                impact?.Invoke(pawn);
            }
        }
        Draw(0.001f);
        var tween = beam.CreateTween();
        tween.TweenMethod(Callable.From<float>(Draw), 0.001f, 1f, duration);
        // 再生し直しでエフェクトが消されても待ち続けない。
        await ToSignal(GetTree().CreateTimer(duration + 0.02 / speed), SceneTreeTimer.SignalName.Timeout);
        if (generation != _thrustGeneration || !IsInstanceValid(beam)) return;
        beam.QueueFree();
        foreach (var pawn in hits) pawn.EndThrustPose(0.18 / speed);
        from.EndThrustPose(0.18 / speed);
        await ToSignal(GetTree().CreateTimer(0.20 / speed), SceneTreeTimer.SignalName.Timeout);
    }
}
