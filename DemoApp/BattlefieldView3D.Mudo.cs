using Godot;
using System;
using System.Collections.Generic;

public partial class BattlefieldView3D
{
    private readonly List<MudoMudPulse3D> _mudoPulses = new();
    private readonly List<PopupLabel2D> _mudoLabels = new();
    private bool _mudoPaused;
    private double _mudoSpeed = 1;
    internal int MudoBursts, MudoDulls;

    internal void ResetMudoPresentation()
    {
        foreach (var pulse in _mudoPulses)
            if (IsInstanceValid(pulse)) { pulse.Hide(); pulse.QueueFree(); }
        foreach (var label in _mudoLabels)
            if (IsInstanceValid(label)) { label.Hide(); label.QueueFree(); }
        _mudoPulses.Clear(); _mudoLabels.Clear();
        MudoBursts = MudoDulls = 0;
    }

    internal void SetMudoClock(bool paused, double speed)
    {
        _mudoPaused = paused; _mudoSpeed = Math.Max(0.1, speed);
        _mudoPulses.RemoveAll(p => !IsInstanceValid(p) || p.IsQueuedForDeletion());
        foreach (var pulse in _mudoPulses) pulse.SetClock(paused, _mudoSpeed);
    }

    // 怒りの加算は台本に無い。実際の暴発のHighlightだけで泥を揺らす。
    internal void ShowMudoBurst(BattlePawn3D pawn)
    {
        MudoBursts++;
        var pulse = new MudoMudPulse3D();
        pulse.Configure(pawn);
        pulse.SetClock(_mudoPaused, _mudoSpeed);
        _fxRoot.AddChild(pulse);
        _mudoPulses.RemoveAll(p => !IsInstanceValid(p) || p.IsQueuedForDeletion());
        _mudoPulses.Add(pulse);
    }

    internal void ShowMudoDull(BattlePawn3D pawn)
    {
        MudoDulls++;
        // 他の浮き文字と同じ実時間の寿命。連撃でも頭・HPを覆わない小さな数字にする。
        _mudoLabels.RemoveAll(p => !IsInstanceValid(p) || p.IsQueuedForDeletion());
        if (Float(pawn, "−1", HexMudFx.Tint, false, 3.35f, 0.85f) is { } label) _mudoLabels.Add(label);
    }
}

// 呪いのしみと同じ泥の形・色。駒の動作や位置を変更せず、胸元より下で短く揺れる。
public partial class MudoMudPulse3D : Node3D
{
    private BattlePawn3D _pawn = null!;
    private MeshInstance3D _mud = null!;
    private bool _paused;
    private double _speed = 1;
    internal double Elapsed { get; private set; }

    internal void Configure(BattlePawn3D pawn) => _pawn = pawn;
    internal void SetClock(bool paused, double speed) { _paused = paused; _speed = Math.Max(0.1, speed); }

    public override void _Ready()
    {
        _mud = HexMudFx.Stain(this, Vector3.Zero);
        ((ShaderMaterial)_mud.MaterialOverride).SetShaderParameter("tint", new Color(HexMudFx.Tint, 0.85f));
        DrawPulse();
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_pawn) || !_pawn.IsInsideTree() || !_pawn.CanPlayAttackClip)
        { Hide(); QueueFree(); return; }
        if (_paused) return;
        Elapsed += delta * _speed;
        if (Elapsed >= 0.36) { Hide(); QueueFree(); return; }
        DrawPulse();
    }

    private void DrawPulse()
    {
        float t = (float)(Elapsed / 0.36);
        Position = GetParent<Node3D>().ToLocal(_pawn.FxPoint + Vector3.Down * 0.42f);
        _mud.Position = Vector3.Right * Mathf.Sin(t * Mathf.Tau * 2) * 0.10f * (1 - t);
        _mud.Scale = new Vector3(1.3f + Mathf.Sin(t * Mathf.Pi) * 0.4f, 0.65f + t * 0.2f, 1);
        _mud.Transparency = t;
    }
}
