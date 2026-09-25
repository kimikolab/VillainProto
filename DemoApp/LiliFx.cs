using Godot;
using System;

// リリだけの桃色の精気。火・泥・毒の粒子は借りない。
public static class LiliFx
{
    public static readonly Color Rose = new("ffaccb");
    private static Texture2D? _light, _drop, _shell;
    private static Texture2D Svg(string body)
    {
        var image = new Image();
        image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='96' height='96'>" + body + "</svg>");
        return ImageTexture.CreateFromImage(image);
    }
    public static Texture2D Drop => _drop ??= Svg("<path d='M48 8C42 27 24 42 24 59a24 24 0 0 0 48 0C72 42 54 27 48 8z' fill='#e85c89' stroke='#ffd1df' stroke-width='4'/><path d='M44 29Q29 54 36 62' fill='none' stroke='#fff0f5' stroke-width='5'/>");
    private static Texture2D Light => _light ??= Svg("<defs><radialGradient id='g'><stop stop-color='#fff4fa'/><stop offset='.25' stop-color='#ffc1d9' stop-opacity='.95'/><stop offset='1' stop-color='#ff8bbb' stop-opacity='0'/></radialGradient></defs><circle cx='48' cy='48' r='47' fill='url(#g)'/>");
    public static Sprite3D Sprite(Node3D parent, Vector3 world, Texture2D texture, float size)
    {
        var sprite = new Sprite3D { Texture = texture, PixelSize = size / texture.GetWidth(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Shaded = false,
            NoDepthTest = true, Position = parent.ToLocal(world) };
        parent.AddChild(sprite);
        return sprite;
    }
    public static void Travel(Node3D parent, Vector3 from, Vector3 to, double seconds,
        Texture2D? icon = null, float offset = 0, double delay = 0)
    {
        var sprite = Sprite(parent, from, icon ?? Light, icon is null ? 0.45f : 0.33f);
        sprite.RenderPriority = icon is null ? 1 : 2;
        var a = parent.ToLocal(from); var b = parent.ToLocal(to);
        sprite.Visible = delay <= 0;
        var tween = sprite.CreateTween();
        if (delay > 0) tween.TweenInterval(delay);
        tween.TweenCallback(Callable.From(() => sprite.Visible = true));
        tween.TweenMethod(Callable.From<float>(p => {
            sprite.Position = a.Lerp(b, p) + Vector3.Up * (Mathf.Sin(p * Mathf.Pi) * (0.45f + offset));
            sprite.Scale = Vector3.One * (icon is null ? 0.7f + Mathf.Sin(p * Mathf.Pi) * 0.5f : 1f);
        }), 0f, 1f, Math.Max(0.001, seconds));
        tween.TweenCallback(Callable.From(sprite.QueueFree));
    }
    public static void Flow(Node3D parent, Vector3 from, Vector3 to, double seconds)
    {
        for (int k = 0; k < 9; k++)
            Travel(parent, from, to, seconds * 0.70, offset: k * 0.025f, delay: seconds * k * 0.03);
    }
    public static void Shell(Node3D parent, Vector3 point, double speed)
    {
        _shell ??= Svg("<ellipse cx='48' cy='48' rx='32' ry='44' fill='#ffb4d0' fill-opacity='.08' stroke='#ffd1e2' stroke-opacity='.65' stroke-width='2'/><path d='M22 25l9 6-5 13-8-7zM68 57l9 8-6 14-9-10zM37 9l8 6-5 8-7-8z' fill='#ffd1e2' fill-opacity='.7'/>");
        var sprite = Sprite(parent, point, _shell, 1.5f);
        var tween = sprite.CreateTween();
        tween.TweenProperty(sprite, "scale", Vector3.One * 1.12f, 0.18 / speed);
        tween.TweenProperty(sprite, "modulate:a", 0f, 0.35 / speed);
        tween.TweenCallback(Callable.From(sprite.QueueFree));
    }
}
