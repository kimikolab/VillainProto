using Godot;
using BattleCore;
using System.Collections.Generic;

// 戦闘計算とは独立した音選び。専用音の未登録キャラには共通音を使う。
public partial class BattleAttackAudio : Node
{
    // SE全体をさらに3 dB上げ、専用音との音量差を保つ。
    private const float NormalVolumeDb = -10;

    private readonly RandomNumberGenerator _random = new();
    private readonly Dictionary<(string, AttackPattern), string[]> _overrides = new();
    private readonly Dictionary<string, AudioStream> _streams = new();
    private readonly Dictionary<string, int> _last = new();
    private readonly AudioStreamPlayer[] _voices = new AudioStreamPlayer[4];
    private int _nextVoice;
    private readonly float[] _voiceBoosts = new float[4];
    private readonly AudioStreamPlayer[] _chargeVoices = new AudioStreamPlayer[2];
    private int _nextChargeVoice;
    private AudioStreamPlayer _yokeVoice = null!;
    private readonly AudioStreamPlayer[] _hushVoices = new AudioStreamPlayer[2];
    private int _nextHushVoice;
    private Tween? _duckTween;
    private readonly AudioStreamPlayer[] _deathVoices = new AudioStreamPlayer[2];
    private int _nextDeathVoice;
    private ulong _lastAttackUpAt;
    private ulong _lastAttackDownAt;
    private const string EnemyDeath = "res://assets/audio/se/death_enemy.mp3";
    private const string PlayerDeath = "res://assets/audio/se/death_player.mp3";
    private const string Finish = "res://assets/audio/se/finish_ko.mp3";
    private static readonly string[] BattleStart = { "res://assets/audio/se/battle_start.mp3" };
    private static readonly string[] AttackUp =
    {
        "res://assets/audio/se/attack_up_1.mp3",
        "res://assets/audio/se/attack_up_2.mp3",
        "res://assets/audio/se/attack_up_3.mp3",
    };
    private static readonly string[] AttackDown =
    {
        "res://assets/audio/se/attack_down_1.mp3",
        "res://assets/audio/se/attack_down_2.mp3",
        "res://assets/audio/se/attack_down_3.mp3",
    };
    private static readonly string[] YomiBonus = { "res://assets/audio/se/yomi_bonus.mp3" };
    private static readonly string[] YomiAttack = { "res://assets/audio/se/yomi_attack.mp3" };
    private static readonly string[] UtsuAttack =
    {
        "res://assets/audio/se/utsu_attack_1.mp3",
        "res://assets/audio/se/utsu_attack_2.mp3",
        "res://assets/audio/se/utsu_attack_3.mp3",
        "res://assets/audio/se/utsu_attack_4.mp3",
        "res://assets/audio/se/utsu_attack_5.mp3",
    };
    private static readonly string[] MudoAttack =
    {
        "res://assets/audio/se/mudo_attack_1.mp3",
        "res://assets/audio/se/mudo_attack_2.mp3",
        "res://assets/audio/se/mudo_attack_3.mp3",
    };
    private static readonly string[] ShigaAttack =
    {
        "res://assets/audio/se/shiga_attack_1.mp3",
        "res://assets/audio/se/shiga_attack_2.mp3",
        "res://assets/audio/se/shiga_attack_3.mp3",
    };
    private static readonly string[] KadoHit = { "res://assets/audio/se/kado_hit.mp3" };
    private static readonly string[] SoraThrustMedium = { "res://assets/audio/se/sora_thrust_2_3.mp3" };
    private static readonly string[] SoraThrustFull = { "res://assets/audio/se/sora_thrust_4.mp3" };
    private static readonly string[] Heal = { "res://assets/audio/se/heal_magic_1.mp3" };
    private static readonly string[] TraitHeal = { "res://assets/audio/se/heal_trait.mp3" };
    private static readonly string[] GaldReflect = { "res://assets/audio/se/gald_reflect.mp3" };
    private static readonly string[] KadoCounter = { "res://assets/audio/se/kado_counter.mp3" };
    private static readonly string[] MagicCharge = { "res://assets/audio/se/charge_magic.mp3" };
    private static readonly string[] MagicChargeRelease = { "res://assets/audio/se/charge_magic_release.mp3" };
    private static readonly string[] PhysicalCharge = { "res://assets/audio/se/charge_physical.mp3" };
    private static readonly string[] PhysicalChargeRelease = { "res://assets/audio/se/charge_physical_release.mp3" };
    private static readonly string[] Summon = { "res://assets/audio/se/summon.mp3" };
    private static readonly string[] Revive = { "res://assets/audio/se/revive.mp3" };
    private static readonly string[] BurnGain = { "res://assets/audio/se/burn_gain.mp3" };
    private static readonly string[] BurnDamage = { "res://assets/audio/se/burn_damage.mp3" };
    private static readonly string[] PoisonGain = { "res://assets/audio/se/poison_gain.mp3" };
    private static readonly string[] PoisonDamage = { "res://assets/audio/se/poison_damage.mp3" };
    private static readonly string[] StaggerGain = { "res://assets/audio/se/stagger_gain.mp3" };
    private static readonly string[] ConfusedGain = { "res://assets/audio/se/confused_gain.mp3" };
    private static readonly string[] CowedGain = { "res://assets/audio/se/cowed_gain.mp3" };
    private static readonly string[] Yoke = { "res://assets/audio/se/rule_yoke.mp3" };
    private static readonly string[] Drought = { "res://assets/audio/se/rule_drought.mp3" };
    private static readonly string[] HushBlock = { "res://assets/audio/se/hush_block.mp3" };
    private static readonly string[] HushBreak =
    {
        "res://assets/audio/se/hush_break_1.mp3",
        "res://assets/audio/se/hush_break_2.mp3",
        "res://assets/audio/se/hush_break_3.mp3",
    };
    private static readonly string[] Common =
    {
        "res://assets/audio/se/sword_slash_001.wav",
        "res://assets/audio/se/sword_slash_002.wav",
        "res://assets/audio/se/sword_slash_003.wav",
    };
    private static readonly string[] EnemyCommon =
    {
        "res://assets/audio/se/enemy_swish_hit_004.wav",
        "res://assets/audio/se/enemy_swish_hit_005.wav",
        "res://assets/audio/se/enemy_swish_hit_006.wav",
    };
    private static readonly string[] ParrySounds =
    {
        "res://assets/audio/se/parry_1.mp3",
        "res://assets/audio/se/parry_2.mp3",
        "res://assets/audio/se/parry_3.mp3",
    };

