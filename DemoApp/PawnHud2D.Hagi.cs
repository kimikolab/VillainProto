using Godot;

public partial class PawnHud2D
{
    private int _hagiLine;
    internal float HagiLineX => _barWidth * (-.5f + System.Math.Min(.5f, _hagiLine / 1000f * _maxHp / System.Math.Max(1, _openingMaxHp)));
    internal void SetHagiLine(int permil) { if (_hagiLine == permil) return; _hagiLine = permil; Layout(); QueueRedraw(); }
    private void DrawHagiLine()
    {
        if (_hagiLine <= 0 || _hp <= 0) return;
        float x = HagiLineX;
        float y = -_barHeight - 3;
        Color silver = new("ff8b80");
        DrawLine(new Vector2(x, y + 3), new Vector2(x, y - 10), new Color("141820"), 4, true);
        DrawLine(new Vector2(x, y + 3), new Vector2(x, y - 10), silver, 1.7f, true);
        var arc = new Vector2[13];
        for (int i = 0; i < arc.Length; i++)
        {
            float t = i / 12f;
            arc[i] = new Vector2(x - 8 + t * 15, y - 8 - Mathf.Sin(t * Mathf.Pi) * 4);
        }
        DrawPolyline(arc, new Color("141820"), 4, true); DrawPolyline(arc, silver, 1.8f, true);
    }
}
