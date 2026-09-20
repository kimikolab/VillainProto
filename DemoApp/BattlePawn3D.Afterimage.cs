using BattleCore;
using Godot;

public partial class BattlePawn3D
{
    /// <summary>本体の透過処理を共有する青い残像。判定・本体の移動には触れない。</summary>
    public void BeginBonusAfterimage(Color color, double duration, bool interrupted)
    {
        var root = new Node3D();
        AddChild(root);
        float forward = Team == BattleContext.PlayerTeam ? 1 : -1;
        var ghosts = new Sprite3D[3];
        var materials = new ShaderMaterial[3];
        for (int i = 0; i < ghosts.Length; i++)
        {
            var material = (ShaderMaterial)_portraitMaterial.Duplicate();
            material.SetShaderParameter("aura_amount", 1.0f);
            material.SetShaderParameter("portrait_tint", new Color(color, 0));
            materials[i] = material;
            ghosts[i] = new Sprite3D
            {
                Texture = _sprite.Texture, PixelSize = _sprite.PixelSize,
                Billboard = _sprite.Billboard, FlipH = _sprite.FlipH,
                Position = _sprite.Position + new Vector3(0, 0, -0.045f - i * 0.015f),
                Scale = _sprite.Scale * (i == 0 ? 1.055f : 1.015f),
                MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            root.AddChild(ghosts[i]);
        }

        // 粒は引き戻された残像からだけ出す。規則的な柱や輪に並べない。
        var particles = new MeshInstance3D[22];
        var particleMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = color,
        };
        var mesh = new SphereMesh { Radius = 0.026f, Height = 0.052f, RadialSegments = 6, Rings = 3 };
        if (interrupted)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i] = new MeshInstance3D
                {
                    Mesh = mesh, MaterialOverride = particleMaterial, Visible = false,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                };
                root.AddChild(particles[i]);
            }
        }
        var tween = root.CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            float rise = Mathf.SmoothStep(0, 0.28f, t);
            float collapse = Mathf.SmoothStep(0.28f, 0.48f, t);
            float dissolve = Mathf.SmoothStep(0.45f, 0.95f, t);
            for (int i = 0; i < ghosts.Length; i++)
            {
                float distance = i == 0 ? 0 : 0.24f + i * 0.22f;
                float offset = interrupted ? distance * rise * (1 - collapse)
                    : distance * rise + Mathf.Max(0, t - 0.4f) * 0.45f;
                ghosts[i].Position = _sprite.Position + new Vector3(forward * offset, 0, -0.045f - i * 0.015f);
                float alpha = (i == 0 ? 0.75f : 0.36f / i) * rise
                    * (interrupted ? 1 - dissolve : 1 - Mathf.SmoothStep(0.55f, 1, t));
                materials[i].SetShaderParameter("portrait_tint", new Color(color, alpha));
            }
            if (!interrupted) return;
            float scatter = Mathf.Clamp((t - 0.45f) / 0.55f, 0, 1);
            particleMaterial.AlbedoColor = new Color(color, Mathf.Sin(scatter * Mathf.Pi) * 0.8f);
            for (int i = 0; i < particles.Length; i++)
            {
                float angle = i * 2.39996f;
                particles[i].Visible = scatter > 0;
                particles[i].Position = new Vector3(
                    Mathf.Sin(angle) * (0.26f + scatter * 0.85f),
                    0.25f + _portraitHeight * ((i * 7 % 22) / 24.0f) - scatter * 0.35f,
                    0.18f + Mathf.Cos(angle) * scatter * 0.4f);
                particles[i].Scale = Vector3.One * (1 - scatter * 0.7f);
            }
        }), 0.0f, 1.0f, duration);
        tween.Finished += root.QueueFree;
    }
}
