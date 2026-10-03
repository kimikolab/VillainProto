using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class StagingEffectCheck
{
    private async Task CheckFireHits(BattlefieldView3D field)
    {
        var script = new List<BattleEvent>();
        for (int hit = 0; hit < 3; hit++)
        {
            script.Add(new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 2, Pattern = AttackPattern.Single });
            script.Add(new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 2, Amount = 10, HpAfter = 200 - hit * 34 });
            script.Add(new() { Turn = 1, Kind = BattleEventKind.FireArmor, ActorId = 3, TargetId = 1, Text = FireArmorLabels.Guard });
            for (int tick = 1; tick <= 4; tick++)
            {
                script.Add(new() { Turn = 1, Kind = BattleEventKind.Status, ActorId = 1, TargetId = 2, Text = "燃焼", TickIndex = tick, TickCount = 4 });
                script.Add(new() { Turn = 1, Kind = BattleEventKind.Damage, TargetId = 2, Amount = 6, HpAfter = 200 - hit * 34 - tick * 6 });
            }
        }
        script.Add(new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 2, TargetId = 1 }); // 破片で直撃が消えた一撃。
        script.Add(new() { Turn = 1, Kind = BattleEventKind.Status, ActorId = 2, TargetId = 1, Text = "燃焼", SourceTrait = TraitId.Inverse, InverterId = 3 });
        script.Add(new() { Turn = 1, Kind = BattleEventKind.Heal, ActorId = 3, TargetId = 1, Amount = 6, HpAfter = 100 });
        script.Add(new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 2, TargetId = 3 });
        script.Add(new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 2, TargetId = 3, Amount = 10, HpAfter = 90 });
        script.Add(new() { Turn = 1, Kind = BattleEventKind.FireArmor, ActorId = 1, TargetId = 3, Text = FireArmorLabels.Convert });
        script.Add(new() { Turn = 1, Kind = BattleEventKind.Heal, ActorId = 1, TargetId = 3, Amount = 6, HpAfter = 96 });
        script.Add(new() { Turn = 1, Kind = BattleEventKind.TurnStart });
        script.Add(new() { Turn = 1, Kind = BattleEventKind.Status, TargetId = 2, Text = "燃焼" });
        script.Add(new() { Turn = 1, Kind = BattleEventKind.Damage, TargetId = 2, Amount = 6 });
        var original = script.ToArray();
        var plan = FireHitPresentation.Build(script);
        var cues = plan.Contacts.Values.SelectMany(c => c).ToArray();
        Require(cues.Length == 5 && plan.Unpaired.Count == 0, "連撃・直撃なし・変換を着弾へ結ぶ");
        Require(cues.Take(3).All(c => c.Beat.FirePulses == 4 && c.Beat.Numbers.Single().Amount == 24), "各発24の一数字と4回の小火");
        Require(cues.Skip(3).All(c => c.Beat.FireHeal && !c.Beat.FireDamage), "反転・変換のオーラ");
        Require(!plan.Sources.ContainsKey(script.Count - 2) && script.SequenceEqual(original), "ターン頭を混ぜず台本を変更しない");
        BattleEvent[] absorbed = [
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 2, Pattern = AttackPattern.All },
            new() { Turn = 1, Kind = BattleEventKind.Status, ActorId = 1, TargetId = 3, Text = "燃焼" },
            new() { Turn = 1, Kind = BattleEventKind.Damage, TargetId = 3, Amount = 6 },
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 2 },
            new() { Turn = 1, Kind = BattleEventKind.FireArmor, ActorId = 3, TargetId = 2, Text = FireArmorLabels.Mend },
            new() { Turn = 1, Kind = BattleEventKind.Heal, ActorId = 3, TargetId = 2, Amount = 6 }];
        var shielded = FireHitPresentation.Build(absorbed);
        Require(shielded.Contacts[0].Single().Target == 3 && shielded.Contacts[3].Single().Beat.FireHeal,
            "破片で直撃が消えた範囲の巻き込み・回復も実在するAttackへ結ぶ");
        BattleEvent[] detonate = [
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 2 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 2 },
            new() { Turn = 1, Kind = BattleEventKind.Skill, ActorId = 1, Text = "起爆" },
            new() { Turn = 1, Kind = BattleEventKind.Status, ActorId = 1, TargetId = 2, Text = "燃焼" },
            new() { Turn = 1, Kind = BattleEventKind.Damage, TargetId = 2, Amount = 12 }];
        var detonated = FireHitPresentation.Build(detonate);
        Require(detonated.Contacts.Count == 0 && detonated.Unpaired.Count == 0, "起爆の見出しがある刻みを古い一撃へ合算しない");
        DemoOpening[] openings = [
            new(1, 0, "mudo", "ムド", 0, 100, 100, 10, AttackPattern.Single, true),
            new(3, 0, "hiyo", "ヒヨ", 2, 100, 100, 10, AttackPattern.Single, false),
            new(2, 1, "borg", "着弾確認", 0, 300, 300, 10, AttackPattern.Single, false)];
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            field.BeginBattle(openings, "着弾と被弾の燃焼", 1);
            var actor = field.FindPawn(1)!;
            var target = field.FindPawn(2)!;
            actor.AnimationSpeed = target.AnimationSpeed = speed;
            foreach (var pawn in field.Pawns.Values) pawn.SetFireLevel(4);
            foreach (var cue in cues)
            {
                var recipient = field.FindPawn(cue.Target)!;
                int contacts = 0;
                field.AttackContact = pawn => { if (pawn == recipient && contacts++ == 0) field.ShowFireHit(pawn, cue.Beat, speed); };
                var attack = field.Attack(cue.Target == 2 ? actor : target, recipient, AttackPattern.Single, new[] { recipient });
                await Wait(0.16 / speed);
                await attack;
                await Capture($"fire-hit-{cue.Contact}-speed{speed}");
                Require(recipient.FireInvasive == cue.Beat.FireDamage, "同じ炎の侵食／オーラ");
                await Wait(0.45 / speed);
            }
            Require(field.FireHitPlays == 5 && field.FireHitNumberPlays == 5 && field.FireHitPulses == 14, "各発の着弾で一度だけ再生");
        }
        field.BeginBattle(openings, "再開", 1);
        Require(field.FireHitPlays == 0 && field.AttackContact is null, "再開で接触予約も消す");
        if (OS.GetCmdlineUserArgs().Contains("--verify")) await CheckFireTickReplay();
    }
}
