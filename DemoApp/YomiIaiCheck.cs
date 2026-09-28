using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 実際の攻撃入口で差分・左右・倍速・割り込みと再開の競合を検査する。
public partial class YomiIaiCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            Require(BattlefieldView3D.YomiIaiStrength(10, 10) < BattlefieldView3D.YomiIaiStrength(20, 10)
                && BattlefieldView3D.YomiIaiStrength(20, 10) < BattlefieldView3D.YomiIaiStrength(40, 10), "打点に応じた成長");
            Require(BattlefieldView3D.YomiIaiStrength(int.MaxValue, 0) == 1
                && BattlefieldView3D.YomiIaiStrength(0, 10) == 0, "強度の上下限");
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            var atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");
            void Reset(int team = 0, double speed = 1)
            {
                field.BeginBattle(new DemoOpening[] {
                    new(1, team, "yomi", "ヨミ", 2, 100, 100, 10, AttackPattern.Single, true),
                    new(2, 1-team, "knight", "相手", 2, 100, 100, 10, AttackPattern.Single, true),
                    new(3, 1-team, "knight", "相手", 0, 100, 100, 10, AttackPattern.Single, true),
                    new(4, 1-team, "knight", "相手", 1, 100, 100, 10, AttackPattern.Single, true),
                }, "ヨミ・居合の確認", 0);
                foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            }
            foreach (int team in new[] { 0, 1 })
            foreach (double speed in new[] { 1.0, 2.0 })
            foreach (bool extra in new[] { false, true })
            foreach (int power in new[] { 10, 20, 40 })
            {
                Reset(team, speed);
                await Wait(0.08);
                var pawn = field.FindPawn(1)!;
                var target = field.FindPawn(2)!;
                var pattern = power == 40 ? AttackPattern.Sweep : AttackPattern.Single;
                var hits = power == 40 ? new[] { target, field.FindPawn(3)!, field.FindPawn(4)! } : new[] { target };
                pawn.ShowMovementPortrait("yomi_stumble", 0.4);
                await field.Attack(pawn, target, pattern, hits, reaction: extra, advance: false, attackPower: power);
                string key = extra ? "yomi_iai_extra" : "yomi_iai";
                var sprite = pawn.GetChildren().OfType<Sprite3D>().Single();
                var texture = UiKit.BattlePortrait(atlas, key);
                Require(pawn.MovementPortrait == key && sprite.Texture == texture, "攻撃がよろめきを上書きし、通常と追加を区別");
                using var pixels = texture.GetImage();
                Require(pixels.GetPixel(0, 0).A == 0 && texture != UiKit.BattlePortrait(atlas, "yomi"), "透過差分をロード");
                float bottom = sprite.Position.Y - texture.GetHeight() * sprite.PixelSize * (0.5f - UiKit.BattlePortraitBottomPaddingRatio(key));
                Require(Math.Abs(bottom - 0.05f) < 0.04f, "差分でも足元が浮かない");
                Require(field.YomiIaiPlays == 1 && field.YomiIaiExtraPlays == (extra ? 1 : 0), "一振りの計数");
                Require(hits.All(h => h.Hp == 100 && h.Slot is >= 0 and <= 2), "表示がHPや席を変更しない");
                await Wait(0.045 / speed);
                await Capture($"iai-team{team}-speed{speed}-{key}-power{power}");
                await Wait(0.7 / speed);
                Require(pawn.MovementPortrait is null, "倍速でも待機へ復帰");
                GD.Print($"YOMI_IAI_CASE_OK team={team} speed={speed} extra={extra} power={power}");
            }
            Reset();
            await Wait(0.1);
            var old = field.FindPawn(1)!;
            var oldTarget = field.FindPawn(2)!;
            var pending = field.Attack(old, oldTarget, AttackPattern.Single, new[] { oldTarget }, advance: false, attackPower: 40);
            Reset();
            await pending;
            Require(field.YomiIaiPlays == 0 && field.FindPawn(1)!.MovementPortrait is null, "抜刀待ち中の再開で旧攻撃が漏れない");
            var live = field.FindPawn(1)!;
            live.ShowMovementPortrait("yomi_iai", 0.15);
            await Wait(0.1);
            live.ShowMovementPortrait("yomi_iai_extra", 0.4);
            await Wait(0.1);
            Require(live.MovementPortrait == "yomi_iai_extra", "前の攻撃の復帰予約で追加差分を消さない");
            live.AnimateDeath();
            live.AnimateRevive();
            await Wait(0.5);
            Require(live.MovementPortrait is null, "死亡・蘇生で差分が残らない");
            live.ShowMovementPortrait("yomi_iai_extra", 0.4);
            live.AnimateVictory();
            await Wait(0.5);
            Require(live.MovementPortrait is null, "勝利時に差分が残らない");
            GD.Print("YOMI_IAI_CHECK_OK");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
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
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
