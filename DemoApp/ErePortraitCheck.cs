using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 実台本の歌唱と通常攻撃を照合し、本番の再生経路で A → C → A を確認する。
public partial class ErePortraitCheck : Node
{
    private Main _main = null!;
    private Texture2D _idle = null!, _song = null!;

    public override async void _Ready()
    {
        try
        {
            var fixture = CheckSongs();
            var ereId = fixture.Openings.Single(o => o.UnitId == "ere" && o.Team == 0).InstanceId;
            int index = Enumerable.Range(0, fixture.Result.Events.Count).First(i =>
                fixture.Result.Events[i].ActorId == ereId && ErePresentation.IsSongSkill(fixture.Result.Events, i));
            var skill = fixture.Result.Events[index];
            var highlight = fixture.Result.Events.Skip(index + 1).First(e => e.ActorId == ereId && e.Kind == BattleEventKind.Highlight);
            var events = new[] { skill, highlight };
            _main = new Main { ViewerMode = true };
            _main.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(_main);
            var atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");
            _idle = UiKit.BattlePortrait(atlas, "ere");
            _song = UiKit.BattlePortrait(atlas, "ere_singing");
            Require(_idle != _song, "待機Aと歌唱Cの分離");
            _main.EnterScripted(fixture.Openings, events, "エレ：待機A → 歌唱C");
            var pawn = _main.ViewerField.FindPawn(ereId)!;
            Check(pawn, _idle);
            await Snapshot("battle-idle-a-game-check.png");
            await Until(() => pawn.DirgeSinging, "台本の哀歌でCへ変更");
            Check(pawn, _song);
            _main.PausePlayback();
            Require(!_main.ViewerField.FindChildren("*", "", true, false).OfType<PopupLabel2D>().Any(), "歌唱にSKILLテロップを重ねない");
            await Snapshot("battle-song-c-game-check.png");
            await Wait(0.90);
            Require(pawn.DirgeSinging, "停止中はCを保持");
            Check(pawn, _song);
            _main.SetPlaybackSpeed(4);
            _main.ResumePlayback();
            await Until(() => !pawn.DirgeSinging, "速度変更後にAへ復帰");
            Check(pawn, _idle);
            await _main.StopViewerPlayback();

            _main.SetPlaybackSpeed(1);
            _main.EnterScripted(fixture.Openings, events, "エレ：歌唱中の再戦");
            var oldPawn = _main.ViewerField.FindPawn(ereId)!;
            await Until(() => oldPawn.DirgeSinging, "再戦前の歌唱");
            // 歌っている最中に同じ盤面を作り直しても、古い復帰が新しい駒へ触れない。
            _main.EnterScripted(fixture.Openings, Array.Empty<BattleEvent>(), "エレ：待機へリセット");
            pawn = _main.ViewerField.FindPawn(ereId)!;
            await Wait(0.10);
            Require(!pawn.DirgeSinging, "再戦で歌唱を持ち越さない");
            Check(pawn, _idle);
            pawn.SetDirgeSinging(true);
            pawn.AnimateDeath();
            Require(!pawn.DirgeSinging, "死亡で歌唱解除");
            Check(pawn, _idle);
            pawn.SetDirgeSinging(true);
            Require(!pawn.DirgeSinging, "死亡中は歌唱不可");
            pawn.AnimateRevive();
            Check(pawn, _idle);
            pawn.SetDirgeSinging(true);
            pawn.AnimateVictory();
            await Wait(0.50);
            Require(!pawn.DirgeSinging, "勝利で歌唱解除");
            Check(pawn, UiKit.Portrait(atlas, "ere"));
            await _main.StopViewerPlayback();

            var enemy = new BattlePawn3D();
            AddChild(enemy);
            enemy.Configure(fixture.Openings.Single(o => o.InstanceId == ereId) with { Team = 1 }, atlas);
            enemy.SetDirgeSinging(true);
            Check(enemy, _song);
            Require(enemy.GetChildren().OfType<Sprite3D>().Single().FlipH, "敵側の歌唱は左右反転");
            enemy.AnimateDeath();
            Require(!enemy.DirgeSinging, "敵側も死亡で歌唱解除");
            enemy.QueueFree();
            _main.QueueFree();
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            GD.Print("ERE_PORTRAIT_CHECK_OK actual-song fallback idle singing pause speed replay death revive victory enemy");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }

    private static FxLivePlayback CheckSongs()
    {
        FxLivePlayback? fixture = null;
        int songs = 0, fallback = 0;
        foreach (var formation in new[] { Presets.BurnBard384(UnitCatalog.Ere), Presets.DeathBard382(UnitCatalog.Ere) })
        foreach (int stage in new[] { 0, 2, 3 })
        {
            var players = BattleEngine.Materialize(formation, 0);
            var enemies = BattleEngine.Materialize(EnemyCatalog.Stages[stage].Enemy, 1);
            var pending = players.Concat(enemies).Select(u => new {
                Unit = u, u.Hp, u.MaxHp, u.Slot, Attack = u.CurrentAttack, Pattern = u.CurrentPattern }).ToArray();
            var result = BattleEngine.Run(players, enemies, 0, verbose: true);
            var openings = pending.Select(x => new DemoOpening(x.Unit.InstanceId, x.Unit.TeamId,
                x.Unit.Def.Id, x.Unit.Name, x.Slot, x.Hp, x.MaxHp, x.Attack, x.Pattern,
                x.Unit.Def.Advances, x.Unit.Def.Traits, x.Unit.Shape)).ToArray();
            int ere = openings.Single(o => o.UnitId == "ere" && o.Team == 0).InstanceId;
            int actual = Enumerable.Range(0, result.Events.Count).Count(i =>
                result.Events[i].ActorId == ere && ErePresentation.IsSongSkill(result.Events, i));
            Require(actual == result.TallyByUnit["ere"].R382Songs, "実台本の歌唱判定が戦闘の歌唱件数と一致");
            songs += actual;
            fallback += result.Events.Count(e => e.Kind == BattleEventKind.Skill && e.ActorId == ere) - actual;
            if (actual > 0) fixture ??= new(openings, result);
        }
        Require(fixture is not null && songs > 0 && fallback > 0, "奏でる手番と通常攻撃の両方を実戦で検査");
        GD.Print($"ERE_SONG_SCRIPT_CHECK songs={songs} fallback={fallback} battles=6");
        return fixture!;
    }

    private async Task Until(Func<bool> ready, string label)
    {
        for (int i = 0; i < 400 && !ready(); i++) await Wait(0.01);
        Require(ready() && _main.ViewerPlaybackError is null, label);
    }

    private async Task Snapshot(string file)
    {
        if (!OS.GetCmdlineUserArgs().Contains("--capture")) return;
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        Require(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath(
            "res://../design/art/ere/" + file)) == Error.Ok, "実画面の保存");
    }

    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private void Check(BattlePawn3D pawn, Texture2D expected)
    {
        var sprite = pawn.GetChildren().OfType<Sprite3D>().Single();
        string? loop = expected == _idle ? "ere" : expected == _song ? "ere_singing" : null;
        var frames = loop is null ? Array.Empty<Texture2D>() : BattlePawn3D.IdleLoopFramesOf(loop);
        Require(pawn.IdleLoopPlaying && frames.Length > 0 ? frames.Contains(sprite.Texture) : sprite.Texture == expected,
            "実際のSprite3Dの画像または対応するモーション");
        Require(((ShaderMaterial)sprite.MaterialOverride).GetShaderParameter("portrait_texture").AsGodotObject() == sprite.Texture,
            "シェーダーの画像");
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }
}
