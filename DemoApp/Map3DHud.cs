using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 3D のジオラマの遊ぶ UI（発注書 §2-4・仮の配置）。
// 左上 ステージ名・作戦ターン・目標 ／ 右上 金 ／ 左下 隊の札（名前・顔ぶれ・HP）／ 右下 刻を進める。
// <b>判定は 1 つも持たない</b>——中身は呼ぶ側（`Map11Main`）が渡し、押されたことは出来事で返すだけ（操作は今の 2D の画面と同じ口につながる）。
// 画面の基準は 1920×1080。文字は 1280×720 に縮めても読めるよう 20px 以上にしてある。検証の表示はここに混ぜない（`Map3DDebugLayer`）。
// 第474期: 下の UI は画面の高さの 15% 以内（`BottomShare` を 3D のレイアウトの検査が見る）。隊の札は使える隊だけで、
// まだ開けていない隊は小さなボタン 1 つに畳む。「いま何をすればよいか」の 1 行は上の真ん中（出来事の 1 行と同じ場所）へ移した。

/// <summary>隊の札 1 枚に出す中身（使える隊だけ）。</summary>
internal readonly record struct Map3DSquadCardInfo(
    int Index, string Name, IReadOnlyList<UnitDef> Faces, int Count, double HpPercent, string Order, bool Selected, bool Lost);

public partial class Map3DHud : CanvasLayer
{
    internal const int Margin = 20;
    internal static readonly Color Ink = Color.FromHtml("#f4f0dd");
    internal static readonly Color Muted = Color.FromHtml("#b9bfb4");
    internal static readonly Color Gold = Color.FromHtml("#f1c96b");
    internal static readonly Color Back = new(0.055f, 0.075f, 0.095f, 0.90f);
    internal static readonly Color Edge = Color.FromHtml("#4c5a66");

    internal event Action<int>? SquadPressed;
    internal event Action? ReformPressed, DetailPressed, SlotsPressed, RunPressed, StepPressed, FoesPressed, HistoryPressed;
    internal event Action<bool>? SpeedPressed;

    private readonly Control _root = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
    /// <summary>駒の名札と、マスの「あと n」を置く層（遊ぶ UI の下）。</summary>
    internal Control Plates { get; } = new() { MouseFilter = Control.MouseFilterEnum.Ignore };

    private readonly Label _title = Text("", 30, Ink, bold: true);
    private readonly Label _turn = Text("", 24, Ink);
    private readonly Label _goal = Text("", 22, Gold);
    private readonly PanelContainer _goldPanel = Panel();
    private readonly Label _gold = Text("", 34, Gold, bold: true);
    private readonly Button _foes = Small("敵部隊"), _history = Small("戦闘の記録");
    private readonly PanelContainer _notePanel = Panel();
    private readonly Label _note = Text("", 22, Gold, bold: true);
    private string _toast = "", _hint = "";
    private Color _toastColor = Gold;
    private readonly HBoxContainer _left = new();
    private readonly HBoxContainer _cards = new();
    private readonly List<Map3DSquadCard> _squadCards = new();
    private readonly Button _slots = Small("＋ 隊の枠"), _reform = Small("編成・呼ぶ・強化"), _detail = Small("配置を見る");
    private readonly PanelContainer _clock = Panel();
    private readonly Label _clockState = Text("", 20, Ink);
    private readonly ProgressBar _gauge = new() { MinValue = 0, MaxValue = 1, Step = 0, ShowPercentage = false, MouseFilter = Control.MouseFilterEnum.Ignore };
    private readonly Button _run = new() { FocusMode = Control.FocusModeEnum.None };
    private readonly Button _speed1 = Small("1×"), _speed2 = Small("2×"), _step = Small("1 刻");

