using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private Texture2D? _hushSlapTexture;

    // 破砕後は平手で横から払う。台本の標的・打数・HPには触れない。
    private async Task ShowHushSlap(BattlePawn3D actor, IReadOnlyList<BattlePawn3D> hits)
    {
        int generation = _specialGeneration;
        double speed = Math.Max(.1, actor.AnimationSpeed);
        float side = actor.Team == 0 ? 1 : -1;
        Vector3 across = _camera.GlobalBasis.X * side;
        _hushSlapTexture ??= UiKit.LoadTexture("res://assets/fx/hush_slap.svg");
        var palms = new List<(BattlePawn3D Target, Sprite3D Palm)>();
        actor.MovementPose(-.20f, .24f);
        foreach (var target in hits)
        {
            Vector3 point = target.FxPoint + Vector3.Up * .35f + _camera.GlobalBasis.Z * .22f;
            var palm = new Sprite3D {
                Name = "HushSlapPalm", Texture = _hushSlapTexture,
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                Shaded = false, NoDepthTest = true, PixelSize = .0042f,
                FlipH = side < 0, Position = _fxRoot.ToLocal(point - across * .80f),
                Modulate = new Color(1, 1, 1, .8f),
            };
            _fxRoot.AddChild(palm);
            palms.Add((target, palm));
            palm.CreateTween().TweenProperty(palm, "position", _fxRoot.ToLocal(point), .09 / speed)
                .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        }
        await ToSignal(GetTree().CreateTimer(.09 / speed), SceneTreeTimer.SignalName.Timeout);
        if (generation != _specialGeneration || !IsInstanceValid(actor) || !actor.IsInsideTree())
        {
            foreach (var (_, palm) in palms) if (IsInstanceValid(palm)) palm.QueueFree();
            return;
        }
        HushSlaps++;
        _attackAudio.PlayShockMark(ShockMarkSound.HushSlap, side * .4f, speed);
        foreach (var (target, palm) in palms)
        {
            if (!IsInstanceValid(palm)) continue;
            if (IsInstanceValid(target) && target.IsInsideTree())
            {
                MakeContactImpact(palm.GlobalPosition, speed, .9f, .15, "HushSlapImpact");
                target.MovementPose(.28f, .18f);
            }
            var fade = palm.CreateTween().SetParallel();
            fade.TweenProperty(palm, "position", palm.Position + across * .32f, .16 / speed);
            fade.TweenProperty(palm, "modulate:a", 0f, .16 / speed);
            fade.Chain().TweenCallback(Callable.From(palm.QueueFree));
        }
    }
}
