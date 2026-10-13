using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class SomCheck
{
    private static void CheckSwarmPlan()
    {
        var events = new List<BattleEvent>();
        for (int n = 0; n < 5; n++)
        {
            events.Add(new() { Turn = 1, Kind = BattleEventKind.Summon, ActorId = 1, TargetId = 10 + n, Text = UnitCatalog.Fodder.Name });
            events.Add(new() { Turn = 1, Kind = BattleEventKind.BeastBurst, ActorId = 1, TargetId = 10 + n, Text = BeastBurstLabels.Swarm, StatusRemaining = 5 });
        }
        for (int n = 0; n < 5; n++) events.Add(new() { Turn = 1, Kind = BattleEventKind.ShockSpent, ActorId = 1, TargetId = 10 + n, Team = 1, Slot = 0 });
        events.Add(new() { Turn = 1, Kind = BattleEventKind.ShockSpent, TargetId = 20, Team = 1, Slot = 1 });
        events.Add(new() { Turn = 1, Kind = BattleEventKind.Spark, ActorId = 1, Team = 0, Text = SparkLabels.Release, Amount = 6 });
        var plan = SomPresentation.Build(events);
        Require(plan.Calls.Count == 1 && plan.Calls[0].Summons.Length == 5, "5匹は1回の走り");
        Require(plan.Rains[16].Pops.SequenceEqual(Enumerable.Range(10, 6)), "深さ0が5つあっても光の連鎖を切らない");
        events.Add(new() { Turn = 1, Kind = BattleEventKind.StatusGain, Text = StatusKeys.Daunted, TargetId = 20, Amount = 1 });
        events.Add(new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 20 });
        events.Add(new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 20 });
        plan = SomPresentation.Build(events);
        Require(plan.DauntedAttacks.SetEquals(new[] { 18 }), "萎縮は次の攻撃1回のみ");
        foreach (int originalCount in new[] { 1, 3 })
        {
            var nested = Enumerable.Range(0, originalCount).Select(n => new BattleEvent {
                Turn = 1, Kind = BattleEventKind.ShockSpent, TargetId = 50 + n, Team = 1, Slot = n }).ToList();
            nested.Add(new() { Turn = 1, Kind = BattleEventKind.ShockGauge, ActorId = 2, Text = ShockGaugeLabels.Interrupt });
            nested.Add(new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 2, Reaction = true });
            nested.Add(new() { Turn = 1, Kind = BattleEventKind.ShockSpent, TargetId = 60, Team = 1, Slot = 0 });
            int inner = nested.Count;
            nested.Add(new() { Turn = 1, Kind = BattleEventKind.Spark, ActorId = 1, Team = 0, Amount = 1, Text = SparkLabels.Release });
            int outer = nested.Count;
            nested.Add(new() { Turn = 1, Kind = BattleEventKind.Spark, ActorId = 1, Team = 0, Amount = originalCount, Text = SparkLabels.Release });
            var nestedPlan = SomPresentation.Build(nested);
            Require(nestedPlan.Rains[inner].Pops.Single() == originalCount + 2
                && nestedPlan.Rains[outer].Pops.SequenceEqual(Enumerable.Range(0, originalCount)),
                "蓄電を消費しない鞭の後も、元の光の出どころへ戻る（同数の連鎖も区別）");
        }
        GD.Print("SOM_SWARM_PLAN_OK");
    }

    private async Task SwarmVisuals()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        await Wait(.15);
        static DemoOpening Open(UnitDef def, int id, int team, int slot) => new(id, team, def.Id, def.Name,
            slot, def.MaxHp, def.MaxHp, def.Attack, def.Pattern, def.Advances, def.Traits);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 4.0 })
        {
            var units = new[] { Open(UnitCatalog.Som, 1, team, 4), Open(UnitCatalog.Kugu, 2, team, 0),
                Open(UnitCatalog.Tou, 3, team, 2), Open(UnitCatalog.Kata, 5, team, 3), Open(UnitCatalog.Kado, 6, 1 - team, 0) };
            field.BeginBattle(units, "ソム — 群れ・緊急・萎縮", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            var som = field.FindPawn(1)!; var partner = field.FindPawn(2)!;
            field.BeginSomCall(som, 5, false, null, true, speed);
            int[] seats = { 7, 5, 6, 8, 7 }, ids = Enumerable.Range(30, 5).ToArray();
            for (int i = 0; i < 5; i++) field.PlaceSomBurstBeast(som,
                new() { Turn = 1, Kind = BattleEventKind.Summon, TargetId = ids[i], Team = 1 - team, Slot = seats[i] }, false, null, i, 5, speed);
            Require(field.SomBeastCount == 5 && field.Pawns.Count == units.Length, "群れの5匹は駒を増やさない");
            Require(field.SomBeasts.All(b => b.EnergyColor == SomFx.Violet && !b.ContractCracked), "召喚直後は菫の光と契約紋");
            Require(field.SomBeasts.All(b => b.FigureTexture == SomSpiritArt.Texture(false)
                && b.FigureFlipped == (team == 1)), "手番の一団は雷人形で敵陣を向く");
            await Wait(.20 / speed);
            await Capture($"swarm-team{team}-speed{speed}-appear");
            field.RunSomBurstBeasts(som, ids, true); await Wait(.18 / speed);
            field.SetSomClock(true, speed);
            var positions = field.SomBeasts.Select(b => b.Position).ToArray();
            await Wait(.15);
            Require(field.SomBeasts.Select(b => b.Position).SequenceEqual(positions), "走る途中の一時停止");
            await Capture($"swarm-team{team}-speed{speed}-paused");
            field.SetSomClock(false, speed * 2); await Wait(.26 / speed);
            Require(field.SomBeasts.All(b => b.Progress == 1), "走る途中の速度切替");
            field.SetSomClock(false, speed);
            field.TakeSomStance(som, ids, false);
            await Wait(.14 / speed);
            Require(field.SomBeasts.All(b => b.InStance && b.FacingSummoner), "群れが一斉にこちらへ牙を剥く");
            Require(field.SomBeasts.All(b => b.ContractCracked && b.EnergyColor == SomFx.SpiritRebel), "反転した拍で紋がひび割れ黄白の雷になる");
            Require(field.SomBeasts.All(b => b.FigureFlipped == (team == 0)), "寝返った雷人形の顔と手がソム側へ向く");
            await Capture($"stance-team{team}-speed{speed}-threat");
            field.SetSomClock(true, speed);
            var charge = field.SomBeasts.Select(b => b.Charge).ToArray();
            await Wait(.12);
            Require(field.SomBeasts.Select(b => b.Charge).SequenceEqual(charge), "臨戦態勢の一時停止");
            field.SetSomClock(false, speed);
            await Wait(.24 / speed);
            Require(field.SomBeasts.All(b => b.Charge == 1 && b.FacingSummoner), "体表の雷が膨らんでもこちらを向く");
            if (ResourceLoader.Exists("res://PolyBlocks/EffectBlocks/assets/energy/electric_sparks_1.tscn"))
                Require(field.SomBeasts.All(b => b.UsesPurchasedCharge), "購入素材の電気火花を胴体の帯電へ接続");
            await Capture($"stance-team{team}-speed{speed}-charged");
            await Wait(.3 / speed);
            Require(field.SomBeastCount == 5 && field.SomBeasts.All(b => b.Charge == 1), "臨戦態勢だけでは暴発せず立ち続ける");
            field.BurstSomBeasts(ids, speed);
            Require(field.SomBursts == 5 && field.SomBeastCount == 0, "5匹が同時に弾けて消える");
            Require(field.SomScatters.Count() == 5, "亡骸を残さず光粒へ還る");
            await Wait(.04 / speed); await Capture($"swarm-team{team}-speed{speed}-burst");
            field.SetSomClock(true, speed);
            var scatteringAge = field.SomScatters.Select(s => s.VisualAge).ToArray();
            await Wait(.12);
            Require(field.SomScatters.Select(s => s.VisualAge).SequenceEqual(scatteringAge), "核の散開も一時停止");
            field.SetSomClock(false, speed);
            for (int i = 0; i < 5; i++) field.RaiseSomLight(i, ids[i], speed);
            field.GatherSomLight(som, Enumerable.Range(0, 5).ToArray(), false, speed);
            field.SetSomClock(true, speed);
            Require(field.SomRisingCount == 0, "集光の出どころを二重保持しない");
            await Wait(.12); field.SetSomClock(false, speed);
            await Wait(.27 / speed); field.AbsorbSomBookLight(som, 5, speed);
            await Wait(.06 / speed); await Capture($"stance-team{team}-speed{speed}-book");
            field.RainSomLight(som, 5, new HashSet<int>(), speed);
            await Wait(.13 / speed); await Capture($"swarm-team{team}-speed{speed}-rain");
            field.BeginSomCall(som, 1, true, partner, true, speed);
            await Wait(.12 / speed);
            field.PlaceSomBurstBeast(som, new() { Turn = 1, Kind = BattleEventKind.Summon, TargetId = 40, Team = 1 - team, Slot = 7 }, true, partner, 0, 1, speed);
            Require(field.SomBeasts.Single().FigureTexture == SomSpiritArt.Texture(true)
                && field.SomBeasts.Single().FigureTexture != SomSpiritArt.Texture(false), "緊急だけは顔・手足のある別種の雷精");
            await Wait(.13 / speed);
            await Capture($"emergency-team{team}-speed{speed}-lookback");
            field.RunSomBurstBeasts(som, new[] { 40 }, true); await Wait(.2 / speed);
            await Capture($"emergency-team{team}-speed{speed}-run");
            await Wait(.46 / speed); field.TakeSomStance(som, new[] { 40 }, true);
            await Wait(.18 / speed);
            Require(field.SomBeasts.Single().Charge == 1 && field.SomBeasts.Single().FacingSummoner, "空き枠なしは短い構えでこちらへ向く");
            await Capture($"stance-team{team}-speed{speed}-emergency");
            field.BurstSomBeasts(new[] { 40 }, speed);
            field.RaiseSomLight(10, 40, speed); field.GatherSomLight(som, new[] { 10 }, false, speed);
            await Wait(.27 / speed); field.AbsorbSomBookLight(som, 1, speed);
            await Wait(.08 / speed); field.RainSomLight(som, 1, new HashSet<int>(), speed, partner);
            var root = (Node3D)typeof(BattlefieldView3D).GetField("_somRoot", Flags)!.GetValue(field)!;
            Require(root.GetChildren().OfType<Sprite3D>().Where(s => s.Texture == SomFx.Light)
                .All(s => s.GlobalPosition.IsEqualApprox(field.SomBookPoint(som))), "緊急の光も魔導書から救援先へ出る");
            await Wait(.08 / speed); await Capture($"emergency-team{team}-speed{speed}-focus");
            field.RainSomLight(som, 3, new HashSet<int> { 2 }, speed, scattered: partner);
            await Wait(.12 / speed); await Capture($"emergency-team{team}-speed{speed}-scatter");
            field.ShowSomDaunt(field.FindPawn(6)!, speed); await Wait(.1 / speed);
            await Capture($"daunt-team{team}-speed{speed}");
            field.PlaceSomBurstBeast(som, new() { Turn = 1, Kind = BattleEventKind.Summon, TargetId = 50, Team = 1 - team, Slot = 7 }, true, partner, 0, 1, speed);
            field.RunSomBurstBeasts(som, new[] { 50 }, false); await Wait(.66 / speed);
            field.TakeSomStance(som, new[] { 50 }, false); await Wait(.24 / speed);
            Require(field.SomBeasts.Single().Charge > 0, "帯電中に中断する確認台");
            field.EndSomPresentation(); await Wait(.1);
            Require(field.SomBeastCount == 0 && field.SomRisingCount == 0 && field.SomVeilCount == 0, "中断ですべて消える");
            Require(som.Hp == som.MaxHp && partner.Hp == partner.MaxHp, "演出はHPを計算しない");
            GD.Print($"SOM_STANCE_VISUAL_OK team={team} speed={speed}");
        }
        field.QueueFree(); await Wait(.1);
    }
}
