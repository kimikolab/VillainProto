using Godot;
using System;
using System.Threading.Tasks;

internal enum TouPowderFxKind { Settle, Lift, Thread, Contagion, Pop, Burn, Heavy, DustFall }

// 粉の移動は毎フレーム描く。一時停止・途中の倍速変更も同じ時計で受ける。
public partial class TouPowderFx3D : Node3D
{
    internal const string SparkPath = "res://PolyBlocks/EffectBlocks/assets/energy/electric_sparks_2.tscn";
    private static Texture2D? _grain;
    internal static Texture2D Grain => _grain ??= UiKit.LoadTexture("res://assets/fx/tou_powder_grain.svg", mipmaps: true);
    private readonly ImmediateMesh _mesh = new();
    private readonly Sprite3D[] _motes = new Sprite3D[9];
    private Sprite3D? _core, _smoke;
    private Label3D? _label;
    private GpuParticles3D? _sparks;
    private Vector3 _from, _to, _right, _up, _front;
    private TouPowderFxKind _kind;
    private float _age, _duration;
    private int _amount;
    private bool _paused;
    private bool _surfaceStarted;
    private BattlePawn3D? _burnPawn;
    private int _burnGeneration;
    private PowderBurnBurst3D? _burnBurst;
    private double _speed = 1;
    private Action? _onArrival;
    internal float Age => _age;
    internal TouPowderFxKind Kind => _kind;
    internal PowderBurnBurst3D? BurnBurst => _burnBurst;
    internal int? BurnPawnId => _burnPawn?.InstanceId;
    private readonly TaskCompletionSource<bool> _arrival = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task<bool> Arrival => _arrival.Task;
    internal bool UsesPurchasedSparks => _sparks is not null;

