using Godot;
using System;

/// <summary>台本で確認した保持者と被封鎖者を結ぶ鎖。戦闘の判定は持たない。</summary>
public partial class HushChain3D : Node3D
{
    private BattlePawn3D _holder = null!;
    private BattlePawn3D _target = null!;
    private readonly MeshInstance3D[] _links = new MeshInstance3D[40];
    private StandardMaterial3D _material = null!;
    private float _pulse;
    private float _elapsed;
    private float _breakTime = -1;
    private Vector3[]? _brokenPositions;

    public void Configure(BattlePawn3D holder, BattlePawn3D target)
    {
        _holder = holder;
        _target = target;
        _material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(0.65f, 0.52f, 0.76f, 0.22f),
            NoDepthTest = true,
        };
        var mesh = new TorusMesh { InnerRadius = 0.075f, OuterRadius = 0.115f, Rings = 12, RingSegments = 6 };
        for (int i = 0; i < _links.Length; i++)
        {
            var link = new MeshInstance3D
            {
                Mesh = mesh, MaterialOverride = _material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(link);
            _links[i] = link;
        }
        UpdateLinks();
    }

    public void Tighten() => _pulse = 1;

    public void Shatter()
    {
        if (_breakTime >= 0) return;
        _breakTime = 0;
        _brokenPositions = Array.ConvertAll(_links, link => link.Position);
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        _pulse = Math.Max(0, _pulse - (float)delta / 0.55f);
        if (_breakTime >= 0)
        {
            _breakTime += (float)delta;
            for (int i = 0; i < _links.Length; i++)
            {
                float angle = i * 2.4f;
                _links[i].Position = _brokenPositions![i] + new Vector3(
                    Mathf.Cos(angle) * _breakTime * 1.5f,
                    _breakTime * 1.6f - _breakTime * _breakTime * 3,
                    Mathf.Sin(angle) * _breakTime);
                _links[i].RotateX((float)delta * (i % 2 == 0 ? 5 : -5));
            }
            _material.AlbedoColor = new Color(0.8f, 0.9f, 1, Math.Max(0, 1 - _breakTime / 0.7f));
            if (_breakTime >= 0.7f) QueueFree();
            return;
        }
        if (!IsInstanceValid(_holder) || !IsInstanceValid(_target)) { QueueFree(); return; }
        UpdateLinks();
    }

    private void UpdateLinks()
    {
        Vector3 a = _holder.FxPoint;
        Vector3 b = _target.FxPoint;
        if (_holder == _target) { a -= Vector3.Right * 0.7f; b += Vector3.Right * 0.7f; }
        Vector3 direction = (b - a).Normalized();
        if (direction.LengthSquared() < 0.01f) direction = Vector3.Right;
        Vector3 across = direction.Cross(Vector3.Up).Normalized();
        if (across.LengthSquared() < 0.01f) across = Vector3.Forward;
        Vector3 normal = across.Cross(direction).Normalized();
        float sag = Mathf.Lerp(0.7f, 0.04f, _pulse);
        for (int i = 0; i < _links.Length; i++)
        {
            float t = i / (float)(_links.Length - 1);
            _links[i].Position = a.Lerp(b, t) - Vector3.Up * (Mathf.Sin(t * Mathf.Pi) * sag);
            // 輪の長軸を鎖の向きに揃え、交互の輪をひねる。
            _links[i].Basis = new Basis(direction, normal, across)
                .Rotated(direction, i % 2 == 0 ? 0.2f : 1.15f);
            _links[i].Scale = new Vector3(Math.Max(1, a.DistanceTo(b) / 7.4f), 1, 0.72f);
        }
        _material.AlbedoColor = new Color(
            new Color(0.65f, 0.52f, 0.76f).Lerp(new Color(1, 0.76f, 1), _pulse),
            0.16f + 0.78f * _pulse + 0.025f * Mathf.Sin(_elapsed * 2));
    }
}
