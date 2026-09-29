using Godot;
using System;
using System.Threading.Tasks;

public partial class BattlePawn3D
{
    private Tween? _palmTween;
    private Vector3 _palmOffset;
    private TaskCompletionSource? _palmImpact;
    internal bool PalmStrikeActive => _palmTween is not null;
    internal Task PalmStrikeImpact => _palmImpact?.Task ?? Task.CompletedTask;
    // 1536x1024原画の中心から掌への座標。双掌（1410,425）／救援の片掌（1463,321）。
    private Vector2 PalmContactPixel => _movementPortrait == "hane_spring_guard" ? new(695, 191) : new(642, 87);
    // 低い構えの掌の高さ。相手の身長に合わせて足まで浮かせない。
    internal float PalmStrikeHeight(Vector3 cameraUp) => GlobalPosition.Y + _portraitBaseY + cameraUp.Y * PalmContactPixel.Y * _sprite.PixelSize;

    // 地面を蹴って両掌へ体重を伝える。名前・HP・席は動かさない。
    internal void BeginPalmStrike(Func<Vector3> impact, Vector3 right, Vector3 up, Action hit, Vector3? guarded = null,
        bool rescue = false)
    {
        if (_unitId != "hane" || !CanReceiveMovementImpact) return;
        ClearMovementPortrait();
        ShowMovementPortrait(rescue ? "hane_rescue" : guarded is null ? "hane_palm" : "hane_spring_guard", rescue ? 0.85 : 0.65);
        _palmImpact = new TaskCompletionSource();
        float sign = Team == 0 ? 1 : -1;
        double speed = Math.Max(0.1, AnimationSpeed);
        Vector3 windup = -right * sign * 0.20f;
        // 隣は短く、同列の遠い仲間へは全距離を跳ぶ。席は台本のMoveだけが変える。
        if (guarded is Vector3 ally)
            windup = (rescue ? ally - FxPoint : (ally - FxPoint).LimitLength(0.85f)) + Vector3.Up * (rescue ? 0.42f : 0.16f);
        if (rescue)
        {
            _movementFlip = windup.Dot(right) < 0;
            _sprite.FlipH = _movementFlip.Value;
            BeginBonusAfterimage(MovementFx.Bounce, 0.20 / speed, false);
        }
        Vector3 contact = Vector3.Zero;
        _palmTween = CreateTween();
        _palmTween.TweenMethod(Callable.From<float>(t => _palmOffset = windup * t
            + (rescue ? Vector3.Up * Mathf.Sin(t * Mathf.Pi) * 0.35f : Vector3.Zero)), 0f, 1f, (rescue ? 0.20 : 0.08) / speed);
        if (rescue) _palmTween.TweenCallback(Callable.From(() => {
            // 移動差分から掌へ。ShowMovementPortraitは掌のTweenを解除するためここでは使わない。
            _movementPortrait = "hane_spring_guard";
            _movementFlip = null;
            RefreshBattlePortrait();
        }));
        _palmTween.TweenMethod(Callable.From<float>(t => {
            // 自分への弾き返しと味方への救援で、実際に描かれた掌を接触点へ合わせる。
            float pixel = _sprite.PixelSize;
            Vector3 center = impact() - right * sign * PalmContactPixel.X * pixel - up * PalmContactPixel.Y * pixel;
            contact = center - (GlobalPosition + Vector3.Up * _portraitBaseY);
            _palmOffset = windup.Lerp(contact, t * t);
        }), 0f, 1f, 0.14 / speed);
        _palmTween.TweenCallback(Callable.From(() => {
            // 接触を通知するフレームにも両掌を命中点に描く。
            ProcessPalmStrikePose();
            if (CanReceiveMovementImpact) hit();
            _palmImpact?.TrySetResult();
        }));
        _palmTween.TweenInterval(0.08 / speed);
        _palmTween.TweenMethod(Callable.From<float>(t => _palmOffset = contact * (1 - Mathf.SmoothStep(0, 1, t))),
            0f, 1f, 0.20 / speed);
        _palmTween.TweenCallback(Callable.From(ClearMovementPortrait));
    }

    private void ProcessPalmStrikePose()
    {
        if (!PalmStrikeActive || _movementPortrait is not ("hane_palm" or "hane_spring_guard" or "hane_rescue")) return;
        _sprite.Position = new Vector3(0, _portraitBaseY, 0) + _palmOffset;
        _sprite.Rotation = Vector3.Zero;
        _sprite.Scale = Vector3.One;
    }

    private void ClearPalmStrikePose()
    {
        _palmTween?.Kill();
        _palmTween = null;
        _palmOffset = Vector3.Zero;
        var pending = _palmImpact;
        _palmImpact = null;
        pending?.TrySetResult();
    }
}
