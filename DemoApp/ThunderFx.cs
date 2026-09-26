using Godot;
using System;

// 青白い電弧。経路は台本から渡す。乱数は描画専用で戦闘へ戻さない。
public static class ThunderFx
{
    public static readonly Color Cyan = new("70dfff");
    private static Texture2D? _stake;
    public static Texture2D Stake
    {
        get
        {
            if (_stake is not null) return _stake;
            if (FileAccess.FileExists("res://assets/fx/kata_stake.png"))
                return _stake = UiKit.LoadTexture("res://assets/fx/kata_stake.png");
            var image = new Image();
            image.LoadSvgFromString("<svg xmlns='http://www.w3.org/2000/svg' width='64' height='256'><path d='M32 4L45 51 37 66 37 202 32 250 27 202 27 66 19 51Z' fill='#292c32' stroke='#d9bc83' stroke-width='3'/><path d='M32 4L32 65 45 51Z' fill='#f7dfa8'/><ellipse cx='32' cy='73' rx='25' ry='10' fill='none' stroke='#d9bc83' stroke-width='3'/><path d='M8 63L32 96 56 63M12 93L32 53 52 93' fill='none' stroke='#d9bc83' stroke-width='2'/></svg>");
            return _stake = ImageTexture.CreateFromImage(image);
        }
    }

    public static void Arc(Node3D parent, Vector3 from, Vector3 to, float width, double seconds)
    {
        var root = new Node3D(); parent.AddChild(root);
        var points = new Vector3[10];
        for (int i = 0; i < points.Length; i++)
        {
            float t = i / 9f;
            points[i] = root.ToLocal(from.Lerp(to, t));
            if (i > 0 && i < 9) points[i] += new Vector3((float)GD.RandRange(-0.18, 0.18),
                (float)GD.RandRange(-0.13, 0.13), (float)GD.RandRange(-0.12, 0.12));
        }
        foreach (var layer in new[] { (width * 3, Cyan, 0.28f), (width, new Color("e4fbff"), 0.95f) })
        {
            var mesh = new ImmediateMesh();
            var material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
                AlbedoColor = new Color(layer.Item2, layer.Item3), EmissionEnabled = true, Emission = layer.Item2 };
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            for (int i = 1; i < points.Length; i++)
            {
                var a = points[i - 1]; var b = points[i];
                var side = (b - a).Cross(Vector3.Forward).Normalized() * layer.Item1;
                if (side.LengthSquared() < 0.00001f) side = Vector3.Right * layer.Item1;
                foreach (var v in new[] { a-side, a+side, b+side, a-side, b+side, b-side }) mesh.SurfaceAddVertex(v);
            }
            mesh.SurfaceEnd();
            root.AddChild(new MeshInstance3D { Mesh = mesh, MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
            var tween = root.CreateTween();
            tween.TweenProperty(material, "albedo_color:a", 0f, Math.Max(0.02, seconds));
        }
        var life = root.CreateTween(); life.TweenInterval(Math.Max(0.02, seconds));
        life.TweenCallback(Callable.From(root.QueueFree));
    }

    public static void Burst(Node3D parent, Vector3 point, float strength, double seconds)
    {
        for (int k = 0; k < 7; k++)
        {
            float a = k * Mathf.Tau / 7;
            Arc(parent, point, point + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0.15f) * strength,
                0.018f, seconds);
        }
    }
}

public partial class ShockAura3D : Node3D
{
    private double _clock;
    public override void _Process(double delta)
    {
        _clock -= delta;
        if (_clock > 0 || !IsVisibleInTree()) return;
        _clock = 0.27;
        var p = GlobalPosition + new Vector3((float)GD.RandRange(-0.42, 0.42), (float)GD.RandRange(-0.65, 0.65), 0.13f);
        ThunderFx.Arc(this, p, p + new Vector3(0.18f, 0.30f, 0), 0.013f, 0.16);
    }
}
