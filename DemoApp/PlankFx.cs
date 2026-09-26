using Godot;
using System;

// 魔法の盾ではなく、輪郭のある鉄・木・骨。乱数は戦闘側にも表示側にも要らない。
public static class PlankFx
{
    public static readonly Color Rust = new("c58a55");
    private static readonly Texture2D?[] Textures = new Texture2D?[3];
    private static Texture2D? _streak;
    private static Texture2D? _mirror;
    private static Texture2D Mirror
    {
        get
        {
            if (_mirror is not null) return _mirror;
            var image = new Image();
            image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='256' height='256'><defs><radialGradient id='g'><stop stop-color='#fffbe5' stop-opacity='.85'/><stop offset='.35' stop-color='#ffe0a0' stop-opacity='.12'/><stop offset='.82' stop-color='#9de8ff' stop-opacity='.2'/><stop offset='1' stop-color='#aeeaff' stop-opacity='0'/></radialGradient></defs><circle cx='128' cy='128' r='125' fill='url(#g)'/><circle cx='128' cy='128' r='102' fill='none' stroke='#d3f5ff' stroke-width='5'/><circle cx='128' cy='128' r='112' fill='none' stroke='#efc88a' stroke-width='2' stroke-dasharray='24 10'/><path d='M128 10L137 113 246 128 139 138 128 246 118 140 10 128 116 117Z' fill='#fff9de'/></svg>");
            return _mirror = ImageTexture.CreateFromImage(image);
        }
    }

    // 受け止める円盤 → 破片の収束 → 敵へ放出。全保持者を同じ時計で再生する。
    public static void Reflect(Node3D root, Vector3 from, Vector3 to, int amount, double speed)
    {
        speed = Math.Max(0.1, speed);
        var camera = root.GetViewport().GetCamera3D();
        var right = camera?.GlobalBasis.X ?? Vector3.Right;
        var up = camera?.GlobalBasis.Y ?? Vector3.Up;
        var direction = (to - from).Normalized();
        var focus = from + direction * 0.45f;
        var shield = LiliFx.Sprite(root, focus, Mirror, 2.35f);
        shield.RenderPriority = 7;
        shield.Scale = Vector3.One * 0.3f;
        var ring = shield.CreateTween();
        ring.TweenProperty(shield, "scale", Vector3.One, 0.10 / speed).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        ring.TweenInterval(0.08 / speed);
        ring.TweenProperty(shield, "scale", Vector3.One * 0.16f, 0.14 / speed);
        ring.TweenCallback(Callable.From(shield.QueueFree));
        int count = Math.Clamp(10 + amount / 8, 10, 20);
        float angle = Mathf.Atan2(direction.Dot(up), direction.Dot(right));
        for (int k = 0; k < count; k++)
        {
            int n = k;
            float a = k * Mathf.Tau / count;
            var radial = right * Mathf.Cos(a) + up * Mathf.Sin(a);
            var edge = focus + radial * (0.85f + k % 3 * 0.10f);
            var piece = LiliFx.Sprite(root, from, Piece(k), 0.30f + k % 3 * 0.05f);
            piece.RenderPriority = 9;
            var trail = LiliFx.Sprite(root, edge, Streak, 1.25f);
            trail.RenderPriority = 8;
            trail.Visible = false;
            trail.Rotation = new Vector3(0, 0, angle);
            var tween = piece.CreateTween();
            tween.TweenProperty(piece, "global_position", edge, 0.10 / speed).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenInterval(0.08 / speed);
            tween.TweenProperty(piece, "global_position", focus + radial * 0.07f, 0.14 / speed).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            tween.TweenCallback(Callable.From(() => trail.Visible = true));
            tween.TweenMethod(Callable.From<float>(p => {
                piece.GlobalPosition = focus.Lerp(to, p) + radial * (0.07f + Mathf.Sin(p * Mathf.Pi) * 0.32f);
                piece.Rotation = new Vector3(0, 0, n + p * 8);
                trail.GlobalPosition = piece.GlobalPosition - direction * 0.42f;
            }), 0f, 1f, 0.18 / speed);
            tween.TweenCallback(Callable.From(piece.QueueFree));
            tween.TweenCallback(Callable.From(trail.QueueFree));
        }
    }

