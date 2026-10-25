using Godot;
using System;

// 覚醒・着地・吸収は斬撃とは別の枠。長い余韻が斬撃の再生枠を奪わないようにする。
public partial class BattleAttackAudio
{
    private static readonly string[] RicaAwakenFiles = {
        "res://assets/audio/se/rica_awaken_3.wav",
        "res://assets/audio/se/rica_awaken_6.wav",
        "res://assets/audio/se/rica_awaken_9.wav",
    };
    private const string RicaLandingFile = "res://assets/audio/se/rica_landing.wav";
    private const string RicaAbsorbFile = "res://assets/audio/se/rica_absorb.wav";
    private readonly AudioStreamPlayer?[] _ricaEventVoices = new AudioStreamPlayer?[3];
    private bool _ricaEventsPaused;
    private ulong? _ricaLastAbsorbAt;
    internal event Action<string>? RicaEventPlayed;

    internal void PlayRicaAwakening(int stage)
    {
        if (stage < 1) return;
        int tier = Math.Clamp(stage, 1, 3);
        PlayRicaEvent(0, RicaAwakenFiles[tier - 1], -8, "awaken" + tier * 3);
    }
    internal void PlayRicaLanding() => PlayRicaEvent(1, RicaLandingFile, -6, "landing");
    internal void PlayRicaAbsorption()
    {
        ulong now = Time.GetTicksMsec();
        if (_ricaLastAbsorbAt is ulong previous && now - previous < 180) return;
        if (PlayRicaEvent(2, RicaAbsorbFile, -16, "absorb")) _ricaLastAbsorbAt = now;
    }
    private bool PlayRicaEvent(int slot, string path, float volumeDb, string cue)
    {
        if (!ResourceLoader.Exists(path) && !FileAccess.FileExists(path)) return false;
        var voice = _ricaEventVoices[slot];
        if (voice is null)
        {
            voice = new AudioStreamPlayer { MaxPolyphony = 1 };
            AddChild(voice); _ricaEventVoices[slot] = voice;
        }
        voice.Stop(); voice.Stream = LoadSound(path);
        voice.VolumeDb = volumeDb; voice.PitchScale = 1;
        voice.Play(); voice.StreamPaused = _ricaEventsPaused;
        RicaEventPlayed?.Invoke(cue);
        return true;
    }
    internal void PauseRicaEvents(bool paused)
    {
        _ricaEventsPaused = paused;
        foreach (var voice in _ricaEventVoices) if (voice is not null) voice.StreamPaused = paused;
    }
    internal void StopRicaEvents()
    {
        foreach (var voice in _ricaEventVoices)
            if (voice is not null) { voice.Stop(); voice.StreamPaused = false; }
        _ricaEventsPaused = false; _ricaLastAbsorbAt = null;
    }
}
