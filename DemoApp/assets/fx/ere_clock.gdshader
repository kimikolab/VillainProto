shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never, depth_test_disabled;

uniform vec4 tint : source_color;
uniform sampler2D score : filter_linear, repeat_disable;
uniform float opacity = 0.0;
uniform float turns = 0.0;
uniform float orbit = 0.0;
uniform float flare = 0.0;
uniform float ripple = 0.0;
uniform bool billboard = true;

void vertex() {
    if (billboard) MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
        INV_VIEW_MATRIX[0] * length(MODEL_MATRIX[0].xyz),
        INV_VIEW_MATRIX[1] * length(MODEL_MATRIX[1].xyz),
        INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}

float stroke(float distance, float width, float aa) {
    return 1.0 - smoothstep(width, width + aa, abs(distance));
}

float hand(vec2 p, float angle, float reach, float width, float aa) {
    vec2 direction = vec2(sin(angle), -cos(angle));
    float along = dot(p, direction);
    float across = abs(dot(p, vec2(direction.y, -direction.x)));
    float taper = width * (1.0 - 0.65 * clamp(along / reach, 0.0, 1.0));
    return (1.0 - smoothstep(taper, taper + aa, across))
        * smoothstep(-0.12, -0.10, along) * (1.0 - smoothstep(reach - aa, reach, along));
}

void fragment() {
    vec2 p = (UV - 0.5) * 2.0;
    float r = length(p);
    float aa = max(fwidth(r), 0.002);
    float a = mod(atan(p.x, -p.y) + TAU, TAU);
    float angle = turns * TAU;
    float behind = mod(angle - a + TAU, TAU);
    float sweep = exp(-behind * 2.4);

    // 赤は金の音符と蔓飾り、青は円形の五線譜。文字盤は正立したまま保つ。
    vec4 face = texture(score, UV);
    float music = face.r * face.a;
    float staff = face.b * face.a;
    float trail = stroke(r - 0.865, 0.013, aa) * sweep;
    float hands = max(hand(p, angle, 0.625, 0.018, aa),
        hand(p, angle / 12.0 - 0.75, 0.39, 0.024, aa));
    float echoes = 0.0;
    for (int i = 1; i <= 3; i++) {
        echoes += hand(p, angle - float(i) * 0.17, 0.625, 0.009, aa)
            * (0.15 - float(i) * 0.033);
    }
    float hub = stroke(abs(p.x) + abs(p.y) - 0.052, 0.006, aa);
    float halo = exp(-pow((r - 0.855) / 0.045, 2.0)) * (0.07 + sweep * 0.15);
    float wave = stroke(r - (0.20 + ripple * 0.77), 0.005, aa)
        * sin(clamp(ripple, 0.0, 1.0) * PI) * 0.65;

    float silver = staff * 0.48 + trail * 0.70 + hands + echoes + halo + wave;
    float gold = music * (0.86 + 0.10 * sin(orbit + a * 3.0)) + hub;
    vec3 warm = vec3(1.0, 0.81, 0.47);
    vec3 color = mix(tint.rgb, warm, clamp(gold / max(silver + gold, 0.001), 0.0, 1.0));
    float mask = clamp(silver + gold, 0.0, 1.0);
    ALBEDO = color;
    EMISSION = color * (0.35 + flare * 0.55);
    ALPHA = mask * opacity * (1.0 - smoothstep(0.98, 1.0, r));
}
