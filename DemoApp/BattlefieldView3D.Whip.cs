using Godot;
using System;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int TormentHitPlays { get; private set; }
    // 責め苦はAttackを増やさない特性ダメージ。踏み込み・追加攻撃のカットインは呼ばない。
    public async Task ShowTormentHit(BattlePawn3D from, BattlePawn3D target)
    {
        TormentHitPlays++;
        _attackAudio.PlayAttack(from.UnitId, from.Team, BattleCore.AttackPattern.Single, false, false);
        await ShowWhipAttack(from, target);
    }

    // 手元からしなる一本の鞭。先端の波が走り、伸びきった瞬間に小さな火花を出す。
    // 戦闘の乱数やダメージには触れず、戻りの動作もこの表示の完了を待つ。
    private async Task ShowWhipAttack(BattlePawn3D from, BattlePawn3D target)
    {
        Vector3 direction = (target.FxPoint - from.FxPoint).Normalized();
        Vector3 start = from.FxPoint + direction * 0.22f;
        Vector3 end = target.FxPoint;
        Vector3 sideways = direction.Cross(Vector3.Up).Normalized();
        Vector3 view = _camera.GlobalPosition - (start + end) * 0.5f;
        var material = MakeMaterial(new Color("b84e69"), true, true, 0.35f, new Color("682239"));
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var mesh = new ImmediateMesh();
        var whip = new MeshInstance3D { Mesh = mesh, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        _fxRoot.AddChild(whip);
        double duration = Math.Max(0.30, 0.60 / Math.Max(0.1, from.AnimationSpeed));
        bool cracked = false;
        var tween = whip.CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            float reach = t < 0.62f ? Mathf.Pow(t / 0.62f, 0.55f) : 1 - (t - 0.62f) / 0.38f * 0.7f;
            float curl = t < 0.62f ? (1 - t / 0.62f) : (t - 0.62f) * 1.5f;
            Vector3 Point(float u) => start.Lerp(end, u * reach)
                + Vector3.Up * (Mathf.Sin(u * Mathf.Pi) * curl * 1.6f)
                + sideways * (Mathf.Sin(u * Mathf.Tau - t * 9) * Mathf.Sin(u * Mathf.Pi) * curl * 0.85f);
            mesh.ClearSurfaces();
            mesh.SurfaceBegin(Mesh.PrimitiveType.Triangles);
            for (int i = 0; i < 48; i++)
            {
                float u = i / 48f, v = (i + 1) / 48f;
                Vector3 a = Point(u), b = Point(v);
                Vector3 normal = (b - a).Cross(view).Normalized();
                Vector3 wa = normal * Mathf.Lerp(0.048f, 0.012f, u);
                Vector3 wb = normal * Mathf.Lerp(0.048f, 0.012f, v);
                mesh.SurfaceAddVertex(a - wa); mesh.SurfaceAddVertex(b - wb); mesh.SurfaceAddVertex(a + wa);
                mesh.SurfaceAddVertex(a + wa); mesh.SurfaceAddVertex(b - wb); mesh.SurfaceAddVertex(b + wb);
            }
            mesh.SurfaceEnd();
            if (!cracked && t >= 0.62f)
            {
                cracked = true;
                Color flash = new("ffe0ba");
                for (int i = 0; i < 5; i++)
                {
                    float angle = i * Mathf.Tau / 5;
                    Vector3 ray = Vector3.Up * Mathf.Cos(angle) + sideways * Mathf.Sin(angle);
                    MakeBeam(end + ray * 0.08f, end + ray * 0.32f, flash, 0.036f, 0.12);
                }
            }
            whip.Transparency = Mathf.Max(0, (t - 0.78f) / 0.22f);
        }), 0.01f, 1f, duration);
        tween.Finished += whip.QueueFree;
        await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
    }
}
