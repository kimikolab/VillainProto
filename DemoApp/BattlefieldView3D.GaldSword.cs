using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// 抜剣済みのガルドだけが上段から斬り下ろす。戦闘判定は台本のまま、接触するコマまで表示を待つ。
public partial class BattlefieldView3D
{
    private int _galdSwordGeneration;
    internal int GaldAttackClipPlays { get; private set; }
    internal int GaldSwordFlashPlays { get; private set; }
    internal GaldSwordSlashFx? LastGaldSwordFlash { get; private set; }

    private async Task<bool> PlayGaldSwordClipToContact(BattlePawn3D actor, bool dying = false)
    {
        int generation = _galdSwordGeneration;
        bool Live() => generation == _galdSwordGeneration && IsInstanceValid(actor) && actor.IsInsideTree()
            && (actor.Hp > 0 || dying) && actor.CanPlayAttackClip && actor.SwordDrawn
            && !actor.QuietLastStand && IsVisibleInTree();
        if (!Live()) return false;
        if (BattlePawn3D.AttackClipOf("gald_cleave") is null) return true;
        GaldAttackClipPlays++;
        var contact = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = actor.PlayAttackClip("gald_cleave", () => contact.TrySetResult(), hold: 0.12);
        await contact.Task;
        return Live();
    }

    public async Task ShowSwordRiposte(BattlePawn3D? actor, IReadOnlyList<BattlePawn3D> targets, double speed, bool dying = false)
    {
        // 相打ちの台本はHP更新後・Death前に来る。Slot=1の表示指示だけを受け、死亡判定を推測しない。
        if (actor is null || targets.Count == 0 || !await PlayGaldSwordClipToContact(actor, dying)) return;
        var hits = targets.Where(p => IsInstanceValid(p) && p.IsInsideTree()).Distinct().ToArray();
        if (hits.Length == 0) return;
        ShowSwordSlash(actor, hits, speed, riposte: true);
    }

    private void ShowGaldSwordFlash(BattlePawn3D source, IReadOnlyList<BattlePawn3D> targets)
    {
        var hits = targets.Where(p => IsInstanceValid(p) && p.IsInsideTree()).Distinct().ToArray();
        if (hits.Length == 0) return;
        int generation = _galdSwordGeneration;
        var flash = new GaldSwordSlashFx { Name = "GaldSwordSlash" };
        _fxRoot.AddChild(flash);
        flash.Start(source.FxPoint, hits.Select(p => p.FxPoint).ToArray(), _camera,
            () => IsInstanceValid(source) ? source.AnimationSpeed : 1,
            () => generation == _galdSwordGeneration && IsInsideTree() && IsVisibleInTree());
        LastGaldSwordFlash = flash;
        GaldSwordFlashPlays++;
    }

    private void ResetGaldSwordPresentation()
    {
        _galdSwordGeneration++;
        foreach (var pawn in _pawns.Values)
            if (IsInstanceValid(pawn) && pawn.UnitId == "gald") pawn.CancelAttackClip();
        if (IsInstanceValid(_fxRoot))
            foreach (var flash in _fxRoot.GetChildren().OfType<GaldSwordSlashFx>()) flash.Cancel();
        LastGaldSwordFlash = null;
    }
}
