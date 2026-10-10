using Godot;
using System;

// 付与された対象だけの四隅を収束させ、二拍の明滅で確定を示す。再付与は同じ枠を再利用。
public partial class MarkLockOn3D : Node3D
{
    private BattlePawn3D _owner = null!;
    private Sprite3D _frame = null!;
    private double _age, _rate;
    internal bool Active { get; private set; }
    internal int Plays { get; private set; }

    internal void Configure(BattlePawn3D owner)
    {
        _owner = owner;
        _frame = ShockMarkFx.Sprite(this, owner.FxPoint, ShockMarkFx.LockFrame(owner.Team), 1.28f, Colors.White);
        _frame.NoDepthTest = true; _frame.RenderPriority = 9;
        Stop();
    }

    internal void Play(double speed)
    {
        _age = 0;
        // 倍速でも約0.5秒は残す。表示だけなので台本の待ちを増やさない。
        _rate = Math.Clamp(speed, 0.1, 1.4);
        Active = true; Plays++; Show();
        Draw();
    }

    internal void Stop() { Active = false; Hide(); }

    public override void _Process(double delta)
    {
        if (!Active) return;
        if (!_owner.ShockMarkActive || !_owner.HasVisibleMark) { Stop(); return; }
        _age += delta * _rate;
        if (_age >= 0.70) { Stop(); return; }
        Draw();
    }

    private void Draw()
    {
        _frame.GlobalPosition = _owner.MarkFocusPoint;
        float converge = Mathf.Clamp((float)(_age / 0.22), 0, 1);
        _frame.Scale = Vector3.One * (1 + 0.65f * Mathf.Pow(1 - converge, 3));
        // 消し切らず、枠の位置を見失わない明暗を二回だけ付ける。
        float alpha = _age < 0.22 ? 1 : _age < 0.50
            ? 0.30f + 0.70f * Mathf.Pow(Mathf.Cos((float)(_age - 0.22) / 0.28f * Mathf.Tau), 2)
            : (float)((0.70 - _age) / 0.20);
        _frame.Modulate = new Color(1, 1, 1, alpha);
    }
}
