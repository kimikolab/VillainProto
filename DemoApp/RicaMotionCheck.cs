using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 7打の実コマ・接触同期と、FxViewerと共通のMain.ApplyEventによる4段の再生を検査する。
public partial class RicaMotionCheck : Control
{
    private BattlefieldView3D _field = null!;
    private int _checks;
    private static readonly string[][] Clips = {
        new[]{"rica_shovel"},new[]{"rica_crescent"},new[]{"rica_rise","rica_return"},
        new[]{"rica_thrust","rica_sweep","rica_descend"},
    };
    public override async void _Ready()
    {
        try
        {
            if(OS.GetCmdlineUserArgs().Contains("--idle-capture"))
            {
                _field=new BattlefieldView3D();_field.SetAnchorsPreset(LayoutPreset.FullRect);AddChild(_field);
                foreach(int stage in new[]{0,1,2,3})
                {
                    Begin(stage,0,1);await Wait(.25);await Capture($"idle-stage{stage}");
                }
                GD.Print("RICA_IDLE_RENDER_OK stages=4");GetTree().Quit();return;
            }
            foreach(string name in Clips.SelectMany(c=>c))
            {
                var clip=BattlePawn3D.AttackClipOf(name);
                Require(clip is not null && clip.Frames.Length>=12 && clip.Contact>0 && clip.Contact<clip.Frames.Length,"実コマと命中点 "+name);
                Require(BattlePawn3D.RicaTrackOf(name).Length==clip!.Frames.Length,"追跡点が全コマに存在 "+name);
                if(name=="rica_return")
                {
                    var angles=BattlePawn3D.RicaTrackOf(name).Select(f=>MathF.Atan2(-(f.Tip-f.Socket).Y,(f.Tip-f.Socket).X)).ToArray();
                    Require(angles[0]>1.3f && angles[^1]<-.6f,"2撃目は頭上から前方斜め下へ振り抜く");
                    Require(angles.Zip(angles.Skip(1),(a,b)=>b<=a+.03f && a-b<.9f).All(v=>v),
                        "2撃目の剣先は大きく飛ばず同じ方向へ下がる");
                    int release=Array.FindIndex(angles,a=>a<angles[0]-.05f);
                    Require(release>=4 && clip.Contact-release<=4,"頭上で溜め、振り出しから4コマ以内に命中する");
                }
                if(name is "rica_crescent" or "rica_sweep")
                {
                    var turns=BattlePawn3D.RicaTrackOf(name).Select(f=>f.Turn).ToArray();
                    Require(turns[^1]-turns[0]>=360 && turns.Zip(turns.Skip(1),(a,b)=>b>=a).All(v=>v),
                        "回転角は逆戻りせず一周を超える "+name);
                    if(name=="rica_crescent")
                    {
                        int hit=clip.Contact;
                        Require(turns[hit+1]-turns[hit]>=(turns[hit]-turns[hit-1])*.8f && turns[hit+5]-turns[hit]>=100,
                            "命中で急停止せず、敵の向こうへ100度以上振り抜く");
                    }
                }
                foreach(var frame in BattlePawn3D.RicaTrackOf(name))
                    Require(new[]{frame.Socket,frame.Tip,frame.Eye}.All(p=>p.X>=0 && p.X<=1 && p.Y>=0 && p.Y<=1),
                        "全コマのシャベルと目が枠内 "+name);
            }
            _field=new BattlefieldView3D();_field.SetAnchorsPreset(LayoutPreset.FullRect);AddChild(_field);
            foreach(int stage in new[]{0,1,2,3})
                Require(BattlePawn3D.RicaApproachOf(stage) is {} pose && pose.Texture.GetSize()==new Vector2(1152,896),
                    "4段階の移動構えが共通キャンバスに存在する");
            foreach(int team in new[]{0,1})
            foreach(int stage in new[]{0,1,2,3}) await Direct(stage,team);
            await RangeAttack();
            await ApproachInterruptions();
            await LinkInterruptions();
            await Interruptions();
            _field.QueueFree(); await Frame();
            await ViewerCues();
            GD.Print($"RICA_MOTION_CHECK_OK checks={_checks} clips=7 stages=4 mirrors contacts pause speed cancel replay hp");
            GetTree().Quit();
        }
        catch(Exception e){GetTree().Paused=false;GD.PushError(e.ToString());GetTree().Quit(1);}
    }

