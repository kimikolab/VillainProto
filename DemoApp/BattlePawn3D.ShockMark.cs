using Godot;
using System;

public partial class BattlePawn3D
{
    private ShockMarkAura3D? _shockMark;
    internal int StoredCharge { get; private set; }
    internal int Thundercloud { get; private set; }
    internal int MarkLayers { get; private set; }
    internal bool InterruptWhip { get; set; }
    internal int WhipChainSize { get; set; }
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
        Hud.SetMarkLayers(MarkLayers);
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
        Hud.SetMarkLayers(0);
        InterruptWhip = false;
        WhipChainSize = 0;
        _misaFeathers?.SetActive(false);
        if (_shockMark is not null) _shockMark.Hide();
    }
    internal Vector3 WhipOrigin(Camera3D camera) => MovementPortrait == "shiga_interrupt"
        ? MovementPortraitPoint(new Vector2(994, 290), camera) : FxPoint;

    private float _whipFlurry;
    internal void BeginWhipFlurry() => _whipFlurry = 0.64f;
    private void ProcessWhipFlurry(float delta)
    {
        if (_whipFlurry <= 0) return;
        _whipFlurry = Math.Max(0, _whipFlurry - delta);
        float t = 1 - _whipFlurry / 0.64f;
        float twist = Mathf.Sin(t * Mathf.Tau * 2) * Mathf.Sin(t * Mathf.Pi);
        // 席と足元は固定し、上体のひねりだけを鞭の往復に合わせる。
        _sprite.Rotation += Vector3.Back * twist * (Team == 0 ? -0.07f : 0.07f);
        _sprite.Scale *= new Vector3(1 + twist * 0.045f, 1, 1);
    }
}
