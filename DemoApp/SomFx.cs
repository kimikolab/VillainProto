using Godot;
using System;

// 魔法陣・光滴・衣の共通素材。白く塗り潰さず輪郭と薄布だけを重ねる。
internal static class SomFx
{
    internal static readonly Color Gold = new("ffedbd"), Violet = new("bc9de9"), Murky = new("b176c9");
    private static Texture2D? _circle, _light, _veil;
    internal static Texture2D Circle => _circle ??= UiKit.LoadTexture("res://assets/fx/som_circle.svg");
    internal static Texture2D Light => _light ??= UiKit.LoadTexture("res://assets/fx/som_light.svg");
    internal static Texture2D Veil => _veil ??= UiKit.LoadTexture("res://assets/fx/som_veil.svg");

    internal static void Flight(Sprite3D mote, Vector3 to, float arc, double seconds, bool dissolve = false)
    {
        Vector3 from = mote.GlobalPosition;
        var tween = mote.CreateTween();
        tween.TweenMethod(Callable.From<float>(t => {
            mote.GlobalPosition = from.Lerp(to, t) + Vector3.Up * MathF.Sin(t * MathF.PI) * arc;
            mote.Modulate = new Color(mote.Modulate, dissolve ? 1 - t : 1 - Math.Max(0, t - .8f) * 5);
            mote.Scale = Vector3.One * (1 - t * .35f);
        }), 0f, 1f, seconds);
        tween.TweenCallback(Callable.From(mote.QueueFree));
    }
}

public partial class SomVeil3D : Sprite3D
{
    private BattlePawn3D _owner = null!;
    private float _alpha, _pulse, _age;
    internal int Amount { get; private set; }
    internal void Configure(BattlePawn3D owner)
    {
        _owner = owner; Texture = SomFx.Veil;
        PixelSize = owner.SomHeight * .75f / Texture.GetWidth();
        Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        Shaded = false; RenderPriority = 2;
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
    }
    internal void SetAmount(int amount, bool gained)
    {
        Amount = Math.Max(0, amount); _alpha = SomVeilLedger.Brightness(Amount);
        if (gained) _pulse = .22f;
        Visible = Amount > 0;
    }
    internal void Ripple() => _pulse = .38f;
    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_owner) || !_owner.PresentationAlive) { Hide(); return; }
        _age += (float)(delta * _owner.AnimationSpeed);
        _pulse = Math.Max(0, _pulse - (float)(delta * _owner.AnimationSpeed) * 1.6f);
        var camera = GetViewport().GetCamera3D();
        GlobalPosition = _owner.SomCenter + (camera?.GlobalBasis.Z ?? Vector3.Back) * .06f;
        Scale = new Vector3(1 + MathF.Sin(_age * 2) * .018f + _pulse * .12f, 1, 1);
        // 常駐の輝度上限と被弾の短い波紋を別にし、量が積もっても顔を隠さない。
        Modulate = new Color(SomFx.Gold, Math.Min(.64f, _alpha + _pulse));
    }
}
