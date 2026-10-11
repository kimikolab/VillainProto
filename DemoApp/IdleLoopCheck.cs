using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 開発用シーン。待機ループ（idle_loop/<駒ID>/ の連番）が通常の待機絵のときだけ回り、
// 差分・死亡・勝利で止まり、戻ると再開することを実際の Sprite3D とシェーダーで確かめる。
// --capture で design/art/hiyo/idle-loop-game/ に数コマの画面を保存する。
public partial class IdleLoopCheck : Node3D
{
    public override async void _Ready()
    {
        try
        {
            var atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");
            var frames = BattlePawn3D.IdleLoopFramesOf("hiyo");
            Require(frames.Length == 19, $"ヒヨの連番 19 コマ（{frames.Length}）");
            Require(BattlePawn3D.IdleLoopFramesOf("hota").Length == 0, "連番の無い駒は空");
            var still = UiKit.BattlePortrait(atlas, "hiyo");

            var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 3.0f, Position = new Vector3(0, 1.1f, 8), Current = true };
            AddChild(camera);
            var hiyo = MakePawn(atlas, 1, BattleContext.PlayerTeam, -0.9f, "hiyo");
            var enemy = MakePawn(atlas, 2, BattleContext.EnemyTeam, 1.4f, "hiyo");
            var hota = MakePawn(atlas, 3, BattleContext.PlayerTeam, 3.6f, "hota");
            var sprite = SpriteOf(hiyo);

            // 開戦直後から連番。待機絵と同じ高さ（=同じ足元）で描く。
            Require(hiyo.IdleLoopPlaying && frames.Contains(sprite.Texture), "開戦から連番");
            Require(Shader(sprite) == sprite.Texture, "シェーダーも連番");
            float worldHeight = sprite.Texture.GetHeight() * sprite.PixelSize;
            Require(Math.Abs(worldHeight - UiKit.PortraitWorldHeight("hiyo")) < 1e-4f, "待機絵と同じ高さ");
            Require(!hota.IdleLoopPlaying && SpriteOf(hota).Texture == UiKit.BattlePortrait(atlas, "hota"), "連番の無い駒は静止絵のまま");
            Require(SpriteOf(enemy).FlipH && enemy.IdleLoopPlaying, "敵側も反転して回る");

            // 時間とともにコマが進む（16fps）。倍速では倍進む。
            int seen = CountDistinct(hiyo, 0.6);
            Require(seen >= 6, $"コマが進む（{seen}）");
            hiyo.AnimationSpeed = 2;
            int seenFast = CountDistinct(hiyo, 0.3);
            Require(seenFast >= 6, $"倍速で進む（{seenFast}）");
            hiyo.AnimationSpeed = 1;
            await Wait(0.05);
            await Capture("loop-a");
            await Wait(0.31);
            await Capture("loop-b");

            // 動作差分のあいだは止め、差分を出す。
            hiyo.ShowMovementPortrait("hiyo_gift", 0.4);
            var gift = UiKit.BattlePortrait(atlas, "hiyo_gift");
            Require(!hiyo.IdleLoopPlaying && sprite.Texture == gift && Shader(sprite) == gift, "差分で止める");
            await Wait(0.2);
            Require(sprite.Texture == gift, "差分の途中で連番に戻さない");
            await Capture("gift");
            await Wait(0.4);
            Require(hiyo.IdleLoopPlaying && frames.Contains(sprite.Texture), "差分の後に再開");

            // 燃焼の通知でも止めない（ヒヨに燃焼差分は無い）。
            hiyo.SetBurning(true);
            Require(hiyo.IdleLoopPlaying, "燃焼中も回る");
            hiyo.SetBurning(false);

            // 倒れたら止まり、蘇生で再開する。
            hiyo.AnimateDeath();
            await Wait(0.1);
            var dead = sprite.Texture;
            await Wait(0.3);
            Require(!hiyo.IdleLoopPlaying && sprite.Texture == dead, "倒れたら止まる");
            hiyo.AnimateRevive();
            Require(hiyo.IdleLoopPlaying && frames.Contains(sprite.Texture), "蘇生で再開");

            // 勝利絵を連番で上書きしない。
            hiyo.AnimateVictory();
            await Wait(0.7);
            Require(!hiyo.IdleLoopPlaying && sprite.Texture == UiKit.Portrait(atlas, "hiyo"), "勝利絵を保つ");
            Require(still != frames[0], "静止絵と連番は別の画像");

            GD.Print("IDLE_LOOP_CHECK_OK start advance speed gift burning death revive victory enemy fallback");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            GetTree().Quit(1);
        }
    }

    private int CountDistinct(BattlePawn3D pawn, double seconds)
    {
        // 物理時間を待たずに駆動ノードを直接進める（頭なしでも描画速度に依らない）。
        var seen = new System.Collections.Generic.HashSet<int>();
        for (double t = 0; t < seconds; t += 1.0 / 60)
        {
            pawn.ProcessIdleLoop(1.0 / 60);
            seen.Add(pawn.IdleLoopFrame);
        }
        return seen.Count;
    }

    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string dir = ProjectSettings.GlobalizePath("res://../design/art/hiyo/idle-loop-game");
        DirAccess.MakeDirRecursiveAbsolute(dir);
        GetViewport().GetTexture().GetImage().SavePng($"{dir}/{name}.png");
    }

    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private BattlePawn3D MakePawn(Texture2D atlas, int id, int team, float x, string key)
    {
        var pawn = new BattlePawn3D();
        AddChild(pawn);
        pawn.Configure(new DemoOpening(id, team, key, key, 0, 78, 78, 6, AttackPattern.Single, true), atlas);
        pawn.Position = new Vector3(x, 0, 0);
        return pawn;
    }

    private static Sprite3D SpriteOf(BattlePawn3D pawn) => pawn.GetChildren().OfType<Sprite3D>().Single();
    private static GodotObject Shader(Sprite3D sprite) => ((ShaderMaterial)sprite.MaterialOverride).GetShaderParameter("portrait_texture").AsGodotObject();

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }
}
