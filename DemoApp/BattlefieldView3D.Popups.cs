using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    // 文字の寿命は実時間。2倍速でも読み取る時間を残し、同じ駒の数字は合算する。
    private Control _popupLayer = null!;
    private readonly List<(int Pawn, PopupLabel2D Label)> _popups = new();
    private readonly Dictionary<(int Pawn, bool Heal), (PopupLabel2D Label, long Amount)> _numbers = new();
    internal int PopupCount => _popups.Count(p => LivePopup(p.Label));
    private static bool LivePopup(PopupLabel2D label)
        => IsInstanceValid(label) && !label.IsQueuedForDeletion();

    private void BuildPopupLayer()
    {
        // HP 札の後、見出し・カットインの前。同じ親の順序で描画を保証する。
        _popupLayer = new Control { Name = "PopupLayer", MouseFilter = MouseFilterEnum.Ignore };
        _popupLayer.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_popupLayer);
    }

    private PopupLabel2D CreatePopup(Vector3 position, string text, Color color, int fontSize)
    {
        var label = new PopupLabel2D();
        _popupLayer.AddChild(label);
        label.Configure(_camera, position, text, color, fontSize);
        return label;
    }

    private void ResetPopups()
    {
        // 3D の _fxRoot とは別の所有物。再戦したフレームから古い文字を消す。
        foreach (Node child in _popupLayer.GetChildren())
        {
            ((CanvasItem)child).Hide();
            child.QueueFree();
        }
        _popups.Clear();
        _numbers.Clear();
        _tickNumbers.Clear();
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
