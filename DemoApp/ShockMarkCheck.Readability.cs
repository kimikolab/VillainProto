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
            string[] enemies = ["warden", "warden", "yoker", "knight", "knight"];
            for (int slot = 0; slot < 5; slot++)
            {
                opening.Add(new(slot + 1, side, enemies[slot], "標の敵", slot, 60, 145, 12, AttackPattern.Single, false));
                opening.Add(new(slot + 6, 1 - side, friends[slot], "味方", slot, 60, 100, 12, AttackPattern.Single, false));
            }
            field.BeginBattle(opening, "標の視認性・密集した重装兵", stage);
            int[] layers = [9, 10, 10, 32, 90];
            for (int id = 1; id <= 5; id++)
            {
                var pawn = field.FindPawn(id)!;
                pawn.ApplyScar(100, 60);
                pawn.SetMarkLayers(layers[id - 1]);
                pawn.SetStatusIcon(StatusKeys.Marked, true);
                Require(pawn.Hud.MarkBadgeVisible && pawn.Hud.MarkBadgeText == $"標 {layers[id - 1]}層", "深い層も実数で表示");
            }
            field.FindPawn(6)!.SetMarkLayers(1);
            field.FindPawn(8)!.SetMarkLayers(1);
            await Wait(0.6);
            await Capture($"mark-readable-stage{stage}-side{side}");
            var marked = field.FindPawn(1)!;
            var aura = marked.GetChildren().OfType<ShockMarkAura3D>().Single();
            foreach (int amount in new[] { 1, 2, 3, 6, 7, 10, 90, 3 })
            {
                marked.SetMarkLayers(amount);
                Require(aura.GetChildren().OfType<Sprite3D>().Count(s => s.Visible) == System.Math.Min(amount, 7),
                    "層に応じて複数照準を増やし、深い層でも7個まで。減少時は余分な照準を消す");
            }
            marked.SetMarkLayers(0);
            Require(!marked.Hud.MarkBadgeVisible, "標の消失で札を消す");
            Require(!aura.GetChildren().OfType<Sprite3D>().Any(s => s.Visible), "標の消失で全照準を消す");
            marked.SetMarkLayers(10);
            field.Hide();
            Require(!marked.Hud.MarkBadgeVisible, "終了時に札を残さない");
            field.Show();
            GD.Print($"MARK_READABILITY_OK stage={stage} side={side}");
        }
        field.QueueFree(); await Wait(0.2);
    }
}
