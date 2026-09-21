using Godot;
using System.Collections.Generic;

public partial class BattlePawn3D
{
    private StatusIconRow3D _statusIcons = null!;
    private readonly HashSet<string> _statusSnapshot = new();
    internal int StatusIconCount => _statusIcons.ActiveCount;
    internal int StatusIconFlashCount => _statusIcons.FlashCount;
    internal bool HasStatusIcon(string key) => _statusIcons.Has(key);
    private void BuildStatusIcons()
    {
        _statusIcons = new StatusIconRow3D { Position = new Vector3(0, _hpBack.Position.Y - 0.23f, 0.06f) };
        AddChild(_statusIcons);
    }
    public void BeginStatusSnapshot() => _statusSnapshot.Clear();
    public void ReadStatusSnapshot(string label, int amount)
    {
        if (amount > 0 && StatusIconArt.KeyOf(label) is { } key) _statusSnapshot.Add(key);
    }
    public void CommitStatusSnapshot()
    {
        if (!_alive) return;
        foreach (string key in StatusIconArt.Keys) _statusIcons.Set(key, _statusSnapshot.Contains(key));
    }
    public void SetStatusIcon(string keyOrLabel, bool active)
    {
        if (_alive && StatusIconArt.KeyOf(keyOrLabel) is { } key) _statusIcons.Set(key, active);
    }
}
