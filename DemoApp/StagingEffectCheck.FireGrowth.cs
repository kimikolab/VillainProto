using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class StagingEffectCheck
{
    private async Task CheckFireGrowth(BattlefieldView3D field)
    {
        DemoOpening[] openings = [
            new(1, 0, "borg", "ボルグ", 0, 100, 100, 20, AttackPattern.Sweep, true),
            new(2, 0, "hota", "ホタ", 2, 100, 100, 20, AttackPattern.Single, false),
            new(3, 0, "hiyo", "ヒヨ", 4, 100, 100, 20, AttackPattern.Single, false),
            new(4, 1, "knight", "敵A", 0, 300, 300, 20, AttackPattern.Single, true),
            new(5, 1, "knight", "敵B", 2, 300, 300, 20, AttackPattern.Single, true),
            new(6, 1, "knight", "敵C", 4, 300, 300, 20, AttackPattern.Single, true)];
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            field.BeginBattle(openings, "守りから育ち、ヒヨ→ボルグ→ホタ", 3);
            foreach (var p in field.Pawns.Values) { p.AnimationSpeed = speed; p.SetFireLevel(1); }
            var borg = field.FindPawn(1)!; var hota = field.FindPawn(2)!; var hiyo = field.FindPawn(3)!;
            var foes = new[] { field.FindPawn(4)!, field.FindPawn(5)!, field.FindPawn(6)! };
            async Task Cue(string label, int target, int amount = 0, int slot = 0, int actor = 1)
            {
                var e = new BattleEvent { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = actor,
                    TargetId = target, Text = label, Amount = amount, Slot = slot };
                double seconds = field.ShowFireCue(e, field.FindPawn(actor), field.FindPawn(target), speed);
                await Wait(Math.Max(0.001, seconds / speed));
            }
            await Cue(FireLevelLabels.KindleOpen, 1, 2, 1);
            await Cue(FireLevelLabels.KindleGuard, 1, 2, 0);
            await Cue(FireLevelLabels.GrowGuard, 1, 3, 2);
            Require(borg.FireLevel == 3, "開幕・守りの実際の火勢を表示");
            await Wait(0.18 / speed); await Capture($"fire-growth-guard-speed{speed}");
            await Cue(FireLevelLabels.GrowGuard, 1, 4, 3);
            for (int n = 1; n <= 3; n++)
            {
                await Cue(FireLevelLabels.Hoard, 1, 1, n);
                Require(borg.FireHoard == n && borg.FireLevel == 4, "溜め火は火勢を変えず台本の数を写す");
                await Wait(0.22 / speed); await Capture($"fire-growth-sword-{n}-speed{speed}");
            }
            for (int n = 1; n <= 3; n++) await Cue(FireLevelLabels.GiftHoardAdd, 3, 1, n, 3);
            await Cue(FireLevelLabels.GiftOrder, 1, 4, 1, 3);
            await Cue(FireLevelLabels.Gift, 1, 4, 1, 3);
            await Cue(FireLevelLabels.GiftHoard, 1, 4, 4, 3);
            await Cue(FireLevelLabels.Gift, 2, 1, 2, 3);
            await Cue(FireLevelLabels.GiftHoard, 2, 4, 1, 3);
            Require(borg.FireGiftOrder == 1 && hota.FireGiftOrder == 2 && hota.FireLevel == 4,
                "Slotの順を表示し、渡す火の色は手番前に上がる");
            await Capture($"fire-gift-order-speed{speed}");
            await Cue(FireLevelLabels.GiftTurn, 1, 4, 1, 3);
            await Cue(FireLevelLabels.HoardRelease, 1, 3, 450);
            Require(borg.FireHoard == 0 && borg.FireGiftOrder == 0 && hota.FireGiftOrder == 2,
                "ボルグだけ追加手番を開始して溜めを解放");
            var blaze = field.PlayFireAttack(borg, foes[0], foes, new(FireLevelLabels.Blaze, 4, 0, 3, 450), speed);
            await Wait(0.63 / speed); await Capture($"fire-gift-blaze-speed{speed}"); await blaze;
            await Cue(FireLevelLabels.Spent, 1, 1, 4);
            await Wait(0.5 / speed);
            await Cue(FireLevelLabels.GiftTurn, 2, 4, 2, 3);
            var burnout = field.PlayFireAttack(hota, foes[0], foes, new(FireLevelLabels.Burnout, 4, 0), speed);
            await Wait(1.02 / speed); await Capture($"fire-gift-burnout-speed{speed}"); await burnout;
            await Cue(FireLevelLabels.Spent, 2, 1, 4);
            Require(borg.FireLevel == 1 && hota.FireLevel == 1 && hota.FireGiftOrder == 0, "大技の後は台本で小さく戻る");
            await Wait(0.8 / speed);
        }
        BattleEvent[] script = [
            new() { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, Text = FireLevelLabels.Unleash, Amount = 4 },
            new() { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, Text = FireLevelLabels.HoardRelease, Amount = 7, Slot = 650 },
            new() { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, Text = FireLevelLabels.Blaze, Amount = 79 },
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, Amount = 999 },
            new() { Turn = 1, Kind = BattleEventKind.FireLevel, ActorId = 1, Text = FireLevelLabels.Blaze, Amount = 12 },
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, Amount = 321 }];
        var plan = new FirePresentation(script);
        Require(plan.Attacks[3].Stored == 7 && plan.Attacks[3].Percent == 650 && plan.Attacks[5].Stored == 0,
            "実在する解放量だけを次の爆炎へ結び、次発に持ち越さない");
        field.FindPawn(1)!.SetFireHoard(99);
        field.FindPawn(1)!.AnimateDeath();
        Require(field.FindPawn(1)!.FireHoard == 0, "死亡で剣の溜め火を残さない");
        field.FindPawn(3)!.SetFireGiftHoard(3);
        field.FindPawn(3)!.SetFireGiftOrder(2);
        field.FindPawn(3)!.AnimateDeath();
        Require(field.FindPawn(3)!.FireGiftHoard == 0 && field.FindPawn(3)!.FireGiftOrder == 0, "死亡で渡す火と待機手番を残さない");
        field.BeginBattle(openings, "再開", 3);
        Require(field.Pawns.Values.All(p => p.FireHoard == 0 && p.FireGiftHoard == 0 && p.FireGiftOrder == 0), "再開で溜めと手番を残さない");
        if (OS.GetCmdlineUserArgs().Contains("--verify")) await CheckFireTickReplay();
    }
}
