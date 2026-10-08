using BattleCore;
using Godot;
using System;
using System.Collections.Generic;

public partial class BattlefieldView3D
{
    internal int MarkRallyPlays, MarkRallyCues, MarkRallyLights, InsightPlays, InsightGuards, VengeancePlays;
    private static readonly Color RallyAmber = new("ffae46");
    private static readonly Color InsightWhite = new("b8ecff");
    private readonly Dictionary<int, PopupLabel2D> _rallyCaptions = new();
    private bool _markRallyEnded;

    private void ResetMarkRallyCounts()
    {
        MarkRallyPlays = MarkRallyCues = MarkRallyLights = InsightPlays = InsightGuards = VengeancePlays = 0;
        _rallyCaptions.Clear();
        _markRallyEnded = false;
        ResetZanPresentation();
    }

    private void EndMarkRallyPresentation()
    {
        _markRallyEnded = true;
        foreach (var caption in _rallyCaptions.Values)
            if (LivePopup(caption)) { caption.Hide(); caption.QueueFree(); }
        _rallyCaptions.Clear();
        EndZanPresentation();
    }

    internal void ShowMarkRally(MarkRallyPresentation.Rally rally, double speed)
    {
        var cue = rally.Cues[0];
        var hisa = FindPawn(cue.ActorId);
        if (hisa is null) return;
        MarkRallyPlays++;
        int generation = _specialGeneration;
        speed = Math.Max(0.1, speed);
        float force = 1 + Math.Min(6, Math.Max(0, cue.Slot - 1)) * 0.12f;
        var enemy = FindPawn(rally.EnemyId);
        float facing = enemy is null ? (hisa.Team == 0 ? 1 : -1)
            : Math.Sign((enemy.FxPoint - hisa.FxPoint).Dot(_camera.GlobalBasis.X));
        hisa.ShowMovementPortrait("hisa_rally", 0.85, flip: facing < 0);
        hisa.MovementPose(-0.10f, (float)(0.32 / speed));
        var mouth = hisa.FxPoint + Vector3.Up * 0.65f;
        if (_rallyCaptions.TryGetValue(hisa.InstanceId, out var previous) && LivePopup(previous))
        { previous.Hide(); previous.QueueFree(); }
        TrimPopups(hisa.InstanceId);
        var caption = CreatePopup(mouth + Vector3.Up * 0.75f,
            cue.Slot >= 3 ? "あいつを狙え！\nまだ倒れるな！" : "狙え！ 倒れるな！",
            RallyAmber, Mathf.RoundToInt(17 * force));
        caption.AddThemeStyleboxOverride("normal", UiKit.Box(new Color("24190e"), RallyAmber, 2, 5));
        _rallyCaptions[hisa.InstanceId] = caption;
        _popups.Add((hisa.InstanceId, caption));
        var captionTween = caption.CreateTween();
        captionTween.TweenInterval(0.55);
        captionTween.TweenProperty(caption, "modulate:a", 0f, 0.20);
        captionTween.TweenCallback(Callable.From(caption.QueueFree));
        // 閉じた聖なる輪ではなく、指差す先へ開く声の三本線。
        for (int i = -1; i <= 1; i++)
        {
            var dir = _camera.GlobalBasis.X * facing + Vector3.Up * i * 0.40f;
            ShockMarkFx.Beam(_fxRoot, mouth + dir * 0.30f, mouth + dir * (0.80f * force),
                RallyAmber, 0.05f, 0.28 / speed);
        }
        foreach (var heal in rally.Cues)
        {
            var recipient = FindPawn(heal.TargetId);
            if (recipient is null || heal.Amount <= 0) continue;
            MarkRallyLights++;
            Vector3 from = mouth, to = recipient.FxPoint;
            var spark = ShockMarkFx.Sprite(_fxRoot, from, ShockMarkFx.Halo, 0.48f * force, RallyAmber);
            var tween = spark.CreateTween();
            tween.TweenMethod(Callable.From<float>(p => {
                spark.GlobalPosition = from.Lerp(to, p) + Vector3.Up * Mathf.Sin(p * Mathf.Pi) * 0.6f;
            }), 0f, 1f, 0.18 / speed);
            tween.TweenCallback(Callable.From(() => {
                if (generation != _specialGeneration || _markRallyEnded || !IsVisibleInTree()) return;
                ShockMarkFx.Sparks(_fxRoot, to, RallyAmber, 10 + Math.Min(heal.Amount, 12),
                    0.65f * force, 0.30 / speed);
                ShockMarkFx.Glow(_fxRoot, to, RallyAmber, force, 0.25 / speed);
                _attackAudio.PlayShockMark(ShockMarkSound.RallyHeal);
            }));
            tween.TweenCallback(Callable.From(spark.QueueFree));
        }
    }

