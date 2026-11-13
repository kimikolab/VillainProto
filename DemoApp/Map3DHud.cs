using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 3D のジオラマの遊ぶ UI（発注書 §2-4・仮の配置）。
// 左上 ステージ名・作戦ターン・目標 ／ 右上 金 ／ 左下 隊の札（名前・顔ぶれ・HP）／ 右下 刻を進める。
// <b>判定は 1 つも持たない</b>——中身は呼ぶ側（`Map11Main`）が渡し、押されたことは出来事で返すだけ（操作は今の 2D の画面と同じ口につながる）。
// 画面の基準は 1920×1080。文字は 1280×720 に縮めても読めるよう 20px 以上にしてある。検証の表示はここに混ぜない（`Map3DDebugLayer`）。

/// <summary>隊の札 1 枚に出す中身。</summary>
internal readonly record struct Map3DSquadCardInfo(
    int Index, string Name, string Role, IReadOnlyList<UnitDef> Faces, int Count, double HpPercent,
    string Where, string Order, bool Selected, bool Lost, bool Locked, string LockText);

public partial class Map3DHud : CanvasLayer
{
    internal const int Margin = 24;
    internal static readonly Color Ink = Color.FromHtml("#f4f0dd");
    internal static readonly Color Muted = Color.FromHtml("#b9bfb4");
    internal static readonly Color Gold = Color.FromHtml("#f1c96b");
    internal static readonly Color Back = new(0.055f, 0.065f, 0.085f, 0.88f);
    internal static readonly Color Edge = Color.FromHtml("#4c5a66");

    internal event Action<int>? SquadPressed;
    internal event Action? ReformPressed, DetailPressed, RunPressed, StepPressed, FoesPressed, HistoryPressed;
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
    private readonly PanelContainer _toastPanel = Panel();
    private readonly Label _toast = Text("", 24, Gold, bold: true);
    private readonly PanelContainer _hintPanel = Panel();
    private readonly Label _hint = Text("", 22, Ink);
    private readonly HBoxContainer _cards = new();
    private readonly List<Map3DSquadCard> _squadCards = new();
    private readonly Button _reform = Small("編成・呼ぶ・強化"), _detail = Small("配置を見る");
    private readonly Label _clockState = Text("", 22, Ink);
    private readonly ProgressBar _gauge = new() { MinValue = 0, MaxValue = 1, Step = 0, ShowPercentage = false, MouseFilter = Control.MouseFilterEnum.Ignore };
    private readonly Button _run = new() { FocusMode = Control.FocusModeEnum.None };
    private readonly Button _speed1 = Small("1×"), _speed2 = Small("2×"), _step = Small("1 刻だけ");

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

