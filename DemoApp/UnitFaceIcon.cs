using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>地図・手札・接敵・結果で共用する顔。味方は既存の立ち絵を切り出し、敵は専用の顔を使う。原本には書き込まない。</summary>
public partial class UnitFaceIcon : Control
{
    // 画像幅・高さに対する顔の中心、画像幅に対する切り出し幅。絵の構図だけを持つ。
    private static readonly Dictionary<string, Vector3> Framing = new(StringComparer.Ordinal)
    {
        ["ban"] = new(.50f, .09f, .32f), ["basa"] = new(.48f, .16f, .32f),
        ["beni"] = new(.50f, .10f, .30f), ["borg"] = new(.51f, .12f, .34f),
        ["chiri"] = new(.49f, .13f, .37f), ["doha"] = new(.53f, .12f, .35f),
        ["dolga"] = new(.50f, .11f, .34f), ["egu"] = new(.48f, .16f, .34f),
        ["ere"] = new(.48f, .11f, .30f), ["fodder"] = new(.55f, .38f, .60f),
        ["gald"] = new(.50f, .10f, .32f), ["gan"] = new(.52f, .11f, .34f),
        ["gare"] = new(.48f, .13f, .32f), ["golm"] = new(.49f, .20f, .38f),
        ["guza"] = new(.50f, .18f, .42f), ["hagi"] = new(.47f, .16f, .33f),
        ["hane"] = new(.50f, .17f, .34f), ["hibi"] = new(.51f, .11f, .32f),
        ["hisa"] = new(.46f, .11f, .34f), ["hiyo"] = new(.48f, .14f, .35f),
        ["hota"] = new(.49f, .15f, .30f), ["kado"] = new(.48f, .09f, .34f),
        ["kari"] = new(.50f, .10f, .30f), ["kata"] = new(.52f, .12f, .32f),
        ["kubi"] = new(.49f, .13f, .36f), ["kugu"] = new(.51f, .10f, .30f),
        ["lili"] = new(.48f, .12f, .34f), ["mio"] = new(.47f, .12f, .34f),
        ["mudo"] = new(.52f, .34f, .50f), ["mug"] = new(.50f, .50f, .72f),
        ["nara"] = new(.51f, .12f, .34f), ["nel"] = new(.49f, .13f, .34f),
        ["nomi"] = new(.48f, .11f, .32f), ["rau"] = new(.47f, .15f, .38f),
        ["rica"] = new(.40f, .12f, .36f), ["sasa"] = new(.49f, .13f, .34f),
        ["sekki"] = new(.53f, .20f, .42f), ["sero"] = new(.51f, .11f, .32f),
        ["shiga"] = new(.50f, .12f, .32f), ["shio"] = new(.59f, .13f, .34f),
        ["sid"] = new(.51f, .17f, .36f), ["som"] = new(.50f, .11f, .32f),
        ["sora"] = new(.50f, .11f, .32f), ["spore"] = new(.50f, .50f, .72f),
        ["susu"] = new(.49f, .11f, .32f), ["tome"] = new(.49f, .12f, .34f),
        ["tou"] = new(.51f, .12f, .34f), ["tsugi"] = new(.48f, .19f, .34f),
        ["utsu"] = new(.49f, .11f, .32f), ["vel"] = new(.51f, .15f, .36f),
        ["vio"] = new(.68f, .24f, .43f), ["wata"] = new(.49f, .13f, .34f),
        ["yomi"] = new(.50f, .12f, .34f), ["zan"] = new(.51f, .13f, .32f),
        ["zoto"] = new(.50f, .48f, .68f),
    };
    private static readonly Dictionary<string, Texture2D?> Faces = new(StringComparer.Ordinal);
    public string? UnitId { get; private set; }
    public Texture2D? FaceTexture { get; private set; }
    public string Mark { get; private set; } = "—";
    public Color Tint { get; private set; } = UiKit.Player;

    /// <summary>縁の色だけを替える（第456期・勇者の札の金色の縁・<see cref="SetUnit"/> の後に呼ぶ）。</summary>
    public void SetAccent(Color color) { Tint = color; QueueRedraw(); }

    public UnitFaceIcon() { MouseFilter = MouseFilterEnum.Ignore; CustomMinimumSize = new(40, 40); }

    public void SetUnit(UnitDef? unit, bool enemy = false, string? wave = null)
    {
        UnitId = unit?.Id;
        Tint = enemy ? UiKit.Enemy : UiKit.Player;
        FaceTexture = unit is null ? null : TextureFor(unit.Id);
        Mark = wave ?? (unit is null ? "—" : StringInfo.GetNextTextElement(unit.Name));
        TooltipText = unit?.Name ?? "空席";
        QueueRedraw();
    }

