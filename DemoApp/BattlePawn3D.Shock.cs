using Godot;

public partial class BattlePawn3D
{
    private ShockAura3D? _shockAura;
    internal bool HasShockAura => _shockAura is not null;
    public Vector3 DischargePoint => _alive ? FxPoint
        : GetParent<Node3D>().ToGlobal(_home + new Vector3(0, _fxHeight, 0));
    public void SetShocked(bool active)
    {
        active &= _alive && !_victory;
        if (active && _shockAura is null)
        {
            _shockAura = new ShockAura3D { Position = ToLocal(FxPoint) };
            AddChild(_shockAura);
        }
        if (!active && _shockAura is not null)
        {
            _shockAura.Hide(); _shockAura.QueueFree(); _shockAura = null;
        }
    }
    public void ShockCollapse()
    {
        // 崩れる前の短い痙攣。死亡の処理と音は既存のDeathが一度だけ担当。
        var tween = CreateTween();
        var start = _sprite.Position;
        for (int i = 0; i < 4; i++)
            tween.TweenProperty(_sprite, "position", start + Vector3.Right * (i % 2 == 0 ? 0.055f : -0.055f), 0.035 / AnimationSpeed);
        tween.TweenProperty(_sprite, "position", start, 0.02 / AnimationSpeed);
    }
}
