using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlePawn3D : Node3D
{
    private const float PortraitGroundY = 0.05f;

    private Sprite3D _sprite = null!;
    private ShaderMaterial _portraitMaterial = null!;
    private MeshInstance3D _shadow = null!;
    private MeshInstance3D _ring = null!;
    private MeshInstance3D _turnRing = null!;
    private MeshInstance3D _hpBack = null!;
    private MeshInstance3D _hpFill = null!;
    private QuadMesh _hpFillMesh = null!;
    private Label3D _name = null!;
    private Label3D _seat = null!;
    private Label3D _stats = null!;
    private Label3D _status = null!;
    private Label3D _forecast = null!;
    /// <summary>盤面ルールの保持者の札（第171期・<b>表示専用</b>）。<b>倒れるまで出しっぱなし。</b></summary>
    private Node3D _ruleTag = null!;
    private Vector3 _home;
    private Color _baseTint;
    private Texture2D _atlas = null!;
    private string _unitId = "";
    public string UnitId => _unitId;
    private float _phase;
    private float _portraitHeight = 2.25f;
    private float _portraitBaseY = 1.12f;
    private float _portraitGroundDistance = 1.07f;
    private float _fxHeight = 1.35f;
    private bool _alive = true;
    private bool _victory;
    private bool _burning;
    private bool _staggered;
    private float _staggerPose;
    private float _staggerRecoverDelay;
    private float _staggerPulse;
    private Tween? _motion;
    private Vector3? _guardPosition;
    private Node3D _fire = null!;
    private PoisonEffect3D _poison = null!;
    private ConfusionEffect3D _confusion = null!;
    private StatusEffects3D _statusEffects = null!;
    public double AnimationSpeed { get; set; } = 1;
    public Vector3 RestPosition => _guardPosition ?? _home;
    public bool IsGuarding => _guardPosition is not null;

    // 配置は変えず、台本の介入から被弾までだけ前へ出る。
    public void BeginGuard(Vector3 position)
    {
        if (!_alive) return;
        _guardPosition = position;
        var tween = BeginMotion();
        tween.TweenProperty(this, "position", position, 0.22 / AnimationSpeed)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    public void EndGuard()
    {
        if (_guardPosition is null) return;
        _guardPosition = null;
        if (!_alive) return;
        var tween = BeginMotion();
        tween.TweenProperty(this, "position", _home, 0.24 / AnimationSpeed)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
    }

    private Tween BeginMotion()
    {
        _motion?.Kill();
        return _motion = CreateTween();
    }

    public void SetBurning(bool burning)
    {
        bool active = burning && _alive && !_victory;
        _fire.Visible = active;
        if (_burning == active) return;
        _burning = active;
        // 勝利絵の表示後に遅れた通知が来ても、戦闘絵で上書きしない。
        if (_victory || _sprite is null) return;
        Texture2D portrait = UiKit.BattlePortrait(_atlas, _unitId, active);
        if (_sprite.Texture == portrait) return;
        _sprite.Texture = portrait;
        _sprite.PixelSize = _portraitHeight / Math.Max(1, portrait.GetHeight());
        _portraitMaterial.SetShaderParameter("portrait_texture", portrait);
    }
    public void SetPoisoned(bool poisoned) => _poison.SetActive(poisoned && _alive && !_victory);
    public void SetStatusEffects(int marked, int stunned, int armor)
    {
        bool active = _alive && !_victory;
        _statusEffects.SetAmounts(active ? marked : 0, active ? stunned : 0, active ? armor : 0);
    }

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
    public bool Advances { get; private set; }
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
        _baseAttack = opening.Attack;
        Pattern = opening.Pattern;
        Advances = opening.Advances;
        UnitName = opening.Name;
        _phase = (UiKit.StableHash(opening.UnitId) % 1000) * 0.0061f;
        _baseTint = UiKit.BattlePortraitTint(opening.UnitId, opening.Team == BattleContext.EnemyTeam)
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

        // 手番の主の印（第125期 段2）。**常設の輪で、必要なときだけ見せる。**
        // 攻撃・被弾の輪（`MakeGroundRing`）は一瞬で消えるので、
        // 「いま誰の番か」を出すには**消えない印**が要る。
        _turnRing = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.96f, OuterRadius = 1.14f, Rings = 32, RingSegments = 8 },
            Position = new Vector3(0, 0.04f, 0),
            MaterialOverride = MakeMaterial(new Color(UiKit.Gold, 0.85f), true, UiKit.Gold * 0.9f),
            Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_turnRing);

        // サークルの周囲へ炎を立てる。奥側は駒に隠れ、手前側は足に重なる。
        // 面は Y 軸だけでカメラへ向け、炎の根元を地面に固定する。
        var fireShader = new Shader { Code = @"shader_type spatial;
render_mode unshaded, cull_disabled, blend_mix, depth_draw_never;
uniform float phase = 0.0;
void vertex() {
    vec3 right = normalize(vec3(INV_VIEW_MATRIX[0].x, 0.0, INV_VIEW_MATRIX[0].z));
    vec3 up = vec3(0.0, 1.0, 0.0);
    vec3 forward = cross(right, up);
    MODELVIEW_MATRIX = VIEW_MATRIX * mat4(
        vec4(right * length(MODEL_MATRIX[0].xyz), 0.0),
        vec4(up * length(MODEL_MATRIX[1].xyz), 0.0),
        vec4(forward * length(MODEL_MATRIX[2].xyz), 0.0), MODEL_MATRIX[3]);
}
void fragment() {
    float t = TIME * 2.8 + phase;
    float y = 1.0 - UV.y;
    float flame = 0.0;
    for (int i = 0; i < 2; i++) {
        float k = float(i);
        float h = 0.48 + 0.20 * sin(t * 1.3 + k * 2.1);
        float x = 0.27 + k * 0.46 + sin(y * 8.0 - t * 2.0 + k) * 0.12 * y;
        float width = 0.26 * max(0.0, 1.0 - y / h);
        flame = max(flame, (1.0 - smoothstep(width * 0.35, width + 0.012, abs(UV.x - x))) * (1.0 - smoothstep(h - 0.10, h, y)));
    }
    float sparks = 0.0;
    for (int i = 0; i < 5; i++) {
        float k = float(i);
        float rise = fract(t * 0.23 + k * 0.21);
        vec2 p = vec2(0.15 + k * 0.17 + sin(t + k) * 0.035, rise);
        sparks = max(sparks, (1.0 - smoothstep(0.004, 0.015, length(UV - vec2(p.x, 1.0 - p.y)))) * sin(rise * 3.14159));
    }
    vec3 fire = mix(vec3(1.0, 0.82, 0.18), vec3(1.0, 0.15, 0.015), smoothstep(0.04, 0.65, y));
    ALBEDO = fire;
    EMISSION = fire * 1.4;
    ALPHA = max(flame * 0.85 * smoothstep(0.0, 0.04, y), sparks);
}" };
        _fire = new Node3D { Visible = false };
        AddChild(_fire);
        var fireMesh = new QuadMesh { Size = new Vector2(0.52f, 1.1f) };
        const int flameCount = 12;
        for (int i = 0; i < flameCount; i++)
        {
            // 駒ごとに固定したばらつき。隣同士の順序を保ち、均等な柵に見えない程度に崩す。
            float spacing = Mathf.Tau / flameCount;
            float angle = spacing * (i + 0.22f * Mathf.Sin(i * 2.399f + _phase));
            float heightScale = 1.0f + 0.22f * Mathf.Sin(i * 4.137f + _phase * 1.7f);
            var fireMaterial = new ShaderMaterial { Shader = fireShader };
            fireMaterial.SetShaderParameter("phase", _phase + i * 1.73f);
            _fire.AddChild(new MeshInstance3D
            {
                Mesh = fireMesh,
                Position = new Vector3(Mathf.Cos(angle) * 0.78f, 0.05f + 0.55f * heightScale, Mathf.Sin(angle) * 0.78f),
                Scale = new Vector3(1, heightScale, 1),
                MaterialOverride = fireMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }

        _poison = new PoisonEffect3D();
        _poison.Configure(_phase);
        AddChild(_poison);

        Texture2D portrait = UiKit.BattlePortrait(atlas, opening.UnitId);
        bool hasCustomPortrait = UiKit.HasCustomBattlePortrait(opening.UnitId)
            || UiKit.HasCustomPortrait(opening.UnitId);
        _portraitHeight = UiKit.PortraitWorldHeight(opening.UnitId);
        _confusion = new ConfusionEffect3D();
        _confusion.Configure(_portraitHeight, _phase);
        AddChild(_confusion);
        _statusEffects = new StatusEffects3D();
        _statusEffects.Configure(_portraitHeight, _phase);
        AddChild(_statusEffects);
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
uniform float aura_amount = 0.0;
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
    // 隅の1点に靴や武器がかかっても、透明な隅があれば素材のアルファを使う。
    float corner_alpha = min(min(top_left.a, top_right.a), min(bottom_left.a, bottom_right.a));
    float has_alpha_background = 1.0 - step(0.08, corner_alpha);
    float mask = mix(silhouette, 1.0, has_alpha_background);
    // 緑単色で用意した素材は、輪郭の混色も抜く。既存の透過・灰色背景には適用しない。
    if (has_alpha_background < 0.5 && expected_bg.g > 0.8 && max(expected_bg.r, expected_bg.b) < 0.15) {
        mask = 1.0 - smoothstep(0.02, 0.18, c.g - max(c.r, c.b));
        c.g = min(c.g, max(c.r, c.b));
    }
    float alpha = c.a * portrait_tint.a * mask;
    if (alpha < 0.02) discard;
    ALBEDO = mix(c.rgb * portrait_tint.rgb, portrait_tint.rgb, aura_amount);
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
        _hpBack.Position = new Vector3(0, hpY + 0.55f, 0.02f);
        AddChild(_hpBack);
        _hpFill = MakeBillboardQuad(new Vector2(1.58f, 0.095f), teamColor.Lightened(0.08f), 11);
        _hpFillMesh = (QuadMesh)_hpFill.Mesh;
        _hpFill.Position = new Vector3(0, hpY, 0.04f);
        AddChild(_hpFill);

        _name = MakeLabel(opening.Name, 22, Colors.White, 0.0063f);
        _name.Position = new Vector3(0, nameY + 0.55f, 0);
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

        // 溜めの予告（第125期 段3-b）。**ターンをまたいで残る**ので `_status` とは別に持つ
        // （`SetTurn` が毎ターン status を消すので、そこへ書くと予告が次の手番まで残らない）。
        _forecast = MakeLabel("", 17, UiKit.Gold, 0.0056f);
        _forecast.Position = new Vector3(0, statusY + 0.19f, 0);
        _forecast.Visible = false;
        AddChild(_forecast);

        BuildRuleMarks(opening);
        BuildAttackChange();
        BuildStatusIcons();

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
        int change = attack - AttackValue;
        AttackValue = attack;
        if (pattern is { } value) Pattern = value;
        _stats.Text = $"HP {Hp}/{MaxHp}  ・  攻 {AttackValue} {PatternGlyph(Pattern)}";
        ShowAttackChange(change);
    }

    /// <summary>
    /// 手番の主の印（第125期 段2）。<b>誰の番かが分からないと「割り込み」が成立しない。</b>
    ///
    /// <para><paramref name="paused"/> は<b>割り込まれて待っている</b>状態
    /// ——輪を金から暗い色へ落とす。<b>立ち絵そのものは暗くしない</b>
    /// （<c>Modulate</c> は死亡・出現のトゥイーンが握っているので、そこへ割り込むと絵が壊れる）。</para>
    /// </summary>
    public void SetTurnOwner(bool on, bool paused = false)
    {
        _turnRing.Visible = on && _alive;
        if (!_turnRing.Visible) return;
        Color tint = paused ? UiKit.Faint : UiKit.Gold;
        _turnRing.MaterialOverride = MakeMaterial(new Color(tint, paused ? 0.55f : 0.85f), true, tint * 0.9f);
        _turnRing.Scale = Vector3.One * (paused ? 0.92f : 1.0f);
    }

    /// <summary>いま出している状態異常の札（第125期 段3-e。画面下の一覧が引く）。</summary>
    public string StatusText => _status?.Text ?? "";

    public void SetStatus(string value)
    {
        _status.Text = value;
        // 文章は画面下の一覧に残す。駒の上にはアイコンだけを描く。
        _status.Visible = false;
    }

    /// <summary>
    /// 次の手番に何が来るかの予告（第125期 段3-b）。<b>溜めは画面上ただの空白のターン</b>なので、
    /// 予告が無いと「ためている感」が出ない。<c>Charge</c> は次の倍率・攻撃型・名前を
    /// 全部持っている（`BattleEventKind.Charge` の明文）ので、台本だけで書ける。
    /// </summary>
    public void SetForecast(string value)
    {
        _forecast.Text = value;
        _forecast.Visible = _alive && !string.IsNullOrWhiteSpace(value);
    }

    public async Task AdvanceToAttack(Vector3 targetPosition)
    {
        if (!_alive || !Advances) return;

        Vector3 origin = RestPosition;
        Vector3 planar = new(targetPosition.X - origin.X, 0, targetPosition.Z - origin.Z);
        float distance = planar.Length();
        const float attackDistance = 1.75f;
        if (distance <= attackDistance) return;

        Vector3 destination = origin + planar / distance * (distance - attackDistance) + Vector3.Up * 0.10f;
        double duration = Math.Clamp(0.12 + (distance - attackDistance) * 0.018, 0.16, 0.28) / AnimationSpeed;
        var tween = BeginMotion();
        tween.TweenProperty(this, "position", destination, duration)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        await ToSignal(tween, Tween.SignalName.Finished);
    }

    public void ReturnFromAttack()
    {
        if (!_alive || !Advances || Position.IsEqualApprox(RestPosition)) return;

        var tween = BeginMotion();
        tween.TweenInterval(0.055 / AnimationSpeed);
        tween.TweenProperty(this, "position", RestPosition, 0.20 / AnimationSpeed)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    // 庇いの位置を基準に、刃を迎えて斜めへ払う。被弾の後ずさりとは別の動き。
    public void AnimateParry(Vector3 direction)
    {
        if (!_alive) return;
        Vector3 forward = new(direction.X, 0, direction.Z);
        if (forward.LengthSquared() < 0.001f)
            forward = new Vector3(Team == BattleContext.PlayerTeam ? 1 : -1, 0, 0);
        forward = forward.Normalized();
        Vector3 side = forward.Cross(Vector3.Up);
        var tween = BeginMotion();
        tween.TweenProperty(this, "position", RestPosition + forward * 0.28f, 0.045 / AnimationSpeed);
        tween.TweenProperty(this, "position", RestPosition + side * 0.24f + Vector3.Up * 0.07f, 0.07 / AnimationSpeed)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "position", RestPosition, 0.15 / AnimationSpeed)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    public void AnimateHit(bool poison = false)
    {
        if (!_alive) return;
        Color hit = poison ? new Color(1.05f, 0.48f, 1.35f, 1) : new Color(1.45f, 0.62f, 0.48f, 1);
        if (poison) _poison.Pulse();
        var tween = CreateTween();
        tween.TweenProperty(_sprite, "modulate", hit, 0.045);
        tween.TweenProperty(_sprite, "modulate", Colors.White, 0.18);
        Vector3 kick = new(Team == BattleContext.PlayerTeam ? -0.20f : 0.20f, 0.06f, 0.12f);
        if (poison) kick *= 0.25f;
        var shake = BeginMotion();
        shake.TweenProperty(this, "position", RestPosition + kick, 0.045 / AnimationSpeed);
        shake.TweenProperty(this, "position", RestPosition - kick * 0.35f, 0.055 / AnimationSpeed);
        shake.TweenProperty(this, "position", RestPosition, 0.06 / AnimationSpeed);
    }

    public void AnimateHeal()
    {
        if (!_alive) return;
        var tween = CreateTween();
        tween.TweenProperty(this, "scale", new Vector3(1.09f, 1.09f, 1.09f), 0.12)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "scale", Vector3.One, 0.22);
    }

    /// <summary>
    /// 転倒した姿勢を、次の手番を失うまで残す。死亡の沈み込みとは違い、立ち絵だけを
    /// 地面へ傾けるので、席・HP・状態の札は読み取れるままにする。
    /// </summary>
    public void AnimateStaggerFall()
    {
        if (!_alive) return;
        _staggered = true;
        _staggerRecoverDelay = 0;
        _staggerPulse = 1;
    }

    /// <summary>
    /// 転倒を消費して手番を失った拍。いったん起き上がろうともがいてから通常姿勢へ戻る。
    /// </summary>
    public void AnimateStaggerLost()
    {
        if (!_alive) return;
        _staggered = false;
        _staggerRecoverDelay = 0.11f;
        _staggerPulse = 1;
    }

    public void AnimateDeath()
    {
        if (!_alive) return;
        CancelCharge();
        ShowLifeTransition(LifeTransition3D.Kind.Death);
        ClearAuras();
        _statusIcons.Clear();
        _statusSnapshot.Clear();
        _alive = false;
        ResetStaggerPose();
        SetStatusEffects(0,0,0);
        _poison.Clear();
        _confusion.SetActive(false);
        SetBurning(false);
        _guardPosition = null;
        _ring.Visible = false;
        _turnRing.Visible = false;
        _status.Visible = false;
        _forecast.Visible = false;
        _name.Visible = false;
        _stats.Visible = false;
        // 保持者が倒れたらルールは消える。**札も一緒に消す**（第171期 §2-2）。
        _ruleTag.Visible = false;
        _attackDelta.Visible = false;
        var tween = BeginMotion().SetParallel();
        tween.TweenProperty(this, "position", Position + new Vector3(0.38f, -0.68f, 0.25f), 0.46)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
        tween.TweenProperty(this, "rotation:z", Team == BattleContext.PlayerTeam ? -0.32f : 0.32f, 0.42);
        tween.TweenProperty(_sprite, "modulate:a", 0.24f, 0.48).SetDelay(0.10);
        tween.TweenProperty(_hpBack, "scale", new Vector3(0.01f, 0.01f, 0.01f), 0.28);
        tween.TweenProperty(_hpFill, "scale", new Vector3(0.01f, 0.01f, 0.01f), 0.28);
    }

    public void AnimateRevive()
    {
        ShowLifeTransition(LifeTransition3D.Kind.Revive);
        _motion?.Kill();
        ResetStaggerPose();
        _guardPosition = null;
        _alive = true;
        Visible = true;
        Position = _home + Vector3.Down * 0.45f;
        Rotation = Vector3.Zero;
        Scale = Vector3.One;
        _sprite.Modulate = new Color(1, 1, 1, 0.12f);
        _ring.Visible = true;
        _name.Visible = true;
        _stats.Visible = true;
        // 戻ってきたらルールも戻る（保持者の生死がそのまま規則の生死・第171期 §2-2）。
        _ruleTag.Visible = true;
        ShowAttackChange(0);
        _hpBack.Scale = Vector3.One;
        SetHp(Hp);
        var tween = BeginMotion().SetParallel();
        tween.TweenProperty(this, "position", _home, 0.38)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_sprite, "modulate:a", 1.0f, 0.30);
    }

    public void AnimateAppear()
    {
        ShowLifeTransition(LifeTransition3D.Kind.Summon);
        Scale = new Vector3(0.2f, 0.2f, 0.2f);
        _sprite.Modulate = new Color(1, 1, 1, 0);
        var tween = BeginMotion().SetParallel();
        tween.TweenProperty(this, "scale", Vector3.One, 0.35)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_sprite, "modulate:a", 1.0f, 0.24);
    }

    public void AnimateVictory()
    {
        if (!_alive || Team != BattleContext.PlayerTeam) return;
        CancelCharge();
        _victory = true;
        _confusion.SetActive(false);
        _statusIcons.Clear();
        ResetStaggerPose();
        SetStatusEffects(0,0,0);
        _poison.Clear();
        _motion?.Kill();
        _guardPosition = null;
        SetBurning(false);
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
        _guardPosition = null;
        _home = target;
        var tween = BeginMotion();
        tween.TweenProperty(this, "position", target + Vector3.Up * 0.24f, 0.22)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "position", target, 0.12);
    }

    public override void _Process(double delta)
    {
        _phase += (float)delta * 2.1f;
        if (!_alive || _victory) return;

        float animationDelta = (float)delta * (float)Math.Max(0.1, AnimationSpeed);
        if (_staggerRecoverDelay > 0)
        {
            _staggerRecoverDelay -= animationDelta;
            if (_staggerRecoverDelay < 0) _staggerRecoverDelay = 0;
        }
        float staggerTarget = _staggered || _staggerRecoverDelay > 0 ? 1 : 0;
        float poseSpeed = staggerTarget > _staggerPose ? 7.5f : 5.2f;
        _staggerPose = Mathf.MoveToward(_staggerPose, staggerTarget, animationDelta * poseSpeed);
        _staggerPulse = Mathf.MoveToward(_staggerPulse, 0, animationDelta * 3.6f);

        float breath = Mathf.Sin(_phase) * 0.004f;
        float scaleY = 1.0f + breath;
        float fall = _staggerPose * _staggerPose * (3.0f - 2.0f * _staggerPose);
        float fallSign = Team == BattleContext.PlayerTeam ? -1.0f : 1.0f;
        float struggle = Mathf.Sin((1.0f - _staggerPulse) * Mathf.Tau * 1.5f) * _staggerPulse * 0.10f;
        _sprite.Rotation = new Vector3(0, 0, fallSign * (fall * 0.78f + struggle));
        _sprite.Scale = new Vector3(1.0f - breath * 0.18f + fall * 0.05f, scaleY - fall * 0.08f, 1.0f);
        _sprite.Position = new Vector3(
            fallSign * fall * 0.22f,
            PortraitGroundY + _portraitGroundDistance * scaleY - fall * 0.30f,
            0);
        float shadowSpread = fall * 0.28f;
        _shadow.Scale = new Vector3(1.0f - breath * 0.10f + shadowSpread, 1, 1.0f - breath * 0.10f - shadowSpread * 0.35f);
        _ring.Rotation = new Vector3(0, _phase * 0.15f, 0);
    }

    private void ResetStaggerPose()
    {
        _staggered = false;
        _staggerPose = 0;
        _staggerRecoverDelay = 0;
        _staggerPulse = 0;
        if (_sprite is not null)
        {
            _sprite.Rotation = Vector3.Zero;
            _sprite.Scale = Vector3.One;
            _sprite.Position = new Vector3(0, _portraitBaseY, 0);
        }
        if (_shadow is not null) _shadow.Scale = Vector3.One;
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
