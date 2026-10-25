shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

uniform float age = 0.0;
uniform float stage = 0.0;

float hash(vec2 p) {
    return fract(sin(dot(p,vec2(127.1,311.7)))*43758.5453);
}
float noise(vec2 p) {
    vec2 i=floor(p),f=fract(p);f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1.0,0.0)),f.x),
        mix(hash(i+vec2(0.0,1.0)),hash(i+vec2(1.0)),f.x),f.y);
}
void fragment() {
    float u=UV.x,d=UV.y,s=clamp(stage,0.0,3.0);
    float taper=pow(max(0.0,sin(u*3.14159265)),0.42);
    float core=exp(-pow(d/(0.0035+s*0.0015),2.0));
    float coat=exp(-abs(d)*105.0);
    vec2 flow=vec2(u*29.0-age*(1.6+s*.35)+d*22.0,d*42.0+age*2.0);
    float broad=noise(flow),curl=noise(flow*2.1+broad*2.0);
    float plume=broad*.67+curl*.33;
    float reach=(.017+s*.025)*(.25+plume*.90)*taper;
    float flame=(1.0-smoothstep(reach*.42,max(.001,reach),max(0.0,d)))
        *smoothstep(-.006,.008,d);
    float filament=exp(-pow((d-reach*.72)/.0065,2.0))*smoothstep(.38,.72,plume);
    float heat=.90+sin(age*3.1+u*5.0)*.10;
    float strength=clamp(s,0.0,1.0)*taper*heat;
    // 魂3は赤熱、6から白い刃先が現れ、9は深紅の火舌を強くまとわせる。
    float white=core*clamp((s-.50)/2.50,0.0,1.0);
    vec3 red=mix(vec3(.72,.003,.022),vec3(1.0,.10,.025),coat);
    vec3 light=mix(red,vec3(1.0,.98,.91),white);
    ALBEDO=light;
    EMISSION=light*(1.1+s*.45+coat*.8)+vec3(1.0,.16,.035)*filament*1.2;
    ALPHA=clamp((core*.83+coat*.24+flame*.64+filament*.27)*strength,0.0,.90);
}
