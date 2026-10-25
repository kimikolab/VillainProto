using BattleCore;
using Godot;
using System;

public partial class BattlePawn3D
{
    internal int HagiSouls { get; private set; }
    internal int HagiMarks { get; private set; }
    internal int HagiLine { get; private set; }
    internal bool HagiPaused;
    internal bool HagiReaped { get; private set; }
    private double _hagiDissolve;
    private HagiAura3D? _hagiAura;
    internal void BeginHagiMotion() => _motion?.Kill();
    internal void SetHagiState(HagiPresentation.State state, Func<(bool, double)> clock)
    {
        HagiSouls = state.Souls; HagiMarks = state.Marks; HagiLine = state.LinePermil;
        Hud.SetHagiLine(state.LinePermil);
        _portraitMaterial.SetShaderParameter("hagi_marks", (float)state.Marks);
        RefreshHagiEligibility();
        if (_unitId == "hagi" && _hagiAura is null)
        {
            _hagiAura = new HagiAura3D(); AddChild(_hagiAura); _hagiAura.Start(this, clock);
        }
    }
    private void RefreshHagiEligibility()
    {
        _portraitMaterial?.SetShaderParameter("hagi_eligible", HagiLine > 0 && Hp > 0 && (long)Hp * 1000 <= (long)MaxHp * HagiLine ? 1f : 0f);
    }
    internal void SetHagiPose(string? pose, bool? flip = null)
    {
        if (_unitId != "hagi" || !_alive || _victory) return;
        _movementPortraitTween?.Kill(); _movementPortraitTween = null;
        _movementPortrait = pose; _movementFlip = flip; RefreshBattlePortrait();
    }
    internal void BeginHagiDissolve()
    {
        HagiReaped = true; _hagiDissolve = .001;
        _portraitMaterial.SetShaderParameter("hagi_eligible", 0f);
        Hud.Retire(false);
    }
    internal void ProcessHagiVisual(float delta)
    {
        if (HagiPaused) return;
        if (_hagiDissolve > 0)
        {
            _hagiDissolve = Math.Min(1, _hagiDissolve + delta / .34);
            _portraitMaterial.SetShaderParameter("hagi_dissolve", (float)_hagiDissolve);
        }
    }
    internal bool HagiActive => _alive && !_victory;
    internal Vector2 HagiBladeAnchor => _movementPortrait switch
    {
        "hagi_follow" => new(.94f,.67f), "hagi_step" => new(.95f,.54f), _ => new(.84f,.14f)
    };
    internal Vector2[] HagiBladeContour => _movementPortrait switch
    {
        "hagi_follow" => new[] { new Vector2(.88f,.45f), new Vector2(.96f,.53f), new Vector2(.985f,.64f), new Vector2(.94f,.77f), new Vector2(.81f,.88f), new Vector2(.62f,.95f) },
        "hagi_step" => new[] { new Vector2(.445f,.159f), new Vector2(.58f,.124f), new Vector2(.77f,.145f), new Vector2(.93f,.23f), new Vector2(.995f,.42f), new Vector2(.96f,.60f), new Vector2(.90f,.70f) },
        _ => new[] { new Vector2(.31f,.065f), new Vector2(.46f,.023f), new Vector2(.64f,.023f), new Vector2(.83f,.075f), new Vector2(.96f,.21f), new Vector2(.98f,.36f), new Vector2(.935f,.49f) }
    };
    internal void ResetHagiVisual()
    {
        HagiSouls = HagiMarks = HagiLine = 0; HagiReaped = false; _hagiDissolve = 0; HagiPaused = false;
        _hagiAura?.Hide(); _hagiAura?.QueueFree(); _hagiAura = null;
        Hud?.SetHagiLine(0);
        _portraitMaterial?.SetShaderParameter("hagi_marks", 0f);
        _portraitMaterial?.SetShaderParameter("hagi_eligible", 0f);
        _portraitMaterial?.SetShaderParameter("hagi_dissolve", 0f);
        SetHagiPose(null);
    }
}

