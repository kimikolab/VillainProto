using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int BasaStormPlays { get; private set; }
    internal int TailwindGustPlays { get; private set; }
    internal int BasaSweepPlays { get; private set; }

    internal async Task ShowBasaShuffle(BattlePawn3D actor, IReadOnlyList<BattleEvent> moves, double speed)
    {
        if (!actor.CanBasaFly || actor.Hp <= 0) return;
        int generation = _specialGeneration;
        int audioGeneration = _attackAudio.MovementSoundGeneration;
        double s = Math.Max(0.1, speed);
        actor.AnimationSpeed = s;
        actor.BeginBasaFlight();
        await ToSignal(GetTree().CreateTimer(Math.Max(0.005, 0.24 / s)), SceneTreeTimer.SignalName.Timeout);
        if (generation != _specialGeneration || audioGeneration != _attackAudio.MovementSoundGeneration
            || !IsInstanceValid(actor) || !actor.IsInsideTree() || !actor.CanBasaFly || actor.Hp <= 0) return;
        BasaStormPlays++;
        actor.ShowMovementPortrait("basa_flap", 0.76);
        _attackAudio.PlayMovementSound(MovementSound.Tornado);
        int Stage() => Math.Clamp(_windStages.GetValueOrDefault(actor.InstanceId), 0, 3);
        // 実際に動く陣営だけ。片側が動けない場合に架空の入れ替えは見せない。
        foreach (int team in moves.Select(e => FindPawn(e.TargetId)).OfType<BattlePawn3D>().Select(p => p.Team).Distinct())
        {
            Vector3 center = PawnPosition(team, 2) + Vector3.Up * 0.08f;
            MovementFx.Gust(_fxRoot, _camera, actor.FxPoint + Vector3.Up * 0.85f,
                center + Vector3.Up, 0.44 / s, 0.8f);
            for (int i = 0; i < 2; i++)
                MovementFx.Tornado(_fxRoot, _camera, center, Stage, i,
                    (1.35 + moves.Count * 0.10) / s);
        }
        await ToSignal(GetTree().CreateTimer(Math.Max(0.005, 0.12 / s)), SceneTreeTimer.SignalName.Timeout);
    }

    private void ShowTailwindGust(BattlePawn3D? actor, BattlePawn3D? target, BattlePawn3D? partner, double speed)
    {
        if (target is null) return;
        TailwindGustPlays++;
        _attackAudio.PlayMovementSound(MovementSound.Tailwind);
        Vector3 direction = partner is not null ? partner.Home - target.Home : Vector3.Right * (target.Team == 0 ? 1 : -1);
        direction.Y = 0;
        if (direction.LengthSquared() < 0.01f) direction = Vector3.Right * (target.Team == 0 ? 1 : -1);
        direction = direction.Normalized();
        Vector3 from = target.Home + Vector3.Up * 0.85f - direction * 1.9f;
        Vector3 to = (partner?.Home ?? target.Home + direction * 2) + Vector3.Up * 0.85f + direction * 1.4f;
        float strength = 1 + Math.Clamp(_windStages.GetValueOrDefault(actor?.InstanceId ?? -1), 0, 3) * 0.16f;
        MovementFx.Gust(_fxRoot, _camera, from, to, 0.78 / speed, strength * 1.2f);
        MovementFx.TailwindLane(_fxRoot, _camera, target, partner?.Home ?? target.Home + direction * 2, 0.82 / speed);
        target.BeginBonusAfterimage(new Color("a4ffe1"), 0.52 / speed, false, 0.22f);
        var label = Float(target, "追い風", new Color("a4ffe1"), true, 3.1f);
        if (label is not null)
            label.CreateTween().TweenMethod(Callable.From<float>(_ => {
                if (IsInstanceValid(target) && target.IsInsideTree())
                    label.Position = new Vector3(target.GlobalPosition.X, label.Position.Y, target.GlobalPosition.Z);
            }), 0f, 1f, 0.82 / speed);
        target.MovementPose(-0.24f, 0.34f);
    }

    private async Task ShowBasaSweep(BattlePawn3D actor, IReadOnlyList<BattlePawn3D> hits, double speed)
    {
        BasaSweepPlays++;
        int generation = _specialGeneration;
        actor.ShowMovementPortrait("basa_flap", 0.56);
        actor.MovementPose(-0.22f, 0.22f);
        var points = hits.Select(p => p.FxPoint).ToArray();
        MovementFx.WindSlam(_fxRoot, _camera, actor.FxPoint, points, 0.48 / speed);
        await ToSignal(GetTree().CreateTimer(Math.Max(0.005, 0.24 / speed)), SceneTreeTimer.SignalName.Timeout);
        if (generation != _specialGeneration || !IsInstanceValid(actor) || !actor.IsInsideTree()) return;
        foreach (var hit in hits.Where(p => IsInstanceValid(p) && p.IsInsideTree() && p.Hp > 0))
        {
            MovementFx.WindBurst(_fxRoot, _camera, hit.FxPoint, 0.30 / speed);
            NotifyAttackContact(hit);
            MakeGroundRing(hit.Home, new Color("d0ffed"), 0.8f, 0.30 / speed);
        }
        if (points.Length > 0) CameraPunch(points[0], AttackPattern.Sweep);
    }
}
