using Godot;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

public partial class FxViewer
{
    private async Task VerifyRicaViewer()
    {
        _unit.Selected=_unitIds.IndexOf("rica")+1;Filter();
        foreach(bool mirror in new[]{false,true})
        foreach(int stage in new[]{0,1,2,3})
        foreach(bool idle in new[]{false,true})
        {
            var cue=FxCatalog.Find($"rica.stage{stage}."+(idle?"idle":"attack"));
            _mirror.SetPressedNoSignal(mirror);
            _speed.Selected=3; // 一覧・再生窓口は本番のまま4倍で検査する。
            await LoadCue(cue);await WaitForEnd();
            Require(_main.ViewerField.FindPawn(1)!.GraveStage==stage,"選択した覚醒段を表示");
            if(!idle)
            {
                int expected=stage<2?1:stage==2?2:3;
                Require(_main.ViewerField.RicaAttackClipPlays==expected,"段の全打を連番で再生");
                foreach(var e in _main.PlaybackEvents.Where(e=>e.Kind==BattleCore.BattleEventKind.Damage).GroupBy(e=>e.TargetId).Select(g=>g.Last()))
                    Require(_main.ViewerField.FindPawn(e.TargetId)!.Hp==e.HpAfter,"固定台本のHPと一致");
            }
            GD.Print($"FX_VIEWER_RICA_CUE_OK id={cue.Id} mirror={mirror}");
        }
        foreach(int stage in new[]{1,2,3})
        {
            if (!FxCatalog.All.Any(c => c.Id == $"rica.stage{stage}.idle.b")) continue;
            await LoadCue(FxCatalog.Find($"rica.stage{stage}.idle.b"));
            Require(_main.ViewerField.FindPawn(1)!.IdleLoopPlaying,"B候補の待機も再生");
            await WaitForEnd();
        }
        GD.Print($"FX_VIEWER_RICA_OK cues={FxCatalog.All.Count(c=>c.Id.StartsWith("rica.stage"))} mirrors pause-speed-shared");
        await QuitViewer();
    }

    // --fixed-fps 30 で動かし、2コマおきの15fpsをそのまま動画へ渡す。
    private async Task CaptureRicaViewer(bool idle = false)
    {
        string root=ProjectSettings.GlobalizePath("res://../output/"+(idle?"rica-fxviewer-idle":"rica-fxviewer"));Directory.CreateDirectory(root);
        _unit.Selected=_unitIds.IndexOf("rica")+1;Filter();
        _speed.Selected=1;_loop.SetPressedNoSignal(false);_mirror.SetPressedNoSignal(false);
        foreach(int stage in new[]{0,1,2,3})
        {
            string dir=Path.Combine(root,$"stage{stage}");Directory.CreateDirectory(dir);
            foreach(string old in Directory.GetFiles(dir,"f*.webp"))File.Delete(old);
            await LoadCue(FxCatalog.Find($"rica.stage{stage}."+(idle?"idle":"attack")));
            // 最長62コマ/16fpsの待機も2周を確認する。通常のCue保持時間は変更しない。
            if(idle)_main.ViewerHoldSeconds=8;
            int tick=0,frame=0;ulong deadline=Time.GetTicksMsec()+60000;
            do
            {
                await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);
                if(tick++%2==0)
                    Require(GetViewport().GetTexture().GetImage().SaveWebp(Path.Combine(dir,$"f{frame++:0000}.webp"),false,.85f)==Error.Ok,"動画コマ保存");
                if(_main.ViewerPlaybackError is{} error)throw error;
            }while(_main.IsPlaying && Time.GetTicksMsec()<deadline);
            Require(!_main.IsPlaying,"撮影の台本完走");
            File.WriteAllText(Path.Combine(dir,"capture.json"),$"{{\"frames\":{frame},\"fps\":15}}");
            GD.Print($"FX_VIEWER_RICA{(idle?"_IDLE":"")}_CAPTURE stage={stage} frames={frame} fps=15");
        }
        GD.Print($"FX_VIEWER_RICA{(idle?"_IDLE":"")}_CAPTURE_OK stages=4");await QuitViewer();
    }
}
