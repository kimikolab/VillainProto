public partial class BattlePawn3D
{
    public bool SekkiWrapping { get; private set; }

    public void SetSekkiWrapping(bool active)
    {
        bool next = active && _unitId == "sekki" && _alive && !_victory;
        if (SekkiWrapping == next) return;
        SekkiWrapping = next;
        RefreshBattlePortrait();
    }
}
