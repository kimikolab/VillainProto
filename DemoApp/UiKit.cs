using BattleCore;
using Godot;
using System;
using System.Collections.Generic;

public static class UiKit
{
    public static readonly Color Ink = Color.FromHtml("#f4f0dd");
    public static readonly Color Muted = Color.FromHtml("#a9b3a8");
    public static readonly Color Faint = Color.FromHtml("#6f7f76");
    public static readonly Color Panel = Color.FromHtml("#17221f");
    public static readonly Color Panel2 = Color.FromHtml("#22322b");
    public static readonly Color Line = Color.FromHtml("#3c5547");
    public static readonly Color Player = Color.FromHtml("#69bfe0");
    public static readonly Color Enemy = Color.FromHtml("#ee7962");
    public static readonly Color Gold = Color.FromHtml("#efc66a");
    public static readonly Color Heal = Color.FromHtml("#71d7a1");
    public static readonly Color Hurt = Color.FromHtml("#ff766b");
    public static readonly Color Violet = Color.FromHtml("#bf91f3");
    public static readonly Color Poison = Color.FromHtml("#a9e66f");
    public static readonly Color Burn = Color.FromHtml("#ff9a55");
    public static readonly Color Wound = Color.FromHtml("#ef6f91");
    public static readonly Color Shadow = new(0.02f, 0.04f, 0.035f, 0.86f);

    // 立ち絵は「駒 ID から規約で引く」——辞書に書かずにファイルの有無だけを見る（第124期 §6-1）。
    // 絵を足すのに .cs を触らないため。無ければ従来のアトラスへ落ちる。
    private static string PortraitPathOf(string key) => $"res://assets/portraits/{key}.png";
    private static string BattlePortraitPathOf(string key) => $"res://assets/portraits/battle/{key}_idle_right.png";

    // 存在の判定はファイルごとに1度だけ（見つからなかったことも憶える）。
    private static readonly Dictionary<string, string?> PortraitPathCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, string?> BattlePortraitPathCache = new(StringComparer.Ordinal);

    private static string? FindPortrait(string key)
    {
        if (PortraitPathCache.TryGetValue(key, out string? cached)) return cached;
        string path = PortraitPathOf(key);
        string? found = Godot.FileAccess.FileExists(path) ? path : null;
        PortraitPathCache[key] = found;
        return found;
    }

    private static string? FindBattlePortrait(string key)
    {
        if (BattlePortraitPathCache.TryGetValue(key, out string? cached)) return cached;
        string path = BattlePortraitPathOf(key);
        string? found = Godot.FileAccess.FileExists(path) ? path : null;
        BattlePortraitPathCache[key] = found;
        return found;
    }

    private static readonly Dictionary<string, Texture2D> PortraitCache = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, Texture2D> BattlePortraitCache = new(StringComparer.Ordinal);
    private static ShaderMaterial? _portraitCutoutMaterial;

    private static readonly Color[] UnitTints =
    {
        Color.FromHtml("#f7d7b1"), Color.FromHtml("#b8e5d0"), Color.FromHtml("#d7c4f1"),
        Color.FromHtml("#f2b8a8"), Color.FromHtml("#bad7f0"), Color.FromHtml("#d7e3a5"),
        Color.FromHtml("#e9c2dd"), Color.FromHtml("#cfd0bd")
    };

    public static StyleBoxFlat Box(Color background, Color? border = null, int width = 1, int radius = 10)
    {
        var box = new StyleBoxFlat
        {
            BgColor = background,
            CornerRadiusTopLeft = radius,
            CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius,
            CornerRadiusBottomRight = radius,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
        };
        if (border is { } c)
        {
            box.BorderColor = c;
            box.BorderWidthLeft = width;
            box.BorderWidthRight = width;
            box.BorderWidthTop = width;
            box.BorderWidthBottom = width;
        }
        return box;
    }

