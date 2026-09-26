using Godot;
using System;

public partial class BattlefieldView3D
{
    public void PlayPlankPasteSound() => _attackAudio.PlayPlankPaste();
    public void PlayPlankSkillUpSound() => _attackAudio.PlayPlankSkillUp();
    public void PlayPlankRushSound() => _attackAudio.PlayPlankRush();
    public void PlayPlankOpeningSound() => _attackAudio.PlayPlankOpening();
    public void PlayPlankReflectSound(int amount = 13) => _attackAudio.PlayPlankReflect(amount);
    public void PlankReflect(Vector3 from, Vector3 to, int amount, double speed)
        => PlankFx.Reflect(_fxRoot, from, to, amount, speed);
    public void PlankReflectImpact(BattlePawn3D pawn, int amount, double speed)
        => PlankFx.ReflectImpact(_fxRoot, pawn.FxPoint, amount, speed);
    public void PlankCraftSpark(BattlePawn3D pawn, int tier, double speed)
        => PlankFx.Burst(_fxRoot, pawn.ScrapPoint, 6, 0.22 / speed);
    public void PlankFlight(Vector3 from, Vector3 to, int amount, double seconds, bool arc = false)
        => PlankFx.Fly(_fxRoot, from, to, amount, seconds, arc);
    public void PlankImpact(BattlePawn3D pawn, int amount, double speed, bool fire = false)
        => PlankFx.Burst(_fxRoot, pawn.FxPoint, amount, 0.28 / speed, fire);
}
