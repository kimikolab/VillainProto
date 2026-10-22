using Godot;
using System;

// 毒の在庫を立ち絵に張り付く嚢で描く。独自の時計で停止・倍速に追従する。
public partial class PoisonEffect3D : Node3D
{
    private static Shader? _shader;
    private ShaderMaterial _material = null!;
    private MeshInstance3D _surface = null!;
    private Sprite3D? _portrait;
    private float _clock, _burstAge = 10, _burstSeconds = 1;
    private int _waves = 1;
    private bool _active, _cleared;
    internal bool Paused;
    internal double Speed = 1;
    internal int Amount { get; private set; }
    internal bool Mantle { get; private set; }
    internal bool Lethal { get; private set; }
    internal int BurstWaves { get; private set; }

    public void Configure(float phase)
    {
        _clock = phase;
        _shader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded,cull_disabled,blend_mix,depth_draw_never;
uniform sampler2D portrait : source_color,filter_linear;
uniform bool flip=false;
uniform bool mantle=false;
uniform bool lethal=false;
uniform bool leak=false;
uniform float clock=0.0;
uniform float density=0.0;
uniform float burst=-1.0;
uniform float aspect=1.0;
float hash(float n){return fract(sin(n*127.1+31.7)*43758.5);}
float body(vec2 uv){
 if(any(lessThan(uv,vec2(0.0)))||any(greaterThan(uv,vec2(1.0))))return 0.0;
 vec4 c=texture(portrait,vec2(flip?1.0-uv.x:uv.x,uv.y));
 vec4 corner=texture(portrait,vec2(0.01));
 float mask=corner.a<0.1?1.0:smoothstep(0.06,0.20,distance(c.rgb,corner.rgb));
 return smoothstep(0.12,0.8,c.a*mask);
}
void vertex(){MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0]*length(MODEL_MATRIX[0].xyz),INV_VIEW_MATRIX[1]*length(MODEL_MATRIX[1].xyz),INV_VIEW_MATRIX[2],MODEL_MATRIX[3]);}
void fragment(){
 float mask=body(UV),near_body=0.0;
 for(int j=0;j<8;j++){
  float a=float(j)*0.785398;
  near_body=max(near_body,body(UV+vec2(cos(a)/aspect,sin(a))*(0.008+density*0.012)));
 }
 float rim=max(0.0,near_body-mask);
 float pulse=0.6+0.4*sin(clock*4.8);
 float membrane=mantle?mask*smoothstep(0.17,0.30,UV.y)*(0.16+density*0.32):0.0;
 membrane+=mantle?rim*(0.5+density*0.5):0.0;
 float bubbles=0.0,shine=0.0;
 for(int i=0;i<32;i++){
  float k=float(i);
  if(density<=0.0||k>5.0+density*25.0)continue;
  vec2 center=vec2(0.18+hash(k+3.0)*0.64,0.29+hash(k+50.0)*0.55);
  if(body(center)<0.7)continue;
  vec2 q=(UV-center)*vec2(aspect,1.0);
  float radius=(0.023+density*0.020+hash(k+80.0)*0.01)*(1.0+sin(clock*1.8+k)*0.07);
  if(burst>=0.0){
   float edge=radius+burst*(0.10+density*0.06);
   bubbles=max(bubbles,(1.0-smoothstep(0.003,0.012,abs(length(q)-edge)))*(1.0-burst));
  }else{
   float d=length(q)/radius;
   float sphere=1.0-smoothstep(0.84,1.0,d);
   float h=exp(-dot((q/radius-vec2(-0.30,-0.35))*4.0,(q/radius-vec2(-0.30,-0.35))*4.0));
   bubbles=max(bubbles,sphere*(0.40+0.50*smoothstep(0.5,1.0,d))); shine=max(shine,h*sphere);
  }
 }
 if(mantle)bubbles*=0.38;
 float vapor=0.0;
 if(leak){
  for(int i=0;i<5;i++){
   float k=float(i),life=fract(clock*0.6+k*0.18);
   vec2 origin=vec2(flip?0.20:0.80,0.25);
   vec2 at=origin+vec2((flip?-1.0:1.0)*(life*0.09+sin(clock*2.0+k)*0.015),-life*0.12);
   vec2 d=(UV-at)*vec2(aspect,1.0)/(0.014+life*0.025);
   vapor=max(vapor,exp(-dot(d,d))*sin(life*3.14159)*0.45);
  }
 }
 vec3 color=mix(vec3(0.27,0.035,0.43),vec3(0.76,0.31,1.0),clamp(bubbles+shine,0.0,1.0));
 if(mantle)color=mix(vec3(0.11,0.015,0.20),vec3(0.42,0.06,0.68),clamp(bubbles+shine,0.0,0.6));
 float doom=lethal?rim*(0.7+pulse*0.3):0.0;
 ALBEDO=mix(color,vec3(0.94,0.65,1.0),shine); EMISSION=ALBEDO*(0.45+shine+doom);
 ALPHA=clamp(membrane+bubbles*0.85+shine*0.7+doom+vapor,0.0,0.88);
}" };
        _material = new ShaderMaterial { Shader = _shader, RenderPriority = 5 };
        _surface = new MeshInstance3D { Mesh = new QuadMesh(), MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_surface); Visible = false;
    }

    internal void Track(Sprite3D portrait, int amount, bool mantle, bool lethal, bool leak)
    {
        _cleared = false; _portrait = portrait;
        Amount = Math.Max(0, amount); Mantle = mantle && Amount > 0; Lethal = lethal;
        _material.SetShaderParameter("mantle", Mantle);
        _material.SetShaderParameter("lethal", lethal);
        _material.SetShaderParameter("leak", leak);
        _material.SetShaderParameter("density", Amount == 0 ? 0f : Math.Clamp((float)Math.Log(1.0 + Amount) / 8f, 0.08f, 1f));
        Visible = _active || leak || lethal || _burstAge < _burstSeconds;
        FollowPortrait();
    }
    public void SetActive(bool active) { _active = active; if (!_cleared) Visible = active || _burstAge < _burstSeconds; }
    public void Pulse() => Burst(0.55 / Math.Max(0.1, Speed), 1);
    internal void Burst(double seconds, int waves)
    {
        _burstAge = 0; _burstSeconds = (float)Math.Max(0.015, seconds * Speed);
        _waves = Math.Clamp(waves, 1, 64); BurstWaves += Math.Max(1, waves); Visible = true;
    }
    public void Clear() { _active = false; _cleared = true; Amount = 0; Lethal = Mantle = false; Visible = false; _burstAge = float.PositiveInfinity; }
    private void FollowPortrait()
    {
        if (_portrait?.Texture is not { } texture) return;
        _surface.Transform = _portrait.Transform;
        _surface.Position += Vector3.Back * 0.015f;
        ((QuadMesh)_surface.Mesh).Size = new Vector2(texture.GetWidth(), texture.GetHeight()) * _portrait.PixelSize;
        _material.SetShaderParameter("portrait", texture);
        _material.SetShaderParameter("flip", _portrait.FlipH);
        _material.SetShaderParameter("aspect", texture.GetWidth() / (float)texture.GetHeight());
    }
    public override void _Process(double delta)
    {
        if (_cleared) return;
        if (!Paused) { _clock += (float)(delta * Speed); _burstAge += (float)(delta * Speed); }
        _material.SetShaderParameter("clock", _clock);
        _material.SetShaderParameter("burst", _burstAge < _burstSeconds ? Mathf.Min(0.999f, (_burstAge / _burstSeconds * _waves) % 1f) : -1f);
        FollowPortrait();
    }
}
