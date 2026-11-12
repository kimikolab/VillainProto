using Godot;
using System;

// 焼き付く短い光と、その後の薄い刻印。同じ紋・銅色を攻撃後の鈍化にも使う。
public partial class DebuffKarzMark3D : DebuffFx3D
{
    private BattlePawn3D _source = null!, _target = null!;
    private Camera3D _camera = null!;
    private Sprite3D _scar = null!, _flash = null!;
    internal void Start(BattlePawn3D source, BattlePawn3D target, Camera3D camera, Func<(bool, double)> clock)
    {
        _source = source; _target = target; _camera = camera; Clock = clock;
        _scar = Sprite("res://assets/fx/debuff_karz_scar.svg", .9f, DebuffKarzDull3D.ScarTint);
        _flash = new Sprite3D { Texture = HagiLight3D.Sparkle, PixelSize = .85f / HagiLight3D.Sparkle.GetWidth(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Shaded = false, NoDepthTest = true,
            Modulate = new Color(DebuffKarzDull3D.ScarTint, .9f), RenderPriority = 9 };
        AddChild(_flash); Draw();
    }
    public override void _Process(double delta)
    {
        if (!Live(_source) || !IsInstanceValid(_target) || !_target.IsInsideTree()) { End(); return; }
        if (!Live(_target)) { Hide(); return; }
        Show();
        if (!Advance(delta)) return;
        Draw();
    }
    private void Draw()
    {
        float t = Mathf.Clamp((float)(Age / .3), 0, 1);
        Vector3 point = _target.FxPoint + Vector3.Down * .35f + _camera.GlobalBasis.X * (_target.Team == 0 ? .22f : -.22f);
        _scar.GlobalPosition = point;
        _scar.Scale = Vector3.One * (1.25f - .35f * t);
        _scar.Modulate = new Color(DebuffKarzDull3D.ScarTint, .24f + .71f * (1 - t));
        _flash.GlobalPosition = point;
        _flash.Modulate = new Color(1, .75f, .5f, .9f * (1 - t));
    }
}
