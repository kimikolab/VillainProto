using Godot;
using System;
using System.Collections.Generic;

public partial class BattlefieldView3D
{
    private readonly Dictionary<int, List<Label3D>> _tickNumbers = new();

    public void ShowTick(BattlePawn3D? pawn, bool burn, bool inverse, bool last, double seconds)
    {
        if (pawn is null) return;
        var material = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never;
uniform float progress = 0.0;
uniform bool burn = false;
uniform bool inverse = false;
uniform float strength = 1.0;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    vec2 p = (UV - vec2(0.5)) * vec2(1.0, 1.1);
    float t = progress;
    float flip = inverse ? smoothstep(0.32, 0.57, t) : 0.0;
    float particles = 0.0;
    for (int i = 0; i < 10; i++) {
        float k = float(i);
        float x = sin(k * 5.7) * 0.30;
        float y = 0.34 - t * (0.40 + fract(k * 0.37) * 0.28);
        x += sin(t * 8.0 + k) * 0.035;
        vec2 q = (p - vec2(x, y)) * vec2(1.0, burn ? 0.45 : 1.0);
        float d = length(q);
        float r = burn ? 0.035 : 0.025 + fract(k * 0.31) * 0.018;
        float bubble = 1.0 - smoothstep(r * 0.55, r, d);
        particles = max(particles, bubble);
    }
    float ring = 1.0 - smoothstep(0.014, 0.045, abs(length(p) - (0.07 + t * 0.40)));
    float mist = exp(-dot(p * vec2(3.5, 4.2), p * vec2(3.5, 4.2)));
    float veil = (0.55 + 0.45 * sin(p.x * 31.0 + p.y * 23.0 - t * 12.0));
    vec3 base = burn ? vec3(1.0, 0.16, 0.035) : vec3(0.62, 0.13, 0.86);
    // 同じ泡・火の粉そのものが紅を経て緑白金に変わる。
    vec3 tint = mix(base, vec3(0.95, 0.12, 0.38), inverse ? sin(flip * 3.14159) * 0.6 : 0.0);
    tint = mix(tint, vec3(0.57, 1.0, 0.70), flip);
    tint = mix(tint, vec3(1.0, 0.96, 0.72), flip * particles * 0.65);
    float fade = smoothstep(0.0, 0.1, t) * (1.0 - smoothstep(0.65, 1.0, t));
    ALBEDO = tint;
    EMISSION = tint * strength;
    ALPHA = (particles * 0.85 + ring * 0.60 + mist * veil * 0.32) * fade;
}" } };
        material.SetShaderParameter("burn", burn);
        material.SetShaderParameter("inverse", inverse);
        material.SetShaderParameter("strength", last ? 1.8f : 0.9f);
        var fx = new MeshInstance3D
        {
            Name = inverse ? "InverseTick" : "ConcentratedTick",
            Mesh = new QuadMesh { Size = new Vector2(2.8f, 2.8f) * (last ? 1.13f : 1f) },
            Position = _fxRoot.ToLocal(pawn.FxPoint),
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _fxRoot.AddChild(fx);
        var tween = fx.CreateTween();
        tween.TweenMethod(Callable.From<float>(p => material.SetShaderParameter("progress", p)),
            0f, 1f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(fx.QueueFree));
    }

    // 通常ポップアップの合算・2件制限から独立させる。新しい数字が下に入り、前の数字を押し上げる。
    public void TickNumber(BattlePawn3D? pawn, int amount, bool heal, int ordinal, bool last, bool burn = false)
    {
        if (pawn is null) return;
        if (!_tickNumbers.TryGetValue(pawn.InstanceId, out var numbers))
            _tickNumbers[pawn.InstanceId] = numbers = new();
        numbers.RemoveAll(x => !LivePopup(x));
        foreach (var prior in numbers) prior.Position += Vector3.Up * 0.32f;
        // 多数の印でも画面を埋めない。1発ごとの出現は省かず、古いものから消す。
        while (numbers.Count >= 6)
        {
            numbers[0].Hide();
            numbers[0].QueueFree();
            numbers.RemoveAt(0);
        }
        var label = new Label3D
        {
            Text = (heal ? "＋" : "−") + amount,
            Position = _fxRoot.ToLocal(pawn.FxPoint + _camera.GlobalBasis.X * (1.85f + ordinal % 2 * 0.12f)
                + Vector3.Up * 0.60f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = last ? 38 : 31,
            PixelSize = 0.010f,
            Modulate = heal ? UiKit.Heal : burn ? new Color("ff8d45") : UiKit.Poison,
            OutlineSize = 10,
            OutlineModulate = new Color(0.01f, 0.015f, 0.01f),
            NoDepthTest = true,
            RenderPriority = 10,
        };
        _fxRoot.AddChild(label);
        numbers.Add(label);
        var tween = label.CreateTween();
        tween.TweenInterval(0.95);
        tween.TweenProperty(label, "modulate:a", 0f, 0.40);
        tween.TweenCallback(Callable.From(label.QueueFree));
    }
}
