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

    public void StrikeThunder(BattlePawn3D? previous, BattlePawn3D? target, int hop, int kinds, double speed)
    {
        if (target is null) return;
        ThunderPlays++;
        float power = Math.Clamp(kinds, 0, 4);
        var to = target.FxPoint;
        ThunderFx.Arc(_fxRoot, previous?.FxPoint ?? to + Vector3.Up * 5.5f, to,
            0.022f + power * 0.014f, 0.30 / speed);
        ThunderFx.Burst(_fxRoot, to, 0.45f + power * 0.15f, 0.26 / speed);
        _attackAudio.PlayElectric(hop == 1, hop - 1, kinds);
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
