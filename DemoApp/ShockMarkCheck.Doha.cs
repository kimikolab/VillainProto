using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    private async Task CheckDohaPortraits()
    {
        var atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");
        Require(UiKit.HasCustomPortrait("doha"), "編成用のドハを読み込む");
        foreach (string key in new[] { "doha", "doha_receive", "doha_give" })
        {
            Require(UiKit.HasCustomBattlePortrait(key), $"戦闘差分 {key}");
            using var image = UiKit.BattlePortrait(atlas, key).GetImage();
            Require(image.GetSize() == new Vector2I(1024, 1536) && image.GetPixel(0, 0).A == 0,
                "共通寸法と透過背景");
        }
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        var audio = (BattleAttackAudio)typeof(BattlefieldView3D).GetField("_attackAudio", Flags)!.GetValue(field)!;
        int Sounds(ShockMarkSound sound) => audio.ShockAssetPlays.TryGetValue(sound, out int count) ? count : 0;
        // PNG保存にかかる時間で短い受け止めが終わらないよう、描画中だけ時計を止める。
        async Task Snapshot(string name)
        {
            bool paused = GetTree().Paused;
            GetTree().Paused = true;
            try { await Capture(name); }
            finally { GetTree().Paused = paused; }
        }
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            DemoOpening[] opening = [
                new(1, team, "doha", "分かちのドハ", 2, 80, 100, 10, AttackPattern.Single, false),
                new(2, team, "hisa", "囃し立てのヒサ", 0, 80, 100, 10, AttackPattern.Single, false),
                new(3, 1 - team, "zan", "仇討ちのザン", 2, 80, 100, 10, AttackPattern.Single, false)];
            field.BeginBattle(opening, "ドハ・立ち絵と力配り", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            var doha = field.FindPawn(1)!; var ally = field.FindPawn(2)!;
            var sprite = doha.GetChildren().OfType<Sprite3D>().Single();
            var material = (ShaderMaterial)sprite.MaterialOverride;
            void CheckPose(string key)
            {
                var expected = UiKit.BattlePortrait(atlas, key);
                Require(sprite.Texture == expected
                    && material.GetShaderParameter("portrait_texture").AsGodotObject() == expected,
                    $"Spriteとシェーダーの差分が一致: {key}");
                float ground = sprite.Position.Y - (0.5f - UiKit.BattlePortraitBottomPaddingRatio(key))
                    * expected.GetHeight() * sprite.PixelSize;
                Require(Math.Abs(ground - 0.05f) < 0.025f, "差分を替えても足元が浮かない");
            }
            await Wait(0.1);
            CheckPose("doha");
            Require(sprite.FlipH == (team == 1), "敵側の待機反転");
            await Snapshot($"doha-team{team}-speed{speed}-idle");
            var cue = new BattleEvent { Kind = BattleEventKind.ShareGive, Turn = 1,
                ActorId = 1, TargetId = 2, Text = ShareGiveLabels.Power, Amount = 2 };
            var share = field.ShowSharePower(doha, ally, cue, speed);
            CheckPose("doha_receive");
            await Wait(DohaShareFx.TravelSeconds(speed) * 0.48);
            Require(!share.IsCompleted && Sounds(ShockMarkSound.DohaPain) == 0 && Sounds(ShockMarkSound.DohaGive) == 0,
                "痛みの移動中は受け止めを保ち、まだSEを鳴らさない");
            await Snapshot($"doha-team{team}-speed{speed}-receive");
            GetTree().Paused = true;
            await Wait(0.40);
            Require(!share.IsCompleted && Sounds(ShockMarkSound.DohaPain) == 0, "停止中は流れも受け取り音も進まない");
            Require(doha.MovementPortrait == "doha_receive", $"停止中も差分を保持: {doha.MovementPortrait} speed={speed}");
            GetTree().Paused = false;
            await share;
            CheckPose("doha_give");
            Require(Sounds(ShockMarkSound.DohaPain) == 1 && Sounds(ShockMarkSound.DohaGive) == 1,
                "胸への到達・札の離脱に指定SEを1回ずつ再生");
            Require(audio.GetChildren().OfType<AudioStreamPlayer>().All(v => !v.Playing || v.PitchScale == 1),
                "倍速でも原音程を維持");
            await Snapshot($"doha-team{team}-speed{speed}-give");
            await Wait(0.55 / speed);
            CheckPose("doha");
            await Wait(0.08);
            Require(field.ActiveDohaShares == 0, "往復後に帯・煙・札を解放");
            Require(field.SharePowerPlays == 1 && field.Pawns.Values.All(p => p.Hp == 80),
                "1通知1往復・演出はHPを変更しない");

            // 前の発動の待ち時間が完了しても、新しい受け止めを先に解かない。
            int old = doha.BeginSharePain(false);
            int current = doha.BeginSharePain(true);
            Require(!doha.GiveSharePower(old, false) && doha.MovementPortrait == "doha_receive",
                "古い発動は新しい差分を上書きしない");
            Require(doha.GiveSharePower(current, true) && sprite.FlipH, "左向きに返す");
            current = doha.BeginSharePain(false);
            Require(doha.GiveSharePower(current, false) && !sprite.FlipH, "右向きに返す");
            doha.SetBurning(true); CheckPose("doha_give");
            doha.SetBurning(false);

            int painBefore = Sounds(ShockMarkSound.DohaPain), giveBefore = Sounds(ShockMarkSound.DohaGive);
            var dying = field.ShowSharePower(doha, ally, cue, speed);
            doha.AnimateDeath();
            Require(doha.MovementPortrait is null, "死亡時に差分解除");
            await dying;
            await Wait(0.05);
            Require(field.ActiveDohaShares == 0 && Sounds(ShockMarkSound.DohaPain) == painBefore
                && Sounds(ShockMarkSound.DohaGive) == giveBefore, "死亡後に帯や煙、予約音を残さない");
            Require(doha.MovementPortrait is null, "死亡後に力返しへ戻らない");
            doha.AnimateRevive();
            await Wait(0.5);
            CheckPose("doha");

            var ending = field.ShowSharePower(doha, ally, cue, speed);
            field.EndShockMarkPresentation();
            Require(doha.MovementPortrait is null, "途中終了で差分を即時解除");
            await ending;
            await Wait(0.05);
            Require(field.ActiveDohaShares == 0 && audio.GetChildren().OfType<AudioStreamPlayer>().All(v => !v.Playing),
                "中断で帯・煙・音を片付ける");
            Require(doha.MovementPortrait is null, "終了後の遅延通知も無効");

            field.BeginBattle(opening, "再戦前", 0);
            var previous = field.ShowSharePower(field.FindPawn(1), field.FindPawn(2), cue, speed);
            field.BeginBattle(opening, "再戦", 0);
            await previous;
            Require(field.SharePowerPlays == 0 && field.FindPawn(1)!.MovementPortrait is null,
                "再戦へ差分と通知を持ち越さない");
            // 購入素材が未導入でも同じ往復・SE・表示を完走する。
            field.DohaPurchasedSmokeEnabled = false;
            await field.ShowSharePower(field.FindPawn(1), field.FindPawn(2), cue, speed);
            await Wait(0.65 / speed);
            for (int i = 0; i < 20 && field.ActiveDohaShares > 0; i++) await Wait(0.05);
            Require(field.ActiveDohaShares == 0 && Sounds(ShockMarkSound.DohaGive) == 1, "購入素材なしでも完走");
            field.DohaPurchasedSmokeEnabled = true;
            if (team == 0)
            {
                var winner = field.FindPawn(1)!;
                winner.BeginSharePain(false);
                winner.AnimateVictory();
                await Wait(0.7);
                Require(winner.MovementPortrait is null && winner.GetChildren().OfType<Sprite3D>().Single().Texture
                    == UiKit.Portrait(atlas, "doha"), "勝利は通常立ち絵");
            }
            GD.Print($"DOHA_PORTRAIT_OK team={team} speed={speed} idle receive give audio pause repeat death revive end replay fallback");
        }
        field.QueueFree(); await Wait(0.2);
    }
}
