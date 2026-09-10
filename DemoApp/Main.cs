using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

public partial class Main : Control
{
    private readonly UnitDef?[] _formation = new UnitDef?[FormationRules.PlayableSlotCount];
    private readonly Dictionary<string, RosterCard> _rosterCards = new(StringComparer.Ordinal);
    private readonly Dictionary<int, string> _statusByPawn = new();
    private readonly Dictionary<int, DemoOpening> _openingById = new();
    private readonly Dictionary<int, string> _statusCauseByDamageIndex = new();
    private readonly HashSet<int> _linkedStatusEventIndices = new();

    /// <summary>
    /// 同時着弾（第124期 3-a）で<b>攻撃の側がまとめて描いた</b> <c>Damage</c> の添字。
    /// 本体のループがここへ来たら<b>もう一度描かない</b>。
    /// </summary>
    private readonly HashSet<int> _batchedDamageIndices = new();

    private Texture2D _atlas = null!;
    private BattlefieldView _field = null!;
    private BattlefieldView3D _battleField = null!;
    private PanelContainer _rosterPanel = null!;
    private PanelContainer _detailPanel = null!;
    private PanelContainer _battlePanel = null!;
    private VBoxContainer _rosterList = null!;
    private LineEdit _search = null!;
    private RichTextLabel _detail = null!;
    private RichTextLabel _battleLog = null!;
    private Label _count = null!;
    private Label _notice = null!;
    private Label _battleSummary = null!;
    private OptionButton _stagePicker = null!;
    private OptionButton _presetPicker = null!;
    private Label _presetState = null!;
    private SpinBox _seed = null!;
    private HBoxContainer _setupActions = null!;
    private HBoxContainer _battleActions = null!;
    private Button _deploy = null!;
    private Button _pause = null!;
    private Button _back = null!;
    private Button _replay = null!;
    private Button _campaignSetup = null!;
    private Button _campaignBattle = null!;
    private Button _score = null!;
    private OptionButton _speedPicker = null!;
    private ScorePanel _scorePanel = null!;

    /// <summary>
    /// 編成プリセット（第123期）。<b><c>Presets</c> を直に引くだけで写しを持たない</b>
    /// ——<c>GodotApp/Main.cs</c> の <c>Builds()</c> と同じ引き方（第97期 D6）。
    /// <b>表示名は行名そのもの</b>で、番号も帯名も前置しない
    /// （<c>docs/watch.md</c> の行名と完全一致させるため。第123期 §6 C5）。
    /// </summary>
    private static (string Name, Formation F)[]? _presetRows;

    private static (string Name, Formation F)[] PresetRows() =>
        _presetRows ??= Presets.Compare.Concat(Presets.Cross).ToArray();

    private int _presetIndex;
    private bool _presetDirty;

    private UnitDef? _inspected;
    private string? _armedUnitId;
    private BattleResult? _result;
    private List<DemoOpening> _battleOpening = new();
    private int _eventIndex;
    private int _playToken;
    private bool _battleMode;
    private bool _playing;
    private bool _paused;
    private double _speed = 2.0;
    private bool _quitAfterPlayback;
    private bool _fastSmoke;
    private bool _campaignFlowSmoke;

    private sealed record PendingOpening(
        UnitState Unit,
        int Slot,
        int Hp,
        int MaxHp,
        int Attack,
        AttackPattern Pattern);

    public override void _Ready()
    {
        Theme = new Theme
        {
            DefaultFont = new SystemFont
            {
                FontNames = new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "Segoe UI" },
                AllowSystemFallback = true,
            },
            DefaultFontSize = 14,
        };

        _atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");
        BuildUi();
        BuildRoster();

        _field.SetupSlotClicked = OnSetupSlotClicked;
        _field.SetupSlotRemoveRequested = RemoveSlot;
        _field.SetupUnitDropped = DropUnit;

        string[] userArgs = OS.GetCmdlineUserArgs();
        if (CampaignSession.HasPendingEncounter)
            _stagePicker.Selected = CampaignSession.PendingStageIndex;
        string? stageArg = userArgs.FirstOrDefault(arg => arg.StartsWith("--demo-stage=", StringComparison.Ordinal));
        if (stageArg is not null
            && int.TryParse(stageArg["--demo-stage=".Length..], out int requestedStage)
            && requestedStage >= 0
            && requestedStage < EnemyCatalog.Stages.Count)
            _stagePicker.Selected = requestedStage;

        // 行と seed をコマンドラインからも指定できるようにする（第124期 段1）。
        // **`docs/watch.md` の推奨12戦をそのまま再現するため**——行名は部分一致で、
        // `Presets.Compare` ＋ `Presets.Cross` の並び（＝プリセットの一覧）から先頭の一致を採る。
        // 画面から選ぶ手順（README）は今までどおり。
        string? presetArg = userArgs.FirstOrDefault(arg => arg.StartsWith("--demo-preset=", StringComparison.Ordinal));
        if (presetArg is not null)
        {
            string want = presetArg["--demo-preset=".Length..];
            var rows = PresetRows();
            int hit = Array.FindIndex(rows, r => r.Name.Contains(want, StringComparison.Ordinal));
            if (hit >= 0) _presetIndex = hit;
            GD.Print($"DEMO_PRESET query=\"{want}\" index={hit} name={(hit >= 0 ? rows[hit].Name : "(no match)")}");
        }
        string? seedArg = userArgs.FirstOrDefault(arg => arg.StartsWith("--demo-seed=", StringComparison.Ordinal));
        if (seedArg is not null && int.TryParse(seedArg["--demo-seed=".Length..], out int wantSeed))
            _seed.Value = wantSeed;

        AutoFormation();
        UpdateStageHeader();
        ShowEnemyDetails();

