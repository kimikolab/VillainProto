using BattleCore;

public static partial class FxCatalog
{
    private static FxCue Dropkick() => new("hane.dropkick", "ハネ ドロップキック", "撃破・移動",
        new[] { "hane" }, FxCueKind.Scripted, DropkickOpenings, DropkickEvents);

    private static BattleEvent[] DropkickEvents() => new BattleEvent[] {
        new() { Turn=1, Kind=BattleEventKind.Blast, ActorId=1, TargetId=2, Slot=3 },
        new() { Turn=1, Kind=BattleEventKind.Attack, ActorId=1, TargetId=2, Pattern=AttackPattern.Pierce, Amount=11 },
        new() { Turn=1, Kind=BattleEventKind.Move, ActorId=1, TargetId=2, Slot=3 },
    };
    private static DemoOpening[] DropkickOpenings() => new DemoOpening[] {

        new(1, 0, "hane", "ハネ", 0, 100, 100, 11, AttackPattern.Single, true),
        new(2, 1, "knight", "押し返す相手", 0, 100, 100, 10, AttackPattern.Single, true),

    };
}
