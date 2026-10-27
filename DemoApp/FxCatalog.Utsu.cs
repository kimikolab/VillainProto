using BattleCore;
using System.Collections.Generic;
using System.Linq;

public static partial class FxCatalog
{
    private static IEnumerable<FxCue> UtsuCues()
    {
        for (int count = 2; count <= 5; count++)
        {
            int hits = count;
            yield return new FxCue($"utsu.dance{count}", $"ウツ 双剣の演舞・{count}連撃", "呪い・双剣の連撃",
                new[] { "utsu" }, FxCueKind.Scripted, UtsuOpenings, () => UtsuEvents(hits));
        }
        yield return new FxCue("utsu.live", "ウツ 双剣の演舞（実戦）", "呪い・双剣の連撃", new[] { "utsu", "nel" },
            FxCueKind.Live, BuildFormation: () => Presets.Compare.Single(p => p.Name == "逆しま (ネル×ウツ)").F,
            Stage: 4, Seed: 7, Highlight: e => e.Kind == BattleEventKind.Attack);
    }

    internal static DemoOpening[] UtsuOpenings() => new[] {
        new DemoOpening(1, 0, "utsu", "逆しまのウツ", 0, 300, 300, 40, AttackPattern.Single, true),
        new DemoOpening(2, 1, "knight", "演舞の標的1", 0, 1000, 1000, 10, AttackPattern.Single, true),
        new DemoOpening(3, 1, "knight", "演舞の標的2", 2, 1000, 1000, 10, AttackPattern.Single, true),
    };

    // 表示用の固定台本。打数を増やす条件・攻撃の判定はここでは作らない。
    internal static BattleEvent[] UtsuEvents(int count, bool changeTargets = false)
    {
        var events = new List<BattleEvent> { new() { Turn = 1, Kind = BattleEventKind.TurnStart } };
        var hp = new Dictionary<int, int> { [2] = 1000, [3] = 1000 };
        for (int k = 0; k < count; k++)
        {
            int target = changeTargets && k % 2 == 1 ? 3 : 2;
            hp[target] -= 40;
            events.Add(new() { Turn = 1, Kind = BattleEventKind.Attack, ActorId = 1, TargetId = target, Amount = 40, Pattern = AttackPattern.Single });
            events.Add(new() { Turn = 1, Kind = BattleEventKind.Damage, ActorId = 1, TargetId = target, Amount = 40, HpAfter = hp[target], Pattern = AttackPattern.Single });
        }
        events.Add(new() { Turn = 2, Kind = BattleEventKind.TurnStart });
        return events.ToArray();
    }
}
