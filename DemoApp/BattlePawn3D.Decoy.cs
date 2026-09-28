using Godot;
using System;

public partial class BattlePawn3D
{
    private MeshInstance3D? _decoySign;
    private float _decoyStrength;
    private float _decoyTime;
    internal bool DecoyShown => _decoySign?.Visible == true;

    // 台本の点灯・消灯だけを保持する。狙われる列や回避率の判定はしない。
    internal void SetDecoyShown(bool shown, int evade = 0)
    {
        shown &= _alive && !_victory;
        if (!shown)
        {
            if (_decoySign is not null) _decoySign.Visible = false;
            return;
        }
        if (_decoySign is null)
        {
            // ヒサの赤い照準とは別の、足元へ集まる砂色の3つの山形。
            var mesh = new ImmediateMesh();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.Tau / 3;
                Vector3 radial = new(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                Vector3 side = new(-radial.Z, 0, radial.X);
                Vector3 tip = radial * 0.80f, inner = radial * 0.96f;
                Vector3 left = radial * 1.22f + side * 0.25f;
                Vector3 right = radial * 1.22f - side * 0.25f;
                foreach (var p in new[] { tip, left, inner, tip, inner, right }) mesh.SurfaceAddVertex(p);
            }
            mesh.SurfaceEnd();
            var material = MakeMaterial(MovementFx.Arrow, true, MovementFx.Arrow * 0.6f);
            material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
            _decoySign = new MeshInstance3D {
                Name = "DecoySign", Mesh = mesh, Position = Vector3.Up * 0.09f,
                MaterialOverride = material, Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            AddChild(_decoySign);
        }
        if (!_decoySign.Visible) _decoyTime = 0;
        _decoyStrength = Math.Clamp(evade / 100f, 0, 1);
        _decoySign.Visible = true;
    }

    private void ProcessDecoy(float delta)
    {
        if (!DecoyShown) return;
        _decoyTime += delta;
        float pulse = 1 + Mathf.Sin(_decoyTime * 5) * 0.045f;
        float appear = Mathf.Lerp(1.22f, 1, Mathf.Clamp(_decoyTime / 0.16f, 0, 1));
        _decoySign!.Scale = new Vector3(pulse * appear, 1, pulse * appear);
        _decoySign.Rotation = new Vector3(0, _decoyTime * (0.25f + _decoyStrength * 0.15f), 0);
    }
}
