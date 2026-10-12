using Godot;
using System;

public partial class BattlePawn3D
{
    private ShockMarkAura3D? _shockMark;
    private MarkLockOn3D? _markLock;
    internal bool HasVisibleMark => MarkLayers > 0 || HasStatusIcon(BattleCore.StatusKeys.Marked);
    internal bool MarkLockActive => _markLock?.Active == true;
    internal int MarkLockPlays => _markLock?.Plays ?? 0;
    internal Vector3 MarkFocusPoint => MarkLayers > 0
        ? FxPoint + (GetViewport().GetCamera3D()?.GlobalBasis.Z ?? Vector3.Back) * 0.32f
        : _statusEffects.MarkPoint;
    internal void PulseMark(double speed)
    {
        if (!ShockMarkActive) return;
        if (_markLock is null)
        {
            _markLock = new MarkLockOn3D(); AddChild(_markLock); _markLock.Configure(this);
        }
        _markLock.Play(speed);
    }
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
        if (MarkLayers == 0) _markLock?.Stop();
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
        _markLock?.Stop();
        EndSharePortrait();
        Hud.SetMarkLayers(0);
        InterruptWhip = false;
        WhipChainSize = 0;
        _misaFeathers?.SetActive(false);
        if (_shockMark is not null) _shockMark.Hide();
    }
    internal Vector3 WhipOrigin(Camera3D camera) => MovementPortrait == "shiga_interrupt"
        ? MovementPortraitPoint(new Vector2(994, 290), camera) : FxPoint;

    private float _whipFlurry;
    private float _whipDuration = 0.64f, _whipContact;
    private double _whipPortraitSpeed = 1;
    private bool _electricWhipPose, _whipClockPaused;
    private ElectricWhipStyle _electricWhipStyle;
    internal void BeginWhipFlurry(bool electric = false, bool sweep = true, ElectricWhipStyle style = ElectricWhipStyle.Flurry)
    {
        _electricWhipPose = electric; _whipDuration = sweep ? 0.64f : 0.60f;
        _electricWhipStyle = style;
        _whipContact = sweep ? 0.22f : 0.62f;
        _whipPortraitSpeed = Math.Max(0.1, AnimationSpeed);
        _whipFlurry = _whipDuration;
    }
    internal void SetWhipClock(bool paused, double speed)
    {
        _whipClockPaused = paused;
        if (_electricWhipPose && _whipFlurry > 0) AnimationSpeed = speed;
        if (_electricWhipPose && _movementPortrait == "shiga_interrupt" && _movementPortraitTween?.IsValid() == true)
        {
            _movementPortraitTween.SetSpeedScale((float)(Math.Max(0.1, speed) / _whipPortraitSpeed));
            if (paused) _movementPortraitTween.Pause(); else _movementPortraitTween.Play();
        }
    }
    internal void EndWhipFlurry()
    {
        _whipFlurry = 0; _whipClockPaused = false; _electricWhipPose = false;
        if (_movementPortrait == "shiga_interrupt") ClearMovementPortrait();
    }
    private void ProcessWhipFlurry(float delta)
    {
        if (_whipFlurry <= 0) return;
        if (!_whipClockPaused) _whipFlurry = Math.Max(0, _whipFlurry - delta);
        float t = 1 - _whipFlurry / _whipDuration;
        if (_electricWhipPose)
        {
            float snap = Mathf.Clamp((t - Math.Max(0, _whipContact - 0.18f)) / 0.18f, 0, 1);
            float recover = Mathf.Clamp((t - 0.78f) / 0.22f, 0, 1);
            float lean = Mathf.Lerp(-0.10f, 0.17f, snap * snap) * (1 - recover);
            if (_electricWhipStyle == ElectricWhipStyle.Flurry)
                lean = Mathf.Sin(t * Mathf.Tau * 4.5f) * 0.13f * Mathf.Sin(t * Mathf.Pi);
            _sprite.Rotation += Vector3.Back * lean * (Team == 0 ? -1 : 1);
            _sprite.Scale *= new Vector3(1 + snap * 0.07f * (1 - recover), 1, 1);
            return;
        }
        float twist = Mathf.Sin(t * Mathf.Tau * 2) * Mathf.Sin(t * Mathf.Pi);
        // 席と足元は固定し、上体のひねりだけを鞭の往復に合わせる。
        _sprite.Rotation += Vector3.Back * twist * (Team == 0 ? -0.07f : 0.07f);
        _sprite.Scale *= new Vector3(1 + twist * 0.045f, 1, 1);
    }
}
