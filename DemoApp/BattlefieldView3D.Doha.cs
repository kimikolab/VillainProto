using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private readonly HashSet<DohaShareFx> _dohaShares = new();
    internal int ActiveDohaShares => _dohaShares.Count;
    internal int SharePowerBatches, SharePowerLinks, SharePowerAmount;
    internal bool DohaPurchasedSmokeEnabled { get; set; } = true;

    private void EndDohaShares()
    {
        foreach (var fx in _dohaShares) if (IsInstanceValid(fx)) fx.Cancel();
    }

    internal Task ShowSharePower(BattlePawn3D? doha, BattlePawn3D? ally, BattleEvent cue, double speed)
        => doha is null || ally is null ? Task.CompletedTask : ShowShareBatch(doha, new[] { cue }, speed);

    internal Task ShowSharePowers(IReadOnlyList<BattleEvent> cues, double speed)
        => Task.WhenAll(cues.GroupBy(e => e.ActorId).Select(group => FindPawn(group.Key) is { } doha
            ? ShowShareBatch(doha, group.ToArray(), speed) : Task.CompletedTask));

    private async Task ShowShareBatch(BattlePawn3D doha, IReadOnlyList<BattleEvent> cues, double speed)
    {
        int generation = _specialGeneration;
        var deliveries = cues.GroupBy(e => e.TargetId)
            .Select(group => (Ally: FindPawn(group.Key), Amount: group.Sum(e => e.Amount)))
            .Where(d => d.Ally is not null && MarkLoopLive(generation, doha, d.Ally)).ToArray();
        if (deliveries.Length == 0) return;
        SharePowerPlays += cues.Count; // 台本の件数と、画面の往復数は分けて数える。
        SharePowerBatches++;
        SharePowerLinks += deliveries.Length;
        SharePowerAmount += deliveries.Sum(d => d.Amount);
        speed = Math.Max(0.1, speed);
        bool flip = (deliveries[0].Ally!.FxPoint - doha.FxPoint).Dot(_camera.GlobalBasis.X) < 0;
        int pose = doha.BeginSharePain(flip, hold: true);
        Vector3 to = doha.ShareChestPoint(_camera);
        var flows = new List<(BattlePawn3D Ally, int Amount, Vector3 From, DohaShareFx Fx)>();
        foreach (var delivery in deliveries)
        {
            var ally = delivery.Ally!;
            var fx = new DohaShareFx();
            _fxRoot.AddChild(fx); _dohaShares.Add(fx);
            flows.Add((ally, delivery.Amount, ally.FxPoint, fx));
            fx.TreeExiting += () => {
                _dohaShares.Remove(fx);
                if (IsInstanceValid(doha) && flows.All(f => !IsInstanceValid(f.Fx) || f.Fx.IsQueuedForDeletion()))
                    doha.EndSharePortrait(pose);
            };
            fx.Start(ally.FxPoint, to, _camera, speed, () => MarkLoopLive(generation, doha, ally),
                DohaPurchasedSmokeEnabled, showImpact: flows.Count == 1);
        }
        bool Live(DohaShareFx fx, BattlePawn3D ally) => IsInstanceValid(fx) && !fx.IsQueuedForDeletion()
            && MarkLoopLive(generation, doha, ally);
        float Pan(Vector3 at) => Math.Clamp((_camera.UnprojectPosition(at).X / _viewport.Size.X - 0.5f) * 1.4f, -0.7f, 0.7f);
        await Task.WhenAll(flows.Select(f => f.Fx.Arrival));
        if (!flows.Any(f => Live(f.Fx, f.Ally))) return;
        _attackAudio.PlayShockMark(ShockMarkSound.DohaPain, Pan(to), speed);
        await Task.WhenAll(flows.Select(f => f.Fx.ReadyToGive));
        var live = flows.Where(f => Live(f.Fx, f.Ally)).ToArray();
        if (live.Length == 0) return;
        bool giving = doha.GiveSharePower(pose, flip);
        Vector3 chest = doha.ShareChestPoint(_camera);
        Vector3 hand = giving ? doha.ShareHandPoint(_camera) : chest;
        ShockMarkFx.Glow(live[0].Fx, chest, ShockMarkFx.Gold, 0.55f, 0.22 / speed);
        _attackAudio.PlayShockMark(ShockMarkSound.DohaGive, Pan(chest), speed);
        foreach (var (ally, amount, from, fx) in live)
        {
            var tag = ShockMarkFx.Sprite(fx, chest, UiKit.LoadTexture("res://assets/fx/doha_power_tag.svg"), 0.40f, Colors.White);
            var outward = tag.CreateTween();
            outward.TweenProperty(tag, "global_position", hand, 0.06 / speed);
            outward.TweenMethod(Callable.From<float>(p => {
                tag.GlobalPosition = hand.Lerp(from, p) + Vector3.Up * Mathf.Sin(p * Mathf.Pi) * 0.45f;
            }), 0f, 1f, 0.22 / speed);
            outward.TweenCallback(Callable.From(() => {
                if (!Live(fx, ally)) return;
                ShockMarkFx.Sparks(fx, from, ShockMarkFx.Gold, 5, 0.4f, 0.2 / speed);
                Float(ally, $"力 +{amount}", ShockMarkFx.Gold, false, 2.65f);
            }));
            outward.TweenCallback(Callable.From(tag.QueueFree));
            fx.Finish(0.55 / speed);
        }
    }
}
