using Godot;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

public partial class BattlefieldView3D
{
    public Task ShowPoisonDrain(IReadOnlyList<BattlePawn3D> sources, BattlePawn3D receiver, double speed)
        => Task.WhenAll(sources.Select(source => ShowPoisonTransfer(source, receiver, false, speed, drain: true)));

    // 経路と始点は台本で確定済み。陣営や隣接から戦闘の判定を再現しない。
    public async Task ShowPoisonTransfer(BattlePawn3D? source, BattlePawn3D target,
        bool leak, double speed, bool drain = false)
    {
        if (source is null) return;
        var group = new Node3D { Name = drain ? "PoisonDrain" : leak ? "PoisonLeak" : "PoisonSplash" };
        _fxRoot.AddChild(group);
        Vector3 from = _fxRoot.ToLocal(leak ? source.GlobalPosition + Vector3.Up * 0.09f : source.FxPoint);
        Vector3 to = _fxRoot.ToLocal(leak ? target.GlobalPosition + Vector3.Up * 0.09f : target.FxPoint);
        Vector3 side = (to - from).Cross(Vector3.Up).Normalized();
        int count = leak ? 11 : 7;
        var drops = new MeshInstance3D[count];
        var materials = new StandardMaterial3D[count];
        for (int i = 0; i < count; i++)
        {
            float radius = leak ? 0.19f : 0.095f + (i % 3) * 0.025f;
            materials[i] = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = new Color("b76aee"),
            };
            drops[i] = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = radius, Height = radius * 2,
                    RadialSegments = 12, Rings = 6 },
                MaterialOverride = materials[i], Visible = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            group.AddChild(drops[i]);
        }
        var tween = group.CreateTween();
        tween.TweenMethod(Callable.From<float>(p =>
        {
            for (int i = 0; i < count; i++)
            {
                if (leak)
                {
                    // 地面に薄い滴が順々に広がる。泥の筋や空中の飛沫とは形を分ける。
                    float age = Mathf.Clamp((p - i * 0.065f) / 0.28f, 0, 1);
                    drops[i].Visible = age > 0;
                    drops[i].Position = from.Lerp(to, i / (float)(count - 1))
                        + side * Mathf.Sin(i * 2.4f) * 0.065f;
                    drops[i].Scale = new Vector3(0.25f + age, 0.12f, 0.25f + age);
                    materials[i].AlbedoColor = new Color(new Color("b76aee"), 0.40f * Mathf.Sin(age * Mathf.Pi * 0.75f));
                }
                else if (drain)
                {
                    // 吸い上げは加速しながら細く収束する。伝染の放物線と向き・動きを分ける。
                    float t = Mathf.Clamp((p - i * 0.035f) / 0.79f, 0, 1);
                    float pull = t * t;
                    float curl = Mathf.Sin(t * Mathf.Pi) * (1 - t);
                    drops[i].Visible = p >= i * 0.035f && t < 1;
                    drops[i].Position = from.Lerp(to, pull)
                        + side * curl * Mathf.Sin(t * Mathf.Tau + i) * 0.38f
                        + Vector3.Up * curl * (0.25f + (i % 3) * 0.10f);
                    drops[i].Scale = Vector3.One * (1 - 0.8f * pull);
                }
                else
                {
                    // 尾を引く一本の線ではなく、高さと横幅の違う独立した滴を飛ばす。
                    float t = Mathf.Clamp((p - i * 0.035f) / 0.79f, 0, 1);
                    float arc = Mathf.Sin(t * Mathf.Pi);
                    drops[i].Visible = p >= i * 0.035f;
                    drops[i].Position = from.Lerp(to, t)
                        + Vector3.Up * arc * (0.45f + i % 3 * 0.16f)
                        + side * arc * ((i % 3 - 1) * 0.22f);
                    drops[i].Scale = new Vector3(0.8f, 1.3f, 0.8f);
                }
            }
        }), 0f, 1f, (drain ? 0.80 : leak ? 0.64 : 0.52) / Math.Max(0.1, speed));
        tween.TweenCallback(Callable.From(group.QueueFree));
        await ToSignal(group, Node.SignalName.TreeExiting);
        // 再開時にも解除される。呼び手は再生トークンを照合してから残量を更新する。
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
