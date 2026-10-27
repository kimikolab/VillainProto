using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 受け渡しと実手番を別々に照合。実戦の対象・件数・順序を画面側で選び直さない。
public partial class EreGiftCheck : Node
{
    public override async void _Ready()
    {
        try
        {
            CheckScripts();
            var field = new BattlefieldView3D();
            field.SetAnchorsPreset(Control.LayoutPreset.FullRect); AddChild(field);
            if (OS.GetCmdlineUserArgs().Contains("--clip"))
            { await CaptureSequence(field); field.QueueFree(); await Frame(); GetTree().Quit(); return; }
            foreach (bool mirror in new[] { false, true })
            {
                var openings = new[] {
                    Opening(1, UnitCatalog.Ere, mirror ? 1 : 0, 2),
                    Opening(2, UnitCatalog.Gald, mirror ? 1 : 0, 0),
                    Opening(3, UnitCatalog.Ere, mirror ? 0 : 1, 2),
                };
                field.BeginBattle(openings, "哀歌：味方へ託す／敵にも届く", 0);
                field.SetGatherClock(false, 1);
                foreach (int id in new[] { 2, 3 })
                foreach (bool begins in new[] { false, true })
                {
                    var bard = field.FindPawn(1)!; var target = field.FindPawn(id)!;
                    var fx = field.ShowDirgeGift(bard, target, begins);
                    fx._Process(begins ? .24 : .32);
                    if (begins) foreach (var aura in field.DirgeAuras) aura._Process(.24);
                    field.SetGatherClock(true, 1);
                    float age = fx.Age;
                    var halo = field.DirgeAuras.FirstOrDefault(); float haloAge = halo?.Age ?? 0;
                    await Wait(.12);
                    Require(fx.Age == age && (halo is null || halo.Age == haloAge), "停止中に音符と受け取りの光を進めない");
                    Require(!field.FindChildren("*", "", true, false).OfType<PopupLabel2D>().Any(), "受け渡しと解放でテロップを作らない");
                    await Snapshot($"{(mirror ? "enemy-bard" : "player-bard")}-{id}-{(begins ? "act" : "transfer")}");
                    field.SetGatherClock(false, 4);
                    for (int i = 0; i < 240 && !fx.Finished.IsCompleted; i++) await Frame();
                    Require(fx.Finished.IsCompletedSuccessfully && fx.Finished.Result, "途中の倍速変更で演出完了");
                    await Frame();
                    Require(!bard.DirgeSinging && !field.GatherEffects.Any(), "歌唱と一時ノードの後片付け");
                    field.SetGatherClock(false, 1);
                    Require(begins ? !field.DirgeAuras.Any() && target.DirgeGlow == 0
                        : field.DirgeAuras.Count() == 1 && target.DirgeGlow > 0, "受け取りの光を実手番まで保持し、解放後に消す");
                }
                var dying = field.ShowDirgeGift(field.FindPawn(1)!, field.FindPawn(2)!, false);
                dying._Process(.48);
                field.FindPawn(2)!.AnimateDeath(); dying._Process(.01);
                foreach (var aura in field.DirgeAuras.ToArray()) aura._Process(.01);
                Require(dying.Finished.IsCompleted && !dying.Finished.Result, "受け手の死亡で中断");
                Require(!field.DirgeAuras.Any() && field.FindPawn(2)!.DirgeGlow == 0, "死亡で受け取りの光も消す");
                var interrupted = field.ShowDirgeGift(field.FindPawn(1)!, field.FindPawn(3)!, false);
                field.BeginBattle(openings, "再戦", 0);
                Require(interrupted.Finished.IsCompleted && !interrupted.Finished.Result, "再戦で古い待ち手を解放");
                Require(!field.FindPawn(1)!.DirgeSinging && !field.GatherEffects.Any() && !field.DirgeAuras.Any(), "再戦に光と歌唱を持ち越さない");
                await Frame();
            }
            field.QueueFree(); await Frame();

            // 本番の停止・中断からも同じ時計と待ち手を操作する。
            var pausedMain = new Main { ViewerMode = true };
            pausedMain.SetAnchorsPreset(Control.LayoutPreset.FullRect); AddChild(pausedMain);
            pausedMain.EnterScripted(new[] { Opening(1, UnitCatalog.Ere, 0, 2), Opening(2, UnitCatalog.Gald, 0, 0) },
                new[] { new BattleEvent { Kind = BattleEventKind.Dirge, Turn = 1, ActorId = 1, TargetId = 2, Slot = 1, Text = DirgeLabels.Gift } }, "停止中の哀歌");
            for (int i = 0; i < 240 && pausedMain.ViewerField.DirgeGiftPlays == 0; i++) await Frame();
            Require(pausedMain.ViewerField.DirgeGiftPlays == 1, "本番経路の転送開始");
            pausedMain.PausePlayback();
            var held = pausedMain.ViewerField.GatherEffects.Single(); float heldAge = held.Age;
            await Wait(.12); Require(held.Age == heldAge && pausedMain.IsPaused, "本番の一時停止が転送を止める");
            await pausedMain.StopViewerPlayback();
            Require(!held.Finished.Result && !pausedMain.ViewerField.GatherEffects.Any(), "停止中の中断でも待ち手を解放");
            pausedMain.QueueFree(); await Frame();

            var cue = FxCatalog.Find("live.ere.gift"); var fixture = FxLivePlayback.Create(cue);
            Require(fixture.Result.Events.Any(e => e.Kind == BattleEventKind.Dirge && e.Text == DirgeLabels.Act), "実台本の追加手番");
            var main = new Main { ViewerMode = true, ViewerFastPlayback = true };
            main.SetAnchorsPreset(Control.LayoutPreset.FullRect); AddChild(main); main.SetPlaybackSpeed(1000);
            for (int replay = 0; replay < 2; replay++)
            {
                main.EnterViewerLive(fixture, cue);
                for (int i = 0; i < 3000 && main.IsPlaying; i++) await Frame();
                Require(!main.IsPlaying && main.ViewerPlaybackError is null, "本番経路で実台本を最後まで再生");
                Require(main.ViewerField.DirgeGiftPlays == fixture.Result.Events.Count(e => e.Kind == BattleEventKind.Dirge && e.Text == DirgeLabels.Gift), "託した対象の件数一致");
                Require(main.ViewerField.DirgeActPlays == fixture.Result.Events.Count(e => e.Kind == BattleEventKind.Dirge && e.Text == DirgeLabels.Act), "動いた対象の件数一致");
                foreach (var pawn in main.ViewerField.Pawns.Values)
                {
                    var hp = fixture.Result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId
                        && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Revive);
                    if (hp is not null) Require(pawn.Hp == hp.HpAfter, "実台本の最終HP一致");
                }
                await main.StopViewerPlayback();
            }
            main.QueueFree(); await Frame();
            GD.Print("ERE_GIFT_CHECK_OK textless retained-light release scripts=12 parity actual-targets ally enemy mirror pause speed death interrupt replay hp cleanup");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static void CheckScripts()
    {
        int acts = 0, gifts = 0;
        foreach (var formation in new[] { Presets.BurnBard384(UnitCatalog.Ere), Presets.DeathBard382(UnitCatalog.Ere) })
        foreach (int stage in new[] { 0, 2, 3 })
        foreach (int seed in new[] { 0, 1 })
        {
            var players = BattleEngine.Materialize(formation, 0);
            var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, 1);
            var result = BattleEngine.Run(players, enemies, seed, verbose: true);
            var silentPlayers = BattleEngine.Materialize(formation, 0);
            var silentEnemies = BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, 1);
            var silent = BattleEngine.Run(silentPlayers, silentEnemies, seed, verbose: false);
            Require(result.PlayerWon == silent.PlayerWon && result.Turns == silent.Turns
                && result.PlayerSurvivors == silent.PlayerSurvivors
                && players.Concat(enemies).Select(u => (u.InstanceId, u.Hp, u.Slot, u.CurrentAttack))
                    .SequenceEqual(silentPlayers.Concat(silentEnemies).Select(u => (u.InstanceId, u.Hp, u.Slot, u.CurrentAttack))), "表示通知の有無で盤面が変わらない");
            var events = result.Events;
            int scheduled = events.Count(e => e.Kind == BattleEventKind.Dirge && e.Text == DirgeLabels.Gift);
            int performed = events.Count(e => e.Kind == BattleEventKind.Dirge && e.Text == DirgeLabels.Act);
            Require(performed == result.TallyByUnit.Values.Sum(t => t.R382EncoreTurns + t.R382EchoTurns), "実際に動いた数だけActを記録");
            Require(scheduled == result.TallyByUnit.Values.Sum(t => t.R382Songs + t.R382Echoes), "実際に控えた数だけGiftを記録");
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Kind != BattleEventKind.Dirge || e.Text != DirgeLabels.Act) continue;
                Require(events.Take(i).Any(p => p.Kind == BattleEventKind.Dirge && p.Text == DirgeLabels.Gift
                    && p.Turn == e.Turn && p.ActorId == e.ActorId && p.TargetId == e.TargetId && p.Slot == e.Slot), "受け渡しの後に同じ対象が動く");
                Require(events[i + 1].Kind == BattleEventKind.Highlight && events[i + 1].ActorId == e.TargetId
                    && result.Hands.Any(h => h.EventStart == i + 2 && h.ActorId == e.TargetId), "対象の実手番の直前にActを記録");
            }
            gifts += scheduled; acts += performed;
        }
        Require(gifts > 0 && acts > 0, "空の検査ではない");
        GD.Print($"ERE_GIFT_SCRIPT_CHECK gifts={gifts} acts={acts}");
    }
    private static DemoOpening Opening(int id, UnitDef u, int team, int slot) => new(id, team, u.Id, u.Name,
        slot, u.MaxHp, u.MaxHp, u.Attack, AttackPattern.Single, u.Advances, u.Traits);
    private async Task CaptureSequence(BattlefieldView3D field)
    {
        foreach (bool enemy in new[] { false, true })
        {
            field.BeginBattle(new[] { Opening(1, UnitCatalog.Ere, 0, 2), Opening(2, UnitCatalog.Gald, 0, 0),
                Opening(3, UnitCatalog.Gald, 1, 0) }, "エレの哀歌", 0);
            field.SetGatherClock(false, 1);
            var bard = field.FindPawn(1)!; var target = field.FindPawn(enemy ? 3 : 2)!;
            var opponent = field.FindPawn(enemy ? 2 : 3)!;
            async Task Play()
            {
                await Wait(.30);
                await field.ShowDirgeGift(bard, target, false).Finished;
                await Wait(.32);
                await field.ShowDirgeGift(bard, target, true).Impact;
                await field.Attack(target, opponent, AttackPattern.Single, new[] { opponent }, advance: true);
                await Wait(.55);
            }
            Task play = Play();
            string folder = ProjectSettings.GlobalizePath("res://../.tmp/ere-gift-textless/" + (enemy ? "enemy" : "ally"));
            System.IO.Directory.CreateDirectory(folder);
            for (int i = 0; i < 300 && !play.IsCompleted; i++)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(GetViewport().GetTexture().GetImage().SavePng($"{folder}/{i:D4}.png") == Error.Ok, "連続描画の保存");
            }
            Require(play.IsCompletedSuccessfully, "音の到着から実際の攻撃まで連続描画");
            field.EndGatherPresentation();
        }
        GD.Print("ERE_TEXTLESS_CLIP_OK ally enemy transfer ready release attack");
    }
    private async Task Snapshot(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        string folder = ProjectSettings.GlobalizePath("res://../.tmp/ere-gift-check");
        System.IO.Directory.CreateDirectory(folder);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(folder + "/" + name + ".png") == Error.Ok, "画像保存");
    }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); }
}