    private (BattlePawn3D rica,BattlePawn3D target) Begin(int stage,int team,double speed=2)
    {
        var cue=FxCatalog.Find($"rica.stage{stage}.attack");
        _field.BeginBattle(FxCatalog.GetOpenings(cue.Id,team==1),cue.Title,0);
        var rica=_field.FindPawn(1)!;
        foreach(var pawn in _field.Pawns.Values)pawn.AnimationSpeed=speed;
        _field.SetPoisonClock(false,speed);_field.SetDeathClock(false,speed);
        _field.ApplyDeathState(new(1,stage*3,0,0,rica.MaxHp));
        return(rica,_field.FindPawn(2)!);
    }

    private async Task Direct(int stage,int team)
    {
        var (rica,target)=Begin(stage,team);
        Require(rica.GraveStage==stage,"段を表示写しから受ける");
        var before=rica.DeathSprite.Texture;
        int contacts=0;
        foreach(string name in Clips[stage])
        {
            int old=contacts;
            var clip=BattlePawn3D.AttackClipOf(name)!;
            _field.AttackContact=p=>{
                contacts++;
                Require(p==target && rica.AttackClipFrame>=clip.Contact,"実接触コマで対象に1回通知");
                if(name=="rica_return")
                {
                    Require(_field.RicaReturnContactCount==1,"2撃目の命中は独立した円弧ではなく刃上の短い閃光");
                    var(root,tip)=rica.RicaBladeSegment(rica.GetViewport().GetCamera3D());var blade=tip-root;
                    Vector3 point=_field.RicaReturnContactPoint;
                    float t=Math.Clamp((point-root).Dot(blade)/blade.LengthSquared(),0,1);
                    Require(point.DistanceTo(root+blade*t)<.01f,"2撃目の命中光は実際のエネルギー刃の上にある");
                }
            };
            var attack=_field.Attack(rica,target,AttackPattern.Single,new[]{target},advance:name==Clips[stage][0],holdPosition:true);
            if(name==Clips[stage][0])
            {
                await Until(()=>rica.RicaApproachActive);
                Require(!rica.IdleLoopPlaying && !rica.AttackClipPlaying && rica.DeathSprite.Texture==BattlePawn3D.RicaApproachOf(stage)!.Texture,
                    "敵前への移動中は段別の構えを使う");
                Require(rica.DeathSprite.FlipH==(team==1),"移動の構えも陣営に合わせて左右反転");
                Vector3 position=rica.Position;
                _field.SetDeathClock(true,2);_field.SetPoisonClock(true,2);await Wait(.08);
                Require(rica.RicaApproachActive && rica.Position==position,"停止中も移動構えと位置を保持する");
                await Capture($"stage{stage}-team{team}-approach");
                _field.SetDeathClock(false,2);_field.SetPoisonClock(false,2);
            }
            else Require(!rica.RicaApproachActive && rica.RicaLinkActive,"連撃の移動中もつなぎのセルを進める");
            await Until(()=>rica.AttackClipPlaying && rica.RicaClipName==name);
            Require(!rica.RicaLinkActive,"つなぎから次の実攻撃へ引き継ぐ");
            Require(!rica.RicaApproachActive,"移動構えから攻撃へ直接引き継ぐ");
            Require(rica.RicaClipName==name && !rica.IdleLoopPlaying,"段内の攻撃順と待機停止 "+name);
            Require(rica.DeathSprite.FlipH==(team==1),"攻撃の左右反転");
            if(name=="rica_crescent")
            {
                await Until(()=>!rica.CurrentRicaAnchors().EyesVisible);
                await Frame();
                Require(!rica.CurrentRicaAnchors().EyesVisible,"回転薙ぎの途中で背中を向ける");
                var material=(ShaderMaterial)rica.DeathSprite.MaterialOverride;
                Require(material.GetShaderParameter("grave_eye").AsVector2().X<0,"後頭部には目の発光を出さない");
                float turn=rica.CurrentRicaTurn(),sweep=rica.RicaSpinSweep;
                _field.SetDeathClock(true,2);await Wait(.08);
                Require(rica.CurrentRicaTurn()==turn && rica.RicaSpinSweep==sweep,"停止中は回転角と一周の軌跡も固定する");
                _field.SetDeathClock(false,2);
            }
            if (name == "rica_descend")
                Require(rica.AttackClipVertical > .5f && rica.HudAnchor.Y-rica.GlobalPosition.Y > 2,
                    "空中の絵とHPの札が一緒に上がる");
            Require(contacts==old,"溜め中は未命中");
            await Capture($"stage{stage}-team{team}-{name}-windup");
            await attack;
            Require(contacts==old+1 && rica.AttackClipPlaying,"一打一通知で振り切りを続ける");
            if(name=="rica_shovel")Require(_field.RicaBluntCount==1,"シャベルの命中に一つの打撃衝撃を出す");
            if(name=="rica_descend")Require(rica.AttackClipVertical==0,"落下斬りの命中コマで着地する");
            if(name=="rica_crescent")
            {
                await Frame();
                Require(rica.RicaSpinSweep>=Mathf.Tau-.01f && rica.RicaSpinBothSides,"命中時に回転軌跡の両側を描く");
            }
            await Capture($"stage{stage}-team{team}-{name}-contact");
            await rica.AttackClipFinished;
            if(name=="rica_sweep")
            {
                await Frame();
                Require(rica.RicaSpinSweep>=Mathf.Tau-.01f && rica.RicaSpinBothSides,
                    $"回転軌跡が手前と奥の全周を含む {name} {rica.RicaSpinSweep} / {rica.RicaSpinBothSides}");
            }
            if(name=="rica_crescent")Require(rica.CurrentRicaTrailAlpha()==0,"振り抜き後の待機では剣先の光点を残さない");
            Require(rica.DeathSprite.Texture==clip.Frames[^1] && rica.RicaClipName==name,$"連撃中は振り切りを保持する {name}");
            if(name=="rica_sweep")
            {
                float y=rica.DeathSprite.Position.Y;
                Require(rica.AttackClipVertical>=2.7f,"回転斬りの振り切りで高く上昇する");
                await Wait(.05);
                Require(rica.AttackClipVertical>=2.7f && Math.Abs(rica.DeathSprite.Position.Y-y)<.06f,
                    "空中の構えを次の落下斬りまで保持する");
                var next=BattlePawn3D.AttackClipOf("rica_descend")!;
                Require(Math.Abs(next.Vertical[0]-rica.AttackClipVertical)<.01f && next.Vertical.Max()>=3,
                    "同じ高さからためて落下へつなぐ");
            }
        }
        _field.FinishComboMotion(rica);await Wait(.20);
        Require(rica.AttackClipVertical==0,"連撃後は地上の待機へ戻る");
        Require(_field.RicaPlayedClips.SequenceEqual(Clips[stage]),"段内の全打を順に使う");
        Require(_field.RicaLinksPlayed.Count==(stage<2?0:stage==2?1:2),"つなぎを打数へ数えず段間にだけ使う");
        Require(_field.GraveSlashes==Clips[stage].Length,"斬撃件数を増やさない");
        if(stage>0)Require(rica.RicaWeaponShape>0,"待機中も武器の殻を表示");
        if(stage>=2)Require(rica.RicaWeaponShape==2,"6層以上の待機は幅のある大剣へ戻る");
    }

