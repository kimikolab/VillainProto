using Godot;
using System;

// 自作の燐光・魂の糸・横薙ぎ。各発動の時計で進め、購入素材は光粒のテクスチャだけを使う。
public partial class DeathFx3D : Node3D
{
    internal enum Shape { Flow, Burst, Slash, Rise, Song, Ash }
    private static Shader? _shader;
    private ShaderMaterial[] _materials = Array.Empty<ShaderMaterial>();
    private MeshInstance3D[] _parts = Array.Empty<MeshInstance3D>();
    private Func<(bool Paused, double Speed)> _clock = () => (false, 1);
    private Vector3 _from, _to;
    private Shape _shape;
    private float _strength;
    private double _seconds;
    internal double Age { get; private set; }
    internal void Start(Vector3 from, Vector3 to, Color tint, Shape shape, float strength,
        Func<(bool Paused, double Speed)> clock)
    {
        _from = from; _to = to; _shape = shape; _strength = strength; _clock = clock;
        _seconds = shape == Shape.Song ? 0.80 : shape == Shape.Slash ? 0.38 : 0.65;
        _shader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded,blend_add,cull_disabled,depth_draw_never;
uniform vec4 tint : source_color;
uniform float progress=0.0;
uniform bool slash=false;
uniform bool ash=false;
void vertex(){MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0]*length(MODEL_MATRIX[0].xyz),INV_VIEW_MATRIX[1]*length(MODEL_MATRIX[1].xyz),INV_VIEW_MATRIX[2],MODEL_MATRIX[3]);}
void fragment(){
 vec2 p=(UV-0.5)*2.0;
 float glow=exp(-dot(p,p)*6.0);
 if(slash){float arc=p.y+0.44*(1.0-p.x*p.x);float taper=max(0.0,1.0-p.x*p.x);glow=(exp(-abs(arc)*90.0)+0.23*exp(-abs(arc)*15.0))*taper;}
 if(ash){glow=(1.0-smoothstep(0.3,0.8,max(abs(p.x),abs(p.y))))*0.45;}
 ALBEDO=mix(tint.rgb,vec3(1.0),glow*0.5);EMISSION=tint.rgb*glow*0.75;
 ALPHA=glow*sin(clamp(progress,0.0,1.0)*3.14159)*tint.a;
}" };
        int count = shape == Shape.Slash ? strength >= 2.4f ? 3 : strength >= 1.8f ? 2 : 1 : shape == Shape.Song ? 15 : 12;
        _parts = new MeshInstance3D[count]; _materials = new ShaderMaterial[count];
        for (int k = 0; k < count; k++)
        {
            var mat = new ShaderMaterial { Shader = _shader, RenderPriority = 6 };
            mat.SetShaderParameter("tint", tint); mat.SetShaderParameter("slash", shape == Shape.Slash);
            mat.SetShaderParameter("ash", shape == Shape.Ash);
            _materials[k] = mat;
            var mesh = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = mat,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 5 };
            _parts[k] = mesh; AddChild(mesh);
        }
        Draw(0);
    }
    public override void _Process(double delta)
    {
        var (paused, speed) = _clock(); if (paused) return;
        Age += delta * speed; Draw((float)Math.Min(1, Age / _seconds));
        if (Age >= _seconds) QueueFree();
    }
    private void Draw(float p)
    {
        Vector3 side = (_to - _from).Cross(Vector3.Up).Normalized();
        for (int k = 0; k < _parts.Length; k++)
        {
            float t = Mathf.Clamp((p - k * 0.012f) / 0.82f, 0, 1), a = k * 2.39996f;
            Vector3 at = _from.Lerp(_to, t * t);
            float size = 0.11f + k % 3 * 0.035f;
            if (_shape is Shape.Flow or Shape.Song)
                at += side * Mathf.Sin(t * Mathf.Pi) * Mathf.Sin(t * 8 + k) * 0.22f
                    + Vector3.Up * (Mathf.Sin(t * Mathf.Pi) * 0.55f + (_shape == Shape.Song ? k % 5 * 0.09f : 0));
            else if (_shape == Shape.Slash)
            {
                at = _to + Vector3.Up * (k - 1) * 0.12f;
                _parts[k].Scale = new Vector3((1.9f + _strength * 0.75f) * (0.7f + t * 0.6f), 1.05f + _strength * 0.16f, 1);
            }
            else
            {
                float radius = (_shape == Shape.Rise ? 0.25f : 0.65f) * Mathf.Sin(t * Mathf.Pi) * _strength;
                at = _from + new Vector3(Mathf.Cos(a) * radius, t * (_shape == Shape.Ash ? -0.65f : 1.3f), Mathf.Sin(a) * radius);
            }
            if (_shape != Shape.Slash) _parts[k].Scale = Vector3.One * size;
            _parts[k].GlobalPosition = at; _materials[k].SetShaderParameter("progress", t);
        }
    }
}
