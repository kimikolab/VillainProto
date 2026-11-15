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
        // 第476期 §2-1 ／ §2-2: 区画が自分の賞金を持つ盤（1 章の 1-1）は、敵部隊の名前と賞金（関だけ明るい金）・最初の湧きの刻を図に書く。大きい図だけ。
        if (Size.Y >= 120 && State.Lanes.Any(l => l.Any(n => n.Gold >= 0)))
        {
            Font font = ThemeDB.FallbackFont;
            void Tag(Vector2 at, string text, Color color, int size = 13)
            {
                Vector2 box = font.GetStringSize(text, fontSize: size);
                Vector2 pos = new(Mathf.Clamp(at.X - box.X / 2, 6, Size.X - box.X - 6), Mathf.Clamp(at.Y, size + 4, Size.Y - 6));
                DrawStringOutline(font, pos, text, fontSize: size, size: 5, modulate: new Color(0.03f, 0.05f, 0.04f, 0.95f));
                DrawString(font, pos, text, fontSize: size, modulate: color);
            }
            foreach (var lane in State.Lanes)
                foreach (Map11.RoadNode n in lane)
                {
                    if (n.Gold < 0) continue;
                    bool prize = n.Tag.Length > 0;
                    Vector2 p = n.Stationary && graph.HasPortal ? Point(graph.Portal) : Point(graph.FoeStarts[n.Road][n.Index]);
                    if (!n.Stationary) DrawCircle(p, prize ? 7 : 5, prize ? Color.FromHtml("#ffd24a") : UiKit.Enemy);
                    bool above = p.Y < Size.Y / 2 && !n.Stationary;   // 本陣の守りは本陣の印の下（上は「n 刻目から湧く」）
                    Tag(p + new Vector2(0, above ? -10 : n.Stationary ? 30 : 22), $"{n.Name}{(prize ? $"（{n.Tag}）" : "")} 賞金＋{n.Gold}", prize ? Color.FromHtml("#ffd24a") : Color.FromHtml("#e6dcc0"));
                }
            Tag(Point(graph.Home) + new Vector2(0, -12), "拠点", UiKit.Heal);
            if (graph.HasPortal && State.Portal.On)
                Tag(Point(graph.Portal) + new Vector2(0, -22), $"{(State.Portal.FirstSpawn > 0 ? State.Portal.FirstSpawn : State.Portal.SpawnEvery) + 1} 刻目から湧く", UiKit.Enemy);
        }
        DrawRect(new Rect2(Vector2.One, Size - Vector2.One * 2), UiKit.Gold, false, 2);
    }
}
