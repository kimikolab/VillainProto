using Godot;
using System;
using System.Collections.Generic;

// 儀式の光だけを所有する。台本のHP・状態・勝敗には触れない。
public partial class LiliRite3D : Node3D
{
    private BattlePawn3D _lili = null!;
    private double _speed;
    private Sprite3D _chalice = null!;
    private Sprite3D _crown = null!;
    private float _lift;
    private bool _released;
    private readonly Dictionary<int, Vector3> _drainPoints = new();
    private readonly Dictionary<int, Vector3> _feet = new();
    private readonly Dictionary<int, float> _widths = new();
    private static Texture2D? _halo, _petal;
    private static Shader? _ribbonShader;
    private static AudioStreamWav? _bell;

    private static Texture2D Art(string body)
    {
        var image = new Image();
        image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='128' height='128'>" + body + "</svg>");
        return ImageTexture.CreateFromImage(image);
    }
    private static Texture2D Halo => _halo ??= Art("<defs><radialGradient id='h'><stop stop-color='#fffaf3'/><stop offset='.2' stop-color='#ffe4ee' stop-opacity='.95'/><stop offset='1' stop-color='#ff9dc7' stop-opacity='0'/></radialGradient></defs><circle cx='64' cy='64' r='63' fill='url(#h)'/>");
    private static Texture2D Petal => _petal ??= Art("<path d='M24 68Q30 20 102 24Q97 97 50 101Q31 94 24 68z' fill='#ffe4ed'/><path d='M35 88Q53 56 89 36' fill='none' stroke='#fffaf4' stroke-width='4'/>");

    public void Configure(BattlePawn3D lili, IReadOnlyList<BattlePawn3D> enemies, double speed,
        IReadOnlyDictionary<int, float>? widths = null)
    {
        _lili = lili;
        _speed = Math.Max(0.1, speed);
        _chalice = LiliFx.Sprite(this, lili.ChalicePoint, Halo, 0.9f);
        _chalice.Modulate = new Color(1, 1, 1, 0.65f);
        _crown = LiliFx.Sprite(this, lili.ChalicePoint, LiliFx.Drop, 0.35f);
        _crown.Modulate = new Color("fff1f5");
        foreach (var enemy in enemies)
        {
            _widths[enemy.InstanceId] = widths is not null && widths.TryGetValue(enemy.InstanceId, out float width)
                ? Math.Clamp(width, 1f, 2.5f) : 1f;
            _drainPoints[enemy.InstanceId] = enemy.FxPoint + Vector3.Up * 0.72f;
            _feet[enemy.InstanceId] = enemy.GlobalPosition + Vector3.Up * 0.08f;
            var point = enemy.FxPoint + Vector3.Up * 0.42f;
            var halo = LiliFx.Sprite(this, point, Halo, 1.2f);
            var glyph = LiliFx.Sprite(this, point, LiliFx.Drop, 0.4f);
            var tween = glyph.CreateTween().SetParallel();
            tween.TweenProperty(glyph, "position:y", glyph.Position.Y + 0.30f, 0.22 / _speed);
            tween.TweenProperty(glyph, "scale", Vector3.One * 1.5f, 0.22 / _speed);
            var fade = halo.CreateTween();
            fade.TweenProperty(halo, "scale", Vector3.One * 1.5f, 0.22 / _speed);
            fade.TweenProperty(halo, "modulate:a", 0f, 0.38 / _speed);
            fade.TweenCallback(Callable.From(halo.QueueFree));
            tween.Chain().TweenProperty(glyph, "modulate:a", 0f, 0.38 / _speed);
            tween.Chain().TweenCallback(Callable.From(glyph.QueueFree));
        }
        var audio = new AudioStreamPlayer { Stream = Bell(), VolumeDb = -17, PitchScale = 1 };
        AddChild(audio);
        audio.Play();
    }

