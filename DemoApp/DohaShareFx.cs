using Godot;
using System;
using System.Threading.Tasks;

// 痛みは暗い帯と三つの塊で胸へ流す。購入素材は薄い煙だけに使い、方向を隠さない。
public partial class DohaShareFx : Node3D
{
    internal const string SmokePath = "res://PolyBlocks/EffectBlocks/assets/smoke/smoke_light.tscn";
    private readonly ImmediateMesh _ribbon = new();
    private readonly Sprite3D[] _motes = new Sprite3D[3];
    private GpuParticles3D? _smoke;
    private Func<bool> _live = () => false;
    private Vector3 _from, _to, _up;
    private double _elapsed, _travel, _speed, _endAt = double.PositiveInfinity;
    private bool _received;
    private bool _showImpact;
    // ツリー破棄中に待ち手が次の演出をAddChildしないよう、継続は次の処理へ送る。
    private readonly TaskCompletionSource<bool> _arrival = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _give = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task<bool> Arrival => _arrival.Task;
    internal Task<bool> ReadyToGive => _give.Task;
    internal bool UsesPurchasedSmoke => _smoke is not null;

    // 倍速でも流れを追う時間を残し、検証用の超高速再生では待ち時間を引き延ばさない。
    internal static double TravelSeconds(double speed) => 0.38 / (speed <= 2 ? Math.Sqrt(speed) : speed / Math.Sqrt(2));

    internal void Start(Vector3 from, Vector3 to, Camera3D camera, double speed, Func<bool> live,
        bool usePurchased = true, bool showImpact = true)
    {
        _from = from; _to = to; _up = camera.GlobalBasis.Y;
        _travel = TravelSeconds(speed); _speed = speed; _live = live;
        _showImpact = showImpact;
        AddChild(new MeshInstance3D {
            Mesh = _ribbon,
            MaterialOverride = new StandardMaterial3D {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        for (int i = 0; i < _motes.Length; i++)
            _motes[i] = ShockMarkFx.Sprite(this, from, ShockMarkFx.Halo, 0.58f - i * 0.1f, new Color("ae69ad"));
        if (usePurchased && ResourceLoader.Exists(SmokePath))
        {
            _smoke = GD.Load<PackedScene>(SmokePath).Instantiate<GpuParticles3D>();
            _smoke.SetScript(default);
            var process = (ParticleProcessMaterial)_smoke.ProcessMaterial.Duplicate(true);
            _smoke.ProcessMaterial = process;
            process.Gravity = Vector3.Zero;
            process.EmissionSphereRadius = 0.09f;
            process.ScaleMin = 0.11f; process.ScaleMax = 0.19f;
            process.Color = Colors.White;
            process.ColorRamp = new GradientTexture1D { Gradient = new Gradient {
                Offsets = new[] { 0f, 0.3f, 1f },
                Colors = new[] { new Color("98558fb0"), new Color("542d6090"), new Color("37213f00") },
            } };
            // 共有メッシュ／マテリアルを書き換えず、この発動の描画材だけを複製する。
            var material = (StandardMaterial3D)_smoke.DrawPass1.SurfaceGetMaterial(0).Duplicate();
            material.BlendMode = BaseMaterial3D.BlendModeEnum.Mix;
            _smoke.MaterialOverride = material;
            _smoke.Amount = 18; _smoke.Lifetime = 0.24; _smoke.Preprocess = 0;
            _smoke.LocalCoords = false; _smoke.SpeedScale = 0.38 / _travel;
            _smoke.UseFixedSeed = true; _smoke.Seed = 298;
            _smoke.Emitting = false;
            AddChild(_smoke); _smoke.GlobalPosition = from;
            _smoke.Emitting = true; _smoke.Restart();
        }
        Draw(0);
    }

    public override void _Process(double delta)
    {
        if (!_live()) { Cancel(); return; }
        _elapsed += delta;
        if (!_received)
        {
            Draw((float)Math.Min(1, _elapsed / _travel));
            if (_elapsed >= _travel) { Receive(_speed); _arrival.TrySetResult(true); }
        }
        if (_elapsed >= _travel + 0.10 / _speed) _give.TrySetResult(true);
        if (_elapsed >= _endAt) Cancel();
    }

    private Vector3 Path(float p) => _from.Lerp(_to, p) - _up * Mathf.Sin(p * Mathf.Pi) * 0.24f;

    private void Draw(float progress)
    {
        _ribbon.ClearSurfaces();
        // 暗い外縁で石床から分離し、紫の内側で黒い服からも読めるようにする。
        foreach (var (width, tint) in new[] { (0.24f, new Color("321b40cc")), (0.13f, new Color("ab6aaba0")) })
        {
            _ribbon.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
            for (int i = 0; i <= 28; i++)
            {
                float p = i / 28f;
                float taper = 0.35f + 0.65f * Mathf.Sin(p * Mathf.Pi);
                _ribbon.SurfaceSetColor(new Color(tint, tint.A * (0.5f + 0.5f * progress)));
                _ribbon.SurfaceAddVertex(ToLocal(Path(p) + _up * width * taper * 0.5f));
                _ribbon.SurfaceAddVertex(ToLocal(Path(p) - _up * width * taper * 0.5f));
            }
            _ribbon.SurfaceEnd();
        }
        for (int i = 0; i < _motes.Length; i++)
        {
            float p = Mathf.Clamp(progress * (1 + i * 0.30f) - i * 0.30f, 0, 1);
            _motes[i].GlobalPosition = Path(p);
            _motes[i].Scale = Vector3.One * (0.8f + Mathf.Sin(p * Mathf.Pi) * 0.35f);
        }
        if (_smoke is not null) _smoke.GlobalPosition = Path(progress);
    }

    private void Receive(double speed)
    {
        _received = true;
        _ribbon.ClearSurfaces();
        foreach (var mote in _motes) mote.Hide();
        if (_smoke is not null) _smoke.Emitting = false;
        if (!_showImpact) return;
        ShockMarkFx.Glow(this, _to, new Color("b579bc"), 0.85f, 0.22 / speed);
        ShockMarkFx.Ring(this, _to, new Color("845685"), 0.65f, 0.20 / speed);
    }

    internal void Finish(double seconds) => _endAt = _elapsed + seconds;

    internal void Cancel()
    {
        Hide(); QueueFree();
        _arrival.TrySetResult(false); _give.TrySetResult(false);
    }

    public override void _ExitTree() { _arrival.TrySetResult(false); _give.TrySetResult(false); }
}
