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
    private readonly Dictionary<int, HagiReapFx3D> _hagiAirCuts = new();
    internal int HagiSwings, HagiReaps, HagiPursuits, HagiLinks, HagiColumns, HagiGrazes, HagiSoulFlows, HagiReturns;
    internal int HagiAirOpeners, HagiAirFinishers, HagiAirFollows, HagiLandings;
    internal System.Collections.Generic.IEnumerable<HagiReapFx3D> HagiSweepEffects
        => _fxRoot.GetChildren().OfType<HagiReapFx3D>().Where(f => IsInstanceValid(f) && !f.IsQueuedForDeletion());
    internal System.Collections.Generic.IEnumerable<HagiReapFx3D> HagiReapEffects => HagiSweepEffects.Where(f => f.Heavy);
    private (bool, double) HagiClock() => (_deathPaused, _deathSpeed);
    private void ResetHagiPresentation()
    {
        _hagiGeneration++;
        _hagiAirCuts.Clear();
        foreach (var pawn in _pawns.Values) pawn.ResetHagiVisual();
        if (_fxRoot is not null)
        {
            foreach (var fx in _fxRoot.GetChildren().OfType<HagiLight3D>()) { fx.Hide(); fx.QueueFree(); }
            foreach (var fx in HagiSweepEffects.ToArray()) { fx.Hide(); fx.QueueFree(); }
        }
        HagiSwings = HagiReaps = HagiPursuits = HagiLinks = HagiColumns = HagiGrazes = HagiSoulFlows = HagiReturns = 0;
        HagiAirOpeners = HagiAirFinishers = HagiAirFollows = HagiLandings = 0;
    }
    internal void ApplyHagiState(HagiPresentation.State state)
    {
        FindPawn(state.Id)?.SetHagiState(state, HagiClock);
    }
    internal void ShowHagiOffering(BattlePawn3D source, BattlePawn3D hagi, int origin = ReapLabels.SoulDeath)
    {
        HagiSoulFlows++;
        var fx = new HagiLight3D(); _fxRoot.AddChild(fx);
        fx.Particles(source.FxPoint, hagi.FxPoint, true, true, HagiClock, HagiLight3D.SoulTint(origin), () => hagi.FxPoint);
    }
    internal void ShowHagiDamage(BattlePawn3D target, int amount, bool reap, bool friendly, bool weighted = false)
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
        if (weighted && !reap && !friendly)
        {
            target.AnimateHit();
            Float(target, "−" + amount, new Color("ffc1a8"), true, 2.6f, 1.65f);
            var impact = new HagiLight3D(); _fxRoot.AddChild(impact);
            impact.Particles(target.FxPoint, target.FxPoint, false, false, HagiClock);
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
        HagiPresentation.Swing cue, Action contact, BattlePawn3D? partner = null)
    {
        if (actor is null || targets.Count == 0 || !actor.CanPlayAttackClip || actor.Hp <= 0) return;
        int generation = _hagiGeneration;
        bool Valid() => generation == _hagiGeneration && IsInstanceValid(actor) && IsVisibleInTree();
        bool Live() => Valid() && actor.CanPlayAttackClip && actor.Hp > 0;
        Vector3 start = actor.Position;
        Vector3 center = targets.Aggregate(Vector3.Zero, (p, target) => p + target.FxPoint) / targets.Count;
        Vector3 direction = (center - actor.FxPoint); direction.Y = 0;
        Vector3 destination = actor.Position + direction * Math.Min(.78f, Math.Max(0, direction.Length() - 1.35f) / Math.Max(.01f, direction.Length()));
        bool air = cue.Air != HagiPresentation.AirKind.None, airRoot = air && !cue.AirFollow;
        if (cue.AirFollow || (!airRoot && (cue.Return || cue.Reverse))) destination = start;
        bool flip = (center - actor.FxPoint).Dot(_camera.GlobalBasis.X) < 0;
        bool rotational = !air && (cue.Return || cue.Reverse || !cue.Reap);
        bool backStroke = cue.Return || cue.Reverse;
        if (rotational && actor.HagiClipName is { } held && held.StartsWith("hagi_orbit_", StringComparison.Ordinal))
            backStroke = held.StartsWith("hagi_orbit_out", StringComparison.Ordinal);
        bool reverse = flip ^ (!air && (rotational ? backStroke : cue.Reverse));
        Vector3 side = _camera.GlobalBasis.X.Normalized();
        float approachSide = cue.Pursuit && partner is not null
            ? Math.Clamp((partner.FxPoint - actor.FxPoint).Dot(side), -.35f, .35f) : 0;
        Vector3 Approach(float t) => start.Lerp(destination, 1 - (1 - t) * (1 - t))
            + side * (Mathf.Sin(t * Mathf.Pi) * approachSide);
        actor.BeginHagiMotion();
        actor.SetHagiPose(null, flip);
        if (cue.Pursuit)
        {
            HagiPursuits++;
            // 仲間の一撃の側へ寄る軌道。魂の粒や反撃カットインを挟まず、赤い影だけを残す。
            var shadow = new HagiLight3D(); _fxRoot.AddChild(shadow);
            shadow.Ghost(actor.DeathSprite, HagiClock, .32f);
        }
        HagiReapFx3D? reaping = null;
        bool sharedAirCut = cue.AirFollow && _hagiAirCuts.Remove(actor.InstanceId, out reaping)
            && IsInstanceValid(reaping) && !reaping!.IsQueuedForDeletion() && reaping.Visible && reaping.Released;
        if (!sharedAirCut) reaping = null;
        float span = targets.Max(p => p.FxPoint.Dot(side)) - targets.Min(p => p.FxPoint.Dot(side));
        Vector3 Blade() => actor.HagiBladePoint(actor.CurrentHagiAnchors?.Tip ?? new Vector2(.60f,.035f));
        // 空中斬りは頭上から足元へ抜ける縦の面。直後の刈るも同方向で重ねる。
        // 斬撃の中心を地面基準にして、体の下降で弧が沈んだり持ち上がったりしない。
        Vector3 Orbit() => actor.GlobalPosition + (air ? side * (flip ? -.45f : .45f) : Vector3.Zero)
            + Vector3.Up * (air ? Math.Max(1.85f, (reaping?.SweepRadius ?? 2.1f) * .98f + .18f)
                : .95f + actor.AttackClipVertical);
        if (!sharedAirCut && (cue.Reap || airRoot))
        {
            reaping = new HagiReapFx3D(); _fxRoot.AddChild(reaping);
            reaping.Start(center+Vector3.Up*.12f,span+4.2f,reverse,_camera,
                Blade,Orbit,HagiClock, powerScale: cue.Weighted ? 1.2f : 1, echoes: cue.Encore,
                tiltDegrees: air ? 0 : cue.Return ? 15 : cue.Reverse ? 40 : 0,
                radiusScale: air ? .82f : cue.Return ? .70f : 1,
                pitchDegrees: air ? (flip ? -78 : 78) : 0, crescent: air);
        }
        if (airRoot && cue.Keep && cue.NextReturn && reaping is not null)
            _hagiAirCuts[actor.InstanceId] = reaping;
        string airKey = cue.Air == HagiPresentation.AirKind.Finisher ? "air_finish" : "air_reap";
        string clip = cue.AirFollow
                ? cue.Keep ? "hagi_air_after_hold_v6"
                    : cue.Air == HagiPresentation.AirKind.Finisher ? "hagi_air_after_finish_v6" : "hagi_air_after_v6"
            : airRoot ? "hagi_" + airKey + (cue.Keep ? cue.NextReturn ? "_cut_hold_v6" : "_hold_v6" : "_v6")
            : rotational ? backStroke
                ? cue.Keep ? "hagi_orbit_back_hold_v3" : "hagi_orbit_back_v3"
                : cue.Keep ? "hagi_orbit_out_hold_v3" : "hagi_orbit_out_v3"
            : cue.Link ? "hagi_reap_return_v1"
            : cue.Keep ? "hagi_reap_chain_v1" : "hagi_reap_spin_v1";
        bool contacted = false;
        // 命中は全身セルのcontactで一度だけ通知する。中断時に旧台本の接触を発生させない。
        var animation = actor.PlayHagiClip(clip, () => {
            if (!Live() || !actor.AttackClipPlaying) return;
            contacted = true;
            if (cue.Return) HagiReturns++; else HagiSwings++;
            if (cue.AirFollow) HagiAirFollows++;
            else if (cue.Air == HagiPresentation.AirKind.Opener) HagiAirOpeners++;
            else if (cue.Air == HagiPresentation.AirKind.Finisher) HagiAirFinishers++;
            if (cue.Link) HagiLinks++;
            if (cue.Reap && targets.Count > 1) HagiColumns++;
            if (reaping is not null)
            {
                if (sharedAirCut) reaping.Reinforce(); else reaping.Release();
            }
            else
            {
                var arc = new HagiReapFx3D(); _fxRoot.AddChild(arc);
                arc.Start(center+Vector3.Up*.12f,span+2.8f,reverse,_camera,Blade,Orbit,HagiClock,
                    heavy:false,powerScale:cue.Weighted ? 1.35f : 1,echoes:cue.Encore,
                    tiltDegrees: cue.Reverse ? 40 : 0);
                arc.Release();
            }
            _attackAudio.PlayAttack("hagi", actor.Team, targets.Count > 1 ? AttackPattern.Sweep : AttackPattern.Single,
                cue.Pursuit, cue.Reap || airRoot);
            foreach (var hit in targets) NotifyAttackContact(hit);
            contact();
        }, cue.Keep, air ? () => {
            if (!Live()) return;
            HagiLandings++; _attackAudio.PlayMovementSound(MovementSound.Landing);
        } : null);
        int ghosts = 0, ghostCount = cue.Encore > 0 ? cue.Encore + 1 : cue.Pursuit ? 1 : 0;
        await HagiWait(airRoot ? .32 : cue.Return ? .16 : cue.Link ? .025 : cue.Pursuit ? .065 : .13, generation,
            t => {
                if (!Live() || !actor.AttackClipPlaying) return;
                while (ghosts < ghostCount && t >= (ghosts + 1f) / (ghostCount + 1))
                {
                    float at = (ghosts + 1f) / (ghostCount + 1);
                    Vector3 sample = Approach(at);
                    var ghost = new HagiLight3D(); _fxRoot.AddChild(ghost);
                    ghost.Ghost(actor.DeathSprite, HagiClock, .22f + cue.Encore * .12f,
                        actor.GetParent<Node3D>().GlobalBasis * (sample - actor.Position)); ghosts++;
                }
                actor.Position = Approach(t);
            });
        // 振り抜きと刃を運び直す姿勢まで見せ、保持した最終姿勢から次の刃へ渡す。
        await animation;
        if (!Live() || !contacted)
        {
            _hagiAirCuts.Remove(actor.InstanceId);
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
