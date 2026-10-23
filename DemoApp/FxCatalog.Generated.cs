using BattleCore;
using System;
using System.Collections.Generic;
using System.Linq;

public static partial class FxCatalog
{
    private static readonly Dictionary<string, string> GenerationFailures = new();
    internal static IReadOnlyDictionary<string, string> GeneratedErrors => GenerationFailures;
    internal static IEnumerable<UnitDef> EnemyUnits => EnemyCatalog.Stages
        .SelectMany(s => s.Enemy.Occupied().Select(p => p.Def)).DistinctBy(d => d.Id);

    public static IEnumerable<FxCue> AllUnits()
    {
        foreach (var subject in UnitCatalog.All.Select(d => new FxUnit(d, false))
            .Concat(EnemyUnits.Select(d => new FxUnit(d, true))))
        {
            FxCue[] cues;
            try { cues = ForUnit(subject.Definition, subject.Enemy).ToArray(); }
            catch (Exception e)
            {
                GenerationFailures[subject.Definition.Id] = e.Message;
                continue;
            }
            foreach (var cue in cues) yield return cue;
        }
    }

    public static IEnumerable<FxCue> ForUnit(UnitDef d, bool enemy)
    {
        var subject = new FxUnit(d, enemy);
        string id = $"unit.{d.Id}";
        FxCue Basic(string suffix, string title) => new($"{id}.{suffix}", title, "駒カタログ",
            new[] { d.Id }, FxCueKind.Scripted, () => BasicOpenings(d, enemy),
            () => BasicEvents(d, suffix), Subject: subject, HoldSeconds: suffix == "idle" ? 3 : 0);
        yield return Basic("idle", "待機");
        yield return Basic("attack", $"攻撃（{UiKit.PatternLabel(d.Pattern)}）");
        yield return Basic("hit", "被弾");
        yield return Basic("death", "撃破");

        // 敵は実際に出現する最初の波を1本。味方は第1・第2波を各1本。
        var stages = enemy
            ? Enumerable.Range(0, EnemyCatalog.Stages.Count)
                .Where(i => EnemyCatalog.Stages[i].Enemy.Occupied().Any(p => p.Def.Id == d.Id)).Take(1)
            : Enumerable.Range(0, Math.Min(2, EnemyCatalog.Stages.Count));
        foreach (int stage in stages)
            yield return new FxCue($"{id}.live.{stage}", $"実戦: 手番と特殊（第{stage + 1}波）", "駒カタログ",
                new[] { d.Id }, FxCueKind.Live, Stage: stage, Seed: 0, Subject: subject,
                BuildFormation: () => enemy ? Presets.Playtest.First().F.Clone() : FormationFor(d));
    }

    private static Formation FormationFor(UnitDef d)
    {
        var row = Presets.Playtest.Concat(Presets.Versions).Concat(Presets.Compare)
            .FirstOrDefault(r => r.F.Occupied().Any(p => p.Def.Id == d.Id));
        if (row.F is not null) return row.F.Clone();
        var rest = UnitCatalog.All.Where(u => u.Id != d.Id).Take(4).ToArray();
        return Formation.Build(front1: rest.ElementAtOrDefault(0), front3: rest.ElementAtOrDefault(1),
            center: d, back1: rest.ElementAtOrDefault(2), back3: rest.ElementAtOrDefault(3));
    }

    private static DemoOpening[] BasicOpenings(UnitDef d, bool enemy)
    {
        int team = enemy ? 1 : 0;
        var knight = EnemyCatalog.Knight;
        return new[] {
            new DemoOpening(1, team, d.Id, d.Name, 2, d.MaxHp, d.MaxHp, d.Attack, d.Pattern, d.Advances, d.Traits),
            new DemoOpening(2, 1-team, knight.Id, knight.Name, 0, knight.MaxHp, knight.MaxHp, knight.Attack, knight.Pattern, knight.Advances),
            new DemoOpening(3, 1-team, knight.Id, knight.Name, 2, knight.MaxHp, knight.MaxHp, knight.Attack, knight.Pattern, knight.Advances),
            new DemoOpening(4, 1-team, knight.Id, knight.Name, 4, knight.MaxHp, knight.MaxHp, knight.Attack, knight.Pattern, knight.Advances),
        };
    }

    private static BattleEvent[] BasicEvents(UnitDef d, string kind)
    {
        if (kind == "idle") return Array.Empty<BattleEvent>();
        bool attacking = kind == "attack";
        int actor = attacking ? 1 : 3, target = attacking ? 3 : 1;
        int amount = attacking ? d.Attack : kind == "death" ? d.MaxHp : 10;
        var pattern = attacking ? d.Pattern : AttackPattern.Single;
        var events = new List<BattleEvent> {
            new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = actor, TargetId = target,
                Amount = amount, Pattern = pattern },
        };
        // 固定の試写用の対象列。威力の減衰・標的選択などの戦闘判定は作らない。
        var targets = new[] { (Id: target, Hp: attacking ? EnemyCatalog.Knight.MaxHp : d.MaxHp) };
        if (attacking && pattern is AttackPattern.Sweep or AttackPattern.All)
            targets = new[] { (3, EnemyCatalog.Knight.MaxHp), (2, EnemyCatalog.Knight.MaxHp), (4, EnemyCatalog.Knight.MaxHp) };
        else if (attacking && pattern == AttackPattern.Pierce)
        {
            var pawns = BasicOpenings(d, false).Where(o => o.InstanceId != 1).ToArray();
            targets = FormationRules.LanePath(FormationRules.LanesOf(2)[0])
                .SelectMany(slot => pawns.Where(o => o.Slot == slot)).Select(o => (o.InstanceId, o.Hp)).ToArray();
        }
        foreach (var hit in targets)
            events.Add(new BattleEvent { Turn = 1, Kind = BattleEventKind.Damage, ActorId = actor,
                TargetId = hit.Id, Pattern = pattern, Amount = amount, HpAfter = Math.Max(0, hit.Hp - amount) });
        if (kind == "death")
            events.Add(new BattleEvent { Turn = 1, Kind = BattleEventKind.Death, ActorId = actor, TargetId = target });
        return events.ToArray();
    }
}
