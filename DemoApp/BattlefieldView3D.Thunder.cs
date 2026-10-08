using Godot;
using System;

public partial class BattlefieldView3D
{
    internal int ThunderPlays, DischargePlays, ShockStagePlays, ShockLeakPlays, ShockInversePlays;

    public void LaunchThunderStake(BattlePawn3D? actor, BattlePawn3D? target, double speed)
    {
        if (actor is null || target is null) return;
        if (actor.UnitId == "kata") { actor.LaunchKataStake(target.FxPoint, speed); return; }
        var stake = LiliFx.Sprite(_fxRoot, actor.FxPoint + Vector3.Up * 0.6f, ThunderFx.Stake, 0.57f);
        var tween = stake.CreateTween();
        tween.TweenProperty(stake, "position", _fxRoot.ToLocal(target.FxPoint), 0.18 / speed);
        tween.TweenInterval(0.38 / speed);
        tween.TweenProperty(stake, "modulate:a", 0f, 0.18 / speed);
        tween.TweenCallback(Callable.From(stake.QueueFree));
    }

    public void StrikeThunder(BattlePawn3D? previous, BattlePawn3D? target, int hop, int kinds, double speed, BattlePawn3D? actor = null)
    {
        if (target is null) return;
        ThunderPlays++;
        float power = Math.Clamp(kinds, 0, 4);
        int cloud = actor?.Thundercloud ?? 0;
        actor?.ShowMovementPortrait("kata_thunder", 0.8);
        Color tint = ThunderFx.Cyan;
        if (target.HasStatusIcon(BattleCore.StatusKeys.Poison)) tint = tint.Lerp(new Color("8ce9a1"), 0.35f);
        if (target.HasStatusIcon(BattleCore.StatusKeys.Burn)) tint = tint.Lerp(new Color("ffbb77"), 0.30f);
        var to = target.FxPoint;
        ThunderFx.Arc(_fxRoot, previous?.FxPoint ?? to + Vector3.Up * 5.5f, to,
            0.022f + power * 0.014f + cloud * 0.019f, 0.36 / speed, color: tint);
        if (cloud > 0)
        {
            ShockMarkFx.Glow(_fxRoot, to, tint, 1.2f + cloud * 0.17f, 0.36 / speed);
            MakeGroundRing(target.Home, tint, 0.7f + cloud * 0.12f, 0.38 / speed);
            for (int i = 0; i < 2 + cloud / 2; i++)
            {
                var origin = to + Vector3.Up * (1.4f + i * 0.5f);
                ThunderFx.Arc(_fxRoot, origin, origin + _camera.GlobalBasis.X * (i % 2 == 0 ? -1 : 1) * (0.45f + cloud * 0.10f)
                    - Vector3.Up * 0.65f, 0.025f, 0.26 / speed, color: tint);
            }
            if (hop == 1)
            {
                CameraPunch(to, cloud >= 6 ? BattleCore.AttackPattern.All : BattleCore.AttackPattern.Sweep);
                _attackAudio.PlayShockMark(ShockMarkSound.ThunderHeavy, speed: speed);
            }
        }
        ThunderFx.Burst(_fxRoot, to, 0.45f + power * 0.15f + cloud * 0.08f, 0.26 / speed);
        if (cloud == 0 || hop != 1) _attackAudio.PlayElectric(hop == 1, hop - 1, kinds);
    }

    public void ShockStage(int depth)
    {
        ShockStagePlays++;
        _attackAudio.PlayElectric(false, depth, Math.Min(depth, 3));
    }

    public void ShowDischarge(BattlePawn3D? from, BattlePawn3D? to, double speed, bool leak = false)
    {
        if (from is null || to is null) return;
        if (leak) ShockLeakPlays++; else DischargePlays++;
        ThunderFx.Arc(_fxRoot, from.DischargePoint, to.DischargePoint, leak ? 0.018f : 0.035f, 0.24 / speed);
        ThunderFx.Burst(_fxRoot, to.FxPoint, leak ? 0.32f : 0.55f, 0.22 / speed);
    }

    public void ShowShockInverse(BattlePawn3D? target, double speed)
    {
        ShockInversePlays++;
        // 電弧の着弾後、既存の反転（紅から緑白金）へ切り替える。
        ShowTick(target, false, true, false, 0.34 / speed, electric: true);
    }
}
