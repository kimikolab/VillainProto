using Godot;
using System.Collections.Generic;

internal enum SomSound { Summon, Snub, Gather, Rain, Veil, Impact, Break, Stop }

public partial class BattleAttackAudio
{
    private readonly Dictionary<SomSound, AudioStreamPlayer> _somVoices = new();
    private readonly Dictionary<SomSound, ulong> _somLast = new();
    internal static string SomSoundFile(SomSound cue) => cue switch
    {
        SomSound.Summon => "som_summon.mp3",
        SomSound.Snub => "som_snub.mp3",
        SomSound.Break => "som_break.mp3",
        // 渇きの回復無効と同じ音源を参照し、複製を持たない。
        SomSound.Stop => System.IO.Path.GetFileName(Drought[0]),
        _ => "som_" + cue.ToString().ToLowerInvariant() + ".wav",
    };

    internal void PlaySom(SomSound cue)
    {
        string path = "res://assets/audio/se/" + SomSoundFile(cue);
        // MP3 / WAVを原形式で読み込む。音源を外した場合は無音で演出を続ける。
        if (!FileAccess.FileExists(path)) return;
        ulong now = Time.GetTicksMsec();
        if (_somLast.TryGetValue(cue, out ulong last) && now - last < 100) return;
        _somLast[cue] = now;
        if (!_somVoices.TryGetValue(cue, out var voice))
        {
            voice = new AudioStreamPlayer { Stream = LoadSound(path), MaxPolyphony = 2,
                VolumeDb = cue is SomSound.Rain or SomSound.Impact ? -15 : -11 };
            AddChild(voice); _somVoices.Add(cue, voice);
        }
        voice.PitchScale = 1;
        voice.Play();
    }
    internal void StopSomSounds()
    {
        foreach (var voice in _somVoices.Values) voice.Stop();
        _somLast.Clear();
    }
}
