using Godot;
using System;
using System.Collections.Generic;

// 実際の武器の根元と先端から短い曲面の残光を作る。待機や別の連撃へ線をつながない。
public partial class RicaWeaponTrail3D : MeshInstance3D
{
    private readonly record struct Sample(Vector3 Root, Vector3 Tip, double Age);
    private readonly List<Sample> _samples = new();
    private readonly ImmediateMesh _ribbon = new();
    private ShaderMaterial _material = null!, _returnMaterial = null!;
    private string? _clip;
    private const double Life = .18;

    internal void Start()
    {
        TopLevel = true; GlobalTransform = Transform3D.Identity;
        Mesh = _ribbon; CastShadow = ShadowCastingSetting.Off;
        _material = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded,blend_add,cull_disabled,depth_draw_never;
uniform vec4 tint:source_color;
uniform float strength=1.0;
uniform bool luminous=false;
void fragment(){
 if(luminous){
  float age=clamp(UV.y,0.0,1.0);
  float width=0.004+0.032*sqrt(age);
  float core=exp(-pow((UV.x-0.94)/width,2.0));
  float halo=exp(-pow((UV.x-0.90)/0.105,2.0));
  float inner=exp(-pow((UV.x-0.80)/0.075,2.0))*0.56;
  float flutter=sin(age*11.0)*0.004;
  float fine=exp(-abs(UV.x-(0.70+flutter))*165.0)*0.72;
  fine+=exp(-abs(UV.x-(0.56-flutter))*195.0)*0.38;
  float rim=exp(-abs(UV.x-0.988)*230.0)*0.42;
  float fade=pow(age,0.90);
  // 1撃目のslash_arc.gdshaderと同じ白い芯・青紫の縁。水色の面を作らない。
  vec3 edge_color=mix(pow(tint.rgb,vec3(2.4)),vec3(0.02,0.48,1.0),0.35);
  vec3 white=vec3(0.93,0.98,1.0);
  ALBEDO=mix(edge_color*0.40,vec3(0.96,0.99,1.0),clamp(core+inner*0.5+rim,0.0,1.0))*fade;
  EMISSION=(white*(core*4.3+inner*1.2+rim*2.0)+edge_color*(halo*0.70+fine*1.8))*strength*fade;
  ALPHA=clamp(core+rim+halo*0.48+inner*0.70+fine,0.0,1.0)*fade;
 }else{
 float edge=exp(-pow((1.0-UV.x)*72.0,2.0));
 float hair=exp(-abs(UV.x-0.90)*110.0)*0.55;
 float wake=pow(UV.x,2.0)*0.27;
 float fade=pow(UV.y,1.8);
 ALBEDO=mix(tint.rgb,vec3(0.97,0.99,1.0),edge);
 EMISSION=(tint.rgb*(wake+hair)*1.5+vec3(0.95,0.99,1.0)*edge*3.0)*strength;
 ALPHA=clamp(edge+hair+wake,0.0,1.0)*fade;
 }
}" } };
        MaterialOverride = _material;
        // 剣閃は光の残像。地面との交差で帯を水平に切らない。
        _returnMaterial=new ShaderMaterial { Shader=new Shader { Code=_material.Shader!.Code
            .Replace("depth_draw_never;","depth_draw_never,depth_test_disabled;") } };
    }

    private void Clear()
    {
        _samples.Clear(); _clip = null;
        _ribbon.ClearSurfaces(); Hide();
    }

    // 測った剣先を通る曲線。点の間隔に応じて接線を変え、速い振りでも角やループを作らない。
    private static Vector3 SmoothPoint(Vector3 p0,Vector3 p1,Vector3 p2,Vector3 p3,float weight)
    {
        float Step(Vector3 a,Vector3 b)=>MathF.Sqrt(Math.Max(.0001f,a.DistanceTo(b)));
        float t0=0,t1=Step(p0,p1),t2=t1+Step(p1,p2),t3=t2+Step(p2,p3);
        float t=Mathf.Lerp(t1,t2,weight);
        Vector3 Mix(Vector3 a,Vector3 b,float start,float end)=>a*((end-t)/(end-start))+b*((t-start)/(end-start));
        Vector3 a1=Mix(p0,p1,t0,t1),a2=Mix(p1,p2,t1,t2),a3=Mix(p2,p3,t2,t3);
        Vector3 b1=Mix(a1,a2,t0,t2),b2=Mix(a2,a3,t1,t3);
        return Mix(b1,b2,t1,t2);
    }

