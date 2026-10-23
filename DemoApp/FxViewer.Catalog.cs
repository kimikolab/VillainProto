using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class FxViewer
{
    private readonly List<string> _unitIds = new();
    private readonly Dictionary<string, FxLivePlayback> _live = new();
    private readonly Dictionary<string, int[]> _highlights = new();
    private readonly Dictionary<string, string> _cueErrors = new();
    private readonly Dictionary<string, bool> _collapsedGroups = new();
    private readonly Dictionary<string, TreeItem> _cueItems = new();
    private bool _rebuildingList;

    private static string UnitName(string id) => UnitCatalog.Everyone.Concat(FxCatalog.EnemyUnits)
        .FirstOrDefault(d => d.Id == id)?.Name ?? id;

    private FxLivePlayback GetLive(FxCue cue)
    {
        if (_live.TryGetValue(cue.Id, out var playback)) return playback;
        playback = FxLivePlayback.Create(cue);
        _live[cue.Id] = playback;
        _highlights[cue.Id] = playback.Highlights(cue);
        if (_cueItems.TryGetValue(cue.Id, out var item)) item.SetText(1, $"見せ場 {_highlights[cue.Id].Length}");
        return playback;
    }

    private async Task PrepareHighlights()
    {
        int count = 0;
        foreach (var cue in _cues.Where(c => c.Kind == FxCueKind.Live))
        {
            try { GetLive(cue); }
            catch (Exception e)
            {
                _cueErrors[cue.Id] = e.Message;
                if (_cueItems.TryGetValue(cue.Id, out var item)) item.SetText(1, "再生不可");
                if (!cue.Generated) throw;
            }
            if (++count % 4 == 0)
            {
                _status.Text = $"実戦の見せ場を集計中… {count}";
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
        }
    }

    private int[] HighlightIndices(FxCue cue) => cue.Kind == FxCueKind.Live
        ? _highlights[cue.Id]
        : Enumerable.Range(0, _main.PlaybackEvents.Count)
            .Where(i => FxCatalog.IsVisibleEvent(_main.PlaybackEvents[i])).ToArray();

    private void SelectCurrentCue()
    {
        if (_cue is null || !_cueItems.TryGetValue(_cue.Id, out var item)) return;
        // 選び直すたびに一覧を作り直すと、長い一覧のスクロール位置が先頭へ戻ってしまう。
        _rebuildingList = true;
        try { item.Select(0); }
        finally { _rebuildingList = false; }
    }

    private void Filter()
    {
        if (_list is null) return;
        string? axis = _axis.Selected == 0 ? null : _axis.GetItemText(_axis.Selected);
        string? unit = _unit.Selected == 0 ? null : _unitIds[_unit.Selected - 1];
        string query = _search.Text.Trim();
        var filtered = _cues.Where(c => (axis is null || c.Axis == axis) && (unit is null || c.Units.Contains(unit))
            && (query.Length == 0 || $"{c.Title} {c.Id} {c.Axis} {c.Subject?.Title} {string.Join(" ", c.Units.Select(id => UnitName(id) + " " + id))}"
                .Contains(query, StringComparison.OrdinalIgnoreCase))).ToArray();
        _rebuildingList = true;
        try
        {
            _list.Clear(); _cueItems.Clear(); var root = _list.CreateItem();
            TreeItem Group(TreeItem parent, string title, string key, bool collapsed = false)
            {
                var item = _list.CreateItem(parent); item.SetText(0, title);
                item.SetMetadata(0, key); item.SetSelectable(0, false); item.SetSelectable(1, false);
                item.Collapsed = _collapsedGroups.GetValueOrDefault(key, collapsed);
                return item;
            }
            void Row(TreeItem parent, FxCue cue)
            {
                var item = _list.CreateItem(parent); item.SetText(0, cue.Title); item.SetMetadata(0, cue.Id);
                item.SetSelectable(1, false);
                item.SetTooltipText(0, cue.Id + (cue.Subject is { } s ? $" / {s.Definition.Id}" : ""));
                if (cue.Kind == FxCueKind.Live)
                    item.SetText(1, _highlights.TryGetValue(cue.Id, out var indices) ? $"見せ場 {indices.Length}"
                        : _cueErrors.ContainsKey(cue.Id) ? "再生不可" : "見せ場 …");
                _cueItems[cue.Id] = item;
                if (cue.Id == _cue?.Id) item.Select(0);
            }
            if (filtered.Any(c => c.Generated))
            {
                var catalog = Group(root, "駒カタログ", "@catalog");
                foreach (var group in filtered.Where(c => c.Generated).GroupBy(c => c.Subject!.Key))
                {
                    var subject = group.First().Subject!;
                    var section = Group(catalog, subject.Title, "@" + subject.Key, collapsed: true);
                    foreach (var cue in group) Row(section, cue);
                }
            }
            foreach (var group in filtered.Where(c => !c.Generated).GroupBy(c => c.Axis))
            {
                var section = Group(root, group.Key, "@axis:" + group.Key);
                foreach (var cue in group) Row(section, cue);
            }
        }
        finally { _rebuildingList = false; }
    }

    private async Task RepeatCue()
    {
        try { if (_cue is not null) await LoadCue(_cue); }
        catch (Exception e) { _loop.SetPressedNoSignal(false); Report(e); }
    }
}
