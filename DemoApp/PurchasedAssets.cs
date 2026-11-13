using Godot;

// 原本はGitに含めず、PrepareAssetPreview.ps1で各環境に配置する。
internal static class PurchasedAssets
{
    internal static bool EffectsAvailable => ResourceLoader.Exists("res://PolyBlocks/EffectBlocks/assets/fire/fire_light.tscn")
        && ResourceLoader.Exists("res://PolyBlocks/EffectBlocks/assets/explosions/explosion_light.tscn");
    internal static bool NatureAvailable => ResourceLoader.Exists("res://PolyBlocks/NatureBlocks/assets/trees/generic/forest/nb_tree1_forest.tscn")
        && ResourceLoader.Exists("res://PolyBlocks/NatureBlocks/assets/environments/nb_env_cartoon.tscn");
    // 道・川の道具（Path3D からメッシュを作る）。PrepareAssetPreview.ps1 が tools/ を写す。
    internal const string NatureRoadTool = "res://PolyBlocks/NatureBlocks/tools/generator_road/nb_gen_road.tscn";
    internal const string NatureRiverTool = "res://PolyBlocks/NatureBlocks/tools/generator_river/nb_gen_river.tscn";
    internal static bool NatureToolsAvailable => ResourceLoader.Exists(NatureRoadTool) && ResourceLoader.Exists(NatureRiverTool);
}
