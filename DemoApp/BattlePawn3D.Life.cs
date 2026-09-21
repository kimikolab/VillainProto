using Godot;

public partial class BattlePawn3D
{
    private LifeTransition3D? _lifeTransition;

    private void ShowLifeTransition(LifeTransition3D.Kind kind)
    {
        if (IsInstanceValid(_lifeTransition))
        {
            _lifeTransition!.Visible = false;
            _lifeTransition.QueueFree();
        }
        _lifeTransition = new LifeTransition3D();
        AddChild(_lifeTransition);
        // 駒の倒れ込みや出現時の縮尺に巻き込まず、移動先の足元を基準に描く。
        _lifeTransition.TopLevel = true;
        _lifeTransition.GlobalPosition = kind == LifeTransition3D.Kind.Death ? GlobalPosition : _home;
        _lifeTransition.Configure(kind, _fxHeight * 1.5f);
    }
}
