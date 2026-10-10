using Godot;

public partial class BattlefieldView3D
{
    private Godot.Environment? _assetPreviewOriginalEnvironment, _assetPreviewMeadowEnvironment;
    private Node3D CreateMeadowScenery() => !LegacySceneryForComparison && PurchasedAssets.NatureAvailable
        ? new NatureMeadowPreview() : new MeadowEnvironment3D();
    private void SetMeadowSky(bool meadow)
    {
        _assetPreviewOriginalEnvironment ??= _fireEnvironment ?? throw new System.InvalidOperationException("比較元の環境がありません。");
        if (meadow && _assetPreviewMeadowEnvironment == null)
        {
            var preset = GD.Load<PackedScene>("res://PolyBlocks/NatureBlocks/assets/environments/nb_env_cartoon.tscn").Instantiate<Node3D>();
            var environment = (Godot.Environment)_assetPreviewOriginalEnvironment.Duplicate();
            environment.Sky = (Sky)preset.GetNode<WorldEnvironment>("WorldEnvironment").Environment.Sky.Duplicate(true);
            var sky = (ProceduralSkyMaterial)environment.Sky.SkyMaterial;
            sky.SkyTopColor = new Color("488cc4");
            sky.SkyHorizonColor = new Color("bcdde8");
            sky.GroundHorizonColor = new Color("bcdde8");
            sky.GroundBottomColor = new Color("a7c3d0");
            sky.SkyCurve = 0.16f;
            environment.FogLightColor = new Color("b9d0d5");
            environment.FogDensity = 0.0022f;
            environment.FogSkyAffect = 0.08f;
            environment.AmbientLightColor = new Color("cfdfdc");
            environment.AmbientLightEnergy = 0.55f;
            _assetPreviewMeadowEnvironment = environment;
            preset.Free();
        }
        _fireEnvironment = meadow ? _assetPreviewMeadowEnvironment! : _assetPreviewOriginalEnvironment;
        foreach (Node child in _world.GetChildren())
            if (child is WorldEnvironment worldEnvironment) worldEnvironment.Environment = _fireEnvironment;
    }
}
