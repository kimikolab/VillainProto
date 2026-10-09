using Godot;

public partial class BattlePawn3D
{
    private bool _hisaCover;
    internal void BeginHisaCover(Vector3 ally, Vector3 enemy, Vector3 cameraFront)
    {
        if (!PresentationAlive) return;
        _hisaCover = true;
        Vector3 direction = (enemy - ally).Normalized();
        Vector3 at = ally + direction * .9f + cameraFront;
        at.Y = Home.Y;
        _guardPosition = at;
        ShowMovementPortrait("hisa_cover", 1.25, flip: direction.X < 0);
        var tween = BeginMotion();
        tween.TweenProperty(this, "position", at, .20 / AnimationSpeed)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }
    internal void ReturnFromHisaCover()
    {
        if (!_hisaCover) return;
        _hisaCover = false;
        if (PresentationAlive) EndGuard();
    }
    internal void StopHisaGesture()
    {
        if (UnitId == "hisa" && _movementPortrait is "hisa_rally" or "hisa_command") ClearMovementPortrait();
    }
}
