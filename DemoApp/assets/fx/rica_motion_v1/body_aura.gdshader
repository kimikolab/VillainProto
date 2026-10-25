shader_type canvas_item;
render_mode unshaded, blend_add;

// 位相は再生側が渡す。一時停止・倍速はこの値だけで制御する（TIME は使わない）。
uniform sampler2D portrait : filter_linear, repeat_disable;
uniform vec2 portrait_size = vec2(1024.0,1536.0);
uniform vec2 eye_anchor = vec2(0.56,0.16);
uniform vec4 tint : source_color = vec4(0.64,0.40,0.96,1.0);
uniform float strength : hint_range(0.0,2.0) = 1.0;
uniform float phase = 0.0;
uniform float expansion = 1.18;

float silhouette(vec2 p) {
    if (p.x<0.0 || p.y<0.0 || p.x>1.0 || p.y>1.0) return 0.0;
    return smoothstep(0.12,0.65,texture(portrait,p).a);
}
float dilate(vec2 p, vec2 r) {
    float a=0.0;
    a=max(a,silhouette(p+vec2(r.x,0.0)));a=max(a,silhouette(p-vec2(r.x,0.0)));
    a=max(a,silhouette(p+vec2(0.0,r.y)));a=max(a,silhouette(p-vec2(0.0,r.y)));
    a=max(a,silhouette(p+r*0.707));a=max(a,silhouette(p-r*0.707));
    a=max(a,silhouette(p+vec2(r.x,-r.y)*0.707));a=max(a,silhouette(p+vec2(-r.x,r.y)*0.707));
    return a;
}
void fragment() {
    vec2 p=(UV-0.5)*expansion+0.5;
    float center=silhouette(p);
    vec2 r=vec2(7.0+strength*15.0)/portrait_size;
    float near_edge=max(0.0,dilate(p,r*0.30)-center);
    float middle=max(0.0,dilate(p,r*0.72)-center);
    float outer=max(0.0,dilate(p,r*1.5)-center);
    float mist=max(0.0,dilate(p,r*2.6)-center);
    float pulse=0.78+0.22*sin(phase*2.2+p.y*11.0);
    float streams=pow(max(0.0,sin(p.x*29.0+p.y*9.0-phase*2.5+sin(p.y*19.0)*0.7)),5.0);
    // 顔の周囲を空け、肩から下の輪郭とマントへ霊気を流す。
    float face_clear=smoothstep(eye_anchor.y+0.025,eye_anchor.y+0.13,p.y);
    float amount=(near_edge*0.23+middle*0.10+outer*0.045+mist*0.015)*(0.50+streams*0.80)*pulse*face_clear;
    COLOR=vec4(tint.rgb*1.20,amount*strength*0.55*tint.a);
}
