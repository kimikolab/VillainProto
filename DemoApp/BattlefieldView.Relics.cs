using BattleCore;
using Godot;
using System.Collections.Generic;

// =====================================================================================
// 戦闘側の編成画面で、席の駒の札に「◆名前」（第455期 §1-2・作戦マップの駒の札と同じ印）。表示だけ。
// 札は席に付く（`Main._seatRelics`）——席が空なら出さない。
// =====================================================================================

public partial class BattlefieldView
{
    private readonly string?[] _seatRelicNames = new string?[FormationRules.PlayableSlotCount];

    /// <summary>席ごとのレリックの名前（null ＝ 持たせていない）。</summary>
    public void SetSeatRelics(IReadOnlyList<string?> names)
    {
        for (int i = 0; i < _seatRelicNames.Length; i++) _seatRelicNames[i] = i < names.Count ? names[i] : null;
        ApplySeatRelics();
    }

    /// <summary>その席の札に出ている「◆名前」（頭なしの確認だけが読む）。</summary>
    public string? SeatRelicText(int slot) => _slots[slot]?.RelicText;

    private void ApplySeatRelics()
    {
        for (int i = 0; i < _slots.Length; i++) _slots[i]?.SetRelic(_seatRelicNames[i]);
    }
}

public partial class FormationSlot
{
    private Label? _relic;

    /// <summary>席の札の「◆名前」（第455期）。駒のいない席では出さない。</summary>
    public void SetRelic(string? name)
    {
        if (_relic is null)
        {
            if (name is null) return;
            _relic = UiKit.Text("", 10, UiKit.Gold);
            _relic.HorizontalAlignment = HorizontalAlignment.Center;
            _relic.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            _relic.MouseFilter = MouseFilterEnum.Ignore;
            _stats.GetParent().AddChild(_relic);
        }
        _relic.Text = name is null ? "" : "◆" + name;
        _relic.Visible = name is not null && _def is not null;
    }

    /// <summary>席の札に出ている「◆名前」（頭なしの確認だけが読む・出ていなければ null）。</summary>
    public string? RelicText => _relic is { Visible: true } r ? r.Text : null;
}
