using Godot;
using System;
using System.Collections.Generic;

// 貫通軸に炎を連ねる比較用演出。命中対象と時刻は既存の再生処理が決める。
internal static class AssetFirePierceFx
{
    // 旧火槍の先端・尾・消滅に合わせ、芯を隠さない薄い炎を両縁へ追従させる。
    internal static void Coat(Node3D parent, Vector3 from, Vector3 end, Vector3 cameraNormal, bool grand, double speed)
    {
        var root = new Node3D();
        parent.AddChild(root);
        Vector3 direction = (end - from).Normalized();
        Vector3 side = direction.Cross(cameraNormal).Normalized();
        Vector3 tail = -direction;
        var basis = new Basis(side, tail, side.Cross(tail).Normalized());
        var scene = GD.Load<PackedScene>("res://PolyBlocks/EffectBlocks/assets/fire/fire_light.tscn");
        int pairs = grand ? 18 : 9;
        var flames = new List<GpuParticles3D>();
        for (int i = 0; i < pairs * 2; i++)
        {
            var source = scene.Instantiate<Node3D>();
            AssetFireballFx.Prepare(source, false, i + 90);
            var flame = source.GetNode<GpuParticles3D>("Flame");
            source.RemoveChild(flame);
            source.Free();
            flame.Amount = grand ? 14 : 12;
            flame.SpeedScale = speed;
            root.AddChild(flame);
            flame.Basis = basis;
            flame.Scale = grand ? new Vector3(0.72f, 1.35f, 0.72f) : new Vector3(0.60f, 1.1f, 0.60f);
            flame.Visible = false;
            flames.Add(flame);
        }
        float distance = from.DistanceTo(end);
        double lifetime = grand ? 0.60 : 0.33;
        var tween = root.CreateTween();
        tween.TweenMethod(Callable.From<float>(p => {
            float travel = Mathf.Min(1, p / (grand ? 0.28f : 0.65f));
            Vector3 tip = from.Lerp(end, travel);
            Vector3 back = grand ? from : tip - direction * Mathf.Min(3.1f, distance * travel + 0.03f);
            float fade = 1 - Mathf.SmoothStep(grand ? 0.64f : 0.72f, 1, p);
            for (int i = 0; i < flames.Count; i++)
            {
                float along = (i / 2 + 0.5f) / pairs;
                float rim = Mathf.Sin(along * Mathf.Pi) * (grand ? 0.66f : 0.24f);
                var flame = flames[i];
                flame.GlobalPosition = back.Lerp(tip, along) + side * rim * (i % 2 == 0 ? -1 : 1);
                flame.Visible = true;
                flame.Transparency = 1 - fade * 0.78f;
            }
        }), 0f, 1f, lifetime / speed);
        tween.TweenCallback(Callable.From(root.QueueFree));
    }

    internal static void Shoot(Node3D parent, Vector3 from, Vector3 end, bool grand, double speed)
    {
        var root = new Node3D();
        parent.AddChild(root);
        Vector3 tail = (from - end).Normalized();
        Vector3 side = tail.Cross(Vector3.Forward).Normalized();
        if (side.LengthSquared() < 0.01f) side = Vector3.Right;
        var basis = new Basis(side, tail, side.Cross(tail).Normalized());
        var scene = GD.Load<PackedScene>("res://PolyBlocks/EffectBlocks/assets/fire/fire_light.tscn");
        int count = Math.Clamp((int)(from.DistanceTo(end) / 0.48f), 8, 42);
        var segments = new List<GpuParticles3D>();
        for (int i = 0; i < count; i++)
        {
            var source = scene.Instantiate<Node3D>();
            AssetFireballFx.Prepare(source, false, i + 20);
            var flame = source.GetNode<GpuParticles3D>("Flame");
            source.RemoveChild(flame);
            source.Free();
            flame.Amount = grand ? 22 : 14;
            flame.SpeedScale = speed;
            root.AddChild(flame);
            flame.GlobalPosition = from.Lerp(end, i / (float)(count - 1));
            flame.Basis = basis;
            float width = grand ? 0.9f : 0.58f;
            flame.Scale = new Vector3(width, grand ? 2.0f : 1.7f, width);
            flame.Visible = false;
            segments.Add(flame);
        }
        double travel = grand ? 0.17 : 0.20;
        double lifetime = grand ? 0.64 : 0.46;
        var tween = root.CreateTween();
        tween.TweenMethod(Callable.From<float>(time => {
            float front = Mathf.Min(1, time / (float)travel);
            float fade = 1 - Mathf.SmoothStep((float)travel + 0.04f, (float)lifetime, time);
            for (int i = 0; i < segments.Count; i++)
            {
                var flame = segments[i];
                flame.Visible = i / (float)(count - 1) <= front;
                flame.Transparency = 1 - fade;
            }
        }), 0f, (float)lifetime, lifetime / speed);
        tween.TweenCallback(Callable.From(root.QueueFree));
    }
}
