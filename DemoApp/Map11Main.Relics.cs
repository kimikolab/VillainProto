using BattleCore;
using Godot;
using System.Linq;

// =====================================================================================
// 作戦マップのレリック（第452期・指示書 design/PHASE452_MAP_RELIC_SPEC.md §1-3）。<b>判定は 1 つも持たない</b>——
// 候補・手持ち・持たせる／外すは全部 `Map11State`（`RelicOffer` ／ `ChooseRelic` ／ `RelicHand` ／ `EquipRelic` ／ `UnequipRelic`）。
// 札の名前と文は `RelicCatalog.Info`（`design/RELIC_SUMMARY.md` と同じ文）。見た目は仮（Codex への申し送り）。
// =====================================================================================

public partial class Map11Main
{
    private PanelContainer? _relicOfferPanel;

    /// <summary>レリックの札の 1 行（名前 — 文）。</summary>
    private static string RelicLine(TraitId r) { RelicInfo i = RelicCatalog.Info(r); return $"{i.Name} — {i.Text}"; }

    /// <summary>触れたときの説明（名前・文・何を何に変えるか）。</summary>
    private static string RelicHelp(TraitId r) { RelicInfo i = RelicCatalog.Info(r); return $"{i.Name}（{i.Kind}）\n{i.Text}\n{i.Bridge}"; }

    /// <summary>中間拠点を取った刻の候補の窓（候補が出ている間だけ出す）。</summary>
    private void RefreshRelicOffer()
    {
        bool show = St.Stage.Relic.On && St.RelicOffer.Count > 0 && !St.Finished;
        if (!show) { if (_relicOfferPanel is not null) _relicOfferPanel.Visible = false; return; }
        if (_relicOfferPanel is null)
        {
            _relicOfferPanel = Panel();
            _relicOfferPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
            _relicOfferPanel.CustomMinimumSize = new Vector2(620, 0);
            _uiRoot.AddChild(_relicOfferPanel);
        }
        foreach (Node child in _relicOfferPanel.GetChildren()) { _relicOfferPanel.RemoveChild(child); child.QueueFree(); }
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        _relicOfferPanel.AddChild(box);
        box.AddChild(UiKit.Text("レリックを 1 枚（中間拠点を取った見返り・このマップの間だけ）", 18, GoldColor));
        box.AddChild(UiKit.Text("得た札は手札の「レリック」の段へ。組み直しの窓で駒を選んで持たせる（1 駒 1 枚）", 13, UiKit.Muted));
        for (int k = 0; k < St.RelicOffer.Count; k++)
        {
            int index = k;
            TraitId r = St.RelicOffer[k];
            Button b = UiKit.ActionButton(RelicLine(r), GoldColor);
            b.Alignment = HorizontalAlignment.Left;
            b.ClipText = true;
            b.TooltipText = RelicHelp(r);
            b.Pressed += () =>
            {
                if (!St.ChooseRelic(index)) return;
                ShowToast($"レリック「{RelicCatalog.Info(r).Name}」を得た（手札の「レリック」の段）", GoldColor, 3.5f);
                Refresh();
            };
            box.AddChild(b);
        }
        Button skip = UiKit.ActionButton("取らない", UiKit.Muted);
        skip.Pressed += () => { St.ChooseRelic(-1); Refresh(); };
        box.AddChild(skip);
        _relicOfferPanel.Visible = true;
        _relicOfferPanel.MoveToFront();
    }

    /// <summary>手札の列の「レリック（n）」の段（持たせる先は隊の 5 席か手札の駒を選んでから）。</summary>
    private void AddRelicRows()
    {
        if (!St.Stage.Relic.On) return;
        _benchList.AddChild(UiKit.Text($"レリック（{St.RelicHand.Count}）——駒を選ぶと「持たせる」が出る", 13, GoldColor));
        foreach (TraitId r in St.RelicHand)
        {
            Label l = UiKit.Text("◆ " + RelicLine(r), 13, UiKit.Ink);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.TooltipText = RelicHelp(r);
            l.MouseFilter = Control.MouseFilterEnum.Pass;
            _benchList.AddChild(l);
        }
    }

    /// <summary>選んだ駒の説明の下に、持たせる／外すの札（組み直しのできる場所の駒だけ）。</summary>
    private void AddRelicButtons(VBoxContainer focus, UnitState? u)
    {
        if (!St.Stage.Relic.On || u is null) return;
        if (u.Relic is TraitId has)
        {
            Label l = UiKit.Text("◆ 持っている: " + RelicLine(has), 13, GoldColor);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.TooltipText = RelicHelp(has);
            l.MouseFilter = Control.MouseFilterEnum.Pass;
            focus.AddChild(l);
        }
        if (!_reform || !St.CanEquip(u)) return;
        foreach (TraitId r in St.RelicHand.Distinct().ToList())
        {
            Button b = UiKit.ActionButton($"持たせる  ◆{RelicCatalog.Info(r).Name}", GoldColor);
            b.TooltipText = RelicHelp(r);
            b.Pressed += () =>
            {
                if (!St.EquipRelic(u, r)) return;
                ShowToast($"{u.Def.Name} に「{RelicCatalog.Info(r).Name}」を持たせた", GoldColor, 3.0f);
                Refresh();
            };
            focus.AddChild(b);
        }
        if (u.Relic is TraitId cur)
        {
            Button off = UiKit.ActionButton($"外す  ◆{RelicCatalog.Info(cur).Name}", UiKit.Muted);
            off.Pressed += () => { if (St.UnequipRelic(u)) Refresh(); };
            focus.AddChild(off);
        }
    }
}
