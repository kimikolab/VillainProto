using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private async Task FirePierce(BattlePawn3D actor, BattlePawn3D[] hits, int level, double speed)
    {
        int generation = _fireGeneration;
        int soundGeneration = _attackAudio.FireSoundGeneration;
        bool Live() => generation == _fireGeneration && soundGeneration == _attackAudio.FireSoundGeneration
            && IsInstanceValid(actor) && actor.IsInsideTree() && actor.Hp > 0;
        async Task Wait(double s) => await ToSignal(GetTree().CreateTimer(Math.Max(0.001, s / speed)), SceneTreeTimer.SignalName.Timeout);
        bool grand = level == 3;
        double charge = grand ? 0.48 : 0.12;
        var color = new Color(grand ? "ffd044" : "ff6320");
        actor.ShowMovementPortrait("hota_charge", charge + 0.10);
        FireFx.Bloom(_fxRoot, actor.FxPoint, color, grand ? 4.2f : 2.0f, charge / speed, 3);
        if (grand)
        {
            _attackAudio.PlayFireSound("charge", speed, 0.44);
            FireShade(actor, 1.0 / speed);
            FireFx.Bloom(_fxRoot, actor.GlobalPosition + Vector3.Up * 0.08f, color, 3.8f, charge / speed, 2, true);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.Tau / 6;
                FireFx.Ribbon(_fxRoot, actor.FxPoint + _camera.GlobalBasis.X * Mathf.Cos(a) * 2
                    + _camera.GlobalBasis.Y * Mathf.Sin(a) * 2, actor.FxPoint, color, 0.10f, 0.15f, charge / speed, _camera.GlobalBasis.Z);
            }
        }
        await Wait(charge);
        if (!Live()) return;
        actor.ShowMovementPortrait("hota_release", grand ? 0.8 : 0.5);
        var far = hits.OrderByDescending(p => p.FxPoint.DistanceSquaredTo(actor.FxPoint)).First();
        Vector3 from = actor.FxPoint;
        Vector3 direction = (far.FxPoint - from).Normalized();
        Vector3 end = far.FxPoint + direction * (grand ? 0.9f : 0.45f);
        FireUltimateFx.Lance(_fxRoot, from, end, _camera, grand, (grand ? 0.60 : 0.33) / speed);
        FireFx.Bloom(_fxRoot, from, color, grand ? 4.0f : 2.1f, 0.24 / speed, 7);
        _attackAudio.PlayFireSound(grand ? "jet" : "projectile", speed, grand ? 0.65 : 0.34);
        await Wait(grand ? 0.17 : 0.20);
        if (!Live()) return;
        if (grand) FireImpactCamera(far.GlobalPosition, speed, 0.48f);
        foreach (var hit in hits)
        {
            if (!IsInstanceValid(hit)) continue;
            FireFx.Bloom(_fxRoot, hit.FxPoint, color, grand ? 3.8f : 2.5f, (grand ? 0.48 : 0.3) / speed);
            FireFx.Bloom(_fxRoot, hit.GlobalPosition + Vector3.Up * 0.08f, color, grand ? 4.0f : 2.5f, 0.4 / speed, 2, true);
            if (grand)
            {
                FireUltimateFx.Pillar(_fxRoot, hit.GlobalPosition + Vector3.Up * 0.05f, 3.2f, 2.1f, 0.38 / speed, _camera, false);
                FireFx.Light(_fxRoot, hit.FxPoint, color, 1.8f, 0.35 / speed);
            }
        }
    }
}
