using Godot;
using System;

// 赤い術式と結晶。生成済み立ち絵には焼き込まず、再生状態に追従させる。
public static class AkaMagic
{
    private static Texture2D? _seal, _spear, _glow;
    private static Texture2D Svg(string body)
    {
        var image = new Image();
        image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='256' height='256' viewBox='0 0 256 256'>" + body + "</svg>");
        return ImageTexture.CreateFromImage(image);
    }
    public static Texture2D Seal => _seal ??= Svg("<g fill='none' stroke='#ee3553'><circle cx='128' cy='128' r='118' stroke-width='3'/><circle cx='128' cy='128' r='105' stroke-width='6'/><circle cx='128' cy='128' r='88' stroke-width='2' stroke-dasharray='8 5'/><path d='M128 23L219 180H37ZM128 233L37 76H219Z' stroke-width='3'/><circle cx='128' cy='128' r='49' stroke-width='4'/><path d='M128 79L177 128 128 177 79 128Z' stroke-width='2'/></g><g fill='#ffd0d4'><path d='M128 9l5 11-5 11-5-11ZM128 225l5 11-5 11-5-11ZM9 128l11-5 11 5-11 5ZM225 128l11-5 11 5-11 5Z'/></g>");
    public static Texture2D Spear => _spear ??= Svg("<path d='M128 3L151 91 143 194 128 253 113 194 105 91Z' fill='#46091f' stroke='#ff6574' stroke-width='2'/><path d='M128 3L128 253 113 194 105 91Z' fill='#c91842'/><path d='M128 3L151 91 128 156Z' fill='#ff8292'/><path d='M128 156L143 194 128 253Z' fill='#ee3458'/><path d='M128 8L122 100 128 156' fill='none' stroke='#ffe9eb' stroke-width='3'/>");
    public static Texture2D Glow => _glow ??= Svg("<defs><radialGradient id='g'><stop stop-color='#ffd6dd' stop-opacity='.85'/><stop offset='.17' stop-color='#f92751' stop-opacity='.6'/><stop offset='1' stop-color='#760b2c' stop-opacity='0'/></radialGradient></defs><circle cx='128' cy='128' r='126' fill='url(#g)'/>");

    public static Sprite3D Sprite(Node3D root, Vector3 world, Texture2D texture, float size)
    {
        var sprite = LiliFx.Sprite(root, world, texture, size);
        sprite.RenderPriority = 3;
        return sprite;
    }
}

public partial class AkaChargeGlyph3D : Node3D
{
    private Sprite3D _outer = null!, _inner = null!, _light = null!;
    private float _age;
    public void Configure(float height)
    {
        Vector3 at = GlobalPosition + Vector3.Up * height * 0.82f;
        _light = AkaMagic.Sprite(this, at, AkaMagic.Glow, 3.2f);
        _light.RenderPriority = 1;
        _outer = AkaMagic.Sprite(this, at, AkaMagic.Seal, 2.25f);
        _inner = AkaMagic.Sprite(this, at, AkaMagic.Seal, 1.35f);
    }
    public override void _Process(double delta)
    {
        if (_outer is null) return;
        _age += (float)delta;
        float open = Mathf.SmoothStep(0, 1, Mathf.Clamp(_age / 0.35f, 0, 1));
        _outer.Rotation = new Vector3(0, 0, _age * 0.24f);
        _inner.Rotation = new Vector3(0, 0, -_age * 0.40f);
        _outer.Scale = Vector3.One * (0.55f + open * 0.45f);
        _inner.Scale = Vector3.One * (0.96f + Mathf.Sin(_age * 3) * 0.04f);
        _outer.Modulate = new Color(1, 1, 1, open * 0.9f);
        _inner.Modulate = new Color(1, 0.65f, 0.72f, open * 0.75f);
        _light.Modulate = new Color(1, 1, 1, open * (0.42f + Mathf.Sin(_age * 3) * 0.10f));
    }
}
