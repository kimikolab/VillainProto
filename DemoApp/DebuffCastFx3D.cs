using BattleCore;
using Godot;
using System;

// 泥の飛沫とは形を分けた紙の呪符。付与後は胸元より下に小さく残す。
public partial class DebuffTalisman3D : DebuffFx3D
{
    private BattlePawn3D _target = null!;
    private Camera3D _camera = null!;
    private Vector3 _from;
    private Sprite3D _paper = null!, _glint = null!;
    internal void Start(BattlePawn3D source, BattlePawn3D target, Camera3D camera, Func<(bool, double)> clock)
    {
        _target = target; _camera = camera; Clock = clock;
        _from = source.FxPoint + camera.GlobalBasis.X * .22f;
        _paper = Sprite("res://assets/fx/debuff_talisman.svg", .72f, Colors.White);
        _glint = new Sprite3D { Texture = HagiLight3D.Sparkle, PixelSize = .55f / HagiLight3D.Sparkle.GetWidth(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Shaded = false, NoDepthTest = true,
            Modulate = new Color(Tint, 0), RenderPriority = 9 };
        AddChild(_glint);
        Draw();
    }
    public override void _Process(double delta)
    {
        if (!Live(_target) || !_target.NelCurseStyled || !_target.HasStatusIcon(StatusKeys.Curse)) { End(); return; }
        if (!Advance(delta)) return;
        Draw();
    }
    private void Draw()
    {
        float t = Mathf.Clamp((float)(Age / .26), 0, 1);
        Vector3 destination = _target.FxPoint + Vector3.Down * .36f
            + _camera.GlobalBasis.X * (_target.Team == 0 ? .24f : -.24f);
        _paper.GlobalPosition = _from.Lerp(destination, t * t * (3 - 2 * t))
            + _camera.GlobalBasis.Y * (Mathf.Sin(t * Mathf.Pi) * .24f);
        _paper.Scale = new Vector3(.65f + .35f * t, .86f + .14f * t, 1);
        _paper.Modulate = new Color(1, 1, 1, Age < .26 ? .95f : .78f);
        float flash = Mathf.Max(0, 1 - Mathf.Abs((float)Age - .26f) / .13f);
        _glint.GlobalPosition = destination;
        _glint.Modulate = new Color(Tint, flash * .65f);
    }
}

// 2秒・64粒の購入煙は採らず、薄い靄を対象ごとの短い帯として再生時計で動かす。
public partial class DebuffMist3D : DebuffFx3D
{
    private static Shader? _shader;
    private BattlePawn3D _source = null!, _target = null!;
    private Camera3D _camera = null!;
    private MeshInstance3D _mist = null!;
    private ShaderMaterial _material = null!;
    internal void Start(BattlePawn3D source, BattlePawn3D target, Camera3D camera, Func<(bool, double)> clock)
    {
        _source = source; _target = target; _camera = camera; Clock = clock;
        _shader ??= new Shader { Code = @"shader_type spatial;
render_mode unshaded,cull_disabled,blend_mix,depth_draw_never,depth_test_disabled;
uniform float progress=0.0;
void fragment(){
    float u=UV.x;
    float ripple=sin(u*18.0-progress*6.0)*0.055+sin(u*31.0+progress*4.0)*0.035;
    float fog=exp(-pow((UV.y-0.5-ripple)*6.5,2.0));
    float front=smoothstep(progress*1.65-0.3,progress*1.65-0.05,u);
    float spread=1.0-smoothstep(progress*1.65,progress*1.65+0.24,u);
    float grain=0.65+0.22*sin(u*43.0+UV.y*12.0-progress*8.0);
    ALBEDO=vec3(0.60,0.44,0.72);
    ALPHA=fog*front*spread*grain*sin(clamp(progress,0.0,1.0)*3.14159)*0.22;
}" };
        _material = new ShaderMaterial { Shader = _shader, RenderPriority = 6 };
        _mist = new MeshInstance3D { Mesh = new QuadMesh(), MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 10 };
        AddChild(_mist); Draw();
    }
    public override void _Process(double delta)
    {
        if (!Live(_source) || !Live(_target)) { End(); return; }
        if (!Advance(delta)) return;
        if (Age >= .48) { End(); return; }
        Draw();
    }
    private void Draw()
    {
        Vector3 from = _source.FxPoint + Vector3.Down * .36f, to = _target.FxPoint + Vector3.Down * .36f;
        Vector3 x = (to - from).Normalized();
        if (x.LengthSquared() < .01f) x = _camera.GlobalBasis.X;
        Vector3 y = _camera.GlobalBasis.Z.Cross(x).Normalized();
        if (y.LengthSquared() < .01f) y = _camera.GlobalBasis.Y;
        _mist.GlobalBasis = new Basis(x, y, x.Cross(y).Normalized());
        _mist.GlobalPosition = (from + to) * .5f;
        ((QuadMesh)_mist.Mesh).Size = new Vector2(Math.Max(.1f, from.DistanceTo(to)), .95f);
        _material.SetShaderParameter("progress", (float)(Age / .48));
    }
}

public partial class DebuffPressure3D : DebuffFx3D
{
    private BattlePawn3D _target = null!;
    private Sprite3D _pressure = null!;
    private bool _daunt;
    internal void Start(BattlePawn3D target, Func<(bool, double)> clock, bool daunt)
    {
        _target = target; Clock = clock; _daunt = daunt;
        _pressure = Sprite("res://assets/fx/debuff_shrink.svg", daunt ? 1.45f : .85f,
            new Color(daunt ? new Color("c7c9a8") : Tint, 0));
        Draw();
    }
    public override void _Process(double delta)
    {
        if (!Live(_target)) { End(); return; }
        if (!Advance(delta)) return;
        if (Age >= .34) { End(); return; }
        Draw();
    }
    private void Draw()
    {
        float t = (float)(Age / .34);
        _pressure.GlobalPosition = _target.FxPoint + Vector3.Down * (.4f + .12f * t);
        _pressure.Scale = Vector3.One * (1.2f - t * .48f);
        _pressure.Modulate = new Color(_daunt ? new Color("c7c9a8") : Tint, Mathf.Sin(t * Mathf.Pi) * (_daunt ? .8f : .4f));
    }
}

// 刻んだ瞬間は通知が無いため描かない。実際に腕が鈍った通知だけを銅色で見せる。
public partial class DebuffKarzDull3D : DebuffFx3D
{
    private BattlePawn3D _target = null!;
    private Camera3D _camera = null!;
    private Sprite3D _scar = null!, _dull = null!;
    internal static readonly Color ScarTint = new("df9b86");
    internal void Start(BattlePawn3D target, Camera3D camera, Func<(bool, double)> clock)
    {
        _target = target; _camera = camera; Clock = clock;
        _scar = Sprite("res://assets/fx/debuff_karz_scar.svg", .78f, ScarTint);
        _dull = Sprite("res://assets/fx/debuff_dull.svg", .45f, ScarTint);
        Draw();
    }
    public override void _Process(double delta)
    {
        if (!Live(_target)) { End(); return; }
        if (!Advance(delta)) return;
        if (Age >= .38) { End(); return; }
        Draw();
    }
    private void Draw()
    {
        float t = (float)(Age / .38);
        Vector3 weapon = _target.FxPoint + Vector3.Down * .22f + _camera.GlobalBasis.X * (_target.Team == 0 ? .42f : -.42f);
        _scar.GlobalPosition = weapon;
        _dull.GlobalPosition = weapon + Vector3.Down * (t * .16f);
        _scar.Scale = Vector3.One * (.7f + t * .45f);
        _dull.Scale = new Vector3(1, 1 - Mathf.Sin(t * Mathf.Pi) * .28f, 1);
        _scar.Modulate = new Color(ScarTint, (1 - t) * .75f);
        _dull.Modulate = new Color(1, .86f, .72f, 1 - t);
    }
}
