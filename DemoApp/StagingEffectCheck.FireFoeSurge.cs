using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class StagingEffectCheck
{
    private async Task CheckFireFoeSurge(BattlefieldView3D field)
    {
        DemoOpening[] openings = [
            new(1, 0, "borg", "ボルグ", 0, 100, 100, 20, AttackPattern.Sweep, true),
            new(2, 0, "hiyo", "ヒヨ", 2, 100, 100, 20, AttackPattern.Single, false),
            new(3, 1, "knight", "敵A", 0, 300, 300, 20, AttackPattern.Single, true),
            new(4, 1, "knight", "敵B", 2, 300, 300, 20, AttackPattern.Single, true),
            new(5, 1, "knight", "敵C", 4, 300, 300, 20, AttackPattern.Single, true)];
        foreach (double speed in new[] { 1.0, 2.0 })
        foreach (int level in new[] { 3, 4 })
        {
            field.BeginBattle(openings, "爆炎で敵陣の火が跳ね上がる", 3);
            var actor = field.FindPawn(1)!; var ally = field.FindPawn(2)!;
            actor.AnimationSpeed = ally.AnimationSpeed = speed;
            actor.SetFireLevel(4); ally.SetFireLevel(2);
            var hits = new[] { field.FindPawn(3)!, field.FindPawn(4)!, field.FindPawn(5)! };
            foreach (var p in hits) { p.AnimationSpeed = speed; p.SetFireLevel(1); }
            await field.PlayFireAttack(actor, hits[0], hits, new(FireLevelLabels.Blaze, 4, 0), speed);
            BattleEvent[] events = hits.Select(p => new BattleEvent { Turn = 1, Kind = BattleEventKind.FireLevel,
                ActorId = 1, TargetId = p.InstanceId, Text = FireLevelLabels.BlazeFoeSurge, Amount = level, Slot = 1 }).ToArray();
            var plan = new FirePresentation(events);
            Require(plan.FoeSurges.Count == 1 && plan.FoeSurgeMembers.Count == 3, "3件を一斉の跳ね上がりへ");
            field.ShowFireFoeSurge(plan.FoeSurges[0], speed);
            foreach (var e in events) field.ShowFireCue(e, actor, field.FindPawn(e.TargetId), speed, groupedFoeSurge: true);
            Require(field.FireFoeSurgePlays == 1 && field.FireFoeSurgeTargets == 3 && field.FireCuePlays == 3,
                "一拍で全員を表示し、通知は元の件数で通す");
            Require(hits.All(p => p.FireLevel == level && p.FireInvasive) && actor.FireLevel == 4 && ally.FireLevel == 2,
                "実際のAmountへ跳ね上げ、味方の火勢を変えない");
            await Wait(0.12 / speed); await Capture($"fire-foesurge-level{level}-speed{speed}");
            await Wait(0.75 / speed);
        }
        BattleEvent[] boundary = [
            new() { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, TargetId = 3, Text = FireLevelLabels.BlazeFoeSurge, Amount = 4 },
            new() { Turn = 1, Kind = BattleEventKind.Death, TargetId = 4 },
            new() { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, TargetId = 5, Text = FireLevelLabels.BlazeFoeSurge, Amount = 4 }];
        Require(new FirePresentation(boundary).FoeSurges.Count == 2, "死亡を跨いで束ねない");
        field.BeginBattle(openings, "再開", 3);
        Require(field.FireFoeSurgePlays == 0 && field.FireFoeSurgeTargets == 0, "再開で古い敵上げを残さない");
        if (OS.GetCmdlineUserArgs().Contains("--verify")) await CheckFireReplay("W4", true);
    }
}
