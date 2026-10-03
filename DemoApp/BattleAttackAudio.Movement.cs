using Godot;
using System.Collections.Generic;

internal enum MovementSound { Bow, Vine, Wind, Collision, Landing, Sheathe, ArrowHit, Windup, Tornado, BasaSweep, Tailwind, Evade, SeroPierce, SeroBarrage, Dropkick, SpringBlock, SpringKick, AllyBump }

public partial class BattleAttackAudio
{
    internal static readonly Dictionary<MovementSound, string> MovementPaths = new() {
        [MovementSound.Bow] = "res://assets/audio/se/sero_bow.mp3",
        [MovementSound.Vine] = "res://assets/audio/se/shio_vine_pull.mp3",
        [MovementSound.Wind] = "res://assets/audio/se/basa_wind.mp3",
        [MovementSound.Collision] = "res://assets/audio/se/hane_smash_ice.mp3",
        [MovementSound.Landing] = "res://assets/audio/se/movement_land.mp3",
        [MovementSound.Sheathe] = "res://assets/audio/se/yomi_sheathe.mp3",
        [MovementSound.ArrowHit] = "res://assets/audio/se/sero_arrow_hit.mp3",
        [MovementSound.Windup] = "res://assets/audio/se/hane_windup.mp3",
        [MovementSound.Tornado] = "res://assets/audio/se/basa_tornado.mp3",
        [MovementSound.BasaSweep] = "res://assets/audio/se/basa_sweep.mp3",
        [MovementSound.Tailwind] = "res://assets/audio/se/basa_tailwind.mp3",
        [MovementSound.Evade] = "res://assets/audio/se/sero_evade.mp3",
        [MovementSound.SeroPierce] = "res://assets/audio/se/sero_pierce.wav",
        [MovementSound.SeroBarrage] = "res://assets/audio/se/sero_barrage.wav",
        [MovementSound.Dropkick] = "res://assets/audio/se/hane_dropkick.mp3",
        [MovementSound.SpringBlock] = "res://assets/audio/se/hane_spring_block.mp3",
        [MovementSound.SpringKick] = "res://assets/audio/se/hane_spring_kick.mp3",
        [MovementSound.AllyBump] = "res://assets/audio/se/hane_ally_bump.mp3",
    };
    private readonly Dictionary<MovementSound, ulong> _movementSoundTimes = new();
    internal readonly Dictionary<MovementSound, int> MovementSoundPlays = new();
    internal int MovementSoundGeneration { get; private set; }

    internal void PlayMovementSound(MovementSound sound, bool secondary = false)
    {
        // 発射は毎回鳴らす。風・蔓・巻き込み・着地の密集だけを実時間で間引く。
        ulong interval = sound switch {
            MovementSound.Vine => 70, MovementSound.Wind or MovementSound.Tailwind => 140,
            MovementSound.Collision when secondary => 65, MovementSound.Landing when secondary => 80, _ => 0,
        };
        ulong now = Time.GetTicksMsec();
        if (interval > 0 && _movementSoundTimes.TryGetValue(sound, out ulong last) && now - last < interval) return;
        _movementSoundTimes[sound] = now;
        MovementSoundPlays[sound] = MovementSoundPlays.GetValueOrDefault(sound) + 1;
        float boost = sound switch {
            MovementSound.Wind => -5, MovementSound.Vine => -2,
            MovementSound.Tornado => -5, MovementSound.BasaSweep => -2, MovementSound.Tailwind => -4,
            MovementSound.Evade => -2,
            MovementSound.SeroPierce => 0, MovementSound.SeroBarrage => -4, MovementSound.Dropkick => 0,
            MovementSound.SpringBlock or MovementSound.SpringKick => -2,
            MovementSound.Collision => secondary ? -6 : 1,
            MovementSound.Sheathe => -3, MovementSound.ArrowHit => -2, MovementSound.Windup => -3,
            MovementSound.Landing => secondary ? -7 : -4, _ => 0,
        };
        // 通常SEの4音枠と溜め中の音量抑制を共用。倍速でもピッチは原音のまま。
        PlayVariation(new[] { MovementPaths[sound] }, boost);
    }

    private void ResetMovementSounds()
    {
        MovementSoundGeneration++;
        _movementSoundTimes.Clear();
        MovementSoundPlays.Clear();
    }
}
