using Godot;
using System;

// 音を受け取ってから実手番まで保持し、解放を身体の発光と動く音符だけで描く。
public partial class DirgeAura3D : Node3D
{
    private BattlePawn3D _target = null!;
    private Camera3D _camera = null!;
    private Func<(bool Paused, double Speed)> _clock = null!;
    private Color _tint;
    private readonly Sprite3D[] _notes = new Sprite3D[3];
    private ShaderMaterial _ring = null!;
    private MeshInstance3D _seal = null!;
    private float _age, _releaseAge;
    internal int GiverId { get; private set; }
    internal int TargetId => _target.InstanceId;
    internal bool Releasing { get; private set; }
    internal float Age => _age;

    internal void Configure(int giver, BattlePawn3D target, Camera3D camera, Color tint,
        Func<(bool Paused, double Speed)> clock)
    {
        GiverId = giver; _target = target; _camera = camera; _tint = tint; _clock = clock;
        for (int i = 0; i < _notes.Length; i++)
        {
            _notes[i] = AkaMagic.Sprite(this, target.FxPoint, EreGiftArt.Note, .28f);
            _notes[i].Modulate = tint;
        }
        _ring = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded,cull_disabled,blend_add,depth_draw_never,depth_test_disabled;
uniform sampler2D decal : filter_linear;
uniform vec4 tint : source_color;
uniform float angle=0.0;
uniform float opacity=0.0;
uniform bool billboard=true;
void vertex(){
 if(billboard) MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0]*length(MODEL_MATRIX[0].xyz),
 INV_VIEW_MATRIX[1]*length(MODEL_MATRIX[1].xyz),INV_VIEW_MATRIX[2],MODEL_MATRIX[3]);
}
void fragment(){
 vec2 p=UV-0.5;
 p=mat2(vec2(cos(angle),-sin(angle)),vec2(sin(angle),cos(angle)))*p+0.5;
 float inside=step(0.0,p.x)*step(p.x,1.0)*step(0.0,p.y)*step(p.y,1.0);
 float mask=texture(decal,clamp(p,vec2(0.0),vec2(1.0))).a*inside;
 ALBEDO=tint.rgb; EMISSION=tint.rgb*0.8; ALPHA=mask*opacity;
}" } };
        _ring.SetShaderParameter("decal", EreGiftArt.Encore);
        _ring.SetShaderParameter("tint", tint);
        _seal = FireFx.Card(this, Vector3.Zero, Vector2.One * 1.65f, _ring);
        Draw();
    }
    internal void Release() { Releasing = true; _releaseAge = 0; Draw(); }
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
        if (Releasing && _releaseAge >= .88f) { Stop(); return; }
        Draw();
    }
    private void Draw()
    {
        GlobalPosition = _target.FxPoint - Vector3.Up * .18f;
        float t = _releaseAge;
        float gather = Releasing ? Mathf.Clamp(t / .22f, 0, 1) : 0;
        float burst = Releasing ? Mathf.Clamp((t - .22f) / .66f, 0, 1) : 0;
        float pulse = .82f + Mathf.Sin(_age * 4) * .18f;
        float open = Mathf.Clamp(_age / .18f, 0, 1);
        float flash = Releasing ? Mathf.Exp(-Mathf.Pow((t - .25f) / .10f, 2)) : 0;
        _target.SetDirgeGlow(Releasing ? flash * .9f + (1 - burst) * .20f : open * .20f * pulse, _tint);
        _seal.Position = Vector3.Down * .18f;
        _seal.Scale = Vector3.One * (Releasing ? .88f - gather * .38f + burst * 1.40f : .80f + pulse * .05f);
        _ring.SetShaderParameter("angle", Releasing ? _age * 1.4f + gather * Mathf.Tau + burst * 2 : _age * .45f);
        _ring.SetShaderParameter("opacity", Releasing ? (1 - burst) * (.40f + flash * .5f) : open * .33f * pulse);
        for (int i = 0; i < _notes.Length; i++)
        {
            float angle = _age * 1.7f + i * Mathf.Tau / _notes.Length + gather * 2.8f;
            float radius = Releasing ? .60f * (1 - gather) + burst * 1.15f : .60f;
            var note = _notes[i];
            note.GlobalPosition = GlobalPosition + _camera.GlobalBasis.X * (Mathf.Cos(angle) * radius)
                + _camera.GlobalBasis.Y * (Mathf.Sin(angle) * radius * .62f + burst * .65f);
            note.Scale = Vector3.One * (Releasing ? 1 + flash * .4f + burst * .25f : 1);
            note.Modulate = new Color(_tint, Releasing ? (1 - burst) * .9f : open * .75f);
        }
    }
}
