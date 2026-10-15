using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// Godot_console.exe --path DemoApp res://GolmCheck.tscn [--headless -- --verify]
// --capture-dir=<絶対パス> は各段の画面を保存し、全ケースを一巡して終了する。
public partial class GolmCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    private BattlefieldView3D _field = null!;
    private Label _phase = null!;
    private bool _paused;
    private double _speed = 1;
    private int _generation, _scenario;
    private static readonly string[] Scenarios = { "精気を呑む・吐く", "殴る→捕獲→呑む", "殴らず捕獲", "後列へパンチ・帰還", "パンチで撃破", "踏み潰し" };

    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--verify-throw"))
            { await VerifyThrowAndImpact(); GD.Print("GOLM_COLORED_BODY_CHECK_OK"); await Quit(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--verify-revision"))
            { await VerifyClock(); await VerifyThrowAndImpact(); await VerifyReplays(); GD.Print("GOLM_REVISION_CHECK_OK"); await Quit(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--verify-status"))
            { await VerifyClock(); await VerifySpitStatus(); GD.Print("GOLM_STATUS_CHECK_OK"); await Quit(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--verify"))
            { await VerifyClock(); await VerifySpitStatus(); await VerifyReplays(); GD.Print("GOLM_CHECK_OK"); await Quit(); return; }
            BuildPreview();
            if (OS.GetCmdlineUserArgs().Any(a => a.StartsWith("--capture-dir=")))
            {
                for (int team = 0; team < 2; team++)
                for (int scenario = 0; scenario < Scenarios.Length; scenario++) await ShowScenario(scenario, team);
                GD.Print("GOLM_CAPTURE_OK"); await Quit();
            }
            else await ShowScenario(0, 0);
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private void BuildPreview()
    {
        _field = new BattlefieldView3D { AnchorRight = 1, AnchorBottom = 1, OffsetTop = 80 }; AddChild(_field);
        var panel = new VBoxContainer { Position = new Vector2(16,8) }; AddChild(panel);
        var controls = new HBoxContainer(); panel.AddChild(controls);
        var picker = new OptionButton(); foreach (string name in Scenarios) picker.AddItem(name); controls.AddChild(picker);
        picker.ItemSelected += async i => { _scenario = (int)i; await ShowScenario(_scenario, 0); };
        var replay = new Button { Text = "もう一度" }; controls.AddChild(replay);
        replay.Pressed += async () => await ShowScenario(_scenario,0);
        var pause = new Button { Text = "一時停止" }; controls.AddChild(pause);
        pause.Pressed += () => { _paused = !_paused; _field.SetGolmClock(_paused,_speed); pause.Text = _paused ? "再開" : "一時停止"; };
        var speed = new OptionButton(); foreach (string text in new[] { "×0.5", "×1", "×2", "×4" }) speed.AddItem(text); speed.Selected=1; controls.AddChild(speed);
        speed.ItemSelected += i => { _speed = new[] { .5,1,2,4 }[i]; _field.SetGolmClock(_paused,_speed); };
        _phase = UiKit.Text("",18,GolmFx3D.Soul); panel.AddChild(_phase);
    }

    private DemoOpening[] Openings(int team) => new DemoOpening[] {
        new(1,team,"golm","大喰らいゴルム",0,300,300,45,AttackPattern.Single,false),
        new(2,1-team,EnemyCatalog.Knight.Id,"弱った敵",3,25,100,10,AttackPattern.Single,false),
        new(3,1-team,EnemyCatalog.Knight.Id,"状態異常を浴びる大物",1,500,500,10,AttackPattern.Single,false),
        new(4,1-team,EnemyCatalog.Knight.Id,"前列の敵",0,100,100,10,AttackPattern.Single,false),
    };

    private async Task ShowScenario(int scenario, int team)
    {
        int token=++_generation;
        _field.BeginBattle(Openings(team),"ゴルムの演出確認",0); _field.SetGolmClock(_paused,_speed);
        var actor=_field.FindPawn(1)!; var target=_field.FindPawn(2)!; var top=_field.FindPawn(3)!;
        _field.SetTurnOwner(actor,Scenarios[scenario],GolmFx3D.Soul);
        var voice = _field.ShowGolmCue(actor,target,new BattleEvent { Turn=1,Kind=BattleEventKind.GolmArm,Text=GolmArmLabels.Stomp });
        await voice;
        var fx=_field.GolmEffects.Single(); fx.PhaseChanged = text => _phase.Text=Scenarios[scenario]+"　／　"+text;
        await Wait(.4);
        if(token!=_generation) return;
        if(scenario is 1 or 3 or 4)
        {
            var launch=fx.Launch(target); await Wait(.035/_speed); await Capture($"{team}-{scenario}-ready");
            await Wait(.16/_speed); await Capture($"{team}-{scenario}-launch");
            if(!await launch || token!=_generation) return;
            var impact=fx.Impact(50,captureFollows:scenario==1);
            await Wait(.025/_speed); await Capture($"{team}-{scenario}-contact");
            if(!await impact || token!=_generation) return;
            target.SetHp(scenario==4?0:25);
            await Capture($"{team}-{scenario}-hit");
        }
        if(scenario is 1 or 2)
        {
            var grab=fx.Grab(target,scenario==1); await Wait((scenario==1?.25:.58)/_speed);
            await Capture($"{team}-{scenario}-carry"); if(!await grab || token!=_generation) return;
        }
        if(scenario<3)
        {
            int hp=target.Hp;
            var swallow=fx.Swallow(target); await Wait(.22/_speed); await Capture($"{team}-{scenario}-soul");
            Require(target.Hp==hp,"吸気の途中ではHPバーを減らさない");
            await Wait((scenario==0?.30:.38)/_speed);
            await Capture($"{team}-{scenario}-"+(scenario==0?"shell":"toss"));
            if(!await swallow || token!=_generation) return;
            _field.PlayDeath(target);
            var spit=fx.Spit(top,GolmPresentation.ParseSpit("吐く:毒9,火3,雷1,濃2,標4"));
            await Wait(.25/_speed); await Capture($"{team}-{scenario}-spit"); if(!await spit || token!=_generation) return;
        }
        else if(scenario is 3 or 4)
        {
            if(scenario==4) _field.PlayDeath(target);
            if(!await fx.Return() || token!=_generation) return;
        }
        else
        {
            var stomp=fx.Stomp(target); await Wait(.17/_speed); await Capture($"{team}-{scenario}-stomp");
            if(!await stomp || token!=_generation) return;
        }
        _phase.Text=Scenarios[scenario]+"　／　完了"; await Capture($"{team}-{scenario}-end");
    }

    private async Task VerifyClock()
    {
        Require(GolmPresentation.ParseSpit("吐く:毒9,火0,雷1,濃-1,標2,未知8").Count==3,"吐く内訳は既知の正の量だけ");
        Require(GolmFx3D.SpitSize(16)>GolmFx3D.SpitSize(1),"量に応じて吐息を太くする");
        _field=new BattlefieldView3D(); _field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(_field);
        _field.BeginBattle(Openings(0),"時計の検証",0);
        var actor=_field.FindPawn(1)!; var target=_field.FindPawn(2)!;
        // ストップの見出しで所有者の演出ノードだけを用意する。
        await _field.ShowGolmCue(actor,target,new BattleEvent { Turn=1,Kind=BattleEventKind.GolmArm,Text=GolmArmLabels.Stomp });
        var fx=_field.GolmEffects.Single(); var launch=fx.Launch(target);
        Require(actor.GolmRocketPose,"発射前から右向きの攻撃差分を表示");
        await Wait(.06); _field.SetGolmClock(true,1); double elapsed=fx.Elapsed;
        await Wait(.13); Require(fx.Elapsed==elapsed && !launch.IsCompleted,"一時停止で絵と待ち手が止まる");
        _field.SetGolmClock(false,4); Require(await launch,"途中から4倍速で飛行を完了");
        var capture=fx.Grab(target,true); await Wait(.03); _field.EndGolmPresentation();
        Require(!await capture,"中断時は待ち手へキャンセルを返す");
        Require(!target.GolmUsesProxy && target.Hp==25,"中断で捕獲の代理絵を片付け、HPを保つ");
        Require(!actor.GolmRocketPose,"中断で待機差分へ戻る");
        await _field.ShowGolmCue(actor,target,new BattleEvent { Turn=1,Kind=BattleEventKind.GolmArm,Text=GolmArmLabels.Stomp });
        fx=_field.GolmEffects.Single(); var interrupted=fx.Grab(target,false);
        await Wait(.03); _field.PlayDeath(actor);
        Require(!await interrupted && !_field.GolmEffects.Any() && !target.GolmUsesProxy,"腕の所有者の死亡でも中断を完了");
        Require(!actor.GolmRocketPose,"所有者の死亡でも攻撃差分を解除");
        _field.BeginBattle(Openings(1),"再戦",0); Require(!_field.GolmEffects.Any(),"再戦へ腕を持ち越さない");
        actor=_field.FindPawn(1)!; target=_field.FindPawn(2)!;
        await _field.ShowGolmCue(actor,target,new BattleEvent { Turn=1,Kind=BattleEventKind.Swallow,Text=SwallowLabels.Swallow });
        Require(target.Hp==25 && target.GolmConsumed,"Damageなしの吸気完了までHPを保つ");
        _field.PlayDeath(target);
        Require(target.Hud.Modulate.A==0,"呑む死亡ではHPバーを削らずその場で消す");
        target.SetHp(10); target.AnimateRevive();
        Require(!target.GolmConsumed && !target.GolmUsesProxy,"蘇生で抜け殻の表示を解除");
        _field.QueueFree(); await Wait(.05);
        GD.Print("GOLM_CLOCK_OK pause=true speedChange=true cancel=true replay=true");
    }

    private async Task VerifyReplays()
    {
        bool revision=OS.GetCmdlineUserArgs().Contains("--verify-revision");
        int swallows=0, launches=0, grabs=0, spits=0, stomps=0, direct=0, after=0;
        var rows=Presets.Playtest.Concat(Presets.Versions).Where(p=>p.Name is "試遊・標 守り型" or "版・RP-a 守り型" or "版・RP-b 守り型" or "版・RP-b 毒" or "版・GA-a 守り型")
            .Concat(new[] { (Name:"検証・規定の呑む",F:Formation.Build(front1:UnitCatalog.Golm)),
                (Name:"検証・直接捕獲",F:Formation.Build(front1:UnitCatalog.GolmRPb)),
                (Name:"検証・殴って捕獲",F:Formation.Build(front1:UnitCatalog.GolmRPb)),
                (Name:"検証・踏み潰し",F:Formation.Build(front1:UnitCatalog.GolmGAa)) }).ToArray();
        // 規定化で旧「版・」の台が外れても、本編の守り型と各分岐を再生する。
        if(revision) rows=rows.Where(p=>p.Name is "試遊・標 守り型" or "版・RP-a 守り型" or "版・RP-b 守り型" or "版・RP-b 毒"
            || p.Name.StartsWith("検証・") && p.Name!="検証・踏み潰し").ToArray();
        foreach(var preset in rows)
        foreach(int wave in preset.Name.StartsWith("検証・") ? new[]{1} : revision ? new[]{1,4} : new[]{1,2,3,4})
        {
            var main=GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>(); AddChild(main);
            object? Read(string n)=>typeof(Main).GetField(n,Flags)!.GetValue(main);
            typeof(Main).GetField("_fastSmoke",Flags)!.SetValue(main,true); typeof(Main).GetField("_speed",Flags)!.SetValue(main,1000.0);
            var players=BattleEngine.Materialize(preset.F,0);
            var enemies=wave==1 ? BattleEngine.Materialize(EnemyCatalog.Stages[1].Enemy,1)
                : BattleEngine.MaterializeEnemy(EnemyCatalog.PlaytestStages[wave-2].Enemy,EnemyCatalog.PlaytestStages[wave-2].Scale);
            if (preset.Name.StartsWith("検証・"))
            {
                var dummy=new UnitDef { Id=EnemyCatalog.Knight.Id, Name="台本確認の敵", MaxHp=100, Attack=0, Speed=0, Traits=Array.Empty<TraitId>() };
                enemies=BattleEngine.Materialize(Formation.Build(front1:dummy,back3:dummy.WithStats(500,0)),1);
                players[0].SetCounter(ColossusTrait.BellyKey,200);
                enemies[0].Hp=preset.Name=="検証・殴って捕獲" ? 45 : 20;
                enemies[0].SetCounter(StatusKeys.Poison,3); enemies[0].SetCounter(StatusKeys.Marked,2);
            }
            typeof(Main).GetMethod("EnterBattle",Flags)!.Invoke(main,new object[]{players,enemies,0,1,preset.Name});
            for(int pass=0;pass<2;pass++)
            {
                for(int i=0;i<1800 && (bool)Read("_playing")!;i++) await Wait(.015);
                Require(!(bool)Read("_playing")!,"台本を完走");
                var r=(BattleResult)Read("_result")!; var f=(BattlefieldView3D)Read("_battleField")!;
                int Count(BattleEventKind kind,string label)=>r.Events.Count(e=>e.Kind==kind && e.Text==label);
                Require(f.GolmSwallows==Count(BattleEventKind.Swallow,SwallowLabels.Swallow),"呑むは台本と同じ件数");
                Require(f.GolmLaunches==Count(BattleEventKind.GolmArm,GolmArmLabels.Launch),"飛ばすは台本と同じ件数");
                Require(f.GolmGrabs==Count(BattleEventKind.GolmArm,GolmArmLabels.Grab),"掴むは台本と同じ件数");
                Require(f.GolmStomps==Count(BattleEventKind.GolmArm,GolmArmLabels.Stomp),"踏むは台本と同じ件数");
                Require(f.GolmHits==f.GolmLaunches,"ロケットパンチの通常斬撃との二重再生なし");
                Require(f.GolmSpits==r.Events.Count(e=>e.Kind==BattleEventKind.Swallow && e.Text!.StartsWith("吐く:")),"吐くは台本と同じ件数");
                var plan=GolmPresentation.Build(r.Events);
                Require(plan.CapturingAttacks.Count==r.Events.Count(e=>e.Kind==BattleEventKind.GolmArm && e.Text==GolmArmLabels.Grab && e.Slot==1),"捕獲へ続く一撃を台本で見分ける");
                Require(plan.ConsumedDeaths.Count==f.GolmSwallows,"Damageなしの消滅を呑むと一対一に対応");
                Require(f.GolmReturns==Count(BattleEventKind.GolmArm,GolmArmLabels.Return)+plan.ReturnAfterDeath.Count,"撃破で戻る通知がない手も帰還");
                foreach(var pawn in f.Pawns.Values)
                {
                    var final=r.Events.LastOrDefault(e=>e.TargetId==pawn.InstanceId && e.Kind is BattleEventKind.Death or BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Revive);
                    if(final is not null) Require(pawn.Hp==(final.Kind==BattleEventKind.Death?0:Math.Clamp(final.HpAfter,0,pawn.MaxHp)),"終端HPは台本に一致");
                }
                swallows+=f.GolmSwallows; launches+=f.GolmLaunches; grabs+=f.GolmGrabs; spits+=f.GolmSpits; stomps+=f.GolmStomps;
                direct+=r.Events.Count(e=>e.Kind==BattleEventKind.GolmArm && e.Text==GolmArmLabels.Grab && e.Slot==0);
                after+=r.Events.Count(e=>e.Kind==BattleEventKind.GolmArm && e.Text==GolmArmLabels.Grab && e.Slot==1);
                GD.Print($"GOLM_REPLAY_OK preset={preset.Name} wave={wave} pass={pass} swallow={f.GolmSwallows} launch={f.GolmLaunches} grab={f.GolmGrabs} spit={f.GolmSpits} stomp={f.GolmStomps}");
                if(pass==0) typeof(Main).GetMethod("ReplayBattle",Flags)!.Invoke(main,null);
            }
            main.QueueFree(); await Wait(.05);
        }
        Require(swallows>0 && launches>0 && grabs>0 && spits>0 && (revision || stomps>0) && direct>0 && after>0,"各演出と両捕獲経路の陽性対照");
        GD.Print($"GOLM_COVERAGE swallow={swallows} launch={launches} grab={grabs} spit={spits} stomp={stomps} direct={direct} after={after}");
    }
    private async Task VerifySpitStatus()
    {
        var main=GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>(); AddChild(main);
        var field=(BattlefieldView3D)typeof(Main).GetField("_battleField",Flags)!.GetValue(main)!;
        field.Show(); field.BeginBattle(Openings(0),"吐く状態表示の検証",0);
        typeof(Main).GetField("_battleMode",Flags)!.SetValue(main,true);
        typeof(Main).GetField("_speed",Flags)!.SetValue(main,100.0);
        var target=field.FindPawn(3)!; var actor=field.FindPawn(1)!;
        var e=new BattleEvent { Turn=1,Kind=BattleEventKind.Swallow,ActorId=1,TargetId=3,Text="吐く:毒3,火2,雷1,濃0,標2",Amount=8 };
        Require(await (Task<bool>)typeof(Main).GetMethod("PlayGolm",Flags)!.Invoke(main,new object[]{e,actor,target})!,"吐く通知を再生");
        Require(target.PoisonIconAmount==3 && target.MarkLayers==2 && target.HasStatusIcon(StatusKeys.Burn)
            && target.HasStatusIcon(StatusKeys.Shock),"到着時に毒・火・雷・標を表示");
        Require(target.Hp==500,"吐くではHPを減らさない");
        main.QueueFree(); await Wait(.05); GD.Print("GOLM_SPIT_STATUS_OK poison=3 mark=2 burn=true shock=true hp=500");
    }
    private async Task VerifyThrowAndImpact()
    {
        _field=new BattlefieldView3D(); _field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(_field);
        foreach(int team in new[]{0,1})
        foreach(bool afterHit in new[]{false,true})
        {
            _field.BeginBattle(Openings(team),"投げ返しの検証",0); _field.SetGolmClock(false,4);
            var actor=_field.FindPawn(1)!; var target=_field.FindPawn(2)!;
            var actorSprite=(Sprite3D)typeof(BattlePawn3D).GetField("_sprite",Flags)!.GetValue(actor)!;
            var idleTexture=actorSprite.Texture;
            await _field.ShowGolmCue(actor,target,new BattleEvent { Turn=1,Kind=BattleEventKind.GolmArm,Text=GolmArmLabels.Stomp });
            var fx=_field.GolmEffects.Single();
            if(afterHit) { Require(await fx.Launch(target),"捕獲前の飛行"); Require(await fx.Impact(50,captureFollows:true),"捕獲前の一撃"); }
            Require(await fx.Grab(target,afterHit),"敵を持ち帰る");
            Require(actor.GolmRocketPose && actorSprite.Texture!=idleTexture && actorSprite.FlipH==(team==1),"左右両陣営で相手に向けた発射差分を維持");
            var tossing=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fx.PhaseChanged=phase=>{ if(phase=="ぽいっと投げ返す") tossing.TrySetResult(); };
            var swallow=fx.Swallow(target); await tossing.Task; await Wait(.015);
            _field.SetGolmClock(true,4); Vector3? held=fx.CaptivePoint; double elapsed=fx.Elapsed;
            await Wait(.10); Require(fx.CaptivePoint==held && fx.Elapsed==elapsed && !swallow.IsCompleted,"投げ返しの途中でも停止する");
            _field.SetGolmClock(false,4); Require(await swallow,"投げ返して帰還");
            Require(!actor.GolmRocketPose && actorSprite.Texture==idleTexture,"帰還後に待機差分へ戻る");
            Require(target.GolmDiscarded && !fx.HasCaptive && !fx.HandOut && target.Hp==25 && target.Slot==3,"HPと席を変えず、元の席に敵の姿を残す");
            var sprite=(Sprite3D)typeof(BattlePawn3D).GetField("_sprite",Flags)!.GetValue(target)!;
            Require(sprite.Visible && sprite.GlobalPosition.DistanceTo(target.GolmDiscardPoint((Camera3D)typeof(BattlefieldView3D).GetField("_camera",Flags)!.GetValue(_field)!))<.01f,"元いた場所でへたり込む");
            _field.PlayDeath(target); _field.EndGolmPresentation(); await Wait(.80);
            Require(target.Hp==0 && sprite.Visible && target.GolmDiscarded,"死亡通知と演出ノードの後片付けでも体を消さない");
            var material=(ShaderMaterial)sprite.MaterialOverride;
            Require(material.GetShaderParameter("golm_gray").AsDouble()==0 && material.GetShaderParameter("golm_crumble").AsDouble()==0
                && sprite.Modulate.A==1,"時間が経っても元の色と不透明な体を保つ");
            target.SetHp(10); target.AnimateRevive(); Require(!target.GolmDiscarded,"蘇生時は投げ捨てられた姿を解除");
        }
        _field.BeginBattle(Openings(0),"着弾の検証",0); _field.SetGolmClock(false,1);
        var owner=_field.FindPawn(1)!; var enemy=_field.FindPawn(2)!;
        await _field.ShowGolmCue(owner,enemy,new BattleEvent { Turn=1,Kind=BattleEventKind.GolmArm,Text=GolmArmLabels.Stomp });
        var punch=_field.GolmEffects.Single(); await punch.Launch(enemy);
        var strike=punch.Impact(50); await Wait(.015);
        var burst=punch.PunchImpacts.Single(); Require(burst.Heavy,"攻撃だけのパンチに強い着弾を使う");
        _field.SetGolmClock(true,1); double hitTime=burst.Elapsed; await Wait(.10);
        Require(burst.Elapsed==hitTime && !strike.IsCompleted,"着弾の閃光・衝撃波も停止する");
        _field.SetGolmClock(false,2); Require(await strike,"着弾中の速度変更");
        _field.EndGolmPresentation(); _field.QueueFree(); await Wait(.05);
        GD.Print("GOLM_THROW_IMPACT_OK teams=2 grabRoutes=2 bodyRemains=true pause=true impact=true");
    }
    private async Task Capture(string name)
    {
        string? arg=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--capture-dir=")); if(arg is null) return;
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(arg[14..]+"/"+name+".png")==Error.Ok,"画像保存");
    }
    private async Task Wait(double seconds)=>await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private async Task Quit()
    {
        foreach(var tween in GetTree().GetProcessedTweens()) tween.Kill();
        foreach(Node child in GetChildren()) child.QueueFree();
        await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
        GC.Collect(); GC.WaitForPendingFinalizers(); await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame); GetTree().Quit();
    }
    private static void Require(bool ok,string message) { if(!ok) throw new InvalidOperationException(message); }
}
