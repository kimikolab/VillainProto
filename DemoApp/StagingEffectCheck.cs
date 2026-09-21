using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 既存の演出と同じ公開口を呼ぶ。--effect= で一項目ずつ観察できる。
public partial class StagingEffectCheck : Control
{
    public override async void _Ready()
    {
        try
        {
            string mode = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--effect="))?[9..] ?? "all";
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(field);
            DemoOpening[] openings =
            [
                new(1, 0, "kado", "カド", 0, 100, 100, 20, AttackPattern.Single, false),
                new(2, 0, "nara", "ナラ", 3, 100, 100, 10, AttackPattern.Single, false),
                new(3, 1, "seal-check", "保持者", 2, 100, 100, 10, AttackPattern.Single, true,
                    [TraitId.Hush, TraitId.Drought, TraitId.Yoke]),
            ];
            field.BeginBattle(openings, "演出確認", 1);
            var target = field.FindPawn(1)!;
            var healer = field.FindPawn(2)!;
            var holder = field.FindPawn(3)!;
            target.AnimationSpeed = healer.AnimationSpeed = holder.AnimationSpeed = 2;
            await Wait(0.3);
            if (mode is "all" or "marks")
            {
                Require(holder.RuleMarkCount == 3, "複数の保持者印");
                await Capture("marks");
                holder.AnimateDeath();
                Require(!holder.RuleMarksVisible, "死亡で印が消える");
                await Wait(0.6);
                holder.AnimateRevive();
                Require(holder.RuleMarksVisible, "蘇生で印が戻る");
                await Wait(0.6);
            }
            if (mode is "all" or "popups")
            {
                for (int i = 0; i < 20; i++) field.DamagePopup(target, 3, "出どころ", UiKit.Hurt);
                Require(field.PopupCount == 1, "連続ダメージの合算");
                field.HealPopup(target, 12);
                Require(field.PopupCount == 2, "回復とダメージは別");
                await Capture("numbers");
                for (int i = 0; i < 20; i++) field.Float(target, "確認", UiKit.Gold);
                Require(field.PopupCount <= 2, "駒ごとの上限");
                await Wait(1.2);
                Require(field.PopupCount == 0, "寿命後に消える");
            }
            if (mode is "all" or "yoke")
            {
                field.ShowYokeSeal(target, 5);
                await Wait(0.12);
                await Capture("yoke-small");
                await Wait(0.6);
                field.ShowYokeSeal(target, 80);
                await Wait(0.12);
                await Capture("yoke-large");
                await Wait(0.6);
            }
            if (mode is "all" or "drought")
            {
                field.ShowDroughtSeal(target, 30);
                await Wait(0.16);
                await Capture("drought");
                await Wait(0.6);
            }
            if (mode is "all" or "heal")
            {
                field.HealingLight(healer, target, 30);
                Require(target.HealingDropsActive, "他者からの回復にしずく");
                field.HealPopup(target, 30);
                await Wait(0.12);
                await Capture("heal");
                await Wait(1.1);
                Require(!target.HealingDropsActive, "しずくが自然に消える");
                field.HealingLight(target, target, 15);
                Require(target.HealingDropsActive, "自己回復にしずく");
                await Wait(0.28);
                await Capture("heal-self");
                await Wait(0.20);
                await Capture("heal-absorb");
                await Wait(1.1);
                field.HealingLight(null, target, 15);
                Require(target.HealingDropsActive, "書き手なしの回復にしずく");
                await Wait(0.28);
                await Capture("heal-trait");
                await Wait(1.1);
            }
            if (mode is "all" or "stats")
            {
                target.SetAttack(40);
                Require(target.PowerMistActive, "強化オーラ");
                Require(target.AttackDeltaText == "+20" && target.AttackDeltaVisible, "基準からの強化");
                await Wait(0.28);
                await Capture("attack-up");
                await Wait(0.6);
                target.SetAttack(12);
                Require(target.PowerMistActive, "弱体オーラ");
                Require(target.AttackDeltaText == "-8" && target.AttackDeltaVisible, "基準からの弱体");
                await Wait(0.28);
                await Capture("attack-down");
                await Wait(0.6);
                target.SetAttack(20);
                Require(!target.AttackDeltaVisible, "増減ゼロは消す");
            }
            field.HealingLight(null, target, 20);
            target.SetAttack(30);
            target.AnimateDeath();
            Require(!target.HealingDropsActive && !target.PowerMistActive, "死亡でオーラを消す");
            target.AnimateRevive();
            field.HealingLight(null, target, 20);
            // 召喚で駒が増えても画面全体の上限は変えない。
            for (int id = 4; id <= 10; id++)
                field.AddSummon(openings[0] with { InstanceId = id, Slot = 4 });
            foreach (var pawn in field.Pawns.Values)
            {
                field.Float(pawn, "確認", UiKit.Gold);
                field.Float(pawn, "確認", UiKit.Gold);
            }
            Require(field.PopupCount == 8, "全体の上限");
            field.ShowYokeSeal(target, 80);
            field.ShowDroughtSeal(target, 30);
            field.BeginBattle(openings, "再開確認", 1);
            await Wait(0.8);
            Require(field.PopupCount == 0, "再開で表示を破棄");
            GD.Print("STAGING_EFFECT_CHECK_OK " + mode + " speed=2 restart death revive");
            GetTree().Quit();
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            GetTree().Quit(1);
        }
    }

    private async Task Capture(string phase)
    {
        string? directory = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="));
        if (directory is null) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(directory[14..] + "/staging-" + phase + ".png") == Error.Ok, "画像保存");
    }

    private async Task Wait(double seconds)
        => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
