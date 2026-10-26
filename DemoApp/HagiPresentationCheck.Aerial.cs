using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class HagiPresentationCheck
{
    private static void CheckAirContract()
    {
        var openings = new[] { Opening(1, UnitCatalog.Hagi, 0, 0), Opening(2, EnemyCatalog.Recruit, 1, 0),
            Opening(3, EnemyCatalog.Recruit, 1, 2) };
        BattleEvent E(BattleEventKind kind, int target = 2, int hp = 40, string text = "", int slot = 0,
            bool reaction = false, int amount = 40) => new() {
                Turn = 1, Kind = kind, ActorId = 1, TargetId = target, HpAfter = hp, Text = text,
                Slot = slot, Reaction = reaction, Amount = amount,
                StatusRemaining = text == ReapLabels.Pursue ? 0 : null };
        var events = new[] { E(BattleEventKind.Attack), E(BattleEventKind.Damage),
            E(BattleEventKind.Reap, text: ReapLabels.Reap, slot: ReapLabels.KindAfter),
            E(BattleEventKind.Damage, hp: 0) }.ToList();
        HagiPresentation Parse() => HagiPresentation.Build(events, openings, null);
        var p = Parse();
        Require(p.Swings[0].Air == HagiPresentation.AirKind.Opener && p.Returns[2].AirFollow,
            "初撃の通常命中→刈るは親から一度だけ跳ぶ");
        events.Add(E(BattleEventKind.Attack, target: 3)); events.Add(E(BattleEventKind.Damage, target: 3));
        events.Add(E(BattleEventKind.Reap, target: 3, text: ReapLabels.Reap, slot: ReapLabels.KindAfter));
        events.Add(E(BattleEventKind.Damage, target: 3, hp: 0));
        p = Parse();
        Require(p.Swings[4].Air == HagiPresentation.AirKind.Finisher && p.Returns[6].AirFollow,
            "手番の二撃目でも最後の敵を刈るときはフィニッシュ");
        events.Add(E(BattleEventKind.Revive, target: 3, hp: 60)); p = Parse();
        Require(p.Swings[4].Air == HagiPresentation.AirKind.None, "後で復活する敵は戦闘の止めではない");
        events.Add(E(BattleEventKind.Damage, target: 3, hp: 0, amount: 60)); p = Parse();
        Require(p.Swings[4].Air == HagiPresentation.AirKind.None, "最後の死亡が通常命中なら以前の刈りを止めにしない");
        events.RemoveRange(4, events.Count - 4);
        events.InsertRange(0, new[] { E(BattleEventKind.Attack, target: 3, reaction: true),
            E(BattleEventKind.Damage, target: 3, hp: 80) });
        p = Parse(); Require(p.Swings[2].Air == HagiPresentation.AirKind.Opener,
            "手番前の反撃で初撃の跳躍を消費しない");
        events.Insert(0, E(BattleEventKind.Reap, text: ReapLabels.Pursue));
        p = Parse(); Require(p.Swings[3].Air == HagiPresentation.AirKind.Opener,
            "手番前の追撃で初撃の跳躍を消費しない");
        events[1] = E(BattleEventKind.Attack, target: 3);
        p = Parse(); Require(p.Swings[3].Air == HagiPresentation.AirKind.Opener,
            "Reactionが無い追撃も手番の初撃に数えない");
        events.RemoveRange(0, 3);
        events.Insert(0, E(BattleEventKind.Reap, text: ReapLabels.Encore, amount: 1));
        p = Parse(); Require(p.Swings[1].Air == HagiPresentation.AirKind.None,
            "再行動中の小さな返し刃は跳躍に置き換えない");
        events.Add(new() { Turn = 1, Kind = BattleEventKind.Summon, TargetId = 4, Team = 1, HpAfter = 100 });
        p = Parse(); Require(p.Swings.Values.All(s => s.Air != HagiPresentation.AirKind.Finisher),
            "後で召喚される敵が残るときは止めにしない");
        GD.Print("HAGI_AIR_CONTRACT_OK opener lookahead reaction pursuit encore final revive summon");
    }

    private async Task CheckAirVisual(DemoOpening[] openings)
    {
        _field.BeginBattle(openings, "", 0); _field.SetDeathClock(false, 1);
        var hagi = _field.FindPawn(1)!; var enemy = _field.FindPawn(3)!;
        int contacts = 0;
        var root = new HagiPresentation.Swing(1, Array.Empty<int>(), false, false, false,
            Keep: true, NextReturn: true, Air: HagiPresentation.AirKind.Opener);
        var task = _field.PlayHagiSwing(hagi, new[] { enemy }, root, () => {
            Require(hagi.CurrentHagiAnchors?.Pose == "impact" && hagi.AttackClipVertical > .5f,
                "接触セルで先に斬り、足が着く前に命中する");
            CheckVerticalCut(_field.HagiReapEffects.Last());
            contacts++;
        });
        while (hagi.CurrentHagiAnchors?.Pose != "peak") await Wait(.005);
        _field.SetDeathClock(true, 1);
        float height = hagi.AttackClipVertical; int frame = hagi.AttackClipFrame;
        await Wait(.08);
        Require(height > 1 && hagi.AttackClipFrame == frame && hagi.AttackClipVertical == height && contacts == 0,
            "空中の溜めは1m超で止まり、停止中に落下や命中を進めない");
        _field.SetDeathClock(false, 2); await task;
        Require(contacts == 1 && _field.HagiAirOpeners == 1 && _field.HagiLandings == 0,
            "初撃は接触一度で止め、刈り取りの前に着地しない");
        var join = hagi.CurrentHagiAnchors!;
        var sharedCut = _field.HagiReapEffects.Last();
        double cutAge = sharedCut.Age;
        var follow = root with { Reap = true, Return = true, Keep = false, NextReturn = false, AirFollow = true };
        task = _field.PlayHagiSwing(hagi, new[] { enemy }, follow, () => {
            CheckVerticalCut(_field.HagiReapEffects.Last());
            Require(_field.HagiReapEffects.Count() == 1 && ReferenceEquals(sharedCut, _field.HagiReapEffects.Last())
                && sharedCut.Reinforcements == 1 && sharedCut.Age >= cutAge,
                "刈り取りは同じ三日月を増光し、内側の二本目や描き直しを出さない");
            Require(hagi.AttackClipVertical > 0 && _field.HagiLandings == 0,
                "続く刈り取りも接地より先に命中する");
            contacts++;
        });
        Require(hagi.CurrentHagiAnchors!.Socket == join.Socket && hagi.CurrentHagiAnchors.Tip == join.Tip,
            "落下斬りと直後の刈り取りは同じ接触セルでつなぐ");
        await task;
        Require(contacts == 2 && _field.HagiAirFollows == 1 && _field.HagiLandings == 1 && hagi.IdleLoopPlaying,
            "刈り取りの接触を一度通知し、一度着地して待機へ戻る");
        task = _field.PlayHagiSwing(hagi, new[] { enemy }, root with {
            Reap = true, Return = true, Reverse = true, Keep = false, NextReturn = false,
            Air = HagiPresentation.AirKind.Finisher }, () => {
                Require(_field.HagiReapEffects.Last().Reverse == hagi.DeathSprite.FlipH,
                    "二撃目のフィニッシュも旧返し方向でなく落下斬りの向きに合わせる");
                contacts++;
            });
        await task;
        Require(contacts == 3 && _field.HagiAirFinishers == 1 && _field.HagiLandings == 2,
            "単独の返し刃でもフィニッシュは跳躍から着地まで一回");
        task = _field.PlayHagiSwing(hagi, new[] { enemy }, root with { Keep = false, NextReturn = false },
            () => throw new Exception("中断した空中斬りが命中した"));
        await Wait(.12); _field.BeginBattle(openings, "", 0); await task;
        Require(_field.HagiAirOpeners == 0 && _field.HagiLandings == 0, "再戦で空中斬りと着地予約を破棄");
        hagi = _field.FindPawn(1)!; enemy = _field.FindPawn(3)!;
        task = _field.PlayHagiSwing(hagi, new[] { enemy }, root with { Keep = false, NextReturn = false },
            () => throw new Exception("死亡した空中斬りが命中した"));
        await Wait(.12); hagi.SetHp(0); hagi.AnimateDeath(); await task;
        Require(_field.HagiLandings == 0 && hagi.HagiClipName is null, "空中で死亡したとき着地音を予約しない");
        _field.BeginBattle(openings.Select(o => o with { Team = 1 - o.Team }).ToArray(), "", 0);
        hagi = _field.FindPawn(1)!; enemy = _field.FindPawn(3)!;
        task = _field.PlayHagiSwing(hagi, new[] { enemy }, root with {
            Keep = false, NextReturn = false, Air = HagiPresentation.AirKind.Finisher }, () => {
                CheckVerticalCut(_field.HagiReapEffects.Last()); contacts++;
            });
        await Wait(.03); Require(hagi.DeathSprite.FlipH, "空中斬りの敵側表示も左右反転");
        await task;
        Require(_field.HagiLandings == 1 && hagi.IdleLoopPlaying && hagi.DeathSprite.FlipH,
            "反転した空中斬りも着地して敵側の待機へ戻る");
        GD.Print("HAGI_AIR_VISUAL_OK pause speed shared-contact one-jump landing finish cancel");
    }

    private static void CheckVerticalCut(HagiReapFx3D fx)
    {
        Require(fx.Crescent, "空中斬りは開いた太い三日月");
        Vector3 top = fx.SweepPoint(.08333f), bottom = fx.SweepPoint(.91667f);
        Require(top.Y - bottom.Y > fx.SweepRadius * 1.8f,
            "空中斬りは水平な円弧の横軸を起こし、頭上から足元へ抜ける");
        Require(bottom.Y > .05f, "縦斬りの弧は地面に埋まらない");
        Require(fx.SweepPoint(0).DistanceTo(fx.SweepPoint(1)) > fx.SweepRadius * 1.8f,
            "三日月の両端が離れ、円を閉じない");
    }
}
