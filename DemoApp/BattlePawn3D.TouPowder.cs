using System;
using Godot;

public partial class BattlePawn3D
{
    internal int PowderDensity { get; private set; }
    internal bool PowderMarked { get; private set; }
    private int _powderBurnBodyGeneration;
    internal float PowderBurnBodyHeat { get; private set; }

    internal int BeginPowderBurnBody()
    {
        int generation = ++_powderBurnBodyGeneration;
        SetPowderBurnBody(generation, 1, 0);
        return generation;
    }

    internal void SetPowderBurnBody(int generation, float heat, float charAmount)
    {
        if (generation != _powderBurnBodyGeneration) return;
        PowderBurnBodyHeat = PresentationAlive ? heat : 0;
        _portraitMaterial?.SetShaderParameter("powder_burn_heat", PowderBurnBodyHeat);
        _portraitMaterial?.SetShaderParameter("powder_burn_char", PresentationAlive ? charAmount : 0);
    }

    private void ClearPowderBurnBody()
    {
        ++_powderBurnBodyGeneration;
        PowderBurnBodyHeat = 0;
        _portraitMaterial?.SetShaderParameter("powder_burn_heat", 0f);
        _portraitMaterial?.SetShaderParameter("powder_burn_char", 0f);
    }
    internal void SetPowderDensity(int amount) => PowderDensity = PresentationAlive ? Math.Max(0, amount) : 0;
    internal void SetPowderMark() => PowderMarked = PresentationAlive;
    internal void ClearTouPowder() { PowderDensity = 0; PowderMarked = false; ClearPowderBurnBody(); }
}
