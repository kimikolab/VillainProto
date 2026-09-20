using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 開発用。2倍速の不発→解放→正常発動と、複数保持者・死亡・蘇生・再開を確認する。
public partial class SealEffectCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            DemoOpening[] openings =
            [
                new(1, BattleContext.PlayerTeam, "kado", "カド", 0, 100, 100, 20, AttackPattern.Single, false),
                new(2, BattleContext.EnemyTeam, "seal-check", "粛の伝令", 2, 100, 100, 10, AttackPattern.Single, true, [TraitId.Hush]),
                new(3, BattleContext.EnemyTeam, "seal-check", "粛の伝令", 4, 100, 100, 10, AttackPattern.Single, true, [TraitId.Hush]),
            ];
            field.BeginBattle(openings, "演出確認", 1);
            var target = field.FindPawn(1)!;
            var holder = field.FindPawn(2)!;
            var second = field.FindPawn(3)!;
            target.AnimationSpeed = 2;
            await Wait(0.2);
            Task sealedAnimation = field.ShowHushSeal(target);
            await Wait(0.12);
            await Capture("aura");
            await Wait(0.11);
            await Capture("tight");
            await sealedAnimation;
            Require(field.HushChainCount == 2, "両保持者から鎖が繋がる");
            field.MovePawn(target, 3);
            await Wait(0.5);
            await Capture("idle");
            field.SealPawnDied(holder);
            holder.AnimateDeath();
            Require(field.HushChainCount == 1, "一体の死亡では残りの鎖を保つ");
            field.SealPawnDied(second);
            second.AnimateDeath();
            Require(field.HushChainCount == 0, "最後の保持者で解放");
            await Wait(0.18);
            await Capture("break");
            await Wait(0.6);
            Task bonusAnimation = field.ShowBonusAttack(target);
            await Wait(0.08);
            await Capture("bonus-aura");
            await bonusAnimation;
            holder.AnimateRevive();
            field.SealPawnRevived(holder);
            Require(field.HushChainCount == 1, "保持者の蘇生で鎖が戻る");
            field.SealPawnDied(target);
            Require(field.HushChainCount == 0, "対象の死亡で鎖を消す");
            field.SealPawnRevived(target);
            Require(field.HushChainCount == 1, "対象の復帰で鎖が戻る");
            Task interrupted = field.ShowHushSeal(target);
            field.BeginBattle(openings, "演出確認", 1);
            await interrupted;
            Require(field.HushChainCount == 0, "再開で古い鎖と待機を破棄");
            GD.Print("SEAL_EFFECT_CHECK_OK speed=2 holders death revive move restart");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task Capture(string phase)
    {
        string? directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (directory is null) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string path = directory["--capture-dir=".Length..] + "/seal-" + phase + ".png";
        Error result = GetViewport().GetTexture().GetImage().SavePng(path);
        Require(result == Error.Ok, "画像保存 " + result);
    }

    private async Task Wait(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }
}
