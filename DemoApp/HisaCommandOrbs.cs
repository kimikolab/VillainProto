using Godot;
using System;

// 3つの空枠は常時表示。溢れの量から再計算せずCommandBall.Amountだけを受け取る。
public partial class HisaCommandOrbs : Node3D
{
    private readonly Sprite3D[] _balls = new Sprite3D[3];
    private BattlePawn3D _owner = null!;
    private float _pulse, _phase;
    internal int Count { get; private set; }
    internal void Configure(BattlePawn3D owner)
    {
        _owner = owner;
        for (int i = 0; i < 3; i++)
        {
            var frame = ShockMarkFx.Sprite(this, GlobalPosition,
                UiKit.LoadTexture("res://assets/fx/hisa_command_orb.svg"), 0.29f, new Color("ad804c"));
            frame.Position = new Vector3((i - 1) * 0.38f, 0, 0);
            _balls[i] = ShockMarkFx.Sprite(this, GlobalPosition, ShockMarkFx.Halo, 0.22f, new Color("ffd184"));
            _balls[i].Position = frame.Position;
            _balls[i].Visible = false;
        }
    }
    internal void SetCount(int count) { Count = Math.Clamp(count, 0, 3); _pulse = 1; }
    internal Vector3 BallPoint(int index) => _balls[Math.Clamp(index, 0, 2)].GlobalPosition;
    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_owner)) return;
        Visible = _owner.PresentationAlive;
        if (!Visible) return;
        GlobalPosition = _owner.HudAnchor + Vector3.Up * .55f;
        if (GetViewport().GetCamera3D() is { } camera) GlobalBasis = camera.GlobalBasis;
        _phase += (float)delta;
        _pulse = Math.Max(0, _pulse - (float)delta * 3 * (float)_owner.AnimationSpeed);
        for (int i = 0; i < 3; i++)
        {
            _balls[i].Visible = i < Count;
            _balls[i].Scale = Vector3.One * (1 + _pulse * 0.4f + Mathf.Sin(_phase * 2 + i) * 0.04f);
        }
    }
}
