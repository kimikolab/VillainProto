using Godot;
using System;
using System.Collections.Generic;

public partial class BattleAttackAudio
{
    internal static readonly Dictionary<string, string> FireSounds = new() {
        ["raise"] = "fire_hota_raise.wav", ["slam"] = "fire_sword_slam.mp3",
        ["explosion"] = "fire_explosion.mp3", ["inferno"] = "fire_inferno.mp3",
        ["jet"] = "fire_jet.mp3", ["projectile"] = "fire_projectile.mp3",
        ["strong_pierce"] = "fire_hota_strong_pierce.mp3",
        ["aura"] = "fire_aura.mp3", ["gift"] = "fire_gift.mp3",
        ["gift_turn"] = "fire_gift_turn.mp3", ["overflow"] = "fire_condense.mp3",
        ["heal"] = "fire_heal.mp3", ["charge"] = "fire_charge.mp3",
    };
    private const string FireAudioRoot = "res://assets/audio/se/";
    // 燃焼軸を一段前へ。大技と補助音の相対差は保つ。
    private const float FireVolumeBoostDb = 3;
    private readonly Dictionary<string, ulong> _fireSoundTimes = new();
    // 大技の4層だけ独立させ、連続する着火通知に火柱の余韻を切らせない。
    private readonly AudioStreamPlayer?[] _fireVoices = new AudioStreamPlayer?[4];
    private readonly Tween?[] _fireFades = new Tween?[4];
    internal readonly Dictionary<string, int> FireSoundPlays = new();
    internal int FireSoundGeneration { get; private set; }

    internal void PlayFireSound(string cue, double speed = 1, double seconds = 0.55)
    {
        speed = Math.Max(0.1, speed);
        ulong now = Time.GetTicksMsec();
        ulong cooldown = cue is "grow" or "guard" or "flow" or "fed" or "overflow" or "heal" ? 85UL
            : cue == "aura" ? 180UL : 0UL;
        if (cooldown > 0 && _fireSoundTimes.TryGetValue(cue, out ulong last) && now - last < cooldown) return;
        _fireSoundTimes[cue] = now;
        FireSoundPlays[cue] = FireSoundPlays.GetValueOrDefault(cue) + 1;
        if (cue is "raise" or "charge")
        {
            FireLayer(0, cue, -9, seconds / speed);
            return;
        }
        if (cue == "burnout")
        {
            StopFireCharge();
            FireLayer(1, "slam", -10, 0.85 / speed);
            FireLayer(2, "explosion", -12, 1.05 / speed);
            FireLayer(3, "inferno", -16, 1.35 / speed, 0.07 / speed);
            return;
        }
        if (cue == "blaze")
        {
            StopFireCharge();
            FireLayer(1, "aura", -12, 0.60 / speed);
            FireLayer(3, "jet", -12, 0.85 / speed);
            return;
        }
        if (cue is "jet" or "projectile" or "strong_pierce")
        {
            StopFireCharge();
            FireLayer(1, cue, cue == "projectile" ? -13 : -11, seconds / speed);
            return;
        }
        string path = FireSounds.TryGetValue(cue, out var file) ? FireAudioRoot + file : cue switch {
            "release" => FireAudioRoot + "sword_slash_003.wav",
            "mark" => FireAudioRoot + "heal_trait.mp3",
            "impact" or "rain" or "embers" => FireAudioRoot + "burn_damage.mp3",
            _ => FireAudioRoot + "burn_gain.mp3",
        };
        float boost = cue switch {
            "rain" => -8, "embers" => -4, "heal" => -7,
            "overflow" or "fed" or "aura" => -5, "gift_turn" => -1, _ => -3,
        };
        PlayVariation(new[] { path }, boost + FireVolumeBoostDb);
    }

    private void FireLayer(int slot, string cue, float volume, double seconds, double delay = 0)
    {
        _fireFades[slot]?.Kill();
        var voice = _fireVoices[slot];
        if (voice is null)
        {
            voice = new AudioStreamPlayer { MaxPolyphony = 1 };
            AddChild(voice);
            _fireVoices[slot] = voice;
        }
        voice.Stop();
        voice.Stream = LoadSound(FireAudioRoot + FireSounds[cue]);
        voice.VolumeDb = volume + FireVolumeBoostDb;
        voice.PitchScale = 1; // 倍速でも原音。絵の尺に合わせて末尾だけフェードする。
        double length = Math.Max(0.025, Math.Min(seconds, voice.Stream.GetLength()));
        var tween = _fireFades[slot] = voice.CreateTween();
        if (delay > 0) tween.TweenInterval(delay);
        tween.TweenCallback(Callable.From(() => voice.Play()));
        tween.TweenInterval(length * 0.78);
        tween.TweenProperty(voice, "volume_db", -60f, length * 0.22);
        tween.TweenCallback(Callable.From(voice.Stop));
    }

    internal void StopFireCharge()
    {
        _fireFades[0]?.Kill();
        _fireVoices[0]?.Stop();
    }

    internal void StopFireSounds()
    {
        FireSoundGeneration++;
        for (int i = 0; i < _fireVoices.Length; i++)
        {
            _fireFades[i]?.Kill();
            _fireFades[i] = null;
            _fireVoices[i]?.Stop();
        }
        _fireSoundTimes.Clear();
        FireSoundPlays.Clear();
    }
}