    public static Label Text(string value, int size = 14, Color? color = null)
    {
        var label = new Label { Text = value };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color ?? Ink);
        return label;
    }

    public static Button ActionButton(string value, Color? accent = null)
    {
        Color c = accent ?? Line;
        var button = new Button { Text = value, FocusMode = Control.FocusModeEnum.None };
        button.AddThemeStyleboxOverride("normal", Box(c.Darkened(0.48f), c, 1, 8));
        button.AddThemeStyleboxOverride("hover", Box(c.Darkened(0.32f), c.Lightened(0.14f), 1, 8));
        button.AddThemeStyleboxOverride("pressed", Box(c.Darkened(0.58f), c.Lightened(0.24f), 2, 8));
        button.AddThemeColorOverride("font_color", Ink);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        return button;
    }

    public static Texture2D LoadTexture(string path)
    {
        Image image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
        if (image.IsEmpty())
            throw new InvalidOperationException($"画像を読み込めません: {path}");
        return ImageTexture.CreateFromImage(image);
    }

    public static Texture2D Portrait(Texture2D atlas, string key)
    {
        if (FindPortrait(key) is string path)
        {
            if (!PortraitCache.TryGetValue(key, out Texture2D? portrait))
            {
                portrait = LoadTexture(path);
                PortraitCache[key] = portrait;
            }
            return portrait;
        }

        int index = (int)(StableHash(key) % 6);
        int w = atlas.GetWidth() / 3;
        int h = atlas.GetHeight() / 2;
        return new AtlasTexture
        {
            Atlas = atlas,
            Region = new Rect2((index % 3) * w, (index / 3) * h, w, h),
            FilterClip = true,
        };
    }

    public static Texture2D BattlePortrait(Texture2D atlas, string key)
    {
        if (FindBattlePortrait(key) is string path)
        {
            if (!BattlePortraitCache.TryGetValue(key, out Texture2D? portrait))
            {
                portrait = LoadTexture(path);
                BattlePortraitCache[key] = portrait;
            }
            return portrait;
        }

        return Portrait(atlas, key);
    }

    public static bool HasCustomPortrait(string key) => FindPortrait(key) is not null;
    public static bool HasCustomBattlePortrait(string key) => FindBattlePortrait(key) is not null;

    public static Color PortraitTint(string key, bool enemy = false)
        => HasCustomPortrait(key) ? Colors.White : Tint(key, enemy);

    /// <summary>
    /// 生成立ち絵の相対体格。元画像のピクセル数ではなく、この高さで戦場上の見た目を揃える。
    /// </summary>
    public static float PortraitWorldHeight(string key) => key switch
    {
        "rica" => 1.75f,
        "zoto" => 1.58f,
        "kado" => 2.40f,
        "sid" => 2.55f,
        "borg" => 2.65f,
        _ => 2.25f,
    };

    /// <summary>
    /// 透過キャンバス下端から足元までの余白率。足元を地面へ固定するため、採用画像ごとに保持する。
    /// </summary>
    public static float BattlePortraitBottomPaddingRatio(string key) => key switch
    {
        "rica" => 0.0600f,
        "sid" => 0.0040f,
        "borg" => 0.0384f,
        "zoto" => 0.1108f,
        "kado" => 0.0198f,
        _ => 0.0f,
    };

    public static Material? PortraitCanvasMaterial(string key)
    {
        if (!HasCustomPortrait(key)) return null;
        if (_portraitCutoutMaterial is not null) return _portraitCutoutMaterial;

        var shader = new Shader
        {
            Code = @"shader_type canvas_item;
void fragment() {
    vec4 c = texture(TEXTURE, UV);
    vec4 top_left = texture(TEXTURE, vec2(0.02, 0.02));
    vec4 top_right = texture(TEXTURE, vec2(0.98, 0.02));
    vec4 bottom_left = texture(TEXTURE, vec2(0.02, 0.98));
    vec4 bottom_right = texture(TEXTURE, vec2(0.98, 0.98));
    vec3 top_bg = mix(top_left.rgb, top_right.rgb, UV.x);
    vec3 bottom_bg = mix(bottom_left.rgb, bottom_right.rgb, UV.x);
    vec3 expected_bg = mix(top_bg, bottom_bg, UV.y);
    float silhouette = smoothstep(0.075, 0.19, distance(c.rgb, expected_bg));
    float corner_alpha = max(max(top_left.a, top_right.a), max(bottom_left.a, bottom_right.a));
    float has_alpha_background = 1.0 - step(0.08, corner_alpha);
    float mask = mix(silhouette, 1.0, has_alpha_background);
    COLOR = vec4(c.rgb, c.a * mask);
}"
        };
        _portraitCutoutMaterial = new ShaderMaterial { Shader = shader };
        return _portraitCutoutMaterial;
    }

    public static Color Tint(string key, bool enemy = false)
    {
        Color tint = UnitTints[StableHash(key) % (uint)UnitTints.Length];
        return enemy ? tint.Lerp(new Color(1.0f, 0.62f, 0.56f), 0.28f) : tint;
    }

    public static uint StableHash(string value)
    {
        uint hash = 2166136261;
        foreach (char c in value)
        {
            hash ^= c;
            hash *= 16777619;
        }
        return hash;
    }

    public static string PatternLabel(AttackPattern pattern) => pattern switch
    {
        AttackPattern.Sweep => "薙ぎ",
        AttackPattern.Pierce => "貫き",
        AttackPattern.All => "全体",
        _ => "単体",
    };

    /// <summary>
    /// 行の表示名（第123期）。<b>席から行を引くのは <see cref="FormationRules.RowOf"/> の1箇所だけ</b>で、
    /// ここは <see cref="Row"/> の3値に日本語を当てるだけ（席の表を手で並べない）。
    /// </summary>
    public static string RowLabel(Row row) => row switch
    {
        Row.Front => "前列",
        Row.Mid => "中列",
        _ => "後列",
    };

    /// <summary>席名と行名（例: <c>前1 / 前列</c>）。<see cref="FormationRules.SeatNames"/> から引く。</summary>
    public static string SeatLabel(int slot) =>
        $"{FormationRules.SeatNames[slot]} / {RowLabel(FormationRules.RowOf(slot))}";
}

