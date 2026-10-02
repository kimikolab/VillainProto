using Godot;
using System;

// 大技の輪郭を作る専用レイヤー。刃・火柱・螺旋を分けて、白い面で画面を埋めない。
internal static partial class FireUltimateFx
{
    private static Shader? _energy, _trail, _sparks;
    private static ShaderMaterial Energy(int kind, Color color)
    {
        _energy ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded,cull_disabled,blend_add,depth_draw_never;
uniform vec4 tint : source_color;
uniform float progress=0.0;
uniform float clock=0.0;
uniform float opacity=1.0;
uniform int kind=0;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);
 return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);}
void fragment(){
 float x=UV.x*2.0-1.0,y=1.0-UV.y,t=clock;
 float core=0.0,fire=0.0,edge=0.0,alpha=1.0;
 if(kind==0){
  // 先端が尖った長剣。中央の白芯と両刃の金線を独立させる。
  float width=mix(0.48,0.29,y)*(1.0-smoothstep(0.76,1.0,y));
  float turbulence=noise(vec2(x*8.0,y*15.0-t*5.0));
  core=(1.0-smoothstep(width*0.25,width*0.72,abs(x)))*0.9;
  edge=exp(-pow((abs(x)-width)/0.035,2.0));
  fire=exp(-pow(x/(width+0.08+turbulence*0.19),2.0)*2.0)*(0.3+turbulence);
  alpha=smoothstep(0.0,0.035,y)*(1.0-smoothstep(0.97,1.0,y));
 } else if(kind==2){
  // 敵陣を一枚の噴火として覆う。横一面の火舌に太さと高さの差を作る。
  float n=noise(vec2(x*15.0,y*12.0-t*5.0));
  for(int i=0;i<11;i++){
   float k=float(i),base=(k-5.0)*0.17;
   float tip=0.70+0.27*fract(k*0.371);
   float bend=sin(y*9.0-t*3.7+k)*0.06*y;
   float w=max(0.007,0.17*(1.0-y/tip));
   float tongue=exp(-pow((x-base-bend)/w,2.0));
   float end=1.0-smoothstep(tip-0.15,tip,y);
   fire+=tongue*end*(0.60+n*0.65);
   core+=tongue*end*exp(-y*3.0)*0.18;
  }
  fire+=exp(-y*5.0)*(1.0-smoothstep(0.80,1.0,abs(x)))*0.50;
  edge=fire*pow(max(0.0,sin(x*90.0+y*13.0-t*10.0)),16.0)*0.5;
  float foot=0.015+noise(vec2(x*23.0,t*2.0))*0.035;
  alpha=smoothstep(foot,foot+0.065,y)*(1.0-smoothstep(0.92,1.0,y));
  alpha*=1.0-smoothstep(0.70,0.98,abs(x));
  alpha*=smoothstep(0.0,0.035,progress)*(1.0-smoothstep(0.40,1.0,progress));
 } else {
  float n=noise(vec2(x*7.0,y*12.0-t*4.0));
  float bend=sin(y*9.0-t*3.0)*0.07*y;
  float width=(0.38+0.11*sin(y*8.0+t*3.0)+n*0.2)*(1.0-smoothstep(0.68,1.0,y));
  core=exp(-pow((x-bend)/max(0.025,width*0.36),2.0)*2.5)*(0.65+0.35*n);
  fire=exp(-pow((x-bend)/max(0.02,width),2.0)*2.0)*(0.35+n*0.75);
  float filament=pow(max(0.0,sin(x*64.0+n*4.0+y*8.0-t*9.0)),14.0);
  edge=filament*fire*0.55;
  // 主柱の外側を独立した火舌が追い上がる。
  for(int i=0;i<7;i++){
   float k=float(i),base=(k-3.0)*0.18;
   float tip=0.64+0.28*fract(k*0.371);
   float drift=sin(y*13.0-t*4.7+k)*0.08*y;
   float w=max(0.004,0.14*(1.0-y/tip));
   float tongue=exp(-pow((x-base-drift)/w,2.0));
   fire+=tongue*(0.5+0.5*sin(y*19.0-t*12.0+k))*(1.0-smoothstep(tip-0.1,tip,y))*0.75;
  }
  alpha=smoothstep(0.0,0.035,y)*(1.0-smoothstep(0.82,1.0,y));
  alpha*=smoothstep(0.0,0.025,progress)*(1.0-smoothstep(0.40,1.0,progress));
 }
 vec3 color=mix(tint.rgb,vec3(1.0,0.97,0.79),clamp(core*1.6,0.0,1.0));
 ALBEDO=color;EMISSION=color*(1.4+core*2.0+edge);
 ALPHA=clamp(core+fire*0.6+edge*0.6,0.0,0.88)*alpha*opacity;
}" };
        var material = new ShaderMaterial { Shader = _energy };
        material.SetShaderParameter("kind", kind);
        material.SetShaderParameter("tint", color);
        return material;
    }

    internal static MeshInstance3D Sword(Node3D root)
    {
        var mesh = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One },
            MaterialOverride = Energy(0, new("ffc137")),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(mesh);
        return mesh;
    }

    internal static void SwordPose(MeshInstance3D sword, Vector3 hilt, Vector3 tip, Vector3 facing, float width, float clock)
    {
        Vector3 axis = tip - hilt;
        Vector3 along = axis.Normalized();
        Vector3 across = along.Cross(facing).Normalized();
        sword.GlobalTransform = new Transform3D(new Basis(across * width, axis, across.Cross(along)), (hilt + tip) * 0.5f);
        ((ShaderMaterial)sword.MaterialOverride).SetShaderParameter("clock", clock);
    }

    internal static void FadeSword(MeshInstance3D sword, double seconds)
    {
        var material = (ShaderMaterial)sword.MaterialOverride;
        var tween = sword.CreateTween();
        tween.TweenMethod(Callable.From<float>(v => material.SetShaderParameter("opacity", v)), 1f, 0f, seconds);
        tween.TweenCallback(Callable.From(sword.QueueFree));
    }

    internal static void Pillar(Node3D root, Vector3 ground, float height, float width, double seconds, Camera3D camera, bool grand)
    {
        var material = Energy(1, new("ffac28"));
        var node = FireFx.Card(root, root.ToLocal(ground + Vector3.Up * height * 0.5f), new(width, height), material);
        // 柱は垂直。カメラのピッチで根元が浮かないよう、Y軸を固定する。
        Vector3 across = new Vector3(camera.GlobalBasis.X.X, 0, camera.GlobalBasis.X.Z).Normalized();
        node.Basis = new Basis(across, Vector3.Up, across.Cross(Vector3.Up));
        var tween = node.CreateTween();
        tween.TweenMethod(Callable.From<float>(p => {
            float grow = Mathf.Min(1, p * 13);
            node.Scale = new(1 + p * 0.24f, Math.Max(0.001f, grow), 1);
            node.Position = root.ToLocal(ground + Vector3.Up * height * grow * 0.5f);
            material.SetShaderParameter("progress", p);
            material.SetShaderParameter("clock", p * 2.3f);
        }), 0f, 1f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(node.QueueFree));
        if (grand)
        {
            Helix(root, ground, height * 0.90f, width * 0.32f, seconds, 0);
            Helix(root, ground, height * 0.86f, width * 0.40f, seconds, Mathf.Pi);
        }
    }

    private static ShaderMaterial Trail(Color color)
    {
        _trail ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded,cull_disabled,blend_add,depth_draw_never;
uniform vec4 tint : source_color;
uniform float progress=0.0;
void fragment(){
 float y=abs(UV.y*2.0-1.0);
 float core=exp(-y*y*28.0);
 float ends=pow(max(0.0,sin(UV.x*3.14159)),0.45);
 float flow=0.7+0.3*sin(UV.x*80.0-progress*21.0);
 ALBEDO=mix(tint.rgb,vec3(1.0,0.96,0.8),core*0.9);
 EMISSION=ALBEDO*(1.4+core*2.0);
 ALPHA=exp(-y*y*3.0)*ends*flow*smoothstep(0.0,0.045,progress)*(1.0-smoothstep(0.4,1.0,progress));
}" };
        var m = new ShaderMaterial { Shader = _trail };
        m.SetShaderParameter("tint", color);
        return m;
    }

    private static MeshInstance3D Strip(Node3D root, Func<float, Vector3> path, Func<float, Vector3> side, float width, Color color)
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        for (int i = 0; i <= 96; i++)
        {
            float p = i / 96f;
            float taper = Mathf.Pow(Mathf.Max(0.001f, Mathf.Sin(p * Mathf.Pi)), 0.5f);
            for (int k = 0; k < 2; k++)
            {
                mesh.SurfaceSetUV(new(p, k));
                mesh.SurfaceAddVertex(path(p) + side(p) * width * taper * (k == 0 ? -1 : 1));
            }
        }
        mesh.SurfaceEnd();
        var node = new MeshInstance3D { Mesh = mesh, MaterialOverride = Trail(color),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(node);
        return node;
    }

    private static void Helix(Node3D root, Vector3 ground, float height, float radius, double seconds, float phase)
    {
        Vector3 Path(float p) {
            float a = p * Mathf.Tau * 1.8f + phase;
            float r = radius * (0.8f + p * 0.35f);
            return new(Mathf.Cos(a) * r, p * height, Mathf.Sin(a) * r);
        }
        var node = Strip(root, Path, _ => Vector3.Up, 0.16f, new("ffd666"));
        node.Position = root.ToLocal(ground);
        AnimateTrail(node, seconds, p => node.Rotation = new(0, p * 1.8f, 0));
    }

    internal static void Crescent(Node3D root, Vector3 from, Vector3 to, Camera3D camera, double seconds, float radius = 2.8f)
    {
        Vector3 right = camera.GlobalBasis.X * (camera.UnprojectPosition(to).X >= camera.UnprojectPosition(from).X ? 1 : -1);
        Vector3 up = camera.GlobalBasis.Y;
        for (int layer = 0; layer < 3; layer++)
        {
            float r = radius + layer * 0.15f;
            Vector3 Radial(float p) { float a = (p - 0.5f) * 2.65f; return right * Mathf.Cos(a) + up * Mathf.Sin(a); }
            var node = Strip(root, p => Radial(p) * r - right * r * 0.65f,
                Radial, layer == 0 ? 0.85f : 0.095f, new(layer == 0 ? "ff8d20" : "ffe299"));
            AnimateTrail(node, seconds, p => node.Position = root.ToLocal(from.Lerp(to, Mathf.Min(1, p / 0.36f))));
        }
    }

    private static void AnimateTrail(MeshInstance3D node, double seconds, Action<float> update)
    {
        var m = (ShaderMaterial)node.MaterialOverride;
        var tween = node.CreateTween();
        tween.TweenMethod(Callable.From<float>(p => { update(p); m.SetShaderParameter("progress", p); }), 0f, 1f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(node.QueueFree));
    }

    internal static void Sparks(Node3D root, Vector3 ground, Camera3D camera, double seconds)
    {
        _sparks ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded,cull_disabled,blend_add,depth_draw_never;
uniform float progress=0.0;
void vertex(){
 float p=progress*0.95;
 vec3 velocity=vec3((COLOR.r-0.5)*9.0,2.0+COLOR.g*7.0,(COLOR.b-0.5)*8.0);
 VERTEX+=velocity*p+vec3(0.0,-4.5*p*p,0.0);
}
void fragment(){
 vec2 p=UV*2.0-1.0;
 float spark=exp(-dot(p*vec2(3.0,1.4),p*vec2(3.0,1.4)));
 float cross=exp(-dot(p*vec2(1.4,6.0),p*vec2(1.4,6.0)));
 ALBEDO=vec3(1.0,0.69,0.19);EMISSION=vec3(3.5,2.6,1.3);
 ALPHA=(spark+cross*0.5)*pow(1.0-progress,1.3)*smoothstep(0.0,0.04,progress);
}" };
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        int[] corners = { 0, 1, 2, 0, 2, 3 };
        Vector2[] uv = { new(0, 0), new(1, 0), new(1, 1), new(0, 1) };
        for (int i = 0; i < 52; i++)
        {
            float seed = Mathf.PosMod(i * 0.618034f, 1);
            var data = new Color(seed, Mathf.PosMod(i * 0.371f, 1), Mathf.PosMod(i * 0.713f, 1));
            float size = 0.06f + seed * 0.09f;
            foreach (int corner in corners)
            {
                Vector2 xy = uv[corner] * 2 - Vector2.One;
                mesh.SurfaceSetColor(data);
                mesh.SurfaceSetUV(uv[corner]);
                mesh.SurfaceAddVertex(camera.GlobalBasis.X * xy.X * size + camera.GlobalBasis.Y * xy.Y * size);
            }
        }
        mesh.SurfaceEnd();
        var node = new MeshInstance3D { Mesh = mesh, Position = root.ToLocal(ground),
            MaterialOverride = new ShaderMaterial { Shader = _sparks },
            ExtraCullMargin = 10, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(node);
        AnimateTrail(node, seconds, _ => { });
    }
}
