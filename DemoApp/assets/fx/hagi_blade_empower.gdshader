shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

uniform float age = 0.0;
uniform float stage = 0.0;

void fragment() {
    float u=UV.x,d=UV.y,s=clamp(stage,0.0,3.0);
    float taper=pow(max(0.0,sin(u*3.14159265)),0.42);
    float core=exp(-pow(d/(0.0035+s*0.0015),2.0));
    float coat=exp(-abs(d)*105.0);
    // 段ごとに鋭い赤い帯を広げる。時間で揺らがせない。
    float reach=(.017+s*.025)*taper;
    float edge=(1.0-smoothstep(reach*.65,max(.001,reach),max(0.0,d)))
        *smoothstep(-.006,.008,d);
    float filament=exp(-pow((d-reach*.72)/.005,2.0));
    float strength=clamp(s,0.0,1.0)*taper;
    float white=core*clamp((s-.50)/2.50,0.0,1.0);
    vec3 red=mix(vec3(.72,.003,.022),vec3(1.0,.10,.025),coat);
    vec3 light=mix(red,vec3(1.0,.98,.91),white);
    ALBEDO=light;
    EMISSION=light*(1.1+s*.45+coat*.8)+vec3(1.0,.16,.035)*filament*1.2;
    ALPHA=clamp((core*.83+coat*.24+edge*.64+filament*.27)*strength,0.0,.90);
}
