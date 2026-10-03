using Godot;

public partial class BattlePawn3D
{
    private string? _movementPortrait;
    private Tween? _movementPortraitTween;
    private bool? _movementFlip;
    internal string? MovementPortrait => _movementPortrait;
    internal int OpeningAttack => _baseAttack;

    // 立ち絵の差分は短い拍だけ使う。必死の逃げ足の直後の通常回避では上書きしない。
    public void ShowMovementPortrait(string key, double seconds, System.Action? finished = null, bool? flip = null)
    {
        if (!_alive || _victory || !key.StartsWith(_unitId + "_")) return;
        if (_movementPortrait == "sero_last_dodge" && key == "sero_evade") return;
        ClearAllyBumpPose();
        if (key is not ("hane_dropkick" or "hane_flying_kick")) ClearDropkickPose();
        if (key is not ("hane_palm" or "hane_spring_guard")) ClearPalmStrikePose();
        _movementPortraitTween?.Kill();
        ResetBowPortrait();
        _movementPortrait = key;
        _movementFlip = flip;
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
        ClearAllyBumpPose();
        ClearDropkickPose();
        ClearPalmStrikePose();
        _movementPortraitTween?.Kill();
        _movementPortraitTween = null;
        if (_movementPortrait is null) return;
        _movementPortrait = null;
        _movementFlip = null;
        RefreshBattlePortrait();
    }

    // 差分に描かれた蔓の先・弓の位置からエフェクトを出す。右向き原画を敵側では左右反転する。
    internal Vector3 MovementPortraitPoint(Vector2 pixel, Camera3D camera)
        => _sprite.GlobalPosition
            + camera.GlobalBasis.X * (_sprite.FlipH ? -1 : 1) * (pixel.X - _sprite.Texture.GetWidth() * 0.5f) * _sprite.PixelSize
            + camera.GlobalBasis.Y * (_sprite.Texture.GetHeight() * 0.5f - pixel.Y) * _sprite.PixelSize;
}
