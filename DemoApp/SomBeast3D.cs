using Godot;
using System;

// 台本の召喚体の絵。手番は細身の雷人形、緊急の割り込みは跳ねる雷精。
public partial class SomBeast3D : Node3D
{
    private readonly ImmediateMesh _glowMesh = new(), _arcMesh = new();
    private StandardMaterial3D _glowMaterial = null!, _arcMaterial = null!;
    private Sprite3D _figure = null!, _core = null!, _contract = null!;
    private ShaderMaterial _figureMaterial = null!;
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
    internal Texture2D FigureTexture => _figure.Texture;
    internal bool FigureFlipped => _figure.FlipH;
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
        _radius = emergency ? .43f : .34f; _height = emergency ? .85f : .98f;
        GlobalPosition = origin;
        _glowMaterial = Layer(_glowMesh, .23f); _arcMaterial = Layer(_arcMesh, .86f);
        var texture = SomSpiritArt.Texture(emergency);
        float figureHeight = emergency ? 1.40f : 1.72f;
        _figure = ShockMarkFx.Sprite(this, Point, texture,
            figureHeight * texture.GetWidth() / texture.GetHeight(), Colors.White);
        _figureMaterial = SomSpiritArt.Material(texture, emergency);
        _figure.MaterialOverride = _figureMaterial;
        _core = ShockMarkFx.Sprite(this, Point, SomFx.SpiritCore, .09f, Colors.White);
        _contract = ShockMarkFx.Sprite(this, Point, SomFx.Circle, .24f, Colors.White);
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
            // 雷人形は静かに滑り、緊急の雷精は膝を畳んだ姿で高く跳ねる。
            GlobalPosition = _origin.Lerp(_destination, Progress)
                + Vector3.Up * MathF.Sin(Progress * MathF.PI) * (_emergency ? .68f : .20f);
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
        float reveal = Math.Clamp(age / (_emergency ? .11f : .18f), 0, 1);
        float floatHeight = MathF.Sin(age * (_emergency ? 9 : 3.5f)) * (_emergency ? .045f : .023f);
        Vector3 center = Point + up * floatHeight;
        Color color = EnergyColor;
        _glowMaterial.AlbedoColor = new Color(color, .20f); _glowMaterial.Emission = color;
        _arcMaterial.AlbedoColor = new Color(Colors.White.Lerp(color, .40f), .76f);
        _arcMaterial.Emission = color;
        _figure.GlobalPosition = center;
        _figure.FlipH = _direction < 0;
        float tilt = _flying ? MathF.Sin(Progress * MathF.PI) * (_emergency ? .23f : .08f) : MathF.Sin(age * 3) * .025f;
        _figure.Rotation = new Vector3(0, 0, -_direction * tilt);
        _figure.Scale = Vector3.One * (_body * (.90f + reveal * .10f) * (1 + Charge * .05f));
        _figureMaterial.SetShaderParameter("clock", age);
        _figureMaterial.SetShaderParameter("reveal", reveal);
        _figureMaterial.SetShaderParameter("rebel", _betrayed ? 1f : 0f);
        _figureMaterial.SetShaderParameter("charge", Charge);
        Vector3 chest = center + up * (_emergency ? -.12f : .20f) * _body;
        _core.GlobalPosition = chest + front * .018f;
        _core.Scale = Vector3.One * (_body * (1 + Charge * .55f + MathF.Sin(age * 17) * .05f));
        _core.Modulate = new Color(Colors.White.Lerp(color, .16f), reveal * .75f);
        _contract.GlobalPosition = chest + front * .07f;
        _contract.Scale = Vector3.One * (_body * (_betrayed ? 1.10f : 1));
        _contract.Modulate = new Color(_betrayed ? SomFx.SpiritRed : SomFx.Violet, reveal * (_betrayed ? .90f : .78f));
        _contract.Rotation = new Vector3(0, 0, MathF.Sin(age * 3) * .07f);

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
            // 円形の光輪ではなく、電気の肩・腕・脚をつなぐ細い稲妻を重ねる。
            // 顔を横切らず、身体の穴が見える密度に抑える。
            Vector2[][] limbs = _emergency ? new[] {
                new[] { new Vector2(.02f, .13f), new Vector2(.37f, .05f), new Vector2(.70f, .25f) },
                new[] { new Vector2(-.06f, -.18f), new Vector2(-.40f, -.36f), new Vector2(-.20f, -.83f) },
                new[] { new Vector2(.12f, -.22f), new Vector2(.45f, -.44f), new Vector2(.25f, -.90f) },
            } : new[] {
                new[] { new Vector2(.12f, .31f), new Vector2(.43f, -.01f), new Vector2(.67f, -.38f) },
                new[] { new Vector2(.08f, -.20f), new Vector2(.24f, -.61f), new Vector2(.12f, -1.02f) },
                new[] { new Vector2(-.08f, .16f), new Vector2(.03f, -.04f), new Vector2(-.04f, -.35f) },
            };
            for (int limb = 0; limb < limbs.Length; limb++)
            {
                for (int segment = 0; segment < 2; segment++)
                {
                    Vector3 previous = Vector3.Zero;
                    for (int n = 0; n <= 5; n++)
                    {
                        Vector2 p = limbs[limb][segment].Lerp(limbs[limb][segment + 1], n / 5f);
                        float jitter = n is 0 or 5 ? 0 : MathF.Sin(n * 2.7f + limb * 5 + MathF.Floor(age * 24)) * (.022f + Charge * .016f);
                        Vector3 point = center + front * .035f + right * ((p.X + jitter) * _direction * radius)
                            + up * p.Y * radius * 1.8f;
                        if (n > 0) Line(previous, point, reveal * (.48f + Charge * .28f));
                        previous = point;
                    }
                }
            }
            // 帯電時の枝は胴体から外へ。生物の脚を描き足さない。
            for (int k = 0; k < 4; k++)
            {
                float flicker = MathF.Sin(age * (7 + k) + k * 1.8f);
                if (flicker < .15f || Charge == 0) continue;
                Vector3 a = center + right * ((k % 2 == 0 ? -1 : 1) * radius * .55f)
                    + up * ((k < 2 ? -.35f : .25f) * radius);
                Vector3 b = a + right * ((k % 2 == 0 ? -.35f : .35f) * radius) + up * (-radius * .35f);
                Vector3 c = b + right * ((k % 2 == 0 ? .18f : -.18f) * radius) + up * (-radius * .20f);
                Line(a, b, flicker * Charge * .65f); Line(b, c, flicker * Charge * .5f);
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
