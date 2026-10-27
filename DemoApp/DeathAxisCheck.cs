using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

// 実値の副台本が戦闘を変えないことと、5台×3波の層を取りこぼさないことを検査する。
public partial class DeathAxisCheck : Control
{
    private BattlefieldView3D _field = null!;
    public override async void _Ready()
    {
        try
        {
            var fixture = CheckScripts();
            _field = new BattlefieldView3D(); _field.SetAnchorsPreset(LayoutPreset.FullRect); AddChild(_field);
            var openings = new[] {
                Opening(1, UnitCatalog.Rica, 0, 0), Opening(2, UnitCatalog.Mug, 0, 3),
                Opening(3, UnitCatalog.Vel, 0, 2), Opening(4, UnitCatalog.Ere, 0, 4),
                Opening(5, UnitCatalog.Wight, 0, 5), Opening(6, UnitCatalog.Spore, 0, 6),
                Opening(7, EnemyCatalog.Recruit, 1, 0), Opening(8, EnemyCatalog.Recruit, 1, 1) };
            _field.BeginBattle(openings, "", 0);
            var rica = _field.FindPawn(1)!;
            foreach (int layers in new[] { 0, 3, 6, 9, 18 })
            {
                _field.ApplyDeathState(new(1, layers, 0, 0, rica.MaxHp));
                await Wait(0.25); await Capture("rica-" + layers);
                Require(rica.GraveStage == DeathPresentation.Stage(layers), "段と実層の対応");
            }
            Require(_field.GraveAwakenings == 3, "覚醒は3段・18層で粒を増やさない");
            _field.ApplyDeathState(new(1, 8, 0, 0, rica.MaxHp));
            Require(rica.GraveStage == 2, "薄れる層を写しから戻す");
            _field.ShowDeathOffering(_field.FindPawn(6)!);
            var aura = rica.GetChildren().OfType<DeathAura3D>().Single();
            _field.SetDeathClock(true, 1); double before = aura.Age;
            await Wait(0.12); Require(aura.Age == before, "停止中の燐光");
            _field.SetDeathClock(false, 2); await Wait(0.10);
            Require(aura.Age - before > 0.13, "倍速の燐光");
            _field.SetPoisonClock(false, 2); rica.AnimationSpeed = 2;
            _field.ApplyDeathState(new(1, 18, 0, 0, rica.MaxHp));
            var target = _field.FindPawn(7)!;
            await _field.Attack(rica, target, AttackPattern.Sweep, new[] { target }, holdPosition: true);
            Vector3 first = rica.Position;
            await _field.Attack(rica, _field.FindPawn(8)!, AttackPattern.Sweep, new[] { _field.FindPawn(8)! }, advance: false, holdPosition: true);
            Require(rica.Position.DistanceTo(first) > 0.2f && _field.GraveSlashes == 2, "連撃の実対象へ跳ぶ");
            await Wait(0.08); await Capture("rica-chain");
            _field.ShowDirgeGift(_field.FindPawn(4)!, rica, true);
            _field.ShowDirgeGift(_field.FindPawn(4)!, target, true);
            _field.ShowDeathSummon(_field.FindPawn(3), _field.FindPawn(5)!);
            _field.ApplyDeathState(new(2, 0, 3, 0, 45, true));
            await Wait(0.25); await Capture("death-loop");
            _field.PlayDeath(rica); await Wait(0.05); Require(!aura.Visible, "死亡で燐光解除");
            _field.RevivePawn(rica); rica.SetHp(20); await Wait(0.05);
            Require(aura.Visible, "復帰の燐光");
            rica.AnimateVictory(); await Wait(0.05); Require(!aura.Visible, "勝利で燐光解除");
            _field.BeginBattle(openings, "", 0);
            Require(_field.GraveAwakenings == 0 && _field.FindPawn(1)!.GraveLayers == 0, "再戦で層を破棄");
            _field.QueueFree(); await Wait(0.02);
            var main = new Main { ViewerMode = true }; AddChild(main);
            var cue = new FxCue("death.check", "死の連鎖", "死", new[] { "rica" }, FxCueKind.Live);
            main.ViewerFastPlayback = true; main.SetPlaybackSpeed(1000);
            main.EnterViewerLive(fixture, cue);
            for (int frame = 0; frame < 2400 && main.IsPlaying; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(!main.IsPlaying && main.ViewerPlaybackError is null, "実台本の再生完了");
            Require(main.ViewerField.GraveAwakenings >= 3 && main.ViewerField.GraveOfferings > 0
                && main.ViewerField.GraveSlashes > 0 && main.ViewerField.DeathSongs > 0, "実台本の覚醒・吸収・斬撃・哀歌が接続");
            GD.Print($"DEATH_REPLAY_OK awaken={main.ViewerField.GraveAwakenings} offerings={main.ViewerField.GraveOfferings} slashes={main.ViewerField.GraveSlashes} songs={main.ViewerField.DeathSongs}");
            foreach (var pawn in main.ViewerField.Pawns.Values)
            {
                var hp = fixture.Result.Events.LastOrDefault(e => e.TargetId == pawn.InstanceId && e.Kind is BattleEventKind.Damage or BattleEventKind.Heal or BattleEventKind.Revive);
                if (hp is not null) Require(pawn.Hp == hp.HpAfter, "実台本の最終HP一致");
            }
            int offerings = main.ViewerField.GraveOfferings, slashes = main.ViewerField.GraveSlashes, songs = main.ViewerField.DeathSongs;
            main.ResumePlayback();
            for (int frame = 0; frame < 2400 && main.IsPlaying; frame++)
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Require(!main.IsPlaying && main.ViewerPlaybackError is null
                && main.ViewerField.GraveOfferings == offerings && main.ViewerField.GraveSlashes == slashes
                && main.ViewerField.DeathSongs == songs, "同じ副台本を再戦でも最初から再生");
            GD.Print("DEATH_REPLAY_AGAIN_OK");
            await main.StopViewerPlayback(); main.QueueFree(); await Wait(0.25);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GC.Collect(); GC.WaitForPendingFinalizers();
            GD.Print("DEATH_AXIS_CHECK_OK scripts=15 parity peaks soul sound stages pause speed jump death revive victory replay");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private static DemoOpening Opening(int id, UnitDef u, int team, int slot) => new(id, team, u.Id, u.Name,
        slot, u.MaxHp, u.MaxHp, u.Attack, AttackPattern.Single, u.Advances, u.Traits);
    private static FxLivePlayback CheckScripts()
    {
        FxLivePlayback? fixture = null;
        string[] names = { "試遊・死の連鎖", "試遊・死 探索1位", "試遊・死 ソム", "試遊・死 エレ", "試遊・燃焼 エレ" };
        foreach (string name in names)
        foreach (var stage in EnemyCatalog.PlaytestStages)
        {
            var preset = Presets.Playtest.Concat(Presets.Versions).First(x => x.Name == name);
            var players = BattleEngine.Materialize(preset.F, 0);
            var enemies = BattleEngine.MaterializeEnemy(stage.Enemy, stage.Scale);
            var pending = players.Concat(enemies).Select(u => new { Unit = u, u.Hp, u.MaxHp, u.Slot, Attack = u.CurrentAttack, Pattern = u.CurrentPattern }).ToArray();
            var track = DeathPresentation.Record(players, enemies, 0, out var result);
            if (name == "試遊・死 エレ" && stage == EnemyCatalog.PlaytestStages[0])
                fixture = new(pending.Select(x => new DemoOpening(x.Unit.InstanceId, x.Unit.TeamId, x.Unit.Def.Id,
                    x.Unit.Name, x.Slot, x.Hp, x.MaxHp, x.Attack, x.Pattern, x.Unit.Def.Advances, x.Unit.Def.Traits, x.Unit.Shape)).ToArray(), result) { Death = track };
            var plain = BattleEngine.Run(BattleEngine.Materialize(preset.F, 0), BattleEngine.MaterializeEnemy(stage.Enemy, stage.Scale), 0, verbose: true);
            Require(JsonSerializer.Serialize(result.Events) == JsonSerializer.Serialize(plain.Events)
                && result.PlayerWon == plain.PlayerWon && result.Turns == plain.Turns, "観測の有無で台本と結果が一致");
            foreach (var u in players)
            {
                var frames = track.Frames.Values.SelectMany(x => x).Where(x => x.Id == u.InstanceId).ToArray();
                Require(frames.Length > 0, "駒の実値を記録");
                Require(frames[^1].Layers == u.RawCounter("necro") && frames[^1].Lives == u.RawCounter(VelSoulTrait.LivesKey)
                    && frames[^1].Sound == u.RawCounter(DirgeSoundTrait.SoundKey), "最後の実値一致");
                if (u.Def.Id == "rica") Require(frames.Max(x => x.Layers) == result.TallyByUnit["rica"].NecroPeak, "墓守の最大層を取りこぼさない");
            }
            GD.Print($"DEATH_SCRIPT_OK {name} / {stage.Name} frames={track.Frames.Count}");
        }
        return fixture!;
    }
    private async Task Wait(double s) => await ToSignal(GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout);
    private async Task Capture(string name)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        string dir = ProjectSettings.GlobalizePath("res://../output/death-axis"); System.IO.Directory.CreateDirectory(dir);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(dir, name + ".png"));
    }
    private static void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
}
