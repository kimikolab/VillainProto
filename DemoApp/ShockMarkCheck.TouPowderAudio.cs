using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    private async Task CheckTouPowderAudio()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        var audio = (BattleAttackAudio)typeof(BattlefieldView3D).GetField("_attackAudio", Flags)!.GetValue(field)!;
        int[] densities = [1, 8, 32];
        var sounds = densities.Select(density => (AudioStream)typeof(BattleAttackAudio).GetMethod("LoadSound", Flags)!
            .Invoke(audio, new object[] { BattleAttackAudio.PowderBurnFileFor(density) })!).ToArray();
        Require(sounds.Distinct().Count() == 3, "小・中・大で別の指定音源を使う");
        foreach (var sound in sounds)
        {
            Require(sound is AudioStreamWav && Math.Abs(sound.GetLength() - 2.5) < .01, "指定Phoenix Spark WAVを復号");
            using var playback = sound.InstantiatePlayback();
            playback.Start();
            var samples = playback.MixAudio(1, (int)(AudioServer.GetMixRate() * .2));
            Require(samples.Any(s => Math.Max(Math.Abs(s.X), Math.Abs(s.Y)) > .01), "短い再生尺の頭に火花音がある");
            playback.Stop();
        }
        foreach (var (density, tier) in new[] { (1, 1), (2, 1), (3, 2), (14, 2), (15, 3), (32, 3), (int.MaxValue, 3) })
            Require(BattleAttackAudio.PowderBurnFileFor(density).EndsWith($"_{tier}.wav"), $"濃さ{density}の音源段階{tier}");
        AudioStreamPlayer[] Voices() => audio.GetChildren().OfType<AudioStreamPlayer>().Where(v => sounds.Contains(v.Stream)).ToArray();
        DemoOpening[] opening = [ new(1, 1, "hero", "粉焼の対象", 2, 100, 100, 10, AttackPattern.Single, false) ];
        BattleEvent Cue(string text, int density) => new() { Turn = 1, Kind = BattleEventKind.ShockGauge, ActorId = 1, TargetId = 1,
            Text = text, Amount = 24, Slot = density };
        field.BeginBattle(opening, "粉焼SE・密集", 0);
        foreach (int density in densities) field.ShowTouPowder(Cue(ShockGaugeLabels.DenseBurn, density), 1);
        Require(Voices().Count(v => v.Playing) == 1 && Voices().Single(v => v.Playing).Stream == sounds[2], "同時の小・中・大は大の1音にまとめる");
        field.ShowTouPowder(Cue(ShockGaugeLabels.DenseBurn, 1), 1);
        Require(Voices().Single(v => v.Playing).Stream == sounds[2], "後から小が来ても大の音を維持する");
        foreach (double speed in new[] { 1.0, 2.0, 4.0 })
        {
            for (int tier = 0; tier < densities.Length; tier++)
            {
                int density = densities[tier];
                field.BeginBattle(opening, "粉焼の指定SE", 0);
                field.ShowTouPowder(Cue(ShockGaugeLabels.DensePop, density), speed);
                Require(Voices().All(v => !v.Playing), "弾けや加算には粉焼音を重複させない");
                field.ShowTouPowder(Cue(ShockGaugeLabels.DenseBurn, density), speed);
                Require(Voices().Count(v => v.Playing) == 1 && Voices().All(v => v.PitchScale == 1), "点火時に指定音を原音程で再生");
                Require(Voices().Single(v => v.Playing).Stream == sounds[tier], $"濃さ{density}の火花とPhoenix Spark 00{tier + 1}を接続");
                field.ShowTouPowder(Cue(ShockGaugeLabels.DenseBurn, density), speed);
                Require(Voices().Count(v => v.Playing) == 1, "同時の粉焼をまとめる");
                var active = Voices().Where(v => v.Playing).ToArray();
                field.SetTouPowderClock(true, speed);
                await Wait(.12);
                Require(active.All(v => v.StreamPaused), "一時停止で音も停止");
                field.SetTouPowderClock(false, speed * 2);
                Require(Voices().All(v => !v.StreamPaused && v.PitchScale == 1), "再開と速度変更でも音程を維持");
                await Wait(.55);
                Require(Voices().All(v => !v.Playing), "短い末尾フェードで終了");
                // 完了したTweenを途中の速度変更で再起動しない。
                field.SetTouPowderClock(false, speed);
                GD.Print($"TOU_POWDER_AUDIO_OK speed={speed} density={density} source=PhoenixSpark00{tier + 1}");
            }
            for (int i = 0; i < 5; i++) { audio.PlayPowderBurn(densities[i % 3], 0, .5); await Wait(.10); }
            Require(Voices().Length <= 3, "連続発動でも専用3枠以内");
            field.SetTouPowderClock(true, speed);
            field.BeginBattle(opening, "粉焼SE・再戦", 0);
            Require(Voices().All(v => !v.Playing && !v.StreamPaused), "停止中の再戦でも音とフェードを消す");
            field.ShowTouPowder(Cue(ShockGaugeLabels.DenseBurn, 32), speed);
            field.Hide();
            Require(Voices().All(v => !v.Playing), "画面離脱で音を止める");
            field.Show();
            GD.Print($"TOU_POWDER_AUDIO_POOL_OK speed={speed} voices={Voices().Length}");
        }
        if (OS.GetCmdlineUserArgs().Contains("--powder-mix")) await CheckTouPowderMix(field, audio);
        field.QueueFree(); await Wait(.1);
    }

    // 実際の粉焼の演出口から、左右定位を通ったMasterの出力を測る。
    private async Task CheckTouPowderMix(BattlefieldView3D field, BattleAttackAudio audio)
    {
        var capture = new AudioEffectCapture { BufferLength = 2 };
        int effect = AudioServer.GetBusEffectCount(0);
        AudioServer.AddBusEffect(0, capture);
        try
        {
            foreach (double speed in new[] { 1.0, 2.0, 4.0 })
            foreach (int density in new[] { 1, 8, 32 })
            foreach (int team in new[] { 0, 1 })
            {
                field.BeginBattle([new(1, team, "hero", "粉焼の対象", 2, 100, 100, 10, AttackPattern.Single, false)], "粉焼SEの出力確認", 0);
                audio.StopAll(); await Wait(.12); capture.ClearBuffer();
                field.ShowTouPowder(new() { Turn = 1, Kind = BattleEventKind.ShockGauge,
                    ActorId = 1, TargetId = 1, Text = ShockGaugeLabels.DenseBurn, Amount = density * 8, Slot = density }, speed);
                await Wait(.40);
                var frames = capture.GetBuffer(capture.GetFramesAvailable());
                float peak = frames.Length == 0 ? 0 : frames.Max(v => Math.Max(Math.Abs(v.X), Math.Abs(v.Y)));
                double rms = frames.Length == 0 ? 0 : Math.Sqrt(frames.Average(v => ((double)v.X * v.X + (double)v.Y * v.Y) / 2));
                GD.Print($"TOU_POWDER_AUDIO_MIX team={team} speed={speed} density={density} frames={frames.Length} peak={peak:F4} rmsDb={20 * Math.Log10(Math.Max(rms, 1e-9)):F1}");
                Require(frames.Length > 1000 && peak > .18f && peak < .8f && rms > .025,
                    "粉焼SEが左右定位を通り、倍速でも聞き取れる強さで最終出力へ届く");
            }
        }
        finally { audio.StopAll(); AudioServer.RemoveBusEffect(0, effect); }
        GD.Print("TOU_POWDER_AUDIO_MIX_OK");
    }
}
