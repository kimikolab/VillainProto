using Godot;

public partial class BattlePawn3D
{
    private ChargeAura3D? _chargeAura;
    private bool _charging;
    public bool IsCharging => _charging;

    public void BeginCharge(int percent)
    {
        if (!_alive || _victory) return;
        if (_charging && AuraAlive(_chargeAura)) { _chargeAura!.SetPower(percent); return; }
        CancelCharge();
        _chargeAura = new ChargeAura3D();
        AddChild(_chargeAura);
        _chargeAura.Configure(_fxHeight, percent);
        _charging = true;
    }

    public void ReleaseCharge()
    {
        if (!_charging) return;
        _charging = false;
        if (AuraAlive(_chargeAura)) _chargeAura!.Release();
    }

    public void CancelCharge()
    {
        _charging = false;
        if (AuraAlive(_chargeAura)) { _chargeAura!.Visible = false; _chargeAura.QueueFree(); }
        _chargeAura = null;
    }
}
