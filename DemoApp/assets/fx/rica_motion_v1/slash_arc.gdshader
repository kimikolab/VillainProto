shader_type spatial;
render_mode unshaded,blend_add,cull_disabled,depth_draw_never,depth_test_disabled;
uniform float progress=0.0;
uniform int kind=0;
uniform float offset=0.0;
uniform float thickness=1.0;
uniform vec4 tint:source_color;

void fragment(){
    vec2 p=(UV-0.5)*2.0;
    float t=clamp(progress,0.0,1.0);
    float fade=smoothstep(0.0,0.025,t)*(1.0-smoothstep(0.13,1.0,t));
    float core=0.0, veil=0.0, threads=0.0;
    if(kind==1){
        float taper=pow(max(0.0,1.0-abs(p.x)),0.35);
        core=exp(-pow(p.y*44.0/thickness,2.0))*taper;
        veil=exp(-pow(p.y*9.0/thickness,2.0))*taper;
        threads=(exp(-abs(p.y-0.10*sin(p.x*3.0))*80.0)+exp(-abs(p.y+0.14)*80.0))*taper*0.32;
    }else if(kind==4){
        float r=length(p),a=atan(p.y,p.x);
        float tooth=0.27+0.055*sin(a*7.0)+0.035*cos(a*11.0);
        core=(1.0-smoothstep(tooth,tooth+0.045,r))*exp(-max(0.0,t-0.18)*5.5);
        veil=exp(-abs(r-(0.20+t*0.56))*48.0)*0.55*(1.0-t);
        threads=pow(abs(cos(a*5.0+0.3)),22.0)*smoothstep(0.20,0.38,r)
            *(1.0-smoothstep(0.50,0.95,r))*exp(-t*4.0);
    }else if(kind==2){
        float r=length(p);
        float rays=max(exp(-abs(p.x)*80.0),exp(-abs(p.y)*80.0));
        float diagonal=max(exp(-abs(p.x-p.y)*110.0),exp(-abs(p.x+p.y)*110.0))*0.55;
        core=(rays+diagonal)*exp(-r*3.0);
        veil=exp(-r*r*18.0)*0.65;
        fade*=1.0-smoothstep(0.03,0.36,t);
    }else if(kind==3){
        float d=abs(length(p)-mix(0.24,0.86,sqrt(t)));
        core=exp(-pow(d*65.0/thickness,2.0));
        veil=exp(-pow(d*19.0/thickness,2.0))*0.55;
        threads=exp(-abs(d-0.08)*110.0)*0.35;
    }else{
        float theta=atan(p.y,p.x);
        float head=-1.8+4.5*smoothstep(0.0,0.45,t)+offset;
        float behind=mod(head-theta+12.56637,6.283185);
        float window=smoothstep(0.0,0.16,behind)*(1.0-smoothstep(3.1,3.9,behind));
        float taper=pow(max(0.0,sin(clamp(behind/3.9,0.0,1.0)*3.14159)),0.42);
        float r=length(p);
        float radius=0.76+0.025*sin(theta*2.0+offset);
        float d=r-radius;
        core=exp(-pow(d/((0.010+0.014*taper)*thickness),2.0));
        float inner=exp(-pow((d+0.065*taper*thickness)/((0.018+0.10*taper)*thickness),2.0))*0.72;
        veil=exp(-pow(d/(0.105*thickness),2.0))*0.46+inner;
        threads=exp(-abs(d-0.065)*130.0)*0.68+exp(-abs(d+0.17*thickness)*95.0)*0.24;
        float streaks=0.90+0.10*sin(theta*13.0+r*7.0-t*10.0);
        core*=window*taper;veil*=window*taper*streaks;threads*=window*taper;
    }
    float light=core+veil*0.72+threads*0.62;
    vec3 rim=mix(pow(tint.rgb,vec3(2.4)),vec3(0.02,0.48,1.0),0.35);
    if(kind==4)rim=vec3(1.0,0.62,0.23);
    ALBEDO=mix(rim*0.40,vec3(0.96,0.99,1.0),clamp(core,0.0,1.0));
    EMISSION=(rim*(veil+threads)*0.9+vec3(0.93,0.98,1.0)*core*3.0);
    float border=1.0-smoothstep(0.90,1.0,max(abs(p.x),abs(p.y)));
    ALPHA=clamp(light,0.0,1.0)*fade*border;
}
