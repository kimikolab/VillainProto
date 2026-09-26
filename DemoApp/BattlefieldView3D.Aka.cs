using Godot;
using System;
using System.Collections.Generic;

public partial class BattlefieldView3D
{
    internal int BloodGatherSources { get; private set; }
    internal int BloodReleaseTargets { get; private set; }
    private static Texture2D? _bloodDrop, _bloodSeal;
    private static Texture2D BloodTexture(bool seal)
    {
        if ((seal ? _bloodSeal : _bloodDrop) is { } cached) return cached;
        var image = new Image();
        string shape = seal
            ? "<g fill='none' stroke='#713e42' stroke-width='4'><circle cx='64' cy='64' r='56'/><circle cx='64' cy='64' r='44'/><path d='M64 8L112 92H16ZM64 120L16 36H112Z'/><circle cx='64' cy='64' r='19'/></g><g stroke='#342025' stroke-width='6'><path d='M64 15v17M64 96v17M15 64h17M96 64h17'/></g>"
            : "<path d='M64 9C54 31 34 54 34 78a30 30 0 0 0 60 0C94 54 74 31 64 9Z' fill='#713e42' stroke='#24161b' stroke-width='6'/><path d='M53 56q-12 20-5 31' fill='none' stroke='#a17475' stroke-width='4'/>";
        image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='128' height='128'>" + shape + "</svg>");
        var texture = ImageTexture.CreateFromImage(image);
        if (seal) _bloodSeal = texture; else _bloodDrop = texture;
        return texture;
    }

    // 出どころは再生済みの被害イベント。量や戦闘の収支はここで計算しない。
    public void ShowBloodGather(IReadOnlyList<BattlePawn3D> sources, BattlePawn3D aka, double speed)
    {
        BloodGatherSources += sources.Count;
        Vector3 hand = aka.FxPoint + new Vector3(aka.Team == BattleCore.BattleContext.PlayerTeam ? 0.35f : -0.35f, 0, 0);
        foreach (var source in sources)
            for (int i = 0; i < 5; i++)
                LiliFx.Travel(_fxRoot, source.FxPoint, hand, 0.42 / Math.Max(0.1, speed),
                    BloodTexture(false), i * 0.03f, i * 0.045 / Math.Max(0.1, speed));
    }

    public void ShowBloodRelease(BattlePawn3D aka, IReadOnlyList<BattlePawn3D> targets, double speed)
    {
        BloodReleaseTargets += targets.Count;
        double duration = 0.90 / Math.Sqrt(Math.Max(0.1, speed));
        var seal = AkaMagic.Sprite(_fxRoot, aka.FxPoint, AkaMagic.Seal, 2.6f);
        var tween = seal.CreateTween().SetParallel();
        tween.TweenProperty(seal, "scale", Vector3.One * 1.25f, duration);
        tween.TweenProperty(seal, "modulate:a", 0f, duration);
        tween.Chain().TweenCallback(Callable.From(seal.QueueFree));
        foreach (var target in targets)
        {
            var rainSeal = AkaMagic.Sprite(_fxRoot, target.FxPoint + Vector3.Up * 1.35f, AkaMagic.Seal, 1.35f);
            var fade = rainSeal.CreateTween();
            fade.TweenProperty(rainSeal, "modulate:a", 0f, duration);
            fade.TweenCallback(Callable.From(rainSeal.QueueFree));
            // 紅い結晶槍が扇状に収束し、着弾点から破片が外へ炸裂する。
            Vector3 center = target.FxPoint;
            for (int i = 0; i < 7; i++)
            {
                Vector3 from = center + new Vector3((i - 3) * 0.34f, 1.5f + (i % 2) * 0.35f, 0);
                var spear = AkaMagic.Sprite(_fxRoot, from, AkaMagic.Spear, 1.7f);
                spear.Rotation = new Vector3(0, 0, (i - 3) * -0.12f);
                var shot = spear.CreateTween();
                shot.TweenInterval(i * duration * 0.025);
                shot.TweenProperty(spear, "position", _fxRoot.ToLocal(center), duration * 0.32)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                shot.TweenProperty(spear, "modulate:a", 0f, duration * 0.12);
                shot.TweenCallback(Callable.From(spear.QueueFree));
            }
            var flash = AkaMagic.Sprite(_fxRoot, center, AkaMagic.Glow, 0.4f);
            var flare = flash.CreateTween();
            flare.TweenInterval(duration * 0.28);
            flare.TweenProperty(flash, "scale", Vector3.One * 6f, duration * 0.12);
            flare.TweenProperty(flash, "modulate:a", 0f, duration * 0.5);
            flare.TweenCallback(Callable.From(flash.QueueFree));
            for (int i = 0; i < 12; i++)
            {
                float angle = i * Mathf.Tau / 12;
                var shard = AkaMagic.Sprite(_fxRoot, center, AkaMagic.Spear, 0.38f + (i % 3) * 0.09f);
                shard.Visible = false;
                shard.Rotation = new Vector3(0, 0, angle);
                var burst = shard.CreateTween();
                burst.TweenInterval(duration * 0.32);
                burst.TweenCallback(Callable.From(() => shard.Visible = true));
                burst.SetParallel();
                burst.TweenProperty(shard, "position", _fxRoot.ToLocal(center) + new Vector3(Mathf.Cos(angle) * 1.15f, Mathf.Sin(angle) * 0.85f, 0), duration * 0.65)
                    .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
                burst.TweenProperty(shard, "modulate:a", 0f, duration * 0.65);
                burst.Chain().TweenCallback(Callable.From(shard.QueueFree));
            }
        }
    }
}
