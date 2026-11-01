using Godot;

public partial class BattlePawn3D
{
    internal bool ChiriIdlePaused { get; set; }

    // 復帰は本番の再生時計に任せる。独立Tweenを作らず、停止・倍速で差分だけ先へ進めない。
    internal void SetChiriPose(string? key)
    {
        if (_unitId != "chiri") return;
        if (key is null)
        {
            if (_movementPortrait is "chiri_plant" or "chiri_release") ClearMovementPortrait();
            return;
        }
        if (!_alive || _victory) return;
        _movementPortraitTween?.Kill();
        _movementPortraitTween = null;
        _movementPortrait = key;
        _movementFlip = null;
        RefreshBattlePortrait();
    }
}