// 減衰でも粒を急に消さず、停止・倍速に従う時計で1つずつ薄れる。
public partial class HagiAura3D : Node3D
{
    private BattlePawn3D _owner = null!;
    private Func<(bool Paused, double Speed)> _clock = () => (false, 1);
    private readonly Sprite3D[] _shards = new Sprite3D[9];
    private readonly float[] _alpha = new float[9];
    private readonly ImmediateMesh _blade = new();
    private MeshInstance3D _bladeNode = null!;
    private float _bladeStage;
    internal double Age { get; private set; }
    internal void Start(BattlePawn3D owner, Func<(bool, double)> clock)
    {
        _owner = owner; _clock = clock;
        ProcessPriority = 20;
        var material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = new Color(.91f,.94f,1,.82f), EmissionEnabled = true, Emission = new Color(.93f,.95f,1),
            EmissionEnergyMultiplier = 1.5f, NoDepthTest = true };
        _bladeNode = new MeshInstance3D { Mesh = _blade, MaterialOverride = material, ExtraCullMargin = 10,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; AddChild(_bladeNode);
        for (int k = 0; k < _shards.Length; k++)
        {
            _shards[k] = new Sprite3D { Texture = HagiLight3D.Sparkle, PixelSize = .001f, Shaded = false,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true, RenderPriority = 10 };
            AddChild(_shards[k]);
        }
    }
    public override void _Process(double delta)
    {
        Visible = _owner.HagiActive;
        var (paused, speed) = _clock(); if (paused || !Visible) return;
        Age += delta * speed;
        int stage = HagiDepthTrait.Stage(_owner.HagiSouls);
        _bladeStage = Mathf.MoveToward(_bladeStage, stage, (float)(delta * speed * 7));
        DrawBlade();
        for (int k = 0; k < 9; k++)
        {
            _alpha[k] = Mathf.MoveToward(_alpha[k], k < _owner.HagiSouls ? .65f + stage * .10f : 0, (float)(delta * speed * 3));
            float angle = (float)Age * .8f + k * Mathf.Tau / 9;
            _shards[k].GlobalPosition = _owner.FxPoint + new Vector3(Mathf.Cos(angle) * (.55f + stage * .09f),
                Mathf.Sin(angle * 1.6f + k) * .28f - .10f, Mathf.Sin(angle) * .24f);
            float size = (.10f + stage * .028f) / Math.Max(.001f, HagiLight3D.Sparkle.GetWidth() * .001f);
            _shards[k].Scale = new Vector3(size * .65f, size * 1.5f, 1);
            _shards[k].Modulate = new Color(.94f, .96f, 1, _alpha[k]);
        }
    }
    private void DrawBlade()
    {
        _blade.ClearSurfaces();
        if (_bladeStage <= .01f || GetViewport().GetCamera3D() is not { } camera) return;
        var points = _owner.HagiBladeContour;
        var size = _owner.DeathSprite.Texture.GetSize();
        Vector2 anchor = _owner.HagiBladeAnchor;
        Vector3 normal = camera.GlobalBasis.Z;
        _blade.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        const int segments = 64;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments * (points.Length - 1);
            int k = Math.Min(points.Length - 2, (int)t); float fraction = t - k;
            Vector2 a = points[Math.Max(0,k-1)], b = points[k], c = points[k+1], d = points[Math.Min(points.Length-1,k+2)];
            Vector2 p = .5f * ((2*b)+(-a+c)*fraction+(2*a-5*b+4*c-d)*fraction*fraction+(-a+3*b-3*c+d)*fraction*fraction*fraction);
            p = anchor + (p-anchor)*(1+_bladeStage*.09f);
            Vector3 at = ToLocal(_owner.MovementPortraitPoint(p*size,camera)+normal*.012f);
            Vector2 tangent = (c-b).Normalized();
            Vector3 offset = (camera.GlobalBasis.X * -tangent.Y + camera.GlobalBasis.Y * -tangent.X)
                * (.006f+_bladeStage*.003f)*Mathf.Sin(i/(float)segments*Mathf.Pi);
            _blade.SurfaceAddVertex(at-offset); _blade.SurfaceAddVertex(at+offset);
        }
        _blade.SurfaceEnd();
    }
}
