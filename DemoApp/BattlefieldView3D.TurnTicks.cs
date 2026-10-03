using Godot;
using System;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int TurnTickBeatPlays, TurnTickFirePulses, TurnTickNumberPlays;

    // 数字は毒／火・回復／傷ごとに一つ。火勢の回数は、小さな火の弾け方だけで見せる。
    internal void ShowTurnTickBeat(BattlePawn3D? pawn, TurnTickBeat beat, double speed)
    {
        if (pawn is null) return;
        speed = Math.Max(0.1, speed);
        TurnTickBeatPlays++;
        double seconds = beat.Seconds / speed;
        if (beat.Poison) ShowTick(pawn, false, beat.InversePoison, false, seconds);
        if (beat.Burn)
        {
            ShowFireTick(pawn, beat.FireHeal, beat.FireDamage, seconds);
            _ = PlayTurnTickFirePulses(pawn, beat, seconds);
        }
        for (int i = 0; i < beat.Numbers.Count; i++)
        {
            var number = beat.Numbers[i];
            TickNumber(pawn, number.Amount, number.Heal, i, false, number.Burn, number.Brittle);
            TurnTickNumberPlays++;
            if (!number.Heal && number.Amount > 0) PlayStatusDamageSound(number.Burn ? "燃焼" : "毒");
        }
    }

    private async Task PlayTurnTickFirePulses(BattlePawn3D pawn, TurnTickBeat beat, double seconds)
    {
        int generation = _fireGeneration;
        for (int k = 0; k < beat.FirePulses; k++)
        {
            if (generation != _fireGeneration || !IsInstanceValid(pawn) || !pawn.IsInsideTree()) return;
            // オーラは体を焦がさず外側へ。焼かれる駒は体の内側で弾ける。
            var offset = _camera.GlobalBasis.X * ((k % 3 - 1) * (beat.FireDamage ? 0.27f : 0.55f))
                + Vector3.Up * (0.16f + k % 2 * 0.26f);
            FireFx.Bloom(_fxRoot, pawn.FxPoint + offset,
                beat.FireDamage ? new Color("ff8d45") : new Color("ffd998"),
                0.65f, seconds / Math.Max(2, beat.FirePulses), 1);
            TurnTickFirePulses++;
            if (k + 1 < beat.FirePulses)
                await ToSignal(GetTree().CreateTimer(Math.Max(0.001, seconds * 0.65 / beat.FirePulses)), SceneTreeTimer.SignalName.Timeout);
        }
    }
}
