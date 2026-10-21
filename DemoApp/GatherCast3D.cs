using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// 一回の演出を一つの時計で動かす。戦闘の量・対象・判定には触れない。
public partial class GatherCast3D : Node3D
{
    private readonly List<Action<float>> _draw = new();
    private readonly List<(float At, Action Play)> _cues = new();
    private readonly List<GpuParticles3D> _particles = new();
    private readonly List<AudioStreamPlayer> _sounds = new();
    private readonly TaskCompletionSource<bool> _impact = new(), _finished = new();
    private float _age, _duration, _contact;
    private bool _paused, _ended;
    private double _speed = 1;
    private Action? _cleanup;
    internal Task<bool> Impact => _impact.Task;
    internal Task<bool> Finished => _finished.Task;
    internal float Age => _age;
    internal int PurchasedCount => _particles.Count;

    internal void Configure(float duration, float contact, Action? cleanup = null)
    { _duration = duration; _contact = contact; _cleanup = cleanup; }
    internal void Draw(Action<float> draw) { _draw.Add(draw); draw(0); }
    internal void At(float time, Action action) => _cues.Add((time, action));
    internal void SetClock(bool paused, double speed)
    {
        _paused = paused; _speed = Math.Max(.1, speed);
        foreach (var p in _particles) if (IsInstanceValid(p)) p.SpeedScale = paused ? 0 : _speed;
        foreach (var sound in _sounds) if (IsInstanceValid(sound)) sound.StreamPaused = paused;
    }
    public override void _Process(double delta)
    {
        if (_paused || _ended) return;
        _age += (float)(delta * _speed);
        for (int i = _cues.Count - 1; i >= 0; i--)
            if (_age >= _cues[i].At) { var action = _cues[i].Play; _cues.RemoveAt(i); action(); }
        foreach (var draw in _draw) draw(_age);
        if (_age >= _contact) _impact.TrySetResult(true);
        if (_age >= _duration) Finish(true);
    }
    internal void Cancel() => Finish(false);
    private void Finish(bool success)
    {
        if (_ended) return;
        _ended = true; _cleanup?.Invoke(); _cleanup = null;
        _impact.TrySetResult(success); _finished.TrySetResult(success);
        Hide(); QueueFree();
    }
    public override void _ExitTree()
    { _cleanup?.Invoke(); _cleanup = null; _impact.TrySetResult(false); _finished.TrySetResult(false); }