public partial class RosterCard : PanelContainer
{
    private UnitDef? _def;
    private TextureRect _portrait = null!;
    private Label _name = null!;
    private Label _stats = null!;
    private Label _plus = null!;
    private Label _mark = null!;
    private StyleBoxFlat _style = null!;

    public Action<UnitDef>? Chosen;
    public Action<UnitDef>? Inspected;

    public UnitDef? Definition => _def;

    public void Configure(UnitDef def, Texture2D atlas)
    {
        _def = def;
        CustomMinimumSize = new Vector2(0, 102);
        MouseDefaultCursorShape = CursorShape.PointingHand;
        TooltipText = "クリックで編成 / ドラッグで席を指定";

        _style = UiKit.Box(new Color(0.08f, 0.12f, 0.105f, 0.94f), UiKit.Line, 1, 8);
        AddThemeStyleboxOverride("panel", _style);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        AddChild(row);

        var portraitFrame = new PanelContainer { CustomMinimumSize = new Vector2(76, 84) };
        portraitFrame.AddThemeStyleboxOverride("panel", UiKit.Box(UiKit.Shadow, UiKit.Line, 1, 7));
        _portrait = new TextureRect
        {
            Texture = UiKit.Portrait(atlas, def.Id),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            SelfModulate = UiKit.PortraitTint(def.Id),
            Material = UiKit.PortraitCanvasMaterial(def.Id),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        portraitFrame.AddChild(_portrait);
        row.AddChild(portraitFrame);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", 2);
        row.AddChild(text);

        var title = new HBoxContainer();
        _name = UiKit.Text(def.Name, 14, UiKit.Ink);
        _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.AddChild(_name);
        _mark = UiKit.Text("", 15, UiKit.Gold);
        title.AddChild(_mark);
        text.AddChild(title);

        _stats = UiKit.Text($"HP {def.MaxHp}   攻 {def.Attack}   速 {def.Speed}   {UiKit.PatternLabel(def.Pattern)}", 11, UiKit.Muted);
        text.AddChild(_stats);
        _plus = UiKit.Text(def.PlusText, 10, UiKit.Faint);
        _plus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _plus.MaxLinesVisible = 2;
        _plus.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        text.AddChild(_plus);
    }

    public void SetSelected(bool selected)
    {
        if (_def is null) return;
        _mark.Text = selected ? "◆" : "";
        _style.BgColor = selected ? new Color(0.12f, 0.22f, 0.18f, 0.98f) : new Color(0.08f, 0.12f, 0.105f, 0.94f);
        _style.BorderColor = selected ? UiKit.Gold : UiKit.Line;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_def is null) return;
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Inspected?.Invoke(_def);
            Chosen?.Invoke(_def);
            AcceptEvent();
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_def is null) return default;
        Inspected?.Invoke(_def);
        var preview = UiKit.Text(_def.Name, 14, UiKit.Ink);
        preview.AddThemeStyleboxOverride("normal", UiKit.Box(UiKit.Panel, UiKit.Gold, 1, 7));
        preview.CustomMinimumSize = new Vector2(170, 40);
        SetDragPreview(preview);
        return _def.Id;
    }
}

