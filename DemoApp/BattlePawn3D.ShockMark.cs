using Godot;
using System;

public partial class BattlePawn3D
{
    private ShockMarkAura3D? _shockMark;
    internal int StoredCharge { get; private set; }
    internal int Thundercloud { get; private set; }
    internal int MarkLayers { get; private set; }
    internal bool InterruptWhip { get; set; }
    internal bool PresentationAlive => _alive && !_victory;
    private bool _shockMarkEnded;
    internal bool ShockMarkActive => PresentationAlive && !_shockMarkEnded;

    private ShockMarkAura3D ShockMark
    {
        get
        {
            if (_shockMark is not null) return _shockMark;
            _shockMark = new ShockMarkAura3D();
            AddChild(_shockMark);
            _shockMark.Configure(this);
            return _shockMark;
        }
    }
    internal void SetStoredCharge(int amount) { StoredCharge = Math.Clamp(amount, 0, 4); ShockMark.Refresh(); }
    internal void SetThundercloud(int amount) { Thundercloud = Math.Clamp(amount, 0, 8); ShockMark.Refresh(); }
    internal void SetMarkLayers(int amount)
    {
        MarkLayers = Math.Max(0, amount);
        ShockMark.Refresh();
        // 層の照準と従来の単一照準を重ねない。
        _statusEffects.SetLayeredMark(MarkLayers > 0);
    }
    internal void ApplyScar(int maximum, int hp)
    {
        MaxHp = Math.Max(1, maximum);
        SetHp(hp);
    }
    internal void EndShockMark()
    {
        _shockMarkEnded = true;
        InterruptWhip = false;
        _misaFeathers?.SetActive(false);
        if (_shockMark is not null) _shockMark.Hide();
    }
    internal Vector3 WhipOrigin(Camera3D camera) => MovementPortrait == "shiga_interrupt"
        ? MovementPortraitPoint(new Vector2(994, 290), camera) : FxPoint;
}
