using Godot;

public partial class BattlePawn3D
{
    private string? _movementPortrait;
    private Tween? _movementPortraitTween;
    internal string? MovementPortrait => _movementPortrait;
    internal int OpeningAttack => _baseAttack;

    // 立ち絵の差分は短い拍だけ使う。必死の逃げ足の直後の通常回避では上書きしない。
    public void ShowMovementPortrait(string key, double seconds, System.Action? finished = null)
    {
        if (!_alive || _victory || !key.StartsWith(_unitId + "_")) return;
        if (_movementPortrait == "sero_last_dodge" && key == "sero_evade") return;
        if (key != "hane_dropkick") ClearDropkickPose();
        if (key != "hane_palm") ClearPalmStrikePose();
        _movementPortraitTween?.Kill();
        ResetBowPortrait();
        _movementPortrait = key;
        RefreshBattlePortrait();
        _movementPortraitTween = CreateTween();
        _movementPortraitTween.TweenInterval(seconds / System.Math.Max(0.1, AnimationSpeed));
        _movementPortraitTween.TweenCallback(Callable.From(() => {
            ClearMovementPortrait();
            if (_alive && !_victory) finished?.Invoke();
        }));
    }

    private void ClearMovementPortrait()
    {
        ClearDropkickPose();
        ClearPalmStrikePose();
        _movementPortraitTween?.Kill();
        _movementPortraitTween = null;
        if (_movementPortrait is null) return;
        _movementPortrait = null;
        RefreshBattlePortrait();
    }
}
