using Godot;

// 火を地面ではなく現在の立ち絵から発する。ポーズ・武器・左右反転にも追従する。
internal static class FireMantleFx
{
    private static Shader? _shader;
    internal static ShaderMaterial Material(bool front, float phase)
    {
        _shader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded,cull_disabled,blend_add,depth_draw_never;
uniform sampler2D portrait : source_color, filter_linear;
uniform vec4 tint : source_color;
uniform bool front=false;
uniform bool flip=false;
uniform float clock=0.0;
uniform float phase=0.0;
uniform float strength=1.0;
uniform float reach=1.0;
uniform float canvas_scale=1.3;
float hash(vec2 p){return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);}
float noise(vec2 p){vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);
 return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);}
float body(vec2 uv,vec3 bg,float alpha_bg){
 if(uv.x<0.0||uv.x>1.0||uv.y<0.0||uv.y>1.0)return 0.0;
 vec4 c=texture(portrait,vec2(flip?1.0-uv.x:uv.x,uv.y));
 float mask=mix(smoothstep(0.06,0.20,distance(c.rgb,bg)),1.0,alpha_bg);
 return c.a*mask;
}
void vertex(){
 MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0]*length(MODEL_MATRIX[0].xyz),
 INV_VIEW_MATRIX[1]*length(MODEL_MATRIX[1].xyz),INV_VIEW_MATRIX[2]*length(MODEL_MATRIX[2].xyz),MODEL_MATRIX[3]);
}
void fragment(){
 vec2 uv=(UV-0.5)*canvas_scale+0.5;
 vec4 tl=texture(portrait,vec2(0.02,0.02)),tr=texture(portrait,vec2(0.98,0.02));
 vec4 bl=texture(portrait,vec2(0.02,0.98)),br=texture(portrait,vec2(0.98,0.98));
 float alpha_bg=1.0-step(0.08,min(min(tl.a,tr.a),min(bl.a,br.a)));
 vec3 bg=mix(mix(tl.rgb,tr.rgb,uv.x),mix(bl.rgb,br.rgb,uv.x),uv.y);
 float t=clock+phase;
 float mask=body(uv,bg,alpha_bg);
 float n=noise(vec2(uv.x*23.0,uv.y*19.0+t*3.8));
 float glow=0.0,core=0.0;
 // 腰より下を火の発生源にしない。脚・地面から立つ火と明確に分ける。
 float upper=1.0-smoothstep(0.62,0.82,uv.y);
 if(!front){
  float near_body=0.0,plumes=0.0;
  for(int i=0;i<8;i++){
   float k=float(i),angle=k*6.28318/8.0;
   vec2 offset=vec2(cos(angle),sin(angle))*(0.012+n*0.026)*reach;
   near_body=max(near_body,body(uv+offset,bg,alpha_bg));
   float rise=(0.025+k*0.017)*reach;
   float curl=sin(uv.y*22.0+t*4.4+k)*rise*0.48;
   vec2 source=uv+vec2(curl,rise);
   float shape=body(source,bg,alpha_bg)*(1.0-smoothstep(0.62,0.78,source.y));
   float tongues=smoothstep(0.34,0.74,noise(vec2(uv.x*18.0+k*0.17,uv.y*11.0+t*3.2)));
   plumes=max(plumes,shape*tongues*(1.0-k/9.0));
  }
  float broken=smoothstep(0.30,0.78,n);
  glow=(near_body*broken*0.40+plumes*0.90)*(1.0-mask)*upper;
  core=near_body*(1.0-mask)*broken*0.22*upper;
 }else{
  // 装備の上を短い火の筋が流れる。顔には掛けない。
  float face_clear=smoothstep(0.27,0.39,uv.y)*(1.0-smoothstep(0.63,0.76,uv.y));
  float flow=pow(max(0.0,sin(uv.y*43.0+uv.x*11.0+t*5.0)),16.0);
  float patch=smoothstep(0.42,0.78,n);
  glow=mask*face_clear*flow*patch*0.65;
  core=glow*0.28;
  // 身体から離れた火片も胸・肩・腕の高さから。短く浮いて消える。
  for(int i=0;i<9;i++){
   float k=float(i),life=fract(t*(0.6+fract(k*0.37)*0.3)+k*0.618);
   vec2 source=vec2(0.20+fract(k*0.713)*0.62,0.32+fract(k*0.371)*0.30);
   float attached=body(source,bg,alpha_bg);
   vec2 at=source+vec2(sin(k*7.7)*life*0.10,-life*0.20)*sqrt(reach);
   vec2 d=(uv-at)*vec2(130.0,70.0);
   float ember=exp(-dot(d,d))*sin(life*3.14159)*attached;
   glow+=ember;core+=ember*0.45;
  }
 }
 vec3 color=mix(tint.rgb,vec3(1.0,0.97,0.83),clamp(core,0.0,0.75));
 ALBEDO=color;EMISSION=color*(1.2+core*1.8);
 ALPHA=clamp((glow+core)*strength,0.0,0.85);
}" };
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("front", front);
        material.SetShaderParameter("phase", phase);
        return material;
    }
}