    internal void ShowInsight(BattlePawn3D? sora, BattlePawn3D? enemy, BattleEvent cue, double speed)
    {
        if (sora is null || enemy is null) return;
        InsightPlays++;
        speed = Math.Max(0.1, speed);
        Vector3 eye = sora.FxPoint + Vector3.Up * 0.8f;
        ShockMarkFx.Glow(_fxRoot, eye, InsightWhite, 0.6f + (cue.StatusRemaining ?? 0) * 0.012f, 0.3 / speed);
        ShockMarkFx.Beam(_fxRoot, eye, enemy.FxPoint + Vector3.Up * 0.35f,
            new Color(InsightWhite, 0.42f), 0.016f, 0.24 / speed);
        Float(sora, $"見切り {cue.StatusRemaining}%", InsightWhite, true, 3.45f);
    }

    internal void ShowInsightImpact(BattleEvent cue, IReadOnlyList<BattlePawn3D> targets, double speed)
    {
        var sora = FindPawn(cue.ActorId);
        var enemy = FindPawn(cue.TargetId);
        if (sora is null || enemy is null) return;
        speed = Math.Max(0.1, speed);
        float force = 0.8f + (cue.StatusRemaining ?? 0) / 45f;
        sora.AnimateDeflection(_camera.GlobalBasis.X * (sora.Team == 0 ? -1 : 1), 0.28 / speed);
        foreach (var target in targets)
        {
            InsightGuards++;
            Vector3 at = target.FxPoint;
            Vector3 toward = (enemy.FxPoint - at).Normalized();
            Vector3 deflected = _camera.GlobalBasis.Y * 0.55f + _camera.GlobalBasis.X * (target.Team == 0 ? -0.5f : 0.5f);
            Vector3 contact = at + toward * 0.42f;
            ShockMarkFx.Beam(_fxRoot, contact + toward * 0.55f, contact, InsightWhite, 0.035f, 0.16 / speed);
            ShockMarkFx.Beam(_fxRoot, contact, contact + deflected * force, InsightWhite, 0.045f, 0.30 / speed);
            ShockMarkFx.Sparks(_fxRoot, contact, InsightWhite, 9, 0.5f * force, 0.28 / speed);
        }
        _attackAudio.PlayShockMark(ShockMarkSound.Insight);
    }

    internal void ShowVengeance(BattlePawn3D actor, BattlePawn3D target, double speed)
    {
        VengeancePlays++;
        speed = Math.Max(0.1, speed);
        Float(actor, "仇討ち！", new Color("ff8199"), true, 3.55f);
        actor.MovementPose(0.16f, (float)(0.28 / speed));
        Vector3 side = _camera.GlobalBasis.X * 0.65f;
        Vector3 high = Vector3.Up * 0.5f;
        for (int i = -1; i <= 1; i += 2)
            ShockMarkFx.Beam(_fxRoot, target.FxPoint - side + high * i,
                target.FxPoint + side - high * i, new Color("ff8199"), 0.065f, 0.25 / speed);
        ShockMarkFx.Sparks(_fxRoot, target.FxPoint, new Color("ff8199"), 12, 0.8f, 0.28 / speed);
        PlayDirectReactionSound(actor);
    }
}
