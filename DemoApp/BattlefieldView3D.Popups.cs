using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    // 文字の寿命は実時間。2倍速でも読み取る時間を残し、同じ駒の数字は合算する。
    private readonly List<(int Pawn, Label3D Label)> _popups = new();
    private readonly Dictionary<(int Pawn, bool Heal), (Label3D Label, long Amount)> _numbers = new();
    internal int PopupCount => _popups.Count(p => LivePopup(p.Label));
    private static bool LivePopup(Label3D label)
        => IsInstanceValid(label) && !label.IsQueuedForDeletion();

    private void ResetPopups()
    {
        _popups.Clear();
        _numbers.Clear();
    }

    private void TrimPopups(int pawn)
    {
        _popups.RemoveAll(p => !LivePopup(p.Label));
        foreach (var key in _numbers.Keys.ToArray())
            if (!LivePopup(_numbers[key].Label)) _numbers.Remove(key);
        while (_popups.Count(p => p.Pawn == pawn) >= 2)
            RemovePopup(_popups.FindIndex(p => p.Pawn == pawn));
        while (_popups.Count >= 8) RemovePopup(0);
    }

    private void RemovePopup(int index)
    {
        var entry = _popups[index];
        // QueueFree はフレーム末なので、その場で非表示にする。
        entry.Label.Visible = false;
        entry.Label.QueueFree();
        _popups.RemoveAt(index);
    }

    private void NumberPopup(BattlePawn3D pawn, int amount, bool heal, Color color, bool large)
    {
        var key = (pawn.InstanceId, heal);
        if (_numbers.TryGetValue(key, out var previous) && LivePopup(previous.Label)
            && previous.Label.Modulate.A > 0.45f)
        {
            long total = previous.Amount + amount;
            previous.Label.Text = (heal ? "＋" : "−") + total;
            _numbers[key] = (previous.Label, total);
            return;
        }
        var label = Float(pawn, (heal ? "＋" : "−") + amount, color, true,
            heal ? 3.65f : 3.20f, large ? 2.05f : 1.55f)!;
        _numbers[key] = (label, amount);
    }
}
