using BattleCore;
using Godot;
using System;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int ChargePlays, InterruptPlays, CloudPlays, PowderMainPlays, PowderSpreadPlays, PowderLeakPlays;
    internal int ThreadPlays, ThreadReleasePlays, ScarPlays, MarkLayerPlays, CowerPlays;

    private void ResetShockMarkCounts()
    {
        ChargePlays = InterruptPlays = CloudPlays = PowderMainPlays = PowderSpreadPlays = PowderLeakPlays = 0;
        ThreadPlays = ThreadReleasePlays = ScarPlays = MarkLayerPlays = CowerPlays = 0;
        ResetShockWebCounts();
        ResetMarkRallyCounts();
    }
    internal void EndShockMarkPresentation()
    {
        EndMarkRallyPresentation();
        foreach (var pawn in _pawns.Values) pawn.EndShockMark();
        ResetShockWeb();
        _attackAudio.StopShockMarkPresentation();
    }
    internal void ShowFeatherGain(BattlePawn3D? pawn, double speed)
    {
        if (pawn is null) return;
        ShockMarkFx.Ring(_fxRoot, pawn.FxPoint, ShockMarkFx.Feather, 1.25f, 0.40 / speed);
        ShockMarkFx.Sparks(_fxRoot, pawn.FxPoint, ShockMarkFx.Feather, 9, 0.85f, 0.40 / speed);
        _attackAudio.PlayShockMark(ShockMarkSound.FeatherGain);
    }
    internal void ShowFeatherVolley(BattlePawn3D? pawn, double speed)
    {
        if (pawn is null) return;
        pawn.ShowMovementPortrait("tome_control", 0.8);
        ShockMarkFx.Ring(_fxRoot, pawn.FxPoint, ShockMarkFx.Feather, 2.3f, 0.38 / speed);
        _attackAudio.PlayShockMark(ShockMarkSound.Deploy);
    }
    internal void ShowMarkLayer(BattlePawn3D? target, int amount, double speed)
    {
        if (target is null) return;
        MarkLayerPlays++;
        target.SetMarkLayers(amount);
        target.SetStatusIcon(StatusKeys.Marked, amount > 0);
        ShockMarkFx.Ring(_fxRoot, target.FxPoint, new Color("ff8199"), 1.8f, 0.32 / speed);
        _attackAudio.PlayShockMark(ShockMarkSound.Lock);
    }
    internal void ShowScar(BattlePawn3D? target, int maxHp, int hp, double speed)
    {
        if (target is null) return;
        ScarPlays++;
        target.ApplyScar(maxHp, hp);
        var right = _camera.GlobalBasis.X * 0.38f;
        ShockMarkFx.Beam(_fxRoot, target.FxPoint - right - Vector3.Up * 0.18f,
            target.FxPoint + right + Vector3.Up * 0.32f, ShockMarkFx.Feather, 0.04f, 0.3 / speed);
        ShockMarkFx.Sparks(_fxRoot, target.FxPoint, ShockMarkFx.Feather, 7, 0.5f, 0.3 / speed);
        _attackAudio.PlayShockMark(ShockMarkSound.Scar);
    }
    internal void ShowCharge(BattlePawn3D? target, int amount, bool gain, double speed)
    {
        if (target is null) return;
        ChargePlays++;
        target.SetStoredCharge(amount);
        target.InterruptWhip = false;
        target.WhipChainSize = 0;
        if (gain) target.SetFrightened(false);
        ShockMarkFx.Ring(_fxRoot, target.FxPoint, ThunderFx.Cyan, 0.75f + amount * 0.22f, 0.25 / speed);
        if (gain) _attackAudio.PlayShockMark(ShockMarkSound.Charge);
    }
    internal void ShowInterrupt(BattlePawn3D? actor, BattlePawn3D? signal, double speed)
    {
        if (actor is null) return;
        InterruptPlays++;
        actor.InterruptWhip = true;
        actor.WhipChainSize = 0;
        actor.SetFrightened(false);
        actor.ShowMovementPortrait("shiga_interrupt", 1.8);
        if (signal is not null) ThunderFx.Arc(_fxRoot, signal.DischargePoint, actor.WhipOrigin(_camera), 0.06f, 0.25 / speed);
        ShockMarkFx.Glow(_fxRoot, actor.WhipOrigin(_camera), ThunderFx.Cyan, 1.3f, 0.26 / speed);
        _attackAudio.PlayShockMark(ShockMarkSound.Interrupt);
    }
    internal void ShowCower(BattlePawn3D? actor, double speed)
    {
        if (actor is null) return;
        CowerPlays++;
        actor.SetFrightened(true);
        actor.ShockCollapse();
    }
    internal void ShowCloud(BattlePawn3D? actor, int amount, double speed)
    {
        if (actor is null) return;
        CloudPlays++;
        actor.SetThundercloud(amount);
        ShockMarkFx.Glow(_fxRoot, actor.FxPoint + Vector3.Up * 1.9f, ThunderFx.Cyan, 1.7f, 0.36 / speed);
        _attackAudio.PlayShockMark(ShockMarkSound.Cloud);
    }

    internal void BeginPowderAttack(BattlePawn3D from)
    {
        from.ShowMovementPortrait("tou_powder", 1.25);
        foreach (var pixel in new[] { new Vector2(204, 388), new Vector2(877, 1027) })
            ShockMarkFx.Glow(_fxRoot, from.MovementPortraitPoint(pixel, _camera), ShockMarkFx.Gold, 0.42f, 0.45 / from.AnimationSpeed);
        _attackAudio.PlayShockMark(ShockMarkSound.Powder);
    }
    internal async Task ShowPowder(BattlePawn3D? actor, BattlePawn3D? source, BattlePawn3D? target, PowderRoute route, double speed)
    {
        if (actor is null || target is null) return;
        if (route == PowderRoute.Main) PowderMainPlays++;
        else if (route == PowderRoute.Spread) PowderSpreadPlays++;
        else PowderLeakPlays++;
        int generation = _specialGeneration;
        bool leak = route == PowderRoute.Leak;
        Vector3 from = (route == PowderRoute.Spread ? source ?? actor : actor).FxPoint;
        Vector3 to = target.FxPoint;
        double seconds = (leak ? 0.24 : 0.18) / speed;
        for (int i = 0; i < (leak ? 3 : 5); i++)
        {
            int k = i;
            var puff = ShockMarkFx.Sprite(_fxRoot, from, ShockMarkFx.Powder, leak ? 0.68f : 1.10f, new Color(1, 1, 1, leak ? 0.48f : 0.73f));
            var tween = puff.CreateTween();
            tween.TweenMethod(Callable.From<float>(p => {
                puff.GlobalPosition = from.Lerp(to, p) + _camera.GlobalBasis.X * (Mathf.Sin(p * Mathf.Pi) * (k - 2) * 0.18f)
                    + Vector3.Up * Mathf.Sin(p * Mathf.Pi) * (0.2f + k * 0.06f);
                puff.Scale = Vector3.One * (0.45f + p * 0.65f);
            }), 0f, 1f, seconds);
            tween.TweenProperty(puff, "modulate:a", 0f, 0.32 / speed);
            tween.TweenCallback(Callable.From(puff.QueueFree));
        }
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        if (generation != _specialGeneration || !IsVisibleInTree() || !IsInstanceValid(target) || !target.IsInsideTree()) return;
        ShockMarkFx.Sparks(_fxRoot, to, ShockMarkFx.Gold, leak ? 5 : 11, 0.5f, 0.3 / speed);
        ThunderFx.Arc(_fxRoot, to - _camera.GlobalBasis.X * 0.18f, to + Vector3.Up * 0.2f, 0.012f, 0.2 / speed);
    }

    internal void ShowThreadDischarge(BattlePawn3D? actor, BattlePawn3D? target, BattlePawn3D? partner, bool release, double speed)
    {
        if (actor is null || target is null) return;
        ThreadPlays++; DischargePlays++;
        if (release) ThreadReleasePlays++;
        if (partner is not null) ThunderFx.Arc(_fxRoot, partner.DischargePoint, actor.DischargePoint, 0.035f, 0.18 / speed);
        BindingSilk3D silk;
        if (_bindings.TryGetValue(actor.InstanceId, out var binding) && binding.Target == target && IsInstanceValid(binding.Silk)) silk = binding.Silk;
        else if (_releasingBindings.TryGetValue(actor.InstanceId, out var old) && old.Target == target && IsInstanceValid(old.Silk)) silk = old.Silk;
        else
        {
            // 解除通知が先に来ても、台本に残る「切れる直前」の糸を描く。
            silk = new BindingSilk3D(); _fxRoot.AddChild(silk); silk.Configure(actor, target, _camera);
            silk.Release();
        }
        silk.Conduct(release);
        _attackAudio.PlayShockMark(release ? ShockMarkSound.Snap : ShockMarkSound.Thread);
    }
}
