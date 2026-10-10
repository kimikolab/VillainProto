using Godot;
using System;
using System.Collections.Generic;

internal enum ShockMarkSound { Beam, FeatherGain, Deploy, Spray, Lock, Scar, Charge, Interrupt, Cloud, ThunderHeavy, Powder, Thread, Snap, RallyHeal, Insight, BeamHit, FeatherMove, FeatherLost, ElectricWhip, MarkAdd, HushCry, HushSlap, HushOpening }

public partial class BattleAttackAudio
{
    private readonly Dictionary<(ShockMarkSound, int), AudioStreamWav> _shockMarkSounds = new();
    private readonly List<AudioStreamPlayer> _shockMarkVoices = new();
    private readonly Dictionary<ShockMarkSound, ulong> _shockMarkLast = new();
    private int _shockMarkVoice;

    // 短い光線・結晶・粉・電撃の専用音。速度で音程を変えず、同種の密集だけをまとめる。
    internal void PlayShockMark(ShockMarkSound cue, float pan = 0, double speed = 1)
    {
        if (TryPlayShockMarkAsset(cue, pan, speed)) return;
        ulong now = Time.GetTicksMsec();
        if (cue is not ShockMarkSound.Beam && _shockMarkLast.TryGetValue(cue, out ulong previous) && now - previous < 90) return;
        _shockMarkLast[cue] = now;
        while (_shockMarkVoices.Count < 6)
        {
            var player = new AudioStreamPlayer { VolumeDb = -13 };
            AddChild(player); _shockMarkVoices.Add(player);
        }
        int side = Math.Clamp((int)Math.Round(pan * 3), -3, 3);
        var key = (cue, side);
        if (!_shockMarkSounds.TryGetValue(key, out var sound))
            _shockMarkSounds[key] = sound = BuildShockMarkSound(cue, side / 3f);
        var voice = _shockMarkVoices[_shockMarkVoice++ % _shockMarkVoices.Count];
        voice.Stop(); voice.Stream = sound; voice.PitchScale = 1;
        voice.VolumeDb = cue switch {
            ShockMarkSound.Lock or ShockMarkSound.Scar => -22,
            ShockMarkSound.Cloud => -20,
            ShockMarkSound.Powder => -17,
            ShockMarkSound.ThunderHeavy => -10,
            ShockMarkSound.HushSlap => -9,
            _ => -14,
        };
        voice.Play();
    }

    private static AudioStreamWav BuildShockMarkSound(ShockMarkSound cue, float pan)
    {
        const int rate = 22050;
        double duration = cue switch { ShockMarkSound.Cloud => 1.15, ShockMarkSound.ThunderHeavy => 0.8,
            ShockMarkSound.Deploy or ShockMarkSound.Powder => 0.48, ShockMarkSound.Spray => 0.36,
            ShockMarkSound.HushSlap => .13, _ => 0.21 };
        int count = (int)(duration * rate);
        var bytes = new byte[count * 4];
        var random = new Random(291 + (int)cue);
        double low = 0, phase = 0;
        for (int i = 0; i < count; i++)
        {
            double t = i / (double)rate, u = t / duration, noise = random.NextDouble() * 2 - 1;
            low = low * 0.965 + noise * 0.035;
            double freq = cue switch {
                ShockMarkSound.Beam => 1600 * Math.Exp(-u * 3.6) + 240,
                ShockMarkSound.FeatherGain => 1300 + 800 * u,
                ShockMarkSound.Deploy => 430 + Math.Floor(u * 5) * 170,
                ShockMarkSound.Spray => 850 + Math.Sin(u * 32) * 450,
                ShockMarkSound.Lock => 1850,
                ShockMarkSound.Scar => 2800 * Math.Exp(-u * 4) + 120,
                ShockMarkSound.Thread => 600 + u * 2200,
                ShockMarkSound.RallyHeal => 320 + u * 780,
                ShockMarkSound.Insight => 2100 * Math.Exp(-u * 2) + 650,
                _ => 120 + (1 - u) * 380,
            };
            phase += Math.Tau * freq / rate;
            double tonal = Math.Sin(phase) * 0.45 + Math.Sin(phase * 2.01) * 0.18;
            double value = cue switch {
                ShockMarkSound.Cloud or ShockMarkSound.ThunderHeavy => low * 4 + Math.Sin(t * Math.Tau * 54) * 0.18,
                ShockMarkSound.Powder => (noise - low) * 0.16 + Math.Sin(t * 9700) * Math.Pow(Math.Max(0, Math.Sin(t * 61)), 14) * 0.08,
                ShockMarkSound.Charge or ShockMarkSound.Interrupt or ShockMarkSound.Snap => noise * 0.42 + tonal * 0.5,
                // 平手の乾いた破裂音。金属の倍音や長い余韻を付けない。
                ShockMarkSound.HushSlap => (noise - low) * .85 + Math.Sin(t * Math.Tau * 180) * Math.Exp(-t * 70) * .30,
                _ => tonal + noise * 0.06,
            };
            double envelope = Math.Min(1, t * 500) * Math.Pow(1 - u, cue == ShockMarkSound.Powder ? 1.5 : 2.5);
            if (cue == ShockMarkSound.Cloud) envelope *= Math.Sin(u * Math.PI);
            value = Math.Clamp(value * envelope, -0.95, 0.95);
            for (int channel = 0; channel < 2; channel++)
            {
                double gain = Math.Sqrt((1 + (channel == 0 ? -pan : pan)) * 0.5);
                short sample = (short)(value * gain * 26000);
                int offset = i * 4 + channel * 2;
                bytes[offset] = (byte)sample; bytes[offset + 1] = (byte)(sample >> 8);
            }
        }
        return new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Stereo = true, Data = bytes };
    }
    private void StopShockMarkSounds()
    {
        StopZanSounds();
        StopShockMarkAssets();
        foreach (var voice in _shockMarkVoices) voice.Stop();
        _shockMarkLast.Clear(); _shockMarkVoice = 0;
    }
    internal void StopShockMarkPresentation() { StopShockMarkSounds(); StopHisaSounds(); }

}
