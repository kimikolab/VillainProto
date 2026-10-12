using Godot;
using System;

// 台本の「背いた獣」の絵。身体を持たない雷の精霊として光核と電弧だけを描く。
public partial class SomBeast3D : Node3D
{
    private readonly ImmediateMesh _glowMesh = new(), _arcMesh = new();
    private StandardMaterial3D _glowMaterial = null!, _arcMaterial = null!;
    private Sprite3D _core = null!, _halo = null!, _contract = null!, _eye = null!;
    private GpuParticles3D? _sparks;
    private Vector3 _origin, _destination;
    private float _radius, _height, _direction;
    private bool _emergency, _flip, _flying, _shortStance, _betrayed;
    private double _flightAge, _stanceAge, _age, _throwAge = -1;
    private float _body = 1;
    internal double Speed = 1;
    internal bool Paused;
    internal bool InStance { get; private set; }
    internal bool Emergency => _emergency;
    internal bool FacingSummoner => InStance && _betrayed;
    internal bool ContractCracked => _betrayed;
    internal Color EnergyColor => _betrayed ? SomFx.SpiritRebel : SomFx.Violet;
    internal float BodyScale => _body;
    internal double VisualAge => _age;
    internal float Charge { get; private set; }
    internal float Progress { get; private set; }
    internal double StanceSeconds => _shortStance ? SomFx.BeastShortStanceSeconds : SomFx.BeastStanceSeconds;
    internal bool UsesPurchasedCharge => _sparks is not null;
    internal bool UsePurchasedCharge { get; set; } = true;
    internal Vector3 Point => GlobalPosition + Vector3.Up * _height;
    internal Vector3 DestinationPoint => _destination + Vector3.Up * _height;

