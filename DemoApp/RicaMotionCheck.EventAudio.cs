using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

// --write-movie と併用する確認用。映像・音声とも本編の描画・再生経路を録画する。
public partial class RicaMotionCheck
{
    private async Task CaptureRicaEventAudio()
    {
        _field=new BattlefieldView3D();_field.SetAnchorsPreset(LayoutPreset.FullRect);AddChild(_field);
        var openings=FxCatalog.GetOpenings("rica.stage3.attack",false).Concat(new[]{
            new DemoOpening(5,0,"spore","胞子1",5,10,10,1,AttackPattern.Single,false),
            new DemoOpening(6,0,"spore","胞子2",6,10,10,1,AttackPattern.Single,false),
            new DemoOpening(7,0,"spore","胞子3",7,10,10,1,AttackPattern.Single,false),
        }).ToArray();
        _field.BeginBattle(openings,"リィカ 覚醒・光の吸収・落下斬りのSE確認",0);
        _field.SetDeathClock(false,1);_field.SetPoisonClock(false,1);
        var rica=_field.FindPawn(1)!;
        foreach(var pawn in _field.Pawns.Values)pawn.AnimationSpeed=1;
        var audio=_field.GetChildren().OfType<BattleAttackAudio>().Single();
        var cues=new List<string>();var records=new List<object>();
        int tick=0;
        audio.RicaEventPlayed+=cue=>{
            cues.Add(cue);records.Add(new{frame=tick,engineFrame=Engine.GetProcessFrames(),cue});
            GD.Print($"RICA_EVENT_AUDIO cue={cue} frame={tick} engineFrame={Engine.GetProcessFrames()}");
        };
        _field.SetSubline("覚醒3層 → 6層 → 9層 → 仲間3体からの光の吸収 → 三連撃の着地");
        Task? attacks=null;
        for(tick=0;tick<660;tick++)
        {
            if(tick is 30 or 126 or 222)
            {
                int layers=tick==30?3:tick==126?6:9;
                _field.SetSubline($"{layers}層への覚醒音");
                _field.ApplyDeathState(new(1,layers,0,0,rica.MaxHp));
            }
            if(tick==318)
            {
                _field.SetSubline("同時に倒れた仲間3体 → 光の吸収音は一つにまとめる");
                foreach(int id in new[]{5,6,7})
                {
                    var dead=_field.FindPawn(id)!;dead.SetHp(0);dead.AnimateDeath();
                    _field.ShowDeathOffering(dead);
                }
            }
            if(tick==414)
            {
                _field.SetSubline("三連撃 → 最後の落下斬りの接地で着地音（既存の斬撃音も再生）");
                attacks=RicaAudioReviewAttack(rica);
            }
            await Frame();
        }
        Require(attacks is{IsCompleted:true},"録画時間内に三連撃が完了");
        await attacks!;
        Require(cues.SequenceEqual(new[]{"awaken3","awaken6","awaken9","absorb","landing"}),
            "3段の覚醒・同時吸収1音・三連撃の着地1音");
        Require(_field.RicaAttackClipPlays==3 && _field.GraveOfferings==3,"3打と3体の光を実表示");
        string dir=ProjectSettings.GlobalizePath("res://../output/rica-event-audio-v17");Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir,"capture.json"),JsonSerializer.Serialize(new{
            fps=30,frames=tick,records,attacks=_field.RicaAttackClipPlays,offerings=_field.GraveOfferings,
        },new JsonSerializerOptions{WriteIndented=true})+"\n");
        GD.Print("RICA_EVENT_AUDIO_CAPTURE_OK awakenings=3 absorb=1 offerings=3 landing=1 attacks=3");
        GetTree().Quit();
    }
    private async Task RicaAudioReviewAttack(BattlePawn3D rica)
    {
        foreach(int id in new[]{2,3,4})
        {
            var target=_field.FindPawn(id)!;
            await _field.Attack(rica,target,AttackPattern.Sweep,new[]{target},advance:id==2,holdPosition:true);
            await rica.AttackClipFinished;
        }
        _field.FinishComboMotion(rica);
        await _field.FinishRicaMotions();
    }
}
