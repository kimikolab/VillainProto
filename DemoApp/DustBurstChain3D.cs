using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// 放電→小さな着火→飛び火を一団の時計で重ねる。台本側のHP・死亡処理は待たせない。
public partial class DustBurstChain3D : Node3D
{
    internal const float ChainSeconds = .60f;
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task<bool> Completion => _completion.Task;
    internal float Age { get; private set; }
    internal int BurstCount => _bursts.Count;
    internal int BranchCount { get; private set; }
    private readonly List<(float At, Vector3 Point, PowderBurnBurst3D Fx, int Density, float Pan, bool Ally)> _bursts = new();
    private readonly List<(float At, Vector3 From, Vector3 To, float Width, bool Branch)> _arcs = new();
    private readonly List<(float At, GpuParticles3D Fx, bool Started)> _particles = new();
    private readonly List<(float At, Action Play)> _sounds = new();
    private readonly ImmediateMesh _mesh = new();
    private Camera3D _camera = null!;
    private bool _paused, _drawing, _summarized;
    private double _speed = 1;
    private Action _summary = null!;

    internal void Configure(Camera3D camera, Action summary)
    {
        _camera = camera; _summary = summary;
        AddChild(new MeshInstance3D { Mesh = _mesh, MaterialOverride = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
            VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            NoDepthTest = true, RenderPriority = 2,
        }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    internal void AddArc(float at, Vector3 from, Vector3 to, float width, bool branch = false)
    {
        _arcs.Add((at, from, to, width, branch));
        if (branch) BranchCount++;
    }

    internal void AddBurst(float at, Vector3 point, int density, float pan, bool ally,
        bool purchased, Action<int, float> sound)
    {
        var fx = new PowderBurnBurst3D(); AddChild(fx); fx.Configure(_camera, density, compact: true); fx.Hide();
        if (ally) fx.Brightness = .75f;
        _bursts.Add((at, point, fx, density, pan, ally));
        for (int pulse = 0; pulse < fx.PulseCount; pulse++)
            _sounds.Add((at + pulse * .045f, () => sound(density, pan)));
        // 購入済みの針状粒子を短い補助に。密集時もGPU粒子の emitter は8つまで。
        if (!purchased || _particles.Count >= 8 || !ResourceLoader.Exists(TouPowderFx3D.SparkPath)) return;
        var particles = GD.Load<PackedScene>(TouPowderFx3D.SparkPath).Instantiate<GpuParticles3D>();
        particles.SetScript(default);
        var process = (ParticleProcessMaterial)particles.ProcessMaterial.Duplicate(true);
        particles.ProcessMaterial = process; process.Gravity = Vector3.Zero;
        float level = PowderBurnBurst3D.Level(density);
        process.InitialVelocityMin = 1.0f; process.InitialVelocityMax = 1.6f + level * .15f;
        process.ScaleMin = .045f; process.ScaleMax = .075f + level * .006f;
        process.Color = ally ? new Color(.75f, .75f, .75f) : Colors.White;
        process.ColorRamp = new GradientTexture1D { Gradient = new Gradient {
            Offsets = new[] { 0f, .25f, 1f },
            Colors = new[] { new Color("ffd1d8"), new Color("ff3045"), new Color("a20b2700") },
        } };
        particles.MaterialOverride = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, NoDepthTest = true, RenderPriority = 2,
        };
        particles.Amount = 6 + (int)(level * 2); particles.Lifetime = .15; particles.OneShot = true;
        particles.Emitting = false; particles.LocalCoords = true; particles.UseFixedSeed = true; particles.Seed = 328;
        AddChild(particles); particles.GlobalPosition = point; particles.Hide();
        _particles.Add((at, particles, false));
    }

    internal void SetClock(bool paused, double speed)
    {
        _paused = paused; _speed = Math.Max(.1, speed);
        foreach (var p in _particles) p.Fx.SpeedScale = paused ? 0 : _speed;
    }

    public override void _Process(double delta)
    {
        if (_paused) return;
        Age += (float)(delta * _speed);
        for (int i = 0; i < _sounds.Count; i++)
            if (_sounds[i].At <= Age) { _sounds[i].Play(); _sounds.RemoveAt(i--); }
        for (int i = 0; i < _particles.Count; i++)
        {
            var p = _particles[i];
            if (!p.Started && Age >= p.At)
            { p.Fx.Show(); p.Fx.Emitting = true; p.Fx.Restart(); _particles[i] = (p.At, p.Fx, true); }
        }
        foreach (var b in _bursts)
        {
            float elapsed = Age - b.At;
            b.Fx.Visible = elapsed >= 0 && elapsed < b.Fx.Duration;
            if (b.Fx.Visible) b.Fx.Draw(elapsed, b.Point);
        }
        DrawArcs();
        if (!_summarized && Age >= .44f) { _summarized = true; _summary(); }
        if (Age >= ChainSeconds) { _completion.TrySetResult(true); Hide(); QueueFree(); }
    }

    private void DrawArcs()
    {
        _mesh.ClearSurfaces(); _drawing = false;
        foreach (var arc in _arcs)
        {
            float elapsed = Age - arc.At;
            if (elapsed < 0 || elapsed >= .16f) continue;
            float head = Math.Min(1, elapsed / .055f), tail = Math.Max(0, head - .55f);
            float fade = Math.Min(1, (.16f - elapsed) / .08f);
            Vector3 Point(float p) => arc.From.Lerp(arc.To, p) + _camera.GlobalBasis.Y
                * Mathf.Sin(p * Mathf.Pi) * Mathf.Sin(p * 44f + arc.At * 70) * .06f;
            for (int i = 0; i < 12; i++)
            {
                float p = i / 12f;
                if (p < tail || p > head) continue;
                Segment(Point(p), Point(Math.Min(head, (i + 1) / 12f)), arc.Width * 1.7f,
                    new Color(ThunderFx.Cyan, fade * .65f));
                Segment(Point(p), Point(Math.Min(head, (i + 1) / 12f)), arc.Width * .45f,
                    new Color(new Color("e3fbff"), fade));
            }
            if (arc.Branch)
            {
                Vector3 point = Point(head), up = _camera.GlobalBasis.Y * .022f;
                Segment(point - up, point + up, .022f, new Color(new Color("ff6473"), fade));
            }
        }
        if (_drawing) _mesh.SurfaceEnd();
    }

    private void Segment(Vector3 a, Vector3 b, float width, Color tint)
    {
        if (!_drawing) { _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles); _drawing = true; }
        Vector3 side = (b - a).Cross(_camera.GlobalBasis.Z).Normalized() * width;
        _mesh.SurfaceSetColor(tint);
        foreach (var p in new[] { a - side, a + side, b + side, a - side, b + side, b - side }) _mesh.SurfaceAddVertex(ToLocal(p));
    }

    internal void Cancel() { _completion.TrySetResult(false); Hide(); QueueFree(); }
    public override void _ExitTree() => _completion.TrySetResult(false);
}
