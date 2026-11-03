using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>黄・緑の鎖の複数保持者、停止、倍速、死亡・蘇生、再戦を本物の描画部品で確かめる。</summary>
public partial class RuleChainCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
            DemoOpening[] opening = [
                new(1, BattleContext.PlayerTeam, "gald", "ガルド", 0, 100, 100, 20, AttackPattern.Single, false),
                new(2, BattleContext.EnemyTeam, "yoker", "軛の重装兵", 0, 100, 100, 20, AttackPattern.Single, true, [TraitId.Yoke]),
                new(3, BattleContext.EnemyTeam, "droughter", "渇きの祭司", 2, 100, 100, 20, AttackPattern.Single, true, [TraitId.Drought]),
                new(4, BattleContext.EnemyTeam, "yoker", "軛の重装兵", 4, 100, 100, 20, AttackPattern.Single, true, [TraitId.Yoke])];
            field.BeginBattle(opening, "封じの鎖", 1); await Wait(.2);
            var target = field.FindPawn(1)!;
            Require(field.RuleChainCount == 0, "封じが起きるまでは鎖を予測しない");
            field.SetDeathClock(false, 2);
            field.ShowYokeSeal(target, 80); field.ShowDroughtSeal(target, 30);
            Require(field.RuleChainCount == 3, "黄2本・緑1本");
            await Wait(.12); await Capture("01-tight");
            HushChain3D chain = Descendants(field).OfType<HushChain3D>().First();
            field.SetDeathClock(true, 2); float age = chain.Elapsed;
            await Wait(.15); Require(chain.Elapsed == age, "停止中は鎖も停止");
            field.SetDeathClock(false, 2); await Wait(.12);
            Require(chain.Elapsed > age + .15f, "再開後は倍速");
            field.MovePawn(target, 3); await Wait(.3); await Capture("02-moved");
            field.SealPawnDied(field.FindPawn(2)); Require(field.RuleChainCount == 2, "一体の死亡では他の保持者を残す");
            field.SealPawnDied(field.FindPawn(4)); Require(field.RuleChainCount == 1, "黄の最後の保持者の死亡で黄だけ解放");
            field.SealPawnDied(field.FindPawn(3)); Require(field.RuleChainCount == 0, "緑も保持者の死亡で解放");
            await Wait(.08); await Capture("03-shatter");
            field.SealPawnRevived(field.FindPawn(3)!); Require(field.RuleChainCount == 1, "保持者の蘇生");
            field.SealPawnDied(target); Require(field.RuleChainCount == 0, "対象の死亡");
            field.SealPawnRevived(target); Require(field.RuleChainCount == 1, "対象の蘇生");
            field.BeginBattle(opening, "再戦", 1); Require(field.RuleChainCount == 0, "再戦の掃除");
            await Wait(.15);
            Require(!Descendants(field).OfType<HushChain3D>().Any(), "砕けた鎖も前の戦から持ち越さない");
            GD.Print("RULE_CHAIN_CHECK_OK speed=2 pause move holders death revive restart"); GetTree().Quit();
        }
        catch (Exception e) { GD.PrintErr(e); GetTree().Quit(1); }
    }
    private static System.Collections.Generic.IEnumerable<Node> Descendants(Node n)
    {
        foreach (Node child in n.GetChildren()) { yield return child; foreach (var d in Descendants(child)) yield return d; }
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); GD.Print("RULE_CHAIN_OK " + message); }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        string? directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="))?["--capture-dir=".Length..];
        if (directory is null) return;
        System.IO.Directory.CreateDirectory(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok, name);
    }
}