    internal static Label Text(string value, int size, Color color, bool bold = false)
    {
        var label = new Label { Text = value, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontOverride("font", bold ? Map3DFonts.Bold : Map3DFonts.Regular);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
        label.AddThemeConstantOverride("shadow_offset_y", 2);
        return label;
    }

    internal static StyleBoxFlat Box(Color back, Color edge, int width = 2, int radius = 12, int pad = 14) => new()
    {
        BgColor = back, BorderColor = edge,
        BorderWidthLeft = width, BorderWidthRight = width, BorderWidthTop = width, BorderWidthBottom = width,
        CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = pad + 4, ContentMarginRight = pad + 4, ContentMarginTop = pad, ContentMarginBottom = pad,
    };

    internal static PanelContainer Panel(int pad = 14)
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Box(Back, Edge, 2, 12, pad));
        return panel;
    }

    private static void Dress(Button button, Color accent, int size, bool bold = true)
    {
        button.AddThemeFontOverride("font", bold ? Map3DFonts.Bold : Map3DFonts.Regular);
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeStyleboxOverride("normal", Box(accent.Darkened(0.66f), accent, 2, 10, 6));
        button.AddThemeStyleboxOverride("hover", Box(accent.Darkened(0.50f), accent.Lightened(0.2f), 2, 10, 6));
        button.AddThemeStyleboxOverride("pressed", Box(accent.Darkened(0.74f), accent.Lightened(0.3f), 3, 10, 6));
        button.AddThemeStyleboxOverride("disabled", Box(new Color(0.12f, 0.13f, 0.15f, 0.8f), Edge.Darkened(0.3f), 2, 10, 6));
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", new Color(0.6f, 0.62f, 0.6f));
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
    }

    private static Button Small(string text)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 44) };
        Dress(button, Edge.Lightened(0.15f), 20);
        return button;
    }

    /// <summary>四隅（か上の真ん中）に寄せる。中身が増えたら隅から内側へ伸びる。</summary>
    private static void Pin(Control node, Control.LayoutPreset corner)
    {
        node.GrowHorizontal = corner is Control.LayoutPreset.TopRight or Control.LayoutPreset.BottomRight ? Control.GrowDirection.Begin
            : corner is Control.LayoutPreset.CenterTop ? Control.GrowDirection.Both : Control.GrowDirection.End;
        node.GrowVertical = corner is Control.LayoutPreset.BottomLeft or Control.LayoutPreset.BottomRight
            ? Control.GrowDirection.Begin : Control.GrowDirection.End;
        node.SetAnchorsAndOffsetsPreset(corner, Control.LayoutPresetMode.Minsize, Margin);
    }

    public override void _Ready()
    {
        Layer = 0;
        // 画面の縁を少し暗く落とす（夕暮れ・ヴィラン側の話）。入力は受けない。
        var vignette = new ColorRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new ShaderMaterial
            {
                Shader = new Shader
                {
                    Code = @"shader_type canvas_item;
void fragment() {
    vec2 d = (UV - vec2(0.5)) * vec2(1.0, 1.12);
    float v = smoothstep(0.56, 1.0, length(d));
    COLOR = vec4(0.04, 0.06, 0.09, v * 0.30);
}",
                },
            },
        };
        AddChild(vignette);
        vignette.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(Plates);
        Plates.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        // ---- 左上: ステージ名・作戦ターン・目標 ----
        PanelContainer head = Panel();
        var headCol = new VBoxContainer();
        headCol.AddThemeConstantOverride("separation", 2);
        headCol.AddChild(_title); headCol.AddChild(_turn); headCol.AddChild(_goal);
        head.AddChild(headCol);
        _root.AddChild(head);
        Pin(head, Control.LayoutPreset.TopLeft);

        // ---- 右上: 金 ＋ 今の窓を開くボタン ----
        var right = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
        right.AddThemeConstantOverride("separation", 8);
        var goldRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        goldRow.AddThemeConstantOverride("separation", 10);
        goldRow.AddChild(new Map3DCoin { CustomMinimumSize = new Vector2(40, 40), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });
        goldRow.AddChild(_gold);
        _goldPanel.AddChild(goldRow);
        _goldPanel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
        right.AddChild(_goldPanel);
        var windows = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        windows.AddThemeConstantOverride("separation", 8);
        windows.AddChild(_foes); windows.AddChild(_history);
        right.AddChild(windows);
        _root.AddChild(right);
        Pin(right, Control.LayoutPreset.TopRight);
        _foes.Pressed += () => FoesPressed?.Invoke();
        _history.Pressed += () => HistoryPressed?.Invoke();

        // ---- 上の真ん中: 出来事の 1 行（今の画面のトーストと同じ文）。出来事が無いあいだは「いま何をすればよいか」の 1 行 ----
        _note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _note.HorizontalAlignment = HorizontalAlignment.Center;
        _note.CustomMinimumSize = new Vector2(720, 0);
        _notePanel.AddThemeStyleboxOverride("panel", Box(Back, Edge, 2, 12, 8));
        _notePanel.AddChild(_note);
        _notePanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _notePanel.Visible = false;
        _root.AddChild(_notePanel);
        Pin(_notePanel, Control.LayoutPreset.CenterTop);

        // ---- 左下: 隊の札（使える隊だけ）＋ 隊の枠 ＋ 今の窓を開くボタン ----
        _left.AddThemeConstantOverride("separation", 10);
        _cards.AddThemeConstantOverride("separation", 10);
        _left.AddChild(_cards);
        var side = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
        side.AddThemeConstantOverride("separation", 6);
        var sideTop = new HBoxContainer();
        sideTop.AddThemeConstantOverride("separation", 6);
        sideTop.AddChild(_detail);
        sideTop.AddChild(_slots);
        side.AddChild(sideTop);
        side.AddChild(_reform);
        _left.AddChild(side);
        _root.AddChild(_left);
        Pin(_left, Control.LayoutPreset.BottomLeft);
        _reform.Pressed += () => ReformPressed?.Invoke();
        _detail.Pressed += () => DetailPressed?.Invoke();
        _slots.Pressed += () => SlotsPressed?.Invoke();

        // ---- 右下: 刻を進める ----
        var clockRow = new HBoxContainer();
        clockRow.AddThemeConstantOverride("separation", 10);
        _run.CustomMinimumSize = new Vector2(250, 96);
        Dress(_run, Gold, 28);
        _run.Pressed += () => RunPressed?.Invoke();
        clockRow.AddChild(_run);
        var clockCol = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        clockCol.AddThemeConstantOverride("separation", 6);
        clockCol.AddChild(_clockState);
        _gauge.CustomMinimumSize = new Vector2(196, 8);
        var fill = new StyleBoxFlat { BgColor = Gold, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
        var groove = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.14f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
        _gauge.AddThemeStyleboxOverride("fill", fill);
        _gauge.AddThemeStyleboxOverride("background", groove);
        clockCol.AddChild(_gauge);
        var speeds = new HBoxContainer();
        speeds.AddThemeConstantOverride("separation", 6);
        foreach (Button b in new[] { _speed1, _speed2, _step }) { b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; speeds.AddChild(b); }
        _speed1.Pressed += () => SpeedPressed?.Invoke(false);
        _speed2.Pressed += () => SpeedPressed?.Invoke(true);
        _step.Pressed += () => StepPressed?.Invoke();
        clockCol.AddChild(speeds);
        clockRow.AddChild(clockCol);
        _clock.AddThemeStyleboxOverride("panel", Box(Back, Edge, 2, 12, 8));
        _clock.AddChild(clockRow);
        _root.AddChild(_clock);
        Pin(_clock, Control.LayoutPreset.BottomRight);
    }

    /// <summary>遊ぶ UI を出し入れする（今の窓＝編成の画面を開いているあいだは隠す）。</summary>
    internal void SetShown(bool shown) => Visible = shown;

    internal void SetHeader(string title, string turn, string goal)
    {
        _title.Text = title; _turn.Text = turn; _goal.Text = goal;
        _turn.Visible = turn.Length > 0; _goal.Visible = goal.Length > 0;
    }

    /// <summary>金（金の無い盤では null ＝ 出さない）。</summary>
    private bool _goldNag;
    private float _goldClock;

    /// <summary>金の札をゆっくり光らせる（第478期: 金が貯まっているのに使っていないあいだ）。`Map11Main` が毎フレーム呼ぶ。</summary>
    internal void PulseGold(float delta)
    {
        if (!_goldNag) { if (_goldPanel.SelfModulate != Colors.White) _goldPanel.SelfModulate = Colors.White; return; }
        _goldClock += delta;
        float wave = 0.5f + 0.5f * Mathf.Sin(_goldClock * 4.2f);
        _goldPanel.SelfModulate = new Color(1f + 1.1f * wave, 1f + 0.8f * wave, 1f + 0.1f * wave);
    }

    internal void SetGold(int? gold, bool spend = false, bool nag = false)
    {
        _goldNag = nag && gold is not null;
        _goldPanel.Visible = gold is not null;
        // 第477期: 金を使える場所に隊がいて、使い道（呼び戻す・強化）があるときの印。
        _gold.Text = gold is { } g ? $"金 {g}" + (nag ? "　◆ 貯まっています" : spend ? "　◆ 使える" : "") : "";
    }

    /// <summary>浮いた金の数字が飛んでいく先（画面の座標）。</summary>
    internal Vector2 GoldAnchor => _goldPanel.Visible ? _goldPanel.GetGlobalRect().GetCenter() : new Vector2(_root.Size.X - 120, 60);

    internal void SetWindows(bool foesOpen, bool historyOpen)
    {
        _foes.Text = foesOpen ? "敵部隊 ▴" : "敵部隊";
        _history.Text = historyOpen ? "戦闘の記録 ▴" : "戦闘の記録";
    }

    /// <summary>出来事の 1 行（空なら <see cref="SetHint"/> の 1 行が出る）。</summary>
    internal void SetToast(string text, Color color) { _toast = text; _toastColor = color; ShowNote(); }

    /// <summary>いま何をすればよいかの 1 行（出来事の 1 行が出ているあいだは隠れる）。</summary>
    internal void SetHint(string text) { _hint = text; ShowNote(); }

    private void ShowNote()
    {
        bool toast = _toast.Length > 0;
        string text = toast ? _toast : _hint;
        _notePanel.Visible = text.Length > 0;
        if (_note.Text == text) return;
        _note.Text = text;
        _note.AddThemeColorOverride("font_color", toast ? _toastColor : Ink);
        _notePanel.ResetSize();
    }

    internal void SetSquads(IReadOnlyList<Map3DSquadCardInfo> squads)
    {
        while (_squadCards.Count < squads.Count)
        {
            var card = new Map3DSquadCard();
            card.Pressed += () => SquadPressed?.Invoke(card.Index);
            _cards.AddChild(card);
            _squadCards.Add(card);
        }
        for (int i = 0; i < _squadCards.Count; i++)
        {
            _squadCards[i].Visible = i < squads.Count;
            if (i < squads.Count) _squadCards[i].Set(squads[i]);
        }
    }

    /// <summary>
    /// 今の窓を開くボタン。<paramref name="slots"/> はまだ開けていない隊の枠の 1 行（「＋ 隊の枠（金 8）」・無ければ空 ＝ ボタンを出さない）。
    /// </summary>
    internal void SetReform(string text, bool enabled, string detail, string slots)
    {
        _reform.Text = text;
        _reform.Disabled = !enabled;
        _reform.TooltipText = enabled ? "" : "拠点にいる隊だけ組み直せます";
        _detail.Text = detail;
        _slots.Visible = slots.Length > 0;
        _slots.Text = slots;
        _slots.TooltipText = "まだ使えない隊の枠。編成の窓で、金を払って開けます";
    }

    internal void SetClock(string run, bool busy, bool clockOn, bool fast, double fraction, string state)
    {
        _run.Text = run;
        _run.Disabled = _step.Disabled = busy;
        _speed1.Visible = _speed2.Visible = clockOn;
        _gauge.Visible = clockOn;
        _gauge.Value = fraction;
        _clockState.Text = state;
        _speed1.Modulate = !fast ? new Color(1.0f, 0.90f, 0.62f) : new Color(1, 1, 1, 0.6f);
        _speed2.Modulate = fast ? new Color(1.0f, 0.90f, 0.62f) : new Color(1, 1, 1, 0.6f);
    }

    /// <summary>隊の札と「刻を進める」の四角（押す操作の確かめが使う）。</summary>
    internal Rect2 CardRect(int index) => _squadCards[index].GetGlobalRect();
    internal Rect2 RunRect => _run.GetGlobalRect();
    internal Rect2 FoesRect => _foes.GetGlobalRect();
    internal Rect2 StepRect => _step.GetGlobalRect();
    internal bool StepDisabled => _step.Disabled;
    /// <summary>隊の札の命令の文が切れていないか（3D のレイアウトの検査が見る）。</summary>
    internal bool OrdersFit => _squadCards.Where(c => c.Visible).All(c => c.OrderFits);

    /// <summary>下の UI（隊の札・刻を進める）が画面の高さに占める割合（3D のレイアウトの検査が 15% 以内を見る）。</summary>
    internal float BottomShare
    {
        get
        {
            float top = Math.Min(_left.GetGlobalRect().Position.Y, _clock.GetGlobalRect().Position.Y);
            return (_root.Size.Y - top) / Math.Max(1, _root.Size.Y);
        }
    }

    /// <summary>遊ぶ UI が占める四角（名札を重ねないため・3D のレイアウトの検査も使う）。</summary>
    internal IEnumerable<Rect2> Blocks()
    {
        foreach (Node child in _root.GetChildren())
            if (child is Control { Visible: true } c) yield return c.GetGlobalRect();
    }
}

