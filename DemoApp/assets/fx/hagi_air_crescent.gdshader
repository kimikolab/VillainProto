shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;

uniform float age = 0.0;
uniform float power = 1.0;
uniform float radial_width = 1.25;
uniform float aura = 0.0;

void fragment() {
    float u = UV.x;
    // 内周を深くえぐり、長い切っ先へ厚みを絞る。背面を結ぶ円周は描かない。
    float belly = pow(max(0.0, sin(u * 3.14159265)), 1.35);
    float thickness = 0.30 * belly;
    float d = (UV.y - 0.5) * radial_width;
    float outer = 1.0 - smoothstep(0.0, 0.008, d);
    float inner = smoothstep(-thickness - 0.008, -thickness + 0.008, d);
    float body = outer * inner;
    // 白い芯は外刃に沿う細い稜線に限定し、切っ先を丸く光らせない。
    float core = exp(-pow((d + thickness * 0.10) / (0.005 + 0.025 * belly), 2.0)) * body;
    float glow = exp(-abs(d + thickness * 0.30) / (0.012 + 0.055 * belly)) * belly;
    float taper = smoothstep(0.0, 0.035, u) * (1.0 - smoothstep(0.965, 1.0, u));
    float head = mix(-0.04, 1.08, smoothstep(0.0, 0.075, age));
    float reveal = 1.0 - smoothstep(head - 0.08, head, u);
    float pulse = (1.0 - exp(-age * 150.0)) * exp(-age * 5.4);
    vec3 red = vec3(1.0, 0.018, 0.035);
    vec3 light = mix(red, vec3(1.0, 0.96, 0.90), core * (1.0 - aura));
    ALBEDO = light;
    EMISSION = light * mix(1.6 + core * 3.8, 1.8, aura);
    ALPHA = mix(body + core * 0.25, glow * 0.16, aura)
        * taper * reveal * pulse * power;
}
