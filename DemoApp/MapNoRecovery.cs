using Godot;

/// <summary>回復しないことを示す斜線付きの十字。説明は触れたときだけ。</summary>
public partial class MapNoRecovery : Control
{
    public override void _Draw()
    {
        Vector2 p = Size / 2;
        DrawRect(new Rect2(p - new Vector2(5, 20), new Vector2(10, 40)), UiKit.Heal);
        DrawRect(new Rect2(p - new Vector2(20, 5), new Vector2(40, 10)), UiKit.Heal);
        DrawArc(p, 27, 0, Mathf.Tau, 48, UiKit.Enemy, 4, true);
        DrawLine(p + new Vector2(-20, 20), p + new Vector2(20, -20), UiKit.Enemy, 5, true);
    }
}
