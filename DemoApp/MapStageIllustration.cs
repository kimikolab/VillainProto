using Godot;
using System;
using System.Linq;

/// <summary>本文と差し替え可能な挿絵枠。各盤の本当の道・拠点の位置を描く。</summary>
public partial class MapStageIllustration : Control
{
    public MapStage State { get; set; } = null!;
    public override void _Ready() { MouseFilter = MouseFilterEnum.Ignore; Resized += QueueRedraw; }
    public override void _Draw()
    {
        if (State is null) return;
        var graph = State.Board(State.Portal.On);
        float x0 = graph.Cells.Min(c => c.Pos.X), x1 = graph.Cells.Max(c => c.Pos.X);
        float z0 = graph.Cells.Min(c => c.Pos.Z), z1 = graph.Cells.Max(c => c.Pos.Z);
        Vector2 Point(int id)
        {
            var p = graph[id].Pos;
            return new Vector2(22 + (Size.X - 44) * (p.X - x0) / Math.Max(1, x1 - x0), 20 + (Size.Y - 40) * (p.Z - z0) / Math.Max(1, z1 - z0));
        }
        DrawRect(new Rect2(Vector2.Zero, Size), Color.FromHtml("#20382f"));
        foreach (var (a, b) in graph.Edges) DrawLine(Point(a), Point(b), new Color(.65f, .58f, .38f), 3, true);
        foreach (var cell in graph.Cells)
        {
            Vector2 p = Point(cell.Id);
            Color color = cell.Id == graph.Home ? UiKit.Heal : cell.Id == graph.Portal ? UiKit.Enemy : UiKit.Gold;
            if (cell.Id == graph.Portal)
            {
                DrawRect(new Rect2(p - new Vector2(9, 14), new Vector2(18, 24)), color);
                DrawRect(new Rect2(p - new Vector2(4, 4), new Vector2(8, 14)), UiKit.Panel);
            }
            else DrawCircle(p, cell.Kind == MapGraph.Kind.Outpost ? 6 : 3, color);
        }
        DrawRect(new Rect2(Vector2.One, Size - Vector2.One * 2), UiKit.Gold, false, 2);
    }
}
