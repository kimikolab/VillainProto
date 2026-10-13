using Godot;
using System;

public partial class BattleAttackAudio
{
    internal static string PowderBurnFileFor(int density) =>
        $"res://assets/audio/se/tou_powder_spark_{PowderBurnBurst3D.SizeTier(density)}.wav";
    private readonly AudioStreamPlayer?[] _powderBurnVoices = new AudioStreamPlayer?[3];
    private readonly Tween?[] _powderBurnFades = new Tween?[3];
    private readonly double[] _powderBurnSpeeds = new double[3];
    private int _nextPowderBurn;
    private ulong? _lastPowderBurn;
    private ulong? _lastPowderBurnFrame;
    private int _lastPowderBurnTier;
    private int _lastPowderBurnVoice;
    private bool _powderBurnPaused;

    internal bool PlayPowderBurn(int density, float pan, double speed)
    {
        // 購入音源が未配置の環境でも、粉の演出は続ける。
        string file = PowderBurnFileFor(density);
        if (!FileAccess.FileExists(file)) return false;
        int tier = PowderBurnBurst3D.SizeTier(density);
        ulong now = Time.GetTicksMsec();
        ulong frame = Engine.GetProcessFrames();
        bool grouped = _lastPowderBurnFrame == frame || _lastPowderBurn is ulong last && now - last < 85;
        if (grouped && tier <= _lastPowderBurnTier) return false;
        _lastPowderBurnFrame = frame;
        speed = Math.Max(.1, speed);
        // 密集時も大きい火花の音を優先。同じ枠を差し替えて音量を重ねない。
        int index = grouped ? _lastPowderBurnVoice : _nextPowderBurn++ % _powderBurnVoices.Length;
        _powderBurnFades[index]?.Kill();
        var voice = _powderBurnVoices[index];
        if (voice is null)
        {
            voice = new AudioStreamPlayer { MaxPolyphony = 1 };
            AddChild(voice); _powderBurnVoices[index] = voice;
        }
        voice.Stop(); voice.Stream = LoadSound(file);
        // 放電・攻撃音に埋もれない音量へ。小-6／中-4／大-2 dB。
        voice.Bus = ShockPanBus(pan); voice.VolumeDb = -8 + tier * 2; voice.PitchScale = 1;
        voice.Play(); voice.StreamPaused = _powderBurnPaused;
        _lastPowderBurn = Time.GetTicksMsec();
        _lastPowderBurnTier = tier; _lastPowderBurnVoice = index;
        // 原本は各2.5秒。冒頭の火花を残し、大きいほど余韻を少し長くする。
        double length = Math.Min(voice.Stream.GetLength(), Math.Max(.30, (.45 + (tier - 1) * .10) / speed));
        _powderBurnSpeeds[index] = speed;
        var fade = _powderBurnFades[index] = voice.CreateTween();
        fade.TweenInterval(length * .72);
        fade.TweenProperty(voice, "volume_db", -60f, length * .28);
        fade.TweenCallback(Callable.From(voice.Stop));
        fade.Finished += () => { if (_powderBurnFades[index] == fade) _powderBurnFades[index] = null; };
        if (_powderBurnPaused) fade.Pause();
        return true;
    }

    internal void SetPowderBurnClock(bool paused, double speed)
    {
        _powderBurnPaused = paused;
        for (int i = 0; i < _powderBurnVoices.Length; i++)
        {
            if (_powderBurnVoices[i] is { } voice) voice.StreamPaused = paused;
            if (_powderBurnFades[i] is not { } fade || !fade.IsValid()) continue;
            fade.SetSpeedScale((float)(Math.Max(.1, speed) / _powderBurnSpeeds[i]));
            if (paused) fade.Pause(); else fade.Play();
        }
    }

    internal void StopPowderBurn()
    {
        for (int i = 0; i < _powderBurnVoices.Length; i++)
        {
            _powderBurnFades[i]?.Kill(); _powderBurnFades[i] = null;
            if (_powderBurnVoices[i] is { } voice) { voice.Stop(); voice.StreamPaused = false; }
        }
        _lastPowderBurn = _lastPowderBurnFrame = null; _nextPowderBurn = 0; _powderBurnPaused = false;
        _lastPowderBurnTier = _lastPowderBurnVoice = 0;
    }
}
