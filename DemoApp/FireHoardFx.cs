using Godot;

// 大剣の線に沿う溜め火。右向き原画の座標を使い、立ち絵の向きと差分に追従する。
internal static class FireHoardFx
{
    private static Shader? _shader;
    internal static ShaderMaterial Material()
    {
        _shader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded,cull_disabled,blend_add,depth_draw_never;
uniform bool flip=false;
uniform vec2 start=vec2(0.43,0.56);
uniform vec2 end=vec2(0.96,0.89);
uniform float strength=1.0;
uniform float clock=0.0;
void vertex(){ MODELVIEW_MATRIX=VIEW_MATRIX*mat4(
 INV_VIEW_MATRIX[0]*length(MODEL_MATRIX[0].xyz),INV_VIEW_MATRIX[1]*length(MODEL_MATRIX[1].xyz),
 INV_VIEW_MATRIX[2]*length(MODEL_MATRIX[2].xyz),MODEL_MATRIX[3]); }
void fragment(){
 vec2 uv=vec2(flip?1.0-UV.x:UV.x,UV.y);
 vec2 blade=end-start;
 float along=clamp(dot(uv-start,blade)/dot(blade,blade),0.0,1.0);
 float d=length(uv-start-blade*along);
 float pulse=0.7+0.3*sin(along*31.0-clock*7.0);
 float core=exp(-pow(d/0.014,2.0))*pulse;
 float flame=exp(-pow(d/(0.022+0.008*sin(along*48.0-clock*5.0)),2.0));
 float ends=smoothstep(0.0,0.08,along)*(1.0-smoothstep(0.9,1.0,along));
 ALBEDO=mix(vec3(0.25,0.66,1.0),vec3(0.92,0.98,1.0),core);
 EMISSION=ALBEDO*strength*2.0;
 ALPHA=clamp((core+flame*0.6)*ends*strength,0.0,0.9);
}" };
        return new ShaderMaterial { Shader = _shader };
    }
}
