using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private async Task FireSwordSlam(BattlePawn3D actor, BattlePawn3D[] hits, double speed)
    {
        int generation = _fireGeneration;
        int soundGeneration = _attackAudio.FireSoundGeneration;
        bool Live() => generation == _fireGeneration && soundGeneration == _attackAudio.FireSoundGeneration
            && IsInstanceValid(actor) && actor.IsInsideTree() && actor.Hp > 0;
        async Task Wait(double s) => await ToSignal(GetTree().CreateTimer(Math.Max(0.001, s / speed)), SceneTreeTimer.SignalName.Timeout);
        Vector3 center = hits.Select(p => p.GlobalPosition).Aggregate(Vector3.Zero, (a, b) => a + b) / hits.Length;
        Vector3 ground = center + Vector3.Up * 0.06f;
        actor.ShowMovementPortrait("hota_sword_raise", 1.05);
        FireShade(actor, 1.65 / speed);
        _attackAudio.PlayFireSound("raise", speed, 0.70);
        FireFx.Bloom(_fxRoot, actor.FxPoint, new("8be5ff"), 4.0f, 0.72 / speed, 3);
        FireFx.Bloom(_fxRoot, actor.GlobalPosition + Vector3.Up * 0.08f, new("89ddff"), 4.6f, 0.8 / speed, 2, true);
        var sword = FireUltimateFx.Sword(_fxRoot);
        Vector3 Hilt() => actor.MovementPortraitPoint(new(606, 350), _camera);
        Vector3 raised = Hilt();
        // 刃を出した瞬間に完成形を見せず、剣先へ光が積み上がる。
        var gather = sword.CreateTween();
        gather.TweenMethod(Callable.From<float>(p => {
            Vector3 hilt = Hilt();
            FireUltimateFx.SwordPose(sword, hilt, hilt + Vector3.Up * (0.3f + p * 4.1f),
                _camera.GlobalBasis.Z, 0.55f + p * 1.2f, p);
        }), 0f, 1f, 0.68 / speed).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        for (int k = 0; k < 8; k++)
        {
            float a = k * Mathf.Tau / 8;
            FireFx.Ribbon(_fxRoot, raised + new Vector3(Mathf.Cos(a) * 2.2f, -0.8f, Mathf.Sin(a) * 1.5f),
                raised + Vector3.Up * 1.2f, new("ffda80"), 0.065f, 0.8f, 0.65 / speed, _camera.GlobalBasis.Z);
        }
        await Wait(0.78);
        if (!Live()) { if (IsInstanceValid(sword)) sword.QueueFree(); return; }
        raised = Hilt();
        actor.ShowMovementPortrait("hota_sword_slam", 1.35);
        Vector3 lowered = actor.MovementPortraitPoint(new(595, 905), _camera);
        Vector3 endDirection = (ground - lowered).Normalized();
        float endLength = lowered.DistanceTo(ground);
        var slam = sword.CreateTween();
        slam.TweenMethod(Callable.From<float>(p => {
            float swing = p * p;
            Vector3 hilt = raised.Lerp(lowered, swing);
            Vector3 direction = Vector3.Up.Slerp(endDirection, swing);
            FireUltimateFx.SwordPose(sword, hilt, hilt + direction * Mathf.Lerp(4.4f, endLength, swing),
                _camera.GlobalBasis.Z, 1.75f, 1 + p);
        }), 0f, 1f, 0.17 / speed);
        // 高い弧を描く細い残像で振り下ろし方向を読む。
        FireFx.Ribbon(_fxRoot, raised + Vector3.Up * 4.4f, ground, new("ffe39b"), 0.16f, 1.8f,
            0.23 / speed, _camera.GlobalBasis.Z);
        await Wait(0.17);
        if (!Live()) { if (IsInstanceValid(sword)) sword.QueueFree(); return; }
        FireUltimateFx.FadeSword(sword, 0.18 / speed);
        _attackAudio.PlayFireSound("burnout", speed);
        FireImpactCamera(center, speed, 1.0f);
        // 生存している標的の広がりに合わせ、前列から後列まで一つの噴火に包む。
        float span = hits.Max(p => p.GlobalPosition.Dot(_camera.GlobalBasis.X))
            - hits.Min(p => p.GlobalPosition.Dot(_camera.GlobalBasis.X));
        float infernoWidth = Math.Max(9.5f, span + 5.4f);
        FireUltimateFx.Inferno(_fxRoot, ground, infernoWidth, 8.5f, 1.20 / speed, _camera);
        FireUltimateFx.Pillar(_fxRoot, ground, 10.0f, 7.8f, 1.15 / speed, _camera, true);
        FireUltimateFx.Sparks(_fxRoot, ground + Vector3.Up * 0.15f, _camera, 0.9 / speed);
        FireFx.Bloom(_fxRoot, ground + Vector3.Up * 0.5f, new("fff2c2"), 5.0f, 0.22 / speed, 7);
        for (int ring = 0; ring < 3; ring++)
            FireFx.Bloom(_fxRoot, ground + Vector3.Up * ring * 0.025f, new("ffc35b"), infernoWidth + ring * 2.0f,
                (0.55 + ring * 0.12) / speed, 2, true, (float)(ring * 0.035 / speed));
        foreach (var hit in hits)
        {
            FireUltimateFx.Pillar(_fxRoot, hit.GlobalPosition + Vector3.Up * 0.05f, 7.0f, 4.0f, 0.85 / speed, _camera, false);
            FireFx.Bloom(_fxRoot, hit.FxPoint, new("ffac42"), 5.3f, 0.50 / speed);
            FireFx.Light(_fxRoot, hit.FxPoint, new("ffc66d"), 3.4f, 0.48 / speed);
            NotifyAttackContact(hit);
        }
        // HPの反映はこの直後。火柱が立ち切る前にダメージを出す。
    }

    private async Task FireGreatCleave(BattlePawn3D actor, BattlePawn3D[] hits, double speed)
    {
        int generation = _fireGeneration;
        int soundGeneration = _attackAudio.FireSoundGeneration;
        bool Live() => generation == _fireGeneration && soundGeneration == _attackAudio.FireSoundGeneration
            && IsInstanceValid(actor) && actor.IsInsideTree() && actor.Hp > 0;
        async Task Wait(double s) => await ToSignal(GetTree().CreateTimer(Math.Max(0.001, s / speed)), SceneTreeTimer.SignalName.Timeout);
        Vector3 center = hits.Select(p => p.FxPoint).Aggregate(Vector3.Zero, (a, b) => a + b) / hits.Length;
        actor.ShowMovementPortrait("borg_guard", 0.8);
        actor.FireBrace(center, false);
        FireShade(actor, 1.12 / speed);
        _attackAudio.PlayFireSound("charge", speed, 0.40);
        FireFx.Bloom(_fxRoot, actor.FxPoint, new("ff9b3b"), 4.1f, 0.45 / speed, 3);
        FireFx.Bloom(_fxRoot, actor.GlobalPosition + Vector3.Up * 0.06f, new("ffb446"), 3.4f, 0.45 / speed, 2, true);
        await Wait(0.43);
        if (!Live()) return;
        actor.ShowMovementPortrait("borg_release", 0.7);
        actor.FireBrace(center, true);
        await Wait(0.09);
        if (!Live()) return;
        FireUltimateFx.Crescent(_fxRoot, actor.FxPoint, center, _camera, 0.52 / speed, 3.2f);
        _attackAudio.PlayFireSound("release");
        await Wait(0.17);
        if (!Live()) return;
        FireImpactCamera(center, speed, 0.8f);
        FireUltimateFx.Sparks(_fxRoot, center - Vector3.Up * 0.7f, _camera, 0.65 / speed);
        foreach (var hit in hits)
        {
            FireFx.Bloom(_fxRoot, hit.FxPoint, new("ffbd58"), 3.5f, 0.42 / speed);
            FireFx.Bloom(_fxRoot, hit.FxPoint, new("fff0bf"), 4.1f, 0.20 / speed, 7);
            FireFx.Bloom(_fxRoot, hit.GlobalPosition + Vector3.Up * 0.08f, new("ffab3c"), 4.3f, 0.5 / speed, 2, true);
            FireFx.Light(_fxRoot, hit.FxPoint, new("ffb550"), 2.5f, 0.35 / speed);
            NotifyAttackContact(hit);
        }
        actor.ReturnFromAttack();
    }

    private void FireImpactCamera(Vector3 focus, double speed, float strength)
    {
        _cameraMotion?.Kill();
        Vector3 punch = _cameraHome + (focus - Vector3.Zero) * 0.016f + new Vector3(0, -0.24f, -0.40f) * strength;
        var tween = _cameraMotion = _camera.CreateTween();
        tween.TweenProperty(_camera, "position", punch, 0.035 / speed);
        tween.Parallel().TweenProperty(_camera, "fov", CameraFov - 3.2f * strength, 0.035 / speed);
        tween.TweenInterval(0.065 / speed); // 衝撃の構図を短く止める。
        tween.TweenProperty(_camera, "position", _cameraHome, 0.28 / speed).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(_camera, "fov", CameraFov, 0.28 / speed);
    }
}
