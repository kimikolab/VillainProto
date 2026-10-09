using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private readonly Dictionary<int, HisaCommandOrbs> _commandOrbs = new();
    private readonly HashSet<int> _commandSpilled = new();
    private readonly Dictionary<int, int> _quietTurns = new();
    internal int CommandPlays, CommandBallPlays, CommandSpills, CoverPlays, QuietPlays;
    internal int CommandOrbCount(int id) => _commandOrbs.TryGetValue(id, out var orbs) ? orbs.Count : 0;

    private void ResetHisaCommand()
    {
        EndHisaCommand();
        CommandPlays = CommandBallPlays = CommandSpills = CoverPlays = QuietPlays = 0;
        _quietTurns.Clear(); _commandSpilled.Clear();
    }
    private void EndHisaCommand()
    {
        EndCommandFocus();
        foreach (var orbs in _commandOrbs.Values)
            if (IsInstanceValid(orbs)) { orbs.Hide(); orbs.QueueFree(); }
        _commandOrbs.Clear();
        foreach (var pawn in _pawns.Values) pawn.ReturnFromHisaCover();
    }
    private HisaCommandOrbs Orbs(BattlePawn3D pawn)
    {
        if (_commandOrbs.TryGetValue(pawn.InstanceId, out var orbs)) return orbs;
        orbs = new HisaCommandOrbs();
        pawn.AddChild(orbs); orbs.Configure(pawn);
        _commandOrbs[pawn.InstanceId] = orbs;
        return orbs;
    }
    internal void ShowCommandBall(BattlePawn3D? pawn, BattleEvent cue, double speed)
    {
        if (pawn is null) return;
        CommandBallPlays++;
        var orbs = Orbs(pawn);
        if (cue.Text != CommandBallLabels.Spill || orbs.Count != cue.Amount) orbs.SetCount(cue.Amount);
        if (cue.Text == CommandBallLabels.Use) _commandSpilled.Remove(pawn.InstanceId);
        if (cue.Text == CommandBallLabels.Gain) _attackAudio.PlayHisa(HisaSound.Ball);
        // 満杯の連続区間につき1回だけ。ボス戦で70件以上の捨てる通知を鳴らさない。
        if (cue.Text == CommandBallLabels.Spill && _commandSpilled.Add(pawn.InstanceId))
        {
            CommandSpills++;
            ShockMarkFx.Sparks(_fxRoot, orbs.BallPoint(2), RallyAmber, 3, 0.25f, 0.24 / Math.Max(.1, speed));
        }
    }
    private void HisaCaption(BattlePawn3D pawn, string text, Color tint, double speed)
    {
        if (_rallyCaptions.TryGetValue(pawn.InstanceId, out var old) && LivePopup(old)) { old.Hide(); old.QueueFree(); }
        var caption = CreatePopup(pawn.FxPoint + Vector3.Up * 1.65f, text, tint, 22);
        caption.AddThemeStyleboxOverride("normal", UiKit.Box(new Color("281d27"), tint, 2, 6));
        _rallyCaptions[pawn.InstanceId] = caption;
        _popups.Add((pawn.InstanceId, caption));
        var tween = caption.CreateTween();
        tween.TweenInterval(.65 / Math.Max(.1, speed));
        tween.TweenProperty(caption, "modulate:a", 0f, .18 / Math.Max(.1, speed));
        tween.TweenCallback(Callable.From(caption.QueueFree));
    }
    internal async Task ShowCommand(BattlePawn3D? pawn, BattlePawn3D? target, BattleEvent cue,
        IReadOnlyList<BattleEvent>? layers, double speed, bool freshMark = false)
    {
        if (pawn is null || target is null) return;
        int generation = _specialGeneration;
        speed = Math.Max(.1, speed); CommandPlays++;
        bool charged = cue.Text == CommandLabels.Turn && cue.Slot > 0;
        pawn.ShowMovementPortrait("hisa_command", charged ? 1.6 : 1.1, flip: (target.FxPoint - pawn.FxPoint).Dot(_camera.GlobalBasis.X) < 0);
        if (charged)
        {
            BeginCommandFocus(pawn, target, cue.Amount, 1.45 / speed);
            MakeGroundRing(pawn.Home, RallyAmber, 1.25f, .52 / speed);
            var stock = Orbs(pawn);
            for (int i = 0; i < Math.Min(3, cue.Amount); i++)
                ShockMarkFx.Ring(_fxRoot, stock.BallPoint(i), RallyAmber, .6f, .35 / speed);
        }
        HisaCaption(pawn, "ほら、あいつだよ", RallyAmber, speed);
        _attackAudio.PlayHisa(HisaSound.Command);
        await ToSignal(GetTree().CreateTimer(.24 / speed), SceneTreeTimer.SignalName.Timeout);
        if (!MarkLoopLive(generation, pawn, target)) return;
        Vector3 finger = pawn.MovementPortraitPoint(new Vector2(1007, 204), _camera);
        Vector3 to = target.FxPoint + Vector3.Up * .4f;
        var orbs = Orbs(pawn);
        var balls = new List<Sprite3D>();
        try
        {
            for (int i = 0; i < Math.Min(3, cue.Amount); i++)
            {
                Vector3 from = orbs.BallPoint(i);
                var ball = ShockMarkFx.Sprite(_fxRoot, from, ShockMarkFx.Halo, charged ? .48f : .34f, RallyAmber);
                balls.Add(ball);
                var flight = ball.CreateTween();
                flight.TweenProperty(ball, "global_position", finger, (charged ? .24 : .10) / speed)
                    .SetDelay((charged ? i * .03 : 0) / speed).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            }
            orbs.SetCount(0);
            await ToSignal(GetTree().CreateTimer((charged ? .32 : .10) / speed), SceneTreeTimer.SignalName.Timeout);
            if (!MarkLoopLive(generation, pawn, target)) return;
            if (charged)
            {
                ShockMarkFx.Glow(_fxRoot, finger, Colors.White, .9f, .30 / speed);
                ShockMarkFx.Ring(_fxRoot, finger, RallyAmber, 1.2f, .30 / speed);
                await ToSignal(GetTree().CreateTimer(.14 / speed), SceneTreeTimer.SignalName.Timeout);
                if (!MarkLoopLive(generation, pawn, target)) return;
            }
            _attackAudio.PlayHisa(HisaSound.Send);
            for (int i = 0; i < balls.Count; i++)
            {
                var ball = balls[i];
                var flight = ball.CreateTween();
                flight.TweenProperty(ball, "global_position", to, (charged ? .13 : .24) / speed)
                    .SetDelay(i * .025 / speed).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                flight.TweenCallback(Callable.From(ball.QueueFree));
            }
            // 発射数はFeatherMarkだけで決める。ここでは在庫全体の向きだけを揃える。
            foreach (var misa in _pawns.Values.Where(p => p.Team == pawn.Team && p.PresentationAlive))
                misa.MisaFeathers?.AimCommand(to);
            await ToSignal(GetTree().CreateTimer((charged ? .20 : .30) / speed), SceneTreeTimer.SignalName.Timeout);
            if (!MarkLoopLive(generation, pawn, target)) return;
            if (layers is { Count: > 0 })
            {
                MarkLayerPlays += layers.Count - 1;
                ShowMarkLayer(target, layers[^1].Amount, speed);
            }
            else if (freshMark)
            {
                target.SetStatusIcon(StatusKeys.Marked, true);
                PlayMarkAdded(target);
            }
            if (charged)
            {
                var stamp = ShockMarkFx.Sprite(_fxRoot, target.FxPoint, ShockMarkFx.BoldMark, 3.2f, RallyAmber);
                var press = stamp.CreateTween();
                press.TweenProperty(stamp, "scale", Vector3.One * .50f, .10 / speed)
                    .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                press.TweenProperty(stamp, "modulate:a", 0f, .24 / speed);
                press.TweenCallback(Callable.From(stamp.QueueFree));
                ShockMarkFx.Sparks(_fxRoot, target.FxPoint, RallyAmber, 12, 1.4f, .36 / speed);
                MakeGroundRing(target.Home, RallyAmber, 1.5f, .35 / speed);
                CameraPunch(target.FxPoint, AttackPattern.Single);
            }
            Float(target, $"号令 +{cue.Amount}層", RallyAmber, true, 3.8f);
            await ToSignal(GetTree().CreateTimer((charged ? .32 : .20) / speed), SceneTreeTimer.SignalName.Timeout);
        }
        finally
        {
            // 溜めの途中で離脱・死亡・再戦した場合も、指先に玉を残さない。
            foreach (var ball in balls)
                if (IsInstanceValid(ball) && !ball.IsQueuedForDeletion()) ball.QueueFree();
        }
    }
    internal async Task ShowHisaCover(BattlePawn3D? hisa, BattlePawn3D? ally, BattlePawn3D? enemy, double speed)
    {
        if (hisa is null || ally is null) return;
        int generation = _specialGeneration;
        CoverPlays++;
        // 大柄な味方の背中に埋もれないよう、カメラ側にも一歩出す。席は変えない。
        hisa.BeginHisaCover(ally.Position, enemy?.Position ?? ally.Position + Vector3.Right,
            _camera.GlobalBasis.Z * 1.3f + _camera.GlobalBasis.X * (hisa.Team == 0 ? .65f : -.65f));
        await ToSignal(GetTree().CreateTimer(.22 / Math.Max(.1, speed)), SceneTreeTimer.SignalName.Timeout);
        if (!MarkLoopLive(generation, hisa, ally)) return;
        HisaCaption(hisa, "…こっちだ", RallyAmber, speed);
        _attackAudio.PlayHisa(HisaSound.Cover);
        await ToSignal(GetTree().CreateTimer(.20 / Math.Max(.1, speed)), SceneTreeTimer.SignalName.Timeout);
    }
    internal void ShowHisaQuiet(BattlePawn3D? hisa, int turn, double speed)
    {
        if (hisa?.UnitId != "hisa" || _quietTurns.GetValueOrDefault(hisa.InstanceId, -1) == turn) return;
        _quietTurns[hisa.InstanceId] = turn; QuietPlays++;
        hisa.StopHisaGesture();
        HisaCaption(hisa, "…", new Color("b9aecb"), speed);
        _attackAudio.PlayHisa(HisaSound.Quiet);
    }
}
