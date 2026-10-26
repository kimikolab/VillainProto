using BattleCore;
using Godot;
using System;

public partial class BattlePawn3D
{
    internal int HagiSouls { get; private set; }
    internal int HagiMarks { get; private set; }
    internal int HagiLine { get; private set; }
    internal int HagiStage { get; private set; }
    internal bool HagiEligible => HagiLine > 0 && Hp > 0 && (long)Hp * 1000 <= (long)MaxHp * HagiLine;
    internal bool HagiPaused;
    internal bool HagiReaped { get; private set; }
    private double _hagiDissolve;
    private HagiAura3D? _hagiAura;
    internal void BeginHagiMotion() => _motion?.Kill();
    internal void SetHagiState(HagiPresentation.State state, Func<(bool, double)> clock)
    {
        HagiSouls = state.Souls; HagiMarks = state.Marks; HagiLine = state.LinePermil; HagiStage = state.Stage;
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
        _portraitMaterial?.SetShaderParameter("hagi_eligible", HagiEligible ? 1f : 0f);
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
    internal void SetHagiMantle(bool visible) => _portraitMaterial.SetShaderParameter("hagi_aura", visible ? 1f : 0f);
    internal Vector2[] HagiBladeContour => CurrentHagiAnchors?.Contour ?? HagiIdleBladeContour ?? (_movementPortrait switch
    {
        "hagi_follow" => new[] { new Vector2(.88f,.45f), new Vector2(.96f,.53f), new Vector2(.985f,.64f), new Vector2(.94f,.77f), new Vector2(.81f,.88f), new Vector2(.62f,.95f) },
        "hagi_step" => new[] { new Vector2(.445f,.159f), new Vector2(.58f,.124f), new Vector2(.77f,.145f), new Vector2(.93f,.23f), new Vector2(.995f,.42f), new Vector2(.96f,.60f), new Vector2(.90f,.70f) },
        _ => new[] { new Vector2(.31f,.065f), new Vector2(.46f,.023f), new Vector2(.64f,.023f), new Vector2(.83f,.075f), new Vector2(.96f,.21f), new Vector2(.98f,.36f), new Vector2(.935f,.49f) }
    });
    internal void ResetHagiVisual()
    {
        CancelHagiMotion();
        HagiSouls = HagiMarks = HagiLine = HagiStage = 0; HagiReaped = false; _hagiDissolve = 0; HagiPaused = false;
        _hagiAura?.Hide(); _hagiAura?.QueueFree(); _hagiAura = null;
        Hud?.SetHagiLine(0);
        _portraitMaterial?.SetShaderParameter("hagi_marks", 0f);
        _portraitMaterial?.SetShaderParameter("hagi_eligible", 0f);
        _portraitMaterial?.SetShaderParameter("hagi_dissolve", 0f);
        _portraitMaterial?.SetShaderParameter("hagi_aura", 0f);
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
    private ShaderMaterial _bladeMaterial = null!;
    private MeshInstance3D _bladeNode = null!;
    private readonly Sprite3D[] _embers = new Sprite3D[6];
    private readonly Vector3[] _edge = new Vector3[97];
    private float _bladeStage;
    internal double Age { get; private set; }
    internal float BladeStage => _bladeStage;
    internal bool BladeVisible => _bladeNode.Visible;
    internal Vector3 BladeEdgeMiddle => _edge[48];
    internal void Start(BattlePawn3D owner, Func<(bool, double)> clock)
    {
        _owner = owner; _clock = clock;
        ProcessPriority = 20;
        _bladeMaterial=new ShaderMaterial { Shader=new Shader { Code=Godot.FileAccess.GetFileAsString(
            "res://assets/fx/hagi_blade_empower.gdshader") }, RenderPriority=8 };
        _bladeNode = new MeshInstance3D { Mesh = _blade, MaterialOverride = _bladeMaterial, ExtraCullMargin = 10,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off }; AddChild(_bladeNode);
        for(int k=0;k<_embers.Length;k++)
        {
            _embers[k]=new Sprite3D { Texture=HagiLight3D.Sparkle, PixelSize=.001f, Shaded=false,
                Billboard=BaseMaterial3D.BillboardModeEnum.Enabled, RenderPriority=9, Visible=false };
            AddChild(_embers[k]);
        }
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
        _owner.SetHagiMantle(Visible && _owner.HagiStage == 3);
        var (paused, speed) = _clock(); if (paused || !Visible) return;
        Age += delta * speed;
        int stage = _owner.HagiStage;
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
            _shards[k].Modulate = new Color(1, stage == 3 ? .18f : .72f, stage == 3 ? .10f : .57f, _alpha[k]);
        }
    }
    private void DrawBlade()
    {
        _blade.ClearSurfaces();
        _bladeNode.Visible=false;
        foreach(var ember in _embers)ember.Hide();
        if (_bladeStage <= .01f || GetViewport().GetCamera3D() is not { } camera) return;
        var points = _owner.HagiBladeContour;
        Vector3 normal = camera.GlobalBasis.Z.Normalized();
        Vector3 center=Vector3.Zero;
        foreach(var point in points)center+=_owner.HagiBladePoint(point)/points.Length;
        const int segments = 96;
        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments * (points.Length - 1);
            int k = Math.Min(points.Length - 2, (int)t); float fraction = t - k;
            Vector2 a = points[Math.Max(0,k-1)], b = points[k], c = points[k+1], d = points[Math.Min(points.Length-1,k+2)];
            Vector2 p = .5f * ((2*b)+(-a+c)*fraction+(2*a-5*b+4*c-d)*fraction*fraction+(-a+3*b-3*c+d)*fraction*fraction*fraction);
            _edge[i]=_owner.HagiBladePoint(p)+normal*.012f;
        }
        // 刃に沿う帯の両端を段ごとに延ばす。炎の揺らぎや独立した浮く刃にはしない。
        Vector3 startTip = (_edge[0] - _edge[1]).Normalized(), endTip = (_edge[segments] - _edge[segments - 1]).Normalized();
        for (int i = 0; i <= segments; i++)
        {
            float u = i / (float)segments;
            _edge[i] += (startTip * Math.Max(0, 1 - u / .12f) + endTip * Math.Max(0, 1 - (1 - u) / .12f)) * (_bladeStage * .12f);
        }
        Vector3 middleTangent=(_edge[49]-_edge[47]).Normalized();
        float sign=normal.Cross(middleTangent).Dot(_edge[48]-center)>=0?1:-1;
        float inner=.018f+_bladeStage*.003f, outer=.045f+_bladeStage*.035f;
        _bladeMaterial.SetShaderParameter("age",(float)Age);
        _bladeMaterial.SetShaderParameter("stage",_bladeStage);
        _blade.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        for(int i=0;i<=segments;i++)
        {
            Vector3 tangent=(_edge[Math.Min(segments,i+1)]-_edge[Math.Max(0,i-1)]).Normalized();
            Vector3 outward=normal.Cross(tangent).Normalized()*sign;
            _blade.SurfaceSetUV(new Vector2(i/(float)segments,-inner));
            _blade.SurfaceAddVertex(ToLocal(_edge[i]-outward*inner));
            _blade.SurfaceSetUV(new Vector2(i/(float)segments,outer));
            _blade.SurfaceAddVertex(ToLocal(_edge[i]+outward*outer));
        }
        _blade.SurfaceEnd();
        _bladeNode.Show();
        for(int k=0;k<_embers.Length;k++)
        {
            if(k>=Mathf.CeilToInt(_bladeStage*2))continue;
            float phase=Mathf.PosMod((float)Age*(.28f+k*.017f)+k*.173f,1);
            int at=Math.Clamp((int)(phase*segments),1,segments-1);
            Vector3 tangent=(_edge[at+1]-_edge[at-1]).Normalized();
            Vector3 outward=normal.Cross(tangent).Normalized()*sign;
            _embers[k].GlobalPosition=_edge[at]+outward*Mathf.Sin(phase*Mathf.Pi)*(.025f+_bladeStage*.012f);
            float size=(.020f+_bladeStage*.007f)/Math.Max(.001f,HagiLight3D.Sparkle.GetWidth()*.001f);
            _embers[k].Scale=Vector3.One*size;
            _embers[k].Modulate=new Color(1,.14f,.045f,Mathf.Sin(phase*Mathf.Pi)*Math.Min(1,_bladeStage)*.68f);
            _embers[k].Show();
        }
    }
}
