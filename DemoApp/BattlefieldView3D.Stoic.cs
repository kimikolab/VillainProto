using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    // 金色の直線光と菱形の盾。守りの標・範囲盾・悲鳴の輪とは形を分ける。
    public async Task ShowStoicSupport(BattlePawn3D? source, BattlePawn3D relay,
        IReadOnlyList<BattleEvent> deliveries, double speed)
    {
        int generation = _shieldCowedGeneration;
        double beat = Math.Max(0.20, 0.38 / Math.Max(0.1, speed));
        Color tint = new(deliveries.Count == 0 ? "9de8cb" : "ffe69b");
        Vector3 center = relay.FxPoint + new Vector3(0, 0, 0.30f);
        Vector3 origin = source?.FxPoint ?? center + Vector3.Up * 1.6f;
        SupportLight(origin, center, tint, beat);
        await ToSignal(GetTree().CreateTimer(beat), SceneTreeTimer.SignalName.Timeout);
        if (generation != _shieldCowedGeneration || !GodotObject.IsInstanceValid(relay)) return;

        // 駒の移動・庇いの姿勢は変えず、盾の面だけを閃かせる。
        // 分配先の数にかかわらず、強化が盾へ届いた瞬間に1回鳴らす。
        if (deliveries.Count > 0) _attackAudio.PlayGaldReflect();
        Vector3[] rim = [new(0, 0.68f, 0), new(0.49f, 0.18f, 0),
            new(0.32f, -0.43f, 0), new(0, -0.70f, 0),
            new(-0.32f, -0.43f, 0), new(-0.49f, 0.18f, 0)];
        for (int i = 0; i < rim.Length; i++)
            MakeBeam(center + rim[i], center + rim[(i + 1) % rim.Length], tint, 0.065f, beat * 2);
        MakeBeam(center + rim[0], center + rim[3], Colors.White, 0.045f, beat);
        await ToSignal(GetTree().CreateTimer(beat * 0.5), SceneTreeTimer.SignalName.Timeout);
        if (generation != _shieldCowedGeneration) return;

        if (deliveries.Count == 0)
        {
            // 回復は近くで散って消える。味方への光線も回復反応も出さない。
            for (int i = 0; i < 7; i++)
            {
                float angle = Mathf.Tau * i / 7;
                SupportLight(center, center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0.3f) * 0.85f,
                    tint, beat, true);
            }
        }
        else
            foreach (var e in deliveries)
                if (FindPawn(e.TargetId) is { } receiver)
                    SupportLight(center, receiver.FxPoint, tint, beat);

        await ToSignal(GetTree().CreateTimer(beat), SceneTreeTimer.SignalName.Timeout);
        if (generation != _shieldCowedGeneration) return;
        foreach (var e in deliveries)
            if (FindPawn(e.TargetId) is { } receiver)
            {
                if (e.AttackAfter is { } attack) receiver.SetAttack(attack);
                receiver.AnimateHeal();
                Float(receiver, $"+{e.Amount}", tint);
            }
    }

    private void SupportLight(Vector3 from, Vector3 to, Color color, double duration, bool scatter = false)
    {
        var light = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.10f, Height = 0.20f, RadialSegments = 8, Rings = 4 },
            Position = from,
            MaterialOverride = MakeMaterial(color, true, true, 0.1f, color),
        };
        _fxRoot.AddChild(light);
        var tween = light.CreateTween();
        tween.TweenMethod(Callable.From<float>(t =>
        {
            Vector3 point = from.Lerp(to, t);
            if (point.DistanceSquaredTo(light.Position) > 0.000001f)
                MakeBeam(light.Position, point, new Color(color, scatter ? (1 - t) * 0.7f : 0.85f),
                    scatter ? 0.025f : 0.045f, duration * 0.5);
            light.Position = point;
            if (scatter) light.Scale = Vector3.One * (1 - t);
        }), 0f, 1f, duration);
        tween.Finished += light.QueueFree;
    }
}
