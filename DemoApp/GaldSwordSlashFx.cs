using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// 一刀の大きな白金の剣閃。経過は再生速度で進め、TIME・素材側の自動反復には任せない。
public partial class GaldSwordSlashFx : Node3D
{
    internal const string ImpactPath = "res://PolyBlocks/EffectBlocks/assets/impacts/impact_4.tscn";
    private static Shader? _shader;
    private static bool _shaderShutdownHooked;
    private readonly List<ShaderMaterial> _materials = new();
    private readonly List<GpuParticles3D> _particles = new();
    private Func<double> _speed = () => 1;
    private Func<bool> _live = () => false;
    private double _elapsed;
    internal double Elapsed => _elapsed;
    internal int PurchasedImpacts => _particles.Count;
    internal int TargetCount { get; private set; }
    internal Vector2 SlashSize { get; private set; }

    private const string Code = @"
shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never, depth_test_disabled;
uniform float progress = 0.0;
uniform float direction = 1.0;
uniform int kind = 0;
void fragment() {
    vec2 p = UV - vec2(0.5);
    vec3 warm = vec3(1.0, 0.85, 0.49);
    vec3 cold = vec3(0.42, 0.72, 1.0);
    float alpha = 0.0;
    vec3 color = warm;
    if (kind == 0) {
        // 上から下へ走る一本の刃。中央が広く、両端を尖らせて一閃と読める形にする。
        vec2 along = normalize(vec2(direction * 0.48, 1.0));
        vec2 across = vec2(along.y, -along.x);
        float s = dot(p, along);
        float u = dot(p, across);
        float bend = direction * 0.13 * (1.0 - 4.0 * s * s);
        float taper = pow(max(0.0, sin((s + 0.5) * 3.14159)), 0.65);
        float width = 0.012 + taper * 0.072;
        float blade = exp(-pow((u - bend) / width, 2.0) * 2.0);
        float core = exp(-pow((u - bend) / (0.005 + taper * 0.013), 2.0) * 2.0);
        float rim = exp(-pow((abs(u - bend) - width * 0.83) / 0.006, 2.0));
        float halo = exp(-pow((u - bend) / (width * 2.8), 2.0) * 2.0) * 0.24;
        float head = mix(-0.60, 0.65, min(1.0, progress / 0.16));
        float reveal = 1.0 - smoothstep(head - 0.045, head + 0.02, s);
        float ends = 1.0 - smoothstep(0.44, 0.51, abs(s));
        float fade = 1.0 - smoothstep(0.22, 1.0, progress);
        color = mix(warm, vec3(1.0), clamp(core * 1.8, 0.0, 1.0)) + cold * rim * 0.8;
        alpha = min(1.0, blade * 0.92 + core + halo + rim * 0.55) * ends * reveal * fade;
    } else {
        // 命中点の短い白芯と放射状の火花。剣閃より先に消えて敵の姿を戻す。
        float r = length(p);
        float a = atan(p.y, p.x);
        float flash = exp(-r*r*80.0) * (1.0 - smoothstep(0.0, 0.30, progress));
        float rays = pow(max(0.0, cos(a * 9.0 + 0.3)), 22.0);
        float reach = 0.05 + progress * 0.50;
        float sparks = rays * exp(-pow((r-reach) / 0.10, 2.0)) * (1.0 - smoothstep(0.15, 0.75, progress));
        float ring = exp(-pow((r - progress * 0.55) / 0.012, 2.0)) * (1.0-progress) * 0.45;
        color = mix(warm, vec3(1.0), flash);
        alpha = clamp(flash + sparks + ring, 0.0, 1.0);
    }
    ALBEDO = color;
    EMISSION = color * 1.5;
    ALPHA = alpha;
}";

