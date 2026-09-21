using Godot;
using System;
using System.Collections.Generic;

// 戦闘ルールとは独立した波別BGM。再生速度ではピッチを変えず、戦闘画面にいる間だけ鳴らす。
public partial class BattleMusic : Node
{
    private static readonly string[] WaveTracks =
    {
        "res://assets/audio/bgm/wave_01.mp3",
        "res://assets/audio/bgm/wave_02.mp3",
        "res://assets/audio/bgm/wave_03.mp3",
        "res://assets/audio/bgm/wave_04.mp3",
        "res://assets/audio/bgm/wave_05.mp3",
    };

    private readonly Dictionary<string, AudioStreamMP3> _streams = new();
    private AudioStreamPlayer _player = null!;
    private Tween? _fade;
    internal int CurrentWaveIndex { get; private set; } = -1;
    internal bool IsPlaying => _player?.Playing == true;

    public override void _Ready()
    {
        // SEは短い瞬間音なので、BGMは常時聞こえる基準まで上げる。曲ごとの差は試聴後に個別調整する。
        _player = new AudioStreamPlayer { VolumeDb = -12, MaxPolyphony = 1 };
        AddChild(_player);
    }

    public void PlayWave(int stageIndex)
    {
        if (stageIndex < 0 || stageIndex >= WaveTracks.Length) return;
        string path = WaveTracks[stageIndex];
        if (!_streams.TryGetValue(path, out AudioStreamMP3? stream))
        {
            stream = ResourceLoader.Exists(path)
                ? GD.Load<AudioStreamMP3>(path)
                : AudioStreamMP3.LoadFromFile(path);
            stream.Loop = true;
            _streams[path] = stream;
        }

        _fade?.Kill();
        _player.Stop();
        _player.Stream = stream;
        _player.VolumeDb = -40;
        _player.Play();
        CurrentWaveIndex = stageIndex;
        _fade = CreateTween();
        _fade.TweenProperty(_player, "volume_db", -12.0f, 0.45);
    }

    public void Stop()
    {
        _fade?.Kill();
        _fade = null;
        _player.Stop();
        CurrentWaveIndex = -1;
    }
}
