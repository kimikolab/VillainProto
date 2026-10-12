using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

internal enum ElectricWhipStyle { Flurry, WideSwing }

// 短い打ち付けの軌跡、または大きな円弧の残光と、飛び散る電気を一つの時計で描く。
// 命中の相手と時刻は再生側が渡す。ここでは戦闘の判定をしない。
public partial class ElectricWhip3D : Node3D
{
    internal const string SparksPath = "res://PolyBlocks/EffectBlocks/assets/energy/electric_sparks_2.tscn";
    private const int Segments = 56;
    private readonly ImmediateMesh _glow = new(), _current = new(), _trail = new(), _sparks = new();
    private readonly List<(float Born, int Stroke, int Target)> _strokes = new();
    private readonly List<(Vector3 Point, float Born)> _bursts = new();
    private readonly List<GpuParticles3D> _particles = new();
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Func<Vector3> _origin = null!;
    private Func<Vector3>[] _targets = null!;
    private Func<bool> _live = null!;
    private Action<int> _contact = null!;
    private Camera3D _camera = null!;
    private double _speed;
    private float _elapsed, _duration, _power;
    private bool _sweep, _paused;
    private int _landed;
    internal Task Completion => _completion.Task;
    internal float Elapsed => _elapsed;
    internal int Contacts => _landed;
    internal int PurchasedBursts => _particles.Count;
    internal bool UsePurchasedSparks { get; set; } = true;
    internal ElectricWhipStyle Style { get; set; } = ElectricWhipStyle.Flurry;

    internal void Start(Camera3D camera, Func<Vector3> origin, Func<Vector3>[] targets,
        bool sweep, int chain, double speed, Func<bool> live, Action<int> contact)
    {
        _camera = camera; _origin = origin; _targets = targets; _sweep = sweep;
        _speed = Math.Max(0.1, speed); _live = live; _contact = contact;
        _duration = sweep ? 0.64f : 0.60f;
        _power = 1 + Math.Min(chain, 8) * 0.12f;
        Layer(_trail, new Color("8e74ff"), true);
        Layer(_glow, new Color("64dfff"), true);
        Layer(_current, new Color("f2fcff"), true);
        Layer(_sparks, new Color("ccf7ff"), true);
        for (int i = 0; i < 12; i++)
            _strokes.Add((_duration * (0.12f + i * 0.06f), i, i % targets.Length));
        for (int i = 0; i < targets.Length; i++)
            _strokes.Add((_duration * ContactPhase(i) - 0.035f, i + 12, i));
        Draw();
    }

