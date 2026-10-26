shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

uniform float age = 0.0;
uniform float slash_width = 12.0;
uniform float power = 1.0;

void fragment() {
    float u=UV.x,d=(UV.y-0.5)*2.0,t=age;
    float angle=-3.14159265*0.94+u*6.2831853*0.94;
    float front=smoothstep(-0.4,0.7,sin(angle));
    float tip=pow(max(0.0,sin(u*3.14159265)),0.48);
    float belly=pow(max(0.0,sin(angle)),0.75)*tip;
    // 三日月の刃に沿う細い赤い帯。炎のようには揺らがせない。
    d+=(0.028+belly*0.25)*(0.60*2.0/0.90);
    float taper=smoothstep(0.0,0.055,u)*(1.0-smoothstep(0.88,1.0,u));
    float head=mix(-0.05,1.09,smoothstep(0.0,0.105,t));
    float reveal=1.0-smoothstep(head-0.06,head,u);
    float root=exp(-abs(d)*26.0);
    float ribbon=exp(-pow((d+0.08*front)/0.07,2.0))*front;
    float pulse=(1.0-exp(-t*125.0))*exp(-t*5.8);
    vec3 red=mix(vec3(0.72,0.004,0.025),vec3(1.0,0.12,0.035),root);
    ALBEDO=red;
    EMISSION=red*(2.0+root*2.2+ribbon);
    ALPHA=clamp((root*0.65+ribbon*0.45)*taper*reveal*pulse*mix(0.18,1.0,front)*power,0.0,0.92);
}
