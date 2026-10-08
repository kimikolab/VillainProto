using Godot;
using System.Collections.Generic;

internal enum ZanSound { Cue, Slash, Finish, Recoil }

public partial class BattleAttackAudio
{
    private readonly Dictionary<ZanSound, AudioStreamPlayer> _zanVoices = new();
    internal void PlayZan(ZanSound cue)
    {
        string file = cue switch { ZanSound.Cue => "zan_vendetta_cue.mp3", ZanSound.Slash => "zan_vendetta_slash.mp3",
            ZanSound.Finish => "zan_vendetta_finish.mp3", _ => "zan_recoil.mp3" };
        string path = "res://assets/audio/se/" + file;
        // 指定素材が欠けた環境だけ、既存の短い合成音へ戻す。
        if (!FileAccess.FileExists(path))
        {
            PlayShockMark(cue switch { ZanSound.Cue => ShockMarkSound.Lock, ZanSound.Recoil => ShockMarkSound.Scar,
                ZanSound.Slash => ShockMarkSound.Snap, _ => ShockMarkSound.Interrupt });
            return;
        }
        if (!_zanVoices.TryGetValue(cue, out var voice))
        {
            voice = new AudioStreamPlayer { MaxPolyphony = 2, Stream = LoadSound(path) };
            AddChild(voice); _zanVoices[cue] = voice;
        }
        // 連打中にStreamを再設定すると再生中の余韻を切るため、読み込みは作成時だけ。
        voice.VolumeDb = cue == ZanSound.Recoil ? -24 : cue == ZanSound.Cue ? -20 : -14;
        voice.PitchScale = 1;
        voice.Play();
    }
    private void StopZanSounds()
    {
        foreach (var voice in _zanVoices.Values) voice.Stop();
    }
}
