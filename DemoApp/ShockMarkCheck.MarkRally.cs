using BattleCore;
using Godot;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    private async Task CheckRallyVisuals()
    {
        BattleEvent E(BattleEventKind kind, int target, int amount = 6) => new() {
            Kind = kind, Turn = 1, ActorId = 1, TargetId = target, PartnerId = 3,
            Slot = 4, Amount = amount, HpAfter = 60, StatusRemaining = 45 };
        // 同じターンのザンの2回目を潰さず、0回復も発声に残す。
        BattleEvent[] script = [E(BattleEventKind.Heal, 3), E(BattleEventKind.MarkRally, 3),
            E(BattleEventKind.Heal, 4), E(BattleEventKind.MarkRally, 4),
            new() { Kind = BattleEventKind.Damage, Turn = 1, ActorId = 3, TargetId = 6, Reaction = true },
            E(BattleEventKind.MarkRally, 3, 0) ];
        var plan = MarkRallyPresentation.Build(script);
        Require(plan.Starts.Count == 2 && plan.Starts[0].Cues.Count == 2 && plan.Starts[5].Cues.Count == 1,
            "同じターン・同じ攻撃者の別の攻撃を束ねない");
        Require(plan.Heals.SetEquals(new[] { 0, 2 }) && plan.Ends.SetEquals(new[] { 3, 5 }), "Healとの重複を除く");
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            DemoOpening O(int id, int side, UnitDef unit, int slot) => new(id, side, unit.Id, unit.Name, slot,
                60, 100, 10, AttackPattern.Single, false, unit.Traits);
            DemoOpening[] opening = [O(1, team, UnitCatalog.Hisa, 4), O(2, team, UnitCatalog.Sora, 3),
                O(3, team, UnitCatalog.Zan, 0), O(4, team, UnitCatalog.Golm, 1), O(5, team, UnitCatalog.Tome, 2),
                new(6, 1-team, "knight", "標の敵", 0, 100, 100, 10, AttackPattern.All, false) ];
            field.BeginBattle(opening, "標の循環・演出確認", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            var hisa = field.FindPawn(1)!; var sora = field.FindPawn(2)!; var enemy = field.FindPawn(6)!;
            await Wait(0.2);
            field.ShowMarkLayer(enemy, 4, speed);
            plan.Starts[0].EnemyId = 6;
            field.ShowMarkRally(plan.Starts[0], speed);
            await Wait(0.12 / speed);
            await Capture($"rally-team{team}-speed{speed}-two-lights");
            Require(hisa.MovementPortrait == "hisa_rally" && field.MarkRallyPlays == 1 && field.MarkRallyLights == 2,
                "叫ぶ立ち絵と2本の光");
            await Wait(0.7);
            field.ShowMarkRally(plan.Starts[5], speed);
            Require(field.MarkRallyPlays == 2 && field.MarkRallyLights == 2, "満タンでも叫び、光は増やさない");
            var insight = new BattleEvent { Kind = BattleEventKind.Insight, ActorId = 2, TargetId = 6,
                Turn = 1, Slot = 4, Amount = 12, StatusRemaining = 45 };
            field.ShowInsight(sora, enemy, insight, speed);
            await Wait(0.10 / speed);
            field.ShowInsightImpact(insight, field.Pawns.Values.Where(p => p.Team == team).ToArray(), speed);
            await Wait(0.06 / speed);
            await Capture($"rally-team{team}-speed{speed}-insight");
            Require(field.InsightPlays == 1 && field.InsightGuards == 5, "全体攻撃は1件から全員の前に火花");
            field.ShowVengeance(field.FindPawn(3)!, enemy, speed);
            Require(field.Pawns.Values.Where(p => p.Team == team).All(p => p.Hp == 60), "演出がHPを変えない");
            field.BeginBattle(opening, "再戦", 0);
            Require(field.MarkRallyPlays == 0 && field.InsightPlays == 0 && field.VengeancePlays == 0, "再戦で初期化");
            await Wait(0.4);
            Require(field.MarkRallyLights == 0 && field.Pawns.Values.All(p => p.MovementPortrait is null), "古い光と立ち絵を持ち越さない");
            GD.Print($"MARK_RALLY_VISUAL_OK team={team} speed={speed}");
        }
        field.QueueFree(); await Wait(0.2);
    }
}
