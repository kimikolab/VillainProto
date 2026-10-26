using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

// capture-integration-v2.gdからだけ呼ぶ、本番と同じ台本・描画経路の確認。
public partial class Main
{
    public async void RunSekkiV2Check()
    {
        try
        {
            var events = _result!.Events.ToArray();
            var openings = _battleOpening.ToArray();
            int id = openings.Single(o => o.UnitId == "sekki" && o.Team == 0).InstanceId;
            int wraps = events.Count(e => e.Kind == BattleEventKind.Slime && e.Text == SlimeLabels.Wrap);
            var options = new JsonSerializerOptions { WriteIndented = true };
            options.Converters.Add(new JsonStringEnumConverter());
            using (var file = Godot.FileAccess.Open("res://../design/art/sekki/replay-v2-stage4.json", Godot.FileAccess.ModeFlags.Write))
                file.StoreString(JsonSerializer.Serialize(events, options));

            SetPlaybackSpeed(4);
            ReplayBattle();
            var pawn = _battleField.FindPawn(id)!;
            var atlas = UiKit.LoadTexture("res://assets/outcast_atlas.png");
            var idle = UiKit.BattlePortrait(atlas, "sekki");
            var wrap = UiKit.BattlePortrait(atlas, "sekki_wrap");
            SekkiCheckTexture(pawn, idle);
            await SekkiUntil(() => pawn.SekkiWrapping, "実台本の最初の包む");
            SekkiCheckTexture(pawn, wrap);
            SekkiRequire(_sekkiWrapPlays == 1, "最初の一振りは1回");
            PausePlayback();
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            SekkiRequire(GetViewport().GetTexture().GetImage().SavePng(ProjectSettings.GlobalizePath(
                "res://../design/art/sekki/battle-wrap-v2-game-check.png")) == Error.Ok, "包むの実画面撮影");
            await SekkiWait(0.65);
            SekkiRequire(pawn.SekkiWrapping, "停止中は0.5秒の時計も停止");
            SekkiCheckTexture(pawn, wrap);
            SetPlaybackSpeed(2);
            ResumePlayback();
            double resumed = Time.GetTicksMsec();
            await SekkiUntil(() => !pawn.SekkiWrapping, "倍速で待機へ戻る");
            double resumedMs = Time.GetTicksMsec() - resumed;
            SekkiRequire(resumedMs < 420, "途中の速度変更に追従");
            SekkiCheckTexture(pawn, idle);
            GD.Print($"SEKKI_V2_CLOCK pause=ok speed=2 returnMs={resumedMs}");
            SetPlaybackSpeed(4);
            await SekkiUntil(() => !_playing, "実台本の完走", 6000);
            int beats = _sekkiWrapEvents.Values.Select(c => (c.Beat, c.Actor)).Distinct().Count();
            SekkiRequire(_sekkiWrapEventCount == wraps && _sekkiWrapPlays == beats && wraps > beats,
                "全Wrapと一振りごとの表示の照合");
            SekkiRequire(_sekkiWrapActive.Count == 0 && !pawn.SekkiWrapping, "戦闘終了時に残留しない");
            GD.Print($"SEKKI_V2_ACTUAL_SCRIPT events={events.Length} wraps={wraps} damageLinks={_sekkiWrapDamage.Values.Sum(c => c.Count)} beats={beats} plays={_sekkiWrapPlays}");

            EnterScripted(openings, events, "セッキ v2 状態検証", 4);
            pawn = _battleField.FindPawn(id)!;
            await SekkiUntil(() => pawn.SekkiWrapping, "再戦前の包む");
            ReplayBattle();
            PausePlayback();
            pawn = _battleField.FindPawn(id)!;
            SekkiRequire(!pawn.SekkiWrapping && _sekkiWrapActive.Count == 0, "再戦は待機で開始");
            await SekkiWait(0.65);
            SekkiRequire(!pawn.SekkiWrapping, "古い復帰予約が新盤面へ残らない");
            SekkiCheckTexture(pawn, idle);
            ResumePlayback();
            await SekkiUntil(() => pawn.SekkiWrapping, "中断前の包む");
            ReturnToFormation();
            SekkiRequire(!pawn.SekkiWrapping && _sekkiWrapActive.Count == 0, "編成への中断で解除");

            EnterScripted(openings, Array.Empty<BattleEvent>(), "セッキ v2 生死検証", 4);
            PausePlayback();
            pawn = _battleField.FindPawn(id)!;
            pawn.SetSekkiWrapping(true);
            SekkiCheckTexture(pawn, wrap);
            pawn.AnimateDeath();
            SekkiRequire(!pawn.SekkiWrapping, "死亡で解除");
            SekkiCheckTexture(pawn, idle);
            pawn.SetSekkiWrapping(true);
            SekkiRequire(!pawn.SekkiWrapping, "死亡中は差分へ移れない");
            pawn.AnimateRevive();
            SekkiRequire(!pawn.SekkiWrapping, "蘇生は待機");
            pawn.SetSekkiWrapping(true);
            pawn.AnimateVictory();
            SekkiRequire(!pawn.SekkiWrapping, "勝利で解除");
            await StopViewerPlayback();
            SekkiRequire(_sekkiWrapActive.Count == 0, "停止した再生の予約を持ち越さない");

            var otherSlime = events.Where(e => e.Kind == BattleEventKind.Slime && e.Text != SlimeLabels.Wrap).ToArray();
            SekkiRequire(otherSlime.Length > 0, "他のSlime通知も実台本から検査");
            EnterScripted(openings, otherSlime, "セッキ v2 他通知は素通り", 4);
            await SekkiUntil(() => !_playing, "Swell/Burst/Idleだけの完走");
            SekkiRequire(_sekkiWrapPlays == 0 && _sekkiWrapEventCount == 0, "Wrap以外で差分を出さない");
            GD.Print("SEKKI_V2_STATE_COMPLETE pause speed replay interrupt death revive victory otherSlime=ok");
            GetTree().Quit();
        }
        catch (Exception ex)
        {
            GD.PushError("セッキv2検証失敗: " + ex);
            GetTree().Quit(1);
        }
    }

    private async Task SekkiUntil(Func<bool> ready, string label, int frames = 1200)
    {
        for (int i = 0; i < frames && !ready(); i++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            SekkiRequire(ViewerPlaybackError is null, label + ": 再生エラー");
        }
        SekkiRequire(ready(), label);
    }

    private async Task SekkiWait(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private static void SekkiCheckTexture(BattlePawn3D pawn, Texture2D expected)
    {
        var sprite = pawn.GetChildren().OfType<Sprite3D>().Single();
        SekkiRequire(sprite.Texture == expected, "本番Sprite3Dの画像");
        SekkiRequire(((ShaderMaterial)sprite.MaterialOverride).GetShaderParameter("portrait_texture").AsGodotObject() == expected,
            "本番シェーダーの画像");
    }

    private static void SekkiRequire(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
    }
}