    private async Task Interruptions()
    {
        var(rica,target)=Begin(3,0,1);
        var attack=_field.Attack(rica,target,AttackPattern.Single,new[]{target},holdPosition:true);
        await Until(()=>rica.AttackClipPlaying);
        int frame=rica.AttackClipFrame;double age=rica.RicaFxAge;
        Vector3 position=rica.Position,spritePosition=rica.DeathSprite.Position;
        _field.SetDeathClock(true,1);_field.SetPoisonClock(true,1);await Wait(.12);
        Require(rica.AttackClipFrame==frame && rica.RicaFxAge==age && rica.Position==position && rica.DeathSprite.Position==spritePosition,
            "本編の時計だけの停止でも連番と身体を止める");
        _field.SetDeathClock(false,1);_field.SetPoisonClock(false,1);
        GetTree().Paused=true;await Wait(.12);
        Require(rica.AttackClipFrame==frame && rica.RicaFxAge==age,"停止は動画と武器・身体に共通");
        GetTree().Paused=false;
        rica.AnimationSpeed=4;_field.SetDeathClock(false,4);_field.SetPoisonClock(false,4);
        await attack;Require(rica.AttackClipFrame>frame && rica.RicaFxAge>age,"途中倍速変更で再開");
        await rica.AttackClipFinished;
        attack=_field.Attack(rica,target,AttackPattern.Single,new[]{target},advance:false);
        await Until(()=>rica.AttackClipPlaying);
        int slashes=_field.GraveSlashes;
        _field.PlayDeath(rica);await attack;
        Require(!rica.AttackClipPlaying && _field.GraveSlashes==slashes,"命中前の死亡では斬撃を出さない");
        _field.RevivePawn(rica);await Wait(.04);
        Require(rica.DeathActive && rica.GraveStage==3,"蘇生後に保持した段の絵へ戻る");
        (rica,target)=Begin(3,0,1);
        attack=_field.Attack(rica,target,AttackPattern.Single,new[]{target},holdPosition:true);
        await Until(()=>rica.AttackClipPlaying);
        _field.Hide();_field.Show();await attack;
        Require(!rica.AttackClipPlaying && _field.GraveSlashes==0,"非表示で古い命中待ちを破棄");
        (rica,target)=Begin(2,0,1);
        attack=_field.Attack(rica,target,AttackPattern.Single,new[]{target},holdPosition:true);
        await Until(()=>rica.AttackClipPlaying);
        Begin(0,1);await attack;
        Require(_field.RicaAttackClipPlays==0 && _field.GraveSlashes==0,"再戦へ古い命中を持ち込まない");
        // 落下斬りの空中のためを単独で中断し、高さと未命中の片付けを確認する。
        (rica,target)=Begin(3,0,1);
        rica.NextRicaClip(true);rica.NextRicaClip(false);
        attack=_field.Attack(rica,target,AttackPattern.Single,new[]{target},advance:false,holdPosition:true);
        await Until(()=>rica.AttackClipPlaying && rica.AttackClipVertical>2.7f);
        frame=rica.AttackClipFrame;float height=rica.AttackClipVertical;
        _field.SetDeathClock(true,1);await Wait(.12);
        Require(rica.AttackClipFrame==frame && rica.AttackClipVertical==height,"空中のためも停止時計で固定する");
        _field.SetDeathClock(false,1);_field.PlayDeath(rica);await attack;
        Require(rica.AttackClipVertical==0 && !rica.AttackClipPlaying && _field.GraveSlashes==0,
            "空中での死亡は高さを戻し、落下斬りの命中を持ち越さない");
    }

