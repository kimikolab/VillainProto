shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform float age = 0.0;
uniform float echo = 0.0;
uniform float power = 1.0;

void fragment() {
    float u = UV.x;
    float t = max(0.0, age - echo * 0.025);
    float angle = -3.14159265 * 0.94 + u * 6.2831853 * 0.94;
    // 外周を保ち、内周を大きくえぐった三日月。中央の厚みから両端へ細く抜く。
    float front = smoothstep(-0.4, 0.7, sin(angle));
    float tip = pow(max(0.0, sin(u * 3.14159265)), 0.48);
    float belly = pow(max(0.0, sin(angle)), 0.75) * tip;
    float thickness = 0.028 + belly * 0.25;
    float d = UV.y - 0.5;
    float core = exp(-pow(d / mix(0.018, 0.040, front), 2.0));
    float outer = 1.0 - smoothstep(0.006, 0.047, d);
    float inner = smoothstep(-thickness - 0.028, -thickness + 0.020, d);
    float blade = outer * inner;
    float wake = exp(-abs(d + thickness) * 18.0) * 0.22;
    float head = mix(-0.05, 1.09, smoothstep(0.0, 0.105, t));
    float reveal = 1.0 - smoothstep(head - 0.06, head, u);
    float taper = smoothstep(0.0, 0.045, u) * (1.0 - smoothstep(0.90, 1.0, u));
    float pulse = (1.0 - exp(-t * 150.0)) * exp(-t * (6.3 - echo));
    float brightness = mix(0.40, 1.0, front);
    vec3 light = mix(vec3(1.0, 0.012, 0.025), vec3(1.0, 0.79, 0.62), clamp(core * 0.60, 0.0, 1.0));
    ALBEDO = light;
    EMISSION = light * (1.8 + core * 3.8 + blade * 2.2);
    ALPHA = (core * 1.15 + blade * 1.25 + wake) * taper * reveal * pulse
        * brightness * power * (1.0 - echo * 0.78);
}
