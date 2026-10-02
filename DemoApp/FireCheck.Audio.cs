using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class FireCheck
{
    private async Task CheckFireAudio()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        var audio = (BattleAttackAudio)typeof(BattlefieldView3D).GetField("_attackAudio", Flags)!.GetValue(field)!;
        foreach (var entry in BattleAttackAudio.FireSounds)
        {
            var stream = (AudioStream)typeof(BattleAttackAudio).GetMethod("LoadSound", Flags)!
                .Invoke(audio, new object[] { "res://assets/audio/se/" + entry.Value })!;
            Require(stream.GetLength() > 0, "追加した音源を復号: " + entry.Key);
            GD.Print($"FIRE_AUDIO_FILE_OK {entry.Key} seconds={stream.GetLength():F3}");
        }
        DemoOpening[] openings = {
            new(1,0,"hota","ホタ",2,100,100,20,AttackPattern.Single,false),
            new(2,1,"knight","標的",2,100,100,20,AttackPattern.Single,true),
            new(3,0,"borg","ボルグ",0,100,100,20,AttackPattern.Sweep,true),
        };
        void Reset(double speed)
        {
            field.BeginBattle(openings, "炎の効果音", 3);
            foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
        }
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            Reset(speed);
            var task = field.PlayFireAttack(field.FindPawn(1), field.FindPawn(2), new[] { field.FindPawn(2)! },
                new(FireLevelLabels.Burnout, 4, 0), speed);
            Require(audio.FireSoundPlays.GetValueOrDefault("raise") == 1
                && !audio.FireSoundPlays.ContainsKey("burnout"), "掲剣だけ鳴り、まだ爆発しない");
            await task;
            Require(audio.FireSoundPlays.GetValueOrDefault("burnout") == 1, "着弾で大剣と爆発を一度再生");
            await Wait(0.12 / speed);
            var voices = (AudioStreamPlayer?[])typeof(BattleAttackAudio).GetField("_fireVoices", Flags)!.GetValue(audio)!;
            Require(voices[0] is { Playing: false } && voices[1] is { Playing: true }
                && voices[2] is { Playing: true } && voices[3] is { Playing: true }, "掲剣を止め、衝撃2層と火柱の3層が重なる");
            for (int i = 0; i < 10; i++) audio.PlayFireSound("rain");
            Require(voices[3]!.Playing && voices.All(v => v is null || v.PitchScale == 1), "火の雨で火柱を切らず、倍速でも原音ピッチ");
            audio.StopAll();
            Require(voices.All(v => v is null || !v.Playing), "停止で大技の全層を停止");
            GD.Print($"FIRE_AUDIO_TIMING_OK speed={speed}");
        }
        Reset(1);
        audio.PlayFireSound("burnout");
        audio.StopAll();
        await Wait(0.2);
        Require(audio.GetChildren().OfType<AudioStreamPlayer>().All(v => !v.Playing), "停止後に遅延した火柱音が復活しない");
        Reset(1);
        var interrupted = field.PlayFireAttack(field.FindPawn(1), field.FindPawn(2), new[] { field.FindPawn(2)! },
            new(FireLevelLabels.Burnout, 4, 0), 1);
        audio.StopAll();
        await interrupted;
        Require(!audio.FireSoundPlays.ContainsKey("burnout"), "掲剣中の停止で古い着弾音を鳴らさない");
        Reset(2);
        for (int i = 1; i <= 5; i++)
            await field.PlayFireAttack(field.FindPawn(1), field.FindPawn(2), new[] { field.FindPawn(2)! },
                new(FireLevelLabels.EmbersHit, 1, i), 2);
        Require(audio.FireSoundPlays.GetValueOrDefault("embers") == 5, "残り火は5回の着弾ごとに鳴る");
        audio.StopAll();
        for (int i = 0; i < 10; i++) audio.PlayFireSound("aura");
        Require(audio.FireSoundPlays.GetValueOrDefault("aura") == 1, "同時に火勢4へ上がる音を重ねすぎない");
        field.QueueFree();
        GD.Print("FIRE_AUDIO_CHECK_OK");
        await Wait(0.1);
    }
}
