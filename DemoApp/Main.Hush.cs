using BattleCore;
using System.Threading.Tasks;

public partial class Main
{
    private HushPresentation _hush = new();
    private async Task<bool> PlayHush(BattleEvent e, int index, BattlePawn3D? actor, BattlePawn3D? target)
    {
        if (e.Kind == BattleEventKind.HushState)
        {
            await _battleField.ShowHushState(actor, target, e, _speed);
            return true;
        }
        if (_hush.Ripostes.TryGetValue(index, out int victim))
            await _battleField.ShowKnightRiposte(actor, _battleField.FindPawn(victim), _speed);
        return false; // HP・死亡は必ず元のDamage/Deathへ渡す。
    }
}
