using Godot;

public partial class BattlePawn3D
{
    private float _hushBrace;
    internal void BraceUnderHush() => _hushBrace = .65f;
    private void ProcessHushBrace(float delta)
    {
        if (_hushBrace <= 0) return;
        _hushBrace = Mathf.Max(0, _hushBrace - delta);
        // 振り上げて静止し、最後だけ構えへ戻る。
        float pose = Mathf.Min(1, _hushBrace / .12f);
        _sprite.Rotation += Vector3.Back * (Team == 0 ? -.14f : .14f) * pose;
        _sprite.Position += Vector3.Up * .09f * pose;
    }
    internal bool HushShattered { get; private set; }
    internal void ShatterHushPortrait()
    {
        HushShattered = true;
        ClearMovementPortrait();
        RefreshBattlePortrait();
        // 破砕の叫びを実時間で一拍見せてから、覆って立つ姿へ戻す。
        ShowMovementPortrait("husher_shatter_burst", .65 * System.Math.Max(.1, AnimationSpeed));
    }
}