    internal void Configure(Vector3 origin, Vector3 destination, bool emergency, bool flip, double speed)
    {
        _origin = origin; _destination = destination; _emergency = emergency; _flip = flip;
        Speed = Math.Max(.1, speed); _direction = flip ? -1 : 1;
        _radius = emergency ? .51f : .30f; _height = emergency ? .85f : .45f;
        GlobalPosition = origin;
        _glowMaterial = Layer(_glowMesh, .23f); _arcMaterial = Layer(_arcMesh, .86f);
        _halo = ShockMarkFx.Sprite(this, Point, ShockMarkFx.Halo, _radius * 2.8f, SomFx.Violet);
        _core = ShockMarkFx.Sprite(this, Point, SomFx.SpiritCore, _radius * 1.05f, Colors.White);
        _contract = ShockMarkFx.Sprite(this, Point, SomFx.Circle, _radius * 1.05f, Colors.White);
        _eye = ShockMarkFx.Sprite(this, Point, ShockMarkFx.Halo, _radius * .14f, Colors.White);
        Draw();
    }
    private StandardMaterial3D Layer(ImmediateMesh mesh, float alpha)
    {
        var material = new StandardMaterial3D {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = new Color(SomFx.Violet, alpha), VertexColorUseAsAlbedo = true,
            EmissionEnabled = true, Emission = SomFx.Violet, EmissionEnergyMultiplier = 1.15f,
        };
        AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        return material;
    }
    internal void SetClock(bool paused, double speed)
    { Paused = paused; Speed = Math.Max(.1, speed); if (_sparks is not null) _sparks.SpeedScale = paused ? 0 : Speed; }
    internal void Run() { _flying = true; _flightAge = 0; }
    internal void Arrive() { GlobalPosition = _destination; Progress = 1; _flying = false; }
    internal void Throw() { _throwAge = 0; }
    internal void TakeStance(bool shortened)
    {
        if (InStance) return;
        Arrive(); InStance = true; _shortStance = shortened; _stanceAge = 0; Charge = 0;
    }
    private void Betray()
    {
        _betrayed = true; _direction = _flip ? 1 : -1;
        _contract.Texture = SomFx.ContractCracked;
    }
    private void BeginCharge()
    {
        const string path = "res://PolyBlocks/EffectBlocks/assets/energy/electric_sparks_1.tscn";
        if (!UsePurchasedCharge || !ResourceLoader.Exists(path)) return;
        _sparks = GD.Load<PackedScene>(path).Instantiate<GpuParticles3D>(); _sparks.SetScript(default);
        var material = (ParticleProcessMaterial)_sparks.ProcessMaterial.Duplicate(true);
        _sparks.ProcessMaterial = material; material.Color = SomFx.SpiritRebel;
        material.InitialVelocityMin = .3f; material.InitialVelocityMax = .65f;
        material.ScaleMin = .025f; material.ScaleMax = .05f;
        material.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere;
        material.EmissionSphereRadius = _radius * .65f;
        _sparks.Amount = _emergency ? 8 : 6; _sparks.Lifetime = .18; _sparks.Preprocess = 0;
        _sparks.LocalCoords = true; _sparks.SpeedScale = Speed; _sparks.Emitting = false;
        AddChild(_sparks); _sparks.Position = Vector3.Up * _height;
        _sparks.Emitting = true; _sparks.Restart();
    }
    public override void _Process(double delta)
    {
        if (_sparks is not null) _sparks.SpeedScale = Paused ? 0 : Speed;
        if (Paused) return;
        double step = delta * Speed; _age += step;
        if (_flying)
        {
            _flightAge += step;
            Progress = Math.Clamp((float)(_flightAge / SomFx.BeastRunSeconds), 0, 1);
            // 足踏みはしない。光塊が一つの放物線で敵陣へ滑る。
            GlobalPosition = _origin.Lerp(_destination, Progress)
                + Vector3.Up * MathF.Sin(Progress * MathF.PI) * (_emergency ? .48f : .28f);
            if (Progress >= 1) Arrive();
        }
        if (InStance)
        {
            _stanceAge += step;
            double turn = _shortStance ? .05 : .10, threat = _shortStance ? .03 : .08;
            if (_stanceAge >= turn * .5 && !_betrayed) Betray();
            if (_stanceAge >= turn + threat)
            {
                if (_sparks is null && Charge == 0) BeginCharge();
                Charge = Math.Clamp((float)((_stanceAge - turn - threat) / (StanceSeconds - turn - threat)), 0, 1);
            }
        }
        if (_throwAge >= 0)
        {
            _throwAge += step;
            // 放出した雷は自身の一部。射出の前半で核と輪郭が25%縮み、そのまま残る。
            _body = 1 - .25f * Math.Clamp((float)(_throwAge / (SomFx.ThrowSeconds * .55)), 0, 1);
            if (_throwAge >= SomFx.ThrowSeconds) _throwAge = -1;
        }
        Draw();
    }
    private void Draw()
    {
        var camera = GetViewport().GetCamera3D();
        Vector3 right = camera?.GlobalBasis.X ?? Vector3.Right, up = camera?.GlobalBasis.Y ?? Vector3.Up;
        Vector3 front = camera?.GlobalBasis.Z ?? Vector3.Back;
        float age = (float)_age, radius = _radius * _body * (1 + Charge * .22f);
        Vector3 center = Point + up * (MathF.Sin(age * 5) * .025f);
        Color color = EnergyColor;
        _glowMaterial.AlbedoColor = new Color(color, .20f); _glowMaterial.Emission = color;
        _arcMaterial.AlbedoColor = new Color(Colors.White.Lerp(color, .40f), .76f);
        _arcMaterial.Emission = color;
        _halo.GlobalPosition = center; _halo.Scale = Vector3.One * (_body * (1 + Charge * .18f));
        _halo.Modulate = new Color(color, .15f + Charge * .07f);
        _core.GlobalPosition = center + front * .012f;
        _core.Scale = Vector3.One * (_body * (1 + Charge * .42f + MathF.Sin(age * 17) * .05f));
        _core.Modulate = new Color(Colors.White.Lerp(color, .16f), .88f);
        _contract.GlobalPosition = center + right * (_direction * radius * .32f) + front * .08f;
        _contract.Scale = Vector3.One * (_body * (_betrayed ? 1.10f : 1));
        _contract.Modulate = new Color(_betrayed ? SomFx.SpiritRed : SomFx.Violet, _betrayed ? .90f : .78f);
        _contract.Rotation = new Vector3(0, 0, MathF.Sin(age * 3) * .07f);
        _eye.GlobalPosition = center + right * (_direction * radius * .80f) + up * radius * .15f + front * .06f;
        _eye.Modulate = new Color(Colors.White, .85f); // 向きを示す光点1つ。顔・口・牙を作らない。

        foreach (var mesh in new[] { _glowMesh, _arcMesh })
        {
            mesh.ClearSurfaces(); mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            float width = (mesh == _glowMesh ? .018f : .006f) * (1 + Charge * .35f) * (_emergency ? 1.4f : 1);
            void Line(Vector3 a, Vector3 b, float alpha)
            {
                Vector3 side = (b - a).Cross(front).Normalized() * width;
                mesh.SurfaceSetColor(new Color(1, 1, 1, alpha));
                foreach (var p in new[] { a - side, a + side, b + side, a - side, b + side, b - side }) mesh.SurfaceAddVertex(ToLocal(p));
            }
            // 閉じた動物の輪郭にしない。切れ目と位相の違う弧が入れ替わり続ける。
            for (int arc = 0; arc < 4 + (int)(Charge * 2); arc++)
            {
                float start = age * (.7f + arc * .08f) + arc * 2.51f;
                Vector3 previous = Vector3.Zero;
                for (int n = 0; n < 15; n++)
                {
                    float angle = start + n * .070f;
                    float jitter = MathF.Sin(n * 1.35f + arc * 5 + MathF.Floor(age * 24)) * (.06f + Charge * .07f);
                    float r = radius * (.76f + arc % 3 * .16f + jitter);
                    Vector3 p = center + right * (MathF.Cos(angle) * r * 1.12f)
                        + up * (MathF.Sin(angle) * r * .78f);
                    if (n > 0) Line(previous, p, .52f + .38f * MathF.Sin(n / 14f * MathF.PI));
                    previous = p;
                }
            }
            // 四足の気配は一瞬の放電の枝だけ。接地する脚・耳・毛並みは持たない。
            for (int k = 0; k < 4; k++)
            {
                float flicker = MathF.Sin(age * (7 + k) + k * 1.8f);
                if (flicker < .15f) continue;
                Vector3 a = center + right * ((k % 2 == 0 ? -1 : 1) * radius * .55f)
                    + up * ((k < 2 ? -.35f : .25f) * radius);
                Vector3 b = a + right * ((k % 2 == 0 ? -.35f : .35f) * radius) + up * (-radius * .35f);
                Vector3 c = b + right * ((k % 2 == 0 ? .18f : -.18f) * radius) + up * (-radius * .20f);
                Line(a, b, flicker * .65f); Line(b, c, flicker * .5f);
            }
            if (_flying)
            {
                Vector3 direction = (_destination - _origin).Normalized();
                Vector3 tail = center - direction * (_emergency ? .9f : .6f);
                Line(center - direction * radius, tail + up * .07f, .55f);
                Line(tail + up * .07f, tail - direction * .18f, .18f);
            }
            mesh.SurfaceEnd();
        }
    }
}
