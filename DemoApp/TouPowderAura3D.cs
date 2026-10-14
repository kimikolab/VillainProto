using Godot;
using System;

// 濃さは帯電が弾けても残る。常駐粒は最大12粒、数字は実数のまま。
public partial class TouPowderAura3D : Node3D
{
    private Func<Vector3?> _point = null!;
    private Func<int> _density = null!;
    private Func<bool> _marked = null!;
    private bool _ball;
    private Camera3D _camera = null!;
    private readonly Sprite3D[] _motes = new Sprite3D[12];
    private Label3D _badge = null!;
    private Sprite3D _stamp = null!;
    private static Texture2D? _stampTexture;
    private float _age;
    private bool _paused;
    private double _speed = 1;
    internal float Age => _age;
    internal string BadgeText => _badge.Text;
    internal void Configure(BattlePawn3D pawn, Camera3D camera)
        => Configure(() => IsInstanceValid(pawn) && pawn.PresentationAlive ? pawn.FxPoint : null,
            () => pawn.PowderDensity, () => pawn.PowderMarked, camera);
    internal void Configure(Func<Vector3?> point, Func<int> density, Func<bool> marked, Camera3D camera, bool ball = false)
    {
        _point = point; _density = density; _marked = marked; _camera = camera; _ball = ball;
        for (int i = 0; i < _motes.Length; i++)
            _motes[i] = ShockMarkFx.Sprite(this, point() ?? Vector3.Zero, TouPowderFx3D.Grain, .085f, ShockMarkFx.Gold);
        _badge = new Label3D {
            FontSize = 30, OutlineSize = 9, PixelSize = .006f,
            Modulate = ShockMarkFx.Gold, OutlineModulate = new Color("382733"),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_badge);
        _stamp = ShockMarkFx.Sprite(this, point() ?? Vector3.Zero,
            _stampTexture ??= UiKit.LoadTexture("res://assets/fx/tou_powder_mark.svg", mipmaps: true), .30f, Colors.White);
        _stamp.NoDepthTest = true; _stamp.RenderPriority = 2;
        Draw();
    }
    internal void SetClock(bool paused, double speed) { _paused = paused; _speed = speed; }
    public override void _Process(double delta)
    {
        if (!_paused) _age += (float)(delta * _speed);
        Draw();
    }
    private void Draw()
    {
        Vector3? at = _point();
        Visible = at is not null && (_density() > 0 || _marked());
        if (!Visible) return;
        int n = _density();
        int count = n > 0 ? Math.Min(12, 4 + n * 2) : 3;
        Color tint = ShockMarkFx.Gold.Lerp(new Color("e9a340"), Math.Min(n, 8) / 10f);
        Vector3 center = at!.Value;
        for (int i = 0; i < _motes.Length; i++)
        {
            var mote = _motes[i]; mote.Visible = i < count;
            float phase = i * 2.39996f + _age * .42f;
            // 胴の外縁だけに漂わせ、顔と頭上のHP表示を空ける。
            mote.GlobalPosition = center + _camera.GlobalBasis.X * Mathf.Cos(phase) * (_ball ? .32f : .43f + i % 3 * .07f)
                + Vector3.Up * (-.48f + i % 4 * .24f + Mathf.Sin(_age * 1.5f + i) * .06f)
                + _camera.GlobalBasis.Z * .28f;
            mote.Modulate = new Color(tint, .5f + .13f * Mathf.Sin(_age * 2 + i));
        }
        bool marked = _marked();
        _stamp.Visible = marked;
        _stamp.GlobalPosition = center + _camera.GlobalBasis.X * .64f - Vector3.Up * .23f + _camera.GlobalBasis.Z * .40f;
        _badge.Visible = n > 0 || marked;
        _badge.Text = n > 0 ? $"粉 {n}{(marked ? " ◇" : "")}" : "粉印";
        _badge.GlobalPosition = center + _camera.GlobalBasis.X * .60f - Vector3.Up * .5f + _camera.GlobalBasis.Z * .35f;
    }
}
