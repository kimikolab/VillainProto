using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int NelTalismans { get; private set; }
    internal int NelSinkHits { get; private set; }
    internal int NelMistBranches { get; private set; }
    internal int NelLeakReceipts { get; private set; }
    internal int KarzDullPlays { get; private set; }
    internal int KarzMarkPlays { get; private set; }
    internal int KubiBatchPlays { get; private set; }
    internal int KubiDauntHits { get; private set; }

    private void ResetDebuffCastCounts()
        => NelTalismans = NelSinkHits = NelMistBranches = NelLeakReceipts = KarzDullPlays = KarzMarkPlays = KubiBatchPlays = KubiBellPlays = KubiDauntHits = 0;

    internal void ShowKarzMark(BattlePawn3D source, BattlePawn3D target)
    {
        if (!source.CanReceiveMovementImpact || !target.CanReceiveMovementImpact) return;
        KarzMarkPlays++;
        var mark = new DebuffKarzMark3D(); _fxRoot.AddChild(mark);
        mark.Start(source, target, _camera, UtsuClock);
    }

    internal async Task ShowNelTalisman(BattlePawn3D source, BattlePawn3D target)
    {
        if (!source.CanReceiveMovementImpact || !target.CanReceiveMovementImpact) return;
        int generation = _specialGeneration;
        NelTalismans++;
        var paper = new DebuffTalisman3D(); _fxRoot.AddChild(paper);
        paper.Start(source, target, _camera, UtsuClock);
        await UtsuWait(.26, generation, _ => { });
    }

    internal async Task ShowNelSink(IReadOnlyList<BattlePawn3D> targets)
    {
        int generation = _specialGeneration;
        foreach (var target in targets.Distinct().Where(p => p.CanReceiveMovementImpact))
        {
            NelSinkHits++;
            target.SinkFromDebuff(.11f);
            DebuffPressure(target, false);
        }
        await UtsuWait(.20, generation, _ => { });
    }

    internal void ShowNelLeak(BattlePawn3D source, IReadOnlyList<BattlePawn3D> intended)
    {
        if (!source.CanReceiveMovementImpact) return;
        foreach (var target in intended.Distinct().Where(p => p.CanReceiveMovementImpact))
        {
            NelMistBranches++;
            var mist = new DebuffMist3D(); _fxRoot.AddChild(mist);
            mist.Start(source, target, _camera, UtsuClock);
        }
    }

    internal void ShowNelLeakReceipt(BattlePawn3D target)
    {
        if (!target.CanReceiveMovementImpact) return;
        NelLeakReceipts++;
        target.SinkFromDebuff(.07f);
        DebuffPressure(target, false);
    }

    internal void ShowKarzDull(BattlePawn3D target)
    {
        if (!target.CanReceiveMovementImpact) return;
        KarzDullPlays++;
        var scar = new DebuffKarzDull3D(); _fxRoot.AddChild(scar);
        scar.Start(target, _camera, UtsuClock);
    }

    internal async Task ShowKubiDaunt(IReadOnlyList<BattlePawn3D> targets, BattlePawn3D? source = null)
    {
        int generation = _specialGeneration;
        KubiBatchPlays++;
        var recipients = targets.Distinct().Where(p => p.CanReceiveMovementImpact).ToArray();
        bool bell = recipients.Length > 0 && source is { UnitId: "kubi", CanReceiveMovementImpact: true };
        if (bell)
        {
            if (source!.KubiBellAge >= KubiBellFx3D.Duration) ShowKubiBell(source, recipients);
        }
        bool any = false;
        foreach (var target in recipients)
        {
            KubiDauntHits++;
            SomDaunts++; // 既存の診断が読む「萎縮を描いた総数」は、専用の絵に替えても保つ。
            target.SinkFromDebuff(.21f);
            target.SetStatusIcon(StatusKeys.Daunted, true);
            DebuffPressure(target, true);
            any = true;
        }
        if (any && !bell) _attackAudio.PlayCowedGain();
        await UtsuWait(.25, generation, _ => { });
    }

    private void DebuffPressure(BattlePawn3D target, bool daunt)
    {
        var pressure = new DebuffPressure3D(); _fxRoot.AddChild(pressure);
        pressure.Start(target, UtsuClock, daunt);
    }
}
