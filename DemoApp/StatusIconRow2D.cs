using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 頭上の状態アイコンを画面の平面（2D）に描く列。<b>`StatusIconRow3D` と同じ口</b>を持つので、
/// 駒の側（`BattlePawn3D.StatusIcons`）と頭なしの門は差し替えに気づかない。
/// 3D の板に描くと遠い駒ほど縮んで数字が読めなくなるので、画面上の大きさを固定する。
/// 原点は列の下辺の中央。2列目以降は上へ積む（下は HP バーなので）。
/// </summary>
public partial class StatusIconRow2D : Control
{
    public const float IconSize = 22f;
    private const float Pitch = 24f;
    private const int PerRow = 6;

    private sealed class Icon
    {
        public TextureRect Sprite = null!;
        public bool Active;
        public float Flash;
        public int Amount;
        public Label? Count;
        public float Bounce;
        public Vector2 RestPosition;
    }
    private readonly Dictionary<string, Icon> _icons = new();
    public int ActiveCount => _icons.Values.Count(i => i.Active);
    public int FlashCount => _icons.Values.Count(i => i.Flash > 0);
    public bool Has(string key) => _icons.TryGetValue(key, out var i) && i.Active;
    public int Amount(string key) => _icons.TryGetValue(key, out var i) && i.Active ? i.Amount : 0;
    /// <summary>いま描いている段の数（頭上の札の高さを決める）。</summary>
    public int Rows { get; private set; }
    /// <summary>いま描いている列の幅（札の重なり回避が読む）。</summary>
    public float Width { get; private set; }

    public StatusIconRow2D() => MouseFilter = MouseFilterEnum.Ignore;

    public void SetAmount(string key, int amount, bool animateGain)
    {
        int before = Amount(key);
        Set(key, amount > 0);
        if (!_icons.TryGetValue(key, out var icon)) return;
        icon.Amount = System.Math.Max(0, amount);
        // 紅蓮は数字でなく、満ち具合と発光で予告する。
        if (key == BattleCore.StatusKeys.Guren)
        {
            if (animateGain && amount > before) icon.Bounce = 0.4f;
            return;
        }
        if (key == BattleCore.StatusKeys.Concentrated)
        {
            if (amount > 0 && before != amount) icon.Sprite.Texture = StatusIconArt.ConcentratedTexture(amount);
            if (animateGain && amount > before) icon.Bounce = 0.4f;
            return;
        }
        if (icon.Count is null && amount > 0)
        {
            icon.Count = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Position = new Vector2(-7, 5),
                Size = new Vector2(IconSize + 10, IconSize),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            icon.Count.AddThemeColorOverride("font_color", new Color("f1e5ff"));
            icon.Count.AddThemeColorOverride("font_outline_color", new Color("171020"));
            icon.Count.AddThemeConstantOverride("outline_size", 4);
            icon.Sprite.AddChild(icon.Count);
        }
        if (icon.Count is not null)
        {
            icon.Count.Text = icon.Amount.ToString();
            // 桁が増えても隣の状態アイコンへはみ出さない。
            icon.Count.AddThemeFontSizeOverride("font_size", amount >= 1000 ? 9 : amount >= 100 ? 10 : 12);
            icon.Count.Visible = amount > 0;
        }
        // 実時間で短く跳ねる。2倍速でも見える長さを保ち、待ち時間は増やさない。
        if (animateGain && amount > before) icon.Bounce = 0.4f;
        else if (amount <= 0) icon.Bounce = 0;
    }

    public void Set(string key, bool active)
    {
        if (!_icons.TryGetValue(key, out var icon))
        {
            if (!active) return;
            icon = new Icon { Sprite = new TextureRect
            {
                Texture = StatusIconArt.Texture(key),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                Size = new Vector2(IconSize, IconSize),
                PivotOffset = new Vector2(IconSize, IconSize) * 0.5f,
                MouseFilter = MouseFilterEnum.Ignore,
            } };
            AddChild(icon.Sprite);
            _icons[key] = icon;
        }
        if (icon.Active == active) return;
        icon.Active = active;
        icon.Flash = 0.32f;
        Layout();
    }

    public void Pulse(string key)
    {
        if (_icons.TryGetValue(key, out var icon) && icon.Active) icon.Bounce = 0.4f;
    }

    public void Clear()
    {
        foreach (var icon in _icons.Values) icon.Sprite.QueueFree();
        _icons.Clear();
        Rows = 0;
        Width = 0;
    }

    private void Layout()
    {
        var visible = StatusIconArt.Keys.Where(k => _icons.TryGetValue(k, out var icon) && (icon.Active || icon.Flash > 0)).ToArray();
        Rows = (visible.Length + PerRow - 1) / PerRow;
        Width = visible.Length == 0 ? 0 : (System.Math.Min(PerRow, visible.Length) - 1) * Pitch + IconSize;
        for (int i = 0; i < visible.Length; i++)
        {
            int row = i / PerRow, count = System.Math.Min(PerRow, visible.Length - row * PerRow);
            var icon = _icons[visible[i]];
            icon.RestPosition = new Vector2((i % PerRow - (count - 1) * 0.5f) * Pitch - IconSize * 0.5f, -IconSize - row * Pitch);
            icon.Sprite.Position = icon.RestPosition;
        }
    }

    public override void _Process(double delta)
    {
        bool relayout = false;
        foreach (var (key, icon) in _icons)
        {
            float before = icon.Flash;
            icon.Flash = System.Math.Max(0, icon.Flash - (float)delta);
            icon.Bounce = System.Math.Max(0, icon.Bounce - (float)delta);
            float hop = Mathf.Sin(icon.Bounce / 0.4f * Mathf.Pi);
            float glow = icon.Flash / 0.32f;
            if (key == BattleCore.StatusKeys.Guren && icon.Active)
                glow += System.Math.Min(1f, icon.Amount / 12f) * 0.35f
                    + (icon.Amount >= 12 ? 0.45f + 0.25f * Mathf.Sin(Time.GetTicksMsec() * 0.009f) : 0);
            icon.Sprite.Modulate = new Color(1 + glow, 1 + glow, 1 + glow, icon.Active ? 1 : glow);
            float weight = key == BattleCore.StatusKeys.Concentrated ? 1 + System.Math.Min(8, icon.Amount) * 0.02f : 1;
            icon.Sprite.Scale = Vector2.One * (1 + glow * 0.18f + hop * 0.22f) * weight;
            icon.Sprite.Position = icon.RestPosition + Vector2.Up * (hop * 7f);
            icon.Sprite.Visible = icon.Active || icon.Flash > 0;
            relayout |= before > 0 && icon.Flash == 0 && !icon.Active;
        }
        if (relayout) Layout();
    }
}
