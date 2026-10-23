using BattleCore;

public static partial class FxCatalog
{
    private static FxCue Swallow() => new("golm.swallow", "ゴルム 精気を呑む・吐く", "標",
        new[] { "golm" }, FxCueKind.Scripted, SwallowOpenings, SwallowEvents);

    private static BattleEvent[] SwallowEvents() => new BattleEvent[] {
        new() { Turn=1, Kind=BattleEventKind.GolmArm, ActorId=1, TargetId=2, Text=GolmArmLabels.Stomp },
        new() { Turn=1, Kind=BattleEventKind.Swallow, ActorId=1, TargetId=2, Text=SwallowLabels.Swallow },
        new() { Turn=1, Kind=BattleEventKind.Death, ActorId=1, TargetId=2 },
        new() { Turn=1, Kind=BattleEventKind.Swallow, ActorId=1, TargetId=3, Text="吐く:毒9,火3,雷1,濃2,標4" },
    };
    private static DemoOpening[] SwallowOpenings() => new DemoOpening[] {

        new(1,0,"golm","大喰らいゴルム",0,300,300,45,AttackPattern.Single,false),
        new(2,1,EnemyCatalog.Knight.Id,"弱った敵",3,25,100,10,AttackPattern.Single,false),
        new(3,1,EnemyCatalog.Knight.Id,"状態異常を浴びる大物",1,500,500,10,AttackPattern.Single,false),
        new(4,1,EnemyCatalog.Knight.Id,"前列の敵",0,100,100,10,AttackPattern.Single,false),

    };
}
