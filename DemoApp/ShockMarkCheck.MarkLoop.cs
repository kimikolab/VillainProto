using BattleCore;
using Godot;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    private async Task CheckMarkLoopVisuals()
    {
        // 予定8太刀でも、実際の台本が2太刀で終われば2太刀目で帰還する。
        BattleEvent E(BattleEventKind kind, int? target = null, string? text = null) => new() {
            Kind = kind, Turn = 1, ActorId = 3, TargetId = target, Text = text, Amount = 8 };
        BattleEvent[] script = [E(BattleEventKind.VendettaRound, text: VendettaRoundLabels.Start),
            E(BattleEventKind.VendettaRound, 10, VendettaRoundLabels.Slash), E(BattleEventKind.Attack, 10),
            E(BattleEventKind.Damage, 10), E(BattleEventKind.VendettaRound, 11, VendettaRoundLabels.Slash),
            E(BattleEventKind.Attack, 11), E(BattleEventKind.Damage, 11), E(BattleEventKind.Death, 11)];
        var plan = MarkLoopPresentation.Build(script);
        Require(plan.LastSlashes.SetEquals(new[] { 4 }) && plan.RoundEnds.Keys.SequenceEqual(new[] { 6 })
            && plan.RoundAttacks.SetEquals(new[] { 2, 5 }), "予定数ではなく実際の終太刀を使う");
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            DemoOpening O(int id, UnitDef unit, int slot) => new(id, team, unit.Id, unit.Name, slot,
                60, 100, 10, AttackPattern.Single, false, unit.Traits);
            DemoOpening[] opening = [O(1, UnitCatalog.Tome, 2), O(2, UnitCatalog.Hisa, 4),
                O(3, UnitCatalog.Zan, 0), O(4, UnitCatalog.Golm, 1), O(5, UnitCatalog.Doha, 3),
                .. Enumerable.Range(0, 7).Select(i => new DemoOpening(10 + i, 1 - team, "knight", "標の敵", i,
                    100, 100, 10, AttackPattern.Single, false))];
            field.BeginBattle(opening, "標の循環・演出確認", 3);
            foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
            var misa = field.FindPawn(1)!; var hisa = field.FindPawn(2)!; var zan = field.FindPawn(3)!;
            var ally = field.FindPawn(4)!; var doha = field.FindPawn(5)!; var enemy = field.FindPawn(10)!;
            var feathers = misa.MisaFeathers!;
            feathers.SetCount(5);
            await Wait(0.75 / speed);
            Require(feathers.IsDeployed && feathers.VisibleCount == 5, "在庫の羽が手番外にも常駐");
            Vector3 nearest = feathers.VisiblePositions.OrderBy(p => p.DistanceSquaredTo(ally.FxPoint)).First();
            await field.PlayFeatherMark(misa, ally, new() { Kind = BattleEventKind.FeatherMark, Turn = 1, Text = FeatherMarkLabels.Ally }, speed);
            Require(feathers.LastMuzzle.DistanceTo(nearest) < 0.5f && feathers.Count == 5, "近い羽から誤射・在庫不変");
            field.ShowBeckonFeather(ally, misa, 12, speed);
            await Capture($"loop-team{team}-speed{speed}-feather-guard");
            field.ShowFramed(hisa, enemy, new() { Kind = BattleEventKind.Framed, Turn = 1, Text = FramedLabels.Accuse }, speed);
            field.ShowFramed(zan, enemy, new() { Kind = BattleEventKind.Framed, Turn = 1, Text = FramedLabels.Vendetta }, speed);
            await Capture($"loop-team{team}-speed{speed}-accuse");
            await field.PlayFeatherMark(misa, enemy, new() { Kind = BattleEventKind.FeatherMark, Turn = 1, Text = FeatherMarkLabels.Foe }, speed);
            Require(field.FeatherMarkPlays == 2 && field.FeatherMarkAllyPlays == 1, "敵と味方の標撃ち");
            var share = field.ShowSharePower(doha, ally, new() { Kind = BattleEventKind.ShareGive, Turn = 1, Amount = 2 }, speed);
            await Wait(0.05 / speed);
            await Capture($"loop-team{team}-speed{speed}-pain");
            await share;
            await Wait(0.04 / speed);
            await Capture($"loop-team{team}-speed{speed}-power");
            var rally = new MarkRallyPresentation.Rally { EnemyId = 10 };
            rally.Cues.Add(new() { Kind = BattleEventKind.MarkRally, Turn = 1, ActorId = 2, TargetId = 2, Amount = 5, Slot = 3 });
            field.ShowMarkRally(rally, speed);
            Require(field.MarkRallyLights == 1, "叫びが自分を癒す光");
            await Wait(0.10 / speed);
            await Capture($"loop-team{team}-speed{speed}-self-heal");
            // ボス5太刀・8太刀、7体を8太刀。台本の数だけ描き、最後だけ大きくする。
            foreach (int count in new[] { 5, 8, 8 })
            {
                bool tour = field.RoundStarts == 2;
                field.ShowRoundStart(zan, new() { Kind = BattleEventKind.VendettaRound, Turn = 1, Amount = count, Slot = tour ? 7 : 1 }, speed);
                int travels = field.RoundTravels;
                for (int i = 1; i <= count; i++)
                {
                    var target = field.FindPawn(tour ? 10 + System.Math.Max(0, i - 2) : 10)!;
                    await field.ShowRoundSlash(zan, target, new() { Kind = BattleEventKind.VendettaRound, Turn = 1, Amount = count, Slot = i }, i == count, speed);
                    Require(zan.RoundMoving && zan.Position.DistanceTo(zan.Home) > 0.1f, "各太刀の間に席へ戻らない");
                    if (i == 3 || i == count) await Capture($"loop-team{team}-speed{speed}-round{field.RoundStarts}-cut{i}");
                }
                Require(field.RoundTravels - travels == (tour ? 7 : 1), "ボスの間合いに留まり、7体は7箇所を巡る");
                zan.ReturnFromRound();
                // 描画・PNG保存に時間がかかっても、タイマーの発火とTweenの更新順を同一視しない。
                for (int frame = 0; frame < 20 && (zan.RoundMoving || zan.Position.DistanceTo(zan.Home) > 0.01f); frame++)
                    await Wait(0.05);
                Require(!zan.RoundMoving && zan.Position.DistanceTo(zan.Home) < 0.01f, "仇巡りの最後に帰還");
            }
            Require(field.RoundSlashes == 21 && field.RoundFinishes == 3, "1太刀1剣閃・手番に1つの締め");
            Require(field.Pawns.Values.All(p => p.Hp == (p.Team == team ? 60 : 100)), "演出でHPを動かさない");
            await Wait(0.8 / speed);
            Require(feathers.IsDeployed && feathers.VisibleCount == 5, "循環が終わっても羽は在庫どおり浮く");
            var interrupted = field.PlayFeatherMark(misa, enemy, new() { Kind = BattleEventKind.FeatherMark, Turn = 1 }, speed);
            field.BeginBattle(opening, "再戦", 0);
            await interrupted;
            Require(field.FeatherMarkPlays == 0 && field.RoundSlashes == 0, "再戦へ古い射撃を持ち越さない");
            var pawn = field.FindPawn(3)!;
            var moving = field.ShowRoundSlash(pawn, field.FindPawn(10), new() { Kind = BattleEventKind.VendettaRound, Turn = 1, Slot = 1 }, false, speed);
            field.Hide(); field.Show();
            await moving;
            Require(field.RoundSlashes == 0 && !pawn.RoundMoving && pawn.RoundAfterimageCount == 0
                && pawn.Position.DistanceTo(pawn.Home) < 0.01f,
                "途中終了で巡り・残像・後続剣閃を止める");
            GD.Print($"MARK_LOOP_VISUAL_OK team={team} speed={speed}");
        }
        field.QueueFree(); await Wait(0.2);
    }
}
