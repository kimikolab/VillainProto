using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class ShockMarkCheck
{
    private async Task CheckWebVisuals()
    {
        BattleEvent E(BattleEventKind kind, int? actor = null, int? target = null, int depth = 0,
            int? team = null, string? text = null) => new() {
                Turn = 1, Kind = kind, ActorId = actor, TargetId = target, Slot = depth, Team = team, Text = text };
        BattleEvent[] script = [
            E(BattleEventKind.ShockSpent, target: 10, team: 0),
            E(BattleEventKind.ShockSpent, target: 20, depth: 1, team: 1),
            E(BattleEventKind.ShockSpent, target: 21, depth: 2, team: 1),
            E(BattleEventKind.SilkBall, target: 21, team: 1, text: SilkBallLabels.Pop),
            E(BattleEventKind.ShockGauge, actor: 2, text: ShockGaugeLabels.Interrupt),
            E(BattleEventKind.ShockGauge, actor: 2, team: 1, text: ShockGaugeLabels.WhipChain),
            E(BattleEventKind.ShockSpent, target: 30, team: 1),
            E(BattleEventKind.ShockGauge, actor: 2, text: ShockGaugeLabels.ChargeSpent),
            E(BattleEventKind.ShockGauge, actor: 3, text: ShockGaugeLabels.Interrupt),
            E(BattleEventKind.ShockGauge, actor: 3, team: 1, text: ShockGaugeLabels.WhipChain) ];
        Require(ShockWebPresentation.ChainSources(script, 5, 2).SequenceEqual(new[] { 20, 21 }), "味方の弾けを除き、糸玉を二重に数えない");
        Require(ShockWebPresentation.ChainSources(script, 9, 2).SequenceEqual(new[] { 20, 21 }), "別のシガも割り込み前の連鎖を読む");
        var field = new BattlefieldView3D(); field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(field);
        foreach (int team in new[] { 0, 1 })
        foreach (double speed in new[] { 1.0, 2.0 })
        {
            DemoOpening O(int id, int side, UnitDef def, int slot) => new(id, side, def.Id, def.Name, slot,
                100, 100, 10, AttackPattern.Sweep, true, def.Traits);
            var opening = new[] { O(1, team, UnitCatalog.Kugu, 2), O(2, team, UnitCatalog.Shiga, 0),
                O(3, 1-team, UnitCatalog.Golm, 0), O(4, 1-team, UnitCatalog.Sora, 1),
                O(5, 1-team, UnitCatalog.Hisa, 2), O(6, 1-team, UnitCatalog.Gald, 3), O(7, 1-team, UnitCatalog.Tou, 4) };
            field.BeginBattle(opening, "クグの網と連鎖の鞭", 0);
            foreach (var p in field.Pawns.Values) p.AnimationSpeed = speed;
            var kugu = field.FindPawn(1)!; var shiga = field.FindPawn(2)!; var hub = field.FindPawn(3)!;
            await Wait(0.2);
            field.SetBinding(kugu, hub, true);
            foreach (int id in new[] { 4, 5, 6, 7 })
            {
                field.ShowWeb(new() { Turn = 1, Kind = BattleEventKind.Web, Text = WebLabels.Spin,
                    ActorId = 1, TargetId = id, PartnerId = 3 }, speed);
                await Wait(0.26 / speed);
            }
            await Capture($"web-team{team}-speed{speed}-spread");
            field.SetBinding(kugu, hub, false);
            Require(field.ActiveWebCount == 4, "拘束が解けても網は残る");
            kugu.SetHp(0); field.CutWebFor(kugu);
            foreach (int id in new[] { 4, 5, 6, 7 }) field.ShowWeb(new() { Turn = 2, Kind = BattleEventKind.Web,
                Text = WebLabels.Recharge, ActorId = 1, TargetId = id }, speed);
            await Wait(0.13 / speed); await Capture($"web-team{team}-speed{speed}-recharge");
            Require(field.ActiveWebCount == 4, "書き手が倒れても網の再帯電を表示");
            field.CutWebFor(field.FindPawn(4));
            await Wait(0.1 / speed); await Capture($"web-team{team}-speed{speed}-cut");
            Require(field.ActiveWebCount == 3, "倒れた的の糸だけを切る");
            field.ShowSilkBall(new() { Turn = 2, Kind = BattleEventKind.SilkBall, Text = SilkBallLabels.Place,
                ActorId = 1, TargetId = 20, PartnerId = 3, Team = 1-team, Slot = 5 }, speed);
            field.ShowWeb(new() { Turn = 2, Kind = BattleEventKind.Web, Text = WebLabels.Ball, ActorId = 1, TargetId = 20 }, speed);
            Require(field.SilkBallCount == 1 && field.ActiveWebCount == 4 && field.FindPawn(20) is null, "糸玉を駒にせず一度だけ張る");
            await Wait(0.25 / speed); await Capture($"web-team{team}-speed{speed}-ball");
            Require(field.ShowSilkBallDischarge(3, 20, speed) && field.ShowSilkBallDischarge(20, 3, speed), "糸玉の往復");
            field.ShowSilkBall(new() { Turn = 2, Kind = BattleEventKind.SilkBall, Text = SilkBallLabels.Pop, TargetId = 20 }, speed);
            Require(field.SilkBallCount == 1 && field.ChargedSilkBallCount == 0, "弾けても糸玉は残る");
            field.ShowSilkBall(new() { Turn = 3, Kind = BattleEventKind.SilkBall, Text = SilkBallLabels.Recharge, Amount = 1 }, speed);
            Require(field.ChargedSilkBallCount == 1, "対象番号のない再帯電");
            foreach (int count in new[] { 1, 3, 5 })
            {
                field.ShowInterrupt(shiga, hub, speed);
                int[] sources = new[] { 3, 4, 5, 6, 20 }.Take(count).ToArray();
                field.ShowWhipChain(shiga, count, sources, speed);
                await Wait(0.16 / speed); await Capture($"web-team{team}-speed{speed}-gather{count}");
                Vector3 home = shiga.Position;
                var attack = field.Attack(shiga, hub, AttackPattern.Sweep, new[] { hub, field.FindPawn(5)!, field.FindPawn(6)! }, reaction: true);
                for (int i = 0; i < 4; i++)
                {
                    await Wait(0.1 / speed);
                    Require(shiga.Position.IsEqualApprox(home), "割り込み中に踏み込まない");
                    if (i == 2) await Capture($"web-team{team}-speed{speed}-whip{count}");
                }
                await attack;
                field.ShowCharge(shiga, 0, false, speed);
                Require(shiga.WhipChainSize == 0, "倍率を次の通常攻撃へ持ち越さない");
            }
            Require(field.WhipWhiteFlashes == 1, "4連鎖以上だけ一振りに一度の白閃光");
            Vector3 rest = shiga.Position;
            var normal = field.Attack(shiga, hub, AttackPattern.Single, new[] { hub });
            await Wait(0.3 / speed);
            Require(shiga.Position.IsEqualApprox(rest), "通常攻撃も据え置き");
            await normal;
            Require(field.Pawns.Values.Where(p => p != kugu).All(p => p.Hp == 100), "演出はHPを変えない");
            field.EndShockMarkPresentation();
            Require(field.ActiveWebCount == 0 && field.SilkBallCount == 0, "終了の消去");
            field.BeginBattle(opening, "再戦", 0);
            Require(field.ActiveWebCount == 0 && field.SilkBallCount == 0 && field.WhipChainPlays == 0, "再戦で状態を初期化");
            GD.Print($"SHOCK_WEB_VISUAL_OK team={team} speed={speed}");
        }
        field.QueueFree(); await Wait(0.2);
    }
}
