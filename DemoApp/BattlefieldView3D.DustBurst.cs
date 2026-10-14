using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    private DustBurstChain3D? _dustChain;
    internal DustBurstChain3D? DustChain => _dustChain is not null && IsInstanceValid(_dustChain) && !_dustChain.IsQueuedForDeletion() ? _dustChain : null;
    internal Task<bool> DustBurstCompletion { get; private set; } = Task.FromResult(true);
    internal int DustBurstPlays, DustChainPlays, DustDamageSummaries, DustFallPlays;
    internal int BallPowderCount => _ballPowder.Count;
    private readonly Dictionary<int, (int Density, bool Marked)> _ballPowder = new();

    internal void FinishDustFall(BattleEvent e)
    {
        if (e.Text != ShockGaugeLabels.DustFall || e.TargetId is not int id) return;
        if (FindPawn(id) is { PresentationAlive: true } pawn)
        { pawn.SetPowderMark(); PowderAura(pawn); }
        else if (_silkBalls.ContainsKey(id)) BallPowder(id, marked: true);
    }

    private TouPowderFx3D? DustFallFlight(BattleEvent e, Vector3 from, double speed)
    {
        if (ElectricPoint(e.TargetId) is not Vector3 to) return null;
        var flight = PowderEffect(TouPowderFxKind.DustFall, from, to, speed);
        flight.OnArrival(() => FinishDustFall(e));
        return flight;
    }

    private void BallPowder(int id, int? density = null, bool marked = false)
    {
        if (!_silkBalls.ContainsKey(id)) return;
        var previous = _ballPowder.GetValueOrDefault(id);
        _ballPowder[id] = (density ?? previous.Density, marked || previous.Marked);
        if (_powderAuras.ContainsKey(id)) return;
        var aura = new TouPowderAura3D(); PowderRoot.AddChild(aura);
        aura.Configure(() => ElectricPoint(id), () => _ballPowder.GetValueOrDefault(id).Density,
            () => _ballPowder.GetValueOrDefault(id).Marked, _camera, ball: true);
        aura.SetClock(_powderPaused, _powderSpeed); _powderAuras.Add(id, aura);
    }

    internal void BeginDustBurstChain(DustBurstPresentation.Chain chain, IReadOnlyList<BattleEvent> events, double speed)
    {
        DustChain?.Cancel();
        DustChainPlays++;
        var summaryPositions = chain.Totals.Keys.Where(id => ElectricPoint(id) is not null)
            .ToDictionary(id => id, id => ElectricPoint(id)!.Value);
        var fx = new DustBurstChain3D(); PowderRoot.AddChild(fx);
        fx.Configure(_camera, () => {
            foreach (var (id, total) in chain.Totals)
            {
                if (!summaryPositions.TryGetValue(id, out var point)) continue;
                DustDamageSummaries++;
                // 独立した連鎖の合計。通常攻撃の数字へ足し込まず、500以上だけ大きくする。
                TrimPopups(id);
                var label = CreatePopup(point + Vector3.Up * .4f, $"−{total}", new Color("ff7384"), total >= 500 ? 48 : 32);
                _popups.Add((id, label));
                var fade = label.CreateTween();
                fade.TweenInterval(.55);
                fade.TweenProperty(label, "modulate:a", 0f, .25);
                fade.TweenCallback(Callable.From(label.QueueFree));
            }
        });
        _dustChain = fx; DustBurstCompletion = fx.Completion;
        fx.SetClock(_powderPaused, speed);
        var arrivalTimes = new Dictionary<int, float>();
        for (int n = 0; n < chain.Discharges.Length; n++)
        {
            int index = chain.Discharges[n]; var e = events[index];
            float time = .24f * MathF.Pow(n / (float)Math.Max(1, chain.Discharges.Length - 1), .72f);
            arrivalTimes[index] = time;
            DischargePlays++;
            if (e.ActorId is int actor && _silkBalls.ContainsKey(actor) || e.TargetId is int target && _silkBalls.ContainsKey(target)) SilkDischargePlays++;
            if (ElectricPoint(e.ActorId) is not Vector3 from || ElectricPoint(e.TargetId) is not Vector3 to) continue;
            float width = .024f;
            if (e.ReceiverDensity is int receiver && receiver > 0)
                width += .008f * MathF.Sqrt(Math.Min(16, receiver));
            else if (e.Amount > 8 && e.SourceTrait != TraitId.Thread) width = TouPowderFx3D.HeavyWidth(e.Amount);
            if (e.Amount > 8 && e.SourceTrait != TraitId.Thread) PowderHeavyPlays++;
            fx.AddArc(time, from, to, width);
        }
        foreach (var burst in chain.Bursts)
        {
            var cue = events[burst.Index];
            if (ElectricPoint(cue.TargetId) is not Vector3 point) continue;
            int preceding = chain.Discharges.LastOrDefault(i => i < burst.Index, -1);
            float at = arrivalTimes.GetValueOrDefault(preceding) + .075f;
            bool ally = FindPawn(cue.TargetId)?.Team == BattleContext.PlayerTeam;
            float pan = Math.Clamp((_camera.UnprojectPosition(point).X / _viewport.Size.X - .5f) * 1.4f, -.7f, .7f);
            fx.AddBurst(at, point, cue.Slot, pan, ally, UsePurchasedPowderFx,
                (density, side) => _attackAudio.PlayDustCrackle(density, side, _powderSpeed));
            foreach (int hit in burst.Hits)
                if (ElectricPoint(events[hit].TargetId) is Vector3 end)
                    fx.AddArc(at + .02f, point, end, .011f, branch: true);
        }
        // 「粉が降る」の端点は通知を読むだけ。爆発と同じ束の中で飛行と到着を予約する。
        for (int index = chain.Start; index <= chain.End; index++)
        {
            var fall = events[index];
            if (fall.Kind != BattleEventKind.ShockGauge || fall.Text != ShockGaugeLabels.DustFall
                || ElectricPoint(fall.ActorId) is not Vector3 from) continue;
            var burst = chain.Bursts.LastOrDefault(b => b.Index < index && events[b.Index].TargetId == fall.ActorId);
            int preceding = burst is null ? -1 : chain.Discharges.LastOrDefault(i => i < burst.Index, -1);
            float at = arrivalTimes.GetValueOrDefault(preceding) + .11f;
            fx.Schedule(at, () => DustFallFlight(fall, from, _powderSpeed));
        }
    }
}
