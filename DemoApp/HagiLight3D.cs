using Godot;
using BattleCore;
using System;

// 円弧はコードで描く。光粒は購入済み EffectBlocks の sparkle を共有し、自動ループのシーンは使わない。
public partial class HagiLight3D : Node3D
{
    private static Shader? _arcShader;
    private static Texture2D? _sparkle;
    internal static Texture2D Sparkle => _sparkle ??= ResourceLoader.Exists("res://PolyBlocks/EffectBlocks/source_files/textures/sparkle.png")
        ? GD.Load<Texture2D>("res://PolyBlocks/EffectBlocks/source_files/textures/sparkle.png")
        : GD.Load<Texture2D>("res://assets/fx/death_light.svg");
    private Func<(bool Paused, double Speed)> _clock = () => (false, 1);
    private ShaderMaterial? _material;
    private MeshInstance3D? _arc;
    private Sprite3D[] _particles = Array.Empty<Sprite3D>();
    private Vector3 _from, _to;
    private Func<Vector3>? _destination;
    private Color _particleTint = new(1, .72f, .57f);
    private bool _flow;
    private bool _large;
    private double _duration;
    private Sprite3D? _ghost;
    private float _ghostAlpha;
    internal double Age { get; private set; }
    // 墓守の青白・紫と重ならない、敵の赤／傷の朱／味方の淡い金。
    internal static Color SoulTint(int origin) => origin switch
    {
        ReapLabels.SoulAlly => new Color("ffe2aa"),
        ReapLabels.SoulDeath => new Color("ff374b"),
        ReapLabels.SoulMark => new Color("ffad7a"),
        _ => new Color("ff7959"),
    };

    internal void Ghost(Sprite3D portrait, Func<(bool, double)> clock, float alpha, Vector3 offset = default)
    {
        _clock = clock; _duration = .30; _ghostAlpha = alpha;
        _ghost = new Sprite3D { Texture = portrait.Texture, PixelSize = portrait.PixelSize, FlipH = portrait.FlipH,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Shaded = false, Modulate = new Color(1,.08f,.04f,alpha) };
        AddChild(_ghost); _ghost.GlobalTransform = portrait.GlobalTransform;
        _ghost.GlobalPosition += offset;
    }

    internal void Arc(Vector3 center, float width, bool reap, bool reverse, Func<(bool, double)> clock, Camera3D camera)
    {
        _clock = clock; _duration = reap ? .38 : .20; GlobalPosition = center;
        _arcShader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded,blend_add,cull_disabled,depth_draw_never,depth_test_disabled;
uniform float age=0.0;
uniform float heavy=0.0;
void fragment(){
 vec2 p=(UV-0.5)*2.0;
 float curve=p.y+0.55*(1.0-p.x*p.x);
 float edge=exp(-abs(curve)*(100.0-heavy*45.0));
 float wake=exp(-abs(curve+0.08)*22.0)*0.2*heavy;
 float taper=pow(max(0.0,1.0-p.x*p.x),0.45);
 float head=mix(-1.2,1.2,smoothstep(0.0,0.28,age));
 float sweep=1.0-smoothstep(head-0.18,head,p.x);
 ALBEDO=vec3(0.94,0.96,1.0);EMISSION=vec3(0.92,0.95,1.0)*1.7;
 ALPHA=(edge+wake)*taper*sweep*sin(clamp(age,0.0,1.0)*3.14159);
}" };
        _material = new ShaderMaterial { Shader = _arcShader, RenderPriority = 12 };
        _material.SetShaderParameter("heavy", reap ? 1f : 0f);
        _arc = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(width, reap ? 1.55f : .48f) },
            MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 10 };
        AddChild(_arc); _arc.GlobalBasis = camera.GlobalBasis;
        if (reverse) _arc.RotateObjectLocal(Vector3.Forward, Mathf.Pi);
    }
    internal void Particles(Vector3 from, Vector3 to, bool flow, bool large, Func<(bool, double)> clock,
        Color? tint = null, Func<Vector3>? destination = null)
    {
        _particleTint = tint ?? new Color(1, .72f, .57f); _destination = destination;
        _from = from; _to = to; _flow = flow; _large = large; _clock = clock; _duration = flow ? .64 : large ? .78 : .48;
        _particles = new Sprite3D[large ? flow ? 24 : 28 : 8];
        for (int k = 0; k < _particles.Length; k++)
        {
            var sprite = new Sprite3D { Texture = Sparkle, PixelSize = .001f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Shaded = false, NoDepthTest = true, Modulate = new Color(.93f, .95f, 1, 0), RenderPriority = 13 };
            _particles[k] = sprite; AddChild(sprite);
        }
    }
    public override void _Process(double delta)
    {
        var (paused, speed) = _clock(); if (paused) return;
        Age += delta * speed; float t = (float)Math.Min(1, Age / _duration);
        if (_ghost is not null) _ghost.Modulate = new Color(1,.08f,.04f,_ghostAlpha*(1-t));
        _material?.SetShaderParameter("age", t);
        if (_arc is not null) _arc.Scale = new Vector3(.92f + t * .14f, 1, 1);
        for (int k = 0; k < _particles.Length; k++)
        {
            float angle = k * 2.39996f;
            float seed = (k % 7 + 1) / 7f;
            Vector3 scatter = new(Mathf.Cos(angle) * .70f, (seed - .3f) * 1.3f, Mathf.Sin(angle) * .35f);
            var particle = _particles[k];
            particle.GlobalPosition = _flow ? _from.Lerp(_destination?.Invoke() ?? _to, t * t) + scatter * Mathf.Sin(t * Mathf.Pi)
                : _large ? _from + new Vector3(Mathf.Cos(angle+t)*(.22f+t*.45f),
                    (seed-.45f)*1.2f+t*t*1.1f,Mathf.Sin(angle+t)*(.16f+t*.3f))
                : _from + scatter * (.2f + t * 1.8f) + Vector3.Up * t * .6f;
            float scale = (.10f + seed * .09f) / Math.Max(.001f, Sparkle.GetWidth() * .001f);
            particle.Scale = Vector3.One * scale * (1 - t * .55f);
            particle.Modulate = new Color(_particleTint, Mathf.Sin(t * Mathf.Pi));
        }
        if (Age >= _duration) QueueFree();
    }
}
