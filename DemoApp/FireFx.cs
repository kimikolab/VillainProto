using Godot;
using System;

// 炎の芯・薄い外炎・火の粉を同じ時間で動かす。乱数は演出専用の固定した位相。
internal static class FireFx
{
    internal static Color ColorOf(int level) => level switch {
        <= 1 => new("b93220"), 2 => new("ff6b23"), 3 => new("ffd66e"), _ => new("82dfff") };
    private static Shader? _shader, _ribbon;
    internal static ShaderMaterial Material(int mode, Color color, float phase = 0)
    {
        _shader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never;
uniform vec4 tint : source_color = vec4(1.0,0.3,0.05,1.0);
uniform float clock = 0.0;
uniform float phase = 0.0;
uniform float progress = 0.0;
uniform float strength = 1.0;
uniform int mode = 0;
uniform bool persistent = false;
uniform bool invasive = false;
uniform bool billboard = true;
float hash(vec2 p) { return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453); }
float noise(vec2 p) {
    vec2 i=floor(p),f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
float fbm(vec2 p) { return noise(p)*0.57+noise(p*2.1)*0.28+noise(p*4.2)*0.15; }
void vertex() {
    if(billboard) MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
        INV_VIEW_MATRIX[0]*length(MODEL_MATRIX[0].xyz),
        INV_VIEW_MATRIX[1]*length(MODEL_MATRIX[1].xyz),
        INV_VIEW_MATRIX[2]*length(MODEL_MATRIX[2].xyz),MODEL_MATRIX[3]);
}
void fragment() {
    vec2 p=UV*2.0-1.0; float r=length(p); float a=atan(p.y,p.x);
    float t=clock+phase; float q=progress; float glow=0.0; float core=0.0;
    float fade=persistent ? 1.0 : smoothstep(0.0,0.07,q)*(1.0-smoothstep(0.55,1.0,q));
    float n=fbm(vec2(p.x*4.0,p.y*3.0+t*2.8));
    if(mode==0) {
        float y=1.0-UV.y;
        float taper=max(0.0,1.0-y);
        float bend=sin(y*9.0-t*3.0)*0.13*y;
        float flames=0.0;
        for(int i=0;i<5;i++) {
            float k=float(i); float x=(k-2.0)*0.30+bend+sin(y*14.0-t*4.0+k*2.1)*y*0.16;
            float height=0.52+0.20*sin(k*2.9+phase)+0.13*n;
            float w=(0.15+0.13*n)*max(0.0,1.0-y/height);
            flames=max(flames,(1.0-smoothstep(w*0.2,w+0.035,abs(p.x-x)))*(1.0-smoothstep(height-0.18,height,y)));
        }
        glow=flames*smoothstep(0.0,0.06,y)*(0.48+0.52*n);
        core=pow(max(0.0,flames),3.0)*(1.0-y)*0.6;
        if(invasive) { float cracks=pow(max(0.0,1.0-abs(sin(p.y*19.0+p.x*9.0+n*6.0))),18.0);
            glow+=cracks*exp(-r*r*4.0)*0.8; }
    } else if(mode==1) {
        float edge=0.10+q*0.68+(n-0.5)*0.26;
        glow=exp(-pow((r-edge)/(0.06+q*0.16),2.0))*(0.25+1.35*n);
        glow+=pow(max(0.0,sin(a*11.0+n*5.0)),7.0)*exp(-pow((r-edge)/0.20,2.0))*0.40;
        core=exp(-r*r/(0.018+q*0.10))*(1.0-q);
    } else if(mode==2) {
        float edge=0.22+q*0.60;
        glow=exp(-pow((r-edge)/0.028,2.0))+exp(-pow((r-edge*0.83)/0.018,2.0))*0.45;
        core=glow*0.35;
    } else if(mode==3) {
        float swirl=sin(a*5.0+r*22.0+t*7.0);
        glow=pow(max(0.0,swirl),9.0)*smoothstep(0.98,0.25,r)*smoothstep(0.02,0.25,r);
        core=exp(-r*r*45.0)*(0.4+q);
    } else if(mode==4) {
        float diamond=abs(p.x)+abs(p.y)*0.75;
        glow=exp(-pow((diamond-0.37)/0.023,2.0));
        glow+=exp(-pow((diamond-0.52)/0.012,2.0))*0.5;
        core=exp(-dot(p*vec2(11.0,6.0),p*vec2(11.0,6.0)));
        glow*=0.75+0.25*sin(t*2.5);
    } else if(mode==5) {
        glow=exp(-r*r*4.5)*n*0.18; core=0.0;
    } else if(mode==6) {
        float rim=exp(-pow((r-0.65)/0.04,2.0));
        glow=rim*(0.6+0.4*sin(a*8.0+t*4.0))+exp(-r*r*3.0)*n*0.23;
        core=rim*0.45;
    } else if(mode==7) {
        glow=exp(-dot(p*vec2(3.0,14.0),p*vec2(3.0,14.0)));
        core=exp(-dot(p*vec2(5.0,35.0),p*vec2(5.0,35.0)));
    }
    float sparks=0.0;
    for(int i=0;i<18;i++) {
        float k=float(i); float life=fract(t*0.24+k*0.618);
        vec2 at=vec2(sin(k*8.31+phase)*0.85,0.8-life*1.7);
        if(mode==1) at=vec2(cos(k*2.4),sin(k*2.4))*(q*0.88);
        float d=length((p-at)*vec2(1.0,0.65));
        sparks+=exp(-d*d*9000.0)*sin(life*3.14159);
    }
    if(mode==0 || mode==1 || mode==3) {glow+=sparks;core+=sparks;}
    vec3 light=mix(tint.rgb,vec3(1.0,0.96,0.82),clamp(core,0.0,0.9));
    float energy=(glow+core)*fade*strength;
    ALBEDO=light; EMISSION=light*(1.0+core*2.0);
    ALPHA=clamp(energy,0.0,0.9)*smoothstep(1.0,0.86,max(abs(p.x),abs(p.y)));
}" };
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("mode", mode);
        material.SetShaderParameter("tint", color);
        material.SetShaderParameter("phase", phase);
        return material;
    }

    internal static MeshInstance3D Card(Node3D root, Vector3 local, Vector2 size, ShaderMaterial material, bool ground = false)
    {
        material.SetShaderParameter("billboard", !ground);
        var mesh = new MeshInstance3D { Mesh = new QuadMesh { Size = size }, Position = local,
            MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        if (ground) mesh.RotationDegrees = new(-90, 0, 0);
        root.AddChild(mesh);
        return mesh;
    }

    internal static void Bloom(Node3D root, Vector3 point, Color color, float size, double seconds,
        int mode = 1, bool ground = false, float delay = 0)
    {
        var material = Material(mode, color, point.X * 1.7f + point.Z);
        var mesh = Card(root, root.ToLocal(point), Vector2.One * size, material, ground);
        mesh.Visible = delay <= 0;
        var tween = mesh.CreateTween();
        if (delay > 0) { tween.TweenInterval(delay); tween.TweenCallback(Callable.From(() => mesh.Show())); }
        tween.TweenMethod(Callable.From<float>(p => {
            material.SetShaderParameter("progress", p);
            material.SetShaderParameter("clock", p * 1.5f);
        }), 0f, 1f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(mesh.QueueFree));
    }

    // 発光は画面の白塗りにせず、地形にだけ短い照り返しを落とす。
    internal static void Light(Node3D root, Vector3 point, Color color, float energy, double seconds)
    {
        if (root.GetChildCount() > 100) return;
        var light = new OmniLight3D { Position = root.ToLocal(point), LightColor = color,
            LightEnergy = energy, OmniRange = 5, ShadowEnabled = false };
        root.AddChild(light);
        var tween = light.CreateTween();
        tween.TweenProperty(light, "light_energy", 0f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(light.QueueFree));
    }

    internal static void Ribbon(Node3D root, Vector3 from, Vector3 to, Color color,
        float width, float arc, double seconds, Vector3 facing)
    {
        if (from.DistanceSquaredTo(to) < 0.001f) return;
        _ribbon ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded,cull_disabled,blend_add,depth_draw_never;
uniform vec4 tint : source_color;
uniform float progress=0.0;
void fragment(){
    float head=progress*1.45;
    float body=smoothstep(head-0.46,head-0.16,UV.x)*(1.0-smoothstep(head-0.04,head,UV.x));
    float y=abs(UV.y*2.0-1.0);
    float core=exp(-y*y*60.0);
    float filaments=0.7+0.3*sin(UV.x*100.0-progress*26.0+UV.y*16.0);
    ALBEDO=mix(tint.rgb,vec3(1.0,0.97,0.86),core*0.85);
    EMISSION=ALBEDO*(1.4+core*2.3);
    ALPHA=body*exp(-y*y*4.0)*filaments;
}" };
        var material = new ShaderMaterial { Shader = _ribbon };
        material.SetShaderParameter("tint", color);
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        for (int i = 0; i <= 32; i++)
        {
            float p = i / 32f;
            Vector3 point = from.Lerp(to, p) + Vector3.Up * Mathf.Sin(p * Mathf.Pi) * arc;
            Vector3 tangent = to - from + Vector3.Up * Mathf.Cos(p * Mathf.Pi) * arc * Mathf.Pi;
            Vector3 side = tangent.Cross(facing).Normalized();
            for (int k = 0; k < 2; k++)
            {
                mesh.SurfaceSetUV(new(p, k));
                mesh.SurfaceAddVertex(root.ToLocal(point + side * width * (k == 0 ? -1 : 1)));
            }
        }
        mesh.SurfaceEnd();
        var node = new MeshInstance3D { Mesh = mesh, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        root.AddChild(node);
        var tween = node.CreateTween();
        tween.TweenMethod(Callable.From<float>(p => material.SetShaderParameter("progress", p)),
            0f, 1f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(node.QueueFree));
    }

    internal static void Fireball(Node3D root, Vector3 from, Vector3 to, Color color, double seconds)
    {
        var material = Material(1, color);
        material.SetShaderParameter("persistent", true);
        material.SetShaderParameter("progress", 0.18f);
        var node = Card(root, root.ToLocal(from), Vector2.One * 1.15f, material);
        var tween = node.CreateTween();
        tween.TweenMethod(Callable.From<float>(p => {
            node.Position = root.ToLocal(from.Lerp(to, p) + Vector3.Up * Mathf.Sin(p * Mathf.Pi) * 0.35f);
            material.SetShaderParameter("clock", p * 2);
        }), 0f, 1f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(node.QueueFree));
    }
}
