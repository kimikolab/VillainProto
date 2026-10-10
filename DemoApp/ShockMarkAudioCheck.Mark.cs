using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkAudioCheck
{
    // 再生要求の件数だけでなく、定位バスを経由した最終出力を採取する。
    private async Task CheckMarkMix(BattlefieldView3D field, BattleAttackAudio audio)
    {
        var capture = new AudioEffectCapture { BufferLength = 3 };
        int effect = AudioServer.GetBusEffectCount(0);
        AudioServer.AddBusEffect(0, capture);
        try
        {
            foreach (int team in new[] { 0, 1 })
            foreach (double speed in new[] { 1.0, 2.0 })
            {
                field.BeginBattle(new DemoOpening[] {
                    new(1, team, "hisa", "ヒサ", 4, 100, 100, 10, AttackPattern.Single, false, UnitCatalog.Hisa.Traits),
                    new(2, 1 - team, "knight", "標的", 0, 100, 100, 10, AttackPattern.Single, false),
                }, "標SEの出力確認", 0);
                await Wait(.15);
                var target = field.FindPawn(2)!;
                foreach (int layers in new[] { 1, 2 })
                {
                    audio.StopAll(); await Wait(.12); capture.ClearBuffer();
                    field.ShowMarkLayer(target, layers, speed);
                    await Wait(.4);
                    var frames = capture.GetBuffer(capture.GetFramesAvailable());
                    float peak = frames.Length == 0 ? 0 : frames.Max(v => Math.Max(Math.Abs(v.X), Math.Abs(v.Y)));
                    double rms = frames.Length == 0 ? 0 : Math.Sqrt(frames.Average(v => ((double)v.X * v.X + (double)v.Y * v.Y) / 2));
                    GD.Print($"MARK_AUDIO_MIX team={team} speed={speed} layers={layers} frames={frames.Length} peak={peak:F4} rmsDb={20 * Math.Log10(Math.Max(rms, 1e-9)):F1}");
                    Require(frames.Length > 1000 && peak > .24f && peak < .40f && rms > .035,
                        "標SEが最終出力まで届き、従来より3 dB低い音量に収まる");
                    field.ShowMarkLayer(target, layers, 1);
                    field.ShowMarkLayer(target, 0, 1);
                    Require(audio.ShockAssetPlays.GetValueOrDefault(ShockMarkSound.MarkAdd) == 1, "同値・除去で重ねない");
                    target.SetMarkLayers(layers);
                }
                audio.StopAll(); await Wait(.12); capture.ClearBuffer();
                var layersAtImpact = Enumerable.Range(3, 3).Select(n => new BattleEvent {
                    Kind = BattleEventKind.MarkLayer, Turn = 1, ActorId = 1, TargetId = 2, Amount = n }).ToArray();
                await field.ShowCommand(field.FindPawn(1), target, new BattleEvent {
                    Kind = BattleEventKind.Command, Turn = 1, Amount = 3, Slot = 60, Text = CommandLabels.Turn }, layersAtImpact, speed);
                await Wait(.12);
                var commandFrames = capture.GetBuffer(capture.GetFramesAvailable());
                float commandPeak = commandFrames.Length == 0 ? 0 : commandFrames.Max(v => Math.Max(Math.Abs(v.X), Math.Abs(v.Y)));
                Require(commandPeak > .24f && audio.ShockAssetPlays.GetValueOrDefault(ShockMarkSound.MarkAdd) == 1,
                    "号令の着弾で指定音が最終出力まで1回届く");
                GD.Print($"MARK_COMMAND_MIX team={team} speed={speed} peak={commandPeak:F4}");
            }
        }
        finally { audio.StopAll(); AudioServer.RemoveBusEffect(0, effect); }
    }
}
