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
            Require(BattlePawn3D.IdleLoopFramesOf("borg").Length == 44, "ボルグの連番 44 コマ");
            Require(BattlePawn3D.IdleLoopFramesOf("hota").Length == 36, "ホタの連番 36 コマ");
            Require(BattlePawn3D.IdleLoopFramesOf("gald").Length == 32, "ガルドの連番 32 コマ");
            Require(BattlePawn3D.IdleLoopFramesOf("gare").Length == 30, "ガレの連番 30 コマ");
            Require(BattlePawn3D.IdleLoopFramesOf("hibi").Length == 30, "ヒビのリテイク版 30 コマ");
            Require(BattlePawn3D.IdleLoopFramesOf("hibi").All(frame => frame.GetSize() == new Vector2(640, 960)), "ヒビの連番の寸法");
            var ricaFrames = BattlePawn3D.IdleLoopFramesOf("rica");
            Require(ricaFrames.Length == 39, "リィカの採用Bは39コマ");
            Require(ricaFrames.All(frame => frame.GetSize() == new Vector2(640, 960)), "リィカの連番の寸法");
            var hagiFrames = BattlePawn3D.IdleLoopFramesOf("hagi");
            Require(hagiFrames.Length == 48, "ハギの修正版Bは48コマ");
            Require(hagiFrames.All(frame => frame.GetSize() == new Vector2(640, 960)), "ハギの連番の寸法");
            Require(BattlePawn3D.IdleLoopFramesOf("golm").Length == 0, "連番の無い駒は空");
            var still = UiKit.BattlePortrait(atlas, "hiyo");

            var camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Size = 3.0f, Position = new Vector3(0, 1.1f, 8), Current = true };
            AddChild(camera);
            var hiyo = MakePawn(atlas, 1, BattleContext.PlayerTeam, -0.9f, "hiyo");
            var enemy = MakePawn(atlas, 2, BattleContext.EnemyTeam, 1.4f, "hiyo");
            var golm = MakePawn(atlas, 3, BattleContext.PlayerTeam, 3.6f, "golm");
            var borg = MakePawn(atlas, 4, BattleContext.PlayerTeam, -2.6f, "borg");
            var hota = MakePawn(atlas, 5, BattleContext.PlayerTeam, 2.4f, "hota");
            var gald = MakePawn(atlas, 6, BattleContext.PlayerTeam, 4.8f, "gald");
            var gare = MakePawn(atlas, 8, BattleContext.PlayerTeam, 8.0f, "gare");
            var gareEnemy = MakePawn(atlas, 9, BattleContext.EnemyTeam, 9.8f, "gare");
            var hibi = MakePawn(atlas, 10, BattleContext.PlayerTeam, 11.6f, "hibi");
            var hibiEnemy = MakePawn(atlas, 11, BattleContext.EnemyTeam, 13.4f, "hibi");
            var rica = MakePawn(atlas, 12, BattleContext.PlayerTeam, 15.2f, "rica");
            var ricaEnemy = MakePawn(atlas, 13, BattleContext.EnemyTeam, 17.0f, "rica");
            var hagi = MakePawn(atlas, 14, BattleContext.PlayerTeam, 18.8f, "hagi");
            var hagiEnemy = MakePawn(atlas, 15, BattleContext.EnemyTeam, 20.6f, "hagi");
            var sprite = SpriteOf(hiyo);

            // 開戦直後から連番。待機絵と同じ高さ（=同じ足元）で描く。
            Require(hiyo.IdleLoopPlaying && frames.Contains(sprite.Texture), "開戦から連番");
            Require(Shader(sprite) == sprite.Texture, "シェーダーも連番");
            float worldHeight = sprite.Texture.GetHeight() * sprite.PixelSize;
            Require(Math.Abs(worldHeight - UiKit.PortraitWorldHeight("hiyo")) < 1e-4f, "待機絵と同じ高さ");
            Require(!golm.IdleLoopPlaying && SpriteOf(golm).Texture == UiKit.BattlePortrait(atlas, "golm"), "連番の無い駒は静止絵のまま");
            foreach (var (pawn, key) in new[] { (borg, "borg"), (hota, "hota"), (gald, "gald"), (gare, "gare"), (hibi, "hibi"), (rica, "rica"), (hagi, "hagi") })
            {
                var s = SpriteOf(pawn);
                Require(pawn.IdleLoopPlaying && BattlePawn3D.IdleLoopFramesOf(key).Contains(s.Texture), $"{key} も開戦から連番");
                Require(Math.Abs(s.Texture.GetHeight() * s.PixelSize - UiKit.PortraitWorldHeight(key)) < 1e-4f, $"{key} も待機絵と同じ高さ");
                Require(Shader(s) == s.Texture, $"{key} もシェーダーと同じ連番");
            }
            Require(CountDistinct(hagi, 3.1) == 48, "ハギの全48コマが進み周期を跨ぐ");
            hagi.AnimationSpeed = 2;
            Require(CountDistinct(hagi, 0.3) >= 6, "ハギも倍速で進む");
            hagi.AnimationSpeed = 1;
            hagi.HagiPaused = true;
            int hagiPausedFrame = hagi.IdleLoopFrame;
            CountDistinct(hagi, 0.3);
            Require(hagi.IdleLoopFrame == hagiPausedFrame, "ハギは一時停止で待機連番も止まる");
            hagi.HagiPaused = false;
            Require(CountDistinct(hagi, 0.4) >= 6, "ハギは一時停止解除で再開");
            foreach (string pose in new[] { "hagi_step", "hagi_windup", "hagi_follow" })
            {
                hagi.SetHagiPose(pose);
                var action = UiKit.BattlePortrait(atlas, pose);
                CountDistinct(hagi, 0.3);
                Require(!hagi.IdleLoopPlaying && SpriteOf(hagi).Texture == action && Shader(SpriteOf(hagi)) == action, "ハギの攻撃差分を待機で上書きしない " + pose);
            }
            hagi.SetHagiPose(null);
            Require(hagi.IdleLoopPlaying && hagiFrames.Contains(SpriteOf(hagi).Texture), "ハギは攻撃差分から待機へ戻る");
            hagi.SetBurning(true);
            Require(hagi.IdleLoopPlaying && hagiFrames.Contains(SpriteOf(hagi).Texture), "ハギは燃焼中も通常の連番が回る");
            hagi.SetBurning(false);
            hagi.AnimateDeath();
            var hagiDead = SpriteOf(hagi).Texture;
            CountDistinct(hagi, 0.3);
            Require(!hagi.IdleLoopPlaying && SpriteOf(hagi).Texture == hagiDead && Shader(SpriteOf(hagi)) == hagiDead, "ハギも死亡で止まる");
            hagi.AnimateRevive();
            Require(hagi.IdleLoopPlaying && hagiFrames.Contains(SpriteOf(hagi).Texture), "ハギも蘇生で再開");
            Require(SpriteOf(hagiEnemy).FlipH && hagiEnemy.IdleLoopPlaying, "敵側のハギも反転して回る");
            Require(CountDistinct(gare, 0.6) >= 6, "ガレの連番も進む");
            gare.AnimationSpeed = 2;
            Require(CountDistinct(gare, 0.3) >= 6, "ガレも倍速で進む");
            gare.AnimationSpeed = 1;
            gare.SetBurning(true);
            Require(gare.IdleLoopPlaying, "ガレは燃焼中も回る");
            gare.SetBurning(false);
            gare.AnimateDeath();
            var gareDead = SpriteOf(gare).Texture;
            CountDistinct(gare, 0.3);
            Require(!gare.IdleLoopPlaying && SpriteOf(gare).Texture == gareDead, "ガレも死亡で止まる");
            gare.AnimateRevive();
            Require(gare.IdleLoopPlaying && BattlePawn3D.IdleLoopFramesOf("gare").Contains(SpriteOf(gare).Texture), "ガレも蘇生で再開");
            Require(SpriteOf(gareEnemy).FlipH && gareEnemy.IdleLoopPlaying, "敵側のガレも反転して回る");
            Require(CountDistinct(hibi, 2.0) == 30, "ヒビの全30コマが進み周期を跨ぐ");
            hibi.AnimationSpeed = 2;
            Require(CountDistinct(hibi, 0.3) >= 6, "ヒビも倍速で進む");
            hibi.AnimationSpeed = 1;
            hibi.SetBurning(true);
            Require(hibi.IdleLoopPlaying && BattlePawn3D.IdleLoopFramesOf("hibi").Contains(SpriteOf(hibi).Texture), "ヒビは燃焼中も通常の連番が回る");
            hibi.SetBurning(false);
            hibi.AnimateDeath();
            var hibiDead = SpriteOf(hibi).Texture;
            CountDistinct(hibi, 0.3);
            Require(!hibi.IdleLoopPlaying && SpriteOf(hibi).Texture == hibiDead && Shader(SpriteOf(hibi)) == hibiDead, "ヒビも死亡で止まる");
            hibi.AnimateRevive();
            Require(hibi.IdleLoopPlaying && BattlePawn3D.IdleLoopFramesOf("hibi").Contains(SpriteOf(hibi).Texture), "ヒビも蘇生で再開");
            Require(SpriteOf(hibiEnemy).FlipH && hibiEnemy.IdleLoopPlaying, "敵側のヒビも反転して回る");
            Require(CountDistinct(rica, 2.5) == 39, "リィカの全39コマが進み周期を跨ぐ");
            rica.AnimationSpeed = 2;
            Require(CountDistinct(rica, 0.3) >= 6, "リィカも倍速で進む");
            rica.AnimationSpeed = 1;
            rica.SetBurning(true);
            Require(rica.IdleLoopPlaying && ricaFrames.Contains(SpriteOf(rica).Texture), "リィカは燃焼中も通常連番が回る");
            rica.SetBurning(false);
            rica.AnimateDeath();
            var ricaDead = SpriteOf(rica).Texture;
            CountDistinct(rica, 0.3);
            Require(!rica.IdleLoopPlaying && SpriteOf(rica).Texture == ricaDead && Shader(SpriteOf(rica)) == ricaDead, "リィカも死亡で止まる");
            rica.AnimateRevive();
            Require(rica.IdleLoopPlaying && ricaFrames.Contains(SpriteOf(rica).Texture), "リィカも蘇生で再開");
            Require(SpriteOf(ricaEnemy).FlipH && ricaEnemy.IdleLoopPlaying, "敵側のリィカも反転して回る");
            foreach (int stage in new[] { 1, 2, 3 })
            foreach (string? candidate in new string?[] { null, "B" })
            {
                rica.SetDeathState(stage * 3, 0, 0);
                rica.SetRicaIdleVariant(candidate);
                string name = $"rica_stage{stage}" + (candidate == "B" ? "_b" : "");
                var stageFrames = BattlePawn3D.IdleLoopFramesOf(name);
                Require(stageFrames.Length > 0 || candidate is not null || !OS.GetCmdlineUserArgs().Contains("--rica-idles-required"),
                    "指定された覚醒待機が全て存在 " + name);
                if (stageFrames.Length == 0) continue;
                Require(stageFrames.Length > 8 && rica.IdleLoopPlaying && stageFrames.Contains(SpriteOf(rica).Texture),
                    "覚醒待機の候補が開戦と同じ窓口で再生 " + name);
                Require(BattlePawn3D.RicaTrackOf(name).Length == stageFrames.Length, "待機の全コマに武器の追跡点 " + name);
                Require(CountDistinct(rica, 0.4) >= 5, "覚醒待機も時刻に従って進む " + name);
            }
            rica.SetRicaIdleVariant(null);
            rica.SetDeathState(0, 0, 0);
            Require(rica.IdleLoopFrameCount == 39, "層が戻ると採用済み通常39コマへ戻る");
            Require(CountDistinct(gald, 0.6) >= 6, "ガルドの連番も進む");
            gald.AnimateDeath();
            var galdDead = SpriteOf(gald).Texture;
            CountDistinct(gald, 0.1);
            Require(!gald.IdleLoopPlaying && SpriteOf(gald).Texture == galdDead, "ガルドも死亡で止まる");
            gald.AnimateRevive();
            Require(gald.IdleLoopPlaying, "ガルドも蘇生で再開");
            gald.DrawSword(18);
            var galdSprite = SpriteOf(gald);
            Require(!gald.IdleLoopPlaying && galdSprite.Texture == UiKit.BattlePortrait(atlas, "gald_sword")
                && Shader(galdSprite) == galdSprite.Texture, "ガルドの抜剣絵を連番で上書きしない");
            gald.QuietLastStand = true;
            gald.AnimateDeath();
            CountDistinct(gald, 0.6);
            Require(!gald.IdleLoopPlaying && galdSprite.Texture == UiKit.BattlePortrait(atlas, "gald_kneel")
                && Shader(galdSprite) == galdSprite.Texture, "ガルドの膝つき絵を保持");
            var galdWinner = MakePawn(atlas, 7, BattleContext.PlayerTeam, 6.8f, "gald");
            galdWinner.AnimateVictory();
            // ホタは燃焼の差分絵を持つ。燃えているあいだは差分の連番（hota_burning）、消えたら通常の連番へ戻る。
            var burnFrames = BattlePawn3D.IdleLoopFramesOf("hota_burning");
            Require(burnFrames.Length > 0 && !burnFrames.Intersect(BattlePawn3D.IdleLoopFramesOf("hota")).Any(), "ホタの燃焼差分の連番");
            hota.SetBurning(true);
            Require(hota.IdleLoopPlaying && burnFrames.Contains(SpriteOf(hota).Texture) && Shader(SpriteOf(hota)) == SpriteOf(hota).Texture, "燃焼中は差分の連番");
            Require(CountDistinct(hota, 0.6) >= 6, "燃焼中の連番も進む");
            Require(burnFrames.Contains(SpriteOf(hota).Texture), "燃焼中に通常の連番へ戻さない");
            Require(Math.Abs(SpriteOf(hota).Texture.GetHeight() * SpriteOf(hota).PixelSize - UiKit.PortraitWorldHeight("hota")) < 1e-4f, "燃焼差分も同じ高さ");
            hota.SetBurning(false);
            Require(hota.IdleLoopPlaying && BattlePawn3D.IdleLoopFramesOf("hota").Contains(SpriteOf(hota).Texture), "鎮火で通常の連番へ戻る");
            Require(BattlePawn3D.IdleLoopFramesOf("borg_burning").Length == 0, "差分の連番が無い駒は空");
            // ボルグは燃焼差分を持たないので、燃えていても回る。
            borg.SetBurning(true);
            Require(borg.IdleLoopPlaying, "ボルグは燃焼中も回る");
            borg.SetBurning(false);
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
            gare.AnimateVictory();
            hibi.AnimateVictory();
            rica.AnimateVictory();
            hagi.AnimateVictory();
            await Wait(0.7);
            Require(!hiyo.IdleLoopPlaying && sprite.Texture == UiKit.Portrait(atlas, "hiyo"), "勝利絵を保つ");
            Require(!galdWinner.IdleLoopPlaying && SpriteOf(galdWinner).Texture == UiKit.Portrait(atlas, "gald"), "ガルドも勝利絵を保つ");
            Require(!gare.IdleLoopPlaying && SpriteOf(gare).Texture == UiKit.Portrait(atlas, "gare"), "ガレも勝利絵を保つ");
            Require(!hibi.IdleLoopPlaying && SpriteOf(hibi).Texture == UiKit.Portrait(atlas, "hibi") && Shader(SpriteOf(hibi)) == SpriteOf(hibi).Texture, "ヒビも勝利絵を保つ");
            Require(!rica.IdleLoopPlaying && SpriteOf(rica).Texture == UiKit.Portrait(atlas, "rica") && Shader(SpriteOf(rica)) == SpriteOf(rica).Texture, "リィカも勝利絵を保つ");
            Require(still != frames[0], "静止絵と連番は別の画像");

            CountDistinct(hagi, 0.3);
            Require(!hagi.IdleLoopPlaying && SpriteOf(hagi).Texture == UiKit.Portrait(atlas, "hagi") && Shader(SpriteOf(hagi)) == SpriteOf(hagi).Texture, "ハギも勝利絵を保つ");
            GD.Print("IDLE_LOOP_CHECK_OK hiyo borg hota hota_burning gald gare hibi rica hagi start advance speed pause action gift burning burning-variant death revive victory enemy fallback sword kneel");
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
