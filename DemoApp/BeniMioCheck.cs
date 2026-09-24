using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// ベニ・ミオの演出観察。本番の演出口を1倍速・2倍速で繰り返す。
public partial class BeniMioCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            CheckTimeline();
            if (OS.GetCmdlineUserArgs().Contains("--verify"))
            {
                await CheckReplay();
                await CheckReplay(support: true);
                await CheckPersistentEffects();
                GD.Print("BENI_MIO_CHECK_OK");
                GetTree().Quit();
                return;
            }
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            DemoOpening[] openings = [
                new(1, 0, "beni", "毒喰らいのベニ", 2, 80, 100, 0, AttackPattern.Single, false, UnitCatalog.Beni.Traits),
                new(2, 0, "mio", "澱みのミオ", 0, 80, 100, 0, AttackPattern.Single, false),
                new(3, 1, EnemyCatalog.Knight.Id, "巡礼騎士", 2, 100, 100, 10, AttackPattern.Single, false),
                new(4, 1, EnemyCatalog.Knight.Id, "巡礼騎士", 0, 100, 100, 10, AttackPattern.Single, false),
                new(5, 0, "nono", "傷縫いのノノ", 4, 80, 100, 0, AttackPattern.Single, false),
            ];
            field.BeginBattle(openings, "", 0);
            await Wait(0.5);
            int loop = 0;
            do
            {
                double speed = OS.GetCmdlineUserArgs().Contains("--double") ? 2 : loop++ % 2 == 0 ? 1 : 2;
                await PreviewRemaining(field, speed);
                foreach (bool burn in new[] { false, true })
                {
                    var pawn = field.FindPawn(1)!;
                    field.ShowTick(pawn, burn, true, false, 0.7 / speed);
                    await Wait(0.16 / speed);
                    await Capture(burn ? "burn-before" : "poison-before");
                    await Wait(0.23 / speed);
                    field.TickNumber(pawn, 8, true, 0, false, burn);
                    await Capture(burn ? "burn-after" : "poison-after");
                    await Wait(0.85 / speed);
                }
                foreach (int count in new[] { 2, 4, 7 })
                {
                    var script = new List<BattleEvent>();
                    for (int k = 1; k <= count; k++)
                    {
                        script.Add(new BattleEvent { Turn = 1, Kind = BattleEventKind.Status, TargetId = 3,
                            Text = "毒", TickIndex = k, TickCount = count });
                        script.Add(new BattleEvent { Turn = 1, Kind = BattleEventKind.Damage, TargetId = 3, Amount = 8 });
                    }
                    foreach (var cue in TickPresentation.Build(script).Starts.Values)
                    {
                        var pawn = field.FindPawn(3)!;
                        field.ShowTick(pawn, false, false, cue.Last, cue.Seconds / speed);
                        field.TickNumber(pawn, 8, false, cue.Ordinal, cue.Last);
                        await Wait(cue.Seconds * 0.6 / speed);
                        if (count == 4 && cue.Last) await Capture("four-ticks");
                        await Wait(cue.Seconds * 0.4 / speed);
                    }
                    await Wait(0.8);
                }
                GD.Print($"BENI_MIO_PREVIEW speed={speed}");
            } while (!OS.GetCmdlineUserArgs().Contains("--once"));
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static void CheckTimeline()
    {
        int ticks = 0, inversions = 0, repeats = 0;
        var formations = Presets.Compare.Where(x => x.F.Occupied().Any(s => s.Def.Id is "beni" or "mio" or "kata"))
            .Select(x => x.F).ToArray();
        foreach (var formation in formations)
        for (int stage = 0; stage < 5; stage++)
        for (int seed = 0; seed < 4; seed++)
        {
            var result = BattleEngine.Run(BattleEngine.Materialize(formation, 0),
                BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, 1), seed, verbose: true);
            var plan = TickPresentation.Build(result.Events);
            Require(plan.Starts.Count == result.Events.Count(TickPresentation.IsTick), "刻みを漏らさない");
            foreach (var cue in plan.Starts.Values)
            {
                Require(plan.Budgets.Where(x => x.Key >= cue.Start && x.Key < cue.End)
                    .Sum(x => x.Value) <= cue.Seconds + 0.00001, "副作用を含む待ち時間の上限");
                if (cue.Ordinal != 0) continue;
                var run = plan.Starts.Values.SkipWhile(x => x.Start != cue.Start).Take(cue.Count).ToArray();
                Require(run.Sum(x => x.Seconds) <= TickPresentation.MaxSeconds + 0.00001, "1駒の尺の上限");
                Require(run[^1].Last, "最後の実在する刻みを強調");
                Require(run.Zip(run.Skip(1)).All(x => x.First.Seconds >= x.Second.Seconds), "後半の間隔を詰める");
            }
            foreach (var (index, cue) in plan.Outcomes)
                Require(result.Events[index].TargetId == result.Events[cue.Start].TargetId,
                    "啜りや別の駒の副作用を数字に混ぜない");
            ticks += plan.Starts.Count;
            inversions += result.Events.Count(e => TickPresentation.IsTick(e) && e.SourceTrait == TraitId.Inverse);
            repeats += result.Events.Count(e => TickPresentation.IsTick(e) && e.TickIndex > 1);
        }
        Require(ticks > 0 && inversions > 0 && repeats > 0, $"陽性対照 ticks={ticks} inverse={inversions} repeats={repeats}");
        // 予定7回でも2回目で死亡。次の別発動・別の駒を最後の判定へ混ぜない。
        BattleEvent[] truncated = [
            new() { Turn = 1, Kind = BattleEventKind.Status, TargetId = 1, Text = "毒", TickIndex = 1, TickCount = 7 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, TargetId = 1 },
            new() { Turn = 1, Kind = BattleEventKind.Status, TargetId = 1, Text = "毒", TickIndex = 2, TickCount = 7 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, TargetId = 1 },
            new() { Turn = 1, Kind = BattleEventKind.Death, TargetId = 1 },
            new() { Turn = 1, Kind = BattleEventKind.Status, TargetId = 2, Text = "毒", TickIndex = 1, TickCount = 3 },
        ];
        var shortPlan = TickPresentation.Build(truncated);
        Require(shortPlan.Starts[2].Last && shortPlan.Starts[0].Count == 2, "死亡による途中打ち切り");
        GD.Print($"BENI_MIO_TIMELINE_OK battles={formations.Length * 20} ticks={ticks} inverse={inversions} repeats={repeats}");
    }

    private async Task CheckReplay(bool support = false)
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Read(string name) => typeof(Main).GetField(name, flags)!.GetValue(main);
        typeof(Main).GetField("_fastSmoke", flags)!.SetValue(main, true);
        typeof(Main).GetField("_speed", flags)!.SetValue(main, 1000.0);
        var formation = support ? Formation.Build(front1: UnitCatalog.Beni, front3: UnitCatalog.Rau,
            center: UnitCatalog.Mio, back1: UnitCatalog.Kata, back3: UnitCatalog.Nono)
            : Presets.Compare.First(x => x.Name.StartsWith("毒+耐久")).F;
        typeof(Main).GetMethod("EnterBattle", flags)!.Invoke(main, new object[] {
            BattleEngine.Materialize(formation, 0), BattleEngine.Materialize(EnemyCatalog.Stages[0].Enemy, 1), 0, 0, "" });
        for (int k = 0; k < 240 && (bool)Read("_playing")!; k++) await Wait(0.25);
        Require(!(bool)Read("_playing")!, "通常再生の完走");
        var result = (BattleResult)Read("_result")!;
        if (!support) Require(result.Events.Any(e => e.TickIndex > 1), "通常再生にも追加刻みがある");
        Require((int)Read("_tickPlays")! == result.Events.Count(TickPresentation.IsTick), "刻みを一度ずつ再生");
        Require((int)Read("_inverseTickPlays")! == result.Events.Count(e => TickPresentation.IsTick(e)
            && e.SourceTrait == TraitId.Inverse), "反転の実台本を再生");
        var field = (BattlefieldView3D)Read("_battleField")!;
        Require(field.ConcentratePlays == result.Events.Count(e => e.Kind == BattleEventKind.ConcentrateMark), "印は台本と同数");
        Require(field.ThickenPlays == result.Events.Count(e => e.Kind == BattleEventKind.PoisonThicken), "濃縮は台本と同数");
        Require(field.InvertedHealPlays == result.Events.Count(e => e.Kind == BattleEventKind.HealInverted), "回復の反転は台本と同数");
        Require(field.SipPlays == result.Events.Count(e => e.Kind == BattleEventKind.InverseSip), "啜りは台本と同数");
        Require(field.KindlePlays > 0, "火の拍の陽性対照");
        if (support) Require(field.InvertedHealPlays > 0, "回復の反転の陽性対照");
        foreach (var group in result.Events.Where(e => e.TargetId is not null
            && e.Kind is BattleEventKind.Heal or BattleEventKind.Damage).GroupBy(e => e.TargetId))
            Require(field.FindPawn(group.Key)?.Hp == group.Last().HpAfter, "HPは台本の最終値と一致");
        GD.Print($"BENI_MIO_REPLAY_OK support={support} mark={field.ConcentratePlays} thicken={field.ThickenPlays} inverted={field.InvertedHealPlays} sip={field.SipPlays} fire={field.KindlePlays} poison={field.TaintPlays}");
        if (!support)
        {
            int marks = field.ConcentratePlays, thickens = field.ThickenPlays, sips = field.SipPlays;
            typeof(Main).GetMethod("ReplayBattle", flags)!.Invoke(main, null);
            for (int k = 0; k < 240 && (bool)Read("_playing")!; k++) await Wait(0.25);
            Require(!(bool)Read("_playing")! && field.ConcentratePlays == marks
                && field.ThickenPlays == thickens && field.SipPlays == sips,
                "最初から再生しても濃縮・印・啜りを省かない");
            GD.Print("BENI_MIO_RESTART_OK");
        }
        main.QueueFree();
        await Wait(0.5);
    }

    private async Task CheckPersistentEffects()
    {
        var field = new BattlefieldView3D();
        AddChild(field);
        DemoOpening[] openings = [
            new(1, 0, "beni", "ベニ", 2, 60, 64, 4, AttackPattern.Single, false, UnitCatalog.Beni.Traits),
            new(2, 0, "mio", "ミオ", 4, 40, 42, 2, AttackPattern.Single, false),
            new(3, 1, EnemyCatalog.Knight.Id, "敵", 4, 40, 42, 2, AttackPattern.Single, false),
        ];
        field.BeginBattle(openings, "", 0);
        var beni = field.FindPawn(1)!; var mio = field.FindPawn(2)!;
        Require(beni.InverseBarrierVisible && mio.InverseBarrierVisible && !field.FindPawn(3)!.InverseBarrierVisible,
            "結界は自分と隣の味方だけ");
        int outside = Enumerable.Range(0, 5).First(s => s != mio.Slot && !FormationRules.AreAdjacent(s, mio.Slot));
        field.MovePawn(beni, outside); field.RefreshInverseBarriers();
        Require(!mio.InverseBarrierVisible && beni.InverseBarrierVisible, "移動で結界が付け替わる");
        field.MovePawn(beni, 2); field.RefreshInverseBarriers();
        field.PlayDeath(beni); field.RefreshInverseBarriers();
        Require(!mio.InverseBarrierVisible && !beni.InverseBarrierVisible, "死亡で結界が消える");
        beni.SetHp(30); field.RevivePawn(beni); field.RefreshInverseBarriers();
        Require(mio.InverseBarrierVisible, "蘇生で結界が戻る");
        mio.SetConcentrated(4);
        Require(mio.ConcentratedAmount == 4, "付与した印が残る");
        mio.BeginStatusSnapshot(); mio.ReadStatusSnapshot("濃", 4); mio.CommitStatusSnapshot();
        Require(mio.ConcentratedAmount == 4, "スナップショットは加算しない");
        mio.BeginStatusSnapshot(); mio.CommitStatusSnapshot();
        Require(mio.ConcentratedAmount == 0, "残量ゼロで印が消える");
        mio.SetConcentrated(3); field.PlayDeath(mio);
        Require(mio.ConcentratedAmount == 0, "死亡で印を消す");
        field.BeginBattle(openings, "", 0);
        Require(field.FindPawn(2)!.ConcentratedAmount == 0, "再開時に印を持ち越さない");
        GD.Print("BENI_MIO_PERSISTENT_OK");
        field.QueueFree(); await Wait(0.3);
    }

    private async Task PreviewRemaining(BattlefieldView3D field, double speed)
    {
        var beni = field.FindPawn(1)!; var mio = field.FindPawn(2)!;
        var center = field.FindPawn(3)!; var around = field.FindPawn(4)!; var nono = field.FindPawn(5)!;
        foreach (bool fire in new[] { true, false, false })
        {
            field.ShowBeniGift(beni, new[] { mio, nono }, fire, speed);
            await Wait(0.23 / speed); await Capture(fire ? "beni-fire" : "beni-poison");
            await Wait(0.6 / speed);
        }
        for (int n = 1; n <= 3; n++)
        {
            field.ShowThicken(center, speed); field.ShowThicken(around, speed);
            await Wait(0.36 / speed);
            center.SetPoisonRemaining(n * 4); center.PulsePoisonIcon();
            around.SetPoisonRemaining(n * 4); around.PulsePoisonIcon();
            field.ShowConcentrate(mio, center, true, speed);
            await Wait(0.22 / speed);
            field.ShowConcentrate(center, around, false, speed);
            field.ShowConcentrate(mio, beni, false, speed);
            center.SetConcentrated(n); around.SetConcentrated(n); beni.SetConcentrated(n);
            await Wait(0.19 / speed); await Capture("mio-mark"); await Wait(0.7 / speed);
        }
        field.ShowInvertedHeal(nono, beni, speed);
        await Wait(0.34 / speed); await Capture("heal-inverted"); await Wait(0.65 / speed);
        field.ShowInverseSip(mio, beni, speed);
        await Wait(0.19 / speed); await Capture("inverse-sip"); await Wait(0.65 / speed);
        await Capture("barrier");
    }

    private async Task Capture(string phase)
    {
        var arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(arg[14..] + "/" + phase + ".png") == Error.Ok, "画像保存");
    }
    private async Task Wait(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
