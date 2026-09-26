using Godot;

public partial class BattleAttackAudio
{
    // 通常攻撃の4音枠と分離し、直後の攻撃やチャージによる中断・減衰を避ける。
    private AudioStreamPlayer? _plankPasteVoice, _plankSkillVoice, _plankRushVoice, _plankOpeningVoice;

    private void PlayPlankAccent(ref AudioStreamPlayer? voice, string path, int polyphony = 1)
    {
        if (voice is null)
        {
            voice = new AudioStreamPlayer { VolumeDb = -3, MaxPolyphony = polyphony };
            AddChild(voice);
            voice.Stream = LoadSound(path);
        }
        voice.Play();
    }

    private void StopPlankAccents()
    {
        _plankPasteVoice?.Stop();
        _plankSkillVoice?.Stop();
        _plankRushVoice?.Stop();
        _plankOpeningVoice?.Stop();
    }
    private static readonly string[] ArmorBreak = { "res://assets/audio/se/armor_break.mp3" };
    private static readonly string[] PlankPaste = { "res://assets/audio/se/tsugi_paste.mp3" };
    private static readonly string[] PlankSkillUp = { "res://assets/audio/se/tsugi_skill_up.wav" };
    private static readonly string[] PlankRush = { "res://assets/audio/se/tsugi_rush.mp3" };
    private static readonly string[] PlankReflect = {
        "res://assets/audio/se/tsugi_reflect_1.mp3",
        "res://assets/audio/se/tsugi_reflect_2.mp3",
    };

    public void PlayArmorBreak() => PlayVariation(ArmorBreak);
    public void PlayPlankPaste() => PlayPlankAccent(ref _plankPasteVoice, PlankPaste[0], 2);
    public void PlayPlankSkillUp() => PlayPlankAccent(ref _plankSkillVoice, PlankSkillUp[0]);
    public void PlayPlankRush() => PlayPlankAccent(ref _plankRushVoice, PlankRush[0]);
    // 既存の同音連続回避により、2種類なら必ず交互になる。
    public void PlayPlankReflect(int amount = 13)
        => PlayVariation(PlankReflect, Mathf.Lerp(0, 6, Mathf.Clamp((amount - 6) / 32f, 0, 1)));
    public void PlayPlankOpening()
    {
        if (_plankOpeningVoice?.Playing == true) return; // 前衛全員分を短い一打にまとめる。
        PlayPlankAccent(ref _plankOpeningVoice, PlankPaste[0]);
        _plankOpeningVoice!.VolumeDb = -10;
    }
}
