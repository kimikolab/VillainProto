using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    private async Task CheckZanTiers()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 4.0 })
        foreach (var tier in new[] { (1, 1), (2, 1), (3, 2), (4, 3), (5, 3), (6, 4), (7, 4), (8, 5), (9, 5), (10, 7), (90, 7) })
        {
            DemoOpening[] opening = [new(1, team, "zan", "ザン", 0, 100, 100, 10, AttackPattern.Single, false),
                new(2, team, "golm", "ゴルム", 1, 100, 100, 10, AttackPattern.Single, false),
                new(3, 1 - team, "knight", "標の敵", 0, 100, 100, 10, AttackPattern.Single, false)];
            field.BeginBattle(opening, $"仇討ち {tier.Item1}回・剣閃 {tier.Item2}本", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            var zan = field.FindPawn(1)!; var enemy = field.FindPawn(3)!;
            await Wait(0.15);
            field.ShowMarkLayer(enemy, 4, speed);
            var flurry = new ZanPresentation.Flurry { Actor = 1, Enemy = 3, Damage = tier.Item1 * 20 };
            flurry.Hits.AddRange(Enumerable.Range(0, tier.Item1)); flurry.Allies.Add(2);
            double duration = field.ShowZanFlurry(zan, enemy, flurry, speed);
            Require(duration <= 0.74, "束の長さは回数が増えても上限内");
            double cutTime = tier.Item2 <= 1 ? 0.28 : tier.Item2 == 2 ? 0.33 : tier.Item2 == 3 ? 0.41 : 0.43;
            await Wait(cutTime / speed);
            if (speed == 1) await Capture($"zan-tier{tier.Item1}-team{team}-speed{speed}-cuts");
            if (tier.Item1 >= 10)
            {
                await Wait(0.06 / speed);
                if (speed == 1) await Capture($"zan-tier{tier.Item1}-team{team}-speed{speed}-finish");
            }
            await Wait((duration + 0.25) / speed);
            Require(field.ZanSlashPlays == tier.Item2, "実際に描画した剣閃の段階と上限");
            Require(field.ZanWhiteFlashes == (tier.Item1 >= 10 ? 1 : 0) && !field.ZanFlashVisible,
                "白みは10回以上の締めに1度だけで消える");
            Require(zan.Position.DistanceTo(zan.Home) < 0.01f && enemy.Hp == 100, "帰還・HP不変");
            for (int i = 0; i < tier.Item1; i++) field.RecordZanHit(zan, 20);
            field.FinishZanFlurry(flurry);
            if (speed == 1) await Capture($"zan-tier{tier.Item1}-team{team}-speed{speed}-labels");
            if (tier.Item1 == 90)
            {
                field.ShowZanFlurry(zan, enemy, flurry, speed);
                await Wait(0.29 / speed);
                field.Hide(); field.Show();
                int drawn = field.ZanSlashPlays;
                await Wait(0.8 / speed);
                Require(field.ZanSlashPlays == drawn && !field.ZanFlashVisible && field.ZanHudCount == 0,
                    "途中終了で残りの斬撃と白みを止める");
            }
            GD.Print($"ZAN_TIER_OK hits={tier.Item1} cuts={tier.Item2} team={team} speed={speed}");
        }
        field.QueueFree(); await Wait(0.2);
    }

    private void CheckZanPlan()
    {
        DemoOpening O(int id, int team, string unit) => new(id, team, unit, unit, 0, 100, 100, 10, AttackPattern.Single, true);
        DemoOpening[] opening = [O(10, 0, "zan"), O(11, 0, "hisa"), O(12, 0, "golm"), O(20, 1, "knight")];
        BattleEvent E(BattleEventKind kind, int actor, int target, bool reaction = false, bool ff = false) => new() {
            Kind = kind, Turn = 1, ActorId = actor, TargetId = target, Reaction = reaction, FriendlyFire = ff, Amount = 20 };
        var script = new List<BattleEvent> { E(BattleEventKind.Attack, 20, 12), E(BattleEventKind.Damage, 20, 12) };
        for (int i = 0; i < 12; i++)
        {
            script.Add(E(BattleEventKind.Damage, 10, 20, true));
            script.Add(E(BattleEventKind.Damage, 10, 10, true, true));
        }
        int boundary = script.Count;
        script.Add(E(BattleEventKind.Attack, 20, 12));
        script.Add(E(BattleEventKind.Damage, 20, 12));
        script.Add(E(BattleEventKind.Parry, 10, 20, true));
        script.Add(E(BattleEventKind.Death, 10, 20));
        script.Add(E(BattleEventKind.Damage, 10, 10, true, true));
        script.Add(new BattleEvent { Kind = BattleEventKind.Damage, Turn = 2, ActorId = 10, TargetId = 20,
            Pattern = AttackPattern.Single, Reaction = true });
        var plan = ZanPresentation.Build(script, opening, MarkRallyPresentation.Build(script));
        Require(plan.Starts.Count == 2 && plan.Starts[2].Hits.Count == 12 && plan.Starts[2].Damage == 240,
            "同じ攻撃の12回は1束、次のAttackは同じ敵でも別の束");
        Require(plan.Starts[2].Allies.SetEquals(new[] { 12 }), "合図は殴られた味方から");
        Require(plan.Recoils.Count == 13 && plan.Hits.Count == 13, "敵の死亡通知後の返り血も読む・自傷を斬撃に数えない");
        Require(!plan.FastEvents.Contains(boundary) && !plan.Hits.ContainsKey(script.Count - 1), "次のAttackを早送りせず、型付きの通常攻撃を混ぜない");
        GD.Print("ZAN_PLAN_OK");
    }
}
