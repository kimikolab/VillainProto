using Godot;
using System;

// 同じ弧を往復する短い雷。先端と尾を移動させ、来た方向と返る方向を分ける。
public partial class SomLightning3D : Node3D
{
    private readonly ImmediateMesh _glow = new(), _core = new();
    private Camera3D _camera = null!;
    private Vector3 _from, _to;
    private bool _reverse, _paused;
    private double _speed, _seconds, _elapsed;
    private float _width;
    private Sprite3D _head = null!;
    private Color _tint;
    internal float Progress => Math.Clamp((float)(_elapsed / _seconds), 0, 1);
    internal Vector3 Head => Point(_reverse ? 1 - Progress : Progress);

    internal void Configure(Camera3D camera, Vector3 from, Vector3 to, bool reverse, double speed, double seconds, Color? tint = null)
    {
        _camera = camera; _from = from; _to = to; _reverse = reverse;
        _speed = Math.Max(.1, speed); _seconds = seconds; _width = reverse ? .052f : .023f;
        _tint = tint ?? ThunderFx.Cyan;
        Layer(_glow, _tint, .33f);
        Layer(_core, new Color("effcff"), .96f);
        _head = ShockMarkFx.Sprite(this, Head, SomFx.SpiritSpear, reverse ? .40f : .31f, _tint);
        Draw();
    }
    private void Layer(ImmediateMesh mesh, Color color, float alpha)
    {
        AddChild(new MeshInstance3D { Mesh = mesh,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled, VertexColorUseAsAlbedo = true,
                AlbedoColor = new Color(color, alpha), EmissionEnabled = true, Emission = color,
                EmissionEnergyMultiplier = 1.2f,
            } });
    }
    internal void SetClock(bool paused, double speed) { _paused = paused; _speed = Math.Max(.1, speed); }
    private Vector3 Point(float t)
        => _from.Lerp(_to, t) + Vector3.Up * (MathF.Sin(t * MathF.PI) * .65f)
            + _camera.GlobalBasis.X * MathF.Sin(t * 59) * MathF.Sin(t * MathF.PI) * .08f;
    public override void _Process(double delta)
    {
        if (_paused) return;
        _elapsed += delta * _speed;
        if (_elapsed > _seconds + .10) { Hide(); QueueFree(); return; }
        Draw();
    }
    private void Draw()
    {
        float head = Progress, tail = Math.Max(0, head - .48f);
        float fade = 1 - Math.Clamp((float)((_elapsed - _seconds) / .10), 0, 1);
        Vector3 direction = (_reverse ? _from - _to : _to - _from);
        _head.GlobalPosition = Head;
        _head.Rotation = new Vector3(0, 0, MathF.Atan2(direction.Dot(_camera.GlobalBasis.Y), direction.Dot(_camera.GlobalBasis.X)));
        _head.Modulate = new Color(_tint, Progress > 0 ? fade : 0);
        foreach (var mesh in new[] { _glow, _core })
        {
            mesh.ClearSurfaces();
            if (head <= tail) continue;
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            for (int i = 0; i < 24; i++)
            {
                float u = tail + (head - tail) * i / 24, v = tail + (head - tail) * (i + 1) / 24;
                Vector3 a = Point(_reverse ? 1 - u : u), b = Point(_reverse ? 1 - v : v);
                Vector3 side = (b - a).Cross(_camera.GlobalPosition - a).Normalized()
                    * _width * (mesh == _glow ? 3.2f : 1);
                mesh.SurfaceSetColor(new Color(1, 1, 1, fade * (.2f + .8f * i / 24f)));
                foreach (var p in new[] { a - side, a + side, b + side, a - side, b + side, b - side })
                    mesh.SurfaceAddVertex(ToLocal(p));
            }
            mesh.SurfaceEnd();
        }
    }
}
