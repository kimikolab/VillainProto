using Godot;
using System;

// 濃さは帯電が弾けても残る。常駐粒は最大12粒、数字は実数のまま。
public partial class TouPowderAura3D : Node3D
{
    private BattlePawn3D _pawn = null!;
    private Camera3D _camera = null!;
    private readonly Sprite3D[] _motes = new Sprite3D[12];
    private Label3D _badge = null!;
    private float _age;
    private bool _paused;
    private double _speed = 1;
    internal float Age => _age;
    internal void Configure(BattlePawn3D pawn, Camera3D camera)
    {
        _pawn = pawn; _camera = camera;
        for (int i = 0; i < _motes.Length; i++)
            _motes[i] = ShockMarkFx.Sprite(this, pawn.FxPoint, TouPowderFx3D.Grain, .085f, ShockMarkFx.Gold);
        _badge = new Label3D {
            FontSize = 30, OutlineSize = 9, PixelSize = .006f,
            Modulate = ShockMarkFx.Gold, OutlineModulate = new Color("382733"),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_badge); Draw();
    }
    internal void SetClock(bool paused, double speed) { _paused = paused; _speed = speed; }
    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_pawn)) { Hide(); QueueFree(); return; }
        if (!_paused) _age += (float)(delta * _speed);
        Draw();
    }
    private void Draw()
    {
        Visible = _pawn.PresentationAlive && (_pawn.PowderDensity > 0 || _pawn.PowderMarked);
        if (!Visible) return;
        int n = _pawn.PowderDensity;
        int count = n > 0 ? Math.Min(12, 4 + n * 2) : 3;
        Color tint = ShockMarkFx.Gold.Lerp(new Color("e9a340"), Math.Min(n, 8) / 10f);
        Vector3 center = _pawn.FxPoint;
        for (int i = 0; i < _motes.Length; i++)
        {
            var mote = _motes[i]; mote.Visible = i < count;
            float phase = i * 2.39996f + _age * .42f;
            // 胴の外縁だけに漂わせ、顔と頭上のHP表示を空ける。
            mote.GlobalPosition = center + _camera.GlobalBasis.X * Mathf.Cos(phase) * (.43f + i % 3 * .07f)
                + Vector3.Up * (-.48f + i % 4 * .24f + Mathf.Sin(_age * 1.5f + i) * .06f)
                + _camera.GlobalBasis.Z * .28f;
            mote.Modulate = new Color(tint, .5f + .13f * Mathf.Sin(_age * 2 + i));
        }
        _badge.Visible = n > 0;
        _badge.Text = $"粉 {n}";
        _badge.GlobalPosition = center + _camera.GlobalBasis.X * .60f - Vector3.Up * .5f + _camera.GlobalBasis.Z * .35f;
    }
}