    public override void _Ready()
    {
        _random.Randomize();
        for (int i = 0; i < _voices.Length; i++)
        {
            _voices[i] = new AudioStreamPlayer { VolumeDb = NormalVolumeDb, MaxPolyphony = 1 };
            AddChild(_voices[i]);
        }
        for (int i = 0; i < _chargeVoices.Length; i++)
        {
            _chargeVoices[i] = new AudioStreamPlayer { VolumeDb = -5, MaxPolyphony = 1 };
            AddChild(_chargeVoices[i]);
        }
        _yokeVoice = new AudioStreamPlayer { VolumeDb = -7, MaxPolyphony = 1 };
        AddChild(_yokeVoice);
        for (int i = 0; i < _hushVoices.Length; i++)
        {
            _hushVoices[i] = new AudioStreamPlayer { VolumeDb = -6, MaxPolyphony = 1 };
            AddChild(_hushVoices[i]);
        }
        foreach (string path in Common) LoadSound(path);
        foreach (string path in EnemyCommon) LoadSound(path);
        foreach (string path in ParrySounds) LoadSound(path);
        LoadSound(EnemyDeath);
        LoadSound(PlayerDeath);
        LoadSound(Finish);
        LoadSound(BattleStart[0]);
        foreach (string path in AttackUp) LoadSound(path);
        foreach (string path in AttackDown) LoadSound(path);
        LoadSound(YomiBonus[0]);
        LoadSound(YomiAttack[0]);
        foreach (string path in UtsuAttack) LoadSound(path);
        foreach (string path in MudoAttack) LoadSound(path);
        foreach (string path in ShigaAttack) LoadSound(path);
        LoadSound(KadoHit[0]);
        LoadSound(SoraThrustMedium[0]);
        LoadSound(SoraThrustFull[0]);
        LoadSound(Heal[0]);
        LoadSound(TraitHeal[0]);
        LoadSound(GaldReflect[0]);
        LoadSound(KadoCounter[0]);
        LoadSound(MagicCharge[0]);
        LoadSound(MagicChargeRelease[0]);
        LoadSound(PhysicalCharge[0]);
        LoadSound(PhysicalChargeRelease[0]);
        LoadSound(Summon[0]);
        LoadSound(Revive[0]);
        LoadSound(BurnGain[0]);
        LoadSound(BurnDamage[0]);
        LoadSound(PoisonGain[0]);
        LoadSound(PoisonDamage[0]);
        LoadSound(StaggerGain[0]);
        LoadSound(ConfusedGain[0]);
        LoadSound(CowedGain[0]);
        LoadSound(Yoke[0]);
        LoadSound(Drought[0]);
        LoadSound(HushBlock[0]);
        foreach (string path in HushBreak) LoadSound(path);
        for (int i = 0; i < _deathVoices.Length; i++)
        {
            _deathVoices[i] = new AudioStreamPlayer { VolumeDb = -5, MaxPolyphony = 1 };
            AddChild(_deathVoices[i]);
        }
    }

