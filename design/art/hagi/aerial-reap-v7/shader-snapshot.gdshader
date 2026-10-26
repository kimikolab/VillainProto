shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform float age = 0.0;
uniform float power = 1.0;
uniform float radial_width = 1.25;
uniform float aura = 0.0;

void fragment() {
    float u = UV.x;
    // 開いた弧の中央に厚みを持たせ、両端は尖らせる。背面を結ぶ円周は描かない。
    float belly = pow(max(0.0, sin(u * 3.14159265)), 0.85);
    float thickness = 0.56 * belly;
    float d = (UV.y - 0.5) * radial_width;
    float outer = 1.0 - smoothstep(0.0, 0.035, d);
    float inner = smoothstep(-thickness - 0.025, -thickness + 0.025, d);
    float body = outer * inner;
    float core = exp(-pow((d + 0.035 + thickness * 0.16) / (0.035 + 0.065 * belly), 2.0));
    float glow = exp(-abs(d + thickness * 0.36) / (0.045 + 0.16 * belly));
    float taper = smoothstep(0.0, 0.06, u) * (1.0 - smoothstep(0.94, 1.0, u));
    float head = mix(-0.04, 1.08, smoothstep(0.0, 0.075, age));
    float reveal = 1.0 - smoothstep(head - 0.08, head, u);
    float pulse = (1.0 - exp(-age * 150.0)) * exp(-age * 5.4);
    vec3 red = vec3(1.0, 0.018, 0.035);
    vec3 light = mix(red, vec3(1.0, 0.91, 0.77), core * (1.0 - aura));
    ALBEDO = light;
    EMISSION = light * mix(2.5 + core * 4.0, 2.0, aura);
    ALPHA = mix(body * 1.2 + core * 0.35, glow * 0.35, aura)
        * taper * reveal * pulse * power;
}
