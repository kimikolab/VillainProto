using Godot;
using BattleCore;
using System.Collections.Generic;

// 戦闘計算とは独立した音選び。専用音の未登録キャラには共通音を使う。
public partial class BattleAttackAudio : Node
{
    // ヨミの追加攻撃を含む通常SEを旧設定から3 dB上げ、BGMに埋もれにくくする。
    private const float NormalVolumeDb = -13;

    private readonly RandomNumberGenerator _random = new();
    private readonly Dictionary<(string, AttackPattern), string[]> _overrides = new();
    private readonly Dictionary<string, AudioStream> _streams = new();
    private readonly Dictionary<string, int> _last = new();
    private readonly AudioStreamPlayer[] _voices = new AudioStreamPlayer[4];
    private int _nextVoice;
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
    private static readonly string[] KadoHit = { "res://assets/audio/se/kado_hit.mp3" };
    private static readonly string[] Heal = { "res://assets/audio/se/heal_magic_1.mp3" };
    private static readonly string[] TraitHeal = { "res://assets/audio/se/heal_trait.mp3" };
    private static readonly string[] KadoCounter = { "res://assets/audio/se/kado_counter.mp3" };
    private static readonly string[] MagicCharge = { "res://assets/audio/se/charge_magic.mp3" };
    private static readonly string[] MagicChargeRelease = { "res://assets/audio/se/charge_magic_release.mp3" };
    private static readonly string[] PhysicalCharge = { "res://assets/audio/se/charge_physical.mp3" };
    private static readonly string[] PhysicalChargeRelease = { "res://assets/audio/se/charge_physical_release.mp3" };
    private static readonly string[] Summon = { "res://assets/audio/se/summon.mp3" };
    private static readonly string[] Revive = { "res://assets/audio/se/revive.mp3" };
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
            _chargeVoices[i] = new AudioStreamPlayer { VolumeDb = -8, MaxPolyphony = 1 };
            AddChild(_chargeVoices[i]);
        }
        _yokeVoice = new AudioStreamPlayer { VolumeDb = -10, MaxPolyphony = 1 };
        AddChild(_yokeVoice);
        for (int i = 0; i < _hushVoices.Length; i++)
        {
            _hushVoices[i] = new AudioStreamPlayer { VolumeDb = -9, MaxPolyphony = 1 };
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
        LoadSound(KadoHit[0]);
        LoadSound(Heal[0]);
        LoadSound(TraitHeal[0]);
        LoadSound(KadoCounter[0]);
        LoadSound(MagicCharge[0]);
        LoadSound(MagicChargeRelease[0]);
        LoadSound(PhysicalCharge[0]);
        LoadSound(PhysicalChargeRelease[0]);
        LoadSound(Summon[0]);
        LoadSound(Revive[0]);
        LoadSound(Yoke[0]);
        LoadSound(Drought[0]);
        LoadSound(HushBlock[0]);
        foreach (string path in HushBreak) LoadSound(path);
        for (int i = 0; i < _deathVoices.Length; i++)
        {
            _deathVoices[i] = new AudioStreamPlayer { VolumeDb = -8, MaxPolyphony = 1 };
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
        if (reaction && unitId == "kado") { PlayVariation(KadoCounter); return; }
        string[] paths = _overrides.GetValueOrDefault((unitId, pattern))
            ?? (team == BattleContext.EnemyTeam ? EnemyCommon : Common);
        PlayVariation(paths);
    }

    public void PlayParry() => PlayVariation(ParrySounds);
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

    private void PlayVariation(string[] paths)
    {
        string path = PickVariation(paths);
        // 攻撃・受け流しを合わせて最大4音。再生速度でピッチを変えない。
        var voice = _voices[_nextVoice++ % _voices.Length];
        voice.Stop();
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
        foreach (var ordinary in _voices) ordinary.VolumeDb = NormalVolumeDb - 9;
        _duckTween = CreateTween();
        _duckTween.TweenInterval(duckSeconds);
        _duckTween.TweenProperty(_voices[0], "volume_db", NormalVolumeDb, 0.18);
        for (int i = 1; i < _voices.Length; i++)
            _duckTween.Parallel().TweenProperty(_voices[i], "volume_db", NormalVolumeDb, 0.18);
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
        voice.VolumeDb = finish ? -5 : -8;
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
        _nextVoice = 0;
        _nextChargeVoice = 0;
        _nextDeathVoice = 0;
        _nextHushVoice = 0;
        _lastAttackUpAt = 0;
        _lastAttackDownAt = 0;
    }
}
