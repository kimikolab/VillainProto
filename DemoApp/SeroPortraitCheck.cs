using Godot;
using System;
using System.Linq;

// セロの実際の攻撃再生と、遅延した待機復帰が死亡・勝利を壊さないことを検証する。
public partial class SeroPortraitCheck : Node
{
    public override async void _Ready()
    {
        try
        {
            var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate();
            AddChild(main);
            main.Call("ClearFormation");
            string[] keys = { "sero", "yomi", "basa", "shio", "hane" };
            for (int i = 0; i < keys.Length; i++) main.Call("DropUnit", i, keys[i]);
            main.Call("SetSpeed", 1);
            main.Call("StartBattle");
            var pawn = FindChildren("*", "", true, false).OfType<BattlePawn3D>().Single(p => p.UnitId == "sero");
            var sprite = pawn.GetChildren().OfType<Sprite3D>().Single();
            var atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");
            var idle = UiKit.BattlePortrait(atlas, "sero");
            var attack = UiKit.BattlePortrait(atlas, "sero_attack");
            Require(sprite.Texture == idle, "開始時は弓下ろし");
            bool fired = false, returned = false;
            for (int i = 0; i < 2400 && !returned; i++)
            {
                await ToSignal(GetTree().CreateTimer(0.025), SceneTreeTimer.SignalName.Timeout);
                if (sprite.Texture == attack) fired = true;
                if (fired && sprite.Texture == idle) returned = true;
            }
            Require(fired && returned, "実際の攻撃から待機へ復帰");
            main.Call("TogglePause");
            await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
            pawn.AnimationSpeed = 1;
            pawn.AnimateBowAttack();
            pawn.SetBurning(true);
            Require(sprite.Texture == attack, "燃焼通知で射撃絵を消さない");
            pawn.SetBurning(false);
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            pawn.AnimateBowAttack();
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            Require(sprite.Texture == attack, "連射時に古い復帰予約が発火しない");
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            Require(sprite.Texture == idle, "連射終了後に待機へ復帰");
            pawn.AnimateBowAttack();
            pawn.AnimateDeath();
            Require(sprite.Texture == idle, "死亡時に射撃解除");
            pawn.AnimateRevive();
            Require(sprite.Texture == idle, "蘇生時は待機");
            pawn.AnimateBowAttack();
            pawn.AnimateVictory();
            await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
            Require(sprite.Texture == UiKit.Portrait(atlas, "sero"), "勝利時は通常立ち絵を保持");
            GD.Print("SERO_PORTRAIT_CHECK_OK actual-attack idle repeat burn death revive victory");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }
}
