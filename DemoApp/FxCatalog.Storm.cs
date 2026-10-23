using BattleCore;

public static partial class FxCatalog
{
    private static FxCue Storm() => new("basa.storm", "バサ 嵐（両陣営の入れ替え）", "移動",
        new[] { "basa", "yomi", "sero" }, FxCueKind.Scripted, StormOpenings, StormEvents);

    private static BattleEvent[] StormEvents() => new BattleEvent[] {
        new() { Turn=1, Kind=BattleEventKind.DisarrayStage, ActorId=1, TargetId=1, Slot=3 },
        Move(2,3), Move(3,0), Move(4,3), Move(5,0),
    };
    private static DemoOpening[] StormOpenings() => new DemoOpening[] {

        new(1, 0, "basa", "バサ", 2, 100, 100, 10, AttackPattern.Sweep, true),
        new(2, 0, "yomi", "ヨミ", 0, 100, 100, 10, AttackPattern.Single, true),
        new(3, 0, "sero", "セロ", 3, 100, 100, 10, AttackPattern.Single, false),
        new(4, 1, "knight", "標的A", 0, 100, 100, 10, AttackPattern.Single, true),
        new(5, 1, "axeman", "標的B", 3, 100, 100, 10, AttackPattern.Single, true),

    };
}
