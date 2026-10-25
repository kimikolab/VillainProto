using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

// 見せ場の件数と再生に同じ実台本を使う。駒の個体IDは Materialize 後の写しから引く。
internal sealed record FxLivePlayback(IReadOnlyList<DemoOpening> Openings, BattleResult Result)
{
    internal DeathPresentation? Death { get; init; }
    internal static FxLivePlayback Create(FxCue cue)
    {
        if (!FxCatalog.TryGetPreset(cue, out var formation))
            throw new InvalidOperationException($"プリセットが見つかりません: {cue.PresetName}");
        var players = BattleEngine.Materialize(formation, 0);
        var enemies = cue.PlaytestWave is int wave
            ? BattleEngine.MaterializeEnemy(EnemyCatalog.PlaytestStages[wave].Enemy, EnemyCatalog.PlaytestStages[wave].Scale)
            : BattleEngine.Materialize(EnemyCatalog.Stages[cue.Stage].Enemy, 1);
        // 個体IDは engine の Add が振る。開始時の値を控え、IDだけは Run 後に読む（Main と同じ）。
        var pending = players.Concat(enemies).Select(u => new {
            Unit = u, u.Slot, u.Hp, u.MaxHp, Attack = u.CurrentAttack, Pattern = u.CurrentPattern,
        }).ToArray();
        var death = DeathPresentation.Record(players, enemies, cue.Seed, out var result);
        var openings = pending.Select(x => new DemoOpening(
            x.Unit.InstanceId, x.Unit.TeamId, x.Unit.Def.Id, x.Unit.Def.Name, x.Slot, x.Hp, x.MaxHp,
            x.Attack, x.Pattern, x.Unit.Def.Advances, x.Unit.Def.Traits, x.Unit.Shape)).ToArray();
        return new(openings, result) { Death = death };
    }

    internal int[] Highlights(FxCue cue)
    {
        var ids = Openings.Where(o => cue.Subject is { } subject
            ? o.UnitId == subject.Definition.Id && o.Team == (subject.Enemy ? 1 : 0)
            : cue.Units.Contains(o.UnitId)).Select(o => o.InstanceId).ToHashSet();
        bool Matches(BattleEvent e) => cue.Subject is not null
            ? e.Kind is not (BattleEventKind.TurnStart or BattleEventKind.StatSnapshot or BattleEventKind.StatusSnapshot)
                && (e.ActorId is int actor && ids.Contains(actor) || e.TargetId is int target && ids.Contains(target))
            : cue.Highlight?.Invoke(e) ?? (e.ActorId is int writer && ids.Contains(writer));
        return Enumerable.Range(0, Result.Events.Count).Where(i => Matches(Result.Events[i])).ToArray();
    }
}
