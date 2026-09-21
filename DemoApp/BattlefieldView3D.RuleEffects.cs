using BattleCore;
using Godot;
using System;

public partial class BattlefieldView3D
{
    public static Color RuleColor(string rule) => rule switch
    {
        SealedLabels.Hush => Color.FromHtml("#bf91f3"),
        SealedLabels.Drought => Color.FromHtml("#71d7a1"),
        SealedLabels.Yoke => Color.FromHtml("#efc66a"),
        _ => UiKit.Violet,
    };

    public void ShowYokeSeal(BattlePawn3D? target, int cut)
    {
        if (target is null) return;
        Color color = RuleColor(SealedLabels.Yoke);
        float strength = Math.Clamp(cut / 60f, 0.15f, 1f);
        Vector3 center = target.FxPoint + Vector3.Up * 0.75f;
        // 上から落ちる横木が頭上で急停止し、両端から押さえ込む。量は切られた実数のみ。
        var bar = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.2f + strength, 0.14f + strength * 0.16f, 0.24f) },
            Position = center + Vector3.Up * 0.65f,
            MaterialOverride = MakeMaterial(color, true, true),
        };
        _fxRoot.AddChild(bar);
        var tween = bar.CreateTween();
        tween.TweenProperty(bar, "position", center, 0.07);
        tween.TweenCallback(Callable.From(() =>
        {
            MakeBeam(center - Vector3.Right * 0.65f, center - Vector3.Right * 0.65f - Vector3.Up * 0.6f, color, 0.12f, 0.32);
            MakeBeam(center + Vector3.Right * 0.65f, center + Vector3.Right * 0.65f - Vector3.Up * 0.6f, color, 0.12f, 0.32);
            RuleFragments(center, color, 6 + (int)(strength * 10), false);
        }));
        tween.TweenInterval(0.18);
        tween.TweenProperty(bar, "transparency", 1f, 0.22);
        tween.Finished += bar.QueueFree;
    }

    public void ShowDroughtSeal(BattlePawn3D? target, int blocked)
    {
        if (target is null) return;
        // 渇きの台本は回復の書き手を持たない。架空の出発点を他の駒に結ばない。
        Vector3 center = target.FxPoint;
        Color color = RuleColor(SealedLabels.Drought);
        MakeGroundRing(target.Home, new Color(color, 0.4f), 0.85f, 0.35);
        RuleFragments(center, color, 8 + Math.Clamp(blocked / 5, 0, 12), true);
    }

    private void RuleFragments(Vector3 center, Color color, int count, bool wither)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.Tau * i / count;
            Vector3 radial = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var piece = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.065f, Height = 0.13f, RadialSegments = 8, Rings = 4 },
                Position = center + radial * (wither ? 0.8f : 0.15f),
                MaterialOverride = MakeMaterial(color, true, true),
            };
            _fxRoot.AddChild(piece);
            var tween = piece.CreateTween();
            if (wither)
                tween.TweenProperty(piece, "position", center + radial * 0.38f + Vector3.Up * 0.12f, 0.14);
            tween.TweenProperty(piece, "position", center + radial * 1.0f - Vector3.Up * 0.55f, 0.36);
            tween.Parallel().TweenProperty(piece, "scale", Vector3.One * 0.05f, 0.36);
            tween.Parallel().TweenProperty(piece.MaterialOverride, "albedo_color", new Color(0.32f, 0.28f, 0.15f, 0), 0.36);
            tween.Finished += piece.QueueFree;
        }
    }

    public void HealingLight(BattlePawn3D? source, BattlePawn3D? target, int amount)
    {
        if (target is null || amount <= 0) return;
        target.ShowHealingDrops(amount);
        Color color = Color.FromHtml("#bdefff");
        if (source is not null && source != target)
        {
            Vector3 start = source.FxPoint;
            Vector3 end = target.FxPoint;
            float radius = 0.07f + Math.Clamp(amount / 120f, 0, 0.18f);
            var light = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = radius, Height = radius * 2 },
                Position = start,
                MaterialOverride = MakeMaterial(color, true, true),
            };
            _fxRoot.AddChild(light);
            MakeBeam(start, end, new Color(color, 0.4f), radius * 0.55f, 0.32);
            var tween = light.CreateTween();
            tween.TweenProperty(light, "position", end, 0.22);
            tween.TweenProperty(light, "scale", Vector3.One * 2.4f, 0.14);
            tween.Parallel().TweenProperty(light, "transparency", 1f, 0.14);
            tween.Finished += light.QueueFree;
        }
    }
}
