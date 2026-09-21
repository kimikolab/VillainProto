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
            if (mode == "charge")
            {
                // ドルガの「のろま」は Charge ではない。実際に溜める詠唱兵の姿で確認する。
                UnitDef caster = EnemyCatalog.Chanter;
                openings[0] = new DemoOpening(1, BattleContext.PlayerTeam, caster.Id, caster.Name,
                    0, caster.MaxHp, caster.MaxHp, caster.Attack, caster.Pattern, caster.Advances, caster.Traits);
            }
            field.BeginBattle(openings, "演出確認", 1);
            var target = field.FindPawn(1)!;
            var healer = field.FindPawn(2)!;
            var holder = field.FindPawn(3)!;
            target.AnimationSpeed = healer.AnimationSpeed = holder.AnimationSpeed = 2;
            await Wait(0.3);
            if (mode == "camera")
            {
                await Capture("camera-default");
                field.SetCameraElevation(0);
                await Wait(0.2);
                await Capture("camera-side");
                field.SetCameraYaw(-35);
                field.SetCameraZoom(1.3f);
                await Wait(0.2);
                await Capture("camera-left-near");
                field.SetCameraYaw(35);
                field.SetCameraZoom(0.75f);
                await Wait(0.2);
                await Capture("camera-right-far");
                var attack = field.Attack(target, holder, AttackPattern.Single, new[] { holder });
                await Wait(0.12);
                field.SetCameraElevation(10);
                await attack;
                await Wait(0.4);
                await Capture("camera-low-after-attack");
                field.BeginBattle(openings, "演出確認", 1);
                Require(field.CameraElevation == 10, "再開後も視点を保持");
                Require(field.CameraYaw == 35 && field.CameraZoom == 0.75f, "再開後も左右と距離を保持");
                field.ResetCameraView();
                Require(field.CameraYaw == 0 && field.CameraZoom == 1, "元の視点へ一括復帰");
                field.SetCameraElevation(50);
                await Wait(0.2);
                await Capture("camera-high");
                target = field.FindPawn(1)!;
                healer = field.FindPawn(2)!;
                holder = field.FindPawn(3)!;
            }
            if (mode == "turn")
            {
                field.SetTurn(1);
                await Wait(0.25);
                Require(field.TurnLabelVisible && field.TurnLabelText == "TURN  01", "ターン開始表示");
                await Capture("turn-01");
                await Wait(1.1);
                Require(!field.TurnLabelVisible, "ラベルが消える");
                field.SetTurn(2);
                await Wait(0.08);
                field.SetTurn(3);
                await Wait(0.25);
                Require(field.TurnLabelVisible && field.TurnLabelText == "TURN  03", "速い更新で最新ターンを表示");
                await Capture("turn-03");
                field.BeginBattle(openings, "演出確認", 1);
                Require(!field.TurnLabelVisible, "再開で古いラベルを破棄");
                field.SetTurn(1);
                await Wait(0.3);
                Require(field.TurnLabelVisible, "再開後の第1ターン");
                target = field.FindPawn(1)!;
                healer = field.FindPawn(2)!;
                holder = field.FindPawn(3)!;
            }
            if (mode is "all" or "status")
            {
                Require(StatusKeys.All.Where(k => k != StatusKeys.IdleTurn).All(k => StatusIconArt.KeyOf(k) is not null), "状態一覧の網羅");
                string[] common = { StatusKeys.Poison, StatusKeys.Burn, StatusKeys.Stun, StatusKeys.Stagger,
                    StatusKeys.Confused, StatusKeys.Wound, StatusKeys.Marked, StatusKeys.Armor };
                foreach (string key in common) target.SetStatusIcon(key, true);
                Require(target.StatusIconCount == 8, "8状態の同時表示");
                await Wait(0.45);
                await Capture("status-eight");
                target.BeginStatusSnapshot();
                foreach (string key in common) target.ReadStatusSnapshot(StatusKeys.LabelOf(key), 2);
                target.ReadStatusSnapshot(StatusKeys.LabelOf(StatusKeys.IdleTurn), 1);
                target.CommitStatusSnapshot();
                Require(target.StatusIconCount == 8 && target.StatusIconFlashCount == 0, "同じ写しでは点滅し直さない");
                target.BeginStatusSnapshot();
                target.ReadStatusSnapshot(StatusKeys.LabelOf(StatusKeys.Poison), 3);
                target.CommitStatusSnapshot();
                Require(target.StatusIconCount == 1 && target.HasStatusIcon(StatusKeys.Poison), "写しにない状態を解除");
                await Wait(0.1);
                await Capture("status-clear");
                await Wait(0.4);
                target.SetStatusIcon(StatusKeys.Stagger, true);
                target.SetStatusIcon(StatusKeys.Stagger, false);
                Require(!target.HasStatusIcon(StatusKeys.Stagger), "転倒の即時消費");
                foreach (string key in StatusIconArt.Keys) target.SetStatusIcon(key, true);
                await Wait(0.4);
                await Capture("status-all");
                target.AnimateDeath();
                Require(target.StatusIconCount == 0, "死亡でアイコンを破棄");
                target.AnimateRevive();
                Require(target.StatusIconCount == 0, "蘇生で古い状態を復元しない");
                await Wait(0.5);
            }
            if (mode is "all" or "life")
            {
                target.AnimateDeath();
                await Wait(0.28);
                await Capture("life-death");
                await Wait(0.7);
                target.AnimateRevive();
                await Wait(0.25);
                await Capture("life-revive");
                await Wait(0.7);
                field.AddSummon(new DemoOpening(99, 0, "nara", "召喚確認", 4, 20, 20, 5, AttackPattern.Single, false));
                await Wait(0.3);
                await Capture("life-summon");
                var summoned = field.FindPawn(99)!;
                summoned.AnimateDeath();
                summoned.AnimateRevive();
                await Wait(1);
                Require(!summoned.GetChildren().OfType<LifeTransition3D>().Any(), "出現・死亡・蘇生の連続後に残らない");
            }
            if (mode is "all" or "slash")
            {
                await field.Attack(target, holder, AttackPattern.Single, new[] { holder });
                await Wait(0.10);
                await Capture("single-slash");
                await Wait(0.6);
                await field.Attack(holder, target, AttackPattern.Single, new[] { target });
                await Wait(0.10);
                await Capture("single-slash-return");
                await Wait(0.6);
            }
            if (mode is "all" or "charge")
            {
                target.BeginCharge(250);
                await Wait(0.4);
                await Capture("charge-start");
                field.SetTurn(2);
                target.BeginCharge(300);
                Require(target.GetChildren().OfType<ChargeAura3D>().Count() == 1, "再度の溜めは重ねない");
                await Wait(0.7);
                Require(target.IsCharging, "ターンをまたいで溜めを維持");
                await Capture("charge-hold");
                if (mode == "charge") await Wait(2.0);
                await field.Attack(target, holder, AttackPattern.Single, new[] { holder }, reaction: true);
                Require(target.IsCharging, "手番外では溜めを解放しない");
                await field.Attack(target, holder, AttackPattern.All, new[] { holder });
                Require(!target.IsCharging, "本来の攻撃で解放");
                await Wait(0.12);
                await Capture("charge-release");
                await Wait(0.5);
                Require(!target.GetChildren().OfType<ChargeAura3D>().Any(), "解放後の片付け");
                target.BeginCharge(200);
                target.AnimateDeath();
                Require(!target.IsCharging, "死亡で溜めを解除");
                target.AnimateRevive();
                Require(!target.IsCharging, "蘇生で古い予兆が復活しない");
                await Wait(0.5);
                target.BeginCharge(200);
            }
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
            Require(field.Pawns.Values.All(p => !p.IsCharging), "再開で溜めを破棄");
            Require(field.Pawns.Values.All(p => p.StatusIconCount == 0), "再開で状態を破棄");
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
