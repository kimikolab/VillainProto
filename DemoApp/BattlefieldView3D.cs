using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D : Control
{
    private readonly Dictionary<int, BattlePawn3D> _pawns = new();
    private SubViewport _viewport = null!;
    private Node3D _world = null!;
    private Node3D _actorRoot = null!;
    private Node3D _fxRoot = null!;
    private Camera3D _camera = null!;
    private Texture2D _atlas = null!;
    private Label _eyebrow = null!;
    private Label _headline = null!;
    private Label _subline = null!;
    private Label _centerBanner = null!;
    private Vector3 _cameraHome;

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
            AmbientLightEnergy = 0.74f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            FogEnabled = true,
            FogLightColor = Color.FromHtml("#d8cba5"),
            FogLightEnergy = 0.62f,
            FogDensity = 0.0062f,
        };
        _world.AddChild(new WorldEnvironment { Environment = environment });
        _world.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-57, -24, 0),
            LightColor = Color.FromHtml("#ffe6b0"),
            LightEnergy = 1.36f,
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

        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(26, 20), SubdivideWidth = 12, SubdivideDepth = 10 },
            MaterialOverride = MakeMaterial(Color.FromHtml("#526f3e"), roughness: 0.98f),
        };
        _world.AddChild(ground);

        AddHill(new Vector3(-10.2f, -1.35f, -6.5f), new Vector3(4.8f, 1.4f, 3.0f), "#3d5c36");
        AddHill(new Vector3(10.5f, -1.55f, -5.2f), new Vector3(4.4f, 1.5f, 3.2f), "#405f38");
        AddHill(new Vector3(-11.2f, -1.65f, 7.5f), new Vector3(4.0f, 1.5f, 3.4f), "#48683c");
        AddHill(new Vector3(11.4f, -1.7f, 8.2f), new Vector3(4.4f, 1.45f, 3.0f), "#46643b");

        StandardMaterial3D lane = MakeMaterial(new Color(0.78f, 0.69f, 0.42f, 0.18f), true, true, 1.0f, Color.FromHtml("#8f7f4e") * 0.16f);
        AddStrip(new Vector3(-6.5f, 0.023f, -2.15f), new Vector3(6.5f, 0.023f, -2.15f), 0.18f, lane);
        AddStrip(new Vector3(-6.5f, 0.023f, 2.15f), new Vector3(6.5f, 0.023f, 2.15f), 0.18f, lane);
        AddStrip(new Vector3(0, 0.021f, -4.6f), new Vector3(0, 0.021f, 4.6f), 0.10f, lane);

        StandardMaterial3D trunk = MakeMaterial(Color.FromHtml("#55442c"));
        StandardMaterial3D leavesA = MakeMaterial(Color.FromHtml("#244c34"));
        StandardMaterial3D leavesB = MakeMaterial(Color.FromHtml("#345d39"));
        Vector3[] trees =
        {
            new(-10,0,-7), new(-8.6f,0,-8.1f), new(-11.2f,0,-4.9f), new(9.4f,0,-7.7f), new(11.0f,0,-5.8f),
            new(-10.8f,0,6.8f), new(-9.0f,0,8.4f), new(9.1f,0,8.3f), new(11.0f,0,6.7f), new(12.1f,0,9.0f),
        };
        for (int i = 0; i < trees.Length; i++) AddTree(trees[i], trunk, i % 2 == 0 ? leavesA : leavesB, 0.78f + (i % 3) * 0.10f);

        StandardMaterial3D rock = MakeMaterial(Color.FromHtml("#6d7061"), roughness: 0.96f);
        foreach ((Vector3 pos, Vector3 scale) in new[]
        {
            (new Vector3(-7.2f,0,-4.4f), new Vector3(1.0f,0.45f,0.7f)),
            (new Vector3(7.8f,0,5.0f), new Vector3(1.25f,0.55f,0.8f)),
            (new Vector3(8.3f,0,-4.3f), new Vector3(0.7f,0.38f,0.55f)),
        })
        {
            _world.AddChild(new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 1.0f, Height = 1.35f, RadialSegments = 20, Rings = 8 },
                Position = pos + Vector3.Up * 0.25f,
                Scale = scale,
                MaterialOverride = rock,
            });
        }

        var mist = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(21, 4.5f) },
            Position = new Vector3(0, 2.0f, -8.8f),
            MaterialOverride = MakeMaterial(new Color(0.73f, 0.78f, 0.65f, 0.12f), true, true),
        };
        _world.AddChild(mist);

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

        var depthBadge = new PanelContainer
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            OffsetLeft = -194,
            OffsetTop = 15,
            OffsetRight = -18,
            OffsetBottom = 61,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        depthBadge.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.03f, 0.07f, 0.055f, 0.9f), UiKit.Player, 1, 18));
        var depthText = UiKit.Text("3D FIELD  ×  2D UNITS", 10, UiKit.Player.Lightened(0.18f));
        depthText.HorizontalAlignment = HorizontalAlignment.Center;
        depthText.VerticalAlignment = VerticalAlignment.Center;
        depthBadge.AddChild(depthText);
        AddChild(depthBadge);

        _centerBanner = UiKit.Text("", 36, Colors.White);
        _centerBanner.SetAnchorsPreset(LayoutPreset.FullRect);
        _centerBanner.HorizontalAlignment = HorizontalAlignment.Center;
        _centerBanner.VerticalAlignment = VerticalAlignment.Center;
        _centerBanner.MouseFilter = MouseFilterEnum.Ignore;
        _centerBanner.AddThemeConstantOverride("outline_size", 12);
        _centerBanner.AddThemeColorOverride("font_outline_color", Colors.Black);
        _centerBanner.Visible = false;
        AddChild(_centerBanner);
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

    public void BeginBattle(IReadOnlyList<DemoOpening> openings, string stageName)
    {
        foreach (BattlePawn3D pawn in _pawns.Values) pawn.QueueFree();
        _pawns.Clear();
        foreach (Node child in _fxRoot.GetChildren()) child.QueueFree();
        _eyebrow.Text = "BATTLE 2.5D  /  TURN 0";
        _headline.Text = $"{stageName} — 草原遭遇戦";
        _subline.Text = "Space: 一時停止   1–4: 再生速度   T: 戦績   細い線＝誰の仕業か";
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
        }
    }

    public void SetTurn(int turn)
    {
        _eyebrow.Text = $"BATTLE 2.5D  /  TURN {turn}";
        foreach (BattlePawn3D pawn in _pawns.Values) pawn.SetStatus("");
    }

    public void SetSubline(string value) => _subline.Text = value;

    public void ShowVictoryPortraits()
    {
        foreach (BattlePawn3D pawn in _pawns.Values) pawn.AnimateVictory();
    }

    public BattlePawn3D? FindPawn(int? instanceId)
        => instanceId is { } id && _pawns.TryGetValue(id, out BattlePawn3D? pawn) ? pawn : null;

    public void AddSummon(DemoOpening opening)
    {
        var pawn = new BattlePawn3D();
        pawn.Configure(opening, _atlas);
        pawn.SetHome(PawnPosition(opening.Team, opening.Slot));
        _actorRoot.AddChild(pawn);
        _pawns[opening.InstanceId] = pawn;
        pawn.AnimateAppear();
        MakeGroundRing(pawn.Home, UiKit.Violet, 0.9f, 0.50);
    }

    public void MovePawn(BattlePawn3D pawn, int slot)
    {
        pawn.Slot = slot;
        pawn.AnimateMove(PawnPosition(pawn.Team, slot));
    }

    public void Attack(
        BattlePawn3D? from,
        BattlePawn3D? to,
        AttackPattern pattern,
        IReadOnlyList<BattlePawn3D> impacted,
        bool reaction = false,
        bool friendly = false)
    {
        if (from is null || to is null) return;
        Color color = friendly ? UiKit.Violet : reaction ? UiKit.Gold : from.Team == BattleContext.PlayerTeam ? UiKit.Player : UiKit.Enemy;
        List<BattlePawn3D> hits = impacted.Distinct().ToList();
        if (hits.Count == 0) hits.Add(to);
        from.AnimateAttack(to.GlobalPosition - from.GlobalPosition);
        CameraPunch((from.GlobalPosition + to.GlobalPosition) * 0.5f, pattern);

        switch (pattern)
        {
            case AttackPattern.Sweep:
                // 第124期 3-d: 薙ぎは**面**（扇）で示す。第123期に「よく見えた」側なので形は変えず、
                // 副次目標の輪だけ大きくして「どこまで届いたか」を面と一緒に読めるようにする。
                foreach (BattlePawn3D hit in hits)
                {
                    MakeBeam(from.FxPoint, hit.FxPoint, color, 0.10f, 0.34);
                    MakeGroundRing(hit.Home, color, hit == to ? 0.62f : 0.86f, 0.52);
                }
                MakeSweepFan(from.FxPoint, to.FxPoint, color);
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
                MakeBeam(from.FxPoint, to.FxPoint, color, reaction ? 0.15f : 0.11f, 0.34);
                break;
        }
    }

    public void AttackCue(BattlePawn3D? pawn, string value, Color color)
    {
        if (pawn is null) return;
        Float(pawn, $"【{value}】", color, true, 3.45f);
    }

    /// <summary>
    /// ダメージの数字（第124期 3-b: 「もっと大きくわかりやすく」への直答）。
    ///
    /// <para><b>数字と出どころを別の大きさで出す。</b> 1行に混ぜると、読みたい数字が
    /// 駒名の長さに埋もれる（第123期の「ポップアップが小さい」の実体）。
    /// 数字は 2 段大きく、出どころはその下に小さく置く。</para>
    /// </summary>
    /// <param name="withSource">
    /// 出どころの札を添えるか（第124期 3-a）。<b>同時着弾では主目標だけに添える</b>
    /// ——5体ぶんの駒名が一度に浮くと、**大きくした数字がまた読めなくなる。**
    /// </param>
    public void DamagePopup(BattlePawn3D? pawn, int amount, string source, Color color,
                            bool large = false, bool withSource = true)
    {
        if (pawn is null) return;
        Float(pawn, $"−{amount}", color, true, 3.20f, large ? 2.05f : 1.55f);
        if (withSource) Float(pawn, source, color, false, 2.86f, 0.86f);
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
        Vector3 center = pawn.Home + new Vector3(0, 1.15f, 0);
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
        Float(from, label, color, false, 3.55f, 0.92f);
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

    private void Float(BattlePawn3D? pawn, string value, Color color, bool large, float height, float scale = 1.0f)
    {
        if (pawn is null) return;
        var label = new Label3D
        {
            Text = value,
            Position = pawn.Home + new Vector3(0, height, 0),
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
        var tween = label.CreateTween().SetParallel();
        tween.TweenProperty(label, "position:y", height + pawn.Home.Y + 0.95f, 0.78)
            .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(label, "modulate:a", 0.0f, 0.78).SetDelay(0.22);
        tween.Finished += label.QueueFree;
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

    private void MakeSweepFan(Vector3 origin, Vector3 target, Color color)
    {
        Vector3 forward = target - origin;
        forward.Y = 0;
        if (forward.LengthSquared() < 0.001f) return;
        forward = forward.Normalized();
        for (int i = -3; i <= 3; i++)
        {
            Vector3 ray = forward.Rotated(Vector3.Up, i * 0.13f);
            MakeBeam(origin, origin + ray * (3.6f - Math.Abs(i) * 0.14f), new Color(color, 0.48f), 0.055f, 0.28 + Math.Abs(i) * 0.018);
        }
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
        var tween = _camera.CreateTween();
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

    private void AddHill(Vector3 position, Vector3 scale, string color)
    {
        _world.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1.0f, Height = 2.0f, RadialSegments = 28, Rings = 10 },
            Position = position,
            Scale = scale,
            MaterialOverride = MakeMaterial(Color.FromHtml(color), roughness: 1.0f),
        });
    }

    private void AddTree(Vector3 position, Material trunk, Material leaves, float scale)
    {
        var root = new Node3D { Position = position, Scale = Vector3.One * scale };
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.10f, BottomRadius = 0.15f, Height = 1.2f, RadialSegments = 8 },
            Position = new Vector3(0, 0.6f, 0),
            MaterialOverride = trunk,
        });
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.08f, BottomRadius = 0.82f, Height = 1.7f, RadialSegments = 12 },
            Position = new Vector3(0, 1.65f, 0),
            MaterialOverride = leaves,
        });
        _world.AddChild(root);
    }

    private void AddStrip(Vector3 start, Vector3 end, float width, Material material)
    {
        Vector3 delta = end - start;
        var strip = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(width, 0.018f, 1.0f) },
            Position = (start + end) * 0.5f,
            Scale = new Vector3(1, 1, delta.Length()),
            MaterialOverride = material,
        };
        strip.Rotation = new Vector3(0, Mathf.Atan2(delta.X, delta.Z), 0);
        _world.AddChild(strip);
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
