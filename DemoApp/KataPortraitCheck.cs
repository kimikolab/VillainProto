using Godot;
using System;
using System.Linq;

// 採用立ち絵を実際の編成・戦闘・勝利表示で確認する開発用シーン。
public partial class KataPortraitCheck : Node
{
    public override async void _Ready()
    {
        try
        {
            var main = GD.Load<PackedScene>("res://Main.tscn").Instantiate();
            AddChild(main);
            main.Call("ClearFormation");
            string[] keys = { "borg", "beni", "kata", "mio", "gald" };
            for (int i = 0; i < keys.Length; i++) main.Call("DropUnit", i, keys[i]);
            await Capture("formation");
            main.Call("StartBattle");
            var pawn = FindChildren("*", "", true, false).OfType<BattlePawn3D>().Single(p => p.UnitId == "kata");
            var sprite = pawn.GetChildren().OfType<Sprite3D>().Single();
            var atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");
            if (!UiKit.HasCustomPortrait("kata") || !UiKit.HasCustomBattlePortrait("kata")
                || sprite.Texture != UiKit.BattlePortrait(atlas, "kata"))
                throw new Exception("カタの採用画像が読み込まれていない");
            await Capture("battle");
            pawn.SetBurning(true);
            pawn.SetBurning(false);
            if (sprite.Texture != UiKit.BattlePortrait(atlas, "kata")) throw new Exception("状態更新後の画像");
            pawn.AnimateVictory();
            await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
            if (sprite.Texture != UiKit.Portrait(atlas, "kata")) throw new Exception("勝利時の待機画像");
            GD.Print("KATA_PORTRAIT_CHECK_OK formation battle status victory");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private async System.Threading.Tasks.Task Capture(string state)
    {
        await ToSignal(GetTree().CreateTimer(0.2), SceneTreeTimer.SignalName.Timeout);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var error = GetViewport().GetTexture().GetImage().SavePng(
            ProjectSettings.GlobalizePath($"res://../design/art/kata/{state}-game-check.png"));
        if (error != Error.Ok) throw new Exception("画面保存失敗");
    }
}
