using BattleCore;
using Godot;
using System.Linq;
using System.Threading.Tasks;

public partial class FireCheck
{
    private async Task CheckPurchasedAssets()
    {
        Require(PurchasedAssets.EffectsAvailable && PurchasedAssets.NatureAvailable, "購入素材が配置済み");
        var field = new BattlefieldView3D();
        field.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(field);
        DemoOpening[] units = {
            new(1,0,"hota","ホタ",0,100,100,20,AttackPattern.Single,false),
            new(2,1,"knight","標的1",0,100,100,20,AttackPattern.Single,true),
            new(3,1,"knight","標的2",2,100,100,20,AttackPattern.Single,true),
        };
        foreach (int stage in new[] { 0, 3, 0 })
        {
            field.BeginBattle(units, "購入素材・本編接続確認", stage);
            var scenery = (Node3D)typeof(BattlefieldView3D).GetField("_scenery", Flags)!.GetValue(field)!;
            Require(stage == 3 ? scenery is FortressEnvironment3D : scenery is NatureMeadowPreview, "草原と城塞の切り替え");
            var actor = field.FindPawn(1)!;
            var target = field.FindPawn(2)!;
            actor.SetFireLevel(2);
            target.SetFireLevel(2);
            Require(!actor.GetChildren().Any(n => n is AssetBurnPreview), "味方の炎は従来どおり");
            Require(target.GetChildren().Count(n => n is AssetBurnPreview) == 1, "敵の燃焼だけ購入素材");
            target.SetFireLevel(3);
            Require(target.GetChildren().Count(n => n is AssetBurnPreview) == 1, "燃焼更新で重複しない");
            await Wait(0.8);
            await Capture("assets-stage-" + stage);
            target.SetFireLevel(0);
            Require(!target.GetChildren().Any(n => n is AssetBurnPreview), "燃焼解除で炎を削除");
            target.SetFireLevel(2);
            target.AnimateDeath();
            Require(!target.GetChildren().Any(n => n is AssetBurnPreview), "死亡で炎を削除");
            target.AnimateRevive();
            target.SetFireLevel(2);
            Require(target.GetChildren().Count(n => n is AssetBurnPreview) == 1, "蘇生後の燃焼を再表示");
            actor.AnimateVictory();
            Require(actor.FireLevel == 0, "味方は勝利で炎を解除");
        }
        field.BeginBattle(units, "本編の火球・火槍", 0);
        int contacts = 0;
        field.AttackContact = _ => contacts++;
        var hota = field.FindPawn(1)!;
        var hits = new[] { field.FindPawn(2)!, field.FindPawn(3)! };
        for (int ordinal = 1; ordinal <= 5; ordinal++)
            await field.PlayFireAttack(hota, hits[0], new[] { hits[0] }, new(FireLevelLabels.EmbersHit, 1, ordinal), 1);
        Require(contacts == 5 && field.AssetPreviewFireballImpacts == 5, "本編の既定で火球5発・爆炎5回");
        foreach (int level in new[] { 2, 3 })
            await field.PlayFireAttack(hota, hits[0], hits, new(FireLevelLabels.Stage, level, 0), 1);
        Require(contacts == 9 && field.AssetPreviewPierceImpacts == 4, "本編の貫通で各敵へ1回の命中と爆炎");
        await Wait(1.3);
        Require(field.AssetPreviewEffects.GetChildCount() == 0, "本編でも粒子が残らない");
        Require(field.Pawns.Values.All(p => p.Hp == 100), "演出でHPを変更しない");
        field.QueueFree();
        await Wait(0.1);
        GD.Print("PURCHASED_ASSETS_CHECK_OK");
    }
}
