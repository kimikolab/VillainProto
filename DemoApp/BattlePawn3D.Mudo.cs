using Godot;
using System;

public partial class BattlePawn3D
{
    internal bool MudoErupting { get; private set; }
    internal bool MudoIdlePaused { get; private set; }

    internal void SetMudoIdleClock(bool paused, double speed)
    {
        if (_unitId != "mudo") return;
        MudoIdlePaused = paused;
        AnimationSpeed = Math.Max(0.1, speed);
    }

    internal void SetMudoErupting(bool active)
    {
        bool next = active && _unitId == "mudo" && _alive && !_victory;
        if (MudoErupting == next) return;
        MudoErupting = next;
        RefreshBattlePortrait();
    }

    private void ApplyMudoPortrait(ref string key, ref float height, ref float padding)
    {
        if (_unitId != "mudo" || !MudoErupting || !_alive) return;
        key = "mudo_erupt";
        // 外套のフード〜泥だまり（通常1335px・怒り933px）を揃え、拳だけを巨大化する。
        // 掲げた拳はフードより高いため、画像全体の輪郭高では合わせない。
        height *= 1024f / 1515f * 1335f / 933f;
        padding = UiKit.BattlePortraitBottomPaddingRatio(key);
    }
}
