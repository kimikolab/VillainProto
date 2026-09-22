using BattleCore;
using Godot;
using System;
using System.Linq;

// ススの実際の再生経路で、貯める→放つ→通常と透過画像を検証する開発用シーン。
public partial class SusuPortraitCheck : Node
{
    public override async void _Ready()
    {
        try
        {
            var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate();
            AddChild(main);
            main.Call("ClearFormation");
            string[] keys = { "borg", "gald", "susu", "nono", "mudo" };
            for (int i = 0; i < keys.Length; i++) main.Call("DropUnit", i, keys[i]);
            main.Call("SetSpeed", 3);
            main.Call("StartBattle");
            var pawn = FindChildren("*", "", true, false).OfType<BattlePawn3D>().Single(p => p.UnitId == "susu");
            var sprite = pawn.GetChildren().OfType<Sprite3D>().Single();
            var atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");
            bool charged = false, released = false, returned = false;
            for (int i = 0; i < 1200 && !returned; i++)
            {
                await ToSignal(GetTree().CreateTimer(0.025), SceneTreeTimer.SignalName.Timeout);
                if (!charged && pawn.IsCharging)
                {
                    Require(sprite.Texture == UiKit.BattlePortrait(atlas, "susu_charging"), "貯める画像");
                    Require(pawn.GetChildren().OfType<ChargeAura3D>().Any(a => a.Visible), "チャージ演出");
                    pawn.SetBurning(true);
                    Require(sprite.Texture == UiKit.BattlePortrait(atlas, "susu_charging"), "燃焼通知で貯める絵を消さない");
                    pawn.SetBurning(false);
                    charged = true;
                    await Capture("charging");
                }
                if (charged && !released && pawn.IsAshReleasing)
                {
                    Require(!pawn.IsCharging, "放出時にチャージ終了");
                    Require(sprite.Texture == UiKit.BattlePortrait(atlas, "susu_release"), "放つ画像");
                    Require(sprite.PixelSize * sprite.Texture.GetHeight() > UiKit.PortraitWorldHeight("susu"), "灰の余白ぶん本体を縮めない");
                    released = true;
                    await Capture("release");
                }
                if (released && !pawn.IsAshReleasing && !pawn.IsCharging)
                {
                    Require(sprite.Texture == UiKit.BattlePortrait(atlas, "susu"), "攻撃後に通常画像");
                    returned = true;
                    await Capture("returned");
                }
            }
            Require(charged && released && returned, "実際の台本で全状態を通過");
            pawn.BeginCharge(100);
            pawn.AnimateDeath();
            Require(!pawn.IsCharging && !pawn.IsAshReleasing, "死亡で解除");
            pawn.AnimateRevive();
            Require(sprite.Texture == UiKit.BattlePortrait(atlas, "susu"), "復帰時は通常");
            pawn.BeginCharge(100);
            pawn.ReleaseCharge();
            Require(sprite.Texture == UiKit.BattlePortrait(atlas, "susu"), "灰なし通常攻撃で解除");
            pawn.BeginAshRelease();
            pawn.AnimateVictory();
            pawn.EndAshRelease();
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            Require(sprite.Texture == UiKit.Portrait(atlas, "susu"), "勝利絵を遅延通知で上書きしない");
            GD.Print("SUSU_PORTRAIT_CHECK_OK charge aura release idle burn death revive dry victory");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            GetTree().Quit(1);
        }
    }

    private async System.Threading.Tasks.Task Capture(string state)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(
            ProjectSettings.GlobalizePath($"res://../design/art/susu/action-{state}-game-check.png"));
        Require(error == Error.Ok, "画面保存");
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }
}
