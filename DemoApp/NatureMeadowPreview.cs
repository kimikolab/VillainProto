using Godot;
using System;

// 背景D。Cの起伏・地面・風に揺れる草に、購入素材の植生と青空を組み合わせる。
public partial class NatureMeadowPreview : Node3D
{
    private const string Nature = "res://PolyBlocks/NatureBlocks/";
    private readonly Random _random = new(42017);
    private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

    public override void _Ready()
    {
        // Cと同じ地面・草・風を使い、購入素材は木や茂みに組み合わせる。
        AddChild(new MeadowEnvironment3D { IncludeWoodland = false });

        // 中央を開き、左右の近景と、丘の向こうの林で奥行きを分ける。
        for (int i = 0; i < 22; i++)
        {
            float x = Range(-31, 31), z = Range(-33, -16);
            Place($"trees/generic/forest/nb_tree{i % 5 + 1}_forest.tscn", x, z, Range(0.45f, 0.85f));
        }
        Place("trees/generic/forest/nb_tree2_forest.tscn", -10.8f, -7.8f, 0.85f);
        Place("trees/generic/forest/nb_tree4_forest.tscn", 11.8f, -8.2f, 0.9f);
        Place("trees/generic/forest/nb_tree1_forest.tscn", -15, -12, 0.95f);
        Place("trees/generic/forest/nb_tree3_forest.tscn", 16, -15, 1.1f);
        for (int i = 0; i < 62; i++)
        {
            float x = Range(-24, 24), z = Range(-27, 7);
            if (Math.Abs(x) < 8 && z > -6) continue;
            Place($"vegetation/shrubs/forest/nb_shrub{i % 2 + 1}_forest.tscn", x, z, Range(0.32f, 0.7f));
            if (i % 5 == 0)
                Place($"ground/rocks/verdant/nb_rock{i % 6 + 1}_verdant.tscn", x + 0.7f, z, Range(0.35f, 0.75f));
        }
        for (int i = 0; i < 42; i++)
        {
            float x = Range(-16, 16), z = Range(-12, 5);
            if (Math.Abs(x) < 7.8f && z > -5.5f) continue;
            Place(i % 3 == 0 ? "vegetation/flowers/nb_flower2_yellow.tscn" : "vegetation/flowers/nb_flower1_white.tscn",
                x, z, Range(0.24f, 0.38f));
        }
    }

    private void Place(string path, float x, float z, float scale)
    {
        var instance = GD.Load<PackedScene>(Nature + "assets/" + path).Instantiate<Node3D>();
        instance.Position = new Vector3(x, MeadowEnvironment3D.Height(x, z) - 0.02f, z);
        instance.Scale = Vector3.One * scale;
        instance.RotationDegrees = new Vector3(0, Range(0, 360), 0);
        AddChild(instance);
    }
}
