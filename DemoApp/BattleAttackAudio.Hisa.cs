using Godot;
using System.Collections.Generic;

internal enum HisaSound { Ball, Command, Send, Cover, Quiet }
public partial class BattleAttackAudio
{
    private readonly Dictionary<HisaSound, AudioStreamPlayer> _hisaVoices = new();
    internal void PlayHisa(HisaSound cue)
    {
        if (cue == HisaSound.Quiet) StopHisaSounds();
        string file = cue switch {
            HisaSound.Ball => "hisa_command_ball", HisaSound.Command => "hisa_command_voice",
            HisaSound.Send => "hisa_command_send", HisaSound.Cover => "hisa_cover_voice", _ => "hisa_quiet" };
        string path = "res://assets/audio/se/" + file + ".mp3";
        // 音声は後日支給。欠けている間は無音で、他の台詞を流用しない。
        if (!FileAccess.FileExists(path)) return;
        if (!_hisaVoices.TryGetValue(cue, out var voice))
        {
            voice = new AudioStreamPlayer { Stream = LoadSound(path), MaxPolyphony = 1, VolumeDb = -14 };
            AddChild(voice); _hisaVoices[cue] = voice;
        }
        voice.PitchScale = 1;
        voice.Play();
    }
    private void StopHisaSounds() { foreach (var voice in _hisaVoices.Values) voice.Stop(); }
}
