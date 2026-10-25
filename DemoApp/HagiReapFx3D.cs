using Godot;
using System;

// 一振りに一つの時計。予兆から斬光・光片の余韻まで、停止と速度変更を同じ時計で扱う。
public partial class HagiReapFx3D : Node3D
{
    private static Shader? _arcShader, _glowShader, _auraShader;
    private Func<(bool Paused, double Speed)> _clock = () => (false, 1);
    private Func<Vector3> _blade = () => Vector3.Zero;
    private Func<Vector3> _orbit = () => Vector3.Zero;
    private Camera3D _camera = null!;
    private Vector3 _center, _strikeCenter;
    private float _radius, _power;
    private bool _reverse;
    private ShaderMaterial _main = null!, _echo = null!, _flash = null!, _shade = null!, _shards = null!;
    private MeshInstance3D _arcMesh = null!, _echoMesh = null!, _flashMesh = null!, _shadeMesh = null!;
    private ShaderMaterial _aura = null!;
    private MeshInstance3D _auraMesh = null!;
    private MeshInstance3D[] _flakes = Array.Empty<MeshInstance3D>();
    private Sprite3D[] _sparks = Array.Empty<Sprite3D>();
    private Sprite3D _star = null!;
    internal double Age { get; private set; }
    internal bool Released { get; private set; }
    internal bool Heavy { get; private set; }
    internal Vector3 SweepCenter => _strikeCenter;
    internal float SweepRadius => _radius;
    internal Vector3 SweepPoint(float phase)
    {
        float angle=SweepAngle(phase);
        return _arcMesh.ToGlobal(new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0));
    }

    internal void Start(Vector3 center, float width, bool reverse, Camera3D camera,
        Func<Vector3> blade, Func<Vector3> orbit, Func<(bool Paused, double Speed)> clock, bool heavy = true)
    {
        _center = center; _radius = Math.Clamp(width * .5f, 1.65f, 2.8f);
        _reverse = reverse; _camera = camera; _blade = blade; _orbit = orbit; _clock = clock;
        Heavy = heavy; _power = heavy ? 1f : .48f;
        _arcShader ??= new Shader { Code = Godot.FileAccess.GetFileAsString("res://assets/fx/hagi_reap_arc.gdshader") };
        _glowShader ??= new Shader { Code = Godot.FileAccess.GetFileAsString("res://assets/fx/hagi_reap_glow.gdshader") };
        _auraShader ??= new Shader { Code = Godot.FileAccess.GetFileAsString("res://assets/fx/hagi_reap_aura.gdshader") };
        _main = Material(_arcShader, 12); _echo = Material(_arcShader, 10); _echo.SetShaderParameter("echo",1f);
        _flash = Material(_glowShader, 14);
        _shade = Material(_glowShader, -12); _shade.SetShaderParameter("layer",1);
        _shards = Material(_glowShader, 15); _shards.SetShaderParameter("layer",2);
        _arcMesh = Ribbon(_main, .60f);
        _aura = Material(_auraShader, 11);
        _aura.SetShaderParameter("slash_width", _radius * Mathf.Tau);
        _auraMesh = Ribbon(_aura, .90f);
        _echoMesh = Ribbon(_echo, .60f);
        _flashMesh = Quad(_flash,new(width+1f,1.4f));
        _shadeMesh = Quad(_shade,new(_radius*2.3f,2.5f));
        _star = Sprite(HagiLight3D.Sparkle,15);
        _sparks = new Sprite3D[heavy ? 26 : 12];
        for (int i=0;i<_sparks.Length;i++) _sparks[i] = Sprite(HagiLight3D.Sparkle,14);
        _flakes = new MeshInstance3D[heavy ? 12 : 5];
        for (int i=0;i<_flakes.Length;i++) _flakes[i] = Quad(_shards,Vector2.One);
        foreach (var material in new[] { _main, _echo, _aura, _flash, _shade, _shards })
            material.SetShaderParameter("power", _power);
        Draw();
    }
    internal void Release()
    {
        if (Released) return;
        _strikeCenter = _orbit();
        Released = true; Age = 0; Draw();
    }
    // 地面と平行な円弧。人物の手前と奥を通り、命中した位置に余韻を残す。
    private MeshInstance3D Ribbon(ShaderMaterial material, float width)
    {
        var mesh = new ImmediateMesh(); mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        const int segments = 128;
        for (int i=0;i<=segments;i++)
        {
            float u=i/(float)segments, angle=SweepAngle(u);
            for (int edge=0;edge<2;edge++)
            {
                float radius=1+(edge-.5f)*width;
                mesh.SurfaceSetUV(new Vector2(u,edge));
                mesh.SurfaceAddVertex(new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0));
            }
        }
        mesh.SurfaceEnd();
        var node=new MeshInstance3D { Mesh=mesh, MaterialOverride=material, ExtraCullMargin=12,
            CastShadow=GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(node); return node;
    }
    private static float SweepAngle(float u) => -Mathf.Pi*.94f + u*Mathf.Tau*.94f;
    private static ShaderMaterial Material(Shader shader, int priority) => new() { Shader=shader, RenderPriority=priority };
    private MeshInstance3D Quad(ShaderMaterial mat, Vector2 size)
    {
        var mesh = new MeshInstance3D { Mesh=new QuadMesh { Size=size }, MaterialOverride=mat,
            ExtraCullMargin=12, CastShadow=GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(mesh); return mesh;
    }
    private Sprite3D Sprite(Texture2D texture,int priority)
    {
        var sprite = new Sprite3D { Texture=texture, PixelSize=.001f, Shaded=false, NoDepthTest=true,
            Billboard=BaseMaterial3D.BillboardModeEnum.Enabled, RenderPriority=priority, Modulate=Colors.Transparent };
        AddChild(sprite); return sprite;
    }
    public override void _Process(double delta)
    {
        if(IsQueuedForDeletion() || !IsVisibleInTree())return;
        var (paused,speed)=_clock(); if(paused)return;
        Age += delta*speed;
        Draw();
        if(Released && Age>(Heavy ? .85 : .42)) { Hide(); QueueFree(); }
    }
    private void Draw()
    {
        float t=(float)Age;
        var basis=_camera.GlobalBasis.Orthonormalized();
        Vector3 x=basis.X*(_reverse?-1:1), y=basis.Y, z=basis.Z;
        Vector3 center=_center+z*.06f;
        Vector3 right=new Vector3(basis.X.X,0,basis.X.Z).Normalized()*(_reverse?-1:1);
        Vector3 near=new Vector3(basis.Z.X,0,basis.Z.Z).Normalized();
        Vector3 orbit=Released?_strikeCenter:_orbit();
        var plane=new Basis(right*_radius,near*_radius,Vector3.Up);
        _arcMesh.GlobalTransform=new Transform3D(plane,orbit);
        _auraMesh.GlobalTransform=new Transform3D(plane,orbit-Vector3.Up*.004f);
        _echoMesh.GlobalTransform=new Transform3D(plane.Scaled(new Vector3(.965f,.965f,1)),orbit-Vector3.Up*.008f);
        _flashMesh.GlobalTransform=new Transform3D(basis,center+z*.02f);
        _shadeMesh.GlobalTransform=new Transform3D(basis,orbit-z*.02f);
        foreach(var mat in new[]{_main,_echo,_flash,_shade,_shards,_aura}) mat.SetShaderParameter("age",t);
        foreach(var mat in new[]{_flash,_shade,_shards}) mat.SetShaderParameter("release",Released?1f:0f);
        _arcMesh.Visible=_echoMesh.Visible=_flashMesh.Visible=_auraMesh.Visible=Released;
        Vector3 blade=Released?center:_blade();
        float star=Released?Mathf.Exp(-t*25)*.85f:Mathf.SmoothStep(0,1,Mathf.Min(1,t/.1f))*.8f;
        _star.GlobalPosition=blade+z*.08f;
        _star.Scale=Vector3.One*(Released?.42f:.34f)/Math.Max(.001f,HagiLight3D.Sparkle.GetWidth()*.001f);
        _star.Modulate=new Color(1,.91f,.80f,star*_power);
        for(int k=0;k<_sparks.Length;k++)
        {
            var spark=_sparks[k]; float a=k*2.39996f, seed=(k%7+1)/7f;
            if(!Released)
            {
                spark.Visible=k<14;
                float r=Math.Max(.03f,.55f-t*2.5f);
                spark.GlobalPosition=blade+(x*Mathf.Cos(a)+y*Mathf.Sin(a))*r;
                spark.Modulate=new Color(1,.58f,.42f,Mathf.Min(1,t*14)*.6f);
                spark.Scale=Vector3.One*(.045f+seed*.025f)/Math.Max(.001f,HagiLight3D.Sparkle.GetWidth()*.001f);
                continue;
            }
            spark.Visible=true;
            spark.NoDepthTest=false;
            float along=k*.618034f-Mathf.Floor(k*.618034f), angle=SweepAngle(along);
            Vector3 radial=right*Mathf.Cos(angle)+near*Mathf.Sin(angle);
            Vector3 tangent=-right*Mathf.Sin(angle)+near*Mathf.Cos(angle);
            Vector3 at=orbit+radial*_radius;
            spark.GlobalPosition=at+tangent*t*(.45f+seed*.5f)+radial*t*.35f+Vector3.Up*(Mathf.Sin(a)*t*.35f+t*t*.45f);
            float fade=(1-Mathf.Exp(-t*90))*Mathf.Exp(-t*(3.4f+seed));
            spark.Modulate=new Color(1,.40f+seed*.45f,.22f+seed*.50f,fade*_power);
            float size=(.065f+seed*.09f)/Math.Max(.001f,HagiLight3D.Sparkle.GetWidth()*.001f);
            spark.Scale=new Vector3(size*.7f,size*(1.1f+seed),1);
        }
        for(int k=0;k<_flakes.Length;k++)
        {
            var flake=_flakes[k]; flake.Visible=Released;
            float along=(k+.5f)/_flakes.Length, a=k*2.39996f, phase=SweepAngle(along)+t*.55f;
            Vector3 at=orbit+(right*Mathf.Cos(phase)+near*Mathf.Sin(phase))*(_radius+t*.45f)
                +Vector3.Up*(Mathf.Sin(a)*t*.35f+t*t*.3f);
            float angle=a*.4f+t*(k%2==0?1:-1);
            Vector3 sx=x*Mathf.Cos(angle)+y*Mathf.Sin(angle), sy=-x*Mathf.Sin(angle)+y*Mathf.Cos(angle);
            float length=(.18f+k%4*.055f)*(1-t*.55f);
            flake.GlobalTransform=new Transform3D(new Basis(sx*.075f,sy*length,z),at);
        }
    }
}
