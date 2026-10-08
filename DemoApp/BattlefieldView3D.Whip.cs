using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int TormentHitPlays { get; private set; }
    internal int WhipSweeps, ElectricWhipSweeps;

    // 一振りの先端を敵の並びに沿って走らせる。電光は革の鞭の上にだけ乗る。
    public async Task ShowWhipSweep(BattlePawn3D from, IReadOnlyList<BattlePawn3D> targets,
        Action<BattlePawn3D>? impact = null)
    {
        if (targets.Count == 0) return;
        WhipSweeps++;
        var hits = targets.OrderBy(p => p.FxPoint.Z).ToArray();
        bool electric = from.HasShockAura || from.InterruptWhip;
        int chain = from.InterruptWhip ? from.WhipChainSize : 0;
        float thickness = 1 + Math.Min(chain, 8) * 0.42f;
        if (electric) ElectricWhipSweeps++;
        double speed = Math.Max(0.1, from.AnimationSpeed);
        int generation = _specialGeneration;
        from.ShowMovementPortrait("shiga_interrupt", 0.82);
        from.BeginWhipFlurry();
        var material = MakeMaterial(electric ? new Color("c4f8ff") : new Color("936447"), true, true,
            0.35f, electric ? ThunderFx.Cyan : Colors.Black);
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        if (electric) material.EmissionEnergyMultiplier = 1.2f + chain * 0.3f;
        var mesh = new ImmediateMesh();
        var whip = new MeshInstance3D { Mesh = mesh, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _fxRoot.AddChild(whip);
        int landed = 0;
        var tween = whip.CreateTween();
        tween.TweenMethod(Callable.From<float>(t => {
            if (generation != _specialGeneration || !IsInstanceValid(from)) return;
            float sweep = Mathf.Clamp((t - 0.22f) / 0.55f, 0, 1) * Math.Max(1, hits.Length - 1);
            int n = Math.Min((int)sweep, hits.Length - 1);
            Vector3 end = hits[n].FxPoint.Lerp(hits[Math.Min(n + 1, hits.Length - 1)].FxPoint, sweep - n);
            Vector3 start = from.WhipOrigin(_camera);
            float reach = Mathf.Clamp(t / 0.22f, 0, 1) * (t > 0.82f ? (1 - t) / 0.18f : 1);
            Vector3 Point(float u) => start.Lerp(end, u * reach)
                + Vector3.Up * Mathf.Sin(u * Mathf.Pi) * (0.25f + (1 - reach) * 1.5f)
                + Vector3.Forward * Mathf.Sin(u * 9 - t * 24) * Mathf.Sin(u * Mathf.Pi) * 0.65f;
            mesh.ClearSurfaces(); mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            for (int i = 0; i < 40; i++)
            {
                Vector3 a = Point(i / 40f), b = Point((i + 1) / 40f);
                Vector3 w = (b - a).Cross(_camera.GlobalPosition - a).Normalized() * 0.035f * thickness * (1 - i / 40f * 0.6f);
                foreach (var p in new[] { a-w, b-w, a+w, a+w, b-w, b+w })
                    mesh.SurfaceAddVertex(_fxRoot.ToLocal(p));
            }
            mesh.SurfaceEnd();
            while (landed < hits.Length && t >= 0.22f + 0.55f * landed / Math.Max(1, hits.Length - 1))
            {
                var hit = hits[landed++];
                if (landed == 1)
                {
                    WhipChainContact(from, speed);
                    if (electric) _attackAudio.PlayShockMark(ShockMarkSound.ElectricWhip, speed: speed);
                }
                if (electric)
                {
                    for (int k = 0; k < 6; k++)
                        ThunderFx.Arc(_fxRoot, Point(k / 6f), Point((k + 1) / 6f), (from.InterruptWhip ? 0.020f : 0.018f) * thickness, 0.20 / speed);
                    ThunderFx.Burst(_fxRoot, hit.FxPoint, 0.5f + chain * 0.10f, 0.22 / speed);
                }
                impact?.Invoke(hit);
                NotifyAttackContact(hit);
            }
        }), 0f, 1f, 0.64 / speed);
        tween.TweenCallback(Callable.From(whip.QueueFree));
        await ToSignal(GetTree().CreateTimer(0.64 / speed), SceneTreeTimer.SignalName.Timeout);
    }

    public void ShowWhipFlash(BattlePawn3D target, int amount, double speed)
    {
        float size = Math.Clamp(0.18f + amount * 0.008f, 0.22f, 0.85f);
        for (int i = 0; i < 6; i++)
        {
            float a = i * Mathf.Tau / 6;
            var ray = _camera.GlobalBasis.X * Mathf.Cos(a) + Vector3.Up * Mathf.Sin(a);
            MakeBeam(target.FxPoint, target.FxPoint + ray * size, new Color("ffe0ba"), 0.035f, 0.18 / speed);
        }
    }
    // 責め苦はAttackを増やさない特性ダメージ。踏み込み・追加攻撃のカットインは呼ばない。
    public async Task ShowTormentHit(BattlePawn3D from, BattlePawn3D target)
    {
        TormentHitPlays++;
        if (!from.HasShockAura && !from.InterruptWhip)
            _attackAudio.PlayAttack(from.UnitId, from.Team, BattleCore.AttackPattern.Single, false, false);
        await ShowWhipAttack(from, target);
    }

    // 手元からしなる一本の鞭。先端の波が走り、伸びきった瞬間に小さな火花を出す。
    // 戦闘の乱数やダメージには触れず、戻りの動作もこの表示の完了を待つ。
    private async Task ShowWhipAttack(BattlePawn3D from, BattlePawn3D target)
    {
        int generation = _specialGeneration;
        int chain = from.InterruptWhip ? from.WhipChainSize : 0;
        bool electric = from.HasShockAura || from.InterruptWhip;
        float thickness = 1 + Math.Min(chain, 8) * 0.42f;
        from.ShowMovementPortrait("shiga_interrupt", 0.82);
        from.BeginWhipFlurry();
        Vector3 direction = (target.FxPoint - from.FxPoint).Normalized();
        Vector3 start = from.WhipOrigin(_camera) + direction * 0.08f;
        Vector3 end = target.FxPoint;
        Vector3 sideways = direction.Cross(Vector3.Up).Normalized();
        Vector3 view = _camera.GlobalPosition - (start + end) * 0.5f;
        var material = MakeMaterial(electric ? new Color("c4f8ff") : new Color("b84e69"), true, true,
            0.35f, electric ? ThunderFx.Cyan : new Color("682239"));
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        if (electric) material.EmissionEnergyMultiplier = 1.2f + chain * 0.3f;
        var mesh = new ImmediateMesh();
        var whip = new MeshInstance3D { Mesh = mesh, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _fxRoot.AddChild(whip);
        double duration = 0.60 / Math.Max(0.1, from.AnimationSpeed);
        bool cracked = false;
        var tween = whip.CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            if (generation != _specialGeneration || !IsInstanceValid(from) || !IsInstanceValid(target)) return;
            start = from.WhipOrigin(_camera) + direction * 0.08f;
            float reach = t < 0.62f ? Mathf.Pow(t / 0.62f, 0.55f) : 1 - (t - 0.62f) / 0.38f * 0.7f;
            float curl = t < 0.62f ? (1 - t / 0.62f) : (t - 0.62f) * 1.5f;
            Vector3 Point(float u) => start.Lerp(end, u * reach)
                + Vector3.Up * (Mathf.Sin(u * Mathf.Pi) * curl * 1.6f)
                + sideways * (Mathf.Sin(u * Mathf.Tau - t * 24) * Mathf.Sin(u * Mathf.Pi) * (curl + 0.18f) * 0.85f);
            mesh.ClearSurfaces();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            for (int i = 0; i < 48; i++)
            {
                float u = i / 48f, v = (i + 1) / 48f;
                Vector3 a = Point(u), b = Point(v);
                Vector3 normal = (b - a).Cross(view).Normalized();
                Vector3 wa = normal * Mathf.Lerp(0.048f, 0.012f, u) * thickness;
                Vector3 wb = normal * Mathf.Lerp(0.048f, 0.012f, v) * thickness;
                mesh.SurfaceAddVertex(a - wa); mesh.SurfaceAddVertex(b - wb); mesh.SurfaceAddVertex(a + wa);
                mesh.SurfaceAddVertex(a + wa); mesh.SurfaceAddVertex(b - wb); mesh.SurfaceAddVertex(b + wb);
            }
            mesh.SurfaceEnd();
            if (!cracked && t >= 0.62f)
            {
                cracked = true;
                if (electric) _attackAudio.PlayShockMark(ShockMarkSound.ElectricWhip, speed: from.AnimationSpeed);
                WhipChainContact(from, Math.Max(0.1, from.AnimationSpeed));
                if (from.HasShockAura || from.InterruptWhip)
                {
                    for (int k = 0; k < 7; k++) ThunderFx.Arc(_fxRoot, Point(k / 7f), Point((k + 1) / 7f),
                        0.02f * thickness, 0.22 / from.AnimationSpeed);
                    ThunderFx.Burst(_fxRoot, end, 0.65f + chain * 0.10f, 0.22 / from.AnimationSpeed);
                }
                NotifyAttackContact(target);
                Color flash = new("ffe0ba");
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.Tau / 5;
                    Vector3 ray = Vector3.Up * Mathf.Cos(angle) + sideways * Mathf.Sin(angle);
                    MakeBeam(end + ray * 0.08f, end + ray * 0.32f, flash, 0.036f, 0.12);
                }
            }
            whip.Transparency = Mathf.Max(0, (t - 0.78f) / 0.22f);
        }), 0.01f, 1f, duration);
        tween.Finished += whip.QueueFree;
        await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
    }
}
