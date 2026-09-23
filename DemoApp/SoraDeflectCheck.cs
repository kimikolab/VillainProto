using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

// 2倍速の左右の見え方と、実台本から通常再生への逸らしの漏れ・重複を検査する。
public partial class SoraDeflectCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            if (OS.GetCmdlineUserArgs().Contains("--replay")) await CheckReplay();
            else if (OS.GetCmdlineUserArgs().Contains("--thrust")) await PreviewThrust();
            else await Preview();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Wait(0.3);
            GD.Print("SORA_DEFLECT_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async Task Preview()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        foreach (int team in new[] { 0, 1 })
        {
            field.BeginBattle(new DemoOpening[] {
                new(1, team, "sora", "逸らしのソラ", 2, 100, 100, 10, AttackPattern.Pierce, true),
                new(2, 1 - team, "gald", "殴ってきた敵", 0, 100, 100, 10, AttackPattern.Single, true),
                new(3, 1 - team, "borg", "逸らし先の敵", 4, 100, 100, 10, AttackPattern.Sweep, true),
            }, "", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = 2;
            await Wait(0.6);
            var sora = field.FindPawn(1)!;
            var attacker = field.FindPawn(2)!;
            var target = field.FindPawn(3)!;
            Vector3 home = sora.RestPosition;
            await field.Attack(attacker, sora, AttackPattern.Single, new[] { sora });
            var flight = field.ShowDeflection(attacker, sora, target, 2);
            await Wait(0.12);
            await Capture($"sora-{team}-cape");
            await Wait(0.12);
            await Capture($"sora-{team}-flight");
            await flight;
            Require(sora.Hp == 100 && target.Hp == 100 && attacker.Hp == 100, "演出がHPを変えない");
            Require(sora.Position.DistanceTo(home) < 0.01f, "横へ逃がした駒は元の席へ戻る");
            target.SetHp(85);
            target.AnimateHit();
            field.DamagePopup(target, 15, "", new Color("ffe3a0"), false, false);
            sora.SetHp(90);
            sora.AnimateHit();
            field.DamagePopup(sora, 10, "", UiKit.Hurt, false, false);
            await Capture($"sora-{team}-impact");
            await Wait(0.7);
            Require(field.DeflectionPlays == 1, "1回だけ演出");
        }
    }

    private async Task CheckReplay()
    {
        var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate<Main>();
        AddChild(main);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object? Read(string name) => typeof(Main).GetField(name, flags)!.GetValue(main);
        var formation = Presets.Compare.First(x => x.Name == "逸らし (ソラ×カド)").F;
        typeof(Main).GetMethod("EnterBattle", flags)!.Invoke(main, new object[] {
            BattleEngine.Materialize(formation, 0), BattleEngine.Materialize(EnemyCatalog.Stages[2].Enemy, 1),
            0, 2, "" });
        for (int i = 0; i < 180 && (bool)Read("_playing")!; i++) await Wait(0.5);
        Require(!(bool)Read("_playing")!, "通常再生が完走");
        var result = (BattleResult)Read("_result")!;
        var field = (BattlefieldView3D)Read("_battleField")!;
        var deflections = result.Events.Where(e => e.Kind == BattleEventKind.Damage && e.DeflectFromId is not null).ToArray();
        Require(deflections.Length > 0 && field.DeflectionPlays == deflections.Length, "逸らしを漏れなく1回ずつ描く");
        Require(deflections.All(e => e.ActorId != e.TargetId), "殴った本人へ返さない台本");
        int thrusts = result.Events.Count(e => e.Kind == BattleEventKind.Attack && e.ThrustCharge is not null);
        Require(result.Events.Any(e => e.Kind == BattleEventKind.Attack && e.ThrustCharge > 0), "溜めありの実戦台本");
        Require(thrusts > 0 && field.ThrustPlays == thrusts, "突きを漏れなく1回ずつ描く");
        Require(field.Pawns.Values.All(p => p.ThrustCharge == 0), "終了後に光を残さない");
        foreach (var pawn in field.Pawns.Values)
        {
            var last = result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId
                && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal);
            if (last is not null) Require(pawn.Hp == Math.Clamp(last.HpAfter, 0, pawn.MaxHp), "最終HPが台本と一致");
        }
        GD.Print($"SORA_REPLAY_OK expected={deflections.Length} played={field.DeflectionPlays} thrusts={thrusts}/{field.ThrustPlays}");
        main.QueueFree();
        await Wait(1);
    }

    private async Task PreviewThrust()
    {
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        foreach (int team in new[] { 0, 1 })
        {
            field.BeginBattle(new DemoOpening[] {
                new(1, team, "sora", "逸らしのソラ", 0, 100, 100, 10, AttackPattern.Pierce, true),
                new(2, 1 - team, "gald", "前列", 0, 100, 100, 10, AttackPattern.Single, true),
                new(3, 1 - team, "borg", "中央・標", 2, 100, 100, 10, AttackPattern.Sweep, true),
                new(4, 1 - team, "utsu", "後列", 3, 100, 100, 10, AttackPattern.Single, true),
            }, "", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = 2;
            await Wait(0.6);
            var sora = field.FindPawn(1)!;
            var targets = new[] { field.FindPawn(2)!, field.FindPawn(3)!, field.FindPawn(4)! };
            targets[1].SetStatusIcon(StatusKeys.Marked, true);
            for (int charge = 0; charge <= 4; charge++)
            {
                sora.SetThrustCharge(charge);
                await Wait(0.12);
                await Capture($"sora-{team}-charge-{charge}");
                Require(sora.ThrustCharge == charge, "台本の累計を保持");
            }
            // 同じ台本値を読み直しても加算しない。
            sora.SetThrustCharge(4);
            Require(sora.ThrustCharge == 4, "累計を二重に積まない");
            foreach (int charge in new[] { 0, 1, 2, 3, 4 })
            {
                sora.SetThrustCharge(charge);
                var order = new System.Collections.Generic.List<int>();
                var thrust = field.Attack(sora, targets[0], AttackPattern.Pierce, targets,
                    advance: false, thrustCharge: charge, thrustImpact: pawn => {
                        order.Add(pawn.InstanceId);
                        pawn.AnimateHit();
                    });
                await Wait(0.18);
                await Capture($"sora-{team}-thrust-{charge}-front");
                await Wait(0.10);
                await Capture($"sora-{team}-thrust-{charge}-back");
                await thrust;
                await Wait(0.25);
                Require(order.SequenceEqual(new[] { 2, 3, 4 }), "前・中央・後の順に各1回着弾");
                Require(sora.ThrustCharge == 0, "突きで光を消費");
                Require(field.Pawns.Values.All(p => p.Position.DistanceTo(p.Home) < 0.01f), "表示位置が全員の席へ戻る");
                Require(field.Pawns.Values.All(p => p.Hp == 100), "演出はダメージを計算しない");
            }
            sora.SetThrustCharge(3);
            Require(field.ThrustMarkedHits == 5, "標持ちだけ各突きで大きく弾ける");
            sora.AnimateDeath();
            Require(sora.ThrustCharge == 0, "死亡で光を除去");
            sora.AnimateRevive();
            Require(sora.ThrustCharge == 0, "蘇生で古い光が戻らない");
            await Wait(0.6);
            sora.SetThrustCharge(2);
            field.ShowVictoryPortraits();
            Require(sora.ThrustCharge == 0, "戦闘終了で光を除去");
        }
        GD.Print("SORA_THRUST_CHECK_OK teams=2 charge=0..4 ordered_hits=30");
    }

    private async Task Capture(string name)
    {
        string? arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null || DisplayServer.GetName() == "headless") return;
        string directory = arg[14..];
        DirAccess.MakeDirRecursiveAbsolute(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok,
            "画像保存");
    }

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }

    private async Task Wait(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
}
