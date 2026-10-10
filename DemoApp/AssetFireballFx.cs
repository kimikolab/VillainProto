using Godot;
using System;

// 購入素材の試着用。移動と寿命は再生側が管理し、作者の反復スクリプトは使わない。
internal static class AssetFireballFx
{
    private const string Root = "res://PolyBlocks/EffectBlocks/assets/";

    internal static void Launch(Node3D parent, Vector3 from, Vector3 to, double seconds, int ordinal)
    {
        var projectile = new Node3D();
        parent.AddChild(projectile);
        projectile.GlobalPosition = from;
        var flame = GD.Load<PackedScene>(Root + "fire/fire_light.tscn").Instantiate<Node3D>();
        Prepare(flame, false, ordinal);
        projectile.AddChild(flame);
        // 炎の上昇方向を弾の後ろへ向け、芯の周囲から尾が伸びる形にする。
        Vector3 tail = (from - to).Normalized();
        Vector3 side = tail.Cross(Vector3.Forward).Normalized();
        if (side.LengthSquared() < 0.01f) side = Vector3.Right;
        flame.Basis = new Basis(side, tail, side.Cross(tail).Normalized());
        flame.Scale = new Vector3(0.72f, 1.35f, 0.72f);
        var tween = projectile.CreateTween();
        tween.TweenMethod(Callable.From<float>(p =>
            projectile.GlobalPosition = from.Lerp(to, p) + Vector3.Up * MathF.Sin(p * Mathf.Pi) * 0.15f),
            0f, 1f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(projectile.QueueFree));
    }

    internal static void Impact(Node3D parent, Vector3 point, double speed, int ordinal, float size = 1)
    {
        var explosion = GD.Load<PackedScene>(Root + "explosions/explosion_light.tscn").Instantiate<Node3D>();
        Prepare(explosion, true, ordinal);
        // 爆発の球メッシュを半透明の炎へ替え、重なっても平たい単色の塊にしない。
        var flameSource = GD.Load<PackedScene>(Root + "fire/fire_light.tscn").Instantiate<Node3D>();
        explosion.GetNode<GpuParticles3D>("Fire").DrawPass1 = flameSource.GetNode<GpuParticles3D>("Flame").DrawPass1;
        flameSource.Free();
        parent.AddChild(explosion);
        explosion.GlobalPosition = point;
        explosion.Scale = Vector3.One * (2.3f * size);
        foreach (Node child in explosion.GetChildren())
            if (child is GpuParticles3D particles)
            {
                particles.SpeedScale = speed;
                particles.Restart();
            }
        var tween = explosion.CreateTween();
        tween.TweenInterval(0.9 / speed);
        tween.TweenCallback(Callable.From(explosion.QueueFree));
        FireFx.Light(parent, point, new Color("ff7028"), 0.7f, 0.25 / speed);
    }

    internal static void Prepare(Node node, bool impact, int ordinal)
    {
        node.SetScript(default);
        if (node is AudioStreamPlayer3D audio) audio.Autoplay = false;
        if (node is Light3D light) light.LightEnergy = 0.15f;
        if (node is GpuParticles3D particles)
        {
            var material = (ParticleProcessMaterial)particles.ProcessMaterial.Duplicate();
            particles.ProcessMaterial = material;
            particles.UseFixedSeed = true;
            particles.Seed = (uint)(8201 + ordinal * 37);
            particles.LocalCoords = true;
            particles.Emitting = true;
            if (impact)
            {
                particles.OneShot = true;
                particles.Lifetime = particles.Name == "Smoke" ? 0.65 : 0.55;
                particles.Amount = particles.Name == "Smoke" ? 10 : particles.Name == "Sparks" ? 14 : 24;
                if (particles.Name == "Smoke") { material.ScaleMin *= 0.65f; material.ScaleMax *= 0.65f; }
                if (particles.Name == "Fire")
                {
                    material.ScaleMin = 0.15f;
                    material.ScaleMax = 0.22f;
                    material.Direction = Vector3.Up;
                    material.Spread = 180;
                    material.InitialVelocityMin = 0.8f;
                    material.InitialVelocityMax = 1.4f;
                    material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
                    material.EmissionSphereRadius = 0.12f;
                    material.LifetimeRandomness = 0.1f;
                    material.Color = Colors.White;
                    var heat = new Curve();
                    heat.AddPoint(new Vector2(0, 0.6f));
                    heat.AddPoint(new Vector2(0.25f, 0.45f));
                    heat.AddPoint(new Vector2(1, 0));
                    material.EmissionCurve = new CurveTexture { Curve = heat };
                    material.ColorRamp = new GradientTexture1D { Gradient = new Gradient {
                        Offsets = new[] { 0f, 0.35f, 0.75f, 1f },
                        Colors = new[] { new Color("ffe1a0"), new Color("ff902b"), new Color("d53d13"), new Color("49291d") }
                    } };
                }
            }
            else
            {
                particles.OneShot = false;
                particles.Lifetime = 0.32;
                particles.Preprocess = 0.32;
                particles.Amount = particles.Name == "Flame" ? 44 : 8;
                particles.Visible = particles.Name != "Smoke";
                if (particles.Name == "Flame")
                {
                    material.Direction = Vector3.Up;
                    material.InitialVelocityMin = 1.8f;
                    material.InitialVelocityMax = 3.0f;
                    material.Gravity = Vector3.Zero;
                    material.Spread = 16;
                    material.ScaleMin = 0.22f;
                    material.ScaleMax = 0.36f;
                    material.EmissionSphereRadius = 0.18f;
                }
            }
        }
        foreach (Node child in node.GetChildren()) Prepare(child, impact, ordinal);
    }
}
