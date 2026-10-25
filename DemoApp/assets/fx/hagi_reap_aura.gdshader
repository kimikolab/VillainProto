shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, depth_test_disabled;

uniform float age = 0.0;
uniform float slash_width = 5.0;

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
        mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0)), f.x), f.y);
}

void fragment() {
    float x = (UV.x - 0.5) * 2.0;
    // 芯と同じ世界座標の弧。上下の炎が伸びても芯の位置は揺らさない。
    float d = (UV.y - 0.5) * 3.6 + 0.59625 * (1.0 - x * x);
    float taper = pow(max(0.0, 1.0 - x * x), 0.65);
    float t = age;
    float head = mix(-1.2, 1.3, smoothstep(0.0, 0.085, t));
    float reveal = 1.0 - smoothstep(head - 0.12, head, x);
    // TIMEを使わず再生時計で移流。停止・倍速・反転でも同じ炎を保つ。
    float u = x * slash_width * 1.75;
    vec2 flow = vec2(u + d * 1.8 - t * 4.8, d * 3.0 + t * 6.0);
    float broad = noise(flow);
    float curl = noise(flow * 2.1 + broad * 2.8);
    float fine = noise(flow * 4.6 - t * 3.0);
    float plume = broad * 0.56 + curl * 0.30 + fine * 0.14;
    // 上側は長い火舌、下側は短い巻き返し。境界を鋭くして煙と区別する。
    float reach = (d < 0.0 ? 1.50 : 0.66) * taper;
    float field = plume - abs(d) / max(0.025, reach) - 0.12;
    float body = smoothstep(-0.025, 0.09, field);
    float filaments = exp(-abs(field - 0.025) * 42.0) * 0.24;
    float root = exp(-abs(d) * 14.0);
    float bloom = exp(-abs(d) * 6.0) * 0.14;
    float pulse = (1.0 - exp(-t * 120.0)) * exp(-t * 5.4);
    float rim = smoothstep(0.025, 0.30, abs(d));
    // 内側の明るい朱から外縁の深紅へ。芯の白を赤い炎で縁取る。
    // 外側は透過合成で背景の緑を抑え、明るい草原でも深紅を保つ。
    vec3 flame = mix(vec3(1.0, 0.16, 0.08), vec3(0.70, 0.006, 0.035), rim);
    ALBEDO = flame;
    EMISSION = flame * (1.3 + root * 1.8);
    ALPHA = (body * 0.72 + filaments + root * 0.28 + bloom) * taper * reveal * pulse;
}
