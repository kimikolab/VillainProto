using BattleCore;
using Godot;
using System;

public partial class BattlePawn3D : Node3D
{
    private const float PortraitGroundY = 0.05f;

    private Sprite3D _sprite = null!;
    private ShaderMaterial _portraitMaterial = null!;
    private MeshInstance3D _shadow = null!;
    private MeshInstance3D _ring = null!;
    private MeshInstance3D _hpBack = null!;
    private MeshInstance3D _hpFill = null!;
    private QuadMesh _hpFillMesh = null!;
    private Label3D _name = null!;
    private Label3D _seat = null!;
    private Label3D _stats = null!;
    private Label3D _status = null!;
    private Vector3 _home;
    private Color _baseTint;
    private Texture2D _atlas = null!;
    private string _unitId = "";
    private float _phase;
    private float _portraitHeight = 2.25f;
    private float _portraitBaseY = 1.12f;
    private float _portraitGroundDistance = 1.07f;
    private float _fxHeight = 1.35f;
    private bool _alive = true;
    private bool _victory;

    public int InstanceId { get; private set; }
    public int Team { get; private set; }
    /// <summary>
    /// 今いる席。<b>移動（逃亡・後退・突き返し）で戦闘中に変わる</b>ので、
    /// 席名の札（第123期）は setter から張り替える。
    /// </summary>
    public int Slot
    {
        get => _slot;
        set
        {
            _slot = value;
            if (_seat is not null) _seat.Text = UiKit.SeatLabel(_slot);
        }
    }

    private int _slot;
    public int Hp { get; private set; }
    public int MaxHp { get; private set; }
    public int AttackValue { get; private set; }
    public AttackPattern Pattern { get; private set; }
    public string UnitName { get; private set; } = "";
    public Vector3 Home => _home;
    public Vector3 FxPoint => GlobalPosition + new Vector3(0, _fxHeight, 0);

