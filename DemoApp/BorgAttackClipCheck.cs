using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 開発用シーン。ボルグの攻撃の連番（attack_clip/borg_sweep/・採用は F-5055）を実際の戦闘画面で流し、
// 再生・当たるコマ・踏み込んだ位置を振り切りまで保つ・待機ループへの復帰・足元の合わせ・踏み込んだ駒が振り切ってから席へ戻るを確かめる。
// 画面を撮るときは Godot の --write-movie <出力>/frame.png --fixed-fps 30 を付けて窓ありで回す
public partial class BorgAttackClipCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            string name = "borg_sweep";
            Require(BattlefieldView3D.BorgAttackClip == name, "ボルグの攻撃は既定で連番");
            var clip = BattlePawn3D.AttackClipOf(name);
            Require(clip is not null && clip.Frames.Length == 25, $"{name} の連番 25 コマ");

            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            DemoOpening[] openings = {
                new(1, 0, "borg", "ボルグ", 0, 100, 100, 20, AttackPattern.Sweep, true),
                new(2, 0, "hota", "ホタ", 2, 100, 100, 20, AttackPattern.Single, false),
                new(3, 0, "hiyo", "ヒヨ", 4, 100, 100, 20, AttackPattern.Single, false),
                new(4, 1, "knight", "標的1", 0, 100, 100, 20, AttackPattern.Single, true),
                new(5, 1, "knight", "標的2", 2, 100, 100, 20, AttackPattern.Single, true),
                new(6, 1, "knight", "標的3", 4, 100, 100, 20, AttackPattern.Single, true),
            };
            field.BeginBattle(openings, "ボルグの薙ぎ・連番の確認", 3);
            foreach (var p in field.Pawns.Values) p.AnimationSpeed = 1;
            var borg = field.FindPawn(1)!;
            var target = field.FindPawn(4)!;
            var hits = new[] { target, field.FindPawn(5)!, field.FindPawn(6)! };
            var sprite = borg.GetChildren().OfType<Sprite3D>().Single();
            await Wait(0.6);
            Require(borg.IdleLoopPlaying, "攻撃前は待機ループ");

            // 燃焼の攻撃の入口（PlayFireAttack）で流す。
            var attack = field.PlayFireAttack(borg, target, hits, new(FireLevelLabels.Stage, 3, 0), 1);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(borg.AttackClipPlaying && clip!.Frames.Contains(sprite.Texture) && !borg.IdleLoopPlaying, "攻撃で連番を流し、待機ループを止める");
            // 足底（連番の anchor の y）が地面の高さに来る。待機絵の足底と同じ高さ。
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            float feet = sprite.Position.Y - (clip.Anchor.Y - sprite.Texture.GetHeight() * 0.5f) * sprite.PixelSize;
            Require(Math.Abs(feet - BattlePawn3D.PortraitGroundYForCheck) < 0.05f, $"足底が地面に乗る（{feet:0.000}）");
            var shooting = Wait(clip.Frames.Length / clip.Fps + 0.9); // ゲーム内の時間で待つ（--write-movie でも同じ）
            await attack;
            Require(field.BorgAttackClipPlays == 1, "連番を 1 回流した");
            Require(borg.AttackClipFrame >= clip.Contact, "当たるコマまで進んでから命中を通知");
            // 踏み込んだ位置を振り切りまで保つ（胴が元の位置へ引き戻されない）。
            float peak = clip.Forward.Max();
            while (borg.AttackClipPlaying && borg.AttackClipFrame < clip.Frames.Length - 2)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(borg.AttackClipPlaying && borg.AttackClipForward > peak * 0.4f,
                $"振り切りまで前へ出たまま（{borg.AttackClipForward:0.0}）");
            await shooting;
            Require(!borg.AttackClipPlaying && borg.IdleLoopPlaying, "連番のあと待機ループへ戻る");
            Require(Math.Abs(sprite.PixelSize - UiKit.PortraitWorldHeight("borg") / sprite.Texture.GetHeight()) < 1e-4f, "待機絵の大きさへ戻す");

            // 汎用の攻撃の入口（燃焼の台本が無い攻撃）でも流す。踏み込んだ駒は振り切ってから席へ戻る。
            await Wait(0.3);
            Vector3 home = borg.Position;
            await field.Attack(borg, target, AttackPattern.Sweep, hits);
            Require(field.BorgAttackClipPlays == 2, "汎用の攻撃でも連番");
            Require(borg.AttackClipPlaying && borg.Position.DistanceTo(home) > 0.3f, "当たった直後はまだ踏み込んだ位置");
            await Wait(0.3);
            Require(borg.AttackClipPlaying && borg.Position.DistanceTo(home) > 0.3f, "振り切るまで席へ戻らない");
            await borg.AttackClipFinished;
            await Wait(0.5);
            Require(!borg.AttackClipPlaying && borg.IdleLoopPlaying && borg.Position.DistanceTo(home) < 0.05f, "振り切ってから席へ戻る");

            // 倒れたら止めて通常の絵へ。
            _ = borg.PlayAttackClip(name);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            borg.AnimateDeath();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(!borg.AttackClipPlaying && !clip.Frames.Contains(sprite.Texture), "倒れたら連番を止める");

            GD.Print($"BORG_ATTACK_CLIP_CHECK_OK frames={clip.Frames.Length} contact={clip.Contact} forward={peak:0.0}");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
