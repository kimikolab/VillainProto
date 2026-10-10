using Godot;
using System;

// 亀裂と残数を同じ法具に表示する。上限はHushState.Slotの写し。
public partial class HushGauge : Control
{
    internal int Count { get; private set; }
    internal int Limit { get; private set; }
    private Label _label = null!;
    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _label = UiKit.Text("沈黙", 15, new Color("c9e8ef"));
        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.Position = new Vector2(-65, 25); _label.Size = new Vector2(130, 24);
        AddChild(_label);
    }
    internal void SetCracks(int count, int limit)
    {
        Count = count; Limit = limit;
        _label.Text = limit > 0 ? $"沈黙　残り {Math.Max(0, limit - count)}" : "沈黙";
        QueueRedraw();
    }
    public override void _Draw()
    {
        DrawCircle(Vector2.Zero, 27, new Color("182b3b"));
        DrawArc(Vector2.Zero, 26, 0, Mathf.Tau, 48, new Color("b8deea"), 2, true);
        DrawLine(new Vector2(-12, 0), new Vector2(12, 0), Colors.White, 3, true);
        for (int i = 0; i < Limit; i++)
        {
            float angle = i * Mathf.Tau / Limit - Mathf.Pi / 2;
            Vector2 d = Vector2.FromAngle(angle);
            DrawLine(d * 30, d * 34, i < Count ? new Color("ffac91") : new Color("678598"), 3, true);
            if (i >= Count) continue;
            Vector2 bend = d * 15 + d.Orthogonal() * ((i % 2 == 0) ? 5 : -5);
            DrawPolyline(new[] { d * 25, bend, d * 5 }, new Color("ffb79d"), 1.5f, true);
        }
    }
}
