using BattleCore;
using Godot;
using System;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int FeatherMarkPlays, FeatherMarkAllyPlays, FramedAccusations, FramedVendettas;
    internal int RoundStarts, RoundSlashes, RoundTravels, RoundFinishes, SharePowerPlays, BeckonFeatherPlays;
    private void ResetMarkLoop()
    {
        FeatherMarkPlays = FeatherMarkAllyPlays = FramedAccusations = FramedVendettas = 0;
        RoundStarts = RoundSlashes = RoundTravels = RoundFinishes = SharePowerPlays = BeckonFeatherPlays = 0;
        SharePowerBatches = SharePowerLinks = SharePowerAmount = 0;
    }

    private bool MarkLoopLive(int generation, BattlePawn3D actor, BattlePawn3D target) =>
        generation == _specialGeneration && !_markRallyEnded && IsVisibleInTree()
        && IsInstanceValid(actor) && IsInstanceValid(target) && actor.IsInsideTree() && target.IsInsideTree()
        && actor.PresentationAlive;

    internal async Task PlayFeatherMark(BattlePawn3D? actor, BattlePawn3D? target, BattleEvent cue, double speed)
    {
        if (actor?.MisaFeathers is not { } feathers || target is null) return;
        int generation = _specialGeneration;
        speed = Math.Max(0.1, speed);
        // 古い台本の駒も見出しを受け取れば常駐に切り替える。
        feathers.Persistent = true;
        if (!feathers.IsDeployed)
        {
            await BeginMisaVolley(actor, feathers.Count, speed);
            if (!MarkLoopLive(generation, actor, target)) return;
        }
        bool ally = cue.Text == FeatherMarkLabels.Ally;
        if (!feathers.AimMark(target.FxPoint, crossfire: !ally)) return;
        actor.ShowMovementPortrait(ally ? "tome_spray" : "tome_control", 0.4,
            flip: (target.FxPoint - actor.FxPoint).Dot(_camera.GlobalBasis.X) < 0);
        await ToSignal(GetTree().CreateTimer((ally ? 0.075 : 0.045) / speed), SceneTreeTimer.SignalName.Timeout);
        if (!MarkLoopLive(generation, actor, target) || feathers.FireMark() is not { } muzzle) return;
        FeatherMarkPlays++;
        if (ally) FeatherMarkAllyPlays++;
        Color tint = ally ? new Color("f9b0c8") : MisaLight;
        if (ally)
        {
            ShockMarkFx.Glow(_fxRoot, muzzle, tint, 0.48f, 0.16 / speed);
            ShockMarkFx.Beam(_fxRoot, muzzle, target.FxPoint, tint, 0.055f, 0.18 / speed);
            ShockMarkFx.Sparks(_fxRoot, target.FxPoint, tint, 5, 0.4f, 0.18 / speed);
            _attackAudio.PlayShockMark(ShockMarkSound.Beam, speed: speed);
            return;
        }
        // 敵への標撃ちは、細い点滅ではなく白い芯を持つ瞬発レーザー。
        // 倍速でも保持と残光を数フレーム残し、着弾表示より先に消えないようにする。
        double hold = Math.Max(0.045, 0.07 / speed), decay = Math.Max(0.10, 0.18 / speed);
        ShockMarkFx.Glow(_fxRoot, muzzle, Colors.White, 0.8f, hold + decay);
        ShockMarkFx.Beam(_fxRoot, muzzle, target.FxPoint, tint, 0.13f, decay, hold);
        ShockMarkFx.Beam(_fxRoot, muzzle, target.FxPoint, new Color(tint, 0.45f), 0.035f,
            Math.Max(0.18, 0.38 / speed), hold);
        ShockMarkFx.Glow(_fxRoot, target.FxPoint, tint, 1.15f, Math.Max(0.16, 0.28 / speed));
        ShockMarkFx.Sparks(_fxRoot, target.FxPoint, tint, 10, 0.8f, Math.Max(0.16, 0.30 / speed));
        MakeGroundRing(target.Home, tint, 0.7f, Math.Max(0.14, 0.24 / speed));
        float muzzlePan = Math.Clamp((_camera.UnprojectPosition(muzzle).X / _viewport.Size.X - 0.5f) * 1.4f, -0.7f, 0.7f);
        float hitPan = Math.Clamp((_camera.UnprojectPosition(target.FxPoint).X / _viewport.Size.X - 0.5f) * 1.4f, -0.7f, 0.7f);
        // 音程を変えず、倍速でも発射音と着弾音の胴を切り詰めすぎない。
        double soundSpeed = Math.Min(speed, 1.25);
        _attackAudio.PlayShockMark(ShockMarkSound.Beam, pan: muzzlePan, speed: soundSpeed);
        _attackAudio.PlayShockMark(ShockMarkSound.BeamHit, pan: hitPan, speed: soundSpeed);
        CameraPunch(target.FxPoint, AttackPattern.Single);
    }

    internal void ShowFramed(BattlePawn3D? actor, BattlePawn3D? target, BattleEvent cue, double speed)
    {
        if (actor is null || target is null) return;
        if (cue.Text == FramedLabels.Vendetta)
        {
            FramedVendettas++;
            Float(target, "？", new Color("ffe6bc"), true, 3.8f, 1.2f);
            return;
        }
        if (cue.Text != FramedLabels.Accuse) return;
        FramedAccusations++;
        var tint = new Color("f19bde");
        actor.ShowMovementPortrait("hisa_rally", 0.8,
            flip: (target.FxPoint - actor.FxPoint).Dot(_camera.GlobalBasis.X) < 0);
        if (_rallyCaptions.TryGetValue(actor.InstanceId, out var old) && LivePopup(old))
        { old.Hide(); old.QueueFree(); }
        var caption = CreatePopup(actor.FxPoint + Vector3.Up * 1.4f, "あいつがやった！", tint, 20);
        caption.AddThemeStyleboxOverride("normal", UiKit.Box(new Color("31182e"), tint, 2, 5));
        _rallyCaptions[actor.InstanceId] = caption;
        _popups.Add((actor.InstanceId, caption));
        var fade = caption.CreateTween();
        fade.TweenInterval(0.55 / Math.Max(0.1, speed));
        fade.TweenProperty(caption, "modulate:a", 0f, 0.20 / Math.Max(0.1, speed));
        fade.TweenCallback(Callable.From(caption.QueueFree));
        ShockMarkFx.Beam(_fxRoot, actor.FxPoint + Vector3.Up * 0.45f, target.FxPoint,
            new Color(tint, 0.55f), 0.025f, 0.32 / Math.Max(0.1, speed));
    }

    internal void ShowBeckonFeather(BattlePawn3D? target, BattlePawn3D? shooter, int prevented, double speed)
    {
        if (target is null) return;
        BeckonFeatherPlays++;
        speed = Math.Max(0.1, speed);
        Vector3 at = target.FxPoint;
        var from = shooter?.MisaFeathers?.LastMuzzle ?? at + _camera.GlobalBasis.X;
        Vector3 contact = at + (from - at).Normalized() * 0.38f;
        ShockMarkFx.Ring(_fxRoot, at, RallyAmber, 1.4f, 0.24 / speed);
        ShockMarkFx.Beam(_fxRoot, contact, contact + _camera.GlobalBasis.X * 0.6f + Vector3.Up * 0.65f,
            MisaLight, 0.045f, 0.22 / speed);
        ShockMarkFx.Sparks(_fxRoot, contact, RallyAmber, 7, 0.5f, 0.22 / speed);
        Float(target, $"矢面 −{prevented}", RallyAmber, false, 2.8f);
        _attackAudio.PlayShockMark(ShockMarkSound.Insight);
    }

    internal void ShowRoundStart(BattlePawn3D? actor, BattleEvent cue, double speed)
    {
        if (actor is null) return;
        RoundStarts++;
        Float(actor, $"仇巡り ×{cue.Amount}", ZanRed, true, 3.65f, 1.35f);
        _attackAudio.PlayZan(ZanSound.Cue);
    }

    internal async Task ShowRoundSlash(BattlePawn3D? actor, BattlePawn3D? target, BattleEvent cue, bool last, double speed)
    {
        if (actor is null || target is null) return;
        int generation = _specialGeneration;
        speed = Math.Max(0.1, speed);
        Vector3 from = actor.FxPoint;
        if (actor.MoveRound(target.Position))
        {
            RoundTravels++;
            ShockMarkFx.Beam(_fxRoot, from, target.FxPoint, new Color(ZanRed, 0.34f), 0.04f, 0.26 / speed);
            await ToSignal(GetTree().CreateTimer(0.12 / speed), SceneTreeTimer.SignalName.Timeout);
        }
        if (!MarkLoopLive(generation, actor, target)) return;
        RoundSlashes++;
        if (last) RoundFinishes++;
        // 終太刀を大きくする。最大8太刀でも既存の7枚段階へ切り捨てず、1件1閃。
        DrawZanCut(_fxRoot, target, last ? 10 : 6, last ? 6 : (cue.Slot - 1) % 5, last ? 7 : 8, speed);
        await ToSignal(GetTree().CreateTimer(0.075 / speed), SceneTreeTimer.SignalName.Timeout);
    }
}