    private void Layer(ImmediateMesh mesh, Color tint, bool luminous)
    {
        AddChild(new MeshInstance3D {
            Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                VertexColorUseAsAlbedo = true, AlbedoColor = tint,
                // 薄い軌跡を立ち絵の裏へ埋めず、打ち付けの交差が読めるようにする。
                NoDepthTest = mesh != _sparks,
                RenderPriority = mesh == _current ? 3 : mesh == _glow ? 2 : 1,
                EmissionEnabled = luminous, Emission = tint,
                EmissionEnergyMultiplier = luminous ? 1.4f : 0,
            },
        });
    }

    internal void SetClock(bool paused, double speed)
    {
        _paused = paused; _speed = Math.Max(0.1, speed);
        foreach (var particle in _particles)
            if (IsInstanceValid(particle)) particle.SpeedScale = paused ? 0 : _speed;
    }

    public override void _Process(double delta)
    {
        if (!_live()) { Cancel(); return; }
        if (_paused) return;
        _elapsed += (float)(delta * _speed);
        float t = _elapsed / _duration;
        while (_landed < _targets.Length && t >= ContactPhase(_landed))
        {
            int index = _landed++;
            var at = _targets[index]();
            _bursts.Add((at, _elapsed));
            PurchasedSpark(at);
            _contact(index);
        }
        Draw();
        if (_elapsed >= _duration) _completion.TrySetResult(true);
        if (_elapsed >= _duration + 0.38f) Cancel();
    }

    private float ContactPhase(int index) => _sweep
        ? 0.22f + 0.55f * index / Math.Max(1, _targets.Length - 1) : 0.62f;

    private Vector3 SlashPoint(int stroke, int target, float u)
    {
        // 一筆の滑らかな放物線。先端まで鋭く絞り、胴体のような太い芯や蛇行を持たせない。
        float angle = (stroke % 7) switch { 0 => -0.65f, 1 => 0.45f, 2 => 1.35f,
            3 => -1.15f, 4 => 0.05f, 5 => 0.85f, _ => -0.35f };
        float facing = (_targets[target]() - _origin()).Dot(_camera.GlobalBasis.X) >= 0 ? 1 : -1;
        Vector3 axis = _camera.GlobalBasis.X * Mathf.Cos(angle) * facing + _camera.GlobalBasis.Y * Mathf.Sin(angle);
        Vector3 normal = _camera.GlobalBasis.Y * Mathf.Cos(angle) - _camera.GlobalBasis.X * Mathf.Sin(angle) * facing;
        float q = u * 2 - 1;
        float span = (2.0f + stroke % 3 * 0.35f) * (1 + (_power - 1) * 0.25f);
        float bend = (stroke % 2 == 0 ? 1 : -1) * (0.5f + stroke % 3 * 0.45f);
        return _targets[target]() + axis * q * span + normal * q * q * bend
            + _camera.GlobalBasis.Z * 0.16f;
    }

    private void DrawFlurry()
    {
        foreach (var (born, stroke, target) in _strokes)
        {
            float age = _elapsed - born;
            if (age < 0 || age >= 0.16f) continue;
            float head = Mathf.Clamp(age / 0.07f, 0, 1);
            float tail = Mathf.Clamp((age - 0.06f) / 0.10f, 0, 1);
            float fade = 1 - Mathf.Clamp((age - 0.065f) / 0.095f, 0, 1);
            for (int i = 0; i < Segments; i++)
            {
                float u = Mathf.Lerp(tail, head, i / (float)Segments), v = Mathf.Lerp(tail, head, (i + 1f) / Segments);
                float taper = Mathf.Pow(Mathf.Max(0, Mathf.Sin(i / (float)Segments * Mathf.Pi)), 0.5f);
                Vector3 a = SlashPoint(stroke, target, u), b = SlashPoint(stroke, target, v);
                Ribbon(_trail, a, b, 0.22f * taper * _power, fade * 0.22f);
                Ribbon(_glow, a, b, 0.11f * taper * _power, fade * 0.65f);
                Ribbon(_current, a, b, 0.025f * taper * _power, fade);
            }
        }
    }

    private void DrawWideSwing()
    {
        Vector3 start = _origin(), far = _targets[0]();
        foreach (var target in _targets)
            if (target().DistanceSquaredTo(start) > far.DistanceSquaredTo(start)) far = target();
        var right = _camera.GlobalBasis.X; var up = _camera.GlobalBasis.Y;
        float facing = (far - start).Dot(right) >= 0 ? 1 : -1;
        float radius = Math.Max(2.1f, Math.Abs((far - start).Dot(right)) * 0.62f + 0.7f);
        Vector3 center = start.Lerp(far, 0.45f) - up * 0.40f + _camera.GlobalBasis.Z * 0.20f;
        float progress = Mathf.Clamp((_elapsed - 0.045f) / (_duration * 0.74f), 0, 1);
        float tail = Mathf.Max(0, progress - 0.68f);
        float fade = 1 - Mathf.Clamp((_elapsed - _duration * 0.78f) / 0.12f, 0, 1);
        Vector3 Point(float q, float offset = 0)
        {
            float angle = -2.9f + q * Mathf.Tau * 0.94f;
            return center + right * facing * Mathf.Cos(angle) * (radius + offset)
                + up * Mathf.Sin(angle) * (radius * 0.38f + offset);
        }
        for (int i = 0; i < Segments * 2; i++)
        {
            float p = i / (float)(Segments * 2);
            float q = Mathf.Lerp(tail, progress, p), r = Mathf.Lerp(tail, progress, (i + 1f) / (Segments * 2));
            float taper = Mathf.Pow(Mathf.Sin(p * Mathf.Pi), 0.6f);
            float alpha = fade * Mathf.Lerp(0.12f, 1, p);
            Ribbon(_trail, Point(q), Point(r), 0.26f * taper * _power, alpha * 0.25f);
            Ribbon(_glow, Point(q), Point(r), 0.13f * taper * _power, alpha * 0.75f);
            Ribbon(_current, Point(q), Point(r), 0.033f * taper * _power, alpha);
            // 主軌道の外に細い残光を一本。大きな振り回しの速度を面ではなく線で示す。
            Ribbon(_current, Point(q, 0.16f), Point(r, 0.16f), 0.006f * taper, alpha * 0.40f);
        }
    }

    private void Draw()
    {
        foreach (var mesh in new[] { _glow, _current, _trail, _sparks }) mesh.ClearSurfaces();
        bool visible = Style == ElectricWhipStyle.Flurry
            ? _strokes.Exists(s => _elapsed >= s.Born && _elapsed < s.Born + 0.16f)
            : _elapsed > 0.045f && _elapsed < _duration * 0.78f + 0.12f;
        if (visible)
        {
            foreach (var mesh in new[] { _glow, _current, _trail }) mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            if (Style == ElectricWhipStyle.Flurry) DrawFlurry(); else DrawWideSwing();
            foreach (var mesh in new[] { _glow, _current, _trail }) mesh.SurfaceEnd();
        }
        if (!_bursts.Exists(b => _elapsed - b.Born < 0.36f)) return;
        _sparks.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        foreach (var (point, born) in _bursts)
        {
            float age = (_elapsed - born) / 0.36f;
            if (age >= 1) continue;
            for (int k = 0; k < 18; k++)
            {
                float angle = k * 2.399963f;
                Vector3 ray = (_camera.GlobalBasis.X * Mathf.Cos(angle) + _camera.GlobalBasis.Y * Mathf.Sin(angle)).Normalized();
                float travel = (0.3f + Mathf.Sqrt(age) * (0.95f + k % 4 * 0.24f)) * _power;
                Vector3 head = point + ray * travel - Vector3.Up * age * age * 0.5f;
                Vector3 tail = head - ray * (0.12f + (1 - age) * 0.26f);
                Vector3 kink = tail.Lerp(head, 0.55f) + _camera.GlobalBasis.Y * (k % 2 == 0 ? 0.07f : -0.07f);
                Ribbon(_sparks, tail, kink, 0.018f * (1 - age), 1 - age);
                Ribbon(_sparks, kink, head, 0.018f * (1 - age), 1 - age);
                if (age < 0.3f) Ribbon(_sparks, point, point + ray * 0.65f * _power, 0.025f, (1 - age / 0.3f) * 0.7f);
            }
        }
        _sparks.SurfaceEnd();
    }

    private void Ribbon(ImmediateMesh mesh, Vector3 a, Vector3 b, float width, float alpha)
    {
        Vector3 side = (b - a).Cross(_camera.GlobalBasis.Z).Normalized() * width;
        if (mesh == _glow || mesh == _trail)
        {
            // 光の外縁を透明へ落とす。硬い帯の輪郭を残すと再び太い管に見える。
            void Vertex(Vector3 point, float opacity)
            {
                mesh.SurfaceSetColor(new Color(1, 1, 1, opacity)); mesh.SurfaceAddVertex(ToLocal(point));
            }
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vertex(a, alpha); Vertex(b, alpha); Vertex(a + side * sign, 0);
                Vertex(a + side * sign, 0); Vertex(b, alpha); Vertex(b + side * sign, 0);
            }
            return;
        }
        Quad(mesh, a - side, b - side, a + side, b + side, alpha);
    }

    private void Quad(ImmediateMesh mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float alpha)
    {
        mesh.SurfaceSetColor(new Color(1, 1, 1, alpha));
        mesh.SurfaceAddVertex(ToLocal(a)); mesh.SurfaceAddVertex(ToLocal(b)); mesh.SurfaceAddVertex(ToLocal(c));
        mesh.SurfaceAddVertex(ToLocal(c)); mesh.SurfaceAddVertex(ToLocal(b)); mesh.SurfaceAddVertex(ToLocal(d));
    }

    private void PurchasedSpark(Vector3 at)
    {
        if (!UsePurchasedSparks || !ResourceLoader.Exists(SparksPath)) return;
        var particle = GD.Load<PackedScene>(SparksPath).Instantiate<GpuParticles3D>();
        particle.SetScript(default);
        var process = (ParticleProcessMaterial)particle.ProcessMaterial.Duplicate(true);
        process.Gravity = new Vector3(0, -2.0f, 0);
        process.InitialVelocityMin = 3.8f * _power; process.InitialVelocityMax = 7.0f * _power;
        process.ScaleMin = 0.09f; process.ScaleMax = 0.17f;
        process.Color = new Color("a9efff");
        particle.ProcessMaterial = process;
        var material = (StandardMaterial3D)particle.DrawPass1.SurfaceGetMaterial(0).Duplicate();
        material.VertexColorUseAsAlbedo = true;
        particle.MaterialOverride = material;
        particle.Amount = 22; particle.Lifetime = 0.32; particle.OneShot = true;
        particle.Explosiveness = 1; particle.Preprocess = 0;
        particle.SpeedScale = _speed; particle.UseFixedSeed = true; particle.Seed = (uint)(318 + _landed);
        particle.Emitting = false;
        AddChild(particle); particle.GlobalPosition = at;
        particle.Emitting = true; particle.Restart(); _particles.Add(particle);
    }

    internal void Cancel() { Hide(); QueueFree(); _completion.TrySetResult(false); }
    public override void _ExitTree() => _completion.TrySetResult(false);
}