    internal static PanelContainer Panel()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Box(Back, Edge));
        return panel;
    }

    private static void Dress(Button button, Color accent, int size, bool bold = true)
    {
        button.AddThemeFontOverride("font", bold ? Map3DFonts.Bold : Map3DFonts.Regular);
        button.AddThemeFontSizeOverride("font_size", size);
        button.AddThemeStyleboxOverride("normal", Box(accent.Darkened(0.66f), accent, 2, 10, 8));
        button.AddThemeStyleboxOverride("hover", Box(accent.Darkened(0.50f), accent.Lightened(0.2f), 2, 10, 8));
        button.AddThemeStyleboxOverride("pressed", Box(accent.Darkened(0.74f), accent.Lightened(0.3f), 3, 10, 8));
        button.AddThemeStyleboxOverride("disabled", Box(new Color(0.12f, 0.13f, 0.15f, 0.8f), Edge.Darkened(0.3f), 2, 10, 8));
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_disabled_color", new Color(0.6f, 0.62f, 0.6f));
        button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
    }

    private static Button Small(string text)
    {
        var button = new Button { Text = text, FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(0, 46) };
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
    float v = smoothstep(0.42, 0.92, length(d));
    COLOR = vec4(0.05, 0.03, 0.09, v * 0.50);
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
        right.AddThemeConstantOverride("separation", 10);
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

        // ---- 上の真ん中: 出来事の 1 行（今の画面のトーストと同じ文） ----
        _toast.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _toast.HorizontalAlignment = HorizontalAlignment.Center;
        _toast.CustomMinimumSize = new Vector2(760, 0);
        _toastPanel.AddChild(_toast);
        _toastPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _toastPanel.Visible = false;
        _root.AddChild(_toastPanel);
        Pin(_toastPanel, Control.LayoutPreset.CenterTop);

        // ---- 左下: 隊の札 ----
        var left = new VBoxContainer();
        left.AddThemeConstantOverride("separation", 10);
        _hint.AutowrapMode = TextServer.AutowrapMode.Off;
        _hintPanel.AddChild(_hint);
        _hintPanel.AddThemeStyleboxOverride("panel", Box(Back, Gold.Darkened(0.35f), 2, 10, 8));
        _hintPanel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        _hintPanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        left.AddChild(_hintPanel);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
        row.AddThemeConstantOverride("separation", 12);
        _cards.AddThemeConstantOverride("separation", 12);
        row.AddChild(_cards);
        // 今の窓を開くボタン（組み直し・呼ぶ・強化 ／ 隊の配置の図）。
        var side = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkEnd };
        side.AddThemeConstantOverride("separation", 8);
        _reform.CustomMinimumSize = _detail.CustomMinimumSize = new Vector2(0, 56);
        side.AddChild(_detail);
        side.AddChild(_reform);
        row.AddChild(side);
        _detail.Pressed += () => DetailPressed?.Invoke();
        left.AddChild(row);
        _root.AddChild(left);
        Pin(left, Control.LayoutPreset.BottomLeft);
        _reform.Pressed += () => ReformPressed?.Invoke();

        // ---- 右下: 刻を進める ----
        PanelContainer clock = Panel();
        var clockCol = new VBoxContainer();
        clockCol.AddThemeConstantOverride("separation", 8);
        clockCol.AddChild(_clockState);
        _gauge.CustomMinimumSize = new Vector2(340, 10);
        var fill = new StyleBoxFlat { BgColor = Gold, CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5 };
        var groove = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.14f), CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5 };
        _gauge.AddThemeStyleboxOverride("fill", fill);
        _gauge.AddThemeStyleboxOverride("background", groove);
        clockCol.AddChild(_gauge);
        _run.CustomMinimumSize = new Vector2(340, 76);
        Dress(_run, Gold, 30);
        _run.Pressed += () => RunPressed?.Invoke();
        clockCol.AddChild(_run);
        var speeds = new HBoxContainer();
        speeds.AddThemeConstantOverride("separation", 8);
        foreach (Button b in new[] { _speed1, _speed2, _step }) { b.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill; speeds.AddChild(b); }
        _speed1.Pressed += () => SpeedPressed?.Invoke(false);
        _speed2.Pressed += () => SpeedPressed?.Invoke(true);
        _step.Pressed += () => StepPressed?.Invoke();
        clockCol.AddChild(speeds);
        clock.AddChild(clockCol);
        _root.AddChild(clock);
        Pin(clock, Control.LayoutPreset.BottomRight);
    }

    /// <summary>遊ぶ UI を出し入れする（今の窓＝編成の画面を開いているあいだは隠す）。</summary>
    internal void SetShown(bool shown) => Visible = shown;

    internal void SetHeader(string title, string turn, string goal)
    {
        _title.Text = title; _turn.Text = turn; _goal.Text = goal;
        _turn.Visible = turn.Length > 0; _goal.Visible = goal.Length > 0;
    }

    /// <summary>金（金の無い盤では null ＝ 出さない）。</summary>
    internal void SetGold(int? gold)
    {
        _goldPanel.Visible = gold is not null;
        _gold.Text = gold is { } g ? $"金 {g}" : "";
    }

    /// <summary>浮いた金の数字が飛んでいく先（画面の座標）。</summary>
    internal Vector2 GoldAnchor => _goldPanel.Visible ? _goldPanel.GetGlobalRect().GetCenter() : new Vector2(_root.Size.X - 120, 60);

    internal void SetWindows(bool foesOpen, bool historyOpen)
    {
        _foes.Text = foesOpen ? "敵部隊 ▴" : "敵部隊";
        _history.Text = historyOpen ? "戦闘の記録 ▴" : "戦闘の記録";
    }

    internal void SetToast(string text, Color color)
    {
        _toastPanel.Visible = text.Length > 0;
        if (_toast.Text == text) return;
        _toast.Text = text;
        _toast.AddThemeColorOverride("font_color", color);
        _toastPanel.ResetSize();
    }

    internal void SetHint(string text)
    {
        _hintPanel.Visible = text.Length > 0;
        _hint.Text = text;
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

    internal void SetReform(string text, bool enabled, string detail)
    {
        _reform.Text = text;
        _reform.Disabled = !enabled;
        _reform.TooltipText = enabled ? "" : "拠点にいる隊だけ組み直せます";
        _detail.Text = detail;
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

    /// <summary>遊ぶ UI が占める四角（盤の上の押す領域を重ねないため・検証の層も使う）。</summary>
    internal IEnumerable<Rect2> Blocks()
    {
        foreach (Node child in _root.GetChildren())
            if (child is Control { Visible: true } c && c.MouseFilter != Control.MouseFilterEnum.Ignore) yield return c.GetGlobalRect();
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

/// <summary>左下の隊の札 1 枚（名前・顔ぶれ・HP・いまの命令）。押すとその隊を選ぶ。</summary>
public partial class Map3DSquadCard : Button
{
    internal int Index { get; private set; }
    private readonly Label _name = Map3DHud.Text("", 24, Map3DHud.Ink, bold: true);
    private readonly Label _role = Map3DHud.Text("", 20, Map3DHud.Muted);
    private readonly HBoxContainer _faces = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _hp = Map3DHud.Text("", 20, Map3DHud.Ink);
    private readonly Label _where = Map3DHud.Text("", 20, Map3DHud.Muted);
    private readonly Label _order = Map3DHud.Text("", 20, Map3DHud.Gold);
    private Map3DSquadCardInfo _info;
    private string _faceKey = "";

    public Map3DSquadCard()
    {
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(322, 196);
        MouseDefaultCursorShape = CursorShape.PointingHand;
        ClipContents = true;
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 3);
        AddChild(col);
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect, margin: 12);
        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 10);
        _role.ClipText = true;
        _role.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _role.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        head.AddChild(_name); head.AddChild(_role);
        col.AddChild(head);
        _faces.AddThemeConstantOverride("separation", 4);
        _faces.CustomMinimumSize = new Vector2(0, 50);
        col.AddChild(_faces);
        col.AddChild(_hp);
        _where.ClipText = _order.ClipText = true;
        col.AddChild(_where);
        col.AddChild(_order);
    }

    internal void Set(in Map3DSquadCardInfo info)
    {
        _info = info;
        Index = info.Index;
        // まだ使えない隊の札は細くする（枠を開ける値段だけ）。
        CustomMinimumSize = new Vector2(info.Locked ? 214 : 322, 196);
        _role.Visible = !info.Locked;
        _hp.AutowrapMode = info.Locked ? TextServer.AutowrapMode.Arbitrary : TextServer.AutowrapMode.Off;
        _name.Text = info.Name;
        _role.Text = info.Role;
        Disabled = info.Lost;
        string key = string.Join(",", info.Faces.Select(u => u.Id));
        if (key != _faceKey)
        {
            _faceKey = key;
            foreach (Node child in _faces.GetChildren()) { _faces.RemoveChild(child); child.QueueFree(); }
            foreach (UnitDef unit in info.Faces)
            {
                var face = new UnitFaceIcon { CustomMinimumSize = new Vector2(50, 50) };
                face.SetUnit(unit);
                _faces.AddChild(face);
            }
        }
        _faces.Visible = info.Faces.Count > 0;
        _hp.Text = info.Lost ? "失われた" : info.Locked ? info.LockText : info.Count == 0 ? "空席（駒を配る）" : $"{info.Count} 枚　HP {info.HpPercent:F0}%";
        _where.Text = info.Lost || info.Locked ? "" : $"いま: {info.Where}";
        _order.Text = info.Lost || info.Locked ? "" : info.Order;
        Color edge = info.Selected ? Color.FromHtml("#ffd874") : info.Lost || info.Locked ? Map3DHud.Edge.Darkened(0.2f) : Color.FromHtml("#55d5b8").Darkened(0.25f);
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
        if (_info.Lost || _info.Locked || _info.Count == 0) return;
        var bar = new Rect2(14, Size.Y - 12, Size.X - 28, 5);
        DrawRect(bar, new Color(1, 1, 1, 0.14f));
        DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * (float)Math.Clamp(_info.HpPercent / 100.0, 0, 1), bar.Size.Y)), Color.FromHtml("#71d7a1"));
    }
}
