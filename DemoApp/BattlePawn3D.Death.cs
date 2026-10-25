using Godot;
using System;

public partial class BattlePawn3D
{
    internal int GraveLayers { get; private set; }
    internal int GraveStage => DeathPresentation.Stage(GraveLayers);
    internal int SoulKnots { get; private set; }
    internal int DirgeSound { get; private set; }
    private DeathAura3D? _deathAura;
    private bool _deathStopped;
    internal bool DeathActive => _alive && !_victory && !_deathStopped;
    internal Sprite3D DeathSprite => _sprite;
    internal void SetDeathState(int layers, int lives, int sound)
    {
        int before = GraveStage;
        GraveLayers = layers; SoulKnots = lives; DirgeSound = sound;
        if (_unitId == "rica" && before != GraveStage && !AttackClipPlaying)
        { ReleaseRicaPose(); RefreshBattlePortrait(); }
    }
    internal void SetSoulMaxHp(int maxHp)
    {
        if (maxHp == MaxHp) return;
        MaxHp = Math.Max(1, maxHp); SetHp(Math.Min(Hp, MaxHp));
    }
    internal void EnsureDeathAura(Func<(bool Paused, double Speed)> clock)
    {
        if (_deathAura is not null) return;
        _deathAura = new DeathAura3D(); AddChild(_deathAura); _deathAura.Start(this, clock);
    }
    internal void StopDeathVisual()
    {
        _deathStopped = true;
        EndRicaApproach();
        ReleaseRicaPose();
        _deathAura?.Hide();
        _portraitMaterial?.SetShaderParameter("grave_heat", 0f);
    }
    internal void SetRicaEyeAnchor(Vector2 eye) => _portraitMaterial?.SetShaderParameter("grave_eye",eye);
    internal void ResumeDeathVisual() => _deathStopped = false;
    internal void ShowWightDecay()
    {
        if (UnitId != "wight") return;
        _portraitMaterial.SetShaderParameter("golm_gray", 0.65f);
        _portraitMaterial.SetShaderParameter("golm_crumble", (1f - Hp / (float)Math.Max(1, MaxHp)) * 0.58f);
    }
    internal void UpdateDeathEyes()
    {
        _portraitMaterial.SetShaderParameter("grave_heat", DeathActive && UnitId == "rica" ? GraveStage / 3f : 0f);
        _portraitMaterial.SetShaderParameter("grave_tint", BattlefieldView3D.GraveColor(GraveLayers));
    }
}

