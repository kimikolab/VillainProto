using Godot;

// 顔・手足のある二種の精霊。台本の陣営は変えず、契約破棄の拍だけ配色を反転する。
internal static class SomSpiritArt
{
    internal const string TurnPath = "res://assets/fx/som_spirit_turn.png";
    internal const string InterruptPath = "res://assets/fx/som_spirit_interrupt.png";
    private static Texture2D? _turn, _interrupt;
    private static Shader? _shader;
    internal static Texture2D Texture(bool emergency) => emergency
        ? _interrupt ??= UiKit.LoadTexture(InterruptPath, mipmaps: true)
        : _turn ??= UiKit.LoadTexture(TurnPath, mipmaps: true);

    internal static ShaderMaterial Material(Texture2D texture, bool emergency)
    {
        _shader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_prepass_alpha;
uniform sampler2D spirit_texture : source_color, filter_linear_mipmap;
uniform float clock = 0.0;
uniform float reveal = 1.0;
uniform float rebel = 0.0;
uniform float charge = 0.0;
uniform float unstable = 0.0;
float electric_mask(vec3 color) {
    // 稲妻の水色だけを拾う。赤より緑が低い紫白の仮面・断片は含めない。
    return smoothstep(0.015, 0.10, color.g - color.r) * smoothstep(0.05, 0.20, color.b - color.r);
}
void fragment() {
    vec4 c = texture(spirit_texture, UV);
    float light = dot(c.rgb, vec3(0.30, 0.59, 0.11));
    float electric = electric_mask(c.rgb);
    // 水色の縁に挟まれた白い稲妻の芯も拾う。身体の白い面は原色のまま残す。
    vec2 pixel = 1.5 / vec2(textureSize(spirit_texture, 0));
    float nearby = max(max(electric_mask(texture(spirit_texture, UV + vec2(pixel.x, 0.0)).rgb),
        electric_mask(texture(spirit_texture, UV - vec2(pixel.x, 0.0)).rgb)),
        max(electric_mask(texture(spirit_texture, UV + vec2(0.0, pixel.y)).rgb),
        electric_mask(texture(spirit_texture, UV - vec2(0.0, pixel.y)).rgb)));
    electric = max(electric, nearby * smoothstep(0.72, 0.93, light));
    // 寝返りで黄白へ替えるのは電気だけ。仮面と身体の紫白は保持する。
    vec3 warm = light * mix(vec3(1.0, 0.48, 0.20), vec3(1.0, 0.94, 0.73), smoothstep(0.10, 0.85, light));
    ALBEDO = mix(c.rgb, warm, rebel * electric);
    float pulse = 0.5 + 0.5 * sin(clock * 19.0 + UV.y * 31.0);
    EMISSION = ALBEDO * electric * (0.08 + pulse * (0.10 + charge * 0.18));
    float forming = smoothstep(1.0 - reveal - 0.12, 1.0 - reveal, UV.y);
    if (reveal >= 0.999) forming = 1.0;
    ALPHA = c.a * forming * (1.0 - electric * unstable * (1.0 - pulse) * 0.16);
    if (ALPHA < 0.015) discard;
}" };
        var material = new ShaderMaterial { Shader = _shader };
        material.SetShaderParameter("spirit_texture", texture);
        material.SetShaderParameter("unstable", emergency ? 1f : 0f);
        return material;
    }
}
