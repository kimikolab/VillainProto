using Godot;

public partial class BattlePawn3D
{
    private HealingDrops3D? _healingDrops;
    private PawnAura3D? _powerMist;
    private PawnAura3D.AuraKind _powerKind;
    internal bool HealingDropsActive => AuraAlive(_healingDrops);
    internal bool PowerMistActive => AuraAlive(_powerMist);
    private static bool AuraAlive(Node3D? aura)
        => aura is not null && IsInstanceValid(aura) && !aura.IsQueuedForDeletion();

    public void ShowHealingDrops(int amount)
    {
        if (!_alive || amount <= 0) return;
        if (AuraAlive(_healingDrops)) { _healingDrops!.Refresh(amount); return; }
        _healingDrops = new HealingDrops3D();
        AddChild(_healingDrops);
        _healingDrops.Configure(_fxHeight, amount);
    }

    private void ShowPowerMist(int change)
    {
        var kind = change > 0 ? PawnAura3D.AuraKind.PowerUp : PawnAura3D.AuraKind.PowerDown;
        if (AuraAlive(_powerMist))
        {
            if (_powerKind == kind) { _powerMist!.Refresh(change); return; }
            _powerMist!.Visible = false;
            _powerMist.QueueFree();
        }
        _powerKind = kind;
        _powerMist = CreateAura(kind, change);
    }

    private PawnAura3D CreateAura(PawnAura3D.AuraKind kind, int amount)
    {
        var aura = new PawnAura3D();
        AddChild(aura);
        aura.Configure(kind, _fxHeight, amount);
        return aura;
    }

    private void ClearAuras()
    {
        foreach (var aura in new Node3D?[] { _healingDrops, _powerMist })
            if (AuraAlive(aura)) { aura!.Visible = false; aura.QueueFree(); }
        _healingDrops = null;
        _powerMist = null;
    }
}
