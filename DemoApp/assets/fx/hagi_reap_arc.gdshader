shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, depth_test_disabled;

uniform float age = 0.0;
uniform float echo = 0.0;

void fragment() {
    vec2 p = (UV - 0.5) * 2.0;
    float t = max(0.0, age - echo * 0.035);
    float curve = p.y + (0.53 + echo * 0.04) * (1.0 - p.x * p.x) + echo * 0.06;
    float taper = pow(max(0.0, 1.0 - p.x * p.x), 0.55);
    float head = mix(-1.2, 1.3, smoothstep(0.0, 0.085, t));
    float reveal = 1.0 - smoothstep(head - 0.12, head, p.x);
    // 芯を白熱させ、炎の輪郭とは独立した一続きの斬線として残す。
    float core = exp(-pow(abs(curve) * 42.0, 2.0));
    float blade = (1.0 - smoothstep(0.018, 0.10, abs(curve + 0.018))) * 0.85;
    float wake = exp(-abs(curve) * 10.0) * 0.28;
    float pulse = (1.0 - exp(-t * 140.0)) * exp(-t * (5.2 - echo));
    float glint = pow(max(0.0, 1.0 - abs(p.x - (head - 0.18)) * 6.0), 4.0);
    // 白熱する芯から朱色の残光へ。弧の形と発光の時間は銀白版と共通。
    vec3 light = mix(vec3(1.0, 0.035, 0.055), vec3(1.0, 0.985, 0.94), core);
    ALBEDO = light;
    EMISSION = light * (2.0 + core * 5.5);
    ALPHA = (core * 1.8 + blade + wake + glint * core * 0.8) * taper * reveal * pulse * (1.0 - echo * 0.78);
}
