using BattleCore;

public static partial class FxCatalog
{
    private static FxCue Thunder() => new("kata.thunder.chain", "カタ 雷の跳ねと放電（3体）", "雷",
        new[] { "kata", "beni" }, FxCueKind.Scripted, ThunderOpenings, ThunderEvents);

    private static BattleEvent[] ThunderEvents() => new BattleEvent[] {
        new() { Turn=1, Kind=BattleEventKind.Skill, ActorId=1, Text="雷を落とした" },
        new() { Turn=1, Kind=BattleEventKind.Thunder, ActorId=1, TargetId=3, Slot=1, StatusRemaining=3 },
        new() { Turn=1, Kind=BattleEventKind.StatusGain, ActorId=1, TargetId=3, Text=StatusKeys.Shock, Amount=1 },
        new() { Turn=1, Kind=BattleEventKind.Thunder, ActorId=1, TargetId=4, Slot=2, StatusRemaining=1 },
        new() { Turn=1, Kind=BattleEventKind.StatusGain, ActorId=1, TargetId=4, Text=StatusKeys.Shock, Amount=1 },
        new() { Turn=1, Kind=BattleEventKind.StatusGain, ActorId=1, TargetId=5, Text=StatusKeys.Shock, Amount=1 },
        new() { Turn=1, Kind=BattleEventKind.ShockSpent, ActorId=3, TargetId=3, Slot=0 },
        new() { Turn=1, Kind=BattleEventKind.Discharge, ActorId=3, TargetId=4 },
        new() { Turn=1, Kind=BattleEventKind.Discharge, ActorId=3, TargetId=5 },
        new() { Turn=1, Kind=BattleEventKind.ShockSpent, ActorId=4, TargetId=4, Slot=1 },
        new() { Turn=1, Kind=BattleEventKind.Discharge, ActorId=4, TargetId=5 },
        new() { Turn=1, Kind=BattleEventKind.Discharge, ActorId=1, TargetId=2, SourceTrait=TraitId.Inverse },
    };
    private static DemoOpening[] ThunderOpenings() => new DemoOpening[] {

        new(1,0,"kata","禍導のカタ",2,48,48,6,AttackPattern.Single,false),
        new(2,0,"beni","毒喰らいのベニ",0,64,64,4,AttackPattern.Single,false,UnitCatalog.Beni.Traits),
        new(3,1,"knight","巡礼騎士",0,100,100,10,AttackPattern.Single,false),
        new(4,1,"knight","巡礼騎士",2,100,100,10,AttackPattern.Single,false),
        new(5,1,"knight","巡礼騎士",4,100,100,10,AttackPattern.Single,false)
    };
}
