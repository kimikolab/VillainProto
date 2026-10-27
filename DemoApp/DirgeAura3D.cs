using Godot;
using System;

// 音が受け手の時計を進める。文字盤は固定し、針だけを時計回りに加速する。
public partial class DirgeAura3D : Node3D
{
    internal const float ReleaseContact = .70f, ReleaseDuration = 1.16f;
    private BattlePawn3D _target = null!;
    private Camera3D _camera = null!;
    private Func<(bool Paused, double Speed)> _clock = null!;
    private Color _tint;
    private readonly Sprite3D[] _notes = new Sprite3D[3];
    private ShaderMaterial _ring = null!;
    private MeshInstance3D _seal = null!;
    private float _age, _releaseAge;
    private float _releaseFrom, _releaseTo;
    internal int GiverId { get; private set; }
    internal int TargetId => _target.InstanceId;
    internal bool Releasing { get; private set; }
    internal float Age => _age;
    internal float HandTurns { get; private set; }

    internal void Configure(int giver, BattlePawn3D target, Camera3D camera, Color tint,
        Func<(bool Paused, double Speed)> clock)
    {
        GiverId = giver; _target = target; _camera = camera; _tint = tint; _clock = clock;
        for (int i = 0; i < _notes.Length; i++)
        {
            _notes[i] = AkaMagic.Sprite(this, target.FxPoint, EreGiftArt.Note, .28f);
            _notes[i].Modulate = tint;
        }
        _ring = new ShaderMaterial { Shader = GD.Load<Shader>("res://assets/fx/ere_clock.gdshader"), RenderPriority = 3 };
        _ring.SetShaderParameter("tint", tint);
        _ring.SetShaderParameter("score", EreGiftArt.ClockFace);
        _seal = FireFx.Card(this, Vector3.Zero, Vector2.One * 3.15f, _ring);
        Draw();
    }
    internal void Release()
    {
        if (Releasing) return;
        _releaseFrom = HandTurns;
        _releaseTo = Mathf.Ceil(_releaseFrom) + 2;
        Releasing = true; _releaseAge = 0; Draw();
    }
    internal void Stop()
    {
        if (IsInstanceValid(_target)) _target.SetDirgeGlow(0, _tint);
        Hide(); QueueFree();
    }
    public override void _ExitTree()
    { if (IsInstanceValid(_target)) _target.SetDirgeGlow(0, _tint); }
    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_target) || !_target.PresentationAlive) { Stop(); return; }
        var (paused, speed) = _clock();
        if (!paused) { _age += (float)(delta * speed); if (Releasing) _releaseAge += (float)(delta * speed); }
        if (Releasing && _releaseAge >= ReleaseDuration) { Stop(); return; }
        Draw();
    }
    private void Draw()
    {
        GlobalPosition = _target.FxPoint - Vector3.Up * .28f;
        float t = _releaseAge;
        float gather = Releasing ? Mathf.Clamp(t / ReleaseContact, 0, 1) : 0;
        float burst = Releasing ? Mathf.Clamp((t - ReleaseContact) / (ReleaseDuration - ReleaseContact), 0, 1) : 0;
        float pulse = .82f + Mathf.Sin(_age * 4) * .18f;
        float open = Mathf.SmoothStep(0, 1, Mathf.Clamp(_age / .22f, 0, 1));
        float arrival = Mathf.Exp(-Mathf.Pow((_age - .24f) / .24f, 2));
        float flash = Releasing ? Mathf.Exp(-Mathf.Pow((t - ReleaseContact) / .075f, 2)) : 0;
        // 受け取りで一度巻き、待機中はゆっくり進める。実手番は加速→12時で止めて解放。
        float winding = Mathf.Clamp(_age / .70f, 0, 1);
        HandTurns = Releasing ? Mathf.Lerp(_releaseFrom, _releaseTo,
            gather * gather * (3 - 2 * gather)) : 1.25f * (1 - Mathf.Pow(1 - winding, 3)) + Math.Max(0, _age - .70f) * .035f;
        _target.SetDirgeGlow(Releasing ? flash * .82f + (1 - burst) * .18f : open * (.16f * pulse + arrival * .18f), _tint);
        _seal.Scale = Vector3.One * (Releasing ? .88f + Mathf.Sin(gather * Mathf.Pi * .5f) * .20f + burst * .24f
            : .76f + open * .12f + arrival * .10f);
        _ring.SetShaderParameter("turns", HandTurns);
        _ring.SetShaderParameter("orbit", _age * .7f);
        _ring.SetShaderParameter("flare", Releasing ? flash : arrival * .5f);
        _ring.SetShaderParameter("ripple", burst);
        _ring.SetShaderParameter("opacity", Releasing ? (1 - Mathf.SmoothStep(0, 1, burst)) * (.82f + flash * .18f)
            : open * (.42f + arrival * .42f));
        for (int i = 0; i < _notes.Length; i++)
        {
            float angle = _age * 1.7f + i * Mathf.Tau / _notes.Length + gather * 2.8f;
            float radius = Releasing ? .95f * (1 - gather) + burst * 1.45f : .95f;
            var note = _notes[i];
            note.GlobalPosition = GlobalPosition + _camera.GlobalBasis.X * (Mathf.Cos(angle) * radius)
                + _camera.GlobalBasis.Y * (Mathf.Sin(angle) * radius * .62f + burst * .65f);
            note.Scale = Vector3.One * (Releasing ? 1 + flash * .4f + burst * .25f : 1);
            note.Modulate = new Color(_tint, Releasing ? (1 - burst) * .9f : open * .75f);
        }
    }
}
