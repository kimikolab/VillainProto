using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class CampaignMain : Node3D
{
    private static readonly Color PlayerColor = Color.FromHtml("#55d5b8");
    private static readonly Color EnemyColor = Color.FromHtml("#e96951");
    private static readonly Color NeutralColor = Color.FromHtml("#b4ae91");
    private static readonly Color GoldColor = Color.FromHtml("#f1c96b");
    private static readonly Vector2 MapMin = new(-23.0f, -14.0f);
    private static readonly Vector2 MapMax = new(23.0f, 14.0f);

    private readonly Dictionary<string, CampaignSquad> _squads = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BaseSite> _bases = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _squadButtons = new(StringComparer.Ordinal);

    private Camera3D _camera = null!;
    private CampaignSquad? _selected;
    private CampaignSquad? _pendingPlayer;
    private CampaignSquad? _pendingEnemy;
    private Label _selectionLabel = null!;
    private Label _orderLabel = null!;
    private Label _objectiveLabel = null!;
    private VBoxContainer _baseList = null!;
    private Label _toast = null!;
    private ColorRect _encounterOverlay = null!;
    private Label _encounterTitle = null!;
    private Label _encounterBody = null!;
    private Button _pauseButton = null!;
    private OptionButton _timePicker = null!;
    private Label _timeBadge = null!;
    private MeshInstance3D _orderLine = null!;
    private MeshInstance3D _orderMarker = null!;

    private Vector3 _cameraTarget = new(0, 0, 0);
    private float _cameraYaw = -0.22f;
    private float _cameraDistance = 29.0f;
    private float _cameraHeight = 20.5f;
    private float _enemyThink;
    private float _contactCooldown;
    private float _saveClock;
    private float _toastClock;
    private float _commandSlowClock;
    private bool _paused;
    private bool _encounterOpen;
    private double _strategicSpeed = 1.0;
    private readonly HashSet<string> _warnedContacts = new(StringComparer.Ordinal);

    private double EffectiveTimeScale => _paused ? 0.0 : _strategicSpeed * (_commandSlowClock > 0 ? 0.30 : 1.0);

    private sealed class BaseSite
    {
        public required string Id { get; init; }
        public required string Name { get; init; }
        public required Vector3 Position { get; init; }
        public required MeshInstance3D Flag { get; init; }
        public required MeshInstance3D Ring { get; init; }
        public required Label3D Label { get; init; }
        public int Owner { get; set; }
        public int CapturingTeam { get; set; } = -2;
        public float CaptureProgress { get; set; }
    }

    public override void _Ready()
    {
        CampaignSession.EnsureInitialized();
        BuildEnvironment();
        BuildTerrain();
        BuildBases();
        BuildOrderIndicator();
        BuildUi();
        BuildSquads();
        UpdateCamera();
        UpdateAllUi();

        string? outcome = CampaignSession.ConsumeOutcome();
        if (!string.IsNullOrEmpty(outcome))
            ShowToast(outcome, CampaignSession.LastPlayerWon == true ? PlayerColor : EnemyColor, 5.5f);
        else
            ShowToast("灰狼隊を選択し、右クリックで旧監視塔へ進軍してください", GoldColor, 6.0f);

        string[] args = OS.GetCmdlineUserArgs();
        string? captureArg = args.FirstOrDefault(arg => arg.StartsWith("--campaign-capture-dir=", StringComparison.Ordinal));
        if (captureArg is not null)
            _ = RunCampaignVisualCapture(captureArg["--campaign-capture-dir=".Length..]);
        else if (args.Contains("--campaign-flow-smoke", StringComparer.Ordinal))
        {
            if (CampaignSession.FlowSmokeReturning)
            {
                CampaignSession.FlowSmokeReturning = false;
                GD.Print($"CAMPAIGN_FLOW_SMOKE_COMPLETE won={CampaignSession.LastPlayerWon} remaining_enemies={_squads.Values.Count(s => s.Team == 1)}");
                GetTree().Quit();
            }
            else
            {
                _ = RunFlowSmoke();
            }
        }
        else if (args.Contains("--campaign-time-smoke", StringComparer.Ordinal))
            _ = RunTimeControlSmoke();
        else if (args.Contains("--campaign-capture-smoke", StringComparer.Ordinal))
            _ = RunCaptureSmoke();
        else if (args.Contains("--campaign-smoke", StringComparer.Ordinal))
            _ = RunSmoke();
    }

    private void BuildEnvironment()
    {
        var skyMaterial = new ProceduralSkyMaterial
        {
            SkyTopColor = Color.FromHtml("#718b91"),
            SkyHorizonColor = Color.FromHtml("#d5c9a4"),
            GroundBottomColor = Color.FromHtml("#253a32"),
            GroundHorizonColor = Color.FromHtml("#aaa580"),
            SunAngleMax = 18.0f,
        };
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = skyMaterial },
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = Color.FromHtml("#a7beb3"),
            AmbientLightEnergy = 0.72f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            FogEnabled = true,
            FogLightColor = Color.FromHtml("#c9c3a4"),
            FogLightEnergy = 0.55f,
            FogDensity = 0.0022f,
            VolumetricFogEnabled = true,
            VolumetricFogDensity = 0.0025f,
            VolumetricFogLength = 42.0f,
        };
        AddChild(new WorldEnvironment { Environment = environment });

        var sun = new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-56, -28, 0),
            LightColor = Color.FromHtml("#ffe8b3"),
            LightEnergy = 1.25f,
            ShadowEnabled = true,
        };
        AddChild(sun);

        _camera = new Camera3D
        {
            Current = true,
            Fov = 38.0f,
            Near = 0.15f,
            Far = 120.0f,
        };
        AddChild(_camera);
    }

    private void BuildTerrain()
    {
        var ground = new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = new Vector2(54, 35), SubdivideWidth = 12, SubdivideDepth = 8 },
            MaterialOverride = MakeMaterial(Color.FromHtml("#557343"), roughness: 0.96f),
        };
        AddChild(ground);

        AddHill(new Vector3(-19, -1.9f, -10), new Vector3(7.5f, 2.2f, 5.2f), "#425f38");
        AddHill(new Vector3(-5, -2.2f, 13), new Vector3(9.0f, 2.0f, 4.6f), "#49663b");
        AddHill(new Vector3(18, -1.8f, 10), new Vector3(7.0f, 2.1f, 5.0f), "#3c5936");
        AddHill(new Vector3(20, -2.5f, -11), new Vector3(8.0f, 2.0f, 4.0f), "#445f36");

        StandardMaterial3D road = MakeMaterial(Color.FromHtml("#8b7756"), roughness: 1.0f);
        AddStrip(new Vector3(-17, 0.018f, 4), new Vector3(-2, 0.018f, 0), 1.65f, road);
        AddStrip(new Vector3(-2, 0.018f, 0), new Vector3(5.5f, 0.018f, -1.8f), 1.55f, road);
        AddStrip(new Vector3(5.5f, 0.018f, -1.8f), new Vector3(17, 0.018f, -3.5f), 1.55f, road);
        AddStrip(new Vector3(-2, 0.02f, 0), new Vector3(2.0f, 0.02f, 8.2f), 1.2f, road);

        StandardMaterial3D water = MakeMaterial(new Color(0.18f, 0.40f, 0.46f, 0.82f), transparent: true, roughness: 0.2f, emission: new Color(0.05f, 0.14f, 0.16f));
        AddStrip(new Vector3(-24, 0.035f, -5.5f), new Vector3(-9, 0.035f, -6.6f), 2.2f, water);
        AddStrip(new Vector3(-9, 0.035f, -6.6f), new Vector3(5, 0.035f, -5.4f), 2.2f, water);
        AddStrip(new Vector3(5, 0.035f, -5.4f), new Vector3(24, 0.035f, -8.4f), 2.3f, water);

        StandardMaterial3D trunk = MakeMaterial(Color.FromHtml("#59462e"));
        StandardMaterial3D leafA = MakeMaterial(Color.FromHtml("#274f37"));
        StandardMaterial3D leafB = MakeMaterial(Color.FromHtml("#375f3c"));
        Vector3[] trees =
        {
            new(-21,0,-11), new(-18,0,-9), new(-14,0,-12), new(-10,0,11), new(-6,0,12),
            new(0,0,12), new(8,0,11), new(13,0,12), new(18,0,9), new(21,0,11),
            new(-22,0,9), new(-20,0,11), new(20,0,-11), new(16,0,-12), new(12,0,-10),
            new(-3,0,-12), new(2,0,-11), new(7,0,-12), new(-22,0,-1), new(22,0,2),
        };
        for (int i = 0; i < trees.Length; i++) AddTree(trees[i], trunk, i % 2 == 0 ? leafA : leafB, 0.86f + (i % 4) * 0.08f);

        StandardMaterial3D rock = MakeMaterial(Color.FromHtml("#777868"), roughness: 0.93f);
        foreach ((Vector3 position, Vector3 scale) in new[]
        {
            (new Vector3(-8,0,-8), new Vector3(1.6f,0.7f,1.0f)),
            (new Vector3(10,0,7), new Vector3(1.2f,0.55f,0.9f)),
            (new Vector3(14,0,-10), new Vector3(1.7f,0.65f,1.1f)),
            (new Vector3(-13,0,9), new Vector3(1.0f,0.5f,0.75f)),
        })
        {
            var stone = new MeshInstance3D { Mesh = new SphereMesh { Radius = 1, Height = 1.45f }, Position = position + Vector3.Up * 0.3f, Scale = scale, MaterialOverride = rock };
            AddChild(stone);
        }
    }

    private void AddHill(Vector3 position, Vector3 scale, string color)
    {
        var hill = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 1.0f, Height = 2.0f, RadialSegments = 32, Rings = 12 },
            Position = position,
            Scale = scale,
            MaterialOverride = MakeMaterial(Color.FromHtml(color), roughness: 1.0f),
        };
        AddChild(hill);
    }

    private void AddTree(Vector3 position, Material trunk, Material leaves, float scale)
    {
        var root = new Node3D { Position = position, Scale = Vector3.One * scale };
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.11f, BottomRadius = 0.16f, Height = 1.25f, RadialSegments = 8 },
            Position = new Vector3(0, 0.62f, 0),
            MaterialOverride = trunk,
        });
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.06f, BottomRadius = 0.72f, Height = 1.65f, RadialSegments = 12 },
            Position = new Vector3(0, 1.72f, 0),
            MaterialOverride = leaves,
        });
        AddChild(root);
    }

    private void AddStrip(Vector3 from, Vector3 to, float width, Material material)
    {
        Vector3 delta = to - from;
        var strip = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(width, 0.025f, delta.Length()) },
            Position = (from + to) * 0.5f,
            Rotation = new Vector3(0, Mathf.Atan2(delta.X, delta.Z), 0),
            MaterialOverride = material,
        };
        AddChild(strip);
    }

    private void BuildBases()
    {
        CreateBase("west-camp", "灰風の野営地", new Vector3(-17.2f, 0, 4.0f));
        CreateBase("old-watch", "旧監視塔", new Vector3(1.8f, 0, 7.8f));
        CreateBase("east-fort", "黒鉄砦", new Vector3(17.0f, 0, -3.4f));
    }

    private void CreateBase(string id, string name, Vector3 position)
    {
        int owner = CampaignSession.BaseOwners.GetValueOrDefault(id, -1);
        var root = new Node3D { Position = position };
        AddChild(root);

        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 2.0f, BottomRadius = 2.35f, Height = 0.55f, RadialSegments = 10 },
            Position = new Vector3(0, 0.25f, 0),
            MaterialOverride = MakeMaterial(Color.FromHtml("#686956"), roughness: 0.95f),
        });
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.6f, 2.25f, 1.55f) },
            Position = new Vector3(0, 1.45f, 0),
            MaterialOverride = MakeMaterial(Color.FromHtml("#6d6b5b"), roughness: 0.9f),
        });
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.95f, BottomRadius = 1.2f, Height = 0.9f, RadialSegments = 4 },
            Position = new Vector3(0, 3.0f, 0),
            RotationDegrees = new Vector3(0, 45, 0),
            MaterialOverride = MakeMaterial(Color.FromHtml("#4e5045"), roughness: 0.95f),
        });
        root.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.045f, BottomRadius = 0.055f, Height = 3.7f, RadialSegments = 8 },
            Position = new Vector3(1.05f, 2.15f, 0),
            MaterialOverride = MakeMaterial(Color.FromHtml("#3a3025")),
        });
        var flag = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(1.2f, 0.72f, 0.035f) },
            Position = new Vector3(1.64f, 3.42f, 0),
        };
        root.AddChild(flag);
        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 2.05f, OuterRadius = 2.22f, Rings = 36, RingSegments = 8 },
            Position = new Vector3(0, 0.08f, 0),
        };
        root.AddChild(ring);
        var label = new Label3D
        {
            Text = name,
            Position = new Vector3(0, 4.35f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            Font = new SystemFont { FontNames = new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP" }, AllowSystemFallback = true },
            FontSize = 28,
            PixelSize = 0.007f,
            OutlineSize = 8,
            OutlineModulate = new Color(0.01f, 0.015f, 0.012f, 0.94f),
            NoDepthTest = true,
        };
        root.AddChild(label);

        var site = new BaseSite { Id = id, Name = name, Position = position, Owner = owner, Flag = flag, Ring = ring, Label = label };
        _bases[id] = site;
        UpdateBaseVisual(site);
    }

    private void BuildSquads()
    {
        Texture2D playerTexture = UiKit.LoadTexture("res://assets/campaign_player_squad.png");
        Texture2D enemyTexture = UiKit.LoadTexture("res://assets/campaign_enemy_squad.png");
        AddSquad("grey-wolves", "灰狼隊", 0, 0, playerTexture);
        AddSquad("last-embers", "残火隊", 0, 2, playerTexture);
        AddSquad("iron-hunt", "鉄猟隊", 1, 0, enemyTexture);
        AddSquad("red-tusk", "赤牙隊", 1, 3, enemyTexture);
        SelectSquad("grey-wolves");
    }

    private void AddSquad(string id, string name, int team, int stageIndex, Texture2D texture)
    {
        if (team == 1 && CampaignSession.DefeatedEnemies.Contains(id)) return;
        var squad = new CampaignSquad();
        AddChild(squad);
        squad.Configure(id, name, team, stageIndex, texture, CampaignSession.PositionOf(id));
        _squads[id] = squad;
    }

    private void BuildOrderIndicator()
    {
        _orderLine = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.10f, 0.035f, 1.0f) },
            MaterialOverride = MakeMaterial(new Color(GoldColor, 0.68f), transparent: true, unshaded: true, emission: GoldColor * 0.35f),
            Visible = false,
        };
        AddChild(_orderLine);
        _orderMarker = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.55f, OuterRadius = 0.73f, Rings = 28, RingSegments = 8 },
            MaterialOverride = MakeMaterial(new Color(GoldColor, 0.9f), transparent: true, unshaded: true, emission: GoldColor),
            Visible = false,
        };
        AddChild(_orderMarker);
    }

    private void BuildUi()
    {
        var layer = new CanvasLayer();
        AddChild(layer);
        var root = new Control
        {
            Theme = new Theme
            {
                DefaultFont = new SystemFont { FontNames = new[] { "Yu Gothic UI", "Meiryo", "Noto Sans CJK JP", "Segoe UI" }, AllowSystemFallback = true },
                DefaultFontSize = 14,
            },
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(root);

        var vignette = new ColorRect { Color = new Color(0.01f, 0.018f, 0.015f, 0.20f), MouseFilter = Control.MouseFilterEnum.Ignore };
        vignette.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(vignette);

        var header = new PanelContainer
        {
            AnchorRight = 1,
            OffsetBottom = 86,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        header.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(0.025f, 0.045f, 0.038f, 0.94f), new Color(0.33f, 0.49f, 0.40f, 0.8f), 1, 0));
        root.AddChild(header);
        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 18);
        header.AddChild(headerRow);
        var brand = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        brand.AddThemeConstantOverride("separation", 0);
        brand.AddChild(UiKit.Text("2.5D CAMPAIGN PROTOTYPE", 11, GoldColor));
        brand.AddChild(UiKit.Text("灰原戦線 — 群狼の進軍", 25, Colors.White));
        headerRow.AddChild(brand);
        _objectiveLabel = UiKit.Text("", 12, Color.FromHtml("#d9dfd4"));
        _objectiveLabel.VerticalAlignment = VerticalAlignment.Center;
        _objectiveLabel.HorizontalAlignment = HorizontalAlignment.Right;
        _objectiveLabel.CustomMinimumSize = new Vector2(360, 0);
        headerRow.AddChild(_objectiveLabel);

        var timeCol = new VBoxContainer { CustomMinimumSize = new Vector2(150, 0) };
        timeCol.AddThemeConstantOverride("separation", 2);
        _timeBadge = UiKit.Text("戦術時間 ×1", 10, PlayerColor);
        _timeBadge.HorizontalAlignment = HorizontalAlignment.Center;
        timeCol.AddChild(_timeBadge);
        _timePicker = new OptionButton { CustomMinimumSize = new Vector2(150, 38), FocusMode = Control.FocusModeEnum.None };
        _timePicker.AddItem("停止", 0);
        _timePicker.AddItem("×0.5", 1);
        _timePicker.AddItem("×1", 2);
        _timePicker.AddItem("×2", 3);
        _timePicker.Selected = 2;
        _timePicker.ItemSelected += SetCampaignSpeed;
        timeCol.AddChild(_timePicker);
        headerRow.AddChild(timeCol);

        _pauseButton = UiKit.ActionButton("一時停止", GoldColor);
        _pauseButton.CustomMinimumSize = new Vector2(112, 42);
        _pauseButton.Pressed += TogglePause;
        headerRow.AddChild(_pauseButton);

        var squadPanel = MakePanel(new Color(0.025f, 0.05f, 0.042f, 0.94f));
        squadPanel.OffsetLeft = 22;
        squadPanel.OffsetTop = 112;
        squadPanel.OffsetRight = 310;
        squadPanel.OffsetBottom = 424;
        root.AddChild(squadPanel);
        var squadCol = new VBoxContainer();
        squadCol.AddThemeConstantOverride("separation", 9);
        squadPanel.AddChild(squadCol);
        squadCol.AddChild(UiKit.Text("味方部隊", 18, Colors.White));
        squadCol.AddChild(UiKit.Text("部隊を選択 → 地面を右クリックで移動", 10, UiKit.Muted));
        AddSquadButton(squadCol, "grey-wolves", "灰狼隊", "先遣 / 第1波対応");
        AddSquadButton(squadCol, "last-embers", "残火隊", "遊撃 / 第3波対応");
        squadCol.AddChild(new HSeparator());
        _selectionLabel = UiKit.Text("", 14, GoldColor);
        squadCol.AddChild(_selectionLabel);
        _orderLabel = UiKit.Text("", 11, UiKit.Muted);
        _orderLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        squadCol.AddChild(_orderLabel);

        var basePanel = MakePanel(new Color(0.025f, 0.05f, 0.042f, 0.94f));
        basePanel.AnchorLeft = 1;
        basePanel.AnchorRight = 1;
        basePanel.OffsetLeft = -310;
        basePanel.OffsetTop = 112;
        basePanel.OffsetRight = -22;
        basePanel.OffsetBottom = 414;
        root.AddChild(basePanel);
        var baseCol = new VBoxContainer();
        baseCol.AddThemeConstantOverride("separation", 8);
        basePanel.AddChild(baseCol);
        baseCol.AddChild(UiKit.Text("戦域拠点", 18, Colors.White));
        baseCol.AddChild(UiKit.Text("範囲内に3秒留まると制圧", 10, UiKit.Muted));
        baseCol.AddChild(new HSeparator());
        _baseList = new VBoxContainer();
        _baseList.AddThemeConstantOverride("separation", 7);
        baseCol.AddChild(_baseList);
        var battleOnly = UiKit.ActionButton("戦闘画面だけ試す", UiKit.Player);
        battleOnly.Pressed += GoToBattleOnly;
        baseCol.AddChild(battleOnly);
        var reset = UiKit.ActionButton("作戦をリセット");
        reset.Pressed += ResetCampaign;
        baseCol.AddChild(reset);

        var helpPanel = MakePanel(new Color(0.025f, 0.045f, 0.038f, 0.92f));
        helpPanel.AnchorTop = 1;
        helpPanel.AnchorRight = 1;
        helpPanel.AnchorBottom = 1;
        helpPanel.OffsetLeft = 22;
        helpPanel.OffsetTop = -94;
        helpPanel.OffsetRight = -22;
        helpPanel.OffsetBottom = -20;
        root.AddChild(helpPanel);
        var helpRow = new HBoxContainer();
        helpRow.AddThemeConstantOverride("separation", 16);
        helpPanel.AddChild(helpRow);
        var controls = UiKit.Text("左クリック: 部隊選択   右クリック: 移動命令   0:停止 / 1:×0.5 / 2:×1 / 3:×2   WASD/Q/E: カメラ", 11, UiKit.Muted);
        controls.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        controls.VerticalAlignment = VerticalAlignment.Center;
        helpRow.AddChild(controls);
        _toast = UiKit.Text("", 13, GoldColor);
        _toast.HorizontalAlignment = HorizontalAlignment.Right;
        _toast.VerticalAlignment = VerticalAlignment.Center;
        _toast.CustomMinimumSize = new Vector2(480, 0);
        helpRow.AddChild(_toast);

        BuildEncounterOverlay(root);
    }

    private static PanelContainer MakePanel(Color color)
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        panel.AddThemeStyleboxOverride("panel", UiKit.Box(color, new Color(0.30f, 0.46f, 0.37f, 0.78f), 1, 10));
        return panel;
    }

    private void AddSquadButton(VBoxContainer parent, string id, string name, string role)
    {
        var button = new Button
        {
            Text = $"{name}\n{role}",
            CustomMinimumSize = new Vector2(0, 62),
            Alignment = HorizontalAlignment.Left,
            FocusMode = Control.FocusModeEnum.None,
        };
        button.Pressed += () => SelectSquad(id);
        parent.AddChild(button);
        _squadButtons[id] = button;
    }

    private void BuildEncounterOverlay(Control root)
    {
        _encounterOverlay = new ColorRect
        {
            Color = new Color(0.005f, 0.01f, 0.008f, 0.76f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        _encounterOverlay.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(_encounterOverlay);
        var modal = MakePanel(new Color(0.035f, 0.055f, 0.047f, 0.985f));
        modal.AnchorLeft = 0.5f;
        modal.AnchorTop = 0.5f;
        modal.AnchorRight = 0.5f;
        modal.AnchorBottom = 0.5f;
        modal.OffsetLeft = -260;
        modal.OffsetTop = -170;
        modal.OffsetRight = 260;
        modal.OffsetBottom = 170;
        _encounterOverlay.AddChild(modal);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 12);
        modal.AddChild(col);
        col.AddChild(UiKit.Text("ENCOUNTER", 11, EnemyColor));
        _encounterTitle = UiKit.Text("敵部隊と接触", 26, Colors.White);
        col.AddChild(_encounterTitle);
        _encounterBody = UiKit.Text("", 13, Color.FromHtml("#ccd5cb"));
        _encounterBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _encounterBody.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        col.AddChild(_encounterBody);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        col.AddChild(row);
        var retreat = UiKit.ActionButton("いったん退く");
        retreat.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        retreat.Pressed += RetreatEncounter;
        row.AddChild(retreat);
        var fight = UiKit.ActionButton("編成して戦う", GoldColor);
        fight.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        fight.Pressed += StartEncounterBattle;
        row.AddChild(fight);
    }

    public override void _Process(double delta)
    {
        UpdateCameraInput(delta);
        if (_toastClock > 0)
        {
            _toastClock -= (float)delta;
            if (_toastClock <= 0) _toast.Text = "";
        }
        if (_commandSlowClock > 0) _commandSlowClock = Math.Max(0, _commandSlowClock - (float)delta);
        UpdateTimeBadge();
        if (_encounterOpen || EffectiveTimeScale <= 0.0) return;

        double simulationDelta = delta * EffectiveTimeScale;
        foreach (CampaignSquad squad in _squads.Values) squad.Step(simulationDelta, squad.Team == 1 ? 0.68f : 1.0f);
        _enemyThink -= (float)simulationDelta;
        if (_enemyThink <= 0)
        {
            _enemyThink = 1.0f;
            DirectEnemies();
        }
        UpdateCapture(simulationDelta);
        WarnApproachingEnemies();
        _contactCooldown = Math.Max(0, _contactCooldown - (float)simulationDelta);
        if (_contactCooldown <= 0) CheckEncounters();
        UpdateOrderIndicator();

        _saveClock += (float)simulationDelta;
        if (_saveClock >= 0.35f)
        {
            _saveClock = 0;
            SaveWorldState();
        }
    }

    private void UpdateCameraInput(double delta)
    {
        Vector3 right = new Vector3(1, 0, 0).Rotated(Vector3.Up, _cameraYaw);
        Vector3 forward = new Vector3(0, 0, -1).Rotated(Vector3.Up, _cameraYaw);
        Vector3 motion = Vector3.Zero;
        if (Input.IsKeyPressed(Key.A)) motion -= right;
        if (Input.IsKeyPressed(Key.D)) motion += right;
        if (Input.IsKeyPressed(Key.W)) motion += forward;
        if (Input.IsKeyPressed(Key.S)) motion -= forward;
        if (motion.LengthSquared() > 0)
        {
            _cameraTarget += motion.Normalized() * (float)delta * 10.0f;
            _cameraTarget.X = Math.Clamp(_cameraTarget.X, MapMin.X + 5, MapMax.X - 5);
            _cameraTarget.Z = Math.Clamp(_cameraTarget.Z, MapMin.Y + 4, MapMax.Y - 4);
        }
        if (Input.IsKeyPressed(Key.Q)) _cameraYaw -= (float)delta * 0.7f;
        if (Input.IsKeyPressed(Key.E)) _cameraYaw += (float)delta * 0.7f;
        UpdateCamera();
    }

    private void UpdateCamera()
    {
        Vector3 offset = new Vector3(0, _cameraHeight, _cameraDistance).Rotated(Vector3.Up, _cameraYaw);
        _camera.Position = _cameraTarget + offset;
        _camera.LookAt(_cameraTarget + new Vector3(0, 0.25f, 0), Vector3.Up);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouse && mouse.Pressed)
        {
            if (mouse.ButtonIndex == MouseButton.Left)
            {
                SelectSquadAt(mouse.Position);
                GetViewport().SetInputAsHandled();
            }
            else if (mouse.ButtonIndex == MouseButton.Right && TryGroundPoint(mouse.Position, out Vector3 point))
            {
                IssueOrder(point);
                GetViewport().SetInputAsHandled();
            }
            else if (mouse.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
            {
                float direction = mouse.ButtonIndex == MouseButton.WheelUp ? -1 : 1;
                _cameraDistance = Math.Clamp(_cameraDistance + direction * 2.3f, 18.0f, 38.0f);
                _cameraHeight = Math.Clamp(_cameraHeight + direction * 1.25f, 13.5f, 27.0f);
                UpdateCamera();
                GetViewport().SetInputAsHandled();
            }
        }
        else if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            switch (key.Keycode)
            {
                case Key.Space:
                    TogglePause();
                    break;
                case Key.Key0:
                    SetCampaignSpeed(0);
                    break;
                case Key.Key1:
                    SetCampaignSpeed(1);
                    break;
                case Key.Key2:
                    SetCampaignSpeed(2);
                    break;
                case Key.Key3:
                    SetCampaignSpeed(3);
                    break;
                default:
                    return;
            }
            GetViewport().SetInputAsHandled();
        }
    }

    private bool TryGroundPoint(Vector2 screen, out Vector3 point)
    {
        Vector3 origin = _camera.ProjectRayOrigin(screen);
        Vector3 direction = _camera.ProjectRayNormal(screen);
        if (Math.Abs(direction.Y) < 0.0001f)
        {
            point = Vector3.Zero;
            return false;
        }
        float distance = -origin.Y / direction.Y;
        if (distance <= 0)
        {
            point = Vector3.Zero;
            return false;
        }
        point = origin + direction * distance;
        point.X = Math.Clamp(point.X, MapMin.X, MapMax.X);
        point.Z = Math.Clamp(point.Z, MapMin.Y, MapMax.Y);
        point.Y = 0.08f;
        return true;
    }

    private void SelectSquadAt(Vector2 mouse)
    {
        CampaignSquad? closest = null;
        float best = 82.0f;
        foreach (CampaignSquad squad in _squads.Values.Where(s => s.Team == 0))
        {
            Vector2 screen = _camera.UnprojectPosition(squad.GlobalPosition + new Vector3(0, 2.2f, 0));
            float distance = screen.DistanceTo(mouse);
            if (distance >= best) continue;
            closest = squad;
            best = distance;
        }
        if (closest is not null) SelectSquad(closest.SquadId);
    }

    private void SelectSquad(string id)
    {
        if (!_squads.TryGetValue(id, out CampaignSquad? squad) || squad.Team != 0) return;
        _selected = squad;
        foreach (CampaignSquad item in _squads.Values) item.SetSelected(item == squad);
        foreach ((string buttonId, Button button) in _squadButtons)
            button.Modulate = buttonId == id ? new Color(0.72f, 1.0f, 0.90f) : Colors.White;
        BeginCommandSlow(1.35f);
        UpdateSelectionUi();
    }

    private void IssueOrder(Vector3 point)
    {
        if (_selected is null || _paused || _encounterOpen) return;
        _selected.SetDestination(point);
        BeginCommandSlow(2.0f);
        ShowToast($"{_selected.DisplayName}: 移動命令を受領", PlayerColor, 2.4f);
        UpdateSelectionUi();
        UpdateOrderIndicator();
    }

    private void DirectEnemies()
    {
        foreach (CampaignSquad enemy in _squads.Values.Where(s => s.Team == 1))
        {
            CampaignSquad? nearby = _squads.Values
                .Where(s => s.Team == 0)
                .OrderBy(s => s.Position.DistanceSquaredTo(enemy.Position))
                .FirstOrDefault();
            if (nearby is not null && nearby.Position.DistanceTo(enemy.Position) < 8.5f)
            {
                enemy.SetDestination(nearby.Position);
                continue;
            }
            BaseSite? target = _bases.Values
                .Where(b => b.Owner != 1)
                .OrderBy(b => b.Position.DistanceSquaredTo(enemy.Position))
                .FirstOrDefault();
            if (target is not null) enemy.SetDestination(target.Position);
        }
    }

    private void UpdateCapture(double delta)
    {
        bool changed = false;
        foreach (BaseSite site in _bases.Values)
        {
            bool player = _squads.Values.Any(s => s.Team == 0 && s.Position.DistanceTo(site.Position) <= 2.45f);
            bool enemy = _squads.Values.Any(s => s.Team == 1 && s.Position.DistanceTo(site.Position) <= 2.45f);
            int team = player == enemy ? -2 : player ? 0 : 1;
            if (team < 0 || team == site.Owner)
            {
                site.CapturingTeam = -2;
                site.CaptureProgress = Math.Max(0, site.CaptureProgress - (float)delta * 1.8f);
            }
            else
            {
                if (site.CapturingTeam != team)
                {
                    site.CapturingTeam = team;
                    site.CaptureProgress = 0;
                }
                site.CaptureProgress += (float)delta;
                if (site.CaptureProgress >= 3.0f)
                {
                    site.Owner = team;
                    site.CaptureProgress = 0;
                    site.CapturingTeam = -2;
                    CampaignSession.SaveBaseOwner(site.Id, team);
                    UpdateBaseVisual(site);
                    ShowToast($"{site.Name}を{(team == 0 ? "制圧" : "奪取されました")}", team == 0 ? PlayerColor : EnemyColor, 4.0f);
                    changed = true;
                }
            }
            float pulse = 1.0f + site.CaptureProgress / 3.0f * 0.18f;
            site.Ring.Scale = new Vector3(pulse, 1, pulse);
            site.Ring.Rotation = new Vector3(0, site.Ring.Rotation.Y + (float)delta * 0.25f, 0);
        }
        if (changed) UpdateAllUi();
    }

    private void WarnApproachingEnemies()
    {
        foreach (CampaignSquad player in _squads.Values.Where(s => s.Team == 0))
        foreach (CampaignSquad enemy in _squads.Values.Where(s => s.Team == 1))
        {
            string pair = $"{player.SquadId}|{enemy.SquadId}";
            float distance = player.Position.DistanceTo(enemy.Position);
            if (distance >= 7.0f)
            {
                _warnedContacts.Remove(pair);
                continue;
            }
            if (distance > 5.0f || !_warnedContacts.Add(pair)) continue;
            BeginCommandSlow(2.8f);
            ShowToast($"敵影接近 — {enemy.DisplayName}  距離 {distance:0.0}", EnemyColor, 2.8f);
        }
    }

    private void CheckEncounters()
    {
        foreach (CampaignSquad player in _squads.Values.Where(s => s.Team == 0))
        foreach (CampaignSquad enemy in _squads.Values.Where(s => s.Team == 1))
        {
            if (player.Position.DistanceTo(enemy.Position) > 1.75f) continue;
            OpenEncounter(player, enemy);
            return;
        }
    }

    private void OpenEncounter(CampaignSquad player, CampaignSquad enemy)
    {
        player.Stop();
        enemy.Stop();
        _pendingPlayer = player;
        _pendingEnemy = enemy;
        _encounterOpen = true;
        _encounterOverlay.Visible = true;
        string stageName = EnemyCatalog.Stages[enemy.StageIndex].Name;
        _encounterTitle.Text = $"{player.DisplayName} × {enemy.DisplayName}";
        _encounterBody.Text = $"{enemy.DisplayName}と接敵しました。敵編成は BattleCore の「{stageName}」です。\n\n戦闘画面でユニット5体と配置を自由に組み、勝敗を作戦マップへ持ち帰れます。";
        SaveWorldState();
    }

    private void RetreatEncounter()
    {
        if (_pendingPlayer is not null)
            _pendingPlayer.SetDestination(CampaignSession.HomeOf(_pendingPlayer.SquadId));
        if (_pendingEnemy is not null)
            _pendingEnemy.SetDestination(CampaignSession.HomeOf(_pendingEnemy.SquadId));
        _pendingPlayer = null;
        _pendingEnemy = null;
        _encounterOpen = false;
        _encounterOverlay.Visible = false;
        _contactCooldown = 4.0f;
        ShowToast("両部隊が距離を取りました", NeutralColor, 3.0f);
    }

    private void StartEncounterBattle()
    {
        if (_pendingPlayer is null || _pendingEnemy is null) return;
        SaveWorldState();
        CampaignSession.BeginEncounter(_pendingPlayer.SquadId, _pendingEnemy.SquadId, _pendingEnemy.DisplayName, _pendingEnemy.StageIndex);
        GetTree().ChangeSceneToFile(CampaignSession.BattleScene);
    }

    private void GoToBattleOnly()
    {
        SaveWorldState();
        GetTree().ChangeSceneToFile(CampaignSession.BattleScene);
    }

    private void TogglePause()
    {
        if (_encounterOpen) return;
        _paused = !_paused;
        _pauseButton.Text = _paused ? "再開" : "一時停止";
        _timePicker.Selected = _paused ? 0 : SpeedPickerIndex(_strategicSpeed);
        UpdateTimeBadge();
        ShowToast(_paused ? "作戦を一時停止しました" : "作戦を再開しました", _paused ? GoldColor : PlayerColor, 2.2f);
    }

    private void SetCampaignSpeed(long index)
    {
        if (_encounterOpen) return;
        if (index == 0)
        {
            _paused = true;
        }
        else
        {
            _strategicSpeed = index switch { 1 => 0.5, 3 => 2.0, _ => 1.0 };
            _paused = false;
        }
        _timePicker.Selected = (int)index;
        _pauseButton.Text = _paused ? "再開" : "一時停止";
        UpdateTimeBadge();
        ShowToast(_paused ? "戦術時間を停止" : $"戦術時間 ×{_strategicSpeed:0.#}", _paused ? GoldColor : PlayerColor, 1.8f);
    }

    private void BeginCommandSlow(float seconds)
    {
        if (_paused) return;
        _commandSlowClock = Math.Max(_commandSlowClock, seconds);
        UpdateTimeBadge();
    }

    private void UpdateTimeBadge()
    {
        if (_timeBadge is null) return;
        if (_paused)
        {
            _timeBadge.Text = "戦術時間 — 停止";
            _timeBadge.AddThemeColorOverride("font_color", GoldColor);
        }
        else if (_commandSlowClock > 0)
        {
            _timeBadge.Text = $"指揮スロー 30%  ·  実効×{EffectiveTimeScale:0.##}";
            _timeBadge.AddThemeColorOverride("font_color", GoldColor);
        }
        else
        {
            _timeBadge.Text = $"戦術時間 ×{_strategicSpeed:0.#}";
            _timeBadge.AddThemeColorOverride("font_color", PlayerColor);
        }
    }

    private static int SpeedPickerIndex(double speed)
        => speed switch { <= 0.5 => 1, >= 2.0 => 3, _ => 2 };

    private void ResetCampaign()
    {
        CampaignSession.ResetCampaign();
        GetTree().ReloadCurrentScene();
    }

    private void SaveWorldState()
    {
        foreach (CampaignSquad squad in _squads.Values)
            CampaignSession.SavePosition(squad.SquadId, squad.Position);
    }

    private void UpdateOrderIndicator()
    {
        if (_selected is null || !_selected.IsMoving)
        {
            _orderLine.Visible = false;
            _orderMarker.Visible = false;
            return;
        }
        Vector3 start = _selected.Position + new Vector3(0, 0.08f, 0);
        Vector3 end = _selected.Destination + new Vector3(0, 0.08f, 0);
        Vector3 delta = end - start;
        _orderLine.Visible = true;
        _orderLine.Position = (start + end) * 0.5f;
        _orderLine.Rotation = new Vector3(0, Mathf.Atan2(delta.X, delta.Z), 0);
        _orderLine.Scale = new Vector3(1, 1, Math.Max(0.05f, delta.Length()));
        _orderMarker.Visible = true;
        _orderMarker.Position = end;
        _orderMarker.Rotation = new Vector3(0, _orderMarker.Rotation.Y + 0.025f, 0);
        UpdateSelectionUi();
    }

    private void UpdateBaseVisual(BaseSite site)
    {
        Color color = TeamColor(site.Owner);
        site.Flag.MaterialOverride = MakeMaterial(color, roughness: 0.75f, emission: color * 0.18f);
        site.Ring.MaterialOverride = MakeMaterial(new Color(color, 0.85f), transparent: true, unshaded: true, emission: color * 0.6f);
        site.Label.Text = $"{site.Name}\n{OwnerLabel(site.Owner)}";
        site.Label.Modulate = color.Lightened(0.25f);
    }

    private void UpdateAllUi()
    {
        int playerBases = _bases.Values.Count(b => b.Owner == 0);
        int enemies = _squads.Values.Count(s => s.Team == 1);
        _objectiveLabel.Text = playerBases == _bases.Count
            ? "戦域制圧完了 — 敵残党を掃討せよ"
            : $"目標: 3拠点を制圧   自軍 {playerBases}/3   敵部隊 {enemies}";
        foreach (Node child in _baseList.GetChildren()) child.QueueFree();
        foreach (BaseSite site in _bases.Values)
        {
            var row = new HBoxContainer();
            var dot = UiKit.Text("◆", 14, TeamColor(site.Owner));
            row.AddChild(dot);
            var name = UiKit.Text(site.Name, 13, Colors.White);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            row.AddChild(name);
            row.AddChild(UiKit.Text(OwnerLabel(site.Owner), 11, TeamColor(site.Owner)));
            _baseList.AddChild(row);
        }
        UpdateSelectionUi();
    }

    private void UpdateSelectionUi()
    {
        if (_selected is null) return;
        _selectionLabel.Text = $"選択中: {_selected.DisplayName}";
        _orderLabel.Text = _selected.IsMoving
            ? $"進軍中  X {_selected.Destination.X:0.0} / Z {_selected.Destination.Z:0.0}"
            : "待機中 — 地形上を右クリックして命令";
    }

    private void ShowToast(string text, Color color, float seconds)
    {
        if (_toast is null) return;
        _toast.Text = text;
        _toast.AddThemeColorOverride("font_color", color);
        _toastClock = seconds;
    }

    private async Task RunSmoke()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        SelectSquad("grey-wolves");
        IssueOrder(new Vector3(-12, 0.08f, 5));
        await ToSignal(GetTree().CreateTimer(0.8), SceneTreeTimer.SignalName.Timeout);
        SaveWorldState();
        GD.Print($"CAMPAIGN_SMOKE_COMPLETE squads={_squads.Count} bases={_bases.Count} selected={_selected?.SquadId}");
        GetTree().Quit();
    }

    private async Task RunCampaignVisualCapture(string directory)
    {
        Directory.CreateDirectory(directory);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        SelectSquad("grey-wolves");
        IssueOrder(new Vector3(-10.5f, 0.08f, 3.8f));
        await ToSignal(GetTree().CreateTimer(0.32), SceneTreeTimer.SignalName.Timeout);
        string path = Path.Combine(directory, "campaign-2_5d-time.png");
        Error error = GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"CAMPAIGN_CAPTURE_COMPLETE error={error} path={path}");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    private async Task RunFlowSmoke()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!_squads.TryGetValue("grey-wolves", out CampaignSquad? player)
            || !_squads.TryGetValue("iron-hunt", out CampaignSquad? enemy))
            throw new InvalidOperationException("フロースモーク用の部隊が見つかりません");
        OpenEncounter(player, enemy);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        StartEncounterBattle();
    }

    private async Task RunTimeControlSmoke()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _paused = false;
        _strategicSpeed = 1.0;
        SelectSquad("grey-wolves");
        bool commandSlow = _commandSlowClock > 0 && Math.Abs(EffectiveTimeScale - 0.30) < 0.01;
        SetCampaignSpeed(3);
        bool scaledSlow = Math.Abs(EffectiveTimeScale - 0.60) < 0.01;
        GD.Print($"CAMPAIGN_TIME_SMOKE_COMPLETE command_slow={commandSlow} scaled_slow={scaledSlow} effective={EffectiveTimeScale:0.00}");
        GetTree().Quit(commandSlow && scaledSlow ? 0 : 1);
    }

    private async Task RunCaptureSmoke()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        _commandSlowClock = 0;
        _paused = false;
        _strategicSpeed = 1.0;
        foreach (CampaignSquad enemy in _squads.Values.Where(s => s.Team == 1)) enemy.Stop();
        _enemyThink = 999.0f;
        CampaignSquad player = _squads["grey-wolves"];
        BaseSite target = _bases["old-watch"];
        player.Position = target.Position + new Vector3(0, 0.08f, 0);
        player.Stop();
        await ToSignal(GetTree().CreateTimer(3.35), SceneTreeTimer.SignalName.Timeout);
        bool captured = target.Owner == 0;
        GD.Print($"CAMPAIGN_CAPTURE_SMOKE_COMPLETE captured={captured} owner={target.Owner}");
        GetTree().Quit(captured ? 0 : 1);
    }

    private static string OwnerLabel(int owner) => owner switch { 0 => "自軍", 1 => "敵軍", _ => "中立" };
    private static Color TeamColor(int owner) => owner switch { 0 => PlayerColor, 1 => EnemyColor, _ => NeutralColor };

    public static StandardMaterial3D MakeMaterial(Color color, bool transparent = false, bool unshaded = false, float roughness = 0.82f, Color? emission = null)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = roughness,
            Metallic = 0.02f,
        };
        if (transparent || color.A < 0.999f)
            material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        if (unshaded)
            material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        if (emission is { } glow)
        {
            material.EmissionEnabled = true;
            material.Emission = glow;
            material.EmissionEnergyMultiplier = 1.15f;
        }
        return material;
    }
}
