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
    }
    private readonly Dictionary<string, Icon> _icons = new();
    public int ActiveCount => _icons.Values.Count(i => i.Active);
    public int FlashCount => _icons.Values.Count(i => i.Flash > 0);
    public bool Has(string key) => _icons.TryGetValue(key, out var i) && i.Active;

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
            _icons[visible[i]].Sprite.Position = new Vector3((i % 6 - (count - 1) * 0.5f) * 0.29f, -row * 0.29f, 0);
        }
    }

    public override void _Process(double delta)
    {
        bool relayout = false;
        foreach (var icon in _icons.Values)
        {
            float before = icon.Flash;
            icon.Flash = System.Math.Max(0, icon.Flash - (float)delta);
            float glow = icon.Flash / 0.32f;
            icon.Sprite.Modulate = new Color(1 + glow, 1 + glow, 1 + glow, icon.Active ? 1 : glow);
            icon.Sprite.Scale = Vector3.One * (1 + glow * 0.18f);
            icon.Sprite.Visible = icon.Active || icon.Flash > 0;
            relayout |= before > 0 && icon.Flash == 0 && !icon.Active;
        }
        if (relayout) Layout();
    }
}
