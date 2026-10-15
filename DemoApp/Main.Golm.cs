using BattleCore;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class Main
{
    private GolmPresentation _golm = new();
    private bool _golmFastEvent;

    private async Task<bool> PlayGolm(BattleEvent e, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (e.Kind is not (BattleEventKind.GolmArm or BattleEventKind.Swallow)) return false;
        int token = _playToken;
        _battleField.SetGolmClock(_paused, _speed);
        await _battleField.ShowGolmCue(actor, target, e);
        if (token != _playToken || !IsInstanceValid(this) || !IsInsideTree() || !_battleMode) return true;
        if (target is not null && e.Kind == BattleEventKind.Swallow)
        {
            var parts = GolmPresentation.ParseSpit(e.Text);
            foreach (var part in parts)
            {
                // 吐く通知の量は表示上の差分。実盤面へは書かず、次の写しで残量に置き換わる。
                string? key = part.Label switch { "毒" => StatusKeys.Poison, "火" => StatusKeys.Burn,
                    "雷" => StatusKeys.Shock, "標" => StatusKeys.Marked, _ => null };
                if (key is null) continue;
                string label = DisplayStatusKey(key);
                int before = _statusByPawn.GetValueOrDefault(target.InstanceId)?.GetValueOrDefault(label) ?? 0;
                int amount = before + part.Amount;
                if (key == StatusKeys.Poison) { target.AddPoisonIconAmount(part.Amount); target.SetPoisoned(true); amount = target.PoisonIconAmount; }
                else if (key == StatusKeys.Marked) target.SetMarkLayers(Math.Max(before, target.MarkLayers) + part.Amount);
                else if (key == StatusKeys.Burn) target.SetBurning(true);
                if (key == StatusKeys.Shock) amount = 1;
                target.SetStatusIcon(key, true);
                SetDisplayedStatus(target, label, amount);
            }
            if (parts.Count > 0)
                _battleField.Float(target, string.Join("  ", parts.Select(p => $"{p.Label}＋{p.Amount}")), GolmFx3D.Soul, true);
        }
        AppendLog($"  [color=#bcb1ff]{NameOf(e.ActorId)} — {e.Text}[/color]"
            + (e.TargetId is null ? "" : $" → {NameOf(e.TargetId)}"));
        return true;
    }
}
