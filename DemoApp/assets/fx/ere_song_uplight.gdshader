shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never, depth_test_disabled;

uniform float age = 0.0;
uniform float gain = 0.0;
uniform bool billboard = true;

void vertex() {
    if (billboard) MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
        INV_VIEW_MATRIX[0] * length(MODEL_MATRIX[0].xyz),
        INV_VIEW_MATRIX[1] * length(MODEL_MATRIX[1].xyz),
        INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}

void fragment() {
    float h = 1.0 - UV.y;
    float x = (UV.x - 0.5) * 2.0;
    float width = 0.25 + h * 0.58;
    float fan = 1.0 - smoothstep(width * 0.12, width, abs(x));
    float core = exp(-pow(x / (0.12 + h * 0.30), 2.0));
    float rays = exp(-pow((abs(x) - (0.08 + h * 0.42)) / 0.032, 2.0));
    float falloff = exp(-h * 2.1);
    // 下から照らす薄い光。顔の手前で消し、衣装の陰影を残す。
    float fade = smoothstep(0.0, 0.035, h) * (1.0 - smoothstep(0.48, 0.95, h));
    float breath = 0.92 + 0.08 * sin(age * 4.0);
    ALBEDO = vec3(1.0, 0.83, 0.51);
    EMISSION = vec3(1.0, 0.83, 0.51) * 0.25;
    ALPHA = (fan * 0.15 + core * 0.09 + rays * 0.055) * falloff * fade * gain * breath;
}