/// <summary>金の印（丸い硬貨）。</summary>
public partial class Map3DCoin : Control
{
    public Map3DCoin() { MouseFilter = MouseFilterEnum.Ignore; }
    public override void _Draw()
    {
        Vector2 c = Size / 2;
        float r = Math.Min(Size.X, Size.Y) / 2 - 2;
        DrawCircle(c, r, Color.FromHtml("#8a6a1e"));
        DrawCircle(c, r - 3, Color.FromHtml("#f1c96b"));
        DrawArc(c, r - 8, 0, Mathf.Tau, 32, Color.FromHtml("#b88a2a"), 2, true);
    }
}

/// <summary>左下の隊の札 1 枚（名前・枚数と HP・顔ぶれ・いまの命令）。押すとその隊を選ぶ。</summary>
public partial class Map3DSquadCard : Button
{
    internal int Index { get; private set; }
    internal bool OrderFits => _order.GetLineCount() <= 2;
    private readonly Label _name = Map3DHud.Text("", 22, Map3DHud.Ink, bold: true);
    private readonly Label _hp = Map3DHud.Text("", 20, Map3DHud.Ink);
    private readonly HBoxContainer _faces = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _order = Map3DHud.Text("", 18, Map3DHud.Gold);
    private Map3DSquadCardInfo _info;
    private string _faceKey = "";

