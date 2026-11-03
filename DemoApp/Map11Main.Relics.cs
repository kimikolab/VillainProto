using BattleCore;
using Godot;
using System.Linq;

// =====================================================================================
// 作戦マップのレリック（第452期・第453期・指示書 design/PHASE452_MAP_RELIC_SPEC.md §1-3 ／ PHASE453_RELIC_BRIDGE_SPEC.md §0-3・§0-4）。
// <b>判定は 1 つも持たない</b>——候補・手持ち・持たせる／外すは全部 `Map11State`（`RelicOffer` ／ `ChooseRelic` ／ `RelicHand` ／ `EquipRelic` ／ `UnequipRelic`）。
// 札の名前と文は `RelicCatalog.Info`、両端（引き金 → 効果）は `RelicBridges`。見た目は仮（Codex への申し送り）。
// 第453期: 手札の札を押すと<b>つまむ</b>（駒の入れ替えと同じ作法）→ 駒の札（隊の 5 席・手札）を押すと持たせる。持たせた札を押すと外す。
// =====================================================================================

public partial class Map11Main
{
    private PanelContainer? _relicOfferPanel;
    private PanelContainer? _relicCatalogPanel;
    /// <summary>つまんでいるレリック（第453期・次に押した駒に持たせる）。</summary>
    private TraitId? _holdRelic;

    /// <summary>レリックの札の 1 行（名前 — 文）。</summary>
    private static string RelicLine(TraitId r) { RelicInfo i = RelicCatalog.Info(r); return $"{i.Name} — {i.Text}"; }

    /// <summary>触れたときの説明（名前・文・両端・何を何に変えるか）。</summary>
    private static string RelicHelp(TraitId r)
    {
        RelicInfo i = RelicCatalog.Info(r);
        string ends = RelicBridges.Of(r) is RelicEnds e ? $"\n{RelicBridges.Label(e.Trigger)} → {RelicBridges.Label(e.Effect)}" : "";
        return $"{i.Name}（{i.Kind}）\n{i.Text}{ends}\n{i.Bridge}";
    }

