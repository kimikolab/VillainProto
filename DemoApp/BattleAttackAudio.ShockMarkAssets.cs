using Godot;
using System;
using System.Collections.Generic;

public partial class BattleAttackAudio
{
    // 発射8・着弾6・移動2・消失2・電撃鞭4・大落雷2。別の音に余韻を切らせない。
    internal const int ShockMarkAssetVoiceLimit = 24;
    internal static readonly string[] ShockMarkAssetFiles = {
        "misa_beam_1.wav", "misa_beam_2.wav", "misa_beam_3.wav", "misa_beam_hit.wav",
        "misa_deploy.wav", "misa_funnel_move.mp3", "misa_feather_lost.mp3",
        "shiga_electric_whip.mp3", "shiga_electric_hit.wav",
        "kata_thunder_heavy_4.mp3",
    };
    private readonly AudioStreamPlayer?[] _shockAssetVoices = new AudioStreamPlayer?[ShockMarkAssetVoiceLimit];
    private readonly Tween?[] _shockAssetFades = new Tween?[ShockMarkAssetVoiceLimit];
    private readonly Dictionary<int, int> _shockAssetNext = new();
    private readonly Dictionary<int, StringName> _shockPanBuses = new();
    internal readonly Dictionary<ShockMarkSound, int> ShockAssetPlays = new();
    internal readonly List<string> MisaBeamVariants = new();
    private int _misaBeamVariant;

    private bool TryPlayShockMarkAsset(ShockMarkSound cue, float pan, double speed)
    {
        speed = Math.Max(0.1, speed);
        switch (cue)
        {
            case ShockMarkSound.Beam:
                string beam = ShockMarkAssetFiles[_misaBeamVariant++ % 3];
                // 確認用には直近の3音だけ残す。戦闘台本の乱数は使わない。
                MisaBeamVariants.Add(beam);
                if (MisaBeamVariants.Count > 3) MisaBeamVariants.RemoveAt(0);
                ShockAssetLayer(0, 8, beam, -17, Math.Max(0.08, 0.24 / speed), pan);
                break;
            case ShockMarkSound.BeamHit:
                ShockAssetLayer(8, 6, "misa_beam_hit.wav", -14, Math.Max(0.06, 0.20 / speed), pan);
                break;
            case ShockMarkSound.Deploy:
                ShockAssetLayer(14, 2, "misa_deploy.wav", -13, 0.65 / speed, pan);
                break;
            case ShockMarkSound.FeatherMove:
                ShockAssetLayer(14, 2, "misa_funnel_move.mp3", -17, 0.38 / speed, pan);
                break;
            case ShockMarkSound.FeatherLost:
                // 粒ごとに鳴る消失は、密集した分だけまとめる。
                ulong now = Time.GetTicksMsec();
                if (_shockMarkLast.TryGetValue(cue, out ulong last) && now - last < 85) return true;
                _shockMarkLast[cue] = now;
                ShockAssetLayer(16, 2, "misa_feather_lost.mp3", -18, 0.32 / speed, pan);
                break;
            case ShockMarkSound.ElectricWhip:
                // 2組を交互に使い、連続する鞭でも革と電気が互いを奪わない。
                int whip = _shockAssetNext.GetValueOrDefault(18) % 2;
                _shockAssetNext[18] = whip + 1;
                ShockAssetLayer(18 + whip * 2, 1, "shiga_electric_whip.mp3", -10, 0.55 / speed, pan);
                ShockAssetLayer(19 + whip * 2, 1, "shiga_electric_hit.wav", -15, 0.65 / speed, pan);
                break;
            case ShockMarkSound.ThunderHeavy:
                ShockAssetLayer(22, 2, "kata_thunder_heavy_4.mp3", -15, 1.50 / speed, pan);
                break;
            default: return false;
        }
        ShockAssetPlays[cue] = ShockAssetPlays.GetValueOrDefault(cue) + 1;
        return true;
    }

    private void ShockAssetLayer(int first, int count, string file, float volume, double seconds, float pan)
    {
        int slot = first;
        if (count > 1)
        {
            int next = _shockAssetNext.GetValueOrDefault(first);
            slot += next % count;
            _shockAssetNext[first] = next + 1;
        }
        _shockAssetFades[slot]?.Kill();
        var voice = _shockAssetVoices[slot];
        if (voice is null)
        {
            voice = new AudioStreamPlayer { MaxPolyphony = 1 };
            AddChild(voice); _shockAssetVoices[slot] = voice;
        }
        voice.Stop();
        voice.Stream = LoadSound("res://assets/audio/se/" + file);
        voice.Bus = ShockPanBus(pan);
        voice.VolumeDb = volume;
        voice.PitchScale = 1; // 倍速でも原音を保ち、末尾の尺だけ縮める。
        double length = Math.Max(0.025, Math.Min(seconds, voice.Stream.GetLength()));
        voice.Play();
        var fade = _shockAssetFades[slot] = voice.CreateTween();
        fade.TweenInterval(length * 0.72);
        fade.TweenProperty(voice, "volume_db", -60f, length * 0.28);
        fade.TweenCallback(Callable.From(voice.Stop));
    }

    private StringName ShockPanBus(float pan)
    {
        int side = Math.Clamp((int)Math.Round(pan * 3), -2, 2);
        if (side == 0) return "Master";
        if (_shockPanBuses.TryGetValue(side, out var bus)) return bus;
        if (_shockPanBuses.Count == 0) TreeExiting += ReleaseShockPanBuses;
        int index = AudioServer.BusCount;
        AudioServer.AddBus();
        bus = new StringName($"ShockMark_{GetInstanceId()}_{side}");
        AudioServer.SetBusName(index, bus);
        AudioServer.SetBusSend(index, "Master");
        AudioServer.AddBusEffect(index, new AudioEffectPanner { Pan = side / 3f });
        _shockPanBuses[side] = bus;
        return bus;
    }

    private void ReleaseShockPanBuses()
    {
        StopShockMarkAssets();
        foreach (var bus in _shockPanBuses.Values)
        {
            int index = AudioServer.GetBusIndex(bus);
            if (index > 0) AudioServer.RemoveBus(index);
        }
        _shockPanBuses.Clear();
        TreeExiting -= ReleaseShockPanBuses;
    }

    private void StopShockMarkAssets()
    {
        for (int i = 0; i < _shockAssetVoices.Length; i++)
        {
            _shockAssetFades[i]?.Kill(); _shockAssetFades[i] = null;
            _shockAssetVoices[i]?.Stop();
        }
        _shockAssetNext.Clear(); _misaBeamVariant = 0;
        ShockAssetPlays.Clear(); MisaBeamVariants.Clear();
    }
}
