using Godot;
using System;

public partial class BattlefieldView3D
{
    // 攻撃再生の接触口。再開時には破棄し、古い飛翔・鞭の予約を持ち越さない。
    internal Action<BattlePawn3D>? AttackContact;
    internal int FireHitPlays, FireHitNumberPlays, FireHitPulses;
    private void NotifyAttackContact(BattlePawn3D pawn) => AttackContact?.Invoke(pawn);

    internal void ShowFireHit(BattlePawn3D pawn, TurnTickBeat beat, double speed)
    {
        speed = Math.Max(0.1, speed);
        FireHitPlays++;
        FireHitPulses += beat.FirePulses;
        pawn.SetFireInvasive(beat.FireDamage);
        Color tint = FireFx.ColorOf(Math.Max(1, pawn.FireLevel));
        if (beat.FireDamage)
        {
            FireUltimateFx.Pillar(_fxRoot, pawn.GlobalPosition + Vector3.Up * 0.05f,
                1.3f + pawn.FireLevel * 0.70f, 0.65f + pawn.FireLevel * 0.36f, 0.38 / speed, _camera, false);
            FireFx.Bloom(_fxRoot, pawn.FxPoint, tint, 1.6f + pawn.FireLevel * 0.38f, 0.30 / speed, 1);
            _attackAudio.PlayFireSound("impact");
        }
        else
        {
            pawn.PulseFireGift();
            FireFx.Bloom(_fxRoot, pawn.FxPoint, tint, 2.6f, 0.35 / speed, 6);
            FireFx.Bloom(_fxRoot, pawn.FxPoint, new("fff4ce"), 1.9f, 0.13 / speed, 7);
            if (beat.FireHeal) _attackAudio.PlayFireSound("heal");
        }
        // 実在する刻みだけを小火として弾く。合計数字は一拍に一つずつ。
        for (int k = 0; k < beat.FirePulses; k++)
            FireFx.Bloom(_fxRoot, pawn.FxPoint + _camera.GlobalBasis.X * ((k % 3 - 1) * 0.23f)
                + Vector3.Up * (k % 2 * 0.18f), tint, 0.65f, 0.16 / speed, 7, delay: (float)(k * 0.045 / speed));
        foreach (var number in beat.Numbers)
        {
            TickNumber(pawn, number.Amount, number.Heal, 1, true, true, brittle: number.Brittle);
            FireHitNumberPlays++;
        }
    }
}
