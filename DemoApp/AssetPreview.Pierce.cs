using BattleCore;
using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

public partial class AssetPreview
{
    private async Task PlayHotaPierce()
    {
        try
        {
            var actor = _field.FindPawn(1)!;
            var hits = (_allEnemies ? new[] { 4, 5, 6 } : new[] { 4 }).Select(id => _field.FindPawn(id)!).ToArray();
            await _field.PlayFireAttack(actor, hits[0], hits, new(FireLevelLabels.Stage, _pierceLevel, 0), 1);
        }
        catch (Exception e)
        {
            GD.PushError(e.ToString());
            if (_capturing) GetTree().Quit(1);
        }
    }

    private async Task CapturePierce()
    {
        _capturing = true;
        string directory = ProjectSettings.GlobalizePath("res://../.tmp/asset-preview-captures");
        System.IO.Directory.CreateDirectory(directory);
        async Task Wait(double time) => await ToSignal(GetTree().CreateTimer(time), SceneTreeTimer.SignalName.Timeout);
        void CheckContacts(int count)
        {
            if (!_previewContacts.SequenceEqual(Enumerable.Range(4, count)))
                throw new InvalidOperationException("貫通の命中対象・回数が一致しません。");
            if (_pierceVariant != 0 && _field.AssetPreviewPierceImpacts != count)
                throw new InvalidOperationException("各敵への爆炎が一致しません。");
            if (_field.Pawns.Values.Any(p => p.Hp != 100))
                throw new InvalidOperationException("比較演出がHPを変更しました。");
        }
        await Wait(2);
        _speed = 0.25;
        Engine.TimeScale = _speed;
        foreach (int level in new[] { 2, 3 })
        foreach (int candidate in new[] { 0, 1, 2 })
        {
            _pierceLevel = level; _pierceVariant = candidate; _allEnemies = true;
            UpdateLabels();
            RestartEffects();
            await Wait((level == 3 ? 0.48 : 0.12) + 0.09);
            await SaveCapture($"{directory}/pierce-{level}-{(char)('A' + candidate)}-flight.png");
            TogglePause();
            int contacts = _previewContacts.Count;
            await ToSignal(GetTree().CreateTimer(0.2, ignoreTimeScale: true), SceneTreeTimer.SignalName.Timeout);
            if (_previewContacts.Count != contacts) throw new InvalidOperationException("停止中に貫通が命中しました。");
            TogglePause();
            await Wait(0.20);
            await SaveCapture($"{directory}/pierce-{level}-{(char)('A' + candidate)}-impact.png");
            await Wait(1.1);
            CheckContacts(3);
            if (_field.AssetPreviewEffects.GetChildCount() != 0)
                throw new InvalidOperationException("貫通エフェクトが残っています。");
        }
        _speed = 2;
        Engine.TimeScale = _speed;
        _allEnemies = false;
        RestartEffects();
        await Wait(2);
        CheckContacts(1);
        if (_field.AssetPreviewEffects.GetChildCount() != 0) throw new InvalidOperationException("倍速で粒子が残っています。");
        RestartEffects();
        await Wait(0.05);
        _pierceMode = false; _hotaMode = false; _burnMode = true; _burnOn = false;
        RestartEffects();
        await Wait(1.4);
        if (_previewContacts.Count != 0 || _field.AssetPreviewEffects.GetChildCount() != 0)
            throw new InvalidOperationException("中断した貫通攻撃が再生されました。");
        GD.Print("HOTA_PIERCE_CAPTURE_COMPLETE: 火勢2・3／3体・1体の命中／被弾通知／停止／倍速／中断／後片付けOK");
        GetTree().Quit();
    }
}
