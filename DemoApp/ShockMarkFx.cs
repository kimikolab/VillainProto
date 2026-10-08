using Godot;
using System;

// 感電・標の描画素材。位置は台本を読む側から受け取り、命中や隣接を判断しない。
internal static class ShockMarkFx
{
    internal static readonly Color Feather = new("c2acff");
    internal static readonly Color Gold = new("ffd685");
    private static Texture2D? _halo, _reticle, _cloud, _powder;
    private static Texture2D? _boldMark;
    // 黒い下線＋朱色＋象牙色の芯。鎧・石壁・暗い服のどれにも輪郭を残す。
    internal static Texture2D BoldMark => _boldMark ??= Svg("<defs><g id='m' fill='none'><circle cx='64' cy='64' r='37'/><path d='M64 9v25M64 94v25M9 64h25M94 64h25M20 39V20h19M89 20h19v19M108 89v19H89M39 108H20V89'/><circle cx='64' cy='64' r='5'/></g></defs><use href='#m' stroke='#200917' stroke-width='12'/><use href='#m' stroke='#ff4269' stroke-width='7'/><use href='#m' stroke='#fff0d6' stroke-width='2'/>");
    private static Texture2D Svg(string body)
    {
        using var image = new Image();
        image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='128' height='128'>" + body + "</svg>");
        return ImageTexture.CreateFromImage(image);
    }
    internal static Texture2D Halo => _halo ??= Svg("<defs><radialGradient id='g'><stop stop-color='white'/><stop offset='.2' stop-color='white' stop-opacity='.8'/><stop offset='1' stop-color='white' stop-opacity='0'/></radialGradient></defs><circle cx='64' cy='64' r='63' fill='url(#g)'/>");
    internal static Texture2D Reticle => _reticle ??= Svg("<g fill='none' stroke='white'><circle cx='64' cy='64' r='42' stroke-width='2'/><path d='M64 5v23M64 100v23M5 64h23M100 64h23M22 39V22h17M89 22h17v17M106 89v17H89M39 106H22V89' stroke-width='4'/><circle cx='64' cy='64' r='7' stroke-width='2'/></g>");
    internal static Texture2D Cloud => _cloud ??= UiKit.LoadTexture("res://assets/fx/kata_thundercloud.png", mipmaps: true);
    internal static Texture2D Powder => _powder ??= UiKit.LoadTexture("res://assets/fx/tou_powder.png", mipmaps: true);

    internal static Sprite3D Sprite(Node3D parent, Vector3 world, Texture2D texture, float width, Color tint)
    {
        var sprite = new Sprite3D { Texture = texture, PixelSize = width / texture.GetWidth(),
            Shaded = false, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
            Modulate = tint, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        parent.AddChild(sprite);
        sprite.GlobalPosition = world;
        return sprite;
    }

    internal static void Glow(Node3D parent, Vector3 at, Color tint, float size, double seconds)
    {
        var sprite = Sprite(parent, at, Halo, size, tint);
        var tween = sprite.CreateTween().SetParallel();
        tween.TweenProperty(sprite, "scale", Vector3.One * 1.7f, seconds);
        tween.TweenProperty(sprite, "modulate:a", 0f, seconds);
        tween.Finished += sprite.QueueFree;
    }

    internal static void Ring(Node3D parent, Vector3 at, Color tint, float size, double seconds)
    {
        var sprite = Sprite(parent, at, Reticle, size, tint);
        sprite.Scale = Vector3.One * 0.65f;
        var tween = sprite.CreateTween().SetParallel();
        tween.TweenProperty(sprite, "scale", Vector3.One * 1.3f, seconds);
        tween.TweenProperty(sprite, "modulate:a", 0f, seconds);
        tween.Finished += sprite.QueueFree;
    }

    internal static void Beam(Node3D parent, Vector3 from, Vector3 to, Color tint, float width, double seconds)
    {
        var delta = to - from;
        if (delta.LengthSquared() < 0.00001f) return;
        foreach (var layer in new[] { (width * 3.5f, new Color(tint, tint.A * 0.18f)), (width, tint), (width * 0.28f, new Color(Colors.White, tint.A)) })
        {
            var material = new StandardMaterial3D {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = layer.Item2, EmissionEnabled = true, Emission = tint,
            };
            var beam = new MeshInstance3D {
                Mesh = new CylinderMesh { TopRadius = layer.Item1 * 0.5f, BottomRadius = layer.Item1 * 0.5f,
                    Height = delta.Length(), RadialSegments = 8 }, MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            parent.AddChild(beam);
            beam.GlobalPosition = (from + to) * 0.5f;
            var y = delta.Normalized();
            var x = y.Cross(Math.Abs(y.Dot(Vector3.Up)) > 0.95f ? Vector3.Forward : Vector3.Up).Normalized();
            beam.GlobalBasis = new Basis(x, y, x.Cross(y));
            var tween = beam.CreateTween().SetParallel();
            tween.TweenProperty(beam, "scale", new Vector3(0.05f, 1, 0.05f), seconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            tween.TweenProperty(material, "albedo_color:a", 0f, seconds);
            tween.Finished += beam.QueueFree;
        }
    }

    internal static void Sparks(Node3D parent, Vector3 at, Color tint, int count, float size, double seconds)
    {
        var camera = parent.GetViewport().GetCamera3D();
        var right = camera?.GlobalBasis.X ?? Vector3.Right;
        var up = camera?.GlobalBasis.Y ?? Vector3.Up;
        for (int i = 0; i < count; i++)
        {
            float angle = i * 2.399963f;
            var offset = (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * size * (0.5f + i % 3 * 0.22f);
            var sprite = Sprite(parent, at, Halo, 0.10f + i % 3 * 0.03f, tint);
            var tween = sprite.CreateTween().SetParallel();
            tween.TweenProperty(sprite, "position", parent.ToLocal(at + offset), seconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(sprite, "modulate:a", 0f, seconds);
            tween.Finished += sprite.QueueFree;
        }
    }
}
