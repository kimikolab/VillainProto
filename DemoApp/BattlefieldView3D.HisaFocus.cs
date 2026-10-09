using Godot;
using System;

public partial class BattlefieldView3D
{
    private Control? _commandFocus;
    private Tween? _commandFocusTween;
    internal bool CommandFocusVisible => _commandFocus?.Visible == true;

    private void BeginCommandFocus(BattlePawn3D hisa, BattlePawn3D target, int balls, double seconds)
    {
        EndCommandFocus();
        var material = new ShaderMaterial { Shader = new Shader { Code = @"shader_type canvas_item;
uniform vec2 actor;
uniform vec2 target;
uniform float aspect;
uniform float strength = 0.0;
void fragment() {
    vec2 a = (UV - actor) * vec2(aspect, 1.0);
    vec2 b = (UV - target) * vec2(aspect, 1.0);
    float hole = min(length(a / vec2(0.16, 0.26)), length(b / vec2(0.16, 0.26)));
    float shade = smoothstep(0.7, 1.7, hole) * 0.60;
    float edge = (1.0 - smoothstep(0.0, 0.07, min(UV.y, 1.0 - UV.y))) * 0.32;
    COLOR = vec4(mix(vec3(0.035, 0.018, 0.012), vec3(0.70, 0.37, 0.08), edge), max(shade, edge) * strength);
}" } };
        Vector2 size = _viewport.Size;
        material.SetShaderParameter("actor", _camera.UnprojectPosition(hisa.FxPoint) / size);
        material.SetShaderParameter("target", _camera.UnprojectPosition(target.FxPoint) / size);
        material.SetShaderParameter("aspect", size.X / Math.Max(1, size.Y));
        var focus = _commandFocus = new Control { MouseFilter = MouseFilterEnum.Ignore };
        focus.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(focus);
        var shade = new ColorRect { Material = material, MouseFilter = MouseFilterEnum.Ignore };
        shade.SetAnchorsPreset(LayoutPreset.FullRect); focus.AddChild(shade);
        var title = UiKit.Text($"号 令   ◆ ×{balls}", 28, RallyAmber);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.MouseFilter = MouseFilterEnum.Ignore;
        title.SetAnchorsPreset(LayoutPreset.TopWide); title.Position = new Vector2(0, 94);
        title.AddThemeConstantOverride("outline_size", 8);
        title.AddThemeColorOverride("font_outline_color", Colors.Black);
        focus.AddChild(title);
        _commandFocusTween = focus.CreateTween();
        _commandFocusTween.TweenMethod(Callable.From<float>(v => material.SetShaderParameter("strength", v)), 0f, 1f, seconds * .12);
        _commandFocusTween.TweenInterval(seconds * .62);
        _commandFocusTween.TweenProperty(focus, "modulate:a", 0f, seconds * .26);
        _commandFocusTween.TweenCallback(Callable.From(() => { focus.Hide(); focus.QueueFree(); if (_commandFocus == focus) _commandFocus = null; }));
    }

    private void EndCommandFocus()
    {
        _commandFocusTween?.Kill(); _commandFocusTween = null;
        if (_commandFocus is not null && IsInstanceValid(_commandFocus)) { _commandFocus.Hide(); _commandFocus.QueueFree(); }
        _commandFocus = null;
    }
}