    public void Configure(DemoOpening opening, Texture2D atlas)
    {
        _atlas = atlas;
        _unitId = opening.UnitId;
        InstanceId = opening.InstanceId;
        Team = opening.Team;
        Slot = opening.Slot;
        Hp = opening.Hp;
        MaxHp = Math.Max(1, opening.MaxHp);
        AttackValue = opening.Attack;
        Pattern = opening.Pattern;
        UnitName = opening.Name;
        _phase = (UiKit.StableHash(opening.UnitId) % 1000) * 0.0061f;
        _baseTint = UiKit.PortraitTint(opening.UnitId, opening.Team == BattleContext.EnemyTeam)
            .Lerp(Colors.White, 0.42f);

        _shadow = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.82f, BottomRadius = 0.82f, Height = 0.018f, RadialSegments = 28 },
            Position = new Vector3(0, 0.018f, 0),
            MaterialOverride = MakeMaterial(new Color(0.01f, 0.015f, 0.012f, 0.52f), true),
        };
        AddChild(_shadow);

        Color teamColor = Team == BattleContext.PlayerTeam ? UiKit.Player : UiKit.Enemy;
        _ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.70f, OuterRadius = 0.84f, Rings = 28, RingSegments = 8 },
            Position = new Vector3(0, 0.055f, 0),
            MaterialOverride = MakeMaterial(new Color(teamColor, 0.78f), true, teamColor * 0.65f),
        };
        AddChild(_ring);

        Texture2D portrait = UiKit.BattlePortrait(atlas, opening.UnitId);
        bool hasCustomPortrait = UiKit.HasCustomBattlePortrait(opening.UnitId)
            || UiKit.HasCustomPortrait(opening.UnitId);
        _portraitHeight = UiKit.PortraitWorldHeight(opening.UnitId);
        float bottomPadding = UiKit.HasCustomBattlePortrait(opening.UnitId)
            ? UiKit.BattlePortraitBottomPaddingRatio(opening.UnitId)
            : 0.0f;
        SetPortraitGeometry(portrait, bottomPadding);
        _fxHeight = Math.Clamp(_portraitHeight * 0.58f, 0.90f, 1.65f);
        float seatY = hasCustomPortrait ? _portraitHeight + 0.06f : 1.88f;
        float statsY = hasCustomPortrait ? seatY + 0.17f : 2.05f;
        float hpY = hasCustomPortrait ? seatY + 0.34f : 2.22f;
        float nameY = hasCustomPortrait ? seatY + 0.61f : 2.49f;
        float statusY = hasCustomPortrait ? seatY + 0.88f : 2.76f;
        var shader = new Shader
        {
            Code = @"shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_prepass_alpha;
uniform sampler2D portrait_texture : source_color, filter_linear_mipmap;
uniform vec4 portrait_tint : source_color = vec4(1.0);
void fragment() {
    vec4 c = texture(portrait_texture, UV);
    vec4 top_left = texture(portrait_texture, vec2(0.02, 0.02));
    vec4 top_right = texture(portrait_texture, vec2(0.98, 0.02));
    vec4 bottom_left = texture(portrait_texture, vec2(0.02, 0.98));
    vec4 bottom_right = texture(portrait_texture, vec2(0.98, 0.98));
    vec3 top_bg = mix(top_left.rgb, top_right.rgb, UV.x);
    vec3 bottom_bg = mix(bottom_left.rgb, bottom_right.rgb, UV.x);
    vec3 expected_bg = mix(top_bg, bottom_bg, UV.y);
    float separation = distance(c.rgb, expected_bg);
    float silhouette = smoothstep(0.075, 0.19, separation);
    float corner_alpha = max(max(top_left.a, top_right.a), max(bottom_left.a, bottom_right.a));
    float has_alpha_background = 1.0 - step(0.08, corner_alpha);
    float mask = mix(silhouette, 1.0, has_alpha_background);
    float alpha = c.a * portrait_tint.a * mask;
    if (alpha < 0.02) discard;
    ALBEDO = c.rgb * portrait_tint.rgb;
    ALPHA = alpha;
}"
        };
        _portraitMaterial = new ShaderMaterial { Shader = shader };
        _portraitMaterial.SetShaderParameter("portrait_texture", portrait);
        _portraitMaterial.SetShaderParameter("portrait_tint", _baseTint);

        _sprite = new Sprite3D
        {
            Texture = portrait,
            PixelSize = _portraitHeight / Math.Max(1, portrait.GetHeight()),
            Position = new Vector3(0, _portraitBaseY, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            MaterialOverride = _portraitMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            FlipH = Team == BattleContext.EnemyTeam,
        };
        AddChild(_sprite);

        _hpBack = MakeBillboardQuad(new Vector2(1.68f, 0.15f), new Color(0.015f, 0.025f, 0.02f, 0.92f), 10);
        _hpBack.Position = new Vector3(0, hpY, 0.02f);
        AddChild(_hpBack);
        _hpFill = MakeBillboardQuad(new Vector2(1.58f, 0.095f), teamColor.Lightened(0.08f), 11);
        _hpFillMesh = (QuadMesh)_hpFill.Mesh;
        _hpFill.Position = new Vector3(0, hpY, 0.04f);
        AddChild(_hpFill);

        _name = MakeLabel(opening.Name, 22, Colors.White, 0.0063f);
        _name.Position = new Vector3(0, nameY, 0);
        AddChild(_name);
        _stats = MakeLabel("", 17, Color.FromHtml("#e2e7dd"), 0.0056f);
        _stats.Position = new Vector3(0, statsY, 0);
        AddChild(_stats);
        // 席名と行名（第123期 §3-3）。`FormationRules.SeatNames` / `RowOf` から引く。
        // 召喚枠（5-8）にも席名があるので、湧いた駒でもそのまま出る。
        _seat = MakeLabel(UiKit.SeatLabel(Slot), 15, UiKit.Muted, 0.0050f);
        _seat.Position = new Vector3(0, seatY, 0);
        AddChild(_seat);
        _status = MakeLabel("", 18, UiKit.Gold, 0.0058f);
        _status.Position = new Vector3(0, statusY, 0);
        AddChild(_status);

        SetHp(Hp);
        SetAttack(AttackValue, Pattern);
    }

    public void SetHome(Vector3 home)
    {
        _home = home;
        if (_alive) Position = home;
    }

    public void SetHp(int hp)
    {
        Hp = Math.Clamp(hp, 0, MaxHp);
        float ratio = Math.Clamp(Hp / (float)MaxHp, 0.001f, 1.0f);
        _hpFill.Scale = Vector3.One;
        _hpFillMesh.Size = new Vector2(1.58f * ratio, 0.095f);
        _hpFillMesh.CenterOffset = new Vector3(-0.79f * (1.0f - ratio), 0, 0);
        _hpFill.Position = new Vector3(0, _hpBack.Position.Y, 0.04f);
        _stats.Text = $"HP {Hp}/{MaxHp}  ・  攻 {AttackValue} {PatternGlyph(Pattern)}";
    }

    public void SetAttack(int attack, AttackPattern? pattern = null)
    {
        AttackValue = attack;
        if (pattern is { } value) Pattern = value;
        _stats.Text = $"HP {Hp}/{MaxHp}  ・  攻 {AttackValue} {PatternGlyph(Pattern)}";
    }

    public void SetStatus(string value)
    {
        _status.Text = value;
        _status.Visible = !string.IsNullOrWhiteSpace(value);
    }

    public void AnimateAttack(Vector3 direction)
    {
        if (!_alive) return;
        Vector3 planar = new(direction.X, 0, direction.Z);
        if (planar.LengthSquared() < 0.001f) planar = Team == BattleContext.PlayerTeam ? Vector3.Back : Vector3.Forward;
        planar = planar.Normalized();
        var tween = CreateTween();
        tween.TweenProperty(this, "position", _home + planar * 0.82f + Vector3.Up * 0.12f, 0.095)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "position", _home, 0.19)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public void AnimateHit()
    {
        if (!_alive) return;
        Color hit = new(1.45f, 0.62f, 0.48f, 1);
        var tween = CreateTween();
        tween.TweenProperty(_sprite, "modulate", hit, 0.045);
        tween.TweenProperty(_sprite, "modulate", Colors.White, 0.18);
        Vector3 kick = new(Team == BattleContext.PlayerTeam ? -0.20f : 0.20f, 0.06f, 0.12f);
        var shake = CreateTween();
        shake.TweenProperty(this, "position", _home + kick, 0.045);
        shake.TweenProperty(this, "position", _home - kick * 0.35f, 0.055);
        shake.TweenProperty(this, "position", _home, 0.08);
    }

    public void AnimateHeal()
    {
        if (!_alive) return;
        var tween = CreateTween();
        tween.TweenProperty(this, "scale", new Vector3(1.09f, 1.09f, 1.09f), 0.12)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "scale", Vector3.One, 0.22);
    }

    public void AnimateDeath()
    {
        if (!_alive) return;
        _alive = false;
        _ring.Visible = false;
        _status.Visible = false;
        _name.Visible = false;
        _stats.Visible = false;
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "position", _home + new Vector3(0.38f, -0.68f, 0.25f), 0.46)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.TweenProperty(this, "rotation:z", Team == BattleContext.PlayerTeam ? -0.32f : 0.32f, 0.42);
        tween.TweenProperty(_sprite, "modulate:a", 0.24f, 0.48).SetDelay(0.10);
        tween.TweenProperty(_hpBack, "scale", new Vector3(0.01f, 0.01f, 0.01f), 0.28);
        tween.TweenProperty(_hpFill, "scale", new Vector3(0.01f, 0.01f, 0.01f), 0.28);
    }

    public void AnimateRevive()
    {
        _alive = true;
        Visible = true;
        Position = _home + Vector3.Down * 0.45f;
        Rotation = Vector3.Zero;
        _sprite.Modulate = new Color(1, 1, 1, 0.12f);
        _ring.Visible = true;
        _name.Visible = true;
        _stats.Visible = true;
        _hpBack.Scale = Vector3.One;
        SetHp(Hp);
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "position", _home, 0.38)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_sprite, "modulate:a", 1.0f, 0.30);
    }

    public void AnimateAppear()
    {
        Scale = new Vector3(0.2f, 0.2f, 0.2f);
        _sprite.Modulate = new Color(1, 1, 1, 0);
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(this, "scale", Vector3.One, 0.35)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_sprite, "modulate:a", 1.0f, 0.24);
    }

    public void AnimateVictory()
    {
        if (!_alive || Team != BattleContext.PlayerTeam) return;
        _victory = true;
        Position = _home;
        Rotation = Vector3.Zero;
        Scale = Vector3.One;
        _sprite.Scale = Vector3.One;
        _ring.Visible = false;
        _hpBack.Visible = false;
        _hpFill.Visible = false;
        _name.Visible = false;
        _seat.Visible = false;
        _stats.Visible = false;
        _status.Visible = false;

        Texture2D victoryPortrait = UiKit.Portrait(_atlas, _unitId);
        var tween = CreateTween();
        tween.TweenProperty(_sprite, "modulate:a", 0.0f, 0.14)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() =>
        {
            SetPortraitGeometry(victoryPortrait, 0.0f);
            _sprite.Texture = victoryPortrait;
            _sprite.PixelSize = _portraitHeight / Math.Max(1, victoryPortrait.GetHeight());
            _sprite.Position = new Vector3(0, _portraitBaseY, 0);
            _sprite.Modulate = new Color(1, 1, 1, 0);
            _portraitMaterial.SetShaderParameter("portrait_texture", victoryPortrait);
        }));
        tween.TweenProperty(_sprite, "modulate:a", 1.0f, 0.30)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    public void AnimateMove(Vector3 target)
    {
        _home = target;
        var tween = CreateTween();
        tween.TweenProperty(this, "position", target + Vector3.Up * 0.24f, 0.22)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "position", target, 0.12);
    }

    public override void _Process(double delta)
    {
        _phase += (float)delta * 2.1f;
        if (!_alive || _victory) return;
        float breath = Mathf.Sin(_phase) * 0.004f;
        float scaleY = 1.0f + breath;
        _sprite.Scale = new Vector3(1.0f - breath * 0.18f, scaleY, 1.0f);
        _sprite.Position = new Vector3(0, PortraitGroundY + _portraitGroundDistance * scaleY, 0);
        _shadow.Scale = new Vector3(1.0f - breath * 0.10f, 1, 1.0f - breath * 0.10f);
        _ring.Rotation = new Vector3(0, _phase * 0.15f, 0);
    }

    private void SetPortraitGeometry(Texture2D portrait, float bottomPaddingRatio)
    {
        float padding = Math.Clamp(bottomPaddingRatio, 0.0f, 0.45f) * _portraitHeight;
        _portraitGroundDistance = _portraitHeight * 0.5f - padding;
        _portraitBaseY = PortraitGroundY + _portraitGroundDistance;
    }

    private static Label3D MakeLabel(string text, int size, Color color, float pixelSize)
        => new()
        {
            Text = text,
            Font = new SystemFont
            {
                FontNames = new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "Segoe UI" },
                AllowSystemFallback = true,
            },
            FontSize = size,
            PixelSize = pixelSize,
            Modulate = color,
            OutlineModulate = new Color(0.005f, 0.01f, 0.008f, 0.96f),
            OutlineSize = 7,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
        };

    private static MeshInstance3D MakeBillboardQuad(Vector2 size, Color color, int renderPriority)
    {
        StandardMaterial3D material = MakeMaterial(color, true, color * 0.18f, true);
        material.NoDepthTest = true;
        material.RenderPriority = renderPriority;
        return new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = size },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
    }

    private static StandardMaterial3D MakeMaterial(Color color, bool transparent = false, Color? emission = null, bool billboard = false)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            BillboardMode = billboard ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled,
        };
        if (transparent || color.A < 0.999f) material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        if (emission is { } glow)
        {
            material.EmissionEnabled = true;
            material.Emission = glow;
            material.EmissionEnergyMultiplier = 1.2f;
        }
        return material;
    }

    private static string PatternGlyph(AttackPattern pattern) => pattern switch
    {
        AttackPattern.Sweep => "《薙》",
        AttackPattern.Pierce => "《貫》",
        AttackPattern.All => "《全》",
        _ => "《単》",
    };
}
