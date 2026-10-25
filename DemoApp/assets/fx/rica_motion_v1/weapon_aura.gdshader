shader_type canvas_item;
render_mode unshaded, blend_add;

// 白熱した光の芯・色の縁・柔らかな外光。物理シャベルより後ろに描く。
uniform int shape : hint_range(1,3) = 2;
uniform vec4 tint : source_color = vec4(0.63,0.36,0.96,1.0);
uniform float phase = 0.0;
uniform float power : hint_range(0.0,2.0) = 1.0;
uniform float reveal : hint_range(0.0,1.0) = 1.0;

void fragment() {
    float x=UV.x;
    float y=(UV.y-0.5)*2.0;
    float body=smoothstep(0.035,0.14,x)*(1.0-smoothstep(0.70,0.98,x));
    float width=0.52*sqrt(max(0.0,body));
    if(shape==3) width=0.24*pow(max(0.0,body),0.65);
    float d=abs(y)/max(0.008,width);
    float core=1.0-smoothstep(0.20,0.68,d);
    float mantle=exp(-d*d*2.7);
    float halo=exp(-d*d*0.80)*0.24;
    if(shape==1) {
        vec2 q=vec2((x-0.42)/0.54,y/0.79);
        float radius=length(q);
        float opening=smoothstep(-0.80,-0.25,q.x);
        float band=abs(radius-0.79)/0.14;
        core=(1.0-smoothstep(0.20,0.65,band))*opening;
        mantle=exp(-band*band*1.15)*opening;
        halo=exp(-band*band*0.22)*0.34*opening;
    }
    float pulse=0.95+0.05*sin(phase*8.0-x*13.0);
    float ends=smoothstep(0.015,0.07,x)*(1.0-smoothstep(0.97,1.0,x));
    float visible=1.0-smoothstep(reveal-0.035,reveal+0.01,x);
    float border=1.0-smoothstep(0.65,0.99,abs(y));
    float light=clamp(core+mantle*0.66+halo,0.0,1.0)*ends*visible*border;
    vec3 edge_color=mix(pow(tint.rgb,vec3(2.2)),vec3(0.04,0.48,1.0),0.40);
    vec3 heat=mix(edge_color,vec3(0.96,0.985,1.0),core);
    COLOR=vec4(heat*(1.2+core*1.6)*pulse,light*power*tint.a);
}
