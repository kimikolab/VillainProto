using Godot;
using System;

// 鎖の共有は短い泥の尾だけを並行して流す。HPの反映や次の打撃を待たせない。
public partial class HexShareFlow3D : DebuffFx3D
{
    private BattlePawn3D _target = null!;
    private Vector3 _from;
    private readonly MeshInstance3D[] _drops = new MeshInstance3D[5];
    internal void Start(BattlePawn3D source, BattlePawn3D target, Func<(bool, double)> clock)
    {
        _from = source.FxPoint; _target = target; Clock = clock;
        var material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, AlbedoColor = new Color(BattlefieldView3D.MudoMudTint, .8f) };
        for (int i = 0; i < _drops.Length; i++)
        {
            float radius = .075f - i * .01f;
            _drops[i] = new MeshInstance3D { Mesh = new SphereMesh { Radius = radius, Height = radius * 2,
                RadialSegments = 6, Rings = 3 }, MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            AddChild(_drops[i]);
        }
        Draw();
    }
    public override void _Process(double delta)
    {
        if (!Live(_target)) { End(); return; }
        if (!Advance(delta)) return;
        if (Age >= .24) { End(); return; }
        Draw();
    }
    private void Draw()
    {
        float head = (float)(Age / .24);
        for (int i = 0; i < _drops.Length; i++)
        {
            float t = Mathf.Clamp(head - i * .035f, 0, 1);
            _drops[i].GlobalPosition = _from.Lerp(_target.FxPoint, t) + Vector3.Up * Mathf.Sin(t * Mathf.Pi) * .16f;
            _drops[i].Visible = head > i * .035f;
        }
    }
}
