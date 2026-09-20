using Godot;
using System;
using System.Linq;

public partial class BattlefieldView3D
{
    // 試作。F8 または起動引数で、採用済みの残像だけの版と比較する。
    private bool _superFlashEnabled = !OS.GetCmdlineUserArgs().Contains("--demo-no-super-flash");
    private ColorRect _superFlash = null!;
    private ShaderMaterial _superFlashMaterial = null!;
    private Tween? _superFlashTween;

    private void BuildSuperFlash()
    {
        var shader = new Shader { Code = @"shader_type canvas_item;
uniform vec2 focus = vec2(0.5);
uniform float aspect = 1.6;
uniform float progress = 0.0;
void fragment() {
    vec2 p = UV - focus;
    p.x *= aspect;
    float t = progress;
    float release = 1.0 - smoothstep(0.62, 1.0, t);
    // 発動者の周囲だけ開ける。駒と青い残像を暗幕に埋めない。
    float distance_to_actor = length(p / vec2(0.17, 0.25));
    float shade = smoothstep(0.65, 1.7, distance_to_actor) * 0.88 * release;
    float angle = atan(p.y, p.x);
    // 不揃いな細い線が外から発動者へ流れ込む。二次の移動量で後半ほど速くする。
    float sector = (angle + 3.14159265) / 6.2831853 * 48.0;
    float lane = floor(sector);
    float seed = fract(sin(lane * 127.1 + 17.0) * 43758.5453);
    float center = 0.30 + seed * 0.40;
    float width = mix(0.018, 0.055, seed);
    float aa = max(fwidth(sector), 0.008);
    float ray = 1.0 - smoothstep(width, width + aa, abs(fract(sector) - center));
    float travel = t * 0.45 + t * t * 2.1;
    float along = fract(length(p) * 1.25 + travel + seed);
    // 明るい先端は内側、薄い尾は外側。頭だけが点滅するのではなく線分ごと移動する。
    float streak = smoothstep(0.0, 0.025, along) * (1.0 - smoothstep(0.06, 0.36, along));
    float rays = ray * streak * smoothstep(0.95, 1.8, distance_to_actor);
    rays *= smoothstep(0.0, 0.06, t) * (1.0 - smoothstep(0.65, 0.95, t));
    // 一度だけ走る青い横閃光。画面全体を白く点滅させない。
    float slash = (1.0 - smoothstep(0.002, 0.010, abs(p.y + p.x * 0.06)));
    slash *= (1.0 - smoothstep(0.10, 0.30, t)) * smoothstep(0.0, 0.025, t);
    float blue = max(slash * 0.85, rays * 0.90);
    vec3 tint = mix(vec3(0.015, 0.045, 0.13), vec3(0.30, 0.79, 1.0), blue);
    COLOR = vec4(tint, max(shade, blue) * release);
}" };
        _superFlashMaterial = new ShaderMaterial { Shader = shader };
        _superFlash = new ColorRect
        {
            Material = _superFlashMaterial, MouseFilter = MouseFilterEnum.Ignore, Visible = false,
        };
        _superFlash.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_superFlash);
    }

    private void BeginSuperFlash(BattlePawn3D actor, double duration)
    {
        ResetSuperFlash();
        if (!_superFlashEnabled) return;
        Vector2 point = _camera.UnprojectPosition(actor.FxPoint);
        Vector2 size = _viewport.Size;
        _superFlashMaterial.SetShaderParameter("focus", point / size);
        _superFlashMaterial.SetShaderParameter("aspect", size.X / Math.Max(1, size.Y));
        _superFlashMaterial.SetShaderParameter("progress", 0.0f);
        _superFlash.Visible = true;
        _superFlashTween = _superFlash.CreateTween();
        _superFlashTween.TweenMethod(Callable.From<float>(t =>
            _superFlashMaterial.SetShaderParameter("progress", t)), 0.0f, 1.0f, duration);
        _superFlashTween.Finished += () => _superFlash.Visible = false;
    }

    private void ResetSuperFlash()
    {
        _superFlashTween?.Kill();
        if (_superFlash is not null) _superFlash.Visible = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsVisibleInTree() || @event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F8 }) return;
        _superFlashEnabled = !_superFlashEnabled;
        ResetSuperFlash();
        _bonusAttackShade.Color = new Color(0.005f, 0.012f, 0.018f, _superFlashEnabled ? 0 : 0.36f);
        GD.Print($"追加攻撃の暗転試作: {(_superFlashEnabled ? "ON" : "OFF")}");
        GetViewport().SetInputAsHandled();
    }
}
