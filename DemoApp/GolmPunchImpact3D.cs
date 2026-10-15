using Godot;
using System;

// 拳の接触に合わせた短い閃光と、遅れて広がる衝撃波。時計はゴルム本体と共有する。
public partial class GolmPunchImpact3D : Node3D
{
    private ShaderMaterial _flash = null!;
    private readonly ImmediateMesh _sparks = new();
    private GpuParticles3D? _purchased;
    private double _elapsed, _speed = 1;
    private bool _paused;
    private bool _heavy;
    private Vector3 _up, _right;
    internal double Elapsed => _elapsed;
    internal bool Heavy => _heavy;
    internal bool UsesPurchased => _purchased is not null;
    private const double Seconds = .32;

    internal void Configure(Vector3 point, Camera3D camera, bool heavy, bool paused, double speed)
    {
        GlobalPosition = point;
        _heavy = heavy; _up = camera.GlobalBasis.Y; _right = camera.GlobalBasis.X;
        _flash = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never, depth_test_disabled;
uniform float progress = 0.0;
uniform float power = 1.0;
void vertex() {
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}
void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float r = length(p);
    float a = atan(p.y,p.x);
    float t = progress;
    float spikes = pow(max(0.0,cos(a*7.0 + 0.5)),18.0);
    float shape = 0.19 + spikes*0.60;
    float edge = 1.0-smoothstep(shape,shape+0.045,r);
    float core = (1.0-smoothstep(shape*0.75,shape,r))*(1.0-smoothstep(0.08,0.36,t));
    float rim = 0.20+t*0.78;
    float wave = exp(-pow((r-rim)/0.025,2.0))*(1.0-t);
    float outline = edge*(1.0-smoothstep(0.14,0.42,t));
    vec3 warm = mix(vec3(1.0,0.55,0.16),vec3(1.0,0.96,0.78),core);
    ALBEDO = mix(vec3(0.21,0.12,0.065),warm,clamp(core+wave,0.0,1.0));
    EMISSION = warm*(core*1.1+wave*0.35);
    ALPHA = clamp((outline*0.85+core+wave*0.75)*power,0.0,1.0)*(1.0-smoothstep(0.82,1.0,t));
}" } };
        _flash.RenderPriority=6;
        _flash.SetShaderParameter("power",heavy ? 1f : .65f);
        float size = heavy ? 3.0f : 1.65f;
        AddChild(new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One*size }, MaterialOverride = _flash,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        AddChild(new MeshInstance3D { Mesh = _sparks, MaterialOverride = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled, NoDepthTest = true, RenderPriority = 6,
        }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        if (heavy) AddPurchasedFlash();
        SetClock(paused,speed); Draw(0);
    }

    internal void SetClock(bool paused,double speed)
    {
        _paused=paused; _speed=Math.Max(.1,speed);
        if (_purchased is not null) _purchased.SpeedScale=paused?0:_speed;
    }
    public override void _Process(double delta)
    {
        if (_paused) return;
        _elapsed+=delta*_speed;
        Draw((float)Math.Min(1,_elapsed/Seconds));
        if (_elapsed>=Seconds) { Hide(); QueueFree(); }
    }
    private void Draw(float p)
    {
        _flash.SetShaderParameter("progress",p);
        _sparks.ClearSurfaces(); _sparks.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        int count=_heavy?14:7;
        for(int i=0;i<count;i++)
        {
            float angle=i*2.39996f;
            Vector3 direction=_right*MathF.Cos(angle)+_up*MathF.Sin(angle);
            Vector3 side=-_right*MathF.Sin(angle)+_up*MathF.Cos(angle);
            float radius=(.15f+p*(1.15f+(i%4)*.18f))*(_heavy?1f:.65f);
            Vector3 at=direction*radius;
            float width=.025f*(1-p), length=(.15f+(i%3)*.06f)*(1-p);
            _sparks.SurfaceSetColor(new Color(i%2==0?new Color("ffe6a4"):new Color("fba047"),1-p));
            _sparks.SurfaceAddVertex(at-direction*length);
            _sparks.SurfaceAddVertex(at+side*width);
            _sparks.SurfaceAddVertex(at-side*width);
        }
        _sparks.SurfaceEnd();
    }
    private void AddPurchasedFlash()
    {
        const string path="res://PolyBlocks/EffectBlocks/assets/impacts/impact_1.tscn";
        if (!ResourceLoader.Exists(path)) return;
        var source=GD.Load<PackedScene>(path).Instantiate<Node3D>(); source.SetScript(default);
        _purchased=source.GetNode<GpuParticles3D>("Effect1"); source.RemoveChild(_purchased); source.Free();
        _purchased.ProcessMaterial=(ParticleProcessMaterial)_purchased.ProcessMaterial.Duplicate(true);
        var process=(ParticleProcessMaterial)_purchased.ProcessMaterial;
        process.Color=new Color("ffe6a4"); process.ColorRamp=null; process.ScaleMin=.20f; process.ScaleMax=.20f;
        var material=(StandardMaterial3D)_purchased.DrawPass1.SurfaceGetMaterial(0).Duplicate(true);
        material.Emission=new Color("ffc06c"); material.EmissionEnergyMultiplier=.7f; material.NoDepthTest=true; material.RenderPriority=6;
        _purchased.MaterialOverride=material; _purchased.DrawPasses=1;
        _purchased.Lifetime=.18; _purchased.OneShot=true; _purchased.Preprocess=0;
        _purchased.UseFixedSeed=true; _purchased.Seed=345;
        AddChild(_purchased); _purchased.Restart(); _purchased.Emitting=true;
    }
}
