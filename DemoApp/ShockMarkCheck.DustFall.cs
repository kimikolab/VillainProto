using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    private async Task CheckDustFall()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        Dictionary<int, TouPowderAura3D> Auras() => (Dictionary<int, TouPowderAura3D>)typeof(BattlefieldView3D)
            .GetField("_powderAuras", Flags)!.GetValue(field)!;
        foreach (int side in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0, 4.0 })
        {
            DemoOpening[] opening = [
                new(1, side, "kugu", "クグ", 3, 50, 50, 3, AttackPattern.Single, false),
                new(2, 1 - side, "hero", "粉が降る相手", 2, 100, 100, 8, AttackPattern.Single, false),
            ];
            void Reset()
            {
                field.BeginBattle(opening, "粉が降る：爆発した糸玉から粉の印へ", 0);
                field.ShowSilkBall(new() { Turn = 1, Kind = BattleEventKind.SilkBall, ActorId = 1,
                    TargetId = 100, Team = 1 - side, Slot = 0, PartnerId = 2, Text = SilkBallLabels.Place }, speed);
                field.SetTouPowderClock(false, speed);
            }
            BattleEvent fall = new() { Turn = 1, Kind = BattleEventKind.ShockGauge, ActorId = 100,
                TargetId = 2, Text = ShockGaugeLabels.DustFall, Amount = 1 };
            Reset(); var target = field.FindPawn(2)!;
            field.ShowTouPowder(fall, speed);
            var flight = field.PowderEffects.Single();
            Require(flight.Kind == TouPowderFxKind.DustFall && !target.PowderMarked, "糸玉からの粉は到着前に印を付けない");
            flight._Process(.09 / speed); field.SetTouPowderClock(true, speed);
            float age = flight.Age; var arrival = field.PowderArrival;
            await Wait(.08);
            Require(flight.Age == age && !arrival.IsCompleted && !target.PowderMarked, "停止中は粉の飛行と印の付与を保留する");
            if (speed == 1) await Capture($"dust-fall-flight-side{side}");
            field.SetTouPowderClock(false, speed * 2); flight._Process(.12 / (speed * 2));
            Require(await arrival && target.PowderMarked, "途中で速度を変えても到着後だけ粉の印が残る");
            Require(target.PowderDensity == 0 && !target.HasStatusIcon(StatusKeys.Shock) && target.Hp == 100,
                "粉が降っても帯電・濃さ・HPを変えない");
            Require(Auras()[2].BadgeText == "粉印", "濃さ0でも粉印の札を見せる");
            await Wait(.26);
            Require(target.PowderMarked && field.ActivePowderEffects == 0, "飛行を消した後も粉印は残る");
            if (speed == 1) await Capture($"dust-fall-mark-side{side}");
            field.ShowTouPowder(new() { Turn = 1, Kind = BattleEventKind.ShockGauge, TargetId = 2,
                Text = ShockGaugeLabels.Dense, Amount = 3 }, speed);
            Auras()[2]._Process(0);
            Require(target.PowderDensity == 3 && Auras()[2].BadgeText == "粉 3 ◇", "濃さと粉の印を同じ札で区別する");
            target.AnimateDeath(); Require(!target.PowderMarked, "死亡で印を消す");

            Reset(); field.ShowTouPowder(fall, speed); var canceled = field.PowderArrival;
            field.SetTouPowderClock(true, speed); field.Hide();
            Require(canceled.IsCompleted && !await canceled, "離脱で粉の到着待ちを取り消す");
            field.Show(); Reset(); await Wait(.25);
            Require(!field.FindPawn(2)!.PowderMarked, "再戦へ古い印を付けない");

            BattleEvent[] events = [
                new() { Turn = 1, Kind = BattleEventKind.Discharge, ActorId = 2, TargetId = 100, Amount = 8 },
                new() { Turn = 1, Kind = BattleEventKind.ShockGauge, ActorId = 2, TargetId = 100,
                    Text = ShockGaugeLabels.DustBurst, Slot = 0, Amount = 8 },
                new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 100, TargetId = 2, Amount = 8, HpAfter = 92 },
                fall,
            ];
            var chain = DustBurstPresentation.Build(events).Starts.Single().Value;
            field.BeginDustBurstChain(chain, events, speed);
            field.ShowTouPowder(fall, speed, scheduledDust: true);
            Require(field.PowderEffects.All(f => f.Kind != TouPowderFxKind.DustFall), "連鎖の粉は爆発前に飛び始めない");
            var packet = field.DustChain!; packet.Advance(.12 / speed);
            var scheduled = field.PowderEffects.Single(f => f.Kind == TouPowderFxKind.DustFall);
            Require(!field.FindPawn(2)!.PowderMarked, "連鎖の中でも到着前に印を出さない");
            scheduled._Process(.09 / speed); field.SetTouPowderClock(true, speed);
            if (speed == 1) await Capture($"dust-fall-chain-side{side}");
            if (side == 0 && speed == 1 && OS.GetCmdlineUserArgs().Contains("--dust-fall-clip"))
            {
                for (int frame = 0; frame < 10; frame++)
                {
                    await Capture($"dust-fall-frame-{frame:00}");
                    field.SetTouPowderClock(false, speed);
                    if (IsInstanceValid(scheduled) && !scheduled.IsQueuedForDeletion()) scheduled._Process(.04);
                    packet.Advance(.04); field.SetTouPowderClock(true, speed);
                }
            }
            field.SetTouPowderClock(false, speed);
            if (IsInstanceValid(scheduled) && !scheduled.IsQueuedForDeletion()) scheduled._Process(.21 / speed);
            Require(field.FindPawn(2)!.PowderMarked, "予約した粉の飛行が到着して印を付ける");
            packet.Advance(.49 / speed);
            Require(await packet.Completion && field.DustChainPlays == 1, "粉の飛行でも0.6秒の連鎖を分割しない");
            GD.Print($"DUST_FALL_OK side={side} speed={speed} origin=silk_ball arrival_mark=true");
        }
        field.QueueFree(); await Wait(.1);
    }
}
