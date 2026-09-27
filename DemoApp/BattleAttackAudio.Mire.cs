using Godot;
using System.Collections.Generic;

public partial class BattleAttackAudio
{
    // 爆発は出どころごとに一打。通常攻撃の音枠に奪われない独立した枠で鳴らす。
    private readonly Dictionary<string, AudioStreamPlayer> _mireVoices = new();
    private readonly Dictionary<string, ulong> _mireLastAt = new();
    internal static readonly string[] MireSounds = {
        "res://assets/audio/se/mio_burst_small.wav",
        "res://assets/audio/se/mio_burst_large.wav",
        "res://assets/audio/se/mio_slam.mp3",
        "res://assets/audio/se/mio_conduct.wav",
        "res://assets/audio/se/mio_flow.wav",
    };

    public void PlayMireBurst(bool large) => PlayMireSound(MireSounds[large ? 1 : 0], large ? -8 : -12, 2);
    public void PlayMireSlam() => PlayMireSound(MireSounds[2], -10, 1);
    public void PlayMireConduct() => PlayMireSound(MireSounds[3], -14, 2, 70);
    public void PlayMireFlow() => PlayMireSound(MireSounds[4], -18, 2, 90);

    private void PlayMireSound(string path, float volume, int polyphony, ulong cooldown = 0)
    {
        ulong now = Time.GetTicksMsec();
        if (_mireLastAt.TryGetValue(path, out ulong previous) && now - previous < cooldown) return;
        if (!_mireVoices.TryGetValue(path, out var voice))
        {
            voice = new AudioStreamPlayer { Stream = LoadSound(path), VolumeDb = volume, MaxPolyphony = polyphony };
            AddChild(voice);
            _mireVoices[path] = voice;
        }
        // 2倍速でも素材の音程を変えない。近接した通電・移動の音だけまとめる。
        voice.Play();
        _mireLastAt[path] = now;
    }

    private void StopMireSounds()
    {
        foreach (var voice in _mireVoices.Values) voice.Stop();
        _mireLastAt.Clear();
    }
}
