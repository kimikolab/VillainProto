using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class StatusIconRow3D : Node3D
{
    private sealed class Icon
    {
        public Sprite3D Sprite = null!;
        public bool Active;
        public float Flash;
        public int Amount;
        public Label3D? Count;
        public float Bounce;
        public Vector3 RestPosition;
    }
    private readonly Dictionary<string, Icon> _icons = new();
    public int ActiveCount => _icons.Values.Count(i => i.Active);
    public int FlashCount => _icons.Values.Count(i => i.Flash > 0);
    public bool Has(string key) => _icons.TryGetValue(key, out var i) && i.Active;
    public int Amount(string key) => _icons.TryGetValue(key, out var i) && i.Active ? i.Amount : 0;

    public void SetAmount(string key, int amount, bool animateGain)
    {
        int before = Amount(key);
        Set(key, amount > 0);
        if (!_icons.TryGetValue(key, out var icon)) return;
        icon.Amount = System.Math.Max(0, amount);
        if (key == BattleCore.StatusKeys.Concentrated)
        {
            if (amount > 0 && before != amount) icon.Sprite.Texture = StatusIconArt.ConcentratedTexture(amount);
            if (animateGain && amount > before) icon.Bounce = 0.4f;
            return;
        }
        if (icon.Count is null && amount > 0)
        {
            icon.Count = new Label3D
            {
                Position = new Vector3(0.06f, -0.075f, 0.015f),
                FontSize = 32, PixelSize = 0.0042f, OutlineSize = 10,
                Modulate = new Color("f1e5ff"), OutlineModulate = new Color("171020"),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true, Shaded = false, RenderPriority = 1,
            };
            icon.Sprite.AddChild(icon.Count);
        }
        if (icon.Count is not null)
        {
            icon.Count.Text = icon.Amount.ToString();
            // 桁が増えても隣の状態アイコンへはみ出さない。
            icon.Count.FontSize = amount >= 1000 ? 22 : amount >= 100 ? 28 : 32;
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
            icon = new Icon { Sprite = new Sprite3D
            {
                Texture = StatusIconArt.Texture(key), PixelSize = 0.0042f,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                NoDepthTest = true, Shaded = false,
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
    }

    private void Layout()
    {
        var visible = StatusIconArt.Keys.Where(k => _icons.TryGetValue(k, out var icon) && (icon.Active || icon.Flash > 0)).ToArray();
        for (int i = 0; i < visible.Length; i++)
        {
            int row = i / 6, count = System.Math.Min(6, visible.Length - row * 6);
            var icon = _icons[visible[i]];
            icon.RestPosition = new Vector3((i % 6 - (count - 1) * 0.5f) * 0.29f, -row * 0.29f, 0);
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
            icon.Sprite.Modulate = new Color(1 + glow, 1 + glow, 1 + glow, icon.Active ? 1 : glow);
            float weight = key == BattleCore.StatusKeys.Concentrated ? 1 + System.Math.Min(8, icon.Amount) * 0.02f : 1;
            icon.Sprite.Scale = Vector3.One * (1 + glow * 0.18f + hop * 0.22f) * weight;
            icon.Sprite.Position = icon.RestPosition + Vector3.Up * (hop * 0.085f);
            icon.Sprite.Visible = icon.Active || icon.Flash > 0;
            relayout |= before > 0 && icon.Flash == 0 && !icon.Active;
        }
        if (relayout) Layout();
    }
}
