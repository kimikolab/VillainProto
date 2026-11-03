using Godot;
using System;

/// <summary>稼ぎの記録1件を金貨の束で見せる。金額や時計は書き換えない。</summary>
public partial class MapGoldBurst : Control
{
    private Vector2 _from, _to;
    private float _age;
    private int _coins;
    public void Start(Vector2 from, Vector2 to, int amount)
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 49;
        _from = from; _to = to; _coins = Math.Clamp(amount, 3, 12);
    }
    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= 1.65f) { QueueFree(); return; }
        QueueRedraw();
    }
    public override void _Draw()
    {
        for (int i = 0; i < _coins; i++)
        {
            float t = _age - i * .025f;
            if (t < 0 || t > 1.2f) continue;
            float angle = i * Mathf.Tau / _coins;
            Vector2 burst = _from + new Vector2(Mathf.Cos(angle) * 34, -28 + Mathf.Sin(angle) * 22);
            Vector2 p;
            if (t < .32f) p = _from.Lerp(burst, 1 - Mathf.Pow(1 - t / .32f, 3));
            else
            {
                float u = Math.Clamp((t - .32f) / .75f, 0, 1);
                Vector2 bend = burst.Lerp(_to, .35f) + new Vector2(35, -80);
                p = burst * (1 - u) * (1 - u) + bend * 2 * u * (1 - u) + _to * u * u;
                if (u >= 1) continue;
            }
            DrawCircle(p, 7, new Color(.32f, .16f, .03f));
            DrawCircle(p, 5.5f, UiKit.Gold);
            DrawArc(p, 3.4f, 0, Mathf.Tau, 16, new Color(1, .93f, .63f), 1, true);
            DrawLine(p + new Vector2(-2, -2), p + new Vector2(2, -2), Colors.White, 1, true);
        }
        float ring = (_age - 1.05f) / .5f;
        if (ring is > 0 and < 1)
            DrawArc(_to, 8 + ring * 22, 0, Mathf.Tau, 32, new Color(UiKit.Gold, 1 - ring), 2, true);
    }
}
