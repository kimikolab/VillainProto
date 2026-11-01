using BattleCore;
using System.Collections.Generic;

public partial class Main
{
    // 連続する実際の付与だけを束ねる。共通デバフ演出の有無に依存せず、鐘は1組1回。
    private void ObserveKubiBell(BattleEvent e, int index, BattlePawn3D? source)
    {
        if (_result is null || source?.UnitId != "kubi" || !Gain(e)) return;
        bool Gain(BattleEvent n) => n.Kind == BattleEventKind.StatusGain && n.Text == StatusKeys.Daunted
            && n.Amount > 0 && n.ActorId == e.ActorId && n.Turn == e.Turn;
        if (index > 0 && Gain(_result.Events[index - 1])) return;
        var targets = new List<BattlePawn3D>();
        for (int i = index; i < _result.Events.Count && Gain(_result.Events[i]); i++)
            if (_battleField.FindPawn(_result.Events[i].TargetId) is { } target) targets.Add(target);
        _battleField.ShowKubiBell(source, targets);
    }
}