public partial class FormationSlot : PanelContainer
{
    private Texture2D _atlas = null!;
    private TextureRect _portrait = null!;
    private Label _seat = null!;
    private Label _name = null!;
    private Label _stats = null!;
    private Label _empty = null!;
    private StyleBoxFlat _style = null!;
    private UnitDef? _def;

    public int Slot { get; private set; }
    public Action<int>? Clicked;
    public Action<int>? RemoveRequested;
    public Action<int, string>? Dropped;

    public void Configure(int slot, Texture2D atlas)
    {
        Slot = slot;
        _atlas = atlas;
        CustomMinimumSize = new Vector2(138, 168);
        Size = CustomMinimumSize;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        _style = UiKit.Box(new Color(0.05f, 0.09f, 0.075f, 0.78f), new Color(UiKit.Player, 0.72f), 2, 16);
        AddThemeStyleboxOverride("panel", _style);

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 2);
        AddChild(stack);

        _seat = UiKit.Text(FormationRules.SeatNames[slot], 10, UiKit.Player.Lightened(0.2f));
        _seat.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(_seat);

        _portrait = new TextureRect
        {
            CustomMinimumSize = new Vector2(0, 92),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        stack.AddChild(_portrait);

        _name = UiKit.Text("", 11, UiKit.Ink);
        _name.HorizontalAlignment = HorizontalAlignment.Center;
        _name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        stack.AddChild(_name);
        _stats = UiKit.Text("", 9, UiKit.Muted);
        _stats.HorizontalAlignment = HorizontalAlignment.Center;
        stack.AddChild(_stats);

        _empty = UiKit.Text("＋ ここに配置", 11, new Color(UiKit.Player, 0.72f));
        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.VerticalAlignment = VerticalAlignment.Center;
        _empty.SetAnchorsPreset(LayoutPreset.FullRect);
        _empty.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_empty);
    }

    public void SetUnit(UnitDef? def)
    {
        _def = def;
        _empty.Visible = def is null;
        _portrait.Visible = def is not null;
        _name.Visible = def is not null;
        _stats.Visible = def is not null;
        if (def is null)
        {
            TooltipText = "ロスターからドラッグ、またはユニットを選んでクリック";
            _style.BorderColor = new Color(UiKit.Player, 0.62f);
            return;
        }

        _portrait.Texture = UiKit.Portrait(_atlas, def.Id);
        _portrait.SelfModulate = UiKit.PortraitTint(def.Id);
        _portrait.Material = UiKit.PortraitCanvasMaterial(def.Id);
        _name.Text = def.Name;
        _stats.Text = $"HP {def.MaxHp}  攻 {def.Attack}  速 {def.Speed}";
        TooltipText = $"{def.Name}\nドラッグで入れ替え / 右クリックで外す";
        _style.BorderColor = UiKit.Gold;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mouse) return;
        if (mouse.ButtonIndex == MouseButton.Right)
        {
            RemoveRequested?.Invoke(Slot);
            AcceptEvent();
        }
        else if (mouse.ButtonIndex == MouseButton.Left)
        {
            Clicked?.Invoke(Slot);
            AcceptEvent();
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (_def is null) return default;
        var preview = UiKit.Text(_def.Name, 14, UiKit.Ink);
        preview.CustomMinimumSize = new Vector2(170, 40);
        SetDragPreview(preview);
        return _def.Id;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
        => data.VariantType == Variant.Type.String && !string.IsNullOrWhiteSpace(data.AsString());

    public override void _DropData(Vector2 atPosition, Variant data)
        => Dropped?.Invoke(Slot, data.AsString());
}