        // 画面検証用。通常起動では使われず、`-- --demo-autoplay` を付けたときだけ出撃する。
        _quitAfterPlayback = userArgs.Contains("--demo-quit", StringComparer.Ordinal);
        _fastSmoke = userArgs.Contains("--demo-fast", StringComparer.Ordinal);
        _campaignFlowSmoke = userArgs.Contains("--campaign-flow-smoke", StringComparer.Ordinal);
        if (_fastSmoke) _speed = 1000.0;
        string? captureArg = userArgs.FirstOrDefault(arg => arg.StartsWith("--demo-capture-dir=", StringComparison.Ordinal));
        string? captureDirectory = captureArg?["--demo-capture-dir=".Length..];
        if (userArgs.Contains("--demo-autoplay", StringComparer.Ordinal))
            _ = AutoplayForCapture(captureDirectory);
    }

    private async Task AutoplayForCapture(string? captureDirectory)
    {
        await Delay(0.2, raw: true);
        StartBattle();
        if (string.IsNullOrWhiteSpace(captureDirectory)) return;
        Directory.CreateDirectory(captureDirectory);
        await Delay(0.65, raw: true);
        for (int i = 0; i < 6; i++)
        {
            Image frame = GetViewport().GetTexture().GetImage();
            string path = Path.Combine(captureDirectory, $"battle-2_5d-{i + 1:00}.png");
            Error error = frame.SavePng(path);
            GD.Print($"DEMO_CAPTURE_FRAME index={i + 1} error={error} path={path}");
            await Delay(0.48, raw: true);
        }
        GD.Print("DEMO_CAPTURE_COMPLETE");
        GetTree().Quit();
    }

    private void BuildUi()
    {
        var page = new ColorRect { Color = Color.FromHtml("#0b1110") };
        page.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(page);

        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        root.AddThemeConstantOverride("separation", 0);
        AddChild(root);

        root.AddChild(BuildHeader());

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 0);
        root.AddChild(body);

        _rosterPanel = BuildRosterPanel();
        body.AddChild(_rosterPanel);

        var fieldFrame = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        fieldFrame.AddThemeConstantOverride("margin_left", 8);
        fieldFrame.AddThemeConstantOverride("margin_right", 8);
        fieldFrame.AddThemeConstantOverride("margin_top", 8);
        fieldFrame.AddThemeConstantOverride("margin_bottom", 8);
        var fieldStack = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _field = new BattlefieldView { Visible = true };
        _field.SetAnchorsPreset(LayoutPreset.FullRect);
        fieldStack.AddChild(_field);
        _battleField = new BattlefieldView3D { Visible = false };
        _battleField.SetAnchorsPreset(LayoutPreset.FullRect);
        fieldStack.AddChild(_battleField);
        // 戦績パネル（第124期 段1）は盤面の上に重ねる。**戦況ログを潰さない**（§4-2）。
        _scorePanel = new ScorePanel();
        _scorePanel.SetAnchorsPreset(LayoutPreset.FullRect);
        fieldStack.AddChild(_scorePanel);
        fieldFrame.AddChild(fieldStack);
        body.AddChild(fieldFrame);

        _detailPanel = BuildDetailPanel();
        body.AddChild(_detailPanel);
        _battlePanel = BuildBattlePanel();
        _battlePanel.Visible = false;
        body.AddChild(_battlePanel);

        root.AddChild(BuildFooter());
    }

    private Control BuildHeader()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 74) };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.055f, 0.085f, 0.075f, 1), UiKit.Line, 0, 0));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        panel.AddChild(row);

        var brand = new VBoxContainer { CustomMinimumSize = new Vector2(210, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        brand.AddThemeConstantOverride("separation", 0);
        brand.AddChild(UiKit.Text("VILLAIN PROTO", 11, UiKit.Gold));
        brand.AddChild(UiKit.Text("追放者たちの戦場", 24, Colors.White));
        row.AddChild(brand);

        // 編成プリセット（第123期）。`docs/watch.md` が「この行のこの波のこの seed」を指すので、
        // **任意の行をそのままの席で再現できる**必要がある。
        var presetBox = new VBoxContainer();
        presetBox.AddThemeConstantOverride("separation", 0);
        presetBox.AddChild(HeaderCaption("編成プリセット"));
        _presetState = UiKit.Text("", 10, UiKit.Muted);
        presetBox.AddChild(_presetState);
        row.AddChild(presetBox);

        var rows = PresetRows();
        _presetPicker = new OptionButton { CustomMinimumSize = new Vector2(300, 40), FocusMode = FocusModeEnum.None };
        _presetPicker.AddThemeFontSizeOverride("font_size", 12);
        for (int i = 0; i < rows.Length; i++) _presetPicker.AddItem(rows[i].Name, i);
        _presetPicker.ItemSelected += index => ApplyPreset((int)index);
        row.AddChild(_presetPicker);

        row.AddChild(HeaderCaption("敵ウェーブ"));
        _stagePicker = new OptionButton { CustomMinimumSize = new Vector2(230, 40), FocusMode = FocusModeEnum.None };
        for (int i = 0; i < EnemyCatalog.Stages.Count; i++) _stagePicker.AddItem(EnemyCatalog.Stages[i].Name, i);
        _stagePicker.ItemSelected += index =>
        {
            UpdateStageHeader();
            if (!_battleMode) ShowEnemyDetails();
        };
        row.AddChild(_stagePicker);

        row.AddChild(HeaderCaption("戦闘 seed"));
        _seed = new SpinBox
        {
            MinValue = 0,
            MaxValue = 9999,
            Step = 1,
            Value = 7,
            CustomMinimumSize = new Vector2(105, 40),
        };
        row.AddChild(_seed);

        var badge = new PanelContainer { CustomMinimumSize = new Vector2(132, 40) };
        badge.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(UiKit.Player, 0.12f), UiKit.Player, 1, 18));
        var badgeText = UiKit.Text("ACTUAL CORE", 10, UiKit.Player.Lightened(0.22f));
        badgeText.HorizontalAlignment = HorizontalAlignment.Center;
        badgeText.VerticalAlignment = VerticalAlignment.Center;
        badge.AddChild(badgeText);
        row.AddChild(badge);
        return panel;
    }

    private static Label HeaderCaption(string value)
    {
        var label = UiKit.Text(value, 10, UiKit.Faint);
        label.VerticalAlignment = VerticalAlignment.Center;
        return label;
    }

    private PanelContainer BuildRosterPanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(322, 0) };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.045f, 0.07f, 0.06f, 1), UiKit.Line, 0, 0));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        panel.AddChild(col);

        var titleRow = new HBoxContainer();
        var title = UiKit.Text("ロスター", 17, Colors.White);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        titleRow.AddChild(title);
        titleRow.AddChild(UiKit.Text($"{UnitCatalog.All.Count}体", 11, UiKit.Faint));
        col.AddChild(titleRow);

        _search = new LineEdit
        {
            PlaceholderText = "名前・能力で検索",
            ClearButtonEnabled = true,
            CustomMinimumSize = new Vector2(0, 38),
        };
        _search.TextChanged += FilterRoster;
        col.AddChild(_search);

        var hint = UiKit.Text("クリックで空席へ / ドラッグで席を指定", 10, UiKit.Muted);
        col.AddChild(hint);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _rosterList = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _rosterList.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_rosterList);
        col.AddChild(scroll);
        return panel;
    }

    private PanelContainer BuildDetailPanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(292, 0) };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.045f, 0.07f, 0.06f, 1), UiKit.Line, 0, 0));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        panel.AddChild(col);

        var header = new HBoxContainer();
        var title = UiKit.Text("作戦情報", 17, Colors.White);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        header.AddChild(title);
        var enemy = UiKit.ActionButton("敵を見る", UiKit.Enemy);
        enemy.Pressed += ShowEnemyDetails;
        header.AddChild(enemy);
        col.AddChild(header);

        _detail = new RichTextLabel
        {
            BbcodeEnabled = true,
            FitContent = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ScrollActive = true,
            SelectionEnabled = true,
        };
        col.AddChild(_detail);
        return panel;
    }

    private PanelContainer BuildBattlePanel()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(302, 0) };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.035f, 0.055f, 0.05f, 1), UiKit.Line, 0, 0));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        panel.AddChild(col);
        col.AddChild(UiKit.Text("戦況ログ", 17, Colors.White));
        _battleSummary = UiKit.Text("", 11, UiKit.Muted);
        _battleSummary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(_battleSummary);
        var legend = UiKit.Text(
            "攻/薙/貫/全＝攻撃の型  ・  特＝特性が直に削った  ・  毒/燃＝継続ダメージ\n"
            + "巻込＝味方の刃（足元が紫の輪）  ・  反撃＝手番外の攻撃  ・  肩代＝肩代わりの中継\n"
            + "細い線＝誰の仕業か（回復・蘇生・召喚・移動・見せ場・状態）  ・  戦績 (T) で数字",
            10, UiKit.Faint);
        legend.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(legend);
        col.AddChild(new HSeparator());
        _battleLog = new RichTextLabel
        {
            BbcodeEnabled = true,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            ScrollFollowing = true,
            SelectionEnabled = true,
        };
        col.AddChild(_battleLog);
        return panel;
    }

    private Control BuildFooter()
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 72) };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.055f, 0.085f, 0.075f, 1), UiKit.Line, 0, 0));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        panel.AddChild(row);

        _notice = UiKit.Text("", 11, UiKit.Muted);
        _notice.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _notice.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(_notice);

        _setupActions = new HBoxContainer();
        _setupActions.AddThemeConstantOverride("separation", 8);
        _count = UiKit.Text("0 / 5", 16, UiKit.Gold);
        _count.VerticalAlignment = VerticalAlignment.Center;
        _setupActions.AddChild(_count);
        var auto = UiKit.ActionButton("プリセットを再配置", UiKit.Player);
        auto.Pressed += AutoFormation;
        _setupActions.AddChild(auto);
        var clear = UiKit.ActionButton("全解除");
        clear.Pressed += ClearFormation;
        _setupActions.AddChild(clear);
        _campaignSetup = UiKit.ActionButton("作戦マップへ");
        _campaignSetup.Pressed += GoToCampaign;
        _setupActions.AddChild(_campaignSetup);
        _deploy = UiKit.ActionButton("この配置で出撃", UiKit.Gold);
        _deploy.CustomMinimumSize = new Vector2(180, 46);
        _deploy.Pressed += StartBattle;
        _setupActions.AddChild(_deploy);
        row.AddChild(_setupActions);

        _battleActions = new HBoxContainer { Visible = false };
        _battleActions.AddThemeConstantOverride("separation", 8);
        _back = UiKit.ActionButton("編成に戻る");
        _back.Pressed += ReturnToFormation;
        _battleActions.AddChild(_back);
        _pause = UiKit.ActionButton("一時停止", UiKit.Player);
        _pause.Pressed += TogglePause;
        _battleActions.AddChild(_pause);
        _replay = UiKit.ActionButton("最初から", UiKit.Gold);
        _replay.Pressed += ReplayBattle;
        _battleActions.AddChild(_replay);
        _campaignBattle = UiKit.ActionButton("作戦マップへ", UiKit.Player);
        _campaignBattle.Pressed += GoToCampaign;
        _battleActions.AddChild(_campaignBattle);
        // 戦績（第124期 段1）。**戦闘中も開ける**（§4-2）。キーは T。
        _score = UiKit.ActionButton("戦績 (T)", UiKit.Gold);
        _score.Pressed += ToggleScore;
        _battleActions.AddChild(_score);
        _speedPicker = new OptionButton { CustomMinimumSize = new Vector2(112, 42), FocusMode = FocusModeEnum.None };
        foreach ((string label, int id) in new[] { ("×0.5", 0), ("×1", 1), ("×2", 2), ("×4", 3) })
            _speedPicker.AddItem(label, id);
        _speedPicker.Selected = 2;
        _speedPicker.ItemSelected += i => SetSpeed((int)i);
        _battleActions.AddChild(_speedPicker);
        row.AddChild(_battleActions);
        return panel;
    }

    private void BuildRoster()
    {
        foreach (UnitDef def in UnitCatalog.All.OrderByDescending(u => u.Speed).ThenBy(u => u.Name, StringComparer.Ordinal))
        {
            var card = new RosterCard();
            card.Configure(def, _atlas);
            card.Chosen = ToggleUnit;
            card.Inspected = ShowUnitDetails;
            _rosterList.AddChild(card);
            _rosterCards[def.Id] = card;
        }
    }

    private void FilterRoster(string query)
    {
        string needle = query.Trim();
        foreach (RosterCard card in _rosterCards.Values)
        {
            UnitDef def = card.Definition!;
            card.Visible = needle.Length == 0
                || def.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || def.PlusText.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || def.MinusText.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || UiKit.PatternLabel(def.Pattern).Contains(needle, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void ToggleUnit(UnitDef def)
    {
        if (_battleMode) return;
        _armedUnitId = def.Id;
        int existing = Array.FindIndex(_formation, u => u?.Id == def.Id);
        if (existing >= 0)
        {
            _formation[existing] = null;
            Notice($"{def.Name} を編成から外しました");
        }
        else
        {
            int empty = Array.FindIndex(_formation, u => u is null);
            if (empty < 0)
            {
                Notice("編成は5体までです。席の駒を右クリックして外すか、上へドラッグして交換してください。", UiKit.Hurt);
                return;
            }
            _formation[empty] = def;
            Notice($"{def.Name} を {FormationRules.SeatNames[empty]} に配置しました");
        }
        RefreshFormation();
    }

    private void DropUnit(int slot, string unitId)
    {
        if (_battleMode) return;
        UnitDef? incoming = UnitCatalog.All.FirstOrDefault(u => u.Id == unitId);
        if (incoming is null) return;
        int source = Array.FindIndex(_formation, u => u?.Id == unitId);
        UnitDef? displaced = _formation[slot];
        if (source >= 0 && source != slot)
        {
            _formation[source] = displaced;
            _formation[slot] = incoming;
            Notice(displaced is null
                ? $"{incoming.Name} を {FormationRules.SeatNames[slot]} へ移動しました"
                : $"{FormationRules.SeatNames[source]} と {FormationRules.SeatNames[slot]} を入れ替えました");
        }
        else if (source < 0)
        {
            _formation[slot] = incoming;
            Notice(displaced is null
                ? $"{incoming.Name} を {FormationRules.SeatNames[slot]} に配置しました"
                : $"{displaced.Name} と交代して {incoming.Name} を配置しました");
        }
        _armedUnitId = incoming.Id;
        MarkPresetDirty();
        ShowUnitDetails(incoming);
        RefreshFormation();
    }

    private void OnSetupSlotClicked(int slot)
    {
        if (_battleMode) return;
        if (_formation[slot] is { } current)
        {
            _armedUnitId = current.Id;
            ShowUnitDetails(current);
            Notice($"{current.Name}: ドラッグで別の席へ移動、右クリックで外せます");
            return;
        }
        if (_armedUnitId is null) return;
        UnitDef? armed = UnitCatalog.All.FirstOrDefault(u => u.Id == _armedUnitId);
        if (armed is not null) DropUnit(slot, armed.Id);
    }

    private void RemoveSlot(int slot)
    {
        if (_battleMode || _formation[slot] is not { } def) return;
        _formation[slot] = null;
        MarkPresetDirty();
        Notice($"{def.Name} を編成から外しました");
        RefreshFormation();
    }

    private void ClearFormation()
    {
        Array.Fill(_formation, null);
        _armedUnitId = null;
        MarkPresetDirty();
        Notice("編成を空にしました");
        RefreshFormation();
    }

    private void AutoFormation() => ApplyPreset(_presetIndex);

    /// <summary>
    /// プリセットの行を<b>その席のまま</b>盤面に置く（第123期）。
    /// ドラッグでの組み替えはそのまま残り、組み替えた時点で「変更あり」に変わる。
    /// </summary>
    private void ApplyPreset(int index)
    {
        if (_battleMode) return;
        var rows = PresetRows();
        _presetIndex = Math.Clamp(index, 0, rows.Length - 1);
        var preset = rows[_presetIndex];
        for (int i = 0; i < _formation.Length; i++) _formation[i] = preset.F[i];
        _presetDirty = false;
        if (_presetPicker.Selected != _presetIndex) _presetPicker.Selected = _presetIndex;
        _armedUnitId = _formation.FirstOrDefault()?.Id;
        Notice($"プリセット「{preset.Name}」を席ごと配置しました");
        RefreshFormation();
        if (_formation.FirstOrDefault() is { } first) ShowUnitDetails(first);
    }

    private void MarkPresetDirty()
    {
        _presetDirty = true;
        UpdatePresetState();
    }

    private void UpdatePresetState()
    {
        if (_presetState is null) return;
        var rows = PresetRows();
        string band = _presetIndex < Presets.Compare.Length
            ? $"compare {_presetIndex + 1}/{Presets.Compare.Length}"
            : $"交差帯 {_presetIndex - Presets.Compare.Length + 1}/{Presets.Cross.Length}";
        _presetState.Text = _presetDirty
            ? $"{band}・プリセットから変更あり"
            : $"{band}・プリセット通り（全 {rows.Length} 行）";
        _presetState.AddThemeColorOverride("font_color", _presetDirty ? UiKit.Gold : UiKit.Muted);
    }

    private void RefreshFormation()
    {
        _field.UpdateFormation(_formation);
        int count = _formation.Count(u => u is not null);
        _count.Text = $"{count} / {FormationRules.PlayableSlotCount}";
        _count.AddThemeColorOverride("font_color", count == FormationRules.PlayableSlotCount ? UiKit.Heal : UiKit.Gold);
        _deploy.Disabled = count != FormationRules.PlayableSlotCount;
        UpdatePresetState();
        foreach ((string id, RosterCard card) in _rosterCards)
            card.SetSelected(_formation.Any(u => u?.Id == id));
    }

    private void ShowUnitDetails(UnitDef def)
    {
        _inspected = def;
        _detail.Text =
            $"[color=#efc66a][font_size=11]UNIT DOSSIER[/font_size][/color]\n" +
            $"[font_size=24][b]{def.Name}[/b][/font_size]\n" +
            $"[color=#a9b3a8]HP {def.MaxHp}   攻撃 {def.Attack}   速度 {def.Speed}   {UiKit.PatternLabel(def.Pattern)}[/color]\n\n" +
            $"[color=#71d7a1][b]＋ 強み[/b][/color]\n{def.PlusText}\n\n" +
            $"[color=#ff766b][b]− 欠点[/b][/color]\n{def.MinusText}\n\n" +
            $"[color=#6f7f76][i]{def.Flavor}[/i][/color]\n\n" +
            $"[color=#a9b3a8]配置: {PlacementOf(def)}[/color]";
    }

    private string PlacementOf(UnitDef def)
    {
        int slot = Array.FindIndex(_formation, u => u?.Id == def.Id);
        return slot < 0 ? "未編成" : FormationRules.SeatNames[slot];
    }

    private void ShowEnemyDetails()
    {
        EnemyCatalog.Stage stage = EnemyCatalog.Stages[_stagePicker.Selected];
        string rows = string.Join("\n", stage.Enemy.Occupied().Select(x =>
            $"[color=#ee7962]◆[/color] [b]{FormationRules.SeatNames[x.Slot]}[/b]  {x.Def.Name}\n" +
            $"   [color=#a9b3a8]HP {x.Def.MaxHp} / 攻 {x.Def.Attack} / 速 {x.Def.Speed} / {UiKit.PatternLabel(x.Def.Pattern)}[/color]"));
        _detail.Text =
            $"[color=#ee7962][font_size=11]ENEMY WAVE {_stagePicker.Selected + 1}[/font_size][/color]\n" +
            $"[font_size=22][b]{stage.Name}[/b][/font_size]\n\n{rows}\n\n" +
            "[color=#6f7f76]敵の編成と戦闘ルールは、元の GodotApp が参照する EnemyCatalog と同じです。[/color]";
    }

    private void UpdateStageHeader()
    {
        if (_field is null) return;
        _field.SetSetupHeader(EnemyCatalog.Stages[_stagePicker.Selected].Name);
    }

    private void Notice(string value, Color? color = null)
    {
        _notice.Text = value;
        _notice.AddThemeColorOverride("font_color", color ?? UiKit.Muted);
    }

    private void StartBattle()
    {
        if (_battleMode || _formation.Any(u => u is null)) return;

        var formation = new Formation();
        for (int i = 0; i < _formation.Length; i++) formation[i] = _formation[i];
        int stageIndex = _stagePicker.Selected;
        int seed = (int)_seed.Value;

        List<UnitState> players = BattleEngine.Materialize(formation, BattleContext.PlayerTeam);
        List<UnitState> enemies = BattleEngine.Materialize(EnemyCatalog.Stages[stageIndex].Enemy, BattleContext.EnemyTeam);
        List<PendingOpening> pending = players.Concat(enemies)
            .Select(u => new PendingOpening(u, u.Slot, u.Hp, u.MaxHp, u.CurrentAttack, u.CurrentPattern))
            .ToList();

        _result = BattleEngine.Run(players, enemies, seed, verbose: true);
        IndexStatusDamageEvents(_result.Events);
        _battleOpening = pending.Select(x => new DemoOpening(
            x.Unit.InstanceId,
            x.Unit.TeamId,
            x.Unit.Def.Id,
            x.Unit.Def.Name,
            x.Slot,
            x.Hp,
            x.MaxHp,
            x.Attack,
            x.Pattern)).ToList();

        _openingById.Clear();
        foreach (DemoOpening opening in _battleOpening) _openingById[opening.InstanceId] = opening;

        _battleMode = true;
        _rosterPanel.Visible = false;
        _detailPanel.Visible = false;
        _battlePanel.Visible = true;
        _setupActions.Visible = false;
        _battleActions.Visible = true;
        _stagePicker.Disabled = true;
        _presetPicker.Disabled = true;
        _seed.Editable = false;
        _battleSummary.Text = $"{EnemyCatalog.Stages[stageIndex].Name}\nseed {seed} ・ 予測済み台本 {_result.Events.Count}イベント";
        _battleLog.Clear();
        _field.Visible = false;
        _battleField.Visible = true;
        _battleField.BeginBattle(_battleOpening, EnemyCatalog.Stages[stageIndex].Name);
        SetScoreVisible(false);
        _eventIndex = 0;
        _statusByPawn.Clear();
        _batchedDamageIndices.Clear();
        Notice("BattleCore が計算したイベント列を再生中", UiKit.Player);
        BeginPlayback();
    }

    private async void BeginPlayback()
    {
        if (_result is null) return;
        int token = ++_playToken;
        _playing = true;
        _paused = false;
        _pause.Text = "一時停止";
        _battleField.ShowBanner("BATTLE START", UiKit.Gold, 0.8);
        await Delay(0.62);

        while (token == _playToken && _battleMode && _eventIndex < _result.Events.Count)
        {
            while (_paused && token == _playToken && _battleMode) await Delay(0.06, raw: true);
            if (token != _playToken || !_battleMode) return;
            int eventIndex = _eventIndex++;
            BattleEvent e = _result.Events[eventIndex];
            await ApplyEvent(e, eventIndex);
        }

        if (token != _playToken || !_battleMode) return;
        _playing = false;
        string verdict = _result.PlayerWon ? "VICTORY" : "DEFEAT";
        Color color = _result.PlayerWon ? UiKit.Heal : UiKit.Hurt;
        if (_result.PlayerWon) _battleField.ShowVictoryPortraits();
        _battleField.ShowBanner(verdict, color, 2.2);
        _battleField.SetSubline($"{(_result.PlayerWon ? "勝利" : "敗北")} ・ {_result.Turns}ターン ・ 生存 {_result.PlayerSurvivors}体 ・ 最大連鎖 {_result.MaxEnemyKillsInOneTurn}");
        AppendLog($"[color=#{color.ToHtml(false)}][b]{verdict}[/b][/color]  {_result.Turns}ターン");
        SetScoreVisible(true);
        bool returnsToCampaign = CampaignSession.HasPendingEncounter;
        CampaignSession.CompleteBattle(_result.PlayerWon);
        Notice(returnsToCampaign
            ? "戦闘終了。結果を持って作戦マップへ戻れます。"
            : "戦闘終了。配置を変えるか、同じ台本をもう一度再生できます。", color);
        if (_campaignFlowSmoke)
        {
            CampaignSession.FlowSmokeReturning = true;
            GetTree().ChangeSceneToFile(CampaignSession.CampaignScene);
        }
        else if (_quitAfterPlayback)
        {
            GD.Print($"DEMO_SMOKE_COMPLETE events={_eventIndex} turns={_result.Turns} won={_result.PlayerWon}");
            GetTree().Quit();
        }
    }

    private async Task ApplyEvent(BattleEvent e, int eventIndex)
    {
        BattlePawn3D? actor = _battleField.FindPawn(e.ActorId);
        BattlePawn3D? target = _battleField.FindPawn(e.TargetId);
        switch (e.Kind)
        {
            case BattleEventKind.TurnStart:
                _statusByPawn.Clear();
                _battleField.SetTurn(e.Turn);
                AppendLog($"\n[color=#efc66a][b]── TURN {e.Turn} ──[/b][/color]");
                await Delay(0.22);
                break;

            case BattleEventKind.StatusSnapshot:
                if (target is not null && e.Text is { } key && e.Amount > 0)
                {
                    string label = DisplayStatusKey(key);
                    _statusByPawn[target.InstanceId] = string.IsNullOrEmpty(_statusByPawn.GetValueOrDefault(target.InstanceId))
                        ? $"{label}{e.Amount}"
                        : _statusByPawn[target.InstanceId] + $"  {label}{e.Amount}";
                    target.SetStatus(_statusByPawn[target.InstanceId]);
                }
                break;

            case BattleEventKind.StatSnapshot:
                target?.SetAttack(e.Amount, e.Pattern);
                break;

            case BattleEventKind.Attack:
            {
                AttackPattern pattern = e.Pattern ?? AttackPattern.Single;
                IReadOnlyList<BattlePawn3D> impactTargets = FindAttackTargets(eventIndex, e);
                _battleField.Attack(actor, target, pattern, impactTargets, e.Reaction, e.FriendlyFire);
                _battleField.AttackCue(actor, $"{(e.Reaction ? "反撃 " : "")}{UiKit.PatternLabel(pattern)}", AttackColor(actor, e));
                AppendLog($"[color=#{(actor?.Team == 0 ? UiKit.Player : UiKit.Enemy).ToHtml(false)}]{NameOf(e.ActorId)}[/color] → {NameOf(e.TargetId)}  [color=#a9b3a8]{UiKit.PatternLabel(e.Pattern ?? AttackPattern.Single)} {e.Amount}[/color]");
                await Delay(0.16);

                // 第124期 3-a: 「薙ぎやゾトの全体攻撃は一斉に入ったほうが爽快感ある」への直答。
                // **範囲の巻き込みだけを同時着弾にする。単体は現状のまま**
                // ——単体は1件しか無いので、まとめても絵が変わらない。
                if (pattern != AttackPattern.Single && ApplyLinkedDamageAtOnce(eventIndex, e) > 0)
                    await Delay(0.30);
                break;
            }

            case BattleEventKind.Damage:
                if (_batchedDamageIndices.Contains(eventIndex)) break;   // 3-a で同時に描き終えている
                ShowDamage(eventIndex, e, actor, target);
                await Delay(0.19);
                break;

            case BattleEventKind.Heal:
                target?.SetHp(e.HpAfter);
                target?.AnimateHeal();
                _battleField.Float(target, $"＋{e.Amount}", UiKit.Heal);
                // 第124期 3-h: 段2 で載った書き手から線を引く。
                if (e.ActorId is not null) _battleField.Link(actor, target, UiKit.Heal, "繕う");
                AppendLog($"  [color=#{UiKit.Heal.ToHtml(false)}]＋{e.Amount} 回復[/color] "
                          + $"{NameOf(e.TargetId)}{WriterSuffix(e.ActorId, e.TargetId)}");
                await Delay(0.17);
                break;

            case BattleEventKind.Death:
                target?.SetHp(0);
                target?.AnimateDeath();
                _battleField.Float(target, "DOWN", UiKit.Hurt, true);
                AppendLog($"  [color=#{UiKit.Hurt.ToHtml(false)}][b]{NameOf(e.TargetId)} 撃破[/b][/color]");
                await Delay(0.36);
                break;

            case BattleEventKind.Move:
                if (target is not null)
                {
                    _battleField.MovePawn(target, e.Slot);
                    // 第124期 3-h / §5-3。**書き手が居ない移動には線を引かない**
                    // ——それは書き手ではないので、線を引くと嘘になる。札だけを別色で出す。
                    if (e.ActorId is null)
                        _battleField.Orphan(target, "移動（原因不明）", UiKit.Faint);
                    else if (e.ActorId == e.TargetId)
                        _battleField.Link(actor, null, UiKit.Player, "自分で動いた");
                    else
                        _battleField.Link(actor, target, UiKit.Violet, "動かした");
                }
                AppendLog($"  {NameOf(e.TargetId)} → {FormationRules.SeatNames[Math.Clamp(e.Slot, 0, FormationRules.TotalSlots - 1)]}"
                          + $"{WriterSuffix(e.ActorId, e.TargetId)}");
                await Delay(0.28);
                break;

            case BattleEventKind.Summon:
                if (e.TargetId is { } summonId)
                {
                    UnitDef? def = FindDefByName(e.Text);
                    var opening = new DemoOpening(
                        summonId,
                        e.Team ?? BattleContext.PlayerTeam,
                        def?.Id ?? $"summon-{e.Text}",
                        e.Text ?? "召喚体",
                        e.Slot,
                        e.HpAfter,
                        Math.Max(e.HpAfter, def?.MaxHp ?? e.HpAfter),
                        def?.Attack ?? 0,
                        def?.Pattern ?? AttackPattern.Single);
                    _openingById[summonId] = opening;
                    _battleField.AddSummon(opening);
                    if (e.ActorId is not null)
                        _battleField.Link(actor, _battleField.FindPawn(summonId), UiKit.Violet, "呼んだ");
                }
                AppendLog($"  [color=#{UiKit.Violet.ToHtml(false)}]{e.Text ?? "召喚体"} が出現[/color]"
                          + $"{WriterSuffix(e.ActorId, e.TargetId)}");
                await Delay(0.34);
                break;

            case BattleEventKind.Revive:
                if (target is not null)
                {
                    target.SetHp(e.HpAfter);
                    _battleField.MovePawn(target, e.Slot);
                    target.AnimateRevive();
                    _battleField.Float(target, "REVIVE", UiKit.Heal, true);
                    if (e.ActorId is not null) _battleField.Link(actor, target, UiKit.Heal, "繋ぎ直した");
                }
                AppendLog($"  [color=#{UiKit.Heal.ToHtml(false)}]{NameOf(e.TargetId)} が復帰[/color]"
                          + $"{WriterSuffix(e.ActorId, e.TargetId)}");
                await Delay(0.38);
                break;

            case BattleEventKind.StatusGain:
                if (target is not null && e.Text is { } statusKey)
                {
                    // 第124期 3-g: 「テロップは出たが効果量が分からない」（ノミ）への直答。
                    // **量を先に、通貨名を次に、書き手は線で出す**——札の中へ駒名を畳むと、
                    // 読みたい量が名前の長さに埋もれる（3-b と同じ理由）。
                    string label = DisplayStatusKey(statusKey);
                    Color tint = StatusColor(label);
                    _battleField.Float(target, $"＋{e.Amount} {label}", tint, e.Amount >= 2);
                    if (e.ActorId is not null && e.ActorId != e.TargetId)
                        _battleField.Link(actor, target, tint, $"{label}を書いた");
                    AppendLog($"  [color=#{tint.ToHtml(false)}]＋{e.Amount} {label}[/color] → {NameOf(e.TargetId)}"
                              + $"{WriterSuffix(e.ActorId, e.TargetId)}");
                }
                await Delay(0.10);
                break;

            case BattleEventKind.Status:
                string statusName = e.Text ?? "状態効果";
                Color statusColor = StatusColor(statusName);
                if (!_linkedStatusEventIndices.Contains(eventIndex))
                    _battleField.Float(target, $"[{statusName}] 発動", statusColor);
                AppendLog($"  [color=#{statusColor.ToHtml(false)}][{statusName}] 発動[/color] → {NameOf(e.TargetId)}");
                await Delay(_linkedStatusEventIndices.Contains(eventIndex) ? 0.05 : 0.15);
                break;

            case BattleEventKind.Highlight:
                // 第124期 3-e: 「テロップは1倍速でも読むのが難しい」「『火のそばを見ている』
                // だけではなんのことやら」への直答。**段2 で書き手が載ったので駒に紐づけて出せる。**
                // 中央のバナーは**誰の見せ場か**を前置し、駒の頭上にも札を置く
                // ——中央だけだと、5体のどれの話なのかが画面から引けない。
                // **駒名が本文に既に入っているなら前置しない**——見せ場の文はほとんどが
                // 「{名前} が〜した」なので、素直に前置すると「粛の伝令 — 粛の伝令 が…」になる。
                string cue = e.Text ?? "発動";
                string banner = actor is null || cue.Contains(actor.UnitName, StringComparison.Ordinal)
                    ? cue : $"{actor.UnitName} — {cue}";
                _battleField.ShowBanner(banner, UiKit.Gold, 0.92);
                if (actor is not null) _battleField.Link(actor, null, UiKit.Gold, "★ 見せ場");
                AppendLog($"  [color=#{UiKit.Gold.ToHtml(false)}][b]{banner}[/b][/color]");
                await Delay(0.42);
                break;

            case BattleEventKind.Charge:
                _battleField.Float(actor, "CHARGE", UiKit.Gold, true);
                AppendLog($"[color=#{UiKit.Gold.ToHtml(false)}]{NameOf(e.ActorId)} — {e.Text ?? "力を溜める"}[/color]");
                await Delay(0.34);
                break;

            case BattleEventKind.Skill:
                _battleField.Float(actor, e.Text ?? "SKILL", UiKit.Heal, true);
                AppendLog($"[color=#{UiKit.Heal.ToHtml(false)}]{NameOf(e.ActorId)} — {e.Text ?? "術"}[/color]");
                await Delay(0.22);
                break;
        }
    }

    /// <summary>
    /// 1件のダメージを描く（間は置かない）。<b>本体のループからも、同時着弾（3-a）からも同じ絵を出す</b>
    /// ——2箇所に書くと、範囲攻撃だけ描き方がずれる。
    /// </summary>
    private void ShowDamage(int eventIndex, BattleEvent e, BattlePawn3D? actor, BattlePawn3D? target,
                            bool withSource = true)
    {
        target?.SetHp(e.HpAfter);
        target?.AnimateHit();
        (string source, Color sourceColor) = DamageSource(eventIndex, e, actor);
        _battleField.DamagePopup(target, e.Amount, source, sourceColor, e.Amount >= 25, withSource);
        _battleField.Impact(target, sourceColor,
                            _statusCauseByDamageIndex.ContainsKey(eventIndex), e.FriendlyFire);
        AppendLog($"  [color=#{sourceColor.ToHtml(false)}]{source}[/color] → {NameOf(e.TargetId)}  "
                  + $"[color=#{UiKit.Hurt.ToHtml(false)}]−{e.Amount}[/color]");
    }

    /// <summary>
    /// 同時着弾（第124期 3-a）。その一振りに<b>紐づく</b> <c>Damage</c> をまとめて描き、
    /// 描いた添字を控える。戻り値は描いた件数。
    ///
    /// <para><b>紐づけの規則は <see cref="FindAttackTargets"/> と同じ</b>
    /// （同じ <c>ActorId</c>・同じ <c>Pattern</c>・次の <c>TurnStart</c> か
    /// 同じ駒の次の <c>Attack</c> まで）——**2つの規則を持つと、線を引いた相手と
    /// 数字を出す相手が食い違う。**</para>
    ///
    /// <para><b>台本は1件も並べ替えない。</b> 描く順を前へ寄せるだけで、
    /// <c>Death</c> や <c>StatusGain</c> は今までどおりその後に流れる。</para>
    /// </summary>
    private int ApplyLinkedDamageAtOnce(int attackIndex, BattleEvent attack)
    {
        if (_result is null) return 0;
        int landed = 0;
        for (int i = attackIndex + 1; i < _result.Events.Count; i++)
        {
            BattleEvent candidate = _result.Events[i];
            if (candidate.Kind == BattleEventKind.TurnStart) break;
            if (candidate.Kind == BattleEventKind.Attack && candidate.ActorId == attack.ActorId) break;
            if (candidate.Kind != BattleEventKind.Damage) continue;
            if (candidate.ActorId != attack.ActorId || candidate.Pattern != attack.Pattern) continue;
            if (!_batchedDamageIndices.Add(i)) continue;
            ShowDamage(i, candidate, _battleField.FindPawn(candidate.ActorId),
                       _battleField.FindPawn(candidate.TargetId),
                       withSource: candidate.TargetId == attack.TargetId);
            landed++;
        }
        return landed;
    }

    /// <summary>
    /// 戦況ログに付ける「誰の仕業か」（第124期 3-h）。<b>書き手が居ないときは何も書かない</b>
    /// ——「盤面」と書くと、書き手が居ないことと書き手が盤面であることの区別が消える。
    /// </summary>
    private string WriterSuffix(int? actorId, int? targetId)
        => actorId is null || actorId == targetId
            ? ""
            : $"  [color=#{UiKit.Faint.ToHtml(false)}]← {NameOf(actorId)}[/color]";

    private void IndexStatusDamageEvents(IReadOnlyList<BattleEvent> events)
    {
        _statusCauseByDamageIndex.Clear();
        _linkedStatusEventIndices.Clear();
        for (int i = 0; i < events.Count; i++)
        {
            BattleEvent status = events[i];
            if (status.Kind != BattleEventKind.Status) continue;

            bool linked = false;
            for (int j = i + 1; j < events.Count && j <= i + 12; j++)
            {
                BattleEvent candidate = events[j];
                if (candidate.Kind is BattleEventKind.TurnStart or BattleEventKind.Attack or BattleEventKind.Status)
                    break;
                if (candidate.Kind != BattleEventKind.Damage || candidate.ActorId is not null) continue;
                _statusCauseByDamageIndex[j] = status.Text ?? "状態効果";
                linked = true;
            }
            if (linked) _linkedStatusEventIndices.Add(i);
        }
    }

    private IReadOnlyList<BattlePawn3D> FindAttackTargets(int attackIndex, BattleEvent attack)
    {
        var ids = new HashSet<int>();
        if (attack.TargetId is { } primary) ids.Add(primary);
        if (_result is null) return ids.Select(id => _battleField.FindPawn(id)).OfType<BattlePawn3D>().ToList();

        AttackPattern pattern = attack.Pattern ?? AttackPattern.Single;
        for (int i = attackIndex + 1; i < _result.Events.Count; i++)
        {
            BattleEvent candidate = _result.Events[i];
            if (candidate.Kind == BattleEventKind.TurnStart) break;
            if (candidate.Kind == BattleEventKind.Attack && candidate.ActorId == attack.ActorId) break;
            if (candidate.Kind == BattleEventKind.Damage
                && candidate.ActorId == attack.ActorId
                && candidate.Pattern == pattern
                && candidate.TargetId is { } targetId)
                ids.Add(targetId);
        }
        return ids.Select(id => _battleField.FindPawn(id)).OfType<BattlePawn3D>().ToList();
    }

    private (string Label, Color Color) DamageSource(int eventIndex, BattleEvent damage, BattlePawn3D? actor)
    {
        if (_statusCauseByDamageIndex.TryGetValue(eventIndex, out string? status))
            return ($"[{status}] 継続", StatusColor(status));
        if (damage.FriendlyFire)
            return ($"[巻込] {ShortNameOf(damage.ActorId)}", UiKit.Violet);
        if (damage.Reaction)
            return ($"[反撃] {ShortNameOf(damage.ActorId)}", UiKit.Gold);
        if (damage.Relayed)
            return ($"[肩代] {ShortNameOf(damage.ActorId)}", UiKit.Muted);
        if (damage.ActorId is null)
            return ("[効果] 盤面", UiKit.Violet);

        string mark = damage.Pattern switch
        {
            AttackPattern.Sweep => "薙",
            AttackPattern.Pierce => "貫",
            AttackPattern.All => "全",
            AttackPattern.Single => "攻",
            // 第124期 3-g: 型を持たない ＝ `PerformAttack` を通っていない＝**特性が直に削った**。
            // 抉り・断ち・なぞり・追い打ち・破裂がここに来る。
            // 「効果」だと盤面由来（毒・燃焼）と区別が付かなかった。
            _ => "特",
        };
        return ($"[{mark}] {ShortNameOf(damage.ActorId)}", AttackColor(actor, damage));
    }

    private static Color AttackColor(BattlePawn3D? actor, BattleEvent e)
        => e.FriendlyFire ? UiKit.Violet : e.Reaction ? UiKit.Gold : actor?.Team == 0 ? UiKit.Player : UiKit.Enemy;

    private static Color StatusColor(string status) => status switch
    {
        "毒" => UiKit.Poison,
        // `StatusGain` は `StatusKeys.LabelOf`（「燃」）、`Status` は特性側の文字列（「燃焼」）で来る。
        // **同じ通貨が2つの名前で来る**ので、両方を同じ色に落とす（第124期 3-g）。
        "燃" or "燃焼" => UiKit.Burn,
        "傷" or "深手" => UiKit.Wound,
        "なまり" => UiKit.Muted,
        "強化" => UiKit.Gold,
        _ => UiKit.Violet,
    };

    private string ShortNameOf(int? id)
    {
        string name = NameOf(id);
        return name.Length <= 8 ? name : name[..7] + "…";
    }

    private static string DisplayStatusKey(string key) => key switch
    {
        "dull" => "なまり",
        "whet" => "強化",
        _ => StatusKeys.LabelOf(key),
    };

    private string NameOf(int? id)
    {
        if (id is not { } value) return "盤面";
        BattlePawn3D? pawn = _battleField.FindPawn(value);
        return pawn?.UnitName ?? _openingById.GetValueOrDefault(value)?.Name ?? "？";
    }

    private static UnitDef? FindDefByName(string? name)
    {
        if (name is null) return null;
        static IEnumerable<UnitDef> Fields(Type type) => type
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(UnitDef))
            .Select(f => f.GetValue(null))
            .OfType<UnitDef>();
        return Fields(typeof(UnitCatalog)).Concat(Fields(typeof(EnemyCatalog))).FirstOrDefault(d => d.Name == name);
    }

    private void AppendLog(string bbcode)
    {
        _battleLog.AppendText(bbcode + "\n");
    }

    private async Task Delay(double seconds, bool raw = false)
    {
        if (_fastSmoke && !raw)
        {
            await Task.CompletedTask;
            return;
        }
        double duration = raw ? seconds : seconds / Math.Max(0.1, _speed);
        await ToSignal(GetTree().CreateTimer(Math.Max(0.01, duration)), SceneTreeTimer.SignalName.Timeout);
    }

    private void TogglePause()
    {
        if (!_battleMode || !_playing) return;
        _paused = !_paused;
        _pause.Text = _paused ? "再開" : "一時停止";
        Notice(_paused ? "一時停止中" : "再生中", _paused ? UiKit.Gold : UiKit.Player);
    }

    private void SetSpeed(int index)
    {
        _speed = index switch { 0 => 0.5, 2 => 2.0, 3 => 4.0, _ => 1.0 };
        _speedPicker.Selected = index;
        Notice($"再生速度 ×{_speed:0.#}", UiKit.Player);
    }

    private void ReplayBattle()
    {
        if (!_battleMode || _result is null) return;
        ++_playToken;
        _eventIndex = 0;
        _statusByPawn.Clear();
        _batchedDamageIndices.Clear();
        _battleLog.Clear();
        _battleField.BeginBattle(_battleOpening, EnemyCatalog.Stages[_stagePicker.Selected].Name);
        SetScoreVisible(false);
        Notice("同じ計算結果を最初から再生します", UiKit.Player);
        BeginPlayback();
    }

    private void ReturnToFormation()
    {
        if (!_battleMode) return;
        ++_playToken;
        _battleMode = false;
        _playing = false;
        _paused = false;
        _result = null;
        _rosterPanel.Visible = true;
        _detailPanel.Visible = true;
        _battlePanel.Visible = false;
        _setupActions.Visible = true;
        _battleActions.Visible = false;
        _stagePicker.Disabled = false;
        _presetPicker.Disabled = false;
        _seed.Editable = true;
        _battleField.Visible = false;
        SetScoreVisible(false);
        _field.Visible = true;
        _field.UpdateFormation(_formation);
        UpdateStageHeader();
        if (_inspected is { } def) ShowUnitDetails(def); else ShowEnemyDetails();
        Notice("編成と配置を変更できます");
    }

    /// <summary>
    /// 戦績パネルの開け閉め（第124期 段1）。<b><c>BattleCore</c> を1行も触らない。</b>
    ///
    /// <para>出す数字は <see cref="BattleResult.TallyByUnit"/> ——判定を1つも通さずに
    /// 「誰が何をしたか」が引ける（Phase 0 Q0-1 / Q0-2）。<b>台本は開戦前に計算済み</b>なので、
    /// この表は再生位置によらず<b>戦闘全体の最終集計</b>である。</para>
    /// </summary>
    private void ToggleScore() => SetScoreVisible(!_scorePanel.Visible);

    private void SetScoreVisible(bool visible)
    {
        if (visible && _result is not null)
        {
            int stage = _stagePicker.Selected;
            _scorePanel.Render(
                _result,
                _battleOpening,
                $"{EnemyCatalog.Stages[stage].Name} ・ seed {(int)_seed.Value} ・ "
                + $"{_result.Turns}ターン ・ {(_result.PlayerWon ? "勝利" : "敗北")}"
                + $"（生存 {_result.PlayerSurvivors}体）。"
                + "台本は開戦前に計算済み。この表は戦闘全体の最終集計で、再生位置によらない。");
        }
        _scorePanel.Visible = visible && _result is not null;
        _score.Text = _scorePanel.Visible ? "戦績を閉じる (T)" : "戦績 (T)";
    }

    private void GoToCampaign()
    {
        ++_playToken;
        GetTree().ChangeSceneToFile(CampaignSession.CampaignScene);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (!_battleMode) return;
        switch (key.Keycode)
        {
            case Key.Space:
                TogglePause();
                break;
            case Key.Key1:
                SetSpeed(0);
                break;
            case Key.Key2:
                SetSpeed(1);
                break;
            case Key.Key3:
                SetSpeed(2);
                break;
            case Key.Key4:
                SetSpeed(3);
                break;
            case Key.T:
                ToggleScore();
                break;
            case Key.Escape:
                if (_scorePanel.Visible) SetScoreVisible(false); else ReturnToFormation();
                break;
            default:
                return;
        }
        AcceptEvent();
    }
}
