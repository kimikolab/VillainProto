using Godot;
using System;

public partial class BattleAttackAudio
{
    private readonly AudioStreamPlayer?[] _dustVoices = new AudioStreamPlayer?[4];
    private readonly Tween?[] _dustFades = new Tween?[4];
    private readonly double[] _dustSpeeds = new double[4];
    private int _nextDustVoice;
    private ulong? _lastDustSound, _lastDustFrame;
    private bool _dustPaused;
    internal int DustCracklePlays { get; private set; }

    internal bool PlayDustCrackle(int density, float pan, double speed)
    {
        string file = PowderBurnFileFor(density);
        if (!FileAccess.FileExists(file)) return false;
        ulong now = Time.GetTicksMsec(), frame = Engine.GetProcessFrames();
        // 同じ描画フレームの破裂は1音へ。倍速でも実時間35msの間隔を残す。
        if (_lastDustFrame == frame || _lastDustSound is ulong last && now - last < 35) return false;
        _lastDustSound = now; _lastDustFrame = frame;
        int index = _nextDustVoice++ % _dustVoices.Length;
        _dustFades[index]?.Kill();
        var voice = _dustVoices[index];
        if (voice is null) { voice = new AudioStreamPlayer { MaxPolyphony = 1 }; AddChild(voice); _dustVoices[index] = voice; }
        voice.Stop(); voice.Stream = LoadSound(file); voice.Bus = ShockPanBus(pan);
        voice.VolumeDb = -15 + PowderBurnBurst3D.SizeTier(density) * 2;
        voice.PitchScale = 1; voice.Play(); voice.StreamPaused = _dustPaused;
        _dustSpeeds[index] = Math.Max(.1, speed);
        double length = Math.Max(.09, .15 / _dustSpeeds[index]);
        var fade = _dustFades[index] = voice.CreateTween();
        fade.TweenInterval(length * .55);
        fade.TweenProperty(voice, "volume_db", -60f, length * .45);
        fade.TweenCallback(Callable.From(voice.Stop));
        fade.Finished += () => { if (_dustFades[index] == fade) _dustFades[index] = null; };
        if (_dustPaused) fade.Pause();
        DustCracklePlays++; return true;
    }

    internal void SetDustCrackleClock(bool paused, double speed)
    {
        _dustPaused = paused;
        for (int i = 0; i < _dustVoices.Length; i++)
        {
            if (_dustVoices[i] is { } voice) voice.StreamPaused = paused;
            if (_dustFades[i] is not { } fade || !fade.IsValid()) continue;
            fade.SetSpeedScale((float)(Math.Max(.1, speed) / _dustSpeeds[i]));
            if (paused) fade.Pause(); else fade.Play();
        }
    }

    internal void StopDustCrackle()
    {
        for (int i = 0; i < _dustVoices.Length; i++)
        {
            _dustFades[i]?.Kill(); _dustFades[i] = null;
            if (_dustVoices[i] is { } voice) { voice.Stop(); voice.StreamPaused = false; }
        }
        _nextDustVoice = 0; _lastDustSound = _lastDustFrame = null; _dustPaused = false;
    }
}