    public static Texture2D? TextureFor(string id)
    {
        if (Faces.TryGetValue(id, out var cached)) return cached;
        string portraitId = id switch
        {
            "recruit_g" => "recruit", "archer_g" => "archer", "priest_g" => "priest",
            "knight_g" or "knight_v" or "knight_slander" => "knight", "axeman_v" => "axeman", _ => id,
        };
        string source = $"res://assets/map/enemies/{portraitId}.png";
        bool faceAsset = Godot.FileAccess.FileExists(source);
        if (!faceAsset) source = $"res://assets/portraits/{id}.png";
        if (!Godot.FileAccess.FileExists(source))
            source = $"res://assets/portraits/battle/{id}_idle_right.png";
        if (!Godot.FileAccess.FileExists(source)) return Faces[id] = null;
        using Image original = Image.LoadFromFile(ProjectSettings.GlobalizePath(source));
        if (original.IsEmpty()) return Faces[id] = null;
        Vector3 frame = faceAsset ? new(.5f, .5f, 1) : Framing.GetValueOrDefault(id, new Vector3(.5f, .14f, .36f));
        int side = Math.Max(1, (int)(original.GetWidth() * frame.Z));
        side = Math.Min(side, Math.Min(original.GetWidth(), original.GetHeight()));
        int x = Math.Clamp((int)(original.GetWidth() * frame.X) - side / 2, 0, original.GetWidth() - side);
        int y = Math.Clamp((int)(original.GetHeight() * frame.Y) - side / 2, 0, original.GetHeight() - side);
        using Image crop = original.GetRegion(new Rect2I(x, y, side, side));
        crop.Convert(Image.Format.Rgba8);
        crop.Resize(128, 128, Image.Interpolation.Lanczos);
        for (int py = 0; py < 128; py++)
            for (int px = 0; px < 128; px++)
            {
                Color color = crop.GetPixel(px, py);
                // 端は1pxでなじませる。クロマキーの立ち絵も顔の周りだけ透かす。
                float edge = Math.Clamp(63.5f - new Vector2(px - 63.5f, py - 63.5f).Length(), 0, 1);
                if (color.G > .7f && color.R < .35f && color.B < .35f) edge = 0;
                color.A *= edge;
                crop.SetPixel(px, py, color);
            }
        crop.GenerateMipmaps();
        return Faces[id] = ImageTexture.CreateFromImage(crop);
    }

    public override void _Draw()
    {
        float diameter = Math.Min(Size.X, Size.Y) - 4;
        Vector2 center = Size / 2;
        DrawCircle(center, diameter / 2, Tint.Darkened(.76f));
        if (FaceTexture is { } face) DrawTextureRect(face, new Rect2(center - Vector2.One * diameter / 2, Vector2.One * diameter), false);
        else
        {
            Font font = GetThemeDefaultFont();
            int size = (int)(diameter * .44f);
            Vector2 text = font.GetStringSize(Mark, fontSize: size);
            DrawString(font, center + new Vector2(-text.X / 2, (font.GetAscent(size) - font.GetDescent(size)) / 2), Mark,
                fontSize: size, modulate: UiKit.Ink);
        }
        DrawArc(center, diameter / 2, 0, Mathf.Tau, 64, Tint, 2, true);
    }
}

/// <summary>部隊を表す1枚の札。人数とHPは呼び出し側から受け取り、計算しない。</summary>
public partial class MapUnitBadge : Button
{
    public UnitFaceIcon Face { get; } = new();
    public int Count { get; private set; }
    public double HpPercent { get; private set; }
    private readonly Label _count = UiKit.Text("", 20);
    private readonly Label _name = UiKit.Text("", 12);
    private readonly Label _notice = UiKit.Text("", 13);
    private Color _color;

    public MapUnitBadge()
    {
        Flat = true; FocusMode = FocusModeEnum.None; CustomMinimumSize = Size = new(82, 92);
        AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        AddThemeStyleboxOverride("hover", UiKit.Box(new Color(1, 1, 1, .10f), UiKit.Gold));
        AddThemeStyleboxOverride("pressed", UiKit.Box(new Color(1, 1, 1, .17f), UiKit.Gold));
        Face.Position = new(6, 15); Face.Size = new(60, 60); AddChild(Face);
        _name.Position = new(0, 0); _name.Size = new(82, 16); _name.HorizontalAlignment = HorizontalAlignment.Center;
        _name.MouseFilter = MouseFilterEnum.Ignore; AddChild(_name);
        _count.Position = new(54, 53); _count.Size = new(26, 28); _count.HorizontalAlignment = HorizontalAlignment.Center;
        _count.MouseFilter = MouseFilterEnum.Ignore; AddChild(_count);
        _notice.Position = new(-69, -36); _notice.Size = new(220, 34);
        _notice.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _notice.HorizontalAlignment = HorizontalAlignment.Center;
        _notice.VerticalAlignment = VerticalAlignment.Bottom;
        _notice.MouseFilter = MouseFilterEnum.Ignore;
        _notice.AddThemeColorOverride("font_outline_color", UiKit.Shadow);
        _notice.AddThemeConstantOverride("outline_size", 4);
        AddChild(_notice);
    }

    public void SetNotice(string text, Color color)
    {
        if (_notice.Text == text && _notice.GetThemeColor("font_color") == color) return;
        _notice.Text = text;
        _notice.AddThemeColorOverride("font_color", color);
    }

    public void ShowUnit(UnitDef? unit, int count, double hp, string name, bool enemy = false, string? wave = null)
    {
        Face.SetUnit(unit, enemy, wave);
        Count = count; HpPercent = Math.Clamp(hp, 0, 100); _color = enemy ? UiKit.Enemy : UiKit.Heal;
        _count.Text = count.ToString(); _name.Text = name;
        TooltipText = $"{name}　{unit?.Name ?? "空席"}　{count}枚　HP {HpPercent:F0}%";
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawCircle(new Vector2(67, 67), 14, UiKit.Panel);
        DrawRect(new Rect2(7, 82, 68, 6), UiKit.Line);
        DrawRect(new Rect2(7, 82, (float)(68 * HpPercent / 100), 6), _color);
    }
}
