using BattleCore;
using Godot;
using System;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int MisaShots, MisaSprays, MisaFlows;
    private static readonly Color MisaLight = new("b8acff");

    internal async Task PlayMisaShot(BattlePawn3D? actor, BattlePawn3D? target, BattleEvent cue, double speed,
        bool willLose)
    {
        if (actor?.MisaFeathers is not { } feathers || target is null || actor.Hp <= 0) return;
        int generation = _specialGeneration;
        speed = Math.Max(0.1, speed);
        bool spray = cue.Text == FeatherLabels.Spray;
        float dx = (target.FxPoint - actor.FxPoint).Dot(_camera.GlobalBasis.X);
        actor.ShowMovementPortrait("tome_attack", 0.62, flip: Math.Abs(dx) < 0.01f ? actor.Team != 0 : dx < 0);
        if (!spray) feathers.AimAt(target.FxPoint);
        if (!feathers.Launch(cue.Slot, target.FxPoint, spray, willLose)) return;
        MisaShots++;
        if (spray) MisaSprays++;
        if (cue.Text == FeatherLabels.Flow) MisaFlows++;
        // 通常の斬撃とは別に、実際の羽を1枚だけ飛ばす。HP更新は後続のDamageに任せる。
        _attackAudio.PlayAttack(actor.UnitId, actor.Team, AttackPattern.Single, false, false);
        await ToSignal(GetTree().CreateTimer(MisaFeathers3D.TravelSeconds / speed), SceneTreeTimer.SignalName.Timeout);
        if (generation != _specialGeneration || !IsVisibleInTree() || !IsInstanceValid(actor) || !IsInstanceValid(target)
            || !actor.IsInsideTree() || !target.IsInsideTree() || actor.Hp <= 0) return;
        MakeGroundRing(target.Home, spray ? UiKit.Violet : MisaLight, 0.40f, 0.20 / speed);
        SpecialsFx.Travel(_fxRoot, target.FxPoint, target.FxPoint + Vector3.Up * 0.12f,
            MisaLight, 0.16 / speed, size: 0.45f);
    }
}
