using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// 表示だけを比べる試着室。戦闘やダメージ判定は行わない。
public partial class AssetPreview : Control
{
    private const string Nature = "res://PolyBlocks/NatureBlocks/";
    private const string Effects = "res://PolyBlocks/EffectBlocks/assets/";
    private BattlefieldView3D _field = null!;
    private Node3D _nature = null!;
    private Node3D _hybrid = null!;
    private Node3D? _meadow;
    private readonly List<AssetBurnPreview> _burnPreviews = new();
    private Label _status = null!;
    private int _background = 3;
    private bool _effects = true, _paused, _burnMode = true, _burnOn = true, _allEnemies;
    private bool _capturing;
    private bool _hotaMode;
    private bool _pierceMode = true;
    private int _pierceLevel = 2;
    private int _pierceVariant = 2;
    private readonly Dictionary<int, Vector3> _originalHomes = new();
    private readonly List<int> _previewContacts = new();
    private int _previewGeneration;
    private double _elapsed;
    private double _speed = 1;
    private Button _backgroundButton = null!, _effectsButton = null!, _pauseButton = null!, _speedButton = null!;
    private Button _modeButton = null!, _burnButton = null!, _targetsButton = null!;

    public override async void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        GetWindow().Title = "VillainProto 素材比較 — ホタの炎貫通・着弾";
        try
        {
            if (!ResourceLoader.Exists(Nature + "assets/trees/generic/forest/nb_tree1_forest.tscn") ||
                !ResourceLoader.Exists(Effects + "fire/fire_light.tscn"))
                throw new InvalidOperationException("購入素材がありません。DemoApp/PrepareAssetPreview.ps1 を実行し、Godotでインポートしてください。");
            _field = new BattlefieldView3D { ProcessMode = ProcessModeEnum.Pausable, LegacySceneryForComparison = true };
            AddChild(_field);
            _field.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _field.OffsetTop = 124;
            _field.BeginBattle(new DemoOpening[] {
                new(1,0,"hota","ホタ",0,100,100,20,AttackPattern.Single,false),
                new(2,0,"borg","ボルグ",2,100,100,20,AttackPattern.Sweep,true),
                new(3,0,"hiyo","ヒヨ",4,100,100,20,AttackPattern.Single,false),
                new(4,1,"knight","騎士",0,100,100,20,AttackPattern.Single,true),
                new(5,1,"knight","騎士",2,100,100,20,AttackPattern.Single,true),
                new(6,1,"knight","騎士",4,100,100,20,AttackPattern.Single,true)
            }, "素材比較", 0);
            foreach (var entry in _field.Pawns) _originalHomes[entry.Key] = entry.Value.Home;
            _allEnemies = true;
            _field.AttackContact = pawn => {
                _previewContacts.Add(pawn.InstanceId);
                pawn.AnimateHit(recoil: _pierceMode && _pierceLevel == 3 ? 1.25f : 0.7f);
            };
            _nature = BuildNature(false);
            _hybrid = BuildNature(true);
            _field.SetAssetPreviewScenery(_nature, 0);
            _field.SetAssetPreviewScenery(_hybrid, 0);
            ApplyBackground();
            _field.SetCameraElevation(12);
            BuildControls();
            UpdateLabels();
            bool capture = OS.GetCmdlineUserArgs().Any(arg => arg is "--hota-capture" or "--asset-capture" or "--pierce-capture");
            if (!capture) RestartEffects();
            GD.Print("ASSET_PREVIEW_READY");
            if (OS.GetCmdlineUserArgs().Contains("--pierce-capture")) await CapturePierce();
            else if (OS.GetCmdlineUserArgs().Contains("--hota-capture")) { _pierceMode = false; _hotaMode = true; await CaptureHota(); }
            else if (OS.GetCmdlineUserArgs().Contains("--asset-capture")) { _pierceMode = false; _hotaMode = false; await CaptureComparison(); }
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            AddChild(new Label { Text = e.Message, Position = new Vector2(24, 24) });
            SetProcess(false);
            if (OS.GetCmdlineUserArgs().Any(arg => arg.EndsWith("-capture"))) GetTree().Quit(1);
        }
    }

    private static Node3D LoadScene(string path) => GD.Load<PackedScene>(path).Instantiate<Node3D>();

    private void ApplyBackground()
    {
        if (_background == 3) _meadow ??= new NatureMeadowPreview();
        _field.SetAssetPreviewScenery(_background switch { 3 => _meadow!, 2 => _hybrid, _ => _nature }, _background);
    }

    private Node3D BuildNature(bool hybrid)
    {
        var root = new Node3D { Name = "NatureBlocksPreview" };
        if (hybrid) root.AddChild(new MeadowEnvironment3D { IncludeWoodland = false });
        else
        {
            var material = (StandardMaterial3D)GD.Load<Material>(Nature + "source_files/materials/terrain/nbmat_terrain_verdant.tres").Duplicate();
            root.AddChild(new MeshInstance3D {
                Mesh = new PlaneMesh { Size = new Vector2(90, 90) },
                Position = new Vector3(0, -0.025f, 0), MaterialOverride = material
            });
        }
        void Place(string relative, Vector3 point, float scale, float angle)
        {
            var node = LoadScene(Nature + "assets/" + relative);
            node.Position = point + Vector3.Up * (hybrid ? MeadowEnvironment3D.Height(point.X, point.Z) : 0);
            node.Scale = Vector3.One * scale;
            node.RotationDegrees = new Vector3(0, angle, 0);
            root.AddChild(node);
        }
        // 戦闘域の中央を空け、奥と側面に素材を置く。
        for (int i = 0; i < 11; i++)
        {
            float x = -17 + i * 3.4f;
            Place($"trees/generic/forest/nb_tree{i % 3 + 1}_forest.tscn",
                new Vector3(x, 0, -9 - i % 3 * 2), 0.8f + i % 3 * 0.12f, i * 47);
        }
        var random = new Random(7319);
        for (int i = 0; i < 65; i++)
        {
            float x = (float)random.NextDouble() * 34 - 17;
            float z = (float)random.NextDouble() * 19 - 15;
            if (Math.Abs(x) < 7.2f && z > -4.5f) continue;
            Place("vegetation/shrubs/forest/nb_shrub1_forest.tscn", new Vector3(x, 0, z), 0.45f + (float)random.NextDouble() * 0.45f, i * 73);
            if (i % 4 == 0)
                Place($"ground/rocks/verdant/nb_rock{i % 6 + 1}_verdant.tscn", new Vector3(x + 0.6f, 0, z), 0.5f, i * 31);
        }
        for (int i = 0; !hybrid && i < 130; i++)
        {
            float x = (float)random.NextDouble() * 30 - 15;
            float z = (float)random.NextDouble() * 20 - 14;
            if (Math.Abs(x) < 7 && z > -4) continue;
            Place("vegetation/grass/verdant/nb_grass1.1_verdant.tscn", new Vector3(x, 0, z), 0.6f, i * 137);
        }
        return root;
    }

    private void BuildControls()
    {
        var panel = new ColorRect { Color = new Color("111c21"), Size = new Vector2(3000, 124) };
        AddChild(panel);
        var row = new GridContainer { Columns = 5, Position = new Vector2(18, 10) };
        AddChild(row);
        Button Button(string text, Action action)
        {
            var button = new Button { Text = text, CustomMinimumSize = new Vector2(160, 34), FocusMode = FocusModeEnum.None };
            button.Pressed += action;
            row.AddChild(button);
            return button;
        }
        _backgroundButton = Button("", () => { _background = (_background + 1) % 4; ApplyBackground(); UpdateLabels(); });
        _effectsButton = Button("", () => {
            if (_pierceMode) _pierceVariant = (_pierceVariant + 1) % 3;
            else _effects = !_effects;
            RestartEffects(); UpdateLabels();
        });
        Button("もう一度再生", RestartEffects);
        _pauseButton = Button("", TogglePause);
        _speedButton = Button("", () => { _speed = _speed == 1 ? 2 : _speed == 2 ? 0.25 : 1; Engine.TimeScale = _speed; UpdateLabels(); });
        _modeButton = Button("", () => {
            if (_pierceMode) { _pierceMode = false; _hotaMode = true; }
            else if (_hotaMode) { _hotaMode = false; _burnMode = true; }
            else if (_burnMode) _burnMode = false;
            else _pierceMode = true;
            RestartEffects(); UpdateLabels();
        });
        _burnButton = Button("", () => { if (_pierceMode) _pierceLevel = _pierceLevel == 2 ? 3 : 2; else _burnOn = !_burnOn; RestartEffects(); UpdateLabels(); });
        _targetsButton = Button("", () => { _allEnemies = !_allEnemies; RestartEffects(); UpdateLabels(); });
        _status = new Label { Position = new Vector2(20, 94) };
        AddChild(_status);
    }

    private void UpdateLabels()
    {
        _backgroundButton.Text = _background switch { 3 => "背景 D：青空の草原", 1 => "背景 B：購入・平面", 2 => "背景 C：起伏＋購入植生", _ => "背景 A：既存" };
        _effectsButton.Text = _pierceMode ? (_pierceVariant switch {
            2 => "効果 C：火槍＋炎の衣", 1 => "効果 B：炎流＋爆炎", _ => "効果 A：従来の火槍" })
            : _hotaMode ? (_effects ? "効果 B：火球＋爆炎" : "効果 A：従来の光弾")
            : _effects ? "効果 B：EffectBlocks" : "効果 A：既存の炎";
        _pauseButton.Text = _paused ? "再開" : "一時停止";
        _speedButton.Text = $"速度：{_speed}倍";
        _modeButton.Text = _pierceMode ? "表示：ホタの炎貫通" : _hotaMode ? "表示：ホタの5連撃" : _burnMode ? "表示：敵の燃焼状態" : "表示：短い炎・着弾";
        _burnButton.Text = _pierceMode ? $"火勢：{_pierceLevel}（{(_pierceLevel == 3 ? "強化" : "通常")}）" : _burnOn ? "燃焼：あり" : "燃焼：なし";
        _burnButton.Disabled = !_pierceMode && (_hotaMode || !_burnMode);
        _targetsButton.Text = _allEnemies ? "対象：敵3体" : "対象：敵1体";
        _targetsButton.Disabled = !_pierceMode && (_hotaMode || !_burnMode);
    }

    private void TogglePause()
    {
        _paused = !_paused;
        GetTree().Paused = _paused;
        UpdateLabels();
    }

    public override void _Process(double delta)
    {
        if (_status == null) return;
        _status.Text = (_pierceMode ? "炎の貫通＋敵ごとの着弾・被弾 / 火勢2・3／敵1体・3体／A・B・C比較（約5秒周期）"
            : _hotaMode ? "ホタの残り火5連撃 / A・Bで比較 / 0.25倍で弾と爆炎を確認（約5秒周期）"
            : _burnMode ? "敵の燃焼を継続表示 / 燃焼あり・なし／1体・3体で見やすさを比較" : "短い炎＋着弾（約5秒周期）")
            + $" / FPS {Engine.GetFramesPerSecond()}（参考値） / 本編には未適用";
        if (_paused || _capturing || (!_pierceMode && !_hotaMode && _burnMode)) return;
        _elapsed += delta;
        if (_elapsed >= 5) RestartEffects();
    }

    private void RestartEffects()
    {
        if (_paused) TogglePause();
        _previewGeneration++;
        _field.CancelAssetPreviewAttack();
        _previewContacts.Clear();
        _field.AssetPreviewFirePierceVariant = _pierceMode ? _pierceVariant : 0;
        foreach (Node node in _field.AssetPreviewEffects.GetChildren())
        {
            _field.AssetPreviewEffects.RemoveChild(node);
            node.QueueFree();
        }
        _elapsed = 0;
        foreach (var preview in _burnPreviews)
        {
            preview.GetParent().RemoveChild(preview);
            preview.QueueFree();
        }
        _burnPreviews.Clear();
        foreach (var pawn in _field.Pawns.Values) pawn.SetAssetPreviewBurning(false, false);
        foreach (var entry in _originalHomes) _field.FindPawn(entry.Key)!.ResetAssetPreviewHome(entry.Value);
        if (_pierceMode)
        {
            Vector3 from = _originalHomes[1], far = _originalHomes[6];
            for (int i = 0; i < 3; i++)
                _field.FindPawn(4 + i)!.SetHome(from.Lerp(far, 0.55f + i * 0.225f));
        }
        EmitEffects();
    }

    private void EmitEffects()
    {
        if (_pierceMode) { _ = PlayHotaPierce(); return; }
        if (_hotaMode) { _ = PlayHotaChain(_previewGeneration); return; }
        if (_burnMode)
        {
            if (!_burnOn) return;
            foreach (int id in _allEnemies ? new[] { 4, 5, 6 } : new[] { 4 })
            {
                var pawn = _field.FindPawn(id)!;
                pawn.SetAssetPreviewBurning(true, _effects);
                if (!_effects) continue;
                var preview = new AssetBurnPreview();
                pawn.AddChild(preview);
                _burnPreviews.Add(preview);
            }
            return;
        }
        Node3D root = _field.AssetPreviewEffects;
        Vector3 fire = _field.FindPawn(4)!.GlobalPosition + new Vector3(0, 0.3f, 0.3f);
        // ビルボードの背面に埋もれないよう、胴体の手前に着弾を置く。
        Vector3 impact = _field.FindPawn(5)!.GlobalPosition + new Vector3(0, 1.2f, 0.9f);
        if (!_effects)
        {
            FireFx.Bloom(root, fire + Vector3.Up * 0.5f, FireFx.ColorOf(2), 1.8f, 1.2, mode: 0);
            FireFx.Bloom(root, impact, FireFx.ColorOf(2), 1.5f, 0.5);
            FireFx.Light(root, impact, FireFx.ColorOf(2), 1.5f, 0.5);
            return;
        }
        SpawnEffect("fire/fire_light.tscn", fire, 0.60f, 4.3);
        SpawnEffect("impacts/impact_1.tscn", impact, 0.55f, 1.2);
    }

    private void SpawnEffect(string path, Vector3 position, float scale, double lifetime)
    {
        var node = LoadScene(Effects + path);
        // 作者デモのSpace入力・自動反復を外し、この画面の再生制御へ揃える。
        void Prepare(Node child)
        {
            child.SetScript(default);
            if (child is GpuParticles3D particles)
            {
                particles.Emitting = true;
                particles.UseFixedSeed = true;
                particles.Seed = 7319;
            }
            foreach (Node nested in child.GetChildren()) Prepare(nested);
        }
        Prepare(node);
        node.Position = position;
        node.Scale = Vector3.One * scale;
        _field.AssetPreviewEffects.AddChild(node);
        void Fade(Node child)
        {
            if (child is Light3D light)
            {
                var fade = light.CreateTween();
                if (lifetime > 2) fade.TweenInterval(1.0);
                fade.TweenProperty(light, "light_energy", 0f, 0.5);
            }
            if (child is GpuParticles3D particles && !particles.OneShot)
            {
                var emission = particles.CreateTween();
                emission.TweenInterval(1.0);
                emission.TweenCallback(Callable.From(() => particles.Emitting = false));
            }
            foreach (Node nested in child.GetChildren()) Fade(nested);
        }
        Fade(node);
        var tween = node.CreateTween();
        tween.TweenInterval(lifetime);
        tween.TweenCallback(Callable.From(node.QueueFree));
    }

    private async Task CaptureComparison()
    {
        _capturing = true;
        string directory = ProjectSettings.GlobalizePath("res://../.tmp/asset-preview-captures");
        System.IO.Directory.CreateDirectory(directory);
        // 初回のシェーダーコンパイルが済んでから同じ時刻の組を保存する。
        await ToSignal(GetTree().CreateTimer(3), SceneTreeTimer.SignalName.Timeout);
        _burnMode = false;
        foreach (int background in new[] { 0, 1, 2, 3 })
        foreach (bool effects in new[] { false, true })
        {
            _background = background; _effects = effects;
            ApplyBackground();
            UpdateLabels();
            RestartEffects();
            await ToSignal(GetTree().CreateTimer(0.18), SceneTreeTimer.SignalName.Timeout);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            string path = $"{directory}/background-{(char)('A' + background)}-effect-{(effects ? "B" : "A")}.png";
            var error = GetViewport().GetTexture().GetImage().SavePng(path);
            if (error != Error.Ok) throw new InvalidOperationException($"画像保存失敗: {error}");
        }
        // 倍速でも消滅まで進み、再生成後に前回の粒子が残らないことを確認する。
        Engine.TimeScale = 2;
        RestartEffects();
        await ToSignal(GetTree().CreateTimer(4.6), SceneTreeTimer.SignalName.Timeout);
        if (_field.AssetPreviewEffects.GetChildCount() != 0)
            throw new InvalidOperationException("エフェクトの後片付けに失敗しました。");
        Engine.TimeScale = 1;
        _burnMode = true;
        _allEnemies = true;
        foreach (bool effects in new[] { false, true })
        {
            _effects = effects;
            RestartEffects();
            UpdateLabels();
            await ToSignal(GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
            await SaveCapture(directory + $"/meadow-D-burning-{(effects ? "B" : "A")}.png");
        }
        _burnOn = false;
        RestartEffects();
        UpdateLabels();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_field.Pawns.Values.Any(p => p.FireLevel != 0 || p.GetChildren().Any(n => n is AssetBurnPreview)))
            throw new InvalidOperationException("燃焼解除後に表示が残っています。");
        await SaveCapture(directory + "/meadow-D.png");
        _field.SetCameraElevation(30);
        await ToSignal(GetTree().CreateTimer(0.3), SceneTreeTimer.SignalName.Timeout);
        await SaveCapture(directory + "/meadow-D-overhead.png");
        _background = 2;
        ApplyBackground();
        UpdateLabels();
        await SaveCapture(directory + "/meadow-C-overhead.png");
        _background = 3;
        ApplyBackground();
        _field.SetCameraElevation(12);
        _burnOn = true;
        RestartEffects();
        UpdateLabels();
        await ToSignal(GetTree().CreateTimer(0.9), SceneTreeTimer.SignalName.Timeout);
        TogglePause();
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(directory + "/purchased-paused.png");
        // 停止中も操作UIが使える。風のTIMEシェーダーは停止対象外。
        await ToSignal(GetTree().CreateTimer(0.3, processAlways: true), SceneTreeTimer.SignalName.Timeout);
        TogglePause();
        GD.Print("ASSET_PREVIEW_CAPTURE_COMPLETE");
        GetTree().Quit();
    }

    private async Task SaveCapture(string path)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(path);
        if (error != Error.Ok) throw new InvalidOperationException($"画像保存失敗: {error}");
    }

    public override void _ExitTree()
    {
        Engine.TimeScale = 1;
        GetTree().Paused = false;
    }
}