    internal void Start(Vector3 from, Vector3[] targets, Camera3D camera, Func<double> speed, Func<bool> live)
    {
        _speed = speed; _live = live;
        TargetCount = targets.Length;
        Vector3 right = camera.GlobalBasis.X.Normalized();
        Vector3 up = camera.GlobalBasis.Y.Normalized();
        Vector3 view = camera.GlobalBasis.Z.Normalized();
        Vector3 center = targets.Aggregate(Vector3.Zero, (a,b) => a+b) / targets.Length;
        float width = targets.Max(p => p.Dot(right)) - targets.Min(p => p.Dot(right));
        float height = targets.Max(p => p.Dot(up)) - targets.Min(p => p.Dot(up));
        SlashSize = new Vector2(Math.Max(4.6f, width+3.2f), Math.Max(5.0f, height+4.0f));
        float side = camera.UnprojectPosition(center).X >= camera.UnprojectPosition(from).X ? 1 : -1;
        AddCard(center + view * 0.25f, SlashSize, camera.GlobalBasis.Orthonormalized(), 0, side);
        foreach (Vector3 hit in targets)
        {
            AddCard(hit + view * 0.30f, new Vector2(2.8f,2.8f), camera.GlobalBasis.Orthonormalized(), 1, side);
            AddPurchasedImpact(hit);
        }
    }

    private void AddCard(Vector3 center, Vector2 size, Basis basis, int kind, float direction)
    {
        if (!_shaderShutdownHooked)
        {
            _shaderShutdownHooked = true;
            GetTree().Root.TreeExiting += () => { _shader?.Dispose(); _shader = null; };
        }
        _shader ??= new Shader { Code = Code };
        var material = new ShaderMaterial { Shader = _shader, RenderPriority = 12 };
        material.SetShaderParameter("kind", kind);
        material.SetShaderParameter("direction", direction);
        var mesh = new MeshInstance3D { Mesh = new QuadMesh { Size = size }, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(mesh);
        mesh.GlobalTransform = new Transform3D(basis, center);
        _materials.Add(material);
    }

    private void AddPurchasedImpact(Vector3 center)
    {
        if (!ResourceLoader.Exists(ImpactPath)) return;
        var particles = GD.Load<PackedScene>(ImpactPath).Instantiate<GpuParticles3D>();
        particles.SetScript(default);
        particles.Emitting = false;
        particles.Amount = 28;
        particles.Lifetime = 0.45;
        particles.OneShot = true;
        particles.Explosiveness = 1;
        particles.UseFixedSeed = true;
        particles.Seed = (uint)(401 + _particles.Count);
        particles.Preprocess = 0;
        var process = (ParticleProcessMaterial)particles.ProcessMaterial.Duplicate(true);
        process.Color = new Color("fff0c9");
        process.InitialVelocityMin = 3.2f;
        process.InitialVelocityMax = 6.0f;
        process.ScaleMin = 0.08f;
        process.ScaleMax = 0.22f;
        process.Gravity = new Vector3(0,-3.0f,0);
        particles.ProcessMaterial = process;
        var material = (StandardMaterial3D)particles.DrawPass1.SurfaceGetMaterial(0).Duplicate(true);
        material.EmissionEnabled = true;
        material.Emission = new Color("ffdf9a");
        material.EmissionEnergyMultiplier = 1.7f;
        particles.MaterialOverride = material;
        AddChild(particles);
        particles.GlobalPosition = center;
        particles.SpeedScale = Math.Max(0.1, _speed());
        _particles.Add(particles);
        particles.Emitting = true;
        particles.Restart();
    }

    public override void _Process(double delta)
    {
        if (!_live()) { Cancel(); return; }
        double speed = Math.Max(0.1, _speed());
        _elapsed += delta * speed;
        float p = (float)Math.Min(1, _elapsed / 0.62);
        foreach (var material in _materials) material.SetShaderParameter("progress", p);
        foreach (var particles in _particles) particles.SpeedScale = speed;
        if (_elapsed >= 0.75) Cancel();
    }

    internal void Cancel() { Hide(); SetProcess(false); QueueFree(); }
}
