using Godot;

public partial class BattlePawn3D
{
    private SomVeil3D? _somVeil;
    internal float SomHeight => _portraitHeight;
    internal Vector3 SomCenter => _sprite.GlobalPosition;
    internal int SomVeilAmount => _somVeil?.Amount ?? 0;

    internal void SetSomVeil(int amount, bool gained = false)
    {
        if (_somVeil is null && amount > 0 && PresentationAlive)
        {
            _somVeil = new SomVeil3D(); AddChild(_somVeil); _somVeil.Configure(this);
        }
        _somVeil?.SetAmount(PresentationAlive ? amount : 0, gained);
    }
    internal void RippleSomVeil() => _somVeil?.Ripple();
    internal void ClearSomPresentation()
    {
        SetSomVeil(0);
        if (_movementPortrait?.StartsWith("som_") == true || _movementPortrait?.StartsWith("fodder_") == true)
            ClearMovementPortrait();
    }
}
