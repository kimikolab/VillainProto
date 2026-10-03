using Godot;
using System;
using System.Threading.Tasks;

public partial class BattlePawn3D
{
    private Tween? _dropkickTween;
    private Vector3 _dropkickOffset;
    private TaskCompletionSource? _dropkickImpact;
    internal bool DropkickActive => _dropkickTween is not null;
    internal bool CanReceiveMovementImpact => _alive && !_victory && Hp > 0;
    internal Task DropkickImpact => _dropkickImpact?.Task ?? Task.CompletedTask;
    internal Task SpringImpact => DropkickActive ? DropkickImpact : PalmStrikeImpact;

    // 席・HP・名前の位置は動かさず、本体だけを跳ばす。両足の先端を命中点へ合わせる。
    internal void BeginDropkick(Func<Vector3> impact, Vector3 screenRight, Vector3 screenUp, Action hit, bool springKick = false)
    {
        if (_unitId != "hane" || !_alive || _victory) return;
        ClearMovementPortrait();
        if (springKick) ShowMovementPortrait("hane_flying_kick", 0.50);
        _dropkickImpact = new TaskCompletionSource();
        MovementPose(springKick ? -0.12f : -0.30f, 0.055f);
        double speed = Math.Max(0.1, AnimationSpeed);
        Vector3 contactOffset = Vector3.Zero;
        Vector3 ContactOffset()
        {
            // 1536x1024の差分: 両靴の平均接点は中心から右へ約700px、下へ約65px。
            float pixel = _sprite.PixelSize;
            // 飛び蹴りは右靴の踵（1504,486）。両足ドロップキックと接点を分ける。
            Vector2 contact = springKick ? new(736, 26) : new(700, -65);
            Vector3 center = impact() - screenRight * (Team == 0 ? 1 : -1) * contact.X * pixel - screenUp * contact.Y * pixel;
            return center - (GlobalPosition + Vector3.Up * _portraitBaseY);
        }
        _dropkickTween = CreateTween();
        _dropkickTween.TweenInterval(0.055 / speed);
        _dropkickTween.TweenCallback(Callable.From(() => {
            if (!springKick) ShowMovementPortrait("hane_dropkick", 0.50);
            contactOffset = ContactOffset();
        }));
        _dropkickTween.TweenMethod(Callable.From<float>(t => {
            // 被弾後に元の席へ戻る相手にも、最後の接触まで追従する。
            contactOffset = ContactOffset();
            _dropkickOffset = contactOffset * Mathf.SmoothStep(0, 1, t) + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * (springKick ? 0.18f : 0.35f);
        }), 0f, 1f, 0.14 / speed);
        _dropkickTween.TweenCallback(Callable.From(() => {
            ProcessDropkickPose();
            if (_alive && !_victory) hit();
            _dropkickImpact?.TrySetResult();
        }));
        _dropkickTween.TweenInterval(0.06 / speed);
        _dropkickTween.TweenMethod(Callable.From<float>(t => {
            _dropkickOffset = contactOffset * (1 - Mathf.SmoothStep(0, 1, t)) + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * (springKick ? 0.14f : 0.28f);
        }), 0f, 1f, 0.20 / speed);
        _dropkickTween.TweenCallback(Callable.From(ClearMovementPortrait));
    }

    private void ProcessDropkickPose()
    {
        if (!DropkickActive || _movementPortrait is not ("hane_dropkick" or "hane_flying_kick")) return;
        _sprite.Position = new Vector3(0, _portraitBaseY, 0) + _dropkickOffset;
        _sprite.Rotation = Vector3.Zero;
        _sprite.Scale = Vector3.One;
    }

    private void ClearDropkickPose()
    {
        _dropkickTween?.Kill();
        _dropkickTween = null;
        _dropkickOffset = Vector3.Zero;
        var pending = _dropkickImpact;
        _dropkickImpact = null;
        pending?.TrySetResult();
    }

    public override void _ExitTree()
    {
        ClearAllyBumpPose();
        ClearDropkickPose();
        ClearPalmStrikePose();
    }
}
