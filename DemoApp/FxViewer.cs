using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class FxViewer : Control
{
    private Main _main = null!;
    private Tree _list = null!;
    private OptionButton _axis = null!, _unit = null!, _speed = null!;
    private LineEdit _search = null!;
    private CheckButton _loop = null!, _mirror = null!;
    private Label _status = null!;
    private Button _pause = null!;
    private readonly List<FxCue> _cues = new();
    private readonly List<Button> _transport = new();
    private FxCue? _cue;
    private bool _changing;
    private int _request;
    private static readonly double[] Speeds = { .5, 1, 2, 4 };
    private bool _smoke;

    public override async void _Ready()
    {
        try
        {
            _smoke = OS.GetCmdlineUserArgs().Contains("--fx-smoke");
            foreach (var cue in FxCatalog.All)
            {
                if (!cue.Generated && cue.Kind == FxCueKind.Live && !FxCatalog.TryGetPreset(cue, out _))
                { GD.PushWarning($"FxViewer: プリセットが見つかりません: {cue.PresetName}（{cue.Id} は省略）"); continue; }
                _cues.Add(cue);
            }
            foreach (var error in FxCatalog.GeneratedErrors)
                GD.Print($"FX_VIEWER_SMOKE_SKIP {error.Key} {error.Value.Replace('\n', ' ')}");
            GD.Print($"FX_VIEWER_CATALOG registered={_cues.Count(c => !c.Generated)} generated={_cues.Count(c => c.Generated)} players={UnitCatalog.All.Count} enemies={FxCatalog.EnemyUnits.Count()}");
            BuildUi();
            Filter();
            await PrepareHighlights();
            if (_smoke) await Smoke();
            else if (OS.GetCmdlineUserArgs().Contains("--fx-generated-capture")) await CaptureGenerated();
            else if (OS.GetCmdlineUserArgs().Contains("--fx-capture")) await CaptureAll();
            else if (_cues.Count > 0) await LoadCue(_cues[0]);
            else _status.Text = "再生できる Cue がありません";
        }
        catch (Exception e) { Report(e); }
    }

    private void BuildUi()
    {
        Theme = new Theme { DefaultFont = new SystemFont {
            FontNames = new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP" }, AllowSystemFallback = true }, DefaultFontSize = 14 };
        var backdrop = new ColorRect { Color = Color.FromHtml("#0b1110"), MouseFilter = MouseFilterEnum.Ignore };
        backdrop.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(backdrop);
        var root = new HBoxContainer(); root.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(root);
        var sidebar = new VBoxContainer { CustomMinimumSize = new Vector2(376, 0) }; root.AddChild(sidebar);
        sidebar.AddChild(UiKit.Text("演出ビューア", 24, UiKit.Gold));
        sidebar.AddChild(UiKit.Text("一覧から選ぶと再生します", 13, UiKit.Muted));
        _axis = new OptionButton(); _axis.AddItem("軸：すべて");
        foreach (string axis in _cues.OrderBy(c => !c.Generated).Select(c => c.Axis).Distinct()) _axis.AddItem(axis);
        sidebar.AddChild(_axis); _axis.ItemSelected += _ => Filter();
        _unit = new OptionButton(); _unit.AddItem("駒：すべて");
        _unitIds.AddRange(_cues.SelectMany(c => c.Units).Distinct().OrderBy(id => id));
        foreach (string id in _unitIds) _unit.AddItem($"{UnitName(id)} ({id})");
        sidebar.AddChild(_unit); _unit.ItemSelected += _ => Filter();
        _search = new LineEdit { PlaceholderText = "検索：名前・ID・駒" }; sidebar.AddChild(_search);
        _search.TextChanged += _ => Filter();
        _list = new Tree { HideRoot = true, Columns = 2, SizeFlagsVertical = SizeFlags.ExpandFill };
        _list.SetColumnExpand(1, false);
        _list.SetColumnCustomMinimumWidth(1, 88);
        _list.ItemCollapsed += item => {
            if (!_rebuildingList) _collapsedGroups[item.GetMetadata(0).AsString()] = item.Collapsed;
        };
        sidebar.AddChild(_list);
        _list.ItemSelected += async () => {
            if (_rebuildingList) return;
            string id = _list.GetSelected().GetMetadata(0).AsString();
            if (_cues.FirstOrDefault(c => c.Id == id) is { } cue && cue.Id != _cue?.Id)
                try { await LoadCue(cue); } catch (Exception e) { Report(e); }
        };
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; root.AddChild(right);
        _main = new Main { ViewerMode = true, SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddChild(_main);
        // 一覧の分だけ狭い盤面でも端の駒まで収める。既存の視点調整と同じ窓口を使う。
        _main.ViewerField.SetCameraZoom(.85f);
        var controls = new HFlowContainer(); right.AddChild(controls);
        Button Action(string title, Func<Task> action)
        {
            var button = UiKit.ActionButton(title); controls.AddChild(button); _transport.Add(button);
            button.Pressed += async () => { try { await action(); } catch (Exception e) { Report(e); } };
            return button;
        }
        Action("▶ 再生", () => { _main.ResumePlayback(); return Task.CompletedTask; });
        _pause = Action("Ⅱ 一時停止", () => {
            if (_main.IsPaused) _main.ResumePlayback(); else _main.PausePlayback();
            return Task.CompletedTask;
        });
        Action("⏭ 次の見せ場", NextHighlight);
        _loop = new CheckButton { Text = "繰り返し" }; controls.AddChild(_loop);
        _speed = new OptionButton(); foreach (double speed in Speeds) _speed.AddItem($"×{speed:0.#}");
        _speed.Selected = 1; controls.AddChild(_speed);
        _speed.ItemSelected += i => _main.SetPlaybackSpeed(Speeds[(int)i]);
        _mirror = new CheckButton { Text = "左右反転" }; controls.AddChild(_mirror);
        _mirror.Toggled += async _ => {
            if (_cue is not null) try { await LoadCue(_cue); } catch (Exception e) { Report(e); }
        };
        Action("📷 撮影", async () => { if (_cue is not null) await Capture(_cue.Id); });
        _status = UiKit.Text("Cue を選んでください", 13, UiKit.Muted); right.AddChild(_status);
    }

    private async Task LoadCue(FxCue cue)
    {
        int request = ++_request;
        while (_changing) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (request != _request) return;
        _changing = true; SetTransport(false);
        try
        {
            await _main.StopViewerPlayback();
            if (request != _request) return;
            _cue = cue;
            _mirror.Disabled = cue.Kind == FxCueKind.Live;
            _main.SetPlaybackSpeed(_smoke ? 1000 : Speeds[_speed.Selected]);
            _main.ViewerFastPlayback = _smoke;
            _main.ViewerHoldSeconds = cue.HoldSeconds;
            if (cue.Kind == FxCueKind.Scripted)
                _main.EnterScripted(FxCatalog.GetOpenings(cue.Id, _mirror.ButtonPressed), cue.Events!(), cue.Title, cue.Stage);
            else _main.EnterViewerLive(GetLive(cue), cue);
            SelectCurrentCue();
        }
        finally { _changing = false; SetTransport(true); }
    }

    private void SetTransport(bool enabled)
    {
        foreach (var button in _transport) button.Disabled = !enabled;
        _speed.Disabled = !enabled;
    }

    private async Task NextHighlight()
    {
        if (_cue is null || _changing) return;
        _changing = true; SetTransport(false);
        try
        {
            var highlights = HighlightIndices(_cue);
            int next = highlights.FirstOrDefault(i => i >= _main.EventIndex, -1);
            if (next < 0 && _loop.ButtonPressed) next = highlights.FirstOrDefault(-1);
            if (next >= 0) { await _main.SeekTo(next); _main.ResumePlayback(); }
            else if (_main.EventIndex < _main.PlaybackEvents.Count)
                await _main.SeekTo(_main.PlaybackEvents.Count);
        }
        finally { _changing = false; SetTransport(true); }
    }

    public override void _Process(double delta)
    {
        if (_main is null || _cue is null || _changing) return;
        _pause.Text = _main.IsPaused ? "▶ 再開" : "Ⅱ 一時停止";
        _status.Text = $"Cue: {(_cue.Subject is { } subject ? subject.Title + " / " : "")}{_cue.Title}（{(_cue.Kind == FxCueKind.Live ? "実台本" : "手組み")}）  event {_main.EventIndex}/{_main.PlaybackEvents.Count}"
            + (_main.IsPaused ? " ・停止中" : !_main.IsPlaying ? " ・完了" : "");
        if (!_smoke && _loop.ButtonPressed && !_main.IsPlaying) _ = RepeatCue();
    }

    private async Task Capture(string id)
    {
        if (DisplayServer.GetName() == "headless") { _status.Text = "撮影には画面ありの起動が必要です"; return; }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string directory = ProjectSettings.GlobalizePath("res://../design/art/_viewer");
        Directory.CreateDirectory(directory);
        using var frame = GetViewport().GetTexture().GetImage();
        string path = Path.Combine(directory, id + ".png");
        if (frame.SavePng(path) != Error.Ok) throw new IOException($"撮影できませんでした: {path}");
        GD.Print($"FX_VIEWER_CAPTURE {path}");
    }

    private void Report(Exception e)
    {
        GD.PushError($"FX_VIEWER_FAILED {e}");
        if (_status is not null) _status.Text = $"再生エラー: {e.Message}";
        if (_smoke) GetTree().Quit(1);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_changing || _main is null || _cue is null || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (key.Keycode == Key.Space)
        {
            if (_main.IsPaused) _main.ResumePlayback(); else _main.PausePlayback();
        }
        else if (key.Keycode is >= Key.Key1 and <= Key.Key4)
        {
            int index = (int)(key.Keycode - Key.Key1);
            _speed.Selected = index; _main.SetPlaybackSpeed(Speeds[index]);
        }
        else return;
        GetViewport().SetInputAsHandled();
    }
}
