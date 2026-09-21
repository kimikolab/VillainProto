using Godot;
using System;

public partial class BattlePawn3D
{
    private int _baseAttack;
    private Label3D _attackDelta = null!;
    internal string AttackDeltaText => _attackDelta.Text;
    internal bool AttackDeltaVisible => _attackDelta.Visible;

    private void BuildAttackChange()
    {
        _attackDelta = MakeLabel("", 20, UiKit.Hurt, 0.006f);
        _attackDelta.Position = new Vector3(1.05f, _hpBack.Position.Y, 0.04f);
        AddChild(_attackDelta);
    }

    private void ShowAttackChange(int change)
    {
        int delta = AttackValue - _baseAttack;
        Color up = Color.FromHtml("#ff746b");
        Color down = Color.FromHtml("#6eaaff");
        _attackDelta.Text = delta > 0 ? "+" + delta : delta.ToString();
        _attackDelta.Modulate = delta > 0 ? up : down;
        _attackDelta.Visible = _alive && delta != 0;
        if (change == 0 || !_alive) return;
        ShowPowerMist(change);
        float sign = Math.Sign(change);
        Color color = change > 0 ? up : down;
        var arrow = new Node3D { Position = new Vector3(0.75f, _fxHeight, 0.15f) };
        AddChild(arrow);
        // 棒と二本の羽根で上下を示す。文字の矢印はフォントに依存するので使わない。
        for (int i = 0; i < 3; i++)
        {
            var part = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.08f, i == 0 ? 0.55f : 0.32f, 0.06f) },
                Position = i == 0 ? Vector3.Zero : new Vector3(i == 1 ? -0.1f : 0.1f, sign * 0.2f, 0),
                Rotation = i == 0 ? Vector3.Zero : new Vector3(0, 0, sign * (i == 1 ? -0.7f : 0.7f)),
                MaterialOverride = MakeMaterial(color, true, color),
            };
            arrow.AddChild(part);
        }
        var tween = arrow.CreateTween();
        tween.TweenProperty(arrow, "position:y", _fxHeight + sign * 0.55f, 0.32);
        tween.TweenProperty(arrow, "scale", Vector3.One * 0.01f, 0.14);
        tween.Finished += arrow.QueueFree;
    }
}
