using BattleCore;
using System;
using System.Collections.Generic;

public static partial class FxCatalog
{
    private static FxCue? RicaIdleAlternative(int stage)
    {
        if (!Godot.FileAccess.FileExists($"res://assets/portraits/battle/idle_loop/rica_stage{stage}_b/motion.json")) return null;
        var cue=RicaMotion(stage,true);
        return cue with { Id=cue.Id+".b",Title=cue.Title+" B候補",RicaIdleVariant="B" };
    }
    private static FxCue RicaMotion(int stage, bool idle)
    {
        string title = stage switch { 0 => "通常 0–2層・シャベル", 1 => "霊刃 3–5層・回転薙ぎ", 2 => "大剣 6–8層・二連撃", _ => "覚醒 9層以上・三連撃" };
        return new FxCue($"rica.stage{stage}." + (idle ? "idle" : "attack"),
            "リィカ " + title + (idle ? "（待機）" : "（攻撃）"), "死・リィカの覚醒", new[] { "rica" }, FxCueKind.Scripted,
            RicaMotionOpenings, () => idle ? Array.Empty<BattleEvent>() : RicaMotionEvents(stage),
            HoldSeconds: idle ? 4 : 0, RicaLayers: stage * 3);
    }

    private static DemoOpening[] RicaMotionOpenings() => new[] {
        new DemoOpening(1,0,"rica","リィカ",2,300,300,40,AttackPattern.Single,true),
        new DemoOpening(2,1,"knight","標的1",0,1000,1000,10,AttackPattern.Single,true),
        new DemoOpening(3,1,"knight","標的2",2,1000,1000,10,AttackPattern.Single,true),
        new DemoOpening(4,1,"knight","標的3",4,1000,1000,10,AttackPattern.Single,true),
    };

    // 表示用の固定台本。本編の威力・標的・打数の規則をここで判定しない。
    private static BattleEvent[] RicaMotionEvents(int stage)
    {
        var events = new List<BattleEvent> { new() { Turn=1,Kind=BattleEventKind.TurnStart } };
        int count = stage < 2 ? 1 : stage == 2 ? 2 : 3;
        for (int k=0;k<count;k++)
        {
            int target = k+2;
            var pattern=stage==0?AttackPattern.Single:AttackPattern.Sweep;
            events.Add(new() { Turn=1,Kind=BattleEventKind.Attack,ActorId=1,TargetId=target,Amount=40,Pattern=pattern });
            int[] hits=stage==0?new[]{target}:new[]{2,3,4};
            foreach(int hit in hits)
                events.Add(new() { Turn=1,Kind=BattleEventKind.Damage,ActorId=1,TargetId=hit,Amount=40,HpAfter=1000-40*(k+1),Pattern=pattern });
        }
        return events.ToArray();
    }
}
