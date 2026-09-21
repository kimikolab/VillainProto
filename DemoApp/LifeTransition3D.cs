using Godot;
using System;

// 生死の出来事だけを描く。特性や書き手の推測はしない。
public partial class LifeTransition3D : Node3D
{
    public enum Kind { Death, Revive, Summon }
    private Kind _kind;
    private float _age;
    private float _height;
    private readonly MeshInstance3D[] _pieces = new MeshInstance3D[14];

    public void Configure(Kind kind, float height)
    {
        _kind = kind;
        _height = height;
        Color tint = kind switch
        {
            Kind.Death => Color.FromHtml("#b3a699"),
            Kind.Revive => Color.FromHtml("#fff0bb"),
            _ => Color.FromHtml("#bb9cff"),
        };
        for (int i = 0; i < _pieces.Length; i++)
        {
            var material = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = tint,
                EmissionEnabled = true, Emission = tint, EmissionEnergyMultiplier = 1.4f,
            };
            _pieces[i] = new MeshInstance3D
            {
                Mesh = kind == Kind.Revive
                    ? new BoxMesh { Size = new Vector3(0.9f, 0.025f, 0.025f) }
                    : new SphereMesh { Radius = 0.045f, Height = 0.09f, RadialSegments = 6, Rings = 3 },
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_pieces[i]);
        }
        UpdatePieces();
    }

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= 0.85f) { QueueFree(); return; }
        UpdatePieces();
    }

    private void UpdatePieces()
    {
        for (int i = 0; i < _pieces.Length; i++)
        {
            float t = Mathf.Clamp((_age - (i / 2) * 0.025f) / 0.65f, 0, 1);
            float angle = i * 2.39996f;
            var piece = _pieces[i];
            if (_kind == Kind.Revive)
            {
                // 二本ずつの糸を足元から順に寄せ、縫い目を閉じてほどく。
                float side = i % 2 == 0 ? -1 : 1;
                piece.Position = new Vector3(side * (0.65f * (1 - Mathf.Min(1, t * 3))),
                    0.18f + (i / 2) * _height / 6, 0.25f);
                piece.Rotation = new Vector3(0, 0, side * 0.24f * (1 - t));
                piece.Scale = new Vector3(Mathf.Sin(t * Mathf.Pi), 1, 1);
            }
            else
            {
                float radius = _kind == Kind.Death ? 0.18f + t * 0.65f : 0.7f * (1 - t * 0.5f);
                if (_kind == Kind.Summon) angle += t * 3;
                float y = _kind == Kind.Death
                    ? Mathf.Max(0.06f, _height * (0.25f + i % 4 * 0.18f) - t * t * 2)
                    : 0.05f + t * _height * 1.3f;
                piece.Position = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                piece.Scale = Vector3.One * (0.5f + Mathf.Sin(t * Mathf.Pi));
            }
            var material = (StandardMaterial3D)piece.MaterialOverride;
            Color color = material.AlbedoColor;
            color.A = Mathf.Sin(t * Mathf.Pi) * 0.9f;
            material.AlbedoColor = color;
        }
    }
}