// 胴の周囲の粒は9個まで。9層以後は数を増やさず焔の高さと明滅だけを強める。
public partial class DeathAura3D : Node3D
{
    private BattlePawn3D _pawn = null!;
    private Func<(bool Paused, double Speed)> _clock = null!;
    private Sprite3D[] _lights = Array.Empty<Sprite3D>();
    private MeshInstance3D[] _flames = Array.Empty<MeshInstance3D>();
    private ShaderMaterial[] _flameMaterials = Array.Empty<ShaderMaterial>();
    private double _age;
    internal double Age => _age;
    internal void Start(BattlePawn3D pawn, Func<(bool Paused, double Speed)> clock)
    {
        _pawn = pawn; _clock = clock;
        // 購入の sparkles シーンは自動反復するため、原本の光粒画像だけを流用する。
        const string purchased = "res://PolyBlocks/EffectBlocks/source_files/textures/sparkle.png";
        Texture2D texture = ResourceLoader.Exists(purchased) ? GD.Load<Texture2D>(purchased)
            : GD.Load<Texture2D>("res://assets/fx/death_light.svg");
        _lights = new Sprite3D[15];
        for (int k = 0; k < _lights.Length; k++)
        {
            var sprite = new Sprite3D { Texture = texture, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Shaded = false, NoDepthTest = false, PixelSize = 0.16f / texture.GetWidth() };
            _lights[k] = sprite; AddChild(sprite);
        }
        if (pawn.UnitId == "rica")
        {
            var shader = new Shader { Code = @"
shader_type spatial;
render_mode unshaded,blend_add,cull_disabled,depth_draw_never;
uniform float phase=0.0;
uniform vec4 tint : source_color;
uniform float heat=1.0;
void vertex(){MODELVIEW_MATRIX=VIEW_MATRIX*mat4(INV_VIEW_MATRIX[0]*length(MODEL_MATRIX[0].xyz),INV_VIEW_MATRIX[1]*length(MODEL_MATRIX[1].xyz),INV_VIEW_MATRIX[2],MODEL_MATRIX[3]);}
void fragment(){
 vec2 p=vec2((UV.x-0.5)*2.0,1.0-UV.y);
 float curl=sin(p.y*11.0-phase*4.0)*0.12+sin(p.y*23.0+phase*3.0)*0.05;
 float width=pow(max(0.0,1.0-p.y),1.4)*0.52;
 float flame=1.0-smoothstep(width*0.3,width+0.06,abs(p.x-curl*p.y));
 float veil=exp(-abs(p.x-curl)*7.0)*(1.0-p.y)*0.25;
 float fade=smoothstep(0.0,0.12,p.y)*(1.0-smoothstep(0.83,1.0,p.y));
 ALBEDO=mix(tint.rgb,vec3(0.88,0.92,1.0),flame*0.38);
 EMISSION=tint.rgb*(flame+veil)*heat;
 ALPHA=(flame*0.52+veil)*fade;
}" };
            _flames = new MeshInstance3D[4]; _flameMaterials = new ShaderMaterial[4];
            for (int k = 0; k < 4; k++)
            {
                var mat = new ShaderMaterial { Shader = shader };
                _flameMaterials[k] = mat;
                var flame = new MeshInstance3D { Mesh = new QuadMesh { Size = new Vector2(0.48f, 1.25f) },
                    MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
                _flames[k] = flame; AddChild(flame);
            }
        }
    }
    public override void _Process(double delta)
    {
        var (paused, speed) = _clock(); if (!paused) _age += delta * speed;
        Visible = _pawn.DeathActive;
        _pawn.UpdateDeathEyes();
        if (!Visible) return;
        int layers = _pawn.GraveLayers, stage = _pawn.GraveStage;
        float intensity = Math.Clamp((layers - 9) / 12f, 0, 1);
        for (int k = 0; k < _flames.Length; k++)
        {
            var flame = _flames[k]; flame.Visible = stage >= 2 && (stage >= 3 || k < 2);
            float sign = k % 2 == 0 ? -1 : 1;
            flame.Position = new Vector3(sign * (0.42f + k / 2 * 0.16f), 0.73f + k / 2 * 0.15f, 0.04f);
            flame.Scale = new Vector3(stage == 3 ? 1.2f : 0.8f, (stage == 3 ? 1.10f : 0.72f) + intensity * 0.40f, 1);
            _flameMaterials[k].SetShaderParameter("phase", (float)_age + k * 1.3f);
            _flameMaterials[k].SetShaderParameter("heat", 0.6f + stage * 0.2f + intensity * 0.8f);
            _flameMaterials[k].SetShaderParameter("tint", BattlefieldView3D.GraveColor(layers));
        }
        for (int k = 0; k < _lights.Length; k++)
        {
            var light = _lights[k];
            int count = _pawn.UnitId == "rica" ? Math.Min(9, layers)
                : _pawn.UnitId == "ere" ? Math.Min(9, (_pawn.DirgeSound + 11) / 12) : 0;
            bool knot = k >= 12 && k - 12 < _pawn.SoulKnots;
            bool eyes = _pawn.UnitId == "wight" && k is 10 or 11;
            light.Visible = k < count || knot || eyes;
            if (!light.Visible) continue;
            float a = (float)_age * (0.7f + stage * 0.24f) + k * Mathf.Tau / Math.Max(1, count);
            light.Position = knot ? new Vector3((k - 13) * 0.18f, 1.38f, 0.12f)
                : eyes ? new Vector3((k - 10.5f) * 0.13f, 1.64f, 0.05f)
                : new Vector3(Mathf.Cos(a) * (0.38f + stage * 0.10f),
                    0.35f + k % 3 * 0.34f + Mathf.Sin(a * 1.7f) * 0.17f, Mathf.Sin(a) * 0.35f);
            float pulse = 0.8f + 0.2f * Mathf.Sin(a * 2.2f);
            light.Modulate = knot ? new Color("e8f6ff") : eyes ? new Color("b4dbea")
                : _pawn.UnitId == "ere" ? new Color("f5d684") : BattlefieldView3D.GraveColor(layers);
            light.Scale = eyes ? Vector3.One * 0.5f : knot ? Vector3.One * pulse
                : new Vector3(1.0f + stage * 0.25f, (stage == 3 ? 3.2f + intensity * 2.2f : 1.3f) * pulse, 1);
        }
    }
}
