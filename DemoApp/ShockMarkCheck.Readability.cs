using BattleCore;
using Godot;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    private async Task CheckMarkReadability()
    {
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        foreach (int stage in new[] { 3, 0 })
        foreach (int side in new[] { 1, 0 })
        {
            var opening = new List<DemoOpening>();
            string[] friends = ["sora", "zan", "doha", "tome", "hisa"];
            string[] enemies = stage == 0 ? ["levy", "levy", "levy", "levy", "levy"]
                : ["warden", "warden", "yoker", "knight", "knight"];
            for (int slot = 0; slot < 5; slot++)
            {
                opening.Add(new(slot + 1, side, enemies[slot], "標の敵", slot, 60, 145, 12, AttackPattern.Single, false));
                opening.Add(new(slot + 6, 1 - side, friends[slot], "味方", slot, 60, 100, 12, AttackPattern.Single, false));
            }
            field.BeginBattle(opening, "標の視認性・密集した重装兵", stage);
            int[] layers = stage == 0 ? [3, 3, 1, 3, 3] : [9, 10, 10, 32, 90];
            for (int id = 1; id <= 5; id++)
            {
                var pawn = field.FindPawn(id)!;
                pawn.ApplyScar(100, 60);
                pawn.SetMarkLayers(layers[id - 1]);
                pawn.SetStatusIcon(StatusKeys.Marked, true);
                Require(pawn.Hud.MarkBadgeVisible && pawn.Hud.MarkBadgeText == $"標 {layers[id - 1]}層", "深い層も実数で表示");
            }
            field.FindPawn(6)!.SetMarkLayers(1);
            var doha = field.FindPawn(8)!;
            doha.SetStatusEffects(1, 0, 0);
            doha.SetStatusIcon(StatusKeys.Marked, true);
            Require(doha.GetChildren().OfType<StatusEffects3D>().Single().MarkTexture
                == ShockMarkFx.MarkTexture(doha.Team), "単一標も味方／敵に応じた縁取り図形");
            await Wait(0.6);
            await Capture($"mark-readable-stage{stage}-side{side}");
            var marked = field.FindPawn(1)!;
            foreach (double speed in new[] { 1.0, 2.0 })
            {
                int before = marked.MarkLockPlays;
                int next = marked.MarkLayers + 1;
                field.ShowMarkLayer(marked, next, speed);
                Require(marked.MarkLockActive && field.FindPawn(2)!.MarkLockPlays == 0, "新しく付いた対象だけロックオン");
                var lockFrame = marked.GetChildren().OfType<MarkLockOn3D>().Single().GetChildren().OfType<Sprite3D>().Single();
                Require(lockFrame.Scale.X > 1.5f && lockFrame.NoDepthTest, "大きな枠が服や鎧に隠れず収束を始める");
                await Wait(.04);
                GetTree().Paused = true;
                float stopped = lockFrame.Scale.X;
                try {
                    await Capture($"mark-lock-stage{stage}-side{side}-speed{speed}");
                    await Wait(.12);
                    Require(lockFrame.Scale.X == stopped, "一時停止中は枠の収束も停止");
                }
                finally { GetTree().Paused = false; }
                field.ShowMarkLayer(marked, next, speed);
                field.ShowMarkLayer(marked, next - 1, speed);
                Require(marked.MarkLockPlays == before + 1, "同値・減少は再ロックしない");
                await Wait(.8);
                Require(!marked.MarkLockActive, "短い明滅の後は常駐標だけに戻る");
            }
            // 層のない通常付与も同じ入口で光らせ、連続付与でも枠を増殖させない。
            field.PlayMarkAdded(doha);
            field.PlayMarkAdded(doha);
            Require(doha.MarkLockActive && doha.GetChildren().OfType<MarkLockOn3D>().Count() == 1,
                "単一標の初回付与と再付与は枠を再利用");
            doha.SetStatusIcon(StatusKeys.Marked, false); doha.SetStatusEffects(0, 0, 0);
            Require(!doha.MarkLockActive, "単一標の除去で点滅停止");
            doha.SetStatusEffects(1, 0, 0); doha.SetStatusIcon(StatusKeys.Marked, true);
            field.PlayMarkAdded(doha);
            doha.AnimateDeath();
            Require(!doha.MarkLockActive, "死亡で付与直後の枠も即時停止");
            var aura = marked.GetChildren().OfType<ShockMarkAura3D>().Single();
            foreach (int amount in new[] { 1, 2, 3, 6, 7, 10, 90, 3 })
            {
                marked.SetMarkLayers(amount);
                Require(aura.GetChildren().OfType<Sprite3D>().Count(s => s.Visible) == System.Math.Min(amount, 7),
                    "層に応じて複数照準を増やし、深い層でも7個まで。減少時は余分な照準を消す");
            }
            field.ShowMarkLayer(marked, 4, 1);
            marked.SetMarkLayers(0);
            Require(!marked.MarkLockActive, "層の除去で点滅停止");
            Require(!marked.Hud.MarkBadgeVisible, "標の消失で札を消す");
            Require(!aura.GetChildren().OfType<Sprite3D>().Any(s => s.Visible), "標の消失で全照準を消す");
            field.ShowMarkLayer(marked, 10, 1);
            field.Hide();
            Require(!marked.MarkLockActive, "画面離脱で点滅停止");
            Require(!marked.Hud.MarkBadgeVisible, "終了時に札を残さない");
            field.Show();
            field.BeginBattle(opening, "標・再戦", stage);
            Require(field.Pawns.Values.All(p => !p.MarkLockActive && p.MarkLockPlays == 0),
                "再戦へ標の枠と再生予約を持ち越さない");
            GD.Print($"MARK_READABILITY_OK stage={stage} side={side}");
        }
        field.QueueFree(); await Wait(0.2);
    }
}