    private async Task ApproachInterruptions()
    {
        var(rica,target)=Begin(1,0,1);
        var attack=_field.Attack(rica,target,AttackPattern.Single,new[]{target},holdPosition:true);
        await Until(()=>rica.RicaApproachActive);
        _field.PlayDeath(rica);await attack;
        Require(!rica.RicaApproachActive && !rica.AttackClipPlaying && _field.GraveSlashes==0,"移動中の死亡で構えと未命中を破棄する");
        _field.RevivePawn(rica);await Wait(.05);
        Require(!rica.RicaApproachActive && rica.IdleLoopPlaying,"蘇生後は移動構えを残さず待機へ戻る");
        (rica,target)=Begin(1,0,1);
        attack=_field.Attack(rica,target,AttackPattern.Single,new[]{target},holdPosition:true);
        await Until(()=>rica.RicaApproachActive);
        _field.Hide();_field.Show();await attack;
        Require(!rica.RicaApproachActive && !rica.AttackClipPlaying && _field.GraveSlashes==0,"移動中の非表示でも構えと攻撃を解除する");
    }

    private async Task LinkInterruptions()
    {
        var(rica,target)=Begin(3,0,1);
        await _field.Attack(rica,target,AttackPattern.Single,new[]{target},holdPosition:true);
        await rica.AttackClipFinished;
        var attack=_field.Attack(rica,target,AttackPattern.Single,new[]{target},advance:false,holdPosition:true);
        await Until(()=>rica.RicaLinkActive && rica.AttackClipFrame>=1);
        Require(!rica.RicaWeaponTrailVisible,"振りかぶりには斬撃の半円を描かない");
        int frame=rica.AttackClipFrame,slashes=_field.GraveSlashes;
        Vector3 position=rica.Position;double age=rica.RicaFxAge;
        _field.SetDeathClock(true,1);_field.SetPoisonClock(true,1);await Wait(.12);
        Require(rica.RicaLinkActive && rica.AttackClipFrame==frame && rica.Position==position && rica.RicaFxAge==age,
            "連撃間のつなぎも移動・原画・オーラを同時に停止する");
        _field.PlayDeath(rica);
        _field.SetDeathClock(false,1);_field.SetPoisonClock(false,1);await attack;
        Require(!rica.RicaLinkActive && !rica.AttackClipPlaying && _field.GraveSlashes==slashes && _field.RicaAttackClipPlays==1,
            "つなぎ中の死亡で次の実攻撃と命中を発生させない");
        (rica,target)=Begin(2,0,1);
        await _field.Attack(rica,target,AttackPattern.Single,new[]{target},holdPosition:true);
        await rica.AttackClipFinished;
        attack=_field.Attack(rica,target,AttackPattern.Single,new[]{target},advance:false,holdPosition:true);
        await Until(()=>rica.RicaLinkActive);
        _field.Hide();_field.Show();await attack;
        Require(!rica.RicaLinkActive && !rica.AttackClipPlaying && _field.GraveSlashes==0 && _field.RicaAttackClipPlays==0,
            "つなぎ中の非表示でも次の実攻撃を持ち越さない");
    }

