using Godot;
using System;

// 回転は画面内の最短補間にせず、腰の周りの一周を保持する。奥の半周は原画の深度で隠れる。
public partial class RicaSpinTrail3D : MeshInstance3D
{
    private readonly ImmediateMesh _mesh = new();
    private ShaderMaterial _material = null!, _headMaterial = null!;
    private MeshInstance3D _head = null!;
    private string? _clip;
    private bool _active;
    private double _ended;
    private float _position, _angle, _startAngle, _radius, _strength, _opacity=1;
    private Vector3 _center, _right, _far;
    private Basis _camera;
    internal float SweepRadians { get; private set; }
    internal int FrontSegments { get; private set; }
    internal int RearSegments { get; private set; }

    internal void Start()
    {
        TopLevel = true; GlobalTransform = Transform3D.Identity;
        Mesh = _mesh; CastShadow = ShadowCastingSetting.Off;
        _material = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded,blend_add,cull_disabled,depth_draw_never;
uniform vec4 tint:source_color;
uniform float opacity=1.0;
void fragment(){
 float core=exp(-pow((UV.x-0.88)*28.0,2.0));
 float edge=exp(-pow((UV.x-0.97)*75.0,2.0))*0.50;
 float veil=exp(-pow((UV.x-0.72)*6.0,2.0))*0.30;
 float fine=exp(-abs(UV.x-0.45)*110.0)*0.22;
 vec3 rim=mix(pow(tint.rgb,vec3(2.2)),vec3(0.03,0.45,1.0),0.45);
 ALBEDO=mix(rim,vec3(0.96,0.99,1.0),core);
 EMISSION=rim*(veil+fine)*1.8+vec3(0.94,0.99,1.0)*(core*2.6+edge);
 ALPHA=clamp(core+edge+veil+fine,0.0,1.0)*UV.y*opacity;
}" } };
        MaterialOverride = _material;
        _headMaterial = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded,blend_add,cull_disabled,depth_draw_never;
uniform float opacity=1.0;
void fragment(){
 vec2 p=(UV-0.5)*2.0;
 float hot=exp(-dot(p,p)*24.0);
 float cross=(exp(-abs(p.x)*60.0)+exp(-abs(p.y)*60.0))*exp(-length(p)*5.0);
 ALBEDO=vec3(0.92,0.98,1.0);EMISSION=ALBEDO*3.0;
 ALPHA=clamp(hot+cross,0.0,1.0)*opacity;
}" } };
        _head = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One },
            MaterialOverride = _headMaterial, CastShadow = ShadowCastingSetting.Off, TopLevel = true };
        AddChild(_head); Clear();
    }

    internal void Clear()
    {
        _clip = null; _active = false; _position = 0;
        SweepRadians = 0; FrontSegments = RearSegments = 0;
        _mesh.ClearSurfaces(); Hide(); _head?.Hide();
    }

    internal void Feed(double age, bool paused, bool active, string? clip, float position, float turn,
        Vector3 center, Basis camera, bool mirror, int stage, Color tint, float opacity=1,float startTurn=0)
    {
        if (paused) return;
        bool spinning = active && clip is "rica_crescent" or "rica_sweep";
        if (active && !spinning) { Clear(); return; }
        if (spinning)
        {
            if (_clip != clip || position < _position) Clear();
            _clip = clip; _active = true; _position = position;
            _angle = Mathf.DegToRad(turn); _center = center; _camera = camera;
            _startAngle=Mathf.DegToRad(startTurn);
            _right = camera.X * (mirror ? -1 : 1);
            _far = -new Vector3(camera.Z.X,0,camera.Z.Z).Normalized()*.65f;
            _radius = stage >= 3 ? 2.65f : 2.05f;
            _strength = stage >= 3 ? 1.0f : .92f;
            _opacity=Math.Clamp(opacity,0,1);
        }
        else if (_active)
        {
            // 倍速で最後の描画コマを飛び越しても、完走した角度で軌道を閉じる。
            if(clip==_clip)_angle=Math.Max(_angle,Mathf.DegToRad(turn));
            _active = false; _ended = age;
        }
        if (_clip is null) return;
        float fade = _active ? 1 : Math.Max(0,1-(float)((age-_ended)/.22));
        if (fade <= 0) { Clear(); return; }
        SweepRadians = Math.Clamp(_angle-_startAngle,0,Mathf.Tau);
        float start = Math.Max(_startAngle,_angle-Mathf.Tau);
        int segments = Math.Max(1,(int)MathF.Ceiling((_angle-start)/.045f));
        _mesh.ClearSurfaces(); Visible = _angle-_startAngle > .035f && _opacity > .001f;
        _head.Visible = Visible;
        FrontSegments = RearSegments = 0;
        if (!Visible) return;
        _material.SetShaderParameter("tint",tint);
        _material.SetShaderParameter("opacity",fade*_strength*_opacity);
        _headMaterial.SetShaderParameter("opacity",fade*_opacity);
        Vector3 Point(float a,float radius) => _center+(_right*Mathf.Cos(a)+_far*Mathf.Sin(a))*radius;
        void Vertex(float a,float radial)
        {
            float behind = _angle-a;
            // 一周の古い側も薄く残し、手前・奥の両側を同時に読めるようにする。
            float wake = .30f+.70f*Mathf.Pow(Math.Max(0,1-behind/Mathf.Tau),1.15f);
            float depth = Mathf.Sin(a)>0 ? .90f : 1;
            _mesh.SurfaceSetUV(new Vector2(radial,wake*depth));
            _mesh.SurfaceAddVertex(Point(a,_radius*Mathf.Lerp(.75f,1.035f,radial)));
        }
        _mesh.SurfaceBegin(Godot.Mesh.PrimitiveType.Triangles);
        for (int i=0;i<segments;i++)
        {
            float a=Mathf.Lerp(start,_angle,i/(float)segments),b=Mathf.Lerp(start,_angle,(i+1)/(float)segments);
            if(Mathf.Sin((a+b)*.5f)>0)RearSegments++;else FrontSegments++;
            Vertex(a,0);Vertex(a,1);Vertex(b,1);
            Vertex(a,0);Vertex(b,1);Vertex(b,0);
        }
        _mesh.SurfaceEnd();
        float size=stage>=3?.62f:.48f;
        _head.GlobalTransform=new Transform3D(new Basis(_camera.X*size,_camera.Y*size,_camera.Z),
            Point(_angle,_radius*.999f)+_camera.Z*.008f);
    }
}
