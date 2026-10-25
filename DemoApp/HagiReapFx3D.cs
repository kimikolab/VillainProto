using Godot;
using System;

// 一振りに一つの時計。予兆から斬光・光片の余韻まで、停止と速度変更を同じ時計で扱う。
public partial class HagiReapFx3D : Node3D
{
    private static Shader? _arcShader, _glowShader, _auraShader;
    private static Texture2D? _ringTexture;
    private Func<(bool Paused, double Speed)> _clock = () => (false, 1);
    private Func<Vector3> _blade = () => Vector3.Zero;
    private Camera3D _camera = null!;
    private Vector3 _center;
    private float _width;
    private bool _reverse;
    private ShaderMaterial _main = null!, _echo = null!, _flash = null!, _shade = null!, _shards = null!;
    private MeshInstance3D _arcMesh = null!, _echoMesh = null!, _flashMesh = null!, _shadeMesh = null!;
    private ShaderMaterial _aura = null!;
    private MeshInstance3D _auraMesh = null!;
    private MeshInstance3D[] _flakes = Array.Empty<MeshInstance3D>();
    private Sprite3D[] _sparks = Array.Empty<Sprite3D>();
    private Sprite3D _halo = null!, _star = null!;
    internal double Age { get; private set; }
    internal bool Released { get; private set; }

    internal void Start(Vector3 center, float width, bool reverse, Camera3D camera,
        Func<Vector3> blade, Func<(bool Paused, double Speed)> clock)
    {
        _center = center; _width = width; _reverse = reverse; _camera = camera; _blade = blade; _clock = clock;
        _arcShader ??= new Shader { Code = Godot.FileAccess.GetFileAsString("res://assets/fx/hagi_reap_arc.gdshader") };
        _glowShader ??= new Shader { Code = Godot.FileAccess.GetFileAsString("res://assets/fx/hagi_reap_glow.gdshader") };
        _auraShader ??= new Shader { Code = Godot.FileAccess.GetFileAsString("res://assets/fx/hagi_reap_aura.gdshader") };
        const string ring = "res://PolyBlocks/EffectBlocks/source_files/textures/circle_1.png";
        _ringTexture ??= ResourceLoader.Exists(ring) ? GD.Load<Texture2D>(ring) : HagiLight3D.Sparkle;
        _main = Material(_arcShader, 12); _echo = Material(_arcShader, 10); _echo.SetShaderParameter("echo",1f);
        _flash = Material(_glowShader, 14);
        _shade = Material(_glowShader, -12); _shade.SetShaderParameter("layer",1);
        _shards = Material(_glowShader, 15); _shards.SetShaderParameter("layer",2);
        _arcMesh = Quad(_main, new(width,2.25f));
        _aura = Material(_auraShader, 11);
        _aura.SetShaderParameter("slash_width", width);
        _auraMesh = Quad(_aura, new(width,3.6f));
        _echoMesh = Quad(_echo,new(width*1.04f,2.40f));
        _flashMesh = Quad(_flash,new(width+1.4f,2.8f));
        _shadeMesh = Quad(_shade,new(width+2,3.8f));
        _halo = Sprite(_ringTexture,8); _star = Sprite(HagiLight3D.Sparkle,15);
        _sparks = new Sprite3D[26];
        for (int i=0;i<_sparks.Length;i++) _sparks[i] = Sprite(HagiLight3D.Sparkle,14);
        _flakes = new MeshInstance3D[12];
        for (int i=0;i<_flakes.Length;i++) _flakes[i] = Quad(_shards,Vector2.One);
        Draw();
    }
    internal void Release()
    {
        if (Released) return;
        Released = true; Age = 0; Draw();
    }
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
        if(Released && Age>.85) { Hide(); QueueFree(); }
    }
    private void Draw()
    {
        float t=(float)Age;
        var basis=_camera.GlobalBasis.Orthonormalized();
        Vector3 x=basis.X*(_reverse?-1:1), y=basis.Y, z=basis.Z;
        Vector3 center=_center+z*.06f;
        _arcMesh.GlobalTransform=new Transform3D(new Basis(x,y,z),center);
        _auraMesh.GlobalTransform=new Transform3D(new Basis(x,y,z),center-z*.004f);
        _echoMesh.GlobalTransform=new Transform3D(new Basis(x,y,z),center+y*.07f-z*.008f);
        _flashMesh.GlobalTransform=new Transform3D(basis,center+y*.34f+z*.02f);
        _shadeMesh.GlobalTransform=new Transform3D(basis,center-z*.02f);
        foreach(var mat in new[]{_main,_echo,_flash,_shade,_shards,_aura}) mat.SetShaderParameter("age",t);
        foreach(var mat in new[]{_flash,_shade,_shards}) mat.SetShaderParameter("release",Released?1f:0f);
        _arcMesh.Visible=_echoMesh.Visible=_flashMesh.Visible=_auraMesh.Visible=Released;
        Vector3 blade=Released?center:_blade();
        float star=Released?Mathf.Exp(-t*25)*.85f:Mathf.SmoothStep(0,1,Mathf.Min(1,t/.1f))*.8f;
        _star.GlobalPosition=blade+z*.08f;
        _star.Scale=Vector3.One*(Released?.42f:.34f)/Math.Max(.001f,HagiLight3D.Sparkle.GetWidth()*.001f);
        _star.Modulate=new Color(1,.91f,.80f,star);
        _halo.Visible=Released;
        if(Released)
        {
            _halo.GlobalPosition=center+y*.2f;
            float size=Math.Max(.001f,_ringTexture!.GetWidth()*.001f);
            _halo.Scale=new Vector3(_width*(.52f+t*.65f)/size,(.28f+t*.8f)/size,1);
            _halo.Modulate=new Color(1,.20f,.10f,(1-Mathf.Exp(-t*90))*Mathf.Exp(-t*10)*.28f);
        }
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
            float along=(k*.618034f-Mathf.Floor(k*.618034f))*2-1;
            Vector3 at=center+x*(along*_width*.46f)+y*(.60f*(1-along*along));
            spark.GlobalPosition=at+x*Mathf.Cos(a)*t*.5f+y*(Mathf.Sin(a)*t*.8f+t*t*.65f);
            float fade=(1-Mathf.Exp(-t*90))*Mathf.Exp(-t*(3.4f+seed));
            spark.Modulate=new Color(1,.40f+seed*.45f,.22f+seed*.50f,fade);
            float size=(.065f+seed*.09f)/Math.Max(.001f,HagiLight3D.Sparkle.GetWidth()*.001f);
            spark.Scale=new Vector3(size*.7f,size*(1.1f+seed),1);
        }
        for(int k=0;k<_flakes.Length;k++)
        {
            var flake=_flakes[k]; flake.Visible=Released;
            float along=(k+.5f)/_flakes.Length*2-1, a=k*2.39996f;
            Vector3 at=center+x*(along*_width*.43f+Mathf.Cos(a)*t*.9f)
                +y*(.60f*(1-along*along)+Mathf.Sin(a)*t*.9f+t*t*.5f)+z*.03f;
            float angle=a*.4f+t*(k%2==0?1:-1);
            Vector3 sx=x*Mathf.Cos(angle)+y*Mathf.Sin(angle), sy=-x*Mathf.Sin(angle)+y*Mathf.Cos(angle);
            float length=(.18f+k%4*.055f)*(1-t*.55f);
            flake.GlobalTransform=new Transform3D(new Basis(sx*.075f,sy*length,z),at);
        }
    }
}
