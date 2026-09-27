using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    internal int MireSlams, MireConducts, MireFlows, MireBursts;
    private static Texture2D? _mireSplashTexture;
    internal const string MireSplashPath = "res://assets/fx/mio_murky_splash_v1.png";
    internal static float MireBurstSize(int amount) => 0.55f + Math.Clamp(MathF.Sqrt(Math.Max(0, amount)) * 0.17f, 0, 2.2f);
    private Vector3 MireFoot(BattlePawn3D pawn) => _actorRoot.ToGlobal(pawn.Home) + Vector3.Up * 0.12f;

    public void ShowMireSlam(BattlePawn3D? actor, BattlePawn3D? target, bool electric, double speed)
    {
        if (actor is null || target is null) return;
        MireSlams++;
        var top = target.FxPoint + Vector3.Up * 1.8f;
        FlowDrops(actor.FxPoint, top, MioWater, 0.20 / speed, false, pull: true);
        MireBlob(top, MireFoot(target), 0.65f, 0.20 / speed, 0.20 / speed);
        MireSplash(MireFoot(target), 1.1f, 0.36 / speed, 0.32 / speed);
        MireCue(0.32 / speed, _attackAudio.PlayMireSlam);
        if (electric)
        {
            MireCue(0.32 / speed, () => {
                ThunderFx.Burst(_fxRoot, MireFoot(target), 1.0f, 0.26 / speed);
                _attackAudio.PlayMireConduct();
                MakeGroundRing(target.Home, ThunderFx.Cyan, 1.0f, 0.3 / speed);
            });
        }
    }

    public void ShowMireConduct(BattlePawn3D? from, BattlePawn3D? to, double speed)
    {
        if (from is null || to is null) return;
        MireConducts++;
        _attackAudio.PlayMireConduct();
        var a = MireFoot(from); var b = MireFoot(to);
        MireStream(a, b, 0.46 / speed);
        FlowDrops(a, b, MioWater, 0.24 / speed, false);
        for (int i = 0; i < 5; i++)
            WaterVortex(a.Lerp(b, i / 4f), MioWater, 0.34 / speed, i * 0.02 / speed, 0.48f, ground: true);
        ThunderFx.Arc(_fxRoot, a + Vector3.Up * 0.12f, b + Vector3.Up * 0.12f, 0.055f, 0.42 / speed, ground: true);
        MakeGroundRing(to.Home, ThunderFx.Cyan, 0.9f, 0.28 / speed);
    }

    public void ShowMireFlow(BattlePawn3D? from, BattlePawn3D? to, bool carried, int amount, double speed)
    {
        if (from is null || to is null) return;
        MireFlows++;
        _attackAudio.PlayMireFlow();
        var a = carried ? from.DischargePoint : MireFoot(from);
        var b = carried ? to.DischargePoint : MireFoot(to);
        if (!carried) MireStream(a, b, 0.55 / speed);
        FlowDrops(a, b, MioWater, 0.28 / speed, false);
        if (carried) ThunderFx.Arc(_fxRoot, a, b, 0.018f, 0.26 / speed);
        else for (int i = 0; i < 6; i++)
            WaterVortex(a.Lerp(b, i / 5f), MioWater, 0.42 / speed, i * 0.035 / speed,
                0.32f + Math.Min(amount, 8) * 0.035f, ground: true);
        WaterVortex(MireFoot(to), MioWater, 0.44 / speed, 0.18 / speed, 0.8f, ground: true);
    }

    public void ShowMireBurst(BattlePawn3D? from, IReadOnlyList<BattleEvent> hits, double speed)
    {
        if (from is null || hits.Count == 0) return;
        MireBursts++;
        float size = MireBurstSize(hits.Max(e => e.Amount));
        Vector3 origin = MireFoot(from);
        MireBlob(origin, origin + Vector3.Up * size * 0.45f, size, 0.12 / speed);
        MireSplash(origin, size, 0.55 / speed, 0.12 / speed);
        MireCue(0.12 / speed, () => {
            _attackAudio.PlayMireBurst(size > 1.5f);
            if (size > 1.5f) CameraPunch(origin, size > 2 ? AttackPattern.All : AttackPattern.Sweep);
            foreach (var hit in hits)
            {
                if (FindPawn(hit.TargetId) is not { } target) continue;
                FlowDrops(origin + Vector3.Up * size * 0.5f, target.FxPoint, MioWater, 0.22 / speed, false);
                MireSplash(MireFoot(target), size * 0.6f, 0.38 / speed, 0.12 / speed);
                if (hit.BrittleExtra > 0)
                    FlowDrops(origin, target.FxPoint, new Color("f49b51"), 0.28 / speed, true);
            }
        });
    }

    private void MireBlob(Vector3 from, Vector3 to, float size, double seconds, double delay = 0)
    {
        var blob = new MeshInstance3D {
            Mesh = new SphereMesh { Radius = 0.5f, Height = 1, RadialSegments = 20, Rings = 12 },
            MaterialOverride = MakeMaterial(new Color("34594f"), true, false, 0.25f),
            Position = _fxRoot.ToLocal(from), Scale = new Vector3(size, size * 0.25f, size)
        };
        _fxRoot.AddChild(blob);
        var t = blob.CreateTween();
        if (delay > 0) t.TweenInterval(delay);
        t.TweenProperty(blob, "position", _fxRoot.ToLocal(to), seconds);
        t.Parallel().TweenProperty(blob, "scale", new Vector3(size, size * 0.9f, size), seconds);
        t.TweenCallback(Callable.From(blob.QueueFree));
    }

    private void MireStream(Vector3 from, Vector3 to, double seconds)
    {
        var mesh = new ImmediateMesh();
        var material = MakeMaterial(new Color(MioWater, 0.7f), true, true);
        material.NoDepthTest = true;
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var stream = new MeshInstance3D { Mesh = mesh, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _fxRoot.AddChild(stream);
        var side = (to - from).Cross(Vector3.Up).Normalized();
        var tween = stream.CreateTween();
        tween.TweenMethod(Callable.From<float>(t => {
            mesh.ClearSurfaces(); mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            Vector3 Point(float u) => from.Lerp(to, u * Math.Min(1, t * 2))
                + side * Mathf.Sin(u * 12 - t * 8) * 0.12f;
            for (int i = 0; i < 20; i++)
            {
                float u = i / 20f, v = (i + 1) / 20f;
                var a = Point(u); var b = Point(v);
                var width = side * (0.16f + 0.12f * Mathf.Sin(u * Mathf.Pi));
                foreach (var p in new[] { a-width, a+width, b+width, a-width, b+width, b-width })
                    mesh.SurfaceAddVertex(_fxRoot.ToLocal(p));
            }
            mesh.SurfaceEnd();
            stream.Transparency = Mathf.Max(0, (t - 0.6f) / 0.4f);
        }), 0.01f, 1f, seconds);
        tween.TweenCallback(Callable.From(stream.QueueFree));
    }

    private void MireSplash(Vector3 origin, float size, double seconds, double delay)
    {
        WaterVortex(origin, new Color("34594f"), seconds * 1.3, delay, size * 1.1f, ground: true);
        // 生成した透明な水膜を広げて落とす。足元の水面と独立した、重量のある飛沫。
        _mireSplashTexture ??= UiKit.LoadTexture(MireSplashPath);
        var splash = LiliFx.Sprite(_fxRoot, origin + Vector3.Up * size * 0.7f, _mireSplashTexture, size * 2.7f);
        splash.Visible = false;
        splash.Scale = Vector3.One * 0.25f;
        var splashTween = splash.CreateTween();
        if (delay > 0) splashTween.TweenInterval(delay);
        splashTween.TweenCallback(Callable.From(() => splash.Visible = true));
        splashTween.TweenProperty(splash, "scale", Vector3.One, seconds * 0.26)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        splashTween.TweenProperty(splash, "scale", new Vector3(1.12f, 0.78f, 1), seconds * 0.74);
        splashTween.Parallel().TweenProperty(splash, "modulate:a", 0f, seconds * 0.74);
        splashTween.Parallel().TweenProperty(splash, "position", splash.Position - Vector3.Up * size * 0.25f, seconds * 0.74);
        splashTween.TweenCallback(Callable.From(splash.QueueFree));
        MireCue(delay, () => {
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.Tau / 12;
                var end = origin + new Vector3(Mathf.Cos(angle), 0.12f, Mathf.Sin(angle)) * size;
                FlowDrops(origin + Vector3.Up * size * 0.5f, end, i % 3 == 0 ? new Color("7e9b48") : MioWater,
                    seconds, false);
            }
        });
    }

    // 再戦・中断では他の飛沫と同じく子ノードごと消す。次の戦へ遅延発火を持ち越さない。
    private void MireCue(double delay, Action action)
    {
        var cue = new Node3D();
        _fxRoot.AddChild(cue);
        var tween = cue.CreateTween();
        tween.TweenInterval(Math.Max(0.001, delay));
        tween.TweenCallback(Callable.From(action));
        tween.TweenCallback(Callable.From(cue.QueueFree));
    }
}
