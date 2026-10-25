shader_type spatial;
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never;

uniform float age = 0.0;
uniform float slash_width = 12.0;
uniform float power = 1.0;

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
    float u = UV.x, d = (UV.y - 0.5) * 2.0, t = age;
    float angle = -3.14159265 * 0.94 + u * 6.2831853 * 0.94;
    float front = smoothstep(-0.4, 0.7, sin(angle));
    float tip = pow(max(0.0, sin(u * 3.14159265)), 0.48);
    float belly = pow(max(0.0, sin(angle)), 0.75) * tip;
    // 芯の三日月の内縁へ炎の根元を揃える（芯の幅0.60、炎の幅0.90）。
    d += (0.028 + belly * 0.25) * (0.60 * 2.0 / 0.90);
    float taper = smoothstep(0.0, 0.055, u) * (1.0 - smoothstep(0.88, 1.0, u));
    float head = mix(-0.05, 1.09, smoothstep(0.0, 0.105, t));
    float reveal = 1.0 - smoothstep(head - 0.06, head, u);
    // 円弧の内側へ炎を流す。TIMEを使わず、停止・倍速を斬線と共有する。
    vec2 flow = vec2(u * slash_width * 1.5 - t * 7.0 + d * 2.2, d * 3.4 + t * 5.0);
    float broad = noise(flow);
    float curl = noise(flow * 2.1 + broad * 2.8);
    float fine = noise(flow * 4.6 - t * 3.0);
    float plume = broad * 0.56 + curl * 0.30 + fine * 0.14;
    float reach = (d < 0.0 ? 1.48 : 0.54) * mix(0.25, 1.0, front);
    float field = plume - abs(d) / max(0.025, reach) - 0.12;
    float body = smoothstep(-0.025, 0.09, field);
    float filaments = exp(-abs(field - 0.025) * 34.0) * 0.38;
    // 内側に巻く細い火の筋。太い帯の均一な縁を崩し、刈り抜きの流れを出す。
    float curl_line = 0.26 + sin(u * 42.0 - t * 10.0) * 0.13
        + sin(u * 79.0 + t * 6.0) * 0.06;
    float streak = exp(-pow((d + curl_line * front) / 0.065, 2.0))
        * smoothstep(0.27, 0.59, plume) * front;
    float tongue = exp(-pow((d + (curl_line + 0.20) * front) / 0.10, 2.0))
        * smoothstep(0.38, 0.65, plume) * front;
    // 奥へ巻くもう一筋を離して重ね、内縁から伸びる火舌を読み取りやすくする。
    float inner_curl = 0.53 + sin(u * 31.0 + t * 8.0) * 0.15;
    float flare = exp(-pow((d + inner_curl * front) / 0.085, 2.0))
        * smoothstep(0.39, 0.68, plume) * front;
    float root = exp(-abs(d) * 15.0);
    float bloom = exp(-abs(d) * 6.0) * 0.18;
    float pulse = (1.0 - exp(-t * 125.0)) * exp(-t * 4.8);
    float rim = smoothstep(0.025, 0.30, abs(d));
    vec3 flame = mix(vec3(1.0, 0.12, 0.035), vec3(0.86, 0.004, 0.025), rim);
    ALBEDO = flame;
    EMISSION = flame * (2.0 + root * 2.2) + vec3(1.0, 0.16, 0.035) * (streak * 3.2 + flare * 1.8);
    ALPHA = clamp((body * 0.90 + filaments + streak * 1.05 + tongue * 0.75 + flare * 0.60 + root * 0.35 + bloom)
        * taper * reveal * pulse * mix(0.18, 1.0, front) * power, 0.0, 0.92);
}
