using Godot;

public partial class BattlePawn3D
{
    private KataStakes3D? _kataStakes;
    internal int KataAvailableStakes => _kataStakes?.Available ?? 0;
    internal bool KataStakesVisible => _kataStakes?.Visible == true;
    private void BuildKataStakes()
    {
        if (_unitId != "kata") return;
        _kataStakes = new KataStakes3D(); AddChild(_kataStakes);
        _kataStakes.Configure(Team != 0);
    }
    public bool LaunchKataStake(Vector3 target,double speed) => _kataStakes?.Launch(target,speed) == true;
    private void UpdateKataStakes() => _kataStakes?.SetActive(_alive && !_victory);
}