    internal void Configure(TouPowderFxKind kind, Vector3 from, Vector3 to, Camera3D camera, int amount, bool purchased)
    {
        _kind = kind; _from = from; _to = to; _amount = amount;
        _right = camera.GlobalBasis.X; _up = camera.GlobalBasis.Y; _front = camera.GlobalBasis.Z;
        _duration = kind switch {
            TouPowderFxKind.Burn => PowderBurnBurst3D.DurationFor(amount), TouPowderFxKind.Thread => .32f,
            TouPowderFxKind.Lift => .18f, TouPowderFxKind.Contagion or TouPowderFxKind.DustFall => .20f,
            TouPowderFxKind.Heavy => .24f, _ => .22f,
        };
        AddChild(new MeshInstance3D {
            Mesh = _mesh, MaterialOverride = new StandardMaterial3D {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            }, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        for (int i = 0; i < _motes.Length; i++)
        {
            _motes[i] = ShockMarkFx.Sprite(this, from, Grain, .12f, ShockMarkFx.Gold);
            if (kind == TouPowderFxKind.DustFall) { _motes[i].NoDepthTest = true; _motes[i].RenderPriority = 2; }
        }
        if (kind is TouPowderFxKind.Burn or TouPowderFxKind.Pop)
        {
            if (kind == TouPowderFxKind.Pop)
                _core = ShockMarkFx.Sprite(this, to + _front * .32f, ShockMarkFx.Halo, 1.25f, ShockMarkFx.Gold);
            _smoke = ShockMarkFx.Sprite(this, to, ShockMarkFx.Powder, .8f, new Color("643952"));
            if (kind == TouPowderFxKind.Burn)
            {
                _burnBurst = new PowderBurnBurst3D(); AddChild(_burnBurst);
                _burnBurst.Configure(camera, amount);
                _label = new Label3D {
                    Text = "粉焼", FontSize = 42, OutlineSize = 12, PixelSize = .007f,
                    Modulate = new Color("ff8791"), OutlineModulate = new Color("351221"),
                    Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
                };
                AddChild(_label);
                _label.GlobalPosition = to + _right * .80f + _up * .58f + _front * .4f;
                if (purchased) AddPurchasedSparks(to);
            }
        }
        Draw();
    }

    internal void AttachBurnBody(BattlePawn3D pawn)
    {
        _burnPawn = pawn; _burnGeneration = pawn.BeginPowderBurnBody();
    }
    internal void OnArrival(Action arrival) => _onArrival = arrival;

    internal static float HeavyWidth(int amount)
        => .030f + .008f * MathF.Sqrt(Math.Clamp(amount / 8f - 1, 0, 16));

    private void AddPurchasedSparks(Vector3 to)
    {
        if (!ResourceLoader.Exists(SparkPath)) return;
        _sparks = GD.Load<PackedScene>(SparkPath).Instantiate<GpuParticles3D>();
        _sparks.SetScript(default);
        var process = (ParticleProcessMaterial)_sparks.ProcessMaterial.Duplicate(true);
        _sparks.ProcessMaterial = process;
        process.Gravity = Vector3.Zero;
        float level = PowderBurnBurst3D.Level(_amount);
        process.InitialVelocityMin = 1.2f + level * .2f; process.InitialVelocityMax = 2.2f + level * .55f;
        process.ScaleMin = .10f; process.ScaleMax = .18f + level * .025f;
        process.Color = Colors.White;
        process.ColorRamp = new GradientTexture1D { Gradient = new Gradient {
            Offsets = new[] { 0f, .3f, 1f },
            Colors = new[] { new Color("ffd1d8"), new Color("ff3045"), new Color("c40f2c00") },
        } };
        _sparks.MaterialOverride = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true, RenderPriority = 2,
        };
        _sparks.Amount = 12 + (int)(level * 4); _sparks.Lifetime = .38 + level * .02; _sparks.OneShot = true;
        _sparks.Emitting = false; _sparks.LocalCoords = true;
        _sparks.UseFixedSeed = true; _sparks.Seed = 325;
        AddChild(_sparks); _sparks.GlobalPosition = to;
        _sparks.Emitting = true; _sparks.Restart();
    }

    internal void SetClock(bool paused, double speed)
    {
        _paused = paused; _speed = Math.Max(.1, speed);
        if (_sparks is not null) _sparks.SpeedScale = paused ? 0 : _speed;
    }

    public override void _Process(double delta)
    {
        if (_paused || IsQueuedForDeletion()) return;
        _age += (float)(delta * _speed);
        float arrivalAt = _kind == TouPowderFxKind.Burn ? .28f : _kind == TouPowderFxKind.Settle ? .10f : _duration;
        if (_age >= arrivalAt && !_arrival.Task.IsCompleted)
        {
            _onArrival?.Invoke(); _onArrival = null;
            _arrival.TrySetResult(true);
        }
        if (_age >= _duration) { ClearBurnBody(); Hide(); QueueFree(); return; }
        Draw();
    }

    private Vector3 Path(float p, int lane)
    {
        Vector3 point = _from.Lerp(_to, p);
        return _kind switch {
            TouPowderFxKind.Thread => point - Vector3.Up * Mathf.Sin(p * Mathf.Pi) * .10f + Vector3.Up * (lane % 3 - 1) * .14f,
            TouPowderFxKind.Lift => point + _right * Mathf.Sin(p * Mathf.Pi) * (lane - 4) * .045f,
            TouPowderFxKind.Contagion => point - _up * Mathf.Sin(p * Mathf.Pi) * .25f
                + _right * Mathf.Sin(p * Mathf.Tau * 3) * .045f,
            TouPowderFxKind.DustFall => point + _up * Mathf.Sin(p * Mathf.Pi) * (.35f + lane % 3 * .10f)
                + _right * Mathf.Sin(p * Mathf.Pi) * (lane % 3 - 1) * .14f,
            _ => point + _right * Mathf.Sin(p * Mathf.Pi) * (lane - 4) * .055f,
        };
    }

    private void Draw()
    {
        float t = Mathf.Clamp(_age / _duration, 0, 1);
        float fade = 1 - t;
        _mesh.ClearSurfaces(); _surfaceStarted = false;
        if (_kind is TouPowderFxKind.Heavy)
        {
            float width = HeavyWidth(_amount);
            for (int i = 0; i < 12; i++)
            {
                Vector3 Point(int k) => _from.Lerp(_to, k / 12f)
                    + (_up * Mathf.Sin(k * 2.17f) + _right * Mathf.Sin(k * 1.37f) * .45f)
                        * (.07f * Mathf.Sin(k * Mathf.Pi / 12));
                Segment(Point(i), Point(i + 1), width * 1.9f, new Color(ThunderFx.Cyan, fade * .25f));
                Segment(Point(i), Point(i + 1), width, new Color(new Color("d9f8ff"), fade));
            }
        }
        else if (_kind == TouPowderFxKind.Pop)
        {
            for (int i = 0; i < 6; i++)
            {
                // 弾けは外へ短い火花。本人の胴を点火する粉焼とは形も分ける。
                float angle = i * Mathf.Tau / 6;
                Vector3 ray = _right * Mathf.Cos(angle) + _up * Mathf.Sin(angle);
                Vector3 a = _to + ray * .10f + _front * .38f;
                Vector3 b = _to + ray * (.25f + t * .28f) + _front * .38f;
                Segment(a, b, .015f + Math.Min(_amount, 10) * .001f,
                    new Color(ShockMarkFx.Gold, Math.Min(1, fade * 2)));
            }
        }
        else if (_kind is TouPowderFxKind.Thread or TouPowderFxKind.Contagion)
        {
            float head = Math.Min(1, t * 1.5f);
            for (int i = 0; i < 18; i++)
            {
                float p = i / 18f;
                // 動く先頭の短い尾だけが光る。普通の放電のように全経路を一斉に光らせない。
                if (p > head || p < head - .28f) continue;
                Segment(Path(p, 1), Path((i + 1) / 18f, 1), .012f,
                    new Color(ShockMarkFx.Gold, fade * .8f));
            }
        }
        if (_surfaceStarted) _mesh.SurfaceEnd();
        for (int i = 0; i < _motes.Length; i++)
        {
            var mote = _motes[i];
            if (_kind is TouPowderFxKind.Pop or TouPowderFxKind.Burn)
            {
                float phase = i * 2.39996f;
                float radius = .08f + t * (.35f + i % 3 * .05f);
                mote.GlobalPosition = _to + _right * Mathf.Cos(phase) * radius + _up * Mathf.Sin(phase) * radius + _front * .4f;
            }
            else
            {
                float p = Mathf.Clamp(t * 1.6f - i * .055f, 0, 1);
                mote.GlobalPosition = Path(p, i) + _front * .32f;
            }
            mote.Visible = _kind is not (TouPowderFxKind.Heavy or TouPowderFxKind.Burn);
            mote.Scale = Vector3.One * (.7f + Math.Min(6, _amount) * .04f);
            if (_kind == TouPowderFxKind.DustFall) mote.Scale *= 1.6f;
            mote.Modulate = new Color(_kind == TouPowderFxKind.Burn ? new Color("ff3045") : ShockMarkFx.Gold,
                _kind == TouPowderFxKind.DustFall ? Math.Min(1, fade * 5) : Math.Min(1, fade * 2));
        }
        if (_core is not null)
        {
            _core.Scale = Vector3.One * (.45f + Math.Min(8, _amount) * .035f + t * .7f);
            _core.Modulate = new Color((_kind == TouPowderFxKind.Burn ? new Color("ff983f") : ShockMarkFx.Gold)
                .Lerp(new Color("ff792f"), t), Mathf.Sin(Math.Min(1, t * 2) * Mathf.Pi) * .65f);
        }
        if (_smoke is not null)
        {
            _smoke.GlobalPosition = _to + _up * t * .45f + _front * .34f;
            _smoke.Scale = Vector3.One * (.6f + t * .55f);
            _smoke.Modulate = new Color(new Color("643952"), Mathf.Sin(t * Mathf.Pi) * .50f);
        }
        if (_label is not null) _label.Modulate = new Color(new Color("ff8791"), Math.Min(1, fade * 4));
        _burnBurst?.Draw(_age, _burnPawn is not null && IsInstanceValid(_burnPawn) && _burnPawn.PresentationAlive
            ? _burnPawn.FxPoint : _to);
        if (_burnPawn is not null && IsInstanceValid(_burnPawn))
            _burnPawn.SetPowderBurnBody(_burnGeneration, Math.Max(0, 1 - t * 2.4f), Mathf.Sin(t * Mathf.Pi));
    }

    private void Segment(Vector3 a, Vector3 b, float width, Color color)
    {
        if (!_surfaceStarted) { _mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles); _surfaceStarted = true; }
        Vector3 side = (b - a).Cross(_front).Normalized() * width;
        if (side.LengthSquared() < .000001f) side = _right * width;
        _mesh.SurfaceSetColor(color);
        foreach (var v in new[] { a - side, a + side, b + side, a - side, b + side, b - side })
            _mesh.SurfaceAddVertex(ToLocal(v));
    }

    private void ClearBurnBody()
    {
        if (_burnPawn is not null && IsInstanceValid(_burnPawn)) _burnPawn.SetPowderBurnBody(_burnGeneration, 0, 0);
    }
    internal void Cancel() { _onArrival = null; ClearBurnBody(); Hide(); _arrival.TrySetResult(false); QueueFree(); }
    public override void _ExitTree() { ClearBurnBody(); _arrival.TrySetResult(false); }
}
