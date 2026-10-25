using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

public partial class FxViewer
{
    // 各段の同じ駒を10秒連続で撮る。先頭2秒は待機し、攻撃後も待機を撮り続ける。
    private async Task CaptureRicaFlow()
    {
        string root = ProjectSettings.GlobalizePath("res://../output/rica-fxviewer-flow");
        _unit.Selected = _unitIds.IndexOf("rica") + 1; Filter();
        _speed.Selected = 1; _loop.SetPressedNoSignal(false); _mirror.SetPressedNoSignal(false);
        _main.ViewerLeadInSeconds = 2;
        int[] stages={0,1,2,3};
        string? stageArg=OS.GetCmdlineUserArgs().FirstOrDefault(a=>a.StartsWith("--fx-rica-flow-stage="));
        if(stageArg is not null)
        {
            if(!int.TryParse(stageArg["--fx-rica-flow-stage=".Length..],out int selectedStage) || selectedStage is <0 or >3)
                throw new ArgumentException("--fx-rica-flow-stage は0〜3を指定する");
            stages=new[]{selectedStage};
        }
        foreach (int stage in stages)
        {
            string dir = Path.Combine(root, $"stage{stage}"); Directory.CreateDirectory(dir);
            foreach (string old in Directory.GetFiles(dir, "f*.webp")) File.Delete(old);
            await LoadCue(FxCatalog.Find($"rica.stage{stage}.attack"));
            var pawn = _main.ViewerField.FindPawn(1)!;
            ulong identity = pawn.GetInstanceId(); Vector3 home = pawn.GlobalPosition;
            var records = new List<object>(); int returned = 0, leading = 0;
            int expected = stage < 2 ? 1 : stage == 2 ? 2 : 3;
            for (int tick = 0; tick < 300; tick++)
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Require(_main.ViewerField.FindPawn(1)?.GetInstanceId() == identity, "待機と攻撃の間で駒を作り直さない");
                if (_main.ViewerPlaybackError is { } error) throw error;
                int frame = records.Count;
                using var shot = GetViewport().GetTexture().GetImage();
                Require(shot.SaveWebp(Path.Combine(dir, $"f{frame:0000}.webp"), false, .90f) == Error.Ok, "連続再生の撮影");
                Vector3 ground = pawn.GlobalPosition + Vector3.Up * BattlePawn3D.PortraitGroundYForCheck;
                Vector2 point = _main.ViewerField.GlobalPosition + pawn.GetViewport().GetCamera3D().UnprojectPosition(ground);
                float distance = pawn.GlobalPosition.DistanceTo(home);
                string phase = pawn.RicaLinkActive ? "link" : pawn.AttackClipPlaying ? "attack" : pawn.RicaClipName is not null ? "link"
                    : _main.ViewerField.RicaAttackClipPlays >= expected && distance < .05f && pawn.IdleLoopPlaying ? "returned"
                    : _main.ViewerField.RicaAttackClipPlays > 0 ? "return"
                    : distance > .05f ? "approach" : "idle";
                if (phase == "idle") leading++;
                if (phase == "returned") returned++;
                records.Add(new { frame, ground = new[] { point.X, point.Y }, phase,
                    approach = pawn.RicaApproachActive,
                    link = pawn.RicaLinkActive,
                    weaponTrail = pawn.RicaWeaponTrailVisible,
                    clip = pawn.RicaClipName, clipFrame = pawn.AttackClipFrame,
                    idleFrame = pawn.IdleLoopFrame, distance, plays = _main.ViewerField.RicaAttackClipPlays });
            }
            Require(!_main.IsPlaying && !pawn.AttackClipPlaying, "連撃を最後まで撮影");
            Require(_main.ViewerField.RicaAttackClipPlays == expected, "段に対応した全攻撃を実再生");
            Require(leading >= 25 && returned >= 30, "攻撃前と帰還後の待機を含む");
            Require(pawn.IdleLoopPlaying && pawn.RicaClipName is null && pawn.GlobalPosition.DistanceTo(home) < .05f,
                "同じ席の待機へ戻る");
            File.WriteAllText(Path.Combine(dir, "capture.json"), JsonSerializer.Serialize(
                new { fps = 30, frames = records.Count, leadSeconds = 2, stage, expected, returned, records },
                new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"FX_VIEWER_RICA_FLOW_CAPTURE stage={stage} frames={records.Count} attacks={expected} returned={returned}");
        }
        _main.ViewerLeadInSeconds = 0;
        GD.Print($"FX_VIEWER_RICA_FLOW_CAPTURE_OK stages={stages.Length} continuous idle-attack-return-idle");
        await QuitViewer();
    }
}
