using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private static readonly Color ShieldTint = new("e3b577");
    private static readonly Color ScreamTint = new("d9e9eb");
    private int _shieldCowedGeneration;

    public void ShowFooting(BattlePawn3D pawn)
    {
        pawn.BraceShield();
        MakeGroundRing(pawn.RestPosition, ShieldTint, 0.72f, 0.30);
    }

    public async Task ShowRangeShield(BattlePawn3D? attacker, IReadOnlyList<BattlePawn3D> impacted,
        IReadOnlyList<(BattlePawn3D Covered, BattlePawn3D Shield)> shares, AttackPattern pattern, double speed)
    {
        // 2倍速でも折れと着弾を見分けられる最小時間を確保する。
        int generation = _shieldCowedGeneration;
        double travel = Math.Max(0.22, 0.42 / Math.Max(0.1, speed));
        var covered = shares.Select(s => s.Covered).ToHashSet();
        foreach (var hit in impacted.Where(p => !covered.Contains(p)))
        {
            Vector3 start = pattern == AttackPattern.All || attacker is null
                ? hit.FxPoint + Vector3.Up * 4 : attacker.FxPoint;
            ShieldBlade(start, start.Lerp(hit.FxPoint, 0.62f), hit.FxPoint, travel);
        }
        foreach (var share in shares)
        {
            Vector3 destination = share.Shield.FxPoint;
            Vector3 start = pattern == AttackPattern.All || attacker is null
                ? share.Covered.FxPoint + Vector3.Up * 4 : attacker.FxPoint;
            Vector3 bend = start.Lerp(share.Covered.FxPoint, 0.72f);
            ShieldBlade(start, bend, destination, travel);
        }
        await ToSignal(GetTree().CreateTimer(travel), SceneTreeTimer.SignalName.Timeout);
        if (generation != _shieldCowedGeneration) return;
        foreach (var shield in shares.Select(s => s.Shield).Distinct())
        {
            if (!GodotObject.IsInstanceValid(shield) || !shield.IsInsideTree()) continue;
            shield.AnimationSpeed = speed;
            ShowFooting(shield);
            for (int i = 0; i < 5; i++)
            {
                float angle = i * Mathf.Tau / 5;
                var direction = new Vector3(Mathf.Cos(angle), 0.16f, Mathf.Sin(angle));
                MakeBeam(shield.RestPosition + Vector3.Up * 0.10f + direction * 0.35f,
                    shield.RestPosition + Vector3.Up * 0.10f + direction * 0.9f, ShieldTint, 0.055f, 0.24);
            }
        }
    }

    private void ShieldBlade(Vector3 start, Vector3 bend, Vector3 end, double travel)
    {
        var blade = new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = new Vector3(0.18f, 0.12f, 0.62f) },
            Position = start,
            MaterialOverride = MakeMaterial(ShieldTint, true, true, 0.1f, ShieldTint),
        };
        _fxRoot.AddChild(blade);
        var tween = blade.CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            Vector3 previous = blade.Position;
            Vector3 point = t < 0.56f ? start.Lerp(bend, t / 0.56f) : bend.Lerp(end, (t - 0.56f) / 0.44f);
            blade.Position = point;
            if (point.DistanceSquaredTo(previous) > 0.00001f)
            {
                MakeBeam(previous, point, new Color(ShieldTint, 0.65f), 0.045f, 0.18);
                Vector3 direction = (point - previous).Normalized();
                blade.LookAt(point + direction, Math.Abs(direction.Dot(Vector3.Up)) > 0.95 ? Vector3.Right : Vector3.Up);
            }
        }), 0f, 1f, travel);
        tween.Finished += blade.QueueFree;
    }

    public async Task ShowCowedSpread(BattlePawn3D? victim, IReadOnlyList<BattlePawn3D> recipients, double speed)
    {
        int generation = _shieldCowedGeneration;
        double travel = Math.Max(0.24, 0.48 / Math.Max(0.1, speed));
        if (victim is not null)
        {
            // 各通知の対象へだけ扇状の波を送る。盤面の隣接関係は再計算しない。
            foreach (var recipient in recipients)
                ScreamWave(victim.FxPoint, recipient.FxPoint, travel);
        }
        await ToSignal(GetTree().CreateTimer(travel), SceneTreeTimer.SignalName.Timeout);
        if (generation != _shieldCowedGeneration) return;
        foreach (var recipient in recipients)
        {
            if (!GodotObject.IsInstanceValid(recipient) || !recipient.IsInsideTree()) continue;
            recipient.AnimationSpeed = speed;
            recipient.SetStatusIcon(StatusKeys.Cowed, true);
        }
        await ToSignal(GetTree().CreateTimer(Math.Max(0.16, 0.28 / Math.Max(0.1, speed))), SceneTreeTimer.SignalName.Timeout);
    }

    private void ScreamWave(Vector3 origin, Vector3 destination, double travel)
    {
        var mesh = new ImmediateMesh();
        var material = MakeMaterial(ScreamTint, true, true, 0.1f, ScreamTint);
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var wave = new MeshInstance3D { Mesh = mesh,
            MaterialOverride = material };
        _fxRoot.AddChild(wave);
        Vector3 direction = (destination - origin).Normalized();
        Vector3 side = direction.Cross(Vector3.Up).Normalized();
        float distance = origin.DistanceTo(destination);
        var tween = wave.CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            mesh.ClearSurfaces();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            for (int ring = 0; ring < 3; ring++)
            {
                float radius = Math.Max(0.05f, t * distance - ring * 0.23f);
                for (int j = 0; j < 20; j++)
                {
                    float a = -0.55f + j * 1.1f / 20;
                    float b = a + 1.1f / 20;
                    Vector3 da = direction * Mathf.Cos(a) + side * Mathf.Sin(a);
                    Vector3 db = direction * Mathf.Cos(b) + side * Mathf.Sin(b);
                    Vector3 p = origin + da * radius, q = origin + db * radius;
                    Vector3 r = origin + da * (radius + 0.045f), s = origin + db * (radius + 0.045f);
                    mesh.SurfaceAddVertex(p); mesh.SurfaceAddVertex(q); mesh.SurfaceAddVertex(r);
                    mesh.SurfaceAddVertex(r); mesh.SurfaceAddVertex(q); mesh.SurfaceAddVertex(s);
                }
            }
            mesh.SurfaceEnd();
        }), 0.06f, 1f, travel);
        tween.TweenProperty(wave, "transparency", 1f, 0.16);
        tween.Finished += wave.QueueFree;
    }
}