    public Map3DSquadCard()
    {
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(326, 138);
        MouseDefaultCursorShape = CursorShape.PointingHand;
        ClipContents = true;
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 1);
        AddChild(col);
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, margin: 8);
        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 12);
        _hp.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _hp.HorizontalAlignment = HorizontalAlignment.Right;
        head.AddChild(_name); head.AddChild(_hp);
        col.AddChild(head);
        _faces.AddThemeConstantOverride("separation", 4);
        _faces.CustomMinimumSize = new Vector2(0, 40);
        col.AddChild(_faces);
        // 第476期（見せ方 9）: 命令の文は切らずに 2 行まで折る（「北の道 2 マス目 で 峠の関 と接敵（あと 1）」が切れていた）。
        _order.AutowrapMode = TextServer.AutowrapMode.Arbitrary;
        _order.MaxLinesVisible = 2;
        _order.AddThemeConstantOverride("line_spacing", -2);
        // 第478期: 高さは 2 行ぶんで固定する（3 行以上に折れる文で札が伸びて、下の UI の高さが変わらないように）。
        _order.ClipText = true;
        _order.CustomMinimumSize = new Vector2(300, 46);
        col.AddChild(_order);
    }

    internal void Set(in Map3DSquadCardInfo info)
    {
        _info = info;
        Index = info.Index;
        _name.Text = info.Name;
        Disabled = info.Lost;
        string key = string.Join(",", info.Faces.Select(u => u.Id));
        if (key != _faceKey)
        {
            _faceKey = key;
            foreach (Node child in _faces.GetChildren()) { _faces.RemoveChild(child); child.QueueFree(); }
            foreach (UnitDef unit in info.Faces)
            {
                var face = new UnitFaceIcon { CustomMinimumSize = new Vector2(40, 40) };
                face.SetUnit(unit);
                _faces.AddChild(face);
            }
        }
        _hp.Text = info.Lost ? "失われた" : info.Count == 0 ? "空席" : $"{info.Count} 枚　HP {info.HpPercent:F0}%";
        _order.Text = info.Lost ? "" : info.Count == 0 ? "「編成・呼ぶ・強化」で駒を配る" : info.Order;
        Color edge = info.Selected ? Color.FromHtml("#ffd874") : info.Lost ? Map3DHud.Edge.Darkened(0.2f) : Color.FromHtml("#55d5b8").Darkened(0.25f);
        Color back = info.Selected ? new Color(0.13f, 0.15f, 0.12f, 0.94f) : Map3DHud.Back;
        AddThemeStyleboxOverride("normal", Map3DHud.Box(back, edge, info.Selected ? 3 : 2, 12, 8));
        AddThemeStyleboxOverride("hover", Map3DHud.Box(back.Lightened(0.06f), edge.Lightened(0.2f), info.Selected ? 3 : 2, 12, 8));
        AddThemeStyleboxOverride("pressed", Map3DHud.Box(back.Darkened(0.2f), edge.Lightened(0.3f), 3, 12, 8));
        AddThemeStyleboxOverride("disabled", Map3DHud.Box(new Color(0.08f, 0.08f, 0.09f, 0.8f), Map3DHud.Edge.Darkened(0.4f), 2, 12, 8));
        QueueRedraw();
    }

    public override void _Draw()
    {
        // HP の棒（札の下の縁に沿って）。
        if (_info.Lost || _info.Count == 0) return;
        var bar = new Rect2(12, Size.Y - 9, Size.X - 24, 4);
        DrawRect(bar, new Color(1, 1, 1, 0.14f));
        DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * (float)Math.Clamp(_info.HpPercent / 100.0, 0, 1), bar.Size.Y)), Color.FromHtml("#71d7a1"));
    }
}
