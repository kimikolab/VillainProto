using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public partial class BattlefieldView3D
{
    internal int RicaAttackClipPlays { get; private set; }
    internal readonly List<string> RicaPlayedClips = new();
    internal readonly List<string> RicaLinksPlayed = new();
    internal int RicaSlashCount => _fxRoot.GetChildren().OfType<RicaSlashFx3D>().Count();
    internal int RicaBluntCount => _fxRoot.GetChildren().OfType<RicaSlashFx3D>().Count(f=>f.IsBlunt);
    internal int RicaReturnContactCount => _fxRoot.GetChildren().OfType<RicaSlashFx3D>().Count(f=>f.IsBladeContact);
    internal Vector3 RicaReturnContactPoint { get; private set; }
    internal void FinishComboMotion(BattlePawn3D pawn) => ReturnAfterAttackClip(pawn);
    internal async Task FinishRicaMotions()
    {
        int generation = _specialGeneration;
        foreach (var pawn in _pawns.Values)
        {
            if (pawn.UnitId == "rica" && pawn.AttackClipPlaying) await pawn.AttackClipFinished;
            if (generation != _specialGeneration) return;
        }
    }

    private async Task<bool> PlayRicaClipToContact(BattlePawn3D pawn, bool first, int generation, bool hold)
    {
        bool Live() => generation == _specialGeneration && IsInstanceValid(pawn) && pawn.IsInsideTree()
            && pawn.CanPlayAttackClip && pawn.DeathActive && IsVisibleInTree();
        if (!Live()) return false;
        if (pawn.AttackClipPlaying) await pawn.AttackClipFinished;
        if (!Live()) return false;
        string name = pawn.NextRicaClip(first);
        if (BattlePawn3D.AttackClipOf(name) is null) return true;
        RicaAttackClipPlays++; RicaPlayedClips.Add(name);
        var contact = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = pawn.PlayRicaClip(name, () => contact.TrySetResult(), hold);
        await contact.Task;
        return Live();
    }
}
