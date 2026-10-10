using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    private async Task CheckDohaBatches()
    {
        BattleEvent Share(int target, int amount, int actor = 1) => new() {
            Kind = BattleEventKind.ShareGive, Turn = 1, ActorId = actor, TargetId = target,
            Amount = amount, Text = ShareGiveLabels.Power };
        BattleEvent Hit(int actor = 9) => new() {
            Kind = BattleEventKind.Damage, Turn = 1, ActorId = actor, TargetId = 1, Relayed = true };
        var cues = new[] { Share(2, 1), Share(2, 2), Share(3, 4) };
        BattleEvent[] events = [Hit(), cues[0], Hit(), cues[1], Hit(), cues[2],
            new() { Kind = BattleEventKind.Attack, Turn = 1, ActorId = 9 }, Hit(), Share(2, 5),
            new() { Kind = BattleEventKind.Death, Turn = 1, TargetId = 2 }, Hit(), Share(3, 6),
            Hit(8), Share(3, 7), new() { Kind = BattleEventKind.TurnStart, Turn = 2 }];
        var plan = DohaSharePresentation.Build(events);
        Require(plan.Ends.Count == 4 && plan.Ends[5].Count == 3 && plan.Members.Count == 6,
            "同じ加害者の肩代わりだけを束ね、次の攻撃・死亡・加害者変更で分ける");
        Require(plan.FastEvents.SetEquals(new[] { 1, 2, 3, 4, 5 }), "束の外の待ち時間は縮めない");
        Require(plan.Ends.Values.SelectMany(g => g).Sum(e => e.Amount) == 25,
            "全通知と強化量を過不足なく保持");

        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        var audio = (BattleAttackAudio)typeof(BattlefieldView3D).GetField("_attackAudio", Flags)!.GetValue(field)!;
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            field.BeginBattle(new DemoOpening[] {
                new(1, team, "doha", "分かちのドハ", 2, 80, 100, 10, AttackPattern.Single, false),
                new(2, team, "golm", "大喰らいゴルム", 0, 80, 100, 10, AttackPattern.Single, false),
                new(3, team, "hisa", "囃し立てのヒサ", 4, 80, 100, 10, AttackPattern.Single, false),
                new(9, 1 - team, "knight", "標的", 0, 80, 100, 10, AttackPattern.All, false),
            }, "ドハ・範囲攻撃の痛みをまとめる", 0);
            foreach (var pawn in field.Pawns.Values) pawn.AnimationSpeed = speed;
            await Wait(.12);
            var task = field.ShowSharePowers(cues, speed);
            Require(field.ActiveDohaShares == 2, "同じ相手の3点は一本、別の相手には別の線");
            await Wait(DohaShareFx.TravelSeconds(speed) * .45);
            GetTree().Paused = true;
            try { await Capture($"doha-batch-team{team}-speed{speed}-receive"); }
            finally { GetTree().Paused = false; }
            await task;
            Require(audio.ShockAssetPlays.TryGetValue(ShockMarkSound.DohaPain, out int pain) && pain == 1
                && audio.ShockAssetPlays.TryGetValue(ShockMarkSound.DohaGive, out int give) && give == 1,
                "対象が複数でも受け取り音と返す音は各一度");
            Require(field.SharePowerPlays == 3 && field.SharePowerBatches == 1
                && field.SharePowerLinks == 2 && field.SharePowerAmount == 7, "3通知を1往復・2対象・合計7に圧縮");
            await Wait(.16 / speed);
            GetTree().Paused = true;
            try { await Capture($"doha-batch-team{team}-speed{speed}-give"); }
            finally { GetTree().Paused = false; }
            await Wait(.6 / speed);
            Require(field.ActiveDohaShares == 0 && field.Pawns.Values.All(p => p.Hp == 80), "束を片付け、HPは変えない");
            task = field.ShowSharePowers(cues, speed);
            field.EndShockMarkPresentation(); await task; await Wait(.05);
            Require(field.ActiveDohaShares == 0 && field.FindPawn(1)!.MovementPortrait is null,
                "複数の線も中断で全て消去");
            GD.Print($"DOHA_BATCH_OK team={team} speed={speed} cues=3 batches=1 links=2 amount=7");
        }
        field.QueueFree(); await Wait(.1);
    }
}
