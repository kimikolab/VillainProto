using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    private readonly HashSet<int> _inverseHolders = new();
    internal int ConcentratePlays, ThickenPlays, InvertedHealPlays, SipPlays, KindlePlays, TaintPlays;
    private static readonly Color BeniRed = new("db3263");
    private static readonly Color MioWater = new("438a76");

    private void RegisterInverse(DemoOpening opening)
    {
        if (opening.Traits?.Contains(TraitId.Inverse) == true) _inverseHolders.Add(opening.InstanceId);
    }
    public override void _Process(double delta) => RefreshInverseBarriers();

    // 既存の隣接表に現在の表示席を当てるだけ。HPや状態の判定・書き換えはしない。
    public void RefreshInverseBarriers()
    {
        var holders = _pawns.Values.Where(p => p.Hp > 0 && _inverseHolders.Contains(p.InstanceId)).ToArray();
        foreach (var pawn in _pawns.Values)
            pawn.SetInverseBarrier(holders.Any(h => h.Team == pawn.Team && (h == pawn
                || FormationRules.AreAdjacent(h.Slot, pawn.Slot))));
    }

    public void ShowConcentrate(BattlePawn3D? source, BattlePawn3D? target, bool center, double speed)
    {
        if (target is null) return;
        ConcentratePlays++;
        double duration = 0.42 / Math.Max(0.1, speed);
        if (center)
            FlowDrops(target.FxPoint + Vector3.Up * 2.5f, target.FxPoint, MioWater, duration, false);
        else if (source is not null)
        {
            // 同じ渦の輪を経路上に広げる。漏れの始点はミオ、周りの始点は台本の中心。
            for (int k = 0; k < 4; k++)
                WaterVortex(source.FxPoint.Lerp(target.FxPoint, k / 3f), MioWater, duration,
                    delay: k * duration * 0.12, radius: 0.42f);
        }
        WaterVortex(target.FxPoint, new Color("34594f"), duration * 1.4, radius: 0.85f,
            delay: duration * 0.30);
    }

    public void ShowThicken(BattlePawn3D? target, double speed)
    {
        if (target is null) return;
        ThickenPlays++;
        FlowDrops(target.FxPoint + Vector3.Up * 2.7f, target.FxPoint, MioWater,
            0.36 / Math.Max(0.1, speed), false);
    }

    public void ShowBeniGift(BattlePawn3D? source, IReadOnlyList<BattlePawn3D> targets, bool fire, double speed)
    {
        if (source is null) return;
        if (fire) KindlePlays++; else TaintPlays++;
        foreach (var target in targets)
            FlowDrops(source.FxPoint, target.FxPoint, fire ? BeniRed : new Color("8c376f"),
                0.48 / Math.Max(0.1, speed), fire, sag: !fire);
    }

    public void ShowInverseSip(BattlePawn3D? overflow, BattlePawn3D? beni, double speed, double seconds = 0.36)
    {
        if (overflow is null || beni is null) return;
        SipPlays++;
        FlowDrops(overflow.FxPoint, beni.FxPoint, BeniRed, seconds / Math.Max(0.1, speed), false, pull: true);
    }

    public void ShowInvertedHeal(BattlePawn3D? healer, BattlePawn3D? target, double speed)
    {
        if (target is null) return;
        InvertedHealPlays++;
        double duration = 0.48 / Math.Max(0.1, speed);
        // 通常回復と同色の光が届いてから濁る。回復の粒を出す既存口はHPが癒えたように見えるので使わない。
        _attackAudio.PlayHeal(healer?.UnitId, healer == target);
        FlowDrops(healer?.FxPoint ?? target.FxPoint + Vector3.Up * 1.5f, target.FxPoint,
            new Color("bdefff"), duration * 0.55, false);
        WaterVortex(target.FxPoint, new Color("493052"), duration * 0.65,
            delay: duration * 0.5, radius: 0.9f);
    }

    private void FlowDrops(Vector3 from, Vector3 to, Color tint, double seconds, bool fire,
        bool sag = false, bool pull = false)
    {
        var group = new Node3D();
        _fxRoot.AddChild(group);
        from = _fxRoot.ToLocal(from); to = _fxRoot.ToLocal(to);
        var material = MakeMaterial(tint, true, true);
        var drops = new MeshInstance3D[9];
        for (int i = 0; i < drops.Length; i++)
        {
            drops[i] = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = fire ? 0.045f : 0.085f, Height = fire ? 0.17f : 0.24f,
                    RadialSegments = 8, Rings = 4 }, MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            group.AddChild(drops[i]);
        }
        var tween = group.CreateTween();
        tween.TweenMethod(Callable.From<float>(p =>
        {
            for (int i = 0; i < drops.Length; i++)
            {
                float t = Mathf.Clamp((p - i * 0.028f) / 0.77f, 0, 1);
                float arc = Mathf.Sin(t * Mathf.Pi);
                float along = pull ? t * t : t;
                drops[i].Position = from.Lerp(to, along) + Vector3.Up * arc * (sag ? -0.55f : 0.12f + i * 0.025f)
                    + Vector3.Right * Mathf.Sin(i * 2.4f + t * 5) * arc * 0.16f;
                drops[i].Scale = Vector3.One * (pull ? 1 - t * 0.75f : 1);
                drops[i].Visible = t > 0 && t < 1;
            }
        }), 0f, 1f, Math.Max(0.01, seconds));
        tween.TweenCallback(Callable.From(group.QueueFree));
    }

    private void WaterVortex(Vector3 position, Color tint, double seconds, double delay = 0, float radius = 0.7f)
    {
        var material = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;
uniform float progress = 0.0;
uniform vec4 tint : source_color;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    vec2 p = UV - vec2(0.5);
    float d = length(p);
    float a = atan(p.y,p.x) + d * 24.0 - progress * 8.0;
    float curl = pow(0.5 + 0.5 * cos(a * 3.0), 4.0);
    float edge = 1.0 - smoothstep(0.28, 0.48, d);
    float wave = 1.0 - smoothstep(0.015, 0.04, abs(d - progress * 0.47));
    float fade = sin(progress * 3.14159);
    ALBEDO = mix(tint.rgb * 0.22, tint.rgb, curl);
    EMISSION = tint.rgb * wave * 0.30;
    ALPHA = (edge * (0.55 + 0.4 * curl) + wave * 0.3) * fade;
}" } };
        material.SetShaderParameter("tint", tint);
        var fx = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * radius * 2 },
            Position = _fxRoot.ToLocal(position), MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _fxRoot.AddChild(fx);
        var tween = fx.CreateTween();
        if (delay > 0) tween.TweenInterval(delay);
        tween.TweenMethod(Callable.From<float>(p => material.SetShaderParameter("progress", p)),
            0f, 1f, Math.Max(0.01, seconds));
        tween.TweenCallback(Callable.From(fx.QueueFree));
    }
}
