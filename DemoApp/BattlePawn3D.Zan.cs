using Godot;
using System;
using System.Collections.Generic;

public partial class BattlePawn3D
{
    private bool _vendettaMoving;
    private readonly List<Node3D> _roundAfterimages = new();
    internal int RoundAfterimageCount => _roundAfterimages.FindAll(n => IsInstanceValid(n) && !n.IsQueuedForDeletion()).Count;
    private void ClearRoundAfterimages()
    {
        foreach (var root in _roundAfterimages)
            if (IsInstanceValid(root) && !root.IsQueuedForDeletion()) { root.Hide(); root.QueueFree(); }
        _roundAfterimages.Clear();
    }
    internal bool RoundMoving { get; private set; }
    internal bool MoveRound(Vector3 target)
    {
        if (!_alive || _victory) return false;
        Vector3 direction = target - Position;
        Vector3 contact = target + new Vector3(Team == 0 ? -1.2f : 1.2f, 0, 0.8f);
        contact.Y = Home.Y;
        bool travel = Position.DistanceSquaredTo(contact) > 0.04f;
        ShowMovementPortrait("zan_vendetta", 10, flip: direction.X < 0);
        RoundMoving = true;
        if (!travel) return false;
        _roundAfterimages.RemoveAll(n => !IsInstanceValid(n) || n.IsQueuedForDeletion());
        _roundAfterimages.Add(BeginBonusAfterimage(new Color("ff5472"), 0.28 / AnimationSpeed, false, 0.75f));
        var tween = BeginMotion();
        tween.TweenProperty(this, "position", contact, 0.12 / AnimationSpeed)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        return true;
    }

    internal void ReturnFromRound()
    {
        if (!RoundMoving) return;
        if (!_alive || _victory) { RoundMoving = false; return; }
        var tween = BeginMotion();
        tween.TweenProperty(this, "position", RestPosition, 0.16 / AnimationSpeed);
        tween.TweenCallback(Callable.From(() => {
            RoundMoving = false;
            if (_movementPortrait == "zan_vendetta") ClearMovementPortrait();
        }));
    }

    internal void EndVendetta()
    {
        ClearRoundAfterimages();
        if (!_vendettaMoving && !RoundMoving) return;
        RoundMoving = false;
        _vendettaMoving = false;
        _motion?.Kill();
        if (_alive && !_victory) Position = RestPosition;
        if (_movementPortrait == "zan_vendetta") ClearMovementPortrait();
    }
    // 席とHomeを変えず、短剣の間合いにだけ踏み込んで帰る。
    internal void BeginVendetta(Vector3 target, double seconds)
    {
        if (!_alive || _victory) return;
        Vector3 origin = RestPosition;
        Vector3 direction = new(target.X - origin.X, 0, target.Z - origin.Z);
        Vector3 contact = target - direction.Normalized() * 1.75f;
        contact.Y = origin.Y;
        ShowMovementPortrait("zan_vendetta", seconds, flip: direction.X < 0);
        BeginBonusAfterimage(new Color("bb234c"), seconds / AnimationSpeed, false, 0.6f);
        var tween = BeginMotion();
        _vendettaMoving = true;
        tween.TweenInterval(0.10 / AnimationSpeed);
        tween.TweenProperty(this, "position", contact, 0.13 / AnimationSpeed)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(Math.Max(0.01, seconds - 0.40) / AnimationSpeed);
        tween.TweenProperty(this, "position", origin, 0.17 / AnimationSpeed)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tween.TweenCallback(Callable.From(() => _vendettaMoving = false));
    }
}
