using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 鐘口の余韻と怯えの伝染。対象・付与時刻は既存の萎縮通知に従い、飛翔による命中判定は持たない。
public partial class KubiBellFx3D : Node3D
{
    internal const double Duration = .62;
    private Func<(bool Paused, double Speed)> Clock = () => (false, 1);
    internal double Age { get; private set; }
    private static bool Live(BattlePawn3D? pawn) => IsInstanceValid(pawn) && pawn!.IsInsideTree() && pawn.CanReceiveMovementImpact;
    private void End() { Hide(); QueueFree(); }
    private static readonly Color Brass = new("d4bb86");
    private static readonly Color Fear = new("b6a9c9");
    private static AudioStreamWav? _bell;
    private BattlePawn3D _source = null!;
    private BattlePawn3D[] _targets = Array.Empty<BattlePawn3D>();
    private Camera3D _camera = null!;
    private readonly ImmediateMesh _mesh = new();
    private Sprite3D _glint = null!;
    private AudioStreamPlayer _voice = null!;
    internal bool SoundPaused => _voice.StreamPaused;

    internal void Start(BattlePawn3D source, IReadOnlyList<BattlePawn3D> targets, Camera3D camera,
        Func<(bool, double)> clock)
    {
        _source = source; _targets = targets.ToArray(); _camera = camera; Clock = clock;
        AddChild(new MeshInstance3D {
            Mesh = _mesh, MaterialOverride = new StandardMaterial3D {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                NoDepthTest = true, RenderPriority = 7,
                VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true,
            }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, ExtraCullMargin = 15,
        });
        // 購入済みEffectBlocksの光粒を鐘の縁に1枚だけ使う。共有画像は変更しない。
        var texture = HagiLight3D.Sparkle;
        _glint = new Sprite3D { Texture = texture, PixelSize = .28f / texture.GetWidth(),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Shaded = false, NoDepthTest = true,
            Modulate = new Color(Brass, 0), RenderPriority = 9,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_glint);
        var (paused, speed) = Clock();
        _voice = new AudioStreamPlayer { Stream = Bell(), Bus = BattleAudioRouting.FieldBus, VolumeDb = -14,
            PitchScale = (float)Math.Clamp(speed, .5, 4), MaxPolyphony = 1 };
        AddChild(_voice); _voice.Play(); _voice.StreamPaused = paused;
        Draw();
    }

    public override void _Process(double delta)
    {
        if (!Live(_source) || !IsVisibleInTree()) { _voice.Stop(); End(); return; }
        var (paused, speed) = Clock();
        _voice.StreamPaused = paused;
        _voice.PitchScale = (float)Math.Clamp(speed, .5, 4);
        if (paused) return;
        Age += delta * speed;
        if (Age >= Duration) { _voice.Stop(); End(); return; }
        Draw();
    }

    public override void _ExitTree() { if (IsInstanceValid(_voice)) _voice.Stop(); }

    private void Draw()
    {
        float t = (float)(Age / Duration);
        Vector3 origin = _source.KubiBellPoint(_camera);
        _mesh.ClearSurfaces();
        _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 3; i++)
        {
            float p = t * 1.4f - i * .18f;
            if (p <= 0 || p >= 1) continue;
            Ring(origin, .12f + p * 1.35f, .018f * (1 - p * .6f), .48f,
                new Color(Brass, Mathf.Sin(p * Mathf.Pi) * (1 - p) * .65f));
        }
        foreach (var target in _targets.Where(Live))
        {
            Vector3 end = target.FxPoint + Vector3.Down * .30f;
            // 短い音の弧が実際の受け手へ広がる。味方への漏れも同じ拍で描く。
            Color tint = target.Team == _source.Team ? Fear : Brass;
            float p = Mathf.Clamp(t * 2, 0, 1);
            if (p > 0 && p < 1)
                Ring(origin.Lerp(end, p), .16f + p * .25f, .015f, .75f,
                    new Color(tint, Mathf.Sin(p * Mathf.Pi) * .45f), -.65f, .65f);
            float inward = Mathf.Clamp(t * 2.2f, 0, 1);
            if (inward > 0 && inward < 1)
                Ring(end, .72f - inward * .35f, .019f, .72f,
                    new Color(tint, Mathf.Sin(inward * Mathf.Pi) * .6f), .12f, .88f);
        }
        // t=0にも退化三角形を置き、空のImmediateMeshの警告を避ける。
        Vertex(origin, new Color(Brass, 0)); Vertex(origin, new Color(Brass, 0)); Vertex(origin, new Color(Brass, 0));
        _mesh.SurfaceEnd();
        _glint.GlobalPosition = origin;
        _glint.Modulate = new Color(Brass, Mathf.Sin(Mathf.Clamp(t * 3, 0, 1) * Mathf.Pi) * .65f);
    }

    private void Ring(Vector3 center, float radius, float width, float squash, Color color, float start = 0, float end = 1)
    {
        Vector3 Point(float turn, float r) => center + _camera.GlobalBasis.X * (Mathf.Cos(turn * Mathf.Tau) * r)
            + _camera.GlobalBasis.Y * (Mathf.Sin(turn * Mathf.Tau) * r * squash);
        const int segments = 40;
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.Lerp(start, end, i / (float)segments), b = Mathf.Lerp(start, end, (i + 1f) / segments);
            Vector3 ai = Point(a, radius - width), ao = Point(a, radius + width);
            Vector3 bi = Point(b, radius - width), bo = Point(b, radius + width);
            Vertex(ai, color); Vertex(bi, color); Vertex(ao, color);
            Vertex(ao, color); Vertex(bi, color); Vertex(bo, color);
        }
    }

    private void Vertex(Vector3 p, Color color) { _mesh.SurfaceSetColor(color); _mesh.SurfaceAddVertex(ToLocal(p)); }

    // 鳴子を押さえ損ねた短い濁った鐘。乱数・外部音源・戦闘ロジックに依存しない。
    private static AudioStreamWav Bell()
    {
        if (_bell is not null) return _bell;
        const int rate = 22050;
        var data = new byte[(int)(rate * Duration) * 2];
        for (int i = 0; i < data.Length / 2; i++)
        {
            double t = (double)i / rate;
            double value = Math.Sin(t * Math.Tau * 392) * Math.Exp(-t * 8)
                + .36 * Math.Sin(t * Math.Tau * 617) * Math.Exp(-t * 11)
                + .18 * Math.Sin(t * Math.Tau * 1063) * Math.Exp(-t * 18)
                + .12 * Math.Sin(t * Math.Tau * 247) * Math.Exp(-t * 6);
            double tail = Math.Min(1, (Duration - t) * 50);
            short sample = (short)(value * Math.Min(1, t * 350) * tail * 14500);
            data[i * 2] = (byte)(sample & 255); data[i * 2 + 1] = (byte)(sample >> 8);
        }
        return _bell = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = rate, Data = data };
    }
}