    private async Task RangeAttack()
    {
        var(rica,target)=Begin(1,0,1);
        var hits=new[]{target,_field.FindPawn(3)!,_field.FindPawn(4)!};
        var contacts=new System.Collections.Generic.List<BattlePawn3D>();
        _field.AttackContact=contacts.Add;
        await _field.Attack(rica,target,AttackPattern.Sweep,hits);
        Require(contacts.Count==3 && contacts.Distinct().Count()==3,"範囲攻撃も実対象ごとに1通知");
        Require(_field.RicaAttackClipPlays==1 && _field.RicaSlashCount==1,"複数対象を一振り・一枚の剣閃で覆う");
        await rica.AttackClipFinished;
    }

    private async Task ViewerCues()
    {
        var main=new Main{ViewerMode=true};AddChild(main);
        foreach(bool mirror in new[]{false,true})
        foreach(int stage in new[]{0,1,2,3})
        {
            var cue=FxCatalog.Find($"rica.stage{stage}.attack");
            await main.StopViewerPlayback();
            main.SetPlaybackSpeed(8);main.ViewerFastPlayback=true;
            main.EnterScripted(FxCatalog.GetOpenings(cue.Id,mirror),cue.Events!(),cue.Title,0,cue.RicaLayers);
            await Until(()=>!main.IsPlaying,main);
            Require(main.ViewerPlaybackError is null && main.EventIndex==main.PlaybackEvents.Count,"FxViewer固定台本の完走");
            Require(main.ViewerField.RicaPlayedClips.SequenceEqual(Clips[stage]),"Mainの連撃も全打を順に再生");
            foreach(var e in main.PlaybackEvents.Where(e=>e.Kind==BattleEventKind.Damage).GroupBy(e=>e.TargetId).Select(g=>g.Last()))
                Require(main.ViewerField.FindPawn(e.TargetId)!.Hp==e.HpAfter,"FxViewerの対象と確定HP");
            await main.SeekTo(0);
            Require(main.ViewerField.FindPawn(1)!.GraveStage==stage,"シークしても指定段を復元");
            main.ResumePlayback();await Until(()=>!main.IsPlaying,main);
            Require(main.ViewerField.RicaPlayedClips.SequenceEqual(Clips[stage]),"シーク後の再生");
        }
        await main.StopViewerPlayback();main.QueueFree();await Frame();
    }

    private async Task Until(Func<bool> condition,Main? main=null)
    {
        ulong end=Time.GetTicksMsec()+30000;
        while(!condition() && Time.GetTicksMsec()<end)
        { if(main?.ViewerPlaybackError is{} error)throw error;await Frame(); }
        Require(condition(),"待ち時間内に進む");
    }
    private async Task Capture(string name)
    {
        string? arg=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--capture-dir="));
        if(arg is null)return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(arg[14..]+"/"+name+".png")==Error.Ok,"撮影");
    }
    private async Task Frame()=>await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private void Require(bool ok,string text){if(!ok)throw new InvalidOperationException(text);_checks++;}
}