    public void Register(string unitId, AttackPattern pattern, params string[] paths)
    {
        if (paths.Length > 0) _overrides[(unitId, pattern)] = paths;
    }

    private AudioStream LoadSound(string path)
    {
        if (!_streams.TryGetValue(path, out var stream))
        {
            stream = ResourceLoader.Exists(path) ? GD.Load<AudioStream>(path)
                : path.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase)
                    ? AudioStreamMP3.LoadFromFile(path) : AudioStreamWav.LoadFromFile(path);
            _streams[path] = stream;
        }
        return stream;
    }

    public void PlayAttack(string unitId, int team, AttackPattern pattern, bool reaction, bool charged)
    {
        if (charged && TryChargeSound(unitId, release: true, out string[] charge))
        {
            PlayChargeAccent(charge, 0.55);
            return;
        }
        if (reaction && unitId == "yomi") { PlayVariation(YomiBonus); return; }
        if (!reaction && unitId == "yomi") { PlayVariation(YomiAttack); return; }
        if (unitId == "utsu") { PlayVariation(UtsuAttack); return; }
        if (unitId == "mudo") { PlayVariation(MudoAttack, 3); return; }
        if (unitId == "shiga") { PlayVariation(ShigaAttack); return; }
        if (reaction && unitId == "kado") { PlayVariation(KadoCounter); return; }
        string[] paths = _overrides.GetValueOrDefault((unitId, pattern))
            ?? (team == BattleContext.EnemyTeam ? EnemyCommon : Common);
        PlayVariation(paths);
    }

    internal static string? ThrustSoundPath(int charge)
        => charge >= 4 ? SoraThrustFull[0] : charge >= 2 ? SoraThrustMedium[0] : null;

    public void PlayThrust(string unitId, int team, int charge)
    {
        if (ThrustSoundPath(charge) is { } path) PlayChargeAccent(new[] { path }, 0.35);
        else PlayAttack(unitId, team, AttackPattern.Pierce, reaction: false, charged: false);
    }

    public void PlayParry() => PlayVariation(ParrySounds);
    public void PlayGaldReflect() => PlayVariation(GaldReflect);
    public void PlayCowedGain() => PlayVariation(CowedGain);
    public void PlayBattleStart() => PlayVariation(BattleStart);
    public void PlayAttackChange(int change)
    {
        if (change == 0) return;
        // 開戦時の全体強化・弱体は同じフレームに人数ぶん並ぶ。方向ごとに一音へまとめる。
        ulong now = Time.GetTicksMsec();
        ulong last = change > 0 ? _lastAttackUpAt : _lastAttackDownAt;
        if (last > 0 && now - last < 180) return;
        if (change > 0) _lastAttackUpAt = now;
        else _lastAttackDownAt = now;
        PlayVariation(change > 0 ? AttackUp : AttackDown);
    }
    public void PlayKadoHit() => PlayVariation(KadoHit);
    public void PlayHeal(string? sourceUnitId, bool selfHeal)
    {
        bool dedicatedHealer = !selfHeal && sourceUnitId is
            "nara" or "nono" or "hari" or "chaplain" or "chaplain_g";
        PlayVariation(dedicatedHealer ? Heal : TraitHeal);
    }
    public void PlaySummon() => PlayVariation(Summon);
    public void PlayRevive() => PlayVariation(Revive);
    public void PlayStatusGain(string key)
    {
        switch (key)
        {
            case StatusKeys.Burn: PlayVariation(BurnGain); break;
            case StatusKeys.Poison: PlayVariation(PoisonGain); break;
            case StatusKeys.Stagger: PlayVariation(StaggerGain); break;
            case StatusKeys.Confused: PlayVariation(ConfusedGain); break;
        }
    }

    public void PlayStatusDamage(string? label)
    {
        if (label == StatusKeys.LabelOf(StatusKeys.Burn)) PlayVariation(BurnDamage);
        else if (label == StatusKeys.LabelOf(StatusKeys.Poison)) PlayVariation(PoisonDamage);
    }
    public void PlayYoke()
    {
        // 頻発する通常SEに埋もれやすいため、軛だけ専用枠で通常より3dB前へ出す。
        _yokeVoice.Stop();
        _yokeVoice.Stream = LoadSound(Yoke[0]);
        _yokeVoice.Play();
    }
    public void PlayDrought() => PlayVariation(Drought);
    public void PlayHushBlock() => PlayHushAccent(HushBlock);
    public void PlayHushBreak() => PlayHushAccent(HushBreak);
    public void PlayCharge(string unitId)
    {
        if (TryChargeSound(unitId, release: false, out string[] charge)) PlayChargeAccent(charge, 0.75);
    }

    public void PlayChargeRelease(string unitId)
    {
        if (TryChargeSound(unitId, release: true, out string[] charge)) PlayChargeAccent(charge, 0.55);
    }

    private static bool TryChargeSound(string unitId, bool release, out string[] sound)
    {
        sound = unitId switch
        {
            "chanter" => release ? MagicChargeRelease : MagicCharge,
            "archer" or "archer_g" => release ? PhysicalChargeRelease : PhysicalCharge,
            _ => null!,
        };
        return sound is not null;
    }
    public void PlayDirectReaction(string unitId)
    {
        if (unitId == "kado") PlayVariation(KadoCounter);
    }

    private void PlayVariation(string[] paths, float boostDb = 0)
    {
        string path = PickVariation(paths);
        // 攻撃・受け流しを合わせて最大4音。再生速度でピッチを変えない。
        int index = _nextVoice++ % _voices.Length;
        var voice = _voices[index];
        voice.Stop();
        voice.VolumeDb += boostDb - _voiceBoosts[index];
        _voiceBoosts[index] = boostDb;
        voice.Stream = LoadSound(path);
        voice.Play();
    }

    private void PlayChargeAccent(string[] paths, double duckSeconds)
    {
        var voice = _chargeVoices[_nextChargeVoice++ % _chargeVoices.Length];
        voice.Stop();
        voice.Stream = LoadSound(PickVariation(paths));
        voice.Play();

        // 予兆と解放の頭だけ通常SEを下げる。死亡・フィニッシュ音は勝敗の手掛かりなので下げない。
        _duckTween?.Kill();
        for (int i = 0; i < _voices.Length; i++)
            _voices[i].VolumeDb = NormalVolumeDb - 9 + _voiceBoosts[i];
        _duckTween = CreateTween();
        _duckTween.TweenInterval(duckSeconds);
        // 再生途中に音が替わっても、その音の音量差を保って戻す。
        _duckTween.TweenMethod(Callable.From<float>(level =>
        {
            for (int i = 0; i < _voices.Length; i++)
                _voices[i].VolumeDb = level + _voiceBoosts[i];
        }), NormalVolumeDb - 9, NormalVolumeDb, 0.18);
    }

    private void PlayHushAccent(string[] paths)
    {
        // 発動と解除を通常SEや死亡音に埋もれさせない。連続する保持者死亡にも2音まで対応する。
        var voice = _hushVoices[_nextHushVoice++ % _hushVoices.Length];
        voice.Stop();
        voice.Stream = LoadSound(PickVariation(paths));
        voice.Play();
    }

    private string PickVariation(string[] paths)
    {
        string key = string.Join('|', paths);
        int previous = _last.GetValueOrDefault(key, -1);
        int index = (int)_random.RandiRange(0, paths.Length - (previous >= 0 && paths.Length > 1 ? 2 : 1));
        if (paths.Length > 1 && previous >= 0 && index >= previous) index++;
        _last[key] = index;
        return paths[index];
    }

    public void PlayDeath(int team, bool finish = false)
    {
        // 攻撃音に直後の撃破音を切らせない。死亡音同士の重なりも2音まで。
        var voice = _deathVoices[_nextDeathVoice++ % _deathVoices.Length];
        voice.Stop();
        voice.VolumeDb = finish ? -2 : -5;
        voice.Stream = LoadSound(team == BattleContext.EnemyTeam ? (finish ? Finish : EnemyDeath) : PlayerDeath);
        voice.Play();
    }

    public void StopAll()
    {
        foreach (var voice in _voices) voice.Stop();
        foreach (var voice in _chargeVoices) voice.Stop();
        foreach (var voice in _deathVoices) voice.Stop();
        _yokeVoice.Stop();
        foreach (var voice in _hushVoices) voice.Stop();
        _duckTween?.Kill();
        _duckTween = null;
        foreach (var voice in _voices) voice.VolumeDb = NormalVolumeDb;
        System.Array.Clear(_voiceBoosts);
        _nextVoice = 0;
        _nextChargeVoice = 0;
        _nextDeathVoice = 0;
        _nextHushVoice = 0;
        _lastAttackUpAt = 0;
        _lastAttackDownAt = 0;
    }
}
