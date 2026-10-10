using BattleCore;
using Godot;
using System;
using System.Threading.Tasks;

public partial class AssetPreview
{
    private async Task PlayHotaChain(int generation)
    {
        try
        {
            _field.AssetPreviewFireballs = _effects;
            var actor = _field.FindPawn(1)!;
            var target = _field.FindPawn(4)!;
            for (int ordinal = 1; ordinal <= 5; ordinal++)
            {
                if (generation != _previewGeneration || !IsInsideTree()) return;
                await _field.PlayFireAttack(actor, target, new[] { target }, new(FireLevelLabels.EmbersHit, 1, ordinal), 1);
                if (generation != _previewGeneration || !IsInsideTree()) return;
                // 本編の残り火と同じく、命中後の通常攻撃用の待ちは挟まない。
            }
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            if (_capturing) GetTree().Quit(1);
        }
    }

    private async Task CaptureHota()
    {
        _capturing = true;
        string directory = ProjectSettings.GlobalizePath("res://../.tmp/asset-preview-captures");
        System.IO.Directory.CreateDirectory(directory);
        await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        _speed = 0.25;
        Engine.TimeScale = _speed;
        foreach (bool candidate in new[] { false, true })
        {
            _effects = candidate;
            UpdateLabels();
            int before = _field.FireEmbersHitPlays;
            RestartEffects();
            await ToSignal(GetTree().CreateTimer(0.055), SceneTreeTimer.SignalName.Timeout);
            await SaveCapture(directory + $"/hota-{(candidate ? "B" : "A")}-flight.png");
            // 発射中の停止では、命中の待ちも止まることを確認する。
            TogglePause();
            int impacts = _field.AssetPreviewFireballImpacts;
            await ToSignal(GetTree().CreateTimer(0.3, ignoreTimeScale: true), SceneTreeTimer.SignalName.Timeout);
            if (_field.AssetPreviewFireballImpacts != impacts) throw new InvalidOperationException("停止中に火球が命中しました。");
            TogglePause();
            await ToSignal(GetTree().CreateTimer(0.12), SceneTreeTimer.SignalName.Timeout);
            await SaveCapture(directory + $"/hota-{(candidate ? "B" : "A")}-impact.png");
            await ToSignal(GetTree().CreateTimer(0.24), SceneTreeTimer.SignalName.Timeout);
            await SaveCapture(directory + $"/hota-{(candidate ? "B" : "A")}-chain.png");
            await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
            if (_field.FireEmbersHitPlays - before != 5 || (candidate && _field.AssetPreviewFireballImpacts != 5))
                throw new InvalidOperationException("ホタの連撃が5発に一致しません。");
            if (_field.AssetPreviewEffects.GetChildCount() != 0) throw new InvalidOperationException("火球の粒子が残っています。");
        }
        _speed = 2;
        Engine.TimeScale = _speed;
        RestartEffects();
        await ToSignal(GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout);
        if (_field.AssetPreviewFireballImpacts != 5 || _field.AssetPreviewEffects.GetChildCount() != 0)
            throw new InvalidOperationException("倍速での命中・後片付けに失敗しました。");
        RestartEffects();
        // 飛行途中で切り替えても古い着弾を出さない。
        _hotaMode = false; _burnMode = true; _burnOn = false;
        RestartEffects();
        await ToSignal(GetTree().CreateTimer(1), SceneTreeTimer.SignalName.Timeout);
        if (_field.AssetPreviewFireballImpacts != 0 || _field.AssetPreviewEffects.GetChildCount() != 0)
            throw new InvalidOperationException("切り替え後に旧攻撃が残っています。");
        GD.Print("HOTA_FIREBALL_CAPTURE_COMPLETE: 5発・停止・倍速・後片付け・中断OK");
        GetTree().Quit();
    }
}
