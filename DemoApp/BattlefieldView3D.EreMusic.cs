using Godot;
using System;
using System.Linq;

public partial class BattlefieldView3D
{
    internal const string DirgeSongA = "ere_song_clock_a.wav", DirgeSongB = "ere_song_clock_b.wav";
    private int _nextDirgeSong;
    internal string? LastDirgeSong { get; private set; }
    private GatherCast3D? _dirgeMusic;
    private Action? _releaseDirgeMusic;
    private int _dirgeMusicGiver;
    internal GatherCast3D? DirgeMusic => IsInstanceValid(_dirgeMusic) && !_dirgeMusic!.IsQueuedForDeletion()
        ? _dirgeMusic : null;

    // 戦闘ごとにAから開始し、発動単位でA→Bを繰り返す。素材が片方だけならある方を使う。
    private string? NextDirgeSong()
    {
        for (int i = 0; i < 2; i++)
        {
            string clip = _nextDirgeSong == 0 ? DirgeSongA : DirgeSongB;
            _nextDirgeSong = 1 - _nextDirgeSong;
            if (ResourceLoader.Exists("res://assets/audio/se/" + clip)) return LastDirgeSong = clip;
        }
        return LastDirgeSong = null;
    }
    private void ResetDirgeSongSequence() { _nextDirgeSong = 0; LastDirgeSong = null; }

    // 開始の光とは寿命を分け、演奏→受け渡し→時計の解放を一つの音楽でつなぐ。
    private void StartDirgeMusic(BattlePawn3D bard, string clip)
    {
        StopDirgeMusic();
        string path = "res://assets/audio/se/" + clip;
        if (!ResourceLoader.Exists(path)) return;
        var stream = GD.Load<AudioStream>(path);
        float end = (float)stream.GetLength() + .02f;
        var music = GatherEffect(end, end);
        _dirgeMusic = music; _dirgeMusicGiver = bard.InstanceId;
        music.Sound(clip, -8, start: .02f);
        var player = music.GetChildren().OfType<AudioStreamPlayer>().Single();
        // 原曲より長くは引き延ばさず、最初の追加手番の時計が消える時刻へ収める。
        _releaseDirgeMusic = () => {
            end = Math.Min(end, music.Age + DirgeAura3D.ReleaseDuration);
            music.Configure(end, end);
        };
        music.Draw(age => {
            if (!IsInstanceValid(bard) || !bard.PresentationAlive) { music.Cancel(); return; }
            float gain = Mathf.SmoothStep(0, 1, Mathf.Clamp((age - .02f) / .02f, 0, 1))
                * Mathf.SmoothStep(0, 1, Mathf.Clamp((end - age) / .35f, 0, 1));
            player.VolumeDb = -8 + Mathf.LinearToDb(Math.Max(.0001f, gain));
        });
    }
    private void ReleaseDirgeMusic(BattlePawn3D bard)
    {
        if (DirgeMusic is not null && bard.InstanceId == _dirgeMusicGiver) _releaseDirgeMusic?.Invoke();
    }
    private void StopDirgeMusic()
    {
        DirgeMusic?.Cancel();
        _dirgeMusic = null; _releaseDirgeMusic = null;
    }
}
