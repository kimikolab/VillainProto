using Godot;
using System;

// 核が割れて光へ還る短い絵。散るものは光滴と電弧だけで、物体の欠片は作らない。
public partial class SomSpiritScatter3D : Node3D
{
    private Sprite3D[] _lights = Array.Empty<Sprite3D>();
    private Sprite3D _seal = null!;
    private Vector3 _origin, _right, _up;
    private double _age, _speed;
    private bool _paused;
    private float _radius, _phase;
    internal double VisualAge => _age;
    internal void Configure(Vector3 point, Camera3D camera, int seed, bool emergency, double speed)
    {
        _origin = point; _right = camera.GlobalBasis.X; _up = camera.GlobalBasis.Y;
        _speed = Math.Max(.1, speed); _radius = emergency ? .78f : .48f; _phase = seed * .73f;
        _lights = new Sprite3D[emergency ? 12 : 9];
        for (int i = 0; i < _lights.Length; i++)
        {
            _lights[i] = ShockMarkFx.Sprite(this, point, i < 3 ? SomFx.SpiritCore : SomFx.Light,
                i < 3 ? .16f : .10f, i % 3 == 0 ? SomFx.SpiritRed : SomFx.Gold);
        }
        _seal = ShockMarkFx.Sprite(this, point + camera.GlobalBasis.Z * .04f, SomFx.ContractCracked,
            emergency ? .45f : .27f, SomFx.SpiritRed);
    }
    internal void SetClock(bool paused, double speed) { _paused = paused; _speed = Math.Max(.1, speed); }
    public override void _Process(double delta)
    {
        if (_paused) return;
        _age += delta * _speed;
        float t = Math.Clamp((float)(_age / .34), 0, 1);
        for (int i = 0; i < _lights.Length; i++)
        {
            float angle = _phase + i * Mathf.Tau / _lights.Length;
            var light = _lights[i];
            light.GlobalPosition = _origin + (_right * MathF.Cos(angle) + _up * MathF.Sin(angle))
                * _radius * (.08f + .92f * (1 - MathF.Pow(1 - t, 2)))
                + _up * t * t * .12f;
            light.Scale = Vector3.One * (1 - t * .65f);
            light.Modulate = new Color(light.Modulate, MathF.Pow(1 - t, 1.4f));
        }
        _seal.Scale = Vector3.One * (1 + t * .6f);
        _seal.Modulate = new Color(SomFx.SpiritRed, Math.Max(0, 1 - t * 3));
        if (t >= 1) { Hide(); QueueFree(); }
    }
}
