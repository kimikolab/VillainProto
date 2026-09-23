using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 実台本から分配を取り出し、本番の演出口を2倍速で見比べる。
public partial class StoicSupportCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            var formation = Formation.Build(front1: UnitCatalog.Shiga, front3: UnitCatalog.Gan,
                center: UnitCatalog.Gald, back1: UnitCatalog.Kugu, back3: UnitCatalog.Ban);
            var units = BattleEngine.Materialize(formation, 0);
            var foes = BattleEngine.Materialize(EnemyCatalog.Stages[1].Enemy, 1);
            var result = BattleEngine.Run(units, foes, 7, verbose: true);
            int gald = units.Single(u => u.Def.Id == "gald").InstanceId;
            var group = result.Events.Where(e => e.Kind == BattleEventKind.Whet
                    && e.IntendedId == gald && e.TargetId != gald)
                .GroupBy(e => e.SupportSeq).First(g => g.Count() > 1).ToArray();
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            var openings = units.Concat(foes).Select(u => new DemoOpening(u.InstanceId, u.TeamId,
                u.Def.Id, u.Def.Name, u.Slot, u.Def.MaxHp, u.Def.MaxHp,
                u.Def.Attack, u.CurrentPattern, false)).ToArray();
            field.BeginBattle(openings, "", 1);
            await Wait(0.5);
            var relay = field.FindPawn(gald)!;
            var source = field.FindPawn(group[0].ActorId);
            Vector3 home = relay.Position;
            var play = field.ShowStoicSupport(source, relay, group, 2);
            await Wait(0.42);
            await Capture("stoic-split");
            await play;
            if (relay.Position != home || group.Any(e => field.FindPawn(e.TargetId)!.AttackValue != e.AttackAfter))
                throw new InvalidOperationException("分配後の攻撃値・盾の位置が台本と一致しない");
            await Wait(1.8);
            int attack = relay.AttackValue, hp = relay.Hp;
            play = field.ShowStoicSupport(source, relay, Array.Empty<BattleEvent>(), 2);
            await Wait(0.42);
            await Capture("stoic-heal-blocked");
            await play;
            if (relay.AttackValue != attack || relay.Hp != hp)
                throw new InvalidOperationException("回復拒否の表示が駒の数値を変えた");
            GD.Print($"STOIC_SUPPORT_OK recipients={group.Length}");
            await Wait(0.6);
            GetTree().Quit();
        }
        catch (Exception e) { GD.PrintErr(e); GetTree().Quit(1); }
    }

    private async Task Wait(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private async Task Capture(string phase)
    {
        string? directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (directory is null) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (GetViewport().GetTexture().GetImage().SavePng(directory[14..] + "/" + phase + ".png") != Error.Ok)
            throw new InvalidOperationException("画像保存失敗");
    }
}
