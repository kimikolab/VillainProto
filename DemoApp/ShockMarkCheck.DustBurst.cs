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
            var bomb = (AudioStream)typeof(BattleAttackAudio).GetMethod("LoadSound", Flags)!
                .Invoke(audio, new object[] { BattleAttackAudio.DustBurstFile })!;
            Require(bomb is AudioStreamWav && Math.Abs(bomb.GetLength() - 2.5) < .01,
                "指定のBit Bomb WAVを復号");
            await CheckDustAudible(field, audio, capture);
            var formation = Presets.Versions.Single(p => p.Name == "試遊・感電 ポンの台").F;
            var players = BattleEngine.Materialize(formation, 0);
            var enemy = BattleEngine.MaterializeEnemy(EnemyCatalog.BossRegularWave, EnemyScaleRule.None);
            var opening = players.Concat(enemy).Select(u => new DemoOpening(u.InstanceId, u.TeamId, u.Def.Id,
                u.Name, u.Slot, u.Hp, u.Def.MaxHp, u.CurrentAttack, u.CurrentPattern, u.Def.Advances, u.Def.Traits)).ToArray();
            int heroId = enemy.Single().InstanceId, kuguId = players.Single(u => u.Def.Id == "kugu").InstanceId;
            foreach (int density in new[] { 0, 1, 12, 30 })
            {
                int damage = Math.Max(1, density) * 8;
                field.BeginBattle(opening, $"粉塵爆発：粉 {density} ／ 糸玉から飛び火", 0);
                var events = new List<BattleEvent> { new() { Turn = 8, Kind = BattleEventKind.ShockSpent, TargetId = heroId } };
                foreach (int slot in Enumerable.Range(0, 9).Where(i => i != 2))
                {
                    int id = 100 + slot;
                    field.ShowSilkBall(new() { Turn = 8, Kind = BattleEventKind.SilkBall, ActorId = kuguId, TargetId = id,
                        Team = 1, Slot = slot, PartnerId = heroId, Text = SilkBallLabels.Place }, 1);
                    field.ShowTouPowder(new() { Turn = 8, Kind = BattleEventKind.ShockGauge, TargetId = id,
                        Text = ShockGaugeLabels.Dense, Amount = density }, 1);
                    if (density == 0)
                        field.ShowTouPowder(new() { Turn = 8, Kind = BattleEventKind.ShockGauge, TargetId = id,
                            Text = ShockGaugeLabels.PowderMark }, 1);
                    events.Add(new() { Turn = 8, Kind = BattleEventKind.Discharge, ActorId = heroId, TargetId = id, Amount = 8 * (density + 1), ReceiverDensity = density });
                    events.Add(new() { Turn = 8, Kind = BattleEventKind.ShockGauge, ActorId = heroId, TargetId = id,
                        Text = ShockGaugeLabels.DustBurst, Amount = damage, Slot = density });
                    events.Add(new() { Turn = 8, Kind = BattleEventKind.Damage, ActorId = id, TargetId = heroId,
                        Amount = damage, HpAfter = 1000, FriendlyFire = true });
                    // 着弾後に粉が降っても、次の放電を別の連鎖へ切り離さない。
                    events.Add(new() { Turn = 8, Kind = BattleEventKind.ShockGauge, ActorId = id, TargetId = heroId,
                        Text = ShockGaugeLabels.DustFall, Amount = 1 });
                }
                events.Add(new() { Turn = 8, Kind = BattleEventKind.SilkBall, ActorId = kuguId, Text = SilkBallLabels.Recharge, Amount = 8 });
                var plan = DustBurstPresentation.Build(events);
                Require(plan.Starts.Count == 1, "着弾ごとに粉が降っても8つの爆発は1つの連鎖のまま");
                var chain = plan.Starts.Single().Value;
                Require(plan.DamageMembers.Count == 8 && chain.Totals[heroId] == damage * 8, "台本の8着弾を勇者の1合計へ束ねる");
                Require(field.BallPowderCount == 8 && field.FindPawn(heroId)!.PowderDensity == 0, "糸玉の濃さを本人のHPや粉から独立して表示する");
                field.BeginDustBurstChain(chain, events, 1);
                foreach (var b in chain.Bursts) field.ShowTouPowder(events[b.Index], 1);
                var fx = field.DustChain!;
                Require(fx.BurstCount == 8 && fx.BranchCount == 8, "四方の糸玉から本人への飛び火を全件接続する");
                fx._Process(.4);
                Require(fx.Age == 0 && !fx.Completion.IsCompleted, "生成前の長いフレームで初期の爆発を飛ばさない");
                int initialPlayed = audio.DustCracklePlays;
                fx.Advance(.20); field.SetTouPowderClock(true, 1);
                var voices = (AudioStreamPlayer?[])typeof(BattleAttackAudio).GetField("_dustVoices", Flags)!.GetValue(audio)!;
                Require(audio.DustCracklePlays > initialPlayed
                    && voices.OfType<AudioStreamPlayer>().Any(v => v.Stream == bomb),
                    "粉の印だけの爆発も累積した爆発もBit Bombへ接続");
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
                        field.SetTouPowderClock(false, 1); fx.Advance(.04); field.SetTouPowderClock(true, 1);
                    }
                }
                field.SetTouPowderClock(false, 2); fx.Advance(.4);
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
                    GD.Print($"DUST_BURST_MIX density={density} speed={speed} peak={peak:F3} sounds={audio.DustCracklePlays - played} fps={Engine.GetFramesPerSecond()}");
                    Require(peak > .04f && peak < .95f && audio.DustCracklePlays - played >= 2,
                        "倍速でも軽い連続破裂音が最終出力まで複数回届き、クリップしない");
                    GD.Print($"DUST_BURST_MIX_OK density={density} speed={speed} peak={peak:F3} sounds={audio.DustCracklePlays - played}");
                    if (density == 0 && speed == 2) SaveDustAudioPreview(frames, "chain");
                }
                GD.Print($"DUST_BURST_VISUAL_OK density={density} bursts=8 branches=8");
            }
        }
        finally { audio.StopAll(); AudioServer.RemoveBusEffect(0, effect); field.QueueFree(); }
        await Wait(.1);
        Require(!Enumerable.Range(0, AudioServer.BusCount).Any(i => AudioServer.GetBusName(i).ToString().StartsWith("DustBurst_")),
            "離脱で専用の爆発音バスと定位・リミッターを残さない");
    }

    private async Task CheckDustAudible(BattlefieldView3D field, BattleAttackAudio audio, AudioEffectCapture capture)
    {
        var hush = typeof(BattlefieldView3D).GetMethod("SetHushAtmosphere", Flags)!;
        try
        {
            foreach (bool muted in new[] { false, true })
            foreach (double speed in new[] { 1.0, 2.0, 4.0 })
            foreach (float pan in new[] { -.65f, 0f, .65f })
            {
                hush.Invoke(field, new object[] { muted });
                audio.StopAll(); await Wait(.12); capture.ClearBuffer();
                Require(audio.PlayDustCrackle(0, pan, speed), "初期の単発爆発を再生");
                await Wait(.17);
                var voices = (AudioStreamPlayer?[])typeof(BattleAttackAudio).GetField("_dustVoices", Flags)!.GetValue(audio)!;
                Require(voices.OfType<AudioStreamPlayer>().Any(v => v.Playing),
                    "倍速でも0.17秒時点に爆発の芯と余韻が残る");
                await Wait(.20);
                var frames = capture.GetBuffer(capture.GetFramesAvailable());
                float peak = frames.Length == 0 ? 0 : frames.Max(v => Math.Max(Math.Abs(v.X), Math.Abs(v.Y)));
                double rms = frames.Length == 0 ? 0 : Math.Sqrt(frames.Average(v => ((double)v.X * v.X + (double)v.Y * v.Y) / 2));
                Require(peak > .24f && peak < .75f && rms > .024,
                    "粉の印だけの単発音が、左右・倍速・粛の下でも聞き取れる強さでMasterへ届く");
                GD.Print($"DUST_AUDIBLE_OK hush={muted} speed={speed} pan={pan} peak={peak:F3} rmsDb={20 * Math.Log10(rms):F1}");
                if (!muted && speed == 2 && pan == 0) SaveDustAudioPreview(frames, "single");
            }
            hush.Invoke(field, new object[] { false });
            audio.StopAll(); await Wait(.12); capture.ClearBuffer();
            var mixBus = (StringName)typeof(BattleAttackAudio).GetField("_dustMixBus", Flags)!.GetValue(audio)!;
            int bus = AudioServer.GetBusIndex(mixBus), effect = AudioServer.GetBusEffectCount(bus);
            var dustCapture = new AudioEffectCapture { BufferLength = 2 }; AudioServer.AddBusEffect(bus, dustCapture);
            try
            {
                audio.PlayShockMark(ShockMarkSound.ElectricWhip, 0, 2);
                audio.PlayShockMark(ShockMarkSound.MarkAdd, 0, 2);
                await Wait(.06); audio.PlayDustCrackle(0, .65f, 2); await Wait(.35);
                var dust = dustCapture.GetBuffer(dustCapture.GetFramesAvailable());
                double rms = Math.Sqrt(dust.Average(v => ((double)v.X * v.X + (double)v.Y * v.Y) / 2));
                var mixed = capture.GetBuffer(capture.GetFramesAvailable());
                float peak = mixed.Max(v => Math.Max(Math.Abs(v.X), Math.Abs(v.Y)));
                Require(rms > .024 && peak < .95f, "鞭・標の音と同時でも初期の爆発音は減衰せず、Masterで音割れしない");
                GD.Print($"DUST_AUDIO_BATTLE_MIX_OK peak={peak:F3} dustRmsDb={20 * Math.Log10(rms):F1}");
                SaveDustAudioPreview(mixed, "battle-mix");
            }
            finally { AudioServer.RemoveBusEffect(bus, effect); }
        }
        finally { hush.Invoke(field, new object[] { false }); audio.StopAll(); }
    }

    private static void SaveDustAudioPreview(Vector2[] frames, string name)
    {
        string? argument = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--dust-audio-dir="));
        if (argument is null) return;
        var data = new byte[frames.Length * 4];
        for (int i = 0; i < frames.Length; i++)
        for (int channel = 0; channel < 2; channel++)
        {
            short sample = (short)(Math.Clamp(channel == 0 ? frames[i].X : frames[i].Y, -1, 1) * short.MaxValue);
            System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(i * 4 + channel * 2, 2), sample);
        }
        var wav = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = (int)AudioServer.GetMixRate(), Stereo = true, Data = data };
        Require(wav.SaveToWav(argument["--dust-audio-dir=".Length..] + $"/dust-{name}.wav") == Error.Ok, "Masterの爆発音を保存");
    }
}
