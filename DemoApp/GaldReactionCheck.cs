using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 実際の被弾・受け流しの入口から、盾差分と待機連番・抜剣・生死の競合を確かめる。
public partial class GaldReactionCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            var atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");
            foreach (int team in new[] { 0, 1 })
            foreach (double speed in new[] { 1.0, 2.0 })
            {
                Begin(field, team);
                var pawn = field.FindPawn(1)!;
                var attacker = field.FindPawn(2)!;
                pawn.AnimationSpeed = speed;
                await Wait(0.1);
                CheckIdle(pawn);
                await Capture($"idle-team{team}-speed{speed}");

                pawn.AnimateHit();
                CheckReaction(pawn, atlas, "gald_hit", team);
                await Capture($"hit-team{team}-speed{speed}");
                await Wait(0.28 / speed + 0.08);
                CheckIdle(pawn);
                field.Parry(attacker, pawn);
                CheckReaction(pawn, atlas, "gald_parry", team);
                pawn.SetBurning(true);
                CheckReaction(pawn, atlas, "gald_parry", team);
                pawn.SetBurning(false);
                await Capture($"parry-team{team}-speed{speed}");
                await Wait(0.32 / speed + 0.08);
                CheckIdle(pawn);
                pawn.AnimateHit(poison: true);
                Require(pawn.MovementPortrait is null && pawn.IdleLoopPlaying, "毒で盾の被弾差分を出さない");
                await Wait(0.25 / speed + 0.05);

                // 先の被弾の復帰予約が、後から始めた受け流しを消さない。
                pawn.AnimateHit();
                await Wait(0.20 / speed);
                field.Parry(attacker, pawn);
                await Wait(0.13 / speed);
                CheckReaction(pawn, atlas, "gald_parry", team);
                await Wait(0.32 / speed);
                CheckIdle(pawn);

                // ノードを止めた間は、差分の復帰予約も進めない。
                pawn.AnimateHit();
                pawn.ProcessMode = ProcessModeEnum.Disabled;
                await Wait(0.4);
                CheckReaction(pawn, atlas, "gald_hit", team);
                pawn.ProcessMode = ProcessModeEnum.Inherit;
                await Wait(0.28 / speed + 0.08);
                CheckIdle(pawn);

                pawn.AnimateHit();
                pawn.AnimateDeath();
                Require(pawn.MovementPortrait is null, "死亡で差分を解除");
                pawn.AnimateRevive();
                await Wait(0.4);
                CheckIdle(pawn);

                // 盾差分の途中で抜剣しても、即座に剣の構えへ切り替える。
                field.Parry(attacker, pawn);
                pawn.DrawSword(18);
                CheckSword(pawn, atlas);
                pawn.AnimateHit();
                pawn.AnimateHit(poison: true);
                field.Parry(attacker, pawn);
                await Wait(0.4);
                CheckSword(pawn, atlas);
                pawn.QuietLastStand = true;
                pawn.AnimateDeath();
                await Wait(0.4);
                Require(pawn.MovementPortrait is null && !pawn.IdleLoopPlaying
                    && Sprite(pawn).Texture == UiKit.BattlePortrait(atlas, "gald_kneel"), "膝つき姿勢を保持");
                Require(pawn.Hp == 100 && attacker.Hp == 100, "差分演出はHPを変えない");

                // 再戦で先のノードと予約を破棄し、新しい待機へ戻る。
                Begin(field, team);
                pawn = field.FindPawn(1)!;
                pawn.AnimationSpeed = speed;
                pawn.AnimateHit();
                if (team == 0)
                {
                    pawn.AnimateVictory();
                    await Wait(0.6);
                    Require(pawn.MovementPortrait is null && !pawn.IdleLoopPlaying
                        && Sprite(pawn).Texture == UiKit.Portrait(atlas, "gald"), "勝利後に盾差分を戻さない");
                    pawn.AnimateHit();
                    Require(pawn.MovementPortrait is null, "勝利後の通知で盾差分を出さない");
                }
                Begin(field, team);
                await Wait(0.4);
                CheckIdle(field.FindPawn(1)!);
                GD.Print($"GALD_REACTION_OK team={team} speed={speed} hit parry poison repeat pause death revive sword kneel victory restart");
            }
            field.QueueFree();
            await Wait(0.2);
            GD.Print("GALD_REACTION_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static void Begin(BattlefieldView3D field, int team) => field.BeginBattle(new DemoOpening[] {
        new(1, team, "gald", "ガルド", 0, 100, 100, 9, AttackPattern.Single, true),
        new(2, 1 - team, "knight", "相手", 0, 100, 100, 10, AttackPattern.Single, true),
    }, "ガルドの被弾・受け流し", 0);

    private static Sprite3D Sprite(BattlePawn3D pawn) => pawn.GetChildren().OfType<Sprite3D>().Single();
    private static void CheckIdle(BattlePawn3D pawn)
    {
        Require(pawn.MovementPortrait is null && pawn.IdleLoopPlaying
            && BattlePawn3D.IdleLoopFramesOf("gald").Contains(Sprite(pawn).Texture), "待機連番へ復帰");
    }
    private static void CheckSword(BattlePawn3D pawn, Texture2D atlas)
    {
        Require(pawn.MovementPortrait is null && !pawn.IdleLoopPlaying
            && Sprite(pawn).Texture == UiKit.BattlePortrait(atlas, "gald_sword"), "抜剣後に盾差分を残さない");
    }
    private static void CheckReaction(BattlePawn3D pawn, Texture2D atlas, string key, int team)
    {
        var sprite = Sprite(pawn);
        var texture = UiKit.BattlePortrait(atlas, key);
        Require(UiKit.HasCustomBattlePortrait(key) && pawn.MovementPortrait == key
            && sprite.Texture == texture && !pawn.IdleLoopPlaying, "実際の入口で差分を表示");
        Require(((ShaderMaterial)sprite.MaterialOverride).GetShaderParameter("portrait_texture").AsGodotObject() == texture, "シェーダーも差分");
        Require(sprite.FlipH == (team == BattleContext.EnemyTeam), "敵側は左右反転");
        using var pixels = texture.GetImage();
        int sole = -1;
        for (int y = pixels.GetHeight() - 1; y >= 0 && sole < 0; y--)
        for (int x = 0; x < pixels.GetWidth(); x++)
            if (pixels.GetPixel(x, y).A > 0.5f) { sole = y; break; }
        float foot = sprite.Position.Y + (pixels.GetHeight() * 0.5f - sole - 1) * sprite.PixelSize * sprite.Scale.Y;
        Require(sole >= 0 && Math.Abs(foot - BattlePawn3D.PortraitGroundYForCheck) < 0.006f, "実画像の靴底が地面に揃う（呼吸の拡縮込み）");
    }
    private async Task Capture(string name)
    {
        string? arg = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (arg is null || DisplayServer.GetName() == "headless") return;
        string directory = arg[14..];
        DirAccess.MakeDirRecursiveAbsolute(directory);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(directory, name + ".png")) == Error.Ok, "画像保存");
    }
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); }
}
