using BattleCore;

public static partial class FxCatalog
{
    private static FxCue Poison() => new("guza.vent", "グザ 瘴気の噴き出し", "毒",
        new[] { "guza", "vio", "sid", "beni" }, FxCueKind.Scripted, PoisonOpenings, PoisonEvents);

    private static BattleEvent[] PoisonEvents() => new BattleEvent[] {
        new() { Turn=1, Kind=BattleEventKind.StatusGain, ActorId=2, TargetId=4, Text=StatusKeys.Poison,
            Amount=150, PoisonRoute=PoisonRoute.Vent },
        new() { Turn=1, Kind=BattleEventKind.StatusGain, ActorId=2, TargetId=3, Text=StatusKeys.Poison,
            Amount=150, PoisonRoute=PoisonRoute.Vent },
        new() { Turn=1, Kind=BattleEventKind.StatusGain, ActorId=2, TargetId=1, Text=StatusKeys.Poison,
            Amount=150, PoisonRoute=PoisonRoute.Vent },
    };
    private static DemoOpening[] PoisonOpenings() => new DemoOpening[] {

        new(1,0,"vio","澱み喰いのヴィオ",3,1000,1000,6,AttackPattern.Single,true,UnitCatalog.Vio.Traits),
        new(2,0,"guza","瘴気袋のグザ",2,1000,1000,2,AttackPattern.Single,false,UnitCatalog.Guza.Traits),
        new(3,0,"sid","毒吐きのスィド",0,1000,1000,4,AttackPattern.Single,true,UnitCatalog.Sid.Traits),
        new(4,1,"knight","巡礼騎士",0,1000,1000,10,AttackPattern.Single,true),
        new(5,1,"axeman","戦斧兵",1,1000,1000,10,AttackPattern.Sweep,true),
        new(6,0,"beni","毒喰らいのベニ",4,1000,1000,4,AttackPattern.Single,false,UnitCatalog.Beni.Traits),

    };
}