    internal Sprite3D Sprite(Vector3 at, Texture2D texture, float size, Color? tint = null)
    {
        var sprite = AkaMagic.Sprite(this, at, texture, size);
        sprite.Modulate = tint ?? Colors.White;
        return sprite;
    }
    internal void Sound(string name, float volume, float start = 0, float offset = 0, float fadeOut = 0,
        float? end = null, float fadeIn = 0)
    {
        string path = $"res://assets/audio/se/{name}";
        if (!System.IO.Path.HasExtension(name)) path += ".mp3";
        if (!ResourceLoader.Exists(path)) return;
        BattleAudioRouting.EnsureFieldBus();
        var sound = new AudioStreamPlayer { Stream = GD.Load<AudioStream>(path), VolumeDb = volume,
            Bus = BattleAudioRouting.FieldBus };
        AddChild(sound); _sounds.Add(sound);
        float stop = end ?? _duration;
        At(start, () => {
            // 高速再生で開始と終了を一度に跨いでも、終了済みの溜め音を鳴らさない。
            if (_age >= stop) return;
            sound.Play(offset); sound.StreamPaused = _paused;
        });
        if (end.HasValue) At(stop, () => sound.Stop());
        // 溜めの立ち上がりと余韻を共通時計で制御。停止・倍速も追従する。
        if (fadeOut > 0 || fadeIn > 0)
            Draw(age => {
                float gain = fadeIn > 0 ? Mathf.SmoothStep(0, 1, Mathf.Clamp((age - start) / fadeIn, 0, 1)) : 1;
                if (fadeOut > 0)
                    gain *= 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp((age - (stop - fadeOut)) / fadeOut, 0, 1));
                sound.VolumeDb = volume + Mathf.LinearToDb(Math.Max(.0001f, gain));
            });
    }
    internal void ImpactShake(Camera3D camera, float start, float amplitude)
    {
        float baseH = camera.HOffset, baseV = camera.VOffset;
        bool restored = false;
        void Restore()
        {
            if (!IsInstanceValid(camera)) return;
            camera.HOffset = baseH; camera.VOffset = baseV;
        }
        // 視点操作や既存の寄り演出と独立した小さな揺れ。中断・離脱でも元の値へ戻す。
        _cleanup += Restore;
        Draw(age => {
            float time = age - start;
            if (time < 0 || restored || !IsInstanceValid(camera)) return;
            const float duration = .26f;
            if (time >= duration) { Restore(); restored = true; return; }
            float fade = MathF.Pow(1 - time / duration, 2);
            camera.HOffset = baseH + amplitude * fade * MathF.Cos(time * 65);
            camera.VOffset = baseV + amplitude * .55f * fade * MathF.Sin(time * 55 + .6f);
        });
    }
    internal void Flow(Vector3 from, Vector3 to, Texture2D texture, int count, float start, float travel, float size)
    {
        for (int i = 0; i < count; i++)
        {
            int k = i;
            var mote = Sprite(from, texture, size * (1 + (k % 3) * .12f));
            Draw(age => {
                float p = (age - start - k * .018f) / travel;
                mote.Visible = p >= 0 && p < 1;
                p = Mathf.Clamp(p, 0, 1);
                mote.GlobalPosition = from.Lerp(to, p) + Vector3.Up * (Mathf.Sin(p * Mathf.Pi) * (.22f + (k % 4) * .09f));
                mote.Scale = Vector3.One * (1 - .55f * p);
                mote.Rotation = new Vector3(0, 0, k + p * 3);
            });
        }
    }
    internal void Flash(Vector3 at, Texture2D texture, float size, float start, float duration)
    {
        var flash = Sprite(at, texture, size);
        Draw(age => {
            float p = (age - start) / duration;
            flash.Visible = p >= 0 && p <= 1;
            flash.Scale = Vector3.One * (.4f + Math.Max(0, p) * 1.2f);
            flash.Modulate = new Color(1, 1, 1, Mathf.Clamp(1 - p, 0, 1));
        });
    }

    // EffectBlocks v4 の尖った光片と薄い煙を部分利用。共有リソースは書き換えない。
    internal void Purchased(Vector3 at, bool mist, Color tint, float scale, float start)
    {
        string path = mist ? "res://PolyBlocks/EffectBlocks/assets/smoke/smoke_light.tscn"
            : "res://PolyBlocks/EffectBlocks/assets/impacts/impact_3.tscn";
        if (!ResourceLoader.Exists(path)) return;
        var p = GD.Load<PackedScene>(path).Instantiate<GpuParticles3D>();
        p.SetScript(default); p.Emitting = false; p.OneShot = true;
        p.Amount = mist ? 12 : 16; p.Lifetime = mist ? .48 : .30;
        p.Explosiveness = 1; p.Preprocess = 0; p.UseFixedSeed = true; p.Seed = 417;
        var material = (ParticleProcessMaterial)p.ProcessMaterial.Duplicate(true);
        p.ProcessMaterial = material;
        material.Gravity = mist ? Vector3.Up * .1f : Vector3.Zero;
        material.ScaleMin = scale * .045f; material.ScaleMax = scale * (mist ? .13f : .09f);
        material.InitialVelocityMin = .15f; material.InitialVelocityMax = mist ? .4f : 1.1f;
        material.Color = Colors.White;
        material.ColorRamp = new GradientTexture1D { Gradient = new Gradient {
            Offsets = new[] { 0f, .35f, 1f },
            Colors = new[] { new Color(tint, .55f), new Color(tint, .35f), new Color(tint, 0) },
        } };
        var draw = (StandardMaterial3D)p.DrawPass1.SurfaceGetMaterial(0).Duplicate();
        draw.BlendMode = BaseMaterial3D.BlendModeEnum.Mix;
        p.MaterialOverride = draw;
        AddChild(p); p.GlobalPosition = at; _particles.Add(p);
        p.SpeedScale = _paused ? 0 : _speed;
        At(start, () => { p.Emitting = true; p.Restart(); });
    }
}

internal static class GatherArt
{
    private static Texture2D? _crystal, _light, _pouch;
    private static Texture2D Svg(string body)
    {
        var image = new Image();
        image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='128' height='128'>" + body + "</svg>");
        return ImageTexture.CreateFromImage(image);
    }
    internal static Texture2D Crystal => _crystal ??= Svg("<path d='M64 3L92 43 80 97 53 124 36 72 43 28Z' fill='#83c9dc' stroke='#ecffff' stroke-width='3'/><path d='M64 3L59 65 53 124 80 97 70 59Z' fill='#e6fcff'/><path d='M43 28L59 65 36 72' fill='#437594'/>");
    internal static Texture2D Light => _light ??= Svg("<defs><radialGradient id='g'><stop stop-color='#ffffff'/><stop offset='.18' stop-color='#c6efff' stop-opacity='.9'/><stop offset='1' stop-color='#61bddf' stop-opacity='0'/></radialGradient></defs><circle cx='64' cy='64' r='63' fill='url(#g)'/>");
    internal static Texture2D Pouch => _pouch ??= Svg("<path d='M32 30Q64 41 96 30L109 96Q103 119 64 120Q25 119 19 96Z' fill='#523b2e' stroke='#b18b54' stroke-width='5'/><path d='M33 29Q64 16 95 29L90 46 39 46Z' fill='#221e23' stroke='#b18b54' stroke-width='3'/><path d='M37 53L33 97M92 53L97 96M40 106Q64 114 88 106' fill='none' stroke='#8d6d49' stroke-width='3'/>");
}
