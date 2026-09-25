using Godot;
using BattleCore;
using System.Collections.Generic;

public partial class BattlePawn3D
{
    private StatusIconRow3D _statusIcons = null!;
    private MeshInstance3D? _curseStain;
    internal bool HasCurseStain => _curseStain is not null;
    private readonly Dictionary<string, int> _statusSnapshot = new();
    internal int PoisonIconAmount => _statusIcons.Amount(StatusKeys.Poison);
    internal int StatusIconCount => _statusIcons.ActiveCount;
    internal int StatusIconFlashCount => _statusIcons.FlashCount;
    internal bool HasStatusIcon(string key) => _statusIcons.Has(key);
    internal bool HasConfusionEffect => _confusion.Visible;
    private void BuildStatusIcons()
    {
        _statusIcons = new StatusIconRow3D { Position = new Vector3(0, _hpBack.Position.Y - 0.23f, 0.06f) };
        AddChild(_statusIcons);
    }
    public void BeginStatusSnapshot() => _statusSnapshot.Clear();
    public void ReadStatusSnapshot(string label, int amount)
    {
        if (amount > 0 && StatusIconArt.KeyOf(label) is { } key) _statusSnapshot[key] = amount;
    }
    public void CommitStatusSnapshot()
    {
        if (!_alive) return;
        if (!_statusSnapshot.ContainsKey(StatusKeys.Stun)) SetFrightened(false);
        foreach (string key in StatusIconArt.Keys)
        {
            if (key is StatusKeys.Poison or StatusKeys.Footing or StatusKeys.Concentrated or StatusKeys.Guren)
                _statusIcons.SetAmount(key, _statusSnapshot.GetValueOrDefault(key), false);
            else _statusIcons.Set(key, _statusSnapshot.ContainsKey(key));
        }
        _confusion.SetActive(!_victory && _statusSnapshot.ContainsKey(StatusKeys.Confused));
        SetCowed(!_victory && _statusSnapshot.ContainsKey(StatusKeys.Cowed));
        SetCurseStain(!_victory && _statusSnapshot.ContainsKey(StatusKeys.Curse));
        SetGuren(_statusSnapshot.GetValueOrDefault(StatusKeys.Guren), false);
        SetStigma(_statusSnapshot.ContainsKey(StatusKeys.Stigma));
    }

    // 付与は差分、ターン頭の写しは残量。写しを加算すると二重計上になる。
    public void AddPoisonIconAmount(int amount)
    {
        if (_alive && amount > 0)
            _statusIcons.SetAmount(StatusKeys.Poison, PoisonIconAmount + amount, true);
    }

    public void SetPoisonRemaining(int amount)
    {
        if (!_alive) return;
        _statusIcons.SetAmount(StatusKeys.Poison, amount, false);
        if (amount > 0) _statusSnapshot[StatusKeys.Poison] = amount;
        else _statusSnapshot.Remove(StatusKeys.Poison);
        SetPoisoned(amount > 0);
    }
    public void SetConcentrated(int amount, bool animate = true)
    {
        if (_alive) _statusIcons.SetAmount(StatusKeys.Concentrated, amount, animate);
    }
    internal int ConcentratedAmount => _statusIcons.Amount(StatusKeys.Concentrated);
    public void PulsePoisonIcon() => _statusIcons.Pulse(StatusKeys.Poison);
    private void SetCurseStain(bool active)
    {
        if (active && _curseStain is null)
            _curseStain = HexMudFx.Stain(this, ToLocal(FxPoint));
        else if (!active && _curseStain is not null)
        {
            _curseStain.QueueFree();
            _curseStain = null;
        }
    }

    public void SetStatusIcon(string keyOrLabel, bool active)
    {
        if (_alive && StatusIconArt.KeyOf(keyOrLabel) is { } key)
        {
            _statusIcons.Set(key, active);
            if (key == StatusKeys.Confused) _confusion.SetActive(active && !_victory);
            if (key == StatusKeys.Curse) SetCurseStain(active && !_victory);
            if (key == StatusKeys.Cowed) SetCowed(active && !_victory);
        }
    }
}
