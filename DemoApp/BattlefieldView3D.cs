using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D : Control
{
    private readonly Dictionary<int, BattlePawn3D> _pawns = new();
    private SubViewport _viewport = null!;
    private Node3D _world = null!;
    private Node3D _actorRoot = null!;
    private Node3D _fxRoot = null!;
    private Node3D _scenery = null!;
    private bool _fortress;
    private Camera3D _camera = null!;
    private Texture2D _atlas = null!;
    private Label _eyebrow = null!;
    private Label _headline = null!;
    private Label _subline = null!;
    private Label _centerBanner = null!;
    private Control _bonusAttackCutIn = null!;
    private ColorRect _bonusAttackShade = null!;
    private ColorRect _bonusAttackSlash = null!;
    private ColorRect _bonusAttackEdgeTop = null!;
    private ColorRect _bonusAttackEdgeBottom = null!;
    private Label _bonusAttackKicker = null!;
    private Label _bonusAttackTitle = null!;
    private Label _bonusAttackActor = null!;

    /// <summary>
    /// いまの「拍」（第125期 段2）。<b>ターン頭 ／ 手番: 誰 ／ 割り込み</b>のどれかを常に出す。
    /// <b>これが無いと割り込みが「そういう順番で起きた1件」に潰れる。</b>
    /// </summary>
    private Label _beat = null!;

    /// <summary>いま手番の主として印を出している駒（第125期 段2）。</summary>
    private BattlePawn3D? _turnOwner;
    private Vector3 _cameraHome;
    private BattleAttackAudio _attackAudio = null!;

    /// <summary>画角（第124期 3-c）。<b>寄りは距離で作る</b>ので、ここは動かさない。</summary>
    private const float CameraFov = 39.0f;

    /// <summary>
    /// 注視点（第124期 3-c）。<b>盤面の中心より奥・上を見る。</b>
    /// 立ち絵は縦に長いので、足元（1.0）ではなく胸のあたりを見る。
    ///
    /// <para><b>奥（z を負）へ振っても駒は大きくならない</b>——手前の地面が枠から出るぶん
    /// 空が上から入ってくるだけで、駒の占める割合は変わらない（第124期に測って戻した）。</para>
    /// </summary>
    private static readonly Vector3 CameraFocus = new(0, 1.35f, 0f);

    public IReadOnlyDictionary<int, BattlePawn3D> Pawns => _pawns;

    public override void _Ready()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Ignore;
        _atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");

        var container = new SubViewportContainer
        {
            Stretch = true,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        container.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(container);

        _viewport = new SubViewport
        {
            // **`Size` は効かない。** `SubViewportContainer.Stretch = true` が
            // SubViewport のサイズを枠のサイズで上書きするので、ここを上げても1ピクセルも増えない
            // （第124期に一度これで測って外した）。**効くノブは 3D のスケーリング。**
            Size = new Vector2I(1280, 720),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            HandleInputLocally = false,
            // 第124期 3-c: 「解像度も低め」への直答。3D を 1.5 倍で描いて縮める（スーパーサンプリング）。
            // FXAA は輪郭をぼかす方向なので、密度を上げた以上は外す。
            Scaling3DMode = Viewport.Scaling3DModeEnum.Bilinear,
            Scaling3DScale = 1.5f,
            Msaa3D = Viewport.Msaa.Msaa4X,
            ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled,
        };
        container.AddChild(_viewport);
        _world = new Node3D();
        _viewport.AddChild(_world);
        BuildWorld();
        BuildOverlay();
        _attackAudio = new BattleAttackAudio();
        AddChild(_attackAudio);
        VisibilityChanged += () => { if (!IsVisibleInTree()) _attackAudio.StopAll(); };
    }

    private void BuildWorld()
    {
        var skyMaterial = new ProceduralSkyMaterial
        {
            SkyTopColor = Color.FromHtml("#607f8d"),
            SkyHorizonColor = Color.FromHtml("#dfc995"),
            GroundBottomColor = Color.FromHtml("#1e3028"),
            GroundHorizonColor = Color.FromHtml("#84926d"),
            SunAngleMax = 20.0f,
        };
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = skyMaterial },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Color.FromHtml("#b8c9bd"),
            AmbientLightEnergy = 0.42f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            FogEnabled = true,
            FogLightColor = Color.FromHtml("#a8b6ae"),
            FogLightEnergy = 0.62f,
            FogDensity = 0.0035f,
        };
        _world.AddChild(new WorldEnvironment { Environment = environment });
        _world.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-34, -42, 0),
            LightColor = Color.FromHtml("#ffe6b0"),
            LightEnergy = 1.12f,
            ShadowEnabled = true,
        });

        // 第124期 3-c: 「駒が小さく見える。もう少しズームアップして」への直答。
        // **画角ではなく距離で寄る**——画角を狭めると遠近が消えて 2.5D の奥行きが失われる。
        //
        // **寄せ幅は手前のレーンで決まる。** 駒は x ∈ [-5.2, 5.2] に並ぶが、
        // 手前のレーン（z = +2.15）はカメラに 2.15 近いぶんだけ横へ広がるので、
        // **原点で足りていても角の駒が枠から出る**（7.9/11.7 で実際に切れた）。
        // 9.7/14.8 → 8.5/12.9。原点での半幅 8.3 に対し、手前の角は約 6.4。
        // 注視点も 1.0 → 1.35 へ上げた——駒の立ち絵は縦に長いので、足元を狙うと上が余る。
        _cameraHome = new Vector3(0, 8.5f, 12.9f);
        _camera = new Camera3D { Position = _cameraHome, Current = true, Fov = CameraFov, Near = 0.1f, Far = 80.0f };
        _world.AddChild(_camera);
        _camera.LookAt(CameraFocus, Vector3.Up);

        _scenery = new MeadowEnvironment3D();
        _world.AddChild(_scenery);

        _actorRoot = new Node3D();
        _world.AddChild(_actorRoot);
        _fxRoot = new Node3D();
        _world.AddChild(_fxRoot);
    }

    private void BuildOverlay()
    {
        var topShade = new ColorRect
        {
            Color = new Color(0.012f, 0.025f, 0.021f, 0.78f),
            Position = Vector2.Zero,
            Size = new Vector2(2400, 92),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(topShade);
        _eyebrow = OverlayText("BATTLE 2.5D  /  TURN 0", 10, UiKit.Gold, new Vector2(20, 12));
        _headline = OverlayText("草原遭遇戦", 24, Colors.White, new Vector2(18, 27));
        _subline = OverlayText("3D地形上で BattleEvent を再生", 11, UiKit.Muted, new Vector2(20, 62));

        BuildCameraControls();

        _centerBanner = UiKit.Text("", 36, Colors.White);
        _centerBanner.SetAnchorsPreset(LayoutPreset.FullRect);
        _centerBanner.HorizontalAlignment = HorizontalAlignment.Center;
        _centerBanner.VerticalAlignment = VerticalAlignment.Center;
        _centerBanner.MouseFilter = MouseFilterEnum.Ignore;
        _centerBanner.AddThemeConstantOverride("outline_size", 12);
        _centerBanner.AddThemeColorOverride("font_outline_color", Colors.Black);
        _centerBanner.Visible = false;
        AddChild(_centerBanner);

        // 拍の帯（第125期 段2）。**眉の行の右**に置く——中央のバナーは見せ場が使っており、
        // 常時出しっぱなしにすると見せ場が読めなくなる。
        _beat = OverlayText("", 13, UiKit.Gold, new Vector2(20, 76));

        BuildSuperFlash();
        BuildBonusAttackCutIn();
        BuildTurnLabel();
    }

    /// <summary>
    /// 追加攻撃専用のカットイン。盤面は見えるまま、上寄りへ細い帯を差し込み、
    /// 通常の手番がいったん断ち切られたことを形で見せる。
    /// </summary>
    private void BuildBonusAttackCutIn()
    {
        _bonusAttackCutIn = new Control
        {
            Visible = false,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _bonusAttackCutIn.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_bonusAttackCutIn);

        _bonusAttackShade = new ColorRect
        {
            Color = new Color(0.005f, 0.012f, 0.018f, _superFlashEnabled ? 0 : 0.36f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _bonusAttackShade.SetAnchorsPreset(LayoutPreset.FullRect);
        _bonusAttackCutIn.AddChild(_bonusAttackShade);

        _bonusAttackSlash = new ColorRect
        {
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0.30f,
            AnchorBottom = 0.30f,
            OffsetLeft = -90,
            OffsetRight = 90,
            OffsetTop = -48,
            OffsetBottom = 48,
            RotationDegrees = -2.0f,
            Color = new Color(0.015f, 0.075f, 0.11f, 0.94f),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _bonusAttackCutIn.AddChild(_bonusAttackSlash);

        _bonusAttackEdgeTop = BonusAttackEdge(-53, 4, Color.FromHtml("#63d7ff"));
        _bonusAttackEdgeBottom = BonusAttackEdge(49, 2, new Color(Colors.White, 0.72f));

        _bonusAttackKicker = BonusAttackText("INTERRUPT  //  EXTRA ATTACK", 13, Color.FromHtml("#63d7ff"), -37, -15);
        _bonusAttackTitle = BonusAttackText("", 36, Colors.White, -20, 25);
        _bonusAttackTitle.AddThemeConstantOverride("outline_size", 11);
        _bonusAttackActor = BonusAttackText("追加攻撃 発動", 14, UiKit.Ink, 24, 45);
    }

    private ColorRect BonusAttackEdge(float centerOffset, float height, Color color)
    {
        var edge = new ColorRect
        {
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0.30f,
            AnchorBottom = 0.30f,
            OffsetLeft = -90,
            OffsetRight = 90,
            OffsetTop = centerOffset - height * 0.5f,
            OffsetBottom = centerOffset + height * 0.5f,
            RotationDegrees = -2.0f,
            Color = color,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _bonusAttackCutIn.AddChild(edge);
        return edge;
    }

    private Label BonusAttackText(string value, int size, Color color, float top, float bottom)
    {
        Label label = UiKit.Text(value, size, color);
        label.AnchorLeft = 0;
        label.AnchorRight = 1;
        label.AnchorTop = 0.30f;
        label.AnchorBottom = 0.30f;
        label.OffsetTop = top;
        label.OffsetBottom = bottom;
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.VerticalAlignment = VerticalAlignment.Center;
        label.MouseFilter = MouseFilterEnum.Ignore;
        label.AddThemeConstantOverride("outline_size", 7);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        _bonusAttackCutIn.AddChild(label);
        return label;
    }

    private Label OverlayText(string value, int size, Color color, Vector2 position)
    {
        Label label = UiKit.Text(value, size, color);
        label.Position = position;
        label.AddThemeConstantOverride("outline_size", 5);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(label);
        return label;
    }

    public void BeginBattle(IReadOnlyList<DemoOpening> openings, string stageName, int stageIndex)
    {
        _shieldCowedGeneration++;
        _attackAudio.StopAll();
        // 波の番号で背景を選ぶ。表示名や戦闘ログの文字列は判定に使わない。
        bool fortress = stageIndex == 3;
        if (_fortress != fortress)
        {
            _world.RemoveChild(_scenery);
            _scenery.QueueFree();
            _scenery = fortress ? new FortressEnvironment3D() : new MeadowEnvironment3D();
            _world.AddChild(_scenery);
            _fortress = fortress;
        }
        ResetSuperFlash();
        ResetBindings();
        ResetSeals();
        ResetPopups();
        ResetTurnLabel();
        foreach (BattlePawn3D pawn in _pawns.Values) pawn.QueueFree();
        _pawns.Clear();
        foreach (Node child in _fxRoot.GetChildren()) child.QueueFree();
        _eyebrow.Text = "BATTLE 2.5D  /  TURN 0";
        _turnOwner = null;
        _beat.Text = "";
        _bonusAttackCutIn.Visible = false;
        _headline.Text = $"{stageName} — {(_fortress ? "城門前の攻防" : "草原遭遇戦")}";
        _subline.Text = "Space: 一時停止   1–4: 再生速度   T: 戦績   細い線＝誰の仕業か   ▶＝手番の主 / ▷＝ターン頭 / ⚡＝手番の外";
        _cameraMotion?.Kill();
        _camera.Position = _cameraHome;
        _camera.Fov = CameraFov;
        _camera.LookAt(CameraFocus, Vector3.Up);

        foreach (DemoOpening opening in openings)
        {
            var pawn = new BattlePawn3D();
            pawn.Configure(opening, _atlas);
            pawn.SetHome(PawnPosition(opening.Team, opening.Slot));
            _actorRoot.AddChild(pawn);
            _pawns[opening.InstanceId] = pawn;
            RegisterSealHolder(opening);
        }
    }

    public void SetTurn(int turn)
    {
        AnnounceTurn(turn);
        _eyebrow.Text = $"BATTLE 2.5D  /  TURN {turn}";
        foreach (BattlePawn3D pawn in _pawns.Values)
        {
            pawn.SetStatus("");
            pawn.BeginStatusSnapshot();
        }
    }

    // =====================================================================================
    // 第125期 段2 —— 手番の外を「手番の外」として見せる。
    //
    // **`BattleCore` を1行も触らない。** 台本には割り込み（`Reaction`）・肩代わり（`Relayed`）・
    // 介入（`Intercept`・段1）が既に載っていて、足りないのは**いつ起きたか**の枠だけだった。
    // =====================================================================================

    /// <summary>
    /// いま誰の番かを画面に出す（第125期 段2・§5-1 の 1）。
    /// <paramref name="pawn"/> が <c>null</c> なら<b>誰の手番でもない時間</b>。
    /// </summary>
    public void SetTurnOwner(BattlePawn3D? pawn, string label, Color color)
    {
        if (_turnOwner is not null && _turnOwner != pawn) _turnOwner.SetTurnOwner(false);
        _turnOwner = pawn;
        pawn?.SetTurnOwner(true);
        _beat.Text = label;
        _beat.AddThemeColorOverride("font_color", color);
    }

    /// <summary>
    /// 割り込みが始まった（第125期 段2・§5-1 の 2）。<b>手番の主の印を落として</b>、
    /// 割り込んだ駒の足元に輪を出す。<b>演出の作り込みではなく「流れが止まった」ことが分かればよい。</b>
    /// </summary>
    public void BeginInterrupt(BattlePawn3D? actor, string kind, Color color, bool markActor = true)
    {
        _turnOwner?.SetTurnOwner(true, paused: true);
        _beat.Text = _turnOwner is null ? $"⚡ {kind}" : $"⚡ {kind}（{_turnOwner.UnitName} の手番を止めて）";
        _beat.AddThemeColorOverride("font_color", color);
        if (actor is null || !markActor) return;
        MakeGroundRing(actor.Home, color, 1.25f, 0.34);
        MakeGroundRing(actor.Home, color, 0.85f, 0.46);
        Float(actor, $"⚡ {kind}", color, true, 3.70f);
    }

    /// <summary>
    /// 追加攻撃の直前に通常進行を止めて見せる。表示専用の <c>Reaction</c> だけを読み、
    /// 攻撃処理や発動条件には触れない。
    /// </summary>
    public async Task ShowBonusAttack(BattlePawn3D? actor)
    {
        if (actor is null) return;

        // 4倍速でも「誰が光ったか」は読める長さを残す。戦闘全体の速度は変えず、
        // この短い発動確認だけ上限を設ける。
        double speed = Math.Clamp(actor.AnimationSpeed, 0.75, 1.60);
        double Time(double seconds) => seconds / speed;
        Color aura = Color.FromHtml("#63d7ff");
        Color teamAccent = actor.Team == BattleContext.PlayerTeam ? UiKit.Player : UiKit.Enemy;

        _bonusAttackSlash.Color = new Color(aura.Darkened(0.78f), 0.94f);
        _bonusAttackEdgeTop.Color = new Color(aura, 0.96f);
        _bonusAttackEdgeBottom.Color = new Color(teamAccent, 0.82f);
        _bonusAttackKicker.AddThemeColorOverride("font_color", aura);
        _bonusAttackTitle.Text = actor.UnitName;
        _bonusAttackActor.Text = "追加攻撃 発動  //  BREAK IN";

        // 格闘ゲームの必殺技発動と同じ順序。先に駒そのものを青く立たせ、
        // 視線が発動者へ移ってから名前の帯を差し込む。
        BeginBonusAura(actor, aura, Time(0.78));
        BeginSuperFlash(actor, Time(0.48));
        var actorPulse = actor.CreateTween();
        actorPulse.TweenProperty(actor, "scale", new Vector3(1.08f, 1.08f, 1.08f), Time(0.075))
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        actorPulse.TweenProperty(actor, "scale", Vector3.One, Time(0.18))
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

        _cameraMotion?.Kill();
        _camera.Position = _cameraHome;
        var cameraTween = _cameraMotion = _camera.CreateTween();
        cameraTween.TweenProperty(_camera, "fov", CameraFov - 4.0f, Time(0.09))
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        cameraTween.TweenInterval(Time(0.13));
        cameraTween.TweenProperty(_camera, "fov", CameraFov, Time(0.18))
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);

        await ToSignal(GetTree().CreateTimer(Time(0.10)), SceneTreeTimer.SignalName.Timeout);

        _bonusAttackCutIn.Visible = true;
        _bonusAttackCutIn.Modulate = Colors.White;
        _bonusAttackSlash.PivotOffset = _bonusAttackSlash.Size * 0.5f;
        _bonusAttackEdgeTop.PivotOffset = _bonusAttackEdgeTop.Size * 0.5f;
        _bonusAttackEdgeBottom.PivotOffset = _bonusAttackEdgeBottom.Size * 0.5f;
        _bonusAttackSlash.Scale = new Vector2(0.025f, 1);
        _bonusAttackEdgeTop.Scale = new Vector2(0.025f, 1);
        _bonusAttackEdgeBottom.Scale = new Vector2(0.025f, 1);
        _bonusAttackKicker.Modulate = new Color(1, 1, 1, 0);
        _bonusAttackTitle.Modulate = new Color(1, 1, 1, 0);
        _bonusAttackActor.Modulate = new Color(1, 1, 1, 0);

        var tween = _bonusAttackCutIn.CreateTween();
        tween.TweenProperty(_bonusAttackSlash, "scale", Vector2.One, Time(0.085))
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(_bonusAttackEdgeTop, "scale", Vector2.One, Time(0.11));
        tween.Parallel().TweenProperty(_bonusAttackEdgeBottom, "scale", Vector2.One, Time(0.14));
        tween.Parallel().TweenProperty(_bonusAttackKicker, "modulate:a", 1.0f, Time(0.065)).SetDelay(Time(0.025));
        tween.Parallel().TweenProperty(_bonusAttackTitle, "modulate:a", 1.0f, Time(0.065)).SetDelay(Time(0.045));
        tween.Parallel().TweenProperty(_bonusAttackActor, "modulate:a", 1.0f, Time(0.065)).SetDelay(Time(0.065));
        tween.TweenInterval(Time(0.15));
        tween.TweenProperty(_bonusAttackCutIn, "modulate:a", 0.0f, Time(0.09))
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);

        await ToSignal(tween, Tween.SignalName.Finished);
        _bonusAttackCutIn.Visible = false;
    }

    /// <summary>
    /// 発動者の輪郭と先行する残像を青く示す。不発では残像を引き戻して粒にほどく。
    /// </summary>
    private void BeginBonusAura(BattlePawn3D actor, Color color, double duration, bool interrupted = false)
    {
        actor.BeginBonusAfterimage(color, duration, interrupted);
        MakeGroundRing(actor.RestPosition, new Color(color, 0.35f), 0.95f, duration * 0.6);
    }
    /// <summary>割り込みが終わって手番へ戻る（第125期 段2）。</summary>
    public void EndInterrupt(string label, Color color)
    {
        _turnOwner?.SetTurnOwner(true);
        _beat.Text = label;
        _beat.AddThemeColorOverride("font_color", color);
    }

    /// <summary>
    /// 矢が逸れた（第125期 段2・§5-1 の 4）。<b>本来の標的から割り込んだ駒へ折れる線</b>を描く。
    ///
    /// <para><b>攻撃の線をそのまま割り込んだ駒へ引くだけでは「その駒が殴られた」にしかならない</b>
    /// ——だから線は<b>本来の標的から始めて、途中で折る。</b> 折れ点を持ち上げるのは、
    /// 攻撃の直線（`Attack`）と形で割るため。</para>
    /// </summary>
    public void Divert(BattlePawn3D? victim, BattlePawn3D? guard, string label, Color color)
    {
        if (guard is null) return;
        if (victim is not null && victim != guard)
        {
            Vector3 a = victim.FxPoint;
            Vector3 b = guard.FxPoint;
            Vector3 bend = (a + b) * 0.5f + Vector3.Up * 1.25f;
            MakeBeam(a, bend, new Color(color, 0.80f), 0.075f, 0.48);
            MakeBeam(bend, b, new Color(color, 0.80f), 0.075f, 0.48);
            MakeGroundRing(victim.Home, UiKit.Faint, 0.68f, 0.42);
        }
        MakeGroundRing(guard.RestPosition, color, 1.05f, 0.44);
        Float(guard, label, color, true, 3.52f);
    }

    /// <summary>
    /// 1発が分割されて中継された（第125期 段2・§5-1 の 5）。
    /// <b>受けた側から中継した側へ、太さの違う線を引く。</b> ゴルムの「耐久している感がない」への直答。
    /// </summary>
    public void Split(BattlePawn3D? victim, BattlePawn3D? relay, int amount, string label, Color color)
    {
        if (relay is null) return;
        if (victim is not null && victim != relay)
            MakeBeam(victim.FxPoint, relay.FxPoint, new Color(color, 0.72f), 0.13f, 0.46);
        MakeGroundRing(relay.Home, color, 1.12f, 0.46);
        Float(relay, $"{label} −{amount}", color, true, 3.46f);
    }

    public void SetSubline(string value) => _subline.Text = value;

    public void ShowVictoryPortraits()
    {
        foreach (BattlePawn3D pawn in _pawns.Values) pawn.AnimateVictory();
    }

    public BattlePawn3D? FindPawn(int? instanceId)
        => instanceId is { } id && _pawns.TryGetValue(id, out BattlePawn3D? pawn) ? pawn : null;

    public void EndGuards()
    {
        foreach (BattlePawn3D pawn in _pawns.Values)
            if (pawn.Hp > 0) pawn.EndGuard();
    }

    public void Guard(BattlePawn3D? victim, BattlePawn3D? guard)
    {
        if (victim is null || guard is null || victim == guard) return;
        Vector3 front = new(guard.Team == BattleContext.PlayerTeam ? 1 : -1, 0, 0);
        guard.BeginGuard(victim.RestPosition + front * 1.1f);
    }

    public void PlayDeath(BattlePawn3D? pawn, bool finish = false)
    {
        if (pawn is null) return;
        SealPawnDied(pawn);
        pawn.SetHp(0);
        _attackAudio.PlayDeath(pawn.Team, finish);
        pawn.AnimateDeath();
    }

    public void AddSummon(DemoOpening opening)
    {
        var pawn = new BattlePawn3D();
        pawn.Configure(opening, _atlas);
        pawn.SetHome(PawnPosition(opening.Team, opening.Slot));
        _actorRoot.AddChild(pawn);
        _pawns[opening.InstanceId] = pawn;
        RegisterSealHolder(opening);
        ConnectSeals();
        _attackAudio.PlaySummon();
        pawn.AnimateAppear();
        MakeGroundRing(pawn.Home, UiKit.Violet, 0.9f, 0.50);
    }

    public void RevivePawn(BattlePawn3D pawn)
    {
        _attackAudio.PlayRevive();
        pawn.AnimateRevive();
    }

    public void MovePawn(BattlePawn3D pawn, int slot)
    {
        pawn.Slot = slot;
        pawn.AnimateMove(PawnPosition(pawn.Team, slot));
    }

    public async Task Attack(
        BattlePawn3D? from,
        BattlePawn3D? to,
        AttackPattern pattern,
        IReadOnlyList<BattlePawn3D> impacted,
        bool reaction = false,
        bool friendly = false,
        bool advance = true,
        bool holdPosition = false,
        Func<Task>? shieldImpact = null)
    {
        if (from is null || to is null) return;
        Color color = friendly ? UiKit.Violet : reaction ? UiKit.Gold : from.Team == BattleContext.PlayerTeam ? UiKit.Player : UiKit.Enemy;
        List<BattlePawn3D> hits = impacted.Distinct().ToList();
        if (hits.Count == 0) hits.Add(to);

        // Advances は表示専用。踏み込む駒だけが標的の手前まで移動し、
        // 到着後に攻撃エフェクトを出してから元の席へ戻る。
        if (advance) await from.AdvanceToAttack(to.RestPosition);
        if (holdPosition) from.HoldComboPosition();
        bool charged = !reaction && from.IsCharging;
        if (!reaction) from.ReleaseCharge();
        _attackAudio.PlayAttack(from.UnitId, from.Team, pattern, reaction, charged);
        CameraPunch((from.GlobalPosition + to.GlobalPosition) * 0.5f, pattern);

        if (shieldImpact is not null) await shieldImpact();
        else switch (pattern)
        {
            case AttackPattern.Sweep:
                // 第125期 3-f: 「薙ぎ全般が散弾みたいで銃撃戦に見える」への直答。
                // **標的ごとの細い線をやめ、1枚の弧にする**——線が人数ぶん飛ぶから散弾に見えていた。
                // 弧は**実際に当たった駒の角度の幅**を覆うので、「どこまで届いたか」は
                // 足元の輪ではなく弧そのものが出す（第124期 3-d の「面で示す」を1枚に絞った形）。
                MakeSweepArc(from.FxPoint, hits, color);
                foreach (BattlePawn3D hit in hits)
                    MakeGroundRing(hit.Home, color, hit == to ? 0.62f : 0.86f, 0.52);
                break;
            case AttackPattern.Pierce:
            {
                // 第124期 3-d: 「貫通・薙ぎが区別できない」への直答。
                // **貫きは1本の槍が列を走り抜ける**——薙ぎ（扇）と形で割れるように、
                // 芯を長く・白く・太くし、**貫いた駒の足元だけを縦長の輪**にする
                // （薙ぎの丸い輪と形が違う）。第123期に「ボルグとドルガの薙ぎはよく見えた」のは
                // 扇が大きな面だったからで、貫きには面が1つも無かった（Q0-9）。
                Vector3 direction = (to.FxPoint - from.FxPoint).Normalized();
                float farthest = hits.Max(pawn => Math.Max(0.0f, (pawn.FxPoint - from.FxPoint).Dot(direction)));
                Vector3 end = from.FxPoint + direction * (farthest + 3.0f);
                MakeBeam(from.FxPoint, end, new Color(color, 0.34f), 0.62f, 0.62);
                MakeBeam(from.FxPoint, end, new Color(color, 0.72f), 0.26f, 0.62);
                MakeBeam(from.FxPoint, end, Colors.White.Lerp(color, 0.22f), 0.085f, 0.62);
                foreach (BattlePawn3D hit in hits) MakeLanceMark(hit.Home, direction, color);
                break;
            }
            case AttackPattern.All:
                foreach (BattlePawn3D hit in hits)
                {
                    MakeBeam(hit.FxPoint + Vector3.Up * 5.8f, hit.FxPoint, color, 0.17f, 0.50);
                    MakeGroundRing(hit.Home, color, 0.74f, 0.55);
                }
                break;
            default:
                if (from.UnitId == "mudo") MakePunchImpact(to, from.AnimationSpeed);
                else MakeSingleSlash(from, to, color);
                break;
        }

        if (!holdPosition) from.ReturnFromAttack();
    }

    public void BeginCharge(BattlePawn3D? pawn, int percent)
    {
        if (pawn is null) return;
        bool alreadyCharging = pawn.IsCharging;
        pawn.BeginCharge(percent);
        if (!alreadyCharging) _attackAudio.PlayCharge(pawn.UnitId);
    }

    public void ReleaseChargedSkill(BattlePawn3D? pawn)
    {
        if (pawn is null || !pawn.IsCharging) return;
        pawn.ReleaseCharge();
        _attackAudio.PlayChargeRelease(pawn.UnitId);
    }

    public void PlayDirectReactionSound(BattlePawn3D? actor)
    {
        if (actor is not null) _attackAudio.PlayDirectReaction(actor.UnitId);
    }

    public void PlayBattleStartSound() => _attackAudio.PlayBattleStart();

    public void PlayAttackChangeSound(int change) => _attackAudio.PlayAttackChange(change);

    public void PlayStatusGainSound(string key) => _attackAudio.PlayStatusGain(key);

    public void PlayStatusDamageSound(string? label) => _attackAudio.PlayStatusDamage(label);

    public void PlayHitSound(BattlePawn3D? target)
    {
        if (target?.UnitId == "kado") _attackAudio.PlayKadoHit();
    }

    public void Parry(BattlePawn3D? attacker, BattlePawn3D? defender)
    {
        if (defender is null) return;
        if (defender.UnitId == "gald") _attackAudio.PlayParry();
        Vector3 forward = (attacker?.FxPoint ?? defender.FxPoint + Vector3.Right) - defender.FxPoint;
        forward.Y = 0;
        if (forward.LengthSquared() < 0.001f) forward = Vector3.Right;
        forward = forward.Normalized();
        defender.AnimateParry(forward);
        Vector3 center = defender.FxPoint + forward * 0.65f;
        Vector3 side = forward.Cross(Vector3.Up).Normalized();
        Color ice = new(0.55f, 0.90f, 1.0f);
        double duration = 0.28 / defender.AnimationSpeed;

        // 盾の前を斜めに払う弧。足元の輪ではなく、刃がぶつかった高さに出す。
        Vector3 previous = center + side * -0.85f + Vector3.Up * -0.48f;
        for (int i = 1; i <= 10; i++)
        {
            float t = i / 10f;
            Vector3 next = center + side * Mathf.Lerp(-0.85f, 0.85f, t)
                + Vector3.Up * Mathf.Lerp(-0.48f, 0.70f, t)
                + forward * (Mathf.Sin(t * Mathf.Pi) * 0.32f);
            MakeBeam(previous, next, ice, 0.075f, duration);
            previous = next;
        }
        MakeBeam(center - side * 0.40f, center + side * 0.40f, Colors.White, 0.13f, duration * 0.6);
        MakeBeam(center - Vector3.Up * 0.50f, center + Vector3.Up * 0.50f, Colors.White, 0.09f, duration * 0.6);

        // 接点から外へ跳ぶ火花。攻撃者へダメージが返ったようには描かない。
        for (int i = 0; i < 7; i++)
        {
            float spread = (i - 3) / 3f;
            Vector3 direction = (forward * 0.45f + side * spread + Vector3.Up * (0.5f + 0.35f * (i % 2))).Normalized();
            var spark = new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.045f, 0.045f, 0.32f) },
                Position = center,
                MaterialOverride = MakeMaterial(i % 2 == 0 ? Colors.White : UiKit.Gold, true, true),
            };
            _fxRoot.AddChild(spark);
            spark.LookAt(center + direction, Vector3.Up);
            var tween = spark.CreateTween().SetParallel();
            tween.TweenProperty(spark, "position", center + direction * (1.2f + 0.15f * i), duration)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(spark, "transparency", 1.0f, duration);
            tween.Finished += spark.QueueFree;
        }
        Float(defender, "受け流し", ice, true, 3.20f, 1.25f);
    }

    /// <summary>
    /// 転倒した瞬間。姿勢を次の手番まで残し、足元の土煙で「移動した」だけではないことを示す。
    /// </summary>
    public void StaggerFall(BattlePawn3D? pawn, Color color)
    {
        if (pawn is null) return;
        pawn.AnimateStaggerFall();
        MakeGroundRing(pawn.Home, color, 1.18f, 0.48);
        MakeGroundRing(pawn.Home, UiKit.Faint, 0.72f, 0.34);

        Vector3 center = pawn.Home + Vector3.Up * 0.10f;
        Color dust = Color.FromHtml("#c9b58a").Lerp(color, 0.18f);
        for (int i = 0; i < 8; i++)
        {
            float angle = Mathf.Tau * i / 8.0f + (UiKit.StableHash(pawn.UnitName) % 17) * 0.017f;
            Vector3 direction = new(Mathf.Cos(angle), 0.16f + 0.05f * (i % 3), Mathf.Sin(angle));
            var mote = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.055f, Height = 0.11f, RadialSegments = 8, Rings = 4 },
                Position = center,
                MaterialOverride = MakeMaterial(new Color(dust, 0.78f), true, true),
            };
            _fxRoot.AddChild(mote);
            double duration = 0.30 + 0.025 * (i % 3);
            var tween = mote.CreateTween().SetParallel();
            tween.TweenProperty(mote, "position", center + direction * (0.75f + 0.08f * i), duration)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.TweenProperty(mote, "scale", Vector3.One * 0.18f, duration);
            tween.TweenProperty(mote, "transparency", 1.0f, duration).SetDelay(0.08);
            tween.Finished += mote.QueueFree;
        }
    }

    /// <summary>
    /// 転倒を消費して手番を失った瞬間。手番の輪を暗くし、地面の×印で「行動なし」を示す。
    /// </summary>
    public void StaggerLost(BattlePawn3D? pawn, Color color)
    {
        if (pawn is null) return;
        pawn.AnimateStaggerLost();
        pawn.SetTurnOwner(true, paused: true);
        _beat.Text = $"⊘ 手番喪失: {pawn.UnitName}（転倒）";
        _beat.AddThemeColorOverride("font_color", color);

        Vector3 center = pawn.Home + Vector3.Up * 0.10f;
        Vector3 a = new(0.68f, 0, 0.68f);
        Vector3 b = new(0.68f, 0, -0.68f);
        MakeBeam(center - a, center + a, new Color(color, 0.86f), 0.095f, 0.42);
        MakeBeam(center - b, center + b, new Color(color, 0.86f), 0.095f, 0.42);
        MakeGroundRing(pawn.Home, UiKit.Faint, 1.02f, 0.42);
    }

    public void AttackCue(BattlePawn3D? pawn, string value, Color color)
    {
        if (pawn is null) return;
        Float(pawn, $"【{value}】", color, true, 3.45f);
    }

    /// <summary>
    /// ダメージの数字（第124期 3-b: 「もっと大きくわかりやすく」への直答）。
    ///
    /// <para>出どころはログに残し、盤上は数字だけ。同じ駒の連続分は合算する。</para>
    /// </summary>
    public void DamagePopup(BattlePawn3D? pawn, int amount, string source, Color color,
                            bool large = false, bool withSource = true, bool poison = false)
    {
        if (pawn is null) return;
        NumberPopup(pawn, amount, false, color, large);
    }

    /// <param name="friendly">
    /// 味方の刃（第124期 3-f: 「味方への隣接ダメージも分かりやすくしたい」への直答）。
    /// <b>形で割る</b>——敵からの一撃は球、継続効果は輪、<b>味方の刃は足元の紫の輪</b>を足す。
    /// 色（紫）だけでは継続効果の紫と混ざる。
    /// </param>
    public void Impact(BattlePawn3D? pawn, Color color, bool status, bool friendly = false)
    {
        if (pawn is null) return;
        if (friendly) MakeGroundRing(pawn.Home, UiKit.Violet, 1.02f, 0.58);
        Vector3 center = pawn.RestPosition + new Vector3(0, 1.15f, 0);
        var burst = new MeshInstance3D
        {
            Mesh = status
                ? new TorusMesh { InnerRadius = 0.28f, OuterRadius = 0.42f, Rings = 24, RingSegments = 8 }
                : new SphereMesh { Radius = 0.42f, Height = 0.84f, RadialSegments = 20, Rings = 10 },
            Position = center,
            MaterialOverride = MakeMaterial(new Color(color, status ? 0.75f : 0.48f), true, true, 0.2f, color),
            Scale = Vector3.One * 0.16f,
        };
        _fxRoot.AddChild(burst);
        var tween = burst.CreateTween().SetParallel();
        tween.TweenProperty(burst, "scale", Vector3.One * (status ? 1.8f : 2.3f), status ? 0.42 : 0.32)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(burst, "position:y", center.Y + 0.25f, 0.32);
        tween.Finished += burst.QueueFree;
    }

    /// <summary>
    /// <b>書き手 → 対象の線</b>（第124期 3-h）。段2 で `Heal` / `Revive` / `Summon` /
    /// `Move` / `Highlight` / `StatusGain` に <c>ActorId</c> が載ったので、
    /// 「起きたこと」に「誰の仕業か」を結べるようになった。
    ///
    /// <para><b>攻撃の線とは別の形にする</b>——攻撃は太い実線、こちらは細い線＋
    /// 書き手の足元の輪。混ぜると「殴った」と「支えた」が同じ絵になる。</para>
    /// </summary>
    public void Link(BattlePawn3D? from, BattlePawn3D? to, Color color, string label)
    {
        if (from is null) { if (to is not null) Float(to, label, color); return; }
        if (to is not null && to != from)
        {
            MakeBeam(from.FxPoint, to.FxPoint, new Color(color, 0.70f), 0.055f, 0.52);
            MakeGroundRing(to.Home, color, 0.70f, 0.46);
        }
        MakeGroundRing(from.Home, color, 0.92f, 0.46);
    }

    /// <summary>
    /// 書き手の居ない出来事（第124期 §5-3）。<b>無理に「動いた本人」を線の根元にしない</b>
    /// ——それは書き手ではないので、線を引くと嘘になる。<b>線を引かずに札だけ出す。</b>
    /// </summary>
    public void Orphan(BattlePawn3D? pawn, string label, Color color)
    {
        if (pawn is null) return;
        MakeGroundRing(pawn.Home, UiKit.Faint, 0.78f, 0.42);
        Float(pawn, label, color, false, 3.35f, 0.90f);
    }

    public void Float(BattlePawn3D? pawn, string value, Color color, bool large = false)
        => Float(pawn, value, color, large, 3.05f);

    private Label3D? Float(BattlePawn3D? pawn, string value, Color color, bool large, float height, float scale = 1.0f)
    {
        if (pawn is null) return null;
        TrimPopups(pawn.InstanceId);
        var label = new Label3D
        {
            Text = value,
            Position = pawn.RestPosition + new Vector3(0, height, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Font = new SystemFont { FontNames = new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "Segoe UI" }, AllowSystemFallback = true },
            FontSize = large ? 29 : 23,
            PixelSize = (large ? 0.0072f : 0.0064f) * scale,
            Modulate = color,
            OutlineSize = 12,
            OutlineModulate = new Color(0.005f, 0.008f, 0.006f, 0.98f),
            NoDepthTest = true,
        };
        _fxRoot.AddChild(label);
        _popups.Add((pawn.InstanceId, label));
        var tween = label.CreateTween().SetParallel();
        tween.TweenProperty(label, "position:y", height + pawn.Home.Y + 0.95f, 0.78)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0.0f, 0.78).SetDelay(0.22);
        tween.Finished += label.QueueFree;
        return label;
    }

    public void ShowBanner(string value, Color color, double seconds = 1.0)
    {
        _centerBanner.Text = value;
        _centerBanner.AddThemeColorOverride("font_color", color);
        _centerBanner.Visible = true;
        _centerBanner.Modulate = Colors.White;
        _centerBanner.Scale = new Vector2(0.88f, 0.88f);
        _centerBanner.PivotOffset = Size * 0.5f;
        var tween = CreateTween().SetParallel();
        tween.TweenProperty(_centerBanner, "scale", Vector2.One, 0.20)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_centerBanner, "modulate:a", 0.0f, 0.38).SetDelay(Math.Max(0.2, seconds - 0.38));
        tween.Finished += () => _centerBanner.Visible = false;
    }

    /// <summary>
    /// 薙ぎの弧（第125期 3-f）。<b>当たった駒の角度の幅を1枚で覆う。</b>
    /// 標的ごとに線を飛ばすと散弾に見える——薙ぎは1回の振りなので、絵も1枚にする。
    /// </summary>
    private void MakeSweepArc(Vector3 origin, IReadOnlyList<BattlePawn3D> hits, Color color)
    {
        if (hits.Count == 0) return;
        var angles = new List<float>();
        float reach = 2.6f;
        foreach (BattlePawn3D hit in hits)
        {
            Vector3 d = hit.FxPoint - origin;
            d.Y = 0;
            if (d.LengthSquared() < 0.0004f) continue;
            angles.Add(Mathf.Atan2(d.X, d.Z));
            reach = Math.Max(reach, d.Length() + 0.9f);
        }
        if (angles.Count == 0) return;
        float lo = angles.Min(), hi = angles.Max();
        // 1体しか当たっていなくても弧として見えるだけの幅は持たせる（薙ぎは薙ぎである）。
        if (hi - lo < 0.34f) { float mid = (lo + hi) * 0.5f; lo = mid - 0.17f; hi = mid + 0.17f; }

        const int Blades = 13;
        for (int i = 0; i < Blades; i++)
        {
            float t = i / (float)(Blades - 1);
            float ang = Mathf.Lerp(lo - 0.10f, hi + 0.10f, t);
            var dir = new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang));
            // 中ほどを長く・端を短く。1枚の刃が振り抜けた跡に見える。
            float len = reach * (0.74f + 0.26f * Mathf.Sin(t * Mathf.Pi));
            MakeBeam(origin, origin + dir * len, new Color(color, 0.30f), 0.30f, 0.30 + t * 0.02);
        }
        MakeGroundRing(new Vector3(origin.X, 0, origin.Z), color, reach * 0.55f, 0.40);
    }

    /// <summary>
    /// 全体に落ちた（第125期 3-a）。<b>ゾトの破裂は `Attack` を1件も出さない</b>
    /// ——敵全員・味方全員へ1体ずつ <c>ApplyDamage</c> を呼ぶだけなので、
    /// 台本には `Pattern` が <c>null</c> の `Damage` が人数ぶん並ぶだけになる（Phase 0 Q0-6）。
    /// <b>敵の全体攻撃と同じ絵</b>（上から降る線）をここで出す。
    /// </summary>
    public void Burst(BattlePawn3D? origin, IReadOnlyList<BattlePawn3D> hits, Color color)
    {
        if (hits.Count == 0) return;
        if (origin is not null)
        {
            MakeGroundRing(origin.Home, color, 2.35f, 0.46);
            MakeGroundRing(origin.Home, color, 1.45f, 0.34);
        }
        foreach (BattlePawn3D hit in hits)
        {
            MakeBeam(hit.FxPoint + Vector3.Up * 5.8f, hit.FxPoint, color, 0.17f, 0.50);
            MakeGroundRing(hit.Home, color, 0.74f, 0.55);
        }
        CameraPunch(origin?.GlobalPosition ?? Vector3.Zero, AttackPattern.All);
    }

    /// <summary>
    /// 回復の数字（第125期 3-c）。<b>ダメージと同じ大きさで出す</b>
    /// ——「回復はもっと緑文字ではっきり出したほうが良い」への直答。
    /// </summary>
    public void HealPopup(BattlePawn3D? pawn, int amount)
    {
        if (pawn is null) return;
        NumberPopup(pawn, amount, true, UiKit.Heal, amount >= 25);
    }

    private void MakeBeam(Vector3 from, Vector3 to, Color color, float width, double duration)
    {
        Vector3 delta = to - from;
        float length = Math.Max(0.05f, delta.Length());
        var beam = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(width, width, length) },
            Position = (from + to) * 0.5f,
            MaterialOverride = MakeMaterial(new Color(color, Math.Min(0.92f, Math.Max(0.18f, color.A))), true, true, 0.15f, color * 1.25f),
        };
        _fxRoot.AddChild(beam);
        Vector3 up = Math.Abs(delta.Normalized().Dot(Vector3.Up)) > 0.94f ? Vector3.Forward : Vector3.Up;
        beam.LookAt(to, up);
        beam.Scale = new Vector3(0.08f, 0.08f, 1);
        var tween = beam.CreateTween();
        tween.TweenProperty(beam, "scale", Vector3.One, Math.Min(0.10, duration * 0.3))
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(Math.Max(0.05, duration - 0.16));
        tween.TweenProperty(beam, "scale", new Vector3(0.05f, 0.05f, 1), 0.08);
        tween.Finished += beam.QueueFree;
    }

    /// <summary>
    /// 貫きが通り抜けた跡（第124期 3-d）。<b>薙ぎの丸い輪と形で割るための縦長の板。</b>
    /// 進行方向へ伸ばすので、レーンを走った向きがそのまま見える。
    /// </summary>
    private void MakeLanceMark(Vector3 position, Vector3 direction, Color color)
    {
        var mark = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.34f, 0.02f, 2.30f) },
            Position = position + new Vector3(0, 0.03f, 0),
            MaterialOverride = MakeMaterial(new Color(color, 0.55f), true, true, 0.35f, color * 0.7f),
        };
        Vector3 flat = new Vector3(direction.X, 0, direction.Z);
        if (flat.LengthSquared() > 0.0001f) mark.Rotation = new Vector3(0, Mathf.Atan2(flat.X, flat.Z), 0);
        _fxRoot.AddChild(mark);
        var tween = mark.CreateTween().SetParallel();
        tween.TweenProperty(mark, "scale", new Vector3(1.0f, 1.0f, 1.22f), 0.50);
        tween.TweenProperty(mark, "transparency", 1.0f, 0.50).SetDelay(0.10);
        tween.Finished += mark.QueueFree;
    }

    private void MakeGroundRing(Vector3 position, Color color, float radius, double duration)
    {
        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = radius * 0.72f, OuterRadius = radius, Rings = 30, RingSegments = 8 },
            Position = position + Vector3.Up * 0.07f,
            MaterialOverride = MakeMaterial(new Color(color, 0.76f), true, true, 0.2f, color),
            Scale = Vector3.One * 0.20f,
        };
        _fxRoot.AddChild(ring);
        var tween = ring.CreateTween();
        tween.TweenProperty(ring, "scale", Vector3.One, duration)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Finished += ring.QueueFree;
    }

    private void CameraPunch(Vector3 focus, AttackPattern pattern)
    {
        float strength = pattern switch { AttackPattern.All => 1.15f, AttackPattern.Pierce => 0.9f, AttackPattern.Sweep => 0.72f, _ => 0.45f };
        Vector3 nudge = (focus - new Vector3(0, 0, 0)) * 0.025f * strength;
        _cameraMotion?.Kill();
        var tween = _cameraMotion = _camera.CreateTween();
        tween.TweenProperty(_camera, "position", _cameraHome + nudge + new Vector3(0, -0.20f * strength, -0.32f * strength), 0.09);
        tween.Parallel().TweenProperty(_camera, "fov", CameraFov - 2.0f * strength, 0.09);
        tween.TweenProperty(_camera, "position", _cameraHome, 0.24)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(_camera, "fov", CameraFov, 0.24);
    }

    private static Vector3 PawnPosition(int team, int slot)
    {
        (float depth, float lane) = slot switch
        {
            0 => (1.35f, -2.15f),
            1 => (1.35f, 2.15f),
            2 => (3.25f, 0.0f),
            3 => (5.20f, -2.15f),
            4 => (5.20f, 2.15f),
            5 => (3.25f, -1.35f),
            6 => (3.25f, 1.35f),
            7 => (1.35f, 0.0f),
            8 => (5.20f, 0.0f),
            _ => (3.25f, 0.0f),
        };
        float x = team == BattleContext.PlayerTeam ? -depth : depth;
        return new Vector3(x, 0.08f, lane);
    }

    private static StandardMaterial3D MakeMaterial(
        Color color,
        bool transparent = false,
        bool unshaded = false,
        float roughness = 0.82f,
        Color? emission = null)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = roughness,
            Metallic = 0.01f,
            ShadingMode = unshaded ? BaseMaterial3D.ShadingModeEnum.Unshaded : BaseMaterial3D.ShadingModeEnum.PerPixel,
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
}
