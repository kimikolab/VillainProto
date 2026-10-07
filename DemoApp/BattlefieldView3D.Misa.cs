using BattleCore;
using Godot;
using System;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int MisaShots, MisaSprays, MisaFlows;
    private static readonly Color MisaLight = new("b8acff");

    internal async Task BeginMisaVolley(BattlePawn3D? actor, int count, double speed)
    {
        if (actor?.MisaFeathers is not { } feathers) return;
        Vector3 left = PawnPosition(0, 3), right = PawnPosition(1, 4);
        feathers.SetField((left + right) * 0.5f,
            new Vector2((right.X - left.X) * 0.5f + 0.35f, (right.Z - left.Z) * 0.5f + 0.70f));
        feathers.BeginVolley(count);
        ShowFeatherVolley(actor, speed);
        await ToSignal(GetTree().CreateTimer(MisaFeathers3D.DeploySeconds / Math.Max(0.1, speed)), SceneTreeTimer.SignalName.Timeout);
    }

    internal async Task PlayMisaShot(BattlePawn3D? actor, BattlePawn3D? target, BattleEvent cue, double speed,
        bool willLose, bool lastShot = false)
    {
        if (actor?.MisaFeathers is not { } feathers || target is null || actor.Hp <= 0) return;
        int generation = _specialGeneration;
        speed = Math.Max(0.1, speed);
        bool spray = cue.Text == FeatherLabels.Spray;
        float dx = (target.FxPoint - actor.FxPoint).Dot(_camera.GlobalBasis.X);
        actor.ShowMovementPortrait(spray ? "tome_spray" : "tome_control", 0.75,
            flip: Math.Abs(dx) < 0.01f ? actor.Team != 0 : dx < 0);
        if (!spray) feathers.AimAt(target.FxPoint);
        if (!feathers.Launch(cue.Slot, target.FxPoint, spray, willLose)) return;
        MisaShots++;
        if (spray) MisaSprays++;
        if (cue.Text == FeatherLabels.Flow) MisaFlows++;
        // 羽は戦場に散開した射撃位置に留まり、その先端から光線を撃つ。
        if (cue.Text == FeatherLabels.Flow)
            ShockMarkFx.Ring(_fxRoot, target.FxPoint, MisaLight, 1.6f, 0.32 / speed);
        if (spray && cue.StatusRemaining == 1)
        {
            ShockMarkFx.Sparks(_fxRoot, actor.FxPoint, MisaLight, 18, 1.8f, 0.45 / speed);
            _attackAudio.PlayShockMark(ShockMarkSound.Spray);
        }
        // 次の羽を照準させる間も、先に撃った光線と着弾光は残す。
        await ToSignal(GetTree().CreateTimer(MisaFeathers3D.ShotIntervalSeconds / speed), SceneTreeTimer.SignalName.Timeout);
        if (generation != _specialGeneration || !IsVisibleInTree() || !IsInstanceValid(actor) || !IsInstanceValid(target)
            || !actor.IsInsideTree() || !target.IsInsideTree() || actor.Hp <= 0) return;
        if (feathers.Fire(cue.Slot) is not { } muzzle) return;
        ShockMarkFx.Glow(_fxRoot, muzzle, MisaLight, 0.65f, 0.25 / speed);
        ShockMarkFx.Ring(_fxRoot, muzzle, MisaLight, 0.65f, 0.30 / speed);
        ShockMarkFx.Beam(_fxRoot, muzzle, target.FxPoint, MisaLight, spray ? 0.075f : 0.095f, 0.34 / speed);
        ShockMarkFx.Beam(_fxRoot, muzzle, target.FxPoint, new Color(MisaLight, 0.32f), 0.023f, 0.62 / speed);
        ShockMarkFx.Glow(_fxRoot, target.FxPoint, MisaLight, 1.0f, 0.27 / speed);
        ShockMarkFx.Sparks(_fxRoot, target.FxPoint, MisaLight, 11, 0.85f, 0.35 / speed);
        MakeGroundRing(target.Home, MisaLight, 0.75f, 0.28 / speed);
        _attackAudio.PlayShockMark(ShockMarkSound.Beam, pan: Math.Clamp(dx / 10f, -0.7f, 0.7f));
        if (cue.Slot == 1 || cue.Slot == cue.Amount) CameraPunch(target.FxPoint, AttackPattern.Single);
        if (lastShot)
        {
            // 最後だけ余韻を置く。敵全滅などで予定枚数より早く終わる一振りも帰還させる。
            await ToSignal(GetTree().CreateTimer(0.18 / speed), SceneTreeTimer.SignalName.Timeout);
            if (generation == _specialGeneration && IsInstanceValid(actor) && actor.IsInsideTree() && actor.Hp > 0)
                feathers.ReturnVolley();
        }
    }
}
