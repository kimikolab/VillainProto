shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, depth_test_disabled;

uniform float age = 0.0;
uniform float release = 0.0;
uniform int layer = 0;
uniform float power = 1.0;

void fragment() {
    vec2 p = (UV - 0.5) * 2.0;
    float glow = 0.0;
    vec3 tint = vec3(1.0, 0.94, 0.86);
    if (layer == 0) {
        // 命中点に一瞬だけ出る細い閃光。画面全体を白く塗らない。
        float pulse = (1.0 - exp(-age * 170.0)) * exp(-age * 16.0) * release;
        float ray = exp(-abs(p.y) * 140.0) * exp(-abs(p.x) * 1.3);
        ray += exp(-abs(p.x) * 160.0) * exp(-abs(p.y) * 3.0) * 0.66;
        ray += exp(-abs(p.y+p.x*0.65)*85.0)*exp(-dot(p,p)*1.7)*0.45;
        glow = (ray + exp(-dot(p,p) * 8.0) * 0.28) * pulse;
    } else if (layer == 1) {
        float fade = mix(smoothstep(0.0,0.10,age), exp(-age * 15.0), release);
        glow = exp(-dot(p,p) * 1.5) * fade * 0.34;
        tint = vec3(0.015,0.022,0.032);
    } else {
        tint = vec3(1.0, 0.24, 0.10);
        float pulse = (1.0 - exp(-age * 100.0)) * exp(-age * 4.7) * release;
        float shard = exp(-abs(p.x) * 17.0) * pow(max(0.0,1.0-abs(p.y)),1.5);
        glow = shard * pulse;
    }
    ALBEDO = tint;
    EMISSION = layer == 1 ? vec3(0.0) : tint * 1.7;
    ALPHA = clamp(glow * power, 0.0, 0.92);
}