    public void Strike(IReadOnlyList<BattlePawn3D> enemies, bool finish)
    {
        double duration = finish ? 1.45 : 0.78;
        foreach (var enemy in enemies)
        {
            Vector3 foot = _feet[enemy.InstanceId];
            float width = _widths[enemy.InstanceId];
            var sky = foot + new Vector3(-1.3f, 7.5f, -0.6f);
            // 敵を貫く白桃色の柱。死亡後も位置を固定して残照を残す。
            Ribbon(sky, foot, 0.68f * width, 0, duration, new Color(1f, 0.80f, 0.88f, 0.48f), true);
            Ribbon(sky, foot, 0.09f * width, 0, duration, new Color(1f, 0.97f, 0.91f, 0.95f), true);
            var flare = LiliFx.Sprite(this, _drainPoints[enemy.InstanceId] - Vector3.Up * 0.4f, Halo,
                1.4f * MathF.Sqrt(width));
            var fade = flare.CreateTween();
            fade.TweenProperty(flare, "scale", Vector3.One * 1.3f, 0.12 / _speed);
            fade.TweenProperty(flare, "modulate:a", 0f, 0.30 / _speed);
            fade.TweenCallback(Callable.From(flare.QueueFree));
        }
    }

    public void Gather(IReadOnlyList<BattlePawn3D> enemies)
    {
        Vector3 cup = _lili.ChalicePoint;
        foreach (var enemy in enemies)
        {
            Vector3 from = _drainPoints[enemy.InstanceId];
            Ribbon(from, cup, 0.16f, 0.9f, 0.32, new Color("ffc0d7"));
            LiliFx.Travel(this, from, cup, 0.30 / _speed, LiliFx.Drop);
        }
        var swell = _chalice.CreateTween();
        swell.TweenProperty(_chalice, "scale", Vector3.One * 2.0f, 0.32 / _speed);
        var rise = CreateTween();
        rise.TweenMethod(Callable.From<float>(v => _lift = v), 0f, 0.38f, 0.32 / _speed);
    }

    public void Release(IReadOnlyList<BattlePawn3D> allies, bool finish)
    {
        if (_released) return;
        _released = true;
        double duration = finish ? 1.05 : 0.65;
        Vector3 origin = _lili.ChalicePoint + Vector3.Up * _lift;
        var bloom = _chalice.CreateTween().SetParallel();
        bloom.TweenProperty(_chalice, "scale", Vector3.One * 2.2f, 0.24 / _speed);
        bloom.TweenProperty(_chalice, "modulate:a", 0f, duration / _speed);
        var crownFade = _crown.CreateTween().SetParallel();
        crownFade.TweenProperty(_crown, "scale", Vector3.One * 2.5f, 0.24 / _speed);
        crownFade.TweenProperty(_crown, "modulate:a", 0f, 0.30 / _speed);
        foreach (var ally in allies)
        {
            var foot = ally.GlobalPosition + Vector3.Up * 0.08f;
            // 味方には柱を落とさず、杯から柔らかな光と花びらを渡す。
            Ribbon(origin, ally.FxPoint + Vector3.Up * 1.6f, 0.07f, 1.4f, 0.30, new Color("ffe9f0"));
            for (int k = 0; k < 11; k++)
            {
                float angle = k * 2.39996f;
                Vector3 spread = new(Mathf.Cos(angle) * 0.85f, 0, Mathf.Sin(angle) * 0.6f);
                Vector3 from = ally.FxPoint + Vector3.Up * (1.3f + k % 3 * 0.35f) + spread;
                var petal = LiliFx.Sprite(this, from, Petal, 0.10f + k % 3 * 0.035f);
                var tween = petal.CreateTween();
                tween.TweenInterval(k * 0.018 / _speed);
                tween.TweenProperty(petal, "position", ToLocal(foot + spread + Vector3.Right * 0.25f), duration / _speed);
                tween.Parallel().TweenProperty(petal, "rotation:z", angle + 2.0f, duration / _speed);
                tween.Parallel().TweenProperty(petal, "modulate:a", 0f, duration / _speed).SetDelay(duration * 0.25 / _speed);
                tween.TweenCallback(Callable.From(petal.QueueFree));
            }
            var pool = LiliFx.Sprite(this, ally.FxPoint, Halo, 1.3f);
            pool.Modulate = new Color(1, 1, 1, 0.42f);
            var fade = pool.CreateTween();
            fade.TweenProperty(pool, "modulate:a", 0f, duration / _speed);
            fade.TweenCallback(Callable.From(pool.QueueFree));
        }
    }

