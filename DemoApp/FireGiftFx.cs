using Godot;
using System;

// 手番を託す帯と、受け取った時間が動き出す光紋。攻撃用の火球とは形を分ける。
internal static class FireGiftFx
{
    private static Shader? _clock, _ribbon;

    internal static void Dial(Node3D root, BattlePawn3D pawn, Camera3D camera,
        bool release, double seconds, double delay = 0)
    {
        _clock ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded,cull_disabled,blend_add,depth_draw_never;
uniform float progress=0.0;
uniform bool release=false;
void vertex(){
 MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0]*length(MODEL_MATRIX[0].xyz),
 INV_VIEW_MATRIX[1]*length(MODEL_MATRIX[1].xyz),INV_VIEW_MATRIX[2]*length(MODEL_MATRIX[2].xyz),MODEL_MATRIX[3]);
}
float line(vec2 p,vec2 end,float width){
 float along=clamp(dot(p,end)/dot(end,end),0.0,1.0);
 return exp(-pow(length(p-end*along)/width,2.0));
}
void fragment(){
 vec2 p=UV*2.0-1.0;
 float q=progress;
 float r=length(p), a=atan(p.y,p.x);
 float turn=release ? smoothstep(0.0,0.65,q) : smoothstep(0.0,0.6,q)*0.16;
 float hand=-1.570796+turn*6.283185;
 float ticks=pow(max(0.0,cos(a*12.0)),28.0);
 float rim=exp(-pow((r-0.71)/0.012,2.0))*0.45;
 rim+=ticks*smoothstep(0.54,0.59,r)*(1.0-smoothstep(0.67,0.70,r));
 float hands=line(p,vec2(cos(hand),sin(hand))*0.51,0.020);
 hands+=line(p,vec2(cos(hand*0.25-0.8),sin(hand*0.25-0.8))*0.32,0.028);
 float sweep=mod(hand-a+6.283185,6.283185);
 float arc=exp(-pow((r-0.82)/0.027,2.0))*exp(-sweep*2.2);
 float opening=smoothstep(0.0,0.12,q);
 float fade=1.0-smoothstep(release ? 0.48 : 0.72,1.0,q);
 float glow=(rim+hands+arc*1.6)*opening*fade;
 // 解放時は光紋が外へほどける。輪を防壁として残さない。
 if(release){
  float rays=pow(max(0.0,cos(a*12.0-q*3.0)),20.0);
  glow+=rays*exp(-pow((r-(0.45+q*0.50))/0.10,2.0))*sin(q*3.14159)*0.7;
 }
 ALBEDO=mix(vec3(1.0,0.56,0.12),vec3(1.0,0.94,0.65),clamp(hands,0.0,1.0));
 EMISSION=ALBEDO*1.8;
 ALPHA=clamp(glow,0.0,0.9);
}" };
        var material = new ShaderMaterial { Shader = _clock };
        material.SetShaderParameter("release", release);
        var node = FireFx.Card(root, root.ToLocal(pawn.FxPoint), Vector2.One * 2.3f, material);
        node.Hide();
        var tween = node.CreateTween();
        if (delay > 0) tween.TweenInterval(delay);
        tween.TweenMethod(Callable.From<float>(p => {
            if (!GodotObject.IsInstanceValid(pawn) || !pawn.IsInsideTree() || pawn.Hp <= 0)
            { node.Hide(); return; }
            node.Show();
            node.Position = root.ToLocal(pawn.FxPoint + camera.GlobalBasis.Z * 0.22f);
            node.Scale = Vector3.One * (release ? 1 + p * 0.45f : 1.18f - Mathf.Min(1, p * 3) * 0.18f);
            material.SetShaderParameter("progress", p);
        }), 0f, 1f, Math.Max(0.015, seconds));
        tween.TweenCallback(Callable.From(node.QueueFree));
    }

    internal static void Transfer(Node3D root, Vector3 from, Vector3 to, Camera3D camera, double seconds)
    {
        if (from.DistanceSquaredTo(to) < 0.001f) return;
        _ribbon ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded,cull_disabled,blend_add,depth_draw_never;
uniform float progress=0.0;
void fragment(){
 float q=progress;
 float x=UV.x, y=abs(UV.y*2.0-1.0);
 float fill=smoothstep(x,x+0.10,q*1.8);
 float fade=1.0-smoothstep(0.65,1.0,q);
 float edge=exp(-pow((y-0.73)/0.075,2.0));
 float chevron=pow(max(0.0,cos((x-q*1.6)*48.0-y*3.0)),14.0);
 float silk=(0.12+chevron*0.60)*(1.0-smoothstep(0.55,1.0,y));
 ALBEDO=mix(vec3(1.0,0.35,0.04),vec3(1.0,0.88,0.46),edge+chevron*0.65);
 EMISSION=ALBEDO*1.7;
 ALPHA=(edge*0.6+silk)*fill*fade*smoothstep(0.0,0.1,q);
}" };
        for (int layer = 0; layer < 2; layer++)
        {
            var material = new ShaderMaterial { Shader = _ribbon };
            var mesh = new ImmediateMesh();
            mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
            for (int i = 0; i <= 64; i++)
            {
                float p = i / 64f;
                float bow = Mathf.Sin(p * Mathf.Pi);
                Vector3 center = from.Lerp(to, p) + Vector3.Up * bow * (layer == 0 ? 0.9f : -0.35f)
                    + camera.GlobalBasis.X * Mathf.Sin(p * Mathf.Tau) * bow * 0.12f;
                Vector3 side = (to - from).Cross(camera.GlobalBasis.Z).Normalized();
                float width = (0.13f + bow * 0.12f) * (layer == 0 ? 1 : 0.65f);
                for (int k = 0; k < 2; k++)
                {
                    mesh.SurfaceSetUV(new(p, k));
                    mesh.SurfaceAddVertex(root.ToLocal(center + side * width * (k == 0 ? -1 : 1)));
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
    }
}
