using Godot;
using System;
using System.Collections.Generic;

public partial class BattleAttackAudio
{
    internal const string DustBurstFile = "res://assets/audio/se/tou_dust_burst.wav";
    private const int DustVoiceLimit = 8;
    private readonly AudioStreamPlayer?[] _dustVoices = new AudioStreamPlayer?[DustVoiceLimit];
    private readonly Tween?[] _dustFades = new Tween?[DustVoiceLimit];
    private readonly double[] _dustSpeeds = new double[DustVoiceLimit];
    private readonly Dictionary<int, StringName> _dustPanBuses = new();
    private StringName? _dustMixBus;
    private int _nextDustVoice;
    private ulong? _lastDustSound, _lastDustFrame;
    private bool _dustPaused;
    internal int DustCracklePlays { get; private set; }

    internal bool PlayDustCrackle(int density, float pan, double speed)
    {
        string file = DustBurstFile;
        if (!FileAccess.FileExists(file)) return false;
        ulong now = Time.GetTicksMsec(), frame = Engine.GetProcessFrames();
        // 同じ描画フレームの破裂は1音へ。倍速でも実時間35msの間隔を残す。
        if (_lastDustFrame == frame || _lastDustSound is ulong last && now - last < 35) return false;
        _lastDustSound = now; _lastDustFrame = frame;
        int index = _nextDustVoice++ % _dustVoices.Length;
        _dustFades[index]?.Kill();
        var voice = _dustVoices[index];
        if (voice is null) { voice = new AudioStreamPlayer { MaxPolyphony = 1 }; AddChild(voice); _dustVoices[index] = voice; }
        voice.Stop(); voice.Stream = LoadSound(file); voice.Bus = DustPanBus(pan);
        voice.VolumeDb = -6 + PowderBurnBurst3D.SizeTier(density);
        voice.PitchScale = 1; voice.Play(); voice.StreamPaused = _dustPaused;
        _dustSpeeds[index] = Math.Max(.1, speed);
        // 倍速でも破裂の芯と短い余韻を残す。原音程は変えない。
        double length = Math.Max(.30, .40 / _dustSpeeds[index]);
        var fade = _dustFades[index] = voice.CreateTween();
        fade.TweenInterval(length * .72);
        fade.TweenProperty(voice, "volume_db", -60f, length * .28);
        fade.TweenCallback(Callable.From(voice.Stop));
        fade.Finished += () => { if (_dustFades[index] == fade) _dustFades[index] = null; };
        if (_dustPaused) fade.Pause();
        DustCracklePlays++; return true;
    }

    private StringName DustPanBus(float pan)
    {
        if (_dustMixBus is null)
        {
            int index = AudioServer.BusCount; AudioServer.AddBus();
            _dustMixBus = new StringName($"DustBurst_{GetInstanceId()}");
            AudioServer.SetBusName(index, _dustMixBus);
            // 見えている粉塵爆発の音は粛のこもりで消さない。
            AudioServer.SetBusSend(index, "Master");
            AudioServer.AddBusEffect(index, new AudioEffectHardLimiter { CeilingDb = -3, Release = .06f });
            TreeExiting += ReleaseDustBuses;
        }
        int side = Math.Clamp((int)Math.Round(pan * 3), -2, 2);
        if (side == 0) return _dustMixBus;
        if (_dustPanBuses.TryGetValue(side, out var bus)) return bus;
        int panIndex = AudioServer.BusCount; AudioServer.AddBus();
        bus = new StringName($"DustBurst_{GetInstanceId()}_{side}");
        AudioServer.SetBusName(panIndex, bus);
        AudioServer.SetBusSend(panIndex, _dustMixBus);
        AudioServer.AddBusEffect(panIndex, new AudioEffectPanner { Pan = side / 3f });
        _dustPanBuses[side] = bus;
        return bus;
    }

    private void ReleaseDustBuses()
    {
        StopDustCrackle();
        foreach (var bus in _dustPanBuses.Values)
        {
            int index = AudioServer.GetBusIndex(bus);
            if (index > 0) AudioServer.RemoveBus(index);
        }
        _dustPanBuses.Clear();
        if (_dustMixBus is not null)
        {
            int index = AudioServer.GetBusIndex(_dustMixBus);
            if (index > 0) AudioServer.RemoveBus(index);
            _dustMixBus = null;
        }
        TreeExiting -= ReleaseDustBuses;
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
