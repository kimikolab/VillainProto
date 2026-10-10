using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private static Shader? _hushAtmosphereShader;
    private Tween? _hushOpeningTween;
    private Node3D? _hushOpeningFx;
    private int[] _hushOpeningSources = Array.Empty<int>();
    internal float HushOpeningProgress { get; private set; } = 1;
    internal bool HushOpeningActive => _hushOpeningTween is not null;
    internal int HushOpeningSourceCount => _hushOpeningSources.Length;

    // 既存の沈黙色を、開戦時だけ保持者の位置から塗り広げる。
    private static Shader HushAtmosphereShader() => _hushAtmosphereShader ??= new Shader { Code = @"
shader_type canvas_item;
uniform float spread = 1.0;
uniform float aspect = 1.7778;
uniform float reach = 2.0;
uniform int source_count = 0;
uniform vec2 sources[18];
void fragment() {
    vec4 c = texture(TEXTURE, UV);
    float d = 100.0;
    for (int i = 0; i < source_count; i++) {
        d = min(d, length((UV - sources[i]) * vec2(aspect, 1.0)));
    }
    float radius = spread * reach;
    float inside = 1.0 - smoothstep(radius - 0.045, radius, d);
    if (spread >= 0.999) inside = 1.0;
    float edge = exp(-abs(d - radius) * 100.0);
    edge *= smoothstep(0.0, 0.07, spread) * (1.0 - smoothstep(0.82, 1.0, spread));
    float gray = dot(c.rgb, vec3(0.299, 0.587, 0.114));
    vec3 muted = mix(c.rgb, vec3(gray) * vec3(0.82, 0.92, 1.06), 0.48);
    vec3 result = mix(c.rgb, muted, inside);
    result = mix(result, vec3(0.70, 0.87, 0.96), edge * 0.48);
    COLOR = vec4(result, c.a);
}" };

    private void BeginHushOpening()
    {
        if (!HushMuted) return;
        _hushOpeningSources = _hushHolders.Where(HushActive).OrderBy(id => id).Take(18).ToArray();
        if (_hushOpeningSources.Length == 0) return;
        _hushOpeningFx = new Node3D { Name = "HushOpening" };
        _fxRoot.AddChild(_hushOpeningFx);
        var tint = new Color("b8deea");
        foreach (int id in _hushOpeningSources)
        {
            var holder = FindPawn(id);
            if (holder is null) continue;
            // 胸元の法具を起点として先に見せる。波の前に短い集光の拍を置く。
            ShockMarkFx.Glow(_hushOpeningFx, holder.FxPoint, tint, 1.8f, .45);
            ShockMarkFx.Ring(_hushOpeningFx, holder.FxPoint, tint, 1.3f, .60);
            // 頭上のひびゲージと重ならない高さに置く。
            Float(holder, "粛・静まれ", tint, true, 4.25f, 1.1f);
        }
        SetHushOpeningProgress(0);
        // 開戦の因果を読む時間なので倍速でも同じ長さ。保持者がいない戦には待ちを足さない。
        _hushOpeningTween = CreateTween();
        _hushOpeningTween.TweenInterval(.28);
        _hushOpeningTween.TweenCallback(Callable.From(() => _attackAudio.PlayShockMark(ShockMarkSound.HushOpening)));
        _hushOpeningTween.TweenMethod(Callable.From<float>(SetHushOpeningProgress), 0f, 1f, 1.05)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _hushOpeningTween.TweenInterval(.16);
        _hushOpeningTween.TweenCallback(Callable.From(() => {
            _hushOpeningTween = null;
            if (IsInstanceValid(_hushOpeningFx)) _hushOpeningFx!.QueueFree();
            _hushOpeningFx = null;
        }));
    }

    private void SetHushOpeningProgress(float progress)
    {
        HushOpeningProgress = progress;
        if (_battleImage.Material is not ShaderMaterial material) return;
        var size = new Vector2(Math.Max(1, _viewport.Size.X), Math.Max(1, _viewport.Size.Y));
        float aspect = size.X / size.Y;
        var centers = _hushOpeningSources.Where(HushActive).Select(id => FindPawn(id)).Where(p => p is not null)
            .Select(p => _camera.UnprojectPosition(p!.FxPoint) / size).ToArray();
        var padded = new Vector2[18];
        centers.CopyTo(padded, 0);
        float reach = .1f;
        // 画角や陣営が変わっても、最遠の画面隅まで波を届かせる。
        if (centers.Length > 0)
            foreach (var corner in new[] { Vector2.Zero, Vector2.One, Vector2.Right, Vector2.Down })
                reach = Math.Max(reach, centers.Min(c => ((corner - c) * new Vector2(aspect, 1)).Length()) + .07f);
        material.SetShaderParameter("sources", padded);
        material.SetShaderParameter("source_count", centers.Length);
        material.SetShaderParameter("aspect", aspect);
        material.SetShaderParameter("reach", reach);
        material.SetShaderParameter("spread", progress);
        if (_hushFilter is not null)
            _hushFilter.CutoffHz = Mathf.Exp(Mathf.Lerp(Mathf.Log(20000), Mathf.Log(1400), progress));
    }

    private void CancelHushOpening()
    {
        _attackAudio.StopHushOpeningSound();
        _hushOpeningTween?.Kill(); _hushOpeningTween = null;
        HushOpeningProgress = 1;
        _hushOpeningSources = Array.Empty<int>();
        if (IsInstanceValid(_hushOpeningFx)) _hushOpeningFx!.QueueFree();
        _hushOpeningFx = null;
    }

    internal async Task WaitForHushOpening()
    {
        int generation = _sealGeneration;
        while (IsInsideTree() && generation == _sealGeneration && HushOpeningActive)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
