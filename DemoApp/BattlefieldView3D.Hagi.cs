using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal static readonly Color HagiSilver = new("edf1f8");
    private int _hagiGeneration;
    internal int HagiSwings, HagiReaps, HagiPursuits, HagiLinks, HagiColumns, HagiGrazes, HagiSoulFlows;
    internal System.Collections.Generic.IEnumerable<HagiReapFx3D> HagiSweepEffects
        => _fxRoot.GetChildren().OfType<HagiReapFx3D>().Where(f => IsInstanceValid(f) && !f.IsQueuedForDeletion());
    internal System.Collections.Generic.IEnumerable<HagiReapFx3D> HagiReapEffects => HagiSweepEffects.Where(f => f.Heavy);
    private (bool, double) HagiClock() => (_deathPaused, _deathSpeed);
    private void ResetHagiPresentation()
    {
        _hagiGeneration++;
        foreach (var pawn in _pawns.Values) pawn.ResetHagiVisual();
        if (_fxRoot is not null)
        {
            foreach (var fx in _fxRoot.GetChildren().OfType<HagiLight3D>()) { fx.Hide(); fx.QueueFree(); }
            foreach (var fx in HagiSweepEffects.ToArray()) { fx.Hide(); fx.QueueFree(); }
        }
        HagiSwings = HagiReaps = HagiPursuits = HagiLinks = HagiColumns = HagiGrazes = HagiSoulFlows = 0;
    }
    internal void ApplyHagiState(HagiPresentation.State state)
    {
        FindPawn(state.Id)?.SetHagiState(state, HagiClock);
    }
    internal void ShowHagiOffering(BattlePawn3D dead)
    {
        foreach (var hagi in _pawns.Values.Where(p => p.UnitId == "hagi" && p.Team != dead.Team && p.Hp > 0))
        {
            HagiSoulFlows++;
            var fx = new HagiLight3D(); _fxRoot.AddChild(fx); fx.Particles(dead.FxPoint, hagi.FxPoint, true, true, HagiClock);
        }
    }
    internal void ShowHagiDamage(BattlePawn3D target, int amount, bool reap, bool friendly)
    {
        if (reap)
        {
            HagiReaps++; target.BeginHagiDissolve();
            // 直前の通常ダメージとの合算を避け、必殺の実値を大きな銀色の数字で描く。
            if (_numbers.Remove((target.InstanceId,false),out var previous) && LivePopup(previous.Label))
            {
                int old = _popups.FindIndex(p => p.Label == previous.Label);
                if (old >= 0) RemovePopup(old);
                else { previous.Label.Hide(); previous.Label.QueueFree(); }
            }
            var number = Float(target,"−"+amount,HagiSilver,true,3.20f,2.50f)!;
            _numbers[(target.InstanceId,false)]=(number,amount);
        }
        if (reap || friendly)
        {
            if (friendly) HagiGrazes++;
            var fx = new HagiLight3D(); _fxRoot.AddChild(fx);
            fx.Particles(target.FxPoint, target.FxPoint, false, reap, HagiClock);
            if (friendly)
            {
                var graze = new HagiLight3D(); _fxRoot.AddChild(graze);
                graze.Arc(target.FxPoint, .65f, false, false, HagiClock, _camera);
                NumberPopup(target, amount, false, HagiSilver, false);
            }
        }
    }
    internal async Task PlayHagiSwing(BattlePawn3D? actor, IReadOnlyList<BattlePawn3D> targets,
        HagiPresentation.Swing cue, Action contact)
    {
        if (actor is null || targets.Count == 0 || !actor.CanPlayAttackClip || actor.Hp <= 0) return;
        int generation = _hagiGeneration;
        bool Valid() => generation == _hagiGeneration && IsInstanceValid(actor) && IsVisibleInTree();
        bool Live() => Valid() && actor.CanPlayAttackClip && actor.Hp > 0;
        Vector3 start = actor.Position;
        Vector3 center = targets.Aggregate(Vector3.Zero, (p, target) => p + target.FxPoint) / targets.Count;
        Vector3 direction = (center - actor.FxPoint); direction.Y = 0;
        Vector3 destination = actor.Position + direction * Math.Min(.78f, Math.Max(0, direction.Length() - 1.35f) / Math.Max(.01f, direction.Length()));
        bool flip = (center - actor.FxPoint).Dot(_camera.GlobalBasis.X) < 0;
        actor.BeginHagiMotion();
        actor.SetHagiPose(null, flip);
        if (cue.Pursuit)
        {
            HagiPursuits++;
            // 残像の粒を置いてから低く滑り込む。通常の反撃カットインは挟まない。
            var shadow = new HagiLight3D(); _fxRoot.AddChild(shadow);
            shadow.Particles(actor.FxPoint, center, true, false, HagiClock);
        }
        HagiReapFx3D? reaping = null;
        Vector3 side = _camera.GlobalBasis.X.Normalized();
        float span = targets.Max(p => p.FxPoint.Dot(side)) - targets.Min(p => p.FxPoint.Dot(side));
        Vector3 Blade() => actor.MovementPortraitPoint((actor.CurrentHagiAnchors?.Tip ?? new Vector2(.60f,.035f))
            * actor.DeathSprite.Texture.GetSize(), _camera);
        Vector3 Orbit() => actor.GlobalPosition + Vector3.Up * (.95f + actor.AttackClipVertical);
        if (cue.Reap)
        {
            reaping = new HagiReapFx3D(); _fxRoot.AddChild(reaping);
            reaping.Start(center+Vector3.Up*.12f,span+4.2f,flip,_camera,
                Blade,Orbit,HagiClock);
        }
        string clip = !cue.Reap ? "hagi_spin_v1" : cue.Link ? "hagi_reap_return_v1"
            : cue.Keep ? "hagi_reap_chain_v1" : "hagi_reap_spin_v1";
        bool contacted = false;
        // 命中は全身セルのcontactで一度だけ通知する。中断時に旧台本の接触を発生させない。
        var animation = actor.PlayHagiClip(clip, () => {
            if (!Live() || !actor.AttackClipPlaying) return;
            contacted = true;
            HagiSwings++; if (cue.Link) HagiLinks++;
            if (cue.Reap && targets.Count > 1) HagiColumns++;
            if (reaping is not null) reaping.Release();
            else
            {
                var arc = new HagiReapFx3D(); _fxRoot.AddChild(arc);
                arc.Start(center+Vector3.Up*.12f,span+2.8f,flip,_camera,Blade,Orbit,HagiClock,heavy:false);
                arc.Release();
            }
            _attackAudio.PlayAttack("hagi", actor.Team, targets.Count > 1 ? AttackPattern.Sweep : AttackPattern.Single,
                cue.Pursuit, cue.Reap);
            foreach (var hit in targets) NotifyAttackContact(hit);
            contact();
        }, cue.Keep);
        await HagiWait(cue.Link ? .035 : cue.Pursuit ? .065 : .13, generation,
            t => { if (Live() && actor.AttackClipPlaying) actor.Position = start.Lerp(destination, 1 - (1 - t) * (1 - t)); });
        await animation;
        if (!Live() || !contacted)
        {
            if (IsInstanceValid(reaping)) { reaping!.Hide(); reaping.QueueFree(); }
            if (Live()) actor.Position = actor.Home;
            return;
        }
        if (cue.Keep) return;
        Vector3 end = actor.Position;
        await HagiWait(.10, generation, t => { if (Live()) actor.Position = end.Lerp(actor.Home, t); });
        if (Live()) actor.ReleaseHagiPose();
    }
    private async Task HagiWait(double seconds, int generation, Action<float>? tick = null)
    {
        double age = 0;
        while (age < seconds && generation == _hagiGeneration && IsVisibleInTree())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (generation != _hagiGeneration || !IsVisibleInTree()) return;
            if (_deathPaused) continue;
            age += GetProcessDeltaTime() * _deathSpeed;
            tick?.Invoke((float)Math.Min(1, age / seconds));
        }
    }
}