    /// <summary>札の両端の小札（引き金 → 効果・第453期）。</summary>
    private static HBoxContainer RelicEndsChips(TraitId r, int fontSize = 10)
    {
        var box = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 3);
        if (RelicBridges.Of(r) is not RelicEnds e) return box;
        box.AddChild(UnitTagChips.Word(RelicBridges.Label(e.Trigger), fontSize));
        Label arrow = UiKit.Text("→", fontSize, UiKit.Muted);
        arrow.MouseFilter = Control.MouseFilterEnum.Ignore;
        box.AddChild(arrow);
        box.AddChild(UnitTagChips.Word(RelicBridges.Label(e.Effect), fontSize));
        return box;
    }

    /// <summary>中間拠点を取った刻の候補の窓（候補が出ている間だけ出す）。</summary>
    private void RefreshRelicOffer()
    {
        bool show = St.Stage.Relic.On && St.RelicOffer.Count > 0 && !St.Finished;
        if (!show) { if (_relicOfferPanel is not null) _relicOfferPanel.Visible = false; return; }
        if (_relicOfferPanel is null)
        {
            _relicOfferPanel = Panel();
            _relicOfferPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
            _relicOfferPanel.CustomMinimumSize = new Vector2(680, 0);
            _uiRoot.AddChild(_relicOfferPanel);
        }
        foreach (Node child in _relicOfferPanel.GetChildren()) { _relicOfferPanel.RemoveChild(child); child.QueueFree(); }
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        _relicOfferPanel.AddChild(box);
        box.AddChild(UiKit.Text("レリックを 1 枚（中間拠点を取った見返り・このマップの間だけ）", 18, GoldColor));
        box.AddChild(UiKit.Text("得た札は手札の「レリック」の段へ。札を押してつまみ、駒の札を押すと持たせる（1 駒 1 枚）", 13, UiKit.Muted));
        for (int k = 0; k < St.RelicOffer.Count; k++)
        {
            int index = k;
            TraitId r = St.RelicOffer[k];
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            HBoxContainer ends = RelicEndsChips(r, 11);
            ends.CustomMinimumSize = new Vector2(150, 0);
            row.AddChild(ends);
            Button b = UiKit.ActionButton(RelicLine(r), GoldColor);
            b.Alignment = HorizontalAlignment.Left;
            b.ClipText = true;
            b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            b.TooltipText = RelicHelp(r);
            b.Pressed += () =>
            {
                if (!St.ChooseRelic(index)) return;
                ShowToast($"レリック「{RelicCatalog.Info(r).Name}」を得た（手札の「レリック」の段）", GoldColor, 3.5f);
                Refresh();
            };
            row.AddChild(b);
            box.AddChild(row);
        }
        var foot = new HBoxContainer();
        foot.AddThemeConstantOverride("separation", 8);
        Button skip = UiKit.ActionButton("取らない", UiKit.Muted);
        skip.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        skip.Pressed += () => { St.ChooseRelic(-1); Refresh(); };
        foot.AddChild(skip);
        Button all = UiKit.ActionButton("一覧（12 枚）", UiKit.Muted);
        all.Pressed += ShowRelicCatalog;
        foot.AddChild(all);
        box.AddChild(foot);
        _relicOfferPanel.Visible = true;
        _relicOfferPanel.MoveToFront();
    }

    /// <summary>レリックの一覧（第453期 §0-3）。12 枚の名前・1 行・両端。このマップで出た札・手持ち・持たせた札に印。</summary>
    private void ShowRelicCatalog()
    {
        if (_relicCatalogPanel is null)
        {
            _relicCatalogPanel = Panel();
            _relicCatalogPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
            _relicCatalogPanel.CustomMinimumSize = new Vector2(820, 0);
            _uiRoot.AddChild(_relicCatalogPanel);
        }
        foreach (Node child in _relicCatalogPanel.GetChildren()) { _relicCatalogPanel.RemoveChild(child); child.QueueFree(); }
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        _relicCatalogPanel.AddChild(box);
        box.AddChild(UiKit.Text("レリックの一覧（12 枚）—— 引き金 → 効果", 18, GoldColor));
        box.AddChild(UiKit.Text("「誰でも」の端はどの駒でも満たす。印: ★このマップで出た ／ ◆手持ち・持たせた", 12, UiKit.Muted));
        foreach (RelicInfo info in RelicCatalog.All)
        {
            TraitId r = info.Id;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            row.TooltipText = RelicHelp(r);
            row.MouseFilter = Control.MouseFilterEnum.Pass;
            HBoxContainer ends = RelicEndsChips(r, 11);
            ends.CustomMinimumSize = new Vector2(150, 0);
            row.AddChild(ends);
            string mark = (St.RelicHand.Contains(r) || St.RelicHolder(r) is not null ? "◆" : St.RelicShown.ContainsKey(r) ? "★" : "　");
            Label l = UiKit.Text($"{mark} {info.Name}（{info.Kind}）— {info.Text}", 13, St.RelicShown.ContainsKey(r) ? UiKit.Ink : UiKit.Muted);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            l.MouseFilter = Control.MouseFilterEnum.Ignore;
            row.AddChild(l);
            box.AddChild(row);
        }
        Button close = UiKit.ActionButton("閉じる", UiKit.Muted);
        close.Pressed += () => _relicCatalogPanel!.Visible = false;
        box.AddChild(close);
        _relicCatalogPanel.Visible = true;
        _relicCatalogPanel.MoveToFront();
    }

    /// <summary>手札の列の「レリック（n）」の段。手持ちの札は押すとつまむ・持たせた札は押すと外す（第453期 §0-4）。</summary>
    private void AddRelicRows()
    {
        if (!St.Stage.Relic.On) return;
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 6);
        Label title = UiKit.Text($"レリック（{St.RelicHand.Count}）——札を押してつまみ、駒を押すと持たせる", 13, GoldColor);
        title.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        title.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(title);
        Button all = UiKit.ActionButton("一覧", UiKit.Muted);
        all.Pressed += ShowRelicCatalog;
        head.AddChild(all);
        _benchList.AddChild(head);
        foreach (TraitId r in St.RelicHand.Distinct().ToList())
        {
            bool holding = _holdRelic == r;
            Button b = UiKit.ActionButton((holding ? "▶ " : "◆ ") + RelicLine(r), GoldColor);
            b.Alignment = HorizontalAlignment.Left;
            b.ClipText = true;
            b.TooltipText = RelicHelp(r);
            if (holding) b.Modulate = new Color(1.0f, 0.90f, 0.62f);
            b.Pressed += () =>
            {
                _holdRelic = _holdRelic == r ? null : r;
                if (_holdRelic is not null) { _holdBench = -1; _holdSlot = -1; _holdSquad = -1; ShowToast($"「{RelicCatalog.Info(r).Name}」をつまんだ——持たせる駒の札を押す", GoldColor, 3.0f); }
                Refresh();
            };
            _benchList.AddChild(b);
        }
        // 持たせた札（押すと外して手持ちに戻す）。
        foreach (RelicInfo info in RelicCatalog.All)
        {
            if (St.RelicHolder(info.Id) is not UnitState holder) continue;
            Button b = UiKit.ActionButton($"◆{info.Name} → {holder.Def.Name}（押すと外す）", UiKit.Muted);
            b.Alignment = HorizontalAlignment.Left;
            b.ClipText = true;
            b.TooltipText = RelicHelp(info.Id);
            b.Disabled = !St.CanEquip(holder);
            b.Pressed += () => { if (St.UnequipRelic(holder)) ShowToast($"{holder.Def.Name} から「{info.Name}」を外した", UiKit.Muted, 2.5f); Refresh(); };
            _benchList.AddChild(b);
        }
    }

    /// <summary>つまんでいるレリックを駒に持たせる（第453期 §0-4）。持たせられなければ理由を出す。つまみはどちらでも離す。</summary>
    private void EquipHeldRelic(UnitState? u, TraitId relic)
    {
        _holdRelic = null;
        if (u is null) { ShowToast("駒のいる札を押してください（つまみを離した）", UiKit.Muted, 3.0f); return; }
        if (!St.EquipRelic(u, relic)) { ShowToast($"{u.Def.Name} にはいま持たせられない（組み直しのできる場所の駒だけ）", EnemyColor, 3.0f); return; }
        ShowToast($"{u.Def.Name} に「{RelicCatalog.Info(relic).Name}」を持たせた", GoldColor, 3.0f);
    }

    /// <summary>選んだ駒の説明の上に、持っている札と持たせる／外すの札（組み直しのできる場所の駒だけ）。</summary>
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
