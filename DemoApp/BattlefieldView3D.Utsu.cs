using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private readonly Dictionary<int, Task> _utsuTails = new();
    private readonly Dictionary<int, UtsuDanceTrail3D> _utsuTrails = new();
    internal readonly List<string> UtsuPlayedClips = new();
    internal int UtsuContacts { get; private set; }
    internal int UtsuReturns { get; private set; }
    private (bool Paused, double Speed) UtsuClock() => (_deathPaused, _deathSpeed);

    private void ResetUtsuPresentation()
    {
        foreach (var pawn in _pawns.Values) pawn.CancelUtsuMotion();
        foreach (var trail in _utsuTrails.Values) if (IsInstanceValid(trail)) { trail.Hide(); trail.QueueFree(); }
        _utsuTrails.Clear(); _utsuTails.Clear(); UtsuPlayedClips.Clear(); UtsuContacts = UtsuReturns = 0;
    }

    internal async Task PlayUtsuSwing(BattlePawn3D actor, IReadOnlyList<BattlePawn3D> targets, UtsuPresentation.Swing cue)
    {
        int generation = _specialGeneration;
        bool Live() => generation == _specialGeneration && IsInstanceValid(actor) && actor.IsInsideTree()
            && actor.CanPlayAttackClip && actor.Hp > 0 && IsVisibleInTree();
        if (_utsuTails.TryGetValue(actor.InstanceId, out var previous)) await previous;
        if (actor.AttackClipPlaying) await actor.AttackClipFinished;
        if (!Live() || targets.Count == 0) return;
        var clip = BattlePawn3D.AttackClipOf(cue.Clip)
            ?? throw new InvalidOperationException("ウツの攻撃素材がありません: " + cue.Clip);
        Vector3 start = actor.Position, target = targets[0].RestPosition;
        Vector3 direction = target - start; direction.Y = 0;
        Vector3 destination = direction.Length() > 1.28f ? target - direction.Normalized() * 1.28f : start;
        actor.HoldComboPosition();
        if (!_utsuTrails.ContainsKey(actor.InstanceId))
        {
            var trail = new UtsuDanceTrail3D(); _fxRoot.AddChild(trail);
            trail.Start(actor, _camera, UtsuClock); _utsuTrails[actor.InstanceId] = trail;
        }
        UtsuPlayedClips.Add(cue.Clip);
        var contact = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = actor.PlayUtsuClip(cue.Clip, () => {
            if (!Live() || !actor.AttackClipPlaying) return;
            UtsuContacts++;
            _attackAudio.PlayAttack("utsu", actor.Team, AttackPattern.Single, false, false);
            var blade = actor.UtsuBladeSegment(_camera, actor.UtsuActiveBlade);
            var sparks = new HagiLight3D(); _fxRoot.AddChild(sparks);
            sparks.Particles(blade.Tip, blade.Tip, false, false, UtsuClock, new Color("e6edf7"));
            foreach (var hit in targets.Distinct()) NotifyAttackContact(hit);
            contact.TrySetResult();
        }, cue.Keep, cue.Ordinal == 1 ? t => {
            if (!Live()) return;
            actor.Position = start.Lerp(destination, t);
            actor.HoldComboPosition();
        } : null);
        // 2撃目以降で対象が替わる場合の距離合わせ。初動の踏み込みは全身セルの時計で運ぶ。
        async Task Step()
        {
            double contactTime = clip.Contact / clip.Fps;
            double duration = Math.Min(.16, contactTime * .8);
            await UtsuWait(Math.Max(0, contactTime - duration - 1 / clip.Fps), generation, _ => { });
            await UtsuWait(duration, generation, t => {
                if (!Live()) return;
                actor.Position = start.Lerp(destination, t * t * (3 - 2 * t));
                actor.HoldComboPosition();
            });
        }
        var step = cue.Ordinal == 1 ? Task.CompletedTask : Step();
        if (!cue.Keep) _utsuTails[actor.InstanceId] = FinishUtsuTail(actor, animation, generation);
        // 命中の瞬間にMainへ戻し、その直後の確定Damageを既存の経路で描く。
        await Task.WhenAny(contact.Task, animation);
        await step;
    }

    private async Task FinishUtsuTail(BattlePawn3D actor, Task animation, int generation)
    {
        await animation;
        if (generation != _specialGeneration || !IsInstanceValid(actor) || !actor.CanPlayAttackClip || actor.Hp <= 0) return;
        actor.ReleaseUtsuPose();
        Vector3 start = actor.Position;
        await UtsuWait(.18, generation, t => {
            if (IsInstanceValid(actor) && actor.CanPlayAttackClip) actor.Position = start.Lerp(actor.Home, t * t * (3 - 2 * t));
        });
        if (generation != _specialGeneration || !IsInstanceValid(actor) || !actor.CanPlayAttackClip) return;
        actor.Position = actor.Home; actor.ReturnFromAttack(); UtsuReturns++;
    }

    internal async Task FinishUtsuMotion(int actor)
    {
        if (_utsuTails.TryGetValue(actor, out var tail)) await tail;
    }

    internal async Task FinishUtsuMotions()
    {
        foreach (var tail in _utsuTails.Values.ToArray()) await tail;
    }

    private async Task UtsuWait(double seconds, int generation, Action<float> tick)
    {
        double age = 0;
        while (age < seconds && generation == _specialGeneration && IsVisibleInTree())
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (generation != _specialGeneration || !IsVisibleInTree()) return;
            if (_deathPaused) continue;
            age += GetProcessDeltaTime() * _deathSpeed;
            tick((float)Math.Min(1, age / seconds));
        }
    }
}

// 二本の実際の刃の追跡点を使う。振りかぶりや着地に余計な斬撃を描かない。
public partial class UtsuDanceTrail3D : Node3D
{
    private BattlePawn3D _actor = null!;
    private Camera3D _camera = null!;
    private Func<(bool Paused, double Speed)> _clock = null!;
    private readonly RicaWeaponTrail3D[] _blades = { new(), new() };
    private double _age;
    internal void Start(BattlePawn3D actor, Camera3D camera, Func<(bool Paused, double Speed)> clock)
    {
        _actor = actor; _camera = camera; _clock = clock;
        foreach (var blade in _blades) { AddChild(blade); blade.Start(); }
    }
    public override void _Process(double delta)
    {
        var (paused, speed) = _clock();
        if (paused) return;
        _age += delta * speed;
        bool live = IsInstanceValid(_actor) && _actor.CanPlayAttackClip && _actor.Hp > 0 && IsVisibleInTree();
        for (int i = 0; i < 2; i++)
        {
            var segment = live ? _actor.UtsuBladeSegment(_camera, i) : (Vector3.Zero, Vector3.Zero);
            bool active = live && _actor.UtsuTrailActive && _actor.UtsuActiveBlade == i;
            bool preparing = live && _actor.UtsuClipName is { } name
                && _actor.AttackClipFrame < Math.Max(0, BattlePawn3D.AttackClipOf(name)!.Contact - 2);
            _blades[i].Feed(_age, false, active, live ? _actor.UtsuClipName : null, segment.Item1, segment.Item2,
                new Color("c9d9ee"), recovery: !live || !_actor.AttackClipPlaying || preparing);
        }
    }
}