    private void Ribbon(Vector3 from, Vector3 to, float width, float arc, double duration, Color tint, bool strike = false)
    {
        _ribbonShader ??= new Shader { Code = @"
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never;
uniform vec4 tint : source_color;
uniform float phase = 0.0;
uniform bool strike = false;
void fragment() {
    float edge = exp(-pow((UV.y - 0.5) * 4.5, 2.0));
    float end = smoothstep(0.0, 0.07, UV.x) * (1.0 - smoothstep(0.88, 1.0, UV.x));
    float pulse = 0.82 + 0.18 * sin(UV.x * 22.0 - phase * 12.0);
    float envelope = smoothstep(0.0, strike ? 0.06 : 0.13, phase) * (1.0 - smoothstep(strike ? 0.20 : 0.5, 1.0, phase));
    ALBEDO = tint.rgb;
    ALPHA = edge * end * pulse * envelope * tint.a;
}" };
        var material = new ShaderMaterial { Shader = _ribbonShader };
        material.SetShaderParameter("tint", tint);
        material.SetShaderParameter("strike", strike);
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        var camera = GetViewport().GetCamera3D();
        var facing = camera?.GlobalBasis.Z ?? Vector3.Back;
        for (int k = 0; k <= 24; k++)
        {
            float p = k / 24f;
            Vector3 center = from.Lerp(to, p) + Vector3.Up * Mathf.Sin(p * Mathf.Pi) * arc;
            Vector3 tangent = to - from + Vector3.Up * Mathf.Cos(p * Mathf.Pi) * arc * Mathf.Pi;
            Vector3 side = tangent.Cross(facing).Normalized() * width;
            mesh.SurfaceSetUV(new Vector2(p, 0)); mesh.SurfaceAddVertex(ToLocal(center - side));
            mesh.SurfaceSetUV(new Vector2(p, 1)); mesh.SurfaceAddVertex(ToLocal(center + side));
        }
        mesh.SurfaceEnd();
        var ribbon = new MeshInstance3D { Mesh = mesh, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(ribbon);
        var tween = ribbon.CreateTween();
        tween.TweenMethod(Callable.From<float>(p => material.SetShaderParameter("phase", p)), 0f, 1f, duration / _speed);
        tween.TweenCallback(Callable.From(ribbon.QueueFree));
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_lili) || !IsInstanceValid(_chalice)) return;
        _chalice.GlobalPosition = _lili.ChalicePoint;
        _crown.GlobalPosition = _lili.ChalicePoint + Vector3.Up * _lift;
    }

    // 小さな鐘の余韻。音源・乱数を戦闘側に持ち込まない。
    private static AudioStreamWav Bell()
    {
        if (_bell is not null) return _bell;
        const int rate = 22050;
        var bytes = new byte[(int)(rate * 1.6) * 2];
        for (int i = 0; i < bytes.Length / 2; i++)
        {
            double t = (double)i / rate;
            double sound = Math.Sin(t * Math.Tau * 880) * Math.Exp(-t * 3.2)
                + 0.28 * Math.Sin(t * Math.Tau * 2217) * Math.Exp(-t * 5)
                + 0.14 * Math.Sin(t * Math.Tau * 3511) * Math.Exp(-t * 7);
            short sample = (short)(sound * Math.Min(1, t * 200) * 14000);
            bytes[i * 2] = (byte)(sample & 255); bytes[i * 2 + 1] = (byte)(sample >> 8);
        }
        return _bell = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Data = bytes };
    }
}