    public static void ReflectImpact(Node3D root, Vector3 point, int amount, double speed)
    {
        Burst(root, point, Math.Max(60, amount), 0.32 / speed);
        var flash = LiliFx.Sprite(root, point, Mirror, 1.0f);
        flash.RenderPriority = 8;
        var tween = flash.CreateTween().SetParallel();
        tween.TweenProperty(flash, "scale", Vector3.One * 2.8f, 0.24 / speed);
        tween.TweenProperty(flash, "modulate:a", 0f, 0.24 / speed);
        tween.Chain().TweenCallback(Callable.From(flash.QueueFree));
    }
    private static Texture2D Streak
    {
        get
        {
            if (_streak is not null) return _streak;
            var image = new Image();
            image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='96' height='16'><path d='M0 8L88 4 96 8 88 12Z' fill='#e7ba72'/><path d='M35 8L92 6 96 8 92 10Z' fill='#fff0c5'/></svg>");
            return _streak = ImageTexture.CreateFromImage(image);
        }
    }
    public static Texture2D Piece(int kind)
    {
        kind %= 3;
        if (Textures[kind] is { } texture) return texture;
        string body = kind switch
        {
            0 => "<path d='M13 18L46 8 57 23 49 54 15 57 7 38Z' fill='#777c79' stroke='#503e32' stroke-width='4'/><path d='M18 20l22-7M12 37l8 12' stroke='#c3b99f' stroke-width='3'/><path d='M39 32l10-4-3 13-8 5Z' fill='#9a5936'/><circle cx='21' cy='27' r='3' fill='#d7c6a4'/><circle cx='40' cy='46' r='3' fill='#302c27'/>",
            1 => "<path d='M7 17L53 8 50 18 59 45 46 49 41 57 12 54Z' fill='#976b42' stroke='#493828' stroke-width='4'/><path d='M16 24l31-7M18 34l32-9M22 45l22-7' stroke='#c39a62' stroke-width='3'/><circle cx='19' cy='22' r='3' fill='#45413a'/>",
            _ => "<path d='M15 15l12-6 27 29-6 17-12-2-6-14Z' fill='#d4c4a0' stroke='#786649' stroke-width='4'/><path d='M24 19l18 26' stroke='#f0e5c6' stroke-width='4'/>"
        };
        var image = new Image();
        image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64'>" + body + "</svg>");
        return Textures[kind] = ImageTexture.CreateFromImage(image);
    }
    public static void Fly(Node3D root, Vector3 from, Vector3 to, int amount, double seconds, bool arc = false)
    {
        int count = Math.Clamp(2 + amount / 10, 2, 10);
        for (int k = 0; k < count; k++)
        {
            int n = k;
            var offset = new Vector3((k % 3 - 1) * 0.18f, (k / 3 - 1) * 0.15f, 0);
            float size = Math.Clamp(0.16f + amount * 0.003f, 0.16f, 0.38f);
            var piece = LiliFx.Sprite(root, from + offset, Piece(k), size);
            piece.RenderPriority = 5;
            Sprite3D? trail = null;
            if (!arc)
            {
                trail = LiliFx.Sprite(root, from + offset, Streak, size * 3.2f);
                trail.RenderPriority = 4;
                var camera = root.GetViewport().GetCamera3D();
                var direction = to - from;
                trail.Rotation = new Vector3(0, 0, Mathf.Atan2(direction.Dot(camera?.GlobalBasis.Y ?? Vector3.Up),
                    direction.Dot(camera?.GlobalBasis.X ?? Vector3.Right)));
                trail.Modulate = new Color(1, 1, 1, 0.65f);
            }
            var tween = piece.CreateTween();
            tween.TweenMethod(Callable.From<float>(p => {
                piece.GlobalPosition = (from + offset).Lerp(to + offset * 0.18f, p)
                    + Vector3.Up * (arc ? Mathf.Sin(p * Mathf.Pi) * 0.45f : 0);
                piece.Rotation = new Vector3(0, 0, p * (n % 2 == 0 ? 5 : -5));
                if (trail is not null)
                    trail.GlobalPosition = piece.GlobalPosition - (to - from).Normalized() * size * 0.9f;
            }), 0f, 1f, Math.Max(0.001, seconds));
            tween.TweenCallback(Callable.From(piece.QueueFree));
            if (trail is not null) tween.TweenCallback(Callable.From(trail.QueueFree));
        }
    }
    public static void Burst(Node3D root, Vector3 point, int amount, double seconds, bool fire = false)
    {
        int count = Math.Clamp(5 + amount / 12, 5, 18);
        for (int k = 0; k < count; k++)
        {
            float a = k * Mathf.Tau / count;
            bool spark = k % 2 == 0;
            var piece = LiliFx.Sprite(root, point, spark ? Streak : Piece(k), spark ? 0.3f : 0.13f);
            piece.RenderPriority = 6;
            piece.Modulate = fire ? new Color("ff9a39") : Colors.White;
            piece.Rotation = new Vector3(0, 0, a);
            var end = point + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0.1f) * (0.35f + Math.Min(amount, 100) * 0.005f);
            var tween = piece.CreateTween().SetParallel();
            tween.TweenProperty(piece, "global_position", end, seconds);
            tween.TweenProperty(piece, "modulate:a", 0f, seconds);
            tween.Chain().TweenCallback(Callable.From(piece.QueueFree));
        }
    }
}
