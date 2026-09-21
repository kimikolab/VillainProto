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
    private readonly HashSet<int> _burningSnapshot = new();
    private readonly Dictionary<int, (int Marked, int Stunned, int Armor)> _statusEffectSnapshot = new();
    private readonly HashSet<int> _poisonedSnapshot = new();
    private readonly Dictionary<int, DemoOpening> _openingById = new();
    private readonly Dictionary<int, string> _statusCauseByDamageIndex = new();
    private readonly HashSet<int> _linkedStatusEventIndices = new();

    /// <summary>
    /// 同時着弾（第124期 3-a）で<b>攻撃の側がまとめて描いた</b> <c>Damage</c> の添字。
    /// 本体のループがここへ来たら<b>もう一度描かない</b>。
    /// </summary>
    private readonly HashSet<int> _batchedDamageIndices = new();
    private readonly HashSet<int> _shownYokeIndices = new();

    // =====================================================================================
    // 第125期 段2 —— 手番の外を「手番の外」として見せる。**`BattleCore` を1行も触らない。**
    //
    // 情報は台本にもう全部載っている（Phase 0 Q0-3 / Q0-4 と段1 の `Intercept`）。
    // 足りなかったのは**いつ起きたか**で、いまの再生は `_result.Events` を1本の列として
    // 先頭から流すだけなので、**割り込みも肩代わりもターン頭の一括も「そういう順番で起きた1件」**
    // に潰れていた（Q0-8）。ここで拍（`Beat`）を1本の前処理で割り当て、
    // 再生の側は**その境目でだけ**絵を変える。
    // =====================================================================================

    /// <summary>再生の拍（第125期 段2）。<b>台本は1件も並べ替えない。</b></summary>
    private enum Beat
    {
        /// <summary>誰の手番でもない時間（`TurnStart` 〜 最初の手番の行動）。ターン頭の一括がここに落ちる。</summary>
        TurnOpen,
        /// <summary>手番の中。</summary>
        InTurn,
        /// <summary>手番の外（割り込み・肩代わり・介入）。</summary>
        OffTurn,
    }

    private readonly List<Beat> _beatByIndex = new();
    private readonly List<int> _ownerByIndex = new();

    /// <summary>中継された段（`Relayed`）の<b>本来の標的</b>。台本の前方走査で引く。</summary>
    private readonly Dictionary<int, int> _relayVictimByIndex = new();

    private Beat _shownBeat = Beat.TurnOpen;
    private int _shownOwner = -1;

    /// <summary>
    /// 溜めの予告を出したまま次の一撃を待っている駒（第125期 段3-b）。
    /// <c>Charge</c> は次の倍率・攻撃型・名前を全部持っているので、<b>台本だけで予告が書ける。</b>
    /// </summary>

    /// <summary>
    /// 全体に落ちた一撃（第125期 段3-a）。<c>Highlight</c> の側でまとめて描いた <c>Damage</c> の添字。
    /// <b>ゾトの破裂は `Attack` を1件も出さない</b>ので、同時着弾（3-a・第124期）の経路に乗らない。
    /// </summary>
    private readonly HashSet<int> _burstDamageIndices = new();

    private Texture2D _atlas = null!;
    private BattlefieldView _field = null!;
    private BattlefieldView3D _battleField = null!;
    private BattleMusic _battleMusic = null!;
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

    /// <summary>
    /// 「作戦マップへ」の戻り先。入場時に1回だけ決める（第169期）。
    /// 既定は検証用マップ 1-1 で、既存の作戦マップから来たときだけそちらを指す。
    /// </summary>
    private string _returnScene = Map11Session.Scene;
    private Button _campaignBattle = null!;
    private Button _score = null!;
    private OptionButton _speedPicker = null!;
    private ScorePanel _scorePanel = null!;
    private PartyBar _partyBar = null!;

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
    private bool _map11FlowSmoke;

    private sealed record PendingOpening(
        UnitState Unit,
        int Slot,
        int Hp,
        int MaxHp,
        int Attack,
        AttackPattern Pattern);

    public override void _Ready()
    {
        // 第169期 自己検査 (c)。**画面を1つも作らずに `Map11State` だけを回す。**
        // 通常起動では 1 ビットも走らない。
        string[] bootArgs = OS.GetCmdlineUserArgs();
        if (bootArgs.Contains("--map11-phase0", StringComparer.Ordinal))
        {
            bool p0 = Map11Verify.Phase0(GD.Print);
            GD.Print($"MAP11_PHASE0_COMPLETE ok={p0}");
            GetTree().Quit(p0 ? 0 : 1);
            return;
        }
        // 第171期 Phase 0。**ソースの中身はここで読んで渡す**——`Map11Phase171` に
        // Godot の型を入れないため（`Map11` / `Map11Verify` と同じ扱い）。
        if (bootArgs.Contains("--map11-phase171", StringComparer.Ordinal))
        {
            string root = ProjectSettings.GlobalizePath("res://") + "../BattleCore/";
            string Read(string name)
            {
                try { return System.IO.File.ReadAllText(root + name); }
                catch (Exception ex) { GD.Print($"（{name} を読めなかった: {ex.Message}）"); return ""; }
            }
            bool p171 = Map11Phase171.Run(GD.Print, Read("Traits.cs"), Read("BattleEngine.cs"));
            GD.Print($"MAP11_PHASE171_COMPLETE ok={p171}");
            GetTree().Quit(p171 ? 0 : 1);
            return;
        }
        // 第172期 Phase 0。**ソースの中身はここで読んで渡す**（`Map11Phase172` に Godot の型を入れない）。
        if (bootArgs.Contains("--map11-phase172", StringComparer.Ordinal))
        {
            string core = ProjectSettings.GlobalizePath("res://") + "../BattleCore/";
            string demo = ProjectSettings.GlobalizePath("res://");
            string Read172(string path)
            {
                try { return System.IO.File.ReadAllText(path); }
                catch (Exception ex) { GD.Print($"（{path} を読めなかった: {ex.Message}）"); return ""; }
            }
            bool p172 = Map11Phase172.Run(GD.Print, Read172(core + "Traits.cs"),
                                          Read172(core + "Models.cs"),
                                          Read172(demo + "BattlefieldView.cs"));
            GD.Print($"MAP11_PHASE172_COMPLETE ok={p172}");
            GetTree().Quit(p172 ? 0 : 1);
            return;
        }
        // 第173期 Phase 0。**ソースの中身はここで読んで渡す**（`Map11Phase173` に Godot の型を入れない）。
        if (bootArgs.Contains("--map11-phase173", StringComparer.Ordinal))
        {
            string core173 = ProjectSettings.GlobalizePath("res://") + "../BattleCore/";
            string traits173 = "";
            try { traits173 = System.IO.File.ReadAllText(core173 + "Traits.cs"); }
            catch (Exception ex) { GD.Print($"（Traits.cs を読めなかった: {ex.Message}）"); }
            bool p173 = Map11Phase173.Run(GD.Print, traits173);
            GD.Print($"MAP11_PHASE173_COMPLETE ok={p173}");
            GetTree().Quit(p173 ? 0 : 1);
            return;
        }
        // 第171期 部C —— 演出の穴の棚卸し（**調べて表にするだけ**）。
        if (bootArgs.Contains("--map11-artgap", StringComparer.Ordinal))
        {
            string main = "";
            try { main = System.IO.File.ReadAllText(ProjectSettings.GlobalizePath("res://") + "Main.cs"); }
            catch (Exception ex) { GD.Print($"（Main.cs を読めなかった: {ex.Message}）"); }
            bool gap = Map11ArtGap.Run(GD.Print, main);
            GD.Print($"MAP11_ARTGAP_COMPLETE ok={gap}");
            GetTree().Quit(gap ? 0 : 1);
            return;
        }
        string? verifyArg = bootArgs.FirstOrDefault(a => a.StartsWith("--map11-verify", StringComparison.Ordinal));
        if (verifyArg is not null)
        {
            int n = verifyArg.Length > "--map11-verify=".Length
                && int.TryParse(verifyArg["--map11-verify=".Length..], out int want) && want > 0
                ? want : Map11Verify.DefaultSeeds;
            bool ok = Map11Verify.Report(n, GD.Print);
            GD.Print($"MAP11_VERIFY_COMPLETE seeds={n} ok={ok}");
            GetTree().Quit(ok ? 0 : 1);
            return;
        }

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
        // 第169期。検証用マップ 1-1 の通し確認（画面あり・頭なし）。
        _map11FlowSmoke = userArgs.Contains("--map11-flow-smoke", StringComparer.Ordinal);
        if (_map11FlowSmoke) _fastSmoke = true;
        if (_fastSmoke) _speed = 1000.0;
        // 第172期 部B。**編成画面の当たりを1枚だけ撮る**（見た目の確認だけ。判定には使わない）。
        // 書式は `--demo-setup-capture=<path>[,<席>]`。
        if (userArgs.FirstOrDefault(a => a.StartsWith("--demo-setup-capture=", StringComparison.Ordinal))
            is { } setupShot)
            _ = CaptureSetup(setupShot["--demo-setup-capture=".Length..]);
        string? captureArg = userArgs.FirstOrDefault(arg => arg.StartsWith("--demo-capture-dir=", StringComparison.Ordinal));
        string? captureDirectory = captureArg?["--demo-capture-dir=".Length..];
        if (userArgs.Contains("--demo-autoplay", StringComparer.Ordinal))
            _ = AutoplayForCapture(captureDirectory);

        // 第169期。戻り先は入場時に決める（勝敗で `HasPendingEncounter` が落ちても揺れない）。
        if (CampaignSession.HasPendingEncounter) _returnScene = CampaignSession.CampaignScene;

        // 第169期。検証用マップ 1-1 から接敵して来たときは、**編成を挟まずそのまま出撃する。**
        if (CampaignSession.HasCarriedBattle) StartCarriedBattle();
    }

    /// <summary>編成画面（関係の線つき）の絵を1枚撮って終わる。第172期 部B の見た目の確認用。</summary>
    private async Task CaptureSetup(string spec)
    {
        string[] parts = spec.Split(',');
        await Delay(0.4, raw: true);
        if (parts.Length > 1 && int.TryParse(parts[1], out int slot)) OnSetupSlotClicked(slot);
        await Delay(0.3, raw: true);
        Error error = GetViewport().GetTexture().GetImage().SavePng(parts[0]);
        GD.Print($"DEMO_SETUP_CAPTURE error={error} path={parts[0]}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
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
        _battleMusic = new BattleMusic();
        AddChild(_battleMusic);

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
        // 画面下の固定の一覧（第125期 段3-e）。**盤面の上に重ねるが、席が変わっても動かない。**
        // 盤面上のゲージは消していない（指示書 §6 の 3-e: 「両方出して構わない」）。
        _partyBar = new PartyBar
        {
            AnchorLeft = 0, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1,
            OffsetLeft = 8, OffsetRight = -8, OffsetTop = -88, OffsetBottom = -8,
        };
        fieldStack.AddChild(_partyBar);
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
            _field.SetSetupSelection(slot);
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
        // 第172期 部B。**選んでいる駒の席**を渡して、その駒に関わる線だけ濃くする。
        _field.UpdateFormation(_formation, _armedUnitId is null
            ? -1 : Array.FindIndex(_formation, u => u?.Id == _armedUnitId));
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
            $"[color=#efc66a][b]◇ 手番[/b][/color]\n{TurnTextOf(def)}\n\n" +
            $"[color=#71d7a1][b]＋ 強み[/b][/color]\n{def.PlusText}\n\n" +
            $"[color=#ff766b][b]− 欠点[/b][/color]\n{def.MinusText}\n\n" +
            $"[color=#6f7f76][i]{def.Flavor}[/i][/color]\n\n" +
            $"[color=#a9b3a8]配置: {PlacementOf(def)}[/color]" + RelationTextOf(def);
    }

    /// <summary>
    /// 「手番」欄の文（第127期 段0-1）。<b>文は書き足さず、実装から組み立てる</b>
    /// ——`Pattern` ＋ `Actions` ＋ 手番そのものを変える札（のろま・不動・追い打ち・軋み）だけを読む。
    /// <c>PlusText</c> / <c>MinusText</c> は1文字も触らない。
    ///
    /// <para><b>欄の名前を「アクティブスキル」にしない。</b> <c>Actions</c> を持つのは 52 枚中 5 枚なので
    /// （第126期に実測）、そう名付けると 47 枚が空欄になる。<b>「手番」なら常に埋まる</b>
    /// ——`—`（計数が無い）と `0`（やっていない）を描き分ける第124期 段1 と同じ判断。</para>
    /// </summary>
    private static string TurnTextOf(UnitDef def)
    {
        var lines = new List<string>();

        if (def.Actions is null || def.Actions.Count == 0)
        {
            lines.Add($"通常攻撃（{UiKit.PatternLabel(def.Pattern)}）");
        }
        else
        {
            for (int i = 0; i < def.Actions.Count; i++)
            {
                UnitAction a = def.Actions[i];
                string head = def.Actions.Count == 1 ? "" : $"{i + 1}周目: ";
                lines.Add(head + a.Kind switch
                {
                    ActionKind.Charge => "溜める（攻撃しない）",
                    ActionKind.Skill => "術を使う（攻撃しない）",
                    _ => $"攻撃（{UiKit.PatternLabel(a.PatternOverride ?? def.Pattern)}"
                         + (a.AttackPercent == 100 ? "" : $"・威力 {a.AttackPercent}%") + "）",
                });
            }
        }

        if (def.Traits.Contains(TraitId.Sluggish)) lines.Add("2ターンに1回しか動かない");
        if (def.Traits.Contains(TraitId.Immobile)) lines.Add("自分からは攻撃しない");
        if (def.Traits.Contains(TraitId.Pursuer)) lines.Add("味方が敵を倒すとターン外に割り込む");
        if (def.Traits.Contains(TraitId.Displaced)) lines.Add("動かされた直後にターン外に割り込む");

        return string.Join("\n", lines);
    }

    /// <summary>
    /// いまの編成で、その駒から出ている／その駒に入っている関係（第172期 部B）。
    /// <b>線と同じ元データ（`Map11Relations`）から引くだけ</b>——写しを持たない（自己検査 (e)）。
    /// 編成に入っていない駒では空。
    /// </summary>
    private string RelationTextOf(UnitDef def)
    {
        int slot = Array.FindIndex(_formation, u => u?.Id == def.Id);
        if (slot < 0) return "";
        var seats = new Dictionary<int, UnitDef>();
        for (int i = 0; i < _formation.Length; i++)
            if (_formation[i] is { } d) seats[i] = d;

        var lines = new List<string>();
        foreach (Map11Relations.Link l in Map11Relations.Of(seats))
        {
            bool from = l.From == slot, to = l.To == slot;
            if (!from && !to) continue;
            string other = seats[from ? l.To : l.From].Name;
            string mean = Map11Relations.MeanOf(l.Trait) is { Length: > 0 } m
                ? $"\n   [color=#6f7f76]{m}[/color]" : "";
            // **得／損／両方 と、条件つき（点線）かを文にも出す**（第173期 §1-1）。
            // 色の出どころは線と同じ `SeatLinks.ColorOf` の1本（写しを持たない）。
            string tint = SeatLinks.ColorOf(l.Sign).ToHtml(false);
            string dash = l.Conditional ? "[color=#6f7f76]（条件つき）[/color]" : "";
            lines.Add($"[color=#{tint}][{Map11Relations.LabelOf(l.Sign)}] {(from ? "→" : "←")}[/color]"
                + $" {other}：[b][color=#{tint}]{l.Word}[/color][/b]{dash}{mean}");
        }
        return lines.Count == 0
            ? "\n\n[color=#6f7f76]この席から出ている関係はありません[/color]"
            : "\n\n[color=#efc66a][b]◇ この席の関係[/b][/color]\n" + string.Join("\n", lines);
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
        EnterBattle(players, enemies, seed, stageIndex, EnemyCatalog.Stages[stageIndex].Name);
    }

    /// <summary>
    /// <b>第169期</b> —— 検証用マップ 1-1 から入る戦闘。<b>編成画面を経由しない。</b>
    ///
    /// <para>渡された <see cref="UnitState"/> のリストを<b>写さずにそのまま</b>
    /// <see cref="BattleEngine.Run"/> へ入れる（傷・死者・最大HPの損耗がそのまま盤面に立つ）。
    /// <c>stageIndex</c> は背景の選択にしか使わない。</para>
    /// </summary>
    private void StartCarriedBattle()
    {
        if (_battleMode || !CampaignSession.HasCarriedBattle) return;
        List<UnitState> players = CampaignSession.CarriedPlayers!;
        List<UnitState> enemies = CampaignSession.CarriedEnemies!;
        int stageIndex = CampaignSession.CarriedStageIndex;
        string title = CampaignSession.CarriedEnemyName ?? EnemyCatalog.Stages[stageIndex].Name;
        _stagePicker.Selected = stageIndex;
        _seed.Value = CampaignSession.CarriedSeed;
        // 編成画面へは戻さない（この戦闘の編成は作戦マップが持っている）。
        _back.Visible = false;
        EnterBattle(players, enemies, CampaignSession.CarriedSeed, stageIndex, title);
    }

    private void EnterBattle(List<UnitState> players, List<UnitState> enemies,
                             int seed, int stageIndex, string title)
    {
        List<PendingOpening> pending = players.Concat(enemies)
            .Select(u => new PendingOpening(u, u.Slot, u.Hp, u.MaxHp, u.CurrentAttack, u.CurrentPattern))
            .ToList();

        _result = BattleEngine.Run(players, enemies, seed, verbose: true);
        IndexStatusDamageEvents(_result.Events);
        IndexTimeline(_result.Events);
        _battleOpening = pending.Select(x => new DemoOpening(
            x.Unit.InstanceId,
            x.Unit.TeamId,
            x.Unit.Def.Id,
            x.Unit.Def.Name,
            x.Slot,
            x.Hp,
            x.MaxHp,
            x.Attack,
            x.Pattern,
            x.Unit.Def.Advances,
            // 第171期 §2-2。**盤面ルールの保持者の札**を再生側が出すための1フィールド。
            x.Unit.Def.Traits)).ToList();

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
        _battleSummary.Text = $"{title}\nseed {seed} ・ 予測済み台本 {_result.Events.Count}イベント";
        _battleLog.Clear();
        _field.Visible = false;
        _battleField.Visible = true;
        _battleField.BeginBattle(_battleOpening, title, stageIndex);
        _battleMusic.PlayWave(stageIndex);
        _partyBar.Begin(_battleOpening);
        _partyBar.Sync(_battleField, -1);
        _partyBar.Visible = true;
        SetScoreVisible(false);
        _eventIndex = 0;
        _statusByPawn.Clear();
        _batchedDamageIndices.Clear();
        _shownYokeIndices.Clear();
        _burstDamageIndices.Clear();
        _shownBeat = Beat.TurnOpen;
        _shownOwner = -1;
        Notice("BattleCore が計算したイベント列を再生中", UiKit.Player);
        BeginPlayback();
    }

    private int _finishSoundIndex = -1;

    private async void BeginPlayback()
    {
        if (_result is null) return;
        _finishSoundIndex = FinishSoundCue.Find(_result.PlayerWon, _battleOpening, _result.Events);
        int token = ++_playToken;
        _playing = true;
        _paused = false;
        _pause.Text = "一時停止";
        _battleField.PlayBattleStartSound();
        _battleField.ShowBanner("BATTLE START", UiKit.Gold, 0.8);
        await Delay(0.62);

        while (token == _playToken && _battleMode && _eventIndex < _result.Events.Count)
        {
            while (_paused && token == _playToken && _battleMode) await Delay(0.06, raw: true);
            if (token != _playToken || !_battleMode) return;
            int eventIndex = _eventIndex++;
            BattleEvent e = _result.Events[eventIndex];
            await ApplyEvent(e, eventIndex);
            // 第125期 段3-e: 画面下の一覧を引き直す。**数字の出どころは盤面の駒だけ**で、
            // 台本からは数え直さない（同じ言葉の表を2つ作らない・第124期 §4）。
            _partyBar.Sync(_battleField, _shownOwner);
        }

        if (token != _playToken || !_battleMode) return;
        _playing = false;
        string verdict = _result.PlayerWon ? "VICTORY" : "DEFEAT";
        foreach (var pawn in _battleField.Pawns.Values) pawn.CancelCharge();
        Color color = _result.PlayerWon ? UiKit.Heal : UiKit.Hurt;
        if (_result.PlayerWon) _battleField.ShowVictoryPortraits();
        _battleField.ShowBanner(verdict, color, 2.2);
        _battleField.SetSubline($"{(_result.PlayerWon ? "勝利" : "敗北")} ・ {_result.Turns}ターン ・ 生存 {_result.PlayerSurvivors}体 ・ 最大連鎖 {_result.MaxEnemyKillsInOneTurn}");
        AppendLog($"[color=#{color.ToHtml(false)}][b]{verdict}[/b][/color]  {_result.Turns}ターン");
        SetScoreVisible(true);
        bool returnsToCampaign = CampaignSession.HasPendingEncounter || Map11Session.HasPendingBattle;
        // 第169期。**精算はここ1箇所**。`Map11Session` 側が「戻った1回だけ」に絞っているので、
        // 「最初から」で台本を再生し直しても二度は走らない。
        Map11Session.CompleteBattle(_result.PlayerWon, _result);
        CampaignSession.ClearCarriedBattle();
        CampaignSession.CompleteBattle(_result.PlayerWon);
        Notice(returnsToCampaign
            ? "戦闘終了。結果を持って作戦マップへ戻れます。"
            : "戦闘終了。配置を変えるか、同じ台本をもう一度再生できます。", color);
        if (_map11FlowSmoke)
        {
            GD.Print($"MAP11_BATTLE_DONE won={_result.PlayerWon} turns={_result.Turns}");
            GetTree().ChangeSceneToFile(Map11Session.Scene);
        }
        else if (_campaignFlowSmoke)
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
        if (actor is not null) actor.AnimationSpeed = Math.Max(0.1, _speed);
        if (target is not null) target.AnimationSpeed = Math.Max(0.1, _speed);
        // 第125期 段2: 拍の境目でだけ画面を変える。**ここでは待たない**（間は下の switch の中だけ）。
        EnterBeat(eventIndex, e);
        switch (e.Kind)
        {
            case BattleEventKind.TurnStart:
                _battleField.EndGuards();
                _burningSnapshot.Clear();
                _statusEffectSnapshot.Clear();
                _poisonedSnapshot.Clear();
                _statusByPawn.Clear();
                _battleField.SetTurn(e.Turn);
                AppendLog($"\n[color=#efc66a][b]── TURN {e.Turn} ──[/b][/color]");
                await Delay(0.22);
                break;

            case BattleEventKind.StatusSnapshot:
                if (target is not null && e.Text is { } key && e.Amount > 0)
                {
                    target.ReadStatusSnapshot(key, e.Amount);
                    if (key == StatusKeys.LabelOf(StatusKeys.Burn)) _burningSnapshot.Add(target.InstanceId);
                    var effects = _statusEffectSnapshot.GetValueOrDefault(target.InstanceId);
                    if (key == StatusKeys.LabelOf(StatusKeys.Marked)) effects.Marked = e.Amount;
                    if (key == StatusKeys.LabelOf(StatusKeys.Stun)) effects.Stunned = e.Amount;
                    if (key == StatusKeys.LabelOf(StatusKeys.Armor)) effects.Armor = e.Amount;
                    _statusEffectSnapshot[target.InstanceId] = effects;
                    if (key == StatusKeys.LabelOf(StatusKeys.Poison)) _poisonedSnapshot.Add(target.InstanceId);
                    string label = DisplayStatusKey(key);
                    _statusByPawn[target.InstanceId] = string.IsNullOrEmpty(_statusByPawn.GetValueOrDefault(target.InstanceId))
                        ? $"{label}{e.Amount}"
                        : _statusByPawn[target.InstanceId] + $"  {label}{e.Amount}";
                    target.SetStatus(_statusByPawn[target.InstanceId]);
                }
                break;

            case BattleEventKind.StatSnapshot:
                target?.CommitStatusSnapshot();
                // 各駒の状態一覧の直後。最後の燃焼ダメージを見せてから消火する。
                if (target is not null) target.SetBurning(_burningSnapshot.Contains(target.InstanceId));
                if (target is not null)
                {
                    var effects = _statusEffectSnapshot.GetValueOrDefault(target.InstanceId);
                    target.SetStatusEffects(effects.Marked, effects.Stunned, effects.Armor);
                }
                if (target is not null) target.SetPoisoned(_poisonedSnapshot.Contains(target.InstanceId));
                int attackChange = target is null ? 0 : e.Amount - target.AttackValue;
                target?.SetAttack(e.Amount, e.Pattern);
                _battleField.PlayAttackChangeSound(attackChange);
                break;

            case BattleEventKind.Attack:
            {
                AttackPattern pattern = e.Pattern ?? AttackPattern.Single;
                IReadOnlyList<BattlePawn3D> impactTargets = FindAttackTargets(eventIndex, e);
                // 溜めの解放は踏み込み後の着弾で行う。手番外の攻撃では消費しない。
                if (e.Reaction)
                    await _battleField.ShowBonusAttack(actor);
                await _battleField.Attack(actor, target, pattern, impactTargets, e.Reaction, e.FriendlyFire);
                AppendLog($"[color=#{(actor?.Team == 0 ? UiKit.Player : UiKit.Enemy).ToHtml(false)}]{NameOf(e.ActorId)}[/color] → {NameOf(e.TargetId)}  [color=#a9b3a8]{UiKit.PatternLabel(e.Pattern ?? AttackPattern.Single)} {e.Amount}[/color]");
                // 第125期 段2: 手番の外の一撃（棘・仇討ち・軋み）は**流れを一度止める**。
                // **手番の中は詰めてある**（0.16 → 0.14）ので、合計はほぼ動かない（§5-2）。
                if (e.Reaction) await Delay(0.24);
                await Delay(0.14);

                // 第124期 3-a: 「薙ぎやゾトの全体攻撃は一斉に入ったほうが爽快感ある」への直答。
                // **範囲の巻き込みだけを同時着弾にする。単体は現状のまま**
                // ——単体は1件しか無いので、まとめても絵が変わらない。
                if (pattern != AttackPattern.Single && ApplyLinkedDamageAtOnce(eventIndex, e) > 0)
                {
                    await Delay(0.30);
                    _battleField.EndGuards();
                }
                break;
            }

            case BattleEventKind.Parry:
                if (_batchedDamageIndices.Contains(eventIndex)) break;
                if (_burstDamageIndices.Contains(eventIndex)) break;
                if (e.Reaction && StartsDirectReaction(eventIndex, e))
                {
                    await _battleField.ShowBonusAttack(actor);
                    _battleField.PlayDirectReactionSound(actor);
                }
                ShowParry(e);
                await Delay(0.30);
                _battleField.EndGuards();
                break;

            case BattleEventKind.Damage:
                if (_batchedDamageIndices.Contains(eventIndex)) break;   // 3-a で同時に描き終えている
                if (_burstDamageIndices.Contains(eventIndex)) break;     // 破裂（第125期 3-a）で描き終えている
                // 棘（カド）・仇討ちは PerformAttack を通らず、Reaction 付き Damage から始まる。
                // ヨミのように Reaction 付き Attack を持つ段は上で既にカットイン済みなので二重に出さない。
                if (e.Reaction && StartsDirectReaction(eventIndex, e))
                {
                    await _battleField.ShowBonusAttack(actor);
                    _battleField.PlayDirectReactionSound(actor);
                }
                ShowDamage(eventIndex, e, actor, target);
                await Delay(0.16);
                if (!e.Relayed && target?.IsGuarding == true)
                {
                    await Delay(0.10);
                    _battleField.EndGuards();
                }
                break;

            // 第125期 段1・段2 —— 介入が主目標を差し替えた。**この期に台本へ足した唯一の種類。**
            // `ActorId` = 割り込んだ駒 ／ `TargetId` = **本来の標的**なので、
            // 線は「本来の標的 → 割り込んだ駒」に折れる（§5-1 の 4）。
            case BattleEventKind.Intercept:
                // 標的は引き寄せなので踏み込まない。庇いの各段だけを動かす。
                if (e.Text is InterceptLabels.Guardian or InterceptLabels.ThornGuard
                    or InterceptLabels.RearGuard or InterceptLabels.Martyr)
                    _battleField.Guard(target, actor);
                _battleField.Divert(target, actor, e.Text ?? "介入", UiKit.Gold);
                AppendLog($"  [color=#{UiKit.Gold.ToHtml(false)}][b]{NameOf(e.ActorId)} が {NameOf(e.TargetId)} の前に出た[/b][/color]"
                          + $"  [color=#a9b3a8]（{e.Text ?? "介入"}）[/color]");
                await Delay(0.30);
                break;

            // 第145期 —— 転倒。**付与と消費は別の拍で来る**（付与はターン頭の
            // `ShufflerTrait`、消費は行動順ループの中）ので、2本とも出す。
            // `StatusSnapshot` には一度も載らない（写しは `OnTurnStart` より前に撮る）。
            case BattleEventKind.Stagger:
                target?.SetStatusIcon(StatusKeys.Stagger, e.Text == StaggerLabels.Fell);
                Color fallTint = StatusColor(StatusKeys.LabelOf(StatusKeys.Stagger));
                if (e.Text == StaggerLabels.Fell)
                {
                    _battleField.StaggerFall(target, fallTint);
                    if (e.ActorId is not null) _battleField.Link(actor, target, fallTint, "引きずり出した");
                    AppendLog($"  [color=#{fallTint.ToHtml(false)}][b]{NameOf(e.TargetId)} は前へ引きずり出されて転んだ[/b][/color]"
                              + $"  [color=#a9b3a8]（次の手番を失う）[/color]{WriterSuffix(e.ActorId, e.TargetId)}");
                    await Delay(0.34);
                }
                else
                {
                    _battleField.StaggerLost(target, fallTint);
                    AppendLog($"  [color=#{fallTint.ToHtml(false)}]{NameOf(e.TargetId)} は転んだまま手番を失った[/color]");
                    await Delay(0.30);
                }
                break;

            // 第146期 段0 —— 痺れ。転倒と同じ形で**付与と消費の2本**を出す。
            // 痺れは転倒と違ってターンをまたぐので `StatusSnapshot` には載る
            // ——欠けていたのは「いつ誰に付けられたか」と「その手番が実際に潰れたか」。
            case BattleEventKind.Stun:
                // 手番喪失は残量ゼロを意味しない。解除は次の残量通知で読む。
                if (e.Text == StunLabels.Struck) target?.SetStatusIcon(StatusKeys.Stun, true);
                Color stunTint = StatusColor(StatusKeys.LabelOf(StatusKeys.Stun));
                if (e.Text == StunLabels.Struck)
                {
                    if (e.ActorId is not null) _battleField.Link(actor, target, stunTint, "痺れさせた");
                    AppendLog($"  [color=#{stunTint.ToHtml(false)}][b]{NameOf(e.TargetId)} が痺れた[/b][/color]"
                              + $"  [color=#a9b3a8]（次の手番を失う）[/color]{WriterSuffix(e.ActorId, e.TargetId)}");
                    await Delay(0.26);
                }
                else
                {
                    AppendLog($"  [color=#{stunTint.ToHtml(false)}]{NameOf(e.TargetId)} は痺れたまま手番を失った[/color]");
                    await Delay(0.24);
                }
                break;

            // 第147期 —— 混乱。転倒・痺れと同じ形で**付与と発動の2本**を出すが、理由が違う:
            // 混乱した駒は**振る**ので `Attack` は出る——出ないのは「なぜ味方を殴ったのか」のほう。
            // 付与から発動まで何ターンも空きうるので、2本とも無いと因果が画面で繋がらない。
            case BattleEventKind.Confused:
                target?.SetStatusIcon(StatusKeys.Confused, e.Text == ConfusedLabels.Lost);
                Color madTint = StatusColor(StatusKeys.LabelOf(StatusKeys.Confused));
                if (e.Text == ConfusedLabels.Lost)
                {
                    if (e.ActorId is not null) _battleField.Link(actor, target, madTint, "正気を奪った");
                    AppendLog($"  [color=#{madTint.ToHtml(false)}][b]{NameOf(e.TargetId)} は正気を失った[/b][/color]"
                              + $"  [color=#a9b3a8]（次の攻撃を自軍へ向ける）[/color]{WriterSuffix(e.ActorId, e.TargetId)}");
                    await Delay(0.32);
                }
                else
                {
                    AppendLog($"  [color=#{madTint.ToHtml(false)}]{NameOf(e.TargetId)} は自軍へ振った[/color]");
                    await Delay(0.24);
                }
                break;

            // 封じは色と動きで示す。説明文はログにだけ残す。
            case BattleEventKind.Sealed:
                string sealWord = e.Text ?? "封じ";
                Color sealTint = BattlefieldView3D.RuleColor(sealWord);
                if (sealWord == SealedLabels.Hush)
                    await _battleField.ShowHushSeal(target);
                else if (sealWord == SealedLabels.Yoke && _shownYokeIndices.Add(eventIndex))
                    _battleField.ShowYokeSeal(target, e.Amount);
                else if (sealWord == SealedLabels.Drought)
                    _battleField.ShowDroughtSeal(target, e.Amount);
                AppendLog($"  [color=#{sealTint.ToHtml(false)}]{NameOf(e.TargetId)} は{SealLogText(sealWord, e.Amount)}[/color]"
                          + $"  [color=#a9b3a8]（{sealWord}）[/color]");
                break;

            case BattleEventKind.Heal:
                target?.SetHp(e.HpAfter);
                target?.AnimateHeal();
                // 第125期 3-c: 「回復はもっと緑文字ではっきり出したほうが良い」への直答。
                // **ダメージと同じ大きさ・同じ形**で出す（ナラ・ゴルムの還しに効く）。
                _battleField.HealPopup(target, e.Amount);
                // 第124期 3-h: 段2 で載った書き手から線を引く。
                _battleField.HealingLight(actor, target, e.Amount);
                AppendLog($"  [color=#{UiKit.Heal.ToHtml(false)}]＋{e.Amount} 回復[/color] "
                          + $"{NameOf(e.TargetId)}{WriterSuffix(e.ActorId, e.TargetId)}");
                await Delay(0.15);
                break;

            case BattleEventKind.Death:
                _battleField.PlayDeath(target, eventIndex == _finishSoundIndex);
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
                        def?.Pattern ?? AttackPattern.Single,
                        def?.Advances ?? true,
                        def?.Traits);
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
                    _battleField.RevivePawn(target);
                    _battleField.SealPawnRevived(target);
                    if (e.ActorId is not null) _battleField.Link(actor, target, UiKit.Heal, "繋ぎ直した");
                }
                AppendLog($"  [color=#{UiKit.Heal.ToHtml(false)}]{NameOf(e.TargetId)} が復帰[/color]"
                          + $"{WriterSuffix(e.ActorId, e.TargetId)}");
                await Delay(0.38);
                break;

            case BattleEventKind.StatusGain:
                if (target is not null && e.Text is { } statusKey)
                {
                    target.SetStatusIcon(statusKey, e.Amount > 0);
                    if (statusKey == StatusKeys.Burn) target.SetBurning(e.Amount > 0);
                    if (statusKey == StatusKeys.Poison && e.Amount > 0) target.SetPoisoned(true);
                    // 第124期 3-g: 「テロップは出たが効果量が分からない」（ノミ）への直答。
                    // **量を先に、通貨名を次に、書き手は線で出す**——札の中へ駒名を畳むと、
                    // 読みたい量が名前の長さに埋もれる（3-b と同じ理由）。
                    string label = DisplayStatusKey(statusKey);
                    Color tint = StatusColor(label);
                    if (StatusIconArt.KeyOf(statusKey) is null)
                        _battleField.Float(target, $"＋{e.Amount} {label}", tint, e.Amount >= 2);
                    if (e.ActorId is not null && e.ActorId != e.TargetId)
                        _battleField.Link(actor, target, tint, $"{label}を書いた");
                    AppendLog($"  [color=#{tint.ToHtml(false)}]＋{e.Amount} {label}[/color] → {NameOf(e.TargetId)}"
                              + $"{WriterSuffix(e.ActorId, e.TargetId)}");
                }
                await Delay(0.08);
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
                // 第125期 3-a: 「ゾトの爆発が全体攻撃に見えない」への直答。
                // **破裂は `Attack` を1件も出さない**（Q0-6）ので同時着弾（3-a・第124期）の
                // 経路に乗らず、`Pattern` が null の `Damage` が人数ぶん並ぶだけだった。
                if (ApplyBurstAtOnce(eventIndex, e) > 0) await Delay(0.34);
                await Delay(0.42);
                break;

            case BattleEventKind.Charge:
            {
                // 第125期 3-b: 「ドルガの一撃の重さが表現できていない」への直答。
                // **`Charge` は次の倍率・攻撃型・溜めの名前を全部持っている**
                // （`BattleEventKind.Charge` の明文）ので、予告は台本だけで書ける。
                // 溜めは画面上「何も起きないターン」なので、予告が無いとただの空白になる。
                string forecast = $"次 ×{e.Amount / 100.0:0.#} {UiKit.PatternLabel(e.Pattern ?? AttackPattern.Single)}";
                _battleField.BeginCharge(actor, e.Amount);
                AppendLog($"[color=#{UiKit.Gold.ToHtml(false)}]{NameOf(e.ActorId)} — {e.Text ?? "力を溜める"}"
                          + $"　（{forecast}）[/color]");
                await Delay(0.34);
                break;
            }

            case BattleEventKind.Skill:
                if (!e.Reaction) _battleField.ReleaseChargedSkill(actor);
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
        // 範囲攻撃は着弾を前へ寄せるので、直前の軛も同じ拍へ寄せる。
        if (_result is not null)
        for (int i = eventIndex - 1; i >= 0; i--)
        {
            BattleEvent prior = _result.Events[i];
            if (prior.Kind is BattleEventKind.Damage or BattleEventKind.Parry or BattleEventKind.Attack
                or BattleEventKind.TurnStart or BattleEventKind.Death) break;
            if (prior.Kind != BattleEventKind.Sealed || prior.Text != SealedLabels.Yoke
                || prior.TargetId != e.TargetId || prior.ActorId != e.ActorId) continue;
            if (_shownYokeIndices.Add(i)) _battleField.ShowYokeSeal(target, prior.Amount);
            break;
        }
        target?.SetHp(e.HpAfter);
        bool poison = _statusCauseByDamageIndex.TryGetValue(eventIndex, out string? status)
            && status == StatusKeys.LabelOf(StatusKeys.Poison);
        target?.AnimateHit(poison);
        // 毒・燃焼などの継続ダメージや自傷では金属の被弾音を鳴らさない。
        if (e.Amount > 0 && actor is not null && actor != target
            && !_statusCauseByDamageIndex.ContainsKey(eventIndex))
            _battleField.PlayHitSound(target);
        // 第125期 段2（§5-1 の 5）: **1発が分割されて中継された**ことを線で出す。
        // ゴルムの「耐久している感がない」への直答——中継の段はいままで
        // 「なぜかゴルムが殴られた」としか見えなかった。
        if (e.Relayed)
            _battleField.Split(
                _relayVictimByIndex.TryGetValue(eventIndex, out int victimId) ? _battleField.FindPawn(victimId) : null,
                target, e.Amount, "肩代わり", UiKit.Muted);
        (string source, Color sourceColor) = DamageSource(eventIndex, e, actor);
        _battleField.DamagePopup(target, e.Amount, source, sourceColor, e.Amount >= 25, withSource, poison);
        if (!poison)
            _battleField.Impact(target, sourceColor,
                                _statusCauseByDamageIndex.ContainsKey(eventIndex), e.FriendlyFire);
        AppendLog($"  [color=#{sourceColor.ToHtml(false)}]{source}[/color] → {NameOf(e.TargetId)}  "
                  + $"[color=#{(poison ? UiKit.Poison : UiKit.Hurt).ToHtml(false)}]−{e.Amount}[/color]");
    }

    private void ShowParry(BattleEvent e)
    {
        var defender = _battleField.FindPawn(e.TargetId);
        if (defender is not null) defender.AnimationSpeed = Math.Max(0.1, _speed);
        _battleField.Parry(_battleField.FindPawn(e.ActorId), defender);
        AppendLog($"  [color=#8ce6ff][b]{NameOf(e.TargetId)} が {NameOf(e.ActorId)} の一撃を受け流した[/b]（{e.Amount} を無効化）[/color]");
    }

    /// <summary>
    /// 同時着弾（第124期 3-a）。その一振りに<b>紐づく</b> Damage / Parry をまとめて描き、
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
            if (candidate.Kind is not (BattleEventKind.Damage or BattleEventKind.Parry)) continue;
            if (candidate.ActorId != attack.ActorId || candidate.Pattern != attack.Pattern) continue;
            if (!_batchedDamageIndices.Add(i)) continue;
            if (candidate.Kind == BattleEventKind.Parry) ShowParry(candidate);
            else ShowDamage(i, candidate, _battleField.FindPawn(candidate.ActorId),
                       _battleField.FindPawn(candidate.TargetId),
                       withSource: candidate.TargetId == attack.TargetId);
            landed++;
        }
        return landed;
    }

    /// <summary>
    /// 破裂を「全体に落ちた1発」として描く（第125期 3-a）。戻り値は描いた件数。
    ///
    /// <para><b>ゾトの破裂は <c>Attack</c> を1件も出さない</b>——敵全員・味方全員へ
    /// 1体ずつ <c>ApplyDamage</c> を呼ぶだけなので、台本には <c>Pattern</c> が <c>null</c> の
    /// <c>Damage</c> が人数ぶん並ぶ（Phase 0 Q0-6）。第124期の同時着弾は <c>Attack</c> を起点に
    /// 紐づけるので、この形はどちらの経路にも乗っていなかった。</para>
    ///
    /// <para><b>紐づけは構造だけで決める</b>（文字列は1文字も見ない・`LogKind` の原則）:
    /// その見せ場と<b>同じ書き手</b>の、<b>攻撃型を持たない</b> <c>Damage</c> を、
    /// 次の <c>TurnStart</c> / <c>Attack</c> / <c>Highlight</c> まで拾う。
    /// <b>2体以上に落ちたときだけ</b>「全体」として描く——1体なら普通の一撃と変わらない。</para>
    ///
    /// <para><b>台本は1件も並べ替えない。</b> 描く順を前へ寄せるだけで、
    /// <c>Death</c> や <c>StatusGain</c> は今までどおりその後に流れる。</para>
    /// </summary>
    private int ApplyBurstAtOnce(int highlightIndex, BattleEvent highlight)
    {
        if (_result is null || highlight.ActorId is null) return 0;

        var hits = new List<int>();
        for (int i = highlightIndex + 1; i < _result.Events.Count; i++)
        {
            BattleEvent candidate = _result.Events[i];
            if (candidate.Kind is BattleEventKind.TurnStart or BattleEventKind.Attack or BattleEventKind.Highlight) break;
            if (candidate.Kind is not (BattleEventKind.Damage or BattleEventKind.Parry)) continue;
            if (candidate.ActorId != highlight.ActorId || candidate.Pattern is not null) continue;
            if (candidate.Relayed) continue;   // 中継の段は別の絵（§5-1 の 5）
            hits.Add(i);
        }
        if (hits.Count < 2) return 0;

        _battleField.Burst(
            _battleField.FindPawn(highlight.ActorId),
            hits.Select(i => _battleField.FindPawn(_result.Events[i].TargetId)).OfType<BattlePawn3D>().ToList(),
            UiKit.Burn);

        int landed = 0;
        foreach (int i in hits)
        {
            if (!_burstDamageIndices.Add(i)) continue;
            BattleEvent damage = _result.Events[i];
            if (damage.Kind == BattleEventKind.Parry) ShowParry(damage);
            else ShowDamage(i, damage, _battleField.FindPawn(damage.ActorId), _battleField.FindPawn(damage.TargetId),
                       withSource: landed == 0);
            landed++;
        }
        return landed;
    }

    /// <summary>同・ログ側の一文。<b>評価の言葉は書かない</b>（起きたことだけ）。</summary>
    private static string SealLogText(string rule, int amount) => rule switch
    {
        SealedLabels.Hush => "ターン外に動けなかった",
        SealedLabels.Drought => $"回復が通らなかった（{amount} 点）",
        SealedLabels.Yoke => $"受けた一撃が 25 で切られた（{amount} 点ぶん）",
        _ => "封じられた",
    };

    /// <summary>
    /// 戦況ログに付ける「誰の仕業か」（第124期 3-h）。<b>書き手が居ないときは何も書かない</b>
    /// ——「盤面」と書くと、書き手が居ないことと書き手が盤面であることの区別が消える。
    /// </summary>
    private string WriterSuffix(int? actorId, int? targetId)
        => actorId is null || actorId == targetId
            ? ""
            : $"  [color=#{UiKit.Faint.ToHtml(false)}]← {NameOf(actorId)}[/color]";

    /// <summary>
    /// 拍を割り当てる前処理（第125期 段2）。<b>台本は1件も並べ替えない。</b>
    ///
    /// <para>規則は3つだけ:</para>
    /// <list type="number">
    /// <item><c>TurnStart</c> が来たら<b>誰の手番でもない時間</b>に戻る（主は居ない）。
    /// `BattleEngine.Run` は <c>EmitTurnStart</c> → <c>TickStatuses</c> → <b><c>OnTurnStart</c> の全駒ループ</b>
    /// → 行動順ループ、の順に走るので、ターン頭の一括（味方 19 体・Q0-5）は全部ここに落ちる。</item>
    /// <item>手番の外<b>ではない</b> <c>Attack</c> / <c>Skill</c> / <c>Charge</c> が来たら、
    /// その <c>ActorId</c> が<b>手番の主</b>になる。</item>
    /// <item><c>Reaction</c>（棘・仇討ち・軋み）・<c>Relayed</c>（巨躯・分かち）・
    /// <c>Intercept</c>（鎖の全段）は<b>手番の外</b>。</item>
    /// </list>
    ///
    /// <para><b>介入だけは攻撃の手前に出る</b>——<c>PerformAttack</c> は
    /// <c>SelectTargetCore</c> を先に呼ぶので、台本では <c>Intercept</c> が <c>Attack</c> より前に来る。
    /// そのままだと「前の駒の手番を止めた」ことになるので、<b>次の <c>Attack</c> を先読みして
    /// 主を先に立てる</b>。</para>
    ///
    /// <para><b>追い打ち（ハギ）はここでも手番の外にならない</b>——<c>OnAnyDeath</c> から
    /// <c>ctx.PerformAttack</c> を直に呼ぶので <c>Reaction</c> が立たず、台本の上では
    /// 普通の一振りと区別が付かない（`Models.cs` の明文。Phase 0 Q0-3 で今も正しいことを確かめた）。
    /// <b>この期では直さない</b>——包むと発火条件に触れる恐れがある（指示書 §0-4）。</para>
    /// </summary>
    private void IndexTimeline(IReadOnlyList<BattleEvent> events)
    {
        _beatByIndex.Clear();
        _ownerByIndex.Clear();
        _relayVictimByIndex.Clear();
        _shownBeat = Beat.TurnOpen;
        _shownOwner = -1;

        Beat current = Beat.TurnOpen;
        int owner = -1;
        for (int i = 0; i < events.Count; i++)
        {
            BattleEvent e = events[i];
            bool off = e.Reaction || e.Relayed || e.Kind == BattleEventKind.Intercept;

            if (e.Kind == BattleEventKind.TurnStart)
            {
                current = Beat.TurnOpen;
                owner = -1;
            }
            else if (e.Kind == BattleEventKind.Intercept)
            {
                // 介入は標的選択の中＝これから振る駒の手番の入口。主を先に立てる。
                if (NextAttacker(events, i) is { } next) { owner = next; current = Beat.InTurn; }
            }
            else if (!off
                     && e.Kind is BattleEventKind.Attack or BattleEventKind.Skill or BattleEventKind.Charge
                     && e.ActorId is { } actorId)
            {
                owner = actorId;
                current = Beat.InTurn;
            }
            else if (e.Kind == BattleEventKind.Stagger
                     && e.Text == StaggerLabels.Lost
                     && e.TargetId is { } staggeredId)
            {
                // 転倒で潰れた手番には Attack / Skill / Charge が1件も無い。
                // 消費イベント自身を入口にしないと、直前の駒の手番として表示される。
                owner = staggeredId;
                current = Beat.InTurn;
            }

            _beatByIndex.Add(off ? Beat.OffTurn : current);
            _ownerByIndex.Add(owner);

            if (e.Kind == BattleEventKind.Damage && e.Relayed && RelayVictim(events, i) is { } victim)
                _relayVictimByIndex[i] = victim;
        }
    }

    /// <summary>次に振る駒（手番の中の <c>Attack</c> の書き手）。同じターンの中だけを見る。</summary>
    private static int? NextAttacker(IReadOnlyList<BattleEvent> events, int from)
    {
        for (int i = from + 1; i < events.Count; i++)
        {
            BattleEvent e = events[i];
            if (e.Kind == BattleEventKind.TurnStart) return null;
            if (e.Kind != BattleEventKind.Attack) continue;
            return e.Reaction ? null : e.ActorId;
        }
        return null;
    }

    /// <summary>
    /// 中継された段の<b>本来の標的</b>（第125期 段2）。<c>ApplyDamage</c> は肩代わりを
    /// <b>元の駒の HP を引く前に</b>解決するので、台本では<b>中継の段が先・本来の標的が後</b>に来る。
    /// 同じ書き手の、中継でない次の <c>Damage</c> がそれ。
    /// </summary>
    private static int? RelayVictim(IReadOnlyList<BattleEvent> events, int from)
    {
        for (int i = from + 1; i < events.Count; i++)
        {
            BattleEvent e = events[i];
            if (e.Kind is BattleEventKind.TurnStart or BattleEventKind.Attack) return null;
            if (e.Kind != BattleEventKind.Damage || e.Relayed) continue;
            return e.ActorId == events[from].ActorId ? e.TargetId : null;
        }
        return null;
    }

    /// <summary>
    /// 拍の境目でだけ画面を変える（第125期 段2）。<b>ここでは待たない</b>
    /// ——間は `ApplyEvent` の switch の中だけに置く（そうしないと「1件あたりの間」が測れなくなる）。
    /// </summary>
    /// <returns>拍か主が変わったか。</returns>
    private bool EnterBeat(int index, BattleEvent e)
    {
        if (index >= _beatByIndex.Count) return false;
        Beat beat = _beatByIndex[index];
        int owner = _ownerByIndex[index];
        if (beat == _shownBeat && owner == _shownOwner) return false;

        BattlePawn3D? ownerPawn = owner >= 0 ? _battleField.FindPawn(owner) : null;
        Color ownerColor = ownerPawn?.Team == BattleContext.EnemyTeam ? UiKit.Enemy : UiKit.Player;

        switch (beat)
        {
            case Beat.TurnOpen:
                _battleField.SetTurnOwner(null, "▷ ターン頭 —— 誰の手番でもない時間", UiKit.Violet);
                AppendLog($"  [color=#{UiKit.Violet.ToHtml(false)}]▷ ターン頭（誰の手番でもない）[/color]");
                break;

            case Beat.InTurn:
                if (_shownBeat == Beat.OffTurn && owner == _shownOwner)
                {
                    _battleField.EndInterrupt($"▶ 手番: {NameOf(owner)}", ownerColor);
                    AppendLog($"  [color=#{UiKit.Faint.ToHtml(false)}]└ 手番へ戻る[/color]");
                }
                else
                {
                    _battleField.SetTurnOwner(ownerPawn, $"▶ 手番: {NameOf(owner)}", ownerColor);
                    AppendLog($"  [color=#{ownerColor.ToHtml(false)}]▶ {NameOf(owner)} の手番[/color]");
                }
                break;

            case Beat.OffTurn:
                // 主が先に立っていないと「誰の手番を止めたのか」が読めない。
                if (owner != _shownOwner && ownerPawn is not null)
                    _battleField.SetTurnOwner(ownerPawn, $"▶ 手番: {NameOf(owner)}", ownerColor);
                (string kind, Color tint) = OffTurnLabel(e);
                // 介入は駒の合図を `Divert` が出すので、ここでは帯だけにする（札を二重に出さない）。
                _battleField.BeginInterrupt(
                    e.Kind == BattleEventKind.Intercept ? null : _battleField.FindPawn(e.ActorId), kind, tint,
                    markActor: !e.Reaction);
                AppendLog($"  [color=#{tint.ToHtml(false)}]⚡ {kind}[/color]");
                break;
        }

        _shownBeat = beat;
        _shownOwner = owner;
        return true;
    }

    /// <summary>手番の外の種類（第125期 段2）。<b>台本の札だけで決まる。</b></summary>
    private static (string Kind, Color Tint) OffTurnLabel(BattleEvent e)
        => e.Kind == BattleEventKind.Intercept ? ("介入（狙いが逸れた）", UiKit.Gold)
         : e.Relayed ? ("肩代わり（1発が分けられた）", UiKit.Muted)
         : ("追加攻撃（手番へ割り込み）", UiKit.Gold);

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
            if (candidate.Kind is BattleEventKind.Damage or BattleEventKind.Parry
                && candidate.ActorId == attack.ActorId
                && candidate.Pattern == pattern
                && candidate.TargetId is { } targetId)
                ids.Add(targetId);
        }
        return ids.Select(id => _battleField.FindPawn(id)).OfType<BattlePawn3D>().ToList();
    }

    /// <summary>
    /// <see cref="BattleEventKind.Attack"/> を持たず、Damage / Parry から直接始まる追加攻撃か。
    /// 棘の範囲反撃は Reaction 付き Damage が複数並ぶので、先頭にだけカットインを出す。
    /// Death / StatusGain など反撃中に挟まる表示イベントは読み飛ばす。
    /// </summary>
    private bool StartsDirectReaction(int eventIndex, BattleEvent damage)
    {
        for (int i = eventIndex - 1; i >= 0; i--)
        {
            BattleEvent previous = _result!.Events[i];
            if (previous.Kind == BattleEventKind.TurnStart) return true;
            if (previous.Kind == BattleEventKind.Attack)
                return !(previous.Reaction && previous.ActorId == damage.ActorId);
            if (previous.Kind is BattleEventKind.Damage or BattleEventKind.Parry)
                return !(previous.Reaction && previous.ActorId == damage.ActorId);
        }
        return true;
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
        _shownYokeIndices.Clear();
        _burstDamageIndices.Clear();
        _shownBeat = Beat.TurnOpen;
        _shownOwner = -1;
        _battleLog.Clear();
        _battleField.BeginBattle(_battleOpening, EnemyCatalog.Stages[_stagePicker.Selected].Name, _stagePicker.Selected);
        _battleMusic.PlayWave(_stagePicker.Selected);
        _partyBar.Begin(_battleOpening);
        _partyBar.Sync(_battleField, -1);
        _partyBar.Visible = true;
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
        _battleMusic.Stop();
        _partyBar.Visible = false;
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
        if (_partyBar is not null && _battleMode) _partyBar.Visible = !_scorePanel.Visible;
        _score.Text = _scorePanel.Visible ? "戦績を閉じる (T)" : "戦績 (T)";
    }

    /// <summary>
    /// 作戦マップへ。<b>第169期から既定は検証用マップ 1-1</b>（<c>Map11Main.tscn</c>）。
    /// 既存の自由移動の作戦マップ（<c>CampaignMain.tscn</c>）から接敵して来たときだけ
    /// そちらへ戻す——戻り先は入場時（<c>_Ready</c>）に決めてあり、
    /// 戦闘の勝敗で <c>HasPendingEncounter</c> が落ちても揺れない。
    /// </summary>
    private void GoToCampaign()
    {
        ++_playToken;
        GetTree().ChangeSceneToFile(_returnScene);
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
