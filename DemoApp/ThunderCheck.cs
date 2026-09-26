using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 実台本で対象数・反転・HP・再実行を検査し、1倍速/2倍速の見た目も保存する。
public partial class ThunderCheck : Control
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--preview"))
            {
                await Preview(); GD.Print("THUNDER_PREVIEW_OK"); GetTree().Quit(); return;
            }
            int hits = 0, arcs = 0, inverse = 0, depth = 0;
            var formation = Formation.Build(front1: UnitCatalog.Beni, front3: UnitCatalog.Rau,
                center: UnitCatalog.Mio, back1: UnitCatalog.Kata, back3: UnitCatalog.Gald);
            for (int stage = 0; stage < 2; stage++)
            {
                var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>(); AddChild(main);
                object? Read(string name) => typeof(Main).GetField(name, Flags)!.GetValue(main);
                typeof(Main).GetField("_fastSmoke", Flags)!.SetValue(main, true);
                typeof(Main).GetField("_speed", Flags)!.SetValue(main, 1000.0);
                typeof(Main).GetMethod("EnterBattle", Flags)!.Invoke(main, new object[] {
                    BattleEngine.Materialize(formation, 0), BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, 1), 0, stage, "" });
                for (int k = 0; k < 500 && (bool)Read("_playing")!; k++) await Wait(0.05);
                Require(!(bool)Read("_playing")!, "再生完走");
                var result = (BattleResult)Read("_result")!;
                var field = (BattlefieldView3D)Read("_battleField")!;
                Require(field.ThunderPlays == result.Events.Count(e => e.Kind == BattleEventKind.Thunder), "雷の実在対象を一度ずつ");
                Require(field.DischargePlays == result.Events.Count(e => e.Kind == BattleEventKind.Discharge), "放電の実在経路を一度ずつ");
                Require(field.ShockInversePlays == result.Events.Count(e => e.Kind == BattleEventKind.Discharge && e.SourceTrait == TraitId.Inverse), "放電反転の実在件数");
                foreach (var group in result.Events.Where(e => e.TargetId is not null && e.Kind is BattleEventKind.Heal or BattleEventKind.Damage).GroupBy(e => e.TargetId))
                    Require(field.FindPawn(group.Key)?.Hp == group.Last().HpAfter, "最終HPは台本に一致");
                hits += field.ThunderPlays; arcs += field.DischargePlays; inverse += field.ShockInversePlays;
                depth = Math.Max(depth, result.Events.Where(e => e.Kind == BattleEventKind.ShockSpent).Select(e => e.Slot).DefaultIfEmpty().Max());
                int prior = field.ThunderPlays;
                typeof(Main).GetMethod("ReplayBattle", Flags)!.Invoke(main, null);
                for (int k = 0; k < 500 && (bool)Read("_playing")!; k++) await Wait(0.05);
                Require(!(bool)Read("_playing")! && prior == field.ThunderPlays, "再生し直し");
                GD.Print($"THUNDER_REPLAY_OK stage={stage} hits={field.ThunderPlays} arcs={field.DischargePlays} inverse={field.ShockInversePlays}");
                main.QueueFree(); await Wait(0.1);
            }
            Require(hits > 0 && arcs > 0 && inverse > 0 && depth > 0, $"陽性対照 hits={hits} arcs={arcs} inverse={inverse} depth={depth}");
            await Preview();
            GD.Print("THUNDER_CHECK_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async Task Preview()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(field);
        field.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        DemoOpening[] openings = [
            new(1,0,"kata","禍導のカタ",2,48,48,6,AttackPattern.Single,false),
            new(2,0,"beni","毒喰らいのベニ",0,64,64,4,AttackPattern.Single,false,UnitCatalog.Beni.Traits),
            new(3,1,"knight","巡礼騎士",0,100,100,10,AttackPattern.Single,false),
            new(4,1,"knight","巡礼騎士",2,100,100,10,AttackPattern.Single,false),
            new(5,1,"knight","巡礼騎士",4,100,100,10,AttackPattern.Single,false) ];
        field.BeginBattle(openings,"",0);
        await Wait(0.5);
        var kata=field.FindPawn(1)!; var a=field.FindPawn(3)!; var b=field.FindPawn(4)!; var c=field.FindPawn(5)!;
        Require(kata.KataAvailableStakes == 3,"独立した杭が3本待機");
        field.LaunchThunderStake(kata,a,1);
        Require(kata.KataAvailableStakes == 2,"発射した杭は待機位置から離れる");
        await Wait(0.85);
        Require(kata.KataAvailableStakes == 3,"発射した同じ杭が帰還");
        a.SetStatusIcon(StatusKeys.Shock,true);
        Require(a.HasShockAura,"感電の持続");
        a.BeginStatusSnapshot(); a.ReadStatusSnapshot("雷",1); a.CommitStatusSnapshot();
        Require(a.HasShockAura,"感電の写し");
        a.BeginStatusSnapshot(); a.CommitStatusSnapshot(); Require(!a.HasShockAura,"写しで解除");
        foreach (double speed in new[] {1.0,2.0})
        {
            field.LaunchThunderStake(kata,a,speed); await Wait(0.18/speed);
            field.StrikeThunder(null,a,1,3,speed); await Capture($"thunder-{speed}");
            await Wait(0.3/speed);
            field.LaunchThunderStake(kata,b,speed); await Wait(0.18/speed);
            field.StrikeThunder(a,b,2,1,speed); await Capture($"hop-{speed}");
            foreach (var p in new[]{a,b,c}) p.SetStatusIcon(StatusKeys.Shock,true);
            await Wait(0.3/speed);
            field.ShockStage(0); field.ShowDischarge(a,b,speed); field.ShowDischarge(a,c,speed);
            await Capture($"chain-{speed}"); await Wait(0.24/speed);
            a.SetStatusIcon(StatusKeys.Shock,false);
            field.ShockStage(1); field.ShowDischarge(b,c,speed); await Wait(0.24/speed);
            b.SetStatusIcon(StatusKeys.Shock,false); c.SetStatusIcon(StatusKeys.Shock,false);
            field.ShowDischarge(kata,field.FindPawn(2),speed,leak:true); await Wait(0.2/speed);
            field.ShowShockInverse(field.FindPawn(2),speed); await Capture($"inverse-{speed}");
        }
        var origin = a.DischargePoint;
        a.SetStatusIcon(StatusKeys.Shock,true); a.AnimateDeath(); Require(!a.HasShockAura,"死亡で解除");
        await Wait(0.25);
        Require(a.DischargePoint.DistanceTo(origin) < 0.001f,"倒れた駒も元の席から放電");
        field.ShowDischarge(a,b,1);
        a.AnimateRevive(); Require(!a.HasShockAura,"蘇生で残らない");
        kata.LaunchKataStake(b.FxPoint,1);
        kata.AnimateDeath(); await Wait(0.1);
        Require(!kata.KataStakesVisible && kata.KataAvailableStakes == 3,"死亡で飛行を解除して杭を隠す");
        kata.AnimateRevive(); await Wait(0.1);
        Require(kata.KataStakesVisible,"蘇生で杭が戻る");
        kata.AnimateVictory(); await Wait(0.1);
        Require(!kata.KataStakesVisible,"勝利絵に切り替える際は独立杭を隠す");
    }
    private async Task Capture(string name)
    {
        await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath($"res://../design/art/kata/{name}-fx-check.png"));
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds),SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool condition,string label) { if(!condition) throw new Exception(label); }
}
