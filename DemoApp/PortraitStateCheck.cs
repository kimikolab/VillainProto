using BattleCore;
using Godot;
using System;
using System.Linq;

// 開発用シーン。表示の状態遷移を実際の Sprite3D とシェーダーの両方で確かめる。
public partial class PortraitStateCheck : Node3D
{
    public override async void _Ready()
    {
        try
        {
            var atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");
            var normal = UiKit.BattlePortrait(atlas, "hota");
            var burning = UiKit.BattlePortrait(atlas, "hota", true);
            Require(!UiKit.Portrait(atlas, "hota").GetImage().GetData().SequenceEqual(normal.GetImage().GetData()), "選択用と戦闘用の画像を分離");
            Require(normal != burning, "燃焼差分の読み込み");
            Require(normal.GetSize() == burning.GetSize(), "差分のキャンバス寸法");
            Require(UiKit.BattlePortrait(atlas, "yomi", true) == UiKit.BattlePortrait(atlas, "yomi"), "差分なしの駒");
            var left = MakePawn(atlas, 1, BattleContext.PlayerTeam, -1.6f);
            var right = MakePawn(atlas, 2, BattleContext.EnemyTeam, 1.6f);
            Check(left, normal);
            left.SetBurning(true);
            Check(left, burning);
            left.SetBurning(false);
            Check(left, normal);
            left.SetBurning(true);
            left.AnimateDeath();
            Check(left, normal);
            left.SetBurning(true);
            Check(left, normal);
            left.AnimateRevive();
            left.SetBurning(true);
            Check(left, burning);
            left.AnimateVictory();
            await ToSignal(GetTree().CreateTimer(0.7), SceneTreeTimer.SignalName.Timeout);
            Check(left, UiKit.Portrait(atlas, "hota"));
            left.SetBurning(true);
            Check(left, UiKit.Portrait(atlas, "hota"));
            left.QueueFree();
            MakePawn(atlas, 3, BattleContext.PlayerTeam, -1.6f);
            right.SetBurning(true);
            Check(right, burning);
            Require(right.GetChildren().OfType<Sprite3D>().Single().FlipH, "敵側の左右反転");
            var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 5.4f, Position = new Vector3(0, 1.5f, 8), Current = true };
            AddChild(camera);
            var args = OS.GetCmdlineUserArgs();
            if (args.Contains("--capture"))
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath("res://../design/art/hota/portrait-state-check.png"));
            }
            GD.Print("PORTRAIT_STATE_CHECK_OK normal burning extinguish death revive victory fallback enemy");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            GetTree().Quit(1);
        }
    }

    private BattlePawn3D MakePawn(Texture2D atlas, int id, int team, float x)
    {
        var pawn = new BattlePawn3D();
        AddChild(pawn);
        pawn.Configure(new DemoOpening(id, team, "hota", "ホタ", 0, 78, 78, 6, AttackPattern.Single, true), atlas);
        pawn.Position = new Vector3(x, 0, 0);
        return pawn;
    }

    private static void Check(BattlePawn3D pawn, Texture2D expected)
    {
        var sprite = pawn.GetChildren().OfType<Sprite3D>().Single();
        Require(sprite.Texture == expected, "Sprite3D の画像");
        Require(((ShaderMaterial)sprite.MaterialOverride).GetShaderParameter("portrait_texture").AsGodotObject() == expected, "シェーダーの画像");
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }
}
