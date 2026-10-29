shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never;

uniform float age = 0.0;
uniform float gain = 0.0;
uniform bool billboard = false;

void fragment() {
    float r = length((UV - 0.5) * 2.0);
    float pulse = 0.5 + 0.5 * sin(age * 8.0);
    float pool = exp(-r * r * 4.8) * 0.50;
    float halo = exp(-pow((r - 0.67 - pulse * 0.025) / 0.018, 2.0)) * 0.20;
    float travel = fract(age * 0.55);
    float wave = exp(-pow((r - travel * 0.94) / 0.033, 2.0)) * sin(travel * PI) * 0.14;
    ALBEDO = vec3(1.0, 0.78, 0.40);
    EMISSION = vec3(1.0, 0.78, 0.40) * 0.35;
    ALPHA = (pool + halo + wave) * gain * (1.0 - smoothstep(0.90, 1.0, r));
}
