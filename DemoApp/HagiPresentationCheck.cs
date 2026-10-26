using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class HagiPresentationCheck : Control
{
    private BattlefieldView3D _field = null!;
    public override async void _Ready()
    {
        try
        {
            CheckReapContract();
            CheckAirContract();
            var fixtures = CheckScripts();
            if (OS.GetCmdlineUserArgs().Contains("--script-only"))
            { GD.Print("HAGI_SCRIPT_AND_AIR_CHECK_OK parity=15"); GetTree().Quit(); return; }
            if (OS.GetCmdlineUserArgs().Contains("--real-2x"))
            {
                foreach (int i in new[] { 11, 12, 3 })
                    await CaptureRealBattle(fixtures[i].Cue, fixtures[i].Playback);
                GD.Print("HAGI_REAL_2X_OK carry-battalion elegy-boss death-pair-boss"); GetTree().Quit(); return;
            }
            _field = new BattlefieldView3D(); _field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(_field);
            var openings = new[] {
                Opening(1, UnitCatalog.Hagi, 0, 2), Opening(2, UnitCatalog.Rica, 0, 0),
                Opening(3, EnemyCatalog.Recruit, 1, 0), Opening(4, EnemyCatalog.Recruit, 1, 2), Opening(5, EnemyCatalog.Recruit, 1, 4) };
            _field.BeginBattle(openings, "", 0);
            var hagi = _field.FindPawn(1)!; var enemy = _field.FindPawn(3)!;
            foreach (int souls in new[] { 0, 3, 6, 9 })
            {
                _field.ApplyHagiState(new(1, souls, 0, 0, Stage: souls / 3));
                _field.ApplyHagiState(new(3, 0, souls / 2, souls == 0 ? 200 : 500));
                _field.ApplyDeathState(new(2, 9, 0, 0, _field.FindPawn(2)!.MaxHp));
                enemy.SetHp(enemy.MaxHp / 2);
                await Wait(.24); await Capture("souls-" + souls);
                Require(enemy.HagiMarks == souls / 2 && enemy.HagiEligible == (souls > 0), "印の本数と線以下の縁取り");
                Require(souls == 0 || Math.Abs(enemy.Hud.HagiLineX) < .001f, "5割の鎌の印はHPバーの中央");
                var bladeAura=hagi.GetChildren().OfType<HagiAura3D>().Single();
                Require(Math.Abs(bladeAura.BladeStage-souls/3f)<.03f,"強化の光は魂の実段階に一致");
                Require(bladeAura.BladeVisible==(souls>0),"未強化では刃の赤い帯を出さない");
            }
            _field.ApplyHagiState(new(3, 0, 5, 350));
            float lowerLineX = enemy.Hud.HagiLineX;
            Require(!enemy.HagiEligible, "HPが線より上なら縁取りを外す");
            _field.ApplyHagiState(new(3, 0, 5, 500));
            Require(enemy.HagiEligible && enemy.Hud.HagiLineX > lowerLineX, "Reap 線で印だけ右へ動かし傷5本は保持");
            enemy.SetHp(enemy.MaxHp);
            Require(!enemy.HagiEligible, "回復で線より上へ戻れば縁取りを外す");
            enemy.SetHp(enemy.MaxHp / 2);
            Require(HagiLight3D.SoulTint(ReapLabels.SoulDeath) != HagiLight3D.SoulTint(ReapLabels.SoulAlly)
                && HagiLight3D.SoulTint(80) != HagiLight3D.SoulTint(ReapLabels.SoulDeath), "敵死・味方死・線を割った魂の色を区別");
            var aura = hagi.GetChildren().OfType<HagiAura3D>().Single();
            Vector3 bladeVertical=hagi.HagiBladePoint(new Vector2(.5f,0))-hagi.HagiBladePoint(new Vector2(.5f,1));
            Vector3 spriteVertical=hagi.DeathSprite.GlobalBasis.Y*hagi.DeathSprite.Texture.GetHeight()*hagi.DeathSprite.PixelSize;
            Require(bladeVertical.DistanceTo(spriteVertical)<.0001f,"刃の炎は斜めカメラでも立ち絵の実頂点変換を使う");
            Require(hagi.IdleLoopPlaying && hagi.IdleLoopFrameCount == 48, "採用済み48コマの待機ループ");
            _field.SetDeathClock(true, 1); double before = aura.Age;
            Vector3 bladeBeforePause=aura.BladeEdgeMiddle;
            int idleBeforePause = hagi.IdleLoopFrame;
            await Wait(.12); Require(aura.Age == before, "停止中の魂");
            Require(aura.BladeEdgeMiddle==bladeBeforePause,"停止中の刃の炎と追跡点も固定");
            Require(hagi.IdleLoopFrame == idleBeforePause, "再生時計の停止中は待機連番も止まる");
            _field.SetDeathClock(false, 2); await Wait(.12); Require(aura.Age - before > .15, "倍速の魂");
            Require(aura.BladeEdgeMiddle.DistanceTo(bladeBeforePause)>.0001f,"再開で待機連番の刃へ追従");
            Require(hagi.IdleLoopFrame != idleBeforePause, "再生時計の再開で待機連番も進む");
            _field.SetDeathClock(false, 1);
            Vector3 strikeOrigin=Vector3.Zero;
            var stopped = _field.PlayHagiSwing(hagi, new[] { enemy }, new(1, Array.Empty<int>(), true, false, false), () => strikeOrigin=hagi.GlobalPosition);
            await Wait(.04);
            Require(hagi.AttackClipPlaying && hagi.HagiClipName == "hagi_reap_spin_v1", "踏み込みから全身の回転連番");
            Require(!hagi.IdleLoopPlaying, "踏み込み中は待機連番を出さない");
            float stepFoot = hagi.DeathSprite.Position.Y - (940 - 960 * .5f) * hagi.DeathSprite.PixelSize;
            Require(Math.Abs(stepFoot - BattlePawn3D.PortraitGroundYForCheck) < .01f, "踏み込みでも靴底を接地");
            Require(Math.Abs(hagi.DeathSprite.PixelSize - UiKit.PortraitWorldHeight("hagi") / 960f) < .00001f, "しゃがみと跳躍は頭の実寸で統一");
            _field.SetDeathClock(true, 1); Vector3 frozen = hagi.Position;
            int frozenCel = hagi.AttackClipFrame;
            await Wait(.12); Require(hagi.Position == frozen && hagi.AttackClipFrame == frozenCel && !stopped.IsCompleted, "踏み込み中も移動と全身セルが停止");
            _field.SetDeathClock(false, 1); await Wait(.12);
            var reapFx = _field.HagiReapEffects.Single(); Require(!reapFx.Released,"溜めで光が刃に集まる");
            _field.SetDeathClock(true,1); double chargeAge = reapFx.Age;
            await Wait(.09); Require(reapFx.Age == chargeAge,"刈り取りの予兆も停止");
            _field.SetDeathClock(false, 2); await stopped;
            Require(hagi.IdleLoopPlaying, "刈り取りの帰着で待機連番へ戻る");
            Require(reapFx.Released && reapFx.Age > 0,"接触で斬光と余韻へ移行");
            Require(new Vector2(reapFx.SweepCenter.X-strikeOrigin.X,reapFx.SweepCenter.Z-strikeOrigin.Z).Length()<.001f,
                "円弧の中心は標的ではなく攻撃したハギ");
            foreach(float phase in new[]{0f,.25f,.5f,.75f,1f})
            {
                Vector3 p=reapFx.SweepPoint(phase);
                Require(Math.Abs(p.Y-reapFx.SweepCenter.Y)<.001f,"斬撃の全周が地面と平行");
                Require(Math.Abs(p.DistanceTo(reapFx.SweepCenter)-reapFx.SweepRadius)<.001f,"放射状に飛ばず同じ回転半径を保つ");
            }
            _field.SetDeathClock(true,2); double releaseAge = reapFx.Age;
            await Wait(.08); Require(reapFx.Age == releaseAge,"刈り取りの残光も停止");
            _field.SetDeathClock(false, 1);
            var normalSwing=_field.PlayHagiSwing(hagi,new[]{enemy},new(1,Array.Empty<int>(),false,false,false),()=>{});
            await Wait(.25);
            var normalFx=_field.HagiSweepEffects.Single(f=>!f.Heavy);
            Require(normalFx.Released && Math.Abs(normalFx.SweepPoint(.65f).Y-normalFx.SweepCenter.Y)<.001f,
                "通常攻撃も同じ水平円弧を使う");
            float normalRadius = normalFx.SweepRadius;
            await normalSwing;
            var weightedSwing = _field.PlayHagiSwing(hagi, new[] { enemy }, new(1, Array.Empty<int>(), false, false, false, Weighted: true), () => { });
            await Wait(.25);
            Require(_field.HagiSweepEffects.Single(f => !f.Heavy).SweepRadius > normalRadius * 1.1f,
                "重みの一撃は単体でも弧の半径を一回り大きくする");
            await weightedSwing;
            // 刃を返す姿勢を経て引き刈りへ移る。停止中は途中コマも接触も進まない。
            int returnContacts = 0;
            await _field.PlayHagiSwing(hagi, new[] { enemy }, new(1, Array.Empty<int>(), false, false, false, Keep: true, NextReturn: true), () => { });
            Require(hagi.HagiClipName == "hagi_orbit_out_hold_v3" && !hagi.IdleLoopPlaying,
                "通常斬撃を回転で振り抜いた背面姿勢を保持する");
            var join = hagi.CurrentHagiAnchors!;
            bool outward = _field.HagiSweepEffects.Last().Reverse;
            var returnSwing = _field.PlayHagiSwing(hagi, new[] { enemy },
                new(1, Array.Empty<int>(), true, false, true, Return: true, Reverse: true, Kind: ReapLabels.KindAfter), () => returnContacts++);
            _field.SetDeathClock(true, 1); await Wait(.08);
            Require(returnContacts == 0 && !returnSwing.IsCompleted && hagi.AttackClipFrame == 0,
                "返し刃は専用姿勢の先頭から始まり、停止中は命中直前へ飛ばさない");
            Require(hagi.CurrentHagiAnchors!.Socket == join.Socket && hagi.CurrentHagiAnchors.Tip == join.Tip,
                "通常斬撃の終点と引き刈りの始点は同じ全身セル");
            _field.SetDeathClock(false, 2);
            if (OS.GetCmdlineUserArgs().Contains("--capture"))
            {
                while (returnContacts == 0) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                _field.SetDeathClock(true, 2); await Capture("return-contact"); _field.SetDeathClock(false, 2);
            }
            await returnSwing;
            Require(returnContacts == 1 && _field.HagiReturns == 1 && _field.HagiReapEffects.Last().Reverse != outward,
                "同じ振りの返し刃を逆向きに一度だけ接触");
            Require(_field.HagiReapEffects.Last().SweepRadius < normalRadius,
                "引き刈りの弧は腰へ引き込む小さな半径");
            int twinContacts = 0;
            await _field.PlayHagiSwing(hagi, new[] { enemy }, new(1, Array.Empty<int>(), false, false, false, Keep: true), () => twinContacts++);
            Require(hagi.HagiClipName == "hagi_orbit_out_hold_v3", "二度斬りの一振り目を背面まで振り抜く");
            join = hagi.CurrentHagiAnchors!;
            var rising = _field.PlayHagiSwing(hagi, new[] { enemy }, new(1, Array.Empty<int>(), false, false, true, Reverse: true), () => twinContacts++);
            Require(hagi.HagiClipName == "hagi_orbit_back_v3" && hagi.AttackClipFrame == 0
                && hagi.CurrentHagiAnchors!.Socket == join.Socket && hagi.CurrentHagiAnchors.Tip == join.Tip,
                "二撃目は背面姿勢を共有し、そこから専用の回転斬りを始める");
            await rising;
            Require(twinContacts == 2 && hagi.IdleLoopPlaying, "八の字の二振りが各一度命中して待機へ戻る");
            _field.BeginBattle(openings, "", 0); hagi = _field.FindPawn(1)!; enemy = _field.FindPawn(3)!;
            Require(!_field.HagiSweepEffects.Any(),"再戦で通常攻撃と刈り取りの残光を破棄");
            _field.ApplyHagiState(new(1, 9, 0, 0, Stage: 3));
            aura = hagi.GetChildren().OfType<HagiAura3D>().Single();
            _field.ApplyDeathState(new(2, 9, 0, 0, _field.FindPawn(2)!.MaxHp));
            var task = _field.PlayHagiSwing(hagi, new[] { enemy, _field.FindPawn(4)!, _field.FindPawn(5)! }, new(1, Array.Empty<int>(), true, false, false), () => {
                foreach (int id in new[] { 3, 4, 5 }) { var pawn = _field.FindPawn(id)!; pawn.SetHp(0); _field.ShowHagiDamage(pawn, 300, true, false); }
            });
            await Wait(.18); await Capture("windup"); await Wait(.20); await Capture("column-contact");
            await task; Require(_field.HagiColumns == 1 && _field.HagiReaps == 3, "列は一振り・実対象3体");
            _field.ApplyHagiState(new(1, 8, 0, 0, Stage: 2)); await Wait(.25); await Capture("soul-fade");
            hagi.AnimateVictory(); await Wait(.03); Require(!aura.Visible, "勝利で魂を隠す");
            _field.BeginBattle(openings.Select(o => o with { Team = 1 - o.Team }).ToArray(), "", 0);
            hagi = _field.FindPawn(1)!; enemy = _field.FindPawn(3)!;
            Require(hagi.HagiSouls == 0 && _field.HagiReaps == 0, "再戦で破棄");
            _field.ApplyHagiState(new(1, 6, 0, 0, Stage: 2));
            task = _field.PlayHagiSwing(hagi, new[] { enemy }, new(1, Array.Empty<int>(), true, true, false), () => { });
            await Wait(.025); Require(hagi.AttackClipPlaying && hagi.DeathSprite.FlipH, "敵側の全身セルも左右反転");
            await Wait(.135); await Capture("mirrored-pursuit"); await task;
            Require(hagi.DeathSprite.FlipH, "敵側の左右反転");
            int contacts = 0;
            await _field.PlayHagiSwing(hagi, new[] { enemy }, new(1, Array.Empty<int>(), true, false, false, Keep: true), () => contacts++);
            await hagi.AttackClipFinished;
            Require(!hagi.AttackClipPlaying && !hagi.IdleLoopPlaying && hagi.HagiClipName == "hagi_reap_chain_v1", "連撃の間は着地姿勢を保持");
            var heldFx=_field.HagiReapEffects.Last(); Vector3 heldCenter=heldFx.SweepCenter;
            await Wait(.08); Require(heldFx.SweepCenter==heldCenter,"着地後の残光は命中位置に残す");
            await _field.PlayHagiSwing(hagi, new[] { enemy }, new(1, Array.Empty<int>(), true, false, true), () => contacts++);
            Require(contacts == 2 && hagi.HagiClipName is null && hagi.IdleLoopPlaying, "連撃の各一打は接触一度で待機へ帰着");
            var cancelled = _field.PlayHagiSwing(hagi, new[] { enemy }, new(1, Array.Empty<int>(), true, false, false), () => throw new Exception("中断した振りが接触した"));
            await Wait(.02); _field.BeginBattle(openings, "", 0); await cancelled;
            Require(_field.HagiSwings == 0, "途中で再戦しても古い振りを残さない");
            hagi = _field.FindPawn(1)!; enemy = _field.FindPawn(3)!;
            cancelled = _field.PlayHagiSwing(hagi, new[] { enemy }, new(1, Array.Empty<int>(), true, false, false), () => throw new Exception("中断した連番が接触した"));
            await Wait(.02); hagi.CancelHagiMotion(); await cancelled;
            Require(_field.HagiSwings == 0 && !hagi.AttackClipPlaying, "生存中の中断でも接触を起こさない");
            cancelled = _field.PlayHagiSwing(hagi, new[] { enemy }, new(1, Array.Empty<int>(), true, false, false), () => throw new Exception("死亡した攻撃が接触した"));
            await Wait(.02); hagi.SetHp(0); hagi.AnimateDeath(); await cancelled;
            Require(!hagi.AttackClipPlaying && hagi.HagiClipName is null && _field.HagiSwings == 0
                && _field.HagiSweepEffects.All(f=>f.Released), "死亡時は全身セルと未発動の斬撃を破棄");
            hagi.SetHp(hagi.MaxHp); hagi.AnimateRevive();
            Require(hagi.HagiClipName is null && hagi.IdleLoopPlaying, "復活は待機から再開");
            await CheckAirVisual(openings);
            _field.QueueFree(); await Wait(.03);
            if (OS.GetCmdlineUserArgs().Contains("--visual-only"))
            { GD.Print("HAGI_VISUAL_CHECK_OK ok=True"); GetTree().Quit(); return; }
            foreach (var fixture in fixtures)
            {
                var main = new Main { ViewerMode = true }; main.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(main);
                main.ViewerFastPlayback = true; main.SetPlaybackSpeed(1000);
                main.EnterViewerLive(fixture.Playback, fixture.Cue);
                for (int frame = 0; frame < 5000 && main.IsPlaying; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                Require(!main.IsPlaying && main.ViewerPlaybackError is null, "実台本の再生完了 " + main.ViewerPlaybackError);
                var index = HagiPresentation.Build(fixture.Playback.Result.Events, fixture.Playback.Openings, fixture.Playback.Death?.Hagi);
                Require(main.ViewerField.HagiSwings == index.Swings.Count, "Attackと振りの件数が一致");
                Require(main.ViewerField.HagiReaps == index.Reaps.Count, "刈り取りの二重描画なし");
                var hagiIds = fixture.Playback.Openings.Where(o => o.UnitId == "hagi").Select(o => o.InstanceId).ToHashSet();
                Require(main.ViewerField.HagiGrazes == fixture.Playback.Result.Events.Count(e => e.Kind == BattleEventKind.Damage
                    && e.FriendlyFire && e.ActorId is int id && hagiIds.Contains(id)), "巻き込みの二重描画なし");
                foreach (var pawn in main.ViewerField.Pawns.Values)
                {
                    if (fixture.Hp.TryGetValue(pawn.InstanceId, out int hp))
                        Require(pawn.Hp == hp, $"最終HP一致 {fixture.Cue.Id} {pawn.UnitId}/{pawn.InstanceId} actual={pawn.Hp} expected={hp}");
                }
                GD.Print($"HAGI_REPLAY_OK {fixture.Cue.Id} swings={main.ViewerField.HagiSwings} reaps={main.ViewerField.HagiReaps} pursuits={main.ViewerField.HagiPursuits} links={main.ViewerField.HagiLinks} columns={main.ViewerField.HagiColumns} returns={main.ViewerField.HagiReturns} graze={main.ViewerField.HagiGrazes}");
                if (fixture.Cue.Id == "hagi.live.4.1")
                {
                    int beforeReturn = index.Returns.Keys.Min();
                    int victim = fixture.Playback.Result.Events[beforeReturn].TargetId!.Value;
                    int hpBeforeReturn = fixture.Playback.Result.Events.Take(beforeReturn)
                        .Last(e => e.Kind == BattleEventKind.Damage && e.TargetId == victim).HpAfter;
                    await main.SeekTo(beforeReturn);
                    Require(hpBeforeReturn > 0 && main.ViewerField.FindPawn(victim)!.Hp == hpBeforeReturn
                        && main.ViewerField.HagiReaps == index.Reaps.Count(i => i < beforeReturn), "薙ぎが後続の返し刃のHPを先取りしない");
                    main.ResumePlayback();
                    for (int frame = 0; frame < 5000 && main.IsPlaying; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Require(main.ViewerPlaybackError is null && main.ViewerField.HagiReaps == index.Reaps.Count, "返し刃の直前シーク後も全件を一度ずつ刈る");
                }
                if (fixture.Cue.Id == "hagi.live.0.2")
                {
                    int firstHit = index.Reaps.Min();
                    await main.SeekTo(firstHit); Require(main.IsPaused && main.EventIndex == firstHit, "刈り取りの直前へシーク");
                    main.ResumePlayback();
                    for (int frame = 0; frame < 5000 && main.IsPlaying; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Require(main.ViewerPlaybackError is null && main.ViewerField.HagiReaps == index.Reaps.Count, "シーク後の実件数");
                    main.ResumePlayback();
                    for (int frame = 0; frame < 5000 && main.IsPlaying; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
                    Require(main.ViewerPlaybackError is null && main.ViewerField.HagiSwings == index.Swings.Count && main.ViewerField.HagiReaps == index.Reaps.Count, "同じ台本を再戦");
                }
                await main.StopViewerPlayback(); main.QueueFree(); await Wait(.03);
            }
            GD.Print("HAGI_PRESENTATION_CHECK_OK ok=True parity=15 reap-events actual-values reap-count acrobatic-cels head-scale ground horizontal-orbit actor-center fixed-radius normal-sweep blade-stage blade-idle-track blade-pause pause speed mirror combo cancel reset victory replay seek");
            GetTree().Quit();
        }
        catch (Exception error) { GD.PushError(error.ToString()); GetTree().Quit(1); }
    }
    private static void CheckReapContract()
    {
        var openings = new[] { Opening(1, UnitCatalog.Hagi, 0, 0), Opening(2, EnemyCatalog.Recruit, 1, 0) };
        var events = new List<BattleEvent> {
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 2 },
            new() { Turn = 1, Kind = BattleEventKind.Highlight, ActorId = 1 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 2, Amount = 40, HpAfter = 0 },
            new() { Turn = 1, Kind = BattleEventKind.Reap, ActorId = 1, TargetId = 2, Text = "未知", Slot = 99 },
        };
        Require(HagiPresentation.Build(events, openings, null).Reaps.Count == 0, "Highlight と HP 0 から刈りを推定しない");
        for (int kind = 1; kind <= 6; kind++)
        {
            events.Clear();
            events.Add(new() { Turn = 1, Kind = BattleEventKind.Reap, ActorId = 1, TargetId = 2, Text = ReapLabels.Reap, Slot = kind, Amount = 40, StatusRemaining = 450 });
            if (kind is 1 or 2 or 6) events.Add(new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 2 });
            events.Add(new() { Turn = 1, Kind = BattleEventKind.Highlight, ActorId = 1 });
            events.Add(new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 2, Amount = 40, HpAfter = 0 });
            var index = HagiPresentation.Build(events, openings, null);
            Require(index.Reaps.SetEquals(new[] { events.Count - 1 }) && index.ReapKinds[events.Count - 1] == kind,
                "刈る 1〜6 と実 Damage の対応");
        }
        events = new() {
            new() { Turn = 1, Kind = BattleEventKind.Reap, ActorId = 1, Text = ReapLabels.Soul, StatusRemaining = 9, Amount = 2, TargetId = 2 },
            new() { Turn = 1, Kind = BattleEventKind.Reap, ActorId = 1, Text = ReapLabels.Stage, StatusRemaining = 9, Amount = 1, Slot = 321 },
            new() { Turn = 1, Kind = BattleEventKind.Reap, ActorId = 1, TargetId = 2, Text = ReapLabels.Line, Amount = 4, StatusRemaining = 456 },
            new() { Turn = 1, Kind = BattleEventKind.Reap, ActorId = 1, Text = ReapLabels.Twin, Amount = 1 },
            new() { Turn = 1, Kind = BattleEventKind.Reap, ActorId = 1, TargetId = 2, Text = ReapLabels.Reap, Slot = 6, Amount = 40 },
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 2 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 2, Amount = 40, HpAfter = 0 },
        };
        var parsed = HagiPresentation.Build(events, openings, null);
        Require(parsed.Frames[1].Single().Stage == 1 && parsed.Frames[1].Single().BaseLinePermil == 321
            && parsed.Frames[2].Single().LinePermil == 456, "魂から段や線を再計算せず欄の値を読む");
        Require(parsed.Swings[5].Reap && parsed.Swings[5].Reverse, "まれな二度斬りの刈り取り枝");
        events = new() {
            new() { Turn = 1, Kind = BattleEventKind.Reap, ActorId = 1, Text = ReapLabels.Encore, Amount = 1 },
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 2 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 2, HpAfter = 40 },
            new() { Turn = 1, Kind = BattleEventKind.Reap, ActorId = 1, TargetId = 2, Text = ReapLabels.Reap, Slot = 3, Amount = 40 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 2, Amount = 40, HpAfter = 0 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 3, HpAfter = 30 },
            new() { Turn = 1, Kind = BattleEventKind.Reap, ActorId = 1, TargetId = 3, Text = ReapLabels.Reap, Slot = 3, Amount = 30 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 3, Amount = 30, HpAfter = 0 },
            new() { Turn = 1, Kind = BattleEventKind.Reap, ActorId = 1, Text = ReapLabels.Encore, Amount = 2 },
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 3 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 3, HpAfter = 20 },
        };
        parsed = HagiPresentation.Build(events, openings, null);
        Require(parsed.Returns.Keys.SequenceEqual(new[] { 3, 6 }) && parsed.Returns[3].Damage.SequenceEqual(new[] { 4 })
            && parsed.Returns[6].Damage.SequenceEqual(new[] { 7 }), "巻き込んだ敵は当たった順の返し刃へ一件ずつ接続");
        Require(parsed.Swings[1].Keep && parsed.Swings[1].NextReturn && parsed.Returns[3].Keep
            && parsed.Returns[3].NextReturn && parsed.Returns[6].Keep && !parsed.Returns[6].NextReturn
            && parsed.Returns.Values.All(s => s.Encore == 1) && parsed.Swings[9].Encore == 2,
            "複数の刈り取りから再行動へ途切れずつなぎ、残像の濃さも引き継ぐ");
        events = new() {
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 3, TargetId = 2 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 3, TargetId = 2, HpAfter = 40 },
            new() { Turn = 1, Kind = BattleEventKind.Reap, ActorId = 1, TargetId = 2, Text = ReapLabels.Pursue, PartnerId = 3, StatusRemaining = 0, Slot = 0 },
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = 2 },
            new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = 2, HpAfter = 20 },
        };
        parsed = HagiPresentation.Build(events, openings, null);
        Require(parsed.Swings[3].Pursuit && parsed.Swings[3].Partner == 3 && parsed.FastEvents.SetEquals(new[] { 1 }), "PartnerIdの仲間の命中直後だけを詰めて追撃へ");
        events[2] = new() { Turn = 1, Kind = BattleEventKind.Reap, ActorId = 1, TargetId = 2, Text = ReapLabels.Pursue, StatusRemaining = 0, Slot = 0 };
        parsed = HagiPresentation.Build(events, openings, null);
        Require(parsed.Swings[3].Pursuit && parsed.Swings[3].Partner is null && parsed.FastEvents.Count == 0, "刻みからの追撃も仲間を推定せず通す");
    }
    private static List<(FxCue Cue, FxLivePlayback Playback, Dictionary<int,int> Hp)> CheckScripts()
    {
        var fixtures = new List<(FxCue, FxLivePlayback, Dictionary<int,int>)>();
        foreach (var cue in FxCatalog.Registered.Where(c => c.Id.StartsWith("hagi.live.")))
        {
            var playback = FxLivePlayback.Create(cue); var track = playback.Death!.Hagi;
            var index = HagiPresentation.Build(playback.Result.Events, playback.Openings, track);
            FxCatalog.TryGetPreset(cue, out var formation);
            var stage = EnemyCatalog.PlaytestStages[cue.PlaytestWave!.Value];
            var players = BattleEngine.Materialize(formation, 0); var enemies = BattleEngine.MaterializeEnemy(stage.Enemy, stage.Scale);
            var plain = BattleEngine.Run(players, enemies, cue.Seed, verbose: true);
            Require(JsonSerializer.Serialize(plain.Events) == JsonSerializer.Serialize(playback.Result.Events)
                && plain.PlayerWon == playback.Result.PlayerWon && plain.Turns == playback.Result.Turns, "観測で台本を変更しない");
            var tally = playback.Result.TallyByUnit["hagi"];
            Require(index.Reaps.Count == tally.R390ReapKills, $"刈り取り判別 {index.Reaps.Count}/{tally.R390ReapKills}");
            Require(index.Swings.Values.Concat(index.Returns.Values).Sum(s => s.Damage.Count(index.Reaps.Contains)) == index.Reaps.Count, "列の刈り取りもAttackに接続");
            int peak = index.Frames.Values.SelectMany(f => f).Max(s => s.Souls);
            Require(peak == tally.R389SoulPeak, $"魂の最大実値 {peak}/{tally.R389SoulPeak}");
            Require(index.Frames.Values.SelectMany(f => f).Any(s => s.LinePermil > 0), "実値の刈り取り線");
            GD.Print($"HAGI_SCRIPT_OK {cue.Id} events={plain.Events.Count} reaps={index.Reaps.Count} marks={tally.R389MarksPlaced} soul={peak}");
            var states = index.Frames.Values.SelectMany(f => f).GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.Last());
            foreach (var unit in players.Concat(enemies))
                if (states.TryGetValue(unit.InstanceId, out var state))
                    Require(state.Marks == unit.RawCounter(HagiDepthTrait.MarkKey) && state.Souls == unit.RawCounter(HagiDepthTrait.SoulKey), "印と魂の最後の実値が一致");
            fixtures.Add((cue, playback, players.Concat(enemies).ToDictionary(u => u.InstanceId,u => u.Hp)));
        }
        return fixtures;
    }
    private static DemoOpening Opening(int id, UnitDef u, int team, int slot) => new(id, team, u.Id, u.Name, slot, u.MaxHp, u.MaxHp, u.Attack, AttackPattern.Single, u.Advances, u.Traits);
    private async Task CaptureRealBattle(FxCue cue, FxLivePlayback playback)
    {
        var main = new Main { ViewerMode = true }; main.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(main); main.SetPlaybackSpeed(2);
        main.EnterViewerLive(playback, cue);
        bool reap = false, column = false;
        ulong deadline = Time.GetTicksMsec() + 90000;
        while (main.IsPlaying && Time.GetTicksMsec() < deadline)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!reap && main.ViewerField.HagiReaps > 0)
            { reap = true; await Wait(.055); await Capture(cue.Id + "-2x-reap"); }
            if (!column && main.ViewerField.HagiColumns > 0)
            { column = true; await Wait(.055); await Capture(cue.Id + "-2x-column"); }
        }
        Require(!main.IsPlaying && main.ViewerPlaybackError is null && reap, "2倍速の実戦再生");
        GD.Print($"HAGI_2X_CAPTURE_OK {cue.Id} reaps={main.ViewerField.HagiReaps} columns={main.ViewerField.HagiColumns}");
        await main.StopViewerPlayback(); main.QueueFree(); await Wait(.1);
    }
    private async Task Wait(double s) => await ToSignal(GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string dir = ProjectSettings.GlobalizePath("res://../.tmp/hagi-v2/captures"); System.IO.Directory.CreateDirectory(dir);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(dir, name + ".png"));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