    internal void Feed(double age, bool paused, bool active, string? clip, Vector3 root, Vector3 tip, Color tint,
        bool recovery = false)
    {
        if (paused) return;
        // 振りかぶりの離れた原画をSlerpで結ぶと、振っていない半円まで斬撃になる。
        // 武器を運ぶ間は履歴も切り、次の振り下ろしから新しく剣先を追う。
        if (recovery) { Clear(); return; }
        if (active && clip != _clip) { _samples.Clear(); _clip = clip; }
        // 振り終わって名前が外れた後も、同じ色と太さで余韻を消す。
        bool luminous=_clip=="rica_return";
        double life=luminous?.32:Life;
        if (active && (_samples.Count == 0 || _samples[^1].Tip.DistanceSquaredTo(tip) > .0001f
            || _samples[^1].Root.DistanceSquaredTo(root) > .0001f))
            _samples.Add(new(root, tip, age));
        else if (active && _samples.Count > 0) _samples[^1] = _samples[^1] with { Age = age };
        _samples.RemoveAll(s => age - s.Age > life);
        _ribbon.ClearSurfaces(); Visible = _samples.Count > 1;
        if (!Visible) return;
        var material=luminous?_returnMaterial:_material;
        if(MaterialOverride!=material)MaterialOverride=material;
        material.SetShaderParameter("tint", tint);
        material.SetShaderParameter("strength",luminous?1.45f:1.0f);
        material.SetShaderParameter("luminous",luminous);
        _ribbon.SurfaceBegin(Godot.Mesh.PrimitiveType.Triangles);
        var distance=new float[_samples.Count];
        for(int i=1;i<_samples.Count;i++)distance[i]=distance[i-1]+_samples[i-1].Tip.DistanceTo(_samples[i].Tip);
        float length=Math.Max(.0001f,distance[^1]);
        void Vertex(Vector3 p, float u, float fade,float along)
        {
            // 色と透明度の両方を端で消す。加算光でも角ばった切断面を残さない。
            float taper=luminous?Mathf.SmoothStep(0,1,Math.Clamp(along/.25f,0,1)):1;
            _ribbon.SurfaceSetUV(new Vector2(u,fade*taper));_ribbon.SurfaceAddVertex(p);
        }
        for (int i = 1; i < _samples.Count; i++)
        {
            var a = _samples[i-1]; var b = _samples[i];
            Vector3 av = a.Tip-a.Root, bv = b.Tip-b.Root;
            float angle = Mathf.Acos(Mathf.Clamp(av.Normalized().Dot(bv.Normalized()), -1, 1));
            int steps = Math.Clamp((int)MathF.Ceiling(angle/(luminous?.05f:.15f)), 1, luminous?60:18);
            if(luminous)steps=Math.Clamp(Math.Max(steps,(int)MathF.Ceiling(a.Tip.DistanceTo(b.Tip)/.065f)),1,60);
            (Vector3 Root, Vector3 Tip, float Fade,float Along) Point(float t)
            {
                var p = a.Root.Lerp(b.Root,t);
                var v = av.Normalized().Slerp(bv.Normalized(),t)*Mathf.Lerp(av.Length(),bv.Length(),t);
                var tip=p+v;
                if(luminous)
                {
                    var previous=i>1?_samples[i-2]:new Sample(a.Root-(b.Root-a.Root),a.Tip-(b.Tip-a.Tip),a.Age);
                    var next=i+1<_samples.Count?_samples[i+1]:new Sample(b.Root+(b.Root-a.Root),b.Tip+(b.Tip-a.Tip),b.Age);
                    p=SmoothPoint(previous.Root,a.Root,b.Root,next.Root,t);
                    tip=SmoothPoint(previous.Tip,a.Tip,b.Tip,next.Tip,t);
                }
                return (p,tip,(float)Math.Clamp(1-(age-Mathf.Lerp(a.Age,b.Age,t))/life,0,1),Mathf.Lerp(distance[i-1],distance[i],t)/length);
            }
            for (int k = 0; k < steps; k++)
            {
                var p = Point(k/(float)steps); var q = Point((k+1)/(float)steps);
                Vertex(p.Root,0,p.Fade,p.Along); Vertex(p.Tip,1,p.Fade,p.Along); Vertex(q.Tip,1,q.Fade,q.Along);
                Vertex(p.Root,0,p.Fade,p.Along); Vertex(q.Tip,1,q.Fade,q.Along); Vertex(q.Root,0,q.Fade,q.Along);
            }
        }
        _ribbon.SurfaceEnd();
    }
}
