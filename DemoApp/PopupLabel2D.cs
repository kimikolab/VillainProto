using Godot;

/// <summary>戦場の位置を追う浮き文字。HP 札より手前の 2D 層に描き、3D の描画順には依存しない。</summary>
public partial class PopupLabel2D : Label
{
    public Vector3 WorldPosition { get; set; }
    private Camera3D _camera = null!;

    public void Configure(Camera3D camera, Vector3 position, string text, Color color, int fontSize)
    {
        _camera = camera;
        WorldPosition = position;
        Text = text;
        Modulate = color;
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeFontSizeOverride("font_size", fontSize);
        AddThemeColorOverride("font_color", Colors.White);
        AddThemeConstantOverride("outline_size", 3);
        AddThemeColorOverride("font_outline_color", new Color(0.005f, 0.008f, 0.006f, 0.98f));
        Place();
    }

    public override void _Process(double delta)
    {
        if (!IsQueuedForDeletion()) Place();
    }

    private void Place()
    {
        Visible = !_camera.IsPositionBehind(WorldPosition);
        if (!Visible) return;
        // 数字を合算して桁が増えても、出現点を中央に保つ。
        Size = GetMinimumSize();
        Position = (_camera.UnprojectPosition(WorldPosition) - Size * 0.5f).Round();
    }
}
