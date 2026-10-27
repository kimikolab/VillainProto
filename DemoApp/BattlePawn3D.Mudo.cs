using Godot;

public partial class BattlePawn3D
{
    internal bool MudoErupting { get; private set; }

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
        // 頭〜足の実画素高（通常1413px・怒り963px）を揃える。腕だけが大きくなる。
        height *= 1166f / 1515f * 1413f / 963f;
        padding = UiKit.BattlePortraitBottomPaddingRatio(key);
    }
}
