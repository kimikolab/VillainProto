using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    private async Task CheckDustBurst()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        var audio = (BattleAttackAudio)typeof(BattlefieldView3D).GetField("_attackAudio", Flags)!.GetValue(field)!;
        var capture = new AudioEffectCapture { BufferLength = 2 };
        int effect = AudioServer.GetBusEffectCount(0); AudioServer.AddBusEffect(0, capture);
        try
        {
            var formation = Presets.Versions.Single(p => p.Name == "試遊・感電 ポンの台").F;
            var players = BattleEngine.Materialize(formation, 0);
            var enemy = BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None);
            var opening = players.Concat(enemy).Select(u => new DemoOpening(u.InstanceId, u.TeamId, u.Def.Id,
                u.Name, u.Slot, u.Hp, u.Def.MaxHp, u.CurrentAttack, u.CurrentPattern, u.Def.Advances, u.Def.Traits)).ToArray();
            int heroId = enemy.Single().InstanceId, kuguId = players.Single(u => u.Def.Id == "kugu").InstanceId;
            foreach (int density in new[] { 1, 12, 30 })
            {
                field.BeginBattle(opening, $"粉塵爆発：粉 {density} ／ 糸玉から飛び火", 0);
                var events = new List<BattleEvent> { new() { Turn = 8, Kind = BattleEventKind.ShockSpent, TargetId = heroId } };
                foreach (int slot in Enumerable.Range(0, 9).Where(i => i != 2))
                {
                    int id = 100 + slot;
                    field.ShowSilkBall(new() { Turn = 8, Kind = BattleEventKind.SilkBall, ActorId = kuguId, TargetId = id,
                        Team = 1, Slot = slot, PartnerId = heroId, Text = SilkBallLabels.Place }, 1);
                    field.ShowTouPowder(new() { Turn = 8, Kind = BattleEventKind.ShockGauge, TargetId = id,
                        Text = ShockGaugeLabels.Dense, Amount = density }, 1);
                    events.Add(new() { Turn = 8, Kind = BattleEventKind.Discharge, ActorId = heroId, TargetId = id, Amount = 8 * (density + 1), ReceiverDensity = density });
                    events.Add(new() { Turn = 8, Kind = BattleEventKind.ShockGauge, ActorId = heroId, TargetId = id,
                        Text = ShockGaugeLabels.DustBurst, Amount = density * 8, Slot = density });
                    events.Add(new() { Turn = 8, Kind = BattleEventKind.Damage, ActorId = id, TargetId = heroId,
                        Amount = density * 8, HpAfter = 1000, FriendlyFire = true });
                    // 着弾後に粉が降っても、次の放電を別の連鎖へ切り離さない。
                    events.Add(new() { Turn = 8, Kind = BattleEventKind.ShockGauge, ActorId = id, TargetId = heroId,
                        Text = ShockGaugeLabels.DustFall, Amount = 1 });
                }
                events.Add(new() { Turn = 8, Kind = BattleEventKind.SilkBall, ActorId = kuguId, Text = SilkBallLabels.Recharge, Amount = 8 });
                var plan = DustBurstPresentation.Build(events);
                Require(plan.Starts.Count == 1, "着弾ごとに粉が降っても8つの爆発は1つの連鎖のまま");
                var chain = plan.Starts.Single().Value;
                Require(plan.DamageMembers.Count == 8 && chain.Totals[heroId] == density * 8 * 8, "台本の8着弾を勇者の1合計へ束ねる");
                Require(field.BallPowderCount == 8 && field.FindPawn(heroId)!.PowderDensity == 0, "糸玉の濃さを本人のHPや粉から独立して表示する");
                field.BeginDustBurstChain(chain, events, 1);
                foreach (var b in chain.Bursts) field.ShowTouPowder(events[b.Index], 1);
                var fx = field.DustChain!;
                Require(fx.BurstCount == 8 && fx.BranchCount == 8, "四方の糸玉から本人への飛び火を全件接続する");
                fx._Process(.20); field.SetTouPowderClock(true, 1);
                float pausedAge = fx.Age; await Wait(.08);
                Require(fx.Age == pausedAge && !fx.Completion.IsCompleted, "停止中は爆竹の時計・終了待ちも止まる");
                await Capture($"dust-burst-density{density}");
                if (density == 30 && OS.GetCmdlineUserArgs().Contains("--dust-clip"))
                {
                    field.BeginBattle(opening, "爆竹の連なり", 0);
                    foreach (int slot in Enumerable.Range(0, 9).Where(i => i != 2))
                        field.ShowSilkBall(new() { Turn = 8, Kind = BattleEventKind.SilkBall, ActorId = kuguId, TargetId = 100 + slot,
                            Team = 1, Slot = slot, Text = SilkBallLabels.Place }, 1);
                    field.BeginDustBurstChain(chain, events, 1); fx = field.DustChain!;
                    field.SetTouPowderClock(true, 1);
                    for (int frame = 0; frame < 14; frame++)
                    {
                        await Capture($"dust-burst-frame-{frame:00}");
                        field.SetTouPowderClock(false, 1); fx._Process(.04); field.SetTouPowderClock(true, 1);
                    }
                }
                field.SetTouPowderClock(false, 2); fx._Process(.4);
                Require(await fx.Completion && field.DustDamageSummaries == 1, "0.6秒の一団の後に合計数字は1つだけ");
                field.BeginDustBurstChain(chain, events, 1); var canceled = field.DustBurstCompletion;
                field.SetTouPowderClock(true, 1); field.Hide();
                Require(canceled.IsCompleted && !await canceled && field.DustChain is null && field.BallPowderCount == 0,
                    "離脱は予約した音・破裂・数字と終了待ちを取り消す");
                field.Show();
                foreach (double speed in new[] { 1.0, 2.0, 4.0 })
                {
                    field.BeginBattle(opening, "爆竹SEの出力", 0);
                    foreach (int slot in Enumerable.Range(0, 9).Where(i => i != 2))
                        field.ShowSilkBall(new() { Turn = 8, Kind = BattleEventKind.SilkBall, ActorId = kuguId, TargetId = 100 + slot,
                            Team = 1, Slot = slot, Text = SilkBallLabels.Place }, speed);
                    audio.StopAll(); await Wait(.12); capture.ClearBuffer();
                    int played = audio.DustCracklePlays;
                    field.BeginDustBurstChain(chain, events, speed);
                    await Wait(.6 / speed + .15);
                    var frames = capture.GetBuffer(capture.GetFramesAvailable());
                    float peak = frames.Length == 0 ? 0 : frames.Max(v => Math.Max(Math.Abs(v.X), Math.Abs(v.Y)));
                    Require(peak > .04f && peak < .95f && audio.DustCracklePlays - played >= 2,
                        "倍速でも軽い連続破裂音が最終出力まで複数回届き、クリップしない");
                    GD.Print($"DUST_BURST_MIX_OK density={density} speed={speed} peak={peak:F3} sounds={audio.DustCracklePlays - played}");
                }
                GD.Print($"DUST_BURST_VISUAL_OK density={density} bursts=8 branches=8");
            }
        }
        finally { audio.StopAll(); AudioServer.RemoveBusEffect(0, effect); field.QueueFree(); }
        await Wait(.1);
    }
}
