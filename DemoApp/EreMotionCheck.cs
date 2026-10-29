using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

// 候補を本番と同じ再生経路で比較。--installed では採用済みの ere / ere_singing を通常通り読み込む。
public partial class EreMotionCheck : Node
{
    private BattlefieldView3D _field = null!;
    private Texture2D[] _idle = Array.Empty<Texture2D>(), _song = Array.Empty<Texture2D>();
    private bool _installed;
    public override async void _Ready()
    {
        try
        {
            string Arg(string name) => OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith(name + "="))
                ?.Substring(name.Length + 1) ?? throw new InvalidOperationException(name + " が必要です");
            Texture2D[] Load(string dir) => DirAccess.GetFilesAt(dir)
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal)
                .Select(f => UiKit.LoadTexture(dir + "/" + f, mipmaps: true)).ToArray();
            _installed = OS.GetCmdlineUserArgs().Contains("--installed");
            _idle = _installed ? BattlePawn3D.IdleLoopFramesOf("ere") : Load(Arg("--idle-dir"));
            _song = _installed ? BattlePawn3D.IdleLoopFramesOf("ere_singing") : Load(Arg("--song-dir"));
            Require(_idle.Length >= 16 && _song.Length >= 16, "待機と演奏の連続したコマ");
            Require(_idle.Concat(_song).All(t => t.GetSize() == new Vector2(640, 960)), "640×960の同じキャンバス");
            _field = new BattlefieldView3D(); _field.SetAnchorsPreset(Control.LayoutPreset.FullRect); AddChild(_field);
            _field.BeginBattle(new[] {
                new DemoOpening(1,0,"ere","エレ",2,66,66,8,AttackPattern.Single,false),
                new DemoOpening(2,1,"ere","エレ",2,66,66,8,AttackPattern.Single,false),
            }, "エレ：待機と演奏のモーション比較", 0);
            if (!_installed) foreach (var p in _field.Pawns.Values) p.PreviewEreMotion(_idle, _song);
            foreach (var pawn in _field.Pawns.Values)
            {
                Check(pawn, _idle, "開戦から待機");
                int first = pawn.IdleLoopFrame; pawn.ProcessIdleLoop(.12);
                Require(pawn.IdleLoopFrame != first, "待機のコマが進む");
                pawn.SetDirgeSinging(true); Check(pawn, _song, "哀歌で演奏へ");
                Require(pawn.IdleLoopFrame == 0, "演奏は先頭から始まる");
                pawn.ProcessIdleLoop(.12); int normal = pawn.IdleLoopFrame;
                pawn.ProcessIdleLoop(_song.Length / 16.0 + .3);
                Require(pawn.IdleLoopFrame == _song.Length - 1, "演奏は1回で終端を保ち、待機のように巡回しない");
                pawn.SetDirgeSinging(false); pawn.SetDirgeSinging(true); pawn.AnimationSpeed = 2;
                pawn.ProcessIdleLoop(.12);
                Require(pawn.IdleLoopFrame > normal, "演奏も倍速に追従");
                pawn.AnimationSpeed = 1;
                _field.ProcessMode = ProcessModeEnum.Disabled;
                int held = pawn.IdleLoopFrame; await Wait(.12);
                Require(pawn.IdleLoopFrame == held, "停止中は演奏のコマを保つ");
                _field.ProcessMode = ProcessModeEnum.Inherit;
                pawn.SetDirgeSinging(false); Check(pawn, _idle, "演奏から待機へ戻る");
                pawn.SetDirgeSinging(true); pawn.AnimateDeath(); var dead = Sprite(pawn).Texture;
                pawn.ProcessIdleLoop(.4);
                Require(!pawn.DirgeSinging && !pawn.IdleLoopPlaying && Sprite(pawn).Texture == dead, "死亡で演奏を止める");
                pawn.AnimateRevive(); Check(pawn, _idle, "蘇生後は待機");
            }
            string? capture = OS.GetCmdlineUserArgs().FirstOrDefault(a => a.StartsWith("--capture-dir="))?.Substring(14);
            if (capture is not null)
            {
                System.IO.Directory.CreateDirectory(capture);
                for (int i = 0; i < 112; i++)
                {
                    if (i == 24) foreach (var p in _field.Pawns.Values) p.SetDirgeSinging(true);
                    if (i == 88) foreach (var p in _field.Pawns.Values) p.SetDirgeSinging(false);
                    await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    Require(GetViewport().GetTexture().GetImage().SavePng($"{capture}/{i:D4}.png") == Error.Ok, "ゲーム上の連続描画");
                }
            }
            var winner = _field.FindPawn(1)!;
            winner.SetDirgeSinging(true); winner.AnimateVictory(); await Wait(.5);
            Require(!winner.DirgeSinging && !winner.IdleLoopPlaying, "勝利で演奏を止める");
            _field.EndGatherPresentation(); _field.QueueFree(); await Frame();
            await CheckMainPlayback();
            if (!_installed) foreach (var frame in _idle.Concat(_song)) frame.Dispose();
            GD.Print($"ERE_MOTION_CHECK_OK installed={_installed} idle={_idle.Length} song={_song.Length} sprite shader ground mirror speed pause death revive victory handoff");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
    private async Task CheckMainPlayback()
    {
        var main = new Main { ViewerMode = true, ViewerLeadInSeconds = .15 };
        main.SetAnchorsPreset(Control.LayoutPreset.FullRect); AddChild(main); main.SetPlaybackSpeed(1);
        BattleEvent Cue(BattleEventKind kind, int actor = 1, int? target = null, string? text = null, int slot = 0)
            => new() { Turn = 1, Kind = kind, ActorId = actor, TargetId = target, Text = text, Slot = slot };
        main.EnterScripted(new[] {
            new DemoOpening(1,0,"ere","エレ",2,66,66,8,AttackPattern.Single,false),
            new DemoOpening(2,0,"gald","味方",0,100,100,8,AttackPattern.Single,false),
            new DemoOpening(3,1,"gald","敵",0,100,100,8,AttackPattern.Single,false),
        }, new[] {
            Cue(BattleEventKind.Skill, text:"哀歌"),
            Cue(BattleEventKind.Dirge, target:2, text:DirgeLabels.Gift, slot:1),
            Cue(BattleEventKind.Highlight, text:"哀歌"),
            Cue(BattleEventKind.Dirge, target:3, text:DirgeLabels.Gift, slot:2),
            Cue(BattleEventKind.Dirge, target:2, text:DirgeLabels.Act, slot:1),
            Cue(BattleEventKind.Highlight, actor:2, text:"哀歌"),
            Cue(BattleEventKind.Attack, actor:2, target:3),
            Cue(BattleEventKind.Dirge, target:3, text:DirgeLabels.Act, slot:2),
            Cue(BattleEventKind.Highlight, actor:3, text:"哀歌"),
            Cue(BattleEventKind.Attack, actor:3, target:2),
            Cue(BattleEventKind.Skill, text:"哀歌"),
            Cue(BattleEventKind.Dirge, target:2, text:DirgeLabels.Gift, slot:1),
            Cue(BattleEventKind.Dirge, target:2, text:DirgeLabels.Act, slot:1),
            Cue(BattleEventKind.Highlight, actor:2, text:"哀歌"),
            Cue(BattleEventKind.Attack, actor:2, target:3),
        }, "哀歌のモーションと手番の受け渡し");
        var singer = main.ViewerField.FindPawn(1)!;
        if (!_installed) singer.PreviewEreMotion(_idle, _song);
        for (int i = 0; i < 2000 && !main.ViewerField.DirgePerformances.Any() && main.ViewerPlaybackError is null; i++) await Wait(.005);
        var performance = main.ViewerField.DirgePerformances.SingleOrDefault();
        Require(performance is not null && singer.DirgeSinging && main.ViewerField.DirgeGiftPlays == 0,
            "本番で演奏開始と同時に光と音符を出し、ギフト前の3秒を空白にしない");
        bool hasSongs = ResourceLoader.Exists("res://assets/audio/se/" + BattlefieldView3D.DirgeSongA)
            && ResourceLoader.Exists("res://assets/audio/se/" + BattlefieldView3D.DirgeSongB);
        Require(!hasSongs || main.ViewerField.LastDirgeSong == BattlefieldView3D.DirgeSongA
            && main.ViewerField.DirgeMusic is not null, "本番の最初の演奏はA");
        main.PausePlayback();
        float songAge = performance!.Age; int songFrame = singer.IdleLoopFrame;
        await Wait(.12);
        Require(performance.Age == songAge && singer.IdleLoopFrame == songFrame, "本番の停止で演奏Cと開始の光を一緒に止める");
        main.ResumePlayback();
        for (int i = 0; i < 2000 && main.ViewerField.DirgeGiftPlays < 2 && main.ViewerPlaybackError is null; i++) await Wait(.005);
        Require(main.ViewerField.DirgeGiftPlays == 2 && main.ViewerField.DirgeActPlays == 0 && main.ViewerPlaybackError is null,
            "本番経路で1フレーズの後に2体へ音を渡す");
        main.PausePlayback();
        Require(singer.DirgeSinging && singer.IdleLoopFrame == _song.Length - 1,
            "2体への受け渡しでも演奏を先頭へ巻き戻さず終端の姿勢を保つ");
        var earlier = main.ViewerField.GatherEffects.FirstOrDefault(f => f != main.ViewerField.DirgeMusic && f.Age >= .5f);
        Require(earlier is not null, "先の受け渡しの余韻");
        earlier!.SetClock(false, 1); earlier._Process(.3);
        Require(singer.DirgeSinging, "先の受け渡しの後片付けが次の歌唱を消さない");
        Require(!main.ViewerField.DirgePerformances.Any(), "演奏の光は受け渡しへの余韻で消える");
        main.ResumePlayback();
        for (int i = 0; i < 2000 && main.IsPlaying && main.ViewerPlaybackError is null; i++) await Wait(.005);
        Require(!main.IsPlaying && main.ViewerPlaybackError is null && !singer.DirgeSinging,
            "追加手番の開始・再生終了で待機に戻る");
        Require(!hasSongs || main.ViewerField.LastDirgeSong == BattlefieldView3D.DirgeSongB,
            "同じ戦闘の2回目の演奏はB");
        await main.StopViewerPlayback(); main.QueueFree(); await Frame();
    }
    private static Sprite3D Sprite(BattlePawn3D pawn) => pawn.GetChildren().OfType<Sprite3D>().Single();
    private static void Check(BattlePawn3D pawn, Texture2D[] frames, string label)
    {
        var sprite = Sprite(pawn);
        Require(pawn.IdleLoopPlaying && frames.Contains(sprite.Texture), label);
        Require(((ShaderMaterial)sprite.MaterialOverride).GetShaderParameter("portrait_texture").AsGodotObject() == sprite.Texture, "シェーダーも同じコマ");
        Require(sprite.FlipH == (pawn.Team == 1), "敵側は左右反転");
        Require(Math.Abs(sprite.Texture.GetHeight() * sprite.PixelSize - UiKit.PortraitWorldHeight("ere")) < .001f, "元絵と同じ体格");
        float pad = UiKit.BattlePortraitBottomPaddingRatio(pawn.DirgeSinging ? "ere_singing" : "ere");
        Require(Math.Abs(sprite.Position.Y - 2.5f * (.5f - pad) - BattlePawn3D.PortraitGroundYForCheck) < .001f, "元絵と同じ接地");
    }
    private async Task Frame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool ok, string label) { if (!ok) throw new InvalidOperationException(label); }
}
