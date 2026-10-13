using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class SomCheck
{
    private async Task ResonanceLive()
    {
        foreach (string name in new[] { "試遊・感電 光の盾", "試遊・感電 打ち返し" })
        {
            var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>(); AddChild(main);
            typeof(Main).GetMethod("SetSpeed", Flags)!.Invoke(main, new object[] { 3 });
            var formation = Presets.Playtest.First(p => p.Name == name).F;
            typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] {
                BattleEngine.Materialize(formation, 0),
                BattleEngine.MaterializeEnemy(EnemyCatalog.PlaytestStages[0].Enemy, EnemyCatalog.PlaytestStages[0].Scale),
                0, 0, name });
            var field = (BattlefieldView3D)typeof(Main).GetField("_battleField", Flags)!.GetValue(main)!;
            bool backswing = name.EndsWith("打ち返し");
            for (int frame = 0; frame < 18000 && (backswing ? field.SomBackswings : field.SomReturns) == 0; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require((backswing ? field.SomBackswings : field.SomReturns) > 0, "本編の画面で共鳴／打ち返しが出る");
            if (backswing)
            {
                int prior = field.ElectricWhipSweeps;
                for (int frame = 0; frame < 1200 && field.ElectricWhipSweeps == prior; frame++)
                    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Require(field.ElectricWhipSweeps > prior, "打ち返しに続く実際の鞭を待つ");
            }
            await Wait(backswing ? .06 : .035);
            typeof(Main).GetMethod("TogglePause", Flags)!.Invoke(main, null);
            await Capture(backswing ? "live-backswing" : "live-resonance");
            var points = field.SomBolts.Select(b => b.Head).ToArray(); await Wait(.12);
            Require(field.SomBolts.Select(b => b.Head).SequenceEqual(points), "本編の一時停止ボタンで雷が止まる");
            typeof(Main).GetMethod("ReturnToFormation", Flags)!.Invoke(main, null);
            Require(!field.SomBolts.Any() && field.SomBeastCount == 0, "本編で途中離脱しても雷と獣は残らない");
            main.QueueFree(); await Wait(.15);
            GD.Print($"SOM_RESONANCE_LIVE_OK name={name}");
        }
    }

    private static void CheckResonancePlan()
    {
        var labels = new HashSet<string>();
        int battles = 0, grouped = 0;
        // 粉の現行版ではクグ自身が帯電し、糸より先に共鳴の枝へ入る。
        // 糸は粉のいない台も使って実台本で検査する（ロスターの採用札は変えない）。
        var formations = new[] { "試遊・感電 光の盾", "試遊・感電 打ち返し", "試遊・感電 追い打ち", "試遊・感電 瓦礫" }
            .Select(name => (Name: name, F: Presets.Playtest.First(p => p.Name == name).F))
            .Append((Name: "糸の確認", F: Formation.Build(front1: UnitCatalog.Kugu, front3: UnitCatalog.Kado,
                center: UnitCatalog.Tsugi, back1: UnitCatalog.Gald, back3: UnitCatalog.Som)));
        foreach (var (name, formation) in formations)
        foreach (bool campaign in new[] { false, true })
        for (int seed = 0; seed < 12; seed++)
        {
            var enemy = campaign ? BattleEngine.Materialize(EnemyCatalog.Stages[1].Enemy, 1)
                : BattleEngine.MaterializeEnemy(EnemyCatalog.PlaytestStages[0].Enemy, EnemyCatalog.PlaytestStages[0].Scale);
            var events = BattleEngine.Run(BattleEngine.Materialize(formation, 0), enemy, seed, verbose: true).Events;
            var plan = SomPresentation.Build(events); battles++;
            var primary = Enumerable.Range(0, events.Count).Where(i => events[i].Kind == BattleEventKind.BeastThrow
                && events[i].Text is not (BeastThrowLabels.Arc or BeastThrowLabels.Backswing)).ToHashSet();
            Require(plan.BatchedThrows.SetEquals(primary), $"すべての往路を一度だけ一団へ結ぶ {name} {seed}");
            grouped += plan.ThrowBatches.Values.Count(b => b.Length > 1);
            Require(plan.StandingMembers.Count + plan.Calls.Values.Sum(c => c.Summons.Length)
                == events.Count(e => e.Kind == BattleEventKind.Summon && e.Text == UnitCatalog.Fodder.Name), "寝返る獣の台本を欠落なく結ぶ");
            Require(plan.ReturnArcs.Values.SelectMany(a => a).ToHashSet().SetEquals(Enumerable.Range(0, events.Count)
                .Where(i => events[i].Kind == BattleEventKind.BeastThrow && events[i].Text == BeastThrowLabels.Arc)), "枝分かれは元の共鳴にだけ結ぶ");
            Require(plan.BackswingAttacks.Count == events.Count(e => e.Kind == BattleEventKind.BeastThrow
                && e.Text == BeastThrowLabels.Backswing), "打ち返しは直後の一振りに結ぶ");
            foreach (var e in events.Where(e => e.Kind == BattleEventKind.BeastThrow && e.Text is not null)) labels.Add(e.Text!);
        }
        Require(grouped > 0 && new[] { BeastThrowLabels.Hit, BeastThrowLabels.Thread, BeastThrowLabels.Resonance,
            BeastThrowLabels.Hushed, BeastThrowLabels.Arc, BeastThrowLabels.Backswing }.All(labels.Contains),
            $"実台本で群れと全分岐を確認 grouped={grouped} labels={string.Join(",", labels)}");
        GD.Print($"SOM_RESONANCE_PLAN_OK battles={battles} grouped={grouped}");
    }

    private async Task ResonanceVisuals()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        field.UsePurchasedSomFx = !OS.GetCmdlineUserArgs().Contains("--som-no-purchased");
        await Wait(.15);
        static DemoOpening Open(UnitDef d, int id, int team, int slot) => new(id, team, d.Id, d.Name,
            slot, d.MaxHp, d.MaxHp, d.Attack, d.Pattern, d.Advances, d.Traits);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 4.0 })
        foreach (string branch in new[] { BeastThrowLabels.Resonance, BeastThrowLabels.Hit, BeastThrowLabels.Thread,
            BeastThrowLabels.Hushed, BeastThrowLabels.Backswing })
        {
            var units = new[] { Open(UnitCatalog.Som, 1, team, 4), Open(UnitCatalog.Shiga, 2, team, 0),
                Open(UnitCatalog.Kugu, 3, team, 1), Open(UnitCatalog.Tsugi, 4, team, 3),
                Open(UnitCatalog.Kado, 5, 1 - team, 0), Open(UnitCatalog.Borg, 6, 1 - team, 2) };
            field.BeginBattle(units, $"背きの雷 — {branch} ×{speed}", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            var som = field.FindPawn(1)!;
            int count = branch == BeastThrowLabels.Resonance ? 4 : 1;
            int[] ids = Enumerable.Range(30, count).ToArray(), slots = { 7, 5, 6, 8 };
            field.BeginSomCall(som, count, false, null, true, speed);
            for (int n = 0; n < count; n++)
            {
                field.AddSomStandingPawn(Open(UnitCatalog.Fodder, ids[n], 1 - team, slots[n]), speed);
                field.PlaceSomBurstBeast(som, new() { Turn = 1, Kind = BattleEventKind.Summon, TargetId = ids[n], Team = 1 - team, Slot = slots[n] },
                    false, null, n, count, speed);
            }
            field.RunSomBurstBeasts(som, ids, true); await Wait((SomFx.BeastRunSeconds + .03) / speed);
            field.TakeSomStance(som, ids, false); await Wait((SomFx.BeastStanceSeconds + .03) / speed);
            int ally = branch == BeastThrowLabels.Thread ? 3 : 2;
            var receiver = field.FindPawn(ally)!;
            receiver.SetFrightened(branch == BeastThrowLabels.Backswing);
            if (branch is BeastThrowLabels.Resonance or BeastThrowLabels.Hushed) receiver.SetShocked(true);
            var shots = ids.Select(id => new BattleEvent { Turn = 1, Kind = BattleEventKind.BeastThrow, ActorId = id,
                TargetId = ally, PartnerId = branch == BeastThrowLabels.Thread ? 5 : null,
                Text = branch == BeastThrowLabels.Backswing ? BeastThrowLabels.Hit : branch }).ToArray();
            foreach (var shot in shots) field.ThrowSomLightning(shot, speed);
            await Wait(.12 / speed);
            field.SetSomClock(true, speed);
            var pausedHeads = field.SomBolts.Select(b => b.Head).ToArray();
            await Wait(.12);
            Require(field.SomBolts.Select(b => b.Head).SequenceEqual(pausedHeads), "投げる雷は一時停止で止まる");
            await Capture($"team{team}-speed{speed}-{branch}-outgoing");
            field.SetSomClock(false, speed * 2); await Wait(.08 / speed);
            Require(field.SomBolts.All(b => b.Progress == 1), "往路の途中で倍速に切り替わる");
            Require(field.SomBeasts.All(b => b.BodyScale <= .751f), "槍の放出で光の身体が縮む");
            field.SetSomClock(false, speed);
            foreach (var shot in shots) field.ReceiveSomLightning(shot, speed);
            await Wait(.06 / speed); await Capture($"team{team}-speed{speed}-{branch}-contact");
            if (branch == BeastThrowLabels.Resonance)
            {
                foreach (var shot in shots) field.ReturnSomLightning(shot, speed);
                await Wait(.08 / speed);
                await Capture($"team{team}-speed{speed}-{branch}-return");
                field.SetSomClock(true, speed);
                var returnHeads = field.SomBolts.Select(b => b.Head).ToArray(); await Wait(.12);
                Require(field.SomBolts.Select(b => b.Head).SequenceEqual(returnHeads), "共鳴の復路も一時停止");
                field.SetSomClock(false, speed);
                foreach (int id in ids) field.BranchSomLightning(new() { Turn = 1, Kind = BattleEventKind.BeastThrow, ActorId = id, TargetId = 5 }, speed);
                await Wait(.08 / speed); await Capture($"team{team}-speed{speed}-{branch}-branches");
                await Wait(.08 / speed);
            }
            if (branch == BeastThrowLabels.Backswing)
            {
                field.SomBackswing(receiver, speed);
                Require(receiver.InterruptWhip && receiver.MovementPortrait == "shiga_interrupt", "雷で割り込みの構えに目覚める");
                var targets = new[] { field.FindPawn(ids[0])!, field.FindPawn(5)! };
                int contacts = 0;
                Task whip = field.ShowWhipSweep(receiver, targets, _ => contacts++);
                await Wait(.22 / speed); await Capture($"team{team}-speed{speed}-{branch}-whip");
                await whip;
                Require(contacts == targets.Length && field.WhipSweeps == 1, "獣と隣の敵へ一振りで接触");
                receiver.SetShocked(false);
            }
            foreach (int id in ids) field.RemoveSomBeast(id, speed, branch is BeastThrowLabels.Hit or BeastThrowLabels.Thread or BeastThrowLabels.Hushed);
            await Wait(.03 / speed); await Capture($"team{team}-speed{speed}-{branch}-burst");
            Require(field.SomBeastCount == 0 && !field.Pawns.Values.Any(p => p.UnitId == "fodder"), "どの結末でも獣は盤から消える");
            Require(field.GatherSomScrap(new() { Turn = 1, Kind = BattleEventKind.Plank, TargetId = ids[0], Amount = 1 }, field.FindPawn(4), speed), "弾けた位置から瓦礫を回収");
            field.EndSomPresentation(); await Wait(.08);
            Require(!field.SomBolts.Any() && field.SomBeastCount == 0, "中断で雷・獣・粒子を消去");
            Require(units.All(u => field.FindPawn(u.InstanceId)!.Hp == u.Hp), "絵と鞭の確認だけではHPは変わらない");
            GD.Print($"SOM_RESONANCE_VISUAL_OK team={team} speed={speed} branch={branch}");
        }
        field.QueueFree(); await Wait(.1);
    }
}
