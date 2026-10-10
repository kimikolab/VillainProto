using Godot;

// 敵の燃焼と比較画面で共用する購入素材の表示部品。
public partial class AssetBurnPreview : Node3D
{
    internal void SetPlaybackSpeed(double speed)
    {
        foreach (Node flame in GetChildren())
            foreach (Node child in flame.GetChildren())
                if (child is GpuParticles3D particles) particles.SpeedScale = speed;
    }

    public override void _Ready()
    {
        var scene = GD.Load<PackedScene>("res://PolyBlocks/EffectBlocks/assets/fire/fire_light.tscn");
        for (int side = 0; side < 2; side++)
        {
            var flame = scene.Instantiate<Node3D>();
            // 足元から腰へ、小さい炎を2か所。中央の輪と顔を隠さない。
            flame.Position = new Vector3(side == 0 ? -0.22f : 0.22f, 0.28f, 0.35f);
            flame.Scale = Vector3.One * 0.46f;
            Prepare(flame, side);
            AddChild(flame);
        }
    }

    private static void Prepare(Node node, int side)
    {
        node.SetScript(default);
        if (node is GpuParticles3D particles)
        {
            // 素材原本と共有リソースを書き換えず、試着用インスタンスだけ調整する。
            var material = (ParticleProcessMaterial)particles.ProcessMaterial.Duplicate();
            particles.ProcessMaterial = material;
            particles.LocalCoords = true;
            particles.OneShot = false;
            particles.Emitting = true;
            particles.UseFixedSeed = true;
            particles.Seed = (uint)(7319 + side * 97);
            particles.Preprocess = 0.8;
            if (particles.Name == "Smoke")
            {
                particles.Amount = 8;
                particles.Lifetime = 1.1;
                material.ScaleMin *= 0.65f;
                material.ScaleMax *= 0.65f;
            }
            else if (particles.Name == "Sparks")
            {
                particles.Amount = 8;
                particles.Lifetime = 1.1;
            }
            else particles.Amount = 32;
        }
        if (node is OmniLight3D light)
        {
            light.LightEnergy = 0.18f;
            light.OmniRange = 1.8f;
            light.ShadowEnabled = false;
        }
        foreach (Node child in node.GetChildren()) Prepare(child, side);
    }
}
