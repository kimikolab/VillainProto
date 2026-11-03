using BattleCore;
using Godot;
using System;
using System.Linq;

// =====================================================================================
// 戦闘側の編成画面のレリック（第454期 段0・指示書 design/PHASE454_HERO_SPEC.md §0）。<b>試遊だけの一時状態</b>——
// 席ごとに札を 1 枚（`Formation.SetRelic`・付け方は作戦マップと同じ `RelicCatalog.Attach`）。<b>`Presets` の行には記録しない</b>（出撃のときに組む `Formation` にだけ付ける）。
// 作法は作戦マップと同じ「つまむ → 席の駒を押す」（同じ札を持つ駒をもう一度押すと外す）。行を選び直すと全部外れる。
// 頭なしの確認: `--demo-relic=<席>:<札の TraitId>`（例 `--demo-relic=0:RelicFireArrow`）で出撃の前に付け、`DEMO_RELIC` の行を出す。
// =====================================================================================

public partial class Main
{
    private readonly TraitId?[] _seatRelics = new TraitId?[FormationRules.PlayableSlotCount];
    private TraitId? _armedRelic;
    private OptionButton? _relicPicker;
    private Label? _relicState;
    /// <summary>頭なしの確認の口で付ける札（行の選び直しで消えないよう、出撃のときに付ける）。</summary>
    private readonly System.Collections.Generic.List<(int Slot, TraitId Relic)> _demoRelics = new();

    /// <summary>名簿の上に置く「レリック（試遊だけ）」の段。</summary>
    private Control BuildRelicStrip()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(UiKit.Text("レリック（試遊だけ・行には記録しない）——選んで席の駒を押す", 11, UiKit.Gold));
        _relicPicker = new OptionButton { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 32) };
        _relicPicker.AddItem("（つままない）", 0);
        for (int i = 0; i < RelicCatalog.All.Count; i++)
        {
            RelicInfo r = RelicCatalog.All[i];
            string ends = RelicBridges.Of(r.Id) is RelicEnds e ? $"  {RelicBridges.Label(e.Trigger)}→{RelicBridges.Label(e.Effect)}" : "";
            _relicPicker.AddItem($"◆{r.Name}{ends}", i + 1);
            _relicPicker.SetItemTooltip(i + 1, $"{r.Name}（{r.Kind}）\n{r.Text}\n{r.Bridge}");
        }
        _relicPicker.ItemSelected += index =>
        {
            _armedRelic = index <= 0 ? null : RelicCatalog.All[(int)index - 1].Id;
            if (_armedRelic is TraitId a) Notice($"「{RelicCatalog.Info(a).Name}」をつまんだ——持たせる席の駒を押す（同じ札の駒をもう一度押すと外す）");
        };
        box.AddChild(_relicPicker);
        _relicState = UiKit.Text("", 10, UiKit.Muted);
        _relicState.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_relicState);
        var clear = UiKit.ActionButton("レリックを全部外す", UiKit.Muted);
        clear.Pressed += () => { ClearRelics(); Notice("レリックを全部外しました"); };
        box.AddChild(clear);

        // 頭なしの確認の口（`--demo-relic=0:RelicFireArrow`・複数は `,` で区切らず引数を並べる）。
        foreach (string arg in OS.GetCmdlineUserArgs().Where(a => a.StartsWith("--demo-relic=", StringComparison.Ordinal)))
        {
            string[] p = arg["--demo-relic=".Length..].Split(':');
            if (p.Length == 2 && int.TryParse(p[0], out int slot) && slot >= 0 && slot < _seatRelics.Length
                && Enum.TryParse(p[1], out TraitId id) && RelicCatalog.IsRelic(id))
                _demoRelics.Add((slot, id));
        }
        RefreshRelicState();
        return box;
    }

    /// <summary>席を押した（札をつまんでいれば持たせる・同じ札の駒なら外す）。つまんでいなければ偽（ふだんの席の操作へ）。</summary>
    private bool TryPlaceRelic(int slot)
    {
        if (_armedRelic is not TraitId r) return false;
        if (slot < 0 || slot >= _seatRelics.Length || _formation[slot] is null) { Notice("駒のいる席を押してください"); return true; }
        if (_seatRelics[slot] == r) { _seatRelics[slot] = null; Notice($"{SeatLabel(slot)} から「{RelicCatalog.Info(r).Name}」を外しました"); }
        else { _seatRelics[slot] = r; Notice($"{SeatLabel(slot)} の {_formation[slot]!.Name} に「{RelicCatalog.Info(r).Name}」を持たせました（試遊だけ）"); }
        _armedRelic = null;
        if (_relicPicker is not null) _relicPicker.Selected = 0;
        RefreshRelicState();
        return true;
    }

    private void ClearRelics()
    {
        Array.Clear(_seatRelics);
        _armedRelic = null;
        if (_relicPicker is not null) _relicPicker.Selected = 0;
        RefreshRelicState();
    }

    private void RefreshRelicState()
    {
        if (_relicState is null) return;
        var held = Enumerable.Range(0, _seatRelics.Length).Where(i => _seatRelics[i] is not null)
            .Select(i => $"{SeatLabel(i)} ◆{RelicCatalog.Info(_seatRelics[i]!.Value).Name}").ToList();
        _relicState.Text = held.Count == 0 ? "（持たせている札なし）" : string.Join(" ／ ", held);
    }

    /// <summary>出撃のときに組んだ編成に札を付ける（行の <c>Formation</c> は触らない）。付けたら確かめの行を出す。</summary>
    private void ApplyRelics(Formation formation)
    {
        foreach (var (slot, relic) in _demoRelics) if (formation[slot] is not null) _seatRelics[slot] = relic;
        int n = 0;
        for (int i = 0; i < _seatRelics.Length; i++)
            if (_seatRelics[i] is TraitId r && formation[i] is not null) { formation.SetRelic(i, r); n++; }
        if (n == 0) return;
        var probe = BattleEngine.Materialize(formation, BattleContext.PlayerTeam);   // 確かめるだけ（この駒は戦闘に使わない）
        var rows = PresetRows();
        bool rowClean = _presetIndex < 0 || _presetIndex >= rows.Length
                        || Enumerable.Range(0, FormationRules.PlayableSlotCount).All(i => rows[_presetIndex].F.RelicAt(i) is null);
        GD.Print("DEMO_RELIC " + string.Join(" ", probe.Where(u => u.Relic is not null)
                     .Select(u => $"{SeatLabel(u.Slot)}:{u.Def.Name}:{u.Relic}:tail={u.Traits.LastOrDefault()?.Id}"))
                 + $" rowClean={rowClean}");
    }
}
