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
    // 低い構えの掌の高さ。相手の身長に合わせて足まで浮かせない。
    internal float PalmStrikeHeight(Vector3 cameraUp) => GlobalPosition.Y + _portraitBaseY + cameraUp.Y * 87 * _sprite.PixelSize;

    // 地面を蹴って両掌へ体重を伝える。名前・HP・席は動かさない。
    internal void BeginPalmStrike(Func<Vector3> impact, Vector3 right, Vector3 up, Action hit)
    {
        if (_unitId != "hane" || !CanReceiveMovementImpact) return;
        ClearMovementPortrait();
        ShowMovementPortrait("hane_palm", 0.65);
        _palmImpact = new TaskCompletionSource();
        float sign = Team == 0 ? 1 : -1;
        double speed = Math.Max(0.1, AnimationSpeed);
        Vector3 windup = -right * sign * 0.20f;
        Vector3 contact = Vector3.Zero;
        _palmTween = CreateTween();
        _palmTween.TweenMethod(Callable.From<float>(t => _palmOffset = windup * t), 0f, 1f, 0.08 / speed);
        _palmTween.TweenMethod(Callable.From<float>(t => {
            // 1536x1024の差分: 両掌の平均接点（1410,425）を相手の胴へ合わせる。
            float pixel = _sprite.PixelSize;
            Vector3 center = impact() - right * sign * 642 * pixel - up * 87 * pixel;
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
        if (!PalmStrikeActive || _movementPortrait != "hane_palm") return;
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
